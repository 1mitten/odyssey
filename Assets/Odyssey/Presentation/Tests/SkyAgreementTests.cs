#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// One column rule, two readers (design 43 §6, §10; P1): the simulation's shelter map and the
    /// drawing's rain-stop texture answer the same question from different copies of the world —
    /// the cell grid and the render mirror — and must answer it the same, cell by cell, on the
    /// board the game plays. The <c>TerraceFoot</c>/<c>BankLayout</c> pattern: if they disagree, a
    /// colonist is slowed by rain the player can see is not falling on her.
    /// </summary>
    public class SkyAgreementTests
    {
        /// <summary>The played board: the game's own chooser, barren + wooded, at the standard size.</summary>
        static readonly GridSize Size = new GridSize(120, 120, 16);

        sealed class Played : System.IDisposable
        {
            public readonly ColonyWorld Colony;
            public readonly WorldRenderModel Model;
            readonly ModuleLibrary _library = new ModuleLibrary(null);

            public Played()
            {
                Colony = ColonyWorld.Build(Size, 1u, ScenarioDef.Bare(), barren: true, wooded: true);
                Model = new WorldRenderModel(Size, Colony.Pawns.Chunks!, _library);
                Model.RefreshAll(Colony.Grid, Edifices);
            }

            public IReadOnlyList<PlacedEdifice> Edifices => Colony.Pawns.Construction!.Edifices.Records;

            public void Dispose() => _library.Dispose();
        }

        /// <summary>Every column and every cell where the two readers differ, as readable lines.</summary>
        static List<string> Disagreements(Played board)
        {
            var found = new List<string>();
            SkyColumns sky = board.Colony.Pawns.Sky!;
            var mirror = new MirrorSkySource(board.Model);
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                SkyColumn drawn = SkyColumnRule.Compute(mirror, x, z);
                SkyColumn simulated = sky.ColumnAt(x, z);
                if (!drawn.Equals(simulated)) found.Add($"({x},{z}) drawn {drawn}, simulated {simulated}");

                for (int y = 0; y < Size.SizeY; y++)
                {
                    int cell = Size.Index(x, z, y);
                    if (sky.ShelteredFromSky(cell) != y < drawn.StopLayer)
                        found.Add($"cell ({x},{z},{y}): simulated {(sky.ShelteredFromSky(cell) ? "dry" : "wet")}");
                }
            }
            return found;
        }

        [Test]
        public void TheSimulationAndTheMirrorAgreeCellByCellOnThePlayedBoard()
        {
            using var board = new Played();
            List<string> found = Disagreements(board);
            Assert.That(found, Is.Empty, string.Join("\n", found.GetRange(0, System.Math.Min(20, found.Count))));

            // And the board is worth agreeing about: open sky, woodland and water all present.
            int canopy = 0, water = 0, ground = 0;
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
                switch (board.Colony.Pawns.Sky!.ColumnAt(x, z).Kind)
                {
                    case SkyStop.Canopy: canopy++; break;
                    case SkyStop.Water: water++; break;
                    case SkyStop.Ground: ground++; break;
                }
            TestContext.WriteLine($"columns: {ground} ground, {canopy} canopy, {water} water");
            Assert.That(canopy, Is.GreaterThan(0), "the wooded board has no canopy, so it tested nothing about trees");
            Assert.That(water, Is.GreaterThan(0), "the played board has no water");
            Assert.That(ground, Is.GreaterThan(0));
        }

        [Test]
        public void TheyStillAgreeAfterEditsTheMirrorIsToldAbout()
        {
            using var board = new Played();
            ColonyWorld colony = board.Colony;

            // A roof over open ground by the start and a stand of trees taken down, through the
            // simulation's own edit paths: each tells the chunk grid, which is what both readers
            // hear.
            CellRef start = colony.Start;
            int roofed = 0;
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = 3; dx <= 5; dx++)
            {
                int above = Size.Index(start.X + dx, start.Z + dz, start.Y + 1);
                if (colony.Grid.Floor[above] != 0 || colony.Grid.IsSolidTerrain(above)) continue;
                colony.Grid.Floor[above] = CoreContent.SlabBuilt;
                colony.Pawns.Chunks!.MarkDirty(Size.FromIndex(above));
                roofed++;
            }
            int felled = 0;
            for (int i = 0; i < Size.CellCount && felled < 12; i++)
            {
                if (!colony.Designations.IsTree(i)) continue;
                colony.Grid.Terrain[i - Size.LayerStride] = CoreContent.TerrainAir;
                colony.Grid.Flags[i - Size.LayerStride] &= ~CellFlags.SolidTerrain;
                // As a collapse reports the cell that went (SupportSystem): the ground is a layer,
                // and so a chunk, below the tree's own.
                colony.Pawns.Chunks!.MarkDirty(Size.FromIndex(i - Size.LayerStride));
                if (Falling.TreesOutOf(colony.Pawns, i)) felled++;
            }
            Assume.That(roofed, Is.GreaterThan(0));
            Assume.That(felled, Is.EqualTo(12));

            board.Model.RefreshDirty(colony.Grid, board.Edifices);
            List<string> found = Disagreements(board);
            Assert.That(found, Is.Empty, string.Join("\n", found.GetRange(0, System.Math.Min(20, found.Count))));
        }

        [Test]
        public void AnEditOnOneSideOnlyIsFound()
        {
            // The negative control (P11): the comparison can see a difference. A roof reaches the
            // simulation and the mirror is not refreshed, so only one reader knows about it.
            using var board = new Played();
            Assume.That(Disagreements(board), Is.Empty);

            ColonyWorld colony = board.Colony;
            CellRef start = colony.Start;
            int above = Size.Index(start.X, start.Z, start.Y + 3);
            Assume.That(colony.Grid.Floor[above], Is.EqualTo(0));
            colony.Grid.Floor[above] = CoreContent.SlabBuilt;
            colony.Pawns.Chunks!.MarkDirty(Size.FromIndex(above));

            List<string> found = Disagreements(board);
            Assert.That(found, Is.Not.Empty, "a roof only the simulation knows about went unnoticed");
            Assert.That(found[0], Does.StartWith($"({start.X},{start.Z})"));
        }

        [Test]
        public void TheTextureIsTheRulesAnswerInMetres()
        {
            // SkyHeightMap asks the rule and does not keep a copy of it: its heights are exactly the
            // simulation's layers turned into metres, on every column of the played board.
            using var board = new Played();
            using var map = new SkyHeightMap(board.Model);
            map.Rebuild();
            SkyColumns sky = board.Colony.Pawns.Sky!;
            int wrong = 0;
            string first = "";
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                float metres = SkyHeightMap.Metres(sky.ColumnAt(x, z), out byte kind);
                if (map.StopAt(x, z) == metres && map.KindAt(x, z) == kind) continue;
                if (wrong++ == 0) first = $"({x},{z}): texture {map.StopAt(x, z)} m kind {map.KindAt(x, z)}, simulation {metres} m kind {kind}";
            }
            Assert.That(wrong, Is.Zero, first);
        }
    }
}
