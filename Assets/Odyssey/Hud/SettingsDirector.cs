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

        /// <summary>
        /// Cut the roof off the layer you are working on, so you can see into the rooms on it.
        ///
        /// <para><b>The one option that is off by default</b>, and the reason is the owner's report
        /// (2026-09-17): *"I expected to see and be able to build at least floor above from my
        /// current height."* The cut-away is what made a floor built directly overhead invisible
        /// and unclickable, because a surface that is not drawn must not be a pointer target.
        /// Seeing what you have just built is the commoner need; seeing who is indoors without
        /// changing depth is the specialist one, so it is the specialist that asks.</para>
        /// </summary>
        CutAwayCeiling,
    }

    /// <summary>
    /// The graphics levers that answer with a <b>number</b> rather than yes or no — how the frame
    /// is paced, how large it is drawn, and what it is drawn with.
    ///
    /// <para><b>Why a second enum rather than six more properties.</b> The interface scale and the
    /// camera speed each arrived as their own <c>int[]</c>, their own property, their own setter
    /// and their own event, and by the third the shape was plainly a copy. Seven more would be
    /// seven more copies of a rule that has to behave identically in all of them — snap to a rung,
    /// write through, raise once — which is exactly the "one rule, several owners" fault
    /// <c>docs/bug-patterns.md</c> opens with. One enum, one table per question, one setter.</para>
    ///
    /// <para><b>Every rung is an <c>int</c>, and wherever Unity has a number of its own the rung
    /// <em>is</em> that number</b> — <c>vSyncCount</c>, <c>msaaSampleCount</c> and
    /// <c>FullScreenMode</c> are stored verbatim, so the presenter casts rather than translates
    /// and a table cannot drift from the API it feeds. The two that are not Unity's own number say
    /// so: the render scale is a percentage because a ladder of <c>0.7</c> would be the only
    /// fractional thing in a file of whole ones, and the frame cap keeps <c>0</c> for uncapped
    /// because <c>-1</c> as a rung would sort before 30.</para>
    ///
    /// <para>UnityEngine-free like everything else here (ADR 0003), so the rungs, the defaults and
    /// the snapping all run in the fast tier.</para>
    /// </summary>
    public enum GraphicsLadder
    {
        /// <summary>Frames held back to the screen's own refresh. <c>QualitySettings.vSyncCount</c>.</summary>
        VSync,

        /// <summary>The ceiling on frames a second. <c>Application.targetFrameRate</c>.</summary>
        FrameCap,

        /// <summary>How large the world is drawn before it is scaled to the window, as a
        /// percentage. The HUD is drawn afterwards and stays sharp.</summary>
        RenderScale,

        /// <summary>Multisampling, as a sample count. The renderer is Forward+, so this is
        /// honoured; under Deferred it would be silently ignored.</summary>
        AntiAliasing,

        /// <summary>How far from the camera shadows are still drawn, in metres.</summary>
        ShadowDistance,

        /// <summary>Fullscreen, borderless or a window. <c>UnityEngine.FullScreenMode</c>.</summary>
        DisplayMode,
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

        /// <summary>The registry key naming the Build-palette layout row.</summary>
        public const string BuildLayoutKey = "ui.settings.buildlayout";

        /// <summary>The registry key naming the exit row.</summary>
        /// <summary>The heading over the levers that decide how the frame is paced and drawn.</summary>
        public const string DisplayGroupKey = "ui.settings.display";

        /// <summary>The heading over the levers that decide what the board is drawn with.</summary>
        public const string DetailGroupKey = "ui.settings.detail";

        /// <summary>How many pixels the game is drawn at. Its own key, because its rungs are the
        /// machine's rather than ours — see <see cref="Resolutions"/>.</summary>
        public const string ResolutionKey = "ui.settings.resolution";

        public const string ExitKey = "ui.settings.exit";

        static readonly GraphicsOption[] Order =
        {
            GraphicsOption.Shadows,
            GraphicsOption.Surround,
            GraphicsOption.GrassTufts,
            GraphicsOption.GroundRelief,
            GraphicsOption.SeeThrough,
            GraphicsOption.CutAwayCeiling,
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
            BuildLayoutKey,
            ExitKey,
            "ui.settings.shadows",
            "ui.settings.surround",
            "ui.settings.grass",
            "ui.settings.relief",
            "ui.settings.seethrough",
            "ui.settings.cutaway",
            "ui.settings.volume.master",
            "ui.settings.volume.music",
            "ui.settings.volume.ambience",
            "ui.settings.volume.effects",
            "ui.settings.volume.alerts",
            DisplayGroupKey,
            DetailGroupKey,
            ResolutionKey,
            "ui.settings.vsync",
            "ui.settings.framecap",
            "ui.settings.renderscale",
            "ui.settings.antialias",
            "ui.settings.shadowdist",
            "ui.settings.displaymode",
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

        // ==================================================================== the number ladders

        /// <summary>
        /// The ladders, in the order the Display group draws them. VSync leads because it is the
        /// one a player comes looking for, and the frame cap follows it because the cap means
        /// nothing until VSync is off (<see cref="FrameCapIsLive"/>).
        /// </summary>
        static readonly GraphicsLadder[] LadderOrder =
        {
            GraphicsLadder.VSync,
            GraphicsLadder.FrameCap,
            GraphicsLadder.RenderScale,
            GraphicsLadder.AntiAliasing,
            GraphicsLadder.ShadowDistance,
            GraphicsLadder.DisplayMode,
        };

        /// <summary>The ladders, in the order they are drawn.</summary>
        public static IReadOnlyList<GraphicsLadder> AllLadders => LadderOrder;

        // Unity's own numbers wherever Unity has one, so the presenter casts rather than
        // translates. See the enum's remarks for the two that are not.
        static readonly int[] VSyncRungs = { 0, 1, 2 };
        static readonly int[] FrameCapRungs = { 30, 60, 120, 144, 0 };
        static readonly int[] RenderScaleRungs = { 70, 85, 100 };
        static readonly int[] AntiAliasRungs = { 1, 2, 4, 8 };
        static readonly int[] ShadowDistanceRungs = { 30, 60, 120 };
        static readonly int[] DisplayModeRungs = { ExclusiveFullScreen, BorderlessWindow, Windowed };

        /// <summary><c>FullScreenMode.ExclusiveFullScreen</c>. Named rather than written as 0,
        /// because a bare integer here is a number nobody can check against the API.</summary>
        public const int ExclusiveFullScreen = 0;

        /// <summary><c>FullScreenMode.FullScreenWindow</c> — borderless, and the default,
        /// because it is the one that alt-tabs without a mode switch.</summary>
        public const int BorderlessWindow = 1;

        /// <summary><c>FullScreenMode.Windowed</c>. <c>MaximizedWindow</c> (2) is deliberately not
        /// offered: it is a macOS idiom and the third rung would buy nothing on Windows.</summary>
        public const int Windowed = 3;

        /// <summary>The rung meaning "no ceiling at all". <c>0</c> rather than Unity's
        /// <c>-1</c> so that it sorts to the end of the ladder instead of the front.</summary>
        public const int Uncapped = 0;

        /// <summary>The rungs one ladder offers, in the order they are drawn.</summary>
        public static int[] RungsOf(GraphicsLadder ladder) => ladder switch
        {
            GraphicsLadder.VSync => VSyncRungs,
            GraphicsLadder.FrameCap => FrameCapRungs,
            GraphicsLadder.RenderScale => RenderScaleRungs,
            GraphicsLadder.AntiAliasing => AntiAliasRungs,
            GraphicsLadder.ShadowDistance => ShadowDistanceRungs,
            GraphicsLadder.DisplayMode => DisplayModeRungs,
            _ => VSyncRungs,
        };

        /// <summary>
        /// Where a ladder rests on a machine that has never been told.
        ///
        /// <para><b>VSync on, and the frame uncapped behind it.</b> A colony sim is not a game
        /// anybody wins by a millisecond, and tearing is the one artefact a player cannot unsee.
        /// The cap is what they reach for after turning VSync off, so it starts where it does no
        /// work. Anti-aliasing starts off: it is the most expensive thing on the page and the
        /// board is mostly flat colour, so it is a choice rather than a baseline.</para>
        ///
        /// <para>The shadow distance is the one default that is a lie until the presenter seeds
        /// it — the pipeline asset's own figure wins, because the golden-hour work tuned it.</para>
        /// </summary>
        public static int DefaultOf(GraphicsLadder ladder) => ladder switch
        {
            GraphicsLadder.VSync => 1,
            GraphicsLadder.FrameCap => Uncapped,
            GraphicsLadder.RenderScale => 100,
            GraphicsLadder.AntiAliasing => 1,
            GraphicsLadder.ShadowDistance => 60,
            GraphicsLadder.DisplayMode => BorderlessWindow,
            _ => 0,
        };

        /// <summary>The registry key naming one ladder's row.</summary>
        public static string KeyOf(GraphicsLadder ladder) => ladder switch
        {
            GraphicsLadder.VSync => "ui.settings.vsync",
            GraphicsLadder.FrameCap => "ui.settings.framecap",
            GraphicsLadder.RenderScale => "ui.settings.renderscale",
            GraphicsLadder.AntiAliasing => "ui.settings.antialias",
            GraphicsLadder.ShadowDistance => "ui.settings.shadowdist",
            GraphicsLadder.DisplayMode => "ui.settings.displaymode",
            _ => GraphicsKey,
        };

        /// <summary>
        /// What one rung says on its face.
        ///
        /// <para>Built here rather than in the shell because the fast tier can then assert that
        /// every rung has a word, and because the shell builds each string <b>once</b>, at
        /// construction: ADR 0003's flip condition F1 is that the HUD allocates nothing per frame
        /// in steady state, and a label composed in a refresh handler is the usual way that
        /// stops being true.</para>
        /// </summary>
        public static string RungLabel(GraphicsLadder ladder, int rung) => ladder switch
        {
            GraphicsLadder.VSync => rung switch
            {
                0 => "Off",
                2 => "Half",
                _ => "On",
            },
            GraphicsLadder.FrameCap => rung == Uncapped ? "Uncapped" : rung.ToString(),
            GraphicsLadder.RenderScale => rung + "%",
            GraphicsLadder.AntiAliasing => rung <= 1 ? "Off" : rung + "\u00d7",
            GraphicsLadder.ShadowDistance => rung + " m",
            GraphicsLadder.DisplayMode => rung switch
            {
                ExclusiveFullScreen => "Fullscreen",
                Windowed => "Windowed",
                _ => "Borderless",
            },
            _ => rung.ToString(),
        };

        /// <summary>
        /// What one rung costs or buys, said on hover. Every one of these names the trade rather
        /// than restating the number, which is the whole of what a tooltip is for here.
        /// </summary>
        public static string RungTooltip(GraphicsLadder ladder, int rung) => ladder switch
        {
            GraphicsLadder.VSync => rung switch
            {
                0 => "Draw as fast as the machine can. Tears, and runs the fans",
                2 => "One frame every second refresh. Half the rate, half the heat",
                _ => "One frame per refresh. No tearing",
            },
            GraphicsLadder.FrameCap => rung == Uncapped
                ? "No ceiling. The frame rate goes where the scene takes it"
                : $"Hold the frame to {rung} a second. A rate the machine can keep beats a higher one that swings",
            GraphicsLadder.RenderScale => rung == 100
                ? "Draw the world at the window's own size"
                : $"Draw the world at {rung}% and scale it up. The interface stays sharp",
            GraphicsLadder.AntiAliasing => rung <= 1
                ? "Stepped edges, and nothing spent on them"
                : $"{rung} samples an edge. The most expensive thing on this page",
            GraphicsLadder.ShadowDistance => rung switch
            {
                30 => "Shadows near the camera only. The cheapest",
                120 => "Shadows to the far rim. The dearest",
                _ => "Shadows over the working area",
            },
            GraphicsLadder.DisplayMode => rung switch
            {
                ExclusiveFullScreen => "The screen to itself. Fastest, slowest to alt-tab out of",
                Windowed => "A window you can size and move",
                _ => "Fills the screen with no border. Alt-tabs instantly",
            },
            _ => string.Empty,
        };

        /// <summary>
        /// Whether throwing this lever makes the renderer throw its buffers away and build new
        /// ones — a visible hitch, once, on the frame it changes.
        ///
        /// <para>The same bargain <see cref="NeedsRedraw"/> strikes for the toggles: the panel
        /// says so beside the row rather than leaving a player to wonder why one of these
        /// stutters and four do not. Pacing levers cost nothing; the ones that resize a render
        /// target cost a frame.</para>
        /// </summary>
        public static bool CostsAHitch(GraphicsLadder ladder) =>
            ladder == GraphicsLadder.RenderScale || ladder == GraphicsLadder.AntiAliasing;


        /// <summary>
        /// The floor of every volume fader, in dB. −80 because that is
        /// <c>AudioMath.SilenceDb</c> in the Presentation assembly — the fader's own floor,
        /// restated here so the panel and the audio code cannot disagree about where silence
        /// starts.
        /// </summary>
        public const int SilenceDb = -80;

        /// <summary>
        /// The default of every volume fader, in dB: unity, neither attenuated nor boosted.
        /// <c>AudioMath.UnityDb</c> across the seam, and the value the panel seats at the
        /// centre of its track.
        /// </summary>
        public const int UnityDb = 0;

        /// <summary>
        /// The most a bus may be boosted above unity, in dB. +12, mirroring
        /// <c>AudioMath.BoostDb</c> the same way <see cref="SilenceDb"/> mirrors its floor —
        /// the owner asked on 2026-09-17 for faders that raise as well as lower, and the
        /// ceiling is bounded because a boost amplifies the author's own volume and can clip.
        /// </summary>
        public const int BoostDb = 12;

        /// <summary>The buses, in the order the panel draws them.</summary>
        public static readonly SettingsBus[] Buses =
        {
            SettingsBus.Master,
            SettingsBus.Music,
            SettingsBus.Ambience,
            SettingsBus.Effects,
            SettingsBus.Alerts,
        };

        /// <summary>
        /// Where a dB sits on its fader's track: −1 at the left end, 0 at the centre, +1 at
        /// the right.
        ///
        /// <para><b>Unity is seated at the centre because the owner asked for it on
        /// 2026-09-17:</b> the default stands in the middle of the control, everything left
        /// of it lowers towards silence, everything right of it boosts to the ceiling. One
        /// linear track cannot say that — −80 to +12 would put unity six sevenths of the way
        /// to the right — so each half is linear in dB over its own span: 80 dB of
        /// attenuation across the left half, <see cref="BoostDb"/> of boost across the
        /// right. Within a half the fader's own argument holds (equal dB is equal hearing);
        /// that the halves are different lengths in dB is the price of a centre that means
        /// something, and it is paid in drag sensitivity rather than in honesty.</para>
        ///
        /// <para>Unity-free arithmetic on purpose, like everything here: the seating is an
        /// acceptance-shaped fact, and the fast tier holds it — unity at the centre, silence
        /// at the left end, boost at the right, and every whole dB round-trips.</para>
        /// </summary>
        public static float TrackOf(int db) =>
            db >= UnityDb
                ? Math.Min(1f, (float)db / BoostDb)
                : Math.Max(-1f, (float)db / -SilenceDb);

        /// <summary>
        /// The whole dB a track position stands at — the inverse of <see cref="TrackOf"/>,
        /// rounded to the nearest decibel, because a thumb that rests between two of them is
        /// a position the readout cannot say and the store cannot keep. Positions beyond the
        /// track clamp to its ends.
        /// </summary>
        public static int DbOf(float track)
        {
            float seated = Math.Clamp(track, -1f, 1f);
            return (int)Math.Round(seated < 0 ? seated * -SilenceDb : seated * BoostDb,
                MidpointRounding.AwayFromZero);
        }

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
        readonly Dictionary<GraphicsLadder, int> _rung = new();
        readonly Dictionary<SettingsBus, int> _db = new();
        readonly List<Mode> _modes = new();

        ISettingsStore? _store;

        public SettingsDirector()
        {
            foreach (GraphicsOption option in Order) _on[option] = DefaultOn(option);
            foreach (GraphicsLadder ladder in LadderOrder) _rung[ladder] = DefaultOf(ladder);
            foreach (SettingsBus bus in Buses) _db[bus] = 0;
        }

        /// <summary>
        /// Whether an option starts on.
        ///
        /// <para>Everything here has always started on, because every option so far has been a
        /// piece of the world that should be there unless a slow machine wants it gone. The
        /// ceiling cut-away is the first that is not: it <em>removes</em> something, and removing
        /// the floor a player has just built is the surprise it was reported as.</para>
        /// </summary>
        public static bool DefaultOn(GraphicsOption option) => option != GraphicsOption.CutAwayCeiling;

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

        /// <summary>
        /// Which of the three Build-palette layouts the player has chosen.
        ///
        /// <para><b>It lives here rather than on the palette</b> because there are two controls
        /// for it — the switcher in the palette's own header, for the player who is looking at the
        /// panel and wants it shaped differently, and a row in this panel, for the player who
        /// never finds an icon in a header. Two controls over one preference have to read and
        /// write one value or the second one a player touches will appear to undo the first.</para>
        /// </summary>
        public BuildPaletteLayout BuildPaletteLayout { get; private set; } = BuildPaletteModel.Default;

        /// <summary>
        /// The session row that has been clicked once and is asking to be sure, or null.
        ///
        /// <para>A key rather than a flag since U38, because the panel grew three more session
        /// rows — Save, Load and Quit to main menu — and two of them are as irreversible as the
        /// exit row. One armed row at a time, so pressing another stands the first down: a screen
        /// with two rows both asking "are you sure?" is a screen where the second press lands on
        /// whichever one the hand reaches first.</para>
        /// </summary>
        public string? ArmedRow { get; private set; }

        /// <summary>
        /// Whether the exit row is the one asking. Kept as its own name because the panel, its
        /// stylesheet class and the tests written before U38 all speak in these terms, and
        /// renaming them would have been a change to what this file means rather than to what it
        /// does.
        /// </summary>
        public bool ExitArmed => ArmedRow == ExitKey;

        /// <summary>Raised when the panel opens or closes.</summary>
        public event Action? Changed;

        /// <summary>Raised when the showing section changes.</summary>
        public event Action<SettingsTab>? TabChanged;

        /// <summary>Raised when the interface scale changes, with the new percentage.</summary>
        public event Action<int>? UiScaleChanged;

        /// <summary>Raised when one option's value changes, with the option that changed.</summary>
        public event Action<GraphicsOption>? OptionChanged;

        /// <summary>Raised when a number ladder moves. One event for all six, because the
        /// presenter's answer to every one of them is the same shape: read the rung, hand it to
        /// Unity, once.</summary>
        public event Action<GraphicsLadder>? LadderChanged;

        /// <summary>Raised when the camera speed changes, with the new percentage.</summary>
        public event Action<int>? CameraSpeedChanged;

        /// <summary>Raised when the developer overlay is switched by the panel.</summary>
        public event Action? DeveloperOverlayChanged;

        /// <summary>Raised when the Build-palette layout changes, whichever control changed it.</summary>
        public event Action<BuildPaletteLayout>? BuildPaletteLayoutChanged;

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

        /// <summary>
        /// A session row was confirmed, with the key of the row that was: Save, Load, Quit to main
        /// menu or Exit game. Raised rather than performed, like everything else here — writing a
        /// file and putting a colony down both need Unity, and "the panel asked" is a sentence the
        /// fast tier can assert without one.
        /// </summary>
        public event Action<string>? RowRequested;

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
            GraphicsOption.CutAwayCeiling => "ui.settings.cutaway",
            _ => "ui.settings.panel",
        };

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            // The armed exit row lives only in this panel, so the panel going away stands it
            // down. A "quit?" that survived its own panel would be a trap armed across the
            // whole screen.
            if (!open && ArmedRow != null)
            {
                ArmedRow = null;
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

        // ==================================================================== the number ladders

        /// <summary>Which rung a ladder rests on. Always a member of its own
        /// <see cref="RungsOf"/>, whatever was stored.</summary>
        public int Value(GraphicsLadder ladder) => _rung[ladder];

        /// <summary>
        /// Move a ladder, snapping to the nearest rung it actually offers.
        ///
        /// <para>Snapped rather than rejected, for the reason the camera-speed ladder gives: a
        /// value stored by an older build whose rungs were different must never leave the panel
        /// showing a number none of its own buttons can reproduce.</para>
        /// </summary>
        public void SetValue(GraphicsLadder ladder, int rung)
        {
            int snapped = Nearest(RungsOf(ladder), rung);
            if (_rung[ladder] == snapped) return;
            _rung[ladder] = snapped;
            _store?.WriteInt(KeyOf(ladder), snapped);
            LadderChanged?.Invoke(ladder);
        }

        /// <summary>
        /// Record where the machine already had a ladder, without raising anything and without
        /// writing it back — the same bargain every <c>Seed</c> here makes. A stored preference
        /// is laid over it by <see cref="UseStore"/>, so the machine beats the pipeline asset and
        /// the asset beats nothing at all.
        /// </summary>
        public void SeedValue(GraphicsLadder ladder, int rung) => _rung[ladder] = Nearest(RungsOf(ladder), rung);

        /// <summary>
        /// Whether the frame cap is doing anything.
        ///
        /// <para><b>It is not, whenever VSync is on.</b> Unity ignores
        /// <c>Application.targetFrameRate</c> while <c>vSyncCount</c> is above zero, so a player
        /// who sets 144 behind VSync gets 60 and no explanation. The rule is decided here, where
        /// the fast tier can hold it, and the panel greys the cap row and says <i>paced by
        /// VSync</i> rather than letting the screen tell a lie.</para>
        /// </summary>
        public bool FrameCapIsLive => Value(GraphicsLadder.VSync) == 0;

        // ======================================================================== the resolution

        /// <summary>One size the screen can be drawn at.</summary>
        public readonly struct Mode : IEquatable<Mode>
        {
            public Mode(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; }

            public int Height { get; }

            /// <summary>Pixels. How a stored size this machine no longer offers finds its
            /// nearest neighbour.</summary>
            public long Pixels => (long)Width * Height;

            public bool Equals(Mode other) => Width == other.Width && Height == other.Height;

            public override bool Equals(object? obj) => obj is Mode other && Equals(other);

            public override int GetHashCode() => (Width * 397) ^ Height;

            /// <summary>The form the preference is stored in, and the form the row reads.</summary>
            public override string ToString() => Width + "x" + Height;
        }

        /// <summary>
        /// The sizes this screen offers, largest first.
        ///
        /// <para><b>Empty until the presenter seeds it</b>, and that is the point: these rungs are
        /// the machine's, not ours, so they cannot be a table in an assembly that is compiled
        /// without UnityEngine. Everything about <em>choosing</em> one — de-duplication, ordering,
        /// the fallback when a stored size is gone — is decided here anyway, so the rule runs in
        /// the fast tier and only the reading of <c>Screen.resolutions</c> needs Unity.</para>
        /// </summary>
        public IReadOnlyList<Mode> Resolutions => _modes;

        /// <summary>The size the game is drawn at. <c>default</c> until something seeds it.</summary>
        public Mode Resolution { get; private set; }

        /// <summary>Raised when the resolution moves. Separate from
        /// <see cref="LadderChanged"/> because it carries a pair rather than a rung.</summary>
        public event Action? ResolutionChanged;

        /// <summary>
        /// Lay in the sizes this screen offers, de-duplicated by area and ordered largest first.
        ///
        /// <para>De-duplicated because <c>Screen.resolutions</c> returns one entry per refresh
        /// rate, so a monitor that does 60, 120 and 144 Hz reports 1920×1080 three times and the
        /// ladder would draw it three times. Refresh rate is the screen's business and not a
        /// thing this panel offers, so area is the whole of the identity.</para>
        /// </summary>
        public void SeedResolutions(IEnumerable<Mode> modes)
        {
            _modes.Clear();
            foreach (Mode mode in modes)
            {
                if (mode.Width <= 0 || mode.Height <= 0) continue;
                if (!_modes.Contains(mode)) _modes.Add(mode);
            }

            _modes.Sort((a, b) => b.Pixels.CompareTo(a.Pixels));
        }

        /// <summary>Record the size the window already is, without raising and without storing.</summary>
        public void SeedResolution(Mode mode) => Resolution = mode;

        /// <summary>
        /// Draw the game at this size.
        ///
        /// <para>A size this machine does not offer resolves to the <b>nearest by pixel
        /// count</b> rather than being dropped. A player who moves a save between a laptop and a
        /// desktop, or unplugs a second monitor, must not open the panel to find the row blank
        /// and the button dead — the commonest way this preference goes wrong is the one where
        /// nothing happens and nothing says why.</para>
        /// </summary>
        public void SetResolution(Mode mode)
        {
            Mode chosen = NearestMode(mode);
            if (chosen.Width <= 0) return;
            if (chosen.Equals(Resolution)) return;
            Resolution = chosen;
            _store?.WriteString(ResolutionKey, chosen.ToString());
            ResolutionChanged?.Invoke();
        }

        /// <summary>The offered size closest in area to the one asked for, or the one asked for
        /// when nothing has been seeded.</summary>
        public Mode NearestMode(Mode wanted)
        {
            if (_modes.Count == 0) return wanted;
            if (_modes.Contains(wanted)) return wanted;

            Mode best = _modes[0];
            foreach (Mode mode in _modes)
                if (Math.Abs(mode.Pixels - wanted.Pixels) < Math.Abs(best.Pixels - wanted.Pixels))
                    best = mode;
            return best;
        }

        /// <summary>Read a stored <c>"1920x1080"</c>. Anything else is no answer at all, which is
        /// not the same as a wrong one — an unreadable preference falls through to the window the
        /// game already opened at.</summary>
        public static Mode? ParseMode(string? text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int cross = text!.IndexOf('x');
            if (cross <= 0 || cross == text.Length - 1) return null;
            if (!int.TryParse(text.Substring(0, cross), out int width)) return null;
            if (!int.TryParse(text.Substring(cross + 1), out int height)) return null;
            if (width <= 0 || height <= 0) return null;
            return new Mode(width, height);
        }

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
        /// Choose a Build-palette layout, from either of the two controls that offer one, and
        /// write the choice down. Stored as the enum's ordinal, which is the one place this
        /// project stores an enum as a number rather than as its name — a layout is a position on
        /// a switcher of three, and <see cref="BuildPaletteModel"/> rejects an ordinal outside the
        /// set rather than trusting the file.
        /// </summary>
        public void SetBuildPaletteLayout(BuildPaletteLayout layout)
        {
            if (BuildPaletteLayout == layout) return;
            BuildPaletteLayout = layout;
            _store?.WriteInt(BuildLayoutKey, (int)layout);
            BuildPaletteLayoutChanged?.Invoke(layout);
        }

        /// <summary>
        /// Move one bus's volume. A fader holds a continuum, so anything between silence and
        /// the boost ceiling is taken rather than snapped — but whole dB only, because a thumb
        /// that rests between two decibels is a position the readout cannot say and the store
        /// cannot keep. Anything outside the span clamps to its end. Not written to
        /// <see cref="ISettingsStore"/>: the volumes keep their own store in the Presentation
        /// assembly (<c>AudioSettingsStore</c>, in dB, under its own prefix, since before this
        /// panel existed), and the presenter writes through to it so the two stores never
        /// hold one fader between them.
        ///
        /// <para>It was a seven-rung ladder until 2026-09-17, when the owner asked for
        /// sliders — a fader is dragged, and six of the seven rungs sat between −36 and 0,
        /// so most of a slider's travel would have been dead space snapping between rungs.
        /// Later the same day the owner asked for the default (unity) seated at the centre
        /// of the track, lowering to silence on one side and boosting to
        /// <see cref="BoostDb"/> on the other — the span and the seating that came of those
        /// two asks are what this method and <see cref="TrackOf"/> now describe.</para>
        /// </summary>
        public void SetBusDb(SettingsBus bus, int db)
        {
            int clamped = Math.Clamp(db, SilenceDb, BoostDb);
            if (_db[bus] == clamped) return;
            _db[bus] = clamped;
            BusDbChanged?.Invoke(bus);
        }

        /// <summary>One bus's volume, in dB. Always a whole dB between
        /// <see cref="SilenceDb"/> and <see cref="BoostDb"/>.</summary>
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
        public void RequestExit() => Request(ExitKey);

        /// <summary>
        /// A session row was pressed: Save, Load, Quit to main menu, or Exit game (U38).
        ///
        /// <para><b>Whether it asks twice is not decided here.</b> It is read from
        /// <see cref="SessionCommands"/>, the one table both this panel and the start screen build
        /// their rows from — which is the whole reason that table exists. Before U38 the exit row's
        /// two clicks were written into this method, and adding three more destructive rows with
        /// the same rule written again beside them would have been two answers to one question, in
        /// the file whose job is to have one.</para>
        ///
        /// <para>Pressing a different row stands down whatever was armed, so the second click
        /// always answers the row it landed on.</para>
        /// </summary>
        public void Request(string key)
        {
            if (key == null) return;

            if (SessionCommands.AsksTwice(key, SessionContext.InGame) && ArmedRow != key)
            {
                ArmedRow = key;
                ExitChanged?.Invoke();
                return;
            }

            ArmedRow = null;

            // Exit keeps its own event as well as the general one: the presenter that quits the
            // application has listened to it since before this panel had any other session row,
            // and "the panel asked the game to leave" is still the sentence the fast tier asserts.
            if (key == ExitKey) ExitRequested?.Invoke();
            RowRequested?.Invoke(key);
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
        /// Record one bus's volume as the audio store left it, without raising anything —
        /// clamped to the fader's span rather than snapped, because the fader can rest
        /// anywhere in it. The presenter lays <c>AudioSettingsStore.Load()</c> in through
        /// this, so the panel opens describing what the game is already playing at.
        /// </summary>
        public void SeedBusDb(SettingsBus bus, int db) => _db[bus] = Math.Clamp(db, SilenceDb, BoostDb);

        /// <summary>Record a Build-palette layout without raising anything, so the shell can lay
        /// in what it opened with.</summary>
        public void SeedBuildPaletteLayout(BuildPaletteLayout layout) => BuildPaletteLayout = layout;

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

            int? layout = store.ReadInt(BuildLayoutKey);
            if (layout.HasValue && BuildPaletteModel.IsLayout(layout.Value))
                SetBuildPaletteLayout((BuildPaletteLayout)layout.Value);

            foreach (GraphicsLadder ladder in LadderOrder)
            {
                int? rung = store.ReadInt(KeyOf(ladder));
                if (rung.HasValue) SetValue(ladder, rung.Value);
            }

            Mode? mode = ParseMode(store.ReadString(ResolutionKey));
            if (mode.HasValue) SetResolution(mode.Value);
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
