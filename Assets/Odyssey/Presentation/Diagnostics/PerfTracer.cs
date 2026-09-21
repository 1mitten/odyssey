#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Odyssey.Hud.Diagnostics;
using Odyssey.Sim.Diagnostics;
using UnityEngine;

namespace Odyssey.Presentation.Diagnostics
{
    /// <summary>
    /// Records what the game costs while somebody plays it, a row a second, to a file.
    ///
    /// <para><b>Why this exists.</b> Every performance question in this project has been answered
    /// either by a five-to-fifteen-minute Unity batch run whose result is free text in a log, or
    /// by a screenshot of the developer overlay read by eye. Both are slow, neither has any
    /// history, and both report means — which cannot see the thing a player calls stutter. This
    /// closes the loop: the owner plays, hands over one path, and
    /// <c>tools/perf/trace.py</c> answers in seconds.</para>
    ///
    /// <para><b>It measures nothing.</b> Every number it writes is already a public property on
    /// the bootstrap, the renderer or the skirt, and the tick phases come from
    /// <see cref="PhaseTrace"/>, which has existed since the tick benchmark was written and has
    /// never had a consumer in the running game. That is deliberate and is the safeguard: a
    /// recorder that invented a figure of its own could become the next <c>CpuFrameMs</c> — the
    /// field that read 16.81 ms, then 296.32, then 17,898.04 on screen with nothing able to
    /// tell.</para>
    ///
    /// <para><b>Cost.</b> Per frame it is a dozen doubles into pre-grown arrays and no allocation.
    /// Per second it is one sort per percentile and one line of about a kilobyte, flushed. The
    /// phase sink is the only part that touches the simulation, and it is two
    /// <c>Stopwatch.GetTimestamp</c> calls a phase — which <c>PhaseTrace</c>'s own header already
    /// weighs, and which <c>TickBenchmarkTests.TimingATickCannotChangeIt</c> asserts is harmless
    /// to the result.</para>
    ///
    /// <para><b>Nothing here may throw into the game.</b> A diagnostic that takes the session down
    /// is worse than no diagnostic. An IO failure disables the tracer, records why, and the game
    /// carries on.</para>
    /// </summary>
    public sealed class PerfTracer : IDisposable
    {
        /// <summary>Seconds a row covers.</summary>
        public const double RowSeconds = 1d;

        /// <summary>
        /// A frame longer than this is always captured on its own, whatever the session's
        /// baseline. 50 ms is a fifth of a second lost at 4K — well past the point a player feels
        /// it and well past anything the frame budget contemplates.
        /// </summary>
        public const double SpikeFloorMs = 50d;

        /// <summary>
        /// And a frame this many times the previous second's median is captured too, so a smooth
        /// session's hitches are caught even when they stay under the floor.
        /// </summary>
        public const double SpikeMultiple = 3d;

        /// <summary>
        /// At most this many spikes a second. A stall that lasts does not need a thousand rows to
        /// describe it, and an unbounded writer during a pathological frame is how a diagnostic
        /// becomes the fault.
        /// </summary>
        public const int MaxSpikesPerRow = 4;

        readonly TraceWriter _writer;
        readonly TextWriter _sink;
        readonly FrameWindow _window;
        readonly TraceRow _row;
        readonly PhaseTrace _phases = new PhaseTrace();
        readonly TickSegment[] _segments;
        readonly double[] _spikeSplit;

        double _elapsed;
        double _rowElapsed;
        double _lastP50;
        int _spikesThisRow;
        int _markers;

        // Collection counts at the last row boundary and at the last frame. Two baselines, because
        // the two questions are different: a row wants "how many this second" and a spike wants
        // "did one happen on this very frame", and a per-second count cannot answer the second.
        int _rowGc0, _rowGc1, _rowGc2;
        int _frameGc0, _frameGc1, _frameGc2;
        int _rowProbes;

        /// <summary>Chunks re-meshed since the last row. An event, so it is summed, not sampled.</summary>
        int _remeshedThisRow;

        /// <summary>The file being written, for the overlay and the handover.</summary>
        public string Path { get; }

