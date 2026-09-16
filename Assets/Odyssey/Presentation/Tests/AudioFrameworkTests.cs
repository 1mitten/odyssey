#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The pure arithmetic and pure decisions of the audio system: faders, the clock's music
    /// phases, and the alert watcher's hysteresis.
    ///
    /// None of these touch an AudioSource; that is the point of splitting them out. A volume
    /// conversion that drifts, a phase boundary off by an hour, or an alert that re-fires every
    /// frame would each be expensive to debug through the sound itself, and each is a three-line
    /// test here.
    /// </summary>
    public class AudioMathTests
    {
        [Test]
        public void UnityGainIsZeroDbAndSilenceIsTheFloor()
        {
            Assert.That(AudioMath.LinearToDb(1f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(AudioMath.LinearToDb(0f), Is.EqualTo(AudioMath.SilenceDb));
            Assert.That(AudioMath.LinearToDb(0.5f), Is.EqualTo(-6.02f).Within(0.01f));
        }

        [Test]
        public void DecibelsRoundTripToLinear()
        {
            foreach (float db in new[] { 0f, -6f, -20f, -79.9f, AudioMath.SilenceDb })
                Assert.That(AudioMath.LinearToDb(AudioMath.DbToLinear(db)),
                    Is.EqualTo(db).Within(0.01f), $"dB {db} should survive the trip");
        }

        [Test]
        public void TheFloorIsExactlySilent()
        {
            Assert.That(AudioMath.DbToLinear(AudioMath.SilenceDb), Is.EqualTo(0f));
        }

        [Test]
        public void SlidersSpendTheirTravelWhereTheHearingIs()
        {
            Assert.That(AudioMath.SliderToDb(1f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(AudioMath.SliderToDb(0f), Is.EqualTo(AudioMath.SilenceDb));
            // The taper is a square in gain: a quarter of the travel is a sixteenth of the
            // amplitude, which is minus 24 dB — half the travel is minus 12, exactly half.
            Assert.That(AudioMath.SliderToDb(0.5f), Is.EqualTo(-12.04f).Within(0.01f));
            Assert.That(AudioMath.SliderToDb(0.25f), Is.EqualTo(-24.08f).Within(0.01f));
        }

        [Test]
        public void BusesStackByAddingDecibelsUnderMaster()
        {
            Assert.That(AudioMath.StackDb(0f, 0f), Is.EqualTo(0f));
            Assert.That(AudioMath.StackDb(-10f, -10f), Is.EqualTo(-20f).Within(0.001f));
            Assert.That(AudioMath.StackDb(-90f, -90f), Is.EqualTo(AudioMath.SilenceDb),
                "past the floor is the floor");
            // Stacking is arithmetic on purpose; what keeps a bus from raising anything is the
            // fader range itself — nothing may sit above 0 dB, so nothing adds gain.
            Assert.That(AudioMath.StackDb(AudioMath.SilenceDb, 6f), Is.EqualTo(-74f).Within(0.001f));
        }
    }

    public class MusicClockTests
    {
        [Test]
        public void DayRunsFromSixUntilNineteen()
        {
            Assert.That(MusicClock.PhaseOf(Hour(6)), Is.EqualTo(MusicPhase.Day));
            Assert.That(MusicClock.PhaseOf(Hour(18)), Is.EqualTo(MusicPhase.Day));
            Assert.That(MusicClock.PhaseOf(Hour(5)), Is.EqualTo(MusicPhase.Night));
            Assert.That(MusicClock.PhaseOf(Hour(19)), Is.EqualTo(MusicPhase.Night));
            Assert.That(MusicClock.PhaseOf(0), Is.EqualTo(MusicPhase.Night),
                "midnight is the middle of the night, not the start of a new track");
        }

        static long Hour(int hour) => (long)hour * GameClock.TicksPerHour;
    }

    /// <summary>
    /// Where a blow lands, and — the part that mattered — that it lands at all when nobody is
    /// holding anything.
    /// </summary>
    public class BlowPointTests
    {
        [Test]
        public void ABladeDecidesWhereTheBlowIs()
        {
            Vector3 point = Odyssey.Presentation.World.PawnFigureDirector.BlowPoint(
                bladeTip: new Vector3(4f, 1f, 0f), workCentre: new Vector3(5f, 1f, 0f),
                outward: Vector3.left, standOff: 0f);

            Assert.That(point, Is.EqualTo(new Vector3(4f, 1f, 0f)));
        }

        [Test]
        public void WithNoBladeTheBlowIsStillWhereTheWorkIs()
        {
            // A clone without the art packs fells trees bare-handed. The axe it is not holding
            // used to decide whether the work made any sound at all.
            Vector3 point = Odyssey.Presentation.World.PawnFigureDirector.BlowPoint(
                bladeTip: null, workCentre: new Vector3(5f, 1f, 0f),
                outward: Vector3.left, standOff: 0f);

            Assert.That(point, Is.EqualTo(new Vector3(5f, 1f, 0f)),
                "no tool is not no blow");
        }

        [Test]
        public void TheBlowStandsOffTheFaceItStruck()
        {
            // The head finishes inside what it hit; debris and sound both belong on the face.
            Vector3 point = Odyssey.Presentation.World.PawnFigureDirector.BlowPoint(
                bladeTip: null, workCentre: Vector3.zero, outward: new Vector3(2f, 0f, 0f),
                standOff: 0.4f);

            Assert.That(point, Is.EqualTo(new Vector3(0.4f, 0f, 0f)).Using<Vector3>(
                (a, b) => (a - b).magnitude < 0.001f ? 0 : 1));
        }
    }

    public class AlertWatchTests
    {
        static PawnView Fed(int id, int food) =>
            new(new PawnId(id), new CellRef(1, 1, 0), food, 80, 50);

        [Test]
        public void CrossingTheThresholdRaisesOnce()
        {
            var watch = new AlertWatch();

            Assert.That(watch.Step(new[] { Fed(1, 50) }), Is.EqualTo(AudioAlert.None));
            Assert.That(watch.Step(new[] { Fed(1, 13) }), Is.EqualTo(AudioAlert.None),
                "13 is hungry, not starving");
            Assert.That(watch.Step(new[] { Fed(1, AlertWatch.StarveThreshold) }),
                Is.EqualTo(AudioAlert.Starving), "the boundary itself is the crossing");
            Assert.That(watch.Step(new[] { Fed(1, 5) }), Is.EqualTo(AudioAlert.None),
                "deeper into starvation is not a second alert");
            Assert.That(watch.Step(new[] { Fed(1, 5) }), Is.EqualTo(AudioAlert.None));
        }

        [Test]
        public void TheAlertRearmsOnlyPastTheBand()
        {
            var watch = new AlertWatch();
            watch.Step(new[] { Fed(1, 0) });

            // Wobble inside the band: no re-fire, because the chime would flap every frame.
            watch.Step(new[] { Fed(1, AlertWatch.RearmAbove - 1) });
            Assert.That(watch.Step(new[] { Fed(1, 0) }), Is.EqualTo(AudioAlert.None),
                "a need oscillating around the threshold raised once and stays raised");

            // Recover past the band, then cross again: that is a second event and chimes again.
            watch.Step(new[] { Fed(1, AlertWatch.RearmAbove) });
            Assert.That(watch.Step(new[] { Fed(1, 0) }), Is.EqualTo(AudioAlert.Starving));
        }

        [Test]
        public void SeveralColonistsCrossingAtOnceChimeOnce()
        {
            var watch = new AlertWatch();
            AudioAlert fired = watch.Step(new[] { Fed(1, 0), Fed(2, 0), Fed(3, 0) });

            Assert.That(fired, Is.EqualTo(AudioAlert.Starving),
                "one chime carries 'somebody is starving'; the roster says who");
        }
    }
}
