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
            "ui.res.meal", "ui.res.scrap", "ui.res.wood", "ui.res.stone", "ui.res.ironore", "ui.res.coal",
            "ui.res.carrots",
            // The four melee weapons (design 33 §1, C3), in ItemHandle order 7 to 10.
            "ui.item.bat", "ui.item.crowbar", "ui.item.machete", "ui.item.arcblade",
        };

        public static string IconKey(int def) =>
            def >= 0 && def < Keys.Length ? Keys[def] : "ui.res.scrap";

        public static string Label(int def) => Registry.Label(IconKey(def));
    }
}
