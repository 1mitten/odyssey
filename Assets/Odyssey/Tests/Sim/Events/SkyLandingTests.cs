#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// The one rule that decides what "through open sky" means (design 23 §6): the topmost cell
    /// that is not open air, if it can be stood in, else nothing.
    /// </summary>
    public class SkyLandingTests
    {
        static readonly GridSize Size = new GridSize(4, 4, 4);

        static CellGrid Flat()
        {
            var grid = new CellGrid(Size);
            for (int x = 0; x < 4; x++)
            for (int z = 0; z < 4; z++)
                grid.Floor[Size.Index(x, z, 0)] = 1;
            return grid;
        }

        [Test]
        public void OpenGroundLandsOnTheGround()
        {
            Assert.That(Flat().SkyLanding(1, 1), Is.EqualTo(Size.Index(1, 1, 0)));
        }

        [Test]
        public void ARoofLandsOnTheRoofAndNeverInTheRoomUnderIt()
        {
            CellGrid grid = Flat();
            grid.Floor[Size.Index(1, 1, 2)] = 1;

            Assert.That(grid.SkyLanding(1, 1), Is.EqualTo(Size.Index(1, 1, 2)));
        }

        [Test]
        public void AWallIsMetFirstAndRefusesTheColumn()
        {
            CellGrid grid = Flat();
            int wall = Size.Index(2, 2, 0);
            grid.Edifice[wall] = 0;
            grid.Flags[wall] |= CellFlags.BlockingEdifice;

            Assert.That(grid.SkyLanding(2, 2), Is.EqualTo(-1),
                "the search must not slip past the wall to the floor beside its foot");
        }

        [Test]
        public void DeepWaterRefusesTheColumnRatherThanLandingOnTheBed()
        {
            CellGrid grid = Flat();
            int water = Size.Index(0, 0, 0);
            grid.Flags[water] |= CellFlags.ImpassableTerrain;

            Assert.That(grid.SkyLanding(0, 0), Is.EqualTo(-1));
        }

        [Test]
        public void BareRockAtTheTopRefusesTheColumn()
        {
            CellGrid grid = Flat();
            int rock = Size.Index(3, 3, 3);
            grid.Flags[rock] |= CellFlags.SolidTerrain;

            Assert.That(grid.SkyLanding(3, 3), Is.EqualTo(-1),
                "solid terrain with nothing standable above it is the top of a cliff nothing can rest on");
        }

        [Test]
        public void AColumnOfNothingAndAColumnOffTheBoardAreBothNothing()
        {
            var empty = new CellGrid(Size);
            Assert.That(empty.SkyLanding(1, 1), Is.EqualTo(-1));
            Assert.That(Flat().SkyLanding(4, 0), Is.EqualTo(-1));
            Assert.That(Flat().SkyLanding(0, -1), Is.EqualTo(-1));
        }

        /// <summary>
        /// A tree is an edifice that blocks nothing, so its cell is met first and can be stood
        /// in — a drop lands under the tree, exactly where felled wood already lands.
        /// </summary>
        [Test]
        public void ATreeIsMetFirstAndTheLoadLandsUnderIt()
        {
            CellGrid grid = Flat();
            int tree = Size.Index(1, 2, 0);
            grid.Edifice[tree] = 7;

            Assert.That(grid.SkyLanding(1, 2), Is.EqualTo(tree));
        }
    }
}
