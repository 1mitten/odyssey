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

        /// <summary>
        /// A walkable cell on the colony's own layer next door to the shaft.
        ///
        /// <para>Every test here used to use the cell directly over the shaft as its idea of "the
        /// surface", and that is exactly the cell a dig takes the floor out from under. It worked
        /// only while a climb ended in the air above the hole and a colonist could stand there. A
        /// climb now ends on the ground <em>beside</em> the hole, which is where a person actually
        /// ends up, so the surface reference has to be a cell that is still ground.</para>
        /// </summary>
        static int GroundBeside(ColonyWorld colony, CellRef at)
        {
            foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int x = at.X + d.Item1, z = at.Z + d.Item2;
                if (!Size.Contains(x, z, at.Y)) continue;
                int cell = Size.Index(x, z, at.Y);
                if (colony.Grid.IsWalkable(cell)) return cell;
            }
            return -1;
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

            // From the ground BESIDE the hole, not from over it: the cell over it is the one whose
            // floor was just dug away. See GroundBeside.
            int ground = GroundBeside(colony, start);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0), "no ground beside the shaft on this board");

            // Reachable both ways. Down is the easy direction and proves little; up is the one a
            // fall edge would fail, because falls are one-way and are left out of the districts.
            Assert.That(colony.Pawns.Nav.Reachable(ground, bottom, TraverseMode.Colonist), Is.True,
                "the bottom of the shaft cannot be walked to");
            Assert.That(colony.Pawns.Nav.Reachable(bottom, ground, TraverseMode.Colonist), Is.True,
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

            int ground = GroundBeside(colony, start);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0), "no ground beside the shaft on this board");

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                Assert.That(colony.Grid.IsWalkable(pawn.Cell), Is.True,
                    $"a colonist is standing in {Size.FromIndex(pawn.Cell)}, which cannot be stood in");
                Assert.That(colony.Pawns.Nav.Reachable(pawn.Cell, ground, TraverseMode.Colonist), Is.True,
                    $"the colonist at {Size.FromIndex(pawn.Cell)} cannot get back to the surface");
            }
        }

        [Test]
        public void ASecondDigDoesNotStackASecondClimb()
        {
            // EnsureClimb is asked on both sides of every dig, so a shaft cut downward asks for
            // the same pair twice. Flagging one cell's footprint twice would leave a connector
            // nothing can take away again.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int lower = Size.Index(start.X, start.Z, start.Y) - Size.LayerStride;

            Dig(colony, lower);

            // Asked for the very climb the dig declared: onto the ground beside the hole, which is
            // where a climb lands now. Asking for the vertical pair would be asking for a climb
            // that was never made and would say nothing about stacking.
            int landing = GroundBeside(colony, start);
            Assume.That(landing, Is.GreaterThanOrEqualTo(0), "no ground beside the hole on this board");

            int again = colony.Pawns.Nav.EnsureClimb(lower, landing);
            Assert.That(again, Is.EqualTo(-1), "a second climb was stacked on the first");
        }

        [Test]
        public void AClimbJoinsOnlyCellsOneLayerApart()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int cell = Size.Index(start.X, start.Z, start.Y);

            Assert.That(colony.Pawns.Nav.EnsureClimb(cell, cell + 2 * Size.LayerStride), Is.EqualTo(-1),
                "a climb skipped a layer");
            Assert.That(colony.Pawns.Nav.EnsureClimb(cell, cell + 1), Is.EqualTo(-1),
                "a climb was laid sideways");
            Assert.That(colony.Pawns.Nav.EnsureClimb(-1, cell), Is.EqualTo(-1));
        }

        [Test]
        public void BreakingIntoAChamberFromBelowIsAlsoClimbable()
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

        // ---- where a colonist stands to dig downward ---------------------------------------

        [Test]
        public void ACellUnderFlatGroundIsCutFromTheRimAndNotFromOnTopOfIt()
        {
            // The owner's decision, and the reason the pick now aims downward. A cell just under
            // the surface has its top face exposed, so a colonist standing on the ground beside it
            // has the stone level with its boots one cell across — which is how a person digs, and
            // costs it nothing when the floor goes. Standing on top costs it its own floor.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            Pawn pawn = colony.Pawns.Pawns.All[0];

            int surface = Size.Index(start.X, start.Z, start.Y);
            int target = surface - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(target), Is.True, "the colony stands on solid ground");

            int stand = MineWorkGiver.StandToMine(colony.Pawns, pawn, target);
            Assert.That(stand, Is.GreaterThanOrEqualTo(0), "no stance at all for the first cut of a shaft");
            Assert.That(stand, Is.Not.EqualTo(surface),
                "the colonist stood on the cell it was about to cut out from under itself");

            CellRef at = Size.FromIndex(stand);
            Assert.That(at.Y, Is.EqualTo(start.Y), "the rim is the layer the colonist is already walking on");
            Assert.That(System.Math.Abs(at.X - start.X) + System.Math.Abs(at.Z - start.Z),
                Is.GreaterThan(0), "the rim cell is beside the hole, not over it");
            Assert.That(System.Math.Abs(at.X - start.X), Is.LessThanOrEqualTo(1));
            Assert.That(System.Math.Abs(at.Z - start.Z), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void AOneWideShaftIsDeepenedFromDirectlyAboveBecauseThereIsNowhereElse()
        {
            // The stance that could not be designed away, and the reason the step down into the
            // hole still exists. Sunk one cell across, a shaft has solid rock on all eight sides of
            // its bottom and on all eight sides of the cell below that: the only floor within
            // reach of the next cell down is the one standing on it.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            Pawn pawn = colony.Pawns.Pawns.All[0];

            int surface = Size.Index(start.X, start.Z, start.Y);
            int bottom = surface - Size.LayerStride;
            Dig(colony, bottom);

            int target = bottom - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(target), Is.True,
                "the board has no second layer of rock under the landing site");

            Assert.That(MineWorkGiver.StandToMine(colony.Pawns, pawn, target), Is.EqualTo(bottom),
                "a shaft was deepened from somewhere other than its own floor");
        }

        [Test]
        public void AStanceOnTheSameLayerStillBeatsOneOnTheRim()
        {
            // The order is beside, then rim, then on top, and the first of those must not have
            // been lost: cutting an adit into a face is a level swing from level ground, and
            // demoting it would have miners climbing onto a cliff to work its face from above.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            Pawn pawn = colony.Pawns.Pawns.All[0];

            int surface = Size.Index(start.X, start.Z, start.Y);
            int mouth = surface - Size.LayerStride;
            Dig(colony, mouth);
            Assume.That(colony.Grid.IsWalkable(mouth), Is.True, "the cut cell cannot be stood in");

            int target = mouth + 1;   // its neighbour on the same layer: the face of the little pit
            Assume.That(colony.Designations.CanMine(target), Is.True);

            int stand = MineWorkGiver.StandToMine(colony.Pawns, pawn, target);
            Assert.That(Size.FromIndex(stand).Y, Is.EqualTo(Size.FromIndex(target).Y),
                "a level stance was available and a higher one was taken anyway");
        }

        [Test]
        public void ThereIsNoStanceOnTheRimOfACellWithRockOnTopOfIt()
        {
            // The rim is only a stance where the rock has a top face to strike. A buried cell does
            // not: a colonist stood over it would be swinging at the cell above instead, which is
            // the floor it is standing on. Without this test the rim rule quietly handed out
            // stances at rock nobody could reach, and the only thing that noticed was a hauling
            // test two files away wondering where its stone had gone.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            Pawn pawn = colony.Pawns.Pawns.All[0];

            // Two layers down under untouched ground: solid rock above it, solid rock all round it.
            int buried = Size.Index(start.X, start.Z, start.Y - 2);
            Assume.That(colony.Designations.CanMine(buried), Is.True, "not rock on this board");
            Assume.That(colony.Grid.IsSolidTerrain(buried + Size.LayerStride), Is.True,
                "the cell is not buried, so this proved nothing");

            Assert.That(MineWorkGiver.StandToMine(colony.Pawns, pawn, buried), Is.LessThan(0),
                "a stance was offered on rock with a metre of rock on top of it");
        }
    }
}
