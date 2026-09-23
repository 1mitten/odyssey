# 31 — The aspect lookup

*Written 2026-09-23, straight out of `25-pawn-steering.md` §9d. Fixing the crowd scan removed one
O(N²) from the frame and left another standing in the same pass; this is that one. The frame context
is `06-rendering-and-camera.md` §6c.9, and the pattern is `docs/bug-patterns.md` P12.*

## 1. What it is

`WorldSnapshot.TryGetPawnAspect(pawn, key, out value)` walks every published aspect row looking for
one. Its own doc comment justified that, and the justification is worth quoting because it was
correct when written:

> *A scan, like `TryGetPawn` beside it. The published set is tens of rows on a real colony — sparse
> is the whole shape of `PawnAspect` — so an index would cost a dictionary per frame to save
> arithmetic that does not show up. A reader that wants every aspect of every pawn walks
> `PawnAspects` once instead of calling this in a loop.*

**Two of those three claims have since become false, and nothing told anybody.**

## 2. Measured, not reasoned

`AspectScaleTests`, fast tier, 2026-09-23: **a colonist publishes 57 aspect rows**, the same bundle
for every colonist, every tick. Not "tens". `PawnRegistry` publishes, per colonist:

| Rows | What |
|---|---|
| 4 × 12 | every skill's level, passion, experience and progress |
| 2 × *n* | every work type's priority and whether she is capable of it |
| 24 | the schedule, one row an hour |
| 1 | the roll seed she was generated from |
| 2 | her current work and move rates |
| 0–3 | what she is carrying |

**The schedule and the work priorities were both added after that comment was written**, and both
publish for every colonist unconditionally — for good reasons, given in their own comments (the Work
tab is a grid of everybody and could not open on a per-row subscription). Neither change was wrong.
What went unnoticed is that together they took the published set from tens of rows to
**57 × colonists**, which turns *every* call of `TryGetPawnAspect` into a scan whose length grows
with the colony, and every *per-colonist* call into a quadratic.

So the arithmetic at 384 colonists: 21,888 rows published, about 10,900 compares an average lookup,
once per far-form colonist per frame = **3.5 million compares a frame**. Against the residual
measured in §9d — 4.9 ms, flat at ~43 ns a pair — that is about 1.4 ns a compare, which is what a
linear walk of a packed struct array costs. The shape, the constant and the code path all agree.

**The third claim is still true and is the one to keep**: sparse *is* the shape of `PawnAspect`, and
a reader wanting every aspect of one pawn should still walk `PawnAspects` once. Nothing here changes
that advice; it changes what a single lookup costs for the 57 call sites that do not.

## 3. The decision: a lazy index on the snapshot, not a fix at the caller

Three candidates were considered.

**Rejected — publish the roll seed as a field on `PawnView`.** It would make the far-form renderer's
lookup free, and it is arguably the right shape for a per-pawn identity read every frame. But it
fixes exactly one caller out of fifty-seven, leaves the Work tab (five lookups per colonist per row)
and every future reader on the quadratic, and adds a second place a roll seed lives. The problem is
not the roll seed; it is the lookup.

**Rejected — build the index eagerly as rows are published.** One hash insert per row is cheap, but
it puts the cost inside the *tick*, which is the budget this project guards hardest, and it charges
every world that publishes aspects whether anything ever reads one. A headless golden run reads no
aspects at all.

**Taken — build the index lazily, on the first lookup of each published frame.** A snapshot nobody
queries pays nothing and the tick is untouched. A snapshot that is queried pays one O(rows) build
and then answers every later lookup in O(1). Since presentation queries the same snapshot many times
a frame, the build is amortised over hundreds of lookups.

The index is invalidated where `AspectCount` is reset, which is the one place a frame begins. It is
**auxiliary**: `PawnAspects` still hands back the rows in publish order, untouched, so the published
frame is byte-for-byte what it was and **no golden moves**.

### 3a. Two details that are easy to get wrong

- **Duplicates must resolve the way the scan did.** The scan returns the *first* matching row. If a
  contributor ever publishes the same `(pawn, key)` twice, the index must return the earlier row
  too, or a snapshot's answer would depend on whether anything had queried it yet. Insertion
  therefore keeps the first row it sees for a key and ignores later ones.
- **`Sim.Contracts` is UnityEngine-free**, and that is load-bearing (the fast tier compiles it, and
  the headless golden runs depend on it). No `Mathf`, no `Dictionary` on the hot path — an
  open-addressed table of `int` row indices over arrays the snapshot already owns and reuses.

## 4. What this does not do

- It does not change what is published, in what order, or the state hash.
- It does not make a per-colonist loop free — 57 call sites still each cost a hash probe. A reader
  that wants many aspects of one pawn should still walk `PawnAspects` once, and
  `TryGetPawnAspect`'s comment is corrected rather than deleted.
- **It does not raise the figure ceiling.** 64 stands.

## 5. Measured

`FrameTimeTests.TheAspectLookupCostsWhatItScans`, both arms alternated twice in one run on a clear
machine. Barren natural board, 640 × 480, RTX 5070 Ti, 2026-09-23.

| colony | rows published | frame, scan | frame, index | `Actors` | `Figures` |
|---|---|---|---|---|---|
| 64 | 3,648 | 3.606 ms | **3.142 ms** | 0.025 → 0.024 | 1.185 → 0.778 |
| 192 | 10,944 | 6.189 ms | **3.428 ms** | 1.076 → 0.344 | 2.638 → 0.754 |
| 384 | 21,888 | 14.202 ms | **4.107 ms** | 4.587 → 0.830 | 6.390 → 0.743 |

**`Figures` is now flat: 0.778, 0.754, 0.743 ms at 64, 192 and 384 colonists.** That is the figure
ceiling doing exactly what it was designed to do, visible for the first time. It never was: the cap
pins the number of *posed* figures at 64, but each of them was doing a lookup whose cost grew with
the whole colony, so the pass grew anyway. §9d of `25-pawn-steering.md` predicted this — it found
`Figures` growing linearly at `64 × kN` after the crowd cull and named the aspect scan as the
reason. The prediction is confirmed.

### 5a. The colony sweep, and the end of the knee

`TheFrameAgainstColonySize`, same run:

| pawns | 8 | 32 | 64 | 96 | 128 | 192 | 256 | 384 |
|---|---|---|---|---|---|---|---|---|
| frame | 2.20 | 2.61 | 3.21 | 3.37 | 3.53 | 3.75 | 4.99 | **4.82 ms** |

**There is no knee left.** The whole 48-fold range spans 2.2 to 5.0 ms, and the last two points are
within noise of each other. Set against the Play report this line of work came from — *"it seemed to
hover 1.7 ms no matter the colony size but then frames dropped after so many colonists"* — what is
left is the hover.

The journey at 384 colonists, all on a clear machine:

| | frame | `Actors` | `Figures` |
|---|---|---|---|
| before the crowd cull | 27.81 ms | 15.79 | 8.65 |
| after the crowd cull (`25-pawn-steering.md` §9) | 14.99 ms | 4.94 | 6.84 |
| after this | **4.82 ms** | **0.83** | **0.74** |

**Two quadratics, one pass, and only the second was visible once the first had gone.** That is the
thing to carry: the crowd scan was 3.5× the aspect scan, so until it was removed the aspect scan
looked like a constant. Neither was found by reading the code — both were found by measuring a
split and noticing that the growth had the wrong shape.
