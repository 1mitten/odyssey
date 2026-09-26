#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Underground fog (design 62 §6, DM5): a sealed chamber is air to the simulation and rock to
    /// the colony — unseen, drawn and described as rock, until a cut breaks in and reveals it whole,
    /// once, with the ore on its walls. And the inspect pane's old leak, fixed on the way: a click
    /// on an unexposed seam named the ore in it.
    /// </summary>
    public class CavernFogTests
    {
        // Thirty-two layers, as every offered board is (design 62, DM2): at sixteen a board this
        // small has no rock band deep enough for a chamber.
        static readonly GridSize Size = new GridSize(60, 60, GridSize.OfferedLayers);

        static ColonyWorld Board(uint seed = 1u, bool wooded = true)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: wooded);
        }

        static ColonyWorld CavernBoard()
        {
            ColonyWorld colony = Board();
            Assume.That(colony.Grid.Unseen.Count, Is.GreaterThan(0), "the board has a chamber");
            return colony;
        }

        static IEnumerable<int> Faces(GridSize size, int cell)
        {
            CellRef at = size.FromIndex(cell);
            if (at.X > 0) yield return cell - 1;
            if (at.X < size.SizeX - 1) yield return cell + 1;
            if (at.Z > 0) yield return cell - size.SizeX;
            if (at.Z < size.SizeZ - 1) yield return cell + size.SizeX;
            if (at.Y > 0) yield return cell - size.LayerStride;
            if (at.Y < size.SizeY - 1) yield return cell + size.LayerStride;
        }

        static List<int> UnseenCells(CellGrid grid)
        {
            var cells = new List<int>();
            for (int i = 0; i < grid.Terrain.Length; i++) if (grid.IsUnseen(i)) cells.Add(i);
            return cells;
        }

        /// <summary>The unseen cells face-connected to <paramref name="seed"/>, found independently of the flood.</summary>
        static HashSet<int> Chamber(CellGrid grid, int seed)
        {
            var found = new HashSet<int> { seed };
            var queue = new Queue<int>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                foreach (int n in Faces(grid.Size, cell))
                    if (grid.IsUnseen(n) && found.Add(n)) queue.Enqueue(n);
            }
            return found;
        }

        /// <summary>A rock wall of the chamber: a solid, rock-like face neighbour of one of its cells.</summary>
        static (int wall, int inside) WallOf(CellGrid grid, IEnumerable<int> chamber)
        {
            foreach (int cell in chamber)
                foreach (int n in Faces(grid.Size, cell))
                    if (grid.IsSolidTerrain(n) && TerrainHandle.IsRockLike(grid.Terrain[n])
                        && grid.Terrain[n] != NaturalContent.TerrainBedrock)
                        return (n, cell);
            return (-1, -1);
        }

        static CellDetail Describe(ColonyWorld colony, int cell)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, Size.FromIndex(cell)));
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetCellDetail(cell, out CellDetail detail), Is.True,
                "no detail was published for the cell asked about");
            return detail;
        }

        static int RockWork => NaturalContent.TerrainAt(NaturalContent.TerrainRock).workToClear;

        // ---------------------------------------------------------------- generation

        [Test]
        public void EveryCarvedCellIsUnseenAndNothingElseIs()
        {
            CellGrid grid = PlayedMap.Generate(Size, 1u, out NaturalMapResult result);
            List<int> carved = result.Context.CavernCells;
            Assume.That(carved.Count, Is.GreaterThan(0), "the board has a chamber");

            Assert.That(grid.Unseen.Count, Is.EqualTo(carved.Count), "unseen and carved are different sets");
            foreach (int cell in carved)
            {
                Assert.That(grid.IsUnseen(cell), Is.True, $"carved cell {Size.FromIndex(cell)} is not unseen");
                Assert.That(grid.IsSolidTerrain(cell), Is.False, "the simulation lost the chamber's air");
            }
        }

        [Test]
        public void AFreshColonyStartsWithEveryChamberSealed()
        {
            // RebuildDerived reveals any chamber open to the colony's air. On a fresh board that
            // must be none, or the cavern pass has carved one it did not seal.
            ColonyWorld colony = CavernBoard();
            CellGrid generated = PlayedMap.Generate(Size, 1u, out NaturalMapResult result);

            Assert.That(colony.Grid.Unseen.Count, Is.EqualTo(result.Context.CavernCells.Count),
                "a chamber was revealed before anybody dug");
            Assert.That(colony.Grid.Unseen.RevealBreached(colony.Grid, new List<int>()), Is.Zero,
                "an unseen chamber has an open face at tick zero");
            Assert.That(generated.Unseen.Count, Is.EqualTo(result.Context.CavernCells.Count));
        }

        [Test]
        public void TheBareBoardHasNothingUnseen()
        {
            ColonyWorld colony = Board(wooded: false);
            Assert.That(colony.Grid.Unseen.Count, Is.Zero, "the bare board carries fog it has no chamber for");
        }

        // ---------------------------------------------------------------- what the colony is told

        [Test]
        public void AnUnseenCellIsDescribedExactlyAsTheRockBesideItWouldBe()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;

            // A chamber cell with rock under it, and a real rock cell with rock under it and rock
            // over it: the pane must say the same thing of both.
            int hidden = UnseenCells(grid).First(c => grid.IsSolidTerrain(c - Size.LayerStride));
            int rock = -1;
            for (int i = Size.LayerStride; i < Size.CellCount - Size.LayerStride && rock < 0; i++)
                if (grid.Terrain[i] == NaturalContent.TerrainRock && grid.IsSolidTerrain(i)
                    && grid.Terrain[i - Size.LayerStride] == NaturalContent.TerrainRock
                    && grid.Terrain[i + Size.LayerStride] == NaturalContent.TerrainRock
                    && !grid.IsDiscovered(i))
                    rock = i;
            Assume.That(rock, Is.GreaterThanOrEqualTo(0), "the board has buried rock");

            CellDetail h = Describe(colony, hidden);
            CellDetail r = Describe(colony, rock);

            Assert.That(h.Terrain, Is.EqualTo((byte)NaturalContent.TerrainRock), "the pane named the void");
            Assert.That(h.WorkToClear, Is.EqualTo(r.WorkToClear), "the void quotes other work than rock");
            Assert.That(h.MoveCostPerMille, Is.EqualTo(r.MoveCostPerMille));
            Assert.That(h.Support, Is.EqualTo(r.Support), "the void's support is not rock's");
            Assert.That(h.IsIndoors, Is.EqualTo(r.IsIndoors), "the chamber reads as a room");
            // One tick apart, so the outdoor curve both read may have moved by a hair; a chamber
            // read as its own room would be degrees off the ground's reading, not a hair.
            Assert.That(h.AmbientTempC, Is.EqualTo(r.AmbientTempC).Within(10),
                "the chamber's own air gave its temperature away");
        }

        /// <summary>
        /// The leak design 62 §3 found: the pane published an unexposed seam's real terrain and
        /// work. A sweep of every kind of hidden thing on a real board — unexposed seams and unseen
        /// chambers, up to forty of each — must name nothing but rock.
        /// </summary>
        [Test]
        public void ThePaneNeverNamesUndiscoveredOreOrAnUnseenVoid()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;

            var hidden = new List<int>();
            int seams = 0;
            for (int i = 0; i < Size.CellCount && seams < 40; i++)
                if (NaturalContent.IsOre(grid.Terrain[i]) && !grid.IsDiscovered(i)) { hidden.Add(i); seams++; }
            Assume.That(seams, Is.GreaterThan(0), "the board has ore");
            hidden.AddRange(UnseenCells(grid).Take(40));

            foreach (int cell in hidden)
            {
                CellDetail detail = Describe(colony, cell);
                Assert.That(detail.Terrain, Is.EqualTo((byte)NaturalContent.TerrainRock),
                    $"the pane named terrain {detail.Terrain} at hidden cell {Size.FromIndex(cell)}");
                Assert.That(detail.WorkToClear, Is.EqualTo((ushort)RockWork),
                    $"the pane quoted the true work of hidden cell {Size.FromIndex(cell)}");
            }
        }

        [Test]
        public void ADiscoveredSeamIsDescribedAsItself()
        {
            // The control for the sweep above: the fog hides, it does not flatten.
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            int seam = Enumerable.Range(0, Size.CellCount)
                .First(i => NaturalContent.IsOre(grid.Terrain[i]) && !grid.IsDiscovered(i));

            grid.Flags[seam] |= CellFlags.Discovered;
            CellDetail detail = Describe(colony, seam);
            Assert.That(detail.Terrain, Is.EqualTo((byte)grid.Terrain[seam]), "a known seam reads as rock");
        }

        // ---------------------------------------------------------------- the breach

        [Test]
        public void ACutIntoAChamberRevealsAllOfItOnceAndDiscoversItsWalls()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;

            var (wall, inside) = WallOf(grid, UnseenCells(grid));
            Assume.That(wall, Is.GreaterThanOrEqualTo(0), "a chamber has a rock wall");
            HashSet<int> chamber = Chamber(grid, inside);
            int before = grid.Unseen.Count;

            MineJobDriver.MineCell(colony.Pawns, wall, grid.Terrain[wall]);

            foreach (int cell in chamber)
            {
                Assert.That(grid.IsUnseen(cell), Is.False, $"chamber cell {Size.FromIndex(cell)} is still unseen");
                foreach (int n in Faces(Size, cell))
                    if (grid.IsSolidTerrain(n))
                        Assert.That(grid.IsDiscovered(n), Is.True,
                            $"the wall {Size.FromIndex(n)} of a revealed chamber is not known");
            }
            Assert.That(grid.Unseen.Count, Is.EqualTo(before - chamber.Count),
                "the breach revealed more, or less, than the one chamber it broke into");
            Assert.That(CavernBreach.Open(colony.Pawns, wall), Is.Zero, "a chamber was revealed twice");
        }

        [Test]
        public void TheOreOnARevealedChambersWallsShows()
        {
            // Seeded chambers carry ore on their walls (design 62 §5c: gems and gold favour them).
            // Every chamber is broken into in turn; whatever wall ore exists must be known after.
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            int wallOre = 0;

            while (grid.Unseen.Count > 0)
            {
                var (wall, inside) = WallOf(grid, UnseenCells(grid));
                if (wall < 0) break;
                HashSet<int> chamber = Chamber(grid, inside);
                MineJobDriver.MineCell(colony.Pawns, wall, grid.Terrain[wall]);
                foreach (int cell in chamber)
                    foreach (int n in Faces(Size, cell))
                        if (NaturalContent.IsOre(grid.Terrain[n]))
                        {
                            wallOre++;
                            Assert.That(grid.IsDiscovered(n), Is.True, "ore on a revealed wall is hidden");
                            Assert.That(grid.SeenTerrain(n), Is.EqualTo(grid.Terrain[n]));
                        }
            }

            Assume.That(wallOre, Is.GreaterThan(0), "no chamber on this board has ore on its walls");
        }

        [Test]
        public void ACutAwayFromEveryChamberRevealsNothing()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            int before = grid.Unseen.Count;

            int rock = -1;
            for (int i = 0; i < Size.CellCount && rock < 0; i++)
                if (grid.Terrain[i] == NaturalContent.TerrainRock && grid.IsSolidTerrain(i)
                    && !Faces(Size, i).Any(grid.IsUnseen))
                    rock = i;

            MineJobDriver.MineCell(colony.Pawns, rock, grid.Terrain[rock]);
            Assert.That(grid.Unseen.Count, Is.EqualTo(before), "a cut beside no chamber revealed one");
        }

        [Test]
        public void TheBreachMarksTheChambersChunksForDrawing()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            var (wall, inside) = WallOf(grid, UnseenCells(grid));
            HashSet<int> chamber = Chamber(grid, inside);
            ChunkGrid chunks = colony.Pawns.Chunks!;
            chunks.ClearAll();

            MineJobDriver.MineCell(colony.Pawns, wall, grid.Terrain[wall]);

            foreach (int cell in chamber)
                Assert.That(chunks.IsDirty(chunks.ChunkIndexOfCell(Size.FromIndex(cell))), Is.True,
                    $"the chunk of revealed cell {Size.FromIndex(cell)} will not re-copy");
        }

        // ---------------------------------------------------------------- orders

        [Test]
        public void AnUnseenCellTakesAMineOrderAsRockDoesAndIsPricedAsRock()
        {
            ColonyWorld colony = CavernBoard();
            int hidden = UnseenCells(colony.Grid)[0];

            Assert.That(colony.Designations.Designate(Size.FromIndex(hidden), DesignationKind.Mine, rockOnly: true),
                Is.EqualTo(IntentRejection.None), "a rock-only drag parted round the chamber");
            Assert.That(colony.Designations.WorkFor(hidden), Is.EqualTo(RockWork),
                "the order on the void is priced as something other than the rock it looks like");
        }

        [Test]
        public void AnOrderOnAChamberIsTakenOffWhenTheBreachShowsItWasAir()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            var (wall, inside) = WallOf(grid, UnseenCells(grid));
            colony.Designations.Designate(Size.FromIndex(inside), DesignationKind.Mine);
            Assume.That(colony.Designations.At(inside), Is.EqualTo(DesignationKind.Mine));

            MineJobDriver.MineCell(colony.Pawns, wall, grid.Terrain[wall]);

            Assert.That(colony.Designations.At(inside), Is.EqualTo(DesignationKind.None),
                "an order stands on a cell that turned out to be air");
        }

        [Test]
        public void MiningAnUnseenCellFromADiagonalRevealsItsChamberAndYieldsNothing()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            int hidden = UnseenCells(grid)[0];
            HashSet<int> chamber = Chamber(grid, hidden);
            int items = colony.Pawns.Items.Items.Count;

            MineJobDriver.MineCell(colony.Pawns, hidden, grid.Terrain[hidden]);

            Assert.That(chamber.All(c => !grid.IsUnseen(c)), Is.True, "the chamber stayed unseen");
            Assert.That(colony.Pawns.Items.Items.Count, Is.EqualTo(items), "a void yielded something");
        }

        [Test]
        public void NothingCanBeBuiltOrZonedInAnUnseenChamber()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            int hidden = UnseenCells(grid).First(grid.IsWalkable);

            Assert.That(colony.Construction.Allows(hidden), Is.False, "a wall can be ordered inside the rock");
            Assert.That(colony.Pawns.Storage!.SiteAllows(hidden), Is.False, "a stockpile can be painted inside the rock");
            Assert.That(colony.Pawns.Power!.AllowsLine(hidden), Is.False, "a line can be laid inside the rock");
        }

        // ---------------------------------------------------------------- the hash and the save

        [Test]
        public void TheBitsetIsHashedOnlyWhileItHoldsACell()
        {
            var unseen = new UnseenCells(new GridSize(4, 4, 4));
            var empty = StateHash.New();
            unseen.ContributeTo(ref empty);
            Assert.That(empty.Value, Is.EqualTo(StateHash.New().Value), "an empty bitset moved the hash");

            unseen.Add(5);
            var one = StateHash.New();
            unseen.ContributeTo(ref one);
            Assert.That(one.Value, Is.Not.EqualTo(StateHash.New().Value), "an unseen cell is not in the hash");
        }

        [Test]
        public void ASaveKeepsWhatHasBeenSeenAndWhatHasNot()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            var (wall, _) = WallOf(grid, UnseenCells(grid));
            MineJobDriver.MineCell(colony.Pawns, wall, grid.Terrain[wall]);
            List<int> expected = UnseenCells(grid);
            Assume.That(expected.Count, Is.GreaterThan(0), "a second chamber is still unseen");

            byte[] bytes = colony.Save();
            ColonyWorld restored = Board();
            restored.Load(bytes);

            Assert.That(UnseenCells(restored.Grid), Is.EqualTo(expected), "the bitset did not survive a save");
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
        }

        [Test]
        public void ASaveFromBeforeTheFogReopensTheChambersItHadBrokenInto()
        {
            ColonyWorld colony = CavernBoard();
            CellGrid grid = colony.Grid;
            var (wall, _) = WallOf(grid, UnseenCells(grid));
            MineJobDriver.MineCell(colony.Pawns, wall, grid.Terrain[wall]);
            List<int> expected = UnseenCells(grid);

            // The components this build writes, less the new section: a save as the build before
            // DM5 would have written it. The fresh board it loads into has every chamber unseen.
            var older = colony.SaveComponents.Where(c => c.SaveKey != grid.Unseen.SaveKey).ToArray();
            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, older);

            ColonyWorld restored = Board();
            stream.Position = 0;
            restored.Load(stream);

            Assert.That(UnseenCells(restored.Grid), Is.EqualTo(expected),
                "a chamber the old save had broken into came back unseen, or a sealed one was opened");
        }

        [Test]
        public void ABoardWithNoChamberSavesNoMoreThanAnEmptyCount()
        {
            ColonyWorld colony = Board(wooded: false);
            var unseen = colony.Grid.Unseen;

            using var without = new MemoryStream();
            WorldSave.Save(colony.World, without, System.Array.Empty<ISaveable>());
            using var with = new MemoryStream();
            WorldSave.Save(colony.World, with, new ISaveable[] { unseen });

            int header = 4 + System.Text.Encoding.UTF8.GetByteCount(unseen.SaveKey) + 4;
            Assert.That(with.Length - without.Length, Is.EqualTo(header + 4),
                "a board with no chamber wrote more than a count of nought");
        }
    }
}
