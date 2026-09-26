#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;
using static Odyssey.Tests.Sim.PrisonFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Feeding the held (design 58 §7, §14): a warden carries one portion to a hungry prisoner who
    /// cannot feed herself — shackled, down, or in a cell with nothing in it — and feeds her there.
    /// A prisoner with food in her cell is left to eat it. Food lying in a cell is hers: no colonist
    /// eats it and no hauler carries it back out.
    /// </summary>
    public class FeedingTests
    {
        static ColonyWorld Colony() => Board(colonists: 1, beds: 0);

        static int FoodUnits(ColonyWorld colony) =>
            colony.Pawns.Items.Items.Where(i => !i.Despawned && colony.Pawns.Content.Items[i.DefIndex].nutrition > 0)
                .Sum(i => i.Stack);

        [Test]
        public void AShackledHungryPrisonerIsFedByAWarden()
        {
            ColonyWorld colony = Colony();
            colony.World.Tick();
            int bed = ShackleBed(colony, -8);
            Pawn prisoner = HeldOn(colony, bed, Near(colony, -6, 0));
            colony.Pawns.Items.Spawn(ItemIndex.Meal, Near(colony, 3, 3), 4);
            int seek = colony.Pawns.Content.Needs[NeedIndex.Food].seekThreshold;
            prisoner.Needs[NeedIndex.Food] = seek / 4;
            // The colonist is kept from eating, so every portion that goes is the prisoner's.
            colony.Pawns.Pawns.All[0].Needs[NeedIndex.Food] = colony.Pawns.Content.Needs[NeedIndex.Food].max;
            int before = FoodUnits(colony);

            for (int t = 0; t < 4_000 && prisoner.Needs[NeedIndex.Food] < seek; t++) colony.World.Tick();
            Assert.That(prisoner.Needs[NeedIndex.Food], Is.GreaterThanOrEqualTo(seek), "fed");
            Assert.That(FoodUnits(colony), Is.EqualTo(before - 1), "one portion, not the pile");
            Assert.That(prisoner.Memories.Any(m => m.ThoughtIndex != ThoughtIndex.Imprisoned), Is.True,
                "and she thinks of what she ate");
        }

        [Test]
        public void APrisonerWithFoodInHerCellIsLeftToEatIt()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            Pawn warden = colony.Pawns.Pawns.All[0];
            colony.Pawns.Items.Spawn(ItemIndex.Meal, Near(colony, -3, 3), 2);
            prisoner.Needs[NeedIndex.Food] = 1;

            var giver = new FeedPrisonerWorkGiver();
            Assert.That(giver.TryGiveJob(warden, colony.Pawns, warden.JobBuffer), Is.True, "the control: nothing in her cell");
            colony.Pawns.Items.Spawn(ItemIndex.Meal, cell.Inside, 1);
            Assert.That(giver.TryGiveJob(warden, colony.Pawns, warden.JobBuffer), Is.False, "she can feed herself");
        }

        [Test]
        public void AMealInACellIsNeitherEatenByAColonistNorHauledOut()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            HeldIn(colony, cell);
            ThingId meal = colony.Pawns.Items.Spawn(ItemIndex.Meal, cell.Inside, 1);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colonist.Needs[NeedIndex.Food] = 1;

            var eat = new CriticalNeedsThinkNode();
            Job job = colonist.JobBuffer;
            job.Reset(JobIndex.Wait);
            bool ate = eat.TryGiveJob(colonist, colony.Pawns, job);
            Assert.That(ate && job.DefIndex == JobIndex.Eat && job.TargetItem == meal, Is.False,
                "a prisoner's meal is not a colonist's");

            var haul = new HaulWorkGiver();
            job.Reset(JobIndex.Wait);
            bool hauled = haul.TryGiveJob(colonist, colony.Pawns, job);
            Assert.That(hauled && job.TargetItem == meal, Is.False, "and no hauler carries it back out");
        }
    }
}
