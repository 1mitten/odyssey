#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The question the inspect pane asks and the world answers: one <see cref="CellDetail"/> row
    /// for the asked-about cell, real values, withdrawn when the question is.
    ///
    /// <para>The three player reports this stands on (owner, 2026-09-17): a wood pile read as a
    /// generic placeholder with no count, a rock could be clicked but not told from grass, and a
    /// water tile said nothing about being water. The first is the interface's to fix; the second
    /// and third are this seam's — the pane cannot say what it is never told.</para>
    /// </summary>
    public class CellDetailTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: true);
        }

        /// <summary>
        /// Ask about one cell and hand back the frame that answers. The submit-then-tick shape is
        /// the running world's; the paused world's is <see cref="APausedWorldAnswersWithoutASpentTick"/>.
        /// </summary>
        static WorldSnapshot Ask(ColonyWorld colony, CellRef cell)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, cell));
            colony.World.Tick();
            return colony.World.Views.Current;
        }

        /// <summary>
        /// Write a terrain the way the generator's own passes do — the index, and the two flags
        /// that follow from it — so a hand-made water cell is the cell a real map would hold.
        /// </summary>
        static void SetTerrain(ColonyWorld colony, int index, ushort terrain)
        {
            var grid = colony.Grid;
            grid.Terrain[index] = terrain;
            if (NaturalContent.IsSolid(terrain)) grid.Flags[index] |= CellFlags.SolidTerrain;
            else grid.Flags[index] &= ~CellFlags.SolidTerrain;
            if (NaturalContent.IsImpassable(terrain)) grid.Flags[index] |= CellFlags.ImpassableTerrain;
            else grid.Flags[index] &= ~CellFlags.ImpassableTerrain;
        }

        /// <summary>
        /// A solid grass cell on the surface beside the start, ours to rewrite. The start cell
        /// itself is the air a colonist stands in, so the search begins at the start's layer and
        /// walks down the column to the grass under it.
        /// </summary>
        static int GrassNeighbourOfTheStart(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            for (int y = start.Y; y >= 0; y--)
            for (int dx = -4; dx <= 4; dx++)
            for (int dz = -4; dz <= 4; dz++)
            {
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, y)) continue;
                int index = Size.Index(x, z, y);
                if (colony.Grid.Terrain[index] == NaturalContent.TerrainGrass) return index;
            }
            return -1;
        }

        // ---- the handle tables ------------------------------------------------------------

        /// <summary>
        /// The contract's terrain and edifice numbers are the simulation's own, restated. Nothing
        /// in either assembly can check that — each side can only see its own copy — so this test
        /// is the weld. <c>WorldContentDefTests</c> already holds the simulation's constants to
        /// <c>WorldContent.TerrainOrder</c>; this holds the contract to the same order.
        /// </summary>
        [Test]
        public void TheContractMirrorsTheSimulationTerrainAndEdificeNumbers()
        {
            Assert.That(TerrainHandle.Count, Is.EqualTo(WorldContent.TerrainOrder.Length),
                "a terrain the contract does not count is a terrain the pane cannot name");

            Assert.That(TerrainHandle.Air, Is.EqualTo(CoreContent.TerrainAir));
            Assert.That(TerrainHandle.Rock, Is.EqualTo(CoreContent.TerrainRock));
            Assert.That(TerrainHandle.Grass, Is.EqualTo(NaturalContent.TerrainGrass));
            Assert.That(TerrainHandle.Bedrock, Is.EqualTo(NaturalContent.TerrainBedrock));
            Assert.That(TerrainHandle.IronOre, Is.EqualTo(NaturalContent.TerrainIronOre));
            Assert.That(TerrainHandle.CoalSeam, Is.EqualTo(NaturalContent.TerrainCoalSeam));
            Assert.That(TerrainHandle.ShallowWater, Is.EqualTo(NaturalContent.TerrainShallowWater));
            Assert.That(TerrainHandle.DeepWater, Is.EqualTo(NaturalContent.TerrainDeepWater));
            Assert.That(TerrainHandle.Marsh, Is.EqualTo(NaturalContent.TerrainMarsh));

            // The bed, the shelf, the campfire, the generator and the heater are the edifices past
            // the generators' own numbering: CoreContent's ids end at 9, the trees continue from 10,
            // and each takes the next free id rather than either family's next offset - see
            // CoreContent.EdificeBed for why 12 and not 10, EdificeShelf for 13, EdificeCampfire for
            // 14, and design 32 for the generator's 15 and the heater's 16.
            // The wild things of design 45 continue after the heater: fruit tree 17, giant 18,
            // bush 19, berry bush 20 and 21 picked.
            // And the galley after them (design 48): 22, the Electric Cooker.
            Assert.That(NaturalContent.EdificeLimit, Is.EqualTo(CoreContent.EdificeGalley));
            Assert.That(EdificeHandle.Galley, Is.EqualTo(CoreContent.EdificeGalley));
            // Cover's sandbags (design 53 §4) after the galley.
            Assert.That(EdificeHandle.Sandbags, Is.EqualTo(CoreContent.EdificeSandbags));
            Assert.That(EdificeHandle.Count, Is.EqualTo(CoreContent.EdificeSandbags + 1));
            Assert.That(EdificeHandle.TreeFruit, Is.EqualTo(NaturalContent.EdificeTreeFruit));
            Assert.That(EdificeHandle.TreeGiant, Is.EqualTo(NaturalContent.EdificeTreeGiant));
            Assert.That(EdificeHandle.Bush, Is.EqualTo(NaturalContent.EdificeBush));
            Assert.That(EdificeHandle.BerryBush, Is.EqualTo(NaturalContent.EdificeBerryBush));
            Assert.That(EdificeHandle.BerryBushPicked, Is.EqualTo(NaturalContent.EdificeBerryBushPicked));
            Assert.That(EdificeHandle.Wall, Is.EqualTo(CoreContent.EdificeWall));
            Assert.That(EdificeHandle.Door, Is.EqualTo(CoreContent.EdificeDoor));
            Assert.That(EdificeHandle.Ladder, Is.EqualTo(CoreContent.EdificeLadder));
            Assert.That(EdificeHandle.TreeBirch, Is.EqualTo(NaturalContent.EdificeTreeBirch));
            Assert.That(EdificeHandle.TreeMeadow, Is.EqualTo(NaturalContent.EdificeTreeMeadow));
            Assert.That(EdificeHandle.Bed, Is.EqualTo(CoreContent.EdificeBed));
            Assert.That(EdificeHandle.Shelf, Is.EqualTo(CoreContent.EdificeShelf));
            Assert.That(EdificeHandle.Campfire, Is.EqualTo(CoreContent.EdificeCampfire));
            Assert.That(EdificeHandle.Generator, Is.EqualTo(CoreContent.EdificeGenerator));
            Assert.That(EdificeHandle.Heater, Is.EqualTo(CoreContent.EdificeHeater));
        }

        // ---- the answer -------------------------------------------------------------------

        [Test]
        public void GrassAnswersGrassAtFullSpeedAndNothingElse()
        {
            ColonyWorld colony = Board();
            int index = GrassNeighbourOfTheStart(colony);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "the start clearing is grass");

            var frame = Ask(colony, Size.FromIndex(index));

            Assert.That(frame.TryGetCellDetail(index, out CellDetail detail), Is.True);
            Assert.That(detail.Terrain, Is.EqualTo(TerrainHandle.Grass));
            Assert.That(detail.MoveCostPerMille, Is.EqualTo(1000), "firm ground is the measure itself");
            Assert.That(detail.Edifice, Is.EqualTo(EdificeHandle.None));
            Assert.That(detail.FloorStuff, Is.EqualTo(StuffHandle.None));
        }

        /// <summary>
        /// The report that started this: a water tile said nothing about being water. Shallow
        /// water is wadeable at exactly a third of walking pace — the addend worldgen owns is
        /// +200 hundredths — and the row says so in the ratio a player reads.
        /// </summary>
        [Test]
        public void ShallowWaterAnswersWaterAtAThirdSpeed()
        {
            ColonyWorld colony = Board();
            int index = GrassNeighbourOfTheStart(colony);
            SetTerrain(colony, index, NaturalContent.TerrainShallowWater);

            var frame = Ask(colony, Size.FromIndex(index));

            Assert.That(frame.TryGetCellDetail(index, out CellDetail detail), Is.True);
            Assert.That(detail.Terrain, Is.EqualTo(TerrainHandle.ShallowWater));
            Assert.That(detail.MoveCostPerMille, Is.EqualTo(3000),
                "the +200 hundredths worldgen owns, restated as a ratio");
        }

        [Test]
        public void DeepWaterAnswersThatItCannotBeCrossed()
        {
            ColonyWorld colony = Board();
            int index = GrassNeighbourOfTheStart(colony);
            SetTerrain(colony, index, NaturalContent.TerrainDeepWater);

            var frame = Ask(colony, Size.FromIndex(index));

            Assert.That(frame.TryGetCellDetail(index, out CellDetail detail), Is.True);
            Assert.That(detail.MoveCostPerMille, Is.EqualTo(0),
                "impassable is not 'very expensive'; the pathfinder routes round, and so does the pane");
        }

        /// <summary>
        /// Marsh is solid ground that is merely slow, so it keeps a speed rather than losing the
        /// right to one — 1400 is boggy, not wading.
        /// </summary>
        [Test]
        public void MarshAnswersAsSlowGround()
        {
            ColonyWorld colony = Board();
            int index = GrassNeighbourOfTheStart(colony);
            SetTerrain(colony, index, NaturalContent.TerrainMarsh);

            var frame = Ask(colony, Size.FromIndex(index));

            Assert.That(frame.TryGetCellDetail(index, out CellDetail detail), Is.True);
            Assert.That(detail.Terrain, Is.EqualTo(TerrainHandle.Marsh));
            Assert.That(detail.MoveCostPerMille, Is.EqualTo(1400));
        }

        /// <summary>
        /// A rock's whole point is the work of it, so the row carries the same number the mine
        /// order charges — read from the table rather than restated, because content values are
        /// pinned by their own fingerprints and this test is about the seam, not the number.
        /// </summary>
        [Test]
        public void RockAnswersItsWorkToClear()
        {
            ColonyWorld colony = Board();
            int index = -1;
            for (int i = 0; i < colony.Grid.Terrain.Length; i++)
                if (colony.Grid.Terrain[i] == CoreContent.TerrainRock) { index = i; break; }
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "there is rock beneath the meadow");

            var frame = Ask(colony, Size.FromIndex(index));

            Assert.That(frame.TryGetCellDetail(index, out CellDetail detail), Is.True);
            Assert.That(detail.Terrain, Is.EqualTo(TerrainHandle.Rock));
            Assert.That(detail.WorkToClear,
                Is.EqualTo(WorldContent.Table[CoreContent.TerrainRock].workToClear));
        }

        [Test]
        public void ATreeStandingInTheCellIsInTheAnswer()
        {
            ColonyWorld colony = Board();
            int index = -1;
            var records = colony.Construction.Edifices.Records;
            for (int i = 0; i < records.Count; i++)
                if (!records[i].Removed && NaturalContent.IsTree(records[i].Def))
                { index = records[i].CellIndex; break; }
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "a wooded board has trees");

            var frame = Ask(colony, Size.FromIndex(index));

            Assert.That(frame.TryGetCellDetail(index, out CellDetail detail), Is.True);
            Assert.That(detail.Edifice, Is.GreaterThanOrEqualTo(EdificeHandle.TreeBirch),
                "the tree is the thing the player clicked");
        }

        // ---- the question's lifecycle -----------------------------------------------------

        [Test]
        public void WithdrawingTheQuestionPublishesNoRow()
        {
            ColonyWorld colony = Board();
            int index = GrassNeighbourOfTheStart(colony);
            Ask(colony, Size.FromIndex(index));
            Assert.That(colony.World.Views.Current.CellDetailCount, Is.EqualTo(1));

            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, default, -1));
            colony.World.Tick();

            Assert.That(colony.World.Views.Current.CellDetailCount, Is.Zero,
                "the row is a report of a standing question, not cell state");
        }

        [Test]
        public void NoQuestionStandingPublishesNoRow()
        {
            ColonyWorld colony = Board();
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.CellDetailCount, Is.Zero,
                "the channel costs nothing while nobody is asking");
        }

        [Test]
        public void ACellOffTheBoardIsQuietlyIgnored()
        {
            ColonyWorld colony = Board();
            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, new CellRef(9999, 0, 0)));
            colony.World.Tick();

            Assert.That(colony.World.Views.Current.CellDetailCount, Is.Zero);
            Assert.That(colony.World.Intents.Rejected, Is.Empty,
                "a pick that resolved off-board never had a subject; silence is not an error");
        }

        /// <summary>
        /// The paused world: a question queued as an intent would wait for a tick boundary that
        /// never comes, and the player inspects a stopped world more than a running one. The
        /// republish answers the question without moving the tick counter, and a command queued
        /// behind it stays queued — only questions may cross the boundary out of turn.
        /// </summary>
        [Test]
        public void APausedWorldAnswersWithoutASpentTick()
        {
            ColonyWorld colony = Board();
            int index = GrassNeighbourOfTheStart(colony);
            int tick = colony.World.CurrentTick;

            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, Size.FromIndex(index)));
            colony.World.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
            colony.World.RepublishViews();

            var frame = colony.World.Views.Current;
            Assert.That(frame.TryGetCellDetail(index, out _), Is.True,
                "the answer is published without a tick being spent");
            Assert.That(colony.World.CurrentTick, Is.EqualTo(tick));
            Assert.That(colony.World.Intents.PendingCount, Is.EqualTo(1),
                "the command waits for a real tick boundary, as it must");
            Assert.That(colony.World.GameSpeed, Is.EqualTo(1), "and has not been applied early");
        }

        [Test]
        public void AnIndoorsCellReportsIndoorsOnCellDetail()
        {
            ColonyWorld colony = Board();
            int index = GrassNeighbourOfTheStart(colony);
            CellRef at = Size.FromIndex(index);

            // Outdoors initially
            var frame = Ask(colony, at);
            Assert.That(frame.TryGetCellDetail(index, out CellDetail outdoorDetail), Is.True);
            Assert.That(outdoorDetail.IsIndoors, Is.False, "bare grass is outdoors");

            // Build a small enclosed room on the layer above the grass (at.Y + 1)
            int minX = at.X - 1, maxX = at.X + 1;
            int minZ = at.Z - 1, maxZ = at.Z + 1;
            int roomY = at.Y + 1;
            int indoorIndex = Size.Index(at.X, at.Z, roomY);
            CellRef indoorCell = new CellRef(at.X, at.Z, roomY);

            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                int c = Size.Index(x, z, roomY);
                if (x == at.X && z == at.Z) continue;

                ushort def = (ushort)((x == minX && z == at.Z) ? CoreContent.EdificeDoor : CoreContent.EdificeWall);
                colony.Construction.Edifices.Records.Add(new PlacedEdifice
                {
                    CellIndex = c,
                    Def = def,
                    Removed = false
                });
                colony.Pawns.Cells.Edifice[c] = colony.Construction.Edifices.Records.Count - 1;

                // Roof on layer roomY + 1
                colony.Pawns.Cells.Floor[Size.Index(x, z, roomY + 1)] = CoreContent.SlabBuilt;
            }
            colony.Pawns.Cells.Floor[Size.Index(at.X, at.Z, roomY + 1)] = CoreContent.SlabBuilt;
            colony.Pawns.Enclosure?.MarkAllDirty();

            frame = Ask(colony, indoorCell);
            Assert.That(frame.TryGetCellDetail(indoorIndex, out CellDetail indoorDetail), Is.True);
            Assert.That(indoorDetail.IsIndoors, Is.True, "enclosed roofed room reports IsIndoors = true");
        }
    }
}
