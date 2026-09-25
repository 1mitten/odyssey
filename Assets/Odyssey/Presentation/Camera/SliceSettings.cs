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
        public bool suppressActiveCeiling;

        /// <summary>
        /// Walls are drawn as stumps and the built storeys above the slice are hidden, this frame
        /// (design 42).
        ///
        /// <para><b>Written once a frame by the composition root and by nothing else</b>, from
        /// <c>Odyssey.Hud.WallsView.Lowered</c> — the player's choice with build mode taken out of
        /// it. It is the answer rather than the choice, so nothing downstream ever works out build
        /// mode for itself: the renderer, the picker, the order marks, the door leaves and every
        /// actor pass ask the three questions below and agree by construction (P1).</para>
        ///
        /// <para>Not serialised: it is the state of this frame, not a setting of the scene. The
        /// choice itself is kept by the settings store.</para>
        /// </summary>
        [NonSerialized] public bool wallsLowered;

        /// <summary>
        /// The player's <b>Walls down</b> choice, before build mode takes it out — what decides
        /// where the ground is (<see cref="BelowSurface"/>), where <see cref="wallsLowered"/>
        /// decides only what is drawn.
        ///
        /// <para><b>Two fields because they are two questions</b> (owner, 2026-09-25). Building
        /// raises the walls so a player can see what they are building; it was never meant to
        /// turn a lower terrace back into a tunnel. While the surface followed
        /// <see cref="wallsLowered"/>, opening the palette on a lower terrace x-rayed the
        /// terrace above and made everything on it unclickable — reported as a campfire one
        /// terrace up that "did nothing" when clicked, and measured by
        /// <c>CampfirePickTests</c>. A player who turns Walls down off keeps the tunnel view the
        /// owner chose for walls up (design 42 §3a).</para>
        ///
        /// <para>Written once a frame by the composition root beside the other two, and not
        /// serialised, for the same reasons.</para>
        /// </summary>
        [NonSerialized] public bool landscapeGround;

        /// <summary>
        /// The layer of the topmost rock in the lowest column of the generated landscape —
        /// <c>WorldRenderModel.LowestOutdoorLayer</c>, handed over once a frame by the composition
        /// root with <see cref="wallsLowered"/>, or -1 before there is a world.
        ///
        /// <para>It is what lets walls-down tell a lower terrace from a tunnel (design 42 §3a).
        /// <see cref="surfaceLayer"/> is the one layer the colony opened on, and the terraced ground
        /// runs several layers below it — on the played board the colony opens on L12 and the
        /// lowest terrace's rock tops out at L8, so its ground is walked on L9 (measured,
        /// <c>LandscapeBandTests.WithTheWallsDownEveryTerraceIsAboveGround</c>). A player standing
        /// on real ground at L10 was "underground" and got the one-layer x-ray, the see-through
        /// building the owner reported on 2026-09-24.</para>
        /// </summary>
        [NonSerialized] public int landscapeFloor = -1;

        /// <summary>
        /// Are walls, doors and pillars drawn as stumps on this layer? On every layer that is drawn
        /// while the walls are down (owner, 2026-09-24): a house standing on a terrace above the
        /// slice shows its plan in stumps exactly as the one on the slice does.
        /// </summary>
        public bool LowersWallsOn(int activeLayer, int layer) => wallsLowered;

        /// <summary>
        /// Is what is <em>stacked</em> on this layer hidden — an upper storey, and whatever stands
        /// on one? Above the slice, and only where the layers above are drawn solid (design 42 §3).
        /// A building standing on the ground of a higher terrace is not stacked and stays. Truly
        /// underground the one layer above is still an x-ray, a ghost nobody can click, and it is
        /// left as it is.
        /// </summary>
        public bool HidesStackedOn(int activeLayer, int layer) =>
            wallsLowered && layer > activeLayer && !GhostsAbove(activeLayer);

        /// <summary>
        /// Is something standing in this cell hidden with the storey it stands on? A colonist on
        /// the upper floor of a house goes with it; one on the ground floor of a house up on a
        /// terrace, or on a hilltop, stays (design 42 §5). The one question every actor pass asks.
        /// </summary>
        public bool HidesStandingAt(int activeLayer, Odyssey.Sim.Contracts.CellRef cell,
            Odyssey.Presentation.World.WorldRenderModel? model) =>
            model != null && HidesStackedOn(activeLayer, cell.Y)
            && model.Size.Contains(cell.X, cell.Z, cell.Y) && model.IsStackedAt(model.Size.Index(cell));

        /// <summary>
        /// Below the opacity at which a ghosted layer is not drawn at all.
        ///
        /// <para>It lived as a literal in <c>ChunkRenderer</c>'s loop. It is named here because
        /// <see cref="HighestVisibleLayer"/> has to agree with it exactly: a figure must be drawn
        /// on every layer the world is drawn on and on no other, or a colonist appears standing
        /// in rock that has faded away.</para>
        /// </summary>
        public const float MinVisibleAlpha = 0.012f;

        /// <summary>
        /// Is the slice underground — below the layer the game opens at? With Walls down chosen
        /// (<see cref="landscapeGround"/>), below every piece of ground instead, whether or not
        /// build mode has raised the walls: a lower terrace is ground, not a tunnel (design 42
        /// §3a). Only a slice beneath the whole landscape keeps the x-ray, because there solid
        /// rock drawn overhead would bury the working the player went down to see.
        /// </summary>
        public bool BelowSurface(int activeLayer) => followDepth && activeLayer < SurfaceFor();

        /// <summary>The layer at and above which the slice counts as above ground.</summary>
        int SurfaceFor() =>
            landscapeGround && landscapeFloor >= 0 ? Math.Min(surfaceLayer, landscapeFloor + 1) : surfaceLayer;

        /// <summary>
        /// The treatment above the slice, after <see cref="followDepth"/> has had its say.
        /// Everything that asks about the layers above goes through here.
        /// </summary>
        public AboveMode AboveAt(int activeLayer) =>
            !followDepth ? above
            : BelowSurface(activeLayer) ? AboveMode.XrayMin
            : AboveMode.Full;

        /// <summary>
        /// The lowest layer that is drawn at all.
        ///
        /// <para><paramref name="lowestOutdoorLayer"/> is the floor of the open landscape —
        /// <c>WorldRenderModel.LowestOutdoorLayer</c>, the lowest layer any column's surface sits
        /// on. The default means "the caller knows of no landscape", which is what every call site
        /// meant before the floor existed.</para>
        /// </summary>
        public int LowestDrawnLayer(int activeLayer, int lowestOutdoorLayer = int.MaxValue)
        {
            if (below == BelowMode.Hide) return activeLayer;

            // Underground, the cap comes off: the shape of a working is what is beneath you, and
            // the dimming falloff is what keeps the active layer standing out from it.
            if (BelowSurface(activeLayer)) return 0;

            int budget = activeLayer - belowDepth;
            if (!followDepth) return budget;

            // **The landscape is never cut away** (owner, 2026-09-16: on low ground under the
            // trees "there appears to be no ground texture or grass"). The board is terraced, so
            // the outdoor surface spans five layers and only one of them is the active one; a
            // budget of three below the slice therefore deletes the bottom of a hillside, and what
            // shows through the gap is the skybox with the wood still standing over it. Measured
            // on the played board at seed 1: the ground runs L8 to L12, the colony opens on L12,
            // and a slice one layer above that lost 6,140 of 14,400 columns.
            //
            // The depth budget is a cue for looking *through* the world — down a shaft, into a
            // room, over the lip of a quarry — and it keeps all of that, because the floor handed
            // in is the landscape as it was generated rather than as it has since been dug. It is
            // the same distinction TintCode.DaylitBase draws for the depth *shade*, one step
            // earlier: there the fix was that a lower terrace must not be dim, here it is that a
            // lower terrace must exist at all.
            return lowestOutdoorLayer < budget ? lowestOutdoorLayer : budget;
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
        /// The highest layer a click may land on.
        ///
        /// <para><b>Solid is clickable; a ghost never is.</b> ADR 0006 originally said nothing
        /// above the slice may be a pointer target, and the owner took that up on 2026-09-16:
        /// <i>"I couldn't select the stones for mining … I should be able to click on an object in
        /// 3D space"</i>. The part of the old rule that was really carrying the weight is that a
        /// translucent hint of a wall is a depth cue, and clicking a depth cue is Going Medieval's
        /// misclick complaint. Drawn at full opacity it is not a cue, it is the world.</para>
        ///
        /// <para>So this follows <see cref="AboveAt"/> exactly: above the surface, where every
        /// layer is drawn solid, the whole stack is selectable up to the fade bound; underground,
        /// where the one layer above is x-rayed, a click cannot leave the active layer upwards and
        /// the old behaviour is unchanged.</para>
        /// </summary>
        public int HighestSelectableLayer(int activeLayer, int layerCount) =>
            GhostsAbove(activeLayer) ? activeLayer : HighestVisibleLayer(activeLayer, layerCount);

        /// <summary>
        /// The lowest layer a click may land on — every layer drawn below the slice.
        ///
        /// <para>Dimmed is not ghosted: a layer below is drawn opaque and merely darker, and it is
        /// only reachable by a ray at all where nothing above it occludes — through a shaft, over
        /// a cliff, down a stairwell. Which is exactly where a player means to click it.</para>
        ///
        /// <para>It follows <see cref="LowestDrawnLayer"/> exactly, landscape floor and all, for
        /// the standing reason that "selectable" and "drawn solid" must not drift apart: a terrace
        /// the player can see is a terrace they can mark for mining.</para>
        /// </summary>
        public int LowestSelectableLayer(int activeLayer, int lowestOutdoorLayer = int.MaxValue) =>
            Mathf.Max(0, LowestDrawnLayer(activeLayer, lowestOutdoorLayer));

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
