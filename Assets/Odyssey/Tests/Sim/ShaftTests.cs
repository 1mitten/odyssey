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

        /// <summary>
        /// A one-wide shaft cannot be deepened past one block, because nothing could get out of it
        /// (owner, 2026-09-16: a colonist jumps up one block, and a ladder is wanted for more).
        ///
        /// <para>This test used to assert the opposite — that such a shaft <i>is</i> deepened, from
        /// the only stance available, its own floor. That was true and it was the mechanism behind
        /// three playtest reports at once: the colonist that cut the second cell was then at the
        /// bottom of a two-block hole, and the only way out was a climb that ended in mid-air.
        /// Refusing the cut is what makes a quarry come out as benches.</para>
        /// </summary>
        [Test]
        public void AOneWideShaftIsNotDeepenedPastOneBlock()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;

            int surface = Size.Index(start.X, start.Z, start.Y);
            int bottom = surface - Size.LayerStride;
            Dig(colony, bottom);

            Assert.That(
                Odyssey.Sim.Designations.DesignationGrid.CanBeLeftAfterCutting(
                    colony.Grid, bottom - Size.LayerStride),
                Is.False,
                "a one-wide shaft was deepened to two blocks, stranding whoever dug it");
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
