# 31 — Animals: the creature seam, and the first pig

**Status: ground and interview, 2026-09-21. Nothing built.** The owner asked for a plan that gets
a pig — their own model, to be animated — generated into the world and moving around, harmless even
when attacked, clickable like a colonist with its food and rest readable, with the interface to
come later and the emphasis on *how the system works and what it costs*. Hunting, attacking,
taming and husbandry are explicitly for a later conversation. This document is §1 what exists,
§2 the reference model, §3 the decision and the alternatives, §4 the unit ladder to that MVP, and
§5 the questions — **the phase stops at §5 and nothing in §4 starts until they are answered.**

The name in `docs/design/proper-nouns.csv` is the **midden hog** (`creature.scavenger`,
*proposed*): pig-descended, thrives on refuse heaps, tameable, "the one you meet first". This
document uses *pig* until §5 Q1 settles it.

## 1. What exists today (grounded against the code, not the status files)

Three things the plan cannot ignore, and one that makes it cheaper than it looks.

### 1a. A pawn is a heap object with virtual decision points, not a table

`Pawn` (`Sim/Pawns/Pawn.cs`) is a plain class in an id-ordered `List` on `PawnRegistry`; every
field is an `int`; every behavioural choice is a **virtual method** — `NeedFallPerInterval`,
`RestGainPerInterval`, `MoodDriftPerInterval`, `CanMentalBreak`, `WorkRatePerMille`,
`MoveRatePerMille`, `InnatePacePerMille`, `ConditionPerMille`, `Mode` (the `TraverseMode`),
`WillWork`, `WorkPriority`, `GainExperience`, `RollPassions`, `RollStartingSkills`, `AddMemory`.
`PawnRegistry.Adopt` is documented as *"the seam a mod would use to add a pawn kind"* and nothing
has ever gone through it. `ContributeTo(ref StateHash)` names the exact hashed set.

What is **not** there: any notion of kind, species or faction on a pawn (`PawnContent.Kind` is one
shared `PawnKindDef`, `PawnKind_Colonist`, and every pawn points at it); any way to **remove** a
pawn (no despawn, no death path); a per-record kind in the save; and `BuildDrivers()` is a
hard-coded twelve-entry array indexed by `JobDef.driver`.

### 1b. The navigation already knows what an animal is

`TraverseMode.Animal = 2` has existed since the pathfinder was written (`Sim/Pathing/NavGrid.cs`).
`NavGraph` builds a district table for **every** mode, `PathFinder` filters links by mode mask,
`NavGrid.CanEnter` refuses a closed door to an animal, and `Connector` refuses a ladder to
"an animal's lack of hands". `PathingTests`, `DoorMovementTests` and `LadderTests` already assert
all three. So `Reachable(pawn, cell)` for a pig is the same two array reads it is for a colonist,
and no graph work is owed. Terrace steps and one-block hops are ordinary walking and a pig takes
them.

### 1c. Needs, jobs, movement and publishing are all per-pawn and reusable

- `NeedsSystem` ticks every pawn on the 150-tick cadence, phase-spread by id, reading the Def bands
  and the pawn's virtual rates. Food, rest and joy are one `int[3]`.
- `JobSystem` runs **one** think tree (`MentalState → CriticalNeeds → Work → Idle`) for every pawn;
  `IdleThinkNode` already wanders (`WanderTarget.Fill`, radius 6, reachability-gated, seeded from
  `PawnPurpose.Wander ^ id`) and the `WanderJobDriver` and `WaitJobDriver` exist.
- `MovementSystem` and `PathService` are keyed by agent id and know nothing about who is walking.
- `ReservationManager`, `PawnContext.Reachable`, `IsCellOccupiedByStandingPawn` all iterate the one
  registry — so a pig in that list is automatically avoided, reserved against and pathed around.
- `PawnRegistry.Contribute` publishes a `PawnView` (id, cell, food, rest, mood, job, movement,
  gesture, asleep) plus **39 aspects per pawn** that assume skills, work priorities, a schedule
  and rates. `Sim.Contracts` has no pawn kind.

### 1d. Presentation keys everything off `snapshot.Pawns`, and three passes assume a person

