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
    /// <para><b>From above the top of the world, accelerating.</b> The thing enters above the
    /// highest layer whatever layer it lands on, so a drop on to a rooftop and a drop on to the
    /// meadow both come out of the sky rather than out of a ceiling. The height falls as the
    /// square of the progress: a real fall gathers speed, and a thing sliding down at one rate
    /// reads as a lift, not a drop. Nothing here is a cell, a save or a hash — it is the same
    /// facade over discrete ticks that <see cref="PawnPose"/> is for a walking colonist.</para>
    /// </summary>
    public static class FallArc
    {
        /// <summary>Metres above the top layer's floor that the fall starts from.</summary>
        public const float Clearance = 6f;

        /// <summary>The height above the landing floor at which the fall begins.</summary>
        public static float StartHeight(int worldLayers, int landingLayer) =>
            Mathf.Max(0, worldLayers - landingLayer) * CellMetrics.SizeY + Clearance;

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

        /// <summary>The height at a given progress: the whole start height at 0, nothing at 1.</summary>
        public static float HeightAt(float startHeight, float progress)
        {
            float p = Mathf.Clamp01(progress);
            return startHeight * (1f - p * p);
        }

        public static float HeightAbove(int worldLayers, int landingLayer, int tick, float tickAlpha,
            int launchTick, int landTick) =>
            HeightAt(StartHeight(worldLayers, landingLayer), Progress(tick, tickAlpha, launchTick, landTick));
    }
}
