#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The five tiers a quality-bearing thing finishes at, as words — parallel to
    /// <see cref="QualityHandle"/> exactly as <see cref="BuildLabels"/> is parallel to
    /// <see cref="BuildingHandle"/>, and for the same reason: the table lives in
    /// <c>Odyssey.Sim.Construction</c>, which this assembly cannot see (ADR 0003), and a tier a
    /// player can read is a name the registry owns.
    /// </summary>
    public static class QualityLabels
    {
        /// <summary>
        /// Parallel to <see cref="QualityHandle"/>: None, Poor, Normal, Decent, Uber, Epic. The
        /// blank first row is tier 0, "takes no quality", which no bed ever shows.
        /// </summary>
        public static readonly string[] Keys =
        {
            "", "ui.quality.poor", "ui.quality.normal", "ui.quality.decent", "ui.quality.uber",
            "ui.quality.epic",
        };

        public static string Key(int tier) =>
            (uint)tier < (uint)Keys.Length ? Keys[tier] : string.Empty;

        /// <summary>
        /// The tier's name, lower-cased, because it reads inside a sentence — "quality: decent" —
        /// the same bargain <see cref="BuildLabels.Stuff"/> makes for a material.
        /// </summary>
        public static string Label(int tier)
        {
            string key = Key(tier);
            return key.Length == 0 ? string.Empty : Registry.Label(key).ToLowerInvariant();
        }
    }
}
