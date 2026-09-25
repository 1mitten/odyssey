#nullable enable
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <b>The blood seam</b> (design 33 §7d; decision, 2026-09-23: <i>"seam now, build next"</i>).
    /// What <c>CombatFeedback</c> hands on for every combat event that bleeds, and nothing else.
    /// <see cref="BloodDirector"/> draws it (design 33 §10); the default is
    /// <see cref="NoBloodEffects"/>, which draws nothing.
    ///
    /// <para><b>Reshaped by the blood unit</b> (§10c): a spurt carries the struck pawn's feet
    /// beside the wound, because the drops need ground to land on, and a pool carries who it is
    /// under and how long that body is, because it waits for the fall and then asks where the body
    /// lies.</para>
    ///
    /// <para><b>Presentation only, for ever</b>: nothing an implementation does is in a cell, a
    /// save or the state hash, and nothing in the simulation hears of it. A player cannot clean
    /// blood because there is nothing to clean.</para>
    ///
    /// <para><b>Every event is handed on whatever layer it happened on</b>, unlike the floating
    /// words: a mark on the ground has to exist when the player later looks at that layer, so
    /// hiding a mark on an undrawn layer is the implementation's business (the corpses' rule —
    /// forced off with its layer), not the caller's.</para>
    /// </summary>
    public interface IBloodEffects
    {
        /// <summary>
        /// A landed hit. <paramref name="feet"/> is the struck pawn's feet on the ground and
        /// <paramref name="wound"/> where on its body the blow landed, both in world space;
        /// <paramref name="direction"/> the way the blow travelled, horizontal and
        /// normalised, or zero when the attacker could not be placed; <paramref name="amount"/>
        /// the damage in whole hit points; <paramref name="sharp"/> whether the weapon cuts
        /// (<c>Odyssey.Hud.BloodSides</c>). Sharp spurts more and leaves a splatter; blunt is a
        /// smaller puff and a smaller mark (owner).
        /// </summary>
        void Spurt(Vector3 feet, Vector3 wound, Vector3 direction, float amount, bool sharp);

        /// <summary>
        /// A body gone down or dead: a pool under it. <paramref name="who"/> is the body's pawn,
        /// <paramref name="feet"/> its feet on the ground when it fell, <paramref name="sizeFactor"/>
        /// 0 to 1 of the largest pool (<c>Odyssey.Hud.BloodModel.PoolSize</c>: a death 1, a down
        /// 0.6), and <paramref name="bodyLength"/> how long the body is lying down, in metres.
        /// </summary>
        void Pool(PawnId who, Vector3 feet, float sizeFactor, float bodyLength);

        /// <summary>
        /// The world changed — a load, a new game, back to the menu. Every mark belongs to the
        /// world that made it and goes with it.
        /// </summary>
        void Clear();
    }

    /// <summary>The blood seam's default until the blood unit: it draws nothing, and costs a virtual call per bleeding event.</summary>
    public sealed class NoBloodEffects : IBloodEffects
    {
        public static readonly NoBloodEffects Instance = new NoBloodEffects();

        NoBloodEffects() { }

        public void Spurt(Vector3 feet, Vector3 wound, Vector3 direction, float amount, bool sharp) { }

        public void Pool(PawnId who, Vector3 feet, float sizeFactor, float bodyLength) { }

        public void Clear() { }
    }
}
