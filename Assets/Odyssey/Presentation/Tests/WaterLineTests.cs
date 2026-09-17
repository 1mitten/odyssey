#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// A colonist in water is drawn at the surface, not on the bed.
    ///
    /// <para>The owner reported it twice — "when walking under/through water", and then, after the
    /// first round had only measured the depth, "I saw someone walk under water again when it was
    /// 1 deep … it's meant to float when this shallow if possible" (2026-09-17). Both water rows
    /// are non-solid, so the cell a colonist occupies in a stream is the water cell and the floor
    /// beneath it is the bed; at <c>ChunkMesher.WaterSurface</c> 0.72 of a 3 m cell that is
    /// <b>2.16 m of water over a 1.8 m person</b>.</para>
    ///
    /// <para><b>Every claim here is about the drawing.</b> Nothing in this file has an opinion
    /// about speed, carrying, jobs or paths, because the owner's decision was that the float is how
    /// it looks and shallow water stays crossable.</para>
    /// </summary>
    public class WaterLineTests
    {
        [SetUp]
        public void Reset()
        {
            WaterLine.Reset();
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        [TearDown]
        public void Restore()
        {
            WaterLine.Reset();
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        static PawnView Standing(CellRef cell) => Walking(cell, cell, 0);

        static PawnView Walking(CellRef from, CellRef to, int percent) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, percent);

        /// <summary>
        /// A flat board with a strip of shallow water down the middle of it: dry at x = 2, wet at
        /// x = 3 and x = 4, dry again at x = 5.
        /// </summary>
        static RenderTestWorld Stream()
        {
            var world = new RenderTestWorld(8, 8, 6);

            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, 1, NaturalContent.TerrainSubsoil);

                // Layer 2 is where a colonist stands. In the channel it is water over the bed;
                // elsewhere it is open air over solid ground, which is ordinary standable land.
                if (x == 3 || x == 4) world.Surface(x, z, 2, NaturalContent.TerrainShallowWater);
            }

            return world.Publish();
        }

        static CellRef Wet(int x = 3, int z = 4) => new CellRef(x, z, 2);
        static CellRef Dry(int x = 2, int z = 4) => new CellRef(x, z, 2);

        [Test]
        public void TheFixtureReallyHasWaterInIt()
        {
            // The control. Every test below is vacuous if the strip is not water, and a vacuous
            // test that passes is worse than no test at all — the lesson from the continuity suite
            // that ran for weeks on a flat field.
            RenderTestWorld world = Stream();
            Assert.That(WaterLine.IsWater(world.Model, Wet()), Is.True, "the channel is not water");
            Assert.That(WaterLine.IsWater(world.Model, Dry()), Is.False, "the bank is water");
        }

        [Test]
        public void AColonistInWaterIsDrawnNearTheSurfaceRatherThanOnTheBed()
        {
            RenderTestWorld world = Stream();

            float bed = CellMetrics.FloorCentre(Wet()).y;
            float drawn = PawnPose.Of(Standing(Wet()), 0f, 0, out _, world.Model).y;
            float surface = bed + CellMetrics.SizeY * ChunkMesher.WaterSurface;

            Assert.That(drawn, Is.GreaterThan(bed + 1f),
                "a colonist in the water is still drawn on the bed, which is the reported fault");
            Assert.That(drawn, Is.EqualTo(surface - WaterLine.Draught).Within(1e-3f),
                "a floating colonist is not at the waterline less its draught");
            Assert.That(drawn, Is.LessThan(surface),
                "a colonist is drawn on top of the water rather than in it");
        }

        [Test]
        public void AColonistOnDryGroundIsUntouched()
        {
            RenderTestWorld world = Stream();

            Assert.That(PawnPose.Of(Standing(Dry()), 0f, 0, out _, world.Model).y,
                Is.EqualTo(CellMetrics.FloorCentre(Dry()).y).Within(1e-4f),
                "the float has lifted somebody standing on dry land");
        }

        /// <summary>
        /// Walk one step and report every drawn height along it, plus the worst change between two
        /// consecutive samples.
        /// </summary>
        static (float worst, float[] heights) Cross(RenderTestWorld world, CellRef from, CellRef to)
        {
            var heights = new float[101];
            float worst = 0f;

            for (int percent = 0; percent <= 100; percent++)
            {
                heights[percent] = PawnPose.Of(Walking(from, to, percent), 0f, 0, out _, world.Model).y;
                if (percent > 0)
                    worst = Mathf.Max(worst, Mathf.Abs(heights[percent] - heights[percent - 1]));
            }

            return (worst, heights);
        }

        // The crossing tests all run on the cut channel rather than the flat strip, and the reason
        // is a fault the flat one hid: with the bed at the same layer as the land beside it, the
        // waterline stands ABOVE the bank, so "climbing out" is a descent and every claim about
        // not overshooting the bank is inverted. It failed exactly that way the first time it ran.
        // A stream the generator actually cuts has its water a storey below the ground, which is
        // the case these are about. See CutChannel.
        static CellRef ChannelWet() => new CellRef(3, 4, 2);
        static CellRef ChannelBank() => new CellRef(2, 4, 3);

        /// <summary>
        /// **The one that matters in motion.** The float is over a metre; switched on at the cell
        /// boundary it is a colonist teleporting — the same class of fault as the 81.9 mm midpoint
        /// snap <c>WalkOnReliefTests</c> was written for, only ten times larger.
        ///
        /// <para><b>The budget is not one frame of walking, and that is deliberate.</b> The whole
        /// height change happens in the water half of the step (<see cref="WaterLine.VerticalProgress"/>),
        /// so it is compressed into half the time on purpose — a colonist climbing out pulls itself
        /// up, and a pull-up is fast. The walking-frame yardstick measures *unintended*
        /// discontinuity, and applying it here would fail a motion the owner asked for. What is
        /// asserted instead is that the change is spread over many samples rather than taken in
        /// one, which is what tells a ramp from a switch.</para>
        /// </summary>
        [Test]
        public void GettingInIsARampAndNotASwitch()
        {
            RenderTestWorld world = CutChannel();
            (float worst, float[] heights) = Cross(world, ChannelBank(), ChannelWet());

            float total = Mathf.Abs(heights[100] - heights[0]);
            TestContext.WriteLine(
                $"MEASURED getting in: {total:F2} m of drop, worst {worst * 1000f:F1} mm a sample.");

            Assert.That(total, Is.GreaterThan(1f), "the fixture has no drop to measure");
            Assert.That(worst, Is.LessThan(total * 0.15f),
                "more than a seventh of the whole drop happens between two consecutive samples, " +
                "so the float is switching rather than ramping");
        }

        [Test]
        public void GettingOutIsARampAndNotASwitch()
        {
            RenderTestWorld world = CutChannel();
            (float worst, float[] heights) = Cross(world, ChannelWet(), ChannelBank());

            float total = Mathf.Abs(heights[100] - heights[0]);
            Assert.That(worst, Is.LessThan(total * 0.15f), "the drawn height switches leaving the water");
        }

        /// <summary>
        /// **The owner's own description of what climbing out should look like** (2026-09-17: "the
        /// lift out of the water happens earlier or reaches the edge and pulls up").
        ///
        /// <para>By the time the figure is over the cell it is climbing into, it must already be at
        /// that cell's height — anything lower is inside the block, which is what was reported as
        /// "clipped and sunk half way into a terrain tile". And it must never go above it, or the
        /// figure pops up and settles back down.</para>
        /// </summary>
        [Test]
        public void ClimbingOutIsFinishedByTheEdgeAndNeverOvershoots()
        {
            RenderTestWorld world = CutChannel();
            (float _, float[] heights) = Cross(world, ChannelWet(), ChannelBank());

            float bank = heights[100];

            Assert.That(heights[50], Is.EqualTo(bank).Within(1e-3f),
                "the figure has not finished climbing by the time it crosses the edge, so it is " +
                "inside the ground it is climbing into");

            for (int percent = 50; percent <= 100; percent++)
                Assert.That(heights[percent], Is.EqualTo(bank).Within(1e-3f),
                    $"at {percent}% the figure is not at the height of the cell it is walking on");

            for (int percent = 0; percent <= 100; percent++)
                Assert.That(heights[percent], Is.LessThanOrEqualTo(bank + 1e-3f),
                    $"at {percent}% the figure is above the bank it is climbing onto");
        }

        /// <summary>The mirror image: getting in waits until the edge before dropping.</summary>
        [Test]
        public void GettingInWaitsUntilTheEdgeBeforeDropping()
        {
            RenderTestWorld world = CutChannel();
            (float _, float[] heights) = Cross(world, ChannelBank(), ChannelWet());

            float bank = heights[0];

            for (int percent = 0; percent <= 50; percent++)
                Assert.That(heights[percent], Is.EqualTo(bank).Within(1e-3f),
                    $"at {percent}% the figure has already started sinking, although it is still " +
                    "walking on the bank");
        }

        /// <summary>
        /// The pose and the height move together. A figure still lying prone once its height has
        /// reached the bank is a colonist sliding onto the grass on its front; one that has stood
        /// up while still at the waterline is a colonist standing on water.
        /// </summary>
        [Test]
        public void ThePoseArrivesAndLeavesWithTheHeight()
        {
            RenderTestWorld world = CutChannel();

            Assert.That(WaterLine.Weight(world.Model, ChannelWet(), ChannelBank(), 0.5f), Is.EqualTo(0f).Within(1e-3f),
                "the figure is still posed as a swimmer at the moment it steps onto the bank");
            Assert.That(WaterLine.Weight(world.Model, ChannelBank(), ChannelWet(), 0.5f), Is.EqualTo(0f).Within(1e-3f),
                "the figure is posed as a swimmer while still walking on the bank");
            Assert.That(WaterLine.Weight(world.Model, ChannelBank(), ChannelWet(), 1f), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(WaterLine.Weight(world.Model, ChannelWet(), ChannelBank(), 0f), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void TheLeverPutsEverybodyBackOnTheBed()
        {
            RenderTestWorld world = Stream();
            WaterLine.FloatFigures = false;

            Assert.That(PawnPose.Of(Standing(Wet()), 0f, 0, out _, world.Model).y,
                Is.EqualTo(CellMetrics.FloorCentre(Wet()).y).Within(1e-4f));
            Assert.That(WaterLine.Weight(world.Model, Wet(), Wet(), 0.5f), Is.Zero,
                "the pose is still on with the float off, so a figure would swim on the bed");
        }

        /// <summary>
        /// A stream as the generator actually cuts one: the bed a layer below the land beside it.
        ///
        /// <para>The flat fixture above is honest arithmetic and unrealistic geometry — it puts the
        /// bed at the same height as the bank, so a floating colonist sits 1.9 m <em>above</em> the
        /// ground it just left. Real water is cut into the land, which is what makes the waterline
        /// land near bank height and the float read as stepping down into a stream rather than
        /// climbing a mound of water. That relationship is the thing a sign error in the float
        /// would break, and nothing above would notice.</para>
        /// </summary>
        static RenderTestWorld CutChannel()
        {
            var world = new RenderTestWorld(8, 8, 6);

            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainSubsoil);

                bool channel = x == 3 || x == 4;

                // The bed is layer 1 in the channel; the land beside it is solid up to layer 2, so
                // somebody on the bank stands at layer 3 and the water lies a storey below them.
                world.Solid(x, z, 1, NaturalContent.TerrainSubsoil);
                if (!channel) world.Solid(x, z, 2, NaturalContent.TerrainGrass);
                else world.Surface(x, z, 2, NaturalContent.TerrainShallowWater);
            }

            return world.Publish();
        }

        [Test]
        public void TheWaterlineLandsNearBankHeightRatherThanAboveIt()
        {
            RenderTestWorld world = CutChannel();

            var wet = new CellRef(3, 4, 2);
            var bank = new CellRef(2, 4, 3);

            float floating = PawnPose.Of(Standing(wet), 0f, 0, out _, world.Model).y;
            float onTheBank = PawnPose.Of(Standing(bank), 0f, 0, out _, world.Model).y;

            TestContext.WriteLine(
                $"MEASURED cut channel: a floating colonist sits {(onTheBank - floating) * 100f:F0} cm " +
                $"below one on the bank beside it.");

            Assert.That(floating, Is.LessThan(onTheBank),
                "a colonist in a cut stream is drawn ABOVE one standing on the bank, so the float " +
                "reads as climbing onto a mound of water");
            Assert.That(onTheBank - floating, Is.LessThan(CellMetrics.SizeY),
                "a floating colonist is more than a whole storey below the bank, which is being on " +
                "the bed with extra steps");
        }

        [Test]
        public void ASurfaceShallowerThanTheDraughtNeverPushesAnybodyUnderTheBed()
        {
            // Not reachable today at 0.72, and one edit to WaterSurface away from being reachable.
            // A negative rise would draw a colonist below the ground it is standing on, which is a
            // worse fault than the one being fixed and would be blamed on the terrain.
            RenderTestWorld world = Stream();
            WaterLine.Draught = 99f;

            Assert.That(WaterLine.FloatRise(world.Model, Wet()), Is.Zero);
        }
    }
}
