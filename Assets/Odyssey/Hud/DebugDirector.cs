#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>The debug menu's sections (owner, 2026-09-20: events want a tab of their own).</summary>
    public enum DebugTab
    {
        /// <summary>The overlay toggle and the grants: things done to the colony that exists.</summary>
        Cheats,

        /// <summary>One row per incident the content declares, each fired on click (design 23 §3).</summary>
        Events,

        /// <summary>Colonists, animals, the bandit and the weapons, placed near the camera (owner, 2026-09-22: a tab of its own).</summary>
        Spawn,

        /// <summary>
        /// The sky set by hand (owner, 2026-09-24: "we need to be able to test it"): each row commands
        /// the weather system (design 43 §8), and two switches change only how the rain is drawn.
        /// </summary>
        Weather,
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
        public const string WeatherTabKey = "ui.debug.tab.weather";
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

        /// <summary>Completes the project in hand, which is how a Research tab project becomes done until the mechanism exists (design 34).</summary>
        public const string FinishResearchKey = "ui.debug.finishresearch";

        /// <summary>
        /// Spend a whole game month at once, so the year can be walked through.
        ///
        /// <para><b>It exists because the season was otherwise unreachable.</b> Temperature
        /// (design 28) puts the year's shape into the game — Wash benign, Glare warm, Rime
        /// lethal — and Rime is months four and five of six. With a day a press that is sixty
        /// presses to reach the season the whole model was built for, which is not a playtest
        /// anybody runs; the one question the work most needs answered would have been the one
        /// question nobody could ask. Six presses now walk the year from spring to the cold and
        /// back, which is also the shape of the test: the seasons are only worth having if the
        /// turn between them is worth watching.</para>
        ///
        /// <para>The same mechanism as <see cref="SkipDayKey"/> and deliberately not a new one —
        /// a month is the calendar's own <c>DaysPerMonth</c> days of ticks, read rather than
        /// written, so a retuned calendar does not leave this row skipping some other amount.
        /// </para>
        /// </summary>
        public const string SkipMonthKey = "ui.debug.skipmonth";

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
            SpawnBanditKey, SpawnBatKey, SpawnCrowbarKey, SpawnMacheteKey, SpawnArcBladeKey,
            SpawnBanditsKey, ArmColonistsKey,
            GroupColonistsKey, GroupHostilesKey, GroupAnimalsKey, GroupWeaponsKey, GroupItemsKey,
            GiveWoodKey, GiveStoneKey, GiveFoodKey,
            SkipDayKey, SkipMonthKey, SkipMorningKey, RipenCropsKey, FinishResearchKey, MarkTraceKey, TraceKey,
            JumpsFailKey,
            WeatherTabKey, WeatherClearKey, WeatherOvercastKey, WeatherDrizzleKey, WeatherRainKey,
            WeatherDownpourKey, WeatherStormKey, RainParticlesKey, WetGlossKey,
        };

        public const string WeatherClearKey = "ui.debug.weather.clear",
            WeatherOvercastKey = "ui.debug.weather.overcast",
            WeatherDrizzleKey = "ui.debug.weather.drizzle",
            WeatherRainKey = "ui.debug.weather.rain",
            WeatherDownpourKey = "ui.debug.weather.downpour",
            WeatherStormKey = "ui.debug.weather.storm";

        /// <summary>
        /// Draw wet ground as gloss only rather than richer and a little darker — the two
        /// candidates the owner is choosing between by eye (2026-09-25).
        /// </summary>
        public const string WetGlossKey = "ui.debug.wetgloss";

        /// <summary>Draw the rain as the weather design's §7 first wrote it (CPU particles), to compare.</summary>
        public const string RainParticlesKey = "ui.debug.rainparticles";

        /// <summary>
        /// One sky the Weather tab sets (design 43 §8). Since the weather system exists these are
        /// <b>commands to it</b>, not looks: a row sends <see cref="IntentKind.DebugSetWeather"/> with
        /// a kind and an intensity, the sky blends in over a few seconds, and the spell then runs its
        /// rolled length before the season takes over. What each kind looks like is the content's
        /// (<c>Weather.xml</c>), so the tab can never show a sky the game cannot roll.
        /// </summary>
        public readonly struct WeatherPreset
        {
            public readonly string Key;
            public readonly string Tooltip;
            public readonly WeatherKind Kind;

            /// <summary>The intensity the sky is set to, in per-mille: for rain, drizzle to downpour.</summary>
            public readonly int IntensityPerMille;

            public WeatherPreset(string key, string tooltip, WeatherKind kind, int intensityPerMille)
            {
                Key = key;
                Tooltip = tooltip;
                Kind = kind;
                IntensityPerMille = intensityPerMille;
            }

            /// <summary>The command a click on this row sends: blend in quickly (C = 1).</summary>
            public Intent ToIntent() => new Intent(IntentKind.DebugSetWeather, default, (int)Kind, IntensityPerMille, 1);
        }

        /// <summary>The Weather tab, top to bottom. The first is the game as it draws without weather.</summary>
        public static readonly WeatherPreset[] WeatherPresets =
        {
            new WeatherPreset(WeatherClearKey, "Clears the sky now; the season takes over again when the spell ends",
                WeatherKind.Clear, 1000),
            new WeatherPreset(WeatherOvercastKey,
                "A grey day with no rain: the sun and its shadows faded, the colour drained",
                WeatherKind.Cloudy, 1000),
            new WeatherPreset(WeatherDrizzleKey, "Light rain in full colour; the ground turns half wet",
                WeatherKind.Rain, 250),
            new WeatherPreset(WeatherRainKey, "Steady rain in full colour: softer light, wet ground, puddles starting",
                WeatherKind.Rain, 700),
            new WeatherPreset(WeatherDownpourKey, "The heaviest ordinary rain, still in colour: soaked ground and puddles",
                WeatherKind.Rain, 1000),
            new WeatherPreset(WeatherStormKey,
                "The rarer dim day: heavy rain, the colour drained to grey, the wind bending grass and rain",
                WeatherKind.Storm, 1000),
        };

        /// <summary>
        /// Every jump over a stream falls short while this is on (design 43 §6), so a failed jump
        /// can be watched: at one in thirty-three it is not something a playtest can wait for.
        /// Sends <see cref="IntentKind.DebugJumpsFail"/> with <c>A</c> 1 or 0.
        /// </summary>
        public const string JumpsFailKey = "ui.debug.jumpsfail";

        /// <summary>The bandit (design 33 §1): a hostile person, the same intent as the colonist's with a kind.</summary>
        public const string SpawnBanditKey = "ui.debug.spawnbandit";

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

            /// <summary><see cref="IntentKind.SpawnPawn"/> with a kind, <see cref="IntentKind.GiveResource"/> with an item and a count, or <see cref="IntentKind.DebugArmColonists"/>.</summary>
            public readonly IntentKind Kind;

            public readonly int A;
            public readonly int B;

            /// <summary>The heading this row sits under (design 33 §9i): one of the <c>Group…Key</c>s.</summary>
            public readonly string Group;

            /// <summary>How many times a click sends the intent: 3 for the bandit band, else 1. The
            /// simulation spreads each onto its own tile (§9h).</summary>
            public readonly int Repeat;

            public SpawnRow(string key, string tooltip, IntentKind kind, int a, int b = 0,
                string group = GroupColonistsKey, int repeat = 1)
            {
                Key = key;
                Tooltip = tooltip;
                Kind = kind;
                A = a;
                B = b;
                Group = group;
                Repeat = repeat < 1 ? 1 : repeat;
            }

            /// <summary>The intent a click on this row sends, aimed at <paramref name="anchor"/>'s column.</summary>
            public Intent ToIntent(CellRef anchor) => new Intent(Kind, anchor, A, B);
        }

        /// <summary>The Spawn tab's headings, in the order they are drawn (design 33 §9i; owner,
        /// 2026-09-24: "a category for each type of spawn").</summary>
        public const string GroupColonistsKey = "ui.debug.group.colonists", GroupHostilesKey = "ui.debug.group.hostiles",
            GroupAnimalsKey = "ui.debug.group.animals", GroupWeaponsKey = "ui.debug.group.weapons",
            GroupItemsKey = "ui.debug.group.items";

        public static readonly string[] SpawnGroups =
            { GroupColonistsKey, GroupHostilesKey, GroupAnimalsKey, GroupWeaponsKey, GroupItemsKey };

        /// <summary>Three bandits at once, spread over neighbouring tiles; and every unarmed colonist given a weapon.</summary>
        public const string SpawnBanditsKey = "ui.debug.spawnbandits", ArmColonistsKey = "ui.debug.armcolonists";

        /// <summary>How many a resource row grants: the Cheats tab's old fifty, moved here with the rows.</summary>
        public const int GiveAmount = 50;

        static SpawnRow Pawn(string key, string tooltip, int kind, string group, int repeat = 1) =>
            new SpawnRow(key, tooltip, IntentKind.SpawnPawn, kind, 0, group, repeat);

        static SpawnRow Weapon(string key, string tooltip, int item) =>
            new SpawnRow(key, tooltip, IntentKind.GiveResource, item, 1, GroupWeaponsKey);

        static SpawnRow Resource(string key, string tooltip, int item) =>
            new SpawnRow(key, tooltip, IntentKind.GiveResource, item, GiveAmount, GroupItemsKey);

        /// <summary>
        /// The Spawn tab, top to bottom: who can be put on the board (owner, 2026-09-22: the
        /// colonist first, then the animals), then the bandit, then one of each weapon (design
        /// 33 §1: "any from the debug Spawn tab"). A weapon is one to a stack, so it is granted one
        /// at a time — fifty machetes would be fifty piles.
        /// </summary>
        public static readonly SpawnRow[] SpawnRows =
        {
            Pawn(SpawnPawnKey, "Adds a colonist near the camera, with no scenario and no starting kit",
                PawnKindLabels.ColonistKind, GroupColonistsKey),
            new SpawnRow(ArmColonistsKey,
                "Every colonist standing with nothing in hand takes a random melee weapon, at once. Armed colonists keep theirs",
                IntentKind.DebugArmColonists, 0, 0, GroupColonistsKey),
            Pawn(SpawnBanditKey, "Adds a hostile bandit near the camera, armed. It hunts whoever is still standing",
                PawnKindLabels.Bandit, GroupHostilesKey),
            Pawn(SpawnBanditsKey, "Adds three bandits near the camera, each on its own tile",
                PawnKindLabels.Bandit, GroupHostilesKey, repeat: 3),
            Pawn(SpawnHogKey, "Adds a wild midden hog near the camera. It wanders and rests, and never takes a ladder",
                PawnKindLabels.MiddenHogKind, GroupAnimalsKey),
            Pawn(SpawnRatKey, "Adds a duct rat near the camera. It wanders and rests, and climbs anything",
                PawnKindLabels.DuctRatKind, GroupAnimalsKey),
            Weapon(SpawnBatKey, "Adds a bat near the camera. Blunt, and now and then it stuns", ItemHandle.Bat),
            Weapon(SpawnCrowbarKey, "Adds a crowbar near the camera. Heavier and slower than a bat, and stuns more often",
                ItemHandle.Crowbar),
            Weapon(SpawnMacheteKey, "Adds a machete near the camera. Sharp and quick", ItemHandle.Machete),
            Weapon(SpawnArcBladeKey, "Adds an arc blade near the camera. The best thing a colonist can hold",
                ItemHandle.ArcBlade),
            Resource(GiveWoodKey, "Adds 50 wood near the camera", ItemHandle.Wood),
            Resource(GiveStoneKey, "Adds 50 stone near the camera", ItemHandle.Stone),
            Resource(GiveFoodKey, "Adds 50 meals near the camera", ItemHandle.Meal),
        };

        public static string TabKey(DebugTab tab) =>
            tab == DebugTab.Events ? EventsKey
            : tab == DebugTab.Spawn ? SpawnTabKey
            : tab == DebugTab.Weather ? WeatherTabKey
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

        /// <summary>Whether the rain is drawn by the particle control arm rather than the GPU.</summary>
        public bool RainAsParticles { get; private set; }

        /// <summary>Whether wet ground is drawn as gloss only, rather than richer and a little darker.</summary>
        public bool WetGlossOnly { get; private set; }

        /// <summary>Raised when one of the two drawing switches changes, and only then.</summary>
        public event Action? WeatherChanged;

        public void SetRainAsParticles(bool on)
        {
            if (RainAsParticles == on) return;
            RainAsParticles = on;
            WeatherChanged?.Invoke();
        }

        public void SetWetGlossOnly(bool on)
        {
            if (WetGlossOnly == on) return;
            WetGlossOnly = on;
            WeatherChanged?.Invoke();
        }
    }
}
