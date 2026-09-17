#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// <b>Can a player build a floor by pointing at something?</b>
    ///
    /// <para>U29's own tests all order their floor at a cell named in C#, which is correct and is
    /// not what the running game sends. Between a pointer and a site there are two rules in two
    /// assemblies — <c>SlicePicker</c> decides which cell a click means, and
    /// <c>ConstructionGrid.Place</c> decides which cell an order lands in — and neither assembly's
    /// tests can see the other. On 2026-09-17 they disagreed: the picker answered a click on a
    /// wall with the <b>wall's own cell</b>, a floor ordered there was <c>NotPermitted</c>, and the
    /// only cell that accepted one could not be pointed at. The tool armed, dragged, previewed and
    /// did nothing.</para>
    ///
    /// <para>This fixture is the seam itself: the cell the picker returns is fed straight into the
    /// order, with nothing typed in between. It belongs here rather than in the Sim tier because
    /// this is the only assembly that can see both halves.</para>
    /// </summary>
    public class FloorToolReachTests
    {
        /// <summary>A ray looking straight down at the middle of a column, from well above it.</summary>
        static Ray DownAt(int x, int z, float height = 60f)
        {
            var target = new Vector3(
                (x + 0.5f) * CellMetrics.SizeXZ, 0f, (z + 0.5f) * CellMetrics.SizeXZ);
            return new Ray(target + new Vector3(0f, height, 0f), Vector3.down);
        }

        /// <summary>
        /// Flat ground at layer 0 and one wall standing on it at layer 1 — the board a player has
        /// after raising their first wall, and the smallest one this question can be asked on.
        /// </summary>
        static RenderTestWorld GroundWithAWallAt(int x, int z)
        {
            var world = new RenderTestWorld(8, 8, 6);
            for (int cz = 0; cz < 8; cz++)
            for (int cx = 0; cx < 8; cx++)
                world.Solid(cx, cz, 0, Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainGrass);

            return world.Edifice(x, z, 1, CoreContent.EdificeWall).Publish();
        }

        /// <summary>
        /// A construction grid over that board, wired to a solver, exactly as
        /// <c>ColonyComposition.AddColony</c> wires the real one.
        /// </summary>
        static ConstructionGrid ConstructionOver(RenderTestWorld world)
        {
            var edifices = new List<PlacedEdifice>();
            var items = new ColonyItems(ContentPack.Pawns());
            return new ConstructionGrid(
                world.Grid, new EdificeSaveSection(edifices), items, new SupportSolver(world.Grid));
        }

        /// <summary>
        /// The whole seam in one assertion: point at a wall, and a floor site appears on top of it.
        ///
        /// <para>The cell in the middle is never written down by the test. Whatever the picker
        /// says, that is what the order is given — which is the only way a disagreement between
        /// the two rules can fail a test rather than a playtest.</para>
        /// </summary>
        [Test]
        public void PointingAtAWallOrdersAFloorOnTopOfIt()
        {
            RenderTestWorld world = GroundWithAWallAt(4, 4);
            ConstructionGrid construction = ConstructionOver(world);

            bool hit = SlicePicker.Pick(DownAt(4, 4), world.Model, activeLayer: 1, out CellRef clicked);
            Assert.That(hit, Is.True, "a click on a wall hits something");
            Assert.That(clicked, Is.EqualTo(new CellRef(4, 4, 1)),
                "and what it hits is the wall's own cell — this is the fact the Sim tier cannot see");

            Assert.That(construction.Place(clicked, BuildingHandle.Floor, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None),
                "so the floor tool must accept that cell, or it accepts nothing a player can point at");
            Assert.That(construction.At(world.Index(4, 4, 2)), Is.EqualTo(BuildingHandle.Floor),
                "and the site stands on the wall rather than inside it");
        }

        /// <summary>
        /// The control, and the reason the lift is not simply "always one up". Pointing at open
        /// grass is refused, because the ground is already a floor — and the refusal must not be
        /// smuggled one layer higher, where there would be nothing to hold a slab up.
        /// </summary>
        [Test]
        public void PointingAtBareGroundOrdersNothing()
        {
            RenderTestWorld world = GroundWithAWallAt(4, 4);
            ConstructionGrid construction = ConstructionOver(world);

            bool hit = SlicePicker.Pick(DownAt(6, 6), world.Model, activeLayer: 1, out CellRef clicked);
            Assert.That(hit, Is.True, "grass is pickable");

            Assert.That(construction.Place(clicked, BuildingHandle.Floor, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.NotPermitted), "there is already a floor there");
            Assert.That(construction.At(world.Index(6, 6, 1)), Is.EqualTo(BuildingHandle.None));
            Assert.That(construction.At(world.Index(6, 6, 2)), Is.EqualTo(BuildingHandle.None),
                "and nothing was ordered in the air above it either");
        }

        /// <summary>
        /// The storey loop, which is what makes the feature a building game rather than one slab:
        /// point at the wall, floor it, then point at the floor and put the next wall on that.
        /// Every cell comes from the picker; none is named here.
        /// </summary>
        [Test]
        public void AFloorRaisedOnAWallIsItselfSomethingToPointAt()
        {
            RenderTestWorld world = GroundWithAWallAt(4, 4);
            ConstructionGrid construction = ConstructionOver(world);
            var nav = new NavGraph(world.Grid);
            var pawns = new PawnContext(
                world.Grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns());

            SlicePicker.Pick(DownAt(4, 4), world.Model, activeLayer: 1, out CellRef onTheWall);
            Assert.That(construction.Place(onTheWall, BuildingHandle.Floor, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            construction.Raise(pawns, world.Index(4, 4, 2));
            world.Publish();

            SlicePicker.Pick(DownAt(4, 4), world.Model, activeLayer: 2, out CellRef onTheFloor);
            Assert.That(onTheFloor, Is.EqualTo(new CellRef(4, 4, 2)),
                "a slab is picked in the cell it is laid in");
            Assert.That(construction.Place(onTheFloor, BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None),
                "and a wall stands on it, which is how the second storey begins");
        }
    }
}
