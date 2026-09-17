#nullable enable
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The shape of a bed, as three scaled boxes about a point between its two cells — and the
    /// <b>one</b> place that shape is decided.
    ///
    /// <para><b>Why it is not simply inlined in the mesher.</b> A bed is drawn twice: once as the
    /// thing standing on the board (<c>ChunkMesher.EmitBed</c>) and once as the ghost under the
    /// cursor and over a waiting site (<c>OdysseyBootstrap</c>). The ghost path arrived from the
    /// build-cursor work knowing nothing about beds, so it drew the bed's module as one
    /// cell-filling cube at the head cell alone: a bed ordered on the grass appeared as a block,
    /// and — worse for the feature this exists to prove — turning the ghost with <b>R</b> changed
    /// nothing anybody could see, because a cube looks the same all four ways round.</para>
    ///
    /// <para>Two copies of a shape is how one of them gets corrected on its own. The mesher and the
    /// cursor now ask the same three matrices, so what the player sees under the pointer is what
    /// stands on the board, which is the bargain the build cursor already makes for a wall.</para>
    ///
    /// <para><b>Placeholder geometry.</b> No pack contains a two-tile bed, so these are the plain
    /// block module scaled three ways — frame, mattress, pillow. One catalogue row on
    /// <c>odyssey.module.bed</c> replaces every bed in the game when real art exists, and this
    /// file goes with it.</para>
    /// </summary>
    public static class BedShape
    {
        /// <summary>How many boxes a bed is drawn from.</summary>
        public const int PartCount = 3;

        // Frame, mattress, pillow, in local space: +Z is the foot end and −Z the head, so the
        // pillow sits at the cell the order named once the facing's yaw is applied.
        static readonly Vector3[] Offsets =
        {
            new Vector3(0f, 0.195f, 0f),
            new Vector3(0f, 0.54f, 0.05f),
            new Vector3(0f, 0.57f, -1.75f),
        };

        static readonly Vector3[] Scales =
        {
            new Vector3(0.80f, 0.13f, 0.97f),
            new Vector3(0.66f, 0.10f, 0.86f),
            new Vector3(0.46f, 0.06f, 0.13f),
        };

        /// <summary>
        /// The bed's own origin: the draped floor point half a cell along the facing from the head
        /// cell, which is the middle of the two cells it occupies.
        ///
        /// <para>Draped, because a bed is fixed to the grid — the rule the whole project settled on
        /// after walls went up stepped: anything fixed to the grid is draped, only what moves over
        /// it is lifted.</para>
        /// </summary>
        public static Matrix4x4 Root(int x, int z, int y, int facing)
        {
            Vector3 centre = CellMetrics.FloorCentre(x, z, y)
                + new Vector3(Directions.DeltaX[facing], 0f, Directions.DeltaZ[facing])
                    * CellMetrics.HalfXZ;
            return GroundRelief.Drape(centre);
        }

        /// <summary>One box of the bed, placed and turned: <paramref name="part"/> is 0 to 2.</summary>
        public static Matrix4x4 Part(in Matrix4x4 root, int facing, int part)
        {
            Matrix4x4 yaw = Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f));
            return root * yaw
                * Matrix4x4.Translate(Offsets[part])
                * Matrix4x4.Scale(Scales[part]);
        }
    }
}
