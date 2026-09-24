#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The bands design 41 §4.3 promises, measured off <see cref="ColonistDraw.Roll(uint, int, RollProfile)"/>
    /// itself rather than off the arithmetic the design was written from. The owner chose the
    /// Gamble on these numbers, so they are the contract and this is what holds it: a retune of
    /// the tables that drifts outside them fails here, and the doc quotes what this prints.
    /// </summary>
    [Category("Long")]
    public class DrawDistributionTests
    {
        const int Draws = 100_000;

        static int BudgetTotal(Pawn p, out int best)
        {
            var content = ContentPack.Pawns();
            int total = 0;
            best = 0;
            for (int s = 0; s < SkillIndex.Count; s++)
            {
                if (content.Skills[s].outsideBudget) continue;
                int level = p.SkillLevel(s);
                total += level;
                if (level > best) best = level;
            }

            return total;
        }

        [Test]
        public void TheGambleKeepsThePromisesTheOwnerChoseItOn()
        {
            long sum = 0;
            int dud = 0, worse = 0, better = 0, star = 0, extreme = 0;
            int standardTotal = ContentPack.Pawns().Kind.standardSkillBudget;
            for (int i = 0; i < Draws; i++)
            {
                Pawn p = ColonistDraw.Roll(Seed(i), i % 3, RollProfile.Gamble);
                int total = BudgetTotal(p, out int best);
                sum += total;
                if (total <= 6) dud++;
                if (total < standardTotal) worse++;
                if (total > standardTotal) better++;
                if (best >= 12) star++;
                foreach (int t in p.Traits)
                    if (ContentPack.Pawns().Traits[t].pool == TraitPool.Extreme) { extreme++; break; }
            }

            double mean = (double)sum / Draws;
            TestContext.Out.WriteLine(
                $"Gamble over {Draws:N0} pulls: skill total mean {mean:F2} (Standard {standardTotal}, " +
                $"+{(mean / standardTotal - 1) * 100:F0}%), dud {Pct(dud)}, worse {Pct(worse)}, better {Pct(better)}, " +
                $"a 12+ skill {Pct(star)}, an extreme trait {Pct(extreme)}");

            Assert.That(mean / standardTotal, Is.InRange(1.20, 1.45), "Gamble is no longer better on average by about a third");
            Assert.That(Share(dud), Is.InRange(0.05, 0.13), "the dud rate left its band");
            Assert.That(Share(worse), Is.InRange(0.22, 0.38), "the worse-than-Standard rate left its band");
            Assert.That(Share(better), Is.InRange(0.55, 0.72), "the better-than-Standard rate left its band");
            Assert.That(Share(star), Is.InRange(0.04, 0.10), "the star rate left its band");
        }

        [Test]
        public void StandardHasNoSpreadInTotalAndSomeInShape()
        {
            int standardTotal = ContentPack.Pawns().Kind.standardSkillBudget;
            int specialists = 0;
            for (int i = 0; i < Draws; i++)
            {
                Pawn p = ColonistDraw.Roll(Seed(i), i % 3, RollProfile.Standard);
                Assert.That(BudgetTotal(p, out int best), Is.EqualTo(standardTotal));
                if (best >= 6) specialists++;
            }

            TestContext.Out.WriteLine($"Standard over {Draws:N0}: a 6+ skill {Pct(specialists)}");
            Assert.That(Share(specialists), Is.InRange(0.15, 0.40), "Standard stopped making specialists, or made nothing else");
        }

        /// <summary>Spread seeds, as a player's would be; consecutive integers are fine for the stream but read as a pattern.</summary>
        static uint Seed(int i) => unchecked((uint)i * 2654435761u + 0x9E3779B9u);

        static double Share(int n) => (double)n / Draws;

        static string Pct(int n) => $"{Share(n) * 100:F1}%";
    }
}
