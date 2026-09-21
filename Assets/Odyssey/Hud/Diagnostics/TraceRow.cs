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

        /// <summary>Frames over a 30 Hz tick, and over 50 ms. Counted, not inferred from the ranks.</summary>
        public int Over33;
        public int Over50;

        /// <summary>Mean milliseconds per frame, one per <c>OdysseyBootstrap.FrameSection</c>.</summary>
        public readonly double[] Sections;

        /// <summary>From <c>PhaseTrace</c>, one per <c>TickSegment</c>, over the ticks in this second.</summary>
        public readonly double[] PhaseMeanMs;
        public readonly double[] PhaseP95Ms;

        public int DrawCalls;
        public int Instances;
        public int Chunks;
        public int CellPlates;
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
            GpuP50 = GpuMax = SubmitP50 = TickP50 = 0d;
            Over33 = Over50 = 0;
            for (int i = 0; i < Sections.Length; i++) Sections[i] = 0d;
            for (int i = 0; i < PhaseMeanMs.Length; i++) PhaseMeanMs[i] = PhaseP95Ms[i] = 0d;
            DrawCalls = Instances = Chunks = CellPlates = Remeshed = Materials = SurroundBatches = 0;
            Figures = Pawns = Layer = 0;
            Gc0 = Gc1 = Gc2 = Probes = 0;
            HeapMb = 0d;
        }
    }
}
