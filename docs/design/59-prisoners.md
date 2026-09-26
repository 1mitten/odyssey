# 59 — Prisoners

**Written 2026-09-26**, from the owner's request and a four-round interview the same day.
Branch `claude/prisoner-bed-assignment-98afc0`. Plan: `docs/plans/prisoners.md`. Interview:
`docs/research/prisoner-interview.md`. Reference: `docs/research/a-20-prisoners.md`.

> *"We need a prisoner similarish is to this but improved … We need to be able to assign beds as
> prison bed to assign room. We need to create some systems here that could help bring this
> together."* — the owner, 2026-09-26

**Status: built (P1–P12), reviewed twice, merged with `main` and not yet played** (§15, §16).
**Renumbered 58 → 59 on 2026-09-26**: `main` took 58 for cracks first. Every number below is a
proposal the owner confirms at the first play (§8, §9, §10).

## 1. What a prisoner is

A prisoner is a **person held by the colony**. They are not a colonist and not an enemy.

- **How they arrive:** captured while downed, surrendered, or arrested.
- **Where they live:** a **prison bed**. Marking one bed *Prisoner* makes its whole enclosed room a
  **cell**, and a prison bed with no room around it holds its prisoner **shackled**.
- **How they are held:** they cannot open a door.
- **Who looks after them:** a **warden** feeds them and talks to them.
- **What becomes of them:**
  - **recruited**, when a visible willingness bar fills;
  - **released** or **exiled**;
  - or they **escape**, at a visible risk the player can drive down.

Execution, prison labour and ransom are later units (§13).

**Our names** (clean room): `PawnCustody`, `Allegiance`, `PrisonSystem`, `PrisonRecord`,
`PrisonMode`, `BedRules`, `PrisonCells`, `Recruitment`, `EscapeRisk`, `PrisonerTree`,
`EscapeTree`, *willingness*, *shackled*, *warden*, *exile*.

### What improves on the reference (a-20 §5)

| The reference's complaint | Our answer |
|---|---|
| Recruiting is slow and random | A deterministic bar with an ETA and its blockers named (§8). No hidden roll. |
| Break risk grows linearly with headcount | Per-prisoner risk × 1/√n, so the colony's total grows with √n (§9). |
| Breaks are unexplained | The risk is shown **with its reasons**, and the number shown is the number rolled. |
| Doors do not hold | A prisoner cannot open a door; an escapee has to **bash one down**, so door material counts (§9c). |
| Escape UI names one prisoner | The alert cycles every escapee (§11). |
| Hauling the downed is a chore | A warden capture mark and surrender (§7, §10). |
| Only grim ways out | Exile and release, now; ransom once factions exist. |

## 2. The owner's rulings (2026-09-26)

| # | Ruling |
|---|---|
| 1 | **A bed flag sets the room.** A prison bed in an open room still works: its prisoner is shackled. |
| 2 | **Prisoners can't open doors.** They can slip through one a colonist is holding open. |
| 3 | **Four ways in:** right-click Capture, a warden capture mark, surrender, arrest. |
| 4 | **Recruitment is in the first build**, as a visible willingness bar. |
| 5 | **The Social skill becomes real** (a format bump and one golden bake). |
| 6 | **Escape risk is visible, comes from conditions, and is normalised by prison size.** Escapees bash the door and can be recaptured. |
| 7 | **Recruit, Release and Exile first.** Ransom is a seam. Execute and labour come later. |
| 8 | **Arrest works on any colonist, any time.** She may resist. **Releasing her returns her to the colony.** |
| 9 | **A right-click on a downed enemy opens a menu: Capture / Finish off.** |
| 10 | **A prison jumpsuit** in the cell and the **issued jumpsuit** on joining. Face, hair and beard are kept. |
| 11 | **Walk the cell by day, sleep in the bed at night**, eat in the cell. Shackled prisoners stay on the bed. |
| 12 | **No mental breaks for prisoners.** Low mood raises escape risk and slows recruitment instead. |

## 3. What existed before, and is reused rather than rebuilt

| Existing piece | What this design uses it for |
|---|---|
| **Reserved names**: `ui.pawn.prisoner`, `ui.command.capture`, `ui.command.arrest`, `ui.command.recruit`, `ui.bulletin.recruited`, `ui.alert.prisonerescape`, `ui.work.warden` (`WorkCatalogue.cs:123`, not simulated), `ui.skill.social` (`SkillCatalogue.cs:127`, not simulated) | Their labels, as they stand |
| **Bed ownership**: `PlacedEdifice.Owner`, `ConstructionGrid.AssignOwnerAt` (:1778), `TryClaimForSleeper` (:1920) | The owner of a prison bed is its prisoner |
| **Rooms**: `EnclosureGrid.RoomAt` (:161), `ThermalRoom.Cells` and `.Doors`, from design 28. Per layer, roofed, doors are boundaries | A cell is a room with a prison bed in it |
| **The door rule**: `TraverseModes.OpensDoors` (`NavGrid.cs:142`); `DoorSystem` holds a door 30 ticks | Confinement, with no new traverse mode (§6) |
| **The rescue carry**: `RescueJobDriver`, `RescueRules`, `Pawn.CarriedBy`, `ReservationTargetKind.Pawn` (design 33 §11) | Capture |
| **The building attack**: bandits already break doors (design 33, *buildings as targets*) | The escapee's way out |
| **The edge search**: `EdgeTarget.Find` (`Theft.cs:104`), `Theft.Leave` | Escape, release and exile |
| **The sparse per-pawn setting pattern**: `PawnArea`, `AssignSection` (design 43) | The shape of `odyssey.prison` |
| **The outfit owner**: `PawnOutfits.For` (`Hud/PawnOutfit.cs`) | The jumpsuit (§11d) |
| **The context menu**: `ContextMenuModel.OfferEquip` and `OfferTend` | `OfferCapture`, `OfferFinishOff` and `OfferArrest` |
| **The doctor and the patient** (designs 37, 43 §15) | Treating a prisoner |

