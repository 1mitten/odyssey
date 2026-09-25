#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The one table from a <see cref="WeatherKind"/> to the word the interface shows for it, read
    /// through <see cref="Registry.Label"/> so the clock and the wiki say the same thing
    /// (design 43 §5, the clock panel's weather word). Unity-free, so the fast tier holds it.
    /// </summary>
    public static class WeatherLabels
    {
        public const string ClearKey = "ui.weather.clear";
        public const string CloudyKey = "ui.weather.cloudy";
        public const string RainKey = "ui.weather.rain";
        public const string StormKey = "ui.weather.storm";

        public static readonly string[] IconKeys = { ClearKey, CloudyKey, RainKey, StormKey };

        public static string KeyOf(WeatherKind kind) => kind switch
        {
            WeatherKind.Cloudy => CloudyKey,
            WeatherKind.Rain => RainKey,
            WeatherKind.Storm => StormKey,
            _ => ClearKey,
        };

        /// <summary>
        /// The word for the sky this frame, or empty when there is no sky to name — a snapshot from
        /// a world without the weather system publishes <see cref="WeatherView.None"/>.
        /// </summary>
        public static string Describe(in WeatherView view) =>
            view.IntensityPerMille <= 0 ? string.Empty : Registry.Label(KeyOf(view.Kind));
    }
}
