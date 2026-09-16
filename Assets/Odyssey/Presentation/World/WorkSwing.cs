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
    /// **The angles themselves moved to <see cref="WorkStroke"/>** when mining needed a stroke of
    /// its own: they were <c>const</c> here, which meant the build could hold exactly one stroke.
    /// What is left is the pose — three angles and a scale — which is the same shape whatever is
    /// being swung.
    ///
    /// **Why the angles are not in the director.** This is where the mistakes live and
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
        /// The closest a figure will ever be put to what it is working on, in metres.
        ///
        /// The stand is solved from a pose that is still being tuned by eye, and a bad set of
        /// angles could solve to no distance at all and stand a colonist inside the trunk. Half a
        /// metre is about a person's own width; nearer than that is a bug whatever the arithmetic
        /// says, and the blow lands short rather than the figure ending up in the tree.
        /// </summary>
        public const float MinimumStandOff = 0.5f;

        /// <summary>
        /// Where to draw a figure so that its blade lands on <paramref name="workCentre"/>.
        ///
        /// **Why this is solved rather than measured off as a distance.** The first version stood
        /// the figure at its own reach, taken as the length of the line from its feet to the edge
        /// of its axe at the moment of the blow. That is the right number for a swing that comes
        /// straight down in front, and the wrong one for a swing that comes over the shoulder: the
        /// edge ends up a metre and a half away, but a good part of that is *sideways*, so the axe
        /// arrives beside the tree rather than in it. Reach is not a scalar once the swing is
        /// diagonal.
        ///
        /// <paramref name="strike"/> is therefore the whole offset from the figure's feet to its
        /// edge, turned to face the way the figure is facing, and the stand is simply wherever
        /// puts that offset's far end in the wood. It stays right when the swing's tilt changes,
        /// when the angles are retuned, and for a colonist scaled differently from the rest.
        ///
        /// <paramref name="facing"/> is used only when the pawn stands on the very cell it is
        /// working on and the positions give no direction to approach from. <paramref name="weight"/>
        /// eases the step, 0 leaving the figure exactly where the simulation put it.
        /// </summary>
        /// <param name="aimFromCentre">
        /// How far in from the centre of the work, towards the worker, the head should finish —
        /// <see cref="WorkStyle.AimFromCentre"/>. It used to be a constant 0.15 m, which is the
        /// middle of a tree trunk and 1.1 m inside a 2.5 m block of stone.
        /// </param>
        public static Vector3 StandAt(Vector3 position, Vector3 workCentre, Vector3 facing,
            float weight, Vector3 strike, float aimFromCentre)
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
            away.Normalize();

            // Aim the edge into the wood, then put the figure wherever its own strike offset
            // reaches that point from.
            Vector3 target = workCentre + away * aimFromCentre;
            Vector3 stand = target - new Vector3(strike.x, 0f, strike.z);

            Vector3 gap = stand - workCentre;
            gap.y = 0f;
            if (gap.sqrMagnitude < MinimumStandOff * MinimumStandOff)
                stand = workCentre + away * MinimumStandOff;

            stand.y = position.y;
            return Vector3.Lerp(position, stand, Mathf.Clamp01(weight));
        }
    }
}
