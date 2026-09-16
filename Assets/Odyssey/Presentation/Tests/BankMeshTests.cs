#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The bank that makes a terrace step somewhere you can walk up, and the four conditions that
    /// decide where one belongs.
    ///
    /// <para>A riser is a whole cell — 3.0 m, because ADR 0002 fixes the layer height and calls it
    /// effectively irreversible — while the simulation says a colonist hops straight up it. So the
    /// board showed a wall where the game had a path. The bank is the picture of that path, and it
    /// is a facade in the strict sense: no cell knows it is there, nothing walks on it, and it is
    /// in neither the save nor the state hash.</para>
    /// </summary>
    public class BankMeshTests
    {
        const float Tolerance = 1e-4f;

        [SetUp]
        public void ResetLevers() => GroundMesh.ResetLevers();

        [TearDown]
        public void RestoreLevers() => GroundMesh.ResetLevers();

        static IEnumerable<int> Variants
        {
            get { for (int v = 0; v < BankMesh.Variants; v++) yield return v; }
        }

        // ------------------------------------------------------------------ the mesh

        [Test]
        public void ABankFillsItsCellAndNeverLeavesIt()
        {
            // It is drawn in an empty cell, so anything reaching past that cell reaches into a
            // cell some other rule is responsible for. Above the top is the worst direction: that
            // is the terrace a colonist is standing on, and earth through the feet is the fault
            // RockMesh forbids raising for.
            foreach (int v in Variants)
            foreach (Vector3 vertex in BankMesh.For(v).vertices)
            {
                Assert.That(Mathf.Abs(vertex.x), Is.LessThanOrEqualTo(0.5f + Tolerance),
                    $"variant {v}: {vertex} leaves its cell in x");
                Assert.That(Mathf.Abs(vertex.z), Is.LessThanOrEqualTo(0.5f + Tolerance),
                    $"variant {v}: {vertex} leaves its cell in z");
                Assert.That(vertex.y, Is.LessThanOrEqualTo(0.5f + Tolerance),
                    $"variant {v}: {vertex} rises above the terrace it climbs to");
                Assert.That(vertex.y, Is.GreaterThanOrEqualTo(-0.5f - BankMesh.Sink - Tolerance),
                    $"variant {v}: {vertex} sinks past its own foot");
            }
        }

        [Test]
        public void TheTopTreadMeetsTheTerraceExactly()
        {
            // The last step is the one that has to be right to the millimetre: it is where the
            // bank becomes the ground above it. A tread a few centimetres short reads as a lip.
            foreach (int v in Variants)
            {
                Assert.That(BankMesh.TreadHeight(v, BankMesh.Steps - 1), Is.EqualTo(0.5f).Within(Tolerance),
                    $"variant {v}: the top tread does not meet the cell top");

                float highest = -1f;
                foreach (Vector3 vertex in BankMesh.For(v).vertices) highest = Mathf.Max(highest, vertex.y);
                Assert.That(highest, Is.EqualTo(0.5f).Within(Tolerance),
                    $"variant {v}: the bank stops at {highest} rather than at the terrace top");
            }
        }

        [Test]
        public void ItIsSunkIntoTheGroundItStandsOn()
        {
            // The cell below wears a GroundMesh top whose rim may dip. A bank sitting exactly on
            // the nominal floor would hang over that dip along its whole foot.
            // Measured against the lever at its documented setting, not at the shipped zero:
            // the inequality has to hold if anybody ever turns the ripple back on, and at zero it
            // would hold trivially and prove nothing.
            GroundMesh.MaxRipple = 0.012f;
            Assert.That(BankMesh.Sink, Is.GreaterThan(GroundMesh.MaxRipple),
                "a bank can hang over the dip in the ground under it");
        }

        [Test]
        public void TheStepsClimbAndNeverDoubleBack()
        {
            // Monotonic by construction rather than by luck: each tread edge is the even division
            // plus a bounded wobble, and the wobble is less than half the spacing, so no amount of
            // jitter can put two edges out of order and turn a tread inside out.
            foreach (int v in Variants)
            {
                float[] edges = BankMesh.Edges(v);
                Assert.That(edges.Length, Is.EqualTo(BankMesh.Steps + 1));
                Assert.That(edges[0], Is.EqualTo(-0.5f).Within(Tolerance), "the foot does not start at the cell edge");
                Assert.That(edges[BankMesh.Steps], Is.EqualTo(0.5f).Within(Tolerance), "the back is not the cell edge");

                for (int k = 1; k < edges.Length; k++)
                    Assert.That(edges[k], Is.GreaterThan(edges[k - 1]),
                        $"variant {v}: tread edge {k} is behind edge {k - 1}");

                for (int k = 1; k < BankMesh.Steps; k++)
                    Assert.That(BankMesh.TreadHeight(v, k), Is.GreaterThan(BankMesh.TreadHeight(v, k - 1)),
                        $"variant {v}: tread {k} is no higher than tread {k - 1}");
            }
        }

        [Test]
        public void ItIsLowAtTheFootAndHighAgainstTheStep()
        {
            // Local +z faces the step, because that is what Directions.Yaw turns a module's local
            // +z to do. Getting this backwards draws a bank running down from the terrace into
            // open ground, which is a perfectly good-looking piece of geometry pointing the wrong
            // way — the kind of fault that survives a screenshot.
            foreach (int v in Variants)
            {
                float atFoot = -1f, atStep = -1f;
                foreach (Vector3 vertex in BankMesh.For(v).vertices)
                {
                    if (vertex.z < -0.4f) atFoot = Mathf.Max(atFoot, vertex.y);
                    if (vertex.z > 0.4f) atStep = Mathf.Max(atStep, vertex.y);
                }

                Assert.That(atStep, Is.GreaterThan(atFoot + 0.3f),
                    $"variant {v}: the bank is not appreciably higher against the step than at its foot");
            }
        }

        [Test]
        public void EveryFacetFacesOutwards()
        {
            foreach (int v in Variants)
            {
                Mesh mesh = BankMesh.For(v);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 wound = Vector3.Cross(vertices[triangles[t + 1]] - a, vertices[triangles[t + 2]] - a);
                    if (wound.sqrMagnitude < 1e-9f) continue;
                    Assert.That(Vector3.Dot(wound.normalized, normals[triangles[t]]), Is.GreaterThan(0.5f),
                        $"variant {v} triangle {t / 3} is wound against its own normal");
                }
            }
        }

        [Test]
        public void ABankIsCachedAndTheVariantsDiffer()
        {
            Assert.That(BankMesh.For(1), Is.SameAs(BankMesh.For(1)), "a new mesh per call would leak one per cell");
            Assert.That(BankMesh.For(-1), Is.SameAs(BankMesh.For(BankMesh.Variants - 1)), "negative variants must wrap");

            var seen = new HashSet<string>();
            foreach (int v in Variants)
            {
                var key = new System.Text.StringBuilder();
                foreach (float edge in BankMesh.Edges(v)) key.Append(edge.ToString("F4")).Append('|');
                Assert.That(seen.Add(key.ToString()), Is.True,
                    $"variant {v} steps identically to an earlier one, so a long riser is a repeated stamp");
            }
        }

        // ------------------------------------------------------------------ where one belongs

        static ChunkBatch MeshLayer(RenderTestWorld world, int layer)
        {
            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model);
            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            mesher.Mesh(batch, layer * chunksPerLayer);
            return batch;
        }

        /// <summary>How many bank instances the layer drew, counted by module rather than by mesh.</summary>
        static int BanksIn(RenderTestWorld world, ChunkBatch batch)
        {
            var banks = new HashSet<int>();
            for (ushort terrain = 0; terrain < NaturalContent.TerrainCount; terrain++)
            for (int v = 0; v < BankMesh.Variants; v++)
            {
                int module = world.Model.BankModuleFor(terrain, v);
                if (module != 0) banks.Add(module);
            }

            int n = 0;
            foreach (InstanceBucket bucket in batch.Body)
                if (banks.Contains(bucket.Module)) n += bucket.Count;
            return n;
        }

        /// <summary>
        /// A board whose x &lt; half is one layer higher than the rest: a straight terrace step
        /// running the whole depth of the map, with <paramref name="rise"/> layers between them.
        /// </summary>
        static RenderTestWorld Step(int rise, ushort stepTerrain)
        {
            const int n = 6;
            var world = new RenderTestWorld(n, n, 8);

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x < n / 2 ? 1 + rise : 1;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, x < n / 2 ? stepTerrain : NaturalContent.TerrainGrass);
            }

            return world.Publish();
        }

        [Test]
        public void EveryOneLayerStepGrowsExactlyOneBank()
        {
            // The low half is at layer 1, the high half at layer 2, and the bank stands in the
            // empty cell at layer 2 on the low side — the same layer as the riser it climbs, which
            // is what lets one single-layer pass see both ends of it.
            RenderTestWorld world = Step(rise: 1, stepTerrain: NaturalContent.TerrainGrass);

            Assert.That(BanksIn(world, MeshLayer(world, 2)), Is.EqualTo(6),
                "one bank per cell along a six-deep step, and no more");
            Assert.That(BanksIn(world, MeshLayer(world, 1)), Is.Zero,
                "a bank was drawn on the layer below the step it climbs");
        }

        [Test]
        public void FlatGroundGrowsNone()
        {
            // The control, and the one that matters most for cost: the meadow is nearly all flat,
            // and a bank appearing on flat ground would put an instance on every cell of the board.
            RenderTestWorld world = Step(rise: 0, stepTerrain: NaturalContent.TerrainGrass);
            for (int layer = 0; layer < 8; layer++)
                Assert.That(BanksIn(world, MeshLayer(world, layer)), Is.Zero,
                    $"flat ground grew a bank on layer {layer}");
        }

        [Test]
        public void ATwoLayerFaceIsAWallAndGrowsNone()
        {
            // The rule the simulation already enforces: a hop is one block, and two is a wall a
            // colonist cannot pass (VerticalMovementTests.ATwoBlockFaceIsAWall). Drawing a bank up
            // one would promise a route that does not exist, which is worse than drawing a cliff.
            RenderTestWorld world = Step(rise: 2, stepTerrain: NaturalContent.TerrainGrass);
            for (int layer = 0; layer < 8; layer++)
                Assert.That(BanksIn(world, MeshLayer(world, layer)), Is.Zero,
                    $"a two-layer face grew a bank on layer {layer}");
        }

        [Test]
        public void CutRockKeepsItsSheerFace()
        {
            // A grassy ramp growing out of cut rock is a lie about what was done to it. A quarry
            // should read as quarried, and the colonist hops the ledge without a bank to do it.
            RenderTestWorld world = Step(rise: 1, stepTerrain: CoreContent.TerrainRock);
            Assert.That(BanksIn(world, MeshLayer(world, 2)), Is.Zero, "cut rock grew a bank");
        }

        [Test]
        public void GroundUnderARoofGrowsNone()
        {
            // Banks are for the outdoor hillside, which is where the ruled 3 m rectangle is the
            // fault. Inside a working, sharp edges are the honest picture of what was dug.
            const int n = 6;
            var world = new RenderTestWorld(n, n, 8);
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x < n / 2 ? 2 : 1;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, NaturalContent.TerrainGrass);
                world.Slab(x, z, 4);          // a roof two layers over the whole board
            }
            world.Publish();

            Assert.That(BanksIn(world, MeshLayer(world, 2)), Is.Zero, "a roofed step grew a bank");
        }

        [Test]
        public void TheLeverTurnsThemOff()
        {
            // The check harness photographs the same board with and without, because whether this
            // looks better than a cliff is a judgement and not something to reason out of source.
            RenderTestWorld world = Step(rise: 1, stepTerrain: NaturalContent.TerrainGrass);

            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model) { Banks = false };
            mesher.Mesh(batch, 2 * world.Chunks.ChunksX * world.Chunks.ChunksZ);

            Assert.That(BanksIn(world, batch), Is.Zero, "the lever does not turn banks off");
        }

        [Test]
        public void AnInsideCornerGrowsOneBankAndNotTwo()
        {
            // **A z-fighting fix, and the fault was invisible to every other test.** A bank fills
            // its cell in plan, so two banks in one cell are two boxes turned ninety degrees to
            // each other — and the side wall of one lands in the same plane as the *back* wall of
            // the other, facing the same way. Coplanar surfaces with opposite normals are harmless,
            // because culling removes one from every viewpoint; coplanar surfaces facing the same
            // way are two candidates for one pixel with nothing to separate them, and the result
            // flickers as the camera moves and the rounding changes. The owner saw it before any
            // test did, because no test was looking at pairs of surfaces.
            //
            // A straight run is fine, and that is what the count in the test above is checking: two
            // neighbouring banks touch along walls that face away from each other.
            const int n = 5;
            var world = new RenderTestWorld(n, n, 8);
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                // High everywhere except a single notch cut out of the corner at (0,0).
                int top = (x == 0 && z == 0) ? 1 : 2;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, NaturalContent.TerrainGrass);
            }
            world.Publish();

            Assert.That(BanksIn(world, MeshLayer(world, 2)), Is.EqualTo(1),
                "the notch faces two risers and must still grow exactly one bank: two would " +
                "put coplanar same-facing walls in one cell, which is the flicker this pins");
        }
    }
}
