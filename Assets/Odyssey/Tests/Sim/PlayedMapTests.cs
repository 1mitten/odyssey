#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <see cref="PlayedMap"/> really is the map the colony builds.
    ///
    /// <para><b>This is the test that should have existed before any board was measured.</b>
    /// Without it, every per-board figure in <c>docs/design/28-map-size.md</c> was taken on the
    /// unmodified default def while the game applies <c>MakeWooded()</c> on top — and nothing
    /// anywhere tied the two together, so the arms reported confidently for two days.</para>
    /// </summary>
    public class PlayedMapTests
    {
        static ulong HashOf(CellGrid grid)
        {
            var hash = new StateHash();
            grid.ContributeTo(ref hash);
            return hash.Value;
        }

        /// <summary>
        /// The helper asks for the board the played scene asks for: <c>barren</c> and
        /// <c>wooded</c>, which is <c>barrenMap: 1, woodedMap: 1</c> in
        /// <c>Assets/Scenes/Play.unity</c> and the defaults on <c>OdysseyBootstrap</c>.
        ///
        /// <para>Checked against an observable property of <c>MakeWooded</c> rather than against
        /// itself: it zeroes <c>barePatchThreshold</c>, so a wooded board carries trees and no
        /// bare earth, gravel or sand. A tautology — comparing the helper with a second call to
        /// the same chooser — would pass for ever and prove nothing.</para>
        /// </summary>
        [Test]
        public void ThisIsTheMapTheColonyBuilds(
            [Values(48u, 4242u)] uint seed)
        {
            var size = new GridSize(60, 60, 16);

            PlayedMap.Generate(size, seed, out NaturalMapResult played);
            int patches = played.Report.BareEarthCells + played.Report.GravelCells
                          + played.Report.SandCells;

            Assert.That(played.Report.Trees, Is.GreaterThan(0),
                "the played map has no trees, so MakeWooded is not being applied and every " +
                "per-board measurement is describing a board the game does not build");
            Assert.That(patches, Is.Zero,
                $"the played map has {patches} patch cells; MakeWooded zeroes barePatchThreshold, " +
                "so this is the unmodified default def again");
        }

        /// <summary>
        /// And it is genuinely a different board from the default def, which is what the arms used
        /// to take. <b>The negative control:</b> without this, the helper could quietly become a
        /// synonym for <c>NaturalMapGenDef.For</c> and the test above would still pass while
        /// proving nothing.
        /// </summary>
        [Test]
        public void TheDefaultDefIsNotThePlayedMap()
        {
            var size = new GridSize(60, 60, 16);
            const uint Seed = 4242u;

            var plain = new CellGrid(size);
            NaturalMapGenerator.Generate(plain, Seed, NaturalMapGenDef.For(size));

            Assert.That(HashOf(PlayedMap.Generate(size, Seed)), Is.Not.EqualTo(HashOf(plain)),
                "the played map and the unmodified default def now generate the same board, so " +
                "the mirror above is no longer testing anything. If MakeWooded has become a " +
                "no-op that is the finding; if it has not, PlayedMap has stopped applying it");
        }
    }
}
