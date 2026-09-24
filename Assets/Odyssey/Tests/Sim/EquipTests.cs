#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.WeaponFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <c>OrderEquip</c> and <c>Job_Equip</c> (design 33 §5j, §6D): walk to the weapon, stoop for
    /// it, take it into the hand and put down whatever was there. A fetch and not a fight, so it
    /// is taken drafted or not; refused for anybody who is not a standing colonist of ours and for
    /// anything that is not a weapon lying where it may be taken.
    /// </summary>
    public class EquipTests
    {
        /// <summary>Tick until the equip job is over, one way or the other.</summary>
        static void RunTheJob(ColonyWorld colony, Pawn pawn)
        {
            Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Equip), "the order started no equip job");
            Assert.That(RunUntil(colony, () => pawn.CurrentJob?.DefIndex != JobIndex.Equip, 3_000), Is.True,
                "the equip job never ended");
        }

        /// <summary>
        /// The order walks, lifts and holds — for a colonist drafted or not (§5j: a fetch, not a
        /// fight). She stands where the weapon lay when she takes it; it leaves the ground and is
        /// in her hand, with no cell, carried by her; the stoop is reported; and the job is counted
        /// as done rather than failed. A drafted colonist goes back to the hold afterwards.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void TheOrderWalksLiftsAndHolds(bool drafted)
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            if (drafted)
                Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, colonist.Id.Value, 1)),
                    Is.EqualTo(IntentRejection.None));

            ColonyItem machete = PutDown(colony, colonist, ItemIndex.Machete);
            int lay = machete.Cell;
            int from = colonist.Cell;
            Assume.That(from, Is.Not.EqualTo(lay));
            int done = colony.Jobs.CompletedOf(JobIndex.Equip);
            int lifts = 0;
            byte serial = colonist.GestureSerial;

            Assert.That(Equip(colony, colonist, machete), Is.EqualTo(IntentRejection.None));
            Assert.That(colonist.EquippedItem, Is.Zero, "in the hand before she had walked there");
            Assert.That(RunUntil(colony, () =>
            {
                if (colonist.GestureSerial != serial)
                {
                    serial = colonist.GestureSerial;
                    if (colonist.Gesture == PawnGesture.Lift) lifts++;
                }
                return colonist.CurrentJob?.DefIndex != JobIndex.Equip;
            }, 3_000), Is.True, "the equip job never ended");

            Assert.That(colonist.EquippedItem, Is.EqualTo(machete.Id.Value));
            Assert.That(colonist.Cell, Is.EqualTo(lay), "she did not walk to it");
            Assert.That(machete.Cell, Is.EqualTo(-1), "a held weapon keeps a cell");
            Assert.That(machete.CarriedBy, Is.EqualTo(colonist.Id.Value));
            Assert.That(machete.Despawned, Is.False);
            Assert.That(colony.Pawns.Items.ItemAt(lay), Is.Null, "the weapon is still on the ground");
            Assert.That(lifts, Is.EqualTo(1), "the stoop was not reported once");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Equip), Is.EqualTo(done + 1));
            Assert.That(colony.Pawns.WeaponRules.ArmamentOf(colonist, colony.Pawns).ItemDef, Is.EqualTo(ItemIndex.Machete));
            if (drafted)
            {
                Assert.That(colonist.Drafted, Is.True);
                Assert.That(colonist.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold));
            }
        }

        /// <summary>
        /// One hand: a second weapon puts the first down where she stands. The first is on the
        /// ground again, belongs to nobody, and can be taken up by somebody else.
        /// </summary>
        [Test]
        public void ASecondWeaponPutsTheFirstDown()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0], other = ctx.Pawns.All[1];
            colony.World.Tick(30);

            ColonyItem bat = PutDown(colony, colonist, ItemIndex.Bat);
            Assert.That(Equip(colony, colonist, bat), Is.EqualTo(IntentRejection.None));
            RunTheJob(colony, colonist);
            Assume.That(colonist.EquippedItem, Is.EqualTo(bat.Id.Value));

            ColonyItem blade = PutDown(colony, colonist, ItemIndex.ArcBlade, away: 5);
            Assert.That(Equip(colony, colonist, blade), Is.EqualTo(IntentRejection.None));
            RunTheJob(colony, colonist);

            Assert.That(colonist.EquippedItem, Is.EqualTo(blade.Id.Value));
            Assert.That(blade.CarriedBy, Is.EqualTo(colonist.Id.Value));
            Assert.That(bat.Despawned, Is.False, "the first weapon was lost");
            Assert.That(bat.CarriedBy, Is.Zero, "the first weapon is still in a hand");
            Assert.That(bat.Cell, Is.GreaterThanOrEqualTo(0), "the first weapon is nowhere");
            Assert.That(ctx.Items.ItemAt(bat.Cell), Is.SameAs(bat));
            Assert.That(ColonyItems.Distance(bat.Cell, colonist.Cell, Size, 100), Is.LessThanOrEqualTo(141),
                "the first weapon was put down away from her");
            Assert.That(ctx.WeaponRules.CanEquip(other, bat, ctx), Is.True, "nobody else may take it up");
        }

        /// <summary>
        /// The order line (design 33 §7a): a colonist sent for a weapon publishes its cell under the
        /// draft's order cell while she walks, drafted or not, and stops once it is in her hand —
        /// the board draws the drafted move's line to it. Undrafted there is no drafted row, so the
        /// order cell stands alone; drafted it follows the drafted row, as a move's does.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void TheWeaponsCellIsPublishedAsTheOrderCellWhileSheFetchesIt(bool drafted)
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            if (drafted)
                Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, colonist.Id.Value, 1)),
                    Is.EqualTo(IntentRejection.None));

            WorldSnapshot before = colony.World.Views.Current;
            Assert.That(before.TryGetPawnAspect(colonist.Id, CombatAspects.OrderCell, out _), Is.False,
                "an order cell before any order");

            ColonyItem machete = PutDown(colony, colonist, ItemIndex.Machete);
            int lay = machete.Cell;
            Assert.That(Equip(colony, colonist, machete), Is.EqualTo(IntentRejection.None));

            WorldSnapshot walking = colony.World.Views.Current;
            Assert.That(walking.TryGetPawnAspect(colonist.Id, CombatAspects.OrderCell, out int cell), Is.True,
                "the fetch published no order cell");
            Assert.That(cell, Is.EqualTo(lay));
            Assert.That(walking.TryGetPawnAspect(colonist.Id, CombatAspects.Drafted, out _), Is.EqualTo(drafted));

            RunTheJob(colony, colonist);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(colonist.Id, CombatAspects.OrderCell, out _),
                Is.False, "the order cell outlived the fetch");
        }

        /// <summary>
        /// Refused, and no fetch started: each case against the control of the same colonist being
        /// given a bat she may take.
        /// </summary>
        [Test]
        public void RefusedForAForbiddenThingAndForAThingThatIsNotAWeapon()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);

            ColonyItem bat = PutDown(colony, colonist, ItemIndex.Bat);
            bat.Forbidden = true;
            Assert.That(Equip(colony, colonist, bat), Is.EqualTo(IntentRejection.NotPermitted), "forbidden");
            Assert.That(colonist.CurrentJob?.DefIndex ?? -1, Is.Not.EqualTo(JobIndex.Equip));

            ColonyItem wood = PutDown(colony, colonist, ItemIndex.Wood, away: 9);
            Assert.That(Equip(colony, colonist, wood), Is.EqualTo(IntentRejection.NotPermitted), "not a weapon");
            Assert.That(colonist.CurrentJob?.DefIndex ?? -1, Is.Not.EqualTo(JobIndex.Equip));

            Assert.That(Send(colony, new Intent(IntentKind.OrderEquip, Size.FromIndex(bat.Cell), colonist.Id.Value, 9_999)),
                Is.EqualTo(IntentRejection.NotPermitted), "a thing that does not exist");

            bat.Forbidden = false;
            Assert.That(Equip(colony, colonist, bat), Is.EqualTo(IntentRejection.None), "the control");
        }

        [Test]
        public void RefusedForAnAnimalAHostileAPawnThatDoesNotExistAndADownedColonist()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0], other = ctx.Pawns.All[1];
            colony.World.Tick(30);
            ColonyItem bat = PutDown(colony, colonist, ItemIndex.Bat);

            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            Assert.That(Equip(colony, hog, bat), Is.EqualTo(IntentRejection.NotPermitted), "an animal");
            Assert.That(Equip(colony, bandit, bat), Is.EqualTo(IntentRejection.NotPermitted), "a hostile");
            Assert.That(Send(colony, new Intent(IntentKind.OrderEquip, Size.FromIndex(bat.Cell), 9_999, bat.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "nobody");

            colonist.Downed = true;
            Assert.That(Equip(colony, colonist, bat), Is.EqualTo(IntentRejection.NotPermitted), "downed");
            Assert.That(bat.CarriedBy, Is.Zero);

            Assert.That(Equip(colony, other, bat), Is.EqualTo(IntentRejection.None), "the control: a standing colonist");
        }

        /// <summary>The weapon she already holds is <c>AlreadyInThatState</c>, not a walk to herself.</summary>
        [Test]
        public void TheWeaponAlreadyInTheHandIsANoOp()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            colony.World.Tick(30);
            ColonyItem bat = PutDown(colony, colonist, ItemIndex.Bat);
            WeaponHand.TakeUp(colonist, bat, ctx);

            Assert.That(Send(colony, new Intent(IntentKind.OrderEquip, Size.FromIndex(colonist.Cell), colonist.Id.Value, bat.Id.Value)),
                Is.EqualTo(IntentRejection.AlreadyInThatState));
        }

        /// <summary>
        /// A weapon somebody else is already fetching is theirs: the claim is taken with the job,
        /// so a second colonist sent for the same weapon is refused rather than both walking there.
        /// </summary>
        [Test]
        public void TwoColonistsCannotBeSentForOneWeapon()
        {
            var colony = Board();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            colony.World.Tick(30);
            ColonyItem bat = PutDown(colony, a, ItemIndex.Bat, away: 12);

            Assert.That(Equip(colony, a, bat), Is.EqualTo(IntentRejection.None));
            Assert.That(Equip(colony, b, bat), Is.EqualTo(IntentRejection.NotPermitted));
        }

        /// <summary>
        /// A held weapon is invisible to every scan that looks for things to fetch — it has no cell
        /// — so a colony at work leaves it in her hand. Two thousand ticks of a colony hauling,
        /// eating and sleeping around her, and it is still hers.
        /// </summary>
        [Test]
        public void AHeldWeaponStaysInTheHandWhileTheColonyWorks()
        {
            var colony = Board(colonists: 3);
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            colony.World.Tick(30);
            ColonyItem bat = PutDown(colony, colonist, ItemIndex.Bat);
            Assert.That(Equip(colony, colonist, bat), Is.EqualTo(IntentRejection.None));
            RunTheJob(colony, colonist);

            colony.World.Tick(2_000);
            Assert.That(colonist.EquippedItem, Is.EqualTo(bat.Id.Value));
            Assert.That(bat.CarriedBy, Is.EqualTo(colonist.Id.Value));
            Assert.That(bat.Cell, Is.EqualTo(-1));
        }

        /// <summary>
        /// The save round trip with a weapon in the hand: the hand, the item's carrier and its
        /// missing cell all come back, the armament is still the weapon, and the two worlds agree
        /// on the hash at the load and six hundred ticks on.
        /// </summary>
        [Test]
        public void AWeaponInTheHandSurvivesTheRoundTrip()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            colony.World.Tick(30);
            ColonyItem crowbar = PutDown(colony, colonist, ItemIndex.Crowbar);
            Assert.That(Equip(colony, colonist, crowbar), Is.EqualTo(IntentRejection.None));
            RunTheJob(colony, colonist);
            Assume.That(colonist.EquippedItem, Is.EqualTo(crowbar.Id.Value));

            var restored = Board();
            restored.Load(colony.Save());
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)), "the hash disagrees at the load");

            Pawn back = restored.Pawns.Pawns.Get(colonist.Id)!;
            ColonyItem held = restored.Pawns.Items.Get(crowbar.Id)!;
            Assert.That(back.EquippedItem, Is.EqualTo(crowbar.Id.Value));
            Assert.That(held.Cell, Is.EqualTo(-1));
            Assert.That(held.CarriedBy, Is.EqualTo(colonist.Id.Value));
            Assert.That(restored.Pawns.WeaponRules.ArmamentOf(back, restored.Pawns).ItemDef, Is.EqualTo(ItemIndex.Crowbar));

            colony.World.Tick(600);
            restored.World.Tick(600);
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)), "the hash disagrees 600 ticks on");
        }

        /// <summary>
        /// A save taken while she is still walking to the weapon resumes the same fetch: the
        /// claim, the job and the walk come back, and the two worlds end the job on the same tick
        /// with the same hash.
        /// </summary>
        [Test]
        public void AFetchSavedOnTheWayResumesTheSameFetch()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            ColonyItem bat = PutDown(colony, colonist, ItemIndex.Bat, away: 12);
            Assert.That(Equip(colony, colonist, bat), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(40);
            Assume.That(colonist.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Equip));

            var restored = Board();
            restored.Load(colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(colonist.Id)!;
            Assert.That(back.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Equip));

            RunTheJob(colony, colonist);
            RunTheJob(restored, back);
            Assert.That(restored.World.CurrentTick, Is.EqualTo(colony.World.CurrentTick));
            Assert.That(back.EquippedItem, Is.EqualTo(bat.Id.Value));
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)));
        }
    }
}
