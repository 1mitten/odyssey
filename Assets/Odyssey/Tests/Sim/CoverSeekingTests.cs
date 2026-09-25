#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Fighters with guns taking cover (design 50 §6): a bandit steps behind the sandbags beside it
    /// before it shoots; the same bandit with no sandbags shoots from where it stands; a drafted
    /// colonist holds where she was put; nobody backs off more than two cells to find cover; and the
    /// search is asked now and then, not every tick.
    /// </summary>
    public class CoverSeekingTests
    {
        static int Offset(int cell, int dx, int dz = 0)
        {
            CellRef c = Size.FromIndex(cell);
            return Size.Index(c.X + dx, c.Z + dz, c.Y);
        }

        static void Sandbags(ColonyWorld colony, int cell)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Sandbags, StuffHandle.Stone, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        static void Arm(ColonyWorld colony, Pawn pawn, int def)
        {
            int cell = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, pawn.Cell, def, 1, JobDriver.DropSearchRadius);
            ThingId id = colony.Pawns.Items.Spawn(def, cell);
            WeaponHand.TakeUp(pawn, colony.Pawns.Items.Get(id)!, colony.Pawns);
        }

        /// <summary>Where the pawn stood when it fired its first shot, or -1 if it never fired.</summary>
        static int FirstShotFrom(ColonyWorld colony, Pawn shooter, int ticks)
        {
            var tape = new Tape();
            for (int t = 0; t < ticks; t++)
            {
                colony.World.Tick();
                tape.Read(colony);
                if (tape.By(shooter, CombatEventKind.Shot).Count > 0) return shooter.Cell;
            }
            return -1;
        }

        /// <summary>
        /// A pistol bandit ten cells from a colonist, with sandbags one step to its side between it and
        /// her: it steps behind them and fires from there. The control, the same board without the
        /// sandbags, fires from where it stands.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ABanditStepsBehindTheSandbagsBesideItBeforeItShoots(bool sandbags)
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int start = Near(colony, -15, 8);
            int behind = Offset(start, 1, 1);
            int bags = Offset(start, 2, 1);
            if (sandbags) Sandbags(colony, bags);
            Stand(colony, colonist, Offset(start, 10));
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            Pawn bandit = colony.Pawns.Pawns.Spawn(start, PawnKindIndex.Bandit, ItemIndex.Pistol);

            int from = FirstShotFrom(colony, bandit, 900);
            Assert.That(from, Is.GreaterThanOrEqualTo(0), "it fired");
            if (sandbags)
            {
                Assert.That(from, Is.EqualTo(behind), "behind the sandbags");
                Assert.That(Cover.Evaluate(colony.Pawns, colonist.Cell, from), Is.GreaterThan(400));
            }
            else Assert.That(from, Is.EqualTo(start), "in the open it shoots from where it stands");
        }

        /// <summary>A drafted colonist with sandbags beside her holds where she was put and fires from there.</summary>
        [Test]
        public void ADraftedColonistHoldsWhereSheWasPut()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int start = Near(colony, -15, 8);
            Sandbags(colony, Offset(start, 2, 1));
            Stand(colony, colonist, start);
            Arm(colony, colonist, ItemIndex.Pistol);
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            colony.Pawns.Pawns.Spawn(Offset(start, 10), PawnKindIndex.Bandit, ItemIndex.Machete);

            Assert.That(FirstShotFrom(colony, colonist, 600), Is.EqualTo(start));
        }

        /// <summary>
        /// The only cover is three cells farther from the target than the bandit stands — past the
        /// two it may back off — so it fires from where it is rather than walk the fight away.
        /// </summary>
        [Test]
        public void NobodyBacksOffMoreThanTwoCellsForCover()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int start = Near(colony, -15, 8);
            Sandbags(colony, Offset(start, -2));
            Stand(colony, colonist, Offset(start, 10));
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            Pawn bandit = colony.Pawns.Pawns.Spawn(start, PawnKindIndex.Bandit, ItemIndex.Pistol);

            Assert.That(FirstShotFrom(colony, bandit, 900), Is.EqualTo(start));
        }

        [Test]
        public void TheSearchIsAskedNowAndThenNotEveryTick()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int start = Near(colony, -15, 8);
            Sandbags(colony, Offset(start, 2, 1));
            Stand(colony, colonist, Offset(start, 10));
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            colony.Pawns.Pawns.Spawn(start, PawnKindIndex.Bandit, ItemIndex.Pistol);

            long before = CoverPosition.Searches;
            for (int t = 0; t < 1_200; t++) colony.World.Tick();
            long searches = CoverPosition.Searches - before;
            TestContext.WriteLine($"{searches} cover searches in 1,200 ticks");
            Assert.That(searches, Is.LessThan(60));
        }

        // ---- the crouch (design 50 §8a) --------------------------------------------------------

        static bool Crouched(ColonyWorld colony, Pawn pawn, out int value) =>
            colony.World.Views.Current.TryGetPawnAspect(pawn.Id, CombatAspects.CoverCrouch, out value);

        [TestCase(true)]
        [TestCase(false)]
        public void ADraftedColonistBesideSandbagsCrouchesAndOneInTheOpenStands(bool sandbags)
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int start = Near(colony, -15, 8);
            if (sandbags) Sandbags(colony, Offset(start, 1));
            Stand(colony, colonist, start);
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 5; t++) colony.World.Tick();
            Assert.That(Crouched(colony, colonist, out int value), Is.EqualTo(sandbags));
            if (sandbags) Assert.That(value, Is.EqualTo(550));
        }

        /// <summary>The bandit that stepped behind the sandbags crouches there while it shoots.</summary>
        [Test]
        public void AShooterBehindSandbagsCrouchesWhileSheShoots()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int start = Near(colony, -15, 8);
            Sandbags(colony, Offset(start, 2, 1));
            Stand(colony, colonist, Offset(start, 10));
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            Pawn bandit = colony.Pawns.Pawns.Spawn(start, PawnKindIndex.Bandit, ItemIndex.Pistol);

            Assert.That(FirstShotFrom(colony, bandit, 900), Is.EqualTo(Offset(start, 1, 1)));
            colony.World.Tick();
            Assert.That(Crouched(colony, bandit, out int value), Is.True);
            Assert.That(value, Is.GreaterThanOrEqualTo(colony.Pawns.Content.Combat.coverCrouchPerMille));
        }
    }
}
