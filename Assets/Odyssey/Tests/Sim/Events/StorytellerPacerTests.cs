#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;

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
            public int Big, Good, Small;
            public int Tick;
            public readonly List<int> BigTicks = new List<int>();

            public bool TryFire(IncidentCategory category, bool excludeBad, int budgetPerMille)
            {
                switch (category)
                {
                    case IncidentCategory.ThreatBig:
                        Big++;
                        BigTicks.Add(Tick);
                        return true;
                    case IncidentCategory.Misc:
                        Good++;
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
        static Oracle Run(StorytellerDef def, uint seed, bool bigAllowed = true, bool smallCanFire = false, int stretch = 100)
        {
            var pacer = new StorytellerPacer();
            int graceEnd = (int)((long)def.graceDays * Calendar.TicksPerDay * stretch / 100);
            var arm = DeterministicRandom.ForTick(seed, 0, StorytellerPurpose.Arm);
            pacer.Arm(def, 0, graceEnd, ref arm);
            var oracle = new Oracle { SmallCanFire = smallCanFire };
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
