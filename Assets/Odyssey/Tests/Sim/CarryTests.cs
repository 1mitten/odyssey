#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the simulation says about a load in a colonist's arms. Design 24.
    ///
    /// <para>The whole of the carrying work is presentation, bar this: the two aspects that tell
    /// anybody the load exists at all, and the gesture an interrupted haul now reports. Both are
    /// testable here and neither is testable by looking, which is the split this file keeps.</para>
    ///
    /// <para><b>A carried thing has no cell.</b> <c>ColonyItems.PickUp</c> sets it to -1 and
    /// delists it, so it is gone from the things the snapshot publishes — that is the whole
    /// mechanism behind the owner's report that it "disappears and they walk off", and it is why
    /// an aspect is the only channel left.</para>
    /// </summary>
    public class CarryTests
    {
        static bool InLiftToil(Pawn pawn) =>
            pawn.Driver is HaulJobDriver driver && driver.ToilIndex == 1;

        /// <summary>
        /// The names presentation mints for itself must be the names the simulation publishes
        /// under.
        ///
        /// <para>Two assemblies that cannot see each other agree by spelling and by nothing else
        /// — <c>Odyssey.Hud</c> may not reference <c>Odyssey.Sim</c>, which is the point of the
        /// aspect seam and is what stops a shared constants file. A test on each side holding the
        /// literal is the whole of the guarantee, so this one is load-bearing rather than
        /// ceremonial: change the string here and the load silently stops being drawn.</para>
        /// </summary>
        [Test]
        public void TheCarryAspectsAreSpeltTheWayPresentationSpellsThem()
        {
            Assert.That(CarryAspects.Carrying,
                Is.EqualTo(AspectKey.Of("odyssey.pawn.carrying")));
            Assert.That(CarryAspects.Stack,
                Is.EqualTo(AspectKey.Of("odyssey.pawn.carrying.stack")));
        }

        [Test]
        public void AnEmptyHandedColonistPublishesNoLoad()
        {
            // Absence is the answer, which is what makes the aspect sparse and free: most
            // colonists are holding nothing most of the time, and none of them costs a row.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            colony.World.Tick();

            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.TryGetPawnAspect(pawn.Id, CarryAspects.Carrying, out _), Is.False);
            Assert.That(frame.TryGetPawnAspect(pawn.Id, CarryAspects.Stack, out _), Is.False);
        }

        /// <summary>
        /// The load appears in the published frame on the tick the colonist takes hold of it, and
        /// not before.
        ///
        /// <para>Both ends of this are faults and they are different ones. Publish early and the
        /// load is drawn in the hands of a colonist who is still bending towards it — the
        /// magic-acquisition the stoop exists to prevent, in a new costume. Publish late and the
        /// pile has already gone from the floor while the arms are empty, which is the same
        /// fault the other way round.</para>
        /// </summary>
        [Test]
        public void TheLoadIsPublishedFromTheGraspAndNotBefore()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            colony.Stockpile(1, colony.Cell(12, 12, 0));

            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            for (int i = 0; i < 3_000 && !InLiftToil(pawn); i++) colony.World.Tick();
            Assert.That(InLiftToil(pawn), Is.True, "the hauler never reached the thing");

            int grasp = colony.Ctx.Content.LiftGraspTicks;

            colony.World.Tick(grasp - 1);
            Assert.That(
                colony.World.Views.Current.TryGetPawnAspect(pawn.Id, CarryAspects.Carrying, out _),
                Is.False, "the load was in her arms while she was still bending for it");

            colony.World.Tick(1);
            WorldSnapshot frame = colony.World.Views.Current;

            Assert.That(frame.TryGetPawnAspect(pawn.Id, CarryAspects.Carrying, out int def), Is.True,
                "she took the thing up and nothing said so");
            Assert.That(def, Is.EqualTo(ItemIndex.Salvage));
            Assert.That(frame.TryGetPawnAspect(pawn.Id, CarryAspects.Stack, out int stack), Is.True);
            Assert.That(stack, Is.GreaterThan(0));
        }

        /// <summary>
        /// The load is never in two places: it is on the floor or it is in the arms, and the
        /// frame that shows it in one shows it in neither of the others.
        ///
        /// <para>The single fault a viewer would actually notice, and the one that no amount of
        /// getting the pose right protects against. It is stated across the whole handover rather
        /// than at the instant, because an off-by-one either way is exactly the shape of the
        /// mistake — a pile that lingers a tick under a colonist already walking away, or a
        /// commodity that briefly exists nowhere.</para>
        /// </summary>
        [Test]
        public void TheLoadIsNeverInTwoPlacesAndNeverInNone()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int store = colony.Cell(12, 12, 0);
            colony.Stockpile(1, store);

            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            for (int tick = 0; tick < 3_000; tick++)
            {
                colony.World.Tick();

                var item = colony.Ctx.Items.Get(scrap);
                if (item == null || item.Despawned) break;

                WorldSnapshot frame = colony.World.Views.Current;

                bool onTheFloor = false;
                var things = frame.Things;
                for (int i = 0; i < things.Length; i++)
                    if (things[i].Id == scrap) { onTheFloor = true; break; }

                bool inTheArms =
                    frame.TryGetPawnAspect(pawn.Id, CarryAspects.Carrying, out _);

                Assert.That(onTheFloor && inTheArms, Is.False,
                    $"tick {tick}: the same salvage is lying on the ground and held at once");
                Assert.That(onTheFloor || inTheArms, Is.True,
                    $"tick {tick}: the salvage exists and nothing in the frame says where");

                if (item.Cell == store) break;
            }
        }

        /// <summary>
        /// A haul interrupted mid-carry reports the stow, and until 2026-09-19 it deliberately did
        /// not.
        ///
        /// <para>The old silence was right while nothing was drawn — an abandoning drop really is
        /// a different motion from setting something down. It is wrong the moment the load is
        /// visible, because the alternative to a motion is not "no motion", it is a commodity
        /// teleporting out of a colonist's arms. Design 24 §6d records that the distinction is
        /// deferred rather than abandoned, and that the second gesture belongs here when there is
        /// one to draw.</para>
        /// </summary>
        [Test]
        public void AJobThatEndsMidCarryReportsPuttingTheLoadDown()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            colony.Stockpile(1, colony.Cell(12, 12, 0));

            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            for (int i = 0; i < 3_000 && pawn.CurrentJob?.CarriedItem != scrap.Value; i++)
                colony.World.Tick();
            Assert.That(pawn.CurrentJob!.CarriedItem, Is.EqualTo(scrap.Value),
                "the hauler never picked the thing up");

            byte before = pawn.GestureSerial;
            pawn.Driver!.Cleanup(colony.Ctx, JobStatus.Failed);

            Assert.That(pawn.CurrentJob.CarriedItem, Is.EqualTo(-1));
            Assert.That(pawn.Gesture, Is.EqualTo(PawnGesture.Stow),
                "the load left her arms with nothing drawn");
            Assert.That(pawn.GestureSerial, Is.Not.EqualTo(before),
                "the serial did not move, so presentation cannot tell this stow from the last");
            Assert.That(colony.Ctx.Items.Get(scrap)!.Cell, Is.GreaterThanOrEqualTo(0),
                "the abandoned load was deleted rather than put down");
        }
    }
}
