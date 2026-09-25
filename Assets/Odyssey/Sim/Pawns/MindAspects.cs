#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The names a colonist's state of mind is published under (design 43 §4d): the band, the
    /// target and her own three break lines. Built the way <see cref="SkillAspects"/> is, and for
    /// its reason: the interface asks by name and <c>Sim.Contracts</c> learns nothing new.
    ///
    /// <para><b>Reports, not state.</b> Every row is derived each publish from saved, hashed state,
    /// so none is saved or hashed itself. <c>Odyssey.Hud</c> spells the same strings in
    /// <c>MindAspectNames</c>, and <c>MindAspectTests</c> holds the two to each other.</para>
    /// </summary>
    public static class MindAspects
    {
        /// <summary>A <see cref="MoodBand"/>.</summary>
        public static readonly AspectKey Band = AspectKey.Of("odyssey.pawn.mood.band");

        /// <summary>The target the mood drifts toward, 0..1000.</summary>
        public static readonly AspectKey Target = AspectKey.Of("odyssey.pawn.mood.target");

        /// <summary>Her minor, major and extreme lines, after anything that moves them.</summary>
        public static readonly AspectKey Minor = AspectKey.Of("odyssey.pawn.mood.minor");
        public static readonly AspectKey Major = AspectKey.Of("odyssey.pawn.mood.major");
        public static readonly AspectKey Extreme = AspectKey.Of("odyssey.pawn.mood.extreme");

        /// <summary>The base every target starts from, so the Thoughts tab can show its sum.</summary>
        public static readonly AspectKey Base = AspectKey.Of("odyssey.pawn.mood.base");

        /// <summary>
        /// A need band's situational offset, per need (<c>odyssey.pawn.mood.need.food</c>),
        /// published only while it is not nought.
        /// </summary>
        public static readonly AspectKey[] Need =
        {
            AspectKey.Of("odyssey.pawn.mood.need.food"),
            AspectKey.Of("odyssey.pawn.mood.need.rest"),
            AspectKey.Of("odyssey.pawn.mood.need.joy"),
        };

        /// <summary>The temperature's situational offset, while it is not nought.</summary>
        public static readonly AspectKey Temperature = AspectKey.Of("odyssey.pawn.mood.temperature");

        /// <summary>A memory's worth, stack included, per <see cref="ThoughtHandle"/>, while held.</summary>
        public static readonly AspectKey[] Thought = MintThought(string.Empty);

        /// <summary>Ticks until the soonest copy of the memory lapses.</summary>
        public static readonly AspectKey[] ThoughtLeft = MintThought(".left");

        /// <summary>How many copies of the memory are live.</summary>
        public static readonly AspectKey[] ThoughtCount = MintThought(".count");

        /// <summary>The name one value of one thought is published under, as both sides spell it.</summary>
        public static string ThoughtName(int thought, string suffix) =>
            "odyssey.pawn.thought." + ThoughtHandle.Names[thought] + suffix;

        static AspectKey[] MintThought(string suffix)
        {
            var keys = new AspectKey[ThoughtHandle.Count];
            for (int t = 0; t < keys.Length; t++) keys[t] = AspectKey.Of(ThoughtName(t, suffix));
            return keys;
        }
    }
}
