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
    }

    /// <summary>
    /// The sections of the settings panel.
    ///
    /// <para>Two, because there are now two kinds of thing in it and they answer different
    /// questions: <see cref="Interface"/> is how the HUD itself is drawn, <see cref="Graphics"/>
    /// is how the world is. The panel held only the second until 2026-09-16, when the owner
    /// reported the HUD's type reading too small on a 4K monitor and the fix was a setting rather
    /// than a constant.</para>
    /// </summary>
    public enum SettingsTab
    {
        Interface,
        Graphics,
    }

    /// <summary>
    /// The settings panel: whether it is open, and the graphics options it holds.
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
            "ui.settings.shadows",
            "ui.settings.surround",
            "ui.settings.grass",
            "ui.settings.relief",
            "ui.settings.seethrough",
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
        /// on a 16:9 screen the HUD occupies about a quarter of it rather than an
        /// ninth.</para>
        /// </summary>
        public static readonly int[] UiScales = { 80, 90, 100, 110, 125, 150 };

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

        ISettingsStore? _store;

        public SettingsDirector()
        {
            foreach (GraphicsOption option in Order) _on[option] = true;
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

        /// <summary>Raised when the panel opens or closes.</summary>
        public event Action? Changed;

        /// <summary>Raised when the showing section changes.</summary>
        public event Action<SettingsTab>? TabChanged;

        /// <summary>Raised when the interface scale changes, with the new percentage.</summary>
        public event Action<int>? UiScaleChanged;

        /// <summary>Raised when one option's value changes, with the option that changed.</summary>
        public event Action<GraphicsOption>? OptionChanged;

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
            int snapped = Nearest(percent);
            if (UiScale == snapped) return;
            UiScale = snapped;
            _store?.WriteInt(UiScaleKey, snapped);
            UiScaleChanged?.Invoke(snapped);
        }

        /// <summary>Move one rung up or down the ladder; at either end, stay there.</summary>
        public void StepUiScale(int rungs)
        {
            int at = Array.IndexOf(UiScales, UiScale);
            if (at < 0) at = Array.IndexOf(UiScales, Nearest(UiScale));
            SetUiScale(UiScales[Math.Max(0, Math.Min(UiScales.Length - 1, at + rungs))]);
        }

        static int Nearest(int percent)
        {
            int best = UiScales[0];
            foreach (int rung in UiScales)
                if (Math.Abs(rung - percent) < Math.Abs(best - percent)) best = rung;
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
        public void SeedUiScale(int percent) => UiScale = Nearest(percent);

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
