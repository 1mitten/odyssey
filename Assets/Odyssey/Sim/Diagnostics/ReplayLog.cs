#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Diagnostics
{
    /// <summary>
    /// Something that wants every applied intent and the end of every tick. Attached to
    /// <see cref="SimWorld.ReplaySink"/>, which is null in every ordinary run.
    ///
    /// <para>The contract is <see cref="ITickHashSink"/>'s: read-only and one-way. A sink that
    /// touched the world would change the thing it is recording, and the replay it later failed
    /// would be its own fault (design 63 §3a).</para>
    /// </summary>
    public interface IReplaySink
    {
        /// <summary>
        /// An intent has just been applied, or refused, at <paramref name="tick"/>.
        /// <paramref name="paused"/> is true when it came through
        /// <see cref="SimWorld.RepublishViews"/> rather than a tick's own drain — an order given
        /// while the clock was stopped, which a replay must apply the same way.
        /// </summary>
        void Applied(int tick, bool paused, in Intent intent, IntentRejection result);

        /// <summary><paramref name="tick"/> has finished running. The counter has not moved yet.</summary>
        void TickEnded(SimWorld world, int tick);
    }

    /// <summary>One applied intent, stamped with when and how it was applied.</summary>
    public readonly struct ReplayRecord : IEquatable<ReplayRecord>
    {
        public readonly int Tick;
        public readonly bool Paused;
        public readonly Intent Intent;
        public readonly IntentRejection Result;

        public ReplayRecord(int tick, bool paused, Intent intent, IntentRejection result)
        {
            Tick = tick;
            Paused = paused;
            Intent = intent;
            Result = result;
        }

        public bool Equals(ReplayRecord o) =>
            Tick == o.Tick && Paused == o.Paused && Result == o.Result &&
            Intent.Kind == o.Intent.Kind && Intent.Cell.Equals(o.Intent.Cell) &&
            Intent.A == o.Intent.A && Intent.B == o.Intent.B && Intent.C == o.Intent.C;

        public override bool Equals(object? obj) => obj is ReplayRecord r && Equals(r);
        public override int GetHashCode() => HashCode.Combine(Tick, Intent.Kind, Intent.A, Intent.B);
        public override string ToString() => $"@{Tick}{(Paused ? " paused" : "")} {Intent} -> {Result}";
    }

    /// <summary>A light hash taken at the end of a tick (the tick that produced the state).</summary>
    public readonly struct ReplayCheckpoint
    {
        public readonly int Tick;
        public readonly ulong LightHash;

        public ReplayCheckpoint(int tick, ulong lightHash)
        {
            Tick = tick;
            LightHash = lightHash;
        }
    }

    /// <summary>
    /// What a replay needs beside its keyframe: every order applied since, stamped with its tick,
    /// and the light hashes the live colony passed through, so a replay can tell whether it went
    /// where the colony went (design 63 §3–§4, HR0).
    ///
    /// <para><b>The start is the keyframe's tick</b> — the world was saved between ticks with
    /// <see cref="SimWorld.CurrentTick"/> equal to <see cref="FromTick"/>, and nothing had run
    /// at that tick yet. <b>The end</b> is the tick the recorder stopped at, with the same meaning:
    /// every tick before <see cref="EndTick"/> has run.</para>
    ///
    /// <para><b>The preamble</b> is the view state the world was in when recording began —
    /// the slice, the speed and the standing questions — which the save does not carry because
    /// none of it is simulation state. It is replayed first so that the published frames, which
    /// some lazy passes read, ask the same questions they asked live.</para>
    /// </summary>
    public sealed class ReplayLog
    {
        /// <summary>"OYRP" little-endian.</summary>
        const uint Magic = 0x5052594Fu;
        public const int FormatVersion = 1;

        /// <summary>
        /// How often the live colony's light hash is kept. A game hour is 2,500 ticks; 600 is ten
        /// seconds at normal speed, fine enough to bisect a divergence to a quarter of an hour of
        /// game time without costing the frame anything a player could feel.
        /// </summary>
        public const int CheckpointInterval = 600;

        public uint Seed { get; set; }
        public GridSize Size { get; set; }
        public int FromTick { get; set; }
        public int EndTick { get; set; }

        /// <summary>The full state hash at the keyframe, taken once when recording began. 0 = not taken.</summary>
        public ulong StartFullHash { get; set; }

        /// <summary>The full state hash at <see cref="EndTick"/>, taken once when recording stopped. 0 = not taken.</summary>
        public ulong EndFullHash { get; set; }

        /// <summary>The light hash at <see cref="EndTick"/>, taken at every write. 0 = not taken.</summary>
        public ulong EndLightHash { get; set; }

        /// <summary>A free-text note: the build, the machine, the date. Not read by anything.</summary>
        public string Note { get; set; } = string.Empty;

        public List<Intent> Preamble { get; } = new List<Intent>();
        public List<ReplayRecord> Records { get; } = new List<ReplayRecord>();
        public List<ReplayCheckpoint> Checkpoints { get; } = new List<ReplayCheckpoint>();

        // ------------------------------------------------------------------ the file

        public void Write(Stream stream)
        {
            using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            w.Write(Magic);
            w.Write(FormatVersion);
            w.Write(Seed);
            w.Write(Size.SizeX);
            w.Write(Size.SizeZ);
            w.Write(Size.SizeY);
            w.Write(FromTick);
            w.Write(EndTick);
            w.Write(StartFullHash);
            w.Write(EndFullHash);
            w.Write(EndLightHash);
            w.Write(Note);

            w.Write(Preamble.Count);
            foreach (var intent in Preamble) WriteIntent(w, intent);

            w.Write(Records.Count);
            foreach (var r in Records)
            {
                w.Write(r.Tick);
                w.Write(r.Paused);
                w.Write((byte)r.Result);
                WriteIntent(w, r.Intent);
            }

            w.Write(Checkpoints.Count);
            foreach (var c in Checkpoints)
            {
                w.Write(c.Tick);
                w.Write(c.LightHash);
            }
        }

        public static ReplayLog Read(Stream stream)
        {
            using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (r.ReadUInt32() != Magic) throw new InvalidDataException("not a replay log");
            int version = r.ReadInt32();
            if (version != FormatVersion)
                throw new InvalidDataException($"replay log version {version}, this build reads {FormatVersion}");

            var log = new ReplayLog
            {
                Seed = r.ReadUInt32(),
            };
            int sx = r.ReadInt32(), sz = r.ReadInt32(), sy = r.ReadInt32();
            log.Size = new GridSize(sx, sz, sy);
            log.FromTick = r.ReadInt32();
            log.EndTick = r.ReadInt32();
            log.StartFullHash = r.ReadUInt64();
            log.EndFullHash = r.ReadUInt64();
            log.EndLightHash = r.ReadUInt64();
            log.Note = r.ReadString();

            int preamble = r.ReadInt32();
            for (int i = 0; i < preamble; i++) log.Preamble.Add(ReadIntent(r));

            int records = r.ReadInt32();
            for (int i = 0; i < records; i++)
            {
                int tick = r.ReadInt32();
                bool paused = r.ReadBoolean();
                var result = (IntentRejection)r.ReadByte();
                log.Records.Add(new ReplayRecord(tick, paused, ReadIntent(r), result));
            }

            int checkpoints = r.ReadInt32();
            for (int i = 0; i < checkpoints; i++)
                log.Checkpoints.Add(new ReplayCheckpoint(r.ReadInt32(), r.ReadUInt64()));
            return log;
        }

        public void WriteToFile(string path)
        {
            // Written beside and moved over, so a reader never sees half a log and a crash mid-write
            // leaves the previous one standing.
            string temp = path + ".tmp";
            using (var file = File.Create(temp)) Write(file);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public static ReplayLog ReadFromFile(string path)
        {
            using var file = File.OpenRead(path);
            return Read(file);
        }

        static void WriteIntent(BinaryWriter w, in Intent intent)
        {
            w.Write((int)intent.Kind);
            w.Write(intent.Cell.X);
            w.Write(intent.Cell.Z);
            w.Write(intent.Cell.Y);
            w.Write(intent.A);
            w.Write(intent.B);
            w.Write(intent.C);
        }

        static Intent ReadIntent(BinaryReader r)
        {
            var kind = (IntentKind)r.ReadInt32();
            var cell = new CellRef(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            return new Intent(kind, cell, r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
        }
    }

    /// <summary>
    /// The live half: attached to a world at a keyframe, it writes down every order applied and a
    /// light hash every <see cref="ReplayLog.CheckpointInterval"/> ticks.
    ///
    /// <para><b>Cost while playing.</b> One null check per intent and per tick when detached. When
    /// attached, a list append per intent and one <see cref="SimWorld.ComputeLightHash"/> every
    /// 600 ticks, which skips the cell grid and is measured by <c>ReplayTests</c>.</para>
    /// </summary>
    public sealed class ReplayRecorder : IReplaySink
    {
        public ReplayLog Log { get; }

        ReplayRecorder(ReplayLog log) => Log = log;

        /// <summary>
        /// Begin recording a world that stands at a keyframe: call this between ticks, straight
        /// after the keyframe has been written, and before another intent can be applied.
        /// Attaches itself. <paramref name="fullHashAtStart"/> costs a full hash (about 10 ms on
        /// the played board), so the caller chooses.
        /// </summary>
        public static ReplayRecorder Begin(SimWorld world, bool fullHashAtStart = true)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var log = new ReplayLog
            {
                Seed = world.Seed,
                Size = world.Size,
                FromTick = world.CurrentTick,
                EndTick = world.CurrentTick,
                StartFullHash = fullHashAtStart ? world.ComputeStateHash().Value : 0UL,
            };
            log.Preamble.AddRange(ViewPreamble(world));
            var recorder = new ReplayRecorder(log);
            world.ReplaySink = recorder;
            return recorder;
        }

        /// <summary>
        /// The view questions the world is standing on, as the intents that would ask them again.
        /// None of it is simulation state and none of it is saved, but the snapshot publish reads it
        /// and at least one lazy pass runs from the publish (design 28 §12a), so a replay that
        /// published a different view could in principle do different work.
        /// </summary>
        static IEnumerable<Intent> ViewPreamble(SimWorld world)
        {
            var v = world.Views;
            yield return new Intent(IntentKind.SetSliceLayer, default, v.SliceLayer);
            yield return new Intent(IntentKind.SetGameSpeed, default, world.GameSpeed);
            yield return new Intent(IntentKind.WatchPower, default, v.WatchPower ? 1 : 0);
            yield return new Intent(IntentKind.WatchHome, default, v.WatchHome ? 1 : 0);
            yield return v.QueryCell >= 0
                ? new Intent(IntentKind.QueryCell, world.Size.FromIndex(v.QueryCell))
                : new Intent(IntentKind.QueryCell, default, -1);
            yield return new Intent(IntentKind.QueryShot, default, v.QueryShotShooter, v.QueryShotTarget);
        }

        public void Applied(int tick, bool paused, in Intent intent, IntentRejection result) =>
            Log.Records.Add(new ReplayRecord(tick, paused, intent, result));

        public void TickEnded(SimWorld world, int tick)
        {
            Log.EndTick = tick + 1;
            if ((tick + 1) % ReplayLog.CheckpointInterval == 0)
                Log.Checkpoints.Add(new ReplayCheckpoint(tick, world.ComputeLightHash().Value));
        }

        /// <summary>
        /// Bring the end of the log up to the world as it stands, between ticks. The light hash is
        /// cheap; the full one costs about 10 ms on the played board and is for the final write.
        /// </summary>
        public void Seal(SimWorld world, bool fullHash)
        {
            Log.EndTick = world.CurrentTick;
            Log.EndLightHash = world.ComputeLightHash().Value;
            Log.EndFullHash = fullHash ? world.ComputeStateHash().Value : 0UL;
        }

        /// <summary>Stop recording. The log stays readable.</summary>
        public void Detach(SimWorld world)
        {
            if (ReferenceEquals(world.ReplaySink, this)) world.ReplaySink = null;
        }
    }
}
