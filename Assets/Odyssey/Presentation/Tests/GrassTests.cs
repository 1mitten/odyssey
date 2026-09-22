#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The grass: its geometry, the channel the shader reads, and the promise that a meadow costs
    /// instances rather than draws.
    ///
    /// <para><b>None of this could be tested before.</b> The meadow was three Synty foliage
    /// prefabs and <c>ChunkMesher.EnsureScatterModules</c> dropped any that did not resolve to
    /// real art, so on a machine without <c>Assets/Synty</c> — which is every clone, and the
    /// build runner — there was no grass at all and nothing to assert about. The clump being ours
    /// is what makes this file possible, and <see cref="TheMeadowDrawsWithoutTheArtPacks"/> is the
    /// test that says so.</para>
    ///
    /// <para>The fast tier compiles neither Presentation nor Editor, so nothing here runs until
    /// Unity does.</para>
    /// </summary>
    public class GrassTests
    {
        [SetUp]
        public void Setup() => GrassMesh.Forget();

        static Vector3[] Vertices(int variant) => GrassMesh.For(variant).vertices;

        // ------------------------------------------------------------- geometry

        /// <summary>
        /// A clump stands on the ground it is placed on and is ankle high.
        ///
        /// <para>Both halves matter and the first is the one that bites. The mesher places a
        /// clump by <c>Matrix4x4.TRS(surface, yaw, scale)</c> with no local offset, because
        /// <c>ModuleShape.GrassClump</c>'s fallback box is deliberately the untouched unit box —
        /// so a mesh whose roots were not at y = 0 would float or sink by however far they were
        /// out, uniformly, across the whole board. That is the sort of fault that looks like a
        /// terrain bug.</para>
        /// </summary>
        [Test]
        public void AClumpStandsOnItsRootsAndIsAnkleHigh()
        {
            for (int variant = 0; variant < GrassMesh.Variants; variant++)
            {
                Vector3[] vertices = Vertices(variant);
                float lowest = float.MaxValue, highest = float.MinValue, widest = 0f;
                foreach (Vector3 v in vertices)
                {
                    lowest = Mathf.Min(lowest, v.y);
                    highest = Mathf.Max(highest, v.y);
                    widest = Mathf.Max(widest, new Vector2(v.x, v.z).magnitude);
                }

                Assert.That(lowest, Is.EqualTo(0f).Within(1e-4f),
                    $"variant {variant} does not stand on its own roots");
                Assert.That(highest, Is.InRange(0.3f, 0.8f),
                    $"variant {variant} is {highest:0.00} m tall; grass is ankle high");
                Assert.That(widest, Is.LessThanOrEqualTo(GrassMesh.Reach + 1e-3f),
                    $"variant {variant} reaches {widest:0.00} m, past its declared {GrassMesh.Reach} m — "
                        + "GroundScatter.ClumpReach is what keeps clumps off a tilled tile, and it "
                        + "is set from this number");
            }
        }

        /// <summary>
        /// Vertex colour red is the height along the blade, and the shader believes it.
        ///
        /// <para>It drives three separate things — the root-to-tip colour ramp, how far the wind
        /// bends a vertex, and the lean towards the camera — so a mesh that filled it wrongly
        /// would give grass the right shape, the wrong colour and a bend from the middle, and
        /// only the last of those is obvious in a screenshot.</para>
        /// </summary>
        [Test]
        public void RedIsTheHeightAlongTheBlade()
        {
            for (int variant = 0; variant < GrassMesh.Variants; variant++)
            {
                Mesh mesh = GrassMesh.For(variant);
                Vector3[] vertices = mesh.vertices;
                Color[] colours = mesh.colors;

                Assert.That(colours, Has.Length.EqualTo(vertices.Length),
                    $"variant {variant} has no colour channel; the shader reads one");

                float highest = 0f;
                for (int i = 0; i < vertices.Length; i++) highest = Mathf.Max(highest, vertices[i].y);

                bool anyTip = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Assert.That(colours[i].r, Is.InRange(0f, 1f));

                    if (Mathf.Approximately(colours[i].r, 0f))
                        Assert.That(vertices[i].y, Is.EqualTo(0f).Within(1e-4f),
                            "a vertex the shader calls the root is not on the ground");

                    if (colours[i].r >= 1f)
                    {
                        anyTip = true;
                        Assert.That(vertices[i].y, Is.GreaterThan(0.2f),
                            "a vertex the shader calls the tip is not up in the air");
                    }
                }

                Assert.That(anyTip, Is.True, $"variant {variant} has no tip");
            }
        }

        /// <summary>
        /// The bounds are grown to cover the sway, because nothing that culls knows about it.
        ///
        /// <para>The wind and the camera lean both happen in the vertex shader, and a bucket is
        /// culled as one bounding box. Without the margin a clump at the edge of the view pops out
        /// whole — not a sliver of it — at the moment it bends, which is the kind of fault that is
        /// blamed on the culling rather than on the mesh.</para>
        /// </summary>
        [Test]
        public void TheBoundsCoverTheSway()
        {
            for (int variant = 0; variant < GrassMesh.Variants; variant++)
            {
                Mesh mesh = GrassMesh.For(variant);
                Vector3[] vertices = mesh.vertices;

                var raw = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 v in vertices) raw.Encapsulate(v);

                Assert.That(mesh.bounds.max.x - raw.max.x, Is.GreaterThanOrEqualTo(GrassMesh.MaxSway - 1e-3f),
                    "the bounds do not allow for the wind the shader applies");
                Assert.That(raw.min.z - mesh.bounds.min.z, Is.GreaterThanOrEqualTo(GrassMesh.MaxSway - 1e-3f),
                    "the bounds do not allow for the wind the shader applies");
            }
        }

        /// <summary>
        /// The same variant is the same clump every time, and every variant is a different one.
        ///
        /// <para>The first is the promise <c>GroundScatter</c> makes about placement, one level
        /// down: a chunk is re-meshed whenever anything in it changes, so a clump that came out
        /// differently on a rebuild would have the meadow reshuffling itself whenever a colonist
        /// felled a tree twenty metres away. The second is why there are three of them at all.</para>
        /// </summary>
        [Test]
        public void EveryVariantIsItsOwnClumpAndIsStable()
        {
            var first = new Vector3[GrassMesh.Variants][];
            for (int variant = 0; variant < GrassMesh.Variants; variant++) first[variant] = Vertices(variant);

            GrassMesh.Forget();

            for (int variant = 0; variant < GrassMesh.Variants; variant++)
            {
                Vector3[] again = Vertices(variant);
                Assert.That(again, Has.Length.EqualTo(first[variant].Length),
                    $"variant {variant} rebuilt to a different size");
                for (int i = 0; i < again.Length; i++)
                    Assert.That(again[i], Is.EqualTo(first[variant][i]),
                        $"variant {variant} rebuilt to a different shape at vertex {i}");
            }

            for (int a = 0; a < GrassMesh.Variants; a++)
            for (int b = a + 1; b < GrassMesh.Variants; b++)
                Assert.That(Same(first[a], first[b]), Is.False,
                    $"variants {a} and {b} are the same clump; the scatter is choosing between copies");
        }

        static bool Same(Vector3[] a, Vector3[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>The id table and the mesh table have to agree, and nothing else makes them.</summary>
        [Test]
        public void ThereIsAnIdForEveryClump() =>
            Assert.That(ModuleIds.GrassClumpCount, Is.EqualTo(GrassMesh.Variants),
                "ModuleIds.GrassClumps and GrassMesh.Variants have drifted apart");

        // ------------------------------------------------------------- the library

        /// <summary>
        /// <b>The meadow draws on a machine with no licensed art.</b>
        ///
        /// <para>This is the whole point of the change and it is worth stating as a test rather
        /// than as a comment. Before it, the runner — which has no <c>Assets/Synty</c> — rendered
        /// a board of bare ground, and the largest visible thing in the game was the one thing no
        /// test could see. <see cref="RenderTestWorld"/> builds its library with no catalogue at
        /// all, which is exactly that machine.</para>
        ///
        /// <para>Note what is asked: whether the module <em>resolved to the clump</em>, never
        /// whether a catalogue exists. Asking the second question is how this project has turned
        /// the runner red twice.</para>
        /// </summary>
        [Test]
        public void TheMeadowDrawsWithoutTheArtPacks()
        {
            var world = new RenderTestWorld(4, 4, 3);
            int[] modules = GrassMesh.Modules(world.Library);

            Assert.That(modules, Has.Length.EqualTo(GrassMesh.Variants));
            for (int variant = 0; variant < modules.Length; variant++)
            {
                ResolvedModule resolved = world.Library[modules[variant]];
                Assert.That(modules[variant], Is.Not.Zero, $"clump {variant} resolved to nothing");
                Assert.That(resolved.IsEmpty, Is.False, $"clump {variant} has no parts");
                Assert.That(resolved.Shape, Is.EqualTo(ModuleShape.GrassClump));
                Assert.That(resolved.UsesArt, Is.True,
                    "a clump did not fall back to a primitive, so it must not report that it did");
                Assert.That(resolved.Parts[0].IsFallback, Is.False,
                    "a clump labelled a fallback is given a solid stand-in colour instead of a tint");
                Assert.That(resolved.Parts[0].Mesh, Is.SameAs(GrassMesh.For(variant)),
                    $"clump {variant} is drawing something other than its own mesh");
                Assert.That(resolved.Parts[0].Local, Is.EqualTo(Matrix4x4.identity),
                    "a clump is authored in metres and must not be scaled into a cell");
            }
        }

        /// <summary>Asking twice gives the same modules and builds nothing a second time.</summary>
        [Test]
        public void TheClumpsResolveOnce()
        {
            var world = new RenderTestWorld(4, 4, 3);
            int[] first = GrassMesh.Modules(world.Library);
            int[] again = GrassMesh.Modules(world.Library);
            Assert.That(again, Is.EqualTo(first));
        }

        // ------------------------------------------------------------- the meadow

        static (int Buckets, int Instances) Meadow(int density)
        {
            var world = new RenderTestWorld(8, 8, 3);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            world.Publish();

            var clumps = new HashSet<int>(GrassMesh.Modules(world.Library));

            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model) { ScatterDensity = density };
            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            mesher.Mesh(batch, 1 * chunksPerLayer);

            int buckets = 0, instances = 0;
            foreach (InstanceBucket bucket in batch.Body)
            {
                if (!clumps.Contains(bucket.Module)) continue;
                buckets++;
                instances += bucket.Count;
            }
            return (buckets, instances);
        }

        /// <summary>
        /// <b>A thicker meadow adds instances, not draws.</b>
        ///
        /// <para>The invariant the whole renderer rests on, stated where it can fail. A clump is
        /// one more instance of one more module in the chunk it stands in, so grass inherits
        /// chunk culling, the slice, the depth shade and a single instanced submission per bucket
        /// — and doubling the density must not cost a single extra call. This is the guard that
        /// fails the day somebody draws grass some other way; the same shape of fault has been
        /// found four times in this renderer and cost 3.67 ms of a 5 ms budget once
        /// (<c>docs/bug-patterns.md</c> P10).</para>
        /// </summary>
        [Test]
        public void AThickerMeadowAddsInstancesRatherThanDraws()
        {
            (int Buckets, int Instances) thin = Meadow(100);
            (int Buckets, int Instances) thick = Meadow(200);

            Assert.That(thin.Instances, Is.EqualTo(64), "one clump a cell at a hundred per hundred");
            Assert.That(thick.Instances, Is.EqualTo(128), "two clumps a cell at two hundred");
            Assert.That(thick.Buckets, Is.EqualTo(thin.Buckets),
                $"the meadow doubled and the draws went {thin.Buckets} -> {thick.Buckets}; "
                    + "grass must cost instances, not draws");
            Assert.That(thick.Buckets, Is.LessThanOrEqualTo(GrassMesh.Variants),
                "a meadow is one bucket a clump variant and nothing like a draw a tuft");
        }

        /// <summary>Bare ground is still an option, and it draws nothing at all.</summary>
        [Test]
        public void ZeroDensityIsBareGround() =>
            Assert.That(Meadow(0).Instances, Is.Zero);

        // ------------------------------------------------------------- the wind

        /// <summary>
        /// The wind is a pure function of the tick — so a paused meadow holds still, a loaded
        /// game blows the way it did when it was saved, and two machines at the same tick take
        /// the same photograph.
        /// </summary>
        [Test]
        public void TheWindIsAFunctionOfTheTickAlone()
        {
            var wind = new WindDirector();

            wind.Apply(0);
            Assert.That(wind.Phase, Is.EqualTo(0f).Within(1e-5f), "tick zero is the start of the gust");

            wind.Apply(5_000);
            float phase = wind.Phase;
            Vector3 push = wind.Push;

            wind.Apply(123);
            wind.Apply(5_000);

            Assert.That(wind.Phase, Is.EqualTo(phase).Within(1e-4f));
            Assert.That(wind.Push, Is.EqualTo(push));
        }

        /// <summary>The same tick twice is still air standing still, and time moves it on.</summary>
        [Test]
        public void TheGustAdvancesWithTheClock()
        {
            var wind = new WindDirector();
            wind.Apply(0);
            float still = wind.Phase;
            wind.Apply((long)(wind.TicksPerGust * 0.25f));
            Assert.That(wind.Phase, Is.GreaterThan(still));
        }

        /// <summary>
        /// A global outlives the object that set it. Leaving a gale behind would blow the grass in
        /// the next picture this editor took — a portrait, a contact sheet, an asset preview —
        /// with nothing in the scene to explain it.
        /// </summary>
        [Test]
        public void ClosingDownLeavesStillAir()
        {
            var wind = new WindDirector();
            wind.Apply(9_000);
            Assume.That(wind.Push, Is.Not.EqualTo(Vector3.zero));

            wind.Dispose();

            Assert.That(wind.Push, Is.EqualTo(Vector3.zero));
            Assert.That(wind.Phase, Is.Zero);
            Assert.That(Shader.GetGlobalVector("_OdysseyWind"), Is.EqualTo(Vector4.zero));
        }
    }
}
