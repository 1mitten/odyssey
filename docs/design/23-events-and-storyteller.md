# 23 — Events and the storyteller

Owner, 2026-09-20: *"A world event will happen and we should start with something that we can
invoke [from] the debug menu but there are factors to when a world event might occur — align it
with RimWorld … Regular / weekly / adhoc / condition based … periodic, one off or something else …
positive or negative (depending on the story teller — we may need to plan this out and seam this
off) … could offer a reward on completion but the event itself might be an award … displayed as
an alert (and in relevant activity logs etc). Let's start with a simple event system. A random
drop from the sky … a meal item drop … simply a case of hauling that item."*

The research behind this is `docs/research/a-11-storyteller-incidents.md`. The reasoning for each
decision below is in `docs/journal.md` under the same date.

## 1. Two layers, one built

The reference separates **what can happen** from **when it happens**, and so does this.

| Layer | What it holds | State on 2026-09-20 |
|---|---|---|
| **Incident** | a Def (what it is, its gates, its worker's parameters), a worker (`CanFireNow` / `TryExecute`), the ledger (what has happened), the skyfallers (what is in the air), the Events panel | **Built.** `Assets/Odyssey/Sim/Events/`, `Assets/Odyssey/Hud/BulletinModel.cs` |
| **Storyteller** | which incident fires when: generators with their own clocks, a favourability weighting, a points budget | **Not built.** Owner's decision: debug row only for now. The gates are on the Def and the ledger keeps the refire memory, so a scheduler reads and does not restructure |

The one door between them is `Incidents.TryFire(IncidentParms)`. The debug menu goes through it
today; a storyteller and a quest go through it tomorrow. An earned event and a forced one cannot
behave differently, because they are the same call.

## 2. The vocabulary, mapped

The owner's cadence words against what the reference actually does. Nothing in the reference fires
on a calendar date; that is the one mismatch, and it is deliberate on their side — an event the
player can schedule around is an event with no tension.

| Owner's term | What it becomes | Built? |
|---|---|---|
| Regular | a **mean-time-between** generator: "about every N days", sampled stochastically each storyteller tick | no |
| Periodic | an **on/off cycle** generator: on-days, off-days, one or two fires per on-phase, a minimum spacing. The threat pacer | no (M6) |
| Ad hoc | a **random-bag** generator with an anti-drought rule (N quiet days force the next) | no |
| Condition-based | two things: **gates** on every Def (earliest day, minimum refire, colonists) and **triggered** generators that fire on a player action | gates yes, unread; triggers no |
| One-off | a Def with `maxFires = 1` | the field, yes |
| Weekly | **not recommended.** A `scheduled` generator kind is the seam if the owner wants a festival; it is reserved, not built | no |
| Positive / negative | `favourability` on the Def: Good, Neutral, Bad. A storyteller weights by it; the panel colours by it; the chime picks by it | yes |
| Reward on completion | a **quest**: offer → accept → timer → completion → reward pool, wrapping incident workers. A different thing from an incident and deferred until incidents feel right | no |
| The event is the reward | an incident with `favourability = Good`. The supply drop | yes |

## 3. What fires today, and what does not

The debug menu's *Invoke event* row submits `IntentKind.InvokeIncident` with the supply drop's
index. The handler ignores every gate on the Def — earliest day, refire, weight, colonist count —
because a debug row exists to make the thing happen. It honours only the worker's own
`CanFireNow`, which for the drop asks whether any column on the board can take a landing; a board
that cannot answers `NotPermitted`, which the composition root reports.

Nothing else fires anything. The soak runs, the goldens and the headless days all run with the
events attached and none firing, which is why they still agree with themselves.

## 4. The Def

`Assets/Odyssey/Defs/Core/Events/Incidents.xml`, one Def. Read by `IncidentContent.FromDefs`,
which binds the item name to its `ItemIndex` and the worker name to its class at load, so a typo
in either is a load error with a file and a line.

| Field | Supply drop | Read by |
|---|---|---|
| `bulletinKey` | `ui.bulletin.supplydrop` | the Events panel, through `IncidentLabels`; the wiki, one day, through the generator |
| `favourability` | Good | the panel's ink and chime; a storyteller later |
| `category` | Misc | a storyteller later |
| `worker` | `SupplyDrop` | `IncidentWorkerRegistry`, at load |
| `earliestDay`, `minRefireDays`, `weight`, `minColonists`, `maxFires` | 1, 2, 100, 1, 0 | **nothing yet.** Pinned by the fingerprint so a scheduler reads what was written |
| `item`, `stackMin`, `stackMax` | `Item_Meal`, 10, 20 | the worker |
| `fallTicks` | 120 | the worker: two seconds at 60 ticks a second |

`IncidentContent.Order` is the handle order, as `WorldContent.TerrainOrder` is for terrain:
`IncidentHandle` in the contracts assembly and `IncidentLabels.Keys` in the interface repeat it,
and `IncidentContentTests` and `RegistryTests` hold the three to one length. The interface's key
table is held to the Defs' `bulletinKey` values by a test that reads the XML, on the bargain
`JobLabels.CarryingAspect` already makes: the interface cannot import the Def, so two spellings
of one list are tied together by a test rather than a shared file.

## 5. The ledger and the Events panel

**Alerts and bulletins stay separate** (`10-ui-panel-catalogue.md` A6). An alert is a condition
the panel re-derives from the frame four times a second and that clears itself; a bulletin is an
event, a fact once it has happened, that stays until the player dismisses it. The alert model
rebuilds its rows from scratch on every refresh, which is right for conditions and would wipe an
event on the next refresh — so the Events panel is a second model, `BulletinModel`, and not a
fourth key in the first.

**The ledger is the sim's memory.** `IncidentLedger` is append-only: `(id, tick, incident,
cell)`, hashed and saved. Ids count from one and are never reused. From the entries it derives,
and rebuilds on load, the last tick and the count each incident has fired — the storyteller's
refire gate, one lookup. The ledger publishes its newest sixteen entries each frame
(`BulletinView.PublishedTail`); the whole ledger is the History screen's business and arrives by
another channel when that panel exists.

**The edge is the id.** A one-tick flag would be missed by a panel refreshing at four hertz while
the world ticks at sixty. The model keeps the highest id it has seen and treats anything above it
as new, so a row can never be missed and never raised twice. The first refresh after a world
arrives *primes*: whatever the tail holds becomes history on the panel without announcing itself,
so loading a save does not chime a dozen old events. Dismissals are view state and are not saved;
a load shows the tail as history again.

**The row.** Icon by key, the incident's name in bold in the favourability's ink (Good green, Bad
red, Neutral accent), the stamp in dim ink — "Day 3 · 14h", the day as the clock counts it — and a
dismiss cross. Clicking the row jumps the camera over the event's column at the layer the player
is already looking at, and does nothing else. It used to move the slice to the event's layer and
select the cell as well, and on the first look the owner did not expect the depth to change
(2026-09-20): a jump is a way of getting there, and what is cut away or selected is left as they
had it. The cost is stated: a drop on a rooftop viewed from below the roof is found by the pad's
column, not by the slice moving for you. Six rows at most; older ones fall off the panel and
stay in the ledger. The panel is hidden when empty, exactly as the alerts panel
is, so a colony nothing has happened to pays nothing against the coverage ceiling.

**The chime rides the row**, as an alert's does: `AlertHappy` for a gift, `AlertNegative` for a
blow, `AlertNormal` otherwise, once per refresh however many arrived together. A clone with no
audio catalogue gets silence.

**The History screen is the next unit.** `ui.tab.archive` ("History", F9) is reserved in the
registry and `HudCommands`, B16 in the panel catalogue names it, and the ledger is its store. It
needs a paged or whole-ledger channel and a virtualised list; nothing in this unit anticipates its
shape beyond keeping every entry.

## 6. The skyfaller

**The flight is simulated; only the drawing is not.** `Skyfallers` holds what is in the air —
incident, item, stack, landing cell, launch tick, land tick — ticks every tick, and on the land
tick spawns the item through `NearestCellWithSpace` like every other arrival. The thing does not
exist until then: nothing can haul, eat, count or reserve it in flight, and a save taken mid-air
lands it on the tick an unsaved run would have (`SkyfallerRoundTripTests`). A load that finds no
room within three cells when it comes down is lost and counted; the cell had room at launch, so
this is the corner where a hauler set something down there during the six seconds.

**The landing rule** is `CellGrid.SkyLanding(x, z)`: walk down from the top of the world to the
first cell that is not open air — has a floor, is solid, holds an edifice, or is deep water —
and land there if it can be stood in, else refuse the column. A rooftop slab is met first and is
walkable, so a drop lands on the roof and never in the room under it. A wall's own cell, deep
water and bare rock are met first and are not walkable, so the column is refused rather than the
search slipping past them to the floor beside a wall's foot or the bed under a lake — the two
answers `NearestWalkableInColumn` would give from the top and this exists to refuse. A tree's cell
is met first and *is* walkable (a tree blocks nothing), so a drop lands under the tree where
felled wood already does.

**Anywhere on the board** (owner, 2026-09-20), uniform, which is the reference's own behaviour.
Up to sixty-four columns are drawn before the worker gives up. The consequence is stated rather
than softened: most drops land out of sight, the Events row's jump is how a player finds one, and
a drop the colony cannot reach lies where it fell — no haul job is ever generated for a cell no
stockpile can be reached from. If that proves maddening the knob is one line in
`SupplyDropWorker`: re-draw until the landing is hauler-reachable from the colony.

**A fact about the moment.** Both draws — the column and the stack — mix the tick in
(`IncidentPurpose.Landing`, `IncidentPurpose.Payload`), for the reason `DeconstructRefund` gives:
keyed on the seed alone every drop in a world would land on the same cell with the same stack.
Both salts are SHA-256 round constants, the sixth and seventh outside the spent xxHash family.

**The drawn half** is `FallArc`: the thing starts `FallArc.DropHeight` (120 m) above its
landing floor — above the play camera at any zoom, which sits 32–160 m up and looks down at 48°,
so a drop enters from beyond the top of the frame rather than popping into view part-way down —
and comes down at one speed, the way a crate under a chute does. On a world taller than 120 m
it starts 6 m above the top layer instead. The first cut started just above the top of the
world and fell in two seconds gathering speed like a stone, and the owner saw it land almost
before it had been seen falling (2026-09-20); the duration is now six seconds (`fallTicks` 360,
the Def's) and the height is presentation's, because nothing in the simulation cares how high
the drawing starts. `ChunkRenderer` draws it
into the same instanced batch as the pile it will join, at the frame's own alpha, so a paused
world holds it still. A flat pad is drawn on the landing cell for the whole flight so a player
who jumped to the event has something to look at; it is a cursor, not a thing, and the first
thing to drop if it reads as clutter. `AudioDirector` plays `odyssey.sound.drop.land` at the cell
on the first frame a thing that was in the air is not — a sound named and in no catalogue yet,
which the director declines silently until the owner adds the row.

## 7. Save, hash and the goldens

The ledger and the skyfallers are two new `ISaveable` sections, **appended** to
`ColonyWorld.SaveComponents` after the pawn seeds. Sections are length-prefixed, so a save from
before events has neither and loads with an empty ledger and nothing in the air; no format bump.
Both are hashed: history is state, and a flight that was not would be a load with the meals gone.

All six golden numbers moved, for the dullest reason there is: the hash sees four more integers
before the first tick, all zero. The control is the shape of the failure — all three `Generated`
values moved together, including the barren meadow's, which no gameplay change has ever touched
— and the paragraph is in `Golden.cs`.

## 8. Deferred, and where each attaches

| Deferred | Attaches at |
|---|---|
| A storyteller: generators (mean-time-between, cycle, random-bag, triggered; `scheduled` reserved) and a Def naming them | an `IWorldSystem` in the world phase that reads the Defs' gates and `IncidentLedger.LastFiredTick`, and calls `Incidents.TryFire`. **Designed 2026-09-26: `docs/design/58-storyteller.md`, plan `docs/plans/storyteller.md`** |
| A points budget, adaptation, population intent | **Superseded by design 58 §4:** `Points` stays a raid *size* (design 55 §9); the budget is colony strength × tension × difficulty inside `RaidBudget` |
| Conditions (timed, map-wide, no entities) | a second worker family; the ledger already records them |
| Quests (reward on completion) | a wrapper that calls incident workers; not an incident |
| The History screen (F9, B16) | reads the ledger; needs a paged channel and a virtualised list |
| A second event | the recipe below; the debug menu's Events tab lists it by existing |
| A pod that opens, debris to haul | a second skyfaller kind; the meals fall bare by owner choice |
| Landing reachability | one line in the worker, if "anywhere" proves maddening |

### Adding an incident: the recipe (reviewed 2026-09-20)

The PR was reviewed with one question: what does a raid or an encounter cost to add. The
answer is **five edits, all caught by the fast tier if one is missed**, and nothing in the
layer has to be restructured. In order:

1. **A worker** in `Assets/Odyssey/Sim/Events/`, a class deriving `IncidentWorker` with a
   public parameterless constructor and no state. It joins by existing: the registry scans the
   assembly, and `link.xml` preserves it in a stripped build. `Name` is what the Def spells.
   `CanFireNow` changes nothing and answers cheaply; `TryExecute` does it and records it with
   `ctx.Ledger.Record`. It reaches everything through `IncidentContext`: the world, the pawn
   context (registry, cells, items, navigation), the content, the ledger, the air.
   Override `Validate` to check the fields it reads and throw `DefLoadException` naming the
   Def; the loader checks only what every incident has (a key, a worker, an item the content
   carries), so the supply drop's stack range is the supply drop's business and a raid is not
   held to it. Draw randomness from `ctx.Random(purpose)` with a **new constant** in
   `IncidentPurpose` per draw — never reuse `Landing` or `Payload` — and mix the tick in the
   way `SupplyDropWorker` does, or every firing in a world lands the same way.
2. **A Def** in `Assets/Odyssey/Defs/Core/Events/Incidents.xml`: `defName`, `bulletinKey`,
   `favourability`, `category`, `worker`, the gates, and whichever worker parameters it
   reads. Fields it does not read stay at their defaults. `IncidentDef` is flat today; when a
   second worker wants parameters the first does not, the loader already reads nested objects
   and lists, so a per-worker block (`<raid>…</raid>`) is the shape to reach for rather than
   widening the flat set — that day, not before.
3. **One line in `IncidentContent.Order`**, appended, never inserted: the position is the
   index the ledger and every save carry. The content fingerprint in `IncidentContentTests`
   moves and is re-baked with the reason.
4. **One constant in `IncidentHandle`** (`Sim.Contracts`) and `Count` up one, and **one key in
   `IncidentLabels.Keys`** (`Hud`), both in the same position. Three spellings of one list is
   the project's standing bargain for anything the interface names without importing the
   simulation (`ItemLabels`, `JobLabels`, terrain), and `IncidentContentTests` plus
   `RegistryTests` fail the fast tier on any disagreement in length, order or key.
5. **A registry row** in `docs/design/icon-keys.csv` under `ui.bulletin.*`, then both rebuilds
   and both `--check`s. The Events panel, its ink, its chime and the debug tab's row all follow
   from the key and the Def; no presentation file learns the incident's name.

What a raid needs that the events layer does not provide, and should not: a hostile faction
and a hostility model, a pawn kind that is not a colonist, an arrival edge and a target, and
the combat and health it presupposes. Those are their own units; the incident is the thing
that asks for them at a moment. A visitor or a trader encounter is the same shape with a
neutral faction. A **condition** (a cold snap, a fallout) is the one kind this layer does not
yet represent: the ledger records a firing, not a span, so a condition wants a second
record — active, until tick — beside the skyfallers, and that is the first structural
addition the next kind of event will ask for (§8).

Two seams were left deliberately narrow and are noted so nobody mistakes them for the design:
`IntentKind.InvokeIncident` carries the Def index only, although `IncidentParms` already
takes a cell and a points budget, so the debug tab cannot yet force a landing or a size; and
a ledger entry is `(id, tick, def, cell)`, so a raid that wants to record its points or its
outcome, or a condition its end, adds fields and bumps the save format. Both are one-line
widenings when a caller exists.

**The raid was the caller (2026-09-25, design 55 §9).** It followed the recipe as written, with
two widenings. `InvokeIncident` now carries a size in B and a raid mix in C (0 in either is the
incident's own choice, so every existing row is unchanged). And `IncidentDef` gained its first
per-worker block, `<raid>…</raid>`, the shape this section recommended. `IncidentParms.Points`
is read for the first time, as the band's size. The ledger entry did **not** widen: the band's
size is carried in the entry's `amount`, which the detail section already saved.

## 9. Invited tuning and open questions

**The first look was 2026-09-20**, the day it was built, and moved four things. The fall was too
quick to be seen (two seconds from just above the world, gathering speed) and is now six seconds
at one speed from 120 m, above the camera at any zoom. The Events row's jump moved the slice and
the selection as well as the camera, and the owner did not expect the depth to change; it moves
the camera only now. The debug menu grew a second tab, *Events*, one row per incident Def, built
from the open colony's content so a second Def appears by existing; the *Invoke event* row and
its key are gone. And the chime's end read as the sound snapping to silence and the music
switching back on: the music now swells back over a second instead of the duck's 0.15 s attack,
and the last 0.4 s of an alert voice fades rather than stopping dead (`AudioDirector`,
`docs/design/24-alert-sounds.md`). The reasoning is in `docs/journal.md` under the date.

What remains chosen rather than measured:

- **Six seconds** in the air, from 120 m at one speed. Seen now; whether it is waited for is the
  next question, and whether one speed reads as a chute or as a lift.
- **Ten to twenty meals**: a little under to a full stack, so a drop is one haul.
- **The pad** on the landing cell: a cursor drawn in the stand-in material. Helps or clutters.
- **"Anywhere"**: whether finding a drop through the Events row is a pleasure or a chore, now
  that the row no longer changes the slice for you.
- **The chime**: `AlertHappy` for a gift; whether it reads as good news or as an alarm, and
  whether a second's swell back is the room settling or the music being slow.
- **Six rows** on the panel; whether the panel wants to be shorter, or to collapse to a count.

## 10. By-hand test procedure

1. Press backtick, click the *Events* tab, click *Supply drop*, unpause. An Events panel
   appears under the alerts (or under the clock) with one row, "Supply drop · Day 1 · 12h" in
   green, and the happy chime sounds once; the music dips under it and swells back over about a
   second rather than switching on.
2. Click the row. The camera jumps to the column, the slice stays where it was and nothing is
   selected. Look for a flat pad; the pack comes in from above the top of the frame and takes
   six seconds at one speed to reach it, and the pad goes when it lands.
3. Wait. A colonist walks out, picks the pack up and carries it to the stockpile. If nobody
   comes, the landing is somewhere no stockpile can be reached from — the "anywhere" case.
4. Click *Supply drop* while paused. Nothing happens until you unpause, and then everything
   above does; the tooltip says so.
5. Invoke, then save within the six seconds. Load. The pack still comes down, on the same cell.
6. Dismiss the row with its cross; invoke again; the new row arrives and the old does not return.
   Load a save with events in it: the rows are there, and nothing chimes.

## 11. Nothing lands in a tree — 2026-09-25

Owner: *"Drops and spawned items shouldn't land exactly at a tree's trunk — around it or the next
tile."*

**The comment was wrong.** `CellGrid.SkyLanding` said a tree's cell is met first and is not
walkable, so a column over a tree is refused. A tree blocks nothing: the fall reaches the ground
at its foot and the column answers the tree's own cell, and the space test that followed had no
opinion about trees either. The comment is corrected; the grid still cannot tell a tree from a wall
by its handle, so the rule does not live there.

**It lives in `ColonyItems.CellHasSpace`, both overloads**, which refuses a cell a tree stands in
(`ColonyItems.TreeAt`, the grid of standing orders again, wired by `PawnContext.Designations`).
Every road by which a thing comes to rest asks it — most of them through `NearestCellWithSpace`,
whose ring search then finds the tile beside the trunk:

| Road | What it does now at a trunk |
|---|---|
| Supply drop, column drawn at random | the column is refused and another drawn |
| Supply drop, column forced (a cell named) | the nearest column round it within two rings (`SupplyDropWorker.BesideTreeRings`); a wall, deep water or rock still refuses as before |
| Skyfaller landing (something grew there during the fall) | beside it |
| Debug *Give resource*, debug *Arm colonists* | beside it |
| Felled wood | at the stump — the tree is gone — else past the neighbouring trunks |
| Deconstruct and power refunds, mined blocks, harvests, dropped loads, a haul set down on open ground | beside it |
| A load falling through a hole (design 26-falling-items §4) | beside it |
| A stockpile cell a tree stands in | not a destination |
| Mushrooms beside a tree, loose stones at generation, the wreckage scatter | not in a trunk |

**Live, not cached.** `ColonyItems` keeps a set of cells nothing may be put in (a bed's), but a tree
comes down by more roads than a bed — felling, a site cleared, a collapse — and a cached copy would
have to hear about every one. The live question is one handle read and one Def read.

**Tests** — `TreeTrunkTests`: the space test and its ring search, a debug grant, a forced drop and
its open-ground control, forty drops drawn over a board three-quarters forest, felled wood with a
stone on the stump and trees all round, a load falling onto a tree, and the played board as
generated (nothing the world starts with lies in a tree). With `TreeAt` switched off every item
test fails.

**Goldens.** `Generated` moved on no board: nothing the scenario or the generator places was ever
in a tree. The `Simulated` moves on the played board and the city are the wander's (design 31 §20).
