#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// **The crowd scan may become cheaper. It may not become different.**
    ///
    /// <para><c>PawnPose.Of</c> walked every other pawn for every pawn it posed — O(N squared)
    /// across the colony, measured at 17.2 ms of a 30.4 ms frame at 384 colonists
    /// (<c>docs/plans/pf-crowd-scan.md</c>). The sidestep it computes has been played and judged
    /// (<c>docs/design/25-pawn-steering.md</c>) and is not up for re-judgement, so the only
    /// acceptable fix is one that cannot change a drawn position.</para>
    ///
    /// <para><b>It can be exact, and that is the whole licence for this work.</b>
    /// <c>SteeringCurve.Proximity</c> is <c>SmoothStep((3.0 - d) / 1.5)</c>, which is exactly
    /// <c>0f</c> at and beyond <c>CrowdFarRadius</c>, and the loop already discards a zero. Every
    /// pair the index skips is a pair the old loop visited and threw away, and the reduction is a
    /// <c>max</c>, which does not care what order it sees its arguments in.</para>
    ///
    /// <para>So these tests come in two halves: the index finds everybody it must
    /// (<see cref="NobodyWithinTheCrowdRadiusIsMissed"/>), and the pose is bit-for-bit what the
    /// plain scan drew (<see cref="EveryScanModeDrawsTheIdenticalPose"/>). The third half is the
    /// negative control — <see cref="TheIndexVisitsAHandfulWhereTheScanVisitedTheColony"/> fails
    /// if the cull is withheld, because an index that returns everybody would pass both of the
    /// others perfectly.</para>
    /// </summary>
    public class PawnCrowdIndexTests
    {
        [TearDown]
        public void PutTheModeBack() => PawnCrowdIndex.Mode = CrowdScan.Bucketed;

        /// <summary>
        /// A crowd worth measuring: pawns packed tightly enough that plenty of pairs are inside
        /// the 3 m radius, spread over several layers so the vertical axis is exercised, and a
        /// mixture of walking and standing so <c>InTheWay</c> takes both of its branches. And a
        /// mixture of kinds as the simulation publishes them (design 33 §8c): colonists,
        /// marauders - persons under the hostile flag, who step round and are stepped round - and
        /// animals, who are outside the sidestep on both sides, so every scan is pinned to the
        /// same gate and not only to the same arithmetic.
        /// </summary>
        static PawnView[] Crowd(int count, int seed)
        {
            var random = new System.Random(seed);
            var pawns = new PawnView[count];
            for (int i = 0; i < count; i++)
            {
                // A 10 x 10 x 3 cell block is 25 m x 25 m x 9 m, so at a hundred-odd pawns the
                // board is genuinely crowded and the radius catches real neighbours.
                int x = random.Next(0, 10);
                int z = random.Next(0, 10);
                int y = random.Next(0, 3);
                var cell = new CellRef(x, z, y);

                // One in five a marauder (kind 3), one in seven a hog (kind 1), the rest colonists.
                int kind = i % 5 == 4 ? 3 : i % 7 == 6 ? 1 : 0;
                PawnFlags flags = kind == 3 ? PawnFlags.Person | PawnFlags.Hostile
                    : kind == 1 ? PawnFlags.None : PawnFlags.Person;

                bool moving = random.Next(0, 4) != 0;
                if (!moving)
                {
                    pawns[i] = new PawnView(new PawnId(i + 1), cell, 100, 100, 50, -1, cell, 0,
                        kind: kind, flags: flags);
                    continue;
                }

                int dx = random.Next(-1, 2);
                int dz = random.Next(-1, 2);
                if (dx == 0 && dz == 0) dx = 1;
                var next = new CellRef(Mathf.Clamp(x + dx, 0, 11), Mathf.Clamp(z + dz, 0, 11), y);
                pawns[i] = new PawnView(new PawnId(i + 1), cell, 100, 100, 50, -1, next, 0,
                    movePerMille: random.Next(1, 1000), kind: kind, flags: flags);
            }
            return pawns;
        }

        static (Vector3 at, Vector3 steer) Pose(in PawnView pawn, PawnView[] crowd,
                                                PawnCrowdIndex? index, CrowdScan mode)
        {
            PawnCrowdIndex.Mode = mode;
            Vector3 at = PawnPose.Of(in pawn, 0f, 0, out _, null, crowd, index, out Vector3 steer);
            return (at, steer);
        }

        /// <summary>
        /// **The claim, pinned: the same pose, not a similar one.**
        ///
        /// <para>Every component compared exactly — no <c>Within</c>. A tolerance here would let
        /// the index quietly drift from the scan and the test would still pass, which is the one
        /// failure this whole unit has to be unable to have. Unity's <c>Vector3</c> equality
        /// operator is approximate, so the components are asserted one at a time rather than the
        /// vectors, and NUnit compares two floats exactly when it is given no tolerance.</para>
        /// </summary>
        [Test]
        public void EveryScanModeDrawsTheIdenticalPose()
        {
            PawnView[] crowd = Crowd(220, seed: 20260923);
            var index = new PawnCrowdIndex();
            index.Rebuild(crowd);

            int steered = 0;
            for (int i = 0; i < crowd.Length; i++)
            {
                var span = Pose(in crowd[i], crowd, null, CrowdScan.Span);
                var cached = Pose(in crowd[i], crowd, index, CrowdScan.Cached);
                var bucketed = Pose(in crowd[i], crowd, index, CrowdScan.Bucketed);

                if (span.steer.sqrMagnitude > 0f) steered++;

                Same(cached.at, span.at, i, "cached", "position");
                Same(cached.steer, span.steer, i, "cached", "sidestep");
                Same(bucketed.at, span.at, i, "bucketed", "position");
                Same(bucketed.steer, span.steer, i, "bucketed", "sidestep");
            }

            // Without this the test would pass just as well on a crowd nobody steers around,
            // which would prove only that zero equals zero.
            Assert.That(steered, Is.GreaterThan(crowd.Length / 10),
                $"only {steered} of {crowd.Length} pawns sidestepped anybody - " +
                "this fixture is not crowded enough to be evidence of anything");
        }

        static void Same(Vector3 got, Vector3 want, int pawn, string mode, string what)
        {
            Assert.That(got.x, Is.EqualTo(want.x), $"pawn {pawn}: {mode} {what}.x");
            Assert.That(got.y, Is.EqualTo(want.y), $"pawn {pawn}: {mode} {what}.y");
            Assert.That(got.z, Is.EqualTo(want.z), $"pawn {pawn}: {mode} {what}.z");
        }

        /// <summary>
        /// **The edge of the radius, where a miss is worth almost nothing and therefore hides.**
        ///
        /// <para><b>Written because the test above did not catch a real bug.</b> The bucket size
        /// was mutated from the 3 m radius to the 2.5 m cell — the exact tidy-up
        /// <c>PawnCrowdIndex</c> warns against — and
        /// <see cref="EveryScanModeDrawsTheIdenticalPose"/> <i>passed</i>. A pawn at 2.93 m scores
        /// <c>Proximity</c> of about 0.007, and the crowd term is a <c>max</c>: in a crowd there is
        /// nearly always a nearer neighbour whose weight buries it. So the one test that is
        /// supposed to pin exactness was blind to precisely the failure the cull can have.</para>
        ///
        /// <para>Two pawns and nobody else, at a distance chosen to sit just inside the radius.
        /// There is no larger weight to hide behind, so the sidestep is small — and any miss moves
        /// it to exactly zero.</para>
        /// </summary>
        [Test]
        public void AnInfluenceAtTheVeryEdgeOfTheRadiusSurvivesTheCull()
        {
            // Far enough apart that the pair is near the outside of the 3 m radius, and walking
            // head-on so InTheWay is wide open and the surviving weight is visible.
            var mine = new CellRef(0, 0, 0);
            var ahead = new CellRef(0, 1, 0);
            var theirs = new CellRef(0, 2, 0);

            for (int perMille = 0; perMille <= 1000; perMille += 25)
            {
                var me = new PawnView(new PawnId(1), mine, 100, 100, 50, -1, ahead, 0,
                    movePerMille: perMille);
                var them = new PawnView(new PawnId(2), theirs, 100, 100, 50, -1, ahead, 0,
                    movePerMille: perMille);
                var pair = new[] { me, them };

                var index = new PawnCrowdIndex();
                index.Rebuild(pair);

                var span = Pose(in me, pair, null, CrowdScan.Span);
                var bucketed = Pose(in me, pair, index, CrowdScan.Bucketed);
                var cached = Pose(in me, pair, index, CrowdScan.Cached);

                Same(bucketed.at, span.at, perMille, "bucketed", "position");
                Same(bucketed.steer, span.steer, perMille, "bucketed", "sidestep");
                Same(cached.at, span.at, perMille, "cached", "position");
                Same(cached.steer, span.steer, perMille, "cached", "sidestep");
            }

            // And the approach really does cross the outside of the radius, or the loop above
            // proved only that two pawns who ignore each other agree about it. Walking towards
            // one another from two cells apart, the gap opens at 5 m and closes to nothing.
            var far = new PawnView(new PawnId(1), mine, 100, 100, 50, -1, ahead, 0, movePerMille: 1);
            var farThem = new PawnView(new PawnId(2), theirs, 100, 100, 50, -1, ahead, 0, movePerMille: 1);
            float gap = Vector3.Distance(SteeringCurve.WhereItIsNow(in far),
                                         SteeringCurve.WhereItIsNow(in farThem));
            Assert.That(gap, Is.GreaterThan(SteeringCurve.CrowdFarRadius),
                "the pair starts inside the radius, so the sweep never crosses its edge");

            var near = new PawnView(new PawnId(1), mine, 100, 100, 50, -1, ahead, 0, movePerMille: 999);
            var nearThem = new PawnView(new PawnId(2), theirs, 100, 100, 50, -1, ahead, 0, movePerMille: 999);
            Assert.That(Vector3.Distance(SteeringCurve.WhereItIsNow(in near),
                                         SteeringCurve.WhereItIsNow(in nearThem)),
                Is.LessThan(SteeringCurve.CrowdNearRadius),
                "the pair never closes to a real sidestep, so nothing in the sweep had weight");
        }

        /// <summary>
        /// The index's one obligation: it may hand back extra candidates, but never miss one
        /// inside the radius. Brute-forced against every pawn.
        /// </summary>
        [Test]
        public void NobodyWithinTheCrowdRadiusIsMissed()
        {
            PawnView[] crowd = Crowd(300, seed: 7);
            var index = new PawnCrowdIndex();
            index.Rebuild(crowd);

            for (int i = 0; i < crowd.Length; i++)
            {
                Vector3 at = SteeringCurve.WhereItIsNow(in crowd[i]);

                var found = new HashSet<int>();
                foreach (int j in index.Near(at)) found.Add(j);

                for (int j = 0; j < crowd.Length; j++)
                {
                    float d = Vector3.Distance(at, SteeringCurve.WhereItIsNow(in crowd[j]));
                    if (d > SteeringCurve.CrowdFarRadius) continue;
                    Assert.That(found, Does.Contain(j),
                        $"pawn {j} is {d:F3} m from pawn {i} - inside the {SteeringCurve.CrowdFarRadius} m " +
                        "radius and the index did not return it");
                }
            }
        }

        /// <summary>
        /// **The negative control.** An index that returned the whole colony would satisfy both
        /// of the tests above and save nothing; this is the one that fails when the cull is
        /// withheld.
        ///
        /// <para>The pawns are spread over the board rather than packed, which is the case the
        /// quadratic actually hurt: 384 colonists going about a 120 x 120 board are nowhere near
        /// each other, and the old loop measured all 147,456 pairs to discover it.</para>
        /// </summary>
        [Test]
        public void TheIndexVisitsAHandfulWhereTheScanVisitedTheColony()
        {
            // Every third cell of a 30 x 30 patch: 100 pawns, 7.5 m apart, so a 3 m radius
            // reaches nobody at all and every pair the old scan measured was wasted.
            var pawns = new List<PawnView>();
            for (int x = 0; x < 30; x += 3)
                for (int z = 0; z < 30; z += 3)
                    pawns.Add(new PawnView(new PawnId(pawns.Count + 1), new CellRef(x, z, 0),
                        100, 100, 50, -1, new CellRef(x, z, 0), 0));

            PawnView[] crowd = pawns.ToArray();
            var index = new PawnCrowdIndex();
            index.Rebuild(crowd);

            int visited = 0;
            for (int i = 0; i < crowd.Length; i++)
                foreach (int _ in index.Near(SteeringCurve.WhereItIsNow(in crowd[i])))
                    visited++;

            int scanned = crowd.Length * crowd.Length;
            Assert.That(visited, Is.LessThan(scanned / 10),
                $"the index visited {visited} pairs where the plain scan visited {scanned} - " +
                "it is not culling, and the whole unit buys nothing");
        }

        /// <summary>
        /// **The vertical is a real axis.** Distance is three-dimensional on purpose — on a board
        /// of 3 m terrace risers a colonist on the storey above measured nought metres away under
        /// the old x/z distance and was given the full sidestep (design 25 section 2). A bucket is
        /// 3.0 m and a layer is 3.0 m, so "one storey up" is the exact edge of the neighbourhood
        /// and is the case most likely to be lost by an off-by-one in the block walk.
        /// </summary>
        [Test]
        public void SomebodyJustInsideTheRadiusOverheadIsStillReached()
        {
            // Two cells one layer apart. Their floor centres are CellMetrics.SizeY apart, which
            // is exactly the radius and therefore exactly zero weight; part-way through a step
            // upwards is inside it, which is the case that must be found.
            var below = new CellRef(4, 4, 0);
            var above = new CellRef(4, 4, 1);

            var me = new PawnView(new PawnId(1), below, 100, 100, 50, -1, below, 0);
            // Half a step up: 1.5 m overhead, well inside the 3 m radius.
            var overhead = new PawnView(new PawnId(2), above, 100, 100, 50, -1, above, 0);

            Vector3 gap = SteeringCurve.WhereItIsNow(in overhead) - SteeringCurve.WhereItIsNow(in me);
            Assume.That(gap.magnitude, Is.LessThan(SteeringCurve.CrowdFarRadius).Or.EqualTo(SteeringCurve.CrowdFarRadius),
                "the fixture must put the other pawn inside the radius for this to test anything");

            var crowd = new[] { me, overhead };
            var index = new PawnCrowdIndex();
            index.Rebuild(crowd);

            var found = new HashSet<int>();
            foreach (int j in index.Near(SteeringCurve.WhereItIsNow(in me))) found.Add(j);

            Assert.That(found, Does.Contain(1),
                "the pawn one layer up was not in the neighbourhood - the block walk has lost its y axis");
        }

        /// <summary>
        /// **A per-frame structure that allocates per frame has moved the cost, not removed it.**
        ///
        /// <para>The two fixtures sit either side of a power of two in the bucket table's size
        /// (200 pawns wants 512 slots, 120 wants 256), which is the case that bites: a colony
        /// oscillating across that boundary — a birth and a death either side of it — would
        /// reallocate all seven of the table's arrays every frame, for ever. The table therefore
        /// grows and never shrinks, and this is the guard on that.</para>
        /// </summary>
        [Test]
        public void RebuildingEveryFrameStopsAllocating()
        {
            var index = new PawnCrowdIndex();
            PawnView[] big = Crowd(200, seed: 3);
            PawnView[] small = Crowd(120, seed: 4);

            // Let the arrays reach their working size first: the first rebuilds are allowed to
            // allocate, and only the steady state is being asserted.
            for (int i = 0; i < 8; i++) { index.Rebuild(big); index.Rebuild(small); }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++) { index.Rebuild(big); index.Rebuild(small); }
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.Zero,
                $"a hundred rebuilds allocated {after - before} bytes - the table is being resized " +
                "every time the colony crosses a power of two");
        }

        /// <summary>
        /// The index is rebuilt every frame from a span that grows and shrinks as colonists are
        /// born and spawned, and it recycles its arrays. A shrink must not leave the previous
        /// frame's pawns in the buckets.
        /// </summary>
        [Test]
        public void ARebuildForgetsTheFrameBefore()
        {
            var index = new PawnCrowdIndex();
            index.Rebuild(Crowd(200, seed: 1));

            PawnView[] fewer = Crowd(3, seed: 2);
            index.Rebuild(fewer);

            Assert.That(index.Count, Is.EqualTo(3));
            for (int i = 0; i < fewer.Length; i++)
                foreach (int j in index.Near(SteeringCurve.WhereItIsNow(in fewer[i])))
                    Assert.That(j, Is.LessThan(3),
                        "a pawn from the previous rebuild is still in the buckets");
        }
    }
}