## 4. State: custody, and one owner of who is on whose side

### 4a. `Kind` never changes

A pawn's side comes from its **kind** today: `Pawn.Faction => Content.KindOf(Kind).faction`
(`Pawn.cs:219`). `Kind`'s setter says nothing else may change what a pawn is.

**Recruiting does not rewrite `Kind`.** Changing a Bandit into a Colonist kind would break that
rule. It also cannot express an *arrested colonist*, whose kind is already Colonist. And it throws
away the home faction that ransom and M7's factions will need.

Instead a pawn carries a **custody override**:

| Field | Values | Where |
|---|---|---|
| `Pawn.Custody` | `Free 0, Prisoner 1, Escaping 2, Released 3` | Pawn hash word **bits 28–29**. Zero while Free, so no golden moves |
| `PrisonRecord.Joined` | a recruited pawn: faction reads Colony | `odyssey.prison` |
| `PrisonRecord.Dressed` | has reached a prison bed, so wears the jumpsuit | `odyssey.prison` |
| `PrisonRecord.Arrested` | was a colonist when taken | `odyssey.prison` |
| `PrisonRecord.Mode`, `.Willingness`, `.LastChatTick`, `.CaptureMark` | see §8, §7 | `odyssey.prison` |

**Bits 30–31 of the pawn word stay free on purpose.** The design pass proposed putting `Joined`
and `Dressed` there. That would fill the word, and the next per-pawn flag would need a second word.

The record's fields are hashed by `PrisonSystem.ContributeTo`, and only while any record exists. A
colony that never takes a prisoner therefore hashes as before.

A recruited pawn keeps a one-field record (`Joined`) for ever. That is the price of not rewriting
`Kind`, and it is a few bytes a recruit.

### 4b. `Allegiance`, the one owner

A new static `Sim/Pawns/Prison/Allegiance.cs` answers every question about sides. `Pawn.Faction`,
`IsColonist`, `IsHostile`, `IsPrisoner`, `OwnMode` and `Motive` all delegate to it, and nothing
else computes them.

- `Faction` is `Colony` if `Joined`, else the kind's faction.
- `IsColonist` is person ∧ Faction = Colony ∧ Custody = Free.
- `IsPrisoner` is Custody ∈ {Prisoner, Escaping}.
- `IsHostile` is (Faction = Hostile ∧ Custody = Free) ∨ Custody = Escaping.
  - An arrested colonist who breaks out is an enemy while she runs.
- `OwnMode` is `Bandit` while Prisoner or Escaping, and the kind's mode otherwise.
  - So a joined bandit walks as a colonist once `Joined` makes her one.
- `Motive` is `None` unless Free and not Joined.

**The risk this answers is bug-patterns' first pattern: one rule with two owners.** There are about
forty `IsColonist`/`IsHostile` reads. Because they all go through `Allegiance`, most get the right
answer for free. The ones below need a deliberate answer each, and **`AllegianceConsumerTests`
pins every row**.

| Consumer | A prisoner's answer |
|---|---|
| Mind choice (`JobSystem.cs:506`) | Custody first: Prisoner → `PrisonerTree`, Escaping → `EscapeTree`, Released → `LeaveTree` |
| `Melee.IsThreatTo`, `Ranged.NearestTargetInSight` | Unchanged. Raiders ignore prisoners (not colonists); colonists fight escapees (hostile) |
| `Pawn.NeedsTick` (virtual) | Colonist ∨ (Prisoner ∧ ¬Downed). Food and rest tick. **Joy is frozen** — a cell offers no way to meet it; recreation is a later lever |
| Mental breaks | Never for a prisoner (ruling 12) |
| Healing (`CombatSystem.cs:169`) and recovery (`:191`, `Health.cs:206`) | As a colonist: heals in bed, stands at the colonist threshold. Otherwise a captured bandit lies down for ever |
| Doctor (`Medical.NeedsTreatment`, `WorthAnOrder`, `AwaitsDoctor`) | Treats prisoners. A captured bandit bleeds out without it (design 43) |
| Rescue (`RescueRules.NeedsRescue`) | Colonists only. Returning a downed prisoner to a cell is Capture's job |
| Raid band (`RaidSystem`) | A captured or surrendered member leaves its band. P6's test: capturing must not change **when** the band withdraws |
| Roster, Work tab, Assign tab, draft | Follow `PawnView.IsColonist`, so a prisoner leaves them and a recruit arrives in them with no edit |
| `PawnView` | A `Custody` byte beside `Asleep` (`Views.cs:312`), because `PawnFlags` is full. `IsColonist` requires Free; `IsPrisoner` is added |
| HUD outfit (`PawnOutfits.For`) | Custody first: Dressed → Prisoner, Joined → Issued, else the old flags rule |
| Bed choosers and the owner picker | §5 |
| Inspect pane | The prisoner tab (§11b) |

## 5. Beds and cells

### 5a. One bed rule, before anything else

