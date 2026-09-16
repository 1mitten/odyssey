#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One row of the Depth Ruler (A11): the layer, who is on it, how much is built there, and
    /// whether it is the live slice. The ruler is the region with no counterpart in the
    /// reference game and the answer to "which layer am I on".
    /// </summary>
    public struct LayerRow
    {
        public int Layer;
        public int Pawns;

        /// <summary>Built fraction 0..1, or -1 when the frame does not carry that layer's cells.
        /// Only the active slice is published, so only its row fills; the rest stay honestly
        /// empty until the layer summary lands in the contract.</summary>
        public float Occupancy;

        public bool Active;
        public bool Surface;
    }

    /// <summary>
    /// The Depth Ruler's content, top layer first so up is up. Pawn counts come straight from
    /// the frame; occupancy is filled only for the published slice, which is the only layer
    /// whose cells the frame carries — an empty pip on the others is the honest rendering of
    /// "unknown", not a claim that nothing is built there.
    ///
    /// The active row is passed in rather than read from the frame: the slice the camera shows
    /// is presentation state and moves the moment the player asks, while the frame's copy waits
    /// for the next tick — and while paused, layer intents deliberately wait too. Highlighting
    /// the frame's stale copy would make the ruler lag its own click.
    /// </summary>
    public sealed class LayerRulerModel
    {
        public readonly List<LayerRow> Rows = new List<LayerRow>();

        public void Refresh(WorldSnapshot snapshot, int activeLayer, int surfaceLayer)
        {
            Rows.Clear();

            int layers = snapshot.Size.SizeY;
            var counts = new int[layers];
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                int y = pawns[i].Cell.Y;
                if (y >= 0 && y < layers) counts[y]++;
            }

            float publishedOccupancy = 0f;
            var cells = snapshot.SliceCells;
            if (cells.Length > 0)
            {
                int built = 0;
                for (int i = 0; i < cells.Length; i++) if (cells[i] != 0) built++;
                publishedOccupancy = built / (float)cells.Length;
            }

            for (int layer = layers - 1; layer >= 0; layer--)
                Rows.Add(new LayerRow
                {
                    Layer = layer,
                    Pawns = counts[layer],
                    Occupancy = layer == snapshot.SliceLayer ? publishedOccupancy : -1f,
                    Active = layer == activeLayer,
                    Surface = layer == surfaceLayer,
                });
        }
    }
}
