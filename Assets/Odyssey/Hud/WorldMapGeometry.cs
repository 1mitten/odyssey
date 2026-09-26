#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Where each hex of the planet is drawn, and which hex a point is over (design 57 §9a, the
    /// specification's geometry): pointy-top hexes 18.4 px across at 1×, rows 0.75 of a hex apart,
    /// odd rows shifted half a hex east. Every coordinate here is in <b>1× map pixels</b>; the painter
    /// multiplies by its supersample and the view by its fit and zoom.
    ///
    /// <para><b>Picking is pure arithmetic</b> (the specification's own requirement), so it is tested
    /// in the fast tier through the wrap and both edges rather than trusted to a hit test.</para>
    /// </summary>
    public readonly struct WorldMapGeometry
    {
        public WorldMapGeometry(int columns, int rows, float hexWidth = WorldLayout.HexWidth)
        {
            Columns = columns;
            Rows = rows;
            HexWidth = hexWidth;
            HexHeight = hexWidth * 2f / Sqrt3;
            RowStep = HexHeight * WorldLayout.HexRowStep;
        }

        const float Sqrt3 = 1.7320508f;

        public readonly int Columns;
        public readonly int Rows;

        /// <summary>Flat side to flat side.</summary>
        public readonly float HexWidth;

        /// <summary>Point to point: the width × 2/√3.</summary>
        public readonly float HexHeight;

        /// <summary>From one row's centres to the next.</summary>
        public readonly float RowStep;

        /// <summary>The corner-to-centre radius.</summary>
        public float Radius => HexHeight / 2f;

        /// <summary>The map's width including the odd rows' half-hex overhang: about 1187 at 64 columns.</summary>
        public float Width => Columns * HexWidth + HexWidth / 2f;

        /// <summary>About 515 at 32 rows.</summary>
        public float Height => (Rows - 1) * RowStep + HexHeight;

        /// <summary>How far east the planet repeats: the columns alone, without the overhang.</summary>
        public float WrapWidth => Columns * HexWidth;

        public float CentreX(int column, int row) => column * HexWidth + ((row & 1) == 1 ? HexWidth : HexWidth / 2f);

        public float CentreY(int row) => row * RowStep + HexHeight / 2f;

        /// <summary>
        /// The tile under a point, or −1 above the top row or below the bottom one. East and west wrap,
        /// so any x names a tile.
        /// </summary>
        public int TileAt(float x, float y)
        {
            // Pixel to axial for pointy-top hexes, origin at the centre of tile (0, 0), then round in
            // cube coordinates and convert to offset rows (odd rows shifted east).
            float px = x - HexWidth / 2f;
            float py = y - HexHeight / 2f;
            float q = (Sqrt3 / 3f * px - py / 3f) / Radius;
            float r = 2f / 3f * py / Radius;
            float s = -q - r;

            int rq = (int)Math.Round(q), rr = (int)Math.Round(r), rs = (int)Math.Round(s);
            float dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;

            if (rr < 0 || rr >= Rows) return -1;
            int column = rq + (rr - (rr & 1)) / 2;
            return HexGrid.Index(HexGrid.Wrap(column, Columns), rr, Columns);
        }

        /// <summary>
        /// The six corners of a tile's hex, grown (or shrunk, for a negative) by <paramref name="grow"/>
        /// px, as x, y pairs clockwise from the top — for the selection and hover outlines.
        /// </summary>
        public float[] Outline(int tile, float grow)
        {
            int column = HexGrid.Column(tile, Columns), row = HexGrid.Row(tile, Columns);
            float cx = CentreX(column, row), cy = CentreY(row);
            float radius = Radius + grow;
            var points = new float[12];
            for (int i = 0; i < 6; i++)
            {
                // Pointy-top: corners at −90° (the top), −30°, 30°, 90°, 150° and 210°.
                double angle = Math.PI / 180.0 * (60 * i - 90);
                points[2 * i] = cx + radius * (float)Math.Cos(angle);
                points[2 * i + 1] = cy + radius * (float)Math.Sin(angle);
            }
            return points;
        }
    }
}
