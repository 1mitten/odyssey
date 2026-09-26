#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The kit (design 54, gear unit G3): a few small stacks a colonist keeps on her — two belt
    /// slots, one kind of thing a slot up to its <c>kitCap</c> — taken by order, laid down by order,
    /// and spent where she stands: a doctor treats from her own kit first, a colonist treating
    /// herself does too, a ration is eaten when nothing else can be reached, and <b>Use</b> spends
    /// one now. A kit thing is an ordinary thing carried by her with no cell, exactly as the hand's
    /// weapon is, so no search anywhere can see it.
    /// </summary>
    public class KitTests
    {
        const int Medical = ItemIndex.MedicalSupplies;
        const int Ration = ItemIndex.Meal;

        static int Cap(ColonyWorld colony, int def) => colony.Pawns.Content.Items[def].kitCap;

        static int Pool(Pawn pawn, int perMille) => pawn.HpMaxMilli * perMille / 1_000;

        static ColonyItem Lay(ColonyWorld colony, int def, int count, int dx = 5, int dz = 0)
        {
            int cell = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, Near(colony, dx, dz), def, count, maxRadius: 8);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0), "no room to lay it");
            return colony.Pawns.Items.Get(colony.Pawns.Items.Spawn(def, cell, count))!;
        }

        static IntentRejection Take(ColonyWorld colony, Pawn pawn, ColonyItem item)
        {
            int at = colony.Pawns.WhereIs(item);
            CellRef cell = at >= 0 ? Size.FromIndex(at) : Size.FromIndex(pawn.Cell);
            return Send(colony, new Intent(IntentKind.OrderTakeIntoKit, cell, pawn.Id.Value, item.Id.Value));
        }

        static IntentRejection KitDrop(ColonyWorld colony, Pawn pawn, int slot, bool leaveHere) =>
            Send(colony, new Intent(IntentKind.OrderKitDrop, Size.FromIndex(pawn.Cell), pawn.Id.Value, slot, leaveHere ? 1 : 0));

        static IntentRejection Use(ColonyWorld colony, Pawn pawn, int slot) =>
            Send(colony, new Intent(IntentKind.OrderUseKit, Size.FromIndex(pawn.Cell), pawn.Id.Value, slot));

        static bool RunUntil(ColonyWorld colony, System.Func<bool> done, int maxTicks)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                if (done()) return true;
                colony.World.Tick();
            }
            return done();
        }

        /// <summary>How many of <paramref name="def"/> she holds across her kit.</summary>
        static int InKit(ColonyWorld colony, Pawn pawn, int def)
        {
            int total = 0;
            for (int slot = 0; slot < Kit.Slots; slot++)
            {
                ColonyItem? held = Kit.Held(pawn, colony.Pawns, slot);
                if (held != null && held.DefIndex == def) total += held.Stack;
            }
            return total;
        }

        /// <summary>Put things straight into her kit, as the take job's grasp would.</summary>
        static void Stock(ColonyWorld colony, Pawn pawn, int def, int count)
        {
            ColonyItem pile = Lay(colony, def, count);
            ColonyItem taken = colony.Pawns.Items.SplitOff(pile, count, pawn.Id);
            Assume.That(Kit.Put(pawn, colony.Pawns, taken), Is.Zero, "the kit refused it");
        }

        static ColonyWorld Colony(int colonists = 2)
        {
            var colony = Board(colonists);
            colony.World.Tick(5);
            return colony;
        }

        // ---- the content ------------------------------------------------------------------

        [Test]
        public void OnlyMedicalSuppliesAndRationsFitAKit()
        {
            var colony = Colony();
            Assert.That(Cap(colony, Medical), Is.EqualTo(5), "the owner's five medical supplies");
            Assert.That(Cap(colony, Ration), Is.EqualTo(3), "the owner's three rations");
            var items = colony.Pawns.Content.Items;
            for (int def = 0; def < items.Length; def++)
                if (def != Medical && def != Ration)
                    Assert.That(items[def].kitCap, Is.Zero, $"{items[def].defName} fits a kit");
        }

        // ---- taking ------------------------------------------------------------------------

        /// <summary>
        /// She walks to the stack and lifts as many as fit — five of eight — and the rest stays where
        /// it lay. What she holds is carried by her with no cell, so it is nowhere on the board.
        /// </summary>
        [Test]
        public void SheTakesAsManyAsFitAndLeavesTheRest()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            ColonyItem pile = Lay(colony, Medical, 8);

            Assert.That(Take(colony, pawn, pile), Is.EqualTo(IntentRejection.None));
            Assert.That(RunUntil(colony, () => InKit(colony, pawn, Medical) > 0, 3_000), Is.True, "she never took any");
            colony.World.Tick(60);

            Assert.That(InKit(colony, pawn, Medical), Is.EqualTo(5), "one slot, up to its cap");
            Assert.That(pile.Despawned, Is.False);
            Assert.That(pile.Stack, Is.EqualTo(3), "the rest stays where it was");
            ColonyItem held = Kit.Held(pawn, colony.Pawns, 0)!;
            Assert.That(held.CarriedBy, Is.EqualTo(pawn.Id.Value));
            Assert.That(colony.Pawns.WhereIs(held), Is.EqualTo(-1), "a kit thing is on the board");
        }

        /// <summary>
        /// One slot's worth an order: a take tops up the slot of its kind and does not spill into
        /// the empty one. The second slot of the same thing takes a second order.
        /// </summary>
        [Test]
        public void ATakeTopsUpItsSlotAndASecondOrderOpensAnother()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Stock(colony, pawn, Medical, 2);
            ColonyItem pile = Lay(colony, Medical, 4);

            Assert.That(Take(colony, pawn, pile), Is.EqualTo(IntentRejection.None));
            Assert.That(RunUntil(colony, () => pile.Stack == 1, 3_000), Is.True, "she did not top the slot up");
            colony.World.Tick(60);
            Assert.That(Kit.Held(pawn, colony.Pawns, 0)!.Stack, Is.EqualTo(5));
            Assert.That(Kit.Held(pawn, colony.Pawns, 1), Is.Null, "one take filled two slots");

            Assert.That(Take(colony, pawn, pile), Is.EqualTo(IntentRejection.None));
            // The whole of a stack of one moves into the kit as that very thing (SplitOff returns it).
            Assert.That(RunUntil(colony, () => Kit.Held(pawn, colony.Pawns, 1) != null, 3_000), Is.True, "the second order took nothing");
            Assert.That(Kit.Held(pawn, colony.Pawns, 1)!.Stack, Is.EqualTo(1));
        }

        [Test]
        public void AFullKitAndAThingThatFitsNoKitAreRefused()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            ColonyItem wood = Lay(colony, ItemIndex.Wood, 10);
            ColonyItem bat = Lay(colony, ItemIndex.Bat, 1, dz: 2);
            Assert.That(Take(colony, pawn, wood), Is.EqualTo(IntentRejection.NotPermitted), "wood");
            Assert.That(Take(colony, pawn, bat), Is.EqualTo(IntentRejection.NotPermitted), "a weapon is the hand's");

            Stock(colony, pawn, Medical, 5);
            Stock(colony, pawn, Medical, 5);
            ColonyItem more = Lay(colony, Medical, 1, dz: -2);
            ColonyItem ration = Lay(colony, Ration, 1, dz: 3);
            Assert.That(Take(colony, pawn, more), Is.EqualTo(IntentRejection.NotPermitted), "two full slots");
            Assert.That(Take(colony, pawn, ration), Is.EqualTo(IntentRejection.NotPermitted), "no empty slot for a new kind");
        }

        /// <summary>
        /// Invisible to everybody else: the kit's supplies are the only ones on the board, and the
        /// other colonist cannot find any. Before they went into the kit she could — the control.
        /// </summary>
        [Test]
        public void NobodyElseCanSeeAKitThing()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            ColonyItem pile = Lay(colony, Medical, 2);
            Assert.That(Odyssey.Sim.Pawns.Medical.NearestSupplies(other, colony.Pawns), Is.SameAs(pile), "the control");

            ColonyItem taken = colony.Pawns.Items.SplitOff(pile, 2, pawn.Id);
            Kit.Put(pawn, colony.Pawns, taken);
            Assert.That(Odyssey.Sim.Pawns.Medical.NearestSupplies(other, colony.Pawns), Is.Null);
        }

        // ---- laying down -------------------------------------------------------------------

        [TestCase(false)]
        [TestCase(true)]
        public void RemoveAndDropLayItAtHerFeetAndOnlyDropForbidsIt(bool leaveHere)
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Stock(colony, pawn, Medical, 4);
            ColonyItem held = Kit.Held(pawn, colony.Pawns, 0)!;

            Assert.That(KitDrop(colony, pawn, 0, leaveHere), Is.EqualTo(IntentRejection.None));

            Assert.That(Kit.Held(pawn, colony.Pawns, 0), Is.Null, "the slot is still filled");
            Assert.That(pawn.KitItems[0], Is.Zero);
            Assert.That(held.CarriedBy, Is.Zero);
            Assert.That(held.Cell, Is.GreaterThanOrEqualTo(0));
            Assert.That(held.Stack, Is.EqualTo(4));
            Assert.That(held.Forbidden, Is.EqualTo(leaveHere));
            Assert.That(KitDrop(colony, pawn, 0, leaveHere), Is.EqualTo(IntentRejection.AlreadyInThatState), "an empty slot");
        }

        // ---- spending ----------------------------------------------------------------------

        /// <summary>
        /// Use, on supplies: she treats herself now, from the kit, with no walk. One comes out of
        /// the kit and the supplies in the store are untouched. Whole, the order is refused.
        /// </summary>
        [Test]
        public void UseTreatsHerselfFromTheKit()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Stock(colony, pawn, Medical, 3);
            ColonyItem store = Lay(colony, Medical, 4, dx: -4);
            Assert.That(Use(colony, pawn, 0), Is.EqualTo(IntentRejection.NotPermitted), "whole");

            pawn.HpMilli = Pool(pawn, 300);
            Assert.That(Use(colony, pawn, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(RunUntil(colony, () => pawn.TreatedUntilTick != 0, 12_000), Is.True, "she never treated herself");

            Assert.That(InKit(colony, pawn, Medical), Is.EqualTo(2), "one out of the kit");
            Assert.That(store.Stack, Is.EqualTo(4), "the store was used");
            Assert.That(pawn.HpMilli, Is.GreaterThan(Pool(pawn, 300)));
        }

        /// <summary>
        /// A doctor treats from her own kit before walking to the stores. The same colony with the
        /// kit empty spends the store's — the control.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ADoctorTreatsFromHerOwnKitFirst(bool stocked)
        {
            var colony = Colony();
            Pawn patient = colony.Pawns.Pawns.All[0], doctor = colony.Pawns.Pawns.All[1];
            patient.WorkPriorities[WorkTypeIndex.Doctor] = 0;
            ColonyItem store = Lay(colony, Medical, 4, dx: -6);
            if (stocked) Stock(colony, doctor, Medical, 3);
            Stand(colony, patient, Near(colony, 6, 6));
            Strike(colony, doctor, patient, patient.HpMilli);
            Assume.That(patient.Downed, Is.True);

            Assert.That(RunUntil(colony, () => patient.TreatedUntilTick != 0, 8_000), Is.True, "nobody treated her");

            if (stocked)
            {
                Assert.That(InKit(colony, doctor, Medical), Is.EqualTo(2), "not from her kit");
                Assert.That(store.Stack, Is.EqualTo(4), "she walked to the store anyway");
            }
            else
            {
                Assert.That(store.Despawned || store.Stack == 3, Is.True, "the control: the store's were used");
            }
        }

        /// <summary>
        /// Never lifted: a treatment from the kit cut off before it ends leaves the kit exactly as
        /// it was, where a unit taken from a store would have gone on the floor.
        /// </summary>
        [Test]
        public void AnInterruptedTreatmentLeavesTheKitWhole()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Stock(colony, pawn, Medical, 3);
            pawn.HpMilli = Pool(pawn, 300);
            Assert.That(Use(colony, pawn, 0), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(20);
            Assume.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Treat));

            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            colony.World.Tick();

            Assert.That(InKit(colony, pawn, Medical), Is.EqualTo(3));
            Assert.That(pawn.TreatedUntilTick, Is.Zero);
        }

        /// <summary>
        /// The ration is the fallback: with a meal she can reach she eats the meal and the kit keeps
        /// its ration; with none she eats from the kit.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ARationIsEatenOnlyWhenNothingElseCanBeReached(bool mealOnTheBoard)
        {
            var colony = Colony(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            // The starting kit's food, taken off the board so the only food is the test's.
            var items = colony.Pawns.Items.Items;
            for (int i = items.Count - 1; i >= 0; i--)
                if (!items[i].Despawned && colony.Pawns.Content.Items[items[i].DefIndex].nutrition > 0)
                    colony.Pawns.Items.Despawn(items[i]);
            Stock(colony, pawn, Ration, 2);
            ColonyItem? meal = mealOnTheBoard ? Lay(colony, Ration, 1, dx: -5) : null;
            int seek = colony.Pawns.Content.Needs[NeedIndex.Food].seekThreshold;
            pawn.Needs[NeedIndex.Food] = seek - 1;

            Assert.That(RunUntil(colony, () => pawn.Needs[NeedIndex.Food] >= seek, 4_000), Is.True, "she never ate");

            if (mealOnTheBoard)
            {
                Assert.That(meal!.Despawned, Is.True, "she ate from the kit with a meal in reach");
                Assert.That(InKit(colony, pawn, Ration), Is.EqualTo(2));
            }
            else
            {
                Assert.That(InKit(colony, pawn, Ration), Is.EqualTo(1), "one ration from the kit");
            }
        }

        [Test]
        public void UseEatsARationNowAndAFullColonistIsRefused()
        {
            var colony = Colony(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Stock(colony, pawn, Ration, 2);
            var need = colony.Pawns.Content.Needs[NeedIndex.Food];

            pawn.Needs[NeedIndex.Food] = need.max;
            Assert.That(Use(colony, pawn, 0), Is.EqualTo(IntentRejection.NotPermitted), "full");

            pawn.Needs[NeedIndex.Food] = need.max / 2;
            Assert.That(Use(colony, pawn, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(RunUntil(colony, () => InKit(colony, pawn, Ration) == 1, 2_000), Is.True, "she did not eat it");
            Assert.That(pawn.Needs[NeedIndex.Food], Is.GreaterThan(need.max / 2));
        }

        // ---- who may be ordered -------------------------------------------------------------

        [Test]
        public void OnlyAStandingColonistOfOursMayBeOrdered()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            Stock(colony, pawn, Medical, 2);
            ColonyItem pile = Lay(colony, Medical, 2, dz: 3);

            pawn.Downed = true;
            Assert.That(Take(colony, pawn, pile), Is.EqualTo(IntentRejection.NotPermitted), "downed, take");
            Assert.That(KitDrop(colony, pawn, 0, false), Is.EqualTo(IntentRejection.NotPermitted), "downed, drop");
            Assert.That(Use(colony, pawn, 0), Is.EqualTo(IntentRejection.NotPermitted), "downed, use");
            Assert.That(Send(colony, new Intent(IntentKind.OrderKitDrop, default, 9_999, 0, 0)),
                Is.EqualTo(IntentRejection.NotPermitted), "nobody");

            Assert.That(Take(colony, other, pile), Is.EqualTo(IntentRejection.None), "the control");
        }

        [Test]
        public void TheOrdersApplyWhilePaused()
        {
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.OrderTakeIntoKit), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.OrderKitDrop), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.OrderUseKit), Is.True);
        }

        // ---- death, save, hash, snapshot ----------------------------------------------------

        /// <summary>Until Strip (G7), the kit is laid beside the body with the weapon: nothing is lost.</summary>
        [Test]
        public void TheKitIsLaidBesideTheDead()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            Stock(colony, pawn, Medical, 4);
            ColonyItem held = Kit.Held(pawn, colony.Pawns, 0)!;

            Strike(colony, other, pawn, pawn.HpMilli + pawn.HpMaxMilli);
            colony.World.Tick(2);

            Assert.That(held.Despawned, Is.False, "the kit went with her");
            Assert.That(held.CarriedBy, Is.Zero);
            Assert.That(held.Cell, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void TheKitSurvivesTheRoundTripAndIsInTheHash()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            ulong empty = colony.World.ComputeStateHash().Value;
            Stock(colony, pawn, Medical, 3);
            Stock(colony, pawn, Ration, 2);
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(empty), "the kit is not in the hash");

            var restored = Board();
            restored.Load(colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(pawn.Id)!;
            Assert.That(InKit(restored, back, Medical), Is.EqualTo(3));
            Assert.That(InKit(restored, back, Ration), Is.EqualTo(2));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
        }

        /// <summary>
        /// An empty kit adds nothing to the hash, so a colony that never used one hashes as before:
        /// the kit's hashable is registered, and removing its contribution changes nothing.
        /// </summary>
        [Test]
        public void AnEmptyKitAddsNothingToTheHash()
        {
            var colony = Colony();
            var hash = StateHash.New();
            colony.Pawns.Kits.ContributeTo(ref hash);
            Assert.That(hash.Value, Is.EqualTo(StateHash.New().Value));
        }

        [Test]
        public void AFilledSlotIsPublishedAndAnEmptyOneIsNot()
        {
            var colony = Colony();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(pawn.Id, KitAspects.Def(0), out _), Is.False, "the control");

            Stock(colony, pawn, Medical, 3);
            colony.World.Tick();
            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.TryGetPawnAspect(pawn.Id, KitAspects.Def(0), out int def) && def == Medical, Is.True);
            Assert.That(frame.TryGetPawnAspect(pawn.Id, KitAspects.Count(0), out int count) && count == 3, Is.True);
            Assert.That(frame.TryGetPawnAspect(pawn.Id, KitAspects.Def(1), out _), Is.False);
        }
    }
}
