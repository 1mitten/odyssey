#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The names a colonist's day is published under: one per hour.
    ///
    /// <para>Built the same way as <see cref="SkillAspects"/> and <see cref="WorkAspects"/>, and
    /// the same argument settles the shape. Twenty-four rows a colonist is the most this mechanism
    /// has been asked for — nine for skills, eight for work, twenty-four for the day — and packing
    /// the day into three ints would save twenty-one of them and cost the reader a decode it could
    /// get wrong. <c>SkillAspects</c> already refused that trade for three values; this is the same
    /// trade at eight times the size, and the answer does not change because the number did.</para>
    ///
    /// <para><b>The hour is in the name, zero-padded</b>, so the keys sort the way the day runs and
    /// a log line naming <c>odyssey.pawn.schedule.h07</c> is readable without a lookup.</para>
    ///
    /// <para>Minted once: the publish loop runs every tick for every colonist, and
    /// <see cref="AspectKey.Of"/> walks the string.</para>
    /// </summary>
    public static class ScheduleAspects
    {
        /// <summary>Everything published about a pawn's day shares this prefix.</summary>
        public const string Prefix = "odyssey.pawn.schedule.";

        /// <summary>The full name of one hour, as both sides spell it.</summary>
        public static string Name(int hour) => Prefix + "h" + hour.ToString("00");

        /// <summary>One key per hour of the day, in the order the day runs.</summary>
        public static readonly AspectKey[] Hour = Mint();

        static AspectKey[] Mint()
        {
            var keys = new AspectKey[ScheduleHandle.Hours];
            for (int h = 0; h < keys.Length; h++) keys[h] = AspectKey.Of(Name(h));
            return keys;
        }
    }
}
