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
        /// <summary>
        /// The layer the game opens at — the surface the colony starts on. Anything below it is
        /// underground. Set from the world's start cell; it is a render-side constant, not a
        /// simulation fact.
        /// </summary>
        [Tooltip("The layer the game opens at. Below it is underground.")]
        public int surfaceLayer;

        /// <summary>
        /// Let the depth of the slice choose the treatment, rather than the fields below
        /// (owner, 2026-09-16). **This is the default and it is what a player meets.**
        ///
        /// <para><b>At or above the surface: everything above is drawn, and drawn SOLID.</b> The
        /// player is looking at the world from outside and wants to see all of it — the owner's
        /// words, after the first attempt left it x-rayed: "this includes everything buildings,
        /// stones, rocks and everything, as I noticed the mining rocks were transparent". An
        /// outcrop rising two cells above the surface is a rock, not a hint of one. The depth cap
        /// does not apply and neither does the fade.</para>
        ///
        /// <para><b>Below the surface: one layer above, and every layer below.</b> Underground the
        /// question reverses. What is overhead is a ceiling and one layer of it is all the context
        /// that helps; what is *under* you is the shape of the working, and a base three storeys
        /// deep is unreadable through a three-layer cap. So the cap comes off downwards and goes
        /// on upwards.</para>
        ///
        /// <para>The six ADR 0006 modes are untouched and still ship. This decides what the
        /// player gets when they have not chosen one: switch it off and every field below is
        /// obeyed exactly as before, which is what an explicit mode choice will do.</para>
        /// </summary>
        [Tooltip("Let the slice's depth choose the treatment: everything above at the surface, " +
                 "one above and everything below when underground. Off obeys the fields below.")]
        public bool followDepth = true;

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

        /// <summary>
        /// Opacity of the first layer above the slice.
        ///
        /// <para>Raised from 0.20 to 0.38 on the owner's report that rock above the working layer
        /// could not be seen at all. The arithmetic says why: at 0.20 with a falloff of 0.55 a
        /// stone one layer up drew at a fifth, two up at a ninth and three up at a sixteenth, and
        /// the fourth layer — the last <see cref="aboveDepth"/> allows — was within a whisker of
        /// the 0.012 cutoff that drops it entirely. The cut-away exists so a player can see what is
        /// over their head; at those numbers it was only proving that something was.</para>
        /// </summary>
        [Range(0.02f, 0.6f)]
        [Tooltip("Opacity of the first layer above the slice.")]
        public float ghostAlpha = 0.38f;

        /// <summary>
        /// Opacity multiplier applied for each further layer above.
        ///
        /// <para>0.72 rather than 0.55, so the four layers <see cref="aboveDepth"/> draws run
        /// 0.38, 0.27, 0.20, 0.14 instead of 0.20, 0.11, 0.06, 0.03. Each is still plainly
        /// subordinate to the solid layer being worked, which is the thing the cut-away must not
        /// give up.</para>
        /// </summary>
        [Range(0.1f, 1f)]
        [Tooltip("Opacity multiplier applied for each further layer above.")]
        public float ghostFalloff = 0.72f;

        /// <summary>
        /// Suppress the active layer's ceiling slab, which is stored on the layer above.
        ///
        /// This is the difference between seeing a room and seeing a lid. It is a render decision
        /// only: the slab is still there in the simulation, still holds up what is on it and is
        /// still what makes the cell below roofed.
        /// </summary>
        public bool suppressActiveCeiling = true;

        /// <summary>
        /// Below the opacity at which a ghosted layer is not drawn at all.
        ///
        /// <para>It lived as a literal in <c>ChunkRenderer</c>'s loop. It is named here because
        /// <see cref="HighestVisibleLayer"/> has to agree with it exactly: a figure must be drawn
        /// on every layer the world is drawn on and on no other, or a colonist appears standing
        /// in rock that has faded away.</para>
        /// </summary>
        public const float MinVisibleAlpha = 0.012f;

        /// <summary>Is the slice underground — below the layer the game opens at?</summary>
        public bool BelowSurface(int activeLayer) => followDepth && activeLayer < surfaceLayer;

        /// <summary>
        /// The treatment above the slice, after <see cref="followDepth"/> has had its say.
        /// Everything that asks about the layers above goes through here.
        /// </summary>
        public AboveMode AboveAt(int activeLayer) =>
            !followDepth ? above
            : BelowSurface(activeLayer) ? AboveMode.XrayMin
            : AboveMode.Full;

        /// <summary>The lowest layer that is drawn at all.</summary>
        public int LowestDrawnLayer(int activeLayer)
        {
            if (below == BelowMode.Hide) return activeLayer;

            // Underground, the cap comes off: the shape of a working is what is beneath you, and
            // the dimming falloff is what keeps the active layer standing out from it.
            if (BelowSurface(activeLayer)) return 0;

            return activeLayer - belowDepth;
        }

        /// <summary>The highest layer that is drawn at all, before the fade cutoff.</summary>
        public int HighestDrawnLayer(int activeLayer, int layerCount)
        {
            switch (AboveAt(activeLayer))
            {
                case AboveMode.Hide: return activeLayer;
                case AboveMode.XrayMin: return activeLayer + 1;
                case AboveMode.Full:
                case AboveMode.RoofsOff: return layerCount - 1;

                // Following the depth at or above the surface means every layer above, not
                // aboveDepth of them. What actually bounds it is the fade — see
                // HighestVisibleLayer — rather than a count nobody chose.
                default: return followDepth ? layerCount - 1 : activeLayer + aboveDepth;
            }
        }

        /// <summary>
        /// The highest layer anything is drawn on, the ghost cutoff included — which is the one
        /// every actor should be culled against.
        ///
        /// <para><see cref="HighestDrawnLayer"/> can say "all of them" while the fade has long
        /// since taken the geometry to nothing. Chunks handle that by skipping the layer inside
        /// the loop; a pawn is drawn solid and would not, so it needs the answer up front.</para>
        /// </summary>
        public int HighestVisibleLayer(int activeLayer, int layerCount)
        {
            int highest = Mathf.Min(layerCount - 1, HighestDrawnLayer(activeLayer, layerCount));
            if (!GhostsAbove(activeLayer)) return highest;

            while (highest > activeLayer && AlphaAbove(activeLayer, highest - activeLayer) < MinVisibleAlpha)
                highest--;

            return highest;
        }

        /// <summary>
        /// Should the active layer's ceiling — the slab stored on the layer above — be dropped?
        ///
        /// <para><b>`Full` normally keeps its lid and the depth-following default does not,
        /// although both draw everything above solid.</b> They mean different things. `Full` is the
        /// exterior and screenshot view, deliberately the control case with no cut-away anywhere;
        /// the default is "let me see the world", and "the active layer is drawn roofless" is a
        /// separate standing decision (<c>06-rendering-and-camera.md</c> §3 point 2) that it has no
        /// business quietly reversing.</para>
        ///
        /// <para>Nothing changes outdoors either way, because open ground has no slab over it.
        /// It changes as soon as anything is built, which is why it is settled now rather than
        /// discovered then.</para>
        /// </summary>
        public bool SuppressCeilingAt(int activeLayer) =>
            suppressActiveCeiling && (followDepth || AboveAt(activeLayer) != AboveMode.Full);

        /// <summary>Is a layer above the slice drawn translucent rather than solid?</summary>
        public bool GhostsAbove(int activeLayer)
        {
            AboveMode mode = AboveAt(activeLayer);
            return mode == AboveMode.Ghost || mode == AboveMode.Xray || mode == AboveMode.XrayMin;
        }

        /// <summary>Opacity for a layer this many steps above the slice.</summary>
        public float AlphaAbove(int activeLayer, int steps)
        {
            float alpha = ghostAlpha * Mathf.Pow(ghostFalloff, Mathf.Max(0, steps - 1));
            if (AboveAt(activeLayer) == AboveMode.Ghost) alpha *= 0.55f;
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
