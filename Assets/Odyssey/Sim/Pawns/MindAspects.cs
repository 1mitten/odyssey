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
    }
}
