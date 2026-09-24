#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Blood, the rules half (design 33 §10): how much a hit throws and how big a mark it leaves,
    /// sharp against blunt; how a mark fades by the simulation's tick; the cap of two hundred,
    /// oldest first; the slice; and a pool sized by the body under it.
    /// </summary>
    public class BloodLedgerTests
    {
        [Test]
        public void SharpThrowsMoreDropsAndLeavesALargerMarkThanBlunt()
        {
            foreach (float damage in new[] { 1f, 5f, 12f, 30f })
            {
                Assert.That(BloodSpray.Drops(damage, sharp: true), Is.GreaterThan(BloodSpray.Drops(damage, sharp: false)),
                    $"at {damage} damage a cut threw no more than a blow");
                Assert.That(BloodSpray.MarkRadius(damage, sharp: true), Is.GreaterThan(BloodSpray.MarkRadius(damage, sharp: false)),
                    $"at {damage} damage a cut left no larger a mark than a blow");
            }

            Assert.That(BloodSpray.MarkStretch(sharp: true), Is.GreaterThan(1f), "a cut's splatter is laid along the blow");
            Assert.That(BloodSpray.MarkStretch(sharp: false), Is.EqualTo(1f), "a blow's spot is round");
            Assert.That(BloodSpray.ShapeOf(sharp: true), Is.EqualTo(BloodShape.Splatter));
            Assert.That(BloodSpray.ShapeOf(sharp: false), Is.EqualTo(BloodShape.Spot));
        }

        [Test]
        public void DropsAndMarksGrowWithDamageAndStopAtTheirCeilings()
        {
            foreach (bool sharp in new[] { true, false })
            {
                int lastDrops = 0;
                float lastRadius = 0f;
                for (float damage = 0f; damage <= 80f; damage += 1f)
                {
                    int drops = BloodSpray.Drops(damage, sharp);
                    float radius = BloodSpray.MarkRadius(damage, sharp);
                    Assert.That(drops, Is.GreaterThanOrEqualTo(lastDrops).And.GreaterThanOrEqualTo(1));
                    Assert.That(radius, Is.GreaterThanOrEqualTo(lastRadius).And.GreaterThan(0f));
                    lastDrops = drops;
                    lastRadius = radius;
                }
            }

            Assert.That(BloodSpray.Drops(80f, sharp: true), Is.EqualTo(16));
            Assert.That(BloodSpray.Drops(80f, sharp: false), Is.EqualTo(5));
            Assert.That(BloodSpray.MarkRadius(80f, sharp: true), Is.EqualTo(0.55f).Within(1e-5f));
            Assert.That(BloodSpray.MarkRadius(80f, sharp: false), Is.EqualTo(0.28f).Within(1e-5f));
            Assert.That(BloodSpray.Drops(0f, sharp: true), Is.LessThanOrEqualTo(BloodSpray.MaxDropsPerHit));
        }

        [Test]
        public void ACutIsThrownFasterAndNarrowerThanABlow()
        {
            BloodSpray.Throw(sharp: true, out float cutSlow, out float cutFast, out float cutFan);
            BloodSpray.Throw(sharp: false, out float blowSlow, out float blowFast, out float blowFan);
            Assert.That(cutSlow, Is.GreaterThan(blowSlow));
            Assert.That(cutFast, Is.GreaterThan(blowFast));
            Assert.That(cutFan, Is.LessThan(blowFan));
            Assert.That(cutSlow, Is.LessThan(cutFast));
            Assert.That(blowSlow, Is.LessThan(blowFast));
        }

        [Test]
        public void AMarkHoldsFullStrengthForAQuarterOfADayThenFadesToNothing()
        {
            Assert.That(BloodLedger.Strength(0), Is.EqualTo(1f));
            Assert.That(BloodLedger.Strength(5_000), Is.EqualTo(1f), "held at full strength, not brighter");
            Assert.That(BloodLedger.Strength(BloodLedger.FullStrengthTicks), Is.EqualTo(1f));
            Assert.That(BloodLedger.Strength(37_500), Is.EqualTo(0.5f).Within(1e-5f), "halfway down the fade");
            Assert.That(BloodLedger.Strength(BloodLedger.LifetimeTicks), Is.EqualTo(0f));
            Assert.That(BloodLedger.Strength(BloodLedger.LifetimeTicks * 3), Is.EqualTo(0f));
            Assert.That(BloodLedger.LifetimeTicks, Is.EqualTo(60_000), "about one in-game day (owner)");
        }

        [Test]
        public void TheFadeStepsRunFromStrongestToFaintestAndEndAtTheLifetime()
        {
            Assert.That(BloodLedger.FadeStep(0), Is.EqualTo(0));
            Assert.That(BloodLedger.FadeStep(BloodLedger.FullStrengthTicks), Is.EqualTo(0));
            Assert.That(BloodLedger.FadeStep(BloodLedger.LifetimeTicks - 1), Is.EqualTo(BloodLedger.FadeSteps - 1));
            Assert.That(BloodLedger.FadeStep(BloodLedger.LifetimeTicks), Is.EqualTo(-1), "a day old is gone");

            int last = 0;
            for (long age = 0; age < BloodLedger.LifetimeTicks; age += 500)
            {
                int step = BloodLedger.FadeStep(age);
                Assert.That(step, Is.GreaterThanOrEqualTo(last).And.LessThan(BloodLedger.FadeSteps), $"at age {age}");
                // The step's drawn strength never outshines the mark's own by more than one step.
                Assert.That(BloodLedger.StepStrength(step), Is.GreaterThanOrEqualTo(BloodLedger.Strength(age) - 1e-5f)
                    .And.LessThanOrEqualTo(BloodLedger.Strength(age) + 1f / BloodLedger.FadeSteps + 1e-5f), $"at age {age}");
                last = step;
            }
        }

        [Test]
        public void TheCapKeepsTheNewestTwoHundred()
        {
            var ledger = new BloodLedger();
            for (int i = 0; i < 250; i++) ledger.Add(Mark(born: i));

            Assert.That(BloodLedger.Cap, Is.EqualTo(200));
            Assert.That(ledger.Count, Is.EqualTo(200));
            Assert.That(ledger[0].Born, Is.EqualTo(50), "the oldest fifty went first");
            Assert.That(ledger[199].Born, Is.EqualTo(249), "and the newest is kept");
            for (int i = 1; i < ledger.Count; i++)
                Assert.That(ledger[i].Born, Is.GreaterThan(ledger[i - 1].Born), "oldest first, in order");
        }

        [Test]
        public void ExpiringRemovesOnlyWhatHasOutlivedADay()
        {
            var ledger = new BloodLedger();
            ledger.Add(Mark(born: 0));
            ledger.Add(Mark(born: 10_000));
            ledger.Add(Mark(born: 50_000));

            ledger.Expire(now: 60_000);
            Assert.That(ledger.Count, Is.EqualTo(2), "only the mark a whole day old goes");
            Assert.That(ledger[0].Born, Is.EqualTo(10_000));

            ledger.Expire(now: 70_000);
            Assert.That(ledger.Count, Is.EqualTo(1));
            Assert.That(ledger[0].Born, Is.EqualTo(50_000));
        }

        [Test]
        public void RemovingAMarkKeepsTheRestInOrder()
        {
            var ledger = new BloodLedger();
            for (int i = 0; i < 5; i++) ledger.Add(Mark(born: i));
            ledger.RemoveAt(2);
            Assert.That(ledger.Count, Is.EqualTo(4));
            Assert.That(new[] { ledger[0].Born, ledger[1].Born, ledger[2].Born, ledger[3].Born },
                Is.EqualTo(new long[] { 0, 1, 3, 4 }));

            ledger.Clear();
            Assert.That(ledger.Count, Is.Zero);
        }

        [Test]
        public void AMarkOnALayerTheSliceDoesNotDrawIsHidden()
        {
            Assert.That(BloodLedger.Visible(layer: 5, lowest: 3, highest: 6), Is.True);
            Assert.That(BloodLedger.Visible(layer: 3, lowest: 3, highest: 6), Is.True);
            Assert.That(BloodLedger.Visible(layer: 6, lowest: 3, highest: 6), Is.True);
            Assert.That(BloodLedger.Visible(layer: 2, lowest: 3, highest: 6), Is.False, "below the slice");
            Assert.That(BloodLedger.Visible(layer: 7, lowest: 3, highest: 6), Is.False, "above it");
        }

        [Test]
        public void APoolIsSizedByTheBodyAndByWhetherItDied()
        {
            float death = BloodSpray.PoolRadius(BloodSpray.PersonLength, 1f);
            float down = BloodSpray.PoolRadius(BloodSpray.PersonLength, 0.6f);
            float rat = BloodSpray.PoolRadius(0.3f, 1f);

            Assert.That(death, Is.EqualTo(0.81f).Within(1e-5f));
            Assert.That(down, Is.LessThan(death).And.GreaterThan(0f));
            Assert.That(rat, Is.LessThan(death / 4f), "a rat's pool is not a person's");
            Assert.That(BloodSpray.PoolRadius(0f, 1f), Is.GreaterThan(0f), "a body of unknown length still pools");
        }

        [Test]
        public void APoolSpreadsOverEightSecondsOfGameTime()
        {
            Assert.That(BloodSpray.PoolSpreadTicks, Is.EqualTo(480), "eight seconds at 60 ticks a second");
            Assert.That(BloodSpray.PoolGrowth(0), Is.EqualTo(BloodSpray.PoolStartFraction));
            Assert.That(BloodSpray.PoolGrowth(BloodSpray.PoolSpreadTicks / 2),
                Is.GreaterThan(BloodSpray.PoolStartFraction).And.LessThan(1f));
            Assert.That(BloodSpray.PoolGrowth(BloodSpray.PoolSpreadTicks), Is.EqualTo(1f));
            Assert.That(BloodSpray.PoolGrowth(100_000), Is.EqualTo(1f));
            Assert.That(BloodSpray.PoolGrowth(-5), Is.EqualTo(BloodSpray.PoolStartFraction), "a clock that ran back");
        }

        static BloodMarkRecord Mark(long born) => new BloodMarkRecord
        {
            X = 1f, Y = 3f, Z = 1f, Radius = 0.3f, Stretch = 1f, Shape = BloodShape.Spot, Born = born, Layer = 1,
        };
    }
}
