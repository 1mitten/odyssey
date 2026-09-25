#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// A colour, as the HUD spells one: eight-bit channels and an alpha, with the exact CSS-style
    /// text the stylesheet carries.
    ///
    /// <para>A value rather than <c>UnityEngine.Color</c> because this assembly is compiled
    /// without UnityEngine (ADR 0003) and the whole point of <see cref="HudTheme"/> is that the
    /// tokens live where the fast tier can read them. The Presentation side converts once.</para>
    /// </summary>
    public readonly struct HudColour
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        /// <summary>Opacity, 0 to 1.</summary>
        public readonly float A;

        public HudColour(byte r, byte g, byte b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>
        /// <c>#rrggbb</c> or <c>#rrggbbaa</c>, the form USS takes and the form
        /// <see cref="HudStyleSheetTests"/> matches against the authored sheet. Alpha is rounded to
        /// the nearest byte, which is the resolution USS has.
        /// </summary>
        public string Hex
        {
            get
            {
                int alpha = (int)(A * 255f + 0.5f);
                return alpha >= 255
                    ? $"#{R:x2}{G:x2}{B:x2}"
                    : $"#{R:x2}{G:x2}{B:x2}{alpha:x2}";
            }
        }

        public HudColour WithAlpha(float a) => new HudColour(R, G, B, a);

        public override string ToString() => Hex;
    }

    /// <summary>
    /// What a thing on screen <i>is</i>, for the purpose of colouring its icon stroke.
    ///
    /// <para>The rule from the interface spec is narrow on purpose: <b>category colour lives in
    /// the icon stroke, never in a filled background, and only in stores and the command
    /// bar.</b> Everywhere else an icon inherits the colour of the text beside it. A category is
    /// therefore not decoration hung on every key — it is a small, closed set, and
    /// <see cref="HudTheme.CategoryOf"/> is total so that no key can quietly fall through to a
    /// colour nobody chose.</para>
    ///
    /// <para>Every colour a category resolves to is already a palette token. Nothing on this
    /// screen may introduce a hue of its own: eight categories sharing five tokens is the point,
    /// because a screen with a colour per commodity is a screen with no colour code at all.</para>
    /// </summary>
    public enum HudCategory
    {
        /// <summary>No category: the icon takes the colour of the text beside it.</summary>
        Neutral,

        /// <summary>Food and anything eaten.</summary>
        Sustenance,

        /// <summary>Wood, cloth, anything grown or cut.</summary>
        Organic,

        /// <summary>Scrap, alloy, anything smelted or salvaged.</summary>
        Metal,

        /// <summary>Stone, concrete, anything quarried.</summary>
        Mineral,

        /// <summary>Water and other fluids.</summary>
        Fluid,

        /// <summary>Medicine and anything that treats a body.</summary>
        Medical,

        /// <summary>Placing, digging, ordering: the things the player does to the world.</summary>
        Work,

        /// <summary>People, and the panels about people.</summary>
        People,

        /// <summary>Records, ledgers, factions: the panels about information.</summary>
        Record,
    }

    /// <summary>
    /// The HUD's design tokens, in one place, in the assembly the fast tier can read.
    ///
    /// <para><b>Why this exists.</b> Before this pass every colour and every offset in the
    /// interface was a literal, written once in <c>Hud.uss</c> and again in C# wherever an inline
    /// style needed it, and the stylesheet's own header argued for that: "colours are hardcoded
    /// per class rather than var(--x): the sheet stays legible as plain text". That reasoning
    /// holds for the sheet and fails for the screen, because the acceptance criteria this pass was
    /// given — no two panels overlap, total coverage under eighteen per cent, body text over
    /// 4.5:1 — are statements about numbers, and a number that exists in two files is a number
    /// nobody can test.</para>
    ///
    /// <para>So the tokens live here, the stylesheet is still authored by hand and still legible
    /// as plain text, and <c>HudStyleSheetTests</c> parses the sheet's token block and fails the
    /// fast tier the moment the two disagree. The sheet keeps its literals; it just cannot keep a
    /// literal that nobody agreed to.</para>
    ///
    /// <para><b>Nothing here is simulation.</b> No token is a cell, a save key or a hash bit, and
    /// nothing in <c>Odyssey.Sim</c> can see this assembly at all.</para>
    /// </summary>
    public static class HudTheme
    {
        // ------------------------------------------------------------------ surfaces

        /// <summary>Panel fill. Translucent over the world and blurred behind, so a panel reads as
        /// glass laid on the board rather than as a hole cut in it.</summary>
        public static readonly HudColour PanelFill = new HudColour(12, 16, 20, 1f);

        /// <summary>The command bar's fill: the same colour, a little more opaque, because the
        /// bar is always on screen and always carries text.</summary>
        public static readonly HudColour BarFill = new HudColour(12, 16, 20, 0.90f);

        /// <summary>
        /// The fill of a panel raised from the command bar (owner, 2026-09-17: "there should be
        /// less transparency with these menus that appear from this bar").
        ///
        /// <para><b>Why these and not every panel.</b> A board panel — stores, the clock, the
        /// inspect pane — is something you read *while* watching the world, and the two scrims
        /// carry its text contrast so the panel itself can stay light enough to see terrain
        /// through. A popover is something you have deliberately opened and are looking *at*; the
        /// board behind it is not being read, and showing it through a list of rows is noise on
        /// the one surface the player is attending to. It is also the surface furthest from a
        /// scrim's strongest point, since it stands a panel's height above the bottom edge.</para>
        /// </summary>
        public static readonly HudColour PopoverFill = new HudColour(12, 16, 20, 0.96f);

        public static readonly HudColour PanelBorder = new HudColour(255, 255, 255, 0.13f);
        public static readonly HudColour Divider = new HudColour(255, 255, 255, 0.09f);

        /// <summary>The blur behind a panel, in pixels.</summary>
        public const int PanelBlur = 6;

        // ------------------------------------------------------------------ ink

        public static readonly HudColour TextPrimary = new HudColour(0xee, 0xf3, 0xf6);
        public static readonly HudColour TextMeta = new HudColour(255, 255, 255, 0.66f);
        public static readonly HudColour TextDim = new HudColour(255, 255, 255, 0.50f);
        public static readonly HudColour TextFaint = new HudColour(255, 255, 255, 0.35f);

        // ------------------------------------------------------------------ signal

        public static readonly HudColour Accent = new HudColour(0x6f, 0xd3, 0xe3);

        /// <summary>The ink laid on top of <see cref="Accent"/> when it is used as a fill.</summary>
        public static readonly HudColour OnAccent = new HudColour(0x0b, 0x11, 0x16);

        public static readonly HudColour Warn = new HudColour(0xe8, 0xb5, 0x5c);
        public static readonly HudColour Bad = new HudColour(0xe0, 0x6a, 0x5c);
        public static readonly HudColour Good = new HudColour(0x7f, 0xc9, 0x8c);
        public static readonly HudColour Info = new HudColour(0x8f, 0xd0, 0xe3);

        /// <summary>The settings window's Graphics hue and the title screen's Settings button
        /// (designs 39 and 40): one colour for the one destination.</summary>
        public static readonly HudColour Violet = new HudColour(0xb9, 0xa8, 0xe0);

        /// <summary>The title screen's dock (design 40): the panel fill, translucent, so the
        /// starfield carries on faintly behind it rather than stopping at a wall (owner,
        /// 2026-09-24: "it looks solid").</summary>
        public static readonly HudColour DockFill = new HudColour(12, 16, 20, 0.72f);

        // ------------------------------------------------------------------ controls (design 39)

        /// <summary>The border of a control that can be pressed: a segment, a select, a key chip,
        /// a switch that is off. A step brighter than <see cref="PanelBorder"/>, so a control
        /// reads as a thing to press rather than as a rule.</summary>
        public static readonly HudColour ControlBorder = new HudColour(255, 255, 255, 0.26f);

        /// <summary>The rule under a settings row: quieter than a divider, because there are
        /// many of them and each only says where one row ends.</summary>
        public static readonly HudColour RowRule = new HudColour(255, 255, 255, 0.07f);

        /// <summary>The track of a switch that is off.</summary>
        public static readonly HudColour SwitchOffTrack = new HudColour(255, 255, 255, 0.10f);

        /// <summary>The dashed outline of a key slot with nothing bound to it.</summary>
        public static readonly HudColour EmptySlot = new HudColour(255, 255, 255, 0.22f);

        /// <summary>The wash behind the settings window. Lighter than <see cref="ModalScrim"/>,
        /// because the window's graphics levers are pulled while watching the board.</summary>
        public static readonly HudColour SettingsScrim = new HudColour(6, 10, 12, 0.58f);

        // ------------------------------------------------------------------ quality

        /// <summary>
        /// The colour a quality tier is named in, anywhere in the interface it is named (owner,
        /// 2026-09-17: <i>"Poor (red), Normal (no change), Decent (A light yellow), Uber (Teal)
        /// and Epic (Purple). Anywhere quality is mentioned there should be centralised colours"</i>).
        ///
        /// <para><b>Null is Normal, and it means "leave it alone".</b> That is the owner's own
        /// specification and it is not the same as returning the body colour: a tier named in a
        /// tooltip, a card or a log line has whatever colour that surface gives it, and Normal
        /// must not override any of them. A caller that wants a colour to paint with should skip
        /// the paint when this is null rather than substitute one.</para>
        ///
        /// <para>Poor reuses <see cref="Bad"/> and nothing else is reused: a tier is a judgement
        /// about a thing, not an alarm, and three more signal colours in the palette would make
        /// every one of them mean less. The three new ones are chosen to clear
        /// <c>HudContrast.BodyMinimum</c> on the darkest panel the game draws and to stay apart
        /// from each other for the commonest colour blindness — which is why Decent is a yellow
        /// rather than the green a "good" tier would otherwise want, with Poor's red beside it.</para>
        /// </summary>
        public static HudColour? Quality(int tier) => tier switch
        {
            1 => Bad,                              // Poor
            3 => new HudColour(0xe9, 0xe0, 0x8c),  // Decent: a light yellow
            4 => new HudColour(0x45, 0xc7, 0xb0),  // Uber: teal
            5 => new HudColour(0xb9, 0x8c, 0xe8),  // Epic: purple
            _ => null,                             // Normal, and tier 0 which is never shown
        };

        /// <summary>
        /// The colour a temperature is named in, anywhere the interface names one (design 28
        /// §8): null is comfortable and means "leave it alone", exactly as <see cref="Quality"/>'s
        /// null is Normal. Cold reuses <see cref="Info"/>'s blue and heat <see cref="Warn"/>'s
        /// orange, sweltering <see cref="Bad"/>'s red — the same reuse discipline the quality
        /// tiers follow, and for the same reason: a reading is not an alarm until it is one.
        ///
        /// <para>The thresholds are the interface's own approximations of the content's bands
        /// (the Hud cannot read the simulation's tuning), restated rather than shared — the two
        /// disagreeing would be a display quibble, not a rules bug, and the day that distinction
        /// stops holding they belong in the registry beside the labels.</para>
        /// </summary>
        public static HudColour? Temperature(int centiC) => centiC switch
        {
            > 3_500 => Bad,                               // sweltering
            > 3_000 => Warn,                              // hot
            < 1_000 => Info,                              // cold, however deep
            _ => null,                                    // comfortable, and the work band
        };

        /// <summary>The tint laid over a stores row whose stock is falling.</summary>
        public static readonly HudColour FallingRow = Warn.WithAlpha(0.09f);

        /// <summary>The fill behind the active tab of the inspect pane.</summary>
        public static readonly HudColour ActiveTabFill = Accent.WithAlpha(0.12f);

        /// <summary>The ring drawn around the selected colonist's card, outside its border.</summary>
        public static readonly HudColour SelectedRing = Accent.WithAlpha(0.35f);

        // ------------------------------------------------------------------ scrims

        /// <summary>
        /// The colour both scrims fade from. They are always on, and they are the reason the
        /// panels can stay small: text contrast is carried by the scrim rather than by an opaque
        /// panel behind every word (see <see cref="HudContrast"/>).
        /// </summary>
        public static readonly HudColour ScrimInk = new HudColour(8, 11, 14);

        public const float TopScrimAlpha = 0.72f;
        public const float BottomScrimAlpha = 0.78f;
        /// <summary>
        /// How far the top scrim reaches down the screen.
        ///
        /// <para><b>170 until 2026-09-18</b>, when the avatar doubled and took a two-row strip from
        /// 133 px to 185. A scrim that stops short of the strip is a roster card's text standing
        /// on bare world at the bottom edge, which is the one thing this gradient exists to
        /// prevent — and <c>TheScrimsAreTheHeightsTheThemeDeclares</c> is what caught it, rather
        /// than somebody noticing a hard-to-read name on a bright meadow.</para>
        /// </summary>
        public const int TopScrimHeight = 192;
        public const int BottomScrimHeight = 200;

        /// <summary>
        /// The wash a modal lays over the whole viewport.
        ///
        /// <para><b>The third scrim, and the only one that is not always on.</b> The two above are
        /// ramps at the top and bottom of the screen carrying the HUD's text contrast; this one is
        /// flat, covers everything, and says that what is behind it is not available. It is the
        /// same ink, so the interface has one darkness rather than two.</para>
        ///
        /// <para>Two thirds rather than opaque, because a modal that blacks the screen out is a
        /// scene change and this is not one: the start screen still wants to read as the game with
        /// something in front of it. It is also what makes <c>Modal()</c> usable later over a live
        /// colony, which the start screen never has behind it.</para>
        /// </summary>
        public static readonly HudColour ModalScrim = ScrimInk.WithAlpha(0.66f);

        // ------------------------------------------------------------------ geometry

        public const int PanelRadius = 5;
        /// <summary>
        /// The command bar's corner radius: none. It runs the full width of the screen with three
        /// of its four sides off it (owner, 2026-09-17), and a rounded corner against a screen
        /// edge shows a notch of world through it, which is what a floating panel looks like.
        /// </summary>
        public const int BarRadius = 0;
        public const int ControlRadius = 4;
        public const int ChipRadius = 3;
        public const int BorderWidth = 1;

        /// <summary>
        /// The armed banner's border, which is not a hairline (owner, 2026-09-17: <i>"make that
        /// border much thicker"</i>).
        ///
        /// <para>It is three pixels because the border is the whole of what that banner says at a
        /// glance: it is drawn in the held order's own hue — green for chopping, blue for mining,
        /// amber for deconstructing, red for cancelling — and at one pixel a colour is a detail
        /// rather than a signal. The panel's top edge wears the same idea at two pixels while the
        /// palette is open (<c>docs/design/17-build-palette-layouts.md</c> §7), and this one is
        /// over the board with nothing else around it.</para>
        /// </summary>
        public const int ArmedBorderWidth = 3;

        /// <summary>The width of the neutral square drawn where a real glyph does not exist yet.</summary>
        public const float PlaceholderStroke = 1.6f;

        // ------------------------------------------------------------------ avatars

        /// <summary>
        /// The hull a flat avatar's figure is drawn on (<c>docs/design/20-avatars.md</c>).
        ///
        /// <para><b>It is a contrast guarantee, not a style.</b> The tile behind the figure is that
        /// colonist's own garment colour and the head is their own skin, and the palettes are 14
        /// garments against 7 skins — so some pair of them is close, and on that one colonist the
        /// head would dissolve into the background. Drawing the silhouette once in ink underneath
        /// the colours means no pair can ever do that, without this file inventing a rule about
        /// which colours may sit together.</para>
        ///
        /// <para>The same ink the scrims use, because it is already the game's answer to "darker
        /// than everything". The world inks its own figures for the same reason at a different
        /// scale (<c>ColonistMaterials.AdoptInkFrom</c>).</para>
        /// </summary>
        public static readonly HudColour AvatarInk = ScrimInk.WithAlpha(0.55f);

        /// <summary>
        /// The frame around an avatar (owner, 2026-09-18: *"put a white border around the
        /// portraits"*).
        ///
        /// <para><b>Not quite white.</b> 0.88 alpha rather than 1.0, because a rendered portrait
        /// is a photograph with soft edges and a hard pure-white rectangle around it reads as a
        /// cut-out pasted on the card. At this alpha it frames without outshouting the face, and
        /// it is still the brightest thing on the card by some way — brighter than
        /// <see cref="TextPrimary"/>, which is the name beside it.</para>
        /// </summary>
        public static readonly HudColour AvatarBorder = new HudColour(255, 255, 255, 0.88f);

        /// <summary>
        /// How thick that frame is. Two rather than one: the avatar is 52 px on a card and 60 in
        /// the inspect header, and a single pixel at that size reads as an artefact of the
        /// rendering rather than as a deliberate edge — which is the same reason the armed
        /// banner's border is three (<see cref="ArmedBorderWidth"/>) and not one.
        /// </summary>
        public const int AvatarBorderWidth = 2;

        /// <summary>
        /// How far the ink hull stands out past the figure, in the 24-unit design box — so it is
        /// 0.76 px on a 26 px card avatar and 1.87 px on the 64 px one, which keeps a separation
        /// that reads at the small size from becoming an outline that draws itself at the large.
        /// </summary>
        public const float AvatarInkWidth = 0.7f;

        // ------------------------------------------------------------------ build palette

        /// <summary>
        /// A tier of the Build palette, drawn as one hue and the four states derived from it: the
        /// resting fill, the resting border, the ink, and the wash a selected one takes.
        ///
        /// <para><b>Why a struct rather than four loose tokens per category.</b> The palette has
        /// three layouts and seven categories, so a category drawn from loose tokens is twenty-one
        /// chances for one layout to reach for the wrong one. A tier is asked for its colours as a
        /// set, and every colour in the set is a function of one hue — which is also the rule the
        /// specification states in words: <i>selection is a stronger wash of the category's own
        /// colour</i>, never a global cyan on this tier. Derived rather than authored means the
        /// stronger wash cannot drift away from the hue it is supposed to be a wash of.</para>
        /// </summary>
        public readonly struct BuildTier
        {
            /// <summary>The one colour every other member here is a function of.</summary>
            public readonly HudColour Hue;

            public BuildTier(HudColour hue) => Hue = hue;

            /// <summary>Resting fill: the hue at 9%.</summary>
            public HudColour Fill => Hue.WithAlpha(0.09f);

            /// <summary>Resting border: the hue at 38%.</summary>
            public HudColour Border => Hue.WithAlpha(0.38f);

            /// <summary>Resting ink, for the label and the icon stroke alike: the hue at 92%.</summary>
            public HudColour Ink => Hue.WithAlpha(0.92f);

            /// <summary>Hover: the resting fill, lifted.</summary>
            public HudColour HoverFill => Hue.WithAlpha(0.15f);

            /// <summary>Selected fill: the same hue again, at 22%.</summary>
            public HudColour SelectedFill => Hue.WithAlpha(0.22f);

            /// <summary>Selected border and ink: the solid hue.</summary>
            public HudColour Selected => Hue;
        }

        /// <summary>
        /// The Zones category's olive, named because two things wear it: the category tier below,
        /// and the growing-zone order wherever the mode colour is asked for
        /// (<see cref="PinnedActionHue"/>). Mode and category agreeing is the point — the strip
        /// button, the armed banner and the palette tile all saying the tool's one colour — and
        /// two places asking for it is exactly why it is not written twice.
        /// </summary>
        /// <remarks>
        /// <para><b>Declared above the array that uses it, and that is load-bearing.</b> Static
        /// field initialisers run in declaration order, so a hue declared below
        /// <see cref="BuildCategoryTiers"/> is silently <c>default(HudColour)</c> — black at
        /// alpha 0 — by the time the array is built. The fast tier's contrast test caught exactly
        /// that on the day this landed: "Zones, at rest" measuring 1.16:1.</para>
        /// </remarks>
        // Earthy brown since 2026-09-18 (owner: the green was hard to see on the surface) —
        // worked soil, matching the tint the drawn field wears. Was olive 0xa8c06a.
        public static readonly HudColour ZonesHue = new HudColour(0xc3, 0x98, 0x5c);

        /// <summary>
        /// The five storage rungs, low to high — <b>cool to warm as urgency rises</b>, so the
        /// ladder reads without reading the words (design brief, 2026-09-21).
        ///
        /// <para>This is the second place in the HUD allowed a hue per row, and it earns it on the
        /// same terms <see cref="BuildCategoryTiers"/> does: a closed set, always drawn together,
        /// always in the same order, where the colour separates adjacent things rather than asking
        /// to be recognised out of context. The acceptance criterion is the same one — each is
        /// distinct with the labels masked.</para>
        ///
        /// <para><b>Normal is the accent cyan on purpose.</b> It is the rung a zone is founded at
        /// and the one most zones stay on, so the commonest state wears the colour the interface
        /// already means "ordinary, current" by.</para>
        /// </summary>
        public static readonly HudColour[] StoragePriorityHues =
        {
            // **The cool end of the brief's ladder did not survive its own acceptance criteria**,
            // and the two faults pull in opposite directions, which is why both numbers moved.
            //
            // Grey #6b737a measured 2.71:1 against the panel where the floor is 4.5, and slate
            // #7f9ab0 measured 4.45 — near enough to pass by eye and not near enough to pass. The
            // hue is a *label* colour when a rung is selected, so both had to come up. But
            // lightening grey towards slate then put the two within 41 channel-points of each
            // other, under the 60 the order hues are held to, and they are adjacent rows.
            //
            // So grey goes the other way: a pale neutral, far from slate, and reading as inactive
            // — which is what the bottom rung means. It is the only rung with no colour in it at
            // all, so it is told apart by that as much as by its value.
            new HudColour(0xc6, 0xc9, 0xcb), // Last — pale neutral
            new HudColour(0x8c, 0xa6, 0xbb), // Low — slate
            new HudColour(0x7f, 0xd0, 0xe0), // Normal — cyan
            new HudColour(0x7f, 0xc0, 0x7a), // Preferred — green
            new HudColour(0xe0, 0xa4, 0x5c), // Urgent — amber
        };

        /// <summary>
        /// The six item categories, in registry order.
        ///
        /// <para>The category row is tinted with its hue at a tenth and its label set in the hue,
        /// so the six blocks stay findable while a player scrolls sixty rows. <b>Commodity rows
        /// stay untinted</b>: the colour marks the group, not every line, which is the difference
        /// between a coded list and a striped one.</para>
        ///
        /// <para>Materials' tan and Urgent's amber are the two the brief singles out for contrast,
        /// and <c>StorageThemeTests</c> holds every one of the eleven to
        /// <see cref="HudContrast.BodyMinimum"/> over the panel rather than taking the brief's word
        /// for it.</para>
        ///
        /// <para><b>Re-tuned for colour-blind players on 2026-09-23</b> (owner: "check this for
        /// accessibility"). Every contrast was fine — 6.75:1 and up — but under deuteranopia,
        /// about one man in twenty, Food, Weapons and Materials simulated to within 2 to 3 CIE Lab
        /// units of one another: one colour. Each family is kept (green, pink, tan, violet, blue,
        /// rust) and moved at most 12 units, spreading mostly by lightness, so every pair is now at
        /// least 15 apart under normal vision, protanopia, deuteranopia and tritanopia alike, and
        /// still 60 channel-points apart as before. <b>Colour is the second cue, never the only
        /// one</b>: every category also carries its own drawn glyph and its name.
        /// <c>StorageThemeTests.NoTwoCategoriesLookAlikeToAColourBlindPlayer</c> holds it.</para>
        /// </summary>
        public static readonly HudColour[] ItemCategoryHues =
        {
            new HudColour(0x93, 0xd1, 0x7e), // Food — lighter, so deuteranopia cannot fold it into Materials
            new HudColour(0xf0, 0x86, 0xa8), // Medicine
            new HudColour(0xc7, 0xa5, 0x4f), // Materials
            new HudColour(0xba, 0x99, 0xf5), // Books
            new HudColour(0x75, 0xa3, 0xcb), // Items — deeper, away from Books under protanopia
            new HudColour(0xc1, 0x73, 0x49), // Weapons — a rust darker than the tan beside it
        };

        /// <summary>
        /// How strongly a category's heading row is washed with its hue, and a category with
        /// nothing in it. One owner for the storage pane and the Inventory tab, which draw the same
        /// heading so the two read as one system (owner, 2026-09-23: "uniform for easy
        /// identification").
        /// </summary>
        public const float ItemCategoryWash = 0.09f;
        public const float ItemCategoryWashEmpty = 0.045f;

        /// <summary>The hue's strength on the glyph and label of a category with nothing in it.</summary>
        public const float ItemCategoryEmptyInk = 0.45f;

        /// <summary>The rung's hue, or <see cref="TextDim"/> for a rung that does not exist.</summary>
        public static HudColour StoragePriorityHue(int rung) =>
            (uint)rung < (uint)StoragePriorityHues.Length ? StoragePriorityHues[rung] : TextDim;

        /// <summary>The category's hue, or <see cref="TextDim"/> for a category that does not exist.</summary>
        public static HudColour ItemCategoryHue(int category) =>
            (uint)category < (uint)ItemCategoryHues.Length ? ItemCategoryHues[category] : TextDim;

        /// <summary>
        /// The Build categories' hues, in palette order.
        ///
        /// <para><b>This is the one place in the HUD allowed a hue per row</b>, and it is worth
        /// saying why, because <see cref="HudCategory"/> a few lines below exists on exactly the
        /// opposite principle — eight categories sharing five tokens, "because a screen with a
        /// colour per commodity is a screen with no colour code at all". That argument is about
        /// icons scattered across a whole screen, where a hue has to be recognised out of context
        /// and a large set cannot be. These are a closed row of tiles, always drawn together
        /// and always in the same order, and the hue is doing a different job here: it is not
        /// asking to be recognised in isolation, it is separating adjacent things and then
        /// carrying that separation down into the selected state. The acceptance criterion is
        /// "each is visually distinct with the labels masked", which a shared five-token set
        /// cannot meet by construction.</para>
        ///
        /// <summary>Indexed by position in <c>PaletteTools.Categories</c>, and
        /// <c>BuildPaletteTests</c> fails the fast tier if the two lengths part company. The
        /// eighth row came with the Zones category (U49): olive, because the token that says
        /// "growing" elsewhere (<see cref="Good"/>) is Security's hue in this row, and two
        /// adjacent tiles may not share a colour — the olive is what a field reads as when its
        /// category tile is picked.</para>
        /// </summary>
        public static readonly BuildTier[] BuildCategoryTiers =
        {
            new BuildTier(new HudColour(0x8f, 0xb3, 0xd9)), // Structure
            new BuildTier(new HudColour(0xe8, 0xa4, 0x5c)), // Production
            new BuildTier(new HudColour(0xc9, 0xa0, 0x6a)), // Furniture
            new BuildTier(new HudColour(0xe8, 0xd1, 0x5c)), // Power
            new BuildTier(new HudColour(0x7f, 0xc9, 0x8c)), // Security
            new BuildTier(new HudColour(0x6f, 0xd3, 0xe3)), // Floors
            new BuildTier(ZonesHue),                        // Zones
            new BuildTier(new HudColour(0xb9, 0xa8, 0xe0)), // Recreation
        };

        // ------------------------------------------------------------------ palette sub-types

        /// <summary>A sub-type chip at rest. Neutral: the tier under the hues is not colour-coded,
        /// because a sub-type belongs to whichever category is showing and has no identity of its
        /// own to carry.</summary>
        public static readonly HudColour SubTypeFill = new HudColour(255, 255, 255, 0.04f);

        public static readonly HudColour SubTypeBorder = new HudColour(255, 255, 255, 0.14f);
        public static readonly HudColour SubTypeInk = new HudColour(255, 255, 255, 0.82f);
        public static readonly HudColour SubTypeHoverFill = new HudColour(255, 255, 255, 0.09f);
        public static readonly HudColour SubTypeHoverBorder = new HudColour(255, 255, 255, 0.26f);

        /// <summary>A selected sub-type, and the one tier of the palette that is cyan: the hue
        /// tier above it is its own selected state and the material tier below it is its own, so
        /// this is the only place left where the interface's "this is on" colour still means
        /// what it means everywhere else in the HUD.</summary>
        public static readonly HudColour SubTypeSelectedFill = Accent.WithAlpha(0.14f);

        public static readonly HudColour SubTypeDisabledFill = new HudColour(255, 255, 255, 0.02f);
        public static readonly HudColour SubTypeDisabledBorder = new HudColour(255, 255, 255, 0.07f);

        /// <summary>
        /// A disabled sub-type's ink.
        /// </summary>
        /// <remarks>
        /// <para><b>0.35, where the specification says 0.30</b>, and the five hundredths are the
        /// only place this palette departs from a number it was given. At 0.30 the label measures
        /// <b>2.71:1</b> against its own chip over the brightest terrain the game can draw; at 0.35
        /// it measures <b>3.21:1</b>. WCAG would allow either — it exempts inactive controls
        /// entirely — and on a finished game 0.30 would be right, because a greyed-out row is
        /// meant to recede.</para>
        /// <para>It is wrong <i>here</i>, and for a reason that is temporary and specific: of the
        /// twenty-seven sub-types on this palette, one is live. Five of the seven categories are
        /// disabled from end to end. So the greyed-out state is not an occasional row a player
        /// skims past — it is nearly the whole panel, and it is the only thing telling them what
        /// this game is eventually going to let them build. A label nobody can read is a roadmap
        /// nobody can read. When the palette is mostly live this should go back to 0.30, and the
        /// test that holds it to 3:1 should be the thing that gets deleted, deliberately.</para>
        /// </remarks>
        public static readonly HudColour SubTypeDisabledInk = new HudColour(255, 255, 255, 0.35f);

        // ------------------------------------------------------------------ palette materials

        /// <summary>
        /// A material button's three colours. The button is tinted <i>from the material</i> so
        /// that it reads before its label does — which is also what makes the out-of-stock state
        /// legible without a word: a tinted button always means buildable, an untinted one never
        /// does.
        /// </summary>
        public readonly struct MaterialTint
        {
            public readonly HudColour Fill;
            public readonly HudColour Border;
            public readonly HudColour Ink;

            public MaterialTint(HudColour fill, HudColour border, HudColour ink)
            {
                Fill = fill;
                Border = border;
                Ink = ink;
            }
        }

        /// <summary>
        /// The tint for a <see cref="StuffHandle"/> value, or null where that material has none.
        ///
        /// <para><b>Four tints for two buildable materials, deliberately</b> (owner, 2026-09-17).
        /// Wood and Stone are what the colony can build with; concrete and steel are drawn here
        /// and shown by nothing, because the material band is driven by what is actually buildable
        /// rather than by this table. The alternative was two permanently dead buttons and two
        /// invented registry keys — and concrete was struck out of the content on 2026-09-16, so
        /// putting it back to decorate a menu would add a name to the wiki that no colonist could
        /// ever touch. The tints wait here instead, and the day either material becomes buildable
        /// it arrives correctly coloured with no further decision to make.</para>
        /// </summary>
        public static MaterialTint? MaterialTintOf(int stuff) => stuff switch
        {
            StuffHandle.Wood => new MaterialTint(
                new HudColour(0xf2, 0xe3, 0xcb),
                new HudColour(0x8a, 0x5a, 0x2b),
                new HudColour(0x5c, 0x3a, 0x17)),
            StuffHandle.Stone => new MaterialTint(
                new HudColour(0xe6, 0xe7, 0xe8),
                new HudColour(0x6b, 0x70, 0x75),
                new HudColour(0x3b, 0x40, 0x45)),
            StuffHandle.Concrete => new MaterialTint(
                new HudColour(0xe3, 0xe6, 0xea),
                new HudColour(0x5d, 0x6a, 0x78),
                new HudColour(0x33, 0x3d, 0x48)),
            StuffHandle.Steel => new MaterialTint(
                new HudColour(0xdf, 0xe6, 0xee),
                new HudColour(0x44, 0x60, 0x7d),
                new HudColour(0x28, 0x39, 0x4a)),
            _ => null,
        };

        /// <summary>Out of stock: the tint drops entirely, so the button stops promising.</summary>
        public static readonly HudColour MaterialOutFill = new HudColour(255, 255, 255, 0.05f);

        public static readonly HudColour MaterialOutBorder = new HudColour(255, 255, 255, 0.14f);
        public static readonly HudColour MaterialOutInk = new HudColour(255, 255, 255, 0.38f);

        /// <summary>A material button carries twice the border of everything else on the palette,
        /// because it is the panel's terminal choice.</summary>
        public const int MaterialBorderWidth = 2;

        /// <summary>The radius on a material button: one more than a control, for the same
        /// reason.</summary>
        public const int MaterialRadius = 5;

        // ------------------------------------------------------------------ palette header

        /// <summary>The Deconstruct action's outline and ink in the palette header. Warn, because
        /// taking a building apart is not destruction but it is not placement either.</summary>
        public static readonly HudColour DeconstructBorder = Warn.WithAlpha(0.45f);

        public static readonly HudColour DeconstructFill = Warn.WithAlpha(0.12f);

        /// <summary>The Cancel action's outline and ink. Bad, because it undoes work already
        /// ordered.</summary>
        public static readonly HudColour CancelBorder = Bad.WithAlpha(0.45f);

        public static readonly HudColour CancelFill = Bad.WithAlpha(0.12f);

        /// <summary>The Close control's outline, and the ink of its cross. Neutral: it is the one
        /// header action that does nothing to the world.</summary>
        public static readonly HudColour HeaderNeutralBorder = new HudColour(255, 255, 255, 0.18f);

        public static readonly HudColour HeaderNeutralInk = new HudColour(255, 255, 255, 0.65f);

        /// <summary>
        /// The hue of one pinned action — the verbs in <see cref="PaletteTools.Pinned"/> —
        /// or null for every other key.
        ///
        /// <para><b>The hue is the mode, and that is what it is for</b> (owner, 2026-09-17:
        /// <i>"the cancel/deconstruct colours … should also be represented in the dialog … so it
        /// becomes clearer what mode you are in"</i>). These are the tools that do something
        /// irreversible to what is already on the board, and all are armed from a strip
        /// that is easy to press on the way to somewhere else. So the colour does not stop
        /// at the button: while one of them is held, the palette says so in that tool's own
        /// colour, with that tool's own icon, where the breadcrumb would otherwise be. A player
        /// who is about to drag a box over their colony can tell from the panel whether they are
        /// about to cancel it, take it apart or plant it.</para>
        ///
        /// <para><b>The answer comes from <see cref="OrderColours"/> and not from here</b>, since
        /// 2026-09-20. It used to be four existing signal tokens written out below — already the
        /// interface's words for "careful", "destructive", "growing" and "information", and close
        /// enough to what each tool does that nothing new had to be invented. What that reasoning
        /// missed is that the chip is not the only place an order is coloured: the cursor and the
        /// mark left on the board are two more, they lived in another assembly, and two of the
        /// four disagreed with this list for months. The hue is the mode, so the mode has one
        /// owner — Mine's is the single hue there that is not a signal token, and
        /// <see cref="OrderColours.Mine"/> says why it had to stop being <see cref="Info"/>.</para>
        /// </summary>
        public static HudColour? PinnedActionHue(string key) => key switch
        {
            PaletteTools.Fell => OrderColours.Hue(DesignateTool.Fell),
            PaletteTools.Harvest => OrderColours.Hue(DesignateTool.Harvest),
            PaletteTools.Mine => OrderColours.Hue(DesignateTool.Mine),
            PaletteTools.Deconstruct => OrderColours.Hue(DesignateTool.Deconstruct),
            PaletteTools.Cancel => OrderColours.Hue(DesignateTool.Cancel),
            PaletteTools.GrowZone => OrderColours.Hue(DesignateTool.GrowZone),
            PaletteTools.Stockpile => OrderColours.Hue(DesignateTool.Stockpile),
            _ => null,
        };

        /// <summary>
        /// The hue the armed banner wears for an order: a pinned action's own, or — for the one
        /// order that lives in a category rather than on the strip, taking power lines up — its
        /// order colour. Kept apart from <see cref="PinnedActionHue"/>, which lights the strip's
        /// buttons and must answer nothing for a category's chip (design 32 §10).
        /// </summary>
        public static HudColour? ArmedOrderHue(string key) =>
            PinnedActionHue(key)
            ?? (key == PaletteTools.Unwire ? OrderColours.Hue(DesignateTool.RemoveConduit) : (HudColour?)null);

        // ------------------------------------------------------------------ categories

        /// <summary>
        /// The stroke colour of an icon in this category. Total by construction: every category
        /// resolves, and <see cref="HudCategory.Neutral"/> resolves to the meta ink, which is what
        /// "inherit the row's colour" means for an icon drawn on its own.
        /// </summary>
        public static HudColour ColourOf(HudCategory category) => category switch
        {
            HudCategory.Sustenance => Good,
            HudCategory.Organic => Warn,
            HudCategory.Metal => Info,
            HudCategory.Mineral => TextMeta,
            HudCategory.Fluid => Accent,
            HudCategory.Medical => Bad,
            HudCategory.Work => Accent,
            HudCategory.People => Good,
            HudCategory.Record => Info,
            _ => TextMeta,
        };

        static readonly Dictionary<string, HudCategory> Categories = new Dictionary<string, HudCategory>
        {
            // stores
            { "ui.res.meal", HudCategory.Sustenance },
            { "ui.res.meal.veg", HudCategory.Sustenance },
            { "ui.res.meal.burnt", HudCategory.Sustenance },
            { "ui.res.rations", HudCategory.Sustenance },
            { "ui.res.meat", HudCategory.Sustenance },
            { "ui.res.grain", HudCategory.Sustenance },
            { "ui.res.wood", HudCategory.Organic },
            { "ui.res.fabric", HudCategory.Organic },
            { "ui.res.scrap", HudCategory.Metal },
            { "ui.res.ironore", HudCategory.Metal },
            { "ui.res.stone", HudCategory.Mineral },
            { "ui.res.coal", HudCategory.Mineral },
            { "ui.res.medkit", HudCategory.Medical },

            // command bar
            { "ui.tab.build", HudCategory.Work },
            { "ui.tab.work", HudCategory.Work },
            { "ui.tab.schedule", HudCategory.Work },
            { "ui.tab.research", HudCategory.Record },
            { "ui.tab.colonists", HudCategory.People },
            { "ui.tab.assign", HudCategory.People },
            { "ui.tab.animals", HudCategory.People },
            { "ui.tab.wildlife", HudCategory.People },
            { "ui.tab.bills", HudCategory.Work },
            { "ui.tab.factions", HudCategory.Record },
            { "ui.tab.archive", HudCategory.Record },
            { "ui.tab.almanac", HudCategory.Record },
            { "ui.tab.menu", HudCategory.Neutral },
        };

        /// <summary>
        /// The category a symbolic icon key belongs to, or <see cref="HudCategory.Neutral"/> for
        /// every key that has not been given one — which is most of them, and correct: only stores
        /// and the command bar are allowed to carry a category colour at all.
        /// </summary>
        public static HudCategory CategoryOf(string key) =>
            key != null && Categories.TryGetValue(key, out HudCategory category)
                ? category
                : HudCategory.Neutral;

        /// <summary>Every key that has been given a category, so a test can hold each to the
        /// naming registry the way the ledger and the settings panel are already held.</summary>
        public static IEnumerable<string> CategorisedKeys => Categories.Keys;
    }
}
