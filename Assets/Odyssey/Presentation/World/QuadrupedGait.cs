#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A computed four-legged gait, for a rig that has no walk clip (design 29 §8a;
    /// <c>docs/research/c-procedural-quadruped-gait.md</c>). The hog's whole locomotion is one
    /// idle clip; this lays a <b>trot</b> over it: forward-kinematic sines on the hip and the knee
    /// of each leg, about the figure's own right axis so the same numbers drive any rig with the
    /// four-leg bone convention.
    ///
    /// <para><b>The legs are set from their rest pose, never pre-multiplied onto whatever is
    /// there</b> (owner, 2026-09-22, second look: <i>"the legs are spindles"</i>). The first
    /// version multiplied a pitch onto each bone's current rotation, the way the colonists' work
    /// pose does — which is safe there only as long as the clip underneath rewrites every bone
    /// before every pass, and under the game's own loop, with the idle held at speed nought under
    /// the gait, the hog's legs came out as rods. Every bone this gait drives is captured at
    /// <see cref="Bind"/> and <see cref="Apply"/> writes its pose <i>absolutely</i> from that rest,
    /// so nothing about what the clip did or did not write that frame can reach the answer:
    /// <c>AnimalProbe.ShootMoving</c> runs the hog under the director's real animator for three
    /// seconds and its leg lengths hold to the millimetre.</para>
    ///
    /// <para><b>Why a trot and why the stride is measured</b> (owner, 2026-09-22, first look). The
    /// rig's legs are 23 cm from shoulder joint to sole on a 1.2 m body; a walk cycling once per
    /// authored metre slid the feet over most of every stride. A short-legged animal at speed
    /// trots, on diagonal pairs, with quick short steps; the stride is derived from the leg the rig
    /// actually has — twice the leg times the sine of the hip swing, the ground one leg covers in
    /// its stance — and <see cref="SlideFactor"/> admits that a model this squat must either
    /// scurry or slide, and splits it.</para>
    ///
    /// <para>The phase is advanced once a frame from the figure's measured speed
    /// (<see cref="Advance"/>) and the angles are applied in the pose pass (<see cref="Apply"/>).
    /// The knees are signed by anatomy: a fore leg folds its lower segment back under the body in
    /// the swing, a hind leg's hock flexes the foot forward.</para>
    /// </summary>
    public sealed class QuadrupedGait
    {
        /// <summary>Hip fore-aft swing, half amplitude in degrees.</summary>
        public const float HipDegrees = 40f;

        /// <summary>Knee flex at mid-swing, degrees, signed per leg by <see cref="KneeSign"/>.</summary>
        public const float KneeDegrees = 35f;

        /// <summary>Body bob, metres, twice per cycle: once per diagonal pair landing.</summary>
        public const float BobMetres = 0.01f;

        /// <summary>
        /// How much further the body travels per cycle than the legs geometrically cover. 1 is
        /// no sliding at all and a squat rig scurrying at three or four cycles a second. The
        /// cadence it gives with the swing above is about 1.7 cycles a second at the hog's pace;
        /// the swing was widened from 28° to 40° and this lowered to keep that cadence, because
        /// the owner saw "twisting in one spot" rather than legs (2026-09-22, third look) — the
        /// limbs must be seen to move from the play camera, and 10 cm of foot travel was not.
        /// A playtest number.
        /// </summary>
        public const float SlideFactor = 1.6f;

        /// <summary>Below this the legs ease back to the clip's pose rather than stepping on the spot.</summary>
        public const float StandingSpeed = 0.05f;

        /// <summary>How quickly the gait fades in and out around <see cref="StandingSpeed"/>.</summary>
        public const float EaseSeconds = 0.25f;

        /// <summary>The legs: left hind, left fore, right hind, right fore.</summary>
        static readonly string[] Sides = { "L", "L", "R", "R" };
        static readonly string[] Ends = { "Back", "Front", "Back", "Front" };

        /// <summary>A trot: the diagonal pairs (left fore with right hind, right fore with left hind) half a cycle apart.</summary>
        public static readonly float[] Offsets = { 0f, 0.5f, 0.5f, 0f };

        /// <summary>Which way each leg's lower segment folds to clear the ground: hind forward, fore back.</summary>
        public static readonly float[] KneeSign = { +1f, -1f, +1f, -1f };

        readonly Transform?[] _upper = new Transform?[4];
        readonly Transform?[] _lower = new Transform?[4];
        readonly Quaternion[] _upperRest = new Quaternion[4];
        readonly Quaternion[] _lowerRest = new Quaternion[4];
        readonly Transform? _body;
        readonly Vector3 _bodyRest;

        /// <summary>Shoulder or hip joint to sole, metres as drawn, averaged over the four legs.</summary>
        public float LegMetres { get; }

        /// <summary>Metres the body travels per cycle, as drawn: the legs' own reach times <see cref="SlideFactor"/>.</summary>
        public float Stride { get; }

        /// <summary>Where in the cycle the legs are, 0..1.</summary>
        public float Phase { get; private set; }

        /// <summary>How much of the gait is laid over the clip, 0..1: up while moving, down while standing.</summary>
        public float Weight { get; private set; }

        QuadrupedGait(Transform?[] upper, Transform?[] lower, Transform? body, float legMetres)
        {
            for (int i = 0; i < 4; i++)
            {
                _upper[i] = upper[i];
                _lower[i] = lower[i];
                _upperRest[i] = upper[i] != null ? upper[i]!.localRotation : Quaternion.identity;
                _lowerRest[i] = lower[i] != null ? lower[i]!.localRotation : Quaternion.identity;
            }
            _body = body;
            _bodyRest = body != null ? body.localPosition : Vector3.zero;
            LegMetres = legMetres;
            Stride = 2f * legMetres * Mathf.Sin(HipDegrees * Mathf.Deg2Rad) * SlideFactor;
        }

        /// <summary>
        /// Find the four legs by the rig's bone names, measure them and remember their rest, or
        /// null if the rig has none of them — an animal whose art is a different convention simply
        /// moves on its idle, as a colonist with no bound arms swings no axe. Bind with the rig in
        /// the pose the gait should rest on: a fresh instance, or one the idle has been evaluated
        /// on to once.
        /// </summary>
        public static QuadrupedGait? Bind(Transform root)
        {
            if (root == null) return null;
            var upper = new Transform?[4];
            var lower = new Transform?[4];
            float legs = 0f;
            int bound = 0;
            for (int i = 0; i < 4; i++)
            {
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
            return new QuadrupedGait(upper, lower, Find(root, "Body"), legMetres);
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
                float hip = HipDegrees * Mathf.Sin(p * Mathf.PI * 2f) * Weight;
                // The foot is off the ground while the hip swings forward — the half-cycle centred
                // on the forward peak — and the knee flexes there, in the direction that clears
                // the ground for that end of the animal. Straight through the stance.
                float flex = KneeSign[i] * KneeDegrees * Mathf.Max(0f, Mathf.Cos(p * Mathf.PI * 2f)) * Weight;
                Pitch(_upper[i], _upperRest[i], right, hip);
                Pitch(_lower[i], _lowerRest[i], right, flex);
            }
            if (_body != null)
            {
                float bob = BobMetres * 0.5f * (1f - Mathf.Cos(Phase * Mathf.PI * 4f)) * Weight;
                _body.localPosition = _bodyRest;
                _body.position += up * bob;
            }
        }

        static void Pitch(Transform? bone, Quaternion rest, Vector3 axis, float degrees)
        {
            if (bone == null) return;
            bone.localRotation = rest;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
