#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
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

            // A real PawnContext rather than a bare ColonyItems: the grid validates a bed's owner
            // against the pawn list, so it takes the registry, and the registry is only ever made
            // by a context. Nothing here spawns a pawn — the fixture's questions are about where
            // an order lands, not who sleeps in it.
            var nav = new NavGraph(world.Grid);
            var ctx = new PawnContext(
                world.Grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns());
            return new ConstructionGrid(
                world.Grid, new EdificeSaveSection(edifices), ctx.Items, ctx.Pawns,
                new SupportSolver(world.Grid));
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
        /// <b>A wall that is not on the slice's own layer, which on the played board is most of
        /// them.</b>
        ///
        /// <para>The owner's third report: <i>"I can't place any structure floor/slab on anything
        /// with anything — it never wants to build."</i> This is why. The floor tool takes its layer
        /// from the slice (<c>DesignateDirector.WorkingLayer</c>, added so that a pointer could name
        /// open air over a room), and it took it <b>unconditionally</b> — so a click on a wall
        /// standing one terrace up was rewritten down to the slice's layer, landing inside the
        /// hillside, and refused.</para>
        ///
        /// <para>The played meadow is terraced across five layers and the slice starts on one of
        /// them, so the tool worked only on the columns whose ground happened to be at exactly that
        /// height. From a player's seat that is "it never works".</para>
        ///
        /// <para>The rule now: <b>the higher of the two.</b> The slice is a floor under the order,
        /// not an override of it — it can lift a click into open air above a room, which is what it
        /// was for, and it can never drag one down into the ground, which it was never meant to
        /// do.</para>
        /// </summary>
        [Test]
        public void AWallOnAHigherTerraceIsStillFlooredFromASliceBelowIt()
        {
            // Two terraces: the low half is walkable at layer 2, the high half at layer 3.
            var world = new RenderTestWorld(8, 8, 8);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
            {
                int top = x < 4 ? 2 : 1;
                for (int y = 0; y <= top; y++)
                    world.Solid(x, z, y, Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainGrass);
            }

            // A wall on the HIGH terrace, at its own walkable layer 3.
            world.Edifice(2, 4, 3, CoreContent.EdificeWall).Publish();

            ConstructionGrid construction = ConstructionOver(world);
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Floor);

            // The slice is on the LOW terrace, which is where it starts and where the player is.
            const int sliceLayer = 2;
            director.WorkingLayer = sliceLayer;

            Assert.That(SlicePicker.Pick(DownAt(2, 4), world.Model, activeLayer: 3, out CellRef clicked),
                Is.True);
            Assume.That(clicked, Is.EqualTo(new CellRef(2, 4, 3)), "the pointer names the wall");

            // Through the director, as a gesture does, so the substitution is the real one.
            Assume.That(director.Begin(clicked), Is.True);
            IReadOnlyList<CellRef> ordered = director.Commit();
            Assert.That(ordered, Has.Count.EqualTo(1));

            Assert.That(construction.Place(ordered[0], BuildingHandle.Floor, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None),
                "a wall one terrace above the slice must still take a floor on top of it");
            Assert.That(construction.At(world.Index(2, 4, 4)), Is.EqualTo(BuildingHandle.Floor),
                "and the floor goes on the wall, not into the hillside at the slice's layer");
        }

        /// <summary>
        /// The other half of the same rule, and the reason it is a floor rather than a ceiling: a
        /// slice <em>above</em> what the pointer names still wins, because that is the only way to
        /// name a cell of open air over a room.
        /// </summary>
        [Test]
        public void ASliceAboveThePointerStillDecidesTheLayer()
        {
            RenderTestWorld world = GroundWithAWallAt(4, 4);
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Floor);
            director.WorkingLayer = 3;

            Assume.That(director.Begin(new CellRef(6, 6, 0)), Is.True);
            IReadOnlyList<CellRef> ordered = director.Commit();

            Assert.That(ordered[0].Y, Is.EqualTo(3),
                "a slice above the surface is what lets a pointer name open air over a room");
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

        /// <summary>
        /// <b>Can a player build a bed by pointing at the ground?</b>
        ///
        /// <para>The same question this fixture was written to ask about floors, asked of the one
        /// other thing whose order is not lifted the way a wall's is. A bed is deliberately never
        /// lifted — <c>ConstructionGrid.WhereItWouldLand</c> returns the clicked cell for anything
        /// wider than one, because lifting one end of a two-cell thing while the other stays put
        /// is an order whose shape the player cannot see — and the picker deliberately answers a
        /// click on bare grass with the ground <i>block</i>. Put together, those two rules decide
        /// whether the bed is placeable at all, and neither assembly's own tests can see both.</para>
        ///
        /// <para>Every cell is the picker's. The test says which layer it expects the site on and
        /// nothing else, so a disagreement between the two rules fails here rather than in a
        /// playtest.</para>
        /// </summary>
        [Test]
        public void PointingAtBareGroundOrdersABedInTheAirAboveIt()
        {
            RenderTestWorld world = GroundWithAWallAt(4, 4);
            ConstructionGrid construction = ConstructionOver(world);

            bool hit = SlicePicker.Pick(DownAt(6, 6), world.Model, activeLayer: 1, out CellRef clicked);
            Assert.That(hit, Is.True, "grass is pickable");

            Assert.That(construction.Place(clicked, BuildingHandle.Bed, StuffHandle.Wood, facing: 0),
                Is.EqualTo(IntentRejection.None),
                "a bed ordered on open grass is the commonest order in the game; if this refuses, "
                + "the tool is armable, draggable and inert");

            int head = world.Index(6, 6, 1);
            Assert.That(construction.At(head), Is.EqualTo(BuildingHandle.Bed),
                "the site stands on the grass rather than inside the block");
            Assert.That(construction.SiteAt(new CellRef(6, 7, 1)), Is.EqualTo(head),
                "and the far cell of the footprint names the same one order");
        }
    }
}
