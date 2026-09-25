#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <b>Blood, drawn</b> (design 33 §10). What the blood seam hands on becomes drops thrown from
    /// the wound, one mark on the ground where the lead drop lands, and a pool that waits for a
    /// body to fall and then spreads under it.
    ///
    /// <para><b>Presentation only, for ever</b> (§7d point 5): nothing here is in a cell, a save or
    /// the hash, and nothing in the simulation hears of it. A world change clears it.</para>
    ///
    /// <para><b>Timed by the tick where it lasts and by the frame where it moves.</b> A mark's fade
    /// and a pool's spread are measured in ticks from the tick they were made (§7d point 3). The
    /// drops and a pool's wait for the fall move by the frame's seconds while the game runs, like
    /// the floating words, and hold on a pause.</para>
    ///
    /// <para><b>Draws in fade steps, never in marks</b> (P10): marks are bucketed by shape and fade
    /// step and each bucket is one instanced call, so the ceiling is three shapes by
    /// <see cref="BloodLedger.FadeSteps"/> plus one for the drops, whatever the fight. Per frame it
    /// walks the marks (at most <see cref="BloodLedger.Cap"/>) and the drops (at most
    /// <see cref="MaxDrops"/>) and nothing else (<c>process.md</c> §3).</para>
    /// </summary>
    public sealed class BloodDirector : IBloodEffects
    {
        /// <summary>The most drops in the air at once. A spurt that finds no room lays its mark at once instead.</summary>
        public const int MaxDrops = 512;

        /// <summary>The most pools waiting for a body to fall. One that finds no room is laid at once, at the feet.</summary>
        public const int MaxWaitingPools = 64;

        /// <summary>Metres a second a second.</summary>
        public const float Gravity = 9.81f;

        /// <summary>A drop in the air, drawn as a small cube this many metres a side.</summary>
        public const float DropSize = 0.045f;

        /// <summary>How far above the ground a hit's mark is drawn: over a pool, over an order's plate (8 mm).</summary>
        public const float MarkLift = 0.016f;

        /// <summary>How far above the ground a pool is drawn: under a hit's mark.</summary>
        public const float PoolLift = 0.012f;

        /// <summary>A hit's mark at full strength. INVENTED.</summary>
        public static readonly Color MarkColour = new Color(0.42f, 0.03f, 0.04f, 0.86f);

        /// <summary>A pool at full strength: darker than a fresh mark. INVENTED.</summary>
        public static readonly Color PoolColour = new Color(0.28f, 0.02f, 0.03f, 0.9f);

        /// <summary>A drop in the air. INVENTED.</summary>
        public static readonly Color DropColour = new Color(0.52f, 0.04f, 0.05f, 0.95f);

        /// <summary>Where a body lies, for the pool under it: the middle of its drawn figure, or no answer.</summary>
        public delegate bool BodyFinder(PawnId who, out Vector3 middle);

        struct Drop
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public int Layer;
            public bool Lead;
            public BloodShape Shape;
            public float Radius;
            public float Stretch;
            public float Yaw;
        }

        struct WaitingPool
        {
            public PawnId Who;
            public Vector3 Feet;
            public int Layer;
            public float Radius;
            public float Wait;
        }

        const int Shapes = 3;
        const int Buckets = Shapes * BloodLedger.FadeSteps;

        readonly WorldRenderModel _model;
        readonly BodyFinder? _findBody;
        readonly BloodLedger _ledger = new BloodLedger();
        readonly Drop[] _drops = new Drop[MaxDrops];
        readonly WaitingPool[] _waiting = new WaitingPool[MaxWaitingPools];
        readonly Matrix4x4[][] _buckets = new Matrix4x4[Buckets][];
        readonly int[] _bucketCounts = new int[Buckets];
        readonly Color[] _bucketColours = new Color[Buckets];
        readonly Matrix4x4[] _dropMatrices = new Matrix4x4[MaxDrops];
        readonly System.Random _random = new System.Random(0x5EED);
        int _dropCount;
        int _waitingCount;
        long _now;

        public BloodDirector(WorldRenderModel model, BodyFinder? findBody = null)
        {
            _model = model;
            _findBody = findBody;
            for (int shape = 0; shape < Shapes; shape++)
            for (int step = 0; step < BloodLedger.FadeSteps; step++)
            {
                int bucket = shape * BloodLedger.FadeSteps + step;
                _buckets[bucket] = new Matrix4x4[16];
                Color full = (BloodShape)shape == BloodShape.Pool ? PoolColour : MarkColour;
                _bucketColours[bucket] = new Color(full.r, full.g, full.b, full.a * BloodLedger.StepStrength(step));
            }
        }

        /// <summary>The marks on the ground, oldest first.</summary>
        public BloodLedger Marks => _ledger;

        /// <summary>Drops in the air.</summary>
        public int DropsInFlight => _dropCount;

        /// <summary>Pools waiting for a body to finish falling.</summary>
        public int PoolsWaiting => _waitingCount;

        /// <summary>The tick the marks are aged against.</summary>
        public long Now => _now;

        /// <summary>Instanced calls the last <see cref="Draw"/> submitted.</summary>
        public int LastDrawCalls { get; private set; }

        /// <summary>Marks the last <see cref="Draw"/> submitted.</summary>
        public int LastMarksDrawn { get; private set; }

        /// <summary>
        /// Why landed blood left no mark, counted since the last <see cref="Clear"/>: off the board,
        /// into something solid, into water, or over nothing to stand on. The first thing to read
        /// when a fight leaves less blood than it should.
        /// </summary>
        public int RefusedOffBoard { get; private set; }
        public int RefusedBlocked { get; private set; }
        public int RefusedWater { get; private set; }
        public int RefusedNoGround { get; private set; }

        /// <summary>Where the last refused mark would have gone, and on which layer.</summary>
        public Vector3 LastRefused { get; private set; }
        public int LastRefusedLayer { get; private set; }

        // ---------------------------------------------------------------- the seam

        public void Spurt(Vector3 feet, Vector3 wound, Vector3 direction, float amount, bool sharp)
        {
            int layer = LayerOf(feet);
            if (direction.sqrMagnitude < 1e-6f)
            {
                float any = (float)(_random.NextDouble() * Mathf.PI * 2.0);
                direction = new Vector3(Mathf.Cos(any), 0f, Mathf.Sin(any));
            }

            BloodShape shape = BloodSpray.ShapeOf(sharp);
            float radius = BloodSpray.MarkRadius(amount, sharp);
            float stretch = BloodSpray.MarkStretch(sharp);
            float yaw = YawOf(direction);
            BloodSpray.Throw(sharp, out float slow, out float fast, out float spread);
            float lift = sharp ? 1.4f : 0.9f;

            int drops = BloodSpray.Drops(amount, sharp);
            for (int i = 0; i < drops; i++)
            {
                bool lead = i == 0;
                if (_dropCount == MaxDrops)
                {
                    // No room in the air: the mark still belongs to the hit.
                    if (lead) Lay(feet + direction * 0.6f, layer, shape, radius, stretch, yaw);
                    break;
                }

                // The lead drop goes straight along the blow at the middle speed, so the mark lands
                // where the eye expects; the rest fan out around it.
                float turn = lead ? 0f : (float)((_random.NextDouble() * 2.0 - 1.0) * spread);
                float speed = lead ? (slow + fast) * 0.5f : Mathf.Lerp(slow, fast, (float)_random.NextDouble());
                Vector3 along = Quaternion.Euler(0f, turn, 0f) * direction;
                float up = lead ? lift : lift * (0.5f + (float)_random.NextDouble());

                _drops[_dropCount++] = new Drop
                {
                    Position = wound,
                    Velocity = along * speed + Vector3.up * up,
                    Layer = layer,
                    Lead = lead,
                    Shape = shape,
                    Radius = radius,
                    Stretch = stretch,
                    Yaw = yaw,
                };
            }
        }

        public void Pool(PawnId who, Vector3 feet, float sizeFactor, float bodyLength)
        {
            float radius = BloodSpray.PoolRadius(bodyLength, sizeFactor);
            if (radius <= 0f) return;
            int layer = LayerOf(feet);
            if (_waitingCount == MaxWaitingPools)
            {
                Lay(feet, layer, BloodShape.Pool, radius, 1f, RandomYaw());
                return;
            }

            _waiting[_waitingCount++] = new WaitingPool
            {
                Who = who,
                Feet = feet,
                Layer = layer,
                Radius = radius,
                Wait = BloodSpray.PoolWaitSeconds,
            };
        }

        public void Clear()
        {
            _ledger.Clear();
            _dropCount = 0;
            _waitingCount = 0;
            RefusedOffBoard = RefusedBlocked = RefusedWater = RefusedNoGround = 0;
        }

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Move the drops and the pools' waits on by <paramref name="seconds"/> (nought while
        /// paused) and age the marks to <paramref name="tick"/>. Before the frame's combat events
        /// are handed on, so a mark made this frame is born on this tick.
        /// </summary>
        public void Step(float seconds, long tick)
        {
            _now = tick;
            _ledger.Expire(tick);
            if (seconds <= 0f) return;

            for (int i = _dropCount - 1; i >= 0; i--)
            {
                ref Drop drop = ref _drops[i];
                drop.Velocity.y -= Gravity * seconds;
                drop.Position += drop.Velocity * seconds;

                float ground = drop.Layer * CellMetrics.SizeY + GroundRelief.HeightAt(drop.Position.x, drop.Position.z);
                if (drop.Position.y > ground || drop.Velocity.y > 0f) continue;

                if (drop.Lead) Lay(drop.Position, drop.Layer, drop.Shape, drop.Radius, drop.Stretch, drop.Yaw);
                _drops[i] = _drops[--_dropCount];
            }

            for (int i = _waitingCount - 1; i >= 0; i--)
            {
                ref WaitingPool pool = ref _waiting[i];
                pool.Wait -= seconds;
                if (pool.Wait > 0f) continue;

                // The body has fallen: the pool goes under its middle, whichever way it went (§10c).
                Vector3 at = _findBody != null && _findBody(pool.Who, out Vector3 middle) ? middle : pool.Feet;
                Lay(at, pool.Layer, BloodShape.Pool, pool.Radius, 1f, RandomYaw());
                _waiting[i] = _waiting[--_waitingCount];
            }
        }

        /// <summary>
        /// Submit what is to be seen between <paramref name="lowest"/> and <paramref name="highest"/>:
        /// the marks bucketed by shape and fade step, and the drops in one call. A mark whose ground
        /// has gone — a floor taken up, a wall built on it — goes with it (§10c).
        /// </summary>
        /// <param name="slice">When given, a mark on a storey walls-down is hiding goes with it
        /// (design 42 §5).</param>
        public void Draw(ChunkRenderer renderer, int lowest, int highest,
            SliceSettings? slice = null, int activeLayer = 0)
        {
            int callsBefore = renderer.DrawCalls;
            System.Array.Clear(_bucketCounts, 0, Buckets);
            int drawn = 0;

            for (int i = _ledger.Count - 1; i >= 0; i--)
            {
                BloodMarkRecord mark = _ledger[i];
                if (!Stands(mark.X, mark.Z, mark.Layer, out _, count: false))
                {
                    _ledger.RemoveAt(i);
                    continue;
                }
                if (!BloodLedger.Visible(mark.Layer, lowest, highest)) continue;
                if (slice != null && slice.HidesStandingAt(activeLayer, new CellRef(
                        Mathf.FloorToInt(mark.X / CellMetrics.SizeXZ), Mathf.FloorToInt(mark.Z / CellMetrics.SizeXZ),
                        mark.Layer), _model)) continue;

                long age = _now - mark.Born;
                int step = BloodLedger.FadeStep(age);
                if (step < 0) continue;

                Matrix4x4 place = PlacementOf(mark, _now);

                int bucket = (int)mark.Shape * BloodLedger.FadeSteps + step;
                ref Matrix4x4[] matrices = ref _buckets[bucket];
                if (_bucketCounts[bucket] == matrices.Length) System.Array.Resize(ref matrices, matrices.Length * 2);
                matrices[_bucketCounts[bucket]++] = place;
                drawn++;
            }

            // Pools first, so a fresh hit's mark lies over the pool it fell beside.
            SubmitShape(renderer, BloodShape.Pool);
            SubmitShape(renderer, BloodShape.Spot);
            SubmitShape(renderer, BloodShape.Splatter);

            int drops = 0;
            var size = new Vector3(DropSize, DropSize, DropSize);
            for (int i = 0; i < _dropCount; i++)
            {
                if (!BloodLedger.Visible(_drops[i].Layer, lowest, highest)) continue;
                _dropMatrices[drops++] = Matrix4x4.TRS(_drops[i].Position, Quaternion.identity, size);
            }
            renderer.DrawGroundInstances(PrimitiveMeshes.UnitCube, DropColour, _dropMatrices, drops);

            LastMarksDrawn = drawn;
            LastDrawCalls = renderer.DrawCalls - callsBefore;
        }

        /// <summary>
        /// Where a mark is drawn at <paramref name="now"/>: draped on the relief (ground-fixed, the
        /// standing rule), turned along its blow, stretched, and sized — a pool by how far it has
        /// spread since it was laid.
        /// </summary>
        public static Matrix4x4 PlacementOf(in BloodMarkRecord mark, long now)
        {
            float radius = mark.Shape == BloodShape.Pool ? mark.Radius * BloodSpray.PoolGrowth(now - mark.Born) : mark.Radius;
            return GroundRelief.Drape(new Vector3(mark.X, mark.Y, mark.Z))
                   * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, mark.Yaw, 0f),
                       new Vector3(radius * mark.Stretch, 1f, radius));
        }

        void SubmitShape(ChunkRenderer renderer, BloodShape shape)
        {
            Mesh mesh = PrimitiveMeshes.Blood(shape);
            for (int step = 0; step < BloodLedger.FadeSteps; step++)
            {
                int bucket = (int)shape * BloodLedger.FadeSteps + step;
                renderer.DrawGroundInstances(mesh, _bucketColours[bucket], _buckets[bucket], _bucketCounts[bucket]);
            }
        }

        // ---------------------------------------------------------------- the ground

        /// <summary>
        /// Lay a mark where a drop landed, if there is ground there to take it: nothing is laid
        /// over a drop, into water or against a wall (§10c).
        /// </summary>
        void Lay(Vector3 at, int layer, BloodShape shape, float radius, float stretch, float yaw)
        {
            if (!Stands(at.x, at.z, layer, out int index))
            {
                LastRefused = at;
                LastRefusedLayer = layer;
                return;
            }
            float floor = _model.Floor(index) != CoreContent.SlabNone ? CellMetrics.SlabLift : 0f;
            float lift = shape == BloodShape.Pool ? PoolLift : MarkLift;
            _ledger.Add(new BloodMarkRecord
            {
                X = at.x,
                Y = layer * CellMetrics.SizeY + floor + lift,
                Z = at.z,
                Yaw = yaw,
                Radius = radius,
                Stretch = stretch,
                Shape = shape,
                Born = _now,
                Layer = layer,
                Cell = index,
            });
        }

        /// <summary>
        /// Whether the cell under a point on <paramref name="layer"/> has ground for blood to lie on:
        /// on the board, open, not water, and standing on a floor or on solid ground below.
        /// </summary>
        bool Stands(float worldX, float worldZ, int layer, out int index, bool count = true)
        {
            int x = Mathf.FloorToInt(worldX / CellMetrics.SizeXZ);
            int z = Mathf.FloorToInt(worldZ / CellMetrics.SizeXZ);
            index = -1;
            if (!_model.Size.Contains(x, z, layer))
            {
                if (count) RefusedOffBoard++;
                return false;
            }

            index = _model.Index(x, z, layer);
            if (_model.IsSolid(index) || _model.IsBlocking(index))
            {
                if (count) RefusedBlocked++;
                return false;
            }
            // Water in the cell, or under it — the knockback's rule (CombatSystem.IsWater).
            if (WaterLine.IsWater(_model, new CellRef(x, z, layer))
                || layer > 0 && WaterLine.IsWater(_model, new CellRef(x, z, layer - 1)))
            {
                if (count) RefusedWater++;
                return false;
            }
            if (_model.Floor(index) != CoreContent.SlabNone) return true;
            if (layer > 0 && _model.IsSolid(_model.Index(x, z, layer - 1))) return true;
            if (count) RefusedNoGround++;
            return false;
        }

        /// <summary>The layer a point on the ground belongs to, relief taken off.</summary>
        int LayerOf(Vector3 feet)
        {
            float level = (feet.y - GroundRelief.HeightAt(feet.x, feet.z)) / CellMetrics.SizeY;
            return Mathf.Clamp(Mathf.RoundToInt(level), 0, _model.Size.SizeY - 1);
        }

        /// <summary>The yaw that turns a mark's +x along <paramref name="direction"/>.</summary>
        public static float YawOf(Vector3 direction) => -Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;

        float RandomYaw() => (float)(_random.NextDouble() * 360.0);
    }
}
