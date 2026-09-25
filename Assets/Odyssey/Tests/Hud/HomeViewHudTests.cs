#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Design 43 §5: the Home view is a switch of its own on the views strip and in the Menu, drawn
    /// with the house path Claude Design gave, and named by the registry.
    /// </summary>
    public class HomeViewHudTests
    {
        [Test]
        public void HomeIsItsOwnSwitch()
        {
            var overlays = new OverlayDirector();
            int changes = 0;
            overlays.Changed += () => changes++;
            Assert.That(HudViews.Keys, Is.EqualTo(new[] { HudViews.Power, HudViews.Home }), "Home sits under Power");
            Assert.That(HudViews.IsOn(overlays, HudViews.Home), Is.False, "the view starts off");

            HudViews.Toggle(overlays, HudViews.Home);
            Assert.That(overlays.HomeVisible, Is.True);
            Assert.That(overlays.PowerVisible, Is.False, "Home switched power on");
            Assert.That(changes, Is.EqualTo(1));

            // Control: power's switch leaves home alone.
            HudViews.Toggle(overlays, HudViews.Power);
            Assert.That(overlays.HomeVisible, Is.True);
            HudViews.Toggle(overlays, HudViews.Home);
            Assert.That(HudViews.IsOn(overlays, HudViews.Home), Is.False);
            Assert.That(overlays.PowerVisible, Is.True, "Home switched power off");
        }

        /// <summary>Every view on the strip has a live Menu row, and every other overlay row is dim.</summary>
        [Test]
        public void TheMenuRowsAreLiveExactlyForTheViews()
        {
            foreach (string key in HudViews.Keys)
            {
                Assert.That(HudViews.MenuOverlays, Does.Contain(key), $"{key} is on the strip and not in the Menu");
                Assert.That(HudViews.IsLive(key), Is.True);
            }
            int dim = 0;
            foreach (string key in HudViews.MenuOverlays)
                if (!HudViews.IsLive(key)) dim++;
            Assert.That(dim, Is.EqualTo(HudViews.MenuOverlays.Length - HudViews.Keys.Length), "a Menu row went live with no view");
            Assert.That(HudViews.IsLive("ui.overlay.zones"), Is.False);
        }

        [Test]
        public void TheHomeButtonDrawsTheHouse()
        {
            Assert.That(HudViews.PathOf(HudViews.Home), Is.SameAs(HudIcons.Home));
            Assert.That(HudIcons.PathOf("home"), Is.SameAs(HudIcons.Home));
            Assert.That(HudViews.PathOf(HudViews.Power), Is.Null, "power's bolt is a HudGlyph");
            Assert.That(HudIcons.PathOf("no-such-icon"), Is.Null);

            // One closed shape, so it fills; the parse-and-box check is SettingsLayoutTests'.
            IReadOnlyList<SvgPath.Subpath> house = SvgPath.Parse(HudIcons.Home);
            Assert.That(house, Has.Count.EqualTo(1));
            Assert.That(house[0].Closed, Is.True, "the house is open, so a fill would leak");
        }

        [Test]
        public void EveryViewAndMenuRowIsNamedByTheRegistry()
        {
            Assert.That(Registry.Label(HudViews.Home), Is.EqualTo("Home"));
            foreach (string key in HudViews.MenuOverlays)
                Assert.That(Registry.Labels.ContainsKey(key), Is.True, $"{key} is not in the registry");
        }
    }
}
