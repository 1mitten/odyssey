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
    /// phases, and which chime an alert row gets.
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
            // Stacking is arithmetic on purpose; the range does the limiting. The floor
            // below, and above it the one boost ceiling a stack of boosted faders cannot
            // talk its way past — the owner asked for faders that raise as well as lower
            // (2026-09-17), and the sum still meets one cap.
            Assert.That(AudioMath.StackDb(AudioMath.SilenceDb, 6f), Is.EqualTo(-74f).Within(0.001f));
            Assert.That(AudioMath.StackDb(6f, 6f), Is.EqualTo(AudioMath.BoostDb).Within(0.001f),
                "two boosted faders still meet one ceiling when stacked");
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

    public class AlertChimeTests
    {
        static AlertRow Row(string key, AlertSeverity severity, int pawn = 1) =>
            new(key, "Wrenn", " is starving", severity, pawn: new PawnId(pawn));

        [Test]
        public void SeverityPicksTheChimeAndAnythingUnlistedStillGetsOne()
        {
            Assert.That(AlertChime.ForSeverity(AlertSeverity.Notice), Is.EqualTo(SoundIds.AlertNormal));
            Assert.That(AlertChime.ForSeverity(AlertSeverity.Warning), Is.EqualTo(SoundIds.AlertNegative));
            Assert.That(AlertChime.ForSeverity(AlertSeverity.Danger), Is.EqualTo(SoundIds.AlertNegative));

            // The nineteen ui.alert.* keys nothing raises yet are the point of the default: one
            // of them arriving must chime without anybody editing AlertChime first.
            Assert.That(AlertChime.For("ui.alert.somethingnobodyhaswrittenyet", AlertSeverity.Danger),
                Is.EqualTo(SoundIds.AlertNegative));
        }

        [Test]
        public void AKeyWithItsOwnSoundBeatsItsSeverity()
        {
            Assert.That(AlertChime.For(AlertChime.RaidKey, AlertSeverity.Danger),
                Is.EqualTo(SoundIds.AlertRaid),
                "a raid and a starving colonist are both Danger and must not sound alike");
        }

        /// <summary>
        /// A raid's Events row sounds the war horn (design 53 §7), and the Raid alert is the assault
        /// horn, AlertKey matching the model's; every other row still sounds its favourability.
        /// </summary>
        [Test]
        public void ARaidArrivingSoundsTheWarHornAndTheRestTheirFavourability()
        {
            Assert.That(BulletinChime.For(raid: true, favourability: 2), Is.EqualTo(SoundIds.AlertRaidArrive));
            Assert.That(BulletinChime.For(raid: false, favourability: 2), Is.EqualTo(SoundIds.AlertNegative));
            Assert.That(BulletinChime.For(raid: false, favourability: 1), Is.EqualTo(SoundIds.AlertHappy));
            Assert.That(BulletinChime.For(raid: false, favourability: 0), Is.EqualTo(SoundIds.AlertNormal));
            Assert.That(AlertModel.RaidKey, Is.EqualTo(AlertChime.RaidKey), "the model raises the key the horn is on");
        }

        [Test]
        public void TheFirstStepArmsWithoutChiming()
        {
            var watch = new AlertChimeWatch();

            Assert.That(watch.Step(new[] { Row(AlertModel.StarveKey, AlertSeverity.Danger) }),
                Is.Null, "loading a save with a hungry colonist does not chime at the player");
            Assert.That(watch.Step(new[] { Row(AlertModel.StarveKey, AlertSeverity.Danger) }),
                Is.Null, "and a row that was already there stays silent");
        }

        [Test]
        public void ARowChimesWhenItAppearsAndNotWhileItStays()
        {
            var watch = new AlertChimeWatch();
            watch.Step(System.Array.Empty<AlertRow>());

            AlertRow starving = Row(AlertModel.StarveKey, AlertSeverity.Danger);
            Assert.That(watch.Step(new[] { starving }), Is.EqualTo(SoundIds.AlertNegative));
            Assert.That(watch.Step(new[] { starving }), Is.Null,
                "still starving is not a second event");
            Assert.That(watch.Step(new[] { starving }), Is.Null);
        }

        [Test]
        public void AClearedRowChimesAgainWhenItReturns()
        {
            var watch = new AlertChimeWatch();
            watch.Step(System.Array.Empty<AlertRow>());

            AlertRow starving = Row(AlertModel.StarveKey, AlertSeverity.Danger);
            watch.Step(new[] { starving });
            watch.Step(System.Array.Empty<AlertRow>());

            Assert.That(watch.Step(new[] { starving }), Is.EqualTo(SoundIds.AlertNegative),
                "the colonist ate and is starving again: that is a new alert");
        }

        [Test]
        public void SeveralRowsAppearingTogetherChimeOnceAtTheLoudest()
        {
            var watch = new AlertChimeWatch();
            watch.Step(System.Array.Empty<AlertRow>());

            string? chime = watch.Step(new[]
            {
                Row(AlertModel.IdleKey, AlertSeverity.Notice, pawn: 1),
                Row(AlertModel.StarveKey, AlertSeverity.Danger, pawn: 2),
                Row(AlertModel.IdleKey, AlertSeverity.Notice, pawn: 3),
            });

            Assert.That(chime, Is.EqualTo(SoundIds.AlertNegative),
                "one sound says 'something needs you'; the panel says what");
        }

        [Test]
        public void AnExistingRowDoesNotMaskANewOneBesideIt()
        {
            var watch = new AlertChimeWatch();
            watch.Step(System.Array.Empty<AlertRow>());

            AlertRow starving = Row(AlertModel.StarveKey, AlertSeverity.Danger, pawn: 1);
            watch.Step(new[] { starving });

            AlertRow idle = Row(AlertModel.IdleKey, AlertSeverity.Notice, pawn: 2);
            Assert.That(watch.Step(new[] { starving, idle }), Is.EqualTo(SoundIds.AlertNormal),
                "the loudest row present is not the loudest row that is new");
        }
    }
}
