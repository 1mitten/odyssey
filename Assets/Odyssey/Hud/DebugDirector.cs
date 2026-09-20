#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>The debug menu's two sections (owner, 2026-09-20: events want a tab of their own).</summary>
    public enum DebugTab
    {
        /// <summary>The overlay toggle and the grants: things done to the colony that exists.</summary>
        Cheats,

        /// <summary>One row per incident the content declares, each fired on click (design 23 §3).</summary>
        Events,
    }

    /// <summary>
    /// Whether the debug menu is open, and which of its two tabs is showing. Nothing else lives
    /// here: the developer-overlay toggle it hosts is still <see cref="SettingsDirector.DeveloperOverlay"/>,
    /// and the action rows submit intents directly from the presenter the way every other click in
    /// this HUD does — a director exists to hold state a presenter would otherwise have to invent
    /// for itself, and "is the panel open, and on which tab" is the only such state here.
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
        public const string PanelKey = "ui.debug.panel";
        public const string CheatsKey = "ui.debug.tab.cheats";
        public const string EventsKey = "ui.debug.tab.events";
        public const string SpawnPawnKey = "ui.debug.spawnpawn";
        public const string GiveWoodKey = "ui.debug.givewood";
        public const string GiveStoneKey = "ui.debug.givestone";
        public const string GiveFoodKey = "ui.debug.givefood";
        public const string SkipDayKey = "ui.debug.skipday";
        public const string SkipMorningKey = "ui.debug.skipmorning";
        public const string RipenCropsKey = "ui.debug.ripen";

        /// <summary>
        /// Every key the panel puts on screen that is its own, so <c>RegistryTests</c> can hold
        /// the panel to the naming CSV the way it holds Settings. The event rows are named by
        /// <see cref="IncidentLabels"/>, which has its own test.
        /// </summary>
        public static readonly string[] IconKeys =
        {
            PanelKey, CheatsKey, EventsKey, SpawnPawnKey, GiveWoodKey, GiveStoneKey, GiveFoodKey,
            SkipDayKey, SkipMorningKey, RipenCropsKey,
        };

        public static string TabKey(DebugTab tab) => tab == DebugTab.Events ? EventsKey : CheatsKey;

        public bool Open { get; private set; }

        /// <summary>Opens on Cheats: the overlay toggle is the row the key was bound to for a day.</summary>
        public DebugTab Tab { get; private set; } = DebugTab.Cheats;

        /// <summary>Raised after every open or close, so a presenter can answer without polling.</summary>
        public event Action? Changed;

        /// <summary>Raised when the tab changes, and only then.</summary>
        public event Action<DebugTab>? TabChanged;

        public void Toggle() => SetOpen(!Open);

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            Changed?.Invoke();
        }

        public void SetTab(DebugTab tab)
        {
            if (Tab == tab) return;
            Tab = tab;
            TabChanged?.Invoke(tab);
        }
    }
}
