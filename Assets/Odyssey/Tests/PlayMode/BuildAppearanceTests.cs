#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// <b>How long after a building is built does the player see it?</b>
    ///
    /// <para>Written 2026-09-21 on an owner report: <i>"there is about a second or 3 delay when the
    /// object appears when it's built, IE door, walls etc"</i>. Reading the publish seam said it
    /// should be the next frame, which is exactly the kind of answer this project has been wrong
    /// about before, so it is measured here instead.</para>
    ///
    /// <para><b>What the measurement found.</b> The seam is innocent — a raised wall is in the
    /// render mirror on the very next tick's publish and drawn on the frame after that — and the
    /// *whole board*
    /// was being re-meshed because <c>WorldRenderModel.Version</c> was one number for every
    /// chunk. One wall on the meadow re-meshed all 45 drawn chunks and cost 12.53 ms in that
    /// frame against 0.7 ms either side of it. With per-chunk versions it is 3 chunks and
    /// 1.73 ms. Both figures are contrasts taken <em>inside one run</em>, which is the only way a
    /// frame number means anything on this machine (<c>docs/lessons.md</c>).</para>
    /// </summary>
    public class BuildAppearanceTests
    {
        /// <summary>
        /// A raised building reaches the renderer immediately. The logged numbers are the point of
        /// the fixture; the assertions are the gate.
        /// </summary>
        [UnityTest]
        public IEnumerator ARaisedWallIsDrawnOnTheNextPublishAndRemeshesOnlyItsOwnChunks()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                ColonyWorld colony = boot.Colony!;
                GridSize size = colony.Grid.Size;
                CellRef at = WallSiteNearTheStart(colony);
                int cell = size.Index(at.X, at.Z, at.Y);

                Assume.That(colony.Construction.Place(at, BuildingHandle.Wall, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None), "the fixture could order a wall");

                int meshedBefore = boot.Renderer!.TotalChunksMeshed;
                int tickAtRaise = boot.World!.CurrentTick;
                float raisedAt = Time.realtimeSinceStartup;
                colony.Construction.Raise(colony.Pawns, cell, (byte)QualityHandle.Normal);
                Assert.That(colony.Grid.IsBlockedByEdifice(cell), Is.True,
                    "the fixture raised a wall; nothing below means anything otherwise");

                var frames = new float[12];
                int inMirror = -1, drawn = -1, ticksWaited = -1;
                float secondsWaited = -1f;
                for (int frame = 0; frame < 60; frame++)
                {
                    float t0 = Time.realtimeSinceStartup;
                    yield return null;
                    if (frame < frames.Length) frames[frame] = (Time.realtimeSinceStartup - t0) * 1000f;

                    if (inMirror < 0 && boot.Model!.EdificeDef(cell) == CoreContent.EdificeWall)
                    {
                        inMirror = frame;
                        ticksWaited = boot.World!.CurrentTick - tickAtRaise;
                        secondsWaited = Time.realtimeSinceStartup - raisedAt;
                    }
                    if (drawn < 0 && boot.Renderer.TotalChunksMeshed > meshedBefore)
                        drawn = frame;
                }

                int chunksMeshed = boot.Renderer.TotalChunksMeshed - meshedBefore;
                Debug.Log($"[build-appearance] in the mirror on frame {inMirror} "
                          + $"({ticksWaited} ticks, {secondsWaited * 1000f:0.0} ms), drawn on frame "
                          + $"{drawn}, {chunksMeshed} chunks re-meshed; frame ms after the raise: "
                          + string.Join(", ", System.Array.ConvertAll(frames, f => f.ToString("0.00"))));

                // **Ticks, not frames.** The mirror is written by a snapshot contributor, so it
                // updates once a tick and not once a frame — and a rig with nothing to draw runs
                // frames far faster than the fixed tick. Asserting frames measured the frame rate:
                // this test failed at 13 frames in a full PlayMode run and passed at 0 alone, on
                // the same commit and with the same one tick of delay both times.
                Assert.That(ticksWaited, Is.InRange(0, 1),
                    "a raised wall reaches the render mirror on the very next publish — if this "
                    + "fails, the publish seam is the delay the owner sees");
                Assert.That(secondsWaited, Is.LessThan(0.5f),
                    "and it is there in well under a second of wall clock, which is the owner's "
                    + "own unit: the report this fixture exists for was one to three seconds");
                Assert.That(drawn - inMirror, Is.InRange(0, 2),
                    "and the chunk holding it is re-meshed on the frame after it appears");

                // The board is 120x120x16 and draws 45 chunks; one wall touches at most the 3x3x3
                // neighbourhood MarkChunksAround dirties, which is three chunks of the drawn band.
                // Ten is loose enough not to be a trap and far enough below 45 to fail the day a
                // global invalidation comes back. See WorldRenderModel.ChunkVersion.
                Assert.That(chunksMeshed, Is.LessThan(10),
                    "one changed cell re-meshes its own chunks, not the whole board");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A cell near the start that will take a wall and has nobody in it or walking into it.</summary>
        static CellRef WallSiteNearTheStart(ColonyWorld colony)
        {
            GridSize size = colony.Grid.Size;
            CellRef start = colony.Start;
            for (int radius = 1; radius <= 8; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;

                int cell = size.Index(x, z, start.Y);
                if (!colony.Grid.IsWalkable(cell)) continue;
                if (!colony.Construction.Allows(cell, BuildingHandle.Wall)) continue;
                // Nobody in it and nobody walking into it, or the guard of design 30 refuses the
                // raise and this fixture measures nothing at all.
                if (PawnEviction.Occupant(colony.Pawns, cell) != null) continue;
                return new CellRef(x, z, start.Y);
            }

            throw new AssertionException("no cell near the start will take a wall");
        }
    }
}
