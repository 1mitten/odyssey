#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The names a colonist's work priorities are published under.
    ///
    /// <para>Built the same way as <see cref="SkillAspects"/> and for the same reason: nothing in
    /// <c>Sim.Contracts</c> has to learn that work types exist, and the Work tab asks for
    /// <c>odyssey.pawn.work.mining.priority</c> by name. Two names a work type rather than one
    /// packed number, because a priority and a capability change on entirely different occasions —
    /// one when the player clicks, one when a colonist is injured — and the grid draws them as two
    /// different things.</para>
    ///
    /// <para><b>Capability is published although nothing can yet answer it with a no.</b> There are
    /// no traits and no health model, so every colonist is capable of everything the simulation
    /// runs. The channel exists anyway: adding it later means finding every reader that assumed a
    /// digit, and the interface has already been written to treat an incapable cell as inert at the
    /// model rather than as a style. It costs four rows a colonist.</para>
    ///
    /// <para><b>Minted once</b>, because the publish loop runs every tick for every colonist and
    /// <see cref="AspectKey.Of"/> walks the string.</para>
    /// </summary>
    public static class WorkAspects
    {
        /// <summary>Everything published about a pawn's work shares this prefix.</summary>
        public const string Prefix = "odyssey.pawn.work.";

        /// <summary>The full name of one value of one work type, as both sides spell it.</summary>
        public static string Name(string work, string value) => Prefix + work + "." + value;

        /// <summary>This colonist's priority for the work type, 0 (never) to 4 (lowest).</summary>
        public static readonly AspectKey[] Priority = Mint("priority");

        /// <summary>1 when this colonist can do the work at all, 0 when they cannot.</summary>
        public static readonly AspectKey[] Capable = Mint("capable");

        static AspectKey[] Mint(string value)
        {
            var keys = new AspectKey[WorkTypeIndex.Count];
            for (int w = 0; w < keys.Length; w++)
                keys[w] = AspectKey.Of(Name(WorkTypeIndex.Names[w], value));
            return keys;
        }
    }
}
