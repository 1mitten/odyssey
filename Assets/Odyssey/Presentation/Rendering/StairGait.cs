#nullable enable
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The stair gait: a smooth vertical modulation that makes a figure crossing a bank read as
    /// climbing steps rather than riding an escalator up the ramp.
    ///
    /// <para><b>The third attempt, and what the first two got wrong.</b> A parabola vaulted the
    /// lip (<i>"it looks like they jump a bit and not flat with the terrain"</i>); strides held
    /// and pushed (<i>"four deliberate jolts a climb"</i>); both were removed for the surface
    /// following that <c>HopArc</c> documents. What each actually failed on was shape: a
    /// parabola lifts where the ground does not, and rectangular holds carry velocity
    /// discontinuities at every edge. This curve is a <b>smoothed sawtooth added to the
    /// ramp</b> — C1 by construction, since a cosine half-wave begins and ends with zero slope
    /// and each tread begins exactly where the last ended — so there is no edge to jolt at. The
    /// figure is never below the ramp (the lift only adds), never moves down while climbing
    /// (the lift's steepest descent exactly cancels the ramp's climb, which is what a level
    /// tread is), and lands on the surface at every tread boundary.</para>
    ///
    /// <para><b>Phased to the climbing half of the step, not to the step.</b> A bank sits in
    /// one cell — the one at the foot of the terrace — so the climb occupies one half of the
    /// drawn step and the other half is flat walking. Keying the curve to the whole step would
    /// bob the figure across the flat approach, which is the parabola's fault again from the
    /// other side. <see cref="PawnPose.OnTheDrawnGround"/> hands each half its own rise and
    /// its own progress; the curve is zero wherever its half does not climb.</para>
    ///
    /// <para><b>Nothing here is simulated.</b> Like <see cref="HopArc"/> and
    /// <see cref="GroundRelief"/>, this only decides where a figure is drawn between two cells.
    /// It cannot move a pawn, change a path or touch the state hash, and a pawn on a bank walks
    /// at exactly the price the simulation set long before any of this existed.</para>
    /// </summary>
    public static class StairGait
    {
        /// <summary>
        /// Whether the stair gait is drawn at all. Off, a bank crossing is the plain
        /// surface-following glide — which is what <c>BankFootingTests</c> measured before this
        /// existed, and what an owner who prefers the glide can have back in one switch.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// The height of one apparent step, in metres. The treads that fit a rise are
        /// <c>round(rise / TreadHeight)</c>, so a one-layer terrace climb — two sim steps of
        /// 1.5 m each — reads as about three steps a step, six a terrace: close to a real
        /// staircase, and one number to change at the keyboard while watching.
        /// </summary>
        public static float TreadHeight { get; set; } = 0.5f;

        /// <summary>
        /// How much of each tread's period the movement takes, 0 to 1. Climbing, the rise
        /// happens in the first <c>MoveShare</c> of each tread and the rest is a level tread;
        /// descending, the first part is held level and the lowering takes the last
        /// <c>MoveShare</c>. Smaller is snappier and more jolt-like; the rejects were the
        /// limit at zero.
        /// </summary>
        public static float MoveShare { get; set; } = 0.4f;

        /// <summary>
        /// How much of a tread the swing foot's knee lift raises it, 0 to 1. Zero turns the
        /// lift off and leaves the feet to the gait clips alone.
        /// </summary>
        public static float KneeLiftShare { get; set; } = 0.5f;

        /// <summary>Reset to the shipped values. For tests, which must not inherit each other's
        /// tuning — the rule <c>Footing</c> and <c>BankLayout</c> already follow.</summary>
        public static void Reset()
        {
            Enabled = true;
            TreadHeight = 0.5f;
            MoveShare = 0.4f;
            KneeLiftShare = 0.5f;
        }

        /// <summary>
        /// How far ahead of the ramp's own climb the figure is drawn, in metres — zero at both
        /// ends of the half and at every tread boundary between, and never more than a sole's
        /// width under the ramp in between.
        ///
        /// <para>The lift is <b>added</b> to the sampled surface, so it cannot put the figure
        /// meaningfully below the ground and does not need to agree with the bank's own shape at
        /// a corner. Its one imperfection is bounded and deliberate: the cosine opens with zero
        /// slope while the ramp is already climbing, so for the first sliver of each tread the
        /// pure curve sits up to 8 mm under the linear climb — which the caller's ground clamp
        /// turns into a landing rather than a sinking. Its steepest descent under the hold is
        /// the ramp's climb exactly cancelled: the level tread is the one place the combined
        /// height stands still, which is the look — step, level, step — rather than a
        /// slide.</para>
        /// </summary>
        /// <param name="rise">How far this half of the step climbs. Zero or a rounding on
        /// nothing draws no stair.</param>
        /// <param name="u">Progress through that half, 0 to 1.</param>
        public static float Lift(float rise, float u)
        {
            if (!Enabled || rise <= 0.05f) return 0f;

            int treads = Mathf.Max(1, Mathf.RoundToInt(rise / Mathf.Max(0.05f, TreadHeight)));
            float treadHeight = rise / treads;
            float phase = Mathf.Clamp01(u) * treads;
            int tread = Mathf.Min(treads - 1, Mathf.FloorToInt(phase));
            float within = phase - tread;

            float share = Mathf.Clamp(MoveShare, 0.05f, 0.95f);
            float liftFraction = within < share
                ? 0.5f - 0.5f * Mathf.Cos(Mathf.PI * within / share)
                : 1f;

            float linearHeight = Mathf.Clamp01(u) * rise;
            return (tread + liftFraction) * treadHeight - linearHeight;
        }

        /// <summary>
        /// How far above the descending ramp a figure is held while it steps down, in metres —
        /// zero at both ends of the half and at every tread boundary between, and never more
        /// than a hair under the ramp in between.
        ///
        /// <para>The mirror of <see cref="Lift"/>: each tread holds level — the figure standing
        /// at the height it gained — and then lowers smoothly to the next, instead of the
        /// gravity fall the sheer edges keep. The hold is above the ramp rather than a shelf
        /// cut into it, so like the lift it can never draw the figure meaningfully inside the
        /// ground, and the combined height never once moves up on the way down. The lift's
        /// opening dip is mirrored here at each tread's <i>end</i> — the cosine closes flat
        /// while the ramp is still descending, about a millimetre — and the caller's ground
        /// clamp turns it into a landing.</para>
        /// </summary>
        /// <param name="drop">How far this half of the step descends.</param>
        /// <param name="u">Progress through that half, 0 to 1.</param>
        public static float Hold(float drop, float u)
        {
            if (!Enabled || drop <= 0.05f) return 0f;

            int treads = Mathf.Max(1, Mathf.RoundToInt(drop / Mathf.Max(0.05f, TreadHeight)));
            float treadHeight = drop / treads;
            float phase = Mathf.Clamp01(u) * treads;
            int tread = Mathf.Min(treads - 1, Mathf.FloorToInt(phase));
            float within = phase - tread;

            float share = Mathf.Clamp(MoveShare, 0.05f, 0.95f);
            float lowerFraction = within < 1f - share
                ? 0f
                : 0.5f - 0.5f * Mathf.Cos(Mathf.PI * (within - (1f - share)) / share);

            float linearDrop = Mathf.Clamp01(u) * drop;
            return linearDrop - (tread + lowerFraction) * treadHeight;
        }

        /// <summary>
        /// Is this step a bank being climbed — the one question both the pose and the footing
        /// pass need answered, answered once.
        ///
        /// <para><b>One owner, two askers</b> (<c>docs/bug-patterns.md</c> P1):
        /// <see cref="PawnPose.OnTheDrawnGround"/> decides where the stair is drawn and
        /// <c>PawnFigureDirector</c> decides whether a foot may lift, and the two must not
        /// each re-derive "is this a banked climb" from the mirror in their own words, or the
        /// day one of them changes its mind the knees will lift for a glide.</para>
        /// </summary>
        public static bool IsClimbing(WorldRenderModel? world, in PawnView pawn)
        {
            if (!Enabled || world == null || !pawn.Moving) return false;
            if (!BankLayout.At(world, pawn.Cell).Exists &&
                !BankLayout.At(world, pawn.NextCell).Exists) return false;

            // A layer change is not the test: stepping <b>on to</b> the foot of a bank climbs a
            // metre and a half of ramp without leaving its layer, and it is drawn as a stair like
            // any other climb. The surface at the destination centre against the origin centre,
            // half a tread apart or more — flat crossings along a terrace foot read level and a
            // hop reads a full layer, so the one threshold sits far from either.
            Vector3 from = CellMetrics.FloorCentre(pawn.Cell);
            Vector3 to = CellMetrics.FloorCentre(pawn.NextCell);
            float rise = to.y + BankLayout.RiseAt(world, pawn.NextCell, to.x, to.z)
                       - from.y - BankLayout.RiseAt(world, pawn.Cell, from.x, from.z);
            return rise > TreadHeight * 0.5f;
        }

        /// <summary>
        /// The extra height given a swing foot's target while climbing, in metres, faded in
        /// over exactly the band where <see cref="Footing.Correction"/> fades out.
        ///
        /// <para>The footing pass plants any foot within half its reach flat on the ground at
        /// full strength — that is what keeps a walk from skating — and fades to nothing by the
        /// full reach for a foot the gait has deliberately swung clear. The knee lift fades in
        /// over that same band, so it only ever raises a foot the planter was already letting
        /// go of: the planted foot is untouched, and a mid-swing foot on a climb comes up to
        /// clear the tread rather than dragging along the ramp.</para>
        /// </summary>
        /// <param name="tread">The tread in play — a figure's <c>StairTread</c>, zero when it
        /// is not climbing a bank.</param>
        /// <param name="airGap">How far the foot is above the ground, negative or zero for a
        /// foot at or under it.</param>
        /// <param name="reach">The footing reach, handed in so the two bands cannot drift
        /// apart if one is retuned.</param>
        public static float KneeLift(float tread, float airGap, float reach)
        {
            if (!Enabled || tread <= 0.05f || KneeLiftShare <= 0f || airGap <= 0f) return 0f;

            float half = Mathf.Max(1e-3f, reach * 0.5f);
            float t = Mathf.Clamp01((airGap - half) / half);
            if (t <= 0f) return 0f;

            float swing = t * t * (3f - 2f * t);
            return tread * KneeLiftShare * swing;
        }
    }
}
