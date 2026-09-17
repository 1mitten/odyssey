#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The committed state hashes: what these worlds came to last time somebody looked, written
    /// down so that the next change has to answer for moving them.
    ///
    /// <para><b>What this catches that the existing determinism tests do not.</b>
    /// <c>HeadlessRunTests</c> builds the same world twice in one process and requires the two to
    /// agree, which proves the simulation is deterministic but says nothing about whether it still
    /// does what it did yesterday — two runs of a broken build agree perfectly. A committed number
    /// is the other half: it compares today's run against a value baked on another machine, in
    /// another process, weeks earlier. That is the only test here that can notice a change nobody
    /// intended.</para>
    ///
    /// <para><b>Two hashes per case, and the pair is the diagnosis.</b> A single number says
    /// "something moved" and leaves you bisecting. <see cref="Generated"/> is taken before the
    /// first tick and covers worldgen alone; <see cref="Simulated"/> is taken after
    /// <see cref="Ticks"/> and covers everything. If both moved, the generator changed and the
    /// simulation inherited it. If only the second moved, the board is identical and a system
    /// changed. That distinction is most of the work of reading a failure, and it costs one
    /// <c>ulong</c>.</para>
    ///
    /// <para><b>Re-baking is meant to be deliberate.</b> Run with <c>ODYSSEY_REGOLDEN=1</c> and
    /// the tests print replacement values instead of asserting; paste them in and say in the
    /// commit message what you changed and why the numbers moved. A golden updated without that
    /// sentence is a golden that has stopped being a test.</para>
    /// </summary>
    public static class Golden
    {
        /// <summary>One world, pinned: how to build it, how long to run it, and what it came to.</summary>
        public sealed class Case
        {
            public string Name = string.Empty;
            public GridSize Size;
            public uint Seed;
            public int Ticks;
            public MapType Map = MapType.Natural;

            /// <summary>Trees, streams and ore, as the played board has. False is the bare board.</summary>
            public bool Wooded;

            /// <summary>
            /// The hash of the whole world <b>before the first tick</b>: the generated board and
            /// the colony placed on it, which is what <c>GoldenMasterTests.FullHash</c> covers.
            ///
            /// <para><b>Not worldgen alone, despite the name</b>, and that matters when it moves.
            /// It folds in <c>SimWorld.ComputeStateHash</c>, so anything hashed by any component —
            /// a pawn, its skills, the designation grid, the construction grid — moves this number
            /// without a single generator pass having changed. Measured 2026-09-17: the build
            /// pipeline moved all three cases here while the grid hash alone stayed byte-identical
            /// to main on all three boards.</para>
            ///
            /// <para>The pair is still a diagnosis, one step weaker than the class comment claims:
            /// if only <see cref="Simulated"/> moved, nothing about the starting world changed and a
            /// system did. If this one moved, compare the grid hash by hand before concluding the
            /// generator changed.</para>
            /// </summary>
            public ulong Generated;

            /// <summary>The hash after <see cref="Ticks"/> ticks.</summary>
            public ulong Simulated;

            public ColonyWorld Build() =>
                ColonyWorld.Build(Size, Seed, ScenarioDef.Bare(), mapType: Map, wooded: Wooded);

            public override string ToString() => Name;
        }

        /// <summary>
        /// The one that runs on every save. Small and short on purpose: the fast tier is a thing
        /// people run while working, and a gate nobody waits for is a gate nobody runs.
        /// </summary>
        public static readonly Case Meadow = new Case
        {
            Name = "meadow 60x60x16 barren, seed 4242, 5,000 ticks",
            Size = new GridSize(60, 60, 16),
            Seed = 4242u,
            Ticks = 5_000,
            Map = MapType.Natural,
            Wooded = false,
            Generated = 9018047728549680434UL,
            Simulated = 11445817444308316440UL,
        };

        /// <summary>
        /// The board the game actually loads — 120 x 120 x 16, wooded — at the size and shape
        /// <c>OdysseyBootstrap</c> builds. The bare board above is the clean baseline; this is the
        /// one whose regression a player would actually meet.
        /// </summary>
        public static readonly Case PlayedBoard = new Case
        {
            Name = "wooded meadow 120x120x16, seed 1, 10,000 ticks",
            Size = new GridSize(120, 120, 16),
            Seed = 1u,
            Ticks = 10_000,
            Map = MapType.Natural,
            Wooded = true,
            Generated = 10966701061063906442UL,
            Simulated = 16650643146507109033UL,
        };

        /// <summary>
        /// The ruined city, which is still generated and still tested even though the scene does
        /// not load it. Its passes are the ones nothing else exercises.
        /// </summary>
        public static readonly Case City = new Case
        {
            Name = "ruined city 60x60x5, seed 9, 10,000 ticks",
            Size = new GridSize(60, 60, 5),
            Seed = 9u,
            Ticks = 10_000,
            Map = MapType.RuinedCity,
            Wooded = false,
            Generated = 14684518721141350564UL,
            Simulated = 8796170603677095916UL,
        };
    }
}
