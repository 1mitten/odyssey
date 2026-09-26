#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The one table of line art every <c>IconBadge</c> falls back on (owner, 2026-09-26: icons
    /// "come from the same place … so we only update once"). A key here must be a registry key, an
    /// alias must name a key that has a picture, and every path must draw.
    /// </summary>
    public class IconGlyphsTests
    {
        [Test]
        public void EveryKeyIsARegistryKeyAndEveryPathDraws()
        {
            var wrong = new List<string>();
            foreach (KeyValuePair<string, string> glyph in IconGlyphs.Paths)
            {
                if (!Registry.Labels.ContainsKey(glyph.Key)) wrong.Add($"{glyph.Key} is not a registry key");
                if (SvgPath.Parse(glyph.Value).Count == 0) wrong.Add($"{glyph.Key} draws nothing");
            }
            foreach (KeyValuePair<string, string> alias in IconGlyphs.Aliases)
            {
                if (!Registry.Labels.ContainsKey(alias.Key)) wrong.Add($"alias {alias.Key} is not a registry key");
                if (!IconGlyphs.Paths.ContainsKey(alias.Value)) wrong.Add($"alias {alias.Key} names {alias.Value}, which has no path");
                if (IconGlyphs.Paths.ContainsKey(alias.Key)) wrong.Add($"{alias.Key} is both a path and an alias");
            }
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        [Test]
        public void AnAliasDrawsWhatItNames()
        {
            Assert.That(IconGlyphs.For("ui.terrain.bush.picked"), Is.EqualTo(IconGlyphs.For("ui.terrain.bush.berry")));
            Assert.That(IconGlyphs.For("ui.health.leg.left"), Is.EqualTo(IconGlyphs.For("ui.health.torso")));
            Assert.That(IconGlyphs.For("ui.no.such.key"), Is.Empty);
            Assert.That(IconGlyphs.For(string.Empty), Is.Empty);
        }

        /// <summary>
        /// Everything the inspect pane can put in its avatar for a thing in the world — an item,
        /// a standing thing, the ground, an animal or a person — has a picture.
        /// </summary>
        [Test]
        public void EveryThingThePaneCanShowHasAPicture()
        {
            var missing = new List<string>();
            void Want(string key) { if (key.Length > 0 && IconGlyphs.For(key).Length == 0) missing.Add(key); }
            foreach (string key in ItemLabels.Keys) Want(key);
            foreach (string key in EdificeLabels.Keys) Want(key);
            foreach (string key in TerrainLabels.Keys) Want(key);
            foreach (string key in PawnKindLabels.IconKeys) Want(key);
            foreach (string key in BuildLabels.StuffKeys) Want(key);
            Assert.That(missing, Is.Empty, "the pane would draw a placeholder square for:\n  " + string.Join("\n  ", missing));
        }
    }
}
