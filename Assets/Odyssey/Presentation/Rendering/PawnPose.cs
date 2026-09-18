#nullable enable
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where a pawn is drawn this frame, and which way it is facing.
    ///
    /// The simulation is discrete: a pawn occupies one cell and retires integer cost units towards
    /// the next. Determinism requires that and it is not negotiable. Everything here is the facade
    /// over it — the glide between cells, and the extra fraction of a cell the pawn would have
    /// covered in the part-tick this frame sits in.
    ///
    /// It lives on its own because two things now need it: the instanced pass that draws a crowd
    /// as baked meshes, and the live figures that walk. Two copies of this arithmetic would look
    /// identical until the day they disagreed, and the symptom — a pawn's animated figure lagging
    /// half a cell behind where the game thinks it is — would be blamed on the animation.
    /// </summary>
    public static class PawnPose
    {
        /// <summary>
        /// The pawn's drawn position and heading.
        ///
        /// <paramref name="tickAlpha"/> is how far this frame sits between two ticks, 0 to 1, and
        /// <paramref name="movePerTick"/> the cost units a pawn retires in one tick, so a frame
        /// that lands between ticks can carry the pawn on rather than waiting. The extrapolation
        /// is clamped to the cell being entered: running past it would read as a stumble.
        ///
        /// <paramref name="heading"/> is zero for a pawn that is standing still, which callers
        /// must treat as "keep facing wherever you were" rather than as "face north".
        ///
        /// <paramref name="world"/> is what lets a pawn stand on a bank rather than in one; see
        /// <see cref="BankLayout"/>. Null is a pawn on flat cells, which is what the arithmetic
        /// tests want and what a caller with no mirror to hand gets.
        /// </summary>
        public static Vector3 Of(in PawnView pawn, float tickAlpha, int movePerTick,
            out Vector3 heading, WorldRenderModel? world = null)
        {
            Vector3 from = CellMetrics.FloorCentre(pawn.Cell);
            if (pawn.MovePercent <= 0)
            {
                heading = Vector3.zero;
                return GroundRelief.Lift(from) +
                       Vector3.up * (BankLayout.RiseAt(world, pawn.Cell, from.x, from.z) +
                                     WaterLine.FloatRise(world, pawn.Cell));
            }

            Vector3 to = CellMetrics.FloorCentre(pawn.NextCell);
            Vector3 travel = to - from;

            // **A heading is a bearing, and a bearing has no vertical part.**
            //
            // A step that only changes layer travels (0, ±3, 0), and the yaw of that is
            // Atan2(0, 0) — which is not "no bearing", it is zero, which is due north. So every
            // colonist entering a shaft turned slowly to face north and turned back on the way
            // out. That is what the owner saw as the animation jolting about on a descent, and it
            // was never anything to do with what it happened to be climbing.
            //
            // Flattened here rather than in either caller, because both of them want the same
            // thing and a second copy of this arithmetic is how the instanced crowd and the live
            // figures would come to disagree. Zero length already means "keep facing wherever you
            // were", which is exactly right for a climb as well.
            heading = new Vector3(travel.x, 0f, travel.z);

            float percent = pawn.MovePercent + movePerTick * tickAlpha;

            // Along `travel` and not along `heading`: the bearing has had its vertical part taken
            // out on purpose, and a pawn that moved along it would climb a shaft without going
            // down. The lift is taken at the interpolated position, not at either end, so a pawn
            // walks along the drawn ground instead of cutting the chord between two cell centres.
            float t = Mathf.Clamp(percent, 0f, 100f) * 0.01f;
            Vector3 along = GroundRelief.Lift(from + travel * t);

            // **A step with water at either end is drawn by its two ends, not by the ground under
            // it.** Ground-following is right wherever there is ground; between a waterline and
            // the bank above it there is none, and the first version — the float added on top of
            // the ordinary clamp — produced both of the faults the owner then reported (2026-09-17).
            // Leaving a channel, the float decayed evenly across the step while the clamp jumped to
            // the arriving cell at the midpoint, so the figure spent the first half of the step
            // buried in the bank it was climbing ("clipped and sunk half way into a terrain tile")
            // and the second half hanging above it, having overshot by the float it had not yet
            // lost. See WaterLine.VerticalProgress for the curve and the argument.
            if (WaterLine.Crosses(world, pawn.Cell, pawn.NextCell))
                return new Vector3(along.x,
                    WaterLine.CrossingHeight(world, pawn.Cell, pawn.NextCell, t), along.z);

            return OnTheDrawnGround(along, pawn, t, world);
        }

        /// <summary>
        /// The chord between two cell centres, raised onto whatever is drawn under the figure.
        ///
        /// <para><b>Why anything is needed at all.</b> A bank fills the cell it stands in from the
        /// floor to the rim, and that cell is walkable — it is the cell at the foot of a terrace,
        /// which is the take-off cell for the hop the bank is the picture of. A pawn drawn at its
        /// cell's floor is therefore waist-deep in the ramp, for exactly the reason a miner used to
        /// be waist-deep in a quarry.</para>
        ///
        /// <para><b>Which cell the figure is over</b> is the first half's or the second's, split at
        /// the midpoint, because that is where a step crosses the boundary. The two answers agree
        /// at the crossing: <c>BankMesh</c>'s three shapes are built to tile, and the tests that
        /// say so — a straight piece's open edge sits at the floor, a hip's at the floor, and
        /// neighbouring pieces match along the edge they share — are the same tests that make this
        /// continuous for a walker.</para>
        ///
        /// <para><b>A step that changes layer is a hop, and a hop has a shape</b> —
        /// <see cref="HopArc"/>. Both directions were drawn as a straight line raised onto the
        /// ground until 2026-09-18, which made a climb a slide up the bank and a drop a slide back
        /// down it. Now the climb treads the bank in strides and the fall is a fall, and the ground
        /// only ever pushes the result *up*: going down, the fall is allowed to pass below the bank
        /// and the clamp keeps the figure on the slope until the slope drops away faster than it
        /// does, which is one rule where the descent used to need a hand-faded lift.</para>
        ///
        /// <para><b>The clamp is why it cannot show a figure inside the hillside.</b> Climbing, the
        /// chord runs below the ground for the second half of the step — a hop's straight line from
        /// one cell centre to the next passes a metre and a half inside the block being climbed —
        /// and the surface is continuous, so the maximum of the two is too.</para>
        /// </summary>
        static Vector3 OnTheDrawnGround(Vector3 along, in PawnView pawn, float t, WorldRenderModel? world)
        {
            if (world == null) return along;

            CellRef over = t < 0.5f ? pawn.Cell : pawn.NextCell;

            if (IsDrawnAsAHop(world, pawn))
                along = new Vector3(along.x, HopHeight(along, pawn, t, world), along.z);

            // **The relief is sampled where the walker is, not at the cell's centre**, and that
            // distinction is the whole of a fault the owner reported as colonists jolting about
            // (2026-09-17). `along.y` already carries the field at the walker's own position; this
            // used to compare it against the field at the centre of whichever cell she was over,
            // and `over` switches at the midpoint of every step. So on any ground with a slope to
            // it the clamp held the figure flat at the leaving cell's centre height for the first
            // half of the step and then let go — a vertical snap, once a step, everywhere on the
            // board.
            //
            // **Measured before the fix** (`WalkOnReliefTests`): 81.9 mm in one frame, at phase
            // 0.495, on open rolling ground with no bank anywhere near it, against the 25 mm an
            // honest frame of walking moves her. It is worse than a jolt on its own, too:
            // `PawnFigureDirector.ObserveSpeed` differences position frame to frame to drive the
            // gait blend, so a snapped frame reads as a speed spike and can throw the feet into a
            // run as well.
            //
            // **Why it was never caught.** `BankFootingTests` measures exactly this with exactly
            // this instrument, five ways across a terrace, four hundred samples a step — and
            // `GroundRelief.Reset()` sets `Amplitude` to zero, which every one of those cases
            // inherits. At zero amplitude `Lift` returns its argument, the two samples agree, and
            // the switch is invisible. The board the game loads has a 2 m field on it everywhere.
            //
            // The floor term stays keyed to `over`, because that one is *meant* to jump: a hop up
            // changes layer, the chord passes inside the block being climbed, and taking the
            // arriving cell's floor from the midpoint is what lifts the figure onto it.
            float ground = CellMetrics.FloorCentre(over).y +
                           GroundRelief.HeightAt(along.x, along.z) +
                           BankLayout.RiseAt(world, over, along.x, along.z);

            return along.y >= ground ? along : new Vector3(along.x, ground, along.z);
        }

        /// <summary>
        /// Is this step the thing <see cref="HopArc"/> draws — a jump onto the block next door, or
        /// a drop off it?
        ///
        /// <para><b>Both halves of the simulation's own rule, asked of the mirror.</b>
        /// <see cref="NavGraph.IsHop"/> is pure geometry — one layer, one cell across — and geometry
        /// alone is not enough, because a <b>stair</b> step has exactly that shape. The simulation
        /// separates the two with <see cref="NavGraph.UpperEndIsABlockTop"/>: you hop onto ground,
        /// and you take a stair to a storey, so the cell under the upper end has to be solid
        /// terrain rather than a built floor. That is the test repeated here.</para>
        ///
        /// <para><b>Written before it was needed, on purpose.</b> Stairs are not in the game yet
        /// (<c>U44</c>), so today every step of this shape really is a hop and the second clause
        /// changes nothing. The day stairs land, a colonist on one would have been drawn vaulting
        /// up the stairwell, and nothing would have failed — the class of silent fault
        /// <c>docs/bug-patterns.md</c> calls a rule with two owners. <c>HopArcTests</c> covers it
        /// with a floored upper cell.</para>
        /// </summary>
        public static bool IsDrawnAsAHop(WorldRenderModel? world, in PawnView pawn)
        {
            if (world == null) return false;
            if (!NavGraph.IsHop(pawn.Cell, pawn.NextCell)) return false;

            CellRef upper = pawn.NextCell.Y > pawn.Cell.Y ? pawn.NextCell : pawn.Cell;
            if (upper.Y == 0) return false;

            GridSize size = world.Size;
            if (!size.Contains(upper.X, upper.Z, upper.Y)) return false;

            return world.IsSolid(size.Index(upper.X, upper.Z, upper.Y - 1));
        }

        /// <summary>
        /// How high the figure is drawn part way across a hop, before the ground clamp.
        ///
        /// <para><b>Between the two drawn surfaces, not the two cell floors.</b> The ends are the
        /// heights the figure is drawn at while <i>standing</i> in each cell — floor, plus the bank
        /// beneath it — which is exactly what <see cref="Of"/> returns for a pawn that is not
        /// moving. Taking the floors instead would leave the climb a metre and a half short at a
        /// terrace, because a colonist standing in the cell at the foot of one stands half way up
        /// the ramp, and every hop would start and end with a jolt.</para>
        ///
        /// <para><b>The two directions ask different questions, and the difference is the point.</b>
        /// Going up there is a ramp underfoot the whole way, so the height comes from <i>the ground
        /// under the walker</i>, taken in strides — the figure treads the hillside. Going down
        /// there is nothing underfoot after the edge, so the height comes from <i>time</i>: a beat
        /// at the lip and then a fall. A climb that used time ignored the slope it was on, which is
        /// what the owner saw as jumping.</para>
        ///
        /// <para>The relief field is added at the walker's own position rather than at either end,
        /// for the reason recorded in <see cref="OnTheDrawnGround"/>: sampling it anywhere else
        /// puts a vertical snap at the midpoint of the step. The strides are measured without it,
        /// so a stride is a stride up the bank and not up the bank plus whatever the meadow is
        /// doing underneath.</para>
        /// </summary>
        static float HopHeight(Vector3 along, in PawnView pawn, float t, WorldRenderModel world)
        {
            Vector3 from = CellMetrics.FloorCentre(pawn.Cell);
            Vector3 to = CellMetrics.FloorCentre(pawn.NextCell);

            float leaving = from.y + BankLayout.RiseAt(world, pawn.Cell, from.x, from.z);
            float arriving = to.y + BankLayout.RiseAt(world, pawn.NextCell, to.x, to.z);

            float height;
            if (pawn.NextCell.Y > pawn.Cell.Y)
            {
                // The drawn ground under the walker, without the relief field: the same expression
                // OnTheDrawnGround clamps against, which is what keeps the two continuous.
                CellRef over = t < 0.5f ? pawn.Cell : pawn.NextCell;
                float ground = CellMetrics.FloorCentre(over).y +
                               BankLayout.RiseAt(world, over, along.x, along.z);

                // **The chord is the floor under the strides, and it is what a sheer face gets.**
                //
                // Treading works because a bank is a ramp: the ground rises continuously under the
                // walker, so strides taken off it are continuous too. Not every step has one — a
                // bank is refused against rock, inside a working and under a roof — and there the
                // ground under the walker is flat for the first half of the step and then jumps a
                // whole layer at the midpoint, because that is what `over` does. Strides alone
                // would draw that as a colonist standing still and then teleporting three metres.
                //
                // So the height is the higher of the two: where there is a ramp the strides are
                // always above the straight line and the figure treads the hillside; where there is
                // none the straight line carries it and the step is a plain climb up a wall, which
                // is what it looks like from the outside as well. Both agree at both ends, so
                // neither can put a jolt there.
                height = Mathf.Max(HopArc.Stepped(ground, arriving, arriving - leaving),
                                   Mathf.Lerp(leaving, arriving, t));
            }
            else
            {
                height = Mathf.Lerp(leaving, arriving, HopArc.Fall(t));
            }

            return height + GroundRelief.HeightAt(along.x, along.z);
        }

        /// <summary>The yaw a heading implies, in degrees. Zero-length headings give zero.</summary>
        public static float YawOf(Vector3 heading) =>
            heading.sqrMagnitude > 1e-4f ? Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg : 0f;
    }
}
