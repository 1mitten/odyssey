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
    ///
    /// <para><b>A sleeper does not move at all</b> (owner, 2026-09-18: "when they are sleeping -
    /// they should be static and not animated. Still in that position"). There is no breath and no
    /// cycle here, and the idle clip underneath is held on one frame — see
    /// <c>PawnFigureDirector.Evaluate</c>, which is what actually stops it. This class is
    /// therefore the only pose in the folder that is a <i>position</i> rather than a motion: it has
    /// no clock and no phase, which is why it is also the only one with nothing to hold a
    /// harness to.</para>
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
        /// Half a torso's thickness, front to back, as a fraction of the body's own length. What a
        /// sleeper on its back floats above the surface.
        ///
        /// <para><b>Measured off the cast rather than off a person.</b> The figure lying on its
        /// back is 0.59 m through the chest on a body 2.58 m long, so half of it is 0.135 of the
        /// length — these characters are stylised and stockier than the human proportion the first
        /// numbers came from. Laid at the human figure the body sank 0.13 m into the mattress, and
        /// "sunk" is a word the owner has already used about this pose once
        /// (<c>docs/design/20-beds.md</c> §7b). <c>scripts/unity.sh exec
        /// Odyssey.EditorTools.SleepProbe.Run</c> prints the clearance for every posture.</para>
        /// </summary>
        public static float ThicknessPerBody { get; set; } = 0.135f;

        /// <summary>
        /// Half a torso's width, shoulder to shoulder, as a fraction of the body's own length.
        ///
        /// <para><b>This is what stops a side sleeper sinking.</b> Rolled on to its side a body
        /// presents its width to the mattress rather than its thickness, and the width is half as
        /// much again — so a lift computed from thickness alone buried the shoulder and the hip in
        /// the bed (owner, 2026-09-18: "sunk"). <see cref="Lift"/> takes whichever the roll
        /// actually presents.</para>
        ///
        /// <para>Measured the same way and for the same reason as its neighbour: 0.152 of the
        /// body's length is what puts the drawn shoulder, hip and drawn-up knee of both side
        /// postures on the mattress rather than through it.</para>
        /// </summary>
        public static float ShoulderPerBody { get; set; } = 0.152f;

        /// <summary>
        /// How far the body's middle floats above what it lies on, for a posture at a given roll.
        ///
        /// <para>The half-height of a box of the body's thickness and width, turned through the
        /// roll: flat on the back it is the thickness, full on the side it is the width, and
        /// between the two it is what the rotation gives. There is no fudge in it, which is why it
        /// is right for a posture nobody has drawn yet.</para>
        ///
        /// <para><b>A fraction of the body's length, not of a hip.</b> The two fractions used to
        /// be 0.16 and 0.24 of a hip height that was supposed to be a little over half the figure,
        /// and the hip the director handed over was the 0.2 m floor of a clamp — so the lift came
        /// out at 32 mm and the colonist lay inside the bedding. These are taken of the body's own
        /// measured length, and their values are measured too: what actually puts the drawn mesh
        /// on the mattress rather than what a human being's proportions would suggest.</para>
        /// </summary>
        public static float Lift(in Posture posture, float bodyLength)
        {
            float body = BodyLength(bodyLength);
            float roll = posture.Roll * Mathf.Deg2Rad;
            return body * (ThicknessPerBody * Mathf.Abs(Mathf.Cos(roll))
                           + ShoulderPerBody * Mathf.Abs(Mathf.Sin(roll)));
        }

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

            /// <summary>
            /// How far each upper arm swings <b>out from the body's midline</b>, in degrees.
            ///
            /// <para><b>The second axis, and it is what "arms up behind the head" needed.</b> Every
            /// other angle in this struct is a pitch about the body's own lateral axis, which moves
            /// a limb in the plane that runs head to foot. No amount of it can pull an arm in
            /// <i>towards</i> the midline: whatever spread the idle clip already holds is carried
            /// round with the arm, so the posture that is named for putting the hands over the
            /// crown put them out sideways at something near 45° instead, and read as surrendering
            /// rather than as asleep (the contact sheet, 2026-09-20, <c>docs/design/20-beds.md</c>
            /// §7b). This is taken about the body's <i>forward</i> axis — which on a sleeper on her
            /// back is the vertical — so it swings the arm in the plane of the mattress, between
            /// out across the bed and in along it.</para>
            ///
            /// <para>Nought on every posture that does not ask for it, so the three that were
            /// measured right are untouched by its existence.</para>
            /// </summary>
            public readonly float RightArmOut;
            public readonly float LeftArmOut;

            public Posture(string name, float roll, float rightArm, float leftArm,
                float rightElbow, float leftElbow, float hip, float knee,
                float rightArmOut = 0f, float leftArmOut = 0f)
            {
                Name = name;
                Roll = roll;
                RightArm = rightArm;
                LeftArm = leftArm;
                RightElbow = rightElbow;
                LeftElbow = leftElbow;
                Hip = hip;
                Knee = knee;
                RightArmOut = rightArmOut;
                LeftArmOut = leftArmOut;
            }
        }

        /// <summary>
        /// The four. Two on the back and two on the side, the sides mirrored so a room of sleepers
        /// does not all face the same wall.
        ///
        /// <para><b>The two supine arm angles were measured on 2026-09-20 and both were wrong, in
        /// opposite directions.</b> A positive pitch here swings a supine sleeper's arm
        /// <i>downward</i>, through the bedding, and a negative one raises it; the first cut had it
        /// the other way about. Measured against the drawn mesh, with the body's own top 0.66 m
        /// above the mattress, sweeping the arm through its whole arc
        /// (<c>scripts/unity.sh exec Odyssey.EditorTools.SleepProbe.Run</c>):</para>
        ///
        /// <list type="bullet">
        /// <item><description><b>"back"</b> was <c>-62°</c>, which held both arms 0.55 m in the air
        /// above a colonist lying flat on her back. It is <c>-10°</c>, in the band −170° to +10°
        /// where the arms lie level with the body and touch the mattress.</description></item>
        /// <item><description><b>"back, arms up"</b> was <c>+118°</c> with a <c>+58°</c> elbow, which
        /// drove both forearms 0.54 m <i>through</i> the mattress and out past the head of the bed.
        /// It is <c>-150°</c> with a <c>-15°</c> elbow: arms stretched above the head, flat, ending
        /// 0.42 m past the crown and still 0.34 m inside the bed's own frame.</description></item>
        /// </list>
        ///
        /// <para>The two side postures were measured in the same pass and were already right — their
        /// limbs sit 0.07 m to 0.10 m above the body's own top and nothing dips below the
        /// mattress — so they are untouched. <c>docs/design/20-beds.md</c> §7b.</para>
        ///
        /// <para><b>And then the contact sheet said "arms up" still read as arms <i>out</i></b>, at
        /// something near 45° from above, which is surrendering rather than sleeping. The pictures
        /// were right and the reason was not what it looked like: measured, the arms were never off
        /// the bed at all — the posture spans 1.06 m across a frame 2.00 m wide, the same as "back"
        /// — they simply lay out to the sides instead of over the crown, and no pitch about the
        /// lateral axis can pull them in. <see cref="Posture.RightArmOut"/> is the second angle that
        /// can. Swept through its arc, +15° (mirrored) is both the narrowest the arms get, 1.06 m
        /// down to 0.71 m, and the furthest they reach past the head, so it is the one place on the
        /// sweep where tucking them in costs nothing.</para>
        /// </summary>
        public static readonly Posture[] Postures =
        {
            // On the back, arms down. The plainest, and the one a player will read first.
            new Posture("back", 0f, -10f, -10f, 15f, 15f, -10f, 6f),

            // On the back with the arms up behind the head — the sheet's first figure. The elbow
            // opens the forearm back down on to the bedding, so the hands rest above the crown
            // rather than standing off it; measured, that is where the reach past the head comes
            // from and where the clearance stays at a centimetre rather than going negative.
            new Posture("back, arms up", 0f, -150f, -150f, -15f, -15f, -8f, 10f, 15f, -15f),

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

        /// <summary>
        /// The length of body to lay down: the figure's own drawn height, because a body lying
        /// down is exactly as long as it is tall standing up.
        ///
        /// <para><b>There is no ratio here any more, and that is the fix.</b> It used to be
        /// <c>standingHipHeight * 1.9</c> — a hip is a little over half a person, so the
        /// reciprocal turns one into the other — and the hip the director handed it was
        /// <c>hips.position.y - transform.position.y</c> on a rig whose humanoid <c>Hips</c> is a
        /// bone named <c>Root</c> sitting at the model origin. Nought, on every one of the
        /// sixty-one, clamped up to 0.2 m, so a 2.49 m colonist was laid down 0.38 m long: her
        /// feet went on the pillow and the remaining 2.1 m of her hung off the head end of the
        /// bed and on to the floor. That is what the owner reported twice
        /// (<c>docs/design/20-beds.md</c> §7b), and it survived the first fix because the
        /// arithmetic that was read and pronounced correct was correct — it was being fed a
        /// number that was not a length.</para>
        ///
        /// <para>So the caller measures the body and hands it over, <see cref="FigureBuild"/> does
        /// the measuring off the posed mesh, and the only thing left here is the guard: a length
        /// that is plainly not one falls back on a colonist at the drawn scale rather than laying
        /// somebody out along three cells of bed.</para>
        /// </summary>
        public static float BodyLength(float measured) =>
            measured >= 0.5f && measured <= 5f ? measured : 2.5f;

        /// <summary>
        /// Where the figure's root goes and which way it points, to lie along
        /// <paramref name="along"/> with its head at <paramref name="headAt"/>.
        ///
        /// <para><b>The head is given and the feet are worked out</b>, not the other way round and
        /// not from the middle. The head is the end that has to land somewhere exact — on the
        /// pillow — and the feet may fall wherever a body of that length puts them, because this
        /// bed is two and a half times a colonist's length and nothing is watching its foot end.
        /// Centring the body on the bed instead is what left the head adrift in the middle of the
        /// mattress (owner, 2026-09-18).</para>
        ///
        /// <para><b>The root is the feet</b>, which is what makes this a placement rather than a
        /// rotation about the middle: laid back through a right angle the body extends
        /// <i>behind</i> the root, so the root goes one body-length along the bed from the head.</para>
        ///
        /// <para><b>And it lies along what it is on, not level across it.</b> Everything fixed to
        /// the grid is <i>draped</i> — <c>BedShape.Root</c> is <c>GroundRelief.Drape(...)</c>, which
        /// shears a bed's 4.6 m along the ground's tangent plane — while this used to take one
        /// height and lay the body flat on it. Measured: at the relief's steepest (2.0 m over a
        /// 150 m period, 0.136 rise per metre) that is 0.21 m of disagreement at the pillow and
        /// 0.09 m the other way at the feet, so a colonist on sloping ground was buried in the
        /// mattress at one end and floating above it at the other. <paramref name="surfaceY"/> is
        /// now the surface under the <i>head</i> and <paramref name="alongSlope"/> is its gradient
        /// along the bed, which between them describe the plane rather than a point on it.</para>
        ///
        /// <para><paramref name="weight"/> blends the whole thing against the standing pose, so a
        /// colonist lies down and gets up rather than snapping flat.</para>
        /// </summary>
        /// <param name="surfaceY">The height of the surface beneath <paramref name="headAt"/>.</param>
        /// <param name="alongSlope">Its rise per metre along <paramref name="along"/>: nought on
        /// the level, positive where the foot of the bed is higher than its head.</param>
        public static void Place(
            in Posture posture, Vector3 headAt, Vector3 along, float surfaceY,
            float bodyLength, float alongSlope, float weight,
            Vector3 standingPosition, Quaternion standingRotation,
            out Vector3 position, out Quaternion rotation)
        {
            weight = Mathf.Clamp01(weight);

            Vector3 flat = along;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            flat.Normalize();

            float length = BodyLength(bodyLength);
            float lift = Lift(posture, bodyLength);

            // **The surface the body lies on is a plane, not a height** (see the parameter). The
            // head is given its own point on that plane by the caller; the feet are one body-length
            // along it, which is `alongSlope` metres higher for every metre travelled.
            var feet = new Vector3(
                headAt.x + flat.x * length,
                surfaceY + lift + alongSlope * length,
                headAt.z + flat.z * length);

            Quaternion facing = Quaternion.LookRotation(flat, Vector3.up);

            // Pitched onto the back, and a little further so the body lies *along* what it is on
            // rather than level across it. A right angle exactly is a body on a level mattress;
            // the slope is added to it, so a bed drawn tilted carries its sleeper tilted with it.
            float slopeDegrees = Mathf.Atan(alongSlope) * Mathf.Rad2Deg;
            Quaternion lying =
                Quaternion.AngleAxis(-(PitchDegrees + slopeDegrees), facing * Vector3.right) * facing;

            // Then rolled about the body's **own** long axis, which is now the tilted one. Taking
            // the roll about `flat` was right while the body was level and is a few degrees out
            // once it is not — and the axis is free, because the pitch above has just produced it.
            // Negated so that the sense of Roll is what it always was: `lying * up` runs feet to
            // head, which is the opposite of the direction the body is laid along.
            if (posture.Roll != 0f)
                lying = Quaternion.AngleAxis(posture.Roll, -(lying * Vector3.up)) * lying;

            position = Vector3.Lerp(standingPosition, feet, weight);
            rotation = Quaternion.Slerp(standingRotation, lying, weight);
        }
    }
}
