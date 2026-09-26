#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
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
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True, "Start built no world");
                for (int i = 0; i < 20; i++) yield return null;

                var colony = boot.Colony!;
                var world = boot.World!;
                var figures = boot.Figures;
                if (figures == null || !figures.CanDrawColonists)
                    Assert.Ignore("no colonist art on this machine: the pose is the figures' and there are none");
                if (colony.Pawns.Pawns.Count < 2)
                    Assert.Ignore("the start has too few colonists to rescue anybody");
                // The start kit has no bed: one is raised a few cells off, so the rescue walks.
                int bed = RaiseABedNearTheStart(colony);
                Assert.That(bed, Is.GreaterThanOrEqualTo(0), "no room for a bed near the start");
                world.Tick();
                for (int i = 0; i < 10; i++) yield return null;

                Pawn rescuer = colony.Pawns.Pawns.All[0], patient = colony.Pawns.Pawns.All[1];
                float standingHead;
                foreach (Pawn pawn in colony.Pawns.Pawns.All) pawn.WorkPriorities[WorkTypeIndex.Rescue] = 0;
                var at = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y);
                // Speed one: at three, a bed four cells off is reached inside the two seconds the
                // sample waits, and the "carried" reading was her head on the pillow (2026-09-24).
                world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, at, 1));

                colony.Pawns.Combat!.ApplySwing(rescuer, patient, new Armament(colony.Pawns.Content.Combat.fists),
                    new SwingOutcome(CombatEventKind.Hit, patient.HpMilli + 1_000), world.CurrentTick);
                world.Tick();
                Assume.That(patient.Downed, Is.True, "the blow did not down her");

                // On the floor: the control. Real seconds, not frames: a batch frame is a couple of
                // milliseconds and the fall is a clip (2026-09-24: 90 frames read her mid-fall).
                standingHead = HeadAboveGround(figures, rescuer, colony);
                yield return new WaitForSecondsRealtime(3f);
                float floorHead = HeadAboveGround(figures, patient, colony);
                Debug.Log($"[Rescue] downed on the floor: head {floorHead:0.00} m above the ground " +
                          $"(a colonist standing: {standingHead:0.00})");
                Assert.That(floorHead, Is.LessThan(standingHead * 0.3f), "the control: a downed colonist's head is not on the floor");

                world.Intents.Submit(new Intent(IntentKind.SetDrafted, at, rescuer.Id.Value, 1));
                world.Tick();
                world.Intents.Submit(new Intent(IntentKind.OrderRescue, colony.Pawns.Size.FromIndex(patient.Cell),
                    rescuer.Id.Value, patient.Id.Value));
                world.Tick();
                Assert.That(rescuer.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Rescue), "the order was refused");

                float until = Time.realtimeSinceStartup + 60f;
                while (patient.CarriedBy == 0 && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(patient.CarriedBy, Is.EqualTo(rescuer.Id.Value), "she was never lifted");
                // Past the lift's gesture and the scoop's ease: one second read 1.27 m and 1.79 m on
                // two runs, the difference being how far the arms had come up.
                yield return new WaitForSecondsRealtime(2f);
                Assert.That(patient.CarriedBy, Is.EqualTo(rescuer.Id.Value), "the carry was over before it was sampled");
                Assert.That(rescuer.HasPath, Is.True, "the carrier had stopped walking before the sample");

                // Measured against the carrier's own feet, not the ground of the cell: a carrier
                // climbing a terrace ramp is drawn up the riser before her cell changes, and against
                // the cell the same pose read anything from 1.27 m to 2.93 m (2026-09-24).
                Assert.That(figures.TryGetHead(patient.Id, out Vector3 head), Is.True);
                Assert.That(figures.TryGetFeet(rescuer.Id, out Vector3 carrierFeet), Is.True);
                float carriedHead = head.y - carrierFeet.y;
                float fromCarrier = Vector2.Distance(new Vector2(head.x, head.z), new Vector2(carrierFeet.x, carrierFeet.z));
                Debug.Log($"[Rescue] carried: head {carriedHead:0.00} m above the carrier's feet, {fromCarrier:0.00} m from them");
                // In the arms: the body rests on the carrier's palms, which the load's scoop holds at
                // the chest, so her head is near the carrier's shoulder — measured 1.8 of 2.15 m
                // (2026-09-24, design 33 §11e). Well off the floor and below a standing head.
                Assert.That(carriedHead, Is.InRange(standingHead * 0.45f, standingHead * 0.9f),
                    "not lying in the arms");
                // Across the arms, head to one side: half a body from the carrier, not on top of her
                // and not a body's length away.
                Assert.That(fromCarrier, Is.InRange(0.4f, 1.6f), "not lying across the carrier's arms");

                until = Time.realtimeSinceStartup + 60f;
                while (rescuer.CurrentJob?.DefIndex == JobIndex.Rescue && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(colony.Pawns.Items.Beds, Has.Member(patient.Cell), "she was not laid in a bed");
                yield return new WaitForSecondsRealtime(1f);

                float bedHead = HeadAboveGround(figures, patient, colony);
                // On the mattress, the way a sleeper lies: above the mattress's top and less than a
                // body's thickness over it.
                Debug.Log($"[Rescue] in bed: head {bedHead:0.00} m above the ground, the mattress's top at {BedShape.MattressTop:0.00}");
                Assert.That(bedHead, Is.InRange(BedShape.MattressTop, BedShape.MattressTop + 0.6f), "not lying on the mattress");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Order and raise a bed on the first pair of cells a few cells from the start that will
        /// take one, and answer with its head cell — <c>BedOwnerPickerTests</c>' helper, from four
        /// cells out so the carry is a walk.
        /// </summary>
        static int RaiseABedNearTheStart(ColonyWorld colony)
        {
            GridSize size = colony.Grid.Size;
            CellRef start = colony.Start;

            for (int radius = 4; radius < 12; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;

                int head = size.Index(x, z, start.Y);
                if (!colony.Construction.Allows(head, BuildingHandle.Bed)) continue;

                int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, size);
                if (foot < 0 || !colony.Construction.Allows(foot, BuildingHandle.Bed)) continue;

                if (colony.Construction.Place(size.FromIndex(head), BuildingHandle.Bed,
                        StuffHandle.Wood, facing: 0) != IntentRejection.None) continue;

                colony.Construction.Raise(colony.Pawns, head, (byte)QualityHandle.Normal);
                return head;
            }

            return -1;
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
