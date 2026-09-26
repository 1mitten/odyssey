#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The right-click on a downed enemy (design 59 §7, the owner's ruling): a menu of Capture then
    /// Finish off, where it used to be an instant attack that killed. Nothing is sent until a row is
    /// chosen; Capture sends the primary colonist, drafted or not; Finish off is the old attack, dim
    /// with its reason when nobody selected is drafted; a downed prisoner is offered Capture alone.
    /// </summary>
    public class CaptureMenuTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Raider = new PawnId(4), Held = new PawnId(6);

        static WorldSnapshot Board(params PawnId[] drafted) => Board(stray: true, drafted);

        /// <param name="stray">Whether the downed prisoner lies out of her bed, to be carried back.</param>
        static WorldSnapshot Board(bool stray, params PawnId[] drafted)
        {
            WorldSnapshot frame = Frame.Write(layers: 4);
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Bo, new CellRef(7, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Raider, new CellRef(8, 8, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile | PawnFlags.Downed));
            frame.AddPawn(new PawnView(Held, new CellRef(9, 9, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Downed, custody: PawnCustody.Prisoner));
            if (stray) frame.AddPawnAspect(new PawnAspect(Held, PrisonAspectNames.StrayKey, 1));
            AspectKey key = AspectKey.Of(OrderModel.DraftedAspect);
            foreach (PawnId pawn in drafted) frame.AddPawnAspect(new PawnAspect(pawn, key, 1));
            return frame;
        }

        static (List<Intent> Sent, List<ContextMenuRow> Menu) RightClick(IReadOnlyList<PawnId> selection,
            WorldSnapshot frame, PawnId under)
        {
            var sent = new List<Intent>();
            var menu = new List<ContextMenuRow>();
            OrderModel.RightClick(selection, frame, new CellRef(8, 8, 1), under, false, sent, menu);
            return (sent, menu);
        }

        [Test]
        public void ADownedEnemyOpensTheMenuAndIsNotAttacked()
        {
            var (sent, menu) = RightClick(new[] { Ada }, Board(Ada), Raider);
            Assert.That(sent, Is.Empty, "no instant attack: nothing is sent until a row is chosen");
            Assert.That(menu.Select(r => r.Key), Is.EqualTo(new[]
                { ContextMenuModel.CaptureKey, ContextMenuModel.FinishOffKey, ContextMenuModel.CancelKey }));
        }

        [Test]
        public void CaptureSendsThePrimaryColonistDraftedOrNot()
        {
            var (_, menu) = RightClick(new[] { Ada, Bo }, Board(), Raider);
            ContextMenuRow capture = menu.First(r => r.Key == ContextMenuModel.CaptureKey);
            Assert.That(capture.Enabled, Is.True);
            var sent = new List<Intent>();
            ContextMenuModel.Choose(capture, sent);
            Assert.That(sent.Single().Kind, Is.EqualTo(IntentKind.OrderCapture));
            Assert.That(sent.Single().A, Is.EqualTo(Ada.Value));
            Assert.That(sent.Single().B, Is.EqualTo(Raider.Value));
        }

        [Test]
        public void FinishOffIsTheAttackFromTheDraftedAndDimWithoutThem()
        {
            var (_, drafted) = RightClick(new[] { Ada, Bo }, Board(Ada, Bo), Raider);
            ContextMenuRow finish = drafted.First(r => r.Key == ContextMenuModel.FinishOffKey);
            Assert.That(finish.Enabled, Is.True);
            var sent = new List<Intent>();
            ContextMenuModel.Choose(finish, sent);
            Assert.That(sent.Select(i => i.Kind), Is.All.EqualTo(IntentKind.OrderAttack));
            Assert.That(sent.Select(i => i.A), Is.EquivalentTo(new[] { Ada.Value, Bo.Value }));

            var (_, undrafted) = RightClick(new[] { Ada }, Board(), Raider);
            ContextMenuRow dim = undrafted.First(r => r.Key == ContextMenuModel.FinishOffKey);
            Assert.That(dim.Enabled, Is.False);
            Assert.That(dim.Reason, Is.EqualTo(Registry.Label(ContextMenuModel.NeedsDraftKey)));
        }

        [Test]
        public void ADownedPrisonerIsOfferedCaptureAlone()
        {
            var (sent, menu) = RightClick(new[] { Ada }, Board(Ada), Held);
            Assert.That(sent, Is.Empty);
            Assert.That(menu.Select(r => r.Key), Is.EqualTo(new[] { ContextMenuModel.CaptureKey, ContextMenuModel.CancelKey }),
                "a prisoner is brought back, never finished off from a menu");
        }

        /// <summary>
        /// Design 59 §16 H3. A prisoner lying in her own prison bed is not to be carried anywhere:
        /// the simulation refused the Capture the menu offered, in silence, and a drafted
        /// right-click on her cell opened the menu instead of moving there.
        /// </summary>
        [Test]
        public void APrisonerInHerBedIsOfferedNothingAndTheClickIsAMove()
        {
            var (sent, menu) = RightClick(new[] { Ada }, Board(stray: false, Ada), Held);
            Assert.That(menu, Is.Empty, "offered a capture the simulation would refuse");
            Assert.That(sent.Select(i => i.Kind), Has.Member(IntentKind.OrderMove), "the right-click did not move her");
        }

        [Test]
        public void AStandingEnemyIsStillAnInstantAttack()
        {
            WorldSnapshot frame = Frame.Write(layers: 4);
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Raider, new CellRef(8, 8, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile));
            frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(OrderModel.DraftedAspect), 1));
            var (sent, menu) = RightClick(new[] { Ada }, frame, Raider);
            Assert.That(sent.Single().Kind, Is.EqualTo(IntentKind.OrderAttack), "one sensible order, one click");
            Assert.That(menu, Is.Empty);
        }

        [Test]
        public void APrisonerInTheJumpsuitWearsThePrisonColoursOverHerOwnFace()
        {
            var pools = new ColonistCastPools(
                maleBodies: new[] { 3, 7 }, femaleBodies: new[] { 4, 8 },
                maleHair: new[] { 2, 5 }, femaleHair: new[] { 1, 6 }, beards: new[] { 30 },
                uniformMale: 40, uniformFemale: 41,
                banditMale: new[] { 50 }, banditFemale: new[] { 53 }, headgear: new[] { 2 });
            for (int i = 1; i <= 50; i++)
            {
                ColonistAppearance person = ColonistAppearance.Of(7u, i, pools, 'n', 40);
                ColonistAppearance held = ColonistAppearance.Of(7u, i, pools, 'n', 40, PawnOutfit.Prisoner);
                Assert.That(held.Skin, Is.EqualTo(person.Skin));
                Assert.That(held.HairPiece, Is.EqualTo(person.HairPiece));
                Assert.That(held.BeardPiece, Is.EqualTo(person.BeardPiece));
                Assert.That(held.Look, Is.EqualTo(person.Look), "the issued cut");
                Assert.That(held.Cloth, Is.EqualTo(ColonistAppearance.PrisonCloth));
                Assert.That(held.Outfit, Is.EqualTo(PawnOutfit.Prisoner));
            }
        }
    }
}
