# 33 — Combat: draft, move, melee

**The hostile this document calls the bandit was the marauder until 2026-09-24** (design 42). The
word was renamed everywhere live, this file included; the journal keeps the old word, so an entry
there that says "marauder" means the bandit. Kind index 3, `TraverseMode` 3 and incident index 3
are unchanged. A bandit carries a crowbar or a bat (design 42 §3), not the machete §5b gave it.

**Status (2026-09-24): every unit of the plan is built. C1–C6 and the owner's rounds after them are on
`main` (PRs #176, #180, #182, #194); C7, the gate, is §21 and `docs/milestones/combat-report.md`. What
follows is the history in the order it happened.** C1 (draft and move) built and played 2026-09-23 — the owner's verdict: drafting, T,
moving onto surfaces, the diamond and the four hours all work; the run (§2h) and the deeper red
(§2g) came out of that playtest. C2 (health and melee) and C3 (weapons) built 2026-09-23 by four
parallel lanes on the contracts of §5 (§6A–§6D) and integrated on `claude/combat-c2` (§6E): every
tier green, no golden moved, the frame with a fight in view measured and the player build
smoke-tested. Awaiting the owner's playtest of checkpoints 2 and 3 together. C4–C7 designed, not
built.** The line's plan and its unit status are `docs/plans/combat.md`; the owner's hand-over is
`docs/plans/combat-c2-handover.md`. Research: `docs/research/a-10-melee-combat.md` (the
reference's rules, clean room) and `docs/research/synty-sword-combat.md` (the animation pack).
C1 was branch `claude/combat-mvp` (PR #176, merged); C2 and C3 are branch `claude/combat-c2`,
worktree `D:\code\odyssey-combat`.

## 1. What is being built

The owner's MVP (2026-09-23): *"a colonist can be put into an attack mode, ordered to move to
locations / attack something"* — animals, colonists, buildings and enemies — with melee weapons
from the Synty packs, **starting small and making sure it works great**.

Every decision below was the owner's in the interview of 2026-09-23 unless it says otherwise.

| Topic | Decision |
|---|---|
| Targets | animals (hog, rat), colonists, buildings (walls, doors, furniture), a debug-spawned hostile person |
| Health | one hit-point pool per pawn, by species (person 100, hog 60, rat 15); **downed at 0**, **dead at −50 %** of the pool; no bleeding, no body parts |
| Downed | a colonist heals **only in a bed** and has to be rescued to one; an animal recovers on its own; a bandit stays down until killed; a downed pawn keeps its weapon |
| Rescue | an automatic emergency job (the `ui.work.rescue` column), **and** a drafted colonist ordered to by a right-click; to the patient's own bed, else the nearest free one |
| Death | the corpse stays where it fell, drawn lying in the death pose, clickable as "Corpse of X"; not haulable yet; a dead colonist leaves the roster |
| Retaliation | a hostile always fights; an animal rolls its species' revenge chance on every hit (a hog usually turns, a rat usually runs); a colonist struck by a colonist fights back |
| Friendly fire | the victim remembers being attacked (−8, one day); any colonist's death is felt by every colonist (−6, three days); no opinions yet |
| Drafting | the reference's rules: work stops, the colonist holds its position, needs fall but it will not eat or sleep, it hits a hostile on an adjacent cell by itself, and it undrafts itself after **four in-game hours** with no order and no threat; going down or breaking ends the draft. *Since §15 (2026-09-24): holding, it also joins another colonist's fight within eight cells* |
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
  ones you are commanding are signal. **The bracket is now a ring** (§20, 2026-09-24).

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

## 3. Health and melee (C2, built)

*Built 2026-09-23. This section is the shape; the lane's detail — the rolls, the chase, down and
dead, healing, the minds and the order — is §6A, what is drawn §6B, what the interface says §6C,
and what changed when the four were put together §6E.*

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

**As built, three things this section did not say** (§6A):

- **A fight runs.** `Pawn.UrgencyPerMille` answers the draft's pace for `Job_AttackMelee` and
  `Job_Flee`; at a walk a hunt never caught a colonist walking away from it.
- **A struck colonist fights back against whoever struck her**, colonist or not, for
  `retaliationTicks`; a colonist-only memory let her wander off while being beaten.
- **Death takes the player.** Only the blow that crosses −50 % of the pool kills, a bandit hunts
  only colonists who are standing, and nobody's self-defence strikes a body on the ground — so an
  unordered fight ends in downs, never in corpses. The Long soak, now with a machete in every
  bandit's hand: 241 swings, 122 hits, nine downs, no deaths. A corpse is made by an order on a
  downed pawn (`ToTheDeath`, §6A.8). Recorded for the playtest rather than changed: it follows the
  owner's rules, and whether it feels right is the owner's to say.

## 4. Later units, named

- **C3, weapons (built 2026-09-23, §6D):** an `ItemDef.weapon` block, four weapon items,
  `Pawn.EquippedItem`, the equip job and order, blunt stun (rolled by lane A from lane D's
  armament), drop on death, the Playtest kit (a bat and a machete). **The held prop is not the tool
  code's `GripTool`**: no lane owned it, and the integration drew it as the weapon's own ground row
  under the right hand, seated once by measurement (§6E).
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
| `PawnKindIndex` | 3 `Bandit` (a person, faction `Hostile`) | `Species.xml`, `PawnKindLabels` |
| `ItemHandle` | 7 bat, 8 crowbar, 9 machete, 10 arc blade (category `Weapons`, stack 1) | `Items.xml`, `ItemLabels`, `ModuleIds` |
| `IntentKind` | `OrderAttack`, `OrderEquip`, `OrderRescue` — all apply while paused | `Intents.cs` |
| `PawnGesture` | 4 `Strike` | `Views.cs` |
| `PawnPurpose` | `MeleeHit`, `MeleeDodge`, `MeleeDamage`, `Revenge`, `Stun` | `PawnContent.cs` |
| `icon-keys.csv` | `ui.pawn.bandit`, `ui.status.{fighting,fleeing,equipping,rescuing}`, `ui.item.{bat,crowbar,machete,arcblade}`, `ui.combat.{miss,dodge,stunned,health,dead}`, `ui.debug.spawn{bandit,bat,crowbar,machete,arcblade}` | wiki rebuilt |

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
| `PawnKindDef` | `faction` | `Colony` (default), `Wild` for both animals, `Hostile` for the bandit |
| | `weapon` | an item defName the kind arrives holding: `Item_Machete` for the bandit (owner: "armed"; which weapon INVENTED), empty for the rest. Resolved into `PawnContent.KindWeapon` / `WeaponOf(kind)`; a name that is not a weapon fails the load |
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
- **`Pawn.NeedsTick`** = a colonist who is not downed. A bandit has no needs (it is spawned to
  fight, and a raider that went for the pantry would be a second design); a downed pawn's needs
  pause (the C2 default). `NeedsSystem` asks it and nothing else.
- **A bandit is nobody's to draft**: `SetDrafted` and `OrderMove` ask `IsColonist`, and a downed
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
- **The bandit arrives armed.** `PawnRegistry.Spawn(cell, kind)` calls
  `IWeaponRules.ArmOnSpawn` for a kind whose `weapon` names an item, after the pawn is adopted;
  the loader never does (it restores the hand from the save). Lane D fills it; until then the
  bandit fights with fists.

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
| `InspectModel.ShowsFace`, `ShowsColonistBody`, `ShowsTabBox`, `AvatarKey` | the pane's shape, which `HudShell.Inspect` reads instead of deciding from the subject and `IsAnimal`. Their values reproduce the pane as it was; a bandit and a corpse are lane C's to answer, in the fast tier. `Commands` is drawn for whatever subject the model fills it for | lane C |
| `HudShell.Combat.cs` | the Health tab's body (build, forget, show, sync — all called by `HudShell.Inspect`) | lane C |
| `HudShell.Debug.cs` | the Spawn tab's bandit and weapon rows | lane C |
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
- **`PawnView.Flags`, never the kind, says what a pawn is.** A bandit is kind 3 and a person.
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
  The computed brief said ~20; the Def says 60 and content is frozen, so 60. **Since §7c the walk
  is to a side of the target, not to its cell**, and the re-plan chooses the side again; the
  cadence is unchanged.
- **In reach mid-step**, the step is landed first; at the boundary the pawn stops and swings —
  on a side nobody else holds (§7c).
- **It ends:**
  - when the target is gone or dead;
  - when the target is down — unless the order was given on a pawn already down, which is
    `ToTheDeath` and is how a bandit that "stays down until killed" gets killed;
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
- A **bandit** never heals.

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
| `DraftedThinkNode` (`Draft.cs`) | a threat in reach → the blow (not forced, so never chased), and the four quiet hours start again; else the hold. **The hold itself ends when a threat comes into reach**, since a holding colonist never thinks. *Since §15: or another colonist's fight nearby, which she joins* |
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
    of reach; she found nobody beside her and went back to wandering while the bandit beat her
    down (measured).
- **A bandit** struck by a colonist it is not fighting remembers her for `retaliationTicks`, and
  its hunt prefers her while she stands and can be reached. Chasing somebody else, it is
  interrupted and turns on her at once; **already trading blows with a colonist beside it, it keeps
  to her**, and the hitter is next when she goes down (§6F — the first version interrupted every
  time, which lost the swing in the air and still chose the nearer or lower-id colonist).
- **A drafted colonist** reacts to nothing here: her hold fights.

### 6A.7 The one spine edit: a chase runs

`Pawn.UrgencyPerMille` answers the run (`draftedPacePerMille`, 2,000) for `Job_AttackMelee` and
`Job_Flee`, as well as for the draft. §2h named this seam for exactly that day.

At walking pace a hunt closed on a wandering colonist at the speed she walked away, and in 3,000
ticks on a bare board never reached her (measured). With the edit withheld,
`AColonistStruckByABanditFightsBack` fails. No golden window fights, so no golden moved.

### 6A.8 The order

`OrderAttack(A, B)` is accepted for a drafted, standing colonist of ours against any pawn that
exists, is alive, is not herself, and is within reach or reachable. The target may be an animal, a
bandit, or a colonist.

- **The Ctrl a colonist target needs is the interface's gesture** (`CombatOrders.Route`, lane C).
  The contract gives the intent no argument to carry it. The computed brief asked for "colonist
  only when the intent says forced/Ctrl"; honouring it would need a `C` argument that lane C does
  not send, so it is left to the integrator (open below).
- **`B = 0` stays refused** (C6).
- The order ends the job in hand through `Interrupt`, names the target on the pawn, and starts a
  forced `Job_AttackMelee`. An order given on a pawn already down is `ToTheDeath`.
- **The same order again is `AlreadyInThatState`** — the same target, forced, to the same end — and
  changes nothing (§6F). A confirm-click is sent for every selected drafted colonist, and a restart
  lost the swing in the air while the pawn's clock still waited out its cooldown.

### 6A.9 What it costs

`CombatSystem.Tick` is one pass over the pawns with a branch each, plus the swings landing and the
heals due. The threat scan (`Melee.AdjacentThreat`) is one pass over the pawns per drafted
colonist per tick, and per self-defence think.

`TickBenchmarkTests.TwentyAgainstTwenty` (explicit), 120 × 120 × 16, twenty colonists, one run on
the Windows dev machine, 2026-09-23:

| Case | Tick, mean | Pawns phase, mean |
|---|---|---|
| At peace | 0.034 ms | 0.007 ms |
| Against twenty bandits | 0.069 ms | 0.012 ms |

The fight run resolved 272 swings in its 1,500 ticks.

### 6A.10 Tests (fast tier unless marked)

Each claim was seen to fail with its rule withheld.

| Test file | What it covers |
|---|---|
| `CombatMathTests` | the owner's curves; the rates measured over 4,000 swings; the spread's ends; blunt stuns and sharp never; one stream per roll — failed with a shared stream (measured) |
| `AttackDriverTests` | the chase; the cooldown; a re-order keeps the clock; the wind-up focus; the weapon on every report; the lost stunned swing; the refusals; the hold that never chases — failed with the rule withheld (measured); a save taken mid-swing resumes on the same hash 600 ticks on, with a forgetful load as the control |
| `DownedDeathTests` | the fall, death and the corpse; the corpse's facing |
| `HealingTests` | healing in a bed, and the animal and bandit cases |
| `HostileTests` | the hunt and self-defence |
| `AnimalRevengeTests` | revenge rates, a hog 700 ± 60 ‰ and a rat 50 ± 30 ‰ over 400 blows each |
| **Long:** `BanditSoakTests` | a bandit a day for ten days on the soak's board: invariants at every 500 ticks and a save on day five resumed equal a day later. 9.5 s; 536 swings, all ten bandits down, no deaths with fists |

`CombatFixture` is the shared test fixture. `HeldWeapon` puts a weapon in a hand without lane D.

### 6A.11 Open

- ~~**Proposed for `CombatDef` at the integrator's Def pass:** `rechooseTicks` = 300.~~ Done at the
  integration (§6E).
- ~~**The Ctrl flag on `OrderAttack`** — see §6A.8.~~ Decided at the integration: no flag (§6E).
- **A downed body slides the rest of its step.** The price of keeping the step (§2d): the figure
  lands where it is drawn, rather than snapping back.

## 6B. Lane B — the fight (drawn)

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
getting up ends any one-shot. *Superseded by §9a (2026-09-24): this rule is why the owner saw no
reaction to most blows. A reaction now runs beside the swing and every landed blow is drawn.*

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

## 6C. Lane C — the interface

Built 2026-09-23 on `claude/combat-hud` (from `229b00a0`). Everything below is in `Odyssey.Hud`
and runs in the fast tier except the two Presentation files, which **have never been compiled**
(`HudShell.Combat.cs`, `HudShell.Debug.cs`).

**The right-click** (`CombatOrders.Route`, `CombatOrdersTests`). A pawn under the pointer wins over
the cell it stands in. An animal or a hostile is attacked by every selected drafted colonist, a
downed one included (a bandit stays down until killed). Ctrl on a colonist is an attack by every
selected drafted colonist but her — and **Ctrl wins over the rescue**, being the one gesture that
says "hit this one of ours" outright. A downed colonist is rescued by the **nearest** selected
drafted colonist only (*our call*: one body, one carrier; sending all of them is a walk the
reservation would refuse at the end of). A weapon in the clicked cell **or the one above it** (the
rule a left click selects a pile by) is fetched by the first colonist in the selection, drafted or
not. *(Superseded 2026-09-23: a weapon now opens the context menu, whose Equip row sends the same
order for the same colonist, §7a.)* Everything else is the move, a floored or walled cell included. A downed, hostile or animal
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

- **A bandit** has no face, no colonist body, no tab box, no tabs, no skills and no commands,
  and wears `ui.pawn.bandit`; its line is its job in a person's words ("Fighting"). *Our call*:
  no Health tab means its health is the bar over its head and nowhere else.
- **A corpse** is "Corpse of Wrenn" — the name she wore alive, `ColonistNames.Of` over the seed
  and id the corpse kept, so a player's own name outlives her — or "Corpse of a midden hog" /
  "Corpse of a bandit". The line under it is "Dead · since 07h, day 3 of Larkspur", composed
  once per corpse. A corpse the frame no longer carries says only "Corpse".
- **A pawn that leaves the frame keeps its shape.** An animal (since #167) or a bandit that died
  fell to the colonist's tombstone and grew a Health tab and a Draft button for the grace frames.
- **The Health tab**: "73 / 100" from `hp.max` and `hp` (no `hp` is whole), rounded **up** so a
  colonist on her feet never reads nought; a fill in the bar's colours; then condition (Unhurt,
  Hurt, Stunned, Downed — the flags first) and weapon (the held item, or Bare hands). Five keys
  were added for it: `ui.combat.{unhurt,hurt,condition,weapon,barehands}`.

**Elsewhere.** `HudDirectors.ChooseCorpse` selects only a corpse the frame carries and leaves the
selection alone otherwise. The Spawn tab is `DebugDirector.SpawnRows`, a table the fast tier holds
(the bandit is `SpawnPawn` with kind 3, each weapon `GiveResource` with one item). Alerts and the
Work tab read colonists by the flags; alerts had counted animals toward "is the colony idle" since
#167, which a wandering hog always defeated. The Almanac opens an animal's corpse on its Fauna
entry, and nothing for a person's corpse, a bandit (it opened a colonist's Skills page) or a
weapon (it opened the Ration Pack). No attack colour was added to `OrderColours`: nothing draws an
attack order in a colour of its own yet.

## 6D. Lane D — weapons (C3 simulation)

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
  taken straight up through `WeaponHand.TakeUp`, the same door the equip job uses. A bandit spawned
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
*our call*: a bandit's machete is the colony's the moment it falls, and a line in the listener is
where a forbid would go.

**The starting kit.** `ScenarioDef.startingWeapons`, item defs, **empty by default**, so `Bare` and
every golden are untouched (an empty kit asks the storey search for no more spots). `Playtest` lays a
**bat and a machete** on the ground beside the food, one a cell, in nobody's hand — a blunt and a
sharp, so the first fight shows both a stun and the quicker blade (*INVENTED* inside the owner's "one
or two"). Placement reports them as `Result.Weapons`. A debug-spawned weapon needs nothing here:
`GiveResource` already places any item (lane C's row).

**Scales with** nothing per tick: the equip driver is one pawn and one thing; the listener runs on a
death; the rules are constant-time lookups. No sweep was added.

**Goldens: unchanged** — no golden builds on `Playtest`, spawns a bandit or equips anything.

**One spine edit**: `CombatContractTests.ABanditIsSpawnedThroughTheArmingSeamAndAnAnimalIsNot`
ended by asserting that the stub rules arm nobody. That assertion is exactly what this lane exists
to make false; it now asserts the machete. Nothing else in the file moved.

**Recorded, not fixed:**

- ~~A pawn **despawned without dying** while holding a weapon would leave it held by an id that no
  longer exists.~~ Fixed at the integration: `PawnRegistry.Despawn` puts the hand down (§6E).
- ~~`Job_Equip` has no status word wired in the interface yet.~~ It had one all along:
  `JobLabels.IconKeys` maps job 17 to `ui.status.equipping` since the contracts step.

## 6E. The integration (Phase 3, 2026-09-23)

**Merged in the plan's order** — A (`claude/combat-fight`), D (`claude/combat-weapons`), C
(`claude/combat-hud`), B (`claude/combat-drawn`) — on to `claude/combat-c2` at `229b00a0`. The only
conflicts were the four lanes' subsections of this file, all appended at the end; each is kept
whole and they now read A, B, C, D. The fast tier was green after every merge (Sim 1,118 → 1,159,
Hud 751 → 795), so nothing one lane built broke another.

**What was dangling across the lanes, and is wired now:**

| Seam | Was | Is |
|---|---|---|
| An undrafted colonist's right-click on a weapon | lane C's model sent `OrderEquip` for her (§5j), but lane B's `SelectionPresenter.Order` returned first unless somebody was drafted | the presenter's gate is `OrderModel.HearsRightClick`; everything else still needs a draft inside the model |
| How long a floating word stays up | lane B floated every word 1.2 s; lane C had written `FloatingSeconds` | each floater carries its own life from the model: a number 1.2 s, "Downed" and "Dead" 2.2 s |
| The health bar's colour | two ladders — lane B's 60/30 % and lane C's 60/40 % (the need bar's) — one rule, two owners (`bug-patterns.md` P1) | the bar asks `CombatFeedbackModel.HealthBarColour`; `CombatMarks.BarInk` is gone |
| The weapon in the hand, and on the ground | **nobody's**: lane D published `odyssey.pawn.weapon`, lane B chose the swing's clip family from it, nothing drew it; a dropped machete was the orange stand-in box | four catalogue rows (Battle Royale's bat, crowbar and machete; Sci-Fi City's sword for the arc blade), `ModuleEntry.lieFlat` to lay a prop modelled standing on its broadest face, and `PawnFigureDirector.Weapons.cs`, which puts the same row under the right hand, seated once and shown only while the hand is free |
| A pawn leaving the board holding a weapon | recorded by lane D, not fixed (spine) | `PawnRegistry.Despawn` puts the hand down beside the beds it already releases |
| `rechooseTicks` | a constant on the attack driver, proposed for the Def | `CombatDef.rechooseTicks` = 300 in `Combat.xml`; fingerprint moved (nineteenth), `AHuntThinksAgainAfterTheContentsRechooseTicks` pins it |

**The Ctrl flag on `OrderAttack`: decided, no flag.** Lane A accepts an attack on a colonist
without one; lane C sends one only when Ctrl is held. The order is the player's act either way —
nothing but the interface sends it — so the gesture belongs where the gesture is read, and a `C`
argument would be a second copy of a rule the right-click already owns. A future caller that is not
the right-click (a context menu, C5) goes through `CombatOrders.Route` or states its own reason.

**The weapon's grip is measured, not authored**, the tool code's approach cut down: the long axis
is the haft, the end the mass leans to is the business end (every Synty weapon pivots at the grip),
the haft continues the forearm, the flat of the blade turns to the figure's front, and the butt
sits a tenth of the length into the palm (`WeaponGripFraction`, INVENTED). It is fitted once, when
the pawn's weapon changes, and rides the hand bone as a child after that — nothing re-seats it per
frame, so nothing can wind (`PlaceTool`'s lesson). It is hidden while a tool is in the hand, a load
in the arms, or the body is lying down; a downed pawn keeps its weapon in the simulation but is
drawn without it. **Nobody has looked at it**: whether the grip, the blade's turn and the size read
right is a contact sheet or the playtest.

**Unity's NUnit refused one of lane D's asserts.** `Does.Not.Contain(int)` compiles only against
the fast tier's newer NUnit; the editor rejected the whole test assembly. `Has.No.Member` says the
same in both (`docs/lessons.md`, the two NUnits).

**Measured:**

- **Tiers.** Fast: Sim 1,161, Hud 795, 0 failed. Long: 39, 0 failed. EditMode: 2,823 total,
  2,797 passed, 0 failed (17 skipped, 9 inconclusive) — lane C's two never-compiled files compiled
  first time. PlayMode, alone on the machine: 106 total, 101 passed, 0 failed, 5 skipped. Both
  content gates pass.
- **Goldens: none moved.** `Golden.cs` is byte-identical to `229b00a0`, and `GoldenColonyProbe` run
  on `229b00a0` and on this branch prints the same nine lines for all three colonies. Lane D's
  starting kit did not need its exception: no golden builds on `Playtest`.
- **The frame with a fight in view** (`FrameTimeTests.TheFrameWithAFightInView`, one run, RTX 5070
  Ti at 640 × 480): fifteen colonists at peace **2.06 ms** (1,127 draw calls), the same with ten
  bandits among them **2.29 ms** (1,139); the figures section 0.101 → 0.191 ms, the overlays 0.007
  → 0.016. The fight costs about a tenth of a millisecond of figures and a dozen draws. Not at a
  play resolution and not on the target laptop, like every number in this project.
- **The player build** (`scripts/unity.sh build`, then `Odyssey.exe -odyssey-newgame`): built, booted
  into a colony and ran 45 s with no error or exception in the log. The only warnings are the
  stylesheet's three unknown pseudo-classes, which predate combat. A new game has no hostile in it,
  so the smoke did not play a clip; the clips ship because the catalogue references them.
- **The Long soak with weapons**: 241 swings, 122 hits, nine downs, no deaths (see §3).

**Still open:** the combat sounds have no clips (five named ids, no catalogue rows, so a fight is
silent); every computed pose angle, the floating words' lifetimes and colours, and the weapon's
grip are INVENTED and unseen; a downed pawn past the 64-figure cap is drawn standing, and a downed
colonist's click box is the standing one; a corpse always falls through the front death variant;
swapping weapons draws no put-down of the old one; `PawnKindLabels.Bandit = 3` is a Hud copy of
`PawnKindIndex.Bandit` with nothing holding the two together.

## 6F. The review (2026-09-23)

Two reviewers read the integrated branch and reported eight faults. All eight were real; each is
fixed with a test that was seen to fail without the fix. **No golden moved** — no golden window
fights — and no save format changed: a bandit's retaliation lives in the fields a colonist's
already uses, which were saved and hashed.

| Fault | What the player saw | Fixed | Test |
|---|---|---|---|
| A bandit struck by a second colonist was interrupted, then re-chose the nearest — a tie to the lower id | the swing it had wound up vanished, it waited a whole cooldown, and two colonists could keep it from landing anything; it seldom turned on the one hitting it | `CombatSystem.React` records the hitter on the bandit for `retaliationTicks`; `HostileThinkNode` prefers her; no interrupt while it fights somebody beside it (§6A.6) | `HostileTests.ABanditChasingSomebodyElseTurnsOnTheColonistWhoHitsIt`, `…InAFightKeepsItsSwingWhenASecondColonistHitsIt` |
| The same attack order again restarted the job | clicking a target again faster than a wind-up stopped a drafted colonist landing any blow | `AlreadyInThatState` for the order already in hand (§6A.8) | `AttackDriverTests.ARepeatedAttackOrderIsQuietAndKeepsTheSwingInTheAir` |
| The teardown disposed the figures before the corpses | a death on screen, pause, then Load / New game / Leave to menu threw, and a load left a half-built session | the corpses go first; `ReturnCorpse` lets go of a loan whose director is gone | `CorpseTeardownTests` (PlayMode), `CombatDrawnTests.AFallCutShortByATeardownHandsItsFigureBackQuietly` |
| A pawn killed while downed played its whole fall from standing | finishing a downed bandit stood the body up and knocked it over again | `CorpseDirector` keeps last frame's downed pawns and bakes theirs lying at once | `CombatDrawnTests.APawnKilledWhileDownIsFoundLyingAndOneKilledStandingFalls` |
| The corpse pane's cache outlived the selection | click a corpse, a colonist, the same corpse: the corpse wore her name and job | every other subject clears it | `CombatPaneTests.ACorpseChosenAgainAfterSomethingElseIsNamedAgain` |
| A falling body ignored the slice, and one baked while hidden measured an empty box | a body fell in view on a layer not drawn; afterwards a click missed it and the cursor bracketed the world's origin | the lent figure's renderers are forced off with its layer; the box is measured before the body is hidden (the empty box did **not** reproduce in this Unity — measured — so that half is the safe order, not a proven fault) | `CombatDrawnTests.ABodyFallingOnAHiddenLayerIsHiddenAndIsFoundWhereItLies` |
| The floating words ignored the slice | *-7* and *Miss* floated over the grass above a fight in a cave | a word floats only for a fight on a drawn layer, the bars' rule; figures and sound still take every event | `CombatDrawnTests.AFightOffTheDrawnLayersFloatsNoWords` |
| The pane wrote *colonist*, *hostile*, *animal* as literals | nothing yet; renaming a kind in `icon-keys.csv` would have left the pane on the old word | `Registry.Label(ui.pawn.*)` lower-cased, once, on the living and the corpse pane alike | `RegistryTests.TheInspectPaneWritesNoPawnKindItself` |

**Why a bandit in a fight keeps to it.** The reviewer offered two fixes: turn on every hitter, or
leave a swing in the air alone. Turning on every hitter makes two colonists either side of a
bandit swap its target on every blow, and each swap lost a swing — the fault again, by another
road. So the hitter is remembered, a chase is abandoned for her, and a fight beside somebody is not.
The attack in reach never re-thinks (§6A.2), so a bandit holds to the colonist in front of it
until she goes down, and then the hitter is next rather than whoever is nearest.

**A downed pawn's death is read in presentation, not saved.** The corpse could have carried a
"was down" bit, but it would be saved and hashed state whose only reader is a two-second fall that
a load never plays. `CorpseDirector` reads it off the frame before — one flag test per pawn a frame.

**`RegistryTests` polices six namespaces, case-sensitively, and still does.** Extending it to
`ui.pawn.*` would have missed the pane's lower-cased words and caught a GameObject named "Corpse"
and a USS class "colonist". The new test reads `InspectModel.cs` alone, ignoring case.

## 7. The C2+C3 playtest, and the round after it (2026-09-23)

**The owner's verdict:** *"It's mostly pretty decent."* Four asks came back, and a second interview
settled each one. They are built on `claude/combat-c2-polish` from `50ced466`.

| Ask | Decision (owner) | Section |
|---|---|---|
| *"When picking up the weapon it wasn't clear"* | **A context menu on things.** Right-click a weapon opens a small menu at the pointer: *Equip <weapon>* and *Cancel*. It is the forced-order menu `15-building.md` §8 reserved the gesture for, and later *Rescue*, *Build this now* and the like join it on their own targets. Right-click on bare ground stays an instant move, with no menu. Once ordered, the colonist shows the order line to the weapon, and *Equipping* on the activity line. | §7a |
| *"When I right click to attack an enemy it wasn't clear"* | **Right-clicking an enemy attacks at once, with a lock-on ring.** An enemy has one sensible order, so it takes one click, and the menu is for things with several. A translucent red ring appears at 1.6× the target's footprint and snaps onto its feet in about 0.2 s (ease-out). It flashes once as it lands, then stays as a faint ring under the target while the attack order holds. It fades when the target goes down or dies, or the order changes. | §7b |
| *"2 colonists attacking within the same tile ... should position themselves side by side"* | **Each takes the nearest free side.** Every attacker claims a different cell next to the target, the free one nearest to it, so two arriving from the west stand side by side on the west flank. If all eight cells are taken, the extra waits one ring back. No two attackers ever share a tile. | §7c |
| *"We also need a blood effect ... even better blood splatter"* | **The seam is cut now; blood is built as the next unit.** Every landed hit spurts, scaled: sharp hits (machete, arc blade, bites) spurt more and leave a splatter, blunt hits (bat, crowbar, fists) a smaller puff and a smaller mark. Misses and dodges draw nothing. Downs and deaths leave a pool under the body. Ground marks fade over about one in-game day, capped (around 200, oldest first). They are **presentation only**: not saved, not simulated, nothing to clean. | §7d |

### 7a. The context menu

Built 2026-09-23 on `claude/combat-menu` from `claude/combat-c2-polish` (`10a61b00`). The owner's
report was that picking up a weapon "wasn't clear": a right-click on a machete sent the colonist
for it at once, and nothing said that was what the click had done. The decision: **a right-click
on a thing with more than one sensible answer opens a small menu at the pointer**, and a thing with
one answer keeps acting at once.

**What opens it and what does not.** `OrderModel.RightClick` asks three questions in order and
answers with **either** orders **or** menu rows, never both:

1. **The pawn under the pointer** (`CombatOrders.Route`, unchanged): an animal or a hostile is an
   instant attack by every selected drafted colonist; Ctrl on a colonist is an attack; a downed
   colonist is an instant rescue by the nearest. An enemy standing on a weapon is still attacked —
   the pawn wins over the cell, as it always has.
2. **A thing with a choice** (`ContextMenuModel.Build`): a weapon lying in the clicked cell or the
   one above it (the pick's rule for piles), **or held in a store there**, offers one
   *Equip &lt;weapon&gt;* row per kind of weapon — a shelf of two bats and an arc blade is "Equip bat",
   "Equip arc blade" — and *Cancel* last.
3. **Everything else** is the move it was in C1, for the drafted only: bare ground, a pile of wood,
   a floor, a wall. No menu.

**The rows** (`ContextMenuRow`): the registry key of the verb, the whole line ("Equip machete" —
`ui.command.equip` and the item's own name lower-cased, the pane's "Corpse of a midden hog" rule),
whether it can be chosen, why not, and the intents it sends. Equip sends `OrderEquip` for the
**primary colonist** — the first *standing* colonist in the selection, drafted or not (§5j), passing
over an animal, a bandit or a downed colonist ahead of her — aimed at the weapon's own cell.
A selection whose every colonist is down gets the row **disabled, reason "Downed"**
(`ui.status.downed`), so the player sees why. A selection with **no colonist at all** — an animal, a
bandit — gets **no menu**: they take no orders (the presenter's gate, `HearsRightClick`, never
asks), and a menu of one Cancel says nothing. `ContextMenuModel.Choose` is the one door from a row to
the world and sends nothing for Cancel or a disabled row, whatever the row carries — so a view that
forgets to look at `Enabled` still cannot send one. One new key: `ui.menu.cancel`, in a new
`ui.menu` namespace listed on the wiki's Commands page.

**Closing** (owner): Escape — the new top rung of `SettingsDirector.Escape`, `CloseContextMenu`,
appended to the enum so no value moved — any mouse press outside the menu, the camera turning more
than 5° (a click that did not travel can add a degree or so of yaw while the button is down), and a
change of selection, since the rows name that selection's primary. Also a new session, and
`CloseMenusOverTheBoard`, so the rule "one panel over the board" holds for it too. A right-click
elsewhere is a press outside, so it closes this menu and opens its own on the release. A disabled
row, clicked, does nothing and leaves the menu up so its reason can still be read.

**The view** (`HudShell.ContextMenu.cs`): a `.panel` built on first use — like the bed picker, so the
smoke test's list of regions the shell builds at start is unchanged — in `_worldUi`, so it goes with
the colony. **Its width is the stylesheet's** (`.ctxmenu { min-width: 168px }`), never written from
code, so the border-box trap (CLAUDE.md, "A panel that sets its own width") cannot happen here; its
left and top are written from code, because only the pointer knows them, and
`HudLayout.ContextMenuLeft`/`Top` turn it to the pointer's other side at the right and bottom edges.
It is placed again on `GeometryChangedEvent`, the bed picker's lesson that a panel shown this frame
has no size. Rows are text only, in the `Row` role; the reason is `Meta` in the dim ink after a
disabled row's words; Cancel sits under a divider. No glyph and no non-ASCII character.

**Feedback after ordering.** The equipping colonist shows **the drafted order line to the weapon**
and *Equipping* on her activity line. The line needed one simulation change: the order cell
(`odyssey.pawn.order.cell`) was published only for a drafted colonist's move, and is now published
for a forced `Job_Equip` too, **drafted or not**, still straight after the drafted row when there is
one (`PawnRegistry.OrderCellOf`). `OrderModel.CollectDrafted` gained an overload that, in the same
single walk of the aspects, collects an order cell with no drafted row before it; `DrawDraftMarks`
draws those with the same line and bracket, without the diamond, which says "drafted". An aspect is
neither saved nor hashed, so **no golden moved**. *Equipping* was already there: `JobLabels` has
mapped `Job_Equip` to `ui.status.equipping` since the contracts step (§6D).

**What stayed where it was, and why.** Ctrl + right-click on a colonist and the rescue are
**unchanged** (instant, `CombatOrders.Route`). Moving the rescue into the menu is one `Offer…`
method and one key, but it turns a played one-click order into two, which is a decision for the
keyboard and not for tidying. *Build this now* on a site is also one method, with one catch: the
legality it needs is `JobSystem.CanForce`, a simulation query the Hud assembly cannot call, so it
will need that answer published (or asked through an intent with a reply) before the row can be
honest about being disabled. `15-building.md` §8 step 4 is otherwise built by this.

**Tests** (fast tier, each seen to fail with the thing it guards removed): `ContextMenuModelTests`
(13) — the menu for an undrafted and a drafted selection, Equip's intent for the primary, a stale
selection's primary, Cancel sends nothing, the downed selection's disabled row and its reason, no
menu for an animal or a bandit, the block under a weapon, a store's one row per kind, ground is a
move with no menu, an enemy is an attack with no menu even standing on a weapon, `Choose` refusing a
disabled row that carries orders, Escape's top rung, and the label's two registry words;
`HudLayoutTests.TheContextMenuOpensAtThePointerAndTurnsAtTheEdges`;
`OrderModelTests.AnUndraftedColonistsOrderIsCollectedForItsLineAndADraftedOneIsNot`;
`EquipTests.TheWeaponsCellIsPublishedAsTheOrderCellWhileSheFetchesIt` (drafted and not); and five
colour rows in `HudStyleSheetTests`. The weapon cases in `CombatOrdersTests` moved here, and its
helper now asserts that a click which acts opens no menu.

**Never compiled** (the fast tier does not build Presentation, and Unity was not run):
`HudShell.ContextMenu.cs`, and the edits to `HudShell.cs`, `HudShell.Panels.cs`,
`HudShell.Start.cs`, `SelectionPresenter.cs`, `SettingsPresenter.cs` and `OdysseyBootstrap.cs`.

**Open:** a weapon taken by somebody else while the menu is up leaves the row in place (the
simulation refuses the order quietly); a colonist in a mental break is offered Equip enabled and
refused by the simulation, because the break is not in the view's flags; an unreachable weapon is
offered and refused the same way. Nobody has seen the menu.

### 7b. The lock-on ring

**Built 2026-09-23 on `claude/combat-ring`**, from `claude/combat-c2-polish`. The owner's words:
*"when I right click to attack an enemy it wasn't clear ... paints a red transparent circle quickly
around the selected enemy to indicate that target"*; the decision was lock-on (§7).

**What is drawn.** A flat, translucent red ring under the target. It appears at **1.6×** the
target's footprint and closes on to its feet in **0.2 s** with a cubic ease-out (the owner's two
numbers), flashing brightest on the instant it lands, then settles over 0.18 s to a faint ring that
stays under the target while the order holds, and fades over 0.25 s when it stops holding. Cubic
rather than a back-out, because a back-out overshoots inside the feet and a ring smaller than its
target reads as the target shrinking. Every number but the 1.6 and the 0.2 is INVENTED
(`LockOnRing`: the flash, the hold's opacity 0.35, the arrival's 0.5, the fade).

**What starts one.** The frame a **selected, drafted** colonist's published target
(`odyssey.pawn.order.target`, `Pawn.CombatTarget`) changes to that pawn — read off the snapshot, not
the right-click, so a refused order publishes nothing and draws nothing, and the same order clicked
again (`AlreadyInThatState`) is quiet here too. **At most one ring per target**: a squad sent at one
bandit shares it, and a second order restarts the snap only once the first has settled, so one
click on a box selection locks on once. An order already under way when the player first sees it —
a colonist selected mid-fight, a save loaded mid-fight — is **adopted at rest**, not snapped.

**What ends one.** The target goes down (unless the order was given on a pawn already down, to
finish it — that holds until it dies), dies (a corpse is not a pawn, so it leaves the frame), the
order changes, or the attacker is deselected. The ring remembers where its target stood for the
fade, so a death's ring fades where the body fell.

**Also drawn, by the same rule:** a drafted colonist's own blow at a threat beside her (§2b)
publishes the same target, so a selected colonist who engages without an order wears a ring under
what she is hitting. That is the aspect's meaning — *whom she is attacking* — and was kept rather
than filtered, because the aspect does not say whose idea the fight was and a second aspect for it
would be sim state bought for a colour.

**The colour: `OrderColours.Attack`, `#f0282c`.** A clear saturated red that is neither red already
on the board: not the draft's deep `#8b1212` (which marks *who* is under orders, over the head —
the ring marks *whom*, under the feet, and both are on screen in every fight), and not
`HudTheme.Bad`'s salmon `#e06a5c`, which is the Cancel tool and also the hostile marker over the
very bandit the ring is under — a ring in the marker's colour would read as more marker.
`OrderColoursTests.TheAttackRedIsNeitherTheDraftNorTheCancelRed` holds it 80 points from both and
from every order hue, the board's own threshold (149 from the draft, 130 from the salmon).

**How it is drawn.** `PrimitiveMeshes.UnitRing`, a flat annulus of 48 segments, outer radius 1 m
and inner 0.85, built once from `LockOnRing.RingVertex` and `RingTriangles` so the fast tier checks
it faces up. `ChunkRenderer.DrawRing` draws it in the bracket material — lit, translucent, steady
at night — **one submission per ring**. Placed with `GroundRelief.Drape` at the target's feet for
the relief's tilt and the figure's own height (lifted on to the same ground), 2 cm clear. The
radius at rest is half the longer side of the target's box: an animal's drawn box
(`PawnFigureDirector.TryGetAnimalBox`, centred on the box), a person's the colonist cursor's
1.15 m (0.575 m), a downed person 1.0 m. The opacity is quantised to 32 steps, because the bracket
material is cached per colour and an alpha free to take any value would mint a material per frame
of the animation; quantised, the ring's hue costs at most 33 materials, once each. A target on a
layer the slice does not draw draws no ring — the health bars' rule.

**Where.** `Hud/LockOnRing.cs` (the clock and the mesh's shape), `Hud/LockOnRings.cs` (which
targets, on which frame), `OdysseyBootstrap.DrawLockOnRings` (where each stands; called after the
draft marks), `ChunkRenderer.DrawRing`, `PrimitiveMeshes.UnitRing`.

**Tests** (fast tier, `LockOnRingTests`, 20; `OrderColoursTests`, one new; EditMode
`PrimitiveMeshTests.TheRingFacesUp`, never run). Negative controls, each seen to fail and restored:
an ease-in (`TheSnapEasesOutAndNeverOvershoots`, `TheRingStartsOnTheFrameTheTargetIsPublished`);
no flash (`ItFlashesOnceAsItLandsAndThenHoldsFaint`); no fade (`AReleasedRingFadesToNothing…`,
`ARingReleasedMidSnap…`); the band wound downwards (`TheRingFacesUp`); the attack red set to a
darker draft red and to a near-salmon (`TheAttackRedIsNeither…`); a loaded world snapped
(`AnOrderAlreadyUnderWayIsAdopted…`); an undrafted colonist's target counted
(`AnUndraftedColonistsTargetIsNotAnOrder`); a down that kept the ring
(`TheRingFadesWhenTheTargetGoesDown…`); every second order restarting the snap
(`TwoAttackersShareOneRing`).

**Cost.** Per frame, one indexed aspect lookup per selected pawn, a walk of the rings (one per
target, a handful), a figure lookup and one submission per ring. It scales with the selection and
the fight, never with the board; nothing allocates once the two memories and the place cache have
grown.

**Do not undo by tidying.**
- **The ring starts off the frame, not the key.** Starting it on the click draws a lock-on for an
  order the simulation refused.
- **The alpha is quantised.** See above; it is what keeps the material cache bounded.
- **An order already under way is adopted, not snapped.** A load is not an order.

**Open, for the playtest.** Every INVENTED number, and whether the flash reads as a lock or a blink.
A downed colonist's ring is a guessed 1.0 m round the figure's root, which may not be the body's
middle. On a terrace ramp the ring lies at the figure's height with the relief's tilt, not the
ramp's, so half of it may sink into the slope; in water it floats at the swimmer's feet. **Never
compiled here**: `OdysseyBootstrap.cs`, `ChunkRenderer.cs`, `PrimitiveMeshes.cs`,
`PrimitiveMeshTests.cs` — the integrator compiles.

### 7c. Side by side (built 2026-09-23, `claude/combat-pos`)

Simulation only, fast and Long tiers, no Unity. **No golden moved, no save format changed, no new
state**: no golden window fights, and a side is read off fields every pawn already saves and hashes.

**What an attacker does.** Every `Job_AttackMelee` — a drafted colonist under orders, the
self-defence, the hunt, an animal's revenge — walks to a **side** of its target rather than to its
cell. `Melee.ChooseSide` picks it:

1. Of the eight cells beside the target on its layer that she could strike it from (`IsLegalStep`
   into the target — `Melee.InReach`'s own test, so no blow through a wall's corner) and can reach,
   the one **no other attacker holds, nearest her**. Nearest is the squared distance in cells from
   where she stands; a tie goes to the first in a fixed scan, −Z to +Z then −X to +X. So two
   arriving from the west stand on the west flank, side by side.
2. All eight held: the nearest free cell **one ring back** (Chebyshev distance 2) that she can stand
   on and reach. She waits there, out of reach, and looks again every `chaseRepathTicks` (60), so a
   side that frees up is taken within 60 ticks.
3. That ring full too (24 attackers on one target, or a corridor): she waits where she is and looks
   again at the same cadence.

**A side is held by walking to it or standing on it** — `Melee.SideOf`: the pawn's `Destination`,
else its `Cell`. Only a pawn in an attack on its feet holds one (`Melee.IsInAnAttack`), on **any**
target, so a scrum of two fights does not stack either. Nothing new is saved, and a load holds every
claim it saved. The claims are read in the order the pawns tick — by id — and never out of a
dictionary; the mask round the target is a 5 × 5 `int`.

**Where she stops.** In reach at a step boundary she stops and swings only on a side of her own
(`AttackMeleeJobDriver.MayFightFrom`): never on the target's own cell, never on a side another
attacker holds. So walking past a side somebody else is making for, she walks on. In reach on an
unheld cell she stops there, as she always did — for her it is the nearest free side.

**When she chooses again** — the chase's own re-plan (§6A.2), same cadence. `Job.TargetCell` now
also means, while approaching, *the target's cell the side was chosen against*:

- walking: at a step boundary, once the target has left that cell, no oftener than 60 ticks;
- not walking: at once when the target has left it or the attack is new (`Job.WorkTicks == 0`),
  else every 60 ticks — which is the waiter's look for a freed side.

**The drafted hold is exempt.** Her blow at an adjacent threat still strikes from wherever she
stands and never chases (§6A.2); her cell is her side, so others avoid it. Two drafted colonists
*ordered* onto one target are forced attacks and spread like anybody.

**What it costs.** `ChooseSide` is one pass over the pawns (an integer comparison each for those
not fighting) plus the 24 cells within two of the target — never the board — and is asked at the
chase cadence, not per tick. `MayFightFrom`'s pass over the pawns is asked at a step boundary in
reach while walking, and before each swing; one standing on her side waiting out her swing clock is
not asked. `TickBenchmarkTests.TwentyAgainstTwenty`, one session, Windows dev machine, before and
after:

| | Tick, mean | Pawns phase, mean | Swings in the window |
|---|---|---|---|
| Before §7c | 0.069 ms | 0.013 ms | 203 |
| After §7c | 0.070–0.073 ms | 0.018–0.019 ms | 208 |

About 5 µs a tick on the pawns phase for forty pawns fighting; the tick is inside its own noise.

**Tests** (`SideBySideTests`, fast tier). Every one failed with the driver withheld (measured, with
the per-tick check both on and off, so the end-state assertions were seen to fail on their own):
two from one side end side by side on that flank (before: one tile); nine on one fill the eight
sides and the ninth waits at distance 2 (before: three tiles among nine); the waiter takes a side
that frees up; a target sent ten cells away is surrounded again where it stops (before: all four on
one tile); three bandits hunting one colonist stand on three sides of her; a save taken while
five close resumes on an equal hash 600 ticks on. Every tick of every fight asserts no two attackers
standing on one tile and no two holding one side. Sim fast tier 1,170 (from 1,164), Hud 797, Long 39,
all green; `GoldenMasterTests` green without a re-bake.

**Do not undo by tidying:**

- **The side is `Destination`, else `Cell`.** A reservation or a claim table would be new saved,
  hashed state for something the pawn already carries; a claim on the job alone would miss the
  hold, whose side is simply where she stands.
- **"Nearest" is to the attacker, not to the target.** Nearest the target puts the second arrival on
  whichever orthogonal comes first in the scan, not on the flank she came from.
- **Not asking `MayFightFrom` per tick of a standing attacker.** Nobody else ever chooses a cell
  somebody stands on, so her side stays hers; asking again each tick would be a pass over the pawns
  per attacker per tick for an answer that cannot change.
- **The hold stays exempt.** A drafted colonist who stepped off her cell to find a side would be
  chasing, and the hold never chases.

**Open.** A pawn landing the step an order interrupted (`FinishingStepTo`) runs no job until it
lands, so for those few ticks it holds no side and may pass over one; nothing stops on it. A pawn
walking to a side does not notice another taking it until she arrives, where she chooses again —
which only the hold can cause, by striking from a cell someone was making for.

### 7d. The blood seam

**The owner's decisions (2026-09-23), exactly as §7 records them:** *the seam is cut now; blood is
built as the next unit.* Every landed hit spurts, scaled: **sharp** hits (machete, arc blade,
bites) spurt more and leave a splatter, **blunt** hits (bat, crowbar, fists) a smaller puff and a
smaller mark. **Misses and dodges draw nothing.** **Downs and deaths leave a pool under the body.**
Ground marks **fade over about one in-game day**, **capped (around 200, oldest first)**. They are
**presentation only**: not saved, not simulated, nothing to clean.

**The seam, built 2026-09-23 on `claude/combat-ring`; nothing visible.**

- `Presentation/World/IBloodEffects.cs`: `Spurt(Vector3 at, Vector3 direction, float amount, bool
  sharp)` for a landed hit — `at` the wound (the struck pawn's feet plus 1.3 m for a person, 0.25 m
  lying down, 0.6 of an animal's drawn box), `direction` the blow's travel, horizontal and
  normalised (zero if the attacker could not be placed), `amount` the damage in whole points;
  `Pool(Vector3 at, float sizeFactor)` for a down or a death, `at` the feet; and `Clear()` when the
  world changes. The default is `NoBloodEffects.Instance`, which does nothing.
- `CombatFeedback` calls it from `Handle`, for every event it hands on, **on every layer** — a
  mark on the ground must exist when the player later looks at that layer — and clears it on a
  world change and at teardown. `CombatFeedback.Blood` is the setter the blood unit uses.
- **Which events bleed** is `Hud/BloodModel.For`: `Hit` spurts; `Downed` and `Died` pool (a down
  0.6 of a death's size, INVENTED); `Swing`, `Miss`, `Dodge`, `Stun` and `Recovered` nothing. A stun
  is reported beside the hit that caused it, which already spurted.
- **Sharp or blunt** is `Hud/BloodSides.IsSharp`, resolved in the order the simulation arms a pawn
  (`WeaponRules.ArmamentOf`): the event's weapon if it is an item with an attack, else the
  attacker's species' natural attack, else fists. The table is read once off the content
  (`CombatFeedback.BloodSidesOf`, set by the bootstrap beside the weapon styles), so **the Defs stay
  the one owner** of which weapon cuts: `damageKind` in `Items.xml`, `Species.xml` and
  `Combat.xml`'s fists.

**One disagreement, for the owner.** The owner's list puts *bites* with the sharp hits. The rat's
bite is `Sharp` in `Species.xml`, but **the hog's is `Blunt`** — its comment calls it *tusks*, a
heavy animal that hits hard and slowly. The seam reads the content, so a hog's hit will draw the
**blunt** puff. Recommendation: keep it — a hog's tusks goring and battering are a blunt wound, and
the content is the one owner — but it is one line in `Species.xml` if the owner means every animal
bite (sharp has no stun, and the hog has no stun chance, so the fight itself would not change; the
content fingerprint would).

**What the next unit must build**, and the rules it inherits:

1. **Spurt particles** in the direction of the blow, scaled by `amount` and by sharp against
   blunt — sharp more, and a splatter on the ground; blunt a smaller puff and a smaller mark.
2. **Ground splatter decals** where the spurt lands, and a **pool** under a downed or dead body,
   sized by `sizeFactor` and by the body (an animal's drawn box; a rat's pool is not a person's).
3. **Fading over about one in-game day** (60,000 ticks), timed by the **simulation's tick**, not
   real time: a pause holds the marks and speed three fades them three times as fast.
4. **Capped at about 200 marks, oldest first**, drawn **instanced by material** — never one
   submission per mark (`bug-patterns.md` P10) — and a per-frame cost that scales with the marks,
   never the board.
5. **Presentation only**: nothing in a cell, a save or the hash; a load starts with no blood
   (`Clear`), and nothing in the simulation hears of it.
6. **The slice**: a mark on a layer not drawn is hidden, as the corpses are; a mark on a floor that
   is later removed should go with it or fall, which is the unit's own question.
7. **A death's pool is placed at the event's cell**, because the pawn has left the frame by then;
   the corpse director knows where the body's middle lies (`CorpseDirector`), and the pool should
   sit there.

**Tests.** Fast tier `BloodModelTests` (5): every landed hit spurts and nothing else does, a down
and a death pool and a death's is larger, the weapon in the hand decides, then the species, then
fists, and the resolution is total. Negative controls seen to fail: the weapon ignored
(`TheWeaponInTheHandDecides`), a miss that bled (`EveryLandedHitSpurtsAndNothingElseDoes`). EditMode
`BloodSeamTests` (3, **never compiled or run here**): a recorder behind the seam hears three spurts
(sharp, blunt, fists) travelling along the blow, two pools, and a clear on each world change; the
real content resolves the bat and crowbar blunt, the machete and arc blade sharp, fists blunt, the
rat sharp and the hog blunt. **Never compiled here**: `CombatFeedback.cs`, `IBloodEffects.cs`,
`BloodSeamTests.cs`.

## 8. The second playtest's round (2026-09-23)

The owner played `claude/combat-c2` (`50ced466`), **not** the polish branch: their editor was open
on `D:\code\odyssey-combat`. That is why the context menu did not appear and why enemies still shared
tiles. Neither was a fault in §7a or §7c. The asks that stand on their own:

| Ask | Decision (owner, interviewed 2026-09-23) | Section |
|---|---|---|
| *"The bar above their heads flicker ... use a green like the one used in the colony stats — more greener — deeper colours please and more prominent"* | Find and fix the flicker, measured rather than guessed. The bar's green is the colony-stat green, deeper and more saturated, as are the amber and red. The bar is thicker and has a dark backing, so it reads at the play camera. | §8a |
| *"The weapon is not drawn until the attack is about to happen ... bandits always have their weapons drawn ... we need a good mechanism"* | **Sheathed at the left hip**, where it can be seen. **Drawn** when a colonist is drafted, when its attack target is within 2 tiles, or when it is struck and fights back. The pack's *Draw* clip moves the weapon to the right hand. About 2 s after the fight ends, or on release from the draft, the *Sheathe* clip puts it back. Without the pack it snaps between the two. **Bandits always have theirs drawn.** A tool (axe, pick, hammer) still takes the right hand while a colonist works, and the weapon stays at the hip. | §8b |
| *"Make it a guard that enemies when sharing tiles going side by side as well or handled uniformly"* | One rule for every pawn in a fight: nobody fighting shares a tile. There is a test that walks every tick of mixed brawls (colonists, bandits, hogs, rats, any side) and fails on a shared tile. The drawn crowd sidestep reads the published *person* flag, not "kind 0", so bandits step round each other and round colonists as colonists do. Animals stay outside it (design 29). | §8c |

### 8a. The bar

**Built 2026-09-23 on `claude/combat-bar`**, from `claude/combat-c2-polish`. The owner's words:
*"The bar above their heads flicker ... use a green like the one used in the colony stats — more
greener — deeper colours please and more prominent"*.

**The flicker's cause: two translucent boxes in one place, ordered by the sort.** The bar was a
fill box (0.11 m) drawn *inside* a track box (0.09 m), both in the bracket's translucent material,
which writes no depth. So which one covered the other depended only on the order they were drawn
in, and Unity orders translucent draws by the distance from the camera to each one's bounds
centre. That key cannot order these two:

- **A full bar puts both centres on the same point.** That covers every drafted colonist nobody has
  hurt, which is the commonest bar there is. The key is an exact tie, and the sort breaks it
  differently from frame to frame, depending on everything else translucent in the list (brackets,
  rings, diamonds, other bars, ghosted storeys).
- **A part-full bar puts the fill's centre to the left of the track's**, so which is nearer
  depends on which side of the screen's middle the pawn stands. The order turns over as a pawn
  walks across the middle, and near that line it rides on a millimetre of the figure's motion.

The two orders do not look alike. Fill last: the fill shows at 0.62 of its colour. Track last: the
track's 0.53 of panel ink lies over it and the fill shows at 0.29. So the bar jumped between bright
and dull.

**The measurement.** Two candidates, one each side of the seam.

1. *The bar is owed on some publishes and not others.* **Ruled out.**
   `HealthBarPublishingTests.ABarIsOwedOnEveryTickItsStateSaysAndBlinksOnNone` fights two drafted
   colonists against two bandits for 3,000 ticks with the shipped rules and reads every tick's
   frame: 11,444 pawn-ticks owed a bar, 9,262 hurt. The drafted colonists' bars changed **0** times,
   and each bandit's changed **once** (on being hurt), exactly as their state did. Its negative
   control, `hp` published only on even ticks, failed on tick 5.
2. *The draw order of the track and the fill.* **The cause.** A probe (a scratch test, not kept)
   ran the old `CombatMarks.Bar` arithmetic in single precision for a colonist walking 8 m across
   the middle of the screen at 60 frames a second, at the play camera's 48° and 48 m, at three
   yaws, and compared the two centres' squared distances to the camera, which is the key the sort
   uses. At full health: **480 exact ties in 480 frames** at every yaw. At 0.8 and 0.5: the order
   **turned over once**, at the middle of the screen (fill last on 232–237 frames, track last on
   243–248). Nobody has watched Unity break the tie. This is the key it was handed, not a capture
   of the frame. The integrator's playtest settles whether the flicker is gone.

Also checked and not the cause: the material cache (the bar's inks are constants, so no material
is minted per frame) and the bar's place. `TryGetFeet` is a figure's own transform, and the pose
fallback is used only for a pawn that has no figure, so the two do not alternate.

**The fix: pieces that never overlap, in a plane facing the camera.** `HealthBarLayout` (Hud,
Unity-free) lays the bar as nine rectangles: a thin outline all round (4), a dark plate inside it
(3 round the channel), the fill from the left, and the plate behind whatever is lost, run on into
the right margin so the fill and the plate meet on one edge. No two cover each other at any
fraction, and together they tile the bar exactly, so no gap lets the ground through as a hairline.
`CombatMarks.Place` sets each piece 4 mm deep in the plane the camera's own rotation faces, so
neighbouring pieces meet edge-on. **Any order the sort picks now draws the same picture.**

**More prominent.** The channel is 0.13 m tall (the old fill was a 0.11 m box), with 0.03 m of dark
plate round it and a 0.018 m outline: 1.096 × 0.226 m in all, centred 0.02 m above the cursor
box's top, under the draft diamond. The fill is 92 % opaque. The plate is the panel's ink at 72 %,
so a figure behind it can still be seen. The outline is near-black at 85 %, so the bar has an edge
on dark rock and at night. All INVENTED, to be judged at the play camera.

**The colours.** `CombatFeedbackModel` owns them, as before, and the Health tab's fill follows:

| Band | Was (the stat ink) | Is | Hue | Saturation | Value |
|---|---|---|---|---|---|
| well (60 % and up) | `HudTheme.Good` `#7fc98c` | `HealthGood` `#32b349` | 130.5° held | 0.37 → 0.72 | 0.79 → 0.70 |
| hurt (40–59 %) | `HudTheme.Warn` `#e8b55c` | `HealthWarn` `#d99827` | 38.1° held | 0.60 → 0.82 | 0.91 → 0.85 |
| low (under 40 %) | `HudTheme.Bad` `#e06a5c` | `HealthBad` `#cc3a29` | 6.4° held | 0.59 → 0.80 | 0.88 → 0.80 |

The green in "the colony stats" is the need bars' `HudTheme.Good`, so the bar keeps that hue and
becomes more saturated and deeper. It is deeper as well as greener because the bar is drawn over a
sunlit board through a lit, glowing, translucent material that lifts every colour towards white,
and the stat tints were chosen for a dark panel. `HealthBarLayoutTests.TheBarsInksAreTheStatInksDeeper`
holds each one to its stat ink's hue within 3°, at least 0.15 more saturation and a lower value, so
a retune stays a deeper stat colour. The thresholds are unchanged.

**Cost.** Each bar is nine pieces, gathered by ink and flushed once at the end of the marks. That
is at most **five instanced calls for every bar in view** (outline, plate and the three fill
colours), against two submissions a bar before. One bar alone costs three calls where it cost two.
Per frame it walks the pawns on the drawn layers as before, lays nine rectangles into one reused
array for each hurt or drafted pawn, and allocates nothing once the buckets have grown. The
material is the bracket's (lit, translucent, glowing, the same cache) with the ink's own opacity
rather than 0.62 of it, so no new shader or variant has to survive the player build's stripping.

**Tests.** Fast tier: `HealthBarLayoutTests` (7) and `HealthBarPublishingTests` (1). The colour
assertions in `CombatFeedbackModelTests` and `CombatPaneTests` moved to the new inks. EditMode:
`CombatDrawnTests.EveryPieceOfABarLiesInThePlaneFacingTheCamera`, **never run**. Negative controls,
each seen to fail and then restored:

- the plate laid behind the fill as one rectangle (`NoTwoPiecesOfABarOverlapAtAnyFraction`,
  `ThePiecesTileTheWholeBar`);
- the green set back to `HudTheme.Good` (`TheBarsInksAreTheStatInksDeeper`);
- `hp` published on even ticks only (the publishing test).

**Do not undo by tidying.**
- **Never lay the plate behind the fill as one rectangle**, and never draw the track and then the
  fill over it. That is the obvious way to draw a bar and it is the flicker: two translucent draws
  that cover each other are ordered by the sort, and the sort cannot order a full bar.
- **The pieces meet on shared edges and tile the bar.** Shrinking one to leave a "safe" gap puts
  a hairline of ground through the bar; growing one to "make sure" is the overlap back.
- **The bar faces the camera's rotation.** A bar that lies along the camera's right but is square
  to the world shows its top face too, and its pieces then overlap on screen even though they do
  not in the world.
- **The fill's colour is `CombatFeedbackModel.HealthBarColour`**, never a colour in Presentation.

**Where.** `Hud/HealthBarLayout.cs`, `Hud/CombatFeedbackModel.cs` (the inks), `World/CombatMarks.cs`
(`Place`), `ChunkRenderer.GatherBarPiece` / `FlushBarPieces`, `OdysseyBootstrap.DrawCombatMarks`.
**Never compiled here**: `OdysseyBootstrap.cs`, `ChunkRenderer.cs`, `CombatDrawnTests.cs`
(`CombatMarks.cs` was compiled on its own against `UnityEngine.CoreModule`).

**Open, for the playtest.**
- Whether the flicker is gone. The tie was computed, not watched.
- Every size and opacity.
- Whether the draft diamond sitting just above the bar reads as one mark or two.
- Whether the deeper green, seen through the lit material, is the owner's green or needs darkening
  again.
- The Health tab's fill is now deeper than the need bars beside it on the pane. Keeping one owner
  for the colour costs the two bars on the pane their match.

### 8b. Drawn and sheathed

Built 2026-09-23 on `claude/combat-sheath` (from `claude/combat-c2-polish`, `70253cdf`). The
owner's rule, and who owns each half of it:

| Half | Owner | Where |
|---|---|---|
| **Whether** the weapon is out | the simulation, derived at each publish | `WeaponDraw`, published as `PawnFlags.Drawn` |
| **When** it goes back after the last reason | presentation's clock, in simulation ticks | `Odyssey.Hud.WeaponSheath` (Hud assembly, so the fast tier holds it) |
| **Where** it hangs and **how** it moves | the figure | `PawnFigureDirector.Sheath.cs` |

**The rule** (`WeaponDraw.IsDrawn`). Drawn when the pawn holds a weapon (`WeaponHand.Held`, so a
field naming a thing on the ground draws nothing) and any of: it is **hostile** (a bandit always
has its weapon out); it is **drafted**; it is in `Job_AttackMelee` and its target is within
**2 tiles**, Chebyshev on the ground and at most one layer up or down (a rescue names its patient
in the same `CombatTarget` field, which is why the job is asked and not only the field); or it is
inside the **retaliation window** a blow opened (`RetaliateUntilTick`, 1,200 ticks from the Def).
Every input is saved and hashed where it lives, so the flag is a report: neither saved nor hashed,
the same after a load, read back by nothing in the simulation. **No golden moved.**

**The put-away** (`WeaponSheath.Step`). Out on the frame a reason arrives; back **120 ticks** (about
2 s at the composition root's 60 a second) after the last one, or **at once on release from the
draft** when the simulation no longer says drawn. In the simulation there is no saved tick the last
reason ended on, and adding one would be state that decides nothing and still has to be saved, so
the hold is presentation's. It counts ticks off the frame, so a pause holds a weapon out. A first
sighting (a figure lent mid-fight) takes the weapon as it finds it and animates nothing.

**The hip.** The sheath hangs off the **real pelvis**: the parent of the left thigh, because the
Synty avatar maps `HumanBodyBones.Hips` to `Root` on the floor (`20-beds.md` §7b). Measured once at
bind, in the idle, off the drawn mesh (`HipReach`): at the height of the left hip joint, the
outermost baked vertex on the figure's left that lies nearer the thigh or the pelvis than the left
arm's segments — at hip height the hanging hand sticks out further than the hip, and a sheath hung
outside it hangs in the air. Nearest bone segment rather than skin weights, which a mesh not marked
readable does not give up in a player. Each weapon is then fitted to it off its own mesh, as the
fist's fit is (`FitWeaponAtHip`): the haft down, leaning back 25° and splayed out 6°, the blade's
width along the figure's front so the flat lies on the thigh, the point a quarter of the way up
from the butt at the hip, stood off the body by the weapon's own half-thickness. Both fits are kept
as local poses, and moving between hip and hand is a re-parent, never a re-fit.

**The draw and the sheathe.** Two rows outside `ModuleIds.CombatRows` (whose order is the combat
roles' and whose test holds every non-blow clip to an impact of nought): `CombatDraw`
(`A_Draw_Sword_Masc`, `_Femn`) and `CombatSheathe` (`A_Sheathe_Sword_Masc`, `_Femn`), Polygon, in
place, the variant by the body's sex (the colonist row's `sex`). They play on a **third input of the
fight's layer mixer, masked to the upper body** (`UpperBody`: everything but the root, the legs and
the foot IK), so a colonist drafted mid-stride keeps walking; its weight is multiplied by one minus
the fight layer's, so a swing, a react or a held state wins, and any of them finishes the draw at
once. **The prop changes bone at the hand-on-hilt moment, not at the clip's start**: measured once
per clip, on the first figure of that sex built, by sampling the clip at 60 Hz and taking the time
the palm (`HandGrip.Palm`) comes nearest the stow point; nearer than a fifth of the figure's height
or the fallback fraction (0.35 of the draw, 0.65 of the sheathe, INVENTED) stands. Without the
pack's rows the weapon **snaps** on the edge.

**One hand.** While a tool is in it (the work weight above the threshold `ShowHeldTool` shows the
tool at) or a load is in the arms, the weapon is at the hip whatever the rule says and nothing
plays; lying down (asleep or downed) it is not drawn at all. This replaces the integration's rule
(§6E) that hid the weapon whenever the hand was busy: it is now always visible, at the hip or in the
hand.

**Tests.** Fast tier, `WeaponDrawTests` (7): drafted → drawn and bare hands draw nothing; released
from the draft with nobody near → not drawn on the next publish; an undrafted colonist going about
3,000 ticks of her day → never drawn; the target at 3 tiles → sheathed, at 2 → drawn, at 5 → sheathed;
the distance rule's layer arithmetic; struck with the striker stood 5 tiles off → drawn, and an armed
bystander not; a bandit stood 10 tiles from anybody → drawn every tick, and bare-handed → not.
**Six mutations of the rule, each seen to fail the test that owns it**: no draft clause, no hostile
clause, no retaliation clause, reach 3, always a reason, no hand gate. `WeaponSheathTests` (7, Hud):
the hold, a reason inside it restarting it, the release, a pause, a rewound tick, the first sighting;
three mutations (no hold, release ignored, a first sighting animating) each seen to fail.
`CombatContractTests.EveryPawnViewSaysWhatItIs` now expects a spawned bandit to publish `Drawn`.
EditMode, **written without a Unity run**: `WeaponSheathPlacementTests` — the rows resolve or the
weapon snaps; at peace the weapon is on the pelvis (not the floor bone), on the left, at the hip;
with the pack stripped it snaps both ways, and the hold is 120 ticks; with the pack the draw leaves
it at the hip on its first frame and puts it in the hand past the measured grasp, and the sheathe
brings it back; the tool and the weapon never both in the fist, and the weapon under exactly one
bone, every frame. `WeaponPropTests`' hand test is rewritten for the hip. Their negative controls
are named here and **not yet seen to fail**: the stow parented to `HumanBodyBones.Hips`; the grasp
taken at the clip's start; the hand-busy test removed from `PoseSheath`.

**For the integrator.** Rebuild the catalogue (`PlayScene.RebuildCatalogue`, then
`CharacterSwatches.Classify`): the two rows are new and `TheSheathRowsResolveToAllowedClipsOrTheWeaponSnaps`
fails until they are in the committed asset. Presentation and the two test files were compiled with
`dotnet` against the editor's `UnityEngine` module DLLs as a check; Unity has compiled none of it.

**Open.** Every number in the hip fit (tilt, splay, hang point, forward, clearance, band) is
INVENTED and unseen; whether the grasp measured on one body lands on the hilt on the other sixty;
whether the pack's draw, authored for a sword at the pack's own hip, reads with a bat or a crowbar
hung at ours; the sheathe opens from the sword stance, so the arm lifts into it over the 0.12 s ease
from wherever it was; a hog's revenge draws nothing (no weapon), and a colonist struck keeps her
weapon out for the whole 20 s retaliation window, which is the owner's rule read literally and the
first thing to ask about at the playtest.

### 8c. One guard for every fighter (built 2026-09-23, `claude/combat-guard`)

Owner: *"make it a guard that enemies when sharing tiles going side by side as well or handled
uniformly as we can"*. Simulation, the fast and Long tiers, no Unity. **No golden moved, no save
format changed, no new state.** No golden window drafts or fights, and every claim is still read
off `Destination` and `Cell`, which every pawn already saves and hashes.

**The rule.** At every tick, no two pawns in a fight stand on one cell. A pawn is **in a fight**
while it is in a melee attack on its feet, of any kind — ordered, the drafted hold's blow,
self-defence, the hunt, an animal's revenge. It is also in a fight while it is the target of
one, whatever it is doing: standing, walking, down, or hunted from across the board. A pawn
**stands** once it has arrived and has had a tick to act on it. That means no walk in hand and no
interrupted step still landing, now and at the tick before, and it is in a job it has ticked or
held by a stun. Walking through somebody's cell is not sharing it. The simulation has no collision,
and the drawn sidestep carries a passer-by round. Neither is the one tick a pawn spends between
jobs or landing. Anything that stays is.

**The guard** is `FightGuardTests` (fast tier, 16 cases), which asserts the rule after every tick
of each brawl, with a control that the fight happened:

- several bandits on one colonist;
- several colonists on one bandit;
- two bandits on two colonists side by side;
- bandits walking into a drafted line;
- bandits converging from one side;
- hogs turning on the colonists who hit them;
- a rat fleeing past a brawl;
- drafted targets ordered about mid-fight;
- a body being finished off;
- a colonist sent onto a fighter;
- every mind at once.

The first five run both with every swing a miss and with the shipped rules. **Long:**
`MixedBrawlsOnManySeeds` runs twelve seeded colonies of three to six colonists, bandits, hogs and
rats. Some colonists are drafted and ordered onto the animals. Each colony runs for 4,000 ticks,
and the control is that every kind fought.

**What it found: four holes, each seen to fail with its fix withheld.** §7c held for attackers
against attackers, but not for everyone else in a fight:

| Hole | Evidence before the fix | Fix |
|---|---|---|
| A pawn somebody is attacking held nothing, so another fight's attacker could take its cell as a side | `ABodyBeingFinishedOffIsNobodysSide`: a bandit coming for a colonist stood on the downed bandit a colonist was finishing off beside her, **677 pair-ticks** | `Melee.Holds` (was `SideTaken`) and `ChooseSide`'s mask count **every fighter's `SideOf`**, attacker or target, and the target's own destination |
| The drafted hold struck from wherever she stood | Long seed 12: two drafted colonists on one tile both swinging at bandit 8 from it, **94 pair-ticks** | the hold asks `MayFightFrom` like anybody. On a cell another fighter holds, or on her threat's own cell, she steps to a free side, and she still never chases. Out of reach, she holds again |
| Drafting in place never spread | `BanditsIntoADraftedLine`: two colonists drafted on one tile held there, hunted, **269 pair-ticks** (1,410 with nothing fixed) | a draft is a move order to her own cell, and `SetDrafted` spreads it with the move order's own `Spread` |
| A move order's spread ignored fighters | `AColonistSentOnToAFighterStopsBesideIt`: sent onto the tile a bandit was swinging from, she was given it | `Spread`'s `Taken` also asks `Melee.Holds` |

With everything withheld, `TargetsThatKeepMoving` also failed, with **8 pair-ticks**: a drafted
colonist was moved onto a bandit's side and swung from it. It passes with either the hold's step
or the move order's spread alone.

**What the rule costs.** `Holds` and `ChooseSide` gain a lookup by id per attacker, for its target.
The hold is asked `MayFightFrom` on her first stop and before each swing, the same cadence §7c set,
because `Job.WorkTicks` is now set on the first stop. A refused attacker chooses again at once
rather than waiting out the chase cadence on somebody's tile. `TickBenchmarkTests.TwentyAgainstTwenty`
was measured in one session, twice each way. The tick was 0.080–0.087 ms before and 0.070–0.087 ms
after. The pawns phase was 0.021–0.023 ms before and 0.014–0.020 ms after. Both are inside their
own noise.

**The drawn half had already moved.** The combat contracts (`500a6f09`, §5d) put the crowd sidestep
on the published person flag: `PawnPose` gates on `pawn.IsPerson`, and `CrowdWeight` skips
`other.IsAnimal`. So a bandit steps round colonists and bandits and is stepped round, and
animals stay outside it (design 29). No other presentation or HUD site reads "a person" as
`Kind == 0`. Every remaining `Kind` read is a species-table row: animal looks, labels and bite
sharpness. Nothing pinned it, though, so two tests now do.
`PawnPassingTests.ThePersonFlagDecidesWhoStepsRound` covers the seven pairings of colonist,
bandit and hog. `PawnCrowdIndexTests`' crowd is now a fifth bandits and a seventh hogs, so the
exactness claim holds across the gate as well as the arithmetic. **Both have never been compiled or
run**, because the fast tier does not build Presentation.

**Do not undo by tidying:**

- **A target holds its `SideOf`, not only its cell.** A target walking to a cell will stand there.
  Choosing that cell as a side is how a colonist ordered onto a bandit's flank met the bandit
  arriving on it.
- **The guard's one-tick grace is not a loophole.** Between jobs, on the tick a job is given, and
  on the tick a step lands, a pawn has not yet chosen its cell. The very next tick is checked, and
  every hole above failed for tens to hundreds of ticks. When the grace was tried stricter, every
  failure was a single tick of that kind.
- **The draft spreads; the hold does not wander.** The hold steps only when her cell is held and
  never chases. A side a ring back leaves her out of reach, and she holds there.

**Open.** A colonist going about her day — hunted, not yet struck, not drafted — has no rule that
moves her off a fighter's cell. Nothing she does is the fight's until the first blow, after which
self-defence moves her. The guard counts her from the moment she is hunted, and no brawl here
catches it, because an ordinary job rarely stops on a fighter's tile. If one ever does, the guard
will name it.

## 9. The third playtest's round (2026-09-23)

Played on `claude/combat-c2-polish`. The owner's asks and the interview's answers, built on
`claude/combat-c2-r3`:

| Ask | Decision (owner, interviewed 2026-09-23) | Section |
|---|---|---|
| *"When a person is hit there should be a reaction ... visual reactions to hits"* | **Every landed hit flinches** the target, from the side the blow came (the pack's hit-react clip; computed for animals). **A hit of 12 or more damage, or any critical, staggers**, rocking the body back half a step. The stagger is drawn only: the pawn stays on its tile. | §9a |
| *"A knockback — in fact a chance you can fall back on to the next tile (maybe on critical hit)"* | **Critical hits:** every landed hit has a 10% chance, plus 1% per 4 attacker Melee levels, and does ×1.5 damage. **A critical knocks the target back one tile** with a 50% chance (75% for a blunt weapon), directly away from the attacker. That tile must be free and standable; it may be **one terrace step down, never water, never two layers or more down, never a climb up**. If the tile is not allowed, the target staggers in place instead. The target lands **knocked down** for about 1.5 s (`PawnFlags.KnockedDown`, the knock-down clip), then stands. Animals can be knocked back too. Events: `Critical`, `KnockedBack` (from-cell in `Amount`). | §9b |
| *"Baseball bat wasn't close enough to hips/waist when not drawn. Same goes for machete"* | Bring the sheathed weapon in against the hip. Measure the gap from the weapon mesh to the body surface on the drawn meshes, with a numeric test, and photograph it. | §9c |
| *"We'll make an entry for gear later to include equipped weapon (seam for later)"* | A **seam only**: a Unity-free `GearModel` that lists what the colonist holds (the equipped weapon, drawn or at the hip). The Gear tab stays disabled; later work fills it. | §9d |
| *"You could still attack a pig after it died — make a guard for this — check bandit does this"* | **A dead pawn is never a target.** The attack order is refused on a dead pawn or a corpse, an attack job ends the tick its target dies or leaves the board, and hostile, animal and drafted target choice never picks the dead. A guard test runs every tick of mixed fights to the death and fails if anybody swings at, walks to, or keeps a job against a dead pawn. The same guard covers bandits. | §9e |
| *"Their health needs to be also displayed on their colony stats"* | **The colonist cards along the top get a fourth bar, health, always shown**, in the overhead bar's colours (green, amber below 60%, red below 40%). A downed colonist's card shows it empty and red, with *Downed*. | §9f |

### 9a. Reactions (built 2026-09-24, `claude/combat-react`)

Owner: *"When a person is hit — there should be a reaction ... there needs to be visual reactions to
hits."* Presentation only, against the contracts cut at `29864457` with a scripted event feed; the
simulation half of §9b is another lane's. No simulation, save, hash or golden changed.

**Why the owner saw no reaction.** Lane B had built hit-react and stagger rows (§6B), and they were
routed to the struck figure, not the attacker. They were refused. Measured, not read:

- **`React` returned without drawing anything while the struck figure's own swing was showing**
  (§6B: "a react never cuts off the figure's own swing"), and a `Strike` started a swing over
  whatever was showing, so the next swing cut a react off.
- **In a fight the struck body is nearly always in its own swing.** Both fighters swing at each other
  on cooldowns of 96–150 ticks, and a drawn swing is long, because the pack's clip is timed so its
  impact lands on the wind-up tick: a light swing is drawn for 2.0–4.0 times its wind-up, a heavy
  one 1.6–2.1 times, a computed punch 1.8 times. A machete (22 / 96) is drawn for about 64 % of its
  cycle, and equal cooldowns lock the two fighters in phase.
- **On real fights.** A probe on the fast tier (a drafted colonist ordered on to a bandit with the
  machete, eight fights over three seeds and every weapon, the published tape with lane B's rule
  applied to it; not committed): of **193 landed blows, 86 fell inside the target's own drawn swing
  and drew nothing** (30–70 % per fight), and of the 99 that did start, the target's next swing began
  within 20 ticks (a third of a second at speed one) for 47. **52 of 193 blows (27 %) drew a reaction
  that lasted a third of a second at speed one.** At speed three, where a reaction runs on the
  frame's seconds and a swing on ticks, almost none did.
- **The stagger, which did cut through a swing, almost never fired**: its threshold was 10 points
  and only the arc blade reaches it (bat 7, crowbar 8, machete 8, each ± 20 %).
- **Not the cause:** subtlety (the pack's react is a whole-body clip), and the attacker taking the
  reaction (the target's figure was the one asked).
- **The same rule in the duel harness** (`CombatReactionsTests.LaneBsRuleLeftMostBlowsUnseen`, every
  weapon pairing and phase offset in the content's numbers): **1,512 of 2,274 blows drew under
  0.3 s of reaction at speed one, and 2,034 of 2,195 at speed three.**

**What plays when** (`Odyssey.Hud.CombatReactions`, Unity-free; the figure only asks):

| Event | The struck figure draws |
|---|---|
| `Hit` under 12 points | A **flinch**: the pack's `A_Hit_{F,B,L,R}_React` (0.87 s) on the clip layer, or computed for an animal and without the pack — the chest folded and the head snapped away from the blow, pushed 6 cm, over 0.4 s, peaking at 0.06 s |
| `Hit` of 12 points or more (`Amount >= 12000`), any `Critical` | A **stagger**: `A_Hit_{side}_Stagger` (1.1 s), or computed over 0.9 s, rocked back 0.35 m (half a step) and recovered. Drawn only: the pawn stays on its tile. A critical arrives on the tick of its hit and upgrades that hit's flinch |
| `Stun` | A stagger that ends the figure's own swing, which a stunned pawn does not land (§5j). With the pack, the stun's own begin clip, started by the flag, still wins |
| `KnockedBack` | A **slide** from the cell in `Amount` to `Cell`, along the line of the blow, over 0.25 s: thrown and slowing along the ground, falling (t²) if it lands a layer down, so it goes over the lip before it drops. The frame the event is read was already posed on the landing tile, so the figure is put back where the blow found it that frame, and the speed that one-frame jump measured is forgotten |
| `PawnFlags.KnockedDown` | Drawn as down (`CombatReactions.Floored`): the knock-down row's `Begin`, its `Loop` while the flag is up, its `End` as it stands. Without the pack, and for an animal, the sleeper's lie eased over 0.45 s is the computed topple and rise |

The side is `CombatReactions.SideOf` — quarters at 45°, from the attacker's figure where it has one,
else its cell, else the front. `CombatPose.SideOf` now answers through it.

**Who has the clip layer.** A reaction is no longer a one-shot. It runs on its own track beside the
figure's swing (`ReactionTrack`), and each frame `ReactionTrack.Show` decides between them:

- **The strongest reaction wins**: knock-down, then stagger, then flinch. One as strong or stronger
  restarts it from its own side, and a weaker one is let go while it runs.
- **With the figure's own swing live, the swing has the layer from 0.35 of its wind-up before its
  impact to 0.25 after, and the reaction has it the rest of the time.** A blow taken early in the
  wind-up interrupts it, and the swing comes back in time to be seen landing. A blow taken in the
  follow-through cuts the follow-through short.
- **Once the swing takes the layer back, the reaction does not return to it.** There is one
  hand-over per blow, not a flicker between two clips. From then on the reaction is laid on as the
  computed flinch, over the swing and over the idle if it outlives the swing. So every blow is seen
  for its whole flinch, whatever took the layer.
- **A swing may cut a get-up short**, and so may a blow. The simulation has the pawn on its feet and
  swinging while the pack's two-second get-up would still be playing.

`TryGetFight`'s role now reports **what the layer shows** (`CombatState.Shown`), not the one-shot
underneath. `TryGetReaction` reports the track, whether the flinch is laid over this frame, and
whether a slide is running.

**Tests.** Fast tier, `CombatReactionsTests` (14; Hud 865, Sim 1,196, all green). They cover:

- which reaction each event asks for, and the 12-point line;
- floored;
- the four sides and their boundaries;
- every computed shape moving away from the blow and settling;
- the track's strongest-wins rule and a pause;
- the arbitration window;
- the one hand-over;
- the slide's endpoints, monotony and drop over the lip.

The main test is **`InADuelEveryLandedBlowIsSeen`**. It steps duels on the content's numbers frame
by frame, in the director's order (pose, then the frame's events). It covers five weapon pairings,
every 5-tick phase offset and speeds one and three, with the pack and without it. **Every one of the
2,195–2,274 blows is seen for at least 0.3 s of its first 0.4 s.**

Six mutations were each seen to fail the test that owns them:

- the swing always keeps the layer;
- no computed shape, so no overlay: the duel fails;
- an equal reaction not restarting: the duel at speed three fails;
- lane B's 10-point threshold;
- a linear drop;
- the overlay only over the swing. This was the first version of the hand-over rule, and the duel
  caught it: 537 of 2,195 blows at speed three were under 0.3 s.

EditMode, **written without a Unity run**, compiled with `dotnet` against the editor's module DLLs:
`CombatReactionDrawnTests` (6). Each drives `CombatFeedback.Consume` with a scripted
`CombatEventView` stream, as the bootstrap does:

- struck early in her own wind-up, she flinches on the Hit frame, and the next frame shows
  `HitReact` over the swing: the clip easing in, or the computed flinch;
- struck round her own impact, her punch keeps the layer and the flinch is laid over it;
- a critical staggers;
- a knock-back is drawn at the from-cell on the frame it is read, between the cells seven frames on,
  and on the landing tile after the slide;
- knocked down, the head is on the ground, and a swing cuts the get-up short;
- a hog flinches, computed.

The negative control for the first is lane B's `React` and is **named, not yet seen to fail** there.
Its fast-tier twin is.

**Do not undo by tidying.**
- **A reaction is not a one-shot.** Put it back in `CombatState.Action` and it is refused by, or cut
  off by, the figure's own swing — which in a fight is always there. That is the bug the owner
  reported.
- **The swing's window round its impact stays the swing's.** The simulation lands that blow whatever
  is drawn. A reaction drawn over it shows a hit landing out of no swing.
- **The flinch laid over is what makes "every blow" true.** The window alone leaves a blow taken
  inside it unseen, and so does the hand-over without the overlay.
- **One hand-over per blow.** A reaction that takes the layer back after the swing's window pops
  into the tail of its own clip.
- **The slide is drawing only.** The pawn is on the landing tile from the blow's tick. Nothing may
  read the drawn position back.

**Open, for the integrator and the playtest.**
- Every number here is INVENTED and unseen:
  - the window, 0.35 before and 0.25 after;
  - the flinch, 14° chest, 20° head and 6 cm;
  - the stagger, 26°, 14° and 0.35 m;
  - the slide, 0.25 s.
- The pack's stagger plays at its authored 1.1 s, not the 0.9 s asked for.
- At speed three the knock-down's ~1.5 s flag lasts half a second of real time. That is shorter
  than the pack's 0.73 s `Begin`, so the get-up starts before the fall has finished. Every held
  clip runs on the frame's seconds (§6B).
- A knocked-down colonist keeps her weapon in her hand. `ShowWeapon` hides it for `IsDowned` and for
  sleep, not for `KnockedDown`. `PawnFigureDirector.Weapons.cs` is not this lane's.
- A knocked-down pawn past the figure cap is drawn standing by the instanced pass, as a downed one is.
- The flinch over a pack swing folds the chest and head after the clip. The arms are counter-turned
  by the chest's fold, as the computed swings do, so the blade stays where the clip put it.
- Unity has compiled none of it. PlayMode, the frame budget in a fight and the player build have not
  been run.

### 9b. Criticals and knockback (built 2026-09-24, `claude/combat-crit`)

Simulation, the fast and Long tiers, no Unity. **No golden moved**: no golden window fights, so no
swing is ever decided in one, and every new field is saved and hashed only while it is set. **The
content fingerprint moved once**, for the six `CombatDef` numbers below
(`PawnContentDefTests`, the twentieth move).

**The numbers** are the owner's and live in `Combat.xml`:

| `CombatDef` field | Value | Means |
|---|---|---|
| `critChancePerMille` | 100 | a blow that lands is critical one time in ten… |
| `critPerMillePerFourLevels` | 10 | …plus 1 % for every four whole Melee levels of the attacker. An animal counts its species' `meleeSkill` |
| `critDamagePerMille` | 1,500 | a critical does half as much again. The `Hit` carries the multiplied damage |
| `knockbackPerMille` | 500 | a critical knocks its target back half the time… |
| `knockbackBluntPerMille` | 750 | …three times in four with a blunt weapon. Fists count as blunt |
| `knockedDownTicks` | 90 | a target knocked back lies where it landed for about 1.5 s |

**The rolls.** `MeleeRules.Resolve` rolls the critical after hit, dodge, damage and stun, on its own
stream, `PawnPurpose.MeleeCritical`. A critical then rolls its knockback on `PawnPurpose.Knockback`.
Both salts are SHA-256's ninth and tenth round constants, which carry on the family the combat salts
began. A miss or a dodge is never critical.

- **The critical is its own stream.** On the hit roll's stream the critical share of landed blows at
  level 0 read 208 in a thousand, not 100: the two rolls agree, and only low rolls land. Measured with
  `CriticalsLandAtTheirRate`.
- **A critical moves nothing else.** `ACriticalIsTheSameBlowHalfAsMuchAgain` resolves 20,000 swings
  with and without criticals and finds the same hit, dodge, stun and damage every time, times 1.5
  where it was critical.

**The blow is decided when the swing begins (the §9g contract).** Owner, 2026-09-23: a sharp
critical plays a sword-slice sound *during* the swing. So the outcome has to be known when the
wind-up starts.

- **When it is rolled.** `AttackMeleeJobDriver.StartSwing` asks `IMeleeRules.Resolve` on the tick
  the wind-up begins. The odds and the salts are the same as before; only the tick moved.
- **Where it is kept.** The answer stays on the pawn through the wind-up: `Pawn.HoldSwing` and
  `HeldSwing`, three ints.
- **What is published.** A critical that will land is published as **`SwingCritical`** (11) *in
  place of* `Swing`. Its amount is the wind-up in ticks.
- **What lands.** At the impact, `CombatSystem.LandOrLose` applies exactly the kept outcome. If
  the target has since stepped out of reach, died, or gone down on a job that stops at down, the blow
  is a `Miss`. So an announced critical can fall on air, but nothing is reported that did not happen.
- **When it is let go.** The kept outcome is dropped when the swing lands, when it is lost (the
  attacker stunned, down or dead) and when the job ends (the driver's cleanup).
- **An old save.** A swing in the air in a save older than layout 3 has no kept outcome. It is
  decided at the impact, as every swing was before.

**Order of events at the impact:** `Hit` (the multiplied damage), then `Critical` (amount 0, same
tick, same pair), then death, the fall, the stun, then `KnockedBack` if it went, then the
reaction. A blow that kills or downs is never a knockback, because death and the fall return first.
The stagger in place is presentation's, off `Critical`, when no `KnockedBack` follows it.

**Where it may land** (`CombatSystem.KnockbackCell`):

- **Directly away**: the step from the attacker's cell to the target's, continued one more cell,
  diagonals included. Only from a neighbouring cell on the same layer.
- **On the same layer**, by a step the target itself could take (`NavGraph.IsLegalStep`), so never
  through a wall or its corner.
- **Or one terrace step down**: the cell beyond is open air and the cell under it is ground the target
  can stand on. On a diagonal, neither corner may be a wall.
- **Never two layers or more down, never up.** A rise behind the target, or a wall, is simply no
  knockback.
- **Never water.** The cell beyond, and where it would land, must not be water, a wade, or the top
  of water.
- **Never on to a tile another fighter holds**: `Melee.Holds`, §8c's one rule. This is what keeps
  a knockback from standing two fighters on one tile.

When a knockback is not allowed, the target stays where it is and nothing more happens in the
simulation.

**What a knockback does** (`CombatSystem.KnockBack`), all in the blow's own call:

1. The job ends through `JobSystem.EndJob`, the one release path. A carried load is put down by the
   driver's cleanup where the blow found her, claims are let go, a swing in the air is lost, and the
   path is cleared.
2. The pawn is moved to the landing cell.
3. It is knocked down for 90 ticks: `Pawn.KnockedDownUntilTick`.
4. `KnockedBack` is reported, with the landing cell in `Cell` and the cell it came from in `Amount`.

**The knock-down is the stun's hold.** `JobSystem.TickPawn` returns before the job and the tree while
`KnockedDownAt(tick)`, and `MovementSystem.Advance` takes no step. So the pawn does nothing: no job
ticks and no step. It is published as `PawnFlags.KnockedDown` until it stands, and the combat pass
puts the clock back to nought once past. **Going down clears it**, because down outranks knocked
down. An animal is knocked back the same way.

**The player's attack order outlives the fall.** Ending the job ended the order too, and a drafted
colonist then stood idle one tile off while the foe she was sent at walked up to her. The ordered
duel in `EveryReportCarriesTheWeapon` fell from 23 swings in 3,000 ticks to 12. So a forced
`Job_AttackMelee` is given again in the same call, with the same target and the same end. It is held
with the rest of her until she stands (`AnAttackOrderOutlivesTheFall`). Everything else that was
ended stays ended: a hold comes back from the draft, a hunt and a revenge from the mind, and a haul
from the work scan.

**Saved and hashed only while set.**

- **Saved.** `CombatSection` is **layout 3**, which appends four ints to every record: the
  knock-down clock and the kept swing (result word, damage, stun). Layouts 1 and 2 still load, with
  nobody knocked down and no swing in the air (`AnOlderCombatSectionLoadsWithNobodyKnockedDown`).
- **Hashed.** Each has a bit in the pawn's kind word (20 and 21) and is hashed only while set. A
  pawn with neither hashes exactly as before.
- **Round trips.** A save taken mid-knock-down resumes on an equal hash 300 ticks on. A save taken
  mid-swing resumes on an equal hash 600 ticks on. Each was seen to fail with its field left out of
  the load.

**What it costs.** One integer comparison a pawn a tick in the combat pass (the clock), one in the job
pipeline and one in the mover. `KnockbackCell` is one `Melee.Holds` pass over the pawns, asked only
for a critical that rolled its knockback. `TickBenchmarkTests.TwentyAgainstTwenty`, one run: tick
0.077 ms mean, Pawns phase 0.016 ms, 283 swings. That is inside §8c's band of 0.070–0.087 and
0.014–0.020.

**Tests** (fast tier unless marked). Each was seen to fail with its rule withheld:

| Test | Covers |
|---|---|
| `CriticalTests` | the owner's chance by level (an animal at its `meleeSkill`); the rate over 20,000 rolls at levels 0 and 20 (208 ‰ on the hit's stream); ×1.5 of the same blow with nothing else moved; knockback 500 ‰ sharp and 750 ‰ blunt, and never without a critical; a critical announced at the swing's start lands as announced (with the outcome rolled again at the impact: *"the blow was not the one decided"*) |
| `KnockbackTests` | one tile straight back in six directions, with `Hit`, `Critical` and `KnockedBack` in that order and the cells in the event; a critical with no knockback stays put; 90 ticks lying with no job and the flag published every frame, then standing (with the job pipeline's hold withheld it was given a job at once); one terrace step down; never two down; never up; never into water, level or below a step (both failed with the water check withheld); never on to a fighter's tile, with a non-fighter's as the control (failed with `Holds` withheld); the downed and the dead not knocked back; a hog knocked back; the order outliving the fall (withheld: *"the order was lost with the fall"*); the mid-knock-down save; layouts 2 and 3; a hauler's load put down where she was struck |
| `FightGuardTests.KnockbacksNeverStackFighters` | four brawl shapes with every landed blow a knockback critical, §8c's guard after every tick. `Guard.Stands` counts a knocked-down pawn as standing from the tick it lands. With `Holds` withheld, three of the four shapes failed (10, 67 and 42 pair-ticks) |

Three older tests read *when a swing landed* off the rules' own record, which now sees the swing
start. They read the published impact instead (`Tape.Landed`): `ARepeatedAttackOrderIsQuiet…`,
`AStunnedAttackersSwingDoesNotLand` and `ABanditInAFightKeepsItsSwing…`. The damage-spread test
skips criticals, which are 1.5 times the spread by design.

**Do not undo by tidying:**

- **The outcome lives on the pawn, not in the driver.** The driver pool is not saved; the pawn's
  combat record is, and the mid-swing round trip needs it.
- **`SwingCritical` replaces `Swing` rather than following it.** One swing, one start event. A reader
  counting swings counts both kinds.
- **The knockback ends the job.** A pause would keep a hauler's load in her arms across a tile she
  never walked, and a path from a cell she is no longer on. Only the player's attack order is given
  back.

**Open.**

- **Presentation does not yet read `SwingCritical`.** `PawnFigureDirector.OnCombatEvent` times a
  swing on `case CombatEventKind.Swing`, and `CombatFeedback.WhereOf` places a swing's word at the
  swinger for `Swing` only. Until both also take `SwingCritical`, a critical's wind-up plays at the
  default timing and its moment is placed at the target. That is lane B's, and it could not be
  compiled here (the fast tier does not build Presentation).
- **A knocked-down pawn still dodges** at its level. "A pawn lying down does not dodge" is the
  downed rule; whether it extends to the knock-down is a feel question for the playtest.
- **A knocked-down pawn with nobody attacking it holds no tile.** A drafted hold's blow ends when
  its target is out of reach, so a bandit knocked back by the hold lies on a tile no fighter
  claims. It is in nobody's fight until it stands, so §8c's rule does not count it.

### 9c. At the hip (built 2026-09-23, `claude/combat-c2-r3`)

Owner, playtest: *"Baseball bat wasn't close enough to hips/waist when not drawn. Same goes for
machete."* Every number in §8b's hip fit was invented and none had been seen. This round measured
it, photographed it, and replaced it with a fit taken off the drawn meshes.

**The instrument.** `SheathGauge` bakes the posed skin and splits off the arms. It then takes every
sampled point of the weapon's surface to the nearest body triangle. The points are 1 cm apart and
come off the real mesh, which the editor can read. A point is *inside* when a ray cast outward
from it meets skin: the thigh it is in, or the skirt it is under. `PawnFigureDirector.TryMeasureSheath`
exposes it. `SheathProbe` (`scripts/unity.sh shot Odyssey.EditorTools.SheathProbe.Shoot`) prints
it for all 29 bodies in the colonist pool with each weapon, over three seconds of idle. It also
photographs four bodies front and side. The first version signed the gap by the nearest face's
normal. It read the lower edge of a jacket, an open boundary, as ten centimetres of thigh, and so
it reported intersections that were not there. The ray replaced it.

**Before** (the §8b fit, measured with the final gauge). Nearest point of the weapon to the body,
cm:

| Body | Bat | Crowbar | Machete | Arc blade |
|---|---|---|---|---|
| `SM_Gen_Chr_Street_Male_01` | 2.5 | 2.9 | 1.2 | 0.9 |
| `SM_Gen_Chr_Street_Female_01` | 6.7 | 4.7 | 5.1 | 3.0 |
| `Character_MilitaryMale_01` | 6.0 | 5.4 | 6.3 | 5.8 |
| `Character_70sFemale_01` | 7.4 | 6.7 | 6.6 | 4.9 |
| **all 29, median (max)** | 6.2 (10.9) | 4.5 (6.9) | 5.1 (8.1) | 3.0 (6.0) |

Every weapon leaned 25.7° from plumb. No weapon was inside the body: the owner saw a gap, not an
intersection. `2026-09-23-sheathed-{bat,crowbar,machete,arcblade}-before.png` shows each weapon
hanging in the line of the hanging hand, not against the thigh.

**After**, at two instants of the idle a second apart (`WeaponSheathGapTests`):

| Body | Bat | Crowbar | Machete | Arc blade |
|---|---|---|---|---|
| `SM_Gen_Chr_Street_Male_01` | 1.6–1.7 | 1.7–1.8 | 2.2 | 1.1 |
| `SM_Gen_Chr_Street_Female_01` | 2.0 | 1.8–2.0 | 2.2 | 2.8–2.9 |
| `Character_MilitaryMale_01` | 2.2–2.3 | 2.3 | 1.5–1.6 | 4.0–4.2 |
| `Character_70sFemale_01` | 1.7 | 1.1–1.3 | 1.6–1.8 | 1.5 |
| **all 29, median (max)** | 2.1 (4.0) | 2.1 (3.9) | 2.1 (2.7) | 2.6 (5.0) |

Nothing is inside. The lean is 7.9–12.9°. The handle's top is 29–31 cm above the pelvis bone.
The lowest point is 10–14 cm off the floor. The pictures are
`docs/reference/screenshots/2026-09-23-sheathed-{bat,crowbar,machete,arcblade}.png`: one row per
body in the order above, with the front on the left and the figure's left side on the right.

**The fit** (`PawnFigureDirector.Sheath.cs`, `SheathSurface`, `WeaponProfile`):

1. **The hip is a relief, not a point.** At bind, the drawn skin on the figure's left is mapped
   over height and depth. Each cell of `SheathSurface` holds how far out the body reaches there.
   Separately, it holds how far in the hanging arm comes. The relief is kept in the pelvis's
   space, and so is the weapon.
2. **The relief is the whole idle's envelope.** It is built from six samples of the idle clip.
   `Desynchronise` starts every pawn's idle at a phase of its own, and the idle shifts weight
   between the legs. A relief of the first frame fitted one instant. Measured: a refit in the pose
   on screen closed a 4 cm gap to 2.8 cm. The relief is measured **once per look** and shared.
3. **Arms are told from body by surface, not by bone.** A point belongs to the limb whose
   *surface* is nearest: the distance to the bone less a thickness. The thicknesses, as fractions
   of height, are torso 0.065, thigh 0.045, shin 0.03, upper arm 0.022, forearm 0.018 and hand
   0.01. The hand is a fan from the wrist to each mapped fingertip. The first relief used the old
   rule, bone distance only with the hand as a line on from the forearm. The hanging fingers came
   out as hip, about 7 cm outboard of the skin, which is the size of the gap the owner reported.
   It is the likeliest cause of the §8b gap. This is **inferred**: §8b's `HipReach` was never
   instrumented.
4. **A weapon is its measured profile.** The weapon meshes are not marked readable, so a player
   cannot measure them. `WeaponProfileBake` cuts each one in the editor into 32 slices along its
   long axis. The slices are committed as numbers in `WeaponProfiles.g.cs`. `WeaponProfileTests`
   fails when the numbers and the art disagree. Bounds would make a bat's handle as fat as its
   barrel, and a crowbar's shaft as deep as its claw. Hung by its bounds centre, the crowbar's
   shaft sat in front of the thigh (photographed).
5. **Where it hangs.** The point a quarter of the way up from the butt goes at the hip joint's
   height. It goes at the depth where the side of the hip and upper thigh stands out furthest.
   That depth is set by the weapon's own line, not its bounds centre: the middle of its
   front-to-back spread, band by band. The weapon leans back 8°. It is splayed out by the whole
   degree, 0–10°, that brings the belt end nearest the hip. Several male builds stand wider at the
   knee than at the hip, and a plumb weapon then stands off the hip by the whole difference
   (measured 4–7 cm). A weapon longer than the leg is lifted until its lowest point clears the
   floor by 0.02 of the figure's height. Only the arc blade needs this, and it puts its hilt at the
   ribs.
6. **How far out.** The weapon slides out until every point clears the relief by
   `SheathClearance`, 0.004 of height (about 1 cm). To clear the arm relief as well, it may move
   up to 0.06 of height back and never more than 0.02 forward. If no depth clears, it stays at the
   side.

**Do not undo by tidying:**

- **The envelope, not one frame.** A single-frame relief is right for one pawn id and wrong for
  the next.
- **The profile table, not the bounds.** Bounds put the bat 2 cm further out and the crowbar's
  shaft in front of the leg. Run `scripts/unity.sh exec Odyssey.EditorTools.WeaponProfileBake.Run`
  when a weapon's art changes.
- **Back, not forward, past the hand.** The first search went both ways. Every bat and machete
  walked 12–22 cm forward, in front of the knee, to dodge a hand the relief could not clear.
- **No grace cells on the body relief.** A one-cell dilation added about a centimetre of air to
  every fit (measured).
- **The gauge's ray, not a face normal.** The nearest-face sign reports phantom intersections at
  every open edge (see the instrument, above).

**Tests.** `WeaponSheathGapTests.EverySheathedWeaponSitsAgainstTheHip` checks four bodies, four
weapons and two idle instants. The nearest point must be 0.8–3.0 cm from the body. A weapon
longer than the leg may be up to 4.5 cm. No point may be inside, and the lean must be within 15°.
Nothing may go through the floor. The top must be at most 0.2 of height above the pelvis, unless
the weapon was lifted to clear the floor. **Negative control:** the test failed against the §8b
fit on 2026-09-23. It failed on the lean everywhere, and on the gap: 6.7, 4.7 and 5.1 cm on the
feminine body alone. `WeaponProfileTests` holds the table to the meshes, and a bat's handle to
under 0.7 of its barrel. EditMode, filtered to the weapon and sheath fixtures: 17/17.

**Cost.** Once per look: six skin bakes of the cropped left side, classified and rasterised. The
slowest look took **27 ms** in the editor. It is paid on the first figure built of a look, and
never again in a session. Once per weapon swap: about 10k profile points against the relief,
tried at 11 splays and up to 13 depths. Nothing is per frame. Not measured in a player.

**Open.**

- **The hanging hand overlaps the weapon in the idle** on the bat and the crowbar: the gauge's arm
  gap is 0.0–1.3 cm on several bodies. No depth within 0.06 of height behind clears both the
  thigh and the hand, so the fit stays at the side and the hand hangs over the handle. For the
  owner: a hand resting on it, or the weapon behind the hand?
- **Walking is not measured.** The sheath rides the pelvis, and the thigh swings forward through
  it in the stride.
- **The arc blade is 1.7 m drawn, longer than the leg.** Its hilt sits at the ribs and its guard
  at the elbow. It is 3.8–4.2 cm off the military build and 5.0 cm at worst across the cast. It
  may belong on the back, which is a decision, not a fit.
- **Across the cast, 7 bats and 5 crowbars sit 3.0–4.0 cm off.** All are wide-stance male builds
  or one idle phase in six. This is the price of an envelope that forbids intersection.
- The draw's hand-on-hilt sampling still aims at the hip's surface beside the joint, not at the
  fitted weapon's grip.

### 9d. The gear seam

Built 2026-09-23 on `claude/combat-cards`. The owner's words: *"We'll make a entry for gear later to
include equipped weapon (seam for later)"*. **This is a seam only.** The Gear tab stays disabled
with its reason (`InspectModel.AddColonistTabs`: *"equipment arrives with the inventory"*), and
nothing draws the model yet.

**`Odyssey.Hud.GearModel`** is Unity-free. `Refresh(snapshot, pawn)` fills `Rows`, a reused list of
`GearRow`, and allocates nothing. It returns false with no rows for a pawn the frame no longer
carries. It returns true with no rows for an animal. For a person, colonist or bandit, it returns
true with one row today:

| Field | Today | From |
|---|---|---|
| `Slot` / `SlotName` | `GearSlot.Weapon`, "Weapon" | `ui.combat.weapon` |
| `ItemDef` / `Name` / `IconKey` | the weapon in the hand, or −1, "Bare hands", no icon | `odyssey.pawn.weapon` through `ItemLabels`; `ui.combat.barehands` |
| `Carry` / `CarryWord` | `Drawn` "Drawn", `AtHip` "At the hip", or `None` for the bare hands | `PawnFlags.Drawn` (§8b), never the draft; `ui.combat.drawn`, `ui.combat.athip` (new keys, wiki rebuilt) |

**What the Gear tab will be built from**, when it is built:

- **The model: `GearModel.Rows`, and nothing else.** `InspectModel` owns one and refreshes it for the
  pane's pawn on the pane's cadence, exactly as it refreshes `HealthRows`, and it sets the Gear
  tab's `Enabled` when the subject is a person. The words and the carry come from the model, so
  the view never reads an aspect.
- **The view: a partial of `HudShell` shaped like `HudShell.Combat.cs`.** Build the body once per
  subject into the pane's fixed-height tab box, forget it on rebuild, show it with the tab strip, and
  sync it 15 times a second, writing an element only when its value moved. Each row is the item's
  icon (`IconKey`), its name, and the carry word dimmed. The bare hands row has no icon.
- **The rows it grows.** A `GearSlot` per new place something is held: apparel by body part when
  clothing exists, then the pack when an inventory exists (the carried stack is
  `JobLabels.CarryingAspect` today, a load in the arms and not gear). Each new slot is an enum value,
  a published aspect, a registry key and a test. It is never a second reading in the view.
- **The commands it will carry.** Drop, and equip from the stockpile. Equipping is already an order
  (§6C, the context menu's Equip row, §7a), so the tab's button sends the same intent and adds no
  new path.
- **The Health tab's weapon row stays until the Gear tab ships.** Then the owner decides whether it
  moves. Until then, `GearModelTests.TheGearRowNamesWhatTheHealthTabNames` holds the two to one name
  across every weapon and the bare hands.

**Tests** (fast tier, `GearModelTests`, 12 cases): the bare hands; a weapon at the hip; the drawn
flag; drafted without the flag still at the hip; the flag over empty hands still the bare hands; a
bandit read the same way; an animal and a pawn that has gone; and the gear row agreeing with the
Health tab for each of the four weapons and the bare hands. Negative controls, each seen to fail and
then restored:

- the drawn flag ignored (2 failures);
- drawn read from the draft instead (2);
- the bare hands named by a literal rather than the registry (3).

**Open.** The tab names on the pane ("Needs", "Skills", "Gear", …) and their disabled reasons are
C# literals and not registry keys. That is older than this seam and outside
`RegistryTests.NoPlayerFacingNameIsWrittenInCSharp`'s six namespaces. Whoever enables the Gear tab
should move the tab names into the registry in the same commit.

### 9e. The dead are not targets (built 2026-09-24, `claude/combat-crit`)

Owner: *"You could still attack a pig after it died — make a guard for this for now — check bandit
does this."*

**What let the owner attack the dead pig.** Reproduced before anything was changed. The
simulation never let anybody order or choose an attack on a pawn that had left the board: the order
is refused on a missing or dead pawn, and every automatic choice asks `Melee.IsStanding`. What
there was:

1. **A downed pig reads as a dead one, and attacking it is an order to the death.** A hog goes down
   at nought and dies only at −30, half its pool again. Lying down, it looks like the corpse it will
   become: the corpse director even finds a pawn killed where it lay already lying rather than
   falling. A right-click on it is accepted as `ToTheDeath` (§6A.8), by design. That is the one way
   a corpse is made, and it takes three or four more machete blows. **This is almost certainly what
   the owner did**: the pig "died", and the colonist went on hitting it.
2. **Every attacker carried its job on the dead for a tick.** The guard below caught it on every
   brawl before any fix: *"keeps an attack job on 5, who is gone (dead)"*, on six of six seeds. The
   pawn is removed at the end of the tick it dies, and each attacker only noticed on its own next
   tick. A strike clip begun before the death also plays through over the body; that is
   presentation, and short.
3. **A swing in the air on the killing tick** falls on the dead pawn's cell as a `Miss`. It began
   against the living, so it is kept, as §9b's "no event lies" puts it.

**What is guarded:**

- **The order.** `OrderAttack` is refused (`NotPermitted`) on a dead pawn still on the board — killed
  between ticks, which is when an order can meet one — on a pawn gone from the board, and on a
  corpse, which is not a pawn and names nobody. The rule was already there; it is now pinned
  (`TheOrderIsRefusedOnTheDeadAndTheGone`, which failed with the `IsDead` check withheld).
- **The job ends the tick its target goes.** `PawnRegistry.Despawn`, the one way off the board, now
  calls `CombatSystem.EndAttacksOn`. That ends every attack on the pawn through `JobSystem.Interrupt`
  (keeping the step in hand) on the tick it dies, becomes a corpse, or walks off an edge
  (`AnAttackEndsTheTickItsTargetLeavesTheBoard`). It scales with the pawns on the board, once per
  pawn that leaves.
- **Every automatic choice skips the dead**: the hunt, self-defence and the threat beside her, an
  animal's revenge, and the drafted hold. `EveryAutomaticChoiceSkipsTheDead` asks each with a dead
  pawn still on the board, with the living as the control. It failed on each check withheld in
  turn: `IsThreatTo`, self-defence's retaliation, the animal's revenge, and the hunt.

**The guard** is `DeadTargetGuardTests.NobodyFightsTheDead`, fast tier, six cases, plus
`NobodyFightsTheDeadOnManySeeds` in the Long tier (24 more). Each case is a fight to the death:

- colonists with machetes against bandits, and colonists against hogs, with a player who keeps
  ordering every drafted colonist on to the nearest foe, standing or down, and who right-clicks every
  body the moment it dies;
- bandits and hogs, with no player: the hogs are set on the bandits and the bandits hunt a
  colonist. This is the "check bandit" case.

Half the fights land blows at ten times the damage, so pawns die from standing with several
attackers on them.

After every tick the guard fails if anybody holds an attack job on a pawn that is dead or gone. It
also fails if any swing, blow, dodge, stun, critical or knockback is published against a pawn after
its death was. The one exception is the same-tick `Miss` above. It also checks that every attack
order sent on a body was refused.

With `EndAttacksOn` withheld, all six fast cases failed (two to six violations each), and so did
the Long sweep. **Bandits did it too**: on seeds 2, 5 and 6, bandits kept their attack on the
colonist they had just killed. No hog dies in the bandits-and-hogs mix, because a bandit never
answers a hog (§6A.6). What that mix guards is the hogs' revenge and the bandits' hunt.

**Open.** Whether a downed animal should look different from a dead one, or whether a right-click on
a downed animal should ask before finishing it, is the owner's call. The simulation's rule, that a
corpse is made by an order on a downed pawn, is unchanged.

### 9f. Health on the cards

Built 2026-09-23 on `claude/combat-cards` (from `claude/combat-c2-r3`). The owner's words:
*"Their health needs to be also displayed on their colony stats as it appears above them"*.

**It is the card's only bar, not its fourth.** The interview recorded "a fourth bar", but the roster
card has carried no need bars since 2026-09-17, when the owner took mood, food and rest off it to
fit more colonists in the strip (`HudLayout.CardWidth`). So a card is now the face, the name and
one health bar under the name. Putting the three need bars back is a separate decision for the owner.

**What it reads** (`RosterModel.Health`, `RosterCard.Health` / `HealthInk` / `Downed` /
`HealthWord`). The bar is always drawn. The bar over a colonist's head is drawn only while
`odyssey.pawn.hp` is published, so the card cannot use that rule. It uses the Health tab's rule
instead: `odyssey.pawn.hp.max` is published for every person always, and a pool with no hit points
beside it is a whole colonist (§5d). The fill is the hit points over the pool, clamped, in
thousandths. The ink is `CombatFeedbackModel.HealthBarColour`, the one owner of the bar's colours,
so the card and the bar over her head change colour on the same hit: green, amber below 60 %, red
below 40 %. **Downed is read from the flag, not from the hit points.** The bar is empty and red,
whatever is left of the −50 % a downed pawn may sink to, and the word is `ui.status.downed`. A frame
with no pool (from before combat, or built by hand) reads −1: the track with no fill, never a guess
at whole. Each card costs two O(1) aspect lookups and a flag test per refresh, with no allocation.

**Where it is drawn, and why so thin.** It is a 4 px bar the full width inside the padding, one
pixel under the name (`HudLayout.CardHealthGap`, `CardHealthBar`). The card went from **89 to 94**,
and `CardHeight` is now written as the sum of its parts. **Five pixels was all the coverage ceiling
had left.** A full one-row strip at 1280 × 720 is 480.7 px wide, and the resting HUD there was
19.69 % against the 20 % `CoverageCeiling`. The first cut, a 10 px bar under a 3 px gap (card 102),
measured **20.37 %** and failed `TheStripIsAlwaysOneRowAndNoFurther`. The owner had already
declined raising the ceiling for the name pool (2026-09-18), so the bar took the room there was. At
94 the same HUD is **19.95 %**. **The roster card now spends the last of the ceiling**, so the next
pixel any resting region gains has to be paid for. A 4 px bar is too thin to hold a word, so
**"Downed" goes across the foot of the portrait**, on a plate in the bar's red at 85 %. The job
badge is added after the plate, so it still sits on top at the right. The downed card's track is
tinted the same red at 35 %.

The drag ghost (`.card-drag-ghost`) keeps 89 and no bar. It is a picture of who is being moved, not
a card, and it is still centred on `CardHeight`, so it rides 2.5 px higher than it did.

**Tests.** Fast tier: `RosterHealthTests` (14 cases) covers a whole colonist being full and green,
the fill and each of the three bands at their edges, the card agreeing with the overhead bar at every
hit point from −50 % to past the pool, a downed colonist at −20 %, 0 and a stale +35 % (empty, red,
"Downed") against the same numbers without the flag, no pool reading −1, and each card on a page
reading its own colonist. The stylesheet now pins `.card__name`'s gap and line and `.card__health`'s
gap and height to the model (`HudStyleSheetTests`). Negative controls, each seen to fail and then
restored:

- a pool with no `hp` read as nought, not whole (2 failures);
- the downed flag ignored (4);
- the stat green `HudTheme.Good` in place of `HealthBarColour` (9);
- `.card__health` set to 10 px in the sheet (`EveryAnchorInTheSheetIsTheNumberTheLayoutModelUses`).

**Do not undo by tidying.**
- **Never draw the card's bar only where the overhead bar is owed.** The card is always shown, and
  the absent `hp` beside a pool is *whole*, not missing.
- **The ink is `HealthBarColour`**, never `HudTokens.NeedBand` and never a colour in the sheet. The
  need bars and this bar share thresholds, not inks (§8a).
- **Downed is the flag.** A frame can carry a positive `hp` on the tick a pawn goes down.
- **Growing the card is a coverage decision**, not a styling one. Read `HudLayout.CardHeight`
  before adding a pixel.

**Where.** `Hud/RosterModel.cs`, `Hud/HudLayout.cs` (`CardHeight` and its parts),
`Presentation/Ui/HudShell.Panels.cs` (`SyncCardHealth`, `NewCard`), `Presentation/Ui/HudShell.cs`
(`CardView`), `Presentation/Ui/Hud.uss` (`.card`, `.card__health`, `.card__health-fill`,
`.card__downed`). **Never compiled here**: `HudShell.Panels.cs`, `HudShell.cs`, and the sheet has
not been loaded by Unity.

**Open, for the playtest.**
- Whether a 4 px bar reads at a glance across the strip, or whether the owner would rather spend
  more of the ceiling (a 6 px bar is card 96, about 20.06 %, and needs `CoverageCeiling` moved).
- Whether "Downed" across the face reads as a state or hides who it is.
- Whether the owner wants the need bars back on the card now that it has a bar again.

### 9g. The sound of a blow

**Owner, 2026-09-23**, supplying four recordings from Pixabay: a violent sword slice — *"critical
hits with a sword. To be played as sword is swinging"*; two swing whooshes — *"sword variation
sounds to be played during the relevant moment of the swoosh (not too early). 2 variations"*; and a
cinematic thud — *"use this for someone get hit melee (default for now). Play it as the weapon
connects."* And: *"This all needs to be coordinated at the right times for effect."*

**The interview's answers (owner):**

1. **Whoosh: every swing with a weapon** — bat, crowbar, machete, arc blade — hit or miss. **Fists
   and animal bites are silent.** Two takes, one picked at random per swing. **Its loudest moment
   lands about 0.1 s before the blow connects**: never early, never on top of the thud.
2. **Critical slice: the blow's outcome is settled when the swing's wind-up starts.** The simulation
   publishes `CombatEventKind.SwingCritical` (11) **instead of** `Swing` when the blow it has rolled
   will land critical; `Amount` is the wind-up, as a swing's is. With a **sharp** weapon (machete,
   arc blade) the slice plays **instead of** the whoosh, timed so its biggest moment lands on the
   impact. A **blunt** critical (bat, crowbar) keeps the whoosh.
3. **Thud: every landed hit**, any weapon, fists and bites too, and under the slice on a critical;
   its transient exactly on the impact. **Misses and dodges are the whoosh alone.**
4. **Placed in the world like the axe**: 3D, fading with distance, so every master is mono.

**The one timing fact everything rests on: the impact is known when the swing starts.** A swing is
published at the tick its wind-up begins with the wind-up in ticks, and the blow lands at the one
plus the other — the arithmetic the drawn swing times its contact by (§6B). So a whoosh can start
*before* the blow, which is the only way its peak can come before the thud. The thud cannot be
scheduled the same way, because whether a blow lands is not published until it does; it plays on
the `Hit` event's frame, and the file is cut so that frame is its transient.

**The bake** (`tools/audio/bake_combat.sh`, its header holds every measurement). Each source is cut
at a fixed head time — not a silence threshold, because the offset of the loudest moment is a timing
constant and a threshold moves it — downmixed to mono **first** (a limiter ahead of the downmix held
each channel to −3 and the sum then peaked 3 dB over it), tail-trimmed with a fade, and levelled on
the loudest 400 ms momentary loudness against a −3 dBFS ceiling measured with astats. The two
whooshes are cut so their peaks line up, so one constant times both.

| File | Length | Loudest 10 ms, middle | Peak | Max M | Note |
|---|---|---|---|---|---|
| `combat-whoosh.wav` | 0.200 s | **0.041 s** | −5.1 dBFS | −21.0 LUFS | the swing, take one |
| `combat-whoosh_01.wav` | 0.180 s | **0.041 s** | −4.5 dBFS | −21.0 LUFS | take two, lined up with one |
| `combat-crit-slice.wav` | 1.100 s | **0.065 s** | −3.0 dBFS | −16.8 LUFS | the ceiling binds; the 2.2 s ring cut to 1.1 s |
| `combat-hit.wav` | 0.750 s | **0.013 s** | −3.0 dBFS | −18.5 LUFS | lifted 7.5 dB into a limiter: −23.5 → −18.5 to the ear |

The slice's loudest *sample* is later (0.24 s, the body of the cut), but its loudest 10 ms is the
edge meeting the target, which is the moment the owner means. The balance against the axe is baked:
at one catalogue Volume (0.65) the thud sits about 6 dB over a felling blow at the same distance
and the whoosh about 4 dB over — a fight is heard over woodcutting, not under it.

**The timing model** is `Hud/CombatSoundTiming.cs`, Unity-free, with the schedule beside it
(`CombatSoundSchedule`):

- `WhooshPeakSeconds = 0.040`, `SlicePeakSeconds = 0.065`, `ThudPeakSeconds = 0.013` — the bake's
  numbers. **A re-bake that moves them changes them in the same commit**; the EditMode
  `CombatSoundTests.EachClipIsLoudestWhereTheTimingSaysItIs` reads the imported clips and fails
  otherwise.
- **Real time at the current speed.** The clock is the last tick run plus the frame's fraction
  towards the next (the bootstrap's `_tickAlpha`), and a tick is `1 / (60 × speed)` s: at ×3 the
  impact is three times nearer. Paused, the rate is 0 and nothing waiting starts, or is let go.
- **Never early, at most a frame late.** Each frame a waiting cue starts if starting it now lands
  its peak no earlier than it belongs — 0.1 s before the impact for the whoosh, on it for the slice.
  The frame before, it would have been early. So a whoosh peaks between 0.1 s and 0.1 s less a frame
  before the blow, and a slice between the impact and a frame after.
- **Seen too late.** A whoosh whose peak would land within **0.04 s** of the blow is let go rather
  than smeared into the thud (`WhooshClearanceSeconds`, INVENTED — about where two onsets stop
  fusing). A slice is played up to **0.1 s** late (`SliceLateSeconds`, INVENTED), because a critical
  is the headline and a little late beats silent. **The quickest swing at the top speed cannot be on
  time and is still heard**: the machete's 22 ticks at ×3 is 0.122 s, less than the whoosh's 0.14 s
  to its lead, so it starts on the frame it is read and peaks about 0.07 s before the blow.
- **A swing replaces its swinger's last**, and a swinger **downed, killed or knocked back** loses the
  swing in the air and its sound. The last is an assumption about round 3's knock-down (that it
  interrupts a wind-up); if it does not, drop `KnockedBack` from `CombatSoundSchedule.Hear`.
- **Held weapon or not** is `BloodSides.IsHeldWeapon`, and **sharp or blunt** `BloodSides.IsSharp` —
  the blood seam's table, read once off the content, so the Defs stay the one owner of which weapon
  is which (§7d). An event's weapon is an item def with an attack, or −1 for fists and bites.

**Presentation.** `CombatFeedback` asks the schedule about every event it hands on
(`Sounds.Hear`): a swing is scheduled and makes no sound on its own frame, a hit returns the thud,
anything else plays its named sound as before (`SoundIds.ForCombat`, where a swing is now null).
After the frame's events it starts every cue that is due (`SoundTheDue`), from the swinger's drawn
feet — or, if the swinger has left the frame, the cell the blow was aimed at. The thud plays from
the struck. The schedule is a fixed array of 64, one per fighter at most, so nothing allocates and
the cost is the fighters swinging, never the colony; it is cleared on a world change and at
teardown. `PawnFigureDirector.OnCombatEvent` draws a `SwingCritical` as a `Swing`, and
`CombatFeedback.WhereOf` hears it from the swinger.

**The catalogue** (`AudioSetup`): three rows on the Effects bus, placed at the axe's 20–200 m.
**No cooldown on any of them**: the director's cooldown is per sound, so any cooldown swallows a
second fighter's blow read in the same frame, which at ×3 in a group fight is most of them. The
bound is the voice budget, and the priorities say who yields — a whoosh (140) gives its voice to a
thud (110) or a slice (100). The whoosh gets 6% pitch and 12% level variance on its two takes, the
thud the axe's 7% and 15% on its one, the slice none: a pitched slice moves its peak off the impact.

**`SwingCritical` was added to `Sim.Contracts/Views.cs` here, ahead of the simulation change that
publishes it** (another lane). A merge keeps exactly one copy. Until the simulation publishes it,
nothing slices: every critical is a `Swing` and whooshes.

**Tests.** Fast tier `CombatSoundTimingTests` (17): the whoosh peaks a tenth before the blow and
never earlier at ×1, ×2 and ×3 and at 60 and 144 frames a second; the slice on the impact; the same
swing heard three times sooner at ×3; paused nothing starts and nothing is let go, resumed it starts
on time; a whoosh seen too late is let go and one merely late plays at once; the quickest swing at
the top speed is still heard clear of the blow; fists and bites silent, critical or not; a sharp
critical slices and a blunt one whooshes; every landed hit thuds and a miss or a dodge does not; a
downed, dead or knocked-back swinger loses its sound; a new swing replaces the last; the schedule
overflows and clears. **Negative controls seen to fail** (each broken alone, then restored): a cue
played the moment it is seen (10 fail); the speed ignored (7); the pause ignored (1); a sharp
critical that whooshes (5); fists that whoosh (1); a late whoosh played on the thud (1); a
knocked-back swinger that keeps its whoosh (1). EditMode `CombatSoundTests` (2) holds the shipped
rows — placed, Effects, mono, no cooldown, two whooshes, an unpitched slice — and reads each
imported clip's loudest 10 ms against the constants, to 5 ms. **Red until `AudioSetup.Build` has
run**, because that writes the rows. `CombatDrawnTests.EveryMomentOfAFightHasItsSoundOrNone` now
says a swing is null on its frame and names the three cues.

**Never compiled here:** `CombatFeedback.cs`, `SoundIds.cs`, `OdysseyBootstrap.cs`,
`PawnFigureDirector.Combat.cs`, `AudioSetup.cs`, `CombatSoundTests.cs`, `CombatDrawnTests.cs`. The
new WAVs have no `.meta` until Unity imports them; the integrator runs
`Odyssey.EditorTools.AudioSetup.Build` and commits the metas and the rewritten
`AudioCatalogue.asset`.

**Licence:** all four are Pixabay, under the Pixabay Content License — use in a product without
attribution, modification allowed, no redistribution on their own — committed with the game as §2i's
draft is. **The owner to confirm the sources.**

**Open.** Whether 0.1 s is the lead the ear wants at ×1 (it is a number the owner gave, and a
playtest settles it); whether the thud's lift is enough beside the axe; the misses', downs' and
deaths' named sounds (`CombatMiss`, `CombatDown`, `CombatDeath`) are still in no catalogue; a swing
abandoned mid-wind-up by anything but a fall (its target dying, say) still whooshes (or slices), which is
honest — the arm was already moving.

### 9h. Spawns spread (built 2026-09-24, `claude/combat-spawn`)

**Owner, 2026-09-24:** *"when you spawn a bandit they don't spawn from same tile so quickly — spawn
on free tiles around if quick succession."* A debug spawn used to land on the walkable cell nearest
the camera's column, whoever already stood there, so bandits spawned in quick succession stacked
on one tile. `PawnRegistry.FreeSpawnCell` keeps that cell when nobody stands on it. So the first
spawn lands exactly where it always did, and `SixBanditsSentInOneTickStandOnSixTiles` pins that.
Otherwise it takes the nearest free tile in rings of up to 4 cells round it, in a fixed scan order.
Each column is tried on the spawn layer, then one up, then one down, the same lift a move order
uses. The tile must be standable, unoccupied, and reachable from the spawn point, so nobody arrives
walled into a pocket. It applies to **every kind**, not only bandits, because a shared tile is the
same fault whoever stands on it.

It is a debug command, so it costs a scan of the pawns per candidate tile and nothing per tick. All
three `SpawnSpreadTests` fail with the spread withheld. `DebugIntentTests`' fall-down-the-column test
now aims three columns away from its own colonist, because the column it used was hers and is now
spread off.

| *"This all needs to be coordinated at the right times for effect"* — four recordings: a critical sword slice, two whooshes, a thud | **Whoosh** on every swing with a weapon, hit or miss, peaking about 0.1 s before the blow; fists and bites silent. **Slice** instead of the whoosh on a sharp weapon's critical, peaking on the impact; a blunt critical keeps the whoosh. **Thud** on every landed hit, fists and bites too, crits under the slice. Placed in the world like the axe. | §9g |

### 9i. The debug menu for testing a fight (built 2026-09-24)

**Owner, 2026-09-24:** *"make the debug menu bigger, make a category for each type of spawn —
items, enemies ... also include an option to wield every colonist with a random melee weapon."*

- **Wider:** 280 px to 460 px (`.debug` in `Hud.uss`).
- **The Spawn tab is grouped under headings**, in two columns:
  - on the left, who: **Colonists** (Spawn colonist, **Arm every colonist**), **Hostiles** (Spawn bandit, **Spawn 3 bandits**), **Animals** (midden hog, duct rat);
  - on the right, what: **Weapons** (the four) and **Items** (50 wood, stone, meals).

  The Items rows moved here from the Cheats tab, which keeps the clock, the crops, the overlay and
  the trace. The headings are the Keys tab's `.settings__section`, so nothing is new in the
  stylesheet but the width.
- **`DebugDirector.SpawnRow`** gained `Group` and `Repeat`. The table is the fast tier's
  (`TheSpawnTabIsGroupedUnderNamedHeadings`, `TheNewRowsSendWhatTheySay`), and the shell only lays it
  out. *Spawn 3 bandits* is one intent sent three times at one column, and the simulation spreads
  each onto its own tile (§9h).
- **`IntentKind.DebugArmColonists`** (`PawnRegistry.HandleDebugArmColonists`) gives every colonist
  who is standing and holds nothing one of the content's melee weapons.
  - The weapon is rolled on her own stream (`PawnPurpose.DebugArm`), so a seed deals the same arms
    every time.
  - It is made beside her and taken straight into her hand, as a bandit is armed at spawn. Being
    undrafted, it then hangs at her hip (§8b).
  - A colonist who already holds a weapon keeps it, a downed one and animals are skipped, and a
    second click arms nobody (`AlreadyInThatState`).
  - `DebugArmColonistsTests` fail with the handler unregistered.

## 10. Blood (built 2026-09-24, `claude/combat-blood`)

The unit §7d names, built to the owner's rules there and to the seven points it hands on. Nothing
here reaches a cell, a save, the hash or the simulation: a load starts clean and a player has
nothing to clean.

### 10a. What is drawn

- **A spurt** on every landed hit: a handful of drops thrown from the wound along the blow, falling
  under gravity. **Sharp** throws more and faster drops in a narrower fan, **blunt** fewer and
  slower. Where the lead drop lands it leaves **one mark**: a sharp hit an elongated **splatter**
  laid along the blow, with its satellite drops built into the shape; a blunt hit a smaller, round
  **spot**. One mark per hit, not one per drop, or the cap would be a dozen hits.
- **A pool** under a body gone down (0.6 of the size) or dead (the whole): it waits for the fall,
  then spreads to full size over about 8 s of game time.
- **Misses, dodges, swings, stuns and recoveries draw nothing** — `BloodModel.For`, unchanged.

### 10b. The numbers (all INVENTED, for the playtest)

| | Sharp | Blunt |
|---|---|---|
| Drops | 4 + damage/2, at most 16 | 1 + damage/4, at most 5 |
| Throw | 2.5–4.5 m/s, ±30° | 1–2 m/s, ±60° |
| Mark | radius 0.22 + 0.02 × damage, at most 0.55 m; 1.7 × as long as wide | radius 0.12 + 0.01 × damage, at most 0.28 m; round |

- **A pool**: radius 0.45 × the body's length × the size factor — 0.81 m for a person's death,
  0.27 m for a rat's. The body's length is the animal's drawn box, or 1.8 m for a person.
- **The fade**: a mark holds full strength for the first quarter of a day (15,000 ticks), then
  thins to nothing at 60,000, **by the simulation's tick**. A pause holds it, and speed three fades
  it three times as fast (§7d point 3).
- **The cap**: 200 marks, oldest first (point 4).
- **Colours**: drops and splatter a deep red, pools darker, both translucent enough for the grass to
  show through a thin mark.

### 10c. Decisions this unit made

- **The seam changed shape**, because the first implementation of it needed two things it did not
  carry. `Spurt` now takes the struck pawn's **feet** beside the wound (the drops need a ground to
  land on), and `Pool` takes **who** it is under and **the body's length** (point 2, and point 7
  below). The seam is presentation's own; nothing outside `CombatFeedback` calls it.
- **Where a death's pool goes (point 7).** When the event arrives the body has not fallen, and which
  way it falls is decided by a clip, not the simulation. So a pool **waits 1.2 s of game time**,
  then asks the figures where the body is: the midpoint of its feet and its head, which is under the
  torso whichever way it fell. With no figure (a pawn beyond the figure cap, a checkout without
  the art) it falls back to the feet it was given.
- **A mark stands on what is under it (point 6).** A drop that lands over a cell with nothing to
  stand on (off a terrace edge, into water, against a wall) leaves no mark. And every mark is asked
  again each frame: **a floor taken away takes its blood with it**, rather than leaving a stain
  hanging in the air. It does not fall, because a stain that falls three metres and lands intact is
  stranger than one that goes.
- **The slice hides a mark on a layer not drawn**, by the corpses' own rule (`LowestDrawnLayer`
  to `HighestVisibleLayer`). The mark is kept, and shows again when its layer is drawn. Drops in the
  air are hidden the same way.
- **Draped, not lifted.** A mark is ground-fixed, so it is placed with `GroundRelief.Drape` and
  lies along the relief (the standing rule).
- **Drops move by real time while the game runs**, like the floating words, and freeze on a pause.
  They are half a second long; the marks they leave are timed by the tick.

### 10d. What it costs

- **Draws in fade steps, never in marks** (`bug-patterns.md` P10). Marks are bucketed by shape
  (splatter, spot, pool) and by one of six fade steps, each bucket one instanced call; drops are one
  more. The ceiling is **19 calls** whatever the fight, and a colony with no blood submits
  **nothing**.
- **Per frame it scales with the marks (at most 200) and the drops in the air (at most 512)**,
  never with the board or the colony (`process.md` §3).
- **Measured** (`FrameTimeTests.TheBloodAtItsCap`, one run, 640 x 480, RTX 5070 Ti): 250 hits
  laid round the start, 200 marks standing — **3 draw calls** and the frame **2.40 → 2.53 ms**, the
  whole difference in `Overlays` (0.011 → 0.097 ms).
- **The fight test reads the ground only once the air has emptied.** Drops fall in real seconds,
  about 0.7 each, and a brawl in `TheFrameWithAFightInView` lasts well under one on this machine, so
  read at once it found 54 drops up and no mark — and, with the refusal counters added to find out
  why, **nothing refused**: the drops had simply not landed. `BloodDirector.Refused*` stay, as the
  first thing to read when a fight leaves less blood than it should.

## 11. Rescue (C4, built 2026-09-24, `claude/combat-rescue`)

A downed colonist heals only in a bed (§1), and until this unit nothing could carry her to one, so a
colonist who went down stayed down. Built to the owner's answers of 2026-09-24:

| Question | Owner's answer |
|---|---|
| How long does a rescued colonist stay in bed? | **Until whole.** Not up at 15 %: a 15 hp colonist does not walk back into the fight |
| No free bed? | **Leave her**, and say why. Building a bed is the player's answer |
| How is she carried? | **Cradled in the arms**, the downed body lifted to the carry cradle |

### 11a. The job

`Job_Rescue` (handle 21, `RescueJobDriver`), four toils, walked in **`TraverseMode.Hauler`** like a
haul, because a colonist with a body in her arms does not climb a ladder:

1. **Walk to the patient.** The patient is the rescuer's `Pawn.CombatTarget` (saved and hashed, the
   field the contracts put there for this); the job's `TargetCell` follows her if she is moved.
2. **Lift**: 45 ticks (INVENTED) stooped over her; then `patient.CarriedBy = rescuer`.
3. **Carry to the bed** (`Job.DestCell`, the bed's head cell). A carried patient is **where her
   carrier is**: her `Cell` is set to the carrier's every tick and she has no path of her own.
4. **Lay her down**: 45 ticks; she is set on the head cell, `CarriedBy` goes back to nought, and the
   **bed reservation passes to her** (§11c).

Every other end (a failure, a knockback, the carrier downed or killed, a draft toggle, a forced job)
runs the driver's `Cleanup`, which puts a carried patient down on the carrier's cell, or the nearest
standable cell if that one is mid-step, and clears `CombatTarget`. **Nobody is left in a pair of
arms.** A patient who dies or recovers on the way fails the job.

### 11b. Who, and to which bed

- **Only a downed colonist is rescued.** An animal recovers where it lies; a downed bandit stays
  down until killed (§1). Capturing one is a later unit.
- **The bed**: the patient's own bed if it is free, else the **nearest free unowned bed**, measured
  from the patient; never a bed somebody else owns. *Free* means nobody holds its cell reservation,
  which after §11c includes a patient lying in it. Reachable from the rescuer to the patient and
  from the patient to the bed, both in the hauler's mode.
- **Two rescuers never take one patient**: a new reservation kind, `ReservationTargetKind.Pawn`,
  keyed on the patient's id. Additive; no save holds one before this unit.
- **The order** (`OrderRescue`, right-click a downed colonist with drafted colonists selected;
  already routed to the nearest by `CombatOrders.Route`): a drafted, standing, unbroken colonist;
  a downed colonist who is not already carried or claimed. After it she is still drafted, and
  holds where she laid the patient.
- **The automatic rescue** (`RescueWorkGiver`, the emergency giver in the Rescue column): the
  nearest downed colonist the scanner can reach and bed. It answers no, touching nothing, when
  nobody is down — so a colony that never fought scans one flag per pawn and no golden moves.

### 11c. In the bed until whole

- **A downed colonist lying in a bed gets up when whole**, not at `downedRecoverAtPerMille`. That
  threshold is now the **animals'** rule alone: a colonist heals only in a bed, so "up at 15 %"
  could only ever have happened there. No new state, no new job: while she is down her needs are
  paused (§5c), she heals at `bedHealPerDay`, and at 100 % the combat pass stands her up
  (`Recovered`) exactly as before. **Five days at today's invented rate.**
- **The bed is hers while she lies in it.** The rescuer's cell reservation on the bed passes to the
  patient on the lay (`Reservations.Reserve` in her name, added to her `HeldReservations`, which
  are saved, hashed and re-reserved on load). `Job_Downed` holds it until she gets up, when
  `EndJob` releases it like any other. So `TrySleep` and the next rescuer both see it taken. Without
  it, the next tired colonist would have climbed in beside her.
- **Ownership is not claimed.** A patient in an unowned bed is not given it; `BedOwnershipChanged`
  wakes sleepers, and a sickbed is not a bedroom.

### 11d. No bed: say why

A downed colonist, not carried and not in a bed, for whom no bed is free, publishes
`odyssey.pawn.rescue.nobed`. The alert model raises **`ui.alert.norescuebed`** — *No bed for the
wounded* — once per colonist, and it clears the moment a bed frees or is built. The order is refused
(`NotPermitted`) and the giver answers no, so she stays where she fell (owner). The aspect is
computed at publish for downed colonists only: a colony with nobody down pays one flag test a pawn.

### 11e. What is drawn

- **Cradled.** A carried patient's figure is placed at the carrier's carry cradle
  (`CarryPose.Cradle`, measured off the palms), lying across the arms at right angles to the
  carrier's facing, in the lying pose. The carrier's arms take the item carry's scoop. The carrier
  is found from the patient's side: the pawn on `Job_Rescue` whose order target she is.
- **In the bed.** A downed colonist on a bed's head cell is laid in the bed with the sleep pose
  (`AimSleep`), rather than playing the downed loop on the floor through the frame.
- **No lock-on ring.** `LockOnRings` asks the rescuer's job and draws nothing for a rescue: the ring
  says *attack*.
- **Words.** *Rescuing* (`ui.status.rescuing`) is on the activity line; the patient's stays *Downed*.
  Whether a colonist healing in bed for five days should read *Downed* is the playtest's question.

### 11f. Do not undo by tidying

- **A carried patient's `Cell` is her carrier's.** Everything that asks who is in a cell scans pawns
  and reads `Cell`; a patient at `-1` would be off the board to all of them. `IsCellOccupiedByStandingPawn`,
  `PawnEviction.Occupant` and `TrappedPawnSystem` skip a carried pawn: she is not standing there.
- **`Cleanup` puts her down on every exit.** `CarriedBy` is saved; a patient left with it set after
  her carrier's job ended would be carried by nobody, for ever.

### 11g. Measured (2026-09-24)

- **The pose, under the real bootstrap** (`RescueFigureTests`, the head bone, on a figure whose
  standing head is 2.10–2.15 m): **0.18–0.20 m** over the ground downed on the floor; **1.63–1.66 m
  over the carrier's feet and 1.07 m to her side** in the arms, mid-walk; **1.02–1.04 m** over the
  ground in the bed against a mattress top of 0.70. The arms are the load's scoop, which holds the
  palms level with the chest (1.52 m on this figure), so she lies at chest height with her head
  half a body to one side: a cradle carry, and the playtest's to judge. `CradleSink` and the scoop
  are the two levers.
- **Three readings of the carry were the test's, not the pose's**, and each is why the test reads
  as it does now. Against the ground of her cell, a carrier climbing a terrace ramp is drawn up the
  riser before the cell changes (2.93 m). At speed three a bed four cells off is reached inside the
  sample's wait, so "carried" was her head on the pillow (1.04 m, 0.05 m from the carrier). And one
  second after the lift caught the scoop still easing in (1.27 m). The test measures against the
  carrier's feet, at speed one, asserting she is still carried and the carrier still walking.
- **Controls seen to fail**: the carried-patient pass off leaves her on the ground at the carrier's
  feet (0.36 m); a bed not counted as a cradle plays the floor loop through the bed frame (0.21 m).
  In the simulation: the bed not passed, no set-down, no patient reservation, the 15 % threshold and
  no carried sync each fail their test; the alert with no rows built fails its.
- **Read in real seconds, not frames.** A batch frame is a couple of milliseconds, so ninety frames
  read the body mid-fall, and one second after the lift read 1.27 m on one run and 1.79 on another as
  the scoop eased in. The test waits in real time (`docs/lessons.md`, a frame is not a tick).
- **A Long soak saw it first.** `BanditSoakTests` carried a downed colonist to bed and she healed
  past 15 % while down, which its invariant forbade; the invariant now takes the new line.

### 11h. Known and left

- **A carried patient beyond the 64-figure cap** is drawn by the baked far form, standing at her
  carrier's cell, as a downed pawn out there already is (§6F).
- **A patient's needs are paused for the whole of her bed rest** (five days at today's rate), because
  she is down; nobody feeds a patient yet. A later unit, if the playtest wants it.
- **She reads *Downed* while she heals in bed.** Whether that should say something else is the
  playtest's question (§11e).
- ~~**A bed demolished under a patient** kept her reservation on the old head cell~~ — **fixed
  2026-09-25** (`claude/combat-bed-release`). She still stays down on its cell and is rescued again
  to another bed, but `ConstructionGrid.Demolish` now releases a downed pawn's claim on the bed it
  removes, from the table and from her own list together, so a bed raised on that cell is free at
  once. Only a downed pawn: a sleeper's and a rescuer's jobs ask about their bed and let go
  themselves. `RescueTests.ABedDemolishedUnderHerLetsGoOfHer`, seen failing without it.

## 12. Friendly fire (C5, built 2026-09-24, `claude/combat-friendly-fire`)

**The owner's rows (§1):** *friendly fire — the victim remembers being attacked (−8, one day); any
colonist's death is felt by every colonist (−6, three days); no opinions yet*; *Ctrl+right-click a
colonist attacks it*; *a colonist struck by a colonist fights back*. Built from `main` at
`d1d64891`, fast tier and Long tier only, no Unity.

### 12a. What was already there

Two of C5's three parts arrived with C2 and were checked against the code rather than rebuilt:

| Part | Where it lives | Already tested by |
|---|---|---|
| **Ctrl + right-click on a colonist is an attack** by every selected drafted colonist but her, and wins over the rescue | `CombatOrders.Route` (§6C), fed the Ctrl key by `SelectionPresenter.Order` | `CombatOrdersTests.CtrlRightClickOnAColonistAttacksAndWithoutCtrlItIsAMove`, `ACtrlClickedColonistDoesNotAttackHerself` |
| **The order accepts a colonist as its target** — no flag, decided at the integration (§6E) | `JobSystem.HandleOrderAttack` (§6A.8) | `AttackDriverTests.TheAttackOrderIsForADraftedColonistAndAPawn`; the duel in that file is two colonists |
| **A colonist struck by a colonist fights back** — by anybody, in fact, for `retaliationTicks` (§6A.6) | `CombatSystem.React`, `SelfDefenceThinkNode` | `HostileTests.AColonistStruckRetaliatesAndADraftedOneLeavesItToTheHold` |

What nothing tested was the three **joined**: a Ctrl-attack from the order to the answering blow.
`FriendlyFireTests.ACtrlAttackOnAnUndraftedColonistIsFoughtBackAndRemembered` sends the order, and
the colonist she attacked — undrafted, at her own business — turns on her and swings, and carries
the memory. Nothing in the order or the retaliation needed changing for it.

### 12b. The two memories

Two thoughts, appended to `Thoughts.xml` and `ThoughtIndex` (6 and 7; appended because a thought's
index rides every saved memory), and one listener, `FriendlyFireListener`, registered in
`CombatListeners.Register` after the weapon drop.

| Thought | Given to | When | Mood | Lasts | Stacks |
|---|---|---|---|---|---|
| `Thought_AttackedByColonist` | the colonist swung at | a colonist's swing reaches her, landed or not (`SwingResolved`, since §14f; it was `DamageApplied`) | −80 | one day, 60,000 ticks, renewed by a second swing (§14e) | once |
| `Thought_ColonistDied` | every other colonist on the board | a colonist dies (`Died`) | −60 | three days, 180,000 ticks | three times, at the usual 750 ‰ each |

**The owner's −8 and −6 are points on a mood of a hundred; ours is thousandths of a thousand**
(`MoodDef`: base 500, a break below 350), so they are −80 and −60 — the scale every existing
thought is on (a night on the ground −40, a fall −60). A day is `Calendar.TicksPerDay`.

**Decisions, and why** (each ours, not the owner's, unless it says so):

- *(Superseded by §14f: a miss, a dodge and a blow on air are remembered too.)* **A blow that
  lands is an attack; a miss or a dodge is not remembered.** The hooks report only
  hit points taken (`DamageApplied`), and a memory of an attack that never touched her would need a
  fourth hook for one thought. The smallest reading of "being attacked".
- **Colonist on colonist only.** A bandit's blow or an animal's bite is not friendly fire and
  gives nothing; a colonist hurting an animal or a bandit gives nothing. A broken, drafted or
  downed colonist is still a colonist on both sides.
- *(Superseded by §14e: it renews now, so the day runs from the latest blow.)* **The same attack
  again neither stacks nor renews** (stack limit 1). That is how every thought in
  the game behaves: `Pawn.AddMemory` drops a copy past the limit rather than refreshing one. So the
  day runs from the **first** blow she remembers; a blow after the memory has gone makes a new one.
  A second attacker is the same memory, because a memory names no other pawn — that is what
  "no opinions yet" means in the data. Renewing on a repeat would be a change to `AddMemory` for
  every thought (and would move the goldens through `Thought_AteMeal`), so it is left to the owner.
- **The one who started it remembers the blows she takes back.** She is a colonist hurt by a
  colonist; which of two started a fight is not in the state, and putting it there is the start of
  opinions. So both sides of a Ctrl-attack end the day at −80. Open for the owner.
- **Every death is felt, to three.** One death costs every colonist −60; a second −45 more, a third
  −33, and a fourth nothing further while the three last (−138 at most). The break line is 150
  below the base, so a massacre brings a content colony to the edge and not over it on its own.
  *INVENTED*: the limit is the owner's to tune after a play.
- **The dead feel nothing; everybody else does** — standing, downed, drafted or broken, including a
  colonist who struck the blow. The pawn is still in the registry when `Died` is heard (the hook's
  promise, §5e), so the listener skips her by identity. A bandit's or an animal's death is felt by
  nobody.
- **Only deaths the hooks hear.** Every death in the game today is `CombatSystem.Kill`'s; a later
  way to die (starvation, a fall) must raise `Died` or it will not be mourned.
- **No name, so no wiki row.** No surface names a thought — the pane's Thoughts tab is disabled
  "until the thought log", and none of the six thoughts before these has a key. The unit that
  builds the log names all eight at once; adding two keys now would start a namespace a quarter
  full. The three content gates were run and pass unmoved.

**Cost** (`docs/process.md` §3): nothing per tick. `DamageApplied` is two comparisons and, on friendly
fire, a walk of the victim's memories; `Died` is one pass over the pawns per colonist death.

**The goldens did not move**, and could not: memories are hashed per pawn, and these two are added
only by a colonist hurting a colonist or a colonist dying, which no golden window does. The content
fingerprint moved once, for the two thoughts (the twenty-first move, `PawnContentDefTests`).

### 12c. Tests (`FriendlyFireTests`, fast tier)

Each was seen to fail with its rule withheld — fourteen breaks, one at a time: the attacker or the
victim need not be a colonist; a miss raises the hook; the attacked thought stacks twice, renews, or
is −79; the dead mourns herself; non-colonists mourn; the downed do not; any death is mourned;
deaths stack four; the listener unregistered; the order refusing a colonist target; and a colonist
not retaliating against a colonist. The last two are C2's rules, broken to show the end-to-end
test reaches them.

| Test | Claim |
|---|---|
| `TheTwoThoughtsAreTheOwnersNumbersOnOurScale` | −80 for a day, once; −60 for three days, three times |
| `AColonistHurtByAColonistRemembersItForADay` | the memory, its expiry, its −80, gone at the day's end |
| `ABanditsBlowAndAMissAreNotFriendlyFire` | the controls: a bandit's blow, a miss, a colonist hitting a bandit |
| `ASecondBlowNeitherStacksNorRenews` | one copy, the first blow's expiry |
| `EveryOtherColonistFeelsAColonistsDeath` | the survivors, standing and downed, and not the bandit, the animal or the dead |
| `ABanditsDeathIsFeltByNobody` | the control |
| `DeathsStackToThree` | three copies at most, −138 |
| `TheListenerIsRegisteredOnceAfterTheWeaponDrop` | the order `CombatListeners` promises |
| `ACtrlAttackOnAnUndraftedColonistIsFoughtBackAndRemembered` | the order, the answering blow, the memory |

### 12d. Open for the owner — answered 2026-09-24 (§14)

- ~~Should a second attack within the day **renew** the memory, so the day runs from the last
  blow?~~ Yes, on our recommendation (§14e).
- ~~Should the colonist who **started** a fight remember the blows she takes back?~~ Yes, as built.
- ~~Is **three** the right cap on mourning?~~ Keep. Whether a death should weigh more for somebody
  close still waits for opinions.
- ~~Should a swing that **missed** a colonist be remembered as an attack?~~ Yes (§14f).

## 13. Buildings as targets (C6, built 2026-09-24, `claude/combat-buildings`)

**The owner's MVP named buildings as targets from the first interview** (§1: "walls, doors,
furniture"), and §4 gave the shape: *hit points per placed edifice in a sparse store, demolished at
zero with no refund.* Simulation and interface model only, fast and Long tiers, no Unity. **No
golden moved**: nothing new is hashed while no building has been struck, and no golden window
fights.

### 13a. What was there, and what C6 adds

| Already there (§5) | Added here |
|---|---|
| `EdificeDamage` (`odyssey.edificedamage`): a sorted sparse store, saved and hashed only while it has a row, nothing calling it | the calls, a row per struck building, cleared by the one removal path; an indexer for the publish |
| `BuildingDef.maxHitPoints` on seven rows, INVENTED | the campfire, conduit, generator and heater given theirs; the material's `hitPointsFactorPerMille` read at last |
| `HandleOrderAttack` refusing `B = 0` | the building branch: accepted for a drafted colonist on a cell a target stands in |
| the attack driver failing a job with no `CombatTarget` ("C6's branch") | the building mode: stand beside, swing on the weapon's cadence, done when the building is gone |
| `CombatEventView.Target` "default when the target is a building" | reported so; one new kind, `Demolished` |
| `odyssey.pawn.order.cell` named for a building target (§5d) | published for it |
| `CombatOrders.Route` falling through to the move for a building | `CombatOrders.RouteBuilding`, after the context menu and before the move |

### 13b. What is a target

**An edifice standing in the cell whose building row has hit points** — `BuildingTargets.TryFind`,
the one owner. That is a wall, a door, a ladder, a bed, a shelf, a campfire, a generator and a
heater. Never:

- **a floor or a deck plate**, which is a slab at the cell's lower boundary and not an edifice
  (§5j), although both rows carry `maxHitPoints`;
- **a conduit**, which lives in the power grid's own layer and is not an edifice either. It was
  given its number with the other three power rows, and nothing reads it;
- **a tree** (edifices 10 and 11), which has no building row at all, and nor has anything else the
  ruined city stamps but its walls and doors (windows, pillars, stairs, vault walls, taps);
- **a site**, which is an order, not a building.

**The ruined city's walls and doors are targets**, because they are walls and doors: the rule the
owner gave names what a thing is, not who built it. Deconstruct asks `PlacedEdifice.Built` because
its refund is a yield and the city's yields are Reclaim's and Salvage's; a blow yields nothing, so
the argument does not carry across. *Our call*, and on the list for the owner (§13j). The scene
loads the meadow, so no player meets it yet.

### 13c. Hit points

- **A building's pool is its row's `maxHitPoints` times its material's `hitPointsFactorPerMille`**,
  in thousandths like a pawn's (`BuildingTargets.MaxMilliOf`). That factor had been in the stuff
  table since U27 — wood 1,000, stone 1,500, the reference's own wood-to-stone relation — waiting
  for "a durability stat" to consume it, and this is that stat. So a wooden wall stands 300 and a
  stone one 450; the city's concrete and steel carry the default 1,000.
- **The row is keyed on the record's own cell** — a two-cell bed or generator on its head, whichever
  half was struck — which is what `EdificeDamage` asked of its caller.
- **A building nobody has struck has no row**, and reads as whole.
- **The new numbers**, all INVENTED: campfire 60 (three stuff and a ring of stones, the cheapest
  thing in the table), conduit 40 (unread, above), generator 300 (the first expensive thing, a wall's
  worth), heater 100. `ConstructionContentDefTests.BuildingFingerprint` moved once for them.

### 13d. The order

`OrderAttack(cell, A, B = 0)` — the cell any cell of the building — is accepted for a drafted,
standing colonist of ours when a target stands there and she is beside it or can reach a cell beside
it. Otherwise `NotPermitted`.

- **The job carries the building by its record handle, not its cell** (`Job.DestCell`). Handles are
  never reused (`ConstructionGrid.Demolish` keeps the slot), so a wall pulled down and another raised
  on the same cell is a new building, and the old order ends rather than carrying on into it.
  `Job.TargetCell` is the building's cell she is striking at — what `WorkFocus` turns the figure to
  during the wind-up, and what the order line is drawn to. `Pawn.CombatTarget` stays 0, which is what
  says "a building" everywhere.
- **The same building again is `AlreadyInThatState`**, as the same pawn is (§6A.8).
- A building order is only ever an order: nothing unordered — the hold, the hunt, a revenge, a
  self-defence — chooses a building. *(Superseded in part by §14b: a bandit with no colonist
  to reach now chooses a colony building. The hold, a revenge and a self-defence still never do.)*

### 13e. The driver's building mode

`AttackMeleeJobDriver.TickBuilding`, taken at the branch point lane A left.

- **Where she stands is the deconstructor's stance**: on the building's layer, within one cell of
  any cell of it, never inside it (`BuildingTargets.InReach`). A wall fills its cell, so beside is
  the only place to strike it from; a door, bed or shelf is struck from beside it too, so the rule is
  one rule. Diagonals count, as they do for taking a wall apart.
- **On a side nobody else holds** (`BuildingTargets.ChooseSide`): of the cells in reach that she can
  stand on and reach, the one no other fighter holds (`Melee.Holds`) and nearest her, on the fixed
  scan the pawn sides use (§7c). Nowhere free: she waits where she is and looks again every
  `chaseRepathTicks`. No cell in reach can be reached at all: the order fails.
- **`Melee.IsInAnAttack` is widened to any standing pawn in `Job_AttackMelee`**, a building attack
  included, so a building attacker holds her side against every other fighter. It used to require a
  pawn target; with nothing else ever in the job but a pawn attack, the widening is a no-op there.
- **A building never moves**, so the side is not chosen again while she walks: she chooses when the
  attack is new, when she arrives in reach on a cell somebody else holds, and, waiting, at the chase
  cadence.
- **It ends in success when the building is gone** — demolished by her, by another attacker, or
  taken apart by a deconstructor. Only an order ever starts one, so it is always forced and never
  re-chosen on `rechooseTicks`. A job naming neither a pawn nor a building (`DestCell` −1) still
  fails on its first tick, which is the stub's contract `CombatContractTests` holds.
- **Every swing at a building is activity for the draft's quiet clock** (`DraftQuietSinceTick`).
  Without it a colonist who took more than four hours to beat down a stone wall with her fists
  (450 points at 4 a blow, a blow every 120 ticks, is 13,500 ticks) would undraft the tick it fell.
  An ordered fight with a pawn has the same gap; it is recorded here and not changed, since no
  player has met it.
- **A knockback keeps the building order**, as it keeps a pawn order (§9b): a drafted colonist
  knocked off her side by a bandit gets up and goes back to the wall.

### 13f. The blow

**A blow at a building always lands, for its damage in the spread, and nothing else** — no miss, no
dodge, no stun, no critical, no knockback. A building cannot step aside or be staggered, and a wall
is the one thing nobody misses: the reference treats a target that cannot move as always hit, and
that is the rule taken. The one roll is damage, on the pawn's own `MeleeDamage` stream
(`BuildingTargets.Resolve`, through `MeleeRules.DamageMilli`), decided when the wind-up begins and
held on the pawn to the impact (§9g). Not through `IMeleeRules.Resolve`: that decides a swing
between two pawns and reads the defender at every step, and a new interface member would break every
implementation of the seam.

*(Superseded by §14c: a blow at a building trains nothing.)* **Experience per swing, as for any
swing** (§6A.1). The design's rule has no exception for what is struck. It does mean a drafted
colonist can train Melee on her own wall; that is on the owner's list.

### 13g. The impact, and demolition

- **`CombatSystem.StrikeBuilding` is the building's `ApplySwing`**: the one method a building loses a
  hit point through. It reports `Hit` with `Target` 0 and the struck cell — so the floating number
  and the thud already work — and writes what is left into `EdificeDamage`.
- **No hooks.** `DamageReport` names a pawn, and every listener (the weapon drop, C4, C5) is about
  pawns.
- **At nought the building is demolished, with no refund**: `Demolished` is reported at once (amount
  = the edifice id, so presentation knows what came down) and `ConstructionGrid.Demolish` — **the call
  deconstruction makes** — runs at the end of the tick (`ctx.Defer`), for death's reason: a removal
  inside the pawn loop edits what the loop is reading. Everything the building held is Demolish's
  list, exactly as for a deconstruct: the door's nav flag, the bed out of the bed index (its owner
  with the record), the power device and its hopper, the shelf's contents spilt where the board
  takes them and lost where it does not — a consequence, as for a building falling on them — the
  chunks, navigation, support and the ladder connectors. What is not called is
  `DeconstructJobDriver.TakeApart`'s salvage, which is the refund.
- **Only the blow that crosses nought demolishes**: a second blow the same tick finds nothing left.

### 13h. One owner for "the building has gone"

**`ConstructionGrid.Demolish` clears the damage row, and any deconstruct order on the building's
cells.** It is the one way an edifice leaves the world (a collapse erases slabs, never an edifice —
checked, `SupportSystem.ApplyConsequences`), so the row cannot outlive the building by any route:
deconstructed, demolished, or anything later that calls it. The deconstruct driver still clears its
own order before deferring; clearing twice is harmless, and a demolished wall no longer leaves a
dangling order drawn on an empty cell.

### 13i. What presentation and the interface read

- **`WorldSnapshot.EdificeDamage`**, a row per struck building (`EdificeDamageView`: cell, edifice,
  hit points and pool, in thousandths), and **`WorldSnapshot.EdificeHitPoints(edifice)`**, the
  content's base points per edifice id, nought where it is not a target. Both published by
  `EdificeDamageContributor`; neither saved nor hashed. The table is 17 numbers a publish and is how
  the interface learns which edifices are targets without a copy of the rule.
- **The order line**: a building attack publishes `odyssey.pawn.order.cell` (§5d), the struck cell.
- **`CombatEventKind.Demolished`**, appended.
- **No blood from a building** — `BloodModel.For(in CombatEventView)` answers none for a target of 0,
  and `CombatFeedback.Bleed` asks it (a Presentation line, never compiled here).
- **`CombatFeedbackModel.BuildingHealthBar`** answers a struck building's bar from the rows, for
  lane B to draw.

**The right-click** (`CombatOrders.RouteBuilding`, from `OrderModel.RightClick`):

1. the pawn half first (`Route`) — a pawn under the pointer wins over the cell it stands in;
2. then the context menu — **a weapon on a shelf opens the menu** rather than the shelf being
   attacked, since equipping is the answer the owner asked for on a weapon (§7a);
3. then the building: with **no pawn under the pointer**, a cell whose edifice has hit points is
   attacked by every selected drafted colonist (`OrderAttack`, `B = 0`);
4. then the move — **a floored cell, bare ground, a tree and a site all still move**.

The interface cannot see the grid, so **which edifice stands in the clicked cell is supplied by the
presenter from the render mirror** (`WorldRenderModel.EdificeDef`), as it supplies the pawn under the
pointer and Ctrl; **which edifices are targets is the snapshot's**, so the rule has one owner. A
selection with nobody drafted sends nothing, and the move then sends nothing either.

### 13j. Open for the owner — answered 2026-09-24 (§14)

- ~~**Do bandits attack buildings?**~~ Yes: *"kill colonists, destroy base"* (§14b).
- ~~**The ruined city's walls are targets** (§13b).~~ *"Forget for now."*
- ~~**A ladder is a target**, and so are a door and a bed.~~ Yes, as built.
- ~~**Melee trains on a building** (§13f).~~ No (§14c).
- ~~**Every weapon strikes a wall alike.**~~ No: blunt against stone, sharp against wood (§14d).
- ~~**The numbers**~~: placeholders, to be tuned after play.

### 13k. Owed to drawing

Nothing is drawn yet but what the fight already drew off the log — the floating number over the
struck cell, the swing, the thud, and the order line to the cell. Owed:

- a hit-point bar over a struck building (`BuildingHealthBar` is the answer);
- the building's hit points on the tile pane;
- a damaged look — cracks, or a darker tint — once it is struck;
- a crash and dust on `Demolished`, and a sound of its own for a blow on wood and on stone;
- the lock-on ring (§7b) round a building target.

### 13l. What it costs

A colony that is not fighting a building pays one branch a pawn a tick (`CombatTarget == 0` on a
pawn already in `Job_AttackMelee`, which is nobody) and publishes a 17-entry table. A building
attacker costs a constant reach test a tick; choosing a side costs at most ten candidate cells ×
(one pass over the pawns for `Holds`, plus a reachability query), asked at the chase cadence and not
per tick. The publish scales with the buildings that have been struck.

### 13m. Tests, and the controls seen to fail

Fast tier: Sim **1,370** (from 1,349), Hud **985** (from 979), Long **41**, all green;
`GoldenMasterTests` green without a re-bake. Three content gates clean. Never run in Unity: the
two Presentation lines below are **uncompiled** until the integrator's run.

`BuildingTargetTests` (Sim, 21): what a target is, both halves of a bed and a generator keyed on
the head, a deck plate and bare ground not; which edifice ids have hit points; the pool times the
material; a wall beaten down with no refund, the row gone and she holds again; every blow lands for
its damage alone at melee 0; the refusals; a wall with no reachable side; the same wall again; two
on one wall on two sides; the draft outlasting a stone wall; the building going ending the attack; a
rebuilt wall not the one she was sent at; `Demolish` clearing the row and the order, by fight and by
deconstruction; a demolished bed and door leaving nothing pointing at them; only the blow that
crosses nought; the order line; the publish; the hash only once struck; a save mid-blow at a wall
(with its own forgetful control); a knockback keeping the order. `CombatOrdersTests` (Hud, five, in
place of the C2 case "a walled cell is still a move" — it is an attack now): a building attacked by
every drafted colonist; **a floored cell still moves**, and so do a tree and an edifice the table
does not name; a pawn under the pointer wins; no draft, no fight; a weapon on a shelf opens the
menu. `BloodModelTests` +1, `CombatFeedbackModelTests` +1.

Each rule was withheld and its test run and seen to fail, then restored:

| Withheld | Failed |
|---|---|
| `Demolish` clearing the damage row | four, the row outliving the wall |
| `Demolish` clearing the deconstruct order | `DemolishClearsTheRowAndTheDeconstructOrder` |
| `IsInAnAttack` counting a building attack | `TwoOnOneWallStandOnTwoSides` |
| the quiet clock refreshed by a blow | `TheDraftDoesNotLapseWhileSheBeatsAWall` |
| a blow that always lands (a hit roll put back) | `EveryBlowAtABuildingLandsForItsDamageAlone` |
| the knockback keeping a building order | `AKnockedBackColonistGoesBackToTheWall` |
| the record handle (the cell asked instead) | `ARebuiltWallIsNotTheOneSheWasSentAt` |
| the material's factor | `APoolIsTheRowTimesItsMaterial` |
| a pawn under the pointer winning | `APawnUnderThePointerWinsOverTheBuildingItStandsOn` |
| the interface asking the published table | `AClickOnAFlooredCellIsStillAMove` |
| the menu before the building | `AWeaponOnAShelfOpensTheMenuRatherThanAnAttack` |
| no blood from a building | `ABuildingNeverBleeds` |
| only the blow that crosses nought | `OnlyTheBlowThatCrossesNoughtDemolishes` |
| no refund (`TakeApart` called instead) | `ADraftedColonistBeatsAWallDownAndGetsNothingBack` |
| the order line for a building | `TheOrderLineIsDrawnToTheBuilding` |
| refusing a wall nobody can reach | `AWallWithNoSideAnybodyCanReachIsRefused` |
| the same wall again changing nothing | `TheSameWallAgainChangesNothing` |
| the contributor registered | `StruckBuildingsArePublishedAndTheTableNamesTheTargets` |

**Presentation, never compiled here:** `SelectionPresenter.Order` reads the edifice in the clicked
cell off `WorldRenderModel.EdificeDef` and passes it on; `CombatFeedback.Bleed` asks
`BloodModel.For(combatEvent)`.

## 14. The owner's answers to Phase 4 (2026-09-24)

The owner answered §12d and §13j on 2026-09-24. Built on `claude/combat-owner-round`, from
`claude/combat-phase4` at `99ead9a2`, on the fast and Long tiers only, with no Unity. **No golden
moved.** Nothing new is hashed. The one behaviour a colony at peace could reach is a thought's renewal,
and that is off for every thought but the friendly-fire one, which no golden window gives (§14e).

### 14a. The answers

| Question | The owner's answer | Built |
|---|---|---|
| C6 (a) Do bandits attack buildings? | **Yes**: *"that is the goal: kill colonists, destroy base"* | §14b |
| C6 (b) Are the ruined city's walls targets? | *"Forget for now"* | nothing. They stay targets for an order (§13b), and a bandit passes them over (§14b) |
| C6 (c) Doors, beds and ladders as targets? | **Yes, as built** | nothing |
| C6 (d) Does Melee train on a building? | **No** | §14c |
| C6 (e) Blunt against stone, sharp against wood? | **Yes** | §14d |
| C6 (f) The hit-point numbers | placeholders, tuned after play | nothing |
| C5 (a) Does a second attack in the day renew the memory? | *"Whatever you recommend"*. We recommended **renewing it**, so the day runs from the latest blow | §14e |
| C5 (b) Does the one who started a fight remember the blows she takes back? | **Yes**, as built | nothing new. The end-to-end test now asserts it (§14h) |
| C5 (c) Three stacked deaths? | **Keep** | nothing |
| C5 (d) Does a missed swing count as being attacked? | **Yes** | §14f |

### 14b. A bandit breaks in

`HostileThinkNode` now chooses in this order:

1. the colonist who struck it, while it remembers her (§6A.6);
2. the nearest standing colonist it can reach;
3. **only when there is neither: the nearest colony building it can reach that is a target**;
4. otherwise it idles, as before.

So walls and doors never draw a bandit away from a colonist it can get to. Walling the colony
in, or losing every colonist to a fall, turns it on the base.

- **A colony building** is one a colonist raised (`PlacedEdifice.Built`) and that is a target by
  §13b's one rule (`BuildingTargets.TryStanding`): a wall, a door, a ladder, a bed, a shelf, the
  campfire, a generator or a heater.
  - The ruined city's walls and doors are passed over. *"Destroy base"* names the colony's own,
    and the owner's (b) leaves the city for now. A player's order may still strike a city wall,
    as before (§13b).
- **It can reach a building** if it already stands in reach of it (§13e, the deconstructor's
  stance), or if one of the cells beside it can be entered and reached in the bandit's own
  mode.
  - This is cheaper than `ChooseSide`: there is no pass over the pawns to see which sides are held.
  - A held side is the driver's to sort out. It waits and looks again, as a player's attacker does.
- **Nearest** is `PawnContext.Distance` from the bandit to the building's own cell (a two-cell
  thing's head). It is the travel estimate every work giver orders its candidates by.
- **A tie goes to the lower record handle**, which is the older building. The edifice list is
  saved in handle order, so the choice is the same after a load.
- **The job is C6's building mode (§13e), unforced.** It carries the record handle in
  `Job.DestCell`, with `CombatTarget` at 0 and `PlayerForced` false. It chooses a side, strikes
  with a blow that always lands (§13f, with §14d's multiplier), goes through `StrikeBuilding`, and
  demolishes at nought with no refund. None of that was written twice.
- **It looks again every `rechooseTicks` (300)**, between swings, whether it is in reach or not.
  The building attack ends in success, the think runs again, and a colonist who can now be reached
  comes first.
  - This differs from a hunt on a pawn, which re-chooses only while it is chasing. A bandit
    beating on a wall never chases, so without this it would not look up until the wall fell.
  - A swing in the air always lands first. The swing clock lives on the pawn, so a re-think costs
    no blow.
  - It also looks again **at once** when the building goes (success, then a think) or when a
    colonist strikes it (`CombatSystem.React` interrupts a bandit that is not fighting a pawn
    beside it, and a building is not a pawn).
  - Three hundred ticks is the cadence at which the hunt already notices a nearer colonist. It is
    the number to tune if play says a bandit is slow to notice a door left open.
- **A player's order on a building is unchanged.** It is forced, so it is never re-chosen and runs
  until the building has gone (§13e).
- **No new state.** `DestCell`, `JobStartTick` and `WorkTicks` are all saved already.
- **Cost** (`docs/process.md` §3): nothing per tick, and nothing at all while there is no bandit.
  - The building scan runs only on a bandit's think when no colonist can be reached. While it is
    at a building, that is at most once per `rechooseTicks`, plus once each time a building attack
    ends.
  - The scan scales with the **edifice records**: every edifice ever placed, trees included, with
    removed ones keeping their slots.
    - Anything not colony-built costs one branch.
    - A colony building costs a content lookup of at most twelve rows.
    - A candidate nearer than the best so far costs a reachability test on at most ten cells, two
      array reads each.

**Found on the way, and not changed: a bandit opens the colony's doors as a colonist does.** A
bandit walks as `TraverseMode.Colonist`, and a door can be entered in every mode but an animal's
(`NavGrid.CanEnter`). So a wall keeps a bandit out and a door does not. The owner's example, *"a
door broken open"*, assumes a door holds.

Making a door a wall to a hostile is a navigation change and the owner's call (§14g). The mode for
it half exists: `TraverseMode.IgnoreDoors` is described as "raiders and bashers" and prices a
closed door as a cost, not an obstacle.

**Superseded by §16 (2026-09-24):** a bandit no longer opens doors, and slot 3 is now
`TraverseMode.Bandit`.

### 14c. No Melee from a building

`CombatSystem.StrikeBuilding` no longer grants experience. A swing at a pawn still trains Melee,
landed or not (§6A.1). A wall is no longer a practice dummy, and §13f's paragraph on experience is
superseded.

### 14d. The blow's kind against the material

- **`StuffDef` gains `sharpDamagePerMille` and `bluntDamagePerMille`**, both 1,000 by default,
  beside `hitPointsFactorPerMille` in `Buildings.xml` and in the code oracle.
  - Why the material and not the building row: the owner's rule is wood against stone, so a wooden
    door and a wooden wall answer alike.
- **The kind** is the armament's `AttackDef.damageKind`. Fists are blunt (`Combat.xml`).
- **The values are INVENTED** and are the owner's own examples:

  | Material | Sharp | Blunt |
  |---|---|---|
  | wood | ×1.25 | ×1.0 |
  | stone | ×0.5 | ×1.25 |
  | concrete, steel, composite, nothing | ×1 | ×1 |

  The city's materials are left at the default under (b). **A building with no material, or one
  the table does not know, takes ×1.**
- **Applied in `BuildingTargets.Resolve`**, to the rolled damage, on the tick the wind-up begins.
  The held outcome, the floating number and the hit points taken are therefore one figure.
  - The arithmetic is integer: damage × per-mille ÷ 1,000, rounded down.
  - A blow at a pawn is untouched, because a pawn has no material.
- **What it does to a wall.** Pools are wood 300 and stone 450; the damage is before the ±20 %
  spread.

  | Weapon | Wooden wall | Stone wall |
  |---|---|---|
  | machete, 8 sharp every 96 ticks | 10 a blow, about 30 blows, ~2,900 ticks | 4 a blow, about 113 blows, ~10,800 ticks |
  | bat, 7 blunt every 120 ticks | 7 a blow, about 43 blows, ~5,200 ticks | 8.75 a blow, about 52 blows, ~6,200 ticks |
  | fists, 4 blunt every 120 ticks | 4 a blow, 75 blows, 9,000 ticks | 5 a blow, 90 blows, 10,800 ticks |

  So a machete is the tool for a wooden wall, a bat for a stone one, and stone costs a bandit
  with a machete nearly four times what wood does.
- `ConstructionContentDefTests.StuffFingerprint` moved once, deliberately.

### 14e. A second blow renews the memory

- **`ThoughtDef.renewsOnRepeat`**, false by default. When a thought that renews is added at its
  stack limit, the copy that would expire soonest is pushed out to a full duration from now. It is
  never shortened.
- **Only `Thought_AttackedByColonist` sets it.** Every other thought behaves exactly as before:
  `Thought_AteMeal` at its limit of two is still dropped rather than refreshed. That is what keeps
  the goldens still, and a test holds it.
- Still one copy, still −80. **The day now runs from the latest blow** rather than the first.
- `PawnContentDefTests.ContentFingerprint` moved once, deliberately.

### 14f. A missed swing is an attack

- **A new hook, `ICombatListener.SwingResolved(in SwingReport)`.** `CombatSystem.ApplySwing`
  raises it once for every swing that reaches a pawn: a hit, a miss, a dodge, and a blow that falls
  on air because she stepped out of reach during the wind-up.
  - It is raised **before the outcome is applied**, so for a hit it comes before `DamageApplied`.
  - It is **not** raised for a blow at a building, since `StrikeBuilding` raises no hooks (§13g).
  - It is **not** raised for a swing lost in the air to the attacker's own stun or fall, which
    never reaches `ApplySwing` (§6A.1).
- **`FriendlyFireListener` gives the memory on `SwingResolved`, and no longer on `DamageApplied`.**
  That keeps one rule with one owner: the attack is remembered where the attack is heard, whatever
  came of it. `DamageApplied` keeps its meaning (hit points taken) for the weapon drop and for
  anything later.
- **A blow falling on air counts.** It was aimed at her, and the player sees a *miss* float over
  her head.
- A target already past the death line when the swing arrives remembers nothing, because the dead
  feel nothing (§12b).
- Colonist on colonist only, as before (§12b).
- §12b's first bullet is superseded.

### 14g. Open for the owner — answered 2026-09-24 (§16)

The owner's answer to all four: *"do what you recommend"*. §16h says what each became.

- **Should a door stop a bandit?** Today it walks through a closed door as a colonist does
  (§14b). Suppose "walled in" should include "behind a closed door". Then a hostile needs a mode
  that climbs ladders and opens no door, and the door becomes the thing it breaks.
- **Does a bandit go for the right building?** It picks the building nearest *itself*. A raider
  that picked the wall between it and a colonist would read as smarter and would need a path
  search to find that wall. Worth asking after the first play.
- **The city's concrete, steel and composite take every blow at ×1.** They would need numbers if
  (b) comes back.
- **With every colonist down, a bandit breaks the beds.** A bed is the nearest colony building
  to a fight more often than not. In the soak's ten days (below) it broke five buildings, and no
  colonist got back up, against three who did before the change.
  - That is the owner's rule working as asked, since a bed is a target (c).
  - It also means a bandit left alone destroys the one thing a rescue needs.
  - Say if the base should exclude beds, or if a bandit should leave while nobody is standing.

### 14h. Tests, and the controls seen to fail

Fast tier: Sim **1,401** (from 1,391), Hud **1,003** (unchanged), Long **41**, all green.
`GoldenMasterTests` is green without a re-bake. All three content gates are clean: no name was
added, so there is no wiki row. Never run in Unity, and **no Presentation or Editor file was
touched**.

| Test | Claim |
|---|---|
| `BuildingTargetTests.ABlowAtABuildingTrainsNoMelee` | the one method and a whole order give no Melee; a swing at a pawn still trains (the control) |
| `BuildingTargetTests.TheBlowsKindMeetsTheMaterial` | the four numbers and the defaults in content; `Resolve` multiplies the rolled damage by them |
| `BuildingTargetTests.EveryBlowAtABuildingLandsForItsDamageAlone` (changed) | fists on stone now land at ×1.25 |
| `BuildingTargetTests.ABanditWithNobodyToReachBreaksInThroughTheNearestWall` | a colonist sealed in eight walls: the nearest wall, unforced, only that one struck, and the colonist once it is down |
| `BuildingTargetTests.ABanditGoesForAColonistItCanReachBeforeAnyBuilding` | the order of the rule |
| `BuildingTargetTests.WithEveryColonistDownItTakesTheNearestColonyBuildingAndTheOlderOnATie` | all down; a tie to the older record; a nearer city wall passed over |
| `BuildingTargetTests.ABuildingItCannotGetBesideIsPassedOver` | a nearer shelf sealed in city walls against a reachable wall further off |
| `BuildingTargetTests.ABanditAtAWallLooksUpWhenAColonistCanBeReached` | a way in opened while it strikes: on her 104 ticks later, the wall still standing |
| `FriendlyFireTests.ASecondBlowRenewsTheDay` (replaces `…NeitherStacksNorRenews`) | one copy, the latest blow's day |
| `FriendlyFireTests.NoOtherThoughtRenews` | the flag is the friendly-fire memory's alone; a meal past its limit is still dropped |
| `FriendlyFireTests.ABanditsBlowIsNotFriendlyFire` (replaces `…AndAMissAreNotFriendlyFire`) | a bandit's hit or miss, and a colonist hitting a bandit, give nothing |
| `FriendlyFireTests.AColonistsSwingThatMissesIsRememberedToo` | a miss and a dodge each give the memory; a miss then a hit is one memory; the dead remember nothing |
| `FriendlyFireTests.EverySwingAtAPawnIsHeardOnceAndNoneAtABuilding` | the hook's order (a swing before its damage) and that a building blow raises none |
| `FriendlyFireTests.ACtrlAttackOnAnUndraftedColonistIsFoughtBackAndRemembered` (changed) | the one who started it remembers the swing she takes back (C5 (b)) |
| `HostileTests.WithNobodyStandingAndNothingBuiltABanditIdles` (renamed) | idling needs no building on the board now; the board has no bed |

**The soak** (`BanditSoakTests`, Long) now logs the buildings broken down. Over ten days it
broke five. It resolved 231 pawn swings against 377 before, 7 colonists went down against 11, and
none got up against 3 (§14g). Every invariant held, and the mid-fight save resumed equal.

Each rule was withheld and its test run and seen to fail, then restored:

| Withheld | Failed |
|---|---|
| no Melee from a building (the grant put back) | `ABlowAtABuildingTrainsNoMelee` |
| the material multiplier | `TheBlowsKindMeetsTheMaterial`, `EveryBlowAtABuildingLandsForItsDamageAlone` |
| the XML and the code oracle agreeing (stone sharp 501 in the XML only) | `TheXmlIsTheSameContentAsTheCodeOracle` and the two fingerprint tests |
| renewal (`AddMemory` as it was) | `ASecondBlowRenewsTheDay` |
| renewal only where the thought says (the flag ignored) | `NoOtherThoughtRenews` |
| the memory on the swing (heard on `DamageApplied` only) | `AColonistsSwingThatMissesIsRememberedToo`, and the starter's line of `ACtrlAttack…` |
| the dead remember nothing | `AColonistsSwingThatMissesIsRememberedToo` |
| the building fallback (the node as it was) | `…BreaksInThroughTheNearestWall`, `WithEveryColonistDown…`, `…LooksUpWhenAColonistCanBeReached` |
| the unforced re-look (the driver as it was) | `…LooksUpWhenAColonistCanBeReached` |
| a building only when no colonist (the building first) | `…GoesForAColonistItCanReachBeforeAnyBuilding`, `…BreaksIn…`, `…LooksUp…` |
| the tie to the lower handle (`>` for `>=`) | `WithEveryColonistDown…OlderOnATie` |
| colony-built only | `WithEveryColonistDown…`, `ABuildingItCannotGetBesideIsPassedOver` |
| a side it can reach | `ABuildingItCannotGetBesideIsPassedOver` |
| the re-look for unforced attacks only (applied to a player's order too) | six of C6's order tests, `TheDraftDoesNotLapseWhileSheBeatsAWall` among them |

**Content fingerprints, each moved once, deliberately:**

- `ConstructionContentDefTests.StuffFingerprint`, 4054578596745551293 → 3846353424243238969, for
  the two damage columns;
- `PawnContentDefTests.ContentFingerprint`, 13836212755718261116 → 5393620802301053337, for
  `renewsOnRepeat`.

## 15. Drafted colonists help (2026-09-24)

**The owner, after playtesting, 2026-09-24:** *"if a colonist attack - by default they will fight
back. If I draft colonist/colonists by default - if there is any fight going on nearby (another
colonist is being attack) they will help and start attacking the attacker and help other colonists
by default. Buildings work fine"*

Built on `claude/combat-drafted-help`, from `claude/combat-owner-round` at `cd53b5cf`, on the fast
and Long tiers only, with no Unity. **Simulation only; no Presentation, Hud or Editor file was
touched.** **No golden moved**: nothing new happens unless a colonist is drafted, and no golden
window drafts anybody. The content fingerprint moved once, for the one new number.

### 15a. What was there

- **Fighting back when struck** was built by C2 and needed nothing. Undrafted, a colonist struck
  remembers who struck her and her self-defence answers (`CombatSystem.React`,
  `SelfDefenceThinkNode`, §6A.6). Drafted, her hold strikes any threat beside her — a hostile, or
  anybody attacking her (§2b). `AColonistAttackedFightsBackDraftedOrNot` now holds both halves of
  the owner's first sentence in one place.
- **The hold never chased** (§6A.2, §6A.6). A drafted colonist three cells from a colonist being
  beaten stood and watched until the bandit came to her. That is the gap the owner found.
- *"Buildings work fine"* is the verdict on C6 (§13). It closes that playtest row and changes
  nothing.

### 15b. The rule

A drafted colonist **on her hold** joins a fight nearby. "On her hold" is exactly where the rule is
asked: by `DraftHoldJobDriver` every tick and by `DraftedThinkNode` at each think. So it is never
asked of a colonist who is:

- walking a move order (`Job_Goto`);
- already on an attack, equip or rescue order;
- stunned or knocked down (her job is paused and she does not think, §5c);
- downed or broken (either ends the draft);
- undrafted. **Undrafted colonists keep today's behaviour**: they fight back only when struck
  themselves.

She joins when all of these hold:

1. **Another colonist is being attacked.** The one answer is `Melee.ColonistUnderAttackBy`: the
   attacker's own melee job and `CombatTarget`, which is the thing that swings at her. It is the
   same signal the fight guard and the side rule (§7c, §8c) read. A fight in which every swing
   misses is still a fight, because the question is the job, not a blow that landed.
2. **The attacker is a bandit or an animal.** A colonist attacking a colonist summons nobody.
   That covers both the player's Ctrl order (§12) and the blows the victim takes back.
3. **The victim and her attacker are both within `CombatDef.helpRadiusCells`** — eight cells,
   INVENTED (20 m). The distance is Chebyshev across the layer, on the same layer or one either
   side, so a fight on the terrace step above counts and one three storeys down does not.
   - Both, not just the victim: a bandit hunting a colonist from across the board is not "a
     fight going on nearby". She goes once it has come within eight cells.
4. **She can reach the attacker** in her own mode (`PawnContext.Reachable`, two array reads).

**Which fight:** the nearest victim's attacker, by squared distance in cells, which is how
`ChooseSide` measures. A tie goes to the lower victim id, then the lower attacker id.

**The threat beside her comes first**, unchanged: one pass over the pawns
(`Melee.HoldTarget`) returns the first threat in reach exactly as `AdjacentThreat` did, and she
strikes it from where she stands. Only with no threat in reach does she look for a fight to join.
Several drafted colonists may join one fight. Each takes a side of her own by the one guard for
every fighter (§8c).

### 15c. The job

**The existing `Job_AttackMelee`, not a fork.** The Drafted node starts it just as it starts the
blow at a threat beside her: unforced, named on the pawn, the four quiet hours restarted. The one
difference is a mark: `Job.DestCell = AttackMeleeJobDriver.Joining` (2).

- **The mark is what lets her chase.** The driver treats an unforced attack by a drafted colonist
  as the hold's blow, which never leaves its cell. With the mark she is a chaser like any other
  attacker. She walks to a side (`ChooseSide`) and re-plans at the chase cadence.
- **Why `DestCell`:** for an attack on a pawn it already means "how this attack ends"
  (`ToTheDeath`), and it is saved and hashed. So there is no new field, no new state and no
  save-format change. A save taken mid-join resumes as a join.
- **Why unforced:** it is her own idea, like the blow beside her. A forced attack is never
  re-chosen and would follow its target across the board. Nothing a player's order does applies:
  - a knockback does not give it back (§9b);
  - the order-cell aspect ignores it;
  - the four-hour release is not an order's reset.

**It ends** as any unforced attack on a pawn ends:

- the attacker down, dead or gone: success;
- the attacker unreachable: failure;
- `rechooseTicks` (300) at a step boundary: the node looks again. It finds the same fight, or a
  nearer one, or — standing beside a hostile — the blow from where she stands.

**And one ending is new: the attacker on no colonist any more.** The job ends at a step boundary.

- *Why:* an animal's revenge runs out. A hog rooting about is not a threat, and without this she
  chased it for up to 300 ticks. Her blow would then have started the fight again.
- A bandit that turns on her is still on a colonist, so she fights on.
- A bandit that goes to beat a wall is on no colonist, so she would let it go. In practice this
  hardly arises: a bandit turns to a building only when it can reach no colonist (§14b), and a
  helper who can reach it is a colonist it can reach.

**She holds where the fight ended.** When the job ends the Drafted node gives the hold, and the hold
has no cell of its own to go back to. So she stands where the fight left her, still drafted, and
the next fight within eight cells of *there* is the one she joins.

### 15d. The quiet hours: a swing is activity

**Found by the test, and fixed.** A swing at a building restarted the draft's quiet clock (§13); a
swing at a pawn did not. Only the start of a fight did — the node, or the order.

- An attacker standing on her side in reach never re-thinks, so a fight longer than four hours ran
  the clock out.
- The draft then let go on the tick the fight ended.
- That was true of the hold's own blow before this unit, and it would have been true of every
  join.

`AttackMeleeJobDriver.StartSwing` now restarts the clock for a drafted attacker, as
`StartSwingAtBuilding` already did. It applies to every drafted swing at a pawn, forced or not.
`AFightLongerThanFourHoursKeepsTheDraft` failed without it: undrafted and wandering ten ticks after
the bandit went down.

### 15e. What is drawn

Nothing new, and nothing was touched outside the simulation.

- A helper publishes `odyssey.pawn.order.target` as the hold's own blow does. The aspect means
  *whom she is attacking*, not whose idea it was (§7b).
- So a **selected** helper wears the lock-on ring under the bandit, with the order line to it.
- **The request assumed a self-started fight would not light the ring. The blow beside her already
  does, by §7b's decision, and following that, so does the help.** Filtering it would need a second
  aspect, which is simulation state bought for a colour. §15i puts it to the owner.

### 15f. What it costs

- **Nothing while nobody is drafted.** The scan is asked only by a drafted colonist on her hold,
  every tick, and at her thinks.
- **It is still one pass.** The threat scan the hold already made per tick is now `HoldTarget`, one
  pass over the pawns.
  - A pawn at peace costs one integer comparison more (its target is nought).
  - A pawn in an attack costs a lookup by id and a content row.
  - A reachability test (two array reads) is made only for a candidate nearer than the best so far.
- **It scales with the pawns on the board, per drafted colonist on her hold.** A colonist already in
  a fight or walking an order does not scan.

`TickBenchmarkTests.TwentyAgainstTwenty` (explicit) gained a third arm, *twenty drafted against
twenty*: every colonist drafted, so all twenty scan on every tick they hold. Two runs on the Windows
dev machine, with the join and with it withheld (`HoldTarget` answering the threat only), each
against the other arms in the same run:

| Arm | Tick, mean | Pawns phase, mean | Swings |
|---|---|---|---|
| twenty undrafted against twenty | 0.101–0.168 ms | 0.020–0.038 ms | 307 |
| twenty drafted against twenty, with the join | 0.165–0.217 ms | 0.072–0.095 ms | 361 |
| the same, the join withheld | 0.164–0.183 ms | 0.075–0.082 ms | 361 |

The join costs nothing measurable. In this arm the bandits come to the drafted line, so the fight
is the same, swing for swing, with and without it. What the drafted arm costs over the undrafted one
is the hold's own per-tick scan, which was there before this unit (§6A.9).

### 15g. Do not undo by tidying

- **The mark is on the job, not the pawn.** It dies with the job. A flag on the pawn would outlive
  the fight and would be new saved, hashed state.
- **The victim and the attacker both within the radius.** Dropping the attacker's half sends a
  helper across the board after a bandit that is still hunting from afar.
- **A colonist's fight with a colonist summons nobody.** The attacker's side is what is asked, not
  the victim's.
- **A move order is not diverted.** The rule lives in the hold and the node, never in the walk.
- **The blow beside her comes first, in the same pass.** `HoldTarget` returns the first threat in
  reach in list order, which is `AdjacentThreat`'s answer. A helper who walked past a hostile
  beside her to reach another fight would be wrong.
- **The on-nobody ending is for a join only.** The hold's blow and every other attack keep their
  own endings.

### 15h. Tests, and the controls seen to fail

Fast tier: Sim **1,416** (from 1,401), Hud **1,003** (unchanged). Long **41**, all green.
`GoldenMasterTests` is green without a re-bake. `PawnContentDefTests.ContentFingerprint` moved once,
deliberately: 5393620802301053337 → 1636227730504628602, for `helpRadiusCells`. All three content
gates are clean, because no name was added.

| Test (`DraftedHelpTests`) | Claim |
|---|---|
| `AHoldingColonistJoinsAFightNearbyAndNotOneFarOff` (5, 12) | five cells from a bandit on a colonist she joins — unforced, marked, the bandit on the victim, out of her reach — swings and has left her cell; twelve cells off she holds, and the fight happened |
| `OneWalkingAMoveOrderDoesNotTurnAsideAndJoinsOnceSheHolds` | sent across the radius mid-fight, she walks to the cell she was sent to; holding there, she joins |
| `AnUndraftedColonistLeavesAFightNearbyAlone` | undrafted, five cells off, she never takes the bandit on |
| `AColonistFightingAColonistSummonsNobody` | a Ctrl attack and the blows taken back, four cells off: nobody comes |
| `WhenTheAttackerIsDownSheHoldsWhereTheFightEnded` | on the hold, on the cell the fight ended on, for 600 ticks, still drafted |
| `SheJoinsAgainstAnAnimalAndLetsItGoWhenItIsOnNobody` | a hog on a colonist is joined; its revenge spent, she holds and does not follow it |
| `TwoHelpersNeverShareATile` | two from one side, the fight guard after every tick; both joined and swung |
| `AFightLongerThanFourHoursKeepsTheDraft` | twelve thousand ticks of fighting, still drafted; the quiet hours count from the fight's end |
| `TheRadiusHoldsTheVictimAndHerAttacker` (three cases) | 7/8 in; victim 8 attacker 9 out; victim 9 attacker 8 out |
| `TheNearestVictimsAttackerFirstAndATieToTheLowerVictim` | three cells beats six; on a tie the lower victim wins though her attacker has the higher id |
| `AColonistAttackedFightsBackDraftedOrNot` (two cases) | the owner's first sentence, which C2 already did |

**Long:** `FightGuardTests.MixedBrawlsOnManySeeds` now counts the pawn-ticks spent joining, and
asserts there are some. Its drafted colonists join the fights round them once their own orders are
done: 4,134 pawn-ticks over twelve seeds, 1,460 swings against 1,448 with the join
withheld. The guard held on every tick. `BanditSoakTests` drafts nobody, and its log is identical
line for line. Every other Long test is unchanged.

Each rule was withheld, its tests run and seen to fail, then restored:

| Withheld | Failed |
|---|---|
| the victim's half of the radius | `TheRadiusHoldsTheVictimAndHerAttacker(9,8)` |
| the attacker's half of the radius | `TheRadiusHoldsTheVictimAndHerAttacker(8,9)` |
| a colonist attacker summons nobody | `AColonistFightingAColonistSummonsNobody` |
| the join's mark in the driver (treated as the hold's blow) | `…JoinsAFightNearby…(5)`, `…WhenTheAttackerIsDown…`, `…AgainstAnAnimal…`, `TwoHelpers…`, `AFightLonger…` |
| the hold's scan (the threat only, as before) | the same five |
| the whole join (`HoldTarget` never joining) | all eight positive cases and `MixedBrawlsOnManySeeds` |
| a move order diverted by the scan | `OneWalkingAMoveOrder…` |
| undrafted colonists scanning too (self-defence asks `HoldTarget`) | `AnUndraftedColonistLeavesAFightNearbyAlone` |
| the on-nobody ending | `SheJoinsAgainstAnAnimalAndLetsItGoWhenItIsOnNobody` |
| the on-nobody ending and the down ending together | `…WhenTheAttackerIsDown…`, `…AgainstAnAnimal…`, `AFightLonger…` |
| the side mask ignoring other attackers | `TwoHelpersNeverShareATile` |
| a swing as activity for the draft | `AFightLongerThanFourHoursKeepsTheDraft` |
| nearest first (farthest instead) | `TheNearestVictimsAttackerFirst…` |
| the tie to the lower victim (to the attacker's id instead) | `TheNearestVictimsAttackerFirst…` |
| self-defence (C2's rule) | `AColonistAttackedFightsBackDraftedOrNot(False)` |
| the threat in reach first (C2's rule) | `AColonistAttackedFightsBackDraftedOrNot(True)`, `AColonistFightingAColonistSummonsNobody` |

**The down ending alone did not fail anything.** A downed attacker is not in an attack, so the
on-nobody ending ends the join as well. Both endings stay: the down ending is every attack's, and
the on-nobody ending is the join's own.

**Not tested:** the layer half of the radius (one layer either side). The bare board is flat, and a
pawn put on another layer by hand is unreachable anyway, so a test could not tell the radius from
the reachability.

### 15i. Open for the owner

- **The ring and the line on a helper.** A selected helper wears the lock-on ring under the
  bandit she joined, as she does under one she hits beside her (§7b). If the ring should mean
  "an order I gave" only, that is a second aspect. Say so. *Answered 2026-09-24 (§18b): the ring
  means an order. A helper wears none now; the rescue's patient took the second aspect instead.*
- **Eight cells** (20 m) is invented. If helpers come from too far or not far enough, it is one
  number in `Combat.xml`.
- **Undrafted colonists** still help nobody, as asked; they fight back only when struck. Say if a
  colonist at work should drop it for a friend being beaten beside her. *Answered 2026-09-24
  (§18c): a setting per colonist, Defend, built on this section's rule.*

## 16. Doors hold bandits; beds are spared (2026-09-24)

§14g asked the owner three things about a bandit and the base. The answer was *"do what you
recommend"*. Built on `claude/combat-bandit-doors`, from `claude/combat-owner-round` at
`cd53b5cf`, on the fast and Long tiers only, with no Unity. **No golden moved.** No Presentation or
Editor file was touched.

### 16a. The three decisions

| §14g question | Decision | Built |
|---|---|---|
| Should a door stop a bandit? | **Yes.** A bandit does not open a colony door. It breaks it down. | §16b |
| Should a bandit break the beds? | **No.** A bed is never the bandit's own choice of target. A player's order may still strike one (C6 answer (c)). | §16d |
| Does it go for the right building? | **Unchanged:** the nearest colony building to the bandit. A smarter breach is deferred until play asks for it. | §16e |

The reason for the first is the owner's own example, *"a door broken open"*, and C6's answer,
*"kill colonists, destroy base"*. Both assume a door holds. A bandit that opens doors makes the
door the one building in the base that never needs breaking.

The reason for the second is §14g's soak. With every colonist down, the nearest colony building is
usually a bed, and in ten days nobody got up again. Sparing beds keeps a downed colony rescuable.

### 16b. A bandit moves as a colonist, less a closed door

- **`TraverseMode.Bandit`**: ladders, stairs, the hop and the wade, as `Colonist`. **A closed
  door is a wall.** An open door is a floor.
  - `TraverseModes.OpensDoors` is the one owner of the rule: everyone but `Animal` and `Bandit`.
    `NavGrid.CanEnter` is its only caller, so the district flood, the region links, the cell
    search, `IsLegalStep` and `Reachable` all follow from it.
  - The rat (`Climber`) still opens doors, as it always has. The hog still does not. Colonists and
    haulers are untouched.
- **It is slot 3, repurposed rather than added.** Slot 3 was `IgnoreDoors`, "a closed door is a
  cost, not an obstacle". No pawn, Def or test used it.
  - A sixth mode would cost a sixth district flood on **every** nav rebuild, on every board, with
    or without a bandit. Slot 3 was already being flooded for nothing.
  - The old meaning was the RimWorld-style basher: path through the door at a price. That is not
    what the owner asked for. The door must stop the bandit so that §14b's fallback breaks it.
  - `MoveCost.DoorBash` went with it. The cell search charged it and the region graph never did,
    a disagreement nothing was walking into.
  - The ladder's mask already carried slot 3, and the hop and the wade read "not an animal", so
    nothing else changed.
- **The mode is the kind's, not the species'.** A bandit is `Species_Person`, drawn and hurt as
  a colonist is.
  - `PawnKindDef.traverseMode` names a mode, or is empty for the species' own.
  - `PawnContent.KindMode` resolves it once at load. A name the enum does not have fails the load.
  - `PawnKind_Bandit` names `Bandit` in `Species.xml`. It is the only kind that names one.
- **`Pawn.OwnMode` is the one owner of "how does this pawn move when it chooses for itself".**
  Every place that read `Species.traverseMode` reads it now:
  - the hunt and the building fallback (`HostileThinkNode`), the animal's revenge, the downed job
    (both where it is thought and where it is started);
  - `FightingBeside` and the flight (`CombatSystem.Apply`);
  - the knockback: a bandit is not knocked into a shut door, where standing would open it;
  - the animal's idle mind (identical for animals, whose kinds name no mode);
  - `Pawn.Mode` between jobs.
- **Two places that were `Colonist` for everybody now read `OwnMode`**:
  - `WanderTarget.Fill`'s person overload. An idle bandit with nothing to hunt wanders, and it
    would have wandered through the front door;
  - the idle `Wait` job's mode, so a waiting bandit is a bandit to anything that asks
    `pawn.Mode`.
  - A colonist's `OwnMode` is `Colonist`, so both are unchanged for her.
- **Left as `Colonist` on purpose**: a player's orders, the draft, self-defence and every work job.
  Only colonists take them.

**So a door behaves like this.** With the colonists behind a closed door they are unreachable, and
§14b's fallback takes the nearest colony building the bandit can stand beside. When that is the
door, the door goes down, and the bandit looks again and goes in.

**A door a colonist is walking through is open**, and a bandit may follow her through it. That is
the door working, not a leak.

### 16c. What it costs

- **Nothing per tick, and nothing new on a rebuild.** Slot 3's district flood ran before and runs
  now, over the same regions.
- **The nav graph for the other four modes is identical.** Measured with a probe that hashed every
  region's district and every link's and portal edge's mask with slot 3 masked out, on all three
  goldens at generation and after 3,000 ticks. The ruined city, with 34 doors, included. Every
  number was the same before and after, and so were the state hashes.
- **The goldens did not move.** No golden has a bandit, and the nav graph is not in the state
  hash.
- **`PawnContentDefTests.ContentFingerprint`** moved once, deliberately:
  5393620802301053337 → 9855151047521430616, for the new field and its table.

### 16d. Beds are spared

- **`BuildingTargets.IsBanditTarget`**: every target but a bed. `TryNearestColonyTarget` asks it
  before anything else about a record.
- **What a target is does not change.** `TryStanding` still answers yes for a bed, so a player's
  order on one is taken and its blows land.
- It keys on `CoreContent.EdificeBed`, as every other bed rule in `ConstructionGrid` does. It is
  one method, so a medical bed or a cot joins it there.

### 16e. Target choice stays the nearest

The bandit still picks the colony building nearest **itself**. It does not look for the door
between it and a colonist, nor for the wall that is cheapest to go through.

- A door on the far side of the room from the bandit is not preferred to a nearer wall.
  `ItTakesTheNearestBuildingNotTheDoor` pins that.
- A breach chooser would need a path search with walls priced as time to break them. That is
  deferred until play shows that "nearest" reads as stupid.

### 16f. The soak

`BanditSoakTests` (Long) now reads **377 swings, 165 hits, 11 downed, 0 died, 3 got up, 0
buildings broken down**. Before §16 it was 231 swings, 7 downed, none up and 5 buildings broken.

- The soak's only colony buildings are the scenario's beds, and it has no doors.
- So sparing beds leaves the bandits nothing to break. The run is again exactly the one from
  before §14b, number for number, which is the measurement that the change reached no further.

### 16g. Tests, and the controls seen to fail

Fast tier: Sim **1,412** (from 1,401), Hud **1,003** (unchanged), Long **41**, all green.
`GoldenMasterTests` is green without a re-bake. All three content gates are clean, since no name was
added.

| Test (`BanditDoorTests`) | Claim |
|---|---|
| `EachKindMovesInItsOwnMode` | the bandit's kind is `Bandit` and its species still `Colonist`; the other three kinds keep their species' modes |
| `AClosedDoorIsAWallToTheBanditAndTheHogOnly` | the five modes at a shut door and an open one; the bandit wades and climbs a ladder |
| `OnEveryLinkTheBanditsModeIsTheColonistsLessAClosedDoor` | on the city's 2,066 link ends and 218 portal edges, the bandit's bit equals the colonist's except into a shut door (128 link ends) |
| `BehindAClosedDoorSheIsUnreachableToABanditAndNotToAColonist` | the district, the step and the search say no to a bandit and yes to a colonist |
| `ABanditBreaksTheDoorDownAndThenGoesForHer` | the door is its target, unforced; it never stands in the doorway or opens it; no wall is struck; then her |
| `WithTheDoorOpenItGoesStraightIn` | the control: an open door is a way in, and it is not struck |
| `ItTakesTheNearestBuildingNotTheDoor` | decision three: the door far side, the nearest wall broken |
| `ABanditIsNotKnockedIntoAShutDoor` | a knockback asks the target's own mode; a colonist in the same place is the control |
| `AnIdleBanditDoesNotWanderThroughAShutDoor` | 6,000 ticks of an idle bandit in a yard whose only way out is a city door |
| `ABanditLeavesABedAloneAndBreaksTheWallInstead` | 600 ticks with only a bed: nothing struck; add a wall further off than the bed, and the wall is struck |
| `APlayerCanStillOrderAnAttackOnABed` | the order is taken and a blow lands |

Each rule was withheld and the tests run and seen to fail, then restored:

| Withheld | Failed |
|---|---|
| the kind's mode (the XML line removed) | `EachKind…`, `…BreaksTheDoorDown…`, `…NotKnockedInto…`, `AnIdleBandit…`, `ItTakesTheNearest…` |
| `CanEnter` as it was (only the hog refused a shut door) | seven of the eleven: all but the kind, the open door, the bed and the order tests |
| the idle wander in `Colonist` for everybody | `AnIdleBanditDoesNotWanderThroughAShutDoor` |
| the knockback in the species' mode | `ABanditIsNotKnockedIntoAShutDoor` |
| the hunt in the species' mode | `…BreaksTheDoorDown…`, `ItTakesTheNearest…` |
| a bed as a bandit's target | `ABanditLeavesABedAlone…` (and the order test's own control line) |
| a bed as no target at all (spared in `TryStanding`) | `ABanditLeavesABedAlone…`, `APlayerCanStillOrderAnAttackOnABed` |
| the ladder's mask without the bandit | `AClosedDoorIsAWall…`, `OnEveryLink…` |

**Not tested directly**: the idle `Wait` job's mode. Nothing a bandit does while waiting asks
`pawn.Mode` today, so no test can see it. It is there so the next thing that asks is right.

### 16h. §14g, answered

- *Should a door stop a bandit?* Yes (§16b).
- *Does a bandit go for the right building?* Nearest to itself, unchanged. Revisit after play
  (§16e).
- *The city's materials at ×1*: unchanged. It is (b)'s question, which stays forgotten for now.
- *With every colonist down, a bandit breaks the beds*: it no longer does (§16d). A bandit
  with nothing to hunt and nothing else to break now **idles**. Leaving is a separate question for
  the owner, not taken here. *(Answered 2026-09-24: it steals and leaves, §17.)*

## 17. Bandits steal and leave (2026-09-24)

§16h left one thing to the owner: with every colonist down and nothing left it may break, a
bandit idled for ever. Asked whether it should leave, the owner answered:

> *"It will thieve items or kidnap people depending on their motivation creating a negative event
> (but they could be rescued later) - seam this later but for now - thieve items"*

Built on `claude/combat-thieves`, from `claude/combat-owner-round` at `2e01d57e`, on the fast and
Long tiers only, with no Unity. **No golden moved.** One Presentation file was touched and has
never been compiled (§17g).

### 17a. The decisions

| Question | Decision |
|---|---|
| When does a bandit steal? | When its mind finds **no colonist standing that it can reach and no colony building it may break** (§14b's fallback finds nothing; beds are spared, §16d). Theft is the **last** thing the mind reaches for |
| What does it take? | **The nearest stack it can reach and lift**, on the ground or in a store. There is no value yet, so nearest is the whole of the choice; **a tie goes to the lower item id**, the older stack |
| Where does it go? | **The nearest edge cell it can reach from the stack**, on a layer it can stand on, and it leaves the board there |
| And with nothing to take? | It leaves **empty-handed** by the edge nearest itself |
| And with no edge to reach? | It **stays**, takes nothing, and thinks again as it did before |
| Leaving is? | **Removal, not death**: no corpse, no `Died` report, no hook, nobody mourns. **The stack goes with it**, out of the colony's things, and so does its weapon |
| Struck down carrying it? | It **drops the load where it falls** (`DropCarried`), as every carrier does. Killed, the same, and the corpse is the death's |
| A colonist it can reach again? | It **drops the load and goes back to the fight**, within one look (§17c) |
| The negative event? | A **Theft** row on the Events panel, in the blow's red, with its chime: *Theft · Meal × 12*. An empty-handed leaving is a neutral **Bandit left** |
| Kidnap? | A **motive** on the kind: `Loot` or `Kidnap`. Only `Loot` is acted on; **`Kidnap` does exactly what `Loot` does today** (§17f) |

### 17b. The mind

`HostileThinkNode` now chooses in this order:

1. the colonist who struck it, while it remembers her (§6A.6);
2. the nearest standing colonist it can reach;
3. the nearest colony building it may break (§14b, less beds, §16d);
4. **what it came for** (`Theft.TryFill`), if it came for anything and an edge can be reached;
5. otherwise it idles, as before.

The first two are one method now, `ColonistToFight`, so the think and the thief's look (§17c) ask
exactly the same question. `HostileThinkNode.HasAFight` is the first three without filling a job.

**What it came for is the kind's** (`PawnKindDef.motive`, resolved into `PawnContent.KindMotive`
and read through `Pawn.Motive`). `PawnKind_Bandit` names `Loot`; every other kind is `None`,
so a colonist or an animal is untouched. It is on the kind, as the weapon and the way of walking
are, because nothing yet rolls a bandit's reason for coming. The owner's *"depending on their
motivation"* reads as per bandit; the day a raid rolls one, it becomes a field on the pawn,
saved and hashed only while set, and the kind's value is its default.

### 17c. The job

**`Job_Steal`**, handle 22, `StealJobDriver`, four toils:

1. **Walk to the stack** (`Job.TargetItem`, lying at `Job.TargetCell`). Taken, eaten or moved
   before it gets there: the job fails and it thinks again.
2. **Lift it** through `LiftToil`, the hauler's own stoop, grasp and rise. The load is the job's
   `CarriedItem`, so it is published on the carry aspects and **drawn in its arms for nothing**,
   and the activity line reads *Stealing · Meal × 12* by `JobLabels.Carrying`.
3. **Walk to the edge** (`Job.DestCell`), chosen at the think as the nearest reachable edge cell
   **from the stack**, so the job is fixed from the start and a save resumes it exactly.
4. **Leave**: on the edge cell, `Theft.Leave` runs at the end of the tick (`ctx.Defer`), for
   death's reason — `PawnRegistry.Despawn` shifts the list every pawn loop walks.

With no stack, the first two are skipped. The job reserves the stack (`ReservationTargetKind.Item`),
so a hauler never sets off for the thing a thief has chosen, and a stack a hauler has claimed is
passed over.

- **In its own mode** (`TraverseMode.Bandit`) all the way, so it opens no door with its arms full
  either. **It climbs a ladder with a load**, which a hauler does not (`TraverseMode.Hauler`). A
  mode that was both would be a sixth district flood on every nav rebuild, on every board, for the
  sake of a thief (§16b's argument); recorded rather than built.
- **It walks.** `UrgencyPerMille` runs only an attack and a flight. A thief that ran would be hard
  to catch; the playtest decides.
- **No expiry.** An expiry ends the job, and ending it drops the load, so a long walk to the edge
  would put the loot down and pick it up again.
- **It looks up while it goes.** Once every `rechooseTicks` (300) of the job, at a step boundary, it
  asks `HostileThinkNode.HasAFight`; if there is a colonist it can reach or a building it may break,
  the job fails, `Cleanup` drops the load, and the next think fights. A hunt re-chooses by ending
  its job on the same cadence; a thief cannot, or it would drop the load every three hundred ticks.
  **`Job.WorkTicks` counts the looks taken** — a theft has no duration for it to override — so the
  cadence is saved and hashed with the job, and a load resumes it on the same tick.
- **A colonist who strikes it** turns it at once, as she turns any bandit not fighting beside it
  (`CombatSystem.React`): the job is interrupted and the load dropped.
- **Every end but the leaving drops the load** (`Cleanup` → `DropCarried`): downed, killed, knocked
  back, a look that finds a fight, a failed walk.

**The stack**, `Theft.NearestLoot`: every thing on the three listers a hauler walks (loose, stored,
contained) that has a place (`WhereIs`), can be carried, is not claimed by anybody else and can be
reached, nearest by `PawnContext.Distance`, a tie to the lower id. **A forbidden thing is taken
too**: forbidding is the colony's word to its own people. A weapon in a hand and a load in somebody's
arms have no place and are passed over. A stack from which no edge can be reached is no loot (a
drop into a pocket can be one way), and it leaves empty-handed instead.

### 17d. Leaving the board

`Theft.Leave`, deferred, asks again that it is still a thief standing on its edge — the fight's pass
runs between the driver and the end of the tick, and a thief downed there has already dropped its
load. Then, in order:

1. the load out of the job and **despawned** — out of the colony's things;
2. **its weapon despawned too**. `PawnRegistry.Despawn` puts a held weapon down where the pawn
   stood, which is right for a death and for a pawn that is simply gone; a bandit walking off
   with its machete has not been disarmed, and leaving one at the edge for every thief would arm
   the colony for free;
3. the ledger entry (§17e);
4. the job ended (`EndJob`, a success) and the pawn despawned through `PawnRegistry.Despawn`, the
   one way off the board: it ends every attack on the thief, releases its reservations and any bed.

**Not a death**: no corpse (`CorpseRegistry` untouched), no `Died` on the combat log, no
`CombatHooks.RaiseDied`, so the friendly-fire listener's *a colonist died* memory never fires.
Nobody mourns a bandit that walked off.

### 17e. The negative event

**The ledger, not a new channel.** Design 23 §5 made the incident ledger the colony's memory of
what has happened and the Events panel its reader, and a theft is a thing that happened. Two
incident Defs, appended at 2 and 3:

| Def | Bulletin | Favourability | Row |
|---|---|---|---|
| `Incident_Theft` | `ui.bulletin.theft` *Theft* | **Bad**: red ink, the negative chime | *Theft · Meal × 12* |
| `Incident_BanditLeft` | `ui.bulletin.banditleft` *Bandit left* | Neutral | *Bandit left* |

- **Written down, never fired.** Both name a new worker, **`Recorded`** (`RecordedIncidentWorker`):
  `CanFireNow` is false, so `InvokeIncident` refuses it, and `Fireable` is false, so **the debug
  menu's Events tab leaves it off its list** — the one Presentation line in this unit. A worker
  rather than a flag on the Def, because every Def names one and the loader refuses a Def that names
  none.
- **What was taken rides beside the entry, not in it.** Design 23 §8 foresaw that an entry wanting
  more than `(id, tick, def, cell)` would add fields and bump the save format. It does neither: the
  ledger keeps a **sparse detail row** per entry that is about a thing (`IncidentLedger.Detail`: id,
  item def, amount), saved in its own section, **`odyssey.incidents.detail`**, appended to
  `ColonyWorld.SaveComponents`, and hashed only while it has a row. A save from before it loads
  with no detail, which is what it had. **Save format unchanged.**
- **Published on the bulletin**: `BulletinView` gained `Subject` and `Amount` (−1 and 0 by default),
  and `BulletinModel.Title` writes *name · thing × n*, as the activity line writes a load (one of
  a thing has no count). Both names are the registry's; only the separator and the sign are written
  in C#.
- **Where**: the entry's cell is the edge cell the thief left from, so clicking the row jumps the
  camera to where it went.

### 17f. The kidnap seam (not built)

`Motive.Kidnap` exists, is loaded, and **does exactly what `Loot` does today** — the switch in
`Theft.TryFill` says so, and `TheftTests.AKidnapperStealsAsALooterDoes` holds the two to the same
hash every hundred ticks of a whole theft. How kidnap would work, when it is built:

- **What it takes**: the nearest **downed colonist** it can reach, in place of the nearest stack.
  `RescueRules` already answers who is downed, not carried and not claimed, and the rescuer's
  `ReservationTargetKind.Pawn` claim keeps a rescuer and a kidnapper off one body.
- **How it carries her**: C4's cradle (§11). `Pawn.CarriedBy` and the carried patient's cell riding
  her carrier's are the rescue's, and the kidnapper's job reuses them, with `Cleanup` putting her
  down on every end that is not the leaving, as the rescue's does.
- **Leaving with her**: the colonist is **not despawned into nothing**. She leaves the board into a
  record — *held by the raiders* — that a later unit reads: a rescue, a ransom, or a raid on the
  camp. That record is new saved state, and the unit that builds it decides its shape.
- **The event**: a *Kidnapped* bulletin naming her (a `ui.bulletin.*` key, and a detail row whose
  subject is a pawn rather than an item; the row's shape already allows it).
- **Which it does**: a rolled motive per bandit once raids arrive (§17b), with `Kidnap` chosen
  only when there is somebody down to take, falling back to `Loot`.

### 17g. What was touched

- **Simulation**: `Theft` and `StealJobDriver` (new); `HostileThinkNode` (the order, `HasAFight`);
  `Job_Steal` in `Jobs.xml`, `JobIndex`, the driver pool; `Motive`, `PawnKindDef.motive`,
  `KindMotive`, `Pawn.Motive`, `<motive>Loot</motive>` on the bandit; `EdgeTarget.Find`, shared
  with the animal's leaving walk and unchanged for it; the ledger's detail rows and section;
  `RecordedIncidentWorker`, `IncidentWorker.Fireable`; two incident Defs.
- **The hash**: `JobSystem.HashedAlways` (22). A job def below it hashes its counters as it always
  did; one appended at or after it is hashed **only once it has a count**, with its index. The
  contracts step moved every golden once for ten zeros (§5h); this is what let `Job_Steal` arrive
  without doing that again, and the next job inherits it.
- **Contracts**: `JobHandle.Steal` 22 (`Count` 23), `IncidentHandle.Theft` 2 and `BanditLeft` 3
  (`Count` 4), `BulletinView.Subject` and `Amount`.
- **Interface**: `JobLabels` (*Stealing*), `IncidentLabels` (the two bulletins), `BulletinModel.Title`.
- **Presentation, never compiled**: `HudShell.Debug.cs`, one line skipping a worker that is not
  `Fireable`.
- **Registry**: `ui.status.stealing`, `ui.bulletin.theft`, `ui.bulletin.banditleft`, with two
  icon-map gap rows for the bulletins. All three content gates clean.

**Cost** (`docs/process.md` §3): nothing per tick, and nothing at all while there is no bandit.
The theft scan runs on a bandit's think only when it has nobody to fight and nothing to break,
and scales with **the item stacks on the board** (a branch and a reservation probe each, a
reachability test for each nearer than the best so far) plus the edge search, bounded by the
board's side. A thief's look is one colonist scan and one building scan per `rechooseTicks`.

### 17h. Tests, the soak, and the controls seen to fail

Fast tier: Sim **1,439** (from 1,427), Hud **1,004** (from 1,003). Long **41**, all green.
`GoldenMasterTests` is green without a re-bake. Two fingerprints moved once, deliberately, with the
reason beside each: `PawnContentDefTests.ContentFingerprint` 6953925138484699291 →
12926174003015880195 (`Job_Steal`, `PawnKindDef.motive`), and `IncidentContentTests`
15479437417230274748 → 17580740474630100120 (the two recorded incidents). `CombatContractTests`
holds the new handle at 22 and the pool's driver. All three content gates are clean.

| Test (`TheftTests` unless named) | Claim |
|---|---|
| `WithEveryoneDownItCarriesOffTheNearestStackAndLeaves` | the nearer of two stacks, stood on and lifted, carried to the edge nearest it; the stack and the machete despawned, the other stack untouched; one pawn fewer, no corpse, no `Died`, no mourning. 3,267 ticks on the 60 × 60 board |
| `TheLedgerRecordsTheTheftAndTheBulletinSaysWhat` | one `Theft` entry at the edge it left from, the detail Meal × 12, and the published bulletin carrying both, in the blow's favourability |
| `WithNothingToStealItLeavesEmptyHanded` | by the edge nearest itself; `BanditLeft`, with no detail |
| `OnATieItTakesTheOlderStack` | two stacks the same distance off: the lower id |
| `AColonistThenABuildingComeBeforeTheft` | a standing colonist first, then a wall, never the meal beside it; with both gone, it steals |
| `AThiefThatCanReachAColonistAgainDropsTheLoadAndFights` | a colonist spawned mid-carry: the load is dropped, on the ground, within one look, and it goes for her |
| `DownedWhileCarryingItDropsTheLoad` | the load beside it, not despawned; a downed thief never leaves and records nothing |
| `KilledWhileCarryingItDropsTheLoad` | the load dropped, one corpse, no theft recorded |
| `WithNoEdgeToReachItStays` | ringed by walls it may not break: 3,000 ticks without a theft or the meal lifted; a gap opened, and it leaves with the meal |
| `AKidnapperStealsAsALooterDoes` | the same hash every hundred ticks of a whole theft; one that came for nothing stays |
| `AJobAppendedAfterTheCombatLineIsHashedOnlyOnceItHasRun` | an unrun `Job_Steal` hashes as the goldens were baked; a run one is in the hash |
| `ATheftSavedMidCarryResumesIdentically` | the job, the stack in its arms and the hash after the load; the same edge and tick on leaving; the ledger's detail through a second save |
| `BulletinModelTests.ATheftSaysWhatWasTakenAndHowMany` (Hud) | *Theft · Meal × 12*, no count for one, *Bandit left* alone, and the blow's chime |
| `BanditDoorTests.ABanditLeavesABedAloneAndBreaksTheWallInstead` (changed) | its bandit comes for nothing, in that colony's own content record: a looter would carry off the scenario's meals and be gone before the wall went up |

**The soak** (`BanditSoakTests`, Long) now accounts for every bandit — still on the board,
killed, or off the edge — and asserts that the ones off the edge are exactly the ledger's thefts and
empty-handed leavings. It reads **7 left with a stack, 0 empty-handed, 3 still on the board (down),
391 swings, 211 hits, 12 downed, 0 died, 4 got up**, and every colonist down at the end. With the
theft withheld (the kind's `<motive>` line removed) the same run reads **377 swings, 165 hits, 11
downed, 0 died, 3 got up** and ten bandits on the board, which is §16f number for number: the
change reached nothing but what a bandit does once nobody is standing. The fights that follow a
theft differ because seven bandits are no longer standing about the colony when its colonists
get up.

Each rule was withheld, its tests run and seen to fail, then restored:

| Withheld | Failed |
|---|---|
| theft before the building | `AColonistThenABuildingComeBeforeTheft` |
| theft before the colonist | the same, and `AThiefThatCanReachAColonistAgain…` |
| the nearest stack (the farthest instead) | `WithEveryoneDown…` |
| the tie to the lower id | `OnATieItTakesTheOlderStack` |
| the edge nearest the stack (the thief's instead) | `WithEveryoneDown…` |
| the stack despawned on leaving | `WithEveryoneDown…` |
| the weapon despawned on leaving (left to `Despawn`, which drops it) | `WithEveryoneDown…` |
| leaving as removal (`Kill` instead) | `WithEveryoneDown…`, `WithNothingToSteal…`, `AJobAppended…` |
| the ledger entry | `TheLedgerRecords…`, `WithNothingToSteal…`, `ATheftSaved…` |
| the detail row | `TheLedgerRecords…`, `ATheftSaved…` |
| the detail section in the save | `ATheftSaved…` |
| the drop on every other end | `AThiefThatCanReachAColonistAgain…`, `DownedWhileCarrying…`, `KilledWhileCarrying…` |
| the look while stealing | `AThiefThatCanReachAColonistAgain…` |
| no edge, no theft (its own cell taken as the edge) | `WithNoEdgeToReachItStays` |
| `Kidnap` as `Loot` (kidnap doing nothing) | `AKidnapperStealsAsALooterDoes` |
| `None` doing nothing (looting too) | `AKidnapperStealsAsALooterDoes` |
| the bandit's motive (the XML line) | eleven of the twelve; the soak back to §16f's numbers |
| the sparse job hash (every counter hashed) | the three `GoldenMasterTests` hashes, `AJobAppended…` |
| the bed test's motive set aside | `ABanditLeavesABedAlone…` |
| the row's thing and count (Hud) | `ATheftSaysWhatWasTakenAndHowMany` |

**Not tested**: the debug menu leaving a recorded incident off its Events tab (Presentation, never
compiled); a stack in a shelf (the contained lister is walked exactly as a hauler walks it, and
`LiftToil`'s reach into a shelf is the haul's own); and the thief's look finding a building rather
than a colonist, which is `HasAFight`'s second half and the think's own rule.

### 17i. Open for the owner

- **It walks.** A thief that ran would be hard to catch; one that walks can be run down by a
  colonist who gets up. Say which.
- **Its machete goes with it.** Say if a thief should drop its weapon at the edge instead.
- **Nearest, whatever it is.** A pile of stone is as good as the meals. There is no value on a
  thing yet; say if the choice reads as stupid, because that is what a value would answer.
- **A thief climbs a ladder with a load**, which a hauler does not (§17c). Say if it looks wrong.

## 18. The ring means an order; a colonist's response (2026-09-24)

**The owner, 2026-09-24**, asked whether the red lock-on ring should mean only orders the player
gave: *"Update the depending on the correct action."* And asked whether undrafted colonists should
ever help others in a fight: *"Maybe a setting to configure this - if there is a UI manage the colony
with such things - IE a management of rules - we might need another UI to say that all colonists (by
default) - even if undrafted, will draft themselves and fight maybe? not sure what do you
recommend"*.

Built on `claude/combat-response`, from `claude/combat-owner-round` at `2e01d57e`, on the fast and
Long tiers only, with no Unity.

### 18a. The two decisions

| Question | Decision |
|---|---|
| What does the ring mean? | **An attack the player ordered**, and nothing else. A fight a colonist started herself — the drafted hold's blow beside her, fighting back when struck, a drafted colonist joining a fight nearby (§15), and the new *Defend* below — draws no ring. A rescue already drew none (§11e). |
| Should undrafted colonists help? | **Yes, if the player says so, colonist by colonist.** Each colonist has a **response**, cycled by a button on her pane beside Draft: **Fight back** (the default, today's behaviour), **Defend** (she joins a fight near her, then goes back to work) and **Flee** (she runs from danger near her). |
| Should a colonist draft herself? | **No** (our recommendation, recorded for the owner). A draft takes a colonist off work until released, or until four quiet hours pass; *Defend* gives the protective behaviour without taking her off work. |
| A colony-wide rules panel? | **Deferred.** One default response for everybody is one rule; a panel is worth building when there are several colony rules for it to hold. Until then the pane's button, on a box selection, sets everybody selected at once (§18e). |

### 18b. The ring means an order

**The rule has one owner: what the simulation publishes.**

- `odyssey.pawn.order.target` is published **only for an attack the player ordered**: the pawn's
  job is `Job_AttackMelee`, `PlayerForced`, on a pawn (`PawnRegistry.OrderTargetOf`). That is
  `OrderAttack`, and the knockback's re-issue of it (§9b), which keeps the order forced.
- **A rescue's patient moved to an aspect of its own**, `odyssey.pawn.rescue.patient`, published for
  every rescue, ordered or the giver's (`PawnRegistry.RescuePatientOf`). The target aspect had been
  carrying two meanings — *whom she was sent to hit* and *whom she is carrying* — and the ring had to
  ask the job to tell them apart. Presentation's carrier lookups (`PawnFigureDirector.CarriesAPatient`
  and `CarrierOf`) read the patient aspect now.
- **`LockOnRings` asks the aspect and nothing else about the job.** Its two filters went: the
  rescue's (the aspect no longer names a patient) and "drafted only" (an order needs a draft, which
  the simulation already enforces). A second copy of "was this an order" in the interface is the
  bug pattern the catalogue lists first.
- **Nothing that reads *whom she is fighting* was lost.** The target stays on the pawn
  (`Pawn.CombatTarget`), saved and hashed, and the simulation reads it there. No other reader of the
  published aspect existed; the figure turns to a swing's target through `WorkFocus`, which is the
  cell and unchanged.

**What each kind of fight draws now:**

| Fight | Ring | Order line | Diamond |
|---|---|---|---|
| Ordered attack on a pawn (right-click, Ctrl on a colonist) | yes | none — a pawn target never drew one | yes (drafted) |
| Ordered attack on a building (§13i) | no — a building is not a pawn | yes, to the struck cell | yes |
| Drafted hold's blow beside her | **no** (was yes) | none | yes |
| Drafted colonist joining a fight (§15) | **no** (was yes) | none | yes |
| Undrafted: fighting back, or *Defend* | no | none | none |
| Rescue, ordered or automatic | no | none | while drafted |

So the order marks — the ring and the line — are the player's orders and only those. The diamond is
not an order mark: it says *drafted*, and a drafted colonist fighting on her own is still drafted.

### 18c. The response

`HostilityResponse` on the pawn: **Fight back** 0, **Defend** 1, **Flee** 2.

- **Saved** in `odyssey.combat`, in the record's flags word (two bits beside the draft and the
  downed flags), and the record is written for a colonist whose response is not the default. **No
  layout change**: an older build reading the word ignores the bits, and an older save loads with
  everybody at Fight back.
- **Hashed** as two bits of the kind word (bits 24–25; 22 and 23 left for the thief line, which is
  building beside this one), so a colony with every colonist at the default hashes exactly as before
  and **no golden moved**.
- **Published** as `odyssey.pawn.response` (1 or 2), sparse: absent at the default.
- **Set by `SetHostilityResponse(A pawn, B response)`**, applied while paused like the other orders
  over a colonist (`PausedIntents`). Refused for a pawn that is not a colonist and for a value outside
  the three; `AlreadyInThatState` for a no-op. It may be set on anybody of ours — drafted, downed or
  broken — because it is a standing setting, not an order to act. An undrafted colonist on a fight or
  a flight **the new setting would not have started** is interrupted, keeping her step, so it
  answers at once rather than when that one ends (`HostilityResponses.Started`): a flight under
  anything but Flee, a fight of her own under Flee, a join into somebody else's fight under Fight
  back. A fight she would have started anyway is left alone, so no swing in the air is lost.
- **The draft overrides it.** A drafted colonist does what §2 and §15 say whatever her response; it
  takes effect again when she is released.

### 18d. What each response does

**Fight back** is unchanged: she answers whoever strikes her, and a threat beside her when she next
thinks (§6A.6).

**Defend** is Fight back, and she also **joins a fight near her exactly as a drafted colonist on her
hold does** (§15b): the one rule, `Melee.HoldTarget` — a threat in reach first, else the nearest
victim's attacker, a bandit or an animal on another colonist, both within
`CombatDef.helpRadiusCells`, and she can reach it. The job is §15c's — the unforced attack marked
`Joining` — and it ends as that one ends: the attacker down, dead, gone, unreachable, or on no
colonist any more. **Then she goes back to work**: the tree runs and gives her whatever it would
have. She is never drafted, so no four-hour clock runs and nothing needs releasing.

**Flee** runs from danger near her instead of fighting.

- **Danger** is a standing pawn within the same `helpRadiusCells` (eight cells, 20 m — one number
  for *near* in a fight) that is a bandit, whatever it is doing; an animal attacking a colonist;
  or anybody attacking her. A wild animal at peace is not danger. **Only danger that can reach her
  in its own mode counts**: a bandit behind a shut door (§16b) does not keep her off work.
- **She runs** on `Job_Flee`, to `FleeJobDriver.FindFleeCell`, `fleeCells` (12) straight away from
  the nearest danger, turning 45° and then 90° either side when that is blocked — the animals' own
  flight, at the run's pace (§6A.7).
- **She stops** when she arrives. The tree asks again: danger still within eight cells, she runs
  again from where she is; none, she goes back to work.
- **Struck, she runs rather than fighting back.** Cornered — no flee cell at all — she fights back,
  as Fight back would: the retaliation memory is still written at the blow, for exactly that case.
- **She does not go back for whoever struck her.** With no danger near her the node gives her no
  fight at all, even with the blow still remembered; at Fight back the same memory sends her after
  it across the board, which is the difference the setting is for.

**Both are noticed while working.** The tree runs only between jobs, so a colonist chopping a tree
would never see a fight until the tree was down. `JobSystem.TickPawn` asks each undrafted *Defend*
or *Flee* colonist, every tick, whether her response would act now (`HostilityResponses.WouldAct`,
the same function the think node asks); if so, the job in hand ends as a failure, keeping the step
in progress (§2d), and the tree runs the same tick. **Not asked** of a colonist who is:

- asleep — a fight nearby does not wake her; a blow does (it interrupts, §6A.6);
- already fighting, fleeing, down, or carrying somebody to a bed;
- on a job the player forced (an equip, a prioritised build): the player's order stands;
- stunned, knocked down, broken, drafted.

### 18e. The pane

A second live button on a colonist's pane, after Draft. **It shows the response she has** — *Fight
back*, *Defend*, *Flee*, each a registry name (`ui.command.fightback`, `ui.command.defend`,
`ui.command.flee`) — and pressing it moves to the next, round the three. Its tooltip says what the
current one does and that a press changes it.

- **On a selection of several**, the next response is the one after the first selected colonist's,
  and every selected colonist is set to it, so one press can put a whole squad on Defend. The same
  shape as the draft key's rule for a mixed selection (§2f).
- The decision is `Hud.ResponseModel`, Unity-free and fast-tier tested. The shell only carries its
  intents to the world (`HudShell.CycleResponse`, one line in `ActionButton`).

### 18f. What it costs

- **Nothing while every colonist is at the default.** The notice is asked only of a *Defend* or
  *Flee* colonist, one byte comparison for everybody else.
- **For a *Defend* or *Flee* colonist, nothing while nothing is hostile.** Whether anything is —
  a standing bandit, or anybody in an attack on a colonist — is found once a tick, lazily, by the
  first colonist who asks, in one pass over the pawns (`PawnContext.AnythingHostile`), and reset
  when the job system's tick begins.
- **With something hostile about**, each responder's notice is the §15 scan (`HoldTarget`) or the
  flee scan, one pass over the pawns, and a flee also costs the flee-cell search (at most twenty-five
  column searches) while danger is near. **It scales with the pawns on the board, per responder,
  per tick, while there is a fight** — the drafted hold's own cost profile (§15f).

### 18g. Do not undo by tidying

- **The ring reads the aspect and asks nothing else.** Putting a job or a draft check back in
  `LockOnRings` makes the interface a second owner of "was this an order".
- **A rescue's patient is its own aspect.** Folding it back into the order target puts the rescue
  filter back in the ring.
- **The notice and the node ask one function.** A notice that decided differently from the node
  would interrupt her and then give her work again, every tick, until the think-loop breaker parked
  her.
- **The hostility gate is reset by the job system's tick, not cached by tick number.** A cache keyed
  on the tick would survive a load of an earlier save into the same world.
- **Defend never drafts.** A draft is the player's hand; her own response is not.

### 18h. Open for the owner

- **The recommendation itself**: Defend rather than self-drafting, and the colony-wide rules panel
  deferred until there is more than one rule to hold.
- **Flee's radius** is the help radius, eight cells. If fleeing colonists run from a bandit that
  was never coming for them, it wants its own number.
- **A *Defend* colonist asleep** is not woken by a fight nearby. Say if she should be.
- **The button shows the current response**, where Draft shows what pressing it will do. The
  Draft button's face is an action; this one is a setting. Say if they read as inconsistent.

### 18i. Tests, and the controls seen to fail

Fast tier: Sim **1,458** (from 1,427), Hud **1,015** (from 1,003). Long **41**, all green.
`GoldenMasterTests` is green without a re-bake, and no content number moved, so no fingerprint
did. All three content gates are clean; the wiki and `Registry.g.cs` carry the three new names and
a reworded *Fleeing* tooltip.

**Tests changed on purpose, not relaxed:**

- `CombatContractTests.TheFightsAspectsArePublishedOnlyWhileTheyHaveSomethingToSay` set a target on
  a colonist with no job and expected it published. It now expects nothing without an order, and
  the order as its control.
- `LockOnRingTests.ARescueDrawsNoRing` now feeds the rescuer as the simulation publishes her — the
  patient aspect, no order target — and its control is the same rescuer *with* an order target,
  which draws: the ring asks the aspect, not the job.
- `LockOnRingTests.AnUndraftedColonistsTargetIsNotAnOrder` became `AFightNobodyOrderedDrawsNoRing`:
  the drafted check it tested is gone, because the simulation no longer publishes an unordered
  target at all.
- `HudModelTests.ColonistPaneShowsNeedsAndDisabledTabsAndCommands`: two live commands, not one.

| Test | Claim |
|---|---|
| `ResponseTests.OnlyAnOrderedAttackPublishesItsTarget` (five cases) | the ordered attack publishes its target; the hold's blow, fighting back, a drafted join and a Defend join publish none |
| `…ARescuePublishesItsPatientAndNoOrderTarget` (ordered, automatic) | the patient under its own aspect, carried, and no order target |
| `…TheNumbersAreTheInterfaces` | 0, 1, 2 — the save contract and `ResponseModel`'s |
| `…TheIntentSetsItAndRefusesWhatMeansNothing` | default, published only off it, no-op quiet, refused for 3, −1, a bandit, a hog; a drafted colonist keeps her hold |
| `…ItAppliesWhilePaused` | landed by a republish that spends no tick |
| `…ItIsSavedAndHashedOnlyWhenNotTheDefault` | set and set back hashes and saves byte for byte as before; Defend and Flee survive a load and the worlds stay together for 300 ticks |
| `…DefendJoinsAFightNearbyThenGoesBackToWorkNeverDrafted` (5, 12) | off a 20,000-tick job to join, unforced and marked, then work; never drafted; twelve cells off she stays on the job |
| `…FightBackLeavesAFightNearbyAlone` (default, and back from Defend) | today's behaviour |
| `…DefendIgnoresAColonistFightingAColonist` | a Ctrl attack four cells off leaves her working |
| `…ASleeperIsNotRousedByAFightNearby` | asleep, she sleeps on; awake, the control, she goes |
| `…APlayersOrderIsNotTurnedAsideByDefend` | sent for a weapon across the fight, she keeps walking |
| `…FleeRunsFromABanditNearHerAndGoesBackToWork` (Flee, Fight back) | she runs within 30 ticks, never swings in 300, and works again once it is down; at Fight back she stays on her job |
| `…StruckSheRunsRatherThanFightingBack` (Flee, Fight back) | on a forced job the notice leaves alone, the blow makes her run; at Fight back it makes her fight |
| `…CorneredSheFightsBack` | walled into two cells with it, no flee cell, she fights |
| `…AnAnimalAtPeaceIsNotDangerAndOneOnAColonistIs` | a rooting hog three cells off leaves her working; the same hog on the colonist beside her makes her run |
| `…ABanditBehindAShutDoorIsNotDanger` (shut, empty doorway) | a shut door: she works; an empty doorway: she runs |
| `…SheDoesNotGoBackForWhoeverStruckHer` (Flee, Fight back) | out of range, the blow remembered, she stays; at Fight back she goes for it |
| `…ANewSettingAnswersAtOnce` | fighting back, set to Flee, she is running in the same call |
| `…DraftedSheDoesWhatTheDraftSays` | drafted at Flee: no job starts under her hold in 60 ticks with danger six cells off, and the bandit beside her is struck |
| `…TheGateIsAskedAgainEachTick` | a Defend colonist whose first asking found nothing still notices a bandit that comes later |
| `ResponseModelTests` (twelve) | the pane shows the response she has with the registry's name, after Draft, off for a colonist who has gone, none for a bandit; a press moves one round the three; a selection takes the first colonist's next and passes over the rest; an unknown number reads as Fight back |
| `CombatAspectNamesTests`, `CombatContractTests` | `odyssey.pawn.response` spelled alike on both sides; `odyssey.pawn.rescue.patient` held in the simulation |

Each rule was withheld, its tests run and seen to fail, then restored:

| Withheld | Failed |
|---|---|
| the order target for any target (the old rule) | `OnlyAnOrdered…` hold, struck, joined, defend; `ARescuePublishes…` both |
| the rescue's patient never published | `ARescuePublishes…` both |
| the ring's rescue filter put back | `ARescueDrawsNoRing` (its control) |
| the notice | `DefendJoins…(5)`, `FleeRuns…(Flee)`, `ASleeper…` (its control), `AnAnimalAtPeace…`, `…ShutDoor(empty)`, `TheGateIsAsked…` |
| the notice waking a sleeper | `ASleeperIsNotRoused…` |
| the notice overriding a forced job | `APlayersOrder…`, `StruckSheRuns…(Flee)` |
| the notice asking a drafted colonist | `DraftedSheDoes…` (seen to pass first: the draft's own hold is forced and the forced-job rule hid it; the test now puts her on a hold her mind gave) |
| the node's Defend branch | `DefendJoins…(5)`, `ASleeper…`, `OnlyAnOrdered…(defend)`, `TheGateIsAsked…` |
| the node's Flee branch | six, every Flee test that expects a run |
| "no danger, no fight" at Flee | `SheDoesNotGoBack…(Flee)` |
| the setting's interrupt | `ANewSettingAnswersAtOnce` |
| the hash bits; the flags bits; the record for a response alone | `ItIsSavedAndHashed…`, each |
| the gate always shut; the gate never forgotten | the same six as the notice, each |
| danger that need not reach her | `…ShutDoor(shut)` |
| any animal as danger | `AnAnimalAtPeace…` (seen to pass first: nothing hostile about, so the gate kept the notice from asking; the test now keeps a stunned bandit far off) |
| Defend's join unmarked | `DefendJoins…(5)` |
| the cycle per colonist rather than from the first | `ASelectionTakes…` |
| the pane's button | four `ResponseModelTests` |

**Not tested:** the Presentation wiring (the button's click, the carrier lookup's new aspect) —
never compiled here, and the fast tier has no Unity. A Defend or Flee colonist on a knock-down or a
stun is held by the job loop before the notice, which is §5c's rule and not re-tested.

### 18j. Never compiled here

`Presentation/Ui/HudShell.Inspect.cs` (the button's click), `Presentation/Ui/HudShell.Bar.cs`
(`CycleResponse`), `Presentation/World/PawnFigureDirector.Poses.cs` (the carrier lookups read
`CombatAspects.RescuePatient`). Three buttons now share the inspect pane's header — Prioritise,
Draft and the response — and nothing measures whether they fit; that is the playtest's question.

## 19. The playtest after the owner's rounds (2026-09-24)

The owner played the combined combat branch (PR #194) and reported two things:

> *"I noticed when the mauraders came to attack - 3 of them. Only one of them started attackign the
> building after destroying a campfire - the other 2 said they were fighting but kinda stood around -
> maybe it was because it didn't read the building was up on the hill at another depth or didn't
> know where to attack. Something is off there. Also it seemed tricky to draft then move my
> colonists to another floor in the building - just double check that."*

Built on `claude/combat-stall-fix`, from `claude/combat-owner-round` at `f87a3ab0`, on the fast and
Long tiers only, with no Unity. **No golden moved.** No Presentation or Editor file was touched.

### 19a. Measured on the owner's own save

Every finding below was measured, not reasoned. The save was on the disk: `the-latest-tim.odyssey`,
written at 17:51 on the day, tick 89,868, three bandits already on their way to the campfire at
(59, 37, L10). The colony is a two-storey house on a terrace one layer up: walls on x 56–60,
z 40–47 at L11, the door at (58, 40) on the edge of the step, a ladder at (58, 43), an upper floor
at L12, and four drafted colonists inside. A throwaway fast-tier probe loaded it the way
`Odyssey.SaveProbe` does and ran it on, logging every bandit's job, target, side, path and cell.

**First: a loaded world kept the generated board's paths.** Seven walls on x = 60 (z 41–47) were
walkable to the navigation graph and the floor above them was not. Bandits stepped into those
walls and chose sides inside them, and a colonist ordered to six upper-floor cells over that column
was refused. `ColonyWorld.RebuildDerived` called `NavGraph.Rebuild`, which floods only the blocks
something marked dirty, and a load writes the cell arrays wholesale without marking any. So the
graph the fresh world built for the generated meadow survived the load everywhere a door or a
ladder had not happened to dirty a block — and x = 60 is the first column of a ten-cell block, one
column past the door's. **Fixed:** `MarkAllDirty` before the rebuild. After it, no cell on the board
is blocked in the grid and enterable in the graph. This is older than the combat line: every loaded
game has had it, in every block the player built in that nothing on the load path happened to
dirty.

**Then, with the graph right, the owner's report exactly.** With the colonists behind the shut
door no colonist can be reached, so all three bandits turn on the base (§14b), and all three took
the same wall: (59, 40, L11), beside the door, the nearest colony building to all of them. A side
is a cell beside the wall **on its own layer**, and on the edge of a terrace most of those are air
over the step below: that wall had one, (60, 40, L11). One bandit took it and struck. The other
two stood at (59, 36) and (60, 37) on the lower ground, on *Fighting*, for **3,245 and 3,312
ticks** — to the end of the run.

- **One rule, two owners** (`docs/bug-patterns.md` P1). The choice,
  `BuildingTargets.TryNearestColonyTarget`, asked `CanReach`: is there a side it can get to, held
  or not. The driver asked `ChooseSide`: is there a side nobody holds. §14b wrote the difference
  down on purpose — *"a held side is the driver's to sort out"* — and the driver's sorting was to
  wait and look again. Every 300 ticks the mind thought again, and the choice sent it back to the
  same wall.
- **The owner's guess was half right.** The height is why the wall had one side; the bandits read
  the level correctly.

### 19b. The fix: a building is chosen only with a side free

- **`BuildingTargets.HasAFreeSide`** is `ChooseSide(...) >= 0`: the driver's own answer, her own
  cell counting as hers. `TryNearestColonyTarget` asks it instead of `CanReach`, so a building whose
  every side is held is passed over for the next one. With no building free at all the bandit
  turns to what it came for (§17), as it does with no building.
- **An unforced building attack whose look finds every side held ends**, and the mind chooses again
  in the same tick. Two bandits can choose one side in the same tick — a job given at the end of a
  tick has no destination until its driver's first look — and this is what sorts them out; without
  it the second waited the 300 ticks to its next think.
- **A player's order is unchanged.** It still waits for a side, and `CanReach` is still the order's
  question (§13d): a drafted colonist sent at a wall whose one side is taken queues for it.
- **`HostileThinkNode.HasAFight`** asks the same choice, so a thief (§17c) drops its load only for a
  building it could start on.
- **What it costs.** The choice's side search is now `ChooseSide`: at most ten cells, a reachability
  query each and, for a cell nearer than the last, a pass over the pawns (`Melee.Holds`). Only for a
  building nearer than the best so far, only on a bandit's think with no colonist to reach, never
  per tick.
- **Measured after, on the save:** a colonist placed on each of the house's inside cells in turn, the
  rest down, 92 runs; no bandit stood longer than 183 ticks in one cell without a swing, which is
  the time of the hop up the step. Before, 3,245 and 3,312.

### 19c. Moving a drafted squad to another floor

**One colonist was fine; a squad was not.** From the same save, with the bandits removed:

- One drafted colonist inside, ordered to each of the 117 standable cells in and round the house on
  L10–L13, reached 113; the other four were spread off a cell another drafted colonist stood on
  (§8c). With the stale graph of §19a, six upper-floor cells over x = 60 refused the order outright —
  a right-click that does nothing.
- **All four ordered at once to each of the 40 upper-floor cells — one right-click with the squad
  selected — sent 49 of 160 orders to another layer.** The first colonist goes to the clicked cell;
  the others are spread to free cells round it (`JobSystem.Spread`), and the spread lifted each ring
  cell by the **click's** rule, `StandAt`: that cell, else the one above, else the one below. The
  ladder's open shaft and the air past the floor's edge are not places to stand, so it dropped a
  layer and sent those colonists to the room below or the ground beside the house.
- **Fixed:** the spread keeps to the named cell's own layer. The click itself is still lifted by
  `StandAt` — a click names a block and she stands on top of it — but a spread already knows which
  floor it is on. After: **0 of 160**.

**What a click resolves to, read and not run** (Presentation; no Unity here). `SlicePicker.Owner`
returns a built floor slab's own cell, so a right-click on the upper floor names L12 and she is sent
there. Two things for a person at the keyboard:

- **A right-click on a ladder, a door or a bed with drafted colonists selected is an attack** (§13i;
  C6 answer (c) made all three targets). Right-clicking the ladder to send a squad up it starts them
  beating the ladder. That is as built and as answered; it may be the other half of "tricky", and it
  is the owner's call.
- **Seeing into the ground floor of a two-storey house** needs the cut-away ceiling
  (`GraphicsOption.CutAwayCeiling`) or a lower slice: at the surface every layer above is drawn
  solid, and a click there lands on the storey above.

### 19d. Found and left

- **A colonist with one open side is queued for** (§7c, by design). A drafted colonist on the narrow
  ledge behind the house, (57, 48, L11): one bandit fights her, the other two wait a ring back for
  **2,360 and 2,506 ticks** — and the ring back can be on the far side of a wall. The same shape as
  §19a with a pawn for a wall, but it is §7c's queue and not a slip: whether a bandit that can get
  no side of any colonist should break a building instead is the owner's call.
- **A spread's ring is by distance, not by path.** Once, a colonist sent to a taken cell inside the
  house was spread to (55, 46, L11), outside the west wall on the same layer, a long walk round. Not
  pursued.
- **Exhausted colonists let the draft go the moment they arrive** (rest at nought, §2b). Several in
  the save were; it will look like a squad that will not stay upstairs.
- **An unexplained difference between two runs of the probe.** One placement gave different numbers
  in two versions of the throwaway probe that differed only in read-only logging. Three runs of one
  scenario in one process came to the same hash every hundred ticks, so determinism within a run
  holds; the difference across runs was not chased.

### 19e. Tests, and the controls seen to fail

Fast tier: Sim **1,475** (from 1,470), Hud **1,016** (unchanged). Long **41**, all green.
`GoldenMasterTests` green without a re-bake. `BanditSoakTests` reads exactly §17h's numbers
(7 left with a stack, 391 swings, 211 hits, 12 downed, 4 got up): its only buildings are beds.

| Test | Claim |
|---|---|
| `WorldRoundTripTests.ABuiltWallIsStillAWallToThePathsAfterTheLoad` | walls raised through `Raise` are walls to the world that built them (control) and to the one it is loaded into; the two graphs agree cell for cell |
| `BanditSideTests.ThreeBanditsAtAWallWithOneSideDoNotStandAbout` | the owner's case built small — a two-cell step, a wall on its edge with one side (control), a colonist sealed in (control), three bandits: each swings, three walls are struck, and the longest wait at a building with no side is two ticks |
| `BanditSideTests.AWallWhoseOnlySideIsHeldIsPassedOverForOneWithASide` | with its side free the edge wall is chosen (control); held, `CanReach` still says yes, `HasAFreeSide` says no, and another wall is chosen |
| `DraftOrderLevelTests.ASquadSentUpstairsIsSpreadOnTheFloorItWasSentTo` | a storey on walls up a ladder; three drafted colonists sent to its corner by the shaft and the edge are each sent to, and hold on, a different upper-floor cell |
| `DraftOrderLevelTests.AClickOnTheWallUnderTheFloorStillSendsHerOnToIt` | the control: the click's own lift is untouched |

| Withheld | Failed |
|---|---|
| the whole-graph rebuild on load | `ABuiltWallIsStillAWall…`: a wall walked through after the load |
| the choice's free side (`CanReach` put back) | both `BanditSideTests`: two bandits never swing; the held wall is chosen |
| the driver's rethink when every side is held | `ThreeBandits…`: a 301-tick wait |
| both | `ThreeBandits…`: two never swing |
| the spread on its own layer (`StandAt` put back) | `ASquadSentUpstairs…`: the second sent to the ground a layer down |

**`AWorldWhoseGridHasChangedStillResumesIdentically` could not have caught §19a** and is left as it
is: its wall is written straight into the grid and nothing marks the graph dirty in either world, so
the original is exactly as stale as the copy and the hashes agree. The new test raises its walls
through `Raise`, which marks the graph, so the original is right and the copy is compared with it.

## 20. The landing ring and dragging across the roster (2026-09-24)

**The owner's words:** *"instead of using a square to indicate where to land when drafting people,
can it be a ring that flashes temporarily or has a transition effect that makes sense. Also when
I'm in default mode and I want to select many colonists, I should be drag the across their roster
profile and select them all this as well."*

Two interface changes, both presentation only. **No golden moved**: nothing here reaches a cell, a
save or the hash, and the simulation is untouched. Built on `claude/draft-ring-roster-drag` from
`claude/combat-owner-round` (`0c868710`).

### 20a. The decisions (told to the owner, built as told)

| Ask | Decision |
|---|---|
| A ring, not a square, where a drafted colonist is sent | **A ring on each colonist's own destination**, replacing the floor bracket of §2g. A squad is spread over several cells (§2d), so one ring per colonist. The Equip order (§7a) shares the marker and gets the ring too. |
| *"flashes temporarily or has a transition effect that makes sense"* | **The lock-on ring's transition** (§7b), so the two read as one family: it snaps in from 1.6 times its size over 0.2 s, flashes once as it lands, holds faint while she walks, and fades when she arrives, the order changes or she is undrafted. |
| Its colour | **Pale and neutral, not red**, so it can never be read as the red attack ring: `OrderColours.Move`, `#dce4ec`. |
| The order line and the diamond | **Unchanged.** The line keeps the draft's deep red and ends on the ring. |
| Drag across the roster | **A left press on a card dragged across others selects every card passed over**, as the box does in the world. A click without a drag still selects one; **Shift** adds to the selection; **right-drag still reorders** the cards; paging is untouched, and dragging off the end of a page does not page. |

### 20b. The landing ring

**What starts one.** The frame a **selected** colonist's published order cell
(`odyssey.pawn.order.cell`, §2e) changes to a new cell — read off the frame, not the click, exactly
as the lock-on is, so a refused move draws nothing and the same move clicked twice is quiet. The
order cell is published for a drafted move, a forced Equip and an attack on a building (§13i), and
the ring follows it in all three, as the bracket did. **An order already under way when the player
first sees it** — a colonist selected mid-walk, a world just loaded — is adopted at rest, faint,
with no snap.

**One ring per colonist, keyed on her and the cell.** Sent somewhere else mid-walk, the old ring
fades where it was while the new one snaps in on the new cell; for the length of the fade both are
drawn. Sent back to a cell whose ring is still fading, it snaps again, because that is a new order.

**What ends one.** The cell stops being published — she arrived, she was undrafted (a drafted move
needs the draft), the order became something else — or she is deselected, since only the
selection's orders are drawn (§2g).

**The clock is the lock-on's, not a copy of it.** `LandingRings` evaluates every frame through
`LockOnRing.Evaluate` and quantises through `LockOnRing.Quantise`: the snap from
`StartScale` 1.6 over `SnapSeconds` 0.2, the flash to `FlashAlpha` at the landing, the settle over
`FlashSeconds` 0.18 to `HoldAlpha` 0.35, and the linear fade over `FadeSeconds` **0.25 s**. The owner
was told "about 0.3 s" for the fade; the lock-on's 0.25 is inside that, and a second fade constant
would have been a second clock. A retune of the lock-on retunes this ring too, and
`LandingRingTests.TheRingRunsOnTheLockOnsClock` fails on any curve of its own.

**Its own numbers, in `LandingRings`, INVENTED for the playtest:**

| Number | Value | Why |
|---|---|---|
| `Radius` | 0.8 m (1.6 m across) | inside the 2.5 m cell and wider than a person's lock-on ring (0.575 m), so it reads as a *place* rather than a body |
| `OrderColours.Move` | `#dce4ec` | a cool near-white a step under the interface's ink (`TextPrimary` `#eef3f6`) |
| lift | 2 cm | the lock-on's `LockOnRingLift`, shared |

**The colour** has one owner, `OrderColours` (CLAUDE.md), and a test:
`OrderColoursTests.TheMoveRingIsPaleAndNeutralAndNoRed` holds it 80 points (the board's threshold)
from the attack red, the draft red, the hostile marker's salmon and every order hue, then pale (no
channel under `0xc0`) and neutral (channels within 24). It is **not** held apart from the selection
cursor's white: the cursor is brackets round a colonist and the ring lies on an empty cell, and a
rule the design does not need is a rule a later retune would fight.

**How it is drawn.** `OdysseyBootstrap.DrawLandingRings`, called straight after the draft marks:
`PrimitiveMeshes.UnitRing` through `ChunkRenderer.DrawRing` in the bracket material, draped with
`GroundRelief.Drape` on the destination cell's floor centre — the placement the bracket had — and
scaled by `Radius × scale`. A ring on a layer the slice does not draw is not drawn, the lock-on's
rule. **Cost**: one submission per ring, one ring per selected colonist under orders (two for a
fade's length after a re-order); it scales with the selection, never the board. The alpha is
quantised, so the animation reuses at most 33 cached materials for the hue. `DrawOrderLine` no
longer draws the bracket.

### 20c. Dragging across the roster

**The rule** is `Odyssey.Hud.RosterSweep`, Unity-free and fast-tier tested. A left press on a card
starts a sweep, and copies the page's cards in slot order.

- **A range, as the box is an area.** The sweep covers the cards from the one pressed to the one
  under the pointer, inclusive. Dragged back, it lets go of the cards it no longer spans. A flick
  that never lands on the cards in between still covers them, which is only sound because the strip
  is always **one row** (`HudLayout.StripRowsAllowed`, owner 2026-09-18) — a second row would need
  a rectangle instead.
- **Without Shift** the covered cards are the selection, the **pressed card first**, so the inspect
  pane shows whom the drag began on.
- **With Shift** they are added to the selection held at the press, which stays ahead of them.
- **A press that covers no other card is a click.** Without Shift, that colonist; with Shift, she is
  toggled in or out, as a Shift-click does in the world. Once a sweep has covered a second card it
  is a sweep for good: dragging back on to the pressed card leaves her selected rather than
  toggling her out, and is not taken for a click.
- **Only the page the press was on.** A card that was not on it covers nothing.

**What moved from the press to the release.** A plain click on a card was `ChooseColonist` on the
**press**: select her, put the slice on her layer and take the camera to her. A press cannot know
yet whether it is a click or the start of a drag, and a drag that swung the camera to its first card
would be a lurch nobody asked for. So the press now selects the card at once (the visible answer)
and **the slice change and the camera jump wait for the release**, and happen only for a plain
click. A Shift-click still never jumps.

**What was there.** The strip already had a Shift-drag (`_sweepingRoster`) that **toggled** every
card it entered — the catalogue's A2 "drag-select a range" (`10-ui-panel-catalogue.md`), built as a
toggle. It is replaced: a Shift-drag now adds, which is what Shift means on the world's box
(`SelectionDirector.PickMany`, additive). A drag that re-entered a card toggled it back out; a
range cannot.

**The view** (`HudShell.Panels.cs`, `NewCard`): the press copies `_cards`' ids into a scratch list
and calls `Press`; `PointerEnterEvent` on a card calls `Over` and, when it moved, writes the sweep's
selection through `SelectionDirector.PickMany` (non-additive, since the model already carries
Shift's base) with `SelectionChange.Boxed`; the press uses `Chosen`, or `Toggled` with Shift. The
release is the card's own `PointerUpEvent`, or `HudShell.Update` seeing the left button up wherever
the pointer is (the old sweep's rule, since a release off the strip never reaches a card).
A `PointerCancelEvent` drops the sweep with no jump. The left press does **not** capture the
pointer, or the other cards would never see it enter. Right-drag (button 2) is untouched code.

**Not gated on the tool.** The owner said "in default mode", describing where he meets it; the
card's click was never gated on the armed tool, and gating the drag alone would make a click and a
drag behave differently with a tool in hand.

### 20d. Do not undo by tidying

- **The landing ring calls `LockOnRing.Evaluate`.** A curve of its own is a second clock.
- **It starts off the frame, not the click**, for the lock-on's reason: a click draws an order the
  simulation may have refused.
- **The camera jump is on the release.** Moving it back to the press makes every drag swing the
  camera to its first card.
- **The sweep is a range, not the cards entered.** Entered cards leave gaps on a quick flick.

### 20e. Open, for the playtest

- Whether the pale ring reads against **snow, pale stone and a lit floor** at night; it is drawn
  lit, like the bracket was.
- Whether 1.6 m across is the right size, and whether the hold at 0.35 is too faint to find a
  squad's destinations on grass.
- The draft-red line ending on a pale ring: whether the two read as one mark.
- **An attack on a building also rides the order cell** (§13i), so it now wears the pale ring at the
  struck cell, as it wore the bracket. The lock-on ring round a building target is owed (§13k); until
  then the pale ring there says "going here", not "hitting that".
- Whether the jump on release (rather than press) is noticed.

### 20f. Tests, and the controls seen to fail

Fast tier: Sim **1,475** (unchanged), Hud **1,038** (from 1,016: `LandingRingTests` 12,
`RosterSweepTests` 9, `OrderColoursTests` one). Long **41**, all green. No golden moved.

| Test | Claim |
|---|---|
| `LandingRingTests.TheRingStartsOnTheFrameTheCellIsPublishedAndLandsWithAFlash` | nothing for a refused move; starts at 1.6; lands at 0.2 s on the flash; holds faint |
| `…TheRingRunsOnTheLockOnsClock` | every frame of the snap, the flash and the fade equals `LockOnRing.Evaluate` |
| `…ArrivingFadesTheRingAndThenItIsGone` | the cell unpublished: lets go where it was, fades, gone after `FadeSeconds` |
| `…EachColonistOfASquadWearsARingOnHerOwnCell` | two colonists, two cells, two snapping rings |
| `…SentElsewhereTheOldRingFadesAndTheNewOneSnaps` | re-ordered: the old fades, the new snaps, then only the new |
| `…AnUnselectedColonistsOrderDrawsNothing`, `…DeselectingFadesTheRing` | only the selection's orders |
| `…AnOrderAlreadyUnderWayIsAdoptedAtRestNotSnapped` | a load, a selection mid-walk and a new world object adopt at rest |
| `…TheSameOrderAgainDoesNotSnapAgain`, `…SentBackToAFadingRingSnapsAgain` | quiet on a repeat; a new order on a fading ring snaps |
| `…AnEquipOrderWearsTheRingAndABanditNever` | an undrafted colonist's Equip cell wears it; a hostile's published cell does not |
| `…TheRingIsAPlaceWiderThanAPersonAndInsideItsCell` | 0.575 m < `Radius`, 2 × `Radius` < 2.5 m |
| `OrderColoursTests.TheMoveRingIsPaleAndNeutralAndNoRed` | 80 from both reds, the salmon and every order hue; pale; neutral |
| `RosterSweepTests.AClickWithoutADragSelectsOneAndIsAClick` | a press replaces the selection with her, and the release is a click |
| `…DraggingAcrossCardsSelectsEveryCardPassedOver` | the owner's case: the range, pressed first, the held selection dropped, a sweep |
| `…AFlickPastCardsCoversThemAll`, `…DraggingLeftwardsCoversTheRangeWithThePressedCardFirst` | the range is filled and runs either way |
| `…DraggingBackLetsGoOfTheCardsNoLongerSpanned` | the range shrinks; home on the pressed card is still a sweep |
| `…ShiftAddsTheSweepToTheSelectionHeld` | Shift keeps the held selection ahead and adds the range |
| `…AShiftClickTogglesAndAShiftDragOnlyAdds` | Shift-click toggles in and out; a Shift-drag re-adds the pressed card |
| `…ACardOffThePageOrAfterTheReleaseChangesNothing`, `…ThePageIsTheOneThePressSaw` | nothing idle, off the page, after the release, or from a page refilled under the drag |

| Withheld | Failed |
|---|---|
| the snap (every ring adopted) | five `LandingRingTests`: the start, the clock, the squad, the re-order, the fading re-order |
| the release (rings held for ever) | `Arriving…`, `Deselecting…`, `SentElsewhere…`, `…LockOnsClock` |
| keyed on the colonist alone (the ring moved to the new cell) | `SentElsewhere…` |
| a fade of its own at 0.3 s | `…LockOnsClock` |
| the colonist check | `…AndABanditNever` |
| `Move` a pale pink `#f4c8cc`; the attack red; a mid grey `#9098a0` | `TheMoveRingIsPaleAndNeutralAndNoRed`, each |
| the range filled (only its two ends) | five `RosterSweepTests`, `AFlick…` among them |
| Shift ignored | `ShiftAdds…`, `AShiftClickToggles…` |
| every release a click | `DraggingAcross…`, `DraggingBack…`, `ShiftAdds…` |
| `Dragged` not sticky | `DraggingBack…` |
| every press composed as a click (Shift toggling the pressed card out) | seven `RosterSweepTests` |
| a plain drag keeping the held selection | `DraggingAcross…` |
| the page read live rather than copied | `ThePageIsTheOneThePressSaw` |

### 20g. Never compiled here

The fast tier does not build Presentation and Unity was not run. **Never compiled:**
`OdysseyBootstrap.cs` (`DrawLandingRings`, the two-argument `DrawOrderLine`), `HudShell.cs` and
`HudShell.Panels.cs` (`ApplyRosterSweep`, `FinishRosterSweep`, the card callbacks). Every Unity
call in them is one the same files already make — `ChunkRenderer.DrawRing`, `GroundRelief.Drape`,
`CellMetrics.FloorCentre`, `Time.unscaledTime`, `PointerDownEvent.shiftKey`, `PointerEnterEvent`,
`PointerUpEvent.button`, `SelectionDirector.PickMany` — so what is unverified is the
compilation, not an API shape. **Nothing tests that the pointer reaches the cards** (CLAUDE.md): in
particular, that `PointerEnterEvent` reaches the other cards while the left button is held. The old
Shift-sweep relied on the same, and was never reported broken.

## 21. The gate (C7, 2026-09-24, `claude/combat-c7`)

The last unit of the plan: the ten-day gate with and without hostiles, the benchmark rows, the
records. Built from `main` at `3ca5098c` (PR #194, every combat unit and playtest round in).

### 21a. Without hostiles

`SoakRunTests.TenDays` on seeds 1, 2 and 3 holds on today's `main` with nothing changed: every
colonist alive and published, no need at zero past 2,000 ticks, the reservation table agreeing every
1,000 ticks, a haul, a meal and a sleep completed. **No golden moved** in this unit — `Golden.cs`
is untouched and `GoldenMasterTests` is green in both the fast and Long tiers. The numbers are in
`docs/milestones/soak-runs.md`.

### 21b. With hostiles: `BanditSoakTests.TheGateWithRaids`

There is no storyteller (owner's call), so the test is the storyteller. On the soak's own board and
colony (120 × 120 × 16, `Scenario_Bare`, five colonists, five beds, nine stockpile cells):

- **Armed by the debug menu's own row**, `DebugArmColonists`, so every colonist holds one of the
  four weapons, dealt by her seed.
- **A hut**: a five-by-five ring of wooden walls with a door on the side facing the start, eight
  cells off it, raised outright — sixteen colony buildings and a door (§16) beside the scenario's
  beds.
- **Seven raids** through the debug spawn, a quarter into days 0, 1, 3, 4, 6, 7 and 9: one bandit
  or three, alternately, twenty cells out on a heading that turns. Thirteen bandits.
- **A party of three is answered**: every colonist on her feet is drafted (`SetDrafted`) and sent
  to the start with one `OrderMove` each, as a box selection sends a squad; the spread (§2d) stands
  them together and the four quiet hours (§2b) let them go. A lone bandit is left to each
  colonist's own response (§18). *Why*: unanswered, every seed was all down by day two and eight
  days of the gate were theft; drafted where each stood, five colonists across a 120-cell board met
  the party of three one at a time and lost every fight three to one. Gathered, they fight as a
  squad, which is the fight a player has.

**Asked every in-game hour**, in the invariants both soaks now share (`Invariants`, extended rather
than forked):

- nobody dead still on the board, nobody over the pool, nobody at or under nought standing;
- nobody down past the line she gets up at — whole for a colonist (§11c), the content's for an
  animal — and every downed pawn on `Job_Downed`, never drafted;
- **a carried pawn** is carried by a pawn that exists, is on `Job_Rescue`, names her, and stands
  where she is (§11a, §11f);
- **nobody inside anything solid** — the `TrappedPawnSystem` test: solid or blocked and not
  enterable in her own mode;
- **every claim a pawn believes it holds is in the table under its name**, and the table's count
  is the pawns' — together, "no reservation held by a pawn that has gone".

**Asked every tick** (scales with the pawns):

- **no attacker on a target already gone** for more than one tick: a pawn despawned, dead, or down
  when the attack was not to the death; a building no longer standing. The driver ends the job on
  the tick it sees one, so one tick — the fight's pass downing a target after the jobs ran — is the
  bound.
- **no bandit on *Fighting* at a building** without a step or a swing for more than **500
  ticks**: every unforced attack thinks again at `rechooseTicks` (300), and since §19b one whose
  every side is held ends at once, so the owner's "said they were fighting but kinda stood around"
  is at most a re-choice, the longest cooldown (144) and §19b's 183-tick hop. The same stand at a
  **colonist** is printed and not bound: a bandit that can get no side of her queues a ring back
  by design (§7c), which §19d measured at 2,506 ticks and left for the owner.

**Asked every hour, a rescue**: a downed colonist out of bed who could be carried — nothing hostile
standing, a free bed she can reach, and a colonist on her feet, undrafted, unbroken, awake and **not
already carrying somebody** — may lie like that for at most **two hours**. Rescue is emergency work
taken at the rescuer's next think (§11b). The first cut counted a rescuer already carrying someone
as free, and seed 1 read 12,500 ticks: one colonist left standing working through four patients in
turn, each carry 2,500–4,000 ticks. That is one rescuer at a time, not a fault, and the predicate
says so now. On failure it prints every standing colonist's job, reach, bed and claim.

**Determinism, twice.** A **lockstep twin** — the same seed, the same raids, built beside it — must
hash the same **every hour** for ten days, which is stronger than "the same final hash" and says
where two runs part if they ever do. And **a save at the first swing of day one's raid** (at a
colonist or a building, from the combat tape) is loaded into a fresh world and run a day on: it must
come to the original's hash. Day one because it is the one fight every seed has; by day four seed
1's colony is all down and its hut broken, so that party only steals.

**Accounted for at the end**, as the soak does: every bandit is on the board, dead with its corpse,
or off the edge with a ledger entry (§17e); every death has its corpse; `Job_Downed` never failed;
**no colonist died** — an unordered fight ends in downs (§3).

### 21c. What it found: a step lost across a load

The save round trip failed on seeds 1 and 2 at first. The lockstep twin agreed every hour, so the
fault was the round trip and not the simulation. One tick after the load a drafted colonist who had
joined a fight (§15) had not moved: every saved and hashed field matched, her progress through a
step 69,050 in the loaded world and 71,304 in the other.

`Job_AttackMelee` has five branches that **let a step already under way land** before they decide
(`if (!boundary) return Ongoing`): the join ended, in reach, the hold out of reach, and the building
mode's in-reach and every-side-held. They rely on the mover to finish the step along the path she
holds. **A path is never saved** (`MovementSystem`), and a driver that walks re-asks for one through
`GotoCell` — but these branches wait rather than walk, so a pawn loaded in reach part way through a
step held no path, nothing asked for one, and she stood frozen until her target moved away.

**Fixed**: `LandTheStep` asks for the path again when she has none and none is pending; it is served
before anybody steps in the same tick. A world that was never loaded always holds one there, so it
changes nothing in play: **no golden moved, and the gate's three final hashes were identical to the
digit before and after the fix.** `AttackDriverTests.ASaveTakenMidStepInReachResumesTheSame` finds
that moment in a three-against-two fight and saves there; without the fix it fails one tick on (the
loaded attacker 2,096 into the step against 4,192). `docs/bug-patterns.md`, 2026-09-24.

Nothing else broke. No invariant failed on any seed, nobody was freed from a wall, and nobody died.

### 21d. The numbers

| Seed | Drafts | Swings at pawns | Downed (colonists) | Died | Got up | Rescues (failed) | Buildings broken | Thefts | Colonists at day ten | Longest at a building | Longest unrescued |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4 | 220 | 7 (5) | 0 | 1 | 5 (1) | 16 of 16 | 12 | 0 up, 5 down | 95 | 0 |
| 2 | 9 | 420 | 14 (5) | 0 | 4 | 5 (1) | 16 of 16 | 8 | 0 up, 5 down | 95 | 2,500 |
| 3 | 6 | 327 | 12 (5) | 0 | 3 | 5 (2) | 16 of 16 | 7 | 2 up, 3 down | 95 | 0 |

22–62 s a seed for the colony and its twin, on a machine that was not quiet, so the Long tier grew from 41 tests in 1 m 44 s to
44 in 3 m 03 s. The final hashes are in `docs/milestones/soak-runs.md`.

**The colony loses, and that is for the owner.** Ten days of raids leave two seeds of three with
every colonist down. On seed 1 an armed, drafted squad of four gathered at the start lost to three
bandits on day one; the other two seeds' squads downed nine and seven bandits over the run. Every
number in the fight is INVENTED (§1), so this reads the tuning, not the code: a bandit carries a
machete and is dealt its level like anybody, and a colonist's weapon is a roll. Whether three
bandits should beat four armed colonists is a playtest question (the queue).

**What the gate does not cover**: animals in the fight (the bare board has none), a colonist
ordered to kill (every death here would be a fault), kidnap (seamed, §17f), and anything drawn — the
frame is §21e.

### 21e. What a fight costs

**The tick** (`TickBenchmarkTests.FiftyAgainstTenOnTheBigBoards`, new, and `TwentyAgainstTwenty` beside
it; Explicit; the Windows dev machine, AMD Ryzen 7 9800X3D, CoreCLR, **alone** — no Unity process and
the CPU at 5 % when it started; two runs, the swings identical to the digit and the times within the
spread shown). **Not the room lattice**: these colonies are built by `ColonyWorld.Build` on the played
map (barren and wooded), so the region counts — 24,412 on the scale target, 8,690 on Huge — are a
generated board's, and the figures compare with the game rather than with the class's lattice arms.
Fifty colonists and the board's own animals (23 and 20):

| Board | At peace | Fifty against ten | Fifty drafted against ten | Fight over peace |
|---|---|---|---|---|
| 250 × 250 × 40 | 0.096–0.108 ms | 0.158–0.187 | 0.186–0.189 | +0.05–0.09 ms |
| Huge 240 × 240 × 16 | 0.093–0.098 | 0.117–0.125 | 0.160–0.174 | +0.02–0.03 ms |
| 120 × 120 × 16, twenty against twenty (bare, the older row) | 0.036 | 0.087 | 0.091 | +0.05 ms |

The whole tick stays under **0.19 ms** in every arm. The dearest part is the drafted hold: its Pawns
phase is 0.073–0.081 ms against 0.022–0.028 undrafted, because each drafted colonist scans the pawns
every tick for a threat beside her or a fight to join (§15f) — fifty drafted is fifty scans of eighty
pawns. It scales with drafted × pawns and is a tenth of a millisecond at this size; the day a colony
drafts hundreds it is the number to watch.

**The frame** (`FrameTimeTests`, measured inside the full PlayMode tier, which started with no other
Unity process on the machine and the CPU at 7 %; 640 × 480, RTX 5070 Ti):

- **A fight in view** (`TheFrameWithAFightInView`): peace **1.79 ms**, brawl **2.24 ms**, 738 → 762
  draw calls. The difference is the figures (0.111 → 0.238 ms) and the overlays (0.010 → 0.033). At the
  C2 integration the same arm read 2.06 → 2.29 (§6E): the frame is faster underneath and the fight
  itself dearer by about 0.2 ms, which is the reactions, the draw and sheathe and the criticals the
  playtest rounds added (§8b, §9a–§9c).
- **Blood at its cap** (`TheBloodAtItsCap`, 200 marks): **1.87 → 1.95 ms**, 738 → 741 draw calls, three
  of them blood — §10d's ceiling holding.
- Not at a play resolution and not on the target laptop, like every frame number here.
