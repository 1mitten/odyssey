#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// What a placement clears while a tool is armed (design 45 §13; owner, 2026-09-25: "the
    /// grass/bushes/foliage is getting in the way of placing any orders — I can't see the
    /// blueprint"). The footprint is the cells the tool would act on — the cell under the pointer,
    /// or the dragged box, or a build's ghosts — as whole-cell rectangles on a layer.
    ///
    /// <para><b>Grass lies flat under it</b>: one soft rectangle per footprint rectangle stamped
    /// into the clearance field the tufts, the stands, the flowers and the ground cover already
    /// read (<see cref="GrassClearance"/>), so a drag of any size costs one stamp, not one per
    /// clump. <b>Bushes and trees over it fade</b> by the see-through machinery the colonists
    /// already use; the owner's "bushes never fade" still holds everywhere else.</para>
    /// </summary>
    public static class PlacementClearing
    {
        /// <summary>How far past the footprint the grass is laid, in metres: enough that a blade
        /// rooted in the next cell does not lean over the ghost's edge.</summary>
        public const float Margin = 0.8f;

        /// <summary>Off draws a placement as it was before §13, for the before photograph.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Stamp the footprint into the field while a tool is armed, and nothing while none is.
        /// Returns how many rectangles were stamped.
        /// </summary>
        public static int Stamp(GrassClearance field, bool armed, IReadOnlyList<(CellRef Min, CellRef Max)> footprint)
        {
            if (!armed || !Enabled) return 0;
            int stamped = 0;
            for (int i = 0; i < footprint.Count; i++)
            {
                (CellRef min, CellRef max) = footprint[i];
                Rect(min, max, out Vector2 low, out Vector2 high);
                // Bare to the footprint's edge and laid flat round it (§13a): a blade lying flat
                // across the ghost still hid it.
                field.CutRect(low, high);
                if (field.StampRect(low, high, Margin)) stamped++;
            }
            return stamped;
        }

        /// <summary>A footprint rectangle's world extent on the ground, in X and Z.</summary>
        public static void Rect(CellRef min, CellRef max, out Vector2 low, out Vector2 high)
        {
            int x0 = Mathf.Min(min.X, max.X), x1 = Mathf.Max(min.X, max.X);
            int z0 = Mathf.Min(min.Z, max.Z), z1 = Mathf.Max(min.Z, max.Z);
            low = new Vector2(x0 * CellMetrics.SizeXZ, z0 * CellMetrics.SizeXZ);
            high = new Vector2((x1 + 1) * CellMetrics.SizeXZ, (z1 + 1) * CellMetrics.SizeXZ);
        }
    }
}
