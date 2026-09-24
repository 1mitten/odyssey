#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>One blow that took hit points, as the hooks report it.</summary>
    public readonly struct DamageReport
    {
        /// <summary>Who was hurt.</summary>
        public readonly Pawn Target;

        /// <summary>Who did it, or null where nobody did.</summary>
        public readonly Pawn? Attacker;

        /// <summary>How much, in thousandths of a hit point.</summary>
        public readonly int DamageMilli;

        /// <summary>The item def index it was done with, or -1 for bare hands or teeth.</summary>
        public readonly int Weapon;

        public readonly int Tick;

        public DamageReport(Pawn target, Pawn? attacker, int damageMilli, int weapon, int tick)
        {
            Target = target;
            Attacker = attacker;
            DamageMilli = damageMilli;
            Weapon = weapon;
            Tick = tick;
        }
    }

    /// <summary>
    /// One swing that reached a pawn, whatever came of it — a hit, a miss, a dodge, or a blow that
    /// fell on air because she stepped out of reach during the wind-up (design 33 §14f).
    /// </summary>
    public readonly struct SwingReport
    {
        /// <summary>Who was swung at.</summary>
        public readonly Pawn Target;

        /// <summary>Who swung.</summary>
        public readonly Pawn Attacker;

        /// <summary><see cref="CombatEventKind.Hit"/>, <see cref="CombatEventKind.Miss"/> or <see cref="CombatEventKind.Dodge"/>.</summary>
        public readonly CombatEventKind Result;

        /// <summary>The item def index it was swung with, or -1 for bare hands or teeth.</summary>
        public readonly int Weapon;

        public readonly int Tick;

        public SwingReport(Pawn target, Pawn attacker, CombatEventKind result, int weapon, int tick)
        {
            Target = target;
            Attacker = attacker;
            Result = result;
            Weapon = weapon;
            Tick = tick;
        }

        /// <summary>Did it take hit points? A <see cref="CombatSystem"/> hit is followed by <c>DamageApplied</c>.</summary>
        public bool Landed => Result == CombatEventKind.Hit;
    }

    /// <summary>
    /// Something that wants to hear about a fight: friendly fire's two memories (C5), a dropped
    /// weapon on a death (C3), a rescue's giver noticing somebody went down (C4). Registered on
    /// <see cref="CombatHooks"/> by the composition, in a fixed order.
    ///
    /// <para><b>Called inside the tick, synchronously, and allowed to change the world</b> — a
    /// memory is a write — but not the pawn list: a death is already deferred
    /// (<c>ctx.Defer</c>) by the time <see cref="Died"/> is called, and a listener that removed a
    /// pawn would be doing it twice.</para>
    /// </summary>
    public interface ICombatListener
    {
        /// <summary>
        /// A swing reached a pawn (design 33 §14f): raised once for every swing
        /// <c>CombatSystem.ApplySwing</c> resolves on a pawn — landed, missed, dodged or on air —
        /// before its outcome is applied, so for a hit it comes before <see cref="DamageApplied"/>.
        /// Never for a blow at a building (no hooks, §13g), nor for a swing lost in the air to its
        /// attacker's own stun or fall, which never reaches her.
        /// </summary>
        void SwingResolved(in SwingReport report);

        /// <summary>A blow landed and took hit points.</summary>
        void DamageApplied(in DamageReport report);

        /// <summary>A pawn went down, at <paramref name="tick"/>.</summary>
        void Downed(Pawn pawn, Pawn? by, int tick);

        /// <summary>A pawn died and left <paramref name="corpseId"/>. The pawn has not yet left the registry.</summary>
        void Died(Pawn pawn, Pawn? by, int corpseId, int tick);
    }

    /// <summary>
    /// The hooks (design 33 §5, §14f): <c>SwingResolved</c>, <c>DamageApplied</c>, <c>Downed</c> and
    /// <c>Died</c>, raised by the fight's rules and heard by anyone registered.
    ///
    /// <para><b>A listener list, not C# events</b>, for the reason the work givers are a list: the
    /// order they are called in is the order they were registered in, which the composition fixes,
    /// and it can be read back and asserted. A multicast delegate would give the same order and
    /// no way to see it.</para>
    ///
    /// <para><b>Raised by lane A and by nothing else.</b> Every hit point anybody loses goes through
    /// <c>CombatSystem</c>, and <c>CombatSystem</c> is what calls these — so a listener hears every
    /// blow exactly once, whatever struck it.</para>
    /// </summary>
    public sealed class CombatHooks
    {
        readonly List<ICombatListener> _listeners = new List<ICombatListener>();

        public IReadOnlyList<ICombatListener> Listeners => _listeners;

        /// <summary>Add a listener. The same one twice is refused, because it would hear every blow twice.</summary>
        public void Add(ICombatListener listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            if (_listeners.Contains(listener))
                throw new InvalidOperationException($"{listener.GetType().FullName} is already listening.");
            _listeners.Add(listener);
        }

        public void RaiseSwingResolved(in SwingReport report)
        {
            for (int i = 0; i < _listeners.Count; i++) _listeners[i].SwingResolved(report);
        }

        public void RaiseDamageApplied(in DamageReport report)
        {
            for (int i = 0; i < _listeners.Count; i++) _listeners[i].DamageApplied(report);
        }

        public void RaiseDowned(Pawn pawn, Pawn? by, int tick)
        {
            for (int i = 0; i < _listeners.Count; i++) _listeners[i].Downed(pawn, by, tick);
        }

        public void RaiseDied(Pawn pawn, Pawn? by, int corpseId, int tick)
        {
            for (int i = 0; i < _listeners.Count; i++) _listeners[i].Died(pawn, by, corpseId, tick);
        }
    }
}
