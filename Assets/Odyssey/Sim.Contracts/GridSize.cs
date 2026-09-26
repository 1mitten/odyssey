#nullable enable
using System;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// The dimensions of a layered cell grid, and the one true index convention.
    ///
    /// Fixed by docs/design/02-world-and-layers.md: <c>index = (y * SizeZ + z) * SizeX + x</c>,
    /// where y is the layer. This orders memory layer by layer, then row by row, so a single
    /// layer is a contiguous span — which is what almost every hot loop wants (room flood fill,
    /// region build, overlay meshing, the camera slice).
    ///
    /// Nothing anywhere else may compute a cell index by hand. If this formula is duplicated,
    /// it will eventually be duplicated wrongly.
    /// </summary>
    public readonly struct GridSize : IEquatable<GridSize>
    {
        public readonly int SizeX;
        public readonly int SizeZ;
        public readonly int SizeY;

        public GridSize(int sizeX, int sizeZ, int sizeY)
        {
            if (sizeX <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
            if (sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeZ));
            if (sizeY <= 0) throw new ArgumentOutOfRangeException(nameof(sizeY));
            SizeX = sizeX;
            SizeZ = sizeZ;
            SizeY = sizeY;
        }

        /// <summary>Cells in one layer. A layer is contiguous in memory.</summary>
        public int LayerStride => SizeX * SizeZ;

        public int CellCount => SizeX * SizeZ * SizeY;

        public bool Contains(int x, int z, int y) =>
            (uint)x < (uint)SizeX && (uint)z < (uint)SizeZ && (uint)y < (uint)SizeY;

        public bool Contains(CellRef cell) => Contains(cell.X, cell.Z, cell.Y);

        public int Index(int x, int z, int y) => (y * SizeZ + z) * SizeX + x;

        public int Index(CellRef cell) => Index(cell.X, cell.Z, cell.Y);

        public CellRef FromIndex(int index)
        {
            int x = index % SizeX;
            int rest = index / SizeX;
            int z = rest % SizeZ;
            int y = rest / SizeZ;
            return new CellRef(x, z, y);
        }

        /// <summary>
        /// A cell's width and depth on the ground, in millimetres: ADR 0002's 2.5 m, fixed and
        /// irreversible. <b>The simulation's one owner of the cell size</b> (design 47 §3a): until
        /// the pistol nothing simulated needed a metre, because every rule spoke in cells, and a
        /// shot's fall-off is the first that does (<c>RangedGeometry</c>). Presentation's
        /// <c>CellMetrics.SizeXZ</c> is the same number in metres, and
        /// <c>CellSizeHasOneOwnerTests</c> fails the day the two disagree.
        /// </summary>
        public const int CellSizeXZMm = 2500;

        /// <summary>A cell's height, one storey, in millimetres: ADR 0002's 3.0 m. See <see cref="CellSizeXZMm"/>.</summary>
        public const int CellSizeYMm = 3000;

        /// <summary>The scale target from the brief: a 625 m square district, forty layers deep.</summary>
        public static GridSize ScaleTarget => new GridSize(250, 250, 40);

        /// <summary>
        /// How deep every board the new-game page offers is built: 32 layers (design 62 §2d, §5a,
        /// the deep-mining plan's DM2).
        ///
        /// <para><b>One number, read by every place a board's depth is chosen</b> — the page's
        /// sizes (<c>Odyssey.Hud.MapSizes</c>), the scene's inspector default and the play
        /// scene's generator (<c>OdysseyBootstrap.layers</c>, <c>PlayScene.PlayLayers</c>), the
        /// site panel's default — where until 2026-09-26 it was three literals in three
        /// assemblies. The extra layers go underneath: the ground sits at
        /// <c>SizeY − 1 − headroom − relief</c>, so the surface, the sky over it and everything
        /// on it are unchanged, and a valley has about twenty layers of rock under it instead of
        /// three. Unopened rock costs memory and generation time and, since DM1, nothing per
        /// edit. A saved board keeps the depth it was saved at: the header carries its size.</para>
        /// </summary>
        public const int OfferedLayers = 32;

        public bool Equals(GridSize other) => SizeX == other.SizeX && SizeZ == other.SizeZ && SizeY == other.SizeY;
        public override bool Equals(object? obj) => obj is GridSize other && Equals(other);
        public override int GetHashCode() => unchecked((SizeX * 397 ^ SizeZ) * 397 ^ SizeY);
        public override string ToString() => $"{SizeX}x{SizeZ}x{SizeY}";
    }
}
