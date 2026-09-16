#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The shape of an axe stroke, as three angles and nothing else.
    ///
    /// **Why this exists at all.** No pack we own contains a work animation. The locomotion pack
    /// ships idle, walk, run, sprint, crouch, jump, turns and transitions, and not one swing,
    /// strike or lift; the character packs ship no clips of their own. A colonist felling a tree
    /// therefore stood in the idle, breathing, for ten seconds, and then the tree fell over.
    /// Rather than author a clip we cannot author, the pose is computed: every character in the
    /// packs is a Humanoid rig, so the same three angles drive all sixty-one of them, and they
    /// are laid on top of whatever the mixer produced rather than replacing it.
    ///
    /// **Why the angles are here and not in the director.** This is where the mistakes live and
    /// none of them throw. A stroke eased the wrong way reads as a metronome, an arm and a torso
    /// out of step read as a puppet, and a swing that never dwells at the bottom reads as a man
    /// waving. All three are arithmetic, all three are invisible in code review, and none of them
    /// can be caught by anything except a test and a screenshot. Pulled out here at least the
    /// test is possible.
    ///
    /// Angles are degrees of pitch in the figure's own sagittal plane — the plane an axe swings
    /// in. The director applies them about the figure's right-hand axis, which is deliberately
    /// not the bone's local axis: local axes are a property of whoever built the skeleton, and
    /// this way the pose is the same on any rig the packs ever add.
    ///
    /// **The signs are the thing to get right, they are not the same for all three, and only a
    /// photograph settles them.** One rotation, two opposite results, because the bones point
    /// opposite ways: an arm hangs *down*, so a negative pitch carries it forward and then up,
    /// while a spine stands *up*, so the same negative pitch leans it backward and a positive one
    /// folds it forward into the blow. Reading a single convention off the axis name gets one of
    /// the two wrong whichever way you read it, and both wrong versions look entirely reasonable
    /// in source: the first pass had a woodcutter lift an axe over her head and then return it
    /// neatly to her side, and the second had her lean away from her own swing.
    ///
    /// <see cref="Shoulder"/> is the upper arm's pitch **against the world**, not against the
    /// chest: the director subtracts the spine's own pitch from it, so that folding the back
    /// further into the blow does not also swing the arms. That is what makes these three numbers
    /// three independent numbers rather than a chain in which every one moves the others.
    /// </summary>
    public readonly struct WorkSwing
    {
        /// <summary>Pitch on the upper arm. The whole of the swing's reach.</summary>
        public readonly float Shoulder;

        /// <summary>Pitch on the forearm: bent to cock the axe, straight as it lands.</summary>
        public readonly float Elbow;

        /// <summary>Pitch on the spine. Small, and the difference between a person and a doll.</summary>
        public readonly float Spine;

        public WorkSwing(float shoulder, float elbow, float spine)
        {
            Shoulder = shoulder;
            Elbow = elbow;
            Spine = spine;
        }

        /// <summary>The whole pose scaled towards the rest pose, for easing work in and out.</summary>
        public WorkSwing Scaled(float weight) =>
            new WorkSwing(Shoulder * weight, Elbow * weight, Spine * weight);

        // The arm overhead, and the arm at the end of the strike. The range is wide because it
        // starts from an idle in which the arm hangs at the side: getting an axe above the head
        // is most of a half-turn back from there, and the blow finishes with the arm reaching
        // forward and down at the foot of the tree rather than back at the woodcutter's side.
        //
        // Note that the *forearm* is what the axe follows, and that is Shoulder plus Elbow: the
        // blade arrives at the sum of the two, which is why the struck shoulder looks so much
        // smaller than a drawing of the pose would suggest. The idle the swing is laid over also
        // contributes — a Synty character rests with its arms some way forward of straight down —
        // so these are photographed values and not derived ones.
        const float ShoulderRaised = -158f;
        const float ShoulderStruck = -13f;

        // The forearm cocks the axe behind the head and all but straightens at the moment of
        // impact, which is what makes the blade arrive fast rather than the whole arm arrive slow.
        // Not fully straight: a locked elbow reads as a mannequin rather than as a person.
        const float ElbowRaised = -74f;
        const float ElbowStruck = -20f;

        // The body opens up as the axe goes over the head and folds forward into the blow. Only
        // a little over ten degrees each way — enough to read at board-camera height, not so much
        // that a colonist appears to be bowing to the tree. The signs run the other way from the
        // arm's, for the reason given above: a spine stands up where an arm hangs down, so here
        // positive folds forward and negative leans back.
        const float SpineRaised = -12f;
        const float SpineStruck = 20f;

        /// <summary>
        /// How long one stroke takes, in seconds.
        ///
        /// A felling job is ten seconds of work, so this is about nine swings at a tree — an
        /// unhurried woodcutter's rhythm rather than a man attacking it. Slower and the tree
        /// falls between blows; faster and the colony looks frantic.
        /// </summary>
        public const float StrokeSeconds = 1.15f;

        /// <summary>
        /// Where in the stroke a running clock is, 0 to 1.
        ///
        /// <paramref name="offset"/> is a per-pawn constant that keeps two colonists working the
        /// same clearing from swinging on the same frame, for the same reason the walk cycles are
        /// desynchronised.
        /// </summary>
        public static float Phase(float seconds, float offset = 0f)
        {
            float phase = (seconds / StrokeSeconds + offset) % 1f;
            return phase < 0f ? phase + 1f : phase;
        }

        /// <summary>
        /// How far through the stroke the axe is: 0 with it raised, 1 with it in the wood.
        ///
        /// Three unequal parts, and the inequality is the entire point. The raise is long and
        /// eased at both ends, because lifting an axe is deliberate. The strike is short and
        /// accelerating, because a falling axe accelerates. The dwell at the bottom is the beat
        /// in which the blade is buried and the woodcutter is not doing anything at all — take it
        /// out and the motion reads as a metronome rather than as work, which is exactly what a
        /// plain sine wave gives you.
        /// </summary>
        public static float Stroke(float phase)
        {
            const float RaiseEnds = 0.62f;
            const float StrikeEnds = 0.78f;

            if (phase < RaiseEnds)
            {
                // 1 to 0, eased at both ends: the axe comes up and settles at the top.
                float t = phase / RaiseEnds;
                return 1f - t * t * (3f - 2f * t);
            }

            if (phase < StrikeEnds)
            {
                // 0 to 1, accelerating: gravity is doing most of this.
                float t = (phase - RaiseEnds) / (StrikeEnds - RaiseEnds);
                return t * t;
            }

            return 1f;
        }

        /// <summary>The pose at a point in the stroke.</summary>
        public static WorkSwing At(float phase)
        {
            float stroke = Stroke(phase);
            return new WorkSwing(
                Mathf.Lerp(ShoulderRaised, ShoulderStruck, stroke),
                Mathf.Lerp(ElbowRaised, ElbowStruck, stroke),
                Mathf.Lerp(SpineRaised, SpineStruck, stroke));
        }
    }

    /// <summary>
    /// Where a figure stands to work on something, as opposed to where the simulation says it is.
    ///
    /// **Why these are not the same place.** The simulation puts a pawn at the centre of a cell,
    /// and for felling that cell is the tree's own or a neighbour's. Drawn literally, the first
    /// puts a colonist inside the trunk and the second puts her shoulder against it — and in
    /// neither is there room for an axe to travel. Nothing is wrong with the simulation: a cell is
    /// 2.5 m and a person is half a metre, so where in the cell somebody stands was never its
    /// business. It is presentation's, exactly as the glide between cells is.
    ///
    /// So a working figure steps up to a fixed distance from what it is working on and faces it.
    /// The step is eased in by the same weight that eases in the swing, so it reads as setting
    /// oneself rather than as a jump; and it is a *fixed* distance rather than a minimum, so the
    /// picture is the same whether the pawn is standing in the trunk or a cell away from it.
    ///
    /// Nothing else is moved by this. The pawn is still in its cell for picking, for the selection
    /// cursor and for every part of the simulation; only the figure on screen steps in.
    /// </summary>
    public static class WorkStance
    {
        /// <summary>
        /// How far from what it is working on a figure stands, in metres.
        ///
        /// Measured against a colonist drawn at 2.5 m tall with about a metre of reach and a
        /// trunk about half a metre through: far enough that the axe has somewhere to fall,
        /// near enough that it plainly lands on the tree and not in front of it.
        /// </summary>
        public const float StandOff = 1.15f;

        /// <summary>
        /// Where to draw a figure working on <paramref name="workCentre"/>.
        ///
        /// <paramref name="facing"/> is the direction the figure is currently pointed, used only
        /// when the pawn is standing on the very cell it is working on and there is therefore no
        /// direction to be had from the positions alone — it backs off the way it came in.
        /// <paramref name="weight"/> eases the step, 0 leaving the figure exactly where the
        /// simulation put it.
        /// </summary>
        public static Vector3 StandAt(Vector3 position, Vector3 workCentre, Vector3 facing, float weight)
        {
            Vector3 away = position - workCentre;
            away.y = 0f;

            if (away.sqrMagnitude < 1e-4f)
            {
                away = -facing;
                away.y = 0f;
                // Standing in the trunk and facing nowhere at all. Any direction beats none, and
                // this one is at least stable from frame to frame.
                if (away.sqrMagnitude < 1e-4f) away = Vector3.back;
            }

            Vector3 stand = workCentre + away.normalized * StandOff;
            stand.y = position.y;
            return Vector3.Lerp(position, stand, Mathf.Clamp01(weight));
        }
    }
}
