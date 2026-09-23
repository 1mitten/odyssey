# Combat — the lane briefs (Phase 2 onward)

**Written 2026-09-23 by the Phase 1 contracts step of `docs/plans/combat.md`, and corrected the
same day by a seam review** that read these briefs against the code before any lane started and
found eleven places where no lane could do its job from its own files, or where two lanes would
each have decided the same thing. The spine was fixed for the first kind and the rule written down
once for the second (`docs/design/33-combat.md` §5c, §5d, §5f and **§5j**). Every handle, field and
seam the combat line needs now exists on branch `claude/combat-c2`; what each seam is *for* is
`docs/design/33-combat.md` §5. This file says, lane by lane, **which files you own, which seams you
fill, which tests you add and which files you must not touch.** A lane that finds it needs to edit a
file it does not own has found a missed seam: stop and report it rather than editing — the merge
conflict it would cause is the thing this whole arrangement exists to avoid.

**Start every lane from the head of `claude/combat-c2` after the seam review** — the commit that
last changed this file (`git log -1 --format=%H -- docs/plans/combat-contracts.md`), not the first
contracts commit `bfe8bd52`, which lacks the seams below.

## Rules for every lane

- **Worktree** `D:\code\odyssey-combat-<lane>` (`-a`, `-b`, `-c`, `-d`), branch
  `claude/combat-lane-<lane>`, from the commit above. **Junction `Assets/Synty` first**
  (`docs/lessons.md`); never delete a worktree without unlinking it, never recursively delete a
  directory holding `Assets/Synty`.
- **Fast tier only** (`scripts/test-fast.sh`, and `--filter TestCategory=Long` before you hand
  over). **Do not run Unity**: the integrator runs both Unity tiers once, alone. The fast tier does
  not compile Presentation or Editor, so any Presentation file a lane edits is unproven until the
  integrator's run — **list every such file in the hand-over as never compiled** rather than
  claiming it compiles.
- **The goldens must not move.** Combat state is hashed only while set, the corpse registry and the
  damage store hash nothing while empty, and no golden window fights. If `GoldenMasterTests` fails,
  that is a missed seam or a behaviour leak, never a re-bake: stop and report.
- **Content numbers are frozen during Phase 2.** `PawnContentDefTests.ContentFingerprint` and
  `ConstructionContentDefTests.BuildingFingerprint` are one line each that two lanes would both
  change. A lane that wants a Def value retuned writes the change and its reason in its hand-over;
  the integrator makes it once, with the fingerprint.
- **Registry keys.** Every key the design needs was added in Phase 1 (`design 33 §5a`). A lane that
  still needs one appends it to `docs/design/icon-keys.csv` **directly under the last row of the
  same namespace** it already uses and rebuilds the wiki and `Registry.g.cs`; if the generated files
  conflict at the merge, the integrator regenerates them rather than resolving by hand.
- **New files get a `.meta`** (`fileFormatVersion: 2`, a fresh `guid` of 32 hex). New folders too.
- **Every per-tick loop states what it scales with** (`docs/process.md` §3), and every claim has a
  negative control that has been **seen to fail**. British English in documents.
- **Commit small, push after every commit** (checkouts here get reset). Never `git add -A`.
- **Hand over** with the commit, the tests added and their counts, the fast and Long numbers, the
  Presentation files never compiled, and anything you could not do.

### Already in the spine — do not rebuild these

The seam review put these in the shared files so that no lane has to. They are tested in
`CombatContractTests` and `CombatInspectSeamTests`, which no lane edits.

