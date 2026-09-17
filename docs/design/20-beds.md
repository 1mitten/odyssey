# 20 — Beds: the first furniture

Design for the first buildable furniture: a single bed spanning two cells, ordered from the
Build palette, rotated with R, built through the existing pipeline by colonists, finished at a
rolled quality from the finisher's skill, then owned by one colonist who sleeps in it. The room
bonus ("much more effective inside a building") is a seam recorded here and built when rooms
are.

Ground and the owner's four answers are `docs/research/beds-interview.md`; read it first. What
follows is the design. Numbered 20 because 17–19 are taken on the unmerged `claude/start-flow`
and `claude/floors-review` branches.

## 1. What was asked (owner, 2026-09-17)

Select bed from the build menu → see an outline placeable anywhere and rotatable with R → the
single bed spans two tiles → placing it creates build work colonists complete through the
existing systems → at completion, a skill-based chance of a different quality — Poor, Normal,
Decent, Uber, Epic — as the deciding quality of a built item → a colonist can then be selected
to own that bed and sleep in it → beds are much more effective inside a building (walls, doors):
seam for later.

## 2. Owner decisions (interview round 1, 2026-09-17)

1. **Two tiles, computed placeholder art.** Frame, mattress, pillow — the computed-swing and
   rubble-heap idiom — until real two-tile art exists.
2. **R rotates while a rotatable ghost is armed.** PageUp remains slice-up always; R raises the
   slice whenever no rotatable build is armed.
3. **Owner assigned from the bed's pane** — the pane's first interactive row, a popover listing
   colonists.
4. **The five tiers scale rest effectiveness now**, provisional numbers in XML: Poor 85 /
   Normal 100 / Decent 112 / Uber 125 / Epic 140 per cent (ground 80, a qualityless bed cell
   100 — both unchanged).

Defaults stated at the interview and unopposed: passable at terrain cost (no surcharge yet);
5 wood, buildable in wood or stone; ground sleeping and the −40 `SleptOnGround` memory
unchanged; scenario bed cells stay; deconstruct refunds half; no lying pose this cut; one bed
per colonist, one colonist per bed.

## 3. The bed as content

One `BuildingDef` row, the same shape as the wall's: `Building_Bed`, edifice value
`CoreContent.EdificeBed` (a new constant — the list is append-only and its order is the save
contract), `blocking = false`, `rotates = true`, `takesQuality = true`, `costCount` 5,
`workToBuild` 180, `minSkill` 0, icon key `ui.build.bed`, stuffs wood and stone. XML mirror in
`Buildings.xml`; the in-code oracle in `ConstructionContent` and its fingerprint test move with
it — the double guard `ConstructionContentDefTests` already enforces.

Quality is content too: a `QualityDef` set in `Defs/Core/World/Quality.xml` (defName, order,
`restEffectivenessPerMille`), oracle and fingerprint beside it, names `ui.quality.poor` …
`ui.quality.epic` in `icon-keys.csv`. Walls and doors never take quality — the reference's own
split (a-04 §3: furniture and art do, structures do not), now a flag on the def.

The wiki and `Registry.g.cs` regenerate in the same commit as these rows (both `--check`s
green before commit).

## 4. One bed, two cells

**One `PlacedEdifice` record, two index slots.** `CellGrid.Edifice` is already an index into
the edifice list; a bed points both of its cells at the *same* record. `PlacedEdifice` gains:

| Field | Type | Meaning |
|---|---|---|
| *(none — derived)* | int | the second cell is **not stored**: it is always this cell plus the facing offset, and `EdificeFootprint` derives it on demand. The first cut of this design stored a `CellIndexB`, and the reason it went is a language fact worth recording: Unity compiles C# 9, where struct field initializers do not exist, so a `= -1` default cannot be spelled — and cell 0 is a real cell. Zero-safe by construction beat a sentinel that only the fast tier (compiled at `latest`) could enforce |
| `Facing` | byte | 0–3, the rotation as placed; zero on everything that does not rotate |
| `Quality` | byte | 0 = none (walls); 1–5 = Poor…Epic |
| `Owner` | int | `PawnId`, 0 = unowned — 0 being a value no pawn ever has, ids being 1-based, which is what makes a bare struct default say "nobody" |

The alternative — the stairs' two paired records — was rejected on purpose: worldgen's stair
halves are *different defs* that pair by convention; a bed's halves are the same thing, and two
records would have to agree on quality, owner and removal forever. One record cannot disagree
with itself.

