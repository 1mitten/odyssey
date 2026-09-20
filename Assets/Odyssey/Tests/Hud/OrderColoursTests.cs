#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The chip a player presses and the mark left on the board are the same colour, for every
    /// order, and neither of them is a colour the board already uses for something else.
    ///
    /// <para><b>These are the assertions that would have caught the bug.</b> Deconstruct was
    /// orange on the palette header and red on the ground for months, and Mine was blue on the
    /// header and amber on the ground, because the two mappings lived in two assemblies and only
    /// one of them was in a tier anybody ran. The mapping is in <c>Odyssey.Hud</c> now precisely
    /// so this file can exist and run in two seconds.</para>
    /// </summary>
    public class OrderColoursTests
    {
        static readonly (string Key, DesignateTool Tool)[] Pinned =
        {
            (PaletteTools.Fell, DesignateTool.Fell),
            (PaletteTools.Mine, DesignateTool.Mine),
            (PaletteTools.Deconstruct, DesignateTool.Deconstruct),
            (PaletteTools.Cancel, DesignateTool.Cancel),
        };

        [Test]
        public void EveryPinnedChipIsTheColourItsOwnOrdersAreMarkedIn()
        {
            foreach ((string key, DesignateTool tool) in Pinned)
            {
                HudColour? chip = HudTheme.PinnedActionHue(key);
                Assert.That(chip, Is.Not.Null, $"{key} is pinned and has no hue");

                HudColour mark = OrderColours.Mark(tool);
                Assert.That((mark.R, mark.G, mark.B), Is.EqualTo((chip!.Value.R, chip.Value.G, chip.Value.B)),
                    $"{key}: the chip and the mark on the board are different colours");

                HudColour cursor = OrderColours.Cursor(tool);
                Assert.That((cursor.R, cursor.G, cursor.B), Is.EqualTo((chip.Value.R, chip.Value.G, chip.Value.B)),
                    $"{key}: the chip and the drag cursor are different colours");
            }
        }

        /// <summary>
        /// The four pinned keys are exactly the keys with a hue. A fifth order added to
        /// <see cref="PaletteTools.Pinned"/> and forgotten here is the failure this catches — and
        /// it is the failure the array's own comment warns about.
        /// </summary>
        [Test]
        public void EveryPinnedToolHasAHueAndNothingElseDoes()
        {
            foreach (string key in PaletteTools.Pinned)
                Assert.That(HudTheme.PinnedActionHue(key), Is.Not.Null, $"{key} is pinned and has no hue");

            Assert.That(HudTheme.PinnedActionHue(PaletteTools.Wall), Is.Null);
            Assert.That(HudTheme.PinnedActionHue("ui.arch.tool.nothing"), Is.Null);
        }

        /// <summary>
        /// <c>DesignationKind</c>'s own numbering, read back the way the snapshot delivers it.
        /// The enum lives in <c>Odyssey.Sim</c>, which this assembly cannot reference, so the
        /// numbers are the contract and this is where it is pinned.
        /// </summary>
        [Test]
        public void AStandingOrderIsMarkedInItsOwnToolsColour()
        {
            Assert.That(OrderColours.ToolOf(1), Is.EqualTo(DesignateTool.Mine));
            Assert.That(OrderColours.ToolOf(2), Is.EqualTo(DesignateTool.Deconstruct));
            Assert.That(OrderColours.ToolOf(3), Is.EqualTo(DesignateTool.Fell));

            // Kind 0 is "no order". Nothing draws it, and it falls to the build accent rather
            // than throwing, because a total mapping is what this whole file is about.
            Assert.That(OrderColours.ToolOf(0), Is.EqualTo(DesignateTool.Build));

            foreach (byte kind in new byte[] { 1, 2, 3 })
            {
                HudColour mark = OrderColours.Mark(kind);
                HudColour direct = OrderColours.Mark(OrderColours.ToolOf(kind));
                Assert.That(mark.Hex, Is.EqualTo(direct.Hex));
            }
        }

        /// <summary>
        /// Deconstruct is the owner's orange and not the cancel red (owner, 2026-09-20). Written
        /// as "the same as the Warn token" rather than as a hex literal, because the point of the
        /// change is that it is the palette's own colour for that tool and not a second copy.
        /// </summary>
        [Test]
        public void DeconstructIsTheWarnOrangeAndCancelKeepsTheRed()
        {
            Assert.That(OrderColours.Hue(DesignateTool.Deconstruct).Hex, Is.EqualTo(HudTheme.Warn.Hex));
            Assert.That(OrderColours.Hue(DesignateTool.Cancel).Hex, Is.EqualTo(HudTheme.Bad.Hex));
            Assert.That(OrderColours.Hue(DesignateTool.Fell).Hex, Is.EqualTo(HudTheme.Good.Hex));

            Assert.That(OrderColours.Hue(DesignateTool.Deconstruct).Hex,
                Is.Not.EqualTo(OrderColours.Hue(DesignateTool.Cancel).Hex),
                "deconstruct fell through to the cancel colour once; that is the regression");
        }

        /// <summary>
        /// No two orders on the board are the same colour, and none of them is the build accent a
        /// pending blueprint is drawn in.
        ///
        /// <para><b>This is the constraint that decided Mine's hue.</b> Matching the board to the
        /// toolbar would have made a mine order <see cref="HudTheme.Info"/>, 60 points from
        /// <see cref="HudTheme.Accent"/> — a half-dug, half-planned colony in two blues nobody
        /// could separate. The distance is asserted rather than eyeballed because the tokens are
        /// shared and somebody tuning <c>Info</c> for a tooltip should learn here.</para>
        ///
        /// <para><b>Eighty, because that is what the existing palette already clears.</b> The
        /// closest pair in the set is Deconstruct's orange against Cancel's red at 83 — two warm
        /// hues the owner has looked at and kept — so eighty is the largest number that asserts
        /// something without demanding a repaint of colours already judged. It is deliberately
        /// <i>not</i> sixty: the first draft of Mine's blue measured exactly sixty from the
        /// accent and passed a sixty-point threshold, which is a test agreeing with itself about
        /// the one pair it was written to police.</para>
        /// </summary>
        [Test]
        public void NoTwoOrdersLookAlikeOnTheBoard()
        {
            var tools = new[]
            {
                DesignateTool.Fell, DesignateTool.Mine, DesignateTool.Deconstruct,
                DesignateTool.Cancel, DesignateTool.Build,
            };

            for (int i = 0; i < tools.Length; i++)
            for (int j = i + 1; j < tools.Length; j++)
            {
                HudColour a = OrderColours.Hue(tools[i]);
                HudColour b = OrderColours.Hue(tools[j]);
                int distance = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
                Assert.That(distance, Is.GreaterThanOrEqualTo(80),
                    $"{tools[i]} ({a.Hex}) and {tools[j]} ({b.Hex}) are too close to tell apart");
            }
        }

        /// <summary>
        /// A mark is lighter than the box being dragged. The cursor is following the hand and has
        /// to be found among however many marks are already down; a mark is paint over something
        /// the player is meant to keep looking at.
        /// </summary>
        [Test]
        public void AMarkIsLighterThanTheCursorThatPlacedIt()
        {
            Assert.That(OrderColours.MarkAlpha, Is.LessThan(OrderColours.CursorAlpha));
            Assert.That(OrderColours.Mark(DesignateTool.Mine).A, Is.EqualTo(OrderColours.MarkAlpha));
            Assert.That(OrderColours.Cursor(DesignateTool.Mine).A, Is.EqualTo(OrderColours.CursorAlpha));
        }
    }
}
