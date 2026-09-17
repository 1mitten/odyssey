#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What a night's sleep is worth, per quality tier.
    ///
    /// <para><b>Written because two of the five tiers did nothing.</b> The base gain is 6 and the
    /// tiers are percentages of it, so the products are 4.8, 5.1, 6.0, 6.72, 7.5 and 8.4 — and
    /// integer division flattened them to 4, 5, 6, <b>6</b>, 7, 8. A Decent bed recovered rest at
    /// exactly the rate of a Normal one. Nothing failed, because every number involved was a
    /// perfectly good number; the tier a colonist rolled was simply worth nothing, and the only
    /// way to find that out was to print the table and read it.</para>
    ///
    /// <para>So these assert the <b>relations</b> — every tier strictly better than the one below,
    /// and each one's long-run average equal to its own percentage — rather than the six values.
    /// The owner's numbers can be retuned freely; they cannot be retuned into a tier that means
    /// nothing.</para>
    /// </summary>
    public class RestGainTests
    {
        static readonly GridSize Size = new GridSize(32, 32, 8);

        static Pawn AColonist()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, 4242, ScenarioDef.Bare());
            return colony.Pawns.Pawns.All[0];
        }

        /// <summary>Rest recovered over a hundred intervals, in hundredths of a point per interval.</summary>
        static int AveragePerCent(Pawn pawn, int effectiveness)
        {
            int total = 0;
            for (int i = 0; i < 100; i++) total += pawn.RestGainPerInterval(effectiveness, i);
            return total;
        }

        /// <summary>
        /// The ladder: ground, then every tier, each strictly better than the last over a night.
        /// This is the assertion whose absence let Decent collapse onto Normal.
        /// </summary>
        [Test]
        public void EveryQualityTierSleepsStrictlyBetterThanTheOneBelowIt()
        {
            Pawn pawn = AColonist();

            int ground = AveragePerCent(pawn, pawn.Content.Kind.groundRestEffectiveness);
            int poor = AveragePerCent(pawn, QualityContent.RestEffectiveness(QualityHandle.Poor));
            int normal = AveragePerCent(pawn, QualityContent.RestEffectiveness(QualityHandle.Normal));
            int decent = AveragePerCent(pawn, QualityContent.RestEffectiveness(QualityHandle.Decent));
            int uber = AveragePerCent(pawn, QualityContent.RestEffectiveness(QualityHandle.Uber));
            int epic = AveragePerCent(pawn, QualityContent.RestEffectiveness(QualityHandle.Epic));

            Assert.That(ground, Is.LessThan(poor), "the worst bed must beat the floor");
            Assert.That(poor, Is.LessThan(normal));
            Assert.That(normal, Is.LessThan(decent), "a Decent bed was worth exactly a Normal one");
            Assert.That(decent, Is.LessThan(uber));
            Assert.That(uber, Is.LessThan(epic));
        }

        /// <summary>
        /// And each tier's long-run rate is its own percentage, not a rounded-off approximation of
        /// it: the dither spends the fractional part rather than discarding it.
        /// </summary>
        [Test]
        public void ATiersLongRunRateIsExactlyWhatTheContentSaysItIs()
        {
            Pawn pawn = AColonist();
            int perInterval = pawn.Content.Kind.restGainPerInterval;

            foreach (int tier in new[]
            {
                QualityHandle.Poor, QualityHandle.Normal, QualityHandle.Decent,
                QualityHandle.Uber, QualityHandle.Epic,
            })
            {
                int effectiveness = QualityContent.RestEffectiveness(tier);
                Assert.That(AveragePerCent(pawn, effectiveness),
                    Is.EqualTo(perInterval * effectiveness),
                    $"tier {tier} does not average its own {effectiveness}% over a hundred intervals");
            }
        }

        /// <summary>
        /// A bed beats the floor, which is the whole reason a colonist walks to one — and
        /// <b>by how much is written down here rather than left to be discovered.</b>
        ///
        /// <para>Measured on the content as it stands: the ground is 80, a plain bed 100 and an
        /// Epic one 140, so a plain bed recovers rest <b>1.25x</b> as fast as the floor and the
        /// best bed <b>1.75x</b>. Those are the owner's own numbers (design 20 §2, decision 4,
        /// which states the ground's 80 as unchanged), so this pins the relation and not a target:
        /// a bed must beat the floor and the ladder must be worth climbing. If a night in a bed
        /// should feel more decisive than 1.25x, the lever is <c>groundRestEffectiveness</c> — one
        /// integer in <c>Colonist.xml</c>, and the owner's to turn.</para>
        /// </summary>
        [Test]
        public void SleepingInABedBeatsTheGroundAndTheLadderIsWorthClimbing()
        {
            Pawn pawn = AColonist();
            int ground = AveragePerCent(pawn, pawn.Content.Kind.groundRestEffectiveness);
            int normal = AveragePerCent(pawn, QualityContent.RestEffectiveness(QualityHandle.Normal));
            int epic = AveragePerCent(pawn, QualityContent.RestEffectiveness(QualityHandle.Epic));

            Assert.That(normal, Is.GreaterThan(ground), "a plain bed must beat the floor");
            Assert.That(normal * 100 / ground, Is.EqualTo(125),
                "a plain bed is a quarter again as good as the ground - change this deliberately");
            Assert.That(epic * 100 / ground, Is.EqualTo(175),
                "and the best bed three quarters again - change this deliberately");
        }
    }
}