Construction sites: a multi-cell site is **one site on the head cell carrying its footprint**
(`CellIndexB`). Both cells must satisfy `ConstructionGrid.Allows` at placement — no
`StandingOn` lift for multi-cell orders (a two-cell thing named into solid ground is rejected
with a reason rather than lifted; if either cell fails, the order fails). Delivery, building
and the frame arithmetic are unchanged — the givers already treat a site as a site. `Raise`
appends the one record, sets `Edifice[head] = Edifice[foot] = index`, marks chunks around both
cells and dirties nav at both. Cancel clears the one site; deconstruct removes the record,
clears both slots, refunds half (the existing seeded flip), and clears the owner.

## 5. Placement, ghost and rotation

- **Single-placement gesture.** With a rotatable building armed, the build tool places one per
  click; the drag-rectangle stays what it is for walls. (Drag-running several beds is a later
  gesture, recorded open.)
- **The ghost is the accepted span-box language** over the two cells of the current facing —
  `DrawCellSpanBox` across a 2×1 span — tinted by whether *both* cells would accept the order.
- **R rotates.** `HotkeyDirector` gains a `Rotate` action. While a rotatable building is armed,
  R drives rotation and does not fire slice-up; PageUp is always slice-up; with nothing rotatable
  armed, R raises the slice as today. `09-ui-and-input.md` §6 gains this input case in the same
  commit.
- The facing is **stored on the record**, never inferred by neighbour scan — the stairs infer
  only because worldgen had nowhere to put an answer, and placing is exactly where the answer
  is known.

## 6. Quality — closing U26's outstanding success roll

Rolled **once, at the moment of completion, from the finishing pawn's Construction skill**
(a-04 §4's shape, Odyssey's own numbers): the roll happens in `BuildJobDriver`'s completion
branch and rides into `ConstructionGrid.Raise`. The finisher, not the starter, holds the roll —
a low-skill pawn can do 99 per cent of the work and a master finish it; that property is kept
deliberately, as the reference keeps it.

Determinism: the roll draws from the job context's random stream at raise time — the same
discipline as the deconstruct coin flip, reproducible from the seed and covered by the golden
run. The *weights* live in `Quality.xml` (bell-ish over the five tiers, shifting right with
skill: skill 0 never reaches Epic; skill 20 never falls to Poor) — the tests pin the table and
the endpoints, never the drawn values.

### 6a. A tier has a colour, and one place decides it (owner, 2026-09-17)

*"Poor (red), Normal (no change), Decent (a light yellow), Uber (teal) and Epic (purple).
Anywhere quality is mentioned there should be centralised colours."*

`HudTheme.Quality(tier)` is that place, beside every other interface token. It returns
**null for Normal**, which is the specification read literally and is not the same as returning
the body colour: a tier named on a card, in a tooltip or in a log line keeps whatever colour
that surface gives it, and a caller that wants a colour to paint with skips the paint rather
than substituting one. Poor reuses the HUD's existing `Bad`; the other three are new, because a
tier is a judgement about a thing rather than an alarm and three more signal colours would make
every signal colour mean less. Decent is a yellow rather than the green a "good" tier would
otherwise want, so that it stays apart from Poor's red for the commonest colour blindness.

The tier's colour rides on the row (`InspectRow.Tint`) rather than being chosen by whatever
draws it, which is what will make a second surface naming a tier agree with the pane for free.
A fast-tier test holds every tier to its theme colour and every colour to
`HudContrast.BodyMinimum` on the darkest panel the game draws.

## 7. Ownership and sleep

**The owner lives on the bed record and nowhere else.** No `Pawn` field, no `PawnRegistry`
change: the bed knows its owner; a pawn's bed is found by scanning beds, which the sleep
chooser already does and which is cheap at every scale this game has (beds are few, the think
runs on a heartbeat, not per tick).

- **New intent `AssignBedOwner(cell, pawnId)`**, `pawnId = −1` to unassign. It validates that
  the edifice is a *built* bed and the pawn exists, and first clears any other bed that pawn
  owns — one bed per colonist is enforced by the handler, not by hope. It is a command intent:
  queued while the clock is paused, applying on unpause (the recorded tolerance for commands).
