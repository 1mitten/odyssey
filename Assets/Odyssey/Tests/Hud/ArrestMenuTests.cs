#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Arrest as a right-click row on a colonist (design 59 §10, §16 H1–H2; the owner's ruling at
    /// the second review): offered while nobody selected is drafted, so a drafted right-click is
    /// still a move; the first standing colonist selected is sent, or the nearest; dim with its
    /// reason when the simulation would refuse it.
    /// </summary>
    public class ArrestMenuTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Cy = new PawnId(3);

        static WorldSnapshot Board(bool bedFree = true, bool withBo = true, bool withCy = true, params PawnId[] drafted)
        {
            WorldSnapshot frame = Frame.Write(layers: 4);
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            if (withBo) frame.AddPawn(new PawnView(Bo, new CellRef(7, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            if (withCy) frame.AddPawn(new PawnView(Cy, new CellRef(9, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.SetPrisonBedFree(bedFree);
            AspectKey key = AspectKey.Of(OrderModel.DraftedAspect);
            foreach (PawnId pawn in drafted) frame.AddPawnAspect(new PawnAspect(pawn, key, 1));
            return frame;
        }

        static (List<Intent> Sent, List<ContextMenuRow> Menu) RightClickOnAda(IReadOnlyList<PawnId> selection, WorldSnapshot frame)
        {
            var sent = new List<Intent>();
            var menu = new List<ContextMenuRow>();
            OrderModel.RightClick(selection, frame, new CellRef(4, 4, 1), Ada, false, sent, menu);
            return (sent, menu);
        }

        [Test]
        public void ARightClickOnAColonistOffersArrestAndSendsTheSelectedColonist()
        {
            var (sent, menu) = RightClickOnAda(new[] { Bo }, Board());
            Assert.That(sent, Is.Empty);
            ContextMenuRow arrest = menu.Single(r => r.Key == ContextMenuModel.ArrestKey);
            Assert.That(arrest.Enabled, Is.True);
            var chosen = new List<Intent>();
            ContextMenuModel.Choose(arrest, chosen);
            Assert.That(chosen.Single().Kind, Is.EqualTo(IntentKind.OrderArrest));
            Assert.That(chosen.Single().A, Is.EqualTo(Bo.Value), "the selected colonist makes the arrest");
            Assert.That(chosen.Single().B, Is.EqualTo(Ada.Value));
        }

        /// <summary>A drafted right-click that touches a colonist is a move (design 33 §2f), and stays one.</summary>
        [Test]
        public void ADraftedRightClickOnAColonistIsStillAMove()
        {
            var (sent, menu) = RightClickOnAda(new[] { Bo }, Board(drafted: Bo));
            Assert.That(menu, Is.Empty, "a menu where a drafted squad expects to move");
            Assert.That(sent.Select(i => i.Kind), Has.Member(IntentKind.OrderMove));
        }

        /// <summary>Selecting only her and right-clicking her sends the nearest colonist who can reach her.</summary>
        [Test]
        public void HerselfSelectedSendsTheNearest()
        {
            var (_, menu) = RightClickOnAda(new[] { Ada }, Board());
            var chosen = new List<Intent>();
            ContextMenuModel.Choose(menu.Single(r => r.Key == ContextMenuModel.ArrestKey), chosen);
            Assert.That(chosen.Single().A, Is.EqualTo(0), "A = 0 asks the simulation for the nearest");
        }

        /// <summary>
        /// Design 59 §16 H2: the refusals a player can see coming make the row dim, with the reason,
        /// rather than a press that does nothing.
        /// </summary>
        [TestCase("nobed")]
        [TestCase("alone")]
        public void ArrestIsDimWithItsReasonWhenItWouldBeRefused(string why)
        {
            WorldSnapshot frame = why == "nobed" ? Board(bedFree: false) : Board(withBo: false, withCy: false);
            var (sent, menu) = RightClickOnAda(new[] { Ada }, frame);
            Assert.That(sent, Is.Empty);
            ContextMenuRow arrest = menu.Single(r => r.Key == ContextMenuModel.ArrestKey);
            Assert.That(arrest.Enabled, Is.False, "live, and the press would do nothing");
            Assert.That(arrest.Reason, Is.EqualTo(ContextMenuModel.ArrestRefusal(frame, Ada)));
            Assert.That(arrest.Reason, Is.Not.Empty);
            var chosen = new List<Intent>();
            ContextMenuModel.Choose(arrest, chosen);
            Assert.That(chosen, Is.Empty, "a dim row sent an order");
        }

        /// <summary>No colonist selected, no row — and no menu of one Cancel.</summary>
        [Test]
        public void NoColonistSelectedNoRow()
        {
            var (_, menu) = RightClickOnAda(new PawnId[0], Board());
            Assert.That(menu.Any(r => r.Key == ContextMenuModel.ArrestKey), Is.False);
        }
    }
}
