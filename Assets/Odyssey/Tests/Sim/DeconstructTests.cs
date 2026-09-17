#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Taking our own buildings apart: the order, the work, the refund, and the ruined city that
    /// is not ours to strip.
    /// </summary>
    public class DeconstructTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        /// <summary>Raise a wall beside the start the way the build pipeline does, and return its cell.</summary>
        static int AWallOfOurs(ColonyWorld colony, int stuff = StuffHandle.Wood)
        {
            CellRef start = colony.Start;
            for (int radius = 2; radius < 8; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;
                int cell = Size.Index(x, z, start.Y);
                if (!colony.Construction.Allows(cell)) continue;
                if (FellJobDriver.StandBeside(colony.Pawns, colony.Pawns.Pawns.All[0], cell) < 0) continue;

                colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, stuff);
                colony.Construction.Raise(colony.Pawns, cell);
                return cell;
            }

            return -1;
        }

        static int OnTheGround(ColonyWorld colony, int item)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == item) total += items[i].Stack;
            return total;
        }

        /// <summary>
        /// The whole journey: order a wall of ours pulled down, and a colonist walks over, works at
        /// it, and leaves salvage where it stood.
        /// </summary>
        [Test]
        public void AWallOfOursIsTakenApartAndHalfComesBack()
        {
            ColonyWorld colony = Board();
            int cell = AWallOfOurs(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            int woodBefore = OnTheGround(colony, ItemIndex.Wood);

            Assert.That(colony.Designations.Designate(Size.FromIndex(cell), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));

            int downAt = -1;
            for (int tick = 0; tick < 20_000 && downAt < 0; tick++)
            {
                colony.World.Tick();
                if (colony.Grid.Edifice[cell] < 0) downAt = tick;
            }

            Assert.That(downAt, Is.GreaterThanOrEqualTo(0), "nobody ever pulled the wall down");
            Assert.That(colony.Grid.IsBlockedByEdifice(cell), Is.False, "the cell is walkable again");
            Assert.That(colony.Designations.At(cell), Is.EqualTo(DesignationKind.None), "the order is spent");

            // A wall costs 5, so half is 2 or 3 — never 0, and never the whole 5.
            int refund = OnTheGround(colony, ItemIndex.Wood) - woodBefore;
            Assert.That(refund, Is.InRange(2, 3), "half of a five-unit wall is two or three");
        }

        /// <summary>
        /// The control that makes the test above mean something. A wall the generator stamped is
        /// Reclaim's and Salvage's, with their own yields and their own claim-it-first step —
        /// without this, a deconstruct that took <em>anything</em> apart would pass.
        /// </summary>
        [Test]
        public void TheRuinedCityIsNotOursToTakeApart()
        {
            ColonyWorld colony = Board();
            int cell = AWallOfOurs(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            // The same wall, in the same cell, with the one bit that differs turned off: it stops
            // being ours and the order stops being legal. Nothing else about it changes.
            var edifices = colony.Construction.Edifices.Records;
            int handle = colony.Grid.Edifice[cell];
            PlacedEdifice placed = edifices[handle];
            placed.Built = false;
            edifices[handle] = placed;

            Assert.That(colony.Designations.Designate(Size.FromIndex(cell), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Designations.CanDeconstruct(cell), Is.False);
        }

        /// <summary>
        /// A tree is felled, not deconstructed. It needs no special exclusion — a tree is the
        /// generator's, so it is never <c>Built</c> — and this is the test that says so rather than
        /// leaving it to be rediscovered.
        /// </summary>
        [Test]
        public void ATreeIsNotADeconstructionJob()
        {
            ColonyWorld colony = ColonyWorld.Build(
                Size, 3u, ScenarioDef.Bare(), barren: false, wooded: true);

            int trees = 0;
            for (int i = 0; i < colony.Grid.Size.CellCount; i++)
            {
                if (!colony.Designations.IsTree(i)) continue;
                trees++;
                Assert.That(colony.Designations.CanDeconstruct(i), Is.False,
                    "a tree offered itself as a deconstruction job");
            }

            Assume.That(trees, Is.GreaterThan(0), "the wooded board grew no trees, so this proved nothing");
        }

        /// <summary>
        /// Over many walls the coin flip lands both ways, and the average is worth less than the
        /// wall cost. A test that only asserted "some wood came back" would pass a 100% refund,
        /// which would make a wall a free warehouse.
        /// </summary>
        [Test]
        public void TheRefundIsHalfOnAverageAndNotAlwaysTheSame()
        {
            ColonyWorld colony = Board();
            var ctx = colony.Pawns;
            int twos = 0, threes = 0;

            for (int tick = 0; tick < 400; tick++)
            {
                int refund = DeconstructJobDriver.Refund(
                    ctx, cell: 1234, BuildingHandle.Wall, StuffHandle.Wood, tick);
                Assert.That(refund, Is.InRange(2, 3));
                if (refund == 2) twos++; else threes++;
            }

            Assert.That(twos, Is.GreaterThan(0), "the refund never rounded down");
            Assert.That(threes, Is.GreaterThan(0), "the refund never rounded up");
        }

        /// <summary>
        /// The same seed, cell and tick give the same answer — the refund is in the state hash, so
        /// anything else would desync a replay on the first demolished wall.
        /// </summary>
        [Test]
        public void TheSameMomentRefundsTheSameAmount()
        {
            ColonyWorld one = Board();
            ColonyWorld two = Board();

            for (int tick = 0; tick < 50; tick++)
                Assert.That(
                    DeconstructJobDriver.Refund(two.Pawns, 77, BuildingHandle.Wall, StuffHandle.Wood, tick),
                    Is.EqualTo(DeconstructJobDriver.Refund(one.Pawns, 77, BuildingHandle.Wall, StuffHandle.Wood, tick)));
        }

        /// <summary>
        /// And a cell is not permanently a "3" cell. Keyed on the cell alone — as stone yield
        /// deliberately is — every cell on the board would answer the same way for ever, which is
        /// findable and then farmable by rebuilding the good ones.
        /// </summary>
        [Test]
        public void TheSameCellCanRefundDifferentlyAtADifferentMoment()
        {
            ColonyWorld colony = Board();
            bool differed = false;
            int first = DeconstructJobDriver.Refund(colony.Pawns, 55, BuildingHandle.Wall, StuffHandle.Wood, 0);
            for (int tick = 1; tick < 200 && !differed; tick++)
                differed = DeconstructJobDriver.Refund(
                    colony.Pawns, 55, BuildingHandle.Wall, StuffHandle.Wood, tick) != first;

            Assert.That(differed, Is.True, "one cell always refunds the same, so the roll can be farmed");
        }

        /// <summary>
        /// Cancelling underneath a colonist stops the job, exactly as it does for felling. The
        /// wall must still be standing afterwards — a half-demolished wall is a whole wall.
        /// </summary>
        [Test]
        public void ACancelledOrderStopsTheJob()
        {
            ColonyWorld colony = Board();
            int cell = AWallOfOurs(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            colony.Designations.Designate(Size.FromIndex(cell), DesignationKind.Deconstruct);
            for (int tick = 0; tick < 3_000; tick++) colony.World.Tick();
            Assume.That(colony.Grid.Edifice[cell], Is.GreaterThanOrEqualTo(0), "it came down before the cancel");

            colony.World.Intents.Submit(new Intent(IntentKind.CancelDesignation, Size.FromIndex(cell)));
            for (int tick = 0; tick < 5_000; tick++) colony.World.Tick();

            Assert.That(colony.Grid.Edifice[cell], Is.GreaterThanOrEqualTo(0), "the wall came down after the order was cancelled");
            Assert.That(colony.Designations.At(cell), Is.EqualTo(DesignationKind.None));
        }

        /// <summary>
        /// A stone wall costs the same five units and gives back stone, not wood. Cheap, and it is
        /// the assertion that the refund is paid in what the thing was made of rather than in
        /// whatever the table's first material happens to be.
        /// </summary>
        [Test]
        public void TheRefundIsPaidInWhatTheThingWasMadeOf()
        {
            ColonyWorld colony = Board();
            int cell = AWallOfOurs(colony, StuffHandle.Stone);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            int stoneBefore = OnTheGround(colony, ItemIndex.Stone);
            int woodBefore = OnTheGround(colony, ItemIndex.Wood);

            colony.Designations.Designate(Size.FromIndex(cell), DesignationKind.Deconstruct);
            for (int tick = 0; tick < 20_000 && colony.Grid.Edifice[cell] >= 0; tick++) colony.World.Tick();
            // Assert, not Assume: this is the test's own claim and not a precondition of its
            // fixture. It was an Assume, and with the work giver disabled as a falsification probe
            // this test went on PASSING — inconclusive reported as green, which is the state-hash
            // defect in miniature and the reason the probe was run at all.
            Assert.That(colony.Grid.Edifice[cell], Is.LessThan(0), "nobody pulled the stone wall down");

            Assert.That(OnTheGround(colony, ItemIndex.Stone) - stoneBefore, Is.InRange(2, 3));
            Assert.That(OnTheGround(colony, ItemIndex.Wood) - woodBefore, Is.Zero, "a stone wall gave back wood");
        }

        /// <summary>
        /// An order named at the ground means the wall standing on it. Without the lift, a drag
        /// over a colony's own floor is refused in silence at every cell — the same relation
        /// felling needed for trees and building needed for sites.
        /// </summary>
        [Test]
        public void AnOrderNamedAtTheGroundMeansTheWallStandingOnIt()
        {
            ColonyWorld colony = Board();
            int cell = AWallOfOurs(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            int ground = cell - Size.LayerStride;
            Assume.That(colony.Grid.IsSolidTerrain(ground), Is.True, "the wall is not standing on solid ground");

            Assert.That(colony.Designations.Designate(Size.FromIndex(ground), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Designations.At(cell), Is.EqualTo(DesignationKind.Deconstruct),
                "the order landed on the ground rather than on the wall standing on it");
        }
    }
}
