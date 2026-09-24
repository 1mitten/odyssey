#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <b>The blood seam</b> (design 33 §7d; decision, 2026-09-23: <i>"seam now, build next"</i>).
    /// What <c>CombatFeedback</c> hands on for every combat event that bleeds, and nothing else.
    /// The next unit fills it — spurt particles, ground splatter, pools — and this unit builds
    /// nothing visible: the default is <see cref="NoBloodEffects"/>.
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
        /// A landed hit. <paramref name="at"/> is where on the struck body the blow landed, in
        /// world space; <paramref name="direction"/> the way the blow travelled, horizontal and
        /// normalised, or zero when the attacker could not be placed; <paramref name="amount"/>
        /// the damage in whole hit points; <paramref name="sharp"/> whether the weapon cuts
        /// (<c>Odyssey.Hud.BloodSides</c>). Sharp spurts more and leaves a splatter; blunt is a
        /// smaller puff and a smaller mark (owner).
        /// </summary>
        void Spurt(Vector3 at, Vector3 direction, float amount, bool sharp);

        /// <summary>
        /// A body gone down or dead: a pool under it. <paramref name="at"/> is the body's feet on
        /// the ground, and <paramref name="sizeFactor"/> 0 to 1 of the largest pool
        /// (<c>Odyssey.Hud.BloodModel.PoolSize</c>: a death 1, a down 0.6).
        /// </summary>
        void Pool(Vector3 at, float sizeFactor);

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

        public void Spurt(Vector3 at, Vector3 direction, float amount, bool sharp) { }

        public void Pool(Vector3 at, float sizeFactor) { }

        public void Clear() { }
    }
}
