#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The bandit's outfit laid over a person (<c>docs/design/42-bandits.md</c> §5).
    ///
    /// <para>The property this file exists for is the first one: <b>a bandit is the person the same
    /// seed and id would deal a colonist, dressed.</b> Sex, skin, hair and beard come through
    /// untouched, so a bandit taken prisoner and put in the uniform is who the gang had — the
    /// owner's "they need to be their own character".</para>
    /// </summary>
    public class BanditOutfitTests
    {
        // Sparse, and away from the colonist rows, so a bandit dealt a colonist's body shows.
        static ColonistCastPools Pools() => new ColonistCastPools(
            maleBodies: new[] { 3, 7, 11, 19 },
            femaleBodies: new[] { 4, 8, 12 },
            maleHair: new[] { 2, 5, 9, 14, 21 },
            femaleHair: new[] { 1, 6 },
            beards: new[] { 30, 31, 32 },
            uniformMale: 40, uniformFemale: 41,
            banditMale: new[] { 50, 51, 52 }, banditFemale: new[] { 53, 54, 55 },
            headgear: new[] { 2 });

        // ------------------------------------------------------------------ the person underneath

        [Test]
        public void ABanditIsThePersonTheSameSeedDealsAColonistDressed()
        {
            ColonistCastPools pools = Pools();
            for (int i = 1; i <= 200; i++)
            {
                foreach (char named in new[] { 'm', 'f', 'n' })
                {
                    ColonistAppearance person = ColonistAppearance.Of(7u, i, pools, named, 50);
                    ColonistAppearance bandit = ColonistAppearance.Of(7u, i, pools, named, 50, PawnOutfit.Bandit);

                    Assert.That(bandit.Skin, Is.EqualTo(person.Skin), $"pawn {i}: skin");
                    Assert.That(bandit.Hair, Is.EqualTo(person.Hair), $"pawn {i}: hair colour");
                    Assert.That(bandit.HairPiece, Is.EqualTo(person.HairPiece), $"pawn {i}: the helmet deleted the hair");
                    Assert.That(bandit.BeardPiece, Is.EqualTo(person.BeardPiece), $"pawn {i}: the helmet deleted the beard");
                }
            }
        }

        [Test]
        public void TheIssuedOutfitIsTheColonistExactly()
        {
            ColonistCastPools pools = Pools();
            for (int i = 1; i <= 100; i++)
                Assert.That(ColonistAppearance.Of(9u, i, pools, 'n', 30, PawnOutfit.Issued),
                    Is.EqualTo(ColonistAppearance.Of(9u, i, pools, 'n', 30)));
        }

        // ------------------------------------------------------------------ the clothes

        [Test]
        public void ABanditWearsTheGangsBodyForTheirSexAndNeverAColonists()
        {
            ColonistCastPools pools = Pools();
            var cuts = new HashSet<int>();
            for (int i = 1; i <= 200; i++)
            {
                ColonistAppearance man = ColonistAppearance.Of(11u, i, pools, 'm', 30, PawnOutfit.Bandit);
                ColonistAppearance woman = ColonistAppearance.Of(11u, i, pools, 'f', 30, PawnOutfit.Bandit);
                Assert.That(pools.BanditMale, Contains.Item(man.Look));
                Assert.That(pools.BanditFemale, Contains.Item(woman.Look));
                cuts.Add(man.Look);
                cuts.Add(woman.Look);
            }
            Assert.That(cuts, Has.Count.EqualTo(6), "every vest cut is dealt (owner: the cut varies)");
        }

        [Test]
        public void ABanditWearsARedVestBlackTrousersAndTheHelmet()
        {
            ColonistCastPools pools = Pools();
            var reds = new HashSet<Rgb24>();
            for (int i = 1; i <= 200; i++)
            {
                ColonistAppearance bandit = ColonistAppearance.Of(13u, i, pools, 'n', 30, PawnOutfit.Bandit);
                Assert.That(ColonistAppearance.BanditReds, Contains.Item(bandit.Cloth));
                Assert.That(bandit.Cloth2, Is.EqualTo(ColonistAppearance.BanditTrousers));
                Assert.That(bandit.HeadPiece, Is.EqualTo(2));
                Assert.That(bandit.HidesHair, Is.True);
                Assert.That(bandit.Outfit, Is.EqualTo(PawnOutfit.Bandit));
                reds.Add(bandit.Cloth);
            }
            Assert.That(reds, Has.Count.EqualTo(ColonistAppearance.BanditReds.Length), "every shade of the red is dealt");
        }

        /// <summary>Every shade still reads as red: red leads, and by a margin.</summary>
        [Test]
        public void EveryShadeOfTheVestIsRed()
        {
            foreach (Rgb24 c in ColonistAppearance.BanditReds)
            {
                Assert.That(c.R, Is.GreaterThan(2 * c.G), c.ToString());
                Assert.That(c.R, Is.GreaterThan(2 * c.B), c.ToString());
                Assert.That(c.R, Is.InRange(110, 190), c + ": too dark to read as red, or too bright to be worn");
            }
        }

        /// <summary>The vest cut and the red are rolled on streams of their own, not off the skin's.</summary>
        [Test]
        public void TheCutAndTheRedAreIndependentOfTheSkin()
        {
            ColonistCastPools pools = Pools();
            var pairs = new HashSet<(Rgb24, int)>();
            var redBySkin = new HashSet<(Rgb24, Rgb24)>();
            for (int i = 1; i <= 400; i++)
            {
                ColonistAppearance b = ColonistAppearance.Of(17u, i, pools, 'm', 30, PawnOutfit.Bandit);
                pairs.Add((b.Skin, b.Look));
                redBySkin.Add((b.Skin, b.Cloth));
            }
            int skins = ColonistPalette.Skin.Length;
            Assert.That(pairs.Count, Is.GreaterThan(skins * 2), "the vest cut follows the skin");
            Assert.That(redBySkin.Count, Is.GreaterThan(skins * 3), "the red follows the skin");
        }

        [Test]
        public void WithoutTheGangsRowsABanditIsStillRedAndBlack()
        {
            var bare = new ColonistCastPools(new[] { 0 }, new[] { 0 },
                System.Array.Empty<int>(), System.Array.Empty<int>(), System.Array.Empty<int>());
            ColonistAppearance b = ColonistAppearance.Of(3u, 5, bare, 'm', 30, PawnOutfit.Bandit);
            Assert.That(b.Look, Is.EqualTo(0));
            Assert.That(ColonistAppearance.BanditReds, Contains.Item(b.Cloth));
            Assert.That(b.HeadPiece, Is.EqualTo(ColonistAppearance.NoPiece), "no packs, no helmet, and the hair shows");
            Assert.That(b.HidesHair, Is.False);
        }

        // ------------------------------------------------------------------ the far form

        [Test]
        public void TheFarFormKnowsTheGangsBodiesAndNothingElse()
        {
            ColonistCastPools pools = Pools();
            foreach (int look in new[] { 50, 51, 52, 53, 54, 55 })
            {
                Assert.That(ColonistAppearance.BanditCloth(pools, look, out Rgb24 cloth, out Rgb24 cloth2), Is.True);
                Assert.That(cloth, Is.EqualTo(ColonistAppearance.BanditReds[0]));
                Assert.That(cloth2, Is.EqualTo(ColonistAppearance.BanditTrousers));
                Assert.That(ColonistAppearance.IssuedCloth(pools, look, out _, out _), Is.False);
            }
            foreach (int look in new[] { 3, 4, 40, 41 })
                Assert.That(ColonistAppearance.BanditCloth(pools, look, out _, out _), Is.False, $"look {look}");
        }

        [Test]
        public void AColonistIsNeverDealtTheGangsBody()
        {
            ColonistCastPools pools = Pools();
            foreach (int look in pools.BodiesFor('m')) Assert.That(pools.IsBanditBody(look), Is.False);
            foreach (int look in pools.BodiesFor('f')) Assert.That(pools.IsBanditBody(look), Is.False);
        }

        // ------------------------------------------------------------------ identity

        /// <summary>
        /// The portrait cache is keyed on the appearance, so a bandit and the same person in the
        /// uniform must not compare equal — the fault ColonistAppearance.Equals records for hair.
        /// </summary>
        [Test]
        public void TheOutfitAndTheHelmetArePartOfTheAppearancesIdentity()
        {
            var a = new ColonistAppearance(1, default, default, default, default, 2, 3, PawnOutfit.Issued, ColonistAppearance.NoPiece);
            var outfit = new ColonistAppearance(1, default, default, default, default, 2, 3, PawnOutfit.Bandit, ColonistAppearance.NoPiece);
            var head = new ColonistAppearance(1, default, default, default, default, 2, 3, PawnOutfit.Issued, 0);
            Assert.That(a, Is.Not.EqualTo(outfit));
            Assert.That(a, Is.Not.EqualTo(head));
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(outfit.GetHashCode()));
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(head.GetHashCode()));
        }

        [Test]
        public void TheBookDressesByTheOutfitAndForgetsNothingAboutThePerson()
        {
            var book = new ColonistAppearanceBook(1u, 60, Pools());
            ColonistAppearance bandit = book.For(9, 555u, PawnOutfit.Bandit);
            ColonistAppearance colonist = book.For(9, 555u, PawnOutfit.Issued);
            Assert.That(bandit.Outfit, Is.EqualTo(PawnOutfit.Bandit), "the cache answered the other outfit");
            Assert.That(colonist.Outfit, Is.EqualTo(PawnOutfit.Issued), "the cache answered the other outfit");
            Assert.That(colonist.HairPiece, Is.EqualTo(bandit.HairPiece));
            Assert.That(book.For(9, 555u, PawnOutfit.Bandit), Is.EqualTo(bandit));
        }

        // ------------------------------------------------------------------ who wears what

        [Test]
        public void AHostilePersonDressesAsABanditAndNobodyElseDoes()
        {
            Assert.That(PawnOutfits.For(PawnFlags.Person | PawnFlags.Hostile), Is.EqualTo(PawnOutfit.Bandit));
            Assert.That(PawnOutfits.For(PawnFlags.Person), Is.EqualTo(PawnOutfit.Issued));
            Assert.That(PawnOutfits.For(PawnFlags.None), Is.EqualTo(PawnOutfit.Issued), "an animal");
            Assert.That(PawnOutfits.For(PawnFlags.Hostile), Is.EqualTo(PawnOutfit.Issued), "a hostile animal is not a bandit");
        }

        [Test]
        public void ACorpseKeepsTheOutfitItDiedIn()
        {
            var corpse = new CorpseView(9, new PawnId(13), 3, 44u, new CellRef(7, 6, 2), 0, 0,
                PawnFlags.Person | PawnFlags.Hostile);
            Assert.That(PawnOutfits.For(corpse), Is.EqualTo(PawnOutfit.Bandit));
        }
    }
}