- **Built beds join the bed list the sleep chooser already scans.** `ColonyItems` keeps its
  scenario cells and gains the head cell of every built bed; `Raise` adds it, demolition
  removes it. The chooser's rule becomes: my own bed (if reachable and reservable), else the
  nearest unowned bed, else the ground and the −40 memory, unchanged.
- **Effectiveness replaces the hardcoded 100** (`NeedsSystem.IsBed`): a scenario cell restores
  at 100; a built bed at its quality's `restEffectivenessPerMille`; the ground at 80. The
  quality table becomes the single source of the numbers.
- One sleeper per bed: the head cell is reserved for the sleep, as cell reservations already
  work. Deconstructing an owned bed clears the owner; the pawn falls back to unowned beds and
  the ground.

## 8. The pane and the popover

`CellDetail` widens by two sparse fields, the same shape as its neighbours (ADR 0004 amendment
2's row): `EdificeQuality` (byte, 0 = none) and `EdificeOwner` (int, −1 = none). The bed's pane
then reads: title **Bed**, a **Quality: Decent** row, and an **Owner: Ava** / **Owner: —** row.

The owner row is the pane's first interactive row: clicking it opens a popover listing the
colonists (plus *No owner*); a pick submits `AssignBedOwner`. Names resolve Hud-side by
`ColonistNames.Of(pawnId)` — the channel `PawnAspect` rows already use, so no new coupling. The
popover machinery exists; this is the first time the inspect pane pushes rather than only
reads, and it is deliberately the smallest version of the planned A10 command grid.

## 9. Drawing the placeholder

One computed body per bed, emitted from the head cell only (the foot cell emits nothing): a
frame slab, a mattress and a pillow — boxes tinted by stuff, rotated by `Facing`, centred on
the seam of the two cells and **draped** like everything fixed to the grid. No catalogue row.
When real two-tile art exists (owner or Blender, `Assets/Art/Custom/`, Synty style, snapped to
the grid) the case becomes a module id and the placeholder is deleted, not kept beside it.

A bed body can straddle a chunk boundary when its two cells sit across a 10-cell edge;
instances are not clipped by their bucket, so it renders correctly — written here so nobody
"fixes" it later.

### 9a. What the owner's first look changed (2026-09-17)

Four reports, and all four were about the bed being drawn as *boxes* rather than as a bed.
`BedShape` is now written in **metres** rather than in raw scale factors, because two of the
three parts are drawn from module boxes of different sizes and a bare `Scale` meant a different
thing for each — which is how the numbers drifted apart in the first place.

- **The pillow floated.** The mattress reached `z = ±1.02` and the pillow sat at `z = −1.75`,
  so there was 0.56 m of open air between them and the pillow hung past the end of the bed.
  The frame and mattress now run the bed's whole length and the pillow rests on the mattress at
  the head. `BedShapeTests` asserts the *relations* — pillow on mattress, mattress on frame,
  everything inside the two cells — rather than the numbers, so all six can be tuned freely and
  none of them can be tuned into mid-air.
- **The pillow is bigger, rounded and white.** It was 1.15 × 0.18 × 0.33 m, which is the size
  and shape of a book; it is 1.30 × 0.26 × 0.95 m now. Rounded needed a mesh —
  `PillowMesh`, a superellipsoid spanning the same −0.5..0.5 unit box every stand-in does, with
  **smooth normals**, the only smooth-shaded thing in the renderer: everything else is
  hard-normalled because the flat-lit look depends on it, and a pillow is the one thing in the
  game that is meant to read as soft. White needed a tint that is not a stuff — none of the six
  stuffs is cloth — so bedding got a `TintCode` bit of its own beside foliage and water, and a
  stone bed has the same linen pillow a wooden one does.
- **Selecting a bed highlighted the whole cell.** A bed is two cells long and knee high and is
  the one edifice that does not fill the cell it stands in, so a cell highlight was wrong about
  its size, its facing and both of its ends. It is a selection bracket round the bed's own box
  now, measured from the head cell whichever half was clicked.
- **The pillow's colour and shape are in the ghost too**, because the build cursor's whole
  bargain is that what is under the pointer is what stands on the board.

## 10. Save, hash, goldens, merge order

`PlacedEdifice`'s three new fields — `Facing`, `Quality`, `Owner` — ride `EdificeSaveSection`;
an older file reads none of them and every restored building comes back facing north, of no
quality and owned by nobody, which is what those colonies *were*. (`CellIndexB` was a fourth
field in the first cut and went: Unity compiles C# 9, where struct field initializers do not
exist, so a `= -1` default would have passed every fast-tier run and broken the Unity tier.
The second cell is derived from the facing instead.)

### What the merge actually did (2026-09-17)

This section predicted the dance and both halves of the prediction came true, so here is the
record rather than the forecast.

**Beds merged second, and the bed's handle moved.** `BuildingHandle.Bed` was written as 2, the
next number after the wall. U29's floor, U42's paving and U43's ladder reached main first and
took 2, 3 and 4, so the bed is **5** and `Count` is **6**. A handle position is a save contract
and positions are append-only — the later branch is the one that moves, and that was only safe
because no save with a bed in it had ever left the branch. `BuildingOrder`, `Buildings.xml`,
`BuildLabels.BuildingKeys` and `BuildShapes` all follow the number.

**Save format is 4, not 3.** The start flow took 3 for the recipe's `Barren`/`Wooded` fields.
Versions 1, 2 and 3 all still load.

**The renumbering's one silent casualty was `BuildShapes`.** It is a hand-written table parallel
to `BuildingHandle`, main had never touched the file, and so it was *not* a merge conflict: its
three entries merged in silence and the bed quietly became a one-cell thing that could not be
turned. The class's own remarks claimed "the two tables are held together the same way the
labels are — a test walks both", and no such test existed.
`RegistryTests.EveryBuildableHasAShapeOfItsOwn` is that test now. **The general rule: a
hand-written table parallel to a handle set needs a length assertion, or renumbering the handles
breaks it without a conflict to warn anybody.**

**Three seams needed a real merge rather than a union.** `ConstructionGrid`'s constructor takes
both new arguments (the pawn list a bed's owner is validated against, and U29's support solver).
`Raise` keeps main's `RaiseSlab`/`RaiseEdifice` split, and `RaiseEdifice` took the second cell,
the facing and the quality, so both cells still point at the one record. And **the bed's "never
lifted" rule moved out of `Place` into `WhereItWouldLand`**, which main had written precisely so
the cursor and the order could not disagree — leaving it in `Place` would have drawn the bed's
ghost with the wall's lift.

## 11. Test procedure

Fast tier (Sim, no Unity) — written first:

1. **Multi-cell placement.** A valid 2×1 places as one site owning both cells; an obstructed
   footprint is rejected per facing, with a reason; a multi-cell order into solid ground is
   rejected, not lifted; cancel clears both cells.
2. **Raise and round-trip.** One record with `Edifice[head] = Edifice[foot]`; both cells
   non-blocking; save → load restores both pointers; the full hash is equal across the
   round-trip.
3. **Facing.** Each facing's footprint is the rotated pair; both cells stay passable at
   terrain cost.
4. **Quality.** A wall never rolls (`takesQuality` false); a bed always finishes 1–5;
   same seed → same tier; the pinned table's endpoints (skill 0 never Epic, skill 20 never
   Poor) hold — the table is the contract, not the draws.
