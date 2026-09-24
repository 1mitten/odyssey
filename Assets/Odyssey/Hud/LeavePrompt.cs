#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>Where leaving goes: back to the start screen, or out of the game altogether.</summary>
    public enum LeaveTo
    {
        MainMenu,
        Desktop,
    }

    /// <summary>
    /// The question asked before a running colony is put down: <b>save and leave, leave without
    /// saving, or stay</b>.
    ///
    /// <para><b>Why it replaces the arm-twice rows.</b> Quit to main menu and Quit both armed on
    /// the first press and acted on the second (`17-start-flow.md` §3), which asks *are you sure*
    /// and nothing else. The owner asked for the other half (2026-09-21): *"when you quit the game
    /// (to main menu or to desktop) it should confirm to save before you exit to be sure."* A
    /// second press on a red row cannot offer to save, so the confirmation has to be a prompt with
    /// three answers rather than a row with two states — and once it is, the arming is redundant:
    /// the prompt itself is the second press.</para>
    ///
    /// <para><b>It never writes and never leaves.</b> Like every director here it raises what was
    /// chosen and the presenter does it, so "the player chose to save and go to the desktop" is a
    /// sentence the fast tier can assert without a filesystem or an application to quit.</para>
    ///
    /// <para><b>What the save will be called is told to it, not worked out by it.</b> This
    /// assembly cannot see the saves folder (ADR 0003) — the presenter knows the file the session
    /// is bound to, or what an unnamed colony would be called, and hands it in with the question.
    /// It is the same bargain <see cref="SavePrompt"/> makes about a collision.</para>
    ///
    /// <para>Unity-free by construction: everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class LeavePrompt
    {
        /// <summary>The registry key naming the prompt when it is leaving for the start screen.
        /// Deliberately the session table's own row rather than a second wording of it.</summary>
        public const string ToMenuTitleKey = SessionCommands.QuitToMenuKey;

        /// <summary>And when it is leaving the game altogether.</summary>
        public const string ToDesktopTitleKey = SessionCommands.QuitKey;

        /// <summary>The registry key naming the answer that writes first.</summary>
        public const string SaveAndLeaveKey = "ui.prompt.saveandleave";

        /// <summary>The registry key naming the answer that does not.</summary>
        public const string LeaveKey = "ui.prompt.leaveunsaved";

        /// <summary>The way out. The naming prompt's own key: one word, one row in the CSV.</summary>
        public const string CancelKey = SavePrompt.CancelKey;

        /// <summary>Every key this prompt can put on screen, so <c>RegistryTests</c> can hold it to
        /// the naming CSV.</summary>
        public static readonly string[] IconKeys =
        {
            ToMenuTitleKey,
            ToDesktopTitleKey,
            SaveAndLeaveKey,
            LeaveKey,
            CancelKey,
        };

        /// <summary>Whether the prompt is up. It is modal while it is.</summary>
        public bool Showing { get; private set; }

        /// <summary>Where the answer will take the player, if they say yes to either of the two.</summary>
        public LeaveTo Destination { get; private set; }

        /// <summary>What saving would write to, as a player reads it. Empty when there is no
        /// session, which is a state <see cref="Ask"/> refuses.</summary>
        public string Target { get; private set; } = string.Empty;

        /// <summary>The title key for what is being left.</summary>
        public string TitleKey =>
            Destination == LeaveTo.Desktop ? ToDesktopTitleKey : ToMenuTitleKey;

        /// <summary>Raised when the player has chosen to go. The flag says whether to write the
        /// colony first.</summary>
        public event Action<LeaveTo, bool>? Confirmed;

        /// <summary>Raised whenever the prompt goes up or comes down, so a view can redraw.</summary>
        public event Action? Changed;

        /// <summary>
        /// Ask the question.
        ///
        /// <para>Refused with no session to lose — <paramref name="target"/> empty — because the
        /// whole of this prompt is "you have a colony on screen". Leaving from the main screen is
        /// a different act and keeps the arm-twice row it already had.</para>
        /// </summary>
        public bool Ask(LeaveTo to, string? target)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;

            Destination = to;
            Target = target!;
            HasColony = true;
            Showing = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Whether there is a colony to lose. False only when the question came from
        /// <see cref="AskToExit"/>: then there is nothing to save, and the prompt offers leaving
        /// and staying and nothing else.
        /// </summary>
        public bool HasColony { get; private set; }

        /// <summary>
        /// Ask whether to close the game from the title screen, where nothing is running (design
        /// 40). The same prompt with its save answer taken away: a click on Exit game is still one
        /// click from closing the game, so it is still asked.
        /// </summary>
        public void AskToExit()
        {
            Destination = LeaveTo.Desktop;
            Target = string.Empty;
            HasColony = false;
            Showing = true;
            Changed?.Invoke();
        }

        /// <summary>Stay. The third answer, and the one Escape means.</summary>
        public void Cancel()
        {
            if (!Showing) return;
            Showing = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// Go, with or without writing first. The prompt comes down before the answer is raised,
        /// so a presenter that tears the world down cannot leave a prompt hanging over the screen
        /// that replaces it.
        /// </summary>
        public bool Choose(bool save)
        {
            if (!Showing) return false;
            // With nothing running there is nothing to write, whatever was pressed.
            if (!HasColony) save = false;

            LeaveTo to = Destination;
            Showing = false;
            Changed?.Invoke();
            Confirmed?.Invoke(to, save);
            return true;
        }
    }
}