- **The figure pipeline is rig-agnostic where it matters.** `Create` builds a `PlayableGraph` from
  a row's gait clips with no `AnimatorController`; `GaitBlend` takes a list of drawn speeds;
  `FigureBuild` measures the baked mesh, not bone names; `PawnPose.Of`, `StepPace` and the ground
  clamp are cell arithmetic. A pig row is one more `rows.Add` with its own prefab and clips.
- **Three passes assume a humanoid** and degrade silently rather than fail: `BindWorkBones` binds
  sixteen `HumanBodyBones` (a generic rig answers null to all of them, and `Pitch` no-ops on null),
  every additive pose in `PawnFigureDirector.Poses.cs` (carry, swim, sleep, gesture, work, footing,
  gaze), and `CheckSocialGreetings`, which would have pigs greeting colonists. `ClimbPose.cs:70` and
  `SwimPose.cs:120` both say the bounding motion "belongs to an animal", so the authors expected
  this.
- **Selection is a ray against a fixed per-pawn box**, `colonistCursor = (1.15, 2.7, 1.15)`, with
  the feet taken off the live figure (`SelectionPresenter.PawnUnderRay`), and the white bracket is
  drawn in that same box. A pig in a 2.7 m box is wrong to look at and wrong to click. The pointer
  itself does nothing over a pawn (`CursorDirector.Decide` reads only "over interface" and "tool
  armed").
- **`InspectSubject { None, Colonist, Item, Cell }`** is the selection-kind switch; the storage
  pane already shows a second body sharing the `Cell` subject, so the pattern exists. A9's contract
  line in `10-ui-panel-catalogue.md` reads *"a body that varies by selection class"*.
- **The roster appends every pawn in the snapshot** (`RosterModel.Refresh`), so an animal published
  as a pawn appears as a colonist card with no further change.
- **The registry already has the names**: `ui.pawn.animal`, `ui.tab.animals`, `ui.skill.animals`,
  `ui.work.handling`, `ui.work.hunting`, `ui.arch.tool.kennel`, `ui.command.tame/train/slaughter/
  release/hunt`, `ui.bulletin.animaljoin/birth`, and B5 Animals / B6 Wildlife are catalogued panels.

### 1e. The cost that is already known

`PawnPose.Of` scans **every other pawn in the snapshot** for the crowd sidestep, once per posed
pawn, once per frame: **13.3 ms of a 22.5 ms frame at 384 pawns** against 0.02 ms at 64
(`FrameTimeTests.TheFrameAgainstColonySize`, `06-rendering-and-camera.md` §6c.2, `25-pawn-steering.md`
"What the crowd scan costs"). It is charged to `FrameSection.Actors` and it is quadratic in the
snapshot's pawn count. The brief's scale target is **50 colonists and 300 animals**. Three hundred
pigs in `snapshot.Pawns` puts the frame straight into that knee, and makes every *colonist's*
sidestep scan seven times longer as well. **The fix is already designed and exact** (§6c.2: the
skipped pairs contribute zero because `CrowdFarRadius` 3.0 m is under two cells), and it is the one
unit here that is a prerequisite rather than a feature.

The tick side is healthier: 50 colonists at the scale target cost 0.025 ms a tick at rest and 0.438
under the replan rate (OQ-19), and nothing an idle animal does is dearer than a wandering colonist.

## 2. The reference model (RimWorld, studied for shape, clean room)

What is recorded here is the *mechanics and data shape* as understood from play and public
documentation, to be confirmed by a Phase 3 research lane (`a-09-animals.md`, not yet written).
Confidence **medium** until then; nothing below is copied.

- **An animal is a pawn of a non-human race.** The species record (on the thing definition, a
  "race properties" block) carries: body size, base hunger rate, diet class (herbivore, omnivore,
  carnivore, and what counts as food), wildness (0–1, drives tame chance and tameness decay),
  herd flag, predator flag, trainability tier, pack-animal flag, nuzzle interval, "turns
  hostile when hurt" chance, "turns hostile when taming fails" chance, life stages with ages,
  gestation, life expectancy, meat and leather yields, and per-biome commonality. The **kind**
  record layers on the race: label, combat power, and what it spawns with.
- **Needs**: food and rest only. Herbivores graze plants (grass counts, and crops from about
  two-thirds maturity — a genuine gameplay consequence players fence against); predators hunt
  prey when hungry, including colonists if nothing smaller is about. Animals sleep where they
  stand or in an animal bed.
- **Behaviour** is the same think-tree architecture as a colonist with a different tree: a
  constant tree (flee when hurt; hostile state) and a main tree of *get food → get rest →
  predator hunt → wander near the herd or anywhere → idle*. Wild animals have no faction; tame
  ones join the player's. Wild animals occasionally **leave the map**, and are periodically
  **topped up** at the map edge to a per-map density (a biome number times the map area) with a
  kind chosen by commonality and a group size from the kind.
- **Interaction**: click to inspect (needs, health, training); a **hunt** designation queues a
  colonist with a ranged weapon; a **tame** designation queues a handler with food, rolled against
  wildness and the Animals skill; damage may flip the animal hostile.
- **Layer awareness**: none of it is layer-specific beyond pathing, which is exactly the
  finding `a-01-pawns.md` made for colonists.

The two things worth taking wholesale are the *one pawn class, one think-tree architecture,
different tree* shape — which is what the code in §1 was built for — and the *density with top-up
and departure* population model, which is what keeps three hundred animals a target rather than a
leak. The thing to take with care is grazing on crops: it is the single largest way an animal
touches the colony's economy, and it is a question (§5 Q5), not a default.

## 3. The decision, and what was rejected

### 3a. Recommended: an animal is a `Pawn` subclass with a species Def, in the one registry

`class Animal : Pawn`, adopted through `PawnRegistry.Adopt`, carrying an `AnimalKindDef` (the
species) beside the shared `PawnContent`, overriding the virtual points that differ: `Mode` is
`TraverseMode.Animal`; `WillWork` is false; `CanMentalBreak` is false; need rates read the species;
`InnatePacePerMille` and `MoveRatePerMille` read the species; skills, passions, priorities and
schedule are never rolled and never published. **The think tree becomes a property of the pawn**
(`Pawn.ThinkTree`, virtual, colonist tree by default) so `JobSystem.Think` asks the pawn rather
than owning one tree — one line in `Think`, and the fix for the only real branch in the job
system. **A pawn kind reaches the contract as one field on `PawnView`** (`Kind`: colonist or
animal), and the species as an aspect the feature mints (`odyssey.pawn.species`).

Why this and not the others:

- **Everything in §1c is inherited for free**: reachability, pathing, movement, reservations,
  occupancy, the needs cadence, the wander driver, the snapshot, the selection ray, the figure
  director and the instanced stand-ins beyond the cap. The project's most-repeated fault is *one
  rule with two owners* (`docs/bug-patterns.md`); a second registry is a second owner of every
  one of those rules.
- **The seam was designed for it** (`Adopt`), and the virtual surface is the project's stated
  Harmony-style patch surface — a subclass is the use it was built for.
- **The kind on the view rather than as an aspect**, against the ADR 0004 amendment that says a
  feature mints aspects and `Sim.Contracts` never changes: the roster, the figure director, the
  actor renderer, the selection ray and the box select all read every pawn every frame, and a
  per-pawn aspect lookup is a linear scan by design. One `byte` on `PawnView` is read by six
  per-frame passes; an aspect would be six scans of 40 rows × 350 pawns. The species, read only
  by the inspect pane on selection, stays an aspect. Record this as ADR 0004 amendment 3.
- **Publishing**: `PawnRegistry.Contribute` asks `pawn.PublishesSkills` (or the kind) before the
  39 colonist aspects, so an animal costs one `PawnView` and a handful of rows. This is also where
  HT5(b)'s "five per-figure aspect scans" want to be one walk; it is not this unit's job to do that,
  but it must not make it worse.

### 3b. Rejected: a separate `AnimalRegistry`, `AnimalSystem` and `AnimalView` channel

Cleanest on paper — its own save section, its own contributor, no colonist code touched — and it
forfeits `GotoCell`, `MovementSystem`, `PathService` agent ids, `ReservationManager`,
`IsCellOccupiedByStandingPawn`, `SelectionPresenter`, the figure pool and the actor renderer, each
of which would be duplicated or generalised off `Pawn`. That is the mining line's "six shared files"
again, on purpose. It also leaves colonists walking through pigs, because occupancy reads one list.

### 3c. Rejected: an animal is a `Thing` (an item that moves)

`ColonyItems` has no movement, no needs and no agent id, and an item that walks is a third kind of
mover. Nothing in the reference does this either.

### 3d. Rejected for now: a kind enum in `PawnContent` with branching, no subclass

Every `if (kind == Animal)` inside `Pawn`, `NeedsSystem` and `JobSystem` is a switch that grows a
branch per species trait — the shape `OQ-44` removed from the giver list. The virtual points exist;
use them.

### 3e. Decisions that follow, each with its reason

| Decision | Choice | Why |
|---|---|---|
| Where the species lives | `AnimalKindDef` in `Defs/Core/Animals/MiddenHog.xml`, loaded through `PawnContent` beside `PawnKindDef` | Content is written once; the fingerprint moves once, deliberately |
| Needs | food and rest; joy absent (`HasNeed(index)` virtual, the array stays `[3]` so `NeedIndex` and the save record do not move) | the reference's set; the HUD asks `HasNeed` before drawing a bar |
| Mood | present in the record, never drifted, never published for an animal | removing it moves `NeedIndex`, the save and the hash for no player-visible gain |
| Save | format **8 → 9**: a kind byte **first** in each pawn record, guarded by `FormatVersion >= 9`, so `Load` constructs the right class; species by def name | the record is constructed before its fields are read, so the kind cannot ride at the end the way `StarvationSeverity` did; a separate section cannot retype an object already built |
| Hash | animals hash like any pawn; goldens re-bake **once** with the control that a map with wildlife density 0 is byte-identical to today | determinism is the hash's business and a pig ticks |
| Despawn | `PawnRegistry.Remove(PawnId)` is built here, because a herd that leaves the map is the first thing that ever removes a pawn; it releases reservations, ends the job, drops the figure, and the id is never reused | the reference lets wild animals leave, and it is also the death path M6 will need |
| Population | a `WildlifeDef` per map recipe: density per 10,000 surface cells, a species table with commonality and group size; spawned by a mapgen pass at generation and topped up by `WildlifeSystem` on `TickGroup.Long` (2,000 ticks) at the map edge; a herd rolls to leave on the same cadence | the reference's model; a top-up with departure holds the count near the number rather than growing to it |
| Grazing (MVP) | a herbivore eats **grass terrain** where it stands, the world unchanged; crops are §5 Q5 | grazing crops is an economy decision the owner has not made |
| Harmless | no health, no damage, no flee at MVP; `AnimalKindDef` reserves the "hostile when hurt" and flee fields as documented hooks | owner: harmless even when attacked; there is no health model to be hurt in |
| Herd | a spawn group shares a herd id; `WanderNearHerd` targets a cell within the species' wander radius of the herd's centroid, reachability-gated; a lone animal wanders anywhere on its layer | the reference; keeps a herd reading as a herd rather than dispersing across the board in an hour |
| Traverse | `TraverseMode.Animal`: no doors, no ladders; steps, hops, marsh, shallow water as a colonist | already built and tested |
| Figures | the **same** `PawnFigureDirector` and the one 64 cap, a look table per kind, the humanoid passes gated on an explicit `Figure.IsHumanoid` set at `Create` from whether `BindWorkBones` bound a hip, never on null bones alone | one cap is one frame budget; the instanced actor fallback already draws the 300th pig |
| Selection | a per-kind cursor box (`AnimalKindDef.cursor` in metres, or measured off `FigureBuild` at catalogue time); `InspectSubject.Animal`; a pig never clears a colonist selection tier on the way past | the fixed 2.7 m box is a colonist number |
| Roster | animals are not roster cards (§5 Q4) | wild animals are not the colony's |
| Portrait | one per species, the flat `ui.pawn.animal` glyph as fallback | the studio's cache is keyed on appearance, and a species is one appearance |
| Sound | none at MVP | there are no footsteps for anyone; nothing to wire |

## 4. The ladder to the MVP

**The MVP**: on a new meadow, a small herd of pigs is on the board at the surface, grazes, sleeps,
wanders as a herd, and is drawn with the owner's model walking and idling; you click one and the
inspect pane says what it is and how hungry and tired it is; three hundred of them at the scale
target are measured on the tick and the frame and both numbers are written down; a save and load
brings them back on the same hash. No interface beyond the inspect pane; no hunting, taming or
health.

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **AN1 The kind seam** | M | — | `Animal : Pawn`, `AnimalKindDef`, `Pawn.ThinkTree`, `HasNeed`, `PawnView.Kind` + `odyssey.pawn.species`, save format 9, `PawnRegistry.Remove`. **Nothing spawns one** in any shipped scenario, so every golden is **identical** — that is the done criterion, with the control that adopting one pig moves the hash and survives a round trip. `PawnContentDefTests.ContentFingerprint` moves once with its reason line. `WorldSystemTests`-style tests for the tree-per-pawn dispatch. |
| **AN2 What a pig does** | M | AN1 | The animal think tree: `AnimalCriticalNeeds` (graze on grass terrain, sleep on the ground at the species' rest rate) → `WanderNearHerd` → `Wait`. A herd of five survives ten headless days on the meadow with no error and no starvation; a herd on a bare rock board **does** starve, which is the negative control that grazing is real. Wander is reachability-gated and never targets water, a site or a bank cell. |
| **AN3 Population** | M | AN2 | `WildlifeDef` on the map recipe (meadow: a small density; barren and city: 0), the mapgen pass, `WildlifeSystem` top-up and departure on `TickGroup.Long`, `Remove` exercised by a departing herd. Debug menu: *Spawn a herd here* and *Spawn one pig here*. Goldens re-baked **once**, with the density-0 control. `TickBenchmarkTests` gains an arm: 50 colonists **and 300 animals** at the scale target, at rest and under edits, printed beside the existing arms. |
| **AN4 The crowd scan is exact** | S | — | The §6c.2 fix in `PawnPose.Of`: a spatial bucket or a per-cell occupancy so a pawn only compares against pawns within `CrowdFarRadius`; `FrameTimeTests.TheFrameAgainstColonySize` re-run and the 13.3 ms at 384 becomes flat. **Independent of AN1–AN3 and can go first**; it is a prerequisite for AN3's density and a measured win on its own. The judged sidestep is not re-opened. |
| **AN5 The pig on screen** | M | AN1, the model | A `ModuleIds.Animal("middenhog")` family with the owner's prefab and its idle and walk clips, calibrated by `MeasureGaitSpeed` like a colonist's; `Figure.IsHumanoid` gating; `RenderActors` draws the instanced stand-in beyond the cap; `FigureCapTests` covers a mixed colony. A probe shoots a contact sheet of the pig standing, walking, on a step and in shallow water. `FrameTimeTests` gains an arm with 300 animals, charged to `Actors` and `Figures`. **Blocked on the model arriving** in a committable place (§5 Q2). |
| **AN6 Click a pig** | S | AN1, AN5 | The per-kind cursor box, `InspectSubject.Animal` (title from the species, food and rest bars, no mood, no skills, no tabs that lie), the box select includes animals, the roster excludes them, the see-through sight line works for a selected pig. `InspectModelTests` for the animal body; `SelectionCursorTests` for the box. |
| **AN7 The names** | S | AN1 | `creature.scavenger` moves from *proposed* to live in `proper-nouns.csv`; `ui.pawn.animal` gains whatever the pane needs; both `--check` gates and the icon gate green; wiki and registry rebuilt in the same commit. |

**Order**: AN4 first or in parallel (it shares no files with AN1); then AN1 → AN2 → AN3 on one
branch or three small PRs; AN5 waits on the model and can proceed against a placeholder capsule
row while it does; AN6 and AN7 close it. **The gate**: both tiers green; the ten-day headless run
with a herd on it; the two benchmark arms with their numbers written into this document; the
owner has pressed Play against the handover table.

**Not in the MVP, and where each is waiting**: health and hunting (M6, and the "hostile when hurt"
hooks on the species Def); taming, handling and the Animals skill (`15-skills.md` §6, `ui.skill.animals`
is already a key); breeding, life stages and sexes (fields on the species Def, unread); grazing on
crops (§5 Q5); the Animals tab (B5), the Wildlife panel (B6), and the animal area (`ui.arch.tool.kennel`);
predators (`creature.predator`, the girder cat) and feral synths (`creature.synth`), which are the
second and third species and the reason the tree is per pawn.

**What it costs the rest of the project.** The playtest queue is at 57 open rows against a rule of
about ten, and `docs/process.md` §4 says the next session takes a fix or a measurement rather than a
feature past that point. The owner has asked for this one by name, which is their call; AN4 is a
measurement and a fix, and AN1 moves nothing a player sees, so the first two units add no rows.

## 5. The questions (the interview — the phase stops here)

Each carries the assumption the plan is written on, so a silence is a decision too.

1. **The name.** `proper-nouns.csv` already proposes **midden hog** for exactly this animal. Is the
   pig the midden hog, or a plain pig with the hog for later? *Assumption: it is the midden hog; the
   row goes live in AN7.*
2. **The model.** What is it — FBX with its own rig and idle/walk clips, or a mesh with no
   animation yet? Is it yours to commit (then it goes under `Assets/Art/Custom/`, and CI can draw it)
   or licensed like Synty (then it goes under a gitignored folder and every pig test ignores itself
   on the runner, as the colonist ones do)? Is it configured as a Generic rig? *Assumption: your
   own, committable, Generic rig, clips to come; AN5 builds against a capsule row until it lands.*
3. **Where pigs come from.** On a new meadow from the map recipe at a density, topped up and leaving
   at the edge as the reference does — or only from the debug menu for now, the way events are?
   *Assumption: both; density is a number on the map recipe, small on the meadow and 0 on the
   barren board and the city, so the goldens re-bake once.*
4. **The roster.** Should wild animals be cards in the top bar? *Assumption: no; they are not the
   colony's. A tame animal is B5's business later.*
5. **What a pig eats.** (a) grass terrain, the world unchanged; (b) grass **and crops** from a
   growing zone, which is the reference's behaviour and the reason players build fences; (c) nothing
   real, the bar simply refills near grass. *Assumption: (a), with (b) recorded as the hook, because
   (b) is the first way an animal costs the colony something and that is worth deciding on purpose.*
6. **Herds.** A pig arrives in a group and stays near it, or each wanders alone? *Assumption: herds
   of three to six, wandering within a radius of the herd, as the reference.*
7. **Where they may go.** Surface only, or anywhere walkable — a cavern through an open cut, a
   roofless ruin through a doorless doorway, a terrace step? Doors and ladders are already refused.
   *Assumption: anywhere walkable for `TraverseMode.Animal`; spawning is on surface grass only.*
8. **Selecting.** Does a drag box pick pigs and colonists together into one mixed selection, or
   colonists only when both are inside it? *Assumption: mixed, primary first, as the reference; the
   inspect pane shows the primary.*
9. **When a colonist walks up to it.** Ignore, or move away? Harmless is decided; a wild animal in
   the reference ignores you until hurt. *Assumption: ignore; flee is a reserved node for M6.*
10. **The gate.** Is the MVP measured at the brief's number — 300 animals with 50 colonists at
    250 × 250 × 40 on the tick and the frame — before it merges, with AN4 landing first?
    *Assumption: yes; AN4 is a prerequisite, not a follow-up.*
11. **Out of scope, confirmed?** Sex, age, breeding, life stages, meat and leather yields, wildness
    and tame chance all become **fields on the species Def that nothing reads** at MVP, so the shape
    is not invented under pressure later. *Assumption: yes.*

**Blocked on the owner**: all seven units, by the phase rule. AN4 is the one unit that could start
on an assumed answer with no risk, because it is a measured fix to an open finding and has no
animal in it; it still waits, because the rule is the rule.
