#nullable enable
using System;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// How <see cref="PawnPose"/> finds the colonists worth sidestepping. The control for
    /// <c>FrameTimeTests.TheCrowdScanCostsWhatItVisits</c>, and the three arms are three ways of
    /// getting <b>the same answer</b> rather than three behaviours.
    ///
    /// <para><b>Three rather than two, deliberately</b> (<c>docs/plans/pf-crowd-scan.md</c>): the
    /// plan named two candidates and warned against building a spatial index on top of an
    /// unmeasured constant factor. <see cref="Cached"/> is that constant factor on its own — the
    /// same N-squared visit with <see cref="SteeringCurve.WhereItIsNow"/> hoisted out of the inner
    /// loop — so one run says how much of the cost was the recompute and how much was the
    /// quadratic, instead of attributing all of it to whichever was built.</para>
    /// </summary>
    public enum CrowdScan
    {
        /// <summary>Walk the whole span, recomputing every other pawn's position for every posed
        /// pawn. What shipped until 2026-09-23, and the before in every measurement.</summary>
        Span,

        /// <summary>Walk the whole span, but read each pawn's position from the index's cache:
        /// N distinct answers computed once instead of N-squared times.</summary>
        Cached,

        /// <summary>Visit only the buckets that can hold a pawn within
        /// <see cref="SteeringCurve.CrowdFarRadius"/>.</summary>
        Bucketed,
    }

    /// <summary>
    /// Where every pawn is this frame, bucketed so that <see cref="PawnPose"/> can ask "who is
    /// within three metres of here" without walking the colony.
    ///
    /// <para><b>Why this is allowed to exist at all.</b> The sidestep is judged and settled
    /// (<c>docs/design/25-pawn-steering.md</c>) and nothing here is permitted to change it by so
    /// much as a bit. It is only safe because the cull is <b>exact</b>:
    /// <see cref="SteeringCurve.Proximity"/> is <c>SmoothStep((3.0 - d) / 1.5)</c>, which returns
    /// exactly <c>0f</c> at and beyond <see cref="SteeringCurve.CrowdFarRadius"/>, and the loop
    /// already <c>continue</c>s on <c>near &lt;= 0f</c>. So every pair this index skips is a pair
    /// the old loop visited and threw away. The reduction is a <c>max</c>, which is
    /// order-independent for floats, so visiting the survivors in a different order gives the
    /// identical result — not a similar pose, the same one.
    /// <c>PawnCrowdIndexTests.EveryScanModeDrawsTheIdenticalPose</c> is that claim pinned.</para>
    ///
    /// <para><b>Nothing here is in a cell, a save or the state hash.</b> It is rebuilt from the
    /// published snapshot once a frame and thrown away, exactly like every other presentation
    /// mirror. See <c>CLAUDE.md</c>, "Nothing in presentation is in a cell, a save or the
    /// hash".</para>
    ///
    /// <para><b>Bucket size is the radius, not the cell.</b> 3.0 m, which is
    /// <see cref="SteeringCurve.CrowdFarRadius"/> and also exactly one cell layer
    /// (<c>CellMetrics.SizeY</c>), against a cell 2.5 m square. At bucket size <c>B &gt;= r</c> the
    /// 3 x 3 x 3 block around a query point provably contains every pawn within <c>r</c>: for
    /// <c>p</c> in <c>[Bb, Bb+B)</c> and <c>|q - p| &lt;= r &lt;= B</c>, <c>floor(q/B)</c> lies in
    /// <c>[b-1, b+1]</c>. Keying on the cell instead would need a fourth neighbour in x and z,
    /// because 2.5 m buckets do not cover 3 m in one step.</para>
    ///
    /// <para><b>The vertical is a real axis here, not padding.</b> Distance is three-dimensional on
    /// purpose — on a board of 3 m terrace risers a colonist on the storey above was nought metres
    /// away under the old x/z measure and got the full sidestep (design 25 section 2). A 3.0 m
    /// bucket in y keeps that exactly: one layer up is one bucket up, and is reached.</para>
    /// </summary>
    public sealed class PawnCrowdIndex
    {
        /// <summary>
        /// Which scan <see cref="PawnPose"/> uses. Static because it is a measurement control and
        /// has to be switchable from a test between two timed stretches of the same run, which is
        /// the only comparison this machine supports (<c>docs/design/06-rendering-and-camera.md</c>
        /// section 6c.1). It is never changed in play.
        /// </summary>
        public static CrowdScan Mode = CrowdScan.Bucketed;

        /// <summary>The side of one bucket, in metres. See the class remarks for why it is the
        /// crowd radius and not the cell size.</summary>
        public const float BucketMetres = SteeringCurve.CrowdFarRadius;

        const float InverseBucket = 1f / BucketMetres;

        int _count;
        Vector3[] _position = Array.Empty<Vector3>();
        int[] _slotOf = Array.Empty<int>();
        int[] _items = Array.Empty<int>();

        // An open-addressed table of occupied buckets, linear probing, capacity a power of two.
        // Sized to the colony rather than to the board: a dense grid over 120 x 120 x 16 cells
        // would be 160,000 buckets to clear every frame for a colony of fifty.
        int _mask;
        int[] _keyX = Array.Empty<int>();
        int[] _keyY = Array.Empty<int>();
        int[] _keyZ = Array.Empty<int>();
        int[] _stamp = Array.Empty<int>();
        int[] _start = Array.Empty<int>();
        int[] _size = Array.Empty<int>();
        int[] _cursor = Array.Empty<int>();

        /// <summary>
        /// Which rebuild the stamps belong to, so a rebuild does not have to clear the table.
        /// Bumped once per <see cref="Rebuild"/>; a slot whose stamp is older is free.
        /// </summary>
        int _generation;

        /// <summary>How many pawns the last <see cref="Rebuild"/> saw.</summary>
        public int Count => _count;

        /// <summary>
        /// Where pawn <paramref name="i"/> of the rebuilt span is at this instant — the cached
        /// <see cref="SteeringCurve.WhereItIsNow"/>, computed once per pawn per frame instead of
        /// once per pair.
        /// </summary>
        public Vector3 PositionAt(int i) => _position[i];

        /// <summary>
        /// Take the frame's positions and bucket them. O(N), no allocation once the arrays have
        /// grown to the colony.
        ///
        /// <para><b>Called once a frame, from the composition root, before anything poses a
        /// pawn</b> — <c>OdysseyBootstrap.LateUpdate</c>. Both callers of
        /// <see cref="PawnPose.Of"/> that pass a span share this one index; building it in either
        /// of them would build it twice and leave the two able to disagree, which is the fault
        /// <see cref="PawnPose"/> itself exists to prevent.</para>
        /// </summary>
        public void Rebuild(ReadOnlySpan<PawnView> pawns)
        {
            _count = pawns.Length;
            if (_position.Length < _count)
            {
                int room = Mathf.NextPowerOfTwo(Mathf.Max(16, _count));
                _position = new Vector3[room];
                _slotOf = new int[room];
                _items = new int[room];
            }

            // Two slots a pawn keeps the probe short; a table this size is cheap to walk in the
            // prefix-sum pass below.
            //
            // **It grows and never shrinks**, which is not laziness. Sizing it to the colony every
            // frame would reallocate all seven arrays whenever the colony crossed a power of two —
            // and a colony oscillating across one (a birth and a death either side of 128) would
            // do it every frame, for ever. The cost of keeping the larger table is one walk of it
            // in the prefix-sum pass, which is a few thousand integer compares.
            int wanted = Mathf.NextPowerOfTwo(Mathf.Max(16, _count * 2));
            int capacity = Mathf.Max(wanted, _stamp.Length);
            if (_stamp.Length < capacity)
            {
                _keyX = new int[capacity];
                _keyY = new int[capacity];
                _keyZ = new int[capacity];
                _stamp = new int[capacity];
                _start = new int[capacity];
                _size = new int[capacity];
                _cursor = new int[capacity];
                _generation = 0;
            }
            _mask = capacity - 1;

            // A fresh generation retires every slot without touching the table. Wrapping would
            // make an ancient stamp look current, so the one time it wraps the table is cleared.
            _generation++;
            if (_generation == int.MaxValue)
            {
                Array.Clear(_stamp, 0, _stamp.Length);
                _generation = 1;
            }

            // Pass one: everybody's position, and the bucket it falls in.
            for (int i = 0; i < _count; i++)
            {
                Vector3 at = SteeringCurve.WhereItIsNow(in pawns[i]);
                _position[i] = at;
                int slot = Claim(BucketOf(at.x), BucketOf(at.y), BucketOf(at.z));
                _slotOf[i] = slot;
                _size[slot]++;
            }

            // Pass two: lay the buckets out end to end.
            int running = 0;
            for (int slot = 0; slot <= _mask; slot++)
            {
                if (_stamp[slot] != _generation) continue;
                _start[slot] = running;
                _cursor[slot] = 0;
                running += _size[slot];
            }

            // Pass three: drop each pawn into its bucket's run.
            for (int i = 0; i < _count; i++)
            {
                int slot = _slotOf[i];
                _items[_start[slot] + _cursor[slot]++] = i;
            }
        }

        /// <summary>Which bucket a world coordinate falls in. Floor, so it is continuous across
        /// zero — <c>(int)</c> truncates towards zero and would give bucket 0 two units wide.</summary>
        static int BucketOf(float metres) => Mathf.FloorToInt(metres * InverseBucket);

        /// <summary>The slot for this bucket, claiming a free one if it is the first pawn in it.</summary>
        int Claim(int bx, int by, int bz)
        {
            int slot = Hash(bx, by, bz) & _mask;
            while (true)
            {
                if (_stamp[slot] != _generation)
                {
                    _stamp[slot] = _generation;
                    _keyX[slot] = bx;
                    _keyY[slot] = by;
                    _keyZ[slot] = bz;
                    _size[slot] = 0;
                    return slot;
                }
                if (_keyX[slot] == bx && _keyY[slot] == by && _keyZ[slot] == bz) return slot;
                slot = (slot + 1) & _mask;
            }
        }

        /// <summary>The slot for this bucket, or false if no pawn is in it.</summary>
        bool Find(int bx, int by, int bz, out int found)
        {
            int slot = Hash(bx, by, bz) & _mask;
            while (true)
            {
                if (_stamp[slot] != _generation) { found = -1; return false; }
                if (_keyX[slot] == bx && _keyY[slot] == by && _keyZ[slot] == bz)
                {
                    found = slot;
                    return true;
                }
                slot = (slot + 1) & _mask;
            }
        }

        static int Hash(int bx, int by, int bz)
        {
            unchecked
            {
                uint h = (uint)bx * 73856093u ^ (uint)by * 19349663u ^ (uint)bz * 83492791u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// Every pawn that could be within <see cref="SteeringCurve.CrowdFarRadius"/> of
        /// <paramref name="at"/>, as indices into the span this index was rebuilt from.
        ///
        /// <para>A superset, not a filtered answer: the caller still measures the distance, which
        /// is what keeps the arithmetic identical to the plain scan. What it guarantees is that
        /// nobody inside the radius is missing.</para>
        /// </summary>
        public Neighbourhood Near(Vector3 at) => new Neighbourhood(this, at, ownBucketOnly: false);

        /// <summary>
        /// Every pawn in the one bucket <paramref name="at"/> falls in — no neighbours.
        ///
        /// <para><b>For the stand-apart rule, and exact for it</b> (design 25 §10): the pawns it
        /// wants are the ones standing on the same cell, and a standing pawn's cached position is
        /// its cell's <c>FloorCentre</c>, computed by the same arithmetic for every pawn on that
        /// cell. Identical floats fall in the identical bucket, so one bucket holds all of them —
        /// one probe where <see cref="Near"/> makes twenty-seven. The caller still checks the cell,
        /// because a bucket 3 m across holds several.</para>
        /// </summary>
        public Neighbourhood Here(Vector3 at) => new Neighbourhood(this, at, ownBucketOnly: true);

        /// <summary>The 3 x 3 x 3 block of buckets around a point, or only the middle one of it.
        /// A struct with a struct enumerator so a <c>foreach</c> in the pose loop allocates
        /// nothing.</summary>
        public readonly struct Neighbourhood
        {
            readonly PawnCrowdIndex _index;
            readonly int _bx, _by, _bz;
            readonly bool _ownBucketOnly;

            internal Neighbourhood(PawnCrowdIndex index, Vector3 at, bool ownBucketOnly)
            {
                _index = index;
                _bx = BucketOf(at.x);
                _by = BucketOf(at.y);
                _bz = BucketOf(at.z);
                _ownBucketOnly = ownBucketOnly;
            }

            public Enumerator GetEnumerator() => new Enumerator(_index, _bx, _by, _bz, _ownBucketOnly);
        }

        /// <summary>See <see cref="Neighbourhood"/>.</summary>
        public struct Enumerator
        {
            readonly PawnCrowdIndex _index;
            readonly int _bx, _by, _bz;
            int _neighbour;
            readonly int _lastNeighbour;
            int _at;
            int _end;

            internal Enumerator(PawnCrowdIndex index, int bx, int by, int bz, bool ownBucketOnly)
            {
                _index = index;
                _bx = bx;
                _by = by;
                _bz = bz;
                // Neighbour 13 is (0, 0, 0) in the k % 3, k / 3 % 3, k / 9 unpacking below: the
                // bucket itself. Starting there and stopping after it visits that bucket alone.
                _neighbour = ownBucketOnly ? 13 : 0;
                _lastNeighbour = ownBucketOnly ? 14 : 27;
                _at = 0;
                _end = 0;
                Current = -1;
            }

            public int Current { get; private set; }

            public bool MoveNext()
            {
                while (true)
                {
                    if (_at < _end)
                    {
                        Current = _index._items[_at++];
                        return true;
                    }
                    if (_neighbour >= _lastNeighbour) return false;

                    int k = _neighbour++;
                    int dx = k % 3 - 1;
                    int dy = k / 3 % 3 - 1;
                    int dz = k / 9 - 1;
                    if (_index.Find(_bx + dx, _by + dy, _bz + dz, out int slot))
                    {
                        _at = _index._start[slot];
                        _end = _at + _index._size[slot];
                    }
                }
            }
        }
    }
}
