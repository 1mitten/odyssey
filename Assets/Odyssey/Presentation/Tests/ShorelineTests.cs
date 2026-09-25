#nullable enable

using System.Reflection;
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The shoreline (<c>docs/design/38-meadow-overhaul.md</c> §24): the bank and the bed drawn as
    /// one surface that crosses the water line inside the bank rather than at the cell's edge, the
    /// water laid over each bank, and the ground field the shaders paint marsh and depth from.
    ///
    /// <para>The board: grass at layer 0, and at layer 1 either grass (a bank, its top at 6 m) or
    /// water (its bed the grass at layer 0, its surface at 3 m + <see cref="ChunkMesher.WaterSurface"/>
    /// of a layer).</para>
    /// </summary>
    public class ShorelineTests
    {
        const int Side = 12;
        bool _skinWas, _shoreWas;
        float _reliefWas;

        [SetUp]
        public void SetUp()
        {
            _skinWas = GroundSkin.Enabled;
            _shoreWas = WaterShore.Enabled;
            _reliefWas = GroundRelief.Amplitude;
            GroundSkin.Enabled = true;
            WaterShore.Enabled = true;
            GroundRelief.Amplitude = 0f;
            BankLayout.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GroundSkin.Enabled = _skinWas;
            WaterShore.Enabled = _shoreWas;
            GroundRelief.Amplitude = _reliefWas;
            BankLayout.Reset();
        }

        static RenderTestWorld Board(System.Func<int, int, bool> water, ushort kind = NaturalContent.TerrainShallowWater)
        {
            var world = new RenderTestWorld(Side, Side, 4);
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
                if (water(x, z)) world.Surface(x, z, 1, kind);
                else world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            }
            return world.Publish();
        }

        /// <summary>A stream one cell wide at x = 5.</summary>
        static RenderTestWorld Stream() => Board((x, z) => x == 5);

        static BankLayout.Ramp Bank(RenderTestWorld world, int x, int z)
        {
            Assert.That(BankLayout.BankDips(world.Model, x, z, 1, out BankLayout.Ramp dip), Is.True, $"({x},{z}) is no bank");
            Assert.That(dip.Fan, Is.True, "a bank with the shoreline on is drawn as a fan");
            return dip;
        }

        static BankLayout.Ramp Bed(RenderTestWorld world, int x, int z)
        {
            Assert.That(BankLayout.BedRises(world.Model, x, z, 0, out BankLayout.Ramp rise), Is.True, $"the bed at ({x},{z}) does not rise");
            Assert.That(rise.Fan, Is.True);
            return rise;
        }

        /// <summary>The rise of the water's surface above a bank's top, in cell heights: −0.28.</summary>
        static float WaterLineOnABank => ChunkMesher.WaterSurface - 1f;

        [Test]
        public void AStraightBankCrossesTheWaterLineInsideItsOwnCell()
        {
            BankLayout.Ramp dip = Bank(Stream(), 4, 4);
            Assert.That(new[] { dip.R0, dip.R1, dip.R2, dip.R3 }, Is.EqualTo(new[] { 0f, -0.5f, -0.5f, 0f }),
                "the corners on the water's side are half water, so half a layer down");
            Assert.That(new[] { dip.E0, dip.E1, dip.E2, dip.E3, dip.C }, Is.EqualTo(new[] { 0f, -0.5f, 0f, 0f, 0f }));

            // Across the middle of the cell towards the water: dry at the centre, under at the edge,
            // and so the water line is inside the bank, clear of the grid line it used to follow.
            Assert.That(dip.HeightAt(0.5f, 0.5f), Is.GreaterThan(WaterLineOnABank));
            Assert.That(dip.HeightAt(1f, 0.5f), Is.LessThan(WaterLineOnABank));
            Assert.That(dip.HeightAt(0.5f + 0.25f, 0.5f), Is.GreaterThan(WaterLineOnABank),
                "a quarter of a cell from the edge should still be dry");
        }

        /// <summary>
        /// The places a colonist stands and walks keep their heights: every cell's centre, and the
        /// line between two dry centres. That is what keeps the simulation, the goldens and every
        /// standing figure exactly where they were.
        /// </summary>
        [Test]
        public void EveryCentreStaysWhereItWas()
        {
            RenderTestWorld world = Stream();
            BankLayout.Ramp bank = Bank(world, 4, 4);
            BankLayout.Ramp bed = Bed(world, 5, 4);
            Assert.That(bank.HeightAt(0.5f, 0.5f), Is.EqualTo(0f), "a bank's centre is its top");
            Assert.That(bed.HeightAt(0.5f, 0.5f), Is.EqualTo(0f), "a water cell's centre is its bed");
            // Along the bank, between two dry centres: the −z and +z edge midpoints.
            Assert.That(bank.E0, Is.EqualTo(0f));
            Assert.That(bank.E2, Is.EqualTo(0f));
            for (float t = 0f; t <= 1f; t += 0.125f)
                Assert.That(bank.HeightAt(0.5f, t), Is.EqualTo(0f).Within(1e-6f), $"the line between dry centres dipped at v = {t}");
        }

        /// <summary>The bed rises to meet the bank, so the two are one surface and no wall stands on the grid line.</summary>
        [Test]
        public void TheBedMeetsTheBankWhereTheyTouch()
        {
            RenderTestWorld world = Stream();
            BankLayout.Ramp bank = Bank(world, 4, 4);
            BankLayout.Ramp bed = Bed(world, 5, 4);
            float h = CellMetrics.SizeY;
            float bankTop = 2f * h, bedTop = 1f * h;
            // The bank's +x edge is the bed's −x edge: corners 1, 2 against 0, 3, midpoint 1 against 3.
            Assert.That(bankTop + bank.R1 * h, Is.EqualTo(bedTop + bed.R0 * h).Within(1e-5f));
            Assert.That(bankTop + bank.R2 * h, Is.EqualTo(bedTop + bed.R3 * h).Within(1e-5f));
            Assert.That(bankTop + bank.E1 * h, Is.EqualTo(bedTop + bed.E3 * h).Within(1e-5f));
        }

        /// <summary>
        /// A staircase has its corners cut: a corner that touches water only across the diagonal is a
        /// quarter water, where the square rule dropped it all the way.
        /// </summary>
        [Test]
        public void AStaircaseHasItsCornersCut()
        {
            // An L of water: x = 5 for z >= 5 and z = 5 for x >= 5. (4,4) touches it only at its
            // +x +z corner, across the diagonal.
            RenderTestWorld world = Board((x, z) => (x == 5 && z >= 5) || (z == 5 && x >= 5));
            BankLayout.Ramp dip = Bank(world, 4, 4);
            Assert.That(dip.R2, Is.EqualTo(-0.25f), "the diagonal corner is a quarter water");
            Assert.That(new[] { dip.R0, dip.R1, dip.R3, dip.E0, dip.E1, dip.E2, dip.E3, dip.C },
                Is.All.EqualTo(0f), "nothing else of the cell touches water");

            WaterShore.Enabled = false;
            Assert.That(BankLayout.BankDips(world.Model, 4, 4, 1, out BankLayout.Ramp square), Is.True);
            Assert.That(square.R2, Is.EqualTo(-BankLayout.WaterBankDrop / CellMetrics.SizeY).Within(1e-6f),
                "the square rule drops a wet corner all the way, which is the staircase");
        }

        /// <summary>A cell ringed by water keeps its centre above it, so a colonist on an islet stays dry.</summary>
        [Test]
        public void AnIsletKeepsItsCentreAboveTheWater()
        {
            RenderTestWorld world = Board((x, z) => x >= 3 && x <= 7 && z >= 3 && z <= 7 && !(x == 5 && z == 5));
            BankLayout.Ramp dip = Bank(world, 5, 5);
            Assert.That(dip.C, Is.EqualTo(0f));
            Assert.That(new[] { dip.R0, dip.R1, dip.R2, dip.R3 }, Is.All.EqualTo(-0.75f));
            Assert.That(new[] { dip.E0, dip.E1, dip.E2, dip.E3 }, Is.All.EqualTo(-0.5f));
        }

        /// <summary>
        /// The one surface owner: what a figure stands on over a bank (<c>BankLayout.RiseAt</c>) is
        /// the fan the mesher drew, and the mesh holds a vertex at every one of the fan's nine points.
        /// </summary>
        [Test]
        public void WhatIsDrawnIsWhatIsStoodOn()
        {
            RenderTestWorld world = Stream();
            BankLayout.Ramp dip = Bank(world, 4, 4);
            float h = CellMetrics.SizeY, s = CellMetrics.SizeXZ;

            foreach ((float u, float v) in new[] { (0.5f, 0.5f), (0.9f, 0.5f), (0.75f, 0.2f), (0.99f, 0.99f), (0.3f, 0.7f) })
            {
                float stood = BankLayout.RiseAt(world.Model, new CellRef(4, 4, 2), (4 + u) * s, (4 + v) * s);
                Assert.That(stood, Is.EqualTo(dip.HeightAt(u, v) * h).Within(1e-3f), $"RiseAt disagrees with the fan at ({u},{v})");
            }

            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 };
            renderer.Skirt.Enabled = false;
            renderer.Render(1, new SliceSettings());
            ChunkBatch batch = Batches(renderer)[world.Chunks.ChunkIndexOfCell(4, 4, 1)]!;
            Mesh? mesh = batch.Skin.Mesh;
            Assert.That(mesh, Is.Not.Null, "the bank drew no skin");
            Vector3[] vertices = mesh!.vertices;

            float[] fu = { 0f, 1f, 1f, 0f, 0.5f, 1f, 0.5f, 0f, 0.5f };
            float[] fv = { 0f, 0f, 1f, 1f, 0f, 0.5f, 1f, 0.5f, 0.5f };
            float[] rise = { dip.R0, dip.R1, dip.R2, dip.R3, dip.E0, dip.E1, dip.E2, dip.E3, dip.C };
            for (int i = 0; i < 9; i++)
            {
                var expected = new Vector3((4 + fu[i]) * s, 2f * h + rise[i] * h, (4 + fv[i]) * s);
                bool found = false;
                foreach (Vector3 vertex in vertices)
                    if ((vertex - expected).sqrMagnitude < 1e-6f) { found = true; break; }
                Assert.That(found, Is.True, $"no skin vertex at the fan's point {i}: {expected}");
            }
        }

        /// <summary>
        /// The water is laid over each bank, one instance a bank in the bucket the water is already
        /// in — so the draw calls do not grow with the shore (P10) and the instances grow by exactly
        /// the banks.
        /// </summary>
        [Test]
        public void TheWaterOverTheBanksCostsOneInstanceABankAndNoCalls()
        {
            RenderTestWorld world = Stream();
            int callsOn, instancesOn;
            using (var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 })
            {
                renderer.Skirt.Enabled = false;
                renderer.PrimeAll(1, new SliceSettings());
                renderer.Render(1, new SliceSettings());
                callsOn = renderer.DrawCalls;
                instancesOn = renderer.InstancesDrawn;
            }

            WaterShore.Enabled = false;
            world.Model.Remesh();
            using (var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 })
            {
                renderer.Skirt.Enabled = false;
                renderer.PrimeAll(1, new SliceSettings());
                renderer.Render(1, new SliceSettings());
                Assert.That(callsOn, Is.EqualTo(renderer.DrawCalls), "the shoreline added draw calls");
                Assert.That(instancesOn - renderer.InstancesDrawn, Is.EqualTo(2 * Side),
                    "one sheet of water over each of the two banks' cells, and nothing else");
            }
        }

        [Test]
        public void OffGivesTheSquareShoreBack()
        {
            RenderTestWorld world = Stream();
            WaterShore.Enabled = false;
            Assert.That(BankLayout.BankDips(world.Model, 4, 4, 1, out BankLayout.Ramp dip), Is.True);
            Assert.That(dip.Fan, Is.False);
            Assert.That(BankLayout.BedRises(world.Model, 5, 4, 0, out _), Is.False, "the bed rose with the shoreline off");
        }

        /// <summary>The field the shaders paint marsh and depth from: one texel a column, the column's surface.</summary>
        [Test]
        public void TheGroundFieldCarriesMarshWaterAndDepth()
        {
            var world = new RenderTestWorld(Side, Side, 4);
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
                if (x == 5) world.Surface(x, z, 1, z < 6 ? NaturalContent.TerrainShallowWater : NaturalContent.TerrainDeepWater);
                else if (x == 4) world.Solid(x, z, 1, NaturalContent.TerrainMarsh);
                else world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            }
            world.Publish();

            using var field = new GroundField(world.Model);
            Color32 marsh = field.At(4, 2), shallow = field.At(5, 2), deep = field.At(5, 8), grass = field.At(2, 2);
            Assert.That((marsh.r, marsh.b), Is.EqualTo(((byte)255, (byte)0)), "marsh is R");
            Assert.That((shallow.b, shallow.a), Is.EqualTo(((byte)255, (byte)0)), "shallow water is B");
            Assert.That((deep.b, deep.a), Is.EqualTo(((byte)255, (byte)255)), "deep water is B and A");
            Assert.That((grass.r, grass.g, grass.b, grass.a), Is.EqualTo(((byte)0, (byte)0, (byte)0, (byte)0)), "grass is nothing");
            Assert.That(field.Texture.width, Is.EqualTo(Side), "one texel a column");
        }

        /// <summary>
        /// Water runs to where it can fall (design 38 §24f): a stretch that steps down a layer runs
        /// towards the step, and the stretch below it, with nowhere lower to go, lies still.
        /// </summary>
        [Test]
        public void WaterRunsTowardsTheStepAndAPondLiesStill()
        {
            var world = new RenderTestWorld(Side, Side, 4);
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
                bool upper = x == 5 && z >= 2 && z <= 5, lower = x == 5 && z >= 6 && z <= 9;
                if (lower) world.Surface(x, z, 1, NaturalContent.TerrainShallowWater);
                else world.Solid(x, z, 1, NaturalContent.TerrainGrass);
                if (upper) world.Surface(x, z, 2, NaturalContent.TerrainShallowWater);
                else if (!lower) world.Solid(x, z, 2, NaturalContent.TerrainGrass);
            }
            world.Publish();

            using var field = new GroundField(world.Model);
            for (int z = 2; z <= 5; z++)
            {
                Color32 flow = field.FlowAt(5, z);
                Assert.That(flow.b, Is.GreaterThan(0), $"the upper stretch at z = {z} should run");
                Assert.That(flow.g, Is.GreaterThan(200), $"and run towards the step, +z (z = {z}, g = {flow.g})");
                Assert.That(flow.r, Is.InRange(120, 136), "and not sideways");
            }
            for (int z = 6; z <= 9; z++)
                Assert.That(field.FlowAt(5, z).b, Is.Zero, $"the stretch below has no outlet and lies still (z = {z})");
            Assert.That(field.FlowAt(2, 2).b, Is.Zero, "dry ground has no flow");
        }

        /// <summary>The water's clock is the game's: a paused world holds still, unless told to move on pause.</summary>
        [Test]
        public void TheWaterHoldsStillThroughAPause()
        {
            bool was = WaterDirector.MovesOnPause;
            var water = new WaterDirector { TicksPerSecond = 60f };
            try
            {
                WaterDirector.MovesOnPause = false;
                water.Apply(600, 0.016f);
                Assert.That(water.Seconds, Is.EqualTo(10f).Within(1e-4f));
                water.Apply(600, 0.5f);
                Assert.That(water.Seconds, Is.EqualTo(10f).Within(1e-4f), "a paused world moved the water");
                water.Apply(660, 0.016f);
                Assert.That(water.Seconds, Is.EqualTo(11f).Within(1e-4f), "a second of ticks is a second of water");

                WaterDirector.MovesOnPause = true;
                water.Apply(660, 0.5f);
                Assert.That(water.Seconds, Is.EqualTo(11.5f).Within(1e-4f), "on pause, with the switch, it runs in real time");

                water.Apply(0, 0.016f);
                Assert.That(water.Seconds, Is.EqualTo(0f).Within(1e-4f), "a new session starts the clock again");
            }
            finally
            {
                WaterDirector.MovesOnPause = was;
                water.Dispose();
            }
        }

        static ChunkBatch?[] Batches(ChunkRenderer renderer) =>
            (ChunkBatch?[])typeof(ChunkRenderer)
                .GetField("_batches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
    }
}
