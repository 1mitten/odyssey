#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Dig and Mine (design 62 §4), the interface's half: the word the order is named by, and the
    /// drag's one decision. The simulation's half — that a <see cref="DesignateRun.RockOnly"/> run
    /// refuses every soft cell of it — is <c>Odyssey.Tests.Sim.DigOrMineTests</c>.
    /// </summary>
    public class DigOrMineTests
    {
        static readonly int[] Soft =
        {
            TerrainHandle.Grass, TerrainHandle.BareEarth, TerrainHandle.PackedGravel,
            TerrainHandle.Sand, TerrainHandle.Subsoil,
        };

        static readonly int[] RockLike =
        {
            TerrainHandle.Rock, TerrainHandle.IronOre, TerrainHandle.CoalSeam,
            TerrainHandle.Bedrock, TerrainHandle.Rubble,
        };

        // ------------------------------------------------------------------ the words

        [Test]
        public void EverySoftTerrainReadsDigAndEveryRockReadsMine()
        {
            foreach (int t in Soft)
            {
                Assert.That(Registry.Label(DigOrMine.OrderKey(t)), Is.EqualTo("Dig"), TerrainLabels.Label(t));
                Assert.That(Registry.Label(DigOrMine.ActivityKey(t)), Is.EqualTo("Digging"), TerrainLabels.Label(t));
            }

            foreach (int t in RockLike)
            {
                Assert.That(Registry.Label(DigOrMine.OrderKey(t)), Is.EqualTo("Mine"), TerrainLabels.Label(t));
                Assert.That(Registry.Label(DigOrMine.ActivityKey(t)), Is.EqualTo("Mining"), TerrainLabels.Label(t));
            }

            Assert.That(DigOrMine.OrderKey(-1), Is.EqualTo(DigOrMine.MineKey),
                "a cell the pointer cannot name reads as the tool's own name");
            Assert.That(DigOrMine.OrderKey(TerrainHandle.Air), Is.EqualTo(DigOrMine.MineKey));
            Assert.That(DigOrMine.OrderKey(TerrainHandle.ShallowWater), Is.EqualTo(DigOrMine.MineKey));
        }

        [Test]
        public void TheFourKeysAreRegisteredAndTheAspectIsTheSimulationsSpelling()
        {
            foreach (string key in new[] { DigOrMine.MineKey, DigOrMine.DigKey, DigOrMine.MiningKey, DigOrMine.DiggingKey })
                Assert.That(Registry.Labels, Does.ContainKey(key), key);
            Assert.That(DigOrMine.MineKey, Is.EqualTo(PaletteTools.Mine), "Mine is the palette's own chip");
            // The twin of this line is MineJobDriver.DiggingName, asserted in the simulation's tests.
            Assert.That(DigOrMine.DiggingAspect, Is.EqualTo("odyssey.pawn.digging"));
        }

        // ------------------------------------------------------------------ the drag

        /// <summary>A strip on layer 0: rock at x 0–3, grass at x 4–7.</summary>
        static int Strip(CellRef cell) => cell.X < 4 ? TerrainHandle.Rock : TerrainHandle.Grass;

        static DesignateDirector Mining(System.Func<CellRef, int> terrain) =>
            new DesignateDirector { Tool = DesignateTool.Mine, TerrainAt = terrain };

        [Test]
        public void ADragBegunOnRockIsRockOnly()
        {
            DesignateDirector d = Mining(Strip);
            d.Begin(new CellRef(1, 0, 0));
            d.DragTo(new CellRef(7, 0, 0));
            Assert.That(d.Run, Is.EqualTo(DesignateRun.RockOnly));

            var cells = d.Commit();
            Assert.That(cells.Count, Is.EqualTo(7), "the box itself is not filtered: the simulation refuses the grass");
            Assert.That(d.LastRun, Is.EqualTo(DesignateRun.RockOnly), "and every intent of it carries the run");
        }

        [Test]
        public void ADragBegunOnGrassMarksEverything()
        {
            DesignateDirector d = Mining(Strip);
            d.Begin(new CellRef(6, 0, 0));
            d.DragTo(new CellRef(0, 0, 0));
            Assert.That(d.Run, Is.EqualTo(DesignateRun.Everything));
            d.Commit();
            Assert.That(d.LastRun, Is.EqualTo(DesignateRun.Everything));
        }

        /// <summary>
        /// **Decided once, from the start cell** (P4). The head wandering on to grass, back on to
        /// rock and on to grass again never re-asks; and even if the ground under the start cell
        /// changed while the drag ran, the run is what it was when the drag began.
        /// </summary>
        [Test]
        public void TheDecisionCannotChangeMidDrag()
        {
            int anchorTerrain = TerrainHandle.Rock;
            DesignateDirector d = Mining(c => c.X == 1 ? anchorTerrain : Strip(c));

            d.Begin(new CellRef(1, 0, 0));
            foreach (int x in new[] { 5, 2, 7, 0, 6 })
            {
                d.DragTo(new CellRef(x, 0, 0));
                Assert.That(d.Run, Is.EqualTo(DesignateRun.RockOnly), $"head at x {x}");
            }

            anchorTerrain = TerrainHandle.Grass;
            d.DragTo(new CellRef(7, 0, 0));
            Assert.That(d.Run, Is.EqualTo(DesignateRun.RockOnly), "the start cell is asked once, at Begin");
            d.Commit();
            Assert.That(d.LastRun, Is.EqualTo(DesignateRun.RockOnly));

            // And a fresh drag asks afresh.
            d.Begin(new CellRef(1, 0, 0));
            Assert.That(d.Run, Is.EqualTo(DesignateRun.Everything));
        }

        [Test]
        public void AClickOnEitherGroundMarksTheCellClicked()
        {
            DesignateDirector d = Mining(Strip);

            var rock = d.Click(new CellRef(2, 0, 0));
            Assert.That(rock.Single(), Is.EqualTo(new CellRef(2, 0, 0)));
            Assert.That(d.LastRun, Is.EqualTo(DesignateRun.RockOnly), "a rock cell is rock-like, so it is marked");

            var grass = d.Click(new CellRef(5, 0, 0));
            Assert.That(grass.Single(), Is.EqualTo(new CellRef(5, 0, 0)));
            Assert.That(d.LastRun, Is.EqualTo(DesignateRun.Everything));
        }

        [Test]
        public void OnlyTheMineToolEverCarriesARun()
        {
            foreach (DesignateTool tool in new[] { DesignateTool.Fell, DesignateTool.Cancel, DesignateTool.Deconstruct, DesignateTool.Harvest })
            {
                var d = new DesignateDirector { Tool = tool, TerrainAt = Strip };
                d.Begin(new CellRef(1, 0, 0));
                Assert.That(d.Run, Is.EqualTo(DesignateRun.Everything), tool.ToString());
            }
        }

        [Test]
        public void WithNoWorldToAskTheToolDoesWhatItAlwaysDid()
        {
            var d = new DesignateDirector { Tool = DesignateTool.Mine };
            d.Begin(new CellRef(1, 0, 0));
            Assert.That(d.Run, Is.EqualTo(DesignateRun.Everything));
            Assert.That(d.PointerTerrain, Is.EqualTo(-1));
        }

        // ------------------------------------------------------------------ the banner

        [Test]
        public void TheBannerReadsDigOverGrassAndMineOverRock()
        {
            DesignateDirector d = Mining(Strip);
            var palette = new BuildPaletteModel(d);

            Assert.That(palette.ArmedWordKey, Is.EqualTo(DigOrMine.MineKey), "nothing under the pointer");

            d.HoverAt(new CellRef(6, 0, 0));
            Assert.That(palette.ArmedWordKey, Is.EqualTo(DigOrMine.DigKey));
            d.HoverAt(new CellRef(2, 0, 0));
            Assert.That(palette.ArmedWordKey, Is.EqualTo(DigOrMine.MineKey));

            Assert.That(palette.ArmedOrder, Is.EqualTo(PaletteTools.Mine),
                "one order under two words: the hue and the lit button stay Mine's");
        }

        [Test]
        public void MidDragTheBannerReadsTheStartCellsWord()
        {
            DesignateDirector d = Mining(Strip);
            var palette = new BuildPaletteModel(d);

            d.Begin(new CellRef(6, 0, 0));
            d.DragTo(new CellRef(1, 0, 0));
            Assert.That(palette.ArmedWordKey, Is.EqualTo(DigOrMine.DigKey),
                "a drag begun on grass marks everything and says Dig, wherever the head is");

            d.Abandon();
            d.Begin(new CellRef(1, 0, 0));
            d.DragTo(new CellRef(6, 0, 0));
            Assert.That(palette.ArmedWordKey, Is.EqualTo(DigOrMine.MineKey));
        }

        [Test]
        public void EveryOtherOrderIsNamedAsItWas()
        {
            var d = new DesignateDirector { Tool = DesignateTool.Fell, TerrainAt = Strip };
            var palette = new BuildPaletteModel(d);
            d.HoverAt(new CellRef(6, 0, 0));
            Assert.That(palette.ArmedWordKey, Is.EqualTo(palette.ArmedOrder));
        }

        // ------------------------------------------------------------------ the activity line

        static readonly PawnId Ada = new PawnId(1);

        static WorldSnapshot Frame(bool digging)
        {
            var frame = new WorldSnapshot();
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 0, 4), 800, 800, 700, JobHandle.Mine));
            if (digging) frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(DigOrMine.DiggingAspect), 1));
            return frame;
        }

        [Test]
        public void AColonistDiggingGrassReadsDiggingAndAtRockReadsMining()
        {
            var model = new InspectModel();
            model.SetColonist(Ada);

            model.Refresh(Frame(digging: true));
            Assert.That(model.Job, Is.EqualTo(Registry.Label(DigOrMine.DiggingKey)));

            model.Refresh(Frame(digging: false));
            Assert.That(model.Job, Is.EqualTo(Registry.Label(DigOrMine.MiningKey)),
                "the line follows the flag off again: the cache knows the two apart");
        }

        [Test]
        public void TheFlagMeansNothingOnAnyOtherJob()
        {
            var frame = new WorldSnapshot();
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 0, 4), 800, 800, 700, JobHandle.Fell));
            frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(DigOrMine.DiggingAspect), 1));

            var model = new InspectModel();
            model.SetColonist(Ada);
            model.Refresh(frame);
            Assert.That(model.Job, Is.EqualTo(JobLabels.Label(JobHandle.Fell)));
        }
    }
}