5. **Ownership.** Assign, reassign (the old bed is released), unassign; round-trip and hash;
   deconstruct clears the owner; `AssignBedOwner` on a non-bed or missing pawn is rejected.
6. **Sleep.** An owned reachable bed is chosen over a nearer unowned one; unowned built beds
   are usable; tier effectiveness applies (Poor 85 / Normal 100 / ground 80); scenario cells
   still restore at 100; the head cell is reserved while slept in.

Hud fast tier: the six new registry names exist and label; the bed's pane rows render quality
and owner, resolving a name; the popover's model lists colonists plus *No owner*.

Unity EditMode: R rotates while a rotatable building is armed and does not fire slice-up;
PageUp always raises the slice; the mesher emits one rotated body from the head cell at the
seam and nothing from the foot cell.

By hand (owner or PlayMode rig): order a bed, rotate it, place it, watch it delivered and
built, assign an owner from the pane, see them sleep there through a night and wake; deconstruct
it and see the owner released.

### What the review found afterwards (2026-09-17)

Three faults that both tiers were green over, found reviewing the merge rather than by a test
failing.

**Four of the six ownership tests were never running.** `RaiseABed` called
`ConstructionGrid.Raise` without placing a site first, and `Raise` reads the site out of
`_building[cell]` and returns at once when there is none — so no bed stood, and each of the four
ended on an `Assume` that a bed they had never ordered could be given an owner. A failed `Assume`
is **Inconclusive, not a failure**: `dotnet test` prints `Passed!` and does not count it in the
skip total, so the whole of §7 — walk past a nearer bed to your own, release the old bed, the
owner surviving a save — was untested while the tier read green. The helper now places the order
and asserts a bed is standing afterwards, and the three `Assume`s that swallowed it are
assertions. All four pass; the feature was right, the tests were not asking.

