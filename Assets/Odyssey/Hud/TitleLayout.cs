#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// The title screen's dock, logo and four buttons (design 40): everything about the screen
    /// that is not the flow behind it.
    ///
    /// <para><b>A dock rather than a card.</b> The title screen was a 420 x 384 card in the middle
    /// of the starfield carrying the word ODYSSEY and four rows. It is a full-height panel,
    /// <see cref="DockWidth"/> wide and flush left at every resolution, with the Strata mark and
    /// the wordmark at the top, four buttons under a rule and the build line at the foot; the
    /// starfield shows to the right of it.</para>
    ///
    /// <para><b>The buttons are the Settings rail's</b>: Load and Exit game draw the rail's own
    /// paths and inks, and Settings draws the window's gear. One source for each, so the title
    /// screen and the window cannot drift.</para>
    /// </summary>
    public static class TitleLayout
    {
        public const int DockWidth = 560;
        public const int DockPadX = 48;

        /// <summary>The width a line of the dock can use.</summary>
        public const int ContentWidth = DockWidth - 2 * DockPadX;

        /// <summary>Where the logo starts, as a share of the screen's height: 200 px at 1080.</summary>
        public const float LogoTopShare = 0.185f;

        /// <summary>And never nearer the top than this.</summary>
        public const int LogoTopMin = 96;

        public const int MarkWidth = 70;
        public const int MarkHeight = 60;
        public const int MarkGap = 22;

        /// <summary>The wordmark: Archivo Narrow 600 at 64 px, tracked .3em.</summary>
        public const int WordmarkSize = 64;
        public const float WordmarkTracking = 0.30f;

        /// <summary>The tracking to fall back to if the tracked word will not fit beside the mark
        /// in <see cref="ContentWidth"/>. The size is never what gives.</summary>
        public const float WordmarkTrackingTight = 0.26f;

        public const int DividerAbove = 40;
        public const int DividerBelow = 28;
        public const int ButtonHeight = 64;
        public const int ButtonGap = 6;
        public const int ButtonPadX = 14;
        public const int ButtonIcon = 22;
        public const int IconTextGap = 16;

        /// <summary>A button's name to its description.</summary>
        public const int DescriptionGap = 6;

        public const int FooterBottom = 28;

        /// <summary>A button's fill on hover and focus, and when pressed, as a share of its colour.
        /// The first is the Settings rail's selected row.</summary>
        public const float HoverFill = SettingsLayout.SelectedFill;
        public const float PressedFill = 0.22f;

        /// <summary>The coloured edge a lit button shows on its left.</summary>
        public const int LitEdge = 3;

        /// <summary>Where the logo starts on a screen of this height.</summary>
        public static int LogoTop(float screenHeight) =>
            Math.Max(LogoTopMin, (int)Math.Round(screenHeight * LogoTopShare));

        /// <summary>The build line at the foot of the dock.</summary>
        public static string VersionLine(string version) => "prototype " + version;

        /// <summary>The resolution at the foot of the dock: "1920 x 1080".</summary>
        public static string ResolutionLine(int width, int height) => width + " x " + height;

        // ------------------------------------------------------------------ the buttons

        /// <summary>One of the four: which row of the session table it presses, its colour, whether
        /// its name takes the colour at rest, its line of description and its icon.</summary>
        public readonly struct Button
        {
            public Button(string key, HudColour ink, bool nameTakesInk, string description, string icon)
            {
                Key = key;
                Ink = ink;
                NameTakesInk = nameTakesInk;
                Description = description;
                Icon = icon;
            }

            public string Key { get; }
            public HudColour Ink { get; }
            public bool NameTakesInk { get; }
            public string Description { get; }
            public string Icon { get; }
        }

        /// <summary>New game's four-pointed star. The one icon here the Settings rail has no
        /// copy of.</summary>
        public const string NewGameIcon = "M12 3l2.4 6.6L21 12l-6.6 2.4L12 21l-2.4-6.6L3 12l6.6-2.4z";

        /// <summary>The four, top to bottom, which is the order the session table draws them in
        /// on the main screen.</summary>
        public static readonly Button[] Buttons =
        {
            new Button(SessionCommands.NewGameKey, HudTheme.Good, false,
                "Pick a site and a crew", NewGameIcon),
            new Button(SessionCommands.LoadKey, SettingsLayout.Ink(SettingsLayout.ToneOf(SessionCommands.LoadKey)), false,
                "Continue a saved colony", SettingsLayout.ActionIcon(SessionCommands.LoadKey)),
            new Button(SessionCommands.OptionsKey, HudTheme.Violet, false,
                "Interface, graphics, audio, keys", SettingsLayout.GearIcon),
            new Button(SessionCommands.QuitKey, SettingsLayout.Ink(SettingsLayout.ToneOf(SessionCommands.QuitKey)), true,
                "Close Odyssey", SettingsLayout.ActionIcon(SessionCommands.QuitKey)),
        };

        // ------------------------------------------------------------------ the Strata mark

        /// <summary>One slab of the mark, in its own 112 x 96 box.</summary>
        public readonly struct Slab
        {
            public Slab(int x, int y, int width, int height, HudColour colour)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Colour = colour;
            }

            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }
            public HudColour Colour { get; }
        }

        public const int MarkBoxWidth = 112;
        public const int MarkBoxHeight = 96;

        /// <summary>Five slabs, the middle one lit: the layers of the board, and the one being
        /// looked at.</summary>
        public static readonly Slab[] Mark =
        {
            new Slab(8, 6, 96, 10, HudTheme.TextPrimary),
            new Slab(20, 24, 72, 10, HudTheme.TextPrimary.WithAlpha(0.72f)),
            new Slab(4, 42, 104, 12, HudTheme.Accent),
            new Slab(28, 62, 56, 10, HudTheme.TextPrimary.WithAlpha(0.72f)),
            new Slab(16, 80, 80, 10, HudTheme.TextPrimary.WithAlpha(0.45f)),
        };
    }
}
