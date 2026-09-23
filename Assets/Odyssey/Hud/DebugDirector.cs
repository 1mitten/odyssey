#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>The debug menu's two sections (owner, 2026-09-20: events want a tab of their own).</summary>
    public enum DebugTab
    {
        /// <summary>The overlay toggle and the grants: things done to the colony that exists.</summary>
        Cheats,

        /// <summary>One row per incident the content declares, each fired on click (design 23 §3).</summary>
        Events,

        /// <summary>Colonists, animals, the marauder and the weapons, placed near the camera (owner, 2026-09-22: a tab of its own).</summary>
        Spawn,
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
        public const string SpawnTabKey = "ui.debug.tab.spawn";
        public const string SpawnPawnKey = "ui.debug.spawnpawn";

        /// <summary>The two animals (design 29 §7): the same intent as the colonist's, with a kind.</summary>
        public const string SpawnHogKey = "ui.debug.spawnhog";

        public const string SpawnRatKey = "ui.debug.spawnrat";

        public const string GiveWoodKey = "ui.debug.givewood";
        public const string GiveStoneKey = "ui.debug.givestone";
        public const string GiveFoodKey = "ui.debug.givefood";
        public const string SkipDayKey = "ui.debug.skipday";
        public const string SkipMorningKey = "ui.debug.skipmorning";
        public const string RipenCropsKey = "ui.debug.ripen";

        /// <summary>
        /// Write a marker into the performance trace.
        ///
        /// <para><b>Why this is a menu row and not a key of its own.</b> A marker wants to be
        /// reachable while something is going wrong, which argues for a binding — but a
        /// binding is a <c>HotkeyAction</c>, and those are player controls that appear in the
        /// Keys tab and in the wiki. A developer's trace marker is not game content, and the
        /// debug menu is exactly where developer tools live. The timing is forgiving enough
        /// to afford it: the reader shows the seconds either side of a mark and leans the
        /// window backwards, because nobody reaches anything mid-hitch anyway.</para>
        /// </summary>
        public const string MarkTraceKey = "ui.debug.marktrace";

        /// <summary>
        /// Stop or start the performance trace for this session.
        ///
        /// <para><b>It exists so the tracer can be ruled out of a report about stutter</b>, which
        /// is not hypothetical: the first session ever traced came back with hitches, and the
        /// first question anybody sensibly asks is whether the new thing writing a file every
        /// second is causing them. The trace's own data answered it that time — 59 of 251 rows
        /// carried a slow frame, where a once-a-second flush would have marked nearly all of them
        /// — but "the suspect investigated itself" is a poor argument to have to make twice. One
        /// switch and two sessions settle it.</para>
        /// </summary>
        public const string TraceKey = "ui.debug.trace";

        /// <summary>
        /// Every key the panel puts on screen that is its own, so <c>RegistryTests</c> can hold
        /// the panel to the naming CSV the way it holds Settings. The event rows are named by
        /// <see cref="IncidentLabels"/>, which has its own test.
        /// </summary>
        public static readonly string[] IconKeys =
        {
            PanelKey, CheatsKey, EventsKey, SpawnTabKey, SpawnPawnKey, SpawnHogKey, SpawnRatKey,
            SpawnMarauderKey, SpawnBatKey, SpawnCrowbarKey, SpawnMacheteKey, SpawnArcBladeKey,
            GiveWoodKey, GiveStoneKey, GiveFoodKey,
            SkipDayKey, SkipMorningKey, RipenCropsKey, MarkTraceKey, TraceKey,
        };

        /// <summary>The marauder (design 33 §1): a hostile person, the same intent as the colonist's with a kind.</summary>
        public const string SpawnMarauderKey = "ui.debug.spawnmarauder";

        /// <summary>The four weapons (design 33 §1, C3): one item each, granted as wood is.</summary>
        public const string SpawnBatKey = "ui.debug.spawnbat", SpawnCrowbarKey = "ui.debug.spawncrowbar",
            SpawnMacheteKey = "ui.debug.spawnmachete", SpawnArcBladeKey = "ui.debug.spawnarcblade";

        /// <summary>
        /// One row of the Spawn tab: its name, what its tooltip says, and the intent a click sends
        /// at the column the shell aims it at. <b>A table here rather than eight calls in the
        /// shell</b>, so what each row sends is held by the fast tier — the shell only lays the
        /// rows out and supplies the anchor (<c>HudShell.DebugAnchorCell</c>), which needs Unity.
        /// </summary>
        public readonly struct SpawnRow
        {
            public readonly string Key;
            public readonly string Tooltip;

            /// <summary><see cref="IntentKind.SpawnPawn"/> with a kind, or <see cref="IntentKind.GiveResource"/> with an item and a count.</summary>
            public readonly IntentKind Kind;

            public readonly int A;
            public readonly int B;

            public SpawnRow(string key, string tooltip, IntentKind kind, int a, int b = 0)
            {
                Key = key;
                Tooltip = tooltip;
                Kind = kind;
                A = a;
                B = b;
            }

            /// <summary>The intent a click on this row sends, aimed at <paramref name="anchor"/>'s column.</summary>
            public Intent ToIntent(CellRef anchor) => new Intent(Kind, anchor, A, B);
        }

        static SpawnRow Pawn(string key, string tooltip, int kind) =>
            new SpawnRow(key, tooltip, IntentKind.SpawnPawn, kind);

        static SpawnRow Weapon(string key, string tooltip, int item) =>
            new SpawnRow(key, tooltip, IntentKind.GiveResource, item, 1);

        /// <summary>
        /// The Spawn tab, top to bottom: who can be put on the board (owner, 2026-09-22: the
        /// colonist first, then the animals), then the marauder, then one of each weapon (design
        /// 33 §1: "any from the debug Spawn tab"). A weapon is one to a stack, so it is granted one
        /// at a time — fifty machetes would be fifty piles.
        /// </summary>
        public static readonly SpawnRow[] SpawnRows =
        {
            Pawn(SpawnPawnKey, "Adds a colonist near the camera, with no scenario and no starting kit",
                PawnKindLabels.ColonistKind),
            Pawn(SpawnHogKey, "Adds a wild midden hog near the camera. It wanders and rests, and never takes a ladder",
                PawnKindLabels.MiddenHogKind),
            Pawn(SpawnRatKey, "Adds a duct rat near the camera. It wanders and rests, and climbs anything",
                PawnKindLabels.DuctRatKind),
            Pawn(SpawnMarauderKey, "Adds a hostile marauder near the camera, armed. It hunts whoever is still standing",
                PawnKindLabels.Marauder),
            Weapon(SpawnBatKey, "Adds a bat near the camera. Blunt, and now and then it stuns", ItemHandle.Bat),
            Weapon(SpawnCrowbarKey, "Adds a crowbar near the camera. Heavier and slower than a bat, and stuns more often",
                ItemHandle.Crowbar),
            Weapon(SpawnMacheteKey, "Adds a machete near the camera. Sharp and quick", ItemHandle.Machete),
            Weapon(SpawnArcBladeKey, "Adds an arc blade near the camera. The best thing a colonist can hold",
                ItemHandle.ArcBlade),
        };

        public static string TabKey(DebugTab tab) =>
            tab == DebugTab.Events ? EventsKey
            : tab == DebugTab.Spawn ? SpawnTabKey
            : CheatsKey;

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
