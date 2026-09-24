#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The put-away's timing (design 33 §8b): out the moment the simulation gives a reason, back
    /// at the hip about two seconds after the last one ends, and at once on release from the draft
    /// with no fight near. The reason itself is the simulation's (<c>WeaponDrawTests</c>).
    /// </summary>
    public class WeaponSheathTests
    {
        static SheathClock Seen(bool drawn, bool drafted, int tick)
        {
            var clock = new SheathClock();
            Assert.That(WeaponSheath.Step(ref clock, drawn, drafted, tick), Is.EqualTo(SheathChange.None),
                "a first sighting animates nothing");
            return clock;
        }

        [Test]
        public void AFirstSightingTakesTheWeaponAsItFindsIt()
        {
            SheathClock marauder = Seen(drawn: true, drafted: false, tick: 500);
            Assert.That(marauder.Out, Is.True);
            SheathClock worker = Seen(drawn: false, drafted: false, tick: 500);
            Assert.That(worker.Out, Is.False);
        }

        [Test]
        public void AReasonDrawsTheWeaponOnTheFrameItArrives()
        {
            SheathClock clock = Seen(false, false, 100);
            Assert.That(WeaponSheath.Step(ref clock, false, false, 101), Is.EqualTo(SheathChange.None));
            Assert.That(WeaponSheath.Step(ref clock, true, false, 102), Is.EqualTo(SheathChange.Draw));
            Assert.That(WeaponSheath.Step(ref clock, true, false, 103), Is.EqualTo(SheathChange.None), "drawn twice");
        }

        /// <summary>
        /// The fight ends: the weapon stays out for the hold and goes back on the tick it runs out —
        /// not a tick before (the control, which fails with the hold taken out) and not later.
        /// </summary>
        [Test]
        public void AfterTheFightTheWeaponIsPutAwayWhenTheHoldRunsOut()
        {
            SheathClock clock = Seen(true, false, 1_000);
            int end = 1_000;
            for (int t = end + 1; t < end + WeaponSheath.HoldTicks; t++)
                Assert.That(WeaponSheath.Step(ref clock, false, false, t), Is.EqualTo(SheathChange.None),
                    $"put away {t - end} ticks after the fight, inside the hold");
            Assert.That(WeaponSheath.Step(ref clock, false, false, end + WeaponSheath.HoldTicks),
                Is.EqualTo(SheathChange.Sheathe));
            Assert.That(clock.Out, Is.False);
        }

        [Test]
        public void TheHoldIsAboutTwoSecondsAtSixtyTicksASecond()
        {
            Assert.That(WeaponSheath.HoldTicks / 60f, Is.EqualTo(2f).Within(0.5f));
        }

        /// <summary>A reason coming back inside the hold keeps the weapon out and starts the hold again.</summary>
        [Test]
        public void AReasonInsideTheHoldKeepsItOutAndRestartsTheHold()
        {
            SheathClock clock = Seen(true, false, 0);
            WeaponSheath.Step(ref clock, false, false, 100);
            Assert.That(WeaponSheath.Step(ref clock, true, false, 110), Is.EqualTo(SheathChange.None), "drawn again while out");
            Assert.That(WeaponSheath.Step(ref clock, false, false, 110 + WeaponSheath.HoldTicks - 1), Is.EqualTo(SheathChange.None),
                "the hold ran from the first reason, not the last");
            Assert.That(WeaponSheath.Step(ref clock, false, false, 110 + WeaponSheath.HoldTicks), Is.EqualTo(SheathChange.Sheathe));
        }

        /// <summary>
        /// Released from the draft with no fight near, the weapon goes back at once; released with a
        /// fight still near (the simulation still says drawn) it stays out. A pause — no ticks — holds
        /// a weapon out however long the frame lasts.
        /// </summary>
        [Test]
        public void ReleasedFromTheDraftWithNoFightNearItIsPutAwayAtOnce()
        {
            SheathClock clock = Seen(true, true, 0);
            Assert.That(WeaponSheath.Step(ref clock, false, false, 1), Is.EqualTo(SheathChange.Sheathe),
                "released with nobody near, and the weapon waited out the hold");

            SheathClock fighting = Seen(true, true, 0);
            Assert.That(WeaponSheath.Step(ref fighting, true, false, 1), Is.EqualTo(SheathChange.None),
                "released beside a fight, and the weapon went away");

            SheathClock paused = Seen(true, false, 50);
            WeaponSheath.Step(ref paused, false, false, 60);
            for (int frame = 0; frame < 1_000; frame++)
                Assert.That(WeaponSheath.Step(ref paused, false, false, 60), Is.EqualTo(SheathChange.None), "put away while paused");
        }

        /// <summary>A load rewinds the tick: the hold counts from the world's tick, and still ends.</summary>
        [Test]
        public void ARewoundTickStillPutsTheWeaponAway()
        {
            SheathClock clock = Seen(true, false, 5_000);
            WeaponSheath.Step(ref clock, false, false, 200);
            Assert.That(WeaponSheath.Step(ref clock, false, false, 200 + WeaponSheath.HoldTicks), Is.EqualTo(SheathChange.Sheathe));
        }
    }
}