Four near-copies of *own bed, else the nearest unowned bed* choose beds:
- `CriticalNeedsThinkNode.TrySleep` (`JobSystem.cs`);
- `Medical.BedFor` (`Medical.cs:267`);
- `RescueRules.BedFor` (`RescueRules.cs:57`);
- `ConstructionGrid.TryClaimForSleeper` (:1920).

The owner picker is a fifth, in the Hud. Adding "unless it is a prison bed" to five places is how
this project gets bug-patterns P1.

So **P1 introduces `BedRules`**:
- `BedRules.CanUse(pawn, bed)` — a colonist may use a colony bed, a prisoner a prison bed, and the
  owner must be nobody or the pawn herself.
- `BedRules.MayOwn(pawn, bed)`.
- `BedRules.CountsForBeds(pawn)`.

Every chooser, `AssignOwnerAt` and the picker call it. `BedRulesAgreementTests` walks the Hud
picker's filter against the Sim rule.

Two faults fall out on the way:
1. **`TryClaimForSleeper` counts every pawn** as needing a bed, hogs and bandits included.
2. **The picker offers them as owners** (`HudShell.Inspect.cs:1204`), and `AssignOwnerAt` accepts
   any pawn id.

### 5b. Bed purpose

- **What is saved:** a sparse section **`odyssey.bedpurpose`** holds the set of prison-bed head
  cells. It is hashed only while the set is non-empty.
  - Not a field on `PlacedEdifice`, because `EdificeSaveSection` hashes its records
    unconditionally. A field there would bump the format and move every golden for a feature no
    golden uses.
- **Marking (intent `SetBedPurpose`):** marking a bed stores **every bed in its room** at that
  instant.
  - If a wall is later knocked out, each stored bed becomes a shackle bed rather than silently
    turning back into a colonist's bed with a prisoner in it. The player's intent survives the
    room breaking.
- **Unmarking** a bed clears every stored bed in its room.
- **Effective purpose is derived, never saved.** A bed is a prison bed if it is stored as one or its
  room holds a stored one.
  - So a bed built later inside a cell, or a room merged into a cell, is a prison bed with no
    further order.
- **Shackled:** a stored prison bed whose `RoomAt` is 0 (open, unroofed, or over 2,500 cells).
- **Cache:** `PrisonCells` holds the set of room keys that are cells. It is rebuilt when a new
  **`EnclosureGrid.Generation`** counter moves or the bed list changes, never per query (P12).
  - The enclosure has no change counter today; P4 adds one, bumped wherever `RoomResolved` fires.
- **Eviction:** when a bed becomes a prison bed, a colonist owner is cleared. When one stops being
  a prison bed, a prisoner owner is cleared.
  - An unhoused prisoner is Capture's to move to a free prison bed. If there is none, the
    *No prison bed* alert stands.

### 5c. What a cell is not

A cell is **not saved, not named and not a room role system**. Room roles (a-05) are a later,
broader unit. When they arrive, *cell* becomes the first role and `PrisonCells` becomes a query of
them.

## 6. Confinement and the prisoner's mind

- **Movement:** a prisoner moves in **`TraverseMode.Bandit`**.
  - It cannot open a door. It can pass one `DoorSystem` is holding open.
  - No new traverse mode, because a new one costs a district flood on every rebuild
    (`NavGrid.cs:102`).

**`PrisonerTree`**, in order:
1. **Downed.**
2. **PrisonerNeeds.**
   - Eat food lying anywhere in their own room, a shelf in it included.
   - Sleep in their prison bed when rest calls. This is what *sleeps at night* means, since
     nothing obeys the schedule yet.
3. **Shackled.** If their bed is a shackle bed, lie or sit on it.
4. **CellWander.** Pick a cell of `ThermalRoom.Cells` by a per-pawn deterministic draw.
   - **Never a door cell**, so a held-open door is only ever used by an escapee.
5. **Wait.**

**`EscapeTree`:**
1. Downed. **A downed escapee becomes a Prisoner again.**
2. Retaliate against the arrester, if adjacent.
3. Through a held-open door if there is one; else **bash the nearest door of the room** with the
   existing building attack.
4. Run for `EdgeTarget.Find` and leave the board.

**`LeaveTree`** (Released, Exiled): walk to the edge and leave.
- **An arrested colonist being released instead walks out of the cell door as a colonist**
  (ruling 8).

## 7. The warden, and capture

- **The Warden work type** is `WorkHandle.Warden = 8`, with `Work_Warden` in `WorkTypes.xml`, rated
  by Social.
  - The existing Work tab column goes live.
  - Loaded saves give it priority 3.
- **Job defs**, appended from handle 28: `Capture`, `Arrest`, `FeedPrisoner`, `Chat`, `Escort`,
  `GoToCell`, `Escape`, `LeaveFree`.
  - Counters from index 22 up are hashed only once they are non-zero, so these move no golden.
- **Givers**, found by reflection:
  - **`CaptureWorkGiver`** — emergency, because a downed raider bleeds. It takes marked downed
    enemies and unhoused or strayed prisoners.
  - `FeedPrisonerWorkGiver`.
  - `ChatWorkGiver`.
  - `EscortWorkGiver`.

**Capture** is the rescue driver with a different target and bed rule:
1. walk;
2. lift (`CarriedBy`, with the `Pawn` reservation);
3. carry to `BedRules`' nearest free prison bed;
4. lay down.

On laying down:
- Custody becomes Prisoner.
- `Dressed` is set.
- The pawn leaves its raid band.
- Any weapon still held is dropped.

**No free prison bed means no capture.** The menu row is dim with its reason, the mark stays, and
the *No prison bed* alert stands. Nothing falls back to anything else.

