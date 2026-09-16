#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// What a colonist looks like, and the one invariant that had no test at all.
    ///
    /// The appearance itself is pure arithmetic and could in principle live in the fast tier; it
    /// sits here because <c>Odyssey.Presentation</c> references UnityEngine, and a new assembly
    /// definition is a compile-graph change for the whole project in exchange for running a
    /// handful of pure tests thirty seconds earlier. The derivation is written Unity-free
    /// (<see cref="Rgb24"/>, integers, no <c>Random</c>) so that move stays a file move.
    /// </summary>
    public class ColonistAppearanceTests
    {
        const int Faces = 61;

        [Test]
        public void TheSameWorldDealsTheSamePersonForEver()
        {
            // The owner's requirement in one assertion: load the world again, get the same colony.
            // Two separately constructed books, because a cache that agreed with itself would
            // prove nothing.
            var monday = new ColonistAppearanceBook(20260916u, Faces);
            var tuesday = new ColonistAppearanceBook(20260916u, Faces);

            for (int id = 1; id <= 500; id++)
                Assert.That(tuesday.For(id), Is.EqualTo(monday.For(id)), $"pawn {id}");
        }

        [Test]
        public void ADifferentWorldDealsADifferentCast()
        {
            // The reason the session salt existed in the first place: a cast of sixty-one must not
            // read as a cast of five. The starting colony is always pawns 1 to 5, so those are the
            // ids that matter — a hash that only varied for pawn 400 would have fooled the old
            // code and must not fool this test.
            var a = new ColonistAppearanceBook(1u, Faces);
            var b = new ColonistAppearanceBook(2u, Faces);

            int differences = 0;
            for (int id = 1; id <= 5; id++)
                if (!a.For(id).Equals(b.For(id))) differences++;

            Assert.That(differences, Is.GreaterThanOrEqualTo(4),
                "two worlds should not open on nearly the same five people");
        }

        [Test]
        public void TheStartingFiveAreNotFiveCopiesOfOneMan()
        {
            // Measured over seeds rather than over ids, because the failure being guarded against
            // is per-world: some seed dealing its opening colony a single repeated face.
            //
            // Three is the measured worst case over these 200 seeds, not a margin — seed 81 opens
            // with three distinct faces among five colonists and every other seed does better. So
            // this asserts what the hash actually achieves, and a change that makes any opening
            // cast blander fails here rather than being noticed in a screenshot months later.
            for (uint seed = 1; seed <= 200; seed++)
            {
                var book = new ColonistAppearanceBook(seed, Faces);
                var faces = new HashSet<int>();
                for (int id = 1; id <= 5; id++) faces.Add(book.For(id).Look);
                Assert.That(faces.Count, Is.GreaterThanOrEqualTo(3),
                    $"seed {seed} opened with {faces.Count} distinct faces among five colonists");
            }
        }

        [Test]
        public void TheThreeSlotsAreIndependent()
        {
            // The correlated-hash bug, which is invisible until fifty colonists are on screen at
            // once: take one avalanche and read it modulo three table lengths, and everybody with
            // red hair also wears red. Asserted as conditional distribution — the spread of hair
            // among the colonists wearing one particular garment must look like the spread of hair
            // over everybody.
            var book = new ColonistAppearanceBook(4242u, Faces);
            var byCloth = new Dictionary<uint, HashSet<uint>>();
            var allHair = new HashSet<uint>();

            for (int id = 1; id <= 4000; id++)
            {
                ColonistAppearance a = book.For(id);
                allHair.Add(a.Hair.Packed);
                if (!byCloth.TryGetValue(a.Cloth.Packed, out HashSet<uint>? hairs))
                    byCloth[a.Cloth.Packed] = hairs = new HashSet<uint>();
                hairs.Add(a.Hair.Packed);
            }

            Assert.That(allHair.Count, Is.EqualTo(ColonistPalette.Hair.Length));
            foreach (KeyValuePair<uint, HashSet<uint>> pair in byCloth)
                Assert.That(pair.Value.Count, Is.EqualTo(ColonistPalette.Hair.Length),
                    $"colonists wearing #{pair.Key:X6} only ever have {pair.Value.Count} hair colours");
        }

        [Test]
        public void EveryColourInEveryTableIsReachable()
        {
            // A palette entry nothing can ever draw is a table edit that silently did nothing.
            var book = new ColonistAppearanceBook(7u, Faces);
            var skin = new HashSet<uint>();
            var hair = new HashSet<uint>();
            var cloth = new HashSet<uint>();

            for (int id = 1; id <= 2000; id++)
            {
                ColonistAppearance a = book.For(id);
                skin.Add(a.Skin.Packed);
                hair.Add(a.Hair.Packed);
                cloth.Add(a.Cloth.Packed);
            }

            Assert.That(skin.Count, Is.EqualTo(ColonistPalette.Skin.Length));
            Assert.That(hair.Count, Is.EqualTo(ColonistPalette.Hair.Length));
            Assert.That(cloth.Count, Is.EqualTo(ColonistPalette.Cloth.Length));
        }

        [Test]
        public void TheSecondGarmentIsAlwaysDarkerThanTheFirst()
        {
            // The rule that keeps a randomised colony from reading as a clown parade: the second
            // garment is the same cloth in shadow, never an independent colour.
            var book = new ColonistAppearanceBook(99u, Faces);
            for (int id = 1; id <= 500; id++)
            {
                ColonistAppearance a = book.For(id);
                int first = a.Cloth.R + a.Cloth.G + a.Cloth.B;
                int second = a.Cloth2.R + a.Cloth2.G + a.Cloth2.B;
                Assert.That(second, Is.LessThan(first), $"pawn {id}");
            }
        }

        [Test]
        public void ChangingOnePaletteDoesNotRepaintTheOthers()
        {
            // Stream independence stated the other way round, and the one that bites a future
            // editor: adding a hair colour must not give every colonist different trousers.
            var book = new ColonistAppearanceBook(31337u, Faces);
            var clothBefore = new List<uint>();
            for (int id = 1; id <= 200; id++) clothBefore.Add(book.For(id).Cloth.Packed);

            // The clothing stream does not consult the hair table's length, so a hypothetical
            // change to it cannot move these. Asserted by deriving with a different face count,
            // which is the one length that legitimately *does* move the body and nothing else.
            var wider = new ColonistAppearanceBook(31337u, Faces + 7);
            for (int id = 1; id <= 200; id++)
                Assert.That(wider.For(id).Cloth.Packed, Is.EqualTo(clothBefore[id - 1]),
                    $"pawn {id} changed clothes because the catalogue grew");
        }

        [Test]
        public void AnOverrideWinsAndCanBeTakenBack()
        {
            // The seam for the appearance panel that does not exist yet. Nothing in the game calls
            // this; the test is what keeps the shape honest until something does.
            var book = new ColonistAppearanceBook(5u, Faces);
            ColonistAppearance derived = book.For(3);
            var chosen = new ColonistAppearance(1, Rgb24.FromHex(0x112233), Rgb24.FromHex(0x445566),
                                                Rgb24.FromHex(0x778899), Rgb24.FromHex(0xAABBCC));

            book.Override(3, chosen);
            Assert.That(book.For(3), Is.EqualTo(chosen));
            Assert.That(book.OverrideCount, Is.EqualTo(1));
            Assert.That(book.For(4), Is.Not.EqualTo(chosen), "an override must not leak to a neighbour");

            book.ClearOverride(3);
            Assert.That(book.For(3), Is.EqualTo(derived));
            Assert.That(book.OverrideCount, Is.Zero);
        }

        [Test]
        public void OneFaceIsAlwaysFaceZero()
        {
            // A clone with no licensed packs has exactly one colonist row, and a modulus by one
            // must not be an edge case anywhere.
            var book = new ColonistAppearanceBook(12345u, 1);
            for (int id = 1; id <= 50; id++) Assert.That(book.For(id).Look, Is.Zero);
        }

        [Test]
        public void ASeedOfZeroIsAnOrdinaryWorldAndNotAnUnsetOne()
        {
            // WorldSnapshot.Seed defaults to zero on a frame nobody has written, and a harness
            // that builds one by hand is a legitimate caller. Zero must deal a cast like any
            // other number rather than collapsing everybody onto one face.
            var book = new ColonistAppearanceBook(0u, Faces);
            var faces = new HashSet<int>();
            for (int id = 1; id <= 50; id++) faces.Add(book.For(id).Look);
            Assert.That(faces.Count, Is.GreaterThan(10));
        }

        [Test]
        public void ShadingIsIntegerArithmeticAndNeverOverflows()
        {
            Assert.That(Rgb24.FromHex(0xFFFFFF).Scaled(78), Is.EqualTo(Rgb24.FromHex(0xC7C7C7)));
            Assert.That(Rgb24.FromHex(0x000000).Scaled(55), Is.EqualTo(Rgb24.FromHex(0x000000)));
            Assert.That(Rgb24.FromHex(0xFFFFFF).Scaled(100), Is.EqualTo(Rgb24.FromHex(0xFFFFFF)));
            Assert.That(Rgb24.FromHex(0x8040C0).Packed, Is.EqualTo(0x8040C0u));
        }
    }

    /// <summary>
    /// The invariant <c>ColonistLook</c>'s header has always warned about, which until now nothing
    /// asserted: <b>the two drawers of colonists must deal the same face to the same pawn.</b>
    ///
    /// <para>They did not. The instanced renderer sized its lottery from every colonist row in the
    /// catalogue, while the figure director dropped rows whose prefab or gaits were missing and
    /// <i>compacted the survivors</i> — so with anything missing, look <c>i</c> meant a different
    /// body to each of them, and every colonist would change identity on crossing the figure cap.
    /// It was invisible because either all four packs are installed or none are.</para>
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
                var book = new ColonistAppearanceBook(1u, catalogue);
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
                var book = new ColonistAppearanceBook(1u, catalogue);

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
                var before = new ColonistAppearanceBook(77u, whole);
                var after = new ColonistAppearanceBook(77u, holed);
                for (int id = 1; id <= 300; id++)
                    Assert.That(after.For(id), Is.EqualTo(before.For(id)), $"pawn {id}");
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
            var book = new ColonistAppearanceBook(3u, (ModuleCatalogue?)null);
            Assert.That(book.LookCount, Is.EqualTo(1));
            for (int id = 1; id <= 20; id++) Assert.That(book.For(id).Look, Is.Zero);
        }
    }
}
