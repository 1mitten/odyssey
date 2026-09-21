# 29 — The performance trace

**Written 2026-09-21.** What the game records about its own cost while somebody plays it, where it
goes, and how to read it. Owns `Assets/Odyssey/Hud/Diagnostics/`,
`Assets/Odyssey/Presentation/Diagnostics/` and `tools/perf/`.

## 1. Why this exists

On one day, three separate performance questions were asked and each was answered the hard way.

- **A batch run per question.** `FrameTimeTests` is the authoritative frame instrument and it takes
  five to fifteen minutes; its output is free text in `Logs/test-PlayMode.log` that a person greps.
  Four of them were run in a single afternoon to answer one question about the surround.
- **A screenshot channel.** The only readings at a resolution anybody plays at arrived as three
  photographs of the developer overlay, read by eye. They were enough to establish that the GPU is
  the frame at 4K and useless for anything about *variance*.
- **An instrument that was silently wrong.** `CpuFrameMs` shipped that morning, read 16.81 ms
  beside a 16.79 ms frame in the first shot, then 296.32, then 17,898.04. It had looked plausible
  in a batch run at 640 × 480. Nothing in the project could have told.
- **Everything is a mean.** `TimeFrames` aggregates a mean and a worst. Every number in
  `06-rendering-and-camera.md` came from it. **A mean cannot see stutter**: one frame in a hundred
  at four times the cost moves a mean by three per cent and is the entire complaint.

So: the game writes what it costs, a row a second, to a file; a tool reads it. The owner plays,
hands over one path, and the answer takes seconds.

## 2. What it is not

**It measures nothing.** Every number in a row is already a public property on `OdysseyBootstrap`,
`ChunkRenderer` or `TerrainSkirt`, and the tick's phase split comes from
`Odyssey.Sim.Diagnostics.PhaseTrace`, which has existed since the tick benchmark was written and
**had no consumer in the running game**. That is deliberate and it is the safeguard: a recorder
that invented a figure of its own could become the next `CpuFrameMs`.

It is also not a budget and not a gate. Nothing fails because a trace is slow. The one assertion
anywhere near it compares the trace against an independently-taken number (§7).

## 3. The shape

| Piece | Where | What |
|---|---|---|
| `FrameWindow` | `Odyssey.Hud/Diagnostics` | one second of frames, ranked |
| `TraceRow`, `FrameCounters` | `Odyssey.Hud/Diagnostics` | what a row carries |
| `TraceWriter` | `Odyssey.Hud/Diagnostics` | JSONL, one record a line |
| `PerfTracer` | `Odyssey.Presentation/Diagnostics` | samples the frame, owns the file, owns the phase sink |
| `PerfTraceFiles` | `Odyssey.Presentation/Diagnostics` | where a trace goes, and how many are kept |
| `tools/perf/trace.py` | — | `summarise`, `compare`, `list` |

The split is the assembly rule: `Odyssey.Hud` is UnityEngine-free, so the record shape, the ranking
and the serialisation are **proved by the fast tier**, and only the gathering needs Unity.

## 4. The decisions

### 4a. Percentiles, and the same ranking rule as the tick

A row carries frame **p50, p95, p99 and max**, plus counts of frames over 33 ms and over 50 ms. The
ranking is nearest rank on a sorted copy — *the same arithmetic as `PhaseTrace.Rank`*, copied
deliberately so a p95 in a trace and a p95 in a tick benchmark mean one thing. Two percentile
conventions in one repository is the "one rule, two owners" fault `docs/bug-patterns.md` opens
with, applied to arithmetic.

**Timings are ranked and counters are last-seen, and sections are meaned.** A frame time is an
experience and wants its distribution; a draw-call count is a fact about a moment and would mean
nothing averaged; a section is a cost, and the question asked of it is always "how much of the
frame", never "how bad did it get".

### 4b. JSONL, not CSV

Fields *will* be added — the sim-side counters are a later unit — and a positional format makes
every trace taken before the addition unreadable the day one arrives. The reader ignores fields it
does not know and tolerates fields missing from a row; both are tested. The bytes are not worth
counting: a row is under a kilobyte and there is one a second.

The JSON is hand-written. This assembly is `netstandard2.1` and UnityEngine-free, the values are
only numbers and short strings, and a serialiser would be a dependency for three escape rules —
the same trade `tools/icons/icons.py` makes when it vendors a PNG decoder rather than take Pillow.

**One record a line, always**, because a trace is routinely read while the game is still writing
it. A half-written last line is the normal case; the reader counts it and carries on.

### 4c. `Logs/perf/` in the editor, `perf/` beside a development player

