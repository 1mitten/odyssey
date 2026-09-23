#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where a building drawn from pack art stands (design 32 §14): the generator and the heater,
    /// and whatever machine comes next.
    ///
    /// <para><b>Once, from the head, at the middle of its footprint, turned to its facing.</b> A
    /// two-cell record is two cells pointing at one thing, and a prop drawn per cell would stamp
    /// the generator twice, one in each half. The bed learned the same lesson (<see cref="BedShape"/>);
    /// this is its rule without the bed's three boxes — one model, fitted to the footprint when the
    /// catalogue baked it (<c>ModuleEntry.fitFootprint</c>).</para>
    ///
    /// <para>The mesher and the build cursor both ask here, so the thing under the pointer and the
    /// thing on the board stand in the same place — the rule the bed's and the shelf's shapes keep.
    /// </para>
    /// </summary>
    public static class PropShape
    {
        /// <summary>The draped placement of a prop whose head is this cell, <paramref name="cells"/> long along its facing.</summary>
        public static Matrix4x4 Root(int x, int z, int y, int facing, int cells)
        {
            Vector3 origin = CellMetrics.FloorCentre(x, z, y)
                + new Vector3(Directions.DeltaX[facing], 0f, Directions.DeltaZ[facing])
                  * (CellMetrics.HalfXZ * (cells - 1));
            return GroundRelief.Drape(origin) * Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f));
        }
    }
}
