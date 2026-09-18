#nullable enable

using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// How a figure is drawn across a step that changes layer: treading up it, or dropping off it.
    ///
    /// <para><b>The two directions are not the same kind of thing, and that is the design.</b>
    /// Going up there is a ramp underfoot the whole way — <c>BankMesh.HeightAt</c> is a plane from
    /// the floor of the lower cell to the rim of the upper one — so the figure walks the hillside
    /// in strides and its height is a function of <b>the ground under it</b>
    /// (<see cref="Stepped"/>). Going down there is nothing underfoot past the edge, so its height
    /// is a function of <b>time</b>: a beat at the lip and then a fall (<see cref="Fall"/>).</para>
    ///
    /// <para><b>Both of the owner's reports of 2026-09-18 are in here.</b> First the speed —
    /// <i>"the colonists looked too fast going up definitely"</i> — which was the price and is
    /// answered in <c>MoveCost.JumpUp</c>: the drawn path up a step is 3.91 m and it was being
    /// covered in 2.25 s, faster than walking on the flat. Then the motion — <i>"it looks like they
    /// jump a bit and not flat with the terrain, which they should be … would it be possible they
    /// take actual steps up the terrain in a few motions"</i> — which is this file. The first cut
    /// of the climb was a solved parabola over the lip, a jump; it is now four strides up the bank.
    /// Neither half would have done alone: a slow glide up a hillside is a colonist stuck on it,
    /// which is what the previous retune of that constant produced and was rejected for.</para>
    ///
    /// <para><b>Nothing here is simulated.</b> A hop's price, its legality and its two ends are the
    /// simulation's (<c>NavGraph.HopCost</c>, <c>NavGraph.IsHop</c>); this only decides where the
    /// figure is drawn between them, like <see cref="GroundRelief"/> and <see cref="BankLayout"/>.
    /// It cannot move a pawn, change a path or touch the state hash.</para>
    ///
    /// <para><b>Both ends are exact.</b> Standing at the bottom draws the bottom and standing on
    /// the top draws the top, so a step joins the two standing poses without a jolt at either end —
    /// the fault <c>WalkOnReliefTests</c> was written for.</para>
    /// </summary>
    public static class HopArc
    {
        /// <summary>
        /// About how much height one stride up a bank wins, in metres.
        ///
        /// <para><b>The number of steps falls out of this rather than being chosen.</b> A terrace
        /// climb is about 1.5 m — a colonist at the foot of one is already half way up the ramp —
        /// so 0.4 m gives four strides of 0.375 m each. A layer-high climb with no bank under it
        /// gives eight. Fixing the *count* instead would make a small step and a big one take the
        /// same number of strides, which is the thing that reads as wrong.</para>
        ///
        /// <para>It also bounds the fault this is most likely to be criticised for. The body is
        /// drawn at the tread it has stepped on to, so it leads the slope under it by up to one
        /// tread — that is what stepping *is*, since your hips go up when your foot does — and this
        /// is the size of that lead. Smaller reads as gliding; larger reads as floating.</para>
        /// </summary>
        public const float PreferredTread = 0.4f;

        /// <summary>
        /// The part of one stride spent pushing up on to the next tread, the rest being the plant.
        ///
        /// <para><b>A half, and the number is a budget rather than a taste.</b> A frame may move
        /// the figure about 50 mm before it stops reading as a stride and starts reading as a snap
        /// — that is <c>BankFootingTests.Smooth</c>, and it is twice what an honest frame of
        /// walking moves. A terrace climb is 1.5 m of rise inside half a 240-tick step, so an even
        /// glide is 25 mm a frame and concentrating it into a fraction <c>P</c> of each stride
        /// multiplies that by <c>1.5/P</c> — the 1.5 being the peak of the smoothstep against its
        /// own average. At a third that is 57 mm and it failed; at a half it is 38 mm.</para>
        ///
        /// <para>So this is as concentrated as a stride can be while the motion stays smooth, and
        /// making it snappier means slowing the climb down rather than steepening the push.</para>
        /// </summary>
        public const float Push = 0.5f;

        /// <summary>
        /// The part of a drop spent leaving the edge before the fall begins, as a fraction.
        ///
        /// <para><b>Derived, not chosen.</b> <c>MoveCost.Drop</c> is 50, which is 0.83 s at 60
        /// ticks a second, and a 3.0 m free fall under gravity takes 0.78 s. The difference is the
        /// step off the edge: hold for 0.05 s, then fall at the speed of gravity and land exactly
        /// when the simulation says the step ends. <c>HopArcTests.AFallIsAtTheSpeedOfGravity</c>
        /// pins that arithmetic, so retuning <c>MoveCost.Drop</c> fails a test here rather than
        /// quietly making colonists fall at the wrong speed.</para>
        /// </summary>
        public const float StepOff = 0.06f;

        /// <summary>
        /// Where the figure is drawn while climbing: the ground under it, taken in strides.
        ///
        /// <para><b>This is a function of height, not of time.</b> Give it the height of the drawn
        /// ground under the walker, the height it is climbing on to, and the whole rise of the
        /// step; it hands back the tread the figure has stepped on to. That is what makes it follow
        /// the hillside rather than sail over it: the shape of the bank decides where the strides
        /// fall, so where the ground is flat nothing rises, and where it is steep the strides come
        /// close together.</para>
        ///
        /// <para><b>Why it replaced an arc</b> (owner, 2026-09-18: <i>"when going up hill it looks
        /// like they jump a bit and not flat with the terrain, which they should be … would it be
        /// possible they take actual steps up the terrain in a few motions"</i>). The first cut was
        /// a solved parabola that left the ground, passed over the lip and landed — a jump, which
        /// is what the simulation calls this step and is not what the board shows. The board shows
        /// a <i>ramp</i>: <c>BankMesh.HeightAt</c> is a plane from the floor of the lower cell to
        /// the rim of the upper one, so there is a walkable surface the whole way and a body
        /// arcing over it is a body ignoring the ground it is on.</para>
        ///
        /// <para><b>The figure leads the slope, and that is the stride rather than a fault.</b> It
        /// is drawn at the tread it has stepped on to while the ramp beneath catches up, by at most
        /// <see cref="PreferredTread"/> — your hips go up when your foot does. It is never drawn
        /// below the ground, and it never rises above the ground it is climbing on to, which is the
        /// difference between this and the arc: <c>HopArcTests.AClimbNeverLeavesTheHillside</c>.
        /// </para>
        /// </summary>
        public static float Stepped(float ground, float landing, float rise)
        {
            // Nothing to climb: the caller's own ground is the answer. Also the guard against a
            // division by zero and against a step whose ends have been handed over the wrong way
            // round, either of which would draw a colonist at NaN — which is a colonist who
            // disappears rather than one who looks wrong.
            if (rise <= 0f || ground >= landing) return Mathf.Max(ground, landing);

            int strides = Mathf.Max(1, Mathf.RoundToInt(rise / PreferredTread));
            float tread = rise / strides;

            // How many treads below the landing the ground is. Whole part: which tread the figure
            // has its weight on. Fraction: how far through that stride the ground has come.
            float below = (landing - ground) / tread;
            float whole = Mathf.Floor(below);
            float through = below - whole;

            // The push on to the next tread happens at the *start* of the stride — as the ground
            // leaves the tread line behind — and the rest of it is the plant. Smoothed over that
            // window rather than switched, because a 37 cm jump in one frame is a snap.
            float pushed = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - Push, 1f, through));

            // Never below the ground it is walking on. Smoothstep leaves its ends flat, so at the
            // very start of a push the body rises more slowly than the ramp does for a fraction of
            // a stride — half a millimetre, measured, and cleaned up by the clamp in PawnPose on
            // the board. Cleaned up here as well, so the function is honest about it on its own
            // rather than relying on its caller.
            return Mathf.Max(ground, landing - (whole + pushed) * tread);
        }

        /// <summary>
        /// How much of the descent is done, 0 to 1, at this point through a hop <b>down</b>.
        ///
        /// <para>A square, because that is what falling is: distance goes as the square of time. The
        /// old linear version dropped a colonist three metres at a constant rate, which reads as
        /// being lowered rather than as letting go — the owner's word for the version before that
        /// was "floating".</para>
        ///
        /// <para>The curve is allowed to pass <i>below</i> the bank it is falling past, and the
        /// caller clamps it: a body that runs off a slope stays on the slope until the slope falls
        /// away faster than it does, and then it is in the air. That is one rule for both halves of
        /// the drop and it replaced a hand-faded lift that had to be tuned.</para>
        /// </summary>
        public static float Fall(float t)
        {
            float u = Flight(t, StepOff);
            return u * u;
        }

        /// <summary>How many strides a climb of this many metres is taken in.</summary>
        public static int Strides(float rise) => Mathf.Max(1, Mathf.RoundToInt(rise / PreferredTread));

        /// <summary>
        /// How far through the airborne part of the step this is: nothing until <paramref name="hold"/>
        /// of it has passed, then 0 to 1 over what is left.
        /// </summary>
        static float Flight(float t, float hold)
        {
            float u = (Mathf.Clamp01(t) - hold) / (1f - hold);
            return Mathf.Clamp01(u);
        }
    }
}