        /// <summary>False once an IO failure has retired it. <see cref="Fault"/> says why.</summary>
        public bool Active { get; private set; }

        /// <summary>Why tracing stopped, or null while it is running.</summary>
        public string? Fault { get; private set; }

        /// <summary>Rows written so far. The overlay shows it so "is it recording" has an answer.</summary>
        public int Rows { get; private set; }

        /// <summary>Markers written so far. On the overlay too, so a mark is seen to land.</summary>
        public int Marks => _markers;

        /// <summary>
        /// The sink to hand to <c>SimWorld.PhaseSink</c>. Null in every ordinary run, which is why
        /// attaching it is the tracer's business and detaching it is too.
        /// </summary>
        public ITickPhaseSink PhaseSink => _phases;

        public PerfTracer(string path, IReadOnlyList<string> sectionNames,
            IReadOnlyList<(string Key, string Value)> environment)
        {
            Path = path;

            _segments = (TickSegment[])Enum.GetValues(typeof(TickSegment));
            var phaseNames = new string[_segments.Length];
            for (int i = 0; i < _segments.Length; i++) phaseNames[i] = _segments[i].ToString();

            _window = new FrameWindow(sectionNames.Count);
            _row = new TraceRow(sectionNames.Count, _segments.Length);
            _spikeSplit = new double[sectionNames.Count];

            // Unbuffered-ish on purpose: the writer flushes every record, because a trace matters
            // most when the session ended badly and a buffered tail is what would be missing.
            _sink = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
            _writer = new TraceWriter(_sink, sectionNames, phaseNames);
            _writer.WriteHeader(environment);

            _rowGc0 = _frameGc0 = GC.CollectionCount(0);
            _rowGc1 = _frameGc1 = GC.CollectionCount(1);
            _rowGc2 = _frameGc2 = GC.CollectionCount(2);
            Active = true;
        }