**`ABedOrderIntoSolidGroundIsRefusedNotLifted` ignored itself on every run.** It searched for a
solid cell at `start.Y`, which is the layer a colonist *stands in* — the ground is the layer
below — so it found nothing anywhere and took its `Assert.Ignore` branch every time. Fixing the
search is what exposed the next fault, because the rule it was guarding turned out to be the
wrong rule.

**A bed could not be ordered by pointing at anything.** The seam test above is what found it, and
it is the same fault U29's floor tool had: `Place` refused the lift for anything wider than one
cell, on the reasoning that raising one end of a bed while the other stayed put is an order whose
shape the player cannot see — while `SlicePicker` answers a click on bare grass with the ground
*block*. Put together, **every bed a player could point at was `NotPermitted`**, and the tool
armed, dragged, previewed and did nothing. The reasoning was wrong as well as costly: the far cell
is derived from the head **after** the lift, so both ends are always on one layer and a far cell
with nothing under it refuses the whole order anyway. A bed now takes the wall's lift, exactly as
paving does. This was not one of §2's owner decisions and is not in §5 — it was a choice made in
`Place`, justified in a comment, and pinned by `ABedOrderIntoSolidGroundIsRefusedNotLifted`, a test
that ignored itself on every run since it was written. That test is now
`ABedOrderIntoSolidGroundIsLiftedExactlyAsAWallIs` and it asserts both ends land on one layer.

**The ghost knew nothing about beds.** The build cursor and the waiting-site ghost both arrived
from the build-cursor work after this design was written, and both drew one cell-filling module
at the head cell: a bed ordered on the grass appeared as a block, and — worse for the feature
§5 exists to prove — **turning the ghost with R changed nothing anybody could see**, because a
cube looks the same all four ways round. `BedShape` now owns the three boxes and the mesher and
both ghost paths ask it, so what is under the pointer is what stands on the board.
`FloorToolReachTests.PointingAtBareGroundOrdersABedInTheAirAboveIt` is the seam test that the
bed is orderable by pointing at grass at all — the one question neither assembly's own tests can
ask, and the one that caught U29's floor tool being armable, draggable and inert.

### 8a. Finding the owner row (2026-09-17)

**The owner could not work out how to assign a colonist to a bed**, and the row had been there
since §8 was written. It was styled with a pointer cursor and a hover brighten and nothing else,
on the argument that a row which shouted would be a button wearing a row's clothes — but hover
is not an affordance on a row nobody suspects, and the row sat between "quality" and "walk
speed", which are facts.

Three changes, all of them about saying it is a control while the pointer is still:

1. **The value reads "Assign…"** where nobody owns the bed, instead of an em dash. The word is
   what actually names the action, and a dash says the opposite — that there is nothing here.
   A fast-tier test had pinned the dash; it pins the word now.
2. **The value is boxed** — a faint fill, a border and a radius — and brightens to the accent on
   hover.
3. **A chevron** sits after it, shown only on the pickable row, so the *owned* case says it is a
   control too, where there is no "…" to carry it.

## 12. Open

- **Real two-tile bed art** — replaces the placeholder; the only art question in this line.
  The palette chip borrows the bunk's drawn shape until the bed has one of its own (the merged
  palette specification forbids the placeholder square, and a bunk reads as a bed at 17 px).
- **Crossing-cost surcharge** for walking over a bed — one number, one seam, deferred with the
  number undecided.
- **Drag-running several beds** — a later gesture in the ToolDirector's set.
- **Lying pose** — no sleep clip exists in any pack (e-02); a computed lean or a Mixamo
  retarget later. Sleep is the job label and a still figure today, and stays that in this cut.
- **"Slept in own bed" memory and the room bonus** — rooms first; a-05 already holds the
  reference shape (bedroom validity, per-bed barracks scoring, the impressiveness tiers).
- Double beds (2×2), medical beds, guest rules — nothing wants them yet.
