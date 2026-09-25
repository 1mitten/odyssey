#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What the selection highlight is handed to draw (<c>docs/design/44-selection-highlight.md</c>).
    ///
    /// <para><b>The claim these hold is that the highlight is the thing as drawn</b>: a wall's panels
    /// and core at the chunk's own matrices, not a box somebody sized by hand. What it looks like is
    /// the owner's to judge; that it is the right shape in the right place is a test's. None of these
    /// needs the licensed art: the stand-in modules are meshed by the same emitters.</para>
    /// </summary>
    public class SelectionHighlightTests
    {
        [SetUp]
        public void SetUp() => GroundRelief.Reset();

        [TearDown]
        public void TearDown() => GroundRelief.Reset();

        static List<Matrix4x4> ChunkMatrices(List<InstanceBucket> buckets)
        {
            var all = new List<Matrix4x4>();
            foreach (InstanceBucket bucket in buckets)
                for (int i = 0; i < bucket.Count; i++) all.Add(bucket.Matrices[i]);
            return all;
        }

        static bool SameMatrix(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(a[i] - b[i]) > 1e-5f) return false;
            return true;
        }

        [Test]
        public void AWallIsCollectedAsExactlyThePanelsAndCoreTheChunkDraws()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(3, 3, 1, CoreContent.EdificeWall)
                .Publish();

            var batch = new ChunkBatch();
            new ChunkMesher(world.Model).Mesh(batch, world.Chunks.ChunksX * world.Chunks.ChunksZ * 1);
            List<Matrix4x4> drawn = ChunkMatrices(batch.Walls);
            Assert.That(drawn.Count, Is.EqualTo(5), "the chunk draws four panels and a core");

            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            var frame = new SelectionHighlight();
            int added = renderer.CollectCell(world.Index(3, 3, 1), frame, SelectionHighlight.Primary, terrain: false);

            Assert.That(added, Is.EqualTo(5), "one draw per instance the chunk submits");
            Assert.That(frame.Meshes.Count, Is.EqualTo(5));
            foreach (SelectionHighlight.MeshDraw draw in frame.Meshes)
            {
                Assert.That(drawn.Exists(m => SameMatrix(m, draw.Matrix)), Is.True,
                    $"a highlighted part at {draw.Matrix.GetColumn(3)} is not where the chunk draws one");
                Assert.That(draw.Strength, Is.EqualTo(SelectionHighlight.Primary));
                Assert.That(draw.Fill, Is.Zero, "a wall is outlined, not washed");
            }
        }

        [Test]
        public void OneWallOfARunIsCollectedAloneAndNotItsNeighbours()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeWall)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Publish();

            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            var frame = new SelectionHighlight();
            int added = renderer.CollectCell(world.Index(3, 3, 1), frame, SelectionHighlight.Primary, terrain: false);

            // The middle of a run shows its two long faces over its core.
            Assert.That(added, Is.EqualTo(3));
            Bounds cell = new Bounds(CellMetrics.Centre(3, 3, 1),
                new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ) * 1.2f);
            foreach (SelectionHighlight.MeshDraw draw in frame.Meshes)
                Assert.That(cell.Contains(draw.Matrix.GetColumn(3)), Is.True,
                    "a part of a neighbouring wall was highlighted with this one");
        }

        [Test]
        public void ABedIsCollectedFromItsHead()
        {
            var world = new RenderTestWorld(8, 8, 3).Bed(3, 3, 1, facing: 0).Publish();
            int head = world.Model.BedHeadAt(world.Index(3, 3, 1));
            Assert.That(head, Is.GreaterThanOrEqualTo(0));

            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            var frame = new SelectionHighlight();
            Assert.That(renderer.CollectCell(head, frame, SelectionHighlight.Primary, terrain: false),
                Is.GreaterThan(0), "a bed highlighted as nothing would fall back to the brackets");
        }

        [Test]
        public void AnEmptyCellCollectsNothingSoTheBracketsAreDrawnInstead()
        {
            var world = new RenderTestWorld(8, 8, 3).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            var frame = new SelectionHighlight();
            Assert.That(renderer.CollectCell(world.Index(3, 3, 1), frame, SelectionHighlight.Primary, terrain: true),
                Is.Zero);
            Assert.That(frame.IsEmpty, Is.True);
        }

        [Test]
        public void CollectingACellLeavesTheChunkMeshingAsItWas()
        {
            // The mesher is shared with the chunks; meshing one cell into a scratch batch must not
            // leave state that changes what the next chunk mesh produces.
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            var world = new RenderTestWorld(8, 8, 3);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0);
            world.Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();

            var mesher = new ChunkMesher(world.Model);
            int chunk = world.Chunks.ChunksX * world.Chunks.ChunksZ * 1;
            var before = new ChunkBatch();
            mesher.Mesh(before, chunk);

            mesher.MeshCell(new ChunkBatch(), world.Index(3, 3, 0), terrain: true);

            var after = new ChunkBatch();
            mesher.Mesh(after, chunk);
            List<Matrix4x4> a = ChunkMatrices(before.Walls), b = ChunkMatrices(after.Walls);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++) Assert.That(SameMatrix(a[i], b[i]), Is.True);
        }

        [Test]
        public void ATileIsOneWashedQuadOnTheCornersTheBracketStandsOn()
        {
            var world = new RenderTestWorld(8, 8, 3).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            var frame = new SelectionHighlight();

            Matrix4x4 place = Matrix4x4.Translate(CellMetrics.FloorCentre(3, 3, 1));
            float[] rises = { 0f, 0.5f, 1f, 1.5f };
            renderer.CollectSurface(place, frame, SelectionHighlight.Primary, rises);

            Assert.That(frame.Meshes.Count, Is.EqualTo(1));
            SelectionHighlight.MeshDraw quad = frame.Meshes[0];
            Assert.That(quad.Fill, Is.EqualTo(1f), "a tile is washed as well as outlined");
            Vector3[] corners = quad.Mesh.vertices;
            Assert.That(corners.Length, Is.EqualTo(4));
            for (int k = 0; k < 4; k++)
            {
                Vector3 expected = place.MultiplyPoint3x4(new Vector3(
                    ((k & 1) == 0 ? -1f : 1f) * CellMetrics.HalfXZ,
                    rises[k] + ChunkRenderer.SurfaceHighlightLift,
                    ((k & 2) == 0 ? -1f : 1f) * CellMetrics.HalfXZ));
                Assert.That(Vector3.Distance(corners[k], expected), Is.LessThan(1e-4f), $"corner {k}");
            }
        }

        [Test]
        public void TheListStartsEmptyGrowsAndClears()
        {
            var frame = new SelectionHighlight();
            Assert.That(frame.IsEmpty, Is.True, "nothing selected draws nothing");

            Mesh cube = PrimitiveMeshes.UnitCube;
            frame.AddMesh(cube, 0, Matrix4x4.Translate(new Vector3(10f, 0f, 0f)), 1f);
            frame.AddMesh(cube, 0, Matrix4x4.Translate(new Vector3(-10f, 0f, 0f)), 0.45f);
            Assert.That(frame.Count, Is.EqualTo(2));
            Assert.That(frame.Bounds.min.x, Is.LessThan(-10f));
            Assert.That(frame.Bounds.max.x, Is.GreaterThan(10f));

            frame.Lifted = false;
            frame.Clear();
            Assert.That(frame.IsEmpty, Is.True);
            Assert.That(frame.Count, Is.Zero);
            Assert.That(frame.Lifted, Is.True,
                "a group's 'no lift' must not outlive the group into the next single selection");
        }

        [Test]
        public void ASelectionBehindTheCameraIsGivenTheWholeScreen()
        {
            var go = new GameObject("highlight camera");
            try
            {
                Camera camera = go.AddComponent<Camera>();
                camera.transform.position = Vector3.zero;
                camera.transform.rotation = Quaternion.identity;
                var frame = new SelectionHighlight();

                frame.AddMesh(PrimitiveMeshes.UnitCube, 0, Matrix4x4.Translate(new Vector3(0f, 0f, 20f)), 1f);
                Rect ahead = frame.ViewportRect(camera, 0f);
                Assert.That(ahead.width, Is.LessThan(0.5f), "a small thing ahead is a small rectangle");
                Assert.That(ahead.Contains(new Vector2(0.5f, 0.5f)), Is.True);

                frame.AddMesh(PrimitiveMeshes.UnitCube, 0, Matrix4x4.Translate(new Vector3(0f, 0f, -20f)), 1f);
                Assert.That(frame.ViewportRect(camera, 0f), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                    "a part behind the camera cannot be projected, so the scissor is the whole screen");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AnOpaquePartIsNotClippedAndACutOutOneIs()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Assume.That(lit, Is.Not.Null);
            var opaque = new Material(lit);
            var cutout = new Material(lit);
            var texture = new Texture2D(2, 2);
            try
            {
                cutout.EnableKeyword("_ALPHATEST_ON");
                cutout.SetFloat("_AlphaClip", 1f);
                cutout.SetTexture("_BaseMap", texture);
                cutout.SetFloat("_Cutoff", 0.3f);

                Assert.That(SelectionHighlight.ClipOf(opaque).texture, Is.Null);
                (Texture? clipTexture, float clipCutoff) = SelectionHighlight.ClipOf(cutout);
                Assert.That(clipTexture, Is.SameAs(texture));
                Assert.That(clipCutoff, Is.EqualTo(0.3f).Within(1e-5f));
                Assert.That(SelectionHighlight.ClipOf(null).texture, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(opaque);
                Object.DestroyImmediate(cutout);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
