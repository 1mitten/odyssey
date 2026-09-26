#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The passage from the menu into a world (design 56): the fade out, the build behind the
    /// veil, and the wake — blurred, warm and muffled, clearing over five seconds, with the
    /// colony held until the eyes are open (owner, 2026-09-26).
    /// </summary>
    public class WakeTransitionTests
    {
        const double Frame = 1d / 60d;

        static WakeTransition Dreaming() => new(WakeTiming.Standard, dream: true);

        /// <summary>Step until the build is due, returning how many steps it took.</summary>
        static int StepToBuild(WakeTransition wake, double dt = Frame, int limit = 10_000)
        {
            for (int i = 1; i <= limit; i++)
            {
                wake.Step(dt);
                if (wake.BuildDue) return i;
            }
            throw new AssertionException("the build never came due");
        }

        static void Build(WakeTransition wake)
        {
            StepToBuild(wake);
            wake.TakeBuild();
            wake.Built();
        }

        // ------------------------------------------------------------ the one build

        [Test]
        public void TheBuildIsAskedForOnceAndOnlyAfterTheScreenHasBeenDrawnBlack()
        {
            var wake = Dreaming();
            Assert.That(wake.Begin(), Is.True);

            int blackSteps = 0;
            int builds = 0;
            for (int i = 0; i < 600; i++)
            {
                wake.Step(Frame);
                if (wake.BuildDue)
                {
                    builds++;
                    Assert.That(blackSteps, Is.GreaterThanOrEqualTo(WakeTiming.Standard.DarkFrames),
                        "the build was asked for before two black frames had been drawn — it would freeze the menu");
                    wake.TakeBuild();
                    Assert.That(wake.BuildDue, Is.False, "a taken build is still due, so a second read builds twice");
                    wake.Built();
                }
                // A frame is drawn black when the veil is fully up at the end of its step.
                if (wake.Look.Veil >= 1f && builds == 0) blackSteps++;
            }
            Assert.That(builds, Is.EqualTo(1));
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Done));
        }

        [Test]
        public void TheMenuTakesTheTimeItWasGivenToClose()
        {
            var wake = Dreaming();
            wake.Begin();
            int steps = StepToBuild(wake);
            double seconds = steps * Frame;
            Assert.That(seconds, Is.GreaterThanOrEqualTo(WakeTiming.Standard.CloseSeconds),
                "the build came due before the menu had finished fading");
            Assert.That(seconds, Is.LessThan(WakeTiming.Standard.CloseSeconds + 0.1),
                "the black screen lingered before the build");
        }

        [Test]
        public void ASecondPressWhileAPassageIsUnderWayIsRefused()
        {
            var wake = Dreaming();
            Assert.That(wake.Begin(), Is.True);
            Assert.That(wake.Begin(), Is.False, "a second Start while closing");
            StepToBuild(wake);
            Assert.That(wake.Begin(), Is.False, "a second Start while building");
            wake.TakeBuild();
            wake.Built();
            Assert.That(wake.Begin(), Is.False, "a second Start behind the cover");
            while (wake.Active) wake.Step(Frame);
            Assert.That(wake.Begin(), Is.True, "a finished passage should let the next one start");
        }

        [Test]
        public void InstantTimingBuildsWithinThreeStepsAndStillDrawsTheBlackFrames()
        {
            var wake = new WakeTransition(WakeTiming.Instant, dream: true);
            wake.Begin();
            int steps = StepToBuild(wake);
            Assert.That(steps, Is.EqualTo(3), "closing (nought seconds) then two black frames");
            wake.TakeBuild();
            wake.Built();
            int after = 0;
            while (wake.Active && after < 20) { wake.Step(Frame); after++; }
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Done));
            Assert.That(after, Is.EqualTo(WakeTiming.Instant.CoverFrames), "the cover frames are frames, not seconds");
        }

        // ------------------------------------------------------------ the clock

        [Test]
        public void ALongFrameTakesOnlyOneClampedStep()
        {
            var wake = Dreaming();
            wake.Begin();
            wake.Step(1.2);
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Closing),
                "a one-second frame finished a 0.6-second fade in one jump");
            wake.Step(double.NaN);
            wake.Step(-3);
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Closing));

            Build(wake);
            while (wake.Phase == WakePhase.Covered) wake.Step(Frame);
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Waking));
            float before = wake.Look.Blur;
            wake.Step(1.5); // the frame the GPU first meets the world
            Assert.That(wake.Look.Blur, Is.GreaterThan(0.99f * before),
                "one slow frame took a visible bite out of the dream");
        }

        [Test]
        public void TheClockIsHeldExactlyOnceFromTheBuildToTheEndOfTheDream()
        {
            var random = new Random(1234);
            for (int run = 0; run < 25; run++)
            {
                var wake = Dreaming();
                wake.Begin();
                int edgesOn = 0, edgesOff = 0;
                bool held = false;
                for (int i = 0; i < 5_000 && (wake.Active || i == 0); i++)
                {
                    wake.Step(random.NextDouble() * 0.2);
                    if (wake.BuildDue)
                    {
                        wake.TakeBuild();
                        wake.Built();
                    }
                    if (wake.HoldsClock != held)
                    {
                        if (wake.HoldsClock) edgesOn++;
                        else edgesOff++;
                        held = wake.HoldsClock;
                    }
                }
                Assert.That(wake.Phase, Is.EqualTo(WakePhase.Done), "run " + run);
                Assert.That(edgesOn, Is.EqualTo(1), "run " + run + ": the hold began more than once");
                Assert.That(edgesOff, Is.EqualTo(1), "run " + run + ": the hold was released more than once, or never");
                Assert.That(wake.HoldsClock, Is.False);
            }
        }

        [Test]
        public void ThePlainFadeNeverHoldsTheClockNorTouchesTheWorld()
        {
            // The negative control for the test above: the same steps with the wake-up off.
            var wake = new WakeTransition(WakeTiming.Standard, dream: false);
            wake.Begin();
            for (int i = 0; i < 2_000 && wake.Active; i++)
            {
                wake.Step(Frame);
                if (wake.BuildDue)
                {
                    wake.TakeBuild();
                    wake.Built();
                }
                Assert.That(wake.HoldsClock, Is.False, "step " + i);
                Assert.That(wake.Look.Blur, Is.EqualTo(0f));
                Assert.That(wake.Look.Haze, Is.EqualTo(0f));
                Assert.That(wake.Look.Settle, Is.EqualTo(0f));
                Assert.That(wake.Look.Muffle, Is.EqualTo(0f));
            }
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Done));
        }

        [Test]
        public void ThePlainFadeStillClosesOverTheMenuAndStillCoversTheBuild()
        {
            var wake = new WakeTransition(WakeTiming.Standard, dream: false);
            wake.Begin();
            StepToBuild(wake);
            Assert.That(wake.Look.Veil, Is.EqualTo(1f), "the build frame is not behind an opaque veil");
            Assert.That(wake.WorldCovered, Is.True);
        }

        // ------------------------------------------------------------ the curves

        [Test]
        public void EveryPartOfTheDreamFallsAndEndsAtNought()
        {
            WakeLook last = WakeTransition.Dreaming(0d);
            Assert.That(last.Veil, Is.EqualTo(1f));
            Assert.That(last.Blur, Is.EqualTo(1f));
            Assert.That(last.Haze, Is.EqualTo(1f));
            Assert.That(last.Settle, Is.EqualTo(1f));
            Assert.That(last.Muffle, Is.EqualTo(1f));
            for (int i = 1; i <= 1000; i++)
            {
                WakeLook now = WakeTransition.Dreaming(i / 1000d);
                Assert.That(now.Veil, Is.LessThanOrEqualTo(last.Veil));
                Assert.That(now.Blur, Is.LessThanOrEqualTo(last.Blur));
                Assert.That(now.Haze, Is.LessThanOrEqualTo(last.Haze));
                Assert.That(now.Settle, Is.LessThanOrEqualTo(last.Settle));
                Assert.That(now.Muffle, Is.LessThanOrEqualTo(last.Muffle));
                last = now;
            }
            Assert.That(last.Veil, Is.EqualTo(0f));
            Assert.That(last.Blur, Is.EqualTo(0f));
            Assert.That(last.Haze, Is.EqualTo(0f));
            Assert.That(last.Settle, Is.EqualTo(0f));
            Assert.That(last.Muffle, Is.EqualTo(0f));
        }

        [Test]
        public void HearingComesBackBeforeSightAndFocusArrivesLast()
        {
            double Clear(Func<WakeLook, float> of)
            {
                for (int i = 0; i <= 1000; i++)
                    if (of(WakeTransition.Dreaming(i / 1000d)) <= 0.01f) return i / 1000d;
                return 1d;
            }
            double veil = Clear(l => l.Veil);
            double hearing = Clear(l => l.Muffle);
            double haze = Clear(l => l.Haze);
            double settle = Clear(l => l.Settle);
            double focus = Clear(l => l.Blur);

            Assert.That(veil * WakeTiming.Standard.WakeSeconds, Is.LessThanOrEqualTo(0.8 + 1e-9),
                "the veil should be gone within 0.8 s");
            Assert.That(hearing, Is.LessThan(focus), "sound should come back before sight");
            Assert.That(focus, Is.GreaterThan(haze));
            Assert.That(focus, Is.GreaterThan(settle));
            Assert.That(focus, Is.GreaterThan(hearing));
            Assert.That(focus, Is.GreaterThan(veil), "focus is the last thing to arrive");
        }

        // ------------------------------------------------------------ skip

        [Test]
        public void ASkipWhileTheMenuIsClosingIsIgnored()
        {
            var wake = Dreaming();
            wake.Begin();
            wake.Step(Frame);
            wake.Skip();
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Closing));
            StepToBuild(wake);
            Assert.That(wake.BuildDue, Is.True, "a skip while closing stopped the build");
        }

        [Test]
        public void ASkipBehindTheCoverIsKeptAndTakenWhenTheVeilOpens()
        {
            var wake = Dreaming();
            wake.Begin();
            Build(wake);
            wake.Skip();
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Covered), "the cover frames are not skippable");
            Assert.That(wake.HoldsClock, Is.True);
            while (wake.Phase == WakePhase.Covered) wake.Step(Frame);
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Rushing));
            Assert.That(wake.HoldsClock, Is.False);
        }

        [Test]
        public void ASkipInTheDreamReleasesTheClockAtOnceAndClearsWithinTheRush()
        {
            var wake = Dreaming();
            wake.Begin();
            Build(wake);
            while (wake.Phase == WakePhase.Covered) wake.Step(Frame);
            for (int i = 0; i < 60; i++) wake.Step(Frame); // a second into the dream
            Assert.That(wake.HoldsClock, Is.True);
            Assert.That(wake.CatchesInput, Is.True);

            wake.Skip();
            Assert.That(wake.HoldsClock, Is.False, "the colony was still held after the player woke");
            Assert.That(wake.CatchesInput, Is.False, "the veil still took the pointer after the player woke");

            double seconds = 0d;
            while (wake.Active)
            {
                wake.Step(Frame);
                seconds += Frame;
            }
            Assert.That(seconds, Is.LessThanOrEqualTo(WakeTiming.Standard.RushSeconds + 2 * Frame));
            Assert.That(wake.Look.Blur, Is.EqualTo(0f));
            Assert.That(wake.Look.Settle, Is.EqualTo(0f));
            Assert.That(wake.Look.Muffle, Is.EqualTo(0f));
        }

        [Test]
        public void ARushNeverBrightensWhatWasAlreadyClearing()
        {
            var wake = Dreaming();
            wake.Begin();
            Build(wake);
            while (wake.Phase == WakePhase.Covered) wake.Step(Frame);
            for (int i = 0; i < 150; i++) wake.Step(Frame);
            WakeLook at = wake.Look;
            wake.Skip();
            wake.Step(Frame);
            Assert.That(wake.Look.Blur, Is.LessThanOrEqualTo(at.Blur));
            Assert.That(wake.Look.Veil, Is.LessThanOrEqualTo(at.Veil));
            Assert.That(wake.Look.Settle, Is.LessThanOrEqualTo(at.Settle));
        }

        [Test]
        public void AbortPutsEverythingBack()
        {
            var wake = Dreaming();
            wake.Begin();
            StepToBuild(wake);
            wake.Abort();
            Assert.That(wake.Phase, Is.EqualTo(WakePhase.Idle));
            Assert.That(wake.Active, Is.False);
            Assert.That(wake.HoldsClock, Is.False);
            Assert.That(wake.CatchesInput, Is.False);
            Assert.That(wake.Look.Veil, Is.EqualTo(0f));
            Assert.That(wake.Begin(), Is.True, "an aborted passage should let the player press Start again");
        }

        // ------------------------------------------------------------ the sound and the camera

        [Test]
        public void TheSoundIsHeardThroughALogSpacedFilterThatOpensFully()
        {
            Assert.That(new WakeLook(0, 0, 0, 0, 0f).CutoffHz, Is.EqualTo(22000f).Within(0.5f));
            Assert.That(new WakeLook(0, 0, 0, 0, 1f).CutoffHz, Is.EqualTo(500f).Within(0.5f));
            Assert.That(new WakeLook(0, 0, 0, 0, 0.5f).CutoffHz,
                Is.EqualTo((float)Math.Sqrt(22000d * 500d)).Within(1f), "the half-way muffle is the geometric mean");

            Assert.That(new WakeLook(0, 0, 0, 0, 0f).ListenerGain, Is.EqualTo(1f));
            Assert.That(new WakeLook(0, 0, 0, 0, 1f).ListenerGain, Is.EqualTo(0.35f).Within(1e-6f));

            Assert.That(new WakeLook(0, 0, 0, 0, 0f).ReverbRoomMb, Is.EqualTo(-10000f), "the room is off when the sound is clear");
            Assert.That(new WakeLook(0, 0, 0, 0, 1f).ReverbRoomMb, Is.EqualTo(-600f));
            float last = -10000f;
            for (int i = 0; i <= 100; i++)
            {
                float room = new WakeLook(0, 0, 0, 0, i / 100f).ReverbRoomMb;
                Assert.That(room, Is.GreaterThanOrEqualTo(last), "the room should drain away, not jump");
                last = room;
            }
        }

        [Test]
        public void TheCameraStartsHigherAndFurtherAndLandsExactlyOnItsPose()
        {
            WakeLook start = WakeTransition.Dreaming(0d);
            Assert.That(start.SettlePitchDeg, Is.GreaterThan(0f), "the camera should start higher");
            Assert.That(start.SettleDistanceFactor, Is.GreaterThan(0f), "the camera should start further out");
            WakeLook end = WakeTransition.Dreaming(1d);
            Assert.That(end.SettlePitchDeg, Is.EqualTo(0f));
            Assert.That(end.SettleYawDeg, Is.EqualTo(0f));
            Assert.That(end.SettleDistanceFactor, Is.EqualTo(0f));
        }

        [Test]
        public void TheWholeDreamTakesAboutFiveSeconds()
        {
            var wake = Dreaming();
            wake.Begin();
            Build(wake);
            var phases = new List<WakePhase>();
            double seconds = 0d;
            while (wake.Active)
            {
                wake.Step(Frame);
                if (wake.Phase == WakePhase.Waking) seconds += Frame;
                if (phases.Count == 0 || phases[^1] != wake.Phase) phases.Add(wake.Phase);
            }
            Assert.That(phases, Is.EqualTo(new[] { WakePhase.Covered, WakePhase.Waking, WakePhase.Done }));
            Assert.That(seconds, Is.EqualTo(5d).Within(0.05d));
        }
    }
}
