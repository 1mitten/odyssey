#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Terrain kinds, as <see cref="CellDetail"/> carries them, turned into icon keys and words.
    /// Parallel to <see cref="TerrainHandle"/> the way <see cref="JobLabels"/> is parallel to the
    /// job indices, and for the same reason: the terrain table lives in the simulation, this
    /// assembly cannot see it (ADR 0003), and the pane is where an index becomes a name. The
    /// words come from <see cref="Registry"/>.
    ///
    /// <para><b>A blank is deliberate and means "not the played board's to say".</b> The city's
    /// finished surfaces — pavement, cracked pavement, soil, engineered fill, the buried seam —
    /// have no name here because the ruined city is not the map the scene loads, and inventing
    /// names for tiles a player cannot click is inventing content. The meadow's whole surface
    /// and everything under it is named, ore included: ore terrain carries the resource's own
    /// name, because "Iron ore" on a rock face is exactly what the player needs to read there.
    /// Air is blank for a different reason — a cell of air that was worth clicking was clicked
    /// for its floor, its site or its tree, and the pane names that instead.</para>
    /// </summary>
    public static class TerrainLabels
    {
        /// <summary>
        /// Parallel to <see cref="TerrainHandle"/>. <c>RegistryTests</c> holds the length to
        /// <c>TerrainHandle.Count</c>, so a terrain added to the simulation without a row here
        /// fails the fast tier rather than reading as bare ground.
        /// </summary>
        public static readonly string[] Keys =
        {
            "",
            "", "", "ui.res.rubble", "", "ui.terrain.gravel", "", "ui.terrain.rock", "", "ui.res.scrap",
            "ui.terrain.grass", "ui.terrain.bareearth", "ui.terrain.packedgravel", "ui.terrain.sand",
            "ui.terrain.subsoil", "ui.terrain.bedrock", "ui.res.ironore", "ui.res.coal",
            "ui.terrain.water.shallow", "ui.terrain.water.deep", "ui.terrain.marsh",
        };

        public static string IconKey(int terrain) =>
            terrain >= 0 && terrain < Keys.Length ? Keys[terrain] : string.Empty;

        /// <summary>
        /// The terrain's name, or an empty string where there is nothing to name. Used verbatim
        /// as the pane's title, the way <see cref="BuildLabels.Building"/> names a site.
        /// </summary>
        public static string Label(int terrain)
        {
            string key = IconKey(terrain);
            return key.Length == 0 ? string.Empty : Registry.Label(key);
        }
    }
}
