# 61 — Factions

**Written 2026-09-26**, from the owner's request and a three-round interview the same day
(`docs/research/factions-interview.md`). Research `docs/research/a-13-factions-and-goodwill.md`.
Plan `docs/plans/factions.md`. Branch `claude/lucid-euler-9puelu`, documents only.

> *"We need to explore factions and how this could work - could you explore, plan come up with
> ideas. Ask me questions."* — the owner, 2026-09-26

**Status: plan approved 2026-09-26 (the orcs later). F0 built (§5a); F1 next.** Every number in this document is
a proposal for the owner to tune; none is a measurement.

## 1. What a faction is, and why now

A faction is **a people outside the colony with a memory of how the colony has treated them**. The
reference keeps that memory as one number, goodwill. The number decides three things:
- whether the faction's people fight ours on sight;
- whether its traders and visitors come;
- what it will do for us, and what it will do to us.

Factions were an M7 item, "scoped lightly". They are pulled forward because the game already has
their pieces with nobody to own them:
- **bandits and gunmen** (design 42) who raid (design 55) and thieve (design 33 §17);
- **prisoners** (design 58, on its branch) whose ransom and release are waiting for "somewhere to
  be ransomed to";
- **trade caravans** being built by another agent, whose trader has to belong to someone.

**The owner's rulings** (interview §2–§5):

| Question | Ruling |
|---|---|
| When | **Design now, thin slice now**: F0, F1 and one first interaction (F2); the rest at M7 |
| The racket | **The bandits, as they are now, run a protection racket**: a collector demands a share, and paying buys peace |
| The demand | **A value-weighted share of the stores**, picked by the collector |
| Refusal | **A warning, then a raid**, called off by paying late |
| A new enemy | **The Orc Army, a war horde**: permanently hostile, the late threat tier, fewer but tougher, heavy melee. Art from a Synty pack the owner owns (not yet named) |
| Also on the roster | **The Cartage** (traders) and **the Kindred** (survivors), plus **"something else"** (§3, a candidate) |
| Who owns the model | **This line.** The trade line reads it (§9) |
| The world | **An abstract list of settlements**, each with a distance in days |

## 2. The model

### 2a. `FactionDef`: what a faction is (content)

A new Def under `Assets/Odyssey/Defs/Core/Factions/Factions.xml`, loaded in a fixed defName order
(append only, a save contract, like `PawnKindIndex`).

