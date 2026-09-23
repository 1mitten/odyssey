# Combat — the lane briefs (Phase 2 onward)

**Written 2026-09-23 by the Phase 1 contracts step of `docs/plans/combat.md`.** Every handle,
field and seam the combat line needs now exists on branch `claude/combat-c2`; what each seam is
*for* is `docs/design/33-combat.md` §5. This file says, lane by lane, **which files you own, which
seams you fill, which tests you add and which files you must not touch.** A lane that finds it
needs to edit a file it does not own has found a missed seam: stop and report it rather than
editing — the merge conflict it would cause is the thing this whole arrangement exists to avoid.

Start every lane from the Phase 1 commit (the head of `claude/combat-c2` when this file landed).

## Rules for every lane

- **Worktree** `D:\code\odyssey-combat-<lane>` (`-a`, `-b`, `-c`, `-d`), branch
  `claude/combat-lane-<lane>`, from the Phase 1 commit. **Junction `Assets/Synty` first**
  (`docs/lessons.md`); never delete a worktree without unlinking it, never recursively delete a
  directory holding `Assets/Synty`.
- **Fast tier only** (`scripts/test-fast.sh`, and `--filter TestCategory=Long` before you hand
  over). **Do not run Unity**: the integrator runs both Unity tiers once, alone. The fast tier does
  not compile Presentation or Editor, so lane B's work is unproven until the integrator's run —
  say so in the hand-over rather than claiming it compiles.
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
- **Hand over** with the commit, the tests added and their counts, the fast and Long numbers, and
  anything you could not do.

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
  `HpMilli <= 0` (end its job through `Jobs`, start `Job_Downed`, end any draft), kill it at
  `HpMilli <= DeathAtMilli` **deferred** (`ctx.Defer`: `Corpses.Add`, `CombatHooks.RaiseDied`,
  end its job, `PawnRegistry.Despawn`), heal (`bedHealPerDay` for a colonist in a bed,
  `animalHealPerDay` for an animal anywhere) and get up at `downedRecoverAtPerMille`, expire
  `RetaliateUntilTick`. Raise `CombatHooks.RaiseDamageApplied` / `RaiseDowned` / `RaiseDied`, and
  `CombatLog.Report` every moment (`Swing`, `Hit`, `Miss`, `Dodge`, `Stun`, `Downed`, `Died`,
  `Recovered`). Add partial or helper files beside it freely.
- `Combat/AttackMeleeJobDriver.cs`, `Combat/FleeJobDriver.cs`, `Combat/DownedJobDriver.cs`.
  The attack driver closes on `Pawn.CombatTarget` (re-plan every `chaseRepathTicks`), starts a
  swing when adjacent and `NextSwingTick` has come, reports `PawnGesture.Strike`. Buildings as
  targets are C6's — leave a clear branch point.
- `Combat/CombatThinkNodes.cs` — `DownedThinkNode`, `SelfDefenceThinkNode` (a colonist struck by a
  colonist fights back; a hostile on an adjacent cell is hit), `HostileThinkNode` (nearest reachable
  standing colonist), `AnimalCombatThinkNode` (revenge on `SpeciesDef.revengePerMille`, rolled on
  `PawnPurpose.Revenge` at the hit; else flee `fleeCells`).
- `Assets/Odyssey/Sim/Pawns/JobSystem.Attack.cs` — `HandleOrderAttack` (a drafted colonist, a
  target pawn in `B`; a forced job like a move, `CombatTarget` set).
- `Assets/Odyssey/Sim/Pawns/Draft.cs` — the hold's adjacent auto-attack (design 33 §1).
- `Assets/Odyssey/Tests/Sim/TickBenchmarkTests.cs` — the 20-against-20 row only.
- New test files under `Assets/Odyssey/Tests/Sim/`.

**Reads but must not edit:** `Pawn.cs` (the combat fields are `internal set`, so you write them from
your own files), `PawnContext.cs`, `CombatHooks.cs`, `CombatLog.cs`, `CorpseRegistry.cs`,
`CombatDef.cs`, `IMeleeRules.cs`, `IWeaponRules.cs`, `CombatAspects.cs`.

**Must not touch:** anything in `Presentation/`, `Hud/`, `Sim.Contracts/`, `Editor/`;
`PawnContent.cs`, `Pawn.cs`, `PawnRegistry.cs`, `JobSystem.cs`, `JobSystem.Draft.cs`,
`ColonyComposition.cs`, `ColonyWorld.cs`, `CombatSection.cs`; lane D's `WeaponRules.cs`,
`EquipJobDriver.cs`, `JobSystem.Equip.cs`, `CombatListeners.cs`; the C4/C6 files
(`RescueJobDriver.cs`, `RescueWorkGiver.cs`, `JobSystem.Rescue.cs`, `EdificeDamage.cs`); any Def XML;
`Golden.cs`.

