#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A computed four-legged walk, for a rig that has no walk clip (design 29 §3 of the plan;
    /// <c>docs/research/c-procedural-quadruped-gait.md</c>). The hog's whole locomotion is one
    /// idle clip; this lays a lateral-sequence walk over it, in the manner <c>WorkSwing</c> lays
    /// an axe stroke over a colonist's idle: forward-kinematic sines on the hip and knee of each
    /// leg, written after the mixer has played, about the figure's own right axis so the same
    /// numbers drive any rig with the four-leg bone convention.
    ///
    /// <para><b>The phase is advanced once a frame from the figure's measured speed</b>
    /// (<see cref="Advance"/>) and the angles are applied in the idempotent pose pass
    /// (<see cref="Apply"/>), which may run twice a frame; splitting the two is what keeps a
    /// second pass from taking a second step. Feet do not slide because the cycle is one stride
    /// per <see cref="Stride"/> metres walked, whatever the speed.</para>
    ///
    /// <para>The numbers are the research's starting table and are playtest numbers: phases
    /// 0, ¼, ½, ¾ for left hind, left fore, right hind, right fore; hip ±25°; knee 35° peaking
    /// mid-swing; a bob of a few centimetres at twice the stride rate. Duty factor is the sine's
    /// 0.5 rather than the research's 0.65, which is the first thing to change if the walk reads
    /// as a trot.</para>
    /// </summary>
    public sealed class QuadrupedGait
    {
        public const float HipDegrees = 25f;
        public const float KneeDegrees = 35f;
        public const float BobMetres = 0.02f;

        /// <summary>Below this the legs ease back to the clip's pose rather than stepping on the spot.</summary>
        public const float StandingSpeed = 0.05f;

        /// <summary>How quickly the walk fades in and out around <see cref="StandingSpeed"/>.</summary>
        public const float EaseSeconds = 0.25f;

        /// <summary>The four legs, in lateral sequence: left hind, left fore, right hind, right fore.</summary>
        static readonly float[] Offsets = { 0f, 0.25f, 0.5f, 0.75f };

        readonly Transform?[] _upper = new Transform?[4];
        readonly Transform?[] _lower = new Transform?[4];
        readonly Transform? _body;

        /// <summary>Metres walked per cycle, as drawn.</summary>
        public float Stride { get; }

        /// <summary>Where in the cycle the legs are, 0..1.</summary>
        public float Phase { get; private set; }

        /// <summary>How much of the walk is laid over the clip, 0..1: up while walking, down while standing.</summary>
        public float Weight { get; private set; }

        QuadrupedGait(Transform?[] upper, Transform?[] lower, Transform? body, float stride)
        {
            for (int i = 0; i < 4; i++) { _upper[i] = upper[i]; _lower[i] = lower[i]; }
            _body = body;
            Stride = stride;
        }

        /// <summary>
        /// Find the four legs by the rig's bone names, or null if the rig has none of them — an
        /// animal whose art is a different convention simply walks on its idle, as a colonist
        /// with no bound arms simply never swings an axe.
        /// </summary>
        public static QuadrupedGait? Bind(Transform root, float stride)
        {
            if (root == null || stride <= 0.01f) return null;
            var upper = new Transform?[4];
            var lower = new Transform?[4];
            string[] sides = { "L", "L", "R", "R" };
            string[] ends = { "Back", "Front", "Back", "Front" };
            int bound = 0;
            for (int i = 0; i < 4; i++)
            {
                upper[i] = Find(root, ends[i] + "UpLeg." + sides[i]);
                lower[i] = Find(root, ends[i] + "LowLeg." + sides[i]);
                if (upper[i] != null) bound++;
            }
            if (bound < 4) return null;
            return new QuadrupedGait(upper, lower, Find(root, "Body"), stride);
        }

        static Transform? Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform? found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Step the cycle on by what the figure walked this frame. Once a frame, never in a pose pass.</summary>
        public void Advance(float metresPerSecond, float deltaTime)
        {
            bool walking = metresPerSecond > StandingSpeed;
            float step = EaseSeconds > 1e-3f ? deltaTime / EaseSeconds : 1f;
            Weight = Mathf.MoveTowards(Weight, walking ? 1f : 0f, step);
            if (!walking || deltaTime <= 0f) return;
            Phase = Mathf.Repeat(Phase + metresPerSecond * deltaTime / Stride, 1f);
        }

        /// <summary>
        /// Lay the walk over whatever the mixer wrote. Idempotent for a given phase: every angle
        /// is derived from <see cref="Phase"/> and pre-multiplied onto the clip's pose, so a
        /// second pass on a freshly written skeleton gives the same legs.
        /// </summary>
        public void Apply(Vector3 right, Vector3 up)
        {
            if (Weight <= 0.001f) return;
            for (int i = 0; i < 4; i++)
            {
                float p = Mathf.Repeat(Phase + Offsets[i], 1f);
                float hip = HipDegrees * Mathf.Sin(p * Mathf.PI * 2f) * Weight;
                // The foot is off the ground while the hip swings forward — the half-cycle centred
                // on the forward peak — and the knee flexes there, foot toward the tail, so it
                // clears the ground. Zero through the stance.
                float flex = -KneeDegrees * Mathf.Max(0f, Mathf.Cos(p * Mathf.PI * 2f)) * Weight;
                Pitch(_upper[i], right, hip);
                Pitch(_lower[i], right, flex);
            }
            if (_body != null)
            {
                float bob = BobMetres * 0.5f * (1f - Mathf.Cos(Phase * Mathf.PI * 4f)) * Weight;
                _body.position += up * bob;
            }
        }

        static void Pitch(Transform? bone, Vector3 axis, float degrees)
        {
            if (bone == null) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
