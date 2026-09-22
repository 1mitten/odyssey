#!/usr/bin/env python3
"""Tests for the trace reader.

Run: ``python3 -m unittest discover -s tools/perf -t tools/perf``

The reader is the half of the tracing loop that is not compiled by any of the three C#
tiers, so nothing else can prove it. What it mostly needs to prove is tolerance: a trace
is read *while the game is still writing it*, so half a last line is the normal case and
not corruption, and a field added later must not make every earlier trace unreadable.
"""

from __future__ import annotations

import io, json, os, sys, tempfile, unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import trace as tracetool  # noqa: E402


def header(**overrides) -> str:
    record = {
        "kind": "header", "schema": 1,
        "gpu": "A Card", "cpu": "A Chip", "screen": "3840x2160", "vsync": "1",
        "frame_cap": "-1", "board": "120x120x16", "editor": "yes", "started": "2026-09-21T11:00:00",
        "fields": ["at", "frame_p50", "frame_p95", "frame_p99", "frame_max",
                   "gpu_p50", "gpu_max", "submit_p50", "tick_p50", "frames",
                   "over_33", "over_50", "sect.World", "sect.Surround",
                   "phase.Pawns.mean", "phase.Pawns.p95"],
    }
    record.update(overrides)
    return json.dumps(record)


def row(at: float, p50: float, **overrides) -> str:
    record = {
        "kind": "row", "at": at, "frames": 60,
        "frame_p50": p50, "frame_p95": p50 * 1.1, "frame_p99": p50 * 1.4, "frame_max": p50 * 2,
        "gpu_p50": p50 / 2, "gpu_max": p50, "submit_p50": p50 / 3, "tick_p50": 0.2,
        "over_33": 0, "over_50": 0,
        "sect.World": 4.0, "sect.Surround": 0.5,
        "phase.Pawns.mean": 0.1, "phase.Pawns.p95": 0.3,
    }
    record.update(overrides)
    return json.dumps(record)


def write(lines: list[str]) -> str:
    handle = tempfile.NamedTemporaryFile("w", suffix=".jsonl", delete=False, encoding="utf-8")
    handle.write("\n".join(lines) + "\n")
    handle.close()
    return handle.name


class ReadingATrace(unittest.TestCase):
    def test_the_header_rows_spikes_and_marks_are_told_apart(self):
        path = write([
            header(),
            row(1, 16.0),
            row(2, 17.0),
            json.dumps({"kind": "spike", "at": 2.5, "tick": 9, "frame_ms": 80.0, "sect.World": 60.0}),
            json.dumps({"kind": "marker", "n": 1, "at": 3.0, "note": "it stuttered"}),
        ])
        trace = tracetool.Trace(path)

        self.assertEqual(trace.header["gpu"], "A Card")
        self.assertEqual(len(trace.rows), 2)
        self.assertEqual(len(trace.spikes), 1)
        self.assertEqual(len(trace.marks), 1)
        self.assertEqual(trace.frames, 120)
        self.assertEqual(trace.seconds, 2)

    def test_a_half_written_last_line_is_counted_not_fatal(self):
        """The normal case while the game is running, and it must not lose the rest."""
        path = write([header(), row(1, 16.0), '{"kind":"row","at":2,"frame_p'])
        trace = tracetool.Trace(path)

        self.assertEqual(len(trace.rows), 1)
        self.assertEqual(trace.broken, 1)

    def test_a_field_the_reader_does_not_know_is_ignored(self):
        """Fields will be added. An old reader must still read a new trace."""
        path = write([header(), row(1, 16.0, **{"sect.Nonesuch": 3.0, "jobs_started": 12})])
        trace = tracetool.Trace(path)

        self.assertEqual(len(trace.rows), 1)
        self.assertAlmostEqual(trace.typical("frame_p50"), 16.0)

    def test_a_field_missing_from_a_row_does_not_stop_the_read(self):
        path = write([header(), row(1, 16.0), json.dumps({"kind": "row", "at": 2, "frames": 60})])
        trace = tracetool.Trace(path)

        self.assertEqual(len(trace.rows), 2)
        self.assertAlmostEqual(trace.typical("frame_p50"), 16.0)


