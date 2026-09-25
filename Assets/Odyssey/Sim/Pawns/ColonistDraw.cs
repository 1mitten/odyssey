#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Who a colonist would be, rolled from a seed with no world to put them in (U40).
    ///
    /// <para>This is what a select screen shows before anybody has pressed Start. It is possible at
    /// all because the two draws that decide a colonist —
    /// <see cref="Pawn.RollPassions"/> and <see cref="Pawn.RollStartingSkills"/> — depend on nothing
    /// but a seed and a pawn id: no grid, no tick, no colony.</para>
    ///
    /// <para><b>It rolls a real <see cref="Pawn"/> and calls the same two methods the colony calls.
    /// Not a copy of the arithmetic — the arithmetic itself.</b> This is `NavGraph.HopCost`'s lesson
    /// applied before it has cost anything: a number computed in two places agrees by coincidence
    /// until it does not, and the failure here would be the worst kind to find, a screen that
    /// promises a miner and hands over a cook with both halves individually correct and neither
    /// wrong enough to crash. <c>ColonistDrawTests</c> asserts the card equals the colonist the
    /// world goes on to build, field for field.</para>
    ///
    /// <para><b>The slot is the pawn id, and it has to be.</b> Both draws mix the id in, so the same
    /// seed in slot 0 and slot 1 gives two different people — which is a feature on a screen of
    /// three, and a trap if the preview guesses the id wrong. The colonists a scenario places take
    /// ids in order from 1, so slot <c>i</c> is id <c>i + 1</c>; <see cref="IdForSlot"/> states that
    /// once and both sides call it.</para>
    ///
    /// <para>Pure simulation: no UnityEngine, so the whole of it runs in the fast tier.</para>
    /// </summary>
    public static class ColonistDraw
    {
        /// <summary>
        /// The id the colonist in this slot will be given. Placement spawns colonists first and in
        /// order, and <see cref="PawnRegistry"/> hands out ids from 1.
        /// </summary>
        public static PawnId IdForSlot(int slot) => new PawnId(slot + 1);

        /// <summary>
        /// Roll the colonist that seed would produce in that slot.
        ///
        /// <para>The returned pawn is a real one and a throwaway: it is in no registry, stands at no
        /// cell, and exists to be read for its skills, passions and seed. Callers want
        /// <see cref="Pawn.SkillLevel"/> and <see cref="Pawn.Passions"/> off it and nothing
        /// else.</para>
        /// </summary>
        public static Pawn Roll(uint seed, int slot, PawnContent content)
        {
            // Cell -1: it is nowhere, and a real index would be a lie that some later reader could
            // act on. Nothing here walks, paths or is drawn.
            var pawn = new Pawn(IdForSlot(slot), cell: -1, content) { RollSeed = seed };
            pawn.RollPassions();
            pawn.RollStartingSkills();
            // The traits on the card are the traits she walks with (design 51 §5f): the same
            // stream, the same seed, the same id the colony will give her.
            pawn.RollTraits();
            return pawn;
        }

        /// <summary>
        /// The same roll against the core content pack, for a caller that has no world to take
        /// content from — which on the select screen is every caller, since the whole point is that
        /// no world exists yet.
        /// </summary>
        public static Pawn Roll(uint seed, int slot) => Roll(seed, slot, ContentPack.Pawns());
    }
}
