#nullable enable

using System.Collections.Generic;
using System.IO;
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
    /// The ground skin (<c>docs/design/38-meadow-overhaul.md</c> §6, §20): terraces drawn as one
    /// continuous surface over the unchanged simulation layers.
    ///
    /// <para>The board here is a floor of grass at layer 0 (its top at 3 m, so layer 1 is the air a
    /// colonist walks in) with steps of grass one layer up placed per test. A foot cell is an air
    /// cell at layer 1 beside such a step.</para>
    /// </summary>
    public class GroundSkinTests
    {
        const int Side = 12;
        bool _skinWas;
        float _reliefWas;

        [SetUp]
        public void SetUp()
        {
            _skinWas = GroundSkin.Enabled;
            _reliefWas = GroundRelief.Amplitude;
            GroundSkin.Enabled = true;
            GroundRelief.Amplitude = 0f;
            BankLayout.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GroundSkin.Enabled = _skinWas;
            GroundRelief.Amplitude = _reliefWas;
            BankLayout.Reset();
        }

        static RenderTestWorld Meadow(System.Func<int, int, bool> step)
        {
            var world = new RenderTestWorld(Side, Side, 4);
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
                if (step(x, z)) world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            }
            return world.Publish();
        }

        static BankLayout.Ramp RampAt(RenderTestWorld world, int x, int z)
        {
            Assert.That(BankLayout.RampCorners(world.Model, x, z, 1, out BankLayout.Ramp ramp), Is.True,
                $"no ramp at ({x},{z})");
            return ramp;
        }

        [Test]
        public void AStraightStepLiftsTheTwoCornersOnItsSide()
        {
            RenderTestWorld world = Meadow((x, z) => x >= 6);
            BankLayout.Ramp ramp = RampAt(world, 5, 4);
            Assert.That(new[] { ramp.R0, ramp.R1, ramp.R2, ramp.R3 }, Is.EqualTo(new[] { 0f, 1f, 1f, 0f }));
            Assert.That(ramp.HeightAt(0.5f, 0.5f), Is.EqualTo(0.5f).Within(1e-5f), "a straight ramp is half-way up at its middle");
            Assert.That(BankLayout.RampCorners(world.Model, 4, 4, 1, out _), Is.False, "a cell two away from the step has no ramp");
        }

        [Test]
        public void NeighbouringRampsShareTheirEdge()
        {
            RenderTestWorld world = Meadow((x, z) => x >= 6 || z >= 7);
            for (int z = 0; z < 6; z++)
            {
                if (!BankLayout.RampCorners(world.Model, 5, z, 1, out BankLayout.Ramp a)) continue;
                if (!BankLayout.RampCorners(world.Model, 5, z + 1, 1, out BankLayout.Ramp b)) continue;
                // Cell z's +z edge (corners 3, 2) is cell z+1's −z edge (corners 0, 1).
                Assert.That(a.R3, Is.EqualTo(b.R0), $"corner shared by ({5},{z}) and ({5},{z + 1})");
                Assert.That(a.R2, Is.EqualTo(b.R1), $"corner shared by ({5},{z}) and ({5},{z + 1})");
            }
        }

        [Test]
        public void AnInnerCornerIsTheOldCornerPiece()
        {
            // Steps at +x and +z of (5,5): the notch of an L.
            RenderTestWorld world = Meadow((x, z) => x >= 6 || z >= 6);
            BankLayout.Ramp ramp = RampAt(world, 5, 5);
            Assert.That(new[] { ramp.R0, ramp.R1, ramp.R2, ramp.R3 }, Is.EqualTo(new[] { 0f, 1f, 1f, 1f }));
            // max(u, v): the old inner-corner bank.
            Assert.That(ramp.HeightAt(0.8f, 0.2f), Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(ramp.HeightAt(0.2f, 0.8f), Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void AnOuterCornerIsTheOldHipPiece()
        {
            // A block standing on the diagonal of (5,5) only.
            RenderTestWorld world = Meadow((x, z) => x >= 6 && z >= 6);
            BankLayout.Ramp ramp = RampAt(world, 5, 5);
            Assert.That(new[] { ramp.R0, ramp.R1, ramp.R2, ramp.R3 }, Is.EqualTo(new[] { 0f, 0f, 1f, 0f }));
            // min(u, v): the old outer-corner bank.
            Assert.That(ramp.HeightAt(0.9f, 0.1f), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(ramp.HeightAt(0.5f, 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        /// <summary>
        /// A trench between two terraces would be capped flush by the corner rule — every corner
        /// touches a step — hiding a hole the simulation still has. It stays flat, and the sim's
        /// own copy of the rule (TerraceFoot, and so the goldens) does not move.
        /// </summary>
        [Test]
        public void ATrenchIsNotCappedAndTheSimulationsRuleDoesNotMove()
        {
            RenderTestWorld world = Meadow((x, z) => x <= 4 || x >= 6);
            Assert.That(BankLayout.RampCorners(world.Model, 5, 5, 1, out _), Is.False, "the trench was capped");
            Assert.That(BankLayout.At(world.Model, 5, 5, 1).Exists, Is.True,
                "the bank rule (and TerraceFoot, which mirrors it) must still call this a foot cell");
        }

        /// <summary>
        /// The one surface owner: what a figure stands on (<c>BankLayout.RiseAt</c>) is what the
        /// mesher drew, read back through the same corners and triangulation.
        /// </summary>
        [Test]
        public void WhatIsDrawnIsWhatIsStoodOn()
        {
            RenderTestWorld world = Meadow((x, z) => x >= 6 || (x == 5 && z >= 7));
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 };
            renderer.Skirt.Enabled = false;
            renderer.Render(1, new SliceSettings());

            ChunkBatch batch = Batches(renderer)[world.Chunks.ChunkIndexOfCell(5, 5, 1)]!;
            Mesh? mesh = batch.Skin.Mesh;
            Assert.That(mesh, Is.Not.Null, "the foot cells drew no skin");
            Vector3[] vertices = mesh!.vertices;

            int[] cornerX = { 0, 1, 1, 0 }, cornerZ = { 0, 0, 1, 1 };
            int checkedVertices = 0;
            for (int z = 0; z < Side; z++)
            {
                if (!BankLayout.RampCorners(world.Model, 5, z, 1, out BankLayout.Ramp ramp)) continue;
                for (int c = 0; c < 4; c++)
                {
                    float wx = (5 + cornerX[c]) * CellMetrics.SizeXZ, wz = (z + cornerZ[c]) * CellMetrics.SizeXZ;
                    // What a figure stands on at the corner, read back through the one owner...
                    float stood = BankLayout.RiseAt(world.Model, new CellRef(5, z, 1),
                        wx + (cornerX[c] == 0 ? 1e-4f : -1e-4f), wz + (cornerZ[c] == 0 ? 1e-4f : -1e-4f));
                    Assert.That(stood, Is.EqualTo(ramp.Corner(c) * CellMetrics.SizeY).Within(1e-2f),
                        $"RiseAt disagrees with the ramp's corner {c} at ({5},{z})");
                    // ...and the mesh holds a vertex exactly there.
                    var expected = new Vector3(wx, CellMetrics.SizeY + ramp.Corner(c) * CellMetrics.SizeY + GroundSkin.RampLift, wz);
                    bool found = false;
                    foreach (Vector3 v in vertices)
                        if ((v - expected).sqrMagnitude < 1e-6f) { found = true; break; }
                    Assert.That(found, Is.True, $"no skin vertex where the ramp's corner {c} of ({5},{z}) stands: {expected}");
                    checkedVertices++;
                }
            }
            Assert.That(checkedVertices, Is.GreaterThan(0), "no ramp vertices were compared, so this proves nothing");
        }

        /// <summary>A stream one cell wide at layer 1 (x = 5) with grass banks either side, its bed at layer 0.</summary>
        static RenderTestWorld Stream()
        {
            var world = new RenderTestWorld(Side, Side, 4);
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
                if (x == 5) world.Surface(x, z, 1, NaturalContent.TerrainShallowWater);
                else world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            }
            return world.Publish();
        }

        /// <summary>
        /// A stream bank runs down into the water instead of stopping at a square rim: the corners
        /// on the water side drop to just above the water line, and a colonist on the bank is drawn
        /// on that slope.
        /// </summary>
        [Test]
        public void AStreamBankSlopesIntoTheWater()
        {
            RenderTestWorld world = Stream();
            Assert.That(BankLayout.BankDips(world.Model, 4, 4, 1, out BankLayout.Ramp dip), Is.True, "the bank does not dip");
            float drop = -BankLayout.WaterBankDrop / CellMetrics.SizeY;
            Assert.That(new[] { dip.R0, dip.R1, dip.R2, dip.R3 }, Is.EqualTo(new[] { 0f, drop, drop, 0f }),
                "the water-side corners (+x) should drop and the far side stay at the top");
            Assert.That(BankLayout.BankDips(world.Model, 2, 4, 1, out _), Is.False, "a cell away from the water dips");

            // The air over the bank is where a colonist walks: drawn at the top on the dry side and
            // just above the water line at the edge.
            float dry = BankLayout.RiseAt(world.Model, new CellRef(4, 4, 2), 4 * CellMetrics.SizeXZ + 0.01f, 4.5f * CellMetrics.SizeXZ);
            float edge = BankLayout.RiseAt(world.Model, new CellRef(4, 4, 2), 5 * CellMetrics.SizeXZ - 0.01f, 4.5f * CellMetrics.SizeXZ);
            Assert.That(dry, Is.EqualTo(0f).Within(1e-2f));
            Assert.That(edge, Is.EqualTo(-BankLayout.WaterBankDrop).Within(1e-2f));
            float waterLine = ChunkMesher.WaterSurface * CellMetrics.SizeY;
            Assert.That(2 * CellMetrics.SizeY + edge, Is.GreaterThan(CellMetrics.SizeY + waterLine),
                "the bank went under the water it slopes into");
        }

        [Test]
        public void TheSkinReplacesTheBoxesAndOffGivesThemBack()
        {
            RenderTestWorld world = Meadow((x, z) => x >= 6);
            int skinInstances, boxInstances, skinTriangles;
            using (var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 })
            {
                renderer.Skirt.Enabled = false;
                renderer.PrimeAll(1, new SliceSettings());
                renderer.Render(1, new SliceSettings());
                skinInstances = renderer.InstancesDrawn;
                skinTriangles = renderer.SkinTrianglesDrawn;
            }

            GroundSkin.Enabled = false;
            world.Model.Remesh();
            using (var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 })
            {
                renderer.Skirt.Enabled = false;
                renderer.PrimeAll(1, new SliceSettings());
                renderer.Render(1, new SliceSettings());
                boxInstances = renderer.InstancesDrawn;
                Assert.That(renderer.SkinTrianglesDrawn, Is.Zero, "the skin drew with the switch off");
            }

            Assert.That(skinTriangles, Is.GreaterThan(0), "the skin drew nothing");
            Assert.That(skinInstances, Is.LessThan(boxInstances),
                "the skin should take the flat tops and the banks out of the instanced buckets");
        }

        /// <summary>
        /// P3: the height of a slope has one owner. The old per-shape function
        /// <c>BankMesh.HeightAt</c> may be read only by <c>BankLayout</c> (the owner) and by the
        /// bootstrap's cursor branch that runs with the skin switched off; anything new that stands a
        /// thing on a slope asks <c>BankLayout.RiseAt</c>.
        /// </summary>
        [Test]
        public void TheHeightOfASlopeHasOneOwner()
        {
            string root = Path.GetFullPath("Assets/Odyssey/Presentation");
            var allowed = new HashSet<string> { "BankLayout.cs", "BankMesh.cs", "OdysseyBootstrap.cs" };
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                if (allowed.Contains(Path.GetFileName(file))) continue;
                foreach (string line in File.ReadAllLines(file))
                {
                    string code = line.TrimStart();
                    if (code.StartsWith("//") || code.StartsWith("///")) continue;
                    if (code.Contains("BankMesh.HeightAt(")) offenders.Add(Path.GetFileName(file));
                }
            }
            Assert.That(offenders, Is.Empty,
                "read the slope from BankLayout.RiseAt, which is what the skin draws: " + string.Join(", ", offenders));
        }

        static ChunkBatch?[] Batches(ChunkRenderer renderer) =>
            (ChunkBatch?[])typeof(ChunkRenderer)
                .GetField("_batches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
    }
}
