#nullable enable
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The butterflies as the Unity side draws them (design 52): where the habitat says they may
    /// live, that the shader and the director agree on the shape, and that the draw is two calls
    /// whatever the count. The model itself is the fast tier's (<c>ButterflyTests</c>).
    /// </summary>
    public class ButterflyDirectorTests
    {
        float _relief;

        [SetUp]
        public void Flat()
        {
            _relief = GroundRelief.Amplitude;
            GroundRelief.Amplitude = 0f;
        }

        [TearDown]
        public void Restore() => GroundRelief.Amplitude = _relief;

        /// <summary>
        /// A meadow on layer 1 over rock, and one column of every kind the habitat must refuse:
        /// a slab over the grass, a pond, bare rock, a wall. And a tree, which it must not.
        /// </summary>
        static RenderTestWorld Meadow()
        {
            var world = new RenderTestWorld(30, 30, 4);
            for (int z = 0; z < 30; z++)
            for (int x = 0; x < 30; x++)
            {
                world.Solid(x, z, 0);
                if (x == 6 && z == 6) continue;
                world.Solid(x, z, 1, x == 7 && z == 7 ? CoreContent.TerrainRock : NaturalContent.TerrainGrass);
            }
            world.Surface(6, 6, 1, NaturalContent.TerrainShallowWater);
            world.Slab(5, 5, 2);
            world.Edifice(8, 8, 2, CoreContent.EdificeWall);
            world.Edifice(9, 9, 2, NaturalContent.EdificeTreeBirch, blocking: false);
            return world.Publish();
        }

        /// <summary>Where the play camera stands for a focus at this distance: pitched 48 degrees.</summary>
        static Vector3 View(Vector3 focus, float distance) =>
            focus + new Vector3(0f, distance * Mathf.Sin(48f * Mathf.Deg2Rad), -distance * Mathf.Cos(48f * Mathf.Deg2Rad));

        static Vector2 Centre(int x, int z) =>
            new Vector2((x + 0.5f) * CellMetrics.SizeXZ, (z + 0.5f) * CellMetrics.SizeXZ);

        static bool HabitatAt(ButterflyHabitat habitat, int x, int z, out float ground)
        {
            Vector2 c = Centre(x, z);
            return habitat.Habitat(c.x, c.y, out ground);
        }

        [Test]
        public void TheHabitatIsOpenGrassAndNothingElse()
        {
            var habitat = new ButterflyHabitat(Meadow().Model);

            Assert.That(HabitatAt(habitat, 10, 10, out float ground), Is.True, "open grass");
            Assert.That(ground, Is.EqualTo(2f * CellMetrics.SizeY), "the top of the grass cell on layer 1");
            Assert.That(HabitatAt(habitat, 9, 9, out _), Is.True, "grass under a tree is meadow, as for the tufts");

            Assert.That(HabitatAt(habitat, 5, 5, out _), Is.False, "grass under a slab is indoors");
            Assert.That(HabitatAt(habitat, 6, 6, out _), Is.False, "a pond");
            Assert.That(HabitatAt(habitat, 7, 7, out _), Is.False, "bare rock");
            Assert.That(HabitatAt(habitat, 8, 8, out _), Is.False, "grass with a wall built on it");
            Assert.That(habitat.Habitat(-5f, 10f, out _), Is.False, "off the board");

            Vector2 pond = Centre(6, 6);
            Assert.That(habitat.Surface(pond.x, pond.y),
                Is.EqualTo(CellMetrics.SizeY + CellMetrics.SizeY * ChunkMesher.WaterSurface).Within(1e-4f),
                "a butterfly crossing the pond flies over the water's surface");
        }

        [Test]
        public void AColumnIsWalkedOnceAndAgainOnlyWhenItsChunkChanges()
        {
            RenderTestWorld world = Meadow();
            var habitat = new ButterflyHabitat(world.Model);
            habitat.SyncDirty();

            Assert.That(HabitatAt(habitat, 12, 12, out _), Is.True);
            int walks = habitat.Walks;
            for (int i = 0; i < 10; i++) HabitatAt(habitat, 12, 12, out _);
            Assert.That(habitat.Walks, Is.EqualTo(walks), "a column already known was walked again");
            Assert.That(habitat.SyncDirty(), Is.EqualTo(0), "a still world reported a changed chunk");

            // Pave it over: the grass cell becomes rock, as a quarry face would leave it.
            int index = world.Index(12, 12, 1);
            world.Grid.Terrain[index] = CoreContent.TerrainRock;
            world.Chunks.MarkDirty(12, 12, 1);
            world.PublishEdits();

            Assert.That(habitat.SyncDirty(), Is.GreaterThan(0));
            Assert.That(HabitatAt(habitat, 12, 12, out _), Is.False, "the habitat still thinks rock is grass");
        }

        /// <summary>The shader's corners per butterfly and the director's are one number (P1).</summary>
        [Test]
        public void TheShaderAndTheDirectorAgreeOnTheShape()
        {
            string source = File.ReadAllText("Assets/Odyssey/Presentation/Shaders/OdysseyButterfly.shader");
            Match define = Regex.Match(source, @"#define\s+BUTTERFLY_VERTS\s+(\d+)");
            Assert.That(define.Success, Is.True, "the shader no longer states BUTTERFLY_VERTS");
            Assert.That(int.Parse(define.Groups[1].Value), Is.EqualTo(ButterflyDirector.VertsPerButterfly));

            // Four wings of six fan triangles, and a body of eight faces.
            Assert.That(ButterflyDirector.VertsPerButterfly, Is.EqualTo((4 * 6 + 8) * 3));
            Assert.That(source, Does.Not.Contain("SampleSceneDepth"),
                "the night reads the depth texture again: the halo-and-pool pass is back (design 52 §5a)");
            Assert.That(Regex.Match(source, @"float4 _ButterflyGlow\[(\d+)\]").Groups[1].Value,
                Is.EqualTo(ButterflyPalette.Glows.Length.ToString()), "the glow array is sized for another palette");
            Assert.That(Regex.Match(source, @"float4 _ButterflyShape\[(\d+)\]").Groups[1].Value,
                Is.EqualTo(ButterflyPalette.All.Length.ToString()), "the species array is sized for another table");
        }

        /// <summary>
        /// One call by day and by night, whatever the count — draws in butterflies, never per
        /// butterfly (P10) — and nothing at all on the Off rung, looking down a mine, or zoomed out
        /// past every wing by day. The night's second pass is gone (design 52 §5a).
        /// </summary>
        [Test]
        public void TheDrawIsOneCallDayOrNightWhateverTheCount()
        {
            RenderTestWorld world = Meadow();
            var focus = new Vector3(15f * CellMetrics.SizeXZ, 2f * CellMetrics.SizeY, 15f * CellMetrics.SizeXZ);
            long spring = Odyssey.Sim.Contracts.Calendar.TicksPerDay * 6;

            foreach (int rung in new[] { 80, 500 })
            {
                var director = new ButterflyDirector(world.Model, 11) { Capacity = rung };
                try
                {
                    if (!director.Available) Assert.Ignore("the Odyssey/Butterfly shader did not compile here");
                    director.Sync(1f / 60f, spring, 12f, 0f, 0f, null, focus, 48f, View(focus, 48f), 0, 3, false);
                    Assert.That(director.LastDrawn, Is.GreaterThan(0), $"nothing drawn at rung {rung}");
                    Assert.That(director.LastDrawCalls, Is.EqualTo(1), $"noon at rung {rung}");

                    director.Sync(1f / 60f, spring, 0f, 0f, 0f, null, focus, 48f, View(focus, 48f), 0, 3, false);
                    Assert.That(director.LastNight, Is.EqualTo(1f));
                    Assert.That(director.LastDrawCalls, Is.EqualTo(1), $"midnight at rung {rung}");

                    // Past every wing's reach by day nothing is packed or submitted; at night the lit
                    // wings go on to the full zoom.
                    director.Sync(1f / 60f, spring, 12f, 0f, 0f, null, focus, 160f, View(focus, 160f), 0, 3, false);
                    Assert.That(director.LastDrawCalls, Is.EqualTo(0), "a 160 m day view submitted wings nobody can see");
                    director.Sync(1f / 60f, spring, 0f, 0f, 0f, null, focus, 160f, View(focus, 160f), 0, 3, false);
                    Assert.That(director.LastDrawCalls, Is.EqualTo(1), "a 160 m night view drew no lit wings");

                    director.Sync(1f / 60f, spring, 0f, 0f, 0f, null, focus, 48f, View(focus, 48f), 0, 3, underground: true);
                    Assert.That(director.LastDrawCalls, Is.EqualTo(0), "looking down a mine drew the meadow");

                    director.Sync(1f / 60f, spring, 0f, 0f, 0f, null, focus, 48f, View(focus, 48f), 3, 3, false);
                    Assert.That(director.LastDrawn, Is.EqualTo(0), "a slice above the meadow drew its butterflies");

                    director.Capacity = 0;
                    director.Sync(1f / 60f, spring, 0f, 0f, 0f, null, focus, 48f, View(focus, 48f), 0, 3, false);
                    Assert.That(director.LastDrawCalls, Is.EqualTo(0), "the Off rung drew something");
                    Assert.That(director.Meadow.Live, Is.EqualTo(0), "the Off rung kept butterflies alive");
                }
                finally
                {
                    director.Dispose();
                }
            }
        }

        /// <summary>The glow rises with the night grade: none at the noon key, all of it at the night key.</summary>
        [Test]
        public void TheGlowFollowsTheDaylightCurve()
        {
            Assert.That(ButterflyPalette.NightFor(Daylight.Sample(13f).SunElevation), Is.EqualTo(0f), "noon");
            Assert.That(ButterflyPalette.NightFor(Daylight.Sample(0f).SunElevation), Is.EqualTo(1f), "midnight");
            Assert.That(ButterflyPalette.NightFor(Daylight.Sample(9f).SunElevation), Is.EqualTo(0f), "morning");

            // Somewhere between sunset and full night it is partway, and it only rises.
            float previous = 0f;
            bool partway = false;
            for (float hour = 18f; hour <= 23.5f; hour += 0.1f)
            {
                float night = ButterflyPalette.NightFor(Daylight.Sample(hour).SunElevation);
                Assert.That(night, Is.GreaterThanOrEqualTo(previous - 1e-4f), $"the glow fell at {hour:0.0} h");
                if (night > 0.05f && night < 0.95f) partway = true;
                previous = night;
            }
            Assert.That(partway, Is.True, "the glow switched on at a clock hour rather than rising with the dusk");
        }

        [Test]
        public void APausedFrameMovesNobody()
        {
            RenderTestWorld world = Meadow();
            var focus = new Vector3(15f * CellMetrics.SizeXZ, 2f * CellMetrics.SizeY, 15f * CellMetrics.SizeXZ);
            var director = new ButterflyDirector(world.Model, 3) { Capacity = 200 };
            try
            {
                for (int f = 0; f < 60; f++)
                    director.Sync(1f / 60f, 0, 12f, 0f, 0f, null, focus, 48f, View(focus, 48f), 0, 3, false);
                var before = new float[director.Meadow.Capacity];
                for (int i = 0; i < before.Length; i++) before[i] = director.Meadow.XAt(i);
                for (int f = 0; f < 30; f++)
                    director.Sync(0f, 0, 12f, 0f, 0f, null, focus, 48f, View(focus, 48f), 0, 3, false);
                for (int i = 0; i < before.Length; i++)
                    Assert.That(director.Meadow.XAt(i), Is.EqualTo(before[i]), $"butterfly {i} moved on a pause");
            }
            finally
            {
                director.Dispose();
            }
        }
    }
}
