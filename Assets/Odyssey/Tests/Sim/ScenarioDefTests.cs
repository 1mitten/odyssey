#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The two built-in scenarios differ in exactly one thing: whether the colony has orders
    /// before the first tick. The scene relies on Playtest giving it work, and every headless run
    /// relies on Bare giving it none, so both halves are pinned.
    /// </summary>
    public class ScenarioDefTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        static ColonyWorld Wooded(ScenarioDef scenario) =>
            ColonyWorld.Build(Size, seed: 1u, scenario, barren: true, wooded: true);

        /// <summary>The ruined city, because it is the only map that has storeys to name.</summary>
        static readonly GridSize CitySize = new GridSize(60, 60, 5);

        static ColonyWorld City(ScenarioDef scenario) =>
            ColonyWorld.Build(CitySize, seed: 1u, scenario, barren: false, chunks: null,
                mapType: MapType.RuinedCity);

        /// <summary>
        /// What the played scenario used to be: a colony with a ring of trees and the nearest
        /// outcrop already marked.
        ///
        /// <para>Kept as a fixture rather than as a shipped scenario since the owner asked for a
        /// game that starts with nothing ordered. The marking machinery is still the game's — a
        /// scenario may ask for it, and a later one almost certainly will — so it stays under test
        /// with a scenario that asks.</para>
        /// </summary>
        static ScenarioDef WithStartingOrders() =>
            new ScenarioDef
            {
                defName = "Scenario_Playtest", label = "playtest",
                startingFellRadius = 10, startingMineRadius = 30, startingMineOutcrops = 3,
            };

        /// <summary>
        /// The scene's scenario gives no orders at all (owner, 2026-09-17: *"at the start of game
        /// there are no orders, until you assign them"*).
        ///
        /// <para>Asserted on the <i>world</i> rather than on the def's three fields, because what
        /// the owner asked for is that a new colony arrives with nothing marked — and a def whose
        /// radii are zero while something else marked a tree would satisfy the fields and fail the
        /// request.</para>
        /// </summary>
        [Test]
        public void ThePlayedScenarioGivesNoOrdersAtAll()
        {
            ColonyWorld colony = Wooded(ScenarioDef.Playtest());

            Assert.That(colony.MarkedForWork, Is.Zero, "the colony arrived with work already ordered");
            Assert.That(colony.Designations.Cells, Is.Empty,
                "a cell is designated on a board nobody has given an order on");
        }

        /// <summary>
        /// A scenario that <i>does</i> ask for felling still marks every tree in its radius.
        ///
        /// <para>This was <c>PlaytestMarksEveryTreeNearTheStart</c> until the scene stopped giving
        /// orders. The machinery it covers is unchanged and still reachable — a scenario is what
        /// asks for it — so the test keeps its subject and brings its own scenario rather than
        /// being deleted along with the behaviour that used to call it.</para>
        /// </summary>
        [Test]
        public void AScenarioThatAsksForFellingMarksEveryTreeNearTheStart()
        {
            ScenarioDef scenario = WithStartingOrders();
            ColonyWorld colony = Wooded(scenario);
            CellRef start = colony.Start;
            int radius = scenario.startingFellRadius;

            int marked = 0, standing = 0;
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, start.Y);
                bool near = System.Math.Abs(x - start.X) <= radius && System.Math.Abs(z - start.Z) <= radius;
                bool ordered = colony.Designations.At(index) == DesignationKind.Fell;
                if (ordered) marked++;
                if (ordered && !near) Assert.Fail($"({x}, {z}) is marked but lies outside {radius} cells of {start}");
                if (near && colony.Designations.IsTree(index) && !ordered) standing++;
            }

            Assert.That(marked, Is.GreaterThan(0), "the colony has no felling work");
            Assert.That(standing, Is.Zero, "trees within the radius were left unmarked");
        }

        [Test]
        public void PlaytestAlsoMarksTheNearestOutcropForMining()
        {
            // Added with the mining MVP: the felling half of this used to assert the colony had
            // no other orders at all. It cannot any more, and that is the feature — a scenario
            // that asks for felling work may ask for mining work beside it.
            ScenarioDef scenario = WithStartingOrders();
            ColonyWorld colony = Wooded(scenario);

            int mine = 0;
            foreach (int cell in colony.Designations.Cells)
                if (colony.Designations.At(cell) == DesignationKind.Mine) mine++;

            Assume.That(mine, Is.GreaterThan(0),
                $"no outcrop within {scenario.startingMineRadius} cells of {colony.Start} on this seed");

            foreach (int cell in colony.Designations.Cells)
            {
                if (colony.Designations.At(cell) != DesignationKind.Mine) continue;
                Assert.That(colony.Designations.IsMinableStone(cell), Is.True,
                    $"{Size.FromIndex(cell)} is marked for mining and is not minable stone");
                Assert.That(Size.FromIndex(cell).Y, Is.GreaterThanOrEqualTo(colony.Start.Y - 1),
                    "an order was given for rock buried under the subsoil, which nobody can reach");
            }
        }

        [Test]
        public void ThePlayedBoardStartsWithMiningWorkOnEverySeed()
        {
            // The 60 x 60 x 8 board above is the test fixture; this is the board the scene loads.
            // The point of the starting order is that a playtester can see mining happen, and a
            // seed where the nearest outcrop is off in the trees shows nothing at all — so the
            // radius is checked against the board it has to work on, over several seeds rather
            // than the one that happened to be lucky.
            var playSize = new GridSize(120, 120, 16);

            for (uint seed = 1; seed <= 5; seed++)
            {
                ColonyWorld colony = ColonyWorld.Build(playSize, seed, WithStartingOrders(),
                    barren: true, wooded: true);

                int mine = 0;
                foreach (int cell in colony.Designations.Cells)
                    if (colony.Designations.At(cell) == DesignationKind.Mine) mine++;

                Assert.That(mine, Is.GreaterThan(0), $"seed {seed} starts with no stone to mine");
            }
        }

        [Test]
        public void ThePlaytestColonySplitsBetweenStoneAndTrees()
        {
            // Five identical colonists all walk to the trees, because every priority starts at 3
            // and cutting is scanned before mining. Watching both kinds of work happen at once is
            // the point of having two, so the scenario gives some of them a trade until the
            // player can set priorities from the interface.
            ScenarioDef scenario = ScenarioDef.Playtest();
            ColonyWorld colony = Wooded(scenario);

            int miners = 0, cutters = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                bool favoursMining = pawn.WorkPriority(WorkTypeIndex.Mining) <
                                     pawn.WorkPriority(WorkTypeIndex.Cutting);
                if (favoursMining) miners++; else cutters++;
            }

            Assert.That(miners, Is.EqualTo(scenario.miners), "the wrong number of colonists took up mining");
            Assert.That(cutters, Is.GreaterThan(0), "nobody is left on the trees");

            // A trade is a priority, not a restriction: a miner with no rock left still fells and
            // hauls. Nothing may be switched off.
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            for (int work = 0; work < WorkTypeIndex.Count; work++)
                Assert.That(pawn.WorkPriority(work), Is.GreaterThan(0),
                    $"colonist {pawn.Id.Value} has work type {work} disabled outright");
        }

        [Test]
        public void BothTradesFindTheirOwnWorkOnThePlayedBoard()
        {
            // The behaviour rather than the priorities: within a few thousand ticks somebody is
            // mining and somebody else is felling, on the board the scene loads. The orders are
            // given by the scenario here because the trade split is what is under test and an
            // order is only its precondition; the scene itself gives none any more.
            var playSize = new GridSize(120, 120, 16);
            ColonyWorld colony = ColonyWorld.Build(playSize, 1u, WithStartingOrders(),
                barren: true, wooded: true);

            bool sawMining = false, sawFelling = false;
            for (int tick = 0; tick < 8_000 && !(sawMining && sawFelling); tick++)
            {
                colony.World.Tick();
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    if (pawn.CurrentJob == null) continue;
                    if (pawn.CurrentJob.DefIndex == JobIndex.Mine) sawMining = true;
                    if (pawn.CurrentJob.DefIndex == JobIndex.Fell) sawFelling = true;
                }
            }

            Assert.That(sawFelling, Is.True, "nobody went to the trees");
            Assert.That(sawMining, Is.True, "nobody went to the stone");
        }

        /// <summary>
        /// A scenario that names no storey places its colony cell for cell where it always did.
        /// This is the half of OQ-47 that is a promise not to change anything: every hash, golden
        /// and ten-day soak on record was run against this placement.
        ///
        /// <para><b>Where the numbers come from.</b> They were measured on the commit <i>before</i>
        /// the storey work, in the sibling checkout, by the same signature this test computes, and
        /// they did not move. That is the whole evidence: a baked number nobody has seen the old
        /// code produce proves only that the new code is consistent with itself.</para>
        ///
        /// <para>Deliberately not "everything is on the start layer" — that is false and the first
        /// draft of this test asserted it. The old search widens through nearby layers when a
        /// column has nothing walkable at the start layer, so five colonists on a ruined city
        /// already spread across storeys before any scenario asked them to. The promise is
        /// sameness, not flatness.</para>
        /// </summary>
        [Test]
        public void AScenarioThatNamesNoStoreyPlacesExactlyWhereItAlwaysDid()
        {
            Assert.That(PlacementSignature(City(ScenarioDef.Bare())), Is.EqualTo("34/36347CC0728A29AC"),
                "the default placement moved on the ruined city");
            Assert.That(PlacementSignature(Wooded(ScenarioDef.Bare())), Is.EqualTo("34/1A786205CF734E9A"),
                "the default placement moved on the wooded map");
        }

        /// <summary>
        /// Every cell the placement filled, in the order it filled them, as one string. Coarse on
        /// purpose: it says "the same colony in the same cells" and nothing about why.
        /// </summary>
        static string PlacementSignature(ColonyWorld colony)
        {
            var parts = new List<string>();
            foreach (var pawn in colony.World.Views.Current.Pawns) parts.Add("p" + pawn.Cell);
            foreach (int cell in colony.Pawns.Items.Beds) parts.Add("b" + cell);
            foreach (var item in colony.Pawns.Items.Items)
                parts.Add("i" + item.DefIndex + ":" + item.Cell + ":" + item.Stack);
            foreach (var pile in colony.Pawns.Items.Stockpiles)
                foreach (int cell in pile.Cells) parts.Add("s" + cell);

            ulong hash = 14695981039346656037UL;
            foreach (string part in parts)
            foreach (char c in part)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return parts.Count + "/" + hash.ToString("X16");
        }

        /// <summary>
        /// And the other half: a scenario that does name storeys is obeyed. On a flat map this
        /// could not be asked at all, so it is asked on the ruined city, which has the storeys.
        ///
        /// <para>The negative control is the test above: the same map and the same seed with no
        /// offsets place a colony whose signature is the one the old code produced, beds and all.
        /// So a bed a floor up here is the offset putting it there, not the finder wandering.</para>
        /// </summary>
        [Test]
        public void AScenarioPlacesOnTheStoreysItNames()
        {
            var scenario = ScenarioDef.Bare();
            scenario.bedLayerOffset = 1;
            scenario.mealLayerOffset = 2;

            ColonyWorld colony = City(scenario);
            int floor = colony.Start.Y;

            Assert.That(colony.Placement.Beds, Is.EqualTo(scenario.beds), colony.Placement.ToString());
            Assert.That(colony.Placement.Meals, Is.EqualTo(scenario.mealPiles), colony.Placement.ToString());

            foreach (int cell in colony.Pawns.Items.Beds)
                Assert.That(CitySize.FromIndex(cell).Y, Is.EqualTo(floor + 1), "a bed is not on the storey asked for");

            foreach (var item in colony.Pawns.Items.Items)
            {
                if (item.DefIndex != ItemIndex.Meal || item.Cell < 0) continue;
                Assert.That(CitySize.FromIndex(item.Cell).Y, Is.EqualTo(floor + 2), "a meal is not on the storey asked for");
            }
        }

        /// <summary>
        /// A storey with nothing walkable on it places nothing, and says so. The alternative —
        /// falling back to the floor below — is worse than a shortfall: it silently gives a
        /// scenario a colony it did not ask for, and a run built on it would prove the wrong
        /// thing. The city is five layers and the start is on the second, so +3 is off the top of
        /// the shells.
        /// </summary>
        [Test]
        public void AStoreyThatCannotBeReachedPlacesNothingRatherThanPlacingItLower()
        {
            var scenario = ScenarioDef.Bare();
            scenario.bedLayerOffset = 3;

            ColonyWorld colony = City(scenario);

            Assert.That(colony.Placement.Beds, Is.Zero, colony.Placement.ToString());
            Assert.That(colony.Pawns.Items.Beds.Count, Is.Zero, "a bed was placed on a storey that was not asked for");
            Assert.That(colony.Placement.Colonists, Is.EqualTo(scenario.colonists),
                "the rest of the colony should be unaffected by a storey that could not be filled");
        }

        /// <summary>
        /// Bare is the measurement baseline and has not moved.
        ///
        /// <para><b>This test used to say the two scenarios differed only in their orders.</b>
        /// They differ in the starting kit as well since 2026-09-18, and the reason the old
        /// assertion is not simply relaxed is that its real job was to stop Bare drifting: the
        /// golden table and the ten-day soak build on Bare, so an edit to the field defaults —
        /// which is how the kit would most naturally have been changed — re-bakes three hashes
        /// without anybody meaning to. So the numbers are pinned outright here instead of being
        /// pinned to Playtest's, which are now free to be tuned.</para>
        /// </summary>
        [Test]
        public void BareIsTheBaselineAndGivesNoOrders()
        {
            ColonyWorld bare = Wooded(ScenarioDef.Bare());

            Assert.That(bare.Designations.Count, Is.Zero, "a bare scenario gave an order");
            Assert.That(bare.Placement.Meals, Is.EqualTo(12), "the baseline's pantry moved");
            Assert.That(bare.Placement.Salvage, Is.EqualTo(8), "the baseline's salvage moved");
            Assert.That(bare.Placement.MaterialPiles, Is.Zero,
                "the baseline was given a building kit, which re-bakes every golden");
        }

        /// <summary>
        /// What a player starts with (owner, 2026-09-18): three meal piles, no scrap, and a couple
        /// of piles each of stone and wood. Asserted on the placement rather than on the def, so
        /// that a kit the map has nowhere to put fails here rather than in a playtest.
        /// </summary>
        [Test]
        public void PlaytestStartsWithABuildingKitAndNoScrap()
        {
            ColonyWorld playtest = Wooded(ScenarioDef.Playtest());

            Assert.That(playtest.Designations.Count, Is.Zero, "the playtest scenario gave an order");
            Assert.That(playtest.Placement.Meals, Is.EqualTo(3), playtest.Placement.ToString());
            Assert.That(playtest.Placement.Salvage, Is.Zero, "a new colony was given scrap");
            Assert.That(playtest.Placement.MaterialPiles, Is.EqualTo(4), playtest.Placement.ToString());

            int stone = 0, wood = 0;
            foreach (var item in playtest.Pawns.Items.Items)
            {
                if (item.Cell < 0) continue;
                if (item.DefIndex == ItemIndex.Stone) stone += item.Stack;
                else if (item.DefIndex == ItemIndex.Wood) wood += item.Stack;
            }

            // A full stack apiece, and the assertion is on the total rather than on the pile count
            // because the thing a player cares about is how much there is to build with — and
            // because an unclamped pile would pass a count and fail this.
            Assert.That(stone, Is.EqualTo(150), "stone to build with");
            Assert.That(wood, Is.EqualTo(150), "wood to build with");
        }
    }
}
