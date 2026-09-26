#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The right-click's Tend (design 43 §11): offered on a colonist under the pointer with an
    /// injury nobody has tended, sending the first standing colonist of the selection; disabled
    /// when every one is down; absent for the whole, the tended and a selection of none.
    /// </summary>
    public class TendMenuTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2);

        static WorldSnapshot Board(int injuries, int tended, bool doctorDown = false)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Wait,
                flags: PawnFlags.Person | (doctorDown ? PawnFlags.Downed : PawnFlags.None)));
            frame.AddPawn(new PawnView(Bo, new CellRef(3, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            if (injuries > 0)
            {
                frame.AddPawnAspect(new PawnAspect(Bo, HealthAspectNames.InjuriesKey, injuries));
                frame.AddPawnAspect(new PawnAspect(Bo, HealthAspectNames.TendedKey, tended));
            }
            return frame;
        }

        static List<ContextMenuRow> Menu(WorldSnapshot frame, params PawnId[] selection)
        {
            var rows = new List<ContextMenuRow>();
            ContextMenuModel.Build(selection, frame, new CellRef(3, 1, 1), Bo, rows);
            return rows;
        }

        [Test]
        public void AHurtColonistUnderThePointerIsOfferedATend()
        {
            List<ContextMenuRow> rows = Menu(Board(injuries: 2, tended: 1), Ada);
            ContextMenuRow tend = rows.Single(r => r.Key == ContextMenuModel.TendKey);
            Assert.That(tend.Enabled, Is.True);
            Assert.That(tend.Label, Is.EqualTo(Registry.Label(ContextMenuModel.TendKey)));
            var sent = new List<Intent>();
            ContextMenuModel.Choose(tend, sent);
            Assert.That(sent.Single().Kind, Is.EqualTo(IntentKind.OrderTend));
            Assert.That(sent.Single().A, Is.EqualTo(Ada.Value), "the doctor");
            Assert.That(sent.Single().B, Is.EqualTo(Bo.Value), "the patient");
        }

        [Test]
        public void TheWholeAndTheTendedAreOfferedNothing()
        {
            Assert.That(Menu(Board(injuries: 0, tended: 0), Ada).Any(r => r.Key == ContextMenuModel.TendKey), Is.False);
            Assert.That(Menu(Board(injuries: 2, tended: 2), Ada).Any(r => r.Key == ContextMenuModel.TendKey), Is.False);
            Assert.That(Menu(Board(injuries: 2, tended: 0)).Any(r => r.Key == ContextMenuModel.TendKey), Is.False,
                "nobody selected, nobody to send");
            Assert.That(Menu(Board(injuries: 2, tended: 0), Bo).Any(r => r.Key == ContextMenuModel.TendKey), Is.False,
                "she is not sent to herself");
        }

        [Test]
        public void ADownedSelectionGetsTheRowDisabled()
        {
            ContextMenuRow tend = Menu(Board(injuries: 1, tended: 0, doctorDown: true), Ada).Single(r => r.Key == ContextMenuModel.TendKey);
            Assert.That(tend.Enabled, Is.False);
            Assert.That(tend.Reason, Is.EqualTo(Registry.Label(ContextMenuModel.DownedReasonKey)));
        }
    }
}
