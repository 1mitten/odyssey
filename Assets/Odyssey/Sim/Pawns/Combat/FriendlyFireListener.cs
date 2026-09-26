#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Friendly fire's two memories (design 33 §1, §12), heard on the fight's hooks and registered
    /// by <see cref="CombatListeners"/> after the weapon drop. <b>The C5 lane's file.</b>
    /// <list type="bullet">
    /// <item><b>Attacked</b> (<see cref="ThoughtIndex.AttackedByColonist"/>): a colonist a colonist
    /// swung at — a hit, a miss, a dodge or a blow on air alike (design 33 §14f; the owner: a missed
    /// swing counts), heard on <see cref="SwingResolved"/>.
    /// Added through <see cref="Pawn.AddMemory"/>, so the thought decides a second blow: no second
    /// copy (its stack limit is one), and the day renewed from the latest blow (its
    /// <c>renewsOnRepeat</c>, design 33 §14e).</item>
    /// <item><b>Died</b> (<see cref="ThoughtIndex.ColonistDied"/>): a colonist died, and every
    /// other colonist on the board remembers it — standing, downed, drafted or broken. A bandit's
    /// or an animal's death is felt by nobody, and nobody but a colonist feels anything.</item>
    /// </list>
    ///
    /// <para><b>No opinions</b> (the owner's "no opinions yet"): a memory names no other pawn, so
    /// who struck her, and whom she mourns, is not kept.</para>
    ///
    /// <para><b>Cost:</b> nothing per tick. A swing at a pawn is two comparisons, and on friendly fire a walk of
    /// the victim's memories; a colonist's death is one pass over the pawns on the board. The
    /// goldens cannot see it: memories are hashed per pawn, and no golden window has a colonist
    /// hurt by a colonist or a death.</para>
    /// </summary>
    public sealed class FriendlyFireListener : ICombatListener
    {
        readonly PawnContext _ctx;

        public FriendlyFireListener(PawnContext ctx) { _ctx = ctx; }

        /// <summary>
        /// A colonist swung at a colonist: she remembers it, whatever came of the swing (design 33
        /// §14f). Heard here and not on <see cref="DamageApplied"/>, so the rule has one hook. A
        /// swing reaching a colonist already past the death line gives nothing: the dead feel
        /// nothing.
        /// </summary>
        public void SwingResolved(in SwingReport report)
        {
            Pawn? by = report.Attacker;
            Pawn target = report.Target;
            if (by == null || !Allegiance.AreAllies(by, target) || Melee.IsDead(target)) return;
            target.AddMemory(ThoughtIndex.AttackedByColonist, report.Tick);
        }

        /// <summary>Hit points taken add nothing: the swing that took them was heard already.</summary>
        public void DamageApplied(in DamageReport report) { }

        /// <summary>Going down is not a death, and nobody mourns it.</summary>
        public void Downed(Pawn pawn, Pawn? by, int tick) { }

        /// <summary>
        /// Every other colonist remembers a colonist's death. The dead pawn is still in the registry
        /// here (the hook's promise), so she is passed over by identity.
        /// </summary>
        public void Died(Pawn pawn, Pawn? by, int corpseId, int tick)
        {
            if (!pawn.IsColonist) return;
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == pawn || !other.IsColonist) continue;
                other.AddMemory(ThoughtIndex.ColonistDied, tick);
            }
        }
    }
}
