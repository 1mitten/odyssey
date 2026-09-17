#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The naming prompt: what it offers, what it will not let the player confirm, and the
    /// ask-twice arming that stands between a second Save and a destroyed colony.
    ///
    /// <para>Every rule here is a promise made with the player's finger already on the button —
    /// "that press armed it", "editing the name withdrew the question you were asked" — and the
    /// prompt raises rather than performs precisely so this tier can hold them. The alternative is
    /// discovering the arming latched the way the build gesture's did, by playing it three
    /// times.</para>
    /// </summary>
    public class SavePromptTests
    {
        static SavePrompt Up(string name, SaveNameStatus status)
        {
            var prompt = new SavePrompt();
            prompt.Show(name, status);
            return prompt;
        }

        // ------------------------------------------------------------------ opening

        [Test]
        public void ItStartsAwayWithNothingTypedAndNothingArmed()
        {
            var prompt = new SavePrompt();

            Assert.That(prompt.Showing, Is.False);
            Assert.That(prompt.Name, Is.Empty);
            Assert.That(prompt.Armed, Is.False);
            Assert.That(prompt.CanConfirm, Is.False, "there is nothing to confirm before it is up");
            Assert.That(prompt.Status, Is.EqualTo(SaveNameStatus.Unusable));
        }

        [Test]
        public void ItOpensPrefilledWithTheOfferedNameAndTheVerdictOnIt()
        {
            SavePrompt prompt = Up("Riverbend day 12", SaveNameStatus.Free);

            Assert.That(prompt.Showing, Is.True);
            Assert.That(prompt.Name, Is.EqualTo("Riverbend day 12"));
            Assert.That(prompt.Status, Is.EqualTo(SaveNameStatus.Free));
            Assert.That(prompt.Collides, Is.False);
            Assert.That(prompt.CanConfirm, Is.True);
            Assert.That(prompt.Armed, Is.False, "an offered name has not been asked about yet");
            Assert.That(prompt.ActionKey, Is.EqualTo(SavePrompt.ConfirmKey));

            // A suggestion may perfectly well collide — from the second save onwards it usually
            // does — and the prompt takes that verdict at the door rather than on the first
            // keystroke.
            SavePrompt again = Up("Riverbend day 12", SaveNameStatus.Taken);
            Assert.That(again.Collides, Is.True);
            Assert.That(again.Armed, Is.False, "colliding is not the same as having been asked");
            Assert.That(again.ActionKey, Is.EqualTo(SavePrompt.ConfirmKey));
        }

        [Test]
        public void ShowingItAgainReseatsItRatherThanResumingWhereItStopped()
        {
            SavePrompt prompt = Up("Ashford", SaveNameStatus.Taken);
            Assert.That(prompt.Confirm(), Is.False);
            Assert.That(prompt.Armed, Is.True);

            prompt.Show("Bellwether", SaveNameStatus.Free);

            Assert.That(prompt.Name, Is.EqualTo("Bellwether"));
            Assert.That(prompt.Armed, Is.False,
                "an armed overwrite that survived its own box would be a trap set by something not on screen");
        }

        [Test]
        public void NothingReachesItWhileItIsAway()
        {
            var prompt = new SavePrompt();
            int changed = 0, confirmed = 0;
            prompt.Changed += () => changed++;
            prompt.Confirmed += _ => confirmed++;

            // A keystroke arriving now is a late echo from a box the player already dismissed.
            prompt.Type("Ashford", SaveNameStatus.Free);
            Assert.That(prompt.Name, Is.Empty);

            Assert.That(prompt.Confirm(), Is.False);
            prompt.Cancel();

            Assert.That(changed, Is.Zero);
            Assert.That(confirmed, Is.Zero);
        }

        // ------------------------------------------------------------------ confirming

        [Test]
        public void AFreeNameSavesOnOnePress()
        {
            SavePrompt prompt = Up("Bellwether", SaveNameStatus.Free);
            var names = new List<string>();
            prompt.Confirmed += names.Add;

            Assert.That(prompt.Confirm(), Is.True);

            // One press, not two. Asking "are you sure?" about a save that destroys nothing is how
            // a player learns to press twice without reading, which is what makes the real question
            // useless when it finally matters.
            Assert.That(names.Count, Is.EqualTo(1));
            Assert.That(names[0], Is.EqualTo("Bellwether"));
            Assert.That(prompt.Showing, Is.False, "the box is down before the presenter writes the file");
            Assert.That(prompt.Armed, Is.False);
        }

        [Test]
        public void ATakenNameAsksTwiceAndSaysOverwriteWhileItIsAsking()
        {
            SavePrompt prompt = Up("Ashford", SaveNameStatus.Taken);
            var names = new List<string>();
            prompt.Confirmed += names.Add;

            Assert.That(prompt.Confirm(), Is.False, "the first press asks rather than writes");
            Assert.That(prompt.Armed, Is.True);
            Assert.That(prompt.Showing, Is.True);
            Assert.That(names.Count, Is.Zero, "nothing may be destroyed by one press");

            // The word on the button changes with the act. A button that says the same thing before
            // and after the question is a button that did not ask one.
            Assert.That(prompt.ActionKey, Is.EqualTo(SavePrompt.OverwriteKey));

            Assert.That(prompt.Confirm(), Is.True);
            Assert.That(names.Count, Is.EqualTo(1));
            Assert.That(names[0], Is.EqualTo("Ashford"));
            Assert.That(prompt.Showing, Is.False);
            Assert.That(prompt.Armed, Is.False);
        }

        [Test]
        public void AnUnusableNameCannotBeConfirmedAndSaysSoBeforeThePress()
        {
            SavePrompt prompt = Up("!!!", SaveNameStatus.Unusable);
            var names = new List<string>();
            prompt.Confirmed += names.Add;

            // CanConfirm is how the presenter learns to grey the button. A press that silently does
            // nothing reads as a broken button, and the player's next move is to press it harder
            // rather than to change the name — so the state is published as well as enforced.
            Assert.That(prompt.CanConfirm, Is.False);
            Assert.That(prompt.Confirm(), Is.False);
            Assert.That(prompt.Armed, Is.False, "an unusable name has no file to ask about");
            Assert.That(prompt.Showing, Is.True, "refusing is not closing: the player is still naming it");
            Assert.That(names.Count, Is.Zero);

            // And it is enforced here too, not only by whoever draws it: a rule kept only by the
            // presenter is a rule the next presenter does not have.
            prompt.Type("", SaveNameStatus.Unusable);
            Assert.That(prompt.Confirm(), Is.False);
            Assert.That(names.Count, Is.Zero);

            // Typing something usable makes the button live again in the same call.
            prompt.Type("Ashford", SaveNameStatus.Free);
            Assert.That(prompt.CanConfirm, Is.True);
            Assert.That(prompt.Confirm(), Is.True);
            Assert.That(names, Is.EqualTo(new[] { "Ashford" }));
        }

        // ------------------------------------------------------------------ editing and arming

        /// <summary>
        /// <b>The negative control.</b> "Editing the name stands the arming down" is the easiest
        /// rule in this file to test vacuously: an assertion that <c>Armed</c> is false after a
        /// keystroke passes just as well against a prompt that never arms at all, and half the
        /// tests above would still be green if <see cref="SavePrompt.Type"/> simply left the arming
        /// alone. So this one asserts the whole sequence — armed <i>before</i> the edit, the next
        /// press asking again rather than writing, and the name that is finally written being the
        /// new one — and cannot be satisfied by either mistake.
        ///
        /// <para><b>Run, not assumed</b>, against both mistakes in turn. With the
        /// <c>Armed = false;</c> line removed from <c>SavePrompt.Type</c> — the prompt that never
        /// disarms — <c>scripts/test-fast.sh</c> reported <c>Failed: 2, Passed: 235</c> of 237 on
        /// the Hud tier, this test among them, on the press after the edit writing immediately.
        /// With the arming in <c>SavePrompt.Confirm</c> disabled instead — the prompt that never
        /// arms — it reported <c>Failed: 7, Passed: 230</c>, again with this test among them, on
        /// the first press having written. Restored, 237 of 237 pass. A bare
        /// "<c>Armed</c> is false after a keystroke" would have been green in the second run.</para>
        /// </summary>
        [Test]
        public void AnEditAfterArmingAnswersTheNewNameRatherThanTheOld()
        {
            SavePrompt prompt = Up("Ashford", SaveNameStatus.Taken);
            var names = new List<string>();
            prompt.Confirmed += names.Add;

            Assert.That(prompt.Confirm(), Is.False);
            Assert.That(prompt.Armed, Is.True, "the control depends on it genuinely being armed first");

            // The armed state is an answer to one question — overwrite *this* file — and the file
            // is decided by the name. A press that carried across the edit would answer about a
            // file the player is no longer naming.
            prompt.Type("Ashforde", SaveNameStatus.Taken);
            Assert.That(prompt.Armed, Is.False);
            Assert.That(prompt.ActionKey, Is.EqualTo(SavePrompt.ConfirmKey));

            Assert.That(prompt.Confirm(), Is.False, "the new file has not been asked about yet");
            Assert.That(names.Count, Is.Zero);

            Assert.That(prompt.Confirm(), Is.True);
            Assert.That(names, Is.EqualTo(new[] { "Ashforde" }));
        }

        [Test]
        public void TheVerdictChangingCountsAsAnEdit()
        {
            SavePrompt prompt = Up("Ashford", SaveNameStatus.Taken);
            Assert.That(prompt.Confirm(), Is.False);
            Assert.That(prompt.Armed, Is.True);

            // Same spelling, different file underneath — the folder changed, or the session bound
            // itself to this name. It is not the same question, so it is not the same answer.
            prompt.Type("Ashford", SaveNameStatus.Free);

            Assert.That(prompt.Armed, Is.False);
            Assert.That(prompt.Collides, Is.False);
            Assert.That(prompt.ActionKey, Is.EqualTo(SavePrompt.ConfirmKey),
                "a free name must never be drawn with Overwrite on the button");
        }

        [Test]
        public void AKeystrokeThatChangesNothingChangesNothing()
        {
            SavePrompt prompt = Up("Ashford", SaveNameStatus.Taken);
            Assert.That(prompt.Confirm(), Is.False);

            int changed = 0;
            prompt.Changed += () => changed++;

            // A presenter that echoes the field on every key event also echoes the keys that do not
            // alter it. Those must not disarm the question the player is halfway through answering,
            // and must not ask for a redraw.
            prompt.Type("Ashford", SaveNameStatus.Taken);

            Assert.That(prompt.Armed, Is.True);
            Assert.That(changed, Is.Zero);
        }

        [Test]
        public void CancellingLeavesNothingArmedAndNothingWritten()
        {
            SavePrompt prompt = Up("Ashford", SaveNameStatus.Taken);
            int confirmed = 0;
            prompt.Confirmed += _ => confirmed++;

            Assert.That(prompt.Confirm(), Is.False);
            Assert.That(prompt.Armed, Is.True);

            prompt.Cancel();

            Assert.That(prompt.Showing, Is.False);
            Assert.That(prompt.Armed, Is.False);
            Assert.That(prompt.Name, Is.Empty, "a half-typed name must not come back on the next Save");
            Assert.That(confirmed, Is.Zero);

            // And the armed press cannot be completed from outside the box it was made in.
            Assert.That(prompt.Confirm(), Is.False);
            Assert.That(confirmed, Is.Zero);
        }

        [Test]
        public void EveryChangeAsksForOneRedraw()
        {
            var prompt = new SavePrompt();
            int changed = 0;
            prompt.Changed += () => changed++;

            prompt.Show("Ashford", SaveNameStatus.Taken);   // 1: it appeared
            prompt.Type("Bellwether", SaveNameStatus.Taken); // 2: the name
            prompt.Confirm();                                // 3: it armed
            prompt.Confirm();                                // 4: it went away

            Assert.That(changed, Is.EqualTo(4));
        }

        // ------------------------------------------------------------------ the names on it

        [Test]
        public void EveryKeyThePromptDrawsIsARegisteredName()
        {
            // The HUD names nothing itself: a word invented in C# is a word the owner cannot
            // correct in docs/design/icon-keys.csv, and so a word that is not in the wiki.
            foreach (string key in SavePrompt.IconKeys)
            {
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
                Assert.That(Registry.Label(key), Is.Not.Empty);
            }

            Assert.That(SavePrompt.IconKeys, Does.Contain(SavePrompt.ConfirmKey));
            Assert.That(SavePrompt.IconKeys, Does.Contain(SavePrompt.OverwriteKey));

            // The confirm button reuses the session table's Save key rather than minting a
            // ui.prompt.save beside it: one thing, one word, one row for the owner to correct.
            Assert.That(SavePrompt.ConfirmKey, Is.EqualTo(SessionCommands.SaveKey));
        }
    }
}
