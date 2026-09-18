#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Which way a ladder faces — decided once, and read by both the thing that draws it and the
    /// thing that hangs a colonist on it.
    ///
    /// <para><b>Why this file exists.</b> The rule used to live in the mesher, which took the first
    /// occluding neighbour and fell back to north, while <c>PawnFigureDirector</c> kept its own —
    /// the first <i>solid</i> neighbour, in a different order, falling back to nothing at all. Two
    /// rules and two fallbacks for one plane. Where nothing occluded, the ladder was drawn on the
    /// north face and the climber found no wall, gave up its lean, and rode the idle pose straight
    /// up through the air: the owner's <i>"when a colonist goes up a ladder they seem to levitate"</i>
    /// (2026-09-18). It is the <c>HopPriceHasOneOwnerTests</c> lesson in another place.</para>
    ///
    /// <para>So <see cref="Odyssey.Presentation.World.WorldRenderModel.LadderFacing"/> is the
    /// owner. What is pinned below is the rule itself, and that the drawn ladder really does stand
    /// where the owner says — measured off the mesher's own matrices rather than read off the
    /// source, so routing the mesher through a second rule fails here.</para>
    /// </summary>
    public class LadderFaceTests
    {
        static ChunkBatch MeshLayer(RenderTestWorld world, int layer)
        {
            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model);
            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            mesher.Mesh(batch, layer * chunksPerLayer);
            return batch;
        }

        /// <summary>A ladder standing at (4, 4, 1) with the floor under it, and nothing else.</summary>
        static RenderTestWorld Shaft(int facing = 0) =>
            new RenderTestWorld(8, 8, 4)
                .Solid(4, 4, 0, NaturalContent.TerrainSubsoil)
                .Edifice(4, 4, 1, CoreContent.EdificeLadder, CoreContent.StuffConcrete,
                    blocking: false, facing: facing);

        [Test]
        public void ALadderLooksAwayFromWhateverItIsFixedTo()
        {
            // A wall to the east, so the ladder's back is east and it looks west.
            var world = Shaft().Solid(5, 4, 1).Publish();

            int facing = world.Model.LadderFacing(world.Index(4, 4, 1));

            Assert.That(facing, Is.EqualTo(Directions.West),
                "a ladder fixed to the wall on its east side faces west, out of it");
        }

        /// <summary>
        /// **The case that was levitating.** A ladder run up through an open storey has nothing
        /// occluding beside it — slabs are not solid — and the old pair of rules disagreed about it
        /// completely: the mesher drew north, the climber found nothing. There must be an answer,
        /// and it must be the one the mesher has always drawn.
        /// </summary>
        [Test]
        public void AFreeStandingLadderFacesTheWayItWasTurned()
        {
            // The owner's second report on the same ladder (2026-09-18): right cell, wrong side,
            // "standing in mid-air". With nothing to be fixed to there was nowhere to take an
            // answer from and it fell back to north, which the player could neither predict nor
            // change. A ladder rotates now, and this is where the rotation is read.
            foreach (int facing in new[] { Directions.North, Directions.East, Directions.South, Directions.West })
            {
                RenderTestWorld world = Shaft(facing).Publish();
                Assert.That(world.Model.LadderFacing(world.Index(4, 4, 1)), Is.EqualTo(facing),
                    $"a free-standing ladder turned to {facing} should face {facing}");
            }
        }

        /// <summary>
        /// And the wall still wins, because which side of a wall a ladder is bolted to is physics
        /// rather than preference. A player who turns a ladder into the stone it is fixed to gets
        /// the ladder they can actually climb.
        /// </summary>
        [Test]
        public void AWallBeatsTheRotation()
        {
            var world = Shaft(Directions.North).Solid(5, 4, 1).Publish();

            Assert.That(world.Model.LadderFacing(world.Index(4, 4, 1)), Is.EqualTo(Directions.West),
                "fixed to the wall on its east side, whatever it was turned to");
        }

        [Test]
        public void TheSameWorldAlwaysGivesTheSameFace()
        {
            // Two walls, so a rule that preferred "the nearest" or "the biggest" would have a
            // choice to make. It must make the same one every time it is asked, or the figure
            // swings round the shaft as the world changes around it.
            var world = Shaft().Solid(5, 4, 1).Solid(4, 5, 1).Publish();

            int first = world.Model.LadderFacing(world.Index(4, 4, 1));
            for (int i = 0; i < 8; i++)
                Assert.That(world.Model.LadderFacing(world.Index(4, 4, 1)), Is.EqualTo(first));
        }

        /// <summary>
        /// And the ladder is really drawn there. This is the half that would catch the mesher
        /// quietly growing a rule of its own again: the yaw in the instance matrix is measured, not
        /// assumed, and compared with what the owner says.
        /// </summary>
        [Test]
        public void TheDrawnLadderStandsWhereTheOwnerSaysItDoes()
        {
            foreach (int wall in new[] { Directions.North, Directions.East, Directions.South, Directions.West })
            {
                var world = Shaft();
                world.Solid(4 + Directions.DeltaX[wall], 4 + Directions.DeltaZ[wall], 1);
                world.Publish();

                int facing = world.Model.LadderFacing(world.Index(4, 4, 1));
                Assert.That(facing, Is.EqualTo(Directions.Opposite(wall)),
                    "the fixture is wrong: the ladder should be facing out of its wall");

                float drawn = YawOfTheLadder(
                    MeshLayer(world, 1), world.Model.EdificeModule(world.Index(4, 4, 1)));
                Assert.That(Mathf.DeltaAngle(drawn, Directions.Yaw[facing]), Is.EqualTo(0f).Within(0.01f),
                    $"with a wall to the {wall}, the drawn ladder is at {drawn}° and the owner says " +
                    $"{Directions.Yaw[facing]}°");
            }
        }

        /// <summary>
        /// The yaw of the one ladder in a batch, picked out by its module — the wall beside it is
        /// drawn on the same layer and is not what is being measured.
        /// </summary>
        static float YawOfTheLadder(ChunkBatch batch, int ladderModule)
        {
            var found = new List<Matrix4x4>();
            foreach (InstanceBucket bucket in batch.Body)
            {
                if (bucket.Module != ladderModule) continue;
                for (int i = 0; i < bucket.Count; i++) found.Add(bucket.Matrices[i]);
            }

            Assert.That(found.Count, Is.EqualTo(1),
                $"the fixture should draw exactly one ladder on this layer, it drew {found.Count}");

            return found[0].rotation.eulerAngles.y;
        }
    }
}