class TheFiguresItReports(unittest.TestCase):
    def test_typical_ranks_the_rows_rather_than_averaging_them(self):
        """A session that sat paused must not drag the figure it reports.

        Five seconds, one of them catastrophic. The mean would be 23.2 and describe no
        second that happened; the median is 16 and describes four of the five.
        """
        path = write([header()] + [row(i, ms) for i, ms in enumerate([16, 16, 16, 16, 52])])
        trace = tracetool.Trace(path)

        self.assertAlmostEqual(trace.typical("frame_p50"), 16.0)
        self.assertAlmostEqual(trace.worst("frame_p50"), 52.0)

    def test_sections_come_back_largest_first(self):
        path = write([header(), row(1, 16.0)])
        sections = tracetool.Trace(path).sections()

        self.assertEqual([name for name, _ in sections], ["World", "Surround"])
        self.assertAlmostEqual(sections[0][1], 4.0)

    def test_the_stutter_count_is_summed_over_the_whole_session(self):
        path = write([header(), row(1, 16.0, over_33=2, over_50=1), row(2, 16.0, over_33=3)])
        trace = tracetool.Trace(path)

        self.assertEqual(trace.over("over_33"), 5)
        self.assertEqual(trace.over("over_50"), 1)

    def test_a_marker_window_leans_backwards(self):
        """Nobody reaches a key mid-hitch, so what a marker asks about is mostly behind it."""
        path = write([header()] + [row(i, 16.0) for i in range(1, 21)])
        trace = tracetool.Trace(path)

        window = [r["at"] for r in tracetool.around(trace, at=10.0, window=2.0)]

        self.assertEqual(window, [6, 7, 8, 9, 10, 11, 12])


class ComparingTwoTraces(unittest.TestCase):
    def test_it_refuses_two_traces_from_different_conditions(self):
        a = write([header(screen="3840x2160"), row(1, 16.0)])
        b = write([header(screen="1920x1080"), row(1, 8.0)])

        out = io.StringIO()
        held, sys.stdout = sys.stdout, out
        try:
            code = tracetool.compare(a, b, force=False)
        finally:
            sys.stdout = held

        self.assertEqual(code, 2)
        self.assertIn("screen", out.getvalue())
        self.assertIn("3840x2160", out.getvalue())

    def test_force_compares_them_anyway_and_says_so(self):
        a = write([header(screen="3840x2160"), row(1, 16.0)])
        b = write([header(screen="1920x1080"), row(1, 8.0)])

        out = io.StringIO()
        held, sys.stdout = sys.stdout, out
        try:
            code = tracetool.compare(a, b, force=True)
        finally:
            sys.stdout = held

        self.assertEqual(code, 0)
        self.assertIn("FORCED", out.getvalue())

    def test_the_same_conditions_compare_without_complaint(self):
        a = write([header(), row(1, 16.0)])
        b = write([header(started="2026-09-21T12:00:00"), row(1, 12.0)])

        out = io.StringIO()
        held, sys.stdout = sys.stdout, out
        try:
            code = tracetool.compare(a, b, force=False)
        finally:
            sys.stdout = held

        self.assertEqual(code, 0)
        self.assertIn("better", out.getvalue(), "a 25% improvement went unremarked")

    def test_a_move_under_one_per_cent_is_not_called_a_change(self):
        """This machine reads the same pass at 1.57, 0.81 and 0.19 ms on contention alone."""
        out = io.StringIO()
        held, sys.stdout = sys.stdout, out
        try:
            tracetool.row("frame p50", 16.00, 16.05)
        finally:
            sys.stdout = held

        self.assertNotIn("worse", out.getvalue())


class Summarising(unittest.TestCase):
    def test_it_prints_the_session_the_ranks_and_the_spikes(self):
        path = write([
            header(),
            row(1, 16.0, over_33=1),
            row(2, 17.0),
            json.dumps({"kind": "spike", "at": 1.5, "tick": 9, "frame_ms": 84.0, "sect.World": 60.0}),
            json.dumps({"kind": "marker", "n": 1, "at": 2.0, "note": "here"}),
        ])

        out = io.StringIO()
        held, sys.stdout = sys.stdout, out
        try:
            code = tracetool.summarise(path, top=4)
        finally:
            sys.stdout = held
        text = out.getvalue()

        self.assertEqual(code, 0)
        self.assertIn("3840x2160", text)
        self.assertIn("frame p99", text)
        self.assertIn("84.0", text)
        self.assertIn("here", text)
        self.assertIn("World", text)

    def test_a_trace_with_no_complete_rows_says_so_rather_than_dividing_by_zero(self):
        path = write([header()])

        out = io.StringIO()
        held, sys.stdout = sys.stdout, out
        try:
            code = tracetool.summarise(path, top=4)
        finally:
            sys.stdout = held

        self.assertEqual(code, 1)
        self.assertIn("no complete rows", out.getvalue())


if __name__ == "__main__":
    unittest.main()
