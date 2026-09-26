#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Gendered pools, hair, beards and greying (MC3,
    /// <c>docs/design/29-modular-colonists.md</c> §6).
    ///
    /// <para>The property this file exists for is the first one: <b>a gendered pool is a filter
    /// over catalogue family indices and never a re-indexing.</b> Compacting the survivors and
    /// returning a position is the fault <see cref="ColonistAppearanceBook"/> records — look
    /// <c>i</c> stops being row <c>i</c>, and nothing notices until a colony crosses the figure
    /// cap and everybody changes face at once.</para>
    /// </summary>
    public class ColonistCastPoolTests
    {
        // Deliberately sparse and deliberately not starting at zero: if anything anywhere returns
        // a position in the pool rather than the family index, these numbers make it obvious.
        static ColonistCastPools Pools() => new ColonistCastPools(
            maleBodies: new[] { 3, 7, 11, 19 },
            femaleBodies: new[] { 4, 8, 12 },
            maleHair: new[] { 2, 5, 9, 14, 21 },
            femaleHair: new[] { 1, 6 },
            beards: new[] { 30, 31, 32 });

        // ------------------------------------------------------------------ the index space

        [Test]
        public void ABodyIsAlwaysAFamilyIndexFromTheGendersOwnPool()
        {
            ColonistCastPools pools = Pools();
            for (int i = 1; i <= 200; i++)
            {
                ColonistAppearance man = ColonistAppearance.Of(42u, i, pools, 'm', 30);
                ColonistAppearance woman = ColonistAppearance.Of(42u, i, pools, 'f', 30);

                Assert.That(pools.MaleBodies, Contains.Item(man.Look));
                Assert.That(pools.FemaleBodies, Contains.Item(woman.Look));
            }
        }

        [Test]
        public void ANeutralNameDrawsFromBothPools()
        {
            ColonistCastPools pools = Pools();
            var seen = new HashSet<int>();
            for (int i = 1; i <= 400; i++)
                seen.Add(ColonistAppearance.Of(9u, i, pools, 'n', 30).Look);

            // Both sides must actually turn up, or 'n' has quietly become 'm'.
            Assert.That(seen.Intersect(pools.MaleBodies), Is.Not.Empty);
            Assert.That(seen.Intersect(pools.FemaleBodies), Is.Not.Empty);
        }

        [Test]
        public void EveryBodyInThePoolIsReachable()
        {
            // A lottery that can never deal one of its options is a lottery with a bug in its
            // modulus, and the symptom is a body nobody ever sees.
            ColonistCastPools pools = Pools();
            var seen = new HashSet<int>();
            for (int i = 1; i <= 500; i++)
                seen.Add(ColonistAppearance.Of(5u, i, pools, 'm', 30).Look);

            Assert.That(seen, Is.EquivalentTo(pools.MaleBodies));
        }

        // ------------------------------------------------------------------ hair and beards

        [Test]
        public void HairComesFromTheGendersOwnPoolOrIsNone()
        {
            ColonistCastPools pools = Pools();
            for (int i = 1; i <= 200; i++)
            {
                int man = ColonistAppearance.Of(11u, i, pools, 'm', 30).HairPiece;
                int woman = ColonistAppearance.Of(11u, i, pools, 'f', 30).HairPiece;

                Assert.That(man == ColonistAppearance.NoPiece || pools.MaleHair.Contains(man));
                Assert.That(pools.FemaleHair, Contains.Item(woman),
                    "a woman is never dealt no hair");
            }
        }

        [Test]
        public void AWomanIsNeverDealtABeard()
        {
            ColonistCastPools pools = Pools();
            for (int i = 1; i <= 300; i++)
                Assert.That(ColonistAppearance.Of(3u, i, pools, 'f', 30).BeardPiece,
                    Is.EqualTo(ColonistAppearance.NoPiece));
        }

        [Test]
        public void SomeMenHaveBeardsAndMostDoNot()
        {
            ColonistCastPools pools = Pools();
            int bearded = 0;
            const int n = 1000;
            for (int i = 1; i <= n; i++)
                if (ColonistAppearance.Of(77u, i, pools, 'm', 30).BeardPiece != ColonistAppearance.NoPiece)
                    bearded++;

            // 45% by the derivation. The band is wide because this pins the intent -- a beard
            // reads as that person's choice rather than as the house style -- not the constant.
            Assert.That(bearded, Is.InRange(n * 30 / 100, n * 60 / 100));
        }

        [Test]
        public void EveryBeardInThePoolIsReachable()
        {
            ColonistCastPools pools = Pools();
            var seen = new HashSet<int>();
            for (int i = 1; i <= 1000; i++)
            {
                int beard = ColonistAppearance.Of(21u, i, pools, 'm', 30).BeardPiece;
                if (beard != ColonistAppearance.NoPiece) seen.Add(beard);
            }

            Assert.That(seen, Is.EquivalentTo(pools.Beards));
        }

        [Test]
        public void BaldnessIsMenOnlyAndRisesWithAge()
        {
            ColonistCastPools pools = Pools();
            int young = Bald(pools, 20), old = Bald(pools, 65);

            Assert.That(young, Is.GreaterThan(0), "a young man may still be bald");
            Assert.That(old, Is.GreaterThan(young * 2),
                "baldness must rise with age, or age is not readable on the body");
        }

        static int Bald(ColonistCastPools pools, int age)
        {
            int n = 0;
            for (int i = 1; i <= 2000; i++)
                if (ColonistAppearance.Of(13u, i, pools, 'm', age).HairPiece == ColonistAppearance.NoPiece)
                    n++;
            return n;
        }

        // ------------------------------------------------------------------ greying

        [Test]
        public void HairDoesNotGreyBeforeFortyFive()
        {
            Assert.That(ColonistAppearance.GreyingAt(18), Is.Zero);
            Assert.That(ColonistAppearance.GreyingAt(45), Is.Zero);
            Assert.That(ColonistAppearance.GreyingAt(46), Is.GreaterThan(0));
        }

        [Test]
        public void TheOldestColonistIsGreyingButNotWhite()
        {
            int oldest = ColonistAppearance.GreyingAt(ColonistIdentity.MaximumAge);
            Assert.That(oldest, Is.InRange(40, 80),
                "a colony whose eldest are snow-white reads as a retirement home");
        }

        [Test]
        public void AnOlderColonistHasGreyerHairThanTheSamePersonYoung()
        {
            ColonistCastPools pools = Pools();
            Rgb24 young = ColonistAppearance.Of(8u, 4, pools, 'm', 25).Hair;
            Rgb24 old = ColonistAppearance.Of(8u, 4, pools, 'm', 65).Hair;

            Assert.That(old, Is.Not.EqualTo(young));

            // Greyer means nearer the grey it mixes towards, whichever way each channel moved.
            int Distance(Rgb24 c) =>
                System.Math.Abs(c.R - 0xBF) + System.Math.Abs(c.G - 0xBC) + System.Math.Abs(c.B - 0xB6);
            Assert.That(Distance(old), Is.LessThan(Distance(young)));
        }

        // ------------------------------------------------------------------ degrading

        [Test]
        public void WithNoPacksEverybodyIsBaldAndCleanShavenAndNobodyBreaks()
        {
            for (int i = 1; i <= 50; i++)
            {
                ColonistAppearance a = ColonistAppearance.Of(2u, i, ColonistCastPools.Empty, 'm', 40);
                Assert.That(a.Look, Is.Zero);
                Assert.That(a.HairPiece, Is.EqualTo(ColonistAppearance.NoPiece));
                Assert.That(a.BeardPiece, Is.EqualTo(ColonistAppearance.NoPiece));
            }
        }

        // ------------------------------------------------------------------ a neutral name

        [Test]
        public void ANeutralNameIsDealtOneSexAndIsCoherent()
        {
            // The fault this replaced: 'n' meant "both pools" slot by slot, so a neutral name
            // could be given a female body and still grow a beard -- and once a uniform was
            // issued it meant "male", so all thirty unisex names were men in every colony.
            ColonistCastPools pools = Pools();
            for (int i = 1; i <= 400; i++)
            {
                char sex = ColonistAppearance.SexOf('n', 6u, i);
                ColonistAppearance a = ColonistAppearance.Of(6u, i, pools, 'n', 30);

                if (sex == 'f')
                {
                    Assert.That(pools.FemaleBodies, Contains.Item(a.Look));
                    Assert.That(a.BeardPiece, Is.EqualTo(ColonistAppearance.NoPiece),
                        "a colonist dealt a female body must not also be dealt a beard");
                }
                else
                {
                    Assert.That(pools.MaleBodies, Contains.Item(a.Look));
                }
            }
        }

        [Test]
        public void ANeutralNameIsDealtBothSexesAcrossAColony()
        {
            // Still "both pools" at the population level -- just not both at once.
            int women = 0;
            for (int i = 1; i <= 400; i++)
                if (ColonistAppearance.SexOf('n', 3u, i) == 'f')
                    women++;

            Assert.That(women, Is.InRange(120, 280), "a coin that never lands on one side");
        }

        [Test]
        public void ANameThatCarriesASexKeepsIt()
        {
            for (int i = 1; i <= 100; i++)
            {
                Assert.That(ColonistAppearance.SexOf('m', 8u, i), Is.EqualTo('m'));
                Assert.That(ColonistAppearance.SexOf('f', 8u, i), Is.EqualTo('f'));
            }
        }

        [Test]
        public void TheSexOfANeutralNameSurvivesEverything()
        {
            // Pure in the pair, like the name and the age beside it, so the person on the setup
            // card is the person on the board and the person after a reload.
            for (int i = 1; i <= 100; i++)
                Assert.That(ColonistAppearance.SexOf('n', 99u, i),
                    Is.EqualTo(ColonistAppearance.SexOf('n', 99u, i)));
        }

        // ------------------------------------------------------------------ the uniform

        static ColonistCastPools Issued() => new ColonistCastPools(
            maleBodies: new[] { 3, 7, 11, 19 },
            femaleBodies: new[] { 4, 8, 12 },
            maleHair: new[] { 2, 5, 9, 14, 21 },
            femaleHair: new[] { 1, 6 },
            beards: new[] { 30, 31, 32 },
            uniformMale: 40, uniformFemale: 41);

        [Test]
        public void AColonyWithAUniformPutsEverybodyInIt()
        {
            ColonistCastPools pools = Issued();
            for (int i = 1; i <= 200; i++)
            {
                Assert.That(ColonistAppearance.Of(5u, i, pools, 'm', 30).Look, Is.EqualTo(40));
                Assert.That(ColonistAppearance.Of(5u, i, pools, 'f', 30).Look, Is.EqualTo(41));
            }
        }

        [Test]
        public void TheUniformIsTheSameColourOnEverybody()
        {
            // A uniform whose colour varied per colonist is not a uniform. Both garments, because
            // the trim is rolled separately from the main cloth and would drift on its own.
            ColonistCastPools pools = Issued();
            for (int i = 1; i <= 200; i++)
            {
                ColonistAppearance a = ColonistAppearance.Of(9u, i, pools, 'n', 40);
                Assert.That(a.Cloth, Is.EqualTo(ColonistAppearance.UniformCloth));
                Assert.That(a.Cloth2, Is.EqualTo(ColonistAppearance.UniformTrim));
            }
        }

        [Test]
        public void TheBodyAColonistIsIssuedSaysTheColourTheyWear()
        {
            // The far form asks the body, not the person, which colour to draw. It is one
            // material per body. For that to be the colonist the figure draws, every colonist
            // dealt a uniform body must be wearing exactly what the body says is issued. When the
            // far form asked nobody, it drew the pack's own orange (2026-09-24).
            ColonistCastPools pools = Issued();
            foreach (char gender in new[] { 'm', 'f', 'n' })
                for (int i = 1; i <= 200; i++)
                {
                    ColonistAppearance a = ColonistAppearance.Of(13u, i, pools, gender, 30);
                    Assert.That(ColonistAppearance.IssuedCloth(pools, a.Look, out Rgb24 cloth, out Rgb24 cloth2),
                        Is.True, $"body {a.Look} was issued to pawn {i} but does not say so");
                    Assert.That(cloth, Is.EqualTo(a.Cloth));
                    Assert.That(cloth2, Is.EqualTo(a.Cloth2));
                }
        }

        [Test]
        public void NoBodyButTheUniformClaimsAColour()
        {
            // Every other colour is rolled per colonist, so a body that claimed one would put the
            // whole far crowd in it. Negative control: without a uniform, the uniform's own
            // index claims nothing either.
            ColonistCastPools pools = Issued();
            foreach (int body in new[] { 3, 7, 11, 19, 4, 8, 12, 0 })
                Assert.That(ColonistAppearance.IssuedCloth(pools, body, out _, out _), Is.False, $"body {body}");
            Assert.That(ColonistAppearance.IssuedCloth(pools, ColonistCastPools.NoUniform, out _, out _), Is.False);

            ColonistCastPools plain = ColonistCastPools.AllBodies(48);
            for (int body = 0; body < 48; body++)
                Assert.That(ColonistAppearance.IssuedCloth(plain, body, out _, out _), Is.False, $"body {body}");

            var femaleOnly = new ColonistCastPools(new[] { 3 }, new[] { 4 },
                System.Array.Empty<int>(), System.Array.Empty<int>(), System.Array.Empty<int>(),
                uniformFemale: 41);
            Assert.That(ColonistAppearance.IssuedCloth(femaleOnly, ColonistCastPools.NoUniform, out _, out _),
                Is.False, "an absent male uniform is not a body everyone wears");
            Assert.That(ColonistAppearance.IssuedCloth(femaleOnly, 41, out _, out _), Is.True);
        }

        [Test]
        public void TheUniformIsNotPureWhiteButIsCloseToIt()
        {
            // Pure white has nowhere to go under the grading and reads as a hole in the frame.
            Rgb24 c = ColonistAppearance.UniformCloth;
            Assert.That(c.R, Is.GreaterThan(0xD0), "it should still read as white");
            Assert.That(c.B, Is.GreaterThan(c.R), "and carry a slight blue tint");
            Assert.That(c.Packed, Is.Not.EqualTo(0xFFFFFFu));
        }

        [Test]
        public void ColonistsInAUniformStillTellThemselvesApart()
        {
            // The whole argument for a uniform: identity moves onto the face. If skin and hair
            // stopped varying too, a colony would be a row of clones.
            ColonistCastPools pools = Issued();
            var skins = new HashSet<uint>();
            var hairs = new HashSet<uint>();
            var pieces = new HashSet<int>();
            for (int i = 1; i <= 400; i++)
            {
                ColonistAppearance a = ColonistAppearance.Of(11u, i, pools, 'm', 30);
                skins.Add(a.Skin.Packed);
                hairs.Add(a.Hair.Packed);
                pieces.Add(a.HairPiece);
            }

            Assert.That(skins.Count, Is.GreaterThan(1));
            Assert.That(hairs.Count, Is.GreaterThan(1));
            Assert.That(pieces.Count, Is.GreaterThan(1));
        }

        [Test]
        public void TakingTheUniformOffGivesBackTheCastThatWouldHaveBeenDealt()
        {
            // The uniform is applied after the rolls, not instead of them, so every stream is
            // consumed in the same order either way. That is what lets clothing-as-equipment take
            // it off later without re-shuffling the whole colony (docs/design/29 section 9).
            ColonistCastPools issued = Issued();
            ColonistCastPools bare = Pools();

            for (int i = 1; i <= 100; i++)
            {
                ColonistAppearance withUniform = ColonistAppearance.Of(21u, i, issued, 'm', 30);
                ColonistAppearance without = ColonistAppearance.Of(21u, i, bare, 'm', 30);

                Assert.That(withUniform.Skin, Is.EqualTo(without.Skin));
                Assert.That(withUniform.Hair, Is.EqualTo(without.Hair));
                Assert.That(withUniform.HairPiece, Is.EqualTo(without.HairPiece));
                Assert.That(withUniform.BeardPiece, Is.EqualTo(without.BeardPiece));
            }
        }

        // ------------------------------------------------------------------ equality

        [Test]
        public void TwoColonistsDifferingOnlyInHairAreNotTheSamePerson()
        {
            // PortraitStudio caches a photograph keyed on the appearance -- that is its whole
            // performance argument. When hair and beards were added and Equals was not, every
            // colonist with the same body and colours collapsed onto one cache entry and the
            // first one photographed lent its face to all of them. The symptom was a contact
            // sheet on which fifteen different hair pieces drew the identical frame.
            var a = new ColonistAppearance(1, Skin, Hair, Cloth, Cloth2, 3, 2);
            var b = new ColonistAppearance(1, Skin, Hair, Cloth, Cloth2, 4, 2);
            var c = new ColonistAppearance(1, Skin, Hair, Cloth, Cloth2, 3, 5);
            var same = new ColonistAppearance(1, Skin, Hair, Cloth, Cloth2, 3, 2);

            Assert.That(a, Is.Not.EqualTo(b), "a different hair piece is a different person");
            Assert.That(a, Is.Not.EqualTo(c), "a different beard is a different person");
            Assert.That(a, Is.EqualTo(same));
            Assert.That(a.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        }

        [Test]
        public void ADictionaryKeyedOnAppearanceTellsThePiecesApart()
        {
            // The property the cache actually needs, asserted the way the cache uses it.
            var seen = new HashSet<ColonistAppearance>();
            for (int piece = 0; piece < 15; piece++)
                seen.Add(new ColonistAppearance(1, Skin, Hair, Cloth, Cloth2, piece,
                    ColonistAppearance.NoPiece));

            Assert.That(seen.Count, Is.EqualTo(15));
        }

        static readonly Rgb24 Skin = Rgb24.FromHex(0xE0B088);
        static readonly Rgb24 Hair = Rgb24.FromHex(0x3B2A1E);
        static readonly Rgb24 Cloth = Rgb24.FromHex(0x4A4F55);
        static readonly Rgb24 Cloth2 = Rgb24.FromHex(0x2A2E33);

        [Test]
        public void TheSameSeedAndPawnAlwaysDealTheSamePerson()
        {
            ColonistCastPools pools = Pools();
            for (int i = 1; i <= 100; i++)
            {
                ColonistAppearance a = ColonistAppearance.Of(1234u, i, pools, 'm', 50);
                ColonistAppearance b = ColonistAppearance.Of(1234u, i, pools, 'm', 50);
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.HairPiece, Is.EqualTo(b.HairPiece));
                Assert.That(a.BeardPiece, Is.EqualTo(b.BeardPiece));
            }
        }

        [Test]
        public void HairAndBeardAreNotCorrelatedWithTheBody()
        {
            // The failure this guards is the one the four original streams were given their own
            // mixing constants for: take one hash modulo several lengths and everybody with a
            // given body has the same beard.
            ColonistCastPools pools = Pools();
            var pairs = new HashSet<(int, int)>();
            for (int i = 1; i <= 500; i++)
            {
                ColonistAppearance a = ColonistAppearance.Of(64u, i, pools, 'm', 30);
                if (a.BeardPiece != ColonistAppearance.NoPiece) pairs.Add((a.Look, a.BeardPiece));
            }

            // Four bodies and three beards: if they were correlated there would be at most four
            // distinct pairs, not the dozen an independent draw produces.
            Assert.That(pairs.Count, Is.GreaterThan(pools.MaleBodies.Length));
        }
    }
}
