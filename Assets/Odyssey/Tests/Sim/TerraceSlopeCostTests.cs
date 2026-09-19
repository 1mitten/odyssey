#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The cell at the foot of a terrace step is a **slope**, and costs what a slope costs.
    ///
    /// <para><b>Why the simulation cares about something it cannot see.</b> Presentation fills that
    /// cell with a bank — a ramp from the lower floor to the rim above — so a colonist crossing it
    /// is drawn climbing 1.5 m before its centre and another 1.5 m after. The walk *into* it was
    /// priced as flat grass, so the first half of every terrace climb was drawn at 1.9 m/s against
    /// walking's 1.5, and the slowing down began only half way up. Owner, 2026-09-18: <i>"the
    /// slowness needs to start happening much earlier when entering the beginning of the tile …
    /// you slow down and then you seem to still go slow on the flat so it's out of sync."</i></para>
    ///
    /// <para><b>These tests exist because the goldens did not move.</b> A cost change that shifts no
    /// hash is either inert or lucky, and the three golden windows are a flat meadow, a start
    /// clearing chosen for being flat, and a city of pavement — none of which has a bank in it. So
    /// the price is asserted directly here rather than inferred from a colony that never met one.
    /// </para>
    /// </summary>
    public class TerraceSlopeCostTests
    {
        static readonly GridSize Size = new GridSize(12, 12, 6);

        /// <summary>
        /// Earth up to layer 1, with the half of the board at <c>x &lt; 6</c> one layer higher — so
        /// the cells at <c>x = 6</c>, layer 2, are the feet of a terrace step and everything beyond
        /// them is ordinary flat ground.
        /// </summary>
        static CellGrid Terrace()
        {
            var grid = new CellGrid(Size);
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int top = x < 6 ? 2 : 1;
                for (int y = 0; y <= top; y++) Solid(grid, Size.Index(x, z, y), NaturalContent.TerrainSubsoil);
                Solid(grid, Size.Index(x, z, top), NaturalContent.TerrainGrass);
            }
            return grid;
        }

        static void Solid(CellGrid grid, int index, ushort terrain)
        {
            grid.Terrain[index] = terrain;
            grid.Flags[index] |= CellFlags.SolidTerrain;
        }

        static NavGraph GraphOf(CellGrid grid)
        {
            var nav = new NavGraph(grid);
            nav.Rebuild();
            return nav;
        }

        [Test]
        public void TheFootOfAStepIsASlopeAndTheCellBeyondItIsNot()
        {
            CellGrid grid = Terrace();
            NavGraph nav = GraphOf(grid);

            int foot = Size.Index(6, 5, 2);
            int beyond = Size.Index(8, 5, 2);

            Assert.That(TerraceFoot.IsFoot(grid, foot), Is.True, "the fixture has no terrace foot in it");
            Assert.That(nav.Grid.CostClass[foot], Is.EqualTo(NaturalContent.CostClassSlope),
                "the cell at the foot of the step is not priced as a slope");
            Assert.That(nav.Grid.CostClass[beyond], Is.EqualTo(NaturalContent.CostClassClear),
                "flat ground two cells out is priced as a slope");
        }

        [Test]
        public void WalkingOntoASlopeCostsWhatHoppingOffItDoes()
        {
            // **The point of the number.** A terrace climb is drawn as one ramp and charged as two
            // steps, with the ramp split down the middle between them. If the two differ the figure
            // changes speed half way up a slope that does not change, which is the fault being
            // fixed. So this is not "a slope is dearer" — it is "these two are the same".
            CellGrid grid = Terrace();
            NavGraph nav = GraphOf(grid);

            int foot = Size.Index(6, 5, 2);
            int flat = Size.Index(8, 5, 2);

            Assert.That(nav.Grid.EnterCost(foot, TraverseMode.Colonist),
                Is.EqualTo(NavGraph.HopCost(up: true)),
                "walking on to the ramp and hopping off it are priced differently, so a climb " +
                "changes speed half way up");
            Assert.That(nav.Grid.EnterCost(flat, TraverseMode.Colonist),
                Is.EqualTo(MoveCost.Orthogonal), "flat ground stopped costing what walking costs");
        }

        [Test]
        public void AColonistPrefersTheFlatLineToTheFootOfTheTerrace()
        {
            // The intended consequence, stated so that it is a decision and not a surprise: given
            // the choice, colonists walk the flat line one cell out rather than along the bank.
            CellGrid grid = Terrace();
            NavGraph nav = GraphOf(grid);
            var finder = new PathFinder(nav);

            PathResult result = finder.FindPath(
                Size.Index(6, 1, 2), Size.Index(6, 9, 2), TraverseMode.Colonist);

            Assert.That(result.Status, Is.EqualTo(PathStatus.Success), "no route along the terrace");

            int alongTheBank = 0;
            var cells = finder.PathCells;
            for (int i = 0; i < cells.Length; i++)
                if (Size.FromIndex(cells[i]).X == 6) alongTheBank++;

            Assert.That(alongTheBank, Is.LessThan(result.Length / 2),
                "the route still runs along the foot of the terrace, so the slope costs nothing");
        }

        [Test]
        public void MiningTheStepAwayMakesTheSlopeOrdinaryGroundAgain()
        {
            // The cost now depends on the cells *beside* a cell, which nothing else in the nav grid
            // did. So dirtying has to reach them — see NavGraph.MarkDirty — and this is the case
            // that fails if it does not: take the high ground away and the ramp is not there any
            // more, whatever the cell it stood in still looks like.
            CellGrid grid = Terrace();
            NavGraph nav = GraphOf(grid);

            int foot = Size.Index(6, 5, 2);
            Assert.That(nav.Grid.CostClass[foot], Is.EqualTo(NaturalContent.CostClassSlope));

            // Level the step for a stretch of the terrace, the way a miner would.
            for (int z = 3; z <= 7; z++)
            {
                int step = Size.Index(5, z, 2);
                grid.Terrain[step] = CoreContent.TerrainAir;
                grid.Flags[step] &= ~CellFlags.SolidTerrain;
                CellRef at = Size.FromIndex(step);
                nav.MarkDirty(at.X, at.Z, at.Y);
            }

            nav.Rebuild();

            Assert.That(nav.Grid.CostClass[foot], Is.EqualTo(NaturalContent.CostClassClear),
                "the step was mined away and the cell beside it is still priced as a ramp");
        }
    }
}