**Right-click on a downed enemy** (ruling 9):
- `CombatOrders.cs:52` stops routing it straight to an attack. The context menu offers
  **Capture** then **Finish off**.
- Capture sets the capture mark. If a drafted colonist is selected, it also orders her to do it now.
- Finish off is today's `ToTheDeath` attack, unchanged, behind a deliberate row.

## 8. Recruitment

**One owner: `Recruitment.Factors(prisoner, warden, ctx)`.** It returns S, M, T, the gain and a
blocker mask. The chat driver and the pane's aspect writer both call it, so the ETA shown is the
arithmetic run.

**The bar and the chats:**
- Willingness runs **0 to 1,000,000**.
- In *Recruit* mode a warden chats every **6 in-game hours** (4 a day).
- A chat adds:

```
gain = 50,000 × S × M × T / 1000³

S (Social)    = 400 + 120 × level                      0 → 400, 5 → 1,000, 10 → 1,600
M (mood)      = max(250, 250 + 1.5 × mood)             mood 0–1000; 500 → 1,000
T (treatment) = fed × tended × cell / 1000²
                fed      1,000, or 500 when hungry
                tended   1,000, or 600 with an untended injury
                cell     1,000 + 50 per bed-quality tier above Normal; shackled 600
```

| Warden Social | Well kept (mood 600, fed, tended, a normal cell) | Poorly kept (mood 300, hungry) |
|---|---|---|
| 0 | ≈ 11 days | ≈ 36 days |
| 5 | ≈ 4.3 days | ≈ 14 days |
| 10 | ≈ 2.7 days | ≈ 9 days |

- **Blockers** are published as reasons:
  - no warden has Warden work enabled;
  - hungry;
  - untended;
  - shackled;
  - low mood;
  - low Social.
- **ETA** = ⌈remaining ÷ gain⌉ × 6 h, computed with the best enabled warden (highest Social), and
  the pane names her.
- **Social training:** each chat trains the warden's Social. **Social is `SkillIndex` 9** on the
  shared curve (P3).

**On joining**, the pawn:
- becomes Free with `Joined` set;
- has passions and starting skills rolled (`RollPassions`, `RollStartingSkills`, deterministic from
  `RollSeed`; **raid-spawned bandits have no skills at all today**, `RaidSystem.cs:306`), keeping
  any skill already trained;
- gets work priorities of 3 and the default schedule;
- has the prison bed released;
- wears the issued outfit;
- posts `ui.bulletin.recruited`.

**Mode on arrival:** a captured prisoner starts in **Hold**, and so does an arrested colonist.
Nothing is recruited that the player did not choose.

## 9. Escape

### 9a. The risk

**One owner: `EscapeRisk.Odds(pawn, ctx)`.** It returns parts per million per day and a reasons
mask, in integers (designed in per mille; §15a says why it moved).

| Factor | Effect |
|---|---|
| Base | 20,000 ppm (2 %) a day, a mean of 50 days for one prisoner |
| Mood | below 300 ×3; 300–499 ×1.5; above 700 ×0.5 |
| A door of their room open, or shackled | ×2 |
| No free, standing colonist within 10 cells (x/z), on their layer or the ones either side | ×1.5 |
| Unhurt ×1.5 · hurt ×0.5 · downed | downed → 0 |
| Well kept (fed, no untended injury) | ×0.7 |
| **Prison size** | × 1000/√n, n = prisoners held |

The last row is the headcount fix. Four prisoners each carry half the risk one would, so the
colony's total doubles rather than quadruples.

### 9b. The roll

- **When:** once an hour per prisoner, staggered by `(tick + id) % 2500`.
- **Threshold:** per million an hour = the day's ppm / 24 (`EscapeOdds.PerHourPpm`).
- **Draw:** `DeterministicRandom.ForTick` with a new `PrisonPurpose.Escape` constant.
  - Check it against every existing purpose value; the raids review found cover and raids sharing
    streams (design 55 §15).
- **The risk shown is the risk rolled.** `EscapeTests.ThePublishedRiskIsExactlyWhatIsRolled`
  derives the roll's threshold from the published aspect, with a doubled-threshold negative control;
  `PrisonAspectNamesTests` and `PrisonContractTests.TheAspectNamesAreSpelledAsTheInterfaceReadsThem`
  hold the interface's copy of the names and bits to the simulation's (§16).

### 9c. What an escape is

- Custody becomes **Escaping**, the pawn is hostile, and **`ui.alert.prisonerescape` cycles every
  escapee** on click.
- The escapee bashes the room's nearest door **unarmed** (§6).
  - How long a door holds is the building attack's existing arithmetic against the door's hit
    points. **Door material now matters to a prison**, which is the point.
- A broken door is an open door: every other prisoner in that room now carries the ×2. **A
  breakout can cascade without a cascade rule.**
- **Reaching the edge:** the escapee leaves the board, *Prisoner escaped* is posted, and the record
  is dropped.
- **Downed on the way:** they are a Prisoner again, and Capture returns them.

## 10. Surrender and arrest

### Surrender

- **Trigger:** a hit that leaves a standing raider under 30 % hit points rolls **once**
  (`PrisonPurpose.Surrender`).
- **Chance:** 25 %, doubled when its band is withdrawing or no band-mate stands within 10 cells.
- **Only when a free prison bed exists.** No cell, no surrender.
- **What follows:**
  1. It drops its weapon and leaves the band.
  2. It becomes a Prisoner and walks itself to the bed (`GoToCell`).
  3. *Surrendered* is posted.

### Arrest

