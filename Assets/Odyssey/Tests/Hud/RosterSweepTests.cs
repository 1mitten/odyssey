#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Dragging across the roster's cards (design 33 §20): a left press on a card and a drag over
    /// others selects every colonist passed over; a click is still one colonist; Shift adds. The
    /// pointer events are the view's and cannot be driven here (CLAUDE.md, "Nothing tests that a
    /// click reaches the game"); what they mean is all here.
    /// </summary>
    public class RosterSweepTests
    {
        static readonly PawnId A = new PawnId(1), B = new PawnId(2), C = new PawnId(3),
            D = new PawnId(4), E = new PawnId(5), Offpage = new PawnId(9);

        static readonly PawnId[] Page = { A, B, C, D, E };
        static readonly PawnId[] None = { };

        static PawnId[] Sel(RosterSweep sweep)
        {
            var list = new List<PawnId>(sweep.Selection);
            return list.ToArray();
        }

        /// <summary>A press and a release on one card is a click: that colonist, and the click's outcome (the jump).</summary>
        [Test]
        public void AClickWithoutADragSelectsOneAndIsAClick()
        {
            var sweep = new RosterSweep();
            Assert.That(sweep.Press(Page, C, shift: false, selection: new[] { A, B }), Is.True);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { C }), "a plain press did not replace the selection with her");

            // The pointer wobbles over its own card: still a click.
            Assert.That(sweep.Over(C), Is.False);
            Assert.That(sweep.Release(), Is.EqualTo(RosterSweepEnd.Clicked));
            Assert.That(sweep.Active, Is.False);
        }

        /// <summary>
        /// The owner's ask: press on one card and drag across others, and every card passed over
        /// is selected — the pressed one first, as the primary — and it is not a click, so no jump.
        /// </summary>
        [Test]
        public void DraggingAcrossCardsSelectsEveryCardPassedOver()
        {
            var sweep = new RosterSweep();
            sweep.Press(Page, B, shift: false, selection: new[] { E });
            Assert.That(sweep.Over(C), Is.True, "moving on to another card did not move the sweep");
            Assert.That(sweep.Over(D), Is.True);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { B, C, D }), "the selection held at the press survived a plain sweep");
            Assert.That(sweep.Release(), Is.EqualTo(RosterSweepEnd.Swept));
        }

        /// <summary>
        /// A flick from one end of the strip to the other can land on no card in between; the
        /// range still covers them, because the strip is one row and the pointer crossed them.
        /// </summary>
        [Test]
        public void AFlickPastCardsCoversThemAll()
        {
            var sweep = new RosterSweep();
            sweep.Press(Page, A, shift: false, selection: None);
            sweep.Over(E);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { A, B, C, D, E }));
        }

        /// <summary>Leftwards works, with the pressed card still the primary.</summary>
        [Test]
        public void DraggingLeftwardsCoversTheRangeWithThePressedCardFirst()
        {
            var sweep = new RosterSweep();
            sweep.Press(Page, D, shift: false, selection: None);
            sweep.Over(B);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { D, B, C }));
        }

        /// <summary>
        /// Dragged back, the range lets go of the cards it no longer spans, as the world's box
        /// does; back on the pressed card it is that card alone — still a sweep, not a click.
        /// </summary>
        [Test]
        public void DraggingBackLetsGoOfTheCardsNoLongerSpanned()
        {
            var sweep = new RosterSweep();
            sweep.Press(Page, B, shift: false, selection: None);
            sweep.Over(E);
            sweep.Over(C);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { B, C }));

            sweep.Over(B);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { B }));
            Assert.That(sweep.Release(), Is.EqualTo(RosterSweepEnd.Swept), "a drag that came home was taken for a click");
        }

        /// <summary>Shift keeps what was selected, ahead, and adds every card passed over.</summary>
        [Test]
        public void ShiftAddsTheSweepToTheSelectionHeld()
        {
            var sweep = new RosterSweep();
            sweep.Press(Page, C, shift: true, selection: new[] { A, Offpage });
            sweep.Over(E);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { A, Offpage, C, D, E }));
            Assert.That(sweep.Release(), Is.EqualTo(RosterSweepEnd.Swept));
        }

        /// <summary>
        /// A Shift-click with no drag toggles, as a Shift-click does in the world; a Shift-drag
        /// that starts on a selected card adds her back with the rest rather than toggling her out.
        /// </summary>
        [Test]
        public void AShiftClickTogglesAndAShiftDragOnlyAdds()
        {
            var sweep = new RosterSweep();
            sweep.Press(Page, B, shift: true, selection: new[] { A, B });
            Assert.That(Sel(sweep), Is.EqualTo(new[] { A }), "a shift-click on a selected card did not take her out");
            Assert.That(sweep.Release(), Is.EqualTo(RosterSweepEnd.Clicked));

            sweep.Press(Page, D, shift: true, selection: new[] { A });
            Assert.That(Sel(sweep), Is.EqualTo(new[] { A, D }), "a shift-click on an unselected card did not add her");

            sweep.Press(Page, B, shift: true, selection: new[] { A, B });
            sweep.Over(C);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { A, B, C }), "a shift-drag took the pressed card out");
        }

        /// <summary>
        /// Only the page the press began on: a card that is not on it — another page, the pager —
        /// covers nothing, and nothing is swept once the button is up.
        /// </summary>
        [Test]
        public void ACardOffThePageOrAfterTheReleaseChangesNothing()
        {
            var sweep = new RosterSweep();
            Assert.That(sweep.Over(C), Is.False, "a card under an idle pointer started a sweep");

            Assert.That(sweep.Press(Page, Offpage, shift: false, selection: None), Is.False);
            Assert.That(sweep.Active, Is.False);
            Assert.That(sweep.Release(), Is.EqualTo(RosterSweepEnd.None));

            sweep.Press(Page, A, shift: false, selection: None);
            Assert.That(sweep.Over(Offpage), Is.False);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { A }));
            sweep.Release();
            Assert.That(sweep.Over(C), Is.False, "the sweep outlived its button");
        }

        /// <summary>The page is copied at the press: the strip refilling under the drag does not move what it covers.</summary>
        [Test]
        public void ThePageIsTheOneThePressSaw()
        {
            var page = new List<PawnId>(Page);
            var sweep = new RosterSweep();
            sweep.Press(page, A, shift: false, selection: None);
            page.Reverse();
            sweep.Over(C);
            Assert.That(Sel(sweep), Is.EqualTo(new[] { A, B, C }));
        }
    }
}
