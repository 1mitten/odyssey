#nullable enable
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The shape of a bed — frame, mattress and pillow, about a point between its two cells — and
    /// the <b>one</b> place that shape is decided.
    ///
    /// <para><b>Why it is not simply inlined in the mesher.</b> A bed is drawn twice: once as the
    /// thing standing on the board (<c>ChunkMesher.EmitBed</c>) and once as the ghost under the
    /// cursor and over a waiting site (<c>OdysseyBootstrap</c>). The ghost path arrived from the
    /// build-cursor work knowing nothing about beds, so it drew the bed's module as one
    /// cell-filling cube at the head cell alone: a bed ordered on the grass appeared as a block,
    /// and — worse for the feature this exists to prove — turning the ghost with <b>R</b> changed
    /// nothing anybody could see, because a cube looks the same all four ways round.</para>
    ///
    /// <para>Two copies of a shape is how one of them gets corrected on its own. The mesher, the
    /// cursor and the selection bracket now ask the same numbers, so what the player sees under
    /// the pointer is what stands on the board, and what is highlighted is the bed rather than the
    /// cell it happens to be in.</para>
    ///
    /// <para><b>Everything here is in metres</b>, measured from the bed's own origin: the floor,
    /// midway along the bed, with local <c>+Z</c> the foot end and <c>−Z</c> the head. That is the
    /// only honest way to write it, because the parts are drawn from two module boxes of different
    /// sizes and a raw <c>Scale</c> would mean a different thing for each.</para>
    ///
    /// <para><b>Placeholder geometry.</b> No pack contains a two-tile bed, so the frame and the
    /// mattress are the plain block module scaled, and the pillow is <see cref="PillowMesh"/>.
    /// Catalogue rows on <c>odyssey.module.bed</c> and <c>odyssey.module.bed.pillow</c> replace
    /// every bed in the game when real art exists, and this file goes with them.</para>
    /// </summary>
    public static class BedShape
    {
        /// <summary>How many parts a bed is drawn from: frame, mattress, pillow.</summary>
        public const int PartCount = 3;

        /// <summary>The index of the pillow, which is the one part with its own module and colour.</summary>
        public const int PillowPart = 2;

        // The bed occupies two cells along its facing, so it is CellMetrics.SizeXZ * 2 long and one
        // cell wide. These leave a hand's breadth of floor at each end and side rather than filling
        // the footprint exactly, because a bed flush to its own boundary reads as a platform.
        //
        // The mattress runs the WHOLE length under the pillow (owner, 2026-09-17: "the mattress /
        // base needs to extend to underneath the pillow as the pillow is simply floating in mid
        // air"). It did not before: the mattress reached z = ±1.02 and the pillow sat at z = −1.75,
        // so there was 0.56 m of open air between them and the pillow hung over the end of the bed.
        static readonly Vector3[] Sizes =
        {
            new Vector3(2.00f, 0.42f, 4.60f), // frame
            new Vector3(1.80f, 0.30f, 4.30f), // mattress
            new Vector3(1.30f, 0.26f, 0.95f), // pillow
        };

        // Centres, in metres from the bed's origin. The mattress overlaps the frame's top and the
        // pillow overlaps the mattress's, by a couple of centimetres each, so that no seam can open
        // between them at any camera angle — the same reason the ground and the banks are draped.
        static readonly Vector3[] Centres =
        {
            new Vector3(0f, 0.21f, 0f),     // frame: 0.00 .. 0.42
            new Vector3(0f, 0.55f, 0f),     // mattress: 0.40 .. 0.70, spanning z = ±2.15
            new Vector3(0f, 0.80f, -1.55f), // pillow: 0.67 .. 0.93, z = -2.03 .. -1.08
        };

        /// <summary>
        /// The height of the mattress's top surface above the bed's own floor — what a sleeper
        /// lies on. Derived from the mattress's own box rather than written down beside it, so
        /// tuning the mattress moves the sleeper with it.
        /// </summary>
        public static float MattressTop => Centres[1].y + Sizes[1].y * 0.5f;

        /// <summary>The overall box a bed occupies, for a selection bracket to be drawn round.</summary>
        public static Vector3 Size => new Vector3(Sizes[0].x, Centres[2].y + Sizes[2].y * 0.5f, Sizes[0].z);

        /// <summary>Is this part drawn from the pillow's own module rather than the bed's?</summary>
        public static bool IsPillow(int part) => part == PillowPart;

        /// <summary>
        /// The bed's own origin: the draped floor point half a cell along the facing from the head
        /// cell, which is the middle of the two cells it occupies.
        ///
        /// <para>Draped, because a bed is fixed to the grid — the rule the whole project settled on
        /// after walls went up stepped: anything fixed to the grid is draped, only what moves over
        /// it is lifted.</para>
        /// </summary>
        public static Matrix4x4 Root(int x, int z, int y, int facing) =>
            GroundRelief.Drape(Origin(x, z, y, facing));

        /// <summary>The same point, undraped — what a selection bracket is centred on.</summary>
        public static Vector3 Origin(int x, int z, int y, int facing) =>
            CellMetrics.FloorCentre(x, z, y)
            + new Vector3(Directions.DeltaX[facing], 0f, Directions.DeltaZ[facing]) * CellMetrics.HalfXZ;

        /// <summary>
        /// One part of the bed, placed and turned. <paramref name="part"/> is 0 to 2.
        ///
        /// <para>The two module boxes are different sizes and neither is a unit cube, so the metre
        /// figures above are divided by whichever box this part is drawn from. Getting that wrong
        /// is silent — the part is simply the wrong size — so it is derived here once rather than
        /// written out per part.</para>
        /// </summary>
        public static Matrix4x4 Part(in Matrix4x4 root, int facing, int part)
        {
            ModuleLibrary.GetFallbackBox(
                IsPillow(part) ? ModuleShape.Pillow : ModuleShape.SolidBlock,
                out Vector3 box, out Vector3 boxCentre);

            Vector3 size = Sizes[part];
            var scale = new Vector3(size.x / box.x, size.y / box.y, size.z / box.z);

            // The module's own local centre rides the scale, so the offset that lands the part on
            // its intended centre has to take it back off.
            Vector3 offset = Centres[part] - Vector3.Scale(scale, boxCentre);

            Matrix4x4 yaw = Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f));
            return root * yaw * Matrix4x4.Translate(offset) * Matrix4x4.Scale(scale);
        }

        /// <summary>
        /// The bed's bounding box in world space: where it is, and how big, for the selection
        /// bracket.
        ///
        /// <para><b>Why the bracket is not simply the cell.</b> A bed is two cells long and knee
        /// high, and the pick highlighted the whole 2.5 m cell it was clicked in (owner,
        /// 2026-09-17: <i>"it highlighted the entire cell instead of highlighting the bed"</i>) —
        /// which says neither how big the bed is nor which way it is facing, and is wrong about
        /// both ends of it.</para>
        /// </summary>
        public static void WorldBounds(int x, int z, int y, int facing, out Vector3 centre, out Vector3 size)
        {
            float half = Centres[2].y + Sizes[2].y * 0.5f;
            centre = GroundRelief.Lift(Origin(x, z, y, facing)) + Vector3.up * (half * 0.5f);

            // Turned with the bed: a quarter turn swaps the long axis for the short one.
            bool alongX = facing == Directions.East || facing == Directions.West;
            size = alongX
                ? new Vector3(Sizes[0].z, half, Sizes[0].x)
                : new Vector3(Sizes[0].x, half, Sizes[0].z);
        }
    }
}
