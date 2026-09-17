#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// A span of cells to draw one closed box around: the corners inclusive, both on one layer.
    /// </summary>
    public readonly struct PreviewBox
    {
        public PreviewBox(CellRef min, CellRef max)
        {
            Min = min;
            Max = max;
        }

        public readonly CellRef Min;
        public readonly CellRef Max;

        /// <summary>How many cells the span covers. For the tests, and for a readout.</summary>
        public int Cells => (Max.X - Min.X + 1) * (Max.Z - Min.Z + 1);

        public override string ToString() =>
            $"({Min.X},{Min.Z},{Min.Y})-({Max.X},{Max.Z},{Max.Y})";
    }

    /// <summary>
    /// What a build drag looks like: the run as one box rather than the cells as many.
    ///
    /// <para><b>Why this is not simply the drag's own rectangle.</b> A build order is lifted onto
    /// the cell standing on solid ground — a click on grass names the ground <em>block</em> and a
    /// wall goes in the air above it (<c>ConstructionGrid.StandingOn</c>) — and that lift is
    /// decided per column. A run that crosses a terrace riser therefore stands on two layers at
    /// once, and one box drawn around all of it would be a box around neither. So the run is
    /// gathered into one box per layer: one box in the ordinary case, two where a wall steps up.
    /// </para>
    ///
    /// <para><b>Unity-free, and here rather than in the renderer, for the reason the whole director
    /// is</b> (ADR 0003): it is a decision about what the player is shown, it is decidable from
    /// numbers alone, and the fast tier can hold it. The caller supplies the lift as a function of
    /// the column, because which cells are solid is the simulation's to know and this assembly
    /// cannot see a grid.</para>
    /// </summary>
    public static class BuildPreview
    {
        /// <summary>
        /// Gather the drag box into one span per layer, in the order the layers are first met
        /// walking the box row by row from its low corner.
        ///
        /// <para>The order is fixed rather than incidental for the same reason the cell order is:
        /// two machines drawing the same drag should submit the same instances in the same order.
        /// Nothing here is hashed, but a draw order that wanders is a diff nobody can read in a
        /// contact sheet.</para>
        ///
        /// <para>A span is the bounding box of the cells found on its layer, so a layer whose
        /// cells are not contiguous — a run crossing a riser and coming back — is drawn as one box
        /// reaching over the gap. That is accepted: it costs a box that is slightly too generous
        /// on ground the order will mostly be refused on anyway, and the alternative is a
        /// connected-component walk for a cursor.</para>
        /// </summary>
        public static void Gather(CellRef min, CellRef max, Func<int, int, int> layerAt,
            List<PreviewBox> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (layerAt == null) throw new ArgumentNullException(nameof(layerAt));
            into.Clear();

            for (int z = min.Z; z <= max.Z; z++)
            for (int x = min.X; x <= max.X; x++)
            {
                int y = layerAt(x, z);

                int at = -1;
                for (int i = 0; i < into.Count; i++)
                    if (into[i].Min.Y == y) { at = i; break; }

                if (at < 0)
                {
                    into.Add(new PreviewBox(new CellRef(x, z, y), new CellRef(x, z, y)));
                    continue;
                }

                PreviewBox box = into[at];
                into[at] = new PreviewBox(
                    new CellRef(Math.Min(box.Min.X, x), Math.Min(box.Min.Z, z), y),
                    new CellRef(Math.Max(box.Max.X, x), Math.Max(box.Max.Z, z), y));
            }
        }
    }
}
