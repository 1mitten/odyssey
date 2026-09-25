#nullable enable
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// What a wall of sandbags looks like (design 50 §7a-bis, research <c>e-12</c>), in one place:
    /// the mesher draws the built piece and the ghost draws the piece being placed, from the same
    /// bags.
    ///
    /// <para><b>Bags, not a wall.</b> Each piece is <see cref="SandbagMesh"/> instanced once per
    /// bag, laid as a real revetment is:
    /// <list type="bullet">
    /// <item>four courses of <b>stretchers</b> (long side to the face), two rows deep;</item>
    /// <item>a top course of <b>headers</b> (end to the face), across the wall;</item>
    /// <item><b>running bond</b>: each stretcher course is staggered half a bag against the one
    /// below;</item>
    /// <item>a <b>batter</b>: each course steps in on both faces.</item>
    /// </list>
    /// The owner's report of the boxes it replaces was <i>"the sandbags need to look like
    /// sandbags"</i>, and <c>e-12</c> finding 13 is why these are the cues: a scalloped top, a
    /// groove wherever two bags meet, and the stagger.</para>
    ///
    /// <para><b>The bond is the cell's own and it repeats.</b> A cell is 2.5 m, exactly three
    /// stretchers or five headers, so every cell's joints fall at the same places. A dragged line
    /// is therefore continuous across cells without any piece knowing where it is in the line.
    /// A bag that straddles a join is drawn by the cell it starts in, and only that one — see
    /// <see cref="Course"/>.</para>
    ///
    /// <para><b>A run is drawn joined, a corner interlocks.</b> A piece lays bags along each axis it
    /// is joined on, towards each neighbour holding sandbags; a lone piece runs east–west. Where
    /// both axes are joined — a corner, a T or a crossing — the east–west wall runs through and the
    /// north–south arms stop against its face. Each cell is still its own piece with its own hit
    /// points: the join is drawing only.</para>
    ///
    /// <para><b>About 1.3 m of sandbags</b> against a colonist drawn about 2.5 m tall. The bags are
    /// drawn about twice life size, 0.83 × 0.56 × 0.26 m against the 38 × 25 × 13 cm of
    /// <c>e-12</c> finding 2. At the play camera a life-size bag is a few pixels, and the wall would
    /// read as a striped block.</para>
    /// </summary>
    public static class CoverShape
    {
        /// <summary>The top of a wall of sandbags above its floor.</summary>
        public const float SandbagHeight = 1.3f;

        /// <summary>
        /// The most bags one piece is drawn with. A crossing is the worst case: the through wall
        /// and two arms, two rows each, four stretcher courses and a header course. 38 in a straight run.
        /// </summary>
        public const int MaxParts = 96;

        // ---- the courses -----------------------------------------------------------------------

        /// <summary>Stretcher courses, below the header course on top.</summary>
        public const int StretcherCourses = 4;

        const int Courses = StretcherCourses + 1;

        /// <summary>How far each course's bottom sits above the last: <see cref="SandbagHeight"/> over five.</summary>
        const float CourseRise = SandbagHeight / Courses;

        /// <summary>A bag is drawn taller than its course rises, so each one sits pressed into the one below.</summary>
        const float BagHeight = CourseRise * 1.18f;

        /// <summary>Half the wall's thickness at the bottom course: two rows of 0.52 m bags.</summary>
        const float BaseHalfThickness = 0.52f;

        /// <summary>How far each course steps in on each face (<c>e-12</c> finding 10, about a quarter of the old manuals' course).</summary>
        const float Batter = 0.035f;

        /// <summary>How much wider and longer a bag is drawn than its slot, so neighbours bulge into each other.</summary>
        const float Bulge = 0.05f;

        const float Half = CellMetrics.SizeXZ * 0.5f;

        /// <summary>Three stretchers a cell, five headers.</summary>
        const float StretcherPitch = CellMetrics.SizeXZ / 3f, HeaderPitch = CellMetrics.SizeXZ / 5f;

        /// <summary>Nothing shorter than this at a free end: a sliver merges into the bag beside it.</summary>
        const float ShortestStretcher = 0.3f, ShortestHeader = 0.25f;

        /// <summary>Is this edifice something this class draws?</summary>
        public static bool Draws(ushort edifice) => edifice == CoreContent.EdificeSandbags;

        /// <summary>The height a piece is picked at and marked on.</summary>
        public static float Top(ushort edifice) => SandbagHeight;

        /// <summary>
        /// The piece's bags, placed, into <paramref name="parts"/> with each one's cloth
        /// (0 to <see cref="StuffPalette.HessianShades"/> − 1) in <paramref name="shades"/>, both at
        /// least <see cref="MaxParts"/> long; returns how many. <paramref name="joins"/> is a bit per
        /// <see cref="Directions"/> for each neighbour holding the same thing.
        /// </summary>
        public static int Parts(ushort edifice, int x, int z, int y, int joins, Matrix4x4[] parts, int[] shades)
        {
            Matrix4x4 root = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y));
            bool east = (joins & (1 << Directions.East)) != 0, west = (joins & (1 << Directions.West)) != 0;
            bool north = (joins & (1 << Directions.North)) != 0, south = (joins & (1 << Directions.South)) != 0;
            bool alongZ = north || south;
            bool alongX = east || west || !alongZ;

            var lay = new Layer(root, parts, shades, Seed(x, z, y));
            for (int course = 0; course < Courses; course++)
            {
                float t = BaseHalfThickness - Batter * course;
                if (alongX)
                {
                    // The through wall. It stops at a free end on the cell's edge, or flush with
                    // the far face of the arms if it is a corner and not a run.
                    float end = alongZ ? t : Half;
                    lay.Course(course, 0, west ? float.NegativeInfinity : -end, east ? float.PositiveInfinity : end, t);
                }
                if (alongZ && !alongX)
                    lay.Course(course, 1, south ? float.NegativeInfinity : -Half, north ? float.PositiveInfinity : Half, t);
                else if (alongZ)
                {
                    // The arms, against the through wall's face.
                    if (north) lay.Course(course, 1, t, float.PositiveInfinity, t);
                    if (south) lay.Course(course, 1, float.NegativeInfinity, -t, t);
                }
            }
            return lay.Count;
        }

        /// <summary>A cell's own number for the hash, so two pieces never share their irregularity.</summary>
        static uint Seed(int x, int z, int y) =>
            (uint)x * 73856093u ^ (uint)z * 19349663u ^ (uint)y * 83492791u;

        /// <summary>A cheap integer hash, well mixed enough for a few percent of wobble.</summary>
        static uint Mix(uint h)
        {
            h ^= h >> 16; h *= 0x7FEB352Du;
            h ^= h >> 15; h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }

        /// <summary>−1..1 from eight bits of <paramref name="h"/>.</summary>
        static float Signed(uint h, int shift) => ((h >> shift) & 0xFF) / 127.5f - 1f;

        struct Layer
        {
            readonly Matrix4x4 _root;
            readonly Matrix4x4[] _parts;
            readonly int[] _shades;
            readonly uint _seed;
            public int Count;

            public Layer(Matrix4x4 root, Matrix4x4[] parts, int[] shades, uint seed)
            {
                _root = root; _parts = parts; _shades = shades; _seed = seed; Count = 0;
            }

            /// <summary>
            /// One course of one wall, along X (<paramref name="axis"/> 0) or Z (1), between
            /// <paramref name="lo"/> and <paramref name="hi"/> in metres from the cell's middle.
            ///
            /// <para>An infinite end is a join: the bags run on into the neighbour, which draws its
            /// own. A finite end is free: the course stops there, and a bag cut shorter than the
            /// shortest there is merged into the one beside it rather than drawn as a sliver.</para>
            ///
            /// <para><b>Who draws a bag.</b> The cell its start lies in, <c>[−Half, Half)</c>. Because
            /// both cells of a join see the same joints (the bond repeats per cell) and neither
            /// clips at the join, a straddling bag has one start and one owner: drawn once, never
            /// missed.</para>
            /// </summary>
            public void Course(int course, int axis, float lo, float hi, float halfThickness)
            {
                bool header = course >= StretcherCourses;
                float pitch = header ? HeaderPitch : StretcherPitch;
                float offset = header || (course & 1) == 0 ? 0f : pitch * 0.5f;
                float shortest = header ? ShortestHeader : ShortestStretcher;
                bool loFree = !float.IsInfinity(lo), hiFree = !float.IsInfinity(hi);
                if (!loFree) lo = -Half - 2f * pitch;
                if (!hiFree) hi = Half + 2f * pitch;

                // The joints strictly inside (lo, hi), from the cell's west or south edge.
                float first = Mathf.Floor((lo + Half - offset) / pitch) * pitch + offset - Half;
                float start = lo;
                for (float joint = first; ; joint += pitch)
                {
                    if (joint <= lo + 1e-4f) continue;
                    if (joint >= hi - 1e-4f) { Bag(course, axis, start, hi, halfThickness, header); break; }
                    if (loFree && start == lo && joint - lo < shortest) continue;
                    if (hiFree && hi - joint < shortest) { Bag(course, axis, start, hi, halfThickness, header); break; }
                    Bag(course, axis, start, joint, halfThickness, header);
                    start = joint;
                }
            }

            void Bag(int course, int axis, float from, float to, float halfThickness, bool header)
            {
                if (from < -Half - 1e-4f || from >= Half - 1e-4f) return; // a neighbour's
                if (header) Place(course, axis, from, to, 0, halfThickness, header);
                else
                {
                    Place(course, axis, from, to, -1, halfThickness, header);
                    Place(course, axis, from, to, +1, halfThickness, header);
                }
            }

            void Place(int course, int axis, float from, float to, int row, float halfThickness, bool header)
            {
                if (Count >= _parts.Length || Count >= _shades.Length) return;

                uint h = Mix(_seed ^ Mix((uint)(course * 131 + axis * 17 + (row + 1) * 7)
                                         ^ (uint)Mathf.RoundToInt((from + Half) * 100f) * 2654435761u));
                float run = to - from, middle = (from + to) * 0.5f;

                // The bag's own box: its length along the bag, its width across it.
                float length, width, across;
                if (header)
                {
                    length = 2f * halfThickness + Bulge;
                    width = run + Bulge;
                    across = 0.015f * Signed(h, 0);
                }
                else
                {
                    length = run + Bulge;
                    width = halfThickness + Bulge;
                    across = row * halfThickness * 0.5f + 0.015f * Signed(h, 0);
                }
                length *= 1f + 0.03f * Signed(h, 8);
                width *= 1f + 0.04f * Signed(h, 16);
                float height = BagHeight * (1f + 0.06f * Signed(h, 24));

                // A header lies across the wall, so its long axis turns a quarter from the run's.
                // Which end is tied is a coin (bit 5): a free-standing wall has no inside to turn
                // it to (e-12 finding 9).
                float yaw = (axis == 0 ? 0f : 90f) + (header ? 90f : 0f)
                            + ((h & 32u) != 0 ? 180f : 0f) + 3f * Signed(Mix(h), 0);
                Vector3 centre = axis == 0
                    ? new Vector3(middle, 0f, across)
                    : new Vector3(across, 0f, middle);
                centre.y = course * CourseRise + height * 0.5f;

                _parts[Count] = _root * Matrix4x4.TRS(centre, Quaternion.Euler(0f, yaw, 0f), new Vector3(length, height, width));
                _shades[Count] = (int)((Mix(h ^ 0x9E3779B9u) >> 8) % StuffPalette.HessianShades);
                Count++;
            }
        }
    }
}
