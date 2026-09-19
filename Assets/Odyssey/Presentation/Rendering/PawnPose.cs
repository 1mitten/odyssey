#nullable enable
using System;
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
        ///
        /// <paramref name="otherPawns"/> optionally provides nearby pawns to calculate mutual
        /// right-hand passing lateral offsets.
        /// </summary>
        public static Vector3 Of(in PawnView pawn, float tickAlpha, int movePerTick,
            out Vector3 heading, WorldRenderModel? world = null) =>
            Of(in pawn, tickAlpha, movePerTick, out heading, world, default);

        public static Vector3 Of(in PawnView pawn, float tickAlpha, int movePerTick,
            out Vector3 heading, WorldRenderModel? world,
            ReadOnlySpan<PawnView> otherPawns)
        {
            Vector3 from = CellMetrics.FloorCentre(pawn.Cell);
            if (!pawn.Moving)
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

            // **How far through the step, in percent, at the finest resolution published.**
            //
            // A percent is too coarse to draw with on any step that does not cost 100, and the
            // sub-tick term cannot rescue it: at 60 frames and 60 ticks a second there is about one
            // frame to a tick and the leftover is nearly nought, so the figure advances by whatever
            // the published number advanced. On a 240-tick hop that is a whole point every 2.4
            // ticks — two frames still, then a jump. Measured at 25 mm on the flat, where nobody
            // has ever noticed it, and at 134 mm up a terrace once the climb was drawn in strides.
            // See PawnView.MovePerMille. Zero means the publisher did not say, which is every
            // hand-built fixture, so the percent still answers for them.
            float percent = pawn.MovePerMille > 0
                ? pawn.MovePerMille * 0.1f + movePerTick * tickAlpha
                : pawn.MovePercent + movePerTick * tickAlpha;

            // Along `travel` and not along `heading`: the bearing has had its vertical part taken
            // out on purpose, and a pawn that moved along it would climb a shaft without going
            // down. The lift is taken at the interpolated position, not at either end, so a pawn
            // walks along the drawn ground instead of cutting the chord between two cell centres.
            float t = Mathf.Clamp(percent, 0f, 100f) * 0.01f;

            // **Time is not distance: the step's duration is spread over its drawn path.**
            //
            // A step is priced as a whole and its two halves are not the same journey. Walking on
            // to the foot of a terrace, the first half crosses flat ground and the second climbs
            // 1.5 m of bank; hopping off it, the first half climbs and the second walks the flat
            // top. Spending half the time on each drew the flat halves at a crawl and the climbing
            // halves too fast, which is what the owner reported as being out of sync — "you slow
            // down and then you seem to still go slow on the flat". `StepPace` allocates the time
            // instead: flat ground at a walk, and the slope takes what is left.
            // A water crossing keeps the raw clock. Its vertical profile is a curve of its own
            // (`WaterLine.VerticalProgress`), tuned against the step's progress and played twice
            // in front of the owner; pacing it would move the wading and the climb out of a
            // channel apart from the shape that was judged, to fix a fault that is not there —
            // water is level, so there is no slope in it to spread the time over.
            bool wading = WaterLine.Crosses(world, pawn.Cell, pawn.NextCell);

            StepPace pace = StepPace.Of(world, pawn, travel);
            float s = wading ? t : pace.At(t);
            Vector3 along = GroundRelief.Lift(from + travel * s);

            // Sub-tile lateral steering: veer around oncoming traffic, stationary pawns, and trees.
            Vector3 lateralOffset = Vector3.zero;
            if (heading.sqrMagnitude > 1e-4f)
            {
                // 1. Passing traffic / stationary pawns
                if (otherPawns.Length > 0)
                {
                    for (int i = 0; i < otherPawns.Length; i++)
                    {
                        ref readonly var other = ref otherPawns[i];
                        if (other.Id == pawn.Id) continue;

                        if (other.Moving)
                        {
                            Vector3 otherHeading = new Vector3(
                                CellMetrics.FloorCentre(other.NextCell).x - CellMetrics.FloorCentre(other.Cell).x,
                                0f,
                                CellMetrics.FloorCentre(other.NextCell).z - CellMetrics.FloorCentre(other.Cell).z);

                            if (otherHeading.sqrMagnitude > 1e-4f)
                            {
                                float dot = Vector3.Dot(heading.normalized, otherHeading.normalized);
                                if (dot < -0.5f)
                                {
                                    bool swappingCells = (pawn.Cell == other.NextCell && pawn.NextCell == other.Cell);
                                    bool sharingNext = (pawn.NextCell == other.NextCell);
                                    bool sharingCell = (pawn.Cell == other.Cell);

                                    Vector3 nomA = CellMetrics.FloorCentre(pawn.Cell) + travel * s;
                                    Vector3 otherTravel = CellMetrics.FloorCentre(other.NextCell) - CellMetrics.FloorCentre(other.Cell);
                                    float otherS = Mathf.Clamp01(other.MovePerMille > 0 ? other.MovePerMille * 0.001f : other.MovePercent * 0.01f);
                                    Vector3 nomB = CellMetrics.FloorCentre(other.Cell) + otherTravel * otherS;
                                    float dist = Vector2.Distance(new Vector2(nomA.x, nomA.z), new Vector2(nomB.x, nomB.z));

                                    if (swappingCells || sharingNext || sharingCell || dist < 3.0f)
                                    {
                                        Vector3 passDisp = SteeringCurve.PassingDisplacement(heading, s);
                                        if (passDisp.sqrMagnitude > lateralOffset.sqrMagnitude)
                                            lateralOffset = passDisp;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (other.Cell == pawn.NextCell || other.Cell == pawn.Cell)
                            {
                                Vector3 passDisp = SteeringCurve.PassingDisplacement(heading, s);
                                if (passDisp.sqrMagnitude > lateralOffset.sqrMagnitude)
                                    lateralOffset = passDisp;
                            }
                        }
                    }
                }

                // 2. In-cell static obstacles (e.g. tree trunks)
                if (world != null && world.HasObstacle(pawn.Cell))
                {
                    Vector3 obstDisp = SteeringCurve.ObstacleDisplacement(heading, s);
                    if (obstDisp.sqrMagnitude > lateralOffset.sqrMagnitude)
                        lateralOffset = obstDisp;
                }
            }

            if (lateralOffset.sqrMagnitude > SteeringCurve.HardClampedMax * SteeringCurve.HardClampedMax)
                lateralOffset = lateralOffset.normalized * SteeringCurve.HardClampedMax;

            along += lateralOffset;

            // **A step with water at either end is drawn by its two ends, not by the ground under
            // it.** Ground-following is right wherever there is ground; between a waterline and
            // the bank above it there is none, and the first version — the float added on top of
            // the ordinary clamp — produced both of the faults the owner then reported (2026-09-17).
            // Leaving a channel, the float decayed evenly across the step while the clamp jumped to
            // the arriving cell at the midpoint, so the figure spent the first half of the step
            // buried in the bank it was climbing ("clipped and sunk half way into a terrain tile")
            // and the second half hanging above it, having overshot by the float it had not yet
            // lost. See WaterLine.VerticalProgress for the curve and the argument.
            if (wading)
                return new Vector3(along.x,
                    WaterLine.CrossingHeight(world, pawn.Cell, pawn.NextCell, s), along.z);

            return OnTheDrawnGround(along, pawn, s, world, pace);
        }

        /// <summary>
        /// How a step's time is spread along the path it is drawn over, and the drawn ground at
        /// every point of it.
        ///
        /// <para><b>Three heights are the whole model.</b> The surface at the cell the pawn is
        /// leaving, the surface at the boundary between the two cells, and the surface at the cell
        /// it is entering. Everything between is a straight line — which is exact, because a bank
        /// <i>is</i> a plane (<c>BankMesh.HeightAt</c>), and because a step is half of one cell and
        /// half of the next.</para>
        ///
        /// <para><b>The boundary takes the higher of the two sides</b>, and that is what makes a
        /// sheer face work. Where there is a bank the two agree, since the ramp meets the rim. Where
        /// there is none — a cut rock face, a working, ground under a roof — the lower cell's
        /// surface is its floor and the upper cell's is a layer higher, and a figure that had not
        /// finished climbing by the time it crossed would be inside the block. So it climbs on the
        /// near side of the boundary and walks on the far side of it.</para>
        ///
        /// <para><b>Nothing here is a fall.</b> A drop is timed rather than paced — a body in the
        /// air does not spend longer over the steep part — so a descending hop keeps the raw clock
        /// and <see cref="HopArc.Fall"/> owns its height.</para>
        /// </summary>
        readonly struct StepPace
        {
            readonly float _h0, _h1, _h2, _flatShare;

            /// <summary>True when the step is a drop, which is timed and not paced.</summary>
            public readonly bool Falling;

            StepPace(float h0, float h1, float h2, float flatShare, bool falling)
            {
                _h0 = h0;
                _h1 = h1;
                _h2 = h2;
                _flatShare = flatShare;
                Falling = falling;
            }

            /// <summary>The surface the step ends up on, which is what a climb is climbing to.</summary>
            public float Landing => Mathf.Max(_h0, Mathf.Max(_h1, _h2));

            /// <summary>How far the step climbs in all, or zero when it does not climb.</summary>
            public float Rise => Mathf.Max(0f, Landing - _h0);

            /// <summary>The drawn surface part way along the step, ignoring the relief field.</summary>
            public float GroundAt(float s) => s < 0.5f
                ? Mathf.Lerp(_h0, _h1, s * 2f)
                : Mathf.Lerp(_h1, _h2, (s - 0.5f) * 2f);

            /// <summary>
            /// Where along the step the figure is, at this point of its duration.
            ///
            /// <para>The two halves are given time in proportion to what they cost to cross:
            /// a metre of ground is a metre, and a metre of <i>rise</i> is
            /// <see cref="HopArc.ClimbWeight"/> metres. On flat ground both halves weigh the same
            /// and this is the identity, so an ordinary walk is untouched.</para>
            /// </summary>
            public float At(float t)
            {
                if (_flatShare <= 0f || _flatShare >= 1f) return t;
                return t < _flatShare
                    ? 0.5f * t / _flatShare
                    : 0.5f + 0.5f * (t - _flatShare) / (1f - _flatShare);
            }

            public static StepPace Of(WorldRenderModel? world, in PawnView pawn, Vector3 travel)
            {
                Vector3 from = CellMetrics.FloorCentre(pawn.Cell);
                Vector3 to = CellMetrics.FloorCentre(pawn.NextCell);

                bool falling = pawn.NextCell.Y < pawn.Cell.Y;
                if (world == null) return new StepPace(from.y, from.y, to.y, 0.5f, falling);

                float h0 = from.y + BankLayout.RiseAt(world, pawn.Cell, from.x, from.z);
                float h2 = to.y + BankLayout.RiseAt(world, pawn.NextCell, to.x, to.z);

                float midX = (from.x + to.x) * 0.5f, midZ = (from.z + to.z) * 0.5f;
                float h1 = Mathf.Max(
                    from.y + BankLayout.RiseAt(world, pawn.Cell, midX, midZ),
                    to.y + BankLayout.RiseAt(world, pawn.NextCell, midX, midZ));

                // Half the step's ground distance. A vertical step — a ladder — has none, and
                // weighing its two halves would divide by a rise with nothing to compare it
                // against, so it keeps the raw clock and climbs at the rate the connector's price
                // sets. That is what a ladder is.
                float half = new Vector2(travel.x, travel.z).magnitude * 0.5f;
                if (falling || half <= 1e-4f) return new StepPace(h0, h1, h2, 0.5f, falling);

                float first = half + HopArc.ClimbWeight * Mathf.Max(0f, h1 - h0);
                float second = half + HopArc.ClimbWeight * Mathf.Max(0f, h2 - h1);
                float total = first + second;

                return new StepPace(h0, h1, h2, total > 1e-4f ? first / total : 0.5f, falling);
            }
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
        static Vector3 OnTheDrawnGround(Vector3 along, in PawnView pawn, float s,
            WorldRenderModel? world, in StepPace pace)
        {
            if (world == null) return along;

            CellRef over = s < 0.5f ? pawn.Cell : pawn.NextCell;
            float bare;

            if (pace.Falling && IsDrawnAsAHop(world, pawn))
            {
                // **A fall begins where the ground ends.** Off a bank, the ramp in the arriving
                // cell carries the figure down until it falls away faster than the body does, and
                // the clamp below does that on its own. Off a sheer edge there is no ramp and the
                // clamp holds the figure on the upper floor until the boundary, so a fall timed
                // across the whole step was already 66 cm below the ledge when the clamp let go and
                // snapped there in one frame — 657 mm, measured. Timed into the second half, the
                // release is continuous.
                float fall = BankLayout.At(world, pawn.NextCell).Exists
                    ? HopArc.Fall(s)
                    : HopArc.Fall(Mathf.Clamp01((s - 0.5f) * 2f));

                bare = Mathf.Lerp(pace.GroundAt(0f), pace.GroundAt(1f), fall);
            }
            else
            {
                // **On the surface, sampled where the figure stands.** A bank is a plane and the
                // figure walks on it; nothing computes a climb any more. This is the whole of
                // "motions exactly just above the terrace surface" (owner, 2026-09-19), and every
                // attempt to improve on it — a parabola over the lip, then strides up the treads —
                // was an invention that jolted, because the ramp was already there to be walked on.
                //
                // The model of the step is not used here, only its pacing: three heights and a
                // straight line between them are exact on a straight bank and wrong at a corner,
                // where the surface is two planes (`BankMesh.HeightAt` is a max or a min). Reading
                // the surface itself cannot disagree with the surface.
                bool ramp = BankLayout.At(world, pawn.Cell).Exists ||
                            BankLayout.At(world, pawn.NextCell).Exists;

                // With no ramp there is nothing to sample: the ground under the walker is flat for
                // half the step and a whole layer higher for the other half, because that is when
                // the cell it is over changes. A figure following it would stand still and then
                // teleport 1.51 m, measured. The step's own model climbs instead, finishing by the
                // boundary — which is what hauling yourself onto a ledge looks like anyway.
                bare = ramp
                    ? CellMetrics.FloorCentre(over).y + BankLayout.RiseAt(world, over, along.x, along.z)
                    : pace.GroundAt(s);
            }

            // **The relief is sampled where the walker is, not at the cell's centre**, and that
            // distinction is the whole of a fault the owner reported as colonists jolting about
            // (2026-09-17). It used to compare the height against the field at the centre of
            // whichever cell she was over, and `over` switches at the midpoint of every step. So on
            // any ground with a slope to it the clamp held the figure flat at the leaving cell's
            // centre height for the first half of the step and then let go — a vertical snap, once
            // a step, everywhere on the board.
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
            float height = bare + GroundRelief.HeightAt(along.x, along.z);

            // And the last word belongs to the ground actually drawn there. The three heights the
            // pace is built from are a model of the surface; this is the surface. They agree
            // wherever the model is exact, which is everywhere a bank is a plane, and where they do
            // not the figure is pushed out of the hillside rather than into it.
            float ground = CellMetrics.FloorCentre(over).y +
                           GroundRelief.HeightAt(along.x, along.z) +
                           BankLayout.RiseAt(world, over, along.x, along.z);

            return new Vector3(along.x, Mathf.Max(height, ground), along.z);
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

        /// <summary>The yaw a heading implies, in degrees. Zero-length headings give zero.</summary>
        public static float YawOf(Vector3 heading) =>
            heading.sqrMagnitude > 1e-4f ? Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg : 0f;
    }
}