- **How it is given:** a row in the right-click menu on a standing colonist (`OfferArrest`) —
  **while nobody selected is drafted**, so a drafted right-click stays a move. P10 built it as a
  button on her own pane instead (§15a); the second review measured that header and the owner moved
  it back to the menu (§16 H1). It sends the first standing colonist selected, or the nearest, and is
  dim with its reason when it would be refused (§16 H2).
- **On contact** the target rolls resistance: 20 % + (500 − mood) / 20, clamped to 5–60 %.
- **If she resists**, she is Escaping: she fights the arrester or flees, and is downed, then
  captured.
- **Either way:**
  - she is `Arrested`;
  - every free colonist takes *Colonist arrested* (−40, a proposal);
  - on release she takes *Was arrested* (−80, a proposal).
- **Design 33's gate says an unordered fight ends in downs, never deaths.** An arrest is an ordered
  fight. P12 was to assert it ends in downs; **no test does**, and §15e owes it.

## 11. The interface

### 11a. The bed pane

- A **Prisoner** toggle row, drawn as a `HudGlyph` (bug-patterns P13).
- The owner picker lists **colonists for a colony bed and prisoners for a prison bed**, through
  `BedRules`.
- A shackle bed says so.

### 11b. The prisoner tab

A prisoner's inspect pane shows the face and name. The body and draft rows are hidden, as they are
for a bandit (design 42 §6). The pane opens on a **Prisoner** tab:
- **Mode chips:** Hold · Recruit · Release · Exile. *Ransom* is drawn dim, not built.
- **Willingness bar**, with its ETA, its warden's name, and the blockers as short lines.
- **Escape risk** in ‰ a day, with its reasons.
- **A shackled line** when it applies.

The values arrive as pawn aspects (`odyssey.pawn.prison.*`), written by one writer that calls
`Recruitment.Factors` and `EscapeRisk.PerDay`.

### 11c. Menus, alerts and bulletins

- **Right-click:** the three rows above (§7, §10).
- **A downed enemy's pane** carries a **Capture** mark toggle.
- **Alerts:**
  - `ui.alert.prisonerescape` (reserved), cycling;
  - `ui.alert.noprisonbed`, while a capture mark or an unhoused prisoner is waiting on a bed.
- **Bulletins:**
  - `ui.bulletin.recruited` (reserved);
  - `ui.bulletin.escaped`;
  - `ui.bulletin.surrendered`;
  - `ui.bulletin.arrested`.

### 11d. The look

- **`PawnOutfit.Prisoner`** (value 2) is a prison jumpsuit over the pawn's own rolled person, like
  the bandit outfit layer (design 42). Face, hair and beard are kept.
- **Colour:** a distinct orange is proposed; the owner picks at first look.
- **Who wears it:** anyone `Dressed` — arrived in a prison bed and not yet free. That includes an
  escapee, so a breakout is visible at a glance.
- **On joining:** `Issued`.
- **An arrested colonist** wears it too, and goes back to `Issued` on release.
- **Art:** the packs' prisoner bodies are an option to weigh against a recolour of the issued
  jumpsuit.
  - Either must ask whether the art **resolved** rather than whether a catalogue exists (the
    runner has no `Assets/Synty`).

### 11e. New registry keys

These land with the unit that uses each: P4, P6, P8–P12. **As built, the tab and its readouts were
not needed** (§15a: the facts are rows on her pane, named by row, not by registry key), so
`ui.tab.prisoner`, `ui.bed.prisoner`, `ui.prisoner.willingness`, `ui.prisoner.escaperisk` and
`ui.prisoner.shackled` were never made. §16 added `ui.pawn.released` (*Let go*).
- **Tab and bed:** `ui.tab.prisoner`, `ui.bed.prisoner`.
- **Mode chips and readouts:** `ui.prisoner.hold`, `ui.prisoner.recruit`, `ui.prisoner.release`,
  `ui.prisoner.exile`, `ui.prisoner.ransom`, `ui.prisoner.willingness`, `ui.prisoner.escaperisk`,
  `ui.prisoner.shackled`.
- **Command:** `ui.command.finishoff`.
- **Bulletins:** `ui.bulletin.escaped`, `ui.bulletin.surrendered`, `ui.bulletin.arrested`.
- **Alert:** `ui.alert.noprisonbed`.
- **`ui.command.release` stays the animal's *Let this animal go*.** The prisoner's Release is a
  mode chip with a key of its own.
- **Social and Warden go live:** `SkillCatalogue`'s `ui.skill.social` row in P3, and
  `WorkCatalogue`'s `ui.work.warden` row in P6.

## 12. Save and hash

| What | How | Moves goldens? |
|---|---|---|
| Social skill | **Format 10 → 11**, `BackfillSkills` (idempotent, draws in skill order so the first nine rolls are unchanged) | **Yes — P3, once** |
| Warden work type | Priorities array grows by one | **Yes — same bake, P3** |
| `TryClaimForSleeper` counting only colonists (P1) | Behaviour | Possibly, on boards with animals. Measured in P1; if it moves them, it lands in P3's bake |
| `Pawn.Custody` | Pawn word bits 28–29, zero while Free | No |
| `odyssey.prison`, `odyssey.bedpurpose` | New sections, appended to `ColonyWorld.SaveComponents` after `main`'s part-mined section, a layout `int` first, hashed only while non-empty | No |
| Job defs 28–35 | Appended | No |

**Every golden move is measured with `GoldenColonyProbe`** against `main`. P3 is the only unit that
should re-bake.

## 13. Later units and seams

