#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A guest on the interface side (design 57 §5, T2): the published Visitor flag keeps a trader
    /// off every surface that means "one of ours", dresses it as a trader, and gives it a guest's
    /// pane — no tab strip, no needs, the kind's word under its name.
    /// </summary>
    public class VisitorViewTests
    {
        static readonly PawnId Guest = new PawnId(40);
        const PawnFlags TraderFlags = PawnFlags.Person | PawnFlags.Visitor;

        [Test]
        public void AVisitorIsNotAColonist()
        {
            var view = new PawnView(Guest, new CellRef(2, 2, 1), 800, 800, 600, kind: PawnKindLabels.Trader, flags: TraderFlags);
            Assert.That(view.IsVisitor, Is.True);
            Assert.That(view.IsColonist, Is.False);
            Assert.That(view.IsHostile, Is.False);
            Assert.That(view.IsPerson, Is.True);
        }

        [Test]
        public void APersonWithNoVisitorFlagIsStillAColonist()
        {
            var view = new PawnView(Guest, new CellRef(2, 2, 1), 800, 800, 600, kind: 0, flags: PawnFlags.Person);
            Assert.That(view.IsColonist, Is.True, "the control");
            Assert.That(view.IsVisitor, Is.False);
        }

        [Test]
        public void AVisitorDressesAsATrader()
        {
            Assert.That(PawnOutfits.For(TraderFlags), Is.EqualTo(PawnOutfit.Trader));
            Assert.That(PawnOutfits.For(PawnFlags.Person), Is.EqualTo(PawnOutfit.Issued), "the control");
            Assert.That(PawnOutfits.For(PawnFlags.Person | PawnFlags.Hostile), Is.EqualTo(PawnOutfit.Bandit));
        }

        [Test]
        public void ATraderPaneIsAGuestsShape()
        {
            var frame = new WorldSnapshot();
            frame.AddPawn(new PawnView(Guest, new CellRef(2, 2, 1), 800, 800, 600, jobDef: JobHandle.Wait,
                kind: PawnKindLabels.Trader, flags: TraderFlags));
            var inspect = new InspectModel();
            inspect.SetColonist(Guest);
            inspect.Refresh(frame);

            Assert.That(inspect.IsVisitor, Is.True);
            Assert.That(inspect.ShowsTabBox, Is.False, "a guest has no tabs");
            Assert.That(inspect.ShowsColonistBody, Is.False, "a guest has no needs to show");
            Assert.That(inspect.ShowsFace, Is.True, "a guest is a person, with a face");
            Assert.That(inspect.Subtitle, Is.EqualTo(PawnKindLabels.Label(PawnKindLabels.Trader).ToLowerInvariant()));
        }

        [Test]
        public void TheTraderKindIsNamedInTheRegistry()
        {
            Assert.That(PawnKindLabels.IconKeys[PawnKindLabels.Trader], Is.EqualTo("ui.pawn.trader"));
            Assert.That(Registry.Label("ui.pawn.trader"), Is.Not.EqualTo("ui.pawn.trader"));
        }
    }
}
