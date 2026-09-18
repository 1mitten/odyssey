# Beds — ground findings and interview

**Question.** The owner wants the first furniture: a buildable single bed that spans two cells,
placed from the Build palette as a rotatable outline (R), built by colonists through the existing
pipeline, with a completion quality roll (Poor / Normal / Decent / Uber / Epic) driven by skill,
then owned by a chosen colonist who sleeps in it. Beds more effective inside a building is a seam
for later. This file holds the ground findings and the interview rounds; the design, when the
answers are in, goes to `docs/design/20-beds.md` (numbered 20 because the unmerged start-flow
and floors branches have already taken 17–19).

Grounded 2026-09-17 on `main` at `77615ee`. Every claim below was read in the code or measured
from the committed Synty inventory; file:line citations are `main` unless stated.

## What already exists (more than the plan expected)

**A "bed" is already a first-class concept in the simulation — an invisible one.**
`ColonyItems` keeps a sorted list of bed *cells* (`ColonyItems.cs:76`, filled only by the start
scenario at `ColonyScenario.cs:540`). The sleep think node scans it for the nearest reservable
bed (`JobSystem.cs:524`) and a tired pawn with no bed lies down where it stands with a
`SleptOnGround` memory (`JobDrivers.cs:175`, mood −40 in `Thoughts.xml`). Rest recovery is
already split: **a bed cell restores at 100, the ground at 80**
(`NeedsSystem.cs:74`, `groundRestEffectiveness` in `Colonist.xml:57`). A buildable bed upgrades
this notion from a stamped cell to a real thing; the sleep chooser and the effectiveness split
already exist and are the hooks it lands on.

**The build pipeline is generic over the thing built.** Sites are (building, stuff, delivered,
work) per cell in `ConstructionGrid`; `DeliverWorkGiver` and `BuildWorkGiver` scan `Sites` and
never name a wall (`BuildJob.cs`). A bed site flows through hauling and building untouched.
Cancel and deconstruct already handle any built edifice (`PlacedEdifice.Built` gates
deconstruct, refunding half).

**Quality is U26's outstanding "success roll at completion".** No field exists on `PlacedEdifice`
(`WorldGenContext.cs:113`) and no skill-based outcome exists anywhere — skill today is a gate
(`minSkill`) plus XP. The hook point is the driver's completion branch (`BuildJob.cs:403-406`)
threading a rolled quality into `ConstructionGrid.Raise` (`ConstructionGrid.cs:291`), which today
constructs the record. A new field rides `EdificeSaveSection` and moves goldens deliberately.
a-04 §4 has the reference shape: quality rolled **once, at completion, from the finishing
pawn's** skill on a bell curve; walls and doors do not take quality, furniture does — the same
split the owner drew.

## What does not exist and must be designed

- **Multi-cell buildings.** Every `PlacedEdifice` is one cell; `CellGrid.Edifice` is one index
  per cell (`CellGrid.cs:35`). The only two-cell thing is the ruined city's stairs: two records,
  each cell its own, the partner found by neighbour scan and orientation inferred at draw time
  (`ShellTemplate.cs:230`, `ChunkMesher.EmitStair:603`). The floors branch (`claude/floors-review`)
  adds one-cell ladders and floors — **no 2×1 horizontal footprint pattern exists anywhere yet**.
  A bed is the first: two records with a shared identity, or one record owning a footprint.
- **Placement rotation.** No rotation input exists; the ghost is a drag-rectangle span box
  (`BuildPreview.cs:63`). Doors, stairs and ladders already self-orient at *draw* time by
  scanning neighbours (`ChunkMesher:595-627`), so a stored orientation has a precedent to render.
  **R is already bound to slice-up** (`HotkeyDirector.cs:132`).
- **Ownership.** No pawn owns anything; reservations are transient by design
  (`ReservationManager`, released on job end). A bed owner is persistent sim state: a field on
  `Pawn` saved by `PawnRegistry`, hashed, and published to the Hud through the sparse
  `PawnAspect` channel (`Views.cs:239`) — which is view-only by contract, so the state itself
  must live on the pawn or a registry. Names resolve Hud-side from `PawnId` (`ColonistNames.Of`).
