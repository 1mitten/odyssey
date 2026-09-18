#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <see cref="JobDriver.ToilProgress"/> counts milliwork in every driver, and this fails the
    /// build if one starts counting ticks again.
    ///
    /// <para><b>Why one field cannot carry two units.</b> WS1 made the four work drivers bank
    /// thousandths of a tick so a rate could change how fast a pawn pays without changing what
    /// anything costs. The three toils with no rate to scale — eating, sleeping, standing down —
    /// kept a bare <c>++</c>, so the same saved and hashed field held ticks in one driver and
    /// thousandths in the next. Two things broke quietly:</para>
    ///
    /// <list type="bullet">
    /// <item><description><see cref="Rates.FromSave"/> is told a format version and nothing else,
    /// so it scaled every old value by a thousand — right for a work toil, wrong for a meal. A
    /// pre-format-5 file caught mid-meal loaded with a thousand times the progress it had and the
    /// colonist swallowed it whole on the next tick.</description></item>
    /// <item><description>The state hash divided the field back, so an eat, a sleep or a wait
    /// read zero for the whole of its length and contributed nothing. The hash is now taken
    /// whole, which is the other half of the same fix.</description></item>
    /// </list>
    ///
    /// <para>The rule that settles it, and the one this test enforces: <b>a toil that nothing can
    /// speed up pays at exactly <see cref="Rates.Scale"/> a tick.</b> Not one — the standard rate,
    /// said in the unit everybody else is speaking.</para>
    /// </summary>
    public class ToilProgressHasOneUnitTests
    {
        [Test]
        public void NoDriverAdvancesToilProgressInPlainTicks()
        {
            string simRoot = SimSourceRoot();
            var offenders = new List<string>();

            // ++, --, += 1 and -= 1 on the counter: every shape of "one tick" there is. A driver
            // that means one tick of work means Rates.Scale, and writes it.
            var plainTick = new Regex(
                @"\bToilProgress\s*(\+\+|--)|(\+\+|--)\s*ToilProgress\b|\bToilProgress\s*[-+]=\s*1\s*[;)]");

            foreach (string file in Directory.EnumerateFiles(simRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    if (plainTick.IsMatch(code))
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "ToilProgress counts milliwork in every driver, including the toils no rate can " +
                "speed up: advance it by Rates.Scale, not by 1. One field with two units is what " +
                "made Rates.FromSave wrong for an older save caught mid-meal and left eating, " +
                "sleeping and waiting invisible to the state hash. Offenders:\n  " +
                string.Join("\n  ", offenders));
        }

        /// <summary>
        /// And the rule measured rather than read off the source: a colonist eating a meal
        /// advances the counter by the scale a tick, so a round trip through the save reads back
        /// the meal she was actually in the middle of.
        /// </summary>
        [Test]
        public void AMealInProgressCountsInMilliworkAndSurvivesARoundTrip()
        {
            ColonyWorld colony = Board();
            var pawn = colony.Pawns.Pawns.Spawn(Size.Index(colony.Start));
            pawn.Needs[NeedIndex.Food] = 0;

            // Tick until she is actually chewing: the driver past its walk, with progress banked.
            int guard = 0;
            while (guard++ < 20_000 &&
                   !(pawn.Driver != null && pawn.Driver.ToilIndex == 1 && pawn.Driver.ToilProgress > 0 &&
                     colony.Pawns.Content.Jobs[pawn.CurrentJob!.DefIndex].driver == JobIndex.Eat))
                colony.World.Tick();

            Assume.That(pawn.Driver, Is.Not.Null, "the colonist never started a meal");
            Assume.That(pawn.Driver!.ToilProgress, Is.GreaterThan(0), "the meal banked nothing");

            int progress = pawn.Driver.ToilProgress;
            Assert.That(progress % Rates.Scale, Is.Zero,
                "a rate-free toil advances by whole scales, not by ticks");
            Assert.That(progress / Rates.Scale, Is.LessThanOrEqualTo(
                    colony.Pawns.Content.Jobs[pawn.CurrentJob!.DefIndex].workTicks),
                "the meal ran past its own length, which is what a unit mismatch looks like");

            // And the hash can see it: a field divided back to ticks read zero for the whole of a
            // meal, so two runs could disagree about one and agree about everything.
            ulong before = colony.World.ComputeStateHash().Value;
            pawn.Driver.ToilProgress += Rates.Scale;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(before),
                "one tick of a meal moved no hash — the eat toil is invisible again");
        }

        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 0;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: true);
        }

        /// <summary>Everything from the first // outside a string literal. Good enough for a
        /// source scan, and the same shape <c>HopPriceHasOneOwnerTests</c> uses.</summary>
        static string StripComment(string line)
        {
            int at = line.IndexOf("//", System.StringComparison.Ordinal);
            return at < 0 ? line : line.Substring(0, at);
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

            throw new DirectoryNotFoundException(
                "could not find Assets/Odyssey/Sim by walking up from " +
                TestContext.CurrentContext.TestDirectory);
        }
    }
}
