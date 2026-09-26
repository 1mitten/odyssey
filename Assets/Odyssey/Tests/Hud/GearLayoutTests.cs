#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Gear tab's measurements (design 47 §2) add up to the specification's, and the pane every
    /// tab now stands in is that tall.
    /// </summary>
    public class GearLayoutTests
    {
        [Test]
        public void TheBodyIsTheSpecificationsSum()
        {
            Assert.That(GearLayout.DollHeight, Is.EqualTo(138), "three slot rows of 40 and two gaps of 9");
            Assert.That(GearLayout.BodyHeight, Is.EqualTo(12 + 138 + 12 + 40 + 12 + 30));
            Assert.That(GearLayout.BodyHeight, Is.EqualTo(GearLayout.TabBody));
            Assert.That(HudLayout.InspectTabBody, Is.EqualTo(GearLayout.TabBody), "every tab stands in it");
        }

        /// <summary>The kit row fits across the pane: the label, six tiles and their gaps, and the hint after them.</summary>
        [Test]
        public void TheKitRowFitsThePane()
        {
            int content = HudLayout.InspectWidth - 2 * (HudLayout.Pad + HudTheme.BorderWidth);
            int tiles = GearLayout.KitLabelWidth + GearLayout.KitSlots * (GearLayout.KitGap + GearLayout.KitTile);
            Assert.That(tiles, Is.LessThan(content));
        }

        /// <summary>A slot's tile and its words fit their column beside the figure.</summary>
        [Test]
        public void ASlotFitsItsColumn()
        {
            int content = HudLayout.InspectWidth - 2 * (HudLayout.Pad + HudTheme.BorderWidth);
            int column = (content - GearLayout.FigureWidth - 2 * GearLayout.DollColumnGap) / 2;
            int words = column - GearLayout.SlotTile - GearLayout.SlotTextGap;
            Assert.That(words, Is.GreaterThanOrEqualTo(120),
                "room for \"Issued jumpsuit\" at 14/500 beside a 40 tile");
        }

        /// <summary>The popovers open outside the pane, so they never cover the doll.</summary>
        [Test]
        public void APopoverClearsThePane()
        {
            Assert.That(GearLayout.PopoverLeft(0, HudLayout.InspectWidth), Is.EqualTo(569));
            Assert.That(GearLayout.PopoverLeft(HudLayout.Edge, HudLayout.InspectWidth),
                Is.GreaterThan(HudLayout.Edge + HudLayout.InspectWidth));
        }

        /// <summary>The lock is a shackle and a body, the chevron one stroke; both parse and stay in the 24 box.</summary>
        [Test]
        public void TheTwoMarksParse()
        {
            IReadOnlyList<SvgPath.Subpath> lockMark = SvgPath.Parse(HudIcons.Lock);
            Assert.That(lockMark, Has.Count.EqualTo(2), "the shackle and the body");
            Assert.That(lockMark[1].Closed, Is.True);
            Assert.That(SvgPath.Parse(HudIcons.ChevronDown), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheKitIsTwoOnTheBeltAndFourMoreWithAPack() =>
            Assert.That((GearLayout.KitBelt, GearLayout.KitPack, GearLayout.KitSlots), Is.EqualTo((2, 4, 6)));
    }
}
