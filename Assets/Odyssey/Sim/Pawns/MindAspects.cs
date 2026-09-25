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

        /// <summary>The <see cref="BreakHandle"/> she is in, while she is in one (design 43 §5c).</summary>
        public static readonly AspectKey Break = AspectKey.Of("odyssey.pawn.break");

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

        /// <summary>
        /// Her traits by slot (design 43 §4d): <c>odyssey.pawn.trait.0</c> to <c>.2</c> carry the
        /// <see cref="TraitHandle"/>, and the rest the effects, each only while it is not the
        /// default, so the interface derives no number the simulation knows.
        /// </summary>
        public static readonly AspectKey[] Trait = MintTrait(string.Empty);
        public static readonly AspectKey[] TraitMood = MintTrait(".mood");
        public static readonly AspectKey[] TraitNerve = MintTrait(".nerve");
        public static readonly AspectKey[] TraitLearn = MintTrait(".learn");
        public static readonly AspectKey[] TraitWork = MintTrait(".work");

        /// <summary>The work types a slot's trait forbids, a bit per <c>WorkHandle</c>.</summary>
        public static readonly AspectKey[] TraitCannot = MintTrait(".cannot");

        /// <summary>The name one value of one trait slot is published under, as both sides spell it.</summary>
        public static string TraitName(int slot, string suffix) => "odyssey.pawn.trait." + slot + suffix;

        static AspectKey[] MintTrait(string suffix)
        {
            var keys = new AspectKey[TraitHandle.MaxPerPawn];
            for (int s = 0; s < keys.Length; s++) keys[s] = AspectKey.Of(TraitName(s, suffix));
            return keys;
        }

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
