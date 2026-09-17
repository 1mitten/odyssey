#nullable enable
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Where the water is, and how high a colonist floats in it.
    ///
    /// <para><b>The fault this exists to fix.</b> Neither water terrain is solid, so the cell a
    /// colonist occupies in a stream is the water cell and the floor under it is the <em>bed</em>.
    /// Drawn at that floor, the whole figure is 2.16 m under the drawn surface
    /// (<see cref="ChunkMesher.WaterSurface"/> of a 3 m cell) and a 1.8 m colonist walks along the
    /// bottom. The owner reported it twice — "when walking under/through water", then "I saw
    /// someone walk under water again when it was 1 deep … it's meant to float when this shallow
    /// if possible" (2026-09-17).</para>
    ///
    /// <para><b>It is a drawing and nothing else</b> (owner's decision, 2026-09-17: "float is how
    /// it looks; shallow stays crossable"). The pawn is still in its cell for picking, for the
    /// cursor and for every rule, exactly as a figure leaning on a slope is; shallow water remains
    /// ordinary passable ground at the third speed it has always cost
    /// (<c>NaturalContent.CostClassShallowWater</c>). Nothing here may change how fast anybody
    /// walks, what they may carry, or where a path goes. The helpless-swimmer rules belong to
    /// <em>deep</em> water and are designed but not built — <c>docs/design/20-swimming-and-water.md</c>.
    /// </para>
    ///
    /// <para>Its own file, and pure arithmetic over the render mirror, so it can be checked by a
    /// test rather than by looking — the bargain <see cref="Footing"/>, <see cref="WorkSwing"/> and
    /// <see cref="Gesture"/> all make.</para>
    /// </summary>
    public static class WaterLine
    {
        /// <summary>
        /// How far below the drawn surface a floating figure's root sits, in metres.
        ///
        /// <para><b>Measured to the root, which is at the figure's feet — and that is the whole of
        /// why it is about a metre rather than a few centimetres.</b> The first attempt used 0.25 m,
        /// reasoning that a floating body sits just under the surface. It does; but the body is not
        /// at the root. <c>SwimPose</c> tips the figure about its <em>hips</em>, which the animation
        /// leaves roughly 0.9 m above the feet, so a root 0.25 m under the water put the torso
        /// 0.65 m clear of it and the colonist lay on the stream like a raft. Photographed, not
        /// reasoned about: <c>Logs/swim-play.png</c> before and after.</para>
        ///
        /// <para>So the draught is about a hip height, which sinks the body to the waterline and
        /// leaves the head, shoulders and upper back showing. A hip height and not exactly one:
        /// the figures differ in proportion and the director scales them besides, so a number taken
        /// from any one of them would be wrong for the rest. This is the one place in the swim that
        /// is a metre measurement rather than a fraction, and it is why it is a dial.</para>
        /// </summary>
        public static float Draught { get; set; } = 1.0f;

        /// <summary>
        /// The lever, for a session that wants to see the old behaviour. On by default.
        ///
        /// <para>A static rather than a field on a renderer, for the reason
        /// <c>BankLayout.LiftFigures</c> is one: two renderers with their own copy could disagree,
        /// and the disagreement would show up as figures floating in one pass and wading in
        /// another.</para>
        /// </summary>
        public static bool FloatFigures { get; set; } = true;

        public static void Reset()
        {
            Draught = 1.0f;
            FloatFigures = true;
        }

        /// <summary>Is this cell water of either depth?</summary>
        public static bool IsWater(WorldRenderModel? model, CellRef cell) =>
            model != null &&
            model.Size.Contains(cell.X, cell.Z, cell.Y) &&
            NaturalContent.IsWater(model.Terrain(model.Index(cell.X, cell.Z, cell.Y)));

        /// <summary>
        /// How far above a cell's floor the drawn water surface stands, in metres, or zero for a
        /// cell with no water in it.
        ///
        /// <para>Read off <see cref="ChunkMesher.WaterSurface"/> rather than written down again,
        /// because that is the number the mesh is built from: a second copy would put the figure
        /// at a waterline the water is not drawn at, and the two would drift the first time
        /// anybody tuned one of them.</para>
        /// </summary>
        public static float SurfaceAbove(WorldRenderModel? model, CellRef cell) =>
            IsWater(model, cell) ? CellMetrics.SizeY * ChunkMesher.WaterSurface : 0f;

        /// <summary>
        /// How far above a cell's floor a floating figure's root sits — the surface less the
        /// draught — or zero for a dry cell.
        ///
        /// <para>Never negative: a surface shallower than the draught would otherwise push the
        /// figure <em>below</em> the bed it is standing on, which is a worse fault than the one
        /// being fixed and would appear the moment somebody lowered <c>WaterSurface</c>.</para>
        /// </summary>
        public static float FloatRise(WorldRenderModel? model, CellRef cell)
        {
            if (!FloatFigures) return 0f;

            float surface = SurfaceAbove(model, cell);
            if (surface <= 0f) return 0f;

            float rise = surface - Draught;
            return rise > 0f ? rise : 0f;
        }

        /// <summary>
        /// The rise for a figure part way through a step, blended between the cell it is leaving
        /// and the one it is entering.
        ///
        /// <para><b>Blended rather than switched, and that is not a detail.</b> The drawn height
        /// fault this file sits next to was a hard switch at the midpoint of a step, worth 81.9 mm
        /// in one frame; a float that snapped on at the water's edge would be worth <b>1.9 m</b> in
        /// one frame, which is a colonist teleporting. Easing it over the step also happens to be
        /// what entering water looks like.</para>
        /// </summary>
        public static float FloatRise(WorldRenderModel? model, CellRef from, CellRef to, float t) =>
            Mathf.Lerp(FloatRise(model, from), FloatRise(model, to), Mathf.Clamp01(t));

        /// <summary>
        /// Where a figure at rest in this cell is drawn, in world Y: the drawn ground, whatever
        /// bank stands in the cell, and the float on top.
        ///
        /// <para>The three lifts that decide a resting height, gathered in one place so a step
        /// that crosses a waterline can interpolate between two of them instead of trying to
        /// follow a surface that does not exist between them.</para>
        /// </summary>
        public static float RestingHeight(WorldRenderModel? model, CellRef cell)
        {
            Vector3 centre = CellMetrics.FloorCentre(cell);
            return GroundRelief.Lift(centre).y +
                   BankLayout.RiseAt(model, cell, centre.x, centre.z) +
                   FloatRise(model, cell);
        }

        /// <summary>
        /// How far through its vertical change a step is, given which of its ends are wet.
        ///
        /// <para><b>The whole height change happens on the water side of the boundary</b>, and that
        /// is the owner's own description of what it should look like (2026-09-17: "the lift out of
        /// the water happens earlier or reaches the edge and pulls up"). Climbing out, the figure
        /// has finished rising by the time it crosses the edge, so it steps onto the bank at bank
        /// height; getting in, it walks to the edge at bank height and only then goes down. Both
        /// are what a person does, and both keep the figure out of the block it is climbing.</para>
        ///
        /// <para><b>What it replaces was worth two faults at once.</b> The float used to decay
        /// evenly across the step while <c>PawnPose.OnTheDrawnGround</c> clamped to the arriving
        /// cell at the midpoint — so on the way out of a channel the figure spent the first half
        /// of the step <em>below</em> the bank, buried in it, and the second half <em>above</em>
        /// it, having overshot by the float it had not yet lost. The owner reported the first as
        /// "clipped and sunk half way into a terrain tile".</para>
        ///
        /// <para>Smoothstepped, so the rise has no corner in it at either end. A corner in a
        /// vertical motion is a visible tick as the figure passes through it — the same reason
        /// <c>Footing.Correction</c> fades with a smoothstep rather than linearly.</para>
        /// </summary>
        public static float VerticalProgress(bool wetFrom, bool wetTo, float t)
        {
            t = Mathf.Clamp01(t);

            float v =
                wetFrom && !wetTo ? Mathf.Clamp01(t * 2f) :          // out: risen by the edge
                !wetFrom && wetTo ? Mathf.Clamp01(t * 2f - 1f) :     // in: falls after the edge
                t;                                                   // both ends alike

            return v * v * (3f - 2f * v);
        }

        /// <summary>
        /// The drawn height of a figure part way through a step with water at one end or both.
        ///
        /// <para>An interpolation between the two resting heights rather than a walk along the
        /// drawn ground, because <b>between a waterline and a bank top there is no drawn surface to
        /// walk along</b>. Ground-following is right everywhere it has ground; here the two ends
        /// are a metre or more apart in the vertical with nothing in between, and the only honest
        /// answer is a curve chosen to look like what a person does.</para>
        /// </summary>
        public static float CrossingHeight(WorldRenderModel? model, CellRef from, CellRef to, float t) =>
            Mathf.Lerp(RestingHeight(model, from), RestingHeight(model, to),
                VerticalProgress(IsWater(model, from), IsWater(model, to), t));

        /// <summary>Does this step have water at either end, and so want <see cref="CrossingHeight"/>?</summary>
        public static bool Crosses(WorldRenderModel? model, CellRef from, CellRef to) =>
            FloatFigures && (IsWater(model, from) || IsWater(model, to));

        /// <summary>
        /// How much of a swimmer this figure is, 0 on dry land and 1 afloat.
        ///
        /// <para>The same blend the rise uses, expressed as a weight, because the pose has to come
        /// on at exactly the rate the height does — a figure lying prone while still standing on
        /// the bank is the same class of fault as one floating on dry ground.</para>
        /// </summary>
        public static float Weight(WorldRenderModel? model, CellRef from, CellRef to, float t)
        {
            if (!FloatFigures) return 0f;

            bool wetFrom = IsWater(model, from);
            bool wetTo = IsWater(model, to);

            // **The same curve the height uses, so the two cannot disagree.** A figure that is
            // still lying prone while its height has already reached the bank is a colonist
            // sliding onto the grass on its front; one that has stood up while still at the
            // waterline is a colonist standing on the water. Climbing out, both finish at the
            // edge; getting in, both start there.
            return Mathf.Lerp(wetFrom ? 1f : 0f, wetTo ? 1f : 0f,
                VerticalProgress(wetFrom, wetTo, t));
        }
    }
}
