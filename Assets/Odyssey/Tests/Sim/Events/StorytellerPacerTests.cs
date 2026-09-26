#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// The tuning harness (design 68 §9): the pure pacer run for a year on many seeds per
    /// storyteller, against an oracle that answers the way today's content does — a big threat and
    /// a good event can always fire, a small threat and an arrival never can. It asserts the owner's
    /// targets (design 68 §2 ruling 13) and prints the table the numbers are tuned from.
    /// </summary>
    public class StorytellerPacerTests
    {
        const int Seeds = 200;
        const int Days = 72;
        const int SeasonDays = 24;

        sealed class Oracle : IStoryOracle
        {
            public bool SmallCanFire;
            public bool BigRefused;
            public int Big, Good, Small;
            public int Tick;
            public readonly List<int> BigTicks = new List<int>();
            public readonly List<int> GoodTicks = new List<int>();

            public bool TryFire(IncidentCategory category, bool excludeBad, int budgetPerMille)
            {
                switch (category)
                {
                    case IncidentCategory.ThreatBig:
                        if (BigRefused) return false;
                        Big++;
                        BigTicks.Add(Tick);
                        return true;
                    case IncidentCategory.Misc:
                        Good++;
                        GoodTicks.Add(Tick);
                        return true;
                    case IncidentCategory.ThreatSmall:
                        if (!SmallCanFire) return false;
                        Small++;
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>A year from the end of grace, one check an hour: what the live system does.</summary>
        static Oracle Run(StorytellerDef def, uint seed, bool bigAllowed = true, bool smallCanFire = false, int stretch = 100,
            bool bigRefused = false)
        {
            var pacer = new StorytellerPacer();
            int graceEnd = (int)((long)def.graceDays * Calendar.TicksPerDay * stretch / 100);
            var arm = DeterministicRandom.ForTick(seed, 0, StorytellerPurpose.Arm);
            pacer.Arm(def, 0, graceEnd, ref arm);
            var oracle = new Oracle { SmallCanFire = smallCanFire, BigRefused = bigRefused };
            int end = graceEnd + Days * Calendar.TicksPerDay;
            for (int tick = Calendar.TicksPerHour; tick < end; tick += Calendar.TicksPerHour)
            {
                oracle.Tick = tick;
                var rng = DeterministicRandom.ForTick(seed, tick, StorytellerPurpose.Pace);
                pacer.Step(def, tick, graceEnd, bigAllowed, ref rng, oracle);
            }
            return oracle;
        }

        static (double mean, double variance, double good) Season(StorytellerDef def)
        {
            var perSeason = new List<double>();
            double good = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Oracle o = Run(def, seed);
                int graceEnd = def.graceDays * Calendar.TicksPerDay;
                int[] counts = new int[Days / SeasonDays];
                foreach (int t in o.BigTicks)
                {
                    int season = (t - graceEnd) / (SeasonDays * Calendar.TicksPerDay);
                    if (season >= 0 && season < counts.Length) counts[season]++;
                }
                foreach (int c in counts) perSeason.Add(c);
                good += o.Good;
            }
            double mean = 0;
            foreach (double v in perSeason) mean += v;
            mean /= perSeason.Count;
            double variance = 0;
            foreach (double v in perSeason) variance += (v - mean) * (v - mean);
            variance /= perSeason.Count;
            double daysPerGood = (double)Seeds * (Days + def.graceDays) / Math.Max(1, good);
            return (mean, variance, daysPerGood);
        }

        [Test]
        public void EachStorytellerMeetsItsSeasonTarget()
        {
            StorytellerContent content = ContentPack.Storytellers();
            var jacob = Season(content.Defs[StorytellerHandle.Jacob]);
            var trent = Season(content.Defs[StorytellerHandle.Trent]);
            var kano = Season(content.Defs[StorytellerHandle.Kano]);

            TestContext.Out.WriteLine("storyteller  big/season  variance  days per good event");
            TestContext.Out.WriteLine($"Jacob        {jacob.mean,10:0.00}  {jacob.variance,8:0.00}  {jacob.good,8:0.0}");
            TestContext.Out.WriteLine($"Trent        {trent.mean,10:0.00}  {trent.variance,8:0.00}  {trent.good,8:0.0}");
            TestContext.Out.WriteLine($"Kano         {kano.mean,10:0.00}  {kano.variance,8:0.00}  {kano.good,8:0.0}");

            Assert.That(jacob.mean, Is.InRange(2.5, 3.5), "Jacob: about three big threats a season");
            Assert.That(kano.mean, Is.InRange(1.0, 2.0), "Kano: about half of Jacob's");
            Assert.That(trent.mean, Is.InRange(2.3, 3.7), "Trent: Jacob's mean");
            Assert.That(trent.variance, Is.GreaterThan(jacob.variance), "Trent: lumpier than Jacob");
            Assert.That(jacob.good, Is.InRange(4.5, 7.0), "Jacob: a good event about every five or six days");
            Assert.That(kano.good, Is.InRange(4.5, 7.0), "Kano: the same good stream");
        }

        [Test]
        public void NoBigThreatComesBeforeGraceOrWithBigThreatsOff()
        {
            StorytellerContent content = ContentPack.Storytellers();
            foreach (StorytellerDef def in content.Defs)
            {
                int graceEnd = def.graceDays * Calendar.TicksPerDay;
                for (uint seed = 1; seed <= 20; seed++)
                {
                    Oracle o = Run(def, seed);
                    foreach (int t in o.BigTicks)
                        Assert.That(t, Is.GreaterThanOrEqualTo(graceEnd), $"{def.defName} sent a big threat inside its grace");
                    Assert.That(o.Big, Is.GreaterThan(0), $"{def.defName} sent nothing in a year (the control)");

                    Oracle off = Run(def, seed, bigAllowed: false);
                    Assert.That(off.Big, Is.Zero, $"{def.defName} sent a big threat with big threats off");
                    Assert.That(off.Good, Is.GreaterThan(0), $"{def.defName} stopped the good events too");
                }
            }
        }

        /// <summary>
        /// The good events keep coming in the last season, however long the colony has gone without a
        /// big threat, both with big threats off and with every big threat refused. Trent's drought
        /// once forced every roll into ThreatBig, which with big threats off was thrown away and with
        /// one refused was spent, so from day 22 of a Peaceful game the bag was silent for good; the
        /// check above passed on the first 22 days (review, 2026-09-26).
        /// </summary>
        [Test]
        public void TheGoodEventsOutliveADrought()
        {
            foreach (StorytellerDef def in ContentPack.Storytellers().Defs)
            {
                int graceEnd = def.graceDays * Calendar.TicksPerDay;
                // The drought (Trent's 14 days) has long since run by the last two seasons. Trent's
                // bag is lumpy by design, so a single season can go without a good event by chance;
                // two seasons per seed, and the last season on average over the seeds, cannot.
                int lastTwo = graceEnd + (Days - 2 * SeasonDays) * Calendar.TicksPerDay;
                int lastSeason = graceEnd + (Days - SeasonDays) * Calendar.TicksPerDay;
                int offLast = 0, refusedLast = 0;
                const int SeedCount = 20;
                for (uint seed = 1; seed <= SeedCount; seed++)
                {
                    Oracle off = Run(def, seed, bigAllowed: false);
                    Oracle refused = Run(def, seed, bigRefused: true);
                    Assert.That(off.GoodTicks.FindAll(t => t >= lastTwo).Count, Is.GreaterThanOrEqualTo(2),
                        $"{def.defName} seed {seed}: the good events stopped with big threats off");
                    Assert.That(refused.GoodTicks.FindAll(t => t >= lastTwo).Count, Is.GreaterThanOrEqualTo(2),
                        $"{def.defName} seed {seed}: the good events stopped while every big threat was refused");
                    offLast += off.GoodTicks.FindAll(t => t >= lastSeason).Count;
                    refusedLast += refused.GoodTicks.FindAll(t => t >= lastSeason).Count;
                }
                Assert.That(offLast, Is.GreaterThanOrEqualTo(2 * SeedCount), $"{def.defName}: the last season thinned with big threats off");
                Assert.That(refusedLast, Is.GreaterThanOrEqualTo(2 * SeedCount), $"{def.defName}: the last season thinned with big threats refused");
            }
        }

        /// <summary>
        /// A pacer saved in the middle of an on-phase, with fires drawn and not yet fallen, comes
        /// back with the same plan and fires on the same ticks. The colony-level save test saves
        /// inside the grace, where every slot is still empty, so it never exercised a drawn plan.
        /// </summary>
        [Test]
        public void APlanSavedMidPhaseFiresTheSame()
        {
            foreach (StorytellerDef def in ContentPack.Storytellers().Defs)
            {
                const uint seed = 9;
                int graceEnd = def.graceDays * Calendar.TicksPerDay;
                int saveAt = graceEnd + Calendar.TicksPerDay + 5 * Calendar.TicksPerHour;
                int end = graceEnd + 30 * Calendar.TicksPerDay;

                var a = new StorytellerPacer();
                var arm = DeterministicRandom.ForTick(seed, 0, StorytellerPurpose.Arm);
                a.Arm(def, 0, graceEnd, ref arm);
                var before = new Oracle();
                for (int tick = Calendar.TicksPerHour; tick <= saveAt; tick += Calendar.TicksPerHour)
                {
                    before.Tick = tick;
                    var rng = DeterministicRandom.ForTick(seed, tick, StorytellerPurpose.Pace);
                    a.Step(def, tick, graceEnd, true, ref rng, before);
                }

                var bytes = new MemoryStream();
                using (var binary = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
                    a.Save(new SaveWriter(binary));
                bytes.Position = 0;
                var b = new StorytellerPacer();
                var armB = DeterministicRandom.ForTick(seed, 0, StorytellerPurpose.Arm);
                b.Arm(def, 0, graceEnd, ref armB);
                b.Load(new SaveReader(new BinaryReader(bytes), WorldSave.CurrentFormatVersion));

                StateHash ha = StateHash.New(), hb = StateHash.New();
                a.ContributeTo(ref ha);
                b.ContributeTo(ref hb);
                Assert.That(hb.Value, Is.EqualTo(ha.Value), $"{def.defName}: the loaded plan is not the saved one");

                var oa = new Oracle();
                var ob = new Oracle();
                for (int tick = saveAt + Calendar.TicksPerHour; tick < end; tick += Calendar.TicksPerHour)
                {
                    oa.Tick = ob.Tick = tick;
                    var ra = DeterministicRandom.ForTick(seed, tick, StorytellerPurpose.Pace);
                    var rb = DeterministicRandom.ForTick(seed, tick, StorytellerPurpose.Pace);
                    a.Step(def, tick, graceEnd, true, ref ra, oa);
                    b.Step(def, tick, graceEnd, true, ref rb, ob);
                }
                Assert.That(oa.BigTicks.Count, Is.GreaterThan(0), $"{def.defName}: nothing fell after the save (the control)");
                Assert.That(ob.BigTicks, Is.EqualTo(oa.BigTicks), $"{def.defName}: the big threats moved across the save");
                Assert.That(ob.GoodTicks, Is.EqualTo(oa.GoodTicks), $"{def.defName}: the good events moved across the save");
            }
        }

        [Test]
        public void TheGraceStretchMovesTheFirstBigThreat()
        {
            StorytellerDef jacob = ContentPack.Storytellers().Defs[StorytellerHandle.Jacob];
            Oracle normal = Run(jacob, 5);
            Oracle stretched = Run(jacob, 5, stretch: 150);
            Assert.That(stretched.BigTicks[0], Is.GreaterThan(normal.BigTicks[0]));
            Assert.That(stretched.BigTicks[0], Is.GreaterThanOrEqualTo(jacob.graceDays * Calendar.TicksPerDay * 3 / 2));
        }

        [Test]
        public void WhatOneCategoryHoldsDoesNotMoveAnother()
        {
            // The rule a category with nothing to fire loses its roll exists for this: whether small
            // threats can fire must not change when big ones come.
            foreach (StorytellerDef def in ContentPack.Storytellers().Defs)
                for (uint seed = 1; seed <= 20; seed++)
                {
                    Oracle without = Run(def, seed, smallCanFire: false);
                    Oracle with = Run(def, seed, smallCanFire: true);
                    Assert.That(with.BigTicks, Is.EqualTo(without.BigTicks), $"{def.defName} seed {seed}");
                }
        }

        [Test]
        public void TheDroughtRuleForcesABigThreat()
        {
            StorytellerDef trent = ContentPack.Storytellers().Defs[StorytellerHandle.Trent];
            GeneratorDef bag = trent.generators[0];
            // The drought, then at most one roll's interval (half again the mean) before it is asked.
            int limit = bag.droughtDays * Calendar.TicksPerDay + (bag.meanHours * 3 / 2 + 1) * Calendar.TicksPerHour;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Oracle o = Run(trent, seed);
                int previous = trent.graceDays * Calendar.TicksPerDay;
                foreach (int t in o.BigTicks)
                {
                    Assert.That(t - previous, Is.LessThanOrEqualTo(limit),
                        $"seed {seed}: {(t - previous) / Calendar.TicksPerDay} days without a big threat");
                    previous = t;
                }
            }
        }

        [Test]
        public void ABadGeneratorIsALoadError()
        {
            var def = new StorytellerDef { defName = "Storyteller_Broken", labelKey = "ui.storyteller.jacob" };
            def.generators.Add(new GeneratorDef { kind = GeneratorKind.OnOffCycle, onDays = 1, firesMin = 3, firesMax = 3, minSpacingHours = 20 });
            Assert.Throws<DefLoadException>(() => StorytellerContent.Validate(def));

            var none = new StorytellerDef { defName = "Storyteller_Empty", labelKey = "ui.storyteller.jacob" };
            Assert.Throws<DefLoadException>(() => StorytellerContent.Validate(none));
        }

        [Test]
        public void TheOrderIsTheHandleTable()
        {
            Assert.That(StorytellerContent.Order.Length, Is.EqualTo(StorytellerHandle.Count));
            StorytellerContent content = ContentPack.Storytellers();
            Assert.That(content.Defs[StorytellerHandle.Jacob].labelKey, Is.EqualTo("ui.storyteller.jacob"));
            Assert.That(content.Defs[StorytellerHandle.Trent].labelKey, Is.EqualTo("ui.storyteller.trent"));
            Assert.That(content.Defs[StorytellerHandle.Kano].labelKey, Is.EqualTo("ui.storyteller.kano"));
        }
    }
}
