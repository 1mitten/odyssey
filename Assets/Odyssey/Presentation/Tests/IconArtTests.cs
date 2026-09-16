#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Ui;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The icon art that is in the build, held to ADR 0007's import rules.
    ///
    /// <para><b>Why a test and not an eye.</b> Every one of these settings is invisible on screen
    /// until it is too late: a bilinear icon costs the second atlas page in silence, a mip-mapped
    /// one blurs only at some scales, and a texture that fails to import at all draws the
    /// placeholder square the HUD would draw anyway. The art is either in the build under the key
    /// the HUD asks for, with the settings the ADR fixes, or the interface quietly goes back to
    /// squares and nobody notices for a fortnight.</para>
    ///
    /// <para>It walks whatever art exists rather than naming keys, so the next sheet that lands
    /// is covered by it without being added to it.</para>
    /// </summary>
    public class IconArtTests
    {
        /// <summary>The keys the owner has drawn, in the order they arrived.</summary>
        static readonly string[] Drawn =
        {
            "ui.res.wood",
            "ui.res.stone",
            "ui.res.ironore",
            "ui.res.scrap",
            "ui.status.felling",
            "ui.status.mining",
            "ui.status.building",
        };

        [Test]
        public void TheDrawnKeysAreInTheBuild()
        {
            foreach (string key in Drawn)
                Assert.That(IconArt.For(key), Is.Not.Null,
                    $"{key} has no art; the HUD will draw its placeholder square");
        }

        /// <summary>
        /// A file named for a key nobody asks for draws nothing and says nothing, which is the
        /// one failure this whole arrangement cannot see from the screen: the badge falls back to
        /// its square and looks exactly like a key that was never drawn.
        /// </summary>
        [Test]
        public void EveryIconIsNamedForAKeyTheRegistryKnows()
        {
            foreach (Texture2D art in Resources.LoadAll<Texture2D>(IconArt.Folder))
                Assert.That(Odyssey.Hud.Registry.Labels.ContainsKey(art.name), Is.True,
                    $"'{art.name}.png' is named for no key in icon-keys.csv, so nothing will " +
                    "ever ask for it");
        }

        [Test]
        public void EveryIconIsSixtyFourPixelsSquare()
        {
            foreach (Texture2D art in Resources.LoadAll<Texture2D>(IconArt.Folder))
                Assert.That((art.width, art.height), Is.EqualTo((64, 64)),
                    $"{art.name} is {art.width}x{art.height}; ADR 0007 fixes every interface " +
                    "icon at 64 x 64, which is UI Toolkit's own maxSubTextureSize");
        }

        [Test]
        public void EveryIconIsPointFilteredWithNoMips()
        {
            foreach (Texture2D art in Resources.LoadAll<Texture2D>(IconArt.Folder))
            {
                Assert.That(art.filterMode, Is.EqualTo(FilterMode.Point),
                    $"{art.name} is not point-filtered; filter mode selects the atlas page, so " +
                    "one bilinear icon spends the whole two-page budget by itself");
                Assert.That(art.mipmapCount, Is.EqualTo(1),
                    $"{art.name} has mip-maps; pixel art wants none");
            }
        }

        [Test]
        public void AMissingKeyIsNotAnError()
        {
            Assert.That(IconArt.For("ui.res.nothing-is-called-this"), Is.Null);
            Assert.That(IconArt.Has(string.Empty), Is.False);
        }
    }
}
