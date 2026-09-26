#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Diagnostics;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <b>A colony re-run from a keyframe and its order log goes where the live colony went</b>
    /// (design 63 §3–§4, HR0). The claim ADR 0004 made on day one — "an intent log plus a world
    /// seed is a replayable test case" — written as a test for the first time.
    ///
    /// <para>The live half is played the way the game plays it: orders arrive as intents, some of
    /// them while the clock is stopped (<see cref="SimWorld.RepublishViews"/>, the paused frame),
    /// and the speed changes. The recorder is attached at a keyframe taken mid-run, not at tick
    /// zero, because that is where every real recording starts.</para>
    ///
    /// <para><b>The controls are not optional.</b> A replay that matched because the hashes could
    /// not see anything would be decoration, so the same log is replayed with one order dropped and
    /// with one order moved to a different cell, and both must be caught.</para>
    /// </summary>
    public class ReplayTests
    {
        /// <summary>The <see cref="SessionRoundTripTests"/> board, for its reason: hauling starts.</summary>
        static readonly GridSize Size = new GridSize(60, 60, 16);

        const uint Seed = 20_260_926u;

        /// <summary>Where the keyframe is taken: mid-run, with work already under way.</summary>
        const int KeyframeTick = 1_500;

        /// <summary>
        /// How long the recording runs: nearly a third of a day, long enough for felling, hauling and
        /// eating, and for eleven checkpoints.
        /// </summary>
        const int RecordedTicks = 7_000;

        static ColonyRequest Request() => new ColonyRequest
        {
            Size = Size,
            Seed = Seed,
            Scenario = ScenarioDef.Playtest(),
            Barren = true,
            Wooded = true,
            Map = MapType.Natural,
            Name = "Replay",
        };

        /// <summary>A recorded session: the keyframe's bytes, the log, and what the colony did.</summary>
        sealed class Recording
        {
            public byte[] Keyframe = Array.Empty<byte>();
            public ReplayLog Log = new ReplayLog();
            public int Felled;
        }

        /// <summary>
        /// The live session. Returns only the file and the log, so nothing of the live world can be
        /// reached by the replay half — the <see cref="SessionRoundTripTests"/> rule.
        /// </summary>
        static Recording PlayAndRecord()
        {
            ColonyWorld colony = ColonyWorld.Build(Request());
            SimWorld world = colony.World;
            world.Tick(KeyframeTick);

            // The keyframe, between ticks, and the recorder attached before another order can land.
            var recording = new Recording { Keyframe = colony.Save(colony.Recipe(1)) };
            ReplayRecorder recorder = ReplayRecorder.Begin(world);

            List<int> trees = TreesNear(colony, 12);
            Assume.That(trees.Count, Is.GreaterThanOrEqualTo(8), "the wooded board has trees near the start");
            Pawn first = colony.Pawns.Pawns.All[0];

            int end = KeyframeTick + RecordedTicks;
            while (world.CurrentTick < end)
            {
                int tick = world.CurrentTick;

                // Orders given with the clock running, drained by the tick.
                if (tick == KeyframeTick + 10)
                {
                    for (int i = 0; i < 4; i++) Fell(world, trees[i]);
                    PaintStore(colony);
                }

                // The paused frame: the player stops the clock, gives orders, and starts it again
                // — the game spends one tick to let the speed change through, which is an ordinary
                // tick the log covers like any other.
                if (tick == KeyframeTick + 2_000)
                {
                    world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
                    world.Tick();
                    for (int i = 4; i < 8; i++) Fell(world, trees[i]);
                    world.Intents.Submit(new Intent(IntentKind.SetWorkPriority, default,
                        first.Id.Value, WorkTypeIndex.Cutting, 1));
                    world.RepublishViews();
                    // A second paused frame, with a question only, and a refused order (off the board).
                    world.Intents.Submit(new Intent(IntentKind.QueryCell, colony.Start));
                    world.Intents.Submit(new Intent(IntentKind.Designate, new CellRef(-1, -1, 0),
                        (int)DesignationKind.Fell));
                    world.RepublishViews();
                    world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 3));
                    world.Tick();
                    continue;
                }

                world.Tick();
            }

            recorder.Seal(world, fullHash: true);
            recorder.Detach(world);

            // Through the file format, so a log that could not be read back fails here.
            using (var buffer = new MemoryStream())
            {
                recorder.Log.Write(buffer);
                buffer.Position = 0;
                recording.Log = ReplayLog.Read(buffer);
            }
            recording.Felled = colony.Jobs.CompletedOf(JobIndex.Fell);
            return recording;
        }

        static ColonyWorld LoadKeyframe(byte[] keyframe)
        {
            ColonyWorld colony = ColonyWorld.Build(Request());
            colony.Load(keyframe);
            return colony;
        }

        static void Fell(SimWorld world, int tree) =>
            world.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(tree), (int)DesignationKind.Fell));

        static void PaintStore(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            int anchor = -1;
            for (int dz = 0; dz < 3; dz++)
            for (int dx = 0; dx < 3; dx++)
            {
                int x = start.X + 3 + dx, z = start.Z + 3 + dz;
                if (!Size.Contains(x, z, start.Y)) continue;
                int cell = Size.Index(x, z, start.Y);
                if (anchor < 0) anchor = cell;
                colony.World.Intents.Submit(new Intent(IntentKind.DesignateStorage, Size.FromIndex(cell), anchor,
                    Odyssey.Sim.Storage.StoragePreset.Everything));
            }
        }

        static List<int> TreesNear(ColonyWorld colony, int radius)
        {
            var found = new List<(int distance, int cell)>();
            CellRef start = colony.Start;
            for (int z = Math.Max(0, start.Z - radius); z <= Math.Min(Size.SizeZ - 1, start.Z + radius); z++)
            for (int x = Math.Max(0, start.X - radius); x <= Math.Min(Size.SizeX - 1, start.X + radius); x++)
            {
                int cell = Size.Index(x, z, start.Y);
                if (colony.Designations.IsTree(cell))
                    found.Add((Math.Abs(x - start.X) + Math.Abs(z - start.Z), cell));
            }
            found.Sort();
            return found.ConvertAll(f => f.cell);
        }

        // ------------------------------------------------------------------ the claim

        [Test]
        public void ARecordedSessionReplaysToTheSameHashes()
        {
            Recording recording = PlayAndRecord();
            Assert.That(recording.Felled, Is.GreaterThan(0), "the colony did no work, so the replay proves nothing");
            Assert.That(recording.Log.Records.Exists(r => r.Paused), Is.True, "no order was given while paused");
            Assert.That(recording.Log.Checkpoints.Count, Is.GreaterThanOrEqualTo(10));

            ColonyWorld replay = LoadKeyframe(recording.Keyframe);
            ReplayResult result = Replayer.Run(replay.World, recording.Log);
            TestContext.WriteLine(result);

            Assert.That(result.StartMatches, Is.True, "the keyframe did not load as the colony that wrote it");
            Assert.That(result.ResultsDiffering, Is.Zero, result.FirstDifferingRecord);
            Assert.That(result.FirstDivergentTick, Is.EqualTo(-1), result.ToString());
            Assert.That(result.CheckpointsMatched, Is.EqualTo(recording.Log.Checkpoints.Count));
            Assert.That(result.EndFullMatches, Is.True, "the full hash, grid included, differs at the end");
            Assert.That(result.Matches, Is.True);
            Assert.That(replay.Jobs.CompletedOf(JobIndex.Fell), Is.EqualTo(recording.Felled));
        }

        /// <summary>
        /// <b>Control: one order dropped.</b> The fourth tree is never marked in the replay, so the
        /// colony does less work, and the replay must say so — at a checkpoint, not only at the end.
        /// </summary>
        [Test]
        public void AReplayMissingOneOrderIsCaught()
        {
            Recording recording = PlayAndRecord();
            int drop = recording.Log.Records.FindIndex(r => r.Intent.Kind == IntentKind.Designate && !r.Paused);
            Assume.That(drop, Is.GreaterThanOrEqualTo(0));
            recording.Log.Records.RemoveAt(drop);

            ReplayResult result = Replayer.Run(LoadKeyframe(recording.Keyframe).World, recording.Log);
            TestContext.WriteLine(result);

            Assert.That(result.Matches, Is.False);
            Assert.That(result.FirstDivergentTick, Is.GreaterThanOrEqualTo(0),
                "the light hash did not notice a missing order at any checkpoint");
        }

        /// <summary>
        /// <b>Control: one order moved.</b> A tree marked in the wrong place is the same number of
        /// orders, so only the hashes can tell.
        /// </summary>
        [Test]
        public void AReplayWithOneOrderMovedIsCaught()
        {
            Recording recording = PlayAndRecord();
            var records = recording.Log.Records;
            int move = records.FindLastIndex(r => r.Intent.Kind == IntentKind.Designate && r.Paused &&
                                                  r.Result == IntentRejection.None);
            Assume.That(move, Is.GreaterThanOrEqualTo(0));

            ColonyWorld probe = LoadKeyframe(recording.Keyframe);
            List<int> trees = TreesNear(probe, 20);
            int elsewhere = trees[trees.Count - 1];
            ReplayRecord r0 = records[move];
            records[move] = new ReplayRecord(r0.Tick, r0.Paused,
                new Intent(IntentKind.Designate, Size.FromIndex(elsewhere), (int)DesignationKind.Fell), r0.Result);

            ReplayResult result = Replayer.Run(LoadKeyframe(recording.Keyframe).World, recording.Log);
            TestContext.WriteLine(result);

            Assert.That(result.Matches, Is.False);
            Assert.That(result.FirstDivergentTick, Is.GreaterThanOrEqualTo(0));
        }

        // ------------------------------------------------------------------ the file and the cost

        [Test]
        public void TheLogRoundTripsThroughAFile()
        {
            var log = new ReplayLog
            {
                Seed = 7, Size = Size, FromTick = 100, EndTick = 900,
                StartFullHash = 0xDEADBEEFUL, EndFullHash = 0xFEEDFACEUL, EndLightHash = 42UL, Note = "a note",
            };
            log.Preamble.Add(new Intent(IntentKind.SetSliceLayer, default, 3));
            log.Records.Add(new ReplayRecord(101, false, new Intent(IntentKind.Designate, new CellRef(1, 2, 3), 1), IntentRejection.None));
            log.Records.Add(new ReplayRecord(400, true, new Intent(IntentKind.PlaceBuilding, new CellRef(4, 5, 6), 7, 8, 2), IntentRejection.NotPermitted));
            log.Checkpoints.Add(new ReplayCheckpoint(599, 123456789UL));

            string path = Path.Combine(Path.GetTempPath(), $"odyssey-replay-{Guid.NewGuid():N}.oyreplay");
            try
            {
                log.WriteToFile(path);
                ReplayLog back = ReplayLog.ReadFromFile(path);

                Assert.That(back.Seed, Is.EqualTo(log.Seed));
                Assert.That(back.Size, Is.EqualTo(log.Size));
                Assert.That(back.FromTick, Is.EqualTo(100));
                Assert.That(back.EndTick, Is.EqualTo(900));
                Assert.That(back.StartFullHash, Is.EqualTo(log.StartFullHash));
                Assert.That(back.EndFullHash, Is.EqualTo(log.EndFullHash));
                Assert.That(back.EndLightHash, Is.EqualTo(42UL));
                Assert.That(back.Note, Is.EqualTo("a note"));
                Assert.That(back.Preamble.Count, Is.EqualTo(1));
                Assert.That(back.Records, Is.EqualTo(log.Records));
                Assert.That(back.Checkpoints[0].LightHash, Is.EqualTo(123456789UL));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        /// <summary>
        /// <b>The light hash is what the recorder takes while somebody plays, so its cost is the
        /// cost of recording</b> (design 63 §4). Printed beside the full hash on the played board;
        /// the assertion is only that it is cheaper, because a number on this machine is not a
        /// number on the owner's. And it must still see what the checkpoints rely on: a pawn moved.
        /// </summary>
        [Test]
        public void TheLightHashIsCheaperThanTheFullOneAndStillSeesAPawn()
        {
            ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
            {
                Size = new GridSize(120, 120, 16),
                Seed = 1,
                Scenario = ScenarioDef.Playtest(),
                Barren = true,
                Wooded = true,
                Map = MapType.Natural,
            });
            SimWorld world = colony.World;
            world.Tick(200);

            const int reps = 20;
            world.ComputeLightHash();
            world.ComputeStateHash();
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < reps; i++) world.ComputeLightHash();
            double light = clock.Elapsed.TotalMilliseconds / reps;
            clock.Restart();
            for (int i = 0; i < reps; i++) world.ComputeStateHash();
            double full = clock.Elapsed.TotalMilliseconds / reps;
            TestContext.WriteLine($"played board: light hash {light:0.000} ms, full hash {full:0.000} ms " +
                                  $"(one every {ReplayLog.CheckpointInterval} ticks while recording)");

            Assert.That(light, Is.LessThan(full));

            ulong before = world.ComputeLightHash().Value;
            world.Tick();
            Assert.That(world.ComputeLightHash().Value, Is.Not.EqualTo(before),
                "a tick of five colonists moved nothing the light hash can see");
        }

        /// <summary>No recorder, no record: the hook costs a null check and writes nothing.</summary>
        [Test]
        public void AWorldWithNoRecorderRecordsNothing()
        {
            ColonyWorld colony = ColonyWorld.Build(Request());
            Assert.That(colony.World.ReplaySink, Is.Null);
            colony.World.Tick(10);
            Assert.That(colony.World.ReplaySink, Is.Null);
        }
    }
}
