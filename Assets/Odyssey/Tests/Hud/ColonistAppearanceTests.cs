#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// What a colonist looks like, and the one invariant that had no test at all.
    ///
    /// The appearance itself is pure arithmetic and used to live in <c>Odyssey.Presentation</c>
    /// beside the renderers, where it could only be proved by a Unity run. **It moved on
    /// 2026-09-18 and this file came with it**, which is what the note left here always said it
    /// would be: a file move, because the derivation is written Unity-free (<see cref="Rgb24"/>,
    /// integers, no <c>Random</c>). What forced it was the flat avatar
    /// (<c>docs/design/20-avatars.md</c>) — the HUD cannot draw a colonist's colours from an
    /// assembly it does not reference, and the dependency runs Presentation → Hud and never back.
    /// </summary>
    public class ColonistAppearanceTests
    {
        const int Faces = 61;

        /// <summary>
        /// A pawn with no roll seed of its own, which the book reads as "use the world's".
        ///
        /// <para><b>Every test in this class is now about that path, and deliberately so.</b> A
        /// colonist is dealt from their own <c>RollSeed</c> since 2026-09-18
        /// (<c>docs/design/20-avatars.md</c> §5), and this file's subject is the world-level cast:
        /// the same world dealing the same people, two worlds dealing different ones, the streams
        /// being independent. All of that is still true of the fallback, and the fallback is what a
        /// colony saved before U40 gets — so these are not tests that were quietly retargeted at a
        /// dead branch, they are the tests for the branch those colonies take.</para>
        /// </summary>
        const uint NoRollSeed = 0u;

        [Test]
        public void TheSameWorldDealsTheSamePersonForEver()
        {
            // The owner's requirement in one assertion: load the world again, get the same colony.
            // Two separately constructed books, because a cache that agreed with itself would
            // prove nothing.
            var monday = new ColonistAppearanceBook(20260916u, Faces);
            var tuesday = new ColonistAppearanceBook(20260916u, Faces);

            for (int id = 1; id <= 500; id++)
                Assert.That(tuesday.For(id, NoRollSeed), Is.EqualTo(monday.For(id, NoRollSeed)), $"pawn {id}");
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
                if (!a.For(id, NoRollSeed).Equals(b.For(id, NoRollSeed))) differences++;

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
                for (int id = 1; id <= 5; id++) faces.Add(book.For(id, NoRollSeed).Look);
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
            //
            // Asked of the derivation at a fixed age rather than through the book, because the
            // book greys a colonist's hair with age (MC3) and a greyed colour is a mix rather than
            // a palette entry. The independence being tested here is a property of the four
            // streams, and thirty is simply an age at which nothing has greyed yet.
            ColonistCastPools pools = ColonistCastPools.AllBodies(Faces);
            var byCloth = new Dictionary<uint, HashSet<uint>>();
            var allHair = new HashSet<uint>();

            for (int id = 1; id <= 4000; id++)
            {
                ColonistAppearance a = ColonistAppearance.Of(4242u, id, pools, 'n', 30);
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
            //
            // At a fixed age, for the reason TheThreeSlotsAreIndependent gives: greying makes a
            // hair colour a mix of a palette entry and grey, and what is being asserted here is
            // that every palette entry can be drawn at all.
            ColonistCastPools pools = ColonistCastPools.AllBodies(Faces);
            var skin = new HashSet<uint>();
            var hair = new HashSet<uint>();
            var cloth = new HashSet<uint>();

            for (int id = 1; id <= 2000; id++)
            {
                ColonistAppearance a = ColonistAppearance.Of(7u, id, pools, 'n', 30);
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
                ColonistAppearance a = book.For(id, NoRollSeed);
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
            for (int id = 1; id <= 200; id++) clothBefore.Add(book.For(id, NoRollSeed).Cloth.Packed);

            // The clothing stream does not consult the hair table's length, so a hypothetical
            // change to it cannot move these. Asserted by deriving with a different face count,
            // which is the one length that legitimately *does* move the body and nothing else.
            var wider = new ColonistAppearanceBook(31337u, Faces + 7);
            for (int id = 1; id <= 200; id++)
                Assert.That(wider.For(id, NoRollSeed).Cloth.Packed, Is.EqualTo(clothBefore[id - 1]),
                    $"pawn {id} changed clothes because the catalogue grew");
        }

        [Test]
        public void AnOverrideWinsAndCanBeTakenBack()
        {
            // The seam for the appearance panel that does not exist yet. Nothing in the game calls
            // this; the test is what keeps the shape honest until something does.
            var book = new ColonistAppearanceBook(5u, Faces);
            ColonistAppearance derived = book.For(3, NoRollSeed);
            var chosen = new ColonistAppearance(1, Rgb24.FromHex(0x112233), Rgb24.FromHex(0x445566),
                                                Rgb24.FromHex(0x778899), Rgb24.FromHex(0xAABBCC));

            book.Override(3, chosen);
            Assert.That(book.For(3, NoRollSeed), Is.EqualTo(chosen));
            Assert.That(book.OverrideCount, Is.EqualTo(1));
            Assert.That(book.For(4, NoRollSeed), Is.Not.EqualTo(chosen), "an override must not leak to a neighbour");

            book.ClearOverride(3);
            Assert.That(book.For(3, NoRollSeed), Is.EqualTo(derived));
            Assert.That(book.OverrideCount, Is.Zero);
        }

        [Test]
        public void OneFaceIsAlwaysFaceZero()
        {
            // A clone with no licensed packs has exactly one colonist row, and a modulus by one
            // must not be an edge case anywhere.
            var book = new ColonistAppearanceBook(12345u, 1);
            for (int id = 1; id <= 50; id++) Assert.That(book.For(id, NoRollSeed).Look, Is.Zero);
        }

        [Test]
        public void ASeedOfZeroIsAnOrdinaryWorldAndNotAnUnsetOne()
        {
            // WorldSnapshot.Seed defaults to zero on a frame nobody has written, and a harness
            // that builds one by hand is a legitimate caller. Zero must deal a cast like any
            // other number rather than collapsing everybody onto one face.
            var book = new ColonistAppearanceBook(0u, Faces);
            var faces = new HashSet<int>();
            for (int id = 1; id <= 50; id++) faces.Add(book.For(id, NoRollSeed).Look);
            Assert.That(faces.Count, Is.GreaterThan(10));
        }

        [Test]
        public void AColonistIsDealtFromTheirOwnRollSeed()
        {
            // The change that makes a select screen honest (docs/design/20-avatars.md §5). Two
            // colonists in one world, rolled from two seeds, must not be dealt the world's cast.
            var book = new ColonistAppearanceBook(20260918u, Faces);

            Assert.That(book.For(1, 4242u), Is.Not.EqualTo(book.For(1, NoRollSeed)),
                "a pawn with a seed of its own was still dealt the world's");
            Assert.That(book.For(1, 4242u), Is.EqualTo(new ColonistAppearanceBook(1u, Faces).For(1, 4242u)),
                "the same roll seed gave two answers in two worlds, so a colonist chosen on the " +
                "setup screen is not the colonist the colony builds");
        }

        [Test]
        public void ACacheFilledBeforeTheSeedArrivedIsNotKeptForEver()
        {
            // The one real hazard in caching this. A figure can be leased before the frame
            // carrying the aspect has been handed over, and the fallback answer would then be
            // this colonist's face for the rest of the session.
            var book = new ColonistAppearanceBook(7u, Faces);

            ColonistAppearance early = book.For(2, NoRollSeed);
            ColonistAppearance proper = book.For(2, 99u);

            Assert.That(proper, Is.Not.EqualTo(early));
            Assert.That(book.For(2, 99u), Is.EqualTo(proper), "the second answer is not cached either");
            Assert.That(book.For(2, NoRollSeed), Is.EqualTo(early), "the fallback stopped answering");
        }

        [Test]
        public void APinnedCastOverrulesEveryPawnsOwnSeed()
        {
            // The two development switches, which would otherwise have become inspector fields
            // that quietly do nothing the day a face started following the pawn.
            var book = new ColonistAppearanceBook(7u, Faces) { Pinned = 31337u };
            var pinned = new ColonistAppearanceBook(31337u, Faces);

            for (int id = 1; id <= 20; id++)
                Assert.That(book.For(id, 4242u), Is.EqualTo(pinned.For(id, NoRollSeed)), $"pawn {id}");

            // And taking the pin off gives the pawns back, rather than leaving the pinned answer
            // in the cache — the same hazard the fallback has.
            book.Pinned = 0u;
            Assert.That(book.For(1, 4242u), Is.Not.EqualTo(pinned.For(1, NoRollSeed)));
        }

        [Test]
        public void AnOverrideBeatsASeedOfEitherKind()
        {
            // The appearance panel's seam, which must keep winning: a player-chosen face is not a
            // derivation to be recomputed when a seed turns up.
            var book = new ColonistAppearanceBook(7u, Faces);
            var chosen = new ColonistAppearance(3, Rgb24.FromHex(0xC89870), Rgb24.FromHex(0x3B2A1E),
                                                Rgb24.FromHex(0x4A4F55), Rgb24.FromHex(0x2A2D31));

            book.Override(5, chosen);
            Assert.That(book.For(5, NoRollSeed), Is.EqualTo(chosen));
            Assert.That(book.For(5, 12345u), Is.EqualTo(chosen));
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
}
