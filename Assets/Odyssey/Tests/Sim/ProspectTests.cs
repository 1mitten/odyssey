#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Prospecting (design 62 §7, DM6): a Prospect order on a face the colony has cut, a miner
    /// who taps it for a few seconds, and every rock-like cell within a few cells of it known —
    /// its ore glowing as any discovered seam does — without a void opened or a cell changed.
    /// </summary>
    public class ProspectTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(int colonists = 1, uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static JobDef Def => ContentPack.Pawns().Jobs[JobIndex.Prospect];

        /// <summary>A grid of solid rock, nothing known — the geometry tests' board.</summary>
        static CellGrid SolidRock(GridSize size)
        {
            var grid = new CellGrid(size);
            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                grid.Terrain[i] = NaturalContent.TerrainRock;
                grid.Flags[i] |= CellFlags.SolidTerrain;
            }
            return grid;
        }

        static void Open(CellGrid grid, int cell)
        {
            grid.Terrain[cell] = NaturalContent.TerrainAir;
            grid.Flags[cell] &= ~CellFlags.SolidTerrain;
        }

        /// <summary>
        /// The nearest reachable rock, dug out by hand so its walls are a real cut face; then the
        /// nearest of those walls a colonist can stand at to prospect. -1 where the board has none.
        /// </summary>
        static int CutFace(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int rock = -1, bestDistance = int.MaxValue;
            for (int i = 0; i < Size.CellCount; i++)
            {
                if (colony.Grid.Terrain[i] != NaturalContent.TerrainRock) continue;
                if (!colony.Designations.CanMine(i)) continue;
                if (MineWorkGiver.StandToMine(colony.Pawns, pawn, i) < 0) continue;
                CellRef at = Size.FromIndex(i);
                int distance = System.Math.Abs(at.X - start.X) + System.Math.Abs(at.Z - start.Z)
                             + System.Math.Abs(at.Y - start.Y) * 4;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                rock = i;
            }
            if (rock < 0) return -1;

            MineJobDriver.MineCell(colony.Pawns, rock, colony.Grid.Terrain[rock]);
            colony.World.Tick();

            foreach (int wall in Faces(rock))
                if (colony.Designations.CanProspect(wall)
                    && MineWorkGiver.StandAtFace(colony.Pawns, pawn, wall, cutting: false) >= 0)
                    return wall;
            return -1;
        }

        static IEnumerable<int> Faces(int cell)
        {
            CellRef at = Size.FromIndex(cell);
            if (at.X > 0) yield return cell - 1;
            if (at.X < Size.SizeX - 1) yield return cell + 1;
            if (at.Z > 0) yield return cell - Size.SizeX;
            if (at.Z < Size.SizeZ - 1) yield return cell + Size.SizeX;
            if (at.Y > 0) yield return cell - Size.LayerStride;
            if (at.Y < Size.SizeY - 1) yield return cell + Size.LayerStride;
        }

        static bool InBox(GridSize size, int centre, int cell, int radius)
        {
            CellRef c = size.FromIndex(centre), at = size.FromIndex(cell);
            return System.Math.Abs(at.X - c.X) <= radius && System.Math.Abs(at.Z - c.Z) <= radius
                && System.Math.Abs(at.Y - c.Y) <= ProspectJobDriver.LayersEachWay;
        }

        // ---------------------------------------------------------------- where it may be given

        [Test]
        public void AFaceNobodyHasCutRefusesTheOrder()
        {
            ColonyWorld colony = Board();
            int rock = -1;
            for (int i = 0; i < Size.CellCount && rock < 0; i++)
                if (colony.Grid.Terrain[i] == NaturalContent.TerrainRock && colony.Designations.CanMine(i)) rock = i;
            Assume.That(rock, Is.GreaterThanOrEqualTo(0), "the board has rock");
            Assume.That(colony.Grid.IsDiscovered(rock), Is.False, "nothing is known at generation");

            Assert.That(colony.Designations.Designate(Size.FromIndex(rock), DesignationKind.Prospect),
                Is.EqualTo(IntentRejection.NotPermitted), "an uncut face took a prospect");
            Assert.That(colony.Designations.Allows(rock, DesignationKind.Mine), Is.True,
                "the control: the same cell still takes a Mine order");
        }

        [Test]
        public void ACutFaceOfRockTakesTheOrder()
        {
            ColonyWorld colony = Board();
            int face = CutFace(colony);
            Assume.That(face, Is.GreaterThanOrEqualTo(0), "the board has a rock face to cut");

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(face), (int)DesignationKind.Prospect));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty);
            Assert.That(colony.Designations.At(face), Is.EqualTo(DesignationKind.Prospect));
        }

        [Test]
        public void SoftGroundBedrockAndABuriedKnownCellRefuseTheOrder()
        {
            var size = new GridSize(7, 7, 5);
            var grid = SolidRock(size);
            var designations = new DesignationGrid(grid, new List<PlacedEdifice>());

            int hole = size.Index(3, 3, 2);
            Open(grid, hole);
            grid.RevealAround(hole);

            // Soft ground, cut and known: nothing to find in it.
            int soft = size.Index(2, 3, 2);
            grid.Terrain[soft] = NaturalContent.TerrainSubsoil;
            Assert.That(grid.IsExposedFace(soft), Is.True);
            Assert.That(designations.Allows(soft, DesignationKind.Prospect), Is.False, "subsoil took a prospect");

            // Bedrock, cut and known: nothing behind it.
            int floor = size.Index(3, 3, 1);
            grid.Terrain[floor] = NaturalContent.TerrainBedrock;
            Assert.That(designations.Allows(floor, DesignationKind.Prospect), Is.False, "bedrock took a prospect");

            // Rock that a prospect made known, with nothing open beside it: nowhere to stand.
            int buried = size.Index(0, 0, 0);
            grid.Flags[buried] |= CellFlags.Discovered;
            Assert.That(grid.IsExposedFace(buried), Is.False);
            Assert.That(designations.Allows(buried, DesignationKind.Prospect), Is.False, "a buried cell took a prospect");

            // And the control: rock on the cut takes it.
            int face = size.Index(4, 3, 2);
            Assert.That(designations.Allows(face, DesignationKind.Prospect), Is.True, "a cut rock face refused a prospect");
        }

        // ---------------------------------------------------------------- how far it sees

        [Test]
        public void TheRadiusIsThreeAndGrowsByOneAtEachBand()
        {
            JobDef def = Def;
            Assert.That(def.revealRadius, Is.EqualTo(3), "design 62 §7: radius 3");
            Assert.That(def.revealRadiusBands, Is.EqualTo(new[] { 6, 12, 18 }));

            Assert.That(ProspectJobDriver.RadiusFor(def, 0), Is.EqualTo(3));
            Assert.That(ProspectJobDriver.RadiusFor(def, 5), Is.EqualTo(3));
            Assert.That(ProspectJobDriver.RadiusFor(def, 6), Is.EqualTo(4));
            Assert.That(ProspectJobDriver.RadiusFor(def, 11), Is.EqualTo(4));
            Assert.That(ProspectJobDriver.RadiusFor(def, 12), Is.EqualTo(5));
            Assert.That(ProspectJobDriver.RadiusFor(def, 18), Is.EqualTo(6));
            Assert.That(ProspectJobDriver.RadiusFor(def, 20), Is.EqualTo(6));
        }

        [TestCase(0)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(18)]
        public void EveryRockCellInTheSquareIsKnownAndNothingOutsideIt(int level)
        {
            var size = new GridSize(21, 21, 7);
            var grid = SolidRock(size);
            int radius = ProspectJobDriver.RadiusFor(Def, level);
            int face = size.Index(10, 10, 3);

            int revealed = grid.RevealRockWithin(face, radius, ProspectJobDriver.LayersEachWay, chunks: null);

            int side = 2 * radius + 1;
            Assert.That(revealed, Is.EqualTo(side * side * 3), $"level {level}: a {side} x {side} square on three layers");
            for (int i = 0; i < grid.Terrain.Length; i++)
                Assert.That(grid.IsDiscovered(i), Is.EqualTo(InBox(size, face, i, radius)),
                    $"level {level}: {size.FromIndex(i)} is wrongly {(grid.IsDiscovered(i) ? "known" : "unknown")}");
        }

        [Test]
        public void NoVoidIsRevealedAndNoTerrainChanges()
        {
            var size = new GridSize(11, 11, 5);
            var grid = SolidRock(size);
            int face = size.Index(5, 5, 2);

            // A sealed pocket of air in the square — a cavern nobody has breached — and soil and
            // an ore seam beside it.
            int pocket = size.Index(6, 5, 2);
            Open(grid, pocket);
            int soil = size.Index(4, 5, 2);
            grid.Terrain[soil] = NaturalContent.TerrainSubsoil;
            int seam = size.Index(5, 7, 3);
            grid.Terrain[seam] = NaturalContent.TerrainIronOre;
            ushort[] before = (ushort[])grid.Terrain.Clone();

            grid.RevealRockWithin(face, 3, ProspectJobDriver.LayersEachWay, chunks: null);

            Assert.That(grid.IsDiscovered(pocket), Is.False, "the air of a sealed pocket was revealed");
            Assert.That(grid.IsSolidTerrain(pocket), Is.False, "the pocket was filled");
            Assert.That(grid.IsDiscovered(soil), Is.False, "soil is not rock and has nothing to find");
            Assert.That(grid.IsDiscovered(seam), Is.True, "the seam in the square was not found");
            Assert.That(grid.Terrain, Is.EqualTo(before), "a prospect changed what a cell is made of");
        }

        [Test]
        public void TheRevealDirtiesTheChunksOfWhatItRevealedAndNoOthers()
        {
            var size = new GridSize(60, 60, 5);
            var grid = SolidRock(size);
            var chunks = new ChunkGrid(size);
            // On a chunk corner (25, 25), so the square straddles four chunks on each of three layers.
            int face = size.Index(25, 25, 2);

            grid.RevealRockWithin(face, 3, ProspectJobDriver.LayersEachWay, chunks);

            Assert.That(chunks.DirtyCount(), Is.EqualTo(12), "four chunks a layer, three layers");
            Assert.That(chunks.IsDirty(chunks.ChunkIndexOfCell(24, 24, 1)), Is.True);
            Assert.That(chunks.IsDirty(chunks.ChunkIndexOfCell(26, 26, 3)), Is.True);
            Assert.That(chunks.IsDirty(chunks.ChunkIndexOfCell(25, 25, 0)), Is.False, "a layer outside the reach was dirtied");

            // A second prospect over known rock reveals nothing and dirties nothing.
            chunks.ClearAll();
            Assert.That(grid.RevealRockWithin(face, 3, ProspectJobDriver.LayersEachWay, chunks), Is.Zero);
            Assert.That(chunks.DirtyCount(), Is.Zero);
        }

        // ---------------------------------------------------------------- the job

        [Test]
        public void AMinerProspectsTheFaceTrainsMiningAndTheOrderClears()
        {
            ColonyWorld colony = Board();
            int face = CutFace(colony);
            Assume.That(face, Is.GreaterThanOrEqualTo(0), "the board has a rock face to cut");
            Pawn miner = colony.Pawns.Pawns.All[0];

            var knownBefore = new bool[Size.CellCount];
            for (int i = 0; i < Size.CellCount; i++) knownBefore[i] = colony.Grid.IsDiscovered(i);
            ushort[] terrainBefore = (ushort[])colony.Grid.Terrain.Clone();
            int experienceBefore = miner.Skills[SkillIndex.Mining];

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(face), (int)DesignationKind.Prospect));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty);
            bool prospecting = false;
            for (int tick = 0; tick < 12_000 && colony.Designations.At(face) == DesignationKind.Prospect; tick++)
            {
                colony.World.Tick();
                prospecting |= miner.CurrentJob?.DefIndex == JobIndex.Prospect;
            }
            colony.World.Tick(); // the deferred reveal lands in the tick the order clears

            Assert.That(prospecting, Is.True, "nobody took the prospect");
            Assert.That(colony.Designations.At(face), Is.EqualTo(DesignationKind.None), "the order is cleared once carried out");
            Assert.That(colony.Grid.IsSolidTerrain(face), Is.True, "the face was cut rather than read");
            Assert.That(colony.Grid.Terrain, Is.EqualTo(terrainBefore), "a prospect changed what a cell is made of");
            Assert.That(miner.Skills[SkillIndex.Mining], Is.GreaterThan(experienceBefore), "the prospect trained nothing");

            int radius = ProspectJobDriver.RadiusFor(Def, miner.SkillLevel(SkillIndex.Mining));
            int learned = 0;
            for (int i = 0; i < Size.CellCount; i++)
            {
                bool known = colony.Grid.IsDiscovered(i);
                bool rock = colony.Grid.IsSolidTerrain(i) && TerrainHandle.IsRockLike(colony.Grid.Terrain[i]);
                if (InBox(Size, face, i, radius) && rock)
                    Assert.That(known, Is.True, $"{Size.FromIndex(i)} is rock in the square and still unknown");
                if (known && !knownBefore[i])
                {
                    learned++;
                    Assert.That(InBox(Size, face, i, radius), Is.True, $"{Size.FromIndex(i)} was learned outside the square");
                    Assert.That(rock, Is.True, $"{Size.FromIndex(i)} was learned and is not rock");
                }
            }
            Assert.That(learned, Is.GreaterThan(0), "the prospect learned nothing");
        }

        [Test]
        public void TheWorkIsAFifthOfCuttingACellOfRock()
        {
            Assert.That(Def.workTicks * 5, Is.EqualTo(NaturalContent.TerrainAt(NaturalContent.TerrainRock).workToClear),
                "design 62 §7's few seconds: a fifth of mining a rock cell");
            Assert.That(Def.trainsSkill, Is.EqualTo(SkillIndex.Mining));
            Assert.That(Def.experiencePerWorkTick, Is.GreaterThan(0).And.LessThan(ContentPack.Pawns().Jobs[JobIndex.Mine].experiencePerWorkTick),
                "it trains mining, and lightly");
        }

        // ---------------------------------------------------------------- the save

        [Test]
        public void AProspectOrderSurvivesASave()
        {
            ColonyWorld colony = Board();
            int face = CutFace(colony);
            Assume.That(face, Is.GreaterThanOrEqualTo(0), "the board has a rock face to cut");
            Assert.That(colony.Designations.Designate(Size.FromIndex(face), DesignationKind.Prospect), Is.EqualTo(IntentRejection.None));
            ulong hash = colony.World.ComputeStateHash().Value;

            ColonyWorld restored = Board();
            restored.Load(colony.Save());

            Assert.That(restored.Designations.At(face), Is.EqualTo(DesignationKind.Prospect), "the order was lost");
            Assert.That(restored.Designations.CanProspect(face), Is.True, "the face it stands on was not");
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(hash));
        }

        [Test]
        public void AProspectOrderMovesTheStateHash()
        {
            ColonyWorld colony = Board();
            int face = CutFace(colony);
            Assume.That(face, Is.GreaterThanOrEqualTo(0), "the board has a rock face to cut");
            ulong before = colony.World.ComputeStateHash().Value;
            Assert.That(colony.Designations.Designate(Size.FromIndex(face), DesignationKind.Prospect), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(before));
        }
    }
}
