#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where along its fall a thing in the air is drawn (design 23 §6). The simulation owns the
    /// flight — the tick it launched, the tick it lands, the cell it lands in — and publishes it
    /// as a <see cref="Sim.Contracts.FallingView"/>; this is the whole of what presentation adds,
    /// which is a height for the frame in hand.
    ///
    /// <para><b>From above the camera, at one speed.</b> The first cut fell from just above the
    /// top layer in two seconds, gathering speed like a dropped stone, and the owner saw it land
    /// almost before it had been seen falling (2026-09-20). It now starts
    /// <see cref="DropHeight"/> above its landing floor — above the play camera at any zoom, so
    /// it is always seen arriving — and comes down at a constant rate, the way a crate under a
    /// chute does. The duration is the Def's (<c>fallTicks</c>); the height is this constant,
    /// because nothing in the simulation cares how high the drawing starts and a number only
    /// presentation reads belongs in presentation. Nothing here is a cell, a save or a hash — it
    /// is the same facade over discrete ticks that <see cref="PawnPose"/> is for a walking
    /// colonist.</para>
    /// </summary>
    public static class FallArc
    {
        /// <summary>
        /// Metres above the landing floor the fall starts from. The play camera sits 32–160 m up
        /// and looks down at 48°, so a thing 120 m over its target enters from beyond the top of
        /// the frame at every zoom rather than popping into view part-way down.
        /// </summary>
        public const float DropHeight = 120f;

        /// <summary>Metres above the top layer's floor the fall starts from on a world taller than <see cref="DropHeight"/>.</summary>
        public const float Clearance = 6f;

        /// <summary>
        /// The height above the landing floor at which the fall begins: <see cref="DropHeight"/>,
        /// or above the top of the world if the world is taller than that.
        /// </summary>
        public static float StartHeight(int worldLayers, int landingLayer) =>
            Mathf.Max(DropHeight, Mathf.Max(0, worldLayers - landingLayer) * CellMetrics.SizeY + Clearance);

        /// <summary>
        /// How far through the fall the frame is, 0 at launch and 1 at landing, clamped. The
        /// snapshot's tick is the tick just completed and <paramref name="tickAlpha"/> is how far
        /// the frame sits towards the next, exactly as a pawn's step is carried on.
        /// </summary>
        public static float Progress(int tick, float tickAlpha, int launchTick, int landTick)
        {
            if (landTick <= launchTick) return 1f;
            float elapsed = tick - launchTick + Mathf.Clamp01(tickAlpha);
            return Mathf.Clamp01(elapsed / (landTick - launchTick));
        }

        /// <summary>The height at a given progress: the whole start height at 0, nothing at 1, and a straight line between.</summary>
        public static float HeightAt(float startHeight, float progress) =>
            startHeight * (1f - Mathf.Clamp01(progress));

        public static float HeightAbove(int worldLayers, int landingLayer, int tick, float tickAlpha,
            int launchTick, int landTick) =>
            HeightAt(StartHeight(worldLayers, landingLayer), Progress(tick, tickAlpha, launchTick, landTick));
    }
}
