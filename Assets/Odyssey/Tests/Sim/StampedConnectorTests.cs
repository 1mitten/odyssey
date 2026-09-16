#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Stamped stairs as navigation connectors (OQ-10).
    ///
    /// <para>The whole point of declaring both ends at stamp time is that nothing ever searches
    /// for the far one. Cataclysm resolves a staircase's landing by scanning a twelve-tile radius
    /// at run time and has had broken NPC vertical navigation for a decade as a result
    /// (`c-cataclysm-dda.md`); here the template knows where its stairwell runs and says so.</para>
    ///
    /// <para>The tests below are therefore about two things: that every stair a template lays down
    /// becomes a portal edge, and that a connector whose ends damage destroyed does <b>not</b> —
    /// because a portal into rubble is worse than a missing one. It is a lie no path validator can
    /// catch.</para>
    /// </summary>
    public class StampedConnectorTests
    {
        static readonly GridSize SliceSize = new GridSize(60, 60, 5);

        sealed class Map
        {
            public CellGrid Grid = null!;
            public WorldGenResult Result = null!;
            public NavGraph Nav = null!;
            public ConnectorRegistrar.Result Registration;
        }

        static Map Generate(uint seed, bool damage = true)
        {
            var grid = new CellGrid(SliceSize);
            var gen = MapGenDef.For(SliceSize);
            if (!damage)
            {
                gen.minDamageIntensity = 0;
                gen.maxDamageIntensity = 0;
                gen.toppleChance = 0;
            }

            var result = WorldGenerator.Generate(grid, seed, gen, TemplateLibrary.Slice(),
                WorldGenerator.PassCount, null);

            var nav = new NavGraph(grid);
            var registration = ConnectorRegistrar.Register(nav, grid, result.Context.Connectors);
            nav.Rebuild();

            return new Map { Grid = grid, Result = result, Nav = nav, Registration = registration };
        }

        // ---------------------------------------------------------------- what gets declared

        [Test]
        public void EveryStampedShellDeclaresTheStairsItsTemplateAuthored()
        {
            var map = Generate(1, damage: false);
            var context = map.Result.Context;

            Assert.That(context.Connectors, Is.Not.Empty, "the slice map stamped no connectors at all");
            Assert.That(context.Report.ConnectorsStamped, Is.EqualTo(context.Connectors.Count));

            // Counted straight off the templates: one connector per storey that carries a stair
            // glyph and has a layer above it to reach, plus one per ladder cell.
            int expected = 0;
            for (int s = 0; s < context.Shells.Count; s++)
                expected += ConnectorsIn(context.Templates[context.Shells[s].TemplateIndex], context);

            Assert.That(context.Connectors.Count, Is.EqualTo(expected),
                "the stamper declared a different number of connectors than the templates author");
        }

        static int ConnectorsIn(ShellTemplate template, WorldGenContext context)
        {
            int count = 0;
            for (int layer = template.BottomLayer; layer <= template.TopLayer; layer++)
            {
                if (context.GroundLayer + layer + 1 >= context.Size.SizeY) continue;

                bool anyStair = false;
                for (int tz = 0; tz < template.SizeZ; tz++)
                for (int tx = 0; tx < template.SizeX; tx++)
                {
                    var kind = template.Cell(layer, tx, tz);
                    if (kind == ShellCellKind.Ladder) count++;
                    else if (kind == ShellCellKind.StairLower || kind == ShellCellKind.StairUpper) anyStair = true;
                }

                if (anyStair) count++;
            }
            return count;
        }

        [Test]
        public void ADeclaredConnectorJoinsExactlyTwoAdjacentLayers()
        {
            // The rule the Connector constructor enforces from the other side. Asserting it here
            // as well means a stamper that ever declared a two-layer jump fails in this test
            // rather than as an exception from somewhere else entirely.
            var map = Generate(3, damage: false);

            foreach (var connector in map.Result.Context.Connectors)
            {
                int lowerY = SliceSize.FromIndex(connector.LowerCells[0]).Y;
                foreach (int cell in connector.LowerCells)
                    Assert.That(SliceSize.FromIndex(cell).Y, Is.EqualTo(lowerY), "lower cells span layers");
                foreach (int cell in connector.UpperCells)
                    Assert.That(SliceSize.FromIndex(cell).Y, Is.EqualTo(lowerY + 1), "upper end is not one layer up");

                Assert.That(connector.UpperCells.Length, Is.EqualTo(connector.LowerCells.Length));
            }
        }

        // ---------------------------------------------------------------- what gets registered

        [Test]
        public void AnUndamagedMapRegistersEveryConnectorItDeclared()
        {
            // No damage means nothing can have broken a stairwell, so a dropped connector here is
            // a bug in the stamper or the registrar rather than a ruined building.
            for (uint seed = 1; seed <= 5; seed++)
            {
                var map = Generate(seed, damage: false);
                Assert.That(map.Registration.Dropped, Is.Zero,
                    $"seed {seed}: {map.Registration} on a map with no damage in it");
                Assert.That(map.Registration.Registered, Is.EqualTo(map.Result.Context.Connectors.Count));
            }
        }

        [Test]
        public void DamageDropsTheConnectorsItBreaksRatherThanRegisteringThemIntoRubble()
        {
            // The behaviour that matters, stated as a measurement rather than a threshold: a
            // damaged map registers fewer connectors than it declared, and never more.
            int declared = 0, registered = 0;
            for (uint seed = 1; seed <= 5; seed++)
            {
                var map = Generate(seed);
                declared += map.Registration.Declared;
                registered += map.Registration.Registered;

                Assert.That(map.Registration.Registered, Is.LessThanOrEqualTo(map.Registration.Declared));
                Assert.That(map.Registration.Registered, Is.GreaterThan(0),
                    $"seed {seed}: damage destroyed every stairwell on the map, which is not a ruin, it is rubble");
            }

            TestContext.WriteLine(
                $"five damaged slice maps: {registered} of {declared} connectors usable " +
                $"({100.0 * (declared - registered) / declared:F0}% dropped)");
        }

        [Test]
        public void ARegisteredConnectorHasWalkableCellsAtBothEnds()
        {
            var map = Generate(7);

            int checkedEnds = 0;
            foreach (var connector in map.Result.Context.Connectors)
            {
                if (!AllWalkable(map.Grid, connector.LowerCells) || !AllWalkable(map.Grid, connector.UpperCells))
                    continue;
                checkedEnds++;
            }

            Assert.That(checkedEnds, Is.EqualTo(map.Registration.Registered),
                "the registrar's idea of usable and the grid's idea of walkable have parted");
        }

        static bool AllWalkable(CellGrid grid, int[] cells)
        {
            foreach (int cell in cells)
                if (!grid.IsWalkable(cell)) return false;
            return true;
        }

        // ---------------------------------------------------------------- what it is all for

        [Test]
        public void AnUpperStoreyIsReachableFromTheStreetAndTheRouteClimbs()
        {
            // The end-to-end claim: a colonist standing in the street can reach a storey above it,
            // and the route it would walk goes up one layer at a time.
            //
            // Run damaged as well as undamaged, and over several seeds, because the damaged case
            // is the only one that ships and it is the one that could fail: nearly half of a
            // ruined city's stairwells are unusable, so "some connector was registered" is not the
            // same claim as "a colonist can get upstairs". M2's demo needs the second one.
            foreach (bool damage in new[] { false, true })
            for (uint seed = 1; seed <= 5; seed++)
            {
                var map = Generate(seed, damage);
                int start = SliceSize.Index(map.Result.StartCell.X, map.Result.StartCell.Z, map.Result.StartCell.Y);
                string what = $"seed {seed}, damage {(damage ? "on" : "off")}";

                int upstairs = FirstUpperStoreyCell(map);
                Assert.That(upstairs, Is.GreaterThanOrEqualTo(0),
                    $"{what}: no stamped shell had an upper storey the start cell could reach");

                var finder = new PathFinder(map.Nav);
                PathResult path = finder.FindPath(start, upstairs, TraverseMode.Colonist);

                Assert.That(path.Status, Is.EqualTo(PathStatus.Success), $"{what}: reachable but no path");
                AssertNeverSkipsALayer(finder.PathCells, what);
            }
        }

        /// <summary>
        /// The upper end of some registered connector that the start cell can actually reach.
        /// Chosen through the graph rather than by picking a shell, because a ruined city is under
        /// no obligation to leave any particular building open.
        /// </summary>
        static int FirstUpperStoreyCell(Map map)
        {
            int start = SliceSize.Index(map.Result.StartCell.X, map.Result.StartCell.Z, map.Result.StartCell.Y);

            foreach (var connector in map.Result.Context.Connectors)
            {
                foreach (int cell in connector.UpperCells)
                {
                    if (SliceSize.FromIndex(cell).Y <= map.Result.StartCell.Y) continue;
                    if (!map.Grid.IsWalkable(cell)) continue;
                    if (map.Nav.Reachable(start, cell, TraverseMode.Colonist)) return cell;
                }
            }
            return -1;
        }

        /// <summary>
        /// Consecutive cells differ by at most one layer. A path that jumped two would mean a
        /// connector had been registered across a gap, which is the discontinuity the whole
        /// declare-both-ends design exists to refuse.
        /// </summary>
        static void AssertNeverSkipsALayer(System.ReadOnlySpan<int> cells, string what)
        {
            Assert.That(cells.Length, Is.GreaterThan(1), $"{what}: a path of one cell proves nothing");

            var layers = new List<int>(cells.Length);
            foreach (int cell in cells) layers.Add(SliceSize.FromIndex(cell).Y);

            bool climbed = false;
            for (int i = 1; i < layers.Count; i++)
            {
                int step = layers[i] - layers[i - 1];
                Assert.That(System.Math.Abs(step), Is.LessThanOrEqualTo(1),
                    $"{what}: the path jumped from layer {layers[i - 1]} to {layers[i]} in one step");
                if (step != 0) climbed = true;
            }

            Assert.That(climbed, Is.True, $"{what}: the path never changed layer, so it proves nothing about connectors");
        }

        [Test]
        public void ANaturalMapDeclaresNoConnectors()
        {
            // Its layers are strata, not storeys. A colonist reaches them by digging, which builds
            // a connector rather than finding one — so an empty list here is the correct answer,
            // not an oversight.
            var grid = new CellGrid(SliceSize);
            var outcome = MapGenerator.Generate(grid, 1, MapType.Natural);

            Assert.That(outcome.Connectors, Is.Empty);
        }
    }
}
