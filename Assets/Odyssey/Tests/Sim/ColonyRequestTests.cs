#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The two things a composition root asks for that a headless run does not: a clock that does
    /// not start at midnight, and a snapshot contributor mirroring the grid for the renderer.
    ///
    /// <para><b>Why these are here at all.</b> The play scene used to build its world by hand —
    /// the same dozen lines as <see cref="ColonyWorld.Build"/>, re-typed — precisely because
    /// <c>Build</c> could not express those two things. The copy had already drifted: it never
    /// registered the map's connectors with the navigation graph and never ran the full support
    /// solve, so the one build a player actually ran was the one build no test covered. It also
    /// never assembled a <see cref="ColonyWorld.SaveComponents"/> list, which is the whole reason
    /// the playable scene could not write a save file. Closing that gap is this file's subject.</para>
    ///
    /// <para>The standing rule these tests defend: <b>nothing presentation contributes may reach
    /// simulation state</b>. A mirror writes into the published frame and into nothing else, so a
    /// world built with one must hash exactly like a world built without one.</para>
    /// </summary>
    public class ColonyRequestTests
    {
        // Small on purpose: this file is about wiring, not about worldgen, and the fast tier is a
        // thing you run on every save.
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyRequest Request(uint seed = 1u) => new ColonyRequest
        {
            Size = Size,
            Seed = seed,
            Scenario = ScenarioDef.Bare(),
        };

        /// <summary>
        /// Records that it ran and what it was handed, and writes nothing. Standing in for
        /// <c>GridMirrorContributor</c>, which lives in the presentation assembly and cannot be
        /// referenced from here.
        /// </summary>
        sealed class RecordingMirror : ISnapshotContributor
        {
            public int Contributions;
            public readonly List<int> TicksSeen = new List<int>();

            public void Contribute(SimWorld world, SnapshotWriter writer)
            {
                Contributions++;
                TicksSeen.Add(world.CurrentTick);
            }
        }

        [Test]
        public void ARequestBuildsTheSameWorldAsThePositionalCall()
        {
            // The positional overload is kept so the fifteen existing call sites do not churn;
            // this is the test that it stays a thin wrapper rather than a second build path.
            ColonyWorld positional = ColonyWorld.Build(Size, seed: 4242u, ScenarioDef.Bare());
            ColonyWorld requested = ColonyWorld.Build(Request(4242u));

            Assert.That(requested.World.ComputeStateHash(), Is.EqualTo(positional.World.ComputeStateHash()),
                "the request path built a different world from the positional one");
            Assert.That(requested.Outcome.GridHash, Is.EqualTo(positional.Outcome.GridHash));
            Assert.That(requested.Placement.Colonists, Is.EqualTo(positional.Placement.Colonists));
        }

        [Test]
        public void AMirrorChangesNothingAboutSimulationState()
        {
            var mirror = new RecordingMirror();

            ColonyWorld without = ColonyWorld.Build(Request(7u));
            ColonyRequest with = Request(7u);
            with.Mirror = (grid, outcome) => mirror;
            ColonyWorld observed = ColonyWorld.Build(with);

            without.World.Tick(120);
            observed.World.Tick(120);

            Assert.That(observed.World.ComputeStateHash(), Is.EqualTo(without.World.ComputeStateHash()),
                "attaching a presentation mirror moved simulation state — it must only write the frame");
        }

        [Test]
        public void TheMirrorIsBuiltOnceFromTheGeneratedGrid()
        {
            CellGrid? handedGrid = null;
            MapGenOutcome? handedOutcome = null;
            int calls = 0;

            ColonyRequest request = Request();
            request.Mirror = (grid, outcome) =>
            {
                calls++;
                handedGrid = grid;
                handedOutcome = outcome;
                return new RecordingMirror();
            };

            ColonyWorld colony = ColonyWorld.Build(request);

            // A factory rather than a ready-made contributor, because the mirror it stands for is
            // built *from* the generated grid and cannot exist before generation has run.
            Assert.That(calls, Is.EqualTo(1), "the mirror factory ran more than once");
            Assert.That(handedGrid, Is.SameAs(colony.Grid), "the mirror was built from some other grid");
            Assert.That(handedOutcome, Is.SameAs(colony.Outcome));
        }

        [Test]
        public void TheMirrorPublishesFromTheVeryFirstTick()
        {
            // The composition root ticks once during setup to prime the renderer: until the world
            // has ticked, there is no published frame and nothing to draw. If the mirror were
            // registered after that tick the first frame would be empty.
            var mirror = new RecordingMirror();
            ColonyRequest request = Request();
            request.Mirror = (grid, outcome) => mirror;

            ColonyWorld colony = ColonyWorld.Build(request);
            Assert.That(mirror.Contributions, Is.Zero, "nothing is published before the world has ticked");

            colony.World.Tick();

            Assert.That(mirror.Contributions, Is.EqualTo(1));
            Assert.That(mirror.TicksSeen[0], Is.Zero, "the first publish carries the tick that built it");
        }

        [Test]
        public void TheClockCanStartAwayFromMidnight()
        {
            // A colony that starts at tick 0 starts at 00:00. The scene wants noon; the hour is
            // converted to a tick by the caller, because the calendar lives in the Hud assembly
            // and the simulation may not reference it.
            const int Noon = 12 * 2_500;
            ColonyRequest request = Request();
            request.StartTick = Noon;

            ColonyWorld colony = ColonyWorld.Build(request);

            Assert.That(colony.World.CurrentTick, Is.EqualTo(Noon));
        }

        [Test]
        public void AStartTickOfZeroLeavesEveryBakedHashAlone()
        {
            // The default has to be inert, or every golden value in the repository moves the day
            // this lands.
            ColonyWorld plain = ColonyWorld.Build(Size, seed: 1u, ScenarioDef.Bare());
            ColonyWorld requested = ColonyWorld.Build(Request());

            Assert.That(requested.World.CurrentTick, Is.Zero);
            Assert.That(requested.World.ComputeStateHash(), Is.EqualTo(plain.World.ComputeStateHash()));
        }

        [Test]
        public void TwoLiveWorldsAtOnceDoNotShareAnything()
        {
            // The premise the whole start flow rests on: a session can build a world, drop it and
            // build another. Interleaved, with the first still alive, because that is what catches
            // a hidden static — a Def cache, the work-giver scan, a stream somebody kept. A
            // sequential build-drop-build would pass straight over anything merely *initialised*
            // once, which is the failure that would matter.
            ColonyWorld first = ColonyWorld.Build(Request(3u));
            ColonyWorld second = ColonyWorld.Build(Request(3u));

            first.World.Tick(500);
            second.World.Tick(500);

            Assert.That(second.World.ComputeStateHash(), Is.EqualTo(first.World.ComputeStateHash()),
                "two worlds from one seed diverged while both were alive — something is shared between them");
            Assert.That(second.Grid, Is.Not.SameAs(first.Grid));
            Assert.That(second.Pawns, Is.Not.SameAs(first.Pawns));
        }

        [Test]
        public void TheMapsConnectorsReachTheNavigationGraph()
        {
            // The regression the hand-written copy in the composition root carried: it built the
            // navigation graph and rebuilt it without ever registering the map's connectors, so a
            // stair joined no region and the two storeys it linked were unreachable from each
            // other. Natural maps have no connectors, which is the only reason nobody saw it.
            ColonyRequest request = Request();
            request.Map = MapType.RuinedCity;
            request.Barren = false;

            ColonyWorld colony = ColonyWorld.Build(request);
            Assert.That(colony.Outcome.Connectors, Is.Not.Empty, "a city map with no stairs proves nothing");

            StampedConnector stair = colony.Outcome.Connectors[0];
            Assert.That(colony.Pawns.Nav.Reachable(stair.LowerCells[0], stair.UpperCells[0], TraverseMode.Colonist),
                Is.True, "the two ends of a stair are not reachable from each other — it joined no region");
        }

        [Test]
        public void TheFullSupportSolveAgreesWithWhatWorldgenLeft()
        {
            // RebuildDerived runs a full solve on the generate path as well as the load path, so
            // that "the derived state is correct" has one definition. That is only harmless if the
            // solve reproduces what worldgen pass 10 settled — asserted here against the board the
            // scene actually loads, which is the one nobody had checked.
            ColonyRequest request = Request();
            request.Barren = true;
            request.Wooded = true;

            ColonyWorld colony = ColonyWorld.Build(request);
            byte[] settled = (byte[])colony.Grid.Support.Clone();

            colony.RebuildDerived();

            Assert.That(colony.Grid.Support, Is.EqualTo(settled),
                "a second full solve moved the support field — generation and the solver disagree");
        }

        [Test]
        public void AWorldBuiltTheWayTheSceneBuildsItCanSaveItself()
        {
            // The point of the whole unit. A world carrying a chunk grid, a mirror and a clock
            // that starts at noon is what the play scene runs, and it must round-trip like any
            // other — the scene used to be the one build in the project that could not.
            ColonyRequest request = Request(11u);
            request.Scenario = ScenarioDef.Playtest();
            request.Chunks = new ChunkGrid(Size);
            request.StartTick = 12 * 2_500;
            request.Mirror = (grid, outcome) => new RecordingMirror();

            ColonyWorld colony = ColonyWorld.Build(request);
            Assert.That(colony.SaveComponents, Is.Not.Empty);

            colony.World.Tick(500);
            byte[] saved = colony.Save();

            ColonyRequest again = Request(11u);
            again.Scenario = ScenarioDef.Playtest();
            again.Chunks = new ChunkGrid(Size);
            again.StartTick = 12 * 2_500;
            ColonyWorld reloaded = ColonyWorld.Build(again);
            SaveHeader header = reloaded.Load(saved);

            Assert.That(header.Seed, Is.EqualTo(11u));
            Assert.That(reloaded.World.ComputeStateHash(), Is.EqualTo(colony.World.ComputeStateHash()),
                "the scene's own world did not survive a save and load");
        }
    }
}
