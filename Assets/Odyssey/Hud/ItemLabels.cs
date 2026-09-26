#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Item def indices, as <see cref="ThingView"/> carries them, turned into icon keys and
    /// words. Parallel to <see cref="ItemHandle"/> in exactly the way <see cref="JobLabels"/> is
    /// parallel to the job indices, and for the same reason: the real table lives where this
    /// assembly cannot see it (ADR 0003), and presentation is where an index becomes a name. The
    /// words themselves come from <see cref="Registry"/> — this class knows which key a thing is,
    /// never what the key is called.
    ///
    /// <para><b>Salvage is scrap.</b> The commodity's own name in the wiki is Scrap ("mixed
    /// unsorted salvage"); the pane used to hard-code "Salvage" for every pile of it before this
    /// table existed, and the ledger had already settled the word the other way.</para>
    /// </summary>
    public static class ItemLabels
    {
        /// <summary>
        /// Parallel to <see cref="ItemHandle"/>: Meal, Scrap, Wood, Stone, Iron ore, Coal.
        ///
        /// <para><b>One entry short is not a compile error, it is a mislabelled pile</b> — the
        /// bounds check below turns an unlisted def into scrap, which for a pile of anything is
        /// a lie. <c>RegistryTests</c> holds the length to <c>ItemHandle.Count</c> so the next
        /// commodity cannot arrive quietly.</para>
        /// </summary>
        public static readonly string[] Keys =
        {
            // Handle 0 is the ration pack, and is named as one since the kitchen (design 48 §3):
            // it had borrowed ui.res.meal, which the cooked meal below takes back.
            "ui.res.rations", "ui.res.scrap", "ui.res.wood", "ui.res.stone", "ui.res.ironore", "ui.res.coal",
            "ui.res.carrots",
            // The four melee weapons (design 33 §1, C3), in ItemHandle order 7 to 10.
            "ui.item.bat", "ui.item.crowbar", "ui.item.machete", "ui.item.arcblade",
            // Medical supplies (design 37), handle 11. The key is the old "medkit" one, relabelled.
            "ui.res.medkit",
            // The wild foods (design 45 §6), ItemHandle 12 and 13, after medical supplies.
            "ui.res.berries", "ui.res.mushrooms",
            // The kitchen (design 48 §4), handles 14 to 16, after the wild foods: the meal, the vegetable meal, the burnt one.
            "ui.res.meal", "ui.res.meal.veg", "ui.res.meal.burnt",
            // The pistol (design 47), ItemHandle 17, after the kitchen's meals.
            "ui.item.pistol",
            // Deep mining's finds (design 62 §5c), ItemHandle 18 to 21, after the pistol.
            "ui.res.copperore", "ui.res.goldore", "ui.res.gems", "ui.res.emberquartz",
            // The smelter's bars (design 62 §9), ItemHandle 22 and 23.
            "ui.res.ironbar", "ui.res.copperbar",
        };

        public static string IconKey(int def) =>
            def >= 0 && def < Keys.Length ? Keys[def] : "ui.res.scrap";

        public static string Label(int def) => Registry.Label(IconKey(def));

        /// <summary>
        /// A thing's name with how well it was made after it — "Pistol (Decent)" — or the bare name for
        /// a thing with no tier (design 47 §11). Both words are the registry's; only the brackets are
        /// the layout's.
        /// </summary>
        public static string Label(int def, int quality)
        {
            string key = QualityLabels.Key(quality);
            return key.Length == 0 ? Label(def) : Label(def) + " (" + Registry.Label(key) + ")";
        }
    }
}
