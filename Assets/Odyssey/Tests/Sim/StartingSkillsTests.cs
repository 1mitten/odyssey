#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// U37: a colonist spawns with rolled skill levels, deterministic on its own generation seed,
    /// saved and hashed — and the roll must move every <c>GoldenMasterTests</c> "Simulated" hash
    /// and no "Generated" one.
    ///
    /// <para><b>Why the roll happens on the first tick and not at placement.</b>
    /// <see cref="ColonyScenario.Place"/> runs inside <see cref="ColonyWorld.Build"/>, before
    /// <c>GoldenMasterTests.Check</c> takes its "Generated" hash — the state of a world that has
    /// been generated and had a colony placed on it, but has not run a single tick. A roll at
    /// placement would move that hash exactly as much as it moves "Simulated", which the plan
    /// does not ask for. <see cref="StartingSkillsSystem"/> instead fires once, from inside
    /// <c>SimWorld.Tick()</c>, the first time <c>CurrentTick</c> reads zero — which is after
    /// "Generated" is taken and before "Simulated" is, for any golden case that runs at least one
    /// tick (all three do). <see cref="EveryColonistsSkillsAreZeroImmediatelyAfterBuild"/> is the
    /// direct evidence: nothing a build does writes a skill, whatever <c>ComputeStateHash</c>
    /// walks at that point.</para>
    /// </summary>
    public class StartingSkillsTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);

        static ColonyWorld Build(uint seed) => ColonyWorld.Build(Size, seed, ScenarioDef.Bare());

        [Test]
        public void EveryColonistsSkillsAreZeroImmediatelyAfterBuild()
        {
            // The direct proof that the roll cannot be inside "Generated": placement has already
            // run by the time Build() returns, and every skill on every colonist it placed is
            // still exactly what the constructor gives it.
            ColonyWorld colony = Build(1u);
            Assert.That(colony.Placement.Colonists, Is.GreaterThan(0), "nothing was placed to check");

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                for (int skill = 0; skill < SkillIndex.Count; skill++)
                    Assert.That(pawn.Skills[skill], Is.Zero,
                        $"pawn {pawn.Id.Value} skill {skill} is nonzero before a single tick has run");
        }

        [Test]
        public void TheFirstTickRollsSkillsThatWereZeroAMomentBefore()
        {
            ColonyWorld colony = Build(1u);
            colony.World.Tick();

            int nonZero = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                for (int skill = 0; skill < SkillIndex.Count; skill++)
                    if (pawn.Skills[skill] != 0) nonZero++;

            Assert.That(nonZero, Is.GreaterThan(0),
                "five colonists over four skills rolled nothing but zero, or the roll never ran");
        }

        [Test]
        public void RolledLevelsStayWithinTheSkillsOwnRange()
        {
            ColonyWorld colony = Build(3u);
            colony.World.Tick();

            PawnContent content = colony.Pawns.Content;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                for (int skill = 0; skill < SkillIndex.Count; skill++)
                {
                    int level = pawn.SkillLevel(skill);
                    Assert.That(level, Is.InRange(0, content.Skills[skill].maxLevel));
                }
        }

        [Test]
        public void TheRollIsDeterministicOnTheWorldSeedAndNothingElse()
        {
            // Two colonies from the same seed agree on every colonist's every skill; a third from
            // a different seed disagrees on at least one, or the seed is not actually driving it.
            var first = Build(7u);
            first.World.Tick();
            var second = Build(7u);
            second.World.Tick();

            for (int i = 0; i < first.Pawns.Pawns.Count; i++)
                Assert.That(second.Pawns.Pawns.All[i].Skills, Is.EqualTo(first.Pawns.Pawns.All[i].Skills),
                    "two builds of the same seed rolled different starting skills");

            var third = Build(8u);
            third.World.Tick();

            bool anyDifferent = false;
            for (int i = 0; i < first.Pawns.Pawns.Count && !anyDifferent; i++)
            {
                var a = first.Pawns.Pawns.All[i].Skills;
                var b = third.Pawns.Pawns.All[i].Skills;
                for (int s = 0; s < a.Length; s++)
                    if (a[s] != b[s]) { anyDifferent = true; break; }
            }
            Assert.That(anyDifferent, Is.True, "two different seeds rolled identical starting skills");
        }

        [Test]
        public void ARolledSkillIsNeverReRolled()
        {
            // The guard is CurrentTick == 0, read once ever. If it fired again on a later tick it
            // would either leave an already-nonzero skill alone (this system's own rule) or, if
            // that rule were ever removed, silently stomp experience a colonist had since earned
            // by working. Proven directly: force skill 0 back to a value the roll would not have
            // produced on its own account for a normal skill index, tick on, and require it holds.
            var colony = Build(1u);
            colony.World.Tick();

            Pawn pawn = colony.Pawns.Pawns.All[0];
            int rolled = pawn.Skills[SkillIndex.Mining];
            pawn.Skills[SkillIndex.Mining] = rolled + 12_345;

            colony.World.Tick(500);

            Assert.That(pawn.Skills[SkillIndex.Mining], Is.EqualTo(rolled + 12_345),
                "something touched a skill's experience on a tick after the first");
        }

        [Test]
        public void RolledSkillsAreInTheStateHash()
        {
            var colony = Build(1u);
            ulong beforeRoll = colony.World.ComputeStateHash().Value;

            colony.World.Tick();
            ulong afterRoll = colony.World.ComputeStateHash().Value;

            Assert.That(afterRoll, Is.Not.EqualTo(beforeRoll),
                "the world hash did not move when starting skills were rolled");
        }

        [Test]
        public void RolledSkillsSurviveASaveAndLoad()
        {
            var original = Build(5u);
            original.World.Tick();
            byte[] bytes = original.Save();

            var restored = Build(5u);
            var header = restored.Load(bytes);

            Assert.That(header.SkippedSections, Is.Empty, "a section this build wrote was not read back");
            for (int i = 0; i < original.Pawns.Pawns.Count; i++)
                Assert.That(restored.Pawns.Pawns.All[i].Skills, Is.EqualTo(original.Pawns.Pawns.All[i].Skills),
                    "rolled starting skills did not round-trip through a save");

            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(original.World.ComputeStateHash().Value));
        }

        [Test]
        public void ALoadedWorldDoesNotReRollOverEarnedExperience()
        {
            // The guard reads CurrentTick == 0, so a load that restores a later tick must never
            // fire it again — proven here by giving a colonist experience after the roll, saving,
            // loading, and requiring the earned amount is still there rather than overwritten.
            var original = Build(2u);
            original.World.Tick();
            Pawn worker = original.Pawns.Pawns.All[0];
            worker.GainExperience(SkillIndex.Mining, 4_000, currentTick: original.World.CurrentTick);
            int expected = worker.Skills[SkillIndex.Mining];
            original.World.Tick(200);
            expected = worker.Skills[SkillIndex.Mining];

            byte[] bytes = original.Save();
            var restored = Build(2u);
            restored.Load(bytes);
            restored.World.Tick(50);

            Assert.That(restored.Pawns.Pawns.All[0].Skills[SkillIndex.Mining], Is.EqualTo(expected),
                "resuming a loaded world re-rolled or otherwise disturbed earned experience");
        }
    }
}
