#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The colourway an animal wears (design 66 §4): dealt from its id alone, so the figure and
    /// the far form cannot disagree, in range for every count the pack has, and spread across them.
    /// </summary>
    public class AnimalLooksTests
    {
        [Test]
        public void TheSameAnimalAlwaysWearsTheSameCoat()
        {
            for (int id = 1; id < 200; id++)
                Assert.That(AnimalLooks.Colourway(id, 3), Is.EqualTo(AnimalLooks.Colourway(id, 3)), $"pawn {id}");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryCoatIsOneThePackHas(int count)
        {
            for (int id = 0; id < 1000; id++)
            {
                int coat = AnimalLooks.Colourway(id, count);
                Assert.That(coat, Is.InRange(0, count - 1), $"pawn {id} of {count}");
            }
        }

        [Test]
        public void ASingleCoatIsAlwaysTheFirst()
        {
            Assert.That(AnimalLooks.Colourway(17, 1), Is.EqualTo(0));
            Assert.That(AnimalLooks.Colourway(17, 0), Is.EqualTo(0), "a count of nothing does not divide by zero");
        }

        /// <summary>
        /// Consecutive ids — a herd seeded together — are spread over every coat rather than
        /// dressed alike. The raccoon has two usable coats (Raccoon_03 repeats Raccoon_01, e-15),
        /// the rest three.
        /// </summary>
        [TestCase(2)]
        [TestCase(3)]
        public void AHerdIsSpreadAcrossTheCoats(int count)
        {
            var seen = new int[count];
            for (int id = 100; id < 400; id++) seen[AnimalLooks.Colourway(id, count)]++;
            for (int coat = 0; coat < count; coat++)
                Assert.That(seen[coat], Is.GreaterThan(300 / count / 2),
                    $"coat {coat} of {count} dealt {seen[coat]} times in 300");

            // And a run of neighbours is rarely one coat: a sounder of five in a row nearly always
            // shows two. Chance alone makes five alike 1 in 16 for two coats, so this asks for
            // what a hash that did not correlate with the id would give, not for never.
            int runs = 0, varied = 0;
            for (int start = 1; start < 400; start += 5)
            {
                runs++;
                int first = AnimalLooks.Colourway(start, count);
                for (int id = start + 1; id < start + 5; id++)
                    if (AnimalLooks.Colourway(id, count) != first) { varied++; break; }
            }
            Assert.That(varied, Is.GreaterThanOrEqualTo(runs * 8 / 10),
                $"{runs - varied} of {runs} runs of five neighbours wore one coat of {count}");
        }
    }
}
