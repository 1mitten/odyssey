# 33 — Combat: draft, move, melee

**Status: C1 (draft and move) built 2026-09-23; C2–C7 designed, not built.** The line's plan and
its unit status are `docs/plans/combat.md`. Research: `docs/research/a-10-melee-combat.md` (the
reference's rules, clean room) and `docs/research/synty-sword-combat.md` (the animation pack).
Branch `claude/combat-mvp`, worktree `D:\code\odyssey-combat`, based on `claude/wildlife`
(PR #169, which contains the animals of PR #167) because animals are targets.

## 1. What is being built

The owner's MVP (2026-09-23): *"a colonist can be put into an attack mode, ordered to move to
locations / attack something"* — animals, colonists, buildings and enemies — with melee weapons
from the Synty packs, **starting small and making sure it works great**.

Every decision below was the owner's in the interview of 2026-09-23 unless it says otherwise.

| Topic | Decision |
|---|---|
| Targets | animals (hog, rat), colonists, buildings (walls, doors, furniture), a debug-spawned hostile person |
| Health | one hit-point pool per pawn, by species (person 100, hog 60, rat 15); **downed at 0**, **dead at −50 %** of the pool; no bleeding, no body parts |
| Downed | a colonist heals **only in a bed** and has to be rescued to one; an animal recovers on its own; a marauder stays down until killed; a downed pawn keeps its weapon |
| Rescue | an automatic emergency job (the `ui.work.rescue` column), **and** a drafted colonist ordered to by a right-click; to the patient's own bed, else the nearest free one |
| Death | the corpse stays where it fell, drawn lying in the death pose, clickable as "Corpse of X"; not haulable yet; a dead colonist leaves the roster |
| Retaliation | a hostile always fights; an animal rolls its species' revenge chance on every hit (a hog usually turns, a rat usually runs); a colonist struck by a colonist fights back |
| Friendly fire | the victim remembers being attacked (−8, one day); any colonist's death is felt by every colonist (−6, three days); no opinions yet |
| Drafting | the reference's rules: work stops, the colonist holds its position, needs fall but it will not eat or sleep, it hits a hostile on an adjacent cell by itself, and it undrafts itself after **four in-game hours** with no order and no threat; going down or breaking ends the draft |
| Controls | **T** toggles the draft (R stays slice-up, owner's call) as does the pane's Draft button; right-click ground **moves**; right-click an animal, hostile or building **attacks**; **Ctrl+right-click** a colonist attacks it; right-click a downed colonist **rescues**; right-click a weapon **equips** |
| Weapons | fists by default; **bat and crowbar** (blunt, a chance to stun), **machete and sci-fi blade** (sharp); real items, one hand slot, drawn in the right hand; one or two in the starting kit and any from the debug Spawn tab; our own names in the wiki |
| Numbers | Melee becomes a live skill: hit 50 % at level 0, 80 % at 10, 90 % at 20; dodge 0 / 10 / 30 % by the *defender's* level; fists about 4 damage every 2 s, weapons 7–10 every 1.6–2.4 s; experience per swing; all of it in Defs, tuned after the first play |
| Hostile | debug-spawned, armed, hunts the nearest reachable colonist who is standing; a red marker |
| Animation | the Sword Combat pack's Polygon clips, in place (the simulation owns position), with a computed fallback for every role |
| Feedback | a health bar over hurt and drafted pawns; floating "miss", "dodge" and damage; swing, hit, miss, down and death sounds; a drafted marker and a line to the order's target |
| Delivery | one branch, built in order, with a playtest hand-over after C1, C2 and C3 |

## 2. Drafting (C1, built)

### 2a. The state

`Pawn.Drafted` and `Pawn.DraftQuietSinceTick`: whether the colonist is under the player's hand,
and the tick the draft last had anything to do (the draft itself, or the last order). Both live in
a save section of their own, `odyssey.combat` (`CombatSection`), which writes **only the pawns
that are drafted** — so a colony that has never fought writes an empty section, and a save from
before the section existed loads with nobody drafted. **No save-format bump**: the section is
keyed and skipped when absent, the same terms as the kind and wildlife sections.

The section opens with its own layout number, so C2 can append hit points and the rest without
touching the world's format number either.

**Hashed conditionally.** The drafted flag rides in bit 17 of the pawn's kind word (bit 16 is
wildlife's `Leaving`), and the quiet tick is added only while the colonist is drafted. A colony
nobody drafts hashes exactly as it did before combat existed. That is the precedent `Leaving` set,
and it matters twice: it keeps the combat-free goldens honest about what moved, and it is what lets
C5 and C6 assert that no golden moves at all.

### 2b. The mind

`DraftedThinkNode` sits **directly after `MentalState`** in the colonist's tree and **before
`CriticalNeeds`**. It always returns a job for a drafted colonist — the hold, or a move — so the
needs branch is never reached, which is the whole of "a drafted colonist does not eat or sleep".
Nothing is gated in `NeedsSystem`: the needs keep falling, exactly as the reference does it.

Three things end a draft without the player:

- **A mental break.** `JobSystem.TickPawn` undrafts a broken colonist on the tick the break is
  seen. A break is the one state the player is not allowed to command through.
- **Exhaustion.** At zero rest the Drafted node undrafts and declines, so the tree falls through to
  `CriticalNeeds`, whose collapse-at-zero rule (WS3, design 17 §4c) lays the colonist down where it
  stands. *Our addition, not the owner's words:* the reference lets an exhausted pawn collapse, and
  a draft that held a colonist upright at zero rest for ever would be a way round the rule.
- **Four quiet hours.** `DraftHoldJobDriver` undrafts when `DraftQuietSinceTick` is 10,000 ticks
  old and ends its job; the tree then gives ordinary work. An order resets the clock. The rule is
  checked by the hold itself rather than by a system sweeping the colony, so it costs nothing for a
  colony that is not drafted and one comparison a tick for one that is.

### 2c. The jobs

Two jobs, appended to the job table (handles 12 and 13 — **a job handle is a save contract**):

| Job | Driver | Behaviour |
|---|---|---|
| `Job_DraftHold` | `DraftHoldJobDriver` | Stand. Never expires, claims nothing. Ends when the four quiet hours run out. |
| `Job_Goto` | `GotoJobDriver` | Walk to a cell and end. A path that fails ends the job as failed, unlike the wander's. |

Both carry `casuallyInterruptible = false`. A move is a **forced job** — the same record, with
`PlayerForced` set — not a queue. When it ends, the Drafted node gives the hold again. There is no
order queue (Shift-queued orders are the reference's; not asked for).

### 2d. The intents

| Intent | Arguments | Handler |
|---|---|---|
| `SetDrafted` | `A` pawn id, `B` 1 draft / 0 undraft | `JobSystem.HandleSetDrafted` |
| `OrderMove` | `Cell` the clicked cell, `A` pawn id | `JobSystem.HandleOrderMove` |

Both **apply while paused**, the test `PausedIntents` states: a player's order over a colonist,
which is the thing a player pauses to give.

- **Drafting** ends the colonist's job as a failure. That is the one release path, so it drops a
  carried load, frees a bed and wakes a sleeper through the drivers' own cleanup. Then the hold
  starts *in the same call*, so the pane reads "Drafted" while the game is still paused.
  Undrafting ends the hold and leaves the colonist between jobs; the next tick's think gives it
  work. Refused (`NotPermitted`) for a pawn that does not exist, is not a person, or is broken.
  `AlreadyInThatState` for a no-op.
- **Moving** lifts the clicked cell to where a colonist would stand
  (`CellGrid.NearestWalkableInColumn`, the debug spawn's rule), and requires it reachable.
  Refused for a colonist who is not drafted: the reference moves only drafted pawns, and a
  right-click on an undrafted colonist's behalf is not a gesture this build gives a meaning.
- **Two colonists sent to one cell are spread.** If another drafted colonist already stands on, or
  is walking to, the chosen cell, the handler takes the nearest free reachable cell within two rings
  of it, in a fixed scan order. This is what makes a box selection moved with one click stand as a
  group rather than as one figure. The ring is small on purpose: a click that sends somebody three
  cells from where it was aimed is worse than two figures sharing a tile, and sharing a tile is
  already drawn legibly by the crowd sidestep (design 25).

### 2e. What the interface sees

Two sparse pawn aspects, published only for a drafted colonist (`CombatAspects`):

- `odyssey.pawn.drafted` = 1.
- `odyssey.pawn.order.cell` = the cell index a move is walking to, or absent.

Nothing in `Sim.Contracts` had to learn about drafting. The HUD reads the aspects by name. As with
the carry aspects, the two spellings are held together by a test on each side.

### 2f. Controls

- **T** (`HotkeyAction.Draft`, rebindable) drafts every selected colonist if any of them is
  undrafted, and undrafts them all otherwise. The rule is the reference's for a mixed selection.
- **The pane's Draft button** is live, and reads Undraft on a drafted colonist.
- **A right-click on the world with no tool armed** is an order. `DesignatePresenter` keeps first
  refusal: with a tool in hand the right-click still puts it down, exactly as before. With none,
  it forwards to `OrderPresenter`, the one subscriber that turns the click into orders. The
  gesture `15-building.md` §8 reserved for "build this now" is the same gesture, for the same kind
  of thing: a player overruling the scan for a colonist.
- `Hud.OrderModel` is the decision, Unity-free and tested in the fast tier. Given the selection,
  who is drafted, the clicked cell, the pawn under the pointer and whether Ctrl is held, it returns
  the intents to send, or none. In C1 its only answer is a move for each selected drafted colonist.
- **Box selection works.** Selection was never single — box and shift selection already existed —
  so an order goes to every selected drafted colonist, and 2d spreads them.

### 2g. What is drawn

- **A drafted marker** over every drafted colonist's head: a small diamond in `OrderColours.Draft`,
  lit so it does not dim at night.
- **An order line** from a moving drafted colonist to its destination, with a floor bracket on the
  destination cell, for the **selected** colonists only. Twenty lines across the board is noise; the
  ones you are commanding are signal.

Both are drawn through `ChunkRenderer`'s bracket material, like the selection cursor, and cost one
submission per drafted colonist. That scales with the number drafted, never with the board.

## 3. Health and melee (C2, designed)

- **Hit points** are integers in thousandths (`HpMilli`, the `Rates` convention), so a slow heal is
  exact without a float. A species' pool is `SpeciesDef.healthPoints`. **Downed** at ≤ 0. **Dead**
  at ≤ `deathAtPerMille` × pool (−500 ‰).
- **A swing:**
  - It starts on `NextSwingTick`, which lives on the pawn, so a new order does not reset the
    cooldown.
  - It lands at `windupTicks`. The animation's measured impact frame is scaled to land on the same
    tick.
  - Rolls, each on its own `PawnPurpose` stream salted by the pawn id:
    - **hit**, on the attacker's level curve;
    - **dodge**, on the defender's;
    - **damage**, ±20 % around the weapon's figure.
- **Death is deferred** (`ctx.Defer`), never inside the jobs loop, because `PawnRegistry.Despawn`
  shifts the list the loop is walking.
- **A corpse is not a pawn.** A `CorpseRegistry` (`odyssey.corpses`) records who, what kind, which
  seed, where, when and which way it fell. Every per-pawn loop would otherwise have to skip the
  dead, and wildlife already made every consumer survive a pawn vanishing.
- **Hostility comes from the kind.** `PawnKindDef.faction` is Colony, Wild or Hostile, so no new
  saved field is needed.
- **Presentation hears combat only through the snapshot:**
  - a `Strike` gesture on the existing serial;
  - a `CombatEventView` ring, a report that is never saved or hashed, for floating text and sound;
  - sparse aspects for hit points, the target and the weapon.
- **`PawnView` gains a trailing flags byte** (person, hostile, drafted, downed, stunned, carried).
  The six places that read "kind ≠ 0" as "animal" move to it. A hostile person is not an animal.

## 4. Later units, named

- **C3, weapons:** an `ItemDef.weapon` block, four weapon items, `Pawn.Equipped`, the equip job,
  and blunt stun. The held prop is fitted by the tool code's `GripTool`.
- **C4, rescue:** `Work_Rescue`, a pawn reservation kind, and carrying a body. A downed colonist
  heals only in a bed.
- **C5, friendly fire:** Ctrl-attack, self-defence, and the two memories.
- **C6, buildings:** hit points per placed edifice in a sparse store, demolished at zero with no
  refund.
- **C7, the gate:** the ten-day run with and without hostiles, benchmark rows, the records.

## 5. Do not undo by tidying

- **The Drafted node must stay above `CriticalNeeds`.** Moved below it, a drafted colonist wanders
  off to eat, and the draft means nothing.
- **Draft flags are hashed only while set.** Hashing a zero for every colonist moves every golden
  for no behaviour. Hashing nothing makes a drafted and an undrafted colony agree.
- **An order is a forced job, not a new queue.** A second path into `StartJob` is a second path out
  of it, and a second path out is where a reservation leak comes from. That is the argument
  `HandleForceJob` was written on.
- **`JobSystem.Load` accepts a save with fewer job defs than the build.** The job table is
  append-only, so the missing ones are the new ones, and their counters start at zero. It used to
  refuse any difference, which would have made every save from before C1 unloadable.
