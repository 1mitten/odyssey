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
    /// **The sign is the thing to get right, and the photographs settled it.** A limb at rest
    /// hangs down, and a *negative* pitch about that axis carries it forward and then up. So both
    /// ends of the arm's swing are negative — far negative overhead, less negative reaching
    /// forward at the block — and the first pass, which read "positive is forward" off the axis
    /// name, produced a colonist who lifted an axe over her head and then returned it neatly to
    /// her side, and a torso that leaned away from its own blow.
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
        const float ShoulderRaised = -158f;
        const float ShoulderStruck = -52f;

        // The forearm cocks the axe behind the head and all but straightens at the moment of
        // impact, which is what makes the blade arrive fast rather than the whole arm arrive slow.
        // Not fully straight: a locked elbow reads as a mannequin rather than as a person.
        const float ElbowRaised = -74f;
        const float ElbowStruck = -14f;

        // The body opens up as the axe goes over the head and folds forward into the blow. Only
        // a little over ten degrees each way — enough to read at board-camera height, not so much
        // that a colonist appears to be bowing to the tree. Note the signs run the other way from
        // the arm's: the spine starts from upright rather than from hanging down, so leaning back
        // is positive and folding forward is negative.
        const float SpineRaised = 8f;
        const float SpineStruck = -18f;

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
}
