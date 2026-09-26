#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>What a moment of a fight leaves in blood: nothing, a spurt from the wound, or a pool under the body.</summary>
    public enum BloodMark : byte
    {
        None = 0,
        Spurt = 1,
        Pool = 2,
    }

    /// <summary>
    /// <b>The blood seam's rules</b> (design 33 §7d; decision: <i>"seam now, build next"</i>). Which
    /// combat events bleed, how big a pool is, and whether a blow was sharp or blunt. Nothing here
    /// draws: presentation's <c>CombatFeedback</c> asks these questions of every event it hands on
    /// and passes the answers to an <c>IBloodEffects</c>, which is a no-op until the blood unit
    /// fills it.
    ///
    /// <para><b>Unity-free, so the fast tier owns the rules</b> — the owner's words are rules
    /// ("every landed hit spurts", "misses and dodges draw nothing", "downs and deaths leave a
    /// pool", "sharp spurts more") and a rule is the thing a test can hold. <c>BloodModelTests</c>.</para>
    /// </summary>
    public static class BloodModel
    {
        /// <summary>
        /// What an event leaves (owner): <b>every landed hit spurts</b>; <b>a down or a death
        /// leaves a pool under the body</b>; a swing, a miss, a dodge, a stun and a recovery leave
        /// nothing. A stun is reported beside the hit that caused it, which already spurted.
        /// </summary>
        public static BloodMark For(CombatEventKind kind) => kind switch
        {
            CombatEventKind.Hit => BloodMark.Spurt,
            CombatEventKind.Downed => BloodMark.Pool,
            CombatEventKind.Died => BloodMark.Pool,
            _ => BloodMark.None,
        };

        /// <summary>
        /// What this moment leaves: <see cref="For(CombatEventKind)"/>, except that <b>a building
        /// never bleeds</b> (design 33 §13i) — a blow reported against target 0 is a blow at a
        /// wall, a door or a bed. The one question <c>CombatFeedback.Bleed</c> asks.
        /// </summary>
        public static BloodMark For(in CombatEventView combatEvent) =>
            combatEvent.Target.IsValid ? For(combatEvent.Kind) : BloodMark.None;

        /// <summary>
        /// A pool's size as a fraction of the largest: a death's is the whole, a down's most of
        /// it. INVENTED; the next unit scales its decal by this and by the body's own size.
        /// </summary>
        public static float PoolSize(CombatEventKind kind) => kind switch
        {
            CombatEventKind.Died => 1f,
            CombatEventKind.Downed => 0.6f,
            _ => 0f,
        };
    }

    /// <summary>
    /// <b>Sharp or blunt, per blow</b>, resolved the way the simulation arms a pawn
    /// (<c>WeaponRules.ArmamentOf</c>): the weapon in the hand if it has an attack, else the
    /// attacker's species' natural attack, else bare hands. The table is read once off the
    /// content by presentation (<c>CombatFeedback.BloodSidesOf</c>) — this assembly cannot see
    /// <c>Odyssey.Sim</c>'s Defs — so the Defs stay the one owner of which weapon is which: the
    /// bat and the crowbar are blunt, the machete and the arc blade sharp, fists blunt, a rat's
    /// teeth sharp (<c>Items.xml</c>, <c>Combat.xml</c>, <c>Species.xml</c>).
    /// </summary>
    public sealed class BloodSides
    {
        readonly bool?[] _weapon;
        readonly bool?[] _natural;
        readonly bool[] _wields;
        readonly bool _fists;

        /// <param name="weaponSharp">By item def: whether its attack is sharp, or null for an item that is not a weapon.</param>
        /// <param name="naturalSharp">By pawn kind: whether its species' natural attack is sharp, or null for a species with none (a person).</param>
        /// <param name="fistsSharp">Whether bare hands are sharp. They are not, but the content says so, not this file.</param>
        /// <param name="wieldsNatural">By pawn kind: whether its species' own attack is a weapon it
        /// swings (a Heavy or Light style, the butcher's cleaver) rather than teeth or fists, or
        /// null for none. What makes a natural attack whoosh (design 62 §8d).</param>
        public BloodSides(bool?[] weaponSharp, bool?[] naturalSharp, bool fistsSharp, bool[]? wieldsNatural = null)
        {
            _weapon = weaponSharp ?? Array.Empty<bool?>();
            _natural = naturalSharp ?? Array.Empty<bool?>();
            _wields = wieldsNatural ?? Array.Empty<bool>();
            _fists = fistsSharp;
        }

        /// <summary>Does a pawn of this kind swing a weapon of its own when it holds none (design 62 §8d)?</summary>
        public bool WieldsNatural(int attackerKind) => (uint)attackerKind < (uint)_wields.Length && _wields[attackerKind];

        /// <summary>Everything blunt: the table before any content is read, and a checkout with none.</summary>
        public static readonly BloodSides AllBlunt = new BloodSides(Array.Empty<bool?>(), Array.Empty<bool?>(), false);

        /// <summary>
        /// Was a blow sharp? <paramref name="weapon"/> is the event's (<c>CombatEventView.Weapon</c>,
        /// an item def or -1 for a natural attack or fists); <paramref name="attackerKind"/> is the
        /// attacker's <c>PawnView.Kind</c>, or -1 when the attacker is not in the frame.
        /// </summary>
        public bool IsSharp(int weapon, int attackerKind)
        {
            if ((uint)weapon < (uint)_weapon.Length && _weapon[weapon] is bool held) return held;
            if ((uint)attackerKind < (uint)_natural.Length && _natural[attackerKind] is bool natural) return natural;
            return _fists;
        }

        /// <summary>
        /// Was the blow struck with a weapon held in the hand — an item with an attack — rather than
        /// fists or a natural attack? The sound of a blow asks it (design 33 §9g): every swing of a
        /// bat, a crowbar, a machete or an arc blade whooshes, and fists and bites are silent. The
        /// same table as <see cref="IsSharp"/>, so the Defs stay the one owner of what a weapon is.
        /// </summary>
        public bool IsHeldWeapon(int weapon) => (uint)weapon < (uint)_weapon.Length && _weapon[weapon].HasValue;
    }
}
