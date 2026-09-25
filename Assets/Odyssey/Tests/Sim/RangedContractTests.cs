#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the ranged line's contracts step promised (design 47 §3a): a seventh skill dealt without
    /// disturbing the six, dealt once to a colonist from an older save and never again, a debug
    /// gunman who is still a bandit, and the Arm row dealing the pistol among the rest.
    /// </summary>
    public class RangedContractTests
    {
        /// <summary>
        /// The seventh deal leaves the first six exactly as they were: the draw is in skill order and
        /// Shooting is appended after them. Pinned by value against the same seed and id dealt through
        /// the same stream with the seventh skill's draw withheld.
        /// </summary>
        [Test]
        public void TheSeventhDealLeavesTheFirstSixUnchanged()
        {
            var colony = Board();
            colony.World.Tick();
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                int[] six = DealtWithoutShooting(pawn);
                for (int s = 0; s < SkillIndex.Shooting; s++)
                    Assert.That(pawn.Skills[s], Is.EqualTo(six[s]), $"skill {s} of pawn {pawn.Id.Value} moved");
            }
        }

        /// <summary>
        /// The first six levels as <see cref="Pawn.RollStartingSkills"/> would have dealt them before
        /// Shooting existed: the same weights, the same stream, six draws.
        /// </summary>
        static int[] DealtWithoutShooting(Pawn pawn)
        {
            int[] weights = pawn.Content.Kind.startingSkillLevelWeights;
            int total = 0;
            foreach (int w in weights) total += w;
            var rng = DeterministicRandom.ForTick(pawn.RollSeed, pawn.Id.Value, PawnPurpose.StartingSkill);
            var dealt = new int[SkillIndex.Shooting];
            for (int skill = 0; skill < dealt.Length; skill++)
            {
                int roll = rng.NextInt(total), level = weights.Length - 1, cumulative = 0;
                for (int l = 0; l < weights.Length; l++)
                {
                    cumulative += weights[l];
                    if (roll < cumulative) { level = l; break; }
                }
                var def = pawn.Content.Skills[skill];
                dealt[skill] = def.ExperienceForLevel(Math.Min(level, def.maxLevel));
            }
            return dealt;
        }

        /// <summary>
        /// A colonist from a format-9 save — which carried six skills, so Shooting loads as nought — is
        /// dealt her Shooting once on load, and her other six are untouched; a format-10 save with a
        /// trained Shooting is not re-dealt. Formats 9 and 10 share every layout, so a save relabelled
        /// 9 is exactly what a format-9 build wrote, less the seventh skill set to nought here.
        /// </summary>
        [Test]
        public void AnOlderSaveIsDealtShootingOnceAndANewerOneIsLeftAlone()
        {
            var colony = Board();
            colony.World.Tick();
            Pawn first = colony.Pawns.Pawns.All[0];
            int dealt = first.Skills[SkillIndex.Shooting];
            int[] before = (int[])first.Skills.Clone();

            foreach (Pawn pawn in colony.Pawns.Pawns.All) pawn.Skills[SkillIndex.Shooting] = 0;
            byte[] old = colony.Save();
            BitConverter.GetBytes(9).CopyTo(old, 8);

            var loaded = Board();
            loaded.Load(old);
            Pawn back = loaded.Pawns.Pawns.Get(first.Id)!;
            for (int s = 0; s < SkillIndex.Shooting; s++)
                Assert.That(back.Skills[s], Is.EqualTo(before[s]), $"skill {s} was re-dealt");
            Assert.That(back.Skills[SkillIndex.Shooting], Is.EqualTo(dealt),
                "dealt the Shooting a new colonist of her seed and id would have been");

            // Format 10, trained well past her deal: left alone.
            back.Skills[SkillIndex.Shooting] = dealt + 123_456;
            var again = Board();
            again.Load(loaded.Save());
            Assert.That(again.Pawns.Pawns.Get(first.Id)!.Skills[SkillIndex.Shooting], Is.EqualTo(dealt + 123_456));
        }

        /// <summary>An animal from an older save is not dealt skills: kinds load after skills, and the deal waits for them.</summary>
        [Test]
        public void AnAnimalFromAnOlderSaveIsNotDealtSkills()
        {
            var colony = Board();
            colony.World.Tick();
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 5, 5));
            byte[] old = colony.Save();
            BitConverter.GetBytes(9).CopyTo(old, 8);
            var loaded = Board();
            loaded.Load(old);
            Pawn back = loaded.Pawns.Pawns.Get(hog.Id)!;
            Assert.That(back.IsPerson, Is.False);
            Assert.That(Array.TrueForAll(back.Skills, x => x == 0), Is.True);
        }

        /// <summary>
        /// The debug gunman: <c>SpawnPawn</c> with <c>B</c> the pistol plus one makes a bandit — still a
        /// hostile person, so still helmeted and vested — holding a pistol; <c>B</c> nought deals from
        /// the kind's own table, as it always did; <c>B</c> naming no weapon is refused.
        /// </summary>
        [Test]
        public void TheSpawnIntentsWeaponMakesAPistolBandit()
        {
            var colony = Board();
            colony.World.Tick();
            CellRef at = Size.FromIndex(Near(colony, 6, 0));

            Assert.That(Send(colony, new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Bandit, ItemIndex.Pistol + 1)),
                Is.EqualTo(IntentRejection.None));
            Pawn gunman = colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1];
            Assert.That(gunman.IsHostile && gunman.IsPerson, Is.True);
            Assert.That(colony.Pawns.WeaponRules.ArmamentOf(gunman, colony.Pawns).ItemDef, Is.EqualTo(ItemIndex.Pistol));

            Assert.That(Send(colony, new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Bandit, 0)), Is.EqualTo(IntentRejection.None));
            Pawn plain = colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1];
            Assert.That(colony.Pawns.WeaponRules.ArmamentOf(plain, colony.Pawns).Attack.IsRanged, Is.False,
                "the control: the kind's own table deals a club or a bar");

            Assert.That(Send(colony, new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Bandit, ItemIndex.Wood + 1)),
                Is.EqualTo(IntentRejection.NotPermitted), "wood is not a weapon");
        }

        /// <summary>
        /// The Arm row deals every item with a weapon block, so over enough colonists it deals the
        /// pistol; with the pistol's block withheld by a replaced Def it never does — the control.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void TheArmRowDealsThePistolAmongTheRest(bool withPistol)
        {
            var colony = Board(colonists: 60, beds: 0);
            colony.World.Tick();
            if (!withPistol)
            {
                ItemDef shipped = colony.Pawns.Content.Items[ItemIndex.Pistol];
                colony.Pawns.Content.Items[ItemIndex.Pistol] = new ItemDef
                {
                    defName = shipped.defName, category = shipped.category, stackLimit = shipped.stackLimit, weapon = null,
                };
            }
            Assert.That(Send(colony, new Intent(IntentKind.DebugArmColonists, default, 0, 0)), Is.EqualTo(IntentRejection.None));
            int pistols = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                if (colony.Pawns.WeaponRules.ArmamentOf(pawn, colony.Pawns).ItemDef == ItemIndex.Pistol) pistols++;
            Assert.That(pistols > 0, Is.EqualTo(withPistol), $"{pistols} of 60 dealt a pistol");
        }
    }
}
