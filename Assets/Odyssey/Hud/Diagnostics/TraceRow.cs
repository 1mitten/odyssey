#nullable enable

namespace Odyssey.Hud.Diagnostics
{
    /// <summary>
    /// One second of a performance trace: what the frames cost, and what the world was while they
    /// cost it.
    ///
    /// <para>A class rather than a struct, and reused by the tracer rather than rebuilt, because
    /// it carries three arrays and is written once a second for as long as somebody plays.</para>
    ///
    /// <para><b>Timings are ranked and counters are last-seen, and the difference is the point.</b>
    /// A frame time is an experience and wants its distribution; a draw-call count is a fact about
    /// the world at the moment the row was written and would mean nothing averaged. Sections sit
    /// between the two and are meaned: a section is a cost, and the question asked of it is always
    /// "how much of the frame", never "how bad did it get".</para>
    ///
    /// <para>Every field here must be named in <see cref="TraceWriter.Fields"/>, and
    /// <c>TraceWriterTests.TheHeaderNamesEveryFieldARowCarries</c> fails if one is not. That is
    /// the guard against the fault that produced this whole line of work: a number appearing on a
    /// readout with nothing able to say whether it meant anything.</para>
    /// </summary>
    public sealed class TraceRow
    {
        public TraceRow(int sections, int phases)
        {
            Sections = new double[sections];
            PhaseMeanMs = new double[phases];
            PhaseP95Ms = new double[phases];
            PhaseMaxMs = new double[phases];
        }

        /// <summary>Seconds since the trace was opened. The trace's own clock, not the world's.</summary>
        public double AtSeconds;

        public int Tick;
        public int Speed;

        /// <summary>Frames in this second. Its own field because every rank below is taken over it.</summary>
        public int Frames;

        public double FrameP50;
        public double FrameP95;

        /// <summary>The frame a player notices. Nothing in this project measured it before.</summary>
        public double FrameP99;

        public double FrameMax;

        public double GpuP50;
        public double GpuMax;
        public double SubmitP50;
        public double TickP50;

        /// <summary>
        /// The worst single tick and the worst single frame's submission in this second.
        ///
        /// <para><b>Added 2026-09-21, third time of asking, and the pattern is the lesson.</b>
        /// The frame has carried p50, p95, p99 and max since the first line of this class, because
        /// the whole argument for the trace was that a mean cannot see stutter. The tick was then
        /// given a <em>median</em> and its phases a mean and a p95, and nothing else — so a single
        /// 150 ms tick among the hundred and eighty in a second at 3x speed sits at the 99.4th
        /// percentile and is invisible in every column. The same blind spot as the last-seen
        /// re-mesh counter, wearing different clothes.</para>
        ///
        /// <para>It matters because <b>the tick is not inside any <c>FrameSection</c></b>: it runs
        /// before the draw block, so an expensive one lands in the part of the frame the reader
        /// calls "elsewhere" — which is exactly where the 146-175 ms stalls were found to be.
        /// </para>
        /// </summary>
        public double TickMax;
        public double SubmitMax;

        /// <summary>Frames over a 30 Hz tick, and over 50 ms. Counted, not inferred from the ranks.</summary>
        public int Over33;
        public int Over50;

        /// <summary>Mean milliseconds per frame, one per <c>OdysseyBootstrap.FrameSection</c>.</summary>
        public readonly double[] Sections;

        /// <summary>From <c>PhaseTrace</c>, one per <c>TickSegment</c>, over the ticks in this second.</summary>
        public readonly double[] PhaseMeanMs;
        public readonly double[] PhaseP95Ms;

        /// <summary>
        /// And the worst single tick each phase had. <c>PhaseTrace</c> has offered this since it
        /// was written; the trace simply never asked for it.
        /// </summary>
        public readonly double[] PhaseMaxMs;

        public int DrawCalls;
        public int Instances;
        public int Chunks;
        public int CellPlates;

        /// <summary>
        /// Chunks re-meshed <b>during this second</b>, summed over its frames.
        ///
        /// <para><b>A sum, and it was a last-seen counter for one day, which made it a liar.</b>
        /// Every other counter here is a fact about a moment and is rightly the last value seen —
        /// a draw-call count halfway through a second is the draw-call count. Re-meshing is not
        /// like that: it is an *event*, it happens on a handful of frames a second at most, and
        /// taking the final frame's value reports zero for a second in which eight hundred chunks
        /// were rebuilt. On 2026-09-21 that zero was quoted three times as evidence that meshing
        /// was not behind a 150 ms stall, which it could not possibly have shown.</para>
        ///
        /// <para>The rule it leaves: <b>a counter of events is summed, a counter of state is
        /// last-seen</b>, and which one a field is has to be decided when it is added.</para>
        /// </summary>
        public int Remeshed;
        public int Materials;
        public int SurroundBatches;

        public int Figures;
        public int Pawns;
        public int Layer;

        /// <summary>
        /// Garbage collections in this second, by generation, and the heap after them.
        ///
        /// <para><b>Added 2026-09-21, the day the trace was written, because its first real
        /// session could not answer its own question.</b> A 254-second play at 4K carried 95 frames
        /// over 33 ms, and their section splits were <em>ordinary</em> — World 4-6 ms, Surround
        /// about 1, against frames of 172 ms. So the cost was neither the draw block nor the GPU
        /// (which peaked at 16 ms), and the trace could say where it was not and not where it was.
        /// A collection pause is the leading candidate precisely because it is outside every
        /// section this class measures, and because the hitches grew more frequent as the session
        /// went on, which is what a filling heap looks like.</para>
        ///
        /// <para>Deltas, not totals: what is wanted is "did one happen here", and a running total
        /// makes the reader do subtraction to find out.</para>
        /// </summary>
        public int Gc0;
        public int Gc1;
        public int Gc2;
        public double HeapMb;

        /// <summary>
        /// Ambient probe re-integrations in this second.
        ///
        /// <para>The other candidate for a pause outside the draw block, and the one the daylight
        /// cycle's own comment already nominates: "the second is the only real cost in the cycle,
        /// so it is the number to watch if the sky is ever suspected of being expensive". It runs
        /// in <c>Update</c>, which no <c>FrameSection</c> covers.</para>
        /// </summary>
        public int Probes;

        /// <summary>Wipe the counters so a row cannot quietly repeat the second before it.</summary>
        public void Clear()
        {
            AtSeconds = 0d;
            Tick = Speed = Frames = 0;
            FrameP50 = FrameP95 = FrameP99 = FrameMax = 0d;
            GpuP50 = GpuMax = SubmitP50 = TickP50 = TickMax = SubmitMax = 0d;
            Over33 = Over50 = 0;
            for (int i = 0; i < Sections.Length; i++) Sections[i] = 0d;
            for (int i = 0; i < PhaseMeanMs.Length; i++)
                PhaseMeanMs[i] = PhaseP95Ms[i] = PhaseMaxMs[i] = 0d;
            DrawCalls = Instances = Chunks = CellPlates = Remeshed = Materials = SurroundBatches = 0;
            Figures = Pawns = Layer = 0;
            Gc0 = Gc1 = Gc2 = Probes = 0;
            HeapMb = 0d;
        }
    }
}
