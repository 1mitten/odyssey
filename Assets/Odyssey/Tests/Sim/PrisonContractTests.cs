#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The prisoner line's contracts step (design 59 P3): the Social skill and its backfill, the
    /// Warden work type, the eight job handles and the prison's random streams. Each table is a
    /// save contract, so what these hold is that it was extended and nothing else moved.
    /// </summary>
    public class PrisonContractTests
    {
        static ColonyWorld Colony()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 0;
            return ColonyWorld.Build(new GridSize(40, 40, 16), 20260926u, scenario, barren: true, wooded: false);
        }

        [Test]
        public void EveryNameTableIsAsLongAsItsIndex()
        {
            Assert.That(SkillIndex.Names.Length, Is.EqualTo(SkillIndex.Count));
            Assert.That(WorkTypeIndex.Names.Length, Is.EqualTo(WorkTypeIndex.Count));
            Assert.That(SkillIndex.Names[SkillIndex.Social], Is.EqualTo("social"));
            Assert.That(WorkTypeIndex.Names[WorkTypeIndex.Warden], Is.EqualTo("warden"));

            PawnContent content = ContentPack.Pawns();
            Assert.That(content.Skills[SkillIndex.Social].defName, Is.EqualTo("Skill_Social"));
            Assert.That(content.WorkTypes[WorkTypeIndex.Warden].defName, Is.EqualTo("Work_Warden"));
            Assert.That(content.Jobs[JobIndex.Chat].trainsSkill, Is.EqualTo(SkillIndex.Social),
                "a warden's chat is what trains Social");
            Assert.That(content.Thoughts[ThoughtIndex.Imprisoned].defName, Is.EqualTo("Thought_Imprisoned"));
            Assert.That(content.Thoughts[ThoughtIndex.WasArrested].defName, Is.EqualTo("Thought_WasArrested"));
        }

        /// <summary>
        /// <b>A colonist from a format-10 file is dealt Social once, and nothing else moves.</b>
        /// The deal draws in skill order and writes only a skill still at nought, so the nine she
        /// had keep their experience to the unit, Social comes from the stream a new colonist of
        /// her seed would use, and dealing again changes nothing.
        /// </summary>
        [Test]
        public void AnOlderColonistIsDealtSocialOnceAndKeepsTheRest()
        {
            ColonyWorld colony = Colony();
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int[] dealt = (int[])pawn.Skills.Clone();

            // As she would arrive from a file written before Social existed.
            pawn.Skills[SkillIndex.Social] = 0;
            int[] withoutSocial = (int[])pawn.Skills.Clone();

            colony.Pawns.Pawns.BackfillSkills(11);
            Assert.That(pawn.Skills, Is.EqualTo(withoutSocial), "a file at 11 is left alone");

            colony.Pawns.Pawns.BackfillSkills(10);
            Assert.That(pawn.Skills, Is.EqualTo(dealt), "the same level a new colonist of her seed is dealt, and the rest untouched");

            colony.Pawns.Pawns.BackfillSkills(10);
            Assert.That(pawn.Skills, Is.EqualTo(dealt), "and dealing again changes nothing");
        }

        [Test]
        public void ATrainedSocialIsNeverDealtOver()
        {
            ColonyWorld colony = Colony();
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.Skills[SkillIndex.Social] = 12_345;
            colony.Pawns.Pawns.BackfillSkills(9);
            Assert.That(pawn.Skills[SkillIndex.Social], Is.EqualTo(12_345));
        }

        [Test]
        public void EveryPrisonJobIsAStubUntilItsUnitAndNothingStartsOne()
        {
            PawnContent content = ContentPack.Pawns();
            for (int job = JobIndex.Capture; job <= JobIndex.Arrest; job++)
                Assert.That(content.Jobs[job].driver, Is.EqualTo(job), content.Jobs[job].defName);
            Assert.That(JobIndex.Arrest, Is.EqualTo(JobIndex.Count - 1), "the prisoner line's eight are the last");
        }

        /// <summary>
        /// <b>The prison's four streams are nobody else's.</b> Every <c>const uint</c> in a class
        /// whose name ends in <c>Purpose</c>, across the simulation, is gathered; the prison's
        /// must not appear anywhere else. The raids review found a merge with no conflict marker
        /// putting two systems on one stream (design 55 §15) — this is the check that would have
        /// caught it.
        /// </summary>
        [Test]
        public void ThePrisonsRandomStreamsAreNobodyElses()
        {
            var sim = typeof(Pawn).Assembly;
            var owners = new Dictionary<uint, List<string>>();
            foreach (Type type in sim.GetTypes().Where(t => t.Name.EndsWith("Purpose", StringComparison.Ordinal)))
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (!field.IsLiteral || field.FieldType != typeof(uint)) continue;
                uint value = (uint)field.GetRawConstantValue()!;
                if (!owners.TryGetValue(value, out var names)) owners[value] = names = new List<string>();
                names.Add($"{type.Name}.{field.Name}");
            }

            foreach (FieldInfo field in typeof(PrisonPurpose).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                uint value = (uint)field.GetRawConstantValue()!;
                Assert.That(owners[value], Is.EqualTo(new[] { $"PrisonPurpose.{field.Name}" }),
                    $"PrisonPurpose.{field.Name} shares its stream");
            }
        }
    }
}
