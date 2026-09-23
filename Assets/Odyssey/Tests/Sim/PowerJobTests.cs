#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Power;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The three power jobs (design 32): laying a line, taking one up, and keeping a generator fed
    /// — each run end to end by the colony's own colonists, because a giver that is never offered
    /// or a driver that never finishes looks exactly like an order nobody gave.
    /// </summary>
    public class PowerJobTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        static PowerGrid PowerOf(ColonyWorld colony) => colony.Pawns.Power!;

        static int Open(ColonyWorld colony, int dx, int dz)
        {
            CellRef s = colony.Start;
            int cell = Size.Index(s.X + dx, s.Z + dz, s.Y);
            Assume.That(colony.Construction.Allows(cell), Is.True, "an ordinary buildable cell");
            return cell;
        }

        static int WoodOnTheBoard(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemHandle.Wood) total += items[i].Stack;
            return total;
        }

        /// <summary>Put this much wood down near the start, wherever there is room for it.</summary>
        static void Stock(ColonyWorld colony, int count)
        {
            int near = Open(colony, 1, -3);
            int at = colony.Pawns.Items.NearestCellWithSpace(colony.Grid, near, ItemHandle.Wood, count, 8);
            Assume.That(at, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(ItemHandle.Wood, at, count);
        }

        static void Give(ColonyWorld colony, IntentKind kind, int cell, int a = 0, int b = 0)
        {
            colony.World.Intents.Submit(new Intent(kind, Size.FromIndex(cell), a, b));
        }

        static int RaiseNow(ColonyWorld colony, int cell, int building, int facing = 0)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, StuffHandle.Wood, facing),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Deliver(cell, ConstructionContent.BuildingAt(building).costCount);
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
            return colony.Grid.Edifice[cell];
        }

        /// <summary>Tick until the condition holds, or give up; returns whether it held.</summary>
        static bool RunUntil(ColonyWorld colony, Func<bool> done, int maxTicks)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                if (done()) return true;
                colony.World.Tick();
            }
            return done();
        }

        // ---- laying ---------------------------------------------------------------------------

        /// <summary>
        /// The whole of decision 10 as a player meets it: a run of eight ordered lines is laid by
        /// the colony, one wood a line, into one net — with the wood the lines did not take put
        /// back down rather than lost.
        /// </summary>
        [Test]
        public void ARunOfLinesIsLaidAtOneWoodALine()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            Stock(colony, 20);
            int before = WoodOnTheBoard(colony);

            for (int x = 3; x <= 10; x++) Give(colony, IntentKind.PlaceBuilding, Open(colony, x, 6), BuildingHandle.Conduit, StuffHandle.Wood);
            colony.World.Tick();
            Assume.That(power.Sites.Count, Is.EqualTo(8));

            Assert.That(RunUntil(colony, () => power.Sites.Count == 0, 40_000), Is.True, "every line was laid");
            // The last layer is still straightening up (the settle) when the last line goes in.
            colony.World.Tick(200);
            Assert.That(power.Lines.Count, Is.EqualTo(8));
            Assert.That(power.Nets.Count, Is.EqualTo(1), "a straight run is one net");
            Assert.That(WoodOnTheBoard(colony), Is.EqualTo(before - 8), "one wood a line, and the rest set down again");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.LayConduit), Is.EqualTo(8));
        }

        /// <summary>Decision 2 end to end: a line ordered inside a standing wall is laid from beside it.</summary>
        [Test]
        public void ALineInsideAWallIsLaidFromBesideIt()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            int wall = Open(colony, 4, 4);
            RaiseNow(colony, wall, BuildingHandle.Wall);
            Stock(colony, 5);

            Give(colony, IntentKind.PlaceBuilding, wall, BuildingHandle.Conduit, StuffHandle.Wood);
            Assert.That(RunUntil(colony, () => power.IsLine(wall), 20_000), Is.True);
            Assert.That(colony.Grid.Edifice[wall], Is.GreaterThanOrEqualTo(0), "and the wall still stands");
        }

        /// <summary>
        /// A line one storey up with nothing under it — the foot of a riser — is laid from below,
        /// the way a slab is.
        /// </summary>
        [Test]
        public void ALineAStoreyUpIsLaidFromBelow()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            int high = Open(colony, 5, 3) + Size.LayerStride;
            Stock(colony, 5);

            Give(colony, IntentKind.PlaceBuilding, high, BuildingHandle.Conduit, StuffHandle.Wood);
            Assert.That(RunUntil(colony, () => power.IsLine(high), 20_000), Is.True);
        }

        /// <summary>
        /// The control for the three above: with no wood anywhere nobody takes the job, and the
        /// scan does not loop trying — the giver answers no before it looks at a single site.
        /// </summary>
        [Test]
        public void NoWoodMeansNoLineAndNoLoop()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            Assume.That(WoodOnTheBoard(colony), Is.Zero, "the bare scenario leaves no wood");
            Give(colony, IntentKind.PlaceBuilding, Open(colony, 6, 6), BuildingHandle.Conduit, StuffHandle.Wood);

            colony.World.Tick(3_000);
            Assert.That(power.Sites.Count, Is.EqualTo(1));
            Assert.That(colony.Jobs.FailedOf(JobIndex.LayConduit), Is.Zero);
            Assert.That(colony.Jobs.CompletedOf(JobIndex.LayConduit), Is.Zero);
        }

        // ---- taking up --------------------------------------------------------------------------

        /// <summary>
        /// A marked line is taken up and pays the reference's half of one — nothing or one wood,
        /// by the same seeded flip on every run of the same seed.
        /// </summary>
        [Test]
        public void AMarkedLineIsTakenUpAndTheRefundIsTheSameEveryRun()
        {
            int Run()
            {
                ColonyWorld colony = Board();
                PowerGrid power = PowerOf(colony);
                int cell = Open(colony, 5, 5);
                power.AddLine(cell);
                Give(colony, IntentKind.RemoveConduit, cell);
                Assert.That(RunUntil(colony, () => !power.IsLine(cell), 20_000), Is.True, "the line came up");
                Assert.That(power.IsMarked(cell), Is.False);
                colony.World.Tick(200);
                return WoodOnTheBoard(colony);
            }

            int first = Run();
            Assert.That(first, Is.InRange(0, 1));
            Assert.That(Run(), Is.EqualTo(first));
        }

        // ---- refuelling (§6) ---------------------------------------------------------------------

        /// <summary>
        /// Decision 9: a generator below half is filled to the top by a hauler, unasked — and the
        /// control, one at more than half, is left alone.
        /// </summary>
        [Test]
        public void AGeneratorBelowHalfIsFilledToTheTopAndOneAboveIsLeftAlone()
        {
            ColonyWorld Run(int fuelMilli, out int generator)
            {
                ColonyWorld colony = Board();
                generator = RaiseNow(colony, Open(colony, 4, 5), BuildingHandle.Generator, facing: 1);
                PowerOf(colony).SetFuelMilli(generator, fuelMilli);
                Stock(colony, 75);
                return colony;
            }

            ColonyWorld low = Run(30_000, out int lowGen);
            Assert.That(RunUntil(low, () => PowerOf(low).FuelMilli(lowGen) == 75_000, 20_000), Is.True,
                "filled to the top");
            low.World.Tick(300);
            Assert.That(WoodOnTheBoard(low), Is.EqualTo(30), "45 went in and the rest was set down");
            Assert.That(low.Jobs.CompletedOf(JobIndex.Refuel), Is.EqualTo(1));

            ColonyWorld high = Run(40_000, out int highGen);
            high.World.Tick(4_000);
            Assert.That(PowerOf(high).FuelMilli(highGen), Is.EqualTo(40_000), "more than half is left alone");
            Assert.That(high.Jobs.CompletedOf(JobIndex.Refuel) + high.Jobs.FailedOf(JobIndex.Refuel), Is.Zero);
        }

        /// <summary>A generator ordered taken down is not fed: the fuel would go with it.</summary>
        [Test]
        public void AGeneratorOrderedDownIsNotFed()
        {
            ColonyWorld colony = Board();
            int head = Open(colony, 4, 5);
            int generator = RaiseNow(colony, head, BuildingHandle.Generator, facing: 1);
            PowerOf(colony).SetFuelMilli(generator, 1_000);
            Stock(colony, 75);
            Assert.That(colony.Designations.Designate(Size.FromIndex(head), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));

            colony.World.Tick(4_000);
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Refuel), Is.Zero);
        }

        /// <summary>
        /// Every claim the three jobs make is given back: a colony that has laid, taken up and
        /// refuelled, and then sat idle, holds nothing.
        /// </summary>
        [Test]
        public void NoClaimOutlivesThePowerJobs()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            Stock(colony, 75);
            int generator = RaiseNow(colony, Open(colony, 4, 5), BuildingHandle.Generator, facing: 1);
            power.SetFuelMilli(generator, 1_000);
            for (int x = 3; x <= 6; x++) Give(colony, IntentKind.PlaceBuilding, Open(colony, x, 8), BuildingHandle.Conduit, StuffHandle.Wood);
            int doomed = Open(colony, 8, 8);
            power.AddLine(doomed);
            Give(colony, IntentKind.RemoveConduit, doomed);

            Assert.That(RunUntil(colony,
                () => power.Sites.Count == 0 && power.Marks.Count == 0 && !power.NeedsRefuel(generator), 30_000),
                Is.True);
            colony.World.Tick(2_000);

            var claims = colony.Pawns.Reservations;
            int held = 0;
            var pawns = colony.Pawns.Pawns.All;
            for (int i = 0; i < pawns.Count; i++) held += pawns[i].HeldReservations.Count;
            Assert.That(claims.ActiveClaims, Is.EqualTo(held), "every claim in the table belongs to a job that is running");
        }

        // ---- saves --------------------------------------------------------------------------------

        /// <summary>
        /// A save written before the power jobs existed still loads: its job section has twelve
        /// defs' counters, and the three new ones start at nothing. Until power this threw, which
        /// made every save taken before a new job arrived unloadable for no reason that was true.
        /// </summary>
        [Test]
        public void ASaveWithFewerJobDefsLoads()
        {
            ColonyWorld colony = Board();
            var bytes = new MemoryStream();
            using (var w = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var writer = new SaveWriter(w);
                writer.Write(40);   // started
                writer.Write(3);    // failed
                writer.Write(12);   // defs, as a pre-power build wrote them
                for (int i = 0; i < 12; i++) { writer.Write(i + 1); writer.Write(0); }
            }
            bytes.Position = 0;

            using var r = new BinaryReader(bytes);
            colony.Jobs.Load(new SaveReader(r, WorldSave.CurrentFormatVersion));

            Assert.That(colony.Jobs.CompletedOf(JobIndex.Harvest), Is.EqualTo(12));
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Refuel), Is.Zero);
        }

        /// <summary>The control: a save from a build with more job defs than this one is still refused.</summary>
        [Test]
        public void ASaveWithMoreJobDefsIsRefused()
        {
            ColonyWorld colony = Board();
            var bytes = new MemoryStream();
            using (var w = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var writer = new SaveWriter(w);
                writer.Write(0);
                writer.Write(0);
                writer.Write(JobIndex.Count + 1);
                for (int i = 0; i <= JobIndex.Count; i++) { writer.Write(0); writer.Write(0); }
            }
            bytes.Position = 0;

            using var r = new BinaryReader(bytes);
            Assert.Throws<SaveLoadException>(() => colony.Jobs.Load(new SaveReader(r, WorldSave.CurrentFormatVersion)));
        }

        /// <summary>A driver for every job def, in the def's own place — the table git once merged in silence.</summary>
        [Test]
        public void EveryJobDefHasItsDriverInItsPlace()
        {
            var content = ContentPack.Pawns();
            Assert.That(content.Jobs.Length, Is.EqualTo(JobIndex.Count));
            for (int i = 0; i < content.Jobs.Length; i++)
                Assert.That(content.Jobs[i].driver, Is.EqualTo(i), $"{content.Jobs[i].defName} names driver {content.Jobs[i].driver}");

            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assert.That(pawn.DriverPool.Length, Is.EqualTo(JobIndex.Count));
            Assert.That(pawn.DriverPool[JobIndex.LayConduit], Is.TypeOf<LayConduitJobDriver>());
            Assert.That(pawn.DriverPool[JobIndex.RemoveConduit], Is.TypeOf<RemoveConduitJobDriver>());
            Assert.That(pawn.DriverPool[JobIndex.Refuel], Is.TypeOf<RefuelJobDriver>());
        }
    }
}