- **Execute.** A warden job on the prison tab. Colony mood penalty; a goodwill cost once factions
  exist.
- **Prison labour** (Prison Architect's trust tiers, a-20 §6):
  - prisoners join the job system under a rule that keeps their jobs inside their cell or under
    guard;
  - **layer-aware from its first commit** — a-16 records a multi-floor mod where exactly this broke.
- **Ransom.** `PrisonMode.Ransom` is reserved and refused (`NotBuilt`) until M7's factions give a
  prisoner somewhere to be ransomed to.
- **Kidnap** (design 33 §17f) is this design's mirror: a colonist held by raiders. It should share
  `PrisonRecord`'s shape when built.
- **Raiders freeing captured comrades.** Recorded, not designed.
- **A colony-wide "capture every downed raider" switch.** Recorded. The per-enemy mark is the first
  build.
- **Room roles** (a-05). `PrisonCells` becomes a query of them.

## 14. Risks, and what not to undo by tidying

- **The haul pass will empty a cell of its food.** Loose food a warden leaves in a cell is a loose
  thing like any other. **P7 must exclude items in a `PrisonCells` room from hauling and from
  colonists' eating**, or the warden and the haulers will carry the same meal back and forth.
- **Colonists walking through a cell hold its door open**, which is an escape factor. That is
  deliberate and is the player's to design around (an airlock, or a route that avoids the cell). Do
  not make the door rule smarter to hide it.
- **A captured raider is bleeding.** Capture is an emergency giver and the doctor treats prisoners
  for that reason. Do not demote either.
- **`BedRules` is the only place a bed is judged.** A sixth copy of the chooser is the fault this
  design exists to prevent.
- **`Allegiance` is the only place a side is judged.** A consumer that reads
  `Content.KindOf(Kind).faction` directly will treat a recruit as a bandit.
- **`Kind` is never rewritten.** See §4a.
- **The fast tier compiles neither Presentation nor Editor.** The picker, the bed toggle, the
  prisoner tab and the outfit each need a Unity run before they are called done.

## 15. As built (P1–P12, 2026-09-26)

Every unit is in, tests first, on the fast tier. **No golden moved after P3's bake**: custody,
bed purposes, the prison section and job handles 28–35 are all hashed only while set, and the new
surrender roll draws nothing unless a free prison bed exists, which no golden has.

### 15a. Where the build departs from §1–§14, and why

| Designed | Built | Why |
|---|---|---|
| A **Prisoner tab** with mode chips (§11b) | The prisoner's facts are **rows on her pane** — mode, willing, joins in, slowed by, shackled, escape risk, because — and **pressing the mode row opens the four modes to choose from** (§16 H4; it cycled them until the second review) | A prisoner's pane is the bandit's bare shape with no tab box. The rows reuse the tile readout's grid and the bed-purpose row's pick affordance, so there is no new layout to measure. Chips are a later look if rows read badly |
| *Ransom* drawn dim among the chips | Never offered (`InspectModel.OfferedModes`); refused by the intent | Nothing to draw it on without chips |
| The ETA **names its warden** | It does not | The warden is not published; `Recruitment.BestWarden` is the arithmetic and the pane shows its result |
| **Arrest** from a right-click on a standing colonist with a drafted colonist selected (§10, §11c) | **A command on the colonist's own pane**; the nearest colonist on her feet who can reach her is sent (`OrderArrest` with `A = 0`) | A right-click that touches a colonist is a **move**, and three tests pin it (design 33 §2f: the hit box covers the cell behind her, and the review found every order just behind the squad doing nothing). A menu there would take that away |
| `ui.alert.prisonerescape` **cycles** every escapee on click | **One row per escapee**, Danger, each jumping to her | The alert panel's per-pawn rows already do it; a cycling row is a new control |
| A surrendering raider **leaves the band** (§10) | She **stays on the band's roll**, counted out of the fight | The same rule P6 set for capture (`RaidSystem.InTheFight`): a band that lost her to a cell is a band that lost her, and the withdrawal arithmetic is unchanged |
| `LeaveTree` for Released and Exiled | `PawnCustody.Released` walks in **`TraverseMode.Colonist`** (`Allegiance.ModeOf`) | The warden has opened the door; a released bandit in her own mode could not leave the cell at all |
| Release and Exile | **The same walk** for anybody but an arrested colonist, who on Release goes back to work with *Was arrested* | The difference is goodwill, and there are no factions yet (M7) |
| Escape risk in ‰ a day | **Parts per million a day**, shown as *"2.1% a day"* | The √n divisor and the ×0.7 lose too much in per-mille integers |
| — | **`ToMyCellThinkNode`**, second in the held tree: a prisoner on her feet who owns a cell bed and stands outside that cell walks there in a colonist's mode for the one job (`Job_GoToCell`), and is dressed on arrival | What makes surrender and a quiet arrest need no carry. It also returns anybody who strayed |
| — | **`FightBackThinkNode`**, second in the escape tree: an escapee strikes her arrester while he is beside her and the grudge lasts (2,500 ticks) | §6's "retaliate against the arrester", as a node |
| — | **Debug → Spawn → Break out nearest prisoner** (`DebugImprison` B = 2) | Escape is a 2 %-a-day roll; nobody could see it otherwise |
| The chat giver | Named **`RecruitWorkGiver`** ("Recruit"), and the escort's **`ReleaseWorkGiver`** | Givers of one work type are scanned by type name; a hungry prisoner (FeedPrisoner) must come first |

### 15b. The one-owner table, as built

| Question | Owner |
|---|---|
| Which bed may this pawn use or own | `BedRule` (contracts) / `BedRules` (Sim) |
| Whose side is she on, how does she walk | `Allegiance` |
| Taking her into custody | `CustodyRules.Take` (via `JobSystem.TakeIntoCustody`) |
| How fast she is talked round | `Recruitment.Factors` — the chat and the pane |
| How likely she is to break out | `EscapeRisk.Odds` — the hourly roll and the pane; `EscapeTests.ThePublishedRiskIsExactlyWhatIsRolled` with a doubled-threshold negative control |
| Letting her go | `PrisonRelease.Let` |
| Leaving the board | `PrisonExit.Leave` (escape and release both) |
| Whether a raider yields | `Surrender.Consider`, called by `CombatSystem.Hurt`, the one owner of damage |
| Whether an arrest is resisted | `Arrest.Contact` |

### 15c. A fault on `main` the escape found

A broken cell door did not open the cell. `EnclosureGrid.FillLayer` cut a flood short at the room
limit and left its queued frontier marked visited, and the next region's flood treated those cells
as walls. The fill now flags the limit and carries on (`docs/bug-patterns.md`, 2026-09-26). No golden
moved. It is temperature's code, and every room on `main` is judged by it.

### 15d. The review (2026-09-26)

A high-effort review of the whole line found ten faults, and each was confirmed in the code before
it was fixed. Every fix below has a test that fails without it and passes with it, except §15d-6,
which is a guard against a case no board here can build. No golden moved.

| # | Fault | Fix |
|---|---|---|
| 1 | **A prisoner freshly taken hashed differently after a load.** `Take` leaves an empty record; the load drops an empty record; the hash counted it. The pane then showed Hold and 0 % after a load too, because both the publish gate and the pane asked for a record | An empty record is no record to the hash; the pane and its publish gate ask custody (`CustodyTests.AFreshlyTakenPrisonerSurvivesASaveToTheSameHash`) |
| 2 | **A captured raider kept her weapon**, and would have drawn it the hour she broke out | `CustodyRules.Take` puts it down, so every way in disarms (`CaptureTests.ACapturedRaiderIsDisarmed`) |
| 3 | **A colonist ordered to attack went on striking a raider who had surrendered** | Both attack drivers end on a held target, as on a downed one (`PrisonFateTests.ASurrenderEndsTheAttackOnHer`) |
| 4 | **Marking an unowned bed left the colonist asleep in it** | The owner sweep raises the wake flag whenever the purposes moved (`PrisonerMindTests.MarkingAnUnownedBedWakesTheColonistInIt`) |
| 5 | **A surrendered raider walking to her cell was drawn in the colony's suit**, and a colonist resisting arrest would have been drawn as a bandit; a leased figure never changed clothes | Held but undressed, she wears what her kind came in; a figure whose outfit moves is repainted or re-leased (`PrisonerOutfitTests`) |
| 6 | **A prisoner woken in the wrong bed was put back to sleep by the colonist chooser**, whose fallback is the colony's fireside | `ResumeSleep` is for colonists; a prisoner's own tree picks her bed |
| 7 | **An escapee lost her bed to the first owner sweep**, which runs whenever rooms change, such as when she breaks her door | An escapee sleeps from the prisoners' pool (`EscapeTests.AnEscapeeKeepsHerBedWhenTheRoomsChange`) |
| 8 | **A bed raised inside a cell turned back into a colony bed when the cell opened** | A bed raised in a cell is marked at the raise (`BedPurposes.Raised`, `EscapeTests.ABedBuiltInACellStaysAPrisonBedWhenTheDoorGoes`) |
| 9 | **A pawn let go got a colonist's full pane**, whose commands the simulation refuses | Every custody other than Free gets the bare pane; a released pawn's one row says *let go* |
| 10 | **Capture's bed choice was a copy of the rescue's** (bug-patterns P1) | `RescueRules.BedFor` takes the pool to judge from; the rescue driver's `StillFree` asks a virtual `UserFor` |

### 15e. Still owed

- ~~**Unity.**~~ Compiled and run on the merge (§16).
- ~~**The Arrest button is enabled without a free prison bed.**~~ `WorldSnapshot.PrisonBedFree` (§16 H2).
- **An arrest ends in downs** (§10): asserted by nothing.
- **How long a door holds an unarmed escapee** is the building attack's arithmetic, unmeasured at
  play; `EscapeTests.AnEscapeeBreaksTheDoorRunsAndIsGone` proves only that it ends.
- **Every number is a proposal**: the recruitment gain, the escape factors, surrender's quarter,
  arrest's 5–60 %. The owner confirms them at the first play.

## 16. The second review, on the merge with `main` (2026-09-26)

The branch was merged with `main` first (wake, frog, First Person, cracks): sixteen conflicts, all
two appends at one place. The colonist pane's commands became Draft, the response, **Arrest, then
First Person last**, as First Person's own comment asks. Design 58 was `main`'s by then, so this
document is **59**. The played-board golden was re-baked and measured with `GoldenColonyProbe`: equal
to `main` in every number, and different from this branch only by `main`'s seven frogs.

Three independent passes then read the simulation, the interface, and whether the tests and docs
bear out their claims. Every finding was checked in the code before anything changed, and every fix
has a test in `PrisonReviewTests` (Sim) or the Hud tests that failed on the code before it, unless
the row says otherwise.

### Simulation

| # | Fault | Fix |
|---|---|---|
| 1 | **A load swept the beds as if their purposes had changed.** The sweep's memory is not saved, so the first tick after a load raised `BedOwnershipChanged` and woke a sleeper the twin that never saved left asleep | `BedPurposes.Loaded` primes the sweep instead (`ALoadDoesNotSweepTheBedsAsIfTheirPurposesHadChanged`) |
| 2 | **A prisoner's meal and sleep were walked as a colonist's**, who opens doors, so two doors on one corridor were a way out and in and raised her own risk | Her own mode (`APrisonersMealAndSleepAreWalkedInHerOwnMode`) |
| 3 | **A bleeding prisoner stood untended until she dropped**: a doctor goes only to somebody lying still, and her tree had no patient | `PrisonerPatientThinkNode`; the patient driver's self-treatment exit is a colonist's alone (`ABleedingPrisonerLiesDownAndIsTreatedBeforeSheDrops`) |
| 4 | **An arrest took her with no bed left** when the arrester arrived | The bed is asked again at the touch (`AnArrestWithNoBedLeftWhenHeArrivesTakesNobody`) |
| 5 | **A recruit's corpse was a bandit's**: the corpse read its side off her kind | `Corpse.Joined`, carried above the facing's three bits in the int it was always saved as, so no format bump and no golden moved (`ARecruitDiesOnTheColonysSide`; it reads the new field, so it cannot run on the old code) |
| 6 | **A shackled prisoner in a gated yard** was sent to her bed in a mode the gate holds, every think | Walked as `ToMyCell` walks her into a cell (`AShackledPrisonerIsWalkedThroughTheGateToHerBed`) |
| 7 | **The publish walked every pawn once per prisoner**, twice: the prisoner count and the best warden | Found once a publish (`PrisonAspects.Shared`). Values unchanged by construction; not timed |
| 8 | **The feed finished for a prisoner who had walked off** mid-meal | The feed follows her with the plate (`AWardenFeedsAPrisonerOnlyAtHerSide`) |
| 9 | Stale comments: the drivers "stubs"; a summary on the wrong method | Corrected |
| 10 | **Found writing #4's test: the arrester followed a walking colonist at half pace** and never caught her in 4,000 ticks; a new place beside her every tick snapped his step back | One rule, `PrisonerFollow.StandFor`, for every job that goes to her side: choose again only at a step boundary, as the melee chase does. 2,261 ticks now (`AnArresterCatchesAColonistWhoWalksOn`; `docs/bug-patterns.md`) |

### Interface

| # | Fault | Fix |
|---|---|---|
| H1 | **Four labelled buttons in the colonist's header**, where design 57 §6 took the fourth out because it runs her name under them | Measured (below), and **the owner's ruling: Arrest is a right-click row** on a standing colonist while nobody selected is drafted (`ContextMenuModel.OfferArrest`, `ArrestMenuTests`); the header is `main`'s three again |
| H2 | **Arrest was always live** and a refused press did nothing | The row is dim with its reason — no free prison bed, nobody else on their feet — from the new `WorldSnapshot.PrisonBedFree` (`ArrestMenuTests.ArrestIsDimWithItsReasonWhenItWouldBeRefused`, `WhetherAPrisonBedStandsFreeIsPublished`) |
| H3 | **Capture was offered on a prisoner lying in her own bed**, refused in silence, and a drafted right-click on her cell opened the menu instead of moving | A sparse `stray` aspect from `CaptureRules.WantsCapture`, the order's own rule (`APrisonerInHerBedIsOfferedNothingAndTheClickIsAMove`) |
| H4 | **The mode row cycled Hold → Recruit → Release → Exile**, so going back to Hold passed through Release and Exile while the game ran | A popover of the four to choose from, the bed owner picker's (`InspectModel.OfferedModes`) |
| H5 | **Willingness showed 0 % on Hold** where nothing could move it | Shown in Recruit, or once a warden has made a start (`AHoldPrisonerNobodyHasTalkedToShowsNoWillingness`) |
| H6 | **A pawn let go was still labelled Prisoner** | *Let go*, `ui.pawn.released` (`APawnLetGoIsNotCalledAPrisoner`) |
| H7 | A stale outfit comment | Corrected |
| H8 | **First Person kept riding a colonist once she was arrested** | A ride keeps a colonist only; she is lost to it as if gone (`TheRideEndsWhenSheIsTakenIntoCustody`) |

The interface's copy of the prison aspect names and bits is held to the simulation's by a pair of
tests, as `CombatAspectNamesTests` does for combat: "the risk shown is the risk rolled" stopped at
the simulation until then.

### Unity

The fast tier compiles neither Presentation nor Editor. Unity's own NUnit also refused a
`Does.Not.Contain` on an int in `PrisonBedTests`, which had never been compiled there (`docs/lessons.md`,
the two tiers' NUnit).

### H1, the header, measured (1920 × 1080, `DockedTabGeometryTests.TheLongestNameStillClearsTheColonistsButtons`)

| Control | Width + margin |
|---|---|
| Draft | 74 + 4 |
| Fight back | 105 + 4 |
| **Arrest** | **80 + 4** |
| First Person | 113 + 4 |
| Almanac, Close | 26 + 4 each |

The six take 448 of a 560 pane whose titles start 82 in, leaving **17 px** for the name line; the
longest given name, *Charlotte*, and *colonist · L6* after it need **173**, the name alone **95**.
**Without Arrest it is 101**: the name clears the buttons by 6 px, and the word after it is 72 short
— **on `main` too**, since First Person's 117 went in (design 57 §6 counted three buttons with
Prioritise gone, not their widths). With the owner's ruling Arrest left the header, and the test
asserts the name clears and logs the word's shortfall, which is the header's own open question.

