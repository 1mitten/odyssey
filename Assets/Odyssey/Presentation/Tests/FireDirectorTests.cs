#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The campfire's flame, smoke and light (docs/design/31-campfire-art-and-fire.md §8).
    ///
    /// <para>Each of these covers a thing that would otherwise be found by looking at the game on
    /// the one night somebody happened to scroll a layer down.</para>
    /// </summary>
    public class FireDirectorTests
    {
        static readonly GridSize Size = new GridSize(8, 8, 3);

        static (FireDirector fires, ModuleLibrary library, GameObject host) Build(params CellRef[] at)
        {
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            var library = new ModuleLibrary(catalogue);
            var model = new WorldRenderModel(Size, new ChunkGrid(Size), library);

            var grid = new CellGrid(Size);
            var edifices = new List<PlacedEdifice>();
            for (int i = 0; i < at.Length; i++)
            {
                int cell = Size.Index(at[i]);
                edifices.Add(new PlacedEdifice
                {
                    CellIndex = cell, Def = CoreContent.EdificeCampfire, Stuff = 0,
                });
                // The grid holds a HANDLE into the list, not the def — RefreshAll reads the cell
                // and looks the record up. Without this the list is a set of records nothing on
                // the board points at, and the model quite correctly reports an empty meadow.
                grid.Edifice[cell] = i;
            }
            model.RefreshAll(grid, edifices);

            var host = new GameObject("FireDirectorTests");
            return (new FireDirector(model, host.transform, host.layer), library, host);
        }

        static void Clean(FireDirector fires, ModuleLibrary library, GameObject host)
        {
            fires.Dispose();
            library.Dispose();
            Object.DestroyImmediate(host);
        }

        static int LiveLights(GameObject host)
        {
            int live = 0;
            foreach (Light light in host.GetComponentsInChildren<Light>(includeInactive: true))
                if (light.enabled) live++;
            return live;
        }

        /// <summary>One light per campfire the player can see, and none for a board with none.</summary>
        [Test]
        public void EveryDrawnCampfireGetsOneLight()
        {
            var (fires, library, host) = Build(new CellRef(2, 2, 0), new CellRef(5, 5, 0));
            try
            {
                if (!fires.Warmed) Assert.Ignore("no particle shader in this build");

                fires.Sync(0, new SliceSettings(), 1f / 60f);

                Assert.That(fires.LitFires, Is.EqualTo(2));
                Assert.That(LiveLights(host), Is.EqualTo(2));
            }
            finally { Clean(fires, library, host); }
        }

        /// <summary>
        /// A fire on a layer the slice is hiding is <b>dark</b>, not merely unfed.
        ///
        /// <para><b>The trap this class was most likely to fall into.</b> Hiding the flame does
        /// nothing about the light: a <see cref="Light"/> has no idea what the slice camera is
        /// doing, so a campfire on a hidden storey would go on lighting what the player *can* see
        /// through a floor. Enabling and disabling are two operations on one object and the second
        /// is the one that gets forgotten.</para>
        ///
        /// <para><b>The fire is above and the camera below, which is the way round that hides
        /// anything.</b> The first version of this test stood two layers *above* a fire and
        /// expected it to go dark; it did not, and the code was right. The landscape is never cut
        /// away — CLAUDE.md: below the surface "one layer above is x-rayed and every layer below
        /// is drawn" — so a fire underneath you stays visible on purpose. It is the storeys
        /// overhead that disappear.</para>
        /// </summary>
        [Test]
        public void OnlyTheFiresInsideTheSlicesOwnBandAreLit()
        {
            // One fire on every storey, so whatever the slice decides, something is inside the
            // band and something is outside it.
            var (fires, library, host) = Build(
                new CellRef(2, 2, 0), new CellRef(3, 3, 1), new CellRef(4, 4, 2));
            try
            {
                if (!fires.Warmed) Assert.Ignore("no particle shader in this build");

                var slice = new SliceSettings();

                for (int active = 0; active < Size.SizeY; active++)
                {
                    // The band asked of the slice, exactly as FireDirector asks it. Written out
                    // rather than hard-coded because the slice's own tuning is not this test's
                    // business — what is, is that the fires agree with it.
                    int lowest = Mathf.Max(0, slice.LowestDrawnLayer(active, 0));
                    int highest = slice.HighestVisibleLayer(active, Size.SizeY);

                    int expected = 0;
                    for (int y = 0; y < Size.SizeY; y++)
                        if (y >= lowest && y <= highest) expected++;

                    fires.Sync(active, slice, 1f / 60f);

                    Assert.That(fires.LitFires, Is.EqualTo(expected),
                        $"standing on layer {active}, the slice draws layers {lowest}..{highest}");
                    Assert.That(LiveLights(host), Is.EqualTo(expected),
                        $"standing on layer {active}, a fire outside layers {lowest}..{highest} " +
                        "still has its light on — it is lighting what the player can see through " +
                        "a floor, which hiding the flame alone does not fix");
                }
            }
            finally { Clean(fires, library, host); }
        }

        /// <summary>
        /// A paused game holds the fire still.
        ///
        /// <para>Unity's particle clock knows nothing about the simulation clock, which is the
        /// fault <c>ChipDirector.Running</c> was written for — wood thrown a frame before a pause
        /// went on arcing to the ground after everything that threw it had stopped. The flicker
        /// has the same problem and is more visible, because it never ends.</para>
        /// </summary>
        [Test]
        public void PausingHoldsTheFlickerWhereItIs()
        {
            var (fires, library, host) = Build(new CellRef(2, 2, 0));
            try
            {
                if (!fires.Warmed) Assert.Ignore("no particle shader in this build");

                var slice = new SliceSettings();
                for (int i = 0; i < 10; i++) fires.Sync(0, slice, 1f / 60f);

                fires.Running = false;
                fires.Sync(0, slice, 1f / 60f);
                float held = FirstLight(host)!.intensity;

                for (int i = 0; i < 20; i++) fires.Sync(0, slice, 1f / 60f);

                Assert.That(FirstLight(host)!.intensity, Is.EqualTo(held).Within(0.0001f),
                    "the flicker advanced while the game was paused");
            }
            finally { Clean(fires, library, host); }
        }

        /// <summary>
        /// The light never casts a shadow, whatever else is tuned.
        ///
        /// <para>A point light's shadow is <b>six</b> faces out of a single 2048 additional-light
        /// atlas, and each face re-renders the geometry around it. Ten shadow-casting campfires
        /// would ask for sixty. This is a decision rather than an omission (§8), which is why it
        /// is asserted rather than left to whoever next tunes the look.</para>
        /// </summary>
        [Test]
        public void TheFireCastsNoShadow()
        {
            var (fires, library, host) = Build(new CellRef(2, 2, 0));
            try
            {
                if (!fires.Warmed) Assert.Ignore("no particle shader in this build");

                fires.Sync(0, new SliceSettings(), 1f / 60f);

                Light? light = FirstLight(host);
                Assert.That(light, Is.Not.Null);
                Assert.That(light!.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(light.type, Is.EqualTo(LightType.Point),
                    "a directional fire would light the portrait studio five kilometres below the " +
                    "board, and the portrait is cached for the session");
            }
            finally { Clean(fires, library, host); }
        }

        /// <summary>A board with no campfire on it makes no lights and no particles at all.</summary>
        [Test]
        public void ABoardWithNoCampfireCostsNothing()
        {
            var (fires, library, host) = Build();
            try
            {
                if (!fires.Warmed) Assert.Ignore("no particle shader in this build");

                fires.Sync(0, new SliceSettings(), 1f / 60f);

                Assert.That(fires.LitFires, Is.EqualTo(0));
                Assert.That(LiveLights(host), Is.EqualTo(0));
            }
            finally { Clean(fires, library, host); }
        }

        static Light? FirstLight(GameObject host)
        {
            foreach (Light light in host.GetComponentsInChildren<Light>(includeInactive: true))
                return light;
            return null;
        }
    }
}
