#nullable enable
using System;
using UnityEngine;

namespace Odyssey.Presentation.CameraRig
{
    /// <summary>How the layers above the slice are treated. ADR 0006 ships all six.</summary>
    public enum AboveMode
    {
        /// <summary>Nothing above the slice is drawn. Maximum clarity.</summary>
        Hide = 0,

        /// <summary>Structure only, at the faintest alpha. Stands in for a true outline pass.</summary>
        Ghost = 1,

        /// <summary>One layer above, translucent.</summary>
        XrayMin = 2,

        /// <summary>Default. Every layer above translucent, fading with distance.</summary>
        Xray = 3,

        /// <summary>Solid above, but no roofs or floors, so the player looks into the storey above.</summary>
        RoofsOff = 4,

        /// <summary>No cut-away at all. The exterior and screenshot view.</summary>
        Full = 5,
    }

    /// <summary>How the layers below the slice are treated. An independent axis, per ADR 0006.</summary>
    public enum BelowMode
    {
        Dim = 0,
        Hide = 1,
        Normal = 2,
    }

    /// <summary>
    /// The slice state the renderer reads: which layer is active, and what happens above and below
    /// it.
    ///
    /// This is presentation state, not simulation state, which is why the active layer lives here
    /// and not in the world. The simulation is told about it through a
    /// <c>SetSliceLayer</c> intent so that the published snapshot's per-cell channel matches what
    /// the player is looking at — but the camera does not wait for a tick to redraw, and changing
    /// layers works while the game is paused.
    /// </summary>
    [Serializable]
    public sealed class SliceSettings
    {
        [Tooltip("Treatment of layers above the active one. Never interactive, whatever this says.")]
        public AboveMode above = AboveMode.Xray;

        [Range(1, 8)]
        [Tooltip("How many layers above the slice may be drawn.")]
        public int aboveDepth = 4;

        [Tooltip("Treatment of layers below the active one.")]
        public BelowMode below = BelowMode.Dim;

        [Range(0, 8)]
        [Tooltip("How many layers below the slice are drawn.")]
        public int belowDepth = 3;

        [Range(0.2f, 1f)]
        [Tooltip("Brightness multiplier applied for each layer of depth below the slice.")]
        public float belowFalloff = 0.68f;

        [Range(0.02f, 0.6f)]
        [Tooltip("Opacity of the first layer above the slice.")]
        public float ghostAlpha = 0.20f;

        [Range(0.1f, 1f)]
        [Tooltip("Opacity multiplier applied for each further layer above.")]
        public float ghostFalloff = 0.55f;

        /// <summary>
        /// Suppress the active layer's ceiling slab, which is stored on the layer above.
        ///
        /// This is the difference between seeing a room and seeing a lid. It is a render decision
        /// only: the slab is still there in the simulation, still holds up what is on it and is
        /// still what makes the cell below roofed.
        /// </summary>
        public bool suppressActiveCeiling = true;

        /// <summary>The lowest layer that is drawn at all.</summary>
        public int LowestDrawnLayer(int activeLayer) =>
            below == BelowMode.Hide ? activeLayer : activeLayer - belowDepth;

        /// <summary>The highest layer that is drawn at all.</summary>
        public int HighestDrawnLayer(int activeLayer, int layerCount)
        {
            switch (above)
            {
                case AboveMode.Hide: return activeLayer;
                case AboveMode.XrayMin: return activeLayer + 1;
                case AboveMode.Full:
                case AboveMode.RoofsOff: return layerCount - 1;
                default: return activeLayer + aboveDepth;
            }
        }

        /// <summary>Is a layer above the slice drawn translucent rather than solid?</summary>
        public bool GhostsAbove =>
            above == AboveMode.Ghost || above == AboveMode.Xray || above == AboveMode.XrayMin;

        /// <summary>Opacity for a layer this many steps above the slice.</summary>
        public float AlphaAbove(int steps)
        {
            float alpha = ghostAlpha * Mathf.Pow(ghostFalloff, Mathf.Max(0, steps - 1));
            if (above == AboveMode.Ghost) alpha *= 0.55f;
            return Mathf.Clamp01(alpha);
        }

        /// <summary>Brightness for a layer this many steps below the slice.</summary>
        public float ShadeBelow(int steps)
        {
            if (steps <= 0 || below == BelowMode.Normal) return 1f;
            return Mathf.Clamp(Mathf.Pow(belowFalloff, steps), 0.08f, 1f);
        }
    }
}
