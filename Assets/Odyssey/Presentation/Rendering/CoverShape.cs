#nullable enable
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// What sandbags and a barricade look like (design 50 §7), in one place: the mesher draws the
    /// built thing and the ghost draws the thing being placed, from the same boxes.
    ///
    /// <para><b>Placeholder geometry, deliberately and temporarily</b> — the shelf's idiom. No
    /// pack has a sandbag piece that joins in a line at this grid, so <c>ModuleIds.Sandbags</c>
    /// and <c>ModuleIds.Barricade</c> resolve to the plain block and each piece is a handful of
    /// scaled instances of it. The owner chose custom modular pieces for the sandbags and the
    /// Western Frontier timber and Meadow stone wall for the barricade; the day those rows land,
    /// the mesher reads them instead and this class goes.</para>
    ///
    /// <para><b>A run is drawn joined.</b> A piece is a core in the middle of its cell and an arm
    /// towards each of its four neighbours that holds the same thing, reaching the cell's edge, so
    /// a dragged line reads as one wall of bags rather than a row of separate heaps, and a corner
    /// turns. Each cell is still its own piece with its own hit points: the join is drawing only.</para>
    ///
    /// <para><b>About 1.3 m of sandbags</b> against a colonist drawn about 2.5 m tall — the
    /// Western Frontier sand barricade's 1.54 m and the Meadow stone wall's 1.31 m bracket it. The
    /// owner judges it against a crouched figure in play (design 50 §10).</para>
    /// </summary>
    public static class CoverShape
    {
        /// <summary>The top of a wall of sandbags above its floor.</summary>
        public const float SandbagHeight = 1.3f;

        /// <summary>The top rail of a barricade above its floor.</summary>
        public const float BarricadeHeight = 1.2f;

        /// <summary>The most boxes one piece is drawn with: a core of two and an arm of two each way.</summary>
        public const int MaxParts = 10;

        // ---- sandbags: a lower course the full width, a narrower upper course -------------------

        const float BagWidth = 1.05f, BagLower = 0.95f, UpperWidth = 0.85f;
        const float CoreHalf = 0.55f, Half = CellMetrics.SizeXZ * 0.5f;

        // ---- a barricade: a post in the middle, two rails along the run ------------------------

        const float Post = 0.22f, RailSection = 0.14f, RailLow = 0.5f;

        /// <summary>Is this edifice one of the two pieces of cover this class draws?</summary>
        public static bool Draws(ushort edifice) =>
            edifice == CoreContent.EdificeSandbags || edifice == CoreContent.EdificeBarricade;

        /// <summary>The height a piece is picked at and marked on.</summary>
        public static float Top(ushort edifice) =>
            edifice == CoreContent.EdificeBarricade ? BarricadeHeight : SandbagHeight;

        /// <summary>
        /// The piece's boxes, placed, into <paramref name="parts"/> (at least <see cref="MaxParts"/>
        /// long); returns how many. <paramref name="joins"/> is a bit per <see cref="Directions"/>
        /// for each neighbour holding the same thing.
        /// </summary>
        public static int Parts(ushort edifice, int x, int z, int y, int joins, Matrix4x4[] parts)
        {
            Matrix4x4 root = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y));
            int n = 0;
            if (edifice == CoreContent.EdificeBarricade)
            {
                parts[n++] = Box(root, new Vector3(0f, BarricadeHeight * 0.5f, 0f), new Vector3(Post, BarricadeHeight, Post));
                // A lone barricade still reads as one: rails east and west.
                int rails = joins == 0 ? (1 << Directions.East) | (1 << Directions.West) : joins;
                for (int dir = 0; dir < Directions.Count && n + 2 <= MaxParts; dir++)
                {
                    if ((rails & (1 << dir)) == 0) continue;
                    parts[n++] = Arm(root, dir, 0f, RailLow, RailSection, RailSection);
                    parts[n++] = Arm(root, dir, 0f, BarricadeHeight - RailSection * 0.5f, RailSection, RailSection);
                }
                return n;
            }

            float upper = SandbagHeight - BagLower;
            parts[n++] = Box(root, new Vector3(0f, BagLower * 0.5f, 0f), new Vector3(CoreHalf * 2f, BagLower, BagWidth));
            parts[n++] = Box(root, new Vector3(0f, BagLower + upper * 0.5f, 0f), new Vector3(CoreHalf * 2f - 0.1f, upper, UpperWidth));
            for (int dir = 0; dir < Directions.Count && n + 2 <= MaxParts; dir++)
            {
                if ((joins & (1 << dir)) == 0) continue;
                parts[n++] = Arm(root, dir, CoreHalf, BagLower * 0.5f, BagLower, BagWidth);
                parts[n++] = Arm(root, dir, CoreHalf, BagLower + upper * 0.5f, upper, UpperWidth);
            }
            return n;
        }

        /// <summary>A box from <paramref name="from"/> metres out of the centre to the cell's edge, towards <paramref name="dir"/>.</summary>
        static Matrix4x4 Arm(in Matrix4x4 root, int dir, float from, float centreY, float height, float width)
        {
            float length = Half - from;
            float mid = from + length * 0.5f;
            var centre = new Vector3(Directions.DeltaX[dir] * mid, centreY, Directions.DeltaZ[dir] * mid);
            bool alongX = Directions.DeltaX[dir] != 0;
            var size = alongX ? new Vector3(length, height, width) : new Vector3(width, height, length);
            return Box(root, centre, size);
        }

        /// <summary>
        /// The plain block scaled to <paramref name="size"/> metres and centred on
        /// <paramref name="centre"/> — the module's own box is not a unit cube, which is why this
        /// is derived once rather than written out per part (<c>ShelfShape.Part</c>'s reason).
        /// </summary>
        static Matrix4x4 Box(in Matrix4x4 root, Vector3 centre, Vector3 size)
        {
            ModuleLibrary.GetFallbackBox(ModuleShape.SolidBlock, out Vector3 box, out Vector3 boxCentre);
            var scale = new Vector3(size.x / box.x, size.y / box.y, size.z / box.z);
            Vector3 offset = centre - Vector3.Scale(scale, boxCentre);
            return root * Matrix4x4.Translate(offset) * Matrix4x4.Scale(scale);
        }
    }
}
