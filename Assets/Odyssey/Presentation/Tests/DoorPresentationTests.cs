#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    [TestFixture]
    public class DoorPresentationTests
    {
        [Test]
        public void ADoorBetweenEastWestWallsFacesNorth()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Publish();

            int facing = world.Model.DoorFacing(3, 3, 1);
            Assert.That(facing, Is.EqualTo(Directions.North),
                "with walls to east and west, the doorway opening points north/south");
        }

        [Test]
        public void ADoorBetweenNorthSouthWallsFacesEast()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(3, 2, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(3, 4, 1, CoreContent.EdificeWall)
                .Publish();

            int facing = world.Model.DoorFacing(3, 3, 1);
            Assert.That(facing, Is.EqualTo(Directions.East),
                "with walls to north and south, the doorway opening points east/west");
        }

        [Test]
        public void ADoorFacesOutdoorsWhenOneSideIsRoofed()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Slab(3, 4, 2)
                .Publish();

            int facing = world.Model.DoorFacing(3, 3, 1);
            Assert.That(facing, Is.EqualTo(Directions.South),
                "doorway should face towards unroofed outdoor facade (South)");
        }

        [Test]
        public void ADoorRespectsPlayerFacingAtEndOrCorner()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Publish();

            int facing = world.Model.DoorFacing(3, 3, 1, Directions.South);
            Assert.That(facing, Is.EqualTo(Directions.South),
                "doorway at end of wall should face in the direction chosen by the player (South)");
        }

        [Test]
        public void ADoorDoesNotFaceIntoAdjacentWall()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Publish();

            // West is occupied by an occluding wall at (2, 3).
            int facing = world.Model.DoorFacing(3, 3, 1, Directions.West);
            Assert.That(facing, Is.Not.EqualTo(Directions.West),
                "doorway should not face directly into an adjacent solid wall");
        }

        [Test]
        public void ADoorLeafStartsClosed()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Publish();

            int doorIndex = world.Index(3, 3, 1);
            using var director = new DoorDirector(world.Model, null, null, 0)
            {
                SubmitToGpu = false,
            };

            Assert.That(director.ActiveDoorCount, Is.EqualTo(1));
            Assert.That(director.OpenFactor(doorIndex), Is.EqualTo(0f));
            Assert.That(director.IsOpen(doorIndex), Is.False);
        }

        [Test]
        public void AnApproachingPawnSlidesTheDoorOpen()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Publish();

            int doorIndex = world.Index(3, 3, 1);
            using var director = new DoorDirector(world.Model, null, null, 0)
            {
                SubmitToGpu = false,
            };

            var slice = new SliceSettings();

            // Pawn is approaching door cell (3, 3, 1) from (3, 2, 1)
            var snapshot = new WorldSnapshot();
            snapshot.AddPawn(new PawnView(
                new PawnId(1),
                new CellRef(3, 2, 1),
                food: 100, rest: 100, mood: 100,
                nextCell: new CellRef(3, 3, 1),
                movePercent: 50));

            // Sync 0.1s (half of DoorDirector.OpenDuration 0.2s)
            director.Sync(snapshot, 1, slice, 0.1f);
            Assert.That(director.OpenFactor(doorIndex), Is.EqualTo(0.5f).Within(0.01f));

            // Sync another 0.1s -> fully open
            director.Sync(snapshot, 1, slice, 0.1f);
            Assert.That(director.OpenFactor(doorIndex), Is.EqualTo(1.0f).Within(0.01f));
            Assert.That(director.IsOpen(doorIndex), Is.True);
        }

        [Test]
        public void ADoorClosesWhenPawnLeaves()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Publish();

            int doorIndex = world.Index(3, 3, 1);
            using var director = new DoorDirector(world.Model, null, null, 0)
            {
                SubmitToGpu = false,
            };

            var slice = new SliceSettings();

            // Start fully open
            director.SetDoorState(doorIndex, 1.0f, targetOpen: true);
            Assert.That(director.OpenFactor(doorIndex), Is.EqualTo(1.0f));

            // Pawn is now far away at (0, 0, 1)
            var snapshot = new WorldSnapshot();
            snapshot.AddPawn(new PawnView(
                new PawnId(1),
                new CellRef(0, 0, 1),
                food: 100, rest: 100, mood: 100));

            // Sync 0.1s (half of CloseDuration 0.2s)
            director.Sync(snapshot, 1, slice, 0.1f);
            Assert.That(director.OpenFactor(doorIndex), Is.EqualTo(0.5f).Within(0.01f));

            // Sync another 0.1s -> fully closed
            director.Sync(snapshot, 1, slice, 0.1f);
            Assert.That(director.OpenFactor(doorIndex), Is.EqualTo(0f).Within(0.01f));
            Assert.That(director.IsOpen(doorIndex), Is.False);
        }

        [Test]
        public void DoorLeafResolvesFallbackWithoutPacks()
        {
            var library = new ModuleLibrary(null);
            int leaf = library.Resolve(ModuleIds.DoorLeaf, ModuleShape.WallPanel);
            ResolvedModule module = library[leaf];

            Assert.That(module.IsEmpty, Is.False, "fallback wall panel must not be empty");
            Assert.That(module.Parts.Length, Is.GreaterThan(0), "fallback must contain at least one part");
        }

        /// <summary>
        /// The door list reads the chunks an edit touched, not the board (design 62 §2c, DM1). It
        /// used to rescan every cell whenever the model's one version moved — any edit anywhere.
        /// A 60 x 60 x 3 board is 27 chunks; a door raised on layer 1 re-stamps its own chunk and
        /// the one beneath it (<c>RefreshDirty</c> re-meshes a column downwards), so two.
        /// </summary>
        [Test]
        public void ADoorRaisedRescansItsChunkNotTheBoard()
        {
            var world = new RenderTestWorld(60, 60, 3)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(40, 40, 1, CoreContent.EdificeDoor, blocking: false)
                .Publish();
            using var director = new DoorDirector(world.Model, null, null, 0) { SubmitToGpu = false };

            Assert.That(director.ActiveDoorCount, Is.EqualTo(2));
            Assert.That(director.DoorChunksRescanned, Is.EqualTo(world.Chunks.Count),
                "the first list is a scan of the whole board");

            world.Edifice(41, 40, 1, CoreContent.EdificeDoor, blocking: false);
            world.Chunks.MarkDirty(41, 40, 1);
            world.PublishEdits();

            Assert.That(director.ActiveDoorCount, Is.EqualTo(3), "the new door was missed");
            Assert.That(director.DoorChunksRescanned, Is.InRange(1, 2),
                "an edit in one chunk rescanned more of the board than the chunk and the one below it");

            // And a door taken away leaves the list: its chunk drops what it held before the rescan.
            world.Grid.Edifice[world.Index(3, 3, 1)] = -1;
            world.Chunks.MarkDirty(3, 3, 1);
            world.PublishEdits();

            Assert.That(director.ActiveDoorCount, Is.EqualTo(2), "a door that came down stayed in the list");
        }

#if UNITY_EDITOR
        [Test]
        public void DoorFrameDoesNotUseBrickMaterial()
        {
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            if (catalogue == null) return;

            var library = new ModuleLibrary(catalogue);
            int door = library.Resolve(ModuleIds.Door, ModuleShape.WallPanel);
            ResolvedModule module = library[door];
            if (!module.UsesArt) return;

            foreach (var part in module.Parts)
            {
                Assert.That(part.Material.name, Does.Not.Contain("Brick"),
                    $"Door module part should not use brick material, but has {part.Material.name}");
            }
        }
#endif
    }
}
