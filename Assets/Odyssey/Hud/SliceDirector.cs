#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// The active layer and the rules for moving it, per <c>09-ui-and-input.md</c> §3 row 16.
    ///
    /// Every way of changing layer — the keys, the Depth Ruler, a roster jump, later an alert —
    /// comes through <see cref="SetLayer"/>, so clamping happens once and the change is announced
    /// once. The camera rig realises the layer by moving its focus height and the renderer by
    /// choosing which chunks to draw; neither decides it. The world is told through an intent by
    /// the composition root, which listens to <see cref="LayerChanged"/> like everyone else.
    /// </summary>
    public sealed class SliceDirector
    {
        public int ActiveLayer { get; private set; }

        public int LayerCount { get; private set; } = 1;

        /// <summary>Raised inside <see cref="SetLayer"/>, with the new layer, only when it changed.</summary>
        public event Action<int>? LayerChanged;

        public void Bind(int layerCount, int startLayer)
        {
            LayerCount = Math.Max(1, layerCount);
            ActiveLayer = Clamp(startLayer);
        }

        /// <summary>Move the slice. Returns false when the layer clamps to the one already active.</summary>
        public bool SetLayer(int layer)
        {
            int next = Clamp(layer);
            if (next == ActiveLayer) return false;
            ActiveLayer = next;
            LayerChanged?.Invoke(ActiveLayer);
            return true;
        }

        public bool Step(int delta) => SetLayer(ActiveLayer + delta);

        int Clamp(int layer) => Math.Max(0, Math.Min(LayerCount - 1, layer));
    }
}
