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

## 7a. A bed cell is a cell with a bed in it (2026-09-20)

**Later the same day, the played colony stopped starting with beds at all** (owner: *"beds should
never be given on startup / new game — but things seem to work fine"*). `ScenarioDef.Playtest` has
`beds = 0`; the colonists sleep on the ground, take the slept-on-ground thought, and a bed becomes
the first thing worth building. `Bare` keeps its five, because the tests, the goldens and the
ten-day runs baseline on a colony that is rested, and a scenario shorting the colony of beds on
purpose is what the `beds` field was always for. Everything below about the starting beds being
*real* still holds — for `Bare`, and for whatever scenario next gives some.

Two owner reports, one cause.

> *"Some colonists still sleep off the bed, it needs to understand that the bed spans two tiles and
> the body needs rest within those tiles and not off them as it looks like it's trying to rest them
> in the first tile in some circumstances where they are hanging off the bed."*

> *"When I assigned a bed to a colonist and they are asleep — I expect them to get up immediately
> and get into the bed they have been assigned to."*

### The phantom bed cells

§7 above says the bed list "keeps its scenario cells and gains the head cell of every built bed",
and that sentence is the bug. `ColonyScenario` placed five entries in `ColonyItems.Beds` that were
**cells and nothing else** — no edifice, no record. That was correct when it was written, because a
bed *was* a property of a cell and there was nothing to build. It became a lie the day beds became
furniture, because everything a bed now is hangs off the record:

- **It cannot be seen.** Nothing is drawn at the cell.
- **It cannot be owned.** `AssignOwnerAt` refuses a cell with no edifice in it, so the whole of §7
  and §8 — the pane, the popover, the assignment — was dead on every bed the colony woke up with.
- **It cannot be lain on.** Presentation asks `WorldRenderModel.BedHeadAt` which bed a sleeper is
  in, gets nothing, and falls back to the *ground* pose: a colonist laid flat on the grass, centred
  on her own cell, along whatever yaw she last faced. Beside a real bed that reads exactly as the
  owner described — resting in one tile, hanging off.

**Measured before it was changed**, because reading the code has been wrong every time on this
project. Three colonists, three built beds, three days, with and without the phantom cells:

| | colonist 1 | colonist 2 | colonist 3 |
|---|---|---|---|
| with the five phantom cells | **0** ticks on a bed, 53,222 off | 36,709 on, **17,399 off** | 52,919 on, 0 off |
| without them | 52,439 on, 0 off | 52,331 on, 0 off | 53,145 on, 0 off |

Every off-bed sleep was **one to three cells from a real bed she never used**. That is the
screenshot.

**The fix is that a bed cell is a cell with a bed in it.** `ColonyScenario.RaiseAStartingBed` puts
a real `Building_Bed` down through the construction grid and lets `Raise` add the head cell to the
list — one owner for "what counts as a bed". Normal quality, so the colony wakes as rested as it
always did (a qualityless bed cell restored 100 and so does a Normal bed). Two details the
placement needed:

- **Eight footprints per spot, not four.** The spot can be the bed's head *or* its foot, and both
  keep the bed on the storey the scenario named. Four was measurably not enough: on the ruined
  city, where a storey is rooms rather than open ground, two spots in five had no free neighbour in
  the direction a head needed and the colony started three beds short.
- **The bed stays off cells promised to another group.** Every storey is searched up front, so by
  the time a bed is raised the stockpile's cells are already chosen; adding the bed's far cell to
  the taken set would be too late. `Storeys.Spoken` lets the bed ask instead, and it tries the
  unspoken footprints first. This is not a nicety — a bed claims its cells against items, so a
  stockpile cell under a bed's foot is a cell nothing can ever be put in, and the colony's hauling
  stalls two crates short with nothing to show why.

`BedTests.EveryCellTheSleepChooserKnowsHasABedInIt` is the invariant; `NobodySleepsBesideABed` is
the end-to-end form; `AStartingBedCanBeOwnedLikeAnyOther` is the feature that was dead.

### Getting out of the wrong bed

The assignment used to land on the record and be read again only the next time she looked for
somewhere to sleep, so a bed given to a sleeping colonist did nothing anybody could see until the
following night. `JobSystem.GetOutOfTheWrongBed` runs on the tick after any ownership change —
intents drain at step 1 of `SimWorld.Tick` and the pawn phase is step 4, so it is the *same* tick
the player's click lands in, and the think tree is consulted in the same tick a job ends. She is
walking before the hand has left the mouse.

**The rule is about the bed, not about the assignment**, and the first draft got that wrong twice
in ways worth recording:

1. **Naming the colonists involved does not work.** The obvious implementation lists the old owner
   and the new one. It misses the commonest case there is: a colony short of beds keeps them
   unowned and shared (§7's pool rule), so the colonist actually *lying in* a bed when the player
   gives it away is very often nobody's owner and appears in no such list. Who is affected is a
   question about where people are sleeping, which the construction grid does not know. It raises a
   flag; the job system sweeps.
2. **Acting on the change alone loops.** A sleeper claims an unowned bed the moment she arrives in
   it (`TryClaimForSleeper`), and that claim goes through the very same door a player's assignment
   does. Waking on the change would get her up, send her to walk to the bed she is already in, and
   do it again for ever.

So, for each colonist on a sleep job:

| She is | What happens | Why |
|---|---|---|
| in her own bed | nothing, whatever changed | otherwise her own arrival-claim wakes her, for ever |
| owner of a bed somewhere else | up she gets | an own bed wins outright in `TrySleep`; covers the ground sleeper too |
| in a bed that is now somebody's, owning none | up she gets | it is not hers to be in |
| in an unowned bed, owning none | nothing | that is the shared pool working as designed |

The job ends as a **failure**, which is what releases the bed she was holding.

`ASleeperWhoClaimsTheBedSheIsLyingInIsNotWokenByHerOwnClaim` is the negative control and is the one
that matters; `AColonistAsleepOnTheGroundGetsUpForABedSheIsGiven` is the case draft one missed.

**And an interrupted sleep resumes** (second play day, 2026-09-20: *"when I assigned someone else
to a bed — everyone just started going back to work"*). The first version got her up and handed
her to the think tree, and the tree sleeps below the rest `seekThreshold` of 280 and wakes at 950.
A colonist got up at 600, halfway through the night, was by the tree's lights not tired, and went
to work at three in the morning — both of them did, the one who lost the bed and the one who was
given it, which in a colony of three is "everyone". Every test of the rule had assigned the bed
within a tick of her lying down, at a rest of 40, and so never crossed the gap between the two
thresholds. Now the sweep runs in **two passes**: every affected sleep ends first, so every claim
is released, and then each woken colonist goes straight back through `TrySleep` — her own bed if
she has one, the nearest free one if not, the ground if there is none — past the tiredness gate,
because she was asleep and the only question is where. Two passes rather than one because the bed
the player just gave B is the bed A is still lying in: choose in the same pass and whether B gets
her own bed or the nearest spare depends on which of the two the colony list holds first.
`AColonistWokenMidNightGoesToTheBedSheWasGivenNotToWork` and
`GivingOneSleepersBedToAnotherMidNightMovesThemBothAndWakesNobodyElse` hold it, the second in the
colony's own shape: three colonists, five starting beds, each claimed on the first night.

## 7b. The body was measured against the floor (2026-09-20)

**Third report, and the first two fixes were both right.**

> *"Fix the bug where the colonists seem to sleep off the bed — it should know to always put the
> head onto the first tile and then the rest of the body goes on the 2nd tile — it looks like the
> colonists are resting in the centre and hanging off the bed and sometimes even off the bed. It
> needs to be aware of the position and where to lie — this happens sometimes but often enough so
> there is a miscalculation here."*

### The simulation was clean, so the drawing was not

Start where §7a ended. That fix made every cell in the sleep chooser's list a cell with a bed in
it, and the measurement it was proved by was a sim-side one. So the sim was measured again first,
on the branch the owner is playing: three colonists, three player-built beds, three days, four
seeds.

| | on a bed's head cell | on its foot cell | off a bed |
|---|---|---|---|
| seed 20260917 | 52,439 / 52,331 / 53,145 | 0 | **0** |
| seed 20260918 | 52,992 / 53,840 / 54,202 | 0 | **0** |
| seed 20260919 | 53,828 / 53,453 / 52,869 | 0 | **0** |
| seed 20260920 | 52,482 / 51,808 / 54,166 | 0 | **0** |

Every sleeping tick on the head cell of a real bed. The colonist the owner photographed hanging
off a bed **was in that bed**, exactly as she was in §7a's first round — and, as then, the fault
was in what was drawn rather than in what was simulated. This time, though, it was not that the
pose was missing. It was that the pose was being handed a body of the wrong size.

### `StandingHipHeight` measures the floor

`SleepPose.BodyLength` was `standingHipHeight * 1.9`: a person's hip is a little over half their
height, so the reciprocal turns the one length the director knows about a character into the
length of body it has to lay down. The length the director knows is set once at bind:

```
figure.StandingHipHeight = Mathf.Max(0.2f, figure.Hips.position.y - figure.Transform.position.y);
```

and `figure.Hips` is `animator.GetBoneTransform(HumanBodyBones.Hips)`. **On the Synty humanoid
avatar that bone is called `Root` and it sits at the model origin.** The real pelvis is its child.
Measured, with the graph played and evaluated exactly as `PawnFigureDirector.Create` does it
(`scripts/unity.sh exec Odyssey.EditorTools.SleepProbe.Run`):

| | drawn metres above the figure's root |
|---|---|
| `Root` — what the avatar calls Hips | **−0.010** |
| `Hips` — its child, the real pelvis | 1.211 |
| `UpperLeg_L` | 1.164 |
| `Head` | 2.181 |
| crown of the drawn body | **2.488** |

So the subtraction is nought on every one of the sixty-one characters, the clamp is the whole of
the answer, and every colonist in the game reports a hip height of exactly 0.2 m. The body length
that came out of it was **0.38 m** for a colonist who is **2.49 m** tall — a sixth of her.

### What that draws

The placement is right and was never in doubt: the head goes on the pillow, the root is the feet,
and the body extends one body-length behind the root. Give it a 0.38 m body and the root lands
0.38 m past the pillow — and the 2.49 m figure attached to that root reaches 2.1 m the other way.
Measured, along the bed's facing from the head cell's centre, with the two cells at [−1.25, 1.25]
and [1.25, 3.75] and the frame at [−1.05, 3.55]:

| | body, along the bed |
|---|---|
| before | **[−2.57, 0.29]** — 1.5 m of colonist past the head end, on the floor |
| after | [−0.30, 2.19] — head on the pillow, feet a fifth of the way into the foot cell |

Which is the report. Half of her — 1.3 m of the 2.9 m she spans — is past the head of the bed and
over the floor; what is left on the bed sits around the middle of the first tile, which is
"resting in the centre and hanging off the bed"; and the second tile is empty. The lift was wrong
by the same factor — half a torso's thickness came out as 32 mm rather than 0.21 m — so what was
still over the mattress was inside it.

### Why two rounds of fixes and a test did not catch it

`SleepPoseTests.ASleeperLiesWithinTheBedsOwnTwoCells` was written in §7a's round for exactly this
question, and it passed. It walked a range of plausible **hip heights** — 0.70 m to 1.30 m — and
checked the body each one implies. Every one of them fitted. The number the director actually
passed was 0.2 m, which no range starting at 0.70 m can reach.

That is the pattern, and it is one of the four in `docs/bug-patterns.md`: **a test that fixes the
input instead of asking where the input comes from.** The fixture's `const float Hip = 0.95f` is a
perfectly reasonable hip for a person, and it was never the number the game used. So the test now
walks the **body length** — the quantity the placement is a function of — from 1.5 m to 3.2 m, and
`FigureBuildTests` measures a real rig and asserts the measurement looks like a person.

### The fix

**A body's length is its own drawn height, and it is measured rather than derived.** `FigureBuild`
bakes the posed mesh and takes the sole and the crown — the idiom `MeasureSole` already uses, and
for the same reason: a bone's meaning is a decision made by whoever rigged the character and cannot
be assumed, while where the drawn vertices are is what the player is looking at. There is no ratio
left between the measurement and the thing measured, because every ratio here has been wrong once.

Three details the change needed.

- **The bounds are not the mesh.** `SkinnedMeshRenderer.bounds` is the loose precomputed volume:
  on this cast it runs −0.296 m to 2.618 m on a body that is 0.000 m to 2.488 m. A third of a metre
  of slack at each end is far too much for a body that has to fit a bed, and it is the same error
  that once measured a boot sole at 0.394 m.
- **`BakeMesh(useScale: true)` applies the renderer's own local scale, not the figure's.** The 1.4
  lives on the root above it, so the full local-to-world is still needed afterwards. Measured, the
  three combinations give 2.488 m, 3.483 m and 1.777 m for one figure, and all three look like
  heights.
- **A length that is not one falls back on a colonist.** `SleepPose.BodyLength` refuses anything
  outside 0.5 m to 5 m. The failure this section is about was silent precisely because 0.38 m is a
  number and every line downstream went on working; the worst a broken rig can now do is draw one
  character the wrong size.

### And the arms, which the same measurement exposed

Once the body was the right size and resting on the mattress, the probe could see the limbs — and
**both supine postures were wrong, in opposite directions.** A positive pitch swings a supine
sleeper's arm *downward*, through the bedding; a negative one raises it. The first cut had it the
other way about, and nobody could tell while the whole colonist was hanging off the end of the bed.

Swept through the arm's whole arc, with the body's own top 0.66 m above the mattress, so anything
above that is a limb in the air and anything below nought is a limb through the bed:

| arm | elbow | lowest drawn point vs the mattress | highest | reaches past the crown |
|---|---|---|---|---|
| **+118 (was "arms up")** | +58 | **−0.54** | 0.66 | 0.22 m |
| **−62 (was "back")** | +14 | 0.01 | **≈1.25** | — |
| −150 | −15 | 0.01 | 0.66 | 0.42 m |
| −10 | +15 | 0.01 | 0.66 | — |

So *"back"* held both arms 0.55 m in the air above a colonist lying flat, and *"back, arms up"*
drove both forearms half a metre through the mattress and out past the head of the bed. They are
−10°/+15° and −150°/−15° now: arms level with the body and touching the mattress, and arms stretched
flat above the head ending 0.42 m past the crown and still 0.34 m inside the frame.

**The two side postures were measured in the same pass and were already right** — their limbs sit
0.07 m to 0.10 m above the body's own top and nothing dips below the mattress — so they are
untouched. Which is the useful half of the result: the fault was in the two postures that share a
roll of zero, not in the pitching.

### What the contact sheet showed

`SleepCheck` (`scripts/unity.sh shot Odyssey.EditorTools.SleepCheck.Run`) photographs one colonist
per posture in a real bed, side on, along the bed from the foot, and at the board's own 48° pitch.
It exists because none of the assertions above answer the question the owner actually has to
answer: the arithmetic was *correct* throughout the two days the bug existed, and a sheet that
photographed the body without the bed under it would have looked convincing the whole time.

Two things it settled and one it raised.

- **The fix is visible.** Every colonist on a bed lies along it with her head on the pillow in the
  first tile and her feet well inside the second. A colonist who could not reach a bed lies
  person-sized and flat within her own cell rather than as the 0.38 m blob she was.
- **The mattress beyond the feet is a third of the bed.** A 4.6 m bed and a 2.5 m colonist leave
  about 1.3 m of empty bedding past her boots, in every shot. That is the cell size doing what the
  cell size does (ADR 0002, irreversible) and not a fault, but it is the first thing the eye lands
  on and it is written here so the next report about it is answered in a sentence.
- **"Back, arms up" reads as arms spread, not as arms overhead.** It is safe — measured, it clears
  the mattress by a centimetre and stays inside the frame — but at the play camera the arms go out
  sideways at something near 45° rather than up past the crown, which reads closer to
  *surrendering* than to *asleep*. **The posture cannot currently express what it is named**:
  `Posture` carries one pitch per arm, taken about the body's lateral axis, and a rotation
  about that axis cannot pull an arm in towards the head — whatever lateral spread the idle clip
  already holds is carried round with it. Bringing the hands in over the crown needs a second angle
  on the struct (an abduction), which is a change to the shape of the pose rather than a tuning of
  it, and it is the owner's to call against the picture.

### What was deliberately not changed

`StandingHipHeight` still measures the floor, and it is left doing so. The only thing that still
reads it is the gesture crouch — `ApplyGesturePose` draws `min(depth, DeepestCrouch) *
StandingHipHeight` — and that is tuned against what it actually returns, by photograph, with the
stoop and the lift signed off by the owner (§CL, `24-carrying.md`). Correcting the measurement
without retuning those deepens every crouch six-fold, which is a visual change nobody asked for,
and the retune is a contact sheet rather than a test. So the field keeps its name, gains a comment
saying plainly what it is, and nothing derives a length from it again.

### Open when this was written, and closed in §7c below

**A sleeper lies flat; the bed it lies on is sheared.** Everything fixed to the grid is draped —
`BedShape.Root` is `GroundRelief.Drape(...)`, which tilts the bed's 4.6 m along the ground's
tangent plane — while `AimSleep` samples one height at the bed's origin and lays the body level on
it. At the relief's steepest (amplitude 2.0 m, period 150 m, so 0.136 rise per metre) that is
0.21 m of disagreement at the pillow and 0.09 m the other way at the feet: a body sunk into the
mattress at one end and floating above it at the other, varying with where on the board the bed
stands and which way it faces. It is second-order beside a body six times too short and it is a
change to how a sleeper is drawn, so it is recorded here with its numbers and left for the owner
to judge against the screenshot they now have.

### 7c. The two things §7b left, both done (2026-09-20)

Owner's call after the contact sheet: do both.

#### A sleeper lies along the bed, not level across it

Everything fixed to the grid is draped. `BedShape.Root` is `GroundRelief.Drape(...)`, a **shear**
that takes the ground's tangent plane at the bed's own origin and carries the whole 4.6 m of bed
along it — while `AimSleep` sampled one height at that origin and laid the body flat on it. At the
relief's steepest (2.0 m over a 150 m period, 0.136 rise per metre) that is **0.21 m** of
disagreement at the pillow and 0.09 m the other way at the feet: buried in the mattress at one end,
floating above it at the other, varying with where the bed stands and which way it faces.

`SleepPose.Place` now takes the plane rather than a point — `surfaceY` is the height under the
**head**, and `alongSlope` is its gradient along the bed. Three consequences, each small and each
wrong if left out:

- the feet stand `alongSlope × bodyLength` above the head, so the body is *on* the plane;
- the lying pitch is `90° + atan(alongSlope)`, so the body is *parallel* to it — either alone would
  pass while the sleeper hovered over a bed she matched the angle of;
- the roll is taken about the body's **own** long axis, which is now the tilted one. About `flat` it
  was right while the body was level and a few degrees out once it was not, and the correct axis is
  free because the pitch has just produced it.

The ground fallback gets the same treatment from the same field, so a colonist who collapses on a
hillside lies along the hill.

Asserted by `ASleeperOnASlopeLiesAlongItRatherThanLevelAcrossIt` at the full ±0.136 — both halves,
the gradient the body carries and the height its feet stand at — and by
`NoSlopeIsTheLevelPlacementUntouched`, because the cheapest way for a new argument to go wrong is to
change the answer when it is nought.

#### `Posture` gains a second angle, and "arms up" means it now

The sheet said the posture still read as arms *out*, near 45° from above, which is surrendering
rather than sleeping. **The pictures were right and the reason was not what it looked like.**
Measured, the arms were never off the bed: the posture spanned 1.06 m across a frame 2.00 m wide,
the same as `"back"`. They simply lay out to the sides instead of over the crown — and **no pitch
about the lateral axis can bring them in**, because every angle in `Posture` moves a limb in the
plane that runs head to foot, and whatever spread the idle clip already holds is carried round with
the arm.

So `RightArmOut` / `LeftArmOut`: an abduction, taken about the body's *forward* axis, which on a
sleeper on her back is the vertical — it swings the arm in the plane of the mattress, between out
across the bed and in along it. Nought on every posture that does not ask for it, so the three that
were measured right are untouched by its existence.

Swept through its arc on the real rig, **+15°** (mirrored) is the one place that costs nothing: it
is both the narrowest the arms get and the furthest they reach past the head.

| abduction | across the bed | reach past the crown |
|---|---|---|
| −30° | 1.94 m | 0.48 m |
| 0° (was) | 1.06 m | 0.71 m |
| **+15°** | **0.71 m** | **0.73 m** |
| +30° | 0.68 m | 0.67 m |
| +45° | 0.70 m | 0.55 m |

#### Where the four postures finally sit

Measured on the cast at the catalogue's scale, along the bed from the head cell's centre. The two
cells are [−1.25, 3.75], the frame is [−1.05, 3.55] and 2.00 m wide, and "clears" is the lowest
drawn vertex against the mattress top:

| posture | along | across | clears |
|---|---|---|---|
| back | [−0.29, 2.29] | 1.06 m | +0.01 |
| back, arms up | [−0.73, 2.27] | 0.71 m | +0.01 |
| side, curled | [−0.29, 2.01] | 1.99 m | 0.00 |
| side, loose | [−0.29, 2.25] | 1.68 m | +0.02 |

`side, curled` is 0.04 m wider than the frame on one side — a drawn-up knee just over the rail,
which is what a knee does. Recorded rather than tuned.

### 7d. The owner watches it: no arms above the head, and the trunk on the bedding (2026-09-20)

> *"There's a pose that shouldn't be a sleep pose — any arms above the head — and I see a pose often
> with 2 arms/hands above the head when they can be down the side. Also the body isn't quite flush
> on to the bed surface but the pillow head is placed nicely enough."*

**"Often" is exact, and it is not bad luck.** A posture is `PostureFor(pawnId)`, a hash taken modulo
four, so each of the four is *a quarter of every colony, by construction*. One shape the owner
dislikes is one colonist in four, every night, for ever. That is worth knowing before tuning
anything: there is no frequency to reduce, only a shape to replace.

#### The fourth posture is another arms-down one, differing below the waist

Chosen by the owner from four options. And it took a new field, because the struct could not say it:
the arms have been per-side since the table was written and the legs never were, so `Hip` and `Knee`
drove *both* legs by the same amount — which is a beach, not a bed. `Posture.LeadHip` / `LeadKnee`
are an extra applied to the right leg on top of the shared pair, nought on the three postures that
do not ask for them.

The replacement is `"back, one knee up"`: arms at the sides exactly as `"back"` has them, right hip
−35° and right knee +40° in total, left leg flat. Swept on the real rig first — the band of leg
angles that raises a knee without driving a heel through the mattress runs from about −50° of hip
down to −20°, and everything at or above −10° of hip with more than 20° of knee puts a foot in the
bedding.

Its along-the-bed extent is unchanged from `"back"` at [−0.29, 2.28], which is right rather than
suspicious: the *left* leg still lies straight and it is the left foot that sets the far end.

#### The trunk sets the lift, not whatever hangs lowest

**The supine pair were already flush and the side pair were not.** `Lift` had been tuned until the
lowest drawn vertex *anywhere* on the mesh just touched the mattress — and on a side sleeper that
vertex is a drawn-up knee, which props the body up like a kickstand:

| posture | lowest vertex | trunk, before | trunk, after |
|---|---|---|---|
| back | +0.01 | +0.01 | +0.01 |
| back, one knee up | +0.01 | +0.01 | +0.01 |
| side, curled | 0.00 | **+0.09** | −0.02 |
| side, loose | +0.02 | **+0.12** | +0.01 |

Half the colony floating 9 to 12 cm over its own bedding with one knee resting on it. `ShoulderPerBody`
goes 0.152 → **0.109**, measured against the trunk — the band of the baked mesh between the spine and
the neck bones, which `SleepProbe` now reports beside the whole-mesh figure. The supine pair are
untouched because `Lift` weighs this term against the roll and they have none.

**The price is deliberate:** a drawn-up knee now presses about 0.10 m into a 0.30 m mattress. A limb
sunk a little into bedding is what bedding is for; a torso in mid-air is not.

**And the measurement is the lesson.** The whole-mesh number said 0.00 and 0.02 — a centimetre out,
apparently perfect — for the two postures that were 9 and 12 cm wrong. A summary statistic over a
whole body answers a question about the body's *extremities*, and the thing being judged was its
trunk. `docs/bug-patterns.md` P11 is the neighbouring failure: a number in range, measuring the
wrong thing, with nothing to say so.

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

### What the owner's second look changed (2026-09-18)

Two reports. Both turned out to be about **drawing**, and the second one was the more serious
mistake this line has made: a simulation that was right, reported as broken, because there was
no way to see it working.

**"I couldn't assign anyone with a bed."** Second time of asking. The row had a pointer cursor,
a hover brighten, a border and a chevron, and was still not found. The lesson, written down
because two rounds of it were paid for: **hover is not an affordance.** A control has to look
like one while the pointer is somewhere else entirely. The value now sits in a filled, accent
box with a **bed glyph** before it and a chevron after, and is set in the heavier `Row` type role
(weight is `HudType`'s, never the stylesheet's — `TheSheetSetsNoTypeAtAll` enforces that).

**"Colonists stand outside rather than getting into a spare bed."** *They do not.* Measured
before changing anything: a probe on the owner's own case — nobody owning anything, one spare
bed, one tired colonist — walked the colonist into the bed and slept there (`inBed=True`).
`TrySleep` picks the nearest reachable unowned bed and always did.

What was wrong is that **nothing in the whole of presentation knew a pawn could be asleep**, so
a colonist in a bed was drawn standing bolt upright in it, all night. `PawnView.Asleep` and
`SleepPose` are the fix — a computed lying pose in the idiom `WorkSwing`, `ClimbPose` and
`SwimPose` established, because no pack contains a sleep clip. It lies **in a bed and on the
floor** (the owner's own second ask), because a colonist who could not reach a bed lies down
where it is, which is `SleptOnGround` made visible.

**Four postures, not one and not ten.** From the owner's reference sheet: on the back, on the
back with the arms up, and two sides, one curled and one loose. Which one a colonist takes is
derived from its pawn id, so it is the same every night and after a load and costs no state —
the bargain the colonist palette already makes. One posture would read as a morgue; ten cannot be
reached honestly from a standing idle clip by rotating the root and pitching six bones.

**And a third fault fell out of measuring the first two: two of the five quality tiers did
nothing.** The base gain is 6 and the tiers are percentages, so the products are 4.8, 5.1, 6.0,
6.72, 7.5, 8.4 — and integer division flattened them to 4, 5, 6, **6**, 7, 8. **A Decent bed
recovered rest at exactly the rate of a Normal one**, which is to say the tier a colonist rolled
was worth nothing. The fractional part is now spent by a Bresenham step over the interval index,
which is derived from the tick and the pawn id — so it stays out of the save and out of the hash,
where a remainder field would not have. No golden moved: no colonist gets tired inside those
windows.

**What is not changed, and is the owner's call.** A plain bed recovers rest **1.25x** as fast as
the floor and an Epic one **1.75x**. Those follow from `groundRestEffectiveness = 80`, which §2's
decision 4 states as unchanged, so it has been left alone and pinned by a test that says the
figures out loud. If a night in a bed should feel more decisive than a quarter again, that one
integer in `Colonist.xml` is the lever.

### What the screenshots showed (owner, 2026-09-18)

Three photographs of a colonist asleep, and three faults — one of which turned out to be the
smaller half of itself.

**The head was adrift in the middle of the mattress.** The body was centred on the bed, and
**this bed is 4.6 m long against a colonist's 1.8 m** — two cells of a 2.5 m grid — so centring
left the head two thirds of a metre short of the pillow. `SleepPose.Place` takes the *head* point
now and works the feet out from it, and the point is `BedShape.HeadRestAlong`, derived from the
pillow's own centre so the two cannot drift apart. The head is the end that has to be exact; the
feet may fall where a body of that length puts them, because nothing is watching the foot end of a
bed this size.

**Side sleepers sank.** Rolled onto its side a body presents its **width** to the mattress rather
than its thickness, and width is half as much again — so a lift computed from thickness alone
buried the shoulder and the hip. `SleepPose.Lift` is the half-height of the body box turned
through the roll: the thickness flat on the back, the width full on the side, and what the
rotation gives in between. No fudge, so it is right for a posture nobody has drawn yet.

**Items stood up through the bed, and refusing the order was only half of it.** `needsClearCell`
on `BuildingDef` stops a bed being *ordered* onto a pile. It does nothing about a hauler carrying
a pile *onto* a bed afterwards, which is the same picture arriving the other way round — and that
is the likelier route, since a bed cell was empty and walkable and therefore a perfectly good
destination. A bed's cells are out of circulation for items entirely now
(`ColonyItems.BlockItemsAt`), **derived from the edifice list on load** exactly as support, the
region graph and a ladder's connector are, so it costs no save format and no hash bit.

**A rule found on the way, worth knowing.** `TrySleep` checks a bed's *reservation* before it
checks whose bed it is — so a hauler that had claimed a bed cell as a drop target locked the owner
out of their own bed and sent them to sleep on the ground. Measured, not supposed:
`AnOwnerSleepsInTheirOwnBedAndNobodyElseDoes` began failing for exactly that reason, and the
fixture's own `Unclaimed` helper records it. Beds cannot be claimed that way any more, but a bed
reserved by some other means still would be.

**`needsClearCell` is furniture's and not every building's**, and that was measured too: the
blanket rule failed twelve tests that build perfectly ordinary walls near a start the scenario
strews with wood. A wall fills its cell and a slab is laid at the boundary under it, so neither
shows what is lying there; a bed is broad, low and open, and does. Extending it to walls is one
line and is the owner's call.

### The second round of screenshots (owner, 2026-09-18)

**A log went through a finished bed, and the first item fix had a hole in the middle of it.**
Refusing the *order* on an occupied cell guards the start, and holding the cells once the bed
*stands* guards the end — and the whole of the build in between was unguarded. Order a bed on
clear ground, a colonist takes a while to raise it, a hauler puts a log down where it is going,
and the bed is built straight over it. **A site holds its cells from the moment it is ordered
now**, through the one place a site is written (`ConstructionGrid.Set`), and gives them back when
it is cancelled or replaced. The release runs first and reads the site as it is, because the
facing that says which second cell to give back is about to be overwritten.

**A colonist asleep beside its bed: not reproduced, and recorded as such.** Three colonists with
three beds all sleep in beds (`EveryColonistWithABedToThemselvesSleepsInOne`), and the head lands
inside the pillow's own box for all four facings with the feet still on the mattress
(`TheHeadRestLandsOnThePillowForEveryFacing`, `TheWholeSleeperFitsOnTheMattress`) — the
facing-dependent sign error the photograph suggested is not there, and the bed in question simply
faces the other way from its neighbours.

The likeliest remaining explanation is the simulation being right about a situation that looks
wrong: **`TrySleep` decides once.** A colonist who finds no free bed — the third still being
built, or all of them momentarily reserved by colonists walking to them — lies down where it
stands, and having fallen asleep it stays there all night even after a bed frees up. Whether that
should change is the owner's, and it is a real question rather than a bug: waking a colonist to
shuffle beds would read as twitchy, while a colonist ignoring its *own* empty bed all night would
not.

### Why Assign did nothing, three times (2026-09-18)

The owner reported being unable to give a bed to a colonist on 2026-09-17, again after the row was
restyled, and again after it was made a button with a glyph. **Two independent faults were stacked
on each other, and fixing either alone would still have looked broken.**

1. **The affordance lived for one frame.** `InspectModel.Refresh` clears `_bedUnderPane` every
   time; it was set inside `SetCellRows`, which returns early whenever nothing about the cell has
   changed. So it was true on the refresh that built the rows and false ever after, while the row
   went on reading "Assign…" over a control the shell had already disarmed. It is now set from the
   cell detail *before* the early return, which is where a fact about the held cell belongs.
2. **The picker would have opened in the corner.** A popover shown this frame has not been laid
   out, so its height is NaN — and NaN written to `style.bottom` is not ignored, it drops the
   element to (0, 0). Measured: the picker at the top-left of the screen against the row at
   y = 1095 that raised it. It is placed on `GeometryChangedEvent` now, and `PlacePopover` declines
   to write a position it cannot compute.

**Neither was findable by reading**, which is the part worth keeping. Both earlier attempts changed
things that were genuinely wrong — the row did not look like a control, and the picker did use the
command bar's geometry — and neither was the reason Assign did nothing. What found them was
`BedOwnerPickerTests`: a PlayMode fixture that sends a real `ClickEvent` to the real row and then
asserts on the world. It took four runs; two of those failures were the fixture's own and are
recorded in `lessons.md` as well.

**The standing note in `CLAUDE.md` that "nothing tests that a click reaches the game" is about the
input system**, whose presses a PlayMode test cannot fake. A UI Toolkit event is not subject to
that, and this path had been testable all along.

### A sleeper is a position, not a motion (owner, 2026-09-18)

*"When they are sleeping — they should be static and not animated. Still in that position."*
Three things, and only the first was the one asked for.

1. **The breath is gone.** `SleepPose` now has no clock and no phase at all, which is the
   structural half of the claim: a pose with nothing to drive it cannot drift however long it is
   held. It is the only pose in that folder that is a position rather than a cycle.
2. **The idle clip underneath is held on one frame.** This is the half that actually mattered — a
   sleeping colonist was still playing the standing idle beneath the lying pose and swaying on the
   mattress. `Graph.Evaluate(0f)` rather than skipping the evaluate: the pose is applied with
   `Pitch`, which multiplies onto the bone's current rotation and is safe *only* because the clip
   rewrites the base pose first. Skip the evaluate and the same pitches compound every frame and
   the figure winds itself into a spiral.
3. **A sleeper is no longer "planted".** Found while doing the other two, not reported: the
   footing IK solves each boot against the ground *below the figure*, and a sleeper's feet are on
   a mattress two thirds of a metre above it. Left in, it would haul both boots down to the boards
   and drop the hips after them — a colonist folded through its own bed. Faded rather than
   switched, exactly as the swimmer already was, so getting up hands the footing back continuously.

### The build overlay is the thing and nothing else (owner, 2026-09-18)

*"When we place walls, floors, furniture to build — lets not print the cursor, just the
shape/outline of what is going to be built because it's difficult to visualize anything and just
adds noise."* Asked back with the inventory, the answers were: **ghost only** while placing;
**ghost plus progress** on a placed order; **the ghost turns red** where it cannot be built; and
the order tools (mine, chop, deconstruct, cancel) **left alone**, because they paint on things
that already exist and have no thing to ghost.

What a cell carried before, and what it carries now:

| | Before | Now |
|---|---|---|
| Hovering | ghost, or a red cell box where refused | ghost, red where refused |
| Dragging a run | green box over the whole run **+** a ghost per cell | a ghost per cell |
| Placed order | cell outline **+** filled cell mark **+** ghost **+** progress | ghost **+** progress |

**The two asks are not in conflict, though they look it.** The span box was itself the answer to
an earlier report — that the cursor was invisible — and it was the right answer *then*, when
nothing promised what would be built. The ghosts are that promise now, and once they existed the
box was a second outline of the same run in a different colour, one cell bigger than the wall
inside it.

**Refusal went per cell**, which reverses a decision `NothingHereWillBeBuilt` had argued for in
its own remarks: that a run should go red only when not one cell of it would be built, because a
wall dragged over a meadow routinely crosses a tree and turning the whole thing red would repaint
something nobody complained about. Per-cell red does not repaint the legal part — the buildable
cells keep their material colour and only the refused ones go red — so the objection does not
apply, and the method is deleted.

**`DrawCellSpanBox` and `DrawCellSpanPlate` are kept although nothing calls them.** The geometry
was judged by the owner when it landed and is held by `BuildCursorTests`; it is one call away if
the ghosts alone read too sparse. Their remarks say so, so a later session does not delete them as
tidying — or wonder why they are there.

### 13. A bed claims its own sleeper (owner, 2026-09-19)

Two asks in one breath: *"when you have one selected the sub menu should be clear who is already
assigned a bed and who is unassigned a bed for clarity"*, and *"can you auto assign a bed if it's
been unoccupied or not claimed for a while so colonists find an empty bed to sleep in — instead on
the floor where possible"*.

**The second one was already half true, and the half that was missing is not the half it sounds
like.** `TrySleep` has always let anyone sleep in a bed nobody owns — an unowned bed is a shared
pool, and a colonist only ends up on the floor when there is no free bed within reach, not because
she failed to find one. So auto-assignment does not, on its own, get anyone off the floor. What it
buys is that **who sleeps where stops being redecided every night** by whoever happens to be
nearest, which is what makes the picker's marks worth reading and what makes "she sleeps there"
a fact the pane can state.

**The rule.** A colonist who *reaches* a bed nobody owns takes it as her own
(`ConstructionGrid.TryClaimForSleeper`, called from `SleepJobDriver` on arrival).

**The exception, which is the whole of the design.** Claiming takes a bed out of the shared pool
for good, so the claim happens only when the pool would still hold a bed for every colonist who
has none:

> `UnownedBedCount() - 1 >= (colonists with no bed, excluding this one)`

With a bed each, everybody claims on their first night and nothing is lost. **With two beds
between three colonists nobody ever claims**, the pair stay shared, and the third is not stranded
on the floor for ever because the first two got in early. Without that clause the feature would
have caused exactly the complaint it was asked to fix.

Three things it deliberately does not do:

- **Not on the collapse branch.** A body that goes down on the way to a bed has not reached it and
  does not get to own it (`SleepJobDriver.Claim` tests `Pawn.Cell == Job.TargetCell`).
- **Not every tick.** The rule counts beds and colonists; asked sixty times a second by a colony
  of fifty it would be the only thing in that driver that cost anything. It is asked once, on
  arrival.
- **Not on the scenario's own sleeping spots.** Those carry no edifice record, so `AssignOwnerAt`
  refuses them and `UnownedBedCount` does not see them — which is why the ten-day goldens are
  unchanged by this. They were never ownable and still are not.

The "unoccupied for a while" half of the ask — taking a bed *back* off an owner who has stopped
using it — is **not built**. With one bed per colonist enforced at assignment and no death model,
there is no way to reach a bed whose owner will never return, so a staleness timer would be a
mechanism with nothing to fire on. It goes in §12 rather than into the code.

### 13a. The picker says who is housed, without saying so

The picker was a list of bare names, and using it meant remembering who you had already given a bed
to. It now carries a mark column, 14 px, ahead of the name:

| Mark | Means |
|---|---|
| `✓` | sleeps in **this** bed |
| `•` | has a bed **somewhere else** — picking them moves them, and releases the old one |
| *(blank)* | **no bed at all** |

The owner's words were *"no status and just a tick next to their name and also indicate the others
already have a bed assigned"*, so there are no words in the column: a `has a bed` / `no bed` column
is three times the reading for the same fact, and at three colonists it is longer than the names it
annotates. **The blank is the row the eye is hunting for**, which is why the colonist with nowhere
to sleep is the one with nothing beside her name rather than the one with a badge. The words exist
in the tooltips for anyone who hovers.

The marks come from `ConstructionGrid.PawnOwnsABed` and `BedOwnerAt` — asked of the one owner of
the edifice list on each open, never tallied a second time in the shell, because a second tally is
a thing that goes stale and this popover is rebuilt on open precisely so nothing in it can.

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
- **Releasing a bed its owner has stopped using** — the other half of the 2026-09-19 ask. Wants a
  death or a departure model first; until one exists there is no state it could fire on (§13).
