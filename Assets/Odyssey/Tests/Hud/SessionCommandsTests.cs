#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The session command table, which exists so the main screen and the settings panel cannot
    /// come to disagree about what the session-level rows are.
    ///
    /// <para><b>The first test is the whole point of the file.</b> Stating each context's column
    /// exactly — every key, in order — is what turns "they are built from one table" from a claim
    /// into a thing that fails when it stops being true. A weaker test that only checked the two
    /// lists were non-empty would pass through every drift it was written to catch.</para>
    /// </summary>
    public class SessionCommandsTests
    {
        static string[] KeysOf(SessionContext context)
        {
            IReadOnlyList<SessionCommand> rows = SessionCommands.For(context);
            var keys = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++) keys[i] = rows[i].Key;
            return keys;
        }

        [Test]
        public void TheMainScreenDrawsTheseRowsInThisOrder()
        {
            Assert.That(KeysOf(SessionContext.MainScreen), Is.EqualTo(new[]
            {
                SessionCommands.NewGameKey,
                SessionCommands.LoadKey,
                SessionCommands.OptionsKey,
                SessionCommands.QuitKey,
            }), "the main screen's column is `17-start-flow.md` §4's picture and changing it is a decision");
        }

        [Test]
        public void TheSettingsPanelDrawsTheseRowsInThisOrder()
        {
            Assert.That(KeysOf(SessionContext.InGame), Is.EqualTo(new[]
            {
                SessionCommands.SaveKey,
                SessionCommands.SaveAsKey,
                SessionCommands.LoadKey,
                SessionCommands.QuitToMenuKey,
                SessionCommands.QuitKey,
            }), "the in-game rows run from what costs nothing to what costs everything");
        }

        /// <summary>
        /// New game has no business in a panel over a running colony, Save has none on a screen
        /// with no world behind it, and Quit to main menu has none where there is no main menu to
        /// go back from. Stated as absences because the two tests above can only catch a row that
        /// moved, not one that appeared somewhere it must never be.
        /// </summary>
        [Test]
        public void NoRowIsOfferedWhereItWouldMakeNoSense()
        {
            Assert.That(SessionCommands.IsDrawn(SessionCommands.NewGameKey, SessionContext.InGame), Is.False,
                "there is already a game");
            Assert.That(SessionCommands.IsDrawn(SessionCommands.SaveKey, SessionContext.MainScreen), Is.False,
                "there is nothing to write");
            Assert.That(SessionCommands.IsDrawn(SessionCommands.QuitToMenuKey, SessionContext.MainScreen), Is.False,
                "this is the main menu");
            Assert.That(SessionCommands.IsDrawn(SessionCommands.OptionsKey, SessionContext.InGame), Is.False,
                "the row would open the panel it is drawn in");

            Assert.That(SessionCommands.IsDrawn("ui.nothing.of.the.kind", SessionContext.MainScreen), Is.False);
        }

        /// <summary>
        /// Options and Quit reuse keys the settings panel already owns rather than minting
        /// <c>ui.session.options</c> and <c>ui.session.quit</c> beside them. Asserted rather than
        /// left to a comment, because the failure of that decision is silent: two keys for one
        /// thing draw two rows in the wiki, two icons to cut, and one of them renamed.
        /// </summary>
        [Test]
        public void OptionsAndQuitReuseTheSettingsPanelsOwnKeys()
        {
            Assert.That(SessionCommands.OptionsKey, Is.EqualTo(SettingsDirector.PanelKey));
            Assert.That(SessionCommands.QuitKey, Is.EqualTo(SettingsDirector.ExitKey));
        }

        [Test]
        public void EveryRowIsNamedByTheRegistryRatherThanByCSharp()
        {
            foreach (SessionContext context in SessionCommands.Contexts)
                foreach (SessionCommand row in SessionCommands.For(context))
                {
                    Assert.That(Registry.Labels, Does.ContainKey(row.Key), $"{row.Key} is not in the registry");
                    Assert.That(row.Label, Is.EqualTo(Registry.Label(row.Key)));
                    Assert.That(row.Label, Is.Not.EqualTo(row.Key),
                        $"{row.Key} falls back to drawing its own key, which is a visible fault");
                }
        }

        /// <summary>
        /// Every key the table can put on screen is in <see cref="SessionCommands.IconKeys"/>, and
        /// every key in it resolves. A key the array forgets is a label nothing checks —
        /// <c>RegistryTests</c> holds the array, so the array has to hold the table.
        /// </summary>
        [Test]
        public void EveryDrawableKeyIsInIconKeysAndResolves()
        {
            var seen = new HashSet<string>();
            foreach (SessionContext context in SessionCommands.Contexts)
                foreach (SessionCommand row in SessionCommands.For(context))
                {
                    seen.Add(row.Key);
                    Assert.That(SessionCommands.IconKeys, Does.Contain(row.Key),
                        $"{row.Key} is drawn and the registry test does not cover it");
                }

            Assert.That(SessionCommands.IconKeys, Is.Unique);
            Assert.That(SessionCommands.IconKeys.Length, Is.EqualTo(seen.Count),
                "IconKeys names something the table cannot draw");

            foreach (string key in SessionCommands.IconKeys)
                Assert.That(Registry.Label(key), Is.Not.EqualTo(key), $"{key} has no name");
        }

        /// <summary>
        /// The ask-twice flags, stated per placement. Load is the one that proves the flag belongs
        /// to the placement rather than to the command: the same row throws a colony away in game
        /// and throws nothing away on the main screen.
        /// </summary>
        [Test]
        public void ARowAsksTwiceExactlyWhereSomethingIsLost()
        {
            Assert.That(SessionCommands.AsksTwice(SessionCommands.NewGameKey, SessionContext.MainScreen), Is.False);
            Assert.That(SessionCommands.AsksTwice(SessionCommands.LoadKey, SessionContext.MainScreen), Is.False,
                "nothing is running, so there is nothing to lose");
            Assert.That(SessionCommands.AsksTwice(SessionCommands.OptionsKey, SessionContext.MainScreen), Is.False);
            // Since the title screen (design 40): Exit game raises the leave prompt in its
            // no-colony form, and the prompt is the second press, as it is in game.
            Assert.That(SessionCommands.AsksTwice(SessionCommands.QuitKey, SessionContext.MainScreen), Is.False,
                "the prompt asks; the row arming as well would be asking twice before asking");

            // Neither save arms. Save writes over a file the player already named and asked for;
            // Save as writes a file that does not exist yet, or asks its own question in the
            // prompt. The row that could destroy something is the prompt's Overwrite, not this.
            Assert.That(SessionCommands.AsksTwice(SessionCommands.SaveAsKey, SessionContext.InGame), Is.False);
            Assert.That(SessionCommands.AsksTwice(SessionCommands.SaveKey, SessionContext.InGame), Is.False,
                "writing a file loses nothing");
            Assert.That(SessionCommands.AsksTwice(SessionCommands.LoadKey, SessionContext.InGame), Is.True,
                "the colony on screen is discarded");
            // The two quits stopped arming on 2026-09-21: each raises LeavePrompt, which asks
            // whether to save on the way out, and an armed row in front of a prompt is one
            // question too many.
            Assert.That(SessionCommands.AsksTwice(SessionCommands.QuitToMenuKey, SessionContext.InGame), Is.False);
            Assert.That(SessionCommands.AsksTwice(SessionCommands.QuitKey, SessionContext.InGame), Is.False);

            Assert.That(SessionCommands.AsksTwice(SessionCommands.SaveKey, SessionContext.MainScreen), Is.False,
                "a row nobody draws cannot be armed");
        }

        [Test]
        public void EveryContextHasRowsAndEveryRowKnowsWhereItIs()
        {
            foreach (SessionContext context in SessionCommands.Contexts)
            {
                IReadOnlyList<SessionCommand> rows = SessionCommands.For(context);
                Assert.That(rows, Is.Not.Empty, $"{context} draws an empty column");

                var orders = new HashSet<int>();
                foreach (SessionCommand row in rows)
                {
                    Assert.That(row.Context, Is.EqualTo(context));
                    Assert.That(orders.Add(row.Order), Is.True, $"two rows share order {row.Order}");
                    Assert.That(SessionCommands.AsksTwice(row.Key, context), Is.EqualTo(row.AsksTwice),
                        "the row and the lookup disagree about the same placement");
                }
            }
        }
    }
}
