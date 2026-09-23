#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// What the draft key and a right-click mean (design 33 §2f), in the tier that runs in
    /// seconds — the presenters that hear them cannot be driven by a test at all.
    /// </summary>
    public class OrderModelTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Hog = new PawnId(3);
        static readonly CellRef Ground = new CellRef(10, 12, 3);

        static WorldSnapshot Frame(bool adaDrafted, bool boDrafted, int adaOrderCell = -1)
        {
            var frame = new WorldSnapshot();
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 4, 3), 800, 800, 700));
            frame.AddPawn(new PawnView(Bo, new CellRef(5, 4, 3), 800, 800, 700));
            frame.AddPawn(new PawnView(Hog, new CellRef(6, 4, 3), 800, 800, 700, kind: 1));

            if (adaDrafted)
            {
                frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(OrderModel.DraftedAspect), 1));
                if (adaOrderCell >= 0)
                    frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(OrderModel.OrderCellAspect), adaOrderCell));
            }
            if (boDrafted) frame.AddPawnAspect(new PawnAspect(Bo, AspectKey.Of(OrderModel.DraftedAspect), 1));
            return frame;
        }

        static readonly PawnId[] Everyone = { Ada, Bo, Hog };

        [Test]
        public void TheSpellingsAreTheSimulationsOwn()
        {
            // Held to the literals the simulation mints them from; DraftTests holds the other side.
            Assert.That(OrderModel.DraftedAspect, Is.EqualTo("odyssey.pawn.drafted"));
            Assert.That(OrderModel.OrderCellAspect, Is.EqualTo("odyssey.pawn.order.cell"));
        }

        [Test]
        public void TheKeyDraftsEveryUndraftedColonistAndPassesOverTheAnimal()
        {
            var sent = new List<Intent>();
            OrderModel.ToggleDraft(Everyone, Frame(false, false), sent);

            Assert.That(sent.Count, Is.EqualTo(2));
            foreach (Intent intent in sent)
            {
                Assert.That(intent.Kind, Is.EqualTo(IntentKind.SetDrafted));
                Assert.That(intent.B, Is.EqualTo(1));
                Assert.That(intent.A, Is.Not.EqualTo(Hog.Value), "an animal was drafted");
            }
        }

        [Test]
        public void AMixedSelectionIsDraftedNotReleased()
        {
            var sent = new List<Intent>();
            OrderModel.ToggleDraft(Everyone, Frame(adaDrafted: true, boDrafted: false), sent);

            Assert.That(sent.Count, Is.EqualTo(1), "Ada is already drafted and must be left alone");
            Assert.That(sent[0].A, Is.EqualTo(Bo.Value));
            Assert.That(sent[0].B, Is.EqualTo(1));
        }

        [Test]
        public void AFullyDraftedSelectionIsReleased()
        {
            var sent = new List<Intent>();
            OrderModel.ToggleDraft(Everyone, Frame(true, true), sent);

            Assert.That(sent.Count, Is.EqualTo(2));
            Assert.That(sent.TrueForAll(i => i.B == 0), Is.True);
        }

        [Test]
        public void ARightClickOnTheGroundMovesOnlyTheDrafted()
        {
            var sent = new List<Intent>();
            OrderModel.RightClick(Everyone, Frame(adaDrafted: true, boDrafted: false), Ground, PawnId.None, false, sent, new List<ContextMenuRow>());

            Assert.That(sent.Count, Is.EqualTo(1));
            Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.OrderMove));
            Assert.That(sent[0].A, Is.EqualTo(Ada.Value));
            Assert.That(sent[0].Cell, Is.EqualTo(Ground));
        }

        /// <summary>
        /// A colonist's hit box covers most of the cell behind her at the play camera, so a click
        /// that touched a pawn is still a move in C1 — the review found every order just behind
        /// the squad doing nothing. The sky is still nothing.
        /// </summary>
        [Test]
        public void ARightClickThatTouchesAPawnStillMovesAndTheSkyMovesNobody()
        {
            var sent = new List<Intent>();
            OrderModel.RightClick(Everyone, Frame(true, false), Ground, Bo, false, sent, new List<ContextMenuRow>());
            Assert.That(sent.Count, Is.EqualTo(1), "a click near the squad was swallowed");
            Assert.That(sent[0].Cell, Is.EqualTo(Ground));

            sent.Clear();
            OrderModel.RightClick(Everyone, Frame(true, true), null, PawnId.None, false, sent, new List<ContextMenuRow>());
            Assert.That(sent, Is.Empty);
        }

        [Test]
        public void ARightClickIsOnlyAnOrderWhenSomebodyIsDrafted()
        {
            Assert.That(OrderModel.AnyDrafted(Everyone, Frame(false, false)), Is.False);
            Assert.That(OrderModel.AnyDrafted(Everyone, Frame(false, true)), Is.True);
        }

        [Test]
        public void TheDraftedAreCollectedWithTheirOrdersInOneWalk()
        {
            var marks = new List<OrderModel.DraftedMark>();
            OrderModel.CollectDrafted(Frame(adaDrafted: true, boDrafted: true, adaOrderCell: 777), marks);

            Assert.That(marks.Count, Is.EqualTo(2));
            Assert.That(marks[0].Pawn, Is.EqualTo(Ada));
            Assert.That(marks[0].OrderCell, Is.EqualTo(777));
            Assert.That(marks[1].Pawn, Is.EqualTo(Bo));
            Assert.That(marks[1].OrderCell, Is.EqualTo(-1));
        }

        /// <summary>
        /// An undrafted colonist sent for a weapon from the context menu publishes an order cell
        /// with no drafted row before it (design 33 §7a); she is collected for her order line, in
        /// the same walk as the drafted. A drafted colonist's order cell belongs to her drafted
        /// mark and is not collected twice.
        /// </summary>
        [Test]
        public void AnUndraftedColonistsOrderIsCollectedForItsLineAndADraftedOneIsNot()
        {
            WorldSnapshot frame = Frame(adaDrafted: true, boDrafted: false, adaOrderCell: 777);
            frame.AddPawnAspect(new PawnAspect(Bo, AspectKey.Of(OrderModel.OrderCellAspect), 555));

            var marks = new List<OrderModel.DraftedMark>();
            var lines = new List<OrderModel.DraftedMark>();
            OrderModel.CollectDrafted(frame, marks, lines);
            Assert.That(lines.Count, Is.EqualTo(1), "the drafted order was collected twice, or Bo's not at all");
            Assert.That(lines[0].Pawn, Is.EqualTo(Bo));
            Assert.That(lines[0].OrderCell, Is.EqualTo(555));

            Assert.That(marks.Count, Is.EqualTo(1), "an undrafted colonist was given the draft's diamond");
            Assert.That(marks[0].Pawn, Is.EqualTo(Ada));
            Assert.That(marks[0].OrderCell, Is.EqualTo(777));
        }

        [Test]
        public void ThePanesDraftButtonIsLiveAndChangesFace()
        {
            var model = new InspectModel();
            model.SetColonist(Ada);

            model.Refresh(Frame(false, false));
            InspectCommand draft = model.Commands.Find(c => c.IconKey == InspectModel.DraftKey);
            Assert.That(draft.IconKey, Is.EqualTo(InspectModel.DraftKey));
            Assert.That(draft.Enabled, Is.True);
            Assert.That(draft.Label, Is.EqualTo(Registry.Label(InspectModel.DraftKey)));
            Assert.That(model.Drafted, Is.False);

            model.Refresh(Frame(true, false));
            InspectCommand undraft = model.Commands.Find(c => c.IconKey == InspectModel.UndraftKey);
            Assert.That(undraft.IconKey, Is.EqualTo(InspectModel.UndraftKey), "the button still says Draft");
            Assert.That(model.Drafted, Is.True);
        }

        [Test]
        public void TheDraftKeyIsTAndClashesWithNothing()
        {
            var hotkeys = new HotkeyDirector();
            Assert.That(hotkeys.Key(HotkeyAction.Draft, 0), Is.EqualTo(HudKey.T));
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 0), Is.EqualTo(HudKey.R), "R stays slice-up (owner)");
            Assert.That(Registry.Labels, Does.ContainKey(HotkeyDirector.KeyOf(HotkeyAction.Draft)));
        }
    }
}
