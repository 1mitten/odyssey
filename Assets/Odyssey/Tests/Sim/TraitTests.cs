#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Traits (design 43 §5f, TM3): dealt two or three from a colonist's own seed on a stream of
    /// their own, never twice, and read by the five seams the owner allowed — mood, the break
    /// line, learning, work speed, and work she will not do. Every effect is tested against the
    /// same colonist with her traits taken away, which is the control.
    /// </summary>
    public class TraitTests
    {
        static PawnContent Content => ContentPack.Pawns();

        static Pawn Rolled(uint seed, int slot = 0) => ColonistDraw.Roll(seed, slot);

        static Pawn With(params int[] traits)
        {
            var pawn = new Pawn(new PawnId(1), cell: -1, Content);
            pawn.Traits.AddRange(traits);
            return pawn;
        }

        // ---- the deal ----------------------------------------------------------------------

        [Test]
        public void EveryColonistIsDealtTwoOrThreeAndTheSameSeedDealsTheSame()
        {
            int thirds = 0;
            for (uint seed = 1; seed <= 400; seed++)
            {
                Pawn a = Rolled(seed), b = Rolled(seed);
                Assert.That(a.Traits, Is.EqualTo(b.Traits), $"seed {seed}: a reroll of the same seed is the same person");
                Assert.That(a.Traits.Count, Is.InRange(2, 3), $"seed {seed}");
                if (a.Traits.Count == 3) thirds++;
            }
            // 30 per cent (INVENTED), measured over four hundred seeds with room either side.
            Assert.That(thirds, Is.InRange(80, 160), "the third trait is dealt about three times in ten");
        }

        [Test]
        public void NoColonistHoldsTwoDegreesOfASpectrumOrTwoTraitsThatConflict()
        {
            PawnContent content = Content;
            for (uint seed = 1; seed <= 600; seed++)
            {
                List<int> traits = Rolled(seed).Traits;
                Assert.That(traits.Distinct().Count(), Is.EqualTo(traits.Count), $"seed {seed}: a trait twice");
                var spectra = traits.Select(t => content.TraitSpectrum[t]).Where(s => s >= 0).ToList();
                Assert.That(spectra.Distinct().Count(), Is.EqualTo(spectra.Count), $"seed {seed}: two degrees of one spectrum");
                Assert.That(traits.Contains(TraitHandle.Tireless) && traits.Contains(TraitHandle.SoftHands), Is.False,
                    $"seed {seed}: Tireless and Soft hands conflict");
            }
        }

        [Test]
        public void EveryTraitTurnsUpAndTheRareOnesLess()
        {
            var seen = new int[TraitHandle.Count];
            for (uint seed = 1; seed <= 2000; seed++)
                foreach (int t in Rolled(seed).Traits) seen[t]++;
            for (int t = 0; t < TraitHandle.Count; t++)
                Assert.That(seen[t], Is.GreaterThan(0), TraitHandle.Names[t] + " was never dealt");
            Assert.That(seen[TraitHandle.Sunny], Is.LessThan(seen[TraitHandle.Cheerful]),
                "a rare degree is dealt less than a normal one (commonality 8 against 20)");
        }

        [Test]
        public void DealingTraitsMovedNoPassionAndNoSkill()
        {
            // The trait stream is its own purpose (design 43 §3). The control is a pawn rolled the
            // way ColonistDraw rolled before traits: passions and skills, and nothing else.
            for (uint seed = 1; seed <= 50; seed++)
            {
                Pawn dealt = Rolled(seed);
                var bare = new Pawn(ColonistDraw.IdForSlot(0), cell: -1, Content) { RollSeed = seed };
                bare.RollPassions();
                bare.RollStartingSkills();
                Assert.That(dealt.Passions, Is.EqualTo(bare.Passions), $"seed {seed}");
                Assert.That(dealt.Skills, Is.EqualTo(bare.Skills), $"seed {seed}");
            }
        }

        [Test]
        public void TheColonistOnTheCardIsTheColonistWhoWalks()
        {
            uint[] chosen = { 11u, 222u, 3333u };
            var request = new ColonyRequest
            {
                Size = Size, Seed = 7u, Scenario = Scenario(colonists: 3), Colonists = chosen,
            };
            ColonyWorld colony = ColonyWorld.Build(request);
            colony.World.Tick();

            for (int slot = 0; slot < chosen.Length; slot++)
            {
                Pawn walking = colony.Pawns.Pawns.Get(ColonistDraw.IdForSlot(slot))!;
                Assert.That(walking.Traits, Is.EqualTo(Rolled(chosen[slot], slot).Traits),
                    $"slot {slot}: the select card's traits are the ones she walks with");
            }
        }

        [Test]
        public void TraitsAreDealtOnTheFirstTickAndNeverAgain()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, 7u, Scenario(colonists: 2), barren: true);
            Assert.That(colony.Pawns.Pawns.All.All(p => p.Traits.Count == 0), Is.True,
                "not at placement: that would move every Generated golden for no behaviour");

            colony.World.Tick();
            Pawn first = colony.Pawns.Pawns.All[0];
            Assert.That(first.Traits.Count, Is.InRange(2, 3));

            // A colonist from before traits, loaded past the first tick, is never dealt any: the
            // owner's "new colonies only".
            first.Traits.Clear();
            colony.World.Tick(10);
            Assert.That(first.Traits, Is.Empty);
        }

        [Test]
        public void AScenarioWithoutTraitsDealsNone()
        {
            ScenarioDef scenario = Scenario(colonists: 2);
            scenario.traits = false;
            ColonyWorld colony = ColonyWorld.Build(Size, 7u, scenario, barren: true);
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.All.All(p => p.Traits.Count == 0), Is.True);
        }

        // ---- the five effects --------------------------------------------------------------

        [Test]
        public void AForbiddenWorkTypeIsNeverOfferedAndTakesNoPriority()
        {
            Pawn soft = With(TraitHandle.SoftHands);
            Assert.That(soft.CanDo(WorkTypeIndex.Mining), Is.False);
            Assert.That(soft.WorkPriority(WorkTypeIndex.Mining), Is.EqualTo(0),
                "the scan's one question answers never, whatever is stored");
            Assert.That(soft.WorkPriorities[WorkTypeIndex.Mining], Is.EqualTo(3), "and the stored number is kept");
            Assert.That(soft.WorkPriority(WorkTypeIndex.Cutting), Is.EqualTo(3), "only the work the trait names");

            Pawn control = With();
            Assert.That(control.WorkPriority(WorkTypeIndex.Mining), Is.EqualTo(3));
        }

        [Test]
        public void ARefusedPriorityIsRefusedAtTheIntent()
        {
            ColonyWorld colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.Traits.Clear();
            pawn.Traits.Add(TraitHandle.HamFisted);

            Assert.That(Send(colony, new Intent(IntentKind.SetWorkPriority, default, pawn.Id.Value, WorkHandle.Construction, 1)),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Send(colony, new Intent(IntentKind.SetWorkPriority, default, pawn.Id.Value, WorkHandle.Construction, 0)),
                Is.EqualTo(IntentRejection.None), "never is always allowed");
            Assert.That(Send(colony, new Intent(IntentKind.SetWorkPriority, default, pawn.Id.Value, WorkHandle.Mining, 1)),
                Is.EqualTo(IntentRejection.None), "the control: work she can do");
        }

        [Test]
        public void WorkSpeedIsAFactorOnEveryRate()
        {
            Pawn diligent = With(TraitHandle.Diligent);
            Pawn control = With();
            for (int w = 0; w < WorkTypeIndex.Count; w++)
            {
                int plain = control.WorkRatePerMille(w);
                int fast = diligent.WorkRatePerMille(w);
                int floor = Content.WorkTypes[w].workRateFloorPerMille;
                Assert.That(fast, Is.EqualTo(System.Math.Max(floor, plain * 1200 / 1000)), $"work {w}");
            }
            Assert.That(With(TraitHandle.Unhurried).WorkRatePerMille(WorkTypeIndex.Mining),
                Is.LessThan(control.WorkRatePerMille(WorkTypeIndex.Mining)));
        }

        [Test]
        public void LearningIsAFactorOnExperience()
        {
            Pawn quick = With(TraitHandle.QuickStudy);
            Pawn control = With();
            Assert.That(quick.LearningFactorPerMille(), Is.EqualTo(1750));
            Assert.That(With(TraitHandle.SlowStudy).LearningFactorPerMille(), Is.EqualTo(400));
            Assert.That(control.LearningFactorPerMille(), Is.EqualTo(1000));

            quick.GainExperience(SkillIndex.Mining, 1000, 0);
            control.GainExperience(SkillIndex.Mining, 1000, 0);
            Assert.That(quick.Skills[SkillIndex.Mining], Is.GreaterThan(control.Skills[SkillIndex.Mining]));
        }

        [Test]
        public void MoodAndNerveMoveTheTargetAndTheLines()
        {
            Assert.That(With(TraitHandle.Cheerful).TraitMoodOffset(), Is.EqualTo(60));
            Assert.That(With(TraitHandle.Gloomy).TraitMoodOffset(), Is.EqualTo(-60));

            int minor = Content.Mood.breakThreshold;
            Pawn jumpy = With(TraitHandle.Jumpy);
            Assert.That(jumpy.MinorBreakLine(), Is.EqualTo(minor + 80));
            Assert.That(jumpy.MajorBreakLine(), Is.EqualTo((minor + 80) * 4 / 7), "the other lines follow the minor");
            Assert.That(With(TraitHandle.Steady).MinorBreakLine(), Is.EqualTo(minor - 90));
            Assert.That(With().MinorBreakLine(), Is.EqualTo(minor), "the control");
        }

        [Test]
        public void ACheerfulColonistsTargetIsHigherThroughTheNeedsPass()
        {
            var colony = Colony.Build();
            Pawn cheerful = colony.Ctx.Pawns.Spawn(colony.Cell(4, 4, 0));
            Pawn plain = colony.Ctx.Pawns.Spawn(colony.Cell(10, 10, 0));
            cheerful.Traits.Add(TraitHandle.Cheerful);

            for (int i = 0; i < colony.Needs.IntervalTicks * 2; i++)
            {
                foreach (Pawn p in new[] { cheerful, plain })
                {
                    p.Needs[NeedIndex.Food] = 800;
                    p.Needs[NeedIndex.Rest] = 800;
                    p.Needs[NeedIndex.Joy] = 500;
                }
                colony.World.Tick();
            }

            Assert.That(cheerful.MoodTarget - plain.MoodTarget, Is.EqualTo(60));
        }

        // ---- the save and the hash --------------------------------------------------------

        [Test]
        public void TraitsSurviveASaveAndLoad()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, 7u, Scenario(colonists: 3), barren: true);
            colony.World.Tick();
            List<int>[] dealt = colony.Pawns.Pawns.All.Select(p => p.Traits.ToList()).ToArray();
            ulong hash = colony.World.ComputeStateHash().Value;

            using var stream = new MemoryStream();
            colony.Save(stream);
            stream.Position = 0;
            ColonyWorld restored = ColonyWorld.Build(Size, 7u, Scenario(colonists: 3), barren: true);
            restored.Load(stream);

            for (int i = 0; i < dealt.Length; i++)
                Assert.That(restored.Pawns.Pawns.All[i].Traits, Is.EqualTo(dealt[i]));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(hash));
        }

        [Test]
        public void TheHashSeesTraitsAndATraitlessPawnHashesWithoutThem()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, 7u, Scenario(colonists: 1), barren: true);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assume.That(pawn.Traits.Count, Is.GreaterThan(0));
            ulong dealt = colony.World.ComputeStateHash().Value;

            var held = pawn.Traits.ToList();
            pawn.Traits.Clear();
            ulong bare = colony.World.ComputeStateHash().Value;
            Assert.That(bare, Is.Not.EqualTo(dealt), "saved state that is not derived belongs in the hash");

            pawn.Traits.AddRange(held);
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(dealt));
        }

        [Test]
        public void ADebugSpawnedColonistIsDealtTraitsAndABanditIsNot()
        {
            ColonyWorld colony = Board(colonists: 1);
            colony.World.Tick();
            int before = colony.Pawns.Pawns.All.Count;
            Assert.That(Send(colony, new Intent(IntentKind.SpawnPawn, Size.FromIndex(Near(colony, 3, 0)), 0)), Is.EqualTo(IntentRejection.None));
            Assert.That(Send(colony, new Intent(IntentKind.SpawnPawn, Size.FromIndex(Near(colony, -3, 0)), 3)), Is.EqualTo(IntentRejection.None));

            List<Pawn> spawned = colony.Pawns.Pawns.All.Skip(before).ToList();
            Assert.That(spawned.Single(p => p.IsColonist).Traits.Count, Is.InRange(2, 3));
            Assert.That(spawned.Single(p => p.IsHostile).Traits, Is.Empty);
        }

        // ---- the content ------------------------------------------------------------------

        [Test]
        public void TheTraitHandlesAreTheContentsOrder()
        {
            PawnContent content = Content;
            Assert.That(content.Traits.Length, Is.EqualTo(TraitHandle.Count));
            Assert.That(TraitHandle.Names.Length, Is.EqualTo(TraitHandle.Count));
            for (int t = 0; t < TraitHandle.Count; t++)
                Assert.That(content.Traits[t].defName.ToLowerInvariant(), Is.EqualTo("trait_" + TraitHandle.Names[t]), $"trait {t}");
        }

        [Test]
        public void TheLoaderResolvesSpectraConflictsAndWork()
        {
            PawnContent content = Content;
            Assert.That(content.TraitSpectrum[TraitHandle.Diligent], Is.EqualTo(content.TraitSpectrum[TraitHandle.Unhurried]));
            Assert.That(content.TraitSpectrum[TraitHandle.SoftHands], Is.EqualTo(-1));
            Assert.That(content.TraitConflicts[TraitHandle.SoftHands], Does.Contain(TraitHandle.Tireless),
                "a conflict excludes both ways, whichever Def lists it");
            Assert.That(content.TraitDisabledWork[TraitHandle.BlackThumb], Is.EqualTo(1 << WorkTypeIndex.Growing));
        }

        static ScenarioDef Scenario(int colonists)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            return scenario;
        }
    }
}
