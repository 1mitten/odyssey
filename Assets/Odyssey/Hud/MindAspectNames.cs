#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The names a colonist's state of mind is published under (design 43 §4d), spelled on this
    /// side of the seam because <c>Odyssey.Hud</c> cannot reference the simulation. The simulation
    /// mints the same strings in <c>MindAspects</c>, and <c>MindAspectTests</c> (Sim) holds the two
    /// to each other.
    /// </summary>
    public static class MindAspectNames
    {
        /// <summary>A <see cref="MoodBand"/>: where her mood stands against her own lines.</summary>
        public const string Band = "odyssey.pawn.mood.band";

        /// <summary>The target her mood drifts toward, 0..1000.</summary>
        public const string Target = "odyssey.pawn.mood.target";

        /// <summary>Her three break lines, after anything that moves them.</summary>
        public const string Minor = "odyssey.pawn.mood.minor";
        public const string Major = "odyssey.pawn.mood.major";
        public const string Extreme = "odyssey.pawn.mood.extreme";

        public static readonly AspectKey BandKey = AspectKey.Of(Band);
        public static readonly AspectKey TargetKey = AspectKey.Of(Target);
        public static readonly AspectKey MinorKey = AspectKey.Of(Minor);
        public static readonly AspectKey MajorKey = AspectKey.Of(Major);
        public static readonly AspectKey ExtremeKey = AspectKey.Of(Extreme);
    }
}
