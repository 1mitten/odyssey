#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The tier a bed finishes at: the roll itself, the endpoints the design pins, and what the
    /// tier does to a sleeping colonist (design 20 §6–7). The drawn values are never asserted —
    /// the table is the contract, and the dice are the dice.
    /// </summary>
    public class BedQualityTests
    {
        [Test]
        public void TheRollIsDeterministic()
        {
            for (int skill = 0; skill <= 20; skill += 5)
            for (int key = 0; key < 4; key++)
            {
                byte first = Roll(skill, key);
                Assert.That(Roll(skill, key), Is.EqualTo(first),
                    $"skill {skill}, key {key}: the same question must always answer the same");
            }
        }

        [Test]
        public void EverySkillRollsATier()
        {
            for (int skill = 0; skill <= 20; skill++)
            for (int key = 0; key < 8; key++)
            {
                byte tier = Roll(skill, key);
                Assert.That(tier, Is.InRange(1, 5), "a quality-bearing thing finishes at one of the five tiers");
            }
        }

        /// <summary>
        /// The two endpoints the owner's design names, and the only things about the bell the
        /// tests own: a novice can never be handed an Epic bed, and a master can never be
        /// humiliated with a Poor one. Everything between is luck the table shapes.
        /// </summary>
        [Test]
        public void ANoviceNeverRollsEpicAndAMasterNeverRollsPoor()
        {
            for (int key = 0; key < 200; key++)
            {
                Assert.That(Roll(0, key), Is.Not.EqualTo(QualityHandle.Epic),
                    "skill 0 has no weight at Epic at all, so one draw out of place means the bell moved");
                Assert.That(Roll(20, key), Is.Not.EqualTo(QualityHandle.Poor),
                    "skill 20 has no weight at Poor at all");
            }
        }

        [Test]
        public void ASleepingColonistRestsAtTheTiersOwnRate()
        {
            // Three colonies alike but for the tier of the one bed in them — same seed, same pawn,
            // same walk to the same cell, so the need intervals line up and the only thing that can
            // differ is the effectiveness the tier carries.
            int poorGain = SleepAndGain(QualityHandle.Poor);
            int normalGain = SleepAndGain(QualityHandle.Normal);
            int epicGain = SleepAndGain(QualityHandle.Epic);
            int groundGain = SleepAndGain(0 /* no bed at all */);

            Assert.That(normalGain, Is.GreaterThan(poorGain), "Normal (100) rests better than Poor (85)");
            Assert.That(epicGain, Is.AtLeast(poorGain + 25),
                "Epic (140) against Poor (85) is a 55-point gap, and 1_500 ticks of equal-phase " +
                "intervals turns that into a margin no rounding can eat");
            Assert.That(epicGain, Is.GreaterThan(groundGain + 15),
                "an Epic bed against the bare ground (80) is the whole argument for building one");
        }

        /// <summary>
        /// Wire a construction grid into the bare <c>PawnTests</c>-style rig, which builds its
        /// context by hand and so has nowhere a bed could be raised until this hands it one. The
        /// grid is real: the same Place and Raise the game runs, not a stub.
        /// </summary>
        static ConstructionGrid Wire(Colony colony)
        {
            var grid = new ConstructionGrid(
                colony.Cells, new EdificeSaveSection(new List<PlacedEdifice>()),
                colony.Ctx.Items, colony.Ctx.Pawns);
            colony.Ctx.Construction = grid;
            return grid;
        }

        static int SleepAndGain(int tier)
        {
            var colony = Colony.Build();
            ConstructionGrid? grid = tier > 0 ? Wire(colony) : null;

            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            pawn.Needs[NeedIndex.Rest] = 100;

            int head = colony.Cell(7, 3, 0);
            if (tier > 0)
            {
                Assume.That(grid!.Place(colony.Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                    Is.EqualTo(IntentRejection.None));
                grid.Raise(colony.Ctx, head, (byte)tier);
                Assume.That(colony.Ctx.Items.Beds.Contains(head), Is.True);
            }

            for (int i = 0; i < 3_000 && !pawn.Asleep; i++) colony.World.Tick();
            Assume.That(pawn.Asleep, Is.True);
            if (tier > 0) Assume.That(pawn.Cell, Is.EqualTo(head), "the pawn sleeps in the built bed");

            int before = pawn.Needs[NeedIndex.Rest];
            colony.World.Tick(1_500);
            return pawn.Needs[NeedIndex.Rest] - before;
        }

        static byte Roll(int skill, int key) => QualityContent.Roll(
            skill, DeterministicRandom.ForTick(20260917u, key, PawnPurpose.BuildQuality));
    }
}
