#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The sound of a blow (design 33 §9g): which swings whoosh, which slice, which hits thud, and
    /// <b>when</b> — the whoosh's peak a tenth of a second before the blow and never earlier, the
    /// slice's on it, at any speed, and nothing while paused. The clock is driven frame by frame the
    /// way the bootstrap drives it: the last tick run plus the fraction towards the next.
    /// </summary>
    public class CombatSoundTimingTests
    {
        // Item defs: 0 a bat (blunt), 1 a machete (sharp), 2 a plank (not a weapon).
        const int Bat = 0, Machete = 1, Plank = 2, Fists = -1;

        // Pawn kinds: 0 a colonist, 1 a rat (sharp teeth), 2 a hog (blunt tusks).
        const int Colonist = 0, Rat = 1, Hog = 2;

        const float Nominal = 60f;

        static BloodSides Sides() => new BloodSides(
            new bool?[] { false, true, null },
            new bool?[] { null, true, false },
            fistsSharp: false);

        static readonly PawnId A = new PawnId(1), B = new PawnId(2), C = new PawnId(3);

        static CombatEventView Event(CombatEventKind kind, int tick, PawnId attacker, PawnId target,
            int amount = 0, int weapon = Fists) =>
            new CombatEventView(1, tick, kind, attacker, target, new CellRef(4, 0, 4), amount, weapon);

        /// <summary>
        /// Runs the frames from <paramref name="fromTick"/> at <paramref name="fps"/> and a game speed,
        /// and returns the real seconds (from the first frame) at which the one waiting cue started,
        /// or -1 if it never did. The clock is exactly the bootstrap's: ticks run whole, and the
        /// frame sits a fraction of the way to the next.
        /// </summary>
        static double FirstStart(CombatSoundSchedule schedule, double fromTick, float fps, int speed,
            out PendingCue started, double seconds = 3.0)
        {
            float tps = CombatSoundTiming.TicksPerRealSecond(speed, Nominal);
            for (int frame = 0; frame * (1.0 / fps) <= seconds; frame++)
            {
                double now = fromTick + frame * (1.0 / fps) * tps;
                if (schedule.TryTakeDue(now, tps, out started)) return frame * (1.0 / fps);
            }

            started = default;
            return -1;
        }

        [Test]
        public void EverySwingWithAWeaponWhooshesAndFistsAndBitesAreSilent()
        {
            var sides = Sides();
            Assert.That(CombatSoundTiming.SwingCue(CombatEventKind.Swing, sides.IsHeldWeapon(Bat), sides.IsSharp(Bat, Colonist)),
                Is.EqualTo(CombatCue.Whoosh));
            Assert.That(CombatSoundTiming.SwingCue(CombatEventKind.Swing, sides.IsHeldWeapon(Machete), sides.IsSharp(Machete, Colonist)),
                Is.EqualTo(CombatCue.Whoosh), "an ordinary sharp swing still whooshes");

            foreach ((int weapon, int kind) in new[] { (Fists, Colonist), (Fists, Rat), (Fists, Hog), (Plank, Colonist) })
            foreach (CombatEventKind swing in new[] { CombatEventKind.Swing, CombatEventKind.SwingCritical })
            {
                var schedule = new CombatSoundSchedule();
                schedule.Hear(Event(swing, 100, A, B, amount: 20, weapon: weapon), sides, kind);
                Assert.That(schedule.Count, Is.Zero, $"weapon {weapon}, kind {kind}, {swing} made a sound");
            }
        }

        [Test]
        public void ASharpCriticalSlicesInsteadAndABluntCriticalKeepsTheWhoosh()
        {
            var sides = Sides();
            var schedule = new CombatSoundSchedule();
            schedule.Hear(Event(CombatEventKind.SwingCritical, 100, A, B, amount: 22, weapon: Machete), sides, Colonist);
            schedule.Hear(Event(CombatEventKind.SwingCritical, 100, B, A, amount: 30, weapon: Bat), sides, Colonist);

            Assert.That(schedule.Count, Is.EqualTo(2), "one cue per swing, never both");
            Assert.That(schedule[0].Cue, Is.EqualTo(CombatCue.Slice));
            Assert.That(schedule[0].ImpactTick, Is.EqualTo(122), "the impact is the swing's tick plus its wind-up");
            Assert.That(schedule[1].Cue, Is.EqualTo(CombatCue.Whoosh));
            Assert.That(schedule[1].ImpactTick, Is.EqualTo(130));
        }

        /// <summary>
        /// A shot (design 47 §4c-bis) schedules no cue and sounds nothing through the melee schedule:
        /// the report is played on the frame of the <c>Shot</c> by the feedback director, and its onset
        /// is within a frame of the file's first sample, so nothing waits for it.
        /// </summary>
        [Test]
        public void AShotSchedulesNothing()
        {
            var schedule = new CombatSoundSchedule();
            Assert.That(schedule.Hear(Event(CombatEventKind.Shot, 130, A, B, amount: 8, weapon: Bat), Sides(), Colonist),
                Is.EqualTo(CombatCue.None));
            Assert.That(schedule.Count, Is.EqualTo(0));
        }

        [Test]
        public void EveryLandedHitThudsOnItsFrameAndAMissOrADodgeDoesNot()
        {
            var sides = Sides();
            var schedule = new CombatSoundSchedule();
            foreach (int weapon in new[] { Bat, Machete, Fists })
                Assert.That(schedule.Hear(Event(CombatEventKind.Hit, 130, A, B, amount: 7000, weapon: weapon), sides, Colonist),
                    Is.EqualTo(CombatCue.Thud), $"a hit with {weapon} was silent");
            Assert.That(schedule.Hear(Event(CombatEventKind.Hit, 130, C, B, amount: 3000), sides, Rat), Is.EqualTo(CombatCue.Thud),
                "a bite that lands thuds");

            foreach (CombatEventKind quiet in new[]
                     {
                         CombatEventKind.Miss, CombatEventKind.Dodge, CombatEventKind.Critical, CombatEventKind.Stun,
                         CombatEventKind.Swing, CombatEventKind.SwingCritical, CombatEventKind.Recovered,
                     })
                Assert.That(schedule.Hear(Event(quiet, 130, A, B, amount: 20, weapon: Bat), sides, Colonist),
                    Is.EqualTo(CombatCue.None), $"a {quiet} sounded on its own frame");
        }

        [TestCase(1, 30f, 60f)]
        [TestCase(1, 22f, 144f)]
        [TestCase(2, 30f, 60f)]
        [TestCase(3, 30f, 60f)]
        [TestCase(3, 36f, 144f)]
        public void TheWhooshPeaksATenthBeforeTheBlowAndNeverEarlier(int speed, float windup, float fps)
        {
            var schedule = new CombatSoundSchedule();
            schedule.Hear(Event(CombatEventKind.Swing, 100, A, B, amount: (int)windup, weapon: Bat), Sides(), Colonist);

            double start = FirstStart(schedule, 100, fps, speed, out PendingCue cue);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "the whoosh never started");
            Assert.That(cue.Cue, Is.EqualTo(CombatCue.Whoosh));

            double impact = windup / CombatSoundTiming.TicksPerRealSecond(speed, Nominal);
            double lead = impact - (start + CombatSoundTiming.WhooshPeakSeconds);
            Assert.That(lead, Is.LessThanOrEqualTo(CombatSoundTiming.WhooshLeadSeconds + 1e-9), "the whoosh peaked early");
            Assert.That(lead, Is.GreaterThan(CombatSoundTiming.WhooshLeadSeconds - 1.0 / fps),
                "the whoosh peaked more than a frame late");
        }

        [TestCase(1, 22f, 60f)]
        [TestCase(1, 26f, 144f)]
        [TestCase(3, 22f, 60f)]
        public void TheSlicePeaksOnTheImpact(int speed, float windup, float fps)
        {
            var schedule = new CombatSoundSchedule();
            schedule.Hear(Event(CombatEventKind.SwingCritical, 100, A, B, amount: (int)windup, weapon: Machete), Sides(), Colonist);

            double start = FirstStart(schedule, 100, fps, speed, out PendingCue cue);
            Assert.That(cue.Cue, Is.EqualTo(CombatCue.Slice));

            double impact = windup / CombatSoundTiming.TicksPerRealSecond(speed, Nominal);
            double late = start + CombatSoundTiming.SlicePeakSeconds - impact;
            Assert.That(late, Is.GreaterThanOrEqualTo(-1e-9), "the slice peaked before the blow");
            Assert.That(late, Is.LessThan(1.0 / fps), "the slice peaked more than a frame after the blow");
        }

        [Test]
        public void TheSameSwingIsHeardThreeTimesSoonerAtSpeedThree()
        {
            double At(int speed)
            {
                var schedule = new CombatSoundSchedule();
                schedule.Hear(Event(CombatEventKind.Swing, 100, A, B, amount: 36, weapon: Bat), Sides(), Colonist);
                return FirstStart(schedule, 100, 1000f, speed, out _) + CombatSoundTiming.WhooshPeakSeconds
                       + CombatSoundTiming.WhooshLeadSeconds;
            }

            // The impact, in real seconds from the swing, is the peak plus the lead.
            Assert.That(At(1), Is.EqualTo(36 / 60.0).Within(0.002));
            Assert.That(At(3), Is.EqualTo(36 / 180.0).Within(0.002));
        }

        [Test]
        public void PausedNothingStartsAndResumedItStartsOnTime()
        {
            var schedule = new CombatSoundSchedule();
            schedule.Hear(Event(CombatEventKind.Swing, 100, A, B, amount: 30, weapon: Bat), Sides(), Colonist);

            // Paused a tick from the blow, for a long while: nothing, and nothing let go either.
            float paused = CombatSoundTiming.TicksPerRealSecond(0, Nominal);
            Assert.That(paused, Is.Zero, "a paused world still has a clock");
            for (int frame = 0; frame < 600; frame++)
                Assert.That(schedule.TryTakeDue(129.0, paused, out _), Is.False, "a paused world started a sound");
            Assert.That(schedule.Count, Is.EqualTo(1));
            Assert.That(schedule.Dropped, Is.Zero);

            // Un-paused ten ticks before the blow at x1: it waits until its moment and then starts.
            Assert.That(schedule.TryTakeDue(120.0, 60f, out _), Is.False, "0.167 s out is too early to start");
            Assert.That(schedule.TryTakeDue(121.7, 60f, out PendingCue due), Is.True);
            Assert.That(due.Cue, Is.EqualTo(CombatCue.Whoosh));
        }

        [Test]
        public void AWhooshSeenTooLateIsLetGoRatherThanPlayedOnTheThud()
        {
            var schedule = new CombatSoundSchedule();
            schedule.Hear(Event(CombatEventKind.Swing, 100, A, B, amount: 30, weapon: Bat), Sides(), Colonist);

            // Five ticks out at x3 is 28 ms: started now its peak would land after the blow.
            Assert.That(schedule.TryTakeDue(125.0, 180f, out _), Is.False);
            Assert.That(schedule.Count, Is.Zero);
            Assert.That(schedule.Dropped, Is.EqualTo(1));

            // Late but clear of the blow — 0.1 s out at x1, peak 60 ms early — it starts at once.
            schedule.Hear(Event(CombatEventKind.Swing, 200, A, B, amount: 30, weapon: Bat), Sides(), Colonist);
            Assert.That(schedule.TryTakeDue(224.0, 60f, out _), Is.True);
        }

        [Test]
        public void ASwingerDownedOrKnockedDownLosesTheSoundOfTheSwingInTheAir()
        {
            foreach (CombatEventKind fall in new[] { CombatEventKind.Downed, CombatEventKind.Died, CombatEventKind.KnockedBack })
            {
                var schedule = new CombatSoundSchedule();
                schedule.Hear(Event(CombatEventKind.Swing, 100, A, B, amount: 30, weapon: Bat), Sides(), Colonist);
                schedule.Hear(Event(CombatEventKind.Swing, 100, B, A, amount: 22, weapon: Machete), Sides(), Colonist);

                schedule.Hear(Event(fall, 105, C, A), Sides(), Colonist);
                Assert.That(schedule.Count, Is.EqualTo(1), $"{fall} of a swinger left its whoosh waiting");
                Assert.That(schedule[0].Attacker, Is.EqualTo(B), $"{fall} of one swinger silenced another");
            }
        }

        [Test]
        public void ANewSwingReplacesTheSwingersLastAndTheWorldChangeClearsAll()
        {
            var schedule = new CombatSoundSchedule();
            schedule.Hear(Event(CombatEventKind.Swing, 100, A, B, amount: 30, weapon: Bat), Sides(), Colonist);
            schedule.Hear(Event(CombatEventKind.SwingCritical, 140, A, B, amount: 22, weapon: Machete), Sides(), Colonist);
            Assert.That(schedule.Count, Is.EqualTo(1));
            Assert.That(schedule[0].Cue, Is.EqualTo(CombatCue.Slice));

            for (int i = 0; i < CombatSoundSchedule.Capacity + 5; i++)
                schedule.Hear(Event(CombatEventKind.Swing, 150, new PawnId(10 + i), B, amount: 30, weapon: Bat), Sides(), Colonist);
            Assert.That(schedule.Count, Is.EqualTo(CombatSoundSchedule.Capacity));
            Assert.That(schedule.Overflowed, Is.EqualTo(6));

            schedule.Clear();
            Assert.That(schedule.Count, Is.Zero);
            Assert.That(schedule.TryTakeDue(10_000, 60f, out _), Is.False);
        }

        [Test]
        public void TheQuickestSwingAtTheTopSpeedIsStillHeardClearOfTheBlow()
        {
            // The machete's 22 ticks at x3 is 0.122 s from swing to blow — less than the whoosh's
            // 0.04 s to its peak plus its 0.1 s lead, so it cannot be on time. It must still be
            // heard, clear of the thud, when the swing is first read a whole frame after its tick.
            // A re-bake that lets dead air back in front of the whoosh fails here before it is heard.
            var schedule = new CombatSoundSchedule();
            schedule.Hear(Event(CombatEventKind.Swing, 100, A, B, amount: 22, weapon: Machete), Sides(), Colonist);

            float tps = CombatSoundTiming.TicksPerRealSecond(3, Nominal);
            double seenAt = 100 + tps / 60.0;
            Assert.That(schedule.TryTakeDue(seenAt, tps, out PendingCue due), Is.True, "the quickest swing was not heard");
            double lead = (122 - seenAt) / tps - CombatSoundTiming.WhooshPeakSeconds;
            Assert.That(lead, Is.GreaterThanOrEqualTo(CombatSoundTiming.WhooshClearanceSeconds));
            Assert.That(CombatSoundTiming.ThudPeakSeconds, Is.LessThan(1 / 60.0), "the thud's transient is not on its first frame");
        }
    }
}
