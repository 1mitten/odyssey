#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Walls down (design 42): walls drawn as stumps and the built storeys above the slice hidden,
    /// chosen when drawn and never re-meshed — and the picker, the marks, the doors and the actors
    /// agreeing with the picture.
    /// </summary>
    public class WallsDownTests
    {
        const int Active = 1;

        static ChunkBatch MeshLayer(RenderTestWorld world, int layer)
        {
            var batch = new ChunkBatch();
            new ChunkMesher(world.Model).Mesh(batch, layer * world.Chunks.ChunksX * world.Chunks.ChunksZ);
            return batch;
        }

        static int Instances(List<InstanceBucket> buckets, bool skipStacked = false)
        {
            int n = 0;
            foreach (InstanceBucket bucket in buckets)
                if (!skipStacked || !bucket.Stacked) n += bucket.Count;
            return n;
        }

        static int Stacked(List<InstanceBucket> buckets)
        {
            int n = 0;
            foreach (InstanceBucket bucket in buckets)
                if (bucket.Stacked) n += bucket.Count;
            return n;
        }

        /// <summary>Ground under every cell of an 8 x 8 board, so a ray always has somewhere to land.</summary>
        static RenderTestWorld Ground(int layers = 4)
        {
            var world = new RenderTestWorld(8, 8, layers);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0, CoreContent.TerrainRock);
            return world;
        }

        static SliceSettings Lowered(bool lowered = true) => new SliceSettings { wallsLowered = lowered };

        // ------------------------------------------------------------------ the mesher

        [Test]
        public void AWallIsMeshedStandingAndAsAStumpAndNeitherIsInTheBody()
        {
            GroundRelief.Reset();
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();

            ChunkBatch batch = MeshLayer(world, 1);

            Assert.That(Instances(batch.Walls), Is.EqualTo(5), "four panels and a core, standing");
            Assert.That(Instances(batch.Stumps), Is.EqualTo(1), "one stump per wall cell");
            Assert.That(Instances(batch.Body), Is.Zero, "nothing that lowers is left in the body");
        }

        [Test]
        public void AStumpIsAQuarterOfTheStoreyAndStandsOnTheFloor()
        {
            GroundRelief.Reset();
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();

            ChunkBatch batch = MeshLayer(world, 1);
            Bounds stump = BoxOf(batch.Stumps[0].Matrices[0]);
            float floor = CellMetrics.FloorCentre(3, 3, 1).y;

            // Measured as ChunkMesherTests measures the core: the fallback unit cube placed by the
            // instance's own matrix, which is what the test world draws every module as.
            Assert.That(stump.size.y, Is.EqualTo(CellMetrics.StumpHeight).Within(0.05f), "0.75 m tall (owner, 2026-09-24)");
            Assert.That(stump.min.y, Is.EqualTo(floor).Within(0.05f), "its foot is on the floor");
            Assert.That(stump.size.x, Is.EqualTo(CellMetrics.SizeXZ).Within(0.05f), "and it fills the cell, as the core does");
        }

        /// <summary>The world-space box an instance of the fallback unit cube occupies.</summary>
        static Bounds BoxOf(Matrix4x4 m)
        {
            var box = new Bounds();
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 at = m.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? -0.5f : 0.5f,
                    (corner & 2) == 0 ? -0.5f : 0.5f,
                    (corner & 4) == 0 ? -0.5f : 0.5f));
                if (corner == 0) box = new Bounds(at, Vector3.zero);
                else box.Encapsulate(at);
            }
            return box;
        }

        [Test]
        public void AWindowGetsAStumpThoughItHasNoCore()
        {
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWindow).Publish();

            ChunkBatch batch = MeshLayer(world, 1);

            Assert.That(Instances(batch.Walls), Is.EqualTo(4), "four panels, no core, as ever");
            Assert.That(Instances(batch.Stumps), Is.EqualTo(1), "a window is part of the wall line");
        }

        [Test]
        public void ADoorwayIsTwoJambs()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Publish();

            ChunkBatch batch = MeshLayer(world, 1);

            Assert.That(Instances(batch.Stumps), Is.EqualTo(2 + 2), "a stump for each wall and two jambs for the door");
            Assert.That(Instances(batch.Walls), Is.EqualTo(11), "and the standing form is unchanged");
        }

        [Test]
        public void APillarLowersAndARockDoesNot()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 2, 1, CoreContent.EdificePillar)
                .Solid(5, 5, 1)
                .Publish();

            ChunkBatch batch = MeshLayer(world, 1);

            Assert.That(Instances(batch.Walls), Is.EqualTo(1), "the pillar, standing");
            Assert.That(Instances(batch.Stumps), Is.EqualTo(1), "the pillar, lowered; the rock has neither");
            Assert.That(Instances(batch.Body), Is.GreaterThan(0), "the rock stays in the body, drawn either way");
        }

        // ------------------------------------------------------------------ the renderer

        /// <summary>
        /// A house on the slice with a storey on top of it, and a terrace one layer up with a
        /// house of its own standing on the terrace's ground.
        /// </summary>
        static RenderTestWorld HouseAndTerrace()
        {
            RenderTestWorld world = Ground();
            for (int x = 1; x <= 3; x++) world.Edifice(x, 1, Active, CoreContent.EdificeWall);
            for (int x = 1; x <= 3; x++) world.Edifice(x, 1, Active + 1, CoreContent.EdificeWall);
            for (int x = 1; x <= 3; x++)
            for (int z = 2; z <= 3; z++)
                world.Slab(x, z, Active + 1);
            // The terrace: a step of rock, a wall standing on it and a floor laid on it.
            for (int x = 5; x <= 7; x++)
            for (int z = 5; z <= 7; z++)
                world.Solid(x, z, Active);
            world.Edifice(5, 5, Active + 1, CoreContent.EdificeWall);
            world.Slab(6, 6, Active + 1);
            return world.Publish();
        }

        [Test]
        public void AnUpperStoreyIsStackedAndAHouseOnATerraceIsNot()
        {
            RenderTestWorld world = HouseAndTerrace();
            WorldRenderModel model = world.Model;

            Assert.That(model.IsStackedAt(world.Index(2, 1, Active + 1)), Is.True, "a wall on a wall");
            Assert.That(model.IsStackedAt(world.Index(2, 2, Active + 1)), Is.True, "a floor over a room");
            Assert.That(model.IsStackedAt(world.Index(5, 5, Active + 1)), Is.False, "a wall on the terrace");
            Assert.That(model.IsStackedAt(world.Index(6, 6, Active + 1)), Is.False, "a floor on the terrace");
            Assert.That(model.IsStackedAt(world.Index(2, 1, Active)), Is.False, "the ground floor on the slice");
            Assert.That(model.IsStackedAt(world.Index(6, 5, Active)), Is.False, "rock is never built");

            ChunkBatch above = MeshLayer(world, Active + 1);
            Assert.That(Stacked(above.Walls) + Stacked(above.Stumps) + Stacked(above.Roof), Is.GreaterThan(0),
                "the upper storey's buckets are marked");
            Assert.That(Instances(above.Stumps, skipStacked: true), Is.EqualTo(1),
                "and the terrace wall's stump is not");
        }

        static int Drawn(RenderTestWorld world, SliceSettings slice)
        {
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            renderer.Render(Active, slice);
            // The board only: the surround is submitted from inside Render and counted with it,
            // and it is the same thousands of trees whether the walls are up or down.
            return renderer.InstancesDrawn - renderer.Skirt.InstancesDrawn;
        }

        [Test]
        public void WithTheWallsUpThePictureIsWhatItWas()
        {
            RenderTestWorld world = HouseAndTerrace();
            int expected = 0;
            for (int layer = 0; layer <= Active + 1; layer++)
            {
                ChunkBatch batch = MeshLayer(world, layer);
                expected += Instances(batch.Body) + Instances(batch.Roof) + Instances(batch.Walls);
            }

            Assert.That(Drawn(world, Lowered(false)), Is.EqualTo(expected),
                "body, roof and standing walls on every layer, and not one stump");
        }

        [Test]
        public void WithTheWallsDownStumpsStandInAndOnlyTheUpperStoreyIsHidden()
        {
            RenderTestWorld world = HouseAndTerrace();
            int expected = 0;
            for (int layer = 0; layer <= Active; layer++)
            {
                ChunkBatch batch = MeshLayer(world, layer);
                expected += Instances(batch.Body) + Instances(batch.Roof) + Instances(batch.Stumps);
            }
            ChunkBatch above = MeshLayer(world, Active + 1);
            int keptAbove = Instances(above.Body, skipStacked: true) + Instances(above.Roof, skipStacked: true)
                            + Instances(above.Stumps, skipStacked: true);
            Assert.That(Instances(above.Stumps, skipStacked: true), Is.GreaterThan(0),
                "the house on the terrace is there to keep, as stumps");
            Assert.That(Stacked(above.Roof) + Stacked(above.Stumps), Is.GreaterThan(0),
                "and the storey over the house on the slice is there to hide");
            expected += keptAbove;

            Assert.That(Drawn(world, Lowered()), Is.EqualTo(expected));
        }

        /// <summary>
        /// The toggle flips every time the Build palette opens, so flipping it must mesh nothing:
        /// both forms are always in the batch and the choice is made as the frame is drawn.
        /// </summary>
        [Test]
        public void FlippingTheWallsMeshesNothing()
        {
            RenderTestWorld world = HouseAndTerrace();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            SliceSettings slice = Lowered(false);

            renderer.Render(Active, slice);
            Assert.That(renderer.ChunksMeshedThisFrame, Is.GreaterThan(0), "the first frame meshes the board");

            for (int flip = 0; flip < 4; flip++)
            {
                slice.wallsLowered = !slice.wallsLowered;
                renderer.Render(Active, slice);
                Assert.That(renderer.ChunksMeshedThisFrame, Is.Zero, $"flip {flip} re-meshed a chunk");
            }
        }

        [Test]
        public void UndergroundTheGhostAboveIsLeftAlone()
        {
            // No landscape floor known: the opening layer is the only measure of the surface.
            var slice = new SliceSettings { wallsLowered = true, surfaceLayer = 5 };
            Assert.That(slice.BelowSurface(Active), Is.True, "the fixture is underground");
            Assert.That(slice.HidesStackedOn(Active, Active + 1), Is.False,
                "below the surface the storey above is an x-ray already, and walls-down leaves it be");
            Assert.That(slice.LowersWallsOn(Active, Active), Is.True, "but the walls still lower");
        }

        /// <summary>
        /// The owner's report of 2026-09-24: standing on real ground at L10, with the colony opened
        /// on L12, the building above was drawn see-through, because a lower terrace counted as
        /// underground. With the walls down, anything above the lowest ground is above ground.
        /// </summary>
        [Test]
        public void WithTheWallsDownALowerTerraceIsAboveGroundAndATunnelIsNot()
        {
            // The played board, measured (LandscapeBandTests): the colony opens on L12 and the
            // lowest terrace's rock tops out at L8, so its ground is walked on L9.
            var slice = new SliceSettings { surfaceLayer = 12, landscapeFloor = 8 };

            Assert.That(slice.BelowSurface(10), Is.True, "walls up: today's rule, unchanged");
            Assert.That(slice.GhostsAbove(10), Is.True, "and the one layer above is an x-ray");

            slice.wallsLowered = true;
            Assert.That(slice.BelowSurface(10), Is.False, "walls down: L10 is ground");
            Assert.That(slice.GhostsAbove(10), Is.False, "so nothing above it is see-through");
            Assert.That(slice.HidesStackedOn(10, 11), Is.True, "and upper storeys above it are hidden instead");
            Assert.That(slice.BelowSurface(9), Is.False, "the lowest ground is ground");
            Assert.That(slice.BelowSurface(8), Is.True, "beneath all of it is a tunnel, and keeps its x-ray");
        }

        // ------------------------------------------------------------------ the picker

        const float PlayCameraDegrees = 48f;

        /// <summary>A ray at the play camera's angle, looking north, aimed at a point on a layer.</summary>
        static Ray Aimed(float cellX, float cellZ, int layer, float height)
        {
            var target = new Vector3(cellX * CellMetrics.SizeXZ, layer * CellMetrics.SizeY + height, cellZ * CellMetrics.SizeXZ);
            float radians = PlayCameraDegrees * Mathf.Deg2Rad;
            var direction = new Vector3(0f, -Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
            return new Ray(target - direction * 60f, direction);
        }

        /// <summary>
        /// The floor just beyond a wall, which a standing wall hides and a stump does not: the ray
        /// crosses the wall's cell about 1.4 m up, between the stump's head and the wall's.
        /// </summary>
        [Test]
        public void AClickOverAStumpReachesTheFloorBehindIt()
        {
            GroundRelief.Reset();
            RenderTestWorld world = Ground().Edifice(4, 4, Active, CoreContent.EdificeWall).Publish();
            Ray ray = Aimed(4.5f, 5.5f, Active, 0f);

            Assert.That(SlicePicker.Pick(ray, world.Model, Active, Lowered(false), out CellRef standing), Is.True);
            Assert.That(standing, Is.EqualTo(new CellRef(4, 4, Active)), "a standing wall is in the way");

            Assert.That(SlicePicker.Pick(ray, world.Model, Active, Lowered(), out CellRef lowered), Is.True);
            Assert.That(lowered, Is.EqualTo(new CellRef(4, 5, 0)), "a stump is not: the ground behind it is picked");
        }

        [Test]
        public void AClickOnAStumpPicksTheWall()
        {
            GroundRelief.Reset();
            RenderTestWorld world = Ground().Edifice(4, 4, Active, CoreContent.EdificeWall).Publish();

            Assert.That(SlicePicker.Pick(Aimed(4.5f, 4.5f, Active, CellMetrics.StumpHeight), world.Model, Active,
                Lowered(), out CellRef cell), Is.True);
            Assert.That(cell, Is.EqualTo(new CellRef(4, 4, Active)), "the stump's top is the wall's");
        }

        /// <summary>
        /// A house on a higher terrace is drawn, as stumps, so its stumps are clickable — where an
        /// upper storey at the same height is not (the test below).
        /// </summary>
        [Test]
        public void AWallOnATerraceAboveTheSliceIsPickedByItsStump()
        {
            GroundRelief.Reset();
            RenderTestWorld world = Ground().Solid(4, 4, Active)
                .Edifice(4, 4, Active + 1, CoreContent.EdificeWall).Publish();

            Assert.That(SlicePicker.Pick(Aimed(4.5f, 4.5f, Active + 1, CellMetrics.StumpHeight), world.Model, Active,
                Lowered(), out CellRef cell), Is.True);
            Assert.That(cell, Is.EqualTo(new CellRef(4, 4, Active + 1)),
                "a wall standing on a higher terrace's ground is drawn, so its stump is a click target");
        }

        [Test]
        public void AFloorAboveTheSliceIsNotPickedWhileItIsHidden()
        {
            GroundRelief.Reset();
            RenderTestWorld world = Ground().Slab(4, 4, Active + 1).Publish();
            Ray ray = Aimed(4.5f, 4.5f, Active + 1, 0f);

            Assert.That(SlicePicker.Pick(ray, world.Model, Active, Lowered(false), out CellRef shown), Is.True);
            Assert.That(shown, Is.EqualTo(new CellRef(4, 4, Active + 1)), "drawn, so clickable");

            bool any = SlicePicker.Pick(ray, world.Model, Active, Lowered(), out CellRef hidden);
            Assert.That(any && hidden.Y == Active + 1, Is.False,
                "hidden, so a click passes through it to whatever is drawn below");
        }

        // ------------------------------------------------------------------ marks, actors, doors

        [Test]
        public void AMarkOnALoweredWallSitsOnTheStump()
        {
            RenderTestWorld world = Ground().Edifice(4, 4, Active, CoreContent.EdificeWall)
                .Bed(1, 1, Active, facing: 0).Publish();
            int wall = world.Index(4, 4, Active), bed = world.Index(1, 1, Active);

            Assert.That(world.Model.MarkHeight(wall, lowered: false), Is.EqualTo(CellMetrics.SizeY));
            Assert.That(world.Model.MarkHeight(wall, lowered: true), Is.EqualTo(CellMetrics.StumpHeight));
            Assert.That(world.Model.MarkHeight(bed, lowered: true), Is.EqualTo(world.Model.MarkHeight(bed)),
                "a bed does not lower, so its mark does not move");
        }

        [Test]
        public void SomebodyOnABuiltFloorAboveIsHiddenAndSomebodyOnAHillIsNot()
        {
            RenderTestWorld world = Ground()
                .Slab(2, 2, Active + 1)            // the upper storey of a house
                .Solid(5, 5, Active)               // a hill, whose top is walked at Active + 1
                .Solid(4, 4, Active).Slab(4, 4, Active + 1) // a floor laid on a terrace
                .Publish();
            SliceSettings slice = Lowered();

            Assert.That(slice.HidesStandingAt(Active, new CellRef(2, 2, Active + 1), world.Model), Is.True,
                "upstairs goes with the house");
            Assert.That(slice.HidesStandingAt(Active, new CellRef(5, 5, Active + 1), world.Model), Is.False,
                "the hilltop is landscape");
            Assert.That(slice.HidesStandingAt(Active, new CellRef(4, 4, Active + 1), world.Model), Is.False,
                "a house on a terrace is a ground floor, and whoever is in it stays");
            Assert.That(slice.HidesStandingAt(Active, new CellRef(2, 2, Active), world.Model), Is.False,
                "nobody on the slice itself is hidden");
            Assert.That(Lowered(false).HidesStandingAt(Active, new CellRef(2, 2, Active + 1), world.Model), Is.False,
                "and with the walls up nobody is");
        }

        [Test]
        public void ADoorLeafIsNotDrawnWhileTheWallsAreDown()
        {
            RenderTestWorld world = Ground()
                .Edifice(2, 3, Active, CoreContent.EdificeWall)
                .Edifice(3, 3, Active, CoreContent.EdificeDoor, blocking: false)
                .Edifice(4, 3, Active, CoreContent.EdificeWall)
                .Publish();
            using var doors = new DoorDirector(world.Model, null, null, 0) { IsOpenOverride = _ => false };
            doors.SubmitToGpu = false;

            doors.Sync(new WorldSnapshot(), Active, Lowered(false), 0f);
            Assert.That(doors.LeavesPlaced, Is.EqualTo(1), "walls up: the leaf is drawn");

            doors.Sync(new WorldSnapshot(), Active, Lowered(), 0f);
            Assert.That(doors.LeavesPlaced, Is.Zero, "walls down: the jambs are the doorway, and there is no leaf");
        }
    }
}
