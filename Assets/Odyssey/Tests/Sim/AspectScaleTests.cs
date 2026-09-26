#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// **How many aspect rows a colonist publishes, and what that does to a reader.**
    ///
    /// <para><c>WorldSnapshot.TryGetPawnAspect</c> is a linear scan over every published row, and
    /// its doc comment justified that with "the published set is tens of rows on a real colony".
    /// That was true when it was written and has not been true for some time: work priorities
    /// (two rows per work type) and the colonist schedule (twenty-four rows) were both added
    /// afterwards, and both publish for every colonist every tick.</para>
    ///
    /// <para>This fixture is the number rather than the reasoning, and it is a fast-tier test so
    /// the next feature that adds a per-colonist row has to look at it.</para>
    /// </summary>
    public class AspectScaleTests
    {
        static int RowsPerPawn(int pawns)
        {
            Colony colony = Colony.Build();
            for (int i = 0; i < pawns; i++)
                colony.Ctx.Pawns.Spawn(colony.Cell(2 + i % 12, 2 + i / 12, 0));
            colony.World.Tick();

            WorldSnapshot snapshot = colony.World.Views.Current;
            Assert.That(snapshot.PawnCount, Is.EqualTo(pawns), "the fixture did not spawn what it meant to");
            return snapshot.AspectCount / pawns;
        }

        /// <summary>
        /// The rows scale with the colony, one fixed bundle each, and the bundle is about a
        /// hundred rows — not "tens".
        /// </summary>
        [Test]
        public void AColonistPublishesAboutAHundredAspectRows()
        {
            int one = RowsPerPawn(1);
            int many = RowsPerPawn(24);

            TestContext.WriteLine($"aspect rows per colonist: {one} at 1 pawn, {many} at 24");

            Assert.That(one, Is.EqualTo(many),
                "the bundle should be the same size for every colonist");
            // Measured at 57 on 2026-09-23. The bound is deliberately wide on both sides: the
            // floor fails if the bundle ever shrinks back to the "tens of rows" the scan's doc
            // comment assumed, and the ceiling fails if one feature doubles it — which is the
            // scaling row docs/process.md section 3 asks every growing loop to carry.
            Assert.That(one, Is.InRange(40, 120),
                "aspect rows per colonist has moved a long way; re-read TryGetPawnAspect's doc " +
                "comment and docs/design/31-aspect-lookup.md before changing this bound");
        }

        /// <summary>
        /// **The index must answer exactly what the scan answered, row for row.**
        ///
        /// <para>Brute-forced: every published row is asked for by key and must come back with its
        /// own value, and a key nobody published must come back false. This is the whole of the
        /// correctness claim — the lookup became O(1) and is not allowed to have become
        /// different.</para>
        /// </summary>
        [Test]
        public void TheIndexAnswersWhatAScanWouldHave()
        {
            Colony colony = Colony.Build();
            for (int i = 0; i < 24; i++)
                colony.Ctx.Pawns.Spawn(colony.Cell(2 + i % 12, 2 + i / 12, 0));
            colony.World.Tick();

            WorldSnapshot snapshot = colony.World.Views.Current;
            Assert.That(snapshot.AspectCount, Is.GreaterThan(500), "the fixture published almost nothing");

            // What a scan would say, computed here rather than trusted: first row wins.
            var rows = snapshot.PawnAspects;
            for (int i = 0; i < rows.Length; i++)
            {
                int expected = 0;
                for (int j = 0; j < rows.Length; j++)
                {
                    if (rows[j].Pawn != rows[i].Pawn || rows[j].Key != rows[i].Key) continue;
                    expected = rows[j].Value;
                    break;
                }

                Assert.That(snapshot.TryGetPawnAspect(rows[i].Pawn, rows[i].Key, out int got), Is.True,
                    $"row {i} was published and the index could not find it");
                Assert.That(got, Is.EqualTo(expected), $"row {i} came back with the wrong value");
            }
        }

        /// <summary>A key nobody published is still a miss, and does not loop for ever.</summary>
        [Test]
        public void AKeyNobodyPublishedIsAMiss()
        {
            Colony colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(4, 4, 0));
            colony.World.Tick();

            WorldSnapshot snapshot = colony.World.Views.Current;
            Assert.That(snapshot.TryGetPawnAspect(pawn.Id, AspectKey.Of("odyssey.test.nobody"), out int v),
                Is.False);
            Assert.That(v, Is.Zero, "a miss must hand back zero, which callers rely on");

            // And a pawn that does not exist, against a key that does.
            Assert.That(snapshot.TryGetPawnAspect(new PawnId(99999), SkillAspects.RollSeed, out _),
                Is.False);
        }

        /// <summary>
        /// **The index belongs to one published frame and must not outlive it.**
        ///
        /// <para><b>Written twice, and the first version was worthless.</b> It read an aspect,
        /// changed the underlying number, ticked, and read again — and it <i>passed with the
        /// invalidation deliberately removed</i>. Two reasons, both worth knowing. The snapshots
        /// are double-buffered (<c>WorldViewStore._a</c>/<c>_b</c>), so each buffer would simply
        /// build its own index once; and the index maps a key to a <i>row number</i>, while the
        /// rows are republished in the same order every tick, so a stale table still pointed at
        /// the right row and the value read off it was fresh.</para>
        ///
        /// <para>So a stale index is only wrong when the <b>set</b> of rows changes, and this is
        /// the cheapest change of set there is: a colonist who was not there before. Both buffers
        /// are queried first so that neither can pass by having never built an index at all.</para>
        /// </summary>
        [Test]
        public void ARepublishRetiresTheIndex()
        {
            Colony colony = Colony.Build();
            var first = colony.Ctx.Pawns.Spawn(colony.Cell(4, 4, 0));

            // Tick and query twice, so BOTH double-buffered snapshots have built an index.
            for (int i = 0; i < 2; i++)
            {
                colony.World.Tick();
                Assert.That(colony.World.Views.Current.TryGetPawnAspect(
                    first.Id, SkillAspects.RollSeed, out _), Is.True,
                    "the fixture never found the colonist it started with");
            }

            // A colonist who did not exist when either index was built.
            var late = colony.Ctx.Pawns.Spawn(colony.Cell(6, 6, 0));
            colony.World.Tick();

            Assert.That(colony.World.Views.Current.TryGetPawnAspect(
                late.Id, SkillAspects.RollSeed, out int seed), Is.True,
                "a colonist published this frame was missing from the index - it was not retired");
            Assert.That(seed, Is.EqualTo(unchecked((int)late.RollSeed)));

            // And the one who was there all along is still right.
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(
                first.Id, SkillAspects.RollSeed, out int firstSeed), Is.True);
            Assert.That(firstSeed, Is.EqualTo(unchecked((int)first.RollSeed)));
        }

        /// <summary>
        /// **Both arms of the measurement control give the identical answer, row for row.**
        ///
        /// <para><c>WorldSnapshot.IndexAspects</c> switches between the index and the scan it
        /// replaced so one run can time both. That is only a control if the two agree — otherwise
        /// the "before" arm is measuring a different game.</para>
        /// </summary>
        [Test]
        public void TheIndexAndTheScanAgreeRowForRow()
        {
            Colony colony = Colony.Build();
            for (int i = 0; i < 16; i++)
                colony.Ctx.Pawns.Spawn(colony.Cell(2 + i % 12, 2 + i / 12, 0));
            colony.World.Tick();

            WorldSnapshot snapshot = colony.World.Views.Current;
            var rows = snapshot.PawnAspects;
            Assert.That(rows.Length, Is.GreaterThan(400), "the fixture published almost nothing");

            try
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    WorldSnapshot.IndexAspects = false;
                    bool scanFound = snapshot.TryGetPawnAspect(rows[i].Pawn, rows[i].Key, out int scanned);

                    WorldSnapshot.IndexAspects = true;
                    bool indexFound = snapshot.TryGetPawnAspect(rows[i].Pawn, rows[i].Key, out int indexed);

                    Assert.That(indexFound, Is.EqualTo(scanFound), $"row {i}: found differs");
                    Assert.That(indexed, Is.EqualTo(scanned), $"row {i}: value differs");
                }

                // And a miss, both ways.
                AspectKey absent = AspectKey.Of("odyssey.test.absent");
                WorldSnapshot.IndexAspects = false;
                bool scanMiss = snapshot.TryGetPawnAspect(rows[0].Pawn, absent, out int scanZero);
                WorldSnapshot.IndexAspects = true;
                bool indexMiss = snapshot.TryGetPawnAspect(rows[0].Pawn, absent, out int indexZero);
                Assert.That(indexMiss, Is.EqualTo(scanMiss).And.False);
                Assert.That(indexZero, Is.EqualTo(scanZero).And.Zero);
            }
            finally
            {
                WorldSnapshot.IndexAspects = true;
            }
        }

        /// <summary>
        /// The table grows to the colony and then stops allocating, the same rule
        /// <c>PawnCrowdIndex</c> follows: a per-frame structure that allocates per frame has moved
        /// the cost rather than removed it.
        /// </summary>
        [Test]
        public void RepeatedFramesStopAllocating()
        {
            Colony colony = Colony.Build();
            for (int i = 0; i < 24; i++)
                colony.Ctx.Pawns.Spawn(colony.Cell(2 + i % 12, 2 + i / 12, 0));

            // Warm: let the snapshot arrays and the index table reach their size.
            for (int i = 0; i < 8; i++)
            {
                colony.World.Tick();
                colony.World.Views.Current.TryGetPawnAspect(new PawnId(1), SkillAspects.RollSeed, out _);
            }

            // The least of three windows, not one (2026-09-26). In the fast tier's full run a
            // stray ~5 KB turned up on this thread about one run in two, and instrumenting the tick
            // put it in a different phase each time — the snapshot, the pawns, or outside every
            // phase — and never when the test ran alone: the runtime's, not the simulation's,
            // which is deterministic and would allocate in the same place every run. A structure
            // that reallocates per frame allocates in every window, so the least still catches it.
            long least = long.MaxValue;
            for (int window = 0; window < 3; window++)
            {
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 20; i++)
                {
                    colony.World.Tick();
                    colony.World.Views.Current.TryGetPawnAspect(new PawnId(1), SkillAspects.RollSeed, out _);
                }
                long after = System.GC.GetAllocatedBytesForCurrentThread();
                TestContext.WriteLine($"twenty indexed frames allocated {after - before} bytes");
                least = System.Math.Min(least, after - before);
            }

            Assert.That(least, Is.LessThan(4096),
                "the aspect index is being reallocated every frame");
        }

        /// <summary>
        /// **The cost of one `TryGetPawnAspect`, stated as what it scales with.**
        ///
        /// <para>A lookup scans the whole published set, so it is O(rows-per-pawn x colonists).
        /// A caller that does it once per colonist is therefore quadratic in the colony — which
        /// is what the far-form renderer was doing, at 43 ns a pair and 4.9 ms of a frame at 384
        /// colonists (<c>docs/design/25-pawn-steering.md</c> §9d).</para>
        /// </summary>
        [Test]
        public void ThePublishedSetGrowsWithTheColony()
        {
            Colony colony = Colony.Build();
            for (int i = 0; i < 8; i++) colony.Ctx.Pawns.Spawn(colony.Cell(2 + i, 2, 0));
            colony.World.Tick();
            int eight = colony.World.Views.Current.AspectCount;

            Colony bigger = Colony.Build();
            for (int i = 0; i < 32; i++) bigger.Ctx.Pawns.Spawn(bigger.Cell(2 + i % 12, 2 + i / 12, 0));
            bigger.World.Tick();
            int thirtyTwo = bigger.World.Views.Current.AspectCount;

            TestContext.WriteLine($"published aspect rows: {eight} at 8 pawns, {thirtyTwo} at 32");
            Assert.That(thirtyTwo, Is.EqualTo(eight * 4).Within(eight / 2),
                "the published set should grow in proportion to the colony");
        }
    }
}
