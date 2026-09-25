#nullable enable

using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// How a figure is drawn jumping a one-cell stream, bank to bank, or falling short into it
    /// (design 44 §7). The jump's sibling of <see cref="HopArc"/>, and on the same terms: the
    /// simulation decides that a jump happens, what it costs and where it lands
    /// (<c>NavGraph.IsJumpAcross</c>, <c>NavGraph.JumpCost</c>, <c>Pawn.JumpLanding</c>); this only
    /// decides where the figure is between the two ends. It cannot move a pawn or touch the hash.
    ///
    /// <para><b>The shape is read off the price.</b> A jump costs two cells of walking, so a
    /// quarter of the step is the walk from the bank's centre to its lip — 1.25 m at walking pace
    /// on a square bank, less and slower where the shoreline slopes the bank into the water and
    /// the lip is the last dry ground (<see cref="Lips"/>) — and the last quarter is the same off
    /// the far lip. The middle half is the flight: the
    /// pack's take-off clip at the lip (the gather), the air, and the pack's landing clip on the
    /// far lip (the settle). Every share is a fraction of the step, not a number of seconds, so a
    /// drafted colonist — who runs, and so jumps in half the time — plays the same clips at
    /// twice the rate rather than running out of step before the landing.</para>
    ///
    /// <para><b>The arc is a parabola whose height is gravity's</b> for the time the standard
    /// walker spends in the air, so it cannot drift from the clips: retune the price or the clip
    /// lengths and the apex follows. <c>JumpArcTests</c> pins that.</para>
    /// </summary>
    public static class JumpArc
    {
        /// <summary>The pack's walking take-off, <c>A_Jump_Walking</c>, in seconds (Base Locomotion,
        /// Masc and Femn alike). <c>JumpDrawnTests</c> holds the catalogue's clip to it.</summary>
        public const float TakeOffSeconds = 0.50f;

        /// <summary>The pack's walking landing, <c>A_Land_Walking</c>, in seconds.</summary>
        public const float LandSeconds = 0.40f;

        const float TicksPerSecond = 60f;
        const float Gravity = 9.81f;

        /// <summary>How long a jump takes at the standard walk: its price in ticks, as seconds.</summary>
        public static readonly float StepSeconds = MoveCost.Jump / TicksPerSecond;

        /// <summary>
        /// The share of the step, in time and in distance alike, walked from the bank's centre to
        /// its lip: half a cell at walking pace, over a jump priced as two cells. 0.25.
        /// </summary>
        public static readonly float Approach = 0.5f * MoveCost.Orthogonal / MoveCost.Jump;

        /// <summary>The flight's share of the step: everything between the two lips.</summary>
        public static readonly float Flight = 1f - 2f * Approach;

        /// <summary>How long the flight lasts at the standard walk, in seconds.</summary>
        public static readonly float FlightSeconds = StepSeconds * Flight;

        /// <summary>The take-off clip's share of the flight: the figure gathers at the lip.</summary>
        public static readonly float Gather = TakeOffSeconds / FlightSeconds;

        /// <summary>The landing clip's share of the flight: the figure settles on the far lip.</summary>
        public static readonly float Settle = LandSeconds / FlightSeconds;

        /// <summary>The air's share of the flight: what is left between the two clips.</summary>
        public static readonly float Air = 1f - Gather - Settle;

        /// <summary>How long the standard walker is in the air, in seconds.</summary>
        public static readonly float AirSeconds = FlightSeconds * Air;

        /// <summary>
        /// How far above the straight line between the two lips the arc rises at its middle, in
        /// metres: <c>g t² / 8</c> for a flight of <see cref="AirSeconds"/>, which is what a body
        /// thrown into the air and caught again that much later does.
        /// </summary>
        public static readonly float Apex = Gravity * AirSeconds * AirSeconds / 8f;

        /// <summary>
        /// Is this step drawn as a jump bank to bank? Two cells straight across on one layer, which
        /// is <c>NavGraph.IsJump</c>'s shape: the simulation has no other two-cell step, so the
        /// shape is the whole question (a floored gap is walked as two ordinary steps).
        /// </summary>
        public static bool IsFullJump(in PawnView pawn) =>
            pawn.Moving && NavGraph.IsJump(pawn.Cell, pawn.NextCell);

        /// <summary>Is this step a jump of either kind — clearing the water, or falling short?</summary>
        public static bool IsJump(in PawnView pawn) => IsFullJump(in pawn) || (pawn.Moving && pawn.JumpingShort);

        /// <summary>
        /// How far along the straight line from the bank's centre to the step's far end the figure
        /// is, 0 to 1, at this point through the step. Held at the lip through the gather and at
        /// the far lip through the settle.
        ///
        /// <para>A short jump's far end is the water one cell away rather than the bank two away,
        /// so its lip is half way along instead of a quarter, and it finishes the flight at the
        /// water's centre — where it floats, and where the step ends.</para>
        /// </summary>
        public static float Along(float t, bool short_) =>
            Along(t, short_, short_ ? 0.5f : Approach, short_ ? 1f : 1f - Approach);

        /// <summary>
        /// <see cref="Along(float, bool)"/> with the two lips where the drawn ground puts them, as
        /// shares of the straight line from the bank's centre to the step's far end
        /// (<see cref="Lips"/>). The time shares do not move — only how far the approach walks in
        /// its quarter, and how far the flight carries.
        /// </summary>
        public static float Along(float t, bool short_, float lip, float farLip)
        {
            t = Mathf.Clamp01(t);
            if (t <= Approach) return lip * (t / Approach);
            if (t >= 1f - Approach) return short_ ? 1f : farLip + (1f - farLip) * ((t - (1f - Approach)) / Approach);
            return Mathf.Lerp(lip, farLip, AirProgress(t));
        }

        /// <summary>How far through the flight this is, 0 to 1; nought before and one after.</summary>
        public static float FlightProgress(float t) => Mathf.Clamp01((Mathf.Clamp01(t) - Approach) / Flight);

        /// <summary>
        /// How far through the air this is, 0 to 1: nought through the approach and the gather,
        /// one through the settle and the departure.
        /// </summary>
        public static float AirProgress(float t) => Mathf.Clamp01((FlightProgress(t) - Gather) / Air);

        /// <summary>
        /// Where the figure is drawn, in the world, this far through a jump.
        ///
        /// <para>On the ground under whichever bank it is over while it walks to the lip and away
        /// from the far one — sampled the way <c>PawnPose</c> samples every drawn ground, so a jump
        /// begins and ends exactly where the walk either side of it does — and on the parabola
        /// between the two lips. A short jump's parabola ends on the water line at the water
        /// cell's centre (<see cref="WaterLine.RestingHeight"/>), where a floating figure rests:
        /// the hand-over to the float is nought by construction.</para>
        /// </summary>
        public static Vector3 Position(WorldRenderModel? world, in PawnView pawn, float t)
        {
            bool short_ = !IsFullJump(in pawn);
            Vector3 from = CellMetrics.FloorCentre(pawn.Cell);
            Vector3 to = CellMetrics.FloorCentre(pawn.NextCell);
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);

            Lips(world, in pawn, out float lipS, out float farLipS);
            float s = Along(t, short_, lipS, farLipS);
            Vector3 at = from + flat * s;
            t = Mathf.Clamp01(t);

            if (t <= Approach) return new Vector3(at.x, GroundAt(world, pawn.Cell, at.x, at.z), at.z);

            Vector3 lip = from + flat * lipS;
            float take = GroundAt(world, pawn.Cell, lip.x, lip.z);

            if (!short_ && t >= 1f - Approach)
                return new Vector3(at.x, GroundAt(world, pawn.NextCell, at.x, at.z), at.z);

            float land;
            if (short_) land = WaterLine.RestingHeight(world, pawn.NextCell);
            else
            {
                Vector3 farLip = from + flat * farLipS;
                land = GroundAt(world, pawn.NextCell, farLip.x, farLip.z);
            }

            float a = AirProgress(t);
            return new Vector3(at.x, Height(take, land, a), at.z);
        }

        /// <summary>
        /// The arc between two heights: the straight line from one to the other, with the apex's
        /// parabola over it. Nought and one are exactly the two ends.
        /// </summary>
        public static float Height(float take, float land, float a)
        {
            a = Mathf.Clamp01(a);
            return Mathf.Lerp(take, land, a) + 4f * Apex * a * (1f - a);
        }

        /// <summary>
        /// The drawn ground under a point of one cell: its floor, the relief, and whatever bank
        /// stands in it — <c>PawnPose.OnTheDrawnGround</c>'s own "ground", so the jump and the walk
        /// either side of it agree about where the grass is.
        /// </summary>
        public static float GroundAt(WorldRenderModel? world, CellRef cell, float x, float z) =>
            CellMetrics.FloorCentre(cell).y + GroundRelief.HeightAt(x, z) + BankLayout.RiseAt(world, cell, x, z);

        /// <summary>
        /// How far above the water a lip must stand, in metres: enough that the feet at the gather
        /// and the settle are plainly on grass and not at the water's edge.
        /// </summary>
        public const float DryClearance = 0.20f;

        /// <summary>How finely <see cref="DryReach"/> walks out towards the water: every 5 cm.</summary>
        const int ReachSamples = 25;

        /// <summary>
        /// Where the two lips are for this jump, as shares of the straight line from the bank's
        /// centre to the step's far end: <paramref name="lip"/> the take-off, <paramref name="farLip"/>
        /// the landing (one for a short jump, which comes down at the water's centre).
        ///
        /// <para><b>The lip is the last dry ground, not the cell's edge</b> (owner, 2026-09-25:
        /// <i>"the jump should happen on land … from the ledge"</i>). The shoreline (design 38 §24)
        /// draws a bank sloping into the stream so the water's edge is about 0.7 m from the bank's
        /// centre, and the cell's edge 1.25 m out is 1.5 m down that slope and under the water. A
        /// take-off there walked the figure into the stream to leap out of it. So each lip is
        /// found on the drawn ground itself, which is the only surface that can say where the
        /// grass ends; with no world, or no slope, it is the cell's edge as before.</para>
        /// </summary>
        public static void Lips(WorldRenderModel? world, in PawnView pawn, out float lip, out float farLip)
        {
            bool short_ = !IsFullJump(in pawn);
            float span = short_ ? CellMetrics.SizeXZ : 2f * CellMetrics.SizeXZ;
            CellRef water = short_ ? pawn.NextCell : StreamBetween(world, pawn.Cell, pawn.NextCell);

            Vector3 from = CellMetrics.FloorCentre(pawn.Cell), to = CellMetrics.FloorCentre(pawn.NextCell);
            Vector3 out_ = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;

            lip = DryReach(world, pawn.Cell, out_, water) / span;
            farLip = short_ ? 1f : 1f - DryReach(world, pawn.NextCell, -out_, water) / span;
        }

        /// <summary>
        /// How far from a bank's centre towards the stream, in metres, the drawn ground stays at
        /// least <see cref="DryClearance"/> above the water's surface — at most half a cell, the
        /// cell's edge. The relief is left out of both sides of the comparison because the water
        /// sheet is draped by the same field as the ground under it.
        /// </summary>
        public static float DryReach(WorldRenderModel? world, CellRef bank, Vector3 towards, CellRef water)
        {
            if (world == null || !WaterLine.IsWater(world, water)) return CellMetrics.HalfXZ;

            Vector3 centre = CellMetrics.FloorCentre(bank);
            float surface = CellMetrics.FloorCentre(water).y + WaterLine.SurfaceAbove(world, water);
            float reach = 0f;
            for (int i = 1; i <= ReachSamples; i++)
            {
                float d = CellMetrics.HalfXZ * i / ReachSamples;
                float x = centre.x + towards.x * d, z = centre.z + towards.z * d;
                if (centre.y + BankLayout.RiseAt(world, bank, x, z) < surface + DryClearance) break;
                reach = d;
            }
            return reach;
        }

        /// <summary>
        /// The water cell a full jump clears: the cell between the two banks, a layer down where
        /// the stream is cut into the ground, else level with them.
        /// </summary>
        static CellRef StreamBetween(WorldRenderModel? world, CellRef near, CellRef far)
        {
            var below = new CellRef((near.X + far.X) / 2, (near.Z + far.Z) / 2, near.Y - 1);
            return WaterLine.IsWater(world, below) ? below : new CellRef(below.X, below.Z, near.Y);
        }

        /// <summary>
        /// How much of the figure is off the ground, 0 to 1, for the footing to fade by: through
        /// the whole flight, easing on through the first tenth of the gather and off through the
        /// last tenth of the settle, so the feet are released and planted again continuously.
        /// </summary>
        public static float Airborne(float t, bool short_)
        {
            float f = FlightProgress(t);
            if (f <= 0f) return 0f;
            float on = Smooth(f / (0.1f * Gather));
            if (short_) return on;
            float off = Smooth((1f - f) / (0.1f * Settle));
            return Mathf.Min(on, off);
        }

        /// <summary>
        /// How much of a swimmer a figure falling short is, 0 to 1: nothing through the approach
        /// and most of the air, then coming on over the last third of the air, so it is afloat by
        /// the time it reaches the water line — where <c>WaterLine.Weight</c> takes it, at one.
        /// </summary>
        public static float ShortSwimWeight(float t) => Smooth((AirProgress(t) - 2f / 3f) * 3f);

        /// <summary>Which clip a figure should be showing, and how far into it, this far through a jump.</summary>
        public enum ClipPhase : byte
        {
            /// <summary>No clip: walking to or from the lip.</summary>
            None,
            /// <summary>The take-off clip, at <c>seconds</c> into it (held at its last frame through the air).</summary>
            TakeOff,
            /// <summary>The landing clip, at <c>seconds</c> into it.</summary>
            Land,
        }

        /// <summary>
        /// The clip and its time for this point of a jump. Timed from the step's phase, never by a
        /// clock of its own — the way a sword swing is timed to its tick
        /// (<c>CombatPose.ClipTime</c>) — so a figure that sees a jump for the first frame half way
        /// through draws the right frame of it. A short jump never lands on its feet: through its
        /// settle it holds the take-off's last frame and the swim takes it.
        /// </summary>
        public static ClipPhase Clip(float t, bool short_, out float seconds)
        {
            seconds = 0f;
            float f = FlightProgress(t);
            if (f <= 0f || (f >= 1f && !short_)) return ClipPhase.None;

            if (f < Gather)
            {
                seconds = TakeOffSeconds * (f / Gather);
                return ClipPhase.TakeOff;
            }

            if (short_ || f < Gather + Air)
            {
                seconds = TakeOffSeconds;
                return ClipPhase.TakeOff;
            }

            seconds = LandSeconds * Mathf.Clamp01((f - Gather - Air) / Settle);
            return ClipPhase.Land;
        }

        static float Smooth(float v)
        {
            v = Mathf.Clamp01(v);
            return v * v * (3f - 2f * v);
        }
    }
}
