#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Scrap metal (design 32 §14): a second payment beside a building's material, the wreckage
    /// that supplies it, and the scrap drop.
    /// </summary>
    public class ScrapMetalTests
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

        static int Open(ColonyWorld colony, int dx, int dz)
        {
            CellRef s = colony.Start;
            int cell = Size.Index(s.X + dx, s.Z + dz, s.Y);
            Assume.That(colony.Construction.Allows(cell), Is.True, "an ordinary buildable cell");
            return cell;
        }

        static int OnTheBoard(ColonyWorld colony, int item)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == item) total += items[i].Stack;
            return total;
        }

        static void Stock(ColonyWorld colony, int item, int count, int dx = 1, int dz = -3)
        {
            int at = colony.Pawns.Items.NearestCellWithSpace(colony.Grid, Open(colony, dx, dz), item, count, 8);
            Assume.That(at, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(item, at, count);
        }

        static void ClearScrap(ColonyWorld colony)
        {
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemHandle.Salvage)
                    colony.Pawns.Items.Despawn(items[i]);
        }

        static bool RunUntil(ColonyWorld colony, Func<bool> done, int maxTicks)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                if (done()) return true;
                colony.World.Tick();
            }
            return done();
        }

        // ---- two payments ---------------------------------------------------------------------

        /// <summary>
        /// A generator is paid twice — thirty of its material, twenty scrap metal — and is a frame
        /// only when both are in. The colony carries both unasked and raises it.
        /// </summary>
        [Test]
        public void AGeneratorIsFedItsWoodAndItsScrapAndThenBuilt()
        {
            ColonyWorld colony = Board();
            ClearScrap(colony);
            int head = Open(colony, 4, 5);
            Assert.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Generator, StuffHandle.Wood, 1),
                Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.OutstandingParts(head), Is.EqualTo(20));

            Stock(colony, ItemHandle.Wood, 40);
            Stock(colony, ItemHandle.Salvage, 25, dx: -2);

            Assert.That(RunUntil(colony, () => colony.Grid.Edifice[head] >= 0, 40_000), Is.True, "it was built");
            Assert.That(OnTheBoard(colony, ItemHandle.Salvage), Is.EqualTo(5), "twenty scrap metal went into it");
            Assert.That(OnTheBoard(colony, ItemHandle.Wood), Is.EqualTo(10), "and thirty wood");
        }

        /// <summary>The control: with no scrap metal anywhere the generator stays a blueprint, whatever wood it has.</summary>
        [Test]
        public void WithoutScrapMetalAGeneratorStaysABlueprint()
        {
            ColonyWorld colony = Board();
            ClearScrap(colony);
            int head = Open(colony, 4, 5);
            colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Generator, StuffHandle.Wood, 1);
            Stock(colony, ItemHandle.Wood, 40);

            colony.World.Tick(8_000);
            Assert.That(colony.Construction.Delivered(head), Is.EqualTo(30), "the wood came");
            Assert.That(colony.Construction.IsFrame(head), Is.False, "but it is not a frame");
            Assert.That(colony.Grid.Edifice[head], Is.LessThan(0));
        }

        /// <summary>A cancelled order gives back everything carried to it, the parts too.</summary>
        [Test]
        public void CancellingGivesBackTheScrapMetalAsWellAsTheWood()
        {
            ColonyWorld colony = Board();
            ClearScrap(colony);
            int head = Open(colony, 4, 5);
            colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Generator, StuffHandle.Wood, 1);
            colony.Construction.Deliver(head, 12);
            colony.Construction.DeliverParts(head, 7);

            Assert.That(colony.Construction.Cancel(Size.FromIndex(head)), Is.EqualTo(IntentRejection.None));
            Assert.That(OnTheBoard(colony, ItemHandle.Salvage), Is.EqualTo(7));
            Assert.That(OnTheBoard(colony, ItemHandle.Wood), Is.GreaterThanOrEqualTo(12));
        }

        /// <summary>The parts carried to a site survive a save, in their own section, and the hash sees them.</summary>
        [Test]
        public void PartsDeliveredSurviveASave()
        {
            ColonyWorld colony = Board();
            int head = Open(colony, 4, 5);
            colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Generator, StuffHandle.Wood, 1);
            colony.Construction.DeliverParts(head, 9);
            ulong withParts = colony.World.ComputeStateHash().Value;
            byte[] bytes = colony.Save();

            ColonyWorld again = Board();
            again.Load(bytes);
            Assert.That(again.Construction.PartsDelivered(head), Is.EqualTo(9));
            Assert.That(again.World.ComputeStateHash().Value, Is.EqualTo(withParts));

            colony.Construction.DeliverParts(head, 1);
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(withParts), "the control: parts are hashed");
        }

        /// <summary>Taking a generator down gives back half its scrap metal, beside half its material.</summary>
        [Test]
        public void DeconstructingAGeneratorGivesBackHalfItsScrapMetal()
        {
            ColonyWorld colony = Board();
            ClearScrap(colony);
            int head = Open(colony, 4, 5);
            colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Generator, StuffHandle.Wood, 1);
            colony.Construction.Deliver(head, 30);
            colony.Construction.DeliverParts(head, 20);
            Assert.That(colony.Construction.Raise(colony.Pawns, head), Is.True);
            Assert.That(colony.Designations.Designate(Size.FromIndex(head), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));

            Assert.That(RunUntil(colony, () => colony.Grid.Edifice[head] < 0, 30_000), Is.True);
            colony.World.Tick(100);
            Assert.That(OnTheBoard(colony, ItemHandle.Salvage), Is.EqualTo(10), "half of twenty");
        }

        // ---- where it comes from ------------------------------------------------------------------

        /// <summary>
        /// The played scenario scatters wreckage over the board — ten piles on the 120 × 120 board,
        /// ten to twenty-five each, none within fifteen cells of the start — and the bare one, which
        /// every golden stands on, scatters none.
        /// </summary>
        [Test]
        public void ThePlayedScenarioLeavesWreckageAndTheBareOneDoesNot()
        {
            var size = new GridSize(120, 120, 16);
            ColonyWorld played = ColonyWorld.Build(size, 7u, ScenarioDef.Playtest(), barren: true, wooded: true);

            int piles = 0;
            var items = played.Pawns.Items.Items;
            CellRef start = played.Start;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Despawned || items[i].DefIndex != ItemHandle.Salvage) continue;
                piles++;
                Assert.That(items[i].Stack, Is.InRange(10, 25));
                CellRef at = size.FromIndex(items[i].Cell);
                Assert.That(Math.Abs(at.X - start.X) >= 15 || Math.Abs(at.Z - start.Z) >= 15, Is.True,
                    "wreckage is out in the world, not at the colonists' feet");
            }
            Assert.That(piles, Is.EqualTo(10), "seven per ten thousand columns");

            Assert.That(ScenarioDef.Bare().wreckagePer10kColumns, Is.Zero, "the goldens' scenario scatters none");
        }

        /// <summary>The scrap drop is the supply drop's worker with another cargo, and lands scrap metal.</summary>
        [Test]
        public void TheScrapDropDropsScrapMetal()
        {
            ColonyWorld colony = Board();
            ClearScrap(colony);
            colony.World.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, IncidentHandle.ScrapDrop));
            colony.World.Tick();

            Assert.That(RunUntil(colony, () => OnTheBoard(colony, ItemHandle.Salvage) > 0, 2_000), Is.True);
            Assert.That(OnTheBoard(colony, ItemHandle.Salvage), Is.InRange(15, 30));
        }
    }
}
