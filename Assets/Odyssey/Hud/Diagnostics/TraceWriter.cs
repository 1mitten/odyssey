#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Odyssey.Hud.Diagnostics
{
    /// <summary>
    /// Writes a performance trace: one header object, then one object per record, one to a line.
    ///
    /// <para><b>Why JSONL and not CSV.</b> Fields will be added — the sim-side counters are a
    /// later unit and the schema test below exists precisely so they can be — and a positional
    /// format makes every trace taken before the addition unreadable the day one arrives. Naming
    /// each value costs bytes nobody is counting: a row is under a kilobyte and there is one a
    /// second.</para>
    ///
    /// <para><b>Why the JSON is hand-written.</b> This assembly is <c>netstandard2.1</c> and
    /// UnityEngine-free by assembly definition, and the values are only numbers and short strings.
    /// A serialiser would be a dependency for three escape rules. The project has the same habit
    /// elsewhere — <c>tools/icons/icons.py</c> vendors a PNG decoder rather than take Pillow.</para>
    ///
    /// <para><b>One line per record, always.</b> A trace is routinely read while the game is still
    /// writing it, and the reader is a line reader; an embedded newline would make the tail of a
    /// live trace parse as garbage rather than as a truncated last line.</para>
    ///
    /// <para>It takes a <see cref="TextWriter"/> rather than a path so the fast tier can prove it
    /// against a <see cref="StringWriter"/>, which is the only reason any of this is testable
    /// without Unity.</para>
    /// </summary>
    public sealed class TraceWriter
    {
        /// <summary>
        /// Bumped whenever a field changes meaning — not when one is added, which the reader
        /// tolerates by design.
        /// </summary>
        public const int SchemaVersion = 1;

        /// <summary>The fixed fields, in the order a row writes them.</summary>
        static readonly string[] FixedFields =
        {
            "at", "tick", "speed", "frames",
            "frame_p50", "frame_p95", "frame_p99", "frame_max",
            "gpu_p50", "gpu_max", "submit_p50", "submit_max", "tick_p50", "tick_max",
            "over_33", "over_50",
            "draw_calls", "instances", "chunks", "cell_plates", "remeshed", "materials",
            "surround_batches", "figures", "pawns", "layer",
            "gc0", "gc1", "gc2", "heap_mb", "probes",
        };

        readonly TextWriter _sink;
        readonly string[] _sectionNames;
        readonly string[] _phaseNames;
        readonly string[] _fields;
        readonly StringBuilder _line = new StringBuilder(1024);

        public TraceWriter(TextWriter sink, IReadOnlyList<string> sectionNames,
            IReadOnlyList<string> phaseNames)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _sectionNames = Copy(sectionNames);
            _phaseNames = Copy(phaseNames);

            var fields = new List<string>(FixedFields);
            foreach (string section in _sectionNames) fields.Add("sect." + section);
            foreach (string phase in _phaseNames)
            {
                fields.Add("phase." + phase + ".mean");
                fields.Add("phase." + phase + ".p95");
                fields.Add("phase." + phase + ".max");
            }
            _fields = fields.ToArray();
        }

        static string[] Copy(IReadOnlyList<string> names)
        {
            var copy = new string[names.Count];
            for (int i = 0; i < names.Count; i++) copy[i] = names[i];
            return copy;
        }

        /// <summary>
        /// Every field a row carries, in write order. Declared in the header so a reader never has
        /// to know this class, and asserted against an actual row by the fast tier.
        /// </summary>
        public IReadOnlyList<string> Fields => _fields;

        /// <summary>
        /// The first line: the schema, the field list, and whatever the caller knows about the
        /// machine and the world.
        ///
        /// <para>The environment is a caller-supplied list rather than a type, because what is
        /// worth recording is Unity's business and this assembly cannot see Unity. What it is
        /// <em>for</em> is the rule in <c>docs/process.md</c> — "a number in a doc names its
        /// machine and its date; a timing without either is a rumour" — which the reader enforces
        /// by refusing to compare two traces whose headers disagree.</para>
        /// </summary>
        public void WriteHeader(IReadOnlyList<(string Key, string Value)> environment)
        {
            _line.Clear();
            _line.Append("{\"kind\":\"header\",\"schema\":").Append(SchemaVersion);

            for (int i = 0; i < environment.Count; i++)
            {
                _line.Append(',');
                Text(environment[i].Key, environment[i].Value);
            }

            _line.Append(",\"fields\":[");
            for (int i = 0; i < _fields.Length; i++)
            {
                if (i > 0) _line.Append(',');
                _line.Append('"');
                Escape(_fields[i]);
                _line.Append('"');
            }
            _line.Append("]}");
            Emit();
        }

        public void WriteRow(TraceRow row)
        {
            _line.Clear();
            _line.Append("{\"kind\":\"row\"");

            Number("at", row.AtSeconds);
            Number("tick", row.Tick);
            Number("speed", row.Speed);
            Number("frames", row.Frames);
            Number("frame_p50", row.FrameP50);
            Number("frame_p95", row.FrameP95);
            Number("frame_p99", row.FrameP99);
            Number("frame_max", row.FrameMax);
            Number("gpu_p50", row.GpuP50);
            Number("gpu_max", row.GpuMax);
            Number("submit_p50", row.SubmitP50);
            Number("submit_max", row.SubmitMax);
            Number("tick_p50", row.TickP50);
            Number("tick_max", row.TickMax);
            Number("over_33", row.Over33);
            Number("over_50", row.Over50);
            Number("draw_calls", row.DrawCalls);
            Number("instances", row.Instances);
            Number("chunks", row.Chunks);
            Number("cell_plates", row.CellPlates);
            Number("remeshed", row.Remeshed);
            Number("materials", row.Materials);
            Number("surround_batches", row.SurroundBatches);
            Number("figures", row.Figures);
            Number("pawns", row.Pawns);
            Number("layer", row.Layer);
            Number("gc0", row.Gc0);
            Number("gc1", row.Gc1);
            Number("gc2", row.Gc2);
            Number("heap_mb", row.HeapMb);
            Number("probes", row.Probes);

            for (int i = 0; i < _sectionNames.Length; i++)
                Number("sect." + _sectionNames[i], At(row.Sections, i));

            for (int i = 0; i < _phaseNames.Length; i++)
            {
                Number("phase." + _phaseNames[i] + ".mean", At(row.PhaseMeanMs, i));
                Number("phase." + _phaseNames[i] + ".p95", At(row.PhaseP95Ms, i));
                Number("phase." + _phaseNames[i] + ".max", At(row.PhaseMaxMs, i));
            }

            _line.Append('}');
            Emit();
        }

        static double At(double[] values, int i) => i < values.Length ? values[i] : 0d;

        /// <summary>
        /// A moment the player marked, because something felt wrong and a menu is not reachable
        /// while it is happening.
        /// </summary>
        public void WriteMarker(int number, double atSeconds, string note)
        {
            _line.Clear();
            _line.Append("{\"kind\":\"marker\"");
            Number("n", number);
            Number("at", atSeconds);
            _line.Append(',');
            Text("note", note);
            _line.Append('}');
            Emit();
        }

        /// <summary>
        /// One frame that went badly, with its own split.
        ///
        /// <para>Its own kind, and not folded into the row it interrupted, because averaging a
        /// spike into the second around it is exactly what a mean does and exactly what this trace
        /// exists to stop.</para>
        /// </summary>
        public void WriteSpike(double atSeconds, int tick, double frameMs, int collections,
            int remeshed, IReadOnlyList<double> sections)
        {
            _line.Clear();
            _line.Append("{\"kind\":\"spike\"");
            Number("at", atSeconds);
            Number("tick", tick);
            Number("frame_ms", frameMs);
            // Whether the collector ran on this very frame. The one fact that separates "a pause
            // we caused" from "a pause the runtime imposed", and it is per frame because a
            // per-second count cannot say which frame wore it.
            Number("gc", collections);
            // And whether it meshed a chunk. The same question as the collection count and asked
            // for the same reason: a per-second total cannot say which frame wore the cost.
            Number("remeshed", remeshed);
            for (int i = 0; i < _sectionNames.Length; i++)
                Number("sect." + _sectionNames[i], i < sections.Count ? sections[i] : 0d);
            _line.Append('}');
            Emit();
        }

        void Emit()
        {
            // '\n' and not WriteLine: the reader splits on it, and a CRLF machine would otherwise
            // leave a stray carriage return on the end of every record's last value.
            _sink.Write(_line.ToString());
            _sink.Write('\n');

            // Flushed every record on purpose. A trace matters most when the session ended badly,
            // and a buffered tail is the part that would be missing exactly then.
            _sink.Flush();
        }

        void Number(string key, double value)
        {
            _line.Append(",\"");
            Escape(key);
            _line.Append("\":");

            // Invariant, always. On a comma-decimal machine "16,7" is not merely unreadable, it is
            // readable *wrongly* by anything that splits on commas.
            if (double.IsNaN(value) || double.IsInfinity(value)) _line.Append('0');
            else _line.Append(value.ToString("0.####", CultureInfo.InvariantCulture));
        }

        void Text(string key, string value)
        {
            _line.Append('"');
            Escape(key);
            _line.Append("\":\"");
            Escape(value);
            _line.Append('"');
        }

        void Escape(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"': _line.Append("\\\""); break;
                    case '\\': _line.Append("\\\\"); break;
                    case '\n': _line.Append("\\n"); break;
                    case '\r': _line.Append("\\r"); break;
                    case '\t': _line.Append("\\t"); break;
                    default:
                        if (c < ' ') _line.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _line.Append(c);
                        break;
                }
            }
        }
    }
}
