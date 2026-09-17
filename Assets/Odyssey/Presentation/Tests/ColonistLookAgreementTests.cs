#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The invariant <c>ColonistLook</c>'s header has always warned about, which until now nothing
    /// asserted: <b>the two drawers of colonists must deal the same face to the same pawn.</b>
    ///
    /// <para>They did not. The instanced renderer sized its lottery from every colonist row in the
    /// catalogue, while the figure director dropped rows whose prefab or gaits were missing and
    /// <i>compacted the survivors</i> — so with anything missing, look <c>i</c> meant a different
    /// body to each of them, and every colonist would change identity on crossing the figure cap.
    /// It was invisible because either all four packs are installed or none are.</para>
    ///
    /// <para><b>Why this is the half that stayed.</b> The appearance derivation and its tests moved
    /// into <c>Odyssey.Hud</c> and the fast tier on 2026-09-18 (<c>docs/design/20-avatars.md</c>).
    /// Every test here names <see cref="ModuleCatalogue"/>, which is a <c>ScriptableObject</c> and
    /// therefore Unity, so these are exactly the cases that could not go — and they are the cases
    /// about the catalogue rather than about the colours, which is the same line
    /// <see cref="AppearanceBooks"/> is drawn along.</para>
    /// </summary>
    public class ColonistLookAgreementTests
    {
        static ModuleCatalogue CatalogueOf(int colonistRows, params int[] holes)
        {
            var catalogue = ScriptableObject.CreateInstance<ModuleCatalogue>();
            var rows = new List<ModuleEntry>();

            // A non-colonist row first, so a family lookup that accidentally counted everything
            // would be caught rather than happening to agree.
            rows.Add(new ModuleEntry { moduleId = "odyssey.module.wall.panel" });

            for (int i = 0; i < colonistRows; i++)
                rows.Add(new ModuleEntry
                {
                    moduleId = ModuleIds.Colonist(i),
                    // A hole is a row whose art did not resolve, which on a clone without the
                    // packs is every row. prefab is already null; the holes array only records
                    // which ones the assertions below expect to be unusable.
                    prefabName = System.Array.IndexOf(holes, i) >= 0 ? string.Empty : "SM_Chr_Test_" + i,
                });

            catalogue.SetEntries(rows);
            return catalogue;
        }

        [Test]
        public void TheBookCountsEveryColonistRowIncludingTheUnusableOnes()
        {
            ModuleCatalogue catalogue = CatalogueOf(12, holes: new[] { 3, 7 });
            try
            {
                ColonistAppearanceBook book = AppearanceBooks.For(1u, catalogue);
                Assert.That(book.LookCount, Is.EqualTo(12),
                    "the lottery must run over the catalogue's rows, not over the ones with art");
            }
            finally { Object.DestroyImmediate(catalogue); }
        }

        [Test]
        public void TheFigureDirectorsIndexSpaceIsTheCatalogueFamily()
        {
            // The compaction bug, stated directly. LookCount is the size of the index space and
            // must equal the family size even when nothing in it resolved to art — which, in a
            // test with no licensed packs, is every row.
            ModuleCatalogue catalogue = CatalogueOf(12);
            var parent = new GameObject("figures");
            try
            {
                var director = new World.PawnFigureDirector(catalogue, parent.transform, 0);
                ColonistAppearanceBook book = AppearanceBooks.For(1u, catalogue);

                Assert.That(director.LookCount, Is.EqualTo(book.LookCount));
                Assert.That(director.LookCount, Is.EqualTo(12));

                // No prefabs resolve in a test, so nothing is drawable and every pawn falls
                // through to the baked path. That is the designed degradation, not a failure.
                Assert.That(director.UsableLookCount, Is.Zero);
                Assert.That(director.Enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(catalogue);
            }
        }

        [Test]
        public void AMissingRowDoesNotReDealTheColony()
        {
            // The consequence that made the old behaviour dangerous rather than merely untidy.
            // Whatever the catalogue can and cannot realise, a pawn's face is the same number —
            // so installing three packs of four changes what one colonist can be drawn as, and
            // changes nobody else at all.
            ModuleCatalogue whole = CatalogueOf(12);
            ModuleCatalogue holed = CatalogueOf(12, holes: new[] { 5 });
            try
            {
                ColonistAppearanceBook before = AppearanceBooks.For(77u, whole);
                ColonistAppearanceBook after = AppearanceBooks.For(77u, holed);
                for (int id = 1; id <= 300; id++)
                    Assert.That(after.For(id, 0u), Is.EqualTo(before.For(id, 0u)), $"pawn {id}");
            }
            finally
            {
                Object.DestroyImmediate(whole);
                Object.DestroyImmediate(holed);
            }
        }

        [Test]
        public void ABookWithNoCatalogueStillAnswersEveryPawn()
        {
            // The no-packs path: one face, no art, no figures, and nothing that throws.
            ColonistAppearanceBook book = AppearanceBooks.For(3u, null);
            Assert.That(book.LookCount, Is.EqualTo(1));
            for (int id = 1; id <= 20; id++) Assert.That(book.For(id, 0u).Look, Is.Zero);
        }
    }
}