`SaveFiles` writes to `Application.persistentDataPath` under a stated rule — "a playtest cannot
leave save files in a working tree" — which is about the player's own data. A trace is the opposite
kind of thing: it exists to be read by whoever is working on the repository, minutes after it is
written, and a path under `AppData\LocalLow\…` is a path somebody has to be told. `Logs/` is
gitignored and is already where every editor-side artefact goes (`one-day.txt`, `shot-*.png`,
`hud-*.png`). A built player has no repository and falls back to the persistent data path.

A **development player** writes beside its own executable — `Build/Win64/perf/` — for the same
reason, added the moment the first one was built: that build exists to be compared against the
editor, and `AppData\LocalLow\…` is a path somebody would have to be told. Only a shipped player,
which does not trace at all, falls through to the persistent data path.

Twenty traces are kept. A session is a few hundred kilobytes; the cap is about a readable folder,
not disk.

### 4d. Flushed every record

Not buffered and flushed on close. **A trace matters most when the session ended badly**, and a
buffered tail is exactly the part that would be missing then.

### 4e. Spikes are captured whole, not averaged into the second they interrupted

A frame over **50 ms**, or over **three times the previous second's median**, gets a record of its
own carrying that one frame's section split. Fifty milliseconds is a fifth of a second lost at 4K —
well past feeling it and well past anything the budget contemplates — and the multiple catches a
smooth session's hitches that stay under the floor.

The *previous* second's median and not a running one: a running median means sorting every frame,
which would make the tracer cost more than several of the things it measures. A second of staleness
costs nothing, because a session's baseline does not move in a second — and when it does, the row
that captures the move is the interesting one anyway.

**At most four spikes a second.** A stall that lasts does not need a thousand records to describe
it, and an unbounded writer during a pathological frame is how a diagnostic becomes the fault.

### 4f. On by default in the editor, off in a shipped player

The whole value is catching what does not reproduce, and a tracer that has to be switched on before
the interesting thing happens never is. It costs a dozen doubles a frame and a kilobyte a second,
and a released player has nobody to read it.

### 4g. The marker is a menu row, not a key

A marker wants to be reachable while something is going wrong, which argues for a binding. But a
binding is a `HotkeyAction`, and those are player controls that appear in the Keys tab and in the
wiki — **a developer's trace marker is not game content**, and the debug menu is exactly where
developer tools live. The timing is forgiving enough to afford it: the reader shows the seconds
either side of a mark and **leans the window backwards**, two seconds behind for every one ahead,
because nobody reaches anything mid-hitch anyway.

If marking after the fact turns out to be too late in practice, promoting it to a bindable action
is the next step and nothing here prevents it.

### 4h. The reader refuses an incomparable comparison

`compare` will not diff two traces whose headers disagree about the GPU, the CPU, the resolution,
the vsync setting, the frame cap, the board, or editor-versus-player. That is `docs/process.md` made
executable — *"a number in a doc names its machine and its date; a timing without either is a
rumour"* — and it is the guard against the mistake §6c records costing an afternoon, when a canary
drifted from 2.01 to 4.01 ms on nothing but what a sibling worktree was doing. `--force` is
available and says so in the output.

A move of under one per cent gets no verdict, for the same reason: the same pass has read 1.57,
0.81 and 0.19 ms on this machine on contention alone.

## 5. What a row carries

`at`, `tick`, `speed`, `frames` · `frame_p50/p95/p99/max` · `gpu_p50/max` · `submit_p50` ·
`tick_p50` · `over_33`, `over_50` · one `sect.<name>` per `OdysseyBootstrap.FrameSection` ·
`phase.<name>.mean` and `.p95` per `TickSegment` · `draw_calls`, `instances`, `chunks`,
`cell_plates`, `remeshed`, `materials`, `surround_batches`, `figures`, `pawns`, `layer`.

The header carries the machine, the screen, vsync, the frame cap, the board, the map, the seed, the
scatter density, the surround's three statics, the figure cap — and **every `GraphicsOption` and
`GraphicsLadder`, walked through `SettingsDirector.All` and `AllLadders`**, so a setting added later
appears in traces without anybody remembering to add it.

The **commit SHA is stamped by the reader**, not the game: the tool runs in the repository and the
game does not know it.

## 6. Reading one

```
python3 tools/perf/trace.py summarise          # the newest trace in Logs/perf
python3 tools/perf/trace.py summarise <path>
python3 tools/perf/trace.py compare <a> <b>
python3 tools/perf/trace.py list
```

`python3 -m unittest discover -s tools/perf -t tools/perf` — 14 tests, standard library only.

