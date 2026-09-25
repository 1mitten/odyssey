#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The body's two alerts (design 43 §11, §15): a colonist waiting on a doctor with an injury
    /// nobody has tended gets a row that goes to her — Danger with the hours while it bleeds,
    /// Warning while she lies downed — and "No medicine" stands while somebody needs tending and
    /// there are no medical supplies on the board.
    /// </summary>
    public class HealthAlertTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2);

        static WorldSnapshot Board(int injuries, int tended, int hours, bool medkit, bool downed = false)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Wait,
                flags: PawnFlags.Person | (downed ? PawnFlags.Downed : 0)));
            frame.AddPawn(new PawnView(Bo, new CellRef(2, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            if (injuries > 0)
            {
                frame.AddPawnAspect(new PawnAspect(Ada, HealthAspectNames.InjuriesKey, injuries));
                frame.AddPawnAspect(new PawnAspect(Ada, HealthAspectNames.TendedKey, tended));
                if (hours > 0) frame.AddPawnAspect(new PawnAspect(Ada, HealthAspectNames.BleedHoursKey, hours));
            }
            if (medkit) frame.AddThing(new ThingView(new ThingId(9), new CellRef(3, 1, 1), ItemHandle.MedicalSupplies, 0, 5));
            return frame;
        }

        [Test]
        public void ABleedingColonistGetsADangerRowWithTheHours()
        {
            var alerts = new AlertModel();
            alerts.Refresh(Board(injuries: 1, tended: 0, hours: 14, medkit: true), 0.0);
            AlertRow[] rows = alerts.Rows.Where(r => r.Key == AlertModel.InjuredKey).ToArray();
            Assert.That(rows.Length, Is.EqualTo(1));
            Assert.That(rows[0].Pawn, Is.EqualTo(Ada), "a click must go to her");
            Assert.That(rows[0].Severity, Is.EqualTo(AlertSeverity.Danger));
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.NoMedicineKey), Is.False, "the control: a medkit lies on the board");
        }

        [Test]
        public void ADownedColonistsBruiseIsAWarningAndATendClearsIt()
        {
            var alerts = new AlertModel();
            alerts.Refresh(Board(injuries: 1, tended: 0, hours: 0, medkit: true, downed: true), 0.0);
            Assert.That(alerts.Rows.Single(r => r.Key == AlertModel.InjuredKey).Severity, Is.EqualTo(AlertSeverity.Warning));

            alerts.Refresh(Board(injuries: 1, tended: 1, hours: 0, medkit: true, downed: true), 0.0);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.InjuredKey), Is.False, "the row outlived the tend");
        }

        /// <summary>
        /// A bruise on a colonist on her feet waits for bed rest (design 37 §4): the doctor's round
        /// never walks to her, so a row saying she needs tending would stand until she slept.
        /// </summary>
        [Test]
        public void ABruiseOnAColonistOnHerFeetIsNotNews()
        {
            var alerts = new AlertModel();
            alerts.Refresh(Board(injuries: 1, tended: 0, hours: 0, medkit: true), 0.0);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.InjuredKey), Is.False);
        }

        [Test]
        public void NoMedicineStandsOnlyWhileSomebodyNeedsIt()
        {
            var alerts = new AlertModel();
            alerts.Refresh(Board(injuries: 1, tended: 0, hours: 5, medkit: false), 0.0);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.NoMedicineKey), Is.True);

            alerts.Refresh(Board(injuries: 0, tended: 0, hours: 0, medkit: false), 0.0);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.NoMedicineKey), Is.False, "the control: nobody hurt, no medicine is not news");
        }

        [Test]
        public void TheAlertsAreNamedInTheRegistry()
        {
            Assert.That(AlertModel.IconKeys, Has.Member(AlertModel.InjuredKey).And.Member(AlertModel.NoMedicineKey));
            Assert.That(Registry.Label(AlertModel.InjuredKey), Is.Not.EqualTo(AlertModel.InjuredKey));
            Assert.That(Registry.Label(AlertModel.NoMedicineKey), Is.Not.EqualTo(AlertModel.NoMedicineKey));
        }
    }
}
