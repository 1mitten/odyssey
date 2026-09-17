#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// Where a session command is being drawn.
    ///
    /// <para>Two, and only two, because there are only two surfaces that can offer one: the main
    /// screen, which exists precisely when no world is built, and the settings panel, which exists
    /// precisely when one is. Nothing is ever drawn in both at once, so a command's placement in
    /// each is independent of the other — which is why the table below is a list of placements
    /// rather than a list of commands with a context flag bolted on.</para>
    /// </summary>
    public enum SessionContext
    {
        /// <summary>The screen before the game. No session is built; there is nothing to lose.</summary>
        MainScreen,

        /// <summary>The settings panel, over a running colony (`17-start-flow.md` §2 decision 1:
        /// no second in-game menu is built, these rows join the panel that already exists).</summary>
        InGame,
    }

    /// <summary>
    /// One session-level command as one surface draws it: what it is called, where it sits, and
    /// whether pressing it once is enough.
    ///
    /// <para>Nothing here is named in C#. <see cref="Label"/> comes out of the naming registry, the
    /// same bargain <see cref="HudCommand"/> and <see cref="PaletteTool"/> make — a name the owner
    /// corrects in <c>docs/design/icon-keys.csv</c> reaches both surfaces without anyone retyping
    /// it in either.</para>
    /// </summary>
    public readonly struct SessionCommand
    {
        /// <summary>The registry key, which is also the row's icon and its label.</summary>
        public readonly string Key;

        /// <summary>The word the player reads, from <see cref="Registry.Label"/>.</summary>
        public readonly string Label;

        /// <summary>The surface this placement belongs to.</summary>
        public readonly SessionContext Context;

        /// <summary>Where it sits in that surface's column, counting from 1 at the top.</summary>
        public readonly int Order;

        /// <summary>
        /// Whether this row arms on the first press and acts on the second, the way
        /// <see cref="SettingsDirector.RequestExit"/> already does for the exit row.
        ///
        /// <para>It is a property of the <i>placement</i>, not of the command, and that is the
        /// point: the same Load row throws away a running colony in game and throws away nothing
        /// at all on the main screen. A flag on the command could only be wrong in one of the two
        /// places.</para>
        /// </summary>
        public readonly bool AsksTwice;

        public SessionCommand(string key, string label, SessionContext context, int order,
            bool asksTwice)
        {
            Key = key;
            Label = label;
            Context = context;
            Order = order;
            AsksTwice = asksTwice;
        }
    }

    /// <summary>
    /// The session-level commands — new game, load, save, options, quit — as one table that both
    /// the main screen and the settings panel read.
    ///
    /// <para><b>Why a table and not two hand-written lists.</b> `17-start-flow.md` §3 names this as
    /// the unit's answer to "where is consistency centralised": the same five words appear on two
    /// surfaces built by two different files, and two lists of the same rows drift. The failure is
    /// not a compile error — it is a Quit that asks twice in one place and once in the other, or a
    /// Save row that quietly outlives the screen it was written for. Here the row set is data in
    /// the assembly the fast tier can see, and <c>SessionCommandsTests</c> states each context's
    /// column exactly, so a drift is a red test rather than a report from the owner. It is the same
    /// bargain <see cref="HudCommands"/> makes for the command bar and <see cref="PaletteTools"/>
    /// for the build palette.</para>
    ///
    /// <para><b>Two keys are deliberately reused rather than minted.</b> Options on the main screen
    /// is <see cref="SettingsDirector.PanelKey"/>, and Quit in both contexts is
    /// <see cref="SettingsDirector.ExitKey"/>. A <c>ui.session.options</c> beside a
    /// <c>ui.settings.panel</c> would be two keys for one thing: two rows in the naming CSV, two
    /// entries in the wiki, two icons to draw, and two chances for the owner to rename one and not
    /// the other. Reuse also means the art already mapped to those keys keeps working — the icon
    /// map is keyed the same way, and a new key would draw an outlined square beside a drawn one.
    /// The constants below are written as references to the settings director's own, so the reuse
    /// is visible in code rather than being two identical string literals that merely happen to
    /// agree today.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public static class SessionCommands
    {
        /// <summary>Start a colony. Main screen only — in game there is already one.</summary>
        public const string NewGameKey = "ui.session.newgame";

        /// <summary>Open a colony from a file. Both surfaces.</summary>
        public const string LoadKey = "ui.session.load";

        /// <summary>
        /// Write the running colony over the save it came from. In game only: nothing to write
        /// otherwise.
        ///
        /// <para><b>Over, not beside</b> (owner, 2026-09-17: *"I notice you keep saving a new game
        /// everytime … otherwise lots of saves will be created"*). A session is bound to a file —
        /// the one it was loaded from, or the one it last saved to — and this writes to that. The
        /// first save in a colony has nothing to bind to, so it asks for a name and then binds.
        /// </para>
        /// </summary>
        public const string SaveKey = "ui.session.save";

        /// <summary>
        /// Write the running colony to a save of its own, leaving the one it came from alone.
        ///
        /// <para><b>The reason there are two rows rather than one prompt.</b> Naming the save on
        /// every press is the version that does not multiply files and does annoy: a player who
        /// saves often would confirm the same name every time. Naming it once and then overwriting
        /// it is the ordinary case, and branching is the exception — so the ordinary case is one
        /// press and the exception says what it is. After this, the session is bound to the *new*
        /// file, which is what "save as" means everywhere else.</para>
        /// </summary>
        public const string SaveAsKey = "ui.session.saveas";

        /// <summary>
        /// Options, on the main screen. Deliberately the settings panel's own key: in game the row
        /// would open the panel it is already drawn in, so there is no in-game placement, and the
        /// word on the button is the word on the panel because it is the same word.
        /// </summary>
        public const string OptionsKey = SettingsDirector.PanelKey;

        /// <summary>Tear the world down and return to the main screen. In game only, by decision 5
        /// of `17-start-flow.md` §2 — there is no world to leave from the main screen.</summary>
        public const string QuitToMenuKey = "ui.session.quittomenu";

        /// <summary>
        /// Leave the application. Deliberately the settings panel's existing exit key rather than a
        /// near-duplicate <c>ui.session.quit</c>.
        /// </summary>
        public const string QuitKey = SettingsDirector.ExitKey;

        /// <summary>
        /// Every placement, in one table. One row is one command on one surface.
        ///
        /// <para><b>The main screen's order</b> is `17-start-flow.md` §4's picture, and the reason
        /// it reads that way is the order a player wants them in: New game is the only thing a
        /// first-time player can do, Load is the only thing a returning one does, Options is
        /// configuration rather than a choice about this session, and Quit is last because a
        /// destructive row must never sit under the finger that was reaching for the row above
        /// it.</para>
        ///
        /// <para><b>The in-game order runs from what costs nothing to what costs everything:</b>
        /// Save and Save as lose nothing, Load discards this colony but stays in the game, Quit to
        /// main menu leaves the world, Quit leaves the application. The two saves are therefore
        /// first — Save is also what a player opening this panel before doing anything drastic is
        /// reaching for, and Save as sits directly under it because it is the same act with one
        /// question asked — and
        /// <see cref="QuitKey"/> stays last, which is where the settings panel already draws it, so
        /// adding three rows moves nothing the owner has already looked at.</para>
        ///
        /// <para><b>Which rows ask twice.</b> In game, all three of Load, Quit to main menu and
        /// Quit throw a running colony away, so all three arm first — the argument
        /// <see cref="SettingsDirector.RequestExit"/> already makes for one of them, applied to the
        /// two that arrived beside it. On the main screen nothing is running, so Load and Options
        /// cost nothing and go on the first press. Quit still asks twice there, for the reason the
        /// exit row does: it sits directly under Options in a stack of four, it is irreversible,
        /// and a mis-aimed click closes the game. That also keeps one word behaving one way
        /// everywhere, which is the whole thesis of putting both surfaces through this table.</para>
        /// </summary>
        static readonly (string Key, SessionContext Context, int Order, bool AsksTwice)[] Table =
        {
            (NewGameKey,    SessionContext.MainScreen, 1, false),
            (LoadKey,       SessionContext.MainScreen, 2, false),
            (OptionsKey,    SessionContext.MainScreen, 3, false),
            (QuitKey,       SessionContext.MainScreen, 4, true),

            (SaveKey,       SessionContext.InGame,     1, false),
            (SaveAsKey,     SessionContext.InGame,     2, false),
            (LoadKey,       SessionContext.InGame,     3, true),
            (QuitToMenuKey, SessionContext.InGame,     4, true),
            (QuitKey,       SessionContext.InGame,     5, true),
        };

        /// <summary>The contexts, so a test can walk every one rather than naming two by hand and
        /// missing a third the day there is one.</summary>
        public static readonly SessionContext[] Contexts =
        {
            SessionContext.MainScreen,
            SessionContext.InGame,
        };

        /// <summary>
        /// Every key this table can put on screen, so <c>RegistryTests</c> can hold both surfaces
        /// to the naming CSV the way it already holds the settings panel and the build palette. A
        /// label invented in C# is a label the owner cannot correct.
        /// </summary>
        public static readonly string[] IconKeys = BuildKeys();

        static string[] BuildKeys()
        {
            var keys = new List<string>();
            foreach ((string key, SessionContext _, int __, bool ___) in Table)
                if (!keys.Contains(key))
                    keys.Add(key);
            return keys.ToArray();
        }

        /// <summary>
        /// One surface's column, top to bottom, names resolved from the registry.
        ///
        /// <para>Sorted by <see cref="SessionCommand.Order"/> rather than by position in the table,
        /// so the table can be grouped however reads best without the screen depending on it.</para>
        /// </summary>
        public static IReadOnlyList<SessionCommand> For(SessionContext context)
        {
            var rows = new List<SessionCommand>();
            foreach ((string key, SessionContext at, int order, bool asksTwice) in Table)
                if (at == context)
                    rows.Add(new SessionCommand(key, Registry.Label(key), at, order, asksTwice));
            rows.Sort((a, b) => a.Order.CompareTo(b.Order));
            return rows;
        }

        /// <summary>Whether a key is drawn at all on one surface. False for anything the table does
        /// not place there, which is how a presenter refuses a row it was never drawing.</summary>
        public static bool IsDrawn(string key, SessionContext context)
        {
            foreach ((string row, SessionContext where, int _, bool __) in Table)
                if (where == context && row == key)
                    return true;
            return false;
        }

        /// <summary>
        /// Whether this row arms on the first press in this context. False for a key the table does
        /// not place here: a row nobody draws cannot be armed, and answering "yes" for one would
        /// leave a presenter waiting for a second press that can never come.
        /// </summary>
        public static bool AsksTwice(string key, SessionContext context)
        {
            foreach ((string row, SessionContext where, int _, bool asksTwice) in Table)
                if (where == context && row == key)
                    return asksTwice;
            return false;
        }

        static SessionCommands()
        {
            // A duplicate placement would draw one row twice; two rows sharing an order would make
            // the column's sequence depend on the sort's stability, which is exactly the kind of
            // thing that reads as correct until somebody reorders the table.
            var seen = new HashSet<string>();
            var orders = new HashSet<string>();
            foreach ((string key, SessionContext context, int order, bool _) in Table)
            {
                if (!seen.Add($"{context}/{key}"))
                    throw new InvalidOperationException($"{key} is placed twice in {context}");
                if (!orders.Add($"{context}/{order}"))
                    throw new InvalidOperationException($"two rows share order {order} in {context}");
            }

            foreach (SessionContext context in Contexts)
            {
                bool any = false;
                foreach ((string _, SessionContext where, int __, bool ___) in Table)
                    if (where == context) { any = true; break; }
                if (!any)
                    throw new InvalidOperationException($"{context} has no rows and would draw an empty column");
            }
        }
    }
}