## 7. The tests, and which one matters

| Test | Tier | What it stops |
|---|---|---|
| `FrameWindowTests` | fast | the ranking being quietly wrong; a NaN reaching the JSON and making a whole trace unreadable |
| `TraceWriterTests.TheHeaderNamesEveryFieldARowCarries` | fast | **a field arriving undeclared** — the `CpuFrameMs` shape of fault |
| `TraceWriterTests` escaping / one-line / invariant number | fast | a record that breaks the line reader, or "16,7" on a comma-decimal machine |
| `test_trace.py` | python | the reader choking on a half-written line, or on a field it does not know |
| **`FrameTimeTests.TheTraceAgreesWithTheArmThatTimedIt`** | PlayMode | **the whole class of fault this line of work came from** |

That last one is the point. The trace and `TimeFrames` watch the same frames through different
clocks and their answers have to meet within a factor of two. The band is wide on purpose — one is
a mean over 180 frames, the other a median of per-second medians, and they are not the same
statistic — so what it catches is a tracer reading a different quantity, a different unit, or
nothing at all. It is this sentence as a test: **a number the platform hands you is not a
measurement until it has been seen beside a number taken independently.**

## 7a. What it found in its first two sessions

**Written the same day, because the point of the tool is what it settles and not what it is.**

### The first session: the surround work held, and stutter is not the mean

254 s at 3840 x 2160 on the Huge board. **p50 12.76 ms, p99 19.69** — a comfortable 78 fps — with
**95 frames over 33 ms, 0.53 per cent**. The complaint was entirely in that half a per cent, which
is precisely what a mean over 180 frames cannot see and what every number in §6c was until now.

The captured spikes said something immediately: during a **172 ms** frame, `World` was 4–6 ms and
`Surround` about 1, and the GPU peaked at 16 ms in the worst second. So the cost was **outside the
draw block and off the GPU**.

**One reading of it was wrong and is worth recording as such.** The raw spike times looked like a
rising trend and were called one — a leak. Bucketed properly they are *bursty*, 12, 2, 3, 17, 9, 9,
10, 1, 6 per thirty seconds, and p50 *improves* across the session from 14.82 to 11.87. There was no
leak. A list of timestamps read by eye is not a trend, and the tool now prints the buckets.

### The second session: both candidates eliminated

The fields that would name it were added — collections by generation, the heap, ambient probe
re-integrations, and whether the collector ran on a spiking frame — and 63 s of play settled both:

- **Garbage collection: out.** One gen-0, one gen-1 and one gen-2 collection in the whole session,
  and **zero of the eighteen spikes** had a collection on that frame.
- **The ambient probe: out.** Twenty-three re-integrations, and the seconds carrying one are mostly
  the seconds that did *not* spike.

What is left is the finding. On a 165 ms frame the tick is **0.22 ms**, every tick phase is under a
millisecond, the sections sum to about **7**, and the GPU peaked at **5.5**. Roughly **150 ms of
every spike is accounted for by nothing this game measures at all.** And every spike lands between
**159 and 174 ms** — that tight a cluster is the shape of a fixed blocking operation, not of a
variable workload.

So the reader gained an **unaccounted** line and an **elsewhere** column, computed rather than
recorded: it is a subtraction of numbers already present, and a recorder that wrote it would be
inventing a figure instead of reporting one, which is the rule this whole line of work keeps.

### The player build, which is where the answer probably is

A smoke run of the development player — 44 s, 14,480 frames, 120² at 2854 x 1440 — read **p50
2.50 ms** with **4 frames over 33 ms, 0.03 per cent**, and its only large frames were the first two
seconds of loading. **No 159–174 ms stalls at all.**

That is suggestive and not conclusive: the board and the resolution both differ from the editor
session, so it is not a controlled comparison. What makes it worth acting on is the *character* —
the editor's stalls are regular and clustered at one value, and the player has nothing of that
shape. The controlled run, same board and same settings in the player, is the open item.

## 8. What is deliberately not here

- **No regression record.** Wanted (the owner said so) and scoped out of the first pass. It arrives
  as `FrameTimeTests` writing these same rows, plus a `docs/perf/` file with a `--check` gate; the
  reader already handles it, because it would be the same format.
- **No sim counters beyond the tick phases.** `PhaseTrace` comes free. Job throughput, pathfinding
  and colony stalls are a later unit, and they are added fields — which the schema test exists to
  make safe.
- **No CSV export.** The owner reads the summaries, not the file. Keeping one format keeps it free
  to grow.
- **No automatic upload, no telemetry, nothing leaves the machine.**
