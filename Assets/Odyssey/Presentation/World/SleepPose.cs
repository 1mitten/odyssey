#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A colonist lying down — in a bed, or on the ground where it dropped — as arithmetic.
    ///
    /// <para><b>Written because a sleeping colonist was drawn standing up.</b> Nothing in
    /// presentation knew a pawn could be asleep, so a colonist who had walked to a bed and gone to
    /// sleep in it stood bolt upright in it all night. The owner, watching that, reported that
    /// colonists would not use the beds and stood outside instead (2026-09-18) — and the
    /// simulation was measured and was right all along: the colonist was in the bed. There was
    /// simply no way to see it. A pose is not decoration here; it was the whole of the bug.</para>
    ///
    /// <para><b>Computed, not animated,</b> for the reason every pose in this folder is: no pack
    /// contains a sleep clip (e-02), and the axe, pick and hammer strokes are already standing in
    /// for art we do not have. <c>13-gestures.md</c> §3 draws the line — author the angles when the
    /// figure aims at something whose position the arithmetic does not know, solve to a point when
    /// it must meet something it does. Lying down is both: the mattress is a known plane at a known
    /// height, so the body is <i>placed</i> on it, and the limbs have nothing to reach for, so
    /// their angles are authored.</para>
    ///
    /// <para><b>Four postures, not one and not ten.</b> The owner's reference sheet shows ten, and
    /// a colony asleep in one posture reads as a morgue rather than as people. Four is what can be
    /// reached honestly from a standing idle clip by rotating the root and pitching six bones —
    /// back, back with the arms up, and two sides — and it is enough that no two neighbouring bunks
    /// look stamped. Which one a colonist takes is <see cref="PostureFor"/>: derived from the pawn
    /// id, so it is the same every night and after a load, and costs no state. The same bargain
    /// the colonist palette already makes.</para>
    ///
    /// <para><b>Everything is an angle or a fraction of the figure's own build</b>, never a number
    /// of metres, so it is right on all sixty-one characters without being authored sixty-one
    /// times — the packs differ in proportion and the director scales them besides.</para>
    /// </summary>
    public static class SleepPose
    {
        /// <summary>
        /// How far from upright a sleeper is laid, in degrees. Ninety: flat.
        ///
        /// <para>Unlike <see cref="SwimPose.PitchDegrees"/> this really is the whole right angle.
        /// A swimmer's head is up and its legs trail; a sleeper is on a flat surface, and any part
        /// left standing reads as a colonist who has fallen over rather than one at rest.</para>
        /// </summary>
        public static float PitchDegrees { get; set; } = 90f;

        /// <summary>
        /// Total height as a multiple of the standing hip height, which is what the rig measures.
        ///
        /// <para>A person's hip is a little over half their height, so this is the reciprocal of
        /// that rather than a guess at anybody's build: it turns the one length the director
        /// already knows about a character into the length of body it has to lay down.</para>
        /// </summary>
        public static float HeightPerHip { get; set; } = 1.9f;

        /// <summary>
        /// How far the body's middle floats above whatever it lies on, as a fraction of the hip
        /// height — half a torso's thickness, so the sleeper rests on the surface rather than
        /// sinking through it or hovering over it.
        /// </summary>
        public static float ThicknessPerHip { get; set; } = 0.16f;

        /// <summary>Breaths a second at rest. Slow: a sleeping adult, not a winded one.</summary>
        public static float BreathsPerSecond { get; set; } = 0.22f;

        /// <summary>How far the chest rises and falls, in degrees of spine pitch.</summary>
        public static float BreathDegrees { get; set; } = 1.6f;

        /// <summary>How long the figure takes to lie down or get up, in seconds.</summary>
        public static float SettleSeconds { get; set; } = 0.45f;

        /// <summary>
        /// One way of lying. Every angle is degrees, and every one of them is applied on top of
        /// whatever the standing idle clip is doing, eased by the sleep weight.
        /// </summary>
        public readonly struct Posture
        {
            /// <summary>The name, for a test failure and an overlay to say which one it is.</summary>
            public readonly string Name;

            /// <summary>
            /// Roll about the body's own long axis: 0 flat on the back, positive onto one side.
            /// This is the field that turns a supine sleeper into a side sleeper, and the reason
            /// the reference sheet's ten shapes collapse to four.
            /// </summary>
            public readonly float Roll;

            /// <summary>How far each upper arm comes in to the side, or up past the head.</summary>
            public readonly float RightArm;
            public readonly float LeftArm;

            /// <summary>Bend left at each elbow, so an arm is not a plank.</summary>
            public readonly float RightElbow;
            public readonly float LeftElbow;

            /// <summary>How far the hips draw up. Negative straightens, positive curls.</summary>
            public readonly float Hip;

            /// <summary>And the knees, which is what makes a side sleeper read as foetal.</summary>
            public readonly float Knee;

            public Posture(string name, float roll, float rightArm, float leftArm,
                float rightElbow, float leftElbow, float hip, float knee)
            {
                Name = name;
                Roll = roll;
                RightArm = rightArm;
                LeftArm = leftArm;
                RightElbow = rightElbow;
                LeftElbow = leftElbow;
                Hip = hip;
                Knee = knee;
            }
        }

        /// <summary>
        /// The four. Two on the back and two on the side, the sides mirrored so a room of sleepers
        /// does not all face the same wall.
        /// </summary>
        public static readonly Posture[] Postures =
        {
            // On the back, arms down. The plainest, and the one a player will read first.
            new Posture("back", 0f, -62f, -62f, 14f, 14f, -10f, 6f),

            // On the back with the arms up behind the head — the sheet's first figure. The elbows
            // carry most of it: arms raised with straight elbows reads as a fall, not a sprawl.
            new Posture("back, arms up", 0f, 118f, 118f, 58f, 58f, -8f, 10f),

            // On one side, knees drawn up. The knee bend is what says foetal rather than felled.
            new Posture("side, curled", 74f, -40f, -74f, 46f, 22f, 26f, 46f),

            // And the other side, less curled, so the two do not read as one posture repeated.
            new Posture("side, loose", -70f, -70f, -38f, 20f, 40f, 14f, 28f),
        };

        /// <summary>
        /// Which posture a colonist sleeps in, from its pawn id.
        ///
        /// <para>Derived rather than stored, and derived from the id rather than from the tick, so
        /// a colonist lies the same way every night and after a load — a sleeper who changed
        /// posture whenever the camera moved would read as a twitch. Mixed rather than taken
        /// modulo directly, because ids are consecutive and four consecutive colonists in a
        /// barracks would otherwise be the four postures in order, every time.</para>
        ///
        /// <para><b>The whole 32-bit avalanche, and it is not belt-and-braces.</b> The first cut
        /// was a Knuth multiply and one shift, which is a perfectly ordinary hash and was wrong
        /// here for a reason worth keeping: the posture is chosen with <c>% 4</c>, so only the
        /// bottom two bits are ever read, and those are exactly the bits a multiply-and-shift
        /// leaves unmixed. Every colonist in the colony came out in posture three — one posture,
        /// which is the morgue this table exists to avoid — and
        /// <c>APostureIsStablePerColonistAndVariesBetweenThem</c> is what caught it.</para>
        /// </summary>
        public static Posture PostureFor(int pawnId)
        {
            uint h = (uint)pawnId;
            h ^= h >> 16;
            h *= 0x85ebca6bu;
            h ^= h >> 13;
            h *= 0xc2b2ae35u;
            h ^= h >> 16;
            return Postures[(int)(h % (uint)Postures.Length)];
        }

        /// <summary>Ease the weight towards where it should be, at <see cref="SettleSeconds"/>.</summary>
        public static float Settle(float current, float target, float deltaTime)
        {
            if (deltaTime <= 0f) return current;
            float step = SettleSeconds > 1e-3f ? deltaTime / SettleSeconds : 1f;
            return Mathf.MoveTowards(current, target, step);
        }

        /// <summary>The breathing phase, 0 to 1, from a clock in seconds.</summary>
        public static float Phase(float clock) => Mathf.Repeat(clock * BreathsPerSecond, 1f);

        /// <summary>How far through the breath the chest is, -1 to 1.</summary>
        public static float Breath(float phase) => Mathf.Sin(phase * 2f * Mathf.PI);

        /// <summary>The length of body to lay down, given the rig's standing hip height.</summary>
        public static float BodyLength(float standingHipHeight) =>
            standingHipHeight > 0.01f ? standingHipHeight * HeightPerHip : 1.8f;

        /// <summary>
        /// Where the figure's root goes and which way it points, to lie along
        /// <paramref name="along"/> with its head at the far end.
        ///
        /// <para><b>The root is the feet</b>, which is what makes this a placement rather than a
        /// rotation about the middle. Laid back through a right angle the body extends
        /// <i>behind</i> the root, so the root belongs at the foot end and the head arrives one
        /// body-length back along the bed — on the pillow, which is the one part of this a player
        /// will actually check.</para>
        ///
        /// <para><paramref name="weight"/> blends the whole thing against the standing pose, so a
        /// colonist lies down and gets up rather than snapping flat.</para>
        /// </summary>
        public static void Place(
            in Posture posture, Vector3 centre, Vector3 along, float surfaceY,
            float standingHipHeight, float weight,
            Vector3 standingPosition, Quaternion standingRotation,
            out Vector3 position, out Quaternion rotation)
        {
            weight = Mathf.Clamp01(weight);

            Vector3 flat = along;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            flat.Normalize();

            float length = BodyLength(standingHipHeight);
            float lift = standingHipHeight > 0.01f ? standingHipHeight * ThicknessPerHip : 0.15f;

            var feet = new Vector3(
                centre.x + flat.x * (length * 0.5f),
                surfaceY + lift,
                centre.z + flat.z * (length * 0.5f));

            Quaternion facing = Quaternion.LookRotation(flat, Vector3.up);

            // Pitched onto the back first, then rolled about the body's own long axis — which is
            // the direction it is lying in, so the roll is taken in the world about `flat` and
            // needs no knowledge of how anybody rigged their character.
            Quaternion lying = Quaternion.AngleAxis(-PitchDegrees, facing * Vector3.right) * facing;
            if (posture.Roll != 0f) lying = Quaternion.AngleAxis(posture.Roll, flat) * lying;

            position = Vector3.Lerp(standingPosition, feet, weight);
            rotation = Quaternion.Slerp(standingRotation, lying, weight);
        }
    }
}
