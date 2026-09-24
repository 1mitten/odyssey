#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The draw's simulation half (design 41 §3, §4): traits, the aggregator and its seven seams,
    /// the Standard and Gamble rolls, the Legacy fallback, the save and the hash.
    /// </summary>
    public class DrawTests
    {
        const int Seeds = 400;

        static PawnContent Content => ContentPack.Pawns();

        static Pawn Rolled(uint seed, int slot, RollProfile profile) => ColonistDraw.Roll(seed, slot, profile);

        static int Level(Pawn pawn, int skill) => pawn.SkillLevel(skill);

        static bool InBudget(int skill) => !Content.Skills[skill].outsideBudget;

        // ---- the content ---------------------------------------------------------------------------

        [Test]
        public void EveryTraitHandleHasItsDefInOrder()
        {
            Assert.That(Content.Traits.Length, Is.EqualTo(TraitHandle.Count));
            for (int t = 0; t < TraitHandle.Count; t++)
                Assert.That(Content.Traits[t].defName, Is.EqualTo(TraitHandle.DefNames[t]), $"handle {t}");
            Assert.That(TraitHandle.Keys.Length, Is.EqualTo(TraitHandle.Count));
        }

        [Test]
        public void OppositesArePairsAndMildWorthsNetToZero()
        {
            for (int t = 0; t < TraitHandle.Count; t++)
            {
                int o = Content.TraitOpposite[t];
                Assert.That(o, Is.GreaterThanOrEqualTo(0), $"{TraitHandle.DefNames[t]} has no opposite");
                Assert.That(Content.TraitOpposite[o], Is.EqualTo(t), $"{TraitHandle.DefNames[t]}'s opposite does not point back");
                Assert.That(Content.Traits[o].pool, Is.EqualTo(Content.Traits[t].pool));
                if (Content.Traits[t].pool == TraitPool.Mild)
                    Assert.That(Content.Traits[t].worth + Content.Traits[o].worth, Is.Zero);
            }
        }

        [Test]
        public void HaulingIsTheOneSkillOutsideTheBudget()
        {
            for (int s = 0; s < SkillIndex.Count; s++)
                Assert.That(Content.Skills[s].outsideBudget, Is.EqualTo(s == SkillIndex.Hauling), Content.Skills[s].defName);
        }

        // ---- Standard: everyone averages out -------------------------------------------------------

        [Test]
        public void EveryStandardColonistIsDealtExactlyTheBudget()
        {
            var kind = Content.Kind;
            var shapes = new HashSet<string>();
            for (uint seed = 1; seed <= Seeds; seed++)
            for (int slot = 0; slot < 3; slot++)
            {
                Pawn p = Rolled(seed, slot, RollProfile.Standard);
                int total = 0;
                var shape = new List<int>();
                for (int s = 0; s < SkillIndex.Count; s++)
                {
                    if (!InBudget(s)) continue;
                    int level = Level(p, s);
                    Assert.That(level, Is.InRange(0, kind.standardSkillCap), $"seed {seed} slot {slot} skill {s}");
                    total += level;
                    shape.Add(level);
                }

                Assert.That(total, Is.EqualTo(kind.standardSkillBudget), $"seed {seed} slot {slot}");
                shapes.Add(string.Join(",", shape));
            }

            // The negative control: a budget dealt the same way every time would pass the sum.
            Assert.That(shapes.Count, Is.GreaterThan(100), "Standard deals the same shape to everybody");
        }

        [Test]
        public void EveryStandardColonistHasOneMajorAndOneMinorOnBudgetSkills()
        {
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Pawn p = Rolled(seed, (int)(seed % 3), RollProfile.Standard);
                int majors = 0, minors = 0;
                for (int s = 0; s < SkillIndex.Count; s++)
                {
                    Passion passion = p.PassionFor(s);
                    if (passion == Passion.None) continue;
                    Assert.That(InBudget(s), Is.True, $"seed {seed}: a passion on hauling");
                    if (passion == Passion.Major) majors++; else minors++;
                }

                Assert.That(majors, Is.EqualTo(1), $"seed {seed}");
                Assert.That(minors, Is.EqualTo(1), $"seed {seed}");
            }
        }

        [Test]
        public void AStandardMajorPassionTendsToSitOnWhatSheIsGoodAt()
        {
            // Weighted by level + 1: over many colonists the major's skill is above the colonist's
            // mean budget level far more often than chance would put it there.
            int above = 0, trials = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Pawn p = Rolled(seed, 0, RollProfile.Standard);
                int major = Enumerable.Range(0, SkillIndex.Count).First(s => p.PassionFor(s) == Passion.Major);
                double mean = Content.Kind.standardSkillBudget / 5.0;
                trials++;
                if (Level(p, major) > mean) above++;
            }

            Assert.That(above, Is.GreaterThan(trials * 55 / 100), $"{above} of {trials}");
        }

        [Test]
        public void EveryStandardColonistHasOneMildGoodAndOneMildBadThatAreNotOpposites()
        {
            var goods = new HashSet<int>();
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Pawn p = Rolled(seed, (int)(seed % 3), RollProfile.Standard);
                Assert.That(p.Traits.Length, Is.EqualTo(2), $"seed {seed}");
                TraitDef good = Content.Traits[p.Traits[0]], bad = Content.Traits[p.Traits[1]];
                Assert.That(good.pool, Is.EqualTo(TraitPool.Mild));
                Assert.That(bad.pool, Is.EqualTo(TraitPool.Mild));
                Assert.That(good.worth, Is.GreaterThan(0));
                Assert.That(bad.worth, Is.LessThan(0));
                Assert.That(Content.TraitOpposite[p.Traits[0]], Is.Not.EqualTo(p.Traits[1]), $"seed {seed}: a pair that cancels");
                Assert.That(System.Math.Abs(good.worth + bad.worth), Is.LessThanOrEqualTo(1));
                goods.Add(p.Traits[0]);
            }

            Assert.That(goods.Count, Is.EqualTo(6), "every mild good trait is dealt by somebody");
        }

        [Test]
        public void AStandardColonistWalksInTheNarrowBand()
        {
            var kind = Content.Kind;
            int lowest = int.MaxValue, highest = int.MinValue;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                int pace = Rolled(seed, 0, RollProfile.Standard).InnatePacePerMille();
                Assert.That(pace, Is.InRange(kind.standardPaceMinPerMille, kind.standardPaceMaxPerMille));
                lowest = System.Math.Min(lowest, pace);
                highest = System.Math.Max(highest, pace);
            }

            // The control: a Gamble colonist uses the whole movement band.
            bool wider = false;
            for (uint seed = 1; seed <= Seeds && !wider; seed++)
            {
                int pace = Rolled(seed, 0, RollProfile.Gamble).InnatePacePerMille();
                wider = pace < kind.standardPaceMinPerMille || pace > kind.standardPaceMaxPerMille;
            }

            Assert.That(wider, Is.True, "Gamble never left Standard's band");
            Assert.That(highest - lowest, Is.GreaterThan(50), "the band is not being used");
        }

        // ---- Gamble -------------------------------------------------------------------------------

        [Test]
        public void AGamblePullStaysInsideItsTables()
        {
            int top = Content.Kind.gambleSkillLevelWeights.Length - 1;
            bool reachedPastStandard = false, sawExtreme = false, sawNone = false, sawThree = false;
            for (uint seed = 1; seed <= Seeds * 3; seed++)
            {
                Pawn p = Rolled(seed, (int)(seed % 3), RollProfile.Gamble);
                for (int s = 0; s < SkillIndex.Count; s++)
                {
                    if (!InBudget(s)) continue;
                    Assert.That(Level(p, s), Is.InRange(0, top));
                    if (Level(p, s) > Content.Kind.standardSkillCap) reachedPastStandard = true;
                }

                Assert.That(p.Traits.Length, Is.InRange(0, TraitHandle.MaxPerPawn));
                for (int i = 0; i < p.Traits.Length; i++)
                for (int j = i + 1; j < p.Traits.Length; j++)
                {
                    Assert.That(p.Traits[i], Is.Not.EqualTo(p.Traits[j]), $"seed {seed}: dealt twice");
                    Assert.That(Content.TraitOpposite[p.Traits[i]], Is.Not.EqualTo(p.Traits[j]), $"seed {seed}: both halves of a pair");
                }

                if (p.Traits.Any(t => Content.Traits[t].pool == TraitPool.Extreme)) sawExtreme = true;
                if (p.Traits.Length == 0) sawNone = true;
                if (p.Traits.Length == 3) sawThree = true;
            }

            Assert.That(reachedPastStandard, Is.True, "Gamble never passed Standard's ceiling");
            Assert.That(sawExtreme, Is.True, "Gamble never dealt an extreme");
            Assert.That(sawNone && sawThree, Is.True, "Gamble never used the whole trait count");
        }

        [Test]
        public void StandardNeverDealsAnExtreme()
        {
            for (uint seed = 1; seed <= Seeds; seed++)
                foreach (int t in Rolled(seed, 1, RollProfile.Standard).Traits)
                    Assert.That(Content.Traits[t].pool, Is.EqualTo(TraitPool.Mild), $"seed {seed}");
        }

        // ---- Legacy -------------------------------------------------------------------------------

        [Test]
        public void LegacyDealsNoTraitsAndRollsAsItAlwaysDid()
        {
            var fresh = new Pawn(new PawnId(1), -1, Content) { RollSeed = 99u };
            Assert.That(fresh.Profile, Is.EqualTo(RollProfile.Legacy), "an unclassified pawn is not Legacy");
            fresh.RollTraits();
            Assert.That(fresh.Traits, Is.Empty);

            // The legacy skills: one independent draw a skill from the old table, which can put
            // levels on hauling and leave the budget skills summing to anything.
            fresh.RollStartingSkills();
            var standard = Rolled(99u, 0, RollProfile.Standard);
            Assert.That(fresh.Skills, Is.Not.EqualTo(standard.Skills), "Legacy rolled Standard's skills");
            Assert.That(fresh.InnatePacePerMille(), Is.InRange(Content.Movement.innatePaceMinPerMille, Content.Movement.innatePaceMaxPerMille));
        }

        [Test]
        public void PlacementRollsStandardUnlessTheScreenOrTheScenarioSaysOtherwise()
        {
            var size = new GridSize(60, 60, 16);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 2;
            scenario.beds = 2;
            ColonyWorld standard = ColonyWorld.Build(size, 5u, scenario, barren: true, wooded: false);
            foreach (Pawn p in standard.Pawns.Pawns.All.Where(p => p.IsPerson))
            {
                Assert.That(p.Profile, Is.EqualTo(RollProfile.Standard));
                Assert.That(p.Traits.Length, Is.EqualTo(2), "a placed colonist has no traits");
            }

            ScenarioDef legacy = ScenarioDef.Bare();
            legacy.colonists = 2;
            legacy.beds = 2;
            legacy.colonistProfile = RollProfile.Legacy;
            ColonyWorld old = ColonyWorld.Build(size, 5u, legacy, barren: true, wooded: false);
            foreach (Pawn p in old.Pawns.Pawns.All.Where(p => p.IsPerson))
                Assert.That(p.Traits, Is.Empty, "the scenario's Legacy was ignored");
        }

        // ---- the aggregator ------------------------------------------------------------------------

        [Test]
        public void TraitsMultiplyFactorsAndSumOffsets()
        {
            var p = new Pawn(new PawnId(1), -1, Content);
            Assert.That(p.TraitFactorPerMille(TraitStat.WorkSpeed), Is.EqualTo(1_000), "the control: no traits");

            p.Traits = new[] { TraitHandle.Diligent, TraitHandle.Butterfingers };
            Assert.That(p.TraitFactorPerMille(TraitStat.WorkSpeed), Is.EqualTo(1_150 * 750 / 1_000));
            Assert.That(p.TraitFactorPerMille(TraitStat.MeleeDamage), Is.EqualTo(700));
            Assert.That(p.TraitFactorPerMille(TraitStat.Learning), Is.EqualTo(1_000));

            p.Traits = new[] { TraitHandle.Sunny, TraitHandle.Unshakeable };
            Assert.That(p.TraitOffset(TraitStat.Mood), Is.EqualTo(180));
            Assert.That(p.TraitOffset(TraitStat.WorkSpeed), Is.Zero);
        }

        /// <summary>
        /// The one-owner rule (design 41 §3.2), read from the source: nothing in the simulation walks
        /// a pawn's trait list but the aggregator on <c>Pawn</c>, the save section and the publish.
        /// </summary>
        [Test]
        public void NothingButTheAggregatorReadsAPawnsTraits()
        {
            string sim = SimSourceRoot();
            var allowed = new HashSet<string> { "Pawn.cs", "PawnDrawSections.cs", "PawnRegistry.cs" };
            var reader = new Regex(@"\b(?!Content\b|content\b)\w+\.Traits\b");
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(sim, "*.cs", SearchOption.AllDirectories))
            {
                if (allowed.Contains(Path.GetFileName(file))) continue;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (reader.IsMatch(lines[i]) && !lines[i].TrimStart().StartsWith("//"))
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }

            Assert.That(offenders, Is.Empty, "ask Pawn.TraitFactorPerMille or TraitOffset instead");
        }

        // ---- the seams -----------------------------------------------------------------------------

        [Test]
        public void WorkSpeedTakesTheTraitFactor()
        {
            var colony = Board();
            Pawn p = colony.Pawns.Pawns.All[0];
            int plain = p.WorkRatePerMille(WorkTypeIndex.Mining);
            p.Traits = new[] { TraitHandle.Diligent };
            Assert.That(p.WorkRatePerMille(WorkTypeIndex.Mining), Is.EqualTo(plain * 1_150 / 1_000));
            p.Traits = new[] { TraitHandle.Wreck };
            Assert.That(p.WorkRatePerMille(WorkTypeIndex.Mining),
                Is.EqualTo(System.Math.Max(plain * 600 / 1_000, Content.WorkTypes[WorkTypeIndex.Mining].workRateFloorPerMille)));
        }

        [Test]
        public void PaceTakesTheTraitFactorAndNeverLeavesTheWalkCyclesBand()
        {
            var m = Content.Movement;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Pawn p = Rolled(seed, 0, RollProfile.Gamble);
                p.Traits = System.Array.Empty<int>();   // a pull may have dealt a stride already
                int innate = p.InnatePacePerMille();
                Assert.That(p.PacePerMille(), Is.EqualTo(innate), "the control: no trait, no change");
                p.Traits = new[] { TraitHandle.LongStride };
                int expected = System.Math.Min(m.innatePaceMaxPerMille, innate * 1_050 / 1_000);
                Assert.That(p.PacePerMille(), Is.EqualTo(expected), $"seed {seed}");
                p.Traits = new[] { TraitHandle.ShortStride };
                Assert.That(p.PacePerMille(), Is.InRange(m.innatePaceMinPerMille, m.innatePaceMaxPerMille));
            }
        }

        [Test]
        public void LearningTakesTheTraitFactor()
        {
            var plain = new Pawn(new PawnId(1), -1, Content);
            var quick = new Pawn(new PawnId(2), -1, Content) { Traits = new[] { TraitHandle.QuickStudy } };
            var prodigy = new Pawn(new PawnId(3), -1, Content) { Traits = new[] { TraitHandle.Prodigy } };
            foreach (var p in new[] { plain, quick, prodigy })
            {
                p.Passions[SkillIndex.Mining] = (byte)Passion.Minor;
                p.GainExperience(SkillIndex.Mining, 1_000, currentTick: 0);
            }

            Assert.That(plain.Skills[SkillIndex.Mining], Is.EqualTo(1_000));
            Assert.That(quick.Skills[SkillIndex.Mining], Is.EqualTo(1_400));
            Assert.That(prodigy.Skills[SkillIndex.Mining], Is.EqualTo(2_000));
        }

        [Test]
        public void AFallsFractionIsSpentOverTheIntervalsNotLost()
        {
            var p = new Pawn(new PawnId(1), -1, Content) { Traits = new[] { TraitHandle.BigAppetite } };
            // A food level in a band that falls by one a step: ×1.2 of 1 truncates to nothing.
            int food = 600;
            p.Needs[NeedIndex.Food] = food;
            int one = p.NeedFallPerInterval(NeedIndex.Food);
            int total = 0;
            for (int i = 0; i < 1_000; i++) total += p.NeedFallPerInterval(NeedIndex.Food, i);
            Assert.That(total, Is.EqualTo(one * 1_200), "a thousand intervals did not fall 1.2 times as far");

            var plain = new Pawn(new PawnId(2), -1, Content);
            plain.Needs[NeedIndex.Food] = food;
            int plainTotal = 0;
            for (int i = 0; i < 1_000; i++) plainTotal += plain.NeedFallPerInterval(NeedIndex.Food, i);
            Assert.That(plainTotal, Is.EqualTo(one * 1_000), "the control: no trait, no change");

            var tireless = new Pawn(new PawnId(3), -1, Content) { Traits = new[] { TraitHandle.Tireless } };
            tireless.Needs[NeedIndex.Rest] = 600;
            int rest = tireless.NeedFallPerInterval(NeedIndex.Rest);
            int restTotal = 0;
            for (int i = 0; i < 1_000; i++) restTotal += tireless.NeedFallPerInterval(NeedIndex.Rest, i);
            Assert.That(restTotal, Is.EqualTo(rest * 500));
        }

        [Test]
        public void MoodTakesTheTraitOffset()
        {
            var sunny = Board(colonists: 1, seed: 3u);
            var dour = Board(colonists: 1, seed: 3u);
            var plain = Board(colonists: 1, seed: 3u);
            sunny.Pawns.Pawns.All[0].Traits = new[] { TraitHandle.Sunny };
            dour.Pawns.Pawns.All[0].Traits = new[] { TraitHandle.Dour };
            for (int t = 0; t < 400; t++)
            {
                sunny.World.Tick();
                dour.World.Tick();
                plain.World.Tick();
            }

            int baseline = plain.Pawns.Pawns.All[0].MoodTarget;
            Assert.That(sunny.Pawns.Pawns.All[0].MoodTarget, Is.EqualTo(baseline + 60));
            Assert.That(dour.Pawns.Pawns.All[0].MoodTarget, Is.EqualTo(baseline - 60));
        }

        [Test]
        public void MeleeDamageTakesTheAttackersFactor()
        {
            var colony = Board();
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            var rules = new MeleeRules();
            Armament machete = Weapon(colony.Pawns, ItemHandle.Machete);
            int compared = 0;
            for (int t = 0; t < 400; t++)
            {
                a.Traits = System.Array.Empty<int>();
                SwingOutcome plain = rules.Resolve(a, b, machete, colony.Pawns, 20_000 + t);
                a.Traits = new[] { TraitHandle.Scrapper };
                SwingOutcome hard = rules.Resolve(a, b, machete, colony.Pawns, 20_000 + t);
                Assert.That(hard.Landed, Is.EqualTo(plain.Landed), "the trait changed whether it landed");
                if (!plain.Landed || plain.Critical) continue;
                Assert.That(hard.DamageMilli, Is.EqualTo((int)((long)plain.DamageMilli * 1_200 / 1_000)), $"tick {t}");
                compared++;
            }

            Assert.That(compared, Is.GreaterThan(50));
        }

        // ---- save and hash -------------------------------------------------------------------------

        [Test]
        public void TheProfileAndTraitsRoundTrip()
        {
            var size = new GridSize(60, 60, 16);
            ColonyWorld Build() => ColonyWorld.Build(new ColonyRequest
            {
                Size = size, Seed = 17u, Scenario = Scenario(3),
                Colonists = new[] { 5u, 6u, 7u },
                Profiles = new[] { RollProfile.Gamble, RollProfile.Standard, RollProfile.Gamble },
            });

            ColonyWorld before = Build();
            before.World.Tick();
            Pawn[] people = before.Pawns.Pawns.All.Where(p => p.IsPerson).ToArray();
            people[1].Traits = new[] { TraitHandle.Prodigy, TraitHandle.Tireless, TraitHandle.Idle };
            byte[] bytes = before.Save();

            ColonyWorld after = Build();
            // Scramble what the build dealt so the load is what has to put it back.
            foreach (Pawn p in after.Pawns.Pawns.All) { p.Profile = RollProfile.Legacy; p.Traits = System.Array.Empty<int>(); }
            after.Load(bytes);

            Pawn[] loaded = after.Pawns.Pawns.All.Where(p => p.IsPerson).ToArray();
            for (int i = 0; i < people.Length; i++)
            {
                Assert.That(loaded[i].Profile, Is.EqualTo(people[i].Profile), $"colonist {i}");
                Assert.That(loaded[i].Traits, Is.EqualTo(people[i].Traits), $"colonist {i}");
                Assert.That(loaded[i].InnatePacePerMille(), Is.EqualTo(people[i].InnatePacePerMille()), $"colonist {i}'s pace");
            }

            Assert.That(after.World.ComputeStateHash(), Is.EqualTo(before.World.ComputeStateHash()));
        }

        [Test]
        public void ASaveFromBeforeTheDrawLoadsAsLegacyWithNoTraits()
        {
            var size = new GridSize(60, 60, 16);
            ColonyWorld before = ColonyWorld.Build(size, 17u, Scenario(2), barren: true, wooded: false);
            before.World.Tick();
            var old = before.SaveComponents.Where(c => c.SaveKey != "odyssey.pawn.profile" && c.SaveKey != "odyssey.pawn.traits").ToList();
            Assert.That(old.Count, Is.EqualTo(before.SaveComponents.Count - 2), "the sections were not there to leave out");
            byte[] bytes;
            using (var buffer = new MemoryStream())
            {
                WorldSave.Save(before.World, buffer, old);
                bytes = buffer.ToArray();
            }

            ColonyWorld after = ColonyWorld.Build(size, 17u, Scenario(2), barren: true, wooded: false);
            after.Load(bytes);
            Pawn[] was = before.Pawns.Pawns.All.Where(p => p.IsPerson).ToArray();
            Pawn[] now = after.Pawns.Pawns.All.Where(p => p.IsPerson).ToArray();
            Assert.That(was.All(p => p.Profile == RollProfile.Standard && p.Traits.Length == 2), Is.True,
                "the control: the colony that was saved had been dealt the draw");
            for (int i = 0; i < now.Length; i++)
            {
                // The loader builds each pawn fresh and no section writes into it, so it is the
                // constructor's Legacy with nothing dealt — exactly what a colony from before was.
                Assert.That(now[i].Profile, Is.EqualTo(RollProfile.Legacy), $"colonist {i}");
                Assert.That(now[i].Traits, Is.Empty, $"colonist {i}");
                // And its skills are the saved ones: the record carries them, the profile does not.
                Assert.That(now[i].Skills, Is.EqualTo(was[i].Skills), $"colonist {i}");
            }
        }

        [Test]
        public void ATraitAndAProfileBothMoveTheHash()
        {
            ColonyWorld colony = Board(colonists: 1);
            Pawn p = colony.Pawns.Pawns.All[0];
            ulong start = colony.World.ComputeStateHash().Value;

            p.Traits = new[] { TraitHandle.Sunny };
            ulong traited = colony.World.ComputeStateHash().Value;
            Assert.That(traited, Is.Not.EqualTo(start), "a trait is outside the hash");

            p.Profile = RollProfile.Gamble;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(traited), "the profile is outside the hash");

            p.Profile = RollProfile.Legacy;
            p.Traits = System.Array.Empty<int>();
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(start), "the control: put back, the hash comes back");
        }

        // ---- the card is the colonist --------------------------------------------------------------

        [Test]
        public void AGambleColonyIsTheColonistsTheMachineLandedOn()
        {
            uint[] chosen = { 101u, 202u, 303u };
            RollProfile[] profiles = { RollProfile.Gamble, RollProfile.Gamble, RollProfile.Gamble };
            var cards = new Pawn[chosen.Length];
            for (int slot = 0; slot < chosen.Length; slot++) cards[slot] = Rolled(chosen[slot], slot, profiles[slot]);

            ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
            {
                Size = new GridSize(60, 60, 16), Seed = 4242u, Scenario = Scenario(3),
                Barren = true, Wooded = true, Colonists = chosen, Profiles = profiles,
            });
            colony.World.Tick();
            Pawn[] placed = colony.Pawns.Pawns.All.Where(p => p.IsPerson).ToArray();

            for (int slot = 0; slot < chosen.Length; slot++)
            {
                Assert.That(placed[slot].Profile, Is.EqualTo(RollProfile.Gamble));
                Assert.That(placed[slot].Skills, Is.EqualTo(cards[slot].Skills), $"slot {slot}: skills");
                Assert.That(placed[slot].Passions, Is.EqualTo(cards[slot].Passions), $"slot {slot}: passions");
                Assert.That(placed[slot].Traits, Is.EqualTo(cards[slot].Traits), $"slot {slot}: traits");
                Assert.That(placed[slot].InnatePacePerMille(), Is.EqualTo(cards[slot].InnatePacePerMille()), $"slot {slot}: pace");
            }
        }

        /// <summary>
        /// The same guarantee on the scenario the game actually builds (Playtest, wooded, the
        /// played board's size), rather than on a bare fixture: a PlayMode run found a Construction
        /// level differing from the card here, and this is the fast-tier question it asked.
        /// </summary>
        [Test]
        public void AGambleColonyOnThePlayedScenarioIsTheCards()
        {
            uint[] chosen = { 3_402_411_231u, 77u, 918_273u };
            RollProfile[] profiles = { RollProfile.Gamble, RollProfile.Gamble, RollProfile.Gamble };
            ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
            {
                Size = new GridSize(120, 120, 16), Seed = 555u,
                Scenario = ScenarioDef.Playtest().WithColonists(3),
                Barren = true, Wooded = true, Colonists = chosen, Profiles = profiles,
            });
            colony.World.Tick();
            Pawn[] placed = colony.Pawns.Pawns.All.Where(p => p.IsPerson).ToArray();
            for (int slot = 0; slot < chosen.Length; slot++)
            {
                Pawn card = Rolled(chosen[slot], slot, RollProfile.Gamble);
                string real = string.Join(",", Enumerable.Range(0, SkillIndex.Count).Select(s => placed[slot].SkillLevel(s)));
                string shown = string.Join(",", Enumerable.Range(0, SkillIndex.Count).Select(s => card.SkillLevel(s)));
                Assert.That(placed[slot].Id.Value, Is.EqualTo(card.Id.Value), $"slot {slot}: ids");
                Assert.That(real, Is.EqualTo(shown), $"slot {slot}: the colony's levels against the card's");
            }
        }

        /// <summary>
        /// The played scene starts its clock at noon (<c>ColonyRequest.StartTick</c>), and the
        /// starting-skill roll fired only when the tick read zero — so every colonist in every played
        /// game had no skills at all, while every headless run and golden, which start at zero, had
        /// them. Found by the draw's PlayMode test (design 41 §6.7): the colony's skills were all 0
        /// against the card's.
        /// </summary>
        [Test]
        public void AColonyStartedAtNoonStillRollsItsColonistsSkills()
        {
            uint[] chosen = { 101u, 202u, 303u };
            ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
            {
                Size = new GridSize(60, 60, 16), Seed = 4242u, Scenario = Scenario(3),
                Barren = true, Wooded = true, Colonists = chosen, StartTick = 30_000,
            });
            Assert.That(colony.World.CurrentTick, Is.EqualTo(30_000), "the control: the clock starts at noon");
            colony.World.Tick();

            Pawn[] placed = colony.Pawns.Pawns.All.Where(p => p.IsPerson).ToArray();
            for (int slot = 0; slot < chosen.Length; slot++)
                Assert.That(placed[slot].Skills, Is.EqualTo(Rolled(chosen[slot], slot, RollProfile.Standard).Skills),
                    $"slot {slot}: a colony that began at noon has no starting skills");
        }

        [Test]
        public void TheStartingRollNeverOverwritesEarnedExperienceWhenItFiresAgainAfterALoad()
        {
            ColonyWorld colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn p = colony.Pawns.Pawns.All[0];
            p.Skills[SkillIndex.Mining] += 1_234;
            int[] before = (int[])p.Skills.Clone();
            byte[] bytes = colony.Save();

            ColonyWorld after = Board(colonists: 1);
            after.Load(bytes);
            after.World.Tick();   // a fresh system: fires on the first tick after the load
            Assert.That(after.Pawns.Pawns.All[0].Skills, Is.EqualTo(before), "the roll wrote over what she had");
        }

        [Test]
        public void ADebugSpawnedColonistArrivesWithSkillsAfterTickZero()
        {
            ColonyWorld colony = Board(colonists: 1);
            for (int t = 0; t < 5; t++) colony.World.Tick();
            CellRef at = Size.FromIndex(colony.Pawns.Pawns.All[0].Cell);
            int before = colony.Pawns.Pawns.Count;
            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, 0));
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1));

            Pawn spawned = colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1];
            int total = 0;
            for (int s = 0; s < SkillIndex.Count; s++) if (InBudget(s)) total += spawned.SkillLevel(s);
            Assert.That(total, Is.EqualTo(Content.Kind.standardSkillBudget), "a colonist spawned after tick zero had no skills");
            Assert.That(spawned.Traits.Length, Is.EqualTo(2));
        }

        static string SimSourceRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Assets", "Odyssey", "Sim");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("could not find Assets/Odyssey/Sim from " + TestContext.CurrentContext.TestDirectory);
        }

        static ScenarioDef Scenario(int colonists) => new ScenarioDef
        {
            defName = "Scenario_Bare", label = "bare",
            colonists = colonists, beds = colonists,
            startingFellRadius = 0, startingMineRadius = 0, startingMineOutcrops = 0,
        };
    }
}
