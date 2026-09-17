#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The central claim of the <c>MS</c> milestone, written as one test: <b>a colony can be put
    /// down and picked up again, and it is the same colony.</b> The gate test of
    /// <c>docs/design/17-start-flow.md</c> §6.
    ///
    /// <para><b>What makes this different from <see cref="WorldRoundTripTests"/>.</b> That file
    /// saves one world and loads it into a second world <i>the test still has in its hand</i>,
    /// built from variables that never left scope. This one takes the file as the only thing that
    /// crosses between the two sessions: the first colony is built, played, hashed and saved inside
    /// a method that returns a single <c>ulong</c>, so by the time the comparison runs there is no
    /// reachable reference to it anywhere — and the second world is built from
    /// <see cref="WorldSave.ReadHeaderOnly"/>, not from the fixture. That is the difference between
    /// "the save format round-trips" and "a player can quit to the main menu and come back", which
    /// is what U38's load screen will actually do: read the header, build a session from what it
    /// says, restore into it (§5, "Loading is not a special path").</para>
    ///
    /// <para><b>Why it means anything at all.</b> OQ-50. Until 2026-09-17 the cell grid was in no
    /// hash, so a round-trip test could assert equality over numbers that could not see the board;
    /// <c>WorldRoundTripTests</c> proved a save round-tripped "exactly" while blind to every cell
    /// of it. The hash taken here is <c>SimWorld.ComputeStateHash</c>, which since OQ-50 walks the
    /// grid and the standing buildings as well as the tickables and the systems —
    /// <see cref="StateHashCoverageTests"/> is what holds that true field by field. And because a
    /// green round trip is worth nothing without a red one beside it, the negative control below
    /// is not optional: it was <i>run</i>, and the hashes it produces are recorded in this file's
    /// history, not assumed.</para>
    ///
    /// <para><b>Fast tier, not <c>Long</c> — measured, not guessed.</b> §6 of the design filed this
    /// under <c>Long</c> before anyone had timed it. On this machine the whole file runs in about
    /// 0.4 s: the build is ~40 ms, 20,000 ticks is ~75 ms, and each full-grid hash on a 60 × 60 × 16
    /// board is ~3 ms. A tier that already costs 11 s does not notice that, and the claim is
    /// central enough to want checking on every save rather than on whoever remembers to pass
    /// <c>--filter TestCategory=Long</c>. If the board or the tick count here ever grows enough to
    /// be felt, move it and say so.</para>
    /// </summary>
    public class SessionRoundTripTests
    {
        /// <summary>
        /// The size <see cref="WorldRoundTripTests"/> uses, not the 120 × 120 the scene loads.
        /// Chosen by measurement rather than taste: on the played board this colony fells and mines
        /// but had completed <b>no hauls at all</b> by 40,000 ticks, so a gate test run there would
        /// be quietly proving the round trip over a world where a third of the work never happened.
        /// At 60 × 60 the stockpile is close enough that hauling starts, and all three of the
        /// behaviours that change the board are under way by the time the save is taken.
        /// </summary>
        static readonly GridSize Size = new GridSize(60, 60, 16);

        const uint Seed = 20_260_917u;

        /// <summary>
        /// A third of a game day, and the number is measured rather than round. The requirement is
        /// that the world has genuinely <i>changed</i> before it is written down — trees gone,
        /// rock gone, wood moved — because a save taken at tick 0 round-trips a map that is still
        /// exactly what the seed generates, and would pass even if the grid section wrote nothing.
        /// Counted on this seed: at 6,000 ticks the colony had felled 10 and mined 4 but hauled
        /// nothing; at 20,000 it had felled 11, mined 23, hauled 10 and eaten 1. 20,000 is the
        /// first point at which all three are non-zero, and <see cref="AssertTheColonyDidRealWork"/>
        /// fails rather than passes quietly if that ever stops being true.
        /// </summary>
        const int Ticks = 20_000;

        const int SavedOnDay = 4;
        const string ColonyName = "Meridian";

        string _folder = string.Empty;

        /// <summary>
        /// A folder rather than a single file, because that is the shape U38 §5 needs: a saves
        /// directory a load screen lists. Removed whatever the outcome — a failed assertion must
        /// not leave a file behind that the next run then has to reason about.
        /// </summary>
        [SetUp]
        public void MakeASavesFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), $"odyssey-session-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void RemoveTheSavesFolder()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }

        string SavePath => Path.Combine(_folder, "meridian-day-4.oysave");

        // ------------------------------------------------------------------ the two sessions

        /// <summary>
        /// What a New game row will ask for. The wooded meadow and the playtest scenario, because
        /// this test is about a colony that has done something: a bare board has no trees and no
        /// rock, and a bare scenario gives no orders, so between them there would be nothing for
        /// the save to carry beyond five pawns walking about.
        /// </summary>
        static ColonyRequest NewGame() => new ColonyRequest
        {
            Size = Size,
            Seed = Seed,
            Scenario = ScenarioDef.Playtest(),
            Barren = true,
            Wooded = true,
            Map = MapType.Natural,
            Name = ColonyName,
        };

        /// <summary>
        /// The first session, start to finish, returning nothing but its hash.
        ///
        /// <para><b>The return type is the teardown.</b> Step five of the round trip is "drop every
        /// reference", and the cheapest way to make that true is to make it impossible to violate:
        /// the <see cref="ColonyWorld"/>, its grid, its pawns and its <see cref="ColonyRequest"/>
        /// are all locals of this method, so when it returns a <c>ulong</c> the caller has no way
        /// to reach any of them even by accident. A test that kept the first world in a variable
        /// and then "did not use it" would be one careless line away from comparing a world against
        /// itself, which is the shape of fault OQ-50 found.</para>
        /// </summary>
        static ulong PlayAndSave(string path)
        {
            ColonyWorld colony = ColonyWorld.Build(NewGame());
            GiveTheOrdersAPlayerWould(colony);
            colony.World.Tick(Ticks);

            AssertTheColonyDidRealWork(colony);

            ulong hash = colony.World.ComputeStateHash().Value;
            colony.SaveToFile(path, colony.Recipe(SavedOnDay));
            return hash;
        }

        /// <summary>
        /// Mark some trees and rock, the way a player does on their first morning.
        ///
        /// <para><b>The test gives the orders now because the game does</b> (owner, 2026-09-17: a
        /// new colony arrives with nothing marked). Before that, <see cref="ScenarioDef.Playtest"/>
        /// pre-marked a ring of trees and this test quietly lived off them — which is worth saying
        /// out loud, because it means what it was really proving was "the scenario gives orders and
        /// the colony carries them out". Giving them here is both the honest shape and the closer
        /// one to the game: an order arrives, somebody works, and the save has to carry the result.
        /// </para>
        /// </summary>
        static void GiveTheOrdersAPlayerWould(ColonyWorld colony) =>
            ColonyScenario.GiveStartingOrders(colony.Designations, colony.Outcome.StartCell,
                new ScenarioDef
                {
                    defName = "Scenario_Playtest", label = "playtest",
                    startingFellRadius = 10, startingMineRadius = 30, startingMineOutcrops = 3,
                });

        /// <summary>
        /// Without this the round trip could hold over a world nothing had happened to, and would
        /// still read as a pass. Named jobs rather than a tick count because "20,000 ticks elapsed"
        /// is not evidence that anybody did anything — a colony where every pawn wandered all day
        /// ticks just as far.
        /// </summary>
        static void AssertTheColonyDidRealWork(ColonyWorld colony)
        {
            // Not Assert.Multiple: Unity's bundled NUnit has no such method, and the Unity tier is
            // the authoritative one. Three plain assertions say the same thing and stop at the
            // first failure, which for this check is the same information.
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Fell), Is.GreaterThan(0), "no tree was felled");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Mine), Is.GreaterThan(0), "no cell was mined");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Haul), Is.GreaterThan(0), "nothing was hauled");
        }

        /// <summary>
        /// The second session, built from the header and from nothing else. This is the point of
        /// the whole file: the parameter is a <see cref="SaveHeader"/>, so the compiler will not
        /// let this method reach for the fixture's seed, size or scenario even if a later edit
        /// wants to.
        ///
        /// <para><b>This test found a gap in the header and the gap was then closed.</b> As first
        /// written, the recipe recorded the <see cref="MapType"/> but not
        /// <see cref="ColonyRequest.Barren"/> or <see cref="ColonyRequest.Wooded"/> — the pair that
        /// chooses between the three quite different boards a Natural map can be — so this method
        /// had to use defaults and rebuilt the wrong board. The hashes still matched, because the
        /// load overwrites every cell; what differed was the start cell, the outcome and the count
        /// of cells marked for work, none of which the simulation reads and all of which the camera
        /// does. Format version 3 carries both flags and this method uses them.</para>
        ///
        /// <para><b>One thing the header still cannot supply.</b>
        /// <see cref="SaveRecipe.Scenario"/> is a <c>defName</c> string and nothing in
        /// <c>Odyssey.Sim</c> turns one back into a <see cref="ScenarioDef"/> — there is no
        /// registry and no <c>ByName</c> — so the mapping below is hand-written here, and
        /// <c>OdysseyBootstrap.ScenarioFor</c> hand-writes the same two cases. That is two copies
        /// of one table and it wants a real lookup; it is left as a finding because a scenario only
        /// acts at tick zero, so a loaded world is unaffected by getting it wrong.</para>
        /// </summary>
        static ColonyWorld RebuildFrom(SaveHeader header) => ColonyWorld.Build(new ColonyRequest
        {
            Size = header.Size,
            Seed = header.Seed,
            Map = header.Recipe.Map,
            Barren = header.Recipe.Barren,
            Wooded = header.Recipe.Wooded,
            Name = header.Recipe.ColonyName,
            Scenario = ScenarioByName(header.Recipe.Scenario),
        });

        static ScenarioDef ScenarioByName(string defName)
        {
            switch (defName)
            {
                case "Scenario_Playtest": return ScenarioDef.Playtest();
                case "Scenario_Bare": return ScenarioDef.Bare();
                default:
                    Assert.Fail($"the header names scenario '{defName}' and nothing can build it");
                    return ScenarioDef.Bare();
            }
        }

        // ------------------------------------------------------------------ the claim

        [Test]
        public void AColonyPutDownAndPickedUpAgainIsTheSameColony()
        {
            ulong before = PlayAndSave(SavePath);

            // The first session is unreachable from here; collecting is not what makes that true,
            // but it makes a world that survived anyway show up as a leak rather than as a pass.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            SaveHeader listed = WorldSave.ReadHeaderOnly(SavePath);
            ColonyWorld second = RebuildFrom(listed);
            SaveHeader loaded = second.LoadFromFile(SavePath);

            Assert.That(loaded.SkippedSections, Is.Empty, "a section this build wrote was not read back");
            Assert.That(second.World.CurrentTick, Is.EqualTo(Ticks));
            Assert.That(second.World.ComputeStateHash().Value, Is.EqualTo(before),
                "the colony that came back is not the colony that was put down");
        }

        /// <summary>
        /// <b>The proof that the test above can fail</b>, and the reason it is worth running.
        ///
        /// <para><c>WorldRoundTripTests</c> asserted an exact round trip for months over a hash
        /// that could not see the map, and the control that should have caught it did not (OQ-50,
        /// found by OQ-05's own control). So the same round trip is run again here with one cell
        /// of the restored world mined away afterwards — the smallest possible difference, one
        /// terrain value out of 57,600 — and the hashes must differ. If this ever passes trivially,
        /// the hash has gone blind again and the test above is decoration.</para>
        ///
        /// <para>Run 2026-09-17 on this fixture: the honest round trip gave
        /// <c>9A94DDD9DCCD9036</c> on both sides, and mining one cell of the restored world moved
        /// it to <c>F293C9202D2ECDB5</c>. Recorded rather than assumed, which is the whole point of
        /// a control.</para>
        /// </summary>
        [Test]
        public void TheRoundTripWouldNoticeASingleCellGoingMissing()
        {
            ulong before = PlayAndSave(SavePath);

            SaveHeader listed = WorldSave.ReadHeaderOnly(SavePath);
            ColonyWorld second = RebuildFrom(listed);
            second.LoadFromFile(SavePath);

            int cell = FirstSolidCell(second);
            second.Grid.Terrain[cell] = CoreContent.TerrainAir;

            Assert.That(second.World.ComputeStateHash().Value, Is.Not.EqualTo(before),
                "one cell of the world was mined away and the round trip could not tell — " +
                "the hash is not covering the grid, which is exactly the OQ-50 fault returning");
        }

        static int FirstSolidCell(ColonyWorld colony)
        {
            for (int i = 0; i < colony.Grid.Terrain.Length; i++)
                if (colony.Grid.Terrain[i] != CoreContent.TerrainAir) return i;

            Assert.Fail("the restored board is entirely air, so mining a cell of it proves nothing");
            return -1;
        }

        // ------------------------------------------------------------------ what the header says

        /// <summary>
        /// A load screen lists a folder from headers alone (§4, §5): one row per file, colony name,
        /// day and map, with no body parsed and no world to load into. So the recipe fields have to
        /// survive the trip as surely as the cells do, and they are asserted here against a file on
        /// disk rather than against a stream, because a folder listing is what will read them.
        /// </summary>
        [Test]
        public void AFolderListingCanDescribeTheSaveWithoutOpeningIt()
        {
            PlayAndSave(SavePath);

            SaveHeader header = WorldSave.ReadHeaderOnly(SavePath);

            // Plain assertions rather than Assert.Multiple: Unity's bundled NUnit has no such
            // method, and the Unity tier is the authoritative one.
            Assert.That(header.FormatVersion, Is.EqualTo(WorldSave.CurrentFormatVersion));
            Assert.That(header.Seed, Is.EqualTo(Seed));
            Assert.That(header.Size, Is.EqualTo(Size));
            Assert.That(header.Tick, Is.EqualTo(Ticks));
            Assert.That(header.Recipe.Map, Is.EqualTo(MapType.Natural));
            Assert.That(header.Recipe.Scenario, Is.EqualTo("Scenario_Playtest"));
            Assert.That(header.Recipe.ColonyName, Is.EqualTo(ColonyName));
            Assert.That(header.Recipe.Day, Is.EqualTo(SavedOnDay));

            // The two flags U38 added after this file's own round trip found them missing. The
            // fixture is the wooded meadow, which is the board the scene actually loads and the
            // one a version 2 header could not distinguish from the full natural generator.
            Assert.That(header.Recipe.Barren, Is.True);
            Assert.That(header.Recipe.Wooded, Is.True);
        }

        /// <summary>
        /// The header says which of the three natural boards a colony was built on — and it did
        /// not until this test asked.
        ///
        /// <para><b>This was a gap when the file was first written, and the gap is the interesting
        /// part.</b> <see cref="SaveRecipe"/> recorded the <see cref="MapType"/>, and a Natural map
        /// is three quite different boards depending on <see cref="ColonyRequest.Barren"/> and
        /// <see cref="ColonyRequest.Wooded"/>: flat grass, the wooded meadow the scene loads, and
        /// the full natural generator. So a header rebuilt the wrong one.</para>
        ///
        /// <para><b>The state hash could not have caught it.</b> <c>GridSaveSection</c> writes
        /// every cell of every field, so the wrong board is entirely overwritten by the load and
        /// the hashes agree — which is exactly why U36 shipped without noticing. What is
        /// <i>not</i> overwritten is everything worldgen returns beside the cells: measured on this
        /// seed, a colony saved on the wooded board starts at (25,22,L11) with 34 cells marked for
        /// work, and the same header rebuilt on the default board starts at (30,30,L11) with none.
        /// Nothing in the simulation reads those after a load. The camera that frames a loaded
        /// colony does, so a restored game would have opened on empty ground a third of the map
        /// from the colony it had just restored.</para>
        ///
        /// <para>Format version 3 carries both flags. This test now asserts the fix rather than
        /// recording the gap: the three boards differ, their recipes differ, and a rebuild from
        /// each header lands on the board it names.</para>
        /// </summary>
        [Test]
        public void TheHeaderSaysWhichOfTheThreeNaturalBoardsAColonyWasBuiltOn()
        {
            ColonyWorld flat = ColonyWorld.Build(Board(barren: true, wooded: false));
            ColonyWorld wooded = ColonyWorld.Build(Board(barren: true, wooded: true));
            ColonyWorld full = ColonyWorld.Build(Board(barren: false, wooded: false));

            // The premise: without three genuinely different boards this test says nothing.
            Assert.That(wooded.Outcome.GridHash, Is.Not.EqualTo(flat.Outcome.GridHash),
                "the three boards are not distinguishable, so this test has nothing to say");
            Assert.That(full.Outcome.GridHash, Is.Not.EqualTo(flat.Outcome.GridHash));
            Assert.That(wooded.Start, Is.Not.EqualTo(flat.Start),
                "the boards agree on the start cell, so a mismatched rebuild would go unseen");

            // The map type still cannot tell them apart — it never could, and that is why the two
            // flags exist rather than a third MapType.
            Assert.That(wooded.Recipe(SavedOnDay).Map, Is.EqualTo(flat.Recipe(SavedOnDay).Map));

            // The recipe can.
            foreach (ColonyWorld colony in new[] { flat, wooded, full })
            {
                SaveRecipe recipe = colony.Recipe(SavedOnDay);
                ColonyRequest rebuilt = Board(recipe.Barren, recipe.Wooded);

                Assert.That(ColonyWorld.Build(rebuilt).Start, Is.EqualTo(colony.Start),
                    $"a board rebuilt from its own recipe (barren={recipe.Barren}, " +
                    $"wooded={recipe.Wooded}) is not the board that wrote it");
            }
        }

        static ColonyRequest Board(bool barren, bool wooded)
        {
            ColonyRequest request = NewGame();
            request.Barren = barren;
            request.Wooded = wooded;
            return request;
        }
    }
}
