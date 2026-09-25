#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Presentation.World;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// How a figure is drawn jumping a one-cell stream, or falling short into it (design 44 §7).
    ///
    /// <para>Most of this is on a board with no world under it — flat ground, no relief, no banks
    /// — so the numbers are the arc's own and not the terrain's. The <c>OnAShoreline</c> tests are
    /// the exception, because a bank sloping into the water is where the owner's first play found
    /// the jump taking off in the stream, and no world-free test could see that.</para>
    /// </summary>
    public class JumpArcTests
    {
        const int TicksPerSecond = 60;
        const float Tolerance = 1e-4f;

        static readonly CellRef Near = new CellRef(4, 3, 2);
        static readonly CellRef Far = new CellRef(6, 3, 2);
        static readonly CellRef Water = new CellRef(5, 3, 1);

        [SetUp]
        public void Reset()
        {
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        [TearDown]
        public void Restore()
        {
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        static PawnView Jumping(CellRef to, int perMille, bool jumpingShort = false) =>
            new PawnView(new PawnId(1), Near, 100, 100, 50, -1, to, perMille / 10,
                movePerMille: perMille, jumpingShort: jumpingShort);

        static Vector3 At(CellRef to, float t, bool jumpingShort = false) =>
            JumpArc.Position(null, Jumping(to, Mathf.Max(1, Mathf.RoundToInt(t * 1_000)), jumpingShort), t);

        // ------------------------------------------------------------------ the shares

        [Test]
        public void TheSharesComeFromThePriceAndTheClips()
        {
            // A quarter of the step to the lip: half a cell of walking out of a jump priced as two.
            Assert.That(JumpArc.Approach, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(JumpArc.StepSeconds, Is.EqualTo(MoveCost.Jump / (float)TicksPerSecond).Within(Tolerance));
            Assert.That(JumpArc.Gather + JumpArc.Air + JumpArc.Settle, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(JumpArc.Air, Is.GreaterThan(0.2f),
                "the clips fill the flight and leave no air: shorten them or dear the jump");
        }

        [Test]
        public void TheApexIsAtTheSpeedOfGravity()
        {
            Assert.That(JumpArc.Apex, Is.EqualTo(9.81f * JumpArc.AirSeconds * JumpArc.AirSeconds / 8f).Within(Tolerance));
            // Half way through the air the arc stands exactly its apex over the line between the lips.
            Assert.That(JumpArc.Height(1f, 1f, 0.5f), Is.EqualTo(1f + JumpArc.Apex).Within(Tolerance));
            Assert.That(JumpArc.Height(1f, 3f, 0f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(JumpArc.Height(1f, 3f, 1f), Is.EqualTo(3f).Within(Tolerance));
        }

        // ------------------------------------------------------------------ the path

        [Test]
        public void AJumpTakesOffAndLandsExactlyOnTheBanks()
        {
            Vector3 start = CellMetrics.FloorCentre(Near), end = CellMetrics.FloorCentre(Far);
            Assert.That(Vector3.Distance(At(Far, 0f), start), Is.LessThan(Tolerance), "a jump began off the bank");
            Assert.That(Vector3.Distance(At(Far, 1f), end), Is.LessThan(Tolerance), "a jump ended off the bank");

            // Walking to and from the lip is on the ground, and the flight is never below it.
            for (int i = 0; i <= 1_000; i++)
            {
                float t = i / 1_000f;
                Vector3 p = At(Far, t);
                Assert.That(p.y, Is.GreaterThanOrEqualTo(start.y - Tolerance), $"t {t}: under the bank");
                if (t < JumpArc.Approach || t > 1f - JumpArc.Approach)
                    Assert.That(p.y, Is.EqualTo(start.y).Within(Tolerance), $"t {t}: off the ground while walking");
            }
        }

        [Test]
        public void TheGroundIsWalkedAtWalkingPaceAndTheAirIsNeverSlower()
        {
            float walking = CellMetrics.SizeXZ / (MoveCost.Orthogonal / (float)TicksPerSecond);

            float approach = Horizontal(At(Far, 0f), At(Far, JumpArc.Approach)) / (JumpArc.Approach * JumpArc.StepSeconds);
            Assert.That(approach, Is.EqualTo(walking).Within(1e-3f), "the walk to the lip is not a walk");

            float air = CellMetrics.SizeXZ / JumpArc.AirSeconds;
            Assert.That(air, Is.GreaterThanOrEqualTo(walking), "the figure crosses the gap slower than it walks");
        }

        [Test]
        public void NoFrameMovesTheFigureFurtherThanTheFlightCanExplain()
        {
            // Sixty frames a second through the standard jump. The fastest honest motion is the
            // air: its horizontal speed, and at the ends of the parabola its vertical one.
            int frames = Mathf.CeilToInt(JumpArc.StepSeconds * TicksPerSecond);
            float horizontal = CellMetrics.SizeXZ / JumpArc.AirSeconds / TicksPerSecond;
            float vertical = 4f * JumpArc.Apex / JumpArc.AirSeconds / TicksPerSecond;
            float budget = Mathf.Sqrt(horizontal * horizontal + vertical * vertical) * 1.05f;

            Vector3 was = At(Far, 0f);
            for (int f = 1; f <= frames; f++)
            {
                Vector3 now = At(Far, f / (float)frames);
                Assert.That(Vector3.Distance(was, now), Is.LessThanOrEqualTo(budget), $"frame {f}: a snap");
                was = now;
            }
        }

        [Test]
        public void AShortJumpHandsOverToTheWaterLineAtTheEnd()
        {
            // With no world the water line is the bed, but the hand-over is the property: the arc
            // ends exactly where a figure resting in that cell is drawn.
            Vector3 resting = PawnPose.Of(new PawnView(new PawnId(1), Water, 100, 100, 50), 0f, 0, out _);
            Assert.That(Vector3.Distance(At(Water, 1f, jumpingShort: true), resting), Is.LessThan(Tolerance),
                "the short jump ends somewhere the float does not start");
            Assert.That(Vector3.Distance(At(Water, 0.8f, jumpingShort: true), resting), Is.LessThan(Tolerance),
                "a short jump is still moving after its flight");
            Assert.That(Vector3.Distance(At(Water, 0f, jumpingShort: true), CellMetrics.FloorCentre(Near)),
                Is.LessThan(Tolerance), "a short jump began off the bank");
        }

        [Test]
        public void AShortJumpLiesDownOnlyAtTheWater()
        {
            Assert.That(JumpArc.ShortSwimWeight(JumpArc.Approach), Is.EqualTo(0f));
            float midAir = JumpArc.Approach + JumpArc.Flight * (JumpArc.Gather + JumpArc.Air * 0.5f);
            Assert.That(JumpArc.ShortSwimWeight(midAir), Is.EqualTo(0f), "a figure lay down in mid-air");
            Assert.That(JumpArc.ShortSwimWeight(1f), Is.EqualTo(1f), "a figure reached the water standing up");
        }

        // ------------------------------------------------------------------ the clips

        [Test]
        public void TheClipsPlayInOrderAndOnlyInTheFlight()
        {
            Assert.That(JumpArc.Clip(0.1f, false, out _), Is.EqualTo(JumpArc.ClipPhase.None), "a clip while walking to the lip");
            Assert.That(JumpArc.Clip(0.9f, false, out _), Is.EqualTo(JumpArc.ClipPhase.None), "a clip while walking away");

            float seconds = -1f;
            bool landed = false;
            for (int i = 0; i <= 1_000; i++)
            {
                JumpArc.ClipPhase phase = JumpArc.Clip(i / 1_000f, false, out float s);
                if (phase == JumpArc.ClipPhase.Land && !landed) { landed = true; seconds = -1f; }
                if (phase == JumpArc.ClipPhase.None) continue;
                Assert.That(phase == JumpArc.ClipPhase.TakeOff && landed, Is.False, "took off again after landing");
                Assert.That(s, Is.GreaterThanOrEqualTo(seconds - Tolerance), $"t {i / 1_000f}: a clip ran backwards");
                seconds = s;
            }
            Assert.That(landed, Is.True, "the landing clip never played");

            // A short jump never lands on its feet: it holds the take-off and the swim takes it.
            Assert.That(JumpArc.Clip(0.74f, true, out float held), Is.EqualTo(JumpArc.ClipPhase.TakeOff));
            Assert.That(held, Is.EqualTo(JumpArc.TakeOffSeconds).Within(Tolerance));
        }

        // ------------------------------------------------------------------ the pose

        [Test]
        public void PawnPoseDrawsAJumpFromTheArc()
        {
            // The early return in PawnPose.Of: a jump is not walked along the ground, not waded
            // and not sidestepped.
            for (int perMille = 50; perMille < 1_000; perMille += 50)
            {
                PawnView pawn = Jumping(Far, perMille);
                Vector3 drawn = PawnPose.Of(pawn, 0f, 0, out Vector3 heading);
                Vector3 arc = JumpArc.Position(null, pawn, perMille / 1_000f);
                Assert.That(Vector3.Distance(drawn, arc), Is.LessThan(Tolerance), $"{perMille}: PawnPose drew something else");
                Assert.That(heading.y, Is.EqualTo(0f));
                Assert.That(heading.x, Is.GreaterThan(0f), "a jump faces where it is going");
            }
        }

        // ------------------------------------------------------------------ on a real shoreline

        /// <summary>
        /// A stream one cell wide at x = 5, drawn with the shoreline on (design 38 §24): grass at
        /// layer 0, and at layer 1 grass or water — the same cells as <see cref="Near"/>,
        /// <see cref="Water"/> and <see cref="Far"/>. The water's surface is 5.16 m, 0.84 m under
        /// the banks' 6 m tops.
        /// </summary>
        static WorldRenderModel Stream()
        {
            var world = new RenderTestWorld(12, 12, 4);
            for (int z = 0; z < 12; z++)
            for (int x = 0; x < 12; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
                if (x == 5) world.Surface(x, z, 1, NaturalContent.TerrainShallowWater);
                else world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            }
            return world.Publish().Model;
        }

        static float WaterSurface(WorldRenderModel world) =>
            CellMetrics.FloorCentre(Water).y + WaterLine.SurfaceAbove(world, Water);

        /// <summary>Run a test body on the drawn shoreline, putting the switches back after.</summary>
        static void OnTheShoreline(System.Action<WorldRenderModel> body)
        {
            bool skin = GroundSkin.Enabled, shore = WaterShore.Enabled;
            GroundSkin.Enabled = true;
            WaterShore.Enabled = true;
            try { body(Stream()); }
            finally
            {
                GroundSkin.Enabled = skin;
                WaterShore.Enabled = shore;
            }
        }

        /// <summary>
        /// The owner's report (2026-09-25): <i>"the jump should happen on land (and not in water
        /// which I see it does) — it has to happen from the ledge"</i>. The shoreline slopes a bank
        /// into the stream, so the cell's edge the arc used to take off from is under the water.
        /// The control is that edge: it must be wet, or this board is not the one the owner saw.
        /// </summary>
        [Test]
        public void OnAShorelineTheJumpTakesOffAndLandsOnDryGround() => OnTheShoreline(world =>
        {
            float surface = WaterSurface(world);
            Vector3 edge = CellMetrics.FloorCentre(Near) + new Vector3(CellMetrics.HalfXZ, 0f, 0f);
            Assert.That(JumpArc.GroundAt(world, Near, edge.x, edge.z), Is.LessThan(surface),
                "control: the cell's edge is not under the water, so this board shows nothing");

            JumpArc.Lips(world, Jumping(Far, 500), out float lip, out float farLip);
            Assert.That(lip, Is.GreaterThan(0.05f).And.LessThan(JumpArc.Approach), "the take-off did not move in from the edge");
            Assert.That(farLip, Is.EqualTo(1f - lip).Within(0.011f), "a straight stream is not jumped symmetrically");

            float gatherEnds = JumpArc.Approach + JumpArc.Flight * JumpArc.Gather;
            float settleBegins = JumpArc.Approach + JumpArc.Flight * (JumpArc.Gather + JumpArc.Air);
            for (int i = 0; i <= 1_000; i++)
            {
                float t = i / 1_000f;
                if (t > gatherEnds && t < settleBegins) continue; // in the air
                Vector3 p = JumpArc.Position(world, Jumping(Far, Mathf.Max(1, i)), t);
                Assert.That(p.y, Is.GreaterThanOrEqualTo(surface + JumpArc.DryClearance - Tolerance),
                    $"t {t}: on the ground at the water's edge or in it");
            }
        });

        [Test]
        public void OnAShorelineAShortJumpStillTakesOffFromDryGround() => OnTheShoreline(world =>
        {
            float surface = WaterSurface(world);
            float gatherEnds = JumpArc.Approach + JumpArc.Flight * JumpArc.Gather;
            for (int i = 0; i <= 1_000; i++)
            {
                float t = i / 1_000f;
                if (t > gatherEnds) break;
                Vector3 p = JumpArc.Position(world, Jumping(Water, Mathf.Max(1, i), jumpingShort: true), t);
                Assert.That(p.y, Is.GreaterThanOrEqualTo(surface + JumpArc.DryClearance - Tolerance),
                    $"t {t}: a short jump walked into the water before it took off");
            }
        });

        /// <summary>The lip moving in must not open a snap where the walk hands over to the gather.</summary>
        [Test]
        public void OnAShorelineNoFrameSnaps() => OnTheShoreline(world =>
        {
            JumpArc.Lips(world, Jumping(Far, 500), out float lip, out float farLip);
            float span = (farLip - lip) * 2f * CellMetrics.SizeXZ;
            int frames = Mathf.CeilToInt(JumpArc.StepSeconds * TicksPerSecond);
            float horizontal = span / JumpArc.AirSeconds / TicksPerSecond;
            float vertical = (4f * JumpArc.Apex + 1f) / JumpArc.AirSeconds / TicksPerSecond;
            float budget = Mathf.Sqrt(horizontal * horizontal + vertical * vertical) * 1.05f;

            Vector3 was = JumpArc.Position(world, Jumping(Far, 1), 0f);
            for (int f = 1; f <= frames; f++)
            {
                float t = f / (float)frames;
                Vector3 now = JumpArc.Position(world, Jumping(Far, Mathf.Max(1, Mathf.RoundToInt(t * 1_000))), t);
                Assert.That(Vector3.Distance(was, now), Is.LessThanOrEqualTo(budget), $"frame {f}: a snap");
                was = now;
            }
        });

        static float Horizontal(Vector3 a, Vector3 b) => new Vector2(b.x - a.x, b.z - a.z).magnitude;
    }
}
