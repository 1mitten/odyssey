#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Health tab's model (design 43 §10): two columns of seven on the Skills tab's grid, whole
    /// when nothing is published, the bleed and the tend on the right, a clicked region's injuries
    /// in place of the right column, and no string rebuilt while nothing moved.
    /// </summary>
    public class HealthTabTests
    {
        static readonly PawnId Ada = new PawnId(1);
        const int Pool = 100_000, LegLeft = 4, Wound = 0, Bruise = 1;

        static WorldSnapshot Board()
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 600, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpMaxKey, Pool));
            return frame;
        }

        /// <summary>The bleeding state of the brief: two wounds on the left leg and a bruise on the torso, nothing tended.</summary>
        static WorldSnapshot Bleeding(int care = 0, int tended = 0)
        {
            WorldSnapshot frame = Board();
            void Aspect(AspectKey key, int value) => frame.AddPawnAspect(new PawnAspect(Ada, key, value));
            Aspect(CombatAspectNames.HpKey, 72_000);
            for (int r = 0; r < 6; r++) Aspect(HealthAspectNames.RegionKeys[r], r == LegLeft ? 367 : r == 1 ? 775 : 1000);
            Aspect(HealthAspectNames.PainKey, 350);
            Aspect(HealthAspectNames.ConsciousnessKey, 889);
            Aspect(HealthAspectNames.MovingKey, 608);
            Aspect(HealthAspectNames.ManipulationKey, 889);
            Aspect(HealthAspectNames.BloodKey, 220);
            Aspect(HealthAspectNames.BleedHoursKey, 14);
            Aspect(HealthAspectNames.InjuriesKey, 2);
            Aspect(HealthAspectNames.TendedKey, tended);
            Aspect(HealthAspectNames.InjuryKeys[LegLeft * 3 + Wound], 19_000);
            Aspect(HealthAspectNames.CareKeys[LegLeft * 3 + Wound], care);
            Aspect(HealthAspectNames.InjuryKeys[1 * 3 + Bruise], 9_000);
            Aspect(HealthAspectNames.CareKeys[1 * 3 + Bruise], care);
            return frame;
        }

        static InspectModel Pane(WorldSnapshot frame)
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(frame);
            return pane;
        }

        [Test]
        public void TheAspectNamesAreSpelledAsTheSimulationPublishesThem()
        {
            // Held to the literals Odyssey.Sim.Pawns.HealthAspects publishes;
            // HealthContractTests holds the other side.
            Assert.That(HealthAspectNames.Pain, Is.EqualTo("odyssey.pawn.health.pain"));
            Assert.That(HealthAspectNames.BleedHours, Is.EqualTo("odyssey.pawn.health.bleed.hours"));
            Assert.That(HealthAspectNames.Region(4), Is.EqualTo("odyssey.pawn.health.region.4"));
            Assert.That(HealthAspectNames.Injury(4, 0), Is.EqualTo("odyssey.pawn.health.injury.4.0"));
            Assert.That(HealthAspectNames.Care(4, 0), Is.EqualTo("odyssey.pawn.health.injury.4.0.care"));
            Assert.That(HealthAspectNames.TendedKey, Is.EqualTo(AspectKey.Of("odyssey.pawn.health.tended")));
        }

        [Test]
        public void AWholeColonistReadsWholeAndTheColumnsFitTheSkillsGrid()
        {
            HealthTab tab = Pane(Board()).Health;
            Assert.That(tab.Left.Length, Is.EqualTo(7).And.EqualTo(tab.Right.Length), "seven rows a column, the Skills tab's grid");
            for (int r = 0; r < 6; r++)
            {
                Assert.That(tab.Left[r].Name, Is.EqualTo(Registry.Label(HealthTab.RegionKeys[r])));
                Assert.That(tab.Left[r].Bar, Is.EqualTo(1000), "a whole colonist's regions are full");
                Assert.That(tab.Left[r].Mark, Is.EqualTo(HealthMark.None));
            }
            Assert.That(tab.Left[6].Value, Is.EqualTo("0"), "no pain");
            Assert.That(tab.Right[0].Value, Is.EqualTo("100"));
            Assert.That(tab.Right[5].Value, Is.Empty, "nothing bleeds");
            Assert.That(tab.Right[6].Value, Is.Empty, "nothing to tend");
        }

        [Test]
        public void TheBleedingStateSaysWhereWhatAndHowLong()
        {
            InspectModel pane = Pane(Bleeding());
            HealthTab tab = pane.Health;
            Assert.That(tab.Left[LegLeft].Mark, Is.EqualTo(HealthMark.Bleeding));
            Assert.That(tab.Left[LegLeft].Value, Is.EqualTo("37"));
            Assert.That(tab.Left[LegLeft].Ink, Is.EqualTo(CombatFeedbackModel.HealthBad));
            Assert.That(tab.Left[1].Mark, Is.EqualTo(HealthMark.None), "the control: a bruise does not bleed");
            Assert.That(tab.Left[6].Value, Is.EqualTo("35"));
            Assert.That(tab.Right[0].Value, Is.EqualTo("72"));
            Assert.That(tab.Right[4].Value, Is.EqualTo("22"));
            Assert.That(tab.Right[4].Ink, Is.EqualTo(CombatFeedbackModel.HealthWarn), "22 per cent lost is past the first line");
            Assert.That(tab.Right[5].Value, Is.EqualTo("14h"));
            Assert.That(tab.Right[5].Mark, Is.EqualTo(HealthMark.Bleeding));
            Assert.That(tab.Right[6].Value, Is.EqualTo("0/2"));
            Assert.That(pane.HealthCondition, Is.EqualTo(Registry.Label("ui.combat.hurt")), "the condition moved to the header line");
        }

        [Test]
        public void ARegionClickedListsItsInjuriesAndASecondClickPutsItBack()
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            WorldSnapshot frame = Bleeding();
            pane.Refresh(frame);
            pane.Health.Select(LegLeft);
            pane.Refresh(frame);
            HealthTab tab = pane.Health;

            Assert.That(tab.Left[LegLeft].Selected, Is.True);
            Assert.That(tab.Right[0].Name, Is.EqualTo(Registry.Label("ui.health.wound")));
            Assert.That(tab.Right[0].Value, Is.EqualTo("19"), "two wounds on one region are one record");
            Assert.That(tab.Right[0].Mark, Is.EqualTo(HealthMark.Bleeding));
            Assert.That(tab.Right[1].Empty, Is.True, "the torso's bruise is not the leg's");

            pane.Health.Select(LegLeft);
            pane.Refresh(frame);
            Assert.That(pane.Health.Right[0].Name, Is.EqualTo(Registry.Label("ui.combat.health")), "a second click did not put the column back");

            pane.Health.Select(LegLeft);
            pane.SetColonist(new PawnId(9));
            Assert.That(pane.Health.SelectedRegion, Is.EqualTo(-1), "another colonist kept the last one's selection");
        }

        [Test]
        public void ATendedRegionWearsTheTendAndTheCountSaysSo()
        {
            HealthTab tab = Pane(Bleeding(care: 701, tended: 2)).Health;
            Assert.That(tab.Left[LegLeft].Mark, Is.EqualTo(HealthMark.Tended));
            Assert.That(tab.Right[6].Value, Is.EqualTo("2/2"));
            Assert.That(tab.Right[6].Mark, Is.EqualTo(HealthMark.Tended));
        }

        [Test]
        public void NothingIsRebuiltWhileNothingMoved()
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            WorldSnapshot frame = Bleeding();
            pane.Refresh(frame);
            string before = pane.Health.Right[5].Value;
            pane.Refresh(frame);
            Assert.That(pane.Health.Right[5].Value, Is.SameAs(before), "a refresh that moved nothing rebuilt the strings");
        }
    }
}
