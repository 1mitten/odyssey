#nullable enable
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The one place metres exist.
    ///
    /// The simulation speaks only in cells (x, z, layer); ADR 0002 fixes a cell at
    /// 2.5 x 2.5 m on the ground and 3.0 m tall. Every conversion between the two lives here, so
    /// there is exactly one definition of where a cell is in the world and no system can drift
    /// half a cell away from another.
    ///
    /// Axis convention: simulation z maps to world Z, simulation layer y maps to world Y. The
    /// corner of cell (x, z, y) is at <c>(x * 2.5, y * 3.0, z * 2.5)</c>.
    /// </summary>
    public static class CellMetrics
    {
        public const float SizeXZ = 2.5f;
        public const float SizeY = 3.0f;
        public const float HalfXZ = SizeXZ * 0.5f;

        /// <summary>The low corner of a cell: minimum x, minimum z, the cell's own floor level.</summary>
        public static Vector3 Corner(int x, int z, int y) =>
            new Vector3(x * SizeXZ, y * SizeY, z * SizeXZ);

        /// <summary>The centre of the cell's floor. Everything that stands on the floor is placed here.</summary>
        public static Vector3 FloorCentre(int x, int z, int y) =>
            new Vector3(x * SizeXZ + HalfXZ, y * SizeY, z * SizeXZ + HalfXZ);

        public static Vector3 FloorCentre(CellRef cell) => FloorCentre(cell.X, cell.Z, cell.Y);

        /// <summary>The centre of the cell volume. Used for bounds, never for placement.</summary>
        public static Vector3 Centre(int x, int z, int y) =>
            new Vector3(x * SizeXZ + HalfXZ, y * SizeY + SizeY * 0.5f, z * SizeXZ + HalfXZ);

        /// <summary>
        /// The centre of the vertical face a cell shares with its neighbour in <paramref name="dir"/>,
        /// at floor level. Wall, window and door panels are placed on faces, not in cell centres,
        /// so that a one-cell-thick wall reads as a wall rather than as a 2.5 m block.
        /// </summary>
        public static Vector3 FaceCentre(int x, int z, int y, int dir)
        {
            Vector3 c = FloorCentre(x, z, y);
            switch (dir)
            {
                case Directions.North: return new Vector3(c.x, c.y, c.z + HalfXZ);
                case Directions.East: return new Vector3(c.x + HalfXZ, c.y, c.z);
                case Directions.South: return new Vector3(c.x, c.y, c.z - HalfXZ);
                default: return new Vector3(c.x - HalfXZ, c.y, c.z);
            }
        }

        /// <summary>World-space bounds of a whole layer of a grid, for camera framing.</summary>
        public static Bounds LayerBounds(GridSize size, int layer)
        {
            var min = new Vector3(0f, layer * SizeY, 0f);
            var max = new Vector3(size.SizeX * SizeXZ, (layer + 1) * SizeY, size.SizeZ * SizeXZ);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    /// <summary>
    /// The four horizontal directions, in a fixed order. The order is part of the render
    /// contract: face panels are emitted in this order so that a rebuilt chunk produces the same
    /// instance list as the one it replaced.
    /// </summary>
    public static class Directions
    {
        public const int North = 0; // +z
        public const int East = 1;  // +x
        public const int South = 2; // -z
        public const int West = 3;  // -x
        public const int Count = 4;

        public static readonly int[] DeltaX = { 0, 1, 0, -1 };
        public static readonly int[] DeltaZ = { 1, 0, -1, 0 };

        /// <summary>Yaw in degrees that turns a module's local +Z to face outward in this direction.</summary>
        public static readonly float[] Yaw = { 0f, 90f, 180f, 270f };

        public static int Opposite(int dir) => (dir + 2) & 3;
    }
}
