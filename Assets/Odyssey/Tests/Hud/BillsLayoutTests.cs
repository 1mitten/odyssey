#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>The bill list's arithmetic (design 49): the row fits the pane, and the name keeps its room.</summary>
    public class BillsLayoutTests
    {
        [Test]
        public void TheRowsColumnsFitThePaneWithRoomForTheName()
        {
            Assert.That(BillsLayout.NameColumn, Is.GreaterThanOrEqualTo(BillsLayout.NameColumnMin),
                "widening a fixed column has squeezed the bill's name");
            int total = BillsLayout.IndexColumn + BillsLayout.IconColumn + BillsLayout.NameColumn
                + BillsLayout.ModeColumn + BillsLayout.StepperColumn + BillsLayout.ProgressColumn
                + BillsLayout.ActionsColumn + 6 * BillsLayout.ColumnGap;
            Assert.That(total + 2 * BillsLayout.SidePad + 2 * HudTheme.BorderWidth, Is.EqualTo(BillsLayout.PaneWidth));
        }

        [Test]
        public void AStationsPaneIsWiderThanAStores()
        {
            // The store's pane is the widest tile pane before this one; a bill row does not fit it.
            Assert.That(BillsLayout.PaneWidth, Is.GreaterThan(HudLayout.InspectWidth));
        }

        [Test]
        public void EveryControlIsTheOneSquareAndFitsItsRow()
        {
            Assert.That(BillsLayout.IconColumn, Is.EqualTo(BillsLayout.Control));
            Assert.That(BillsLayout.StepperColumn, Is.GreaterThan(2 * BillsLayout.Control),
                "the stepper's number needs room between its buttons");
            Assert.That(BillsLayout.ActionsColumn, Is.EqualTo(3 * BillsLayout.Control + 2 * BillsLayout.ActionGap));
            Assert.That(BillsLayout.Control, Is.LessThan(BillsLayout.RowHeight));
            Assert.That(BillsLayout.SwitchHeight, Is.LessThan(BillsLayout.StatusHeight));
        }

        [Test]
        public void TheStripsWordsClearItsBarByTheSidePadding()
        {
            // A strip's left padding counts its 3 px bar, so the words stand the side padding clear of it.
            Assert.That(BillsLayout.StripPadLeft, Is.EqualTo(BillsLayout.SidePad + BillsLayout.SectionBar));
        }

        [Test]
        public void TheModeCellHoldsItsLongestWord()
        {
            // "Until you have" at 13 px Archivo Narrow is about 0.45 em a character; the cell also
            // carries its padding and the 11 px cycle mark.
            float widest = 0f;
            foreach (string key in BillsModel.ModeKeys)
                widest = System.Math.Max(widest, Registry.Label(key).Length * 13f * 0.45f);
            Assert.That(widest + 9 + 8 + BillsLayout.ModeGlyph + 6, Is.LessThanOrEqualTo(BillsLayout.ModeColumn));
        }
    }
}
