#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Whether the Work tab is open, and which of its two readings it is showing.
    ///
    /// <para>Built on <see cref="DebugDirector"/>'s model and for the same stated reason: a
    /// director exists to hold state a presenter would otherwise have to invent for itself, and
    /// "is the panel open, and in which mode" is the only such state here. Every click in the grid
    /// submits an intent directly from the presenter, the way every other click in this HUD does.
    /// </para>
    ///
    /// <para><b>The mode is presentation state and pointedly not colony state</b> (design 27
    /// §6.3). It is not saved into a world and not hashed: which way a player prefers to read a
    /// grid is a fact about the player, not about the colony. It belongs beside the camera and the
    /// slice in the saved view if it is ever persisted at all, and it is deliberately not there
    /// yet — nobody has said they want it remembered.</para>
    ///
    /// <para>Session state rather than a preference, like <see cref="DebugDirector"/>: it is built
    /// fresh with every session's <see cref="HudDirectors"/> rather than hoisted above it.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class WorkDirector
    {
        /// <summary>
        /// The panel's own name, which is the tab's: there is one Work and the bar, the window
        /// header and the wiki all read it from the same key.
        /// </summary>
        public const string PanelKey = "ui.tab.work";

        /// <summary>Every key the panel puts on screen that is its own, for <c>RegistryTests</c>.
        /// The column headers are named by <see cref="WorkCatalogue"/>, which has its own.</summary>
        public static readonly string[] IconKeys = { PanelKey };

        public bool Open { get; private set; }

        /// <summary>
        /// Opens on <see cref="WorkGridMode.Detailed"/>, because the panel's whole claim is that
        /// four priorities are worth having. A player who wants the simpler reading says so once.
        /// </summary>
        public WorkGridMode Mode { get; private set; } = WorkGridMode.Detailed;

        /// <summary>Raised after every open or close, so a presenter can answer without polling.</summary>
        public event Action? Changed;

        /// <summary>Raised when the mode changes, and only then.</summary>
        public event Action<WorkGridMode>? ModeChanged;

        public void Toggle() => SetOpen(!Open);

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            Changed?.Invoke();
        }

        public void SetMode(WorkGridMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
