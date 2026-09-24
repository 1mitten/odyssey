# Combat — the plan

**Approved by the owner 2026-09-23** after an interview of five rounds (every answer is in the
decision table of `docs/design/33-combat.md` §1). **C1 merged as PR #176** (branch
`claude/combat-mvp`), which also carried wildlife (#169) to `main`. **C2 and C3 are built and
integrated on `claude/combat-c2`** (worktree `D:\code\odyssey-combat`), awaiting the owner's
playtest; the hand-over is `docs/plans/combat-c2-handover.md`. Temperature (#164) is independent:
combat adds save sections, not a format bump.

## Units

| Unit | What | Checkpoint | Goldens | State |
|---|---|---|---|---|
| C0 | Research `a-10-melee-combat.md`, pack inventory `synty-sword-combat.md`, design 33, this plan | — | — | **done** |
| C1 | Draft and move: drafted state, `Job_DraftHold`, `Job_Goto`, `SetDrafted`, `OrderMove`, the Drafted think node, the four-hour release, the job-count load guard. T, the pane's button, `OrderModel`, `SelectionPresenter.Order`, the diamond and the order line. After the playtest: the run (§2h), the deeper red (§2g), the blade sound (§2i) | **played 2026-09-23 — works** | moved once: two job defs | **PR #176** |
| C2 | Health and melee against a marauder: species hit points, revenge and natural attack, `CombatDef`, live Melee skill, `PawnKind_Marauder`, attack, flee and downed jobs, `OrderAttack`, adjacent auto-attack, `CombatSystem`, corpses; health tab, corpse pane, spawn marauder, feedback, the clip layer and its fallback | **▶ playtest** | moved once, in the contracts step; **none since**, probe-diffed | **built 2026-09-23 (lanes A, B, C), integrated on `claude/combat-c2` — awaiting playtest** |
| C3 | Weapons: bat, crowbar, machete, sci-fi blade; the equip job and order; stun; starting kit; held prop | **▶ playtest** | moved once, in the contracts step; **none since** | **built 2026-09-23 (lane D; the held prop at the integration) — awaiting playtest, with C2** |
| CB | Blood: spurts, a mark per hit, pools under the fallen, a fade by the tick (design 33 §10) | **▶ playtest** | **none** — presentation only | **built 2026-09-24, PR #182** |
| C4 | Rescue and healing in bed: carried in the arms to her own or the nearest free bed, in it until whole, *No bed for the wounded* (design 33 §11) | **▶ playtest** | **none since the contracts step** | **built 2026-09-24, `claude/combat-rescue`** |
| C5 | Friendly fire and its mood: the two memories (design 33 §12); Ctrl-attack and self-defence were C2's, now tested end to end | **▶ playtest** | **none**, asserted; content fingerprint moved once | **built 2026-09-24 on `claude/combat-friendly-fire`** (fast and Long tiers; no Unity needed, nothing drawn) |
| C6 | Buildings as targets: `EdificeDamage` filled, the attack driver's building mode, `OrderAttack` with `B = 0`, demolition through `ConstructionGrid.Demolish` with no refund, the right-click (`CombatOrders.RouteBuilding`), hit points for the campfire and power's three | **▶ playtest** | **none, asserted** (building fingerprint moved once) | **built 2026-09-24, `claude/combat-buildings`, design 33 §13 — awaiting the integrator's Unity run and the owner's playtest** |
| C7 | Ten-day gate with and without hostiles, benchmark rows, records, wiki republish | — | — | — |

## Running C2–C7 with several agents

**Written 2026-09-23 at the owner's request** (*"prepare plan with we can multi-agent the rest of
tasks"*). It is a plan, not a run: a workflow is started only when the owner says so.

### What decides the shape

These are facts measured in this session, not preferences. Each one is a reason something is
single-threaded:

1. **The shared tables are append-only save contracts.** Several things are numbered by the order
   they are listed, and the list *is* the save format:
   - job handles and `ByName` lists;
   - item, skill, work-type and kind handles;
   - `IntentKind`;
   - the `CombatSection` layout.

   Two agents appending a job each, on two branches, each claim handle 14. Every merge conflicts,
   and every conflict is a save-contract decision. So one agent claims every handle the whole line
   needs, **once, first**.
2. **Goldens move on any new hashed row.** Job counters, skill and work-priority arrays and item
   allow-lists all move all six goldens. Moving them once, measured with the colony probe, is a
   morning. Moving them on four branches that then conflict is four re-bakes and a fifth at the
   merge. So the contracts step re-bakes, and **every lane after it must leave the goldens alone**.
   That is achievable because combat state is hashed only while set, and nothing in a golden window
   fights.
3. **One Unity batch run at a time.** This machine ran out of memory with three Unity processes up,
   and killed two of this session's background shells. PlayMode timing tests also fail when runs
   overlap (`CLAUDE.md`, "Tests and gates"). So lanes prove themselves in the **fast tier**, and a
   single integrator owns the Unity tiers.
4. **The spine files are contended.** `JobSystem.cs`, `Pawn.cs`, `PawnRegistry.cs`,
   `Sim.Contracts/Views.cs`, `OdysseyBootstrap.cs` and `PawnFigureDirector*.cs` are touched by
   almost every unit. A lane gets **its own new files**, plus named seams in the spine that the
   contracts step cuts for it. Following `docs/process.md` §2, fan out on leaves and single-thread
   the spine.
5. **The owner playtests at C2 and C3.** Parallelism shortens the build, not the checkpoints. C2 and
   C3 can be handed over together, or C2 first with C3 behind it.

### The plan: four phases, at most four agents at once

**Phase 1 — contracts (one agent, sequential, about a unit's work).** **Done 2026-09-23 on
`claude/combat-c2`**: what each seam is for is `docs/design/33-combat.md` §5, and the lane-by-lane
brief — files owned, seams to fill, tests to add, files not to touch — is
`docs/plans/combat-contracts.md`. Two refinements of the table below came out of cutting the seams:
lane D's "stun from blunt" is rolled and applied by lane A from the armament lane D supplies, and the
debug Spawn rows (the marauder and the four weapons) are lane C's, because they are interface and
`GiveResource` already places any item. **A seam review the same day** read the briefs against the
code before any lane started and moved eleven things into the spine or into one written rule
(design 33 §5j; the journal says why) — so the lanes start from the head of `claude/combat-c2`
after it, not from the first contracts commit. This is the only phase that edits the spine for
everyone.
It delivers:

- **Every handle the line needs, appended once.** No behaviour yet; stub drivers return `Failed`.
  - Jobs: `AttackMelee`, `Flee`, `Downed`, `Equip`, `Rescue`.
  - `Skill_Melee`, live. `Work_Rescue`. `PawnKind_Marauder`.
  - Four weapon items, with our own names in `icon-keys.csv`.
  - Intents: `OrderAttack`, `OrderEquip`, `OrderRescue`.
- **Defs and their fields:**
  - `SpeciesDef` gains `healthPoints`, `deathAtPerMille`, `revengePerMille`, `naturalAttack`, and
    `meleeSkill` for animals.
  - `PawnKindDef` gains `faction`.
  - `ItemDef` gains a `weapon` block.
  - `BuildingDef` gains `maxHitPoints`.
  - A new `CombatDef`: the hit, dodge and heal curves, fists, and the chase and retaliation windows.
  - Every value from the design 33 §1 table, in XML.
- **Views and state:**
  - `PawnView` gains its trailing `Flags` byte, and the six "kind ≠ 0 means animal" checks move to
    it.
  - A `CombatEventView` channel (a report: never saved, never hashed).
  - `PawnGesture.Strike`.
  - The `CombatSection` layout 2 fields: HP, downed, swing tick, stun, retaliation and equipped.
    Saved, and hashed only while set.
- **Named seams for the lanes to fill:**
  - `ICombatRules` (hit, dodge, damage, stun), `CombatSystem` (empty `Tick`), `CorpseRegistry`.
  - `DamageApplied`, `Downed` and `Died` hooks.
  - `EdificeDamage`, empty.
- **Records:** the wiki, the label registry, the fingerprint, and **one golden re-bake with a
  colony-probe diff**.

Gate for the phase:
- fast, Long, EditMode and PlayMode all green;
- the probe diffs clean.

Every later lane branches from this commit.

**Phase 2 — C2 and C3 in parallel (four lanes, each in its own worktree with the Synty junction).**
Each lane owns the files it creates and touches the spine only through Phase 1's seams.

| Lane | Owns | Proves itself with | Must not touch |
|---|---|---|---|
| **A — the fight (sim)** | `CombatSystem`, `CombatRules`, the attack, flee and downed drivers, hostile and animal think nodes, corpses, healing, adjacent auto-attack, drafted retaliation | fast tier: `CombatMathTests`, `AttackDriverTests`, `DownedDeathTests`, `HostileTests`, `AnimalRevengeTests`; the mid-swing save round trip; a Long "marauder a day for ten days"; a 20-against-20 row in `TickBenchmarkTests`; **goldens unchanged** | Presentation, Hud |
| **B — the fight (drawn)** | `PawnFigureDirector.Combat.cs`, `CombatPose.cs`, the Sword Combat clip rows in `PlayScene.cs`, HP bars, floating text, the hostile marker, combat sounds, `CorpseDirector` | fed a scripted `CombatEventView` stream, so it does not wait for lane A. EditMode fallback tests. **Never runs Unity**: it hands its branch to the integrator | Sim |
| **C — the interface** | `Hud/CombatFeedbackModel.cs`, the Health tab, the corpse pane, the Spawn-marauder row, `OrderModel` routing (right-click an animal or hostile to attack, Ctrl for a colonist, a downed colonist to rescue, a weapon to equip), roster and Work-tab filters by flags | Hud fast tier; the registry and font tests | Sim behaviour, Presentation |
| **D — weapons (C3 sim)** | the equip driver and `OrderEquip`, the weapon lookup `ICombatRules` reads, stun from blunt, drop on death, the starting kit, the debug spawn | fast tier; **goldens unchanged** for colonies with no weapon picked up, which the starting kit must respect (design 33 §4) | Presentation, Hud |

Lanes A and D both implement parts of `ICombatRules`. Phase 1 splits it into two interfaces, one
per lane, so the two never edit one file.

**Phase 3 — integrate C2 and C3 (one agent, sequential).** **Done 2026-09-23** on
`claude/combat-c2`: design 33 §6E is what was merged, wired, measured and decided.
- Merge A, D, C, then B, in that order: simulation first, so each merge adds what the next one reads.
- Run the Unity tiers once, alone on the machine.
- Take the frame-budget number (`FrameTimeTests` with a fight in view) and the player-build smoke
  test, which C2 needs because it ships clips.
- Write the handover. **▶ Owner playtest, checkpoints 2 and 3 together.**

**Phase 4 — C4, C5 and C6 in parallel, then C7 (three lanes, then one agent).**
- **Lanes:**
  - **C4 rescue:** the driver and giver fill Phase 1's stubs; the carry cradle comes from `CarryPose`.
  - **C5 friendly fire:** Ctrl-attack, self-defence and two thoughts. **Goldens asserted unchanged.**
  - **C6 buildings:** fill `EdificeDamage`, add the attack driver's building mode, demolish at zero.
    The campfire and power's conduit, generator and heater arrived from `main` with no
    `maxHitPoints` (0); give them one here, not before — nothing strikes a building until C6.
    **Goldens asserted unchanged.**
- **Then C7, one agent:**
  - the ten-day gate with and without hostiles;
  - benchmark rows;
  - `CLAUDE.md`, the journal, the playtest queue;
  - republishing the wiki artifact.

### Budget

- **Agents:**
  - Phase 1: one.
  - Phase 2: four, in parallel.
  - Phase 3: one.
  - Phase 4: three, then one.
  - That is **ten agent-units**, and never more than four running at once, which is inside the
    session's ten-agent guideline.
- **Unity:** Phase 1 runs it once and Phase 3 runs it once; the rest is fast tier. **Owner time:**
  one playtest after Phase 3, one after Phase 4.
- **What parallelism buys:** Phase 2 runs about four units of work in the wall-clock time of one.
- **What it costs:** Phase 1 has to be right about every seam before any lane starts. A seam
  missed there becomes a spine edit in a lane, and the merge conflict comes back. So Phase 1's
  design section in design 33 gets the owner's eye before Phase 2 starts.

### Every lane agent is told

- Its own worktree is `D:\code\odyssey-<lane>`, from the Phase 1 commit. **Junction `Assets/Synty`
  first** (`docs/lessons.md`), and never delete a worktree without unlinking it.
- Fast tier only. **Do not run Unity**: the integrator does. Push after every commit, because
  checkouts here get reset.
- The goldens must not move. If one does, stop and report: that is a missed seam, not a re-bake.
- The files it owns and the files it must not touch (the table above). New files get `.meta`
  files with fresh GUIDs.
- British English in docs. Every per-tick loop states what it scales with (`docs/process.md` §3).
  Each claim gets a negative control that has been seen to fail.

## Blocked on the owner

- **The C2/C3 playtest** — checkpoints 2 and 3 together, on `claude/combat-c2`
  (`docs/plans/combat-c2-handover.md`), and then its PR. Phase 4 (C4–C6) waits on the verdict.
- **The combat sounds.** Five ids are named (`SoundIds.CombatSwing`, `CombatHit`, `CombatMiss`,
  `CombatDown`, `CombatDeath`) with no clips, so a fight is silent until clips are supplied.
- **The draft sound's licence**: `draft.wav` comes from a Pixabay recording (Dragon Studio). To
  confirm, per design 33 §2i.
