#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// SK4: a colonist's skill going up is noticed on this side of the seam and said once.
    ///
    /// <para>The published names are spelled out in full here rather than taken from
    /// <c>SkillCatalogue</c>, for the reason the skills tests already give: a test that asks the
    /// thing under test what a key is called agrees with itself whatever either half has been
    /// renamed to, and the name is the whole of the contract between the two assemblies.</para>
    /// </summary>
    public class SkillLevelWatchTests
    {
        const int Mining = 0;

        static WorldSnapshot Frame(int tick = 0)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(10, 10, 4), 1);
            return snapshot;
        }

        /// <summary>Publish one colonist standing at one level in one skill.</summary>
        static void Level(WorldSnapshot snapshot, PawnId pawn, string skill, int level)
        {
            snapshot.AddPawn(new PawnView(pawn, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Mine));
            snapshot.AddPawnAspect(new PawnAspect(
                pawn, AspectKey.Of("odyssey.pawn.skill." + skill + ".level"), level));
        }

        /// <summary>
        /// <b>The rule everything else depends on.</b> A colonist the watch has never seen is
        /// recorded silently, however skilled she already is.
        ///
        /// <para>Without it every colonist announces her whole starting roll the first time she is
        /// published — on a load, on a new session, and on the frame somebody joins the colony.
        /// This is the gesture serial's own rule ("a figure that has never seen this pawn before
        /// must record the serial and pose nothing") arriving at the same place from a different
        /// direction.</para>
        /// </summary>
        [Test]
        public void TheFirstSightOfAColonistIsSilent()
        {
            var watch = new SkillLevelWatch();
            var pawn = new PawnId(1);

            var first = Frame();
            Level(first, pawn, "mining", 8);

            Assert.That(watch.Step(first), Is.Empty,
                "a colonist arriving already skilled announced levels she did not just earn");
            Assert.That(watch.Tracking, Is.EqualTo(1), "and yet she is now being watched");
        }

        [Test]
        public void ALevelReachedIsReportedOnce()
        {
            var watch = new SkillLevelWatch();
            var pawn = new PawnId(1);

            var before = Frame();
            Level(before, pawn, "mining", 3);
            watch.Step(before);

            var after = Frame(tick: 1);
            Level(after, pawn, "mining", 4);

            IReadOnlyList<SkillLevelUp> risen = watch.Step(after).ToList();
            Assert.That(risen.Count, Is.EqualTo(1));
            Assert.That(risen[0].Pawn, Is.EqualTo(pawn));
            Assert.That(risen[0].Level, Is.EqualTo(4), "the level reached, not the number gained");
            Assert.That(SkillCatalogue.All[risen[0].Skill].Key, Is.EqualTo("ui.skill.mining"));

            // The same level, read again, is not news.
            var again = Frame(tick: 2);
            Level(again, pawn, "mining", 4);
            Assert.That(watch.Step(again), Is.Empty, "the same level was announced twice");
        }

        /// <summary>
        /// A skill above level ten decays, so a level can go down. That is not an event — and the
        /// lower level becomes the one that has to be beaten before anything is said again.
        /// </summary>
        [Test]
        public void AFallThroughDecayIsSilentAndBecomesTheNewMark()
        {
            var watch = new SkillLevelWatch();
            var pawn = new PawnId(1);

            var high = Frame();
            Level(high, pawn, "mining", 12);
            watch.Step(high);

            var decayed = Frame(tick: 1);
            Level(decayed, pawn, "mining", 11);
            Assert.That(watch.Step(decayed), Is.Empty, "losing a level is not news");

            // Climbing back to 12 is news again, because 11 is the mark now.
            var back = Frame(tick: 2);
            Level(back, pawn, "mining", 12);
            Assert.That(watch.Step(back).Count, Is.EqualTo(1),
                "after a decay the level has to be earned again, and earning it is news");
        }

        /// <summary>
        /// Two levels between two reads — which a four-times-a-second poll makes possible at speed
        /// three — is one report naming where she got to, not two reports or none.
        /// </summary>
        [Test]
        public void TwoLevelsBetweenReadsIsOneReportOfTheLevelReached()
        {
            var watch = new SkillLevelWatch();
            var pawn = new PawnId(1);

            var before = Frame();
            Level(before, pawn, "mining", 1);
            watch.Step(before);

            var after = Frame(tick: 1);
            Level(after, pawn, "mining", 3);

            IReadOnlyList<SkillLevelUp> risen = watch.Step(after).ToList();
            Assert.That(risen.Count, Is.EqualTo(1), "a jump is one piece of news, not two");
            Assert.That(risen[0].Level, Is.EqualTo(3));
        }

        /// <summary>
        /// A colonist the frame no longer carries is forgotten, so her entry cannot be inherited by
        /// a later pawn and measured against — the shape <c>AlertModel.Forget</c> already has.
        /// </summary>
        [Test]
        public void AColonistWhoLeavesTheFrameIsForgotten()
        {
            var watch = new SkillLevelWatch();
            var pawn = new PawnId(1);

            var present = Frame();
            Level(present, pawn, "mining", 5);
            watch.Step(present);
            Assert.That(watch.Tracking, Is.EqualTo(1));

            watch.Step(Frame(tick: 1));
            Assert.That(watch.Tracking, Is.Zero, "a colonist off the frame is still being watched");

            // And coming back is a first sight again, not a fall from 5 or a rise to it.
            var returned = Frame(tick: 2);
            Level(returned, pawn, "mining", 9);
            Assert.That(watch.Step(returned), Is.Empty);
        }

        /// <summary>
        /// Every colonist is watched, not merely whoever is selected — the aspects are published
        /// for all of them and a colony announces the level-ups of colonists nobody is looking at.
        /// </summary>
        [Test]
        public void EveryColonistIsWatchedAndNotOnlyASelectedOne()
        {
            var watch = new SkillLevelWatch();
            var one = new PawnId(1);
            var two = new PawnId(2);

            var before = Frame();
            Level(before, one, "mining", 2);
            Level(before, two, "cutting", 2);
            watch.Step(before);

            var after = Frame(tick: 1);
            Level(after, one, "mining", 3);
            Level(after, two, "cutting", 3);

            IReadOnlyList<SkillLevelUp> risen = watch.Step(after).ToList();
            Assert.That(risen.Count, Is.EqualTo(2));
            Assert.That(risen.Select(r => r.Pawn), Is.EquivalentTo(new[] { one, two }));
        }

        /// <summary>
        /// A skill with no simulation behind it is not watched at all. Nothing publishes a level
        /// for it, so there is nothing to compare, and a row that cannot move cannot be news.
        /// </summary>
        [Test]
        public void ASkillWithNoSimulationIsNeverReported()
        {
            var watch = new SkillLevelWatch();
            var pawn = new PawnId(1);

            var before = Frame();
            Level(before, pawn, "social", 1);
            watch.Step(before);

            var after = Frame(tick: 1);
            Level(after, pawn, "social", 5);

            // Social, since cooking went live with the kitchen (design 48).
            Assert.That(watch.Step(after), Is.Empty, "social is not simulated and cannot level");
            Assert.That(watch.Tracking, Is.Zero, "nothing dead is being tracked");
        }
    }

    /// <summary>
    /// SK4: the transient toast stack — design 09 §2.3's own word for an event said once and gone,
    /// as against an alert (a condition) and a bulletin (an event kept until dismissed).
    /// </summary>
    public class ToastModelTests
    {
        static WorldSnapshot Frame(int tick = 0)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(10, 10, 4), 1);
            return snapshot;
        }

        static void Level(WorldSnapshot snapshot, PawnId pawn, string skill, int level)
        {
            snapshot.AddPawn(new PawnView(pawn, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Mine));
            snapshot.AddPawnAspect(new PawnAspect(
                pawn, AspectKey.Of("odyssey.pawn.skill." + skill + ".level"), level));
        }

        [Test]
        public void ALevelUpRaisesOneToastNamingTheColonistTheSkillAndTheLevel()
        {
            var toasts = new ToastModel();
            var pawn = new PawnId(1);

            var before = Frame();
            Level(before, pawn, "mining", 4);
            toasts.Refresh(before, seconds: 0.0);
            Assert.That(toasts.Rows, Is.Empty, "the first sight of a colonist says nothing");

            var after = Frame(tick: 1);
            Level(after, pawn, "mining", 5);
            toasts.Refresh(after, seconds: 1.0);

            Assert.That(toasts.Rows.Count, Is.EqualTo(1));
            ToastRow row = toasts.Rows[0];
            Assert.That(row.Key, Is.EqualTo("ui.toast.skillup"));
            Assert.That(row.Pawn, Is.EqualTo(pawn));
            Assert.That(row.Severity, Is.EqualTo(AlertSeverity.Notice),
                "a toast is good news by being a toast; Notice is the level it is said at");

            // The words come from the registry with the three varying things filled in, so the
            // wiki and the screen cannot disagree. Nothing is left unsubstituted.
            //
            // All three are named individually rather than relying on the "{" sweep alone,
            // because that sweep passes on a registry line somebody has trimmed: drop {level}
            // from icon-keys.csv and the toast silently stops saying which level was reached,
            // with no placeholder left to catch. The name is the one this comment used to claim
            // and not check.
            Assert.That(row.Lead, Does.Contain(ColonistNames.Of(snapshot: after, id: pawn)),
                "the colonist's own name, from the one place names are decided");
            Assert.That(row.Lead, Does.Contain("Mining"), "the skill's own registry word");
            Assert.That(row.Lead, Does.Contain("5"), "the level reached");
            Assert.That(row.Lead, Does.Not.Contain("{"), "a placeholder reached the screen");
        }

        /// <summary>
        /// The line comes in three pieces so the view can draw the level in its own colour (owner,
        /// 2026-09-21), and the pieces must still be the line.
        ///
        /// <para><b>This is the assertion that makes the split safe to have made at all.</b> Three
        /// strings where there was one is three chances for the sentence to come apart - a dropped
        /// space, a piece written twice, a piece left behind from the row before. None of those
        /// would throw and none would fail any other test here, because every other test reads
        /// <see cref="ToastRow.Lead"/>, which is built from the pieces and would stay correct while
        /// what the player reads went wrong. So the identity is asserted directly: the three
        /// concatenate to the whole line, in that order, with nothing added and nothing lost.</para>
        ///
        /// <para>The emphasised piece is the level and only the level. A view that coloured a
        /// piece containing the skill name as well would put half the sentence in amber, which is
        /// the opposite of "so you can see the value clear".</para>
        /// </summary>
        [Test]
        public void TheLineComesInThreePiecesThatStillMakeTheLine()
        {
            var toasts = new ToastModel();
            var pawn = new PawnId(1);

            var before = Frame();
            Level(before, pawn, "mining", 4);
            toasts.Refresh(before, seconds: 0.0);

            var after = Frame(tick: 1);
            Level(after, pawn, "mining", 5);
            toasts.Refresh(after, seconds: 1.0);

            ToastRow row = toasts.Rows[0];

            Assert.That(row.LeadBefore + row.Emphasis + row.LeadAfter, Is.EqualTo(row.Lead),
                "the three pieces are not the line the rest of this file reads");

            Assert.That(row.Emphasis, Is.EqualTo("5"),
                "the emphasised piece is the level and nothing else");
            Assert.That(row.LeadBefore, Does.Not.Contain("5"),
                "the level has been left in the plain piece as well as the coloured one");
            Assert.That(row.LeadBefore, Does.Contain("Mining"),
                "the skill belongs in the plain piece, not the amber one");
            Assert.That(row.Lead, Does.Not.Contain(ToastModel.LevelPlaceholder),
                "the placeholder survived the split");
        }

        /// <summary>
        /// A row raised by anything that is not a level-up draws as one plain piece. The stack's
        /// obvious second customer is the rejection notice design 09 §2.3 named, and it has no
        /// number to colour - so the split has to be optional rather than something every future
        /// caller has to know about.
        /// </summary>
        [Test]
        public void ARowWithNothingToEmphasiseIsAllOnePiece()
        {
            var row = new ToastRow("ui.toast.skillup", "nothing to see", new PawnId(1),
                raised: 0.0, AlertSeverity.Notice, serial: 1);

            Assert.That(row.LeadBefore, Is.EqualTo("nothing to see"));
            Assert.That(row.Emphasis, Is.Empty);
            Assert.That(row.LeadAfter, Is.Empty);
            Assert.That(row.LeadBefore + row.Emphasis + row.LeadAfter, Is.EqualTo(row.Lead));
        }

        [Test]
        public void AToastExpiresOnItsOwnAndCannotBeDismissed()
        {
            var toasts = new ToastModel();
            var pawn = new PawnId(1);

            var before = Frame();
            Level(before, pawn, "mining", 4);
            toasts.Refresh(before, 0.0);

            var after = Frame(tick: 1);
            Level(after, pawn, "mining", 5);
            toasts.Refresh(after, 10.0);
            Assert.That(toasts.Rows.Count, Is.EqualTo(1));

            // Still there just before its time.
            var still = Frame(tick: 2);
            Level(still, pawn, "mining", 5);
            toasts.Refresh(still, 10.0 + ToastModel.LifetimeSeconds - 0.01);
            Assert.That(toasts.Rows.Count, Is.EqualTo(1), "it left early");

            toasts.Refresh(still, 10.0 + ToastModel.LifetimeSeconds);
            Assert.That(toasts.Rows, Is.Empty, "it outstayed its lifetime");
        }

        /// <summary>
        /// The stack is capped, because it sits under the alerts panel and the one thing a passing
        /// toast must never do is bury a starving colonist. The oldest goes: the newest is the news.
        /// </summary>
        [Test]
        public void TheStackIsCappedAndTheOldestGoesFirst()
        {
            var toasts = new ToastModel();

            for (int i = 0; i < ToastModel.MaxRows + 3; i++)
                toasts.Raise(new ToastRow("ui.toast.skillup", "row " + i, new PawnId(i),
                    raised: 0.0, AlertSeverity.Notice, serial: i));

            Assert.That(toasts.Rows.Count, Is.EqualTo(ToastModel.MaxRows));
            Assert.That(toasts.Rows[0].Lead, Is.EqualTo("row 3"), "the oldest should have gone");
            Assert.That(toasts.Rows[^1].Lead, Is.EqualTo("row " + (ToastModel.MaxRows + 2)),
                "the newest belongs at the bottom, nearest the world");
        }

        /// <summary>
        /// One chime for a refresh however many toasts arrived in it — the rule the alerts panel
        /// already follows, because one sound saying "something happened" is the message and the
        /// stack is what says what.
        ///
        /// <para>The count is published for the audio to read <b>because the audio must not detect
        /// a level-up itself.</b> <c>AlertChimeWatch</c>'s own remarks record what that cost the
        /// last time: it kept its own copy of the starvation threshold, the two drifted, and the
        /// chime fired at a hundredth of the food level the red row appears at.</para>
        /// </summary>
        [Test]
        public void SeveralLevelUpsInOneRefreshAreCountedForASingleChime()
        {
            var toasts = new ToastModel();
            var one = new PawnId(1);
            var two = new PawnId(2);

            var before = Frame();
            Level(before, one, "mining", 2);
            Level(before, two, "cutting", 2);
            toasts.Refresh(before, 0.0);
            Assert.That(toasts.Added, Is.Zero, "nothing was raised on the arming refresh");

            var after = Frame(tick: 1);
            Level(after, one, "mining", 3);
            Level(after, two, "cutting", 3);
            toasts.Refresh(after, 1.0);

            Assert.That(toasts.Rows.Count, Is.EqualTo(2));
            Assert.That(toasts.Added, Is.EqualTo(2), "the audio reads this, and plays one sound");
            Assert.That(toasts.LoudestAdded, Is.EqualTo(AlertSeverity.Notice));

            // A refresh that raises nothing says so, or the chime would repeat for ever.
            var quiet = Frame(tick: 2);
            Level(quiet, one, "mining", 3);
            Level(quiet, two, "cutting", 3);
            toasts.Refresh(quiet, 2.0);
            Assert.That(toasts.Added, Is.Zero);
        }

        /// <summary>
        /// A clock that goes backwards — a reloaded session, a rig whose unscaled time restarted —
        /// clears the stack rather than stranding a row on it for ever. A stuck toast is worse than
        /// a missed one, because it never stops being wrong.
        /// </summary>
        [Test]
        public void AClockThatGoesBackwardsClearsTheStackRatherThanStickingIt()
        {
            var toasts = new ToastModel();
            toasts.Raise(new ToastRow("ui.toast.skillup", "row", new PawnId(1),
                raised: 100.0, AlertSeverity.Notice, serial: 1));

            toasts.Refresh(Frame(), seconds: 1.0);
            Assert.That(toasts.Rows, Is.Empty);
        }

        [Test]
        public void EveryKeyTheStackCanDrawIsInTheRegistry()
        {
            foreach (string key in ToastModel.IconKeys)
                Assert.That(Registry.Label(key), Is.Not.Empty, $"{key} has no registry name");
        }

        /// <summary>
        /// <b>A colony going away takes its marks with it</b>, which is the one case the watch's
        /// own housekeeping cannot reach.
        ///
        /// <para><see cref="SkillLevelWatch"/> forgets a colonist who is missing from the frame it
        /// is given, and between two colonies it is given no frame at all — the interface is on
        /// the main menu and nothing is stepped. The next colony then hands it a
        /// <see cref="PawnId"/> 1 who is a different person, and if she happens to be the better
        /// miner she announces a level she arrived with. The first-sight rule is what stops that
        /// everywhere else; this is the door it does not watch, and <c>HudShell</c> closes it on
        /// every session change.</para>
        /// </summary>
        [Test]
        public void AColonyGoingAwayTakesItsLevelMarksWithIt()
        {
            var toasts = new ToastModel();
            var pawn = new PawnId(1);

            var old = Frame();
            Level(old, pawn, "mining", 4);
            toasts.Refresh(old, seconds: 0.0);
            Assert.That(toasts.Rows, Is.Empty, "the first sight of a colonist says nothing");

            // The colony ends. The rows would have drained on their own; the mark would not.
            toasts.Clear();
            Assert.That(toasts.Added, Is.Zero, "a cleared stack still claims to have added a row");

            // A new colony, whose first colonist is the better miner. She is a stranger.
            var fresh = Frame(tick: 1);
            Level(fresh, pawn, "mining", 9);
            toasts.Refresh(fresh, seconds: 1.0);

            Assert.That(toasts.Rows, Is.Empty,
                "a new colony's first colonist announced a level she was rolled with");

            // And she is being watched from where she actually stands, not from the last
            // colony's mark: the next real rise is hers and is reported once.
            var later = Frame(tick: 2);
            Level(later, pawn, "mining", 10);
            toasts.Refresh(later, seconds: 2.0);

            Assert.That(toasts.Rows.Count, Is.EqualTo(1), "the new colonist's own rise was missed");
            Assert.That(toasts.Rows[0].Lead, Does.Contain("10"));
        }
    }
}
