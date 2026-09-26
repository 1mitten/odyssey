#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where each half of a stair is drawn, in one place: the stair module turned to the climb,
    /// the lower half on the floor and the upper half lifted half a storey (docs/design/63-stairs.md
    /// §9).
    ///
    /// <para><b>One place, for the reason <see cref="BedShape"/> and <see cref="ShelfShape"/> are.</b>
    /// The mesher draws the ruined city's stairs and the colony's built ones, and the build cursor
    /// draws the ghost of one being placed. Three readers of one placement, and three copies would
    /// be two chances for the ghost to climb a different way from the stair it becomes.</para>
    ///
    /// <para><c>climb</c> is a <see cref="Directions"/> value — the way up the flight, lower half
    /// to upper — which for a built stair is the record's facing: the simulation's
    /// <c>EdificeFootprint</c> and <see cref="Directions"/> share one numbering (north +Z, east +X,
    /// south −Z, west −X). The module's local +Z is turned to face the climb.</para>
    /// </summary>
    public static class StairShape
    {
        /// <summary>How far the upper half is lifted above its cell's floor: half a storey.</summary>
        public static float UpperRise => CellMetrics.SizeY * 0.5f;

        /// <summary>The placement of one half of a stair standing in cell (x, z, y).</summary>
        public static Matrix4x4 Half(int x, int z, int y, int climb, bool upper)
        {
            float rise = upper ? UpperRise : 0f;
            return GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y) + Vector3.up * rise) *
                   Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[climb & 3], 0f));
        }
    }
}
