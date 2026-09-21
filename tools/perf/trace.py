#!/usr/bin/env python3
"""Read the performance traces the game writes while somebody plays it.

A trace is JSONL, written by ``Odyssey.Presentation.Diagnostics.PerfTracer`` into
``Logs/perf/`` in the editor: one header object naming the machine, the board and every
graphics setting, then one row a second, plus a record for every frame that spiked and
every moment the player marked.

Why this tool exists.  Before it, every performance question in this project cost either
a five-to-fifteen-minute Unity batch run whose answer was free text in a log, or a
screenshot of the developer overlay read by eye.  Both report means, and a mean cannot
see the thing a player calls stutter: one frame in a hundred at four times the cost moves
a mean by three per cent and is the entire complaint.  This reads the ranks instead.

Commands
--------
    summarise <trace>          the header, the percentiles, the split, the spikes, the marks
    compare <a> <b>            two traces side by side, with the deltas
    list [folder]              the traces present, newest first

``compare`` refuses two traces whose headers disagree about the machine, the resolution,
the vsync setting or the board, unless ``--force`` is given.  That is ``docs/process.md``
made executable — *"a number in a doc names its machine and its date; a timing without
either is a rumour"* — and it is the guard against the mistake recorded in
``docs/design/06-rendering-and-camera.md`` §6c, where a canary drifted from 2.01 to
4.01 ms in an afternoon on nothing but what a sibling worktree was doing.

Standard library only, like every other tool in this repository, so it runs in a
container with no Unity and no third-party packages.
"""

from __future__ import annotations

import argparse, json, os, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_FOLDER = os.path.join(ROOT, "Logs", "perf")

# The header fields that must match before two traces may be compared. Not the whole
# header: the seed, the map and the time of day differ between any two sessions and say
# nothing about whether a millisecond means the same thing in both.
COMPARABLE = ("gpu", "cpu", "screen", "vsync", "frame_cap", "board", "editor")

# What `summarise` prints, and what `compare` diffs. Lower is better for every one, which
# is what lets the delta column carry a single arrow.
METRICS = (
    ("frame_p50", "frame p50"),
    ("frame_p95", "frame p95"),
    ("frame_p99", "frame p99"),
    ("frame_max", "frame max"),
    ("gpu_p50", "gpu p50"),
    ("gpu_max", "gpu max"),
    ("submit_p50", "submit p50"),
    ("tick_p50", "tick p50"),
)


class Trace:
    """One trace file, parsed."""

    def __init__(self, path: str) -> None:
        self.path = path
        self.header: dict = {}
        self.rows: list[dict] = []
        self.spikes: list[dict] = []
        self.marks: list[dict] = []
        self.broken = 0

        with open(path, "r", encoding="utf-8") as handle:
            for line in handle:
                line = line.strip()
                if not line:
                    continue
                try:
                    record = json.loads(line)
                except json.JSONDecodeError:
                    # A trace is routinely read while the game is still writing it, so the
                    # last line can be half a record. That is not corruption and must not
                    # stop the read; it is counted and reported.
                    self.broken += 1
                    continue

                kind = record.get("kind")
                if kind == "header":
                    self.header = record
                elif kind == "row":
                    self.rows.append(record)
                elif kind == "spike":
                    self.spikes.append(record)
                elif kind == "marker":
                    self.marks.append(record)

    @property
    def seconds(self) -> float:
        return self.rows[-1]["at"] if self.rows else 0.0

    @property
    def frames(self) -> int:
        return sum(int(row.get("frames", 0)) for row in self.rows)

    def series(self, field: str) -> list[float]:
        return [float(row[field]) for row in self.rows if field in row]

    def typical(self, field: str) -> float:
        """The median across rows.

        The median of per-second medians, not of frames: a row already ranked its own
        second, and a trace of a session that spent half its time paused should not have
        the paused half drag the figure down. Ranking rows keeps every second equal.
        """
        return median(self.series(field))

    def worst(self, field: str) -> float:
        values = self.series(field)
        return max(values) if values else 0.0

    def sections(self) -> list[tuple[str, float]]:
        """Mean milliseconds a frame per section, largest first."""
        names = [f for f in self.header.get("fields", []) if f.startswith("sect.")]
        out = [(name[5:], mean([float(r[name]) for r in self.rows if name in r])) for name in names]
        out.sort(key=lambda pair: pair[1], reverse=True)
        return out

    def phases(self) -> list[tuple[str, float, float]]:
        """The tick's own split: name, mean ms, p95 ms — largest mean first."""
        names = [f[6:-5] for f in self.header.get("fields", []) if f.startswith("phase.") and f.endswith(".mean")]
        out = []
        for name in names:
            avg = mean([float(r[f"phase.{name}.mean"]) for r in self.rows if f"phase.{name}.mean" in r])
            p95 = max([float(r[f"phase.{name}.p95"]) for r in self.rows if f"phase.{name}.p95" in r] or [0.0])
            out.append((name, avg, p95))
        out.sort(key=lambda row: row[1], reverse=True)
        return out

    def over(self, field: str) -> int:
        return sum(int(row.get(field, 0)) for row in self.rows)