        /// <summary>
        /// Open a tracer, or return null with the reason logged if the folder will not take one.
        /// Callers should not have to guard a diagnostic.
        /// </summary>
        public static PerfTracer? TryOpen(IReadOnlyList<string> sectionNames,
            IReadOnlyList<(string Key, string Value)> environment)
        {
            try
            {
                PerfTraceFiles.Prune();
                return new PerfTracer(PerfTraceFiles.PathForNow(), sectionNames, environment);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[perf] tracing is off: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Take one frame. Called from the bootstrap's <c>LateUpdate</c> after the frame's own
        /// timings have been read.
        /// </summary>
        public void Sample(double deltaSeconds, double frameMs, double gpuMs, double submitMs,
            double tickMs, ReadOnlySpan<double> sections, in FrameCounters counters)
        {
            if (!Active) return;

            _elapsed += deltaSeconds;
            _rowElapsed += deltaSeconds;
            _window.Add(frameMs, gpuMs, submitMs, tickMs, sections);

            // Read before the spike is written, so a slow frame is credited with the collection
            // that made it slow rather than with the next one.
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            int collectedThisFrame = (gc0 - _frameGc0) + (gc1 - _frameGc1) + (gc2 - _frameGc2);
            _frameGc0 = gc0;
            _frameGc1 = gc1;
            _frameGc2 = gc2;

            _remeshedThisRow += counters.Remeshed;

            if (IsSpike(frameMs))
                WriteSpike(frameMs, sections, counters.Tick, collectedThisFrame, counters.Remeshed);
            if (_rowElapsed >= RowSeconds) EmitRow(counters);
        }

        /// <summary>
        /// A frame is a spike if it is past the absolute floor, or several times the median of the
        /// second before it.
        ///
        /// <para>The median of the *previous* row and not a running one: a running median means
        /// sorting every frame, which would make the tracer cost more than several of the things
        /// it measures. A second of staleness costs nothing here — a session's baseline does not
        /// move in a second, and when it does, the row that captures the move is the interesting
        /// one anyway.</para>
        /// </summary>
        bool IsSpike(double frameMs)
        {
            if (_spikesThisRow >= MaxSpikesPerRow) return false;
            if (frameMs > SpikeFloorMs) return true;
            return _lastP50 > 0d && frameMs > _lastP50 * SpikeMultiple;
        }

        void WriteSpike(double frameMs, ReadOnlySpan<double> sections, int tick, int collections,
            int remeshed)
        {
            int n = sections.Length < _spikeSplit.Length ? sections.Length : _spikeSplit.Length;
            for (int i = 0; i < _spikeSplit.Length; i++) _spikeSplit[i] = i < n ? sections[i] : 0d;

            _spikesThisRow++;
            Guarded(() => _writer.WriteSpike(_elapsed, tick, frameMs, collections, remeshed, _spikeSplit));
        }

        void EmitRow(in FrameCounters counters)
        {
            _row.Clear();
            counters.WriteTo(_row);

            // Overwrite the last-seen value with the second's total. The counters struct cannot do
            // this itself: it describes one frame and knows nothing of the second around it.
            _row.Remeshed = _remeshedThisRow;
            _remeshedThisRow = 0;

            _row.AtSeconds = _elapsed;
            _row.Frames = _window.Frames;
            _row.FrameP50 = _window.FramePercentile(0.50);
            _row.FrameP95 = _window.FramePercentile(0.95);
            _row.FrameP99 = _window.FramePercentile(0.99);
            _row.FrameMax = _window.FrameMax;
            _row.GpuP50 = _window.GpuPercentile(0.50);
            _row.GpuMax = _window.GpuMax;
            _row.SubmitP50 = _window.SubmitPercentile(0.50);
            _row.SubmitMax = _window.SubmitMax;
            _row.TickP50 = _window.TickPercentile(0.50);
            _row.TickMax = _window.TickMax;
            _row.Over33 = _window.Over(33d);
            _row.Over50 = _window.Over(50d);

            for (int i = 0; i < _row.Sections.Length; i++) _row.Sections[i] = _window.SectionMean(i);

            // Collections in this second, and the heap after them. GetTotalMemory(false) does not
            // provoke a collection, which matters: a diagnostic that forced one to measure one
            // would be causing exactly the pause it is looking for.
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            _row.Gc0 = gc0 - _rowGc0;
            _row.Gc1 = gc1 - _rowGc1;
            _row.Gc2 = gc2 - _rowGc2;
            _rowGc0 = gc0;
            _rowGc1 = gc1;
            _rowGc2 = gc2;
            _row.HeapMb = GC.GetTotalMemory(false) / (1024d * 1024d);

            // The probe count arrives as a running total and leaves as this second's share.
            _row.Probes = counters.Probes - _rowProbes;
            _rowProbes = counters.Probes;

            for (int i = 0; i < _segments.Length; i++)
            {
                _row.PhaseMeanMs[i] = _phases.MeanMs(_segments[i]);
                _row.PhaseP95Ms[i] = _phases.P95Ms(_segments[i]);
                _row.PhaseMaxMs[i] = _phases.MaxMs(_segments[i]);
            }

            Guarded(() => _writer.WriteRow(_row));

            _lastP50 = _row.FrameP50;
            Rows++;
            _spikesThisRow = 0;
            _rowElapsed = 0d;
            _window.Reset();

            // The phase trace keeps raw samples, so a session of any length would grow without
            // this. Cleared here rather than on a cadence of its own so a row's phases cover
            // exactly the ticks its frames do.
            _phases.Clear();
        }

        /// <summary>
        /// Mark this moment. Returns the marker's number so the caller can say which one landed.
        /// </summary>
        public int Mark(string note)
        {
            if (!Active) return 0;

            _markers++;
            Guarded(() => _writer.WriteMarker(_markers, _elapsed, note));
            return _markers;
        }

        /// <summary>
        /// Run a write, and retire the tracer rather than letting a full disk end the session.
        /// </summary>
        void Guarded(Action write)
        {
            try
            {
                write();
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException
                                      || e is UnauthorizedAccessException)
            {
                Active = false;
                Fault = e.Message;
                Debug.LogWarning($"[perf] tracing stopped: {e.Message}");
            }
        }

        public void Dispose()
        {
            Active = false;
            try { _sink.Dispose(); }
            catch (IOException) { }
        }
    }
}
