#nullable enable
using System.Text;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What each golden colony actually <i>does</i>, printed rather than hashed.
    ///
    /// <para><b>Why this exists.</b> A golden hash moving says "something moved" and nothing else,
    /// and a hash moves for two completely different reasons: because the colony changed, or
    /// because the hash can see more of what was always there. Adding a hashed component does the
    /// second to every board in the game, including ones with none of the thing in them. The only
    /// honest way to tell them apart is to print what a colony is made of, on both branches, and
    /// compare — which is what <c>Golden.cs</c>'s own re-bake note means by "measured, not
    /// assumed", and what the WS2 and S1 re-bakes both did.</para>
    ///
    /// <para><b>It is deliberately written against nothing new.</b> Every number below is
    /// available on a build that has never heard of shelves, so the same file can be dropped on
    /// <c>main</c> and run there. A probe that only compiles on one side compares nothing.</para>
    ///
    /// <para><c>[Explicit]</c>, so it is never part of a tier: it asserts nothing and exists to be
    /// read. Run it with <c>--filter GoldenColonyProbe</c> on both branches.</para>
    /// </summary>
    [Explicit("A measuring instrument, not a test. Run it by name on both branches and diff.")]
    public class GoldenColonyProbe
    {
        [Test] public void Meadow() => Report(Golden.Meadow);
        [Test] public void RuinedCity() => Report(Golden.City);
        [Test] public void PlayedBoard() => Report(Golden.PlayedBoard);

        static readonly object Lock = new object();

        static void Report(Golden.Case bench)
        {
            ColonyWorld colony = bench.Build();
            var report = new StringBuilder();
            report.AppendLine(bench.Name);
            report.AppendLine("  generated: " + Census(colony));

            for (int tick = 0; tick < bench.Ticks; tick++) colony.World.Tick();
            report.AppendLine("  simulated: " + Census(colony));

            // To a file named by the environment, because a test runner's captured output is not
            // reliably printed and this exists to be diffed between two branches.
            string? into = System.Environment.GetEnvironmentVariable("ODYSSEY_PROBE");
            if (string.IsNullOrEmpty(into)) into = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "odyssey-probe.txt");

            lock (Lock) System.IO.File.AppendAllText(into, report.ToString());
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// One line of everything a change to storage could plausibly disturb: what exists, where
        /// it is, who is alive, how they are, and what the colony has been told to do.
        /// </summary>
        static string Census(ColonyWorld colony)
        {
            PawnContext ctx = colony.Pawns;

            int live = 0, stacks = 0;
            long itemCells = 0;
            var perDef = new int[ctx.Content.Items.Length];
            for (int i = 0; i < ctx.Items.Items.Count; i++)
            {
                ColonyItem thing = ctx.Items.Items[i];
                if (thing.Despawned) continue;
                live++;
                stacks += thing.Stack;
                if (thing.Cell >= 0) itemCells += thing.Cell;
                if (thing.DefIndex < perDef.Length) perDef[thing.DefIndex] += thing.Stack;
            }

            long pawnCells = 0;
            long food = 0, rest = 0, mood = 0, progress = 0;

            // The first five skills and their passions, which every build since growing has. The
            // bound is written as a number rather than read off the build on purpose: a build that
            // has appended a sixth skill rolls it too, and a census that summed it would call the
            // new roll a colony that behaved differently (added for the combat contracts step,
            // 2026-09-23, whose Skill_Melee is exactly that).
            long experience = 0, passions = 0;
            const int SharedSkills = 5;
            for (int i = 0; i < ctx.Pawns.All.Count; i++)
            {
                Pawn pawn = ctx.Pawns.All[i];
                pawnCells += pawn.Cell;
                food += pawn.Needs[NeedIndex.Food];
                rest += pawn.Needs[NeedIndex.Rest];
                mood += pawn.Mood;
                progress += pawn.MoveProgress;
                for (int s = 0; s < SharedSkills && s < pawn.Skills.Length; s++)
                {
                    experience += pawn.Skills[s];
                    passions += pawn.Passions[s];
                }
            }

            // What the colony did, job by job, over the fourteen job defs every build since the
            // draft has — the same reasoning as the skills above.
            var jobs = new StringBuilder();
            const int SharedJobs = 14;
            for (int j = 0; j < SharedJobs && j < ctx.Content.Jobs.Length; j++)
            {
                int done = colony.Jobs.CompletedOf(j), failed = colony.Jobs.FailedOf(j);
                if (done == 0 && failed == 0) continue;
                if (jobs.Length > 0) jobs.Append(',');
                jobs.Append(j).Append(':').Append(done).Append('/').Append(failed);
            }

            var defs = new StringBuilder();
            for (int i = 0; i < perDef.Length; i++)
            {
                if (perDef[i] == 0) continue;
                if (defs.Length > 0) defs.Append(',');
                defs.Append(i).Append(':').Append(perDef[i]);
            }

            return $"live={live} stacks={stacks} itemCells={itemCells} [{defs}] "
                 + $"loose={ctx.Items.LooseItems.Count} stored={ctx.Items.StoredItems.Count} "
                 + $"pawns={ctx.Pawns.Count} pawnCells={pawnCells} food={food} rest={rest} "
                 + $"mood={mood} progress={progress} experience={experience} passions={passions} "
                 + $"started={colony.Jobs.JobsStarted} failed={colony.Jobs.JobsFailed} jobs=[{jobs}] "
                 + $"orders={ctx.Designations?.Cells.Count ?? 0} zones={ctx.Storage?.ZoneCount ?? 0}";
        }
    }
}