def cell(value: float) -> str:
    """A figure, or ``n/a`` where nothing was recorded.

    A zero and "the platform declined to say" look identical in a column of numbers, and
    the second is the likelier of the two for the GPU time — ``FrameTimingManager`` gives
    nothing in a headless batch run.  Reporting that as ``0.00`` would read as "the GPU is
    free", which is exactly the wrong conclusion to hand somebody hunting a fill-bound
    frame.  The developer overlay makes the same distinction for the same reason, and the
    two must not disagree.
    """
    return f"{value:.2f}" if value > 0 else "n/a"


def median(values: list[float]) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    return ordered[len(ordered) // 2]


def mean(values: list[float]) -> float:
    return sum(values) / len(values) if values else 0.0


def commit() -> str:
    """The commit the repository is on, stamped onto every report.

    The game cannot know this and the report must: a measurement without the code it was
    taken against is the rumour ``docs/process.md`` warns of.
    """
    try:
        out = subprocess.run(["git", "-C", ROOT, "rev-parse", "--short", "HEAD"],
                             capture_output=True, text=True, timeout=10)
        return out.stdout.strip() or "unknown"
    except (OSError, subprocess.SubprocessError):
        return "unknown"


def newest(folder: str) -> str | None:
    traces = sorted(f for f in os.listdir(folder) if f.startswith("trace-") and f.endswith(".jsonl")) \
        if os.path.isdir(folder) else []
    return os.path.join(folder, traces[-1]) if traces else None


def resolve(path: str | None) -> str:
    """A path, a folder, or nothing at all — the last meaning the newest trace there is."""
    if path is None:
        found = newest(DEFAULT_FOLDER)
        if found is None:
            sys.exit(f"no traces in {DEFAULT_FOLDER}. Press Play with the game in the editor first.")
        return found
    if os.path.isdir(path):
        found = newest(path)
        if found is None:
            sys.exit(f"no traces in {path}")
        return found
    if not os.path.isfile(path):
        sys.exit(f"no such trace: {path}")
    return path


# ------------------------------------------------------------------ summarise

def summarise(path: str, top: int) -> int:
    trace = Trace(path)
    if not trace.rows:
        print(f"{os.path.basename(path)}: no complete rows yet "
              f"({trace.broken} partial). A row covers one second.")
        return 1

    print(f"== {os.path.basename(path)} ==")
    print(f"   {trace.seconds:.0f} s, {trace.frames} frames, {len(trace.rows)} rows, "
          f"repo at {commit()}")
    if trace.broken:
        print(f"   {trace.broken} unreadable line(s) — normal if the game is still writing")
    print()

    print("-- the session --")
    for key in ("started", "gpu", "cpu", "screen", "vsync", "frame_cap", "board", "map",
                "seed", "scatter", "surround", "tree_variants", "figure_cap", "unity", "editor"):
        if key in trace.header:
            print(f"   {key:<14} {trace.header[key]}")
    settings = [(k[4:], v) for k, v in trace.header.items() if k.startswith("gfx.")]
    if settings:
        print("   graphics       " + "  ".join(f"{k}={v}" for k, v in settings))
    print()

    print("-- the frame, in milliseconds --")
    print(f"   {'':<12} {'typical':>9} {'worst second':>13}")
    for field, label in METRICS:
        print(f"   {label:<12} {cell(trace.typical(field)):>9} {cell(trace.worst(field)):>13}")
    print()

    over33, over50 = trace.over("over_33"), trace.over("over_50")
    share = 100.0 * over33 / trace.frames if trace.frames else 0.0
    print(f"-- stutter --   {over33} frames over 33 ms ({share:.2f}% of {trace.frames}), "
          f"{over50} over 50 ms, {len(trace.spikes)} captured")
    print()

    print("-- where the submit went (mean ms a frame) --")
    for name, ms in trace.sections():
        if ms >= 0.005:
            print(f"   {name:<12} {ms:>8.3f}")
    print()

    phases = [row for row in trace.phases() if row[1] >= 0.0005]
    if phases:
        print("-- where the tick went (ms) --")
        print(f"   {'':<14} {'mean':>8} {'p95':>8}")
        for name, avg, p95 in phases:
            print(f"   {name:<14} {avg:>8.3f} {p95:>8.3f}")
        print()

    if trace.marks:
        print("-- what you marked --")
        for mark in trace.marks:
            print(f"   #{mark.get('n')} at {mark.get('at', 0):.0f}s  {mark.get('note', '')}")
            for row in around(trace, float(mark.get("at", 0)), 4.0):
                print(f"        {row['at']:>6.0f}s  p50 {row.get('frame_p50', 0):>6.2f}  "
                      f"p99 {row.get('frame_p99', 0):>6.2f}  max {row.get('frame_max', 0):>6.2f}")
        print()

    if trace.spikes:
        print(f"-- the worst {min(top, len(trace.spikes))} frames --")
        for spike in sorted(trace.spikes, key=lambda s: -float(s.get("frame_ms", 0)))[:top]:
            split = sorted(((k[5:], float(v)) for k, v in spike.items() if k.startswith("sect.")),
                           key=lambda pair: -pair[1])
            worst = "  ".join(f"{n} {v:.2f}" for n, v in split[:4] if v >= 0.005)
            print(f"   {spike.get('at', 0):>6.0f}s  {float(spike.get('frame_ms', 0)):>7.1f} ms   {worst}")
    return 0


def around(trace: Trace, at: float, window: float) -> list[dict]:
    """The rows either side of a moment, which is what a marker is actually asking about.

    A marker is pressed *after* the thing it marks — nobody reaches a key mid-hitch — so
    the window leans backwards: twice as much before as after.
    """
    return [r for r in trace.rows if at - window * 2 <= float(r.get("at", 0)) <= at + window]


# ------------------------------------------------------------------ compare

def compare(first: str, second: str, force: bool) -> int:
    a, b = Trace(first), Trace(second)
    if not a.rows or not b.rows:
        sys.exit("one of these traces has no complete rows")

    differences = [(key, a.header.get(key), b.header.get(key))
                   for key in COMPARABLE
                   if a.header.get(key) != b.header.get(key)]
    if differences and not force:
        print("These two were not taken under the same conditions, so comparing them would")
        print("be a lie about what changed:")
        for key, left, right in differences:
            print(f"   {key:<12} {left}   vs   {right}")
        print("\nRe-take one of them, or pass --force if you know why this is all right.")
        return 2

    print(f"== {os.path.basename(first)}  vs  {os.path.basename(second)} ==")
    print(f"   {a.seconds:.0f}s/{a.frames} frames   vs   {b.seconds:.0f}s/{b.frames} frames")
    if differences:
        print("   FORCED, and these differ: " + ", ".join(d[0] for d in differences))
    print()

    print(f"   {'':<12} {'A':>9} {'B':>9} {'delta':>9} {'':>8}")
    for field, label in METRICS:
        left, right = a.typical(field), b.typical(field)
        row(label, left, right)

    print()
    names = dict(a.sections())
    for name, right in b.sections():
        left = names.pop(name, 0.0)
        if max(left, right) >= 0.005:
            row("sect " + name, left, right)
    for name, left in names.items():
        if left >= 0.005:
            row("sect " + name, left, 0.0)

    print()
    share_a = 100.0 * a.over("over_33") / a.frames if a.frames else 0.0
    share_b = 100.0 * b.over("over_33") / b.frames if b.frames else 0.0
    row("over 33ms %", share_a, share_b, unit="%")
    return 0


def row(label: str, left: float, right: float, unit: str = "") -> None:
    delta = right - left
    # A per-cent move under one per cent is noise on this machine — §6c records the same
    # pass reading 1.57, 0.81 and 0.19 ms on contention alone — so it gets no arrow.
    share = (delta / left * 100.0) if left else 0.0
    mark = "" if abs(share) < 1.0 else ("  worse" if delta > 0 else "  better")
    print(f"   {label:<12} {left:>9.2f} {right:>9.2f} {delta:>+9.2f}{unit}{mark}")


# ------------------------------------------------------------------ list

def show_list(folder: str) -> int:
    if not os.path.isdir(folder):
        print(f"no trace folder at {folder}")
        return 1

    names = sorted((f for f in os.listdir(folder) if f.startswith("trace-")), reverse=True)
    if not names:
        print(f"no traces in {folder}")
        return 1

    for name in names:
        path = os.path.join(folder, name)
        try:
            trace = Trace(path)
        except OSError:
            continue
        when = time.strftime("%Y-%m-%d %H:%M", time.localtime(os.path.getmtime(path)))
        if trace.rows:
            print(f"{name}  {when}  {trace.seconds:>5.0f}s  "
                  f"p50 {trace.typical('frame_p50'):>6.2f}  p99 {trace.typical('frame_p99'):>6.2f}  "
                  f"{trace.header.get('screen', '?')}  {trace.header.get('board', '?')}")
        else:
            print(f"{name}  {when}  (no complete rows)")
    return 0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = parser.add_subparsers(dest="command")

    one = sub.add_parser("summarise", help="one trace, in full")
    one.add_argument("trace", nargs="?", help="a file, a folder, or nothing for the newest")
    one.add_argument("--top", type=int, default=8, help="how many spike frames to show")

    two = sub.add_parser("compare", help="two traces, side by side")
    two.add_argument("first")
    two.add_argument("second")
    two.add_argument("--force", action="store_true",
                     help="compare even though the two were taken under different conditions")

    listing = sub.add_parser("list", help="the traces present, newest first")
    listing.add_argument("folder", nargs="?", default=DEFAULT_FOLDER)

    args = parser.parse_args(argv)
    if args.command == "compare":
        return compare(resolve(args.first), resolve(args.second), args.force)
    if args.command == "list":
        return show_list(args.folder)
    return summarise(resolve(getattr(args, "trace", None)), args.top if args.command else 8)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
