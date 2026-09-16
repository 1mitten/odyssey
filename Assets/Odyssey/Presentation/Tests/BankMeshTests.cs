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
    /// The slope that makes a terrace step somewhere you can walk up, and the one property that
    /// makes three shapes enough: <b>they agree exactly where they meet</b>.
    ///
    /// <para>A riser is a whole cell — ADR 0002 fixes the layer at 3.0 m and calls it effectively
    /// irreversible — while the simulation says a colonist hops straight up it. So the board showed
    /// a wall where the game had a path. The bank is the picture of that path, and it is a facade in
    /// the strict sense: no cell knows it is there, nothing walks on it, and it is in neither the
    /// save nor the state hash.</para>
    ///
    /// <para>It was a jittered staircase first and the owner named the fault exactly: neighbouring
    /// cells put their treads in different places, so a run came out as a ridge of misaligned bars.
    /// The tests below are mostly about the replacement not being able to do that again.</para>
    /// </summary>
    public class BankMeshTests
    {
        const float Tolerance = 1e-4f;

        [SetUp]
        public void Reset() => GroundMesh.ResetLevers();

        [TearDown]
        public void Restore() => GroundMesh.ResetLevers();

        static IEnumerable<BankMesh.Kind> Kinds
        {
            get
            {
                yield return BankMesh.Kind.Straight;
                yield return BankMesh.Kind.Inner;
                yield return BankMesh.Kind.Outer;
            }
        }

        /// <summary>How many steps a sweep across a cell takes. Twenty-one samples, ends included.</summary>
        const int Samples = 20;

        /// <summary>
        /// Exact sample positions from -0.5 to 0.5 inclusive.
        ///
        /// <para>Computed from the index rather than accumulated, and that is not fussiness: an
        /// earlier version wrote <c>for (float t = -0.5f; t &lt;= 0.5f; t += 0.05f)</c>, which
        /// accumulates enough error over twenty additions to stop at 0.45 and never sample the edge
        /// at all. It then reported that the slope failed to reach the top of the step, which was
        /// the loop failing to reach the top of the cell. A test that never visits the boundary is
        /// worse than no test, because the boundary is the only place these shapes are interesting.</para>
        /// </summary>
        static float At(int i) => -0.5f + i / (float)Samples;

        // ------------------------------------------------------------------ the surface

        [Test]
        public void EveryPieceClimbsFromTheFloorToTheStep()
        {
            // The two heights that have to be exact. The top edge is where the bank becomes the
            // ground above it, and the foot is where it becomes the ground below; a few
            // centimetres out at either end reads as a lip rather than as a slope.
            foreach (BankMesh.Kind kind in Kinds)
            {
                float low = float.MaxValue, high = float.MinValue;
                for (int i = 0; i <= Samples; i++)
                for (int j = 0; j <= Samples; j++)
                {
                    float h = BankMesh.HeightAt(kind, At(i), At(j));
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                }

                Assert.That(high, Is.EqualTo(0.5f).Within(Tolerance),
                    $"{kind} stops at {high} rather than at the top of the step");
                Assert.That(low, Is.EqualTo(-0.5f).Within(Tolerance),
                    $"{kind} bottoms at {low} rather than on the floor it stands on");
            }
        }

        [Test]
        public void ThePiecesAgreeAlongEveryEdgeTheyShare()
        {
            // **The whole reason these three shapes were chosen**, and the thing the jittered
            // staircase could never have: a run of banks around a terrace, corners and all, is one
            // continuous surface. Width is matched by construction rather than by hand.
            //
            // Local +z and +x point at the steps, so a piece turned by one quarter presents its
            // local -x edge where its neighbour presents local +x, and the heights along those two
            // edges have to be the same function.
            for (int i = 0; i <= Samples; i++)
            {
                float t = At(i);

                // A straight piece meets another straight piece alongside it: both are y = z.
                Assert.That(BankMesh.HeightAt(BankMesh.Kind.Straight, 0.5f, t),
                    Is.EqualTo(BankMesh.HeightAt(BankMesh.Kind.Straight, -0.5f, t)).Within(Tolerance),
                    "two straight banks side by side do not line up");

                // A corner meets a straight piece on its open -x side: max(-0.5, z) is z.
                Assert.That(BankMesh.HeightAt(BankMesh.Kind.Inner, -0.5f, t),
                    Is.EqualTo(BankMesh.HeightAt(BankMesh.Kind.Straight, 0.5f, t)).Within(Tolerance),
                    "an inside corner does not line up with the straight bank beside it");

                // A hip meets a straight piece on its +x side: min(0.5, z) is z.
                Assert.That(BankMesh.HeightAt(BankMesh.Kind.Outer, 0.5f, t),
                    Is.EqualTo(BankMesh.HeightAt(BankMesh.Kind.Straight, -0.5f, t)).Within(Tolerance),
                    "an outside corner does not line up with the straight bank beside it");
            }
        }

        [Test]
        public void AStraightPieceIsFlatAcrossItsWidth()
        {
            // The Toblerone test. A slope that varied along the run is what made a line of banks
            // read as a ridge of bars instead of as one bank, and the jitter that caused it is
            // gone; nothing about the shape may reintroduce it.
            for (int j = 0; j <= Samples; j++)
            {
                float z = At(j);
                float at = BankMesh.HeightAt(BankMesh.Kind.Straight, -0.5f, z);
                for (int i = 0; i <= Samples; i++)
                    Assert.That(BankMesh.HeightAt(BankMesh.Kind.Straight, At(i), z), Is.EqualTo(at).Within(Tolerance),
                        $"a straight bank changes height across its width at z {z}");
            }
        }

        [Test]
        public void ACornerIsHighOnBothItsStepsAndLowAtTheOpenCorner()
        {
            // An inside corner has steps on local +z and +x, so it must reach the top along both of
            // those edges and fall away to the one corner that is open.
            for (int i = 0; i <= Samples; i++)
            {
                float t = At(i);
                Assert.That(BankMesh.HeightAt(BankMesh.Kind.Inner, t, 0.5f), Is.EqualTo(0.5f).Within(Tolerance),
                    "an inside corner does not meet the step on its +z side");
                Assert.That(BankMesh.HeightAt(BankMesh.Kind.Inner, 0.5f, t), Is.EqualTo(0.5f).Within(Tolerance),
                    "an inside corner does not meet the step on its +x side");
            }

            Assert.That(BankMesh.HeightAt(BankMesh.Kind.Inner, -0.5f, -0.5f), Is.EqualTo(-0.5f).Within(Tolerance),
                "an inside corner does not reach the floor at the corner that is open");
        }

        [Test]
        public void AHipRisesOnlyToTheCornerItWraps()
        {
            // An outside corner has no step orthogonally at all — only the one on the diagonal — so
            // it must be at floor level along both open edges and reach the top at that one point.
            for (int i = 0; i <= Samples; i++)
            {
                float t = At(i);
                Assert.That(BankMesh.HeightAt(BankMesh.Kind.Outer, t, -0.5f), Is.EqualTo(-0.5f).Within(Tolerance),
                    "a hip is not on the floor along its open -z side");
                Assert.That(BankMesh.HeightAt(BankMesh.Kind.Outer, -0.5f, t), Is.EqualTo(-0.5f).Within(Tolerance),
                    "a hip is not on the floor along its open -x side");
            }

            Assert.That(BankMesh.HeightAt(BankMesh.Kind.Outer, 0.5f, 0.5f), Is.EqualTo(0.5f).Within(Tolerance),
                "a hip does not reach the step on the diagonal it wraps");
        }

        [Test]
        public void TheSurfaceOnlyEverClimbs()
        {
            // Monotonic towards the steps. A dip anywhere would be a puddle in the middle of a
            // slope, and on a facade nothing would ever drain it.
            foreach (BankMesh.Kind kind in Kinds)
            for (int i = 0; i < Samples; i++)
            for (int j = 0; j < Samples; j++)
            {
                float x = At(i), z = At(j);
                Assert.That(BankMesh.HeightAt(kind, At(i + 1), z),
                    Is.GreaterThanOrEqualTo(BankMesh.HeightAt(kind, x, z) - Tolerance),
                    $"{kind} falls away towards its step along x at ({x}, {z})");
                Assert.That(BankMesh.HeightAt(kind, x, At(j + 1)),
                    Is.GreaterThanOrEqualTo(BankMesh.HeightAt(kind, x, z) - Tolerance),
                    $"{kind} falls away towards its step along z at ({x}, {z})");
            }
        }

        // ------------------------------------------------------------------ the mesh

        [Test]
        public void EveryPieceFillsItsCellAndNeverLeavesIt()
        {
            // It is drawn in an empty cell, so anything reaching past that cell reaches into one
            // some other rule is responsible for. Above the top is the worst direction: that is the
            // terrace a colonist stands on.
            foreach (BankMesh.Kind kind in Kinds)
            foreach (Vector3 vertex in BankMesh.For(kind).vertices)
            {
                Assert.That(Mathf.Abs(vertex.x), Is.LessThanOrEqualTo(0.5f + Tolerance),
                    $"{kind}: {vertex} leaves its cell in x");
                Assert.That(Mathf.Abs(vertex.z), Is.LessThanOrEqualTo(0.5f + Tolerance),
                    $"{kind}: {vertex} leaves its cell in z");
                Assert.That(vertex.y, Is.LessThanOrEqualTo(0.5f + Tolerance),
                    $"{kind}: {vertex} rises above the terrace it climbs to");
                Assert.That(vertex.y, Is.GreaterThanOrEqualTo(-0.5f - BankMesh.Sink - Tolerance),
                    $"{kind}: {vertex} sinks past its own foot");
            }
        }

        [Test]
        public void ItIsSunkIntoTheGroundItStandsOn()
        {
            // The cell below wears a GroundMesh top whose rim may move. A slope sitting exactly on
            // the nominal floor could hang over that along its whole foot.
            GroundMesh.MaxRipple = 0.012f;
            Assert.That(BankMesh.Sink, Is.GreaterThan(GroundMesh.MaxRipple),
                "a bank can hang over the dip in the ground under it");
        }

        [Test]
        public void TheMeshMatchesTheSurfaceItIsBuiltFrom()
        {
            // The triangles have to be the height function, or every property proved above is
            // proved about something that is not on screen.
            foreach (BankMesh.Kind kind in Kinds)
            foreach (Vector3 vertex in BankMesh.For(kind).vertices)
            {
                if (vertex.y < -0.5f - Tolerance) continue;     // the foot, which is below the surface
                Assert.That(vertex.y, Is.EqualTo(BankMesh.HeightAt(kind, vertex.x, vertex.z)).Within(1e-3f),
                    $"{kind}: the mesh has a vertex at {vertex} the surface does not pass through");
            }
        }

        [Test]
        public void NoPieceHasADegenerateTriangle()
        {
            // A slope that reaches the floor along an edge leaves that side with no height, and a
            // quad with coincident corners carries a triangle with no area — which renders as
            // nothing and shades from a normal that cannot be computed. The hip has two such sides,
            // so this is not hypothetical.
            //
            // **Not watertightness.** A bank is drawn in an empty cell and its back is buried in
            // the step, so it is closed, but the tests that matter here are about the surface.
            foreach (BankMesh.Kind kind in Kinds)
            {
                Mesh mesh = BankMesh.For(kind);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;

                Assert.That(triangles.Length, Is.GreaterThan(0), $"{kind} drew nothing");

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 wound = Vector3.Cross(vertices[triangles[t + 1]] - a, vertices[triangles[t + 2]] - a);
                    Assert.That(wound.sqrMagnitude, Is.GreaterThan(1e-12f),
                        $"{kind} has a degenerate triangle at {a}");
                    Assert.That(Vector3.Dot(wound.normalized, normals[triangles[t]]), Is.GreaterThan(0.5f),
                        $"{kind} triangle {t / 3} is wound against its own normal");
                }
            }
        }

        [Test]
        public void APieceIsCachedAndThereAreOnlyThree()
        {
            Assert.That(BankMesh.Kinds, Is.EqualTo(3));
            foreach (BankMesh.Kind kind in Kinds)
                Assert.That(BankMesh.For(kind), Is.SameAs(BankMesh.For(kind)),
                    "a new mesh per call would leak one per cell");

            Assert.That(BankMesh.For(BankMesh.Kind.Straight), Is.Not.SameAs(BankMesh.For(BankMesh.Kind.Inner)));
            Assert.That(BankMesh.For(-1), Is.SameAs(BankMesh.For(BankMesh.Kinds - 1)), "negative kinds must wrap");
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
            for (int kind = 0; kind < BankMesh.Kinds; kind++)
            {
                int module = world.Model.BankModuleFor(terrain, kind);
                if (module != 0) banks.Add(module);
            }

            int n = 0;
            foreach (InstanceBucket bucket in batch.Body)
                if (banks.Contains(bucket.Module)) n += bucket.Count;
            return n;
        }

        /// <summary>
        /// A board whose x &lt; half is <paramref name="rise"/> layers higher than the rest: a
        /// straight terrace step running the whole depth of the map.
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
            // and a bank on flat ground would put an instance on every cell of the board.
            RenderTestWorld world = Step(rise: 0, stepTerrain: NaturalContent.TerrainGrass);
            for (int layer = 0; layer < 8; layer++)
                Assert.That(BanksIn(world, MeshLayer(world, layer)), Is.Zero,
                    $"flat ground grew a bank on layer {layer}");
        }

        [Test]
        public void ATwoLayerFaceIsAWallAndGrowsNone()
        {
            // The simulation refuses it — VerticalMovementTests.ATwoBlockFaceIsAWall — and drawing
            // a way up one would promise a route that does not exist.
            RenderTestWorld world = Step(rise: 2, stepTerrain: NaturalContent.TerrainGrass);
            for (int layer = 0; layer < 8; layer++)
                Assert.That(BanksIn(world, MeshLayer(world, layer)), Is.Zero,
                    $"a two-layer face grew a bank on layer {layer}");
        }

        [Test]
        public void CutRockKeepsItsSheerFace()
        {
            // A grassy ramp growing out of cut rock is a lie about what was done to it.
            RenderTestWorld world = Step(rise: 1, stepTerrain: CoreContent.TerrainRock);
            Assert.That(BanksIn(world, MeshLayer(world, 2)), Is.Zero, "cut rock grew a bank");
        }

        [Test]
        public void GroundUnderARoofGrowsNone()
        {
            // Banks are for the outdoor hillside. Inside a working, sharp edges are the honest
            // picture of what was dug.
            const int n = 6;
            var world = new RenderTestWorld(n, n, 8);
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x < n / 2 ? 2 : 1;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, NaturalContent.TerrainGrass);
                world.Slab(x, z, 4);
            }
            world.Publish();

            Assert.That(BanksIn(world, MeshLayer(world, 2)), Is.Zero, "a roofed step grew a bank");
        }

        [Test]
        public void TheLeverTurnsThemOff()
        {
            RenderTestWorld world = Step(rise: 1, stepTerrain: NaturalContent.TerrainGrass);

            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model) { Banks = false };
            mesher.Mesh(batch, 2 * world.Chunks.ChunksX * world.Chunks.ChunksZ);

            Assert.That(BanksIn(world, batch), Is.Zero, "the lever does not turn banks off");
        }

        /// <summary>
        /// A high plateau occupying one quadrant, so the terrace turns a corner both ways: an inside
        /// corner where the two runs meet, and an outside one diagonally off the plateau's corner.
        /// </summary>
        static RenderTestWorld Plateau()
        {
            const int n = 7;
            var world = new RenderTestWorld(n, n, 8);

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x < 3 && z < 3 ? 2 : 1;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, NaturalContent.TerrainGrass);
            }

            return world.Publish();
        }

        [Test]
        public void ACornerIsFilledRatherThanBittenOut()
        {
            // **The corner fault.** The cell diagonally off the plateau's corner touches no step
            // orthogonally, so it used to get nothing and the run had a square bite out of it. It
            // is exactly the cell that should carry the hip.
            RenderTestWorld world = Plateau();
            ChunkBatch batch = MeshLayer(world, 2);

            // Three cells along each of the two exposed faces, plus the one on the diagonal.
            Assert.That(BanksIn(world, batch), Is.EqualTo(7),
                "the corner of a plateau is not banked all the way round");
        }

        [Test]
        public void APlateauCornerIsStraightRunsAndOneHip()
        {
            // Named by module so a shape cannot quietly stand in for another: a straight piece at a
            // corner would leave one of its two steps bare, and a hip on a straight run would slope
            // the wrong way entirely.
            //
            // A plateau corner has no *inside* corner in it, which is worth pinning because it is
            // the easy thing to assume: the three cells down each exposed face each touch exactly
            // one step, and only the cell on the diagonal touches none. An inside corner needs a
            // notch cut into the high ground, which is the fixture below.
            RenderTestWorld world = Plateau();
            ChunkBatch batch = MeshLayer(world, 2);

            Assert.That(Count(batch, Module(world, BankMesh.Kind.Straight)), Is.EqualTo(6),
                "the two exposed faces are not three cells each");
            Assert.That(Count(batch, Module(world, BankMesh.Kind.Inner)), Is.Zero,
                "a plateau corner grew an inside corner, which it has none of");
            Assert.That(Count(batch, Module(world, BankMesh.Kind.Outer)), Is.EqualTo(1),
                "the cell on the diagonal did not get a hip");
        }

        [Test]
        public void ANotchInTheHighGroundIsAnInsideCorner()
        {
            // The other corner. One cell cut out of a plateau touches steps on two adjacent sides,
            // and a straight piece there would climb one of them and leave the other a bare wall.
            const int n = 5;
            var world = new RenderTestWorld(n, n, 8);
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x == 0 && z == 0 ? 1 : 2;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, NaturalContent.TerrainGrass);
            }
            world.Publish();

            ChunkBatch batch = MeshLayer(world, 2);
            Assert.That(Count(batch, Module(world, BankMesh.Kind.Inner)), Is.EqualTo(1),
                "the notch did not grow an inside corner");
            Assert.That(BanksIn(world, batch), Is.EqualTo(1),
                "the notch is one cell and should carry exactly one bank");
        }

        static int Module(RenderTestWorld world, BankMesh.Kind kind) =>
            world.Model.BankModuleFor(NaturalContent.TerrainGrass, (int)kind);

        static int Count(ChunkBatch batch, int module)
        {
            int n = 0;
            foreach (InstanceBucket bucket in batch.Body)
                if (bucket.Module == module) n += bucket.Count;
            return n;
        }
    }
}
