#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Cutting a shaft, and getting back out of it.
    ///
    /// <para>This is the one part of mining that could not be borrowed from felling. Vertical
    /// movement in this codebase is only ever a declared connector — a fall edge is one-way and
    /// excluded from districts — so without a rule a colonist that dug downward would be sealed
    /// into a district of its own: unable to climb out and, because every work-giver scan gates
    /// on the district comparison, invisible to every job on the surface. The colony would
    /// silently be one colonist short and nothing would report it.</para>
    /// </summary>
    public class ShaftTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        /// <summary>Mine one cell to completion by hand, without waiting for a colonist to walk.</summary>
        static void Dig(ColonyWorld colony, int cell)
        {
            ushort terrain = colony.Grid.Terrain[cell];
            MineJobDriver.MineCell(colony.Pawns, cell, terrain);
            colony.World.Tick();
        }

        [Test]
        public void ADugShaftIsClimbable()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);

            // Three cells straight down from the cell the colony stands in.
            int first = surface - Size.LayerStride;
            Dig(colony, first);
            Dig(colony, first - Size.LayerStride);
            Dig(colony, first - 2 * Size.LayerStride);

            int bottom = first - 2 * Size.LayerStride;
            Assert.That(colony.Grid.IsWalkable(bottom), Is.True, "the bottom of the shaft cannot be stood in");

            // Reachable both ways. Down is the easy direction and proves little; up is the one a
            // fall edge would fail, because falls are one-way and are left out of the districts.
            Assert.That(colony.Pawns.Nav.Reachable(surface, bottom, TraverseMode.Colonist), Is.True,
                "the bottom of the shaft cannot be walked to");
            Assert.That(colony.Pawns.Nav.Reachable(bottom, surface, TraverseMode.Colonist), Is.True,
                "a colonist at the bottom of the shaft cannot get out");
        }

        [Test]
        public void ADiggerComesBackUpAndGoesOnWorking()
        {
            // The whole point, stated as behaviour rather than as graph structure: a colonist that
            // cuts its way down is still a working member of the colony afterwards.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);

            for (int depth = 1; depth <= 3; depth++)
            {
                int cell = surface - depth * Size.LayerStride;
                if (!colony.Designations.CanMine(cell)) continue;
                colony.Designations.Designate(Size.FromIndex(cell), DesignationKind.Mine);
                colony.World.Tick(4_000);
            }

            colony.World.Tick(4_000);

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                Assert.That(colony.Grid.IsWalkable(pawn.Cell), Is.True,
                    $"a colonist is standing in {Size.FromIndex(pawn.Cell)}, which cannot be stood in");
                Assert.That(colony.Pawns.Nav.Reachable(pawn.Cell, surface, TraverseMode.Colonist), Is.True,
                    $"the colonist at {Size.FromIndex(pawn.Cell)} cannot get back to the surface");
            }
        }

        [Test]
        public void ASecondDigDoesNotStackASecondLadder()
        {
            // EnsureLadder is asked on both sides of every dig, so a shaft cut downward asks for
            // the same pair twice. Flagging one cell's footprint twice would leave a connector
            // nothing can take away again.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int upper = Size.Index(start.X, start.Z, start.Y);
            int lower = upper - Size.LayerStride;

            Dig(colony, lower);
            int again = colony.Pawns.Nav.EnsureLadder(lower, upper);

            Assert.That(again, Is.EqualTo(-1), "a second ladder was stacked on the first");
        }

        [Test]
        public void ALadderJoinsOnlyCellsOneLayerApart()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int cell = Size.Index(start.X, start.Z, start.Y);

            Assert.That(colony.Pawns.Nav.EnsureLadder(cell, cell + 2 * Size.LayerStride), Is.EqualTo(-1),
                "a ladder skipped a layer");
            Assert.That(colony.Pawns.Nav.EnsureLadder(cell, cell + 1), Is.EqualTo(-1),
                "a ladder was laid sideways");
            Assert.That(colony.Pawns.Nav.EnsureLadder(-1, cell), Is.EqualTo(-1));
        }

        [Test]
        public void BreakingIntoAChamberFromBelowIsAlsoLaddered()
        {
            // The upward case: a dig that opens into something already hollow. Asking "is my
            // vertical neighbour open" covers both directions without caring which happened.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int top = Size.Index(start.X, start.Z, start.Y - 1);
            int middle = top - Size.LayerStride;
            int bottom = middle - Size.LayerStride;

            Dig(colony, top);
            Dig(colony, bottom);     // an isolated pocket, not yet joined to the shaft above
            Dig(colony, middle);     // breaks through: laddered to the pocket below and the shaft above

            Assert.That(colony.Pawns.Nav.Reachable(bottom, top, TraverseMode.Colonist), Is.True,
                "the pocket below never joined the shaft above it");
        }
    }
}
