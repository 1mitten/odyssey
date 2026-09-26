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
        /// Parallel to <see cref="EdificeHandle"/>. A stair is one edifice in two cells, so both
        /// halves carry the stair's key.
        /// </summary>
        public static readonly string[] Keys =
        {
            "",
            "ui.arch.tool.wall", "ui.arch.tool.door", "", "ui.arch.tool.pillar",
            "ui.arch.tool.stair", "ui.arch.tool.stair", "ui.arch.tool.ladder", "", "",
            "ui.terrain.tree.birch", "ui.terrain.tree.meadow",
            "ui.arch.tool.bed", "ui.arch.tool.shelf",
            "ui.arch.tool.campfire",
            "ui.arch.tool.generator", "ui.arch.tool.heater",
            // The wild things of design 45, after the buildings: two more trees, then the bushes.
            "ui.terrain.tree.fruit", "ui.terrain.tree.giant",
            "ui.terrain.bush", "ui.terrain.bush.berry", "ui.terrain.bush.picked",
            // The kitchen (design 48), edifice 22, after the wild things.
            "ui.arch.tool.galley",
            // Cover (design 53 §4), edifice 23.
            "ui.arch.tool.sandbag",
            // The built stair (design 63), edifice 24: one record, two cells, the city's own key.
            "ui.arch.tool.stair",
            // The smelter (design 62 §9), edifice 25.
            "ui.arch.tool.smelter",
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
