#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A giver that lives outside the simulation assembly, which is the case discovery cannot
    /// cover and <see cref="SimWorldBuilder.AddWorkGiver"/> exists for. It hands out a wait with
    /// a work length nothing else in the game uses, so the job a pawn ends up holding can be
    /// attributed to this giver and not to the idle branch, which also gives out waits.
    /// </summary>
    sealed class MarkerWorkGiver : WorkGiver
    {
        public const int Marker = 4242;

        public int Consulted { get; private set; }

        public override string Name => "Marker";

        public override int WorkType => WorkTypeIndex.Haul;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            Consulted++;
            job.Reset(JobIndex.Wait);
            job.WorkTicks = Marker;
            return true;
        }
    }

    /// <summary>
    /// The seam OQ-44 opened: a work giver joins the scan without anything editing a file it does
    /// not own. A giver inside <c>Odyssey.Sim</c> joins by existing; one outside joins through the
    /// builder, exactly as an intent handler does.
    ///
    /// <para>The risk a registration seam carries is that the scan order quietly becomes an
    /// accident of who registered first. Three of the tests below exist for that one question, and
    /// they are worth more than the comment in <c>JobSystem</c> saying it matters.</para>
    /// </summary>
    public class WorkGiverRegistrationTests
    {
        static JobSystem Shipped() => new JobSystem(Context());

        static PawnContext Context()
        {
            var size = new GridSize(8, 8, 2);
            var cells = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++) cells.Floor[i] = 1;
            var nav = new NavGraph(cells);
            nav.Rebuild();
            return new PawnContext(cells, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns());
        }

        // ---- discovery ---------------------------------------------------------------------

        [Test]
        public void EveryWorkGiverInTheSimulationAssemblyIsDiscovered()
        {
            // Scanned again here, independently of the registry's own filter, so that a filter
            // which quietly stopped matching something would fail rather than agree with itself.
            var expected = typeof(JobSystem).Assembly.GetTypes()
                .Where(t => typeof(WorkGiver).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.FullName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            var found = WorkGiverRegistry.Discover()
                .Select(g => g.GetType().FullName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.That(found, Is.EqualTo(expected));
            Assert.That(found.Length, Is.GreaterThan(0), "a colony with no work givers does nothing at all");
        }

        [Test]
        public void DiscoveryHandsOutFreshInstancesSoNoTwoWorldsShareAGiver()
        {
            var first = WorkGiverRegistry.Discover();
            var second = WorkGiverRegistry.Discover();
            for (int i = 0; i < first.Length; i++)
                Assert.That(ReferenceEquals(first[i], second[i]), Is.False,
                    $"{first[i].Name} was handed to two worlds as one object");
        }

        // ---- scan order --------------------------------------------------------------------

        [Test]
        public void TheShippedColonyScansConstructionThenGrowingThenCuttingThenMiningThenHauling()
        {
            // Pinned deliberately. Adding a kind of work is allowed to change this line — it is
            // the one place in the repository where the scan order is written down — but it must
            // be a decision somebody made, never something that moved on its own.
            //
            // Construction leads because a site is work already begun, and delivery leads within it
            // because a site cannot be worked until it has been fed. Cutting, mining and hauling
            // keep the order they have always had, relative to each other.
            //
            // Growing sits between construction and cutting (U47): a zone's daylight window is the
            // one clock in the colony that will not wait — a field sown late is a field sown
            // tomorrow, where a tree keeps until Thursday. Harvest ahead of Sow within the type is
            // the name tiebreak; both cut and gather at the same patch of soil, and neither has a
            // claim on the other.
            //
            // Deconstruct is last within construction, and that is the decision this line records:
            // a colony that pulls a wall down while a half-ordered hut waits for its last plank
            // finishes neither, and demolition is the one job here that is never urgent — the thing
            // being removed is already standing and already doing its job.
            //
            // Power (design 32): laying a line sits with building, after it by name, and taking one
            // up sits with deconstructing, for deconstruct's reason. Refuelling leads hauling,
            // because a generator run dry darkens a net and a log in the wrong place darkens
            // nothing (a-07 §3 records the reference's generators running dry while pawns tidied).
            // Forage (design 45 §6) is growing work and sorts by name inside it, first: a ripe
            // berry bush keeps for three days where a field's window will not, but the name
            // tiebreak is the rule here and nothing argues against it.
            //
            // Rescue leads everything (design 33 §5, C4): it is the one emergency giver, and an
            // emergency is scanned ahead of every ordinary giver at the same priority, whatever the
            // work types' order says. A colonist bleeding out on the grass outranks the wall.
            // Tend follows it (design 43 §5): the second emergency giver, after rescue by the work
            // types' order, so a downed colonist is carried to a bed and tended there.
            var names = Shipped().Givers.Select(g => g.Name).ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                // Doctor is the second emergency (design 37), behind rescue by the work types' order.
                // Cook sits after growing and before cutting (design 48 §5): a hungry colony cooks
                // before it fells.
                // Capture is the third (design 58 §7): a captured raider is usually bleeding, so the
                // warden's carry to a prison bed is an emergency, after the colony's own wounded.
                "Rescue", "Doctor", "Capture", "Deliver", "Build", "LayConduit", "Deconstruct", "RemoveConduit",
                // FeedPrisoner is warden work and warden work is scanned last (design 58 §7): a
                // prisoner goes hungry slowly, and the capture that brings her in is the emergency.
                "Forage", "Harvest", "Sow", "Cook", "Fell", "Mine", "Refuel", "Haul", "FeedPrisoner",
            }));
        }

        [Test]
        public void TheScanOrderComesFromTheDefsAndNotFromRegistrationOrder()
        {
            var ctx = Context();
            WorkGiver[] discovered = WorkGiverRegistry.Discover();
            string[] shipped = new JobSystem(ctx, JobSystem.DefaultTree(), discovered)
                .Givers.Select(g => g.Name).ToArray();

            // **A sample of orders, not all of them**, since power (design 32). Every permutation
            // was 8! = 40,320 constructions with eight givers; eleven made it 11! and took this one
            // test from well under a second to two minutes, and a twelfth would have made it
            // twenty. What the test proves is that the sort ignores registration order, and a total
            // order is what makes that true — which NoTwoGiversCanTieInTheSort guards directly. So
            // every rotation and every reversal (each giver first and last at least once) and five
            // thousand seeded shuffles are asked, which is the same question at a fixed price.
            foreach (WorkGiver[] permutation in SampledOrders(WorkGiverRegistry.Discover()))
            {
                string[] order = new JobSystem(ctx, JobSystem.DefaultTree(), permutation)
                    .Givers.Select(g => g.Name).ToArray();
                Assert.That(order, Is.EqualTo(shipped),
                    "registered as " + string.Join(", ", permutation.Select(g => g.Name)));
            }
        }

        [Test]
        public void NoTwoGiversCanTieInTheSort()
        {
            // The negative control for the test above. If two givers compared equal on every key,
            // the sort would be free to order them either way and the permutation test would be
            // asserting that List.Sort is stable rather than that our ordering is total.
            var content = ContentPack.Pawns();
            WorkGiver[] givers = WorkGiverRegistry.Discover();

            for (int i = 0; i < givers.Length; i++)
                for (int j = i + 1; j < givers.Length; j++)
                {
                    var a = givers[i];
                    var b = givers[j];
                    bool distinct = a.Emergency != b.Emergency
                        || content.WorkTypes[a.WorkType].order != content.WorkTypes[b.WorkType].order
                        || a.IntraPriority != b.IntraPriority
                        || string.CompareOrdinal(a.Name, b.Name) != 0;
                    Assert.That(distinct, Is.True,
                        $"{a.Name} and {b.Name} are indistinguishable to the sort, so their scan " +
                        "order is whatever the sort felt like");
                }
        }

        // ---- the composition seam ----------------------------------------------------------

        [Test]
        public void AGiverRegisteredOnTheBuilderScansAndAPawnTakesItsJob()
        {
            var marker = new MarkerWorkGiver();
            var colony = Harness.Build(marker);

            Pawn pawn = colony.Pawns.Pawns.Spawn(colony.Cell(4, 4, 0));
            colony.World.Tick();

            Assert.That(marker.Consulted, Is.GreaterThan(0), "the giver was never scanned");
            Assert.That(pawn.CurrentJob, Is.Not.Null);
            Assert.That(pawn.CurrentJob!.WorkTicks, Is.EqualTo(MarkerWorkGiver.Marker),
                "the pawn is holding some other job, so the marker giver did not win the scan");
        }

        [Test]
        public void AGiverRegisteredAfterTheColonyIsStillPickedUp()
        {
            // The ordering trap this seam was written to avoid: AddColony reads the builder's
            // givers inside a factory that runs at Build(), so the two calls may be written in
            // either order. Without that, AddColony would have to be last and nothing would say so.
            var marker = new MarkerWorkGiver();
            var colony = Harness.Build(marker, registerAfterColony: true);

            colony.Pawns.Pawns.Spawn(colony.Cell(4, 4, 0));
            colony.World.Tick();

            Assert.That(marker.Consulted, Is.GreaterThan(0));
            Assert.That(colony.Jobs.Givers.Any(g => g is MarkerWorkGiver), Is.True);
        }

        [Test]
        public void RegisteringAGiverTheAssemblyAlreadyOwnsIsRefused()
        {
            var jobs = Shipped();
            Assert.Throws<InvalidOperationException>(() => jobs.AddGivers(new WorkGiver[] { new HaulWorkGiver() }));
        }

        /// <summary>
        /// A flat board built through <see cref="ColonyComposition.AddColony"/>, which is the one
        /// place the colony is wired and therefore the only place worth testing the seam in.
        /// </summary>
        sealed class Harness
        {
            public PawnContext Pawns = null!;
            public SimWorld World = null!;
            public JobSystem Jobs = null!;
            public CellGrid Cells = null!;

            public int Cell(int x, int z, int y) => Cells.Size.Index(x, z, y);

            public static Harness Build(WorkGiver giver, bool registerAfterColony = false)
            {
                var size = new GridSize(8, 8, 2);
                var cells = new CellGrid(size);
                for (int i = 0; i < size.CellCount; i++) cells.Floor[i] = 1;

                var nav = new NavGraph(cells);
                nav.Rebuild();
                var pawns = new PawnContext(cells, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns());
                var solver = new SupportSolver(cells);
                var support = new SupportSystem(cells, solver);
                var designations = new DesignationGrid(cells, Array.Empty<PlacedEdifice>());
                var jobs = new JobSystem(pawns);

                var builder = new SimWorldBuilder().WithSeed(11u).WithSize(size);
                if (!registerAfterColony) builder.AddWorkGiver(giver);
                builder.AddColony(pawns, designations, support, nav, new List<PlacedEdifice>(), out _, jobs);
                if (registerAfterColony) builder.AddWorkGiver(giver);

                return new Harness
                {
                    Cells = cells, Pawns = pawns, Jobs = jobs, World = builder.Build(),
                };
            }
        }

        /// <summary>Every ordering of a small set, so "order does not matter" is proved rather than sampled.</summary>
        static IEnumerable<WorkGiver[]> SampledOrders(WorkGiver[] givers)
        {
            int n = givers.Length;
            for (int start = 0; start < n; start++)
            {
                var rotated = new WorkGiver[n];
                for (int i = 0; i < n; i++) rotated[i] = givers[(start + i) % n];
                yield return rotated;
                var reversed = (WorkGiver[])rotated.Clone();
                Array.Reverse(reversed);
                yield return reversed;
            }

            var rng = new Random(20260923);
            for (int s = 0; s < 5_000; s++)
            {
                var shuffled = (WorkGiver[])givers.Clone();
                for (int i = n - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
                }
                yield return shuffled;
            }
        }
    }
}
