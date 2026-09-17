#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The sheet a body of water shows where nothing holds it in.
    ///
    /// <para><b>What these are really guarding.</b> The owner's report of 2026-09-17 was water "in
    /// mid air" on the terraced board, and the cause was that the surface was the only thing drawn:
    /// a lid hanging 2.16 m over its own bed with open air on every side. The fix adds faces, and
    /// adding faces to water is the dangerous direction — water writes no depth, so two coincident
    /// faces each add their own alpha, and that is precisely the fault that once ruled the whole
    /// board into dark squares along every cell boundary. So the interesting assertions here are
    /// the negative ones: no face between two water cells, none against a bank, none at the rim.
    /// </para>
    /// </summary>
    public class WaterFaceTests
    {
        static ChunkBatch MeshLayer(RenderTestWorld world, int layer)
        {
            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model);
            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            mesher.Mesh(batch, layer * chunksPerLayer);
            return batch;
        }

        /// <summary>Every water-face instance in a batch, whichever bucket it landed in.</summary>
        static List<Matrix4x4> Falls(RenderTestWorld world, ChunkBatch batch)
        {
            var found = new List<Matrix4x4>();
            foreach (InstanceBucket bucket in batch.Roof)
            {
                if (bucket.Module != world.Model.WaterFallModule) continue;
                for (int i = 0; i < bucket.Count; i++) found.Add(bucket.Matrices[i]);
            }
            return found;
        }

        /// <summary>The top of a face instance, in metres. The mesh hangs from its own origin.</summary>
        static float TopOf(Matrix4x4 m) => m.MultiplyPoint3x4(Vector3.zero).y;

        /// <summary>
        /// Where the bottom edge of a face lands, in metres.
        ///
        /// <para>This and not the sheet's length is what the tests assert, and the distinction is
        /// the point: the sheet is stood off its rock face and tucked up behind its own surface to
        /// close a hairline, so its raw height carries two constants that mean nothing on their
        /// own. Where the water <i>reaches</i> is the thing that has to be right — a fall that
        /// stops short leaves a gap, and one that overshoots hangs through the pool below.</para>
        /// </summary>
        static float BottomOf(Matrix4x4 m) =>
            m.MultiplyPoint3x4(Vector3.zero).y - m.MultiplyVector(Vector3.up).magnitude;

        /// <summary>
        /// A pond walled all round by ground one layer up — the ordinary case, and the one the
        /// generator guarantees: a dry bank stands exactly one layer over the bed it looks down on
        /// (ADR 0009, decision 6). The bank's own cell fills the water's layer, so there is nothing
        /// to show a face to.
        /// </summary>
        [Test]
        public void WaterHeldInOnEverySideShowsNoFace()
        {
            var world = new RenderTestWorld(5, 5, 4);
            for (int z = 0; z < 5; z++)
            for (int x = 0; x < 5; x++)
                world.Solid(x, z, 0);                    // the bed

            // A one-cell pond at the centre of layer 1, with solid bank all around it.
            for (int z = 0; z < 5; z++)
            for (int x = 0; x < 5; x++)
                if (x != 2 || z != 2) world.Solid(x, z, 1);

            world.Surface(2, 2, 1, NaturalContent.TerrainShallowWater);
            world.Publish();

            Assert.That(Falls(world, MeshLayer(world, 1)).Count, Is.EqualTo(0),
                "every side is walled by the bank, so a face would be drawn inside solid rock");
        }

        /// <summary>
        /// The one that would bring back the dark squares. Two water cells side by side share an
        /// edge; a face on it would be drawn twice, once from each cell, in the same plane.
        /// </summary>
        [Test]
        public void NoFaceIsDrawnBetweenTwoWaterCells()
        {
            var world = new RenderTestWorld(6, 6, 4);
            for (int z = 0; z < 6; z++)
            for (int x = 0; x < 6; x++)
            {
                world.Solid(x, z, 0);
                // A two-cell pool, walled all round, so the only candidate face is the shared one.
                bool pool = z == 2 && (x == 2 || x == 3);
                if (!pool) world.Solid(x, z, 1);
            }

            world.Surface(2, 2, 1, NaturalContent.TerrainShallowWater);
            world.Surface(3, 2, 1, NaturalContent.TerrainShallowWater);
            world.Publish();

            Assert.That(Falls(world, MeshLayer(world, 1)).Count, Is.EqualTo(0),
                "a face between two water cells is two coincident translucent quads, which is " +
                "the fault that ruled the board into dark squares");
        }

        /// <summary>
        /// A lip: water with an open side and nothing below to catch it. The sheet closes the
        /// channel, so it falls only as far as the bed this water is lying on.
        /// </summary>
        [Test]
        public void WaterWithAnOpenSideShowsAFaceDownToItsOwnBed()
        {
            var world = new RenderTestWorld(6, 6, 4);
            for (int z = 0; z < 6; z++)
            for (int x = 0; x < 6; x++)
                world.Solid(x, z, 0);

            // Bank on three sides of the water cell; the fourth (+x) is left open air.
            world.Solid(2, 2, 1);
            world.Solid(3, 1, 1);
            world.Solid(3, 3, 1);
            world.Surface(3, 2, 1, NaturalContent.TerrainShallowWater);
            world.Publish();

            List<Matrix4x4> falls = Falls(world, MeshLayer(world, 1));
            Assert.That(falls.Count, Is.EqualTo(1), "exactly the one open side gets a face");
            Assert.That(BottomOf(falls[0]),
                Is.EqualTo(CellMetrics.FloorCentre(3, 2, 1).y).Within(1e-3f),
                "with nothing below it the sheet reaches its own bed and stops");
        }

        /// <summary>
        /// A cascade step, which is what the terraced board makes and what the owner photographed.
        /// Both surfaces stand at the same height inside their own cell, so the drop between them
        /// is exactly one cell however deep either is.
        /// </summary>
        [Test]
        public void WaterOverWaterFallsAFullCell()
        {
            var world = new RenderTestWorld(6, 6, 5);
            for (int z = 0; z < 6; z++)
            for (int x = 0; x < 6; x++)
                world.Solid(x, z, 0);

            // The lower pool: bed at layer 1, water at layer 2.
            world.Solid(3, 2, 1);
            world.Surface(3, 2, 2, NaturalContent.TerrainShallowWater);

            // The upper pool one layer up and one cell over: bed at layer 2, water at layer 3.
            world.Solid(2, 2, 1);
            world.Solid(2, 2, 2);
            world.Surface(2, 2, 3, NaturalContent.TerrainShallowWater);

            // Wall the upper cell's other three sides so the step is the only face it can show.
            world.Solid(1, 2, 3);
            world.Solid(2, 1, 3);
            world.Solid(2, 3, 3);
            world.Publish();

            List<Matrix4x4> falls = Falls(world, MeshLayer(world, 3));
            Assert.That(falls.Count, Is.EqualTo(1), "the step is the only open side");
            Assert.That(BottomOf(falls[0]),
                Is.EqualTo(CellMetrics.FloorCentre(2, 2, 2).y
                           + CellMetrics.SizeY * ChunkMesher.WaterSurface).Within(1e-3f),
                "the sheet reaches exactly the surface of the water one layer down");
        }

        /// <summary>
        /// The rim of the map holds water in, exactly as it holds terrain in for
        /// <c>ChunkMesher.ExposedSides</c>. Treating the boundary as open once drew a cross-section
        /// wall round the whole perimeter; here it would hang a curtain of water off the edge.
        /// </summary>
        [Test]
        public void TheWorldBoundaryHoldsWaterIn()
        {
            var world = new RenderTestWorld(4, 4, 3);
            for (int z = 0; z < 4; z++)
            for (int x = 0; x < 4; x++)
                world.Solid(x, z, 0);

            // Water in the corner cell, with its two in-board sides walled. Both remaining sides
            // are the rim.
            world.Solid(1, 0, 1);
            world.Solid(0, 1, 1);
            world.Surface(0, 0, 1, NaturalContent.TerrainShallowWater);
            world.Publish();

            Assert.That(Falls(world, MeshLayer(world, 1)).Count, Is.EqualTo(0),
                "the rim is not an exposed face");
        }

        /// <summary>
        /// The face starts at its own surface, or fractionally above it, and never below.
        ///
        /// <para>Below is the failure that matters: the sheet is stood off the rock it pours over
        /// so as not to z-fight with it, which opens a slot between the two, and at the brow of the
        /// fall that slot is a line of sight onto the terrace behind — a hairline of lit grass
        /// along the top of every waterfall. The tuck closes it, and this is what holds it
        /// closed.</para>
        /// </summary>
        [Test]
        public void TheFaceHangsFromTheSurfaceItBelongsTo()
        {
            GroundRelief.Reset();
            var world = new RenderTestWorld(6, 6, 4);
            for (int z = 0; z < 6; z++)
            for (int x = 0; x < 6; x++)
                world.Solid(x, z, 0);

            world.Solid(2, 2, 1);
            world.Solid(3, 1, 1);
            world.Solid(3, 3, 1);
            world.Surface(3, 2, 1, NaturalContent.TerrainShallowWater);
            world.Publish();

            ChunkBatch batch = MeshLayer(world, 1);
            List<Matrix4x4> falls = Falls(world, batch);
            Assert.That(falls.Count, Is.EqualTo(1));

            float surface = CellMetrics.FloorCentre(3, 2, 1).y
                            + CellMetrics.SizeY * ChunkMesher.WaterSurface;
            Assert.That(TopOf(falls[0]), Is.GreaterThanOrEqualTo(surface),
                "a sheet starting below its own surface shows a hairline of daylight at the lip");
            Assert.That(TopOf(falls[0]), Is.LessThan(surface + 0.15f),
                "and it is a tuck, not a wall standing proud of the water");
        }
    }

    /// <summary>The two sheets themselves, which are the only water geometry there is.</summary>
    public class WaterMeshTests
    {
        [Test]
        public void TheSurfaceIsOneQuadFacingUp()
        {
            WaterMesh.Invalidate();
            Mesh mesh = WaterMesh.Surface;

            Assert.That(mesh.vertexCount, Is.EqualTo(4), "a sheet, not a slab");
            Assert.That(mesh.triangles.Length, Is.EqualTo(6));
            foreach (Vector3 n in mesh.normals)
                Assert.That(Vector3.Dot(n, Vector3.up), Is.EqualTo(1f).Within(1e-4f));
            foreach (Vector3 v in mesh.vertices)
                Assert.That(v.y, Is.EqualTo(0f).Within(1e-4f),
                    "flat at its own origin, so the water lies exactly where WaterSurface says");
        }

        [Test]
        public void TheFaceHangsOneUnitAndLooksOutwards()
        {
            WaterMesh.Invalidate();
            Mesh mesh = WaterMesh.Fall;

            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            foreach (Vector3 n in mesh.normals)
                Assert.That(Vector3.Dot(n, Vector3.forward), Is.EqualTo(1f).Within(1e-4f),
                    "Directions.Yaw turns local +z onto the bearing, so +z is outwards");

            float top = float.MinValue, bottom = float.MaxValue;
            foreach (Vector3 v in mesh.vertices)
            {
                if (v.y > top) top = v.y;
                if (v.y < bottom) bottom = v.y;
            }

            Assert.That(top, Is.EqualTo(0f).Within(1e-4f), "hangs from its origin");
            Assert.That(bottom, Is.EqualTo(-1f).Within(1e-4f),
                "one local unit is one metre of fall, so the instance scale is the drop");
        }

        /// <summary>
        /// Wound outwards. An inside-out quad is valid geometry that reports no error and simply
        /// is not there when you look at it — the fault that once turned every primitive in the
        /// renderer inside out, and which no test would otherwise catch here.
        /// </summary>
        [Test]
        public void BothSheetsAreWoundToFaceTheWayTheirNormalPoints()
        {
            WaterMesh.Invalidate();
            foreach (Mesh mesh in new[] { WaterMesh.Surface, WaterMesh.Fall })
            {
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                Vector3 geometric = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]).normalized;

                Assert.That(Vector3.Dot(geometric, mesh.normals[0]), Is.GreaterThan(0.9f),
                    $"{mesh.name} is wound inside out");
            }
        }
    }
}
