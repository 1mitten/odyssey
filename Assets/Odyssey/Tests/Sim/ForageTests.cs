#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The wild foods and the loose stones (design 45 §6, M13): a berry bush picked by order and
    /// growing back, mushrooms under the trees, and stone lying where the dressing drew it.
    /// </summary>
    public class ForageTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        static ColonyWorld Wooded(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static int Nearest(ColonyWorld colony, System.Func<int, bool> wanted)
        {
            CellRef start = colony.Start;
            int best = -1, bestDistance = int.MaxValue;
            for (int i = 0; i < Size.CellCount; i++)
            {
                if (!wanted(i)) continue;
                CellRef at = Size.FromIndex(i);
                int distance = System.Math.Abs(at.X - start.X) + System.Math.Abs(at.Z - start.Z) + System.Math.Abs(at.Y - start.Y) * 4;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        static int Count(ColonyWorld colony, int itemDef)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == itemDef) total += items[i].Stack;
            return total;
        }

        static int RipeBush(ColonyWorld colony) => Nearest(colony, i => colony.Designations.IsRipeBerryBush(i));

        static ushort DefAt(ColonyWorld colony, int cell) =>
            colony.Designations.TryEdifice(cell, out PlacedEdifice placed) ? placed.Def : CoreContent.EdificeNone;

        // ---------------------------------------------------------------- berries

        [Test]
        public void ARipeBerryBushIsPickedByOrderAndGoesBare()
        {
            ColonyWorld colony = Wooded();
            int bush = RipeBush(colony);
            Assume.That(bush, Is.GreaterThanOrEqualTo(0), "a berry bush grows on the board");
            WildPlantDef plant = NaturalContent.WildPlantAt(NaturalContent.EdificeBerryBush)!;
            int before = Count(colony, ItemIndex.Berries);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(bush), (int)DesignationKind.Harvest));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty);
            Assert.That(colony.Designations.At(bush), Is.EqualTo(DesignationKind.Harvest));

            for (int tick = 0; tick < 12_000 && DefAt(colony, bush) == NaturalContent.EdificeBerryBush; tick++)
                colony.World.Tick();

            Assert.That(DefAt(colony, bush), Is.EqualTo(NaturalContent.EdificeBerryBushPicked), "the bush is bare");
            Assert.That(colony.Designations.At(bush), Is.EqualTo(DesignationKind.None), "one picking and the order is done");
            Assert.That(Count(colony, ItemIndex.Berries) - before, Is.EqualTo(plant.fruitCount), "a picking's berries");
            Assert.That(colony.Grid.IsUndergrowth(bush), Is.True, "a picked bush is still a bush");
            Assert.That(colony.Pawns.Items.ItemAt(bush), Is.Null, "the berries are put down beside the bush, not in it");
            Assert.That(colony.Pawns.Nature!.Picked.Count, Is.EqualTo(1));
        }

        [Test]
        public void APickedBushCannotBeOrderedAgainUntilItHasGrownBack()
        {
            ColonyWorld colony = Wooded();
            int bush = RipeBush(colony);
            Assume.That(bush, Is.GreaterThanOrEqualTo(0));
            Assert.That(colony.Pawns.Nature!.Pick(bush, colony.World.CurrentTick), Is.True);
            Assert.That(colony.Designations.Designate(colony.Grid.Size.FromIndex(bush), DesignationKind.Harvest),
                Is.EqualTo(IntentRejection.NotPermitted), "a bare bush has nothing to pick");
            Assert.That(colony.Pawns.Nature.Pick(bush, colony.World.CurrentTick), Is.False, "and cannot be picked twice");

            int regrow = NaturalContent.WildPlantAt(NaturalContent.EdificeBerryBush)!.fruitRegrowTicks;
            colony.World.Tick(regrow - 300);
            Assert.That(DefAt(colony, bush), Is.EqualTo(NaturalContent.EdificeBerryBushPicked), "not yet");
            colony.World.Tick(600);
            Assert.That(DefAt(colony, bush), Is.EqualTo(NaturalContent.EdificeBerryBush), "ripe again after its regrowth");
            Assert.That(colony.Pawns.Nature.Picked, Is.Empty);
        }

        [Test]
        public void AHarvestOrderOnAPlainBushOrATreeIsRefused()
        {
            ColonyWorld colony = Wooded();
            int plain = Nearest(colony, i => colony.Designations.IsBush(i) && !colony.Designations.IsRipeBerryBush(i));
            int tree = Nearest(colony, i => colony.Designations.IsTree(i));
            Assume.That(plain, Is.GreaterThanOrEqualTo(0));
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));
            Assert.That(colony.Designations.Designate(Size.FromIndex(plain), DesignationKind.Harvest), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Designations.Designate(Size.FromIndex(tree), DesignationKind.Harvest), Is.EqualTo(IntentRejection.NotPermitted));
        }

        [Test]
        public void APickedBushSurvivesASaveAndGrowsBackOnTime()
        {
            ColonyWorld colony = Wooded();
            int bush = RipeBush(colony);
            Assume.That(bush, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Nature!.Pick(bush, colony.World.CurrentTick);
            colony.World.Tick(250);

            var stream = new MemoryStream();
            colony.Save(stream);
            ColonyWorld restored = Wooded();
            stream.Position = 0;
            restored.Load(stream);

            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
            Assert.That(restored.Pawns.Nature!.Picked.Count, Is.EqualTo(1));
            Assert.That(DefAt(restored, bush), Is.EqualTo(NaturalContent.EdificeBerryBushPicked));
        }

        // ---------------------------------------------------------------- mushrooms and stones

        [Test]
        public void TheMapLaysLooseStonesAndMushroomsAndTheBareBoardNone()
        {
            ColonyWorld wooded = ColonyWorld.Build(new GridSize(120, 120, 16), 1u, ScenarioDef.Bare(), barren: true, wooded: true);
            NaturalGenContext gen = wooded.Outcome.Natural!.Context;
            Assert.That(gen.LooseRocks.Count, Is.GreaterThan(20), "the dressing's stones are real");
            Assert.That(gen.MushroomSpots.Count, Is.GreaterThan(0), "and the first mushrooms");
            Assert.That(Count(wooded, ItemIndex.Mushrooms), Is.GreaterThan(0));

            // Every loose stone is on walkable grass with nothing standing in it.
            foreach ((int cell, int count) in gen.LooseRocks)
            {
                Assert.That(count, Is.InRange(3, 7));
                Assert.That(wooded.Grid.IsWalkable(cell), Is.True);
                Assert.That(wooded.Grid.Edifice[cell], Is.LessThan(0));
                Assert.That(TerraceFoot.IsFoot(wooded.Grid, cell), Is.False, "the bank would hide it");
            }

            ColonyWorld bare = ColonyWorld.Build(Size, 1u, ScenarioDef.Bare(), barren: true, wooded: false);
            Assert.That(bare.Outcome.Natural!.Context.LooseRocks, Is.Empty);
            Assert.That(Count(bare, ItemIndex.Mushrooms), Is.Zero);
        }

        [Test]
        public void MushroomsComeBackSomewhereElseOnceEaten()
        {
            ColonyWorld colony = ColonyWorld.Build(new GridSize(120, 120, 16), 1u, ScenarioDef.Bare(), barren: true, wooded: true);
            // Take every mushroom off the board, as a hungry colony would.
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Mushrooms) colony.Pawns.Items.Despawn(items[i]);
            Assume.That(Count(colony, ItemIndex.Mushrooms), Is.Zero);

            colony.World.Tick(NatureSystem.MushroomIntervalTicks * 3);
            Assert.That(Count(colony, ItemIndex.Mushrooms), Is.GreaterThan(0), "more came up");
        }

        [Test]
        public void TheWildIsDeterministic()
        {
            ColonyWorld a = Wooded(5u), b = Wooded(5u);
            int bush = RipeBush(a);
            Assume.That(bush, Is.GreaterThanOrEqualTo(0));
            a.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(bush), (int)DesignationKind.Harvest));
            b.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(bush), (int)DesignationKind.Harvest));
            a.World.Tick(15_000);
            b.World.Tick(15_000);
            Assert.That(a.World.ComputeStateHash().Value, Is.EqualTo(b.World.ComputeStateHash().Value));
        }
    }
}
