#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Hud.Diagnostics;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The trace file's format, and the one guard that matters.
    ///
    /// <para><b>Why a schema test exists at all.</b> On 2026-09-21 a <c>CpuFrameMs</c> field was
    /// added to the developer overlay, looked plausible in a batch run at 640 x 480, and was wrong
    /// on screen in its first real session — 16.81 ms, then 296.32, then 17,898.04. Nothing could
    /// see it. A trace has the same hazard multiplied by its field count, and one of its two
    /// answers is <see cref="TheHeaderNamesEveryFieldARowCarries"/>: a field that appears in rows
    /// without being declared fails the fast tier, so a field can never arrive undocumented. The
    /// other answer is not here — it is the PlayMode test that checks the trace's own figure
    /// against one taken independently, because a *declared* field can still be nonsense.</para>
    ///
    /// <para>The format is JSONL — one header object, then one object a row — chosen because
    /// fields will be added (the sim-side counters are a later unit) and a positional format would
    /// make every old trace unreadable the day one was.</para>
    /// </summary>
    public class TraceWriterTests
    {
        static readonly string[] Sections = { "World", "Surround" };
        static readonly string[] Phases = { "Pawns", "Snapshot" };

        static TraceWriter WriterOver(StringWriter sink) => new TraceWriter(sink, Sections, Phases);

        static TraceRow SomeRow()
        {
            var row = new TraceRow(Sections.Length, Phases.Length)
            {
                AtSeconds = 12.5,
                Tick = 34412,
                Speed = 1,
                Frames = 60,
                FrameP50 = 16.7,
                FrameP95 = 18.1,
                FrameP99 = 24.0,
                FrameMax = 51.2,
                GpuP50 = 8.4,
                GpuMax = 9.9,
                SubmitP50 = 5.5,
                TickP50 = 0.19,
                Over33 = 2,
                Over50 = 1,
                DrawCalls = 3747,
                Instances = 131359,
                Chunks = 413,
                CellPlates = 0,
                Remeshed = 0,
                Materials = 23,
                SurroundBatches = 151,
                Figures = 3,
                Pawns = 8,
                Layer = 11,
            };
            row.Sections[0] = 4.46;
            row.Sections[1] = 0.98;
            row.PhaseMeanMs[0] = 0.12;
            row.PhaseP95Ms[0] = 0.31;
            return row;
        }

        // ------------------------------------------------------------------ the schema guard

        /// <summary>
        /// Every key a row writes is declared in the header, and every declared field is written.
        ///
        /// <para><b>This is the load-bearing test in the file.</b> It is the check the overlay did
        /// not have: it fails the moment somebody adds a number to a row and does not say what it
        /// is, and it fails equally when a field is declared and quietly stops being written,
        /// which is the shape a removed instrument leaves behind.</para>
        /// </summary>
        [Test]
        public void TheHeaderNamesEveryFieldARowCarries()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            writer.WriteHeader(new (string, string)[] { ("gpu", "a card") });
            writer.WriteRow(SomeRow());

            string[] lines = Lines(sink);
            IReadOnlyList<string> declared = ParseStringList(lines[0], "fields");
            List<string> written = ParseKeys(lines[1]);

            Assert.That(written, Is.EquivalentTo(declared),
                "a row key is undeclared, or a declared field was not written");
        }

        [Test]
        public void TheDeclaredFieldsIncludeEverySectionAndPhaseByName()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            writer.WriteHeader(Array.Empty<(string, string)>());

            IReadOnlyList<string> declared = ParseStringList(Lines(sink)[0], "fields");

            Assert.That(declared, Contains.Item("sect.World"));
            Assert.That(declared, Contains.Item("sect.Surround"));
            Assert.That(declared, Contains.Item("phase.Pawns.mean"));
            Assert.That(declared, Contains.Item("phase.Snapshot.p95"));
        }

        // ------------------------------------------------------------------ the format

        [Test]
        public void TheHeaderIsTheFirstLineAndCarriesTheSchemaVersion()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            writer.WriteHeader(new (string, string)[] { ("board", "120x120x16") });

            string header = Lines(sink)[0];

            Assert.That(header, Does.StartWith("{"));
            Assert.That(header, Does.Contain($"\"schema\":{TraceWriter.SchemaVersion}"));
            Assert.That(header, Does.Contain("\"kind\":\"header\""));
            Assert.That(header, Does.Contain("\"board\":\"120x120x16\""));
        }

        [Test]
        public void ARowRoundTripsThroughTheText()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            writer.WriteHeader(Array.Empty<(string, string)>());
            writer.WriteRow(SomeRow());

            string row = Lines(sink)[1];

            Assert.That(row, Does.Contain("\"kind\":\"row\""));
            Assert.That(Number(row, "frame_p50"), Is.EqualTo(16.7).Within(1e-6));
            Assert.That(Number(row, "gpu_p50"), Is.EqualTo(8.4).Within(1e-6));
            Assert.That(Number(row, "sect.Surround"), Is.EqualTo(0.98).Within(1e-6));
            Assert.That(Number(row, "over_50"), Is.EqualTo(1).Within(1e-9));
            Assert.That(Number(row, "draw_calls"), Is.EqualTo(3747).Within(1e-9));
        }

        /// <summary>
        /// One object a line and no line breaks inside one, because the reader is a line reader
        /// and a trace is routinely read while the game is still writing it.
        /// </summary>
        [Test]
        public void EveryRecordIsExactlyOneLine()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            writer.WriteHeader(new (string, string)[] { ("note", "two\nlines\tand a \"quote\"") });
            writer.WriteRow(SomeRow());
            writer.WriteMarker(1, 3.25, "it stuttered here");

            string[] lines = Lines(sink);

            Assert.That(lines.Length, Is.EqualTo(3));
            foreach (string line in lines)
            {
                Assert.That(line, Does.StartWith("{"));
                Assert.That(line, Does.EndWith("}"));
            }
        }

        [Test]
        public void AwkwardTextIsEscapedRatherThanBreakingTheLine()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            writer.WriteHeader(new (string, string)[] { ("gpu", "a \"card\"\\ with\nbreaks") });

            string header = Lines(sink)[0];

            Assert.That(header, Does.Contain("\\\""));
            Assert.That(header, Does.Contain("\\\\"));
            Assert.That(header, Does.Contain("\\n"));
        }

        /// <summary>
        /// A marker is what the owner presses when something felt wrong, so it carries its own
        /// kind: the reader lists markers separately and a row must never be mistaken for one.
        /// </summary>
        [Test]
        public void AMarkerSaysWhatItIsAndWhenItWas()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            writer.WriteMarker(3, 41.5, "the hitch");

            string line = Lines(sink)[0];

            Assert.That(line, Does.Contain("\"kind\":\"marker\""));
            Assert.That(Number(line, "n"), Is.EqualTo(3).Within(1e-9));
            Assert.That(Number(line, "at"), Is.EqualTo(41.5).Within(1e-6));
            Assert.That(line, Does.Contain("\"note\":\"the hitch\""));
        }

        /// <summary>
        /// A spike is one frame, not a second of them, and it says so — otherwise the reader would
        /// average it into the window it interrupted, which is the opposite of the point.
        /// </summary>
        [Test]
        public void ASpikeIsItsOwnKindAndCarriesTheSplitOfTheOneFrame()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            var split = new double[] { 12.0, 0.5 };
            writer.WriteSpike(atSeconds: 9.0, tick: 100, frameMs: 62.5, collections: 1, remeshed: 4,
                sections: split);

            string line = Lines(sink)[0];

            Assert.That(line, Does.Contain("\"kind\":\"spike\""));
            Assert.That(Number(line, "frame_ms"), Is.EqualTo(62.5).Within(1e-6));
            Assert.That(Number(line, "sect.World"), Is.EqualTo(12.0).Within(1e-6));
        }

        /// <summary>
        /// Numbers are written with the invariant culture. On a machine with a comma decimal
        /// separator a row would otherwise be both unreadable and, worse, *readable wrongly* —
        /// "16,7" parses as two values in anything that splits on commas.
        /// </summary>
        [Test]
        public void NumbersAreWrittenWithADecimalPoint()
        {
            var sink = new StringWriter();
            TraceWriter writer = WriterOver(sink);
            writer.WriteRow(SomeRow());

            Assert.That(Lines(sink)[0], Does.Contain("16.7"));
        }

        // ------------------------------------------------------------------ a small JSON reader

        static string[] Lines(StringWriter sink) =>
            sink.ToString().Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

        static List<string> ParseKeys(string line)
        {
            var keys = new List<string>();
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != '"') continue;
                int end = ClosingQuote(line, i + 1);
                string text = Unescape(line.Substring(i + 1, end - i - 1));
                // A key is a string immediately followed by a colon; a value never is.
                if (end + 1 < line.Length && line[end + 1] == ':') keys.Add(text);
                i = end;
                // Skip the value, so a string value cannot be mistaken for the next key.
                if (keys.Count > 0 && i + 1 < line.Length && line[i + 1] == ':')
                {
                    int j = i + 2;
                    if (j < line.Length && line[j] == '"') i = ClosingQuote(line, j + 1);
                }
            }
            keys.Remove("kind");
            return keys;
        }

        static int ClosingQuote(string line, int from)
        {
            for (int i = from; i < line.Length; i++)
            {
                if (line[i] == '\\') { i++; continue; }
                if (line[i] == '"') return i;
            }
            throw new FormatException("unterminated string in " + line);
        }

        static string Unescape(string text) => text
            .Replace("\\n", "\n").Replace("\\t", "\t")
            .Replace("\\\"", "\"").Replace("\\\\", "\\");

        static IReadOnlyList<string> ParseStringList(string line, string key)
        {
            int at = line.IndexOf("\"" + key + "\":[", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"no \"{key}\" list in {line}");
            int open = line.IndexOf('[', at);
            int close = line.IndexOf(']', open);
            string body = line.Substring(open + 1, close - open - 1);

            var items = new List<string>();
            foreach (string part in body.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length >= 2) items.Add(Unescape(trimmed.Substring(1, trimmed.Length - 2)));
            }
            return items;
        }

        static double Number(string line, string key)
        {
            int at = line.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"no \"{key}\" in {line}");
            int from = at + key.Length + 3;
            int to = from;
            while (to < line.Length && (char.IsDigit(line[to]) || line[to] == '.' || line[to] == '-')) to++;
            return double.Parse(line.Substring(from, to - from),
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
