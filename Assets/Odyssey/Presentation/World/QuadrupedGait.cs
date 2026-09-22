#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A computed four-legged gait, for a rig that has no walk clip (design 29 §8a;
    /// <c>docs/research/c-procedural-quadruped-gait.md</c>). The hog's whole locomotion is one
    /// idle clip; this lays a <b>trot</b> over it, in the manner <c>WorkSwing</c> lays an axe
    /// stroke over a colonist's idle: forward-kinematic sines on the hip and the knee of each leg,
    /// written after the mixer has played, about the figure's own right axis so the same numbers
    /// drive any rig with the four-leg bone convention.
    ///
    /// <para><b>Why a trot and not a walk, and why the stride is measured</b> (owner, 2026-09-22:
    /// <i>"the pig walking looks awful"</i>). The first version was a lateral-sequence walk cycling
    /// once per authored metre. The rig's legs are 23 cm from shoulder joint to sole on a 1.2 m
    /// body, and a 23 cm leg swinging 25° covers about 20 cm a cycle — so the feet slid over most
    /// of every stride while the legs waved slowly, and a 2 cm bob rode on top at the same slow
    /// rate. A short-legged animal at a metre a second does not walk; it trots, on diagonal pairs,
    /// with quick short steps. The stride is therefore <i>derived from the leg the rig actually
    /// has</i> — twice the leg length times the sine of the hip swing, the ground one leg covers
    /// in its stance — and the cycle turns as fast as that stride demands. <see cref="SlideFactor"/>
    /// admits that a stylised model this squat must either scurry or slide, and splits it.</para>
    ///
    /// <para><b>The phase is advanced once a frame</b> from the figure's measured speed
    /// (<see cref="Advance"/>) and the angles are applied in the idempotent pose pass
    /// (<see cref="Apply"/>), which may run twice a frame; splitting the two is what keeps a
    /// second pass from taking a second step.</para>
    ///
    /// <para><b>The knees are signed by anatomy.</b> A fore leg folds its lower segment back under
    /// the body during the swing (the carpus flexes toward the tail); a hind leg's hock flexes the
    /// other way, foot forward and up. Both are the direction that clears the ground.</para>
    /// </summary>
    public sealed class QuadrupedGait
    {
        /// <summary>Hip fore-aft swing, half amplitude in degrees.</summary>
        public const float HipDegrees = 30f;

        /// <summary>Knee flex at mid-swing, degrees, signed per leg by <see cref="KneeSign"/>.</summary>
        public const float KneeDegrees = 30f;

        /// <summary>Body bob, metres, twice per cycle: once per diagonal pair landing.</summary>
        public const float BobMetres = 0.012f;

        /// <summary>
        /// How much further the body travels per cycle than the legs geometrically cover. 1 is
        /// no sliding at all and a squat rig scurrying at three or four cycles a second; 2 halves
        /// the cadence and lets the feet slide the other half. A playtest number.
        /// </summary>
        public const float SlideFactor = 2f;

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
        readonly Transform? _body;

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
            for (int i = 0; i < 4; i++) { _upper[i] = upper[i]; _lower[i] = lower[i]; }
            _body = body;
            LegMetres = legMetres;
            Stride = 2f * legMetres * Mathf.Sin(HipDegrees * Mathf.Deg2Rad) * SlideFactor;
        }

        /// <summary>
        /// Find the four legs by the rig's bone names and measure them, or null if the rig has
        /// none of them — an animal whose art is a different convention simply moves on its idle,
        /// as a colonist with no bound arms swings no axe.
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
        /// Lay the gait over whatever the mixer wrote. Idempotent for a given phase: every angle
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
                // on the forward peak — and the knee flexes there, in the direction that clears
                // the ground for that end of the animal. Straight through the stance.
                float flex = KneeSign[i] * KneeDegrees * Mathf.Max(0f, Mathf.Cos(p * Mathf.PI * 2f)) * Weight;
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
