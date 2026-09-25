#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// U20: skills are experience a colonist earns by working, a level read off that experience
    /// by a table, and a passion that scales what each tick of work is worth (OQ-14).
    ///
    /// <para>The expected numbers come from <c>docs/research/a-01-pawns.md</c>, "Skills,
    /// passions, learning", never from the code under test: a level costs 1,000 × (L + 1) points
    /// and 265,000 reaches 20; passion multiplies gain by 0.35, 1.0 and 1.5; the daily soft cap
    /// is 4,000 points, with gains beyond it at a fifth; and decay applies from level 10, at 30
    /// points a day there and 3,600 a day at 20. Experience is stored in thousandths of a point
    /// so that every one of those is an integer, the way a need is thousandths of a bar.</para>
    /// </summary>
    public class SkillTests
    {
        const int Point = 1_000;

        static PawnContent Content => ContentPack.Pawns();

        static Pawn Lone(Colony colony) => colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));

        // ------------------------------------------------------------------ the published names

        /// <summary>
        /// Skills reach the interface as pawn aspects, and the aspect's <i>name</i> is the whole
        /// of the contract — <c>Odyssey.Tests.Hud</c> cannot reference this assembly at all, so it
        /// mints its keys from the same literals and the two halves can only agree by agreeing on
        /// the spelling.
        ///
        /// <para>The literals are written out here rather than taken from <c>SkillAspects</c>,
        /// because a test that asks the thing under test what it is called agrees with itself
        /// whatever it has been renamed to. Renaming a skill is a change to the interface's copy
        /// too, and this is what says so.</para>
        /// </summary>
        [Test]
        public void SkillsArePublishedUnderTheNamesTheInterfaceReads()
        {
            Assert.That(SkillIndex.Names.Length, Is.EqualTo(SkillIndex.Count),
                "a skill with no name cannot be published at all");

            Assert.That(SkillAspects.Name("mining", "level"),
                Is.EqualTo("odyssey.pawn.skill.mining.level"));
            Assert.That(SkillAspects.Name("cutting", "passion"),
                Is.EqualTo("odyssey.pawn.skill.cutting.passion"));
            Assert.That(SkillAspects.Name("hauling", "experience"),
                Is.EqualTo("odyssey.pawn.skill.hauling.experience"));

            Assert.That(SkillAspects.Level[SkillIndex.Mining],
                Is.EqualTo(AspectKey.Of("odyssey.pawn.skill.mining.level")));
            Assert.That(SkillAspects.Passion[SkillIndex.Cutting],
                Is.EqualTo(AspectKey.Of("odyssey.pawn.skill.cutting.passion")));
            Assert.That(SkillAspects.Experience[SkillIndex.Hauling],
                Is.EqualTo(AspectKey.Of("odyssey.pawn.skill.hauling.experience")));
        }

        /// <summary>
        /// <b>Every skill the simulation trains is written out here, and the list is the tripwire.</b>
        ///
        /// <para>This is the test the growing branch would have failed. <c>SkillCatalogue</c>, over
        /// in <c>Odyssey.Hud</c>, decides whether a skill's row on the colonist pane is live or
        /// greyed out with an excuse beside it, and that decision is a claim about <i>this</i>
        /// assembly. Construction was greyed out from U26 to 2026-09-20 and Growing from U47,
        /// because adding a skill here touched nothing that would notice.</para>
        ///
        /// <para>The two halves cannot reference each other — that is the point of
        /// <see cref="PawnAspect"/> — so the guard is a pair of pins facing each other across the
        /// name. This one fails the moment the simulation grows a skill; its message says where to
        /// go. <c>Odyssey.Tests.Hud.SkillCatalogueTests</c> is the other half.</para>
        /// </summary>
        [Test]
        public void EverySkillTheSimulationTrainsIsNamedHere()
        {
            Assert.That(SkillIndex.Names, Is.EqualTo(new[]
                {
                    "hauling", "cutting", "mining", "construction", "growing",
                    // The combat contracts step (design 33 §5): SkillCatalogue's melee row went
                    // live in the same commit, and SkillCatalogueTests with it.
                    "melee",
                    // Health's H3 (design 43 §5): SkillCatalogue's medicine row went live in the
                    // same commit, and SkillCatalogueTests with it.
                    "medicine",
                }),
                "the simulation's skills have changed. A skill that trains is a skill the " +
                "colonist pane must stop calling unavailable: add or remove the matching live " +
                "row in Odyssey.Hud.SkillCatalogue.All and update SkillCatalogueTests, which " +
                "pins the other end of this contract. Do not simply re-bake this list.");
        }

        /// <summary>
        /// A colonist's skills are on the published frame, under those names, with the level
        /// derived rather than left for the reader to work out — the ladder that derives it is
        /// simulation content and is not published.
        /// </summary>
        [Test]
        public void ThePublishedFrameCarriesEveryColonistsSkills()
        {
            var size = new GridSize(40, 40, 8);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            ColonyWorld colony = ColonyWorld.Build(size, 1u, scenario, barren: true);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.Skills[SkillIndex.Mining] = 6 * Point;
            pawn.Passions[SkillIndex.Mining] = (byte)Passion.Major;

            colony.World.Tick();
            WorldSnapshot frame = colony.World.Views.Current;

            Assert.That(frame.TryGetPawnAspect(
                pawn.Id, AspectKey.Of("odyssey.pawn.skill.mining.experience"), out int experience),
                Is.True, "no skill experience reached the frame");
            Assert.That(experience, Is.EqualTo(6 * Point));

            Assert.That(frame.TryGetPawnAspect(
                pawn.Id, AspectKey.Of("odyssey.pawn.skill.mining.level"), out int level), Is.True);
            Assert.That(level, Is.EqualTo(pawn.SkillLevel(SkillIndex.Mining)),
                "the level is derived by the simulation, which owns the ladder");

            Assert.That(frame.TryGetPawnAspect(
                pawn.Id, AspectKey.Of("odyssey.pawn.skill.mining.passion"), out int passion), Is.True);
            Assert.That(passion, Is.EqualTo((int)Passion.Major));
        }

        // ------------------------------------------------------------------ levels

        [Test]
        public void LevelIsReadOffExperienceByTheDefTable()
        {
            // Per-level cost 1,000; 2,000; 3,000 ... (a-01), so the floors are 0; 1,000; 3,000;
            // 6,000; level 10 begins at 55,000 and level 20 at 265,000 points. The first draft
            // of the table used a-01's formula alone and reached 20 at 210,000; the cumulative
            // figure is the one a-01 states outright, so it is the one asserted.
            SkillDef def = Content.Skills[SkillIndex.Cutting];

            Assert.That(def.Level(0), Is.Zero);
            Assert.That(def.Level(1_000 * Point - 1), Is.Zero);
            Assert.That(def.Level(1_000 * Point), Is.EqualTo(1));
            Assert.That(def.Level(3_000 * Point - 1), Is.EqualTo(1));
            Assert.That(def.Level(3_000 * Point), Is.EqualTo(2));
            Assert.That(def.Level(6_000 * Point), Is.EqualTo(3));
            Assert.That(def.ExperienceForLevel(10), Is.EqualTo(55_000 * Point));
            Assert.That(def.ExperienceForLevel(20), Is.EqualTo(265_000 * Point));
            Assert.That(def.Level(265_000 * Point), Is.EqualTo(20));
            Assert.That(def.Level(int.MaxValue), Is.EqualTo(20), "twenty is the ceiling");
        }

        [Test]
        public void ALevelIsNeverStoredOnThePawn()
        {
            // The level is a function of experience and nothing else, so writing experience is
            // enough to change it and there is no second field to fall out of step.
            var colony = Colony.Build();
            Pawn pawn = Lone(colony);
            Assert.That(pawn.SkillLevel(SkillIndex.Hauling), Is.Zero);

            pawn.Skills[SkillIndex.Hauling] = 6_000 * Point;
            Assert.That(pawn.SkillLevel(SkillIndex.Hauling), Is.EqualTo(3));
        }

        // ------------------------------------------------------------------ passion

        [Test]
        public void PassionMultipliesTheGain()
        {
            // none ×0.35, minor ×1.0, major ×1.5 (a-01). One grant of 1,000 points each, so the
            // per-mille arithmetic has nothing to round.
            var colony = Colony.Build();
            Pawn none = Lone(colony), minor = Lone(colony), major = Lone(colony);
            none.Passions[SkillIndex.Cutting] = (byte)Passion.None;
            minor.Passions[SkillIndex.Cutting] = (byte)Passion.Minor;
            major.Passions[SkillIndex.Cutting] = (byte)Passion.Major;

            none.GainExperience(SkillIndex.Cutting, 1_000 * Point, currentTick: 0);
            minor.GainExperience(SkillIndex.Cutting, 1_000 * Point, currentTick: 0);
            major.GainExperience(SkillIndex.Cutting, 1_000 * Point, currentTick: 0);

            Assert.That(none.Skills[SkillIndex.Cutting], Is.EqualTo(350 * Point));
            Assert.That(minor.Skills[SkillIndex.Cutting], Is.EqualTo(1_000 * Point));
            Assert.That(major.Skills[SkillIndex.Cutting], Is.EqualTo(1_500 * Point));
        }

        [Test]
        public void StartingPassionsAreRolledFromTheSeedAndNothingElse()
        {
            // Two colonies from one seed agree on every passion; and across a handful of seeds
            // the roll produces something other than "none" for somebody, or the field is dead.
            var first = ColonyWorld.Build(new GridSize(40, 40, 8), 7u, ScenarioDef.Bare());
            var second = ColonyWorld.Build(new GridSize(40, 40, 8), 7u, ScenarioDef.Bare());
            for (int i = 0; i < first.Pawns.Pawns.Count; i++)
                Assert.That(second.Pawns.Pawns.All[i].Passions, Is.EqualTo(first.Pawns.Pawns.All[i].Passions));

            int passionate = 0;
            for (uint seed = 1; seed <= 5; seed++)
            {
                var colony = ColonyWorld.Build(new GridSize(40, 40, 8), seed, ScenarioDef.Bare());
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                    for (int s = 0; s < SkillIndex.Count; s++)
                        if (pawn.Passions[s] != (byte)Passion.None) passionate++;
            }
            Assert.That(passionate, Is.GreaterThan(0), "fifty rolls and not one passion");
        }

        // ------------------------------------------------------------------ work grants it

        [Test]
        public void FellingGrantsCuttingExperiencePerWorkTick()
        {
            // The whole of a fell job's work toil is workTicks swings, each worth the job's base
            // experience scaled by the cutter's passion. Nothing else in the scenario trains
            // cutting, so the cutter's experience is exactly that and everybody else's is zero.
            var size = new GridSize(60, 60, 8);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            ColonyWorld colony = ColonyWorld.Build(size, 1u, scenario, barren: true, wooded: true);

            // U37: the first tick of any colony rolls starting skills, so "never cut" no longer
            // means "stayed at zero" — a colonist can start with cutting experience nobody
            // earned. Tick once, alone, before designating anything, and measure every colonist's
            // gain from that baseline rather than from zero.
            colony.World.Tick();
            var startingCutting = new System.Collections.Generic.Dictionary<int, int>();
            foreach (Pawn p in colony.Pawns.Pawns.All) startingCutting[p.Id.Value] = p.Skills[SkillIndex.Cutting];

            int tree = NearestTree(colony, size);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "the wooded board has a tree to fell");
            colony.World.Intents.Submit(new Intent(IntentKind.Designate, size.FromIndex(tree), (int)DesignationKind.Fell));

            Pawn? cutter = null;
            for (int tick = 0; tick < 6_000 && colony.Grid.Edifice[tree] >= 0; tick++)
            {
                colony.World.Tick();
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                    if (pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.Fell) cutter = pawn;
            }
            Assert.That(colony.Grid.Edifice[tree], Is.LessThan(0), "the tree was never felled");
            Assert.That(cutter, Is.Not.Null);

            PawnContent content = colony.Pawns.Content;
            JobDef fell = content.Jobs[JobIndex.Fell];
            Assert.That(fell.experiencePerWorkTick, Is.GreaterThan(0), "felling trains nothing");
            int perMille = content.Skills[SkillIndex.Cutting].gainPerMilleByPassion[cutter!.Passions[SkillIndex.Cutting]];

            // How many swings the tree owes is WS2's question now: a swing banks the cutter's
            // rate in milliwork and the face charges the standard price, and the rate is the
            // curve at the level she has reached — which RISES as she learns, swing by swing.
            // So the expected grant count comes from walking the same loop the driver runs:
            // read the rate at the experience she has, bank it, pay the grant, repeat until the
            // face is paid for. The grant itself is constant (passion, no soft cap in one tree);
            // only its count moves.
            int grant = fell.experiencePerWorkTick * perMille / 1_000;
            int experience = startingCutting[cutter.Id.Value];
            int swings = 0;
            for (int milliwork = 0; milliwork < fell.workTicks * Rates.Scale; swings++)
            {
                milliwork += content.WorkTypes[WorkTypeIndex.Cutting]
                    .WorkRatePerMille(content.Skills[SkillIndex.Cutting].Level(experience));
                experience += grant;
            }
            int expected = startingCutting[cutter.Id.Value] + swings * grant;

            Assert.That(cutter.Skills[SkillIndex.Cutting], Is.EqualTo(expected));
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                if (pawn != cutter)
                    Assert.That(pawn.Skills[SkillIndex.Cutting], Is.EqualTo(startingCutting[pawn.Id.Value]),
                        $"pawn {pawn.Id.Value} gained cutting experience without ever cutting");
        }

        [Test]
        public void HaulingGrantsHaulingExperienceWhileCarrying()
        {
            // A haul has no swing to count, so the work is the carry: every tick spent walking
            // with the thing in hand. The walk to the thing trains nothing — the pawn is not
            // hauling yet — so the experience is bounded by the carry alone.
            var colony = Colony.Build();
            Pawn hauler = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            hauler.Passions[SkillIndex.Hauling] = (byte)Passion.Minor;
            int store = colony.Cell(12, 12, 0);
            colony.Stockpile(1, store);
            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            int carryTicks = 0;
            for (int i = 0; i < 3_000 && colony.Ctx.Items.Get(scrap)!.Cell != store; i++)
            {
                colony.World.Tick();
                if (hauler.CurrentJob != null && hauler.CurrentJob.CarriedItem == scrap.Value) carryTicks++;
            }
            Assume.That(colony.Ctx.Items.Get(scrap)!.Cell, Is.EqualTo(store), "the haul never finished");

            int perTick = colony.Ctx.Content.Jobs[JobIndex.Haul].experiencePerWorkTick;
            Assert.That(perTick, Is.GreaterThan(0), "hauling trains nothing");
            Assert.That(hauler.Skills[SkillIndex.Hauling], Is.GreaterThan(0));
            Assert.That(hauler.Skills[SkillIndex.Hauling], Is.LessThanOrEqualTo(carryTicks * perTick),
                "more experience than there were ticks of carrying");
            Assert.That(hauler.Skills[SkillIndex.Cutting], Is.Zero, "hauling does not train cutting");
        }

        // ------------------------------------------------------------------ the soft cap

        [Test]
        public void GainSlowsToAFifthAfterFourThousandPointsInOneDay()
        {
            // a-01: after 4,000 points in one skill in one day, further gains are ×0.2 until the
            // midnight reset. Minor passion so the multiplier is 1.0 and the sums stay whole.
            var colony = Colony.Build();
            Pawn pawn = Lone(colony);
            pawn.Passions[SkillIndex.Hauling] = (byte)Passion.Minor;
            int day = colony.Ctx.Content.DayTicks;

            for (int i = 0; i < 4; i++) pawn.GainExperience(SkillIndex.Hauling, 1_000 * Point, currentTick: 100);
            Assert.That(pawn.Skills[SkillIndex.Hauling], Is.EqualTo(4_000 * Point), "under the cap, gains are whole");

            pawn.GainExperience(SkillIndex.Hauling, 1_000 * Point, currentTick: 200);
            Assert.That(pawn.Skills[SkillIndex.Hauling], Is.EqualTo(4_200 * Point), "over the cap, a fifth");

            pawn.GainExperience(SkillIndex.Hauling, 1_000 * Point, currentTick: day + 1);
            Assert.That(pawn.Skills[SkillIndex.Hauling], Is.EqualTo(5_200 * Point), "a new day, the full rate again");
        }

        [Test]
        public void ExperienceStopsAtLevelTwenty()
        {
            var colony = Colony.Build();
            Pawn pawn = Lone(colony);
            pawn.Passions[SkillIndex.Hauling] = (byte)Passion.Major;
            pawn.Skills[SkillIndex.Hauling] = 265_000 * Point - 1;

            pawn.GainExperience(SkillIndex.Hauling, 1_000 * Point, currentTick: 0);

            Assert.That(pawn.Skills[SkillIndex.Hauling], Is.EqualTo(265_000 * Point));
            Assert.That(pawn.SkillLevel(SkillIndex.Hauling), Is.EqualTo(20));
        }

        // ------------------------------------------------------------------ decay

        [Test]
        public void SkillsFromLevelTenDecayOnTheLongCadence()
        {
            // a-01: 30 points a day at level 10, 3,600 a day at 20. The Long cadence is 2,000
            // ticks, thirty to a day, so one Long tick at level 10 costs one point and at 20 costs
            // 120. Below ten nothing decays. The pawn is idle in an empty world, so nothing it
            // does earns anything back.
            var colony = Colony.Build();
            Pawn ten = Lone(colony), twenty = Lone(colony), nine = Lone(colony);
            SkillDef def = colony.Ctx.Content.Skills[SkillIndex.Cutting];
            Assume.That(colony.Ctx.Content.DayTicks / (int)Odyssey.Sim.TickGroup.Long, Is.EqualTo(30));

            ten.Skills[SkillIndex.Cutting] = def.ExperienceForLevel(10) + 500 * Point;
            twenty.Skills[SkillIndex.Cutting] = def.ExperienceForLevel(20);
            nine.Skills[SkillIndex.Cutting] = def.ExperienceForLevel(9) + 500 * Point;

            colony.World.Tick((int)Odyssey.Sim.TickGroup.Long);

            Assert.That(ten.Skills[SkillIndex.Cutting], Is.EqualTo(def.ExperienceForLevel(10) + 500 * Point - 1 * Point));
            Assert.That(twenty.Skills[SkillIndex.Cutting], Is.EqualTo(def.ExperienceForLevel(20) - 120 * Point));
            Assert.That(nine.Skills[SkillIndex.Cutting], Is.EqualTo(def.ExperienceForLevel(9) + 500 * Point), "below ten, no decay");
        }

        [Test]
        public void DecayRunsInTheColonyTheGamePlays()
        {
            // The tests above run on the pawn fixture, which registers the decay system itself,
            // so they would stay green with the system missing from ColonyComposition — and did,
            // when that line was removed on purpose. This one is on the real composition. Bare
            // has no felling orders, so nothing earns cutting back while the pawn idles.
            var colony = ColonyWorld.Build(new GridSize(40, 40, 8), 1u, ScenarioDef.Bare());
            Pawn pawn = colony.Pawns.Pawns.All[0];
            SkillDef def = colony.Pawns.Content.Skills[SkillIndex.Cutting];
            pawn.Skills[SkillIndex.Cutting] = def.ExperienceForLevel(20);

            colony.World.Tick((int)Odyssey.Sim.TickGroup.Long);

            Assert.That(pawn.Skills[SkillIndex.Cutting], Is.EqualTo(def.ExperienceForLevel(20) - 120 * Point),
                "the colony world does not decay skills");
        }

        [Test]
        public void DecayNeverTakesASkillBelowTheFloorOfLevelTen()
        {
            // Decay is a property of levels ten and up, so it cannot be what drops a pawn out of
            // them: a pawn exactly at the floor of ten stays there however long it idles.
            var colony = Colony.Build();
            Pawn pawn = Lone(colony);
            SkillDef def = colony.Ctx.Content.Skills[SkillIndex.Cutting];
            pawn.Skills[SkillIndex.Cutting] = def.ExperienceForLevel(10);

            colony.World.Tick((int)Odyssey.Sim.TickGroup.Long * 5);

            Assert.That(pawn.Skills[SkillIndex.Cutting], Is.EqualTo(def.ExperienceForLevel(10)));
            Assert.That(pawn.SkillLevel(SkillIndex.Cutting), Is.EqualTo(10));
        }

        // ------------------------------------------------------------------ state

        [Test]
        public void ExperiencePassionAndTheDayCounterAreAllInTheHash()
        {
            var colony = Colony.Build();
            Pawn pawn = Lone(colony);
            ulong before = colony.World.ComputeStateHash().Value;

            pawn.Skills[SkillIndex.Hauling] += Point;
            ulong experience = colony.World.ComputeStateHash().Value;
            Assert.That(experience, Is.Not.EqualTo(before), "experience is not hashed");

            pawn.Passions[SkillIndex.Hauling] = (byte)Passion.Major;
            ulong passion = colony.World.ComputeStateHash().Value;
            Assert.That(passion, Is.Not.EqualTo(experience), "passion is not hashed");

            pawn.SkillGainedToday[SkillIndex.Hauling] += Point;
            ulong today = colony.World.ComputeStateHash().Value;
            Assert.That(today, Is.Not.EqualTo(passion), "the day counter is not hashed");
        }

        [Test]
        public void SkillsSurviveASaveAndLoad()
        {
            // Values nothing in the world would produce by itself, so a load that re-rolled or
            // recomputed instead of reading would be caught: a passion the seed did not roll,
            // experience no job granted, and a day counter with the cap already reached.
            var original = Colony.Build();
            Pawn pawn = Lone(original);
            pawn.Passions[SkillIndex.Cutting] = (byte)Passion.Major;
            pawn.Skills[SkillIndex.Cutting] = 12_345 * Point;
            pawn.GainExperience(SkillIndex.Cutting, 4_000 * Point, currentTick: 0);
            original.World.Tick(10);

            using var stream = new MemoryStream();
            WorldSave.Save(original.World, stream, original.SaveComponents);

            var restored = Colony.Build();
            using var input = new MemoryStream(stream.ToArray());
            WorldSave.Load(restored.World, input, restored.SaveComponents);
            Pawn back = restored.Ctx.Pawns.Get(pawn.Id)!;

            Assert.That(back.Passions, Is.EqualTo(pawn.Passions));
            Assert.That(back.Skills, Is.EqualTo(pawn.Skills));
            Assert.That(back.SkillGainedToday, Is.EqualTo(pawn.SkillGainedToday));
            Assert.That(back.SkillDay, Is.EqualTo(pawn.SkillDay));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(original.World.ComputeStateHash().Value));
        }

        // ------------------------------------------------------------------ progress to next level

        /// <summary>
        /// SK2: how far a colonist stands towards her next level, per mille, derived where the
        /// ladder lives so the interface never needs a copy of the table.
        ///
        /// <para>The numbers come from a-01's ladder, not from the method: level 0 costs 1,000
        /// points and level 1 costs 2,000, so 500 points into a fresh skill is half way out of
        /// level 0, and 500 points past the 1,000 floor is a quarter of the way out of level 1.
        /// </para>
        /// </summary>
        [Test]
        public void ProgressIsTheFractionOfTheWayToTheNextLevel()
        {
            SkillDef def = Content.Skills[SkillIndex.Cutting];

            Assert.That(def.ProgressPerMille(0, out int level), Is.Zero, "a fresh skill has earned nothing");
            Assert.That(level, Is.Zero);

            // Level 0 spans 1,000 points, so half of it is 500.
            Assert.That(def.ProgressPerMille(500 * Point, out level), Is.EqualTo(500));
            Assert.That(level, Is.Zero);

            // The instant level 1 is reached the bar starts again at nothing.
            Assert.That(def.ProgressPerMille(1_000 * Point, out level), Is.Zero);
            Assert.That(level, Is.EqualTo(1));

            // Level 1 spans 2,000 points from a floor of 1,000, so 500 into it is a quarter.
            Assert.That(def.ProgressPerMille(1_500 * Point, out level), Is.EqualTo(250));
            Assert.That(level, Is.EqualTo(1));
        }

        /// <summary>
        /// The bar never reads full while there is another level to reach, and never reads
        /// anything but full at the top — the two ends a progress bar is wrong at.
        /// </summary>
        [Test]
        public void ProgressIsFullOnlyAtTheTopLevel()
        {
            SkillDef def = Content.Skills[SkillIndex.Cutting];

            // One thousandth of a point below level 1: as close as the ladder can come without
            // arriving, and still not full.
            Assert.That(def.ProgressPerMille(1_000 * Point - 1, out int level), Is.LessThan(1_000));
            Assert.That(level, Is.Zero);

            Assert.That(def.ProgressPerMille(def.MaxExperience, out level), Is.EqualTo(1_000),
                "at the top there is no next level to be part of the way towards");
            Assert.That(level, Is.EqualTo(def.maxLevel));
        }

        /// <summary>
        /// <b>The overflow this method exists to get right.</b> The top of the ladder is 265,000
        /// points — 265,000,000 in thousandths — and a thousand times that is eight times an int.
        /// Done in int arithmetic the fraction reads correctly at low levels and returns nonsense
        /// at high ones, so the failure would have been invisible in every test above.
        /// </summary>
        [Test]
        public void ProgressStaysInRangeAtEveryLevelOfTheLadder()
        {
            SkillDef def = Content.Skills[SkillIndex.Cutting];

            Assert.That(def.MaxExperience, Is.EqualTo(265_000 * Point),
                "the ladder is not the one whose arithmetic this test is about");

            for (int level = 0; level < def.maxLevel; level++)
            {
                int floor = def.ExperienceForLevel(level);
                int span = def.experienceToAdvance[level];

                Assert.That(def.ProgressPerMille(floor, out int atFloor), Is.Zero,
                    $"level {level} does not begin empty");
                Assert.That(atFloor, Is.EqualTo(level));

                int middle = def.ProgressPerMille(floor + span / 2, out int atMiddle);
                Assert.That(middle, Is.InRange(499, 501), $"level {level} is not half full half way");
                Assert.That(atMiddle, Is.EqualTo(level));

                Assert.That(def.ProgressPerMille(floor + span - 1, out _), Is.InRange(0, 1_000),
                    $"level {level} leaves the range just below its ceiling");
            }
        }

        /// <summary>
        /// The level this yields is the same number <see cref="SkillDef.Level"/> yields, at every
        /// point on the ladder. They are two walks of one table and a disagreement between them
        /// would put the bar on one row and the number on another.
        /// </summary>
        [Test]
        public void ProgressAgreesWithTheLevelLadder()
        {
            SkillDef def = Content.Skills[SkillIndex.Mining];

            for (int points = 0; points <= 265_000; points += 137)
            {
                int experience = points * Point;
                def.ProgressPerMille(experience, out int level);
                Assert.That(level, Is.EqualTo(def.Level(experience)),
                    $"the two ladders disagree at {points} points");
            }
        }

        /// <summary>
        /// SK2: the progress reaches the interface under its own name, for every colonist, beside
        /// the level it belongs to.
        /// </summary>
        [Test]
        public void ThePublishedFrameCarriesProgressTowardsTheNextLevel()
        {
            var size = new GridSize(40, 40, 8);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            ColonyWorld colony = ColonyWorld.Build(size, 1u, scenario, barren: true);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            // Half way out of level 0, which spans 1,000 points.
            pawn.Skills[SkillIndex.Mining] = 500 * Point;

            colony.World.Tick();
            WorldSnapshot frame = colony.World.Views.Current;

            Assert.That(frame.TryGetPawnAspect(
                pawn.Id, AspectKey.Of("odyssey.pawn.skill.mining.progress"), out int progress),
                Is.True, "no skill progress reached the frame");
            Assert.That(progress, Is.EqualTo(500));

            Assert.That(SkillAspects.Name("mining", "progress"),
                Is.EqualTo("odyssey.pawn.skill.mining.progress"));
            Assert.That(SkillAspects.Progress[SkillIndex.Mining],
                Is.EqualTo(AspectKey.Of("odyssey.pawn.skill.mining.progress")));
        }

        // ------------------------------------------------------------------ helpers

        static int NearestTree(ColonyWorld colony, GridSize size)
        {
            CellRef start = colony.Start;
            int best = -1, bestDistance = int.MaxValue;
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                int index = size.Index(x, z, start.Y);
                if (!colony.Designations.IsTree(index)) continue;
                int distance = System.Math.Abs(x - start.X) + System.Math.Abs(z - start.Z);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            return best;
        }
    }
}
