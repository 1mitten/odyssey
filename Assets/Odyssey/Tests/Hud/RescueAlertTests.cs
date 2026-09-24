#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// "No bed for the wounded" (design 33 §11d): a downed colonist the simulation says has no free
    /// bed to be carried to gets a row of her own that goes to her, and it clears when a bed does.
    /// </summary>
    public class RescueAlertTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2);

        static WorldSnapshot Board(bool noBed)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Downed,
                flags: PawnFlags.Person | PawnFlags.Downed));
            frame.AddPawn(new PawnView(Bo, new CellRef(2, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            if (noBed) frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.RescueNoBedKey, 1));
            return frame;
        }

        [Test]
        public void ADownedColonistWithNoBedGetsARowThatGoesToHer()
        {
            var alerts = new AlertModel();
            alerts.Refresh(Board(noBed: true), 0.0);

            AlertRow[] rows = alerts.Rows.Where(r => r.Key == AlertModel.NoRescueBedKey).ToArray();
            Assert.That(rows.Length, Is.EqualTo(1), "one row for the one colonist with nowhere to go");
            Assert.That(rows[0].Pawn, Is.EqualTo(Ada), "a click on it must go to her");
            Assert.That(rows[0].Severity, Is.EqualTo(AlertSeverity.Danger));

            alerts.Refresh(Board(noBed: false), 0.0);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.NoRescueBedKey), Is.False, "the row outlived a free bed");
        }

        [Test]
        public void TheAlertIsNamedInTheRegistry()
        {
            Assert.That(Registry.Label(AlertModel.NoRescueBedKey), Is.EqualTo("No bed for the wounded"));
            Assert.That(AlertModel.IconKeys, Has.Member(AlertModel.NoRescueBedKey));
        }
    }
}
