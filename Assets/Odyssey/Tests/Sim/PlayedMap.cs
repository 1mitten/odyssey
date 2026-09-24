#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The map the game actually generates, for the arms that measure what a board costs.
    ///
    /// <para><b>Why this exists, and it is not a convenience.</b> On 2026-09-21 every per-board
    /// measurement in this assembly was taken on a configuration the game does not build. They
    /// reached for <c>NaturalMapGenDef.For(size)</c> — which is exactly
    /// <c>MapGenerator.DefaultDef</c>, and is the *unmodified* def. The played scene sets
    /// <c>barrenMap: 1, woodedMap: 1</c>, so <c>ColonyWorld.Build</c> applies
    /// <see cref="NaturalMapGenDef.MakeWooded"/> on top of it
    /// (<c>ColonyWorld.cs</c>, the <c>request.Barren</c> branch). The two differ in what they put
    /// on the board: the owner's own play log read <c>patches 0, trees 1598</c> where the arms
    /// were reporting <c>patches 2210, trees 1222</c> for the same board.</para>
    ///
    /// <para>That matters beyond tree counts. Region counts — and therefore the navigation
    /// rebuild, which is the one cost that grows with the board — depend on what stands on the
    /// surface and how it is broken up. A measurement of the wrong map is not a conservative
    /// measurement of the right one; it is a different number.</para>
    ///
    /// <para><b>It is not a copy — it calls the game's own chooser.</b>
    /// <c>ColonyWorld.DefFor</c> was extracted out of <c>ColonyWorld.Build</c> on the same day for
    /// this reason: the choice had two owners that silently disagreed, which is the shape
    /// <c>docs/bug-patterns.md</c> keeps meeting. With one owner the mirror cannot drift, and
    /// <c>PlayedMapTests</c> holds the flags (<c>barren</c>, <c>wooded</c>) to what the played
    /// scene carries.</para>
    /// </summary>
    public static class PlayedMap
    {
        /// <summary>The generator definition a new colony is built from.</summary>
        public static NaturalMapGenDef Def(GridSize size)
        {
            // The game's own chooser, not a copy of it. That is the whole point of this class.
            return (NaturalMapGenDef)Odyssey.Sim.Pawns.ColonyWorld.DefFor(
                MapType.Natural, size, barren: true, wooded: true);
        }

        /// <summary>A generated board, as the game generates it.</summary>
        public static CellGrid Generate(GridSize size, uint seed, out NaturalMapResult result)
        {
            var grid = new CellGrid(size);
            result = NaturalMapGenerator.Generate(grid, seed, Def(size));
            return grid;
        }

        /// <inheritdoc cref="Generate(GridSize, uint, out NaturalMapResult)"/>
        public static CellGrid Generate(GridSize size, uint seed) => Generate(size, seed, out _);
    }
}
