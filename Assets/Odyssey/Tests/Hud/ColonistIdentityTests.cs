#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A colonist's age and trade, and the alphabetical skill order both grids read (world setup,
    /// 2026-09-17).
    ///
    /// <para><b>The load-bearing claim is that none of this is stored.</b> Age and occupation are
    /// derived from the pawn's roll seed, so they must be stable for a given seed forever — a
    /// formula that drifts would silently re-age and re-employ every colonist in every existing
    /// save. There is no save format to catch that, so a fingerprint does.</para>
    /// </summary>
    public class ColonistIdentityTests
    {
        // ------------------------------------------------------------------ age

        /// <summary>
        /// <b>Eighteen and over, always</b> — the owner's instruction, and the one property here
        /// that something downstream will depend on. Checked across the whole seed space rather
        /// than on a handful, because an off-by-one at the bottom of the range is exactly the kind
        /// of thing a spot check misses.
        /// </summary>
        [Test]
        public void NobodyIsUnderEighteenOrOverSixtyFive()
        {
            for (uint seed = 0; seed < 2000; seed++)
            for (int id = 1; id <= 3; id++)
            {
                int age = ColonistIdentity.Age(seed, new PawnId(id));
                Assert.That(age, Is.InRange(ColonistIdentity.MinimumAge, ColonistIdentity.MaximumAge),
                    $"seed {seed}, colonist {id} is {age}");
            }
        }

        /// <summary>
        /// The whole range is reachable. A formula that only ever answered thirty-four would pass
        /// the bounds test above and be plainly wrong.
        /// </summary>
        [Test]
        public void EveryAgeInTheRangeComesUp()
        {
            var seen = new HashSet<int>();
            for (uint seed = 0; seed < 20000; seed++) seen.Add(ColonistIdentity.Age(seed, new PawnId(1)));

            int span = ColonistIdentity.MaximumAge - ColonistIdentity.MinimumAge + 1;
            Assert.That(seen.Count, Is.EqualTo(span), "some ages are never dealt");
        }

        [Test]
        public void TheSameSeedIsAlwaysTheSameAge()
        {
            Assert.That(ColonistIdentity.Age(4242u, new PawnId(1)),
                Is.EqualTo(ColonistIdentity.Age(4242u, new PawnId(1))));
        }

        // ------------------------------------------------------------------ occupation

        [Test]
        public void TheOccupationsAreTheRegistrysAndThereAreNinetyTwo()
        {
            Assert.That(ColonistIdentity.Occupations.Count, Is.EqualTo(92),
                "the owner's list is ninety-two; the CSV and this disagree");

            foreach (string key in ColonistIdentity.Occupations)
            {
                Assert.That(key, Does.StartWith(ColonistIdentity.OccupationPrefix));
                Assert.That(Registry.Labels, Does.ContainKey(key));
            }
        }

        [Test]
        public void EverybodyHasATradeAndItIsOneOfTheRegistrys()
        {
            for (uint seed = 0; seed < 2000; seed++)
            {
                string key = ColonistIdentity.OccupationKey(seed, new PawnId(1));
                Assert.That(ColonistIdentity.Occupations, Does.Contain(key));
                Assert.That(ColonistIdentity.Occupation(seed, new PawnId(1)), Is.Not.Empty);
            }
        }

        /// <summary>
        /// Rerolling walks somewhere, not one step down the alphabet. Without the avalanche a
        /// player pressing Reroll would watch the occupation list go by in order, which reads as a
        /// list being stepped through rather than as a new person arriving.
        /// </summary>
        [Test]
        public void ConsecutiveSeedsAreNotConsecutiveTrades()
        {
            int adjacent = 0;
            for (uint seed = 0; seed < 500; seed++)
            {
                int a = IndexOfTrade(seed);
                int b = IndexOfTrade(seed + 1);
                if (b == a + 1) adjacent++;
            }

            Assert.That(adjacent, Is.LessThan(25),
                "the occupation walks down the list with the seed, so a reroll steps rather than jumps");
        }

        static int IndexOfTrade(uint seed)
        {
            string key = ColonistIdentity.OccupationKey(seed, new PawnId(1));
            for (int i = 0; i < ColonistIdentity.Occupations.Count; i++)
                if (ColonistIdentity.Occupations[i] == key) return i;
            return -1;
        }

        /// <summary>
        /// Age and trade come from different streams, so they do not move together. Sharing one
        /// would make every colonist of a given trade the same age, and nobody would notice until
        /// the colony looked odd.
        /// </summary>
        [Test]
        public void AgeAndTradeAreNotTheSameDraw()
        {
            var pairs = new HashSet<string>();
            for (uint seed = 0; seed < 500; seed++)
                pairs.Add(ColonistIdentity.Age(seed, new PawnId(1)) + "/" + IndexOfTrade(seed));

            Assert.That(pairs.Count, Is.GreaterThan(400),
                "age and occupation move together, so they are drawn from one stream");
        }

        // ------------------------------------------------------------------ the fingerprint

        /// <summary>
        /// <b>The guard on everything above.</b> Age and occupation are derived and stored nowhere,
        /// so no save, no hash and no golden would notice the formula changing — every colonist in
        /// every existing colony would quietly become somebody else. This folds the first hundred
        /// seeds into one number.
        ///
        /// <para>A deliberate change to either formula is one line here and a sentence in the
        /// commit. An accidental one fails.</para>
        /// </summary>
        [Test]
        public void TheDerivationIsPinned()
        {
            unchecked
            {
                ulong fingerprint = 14695981039346656037UL;
                for (uint seed = 0; seed < 100; seed++)
                for (int id = 1; id <= 3; id++)
                {
                    var who = new PawnId(id);
                    fingerprint = (fingerprint ^ (ulong)ColonistIdentity.Age(seed, who)) * 1099511628211UL;
                    fingerprint = (fingerprint ^ (ulong)IndexOf(ColonistIdentity.OccupationKey(seed, who)))
                                  * 1099511628211UL;
                }

                Assert.That(fingerprint, Is.EqualTo(7510490284011483811UL),
                    "the age or occupation formula moved. Every colonist in every existing colony " +
                    "just became somebody else; if that was deliberate, re-bake this literal and " +
                    "say so in the commit message.");
            }
        }

        static int IndexOf(string key)
        {
            for (int i = 0; i < ColonistIdentity.Occupations.Count; i++)
                if (ColonistIdentity.Occupations[i] == key) return i;
            return -1;
        }

        // ------------------------------------------------------------------ alphabetical skills

        /// <summary>
        /// The order both grids read, by the word on screen (owner, 2026-09-17). The inspect pane's
        /// Skills tab is not alphabetical today; this is the change that makes it so, and the same
        /// order feeds a candidate's card.
        /// </summary>
        [Test]
        public void SkillsAreAlphabeticalByTheWordOnScreen()
        {
            IReadOnlyList<SkillCatalogue.Entry> order = SkillCatalogue.Alphabetical;

            Assert.That(order.Count, Is.EqualTo(SkillCatalogue.All.Length), "a skill was lost sorting");

            for (int i = 1; i < order.Count; i++)
            {
                string before = Registry.Label(order[i - 1].Key);
                string after = Registry.Label(order[i].Key);
                Assert.That(string.CompareOrdinal(before, after), Is.LessThan(0),
                    $"'{before}' is drawn before '{after}'");
            }

            Assert.That(Registry.Label(order[0].Key), Is.EqualTo("Animals"));
        }

        /// <summary>
        /// Down the left column, then down the right. Every skill appears exactly once across the
        /// two, which is the half of this that would break silently — a column that dropped one
        /// would simply not draw it.
        /// </summary>
        [Test]
        public void TheTwoColumnsReadDownwardsAndHoldEverySkillOnce()
        {
            IReadOnlyList<SkillCatalogue.Entry> left = SkillCatalogue.Column(0);
            IReadOnlyList<SkillCatalogue.Entry> right = SkillCatalogue.Column(1);

            Assert.That(left.Count, Is.EqualTo(SkillCatalogue.Rows));
            Assert.That(left.Count + right.Count, Is.EqualTo(SkillCatalogue.All.Length),
                "a skill is in neither column, or in both");

            var seen = new HashSet<string>();
            foreach (SkillCatalogue.Entry entry in left)
                Assert.That(seen.Add(entry.Key), Is.True, entry.Key + " is drawn twice");
            foreach (SkillCatalogue.Entry entry in right)
                Assert.That(seen.Add(entry.Key), Is.True, entry.Key + " is drawn twice");

            // The left column runs A to the middle; the right takes up where it left off.
            Assert.That(string.CompareOrdinal(
                    Registry.Label(left[left.Count - 1].Key), Registry.Label(right[0].Key)),
                Is.LessThan(0), "the columns are not one alphabetical run split in half");
        }
    }
}
