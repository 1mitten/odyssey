#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// The settings window's geometry, hues, icons and arrangement: everything about the frame
    /// that is not a setting (design 39).
    ///
    /// <para><b>One fixed frame, whatever tab is open.</b> The window used to size itself to the
    /// open tab and carry Save, Load and the two ways out under every one of them, so switching
    /// tab moved the box and every row in it. It is <see cref="Width"/> by <see cref="Height"/>
    /// now, centred by fixed offsets rather than by its own size, and the session actions live
    /// once in the rail. Nothing a tab holds may change the frame; a short tab leaves space.</para>
    ///
    /// <para><b>Unity-free</b>, so the fast tier can hold the arithmetic that matters: that the
    /// tallest tab, Keys, fits the frame with no scrolling (<see cref="ColumnHeight"/>), and that
    /// every icon parses.</para>
    /// </summary>
    public static class SettingsLayout
    {
        // ------------------------------------------------------------------ the frame

        public const int Width = 1240;
        public const int Height = 720;
        public const int HeaderHeight = 52;
        public const int HeaderPad = 16;
        public const int RailWidth = 240;
        public const int RailPadY = 10;
        public const int TabRowHeight = 40;
        public const int ActionRowHeight = 38;
        public const int RowPadX = 16;
        public const int RowIconGap = 12;
        public const int TabIconSize = 18;
        public const int ActionIconSize = 17;
        public const int TitleHeight = 72;
        public const int FooterHeight = 52;
        public const int ContentPadX = 24;
        public const int ContentPadY = 20;
        public const int ColumnGap = 40;
        public const int SectionGap = 22;
        public const int RowHeight = 38;
        public const int CloseButton = 30;

        /// <summary>A section heading's own box: the 11 px capitals on their line.</summary>
        public const int HeadingHeight = 18;

        /// <summary>Between a heading and its first row.</summary>
        public const int HeadingGap = 6;

        public const int SegmentHeight = 28;
        public const int SegmentPad = 11;
        public const int SwitchWidth = 38;
        public const int SwitchHeight = 22;
        public const int SwitchKnob = 14;
        public const int SwitchWordWidth = 26;
        public const int SliderWidth = 220;
        public const int SliderTrack = 4;
        public const int SliderThumbWidth = 10;
        public const int SliderThumbHeight = 18;
        public const int SliderValueWidth = 56;
        public const int SelectWidth = 190;
        public const int SelectHeight = 28;
        public const int KeyChipMinWidth = 36;
        public const int KeyChipHeight = 26;
        public const int KeyChipGap = 6;
        public const int ConfirmWidth = 440;

        /// <summary>How far the focus ring stands off the control it rings, and its weight.</summary>
        public const int FocusOffset = 2;
        public const int FocusWidth = 2;

        /// <summary>The height the columns get: the frame less its border, the header, the title
        /// band, the footer and the columns' own padding.</summary>
        public const int ColumnsHeight =
            Height - 2 * HudTheme.BorderWidth - HeaderHeight - TitleHeight - FooterHeight - 2 * ContentPadY;

        /// <summary>
        /// How tall one column stands, given the number of rows under each of its headings.
        /// The whole of the fit rule: a heading and its gap, the rows, and the gap between
        /// sections.
        /// </summary>
        public static int ColumnHeight(IReadOnlyList<int> rowsPerSection)
        {
            int total = 0;
            for (int s = 0; s < rowsPerSection.Count; s++)
            {
                if (s > 0) total += SectionGap;
                total += HeadingHeight + HeadingGap + rowsPerSection[s] * RowHeight;
            }
            return total;
        }

        // ------------------------------------------------------------------ the tabs

        /// <summary>The rail's order.</summary>
        public static readonly SettingsTab[] Tabs =
        {
            SettingsTab.Interface, SettingsTab.Graphics, SettingsTab.Audio, SettingsTab.Keys,
            SettingsTab.Gameplay,
        };

        /// <summary>Each tab's own hue: its rail icon, its selected row, its title tile and its
        /// section squares.</summary>
        public static HudColour Hue(SettingsTab tab) => tab switch
        {
            SettingsTab.Interface => new HudColour(0x8f, 0xb3, 0xd9),
            SettingsTab.Graphics => HudTheme.Violet,
            SettingsTab.Audio => HudTheme.Good,
            SettingsTab.Keys => HudTheme.Warn,
            SettingsTab.Gameplay => HudTheme.Accent,
            _ => HudTheme.Accent,
        };

        /// <summary>The selected rail row's fill, the hue at this share.</summary>
        public const float SelectedFill = 0.14f;

        /// <summary>The title tile's fill and border, the hue at these shares.</summary>
        public const float TileFill = 0.16f;
        public const float TileBorder = 0.50f;

        /// <summary>The line under the tab's name in the title band.</summary>
        public static string Subtitle(SettingsTab tab) => tab switch
        {
            SettingsTab.Interface => "Scale, camera and build palette",
            SettingsTab.Graphics => "Display, performance and detail",
            SettingsTab.Audio => "Volume by channel",
            SettingsTab.Keys => "Two bindings per action; the second is optional",
            SettingsTab.Gameplay => "Saving",
            _ => string.Empty,
        };

        /// <summary>The footer's reset button, in the tab's own word lower-cased: "Reset
        /// graphics to defaults".</summary>
        public static string ResetLabel(string tabLabel) =>
            "Reset " + tabLabel.ToLowerInvariant() + " to defaults";

        /// <summary>Said at the right of the footer on Keys, and nowhere else.</summary>
        public const string KeysHint = "Click a binding, then press a key. Backspace clears it.";

        /// <summary>The note on a resolution row that cannot be changed in the current mode.</summary>
        public const string FullscreenOnly = "Fullscreen only";

        /// <summary>The note on a row the editor cannot answer.</summary>
        public const string BuiltGameOnly = "Built game only";

        // ------------------------------------------------------------------ headings

        public const string ScaleGroupKey = "ui.settings.group.scale";
        public const string CameraGroupKey = "ui.settings.group.camera";
        public const string PaletteGroupKey = "ui.settings.group.palette";
        public const string PerformanceGroupKey = "ui.settings.group.performance";
        public const string VolumeGroupKey = "ui.settings.group.volume";
        public const string CuesGroupKey = "ui.settings.group.cues";
        public const string SavingGroupKey = "ui.settings.group.saving";
        public const string GameGroupKey = "ui.settings.group.game";
        public const string ViewGroupKey = "ui.settings.group.view";
        public const string ToolsGroupKey = "ui.settings.group.tools";
        public const string TimeGroupKey = "ui.settings.group.time";

        /// <summary>Every key this window names that <see cref="SettingsDirector.IconKeys"/> does
        /// not, so <c>RegistryTests</c> can hold it to the naming CSV.</summary>
        public static readonly string[] IconKeys =
        {
            ScaleGroupKey, CameraGroupKey, PaletteGroupKey, PerformanceGroupKey, VolumeGroupKey,
            CuesGroupKey, SavingGroupKey, GameGroupKey, ViewGroupKey, ToolsGroupKey, TimeGroupKey,
        };

        /// <summary>The Graphics ladders under Display, in order; the rest go under Performance.
        /// The resolution row sits second, after the mode it depends on.</summary>
        public static readonly GraphicsLadder[] DisplayLadders =
        {
            GraphicsLadder.DisplayMode, GraphicsLadder.VSync, GraphicsLadder.FrameCap,
        };

        public static readonly GraphicsLadder[] PerformanceLadders =
        {
            GraphicsLadder.RenderScale, GraphicsLadder.AntiAliasing, GraphicsLadder.ShadowDistance,
        };

        /// <summary>The Audio tab's two columns.</summary>
        public static readonly SettingsBus[] VolumeBuses =
            { SettingsBus.Master, SettingsBus.Music, SettingsBus.Ambience };

        public static readonly SettingsBus[] CueBuses = { SettingsBus.Effects, SettingsBus.Alerts };

        /// <summary>One heading of the Keys tab and the actions under it.</summary>
        public readonly struct KeyGroup
        {
            public KeyGroup(string headingKey, HotkeyAction[] actions)
            {
                HeadingKey = headingKey;
                Actions = actions;
            }

            public string HeadingKey { get; }

            public HotkeyAction[] Actions { get; }
        }

        /// <summary>
        /// The Keys tab: three columns, and the third is the tallest — it is what the frame's
        /// height is checked against.
        /// </summary>
        public static readonly KeyGroup[][] KeyColumns =
        {
            new[]
            {
                new KeyGroup(CameraGroupKey, new[]
                {
                    HotkeyAction.CameraForward, HotkeyAction.CameraBack,
                    HotkeyAction.CameraRight, HotkeyAction.CameraLeft,
                    HotkeyAction.CameraTurnLeft, HotkeyAction.CameraTurnRight,
                }),
            },
            new[]
            {
                new KeyGroup(ViewGroupKey, new[]
                {
                    HotkeyAction.SliceUp, HotkeyAction.SliceDown,
                    HotkeyAction.CycleAbove, HotkeyAction.FrameMap,
                }),
                new KeyGroup(ToolsGroupKey, new[]
                {
                    HotkeyAction.ToolMine, HotkeyAction.ToolFell, HotkeyAction.ToolCancel,
                    HotkeyAction.Draft,
                }),
            },
            new[]
            {
                new KeyGroup(TimeGroupKey, new[]
                {
                    HotkeyAction.Pause, HotkeyAction.Speed1, HotkeyAction.Speed2, HotkeyAction.Speed3,
                }),
                new KeyGroup(SettingsDirector.InterfaceKey, new[]
                {
                    HotkeyAction.BuildPalette, HotkeyAction.WorkTab, HotkeyAction.InventoryTab,
                    HotkeyAction.ResearchTab, HotkeyAction.AnimalsTab, HotkeyAction.DebugMenu,
                }),
            },
        };

        // ------------------------------------------------------------------ icons

        /// <summary>Every icon is line art on this grid, stroked at <see cref="IconStroke"/> with
        /// round caps and joins. None is a font glyph.</summary>
        public const float IconBox = 24f;

        public const float IconStroke = 1.8f;

        public static string IconOf(SettingsTab tab) => tab switch
        {
            SettingsTab.Interface => "M3 4h18v16H3zM3 9h18M9 9v11",
            SettingsTab.Graphics => "M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6",
            SettingsTab.Audio => "M4 9h4l5-4v14l-5-4H4zM16 9a4 4 0 0 1 0 6M18.5 6.5a8 8 0 0 1 0 11",
            SettingsTab.Keys => "M3 7h18v10H3zM6.5 10.5h1M10 10.5h1M13.5 10.5h1M17 10.5h.5M7 14h10",
            SettingsTab.Gameplay => "M5 21V4h12l-2 4 2 4H5",
            _ => CloseIcon,
        };

        /// <summary>The gear: the window's header and the title screen's Settings button, one
        /// source for both (design 40).</summary>
        public const string GearIcon =
            "M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7zM12 2v3M12 19v3M2 12h3M19 12h3" +
            "M4.9 4.9 7 7M17 17l2.1 2.1M4.9 19.1 7 17M17 7l2.1-2.1";

        public const string CloseIcon = "M6 6l12 12M18 6 6 18";

        public const string ResetIcon = "M4 12a8 8 0 1 0 2.3-5.7M4 4v5h5";

        /// <summary>The select's down triangle, filled rather than stroked.</summary>
        public const string SelectArrow = "M0 0h10L5 6z";

        /// <summary>One of the rail's game actions: its icon and which ink it is drawn in.</summary>
        public enum ActionTone
        {
            Good,
            Info,
            Warn,
            Bad,
        }

        public static string ActionIcon(string sessionKey) => sessionKey switch
        {
            SessionCommands.SaveKey => "M5 4h11l3 3v13H5zM8 4v5h7V4M8 20v-6h8v6",
            SessionCommands.SaveAsKey => "M5 4h9l3 3v4M5 4v16h7M8 4v5h6V4M17.5 14v7M14 17.5h7",
            SessionCommands.LoadKey => "M3 6h6l2 2h10v11H3zM12 11v5M9.5 13.5 12 11l2.5 2.5",
            SessionCommands.QuitToMenuKey => "M13 4H5v16h8M10 12h10M16 8l4 4-4 4",
            SessionCommands.QuitKey => "M12 3v8M7 6.3a7 7 0 1 0 10 0",
            _ => CloseIcon,
        };

        public static ActionTone ToneOf(string sessionKey) => sessionKey switch
        {
            SessionCommands.LoadKey => ActionTone.Info,
            SessionCommands.QuitToMenuKey => ActionTone.Warn,
            SessionCommands.QuitKey => ActionTone.Bad,
            _ => ActionTone.Good,
        };

        public static HudColour Ink(ActionTone tone) => tone switch
        {
            ActionTone.Info => HudTheme.Info,
            ActionTone.Warn => HudTheme.Warn,
            ActionTone.Bad => HudTheme.Bad,
            _ => HudTheme.Good,
        };

        /// <summary>Whether an action's label takes its ink too. The two ways out do; saving and
        /// loading keep the reading ink.</summary>
        public static bool LabelTakesInk(string sessionKey) =>
            sessionKey == SessionCommands.QuitToMenuKey || sessionKey == SessionCommands.QuitKey;

        /// <summary>Whether a rule goes above this action: between the save group and the ways
        /// out.</summary>
        public static bool RuleBefore(string sessionKey) => sessionKey == SessionCommands.QuitToMenuKey;
    }
}
