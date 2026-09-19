#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// What a colonist with something in its arms does with itself, and where the load sits.
    /// Design 24.
    ///
    /// <para><b>A stroke's shape without a stroke's clock.</b> Carrying is neither of the two
    /// things <see cref="Gesture"/>'s own note distinguishes: it is not a one-shot motion and it
    /// is not a cycle. It is a <em>stance</em> — a set of angles held for as long as a state
    /// holds — so all there is to author is the shape, and all there is to drive is how quickly
    /// the figure eases into and out of it.</para>
    ///
    /// <para><b>The angles are authored; the cradle is measured.</b> The other way round was
    /// tempting — compute a cradle point from hip height and solve both hands to it — and it is
    /// wrong for this pose. Arm length varies across the sixty-one rigs by more than the cradle
    /// does, so a solved point puts a short-armed colonist's elbows out at full stretch to reach a
    /// load that a long-armed one holds against its chest. Authoring the elbow and shoulder and
    /// then asking where the palms ended up gives every rig the same <em>posture</em>, which is
    /// what a viewer reads, and lets the load sit wherever that rig's arms actually are.</para>
    ///
    /// <para>The one thing that is then solved is <see cref="Clearance"/>, along the one axis
    /// where an authored angle can fail outright: a rig whose arms are short enough to bring the
    /// load inside its own chest. That is measured per figure and corrected, because a wood bundle
    /// drawn through a colonist's ribs is not a pose that needs tuning, it is a fault.</para>
    ///
    /// <para><b>Everything is a fraction or an angle, never a distance in metres</b> — the rule
    /// <see cref="SwimPose"/> and <see cref="Gesture"/> both keep, for the same reason. The packs
    /// differ in proportion and the director scales them besides, so a 0.4 m cradle is a waist on
    /// one colonist and a chest on another.</para>
    ///
    /// <para>Pure arithmetic over transforms, in its own file, so it can be checked by a test
    /// rather than by looking — the bargain <see cref="Footing"/>, <see cref="WorkSwing"/>,
    /// <see cref="WaterLine"/> and <see cref="Gesture"/> all make.</para>
    /// </summary>
    public static class CarryPose
    {
        // ---- the stance --------------------------------------------------------------------
        //
        // Every number below is *proposed* and none has been looked at on a running game. They are
        // settable rather than const so a harness can sweep them for a contact sheet without a
        // recompile, which is how the swim's and the climb's were settled.

        /// <summary>
        /// How far the upper arms pitch forward from hanging, in degrees.
        ///
        /// <para>Small, and that is the difference between carrying and offering. The weight of a
        /// scooped load is on the forearms, not on the shoulders — a person carrying a box does
        /// not raise their upper arms, they bend their elbows and let the box rest there. Push
        /// this past about thirty and the colonist is presenting the thing to somebody.</para>
        /// </summary>
        public static float ShoulderPitch { get; set; } = -15f;

        /// <summary>
        /// How far the elbows bend, in degrees, bringing the forearms up towards horizontal.
        ///
        /// <para><b>Not ninety.</b> A right angle is a waiter's tray: forearms dead level, load
        /// balanced rather than held. Fifteen degrees under it puts the hands slightly higher than
        /// the elbows, which is the cradle — the load cannot slide off the front, and the arms
        /// read as being <em>under</em> it rather than beside it. That is the owner's own word for
        /// what this pose is: "scooped underneath".</para>
        /// </summary>
        public static float ElbowBend { get; set; } = -75f;

        /// <summary>
        /// How far the spine leans back, in degrees, to counter the weight held in front.
        ///
        /// <para>Six, and the smallness is deliberate. A real counter-lean under a heavy load is
        /// much more than this, but the play camera looks down at 48° — a lean away from vertical
        /// foreshortens into almost nothing while the same angle applied to a walking figure reads
        /// as somebody about to fall over backwards. This is the amount that survives the
        /// projection, not the amount a physiotherapist would draw.</para>
        /// </summary>
        public static float SpineLean { get; set; } = 6f;

        /// <summary>
        /// How long the figure takes to ease into the stance, and out of it again, in seconds.
        ///
        /// <para>The same order as <c>ClimbEaseSeconds</c>, and for the same reason: nothing about
        /// a figure may arrive on one frame. It matters most coming <em>out</em> of the lift, where
        /// the hands have just risen from the floor and the arms have to fold into the cradle
        /// without the load appearing to jump the last few inches.</para>
        /// </summary>
        public static float EaseSeconds { get; set; } = 0.20f;

        /// <summary>
        /// The least distance the load's near face may sit from the figure's own centre line, as a
        /// fraction of shoulder width.
        ///
        /// <para>The correction described in the class remarks, and the one number here that is a
        /// fault guard rather than taste. Half a shoulder width clears the chest on a barrel-
        /// chested rig with a little to spare.</para>
        /// </summary>
        public static float Clearance { get; set; } = 0.50f;

        /// <summary>
        /// How much of a swimmer a figure may be before its load stops being drawn.
        ///
        /// <para><b>The load vanishes in the water, and this is knowingly a placeholder</b> (owner,
        /// 2026-09-19: "make the item disappear for now when swimming for ease and decide later").
        /// Design 24 §5c has the argument: deep water is impassable so nobody swims a lake, but
        /// <em>shallow</em> water floats the figure — the owner's own 2026-09-17 decision — and
        /// <see cref="SwimPose"/> strokes both arms. A load left in those arms swings about with
        /// them. Hiding it is one comparison; the alternatives are a second solved pose or
        /// reversing the float.</para>
        ///
        /// <para>Half, so the load goes as the figure commits to the water rather than at the
        /// first toe in it. It is a hard cut and it will pop, which is the "for ease" part.</para>
        /// </summary>
        public static float HideAfloatAbove { get; set; } = 0.5f;

        /// <summary>
        /// Ease the carry weight towards where it should be. 0 is empty-handed, 1 is the full
        /// stance.
        ///
        /// <para>Linear towards the target rather than exponential, exactly as the climb's lean
        /// is, so the time to arrive is a number somebody can reason about instead of an
        /// asymptote.</para>
        /// </summary>
        public static float Settle(float weight, float target, float deltaTime)
        {
            if (EaseSeconds <= 1e-4f) return target;
            return Mathf.MoveTowards(weight, target, deltaTime / EaseSeconds);
        }

        /// <summary>
        /// Where the load sits, given where the two palms ended up.
        ///
        /// <para>The midpoint of the palms, pushed forward if that midpoint is too close to the
        /// figure's own centre line (<see cref="Clearance"/>). Forward along the figure's own
        /// facing, never along a bone's axis: which way a wrist points is a decision made by
        /// whoever rigged the character, where which way a colonist faces is a fact.</para>
        ///
        /// <para><b>The load's base, not its centre.</b> Every item prop in the catalogue has its
        /// origin on the floor — that is how <c>ItemHeap</c> can place a rock at a cell's floor
        /// centre and have it lie on the ground — so a matrix built at this point puts the bottom
        /// of the load on the palms, which is the arms being underneath it. Placing by centre
        /// buries half a wood bundle in the forearms.</para>
        /// </summary>
        /// <param name="leftPalm">World position of the left palm.</param>
        /// <param name="rightPalm">World position of the right palm.</param>
        /// <param name="chest">World position of the figure's centre line at chest height.</param>
        /// <param name="forward">The figure's facing, normalised, horizontal.</param>
        /// <param name="shoulderWidth">Distance between the two shoulder joints, in metres.</param>
        public static Vector3 Cradle(
            Vector3 leftPalm, Vector3 rightPalm, Vector3 chest, Vector3 forward, float shoulderWidth)
        {
            Vector3 at = (leftPalm + rightPalm) * 0.5f;

            if (shoulderWidth <= 1e-4f) return at;

            // How far in front of the centre line the palms are, measured along the facing only:
            // a load held out to one side is still clear of the chest, and penalising it would
            // shove it further out for no reason.
            Vector3 fromChest = at - chest;
            fromChest.y = 0f;
            float ahead = Vector3.Dot(fromChest, forward);

            float wanted = shoulderWidth * Clearance;
            if (ahead >= wanted) return at;

            return at + forward * (wanted - ahead);
        }

        /// <summary>
        /// Whether a load in these arms should be drawn at all.
        ///
        /// <para>One comparison, and the reason it is a named call rather than an inline test is
        /// that it is the placeholder of <see cref="HideAfloatAbove"/>: when the water case is
        /// decided properly there is one place to change, and a grep for the name finds every
        /// caller.</para>
        /// </summary>
        public static bool Drawn(float swimWeight) => swimWeight <= HideAfloatAbove;
    }
}