| What | Where | Design 33 |
|---|---|---|
| **A stun is a pause**: a stunned pawn's job neither ticks nor ends, it does not think, and it lands the step in hand and takes no other | `JobSystem.TickPawn`, `MovementSystem.Advance` | §5c |
| **Despawn releases the pawn's beds** | `PawnRegistry.Despawn` → `ConstructionGrid.ReleaseBedsOf` | §5c |
| **A forced order is refused for a marauder and a downed colonist** | `JobSystem.CanForce` | §5c |
| **The marauder's kind names its weapon** (`Item_Machete`), and `Spawn` calls `IWeaponRules.ArmOnSpawn` for it | `PawnKindDef.weapon`, `PawnContent.WeaponOf`, `PawnRegistry.Spawn` | §5b, §5c |
| **`odyssey.pawn.hp.max` for every person**, whole or hurt; `hp` stays sparse | `PawnRegistry` publish | §5d |
| **The pane's shape is the model's**: `ShowsFace`, `ShowsColonistBody`, `ShowsTabBox`, `AvatarKey`; `Commands` drawn for any subject | `InspectModel`, read by `HudShell.Inspect` | §5f |
| **A corpse can be selected and inspected**: `SelectionDirector.Corpse` / `ChooseCorpse`, `InspectSubject.Corpse`, `InspectModel.SetCorpse` with a stub pane | `SelectionDirector`, `InspectModel`, `HudShell` | §5f |

## Lane A — the fight (simulation), C2

**Fills:** the swing, the damage, going down, dying, healing, retaliation and revenge, the hostile's
hunt, the drafted colonist's adjacent auto-attack, the attack order.

**Owns (may edit):**
- `Assets/Odyssey/Sim/Pawns/Combat/MeleeRules.cs` — `IMeleeRules`: write `Resolve` (hit on
  `PawnPurpose.MeleeHit`, dodge on `MeleeDodge`, damage within `CombatDef.damageSpreadPerMille` on
  `MeleeDamage`, stun for a blunt `Armament` with `stunPerMille` on `Stun`), each drawn from
  `(ctx.Seed, tick, salt ^ attacker id)` as the other pawn rolls are.
- `Combat/CombatSystem.cs` — the tick: resolve every swing whose wind-up tick has come, apply the
  outcome **through one method** (hit points, stun, `NextSwingTick`, experience from
  `Armament.Attack.experiencePerSwing` into `SkillIndex.Melee` for a person), down a pawn at
  `HpMilli <= 0` (end its job through `Jobs`, start `Job_Downed`, end any draft, **and end any
  mental break: `BreakTicksLeft = 0`** — §5j), kill it at `HpMilli <= DeathAtMilli` **deferred**
  (`ctx.Defer`: `Corpses.Add`, `CombatHooks.RaiseDied`, end its job, `PawnRegistry.Despawn` — which
  already releases reservations **and the pawn's beds**, so do not do either again), heal
  (`bedHealPerDay` for a colonist in a bed, `animalHealPerDay` for an animal anywhere) and get up at
  `downedRecoverAtPerMille`, expire `RetaliateUntilTick`. **A swing whose attacker is stunned or
  down when its wind-up ends does not land** (§5j). Raise `CombatHooks.RaiseDamageApplied` /
  `RaiseDowned` / `RaiseDied`, and `CombatLog.Report` every moment (`Swing`, `Hit`, `Miss`, `Dodge`,
  `Stun`, `Downed`, `Died`, `Recovered`) **with the armament's `ItemDef` as `weapon`** (−1 for fists
  or teeth), because lane B picks the clip family from it. Add partial or helper files beside it
  freely.
- **The stun: set `StunnedUntilTick` and nothing else.** The hold is in the spine (the table
  above): do **not** `Interrupt` or end the victim's job for a stun — that drops a carried load,
  frees a bed and throws away a player's order, which is a different mechanic.
- `Combat/AttackMeleeJobDriver.cs`, `Combat/FleeJobDriver.cs`, `Combat/DownedJobDriver.cs`.
  The attack driver closes on `Pawn.CombatTarget` (re-plan every `chaseRepathTicks`), starts a
  swing when adjacent and `NextSwingTick` has come, reports `PawnGesture.Strike`. **`WorkFocus`
  returns the target's cell during the wind-up only, and -1 otherwise** — it is how presentation
  turns the figure. Buildings as targets are C6's — leave a clear branch point.
- `Combat/CombatThinkNodes.cs` — `DownedThinkNode`, `SelfDefenceThinkNode` (a colonist struck by a
  colonist fights back; a hostile on an adjacent cell is hit), `HostileThinkNode` (nearest reachable
  standing colonist), `AnimalCombatThinkNode` (revenge on `SpeciesDef.revengePerMille`, rolled on
  `PawnPurpose.Revenge` at the hit; else flee `fleeCells`).
