# 33 — Combat: draft, move, melee

**Status: C1 (draft and move) built and played 2026-09-23 — the owner's verdict: drafting, T,
moving onto surfaces, the diamond and the four hours all work; the run (§2h) and the deeper red
(§2g) came out of that playtest. C2–C7 designed, not built; their contracts — every handle,
field and seam — cut on 2026-09-23 (§5), so four lanes can fill them at once.** The line's plan and
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
with something to say** — drafted, or part way through a kept step (§2d) — so a colony that has
never fought writes an empty section, and a save from
before the section existed loads with nobody drafted. **No save-format bump**: the section is
keyed and skipped when absent, the same terms as the kind and wildlife sections.

The section opens with its own layout number, so C2 can append hit points and the rest without
touching the world's format number either.

**Hashed conditionally.** The drafted flag rides in bit 17 of the pawn's kind word (bit 16 is
wildlife's `Leaving`), and the quiet tick is added only while the colonist is drafted. A colony
nobody drafts hashes its **pawns** exactly as it did before combat existed. (The goldens moved all
the same, for a different reason: the job table gained two defs and `JobSystem` hashes a counter
pair per def. The colony probe diffs clean.) That is the precedent `Leaving` set,
and it matters twice: it keeps the combat-free goldens honest about what moved, and it is what lets
C5 and C6 assert that no golden moves at all.

### 2b. The mind

`DraftedThinkNode` sits **directly after `MentalState`** in the colonist's tree and **before
`CriticalNeeds`**. It always returns a job for a drafted colonist — the hold, or a move — so the
needs branch is never reached, which is the whole of "a drafted colonist does not eat or sleep".
Nothing is gated in `NeedsSystem`: the needs keep falling, exactly as the reference does it.

**A drafted colonist is exempt from the think-loop breaker** (`JobSystem.Think`). Her own tree
gives only the hold, which cannot loop, and every other start is an order the player clicked; ten
brisk right-clicks tripped the breaker in review, which parked her in a plain 120-tick wait instead
of the hold.

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
  work. Refused (`NotPermitted`) for a pawn that does not exist, is not a person, is broken, or
  has no rest left — the hold would let go on its first tick and she would lie down again.
  `AlreadyInThatState` for a no-op.
- **Moving** lifts the clicked cell to where a colonist would stand on it, and requires it
  reachable. `JobSystem.StandAt` takes the cell itself, then the one above (the click named the
  block and she stands on top of it), then the one below (a click on the air over a lower
  terrace), and otherwise refuses. **It is deliberately not the debug spawn's column search**,
  which C1 first used: that looks down before up, so a click on the ground over a cavern found the
  cavern, found it unreachable and refused the order (review, 2026-09-23;
  `AClickOnGroundOverACavernSendsHerToTheSurface`).
  Refused for a colonist who is not drafted: the reference moves only drafted pawns, and a
  right-click on an undrafted colonist's behalf is not a gesture this build gives a meaning.
