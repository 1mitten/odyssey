#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// The graphics levers the panel can throw. The order is the order they are drawn in.
    ///
    /// <para>Every one of these is <b>decoration</b>: none is a cell, none is in the save and none
    /// reaches the hash. That is why a settings panel may write them at all — it bypasses the
    /// intent queue (`10-ui-panel-catalogue.md` B17), which no panel that touched the simulation
    /// would be allowed to do.</para>
    /// </summary>
    public enum GraphicsOption
    {
        Shadows,
        Surround,
        GrassTufts,
        GroundRelief,
        SeeThrough,
    }

    /// <summary>
    /// What pressing Escape should do, given what is open. Returned rather than performed because
    /// the order is a rule worth testing in the fast tier, and the doing of it needs Unity.
    /// </summary>
    public enum EscapeAction
    {
        /// <summary>Put down the tool the player is holding.</summary>
        DisarmTool,

        /// <summary>Close the Build palette, which is the one panel that opens over the board.</summary>
        ClosePalette,

        /// <summary>Close the Menu popover.</summary>
        CloseMenu,

        /// <summary>Close the settings panel.</summary>
        ClosePanel,

        /// <summary>Nothing is open and nothing is held, so Escape is the way into the menu.</summary>
        OpenPanel,
    }

    /// <summary>
    /// Where a graphics preference is kept between sessions.
    ///
    /// <para>An interface rather than a call to <c>PlayerPrefs</c> because this assembly is
    /// compiled without UnityEngine (ADR 0003) and must keep running in the fast tier. The
    /// implementation lives in the Presentation assembly, which is allowed to know about Unity.
    /// </para>
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>The stored value, or null if this machine has never been told.</summary>
        bool? Read(string key);

        void Write(string key, bool value);

        /// <summary>The stored number, or null if this machine has never been told.</summary>
        int? ReadInt(string key);

        void WriteInt(string key, int value);

        /// <summary>
        /// The stored word, or null if this machine has never been told. Key bindings are
        /// words — a binding is a key's <i>name</i>, not a number some parser would have to
        /// keep in step with an enum it cannot see.
        /// </summary>
        string? ReadString(string key);

        void WriteString(string key, string value);
    }

    /// <summary>
    /// The sections of the settings panel.
    ///
    /// <para>Four, because there are four kinds of thing in it and they answer different
    /// questions: <see cref="Interface"/> is how the HUD and the camera are handled,
    /// <see cref="Graphics"/> is how the world is drawn, <see cref="Audio"/> is how it
    /// sounds, and <see cref="Keys"/> is what the keyboard does. The panel held only the
    /// second until 2026-09-16, when the owner reported the HUD's type reading too small on
    /// a 4K monitor and the fix was a setting rather than a constant; the other two arrived
    /// together on 2026-09-17, when the keybindings became a binding map and the audio
    /// faders, waiting in their store since the sound work landed, finally had a panel to
    /// live in.</para>
    /// </summary>
    public enum SettingsTab
    {
        Interface,
        Graphics,
        Audio,
        Keys,
    }

    /// <summary>
    /// One volume fader. Mirrors the sound buses the Presentation assembly keeps — the same
    /// five, in the same order — because the Hud assembly cannot see
    /// <c>Odyssey.Presentation.Audio.SoundBus</c> and must not (ADR 0003). The map between
    /// them is one cast in the presenter, and a bus added on one side is a compile error on
    /// the other the moment the panel loop reaches it.
    /// </summary>
    public enum SettingsBus
    {
        Master,
        Music,
        Ambience,
        Effects,
        Alerts,
    }

    /// <summary>
    /// The settings panel: whether it is open, and the levers it holds — the graphics
    /// options, the interface and camera speeds, the developer readout, the volume faders,
    /// and the way out.
    ///
    /// <para><b>Why a director owns this at all.</b> Until now every graphics lever was a field on
    /// <c>OdysseyBootstrap</c>, which means stopping play, changing a number and pressing play
    /// again — once per variable. Every look decision this project has made ended with a note that
    /// it still wanted the owner's eye in the play scene, and judging one is really judging A
    /// against B. A panel that throws the lever while the colony runs turns a session per question
    /// into a question per session. It is also the surface the quality tier needs, since the
    /// golden-hour work has already committed to a Low tier that drops the expensive effects.</para>
    ///
    /// <para><b>Two kinds of lever, and the difference is not cosmetic.</b> Shadows and the
    /// surround are read as the frame is drawn, so throwing them is free and instant. Grass tufts
    /// and ground relief are baked into the instance matrices when a chunk is meshed, so throwing
    /// them means meshing the board again. <see cref="NeedsRedraw"/> is how the presenter knows
    /// which it is holding, and the panel says so beside the row rather than leaving the player to
    /// wonder why one toggle stutters and three do not.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class SettingsDirector
    {
        /// <summary>The registry key naming the panel itself.</summary>
        public const string PanelKey = "ui.settings.panel";

        /// <summary>The registry key naming the graphics section.</summary>
        public const string GraphicsKey = "ui.settings.graphics";

        /// <summary>The registry key naming the interface section.</summary>
        public const string InterfaceKey = "ui.settings.interface";

        /// <summary>The registry key naming the interface-scale row.</summary>
        public const string UiScaleKey = "ui.settings.uiscale";

        /// <summary>The registry key naming the audio section.</summary>
        public const string AudioKey = "ui.settings.audio";

        /// <summary>The registry key naming the camera-speed row.</summary>
        public const string CamSpeedKey = "ui.settings.camspeed";

        /// <summary>The registry key naming the developer-overlay row.</summary>
        public const string DeveloperKey = "ui.settings.developer";

        /// <summary>The registry key naming the exit row.</summary>
        public const string ExitKey = "ui.settings.exit";

        static readonly GraphicsOption[] Order =
        {
            GraphicsOption.Shadows,
            GraphicsOption.Surround,
            GraphicsOption.GrassTufts,
            GraphicsOption.GroundRelief,
            GraphicsOption.SeeThrough,
        };

        /// <summary>
        /// Every key this panel can put on screen, so <c>RegistryTests</c> can hold the whole
        /// panel to the naming CSV the way it already holds the ledger and the job list. A label
        /// invented in C# is a label the owner cannot correct.
        /// </summary>
        public static readonly string[] IconKeys =
        {
            PanelKey,
            GraphicsKey,
            InterfaceKey,
            UiScaleKey,
            AudioKey,
            CamSpeedKey,
            DeveloperKey,
            ExitKey,
            "ui.settings.shadows",
            "ui.settings.surround",
            "ui.settings.grass",
            "ui.settings.relief",
            "ui.settings.seethrough",
            "ui.settings.volume.master",
            "ui.settings.volume.music",
            "ui.settings.volume.ambience",
            "ui.settings.volume.effects",
            "ui.settings.volume.alerts",
        };

        /// <summary>The registry key naming one volume row.</summary>
        public static string VolumeKey(SettingsBus bus) => bus switch
        {
            SettingsBus.Master => "ui.settings.volume.master",
            SettingsBus.Music => "ui.settings.volume.music",
            SettingsBus.Ambience => "ui.settings.volume.ambience",
            SettingsBus.Effects => "ui.settings.volume.effects",
            SettingsBus.Alerts => "ui.settings.volume.alerts",
            _ => AudioKey,
        };

        /// <summary>The registry key naming one tab chip.</summary>
        public static string TabKey(SettingsTab tab) => tab switch
        {
            SettingsTab.Interface => InterfaceKey,
            SettingsTab.Graphics => GraphicsKey,
            SettingsTab.Audio => AudioKey,
            SettingsTab.Keys => HotkeyDirector.KeysKey,
            _ => PanelKey,
        };

        /// <summary>
        /// The interface scales the panel offers, as percentages.
        ///
        /// <para>A ladder rather than a slider, and these six rather than a continuous range,
        /// because <c>09-ui-and-input.md</c> §9 D4 fixes the band at 80 to 150 per cent and
        /// because a HUD drawn at a fractional scale is a HUD whose one-pixel hairlines land
        /// between pixels. The steps are close enough together that no two adjacent ones look
        /// like a jump.</para>
        ///
        /// <para><b>Above 100 the HUD covers more of the board</b>, which is the trade the player
        /// is making: the coverage ceiling the interface was built to is stated at 100, and at 150
        /// on a 16:9 screen the HUD occupies about a quarter of it rather than a
        /// ninth.</para>
        /// </summary>
        public static readonly int[] UiScales = { 80, 90, 100, 110, 125, 150 };

        /// <summary>
        /// The camera speeds the panel offers, as percentages of the speeds the rig was tuned
        /// at. A ladder for the same reason the interface scale is one: three honest answers
        /// rather than a continuous knob nobody asked to fine-tune, and the rungs snap, so a
        /// stored value from an older ladder can never leave the panel showing a speed none
        /// of its own buttons can reproduce.
        /// </summary>
        public static readonly int[] CameraSpeeds = { 60, 100, 150 };

        /// <summary>
        /// The labelled marks along a volume fader, in dB. **No longer the only values a bus
        /// can hold** — see <see cref="SetBusDb"/> — but still what the track is printed with,
        /// so a player can see where −24 is without dragging to find out.
        /// </summary>
        public static readonly int[] VolumeDbRungs = { -80, -36, -24, -16, -10, -5, 0 };

        /// <summary>
        /// True silence, restated from <c>AudioMath.SilenceDb</c> in the Presentation assembly
        /// so the panel and the audio code cannot disagree about where silence starts.
        /// <c>DbToLinear</c> returns exactly zero at or below it.
        /// </summary>
        public const int SilenceDb = -80;

        /// <summary>
        /// The bottom and top of a fader's travel. Unity at the top; −60 at the bottom, which a
        /// drag reports as <see cref="SilenceDb"/>.
        ///
        /// <para><b>Why the travel stops at −60 and not at −80.</b> −60 dB is a thousandth of
        /// unity amplitude and is inaudible, so a track drawn over the full −80…0 would spend a
        /// quarter of its length on values that all sound like nothing. Every mixer ever built
        /// ends its throw at −∞ for the same reason. The bottom of this one is that −∞.</para>
        ///
        /// <para><b>And why the track is linear in dB.</b> This control was a seven-rung ladder,
        /// on the argument that "a slider position is a lie about loudness: equal steps of dB are
        /// equal steps of hearing". That argument is right, and it is an argument <i>for</i> a
        /// track measured in dB rather than against a slider — it only convicts a fader linear in
        /// amplitude, where the top half of the throw does almost nothing. Linear in dB, a
        /// millimetre of travel is the same change in loudness wherever the handle is, which is
        /// exactly what the rungs were protecting. The owner asked for a drag (2026-09-17); this
        /// is the same honesty at finer resolution than seven stops.</para>
        /// </summary>
        public const int VolumeDbFloor = -60;

        public const int VolumeDbCeiling = 0;

        /// <summary>
        /// Where along a fader's track a stored volume sits, 0 at the bottom and 1 at unity.
        /// Silence and anything under the floor are the bottom of the travel.
        /// </summary>
        public static float VolumeFraction(int db)
        {
            if (db <= VolumeDbFloor) return 0f;
            if (db >= VolumeDbCeiling) return 1f;
            return (db - VolumeDbFloor) / (float)(VolumeDbCeiling - VolumeDbFloor);
        }

        /// <summary>
        /// The volume a fader dragged to <paramref name="fraction"/> of its track means, in whole
        /// dB. The bottom of the travel is <see cref="SilenceDb"/> rather than
        /// <see cref="VolumeDbFloor"/>, so dragging a fader to the end silences the bus outright
        /// instead of leaving it a thousandth of the way up.
        /// </summary>
        public static int VolumeDbAt(float fraction)
        {
            if (fraction <= 0f) return SilenceDb;
            if (fraction >= 1f) return VolumeDbCeiling;

            int db = (int)Math.Round(VolumeDbFloor + fraction * (VolumeDbCeiling - VolumeDbFloor));
            return db <= VolumeDbFloor ? SilenceDb : db;
        }

        /// <summary>The buses, in the order the panel draws them.</summary>
        public static readonly SettingsBus[] Buses =
        {
            SettingsBus.Master,
            SettingsBus.Music,
            SettingsBus.Ambience,
            SettingsBus.Effects,
            SettingsBus.Alerts,
        };

        /// <summary>The scale a screen this tall should start at, before any stored preference.
        ///
        /// <para>The interface is authored in 1080p pixels, so on a 1080p monitor one reference
        /// pixel is one real one and 100 is right by construction. It is also right on a 4K
        /// monitor <i>in angular terms</i> — the panel scales with the screen, so the type
        /// subtends the same angle — and the owner's report on 2026-09-16 was nonetheless that it
        /// read too small there. Physically identical is not perceptually identical at arm's
        /// length from a large panel, and the HUD this replaced was drawn against a 1200 x 800
        /// reference, which made every glyph on it 1.6 times larger. So a tall screen starts a
        /// step or two up, and the player can move it either way.</para>
        ///
        /// <para><b>125 at 4K is arithmetic rather than taste.</b> The HUD this replaced set its
        /// body text at 11 px against a 1200 x 800 canvas; on a 3840 x 2160 screen, Unity's
        /// match-0.5 scaling is the geometric mean of 3.2 and 2.7, so that text landed at about
        /// <b>32 physical pixels</b>. This one sets body text at 13 px, and at 125 per cent the
        /// canvas is 1536 x 864, so the scale is exactly 2.5 and the text lands at
        /// <b>32.5 physical pixels</b>. The default therefore restores the size the owner was
        /// reading at before the rebuild, which is the size they were telling us about.</para>
        /// </summary>
        public static int DefaultScaleFor(int screenHeight) =>
            screenHeight >= 2160 ? 125 :
            screenHeight >= 1440 ? 110 : 100;

        readonly Dictionary<GraphicsOption, bool> _on = new();
        readonly Dictionary<SettingsBus, int> _db = new();

        ISettingsStore? _store;

        public SettingsDirector()
        {
            foreach (GraphicsOption option in Order) _on[option] = true;
            foreach (SettingsBus bus in Buses) _db[bus] = 0;
        }

        /// <summary>The options, in the order they are drawn.</summary>
        public static IReadOnlyList<GraphicsOption> All => Order;

        public bool Open { get; private set; }

        /// <summary>Which section is showing. Interface first, because it is the one a player
        /// reaches for on the first evening.</summary>
        public SettingsTab Tab { get; private set; } = SettingsTab.Interface;

        /// <summary>How large the HUD is drawn, as a percentage. Always a member of
        /// <see cref="UiScales"/>.</summary>
        public int UiScale { get; private set; } = 100;

        /// <summary>How fast the camera moves and zooms, as a percentage of the tuned speeds.
        /// Always a member of <see cref="CameraSpeeds"/>.</summary>
        public int CameraSpeed { get; private set; } = 100;

        /// <summary>Whether the developer readout is drawn. Seeded from the overlay director,
        /// so the row describes the screen rather than dictating to it.</summary>
        public bool DeveloperOverlay { get; private set; }

        /// <summary>Whether the exit row has been clicked once and is asking to be sure.</summary>
        public bool ExitArmed { get; private set; }

        /// <summary>Raised when the panel opens or closes.</summary>
        public event Action? Changed;

        /// <summary>Raised when the showing section changes.</summary>
        public event Action<SettingsTab>? TabChanged;

        /// <summary>Raised when the interface scale changes, with the new percentage.</summary>
        public event Action<int>? UiScaleChanged;

        /// <summary>Raised when one option's value changes, with the option that changed.</summary>
        public event Action<GraphicsOption>? OptionChanged;

        /// <summary>Raised when the camera speed changes, with the new percentage.</summary>
        public event Action<int>? CameraSpeedChanged;

        /// <summary>Raised when the developer overlay is switched by the panel.</summary>
        public event Action? DeveloperOverlayChanged;

        /// <summary>Raised when one bus's volume changes, with the bus that changed.</summary>
        public event Action<SettingsBus>? BusDbChanged;

        /// <summary>Raised when the exit row arms or stands down.</summary>
        public event Action? ExitChanged;

        /// <summary>
        /// Raised when the player has asked twice to leave. Not performed here because
        /// quitting is the engine's to do — and because "the panel asked the game to leave"
        /// is a sentence the fast tier can assert without one.
        /// </summary>
        public event Action? ExitRequested;

        public bool IsOn(GraphicsOption option) => _on.TryGetValue(option, out bool on) && on;

        /// <summary>
        /// Whether throwing this lever costs a remesh of the board. True for anything baked into
        /// an instance matrix, false for anything read as the frame is drawn.
        /// </summary>
        public static bool NeedsRedraw(GraphicsOption option) =>
            option is GraphicsOption.GrassTufts or GraphicsOption.GroundRelief;

        /// <summary>The registry key naming this option. Never a word: words live in the CSV.</summary>
        public static string KeyOf(GraphicsOption option) => option switch
        {
            GraphicsOption.Shadows => "ui.settings.shadows",
            GraphicsOption.Surround => "ui.settings.surround",
            GraphicsOption.GrassTufts => "ui.settings.grass",
            GraphicsOption.GroundRelief => "ui.settings.relief",
            GraphicsOption.SeeThrough => "ui.settings.seethrough",
            _ => "ui.settings.panel",
        };

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            // The armed exit row lives only in this panel, so the panel going away stands it
            // down. A "quit?" that survived its own panel would be a trap armed across the
            // whole screen.
            if (!open && ExitArmed)
            {
                ExitArmed = false;
                ExitChanged?.Invoke();
            }
            Changed?.Invoke();
        }

        public void Toggle() => SetOpen(!Open);

        public void SetTab(SettingsTab tab)
        {
            if (Tab == tab) return;
            Tab = tab;
            TabChanged?.Invoke(Tab);
        }

        /// <summary>
        /// Move the interface scale. Anything not on the ladder snaps to the nearest rung, so a
        /// stored preference from an older ladder cannot leave the panel showing a value none of
        /// its own buttons can reproduce.
        /// </summary>
        public void SetUiScale(int percent)
        {
            int snapped = Nearest(UiScales, percent);
            if (UiScale == snapped) return;
            UiScale = snapped;
            _store?.WriteInt(UiScaleKey, snapped);
            UiScaleChanged?.Invoke(snapped);
        }

        /// <summary>Move one rung up or down the ladder; at either end, stay there.</summary>
        public void StepUiScale(int rungs)
        {
            int at = Array.IndexOf(UiScales, UiScale);
            if (at < 0) at = Array.IndexOf(UiScales, Nearest(UiScales, UiScale));
            SetUiScale(UiScales[Math.Max(0, Math.Min(UiScales.Length - 1, at + rungs))]);
        }

        static int Nearest(int[] rungs, int value)
        {
            int best = rungs[0];
            foreach (int rung in rungs)
                if (Math.Abs(rung - value) < Math.Abs(best - value)) best = rung;
            return best;
        }

        public void Set(GraphicsOption option, bool on)
        {
            if (IsOn(option) == on) return;
            _on[option] = on;
            _store?.Write(KeyOf(option), on);
            OptionChanged?.Invoke(option);
        }

        public void Toggle(GraphicsOption option) => Set(option, !IsOn(option));

        /// <summary>
        /// Move the camera speed. Anything not on the ladder snaps to the nearest rung —
        /// the same bargain <see cref="SetUiScale"/> makes, for the same reason.
        /// </summary>
        public void SetCameraSpeed(int percent)
        {
            int snapped = Nearest(CameraSpeeds, percent);
            if (CameraSpeed == snapped) return;
            CameraSpeed = snapped;
            _store?.WriteInt(CamSpeedKey, snapped);
            CameraSpeedChanged?.Invoke(snapped);
        }

        /// <summary>
        /// Switch the developer readout from the panel. The backquote key reaches the same
        /// lever through the overlay director, so this writes the preference down — the key
        /// never did, which is why the readout was off again every session.
        /// </summary>
        public void SetDeveloperOverlay(bool on)
        {
            if (DeveloperOverlay == on) return;
            DeveloperOverlay = on;
            _store?.Write(DeveloperKey, on);
            DeveloperOverlayChanged?.Invoke();
        }

        /// <summary>
        /// Move one bus's volume. Anything not on the ladder snaps to the nearest rung.
        /// Not written to <see cref="ISettingsStore"/>: the volumes keep their own store in
        /// the Presentation assembly (<c>AudioSettingsStore</c>, in dB, under its own prefix,
        /// since before this panel existed), and the presenter writes through to it so the
        /// two stores never hold one fader between them.
        /// </summary>
        public void SetBusDb(SettingsBus bus, int db)
        {
            int held = Clamp(db);
            if (_db[bus] == held) return;
            _db[bus] = held;
            BusDbChanged?.Invoke(bus);
        }

        /// <summary>
        /// A volume this director will hold: whole dB between the floor and unity, with anything
        /// at or under the floor collapsing to <see cref="SilenceDb"/> so there is exactly one
        /// value meaning silence rather than twenty of them.
        /// </summary>
        static int Clamp(int db) =>
            db <= VolumeDbFloor ? SilenceDb
            : db >= VolumeDbCeiling ? VolumeDbCeiling
            : db;

        /// <summary>
        /// One bus's volume, in whole dB: <see cref="SilenceDb"/>, or between
        /// <see cref="VolumeDbFloor"/> and <see cref="VolumeDbCeiling"/>.
        /// </summary>
        public int BusDb(SettingsBus bus) => _db[bus];

        /// <summary>
        /// The exit row: click once to arm, twice to leave.
        ///
        /// <para>Two clicks because nothing is saved — there is no save system yet — so the
        /// row must not be a key the player can hit by reaching past it for the close button.
        /// No timeout, because a clock the director does not have would be a clock it could
        /// not test; the armed row says what it wants and the panel closing stands it
        /// down.</para>
        /// </summary>
        public void RequestExit()
        {
            if (!ExitArmed)
            {
                ExitArmed = true;
                ExitChanged?.Invoke();
                return;
            }

            ExitArmed = false;
            ExitRequested?.Invoke();
            ExitChanged?.Invoke();
        }

        /// <summary>
        /// Record what the scene was already configured to do, without raising anything and
        /// without writing it back.
        ///
        /// <para>The inspector fields on the bootstrap stay the source of truth for how a session
        /// starts, so the panel opens describing the board that is actually on screen. A panel
        /// whose defaults quietly overrode the scene would be a panel that changed the game by
        /// existing.</para>
        /// </summary>
        public void Seed(GraphicsOption option, bool on) => _on[option] = on;

        /// <summary>
        /// Record the scale this screen should start at, without raising anything and without
        /// writing it back — the same bargain <see cref="Seed(GraphicsOption, bool)"/> makes. A
        /// stored preference is laid over it by <see cref="UseStore"/>, so the machine beats the
        /// screen and the screen beats nothing at all.
        /// </summary>
        public void SeedUiScale(int percent) => UiScale = Nearest(UiScales, percent);

        /// <summary>
        /// Record the camera speed the rig was tuned at, without raising anything — the same
        /// bargain every seed here makes. A stored preference is laid over it by
        /// <see cref="UseStore"/>.
        /// </summary>
        public void SeedCameraSpeed(int percent) => CameraSpeed = Nearest(CameraSpeeds, percent);

        /// <summary>
        /// Record whether the developer readout is already on screen, so the row describes
        /// the screen rather than changing it by existing. Whatever armed it — the backquote
        /// key, a stored preference from last session — the row starts telling the truth.
        /// </summary>
        public void SeedDeveloperOverlay(bool on) => DeveloperOverlay = on;

        /// <summary>
        /// Record one bus's volume as the audio store left it, without raising anything. The
        /// presenter lays <c>AudioSettingsStore.Load()</c> in through this, so the panel opens
        /// describing what the game is already playing at.
        /// </summary>
        public void SeedBusDb(SettingsBus bus, int db) => _db[bus] = Clamp(db);

        /// <summary>
        /// Attach the place preferences are kept, and apply anything this machine has already been
        /// told. Stored values are laid over the seeded ones, so a preference beats the scene and
        /// the scene beats nothing at all.
        /// </summary>
        public void UseStore(ISettingsStore store)
        {
            _store = store;
            foreach (GraphicsOption option in Order)
            {
                bool? stored = store.Read(KeyOf(option));
                if (stored.HasValue) Set(option, stored.Value);
            }

            int? scale = store.ReadInt(UiScaleKey);
            if (scale.HasValue) SetUiScale(scale.Value);

            int? speed = store.ReadInt(CamSpeedKey);
            if (speed.HasValue) SetCameraSpeed(speed.Value);

            bool? developer = store.Read(DeveloperKey);
            if (developer.HasValue) SetDeveloperOverlay(developer.Value);
        }

        /// <summary>
        /// What Escape means right now. The order is fixed by `09-ui-and-input.md` §6: cancel the
        /// active tool first, then close the top panel, and only then open the menu.
        ///
        /// <para>It is decided here, in one place, rather than in each component that happens to
        /// read the key. Escape was already spoken for by the designate tool, and a second
        /// listener would have disarmed the tool and opened the panel in the same keystroke —
        /// the sort of fault that looks like a flicker and is diagnosed as a rendering bug.</para>
        /// </summary>
        public EscapeAction Escape(bool toolArmed) => Escape(toolArmed, paletteOpen: false);

        /// <summary>
        /// The same rule with the Build palette in it.
        ///
        /// <para>The palette used to be a column pinned to the left edge and permanently open, so
        /// it was never something Escape had to unwind. The interface rebuild makes it a panel
        /// opened by the Build command and closed by Escape, which puts it in the middle of the
        /// order: a tool is held in the hand and comes off first, the palette is the panel the
        /// player just opened over the board, and the menu is the last resort.</para>
        /// </summary>
        public EscapeAction Escape(bool toolArmed, bool paletteOpen) =>
            Escape(toolArmed, paletteOpen, menuOpen: false);

        /// <summary>
        /// The same rule with the Menu popover in it as well (owner, 2026-09-17: every window can
        /// be escaped).
        ///
        /// <para>The two bar popovers sit at the same level of the order, between the tool in the
        /// hand and the settings panel, because they are the same kind of thing: a panel the
        /// player raised from a button a moment ago. Only one of them can be open at a time — the
        /// shell closes the other when either opens — so their relative order is not a decision
        /// anything can observe, and the menu is tested first only because it is the newer of the
        /// two.</para>
        /// </summary>
        public EscapeAction Escape(bool toolArmed, bool paletteOpen, bool menuOpen)
        {
            if (toolArmed) return EscapeAction.DisarmTool;
            if (menuOpen) return EscapeAction.CloseMenu;
            if (paletteOpen) return EscapeAction.ClosePalette;
            return Open ? EscapeAction.ClosePanel : EscapeAction.OpenPanel;
        }
    }
}
