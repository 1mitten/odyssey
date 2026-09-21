#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What can stand in a cell, as <see cref="CellDetail"/> carries it, turned into icon keys
    /// and words. Parallel to <see cref="EdificeHandle"/> beside <see cref="TerrainLabels"/> and
    /// for the same reason; the words come from <see cref="Registry"/>.
    ///
    /// <para><b>Reuse before invention.</b> A wall the player raised and the wall on the Build
    /// palette are one thing, so the build tool's key names both. The trees are the one thing on
    /// the played board that had no name anywhere, and they are new keys because a tree is not a
    /// tool and not a commodity. The rest — the ruined city's windows, vault walls, utility
    /// taps — are blank: the city is not the map the scene loads, and an unnamed row is omitted
    /// from the pane rather than guessed at.</para>
    /// </summary>
    public static class EdificeLabels
    {
        /// <summary>
        /// Parallel to <see cref="EdificeHandle"/>.
        ///
        /// <para><b>Three entries say "stair" and all three are right.</b> 5 and 6 are the two
        /// halves of one of worldgen's stamped stairwells and 13 is the colony's own one-cell
        /// flight; they are different things to the mesher and to the graph, and the same thing to
        /// a player, who clicks a stair and is told it is a stair.</para>
        /// </summary>
        public static readonly string[] Keys =
        {
            "",
            "ui.arch.tool.wall", "ui.arch.tool.door", "", "ui.arch.tool.pillar",
            "ui.arch.tool.stair", "ui.arch.tool.stair", "ui.arch.tool.ladder", "", "",
            "ui.terrain.tree.conifer", "ui.terrain.tree.broadleaf",
            "ui.arch.tool.bed",
            "ui.arch.tool.stair",
        };

        public static string IconKey(int edifice) =>
            edifice > 0 && edifice < Keys.Length ? Keys[edifice] : string.Empty;

        /// <summary>The thing's name in the registry's own case, for the pane's title when the
        /// thing standing in the cell is what was clicked — a tree above all.</summary>
        public static string Title(int edifice)
        {
            string key = IconKey(edifice);
            return key.Length == 0 ? string.Empty : Registry.Label(key);
        }

        /// <summary>
        /// The thing's name, lower-cased, because it is read inside a sentence — "conifer · minable",
        /// not "Conifer · minable". The same rule <see cref="BuildLabels.Stuff"/> follows.
        /// </summary>
        public static string Label(int edifice)
        {
            string key = IconKey(edifice);
            return key.Length == 0 ? string.Empty : Registry.Label(key).ToLowerInvariant();
        }
    }
}
