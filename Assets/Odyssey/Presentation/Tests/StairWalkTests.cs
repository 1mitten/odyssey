#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// How a figure is drawn climbing the colony's one-cell stair.
    ///
    /// <para><b>Drawn up the middle of the staircase until 2026-09-21.</b> The connector joins a
    /// cell to the cell above it, so the step is <c>(0, +3, 0)</c> and the ordinary rule lerped
    /// between two cell centres: no forward motion, no gait, no contact with the art. The owner:
    /// <i>"the animation for going upstairs is terrible it needs to ground 2 flights of stairs and
    /// make sure the feet get onto each step and surface as it climbs."</i></para>
    ///
    /// <para><b>The oracle is the art, measured.</b> <c>StairCheck</c> bins
    /// <c>SM_Bld_Base_Stairs_02</c>'s placed vertices over the cell footprint and prints the top
    /// surface; the samples below are read off that grid. Testing the path against its own
    /// constants would only say the numbers had not been retyped — these say the figure is on the
    /// stair.</para>
    ///
    /// <para><b>And smoothness is asserted, because the obvious fix is the rejected one.</b>
    /// Quantising the height to the sixteen treads would put a foot on every step and jolt the
    /// figure 0.1875 m sixteen times a flight. <see cref="HopArc"/> records that exact rhythm being
    /// built for the terrace climb and taken out again at the owner's request.
    /// <see cref="AClimbNeverJoltsTheFigure"/> is what keeps it out.</para>
    /// </summary>
    public class StairWalkTests
    {
        /// <summary>The composition root's fixed tick rate — <c>OdysseyBootstrap.ticksPerSecond</c>.</summary>
        const int TicksPerSecond = 60;

        static readonly CellRef Cell = new CellRef(6, 6, 3);

        static Vector3 Local(Vector3 world) => world - CellMetrics.FloorCentre(Cell);

        static Vector3 Up(float t)
        {
            Vector3 p = StairWalk.At(Cell, facing: 0, up: true, t, out _);
            return Local(p);
        }

        // ---- the path is on the art ---------------------------------------------------------

        /// <summary>
        /// Both ends are cell centres, so the step before and the step after join without a tear.
        ///
        /// <para>The flight's own foot is over at +X and would be the tidier place to start; a path
        /// that began there would jump the figure a metre sideways the instant the climb started,
        /// which is the class of fault <c>PawnPose</c> already carries three comments about.</para>
        /// </summary>
        [Test]
        public void TheClimbBeginsAndEndsAtACellCentre()
        {
            Assert.That(Up(0f).x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Up(0f).z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Up(0f).y, Is.EqualTo(0f).Within(1e-4f));

            Assert.That(Up(1f).x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Up(1f).z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Up(1f).y, Is.EqualTo(CellMetrics.SizeY).Within(1e-4f),
                "a stair that does not arrive at the floor above is not flush");
        }

        /// <summary>
        /// <b>The figure stays inside the cell the stair is in.</b> A switchback is the whole reason
        /// a full layer fits one square; a path that wandered outside it would be walking on the
        /// neighbour's floor, or on nothing.
        /// </summary>
        [Test]
        public void TheWholeClimbStaysInsideItsOwnCell()
        {
            for (int i = 0; i <= 400; i++)
            {
                Vector3 p = Up(i / 400f);
                Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(CellMetrics.HalfXZ + 1e-3f),
                    $"left the cell in x at t={i / 400f:0.000}");
                Assert.That(Mathf.Abs(p.z), Is.LessThanOrEqualTo(CellMetrics.HalfXZ + 1e-3f),
                    $"left the cell in z at t={i / 400f:0.000}");
            }
        }

        /// <summary>
        /// <b>The measured surface, sampled.</b> Read off <c>StairCheck</c>'s grid: the lower flight
        /// runs along the south half climbing toward −X, the landing is the −X strip at half a
        /// layer, and the upper flight runs along the north half climbing back toward +X.
        ///
        /// <para>Stated as "while the figure is below the landing it is on the south side, and while
        /// it is above the landing it is on the north side" rather than as a list of coordinates,
        /// because that is the claim — it is the switchback that a straight lerp got wrong.</para>
        /// </summary>
        [Test]
        public void TheLowerFlightIsSouthAndTheUpperFlightIsNorth()
        {
            bool sawSouth = false, sawNorth = false;

            for (int i = 0; i <= 400; i++)
            {
                float t = i / 400f;
                Vector3 p = Up(t);

                // Clear of both the start and the end, where the path is crossing the cell centre.
                if (p.y > 0.3f && p.y < StairWalk.Landing - 0.2f)
                {
                    Assert.That(p.z, Is.LessThan(0f), $"the lower flight is the south half (t={t:0.000})");
                    sawSouth = true;
                }

                if (p.y > StairWalk.Landing + 0.2f && p.y < CellMetrics.SizeY - 0.3f)
                {
                    Assert.That(p.z, Is.GreaterThan(0f), $"the upper flight is the north half (t={t:0.000})");
                    sawNorth = true;
                }
            }

            Assert.That(sawSouth, Is.True, "no part of the climb was on the lower flight");
            Assert.That(sawNorth, Is.True, "no part of the climb was on the upper flight");
        }

        /// <summary>
        /// The landing is crossed at exactly half a layer, and it is really crossed — the figure
        /// goes from one side of the cell to the other while level.
        /// </summary>
        [Test]
        public void TheLandingIsFlatAndIsWalkedAcross()
        {
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool any = false;

            for (int i = 0; i <= 800; i++)
            {
                Vector3 p = Up(i / 800f);
                if (Mathf.Abs(p.y - StairWalk.Landing) > 1e-3f) continue;

                any = true;
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }

            Assert.That(any, Is.True, "the climb never levels off, so there is no landing in it");
            Assert.That(maxZ - minZ, Is.GreaterThan(0.8f),
                "the landing is stepped on rather than walked across, so the turn is a pivot");
        }

        /// <summary>A climb only ever goes up. A path that dipped would read as a stumble.</summary>
        [Test]
        public void TheClimbNeverGoesDown()
        {
            float previous = float.MinValue;
            for (int i = 0; i <= 800; i++)
            {
                float y = Up(i / 800f).y;
                Assert.That(y, Is.GreaterThanOrEqualTo(previous - 1e-4f),
                    $"the figure lost height at t={i / 800f:0.000}");
                previous = y;
            }
        }

        // ---- and it is smooth, which is the part with a history ------------------------------

        /// <summary>
        /// <b>No jolt, anywhere, and this is the test that keeps the obvious fix out.</b>
        ///
        /// <para>Quantising to the sixteen treads would put a foot on every step and move the figure
        /// 0.1875 m between two frames, sixteen times a flight. <c>HopArc</c> records the owner
        /// rejecting exactly that rhythm on the terrace — <i>"it jolts and jitters the colonists at
        /// certain points; smoother is preferred and predictable"</i> — and <c>PawnPose</c> records
        /// 81.9 mm in one frame as a reported bug against the 25 mm an honest frame of walking
        /// moves.</para>
        ///
        /// <para>Measured at a stair's own price: <c>MoveCost.StairUp</c> is 290 ticks, so a frame
        /// at 60 Hz is about 1/290 of the step. The bound is generous — this is a guard against a
        /// snap, not a tuning knob.</para>
        /// </summary>
        [Test]
        public void AClimbNeverJoltsTheFigure()
        {
            const int frames = 290;
            Vector3 previous = Up(0f);
            float worst = 0f;
            float worstAt = 0f;

            for (int i = 1; i <= frames; i++)
            {
                float t = i / (float)frames;
                Vector3 now = Up(t);
                float moved = Vector3.Distance(previous, now);
                if (moved > worst) { worst = moved; worstAt = t; }
                previous = now;
            }

            Assert.That(worst, Is.LessThan(0.08f),
                $"the figure moved {worst * 1000f:0} mm in one frame at t={worstAt:0.000}; a tread "
                + "is 187 mm and quantising to it is the fix this test exists to refuse");
        }

        /// <summary>
        /// <b>One steady speed from bottom to top</b>, which is what the owner asked for on the
        /// terrace and got. The landing is flat and the flights climb, so an even share of the clock
        /// per segment would cross the landing at a crawl and take the flights at a run.
        /// </summary>
        [Test]
        public void TheClimbIsAtOneSteadySpeed()
        {
            const int frames = 290;
            float fastest = 0f, slowest = float.MaxValue;
            Vector3 previous = Up(0f);

            for (int i = 1; i <= frames; i++)
            {
                Vector3 now = Up(i / (float)frames);
                // Weighted the way the path is paced: a metre of rise is ClimbWeight metres of
                // ground. Measured in plain metres the flights would honestly read slower than the
                // landing, which is the point of the weighting rather than a fault in it.
                float ground = new Vector2(now.x - previous.x, now.z - previous.z).magnitude;
                float weighted = ground + HopArc.ClimbWeight * Mathf.Abs(now.y - previous.y);
                fastest = Mathf.Max(fastest, weighted);
                slowest = Mathf.Min(slowest, weighted);
                previous = now;
            }

            Assert.That(fastest / Mathf.Max(slowest, 1e-5f), Is.LessThan(1.6f),
                "the climb changes pace along its length");
        }

        // ---- facing, and the way back down ----------------------------------------------------

        /// <summary>
        /// <b>The figure turns on the landing rather than climbing the second flight backwards.</b>
        /// The heading is the segment's own ground direction, so it reverses in x across the turn.
        /// </summary>
        [Test]
        public void TheFigureTurnsRoundOnTheLanding()
        {
            StairWalk.At(Cell, facing: 0, up: true, 0.3f, out Vector3 lower);
            StairWalk.At(Cell, facing: 0, up: true, 0.85f, out Vector3 upper);

            Assert.That(lower.x, Is.LessThan(0f), "the lower flight climbs toward -X");
            Assert.That(upper.x, Is.GreaterThan(0f), "the upper flight climbs back toward +X");

            Assert.That(lower.y, Is.EqualTo(0f).Within(1e-4f), "a bearing has no vertical part");
            Assert.That(upper.y, Is.EqualTo(0f).Within(1e-4f), "a bearing has no vertical part");
        }

        /// <summary>
        /// A descent is the climb walked backwards — one path and one set of numbers, so the two
        /// cannot come to disagree about where the landing is.
        /// </summary>
        [Test]
        public void GoingDownIsTheSameLineReversed()
        {
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                Vector3 up = StairWalk.At(Cell, facing: 0, up: true, t, out _);
                Vector3 down = StairWalk.At(Cell, facing: 0, up: false, 1f - t, out _);
                Assert.That(Vector3.Distance(up, down), Is.LessThan(1e-3f),
                    $"the two directions part company at t={t:0.00}");
            }
        }

        /// <summary>
        /// <b>Turning the stair turns the path with it.</b> The mesher yaws the art by the stored
        /// facing; a path that ignored it would be right on one stair in four, which is the shape of
        /// fault the ghost had the day before this.
        /// </summary>
        [Test]
        public void TheWholePathTurnsWithTheStairsFacing()
        {
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                Vector3 north = StairWalk.At(Cell, facing: 0, up: true, t, out _) - CellMetrics.FloorCentre(Cell);
                Vector3 east = StairWalk.At(Cell, facing: 1, up: true, t, out _) - CellMetrics.FloorCentre(Cell);

                Vector3 expected = Quaternion.Euler(0f, Directions.Yaw[1], 0f) * north;
                Assert.That(Vector3.Distance(expected, east), Is.LessThan(1e-3f),
                    $"facing 1 is not facing 0 turned a quarter, at t={t:0.00}");
            }
        }

        /// <summary>
        /// The whole climb is spent at a stair's own price, so the pace this draws is the pace the
        /// simulation paid for. A guard on the arithmetic, not on the art.
        /// </summary>
        [Test]
        public void TheDrawnClimbIsSlowerThanWalkingTheSameGround()
        {
            float seconds = Sim.Pathing.MoveCost.StairUp / (float)TicksPerSecond;
            Assert.That(seconds, Is.GreaterThan(CellMetrics.SizeXZ / 2f / (float)TicksPerSecond),
                "a stair that costs less than a stride is not a stair");
        }
    }
}
