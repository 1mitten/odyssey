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
