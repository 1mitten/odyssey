#nullable enable

namespace Odyssey.Hud.Diagnostics
{
    /// <summary>
    /// What the world was during one frame: the counters a trace row reports as last-seen rather
    /// than ranked.
    ///
    /// <para>A struct passed by value, so the per-frame path allocates nothing. Verbose at its one
    /// call site on purpose: every field is named there, which is what makes it obvious when a new
    /// counter has been added to the renderer and not to the trace.</para>
    ///
    /// <para>These are facts about a moment, not measurements of an experience, which is why none
    /// of them is averaged or ranked. A draw-call count halfway through a second is the draw-call
    /// count; the mean of it over the second would be a number describing nothing.</para>
    /// </summary>
    public readonly struct FrameCounters
    {
        public FrameCounters(int tick, int speed, int drawCalls, int instances, int chunks,
            int cellPlates, int remeshed, int materials, int surroundBatches,
            int figures, int pawns, int layer, int probes)
        {
            Probes = probes;
            Tick = tick;
            Speed = speed;
            DrawCalls = drawCalls;
            Instances = instances;
            Chunks = chunks;
            CellPlates = cellPlates;
            Remeshed = remeshed;
            Materials = materials;
            SurroundBatches = surroundBatches;
            Figures = figures;
            Pawns = pawns;
            Layer = layer;
        }

        public readonly int Tick;
        public readonly int Speed;
        public readonly int DrawCalls;
        public readonly int Instances;
        public readonly int Chunks;
        public readonly int CellPlates;
        public readonly int Remeshed;
        public readonly int Materials;
        public readonly int SurroundBatches;
        public readonly int Figures;
        public readonly int Pawns;
        public readonly int Layer;

        /// <summary>Ambient probe re-integrations so far. The tracer turns it into a per-row delta.</summary>
        public readonly int Probes;

        /// <summary>Copy onto a row. Here rather than in the tracer so the field list has one owner.</summary>
        public void WriteTo(TraceRow row)
        {
            row.Tick = Tick;
            row.Speed = Speed;
            row.DrawCalls = DrawCalls;
            row.Instances = Instances;
            row.Chunks = Chunks;
            row.CellPlates = CellPlates;
            row.Remeshed = Remeshed;
            row.Materials = Materials;
            row.SurroundBatches = SurroundBatches;
            row.Figures = Figures;
            row.Pawns = Pawns;
            row.Layer = Layer;
            row.Probes = Probes;
        }
    }
}
