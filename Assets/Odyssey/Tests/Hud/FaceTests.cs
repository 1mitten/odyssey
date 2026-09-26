#nullable enable
using System;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Faces and talking (design 59): the six expressions, the blink, the speaker's and the
    /// listener's head, and the turns of a conversation — stepped at sixty frames a second for as
    /// long as a person would have to watch, in milliseconds.
    /// </summary>
    public class FaceTests
    {
        const float Frame = 1f / 60f;

        static readonly FaceExpression[] All = (FaceExpression[])Enum.GetValues(typeof(FaceExpression));

        // ------------------------------------------------------------------ the expressions

        [Test]
        public void NeutralIsTheArtAsPainted()
        {
            Assert.That(FacePose.Of(FaceExpression.Neutral), Is.EqualTo(FacePose.Rest));
            Assert.That(FacePose.Rest.BrowLift, Is.EqualTo(0f));
            Assert.That(FacePose.Rest.BrowRoll, Is.EqualTo(0f));
            Assert.That(FacePose.Rest.EyeOpen, Is.EqualTo(1f));
            Assert.That(FacePose.Rest.EyeSize, Is.EqualTo(1f));
        }

        [Test]
        public void NoTwoExpressionsAreTheSameFace()
        {
            for (int i = 0; i < All.Length; i++)
                for (int j = i + 1; j < All.Length; j++)
                    Assert.That(FacePose.Of(All[i]), Is.Not.EqualTo(FacePose.Of(All[j])),
                        $"{All[i]} and {All[j]} would look identical");
        }

        [Test]
        public void TheExpressionsStayWithinWhatWasPhotographed()
        {
            // e-15's sheets: larger lifts put the male brow band into the hairline, larger rolls
            // pull the brows off the face.
            foreach (FaceExpression e in All)
            {
                FacePose p = FacePose.Of(e);
                Assert.That(p.BrowLift, Is.InRange(-0.008f, 0.020f), e.ToString());
                Assert.That(Math.Abs(p.BrowRoll), Is.LessThanOrEqualTo(12f), e.ToString());
                // Asleep is shut; Pained is screwed up tighter than tired.
                float least = e == FaceExpression.Asleep ? FaceMotion.BlinkShutOpen : 0.35f;
                Assert.That(p.EyeOpen, Is.InRange(least, 1.2f), e.ToString());
                Assert.That(p.EyeSize, Is.InRange(1f, 1.2f), e.ToString());
            }
        }

        [Test]
        public void AnExpressionIsReachedInAboutAThirdOfASecondAndHeld()
        {
            FaceMotion face = FaceMotion.Start(7);
            face.Expression = FaceExpression.Stern;
            FacePose stern = FacePose.Of(FaceExpression.Stern);

            Run(ref face, 0.05f);
            Assert.That(face.Pose.BrowLift, Is.LessThan(stern.BrowLift * 0.2f).And.GreaterThan(stern.BrowLift * 0.9f),
                "eased, not snapped: a twentieth of a second in, it is on its way and not there");

            Run(ref face, 0.45f);
            // The brows only: the eyes may be mid-blink at any instant.
            Assert.That(face.Pose.BrowLift, Is.EqualTo(stern.BrowLift).Within(0.0002f));

            Run(ref face, 5f);
            Assert.That(face.Pose.BrowLift, Is.EqualTo(stern.BrowLift).Within(0.0002f), "and it holds");
        }

        // ------------------------------------------------------------------ the blink

        [Test]
        public void EveryFaceBlinksEveryFewSecondsAndOnlyItsEyesMove()
        {
            FaceMotion face = FaceMotion.Start(11);
            int blinks = 0;
            bool was = false;
            float shut = 1f;
            for (int f = 0; f < 60 * 60; f++)
            {
                face.Step(Frame);
                if (face.Blinking && !was) blinks++;
                was = face.Blinking;
                shut = Math.Min(shut, face.Pose.EyeOpen);
                Assert.That(face.Pose.BrowLift, Is.EqualTo(0f), "a blink never moves the brows");
                if (!face.Blinking) Assert.That(face.Pose.EyeOpen, Is.EqualTo(1f).Within(1e-5f));
            }
            // A minute: at most one every 2.2 s plus the doubles, at least one every 6 s.
            Assert.That(blinks, Is.InRange(8, 40), $"{blinks} blinks in a minute");
            Assert.That(shut, Is.LessThanOrEqualTo(FaceMotion.BlinkShutOpen + 0.01f), "the eyes really close");
        }

        [Test]
        public void ABlinkLastsAboutAFifthOfASecond()
        {
            FaceMotion face = FaceMotion.Start(3);
            while (!face.Blinking) face.Step(Frame);
            int frames = 0;
            while (face.Blinking) { face.Step(Frame); frames++; }
            float seconds = frames * Frame;
            Assert.That(seconds, Is.EqualTo(FaceMotion.BlinkClose + FaceMotion.BlinkShut + FaceMotion.BlinkOpen)
                .Within(2 * Frame));
        }

        [Test]
        public void ATiredFaceBlinksShutRatherThanToLessThanShut()
        {
            FaceMotion face = FaceMotion.Start(5);
            face.Expression = FaceExpression.Tired;
            float least = 1f;
            for (int f = 0; f < 60 * 30; f++) { face.Step(Frame); least = Math.Min(least, face.Pose.EyeOpen); }
            Assert.That(least, Is.EqualTo(FaceMotion.BlinkShutOpen).Within(0.01f),
                "overridden, not multiplied: 0.45 x 0.08 would be a slit narrower than a shut eye");
        }

        [Test]
        public void TwoColonistsDoNotBlinkInStep()
        {
            FaceMotion a = FaceMotion.Start(1), b = FaceMotion.Start(2);
            int together = 0, either = 0;
            for (int f = 0; f < 60 * 60; f++)
            {
                a.Step(Frame); b.Step(Frame);
                if (a.Blinking || b.Blinking) either++;
                if (a.Blinking && b.Blinking) together++;
            }
            Assert.That(either, Is.GreaterThan(0));
            Assert.That(together, Is.LessThan(either / 3), "seeded per pawn, so the colony does not blink as one");
        }

        [Test]
        public void TheSamePawnBlinksTheSameWayEveryTime()
        {
            FaceMotion a = FaceMotion.Start(42), b = FaceMotion.Start(42);
            for (int f = 0; f < 60 * 20; f++)
            {
                a.Step(Frame); b.Step(Frame);
                Assert.That(a.Pose, Is.EqualTo(b.Pose));
            }
        }

        // ------------------------------------------------------------------ pausing

        [Test]
        public void APausedFaceHoldsItsFrame()
        {
            FaceMotion face = FaceMotion.Start(9);
            face.Role = TalkRole.Speaking;
            face.Expression = FaceExpression.Raised;
            Run(ref face, 1.3f);
            FacePose pose = face.Pose;
            float nod = face.NodPitch, roll = face.NodRoll;
            for (int f = 0; f < 600; f++) face.Step(0f);
            Assert.That(face.Pose, Is.EqualTo(pose));
            Assert.That(face.NodPitch, Is.EqualTo(nod));
            Assert.That(face.NodRoll, Is.EqualTo(roll));
        }

        [Test]
        public void AHitchCountsAsATenthOfASecond()
        {
            // A two-second stall must not land the face at the end of whatever it was doing: it
            // resumes as if a tenth of a second had passed.
            FaceMotion hitched = FaceMotion.Start(13), smooth = FaceMotion.Start(13);
            hitched.Expression = smooth.Expression = FaceExpression.Stern;
            hitched.Step(2f);
            smooth.Step(FaceMotion.LongestStep);
            Assert.That(hitched.Pose, Is.EqualTo(smooth.Pose));
            Assert.That(hitched.Pose.BrowLift, Is.GreaterThan(FacePose.Of(FaceExpression.Stern).BrowLift * 0.9f),
                "and so it is not yet all the way to stern");
        }

        // ------------------------------------------------------------------ talking

        [Test]
        public void SomebodyNotTalkingNeverNods()
        {
            FaceMotion face = FaceMotion.Start(17);
            for (int f = 0; f < 60 * 20; f++)
            {
                face.Step(Frame);
                Assert.That(face.NodPitch, Is.EqualTo(0f));
                Assert.That(face.NodRoll, Is.EqualTo(0f));
            }
        }

        [Test]
        public void ASpeakerNodsInPhrasesWithStillnessBetween()
        {
            FaceMotion face = FaceMotion.Start(19);
            face.Role = TalkRole.Speaking;
            float most = 0f, browMost = 0f;
            int pauses = 0;
            float pausedFor = 0f, lastNod = 0f;
            bool phrasing = false;
            var endOfPause = new System.Collections.Generic.List<float>();
            for (int f = 0; f < 60 * 20; f++)
            {
                face.Step(Frame);
                most = Math.Max(most, face.NodPitch);
                browMost = Math.Max(browMost, face.Pose.BrowLift);
                Assert.That(face.NodPitch, Is.GreaterThanOrEqualTo(-FaceMotion.QuestionChin - 0.01f),
                    "a question lifts the chin, and nothing lifts it further");
                if (phrasing && !face.InPhrase) { pauses++; pausedFor = 0f; }
                if (!face.InPhrase) pausedFor += Frame;
                if (!phrasing && face.InPhrase && pausedFor >= FaceMotion.PauseMin - Frame) endOfPause.Add(lastNod);
                phrasing = face.InPhrase;
                lastNod = face.NodPitch;
            }
            Assert.That(most, Is.InRange(FaceMotion.BeatNod, FaceMotion.BeatNod + FaceMotion.EmphasisNod + 0.01f),
                "beats and emphases, and never more than both together");
            Assert.That(browMost, Is.GreaterThan(FaceMotion.EmphasisBrow * 0.5f), "the brows lift on an emphasis");
            // Twenty seconds of phrases up to 2.6 s and pauses up to 0.7 s.
            Assert.That(pauses, Is.GreaterThanOrEqualTo(5), $"{pauses} pauses");
            Assert.That(endOfPause, Is.Not.Empty);
            foreach (float nod in endOfPause)
                Assert.That(nod, Is.LessThan(most * 0.35f), "by the end of a pause the head has settled");
        }

        [Test]
        public void AListenerNodsSlowlyAndSeldom()
        {
            FaceMotion speaker = FaceMotion.Start(23), listener = FaceMotion.Start(24);
            speaker.Role = TalkRole.Speaking;
            listener.Role = TalkRole.Listening;
            int speakerPeaks = 0, listenerPeaks = 0;
            float sPrev = 0f, sPrev2 = 0f, lPrev = 0f, lPrev2 = 0f, browMost = 0f;
            for (int f = 0; f < 60 * 30; f++)
            {
                speaker.Step(Frame); listener.Step(Frame);
                if (sPrev > sPrev2 && sPrev > speaker.NodPitch && sPrev > 1f) speakerPeaks++;
                if (lPrev > lPrev2 && lPrev > listener.NodPitch && lPrev > 1f) listenerPeaks++;
                sPrev2 = sPrev; sPrev = speaker.NodPitch;
                lPrev2 = lPrev; lPrev = listener.NodPitch;
                browMost = Math.Max(browMost, listener.Pose.BrowLift);
                Assert.That(listener.NodPitch, Is.LessThanOrEqualTo(FaceMotion.ListenNod + 1e-4f));
            }
            // Thirty seconds at one nod every 1.6-3.6 s (plus the half second of the nod itself).
            // Some replies are a brow raise or a tilt rather than a nod, so fewer nods than replies.
            Assert.That(listenerPeaks, Is.InRange(4, 16), $"{listenerPeaks} listener nods");
            Assert.That(speakerPeaks, Is.GreaterThan(listenerPeaks * 3), "a speaker beats; a listener agrees");
            Assert.That(browMost, Is.LessThanOrEqualTo(FaceMotion.ListenBrow + 1e-5f),
                "a listener raises her brows now and then, never further than that");
        }

        [Test]
        public void StoppingTalkingSettlesTheHeadRatherThanCuttingIt()
        {
            FaceMotion face = FaceMotion.Start(29);
            face.Role = TalkRole.Speaking;
            float before = 0f;
            for (int f = 0; f < 60 * 5 && before < 2f; f++) { face.Step(Frame); before = face.NodPitch; }
            Assume.That(before, Is.GreaterThanOrEqualTo(2f));
            face.Role = TalkRole.None;
            face.Step(Frame);
            Assert.That(face.NodPitch, Is.GreaterThan(before * 0.5f), "one frame later it is still mostly there");
            Run(ref face, 1f);
            Assert.That(Math.Abs(face.NodPitch), Is.LessThan(0.05f), "and a second later it has gone");
            Assert.That(Math.Abs(face.NodRoll), Is.LessThan(0.05f));
        }

        // ------------------------------------------------------------------ conversation

        [Test]
        public void TwoPeopleTakeTurnsAndOnlyOneSpeaksAtATime()
        {
            var talk = new Conversation(3, 8, 30f);
            int swaps = 0;
            bool aSpeaking = talk.ASpeaking;
            for (int f = 0; f < 60 * 30 && talk.Step(Frame); f++)
            {
                TalkRole a = talk.RoleOf(3), b = talk.RoleOf(8);
                Assert.That(a == TalkRole.Speaking ^ b == TalkRole.Speaking, Is.True, "exactly one speaker");
                Assert.That(a == TalkRole.Listening ^ b == TalkRole.Listening, Is.True, "and one listener");
                if (talk.ASpeaking != aSpeaking) { swaps++; aSpeaking = talk.ASpeaking; }
            }
            // Thirty seconds of turns 2.5-6 s long.
            Assert.That(swaps, Is.InRange(30 / 6 - 1, 30 / 2));
            Assert.That(talk.RoleOf(99), Is.EqualTo(TalkRole.None), "a bystander is not in it");
        }

        [Test]
        public void AConversationEndsWhenItsTimeIsUp()
        {
            var talk = new Conversation(1, 2, 3f);
            int frames = 0;
            while (talk.Step(Frame)) frames++;
            Assert.That(frames * Frame, Is.EqualTo(3f).Within(2 * Frame));
            Assert.That(talk.Step(0f), Is.False, "and stays ended");
        }

        [Test]
        public void TalkingToNobodyAlternatesWithSilence()
        {
            var talk = new Conversation(4, Conversation.Nobody, 30f);
            bool spoke = false, silent = false;
            for (int f = 0; f < 60 * 30 && talk.Step(Frame); f++)
            {
                TalkRole a = talk.RoleOf(4);
                Assert.That(a, Is.Not.EqualTo(TalkRole.Listening), "nobody to listen to");
                spoke |= a == TalkRole.Speaking;
                silent |= a == TalkRole.None;
            }
            Assert.That(spoke && silent, Is.True);
            Assert.That(talk.PartnerOf(4), Is.EqualTo(Conversation.Nobody));
        }

        [Test]
        public void EachKnowsTheOther()
        {
            var talk = new Conversation(5, 6, 10f);
            Assert.That(talk.PartnerOf(5), Is.EqualTo(6));
            Assert.That(talk.PartnerOf(6), Is.EqualTo(5));
            Assert.That(talk.PartnerOf(7), Is.EqualTo(Conversation.Nobody));
            Assert.That(talk.Involves(5) && talk.Involves(6) && !talk.Involves(7), Is.True);
        }

        // ------------------------------------------------------------------ context (§3a)

        static FaceSignals Signals(bool asleep = false, bool downed = false, bool stunned = false,
            bool fleeing = false, bool fighting = false, bool drafted = false, int pain = 0, int rest = 800,
            int mood = 600) =>
            new FaceSignals(asleep, downed, stunned, fleeing, fighting, drafted, pain, rest, mood);

        [Test]
        public void AColonistAtEaseWearsTheFaceAsPainted()
        {
            Assert.That(FaceContext.Expression(Signals()), Is.EqualTo(FaceExpression.Neutral));
            Assert.That(FaceContext.Expression(FaceSignals.AtEase), Is.EqualTo(FaceExpression.Neutral));
        }

        [Test]
        public void EachContextHasItsFace()
        {
            Assert.That(FaceContext.Expression(Signals(drafted: true)), Is.EqualTo(FaceExpression.Stern), "drafted");
            Assert.That(FaceContext.Expression(Signals(fighting: true)), Is.EqualTo(FaceExpression.Stern), "fighting");
            Assert.That(FaceContext.Expression(Signals(rest: FaceContext.TiredBelow - 1)), Is.EqualTo(FaceExpression.Tired));
            Assert.That(FaceContext.Expression(Signals(rest: FaceContext.TiredBelow)), Is.EqualTo(FaceExpression.Neutral),
                "rested enough not to want a bed");
            Assert.That(FaceContext.Expression(Signals(fleeing: true)), Is.EqualTo(FaceExpression.Alarmed));
            Assert.That(FaceContext.Expression(Signals(stunned: true)), Is.EqualTo(FaceExpression.Alarmed));
            Assert.That(FaceContext.Expression(Signals(pain: FaceContext.PainedAbove + 1)), Is.EqualTo(FaceExpression.Pained));
            Assert.That(FaceContext.Expression(Signals(pain: FaceContext.PainedAbove)), Is.EqualTo(FaceExpression.Neutral));
            Assert.That(FaceContext.Expression(Signals(downed: true)), Is.EqualTo(FaceExpression.Pained));
            Assert.That(FaceContext.Expression(Signals(asleep: true)), Is.EqualTo(FaceExpression.Asleep));
            Assert.That(FaceContext.Expression(Signals(mood: MoodBands.Strained - 1)), Is.EqualTo(FaceExpression.Glum));
            Assert.That(FaceContext.Expression(Signals(mood: MoodBands.Strained)), Is.EqualTo(FaceExpression.Neutral));
        }

        [Test]
        public void WhatSheIsDoingToSurviveBeatsHowSheFeels()
        {
            Assert.That(FaceContext.Expression(Signals(drafted: true, rest: 10, mood: 10)), Is.EqualTo(FaceExpression.Stern),
                "a drafted colonist looks stern however tired or unhappy");
            Assert.That(FaceContext.Expression(Signals(drafted: true, pain: 300)), Is.EqualTo(FaceExpression.Stern),
                "a little pain does not show through a fight");
            Assert.That(FaceContext.Expression(Signals(drafted: true, pain: FaceContext.AgonyAbove + 1)),
                Is.EqualTo(FaceExpression.Pained), "agony does");
            Assert.That(FaceContext.Expression(Signals(fighting: true, fleeing: true)), Is.EqualTo(FaceExpression.Alarmed),
                "running away is not fighting");
            Assert.That(FaceContext.Expression(Signals(rest: 10, mood: 10)), Is.EqualTo(FaceExpression.Tired),
                "tired shows before unhappy");
            Assert.That(FaceContext.Expression(Signals(asleep: true, drafted: true, pain: 900)), Is.EqualTo(FaceExpression.Asleep),
                "nothing wakes a sleeping face");
        }

        [Test]
        public void TheSignalsAreReadOffThePublishedPawn()
        {
            var drafted = new PawnView(new PawnId(1), default, 800, 800, 600,
                flags: PawnFlags.Person | PawnFlags.Drafted);
            Assert.That(FaceContext.Expression(FaceSignals.Of(in drafted, 0)), Is.EqualTo(FaceExpression.Stern));
            var swinging = new PawnView(new PawnId(1), default, 800, 800, 600, jobDef: JobHandle.AttackMelee);
            Assert.That(FaceContext.Expression(FaceSignals.Of(in swinging, 0)), Is.EqualTo(FaceExpression.Stern));
            var tired = new PawnView(new PawnId(1), default, 800, 100, 600);
            Assert.That(FaceContext.Expression(FaceSignals.Of(in tired, 0)), Is.EqualTo(FaceExpression.Tired));
            var hurt = new PawnView(new PawnId(1), default, 800, 800, 600);
            Assert.That(FaceContext.Expression(FaceSignals.Of(in hurt, 600)), Is.EqualTo(FaceExpression.Pained));
        }

        [Test]
        public void OnlyAColonistAtLeisureStrikesUpAConversation()
        {
            Assert.That(FaceContext.CanChat(new PawnView(new PawnId(1), default, 800, 800, 600)), Is.True, "idle");
            Assert.That(FaceContext.CanChat(new PawnView(new PawnId(1), default, 800, 800, 600, JobHandle.Wander)), Is.True);
            Assert.That(FaceContext.CanChat(new PawnView(new PawnId(1), default, 800, 800, 600, JobHandle.Eat)), Is.True,
                "over a meal");
            Assert.That(FaceContext.CanChat(new PawnView(new PawnId(1), default, 800, 800, 600, JobHandle.Haul)), Is.False);
            Assert.That(FaceContext.CanChat(new PawnView(new PawnId(1), default, 800, 800, 600, working: true)), Is.False);
            Assert.That(FaceContext.CanChat(new PawnView(new PawnId(1), default, 800, 800, 600, asleep: true)), Is.False);
            Assert.That(FaceContext.CanChat(new PawnView(new PawnId(1), default, 800, 800, 600,
                flags: PawnFlags.Person | PawnFlags.Drafted)), Is.False);
        }

        // ------------------------------------------------------------------ variety (§5a, §5b)

        [Test]
        public void ASpeakerUsesEveryMannerAndBothHands()
        {
            FaceMotion face = FaceMotion.Start(31);
            face.Role = TalkRole.Speaking;
            var manners = new System.Collections.Generic.HashSet<TalkManner>();
            var arms = new System.Collections.Generic.HashSet<TalkArm>();
            float mostYaw = 0f, mostRoll = 0f, mostBrow = 0f, mostBrowRoll = 0f, mostArm = 0f, mostBeat = 0f;
            for (int f = 0; f < 60 * 180; f++)
            {
                face.Step(Frame);
                if (face.InPhrase) manners.Add(face.Manner);
                if (face.ArmLift > 0.9f) arms.Add(face.Arm);
                mostYaw = Math.Max(mostYaw, Math.Abs(face.NodYaw));
                mostRoll = Math.Max(mostRoll, Math.Abs(face.NodRoll));
                mostBrow = Math.Max(mostBrow, face.Pose.BrowLift);
                mostBrowRoll = Math.Max(mostBrowRoll, Math.Abs(face.Pose.BrowRoll));
                mostArm = Math.Max(mostArm, face.ArmLift);
                mostBeat = Math.Max(mostBeat, Math.Abs(face.ElbowBeat));
            }
            Assert.That(manners, Is.EquivalentTo(Enum.GetValues(typeof(TalkManner))), "three minutes uses every manner");
            Assert.That(arms, Is.EquivalentTo(new[] { TalkArm.Right, TalkArm.Left, TalkArm.Both }), "and every hand");
            Assert.That(mostYaw, Is.GreaterThan(5f).And.LessThanOrEqualTo(FaceMotion.MusingYaw + FaceMotion.ShakeYaw),
                "a shake or a glance away turns the head");
            Assert.That(mostRoll, Is.GreaterThan(5f), "a question or a doubt tilts it");
            Assert.That(mostBrow, Is.GreaterThanOrEqualTo(FaceMotion.QuestionBrow * 0.95f), "a question lifts the brows");
            Assert.That(mostBrowRoll, Is.GreaterThan(FaceMotion.TiltBrowRoll * 0.9f), "doubt tilts them");
            Assert.That(mostArm, Is.GreaterThan(0.95f));
            Assert.That(mostBeat, Is.InRange(FaceMotion.ElbowBeatDegrees * 0.5f, FaceMotion.ElbowBeatDegrees + 1e-3f),
                "the talking hand beats with the phrase");
        }

        [Test]
        public void NobodyTalkingMovesAHand()
        {
            FaceMotion face = FaceMotion.Start(37);
            face.Role = TalkRole.Listening;
            for (int f = 0; f < 60 * 60; f++)
            {
                face.Step(Frame);
                Assert.That(face.ArmLift, Is.EqualTo(0f), "a listener keeps her hands down");
                Assert.That(face.Arm, Is.EqualTo(TalkArm.None));
            }
        }

        [Test]
        public void AListenerRepliesInMoreThanOneWay()
        {
            FaceMotion face = FaceMotion.Start(41);
            face.Role = TalkRole.Listening;
            float mostBrow = 0f, mostRoll = 0f, mostNod = 0f;
            for (int f = 0; f < 60 * 120; f++)
            {
                face.Step(Frame);
                mostBrow = Math.Max(mostBrow, face.Pose.BrowLift);
                mostRoll = Math.Max(mostRoll, Math.Abs(face.NodRoll));
                mostNod = Math.Max(mostNod, face.NodPitch);
            }
            Assert.That(mostNod, Is.GreaterThan(FaceMotion.ListenNod * 0.9f), "nods");
            Assert.That(mostBrow, Is.GreaterThan(FaceMotion.ListenBrow * 0.9f), "a brow raised in surprise");
            Assert.That(mostRoll, Is.GreaterThan(FaceMotion.ListenTiltRoll * 0.9f), "a tilt of the head");
            Assert.That(Math.Abs(face.NodYaw), Is.EqualTo(0f), "and a listener does not look away from the speaker");
        }

        [Test]
        public void AGreetingIsAnEyebrowFlash()
        {
            FaceMotion face = FaceMotion.Start(43);
            Run(ref face, 0.5f);
            float before = face.Pose.BrowLift;
            face.Flash();
            float most = 0f;
            for (float t = 0f; t < FaceMotion.FlashSeconds; t += Frame) { face.Step(Frame); most = Math.Max(most, face.Pose.BrowLift); }
            Assert.That(most - before, Is.GreaterThan(FaceMotion.FlashBrow * 0.95f), "up");
            Run(ref face, 0.2f);
            Assert.That(face.Pose.BrowLift, Is.EqualTo(before).Within(1e-5f), "and straight back down");
            Assert.That(face.NodPitch, Is.EqualTo(0f), "with the brows alone");
        }

        [Test]
        public void AQuestionIsHeldThroughThePauseAfterIt()
        {
            FaceMotion face = FaceMotion.Start(47);
            face.Role = TalkRole.Speaking;
            bool heard = false;
            bool wasQuestion = false, phrasing = false;
            for (int f = 0; f < 60 * 120 && !heard; f++)
            {
                face.Step(Frame);
                if (phrasing && !face.InPhrase) wasQuestion = face.Manner == TalkManner.Question;
                if (!face.InPhrase && wasQuestion && face.Pose.BrowLift > FaceMotion.QuestionBrow * 0.8f)
                {
                    Assert.That(face.NodPitch, Is.LessThan(0f), "chin up");
                    Assert.That(Math.Abs(face.NodRoll), Is.GreaterThan(FaceMotion.QuestionRoll * 0.5f), "head on one side");
                    heard = true;
                }
                phrasing = face.InPhrase;
            }
            Assert.That(heard, Is.True, "two minutes of talk held no question through a pause");
        }

        // ------------------------------------------------------------------ the debug tab

        [Test]
        public void TheFacesTabHasOneRowPerExpressionInOrder()
        {
            Assert.That(DebugDirector.FaceRows.Length, Is.EqualTo(All.Length));
            for (int i = 0; i < All.Length; i++)
                Assert.That(DebugDirector.FaceRows[i].Expression, Is.EqualTo(All[i]));
            var keys = new System.Collections.Generic.HashSet<string> { DebugDirector.TalkKey, DebugDirector.AutoFaceKey };
            Assert.That(Registry.Label(DebugDirector.AutoFaceKey), Is.Not.EqualTo(DebugDirector.AutoFaceKey));
            foreach (DebugDirector.FaceRow row in DebugDirector.FaceRows)
            {
                Assert.That(keys.Add(row.Key), Is.True, $"{row.Key} twice");
                Assert.That(Registry.Label(row.Key), Is.Not.EqualTo(row.Key), $"{row.Key} is not in the registry");
            }
            Assert.That(Registry.Label(DebugDirector.TalkKey), Is.Not.EqualTo(DebugDirector.TalkKey));
            Assert.That(DebugDirector.TabKey(DebugTab.Faces), Is.EqualTo(DebugDirector.FacesTabKey));
            Assert.That(Registry.Label(DebugDirector.FacesTabKey), Is.Not.EqualTo(DebugDirector.FacesTabKey));
        }

        // ------------------------------------------------------------------ cost

        [Test]
        public void SixtyFourFacesCostAlmostNothing()
        {
            var faces = new FaceMotion[64];
            for (int i = 0; i < faces.Length; i++)
            {
                faces[i] = FaceMotion.Start(i);
                faces[i].Role = (TalkRole)(i % 3);
                faces[i].Expression = All[i % All.Length];
            }
            const int frames = 6000;
            var clock = Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
                for (int i = 0; i < faces.Length; i++)
                    faces[i].Step(Frame);
            double perFrame = clock.Elapsed.TotalMilliseconds / frames;
            TestContext.WriteLine($"64 faces stepped: {perFrame * 1000.0:F2} us a frame");
            // A hundredfold margin on what was measured: this is a ceiling against a mistake, not a
            // timing (design 59 §9 has the number).
            Assert.That(perFrame, Is.LessThan(0.5), $"{perFrame:F4} ms a frame");
        }

        static void Run(ref FaceMotion face, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame) face.Step(Frame);
        }
    }
}