- `Assets/Odyssey/Sim/Pawns/JobSystem.Attack.cs` — `HandleOrderAttack` (a drafted colonist, a
  target pawn in `B`; a forced job like a move, `CombatTarget` set). **`B = 0` (a building) stays
  refused until C6** (§5j).
- `Assets/Odyssey/Sim/Pawns/Draft.cs` — the hold's adjacent auto-attack (design 33 §1).
- `Assets/Odyssey/Tests/Sim/TickBenchmarkTests.cs` — the 20-against-20 row only.
- New test files under `Assets/Odyssey/Tests/Sim/`.

**Reads but must not edit:** `Pawn.cs` (the combat fields and `BreakTicksLeft` are `internal set`,
so you write them from your own files), `PawnContext.cs`, `CombatHooks.cs`, `CombatLog.cs`,
`CorpseRegistry.cs`, `CombatDef.cs`, `IMeleeRules.cs`, `IWeaponRules.cs`, `CombatAspects.cs`.

**Must not touch:** anything in `Presentation/`, `Hud/`, `Sim.Contracts/`, `Editor/`;
`PawnContent.cs`, `Pawn.cs`, `PawnRegistry.cs`, `JobSystem.cs`, `JobSystem.Draft.cs`,
`MovementSystem.cs`, `ConstructionGrid.cs`, `ColonyComposition.cs`, `ColonyWorld.cs`,
`CombatSection.cs`; lane D's `WeaponRules.cs`, `EquipJobDriver.cs`, `JobSystem.Equip.cs`,
`CombatListeners.cs`; the C4/C6 files (`RescueJobDriver.cs`, `RescueWorkGiver.cs`,
`JobSystem.Rescue.cs`, `EdificeDamage.cs`); the spine's tests (`CombatContractTests.cs`); any Def
XML; `Golden.cs`.

**Tests to add (fast tier):** `CombatMathTests` (the curves through `MeleeRules`, the spread's
bounds, a blunt stun and a sharp none, each stream independent); `AttackDriverTests` (closes,
swings on cooldown, a new order does not reset `NextSwingTick`, the gesture serial moves,
`WorkFocus` is the target's cell in the wind-up and -1 after it, every report carries the weapon);
`DownedDeathTests` (down at 0, dead at −50 %, the corpse, the hooks fired once each, the pawn gone
from every per-pawn loop, a death is deferred, **a broken colonist downed has one `Job_Downed`
start and no failures over the rest of the break**, **a dead colonist owns no bed**); a stunned
attacker's wound-up swing does not land; `HostileTests` (a marauder hunts the nearest standing
colonist and ignores a downed one); `AnimalRevengeTests` (a hog mostly turns, a rat mostly runs,
both seeded); the **mid-swing save round trip** (identical hash after the load and 600 ticks on); a
**Long** "a marauder a day for ten days"; the **20-against-20** row in `TickBenchmarkTests`.
**Goldens unchanged.**

## Lane B — the fight (drawn), C2

**Fills:** the swing, the reactions, the downed and stunned loops, death and the corpse, the Sword
Combat clips and their computed fallbacks, the health bars, the floating text, the hostile marker,
the combat sounds, the cursor on a selected corpse.

