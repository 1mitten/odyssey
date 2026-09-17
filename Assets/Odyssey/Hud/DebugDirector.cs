#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Whether the debug menu is open. Nothing else lives here: the developer-overlay toggle it
    /// hosts is still <see cref="SettingsDirector.DeveloperOverlay"/>, and the two action rows
    /// (spawn a colonist, give a resource) submit intents directly from the presenter the way every
    /// other click in this HUD does — a director exists to hold state a presenter would otherwise
    /// have to invent for itself, and "is the panel open" is the only such state here.
    ///
    /// <para>Backtick opened the overlay directly until 2026-09-17; it opens this panel instead now,
    /// with the overlay as the panel's first row. The rename is <see cref="HotkeyAction.DebugMenu"/>,
    /// not a second binding, so there is exactly one thing the key has ever meant.</para>
    ///
    /// <para>Session state, not a preference: a debug menu belongs to the colony being poked at,
    /// not to the machine, so unlike <see cref="SettingsDirector"/> this is built fresh with every
    /// session's <see cref="HudDirectors"/> rather than hoisted above it.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class DebugDirector
    {
        public bool Open { get; private set; }

        /// <summary>Raised after every change, so a presenter can answer without polling.</summary>
        public event Action? Changed;

        public void Toggle() => SetOpen(!Open);

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            Changed?.Invoke();
        }
    }
}
