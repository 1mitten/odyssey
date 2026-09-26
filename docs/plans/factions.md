# Factions: the plan

The design is `docs/design/61-factions.md`; the interview is `docs/research/factions-interview.md`.
The branch for the documents is `claude/lucid-euler-9puelu`. Each unit below gets a branch of its
own once the plan is approved: one commit per unit, one PR per unit.

**Approved by the owner 2026-09-26** (*"plan approved - we'll sort out the orcs later"*), so F3 and F4 wait. **F0 is built** (design 61 §5a), stacked on the prisoner line.

| Unit | What | Gate |
|---|---|---|
| **FA0** | Design 61, this plan, the interview, `a-13`, the proper-noun rows, the journal and the status line | docs only; the wiki and icon checks |
| **F0** | One owner of "is this an enemy?". `Allegiance.AreHostile` / `IsThreat` are defined to give today's answers, and every call site in design 61 §5 moves onto them | fast and Long tiers; **every golden identical** (`GoldenColonyProbe`, three boards); `AllegianceConsumerTests` extended; a one-owner test that reads the sources |
| **F1** | Covers: <ul><li>`FactionDef` and `Factions.xml` (Colony, Wild, Bandits, Orc Army, Cartage, Kindred);</li><li>the rename `Faction` → `Stance` (plus `Neutral`);</li><li>`PawnKindDef.faction` as a Def reference;</li><li>the `odyssey.factions` section (goodwill, stored relation, reasons ring, override, world list), hashed only where it differs from a new world;</li><li>`ChangeGoodwill` as the one writer; daily drift; the static `hostileTo` matrix;</li><li>parley, as a group flag (the seam only);</li><li>the trade line's API (§6);</li><li>the settlement name pool (`settlement-names.csv` → `emit_labels.py` → `SettlementNames.g.cs` and the wiki);</li><li>the **Factions tab on F8** (the command bar's dead item goes live), briefed in `docs/reference/mockups/factions-tab-brief.md`;</li><li>debug: goodwill ±10 per faction</li></ul> | fast tier; a save round-trip; a lockstep twin; tests for the hysteresis, drift and one-writer rules; `RegistryTests`; both `--check` gates and `icons.py validate`; goldens measured; Unity owed for the tab |
| **F2** | **The collector**, covering: <ul><li>`value` on every item Def (fingerprinted);</li><li>`Incident_Collector` and `CollectorWorker`;</li><li>`CollectionSystem` (arrive under parley → demand → pay / refuse → leave; saved, hashed only while a party exists);</li><li>the tribute spot as a filtered top-rung storage zone;</li><li>the Pay / Refuse prompt (the `LeavePrompt` pattern, pausing);</li><li>the deadline and the punitive raid through `RaidWorker`;</li><li>paying late calls it off;</li><li>striking a parleying pawn costs −50</li></ul> | fast tier: the same stores give the same demand; the refusal → deadline → raid timeline; paying late cancels; partial payment is pro rata; no golden moves while no party exists. Unity owed for the prompt. **Playtest** |
| **F3** | **The Orc Army** as a raid faction: its kinds appended after kind 6, `RaidMix_Horde`, tougher melee numbers, a weight of 2 in the auto size, hostile to the Bandits | the raid gate's seeds with a horde; **a bandit–horde fight on one map**; the tick at 200 raiders against design 55 §11a |
| **F4** | The Orc Army's look, from the owner's Synty pack: import, `MeasureSole`, the far form, the figure test asking whether the art resolved | Unity tiers; a player build; blocked on the owner naming the pack |
| Later (M7) | Visitors and refugees (the Kindred); ransom and a released prisoner going home (design 58's seams); gifts; aid through the comms console; a faction's buildings; leaders; the Sump if chosen; a storyteller choosing factions | — |

**Merge order.**
1. **The prisoner line (design 58) first.** It creates `Allegiance`, which F0 extends, and F0 edits
   the same call sites.
2. Then F0 → F1 → F2 → F3.
3. The trade line can land before F1 by appending `Faction.Neutral` (design 61 §6). F1 then maps
   that onto the Cartage.

**Owed on the owner's machine** for F1 onwards:
- the Unity EditMode and PlayMode tiers, because the fast tier compiles neither Presentation nor
  Editor;
- a player build;
- a play, following each unit's handover table.

**Open with the owner** (design 61 §3, §3a, interview §6):
- Which Synty pack has the orcs.
- What "something else" is. The design recommends the Sump, arriving from below.
- Whether the bandits keep *the Tithe* as a proper name, or it becomes the word for their demand.
