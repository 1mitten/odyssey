#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The fruit machine's model (design 41 §6): it lands every reel exactly on its value whatever
    /// the frame rate, in reading order, with the hot ones teasing, and it tells the view what to
    /// draw. None of it needs Unity.
    /// </summary>
    public class ReelMachineTests
    {
        static readonly int[] Counts = { 24, 24, 16, 16, 16, 16, 16, 31, 19, 19 };

        static ReelMachine Spun(uint seed = 7u)
        {
            var m = new ReelMachine();
            m.Start(Counts, seed);
            return m;
        }

        static void Run(ReelMachine m, double step, double seconds)
        {
            for (double t = 0; t < seconds; t += step) m.Step(step);
        }

        static int[] Targets(uint seed)
        {
            var rng = new Random((int)seed);
            return Counts.Select(n => rng.Next(n)).ToArray();
        }

        [Test]
        public void EveryReelLandsExactlyOnItsValueAtAnyFrameRate()
        {
            double[] steps = { 1 / 30.0, 1 / 60.0, 1 / 144.0, 0.05, 0.1 };
            for (uint seed = 1; seed <= 60; seed++)
            foreach (double step in steps)
            {
                ReelMachine m = Spun(seed);
                int[] targets = Targets(seed);
                bool[] hot = targets.Select((_, i) => (i + seed) % 4 == 0).ToArray();
                Run(m, step, 0.2 + (seed % 7) * 0.13);
                Assert.That(m.Stop(targets, hot, Verdict.Plain), Is.True);
                Run(m, step, 8);

                Assert.That(m.Phase, Is.EqualTo(MachinePhase.Landed), $"seed {seed} step {step}: still moving");
                for (int i = 0; i < Counts.Length; i++)
                {
                    Assert.That(m.LandedSymbol(i), Is.EqualTo(targets[i]), $"seed {seed} step {step} reel {i}");
                    Assert.That(m.SymbolAt(i, 0), Is.EqualTo(targets[i]), $"seed {seed} step {step} reel {i}: the window shows another");
                    Assert.That(m.FractionOf(i), Is.EqualTo(0).Within(1e-9), "it came to rest between two symbols");
                }
            }
        }

        [Test]
        public void TheReelsLandInReadingOrderAtLeastAStaggerApart()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                ReelMachine m = Spun(seed);
                var landedAt = new double[Counts.Length];
                var order = new List<int>();
                m.ReelLanded += i => { order.Add(i); landedAt[i] = m.Now; };
                Run(m, 1 / 60.0, 1);
                bool[] hot = Counts.Select((_, i) => i == 3 || i == 8).ToArray();
                m.Stop(Targets(seed), hot, Verdict.Plain);
                Run(m, 1 / 60.0, 8);

                Assert.That(order, Is.EqualTo(Enumerable.Range(0, Counts.Length)), $"seed {seed}");
                for (int i = 1; i < Counts.Length; i++)
                    Assert.That(landedAt[i] - landedAt[i - 1], Is.GreaterThanOrEqualTo(ReelMachine.LandStagger - 1 / 60.0),
                        $"seed {seed}: reel {i} landed on top of reel {i - 1}");
            }
        }

        [Test]
        public void AHotReelTeasesLongerThanAColdOne()
        {
            ReelMachine m = Spun();
            Run(m, 1 / 60.0, 1);
            var hot = new bool[Counts.Length];
            hot[5] = true;
            m.Stop(Targets(3), hot, Verdict.Plain);

            double teasing = 0, braking4 = 0;
            for (double t = 0; t < 8; t += 1 / 120.0)
            {
                m.Step(1 / 120.0);
                if (m.ReelPhaseOf(5) == ReelPhase.Teasing) teasing += 1 / 120.0;
                if (m.ReelPhaseOf(4) == ReelPhase.Braking) braking4 += 1 / 120.0;
            }

            Assert.That(teasing, Is.GreaterThanOrEqualTo(ReelMachine.TeaseSeconds - 0.02));
            Assert.That(braking4, Is.LessThan(ReelMachine.TeaseSeconds * 0.6), "the control: a cold reel does not tease");
        }

        [Test]
        public void ABrakingReelNeverSpeedsUpOrRunsBackwards()
        {
            ReelMachine m = Spun(11u);
            Run(m, 1 / 60.0, 0.8);
            m.Stop(Targets(11), Counts.Select((_, i) => i % 3 == 0).ToArray(), Verdict.Plain);
            var last = Enumerable.Range(0, Counts.Length).Select(i => m.PositionOf(i)).ToArray();
            var lastSpeed = Enumerable.Repeat(double.MaxValue, Counts.Length).ToArray();
            for (int f = 0; f < 600; f++)
            {
                m.Step(1 / 120.0);
                for (int i = 0; i < Counts.Length; i++)
                {
                    ReelPhase phase = m.ReelPhaseOf(i);
                    if (phase == ReelPhase.Landed) continue;
                    double speed = (m.PositionOf(i) - last[i]) * 120;
                    Assert.That(speed, Is.GreaterThanOrEqualTo(0), $"reel {i} ran backwards");
                    if (phase == ReelPhase.Braking || phase == ReelPhase.Teasing)
                        Assert.That(speed, Is.LessThanOrEqualTo(lastSpeed[i] + 1e-6), $"reel {i} sped up while braking");
                    lastSpeed[i] = phase == ReelPhase.Spinning ? double.MaxValue : speed;
                    last[i] = m.PositionOf(i);
                }
            }
        }

        [Test]
        public void TheTargetIsNeverSeenBeforeItsReelBrakes()
        {
            // The value is written into the slot where the reel will rest. The window shows the
            // symbol nearest the middle and one either side, so that slot must be at least two past
            // the middle when braking begins — or a player would see the answer arrive.
            for (uint seed = 1; seed <= 30; seed++)
            {
                ReelMachine m = Spun(seed);
                Run(m, 1 / 60.0, 1);
                m.Stop(Targets(seed), Counts.Select((_, i) => i % 4 == 0).ToArray(), Verdict.Plain);
                var middleAtBrake = new double[Counts.Length];
                var seen = new bool[Counts.Length];
                while (m.Phase != MachinePhase.Landed)
                {
                    m.Step(1 / 60.0);
                    for (int i = 0; i < Counts.Length; i++)
                    {
                        ReelPhase phase = m.ReelPhaseOf(i);
                        if (seen[i] || (phase != ReelPhase.Braking && phase != ReelPhase.Teasing)) continue;
                        seen[i] = true;
                        middleAtBrake[i] = Math.Round(m.PositionOf(i));
                    }
                }

                for (int i = 0; i < Counts.Length; i++)
                {
                    Assert.That(seen[i], Is.True, $"reel {i} never braked");
                    Assert.That(m.PositionOf(i) - middleAtBrake[i], Is.GreaterThanOrEqualTo(2),
                        $"seed {seed} reel {i}: it landed inside the window it was braking in");
                }
            }
        }

        [Test]
        public void NoTwoSpeedsAreNearASmallRatio()
        {
            double[] s = ReelMachine.Speeds;
            for (int a = 0; a < s.Length; a++)
            for (int b = a + 1; b < s.Length; b++)
            {
                double ratio = s[a] > s[b] ? s[a] / s[b] : s[b] / s[a];
                foreach (double small in new[] { 1.0, 1.5, 2.0, 4.0 / 3.0, 5.0 / 4.0, 5.0 / 3.0 })
                    Assert.That(Math.Abs(ratio - small), Is.GreaterThan(0.02),
                        $"{s[a]} and {s[b]} are {ratio:F3}, near {small:F3}: they will be seen to lock step");
            }
        }

        [Test]
        public void AStopPressedTooSoonWaitsForTheReelsToComeUpToSpeed()
        {
            ReelMachine m = Spun();
            m.Step(0.05);
            Assert.That(m.Stop(Targets(1), new bool[Counts.Length], Verdict.Plain), Is.True);
            Assert.That(m.Phase, Is.EqualTo(MachinePhase.Spinning), "it began braking before it had spun");
            Run(m, 1 / 60.0, ReelMachine.MinSpinSeconds);
            Assert.That(m.Phase, Is.EqualTo(MachinePhase.Stopping));
            Assert.That(m.Stop(Targets(2), new bool[Counts.Length], Verdict.Plain), Is.False, "a second STOP moved the outcome");
        }

        [Test]
        public void TheBulbsChaseWhileSpinningAndSettleByVerdict()
        {
            ReelMachine idle = new ReelMachine();
            Assert.That(Enumerable.Range(0, 52).All(b => idle.Bulb(b, 52) == BulbLight.Dim), Is.True, "idle is dim");

            ReelMachine m = Spun();
            m.Step(0.01);
            int lit = Enumerable.Range(0, 52).Count(b => m.Bulb(b, 52) == BulbLight.Lit);
            Assert.That(lit, Is.InRange(17, 18), "every third bulb");
            int first = Enumerable.Range(0, 52).First(b => m.Bulb(b, 52) == BulbLight.Lit);
            m.Step(ReelMachine.ChaseStep);
            Assert.That(m.Bulb(first, 52), Is.Not.EqualTo(BulbLight.Lit), "the chase did not advance");

            ReelMachine jackpot = Finish(Verdict.Jackpot);
            Run(jackpot, 1 / 60.0, 1.5);
            Assert.That(Enumerable.Range(0, 52).All(b => jackpot.Bulb(b, 52) == BulbLight.Gold), Is.True, "a jackpot holds gold");

            ReelMachine dud = Finish(Verdict.Dud);
            Assert.That(dud.Bulb(51, 52), Is.EqualTo(BulbLight.Off), "the rightmost goes out first");
            Assert.That(dud.Bulb(10, 52), Is.EqualTo(BulbLight.Dim), "and the left ones after it");
            Run(dud, 1 / 60.0, 1);
            Assert.That(Enumerable.Range(0, 52).Count(b => dud.Bulb(b, 52) == BulbLight.Dim), Is.EqualTo(ReelMachine.DudKeptDim));
        }

        [Test]
        public void AHotWindowWarmsOverAFifthOfASecondAndOnlyOnceLanded()
        {
            ReelMachine m = Spun();
            Run(m, 1 / 60.0, 1);
            var hot = new bool[Counts.Length];
            hot[0] = true;
            m.Stop(Targets(4), hot, Verdict.Plain);
            Assert.That(m.HotAmount(0), Is.Zero);
            while (m.ReelPhaseOf(0) != ReelPhase.Landed) m.Step(1 / 240.0);
            Assert.That(m.HotAmount(0), Is.LessThan(0.1));
            Run(m, 1 / 60.0, ReelMachine.HotFade + 0.02);
            Assert.That(m.HotAmount(0), Is.EqualTo(1));
            Assert.That(m.HotAmount(1), Is.Zero, "the control: a cold reel never warms");
        }

        static ReelMachine Finish(Verdict verdict)
        {
            ReelMachine m = Spun();
            Run(m, 1 / 60.0, 1);
            m.Stop(Targets(9), new bool[Counts.Length], verdict);
            while (m.Phase != MachinePhase.Landed) m.Step(1 / 60.0);
            return m;
        }

        // ---- the verdict -------------------------------------------------------------------------

        [Test]
        public void TheVerdictFollowsItsRules()
        {
            int[] none = { 0, 0, 0, 0, 0 };
            Assert.That(DrawVerdict.Of(new[] { 13, 0, 0, 0, 0 }, new[] { 2, 0, 0, 0, 0 }, Array.Empty<int>()), Is.EqualTo(Verdict.Jackpot));
            Assert.That(DrawVerdict.Of(new[] { 13, 0, 0, 0, 0 }, new[] { 1, 0, 0, 0, 0 }, Array.Empty<int>()), Is.EqualTo(Verdict.Plain),
                "a thirteen needs a major passion to be a jackpot");
            Assert.That(DrawVerdict.Of(new[] { 3, 3, 3, 3, 3 }, none, new[] { 4 }), Is.EqualTo(Verdict.Jackpot), "a star trait");
            Assert.That(DrawVerdict.Of(new[] { 3, 3, 3, 3, 3 }, none, new[] { -4 }), Is.EqualTo(Verdict.Dud), "a flaw trait");
            Assert.That(DrawVerdict.Of(new[] { 2, 1, 1, 1, 1 }, none, new[] { 2 }), Is.EqualTo(Verdict.Dud), "six is a dud");
            Assert.That(DrawVerdict.Of(new[] { 2, 2, 1, 1, 1 }, none, Array.Empty<int>()), Is.EqualTo(Verdict.Plain), "seven is not");
            Assert.That(DrawVerdict.Of(new[] { 0, 0, 0, 0, 1 }, none, new[] { 4, -4 }), Is.EqualTo(Verdict.Jackpot), "a jackpot beats a dud");
        }
    }
}
