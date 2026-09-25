# Bug patterns

**What this is for.** `docs/lessons.md` records operational lessons — the things that waste an hour
of *tooling* time. This file records the **bugs themselves**: what the symptom looked like, what it
actually was, how it was found, and the check that would catch the next one of its kind. It exists
because this project keeps meeting the *same three or four faults wearing different clothes*, and a
session that recognises the pattern fixes in an hour what took a day the first time.

**Read the patterns first, then the register.** If a report matches a pattern, go straight to the
measurement that pattern names — do not start by reading code.

**Add to it whenever a bug is fixed.** One row in the register, and a new pattern only when the fault
genuinely does not fit an existing one.

---

## The patterns

### P1 — One rule, two owners

**By far the most common fault in this codebase.** Two pieces of code answer the same question,
agree today, and drift apart tomorrow. It always fails *silently*, because each copy is correct on
its own terms and no test that exercises one exercises the other.

Every occurrence so far:

| The question | The two owners | How it showed |
|---|---|---|
| What does a hop cost? | cell search, region graph, mover | pinned by `HopPriceHasOneOwnerTests` |
| Which face is a ladder on? | `ChunkMesher.EmitLadder` (fell back north), `TryWallBeside` (fell back to nothing) | figure climbed through the air |
| Does this thing rotate? | the Defs, and `BuildShapes.Rotates` | R did the other of its two jobs, silently |
| Which cells does a bed claim? | the simulation's guard, and the ghost | ghost drew legal, click did nothing |
| What layer does a run land on? | `Place`'s per-cell lift, and `DrawRunGhosts` (no lift at all) | a deck with a hole in it |
| **Does a floor hide what is beneath it?** | `ChunkMesher.EmitScatter` (asks), `SurfaceContributor` (never asked) | **rubble drawn through a wooden deck, chased for three sessions** |
| Is this cell the foot of a terrace step? | `BankLayout` (render mirror), `TerraceFoot` (cell grid) | allowed on purpose: different data, pinned cell-by-cell by `TerraceFootTests` |
| **When is a colonist starving?** | `AlertModel.StarveAt` (120, of 1000), `AlertWatch.StarveThreshold` (12, of a scale that does not exist) | **the alert chime fired at 1.2% food instead of 12%, which is to say never — and its three unit tests all passed, because they fed the watcher literal numbers rather than a published pawn** |
| **What colour is this order?** | `HudTheme.PinnedActionHue` (the chip), four `Color` constants in `OdysseyBootstrap` (the board) | **deconstruct was orange on the panel and the cancel red on the ground for months; the board's copy is in an assembly the fast tier does not compile, and the Unity-tier test asserted only that the mapping was total** |
| **How big is the board?** | `BuildSession`'s chunk grid and render model (the inspector's), the colony request (the setup page's) | **silent for as long as nothing wrote to the chunk grid during a build; the first write threw out of bounds with the bounds check passing, because it asked the cell grid** |

**The fix is always the same**: name one owner, make every other site *ask* it, and write a test that
walks both. Never restate the rule "just here"; never answer a disagreement by changing one copy.

**The trap:** an assembly boundary (ADR 0003) forces some tables to be duplicated — `BuildShapes`,
`BuildLabels`. Those are allowed, but only with a test that walks the copy against the original
(`BuildShapesAgreementTests`, `RegistryTests`).

### P2 — The rule asks the built world, and misses the order

A rule that reads `_grid.Edifice` / `_grid.Floor` sees only what **exists**. Sites — blueprints —
are invisible to it, so two individually legal orders combine into an illegal building.

**The tell is the word "sometimes"** in a report. If the outcome depends on which job a colonist
happened to pick up, suspect this immediately.

**The fix**: the rule consults sites as well as built things (`LadderHereOrOrdered`,
`FloorHereOrOrdered`), *and* is asked again at the moment of truth in `Raise`, because a rule that
spans two separately-ordered cells can be invalidated after it was checked.

**Do not re-ask the whole of `Allows` at `Raise`** — a site with its own material hauled to it fails
`needsClearCell`, so that would refuse every bed whose wood had been delivered. Re-ask the one rule
that can go stale.

### P3 — A compatibility clause that keeps the bug alive

A clause kept so old data still works ("a real floor still counts") silently preserves exactly the
behaviour a new rule was written to forbid. New ones cannot be made; the old ones go on misbehaving.

**Ask what it is actually holding up, by measurement, before paying to migrate.** The ladder clause
was believed to require worldgen changes, a golden re-bake and a save break. Measured: **all three
golden masters were byte-identical**, because worldgen's ladders reach the nav as `StampedConnector`s
and never consult the rule at all. The expensive migration did not exist.

### P4 — A conditional rule applied per cell across a dragged run

A lift, a snap or a default that is *conditional* will resolve differently for different cells of one
drag, splitting a single gesture across two layers or two states. The player gave one order and got
two outcomes.

**The fix**: resolve it **once per run** and apply that answer to every cell, and make the preview ask
the same owner. See `ConstructionGrid.RunLayerFor`.

**Every existing construction test placed one cell by hand**, which is exactly why none of them could
see it. When a mechanic has a drag gesture, test the *drag*.

### P5 — The symptom is in presentation, the cause is one layer away

"It looks wrong" reports are ambiguous between the cell, the data and the drawing. Before hunting a
renderer bug, establish **which of the three** is wrong, because the fix lives in a different file for
each.

The cheap discriminators, in order:

1. **Do the pane and the renderer read the same field?** If yes, they cannot disagree about a cell,
   and the thing on screen is a *different cell* than the one being described.
2. **Does the mirror track the grid?** Drive `WorldRenderModel` directly and print grid-vs-mirror
   after each edit (`StaleFloorProbe`). Staleness is then proven or excluded in one run.
3. **Does the art resolve?** A per-cell fallback (`SlabModuleFor` → group slab) can draw one material
   two ways. Check the catalogue has real art for the material before blaming anything else.

**And when all three come back clean, read what they proved.** They exclude the *drawing*; they say
nothing about what the player actually ordered. Three sessions ran these, got three clean answers,
and invented a renderer fault anyway (P6).

### P6 — Excluding every way it could go wrong, without asking whether it went wrong

A report is assumed and then defended. Every mechanism that could corrupt the thing is excluded, one
by one, correctly — and the conclusion drawn is that the mechanism must be more exotic, rather than
that **the thing was never what the report called it**.

The tell is a clean exclusion that nobody turns around. "A wood floor cannot draw as stone" is also
"a cell drawing as stone is not a wood floor", and the second reading ends the hunt in a minute. The
grey tile cost three sessions to the first reading.

**The fix is a habit, not a check: name the player's action, and go and read it.** The owner's save
files hold the order, the material and the kind of every cell, and they were on the same disk for the
whole of that hunt. `tools/dotnet/Odyssey.SaveProbe` loads one without Unity and prints them.

**Reach for the file before the code** whenever a report says a thing "is" something — a wood floor,
a stone tile, the top level. That is the player's name for what they see, not a reading of state.

### P7 — The fix satisfies the test and breaks the thing the old value was quietly doing

A number is found to be inconsistent and is normalised to the clean value — zero, the plane, the
default. The inconsistency goes; so does a margin nobody had written down, because the old value was
doing *two* jobs and only one of them was named.

It slips through because the test written alongside the fix pins the property that was wrong and not
the property that was right. Every slab's top face at +0.008 and +0.033 is a real fault, and
levelling them all on to 0.000 fixes it — and satisfies "they all agree" perfectly while making
every one of them coplanar with the ground beneath. The board flickered inside an hour.

**The tell is a constant that becomes round.** When a fix moves a number to 0, to the plane, to
flush, to exactly — ask what the old, unround value was keeping apart. Clearances, epsilons and
biases look like sloppiness and are usually the only thing standing between two surfaces.

**The fix**: give the margin a name and an owner (`CellMetrics.SlabLift`), put it in the rule rather
than in each row, and **assert both halves** — that the values agree, *and* that what they agree on
is still clear of whatever the old margin was clearing.

### P8 — A per-cell tile's own edge, and the four fixes that are all placement

The board is drawn one instanced piece per cell, so every seam in the game is two pieces meeting on
a line. When something shows along that line, the reflex is to reach for the **placement**: drape it,
level it, lift it, overlap it, stagger it. Every one of those was tried on the floor seam of
2026-09-18 and measured, and not one of them moved it, because the thing being drawn was the piece's
own rim and the rim goes wherever the piece goes.

**The tell is that the artefact tracks the cell pitch and survives the board going flat.** Relief off
and it is still there; the ground taken out from under it and it is still there; the piece grown to
overlap its neighbour by 200 mm and it is still there, on the same pitch.

**Ask what is *at* the line, not where the line is.** A plate has a rim; a rim ends in the plane of
the neighbour's top face; two surfaces at one depth is a tie and a tie is drawn by whoever wins. The
answer was to stop drawing the rim, not to move it — the same answer `ResolveTerrain` had already
written down for water: *"a slab has sides and an underside that water cannot afford to draw."*

**And measure in pixels, because this class of fault is invisible in metres.** Counting pixels much
darker than all four of their neighbours turned four rounds of argument about screenshots into one
number per experiment, and it was that number, not the pictures, that killed the three wrong fixes.

### P9 — A shared buffer whose first use fixes its shape for every use after it

A cache, a scratch list or a property block reused across callers is normally a pure saving. It stops
being one the moment the API remembers something about the *first* call and applies it to the rest.
`MaterialPropertyBlock.SetVectorArray` is exactly that: the length of an array is fixed by the first
set and every later set is **capped to it**, with a warning and no exception. One block reused across
buckets of different sizes therefore serves the first bucket correctly and every larger one with
colours that were never written for it.

**The tell is that the symptom depends on which thing was drawn first, not on the thing that looks
wrong.** The trees repainted themselves; nothing about the trees had changed. It also comes and goes
with a toggle that has no colour in it at all — see-through only decides *whether the shared block is
used*, and that was enough to make a tree-colour bug out of a buffer-reuse bug.

**Ask what the first call taught the object.** An array length, a capacity, a format, a keyword set,
a texture size: anything the object latches. If the answer is "something", the shared thing must be
used at one fixed shape — pad to it — or not be shared.

**And treat a Unity warning in a render path as a bug report.** This one named the property, both
sizes and the exact line, and it had been printing for as long as the feature existed.

### P10 — A pass that draws once per cell, in a renderer built on instancing

**A submission costs about 4.6 us whatever is in it** (`docs/design/06-rendering-and-camera.md`
§6c). So a pass that issues one `Graphics.RenderMesh` per cell is priced by how much of the board
the player has touched, not by how much there is to see — and it is invisible to review, because
each call is obviously correct and the loop around it is three lines.

It hides especially well when the pass **does not increment `DrawCalls`**: the budget then cannot
see it at all, and a milestone can report a healthy number while the pass is the largest thing in
the frame.

| The pass | What it drew per cell | What it cost | Now |
|---|---|---|---|
| `DrawZoneCover` | the ground module again, tinted | 2,065 calls, **3.67 ms of a 5 ms budget**, counted nowhere | a bit on the terrain bucket's tint — no draws |
| `DrawSeedSpecks` | six unit cubes, one material | six submissions a sown cell | one `RenderMeshInstanced` a frame |
| `TerrainSkirt` trees | instanced, but split into 760 spatial batches | 3.5 ms; **the tree count was irrelevant** | 272 batches, same 4,169 trees |
| `DrawCellMark`/`Cut`/`Fill` | a plate per designated cell | **0.40 ms at 901 orders, counted nowhere** | gathered by colour, one instanced call each |

**The tell:** a cost that scales with cells the player painted, designated or planted rather than
with what is on screen. **The check:** anything fixed to the grid belongs in the chunk mesher,
where a bucket is one instanced call and inherits culling and the dirty-chunk rebuild.
`GrowingRenderTests.ABiggerFieldAddsInstancesRatherThanDraws` and
`CellPlateTests.ABiggerMarkedAreaAddsInstancesRatherThanDraws` are the guards — each fails the
moment its pass costs draws in proportion to its cells.

**But price the pass before you believe the arithmetic.** The mark pass was the fourth of these
and the first to be measured with a control in the same run, and it came out at **0.40 ms for
901 plates, of which batching recovered 0.09** — a twentieth of what "4.6 us a submission times
901" predicts. A submission's price depends on what is in it after all: the readings that
constant came from each drew a real mesh, and a mark is a unit cube. The counted-nowhere half of
this pattern is the reliable one; the it-must-be-expensive half is a hypothesis to test.
`docs/design/06-rendering-and-camera.md` §6c.1.

**And once per *thing* rather than once per cell, hidden behind a cadence.** The thermal pass
(2026-09-22) runs one tick in 120 and sweeps **every standing edifice** to find the ones that are
warm — so it is priced by how much is on the board, not by how many rooms there are. On a wooded
board that is trees: 2.5 M cells with 5 edifices cost 0.0054 ms, and 921 k cells with 6,311
edifices cost 0.17. **The class's own summary said "O(rooms + surfaces), never O(cells)"**, and it
had survived a nine-finding review, because the sentence is half true and the false half is the
half that grows. The inner step was `BuildingForEdifice`, a linear scan of the building table — a
scan inside a sweep — and precomputing it by edifice id took the pass to 0.051 ms.
**The cadence is what hides it:** amortised over 120 ticks any of those numbers rounds to nothing,
so the honest figure to look at is the cost of the pass itself and the count it scales with, printed
side by side. A complexity claim in a doc comment is not a measurement.

**The same shape on the simulation side**, found the same day and not yet fixed: `GrowingZones`
publishes one `ZoneView` per zoned cell *every tick* for a list that changes only when the player
paints. With **no colonists alive at all** a 2,015-cell field still cost 0.035 ms a tick, ~97% of
its whole tick cost. Per-cell-per-frame and per-cell-per-tick are one pattern wearing two coats.

### P11 — A measurement that never measured anything, clamped into plausibility

Every length in the figure director is deliberately *measured* off the rig rather than written down,
because sixty-one characters have sixty-one sets of proportions and a number of metres is right on
one of them. That is the correct instinct and it moves the risk rather than removing it: the whole
of the arithmetic downstream now rests on one reading, and **a reading can be of the wrong thing
while still being a number.**

`figure.StandingHipHeight` read `animator.GetBoneTransform(HumanBodyBones.Hips)`, which on this
cast's avatar is a bone named `Root` standing on the floor. The difference it computed was nought.
A `Mathf.Max(0.2f, …)` then turned "nothing" into "twenty centimetres", and everything that
consumed it went on working perfectly on a colonist a sixth of her real size.

**The tell is that the bad value is in range.** Nothing throws, nothing logs, no test fails, and the
symptom appears a long way downstream in a shape that looks like a different bug — here, a body
drawn in the wrong *place*, which sent two rounds of investigation into the placement arithmetic and
into the simulation, both of which were right.

**Ask what the measurement returns when it measures nothing**, and make that answer loud rather than
plausible. A clamp, a `?? default`, a `Mathf.Max` floor and a zero-initialised field are all the
same trap: they are there so that a missing rig does not crash, and they double as a disguise for a
rig that is present and being read wrongly. Where the fallback has to exist, it should be a value a
test can recognise as the fallback — and a test should assert that the real thing is not it.

**Its second face: an aggregate answers the question it aggregates, not the one you asked** (added
2026-09-20). Here the measurement was real and correctly computed, and still about the wrong thing.
`SleepPose.Lift` was tuned until the lowest drawn vertex *anywhere* on a sleeping colonist just
touched the mattress. On a supine sleeper that vertex is her back and the tuning was right. On a
**side** sleeper it is a drawn-up knee, which props the body up like a kickstand — so half the
colony lay 9 to 12 cm above its own bedding while the number reported 0.00 and 0.02 and looked
perfect. A `min` over a whole body is a statement about that body's *extremities*; the thing being
judged was its trunk.

**Ask what the aggregate is over, and whether the subject is the whole of it.** Where it is not,
measure the part in question — `SleepProbe` now prints the trunk's clearance beside the whole
mesh's, so the next person to look can watch the two disagree. This is the harder half of the
pattern, because there is no clamp and no zero to notice: both numbers are true.

**And prefer the thing the player sees to the thing the rigger named.** A bone's meaning is a
decision somebody made in a modelling package and cannot be assumed; where the drawn vertices are is
not. `MeasureSole` already knew this — it bakes the posed mesh rather than believing the root is the
sole — and the fix was to ask the same question the same way.

### P12 — A per-actor pass that consults every other actor

**P10's sibling, one level up.** P10 is about a pass priced by cells the player touched; this is
about a pass priced by the *square* of how many things are alive. It hides even better, because
each call is obviously correct, the loop around it is three lines, and at the colony size anybody
tests with the quadratic term is smaller than the noise.

| The pass | What it consults | What it cost | State |
|---|---|---|---|
| `PawnPose.Of` crowd sidestep | every other pawn, per posed pawn, per frame | **15.8 ms of a 27.8 ms frame at 384 colonists** in `Actors` alone, 0.02 ms at 64 (2026-09-23, clear machine) | **fixed** — `PawnCrowdIndex`, a 3 m bucket index built once a frame; `docs/design/25-pawn-steering.md` §9 |
| `WorldSnapshot.TryGetPawnAspect` | every published aspect row, per lookup — and a colonist publishes **57 rows a tick**, so the set is 57 × colonists | **4.59 ms in `Actors` and 6.39 ms in `Figures` at 384 colonists**, hidden underneath the crowd scan until that was fixed | **fixed** — a lazy index built on the first lookup of each frame; `docs/design/31-aspect-lookup.md` |

**And one pass can hold two of them, which is the lesson from the second row.** The crowd scan was
3.5× the aspect scan, so until it was removed the aspect scan looked like a constant — the bend was
attributed entirely to the larger term, and the smaller one only became visible, and obviously
quadratic, once the larger had gone. **After fixing a quadratic, measure the same pass again rather
than declaring it linear.**

**The tell:** a cost that is flat while the count is small and then bends upward, with **draw
calls and tick time both flat through the bend**. If neither the submissions nor the simulation
moved, the cost is in a per-frame loop, and a loop that bends is a loop inside a loop.

**The check:** for every per-actor pass, ask *what does it read that is not its own actor?* If the
answer is "all of them", ask what the influence radius is. Here `SteeringCurve.CrowdFarRadius` is
3.0 m against a 2.5 m cell, so all but a handful of the 147,000 pairs at 384 pawns contribute
**exactly zero** — the pass is not approximating, it is computing nothing, expensively. **A bound
like that makes the fix exact**, which matters more than the speed: a spatial index that skips
only zero-weight pairs changes no drawn position, so a judged visual does not have to be judged
again.

**The instrument:** `OdysseyBootstrap.FrameSectionMs` splits the draw block eight ways and
`FrameTimeTests.TheFrameAgainstColonySize` sweeps eight colony sizes in one world. A frame number
that says "the renderer is slow" without saying which part only licences a guess.

### P13 — The asset cannot draw the thing the code asked for, and nothing says so

*Numbered out of order on purpose. It arrived as a second `P10` when two branches merged, and both `P10`s were already cited across the design documents; moving the newer one costs four references and leaves every existing citation true. A number in this catalogue has to resolve to one pattern.*

A string literal, a shader keyword, a sprite name or a font glyph is *valid code* that names
something the shipped asset does not contain. Nothing throws. The renderer draws its fallback — a
blank, a magenta quad, a box — and every test passes, because a test asserts the value that was
asked for and not the picture that came back.

**The tell is that the thing is missing rather than wrong.** A colour that is off is a colour
somebody chose; a glyph that is simply absent is nobody's decision, and it only shows up in a
screenshot taken by a person who happens to be looking at that state. The Work tab's Simple mode
had this in its purest form: `"✓"` and `"✕"` in two labels, Archivo Narrow with neither in
its cmap and IBM Plex Mono with only the first, so a legend drew two blanks and every "won't do"
cell drew one. The same fault was already sitting in the bed-owner picker on `main` and had never
been played.

**Neither tier can see it and that is structural, not an oversight.** The fast tier has no text
engine at all; the Unity tier runs one and asserts no pixels. So this class has to be caught by
**reading the asset**, which is cheap: `HudFontTests` parses both `.ttf` cmaps and fails on any
non-ASCII character in a HUD literal that either font cannot draw. It found the second instance on
its first run.

**Ask it of anything the code names by string and the pack has to supply**: a glyph, a shader, a
sprite key, an audio clip, an animation state. If the name is a literal and the asset is a file,
something should read the file. The project already does this for icons (ADR 0007's validator) and
for Defs (the content fingerprints); a font is the same question with a different file format.

### P14 — A rule that only governs arrival, in a world where things are already there

*There is one `P14`, one `P16`, one `P17` and one `P18`, and **two** `P15`s — the second is a known collision from two branches merging, kept because both numbers were already cited. A third would not be kept: renumber on merge, as `P18` was twice (2026-09-23 and 2026-09-24).*

A gate is written where new things come in — a filter on what a store *accepts*, a check on what
may be *placed*, a validator on what may be *entered* — and it is correct about every one of them.
Nothing governs what was already sitting inside the boundary when the rule was written, or was
there before the player narrowed it. The rule reads as total and is not: it is a rule about the
door.

**The tell is that the state is stable and nobody is misbehaving.** Every actor is obeying a rule
it can see. A store refuses stone, so no hauler brings stone; a hauler will only take a thing
somewhere that will have it, and nowhere will; so the stone that is already in the store is
touched by nobody, for ever. There is no error, no failed job to count, no alert — the colony just
quietly stops being able to use those cells, and the second symptom (*"and the meals were never
hauled either"*) arrives later and looks like a different bug, because the occupied cells have no
space for what the store does want.

**The question to ask of any filter, gate or predicate:** *what is already on the wrong side of
this, and who moves it?* If the answer is "nobody", the rule needs an eviction half, and the
eviction needs a destination that the same rule would accept — or it will put the thing straight
back into trouble somewhere else.

**And the two halves are usually written a long way apart.** The filter lives with the thing it
configures; the eviction has to live in whatever scans for work. Here the filter was in
`StorageSettings` and the fix was in `HaulWorkGiver`, two assemblies' worth of intent apart, which
is why "the filter is obviously right" and "the behaviour is obviously wrong" were both true for
two days.

### P15 — A pick resolved against the frame before the one on screen

A screen position is turned into a world thing by casting a ray through the camera — and the cast
happens earlier in the frame than the camera's own move. The input pass reads the pointer and
resolves it immediately, because that is the natural place to write it; the transform is applied
further down, because that is the natural place to write *that*; and the two were written months
apart by people each doing the obvious thing.

**The tell is that it is perfectly accurate when nothing is moving.** Every test passes, because a
test holds the camera still. Every screenshot is right, because a screenshot is one frame with the
camera parked. The complaint arrives as a feeling — *"it doesn't seem super accurate"* — with no
reproduction, because the offset exists only while the player is panning, orbiting or zooming, and
it vanishes the moment they stop to look at it. It does not converge, either: it is a constant one
frame of lag rather than an error that settles, so a slow pan is off by a little for ever and a
fast one is off by a lot.

**The question to ask of any input pass:** *what has this frame already changed that the answer
depends on, and has it happened yet?* A camera is the obvious one; a slice layer, a scroll offset,
a panel that has just been resized and a world that has just ticked are all the same shape.

**The fix is ordering, and the repair worth making is that the ordering becomes an invariant.**
Separating *deciding what the gesture was* from *resolving it against the world* is what makes the
order expressible at all — the decision can be latched as a verb and a screen point, and then
there is exactly one place that consults the camera instead of one per gesture. Guard it with a
test, including the "and nowhere else" half: this one was `SliceCameraRig.Update`, and
`PointerCursorTests` asserts both that the resolve pass runs after the transform and that no other
line in the file turns a screen point into a cell.

---

### P15 — One version number for many caches, so every edit invalidates all of them

A cache keyed on "has anything changed" is a cache that rebuilds *everything* whenever *anything*
changes. It is invisible in review because the code is correct — the picture is always right — and
invisible to the frame budget because the cost lands in the frame after an edit rather than in the
steady state a benchmark measures.

`WorldRenderModel.Version` was exactly this: one integer for a board of 45 drawn chunks, compared
by every `ChunkBatch`. Raising one wall cost **12.53 ms** in the next frame against 0.7 ms either
side, because all 45 chunks re-meshed to redraw one cell (2026-09-21, `BuildAppearanceTests`). Per
chunk it is 3 chunks and 1.73 ms.

**The tell is a stat that moves in lockstep with nothing.** `TotalChunksMeshed` went up by the
whole board on an edit of one cell, and the number was already being counted — nobody had held it
against the size of the edit. Ask of any invalidation stamp: *what is the smallest edit, and how
much does it rebuild?*

**It is not the same fault as an unbudgeted rebuild, and the two fixes are complements.** §6c.7's
meshing budget caps how many chunks may be re-meshed *in one frame*; this caps how many are
invalidated *at all*. With the budget alone, one wall still dirties all 45 drawn chunks and merely
spreads the work over four frames of stale geometry. Landing both, one wall re-meshes three chunks
inside one frame.

**And fixing it makes something else load-bearing.** A global stamp forgives under-marking: a
system that dirtied too few regions still drew correctly, because everything was rebuilt anyway.
The day the stamp becomes per-region, every "I changed this cell" must name every region whose
output depends on it — for terrain, the 3×3×3 neighbourhood, because a face is drawn against what
is beside it. Check the marks *before* narrowing the stamp, not after a stale tile is reported.

---

### P16 — The exactness test, run on the case where exactness cannot show

An optimisation that is *provably* exact still has to be tested, and the obvious test — "compute it
both ways on a busy fixture and compare" — is often blind to the only way the optimisation can
actually be wrong.

**What decides where a miss is visible is the reduction that consumes the set**, not the set. The
crowd sidestep reduces with a `max`: the nearest colonist wins and every other contributor is
discarded. A cull that loses somebody at the *edge* of the influence radius therefore changes
nothing, because that contributor was never the maximum — it is worth about 0.007 of the envelope
where a near neighbour is worth 1.0. On a crowded fixture there is nearly always a nearer neighbour
to hide behind.

**Measured, 2026-09-23.** `PawnCrowdIndex`'s bucket size was mutated from the 3 m influence radius
to the 2.5 m cell — the exact tidy-up its own doc comment warns against, and a plausible one — and
`PawnCrowdIndexTests.EveryScanModeDrawsTheIdenticalPose`, 220 pawns and the headline claim of the
whole unit, **passed**. Only the brute-force set-membership test caught it, and that one asserts no
pose at all. Had it not been written, a broken cull would have shipped behind a green test named for
exactly the property it was not checking.

**The check:** for an exact optimisation, ask *where is the smallest surviving contribution, and
what would hide it?* Then write the fixture where that contribution is **decisive** — one
contributor, at the boundary, nothing larger in the set — and sweep it across the boundary.
`AnInfluenceAtTheVeryEdgeOfTheRadiusSurvivesTheCull` is that test: two pawns, no crowd, walking from
outside the radius to inside it.

**And mutate the constant to prove the test can fail.** Both tests were green before the mutation
and both were believed; one of them was decorative. A test for an exactness claim is worth what a
deliberate break costs it and nothing more — the same lesson as *"A test that could not fail for the
reason it named"* in the register below, reached from the opposite direction.

### P17 — Two translucent draws that cover each other, ordered by a key that cannot tell them apart

A translucent material writes no depth, so where two translucent draws cover the same pixels, the
picture is whatever was drawn **last**. Unity picks that order by the distance from the camera to
each draw's bounds centre. Two things built around one centre (a fill inside its track, a glow
inside its core, a plate behind its label) tie on that key **exactly**. The sort then breaks the
tie differently from frame to frame, depending on everything else in the translucent list. Two
things whose centres differ only across the screen trade places when the object crosses the
middle.

**Measured, 2026-09-23** (design 33 §8a). The health bar's fill was a translucent box inside a
translucent track. For a full bar the two centres tied on 480 frames out of 480 of a walk across
the screen, and the fill showed at 0.62 of its colour one way round and 0.29 the other. The owner
reported it as *"the bar above their heads flicker"*.

**The check:** for any mark made of more than one translucent draw, ask *do any two of them cover
the same pixels?* If they do, the look depends on the sort. Lay the pieces so they meet on edges and
never overlap (`HealthBarLayout`, held by `NoTwoPiecesOfABarOverlapAtAnyFraction`), or merge them
into one draw. Nudging one "a little nearer the camera" is not a fix: the lateral term in the
distance is worth tens of centimetres at the play camera.

---

---

### P18 — An instrument that cannot see the thing it is comparing, and passes

*Numbered P18. It arrived as P14, which `main` had already given to "a rule that only governs
arrival", and was renumbered P17 on merge on 2026-09-23; by the next merge, 2026-09-24, `main` had
given P17 to "two translucent draws", so it moved again, with every place that cited it. This
catalogue's whole value is that a number resolves to one pattern.*

**Symptom.** A before/after comparison reports a small, plausible difference and the test goes
green. The change looks proven.

**The real cause.** The instrument was never looking at the subject. Two of these on 2026-09-21,
both in the frustum-culling work, and **neither was found by a failing assertion** — one was found
by a control, one by reading a log line that looked fine.

1. **The measurement was overwritten before it ran.** An arm set
   `ChunkRenderer.ShadowCasterMarginMetres = 0f` to price the shadow correction, but the composition
   root re-derives that property from `QualitySettings.shadowDistance` **every frame**. The test's
   value was gone before the first timed frame, so the arm timed the same configuration twice and
   reported the difference — 0.07 ms — as the price of correct shadows. **The tell was in its own
   log line:** both readings printed the identical 2,053 draw calls. A comparison whose
   *deterministic* half does not move is not a comparison, whatever its timings say.
2. **The capture never saw the board.** `CullingDoesNotChangeThePicture` rendered the scene culled
   and unculled, compared pixels, and would have reported 2.58% moved as "close enough". Its control
   — the same scene with a frustum admitting *nothing* — moved 3.22%. Rejecting every chunk in the
   world cannot move 3% of a picture of that world, so both figures were noise from a nearly-empty
   buffer. **Without the control the test passes and certifies a blind comparison.**

**Why it is this project's shape.** A timing or pixel comparison has no natural failure. A unit test
asserts a value and is wrong loudly; an instrument asserts a *difference*, and a difference between
two readings of nothing is indistinguishable from a difference between two readings of something.

**The check, and it is two rules rather than one test.**

- **Every comparison carries a control that must show a difference.** Not "the feature changed
  nothing" alone — also "the deliberately broken case changed plenty". One assertion says the answer;
  the other says the instrument could have noticed another answer. `docs/process.md` already asks for
  the negative control; this is what it buys.
- **Assert on the deterministic half, not only the timed half.** Draw calls, chunk counts, instances
  and mined-cell counts do not move with the machine's mood. `MineOneCell.Mined == Ticks` and
  `callsOn < callsOff` catch a fixture that stopped doing its job; a millisecond figure never will.

**A third of the same shape, found hours later by an owner's play log rather than by any test.**
Every per-board measurement arm generated its world from `MapGenerator.DefaultDef` while the played
scene is *barren + wooded* and so gets `MakeWooded()` applied on top — two owners for one choice,
silently disagreeing. The owner's console read `patches 0, trees 1598`; the arms were reporting
`patches 2210, trees 1222` for the same board. The fix deleted the second owner
(`ColonyWorld.DefFor`) rather than copying the first. **This pattern was written one commit earlier,
in the same branch, about the other two — and was not applied to the arms it was written about.**
Writing a pattern down is not the same as running it over the work in hand.

**Where to look for more:** any property a per-frame system re-derives from settings — a test that
writes it is writing into the next frame's overwrite. And any capture-and-compare: ask what the
picture looks like when the subject is removed entirely, and make the test assert that answer.

### P18, met a third time — and the field the root rewrites every frame

**2026-09-23.** `CullingDoesNotChangeThePicture` was the test frustum culling was held on. It
failed its own control — a frustum admitting *nothing* moved 3.22% of pixels — and for two days
nobody could say whether the cull was wrong or the instrument was blind.

**It was blind, and the reason is a specific, repeatable mechanism worth naming on its own: a test
cannot set a field the composition root writes every frame.** The control assigned
`ChunkRenderer.Frustum`; `OdysseyBootstrap.LateUpdate` assigns it too, once per frame, so the test's
planes were gone before the first capture. The comparison was the culled shot against itself.

**The tell both times was two readings with identical counters.** 126 chunks, 57,818 instances and
1,744 draw calls for the culled shot *and* the blind one, where the blind one should have submitted
nothing whatever. The first instance, one commit earlier in the same branch, was
`ShadowCasterMarginMetres` re-derived from `QualitySettings.shadowDistance` — and the tell there was
identical draw calls in both readings. **The lesson was written and then not applied to the field
next to it.**

- **The check:** before trusting any test that sets a renderer or director field, grep the
  composition root for an assignment to that same field. If the root writes it per frame, the test
  needs a seam of its own — `ChunkRenderer.FrustumOverride`, `PawnCrowdIndex.Mode`,
  `ColonistAttachments.Enabled`. A property with a public setter is not a seam if something else
  sets it sixty times a second.
- **And assert on the deterministic half.** Chunk and draw counts would have failed instantly and
  said why; pixels took two days. The counters were already being logged — nobody had held the
  blind row against the culled one.

**A second fault hid underneath the first, and it is the commoner one.** With the control finally
working, two captures of the *identical* configuration still differed by **1.29%** of pixels, against
culling's 2.13% — a difference that is meant to be nought, asked to stand out against a floor most of
its own size. Pausing the simulation was not enough: it stops the ticks, so nobody walks, but water
scrolls its streaks, figures advance their animation graphs and the daylight rig moves, because those
run on `Time.deltaTime` and the shaders on `_Time`. **`Time.timeScale = 0` stills the shaders as well
as the scripts**, and the floor went to 0.00%.

- **The rule:** a comparison needs *both* controls — one that must show a difference and one that
  must not. The repeat-shot is the cheap one and almost nobody writes it, and only the first of the
  three numbers makes the other two mean anything.

**And the repeat-shot immediately earned its keep a second way: it is not a constant.** With the
control fixed and the clock stopped, the test passed *alone* at a 0.00% floor and **failed inside
the full PlayMode tier at 0.77%**, on the same commit — a busy run is still finishing shader
variants, texture streaming and the post stack's first frames. A longer settle takes it to 0.04%,
but the lesson is the acceptance: judge against **the floor measured in the same run**
(`culled <= noise + 0.002`), not against a chosen tolerance. A constant is a guess at the null, and
this one guessed wrong in both directions on the same day.

> That is the narrow exception to *"a loose tolerance can make a test prove nothing"* in
> `docs/lessons.md`: the bound is *measured*, not chosen, and the positive control stays absolute so
> the comparison cannot go quietly blind. **A tolerance you measured is a control; one you picked is
> a hope.**

**And a third machine found a third fault, which is the argument for having one.** The test then
failed on the CI runner and nowhere else — not on pixels, but on its own precondition: `GameSpeed`
was still 1, so it had photographed a moving world. The pause was submitted **once**, and an intent
goes on a bus with a capacity and drains on a tick boundary, with the thousand designations the
fixture had just queued still going through. It landed on the dev machine and did not on the runner.

- **The rule:** *submitted* is not *applied*. Anything a test asks of the game through a queue is
  asked until the state it wanted is readable, bounded, and then asserted — never submitted once and
  waited a fixed number of frames.
- **The guard is what made this cheap.** It failed loudly and named the precondition instead of
  quietly photographing a moving scene and reporting a plausible noise figure. Assert on the
  deterministic half: here that is *is the world actually paused*, not *how many pixels moved*.

---

## The register

### 2026-09-24 — A wait that trusts a path the save does not keep (P14-adjacent; found by the gate)

**Symptom, found by the combat gate before anybody played it.** A save taken at the first swing of
a raid, loaded and run on for a day, came to a different hash from the world it was saved from, on
two seeds of three. The lockstep twin in the same run agreed every hour, so the simulation was
deterministic and the fault was in the round trip.

**Cause.** One tick after the load, one drafted colonist who had joined a fight nearby had not
moved: every field saved and hashed matched, but her progress through a step was 69,050 in the
loaded world and 71,304 in the other. `Job_AttackMelee` has five branches that let **a step already
under way land** before they decide (`if (!boundary) return Ongoing`), and they rely on the mover
to finish it along the path she holds. A path is recomputed, never saved (`MovementSystem`), and a
driver that walks asks for one through `GotoCell` every tick — but these branches do not walk, they
wait. So a pawn loaded in reach, part way through a step, held no path, nothing asked for one, and
she stood frozen until her target moved away.

**How it hid.** Every existing round trip saved at a moment nobody was in that state: mid-swing
(`ASaveTakenMidSwingResumesTheSame`), mid-carry, the soak's save the tick after a spawn. It needs an
attacker in reach of its target and still walking, which is a crowd, which only a raid makes.

**Fix.** `AttackMeleeJobDriver.LandTheStep` asks for the path again when the pawn has none and none
is pending; it is served before anybody steps in the same tick. A world that was never loaded
always holds a path there, so nothing changes in it: no golden moved, and the gate's final hashes
were identical to the digit before and after. `AttackDriverTests.ASaveTakenMidStepInReachResumesTheSame`
finds that moment in a three-against-two fight, saves there, and failed without the fix (the loaded
attacker 2,096 into the step against 4,192 one tick on). `33-combat.md` §21.

**The check this earns.** *A branch that returns "still going" without acting is trusting another
system to move the world on. Ask whether that system's input survives a load.* Derived state —
paths, caches, indexes — is rebuilt by whoever next asks for it, and a wait is a place where nobody
asks.

### 2026-09-24 — A stored graphics preference never reached a new game (P1, P2-adjacent)

Found by reading, while building the quality presets. `SettingsPresenter` attaches on the first
frame — the start screen — and lays the stored preferences over the director, which raises
`OptionChanged`; its `Apply` asks for the renderer, finds none, and returns. The session's renderer
is then built from the bootstrap's fields. So a stored "shadows off" or "surround off" was applied to
nothing and every new game came up as the scene said; only a lever moved *during* play ever took.
Two owners of what a preference means to the renderer — the live handler and the bootstrap's
initialiser — and only one of them read the preferences.

**What now stops it:** `SettingsPresenter.ApplyRendererLevers` is the one mapping, called by the
root as it builds a renderer whenever a store is attached, and `GraphicsLeverTests` holds the
mapping. `27-graphics-settings.md` §10.

### 2026-09-24 — Two bandits stood on "Fighting" at a wall with one side (P1)

The owner: three bandits, one breaking a building, two standing about. Measured in the owner's
save: all three chose the same wall of a house on the edge of a terrace step, whose only side on its
own layer was one cell (the others are air over the step below). One struck; two waited 3,245 and
3,312 ticks. The choice (`TryNearestColonyTarget`) asked whether a side could be **reached**; the
driver (`ChooseSide`) asked whether one was **free**; every rethink sent them back to the same wall.
The difference had been written down on purpose (*"a held side is the driver's to sort out"*), which
is how a P1 looks when it is a decision rather than an accident.

**What now stops it:** the choice asks `BuildingTargets.HasAFreeSide`, the driver's own answer, and
an unforced attack that finds every side held thinks again at once.
`BanditSideTests.ThreeBanditsAtAWallWithOneSideDoNotStandAbout`. `33-combat.md` §19a–§19b.

### 2026-09-24 — A loaded game kept the generated board's paths (P1-adjacent; a test that could not fail)

Found by the same probe. `ColonyWorld.RebuildDerived` — "the derived state is now correct", one
definition for both paths — called `NavGraph.Rebuild`, which floods only the blocks something marked
dirty, and a load marks none. In every block of a loaded game that no door or ladder on the load
path happened to dirty, a built wall was walkable and a built floor was not. In the owner's save:
seven walls on the first column of a block, walked into by bandits, and six upstairs cells a
colonist could not be ordered to. The test written for exactly this,
`AWorldWhoseGridHasChangedStillResumesIdentically`, wrote its wall straight into the grid with
nothing marking the graph in **either** world, so both were equally stale and the hashes agreed.

**What now stops it:** `MarkAllDirty` before the rebuild, and
`WorldRoundTripTests.ABuiltWallIsStillAWallToThePathsAfterTheLoad`, whose walls go up through
`Raise` so the original is right and the loaded copy is compared with it cell by cell. The shape to
ask of any "rebuild" on a load path: **does it rebuild, or does it catch up?** `33-combat.md` §19a.

### 2026-09-24 — A squad sent upstairs was spread downstairs (P1)

A right-click on the upper floor with four drafted colonists selected sent 49 of 160 orders to
another layer (the owner: *"tricky to draft then move my colonists to another floor"*).
`JobSystem.Spread` placed the others round the clicked cell with the **click's** lift, `StandAt` —
that cell, else above, else below — so a ring cell over the ladder's open shaft or past the floor's
edge dropped to the room below or the ground outside. One rule (where she stands for a click) was
serving a second question (where the others stand round her).

**What now stops it:** the spread keeps to the named cell's layer.
`DraftOrderLevelTests.ASquadSentUpstairsIsSpreadOnTheFloorItWasSentTo`. `33-combat.md` §19c.

### 2026-09-24 — The cull was asked after the mesher, so the budget went on chunks nobody could see (P1-adjacent)

Found by reading, while planning the Meadow overhaul, and fixed on merging `main` up to the culling
branch. `ChunkRenderer.Render` called `BatchFor` — which meshes a stale chunk — and only then asked
the frustum. So after a board-wide `Remesh` (every graphics toggle) the eleven-chunk meshing budget
was spent in index order on chunks behind the camera, and the chunks on screen waited behind them.
Two fixes that each worked alone, the budget and the cull, met in an order neither had chosen.

**What now stops it:** `MeshBudgetTests.AnOffScreenChunkSpendsNoneOfTheBudget` — a whole-board
re-mesh under a frustum round one chunk defers nothing, and removing the frustum meshes the rest.
The cull asks `ChunkMesher.BoundsOf`, the same box `Mesh` writes, so the picture cannot move.
`28-map-size.md` §10.1.

### 2026-09-22 — A merge with no conflict where the fault was, and a claim that outlived a review (P1, P10)

PR #164 merged with a `main` that had moved twice under it. Eighteen files conflicted and the
merge was mechanical; **the fault was in a file that did not conflict.**

**Two branches that wrote the same text for different reasons.** `BuildShapes.Cells` is a table
parallel to `BuildingHandle`, and the shelf branch and the campfire branch had each appended a
`1` to it. Git saw one added line and took it once. The merged table was one entry short, so the
campfire silently had no shape — and `EdificeHandle.Count` and `BuildingHandle.Count` merged clean
and were both wrong by one for the same reason.

`RegistryTests.EveryBuildableHasAShapeOfItsOwn` caught it. **That test exists because of this
exact failure**, two months earlier: when the bed's handle moved from 2 to 5, the same table
merged in silence, the bed became a one-cell thing that could not be turned, and three
`DesignateDirector` tests failed without naming the cause. It has now paid for itself twice on the
same fault.

**The tell:** a merge conflict marks where two branches wrote *different* text. The dangerous case
is where they wrote the *same* text for different reasons — which is the normal case for a
hand-maintained parallel table, since every entry in one is some flavour of `1`, `false` or `""`.
**The check:** every such table wants a length assertion against the enum it parallels, and only
the ones that have one are defended. `BuildShapes`, `BuildLabels`, `EdificeLabels`,
`QualityLabels`, `TerrainLabels` and `ItemLabels` are the family.

**And a golden conflict has exactly one honest resolution.** Both branches had moved all six
numbers, so neither side's value was right for the merged code and taking either would have
committed a number nothing had produced. Re-baked, then *measured* with `GoldenColonyProbe` on the
merged branch, the branch head and `main` — three diffs, clean. Which also established the quieter
fact that **the goldens were never evidence the thermal model bites**: their windows sit inside the
work band, nobody sleeps in them, and the boards have no crops.

The other two findings are P1 in its usual clothes (the temperature-to-text form written out in two
assemblies, agreeing by luck — now `TemperatureLabels`, guarded by a test that reads the C# files)
and the P10 entry above.

**A third kind, which this catalogue had no room for and gets a sentence here instead.** The work's
headline claim was *"Rime kills"*; Rime is month five of six; the debug menu offered *Skip one day*.
Sixty presses. Nothing was broken, every test was green, and the effect was that **the scope of the
playtest had been set by the tooling rather than by the work** — the branch's own "still owed" note
asked only about the mild season, which is what a question looks like when the interesting one
cannot be asked. The check is cheap and belongs beside the handover: **read your own playtest
instruction and try to follow it.** "Fast forward into Rime" was already written down, by somebody
who had not counted the presses.

### 2026-09-21 — Nine faults in a thermal model that had thirteen green tests (P1, P2, P11)

Reviewed before its first playtest, PR #164, by writing one probe test per suspicion and
believing none of them until it failed (`docs/design/28-temperature.md` §12, §12a). Four shapes
this catalogue already has, in new clothes:

**A per-mille applied to the wrong unit (P11).** `severitySlopePerMille` 300, "per centi-degree
of distance": a Candle night filled the hypothermia bar in fourteen game-minutes while the XML,
the def comment and the pinning test's own message all promised hours. The number was pinned and
green; the message beside it said "three" and the value said 300. **A test that pins a number
under a sentence describing a different number is the tell** — read the message against the
value, not just the value against the code.

**A fixed point over a fixed set (P2).** The enclosure swept "until nothing changed" over the
layers the edit had marked, but the change it was converging on propagates *downward* (a layer's
roof rule reads the layer above), so the cellar under a house roofed last was never re-solved.
The played world and a loaded one disagreed — the divergence the sweep had been written to end.
The fix is not a wider window: identity solves top-down, and a layer that changed marks the one
below itself. **When a solve iterates to a fixed point, ask whether the set it iterates over can
grow; if the dependency has a direction, solve in that direction and let change carry the mark.**

**One event, several caches, one missed (P1).** `Demolish` and `MineJob` told the enclosure; the
floor's `RemoveSlab` told nav and the structure solver and not the enclosure. Grepping every
caller of `Enclosure` in `Sim` took a minute and is the whole check: **list the caches a world
edit invalidates, then list the edits, and look for the empty cell.** The tick benchmark's own
edit arm was another empty cell — it marked nav alone, so the enclosure had never been in the
edit tick (`docs/lessons.md`).

**A shortcut ahead of the rule (P2 again).** "Return the known key's temperature" sat before the
ledger that knew what the room's cells had been, so a room re-sealed after a day open to the sky
came back at a season-old temperature, and a hall knocked through to a cupboard took the
cupboard's. The rule was right; the shortcut in front of it answered first. And the same shape
once more in the surfaces: a ceiling was "rock or sky", and the third case — another room's
floor — fell into sky, so building upstairs made downstairs colder.

**What now stops it:** `TemperatureRegressionTests`, fifteen tests, one per finding and one per
other side of each rule; `EnclosureCostProbe` for the number the benchmark could not see; the
benchmark's miner marks the enclosure. And the goldens were re-baked only after hashing each
world *component by component* before and after — five minutes that turned "the hash moved"
into "only the thermal section moved".

Newest first. Every row: what was reported, what it actually was, and what now stops it.

### 2026-09-23 — The bar over their heads flickers (P17)

Owner: *"The bar above their heads flicker."* One candidate on each side of the seam was measured
before anything was changed. On the simulation side the bar is owed exactly when the pawn's state
says, tick after tick: `HealthBarPublishingTests` fought 3,000 ticks, and no bar changed on a tick
its state did not. On the drawing side the fill was a translucent box inside a translucent track,
and the sort key that orders them was an exact tie on every frame of a full bar, which is every
drafted colonist nobody has hurt. The bar is now nine camera-facing pieces that never overlap.

**What now stops it:** `HealthBarLayoutTests.NoTwoPiecesOfABarOverlapAtAnyFraction` and
`ThePiecesTileTheWholeBar`. Both failed on the plate laid behind the fill as one rectangle.
Design 33 §8a.

### 2026-09-23 — A stockpile drag paints nothing

Owner: *"I can't seem to create stockpiles anymore"*, then *"There is no visual to the stockpile
or indicator or marker - has this somehow been removed"*. It had not been removed and it was being
created: the session log had no stockpile order refused, and `StockpileDragTests`, handing the
presenter a box, published nine zone cells. **The zone existed and was not drawn.**

On natural ground a store's cell is the air over the ground (`StoreCellOf`), and the wash is on the
ground's top face — meshed by the terrain cell a layer down, which washes itself when the cell
above is stored (`WorldRenderModel.IsStoredAbove`). A chunk is one layer, so that face is in a
different chunk from the store. `StorageZones.Mark` dirtied only the store's own chunk. **That was
enough until 2026-09-21**, when chunks got their own versions (P15, `0df9514b`): before it, any mark
bumped one board-wide version and re-meshed everything, the ground included. The per-chunk fix was
right, and its own note said *"a cell edit must now dirty every chunk whose mesh depends on it —
the global version was forgiving under-marking"*. This was the under-marking it forgave.

**Measured both ways in one test:** without the fix the store's chunk is at version 2 and the
ground's at 1; with it both are at 2. `Mark` now dirties the layer below as well.

**The check this earns.** When a cache stops being global, list every reader that looks at a cell
other than its own — `IsStoredAbove`, and anything else named *Above* or *Below* — and check that
whatever changes that other cell marks this chunk too. The fast tier cannot see it (no meshing)
and the picture is only wrong where nothing else happens to re-mesh the chunk, which on open
grass is everywhere.

### 2026-09-22 — A hog walks past a tree, snaps back a cell, walks past it again

Owner: *"saw a pig walk through a tree went past it then suddenly appear before the tree again
and snapped/teleported back to a position then walked through it again."* The snap detector
(`AnimalProbe.Snaps`) found four in a hundred seconds, every one a hog, every one on the tick a
wander job **expired**. The simulation is discrete: a pawn is on a cell with progress toward the
next, and the figure is drawn that fraction of the way along. Ending a job clears the path and
zeroes the progress, so the pawn is back on the cell it was leaving and the figure — drawn 85% of
the way into the next — snaps back to it. The wander's expiry is 1,200 ticks and a hog's leg at
its pace can be longer, so the expiry landed mid-step, reliably, at ticks 1,200 and 3,200.

**The shape: a discrete state dropped under a continuous drawing.** Anything that resets a
pawn's step — a job ending, a path cleared, a reservation lost — resets the figure by a cell.
The fix for an animal is to end the job on a cell rather than between two: the expiry waits for
progress to reach nought, at most one step late — for every pawn, since the same afternoon: the
colonists' copy of the snap, at the end of a mental-break wander, was a recorded gap for a few
hours until the owner asked for it closed on the PR, and two goldens re-baked for it.

**What now stops it:** `AnAnimalsJobNeverEndsMidStep` — every job an animal starts begins with
its move progress at nought, over twenty thousand ticks — and `AnimalProbe.Snaps`, which is the
instrument the report needed: a hundred seconds of six animals under the director with every
drawn position recorded, and every frame that moves a figure more than 0.35 m or backwards
against its own motion printed with the simulation's view of that pawn. It read four before the
fix and none after.

### 2026-09-22 — Legs drawn as rods: a pose multiplied onto itself, frame after frame

Owner, with a screenshot: *"The pig is terrible - the legs and spindles and too thin."* The legs
were drawn as thin rods longer than the body, which the model never had: every still taken of it
at rest, and every still taken with the gait applied to a bare instance, showed a stubby pig.
The difference in the game was the animator. The computed gait pitched each leg bone by
pre-multiplying onto its current rotation, exactly as the colonists' `WorkSwing` does — and that
is safe only while the clip underneath rewrites every bone before every pass. Under the game's
own loop, with the idle held at speed nought beneath the gait, it did not, and the same pitch
landed on top of last frame's, and the frame before's, until the skin stretched along a bone
pointing somewhere no leg points. `PawnFigureDirector.Evaluate`'s own comment names this trap
for the sleep pose ("the figure winds itself into a spiral"); the gait walked into it anyway.

**The shape: an additive pose whose base is assumed, not owned.** Anything that composes onto
"whatever is there" is correct only under an assumption about who wrote "there" and when. The
fix is to own the base: capture each driven bone's rest at bind and write the pose absolutely,
so nothing about what the clip did that frame can reach the answer.

**What now stops it:** `AnimalProbe.ShootMoving` runs a real hog under the director's real
animator for three seconds and prints leg lengths every twenty frames — a length that grows is
compounding, one that holds is not — and photographs the result. And the standing instruments
were the wrong ones: a still on a bare instance can never show a fault that only the animator
produces, which is why the earlier strips looked fine and the game did not.

### 2026-09-22 — A walk cycling once per authored metre, on legs that are 23 cm long (P11)

Owner, on the first animal figure: *"The pig walking looks awful - it looks odd and screwed up -
I can't even explain because it's so odd."* The computed gait advanced one cycle per **authored
stride of 1 m**, a number typed from a research table for a pig-sized quadruped. The rig's own
legs, read from the joint positions the probe prints, are **0.23 m** from shoulder joint to sole
on a 1.2 m body: a leg that short swinging 25° covers about 0.2 m a cycle, so the feet slid over
four fifths of every stride while the legs waved once a second, and a 2 cm bob rode on top at the
same slow rate. Nothing was wrong with the sines; the number they were driven by described a
different animal.

**P11 again, in its plainest form: a length written in metres where the rig should have been
asked.** Every other length in the figure director is deliberately measured off the rig, and the
one that was typed was the one that looked wrong. The fix measures the leg at bind and derives
the stride from it — and, because a model this squat must then either scurry or slide, names the
compromise as one constant (`QuadrupedGait.SlideFactor`) rather than hiding it in a stride.

**And the instrument that certified the fix was measuring the wrong thing** (found on the fifth
look, 2026-09-22). `AnimalProbe.ShootMoving`'s leg report took the joint-to-`Foot` distance and
reported it holding to the millimetre; the rig's `Foot` bones are IK targets outside the leg
chain, planted on the ground whatever the leg does, so the number could only ever move with the
body bob. It was a true statement about the wrong length. The report now measures each segment
along its own bone axis and puts the sole at the lower segment's end; it reads a ninety-degree
fold and a lifted sole on the swinging leg and nought on the planted one. **Check a new
instrument against a pose it should reject before trusting a pose it accepts.**

**What now stops it:** `AnimalFigureTests.TheHogsLegsTrotWhenItMovesAndRestWhenItStands` pins the
measured leg to the rig (0.15–0.35 m) and the derived stride to what that leg can cover;
`AnimalProbe.Shoot` writes a four-phase side-on strip so the gait is judged from a picture before
a playtest. And the wider rule for the next animal: **a gait's numbers come from the rig, and a
rig's numbers come from its bones or its picture, never from a table.**

### 2026-09-22 — The baked mesh said eleven centimetres; the bones said eleven metres (P11)

Not a report: a measurement taken before anything was built, which is the point of recording it.
The two animal models (`Assets/Art/Custom/Animals`) are a Blender "units scale" export — 1 cm file
units with a ×100 on the mesh node and, on the rat, a ×39.55 on the armature. A first probe
measured them with `SkinnedMeshRenderer.BakeMesh(useScale: true)` and reported a pig **0.114 m**
long and a rat 0.071 m: in range, plausible for a "small placeholder", and wrong by a factor of
a hundred. The bind pose folds the node scale into the skinning, so the baked vertices came out in
the mesh's own space and the renderer's ×100 was never applied. The bone world positions — head
at 3.07 m, front foot to back foot 5 m apart — said 11.39 m, and a photograph on a 2.5 m cell
agreed: the pig covered the whole cell and hid the rat behind it.

**P11's third face: the measurement was of a real thing, correctly computed, in the wrong frame.**
The tell was the same as ever — a number nothing could argue with — and the cure was the same as
`MeasureSole`'s: prefer what the player would see. Here that meant two readings that must agree
(bones and bake) and a picture as the tie-break.

**What now stops it:** `AnimalImport.Scales` carries the measured ×0.105 and ×0.09 with the two
readings in its comment; `AnimalProbe.Shoot` photographs both animals on a cell beside a 1 m cube,
and `AnimalFigureTests.TheAnimalRowsResolveFromTheProjectsOwnArt` pins that the rows resolve.
The rule for the next model: **measure a rig from its bones or its picture, never from a bake
alone**, and if two readings of the same length disagree by more than a few per cent, the
smaller one is in the wrong frame.


### 2026-09-21 — Both tabs underlined, and the contents invisible until you clicked (P1)

Owner, on the store pane: *"storage and tile are both underlined when you enter the shelve menu.
It should be just stored and also when you do see menu — it doesn't show what it is holding until
you click on storage when it should show that soon as you click on the shelve."*

**Two reports, one missing line.** The inspect pane rebuilds its tree when the subject changes, and
the colonist branch of that builder ends with `ShowActiveTab()`. The store branch never called it.
Nothing else establishes which tab is live, so:

- the underlines are created **visible** and both stayed lit until a tab was clicked;
- `_storagePane` and `_cellRowsGrid` kept the display the *previous* subject had left them on, so a
  store selected after one whose Tile tab you had been reading came up with its Storage tab blank.

**A second fault of the same shape sat underneath it.** The Holding list pools its rows in
`_storeHoldingRows`, and that list was not cleared on rebuild — unlike `_tabChips` and
`_storageTabUnderlines` two lines away, which are. So after a rebuild the pool held orphans from the
discarded tree: the fill loop wrote text into elements with no parent while the list on screen
stayed empty, and the signature check then decided the rows were already right.

**The general shape: state that must be re-established when structure is rebuilt.** Anything a
builder creates in a default state — a display flag, a selected index, a pooled list of children —
is *not* carried by the model, so a rebuild resets some of it and leaks the rest. The question to
ask of any `Build*` method: *what did the last tree know that this one does not?* Every pooled list
beside a rebuilt element is a candidate, and the tell is a control that works until you look at
something else and come back.

**What now stops it:** `HudSmokeTests.AStoreOpensOnItsStorageTabShowingWhatItHolds`, which selects a
stocked shelf, selects away, and selects it **again** — the first selection passes against the
broken code, because on a virgin pane the defaults happen to read correctly, so only the
re-selection is a real test. It asserts *display flags*, which is the gap: the fast tier has no
visual tree and the rest of the Unity tier asserts no appearance, so "built but invisible" was a
state nothing in the project could see. Three elements gained names (`HudShell.StoreTabUnderlineName`
and its two neighbours) so a test can find them without the shell opening up its fields.

### 2026-09-21 — Every screenshot tool has been photographing an empty sky (P3-adjacent)

Writing `ShelfCheck` to look at the new rack produced four pictures of blue sky with the goods
floating in it: no ground, no trees, no shelf. The reflex was to debug the new script. **Running
`WallCheck` instead — a tool that has worked for days — produced the same empty sky**, and that
control is the whole diagnosis.

`8b5ecfee` gave the renderer a **meshing budget**: eleven chunks a frame, so that an edit cannot
stall the frame remeshing nine hundred. It is correct, it is measured, and it shipped with the one
exception it needs — `PrimeAll`, an unbudgeted walk the composition root calls before the first
drawn frame, whose own doc comment says a budgeted first frame *"would draw almost nothing and the
board would arrive in instalments"*. **The composition root is the only caller.** A check script
builds its own renderer and camera, renders four or five frames, meshes forty-odd chunks of several
hundred, and photographs the rest of the board as sky. There are twenty-one such scripts and the
game itself is unaffected, which is why nothing went red.

**The general shape: a new budget needs an audit of everyone who drives the thing by hand, not just
the one caller you were thinking about.** The exception was written for the loading screen and the
loading screen got it. Nobody asked who else renders without a composition root — and the harnesses
that answer "how does this look" are exactly the callers that do, because not having the game's
wiring is the point of them.

**And a broken screenshot tool does not look broken.** It returns a well-formed PNG of a plausible
sky at the requested angle, and this project's runbook for a report about how something looks
starts by taking one. The failure mode is a confident wrong answer drawn from a real photograph of
the wrong thing, which is `docs/lessons.md`'s "a plausibly wrong result is worse than an obviously
broken one" with a camera attached.

**What now stops it:** `ShelfCheck` calls `renderer.PrimeAll(start.Y, slice)` before its first
shot, with a comment saying why. **The other twenty scripts in `Assets/Editor/Odyssey/` still need
the same one line and are still blind** — a mechanical fix held out of a shelf PR rather than
forgotten. The durable version is for the shot harness to own the prime instead of each script, so
that the twenty-second tool cannot be written without it.

### 2026-09-21 — The warehouse measurement failed on the one machine that draws no warehouse (P13)

CI's Unity tier went red on `FrameTimeTests.TheWarehouseCostsWhatItHolds` — the measurement
`30-shelves.md` §8b owed, written the same day: 34,827 instances against the 35,027 its own control
demanded. The test passes on this machine. It has never passed on the runner and never could.

**Nothing about shelves was wrong.** `ChunkRenderer.RenderThings` asks `module.UsesArt` before it
looks at anything else, and draws the deliberately ugly stand-in cube when the answer is no —
through `DrawMarker`, which increments `DrawCalls` and *not* `InstancesDrawn`, because a marker is
not an instanced submission and counting it as one would put a fiction in the frame budget. The
runner has no `Assets/Synty`, so all three hundred and twenty stacks took that path, the contained
branch the test exists to measure was never reached, and the control — *each stack is at least one
instance* — was measuring a warehouse that was not drawn. With the packs present the same run reads
43,935 → 45,015 → 45,015 instances and 1,128 draw calls either way, which is the number that was
wanted.

**The general shape: a measurement whose subject is absent is not a failure, and must say which it
is.** This is P13 seen from the test side — the asset cannot draw the thing the code asked for, and
the fallback is silent — with the twist that here the silence is *correct behaviour* and only the
assertion is wrong. Two other tests already knew: `FigureCapTests` asks `PawnFigureDirector.Enabled`
and `PortraitLightingTests` asks `PortraitStudio.Available`, both with a comment saying why the
catalogue is the wrong question. Items had no such question to ask, so the new test could not have
asked it.

**What now stops it:** `ChunkRenderer.ItemArtResolved(defIndex)` — the item-side pair of those two,
returning the identical predicate the marker branch tests rather than a restatement of it — and the
warehouse case ignores itself on the machines where wood resolves to no art. **Ask it of any new
measurement in the Unity tier**: *would this number be the same on a machine with no packs, and if
not, does the test know?* The fast tier cannot see the question at all, and a green local run is
exactly what makes it invisible.

### 2026-09-21 — The warning moved the rows it was about, and the pane was an action stale (P1)

Owner, on the storage pane's first look: *"when I clicked off all the categories a message appeared
about colonists ignoring the zone — but this moved the controls/components — these should stay
fixed."*

**The obvious fix was half of it.** The two notes were the first children of the scrolling list, so
unticking the last category dropped every row by the height of the band, under the cursor that was
working down them. Moving the band below the list made it **worse**: the inspect panel is anchored
to the bottom of the screen and grows upward, so the band shoved every control **up by 90 px**. The
answer is that the band's height comes out of the *list*, not out of the screen — the pane is the
same height either way, the list's top edge does not move, and the scroll view simply shows less.

**The general shape: a panel that grows from an anchor has no free edge.** Adding anything to a
bottom-anchored panel moves everything in it. "Put the message somewhere else" is not a layout fix
unless something else gives up the same space. Ask where the space is coming from, and if the
answer is "the panel gets taller", ask which way it grows.

**And the test found a fault nobody had reported.** Driving the pane's own Clear all button with the
game running, the simulation accepted 0 of 7 commodities and the pane still showed all seven ticked
and no warning. `SendStorageCommand` refilled synchronously after submitting, on a comment saying a
storage intent "applies while paused, so the answer is already true by the time the next frame
draws" — true while paused, false while running, when the intent queues for the next tick. Nothing
refilled the pane again, so **every press was one action stale**, which is indistinguishable from a
button that does not work. P1: one rule with a comment that was right about one mode and quoted as
though it were right about both. `SyncStoragePanel` now rebuilds on a signature change, hung off the
refresh that already runs fifteen times a second.

**What now stops it:** `ZoneInspectTests.ClearingEveryCategoryDoesNotMoveTheRowsThatDidIt` — the
list's top edge, the first row's top edge and the pane's top edge are all unmoved across the press,
the warning is below the list, and its text fits the reserved band. Nothing else can see any of it:
the fast tier has no visual tree and the model does not know where anything is drawn, which is why
a layout that shifts under the pointer reached a playtest. `docs/design/26-storage.md` §12.

### 2026-09-21 — A meals-only store kept its rocks, and then took no meals (P14, P1)

Owner, after painting a stockpile and setting it to meals: *"the colonists left the rocks already
there and left the meals not hauled out in another place … I expect the colonists to ensure that
all those tiles are occupied by meals or nothing, not leave rocks in there."*

**Two symptoms, one cause.** A cell holding a rock has no space for a meal, so every cell a
refused thing squats in is a cell the store cannot use. Reproduced on the bare fixture before
touching anything: a two-cell meals-only store with a rock in each took **0 hauls in 10,000
ticks**, and the meal on the grass never moved. With one rock and one free cell the meal *was*
hauled — which is why the report read as two faults.

**The filter was a rule about the door.** `StorageSettings.Accepts` governed what could be carried
*in* and nothing at all about what was already lying there. `HaulWorkGiver.StoredPriority` already
said in its own comment that a refused thing "is not stored at all, only in the way" — but that
only ever let it move to a store that *would* take it, because of where it was asked: `ColonyItems`
buckets loose against stored by whether the cell is in a zone, so a refused thing is bucketed
**stored**, and the stored pass is the re-stow, which `TryGiveJob` runs only when nothing loose is
waiting. When no store would take it, `dest < 0; continue` — there was no third answer.

**The fix has two halves.** A refused thing is scanned in the *first* pass beside the loose things
(not re-bucketed: that would make `ColonyItems`' buckets depend on the filter table and put the
lister split into the save). And when no store will have it, it is carried out to open ground —
the clause that already existed for a thing standing on tilled soil, now reached by both cases
through `HaulWorkGiver.InTheWay` and one `ClearanceRadius`.

**And the same bug had a second entrance (P1).** `PawnContext.NotZoned`, the predicate the
clearance searched through, knew only about **growing** zones — while both of its call sites said
in their own comments that they wanted ground *"outside every zone"*. A rock lifted off a field
could be set down inside a meals-only stockpile and stay there for ever. One rule, two owners: the
name and the doc comment said one thing, the call sites said another, and neither was tested. It is
`PawnContext.OpenGroundFor(defIndex)` now and it asks about the thing as well as the cell — a store
that *accepts* the thing is a home, not an obstruction.

**What now stops it:** six tests in `StockpileTests` under *what a store refuses*, including the
compound case, the do-not-evict-into-another-refusing-store case, the prefer-a-real-home case, a
shuttling guard, and the player's actual gesture (narrowing a filter on a store that already holds
something); plus `GrowingJobTests.AFieldBlockerIsNotClearedIntoAStoreThatRefusesIt` end to end and
`OpenGroundIsNotAStoreThatRefusesTheThingButMayBeOneThatWantsIt` on the predicate alone.

**Three tests in the area were not running at all.** `AFieldBlockerIsClearedToTheGrassWhenNoStore-
WillTakeIt` — written for the owner's 2026-09-20 stall — `AThingOnTilledSoilIsClearedBeforeANearer-
Pile`, and the new one, all searched the four orthogonal neighbours of the start for a free cell,
and the scenario's own meal piles occupy all four on the fixture's seed. The `Assume` behind it
made them **inconclusive**, which `dotnet test` prints as "Skipped" and the summary counts as
nothing: the tier said `Skipped: 0` while fifteen tests returned no verdict. They search ring by
ring now and assert rather than assume. The first one, allowed to run for the first time, threw
immediately — it asked which zone cell **-1** was in, because it waited for the blocker's old cell
to empty and a carried thing has no cell.

**No golden moved, and that is the gap rather than the reassurance.** Every zone in every golden is
founded at *Everything*, so the state never arises there. `docs/design/26-storage.md` §11.
### 2026-09-21 — A colonist sealed inside a wall, and a board re-meshed to draw one cell

Two reports in one message. *"When colonists build a wall — sometimes they get stuck inside the wall
itself"*, and *"there is about a second or 3 delay when the object appears when it's built"*.

**The first was real and permanent.** A build site is walkable until the instant the building
exists, and `ConstructionGrid.Raise` asked nothing about who was standing in it. Once the cell is
blocking, no path can start in it and none can end in it, so the colonist never got out — the
owner's own answer to "does it resolve itself" was *"stuck forever"*. Fixed in three parts
(`docs/design/30-nobody-in-a-wall.md`): a detour that keeps passers-by out of the cell, a guard that
waits for a walker and moves a loiterer, and a per-tick sweep that frees anybody already inside
solid world — which is the only thing that can help a save written before the guard existed. **The
sweep is the part worth copying**: a guard is a rule about one edit, and the owner asked for a
statement about the world.

**The second was measured rather than reasoned about, and the measurement disagreed with the
reading.** The publish seam is next-frame — a raised wall is in the mirror on frame 0 and drawn on
frame 1 — so the seconds are not the sim, the snapshot or the mesher. What the probe did find was
P15 above: one global version, 45 chunks re-meshed for one cell, 12.53 ms in that frame. That is
the *glitch* half of the report and it is fixed; the *seconds* half is still open and the leading
candidate is the editor's asynchronous shader compilation, which a batch run cannot reproduce.
`docs/design/06-rendering-and-camera.md` §6c.3.

### 2026-09-20 — "it hovers, then frames drop", and the quadratic underneath

Owner, from a Play session while spawning colonists: *"it seemed to hover 1.7 ms no matter the
colony size but then frames dropped after so many colonists. I think at pretty high numbers."*

Both halves were true and neither meant what it looked like. The hover is `World` — the board, its
chunks and the surround — which is flat at 1.9–2.5 ms whatever the colony does. The drop is not
the simulation (0.31 ms of tick at 384 pawns), not the draw calls (1,243 to 1,324 across a 48-fold
colony) and not the figure ceiling being too low. It is `PawnPose.Of` scanning **every other
pawn** for the crowd sidestep, once per posed pawn, every frame: 64 x N for the capped figures and
(N-64) x N for the instanced stand-ins. The knee is at the ceiling because that is where the
quadratic term is born.

**The shape: a cost that is invisible at every size anybody tests at, and cubic-looking at sizes
nobody does.** It is P12, and the reason it is worth a pattern of its own is the check it
suggests — the influence radius. `CrowdFarRadius` is 3.0 m against a 2.5 m cell, so all but a
handful of the pairs contribute exactly zero and are computed anyway, which makes a spatial index
an **exact** rewrite rather than an approximation.

**Found by** `FrameTimeTests.TheFrameAgainstColonySize` and `OdysseyBootstrap.FrameSectionMs`,
both written for this report. **Not yet fixed** — held as the next unit so the sweep stands as its
before. `docs/design/06-rendering-and-camera.md` §6c.2, `25-pawn-steering.md`.

### 2026-09-20 — the pass that was free because it never ran

Not a player report: a benchmark reading. A new `FrameTimeTests` case put 901 mine orders on the
board and measured 4.85 ms against the bare meadow's 4.86 — which reads as "the mark pass costs
nothing" and is one of the two answers that should never be believed on sight (the other being a
five-fold regression). The orders were real: placed, published, counted in the snapshot. They
were simply **outside the band the pass draws**. `DrawStandingOrders` filters every order to
`LowestSelectableLayer .. HighestSelectableLayer`, the drawn slice was 8..15, and the case walked
the board row-major from `z = 1`, which is a strip of lower ground. Nothing drew and nothing
said so.

**The shape: an instrument that can measure nothing and report a number.** It is the sibling of
"count the pass, or the budget cannot see it" — there the pass ran and was uncounted, here the
pass was counted and did not run. Both produce a plausible number from an empty measurement, and
a plausible wrong result is worse than an obviously broken one.

**Stopped by** `ChunkRenderer.CellPlatesDrawn`, printed on every `[FrameTime]` line and asserted
greater than zero by the case that depends on it, and by the case asking the rig for the band
rather than assuming the surface is in it.

### 2026-09-20 — two editors on one machine, and a frame number that meant nothing

Also not a player report. The first three frame-time readings of the session had the city canary
at 4.01, 2.86 and 2.71 ms against its 2.01 ms record, and the mark pass read 1.57 ms, then
0.81 ms, then 0.19 ms on runs of the same code. The machine was running a sibling checkout's
PlayMode suite, then its player build, then an editor importing a third worktree. **Every
absolute was inflated and every cross-run difference was noise.**

**Stopped by** measuring the before and the after **inside one run, seconds apart**:
`TheMarkPassCostsWhatItSubmits` times one meadow three times and quotes the differences, and
`ChunkRenderer.InstanceCellPlates` switches the submission strategy without touching the
geometry. Cross-run comparison on this machine is not a measurement, and the canary was already
in `docs/lessons.md` saying so.

### 2026-09-20 — Half the colony slept on one knee, and the number said nought (P11)

Owner, watching the sleepers after the length fix landed: *"the body isn't quite flush on to the bed
surface but the pillow head is placed nicely enough."*

`SleepPose.Lift` had been tuned until the lowest drawn vertex anywhere on the mesh just touched the
mattress — measured, 0.00 m and +0.02 m on the two side postures, which is a centimetre and reads
as correct. Measuring the **trunk** alone, the band of baked mesh between the spine and the neck,
gave **+0.09 m and +0.12 m**: on a side sleeper the lowest vertex is a drawn-up knee, and seating
it props the whole body up like a kickstand. The two supine postures were genuinely flush and
always had been, which is why the fault survived a contact sheet — half the pictures were right.

**The shape: a true number about the wrong part of the subject.** Unlike the rest of P11 there is
no clamp and no missing measurement to find; both figures are real and correctly computed, and the
aggregate simply answers a question about extremities when the question was about a trunk.

**Stopped by** `ShoulderPerBody` 0.152 → 0.109, set against the trunk, and by `SleepProbe` printing
both clearances side by side so the disagreement is visible rather than inferred. The deliberate
price is a knee pressed about 0.10 m into a 0.30 m mattress. `docs/design/20-beds.md` §7d.

### 2026-09-20 — A colonist was laid down a sixth of her own length (P5, P11)

Owner, third report on the same symptom: *"colonists are resting in the centre and hanging off the
bed and sometimes even off the bed … it should know to always put the head onto the first tile and
then the rest of the body goes on the 2nd tile."*

The simulation was measured first, because the two previous rounds had both ended in the sim: three
colonists, three player-built beds, three days, four seeds — **every sleeping tick on the head cell
of a real bed, none off one**. So the sleeper was in the bed and the drawing was wrong, and the
drawing is `SleepPose.Place`, whose arithmetic had been read and pronounced correct twice.

It was correct. It was being handed a body 0.38 m long for a colonist 2.49 m tall.
`SleepPose.BodyLength` was `StandingHipHeight * 1.9`, and `StandingHipHeight` is
`hips.position.y - transform.position.y` where `hips` is
`animator.GetBoneTransform(HumanBodyBones.Hips)` — **which on the Synty humanoid avatar is a bone
literally named `Root`, sitting at the model origin, with the real pelvis as its child.** Nought on
every one of the sixty-one characters, so the `Mathf.Max(0.2f, …)` clamp was the whole of the
answer. The root of the lying figure went 0.38 m past the pillow and the rest of her — 2.1 m —
extended the other way, off the head end of the bed and on to the floor. Measured along the bed:
body `[−2.57, 0.29]` against a frame of `[−1.05, 3.55]`.

**The shape: a measurement that returns a plausible number for a question it never answered.**
Nothing downstream could tell, because 0.38 m is a length and every line that consumed it worked
perfectly. The clamp made it worse by turning "I measured nothing" into "20 cm".

**Why the test written for exactly this missed it.** `ASleeperLiesWithinTheBedsOwnTwoCells` was
added in the previous round to assert that a sleeper fits the bed, and it passed. It walked a range
of plausible **hip heights** — 0.70 m to 1.30 m — and checked the body each implies. The value the
game passed was 0.2 m, which no range starting at 0.70 m can reach. A fixture constant that is a
reasonable value for a quantity is not evidence that the quantity is reasonable.

**Stopped by** measuring the body instead of deriving it: `FigureBuild.Height` bakes the posed mesh
and takes the sole and the crown, the idiom `MeasureSole` already uses. `SleepPose.BodyLength`
guards anything outside 0.5 m to 5 m. The span test now walks the body length rather than a hip,
from far too short to far too long, and `FigureBuildTests` instantiates a real rig and asserts the
measurement looks like a person and that the avatar's `Hips` bone is nothing like a body length.
`scripts/unity.sh exec Odyssey.EditorTools.SleepProbe.Run` prints the whole of it.
`docs/design/20-beds.md` §7b.

### 2026-09-20 — The soil had borders, and the borders were the frame budget

Owner, with a screenshot: *"Remove the borders from dirt/soil tiles — each tile must have some
kind of border/shade ... or could be something overlaying and reacting."* The second guess was
right.

A growing zone was drawn as the ground's own module laid over itself, tinted and translucent. It
carried a 1.01 scale so neighbours **overlapped rather than met**, on the reasoning that an
overlap of one tint is invisible by construction. That is true of an opaque overlay and false of
a translucent one: alpha blending is not idempotent, so at alpha 0.78 a doubly-covered band
composites to 1-(1-0.78)^2 = 0.95 and the dirt showing through falls from 22% to 5%. Hence a dark
line on every **interior** edge and none on the outside edge — which is the shape in the
photograph, and the thing that identifies the cause without reading any code. Under it,
`MarkLift` did the same again in miniature: lifting a cover raises a *box*, whose four sides then
stand proud of the neighbouring soil by exactly the lift.

**The shape: a geometric fix and a blending fix that are incompatible, each correct alone.** The
overlap was added to close a sliver; the sliver's real cause (the cover drew the plain default
block instead of the drawn variant clump) had already been fixed, so the overlap was belt over
braces, and the braces were made of alpha.

**Settled by four photographs rather than argument** — bug, overlap removed, alpha 1.0 (seamless,
so the meshes tile with no gap), alpha 0 (seamless, so the ground has no seam of its own). Two
controls that each eliminate one candidate outright.

**And the same pass was 3.67 ms of a 5 ms frame**, one submission per zoned cell, incrementing no
counter (P10). Because the look fix had made the cover provably the ground's own mesh in the
ground's own place, the answer to both was to stop drawing it twice.

**Stopped by** `TintCode.TilledBase` — worked soil is a bit on the terrain bucket's tint —
and by `GrowingRenderTests.AZoneCoverSitsExactlyOnTheGroundItCovers` before it was deleted,
`ABiggerFieldAddsInstancesRatherThanDraws` after. `docs/design/22-growing.md` §6a,
`06-rendering-and-camera.md` §6c.

### 2026-09-20 — The carrots did not disappear; there was nowhere to see one

Owner: *"I thought people picked up the carrots ... but they seemed to disappear now?"*

They did not. The ten-day field soak accounts for every one — 116 harvests at five apiece, 501
still on the map, the rest eaten — and the contact sheet shows the pile spawning off the soil and
drawing correctly. `LedgerModel` counted meals, wood and salvage and nothing else, so the whole
visible life of a carrot was: a pile appears, a hauler carries it to the store or a colonist eats
it, and then nothing in the interface mentions carrots again.

**The shape: a complete feature with no readout, reported as a simulation bug.** For a commodity
the player is meant to decide on, "cannot be seen anywhere" and "does not exist" are the same
report. Worth remembering when a report says something vanished: ask what would have shown it.

**Stopped by** a Carrots row counted wherever it lies — the ledger is the colony's count, not the
storeroom's — and `LedgerModelTests.TheFieldCropIsCountedWhereverItLies` /
`TheCropRowStandsEvenWithNoCarrotsInIt`. Stone, iron ore and coal are still uncounted and have
the same report waiting.
### 2026-09-20 — Two glyphs the fonts do not have, in two panels, neither ever drawn (P13)

Not reported. Found while reviewing PR #145 by asking a question no test asks: *are the characters
this code writes actually in the font that draws them?*

The Work tab's Simple mode drew its two readings as the characters U+2713 CHECK MARK and U+2715
MULTIPLICATION X, in a `Label`. **Archivo Narrow's cmap contains neither and IBM Plex Mono contains
only the tick.** The cell glyph takes the mono face (it is `numeric: true`, for the digit beside
it), so every *won't do* cell drew a blank; the legend takes the UI face, so both of its swatches
drew blanks. The whole of Simple mode was two empty columns of boxes.

**And the same character was already on `main`**, in the bed-owner picker `HudShell.Inspect.cs`
shipped by PR #141 — `BedPickerMark.ThisBed` is a tick in `HudTextRole.Body`, which is Archivo
Narrow, which does not have one. The mark that says *this is the bed this colonist owns* has been an
empty column since it was written, and that panel is still on the playtest queue unplayed.

**Why every test passed.** The fast tier compiles no text engine; the Unity tier compiles one and
asserts no pixels; the PlayMode smoke test counts framed regions. A `Label` whose `text` is `"✓"`
has that text in every assertion anybody could write about it. The picture is the only place the
fault exists and nothing in either tier looks at a picture.

**Stopped by** two things. The shapes are now drawn — `HudGlyphKind.Check` and `HudGlyphKind.Cross`
on the same 24-unit grid as every other chrome icon, which is what this project does with icons
anyway — and `HudFontTests.EveryCharacterTheHudWritesExistsInBothFonts` reads both `.ttf` files'
`cmap` tables and fails the **fast tier** on any non-ASCII character in a string literal under
`Odyssey.Hud` or `Odyssey.Presentation` that either face cannot draw. It is held to *both* faces
rather than the one that happens to draw it today, because a label's face is picked by its role and
roles move. Every other character the HUD uses — `· × – — • … ›` — is in both, so the rule costs
nothing to hold. The test found the bed-picker instance on its first run.

### 2026-09-20 — Woken for a bed, and sent to work instead

Owner, second play day: *"when I assigned someone else to a bed — everyone just started going back
to work."*

`JobSystem.GetOutOfTheWrongBed` ended the sleep of everybody the assignment concerned — correctly,
and only them — and handed each to the think tree. The tree's sleep branch is gated on rest below
the `seekThreshold` (280 of 1000); a sleeper wakes at 950. A colonist got up at 600 was, by that
gate, not tired, so `WorkThinkNode` took her. In a colony of three the two people concerned are
"everyone".

**The shape: a rule that reuses a decision made for a different question.** "Should she start
sleeping?" and "she was asleep; where should she continue?" share a chooser but not a gate, and
routing the second through the first's gate was invisible while the gate happened to be open.

**Why five tests missed it.** All five assigned the bed within a tick of her lying down, at the
rest of 40 the helper sets, so the gate was open in every one. A test that probes a range at one
point proves the rule at that point. The repro sleeps her to `seek + 300` first.

**Stopped by** the sweep resuming the sleep itself, in two passes (end every affected sleep, then
choose again, so that the bed just given to B is not still reserved by A when B chooses), through
`CriticalNeedsThinkNode.TrySleep` made public. `BedTests.AColonistWokenMidNightGoesToTheBedSheWasGivenNotToWork`,
`GivingOneSleepersBedToAnotherMidNightMovesThemBothAndWakesNobodyElse`. `docs/design/20-beds.md` §7.

### 2026-09-20 — The board had two sizes, and only a write could tell

Not reported, and not reportable: it was silent until something wrote.

`OdysseyBootstrap.BuildSession` built the **chunk grid and the render model** from
`new GridSize(sizeX, sizeZ, layers)` — the inspector's numbers — and then built the **world** from
`sizeOverride ?? size`, the setup page's. So a new game on any board but the scene's default had a
mirror and a chunk grid of one size over a world of another. Every cell index near the far edge
landed outside them.

**Nothing had ever written to the chunk grid during a world build**, so it sat there. The moment
the scenario started raising real beds (`ColonyScenario.RaiseAStartingBed`, same day) three
PlayMode tests threw `IndexOutOfRangeException` out of `ChunkGrid.MarkDirty` — with the bounds
check one line above it *passing*, because `ConstructionGrid.MarkChunksAround` asked `ctx.Size`,
the cell grid, about a write going to the chunk grid.

- **A plain P1**, with the extra twist that the disagreement was **latent behind an unexercised
  write**. Two owners for one number, agreeing in every configuration anybody ran.
- **The fast tier could not see it.** `PawnContext.Chunks` is null headless, so
  `MarkChunksAround` returns on its first line. 769 Sim tests were green while this was live.
- **Stopped twice over**: the size is decided once in `BuildSession`, before the chunk grid and
  the mirror are built from it, and `MarkChunksAround` now asks the grid it is about to write to
  rather than the one beside it. The second half is the one that does not depend on remembering
  the first.
- **The lesson for the next change like it:** a new *write* into a structure nothing wrote to
  before is a probe. When one starts failing, suspect the structure's provenance rather than the
  new write — the write is usually correct and merely first.

### 2026-09-20 — Colonists slept beside beds, and a phantom cell was why

Owner: *"some colonists still sleep off the bed … it looks like it's trying to rest them in the
first tile in some circumstances where they are hanging off the bed."*

`ColonyScenario` put five entries into `ColonyItems.Beds` that were **cells and nothing else** — no
edifice, no record. Correct when it was written, because a bed was then a property of a cell.
The day beds became furniture it became a lie with three consequences, and only the third was
reported: the cell cannot be seen, it cannot be owned (`AssignOwnerAt` refuses a cell with no
edifice, so bed ownership was dead on every bed the colony started with), and a colonist who
"sleeps in it" is laid out by the **ground** pose — flat on the grass, centred on her cell, along
her last yaw. Next to a real bed that is a colonist hanging off it.

**The shape: a value that meant one thing when a cheap representation was all there was, left in
place after the real representation arrived.** Not two owners disagreeing — one owner holding two
*kinds* of thing in one list and every reader assuming the richer kind.

**The measurement is the point.** Reading the pose arithmetic said it was correct, and it was: a
sleeper aimed from a real bed lies between −1.55 m and +0.26 m of a bed spanning ±2.30 m. Three
colonists, three built beds, three days, counted: one spent **all 53,222** of her sleeping ticks
off a bed and a second **17,399** of hers, every one within three cells of a bed she never used.
With the phantom cells gone, all three slept on a bed for every tick.

**Stopped by** `ColonyScenario.RaiseAStartingBed` — a starting bed is a real bed — and
`BedTests.EveryCellTheSleepChooserKnowsHasABedInIt`, which asserts the invariant directly rather
than the symptom. `docs/design/20-beds.md` §7a.

### 2026-09-20 — Two colour tables for one tool, in two assemblies

Owner: *"the placement shouldn't be red — it should use the same colour as deconstruct (the orange
colour) … match the orders blueprints/placement titles to the color assigned on their toolbar."*

A **P1**, and the plainest one yet. `HudTheme.PinnedActionHue` said what a palette chip was;
four `Color` constants in `OdysseyBootstrap` said what the board was. They disagreed on two of the
four tools. Deconstruct was orange on the chip and **red** on the board — the interface's own
colour for *cancel* — so the panel and the cursor told the player two different things about which
tool was in their hand, for months.

**Why nothing caught it:** the board's copy lived in `Odyssey.Presentation`, which the fast tier
does not compile, and the only thing asserting it was a Unity-tier test of *totality* — every kind
maps to something — which a wrong colour satisfies perfectly.

**Stopped by** `Odyssey.Hud.OrderColours`, one owner in a Unity-free assembly, with
`PinnedActionHue` delegating to it and `OrderColoursTests` in the **fast tier** asserting that the
chip, the cursor and the board mark are the same hue for every tool, and that no two tools look
alike on the board. `docs/design/16-cancel-and-deconstruct.md` §6b.

### 2026-09-20 — The state hash could not see a stockpile

Not reported; found by a control written for an owner ask about saves.

`ColonyItems` hashed its things and **not** its stockpile zones or its bed list — both of which it
had been *saving* since they existed, which is the worse of the two ways round. Save and hash are
meant to cover the same set, and `WorldRoundTripTests` proves a save by **comparing hashes**: a
zone whose filter failed to round-trip would have come back accepting everything, hashed
identically, and passed. The goldens and the determinism harness were blind to it too.

**This is OQ-50's shape exactly** — the whole cell grid sat outside the hash for a year — and the
way to find it is the same: do not read the contributor, flip one bit of the state and ask whether
the number moved.

**Stopped by** hashing both lists, and by
`OrdersSurviveASaveTests.EachOrderMovesTheStateHash`, which walks every kind of player order —
designation, blueprint, bed owner, zone cells, zone priority, zone filter, forbidding, work
priority — and asserts each one moves the hash. It found this on its first run.

### 2026-09-19 — The volume you set on the title screen was never saved

Found while wiring the title-screen bed to the player's faders; never reported, and not the sort of
thing a player would report.

`SettingsPresenter.ApplyBusDb` opened with `if (audio == null || _director == null) return;`. The
audio director is built from a world, so `audio` is null every moment before one exists — and the
settings page is perfectly reachable from the main screen. Moving a volume slider there wrote
nothing to `AudioSettingsStore` and therefore nothing to PlayerPrefs.

**The shape worth recognising: a guard that protects the last line of a method by skipping the
first two.** The null check was correct about the director and wrong about everything above it.
Ask of any early return: *which of the things below this line actually needed the thing I am
checking?*

**Stopped by** writing the store unconditionally and pushing to the director only if it exists.
`MenuAmbience` reads the same store, so the title screen's Music fader now moves the bed drawn
under it. `docs/design/17-start-flow.md` §12.

### 2026-09-19 — Eight seconds of fade that lasted one frame

Caught by its own test on the first run, and recorded because of *which* test caught it.

`MenuAmbience` uses a long fade the first time it arrives and the ordinary one afterwards. The
latch for "has arrived" was set on the first `Sync` rather than on the level reaching full, so the
eight-second arrival fade governed one sixtieth of a second and the remaining 7.98 ran at the
four-second leaving fade — the bed up in half the time it was written to take.

**Both endpoints were correct.** Silent at nought seconds, full at eight. Any test asserting the
ends would have passed. The one that failed asserted the level was between 0.2 and 0.6 at
four-tenths of the way through, and got 0.80.

**The lesson is about the test, not the code: a fade is a curve, so assert somewhere along it.**
The same applies to anything with a shape — an ease, a ramp, a cost curve. Checking that it starts
where it should and ends where it should tests the clamps.

### 2026-09-19 — The alert chime that could not fire (P1)

Not reported as a bug. The owner said the alert sounds were *nasty* and supplied replacements; the
reason they had rarely been heard turned up while wiring the new ones in.

`AlertWatch` decided when a colonist was starving by testing the published food need against
`StarveThreshold = 12`, with a comment reading *"food, in the published 0–100 units"*. Food is
published **0–1000**, and `AlertModel.StarveAt` — the threshold the red panel row uses — is 120. So
the chime fired at a hundredth of the food the warning is for: a colonist reaching 1.2% is one who
is already dying.

**Three unit tests covered the watcher and all three passed**, because each fed it literal `13`,
`5` and `0` rather than a pawn from a published frame. A test that restates the constant it is
testing cannot catch the constant being wrong — it can only catch the *code* being wrong. The unit
under test was the number.

**Stopped by** deleting the second owner outright. `AlertChimeWatch` reads the alerts panel's own
rows, so "what is an alert" has one answer, dismissal silences the sound for free, and a scale
change cannot desynchronise the two again. `docs/design/24-alert-sounds.md`.

### 2026-09-19 — Every dear step began with the figure standing still (P5)

Found by a smoothness test, never reported, and in the game since any step cost more than 100.

`MovePercent` is a whole percent of the step. A 240-tick hop therefore spends its **first 2.4 ticks
at nought percent**, and every reader gated on `MovePercent > 0` — the pose, the climb phase, the
gait hold — treated the pawn as standing still through them, then caught it up in one frame: **30 mm
against the 10 mm a frame that climb moves**. On a flat cell, which costs exactly 100, it cannot
happen at all, which is why five sessions of watching colonists walk never showed it.

**A new shape worth naming: a threshold that is exact for the common case and wrong for every other
one.** `MovePercent > 0` is not a test for "moving", it is a test for "moving *and* the step is cheap
enough that a percent has ticked over". Ask of any threshold on a published number: *what does the
number do on the case this was not written for?*

**Stopped by** `PawnView.Moving`, which asks the per-mille progress, and by
`HopArcTests.AClimbIsSmoothFrameToFrameAllTheWayUp` — which measures the **variation** between
frames rather than a maximum, because a hitch and a rhythm both pass a maximum comfortably.

### 2026-09-19 — One ramp, two steps, two different prices (P1)

*"The slowness needs to start happening much earlier when entering the beginning of the tile … you
slow down and then you seem to still go slow on the flat so it's out of sync."*

**A seam, not a curve.** The bank spans one cell; a step spans two half-cells. So the drawn ramp was
split down the middle of the foot cell between two steps priced for different things — the walk in at
flat-grass price (drawn at 1.9 m/s, faster than walking) and the hop out at 240 (0.62 m/s, and it
kept charging that across the flat top). Both halves of the report are that one seam, seen from
either side.

**It is P1 wearing geometry.** One rule — what a slope costs — with two owners, and here the two
owners are two *steps* rather than two files. The question to ask of any cost: **does the thing it
prices line up with the thing that is drawn?** A step is charged between cell centres; art is drawn
between cell edges; those are half a cell apart, and anything whose appearance spans a cell will be
charged by two steps that know nothing of each other.

**Stopped by** giving the cell its own cost (`NaturalContent.CostClassSlope`, worth
`MoveCost.SlopeExtra` = `JumpUp − Orthogonal`, stated as a subtraction so the pair cannot drift) and
by `PawnPose.StepPace`, which spends each step's time where its climbing is. `TerraceSlopeCostTests`
asserts the two prices are equal; `HopArcTests.TheFlatsAreWalkedAndTheRampIsClimbed` asserts the ramp
is one speed from bottom to top with a walk either side.

**And a caution about goldens.** None of the three moved, which was luck rather than inertness: the
golden windows are a flat meadow, a start clearing chosen for being flat, and a city of pavement.
A cost change that moves no hash wants a direct test, not a shrug.

### 2026-09-18 — Every fixture had a bank in it, so nobody saw the 1.5 m teleport

Found while measuring a new climb, not reported by anybody.

**A hop up a step with no bank — against rock, inside a working, under a roof — snapped the figure
1.51 m in a single frame**, and had done since hops were first drawn. The cause is the ground clamp:
the cell a walker is *over* switches at the midpoint of a step, so with no ramp the ground under it
is flat for the first half and a whole layer higher for the second. Any height curve timed across
the whole step reached half its height and was then clamped the rest of the way at once. The mirror
of it, on a sheer drop, was 657 mm.

**A new shape worth naming: the fixture chose the case.** `BankFootingTests` measures exactly this,
five ways across a terrace, four hundred samples a step — and every one of its worlds is a *terrace*,
which by construction has a bank in it. The bank's ramp made the ground continuous and the fault
invisible. It is the same shape as `GroundRelief.Reset()` hiding the relief snap two days earlier:
a thorough test suite whose fixtures all share one convenient property.

**Ask of any fixture: what does it always have that the game does not?**

**Stopped by** `HopArcTests.ASheerFaceIsClimbedSmoothlyRatherThanInStrides` and
`ADropDownASheerFaceIsAsFastAsTheGeometryAllows`, which build a step of bare rock and assert the
fixture has no bank before measuring anything. The climb now completes by the midpoint and the fall
starts there.

### 2026-09-18 — A stride could not be drawn because progress was published as a whole percent (P5)

A climb drawn in strides stuttered: 134 mm in one frame where 50 is the budget.

**The cause was a layer away from the motion.** `PawnView.MovePercent` is a whole percent, and the
sub-tick term cannot smooth it — at 60 frames and 60 ticks a second there is about one frame to a
tick. A flat cell costs 100, so a percent a tick is exact and the quantisation has never been
visible; a 240-tick hop advances a whole point every 2.4 ticks, so the figure stands still for two
frames and then jumps a hundredth of the step. On the flat that jump is 25 mm. Concentrated into a
stride it was 134.

**Two wrong probes before the right one**, both worth remembering. The first sampled only at
`tickAlpha = 0`, so the two arms of the comparison were identical and the sub-tick term looked
innocent. The second sampled per percent — the very granularity under suspicion — so it reported the
quantisation as the motion.

**Stopped by** `PawnView.MovePerMille`, ten times the resolution, published beside the percent rather
than instead of it, and by `BankFootingTests.WorstJump` sampling per mille: an instrument coarser
than what it measures reports quantisation as a teleport.

### 2026-09-18 — Trees grew inside the hillside at the top of every terrace step

*"The flat side of the terrain where the height changes … things generate in those tiles … Trees
shouldn't be generated in those spots because they get clipped by this façaded terrain."*

**A new shape, and the one to watch for next: a façade with nothing behind it, in a cell the
simulation is free to fill.** A bank — the ramp drawn up a terrace step — fills the empty cell at
the foot of the step from floor to rim, and `Odyssey.Sim` does not know it exists, by design. Every
other façade in this project is drawn *on* ground that stays empty (relief, tufts, chips, banks of
the surrounding land); this one occupies a cell that worldgen scatters into and colonists walk
through. Ask of any new façade: **can the simulation put something where this is drawn?**

**It read as an art fault because the commonest case is handled.** `PawnPose` lifts a walking figure
onto the bank's surface, so colonists look right; only things that are never lifted — a generated
tree, a body lying down — are swallowed.

**Stopped by** `TerraceFoot.IsFoot`, the simulation's copy of the rule, which `TreePass` asks before
placing a tree. Two owners (P1) and allowed, because worldgen has no render mirror to ask — so
`TerraceFootTests` requires the two to agree in every cell of seven boards, four of which are cases
where the right answer is *no bank*. `docs/design/22-terrace-steps.md` §4 records what is still
unguarded: a colonist lying down in one, and an item dropped in one.

### 2026-09-18 — Typing a save name drove the game behind the dialog

*"When you type in during a save game the in game controls still work and can cause confusion."*
Typing `sss` panned the camera three cells south; a digit changed the game speed; a `c` armed the
Cancel tool behind the modal.

**A new shape, and worth naming: a gate in one input system cannot govern another that never sees
the event.** Every in-game key is *polled* out of `Keyboard.current` in six components' `Update`;
the text field is a UI Toolkit control that only ever sees what the panel routes to it. Focus is
real and does its job — it just has no bearing on a poll. Two systems, one keyboard, neither aware
of the other.

**It survived because it was half-fixed already.** The modal's scrim takes the *pointer*, so the
dialog behaved like a modal in the one dimension anybody checks, and the keyboard half looked like
it must be handled too.

**Stopped by** `HotkeyDirector.GameKeysLive`, which every poller now asks instead of keeping its own
copy of the guard (P: one rule with six owners — the second reason to sit a frame out would have had
to be written six times). The holder is a **token, not a flag**: focus moves as a blur and a focus,
in no promised order, so a bool is cleared by the field being *left* after the field being *entered*
set it. `HotkeyDirectorTests` holds both that and the mirror failure — a gate stuck *shut* is a game
that has silently stopped answering its keys.

### 2026-09-18 — Selecting a colonist repainted the whole wood (P9, P5)

*"A bug with the trees occurs when See through to selection/occlusion is set on. When I select a
colonist the trees change colours all around, but when I come away or switch the setting off it goes
back to the original colour."* With the console warning: `Property (_LeafDeepColour) exceeds previous
array size (31 vs 25). Cap to previous size.` from `ChunkRenderer.SolidProps`.

**It is one property block reused at two sizes.** A tree carries its four colours as per-instance
data rather than as a tint, so a bucket's colours ride in a `MaterialPropertyBlock` beside its
matrices. With see-through off, every bucket draws from **its own** block, written once at its own
size — right, always. With it on, a bucket standing in a sight line is partitioned into a ghosted
half and a solid half, and the solid half's colours are gathered into **one block shared by every
bucket on the board**. That block's array length was fixed by the first partitioned bucket of the
session, so from then on any bucket with more solid trees than that had its colours capped: the
trees past the cap drew whatever was left in the array. Move the selection and a different bucket
goes first, so the wood repaints itself. Nothing about trees, colour or the palette was involved.

**Fixed:** `ChunkRenderer.WritePadded` pads every write to a shared block out to
`MaxInstancesPerCall`, so the array is the same length every time and is never capped. The padding is
never read — a draw of *n* instances indexes the first *n* entries. It is the fix
`TreeMaterials.UniformProps` already used for the neighbouring reason (a block's array is indexed
from zero by every draw call, not from the instance offset). The per-bucket blocks of
`ChunkRenderer.PropsOf` are deliberately **not** padded: each is written once at its own size, they
are not shared, and every tree on the board would pay the wider upload every frame.

**Caught next time by:** `TreeColourBlockTests.ABiggerBucketAfterASmallerOneKeepsItsOwnColours`,
which writes 25 then 31 through one block — the reported sizes — and asserts the array length does
not move as well as that the colours come back. Asserting only the colours would pass on a block
that had simply not been reused yet.

### 2026-09-18 — Every floor seam drew a dotted line, and it was the tile's own rim (P8)

*"You can see these slabs leave small artifacts/lines or gaps that don't even up … you can notice
this when you look at the ground from certain angles — you see a slight issue with not being fully
flush."* Two screenshots: a wood slab field on the meadow at night, and a roof deck.

**It is the tile's rim winning a depth tie.** A floor is drawn one instanced plate per cell, and the
slab art is a plain box — measured, 2.5000 m across, its top face flat to the micrometre and the
full width of the piece. So two tiles meet exactly, and the top edge of one tile's **rim** lies
exactly in the plane of its neighbour's top face. Equal depth, so the rasteriser keeps whichever
fragment it likes; a rim takes almost no light under a 72° sun, so where it wins it draws a dot of
wood at four tenths the brightness of the deck. That is a dotted line along every seam and a dotted
grid over every floor, worst at a low camera pitch.

**Fixed:** `CellMetrics.FloorTile` draws a floor as a **sheet** rather than a plate — the rim
squashed to a tenth of a millimetre about the walking surface, so it is degenerate on screen and
generates no fragments to win with — and grows it 3 mm past its own cell
(`CellMetrics.FloorKnit`) so two neighbours overlap rather than share an edge. Both the built floor
(`ChunkMesher.EmitFloor`) and paving (`SurfaceContributor`) go through the one matrix. Measured on
the meadow, counting pixels much darker than all four of their neighbours: at the play camera's 48°
**470 → 16**, at a grazing 25° **884 → 35**, and on a floating deck at 14 m **73 → 3**.

**What it costs, and it is real:** a floor over open air loses its 101 mm of drawn thickness and its
lip reads as paper seen edge-on. A floor laid on the ground had 93 of those millimetres buried in
the block beneath it, so nothing changes there. The fix that keeps the lip is to draw a floor's edge
as a **fascia on the face**, the way a wall is already drawn on faces rather than as a cell; that
wants a panel module of its own and is not done.

**Caught next time by:** `ChunkMesherTests.AFloorTileIsDrawnAsASheetAtTheHeightAColonistWalksOn` and
`.TwoNeighbouringFloorTilesOverlap`. The first asserts *both* halves on purpose (see P7): flat, and
still a clearance above the cell floor plane, because squashing about zero instead is just as flat
and drops every floor back on to the ground it z-fights.

**Reproduced by `SlabFlushProbe`** (`scripts/unity.sh shot Odyssey.EditorTools.SlabFlushProbe.Run`)
— a wood floor laid on the meadow, shot at 48° and 25°, relief on and off. `SlabTopFaceProbe`
measures the art itself, turning Read/Write on for the one model and back off again.

### 2026-09-18 — The outer slabs were built first and fell (P1)

*"The slab was placed on the top level but then the colonists tried to build the most outer slabs
first which then landed a stone/steel looking tile 1 height below instead of where it was … could be
silently resorting to a failure."*

**The owner's first message, and every word of it was right.** Three sessions went on the *drawing*
of the grey tile below before anybody measured the collapse that put it there. The row below is the
drawing; this is the cause.

**Cause (P1).** The support rule has two halves that disagree about *time*. `AllowsSlab` accepts a
cell held up by slabs merely **ordered** around it (`SupportedByWhatIsPlanned`) — a promise about the
*finished* roof, and the whole of why a roof can be dragged in one gesture. Nothing enforced an
order of construction that honours it, and `BuildWorkGiver` hands out the **nearest** site. So the
far end of a bridge was raised while its supports were still blueprints, stood on nothing, was taken
by the solver, and `SupportSystem.Rubble` left debris in the floored cell below.

**The owner's rule:** rubble is for construction that was *destroyed*, never for construction that
never happened. A slab that cannot stand is not built yet.

**Fix:** `ConstructionGrid.SlabWouldStand` — would it stand *now*, asked of the built world with no
plans in it. `BuildWorkGiver.CanBuild` **defers** the site; `Raise` **keeps** it rather than
cancelling, because unlike the shaft rule beside it this is a race and not a permanent illegality.
`TheDraggedRunStillFinishes_BuiltFromTheWallOutward` guards the over-correction: the gate must defer
and never refuse, or the far half of every dragged roof disappears.

**The lesson is the expensive one.** The reporter described the mechanism correctly in their first
sentence — outer slabs first, nothing built, tile appears below — and three sessions were spent
explaining the *symptom's appearance* instead. **Read the report as a causal claim and test that
claim first.**

### 2026-09-18 — The grey tile was rubble terrain drawn on top of a wood floor (P1, P5, P6)

*"I can't seem to recreate this in a new game — could an old game cause problems?"*

**The fourth answer, and the one that holds.** The owner's own question was the right one. The save
from the photographed session:

```
the-lost-buckets-day-3.odyssey   Floor=Built/Wood 95, Paved/Wood 20   <- no stone on the board
                                 terrain Rubble 8                      <- eight cells
```

Eight rubble cells; eight grey plates in the screenshot. `PlayScene` registers
`Slab(ModuleIds.Terrain("Rubble"), "SM_Env_Ground_Tile_Half_03")`, so rubble **terrain** draws as a
grey street tile — the same art family as the stone slab, which is why three sessions kept
recognising it as one.

**Cause (P1).** "A floor hides what is beneath it" has one owner and a second site that never asks
it. `ChunkMesher.EmitScatter` obeys it — *"a built floor, a stamped deck and a deck plate all
equally hide what is beneath"* — and `SurfaceContributor` does not, so a cell holding both a surface
terrain and a floor slab draws both, coplanar. **A collapse guarantees that cell exists**:
`SupportSystem.Rubble` writes into `FirstFloorAtOrBelow`, which has a floor by definition.

**Why the pane and the picture disagreed, and the pane was right.** `CellDetailContributor` reads
`FloorStuff`; the floor really was wood. The grey on top of it was not a floor and has no
`FloorStuff` to report. Three sessions explained a *floor* — stale mirror, hole one layer down,
stone paving — and the answer was a second thing drawn in the same place.

**Fix:** rubble on a floor is lifted `CellMetrics.SlabLift` on to it rather than hidden. Hiding was
shorter and wrong: rubble refuses to be built on until cleared, so an invisible one is a cell that
rejects orders with nothing on screen to say why. Pinned by
`RubbleLyingOnAFloorIsDrawnAboveTheSlabAndNotInIt`, which meshes the same cell twice — with and
without a floor under the rubble — because an instance matrix carries its prefab's normalisation and
the relief varies with x and z, so nothing else isolates the lift.

**Caught next time by:** asking `Odyssey.SaveProbe` first. It reports terrain as well as floors now,
for exactly this reason.

### 2026-09-18 — The grey tile was stone paving all along (P5, P6) — *wrong, superseded*

> The row above is the real cause. Stone paving is real and two older saves hold it, but the board
> the owner photographed has no stone slab on it at all. Kept because the reasoning below is a clean
> example of P6 and was still not enough.

*"The slab was placed on the top level but then the colonists tried to build the most outer slabs
first which then landed a stone/steel looking tile 1 height below instead of where it was … you can
see it thinks this stone slab is a wood floor as well."*

**This is the third report of the same tile and it overturns the previous two rows**, which is the
row worth reading. The screenshot the last one asked for arrived: grey plates **coplanar with an
unbroken wood deck**, which the "surface one cell down" answer said was impossible.

**Cause:** the grey plates are **stone paving**. Measured by loading the owner's own saves
(`tools/dotnet/Odyssey.SaveProbe`): `timmy-test` holds 11 `Paved`/Stone beside 20 `Paved`/Wood and 31
`Built`/Wood, with stone at (72–75, 57, L11) touching wood at the same z. Every mixed deck in the
folder mixes *materials*, never layers, and the stone cells are always `SlabPaved` — the **Paving**
tool — and never `SlabBuilt`.

**Why three sessions missed it.** The three checks in the previous row are all *true*, and all three
answer one question — *can a wood floor draw as stone?* Not one asks **was it ever wood?** The
contrapositive was the whole answer and was sitting in the same paragraph.

**Three separate faults, none of them the renderer** (`15-building.md`):

- Two palette tiles both mean "floor" — `Paving` (filed under *Structure* **and** *Floors*) and
  `Slab` — and `BuildPalette._lastMaterial` is keyed **per sub-type**, so they remember different
  materials and nothing on either tile says which.
- `InspectModel.DescribeCellAt` titles a floor by `FloorStuff` alone, so paving and a structural
  floor are the same sentence and the word *paving* is never printed.
- Both slab catalogue rows carried `baseAtY: 0`, which is *no* placement rule rather than the one
  its own comment described, so each art landed on its artist's pivot. Measured: the street tile's
  top 25 mm **proud** of the plank deck's — the opposite of what the screenshots looked like.

**Fixed:** `ModuleEntry.topAtY` places a walked-on piece by its highest point, so every slab's top
face lands at one height, pinned by `EveryFloorSlabPutsItsWalkingSurfaceOnTheCellFloor`. The other
two are reported and not fixed: both are design calls. 25 mm will not on its own make a street tile
read as decking.

**And the first fix was wrong, in a way worth its own line (P7).** Levelling the slabs on to the
floor plane *exactly* made the owner's board z-fight within the hour — `FloorCentre` for cell y is
also the top face of the block filling cell y−1, so a slab flush with the plane is coplanar with the
ground paving is laid on. The plank deck's +0.008 was never slop; it was clearance.
`CellMetrics.SlabLift` is that clearance and `topAtY` applies it, so the rule owns it rather than
each row. **The first version of the test would have passed the flicker through**: it asserted the
slabs agreed, and levelling them all on to the ground's own plane satisfies that perfectly. It now
asserts they agree *and* that the shared height clears the ground.

**Caught next time by:** `Odyssey.SaveProbe`. Load the file before arguing about the picture.

**And a trap found on the way out:** `PlayScene.RebuildCatalogue` silently drops the `appearance`
block — 311 atlas swatch rectangles clothing the 61 colonists — so `CharacterSwatches.Classify` must
run after it. The rebuild exits zero and keeps all 138 rows and every prefab reference, so the loss
shows up in nothing but a line count.

### 2026-09-18 — A dragged floor built a ring and left a hole (P1, P4)

*"I specified wooden slabs to be built only on that floor but then it constructed stone/steel floor
on the floor below."*

**Cause:** the slab lift is per cell and conditional. Over a walled room the perimeter cells sit over
walls and lift to the storey above; the interior cells sit over open air and do not. Measured: twelve
cells `None` at L12 and four `NotPermitted` at L11, **from one box**. And the drag's ghosts were
stamped at the box's own layer with no lift at all, so the preview could never have shown it.

**Fix:** `ConstructionGrid.RunLayerFor` owns a run's layer; the order and the preview both ask it.
The highest cell of the run wins — anchoring on the first cell touched would refuse the whole run for
a player who starts the drag in mid-air. The lift is idempotent, so handing lifted cells back to
`Place` is safe, and a test pins that.

**Caught next time by:** `FloorRunTests`, which is about a *run*, and which pins the two-layer fault
itself so the fix cannot be mistaken for a no-op.

### 2026-09-18 — The grey floor that would not deconstruct (P5) — *not a bug*

*"It claims it to be a wood floor for the one that looks like a stone floor"*, then *"I deconstructed
them, wood appeared, the game thought they were gone, and the steel/stone floors were still
visible."*

**Cause:** nothing. Three checks excluded every candidate — the pane and the mesher read one field
(`_grid.FloorStuff`, mirrored on adjacent lines by `CopyCell`); the stuff ids match on both sides
(wood 4, stone 5); both slab arts resolve to real prefabs so the per-cell group fallback never fires.
`StaleFloorProbe` then measured the mirror: module 0 → 134 on build, **134 → 0 on removal**, in the
same refresh. Nothing goes stale.

**So the grey was a different surface one cell down**, seen through the hole the bug above left — and
still there after the floor above it was removed, because it was never the floor being removed.

> **Superseded, 2026-09-18.** The conclusion was wrong: the grey was stone paving on the *same*
> layer. See the row at the top of this register. The three exclusions above all still hold; what
> was missing was the question *was it ever wood?*, and the answer was in the save file.

**Worth keeping** because two separate reports and a deconstruction test all pointed at a renderer
fault that did not exist. The probe is committed; next time this is one command.

### 2026-09-18 — Two legal orders made an illegal ladder (P2, P3)

*"Sometimes the colonists climb up the ladder where there is wall or slab directly above."*

**Cause:** the shaft rule asked the built world. Order the ladder, order the floor above it — each
legal on its own because neither exists yet — and both get built. The prime suspect (the P3
compatibility clause) was **wrong**, and one number killed it: the played meadow generates *no*
ladders and no connectors at all, on three seeds.

**Fix:** `ShaftRulePermits` owns the whole question, sees sites, and is asked again at `Raise`.

**Two defects fell out of the hunt, neither reported:**

- **A shaft could only ever be one storey.** A ladder is `blocking false` so a colonist can stand in
  it, and `SomethingUnderfoot` wants a *blocking* edifice — so the second ladder of a chain was
  refused and `LadderArrivesAt`'s own chain clause was unreachable. The first fix was not enough:
  the connector was gated on `CellGrid.IsWalkable`, which cannot see that *a connector is its own
  floor*, so the chain built and the upper ladder silently had none (`StandsOnAFooting`). And the
  fan-out had to reach **upwards**, or pulling the bottom ladder out leaves the upper one on air.
- **Roofing over a working shaft closed it silently.** Refused now.

**Caught next time by:** `LadderTests` — the blueprint race in both orders, the chain, the chain's
teardown, and the roofed shaft.

### 2026-09-18 — A bed built through a wall (P1)

`Raise` derived the far cell from `_facing[cell]`, called `Clear(cell)`, then read the facing *again*
out of the slot it had just zeroed. Every rotatable thing was recorded facing north.

**It hid because only the drawing was wrong** — the cells were derived before the clear and were
always right, so the footprint guard still worked and no simulation test could see it.

**Three tests had a clear shot and all three missed**: one passed on the difference between two beds'
*cells* rather than their facings; one tested the refusal path, which was never broken; and every
other bed test places facing 0, which is also what a lost facing looks like.

---

## Every test used one of each, so the cap was never reached (2026-09-21)

**Symptom.** None, for a day. Three tiers green, the feature demonstrably working, and the number in
the decision, the handover, the status file and the design document was wrong by a factor of eight.

**The real cause.** A shelf was specified as eight *stacks* — 600 wood. `PutIn` merged a load into
*the* stack of its def, and `HasSpaceFor` refused a fresh slot for a def already present, so a shelf
held one stack per **commodity**: 75 wood, one tile's worth. A warehouse unit had quietly become a
spice rack.

**Why nothing caught it.** Every test put *one stack of each kind* into a shelf — because that is the
obvious way to fill eight slots when seven commodities exist, and because the capacity question reads
as answered once "the ninth is refused" passes. The cap that was wrong is the one nobody exercised:
the second stack of the same thing.

**The check that catches the next one.** `EightStacksOfOneCommodityFillAShelf` fills a store with one
commodity and asserts the headline number itself — 600 — rather than a slot count.

**The general shape.** When a container is specified by a *quantity* ("eight stacks", "600 wood"),
test the quantity, not the slot arithmetic. And be suspicious of any test that fills a capacity with
one of each: it exercises the dimension you were thinking about and not the one a player will use.
The tell here was that the feature's own playtest question — *does one shelf do the job of eight
tiles of painted zone?* — was answerable "no" from the code, and nobody asked the code.

## A guard that compares a position, when the thing has stopped having one (2026-09-21)

**Symptom.** A colonist walks to a shelf to fetch wood from it and then stands there. No error, no
failed job, no stuck flag — the toil simply never advances.

**The real cause.** `JobDriver.LiftToil` had two guards reading `item.Cell == Pawn.Cell`, which asks
"is the thing still at my feet". That was a complete question while a thing was on the floor or in a
pair of hands. A thing in a store has **no cell at all**, so the comparison is false for a stack
sitting perfectly still on the shelf being reached into, and the grasp can never happen.

**Why it survived a reading.** The site does not look wrong. It looks like exactly the defensive
check it is, and the three obvious sites — the work giver, the reservation, the walk — had all been
found and fixed. The one that had not was inside a shared toil two layers down, in a file that is not
about storage at all.

**The measurement that found it.** A control that built a wall from material that existed *only* on a
shelf. It failed loudly with the delivery never completing, and a probe printing the job tallies
showed `deliverDone=0` — a job started and never finished, which points at a toil rather than at a
giver.

**The check that catches the next one.** `PawnContext.WhereIs` and `JobDriver.AtHand` are now the two
owners of "where is that thing" and "can she reach it from here". The general shape: **when a thing
gains a third place it can be, every comparison against its position is a question with a stale
answer, including the ones that look like defensive noise.** Grep for the field, not for the concept.

## A test that could not fail for the reason it named (2026-09-21)

**Symptom.** None. The test was green.

**The real cause.** `AnEmptyingShelfGivesUpItsContents` ran a colony to the end of a fixed number of
ticks and then asserted the shelf was empty. It was — but not because haulers had emptied it. The
colonist had finished the deconstruct and `Dissolve` had spilled the contents on the way out. A haul
path that did not work at all produced exactly the same final state.

**What changed.** The test watches tick by tick and requires the shelf to be empty **while it is
still standing**. It went red immediately, and the deadlock it then exposed — an emptying store whose
contents rank below every store and have nowhere to go, against a gate that will not remove the store
until they have gone — was a real one that no other test could see.

**The general shape.** A test that asserts an **end state** reachable by two paths tests neither.
Ask what else could produce the state you are asserting; if the answer is "the thing going wrong",
assert a state only the right path passes through.

## The method, which is the real lesson

**Measure, do not read.** Reading the code has been wrong on every hard bug in this project, and
wrong in a specific way: each part *is* correct, and the fault is in the gap between two of them.
Three sessions read the bed placement path end to end and concluded correctly about every line; ten
lines of throwaway test printing *asked → got* found it on the first run.

The probe is the tool. Write it, run it, delete it — or keep it beside `StoneFloorProbe` and
`StaleFloorProbe` if the question will be asked again.

**Pin the fault, not just the fix.** A test that only asserts the correct outcome passes on a board
where the bug cannot arise. `TheLiftOnItsOwnSendsOneBoxToTwoLayers` asserts the *two layers*, so the
fix cannot be mistaken for a no-op.

**One number can kill a theory.** "The meadow has no connectors at all" ended a hunt that a day of
reading would not have.

**And check the fix is even in the player's build** before hunting a second cause.

---

## Runbook: a tile that looks wrong

**Written 2026-09-18, after one grey tile cost four rounds.** Three of those rounds produced
confident wrong answers reasoned from screenshots — a stale render mirror, a hole one layer down,
stone paving — while the file that answered it sat on the same disk throughout. This is the order to
work in when the next "that tile looks wrong" arrives.

**Do not start by reading the renderer.** That is what was done three times.

### 1. Get the save, not the screenshot

Ask for a save, by name. A screenshot is an argument about pixels; a save is the state.

```
dotnet run --project tools/dotnet/Odyssey.SaveProbe                  # the usual folder
dotnet run --project tools/dotnet/Odyssey.SaveProbe -- "<path>"      # one file
```

No Unity, about a second, and it prints every **floor** (kind and material), every **item** (by kind
and layer) and every **terrain** on the board. Saves live at
`%USERPROFILE%\AppData\LocalLow\Unity Technologies\com_unity_template_urp-blank\Saves`.

### 2. Ask what is *in* the cell before asking why it is *drawn* that way

A cell can hold more than one drawable thing at once, and the report will name only the one the
player recognises. The grey tile was **a wood floor and rubble terrain in the same cell**: the pane
said "Wood floor" and was telling the truth, and the grey on top of it was not a floor at all and had
no material to report.

| The report says | Check, in this order |
|---|---|
| a floor is the wrong material | `Floor=` and `Stuff=` for that cell — **and** `terrain` for the same cell |
| something is at the wrong height | both things drawn there, before any placement code |
| something appears/disappears/flickers | two things at one height: coplanar geometry, not a shader |
| it only happens in an old game | what the old game has that a new one does not — `terrain` and items are where that lives |

### 3. Turn the clean exclusions around

"A wood floor cannot draw as stone" is also "**a cell drawing as stone is not a wood floor**". Three
sessions proved the first and never read the second. When every mechanism is excluded and the
symptom persists, the thing is not what the report calls it (**P6**).

### 4. Read the reporter's causal claim and test *that* first

The owner's first sentence was *"the colonists tried to build the most outer slabs first which then
landed a stone/steel looking tile 1 height below"*. That is the mechanism, exactly, and it was
testable headlessly in the fast tier from the first minute. Four rounds went on the tile's
appearance instead.

### 5. Known-good facts, so they are not re-derived

- **Grey cross-hatched plate** = `SM_Env_Ground_Tile_Half_01/02/03`, used by `slab.stone` **and** by
  the Pavement / CrackedPavement / **Rubble** terrains. Seeing one does not mean a stone floor.
- **Rubble comes from a collapse** (`SupportSystem.Rubble`) and lands in a cell that **has a floor by
  definition**, so floor-plus-terrain in one cell is normal, not corruption.
- **Rubble is clearable** — `clearable: true`, 90 ticks, cleared with the **Mine** order. Rubble
  already in a save stays until somebody clears it; a fix stops new ones and does not tidy old ones.
- **The pane titles a floor by `FloorStuff` alone** — it never says "paving", and it says nothing at
  all about anything drawn on top of the floor.
- **Every slab's top face is `CellMetrics.SlabLift` above the cell floor plane.** Two things at the
  same height z-fight; the clearance is why. Do not "tidy" it to zero.
- **A floor is drawn as a sheet, not as a plate** (`CellMetrics.FloorTile`, `FloorSheet`,
  `FloorKnit`). The plate's rim tied with its neighbour's top face and dotted every seam in the
  colony. Do not "restore" the thickness without reading P8 — and if a floor's lip over open air
  needs to look solid, that is a fascia on the face, not a thicker plate.
- **The slab art itself is exact** — 2.5000 m across, flat on top to the micrometre, the top face
  the full width of the piece, 40 vertices. Measured 2026-09-18 by `SlabTopFaceProbe`. A seam
  artefact is not the art's size, shape or flatness.

## A symptom that names a missing feature usually names a missing half of one

**2026-09-19, the carried load.** "A colonist picks something up and it disappears" reads as
*carrying is not built*. Two of its three beats were: the stoop is a timed toil, the grasp lands
in the middle of the drawn crouch, and the stow was already reported at a stockpile and at a build
site. The one missing piece was that `ColonyItems.PickUp` delists the item, so it leaves the view
feed. Had the symptom been believed, the fix would have rewritten `LiftToil` — and `LiftTicks`
moved all three goldens the day it landed.

- **The check:** before building what a report asks for, grep for the half that already exists. A
  one-line grep is cheaper than a wasted session, and this is the third time that sentence has
  been written in this repo.
- **Sibling of "a rule that asks the built world and misses the order":** both are cases of the
  observable state hiding work that has already happened.

## A pose solved to a point is not more robust than one authored as angles

**2026-09-19, the carry stance.** The rule in `13-gestures.md` §3 — solve when the figure must
meet something whose position we know — is about *reach*, and reading it as "solving is always
safer across rigs" gets the carry backwards. Arm length varies across the 61 rigs by more than the
cradle does, so a cradle computed from hip height and solved to puts a short-armed colonist at
full stretch and a long-armed one folded against its chest: same point, two postures. Authored
angles give the same *posture* and let the point fall where each rig's arms are.

- **The check:** ask which of the two a viewer actually reads. For a reach it is the point; for a
  stance it is the posture. Solve only the axis where an authored angle fails outright rather than
  merely looks wrong — here, a load inside the colonist's own chest.

## Two assemblies that cannot see each other agree by spelling and nothing else

**Standing, tightened 2026-09-19.** `Odyssey.Hud` may not reference `Odyssey.Sim`, so a pawn
aspect's name is a string literal on each side. Nothing links them: change one and the feature
silently stops working, with no compile error and no failing test unless one was written for it.

- **The check:** every aspect key gets a test on **both** sides — the Sim side asserting
  `SomeAspects.Key == AspectKey.Of("the.literal")`, the Hud side asserting its own constant equals
  the same literal. `ColonistNames.RollSeedAspect` established the pattern; `CarryAspects` follows
  it. A key with only one of the two tests is a key with no guarantee.

## A pose that is additive over a clip cannot be a stance

**2026-09-19, the carry arms.** Every pose in `PawnFigureDirector` adds a world-space `Pitch` to
whatever the walk clip put on the bone, which is right for a gesture laid over a gait. A carry is
not a gesture: it is held for as long as a state holds, so added to a swinging arm it is a scoop
that swings — and because the load follows the palms, the load swung with it. The owner reported
it as being about the load.

- **The check:** ask whether the pose is an *event* or a *state*. A state has to take the bones
  off the clip first, back to a rest read off that rig at bind time, because sixty-one rigs have
  sixty-one bind poses and a constant would be wrong on sixty of them.
- **Write the rest, never blend to it.** `ApplyWorkPose` runs twice a frame and only one pass
  starts from a freshly evaluated graph, so easing *towards* a rest integrates instead of
  recomputing. Assigning a constant is the identity on the second pass; put the ease in the
  angles.

## A remembered value is only remembered for the path that writes it

**2026-09-19, the load that would not turn.** A carried prop was drawn at
`ChunkRenderer.FacingOf(pawnId)`, which looks like "which way is this colonist facing" and is
actually "the last heading this loop drew a **stand-in** at". The same loop `continue`s past every
pawn that has a live animated figure, so it never writes an entry for one — and every load on
every real colonist was drawn at a yaw of exactly nought. A log pointed north for ever.

- **The check:** before reading a cache, find the write. If the writer skips the cases the reader
  cares about, the reader gets the default and the default is plausible — nought is a real yaw,
  so nothing looks broken until something asymmetric (a log) is held in it.
- **It repeated one layer down.** `ItemHeap` lays its sunflower out on the world axes, so the
  armful had to be turned about the cradle as a cluster; rotating each rock in place would have
  kept the shape and left the shape pointing north. The same fault twice in one feature.

## An instant transfer drawn literally is a teleport

**2026-09-19, the pickup and the drop.** The simulation moves a thing between a cell and a pair of
hands in one tick, because there is nothing sensible in between. Drawn literally that is a jump of
about a third of a metre in no time, at both ends — and the design draft asserted the pickup was
continuous "for free" because the hands are at the floor on the grasp tick. True vertically, and
it misses the lateral gap between the middle of a cell and a pair of palms. The drop was worse and
the draft did not consider it at all.

- **The check:** when presentation shows a state the simulation changes instantaneously, ask what
  the two endpoints are *in world space*, not whether the tick is right. A continuous quantity on
  one axis says nothing about the others.
- **Matching the two halves needs an identity, not a description.** The falling item is drawn from
  a different list by code that never saw the hands; the def and the stack cannot pick it out of a
  stockpile of the same commodity. Publish the id.

## A shader found at runtime is a shader the build throws away

**2026-09-19, the empty world.** Owner: *"there is no terrain — no graphics, terrain etc, apart
from characters."* Only in a player build; the editor was perfect.

Everything in the world is drawn with `Graphics.RenderMeshInstanced` using materials created **at
runtime** from `Shader.Find(...)` with `enableInstancing = true` — `ChunkRenderer`,
`MaterialCache`, `ModuleLibrary`, `TreeMaterials`, `ColonistMaterials`. A runtime-created material
is not an asset, so the build's shader collector never sees it and Unity ships only what assets
reference. Measured in the built player: `Odyssey/Water`, `Odyssey/Tree`, `Odyssey/Character`,
`Odyssey/Outline` and `Odyssey/GradientSky` were **absent altogether**; `Universal Render
Pipeline/Lit` was present only in the non-instanced variants Synty's prefab materials use.

Characters were the one visible thing because they alone are GameObjects wearing real material
assets — and they drew in the pack's own colours, because the shader that recolours them had gone
with the rest. *"Everything is missing except the one thing that is a GameObject"* is the
signature of this fault.

- **The check:** every `Shader.Find` name belongs in `ShaderInclusion.Required`, which forces the
  shader and all its variants into the build. `ShaderInclusionTests` derives the list from the
  source, so the two cannot drift — it found two names the hand-written list had missed on its
  very first run.
- **Measured cost of always-including URP/Lit: none.** 385 MB and 11 s before and after. The fear
  that all-variants would be ruinous was worth testing rather than designing around.
- **No test in this repository could have caught it.** The editor has every shader and every
  variant, always, and both tiers run in the editor's own domain. Only a player build fails on
  this, and until that day nothing had ever made one.

## One rule, two places, and only one of them knew — the null guard edition

**2026-09-19, `SettingsPresenter`.** A `NullReferenceException` every frame in a player build,
never in the editor. `Attach` dereferenced `_bootstrap.Directors.Overlays` while its own comment
three lines above explains that attaching deliberately does **not** wait for `Directors` to
exist. `OnDestroy` had guarded that exact dereference since it was written.

- **The tell was already in the file**: the same expression guarded in one method and bare in
  another. When you find that, the bare one is the bug, not the guarded one.
- **The build had been saying so**: `CS8602: Dereference of a possibly null reference` at that
  line, in every build log, unread.
- The fix is not a guard but a second waiter — `HookDeveloperOverlay`, which waits on `Directors`
  where `Attach` waits on `Preferences` — because the two genuinely wait on different things.

## The content pack does not exist in a player, and the code said so years before it mattered

**2026-09-19, the empty world — the real cause.** After a shader fix that was a genuine but
*different* fault, the owner reported again: *"No graphics came up in the build… apart from the
characters."*

`ContentPack.FindRoot` locates the Defs by walking up for a directory holding both `Assets` and
`ProjectSettings`. A built player has neither, so it throws, world generation never runs, and the
scene is empty but for the figures the start flow had already made. Its own remarks predicted
this in full: *"A built player has neither directory and would land in the throw below, which is
deliberate. Nothing in CI or scripts/ builds a player, so shipping the pack is not solved here
rather than solved wrongly here."* `UseRoot` exists for exactly this and had never been called.

- **The check:** anything the game reads from a path under `Assets/` at runtime is absent from a
  player. `ContentPackBuild` stages the pack into `StreamingAssets` for the build and removes it
  after, so the repository keeps one copy; the composition root calls `UseRoot` outside the
  editor.
- **A deliberate limitation outlives the sentence that made it deliberate.** "Nothing builds a
  player" was true when written and stopped being true the hour a build command was added. When
  you write *"X is not solved because nobody does Y"*, the note has to be found by whoever first
  does Y — a grep for `StreamingAssets` found it, but only after two wrong answers.

## A clean log from a program sitting on its main menu proves nothing

**Same day, and it cost two wrong diagnoses.** Twice I ran the player from a terminal, saw a
clean log, and reported a fix. Both times the player had stopped at the main screen, where
nothing loads the content pack, nothing generates a world and nothing draws terrain — the entire
subsystem under suspicion had not run.

- **The check:** before believing a smoke test, confirm the code under suspicion actually
  executed. The log that mattered says `world 120x120x16 seed 1 generated in 61 ms`; the two that
  did not say anything of the sort, and their silence read as success.
- **The fix is to make it reachable**: `-odyssey-newgame` boots a player straight into a colony,
  so a build can be smoke-tested without a person clicking. A check nobody can run from a
  terminal is a check that will be skipped.

## Keeping the shader is not keeping the variant

**2026-09-19, the third cause of the empty player build**, after the missing shaders and the
missing content pack had both been found and fixed and the world still did not draw.

A diagnostic in the player reported `draws 1731 instances 43921 chunks 104 materials 22 surround
18192`, with `Universal Render Pipeline/Lit` found, supported and carrying its five passes. The
renderer submitted everything, every frame, and none of it appeared. `INSTANCING_ON` comes from
`#pragma multi_compile_instancing`, and Unity's **built-in** variant stripping drops that axis
unless a **material asset** in the build has instancing switched on. Every instanced material in
this game is created at runtime from `Shader.Find`, so there was none, and
`Graphics.RenderMeshInstanced` drew into a variant that was not there. It does not warn.

- **The check:** `InstancingKeepAlive` ships one instancing-enabled material per kept shader under
  `Assets/Resources/OdysseyKeepAlive`, `PlayerBuild` refuses without them, and
  `EveryKeptShaderAlsoHasAnInstancingKeepAliveMaterial` fails the tier — including when a
  keep-alive exists but has had its instancing switched off, because present is not the same as
  right.
- **The pattern is one rule with two owners, again.** "This shader is in the build" and "the
  variant this draw needs is in the build" are different claims with different mechanisms, and the
  fix for the first was reported as covering the second.
- **A test name that claims more than its assertion actively hides the bug.**
  `TheInstancedVariantOfTheLitShaderIsKept` only asserted that a name appeared in a list — and
  appearing in that list is exactly what did not keep the instanced variant. It was green
  throughout, and its name is why nobody looked here.

## Check the working tree before believing the committed settings

**Same day.** A cold player build was compiling **884,736** variants of one pass — about a day and
a half — and had died the night before with *"Internal error communicating with the shader
compiler process"*, which reads like a flaky tool. The cause was an **uncommitted** change:
`UniversalRenderPipelineGlobalSettings.asset` has `m_StripUnusedVariants: 1` in git and had been
flipped to `0` locally. Restoring it took the same pass to 64 variants and the build to 12 seconds.

- **The check:** `git diff HEAD -- ProjectSettings/ Assets/Settings/` before diagnosing any
  build-shaped problem. A settings asset that Unity rewrites on its own is easy to stop reading,
  and a one-character flip inside 20 lines of migration churn is invisible in a glance.
- **The build log states it plainly when you know the line to want:** *After built-in stripping:
  884,736 → After scriptable stripping: 884,736* — a stripper that returns its input untouched.
- **The flip was probably a fix attempt for the bug above.** Switching stripping off does keep the
  instancing variants. It keeps 884,734 others with them.

## Two paths through one function, and only one of them was ever drawn

**2026-09-19, the fourth cause of the empty player.** Terrain and trees drew; grass tufts, bushes
and every item pile did not. `ModuleLibrary.DressGround` clones the pack's material with
`enableInstancing = true` when it needs an adjustment, and **returns the licensed source untouched
when it does not** — and the source has instancing off. Props take the second path. The property
that differs between the two branches is exactly the one that decides whether a thing is visible in
a player, and it is invisible in the editor, which has every variant always.

- **The check:** `SyntyInstancingKeepAlive` stages an instancing-enabled material per distinct
  (shader, keyword set) the module catalogue can draw, for the duration of a build.
- **Ask what the material actually is before theorising about the shader.** One log line —
  `mat='Generic_01_A' shader='Synty/Generic_Standard' instancing=False kw=[...]` — ended a search
  that had been aimed at URP/Lit, which the props do not use at all.
- **A list of "shaders we use" built from `Shader.Find` call sites cannot see a shader that arrives
  on a prefab.** `ShaderInclusion` derives its list from the source and is right about what it
  covers; the pack's shaders were never in its domain, and nothing said so.

## What a thing is drawn on is not the surface it is picked at

**Symptom.** Clicking a bed is *"really specific"* (owner, 2026-09-19). Some parts of the drawn
bed select it, some select its other cell, and the far end selects the grass behind it. Nothing in
the pane, the model or the footprint is wrong, and both cells of the bed answer bed facts correctly
when a test asks them directly.

**The real cause.** `SlicePicker` resolves a **non-occluding** cell by crossing that cell's *floor
plane*. A bed does not occlude, so the only surface it offered a ray was the floor underneath it —
while the bed itself is drawn 0.70 m up. At the play camera's 48° elevation, a surface 0.70 m high
is drawn `0.70 / tan(48°)` ≈ **0.63 m, a quarter of a cell**, nearer the viewer than the floor it
stands on. So the clickable bed sat a quarter cell behind the drawn one, in every direction the
camera faces.

**The measurement that found it.** A throwaway probe test that swept a 48° ray along the bed in
quarter cells and *printed the cell that came back*, twice: once aimed at the floor and once at
`BedShape.MattressTop`. The floor column was perfect and the mattress column was shifted by one
quarter-cell step throughout. Three sessions of reading `SlicePicker`, `CellDetailContributor` and
`InspectModel` had found nothing, because **nothing in any of them is wrong**. Two runs of a probe
that printed a table found it exactly.

**The check that catches the next one.** `BedPickHeightTests` aims at the mattress — at what a
player aims at — along the whole length of the bed in tenths of a cell, and requires the cell under
the pointer. A check of the two cell centres alone would have passed *before* the fix: the drift is
a quarter cell and a centre has half a cell of slack either side. **Test the ends of a thing, not
its middle**, whenever the complaint is that something is fiddly rather than broken.

**The general shape.** Anything drawn standing above its cell floor that does not occlude has this
fault, and the seam is now `WorldRenderModel.StandHeight`. A bed is the only thing that answers
today. The next non-occluding thing that stands up adds a line there — and if it does not, it will
be a quarter cell out and nobody will know why.

## The caller's guess taken as the caller's instruction

**2026-09-19, the debug menu's spawn.** Owner: *"Every time I tried to generate more colonists — I
get this warning message `[Odyssey] the simulation refused 1293 x SpawnPawn: OutOfBounds`."* The
crowd playtest for pawn avoidance could not be run at all, because no colonist could be added.

Two separate faults wearing one message.

- **The refusal.** `DebugAnchorCell` returned the middle of the map *at the active slice layer*,
  and `HandleSpawnPawn` took that layer as an instruction. Over open ground the slice layer is the
  air several storeys above the terrain, so the cell was in bounds, unstandable, and refused —
  every time, for the life of the feature. A caller that names a *column* and guesses at a layer is
  the normal case wherever the interface is above the ground looking down; the layer is a guess and
  the grid has to be the one to settle it. `CellGrid.NearestWalkableInColumn` is now the single
  owner of that fall, for the spawn; `FirstFloorAtOrBelow`, which already existed, does it for the
  grant.
- **The lie in the reason.** In-bounds-but-unstandable answered `OutOfBounds`, which is the one
  reading of the message that was definitely false and the one the eye goes to. It says
  `NotPermitted` now. **A rejection reason is a diagnosis; a wrong one sends the next reader to the
  wrong half of the code.**
- **The four-figure count.** Nothing in the build had ever called `IntentBus.ClearRejected`, so the
  list grew for the session and `ReportRejections` re-counted the whole of it every frame. One
  click reported as 1,293 refusals a few seconds later — and *that* number is what makes a reader
  hunt for a loop submitting intents, which does not exist. **A diagnostic that is itself wrong
  costs more than no diagnostic**: the same handler's doc comment records that two playtests were
  already spent on refusals being invisible, and this is the other edge of the same knife.
- **The check:** `DebugIntentTests.SpawnPawnAimedAtTheAirFallsToTheGroundInThatColumn`,
  `GiveResourceAimedAtTheAirLandsOnTheFloorBelow` and
  `SpawnPawnRefusesAColumnWithNowhereToStandAndSaysSoTruthfully`. The fast tier proves none of the
  shell half — `HudShell` is Presentation and does not compile there — so the anchor's own three
  paths remain a by-hand test (`docs/design/18-debug-menu.md`).

## A rule with a threshold in it, sampled every tick

**2026-09-19, the sub-tile sidestep.** Owner: *"the colonists sometimes vibrate quickly — as if it's
fighting something or a indecision or a check that is happening — it's mostly smooth — but then
vibrates with an odd movement."*

A sidestep computed per frame from four boolean gates: an oncoming test at `dot < -0.5`, a set of
cell-sharing tests that forced the weight to its maximum whatever the distance, a distance measured
in x and z alone, and a choice between candidate offsets by whichever was **longest**. Measured on a
crowd of twenty over fifty seconds, the offset moved more than 5 cm in a single tick **85 times**,
the worst of them the full 0.600 m envelope in one sixtieth of a second.

- **The pattern is not "a threshold", it is "a threshold sampled continuously".** Each gate is
  defensible as a decision. What makes it a vibration is that it is re-decided sixty times a second
  off inputs — another pawn's cell, its heading, a distance — that twitch across the boundary. Any
  rule evaluated every tick against live neighbours has to enter through a ramp.
- **`max` has a winner, and a winner can be swapped.** Two candidates of near-equal length pointing
  opposite ways swap on any twitch, and the figure crosses the whole envelope and comes back.
  Summing signed scalars and clamping once has no winner to swap, and does something sensible when
  two things push from opposite sides.
- **A distance that ignores an axis is a distance that is sometimes zero.** x/z-only made a colonist
  on the terrace above nought metres away, on a board whose entire surface is 3 m terrace risers.
- **Damping the wrong quantity hides the fault and adds a second one.** A previous pass answered the
  snapping by rate-limiting the *whole drawn position* at 5.5 m/s. That damps the colonist's own
  walking: the figure lags its own locomotion and then surges, and — because the sidestep was still
  inside the position the speed was observed from — a 0.6 m swerve read as 6 m/s and threw the legs
  into a run. Damp the term that steps, not the sum it is part of.
- **The check:** `SteeringContinuityTests`, six cases, one per gate removed. And the measurement
  that found it: drive `PawnPose.Of` over a real ticking colony and count per-tick changes in the
  lateral offset. Reading the code suggested three wrong culprits first; the counter found it in one
  run. `docs/design/25-pawn-steering.md` §6 has the numbers.

## Inferring a number the simulation already knows

**2026-09-19, the walk that went backwards.** Owner, after the steering fix: *"it happens sometimes
when colonists are walking, particularly where there is a terrain step tile it starts to vibrate and
move oddly mostly at the beginning of the frames when going up — so it still exists just less of
it."*

Presentation carries a figure on past the tick it sits on, and needs the rate to do it. It inferred
the rate: a global `movePerTick` from the Defs, added to a percentage as though every step cost
`MoveCost.Orthogonal`. Wrong by the step's geometry (a hop up is 240, not 100), wrong by the
colonist's own pace and condition, and — the one that makes it unfixable where it stood — wrong by
the price of the terrain being entered, which is inside the step cost and cannot be recovered from
the two cells. **An over-estimate draws the next frame behind the last one.** Measured on the real
board: 3,172 frames of 59,000 moved a colonist backwards along her own step, up to 10.9 mm.

- **The pattern:** presentation deriving a quantity the simulation computed exactly and threw away.
  The fix is never a better derivation — the third of those three errors has no derivation — it is
  to publish the number. `PawnView.MoveDeltaPerMille` costs one int in a view that is not saved and
  not hashed.
- **Truncate an estimate towards the side you can't see.** Under-estimating makes the frame after a
  tick jump slightly forward; over-estimating makes it go backwards. Only one of those is visible,
  so the published rate is floored.
- **Where it hides.** Backward travel on flat ground is a few millimetres of stutter nobody names.
  On a terrace bank the same backward travel is *downward* travel, so it becomes a visible vertical
  buzz — which is why the report was about step tiles and why every flat-ground fixture was clean.
- **Every cheap fixture missed it, and that is the lesson for the check.** A hand-built world grows
  no banks (`BankLayout` reads generated terrain), and a hand-built `PawnView` publishes no rate, so
  it takes the fallback path rather than the one the game takes. `WalkContinuityTests` therefore
  ticks a real colony over a real generated board. Two earlier passes at this vibration measured
  hand-built fixtures and found them clean.
- **Bisect before believing the last diagnosis.** The obvious reading was that the previous fix had
  not gone far enough. Running the same measurement with the steering switched off gave numbers
  identical to the frame, which said in one run that this was a different fault.

## A cached picture of a moving world (2026-09-20)

Owner: *"when generating more colonists — some ... their profile picture seems black."*

`PortraitStudio` renders a colonist once and keeps the texture for the session. It is careful that
its own key light must not escape into the world, and never asked the reverse question. The daylight
cycle writes the **global** ambient, fog, skybox and sun, so the portrait camera read whatever hour
it happened to fire on: the same colonist measured 80 of 255 at noon and 26 at midnight, and 26 is a
black square in a 26 px tile. Cached, so it never recovers.

- **The pattern:** *a render that is kept is a render of everything that was true at that instant.*
  Anything one-shot and cached — a portrait, a baked mesh, a thumbnail — has to own every global it
  reads, not merely the ones it set. Ask of any such render: **what in this picture is a fact about
  the subject, and what is a fact about when it was taken?**
- **It is the mirror of a rule the file already had.** Design 20 §10.3 reasons carefully that the
  studio's light must not reach the world. The same sentence, read backwards, is the bug.
- **"Some" is the signature.** A cached derivation that depends on an unnoticed input fails for
  exactly the subset created while that input was wrong — which reads as randomness and is not.
- **A global can look taken over and not be.** Three versions of the fix measured as working and
  were not: `ambientMode`/`ambientLight` leave the renderer on a stale probe,
  `RenderSettings.ambientProbe` is honoured only under `AmbientMode.Custom`, and the skybox still
  arrives as the default reflection probe. **Measure the take-over, do not read it** — a spread
  across the day is one number and it said no three times.
- **The check:** `PortraitLightingTests` photographs one fixed appearance at ten hours of one day
  and fails if the brightest is more than 3% over the darkest; a companion asserts the studio hands
  the environment back. Both fail on the old code.

## One rule with two owners, again: the brightest light in the scene (2026-09-20)

`OdysseyBootstrap.FindKeyLight` means "the scene's own sun" and says so in a comment. What it does
is take the brightest directional light in the scene — and `PortraitStudio` puts one there, hidden
and switched off, the first time the setup page photographs a candidate. Adopt it and the world
loses its sun while every portrait is lit by a light the clock is quietly retuning.

- **The pattern:** a finder whose comment states an intent its predicate does not. The predicate is
  the rule; the comment is a wish. It is this file's recurring *one rule with two owners* in a new
  coat: here the two owners are a comment and a `foreach`.
- **What made it invisible:** it only fires when the scene's own sun is dimmer than 1.6 at the
  moment a session is built, and the play scene bakes its sun at midday.
- **The check:** the predicate now skips lights on objects with hide flags, and the studio
  re-asserts its own light's aim, colour and intensity on every shot — so neither half can be
  quietly retuned by the other again.

## A cap with no order is a cap on identity (2026-09-20)

Past `PawnFigureDirector.MaxFigures` a colonist is drawn as a baked mesh and does not animate, which
is deliberate and documented as *"a colonist beyond the cap is a long way off"*. Nothing sorted. The
loop walked the snapshot and stopped, and snapshot order is pawn id — so the colonists that lost
their animation were fixed at creation and the camera never changed it. Eighty-five colonists: the
nearest frozen one at 134 m, an animated one at 179 m.

- **The pattern:** a budget applied in arrival order rather than in the order the budget's own
  justification names. The comment said "a long way off"; nothing made distance the criterion.
- **Where to look for more:** anything that truncates a list to a budget. If the doc comment gives a
  reason ("far", "old", "least important"), the code has to sort by it or the reason is fiction.
- **The check:** `FigureCapTests` spawns twenty colonists along a line in a *scrambled* order, so id
  order and distance order disagree; the old code fails it. Scrambling is the whole test — spawn
  them nearest-first and taking the first N by id passes without sorting anything.

## A housekeeping rule that only runs while there is something to keep house over

**2026-09-20, found in review of the skills work (SK4), before a player saw it.** The level-up toast
is detected entirely on the presentation side: `SkillLevelWatch` remembers the last level it saw for
each colonist and each skill, and reports a rise. It guards the two ways a remembered mark goes
wrong, and both have tests — **the first sight of a colonist is silent**, so nobody announces her
starting roll, and **a colonist missing from the frame is forgotten**, so a dead one leaves no mark
for a later pawn to inherit.

The second guard runs inside `Step`, over the frame it has just been given. **Between two colonies
there is no frame**: the interface is on the main menu, nothing is published and nothing is stepped.
So the marks from the last colony survive into the next one, where `PawnId` 1 is a different person
— and if she is the better miner she announces, on her first frame, a level she was rolled with.

- **The pattern:** a cleanup that is driven by the same pump as the work. It is correct for every
  case *inside* a session and silent about the boundary between two, because at the boundary the
  pump is stopped. Ask of any per-frame housekeeping: *what runs it when there are no frames?*
- **The tell is a `Clear` nobody calls.** `ToastModel.Clear` existed, was unreferenced, and cleared
  the rows but not the watch — which is the wrong half: the rows expire on a six-second timer
  anyway, the marks never do. An unreferenced teardown method is a design that expected a boundary
  and then did not wire one.
- **The fix goes where the session boundary already is**, not into the watch. `HudShell.OnSessionChanged`
  is the one place that already takes the in-game interface away with its colony; the clear is one
  line below it, so the next thing with session state to drop has an obvious home.
- **The check:** `ToastModelTests.AColonyGoingAwayTakesItsLevelMarksWithIt` — first sight silent,
  clear, then a *higher* level on the same `PawnId` must say nothing, and the rise after that must
  still be reported once. Confirmed to fail on the right assertion with the clear commented out,
  because a test written after a fix is worth nothing until it has seen the bug.

## An instrument that summarises an event (2026-09-21)

**P14.** A performance trace was written to find a stutter, and then hid it three separate times —
each time because a field had been given the shape of a *cost* when the thing it measured was an
*event*.

1. **`remeshed` was last-seen.** Every other counter in a row is a fact about a moment and is
   rightly the final frame's value: a draw-call count halfway through a second *is* the draw-call
   count. Re-meshing is not like that. It happens on a handful of frames a second at most, so the
   final frame's value is almost always zero — and a second in which eight hundred chunks were
   rebuilt reported `0`. That zero was quoted **three times in one afternoon** as evidence that
   meshing was not behind a 150 ms stall, which it could never have shown. It was caught only
   because one full re-mesh happened to land on a row's last frame and printed `800` against a wall
   of zeroes.
2. **The tick had a median and no maximum.** The frame carried p50, p95, p99 and max from the first
   line of the class, because the entire argument for the trace was that *a mean cannot see
   stutter*. The tick was then given `tick_p50` alone. At 3x speed a second holds about a hundred
   and eighty ticks, so one bad tick sits at the 99.4th percentile and is invisible.
3. **The phases had a mean and a p95 and no maximum** — and `PhaseTrace` had offered `MaxMs` since
   the day it was written. The trace simply never asked.

- **The pattern:** a distribution was designed for the headline figure and summaries were added for
  everything underneath it. Each addition looks reasonable on its own; the class of fault only
  appears when something rare and expensive happens in one of the summarised terms.
- **Why it is worse than an ordinary blind spot.** A missing field is obvious. A field that reports
  `0`, or a plausible median, reads as *evidence of absence* — and it was used that way, repeatedly
  and confidently, against the correct hypothesis.
- **What makes it likely here.** The tick is **not inside any `FrameSection`**: it runs before the
  draw block, so an expensive one lands in the part of a frame with no name at all. A term that no
  section covers and no maximum records cannot be seen by anything.
- **The rule:** **a counter of events is summed, a counter of state is last-seen, and anything
  timed carries a maximum as well as a middle.** Which one a field is has to be decided when it is
  added, not discovered when it lies. The question to ask of any new field: *if this went badly
  once in two hundred samples, would this column change?*
- **The check:** `TraceWriterTests.TheHeaderNamesEveryFieldARowCarries` stops a field arriving
  undeclared, and `FrameTimeTests.TheTraceAgreesWithTheArmThatTimedIt` stops the headline figure
  drifting from an independently-taken one. **Neither would have caught any of these three**, and
  that is worth knowing: both guard a field's *existence* and its *accuracy*, and this fault is in a
  field's *shape*. The reader's "elsewhere" column is the nearest thing to a guard — it makes the
  unexplained remainder impossible to overlook, which is what eventually forced each of the three
  into the open.

## Two branches that name the same anchor collide in arithmetic (2026-09-20)

**P12.** SK4 added a transient toast stack "under the alerts in the same column". EV added the
Events panel "under the alerts in the same column". Neither branch knew the other existed, both
computed their top as `alertsTop + alertsHeight + Gap`, and both were right. Merged, they solve to
the same origin and the toast draws over the panel.

- **The pattern:** two features written in parallel that anchor to the same landmark in *prose*.
  The prose is identical on both branches, which is what makes it invisible at review: each reads
  as a correct sentence about a column that, on that branch, has one new member.
- **Why the exhaustive test did not catch it.** `HudLayoutTests.NoTwoPanelsOverlap…` sweeps every
  case in a hand-written list and is genuinely exhaustive over it. SK4's cases set its own row
  count high and the other's to zero — the parameter did not exist there. EV's did the mirror
  image. **An exhaustive sweep is only as exhaustive as its case list**, and the conflict
  resolution that merges two constructors does not write the case that uses both parameters.
- **Where to look for the next one:** anything a doc comment places relative to a named neighbour
  rather than at an absolute figure — a column, a docked strip, a stacked overlay. `git log
  --all --grep` for the anchor's name, or grep the other live branches for the phrase, and if two
  of them add a member to one stack, the merge owes a case with **every** member at once.
- **The fix is an ordering decision, not a nudge.** Ask which member comes and goes: the one that
  appears and disappears goes last, because anything under it steps down and back up every time.
  Here that is the toast (six seconds, every couple of minutes) against the Events panel (a
  standing list a player scans).
- **The check:** `HudLayoutTests.TheToastStackIsTheLastThingInTheAlertsColumn` states the order and
  the reason, and two cases in `Cases()` put all three panels in one column at all three
  resolutions. Both confirmed to fail on the pre-fix arithmetic before the fix went in.

## A guard on one side of an asymmetry names the bug on the other (2026-09-21)

**P15.** The owner photographed sown seeds hanging in mid-air over a quarry the colony had dug out
from under them. `Falling` — the one place that answers *"what happens to whatever is in a cell when
the thing it was standing on goes away"* — knew **pawns** and **loose items** and nothing else. A
crop is neither; it is rooted in the soil rather than resting on it, so nothing had ever asked.

**The tell was already written down, one cell away.** `DesignationGrid.CanMine` refuses the ground
under a standing **tree**, and its comment says why in as many words: digging it away *"would leave
the tree rooted in mid-air, and the right answer is to fell it first rather than to invent a falling
rule here."* The same sentence describes a sown cell exactly. One of the two rooted kinds had a
guard and the other had nothing, and the guard's own prose was the specification of the missing one.

- **The pattern:** a rule written for a set of kinds, and a new kind that joins the set without
  joining the rule. It does not read as a gap, because every line of the rule is correct — what is
  missing is a *case*, and a case that was never there leaves no trace in the code to notice.
- **Where to look for more:** anywhere a guard exists for one member of an obvious pair or family.
  Ask which siblings it does **not** name, and why not. A comment that explains a refusal in general
  language (*"rooted in mid-air"*) while the code names one specific thing is the strongest version
  of this signal.
- **And the second question: guard, or consequence?** The tree's answer is a guard (the order is
  refused); the crop's is a consequence (the dig is allowed and the seed goes with the soil). That
  is a judgement about which side the player would rather be surprised on, and it was the owner's to
  make — a painted field blocking the pick is worse than losing a seed you chose to dig out. Do not
  assume symmetry of mechanism just because the situations are symmetric.
- **The fix went into the shared answer, not the caller.** `Falling.PlantsOutOf` and
  `Falling.TreesOutOf` are called from `Falling.OutOf`, so mining, `ConstructionGrid.RemoveSlab` and
  the collapse solver all inherited them at once — and `UprootFloatingPlants` is the sweep for the
  case no caller is told about, the sibling of `DropFloatingItems`. Three callers, one new rule, no
  copy.
- **The check:** `FallingTests.MiningTheSoilUnderAFieldTakesTheSeedWithIt` and its four neighbours,
  plus `ATreeGoesWithTheGroundAndLeavesNoWood`. **Run with the two lines removed from `OutOf`**: the
  three mining tests fail and the rest pass, which is what says the tests fail on the reported bug
  and not on something adjacent.

### 2026-09-25 — A new way to go down met three rules that assumed there was only one (health)

**Symptom.** Adding the body (design 43) turned the combat gate red three different ways on the
same afternoon, none of them in the new code: *Job_Downed failed*, *a bandit stood on Fighting at a
target already gone* for 60–68 ticks, and two fighters sharing a tile in the mixed brawls.

**Cause: each rule was written when a pawn could only go down, or stop, one way.** Before the body a
pawn went down only under a blow and never died lying down, so (1) a death always ended its job as
a failure, and the gate's sentinel on `Job_Downed` failures had never seen a death; (2) an attack
on a pawn that went down ended on the attacker's own next tick, which a *stunned* attacker does not
get, and nothing but a blow downed anybody so it never mattered; (3) `Melee.SideOf` counted where a
pawn stands and where it walks, but not the cell an interrupted step is still landing on. Bleeding
downs and kills people between blows, and pain shock changes who stands where, so all three were
reached at once.

- **The pattern:** a rule whose "only one way" was true by accident of what existed. It fails the
  day a second way arrives, and it fails in a system nobody touched.
- **Where to look for more:** every sentinel written as "this never happens" (a failure counter that
  must be nought), every per-tick rule that trusts the actor to get a tick, every claim computed
  from a pawn's position that ignores a move in flight.
- **The fixes:** `Remove` ends `Job_Downed` as a success on death, as `JobHandle.Downed` says it
  may; `CombatSystem.EndAttacksOnTheDowned` ends non-lethal attacks when anybody goes down, as
  `EndAttacksOn` already did for the dead; `SideOf` counts `FinishingStepTo`.
- **The check:** the combat gate (`BanditSoakTests.TheGateWithRaids`), `FightGuardTests.MixedBrawlsOnManySeeds`
  and `FightGuardTests.AStepStillLandingIsHeld`, which fails with the `SideOf` line removed.

### 2026-09-22 — Every portrait on the setup screen was magenta (P14)

**Symptom.** The owner's screenshot: three candidate cards and a detail pane, every colonist a flat
pink silhouette. The shapes were right — head, hair, shoulders — so meshes and attachments resolved.
Only the material was wrong, which in Unity means the error shader.

**Cause: a cached object outlived the materials it wears.** `PortraitStudio` keeps one subject
GameObject and reuses it while the look is unchanged. A colony ending disposes `ColonistMaterials`,
which `DestroyImmediate`s every material it cloned, and sets the studio's `Materials` to null —
deliberately, because the pictures were rendered through them. **The subject was not part of that.**
It survived, still pointing at destroyed materials, and `Paint` then early-returned because
`Materials` was null, so nothing reassigned them. Unity draws a destroyed material as magenta.

**Why it appeared only now.** It was latent for as long as there were seventy-three bodies: the
subject is kept only while the look is unchanged, so a different colonist almost always rebuilt it
and got fresh materials off the prefab. The issued uniform took the cast down to **two** looks, so
the stale subject is reused nearly every time. A rare fault became the normal one.

**The fix.** `Materials` is a property, and setting it drops the subject and clears the pictures —
both are painted with materials that have just gone. The composition root also hands the studio live
materials back when it is asked for one after a colony ended, rather than leaving it permanently
unpainted.

**The check this earns.** *An object cached across a session boundary must be dropped by whatever
disposes the things it holds.* The texture cache was already handled — the comment beside it even
says "a cached texture whose shader is gone is worse than one render" — and the subject beside it
was not. When something is disposed, ask what else is still holding it, and prefer a property setter
that invalidates over a comment asking callers to remember.

**The generalisation of P14: a cache keyed on identity outlives a change to what identity means.**
Its sibling is the same day's `ColonistAppearance.Equals`, which kept its old idea of "the same
person" after two fields were added, so a portrait cache handed fifteen different hairstyles the
same picture. Both are caches that were right until something underneath them moved.

### 2026-09-23 — Two lanes, one health bar, two ladders; and a gate that still spoke C1 (P1)

**Symptom, caught at the integration before anyone played.** Lane B coloured the health bar green,
amber and red at 60 and 30 per cent; lane C had written `CombatFeedbackModel.HealthBarColour` at the
need bar's 60 and 40 for lane B to call. And lane C's model sent an undrafted colonist for a weapon,
while lane B's presenter returned before asking the model unless someone was drafted.

**Cause.** Both are one rule with two owners, split across two lanes that each built their half in
a separate worktree: what colour a bar is, and who hears a right-click. Each lane's tests were
green, because each tested its own copy.

**Fix.** The bar asks the model (`CombatMarks.BarInk` deleted); the presenter's gate is
`OrderModel.HearsRightClick`. `docs/design/33-combat.md` §6E.

**The check this earns.** *When lanes split a feature, list every question both sides answer, and
give each one owner in the brief.* The contracts named which lane "writes" and which "calls" for the
four fixed answers, and those four did not diverge. The bar colour was an answer lane C offered as
optional, so lane B wrote its own; the gate was a rule the brief gave lane C in a file it gave lane B.

### 2026-09-23 — Side by side held for attackers, and nobody else in the fight (P15)

**Symptom, found by the guard before anyone played it.** The owner asked that fighters never
share a tile, *"handled uniformly"*. §7c had given every attacker a side of its own. A test that
walked every tick of mixed brawls then found fighters standing together for hundreds of ticks
anyway. A bandit stood on a downed body a colonist was finishing off beside it. Two drafted
colonists swung from one tile. A colonist ordered onto a bandit's tile was given it.

**Cause.** The rule was written for one side of the relation. An attacker held its side, but a pawn
being attacked held nothing. Three ways into a fight also had no rule at all: the drafted hold
("strikes from where she stands"), drafting in place, and the move order's spread. Each was right
for the pawns it had been written for, and none asked about the others.

**Fix.** One answer, `Melee.Holds`: every fighter holds its `SideOf`, attacker or target. The
hold, the draft and the move order all ask it. `docs/design/33-combat.md` §8c.

**The check this earns.** *When a rule is about a relation, test it over the relation, not over
the actor that prompted it.* `SideBySideTests` checked attackers against attackers, and all of
them passed. `FightGuardTests` checks everyone in the fight, on every tick. Each hole it found was
seen to fail with its fix withheld.

### 2026-09-24 — Colonists past the figure cap wore an orange suit (P1)

**Symptom.** Past a certain number of colonists, some were "in an orange suit" and textures "kept
switching".

**Cause.** Two drawers of one colonist gave two answers. The live figure paints the issued uniform
white, but the baked far form (past the 64-figure cap) draws each body in the pack's own paint, and
that jumpsuit is painted burnt orange. The nearest-64 set follows the camera, so people changed
clothes as it panned.

**How it hid.** The first investigation matched the report's colour to the orange stand-in cube,
measured that no stand-in was drawn, and stopped with the report unreproduced. The measurement was
right; it answered a different question.

**Fix.** `ColonistAppearance.IssuedCloth` says which bodies wear one colour for everybody, and
`ChunkRenderer.FarMaterials` paints those bodies' cloth through the shared `ColonistMaterials`.
Design 29-modular-colonists §13a.

**The check this earns.** *When a symptom starts "past a certain number", look at the caps first*.
Behaviour changes form at a cap, and there is more than one drawer of a thing past it. And *a colour
in a report is a hypothesis until the asset's own paint has been sampled*: one script that sampled the atlas
answered what a 384-colonist sweep could not.

**A second fault from the same branch, in the harness.** The spawn ceiling made the frame sweeps hang CI. `GrowColonyTo` used one counter both
to place colonists and to give up, and it reset that counter on walking off the board. The escape
was reachable only while spawns succeeded. *Check: a retry loop's escape must be a counter that
nothing resets, and a loop that ticks without yielding must be bounded by it.*