**Owns (may edit):** everything under `Assets/Odyssey/Presentation/` **except
`Ui/HudShell.Combat.cs` and `Ui/HudShell.Debug.cs`** (lane C's) — in particular
`World/PawnFigureDirector.Combat.cs` (`OnCombatEvent`), `World/CombatPose.cs`,
`World/CorpseDirector.cs` (built, synced and disposed already), `Bootstrap/CombatFeedback.cs`
(`Handle`: route to the figures, the sounds, the floating text), `Rendering/ModuleCatalogue.cs` (the
combat rows' data shape, e.g. an impact time per clip), `Audio/SoundIds.cs` and new clips,
`Bootstrap/OdysseyBootstrap.cs` and `SelectionPresenter.cs` where drawing or picking needs them;
`Assets/Editor/Odyssey/PlayScene.cs` (the catalogue rows for `ModuleIds.CombatRows`: the **Polygon,
in-place, non-returning** clips of `docs/research/synty-sword-combat.md` only, never the Generic
`Dodge_R` or base idle); tests under `Assets/Odyssey/Presentation/Tests/` and
`Assets/Odyssey/Tests/PlayMode/`. `Ui/HudShell.Inspect.cs` and `Ui/HudShell.cs` are yours, but the
combat line should need nothing in them: the pane's shape is `InspectModel`'s now (§5f).

**Reads:** `PawnView.Flags`, `WorldSnapshot.CombatEvents` (through `CombatFeedback`'s watermark),
`WorldSnapshot.Corpses`, `PawnGesture.Strike`, the aspects `odyssey.pawn.hp`, `.hp.max`, `.weapon`,
`.order.target` (the literals are in `Hud.CombatAspectNames`; `hp.max` is published for every
person, so **the bar is owed where `hp` is present**, not where the pool is). **What** a bar, a
floating word or a marker says is lane C's: call `CombatFeedbackModel.HealthBar`, `FloatingText`,
`FloatingColour`, `HostileMarker` — they answer "draw nothing" until lane C's branch merges, so draw
from them and test against a stub of your own. A click on a corpse calls
`HudDirectors.ChooseCorpse`; **the selected corpse is `HudDirectors.Selection.Corpse`** (a
`CorpseView.Id`, 0 for none), which your cursor brackets — test the bracket by calling
`Selection.ChooseCorpse(id)` directly, since `HudDirectors.ChooseCorpse` answers false until lane C
merges.

**The swing (§5j):** the computed work stroke (`WorkSwing` / `WorkStyle`) **never plays for
`Job_AttackMelee`**, even though its driver reports a `WorkFocus` during the wind-up — that focus is
for turning the figure. The clip family comes from the event's `Weapon` (the item's
`AttackDef.style`), else from the flags: a person fights with fists, an animal bites.

**Must not touch:** `Sim/`, `Sim.Contracts/`, `Hud/`, `Tests/Sim/`, `Tests/Hud/`,
`Presentation/Ui/HudShell.Combat.cs`, `Presentation/Ui/HudShell.Debug.cs`, any Def XML,
`icon-keys.csv`.

**Tests to add:** EditMode — every role in `ModuleIds.CombatRows` resolves to clips or falls back to
`CombatPose` with the pack absent (ask whether the art **resolved**, never whether a catalogue
exists — `CLAUDE.md`, the runner has no `Assets/Synty`); `CombatPose` lands each swing's impact on
the attack's `windupTicks`; `CombatFeedback` hands each event on once and replays none after a
world change (feed it a scripted `CombatEventView` stream — lane A is not needed); a corpse is
drawn with the face its `RollSeed` deals; a `Job_AttackMelee` figure with a work focus plays no work
stroke; the cursor brackets a selected corpse. **Never runs Unity**: hand the branch to the
integrator, listing which tests and files have never been compiled.

## Lane C — the interface, C2/C3

**Fills:** the orders a right-click gives, the Health tab, the corpse pane, the marauder's pane, the
words and colours of the feedback, the Spawn tab's rows, the roster and Work tab by flags.

**Owns (may edit):** everything under `Assets/Odyssey/Hud/` — in particular `CombatOrders.cs`
(`Route`, below), `CombatFeedbackModel.cs` (the four answers lane B draws), `InspectModel.cs` (the
Health tab's model; the **corpse subject**, whose stub refresh is already there — name it "Corpse
of X" from `ColonistNames` over the corpse's `RollSeed`, or the kind's label, and write its state
line into `Job`; and the **shape answers** `ShowsFace`, `ShowsColonistBody`, `ShowsTabBox` and
`AvatarKey` for the marauder and the corpse — a marauder needs no needs, skills, Health tab or
Draft button, and wears `ui.pawn.marauder`), `HudDirectors.cs` (`ChooseCorpse`: check the corpse
is in the frame, call `Selection.ChooseCorpse`, answer true), `DebugDirector.cs` (the marauder and
the four weapon rows: `SpawnPawn` with `PawnKindIndex` 3; `GiveResource` with the item def and a
count of 1), `OrderColours.cs` (an attack colour, if one is wanted), `WorkGridModel.cs`,
`RosterModel.cs`, `AlmanacDirector.cs` (a corpse's entry, if any); plus **the two Presentation
files this lane owns: `Ui/HudShell.Combat.cs`** (the Health tab body, already called by
`HudShell.Inspect`) **and `Ui/HudShell.Debug.cs`** (the Spawn rows). Tests under
`Assets/Odyssey/Tests/Hud/`.

**`CombatOrders.Route` in C2/C3:**
- right-click an **animal or a hostile** → `OrderAttack` from the selected **drafted** colonists;
- **Ctrl**+right-click a colonist → `OrderAttack`, drafted colonists only;
- right-click a **downed colonist** → `OrderRescue`, drafted colonists only;
- right-click a **weapon** → `OrderEquip` for the **primary selected colonist, drafted or not**
  (§5j: a fetch, not a fight);
- **anything else falls through to the move — a building included.** Routing a click on a building
  to an attack is C6's, and then only for an edifice that occupies the cell (wall, door,
  furniture), never a floor or slab (§5j); `OrderAttack` with `B = 0` is refused throughout C2/C3.

**Keys already in the registry:** `ui.command.attack`, `ui.command.rescue`, `ui.command.equip`,
`ui.pawn.corpse`, `ui.pawn.hostile`, `ui.pawn.marauder`, `ui.combat.{miss,dodge,stunned,health,dead}`,
`ui.debug.spawn{marauder,bat,crowbar,machete,arcblade}`, `ui.status.{fighting,fleeing,downed,equipping,rescuing}`.
Call `Registry.Label(key)`, never a literal (`RegistryTests`). **The Health tab reads the pool from
`odyssey.pawn.hp.max`, published for every person; an absent `odyssey.pawn.hp` beside it means
whole** (§5d).

**Must not touch:** `Sim/`, `Sim.Contracts/`, any Presentation file but the two above (in
particular not `HudShell.Inspect.cs` or `HudShell.cs` — if the pane needs a shape the four answers
cannot express, that is a missed seam: report it), `Tests/Sim/`, `Tests/Hud/CombatInspectSeamTests.cs`
**except** its marauder and corpse assertions, which move with your answers, any Def XML.

**Tests to add (Hud fast tier):** `CombatOrdersTests` (each of the owner's four gestures, the
Ctrl rule, a click nothing claims is still a move, **a click on a floored or walled cell is still a
move**, an undrafted selection sends no attack or rescue **but does send an equip**);
`CombatFeedbackModelTests` (a bar over the hurt and the drafted and nobody else; miss, dodge and the
damage in whole points; the marker on a hostile only); the corpse pane's title for a colonist, an
animal and a marauder; the marauder's pane has no needs, skills, Health tab or Draft button; the
Health tab's rows, whole and hurt; the registry and font tests stay green (`HudFontTests`: any
non-ASCII character must exist in both shipped fonts). **The hand-over lists `HudShell.Combat.cs`
and `HudShell.Debug.cs` as never compiled** — the fast tier does not compile Presentation.

## Lane D — weapons (simulation), C3

**Fills:** the equipped weapon, the equip job and order, arming the marauder on spawn, dropping the
weapon on death, the starting kit.

**Owns (may edit):** `Assets/Odyssey/Sim/Pawns/Combat/WeaponRules.cs` (`ArmamentOf`: the equipped
weapon's `ItemDef.weapon` first — resolve `Pawn.EquippedItem` through `ColonyItems`; `CanEquip`;
**`ArmOnSpawn`**: `ctx.Content.WeaponOf(pawn.Kind)` is the item def — make it through
`ColonyItems.Spawn` at the pawn's cell, take it off the ground exactly as the equip job does, and
set `EquippedItem`; `PawnRegistry.Spawn` already calls it for the marauder, after adoption),
`Combat/EquipJobDriver.cs` (walk, lift, take into the hand, put down what was there; the item keeps
no cell while held), `Assets/Odyssey/Sim/Pawns/JobSystem.Equip.cs` (`HandleOrderEquip`: **accepted
for a colonist drafted or not**; refused for a downed pawn, a hostile, an animal and a pawn that
does not exist — §5j), `Combat/CombatListeners.cs` (register a listener that drops the weapon where
a pawn **dies** — a downed pawn keeps it, the C2 default), new files beside them,
`ColonyScenario.cs` for the starting kit (**only** in scenarios no golden builds on — the goldens
use `ScenarioDef.Bare`), tests under `Assets/Odyssey/Tests/Sim/`.

**Division with lane A, which the plan's table left loose:** the stun is **rolled and applied by
lane A** (`MeleeRules.Resolve`, `CombatSystem`) from the numbers in the `Armament`; lane D's part is
that the armament of a held bat or crowbar **carries** its stun, which a test of `ArmamentOf` pins.
Weapon spawning from the debug menu needs no simulation work — `GiveResource` already places an item
of any def — so the Spawn rows are lane C's.

**Must not touch:** `MeleeRules.cs`, `CombatSystem.cs`, the attack, flee and downed drivers,
`CombatThinkNodes.cs`, `JobSystem.Attack.cs`, `Draft.cs`; `Presentation/`, `Hud/`, `Sim.Contracts/`;
`Pawn.cs`, `PawnRegistry.cs` (the `odyssey.pawn.weapon` aspect is already published from
`EquippedItem`, and the call to `ArmOnSpawn` is already there), `IWeaponRules.cs`, `PawnContent.cs`,
`JobSystem.cs`, `ColonyComposition.cs`, `CombatSection.cs`, `CombatContractTests.cs`, any Def XML
(the weapons' numbers and the marauder's machete are frozen; propose changes in the hand-over),
`Golden.cs`.

**Tests to add (fast tier):** `WeaponRulesTests` (bare hands, teeth, and each of the four weapons'
armament once held, a blunt one carrying its stun); `EquipTests` (the order walks, lifts, holds;
**accepted for an undrafted colonist**; a second weapon puts the first down; refused for a
forbidden item, a non-weapon, an animal, a hostile and a downed colonist; the save round trip with
a weapon in the hand); `WeaponDropTests` (dies → the weapon on the ground at the corpse; downed →
still held); **`MarauderArmsTests`** (a marauder spawned by the debug intent holds its machete, the
item has no cell while held and `odyssey.pawn.weapon` publishes it, and on death it lies at the
corpse; a colonist and an animal spawned the same way hold nothing); `StartingKitTests` (the kit's
weapons in the playtest scenario and **none in `ScenarioDef.Bare`**). **Goldens unchanged.**

## Phase 4 lanes, named now so their files are known

- **C4 rescue:** `Combat/RescueJobDriver.cs`, `Combat/RescueWorkGiver.cs`, `JobSystem.Rescue.cs`,
  and a listener appended in `CombatListeners.cs` if it needs one. The carried patient is
  `Pawn.CarriedBy` and `PawnFlags.Carried`; the cradle comes from `CarryPose`.
- **C5 friendly fire:** a listener in `CombatListeners.cs` (after C4's), two thoughts in
  `Thoughts.xml` and `ThoughtIndex` — a content change, so the fingerprint moves once, in that lane.
  **Goldens asserted unchanged.**
- **C6 buildings:** `EdificeDamage` is the store; the attack driver's building mode (a branch lane A
  leaves), `HandleOrderAttack` with `B = 0`, demolishing at nought with no refund, and the
  right-click routing in `CombatOrders.Route` — **an edifice occupying the cell only** (wall, door,
  furniture), with a `CombatOrdersTests` case showing a click on a floored cell still moves (§5j).
  **Goldens asserted unchanged.**

## The integrator (Phase 3)

Merge A, D, C, then B — simulation first, so each merge adds what the next one reads. Regenerate
the wiki and `Registry.g.cs` rather than resolving their conflicts. Apply the lanes' proposed Def
changes and the fingerprints once. Run EditMode, then PlayMode **alone on the machine** (check
`Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` for batch runs first), the frame budget
with a fight in view, and the player-build smoke test (`Build/Win64/Odyssey.exe -odyssey-newgame`),
which C2 needs because it ships clips. **Compile every Presentation file the lanes listed as never
compiled first**, and read lane C's shape answers against the pane on screen: a marauder with a
Draft button or a corpse wearing the colonist badge is the seam not being used.
