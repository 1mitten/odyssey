#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The one bed rule (design 59 §5a): <see cref="BedRule"/> in the contracts, answered from a
    /// live pawn by <see cref="BedRules"/> and from a published view by <see cref="BedRule.UserOf"/>.
    /// Every chooser asks it, so these tests hold the rule itself, the two faults it closed — the
    /// owner picker's gift going to a hog, and wildlife keeping the colony's beds shared — and the
    /// agreement between the simulation's answer and the interface's.
    /// </summary>
    public partial class BedTests
    {
        // ---- the rule, as a table ---------------------------------------------------------------

        [TestCase(BedUser.Colonist, BedPurpose.Colony, true)]
        [TestCase(BedUser.Colonist, BedPurpose.Prison, false)]
        [TestCase(BedUser.Prisoner, BedPurpose.Colony, false)]
        [TestCase(BedUser.Prisoner, BedPurpose.Prison, true)]
        [TestCase(BedUser.None, BedPurpose.Colony, false)]
        [TestCase(BedUser.None, BedPurpose.Prison, false)]
        public void ABedFitsOnlyThePoolItIsFor(BedUser user, BedPurpose purpose, bool fits)
        {
            Assert.That(BedRule.Fits(user, purpose), Is.EqualTo(fits));
            Assert.That(BedRule.MayOwn(user, purpose), Is.EqualTo(fits));
            Assert.That(BedRule.MayUse(user, 7, purpose, owner: 0), Is.EqualTo(fits), "nobody's bed");
            Assert.That(BedRule.MayUse(user, 7, purpose, owner: 7), Is.EqualTo(fits), "her own bed");
        }

        [Test]
        public void NobodyUsesAnotherPawnsBedWhateverItIsFor()
        {
            Assert.That(BedRule.MayUse(BedUser.Colonist, 7, BedPurpose.Colony, owner: 8), Is.False);
            Assert.That(BedRule.MayUse(BedUser.Prisoner, 7, BedPurpose.Prison, owner: 8), Is.False);
        }

        // ---- the gift ---------------------------------------------------------------------------

        /// <summary>
        /// <b>A hog and a bandit cannot be given a bed.</b> The owner picker listed every pawn on
        /// the board and <c>AssignOwnerAt</c> accepted any pawn that existed, so a bed could be
        /// given to a wild animal. The colonist is the control: the same bed, the same order.
        /// </summary>
        [Test]
        public void AnAnimalOrABanditCannotBeGivenABed()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, head);

            int ground = Size.Index(colony.Start);
            Pawn hog = colony.Pawns.Pawns.Spawn(ground, PawnKindIndex.MiddenHog);
            Pawn bandit = colony.Pawns.Pawns.Spawn(ground, PawnKindIndex.Bandit);

            Assert.That(Assign(colony, head, hog.Id.Value), Is.EqualTo(IntentRejection.NotPermitted), "a hog");
            Assert.That(Assign(colony, head, bandit.Id.Value), Is.EqualTo(IntentRejection.NotPermitted), "a bandit");
            Assert.That(colony.Construction.BedOwnerAt(head), Is.EqualTo(0), "and the bed is still nobody's");

            Pawn colonist = colony.Pawns.Pawns.All[0];
            Assert.That(Assign(colony, head, colonist.Id.Value), Is.EqualTo(IntentRejection.None), "the control");
        }

        // ---- the claim --------------------------------------------------------------------------

        /// <summary>
        /// <b>Wildlife does not keep the colony's beds shared.</b> The claim holds a bed back when
        /// the pool would not cover everyone else without one, and it counted every pawn on the
        /// board — so on a board with animals, a colonist with a bed to herself never claimed it.
        /// </summary>
        [Test]
        public void AHogOnTheBoardDoesNotKeepTheBedsShared()
        {
            ColonyWorld colony = Alone();
            int head = OpenFootprint(colony, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, head);
            int ground = Size.Index(colony.Start);
            colony.Pawns.Pawns.Spawn(ground, PawnKindIndex.MiddenHog);
            colony.Pawns.Pawns.Spawn(ground, PawnKindIndex.Bandit);

            Pawn colonist = colony.Pawns.Pawns.All[0];
            Assert.That(colony.Construction.TryClaimForSleeper(head, colonist.Id), Is.True);
            Assert.That(colony.Construction.BedOwnerAt(head), Is.EqualTo(colonist.Id.Value));
        }

        /// <summary>The control for the test above: another colonist without a bed still holds the one bed back.</summary>
        [Test]
        public void AnotherBedlessColonistStillKeepsTheOneBedShared()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, head);

            Pawn colonist = colony.Pawns.Pawns.All[0];
            Assert.That(colony.Construction.TryClaimForSleeper(head, colonist.Id), Is.False);
            Assert.That(colony.Construction.BedOwnerAt(head), Is.EqualTo(0));
        }

        // ---- one answer on both sides of the seam ------------------------------------------------

        /// <summary>
        /// The pool a pawn sleeps from, as the simulation answers it from the pawn and as the
        /// owner picker answers it from the published view, is the same for every pawn on the
        /// board: a colonist, a hog and a bandit.
        /// </summary>
        [Test]
        public void ThePublishedPoolAgreesWithTheSimulationsForEveryPawn()
        {
            ColonyWorld colony = Fresh();
            int ground = Size.Index(colony.Start);
            colony.Pawns.Pawns.Spawn(ground, PawnKindIndex.MiddenHog);
            colony.Pawns.Pawns.Spawn(ground, PawnKindIndex.Bandit);
            colony.World.Tick();

            int seen = 0;
            foreach (PawnView view in colony.World.Views.Current.Pawns)
            {
                Pawn? pawn = colony.Pawns.Pawns.Get(view.Id);
                Assert.That(pawn, Is.Not.Null);
                Assert.That(BedRule.UserOf(view), Is.EqualTo(BedRules.UserOf(pawn!)), $"pawn {view.Id.Value}");
                seen++;
            }
            Assert.That(seen, Is.EqualTo(5), "three colonists, a hog and a bandit");
        }
    }
}
