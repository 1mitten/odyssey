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

        /// <summary>Trait slot keys (design 43 §4d): the handle, then each effect.</summary>
        public static readonly AspectKey[] TraitKey = Slots(string.Empty);
        public static readonly AspectKey[] TraitMoodKey = Slots(".mood");
        public static readonly AspectKey[] TraitNerveKey = Slots(".nerve");
        public static readonly AspectKey[] TraitLearnKey = Slots(".learn");
        public static readonly AspectKey[] TraitWorkKey = Slots(".work");
        public static readonly AspectKey[] TraitCannotKey = Slots(".cannot");

        static AspectKey[] Slots(string suffix)
        {
            var keys = new AspectKey[TraitHandle.MaxPerPawn];
            for (int s = 0; s < keys.Length; s++) keys[s] = AspectKey.Of("odyssey.pawn.trait." + s + suffix);
            return keys;
        }

        /// <summary>The <see cref="BreakHandle"/> she is in, while she is in one.</summary>
        public const string Break = "odyssey.pawn.break";
        public static readonly AspectKey BreakKey = AspectKey.Of(Break);

        public static readonly AspectKey BandKey = AspectKey.Of(Band);
        public static readonly AspectKey TargetKey = AspectKey.Of(Target);
    }
}
