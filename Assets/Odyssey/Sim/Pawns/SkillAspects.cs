#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The names a colonist's skills are published under, and the keys minted from them.
    ///
    /// <para><b>This is the whole of the seam.</b> Nothing in <c>Sim.Contracts</c> knows that
    /// skills exist: the interface asks for <c>odyssey.pawn.skill.mining.level</c> by name and
    /// gets a number, which is exactly what <see cref="PawnAspect"/> was added for. A
    /// <c>SkillView</c> struct and a <c>SkillHandle</c> table were written here first and then
    /// removed on merging, because they were a second mechanism for the job this one already
    /// does — and the shared file they would have lived in is the file aspects exist to stop
    /// people editing.</para>
    ///
    /// <para><b>Four names per skill rather than one packed number.</b> A level, a passion, an
    /// experience and a progress are four things the interface shows separately and four things
    /// that change on different occasions; packing them into one int would save rows a pawn and
    /// cost the reader a decode it could get wrong. The published set is twenty rows a colonist,
    /// which on a colony of fifty is a thousand rows into a buffer that is reused.</para>
    ///
    /// <para><b>Minted once.</b> <see cref="AspectKey.Of"/> walks the string, and the publish loop
    /// runs every tick for every colonist, so the keys are static and the loop only indexes them.
    /// </para>
    /// </summary>
    public static class SkillAspects
    {
        /// <summary>Everything this project publishes about a pawn shares this prefix.</summary>
        public const string Prefix = "odyssey.pawn.skill.";

        /// <summary>The full name of one value of one skill, as both sides spell it.</summary>
        public static string Name(string skill, string value) => Prefix + skill + "." + value;

        public static readonly AspectKey[] Level = Mint("level");
        public static readonly AspectKey[] Passion = Mint("passion");
        public static readonly AspectKey[] Experience = Mint("experience");

        /// <summary>
        /// How far this skill stands towards its next level, per mille (SK2) — the number the
        /// inspect pane's bar is drawn from.
        ///
        /// <para><b>A fourth row rather than arithmetic on the other side.</b> The experience is
        /// already published, but the ladder that says how much of it buys a level is content in
        /// <c>SkillDef.experienceToAdvance</c>, and a copy of it in the interface would be a
        /// second source of truth for a tunable number. Deriving it here costs one integer a skill
        /// and keeps the table where a mod can override it.</para>
        /// </summary>
        public static readonly AspectKey[] Progress = Mint("progress");

        /// <summary>
        /// The name a colonist's own roll seed goes out under (U40). Not a skill and so not under
        /// <see cref="Prefix"/>, but minted the same way and travelling through the same channel —
        /// which is the argument for the aspect seam rather than a field on <c>PawnView</c>: a
        /// feature mints the name it needs and <c>Sim.Contracts</c> never hears about it.
        /// </summary>
        public static readonly AspectKey RollSeed = AspectKey.Of("odyssey.pawn.rollseed");

        static AspectKey[] Mint(string value)
        {
            var keys = new AspectKey[SkillIndex.Count];
            for (int s = 0; s < keys.Length; s++)
                keys[s] = AspectKey.Of(Name(SkillIndex.Names[s], value));
            return keys;
        }
    }
}
