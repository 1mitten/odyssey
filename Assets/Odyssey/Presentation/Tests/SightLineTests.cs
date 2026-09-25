#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What stands between the camera and a selected colonist, and what only looks as though it
    /// does.
    ///
    /// <para>The whole decision is arithmetic on a segment and a box, so it is checked here with
    /// no scene, no camera and no art. The cases are the ones that were reasoned about while it
    /// was written and would each be an obvious bug on screen: the tree in the way, the tree
    /// beside it, the tree behind the colonist, the wall behind the camera, and the ground the
    /// colonist is standing on — which is the one a careless radius fades, leaving the player
    /// looking down a hole at the person they selected.</para>
    /// </summary>
    public class SightLineTests
    {
        /// <summary>
        /// A colonist at the origin's chest height, and an eye up and back along -x, which is
        /// roughly the board camera's relation to what it is looking at.
        /// </summary>
        static readonly Vector3 Chest = new Vector3(0f, 1.35f, 0f);
        static readonly Vector3 Eye = new Vector3(-40f, 30f, 0f);

        static SightLines Looking()
        {
            var sight = new SightLines();
            sight.Add(Eye, Chest);
            return sight;
        }

        /// <summary>A box of <paramref name="height"/> metres standing on the ground at x, z.</summary>
        static Bounds Standing(float x, float z, float height, float width = 1.2f)
            => new Bounds(new Vector3(x, height * 0.5f, z), new Vector3(width, height, width));

        [Test]
        public void NothingIsInTheWayUntilSomebodyIsSelected()
        {
            var sight = new SightLines();
            Assert.That(sight.Any, Is.False);
            Assert.That(sight.Blocks(Standing(-10f, 0f, 6f)), Is.False,
                "a tree was faded with nobody selected");
        }

        [Test]
        public void ATreeOnTheLineIsInTheWay()
        {
            // Ten metres back along the sight line the beam is 7.5 m up, which is inside the crown
            // of a tree standing on the ground and nowhere near its trunk. That is the case the
            // whole class exists for, and the reason the test is a box and not a point: asked of
            // the trunk's own position this tree is six metres below the line.
            Assert.That(Looking().Blocks(Standing(-10f, 0f, 9f)), Is.True);
        }

        [Test]
        public void ATreeToOneSideIsNot()
        {
            Assert.That(Looking().Blocks(Standing(-10f, 6f, 9f)), Is.False,
                "a tree six metres off the line was faded");
        }

        [Test]
        public void AShortTreeUnderTheLineIsNot()
        {
            // Same footprint as the tree that blocks, half the height. The eye passes over it.
            Assert.That(Looking().Blocks(Standing(-10f, 0f, 4f)), Is.False);
        }

        [Test]
        public void ATreeBehindTheColonistIsNot()
        {
            // Past the far end of the segment. It is between the colonist and the horizon, which
            // hides nothing — and an unclipped ray test would ghost every one of them.
            Assert.That(Looking().Blocks(Standing(12f, 0f, 9f)), Is.False);
        }

        [Test]
        public void AWallBehindTheCameraIsNot()
        {
            // Past the near end. Not drawn anyway, but a ray run backwards would fade it and the
            // cost would be paid on every chunk behind the viewer.
            Assert.That(Looking().Blocks(new Bounds(new Vector3(-60f, 31f, 0f), Vector3.one * 4f)),
                Is.False);
        }

        [Test]
        public void TheGroundTheColonistStandsOnIsNot()
        {
            // The segment stops at the chest, so the floor is a metre and a third below its end.
            // Fading it would open a hole under the person the player just selected — which is
            // why the line is aimed at the chest and stops there rather than running to the feet.
            var floor = new Bounds(new Vector3(0f, -CellMetrics.SizeY * 0.5f, 0f),
                new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ));
            Assert.That(Looking().Blocks(floor), Is.False);
        }

        [Test]
        public void ATreeInTheColonistsOwnCellIs()
        {
            // Trees block nothing in this game, so a colonist walks about inside woodland and is
            // hidden by the tree they are standing in more often than by any other.
            Assert.That(Looking().Blocks(Standing(0f, 0f, 9f)), Is.True);
        }

        [Test]
        public void TheBeamWidensWithTheRadius()
        {
            var narrow = Looking();
            var wide = Looking();
            wide.Radius = 4f;

            Bounds tree = Standing(-10f, 3f, 9f);
            Assert.That(narrow.Blocks(tree), Is.False);
            Assert.That(wide.Blocks(tree), Is.True);
        }

        [Test]
        public void EverySelectedColonistGetsALineOfTheirOwn()
        {
            var sight = new SightLines();
            sight.Add(Eye, Chest);
            sight.Add(Eye, new Vector3(0f, 1.35f, 20f));

            Assert.That(sight.Count, Is.EqualTo(2));
            Assert.That(sight.Blocks(Standing(-10f, 0f, 9f)), Is.True, "the first line missed");
            Assert.That(sight.Blocks(Standing(-10f, 15f, 9f)), Is.True, "the second line missed");
            Assert.That(sight.Blocks(Standing(-10f, 40f, 9f)), Is.False);
        }

        [Test]
        public void ADegenerateLineIsNoLine()
        {
            // The camera sitting exactly on the colonist has no direction to fade along, and a
            // zero-length segment normalises to a junk direction. Dropped rather than guessed at.
            var sight = new SightLines();
            sight.Add(Chest, Chest);
            Assert.That(sight.Any, Is.False);
        }

        [Test]
        public void AChunkIsAskedForTheAirAboveItAsWellAsForItself()
        {
            // A chunk's bounds are one layer high; the tree rooted in it is three layers tall and
            // is the thing doing the hiding. Without the allowance the coarse test rejects the
            // chunk, no instance inside it is ever tested, and the feature silently does nothing.
            // Well back down the line, so the chunk the colonist is standing in — which the beam
            // reaches whatever the allowance — is not what is being measured.
            var chunk = new Bounds(
                new Vector3(-25f, CellMetrics.SizeY * 0.5f, 0f),
                new Vector3(20f, CellMetrics.SizeY, 20f));

            Assert.That(Looking().Touches(chunk), Is.False, "the beam is above this layer");
            Assert.That(Looking().Touches(chunk, ChunkRenderer.MinimumTallestModuleMetres), Is.True);
        }

        [Test]
        public void PlacingABoxCarriesItsRotationWithIt()
        {
            // A module's bounds are given about its placement point; the instance matrix turns and
            // moves it. The placed box has to contain the turned one, or a tree facing a different
            // way is tested against a box it has rotated out of.
            var local = new Bounds(new Vector3(0f, 3f, 0f), new Vector3(2f, 6f, 1f));
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(10f, 0f, 5f), Quaternion.Euler(0f, 90f, 0f), Vector3.one);

            Bounds placed = SightLines.Place(local, matrix);

            Assert.That(placed.center.x, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(placed.center.y, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(placed.center.z, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(placed.size.x, Is.EqualTo(1f).Within(1e-3f), "the turn was not applied");
            Assert.That(placed.size.z, Is.EqualTo(2f).Within(1e-3f), "the turn was not applied");
            Assert.That(placed.size.y, Is.EqualTo(6f).Within(1e-3f));
        }
    }

    /// <summary>
    /// The renderer's half: an outcrop standing between the eye and a colonist is drawn ghosted
    /// instead of solid, and everything else is drawn exactly as it was before.
    ///
    /// <para>The load-bearing assertion is the one about <see cref="ChunkRenderer.InstancesDrawn"/>
    /// staying put. The fade is a <em>partition</em> — every instance is still submitted, in one
    /// of two materials — so a change in the total means geometry has gone missing, which is the
    /// failure that would be hardest to see on a board covered in rock.</para>
    /// </summary>
    public class SightFadeRenderTests
    {
        /// <summary>
        /// Bare ground with two blocks of rock standing on it, two rows of z apart, and a colonist
        /// to the east of each. Each rock is in the way of its own colonist and of nobody else,
        /// which is what lets the partition be measured rather than merely observed.
        ///
        /// <para>They are two cells apart rather than adjacent so that neither changes how the
        /// other is meshed, and the board is one chunk so both are reached in one pass.</para>
        /// </summary>
        static RenderTestWorld Outcrop()
        {
            GroundRelief.Reset();
            var world = new RenderTestWorld(8, 8, 4);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
            world.Solid(2, 4, 1);
            world.Solid(2, 6, 1);
            return world.Publish();
        }

        static ChunkRenderer RendererFor(RenderTestWorld world)
        {
            var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 };
            renderer.Skirt.Enabled = false;
            return renderer;
        }

        /// <summary>The chest of a colonist standing on the ground in cell (x, z, 1).</summary>
        static Vector3 ChestAt(int x, int z) =>
            CellMetrics.FloorCentre(x, z, 1) + Vector3.up * 1.35f;

        /// <summary>The eye that looks at the colonist east of the rock in row <paramref name="z"/>.</summary>
        static Vector3 EyeFor(int z) => new Vector3(-20f, 8f, CellMetrics.FloorCentre(2, z, 1).z);

        /// <summary>How many instances these beams ghost between them.</summary>
        static int FadedBy(ChunkRenderer renderer, params int[] rows)
        {
            var sight = new SightLines();
            foreach (int z in rows) sight.Add(EyeFor(z), ChestAt(6, z));
            renderer.Sight = sight;
            renderer.Render(1, new SliceSettings());
            return renderer.InstancesFaded;
        }

        /// <summary>
        /// An outcrop between the eye and a colonist is ghosted, and the outcrop two rows over is
        /// not.
        ///
        /// <para><b>Asked as arithmetic on the counts rather than on the draw calls, and that is a
        /// correction.</b> The first version asserted that splitting a bucket costs a second call,
        /// which CI falsified: it stayed at three, because the two rocks are not in one bucket to
        /// begin with. How the mesher buckets a board is its own business — a decision that can
        /// change for reasons having nothing to do with this feature — so a test that leans on it
        /// is testing the wrong thing. What the partition actually promises is that each beam
        /// ghosts what stands in it and nothing else, so two beams together ghost the sum of what
        /// each ghosts alone. That holds however the geometry is bucketed.</para>
        /// </summary>
        [Test]
        public void AnOutcropBetweenTheEyeAndAColonistIsGhosted()
        {
            var world = Outcrop();
            ChunkRenderer renderer = RendererFor(world);

            renderer.Render(1, new SliceSettings());
            int instances = renderer.InstancesDrawn;
            Assert.That(renderer.InstancesFaded, Is.Zero, "something faded with nobody selected");

            int near = FadedBy(renderer, 4);
            Assert.That(renderer.ChunksSightTested, Is.GreaterThan(0), "no chunk was even tested");
            Assert.That(near, Is.GreaterThan(0), "the outcrop stayed solid");
            Assert.That(renderer.InstancesDrawn, Is.EqualTo(instances), "the partition lost geometry");

            int far = FadedBy(renderer, 6);
            Assert.That(far, Is.EqualTo(near),
                "the two outcrops are the same geometry, so a beam through either ghosts as much");

            // The whole of the claim: neither beam reaches the other's rock, so together they
            // ghost exactly twice what one does. A beam spilling sideways onto the far rock, or
            // down onto the ground, comes out over the sum; one that stopped discriminating per
            // instance and ghosted whole buckets comes out over it too.
            Assert.That(FadedBy(renderer, 4, 6), Is.EqualTo(near + far),
                "a beam ghosted something that was not standing in it");
        }

        [Test]
        public void AClearLineFadesNothing()
        {
            var world = Outcrop();
            ChunkRenderer renderer = RendererFor(world);

            // The same eye and the same colonist, moved two rows of z clear of the outcrop.
            var sight = new SightLines();
            sight.Add(new Vector3(-20f, 8f, CellMetrics.FloorCentre(2, 0, 1).z), ChestAt(6, 0));
            renderer.Sight = sight;
            renderer.Render(1, new SliceSettings());

            Assert.That(renderer.InstancesFaded, Is.Zero);
        }
    }
}
