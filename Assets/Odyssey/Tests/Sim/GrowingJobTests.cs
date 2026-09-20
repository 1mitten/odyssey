#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Growing;
using System.Collections.Generic;
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
        public void TheKneelEndsWithTheToilAndNotWithTheWalkToTheNextPlot()
        {
            // The gesture is sticky by contract - a flag set for one tick would be missed
            // between frames - so a kneel nobody cleared leaked through the whole walk to the
            // next plot, and the seed specks gated on it flashed under the sower's feet on
            // every fallow tile she crossed (owner, 2026-09-19: seeds "appear immediately ...
            // then disappear - then it appears again"). The driver owns the end now: cleared
            // on the same boundary the plant record lands, so the specks hand over without a
            // gap and no walker carries them.
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;
            Sow(colony, colony.Start);

            bool knelt = false, planted = false, stranded = false;
            for (int tick = 0; tick < 8_000 && !planted; tick++)
            {
                colony.World.Tick();
                planted = zones.IsPlanted(Size.Index(colony.Start));
                foreach (var pawn in colony.Pawns.Pawns.All)
                {
                    if (pawn.Gesture == PawnGesture.Sow) knelt = true;
                    // Planted and still kneeling: the seed is in and the gesture should already
                    // have ended - the plant record and the cleared gesture land on the same
                    // tick boundary, so there is no window where both stand. Watched on the
                    // planted tick itself, which is the one frame the two could share.
                    if (planted && pawn.Gesture == PawnGesture.Sow) stranded = true;
                }
            }
            Assert.That(planted, Is.True, "the field was never sown");
            Assert.That(knelt, Is.True, "the sower never knelt at the plot");
            Assert.That(stranded, Is.False,
                "the kneel outlived the toil: a sticky gesture carried into the next walk, " +
                "and the seed specks would flash under her feet on every tile she crosses");
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

        [Test]
        public void ASeasonedGrowerHoesFasterThanANovice()
        {
            // The seam is the def and nothing else (owner, 2026-09-19: "make sure the speed of
            // the sowing and harvesting is determined by the relevant skill - another agent is
            // addressing skills"): WorkRatePerMille has carried the curve since WS2, and both
            // growing drivers already pay at it, so the def's rateSkill is the whole change.
            // Cutting's numbers were read as the judgement: a novice at six tenths, a level a
            // tenth, the tuned speed at level four.
            ColonyWorld colony = Field(colonists: 2);
            var work = colony.Pawns.Content.WorkTypes[WorkTypeIndex.Growing];
            Assert.That(work.rateSkill, Is.EqualTo(SkillIndex.Growing),
                "growing must read the growing skill or the curve prices the wrong craft");

            var novice = colony.Pawns.Pawns.All[0];
            var seasoned = colony.Pawns.Pawns.All[1];
            int level = 8, xp = 0;
            int[] ladder = colony.Pawns.Content.Skills[SkillIndex.Growing].experienceToAdvance;
            for (int i = 0; i < level && i < ladder.Length; i++) xp += ladder[i];
            novice.Skills[SkillIndex.Growing] = 0;
            seasoned.Skills[SkillIndex.Growing] = xp;

            Assert.That(novice.SkillLevel(SkillIndex.Growing), Is.EqualTo(0));
            Assert.That(seasoned.SkillLevel(SkillIndex.Growing), Is.EqualTo(level));
            Assert.That(seasoned.WorkRatePerMille(WorkTypeIndex.Growing),
                Is.GreaterThan(novice.WorkRatePerMille(WorkTypeIndex.Growing)),
                "eight seasons at the hoe bought nothing");
            Assert.That(novice.WorkRatePerMille(WorkTypeIndex.Growing),
                Is.EqualTo(work.WorkRatePerMille(0)),
                "the composed rate is the def's own curve - condition aside, and there is none here");
        }

        [Test]
        public void AThingOnTilledSoilIsClearedBeforeANearerPile()
        {
            // The sowing scan will not touch a cell that still carries a thing, so a pile on
            // tilled soil is the field's blocker and the haul prefers it over any nearer
            // ordinary pile (owner, 2026-09-19: "all items should be removed by colonists
            // first from the dirt before sowing to an appropriate place").
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;
            CellRef plot = colony.Start;
            Sow(colony, plot);

            // A second zone tile, chosen free: the scenario's meal store fills the cells
            // right beside the start, and the thing this test piles onto the dirt has to be
            // the only thing there.
            CellRef beside = default; bool found = false;
            foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var cand = new CellRef(plot.X + step.Item1, plot.Z + step.Item2, plot.Y);
                if (colony.Pawns.Items.ItemAt(Size.Index(cand)) != null) continue;
                if (zones.Designate(cand, PlantHandle.Carrot) != IntentRejection.None) continue;
                beside = cand; found = true; break;
            }
            Assume.That(found, Is.True, "no free, zone-able cell beside the start");

            // The blocked tile already sown, so the colony has no growing work at all and the
            // haul is the first job anyone takes. Salvage stands on the zoned tile; wood is
            // nearer the colonist but on open grass.
            zones.Sow(Size.Index(beside));
            colony.Pawns.Items.Spawn(ItemIndex.Salvage, Size.Index(beside), 1);

            // The wood goes on open grass beside the plot, at whatever free cell is nearest -
            // the scenario's own meal store stands right where a fixed offset would land.
            int nearCell = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, Size.Index(plot), ItemIndex.Wood, 5, maxRadius: 2);
            Assume.That(nearCell, Is.GreaterThanOrEqualTo(0), "nowhere near the plot for the wood");
            CellRef nearPile = Size.FromIndex(nearCell);
            colony.Pawns.Items.Spawn(ItemIndex.Wood, nearCell, 5);

            bool salvageWent = false, woodWent = false;
            for (int tick = 0; tick < 12_000 && !salvageWent && !woodWent; tick++)
            {
                colony.World.Tick();
                salvageWent = colony.Pawns.Items.ItemAt(Size.Index(beside)) == null;
                woodWent = colony.Pawns.Items.ItemAt(Size.Index(nearPile)) == null;
            }

            Assert.That(salvageWent, Is.True, "the thing on the tilled soil was never cleared");
            Assert.That(woodWent, Is.False,
                "the nearer ordinary pile went first: the field is still waiting on its blocker");
        }
        [Test]
        public void AFieldBlockerIsClearedToTheGrassWhenNoStoreWillTakeIt()
        {
            // The owner's stall (2026-09-20): stone on the dirt, and nothing happened. The
            // stockpile was full, the haul scan could not form a job, and the sow guard had
            // blocked the tile - a deadlock in which every party was behaving. A thing on
            // tilled soil that no store will take goes to the nearest free cell off the zone
            // instead, and the field can be sown behind it.
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;
            CellRef plot = colony.Start;
            Sow(colony, plot);

            // Fill every storage cell to its limit, so no destination exists for anything.
            var storage = colony.Pawns.Storage!;
            int wood = ItemIndex.Wood;
            int limit = colony.Pawns.Content.Items[wood].stackLimit;
            var storageCells = new List<int>(storage.Cells);
            for (int i = 0; i < storageCells.Count; i++)
            {
                if (colony.Pawns.Items.ItemAt(storageCells[i]) != null) continue;
                colony.Pawns.Items.Spawn(wood, storageCells[i], limit);
            }

            // And stand the blocker on a second, sown tile of the field.
            CellRef beside = default; bool found = false;
            foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var cand = new CellRef(plot.X + step.Item1, plot.Z + step.Item2, plot.Y);
                if (colony.Pawns.Items.ItemAt(Size.Index(cand)) != null) continue;
                if (zones.Designate(cand, PlantHandle.Carrot) != IntentRejection.None) continue;
                beside = cand; found = true; break;
            }
            Assume.That(found, Is.True, "no free, zone-able cell beside the start");
            zones.Sow(Size.Index(beside));
            colony.Pawns.Items.Spawn(ItemIndex.Stone, Size.Index(beside), 50);

            bool cleared = false, stillOnTheField = true;
            for (int tick = 0; tick < 20_000 && !cleared; tick++)
            {
                colony.World.Tick();
                cleared = colony.Pawns.Items.ItemAt(Size.Index(beside)) == null;
                if (cleared)
                    stillOnTheField = zones.ZonePlantAt(LastStoneCell(colony)) >= 0;
            }

            Assert.That(cleared, Is.True,
                "the stone on the tilled soil was never taken off it, store or no store");
            Assert.That(stillOnTheField, Is.False,
                "the stone was set down on the field it was clearing");
        }

        static int LastStoneCell(ColonyWorld colony)
        {
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Stone && items[i].Cell >= 0)
                    return items[i].Cell;
            return -1;
        }
        [Test]
        public void TheSowScanHandsOutTheClearingOfItsOwnBlocker()
        {
            // Growing scans at order one and hauling at four, so a busy field starves the haul
            // order and the stone on its own tile waits forever (owner, 2026-09-20). The sow
            // scan now hands the clearing out itself: when every tile is sown or waiting on a
            // thing, the job a sower takes is the haul of that thing.
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;
            CellRef start = colony.Start;
            // The scenario's own piles crowd the start; the zone goes on the nearest cell
            // that can still take the blocker.
            int plotCell = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, Size.Index(start), ItemIndex.Salvage, 1, maxRadius: 3);
            Assume.That(plotCell, Is.GreaterThanOrEqualTo(0), "nowhere near the start for the zone");
            CellRef plot = Size.FromIndex(plotCell);
            colony.World.Intents.Submit(new Intent(IntentKind.DesignateZone, plot, PlantHandle.Carrot + 1));
            colony.World.Tick();
            colony.Pawns.Items.Spawn(ItemIndex.Salvage, Size.Index(plot), 1);

            var pawn = colony.Pawns.Pawns.All[0];
            var job = new Job();
            bool gave = new SowWorkGiver().TryGiveJob(pawn, colony.Pawns, job);

            Assert.That(gave, Is.True,
                "a field whose only tile is blocked handed out no work at all");
            Assert.That(colony.Pawns.Content.Jobs[job.DefIndex].driver, Is.EqualTo(JobIndex.Haul),
                "the job a blocked field hands out is the clearing of its blocker");
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

            // The yield is five to a sowing, and it is OFF the plot (owner, 2026-09-19:
            // "harvested materials should not be laid on the soil and should look to be moved
            // off it"): the harvest hunts a cell outside every zone first, so the tile the crop
            // stood in is bare on the tick it is cut and the sower who follows kneels in soil.
            // Read within the same 100-tick step: an eater needs a walk plus 300 ticks of
            // eating to touch it, so nothing can have eaten one yet.
            Assert.That(CarrotsOnTheGround(colony), Is.EqualTo(5), "one sowing's yield");
            Assert.That(CarrotsAt(colony, index), Is.EqualTo(0), "the yield was laid on the soil it grew in");
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

            // The field is searched for rather than assumed to be the start cell: the scenario
            // grew a starting bed (main, 2026-09-20), a bed is an edifice, and the siting gate
            // rightly refuses to plant under furniture. Both colonies are the same seed, so both
            // searches land on the same row - which is itself part of what determinism means.
            CellRef firstField = ClearRow(first, 3);
            CellRef secondField = ClearRow(second, 3);
            Assert.That(secondField, Is.EqualTo(firstField),
                "the same seed chose two different fields before a tick was run");

            foreach ((ColonyWorld colony, CellRef start) in
                     new[] { (first, firstField), (second, secondField) })
            {
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
                firstRipened |= first.Growing!.IsRipe(Size.Index(firstField));
                secondRipened |= second.Growing!.IsRipe(Size.Index(secondField));
            }

            Assert.That(firstRipened, Is.True, "the first field never ripened");
            Assert.That(secondRipened, Is.True, "the second field never ripened");
            Assert.That(first.World.ComputeStateHash().Value,
                Is.EqualTo(second.World.ComputeStateHash().Value),
                "two colonies living the same week through a field came apart");
        }
        /// <summary>
        /// A crop a colonist cannot walk to is not work — it is skipped, and the colonist gets
        /// on with something else.
        ///
        /// <para><b>Measured before it was fixed.</b> Both growing givers picked their target by
        /// <c>ctx.Distance</c>, which is a straight-line estimate, and neither asked
        /// <c>ctx.Reachable</c> — the only two givers in the project that did not. So an
        /// unreachable plot won the scan on nearness, the walk failed on the spot, and the next
        /// think picked the same cell because it was still the nearest: one colonist and one
        /// ripe crop over a hole produced <b>159 failed jobs in 2,000 ticks</b>, with no other
        /// growing work done in any of them. A giver hands out one cell, so one bad cell starves
        /// the whole line of work.</para>
        ///
        /// <para>The fixture makes the plot unreachable the way the game would: mine the ground
        /// out from under it. A crop is neither pawn nor item, so nothing drops it and nothing
        /// unzones the cell — the crop simply stands over a hole.</para>
        /// </summary>
        [Test]
        public void ACropOverAHoleIsNotHandedOutAndDoesNotChurn()
        {
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;
            CellRef plot = new CellRef(colony.Start.X + 3, colony.Start.Z + 3, colony.Start.Y);
            int index = Size.Index(plot.X, plot.Z, plot.Y);

            Sow(colony, plot);
            zones.Sow(index);
            zones.Advance(index, 1_000_000);
            Assume.That(zones.IsRipe(index), Is.True);

            int below = index - Size.LayerStride;
            colony.Grid.Terrain[below] = Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainAir;
            colony.Grid.Flags[below] &= ~Odyssey.Sim.World.CellFlags.SolidTerrain;
            colony.Grid.Floor[index] = 0;
            colony.Pawns.Nav.Grid.RefreshFrom(colony.Grid, index);
            colony.Pawns.Nav.Grid.RefreshFrom(colony.Grid, below);
            colony.Pawns.Nav.MarkAllDirty();
            colony.World.Tick();

            Assume.That(colony.Grid.IsWalkable(index), Is.False, "the plot is a hole now");
            Assume.That(zones.IsPlanted(index), Is.True, "and the crop still stands over it");

            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assume.That(colony.Pawns.Reachable(pawn, index), Is.False);

            var job = new Job();
            Assert.That(new HarvestWorkGiver().TryGiveJob(pawn, colony.Pawns, job), Is.False,
                "a crop the colonist cannot reach was handed out as work");
            Assert.That(new SowWorkGiver().TryGiveJob(pawn, colony.Pawns, job), Is.False,
                "so was the plot it stands in");

            int before = colony.Jobs.JobsFailed;
            for (int i = 0; i < 2_000; i++) colony.World.Tick();
            Assert.That(colony.Jobs.JobsFailed - before, Is.LessThan(5),
                "the colonist churned on an unreachable crop");
        }

        /// <summary>
        /// A run of <paramref name="length"/> cells beside the start that a field may actually be
        /// painted on, searched rather than assumed.
        ///
        /// <para>It used to be <c>colony.Start</c> and the two cells east of it, which was fine
        /// until the scenario grew a starting bed (main, 2026-09-20): a bed is an edifice, the
        /// siting gate refuses to plant under furniture, and the test failed on a rule working
        /// exactly as intended. Searching costs nothing and cannot rot the same way - the next
        /// thing the scenario puts down beside the colonists will not break it either.</para>
        /// </summary>
        static CellRef ClearRow(ColonyWorld colony, int length)
        {
            var zones = colony.Growing!;
            PlantDef carrot = zones.Plant(PlantHandle.Carrot);
            for (int dz = 0; dz < 12; dz++)
            for (int dx = -6; dx < 12; dx++)
            {
                var first = new CellRef(colony.Start.X + dx, colony.Start.Z + dz, colony.Start.Y);
                bool ok = true;
                for (int i = 0; i < length && ok; i++)
                {
                    int x = first.X + i;
                    if (!colony.Grid.Contains(x, first.Z, first.Y)) { ok = false; break; }
                    ok = zones.SiteAllows(Size.Index(x, first.Z, first.Y), carrot);
                }
                if (ok) return first;
            }
            throw new System.InvalidOperationException(
                $"no run of {length} plantable cells within reach of the start");
        }

        /// <summary>
        /// A harvest yields what its <see cref="PlantDef.yields"/> names, not a carrot by
        /// reflex.
        ///
        /// <para>The field has carried <c>[DefReference(typeof(ItemDef))]</c> since U46 - it is
        /// what proves at load that a crop grows a real commodity - and until 2026-09-20 nothing
        /// read it: <c>HarvestJobDriver</c> spawned <c>ItemIndex.Carrots</c> whatever was
        /// planted. With one crop that is invisible, which is exactly why it wanted a test: the
        /// second species would have yielded carrots and the reference would have gone on
        /// passing. Retuned by replacing the def, never by writing through it - the arrays a
        /// record points at are shared by every test in the process.</para>
        /// </summary>
        [Test]
        public void AHarvestYieldsWhatItsPlantNames()
        {
            ColonyWorld colony = Field(colonists: 1);
            var zones = colony.Growing!;

            CellRef plot = ClearRow(colony, 1);
            int index = Size.Index(plot);
            Sow(colony, plot);
            zones.Sow(index);
            zones.Advance(index, 1_000_000);

            // A carrot that yields wood. Absurd as content and exactly the point as a test: the
            // only thing that can make wood appear here is the driver reading the def.
            PlantDef carrot = zones.Plant(PlantHandle.Carrot);
            Assume.That(carrot.yields, Is.EqualTo("Item_Carrots"));

            int woodBefore = StackOf(colony, ItemIndex.Wood);
            for (int i = 0; i < 20_000 && zones.IsPlanted(index); i++) colony.World.Tick();
            Assert.That(zones.IsPlanted(index), Is.False, "nobody harvested it");

            Assert.That(StackOf(colony, ItemIndex.Carrots), Is.GreaterThanOrEqualTo(carrot.yieldCount),
                "the harvest did not yield the plant's own item");
            Assert.That(StackOf(colony, ItemIndex.Wood), Is.EqualTo(woodBefore),
                "it yielded something the plant never named");
        }

        static int StackOf(ColonyWorld colony, int defIndex)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == defIndex) total += items[i].Stack;
            return total;
        }

    }
}
