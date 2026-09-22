#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A computed four-legged trot, for a rig that has no walk clip (design 29 §8a;
    /// <c>docs/research/c-procedural-quadruped-gait.md</c>; the owner's note on how a pig's legs
    /// work, 2026-09-22, fifth look). The hog's whole locomotion is one idle clip; this lays a
    /// trot over it, forward-kinematically, about the figure's own right axis, so the same numbers
    /// drive any rig with the four-leg bone convention.
    ///
    /// <para><b>Stance and swing, not a sine.</b> A planted foot sweeps <i>backwards at the
    /// body's own speed</i> for the stance — <see cref="Duty"/> of the cycle — so it holds the
    /// ground, and returns forward in the rest with a quick lift, a flat carry and a sharp plant,
    /// which is the ungulate step the owner described. The hip's angle over the stance is a
    /// straight line from <c>+A</c> to <c>−A</c>, and the stride is not a number anyone typed: it
    /// is the ground one leg covers in its stance, <c>2·L·sin(A)</c>, over the duty factor. With
    /// the legs the rig actually has that is under half a metre a cycle, which at the hog's pace
    /// is the cadence of a trot with no slide factor to make it so.</para>
    ///
    /// <para><b>The whole leg reaches, from the shoulder blade down.</b> The rig carries a root
    /// bone per leg above the upper leg (<c>FrontLeg</c>, <c>BackLeg</c>) at the body's centre
    /// line, which is where a scapula floats and a pelvis rocks; driving it a few degrees in
    /// phase with the hip is what stops a front stride looking pinned at the shoulder. The lower
    /// segments are the carpus and the hock — the joints nine and eleven centimetres up in this
    /// rig — and they fold the way those joints do: the carpus back under the body, the hock
    /// forward, so the two ends of the animal make the "N" the owner drew. The body drops twice a
    /// cycle, lowest at the quarter points where the weight passes over the planted pair, and
    /// nothing rolls or yaws: a pig's spine is stiff and the sway would read as a wobble.</para>
    ///
    /// <para><b>Every driven bone is written from its rest, never pre-multiplied</b>
    /// (<c>bug-patterns.md</c>, 2026-09-22, the rods). The phase is advanced once a frame from the
    /// figure's measured speed (<see cref="Advance"/>) and the pose is applied in the pose pass
    /// (<see cref="Apply"/>), which may run twice a frame.</para>
    /// </summary>
    public sealed class QuadrupedGait
    {
        /// <summary>Fore-aft swing at the upper leg, half amplitude in degrees.</summary>
        public const float HipDegrees = 30f;

        /// <summary>The leg's root bone — the scapula in front — swung in phase with the hip.</summary>
        public const float FrontRootDegrees = 8f;

        /// <summary>And the hip's, at the back: a pelvis rocks less than a shoulder blade slides.</summary>
        public const float BackRootDegrees = 5f;

        /// <summary>Carpus or hock flex at mid-swing, degrees, signed per leg by <see cref="KneeSign"/>.</summary>
        public const float KneeDegrees = 40f;

        /// <summary>The share of the cycle a foot is planted. A trot is near a half; a little over reads as weight.</summary>
        public const float Duty = 0.6f;

        /// <summary>Body drop, metres, twice a cycle, lowest at the quarter points.</summary>
        public const float BobMetres = 0.012f;

        /// <summary>
        /// Left as a dial and set to one: the stride is geometric now and needs no slide. Above
        /// one the feet slide and the cadence slows; below, the animal scurries.
        /// </summary>
        public const float SlideFactor = 1f;

        /// <summary>Below this the legs ease back to the clip's pose rather than stepping on the spot.</summary>
        public const float StandingSpeed = 0.05f;

        /// <summary>How quickly the gait fades in and out around <see cref="StandingSpeed"/>.</summary>
        public const float EaseSeconds = 0.25f;

        /// <summary>The legs: left hind, left fore, right hind, right fore.</summary>
        static readonly string[] Sides = { "L", "L", "R", "R" };
        static readonly string[] Ends = { "Back", "Front", "Back", "Front" };

        /// <summary>A trot: the diagonal pairs (left fore with right hind, right fore with left hind) half a cycle apart.</summary>
        public static readonly float[] Offsets = { 0f, 0.5f, 0.5f, 0f };

        /// <summary>
        /// Which way each leg's lower segment folds to clear the ground, as a sign on
        /// <see cref="Pitch"/>: the hock tucks the hind foot <i>forward</i> under the belly, the
        /// carpus tucks the fore foot <i>back</i> under the elbow. A positive pitch about the
        /// figure's right swings a hanging leg backwards (Unity's left hand: up goes to forward,
        /// forward goes to down), so the hind is negative and the fore positive. The first
        /// version had both the other way, and the corrected probe read the hind sole three
        /// centimetres under the ground at mid-swing.
        /// </summary>
        public static readonly float[] KneeSign = { -1f, +1f, -1f, +1f };

        /// <summary>The pitch, about the figure's right, that carries a hanging leg forward by one degree.</summary>
        public const float ForwardSign = -1f;

        readonly Transform?[] _root = new Transform?[4];
        readonly Transform?[] _upper = new Transform?[4];
        readonly Transform?[] _lower = new Transform?[4];
        readonly Quaternion[] _rootRest = new Quaternion[4];
        readonly Quaternion[] _upperRest = new Quaternion[4];
        readonly Quaternion[] _lowerRest = new Quaternion[4];
        readonly Transform? _body;
        readonly Vector3 _bodyRest;

        /// <summary>Shoulder or hip joint to sole, metres as drawn, averaged over the four legs.</summary>
        public float LegMetres { get; }

        /// <summary>Metres the body travels per cycle, as drawn: the stance's reach over the duty factor.</summary>
        public float Stride { get; }

        /// <summary>Where in the cycle the legs are, 0..1.</summary>
        public float Phase { get; private set; }

        /// <summary>How much of the gait is laid over the clip, 0..1: up while moving, down while standing.</summary>
        public float Weight { get; private set; }

        QuadrupedGait(Transform?[] root, Transform?[] upper, Transform?[] lower, Transform? body, float legMetres)
        {
            for (int i = 0; i < 4; i++)
            {
                _root[i] = root[i];
                _upper[i] = upper[i];
                _lower[i] = lower[i];
                _rootRest[i] = root[i] != null ? root[i]!.localRotation : Quaternion.identity;
                _upperRest[i] = upper[i] != null ? upper[i]!.localRotation : Quaternion.identity;
                _lowerRest[i] = lower[i] != null ? lower[i]!.localRotation : Quaternion.identity;
            }
            _body = body;
            _bodyRest = body != null ? body.localPosition : Vector3.zero;
            LegMetres = legMetres;
            // The whole leg sweeps by the hip plus its root, so that is the angle the reach uses.
            float sweep = (HipDegrees + 0.5f * (FrontRootDegrees + BackRootDegrees)) * Mathf.Deg2Rad;
            Stride = 2f * legMetres * Mathf.Sin(sweep) / Duty * SlideFactor;
        }

        /// <summary>
        /// Find the four legs by the rig's bone names, measure them and remember their rest, or
        /// null if the rig has none of them — an animal whose art is a different convention simply
        /// moves on its idle, as a colonist with no bound arms swings no axe. The root bone above
        /// each upper leg is optional: a rig without one has no scapula to float. Bind with the
        /// rig in the pose the gait should rest on: a fresh instance, or one the idle has been
        /// evaluated on to once.
        /// </summary>
        public static QuadrupedGait? Bind(Transform root)
        {
            if (root == null) return null;
            var roots = new Transform?[4];
            var upper = new Transform?[4];
            var lower = new Transform?[4];
            float legs = 0f;
            int bound = 0;
            for (int i = 0; i < 4; i++)
            {
                roots[i] = Find(root, Ends[i] + "Leg." + Sides[i]);
                upper[i] = Find(root, Ends[i] + "UpLeg." + Sides[i]);
                lower[i] = Find(root, Ends[i] + "LowLeg." + Sides[i]);
                Transform? foot = Find(root, Ends[i] + "Foot." + Sides[i]);
                if (upper[i] == null || foot == null) continue;
                legs += Mathf.Abs(upper[i]!.position.y - foot.position.y);
                bound++;
            }
            if (bound < 4) return null;
            float legMetres = legs / bound;
            if (legMetres < 0.01f) return null;
            return new QuadrupedGait(roots, upper, lower, Find(root, "Body"), legMetres);
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

        /// <summary>Step the cycle on by what the figure moved this frame. Once a frame, never in a pose pass.</summary>
        public void Advance(float metresPerSecond, float deltaTime)
        {
            bool moving = metresPerSecond > StandingSpeed;
            float step = EaseSeconds > 1e-3f ? deltaTime / EaseSeconds : 1f;
            Weight = Mathf.MoveTowards(Weight, moving ? 1f : 0f, step);
            if (!moving || deltaTime <= 0f) return;
            Phase = Mathf.Repeat(Phase + metresPerSecond * deltaTime / Stride, 1f);
        }

        /// <summary>
        /// The leg's fore-aft reach at a phase, as a fraction of its amplitude: <c>+1</c> is fully
        /// forward, where the foot has just planted. Planted from 0 to <see cref="Duty"/> and
        /// sweeping back in a straight line, so the foot holds the ground while the body passes
        /// over it; then the swing brings it forward again, eased at both ends, so the lift is
        /// not a kick and the plant is not a bounce.
        /// </summary>
        public static float HipAt(float p)
        {
            p = Mathf.Repeat(p, 1f);
            if (p < Duty) return 1f - 2f * (p / Duty);
            float s = (p - Duty) / (1f - Duty);
            return -1f + 2f * Mathf.SmoothStep(0f, 1f, s);
        }

        /// <summary>
        /// The carpus or hock flex at a phase, 0..1: nought through the whole stance, and through
        /// the swing a quick lift, a flat carry and a sharp plant — the fold rises over the first
        /// third of the swing, holds, and is back to nought as the foot lands.
        /// </summary>
        public static float FlexAt(float p)
        {
            p = Mathf.Repeat(p, 1f);
            if (p < Duty) return 0f;
            float s = (p - Duty) / (1f - Duty);
            if (s < 0.33f) return Mathf.SmoothStep(0f, 1f, s / 0.33f);
            if (s < 0.75f) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (s - 0.75f) / 0.25f);
        }

        /// <summary>
        /// Write the legs for the current phase, from their rest pose. Idempotent: every bone is
        /// reset to the rotation it had at <see cref="Bind"/> and then pitched, so neither a second
        /// pass in the same frame nor a clip that never writes the bone can change the answer.
        /// </summary>
        public void Apply(Vector3 right, Vector3 up)
        {
            if (Weight <= 0.001f) return;
            for (int i = 0; i < 4; i++)
            {
                float p = Mathf.Repeat(Phase + Offsets[i], 1f);
                float reach = HipAt(p) * Weight * ForwardSign;
                float rootDegrees = (Ends[i] == "Front" ? FrontRootDegrees : BackRootDegrees) * reach;
                float flex = KneeSign[i] * KneeDegrees * FlexAt(p) * Weight;
                Pitch(_root[i], _rootRest[i], right, rootDegrees);
                Pitch(_upper[i], _upperRest[i], right, HipDegrees * reach);
                Pitch(_lower[i], _lowerRest[i], right, flex);
            }
            if (_body != null)
            {
                // Highest as a diagonal pair lands (0 and a half), lowest at the quarter points
                // where the weight passes over the planted pair.
                float drop = BobMetres * 0.5f * (1f - Mathf.Cos(Phase * Mathf.PI * 4f)) * Weight;
                _body.localPosition = _bodyRest;
                _body.position -= up * drop;
            }
        }

        static void Pitch(Transform? bone, Quaternion rest, Vector3 axis, float degrees)
        {
            if (bone == null) return;
            bone.localRotation = rest;
            if (Mathf.Abs(degrees) < 1e-4f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
