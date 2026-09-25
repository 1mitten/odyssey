#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Buildable things and materials, as the snapshot carries them, turned into icon keys and
    /// words.
    ///
    /// <para>Two tables parallel to <see cref="BuildingHandle"/> and <see cref="StuffHandle"/>, in
    /// exactly the way <see cref="JobLabels"/> is parallel to the job indices, and for the same
    /// reason: the real tables live in <c>Odyssey.Sim.Construction</c>, which this assembly cannot
    /// see (ADR 0003), and presentation is where an index becomes a name. The words themselves come
    /// from <see cref="Registry"/> — this class knows which key a thing is, never what the key is
    /// called.</para>
    ///
    /// <para>The observation that would overturn it is the buildable set outgrowing a hand-written
    /// table, which it will: the catalogue has ten categories and eighty tools in it. At that point
    /// the key belongs on the published view beside the handle, or the whole set comes out of the
    /// Defs the way <c>icon-keys.csv</c> is promised to. Two entries do not justify either yet.</para>
    /// </summary>
    public static class BuildLabels
    {
        /// <summary>Parallel to <see cref="BuildingHandle"/>: None, Wall, Floor, DeckPlate, Ladder, Bed, Door, Shelf.</summary>
        public static readonly string[] BuildingKeys =
        {
            "", "ui.arch.tool.wall", "ui.arch.tool.roof", "ui.arch.tool.deckplate",
            "ui.arch.tool.ladder", "ui.arch.tool.bed", "ui.arch.tool.door",
            "ui.arch.tool.shelf", "ui.arch.tool.campfire",
            "ui.arch.tool.conduit", "ui.arch.tool.generator", "ui.arch.tool.heater",
            // The kitchen (design 48), BuildingHandle 12.
            "ui.arch.tool.galley",
        };

        /// <summary>
        /// Parallel to <see cref="StuffHandle"/>: None, Concrete, Steel, Composite, Wood, Stone.
        ///
        /// <para>The first four are blank on purpose. They are what the ruined city is <i>made
        /// of</i>, not what a colony builds with — <c>ConstructionContent.IsBuildable</c> keeps them
        /// off the menu, so none can ever reach a published site and none needs a name here. Two of
        /// them do not even have registry keys: concrete was struck out with alloy and water
        /// (owner, 2026-09-16) and steel never had one. Naming them would mean inventing content to
        /// describe something the player cannot ask for.</para>
        /// </summary>
        public static readonly string[] StuffKeys =
        {
            "", "", "", "", "ui.res.wood", "ui.res.stone",
        };

        /// <summary>
        /// Parallel to <see cref="PlantHandle"/>: Carrot, the first crop and for now the only one.
        /// The picker walks <c>PaletteTools.Plants</c>; a second crop is one key here.
        /// </summary>
        public static readonly string[] PlantKeys =
        {
            "ui.terrain.carrot",
        };

        public static string BuildingKey(int building) =>
            building > 0 && building < BuildingKeys.Length ? BuildingKeys[building] : string.Empty;

        public static string StuffKey(int stuff) =>
            stuff > 0 && stuff < StuffKeys.Length ? StuffKeys[stuff] : string.Empty;

        public static string PlantKey(int plant) =>
            plant >= 0 && plant < PlantKeys.Length ? PlantKeys[plant] : string.Empty;

        /// <summary>The thing's name, or an empty string where there is nothing to name.</summary>
        public static string Building(int building)
        {
            string key = BuildingKey(building);
            return key.Length == 0 ? string.Empty : Registry.Label(key);
        }

        /// <summary>
        /// The material's name, lower-cased, because it is read inside a sentence — "wall of wood",
        /// not "wall of Wood". The registry's own label is title case, which is right for a chip
        /// and wrong for a subtitle.
        /// </summary>
        public static string Stuff(int stuff)
        {
            string key = StuffKey(stuff);
            return key.Length == 0 ? string.Empty : Registry.Label(key).ToLowerInvariant();
        }
    }
}
