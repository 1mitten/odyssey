#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>One colour per order, on every surface that draws it.</b> The chip in the orders strip,
    /// the palette header while the tool is held, the box under the pointer during a drag, and the
    /// mark left on the board afterwards all read their hue from here.
    ///
    /// <para><b>Why it exists</b> (owner, 2026-09-20: <i>"match the orders blueprints/placement
    /// titles to the color assigned on their toolbar"</i>). It was two mappings, in two assemblies,
    /// written months apart: <see cref="HudTheme.PinnedActionHue"/> said what a chip was, and a
    /// handful of <c>Color</c> constants in <c>OdysseyBootstrap</c> said what the board was. They
    /// disagreed on two of the four tools — Mine was pale blue on the chip and amber on the ground,
    /// and Deconstruct was orange on the chip and <i>red</i> on the ground, which is the colour the
    /// interface uses for Cancel. A player holding the deconstruct tool was told "orange" by the
    /// panel and "cancel" by the cursor.</para>
    ///
    /// <para><b>The toolbar won</b> (owner's decision, same day, out of three offered): the chip is
    /// what a player presses and therefore what they learn the colour from, so the board follows
    /// it rather than the other way round. That cost Mine its warm amber, which had been chosen to
    /// stand out against cool stone — recorded in <c>16-cancel-and-deconstruct.md</c> §7 rather
    /// than quietly dropped, because it was a real reason and it lost to a better one.</para>
    ///
    /// <para><b>Unity-free, so the fast tier owns the rule.</b> The mapping is the thing that goes
    /// wrong — a tool added to the palette and forgotten here — and the tier that runs in two
    /// seconds is where that has to be caught. <c>OrderColoursTests</c> walks every
    /// <see cref="DesignateTool"/> and every pinned key and asserts the two agree.</para>
    /// </summary>
    public static class OrderColours
    {
        /// <summary>
        /// Mine's own blue, and the one hue here that is not an existing signal token.
        ///
        /// <para><see cref="HudTheme.Info"/> is what the chip used and it could not survive the
        /// move to the board: at <c>#8fd0e3</c> it sits 60 channel-points from
        /// <see cref="HudTheme.Accent"/> <c>#6fd3e3</c>, which is what a pending <em>build</em> is
        /// marked in, so a colony half dug and half planned would have been two blues nobody could
        /// tell apart at the play camera. Used on <b>both</b> surfaces so the chip and the mark
        /// still match exactly — which is the whole point of this file, and is why the chip moved
        /// rather than the board being allowed to drift.</para>
        ///
        /// <para><b>The green channel is what does the work</b>, and the first attempt at this got
        /// it wrong in an instructive way. <c>#5fb2d8</c> looked deeper and bluer and measured at
        /// exactly 60 from the accent — it passed
        /// <c>OrderColoursTests.NoTwoOrdersLookAlikeOnTheBoard</c> by sitting on the threshold,
        /// for the one pair the threshold existed to police. A blue reads as <i>cyan</i> when its
        /// green is near its blue, so the separation had to come out of green: 144 against the
        /// accent's 211, which is 131 points away and the third-closest pair rather than the
        /// closest.</para>
        /// </summary>
        public static readonly HudColour Mine = new HudColour(0x4a, 0x90, 0xc8);

        /// <summary>
        /// A storage zone's blue-grey, on the chip, the drag cursor, the armed banner and the
        /// ground it is painted on.
        ///
        /// <para>Desaturated on purpose. A store is the one order that covers a large area and
        /// then <b>stays</b> — a mine order is worked off, a field turns into a crop, and a
        /// warehouse floor is simply a warehouse floor for the rest of the colony's life. A
        /// saturated hue over fifty cells for a hundred hours is a screen the player stops
        /// seeing past.</para>
        ///
        /// <para>Its distance from the other five is what <c>OrderColoursTests</c> polices: it is
        /// far from Mine's blue in green and in saturation, and far from the Zones brown in hue
        /// altogether.</para>
        /// </summary>
        public static readonly HudColour StoreHue = new HudColour(0x7f, 0x96, 0xa8);

        /// <summary>
        /// A drafted colonist's hue (design 33 §2g): the marker over its head and the line to where
        /// it has been sent. A hot orange-red, and deliberately not <see cref="HudTheme.Bad"/> — that
        /// is the Cancel tool's, and a drafted colonist is under orders, not being undone. It sits
        /// 79 channel-points from it, mostly in blue, and far from every tool hue in hue itself.
        /// </summary>
        public static readonly HudColour Draft = new HudColour(0xff, 0x5a, 0x3c);

        /// <summary>
        /// The hue of an order, opaque — the chip's colour, and the colour every mark and cursor
        /// below is a transparency of.
        ///
        /// <para>Total over the enum on purpose. A two-branch ternary answering a three-kind
        /// question is what drew deconstruct orders in the felling green once already; the
        /// default here is the build accent because <see cref="DesignateTool.Build"/> and
        /// <see cref="DesignateTool.None"/> are the only values that reach it and a pending build
        /// is what one of them means.</para>
        /// </summary>
        public static HudColour Hue(DesignateTool tool) => tool switch
        {
            DesignateTool.Fell => HudTheme.Good,
            DesignateTool.Mine => Mine,
            DesignateTool.Deconstruct => HudTheme.Warn,
            DesignateTool.Cancel => HudTheme.Bad,
            // The growing zone, on the olive its own Build category already wears. It comes
            // through here rather than keeping the switch it arrived with, because this file's
            // whole point is that a mode has one colour on every surface it appears - and the
            // zone appears on four: the category tile, the pinned chip, the drag cursor and the
            // armed banner. Its committed ground is NOT this colour and is not meant to be: a
            // painted field is worked soil (ChunkRenderer.TilledGrade), which is the result
            // rather than the order, exactly as a built wall is not the blue of its blueprint.
            DesignateTool.GrowZone => HudTheme.ZonesHue,
            // The store, on its own blue-grey. Not the Zones brown the growing tool wears: the
            // two are the only tools that paint ground rather than order work on it, so they are
            // the pair a player is most likely to confuse, and a shared hue would make a field
            // and a warehouse the same colour on the board. Blue-grey because that is what the
            // committed ground wears too (`ChunkRenderer.StoredGrade`) — a store, unlike a field,
            // is not a transformation of the ground, so the order's colour and the result's can
            // be the same one.
            DesignateTool.Stockpile => StoreHue,
            _ => HudTheme.Accent,
        };

        /// <summary>
        /// The same hue for a standing order read back off the world, whose kind arrives as the
        /// byte <c>OrderView.Kind</c> carries.
        ///
        /// <para>The numbers are <c>DesignationKind</c>'s — Mine 1, Deconstruct 2, Fell 3 —
        /// restated here for the reason <see cref="InspectModel"/> already restates them: the enum
        /// lives in <c>Odyssey.Sim</c>, which this assembly does not reference, and the snapshot
        /// carries the value rather than the type. Anything else is a build order.</para>
        /// </summary>
        public static DesignateTool ToolOf(byte designationKind) => designationKind switch
        {
            1 => DesignateTool.Mine,
            2 => DesignateTool.Deconstruct,
            3 => DesignateTool.Fell,
            _ => DesignateTool.Build,
        };

        /// <summary>
        /// How solid a mark left on the board is. Low, because it is paint over something the
        /// player is meant to keep looking at — the rock face, the wall, the grass.
        /// </summary>
        public const float MarkAlpha = 0.42f;

        /// <summary>
        /// How solid the box under the pointer is. Heavier than a mark, because it is following
        /// the hand and has to be found instantly among however many marks are already down.
        /// </summary>
        public const float CursorAlpha = 0.60f;

        /// <summary>The paint an ordered cell carries until the work is done.</summary>
        public static HudColour Mark(DesignateTool tool) => Hue(tool).WithAlpha(MarkAlpha);

        /// <summary>The same, for an order the world is reporting back.</summary>
        public static HudColour Mark(byte designationKind) => Mark(ToolOf(designationKind));

        /// <summary>The box the pointer drags before the button is let go.</summary>
        public static HudColour Cursor(DesignateTool tool) => Hue(tool).WithAlpha(CursorAlpha);
    }
}
