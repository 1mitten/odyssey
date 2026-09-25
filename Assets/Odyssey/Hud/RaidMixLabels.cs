#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// What each raid mix is called (design 50 §8), by registry key, parallel to the simulation's
    /// <c>IncidentContent.MixOrder</c> exactly as <see cref="IncidentLabels"/> is parallel to its
    /// incident order: appended, never inserted. The debug dropdown lists these, and a raid's Events
    /// row names its mix by them. <c>RaidTests</c> holds the two lists to one order.
    /// </summary>
    public static class RaidMixLabels
    {
        public static readonly string[] Keys =
        {
            "ui.raid.mix.bandits",
            "ui.raid.mix.gunmen",
            "ui.raid.mix.mixed",
        };

        /// <summary>The mix the debug menu starts on: Mixed, the raid Def's own.</summary>
        public const int Default = 2;

        public static string IconKey(int mix) =>
            mix >= 0 && mix < Keys.Length ? Keys[mix] : IncidentLabels.Unknown;

        public static string Label(int mix) => Registry.Label(IconKey(mix));
    }
}