| Field | Meaning |
|---|---|
| `nameKey` | registry key, `ui.faction.<id>` |
| `category` | `Player` · `Wild` · `Diplomatic` · `Permanent` |
| `startGoodwill` | where goodwill starts in a new world |
| `bandMin`, `bandMax` | the natural band goodwill drifts back towards (§2c) |
| `driftPerDay` | how fast it drifts, in goodwill a day |
| `hostileTo` | list of faction defNames it fights on sight whatever the colony does (§2e) |
| `arrival` | `Edge` · `Road` · `Below`: where its people enter the map (§3, §10) |
| `livery` | the outfit layer its people wear (design 42 §3's mechanism); empty for none |
| `settlementsMin`, `settlementsMax`, `distanceDaysMin`, `distanceDaysMax` | the world list (§7) |
| `groups` | named pawn-group templates: `raid` (a `RaidMixDef`), `collector`, `trader` (the trade line's), `visitor` (later) |

`Colony` and `Wild` are ordinary entries in categories `Player` and `Wild`. **A kind's side becomes
a reference to a FactionDef**: `PawnKindDef.faction` changes from the enum word `Hostile` to
`Faction_Bandits`.

**The enum `Faction { Colony, Wild, Hostile }` is renamed `Stance`**, because the word now names a
people. It stays as the *derived* answer to "how does this pawn stand towards the colony?", and
gains `Neutral`. That is a mechanical rename behind `Allegiance`, with no behaviour change.

### 2b. `Factions`: what the colony's relations are (state)

A new saved section, `odyssey.factions`, appended to `ColonyWorld.SaveComponents` and registered in
`ColonyComposition` on the raids pattern. Per faction, indexed by its Def's position:

| Field | Type | Notes |
|---|---|---|
| `Goodwill` | `sbyte`, −100…+100 | integers only; no float enters the hash |
| `Relation` | `Hostile / Neutral / Ally` | stored, not derived, because of the hysteresis (§2c) |
| `Reasons` | the last **five** changes: day, amount, reason key, subject pawn id | what the tab shows (§8); a ring, so it is bounded |
| `LeaderId` | pawn id or −1 | **reserved**, unset in the slice |
| `Defeated` | bool | reserved |

Plus the **world list** (§7) and the **faction override** (§2d).

**Hashing: only what differs from a new world.** That covers goodwill away from its start, any
override, and any reason. A colony that never meets a faction hashes as before, so **no golden
should move in F1**; measure it with `GoldenColonyProbe` rather than assuming.

**One door for every change:** `Factions.ChangeGoodwill(faction, delta, reason, subjectPawnId)`.
- It clamps to ±100, records the reason and re-evaluates `Relation`.
- It returns **false and changes nothing for a `Permanent` faction**.
- Nothing else writes `Goodwill`. A second writer is design 58 §14's "one rule with two owners",
  and the plan pins it with a test.

### 2c. The relation and its hysteresis

The reference's thresholds, kept (a-13 §2): high confidence, and chosen so a relation cannot flicker
at a boundary.

| From | To | When goodwill |
|---|---|---|
| Neutral | **Hostile** | ≤ −75 |
| Hostile | **Neutral** | ≥ 0 |
| Neutral | **Ally** | ≥ +75 |
| Ally | **Neutral** | ≤ 0 |

**Drift.** Once a game day (the needs cadence's day boundary, not a new clock), goodwill outside
`[bandMin, bandMax]` moves `driftPerDay` towards the band, and stops at its edge. Drift writes
through `ChangeGoodwill` with no reason row, because a daily "−4, drift" would push the real reasons
out of a five-row ring.

### 2d. Whose side a pawn is on: `Allegiance`, extended

Design 58 made `Allegiance` the one owner of sides:
- a side is the kind's faction;
- custody overrides it;
- `PrisonRecord.Joined` makes a recruit the colony's.

F1 extends it rather than adding a class beside it:

- `Allegiance.FactionOf(pawn)` returns a **faction index**, resolved in this order:
  1. `Joined` → Colony;
  2. else an entry in the **faction override** table (`odyssey.factions`, sparse, pawn id → faction)
     → that faction;
  3. else the kind's faction.
  The override is how a trader kind can serve two factions, and how a released prisoner goes home to
  the faction she came from rather than to her kind's.
- `Allegiance.StanceOf(pawn)` derives `Colony / Wild / Hostile / Neutral`:
  1. custody first (design 58 §4b's rules, unchanged);
  2. then **parley** (§2f);
  3. then the colony's `Relation` with her faction.
- **`Allegiance.AreHostile(a, b)`** is the pairwise question F0 introduces (§5). It is true when
  either side's faction is hostile to the other's:
  - the colony against a faction, from `Relation`;
  - faction against faction, from `hostileTo` (§2e).
  Custody and parley are applied first.

### 2e. Faction against faction

Static, from `hostileTo`, and symmetric: the loader makes it so and **refuses a one-sided entry**.
It is seen only when two peoples meet on the colony's map, as in the reference and Kenshi (a-13
§5, §11).

| | Bandits | Orc Army | Cartage | Kindred |
|---|---|---|---|---|
| **Bandits** | — | hostile | hostile | hostile |
| **Orc Army** | hostile | — | hostile | hostile |
| **Cartage** | hostile | hostile | — | — |
| **Kindred** | hostile | hostile | — | — |

Wild animals keep today's rules (revenge or flee, `CombatSystem.React`). They are not in this
table, and F0 does not change them.

### 2f. Parley

A party that has come to talk is **not hostile while it talks**, whatever its faction's relation.
The collector's party (§4) arrives from a faction that is usually Hostile, and must not be attacked
by colonists or attack them on sight.
- Parley is a flag on the **group** (the collector's, and later a visitor's), in the group's own
  saved record.
- `StanceOf` asks it before `Relation`.
- It ends when the group leaves, or when **anybody on either side strikes first**.
  - A colonist striking a parleying pawn costs **−50** and makes the faction Hostile at once
    (`collector-attacked`).
  - A parleying pawn is never the one to strike first.

## 3. The roster

| Faction | Category | Start | Band | Drift/day | Arrival | Livery | What it does for and to the colony |
|---|---|---|---|---|---|---|---|
| **Colony** (the player) | Player | — | — | — | — | the issued jumpsuit | — |
| **Wild** | Wild | — | — | — | Edge | — | animals, as today |
| **Bandits** | Diplomatic | **−80** (Hostile) | −100…−80 | **4** | Edge | red vest, welding helmet (design 42) | raids, theft; **the collector** (§4); take back their released and ransomed |
| **The Orc Army** | **Permanent** | −100 | — | — | Edge | the Synty pack's own orcs (F4) | **raids only**, the late threat tier (§4b) |
| **The Cartage** | Diplomatic | +10 | 0…+30 | 1 | Road | a hauliers' livery (trade line's choice) | **trade caravans** (the trade line) |
| **The Kindred** | Diplomatic | +20 | 0…+40 | 1 | Edge | none, plain clothes | refugees, wanderers, where released Kindred go home (M7) |

**Why the bandits' band sits below the hostile line.** The band is −100…−80, so drift always
carries them back to Hostile. **That is the racket, as a mechanic rather than a script.**
- A paid tithe lifts them to Neutral.
- Four a day wears it back down.
- A colony that stops paying is raided again, about a month after its last payment (§4a's numbers).

**The name.** The owner said *"Bandits as we know them"*.
- On screen the faction is **Bandits**.
- *The Tithe* (proposed in `proper-nouns.csv`) becomes **the word for what their collector
  demands**: *"The Bandits have come for their tithe."*
- To confirm with the owner (interview §6).

### 3a. "Something else": a candidate, for the owner to pick

The owner asked for "maybe something else" and did not name it. **Recommended candidate: the Sump,
the people of the buried city.**
- They arrive **from below**:
  - out of the lowest open layer of a cavern the colony has broken into;
  - or up a shaft the colony dug.
  They do not come from the map's edge.
- **Digging is diplomacy.** Each cavern the colony breaks into on the layers they claim costs
  goodwill with them (`sump-trespass`, about −5 a cavern).
- They trade salvage, and raid when angered.
- This is the one faction only this game could have, because the world has layers (design 00 §7,
  a-13's layer questions).

**The runner-up is feral machines** (the reference's machine-faction slot). It was not chosen,
because the Orc Army already fills "hostile to all, no diplomacy".

## 4. The collector: the first interaction (F2)

### 4a. The flow

| Step | What happens | Numbers (proposed) |
|---|---|---|
| **Fires** | Incident `Incident_Collector`, worker `Collector`. Gates: the Bandits are not an Ally; the colony's stored value is at least a floor; `minRefireDays` | earliest day 5; refire ≥ 10 days; floor 500 value |
| **Arrives** | A collector and an escort walk in along one edge (the raid's edge choice, design 55 §4) under **parley**, and stop at a **tribute spot** a few cells inside the edge, on a surface they can reach | escort 2–4, scaled by the demand's weight |
| **Demands** | A **prompt**: *"The Bandits have come for their tithe"*, the goods listed, **Pay** / **Refuse**. The game pauses on it (the reference's choice letters do). Its look is piece 2 of `factions-tab-brief.md` | — |
| **The demand** | A **share of the stores' value**. The collector picks goods by highest value per unit, ties by def index then item id, so the demand is deterministic from the stores | **15 %** of stored value |
| **Pay** | The tribute spot becomes a **storage zone that accepts exactly the demanded goods, at the top priority rung** (design 26's filter and rungs, reused whole). Haulers carry the goods there, the party picks them up (`StealJobDriver` with a load) and leaves by its edge, and the zone is removed | up to 6 game hours to deliver |
| **Paid** | `tithe-paid`, pro rata to the value delivered. A payment of **under half** counts as a refusal | a full payment sets goodwill to **max(goodwill, 0) + 20**, so a full payment always ends a Hostile relation |
| **Refuse**, or out of time | `tithe-refused`; the party leaves; a **deadline** is set. **At the deadline**, a punitive raid comes (design 55's band, `RaidWorker`), sized up | **−30**; deadline **2 days**; raid size = auto × **1.5** |
| **Paying late** | Until the deadline, the colony can still pay: a **tribute spot** remains marked on the edge, and filling it before the deadline calls the raid off | — |
| **Struck** | Anybody striking a parleying bandit ends parley (§2f) | **−50**, Hostile at once |

**How the racket runs out.** A full payment lifts goodwill to at least +20.
- Drift at 4 a day brings it to −75 (Hostile) in about 24 days, two of the calendar's 12-day months.
- The collector returns every 10 days or so.
- So a colony that pays every time is left alone. One that skips a payment has about a month of
  grace, then raids.
- **Tune this against a played colony, not on paper.**

**Why a storage zone, not a new haul job.** A zone with a filter and a priority rung is exactly "take
these goods to this place first":
- the haul system already finds, carries and fills one;
- design 26 §11's refusal rule keeps it from filling with anything else;
- the colony's own stores lose the goods by being emptied into it.
No new giver, and no second owner of where a load goes.

### 4b. The value per commodity

Nothing has a value today (`RaidWorker.cs:72` is waiting for "a wealth measure").
- F2 adds `value` to every item Def, in integer credits (`ui.res.credit`'s unit).
- It is pinned by the content fingerprints like every other content number.
- **One table serves two needs:**
  - the collector's share;
  - raid sizing. The auto size can read stored wealth beside headcount and days; whether it should
    is design 55's call, recorded rather than made here.

### 4c. The Orc Army (F3)

- A **permanently hostile** faction; `ChangeGoodwill` refuses every change.
- Its raid group is a new mix, `RaidMix_Horde`, of new kinds appended after kind 6:
  - an orc warrior (heavy melee);
  - later a brute.
- Each is **fewer but tougher**:
  - more hit points, larger blows, slower;
  - a horde member counts **2** in the auto size, so a horde raid has about half the heads of a
    bandit raid of the same weight.
- **It is hostile to the Bandits too.** A horde raid arriving while a bandit raid is on the map
  fights it. That is the one Kenshi moment the slice buys (a-13 §11).
- The phases, gathering, probe and withdraw-at-half are design 55's, unchanged.
- **The look is its own unit (F4)**, once the owner names the Synty pack.
  - It goes under `Assets/Synty/` like every pack, never committed.
  - Its humanoid rig should take Base Locomotion.
  - Check it with `MeasureSole` on import (P11), as every new rig is.

## 5. F0: one owner of "is this an enemy?"

Before any faction exists, every question in the table below moves onto
`Allegiance.AreHostile(a, b)` / `IsThreat(a, b)`.
- Defined, at F0, to give **exactly today's answers**: hostile when one side is Hostile and the
  other is a colonist, plus design 58's custody rules.
- F0 is behaviour-preserving, and **every golden must stay identical**.
- Its value is that F1 changes one function instead of a dozen, and that the asymmetry
  (`Ranged.cs:80`) goes before more call sites copy it.

| Where | What it answers today | After F0 |
|---|---|---|
| `HostileThinkNode.ColonistToFight` | a hostile's target: colonists only | `AreHostile(me, other)` |
| `Melee.IsThreatTo` | a colonist's view: other is hostile or attacking me | `IsThreat(other, me)` |
| `Melee.ColonistUnderAttackBy` | victim a colonist, attacker not | `AreHostile` + the attack |
| `Melee.HoldTarget`, `DangerTo`, `AnythingHostile` | a drafted colonist's hold, Flee's danger, "any hostile about" | `IsThreat` / `AreHostile` against the colony |
| `Ranged.NearestTargetInSight` (`:80`) | `me.IsColonist ? IsThreatTo : other.IsColonist` | `IsThreat(other, me)` both ways |
| `CombatSystem.Apply` (stray bullet, `React`) | a colonist's stray does not start a fight; a hostile remembers only a colonist attacker | `AreHostile` |
| `FriendlyFireListener` | colonist hit colonist | "same faction and not hostile" |
| `BuildingTargets.TryNearestColonyTarget` | any `Built` edifice is the colony's | unchanged at F0; buildings gain an owner only when a faction builds (M7) |
| `RaidThinkNode`, `RaidSystem`, `RaidWorker` | read `IsColonist` directly | `AreHostile` against the band's faction |
| `RaidMixDef` validation | a mix may only name Hostile kinds | a mix names its **faction**, and its kinds must belong to it (F1) |

**The proof** is `AllegianceConsumerTests` (design 58's) extended with a row per call site, plus a
**one-owner test** in the style of `HopPriceHasOneOwnerTests`. That test reads the Sim sources and
fails on any `.IsHostile &&` / `.IsColonist &&` pairing outside `Allegiance`.

**Merge order.** F0 edits the same files as the prisoner line, so it **branches from it, or waits
for it to merge**. It never forks `Allegiance`.

### 5a. As built (2026-09-26)

F0 is built on `claude/lucid-euler-9puelu`, stacked on the prisoner line, which is merged in
underneath. `Allegiance` gained three answers:

| Answer | At F0 | Replaces |
|---|---|---|
| `AreHostile(a, b)` | one is hostile and the other a colonist; symmetric | the hunt's target (`HostileThinkNode`, both the retaliation and the nearest), a raid member's hand-over (`RaidThinkNode`), and whom a struck hostile remembers (`CombatSystem.React`) |
| `IsFoe(me, other)` | an enemy, or, for a colonist, anybody attacking her | `Melee.IsThreatTo`, `Melee.DangerTo`, and both copies of the ternary (`Ranged.NearestTargetInSight`, `CombatJobs.EnemyInReach`) |
| `AreAllies(a, b)` | both colonists | friendly fire (`FriendlyFireListener`) and a stray that starts no fight (`CombatSystem.Apply`) |

**One deliberate difference.**
- The old ternary made every colonist a foe of *any* non-colonist who asked, a held prisoner or a
  hog included.
- Neither ever asks: a prisoner thinks as a prisoner, and an animal has no gun and hunts only its
  attacker.
- `IsFoe` gives them no enemies, and a test says so.

**Left colony-centred on purpose.** Each of these asks about the colony, not about a pair:
- `Melee.AnythingHostile`: is anything hostile to *us* about;
- `Melee.ColonistUnderAttackBy`: is one of *ours* under attack;
- the raid's early trigger (`RaidSystem`: a colonist near the band) and its size census
  (`RaidWorker`);
- `Surrender`, `WeaponDraw`, and the mind choice in `JobSystem` all read the pawn's own stance
  (`IsHostile`), which `Allegiance` already owns.

F1 revisits the raid trigger when a visitor can stand near a band.

**Proof.**
- `HostilityHasOneOwnerTests` (six tests) holds the three answers to the old expressions, written
  out as an oracle, over a cast of every kind of pawn:
  - a colonist, a recruit and an escapee;
  - a bandit, a gunman, a held bandit and a released one;
  - a hog.
- It fails the build on a second copy of a side rule anywhere in `Assets/Odyssey/Sim`. There are
  three patterns and an allow-list of two files, each with its reason, and a test that the patterns
  catch the four lines F0 replaced.
- **Every golden is identical** and the Long tier's combat gate passes beside its lockstep twin,
  so no hash moved.
- Fast tier 2,044 Sim + 1,302 Hud; Long 55.
- Unity has not compiled it yet: it touches only Sim, which the fast tier compiles.

## 6. The contract with the trade line

The trade caravans are another agent's work. This line owns the model, and the trade line needs
exactly this:

```
FactionHandle.Cartage                                    // a constant, the Def's fixed position
Relation  Factions.RelationOf(int faction)               // Hostile / Neutral / Ally
int       Factions.GoodwillOf(int faction)
bool      Factions.ChangeGoodwill(int faction, int delta, GoodwillReason reason, int subject = -1)
FactionView[] snapshot.Factions                          // for any pane that names the trader's people
```

- A trader kind declares `<faction>Faction_Cartage</faction>`.
- **Before F1 lands**, the trade line hits the gap at once: today a pawn can only be Colony, Wild or
  Hostile, and a trader is none of them. **The stop-gap:**
  - append `Neutral = 3` to the current enum;
  - have `Allegiance` answer it as neither colonist nor hostile;
  - F1 then maps it onto `Faction_Cartage`.
- **Goodwill from trade is the trade line's to set** (a reason `trade`, amount by value). This
  document does not fix it.

## 7. The world list

The owner's ruling is an **abstract list**, not a map.
- At world creation, each Diplomatic faction is given `settlementsMin…Max` settlements.
- Each settlement gets a name, and a distance of `distanceDaysMin…Max` days drawn from the world
  seed.
- They are saved in `odyssey.factions`, so a rename in content never renames a player's world.
- They are shown in the Factions tab.
- Nothing travels them in the slice. They are **the destination and origin for M7's timers**: a
  released prisoner going home, a ransom paid, a caravan's source, a refugee's origin.

**Settlement names are player-facing, so they are wiki content.**
- A small pool goes in `docs/design/settlement-names.csv`, in the proper-noun register.
- It is generated into `SettlementNames.g.cs` by **`emit_labels.py`**, the third file that script
  emits (CLAUDE.md: one script and one `--check`), and into the wiki.
- The Orc Army is shown with no settlements ("no dealings").

## 8. The Factions tab (F1)

Panel B9 (`10-ui-panel-catalogue.md`) on **F8**: the command bar has carried a dead *Factions F8*
item since it was written (`HudCommands.cs:136`), and it goes live. (A first draft of this section
said F6, having missed it.) **One row per faction:**
- name;
- a livery swatch;
- the relation word;
- a goodwill bar from −100 to +100 **with the two hysteresis marks drawn on it**, so the player can
  see how far each line is;
- a trend arrow for the last day;
- **the last five reasons**: *"+20 tithe paid, Tansy 4"*;
- its settlements with their distances.

**No diplomatic actions in the slice.** Gift and Declare hostility are M7, with the comms console.

**Its look is briefed to Claude Design** in `docs/reference/mockups/factions-tab-brief.md`, as the
Assign and Animals tabs were. The brief also covers the tithe prompt and alert (§4a) and the four
faction glyphs. It leaves Claude Design to choose how the thresholds are drawn and where the detail
sits; the answer becomes constants in F1.

**Registry keys (wiki content, added with F1):**
- `ui.tab.factions` (exists);
- `ui.faction.<id>` per faction;
- `ui.relation.hostile` / `neutral` / `ally`;
- `ui.goodwill.<reason>` for every reason key;
- `ui.prompt.tithe.*` for the collector's prompt (F2).

## 9. Hooks for the prisoner line (design 58)

| Design 58's seam | What factions give it |
|---|---|
| Release | The prisoner walks off to her faction; **+15** `prisoner-released` (the reference's ≈ +16 for a tended return, a-13 §3) |
| Execute | **−20** `prisoner-executed`, with her faction |
| Ransom (`PrisonMode.Ransom`, refused `NotBuilt`) | M7: a demand to her faction, paid after a timer by her settlement's distance in days, then she leaves. Built on §7's list |
| A downed raider tended and let go | the same as Release |
| Kidnap (design 33 §17f) | the mirror: a colonist held by a faction, whose settlement becomes where a rescue or ransom goes |

## 10. Ideas recorded, not planned

- **Arrival by stratum.**
  - `arrival = Below` is the Sump's (§3a).
  - `Road` is the Cartage's: a real road needs a road generator, so until then it is the edge
    nearest the start.
  - Worth keeping as a Def field from F1 so M7 does not restructure.
- **A faction's buildings.** A toll post, a trader's stall, a Sump shaft head. `BuildingTargets`
  assumes every built edifice is the colony's, and an owner byte per edifice is the M7 change.
- **Leaders.** The reserved `LeaderId`. A named leader who can arrive in person makes killing one a
  choice with a price.
- **The Bandits' collector learns.** Each refusal raises the next demand's share, and each payment
  lowers it: the racket remembers.
- **Allies and aid** (a-13 §4): a comms console (`ui.arch.tool.comms`); spend 25 goodwill for help
  in a raid.
- **Gifts** (`ui.command.gift`): value to goodwill with diminishing returns.

## 11. What not to undo by tidying

- **`Allegiance` is the only place a side is judged, and `ChangeGoodwill` the only writer of
  goodwill.** A second copy of either is the fault this design exists to prevent.
- **`Relation` is stored, not derived from goodwill.** The hysteresis needs the previous state;
  deriving it would make a relation flicker at −75.
- **Drift writes no reason row.** The ring is five long.
- **`Kind` is never rewritten** (design 58 §4a). A faction change is the override.
- **Hash only what differs from a new world**, or every golden moves the day F1 merges for nothing
  the colony did.
