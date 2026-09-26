#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Dig and Mine (design 62 §4): soft ground stays diggable by the Mine order and only the word
    /// differs, and a Mine drag begun on rock marks only rock.
    ///
    /// <para>The simulation's half. The drag's decision is taken once by the interface
    /// (<c>DesignateDirector.Begin</c>, held by <c>DesignateDirectorTests</c>) and carried on every
    /// intent as <see cref="DesignateRun"/>; what is proved here is that the simulation applies
    /// that one answer to every cell of the run, whatever order the cells arrive in, and that the
    /// rock-like rule both halves ask agrees with the terrain table.</para>
    /// </summary>
    public class DigOrMineTests
    {
        // ------------------------------------------------------------------ the one rule

        /// <summary>
        /// <see cref="TerrainHandle.IsSoftGround"/> is a list, because the contracts assembly does
        /// not know which terrain is solid. This holds it to the table: soft is exactly solid and
        /// not rock-like. A stone added to the table without a decision in
        /// <see cref="TerrainHandle.IsRockLike"/> fails here rather than reading "Dig".
        /// </summary>
        [Test]
        public void SoftGroundIsTheTablesSolidTerrainThatIsNotRockLike()
        {
            Assert.That(TerrainHandle.Count, Is.EqualTo(NaturalContent.TerrainCount),
                "the contracts' terrain list and the simulation's must be one table");

            for (int t = 0; t < TerrainHandle.Count; t++)
            {
                bool solid = NaturalContent.IsSolid((ushort)t);
                Assert.That(TerrainHandle.IsSoftGround(t), Is.EqualTo(solid && !TerrainHandle.IsRockLike(t)),
                    $"terrain {t} ({NaturalContent.TerrainAt((ushort)t).defName})");
            }
        }

        [Test]
        public void RockOreBedrockAndRubbleAreRockLikeAndTheSoilsAreNot()
        {
            foreach (ushort t in new[]
                     {
                         NaturalContent.TerrainRock, NaturalContent.TerrainBedrock,
                         NaturalContent.TerrainIronOre, NaturalContent.TerrainCoalSeam, CoreContent.TerrainRubble,
                     })
            {
                Assert.That(TerrainHandle.IsRockLike(t), Is.True, NaturalContent.TerrainAt(t).defName);
                Assert.That(TerrainHandle.IsSoftGround(t), Is.False, NaturalContent.TerrainAt(t).defName);
            }

            foreach (ushort t in SoftSoils)
            {
                Assert.That(TerrainHandle.IsRockLike(t), Is.False, NaturalContent.TerrainAt(t).defName);
                Assert.That(TerrainHandle.IsSoftGround(t), Is.True, NaturalContent.TerrainAt(t).defName);
            }

            Assert.That(TerrainHandle.IsSoftGround(NaturalContent.TerrainAir), Is.False, "air is not ground");
            Assert.That(TerrainHandle.IsSoftGround(NaturalContent.TerrainShallowWater), Is.False, "nor is water");
        }

        static readonly ushort[] SoftSoils =
        {
            NaturalContent.TerrainGrass, NaturalContent.TerrainBareEarth, NaturalContent.TerrainPackedGravel,
            NaturalContent.TerrainSand, NaturalContent.TerrainSubsoil,
        };

        // ------------------------------------------------------------------ still diggable

        /// <summary>A strip of eight cells on layer 0: rock at x 0–3, grass at x 4–7, air above.</summary>
        static (CellGrid grid, DesignationGrid designations) Strip(ushort left = NaturalContent.TerrainRock,
                                                                   ushort right = NaturalContent.TerrainGrass)
        {
            var size = new GridSize(8, 1, 2);
            var grid = new CellGrid(size);
            for (int x = 0; x < 8; x++)
            {
                int ground = size.Index(x, 0, 0);
                grid.Terrain[ground] = x < 4 ? left : right;
                grid.Flags[ground] |= CellFlags.SolidTerrain;
            }

            return (grid, new DesignationGrid(grid, new List<PlacedEdifice>()));
        }

        [Test]
        public void EverySoftSoilStaysDiggableAtItsOwnPrice()
        {
            foreach (ushort soil in SoftSoils)
            {
                var (grid, d) = Strip(soil, soil);
                Assert.That(d.Designate(new CellRef(1, 0, 0), DesignationKind.Mine), Is.EqualTo(IntentRejection.None),
                    NaturalContent.TerrainAt(soil).defName + " must stay diggable: it is how a shaft is sunk on flat meadow");
                Assert.That(d.WorkFor(grid.Size.Index(1, 0, 0)), Is.EqualTo(NaturalContent.TerrainAt(soil).workToClear),
                    "the work is the terrain's own, unchanged");
            }
        }

        // ------------------------------------------------------------------ the drag

        /// <summary>The whole strip as one run, submitted as intents the way the presenter does.</summary>
        static void SubmitRun(DesignationGrid d, int run)
        {
            for (int x = 0; x < 8; x++)
                d.HandleDesignate(new Intent(IntentKind.Designate, new CellRef(x, 0, 0), (int)DesignationKind.Mine, run));
        }

        [Test]
        public void ADragFromRockAcrossGrassMarksNoGrass()
        {
            var (grid, d) = Strip();
            SubmitRun(d, DesignateRun.RockOnly);

            for (int x = 0; x < 8; x++)
            {
                DesignationKind got = d.At(grid.Size.Index(x, 0, 0));
                if (x < 4) Assert.That(got, Is.EqualTo(DesignationKind.Mine), $"rock at x {x}");
                else Assert.That(got, Is.EqualTo(DesignationKind.None), $"grass at x {x} was marked by a drag begun on rock");
            }
        }

        [Test]
        public void ADragFromGrassAcrossRockMarksBoth()
        {
            var (grid, d) = Strip();
            SubmitRun(d, DesignateRun.Everything);

            for (int x = 0; x < 8; x++)
                Assert.That(d.At(grid.Size.Index(x, 0, 0)), Is.EqualTo(DesignationKind.Mine), $"x {x}");
        }

        /// <summary>
        /// **The decision cannot change mid-drag** (P4). The committed cells arrive in grid order,
        /// so a drag begun on rock at the right and dragged left puts its grass first: the first
        /// cell the simulation sees is soft. Deciding per cell, or from the first cell, would mark
        /// that grass. The run's answer rides on every intent and the grass is refused all the way
        /// across, with the rock beyond it still marked.
        /// </summary>
        [Test]
        public void TheRunsAnswerHoldsWhateverOrderTheCellsArriveIn()
        {
            var (grid, d) = Strip(NaturalContent.TerrainGrass, NaturalContent.TerrainRock);

            // Grass at x 0–3 first, rock at x 4–7 last, one run begun on the rock.
            SubmitRun(d, DesignateRun.RockOnly);

            for (int x = 0; x < 8; x++)
            {
                DesignationKind got = d.At(grid.Size.Index(x, 0, 0));
                Assert.That(got, Is.EqualTo(x < 4 ? DesignationKind.None : DesignationKind.Mine), $"x {x}");
            }
        }

        [Test]
        public void AClickMarksTheClickedCellWhateverItIs()
        {
            var (grid, d) = Strip();
            Assert.That(d.Designate(new CellRef(6, 0, 0), DesignationKind.Mine), Is.EqualTo(IntentRejection.None),
                "a click on grass is a run of one begun on grass");
            Assert.That(d.Designate(new CellRef(1, 0, 0), DesignationKind.Mine, rockOnly: true), Is.EqualTo(IntentRejection.None),
                "a click on rock is a run of one begun on rock");
            Assert.That(d.At(grid.Size.Index(6, 0, 0)), Is.EqualTo(DesignationKind.Mine));
            Assert.That(d.At(grid.Size.Index(1, 0, 0)), Is.EqualTo(DesignationKind.Mine));
        }

        [Test]
        public void RockOnlyTouchesTheMineOrderAlone()
        {
            var size = new GridSize(4, 1, 3);
            var grid = new CellGrid(size);
            for (int x = 0; x < 4; x++)
            {
                grid.Terrain[size.Index(x, 0, 0)] = NaturalContent.TerrainGrass;
                grid.Flags[size.Index(x, 0, 0)] |= CellFlags.SolidTerrain;
            }

            var edifices = new List<PlacedEdifice>();
            int tree = size.Index(2, 0, 1);
            edifices.Add(new PlacedEdifice { CellIndex = tree, Def = NaturalContent.EdificeTreeBirch, Stuff = NaturalContent.StuffWood });
            grid.Edifice[tree] = 0;
            var d = new DesignationGrid(grid, edifices);

            Assert.That(d.HandleDesignate(new Intent(IntentKind.Designate, new CellRef(2, 0, 1),
                    (int)DesignationKind.Fell, DesignateRun.RockOnly)), Is.EqualTo(IntentRejection.None),
                "the flag is the Mine drag's; a fell order ignores it");
        }

        // ------------------------------------------------------------------ the activity line

        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Colony()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: true);
        }

        /// <summary>The nearest cell of this terrain a colonist could dig, or -1.</summary>
        static int Nearest(ColonyWorld colony, ushort terrain)
        {
            CellRef start = colony.Start;
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int best = -1, bestDistance = int.MaxValue;

            for (int y = 0; y < Size.SizeY; y++)
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, y);
                if (colony.Grid.Terrain[index] != terrain) continue;
                if (!colony.Designations.CanMine(index)) continue;
                if (!DesignationGrid.CanBeLeftAfterCutting(colony.Grid, index)) continue;
                if (MineWorkGiver.StandToMine(colony.Pawns, pawn, index) < 0) continue;

                int distance = System.Math.Abs(x - start.X) + System.Math.Abs(z - start.Z)
                             + System.Math.Abs(y - start.Y) * 4;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }

            return best;
        }

        /// <summary>
        /// Order one cell dug and watch every published frame until it is gone. Returns how many
        /// frames a colonist was on that cell's Mine job, and how many of them said "digging".
        /// </summary>
        static (int onTheJob, int saidDigging) Watch(ColonyWorld colony, int cell)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(cell), (int)DesignationKind.Mine));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty);

            int onTheJob = 0, saidDigging = 0;
            for (int tick = 0; tick < 12_000; tick++)
            {
                colony.World.Tick();
                WorldSnapshot frame = colony.World.Views.Current;
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    if (pawn.CurrentJob == null || pawn.CurrentJob.DefIndex != JobIndex.Mine) continue;
                    if (pawn.CurrentJob.DestCell != cell) continue;
                    onTheJob++;
                    if (frame.TryGetPawnAspect(pawn.Id, MineJobDriver.Digging, out int v) && v == 1) saidDigging++;
                }

                if (!colony.Grid.IsSolidTerrain(cell) && onTheJob > 0 && NobodyOn(colony, cell)) break;
            }

            return (onTheJob, saidDigging);
        }

        static bool NobodyOn(ColonyWorld colony, int cell)
        {
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                if (pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.Mine && pawn.CurrentJob.DestCell == cell)
                    return false;
            return true;
        }

        /// <summary>
        /// A colonist digging grass says so on every frame of the job — the walk, the cut and the
        /// settle after it, when the cell is already air and could not say what it was.
        /// </summary>
        [Test]
        public void AColonistDiggingGrassPublishesDiggingForTheWholeJob()
        {
            ColonyWorld colony = Colony();
            int grass = Nearest(colony, NaturalContent.TerrainGrass);
            Assume.That(grass, Is.GreaterThanOrEqualTo(0), "the board has diggable grass");

            var (onTheJob, saidDigging) = Watch(colony, grass);
            Assert.That(colony.Grid.IsSolidTerrain(grass), Is.False, "the grass was never dug");
            Assert.That(onTheJob, Is.GreaterThan(0));
            Assert.That(saidDigging, Is.EqualTo(onTheJob), "a frame of a grass dig read as mining");
        }

        /// <summary>The twin of the interface's <c>DigOrMine.DiggingAspect</c>: the two sides agree by string alone.</summary>
        [Test]
        public void TheAspectNameIsTheOneTheInterfaceReads()
        {
            Assert.That(MineJobDriver.DiggingName, Is.EqualTo("odyssey.pawn.digging"));
        }

        [Test]
        public void AColonistMiningRockPublishesNothing()
        {
            ColonyWorld colony = Colony();
            int rock = Nearest(colony, NaturalContent.TerrainRock);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0), "the board has reachable rock");

            var (onTheJob, saidDigging) = Watch(colony, rock);
            Assert.That(onTheJob, Is.GreaterThan(0));
            Assert.That(saidDigging, Is.Zero, "a rock face read as digging");
        }
    }
}
