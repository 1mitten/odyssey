#nullable enable

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The shoreline: where a stream or a pond meets the meadow (design 38 §24).
    ///
    /// <para><b>What was wrong.</b> The water surface is one sheet per water cell, and the ground
    /// stopped at each cell's edge and dropped a wall to the bed, so the edge the player saw was
    /// always a cell's square edge — a diagonal stream came out as a staircase. The shader's shore
    /// fade could not soften it: it traces whatever is under the water, and under the edge was a
    /// wall standing on the grid.</para>
    ///
    /// <para><b>What it is.</b> The bank and the bed are one height field
    /// (<c>BankLayout.ShoreFan</c>), sampled at nine points a cell by how much of the water layer
    /// round each point is water, so the ground crosses the water line on a line of its own
    /// instead of at a cell's edge; the water sheet is laid over each bank too, so there is water
    /// wherever the ground has gone under it; and the shallow and deep water, the marsh and the
    /// damp ground at the water's edge are painted from one small board texture
    /// (<see cref="GroundField"/>) rather than chosen per cell.</para>
    ///
    /// <para><b>Drawing only.</b> No cell, save or hash sees any of it. Every cell's centre stands
    /// where it did — a bank's at its top, a water cell's at its bed — and so does the line between
    /// two dry centres, so where a colonist stands and walks is unchanged; off those, a figure
    /// stands on the drawn slope through the one surface owner (<c>BankLayout.RiseAt</c>).</para>
    /// </summary>
    public static class WaterShore
    {
        /// <summary>Off gives the square shoreline exactly as before: the measurement arm and the
        /// photograph's "before".</summary>
        public static bool Enabled { get; set; } = true;
    }
}
