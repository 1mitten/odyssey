#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Gender reaches the code (MC1, <c>docs/design/29-modular-colonists.md</c> §6).
    ///
    /// <para>It is the column of <c>colonist-names.csv</c> that decides which bodies and which hair
    /// a colonist may be dealt, so the thing worth pinning is not the values — the generator's
    /// <c>--check</c> gate owns those — but the two properties that would be silent if they broke:
    /// that the gender is the gender of the row the <i>name</i> came from, and that renaming
    /// somebody does not reshape them.</para>
    /// </summary>
    public class ColonistGenderTests
    {
        [TearDown]
        public void ForgetNames() => ColonistNames.Book.Clear();

        [Test]
        public void EveryNameHasAGender()
        {
            Assert.That(ColonistNamePool.Genders.Length,
                Is.EqualTo(ColonistNamePool.Names.Length),
                "the two generated arrays are indexed by the same number and must be the same length");
        }

        [Test]
        public void EveryGenderIsOneOfTheThreeTheCsvAllows()
        {
            foreach (char g in ColonistNamePool.Genders)
                Assert.That(g, Is.AnyOf('m', 'f', 'n'));
        }

        [Test]
        public void AllThreeGendersAreActuallyInThePool()
        {
            // 'n' in particular: it is easy to write a rule that treats it as a fallback nobody
            // ever has, and the pool has real neutral names in it.
            var seen = new HashSet<char>(ColonistNamePool.Genders);
            Assert.That(seen, Is.EquivalentTo(new[] { 'm', 'f', 'n' }));
        }

        [Test]
        public void TheGenderIsTheGenderOfTheNameThatWasDealt()
        {
            // The property that matters: walk a colony and check every colonist's gender against
            // the pool row their own name came from. A second copy of the index arithmetic would
            // pass its own test and disagree with this one.
            for (uint seed = 1; seed <= 40; seed++)
            {
                for (int i = 1; i <= 30; i++)
                {
                    var id = new PawnId(i);
                    string name = ColonistNames.Rolled(seed, id);
                    char gender = ColonistNames.GenderOf(seed, id);

                    int row = System.Array.IndexOf(ColonistNamePool.Names, name);
                    Assert.That(row, Is.GreaterThanOrEqualTo(0), $"'{name}' is not in the pool");
                    Assert.That(gender, Is.EqualTo(ColonistNamePool.Genders[row]),
                        $"seed {seed}, pawn {i}: '{name}' is row {row}");
                }
            }
        }

        [Test]
        public void RenamingAColonistDoesNotChangeTheirGender()
        {
            var id = new PawnId(3);
            char before = ColonistNames.GenderOf(7u, id);

            ColonistNames.Book.Rename(id, "Mx Somebody Else");

            Assert.That(ColonistNames.Of(7u, id), Is.EqualTo("Mx Somebody Else"),
                "the rename should have taken");
            Assert.That(ColonistNames.GenderOf(7u, id), Is.EqualTo(before),
                "renaming somebody must not reshape their body");
        }

        [Test]
        public void NobodyIsNeutral()
        {
            Assert.That(ColonistNames.GenderOf(1u, PawnId.None), Is.EqualTo('n'));
        }
    }
}
