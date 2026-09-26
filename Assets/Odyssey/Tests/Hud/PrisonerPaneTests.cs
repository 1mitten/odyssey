#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A prisoner's pane (design 58 §11b): her mode, how willing she is, and in Recruit mode how
    /// long until she joins and what is slowing it — every number read from the aspects the
    /// simulation's own arithmetic published, and the mode row the control that moves it on.
    /// </summary>
    public class PrisonerPaneTests
    {
        static readonly PawnId Held = new PawnId(6);

        static WorldSnapshot Board(PrisonMode mode, int willing, int hours = -1, int blockers = 0, bool shackled = false)
        {
            WorldSnapshot frame = Frame.Write(layers: 4);
            frame.AddPawn(new PawnView(Held, new CellRef(9, 9, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person, custody: PawnCustody.Prisoner));
            frame.AddPawnAspect(new PawnAspect(Held, PrisonAspectNames.ModeKey, (int)mode));
            frame.AddPawnAspect(new PawnAspect(Held, PrisonAspectNames.WillingKey, willing));
            if (mode == PrisonMode.Recruit)
            {
                frame.AddPawnAspect(new PawnAspect(Held, PrisonAspectNames.HoursKey, hours));
                frame.AddPawnAspect(new PawnAspect(Held, PrisonAspectNames.BlockersKey, blockers));
            }
            if (shackled) frame.AddPawnAspect(new PawnAspect(Held, PrisonAspectNames.ShackledKey, 1));
            return frame;
        }

        static InspectModel Pane(WorldSnapshot frame)
        {
            var model = new InspectModel();
            model.SetColonist(Held);
            model.Refresh(frame);
            return model;
        }

        [Test]
        public void AHeldPrisonerShowsHerModeAndWillingness()
        {
            InspectModel pane = Pane(Board(PrisonMode.Hold, 370));
            Assert.That(pane.IsPrisoner, Is.True);
            Assert.That(pane.CellRows.Select(r => r.Name), Is.EqualTo(new[] { InspectModel.PrisonModeRow, InspectModel.WillingRow, InspectModel.EscapeRow }));
            Assert.That(pane.CellRows[0].Value, Is.EqualTo(Registry.Label("ui.prisoner.hold")));
            Assert.That(pane.CellRows[1].Value, Is.EqualTo("37%"));
            Assert.That(pane.PrisonerMode, Is.EqualTo(PrisonMode.Hold));
        }

        [Test]
        public void RecruitModeSaysWhenAndWhatIsSlowingIt()
        {
            InspectModel pane = Pane(Board(PrisonMode.Recruit, 100, hours: 54,
                blockers: PrisonAspectNames.Blocker.Hungry | PrisonAspectNames.Blocker.LowMood, shackled: true));
            Assert.That(pane.CellRows.Select(r => r.Name), Is.EqualTo(new[]
            {
                InspectModel.PrisonModeRow, InspectModel.WillingRow, InspectModel.JoinsInRow,
                InspectModel.SlowedByRow, InspectModel.ShackledRow, InspectModel.EscapeRow,
            }));
            Assert.That(pane.CellRows[2].Value, Is.EqualTo("2 days 6 h"));
            Assert.That(pane.CellRows[3].Value, Is.EqualTo("hungry, low mood"));
        }

        [Test]
        public void NobodyToTalkToHerIsSaid()
        {
            InspectModel pane = Pane(Board(PrisonMode.Recruit, 0, hours: -1, blockers: PrisonAspectNames.Blocker.NoWarden));
            Assert.That(pane.CellRows[2].Value, Is.EqualTo("nobody to talk to her"));
            Assert.That(pane.CellRows[3].Value, Is.EqualTo("no warden"));
        }

        [Test]
        public void TheRowsFollowTheNumbers()
        {
            var model = new InspectModel();
            model.SetColonist(Held);
            model.Refresh(Board(PrisonMode.Recruit, 100, hours: 30));
            model.Refresh(Board(PrisonMode.Hold, 120));
            Assert.That(model.CellRows.Count, Is.EqualTo(3), "back to Hold: the recruit rows go");
            Assert.That(model.CellRows[1].Value, Is.EqualTo("12%"));
        }

        [Test]
        public void TheModeCyclesThroughTheFourAndNeverOffersRansom()
        {
            PrisonMode mode = PrisonMode.Hold;
            var seen = new System.Collections.Generic.List<PrisonMode>();
            for (int i = 0; i < 4; i++) { seen.Add(mode); mode = InspectModel.NextPrisonMode(mode); }
            Assert.That(seen, Is.EqualTo(new[] { PrisonMode.Hold, PrisonMode.Recruit, PrisonMode.Release, PrisonMode.Exile }));
            Assert.That(mode, Is.EqualTo(PrisonMode.Hold), "and round again");
        }

        [Test]
        public void EscapeRiskIsShownWithItsReasons()
        {
            WorldSnapshot frame = Board(PrisonMode.Hold, 0);
            frame.AddPawnAspect(new PawnAspect(Held, PrisonAspectNames.EscapeKey, 63_000));
            frame.AddPawnAspect(new PawnAspect(Held, PrisonAspectNames.EscapeWhyKey,
                PrisonAspectNames.Reason.Unhappy | PrisonAspectNames.Reason.Unwatched | PrisonAspectNames.Reason.WellKept));
            InspectModel pane = Pane(frame);
            InspectRow risk = pane.CellRows.First(r => r.Name == InspectModel.EscapeRow);
            Assert.That(risk.Value, Is.EqualTo("6.3% a day"));
            Assert.That(risk.Tint, Is.Not.Null, "high enough to be drawn amber");
            Assert.That(pane.CellRows.First(r => r.Name == InspectModel.EscapeWhyRow).Value,
                Is.EqualTo("unhappy, unwatched, well kept"));
        }

        [Test]
        public void AnEscapeeSaysSoAndNothingElse()
        {
            WorldSnapshot frame = Frame.Write(layers: 4);
            frame.AddPawn(new PawnView(Held, new CellRef(9, 9, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person, custody: PawnCustody.Escaping));
            InspectModel pane = Pane(frame);
            Assert.That(pane.IsPrisoner, Is.True);
            Assert.That(pane.CellRows.Select(r => r.Name), Is.EqualTo(new[] { InspectModel.EscapingRow }));
        }

        [Test]
        public void AColonistsPaneOffersArrestAndAPrisonersDoesNot()
        {
            WorldSnapshot frame = Frame.Write(layers: 4);
            var ada = new PawnId(1);
            frame.AddPawn(new PawnView(ada, new CellRef(4, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            var model = new InspectModel();
            model.SetColonist(ada);
            model.Refresh(frame);
            Assert.That(model.Commands.Any(c => c.IconKey == InspectModel.ArrestKey && c.Enabled), Is.True);

            InspectModel held = Pane(Board(PrisonMode.Hold, 0));
            Assert.That(held.Commands.Any(c => c.IconKey == InspectModel.ArrestKey), Is.False);
        }

        [TestCase(0, "any moment")]
        [TestCase(1, "1 hour")]
        [TestCase(6, "6 hours")]
        [TestCase(24, "1 day")]
        [TestCase(78, "3 days 6 h")]
        public void HoursReadAsAPersonSaysThem(int hours, string words) =>
            Assert.That(InspectModel.Hours(hours), Is.EqualTo(words));
    }
}