- **The pane cannot say who owns a bed.** `CellDetail` rows carry fixed fields
  (`Views.cs:403`) — owner and quality need the contract widened or a sibling channel.
- **No lying-down pose.** `PawnView` has no asleep flag; sleep is invisible today but for the
  job label. (a-02/e-02: no sleep clip in any pack; would be Mixamo or computed.)

## Bed assets (measured from the committed inventory, `synty-inventory.csv`)

| Prefab | Size (m) | Fits | Notes |
|---|---|---|---|
| `PolygonFarm SM_Prop_Bed_01` | 2.08 × 1.52 × 2.67 | **one cell** (0.17 m overhang long-axis) | 680 tris, 1 material; e-01 named it *the* bed module; e-03: "wooden but plain — fine as scrap-built colony furniture" |
| `PolygonWesternFrontier SM_Prop_CampBed_01` | 1.11 × 0.70 × 3.05 | one cell | 156 tris; the early-game bedroll tier |
| `PolygonSciFiCity SM_Prop_Carpet_Mat_01` | 2.54 × 0.10 × 3.80 | one cell | floor mat, not a bed |

**Nothing in any pack spans two cells (5 m) at bed proportions.** e-01's gap list says "bed
(Farm `SM_Prop_Bed_01`)" — written when one cell was assumed. A true 2-tile bed needs either new
art (Blender under `Assets/Art/Custom/`, per the conventions) or a stretched/derived use of the
Farm bed. The house precedent for missing art is an honest computed placeholder (work swings,
rubble heaps, synthesised audio).

## Fixed by earlier decisions (not re-opened)

- Cell 2.5 × 2.5 × 3.0 m, irreversible (ADR 0002) — so a 2-tile bed is 5.0 × 2.5 m, longer than
  any real bed; a 1-tile bed is already king-size.
- Content values live in XML once (`Buildings.xml`) with the in-code oracle pinned by
  `ConstructionContentDefTests`; new names are wiki content (`icon-keys.csv`) in the same commit.
- Edifice drawing is the module path (`WorldRenderModel.EdificeModule` → `ChunkMesher.AddBody`),
  with a yaw-rotation precedent at door/stair/ladder.
- The room/effectiveness seam (walls, doors) is later: rooms do not exist, a-05 already holds
  the reference shape (private bedroom moodlet etc.), and U42's paving likewise waits on rooms.

## Interview

### Round 1 — asked and answered 2026-09-17

1. **Footprint and art: two tiles, computed placeholder art.** The bed occupies 2 × 1 cells and
   is drawn as an honest placeholder (frame, mattress, pillow — the computed-swing and
   rubble-heap idiom) until proper two-tile art exists.
2. **Rotation key: R while a rotatable ghost is armed.** R rotates the armed ghost; PageUp
   remains slice-up always, and R still raises the slice whenever no rotatable build is armed.
3. **Owner assignment: click the bed, assign from its pane.** A built bed's pane row opens a
   popover listing colonists — the pane's first interactive row, and a first step toward the
   planned A10 command grid.
4. **Quality: tiers scale rest effectiveness now**, with provisional numbers in XML (Poor 85 /
   Normal 100 / Decent 112 / Uber 125 / Epic 140; ground is 80, a qualityless bed cell is 100).
   Tunable without code, so the numbers remain "decided later" in the only sense that matters.

**Defaults carried unchanged** (stated so they can be vetoed): bed cells are passable at
terrain cost (a crossing-cost surcharge is a follow-up once a number is chosen); a bed costs 5
wood and is buildable in wood or stone; ground sleeping and the −40 `SleptOnGround` memory are
unchanged; the scenario's start-of-world bed cells stay as they are; deconstruct refunds half
like a wall; a lying pose is out of scope for the first cut; the room bonus is a recorded seam
only; one bed per colonist and one colonist per bed.
