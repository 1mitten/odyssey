#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
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
                // Taller since the owner asked for grass more like Breath of the Wild's, and
                // the ceiling is a real limit rather than a round number: items, orders and
                // zones are all read off the ground, so grass that reached a colonist's waist
                // would be hiding the game. The arc keeps the tip well under the blade's own
                // length, so the longest blade stands about 0.78 m.
                Assert.That(highest, Is.InRange(0.3f, 1.25f),
                    $"variant {variant} is {highest:0.00} m tall; mid-thigh is the ceiling the "
                        + "owner chose and anything past it is hiding the board");
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
        /// <b>The bounds cover the bow the shader can actually apply</b>, which is a number that
        /// lives in HLSL and a number that lives in C#, and nothing but this connects them.
        ///
        /// <para><see cref="TheBoundsCoverTheSway"/> above checks the mesh against
        /// <c>GrassMesh.MaxSway</c>; this checks <c>MaxSway</c> against the shader. Both halves are
        /// needed, because the failure is not a crash or a wrong pixel: it is clumps at the edge
        /// of the view blinking out when the wind gets up, which reads as a culling bug and sends
        /// the next person to the wrong file.</para>
        ///
        /// <para>The arithmetic is the chord of the arc: a vertex an arm's length from the root,
        /// rotated by the cap, moves <c>2 * arm * sin(cap / 2)</c>. The arm is measured off the
        /// built mesh rather than assumed, so making blades longer fails this too — which is the
        /// half somebody would otherwise miss, since lengthening a blade does not look like it has
        /// anything to do with a shader constant.</para>
        ///
        /// <para>Reading the shader source is the same move <c>HudFontTests</c> makes when it
        /// parses the shipped fonts: the fast tier has no HLSL compiler and the Unity tier asserts
        /// no pixels, so a disagreement between the two languages is otherwise silent.</para>
        /// </summary>
        [Test]
        public void TheBoundsCoverTheBowTheShaderCanApply()
        {
            string source = Path.Combine(
                Application.dataPath, "Odyssey/Presentation/Shaders/OdysseyGrass.shader");
            Assume.That(File.Exists(source), Is.True, $"no grass shader at {source}");

            Match match = Regex.Match(
                File.ReadAllText(source),
                @"#define\s+ODYSSEY_GRASS_MAX_BOW\s+([0-9.]+)");
            Assert.That(match.Success, Is.True,
                "the shader no longer declares ODYSSEY_GRASS_MAX_BOW; if the cap moved or was "
                    + "renamed, this test and GrassMesh.MaxSway both need to hear about it");

            float cap = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

            float arm = 0f;
            for (int variant = 0; variant < GrassMesh.Variants; variant++)
                foreach (Vector3 v in Vertices(variant))
                    arm = Mathf.Max(arm, v.magnitude);

            float travel = 2f * arm * Mathf.Sin(cap * 0.5f);
            Assert.That(GrassMesh.MaxSway, Is.GreaterThanOrEqualTo(travel - 1e-3f),
                $"the shader can bow a vertex {arm:0.00} m from its root by {cap:0.00} rad, which "
                    + $"moves it {travel:0.00} m, but the mesh bounds only allow for "
                    + $"{GrassMesh.MaxSway:0.00} m");
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

            // No longer an exact count. How thick the grass is now depends on the land as well
            // as on the setting (GroundScatter.Lushness), and on an all-grass board that lands
            // at the middle of the range rather than at one — so the numbers to assert are the
            // *relationship* and the draw count, which is what this test was ever about.
            Assert.That(thin.Instances, Is.GreaterThan(0), "no grass at all at a hundred per hundred");
            Assert.That(thick.Instances, Is.GreaterThan(thin.Instances * 17 / 10),
                $"doubling the setting took the meadow {thin.Instances} -> {thick.Instances}, "
                    + "which is not a doubling");
            Assert.That(thick.Buckets, Is.EqualTo(thin.Buckets),
                $"the meadow doubled and the draws went {thin.Buckets} -> {thick.Buckets}; "
                    + "grass must cost instances, not draws");
            Assert.That(thick.Buckets, Is.LessThanOrEqualTo(GrassMesh.Variants),
                "a meadow is one bucket a clump variant and nothing like a draw a tuft");
        }

        /// <summary>
        /// <b>How dense the meadow is has one owner, and the board and the surround both read it.</b>
        ///
        /// <para>It had five. <c>ChunkMesher</c>'s own default, <c>TerrainSkirt.TuftDensity</c>,
        /// <c>OdysseyBootstrap.grassScatter</c>, <c>SettingsPresenter</c>'s fallback and two
        /// frame-time harnesses each wrote the literal 60, and every one of them meant "the
        /// density the game ships with". Raising it on the owner's say-so (2026-09-22) would have
        /// moved the meadow and left the benchmarks timing the old one — a performance number that
        /// is quietly about a world nobody plays.</para>
        ///
        /// <para>The two in this assembly are checked here. The three outside it now reference the
        /// same constant by name, which a reader can see; this is the pair that could drift
        /// without anybody noticing, because the rim of the board is exactly where a difference in
        /// density stops being invisible and starts being a straight line across the view.</para>
        /// </summary>
        [Test]
        public void TheMeadowAndTheSurroundAgreeOnHowThickTheGrassIs()
        {
            var world = new RenderTestWorld(8, 8, 3);
            using var renderer = new ChunkRenderer(world.Model);

            Assert.That(renderer.ScatterDensity, Is.EqualTo(ChunkMesher.DefaultScatterDensity),
                "the board is not strewn at the shipped density");
            Assert.That(renderer.Skirt.TuftDensity, Is.EqualTo(ChunkMesher.DefaultScatterDensity),
                "the surround is strewn at a different density from the board it continues");
        }

        /// <summary>Bare ground is still an option, and it draws nothing at all.</summary>
        [Test]
        public void ZeroDensityIsBareGround() =>
            Assert.That(Meadow(0).Instances, Is.Zero);

        // ------------------------------------------------------------- the land

        /// <summary>
        /// Lushness follows the land: deep by the water, thin against the rock.
        ///
        /// <para>The owner chose this over noise (<c>grass-interview.md</c>, answer 11) because it
        /// says something true about the map. The rule is asserted here rather than being left to
        /// the eye, because "the grass is a bit thin over there" is not a bug report anybody can
        /// act on.</para>
        /// </summary>
        [Test]
        public void GrassIsDeepByWaterAndThinByRock()
        {
            float dry = GroundScatter.Lushness(0, 0);
            float wet = GroundScatter.Lushness(4, 0);
            float stony = GroundScatter.Lushness(0, 4);

            Assert.That(wet, Is.GreaterThan(dry), "water should not thin the grass beside it");
            Assert.That(stony, Is.LessThan(dry), "rock should not fatten the grass beside it");
            Assert.That(wet, Is.EqualTo(1f).Within(1e-3f), "four wet sides should be as lush as it goes");
            Assert.That(stony, Is.EqualTo(0f).Within(1e-3f), "four stony sides should be as thin as it goes");

            Assert.That(GroundScatter.Lushness(9, 0), Is.LessThanOrEqualTo(1f), "unclamped");
            Assert.That(GroundScatter.Lushness(0, 9), Is.GreaterThanOrEqualTo(0f), "unclamped");

            Assert.That(GroundScatter.DensityScale(1f), Is.GreaterThan(GroundScatter.DensityScale(0f)));
            Assert.That(GroundScatter.ClumpScale(1f), Is.GreaterThan(GroundScatter.ClumpScale(0f)));
        }

        /// <summary>
        /// <b>A wall clears the grass around it</b>, and the cell it clears is decided by the layer
        /// the wall stands in rather than the one the grass grows on.
        ///
        /// <para>That distinction is the whole test. Grass is strewn on the top of a solid cell and
        /// a wall stands in the open cell <em>above</em> it, so a margin rule that asked the grass's
        /// own cell whether it was blocked would be asking whether the ground is ground — always
        /// false, clearing nothing, and looking on screen exactly like a feature that was never
        /// written. The first draft did that.</para>
        /// </summary>
        [Test]
        public void AWallClearsTheGrassAroundIt()
        {
            int Clumps(bool withWall)
            {
                var world = new RenderTestWorld(8, 8, 3);
                for (int z = 0; z < 8; z++)
                for (int x = 0; x < 8; x++)
                    world.Solid(x, z, 1, NaturalContent.TerrainGrass);

                if (withWall) world.Edifice(4, 4, 2, CoreContent.EdificeWall);
                world.Publish();

                var clumps = new HashSet<int>(GrassMesh.Modules(world.Library));
                var batch = new ChunkBatch();
                var mesher = new ChunkMesher(world.Model) { ScatterDensity = 300 };
                mesher.Mesh(batch, world.Chunks.ChunksX * world.Chunks.ChunksZ);

                int instances = 0;
                foreach (InstanceBucket bucket in batch.Body)
                    if (clumps.Contains(bucket.Module))
                        instances += bucket.Count;
                return instances;
            }

            int bare = Clumps(withWall: false);
            int walled = Clumps(withWall: true);

            Assume.That(bare, Is.GreaterThan(0), "no grass to clear");
            Assert.That(walled, Is.LessThan(bare),
                "a wall cleared no grass at all, which is what asking the wrong layer looks like");
        }

        // ------------------------------------------------------------- the clearance field

        /// <summary>
        /// A dropped item pushes the grass back, and only where it is.
        ///
        /// <para>This is the mechanism the interview turned on (answer 13): items and order marks
        /// are not in the render mirror the mesher reads, so the only way grass can get out of
        /// their way is a field stamped per frame and read by the shader. Built on the CPU
        /// precisely so that this test can exist.</para>
        /// </summary>
        [Test]
        public void AnItemPushesTheGrassBackAndOnlyWhereItIs()
        {
            using var clearance = new GrassClearance();
            var at = new Vector3(100f, 0f, 100f);

            clearance.Begin(at);
            clearance.Stamp(at, 0.55f);

            Assert.That(clearance.At(at), Is.GreaterThan(0.9f), "the item's own ground is not cleared");
            Assert.That(clearance.At(at + new Vector3(3f, 0f, 0f)), Is.Zero,
                "three metres away is not near anything and must keep its grass");
            Assert.That(clearance.Stamps, Is.EqualTo(1));
        }

        /// <summary>
        /// Two items beside each other clear their own ground and do not gouge a deeper hole where
        /// their rings overlap. The field keeps the strongest stamp rather than summing them.
        /// </summary>
        [Test]
        public void OverlappingRingsDoNotCompound()
        {
            using var clearance = new GrassClearance();
            var centre = new Vector3(100f, 0f, 100f);

            clearance.Begin(centre);
            clearance.Stamp(centre, 1.2f);
            float alone = clearance.At(centre);

            clearance.Stamp(centre + new Vector3(0.4f, 0f, 0f), 1.2f);
            Assert.That(clearance.At(centre), Is.EqualTo(alone).Within(1e-3f));
        }

        /// <summary>
        /// The window snaps to a whole texel as it follows the camera.
        ///
        /// <para>Without it the field slides continuously under the world, every clump's sample
        /// point drifts across texel boundaries, and the grass at the edge of a ring flickers as
        /// you pan — which reads as the grass being broken rather than as the window moving.</para>
        /// </summary>
        [Test]
        public void TheWindowSnapsToATexelAsItFollowsTheCamera()
        {
            using var clearance = new GrassClearance();

            clearance.Begin(new Vector3(100f, 0f, 100f));
            Vector2 first = clearance.Origin;

            clearance.Begin(new Vector3(100f + GrassClearance.MetresPerTexel * 0.25f, 0f, 100f));
            Assert.That(clearance.Origin, Is.EqualTo(first), "the window crawled within one texel");

            clearance.Begin(new Vector3(100f + GrassClearance.MetresPerTexel * 4f, 0f, 100f));
            Assert.That(clearance.Origin, Is.Not.EqualTo(first), "the window did not follow the camera");
        }

        /// <summary>Anything outside the window keeps its grass, rather than being smeared by the
        /// edge texels of a field that does not reach it.</summary>
        [Test]
        public void GroundOutsideTheWindowIsNeverCleared()
        {
            using var clearance = new GrassClearance();
            clearance.Begin(new Vector3(100f, 0f, 100f));
            clearance.Stamp(new Vector3(100f, 0f, 100f), 2f);

            Assert.That(clearance.At(new Vector3(1000f, 0f, 1000f)), Is.Zero);
            Assert.That(clearance.Stamps, Is.EqualTo(1));

            clearance.Stamp(new Vector3(1000f, 0f, 1000f), 2f);
            Assert.That(clearance.Stamps, Is.EqualTo(1), "a stamp outside the window was counted");
        }

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
