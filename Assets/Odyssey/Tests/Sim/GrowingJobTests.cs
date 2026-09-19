#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The crop as the colony lives it: a painted field is sown, ripens on the daylight clock
    /// alone, is cut, yields carrots that a colonist eats straight from the pile, and sows again
    /// — forever, with nobody queueing anything.
    ///
    /// <para>The fixture is the barren board the felling tests use, left with the scenario's
    /// own meal store: a colonist four days hungry is a mental break the crop should not have
    /// to outlast, and 144 meals is several times what two of them eat in a week. Only the
    /// eating test strips the store, because there the question is what a starving colonist
    /// walks to when carrots are all there is.</para>
    /// </summary>
    public class GrowingJobTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        static ColonyWorld Field(uint seed = 1u, int colonists = 2, bool meals = true)
        {
            // The scenario's own meal store — twelve piles of twelve — is what keeps the
            // colonists fed across the four days a field takes, with no help from the test. The
            // one test that wants a world with nothing else to eat asks for no meals: the
            // starting store is food on the ground, and a hungry colonist walks to whatever is
            // nearest, which would make the carrot pile it is asked about irrelevant.
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            // Both knobs: pile count and pile size. Zeroing only the size leaves twelve
            // zero-stack meals standing on the board, and a zero-stack meal still has its
            // nutrition, so the eater "ate" one and the carrot pile went untouched.
            if (!meals) { scenario.mealPiles = 0; scenario.mealsPerPile = 0; }
            return ColonyWorld.Build(Size, seed, scenario);
        }

        static int CarrotsAt(ColonyWorld colony, int cell)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!item.Despawned && item.DefIndex == ItemIndex.Carrots && item.Cell == cell)
                    total += item.Stack;
            }
            return total;
        }

        /// <summary>Paint a one-cell field on the colonist's own cell and see it taken.</summary>
        static void Sow(ColonyWorld colony, CellRef plot)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.DesignateZone, plot, PlantHandle.Carrot + 1));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty, "the field was refused");
        }

        [Test]
        public void ARipeCropIsTakenBeforeANewSeedGoesIn()
        {
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;
            var size = Size;

            // One cell ripe, one fallow, both zoned: the grower must take the harvest first -
            // the owner watched sowers planting beside a standing crop (2026-09-18). The
            // pipeline's own sort promises it (harvest precedes sow at equal priority); this is
            // the promise held where a playtest can meet it.
            CellRef ripe = colony.Start;
            CellRef fallow = new CellRef(colony.Start.X + 1, colony.Start.Z, colony.Start.Y);
            Sow(colony, ripe);
            Sow(colony, fallow);
            zones.Sow(size.Index(ripe.X, ripe.Z, ripe.Y));
            zones.Advance(size.Index(ripe.X, ripe.Z, ripe.Y), 1_000_000);

            bool harvestedFirst = false, sownFirst = false;
            for (int tick = 0; tick < 10_000 && !harvestedFirst && !sownFirst; tick++)
            {
                colony.World.Tick();
                if (!zones.IsPlanted(size.Index(ripe.X, ripe.Z, ripe.Y))) harvestedFirst = true;
                if (zones.IsPlanted(size.Index(fallow.X, fallow.Z, fallow.Y))) sownFirst = true;
            }
            Assert.That(harvestedFirst, Is.True, "the ripe crop was never taken at all");
            Assert.That(sownFirst, Is.False,
                "the fallow cell was sown while a ripe crop stood unharvested");
        }

        [Test]
        public void ClickingTheGroundUnderAFieldAnswersForTheZoneAboveIt()
        {
            ColonyWorld colony = Field(colonists: 1);
            Sow(colony, colony.Start);

            // A click lands on the solid ground the field is drawn on; the zone is the air cell
            // above. The pane must answer for that zone or it stays silent over every field.
            var ground = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y - 1);
            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, ground));
            colony.World.Tick();

            Assert.That(colony.World.Views.Current.TryGetCellDetail(
                Size.Index(ground.X, ground.Z, ground.Y), out CellDetail detail), Is.True);
            Assert.That(detail.ZonePlant, Is.EqualTo((byte)PlantHandle.Carrot),
                "the ground under a field names the crop growing above it");
            Assert.That(detail.CropGrowth, Is.EqualTo(ushort.MaxValue),
                "nothing is planted yet, so the pane says awaiting its seed");

            var elsewhere = new CellRef(ground.X + 5, ground.Z + 5, ground.Y);
            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, elsewhere));
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetCellDetail(
                Size.Index(elsewhere.X, elsewhere.Z, elsewhere.Y), out CellDetail bare), Is.True);
            Assert.That(bare.ZonePlant, Is.EqualTo(byte.MaxValue),
                "a cell with no zone above it says nothing about growing");
        }

        [Test]
        public void ASowerKneelsRatherThanChops()
        {
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;
            Sow(colony, colony.Start);

            // The kneel is the work: while the seed is going in, the sower is down at the soil
            // and no work focus is reported — a focus would summon the computed tool swing, and
            // the sow must not chop (owner, 2026-09-18).
            bool knelt = false, working = false, planted = false;
            for (int tick = 0; tick < 5_000 && !planted; tick++)
            {
                colony.World.Tick();
                planted = zones.IsPlanted(Size.Index(colony.Start));
                foreach (var pawn in colony.Pawns.Pawns.All)
                {
                    if (pawn.Gesture == PawnGesture.Sow) knelt = true;
                    if (pawn.Driver != null && pawn.Driver.WorkFocus >= 0) working = true;
                }
            }
            Assert.That(planted, Is.True, "the field was never sown");
            Assert.That(knelt, Is.True, "the sower never knelt at the plot");
            Assert.That(working, Is.False, "sowing reported a work focus and would have drawn a tool swing");
        }

        [Test]
        public void AHarvesterKneelsRatherThanChops()
        {
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;
            Sow(colony, colony.Start);
            colony.World.Intents.Submit(new Intent(IntentKind.DebugRipen, colony.Start));
            colony.World.Tick();
            Assume.That(zones.IsRipe(Size.Index(colony.Start)), Is.True, "the crop never ripened");

            // The pull is the sow's own kneel: while the crop is coming up the harvester is down
            // at the soil and no work focus is reported — a focus would summon the computed swing
            // and the axe it carries, and the harvest must not chop (owner, 2026-09-19).
            bool knelt = false, working = false, harvested = false;
            for (int tick = 0; tick < 5_000 && !harvested; tick++)
            {
                colony.World.Tick();
                harvested = !zones.IsPlanted(Size.Index(colony.Start));
                foreach (var pawn in colony.Pawns.Pawns.All)
                {
                    bool harvesting = pawn.CurrentJob != null &&
                        colony.Pawns.Content.Jobs[pawn.CurrentJob.DefIndex].driver == JobIndex.Harvest;
                    if (harvesting && pawn.Gesture == PawnGesture.Sow) knelt = true;
                    if (harvesting && pawn.Driver != null && pawn.Driver.WorkFocus >= 0) working = true;
                }
            }
            Assert.That(harvested, Is.True, "the crop was never pulled");
            Assert.That(knelt, Is.True, "the harvester never knelt at the plot");
            Assert.That(working, Is.False,
                "harvesting reported a work focus and would have drawn the axe");
        }

        [Test]
        public void TheYieldIsHauledToTheStockpileLikeWood()
        {
            // The owner's carry-back question (2026-09-19: "the harvest should be carried back
            // like wood"), and the answer is that it already is: the yield drops as a loose
            // haulable pile exactly as a felled trunk drops one, and the haul scan that carries
            // wood to the stockpile asks only whether a thing is haulable and where it may go.
            // This test exists so that stays true — a carrot that somehow stopped being haulable
            // or a stockpile that stopped accepting one would otherwise surface as a pile that
            // sits in the field forever, which is what the owner watched.
            ColonyWorld colony = Field();
            var zones = colony.Growing!;
            Sow(colony, colony.Start);
            colony.World.Intents.Submit(new Intent(IntentKind.DebugRipen, colony.Start));
            colony.World.Tick();

            int harvestedAt = -1;
            for (int end = 0; end <= 60_000 && harvestedAt < 0; end += 100)
            {
                colony.World.Tick(100);
                if (!zones.IsPlanted(Size.Index(colony.Start))) harvestedAt = colony.World.CurrentTick;
            }
            Assume.That(harvestedAt, Is.GreaterThan(0), "the crop was never pulled");
            Assume.That(CarrotsOnTheGround(colony), Is.GreaterThan(0), "no pile was dropped");

            // A day and a half is the wood test's own allowance: walking, sleeping and the
            // sowing the field immediately begins again all come ahead of the haul.
            colony.World.Tick(90_000);

            Assert.That(CarrotsInStockpiles(colony), Is.GreaterThan(0),
                "the harvest pile was never carried to a stockpile");
        }

        static int CarrotsOnTheGround(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!item.Despawned && item.DefIndex == ItemIndex.Carrots && item.Cell >= 0)
                    total += item.Stack;
            }
            return total;
        }

        static int CarrotsInStockpiles(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.DefIndex != ItemIndex.Carrots || item.Cell < 0) continue;
                if (colony.Pawns.Items.IsStockpileCell(item.Cell)) total += item.Stack;
            }
            return total;
        }

        [Test]
        public void AFieldIsSownRipensYieldsAndSowsAgain()
        {
            ColonyWorld colony = Field();
            var zones = colony.Growing!;
            CellRef plot = colony.Start;
            int index = Size.Index(plot);
            Sow(colony, plot);

            // Sown within the first few thousand ticks — the colonist is standing in the field —
            // but not grown: the daylight window has not opened.
            colony.World.Tick(5_000);
            Assert.That(zones.IsPlanted(index), Is.True, "the field was never sown");
            Assert.That(zones.GrowthTicks(index), Is.EqualTo(0), "nothing grows before the window opens");

            // Four daylight windows at 32,500 growth ticks each is the 130,000 the carrot asks
            // for, so a field sown on day nought is ripe before the fourth window closes at
            // 227,500. The cap is generous because the growth clock is not what can slip — the
            // sowing is, by a colonist asleep or mid-break — and the check that follows still
            // asks only whether the field ever ripened.
            int ripeAt = -1;
            for (int end = 5_250; end <= 400_000 && ripeAt < 0; end += 250)
            {
                colony.World.Tick(250);
                if (zones.IsRipe(index)) ripeAt = colony.World.CurrentTick;
            }
            Assert.That(ripeAt, Is.GreaterThan(0), "the crop never ripened");
            Assert.That(ripeAt, Is.LessThanOrEqualTo(228_000),
                "a field sown on day nought ripens inside the fourth daylight window");

            // Cut within a day of ripening: the cutting itself is 200 ticks and everything else
            // is walking, sleeping or standing a beat.
            int harvestedAt = -1;
            for (int end = 0; end <= 60_000 && harvestedAt < 0; end += 100)
            {
                colony.World.Tick(100);
                if (!zones.IsPlanted(index)) harvestedAt = colony.World.CurrentTick;
            }
            Assert.That(harvestedAt, Is.GreaterThan(0), "the ripe crop was never cut");

            // The yield is where the crop stood, five to a sowing. Read within the same 100-tick
            // step that saw the crop go: an eater needs a walk plus 300 ticks of eating to touch
            // it, so nothing can have eaten one yet.
            Assert.That(CarrotsAt(colony, index), Is.EqualTo(5), "one sowing's yield");
            Assert.That(zones.IsRipe(index), Is.False, "the cut cell is fallow");

            // And the loop closes: nobody queues anything, and the field sows itself again.
            int resownAt = -1;
            for (int end = 0; end <= 60_000 && resownAt < 0; end += 100)
            {
                colony.World.Tick(100);
                if (zones.IsPlanted(index)) resownAt = colony.World.CurrentTick;
            }
            Assert.That(resownAt, Is.GreaterThan(0), "the zone never sowed itself again");
        }

        [Test]
        public void CarrotsAreEatenStraightFromThePile()
        {
            ColonyWorld colony = Field(colonists: 1, meals: false);
            var pawn = colony.Pawns.Pawns.All[0];

            // Three carrots at the colonist's feet — the nearest cell that can take them,
            // since the cell a pawn stands in may already hold something — and the colonist
            // starving. There is nothing else to eat on this board: the eater scans for
            // nutrition and does not care that nothing cooked the carrots, and that is the
            // whole of the raw-food decision (docs/design/22-growing.md §5).
            int at = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, pawn.Cell, ItemIndex.Carrots, 3, maxRadius: 3);
            Assume.That(at, Is.GreaterThanOrEqualTo(0), "nowhere within three cells to put a pile");
            colony.Pawns.Items.Spawn(ItemIndex.Carrots, at, 3);
            pawn.Needs[NeedIndex.Food] = 1;

            colony.World.Tick(2_000);

            Assert.That(pawn.Needs[NeedIndex.Food], Is.GreaterThan(100),
                "the colonist ate, but the carrots did not feed");
            Assert.That(CarrotsAt(colony, at), Is.LessThanOrEqualTo(2),
                "one carrot from the pile, not the pile");
        }

        [Test]
        [Category("Long")]
        public void TheFieldLoopIsDeterministic()
        {
            ColonyWorld first = Field(seed: 7u);
            ColonyWorld second = Field(seed: 7u);

            foreach (ColonyWorld colony in new[] { first, second })
            {
                CellRef start = colony.Start;
                for (int dx = 0; dx < 3; dx++)
                {
                    colony.World.Intents.Submit(new Intent(
                        IntentKind.DesignateZone, new CellRef(start.X + dx, start.Z, start.Y), PlantHandle.Carrot + 1));
                }
                colony.World.Tick();
                Assert.That(colony.World.Intents.Rejected, Is.Empty);
            }

            // Six days and a bit, in steps of the growth cadence, and both fields ripen on the
            // way — which is the evidence the loop ran, in a world whose colonists have their
            // own pantry and nothing else to do.
            bool firstRipened = false, secondRipened = false;
            for (int end = 250; end <= 400_000; end += 250)
            {
                first.World.Tick(250);
                second.World.Tick(250);
                firstRipened |= first.Growing!.IsRipe(Size.Index(first.Start));
                secondRipened |= second.Growing!.IsRipe(Size.Index(second.Start));
            }

            Assert.That(firstRipened, Is.True, "the first field never ripened");
            Assert.That(secondRipened, Is.True, "the second field never ripened");
            Assert.That(first.World.ComputeStateHash().Value,
                Is.EqualTo(second.World.ComputeStateHash().Value),
                "two colonies living the same week through a field came apart");
        }
    }
}
