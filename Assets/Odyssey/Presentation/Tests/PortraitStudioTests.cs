#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The photographer with nobody to photograph (<c>docs/design/20-avatars.md</c> §10).
    ///
    /// <para><b>This is the path a clone without the licensed packs takes</b>, and it is the half
    /// of the studio that can be tested anywhere: no catalogue, no prefabs, no graphics device
    /// needed. It must answer "no portrait" rather than throw, because the interface's whole
    /// bargain is that the drawn avatar stands in and the screen is still correct.</para>
    /// </summary>
    public class PortraitStudioTests
    {
        [Test]
        public void WithNoCatalogueNobodyCanBePhotographedAndNothingThrows()
        {
            using var studio = new PortraitStudio(catalogue: null, materials: null);

            Assert.That(studio.Available, Is.False);
            Assert.That(studio.Portraits, Is.Zero);
            Assert.That(studio.For(4242u, new PawnId(1)), Is.Null);
            Assert.That(studio.LiveRenderTextures, Is.Zero,
                "a studio that cannot photograph anybody must not have built a rig");
        }

        [Test]
        public void ACatalogueWhoseArtIsAbsentIsNotAStudio()
        {
            // **The case the self-hosted runner found and this machine could not.** The catalogue
            // is a committed asset; the prefabs it points at live in gitignored Assets/Synty. So a
            // checkout without the packs loads a catalogue full of rows and empty of art, and the
            // first version of `Available` counted the rows — answering "yes, photographs are
            // possible" on exactly the machine where they are not, which made two tests run and
            // fail instead of ignoring themselves.
            var catalogue = ScriptableObject.CreateInstance<ModuleCatalogue>();
            try
            {
                var rows = new List<ModuleEntry>();
                for (int i = 0; i < 12; i++)
                    rows.Add(new ModuleEntry { moduleId = ModuleIds.Colonist(i) });
                catalogue.SetEntries(rows);

                using var studio = new PortraitStudio(catalogue, materials: null);

                Assert.That(studio.Available, Is.False,
                    "twelve rows with no prefab between them is not a studio");
                Assert.That(studio.For(4242u, new PawnId(1)), Is.Null);
            }
            finally { Object.DestroyImmediate(catalogue); }
        }

        [Test]
        public void AMissIsCachedLikeAHit()
        {
            // Nearly every key in the icon registry is a miss and IconArt caches those for the same
            // reason: a lookup that cannot succeed must not be repeated every frame. Here the
            // answer cannot change without the catalogue changing, so one no is enough.
            using var studio = new PortraitStudio(catalogue: null, materials: null);

            studio.For(4242u, new PawnId(1));
            studio.For(4242u, new PawnId(1));
            studio.For(4242u, new PawnId(2));

            Assert.That(studio.Portraits, Is.EqualTo(2),
                "two distinct colonists asked for four times should be two cache entries");
        }

        [Test]
        public void TheStudioUsesTheBookItIsGivenRatherThanOneOfItsOwn()
        {
            // Identity, not equality. The pinned cast and any future player-chosen appearance live
            // on the book, and a studio holding a private copy would photograph somebody the board
            // is not drawing — which is the exact fault this whole design exists to foreclose.
            var book = new ColonistAppearanceBook(7u, 61) { Pinned = 31337u };
            using var studio = new PortraitStudio(catalogue: null, materials: null)
            {
                Appearances = book,
            };

            studio.For(4242u, new PawnId(1));
            Assert.That(studio.Appearances, Is.SameAs(book));
        }
    }
}
