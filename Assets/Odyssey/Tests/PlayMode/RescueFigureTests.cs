#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Rescue, drawn (design 33 §11e), under the real bootstrap: a downed colonist lies on the floor,
    /// is lifted into her carrier's arms — her head at arm height, beside the carrier — and is laid
    /// in the bed at mattress height. Measured off the figure's own head bone, which is what a
    /// viewer sees; the floor is the control that the other two readings are not the same pose.
    /// </summary>
    public class RescueFigureTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        [UnityTest]
        public IEnumerator APatientIsCarriedInTheArmsAndLaidInTheBed()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
#if UNITY_EDITOR
                if (boot.moduleCatalogue == null)
                    boot.moduleCatalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
                for (int i = 0; i < 8; i++) yield return null;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True, "Start built no world");
                for (int i = 0; i < 20; i++) yield return null;

                var colony = boot.Colony!;
                var world = boot.World!;
                var figures = boot.Figures;
                if (figures == null || !figures.CanDrawColonists)
                    Assert.Ignore("no colonist art on this machine: the pose is the figures' and there are none");
                if (colony.Pawns.Items.Beds.Count == 0 || colony.Pawns.Pawns.Count < 2)
                    Assert.Ignore("the start has no bed or too few colonists to rescue anybody");

                Pawn rescuer = colony.Pawns.Pawns.All[0], patient = colony.Pawns.Pawns.All[1];
                foreach (Pawn pawn in colony.Pawns.Pawns.All) pawn.WorkPriorities[WorkTypeIndex.Rescue] = 0;
                var at = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y);
                world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, at, 3));

                colony.Pawns.Combat!.ApplySwing(rescuer, patient, new Armament(colony.Pawns.Content.Combat.fists),
                    new SwingOutcome(CombatEventKind.Hit, patient.HpMilli + 1_000), world.CurrentTick);
                world.Tick();
                Assume.That(patient.Downed, Is.True, "the blow did not down her");

                // On the floor: the control.
                for (int i = 0; i < 90; i++) yield return null;
                float floorHead = HeadAboveGround(figures, patient, colony);
                Debug.Log($"[Rescue] downed on the floor: head {floorHead:0.00} m above the ground");
                Assert.That(floorHead, Is.LessThan(0.5f), "the control: a downed colonist's head is not on the floor");

                world.Intents.Submit(new Intent(IntentKind.SetDrafted, at, rescuer.Id.Value, 1));
                world.Tick();
                world.Intents.Submit(new Intent(IntentKind.OrderRescue, colony.Pawns.Size.FromIndex(patient.Cell),
                    rescuer.Id.Value, patient.Id.Value));
                world.Tick();
                Assert.That(rescuer.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Rescue), "the order was refused");

                float until = Time.realtimeSinceStartup + 60f;
                while (patient.CarriedBy == 0 && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(patient.CarriedBy, Is.EqualTo(rescuer.Id.Value), "she was never lifted");
                for (int i = 0; i < 20; i++) yield return null;

                float carriedHead = HeadAboveGround(figures, patient, colony);
                Assert.That(figures.TryGetHead(patient.Id, out Vector3 head), Is.True);
                Assert.That(figures.TryGetFeet(rescuer.Id, out Vector3 carrierFeet), Is.True);
                float fromCarrier = Vector2.Distance(new Vector2(head.x, head.z), new Vector2(carrierFeet.x, carrierFeet.z));
                Debug.Log($"[Rescue] carried: head {carriedHead:0.00} m above the ground, {fromCarrier:0.00} m from the carrier");
                Assert.That(carriedHead, Is.InRange(0.6f, 1.5f), "not lying in the arms at arm height");
                Assert.That(fromCarrier, Is.LessThan(1.2f), "not in the carrier's arms");

                until = Time.realtimeSinceStartup + 60f;
                while (rescuer.CurrentJob?.DefIndex == JobIndex.Rescue && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(colony.Pawns.Items.Beds, Has.Member(patient.Cell), "she was not laid in a bed");
                for (int i = 0; i < 30; i++) yield return null;

                float bedHead = HeadAboveGround(figures, patient, colony);
                Debug.Log($"[Rescue] in bed: head {bedHead:0.00} m above the ground");
                Assert.That(bedHead, Is.InRange(0.25f, 1.0f), "not lying on the mattress");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>How high the figure's head bone is over the drawn ground of the cell she is in.</summary>
        static float HeadAboveGround(Odyssey.Presentation.World.PawnFigureDirector figures, Pawn pawn, ColonyWorld colony)
        {
            Assert.That(figures.TryGetHead(pawn.Id, out Vector3 head), Is.True, "she has no figure with a head");
            Vector3 ground = GroundRelief.Lift(CellMetrics.FloorCentre(colony.Pawns.Size.FromIndex(pawn.Cell)));
            return head.y - ground.y;
        }
    }
}
