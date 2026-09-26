#nullable enable
using Odyssey.Sim.Worldgen;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// How high a stair is drawn in each of its two cells, and the <b>one</b> place that is
    /// decided.
    ///
    /// <para><b>A stair climbs, which is the whole of why it needs this.</b> Every other
    /// non-occluding thing the picker knows about is flat: a bed has one height and answers with
    /// it. A stair's two halves are different — the lower rises from the floor to half a layer and
    /// the upper carries on from there to the next floor — so a single shared number would put
    /// half of every flight in the wrong place.</para>
    ///
    /// <para><b>Two copies of a height is how one of them gets corrected on its own</b>, which is
    /// <see cref="BedShape"/>'s own argument and the reason this is not inlined in the mesher. The
    /// numbers are read by <c>ChunkMesher.EmitStair</c>, which lifts the upper half by
    /// <see cref="Rise"/>, and by <c>WorldRenderModel.StandHeight</c>, which offers the picker the
    /// top of each half's own run. The owner reported the consequence of them disagreeing on
    /// 2026-09-21: <i>"I couldn't click on the stairs either to get any information"</i>.</para>
    ///
    /// <para><b>Half a layer per cell is the art's own geometry, not a choice.</b>
    /// <c>SM_Bld_Base_Stairs_01</c> rises precisely 1.50 m over a 2.5 m run, so two chained pieces
    /// climb one 3.0 m layer across two cells — measured in
    /// <c>docs/research/e-01-module-mapping.md</c>, and the reason a stair is two cells on one
    /// layer at all.</para>
    /// </summary>
    public static class StairShape
    {
        /// <summary>How far one half of a flight climbs: half a layer, in metres.</summary>
        public const float Rise = CellMetrics.SizeY * 0.5f;

        /// <summary>
        /// How far the colony's own flight climbs: a <b>whole</b> layer, in its own cell
        /// (2026-09-21).
        ///
        /// <para><b>Also the art's own geometry, not a choice.</b> <c>SM_Bld_Base_Stairs_02</c>
        /// rises precisely 3.00 m over the same 2.5 m run — measured in
        /// <c>docs/research/e-01-module-mapping.md</c>, which listed it as a "steep full-layer
        /// stair in one cell (optional variant)" beside the half-flight it recommended. The owner
        /// asked for the variant after playing the recommendation
        /// (<c>docs/design/60-stairs.md</c> §10).</para>
        ///
        /// <para>So <b>flush</b> is arithmetic rather than a tolerance: the top of the flight and
        /// the floor of the cell above are the same plane, and the picker's top plane
        /// (<see cref="TopOfRun"/>) is that plane too.</para>
        /// </summary>
        public const float FullRise = CellMetrics.SizeY;

        /// <summary>
        /// The top of the flight within this cell, measured from the cell's own floor — or 0 for
        /// anything that is not a stair, so a caller may ask it of any cell.
        ///
        /// <para>The lower half reaches <see cref="Rise"/>; the upper half starts there and reaches
        /// the next floor. It is the <em>top</em> of the run rather than its middle because the
        /// picker claims the plane only where the ray crosses it inside the cell's own footprint:
        /// the far half of each cell is then answered by this plane and the near half by the floor
        /// underneath, which between them approximate the ramp from either end without a second
        /// kind of geometry in the picker.</para>
        /// </summary>
        public static float TopOfRun(ushort edifice) =>
            edifice == CoreContent.EdificeStairFull ? FullRise
            : edifice == CoreContent.EdificeStairLower ? Rise
            : edifice == CoreContent.EdificeStairUpper ? Rise * 2f
            : 0f;

        /// <summary>How far above the cell floor this half of a flight is drawn from.</summary>
        public static float FootOfRun(ushort edifice) =>
            edifice == CoreContent.EdificeStairUpper ? Rise : 0f;
    }
}