- **An order given mid-step keeps the step.** Ending a job clears the path and with it the step
  in progress, so the figure — drawn most of the way into the next cell — snapped back by up to a
  cell on every draft and every re-aimed right-click. `JobSystem.Interrupt` ends the job and then
  re-adopts that one step as a path of its own, marking the pawn `FinishingStepTo`; **the job loop
  holds every driver until it lands** (`JobSystem.TickPawn`), and the mover clears the mark on
  arrival. The hold was first in the walk toil alone, which a job whose first toil is not a walk —
  a collapse where she stands, work on the stance she is on — never reaches (review, 2026-09-23;
  `ReleasedMidStepSheLandsTheStepBeforeTheNextJobActs`). The
  mark is saved in `odyssey.combat` and hashed while set, because it is a path the world cannot
  re-derive — its destination went with the job that chose it — and without it a save taken
  mid-step resumed on a different trajectory. `AnOrderGivenMidStepLandsTheStepBeforeTurning`
  fails when the kept step is withheld (measured). A diagonal step is 141 ticks at the standard
  pace, so a colonist drafted mid-stride can take over two seconds to stop; that is the step,
  not a delay.
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
  undrafted, and undrafts them all otherwise. Bindings are stored by the action's key name, so
  adding the action shifted nobody's rebinds. **One edge case is real:** a player who had
  already rebound **T** to another action loses that rebind on the next load — the new default owns
  T before the stored line is read, the stored line is refused into `LoadConflicts`, and that
  action falls back to its own default. It is how every new default has behaved since the map was
  written (the Animals tab's F5 did the same), so it is recorded rather than special-cased. The rule is the reference's for a mixed selection.
- **The pane's Draft button** is live, and reads Undraft on a drafted colonist.
- **A right-click on the world with no tool armed** is an order.
  - `SliceCameraRig.WorldRightClicked` now carries the pick, the cell and the ray, as `Picked`
    does.
  - `DesignatePresenter` keeps first refusal: with a tool in hand the right-click still puts it
    down, exactly as before.
  - With no tool armed it hands the click to `SelectionPresenter.Order`. That presenter already
    owns the hit-test that says who is under the pointer, and it was in every play scene already,
    so the feature needs no scene rebuild. A new `OrderPresenter` component would have needed one.
  - The gesture `15-building.md` §8 reserved for "build this now" is the same gesture, for the
    same kind of thing: a player overruling the scan for a colonist.
- `Hud.OrderModel` is the decision, Unity-free and tested in the fast tier. Given the selection,
  who is drafted, the clicked cell, the pawn under the pointer and whether Ctrl is held, it returns
  the intents to send, or none. In C1 its only answer is a move for each selected drafted colonist.
- **Box selection works.** Selection was never single — box and shift selection already existed —
  so an order goes to every selected drafted colonist, and 2d spreads them.

### 2g. What is drawn

- **A drafted marker** over every drafted colonist's head: a small diamond in `OrderColours.Draft`,
  lit so it does not dim at night. **A deep, dark red, drawn translucent** (`OrderColours.DraftAlpha`
  0.70, the line at three quarters of that) — the owner's call after the first playtest,
  2026-09-23: *"make the cursor a deeper dark red but translucent"*. It was a hot orange-red.
  All three draft marks share the one hue; the selection brackets are untouched.
- **An order line** from a moving drafted colonist to its destination, with a floor bracket on the
  destination cell, for the **selected** colonists only. Twenty lines across the board is noise; the
  ones you are commanding are signal.

Both are drawn through `ChunkRenderer`'s bracket material, like the selection cursor, and cost one
submission per drafted colonist. That scales with the number drafted, never with the board.

### 2h. A drafted colonist runs

**Owner, after the first draft playtest (2026-09-23):** *"when you are drafted you should walk
faster/run as this would make sense with the urgency."* `Pawn.UrgencyPerMille` is the last factor
in `MoveRatePerMille`: `MovementDef.draftedPacePerMille` (2,000) for a drafted colonist, exactly
1,000 for anybody else — so no undrafted pawn's speed moved and no golden with it. After condition
in the product, so a starving colonist runs slower than a well one.

Twice the walk is about 3 m/s at the standard pace (2.6 to 3.5 across the innate band), past the
walk cycle's 2 m/s, so the gait blend draws the run clip in with no animation work — exactly the
run design 17 §4f held back until the game had a reason, and this is the first reason. It is a
**seam, not an urgency model**: fleeing and emergency jobs will answer through the same method when
they exist. The rate is a pawn factor, never a cell cost, so the planner's prices are untouched
(design 17 §4g). `ADraftedColonistRunsAtTwiceHerOwnPace` times the same crossing drafted and
walked.

### 2i. The sound of a draft

**Owner, 2026-09-23**, supplying a recording of a sword being drawn: *"use it when draft mode is
clicked/actioned as an indicator."* `SoundIds.Draft`, baked by `tools/audio/bake_draft.sh`
into `Clips/draft.wav`. The source has 108 ms of silence at the head, which is trimmed hard
because a tenth of a second of nothing after the key reads as lag. The tail is trimmed gently, since
the ring is the sound. It is mono, because the channels were identical, and levelled to a −3 dBFS
peak: 0.66 s, 58 KB.

- **Played off the frame, not the key.** `OdysseyBootstrap.SoundTheDraft` sounds once on the frame
  the snapshot first shows a colonist drafted. The key, the pane's button and anything later that
  drafts all sound, and a refused draft (a broken or spent colonist) stays silent. Releasing and
  the four-hour let-go are silent. A world seen for the first time never sounds, so loading a save
  with colonists drafted is not heard as orders.
- **An indicator in the mix:** 2D, no variance, and on the **Effects** bus rather than Alerts. It
  answers the player's own click and must not duck the music the way an alert does. A 0.25 s
  cooldown makes a box of five drafted at once one draw. `DraftSoundTests` holds the shipped row.
- **Licence:** Pixabay, by Dragon Studio, under the Pixabay Content License, which allows use inside
  a product without attribution but not redistribution on its own. It is committed with the game,
  as `docs/reference/audio-sourcing.md` requires the licence to be noted. **The owner to confirm the
  source.**

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

## 5. Contracts (the Phase 1 step, 2026-09-23)

`docs/plans/combat.md` runs C2–C7 as four parallel lanes, and a lane may only fill a seam; it may
not edit the shared spine. This section is **exactly what each seam is for**, so a lane can build
from it alone. The lane-by-lane brief — which files each owns, which tests it writes, which files
it must not touch — is `docs/plans/combat-contracts.md`. Branch `claude/combat-c2`.

**Nothing here fights.** Every new driver fails on its first tick, every new order is refused
(`NotPermitted`), every new think node declines. A colony that has never fought saves and hashes
exactly as it did before combat; the goldens moved once, in this step, for the new handles alone,
and the colony probe diffs clean (§5h). **From here no lane moves a golden.**

### 5a. Handles, claimed once

Every table below is append-only and a save contract, so all of them were extended in one commit:

| Table | New | Where |
|---|---|---|
| `JobHandle` / `JobIndex` / `Jobs.xml` / `BuildDrivers` | 14 `AttackMelee`, 15 `Flee`, 16 `Downed`, 17 `Equip`, 18 `Rescue` | `Catalogue.cs`, `PawnContent.cs`, `PawnRegistry.cs` |
| `SkillIndex` | 5 `Melee` (`Skill_Melee`), live in the Skills tab | `PawnContent.cs`, `Skills.xml`, `SkillCatalogue` |
| `WorkHandle` / `WorkTypeIndex` | 5 `Rescue` (`Work_Rescue`), live Work-tab column | `Catalogue.cs`, `WorkTypes.xml`, `WorkCatalogue` |
| `PawnKindIndex` | 3 `Marauder` (a person, faction `Hostile`) | `Species.xml`, `PawnKindLabels` |
| `ItemHandle` | 7 bat, 8 crowbar, 9 machete, 10 arc blade (category `Weapons`, stack 1) | `Items.xml`, `ItemLabels`, `ModuleIds` |
| `IntentKind` | `OrderAttack`, `OrderEquip`, `OrderRescue` — all apply while paused | `Intents.cs` |
| `PawnGesture` | 4 `Strike` | `Views.cs` |
| `PawnPurpose` | `MeleeHit`, `MeleeDodge`, `MeleeDamage`, `Revenge`, `Stun` | `PawnContent.cs` |
| `icon-keys.csv` | `ui.pawn.marauder`, `ui.status.{fighting,fleeing,equipping,rescuing}`, `ui.item.{bat,crowbar,machete,arcblade}`, `ui.combat.{miss,dodge,stunned,health,dead}`, `ui.debug.spawn{marauder,bat,crowbar,machete,arcblade}` | wiki rebuilt |

**The weapon names are ours**: bat, crowbar, machete and *arc blade* for the owner's "sci-fi
blade". The job words: Fighting, Fleeing, Downed (already in the registry), Equipping, Rescuing.

### 5b. Defs and every number

| Def | Field | Value (owner's, or INVENTED) |
|---|---|---|
| `SpeciesDef` | `healthPoints` | person 100, hog 60, rat 15 (owner) |
| | `deathAtPerMille` | −500 for all three (owner: dead at −50 %) |
| | `revengePerMille` | hog 700, rat 50 (owner: a hog turns, a rat runs); unread for a person |
| | `naturalAttack` | hog 6 dmg / 150 ticks / wind-up 30, blunt; rat 2 / 90 / 15, sharp (INVENTED); a person has none |
| | `meleeSkill` | hog 6, rat 3 (INVENTED); animals have no skills to train |
| `PawnKindDef` | `faction` | `Colony` (default), `Wild` for both animals, `Hostile` for the marauder |
| | `weapon` | an item defName the kind arrives holding: `Item_Machete` for the marauder (owner: "armed"; which weapon INVENTED), empty for the rest. Resolved into `PawnContent.KindWeapon` / `WeaponOf(kind)`; a name that is not a weapon fails the load |
| `ItemDef` | `weapon` (an `AttackDef`) | bat 7 / 120 / 30 blunt, stun 200 ‰ 60 ticks, heavy; crowbar 8 / 132 / 36 blunt, stun 250 ‰ 90 ticks, heavy; machete 8 / 96 / 22 sharp, light; arc blade 10 / 114 / 26 sharp, light; 50 points a swing (all inside the owner's 7–10 per 1.6–2.4 s, the split INVENTED) |
| `BuildingDef` | `maxHitPoints` | wall 300, floor 250, deck plate 150, ladder 80, bed 120, door 160, shelf 100 (INVENTED, C6's to tune) |
| `CombatDef` (`Combat.xml`) | `hitCurve` | 0 → 500, 10 → 800, 20 → 900 ‰ (owner) |
| | `dodgeCurve` | 0 → 0, 10 → 100, 20 → 300 ‰, by the **defender's** level (owner) |
| | `fists` | 4 damage every 120 ticks (owner), wind-up 18, 50 points a swing |
| | `damageSpreadPerMille` | 200 (±20 %, §3) |
| | `bedHealPerDay`, `animalHealPerDay` | 20,000 and 12,000 thousandths a day (INVENTED) |
| | `downedRecoverAtPerMille` | 150 (INVENTED) |
| | `chaseRepathTicks`, `retaliationTicks`, `revengeTicks`, `fleeCells` | 60, 1,200, 10,000 (the reference's floor), 12 |

`AttackDef` is one shape for a weapon, a natural attack and bare hands: `damage` (whole points),
`cooldownTicks`, `windupTicks`, `damageKind` (Blunt/Sharp), `stunPerMille`, `stunTicks`,
`experiencePerSwing` (thousandths of a point), `style` (Fists/Light/Heavy/Bite — the clip family,
never a clip). `CombatDef.Evaluate` is the curve arithmetic: linear between points, flat past the
ends, integer.

### 5c. State on the pawn, saved and hashed only while set

`Pawn` gained `HpMilli` (full at spawn: the pool × 1,000), `Downed`, `NextSwingTick`,
`StunnedUntilTick`, `RetaliateAgainst` / `RetaliateUntilTick`, `EquippedItem` (a `ThingId` value, 0
for bare hands), `CombatTarget` (the `PawnId` an order attacks or rescues — on the pawn because a
field on the job record would be a save-format bump) and `CarriedBy`. Derived: `HpMaxMilli`,
`DeathAtMilli`, `Faction`, `IsHostile`, `IsColonist`, `StunnedAt(tick)` and `HasCombatState`.

- **`CombatSection` layout 2** writes C1's four fields and then these eight, only for a pawn with
  `Drafted`, a finishing step or `HasCombatState`. Layout 1 still loads, its pawns whole.
- **Hashed as a block behind bit 19 of the kind word**, and only while `HasCombatState`.
- **`Pawn.Kind`'s setter keeps a whole pawn whole.** The loader builds every pawn as a colonist
  (pool 100) and only then reads its kind; a reloaded hog carried 100 of 60 hit points, which is
  combat state, and every round trip of a board with an animal disagreed with itself.
  `AnAnimalReloadedIsWhole` failed with the setter withheld (measured).
- **`Pawn.NeedsTick`** = a colonist who is not downed. A marauder has no needs (it is spawned to
  fight, and a raider that went for the pantry would be a second design); a downed pawn's needs
  pause (the C2 default). `NeedsSystem` asks it and nothing else.
- **A marauder is nobody's to draft**: `SetDrafted` and `OrderMove` ask `IsColonist`, and a downed
  colonist cannot be drafted. **Nor is either anybody's to force**: `JobSystem.CanForce` refuses a
  pawn that is not a colonist or is downed, so a forced build cannot end a `Job_Downed` (seam
  review, 2026-09-23).
- **A stun is a pause, not an interrupt** (seam review, 2026-09-23). `JobSystem.TickPawn` returns
  for a stunned pawn after the finishing-step hold: its job neither ticks nor ends and it does not
  think. `MovementSystem.Advance` lets it land the step in hand and take no other, with nothing
  banked. So a stunned hauler keeps her load, a sleeper his bed, a drafted colonist the player's
  order, and each carries on when it wears off. Interrupting the job instead would drop the load,
  free the bed and throw the order away, which is a different mechanic. `StunnedUntilTick` is
  nought in every golden window, so no golden moved.
- **`PawnRegistry.Despawn` releases the pawn's beds** (`ConstructionGrid.ReleaseBedsOf`), so a dead
  colonist does not keep hers under an id that no longer exists. In the despawn rather than the
  death so that every way off the board releases the same things; it does not raise
  `BedOwnershipChanged`, because a bed going to nobody takes nobody out of it.
- **The marauder arrives armed.** `PawnRegistry.Spawn(cell, kind)` calls
  `IWeaponRules.ArmOnSpawn` for a kind whose `weapon` names an item, after the pawn is adopted;
  the loader never does (it restores the hand from the save). Lane D fills it; until then the
  marauder fights with fists.

### 5d. What presentation reads

- **`PawnView.Flags`**, a trailing byte: `Person`, `Hostile`, `Drafted`, `Downed`, `Stunned`,
  `Carried`, with `IsPerson` / `IsAnimal` / `IsColonist` and the rest as helpers. The six places that
  read "kind ≠ 0 means animal" read the flags now (`OrderModel`, `RosterModel`, `AnimalsModel`,
  `InspectModel` through `PawnKindLabels.IsAnimal(view)`, `PawnFigureDirector`, `ChunkRenderer`,
  `SelectionPresenter`, `OdysseyBootstrap`, `PawnPose` twice). A view built by hand without flags
  reads its kind the old way, so no existing test changed.
- **`CombatEventView`** (`WorldSnapshot.CombatEvents`): a ring of the last 32 moments —
  `Swing` (amount = wind-up ticks), `Hit` (amount = damage in thousandths), `Miss`, `Dodge`, `Stun`
  (ticks), `Downed`, `Died`, `Recovered` — with attacker, target, cell and weapon. **Never saved,
  never hashed; ids are per world instance**, so a reader resets its watermark when the world
  changes. Written only through `CombatLog.Report`.
- **`CorpseView`** (`WorldSnapshot.Corpses`): id, the dead pawn's id, kind, roll seed, cell, tick,
  facing (eight ways) and its person and hostile flags — enough to draw the same face and name it.
- **Aspects**, sparse: `odyssey.pawn.hp` (thousandths, while hurt, downed or drafted — animals
  too; its presence is what says a health bar is owed) and `odyssey.pawn.hp.max` (the pool, for
  **every person always**, so the Health tab can say "100 / 100" of a whole colonist, and for an
  animal beside its `hp`; a person with a pool and no `hp` is whole — seam review, 2026-09-23),
  `odyssey.pawn.weapon` (item def, while armed), `odyssey.pawn.order.target`
  (pawn id, while under orders). Minted in `CombatAspects` (moved out of `Draft.cs`), copied as
  literals in `Hud.CombatAspectNames`, held together by a test on each side. A building target
  rides the existing `odyssey.pawn.order.cell`.
- **`PawnGesture.Strike`** on the existing serial, reported when a swing's wind-up starts.

### 5e. Seams in the simulation

| Seam | For | Owner |
|---|---|---|
| `IMeleeRules` / `MeleeRules` | melee level, hit and dodge chance, `Resolve` a swing (hit → dodge → damage in the spread → stun), deciding and never applying. `Resolve` throws until written | lane A |
| `IWeaponRules` / `WeaponRules` | `ArmamentOf(pawn)` — equipped weapon, else natural attack, else fists (the last two already true); `CanEquip` (no until written); `ArmOnSpawn(pawn, ctx)` — the kind's weapon into the hand, called by `Spawn` (nothing until written) | lane D |
| `CombatSystem` | Pawns phase, order 25 (after jobs, before movement), holding the context and the job pipeline; resolves swings on their wind-up tick, applies damage, stun, downing, deferred death, healing, retaliation expiry. Empty tick | lane A |
| `CombatHooks` / `ICombatListener` | `DamageApplied`, `Downed`, `Died`, raised by `CombatSystem` only, listeners called in registration order | raised by A; heard by C3 (drop on death), C4, C5 |
| `CombatListeners.Register` | the one place listeners are added, called by the composition; empty | lane D in Phase 2, then C4 and C5 in turn |
| `CombatLog` | the event ring; `Report(...)` | written by A (and D, C6), read by B |
| `CorpseRegistry` (`odyssey.corpses`) | `Add(pawn, tick, facing)` → id; saved, hashed while non-empty, published | A decides when |
| `EdificeDamage` (`odyssey.edificedamage`) | sorted sparse hit points by cell: `TryGet`, `Set`, `Clear` | C6 |
| Think nodes | `DownedThinkNode` first in all three trees; `SelfDefenceThinkNode` between the draft and the needs; `HostileThinkNode` in the new hostile tree (Downed, Hostile, Idle); `AnimalCombatThinkNode` ahead of the animal's idle | lane A |
| Drivers | `AttackMeleeJobDriver`, `FleeJobDriver`, `DownedJobDriver` (A); `EquipJobDriver` (D); `RescueJobDriver` (C4) — each in its own file | as named |
| Order handlers | `JobSystem.Attack.cs` (A), `JobSystem.Equip.cs` (D), `JobSystem.Rescue.cs` (C4), partial files, registered in `ColonyComposition` | as named |
| `RescueWorkGiver` | `Work_Rescue`'s emergency giver, answers no | C4 |
| `Draft.cs` | the hold's adjacent auto-attack goes in `DraftHoldJobDriver` | lane A |

`PawnContext` carries `MeleeRules`, `WeaponRules`, `CombatHooks`, `CombatLog`, `Corpses`,
`EdificeDamage` and `Combat`, built in its constructor (the rules settable) so a bare fixture has
them. The composition registers the corpse registry and the damage store as hashables and the
corpse registry and the log as snapshot contributors; `ColonyWorld.SaveComponents` appends the two
sections after `odyssey.combat`.

### 5f. Seams in presentation and the interface

| Seam | For | Owner |
|---|---|---|
| `PawnFigureDirector.Combat.cs` | a partial with `OnCombatEvent(in CombatEventView)`: swings off `Strike`, reactions, downed and stunned loops off the flags | lane B |
| `CombatPose.cs` | the computed fallback for every role, and the punch and bite the pack lacks | lane B |
| `CorpseDirector` | built, synced after the doors and disposed with them by the bootstrap; draws every corpse | lane B |
| `CombatFeedback` | the one reader of `CombatEvents`; the watermark rule is written; `Handle` calls nothing | lane B |
| `ModuleIds.Combat*` | nine clip rows by role — light and heavy swing, hit react, stagger, dodge, stun, downed, death, death pose — and four weapon item ids | lane B fills the catalogue |
| `CombatFeedbackModel` | `HealthBar`, `FloatingText`, `FloatingColour`, `HostileMarker`: fixed signatures, each answering "draw nothing" | lane C writes; lane B calls |
| `CombatOrders.Route` | the right-click's fight half, called first by `OrderModel.RightClick`; claims nothing yet | lane C |
| `HudDirectors.ChooseCorpse(corpseId, snapshot)` | the click on a corpse: lane B's hit-test calls it, lane C checks the corpse is in the frame, calls `Selection.ChooseCorpse` and answers true; answers false | lane C writes; lane B calls |
| `SelectionDirector.Corpse` / `HasCorpse` / `ChooseCorpse` | the selected corpse by `CorpseView.Id`, 0 for none; one subject like a pile, cleared by every other choice, let go after the grace when the frame stops carrying it. `HudShell` hands it to the pane (`InspectModel.SetCorpse`) | fixed; lane B's cursor brackets it, lane C's pane shows it |
| `InspectSubject.Corpse`, `InspectModel.Corpse`, `SetCorpse` | the corpse as a pane subject. A stub refresh: the corpse badge, the registry's word, where it lies, no tabs or commands; its state line is the model's `Job` | lane C names it ("Corpse of X") |
| `InspectModel.ShowsFace`, `ShowsColonistBody`, `ShowsTabBox`, `AvatarKey` | the pane's shape, which `HudShell.Inspect` reads instead of deciding from the subject and `IsAnimal`. Their values reproduce the pane as it was; a marauder and a corpse are lane C's to answer, in the fast tier. `Commands` is drawn for whatever subject the model fills it for | lane C |
| `HudShell.Combat.cs` | the Health tab's body (build, forget, show, sync — all called by `HudShell.Inspect`) | lane C |
| `HudShell.Debug.cs` | the Spawn tab's marauder and weapon rows | lane C |
| Health tab | enabled in `InspectModel`, empty | lane C |

### 5g. Why the rules are two interfaces

Lane A (the fight) and lane D (weapons) both need "the rules". One `ICombatRules` would be one file
both edit. So what a pawn **holds** (`IWeaponRules`, never rolls) is split from what a swing **does**
(`IMeleeRules`, never asks where the armament came from), and `Armament` is the one value between
them. Lane A fights with fists and teeth — already correct — while lane D teaches the lookup about
the hand.

### 5h. The goldens, once

All six moved, because the hash sees more zeros: ten job counters, a sixth skill, passion and daily
slot, a sixth priority, four allow-list slots. Nothing new is hashed while unset. `GoldenColonyProbe`
was widened to print mood, step progress, the first five skills' experience and passions, jobs
started and failed and each of the first fourteen job defs' counts, and run on `origin/main`
(33525521) and on this branch: **the outputs diff clean for all three colonies.** Recorded in
`Golden.cs` and the journal.

### 5i. Recorded, not fixed

`PawnPurpose.AnimalMind` is `0x165667B1`, the same value as `DeconstructRefund`. It predates combat;
changing either moves a golden, and the two streams are keyed differently. Noted beside the new
salts.

An animal's pane builds the tab box from an empty tab list, so it carries an empty needs grid. That
is the pane as it was before combat, and `InspectModel.ShowsTabBox` keeps it rather than a contracts
step changing what a player sees; it is one line to change when somebody decides.

### 5j. Rules two lanes share (the seam review, 2026-09-23)

A reviewer read the contracts against the briefs before any lane started and found places where two
lanes would each have decided the same thing, or neither would. Each is decided here once, and both
briefs point at it.

- **Equipping needs no draft.** A right-click on a weapon sends `OrderEquip` for the **primary
  selected colonist, drafted or not** — it is a fetch, not a fight, and one weapon fills one hand —
  and `HandleOrderEquip` accepts it for an undrafted colonist. Refused for a downed pawn, a
  hostile, an animal and a pawn that does not exist. *Our call, not the owner's*: the reference
  allows it, and the owner's controls row names the gesture without a draft. Lane C's
  `CombatOrdersTests` sends it for an undrafted colonist and lane D's `EquipTests` accepts one.
- **Attack and rescue need a draft**, as §1 has it: the right-click's fight half sends nothing for
  an undrafted selection.
- **A right-click on a building is not an attack until C6.** In C2/C3 it falls through to the move,
  so a drafted colonist can still be moved on to a floored room. When C6 routes it, it attacks only
  an **edifice that occupies the cell** — a wall, a door, furniture — never a floor, a slab or a
  deck plate, although those carry `maxHitPoints` too; C6's `CombatOrdersTests` shows a click on a
  floored cell still moves.
- **Going down ends a mental break.** Lane A's one apply method sets `BreakTicksLeft = 0` when it
  downs a pawn; otherwise `JobSystem.TickPawn` fails the downed job on every tick of the break and
  the downed node restarts it, inflating the hashed counters and tripping the think-loop breaker.
- **What the swing faces and swings with.** Every `CombatLog.Report` carries the armament's
  `ItemDef` (−1 for fists or teeth). `AttackMeleeJobDriver.WorkFocus` returns the target's cell
  during the wind-up only, which is how presentation turns the figure. **The computed work stroke
  never plays for `Job_AttackMelee`** (`WorkSwing`/`WorkStyle` are for work); the swing's clip
  family comes from the event's `Weapon` (its `AttackDef.style`), else from the flags — a person
  fights with fists, an animal bites.
- **A swing whose attacker is stunned or down when its wind-up ends does not land.** *INVENTED*:
  "no swing before it" in the stun's definition, applied to a swing already begun. Lane A tests it.

## 6. Do not undo by tidying

- **The Drafted node must stay above `CriticalNeeds`.** Moved below it, a drafted colonist wanders
  off to eat, and the draft means nothing.
- **Draft flags are hashed only while set.** Hashing a zero for every colonist moves every golden
  for no behaviour. Hashing nothing makes a drafted and an undrafted colony agree.
- **An order is a forced job, not a new queue.** A second path into `StartJob` is a second path out
  of it, and a second path out is where a reservation leak comes from. That is the argument
  `HandleForceJob` was written on.
- **Combat state is hashed only while set** (§5c), and the corpse registry and the damage store
  contribute nothing while empty. That is what lets every combat lane assert the goldens unchanged.
- **`PawnView.Flags`, never the kind, says what a pawn is.** A marauder is kind 3 and a person.
- **`Pawn.Kind`'s setter re-fills a whole pawn's hit points.** Without it a reloaded animal is hurt.
- **`JobSystem.Load` accepts a save with fewer job defs than the build.** The job table is
  append-only, so the missing ones are the new ones, and their counters start at zero. It used to
  refuse any difference, which would have made every save from before C1 unloadable.

## 6A. Lane A — the fight (simulation)

**Built 2026-09-23 on `claude/combat-fight`** (from the contracts head `229b00a0`), fast tier and
Long tier only, no Unity. Every seam of §5e marked lane A is filled; nothing outside them moved but
the one spine line in §6A.7. **No golden moved**: no golden window drafts, strikes or spawns a
hostile, every node declines for a pawn at peace, and every clock the fight sets runs back out to
nought (§6A.5), so a colony that fought and healed hashes as one that never fought.

### 6A.1 A swing

- **The driver starts it; the combat pass lands it.** `AttackMeleeJobDriver` has two toils:
  *approach* and *wind-up*. In reach (`Melee.InReach`: the same cell, or a legal step to the next
  one on the same layer — no blow through a wall's corner, none over a terrace edge) and with
  `NextSwingTick` come, it starts a swing:
  - sets `NextSwingTick` to now plus the attack's cooldown — on the pawn, so a re-order keeps it;
  - reports `PawnGesture.Strike` and the `Swing` moment (amount = wind-up ticks, with the weapon);
  - counts the wind-up in `ToilProgress`, milliwork like every driver.
- **`CombatSystem` resolves the swing** on the tick the wind-up completes: after every job has
  ticked (20), before anybody steps (30). It asks `IMeleeRules.Resolve`, then applies the outcome
  through **`CombatSystem.ApplySwing`, the one method a hit point is lost through**, and puts the
  driver back to *approach*.
- **The four rolls** are hit, dodge, damage and stun, each on its own `PawnPurpose` stream salted by
  the attacker's id.
  - A downed defender does not dodge.
  - Damage is uniform over the figure ±`damageSpreadPerMille`, both ends included.
  - A sharp blow never stuns, whatever its numbers say.
- **A swing whose attacker is stunned, down or dead when it would land is lost** (§5j). A stun
  *pauses* the job (§5c), so without the rule the wind-up would wait out the stun and land the
  moment it wore off. `AStunnedAttackersSwingDoesNotLand` failed with the check withheld
  (measured).
- **A swing that arrives on air** is reported as a `Miss` with no roll. That is a target that
  stepped out of reach during the wind-up, or went down to somebody else's blow on a job that
  stops at down.
- **`WorkFocus`** is the target's cell during the wind-up only, else −1. The cell is kept in the
  job's `TargetCell`, which is saved.
- **Experience**: `experiencePerSwing` into Melee for a person, landed or not, at the resolve.

### 6A.2 The chase, and when an attack ends

- **What the driver decides with is saved.** Every decision reads the cells, the step progress,
  `Destination`, `JobStartTick`, the toil and the job's fields, and never the path, which is not
  saved.
  - `Job.WorkTicks` holds the tick of the last re-plan. `BuildJobDriver` set the precedent of a
    driver using that field for its own purpose.
  - `Job.DestCell` is `ToTheDeath` (1) or −1.
- **Re-plan** to the target's cell only when the target has left the cell the walk leads to. At
  once if the pawn is not walking; otherwise at a step boundary (`MoveProgress` under one tick's
  movement, so the step in hand is never snapped back), no oftener than `chaseRepathTicks` (60).
  The computed brief said ~20; the Def says 60 and content is frozen, so 60.
- **In reach mid-step**, the step is landed first; at the boundary the pawn stops and swings.
- **It ends:**
  - when the target is gone or dead;
  - when the target is down — unless the order was given on a pawn already down, which is
    `ToTheDeath` and is how a marauder that "stays down until killed" gets killed;
  - when the target is unreachable;
  - for a drafted colonist's own blow at an adjacent threat, when the target leaves reach: **the
    hold never chases**;
  - for any attack nobody ordered (the hunt, revenge, self-defence), after **`RechooseTicks` = 300**
    at a step boundary, so the mind picks again. *INVENTED*, a constant in the driver because Defs
    are frozen in Phase 2; proposed below for `CombatDef`.
- **Buildings**: a job with no `CombatTarget` is C6's branch and fails. It is one marked line at
  the top of `Tick`.

### 6A.3 Down, dead, up

- **Down at ≤ 0** (`CombatSystem.Down`), all in the same call:
  - the job ends through `JobSystem.Interrupt`, **keeping the step in progress**, so the body lands
    where its figure was drawn;
  - the draft ends, and so does any break (`BreakTicksLeft = 0`, §5j);
  - `Job_Downed` starts, so a paused frame reads "Downed";
  - `Downed` is reported with the weapon, and `RaiseDowned` fires.
- **A broken colonist downed** has one `Job_Downed` start and no failures for the rest of what
  would have been her break. With the break left running, the downed job restarted (measured).
- **Dead at ≤ −50 %** (`CombatSystem.Kill`):
  - `Died` is reported at once, so it is published in the same frame as the corpse;
  - the removal is `ctx.Defer`red to the end of the tick, and runs in order: `Corpses.Add` (facing
    away from the blow, `Melee.FallFacing`), `RaiseDied`, `EndJob`, `PawnRegistry.Despawn`. The
    despawn releases the pawn's reservations and beds itself.
  - **Only the blow that crosses the line kills**, so two blows on one tick kill nobody twice.
  - **A blow from standing to past the line kills without a fall**: no `Downed` hook, which is a rat
    losing 23 of its 15.
  - `ADeathIsDeferredToTheEndOfTheTick` failed with an immediate removal (measured).
- **Up at 15 %** (`CombatSystem.Recover`), checked on the heal that crosses the line, never
  inferred elsewhere. `Job_Downed` ends as a success, and `Recovered` is reported. A downed pawn
  whose hit points are not rising stays down whatever they are — the contract test's colonist
  flagged down at full health stays down.
- **`Job_Downed`** answers `Ongoing` while down and fails on a pawn standing up, which is the stub's
  contract. It never ends itself: getting up is the combat pass's decision.

### 6A.4 Healing

On the needs cadence and the needs system's own phase spreading (`(tick + id) % 150`), **over the
hurt only**: a whole pawn costs one comparison.

- An **animal** heals anywhere, at `animalHealPerDay`.
- A **colonist** heals only **lying in a bed** (down or asleep, on a bed cell), at `bedHealPerDay`.
- A **marauder** never heals.

The fraction one interval cannot carry is spent by the interval index, so a day's healing is the
content's figure to the thousandth (`ADaysHealingIsExact`).

### 6A.5 The clocks run back to nought

`StunnedUntilTick`, `NextSwingTick` and the retaliation are cleared once past. So is
`CombatTarget`, by the attack driver's cleanup. A pawn over its fight — healed, unstunned, not
fighting — therefore carries no combat state and hashes as it did before (§6), and that is what
lets C5 and C6 assert the goldens unchanged.

### 6A.6 The minds

| Node | Does |
|---|---|
| `DownedThinkNode` | down → `Job_Downed`; one branch for anybody standing |
| `DraftedThinkNode` (`Draft.cs`) | a threat in reach → the blow (not forced, so never chased), and the four quiet hours start again; else the hold. **The hold itself ends when a threat comes into reach**, since a holding colonist never thinks |
| `SelfDefenceThinkNode` | the colonist who struck her while `RetaliateAgainst` holds; else a threat beside her |
| `HostileThinkNode` | the nearest reachable standing colonist; nobody standing → idle |
| `AnimalCombatThinkNode` | carries a revenge on across thinks; the roll itself is at the blow |

**A threat** (`Melee.IsThreatTo`) is a standing hostile, or anybody standing whose attack is aimed
at her. So a vengeful hog, or a colonist who Ctrl-attacked her, is answered by the hold as well.

**At the blow** (`CombatSystem.React`), a pawn still standing answers:

- **An animal** rolls its species' revenge on `PawnPurpose.Revenge`.
  - Turning, it hunts the attacker for `revengeTicks`.
  - Not turning, it runs (`Job_Flee`, to `FleeJobDriver.FindFleeCell`: a fixed scan straight away
    from the threat, then turning 45° and 90° either way, at `fleeCells` halving to 1).
  - **A failed roll does not un-turn an animal already turned on that attacker.**
- **An undrafted, unbroken colonist** remembers **whoever** struck her for `retaliationTicks` and
  is interrupted, so her self-defence answers on her next think.
  - *Our call, past the owner's colonist-on-colonist rule.* Remembering only a colonist was
    tried first, and it failed: the struck colonist landed the step she was on, which took her out
    of reach; she found nobody beside her and went back to wandering while the marauder beat her
    down (measured).
- **A marauder** struck by a colonist it was not fighting re-thinks, and its hunt then finds the
  colonist beside it.
- **A drafted colonist** reacts to nothing here: her hold fights.

### 6A.7 The one spine edit: a chase runs

`Pawn.UrgencyPerMille` answers the run (`draftedPacePerMille`, 2,000) for `Job_AttackMelee` and
`Job_Flee`, as well as for the draft. §2h named this seam for exactly that day.

At walking pace a hunt closed on a wandering colonist at the speed she walked away, and in 3,000
ticks on a bare board never reached her (measured). With the edit withheld,
`AColonistStruckByAMarauderFightsBack` fails. No golden window fights, so no golden moved.

### 6A.8 The order

`OrderAttack(A, B)` is accepted for a drafted, standing colonist of ours against any pawn that
exists, is alive, is not herself, and is within reach or reachable. The target may be an animal, a
marauder, or a colonist.

- **The Ctrl a colonist target needs is the interface's gesture** (`CombatOrders.Route`, lane C).
  The contract gives the intent no argument to carry it. The computed brief asked for "colonist
  only when the intent says forced/Ctrl"; honouring it would need a `C` argument that lane C does
  not send, so it is left to the integrator (open below).
- **`B = 0` stays refused** (C6).
- The order ends the job in hand through `Interrupt`, names the target on the pawn, and starts a
  forced `Job_AttackMelee`. An order given on a pawn already down is `ToTheDeath`.

### 6A.9 What it costs

`CombatSystem.Tick` is one pass over the pawns with a branch each, plus the swings landing and the
heals due. The threat scan (`Melee.AdjacentThreat`) is one pass over the pawns per drafted
colonist per tick, and per self-defence think.

`TickBenchmarkTests.TwentyAgainstTwenty` (explicit), 120 × 120 × 16, twenty colonists, one run on
the Windows dev machine, 2026-09-23:

| Case | Tick, mean | Pawns phase, mean |
|---|---|---|
| At peace | 0.034 ms | 0.007 ms |
| Against twenty marauders | 0.069 ms | 0.012 ms |

The fight run resolved 272 swings in its 1,500 ticks.

### 6A.10 Tests (fast tier unless marked)

Each claim was seen to fail with its rule withheld.

| Test file | What it covers |
|---|---|
| `CombatMathTests` | the owner's curves; the rates measured over 4,000 swings; the spread's ends; blunt stuns and sharp never; one stream per roll — failed with a shared stream (measured) |
| `AttackDriverTests` | the chase; the cooldown; a re-order keeps the clock; the wind-up focus; the weapon on every report; the lost stunned swing; the refusals; the hold that never chases — failed with the rule withheld (measured); a save taken mid-swing resumes on the same hash 600 ticks on, with a forgetful load as the control |
| `DownedDeathTests` | the fall, death and the corpse; the corpse's facing |
| `HealingTests` | healing in a bed, and the animal and marauder cases |
| `HostileTests` | the hunt and self-defence |
| `AnimalRevengeTests` | revenge rates, a hog 700 ± 60 ‰ and a rat 50 ± 30 ‰ over 400 blows each |
| **Long:** `MarauderSoakTests` | a marauder a day for ten days on the soak's board: invariants at every 500 ticks and a save on day five resumed equal a day later. 9.5 s; 536 swings, all ten marauders down, no deaths with fists |

`CombatFixture` is the shared test fixture. `HeldWeapon` puts a weapon in a hand without lane D.

### 6A.11 Open

- **Proposed for `CombatDef` at the integrator's Def pass:** `rechooseTicks` = 300.
- **The Ctrl flag on `OrderAttack`** — see §6A.8.
- **A downed body slides the rest of its step.** The price of keeping the step (§2d): the figure
  lands where it is drawn, rather than snapping back.

### 6D. Lane D — weapons (C3 simulation)

Built 2026-09-23 on `claude/combat-weapons` from the contracts commit `229b00a0`. Fast tier only;
nothing here touches presentation.

**The hand.** `WeaponHand` is the only code that puts a weapon in a hand or takes one out. A held
weapon stays an ordinary `ColonyItem` with **no cell**, `CarriedBy` its holder, named by
`Pawn.EquippedItem`. It goes up through `ColonyItems.PickUp` — the door every lift uses — and down
through `ColonyItems.Drop`, so it leaves and rejoins the listers exactly as a hauled load does.
**Held is one fact with one owner, the item's carrier**: `WeaponHand.Held` believes the pawn's field
only while that thing is carried by that pawn, with no cell and in no store, so a field naming a
thing on the ground arms nobody (`AWeaponLyingOnTheGroundArmsNobody`, which fails with the check
loosened — measured). The hand is **not** the job's `CarriedItem`: that is the load a job is moving
and `DropCarried` puts it down when the job ends, and a weapon has to outlast every job, a haul
included. A colonist can hold a machete and carry a log at once, and neither field reads the other.

**What reads a carried thing** (the audit the brief asked for). `ColonyItem.CarriedBy` is read only by
the item section's hash and save. Every scan that looks for something to fetch — haul, eat, deliver,
the storage re-bucketing — asks `PawnContext.WhereIs`, which answers -1 for a thing in a pair of
hands, so a held weapon is invisible to all of them; the snapshot's thing list skips it for the same
reason, and the carry aspects publish only `Job.CarriedItem`. `Falling.DropFloatingItems` skips a
thing with no cell. Nothing needed changing. `AHeldWeaponStaysInTheHandWhileTheColonyWorks` runs a
working colony round an armed colonist for 2,000 ticks. (`Pawn.CarriedBy` is a different field — a
carried *patient*, C4's — and nothing here touches it.)

**Rules** (`WeaponRules`):

- `ArmamentOf`: the held weapon's `ItemDef.weapon` and its def index, else the species' natural
  attack, else `CombatDef.fists`. It never rolls: a bat's or a crowbar's stun rides in the
  `Armament`, and lane A rolls it.
- `CanEquip`: a standing colonist of ours (not an animal, not a hostile, not downed), a thing whose
  def has a `weapon` block, not forbidden, and lying somewhere a colonist can take it from — a cell
  or a store, never somebody's hands. Reachability is the order's question, not this one's.
- `ArmOnSpawn`: the kind's weapon is made on the nearest cell to the spawn that can take it and
  taken straight up through `WeaponHand.TakeUp`, the same door the equip job uses. A marauder spawned
  on a pile is still armed and the pile is undisturbed. A board with nowhere within
  `JobDriver.DropSearchRadius` leaves it bare-handed.

**The order and the job.** `OrderEquip(cell, A = colonist, B = thing)` is accepted **drafted or
not** (§5j). Every question is asked before anything is interrupted, so a refusal claims nothing:
`NotPermitted` for a pawn that does not exist, an animal, a hostile, a downed colonist and — *our
call, the draft's rule* — a colonist in a mental break; and for a thing that does not exist, is not a
weapon, is forbidden, is in somebody's hands, cannot be reached, or is already claimed by somebody
else's fetch. `AlreadyInThatState` for the weapon already in the hand. The cell is not read; the
thing's own record says where it is. Then the job in hand ends through `Interrupt` (the kept step,
§2d), a drafted colonist's quiet clock resets, and a **forced** `Job_Equip` starts. `EquipJobDriver`
claims the weapon (an item reservation, as a haul or a meal does), walks to it, and runs
`LiftToil` — the one stoop every lift uses. **It changes hands at the grasp**: the lift puts it in
her arms as a job's load and on that same tick it moves to the hand and the old weapon goes down, on
the cell the new one just left if it is free, else the nearest that can take it. No stow is reported
for the old one, because a second gesture would cut the stoop off halfway down. A drafted colonist
returns to the hold when the job ends; an undrafted one to her work.

**Death.** `WeaponDropListener`, the first listener `CombatListeners.Register` adds: on `Died` the
weapon goes down at the corpse's cell, or the nearest cell that can take it; on `Downed` nothing
happens (the C2 default: a downed pawn keeps its weapon). **A dropped weapon is not forbidden** —
*our call*: a marauder's machete is the colony's the moment it falls, and a line in the listener is
where a forbid would go.

**The starting kit.** `ScenarioDef.startingWeapons`, item defs, **empty by default**, so `Bare` and
every golden are untouched (an empty kit asks the storey search for no more spots). `Playtest` lays a
**bat and a machete** on the ground beside the food, one a cell, in nobody's hand — a blunt and a
sharp, so the first fight shows both a stun and the quicker blade (*INVENTED* inside the owner's "one
or two"). Placement reports them as `Result.Weapons`. A debug-spawned weapon needs nothing here:
`GiveResource` already places any item (lane C's row).

**Scales with** nothing per tick: the equip driver is one pawn and one thing; the listener runs on a
death; the rules are constant-time lookups. No sweep was added.

**Goldens: unchanged** — no golden builds on `Playtest`, spawns a marauder or equips anything.

**One spine edit**: `CombatContractTests.AMarauderIsSpawnedThroughTheArmingSeamAndAnAnimalIsNot`
ended by asserting that the stub rules arm nobody. That assertion is exactly what this lane exists
to make false; it now asserts the machete. Nothing else in the file moved.

**Recorded, not fixed:**

- A pawn **despawned without dying** while holding a weapon would leave it held by an id that no
  longer exists — carried, with no cell, for ever. Nothing does that today (only animals leave the
  board, and no animal is armed), but a marauder that flees off the edge would. The fix is one line
  in `PawnRegistry.Despawn` (put the hand down, as it already releases beds), which is spine.
- `Job_Equip` has no status word wired in the interface yet (`ui.status.equipping` exists); lane C.

### 6C. Lane C — the interface

Built 2026-09-23 on `claude/combat-hud` (from `229b00a0`). Everything below is in `Odyssey.Hud`
and runs in the fast tier except the two Presentation files, which **have never been compiled**
(`HudShell.Combat.cs`, `HudShell.Debug.cs`).

**The right-click** (`CombatOrders.Route`, `CombatOrdersTests`). A pawn under the pointer wins over
the cell it stands in. An animal or a hostile is attacked by every selected drafted colonist, a
downed one included (a marauder stays down until killed). Ctrl on a colonist is an attack by every
selected drafted colonist but her — and **Ctrl wins over the rescue**, being the one gesture that
says "hit this one of ours" outright. A downed colonist is rescued by the **nearest** selected
drafted colonist only (*our call*: one body, one carrier; sending all of them is a walk the
reservation would refuse at the end of). A weapon in the clicked cell **or the one above it** (the
rule a left click selects a pile by) is fetched by the first colonist in the selection, drafted or
not. Everything else is the move, a floored or walled cell included. A downed, hostile or animal
pawn in a stale selection is never an attacker.

**A missed seam: the presenter's gate.** `SelectionPresenter.Order` (lane B's file) returns before
the hit-test unless `OrderModel.AnyDrafted`, so an equip for an undrafted colonist never reaches
the model. `OrderModel.HearsRightClick` (any colonist, drafted or not) is the gate it needs; the
one-line change in the presenter is the integrator's. Until it is made, equipping needs a draft.

**What the fight says** (`CombatFeedbackModel`, `CombatFeedbackModelTests`). A bar is owed exactly
where the simulation publishes `hp` (hurt, downed or drafted), clamped to 0..pool, never without a
pool. Floating words: Miss, Dodge, Stunned, Downed, Dead from the registry, and a hit as "-7" —
the nearest whole point, never "-0", from a table built once so a brawl allocates nothing. Ink:
damage and the two ends of a fight in `HudTheme.Bad`, a miss dim, a dodge `Info`, a stun `Warn`.
**Two answers added beside the fixed four** for lane B: `FloatingSeconds` (0.9 s for a miss or a
dodge, 1.2 for damage, 1.4 for a stun, 2.2 for downed and dead — the ones a player looking
elsewhere most needs to catch) and `HealthBarColour` (the need bar's 600/400 thresholds). All
INVENTED, to be tuned after the first play. The marker is `IsHostile` and nothing else: a wild
animal fights back but is not an enemy.

**The pane** (`InspectModel`, `CombatPaneTests`).

- **A marauder** has no face, no colonist body, no tab box, no tabs, no skills and no commands,
  and wears `ui.pawn.marauder`; its line is its job in a person's words ("Fighting"). *Our call*:
  no Health tab means its health is the bar over its head and nowhere else.
- **A corpse** is "Corpse of Wrenn" — the name she wore alive, `ColonistNames.Of` over the seed
  and id the corpse kept, so a player's own name outlives her — or "Corpse of a midden hog" /
  "Corpse of a marauder". The line under it is "Dead · since 07h, day 3 of Larkspur", composed
  once per corpse. A corpse the frame no longer carries says only "Corpse".
- **A pawn that leaves the frame keeps its shape.** An animal (since #167) or a marauder that died
  fell to the colonist's tombstone and grew a Health tab and a Draft button for the grace frames.
- **The Health tab**: "73 / 100" from `hp.max` and `hp` (no `hp` is whole), rounded **up** so a
  colonist on her feet never reads nought; a fill in the bar's colours; then condition (Unhurt,
  Hurt, Stunned, Downed — the flags first) and weapon (the held item, or Bare hands). Five keys
  were added for it: `ui.combat.{unhurt,hurt,condition,weapon,barehands}`.

**Elsewhere.** `HudDirectors.ChooseCorpse` selects only a corpse the frame carries and leaves the
selection alone otherwise. The Spawn tab is `DebugDirector.SpawnRows`, a table the fast tier holds
(the marauder is `SpawnPawn` with kind 3, each weapon `GiveResource` with one item). Alerts and the
Work tab read colonists by the flags; alerts had counted animals toward "is the colony idle" since
#167, which a wandering hog always defeated. The Almanac opens an animal's corpse on its Fauna
entry, and nothing for a person's corpse, a marauder (it opened a colonist's Skills page) or a
weapon (it opened the Ration Pack). No attack colour was added to `OrderColours`: nothing draws an
attack order in a colour of its own yet.

### 6B. Lane B — the fight (drawn)

**Built 2026-09-23 on `claude/combat-drawn`** (worktree `D:\code\odyssey-combat-drawn`, from the
contracts head `229b00a0`), against the `CombatEventView` contract with a scripted event feed, not
against lane A's code. Everything here is presentation: no simulation, interface, Def or golden
changed. Tests: `Presentation/Tests/CombatDrawnTests.cs` (EditMode), each with a negative control
seen to fail (below).

**The clip layer.** A person's graph is now the gait mixer on input 0 of an
`AnimationLayerMixerPlayable` and a two-slot action mixer on input 1 (`PawnFigureDirector.Combat.cs`,
`BuildCombatLayer`). A combat clip goes into the free slot and cross-fades against the other over
0.1 s; the layer's weight eases the action in and out over 0.12 s. **Every action clip plays at
speed nought and has its time set each frame**, so a pause holds it and the game speed cannot run it
away from the blow. An animal, and a checkout without the pack, keep the graph exactly as it was
(output reads the gait mixer).

**The swing's clock is the simulation's.** A `Strike` on the gesture serial starts the swing; its
`Swing` event, read the same frame, refines the start tick, the wind-up and the weapon without
starting a second one. The swing is timed in ticks since the wind-up began — the frame's tick plus
its part-tick — and the clip is scaled by `impactSeconds / windupTicks`, so **the authored impact
frame lands on the tick the simulation resolves the blow** (`CombatPose.ClipTime`). The impact is
measured, not typed: every attack FBX is cut by its author into WindUp, Hit and FollowThrough, and
the catalogue build reads the WindUp's last frame off the importer (`PlayScene.MeasureImpact`):
LightCombo01 A/B/C 0.333/0.167/0.367 s, HeavyCombo01A 0.967 s, HeavyStab01 0.867 s. At the owner's
wind-ups (18–36 ticks) the light swings play at 0.3–1.2× their authored rate and the heavy ones at
1.4–1.9×; the follow-through keeps the pace the blow was struck at.

**Which clip.** The family is the event's weapon's `AttackDef.style` (`WeaponStyles`, read once off
the content by the bootstrap), else the body: a person punches, an animal bites (§5j). Light swings
take LightCombo01 A, B, C in turn; heavy ones HeavyCombo01A and HeavyStab01. A hit reacts by the side
the blow came from (`CombatPose.SideOf`, F/B/L/R); a stun, or a blow of 10 points or more, staggers;
a dodge steps away from the blow, **never right** (`Dodge_R` imports Generic), so a blow from the
left is dodged backwards. Downed is `KnockDown_Begin` then `_Loop`, and `_End` on getting up; stunned
is `Stun_Begin`, `_Loop`, `_End`. The held states are read off `PawnView.Flags`, not events, so a
figure leased mid-fight shows them — with no begin clip, because it did not see the fall.

**A react never cuts off the figure's own swing**, which the simulation will still land on its tick;
a stagger does, because a stunned attacker's wound-up swing does not land (§5j). Going down or
getting up ends any one-shot.

**The work stroke never plays for `Job_AttackMelee`** (`PawnFigureDirector.PlaysWorkStroke`): the
attack driver's work focus during the wind-up turns the figure to its target and does nothing else.
The step-up to a work cell and the gaze's work focus are gated the same way. A `Strike` is never
drawn as the lift's crouch (`GestureOf` would otherwise read it as one).

**The computed fallbacks** (`CombatPose`), laid on after the graph exactly as the work stroke is:
a swing for each style as three keys (rest, the cock at 0.7 of the wind-up, the blow at 1.0) and a
recovery of 0.8 of the wind-up; the punch and the bite are always computed (the pack has neither);
a hit react (0.5 s), a stagger (0.9 s) and a dodge (0.6 s) as a jolt away from the blow; a stun as a
slow sway; **the downed lie is the sleeper's lie on the ground**, and the computed corpse is
`CombatPose.LieFlat`. An animal has no bones bound, so its bite and its jolts move and pitch the
whole body. Every angle and distance here is INVENTED and wants a contact sheet.

**The dead** (`CorpseDirector`). A corpse is a figure lent out (`BorrowForCorpse`), dressed in the
colours its **own roll seed** deals — the frame no longer carries the pawn, so a seed read off the
frame would be nought and deal somebody else — laid down, baked to static meshes and handed straight
back to the pool. **Corpses never count against the 64-figure ceiling** and cost a static mesh each.
A death seen happening (within 120 ticks, in a world already on screen) plays `Death_{side}` first;
one found lying is baked at once. The body is turned so its head lies along the corpse's own facing:
the finished `_Pose` is measured (which way the head ends up from the feet) and turned to it, and its
middle is put on the middle of the cell. With no art for the face, a grey capsule lies there. The
cursor brackets the corpse's own box (`TryBracket`), and a click that finds no living pawn asks
`CorpseUnderRay` and hands the corpse to `HudDirectors.ChooseCorpse` (lane C's; until it answers yes
the click falls through as before).

**The marks and the words.** `DrawCombatMarks` draws a bar over every pawn
`CombatFeedbackModel.HealthBar` owes one to (track in the panel's ink, fill in `HudTheme` good,
warning or bad by the fraction, along the camera's right) and the hostile marker (the draft's
diamond in `HudTheme.Bad`, brighter than the draft's red) where `HostileMarker` says. The floating
words are `CombatFloaters` (1.2 s, 0.9 m rise, fading over the last 40 %, at most 32) drawn by
`Ui.CombatFloaterView`, a layer inserted **beneath** the HUD's tree in the HUD's own fonts. All
three draw nothing until lane C's model answers. Sounds: `SoundIds.CombatSwing/Hit/Miss/Down/Death`,
named and in no catalogue yet — the director declines a sound it has no clip for.

**Per-frame cost** scales with the live figures (capped at 64), the pawns on drawn layers for the
marks, the events since the last frame (at most the published 32) and the corpses (one visibility
test each, a pose and an evaluate for the few still falling). Nothing walks the board.

**The catalogue.** Nine rows, 30 clips, all resolved: 181 → 190 rows, the appearance block intact
(97 classified rows before and after, `PlayScene.RebuildCatalogue` then `CharacterSwatches.Classify`).
Two files name their clip differently from themselves (`A_Stun_Loop_Sword` holds
`A_Stunned_Loop_Sword`, `A_Death_B_01_Pose_Sword` holds `A_Death_B_Pose_01_Sword`), so a clip is
found by its **file's** name and is that file's one whole clip — the first build lost both.

**Negative controls, each seen to fail** (one run with all seven withheld, then restored): the clip
played at its authored rate (`AClipsImpactLandsOnTheWindupTick`); the watermark not reset on a new
world (`CombatFeedbackHandsEachEventOnOnce…`); the stroke gated on `Working` alone
(`AnAttackingFigurePlaysNoWorkStroke`); the corpse's face dealt from seed 0
(`ACorpseWearsTheFaceItsRollSeedDeals`); the bracket refusing a selected corpse
(`TheCursorBracketsASelectedCorpse`); the computed lie withheld
(`WithThePackAbsentADownedColonistLiesOnTheComputedPose`); a strike read as a gesture
(`AStrikeStartsASwingAndItsEventRefinesIt`). The row test failed on the catalogue before the rows
existed and again on the build that lost two clips.

**Do not undo by tidying.**
- **Action clips are timed by hand at speed nought.** Given a speed, a clip runs on the graph's own
  clock, ignores a pause and drifts off the tick its blow belongs to.
- **The held states come off the flags, the reactions off the events.** A figure leased mid-fight
  has no events to replay, and flags cannot say which side a blow came from.
- **A corpse's face is dealt from the corpse's seed**, never the frame's.
- **A corpse figure is borrowed, baked and returned**; keeping it would put the dead under the
  ceiling and a graph under every body.

**Open, for the integrator and the first playtest.** Whether the pack's sword swings read with a bat
or a crowbar in the hand, and whether a heavy swing at 1.9× reads as a blow or a twitch; every
computed angle; which way `Death_F` actually falls (the turn is measured, so the body lies along the
facing either way, but the fall's direction relative to the attacker is not chosen yet — the loan is
always asked for the front variant); a downed pawn past the figure cap is still drawn standing by the
instanced pass; a downed colonist's click box is still the standing one; the pack-present tests ran
here only, and PlayMode, the frame budget with a fight in view and the player build have not been
run.
