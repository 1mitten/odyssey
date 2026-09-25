#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The price of a hop is decided in one place, and this fails the build if a second place
    /// starts deciding it.
    ///
    /// <para><b>Why a hop needs this and a stair does not.</b> A stair is built, so it declares a
    /// connector, and the connector carries its price to everyone who asks. A hop — one block up
    /// or down into the column next door — needs nothing built and declares nothing, so three
    /// separate seams have to arrive at the same number independently: the cell search that plans
    /// the route (<see cref="PathFinder"/>), the region graph that prices the abstract edge
    /// (<c>NavGraph.TryHopEdges</c>), and the mover that charges for the step actually taken
    /// (<c>MovementSystem.StepCost</c>).</para>
    ///
    /// <para><b>They have already disagreed once, and nothing reported it.</b> The mover read a
    /// hop's price off connectors, found none, and fell through to <see cref="MoveCost.Fall"/> —
    /// 100,000, meaning forbidden. The pawn did not throw and did not re-plan. It stood in the
    /// cell before the step holding a legal path, earning about one unit of progress a tick
    /// against a bill of a hundred thousand, and was still standing there after 10,000 ticks.
    /// <c>CLAUDE.md</c> has carried the warning ever since — "a price the planner and the mover
    /// disagree about fails silently" — with nothing enforcing it. This is the enforcement.</para>
    ///
    /// <para><b>What was actually wrong when this was written.</b> Not a disagreement: all three
    /// agreed, because all three named <see cref="MoveCost.JumpUp"/> and <see cref="MoveCost.Drop"/>
    /// for themselves and the constants happened to be the same on every path. That is agreement
    /// by coincidence, and it survives exactly until the price stops being a constant — the day a
    /// hop onto ice, or while carrying, or for a different traverse mode costs something else. Then
    /// two of the three would silently keep the old number. The fix is that
    /// <see cref="NavGraph.HopCost(bool)"/> is the only expression of the rule and the other two
    /// call it.</para>
    /// </summary>
    public class HopPriceHasOneOwnerTests
    {
        /// <summary>
        /// Files permitted to name the hop constants: the file that defines them, and the file
        /// that owns the rule. Anything else naming them is a second opinion about the price.
        /// </summary>
        static readonly string[] Owners = { "NavGrid.cs", "NavGraph.cs" };

        [Test]
        public void OnlyOneFileDecidesWhatAHopCosts()
        {
            string simRoot = SimSourceRoot();
            var offenders = new List<string>();

            foreach (string file in Directory.EnumerateFiles(simRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (Array.IndexOf(Owners, name) >= 0) continue;

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    // A comment may discuss the price; only code may not decide it.
                    string code = StripComment(line);
                    if (Regex.IsMatch(code, @"\bMoveCost\s*\.\s*(JumpUp|Drop|Jump)\b"))
                        offenders.Add($"{name}:{i + 1}: {line.Trim()}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "MoveCost.JumpUp and MoveCost.Drop name the price of a hop, which NavGraph.HopCost " +
                "owns, and MoveCost.Jump the price of a jump over a stream, which NavGraph.JumpCost " +
                "owns (design 43). Call them instead — a second place deciding this is the defect that left a " +
                "colonist standing on a face for 10,000 ticks. Offenders:\n  " +
                string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The owner's two overloads cannot drift from each other either: the one the search uses
        /// (a direction) and the one the mover uses (two cells) must price the same step alike.
        /// </summary>
        [Test]
        public void BothWaysOfAskingTheOwnerAgree()
        {
            var low = new CellRef(4, 4, 1);
            var high = new CellRef(5, 4, 2);

            Assert.That(NavGraph.HopCost(low, high), Is.EqualTo(NavGraph.HopCost(up: true)),
                "the two-cell overload and the direction overload disagree about a hop up");
            Assert.That(NavGraph.HopCost(high, low), Is.EqualTo(NavGraph.HopCost(up: false)),
                "the two-cell overload and the direction overload disagree about a drop");
        }

        /// <summary>
        /// And the region graph's abstract edge carries the same number the owner gives, measured
        /// off a built graph rather than read off the source. This is the assertion that would
        /// still fail if somebody routed <c>TryHopEdges</c> through a different rule.
        /// </summary>
        [Test]
        public void TheRegionGraphPricesAHopAtTheOwnersPrice()
        {
            // A step in the ground: a 1-high shelf with a walkable cell on top of it, so that
            // exactly one hop edge exists between two regions.
            var size = new GridSize(6, 3, 3);
            var grid = new CellGrid(size);
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                grid.Floor[size.Index(x, z, 1)] = 1;
                if (x >= 3)
                {
                    grid.Flags[size.Index(x, z, 1)] |= CellFlags.SolidTerrain;
                    grid.Floor[size.Index(x, z, 2)] = 1;
                }
            }

            var nav = new NavGraph(grid);
            nav.Rebuild();

            int lower = size.Index(2, 1, 1);
            int upper = size.Index(3, 1, 2);
            Assert.That(nav.IsLegalStep(lower, upper, TraverseMode.Colonist), Is.True,
                "the fixture is wrong: that is not a legal hop");

            // Found by the region pair, not by the cell pair: TryHopEdges builds one edge per pair
            // of regions and stores whichever cells it met first, so asking for our own two cells
            // would be asking the wrong question.
            int fromRegion = nav.RegionOfCell(lower);
            int toRegion = nav.RegionOfCell(upper);
            Assert.That(toRegion, Is.Not.EqualTo(fromRegion),
                "the fixture is wrong: a region must never span two layers");

            int found = -1;
            int start = nav.AdjacencyStart(fromRegion);
            for (int i = 0; i < nav.AdjacencyCount(fromRegion); i++)
            {
                int link = nav.AdjacencyLink(start + i);
                if (nav.KindOfLink(link) != LinkKind.Portal) continue;
                if (nav.LinkOther(link, fromRegion) != toRegion) continue;
                found = link;
                break;
            }

            Assert.That(found, Is.Not.EqualTo(-1), "no hop edge was built between the two regions");
            Assert.That(nav.LinkCostFrom(found, fromRegion), Is.EqualTo(NavGraph.HopCost(up: true)),
                "the region graph prices a hop up differently from NavGraph.HopCost");

            Assert.That(nav.LinkCostFrom(found, toRegion), Is.EqualTo(NavGraph.HopCost(up: false)),
                "the region graph prices a drop differently from NavGraph.HopCost");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Everything in the line after a `//`, removed — but not a `//` inside a string, which is
        /// rare here and would only ever cause a false pass, never a false failure.
        /// </summary>
        static string StripComment(string line)
        {
            int at = line.IndexOf("//", StringComparison.Ordinal);
            return at < 0 ? line : line.Substring(0, at);
        }

        /// <summary>
        /// Walk up to the repository root and take the simulation source, the way
        /// <c>ContentPack</c> finds the Def pack. The test has to read source rather than IL
        /// because what it forbids — naming a constant — leaves no trace in the compiled output.
        /// </summary>
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
