# 43 — Health: six regions, afflictions, bleeding, tending, falls

**Designed and built 2026-09-25** — H1–H6 of `docs/plans/health.md` on
`claude/relaxed-heisenberg-zxy63b` (PR 1mitten/odyssey#213); §14 is what the build changed and
measured, and it wins where it and §1–§13 disagree; §15, the merge with medical supplies (design
37), wins over both. The interview is `docs/research/health-interview.md`
(four answers, every recommendation taken); the research is `docs/research/a-02-health.md`; the
units are `docs/plans/health.md`; the brief that Claude Design draws the tab from is
`docs/reference/mockups/health-tab-brief.md`. Health is **M6** in the brief
(`docs/brief.md:207`); design 17 says M4 in three places and this document is the reconciliation:
the rate seam it left open is filled here, whichever milestone the calendar says.

Every number is either cited to a line of `a-02` or marked **INVENTED**, which means it is ours,
defensible, and to be tuned from play. The anchor the invented ones are calibrated against is
`a-02`'s: a thin roof falling on you is 15, pain shock arrives at about 64 points of live injury,
150 points of damage is death. Those three are RimWorld's and they are what makes a fall, a fight
and a wound tell the same story.

## 1. What exists, and what this changes

Combat (design 33) pulled a **single hit-point pool** forward: `HpMilli` per pawn in thousandths,
`HpMaxMilli` from `SpeciesDef.healthPoints` (person 100, hog 60, rat 15), **downed at 0, dead at
−50 %**, one place that loses it (`CombatSystem.ApplySwing`), healing only in a bed at
`bedHealPerDay` 20, rescue to a bed, a corpse registry, and the `DamageApplied` / `Downed` /
`Died` hooks. Beside it stand two severity bars, starvation and temperature, that step
`ConditionPerMille` down and never hurt anyone; a rate seam with a slot called `health` that
nothing fills (design 17 §4a, §4e); three fall sites that move a pawn and hand out a memory
(`Falling.PawnsOutOf`); a Health tab that reads "73 / 100"; and a registry that has named twenty
parts, eight conditions, the doctor, the medkit and four alerts since the day it was written.

**Read the pool as what it already is: total injury.** A pool of 100 that kills at −50 is
`a-02`'s "150 points of damage is death" (`a-02:58`), and downed at 0 is a coarse stand-in for
pain shock. So nothing about the pool moves — not its save, not its hash, not its aspects, not
the roster bar or the bar over a head. What this design adds is *where* the damage is, what it
does to the body, and how it stops.

| Decision | Chosen | Alternatives, and why not |
|---|---|---|
| Fidelity | **Six regions**: head, torso, left arm, right arm, left leg, right leg | A pool with a condition list: no region can slow anyone specifically, and the tab is a list of words. The full tree (~40 parts, 11 capacities, `a-02` §1): needs a tree control the HUD has not got, a window rather than a tab, and 28 icons of which half have no art. The six-region Def shape grows to the tree without a save break (§9). |
| Scope | Injuries on regions, three capacities, **bleeding and tending**, **fall damage** | Cold, heat and hunger turning lethal through the same ledger: design 28 §8's promise, deferred by the owner — the ledger is shaped so they join (§2). Infection and disease: the most work and the most new interface, and `a-02:159` could not determine tend duration. |
| Interface | The Health tab, every state, from a Claude Design brief | Alerts (`ui.alert.injured`, `nomedicine`), right-click Tend and Rescue rows, a docked Medical window: recorded as later units (§11, plan H6). |
| This unit | Documents only | Code follows the mockups and the plan's approval, per the working agreement. |

## 2. The shape: a ledger of afflictions on a body of six regions

**A body.** A `BodyDef` names its regions with a hit-point value and a coverage weight, and
`SpeciesDef` points at one; a species with no body (every animal, in v1) keeps the pool alone.
The person's body:

| Region | HP | Coverage (melee) | Coverage (a fall) | Feeds |
|---|---|---|---|---|
| Head | 25 (`a-02:15`) | 10 | 0 | consciousness |
| Torso | 40 (`a-02:16`) | 40 | 30 | — (vital) |
| Left arm, right arm | 30 each (`a-02:17`) | 12.5 each | 0 | manipulation |
| Left leg, right leg | 30 each (`a-02:18`) | 12.5 each | 35 each | moving |

The coverage columns are **INVENTED** (`a-02:161`: the reference's coverage shares are not
public). A melee hit picks a region by the first column from the same deterministic roll the
resolver already draws; a fall picks from the second, the mirror of the reference's top-facing
roof rule (`a-02:101`).

**An affliction** is one record: `region`, `kind`, `severityMilli` (points taken, in
thousandths), `bleeding`, `tended`, `tendQualityPerMille`, `tick`. Three kinds in v1, each an
`AfflictionDef`:

| Kind | From | Bleeds | Registry |
|---|---|---|---|
| Wound | a Sharp hit | yes | `ui.health.wound` ("Wound", "Fresh injury") |
| Bruise | a Blunt hit, a fall of one layer | no | *needs a key* (`ui.health.bruise`) |
| Fracture | the largest hit of a fall of two or more layers | no | `ui.health.fracture` |

Scar, burn, missing part, implant and anaesthetic stay registered and unwired. **The word is
ours** — not the reference's — and the Def carries a `kind` field from day one because
starvation and temperature severity are the two rows that will join this ledger when their
lethality lands: an affliction with no region is how a condition is written, and the one list
is what the tab and the save read.

**Bounded, by construction.** Afflictions of the same kind on the same region **merge**: a
second cut on a wounded leg adds its points to the one record and re-opens it (untended,
bleeding). So a person carries at most three records per region, **eighteen in all**, whatever
the fight was; the save, the hash, the snapshot and the tab all read a bounded list, and the
region-clicked state of the tab (§10) has at most three rows to show.

**Overflow.** Points past a limb's HP pass to the torso (`a-02:20`); points past the torso's or
the head's stay in the pool, which is what makes a five-layer fall lethal without a special
rule (`a-02:101`). A region at zero **contributes nothing to its capacity and is never lost** in
v1 (no missing part); **head or torso at zero is death** (`a-02:55`).

**One invariant, one owner.** For a person with a body, `HpMilli == HpMaxMilli − Σ severity`
always; `ApplyDamage` and `Heal` write both sides in the same call, and a test walks the whole
combat gate asserting it. The pool is not derived from the ledger, because animals have no ledger
and every existing reader of the pool stays exactly as it is; it is *checked* against it.

## 3. Capacities, pain and the downed line

Three capacities, per mille, derived on the needs cadence and never saved:

- **Pain** = live injury points × 12.5 ‰ per point (`a-02:30`: 1.25 % per HP), over every
  affliction on the body.
- **Consciousness** = 1000 × (1 − clamp((pain − 100) × 4/9, 0, 400) / 1000) × the blood-loss
  factor (§4) — `a-02:24`'s shape with the breathing and filtration terms dropped, because there
  are no lungs and no kidneys.
- **Moving** = the mean of the two legs' remaining fractions × consciousness / 1000.
- **Manipulation** = the mean of the two arms' remaining fractions × consciousness / 1000.

**Downed** when the pool is at or below zero (today's rule) **or** consciousness is below 300
**or** moving is at or below 150 (`a-02:47-50`), and the pain-shock rule is the second of those
made explicit: **`painShockPerMille` 800** (`a-02:30`, `a-02:49`) downs a colonist at 64 points
of live injury. It is a Def field, and 1001 switches it off.

**Recovery.** Up when none of the three holds *and* the pool is whole, for a colonist (today's
"until whole", design 33 §11c); an animal keeps `downedRecoverAtPerMille`.

**The flag on this section.** Today a colonist goes down at 100 points; with pain shock she goes
down at about 64, so a fight ends sooner and a raid downs more. `BanditSoakTests.TheGateWithRaids`
reports downs and deaths per seed; **run it before and after H1 and put both numbers here**.
The owner's open question — whether four armed colonists should lose to three bandits at the
invented numbers (combat report §5) — is the same question, and this is the first lever on it.

## 4. Bleeding and blood

- An untended **Wound** bleeds its severity points × **60 ‰ of the body's blood per day**
  (INVENTED: `a-02:160` could not determine the reference's conversion). A 10-point cut alone
  reaches 1000 ‰ in 40 hours; three cuts totalling 30 points, in 13. Bruises and fractures never
  bleed.
- **Blood loss** is one per-mille scalar on the pawn, summed from every bleeding affliction on
  the needs cadence. Its stages are `a-02:35`'s: **≥ 150** consciousness × 0.9, **≥ 300** × 0.8,
  **≥ 450** × 0.6, **≥ 600** consciousness capped at 100 (downed by §3), **1000 dead** —
  through `Kill`, so it is mourned.
- **Any tend stops every bleed at once, whatever its quality** (`a-02:36`) — the single most
  important fact in this design, because it means a skill-0 colonist with bare hands saves a
  life, and the tab's one urgent number is *how long until somebody must*.
- Once nothing bleeds, blood returns at **333 ‰ a day** (`a-02:37`).
- **Published as hours**: the pane wants "14 h to death" or nothing, never a rate. The
  simulation knows the rate and the day length, so it publishes the hours
  (`docs/process.md` §3: presentation never derives a number the simulation knows).

## 5. Tending, the doctor and the medkit

**Work.** `Work_Doctor` is a **new work type** and lights the `ui.work.doctor` column the Work
tab already draws dim among its twenty-two; Rescue keeps its own column (`ui.work.rescue`) and
its Emergency status. (The plan had proposed relabelling Rescue to avoid a 23rd column; the code
says the column is already there.) A work handle is an append-only save contract
(`docs/plans/combat.md`, "the shared tables"): one agent claims it, once, first.

**The giver.** A patient is a colonist with any untended affliction; a bleeding patient outranks
one who is not; nearest first. **No self-tend in v1** (the reference allows it at × 0.7,
`a-02:42`; a lone colonist bleeding out is legible and is a scenario decision to make at the
keyboard, not by default). The giver has `ctx.Reachable` like every other (design 22's lesson).

**The job.** `Job_Tend`: fetch one **medkit** (`ui.res.medkit`, "Field tending. Consumed per
treatment") from a store if one is reachable — the builder's take-material path, not a new one
— walk to the patient where she is (in a bed or on the ground; a tend is done in place,
`a-02:52`), work **600 ticks** (INVENTED) scaled by tend speed (**40 % at level 0, 100 % at 10,
160 % at 20**, `a-02:42`, linear between), then mark every untended affliction tended at one
quality and stop every bleed. `ui.status.tending` is the line; `ui.command.tend` is the
right-click row, **not in this unit** (§11).

**Quality**, per mille = skill × potency, clamped by what was used (`a-02:42`):

| Medicine skill | 0 | 5 | 10 | 15 | 20 |
|---|---|---|---|---|---|
| skill factor | 200 | 700 | 1100 | 1350 | 1550 |

× potency **300 bare-handed, 1000 with a medkit**; then clamped to **700 without a medkit, 1000
with one**. The random 0.75–1.25 is dropped: a deterministic tend from a deterministic roll
stream is the same thing and the reference's spread bought nothing legible.

**Medicine skill goes live** on the SK5 pattern: `Skill_Medicine` in `Skills.xml`, `SkillIndex`
6, the Skills-tab row (today "wounds heal in bed, untreated") switched on, `Job_Tend` training
it. The Melee skill arrived this way with C2 and **no save format moved** (design 33 line
1076); Medicine is the same path.

**A tend does not expire in v1.** `a-02:159` could not determine the reference's duration, and an
expiring tend adds a clock, a re-tend job and a row to the tab for a fact nobody asked for.
Recorded as a hook: `tendedUntilTick` is a field the record can grow.

## 6. Healing

| Where | Points a day | Source |
|---|---|---|
| Out of bed, untended | 0 | today's rule (design 33 §1), kept |
| In a bed | 20 | `bedHealPerDay`, today's, INVENTED |
| Tended, anywhere | **+4 at 0 % quality up to +12 at 100 %** | `a-02:41` |

The two add, so a tended colonist in a bed heals at 24–32 a day and a tended one at work at 4–12,
which is the reason to tend somebody who can still stand. Healing runs where it runs today
(`CombatSystem.Heal`, the needs cadence, exact over a day), spends on the **most severe
affliction first**, and takes the same milli off the pool in the same call (§2's invariant). A
record at zero severity is removed. The choice that a hurt colonist *out of bed and untended*
heals nothing is the owner's to revisit (§12); the reference gives 8 a day anywhere.

## 7. Fall damage

`fallDamage(n) = round(15 × n^1.5)` blunt for a fall of `n` layers (`a-02:91`), the table
`a-02:93-99`:

| Layers | Points | Pain | On an unhurt colonist |
|---|---|---|---|
| 1 (3 m) | 15 | 190 ‰ | a bruise; walks away |
| 2 (6 m) | 42 | 525 ‰ | a fracture, badly hurt, standing |
| 3 (9 m) | 78 | 975 ‰ | **downed** by pain shock |
| 4 (12 m) | 120 | — | downed; the pool at −20 |
| 5 (15 m) | 168 | — | past 150: **dead** |

Split into **two to four hits** by the fall coverage column (§2) with **± 20 %** spread
(`a-02:101`, INVENTED weights); a fall of two or more layers makes its largest hit a fracture.
`FallDamageBase` 15 and `FallDamageExponent` 1.5 are Def fields (`a-02:103`). A cascade counts
**each landing** by its own uninterrupted height, never the total (`a-02:105`). `Thought_Fell`
goes: the injury is the memory.

**Wired through one owner.** `ApplyDamage(pawn, region, milli, kind, attacker: null, tick)` is
extracted from `ApplySwing` and both call it; the three fall sites (`SupportSystem.cs:133`,
`MineJob.cs:400`, `ConstructionGrid.cs:1483`) call it through `Falling.PawnsOutOf`, so a fatal
fall raises `Died`, leaves a corpse and is mourned exactly as a blow is (design 33 line 2816:
"a later way to die must raise `Died` or it will not be mourned"). Two owners of damage is
`docs/bug-patterns.md`'s first pattern, and this is where it would have started.

## 8. Animals

A species with no body keeps the pool alone: no regions, no afflictions, no bleeding, no tending
(`SpeciesDef.person == false` in v1; `body` is null). An animal is hurt, downed and dead by the
rules it has today. The tab draws the pool and nothing else for one, which the brief says in as
many words.

## 9. Rates, save, hash, snapshot

**Rates.** `MoveRatePerMille` × moving / 1000; `WorkRatePerMille` × manipulation / 1000 — at the
`health` slot design 17 §4a fixes, and nothing else in either changes (§4e). `Capable[w]` stays
1. The condition floor of 700 stays: its replacement by a threshold is the lethality unit.

**Save.** A new keyed section **`odyssey.health`**: for every person with any affliction or any
blood loss — the pawn id, blood loss, the record count and the records. Read **after**
`CombatSection` (the pool) by its place in `ColonyWorld.SaveComponents`. A keyed section needs
**no format bump** (`SaveFormat.cs`, the note at 8); the pawn record's layout does not move.

**Hash.** Under `HasHealthState` (any record, any blood loss), the way `HasCombatState` gates
combat: every record's five numbers and the blood loss, **hashed whole**, never divided back
(the WS review's lesson). A colony nobody has hurt hashes exactly as before, so the meadow and
city goldens should come out **identical** at H1 and H2 and `GoldenColonyProbe` says so; **H3
moves every golden** (a work type and a skill each add a hashed array), measured to be their
counters alone, as power's three job defs were.

**Snapshot.** Aspects, sparse — **only while hurt**, so the 57-row budget for a healthy colonist
is untouched (design 31): `odyssey.pawn.health.region.0` … `.5` (remaining ‰), `.blood` (‰
lost), `.bleed.hours` (0 when nothing bleeds), `.pain`, `.consciousness`, `.moving`,
`.manipulation`, `.afflictions` (count), `.tended` (count). Fourteen rows. The records
themselves go out as `AfflictionView` rows (pawn, region, kind, severity, bleeding, tended,
quality), published like `CorpseView`, at most eighteen a hurt person. Names are minted once in
`HealthAspects` with the Hud's copy in `HealthAspectNames`, each side checked by a test, the
combat pattern.

**Cost.** One pass on the needs cadence, spread by pawn id: bleeding, blood recovery, healing and
the three capacities, **scaling with the hurt pawns and their records (≤ 18 each)**, never with
the colony and never with the board. A healthy colony pays one flag test per pawn per interval.

## 10. The tab, and why the brief asks what it asks

The pane's tab body is **157 px** because the Skills tab is seven 19-px rows with 4-px gaps in
two 256-px columns (`HudLayout.InspectTabBody`, derived); `14-hud-layout.md` §521 says the
disabled tabs "will move this number when they arrive, once". A Health tab that **fits the same
grid** moves no constant, no pane test and no other tab, and reads as the Skills tab's sibling.
So the brief hands Claude Design that grid and this content, and lets it arrange but not add:

| Column | Rows |
|---|---|
| Left, seven | the six regions (icon, name, a 72 × 6 bar of what is left, a mark for bleeding or tended), then **Pain** |
| Right, seven | **Health** (the pool, "73 / 100" and its bar, as today), **Consciousness**, **Moving**, **Manipulation**, **Blood loss** (a bar), **Bleeding** ("14 h to death", or nothing), **Tended** ("2 of 3") |

The Condition word (Unhurt, Hurt, Stunned, Downed) leaves the tab for the header's state line
beside the job, where the roster already says it. **The weapon row leaves the tab**: design 33
§9d kept it "until the Gear tab ships"; H5 moves it to the header line and the Gear tab takes
it when it exists. **Clicking a region** swaps the right column for that region's afflictions —
at most three rows (§2): kind, points, bleeding, tended and quality — which is the whole reason
the merge rule exists, and is a second state rather than a tooltip, because nothing has verified
that UI Toolkit's tooltip string draws in the player.

Marks are **drawn glyphs**, never font characters (`docs/bug-patterns.md` P13; neither shipped
font has a tick or a drop). The glyph set has a hollow medical cross (`CategoryMedicine`),
`Check`, `Cross`, `AlertTriangle` and `Flame`, and no blood drop, bandage or prone figure; the
brief lets the designer propose one as an SVG path, which `SvgPath` / `PathGlyph` can stroke.

**If the designer needs more than 157 px they must say the number** in the file's top comment,
because it becomes `InspectTabBody` and every tab moves with it; that is the honest time to pay
for it, and it is paid once.

States the brief asks for: unhurt; two cuts on one leg, untended, bleeding; tended and healing in
a bed; downed by pain with a leg at zero; a region clicked; a hog (pool only).

## 11. Not in this unit, recorded

- **Alerts**: `ui.alert.injured` (anyone untended), `ui.alert.nomedicine` (a tend done bare-handed
  with no medkit in any store), each one row, Warning; `AlertModel`'s add-an-alert pattern.
  Plan H6.
- **Right-click Tend and Rescue rows** in the forced-order menu: `ContextMenuModel`'s comment
  reserves both and calls moving Rescue "a change to decide on at the keyboard". Plan H6, owner's
  call on Rescue.
- **Debug rows**: Hurt (a 20-point wound on a random region), Heal, Kill on the Spawn tab.
  Design 18 §"Kill, damage or heal" declined them because "`Pawn` has no health", which is no
  longer true; with `ApplyDamage` and `Kill` as the owners, a debug kill is a real death. Plan H6.
- **A Medical window** (a docked table of the hurt: bleed, tended, bed), only if the full tree
  ever comes.
- **Lethal cold, heat and hunger** through the ledger (design 28 §8's promise), replacing the 700
  floor with a threshold (design 17 §4c).
- **Infection**, disease, scars, surgery, prosthetics, missing parts, an expiring tend, self-tend,
  medicine tiers (`ui.res.medicine`).
- **A crawl**: a downed colonist with manipulation who moves toward a bed (`a-02:52`, `a-02:115`).

## 12. Open, the owner's

- Pain shock on or off, once the soak numbers (§3) are beside each other.
- Whether a hurt colonist out of bed and untended heals at all (the reference: 8 a day).
- Whether Rescue moves into the right-click menu (one click becomes two).
- Whether the tab's grid, when it comes back from Claude Design, needs the 157 px moved.

## 13. What not to undo by tidying

- **The pool is not derived from the ledger.** It looks like a duplicate and it is a check; animals
  have no ledger and every reader of `HpMilli` stays untouched.
- **Afflictions merge by (region, kind).** Unmerging them to "keep every wound" unbounds the save,
  the hash, the snapshot and the tab.
- **Fourteen aspects only while hurt.** Publishing them for everyone is 14 × colonists a tick for
  nothing, the exact shape design 31 measured.
- **Hours, not a rate**, on the bleed aspect.
- **One damage owner.** A second `HpMilli -=` anywhere is the bug pattern this document was
  written to avoid.
- **The tab fits the Skills grid unless the designer says a number.**

## 14. As built, 2026-09-25

H1–H6 are in. The fast tier is 1,520 Sim and 1,103 Hud, green; the Long tier is 48 of 48; the three
content gates pass. **Neither Unity tier has run** — the build was done in a cloud container with no
Unity — so the Presentation half (the Health tab's view and the state line) was type-checked only
against stubs of the UnityEngine types it touches. The owner's first Unity run is the first real
compile of `HudShell.Combat.cs`.

### 14a. What the build changed from §1–§13, and why

| Was designed | Is built | Why |
|---|---|---|
| Head or torso at nought is death (a-02:55) | **A vital region at nought puts consciousness at nought, which downs.** Death stays the pool's line and blood loss | The combat gate asserts that an unordered fight ends in downs, never deaths (design 33 §3, the owner's rule). A head at 25 points would have killed colonists standing up |
| The injury records published as an `InjuryView` | **As aspects**, two a record (`odyssey.pawn.health.injury.{r}.{k}` and `.care`), sparse | The reason skills are aspects: nothing in `Sim.Contracts` had to learn what an injury is, and eighteen records is a bounded list |
| Doctor as the Rescue work type relabelled | **`Work_Doctor`, a new work type** | The Work tab already draws `ui.work.doctor` among its 22; a relabel would have left that column dead |
| Medicine bumps the save format 9 → 10 | **No format bump anywhere in the line** | Skill and priority arrays are saved length-prefixed; the Melee skill arrived the same way (design 33 line 1076). The ledger is its own keyed section |
| `tendSpeedCurve` on `HealthDef` | **The Doctor work type's curve** (400 + 60 a level) | One owner of tend speed; the curve in two places was the first bug pattern waiting |
| `Thought_Fell` replaced by the injury | **Both**: the memory stays and the injury comes on top | A fall is frightening as well as painful, and the four collapse tests that pin the memory keep their meaning |
| Falls wired into all three `Falling` call sites | **A collapse hurts from one layer; any drop of two or more hurts** | A miner stepping into the hole she dug and a deconstructor lowering herself off her own slab are steps. The callers already disagreed about the memory for this reason; they disagree about damage the same way |
| Fall damage for animals unspecified | **Scaled by the species' pool** against a person's hundred | a-02's own "health scale" for other bodies. A rat's one-layer fall is about 2 points, a hog's 9 |
| The tab's rows "arranged by the designer" | **Built to the brief's own content** ahead of the mockups | The owner said "implement it". Every figure fits the Skills tab's 22 px level column: per cents and points as bare numbers, the bleed as "14h", the tends as "2/3"; the full readings are in each row's hover |
| The animal state of the brief (a hog's Health row) | **Unchanged: an animal still has no tabs** | The pane never gave an animal a tab box, and adding one is a layout decision for the mockups |

### 14b. The measurement §3 asked for: the combat gate, before and after

Ten days, three seeds, seven raids of thirteen bandits, the colony drafting and gathering as a
player would (`BanditSoakTests.TheGateWithRaids`); taken on commit 379f49c and on d4b62f2, the same
container.

| Seed | Before: downed (colonists), died, colonists standing at day ten | After: downed (colonists), died, colonists standing at day ten |
|---|---|---|
| 1 | 12 (5), 0, 0 of 5 | 15 (2), 10 bandits, 4 of 5 |
| 2 | 15 (5), 0, 1 of 5 | 16 (3), 12 bandits, 4 of 5 |
| 3 | 17 (5), 0, 0 of 5 | 22 (5), 11 bandits, 3 of 5 |

**The body turned the fight.** Before it, every seed ended with the colony down; after it, the
colony stands on every seed and **no colonist died on any**. Two things did it, both from this
design: pain shock downs a bandit at 64 points instead of 100, and a colonist's machete now cuts,
so a downed bandit nobody tends bleeds out. The bandits that died, died of blood loss. That answers
the owner's open question from the combat report — *whether four armed colonists should lose to
three bandits* — with "not any more", and it is the owner's to judge whether that is the game they
want (§12).

### 14c. Three faults the new fights reached, fixed

- **A downed pawn that died ended `Job_Downed` as a failure**, which the gate's sentinel reads as
  "got up by the wrong door". Nobody had ever died lying down before bleeding. `JobHandle.Downed`
  says the job lasts "until healed, rescued or dead", so a death now ends it as a success.
- **An attacker stunned when its target went down stood on it for the whole stun** (the gate's
  "Fighting at a target already gone", 60–68 ticks). Death already ended every attack at once
  (design 33 §9e); going down now does the same for every attack not ordered to the death
  (`CombatSystem.EndAttacksOnTheDowned`).
- **An interrupted step's landing cell was not a held side**, so a bandit chose the cell a stunned
  colonist was still stepping into and they shared a tile (`FightGuardTests.MixedBrawlsOnManySeeds`
  seed 11). `Melee.SideOf` counts `FinishingStepTo` now; `FightGuardTests.AStepStillLandingIsHeld`.

And one in the new code, found by its own tests: **a doctor chasing a colonist on her feet never
arrived**, because the shared walk toil clears the path whenever the destination changes and the
tend re-aimed at every step. It now chooses a free side with the fight's own `Melee.ChooseSide`
and re-aims only once the patient has left it.

### 14d. Open, the owner's (adds to §12)

- **The fight's balance has moved a long way** (§14b). Pain shock is the lever: `painShockPerMille`
  at 1001 switches it off.
- **A standing patient walks on while she is tended.** The doctor follows and keeps the work done,
  but a patient who waits would be quicker; a "patient" work type (`ui.work.patient`, drawn dim)
  is the reference's answer.
- ~~**No medkit arrives except from the debug menu.**~~ Stale since the merge (§15): the starting kit
  carries six medical supplies and the Medical drop brings four to eight.
- **Rescue runs before tend** at equal priority (the work types' order), so a downed colonist is
  carried to bed and tended there.

## 15. Merged with medical supplies (design 37), 2026-09-25

`main` shipped design 37 (PR #184) while this branch was in review: its own Doctor work type,
Medicine skill, `Item_MedicalSupplies` and a `Job_Treat` that heals the pool +40 under an 80 % cap,
with a patient who goes to bed and a colonist who treats herself when nobody can come. Both designs
are the owner's; neither knew of the other. **Where this section and §5 disagree, this wins.**

### 15a. One doctor, one job, one item

| This branch had | Is now | Why |
|---|---|---|
| `Job_Tend` (23), `TendWorkGiver`, `TendJobDriver` | **Deleted.** `Job_Treat` (main's 23) **ends in the tend**: every injury tended at the treater's quality, every bleed stopped (`Medical.Tend`) | Two doctors' jobs would be two owners of who a doctor walks to. Main's is shipped, has its art, its kneel and its owner-played heal-in-shares; the tend is one call at its end |
| `Item_Medkit` (11), `PawnContent.MedkitItem`, `HealthDef.medkit` | **Main's `Item_MedicalSupplies`**; any item whose Def heals is "supplies" (`Medical.NearestSupplies`). `medkitPotency/CapPerMille` renamed `supplies…` | One box, one shelf row, one starting kit, one Medical drop |
| Work_Doctor at 400 + 60 a level | **Main's 600 + 100** (cutting's and growing's curve) | Main's is shipped and owner-approved; a-02's tend-speed curve was ours to choose |
| Nobody tends herself (§5) | **Main's self-treatment, now a tend as well**: bleeding, or under 60 % | Owner-approved in design 37. It tends at her own Medicine quality; no ×0.7, because the heal is already halved and the work tripled |
| Right-click Tend sends `Job_Tend` | Sends **`Job_Treat`, player-forced**; the patient need not be lying still, so a drafted soldier bleeding where she stands can be reached | The order's handler keeps its name, `OrderTend`, and its label |
| Give medkits | **Give medical supplies** (key unchanged) | |

### 15b. The rules the two designs had to agree on

- **A bleeding colonist is a patient whatever her pool and whatever the cooldown**
  (`Medical.NeedsTreatment`). Design 37 alone leaves a colonist at 88 % alone — a scratch — and a
  12-point cut there bleeds her to death in under a day and a half. She lies down for the doctor (or
  treats herself if nobody can come and supplies can be reached), and stays lying while she bleeds.
- **Inside the cooldown a treatment tends and heals nothing** (`Medical.Heals`), so a second cut
  in a fight is tended without a stack of supplies replacing rest — design 37's reason for the
  cooldown, kept. The cooldown is set only by a treatment that could heal.
- **A treatment heals the ledger as well as the pool** (`Medical.ApplyShare`): the same points
  off the worst injury first, in the same call, so §2's invariant holds on the treatment path too.
  Without it a colonist treated to 70 % kept every injury, and pain shock kept her down.
- **She gets up when the treatment ends, not on a share** (`Medical.GetUpIfAble`, asked from the
  driver's `Cleanup`), and only once the body lets her (`Incapacitated`).
- **The doctor's round still walks only to a patient lying still** (design 37: "a doctor chasing a
  colonist walking about with a scratch is a pursuit"). Bleeding colonists come first, then the
  nearest, as §5 had it.
- **The Injured alert stands for a colonist waiting on a doctor**: Danger while bleeding, Warning
  while downed with an untended injury. A bruise on a colonist on her feet waits for bed rest
  under design 37 and is not news.

### 15c. Two faults in design 37's driver the merge reached, fixed

- **A downed patient got up a third of the way through her own treatment.** Getting up was asked
  on every share, at 15 %; she then stopped lying still, the doctor's job failed, the unit went
  back on the floor unused and no cooldown was set — a free heal every time a doctor started.
  Design 37's own test of the case was `[Ignore]`d for an unrelated rescue fault, so nothing saw
  it. `TendTests.ADownedPatientGetsUpWhenHerTreatmentEndsAndTheUnitIsSpent`.
- **A treatment that reached the cap part-way failed the same way**, because "still needs
  treatment" was asked on every tick of the work. It is asked before the work starts now; once
  started, a treatment runs to the end.
- And one smaller: a doctor displaced mid-treatment went back to the walk toil, which zeroed the
  work done while the shares already paid stayed paid — one unit could heal more than its forty.
  She now walks back inside the treatment toil and keeps the work, §14c's rule.

### 15d. What the merge moved underneath

- **Hash bit 22 was taken twice.** Main gave it to the treatment cooldown; the ledger is **bit 23**,
  the other of the two design 33 left free.
- **The region roll shared a stream with the stream jump.** Both were minted as SHA-256's eleventh
  round constant. `HitRegion` is the twelfth now and `FallSplit` the thirteenth; two purposes on
  one stream would have made a jump's roll decide where a blow landed.
  `HealthTests.EveryPointOfTheBlowIsOnTheLedgerAndThePoolAgrees` then showed it had passed by luck:
  six blows on one tick are one roll by design, and it now ticks between them, as a swing's own
  cooldown always does.
- **No golden moved.** Main's goldens hold unchanged on the merge: every handle the health line
  needed is main's, and the ledger is hashed only while somebody is hurt. The content fingerprint
  moved once, for `Health.xml`.

### 15e. The review of the merged branch, 2026-09-25

A read-through of the health code after the merge found three faults, fixed with tests:

- **A pawn killed by a fall outlived her tick.** Falls happen inside the deferred phase (a
  collapse, a dig, a deconstruction), and `Kill` deferred her removal, which a deferral queued
  from inside that phase runs next tick. For one tick she stood, thought and walked past the death
  line, and a save in between wrote her out alive with the removal lost — a pawn nothing could
  ever kill again. `Kill` now uses `SimWorld.DeferThisTick`, which runs such work in the same phase
  after the batch; everything else deferred from the phase keeps its next-tick rule (both halves
  in `FallTests.AFatalFallInsideTheDeferredPhaseIsGoneTheSameTick`).
- **A death by blood loss left the pool above the death line** for the rest of the tick, so a
  blow later in the same tick could cross it and report the death twice. The pool is set to the
  line as she dies.
- **A five-layer fall was not reliably fatal.** Each hit's ±20 % was drawn independently, so the
  168 points came out between 134 and 201. The spread now moves points between the hits and the
  last takes what is left, so the total is exactly §7's (`FallTests.AFallsHitsAlwaysSumToItsTotal`).

Recorded, not fixed:

- **A save from before health** holds colonists already hurt with no ledger behind the damage; a
  later blow starts a ledger with only the new points, so pain reads low until the pool heals
  whole. Nothing crashes and it heals out. A migration would invent where old wounds were.
- **An untended bruise or fracture above the cap waits for bed rest** (§15b, design 37's rule);
  §5 had made any untended injury a patient. The right-click Tend reaches it.
- **Downed bandits bleed out in unordered fights** (§14b): design 33's "downs, never deaths" now
  holds for colonists only while somebody tends them. The owner's call.