**Tests to add (fast tier):** `CombatMathTests` (the curves through `MeleeRules`, the spread's
bounds, a blunt stun and a sharp none, each stream independent); `AttackDriverTests` (closes,
swings on cooldown, a new order does not reset `NextSwingTick`, the gesture serial moves);
`DownedDeathTests` (down at 0, dead at −50 %, the corpse, the hooks fired once each, the pawn gone
from every per-pawn loop, a death is deferred); `HostileTests` (a marauder hunts the nearest
standing colonist and ignores a downed one); `AnimalRevengeTests` (a hog mostly turns, a rat mostly
runs, both seeded); the **mid-swing save round trip** (identical hash after the load and 600 ticks
on); a **Long** "a marauder a day for ten days"; the **20-against-20** row in
`TickBenchmarkTests`. **Goldens unchanged.**

## Lane B — the fight (drawn), C2

**Fills:** the swing, the reactions, the downed and stunned loops, death and the corpse, the Sword
Combat clips and their computed fallbacks, the health bars, the floating text, the hostile marker,
the combat sounds.

**Owns (may edit):** everything under `Assets/Odyssey/Presentation/` **except**
`Ui/HudShell.Combat.cs` — in particular `World/PawnFigureDirector.Combat.cs`
(`OnCombatEvent`), `World/CombatPose.cs`, `World/CorpseDirector.cs` (built, synced and disposed
already), `Bootstrap/CombatFeedback.cs` (`Handle`: route to the figures, the sounds, the floating
text), `Rendering/ModuleCatalogue.cs` (the combat rows' data shape, e.g. an impact time per clip),
`Audio/SoundIds.cs` and new clips, `Bootstrap/OdysseyBootstrap.cs` and `SelectionPresenter.cs` where
drawing or picking needs them; `Assets/Editor/Odyssey/PlayScene.cs` (the catalogue rows for
`ModuleIds.CombatRows`: the **Polygon, in-place, non-returning** clips of
`docs/research/synty-sword-combat.md` only, never the Generic `Dodge_R` or base idle); tests under
`Assets/Odyssey/Presentation/Tests/` and `Assets/Odyssey/Tests/PlayMode/`.

**Reads:** `PawnView.Flags`, `WorldSnapshot.CombatEvents` (through `CombatFeedback`'s watermark),
`WorldSnapshot.Corpses`, `PawnGesture.Strike`, the aspects `odyssey.pawn.hp`, `.hp.max`, `.weapon`,
`.order.target` (the literals are in `Hud.CombatAspectNames`). **What** a bar, a floating word or a
marker says is lane C's: call `CombatFeedbackModel.HealthBar`, `FloatingText`, `FloatingColour`,
`HostileMarker` — they answer "draw nothing" until lane C's branch merges, so draw from them and
test against a stub of your own. A click on a corpse calls `HudDirectors.ChooseCorpse`.

**Must not touch:** `Sim/`, `Sim.Contracts/`, `Hud/`, `Tests/Sim/`, `Tests/Hud/`,
`Presentation/Ui/HudShell.Combat.cs`, any Def XML, `icon-keys.csv`.

**Tests to add:** EditMode — every role in `ModuleIds.CombatRows` resolves to clips or falls back to
`CombatPose` with the pack absent (ask whether the art **resolved**, never whether a catalogue
exists — `CLAUDE.md`, the runner has no `Assets/Synty`); `CombatPose` lands each swing's impact on
the attack's `windupTicks`; `CombatFeedback` hands each event on once and replays none after a
world change (feed it a scripted `CombatEventView` stream — lane A is not needed); a corpse is
drawn with the face its `RollSeed` deals. **Never runs Unity**: hand the branch to the integrator,
listing which tests have never been compiled.

## Lane C — the interface, C2/C3

**Fills:** the orders a right-click gives, the Health tab, the corpse pane, the words and colours of
the feedback, the Spawn tab's rows, the roster and Work tab by flags.

**Owns (may edit):** everything under `Assets/Odyssey/Hud/` — in particular `CombatOrders.cs`
(`Route`: right-click an animal, a hostile or a building **attacks**; **Ctrl**+right-click a colonist
attacks it; a downed colonist **rescues**; a weapon **equips**; anything else falls through to the
move), `CombatFeedbackModel.cs` (the four answers lane B draws), `InspectModel.cs` (the Health tab's
model and the corpse subject — "Corpse of X" from `ColonistNames` over the corpse's `RollSeed`, or
the kind's label), `HudDirectors.cs` (`ChooseCorpse`), `DebugDirector.cs` (the marauder and the four
weapon rows: `SpawnPawn` with `PawnKindIndex` 3; `GiveResource` with the item def and a count of 1),
`OrderColours.cs` (an attack colour, if one is wanted), `WorkGridModel.cs`, `RosterModel.cs`; plus
**`Assets/Odyssey/Presentation/Ui/HudShell.Combat.cs`** (the Health tab body, already called by
`HudShell.Inspect`) and **`Ui/HudShell.Debug.cs`** (the Spawn rows) — the two Presentation files
this lane owns. Tests under `Assets/Odyssey/Tests/Hud/`.

**Keys already in the registry:** `ui.command.attack`, `ui.command.rescue`, `ui.command.equip`,
`ui.pawn.corpse`, `ui.pawn.hostile`, `ui.pawn.marauder`, `ui.combat.{miss,dodge,stunned,health,dead}`,
`ui.debug.spawn{marauder,bat,crowbar,machete,arcblade}`, `ui.status.{fighting,fleeing,downed,equipping,rescuing}`.
Call `Registry.Label(key)`, never a literal (`RegistryTests`).

**Must not touch:** `Sim/`, `Sim.Contracts/`, any Presentation file but the two above,
`Tests/Sim/`, any Def XML.

**Tests to add (Hud fast tier):** `CombatOrdersTests` (each of the owner's four gestures, the
Ctrl rule, a click nothing claims is still a move, an undrafted selection sends nothing);
`CombatFeedbackModelTests` (a bar over the hurt and the drafted and nobody else; miss, dodge and the
damage in whole points; the marker on a hostile only); the corpse pane's title for a colonist, an
animal and a marauder; the Health tab's rows; the registry and font tests stay green
(`HudFontTests`: any non-ASCII character must exist in both shipped fonts).

## Lane D — weapons (simulation), C3

**Fills:** the equipped weapon, the equip job and order, dropping the weapon on death, the
starting kit.

**Owns (may edit):** `Assets/Odyssey/Sim/Pawns/Combat/WeaponRules.cs` (`ArmamentOf`: the equipped
weapon's `ItemDef.weapon` first — resolve `Pawn.EquippedItem` through `ColonyItems`; `CanEquip`),
`Combat/EquipJobDriver.cs` (walk, lift, take into the hand, put down what was there; the item keeps
no cell while held), `Assets/Odyssey/Sim/Pawns/JobSystem.Equip.cs` (`HandleOrderEquip`),
`Combat/CombatListeners.cs` (register a listener that drops the weapon where a pawn **dies** — a
downed pawn keeps it, the C2 default), new files beside them, `ColonyScenario.cs` for the starting
kit (**only** in scenarios no golden builds on — the goldens use `ScenarioDef.Bare`), tests under
`Assets/Odyssey/Tests/Sim/`.

**Division with lane A, which the plan's table left loose:** the stun is **rolled and applied by
lane A** (`MeleeRules.Resolve`, `CombatSystem`) from the numbers in the `Armament`; lane D's part is
that the armament of a held bat or crowbar **carries** its stun, which a test of `ArmamentOf` pins.
Weapon spawning from the debug menu needs no simulation work — `GiveResource` already places an item
of any def — so the Spawn rows are lane C's.

**Must not touch:** `MeleeRules.cs`, `CombatSystem.cs`, the attack, flee and downed drivers,
`CombatThinkNodes.cs`, `JobSystem.Attack.cs`, `Draft.cs`; `Presentation/`, `Hud/`, `Sim.Contracts/`;
`Pawn.cs`, `PawnRegistry.cs` (the `odyssey.pawn.weapon` aspect is already published from
`EquippedItem`), `PawnContent.cs`, `JobSystem.cs`, `ColonyComposition.cs`, `CombatSection.cs`,
any Def XML (the weapons' numbers are frozen; propose changes in the hand-over), `Golden.cs`.

**Tests to add (fast tier):** `WeaponRulesTests` (bare hands, teeth, and each of the four weapons'
armament once held, a blunt one carrying its stun); `EquipTests` (the order walks, lifts, holds;
a second weapon puts the first down; refused for a forbidden item, a non-weapon and an animal; the
save round trip with a weapon in the hand); `WeaponDropTests` (dies → the weapon on the ground at
the corpse; downed → still held); `StartingKitTests` (the kit's weapons in the playtest scenario
and **none in `ScenarioDef.Bare`**). **Goldens unchanged.**

## Phase 4 lanes, named now so their files are known

- **C4 rescue:** `Combat/RescueJobDriver.cs`, `Combat/RescueWorkGiver.cs`, `JobSystem.Rescue.cs`,
  and a listener appended in `CombatListeners.cs` if it needs one. The carried patient is
  `Pawn.CarriedBy` and `PawnFlags.Carried`; the cradle comes from `CarryPose`.
- **C5 friendly fire:** a listener in `CombatListeners.cs` (after C4's), two thoughts in
  `Thoughts.xml` and `ThoughtIndex` — a content change, so the fingerprint moves once, in that lane.
  **Goldens asserted unchanged.**
- **C6 buildings:** `EdificeDamage` is the store; the attack driver's building mode (a branch lane A
  leaves) and demolishing at nought with no refund. **Goldens asserted unchanged.**

## The integrator (Phase 3)

Merge A, D, C, then B — simulation first, so each merge adds what the next one reads. Regenerate
the wiki and `Registry.g.cs` rather than resolving their conflicts. Apply the lanes' proposed Def
changes and the fingerprints once. Run EditMode, then PlayMode **alone on the machine** (check
`Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` for batch runs first), the frame budget
with a fight in view, and the player-build smoke test (`Build/Win64/Odyssey.exe -odyssey-newgame`),
which C2 needs because it ships clips.
