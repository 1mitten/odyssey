#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>The three shapes blood leaves on the ground (design 33 §10a).</summary>
    public enum BloodShape : byte
    {
        /// <summary>A cut's mark: elongated along the blow, with its satellite drops built into the shape.</summary>
        Splatter = 0,
        /// <summary>A blow's mark: smaller and round.</summary>
        Spot = 1,
        /// <summary>Under a body gone down or dead.</summary>
        Pool = 2,
    }

    /// <summary>
    /// <b>How much a hit throws, and what it leaves</b> (design 33 §10b). Every number is INVENTED
    /// for the playtest; the shape of the rules is the owner's (§7d): sharp spurts more and leaves a
    /// splatter, blunt a smaller puff and a smaller mark, and a pool is sized by the body under it.
    /// </summary>
    public static class BloodSpray
    {
        /// <summary>The most drops one hit throws, sharp or blunt.</summary>
        public const int MaxDropsPerHit = 16;

        /// <summary>How long a person is lying down, in metres, for a pool under one.</summary>
        public const float PersonLength = 1.8f;

        /// <summary>A pool's radius against the length of the body over it.</summary>
        public const float PoolRadiusPerLength = 0.45f;

        /// <summary>The smallest body a pool is sized for, so one of unknown length still pools.</summary>
        public const float SmallestBody = 0.25f;

        /// <summary>How long a pool waits for the body to finish falling, in seconds of game time.</summary>
        public const float PoolWaitSeconds = 1.2f;

        /// <summary>How long a pool takes to spread to its full size: eight seconds at 60 ticks a second.</summary>
        public const int PoolSpreadTicks = 480;

        /// <summary>How big a pool is the moment it appears, as a fraction of its full size.</summary>
        public const float PoolStartFraction = 0.2f;

        /// <summary>How many drops a hit of <paramref name="damage"/> hit points throws.</summary>
        public static int Drops(float damage, bool sharp)
        {
            float d = Math.Max(0f, damage);
            return sharp
                ? Math.Min(MaxDropsPerHit, 4 + (int)(d / 2f))
                : Math.Min(5, 1 + (int)(d / 4f));
        }

        /// <summary>The radius of the mark a hit leaves, in metres (its width, for a splatter).</summary>
        public static float MarkRadius(float damage, bool sharp)
        {
            float d = Math.Max(0f, damage);
            return sharp ? Math.Min(0.55f, 0.22f + 0.02f * d) : Math.Min(0.28f, 0.12f + 0.01f * d);
        }

        /// <summary>How much longer than wide a hit's mark is, laid along the blow.</summary>
        public static float MarkStretch(bool sharp) => sharp ? 1.7f : 1f;

        /// <summary>The shape a hit leaves.</summary>
        public static BloodShape ShapeOf(bool sharp) => sharp ? BloodShape.Splatter : BloodShape.Spot;

        /// <summary>
        /// How the drops leave the wound: their speed along the blow, slowest and fastest, in metres
        /// a second, and the half-width of the fan they spread over, in degrees.
        /// </summary>
        public static void Throw(bool sharp, out float minSpeed, out float maxSpeed, out float spreadDegrees)
        {
            if (sharp)
            {
                minSpeed = 2.5f;
                maxSpeed = 4.5f;
                spreadDegrees = 30f;
            }
            else
            {
                minSpeed = 1f;
                maxSpeed = 2f;
                spreadDegrees = 60f;
            }
        }

        /// <summary>
        /// A pool's full radius, in metres: under a body <paramref name="bodyLength"/> long, at
        /// <paramref name="sizeFactor"/> of the largest (<see cref="BloodModel.PoolSize"/>: a death
        /// 1, a down 0.6).
        /// </summary>
        public static float PoolRadius(float bodyLength, float sizeFactor) =>
            PoolRadiusPerLength * Math.Max(SmallestBody, bodyLength) * Math.Max(0f, sizeFactor);

        /// <summary>
        /// How far a pool has spread, <paramref name="ageTicks"/> after it appeared: from
        /// <see cref="PoolStartFraction"/> to the whole over <see cref="PoolSpreadTicks"/>, easing
        /// out, so it runs quickly at first and settles.
        /// </summary>
        public static float PoolGrowth(long ageTicks)
        {
            if (ageTicks <= 0) return PoolStartFraction;
            if (ageTicks >= PoolSpreadTicks) return 1f;
            float t = (float)ageTicks / PoolSpreadTicks;
            float eased = 1f - (1f - t) * (1f - t);
            return PoolStartFraction + (1f - PoolStartFraction) * eased;
        }
    }

    /// <summary>One mark on the ground. Where it lies, in world metres; how it is turned and sized; when it was made, in ticks.</summary>
    public struct BloodMarkRecord
    {
        public float X, Y, Z;
        /// <summary>Which way its long axis points, in degrees about the vertical: the blow's direction.</summary>
        public float Yaw;
        /// <summary>Its full radius, in metres.</summary>
        public float Radius;
        /// <summary>How much longer than wide.</summary>
        public float Stretch;
        public BloodShape Shape;
        /// <summary>The tick it was made on. A pool's size and every mark's fade are measured from here.</summary>
        public long Born;
        /// <summary>The layer it lies on, for the slice.</summary>
        public int Layer;
        /// <summary>The cell it stands on, as the render model indexes it, so a floor taken away can take it too.</summary>
        public int Cell;
    }

    /// <summary>
    /// <b>The marks on the ground</b> (design 33 §10): at most <see cref="Cap"/>, oldest first, each
    /// fading by the simulation's tick and gone after a day. Presentation only — nothing here is in
    /// a cell, a save or the hash, and a world change clears it.
    ///
    /// <para>Kept in the order they were made, so the oldest is always at the front: the cap drops
    /// from the front and so does the day's end. A removal in the middle (a floor taken away)
    /// shifts the rest down, which over two hundred structs is cheaper than keeping a free list
    /// honest.</para>
    /// </summary>
    public sealed class BloodLedger
    {
        /// <summary>The most marks on the ground at once (owner: "around 200, oldest first").</summary>
        public const int Cap = 200;

        /// <summary>How long a mark lasts: about one in-game day (owner).</summary>
        public const long LifetimeTicks = 60_000;

        /// <summary>How long a mark holds full strength before it starts to thin: a quarter of its life.</summary>
        public const long FullStrengthTicks = 15_000;

        /// <summary>How many strengths a mark is drawn at. Each is one instanced call per shape, so this is the draw ceiling's factor.</summary>
        public const int FadeSteps = 6;

        readonly BloodMarkRecord[] _marks = new BloodMarkRecord[Cap];

        /// <summary>How many marks are on the ground.</summary>
        public int Count { get; private set; }

        /// <summary>The <paramref name="index"/>th mark, the oldest first.</summary>
        public ref readonly BloodMarkRecord this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _marks[index];
            }
        }

        /// <summary>Lay a mark down. At the cap, the oldest goes to make room.</summary>
        public void Add(in BloodMarkRecord mark)
        {
            if (Count == Cap) RemoveAt(0);
            _marks[Count++] = mark;
        }

        /// <summary>Take one mark up, keeping the rest in order.</summary>
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            Array.Copy(_marks, index + 1, _marks, index, Count - index - 1);
            Count--;
        }

        /// <summary>Take up every mark a whole day old at <paramref name="now"/>. They are at the front.</summary>
        public void Expire(long now)
        {
            int gone = 0;
            while (gone < Count && now - _marks[gone].Born >= LifetimeTicks) gone++;
            if (gone == 0) return;
            Array.Copy(_marks, gone, _marks, 0, Count - gone);
            Count -= gone;
        }

        /// <summary>Every mark goes: the world they belonged to has.</summary>
        public void Clear() => Count = 0;

        /// <summary>
        /// How strongly a mark <paramref name="ageTicks"/> old is drawn: whole for the first quarter
        /// of a day, then thinning evenly to nothing at the day's end.
        /// </summary>
        public static float Strength(long ageTicks)
        {
            if (ageTicks <= FullStrengthTicks) return 1f;
            if (ageTicks >= LifetimeTicks) return 0f;
            return 1f - (float)(ageTicks - FullStrengthTicks) / (LifetimeTicks - FullStrengthTicks);
        }

        /// <summary>
        /// Which of the <see cref="FadeSteps"/> strengths a mark is drawn at: 0 the strongest, or -1
        /// when it is gone. A step is drawn at the top of its band (<see cref="StepStrength"/>), so a
        /// mark is never drawn fainter than it is.
        /// </summary>
        public static int FadeStep(long ageTicks)
        {
            float strength = Strength(ageTicks);
            if (strength <= 0f) return -1;
            int band = (int)Math.Ceiling(strength * FadeSteps);
            if (band < 1) band = 1;
            return FadeSteps - band;
        }

        /// <summary>The strength a fade step is drawn at: 1 for the first, one step less for each after.</summary>
        public static float StepStrength(int step) => (float)(FadeSteps - step) / FadeSteps;

        /// <summary>Whether a mark on <paramref name="layer"/> is drawn, by the corpses' rule: inside the slice's drawn layers.</summary>
        public static bool Visible(int layer, int lowest, int highest) => layer >= lowest && layer <= highest;
    }
}
