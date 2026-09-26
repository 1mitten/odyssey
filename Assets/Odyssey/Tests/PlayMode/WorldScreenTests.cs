using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The World screen under the real bootstrap (design 59 §9): New game shows the planet with a
    /// site already picked, Next reaches the setup page, Start builds a colony that remembers its
    /// site, and Back and Escape unwind a level at a time. The models are the fast tier's; this is
    /// the one place the page, the texture and the flow are seen together.
    /// </summary>
    public class WorldScreenTests
    {
        static bool Shown(VisualElement? element) =>
            element != null && element.style.display == DisplayStyle.Flex;

        static IEnumerator Settle()
        {
            for (int frame = 0; frame < 8; frame++) yield return null;
        }

        [UnityTest]
        public IEnumerator NewGameShowsThePlanetWithASiteReadyToTake()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.World));
                Assert.That(Shown(doc.rootVisualElement.Q("world")), Is.True, "the World page is not on screen");
                Assert.That(Shown(doc.rootVisualElement.Q("setup")), Is.False, "the setup page is up under the planet");
                Assert.That(shell.Menu.World!.Planet, Is.Not.Null, "New game made no planet");
                Assert.That(shell.Menu.World.CanGoNext, Is.True, "the suggested site cannot be settled");

                // The map is one texture, drawn as the three copies the wrap needs, and it has a size.
                VisualElement? map = doc.rootVisualElement.Q("worldmap");
                Assert.That(map, Is.Not.Null);
                Assert.That(map!.worldBound.width, Is.GreaterThan(100f), "the map box was not laid out");
                var copies = map.Query<Image>().ToList();
                Assert.That(copies.Count, Is.EqualTo(3));
                Assert.That(copies[1].image, Is.Not.Null, "the planet was never painted");
                Assert.That(copies[0].image, Is.SameAs(copies[1].image), "the copies are not one texture");

                Assert.That(shell.Menu.NextFromWorld(), Is.True);
                yield return Settle();
                Assert.That(Shown(doc.rootVisualElement.Q("setup")), Is.True, "Next did not reach the setup page");
                Assert.That(Shown(doc.rootVisualElement.Q("world")), Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>The whole flow ends in a colony whose save header carries the site, on the board seed the site derives.</summary>
        [UnityTest]
        public IEnumerator StartBuildsAColonyThatRemembersItsSite()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();
                SiteTile site = shell.Menu.World!.Site!.Value;
                uint worldSeed = shell.Menu.World.WorldSeed;

                shell.Menu.NextFromWorld();
                yield return Settle();
                Assert.That(shell.Menu.Start(), Is.True);
                for (int frame = 0; frame < 30 && !boot.HasSession; frame++) yield return null;
                Assert.That(boot.HasSession, Is.True, "Start built no world");

                SaveRecipe recipe = boot.Colony!.Recipe(1);
                Assert.That(recipe.Site, Is.EqualTo(site));
                Assert.That(recipe.WorldSeed, Is.EqualTo(worldSeed));
                Assert.That(boot.World.Seed, Is.EqualTo(SiteRules.BoardSeed(worldSeed, site.TileIndex)),
                    "the board was not built on the seed the site derives");
                Assert.That(boot.World.Size.SizeY, Is.EqualTo(SiteRules.BoardLayers(MapSizes.At(MapSizes.Default).Y, site.Hills)));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator BackUnwindsOneLevelAtATime()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();
                shell.Menu.Choose(SessionCommands.NewGameKey);
                shell.Menu.NextFromWorld();
                yield return Settle();

                shell.Menu.Back();
                yield return Settle();
                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.World));
                Assert.That(Shown(doc.rootVisualElement.Q("world")), Is.True);

                shell.Menu.Back();
                yield return Settle();
                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.Root));
                Assert.That(Shown(doc.rootVisualElement.Q("world")), Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
