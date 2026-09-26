# Trading — plan

**Written 2026-09-26.** Design: `docs/design/65-trading.md`. Interview:
`docs/research/trading-interview.md`. **Approved and built 2026-09-26: T0–T7 on this branch** (design 65 §10 has the as-built notes). Each unit is test-first, has at least one commit of its own, and leaves the fast and
Long tiers green.

Paths are under `Assets/Odyssey/` unless shown otherwise. The line numbers were read on `081302f9`.

## Findings that shaped the units

The code was checked against the surveys before this plan was written.

- **`PawnFlags` is a byte with all eight bits taken** (`Sim.Contracts/Views.cs:74`). A Visitor bit
  means widening it to `ushort`.
- **Job labels are `ui.status.*` keys**, not `ui.job.*`. The negotiator's key is
  `ui.status.trading`.
- **The debug Events tab builds a plain row** for any fireable worker that is not a `RaidWorker`.
  The trader's row needs no Presentation edit.
- **The pause is `OdysseyBootstrap.ClockHeld`** (`:604`, read at `:1305`), and the wake assigns it
  outright. The trade window needs its own `ModalHeld`, ORed with it.
- **Hostiles only ever target colonists** (`CombatThinkNodes.cs:192,202`, `RaidThinkNode.cs:173`,
  `Melee.cs:84`), but their stray shots still land on anyone.
- **`PawnRegistry.Despawn` puts the equipped weapon down.** Theft despawns it explicitly
  (`Theft.cs:193`), and the trader must do the same.
- **Non-colonist people never heal** (`CombatSystem.cs:169`).
- **The HUD would treat a visitor as a colonist**: the colonist pane in `InspectModel.cs:670`,
  `CombatOrders.Route:52`, the roster, Assign, the Work tab and the alerts. **The simulation would
  not**: every `IsColonist` check there reads `Faction == Colony`.
- **`Pawn.Leaving` already exists**, is saved for any pawn and is hashed at bit 16. Pawn-word bits
  28–30 are free.
- **Hard-coded counts:** `CombatContractTests` asserts `ItemHandle.Count == 18` and
  `JobHandle.Count == 28`.

## Units (on `claude/wonderful-clarke-kkd9pq`, one commit or more per unit, test-first)

**T0 — design doc** `docs/design/65-trading.md` (**done 2026-09-26**, with this plan). It holds the decisions above, the price formula,
the stock rule, the harm rule, and the save and hash plan. It also records the process §3 scaling
note: the colony's stock is scanned on publish only while a trader is on the board. Add the
*Read this* row to `CLAUDE.md`, and a journal entry.

**T1 — Gold and market value.**
- `marketValue` on `ItemDef` (`Sim/Pawns/PawnContent.cs:665`), filled in for every item in
  `Defs/Core/Pawns/Items.xml`.
- `Item_Gold` as handle 18: `ItemHandle` (`Sim.Contracts/Catalogue.cs:213`, Count 19), `ItemIndex`
  and the ByName list (`PawnContent.cs:638,1597`), `Hud/ItemLabels.cs`, and
  `Presentation/Rendering/ModuleCatalogue.cs:944`.
- Tests: CombatContractTests count, RegistryTests:368, StorageZoneTests:429, gold stacks to 500,
  every tradeable item has a value above 0.
- What moves: `PawnContentDefTests.ContentFingerprint`. Goldens possibly (`StorageSettingsTable`
  hashes `Allow.Length`), so **measure with `GoldenColonyProbe`** and re-bake only if the probe is
  clean.
- Rename the CSV row to gold, then run all three content gates.

**T2 — A neutral visitor side** (plumbing only).
- `Faction.Visitor = 3`, and `PawnKind_Trader` appended as kind 6.
- A `VisitorTree` (Downed → `VisitorThinkNode` → Idle), routed at `JobSystem.cs:506`. The node walks
  to `RaidTargets.Resolve` and mills there (lift `Mill` into a shared helper), or heads for the edge
  while `Leaving`.
- Visitors heal (`CombatSystem.cs:169`).
- Widen `PawnFlags` from byte to `ushort` and add a Visitor bit (`Views.cs:74`, set at
  `PawnRegistry.cs:589`). `PawnView.IsColonist` excludes it, and `IsVisitor` is added.
- Hud: `PawnOutfit.Trader` with its branch in `ColonistAppearance.Of`, a `PawnKindLabels` key, a
  visitor branch in `InspectModel.cs:670` (no colonist tabs, no Draft), and a debug **Spawn** row.
- Tests: neither colonist nor hostile; no needs tick; the visitor mind is used; absent from the
  roster, Assign, Work tab and alerts; no Tend row; raiders never target it.

**T3 — The trader incident and the visit.**
- Extract the edge choice out of `RaidWorker.TryExecute` into `EdgeArrival`, with no change in
  behaviour: the raid tests pass unedited.
- `TraderWorker`, a `TraderParams` block on `IncidentDef`, and `Incident_Trader` appended to
  `IncidentContent.Order`, `IncidentHandle` and `IncidentLabels`. The debug row appears
  automatically.
- `Sim/Trade/TradeSystem` (Pawns phase) holds the visit, sets `Leaving` after the stay, and
  despawns the trader at the edge together with their weapon (the weapon step copies
  `Theft.cs:193`). It is saved as `odyssey.trade` and hashed only while a visit exists. Wire it in
  `ColonyComposition.cs:167-192` and `ColonyWorld.cs:118`.
- Tests: arrives at an edge and reaches the hearth, or the colony start if there is no hearth;
  leaves on time with no weapon left behind; save/load mid-visit gives the same hash; no visit
  hashes as before.
- **Playtest checkpoint 1:** a trader walks in, waits and walks out.

**T4 — The trade core in the Sim** (no UI).
- `TraderKindDef` holds the stock rows, purse range, the ×1400‰ and ×600‰ gap, and the stay.
- `TradePricing` is the one owner of prices, guarded by a test the same way `HopCost` is.
- `ColonyTradeStock` counts `StoredItems + ContainedItems` plus loose stacks in the radius, and
  takes from the smallest stacks first.
- `TradeDrops` plans every cell before changing anything, splits stacks by stack limit, rolls weapon
  quality, and obeys `ColonyItems.CellHasSpace`.
- Intents, appended and all on `PausedIntents.AppliesWhilePaused`:
  - `TradeLine(trader, def, signedQty)`: buffered and never saved.
  - `TradeCommit(trader, expectedBalance)`: validates everything or rejects the whole deal.
- Snapshot: a `TradeView` (trader, negotiator, ready, purse, colony gold, leaves-at) and per-def
  `TradeRowView` rows (colony qty, trader qty, buy, sell).
- Tests: overdrawing the purse, gold, stock or goods rejects the whole deal; a mismatched balance
  rejects; gold is conserved; a hauler's load is never taken; drops avoid trees and beds; stock and
  purse survive a save.

**T5 — The negotiator.**
- `OrderTrade(negotiator, trader)` goes in `JobSystem.Trade.cs`, modelled on `JobSystem.Tend.cs:18`.
  `Job_Trade` is job 28: bump `JobHandle.Count` and the CombatContractTests count.
- The negotiator walks beside the trader, then opens a session: `Ready` is published, the trader
  holds still, and the stay clock pauses.
- `TradeCancel` ends the session, as does drafting, downing or reordering the negotiator, or the
  trader leaving.
- Hud: `ContextMenuModel.OfferTrade` for a visitor under the pointer (`ui.command.trade`), and a
  `JobLabels` entry `ui.status.trading`.

**T6 — The trade window.**
- `Hud/TradeModel`: local signed quantities, clamped to stock, goods, purse and gold, with a running
  balance and Confirm's disabled reasons. It emits N `TradeLine` plus `TradeCommit`; Reset and
  Cancel are local, and Cancel sends `TradeCancel`. It opens once per session.
- `Presentation/Ui/HudShell.Trade.cs`: a `Modal(...)` with the `BillList` stepper (Shift ×10), laid
  out to B10.
- `EscapeAction.CloseTrade`.
- A `ModalHeld` clock gate ORed with `OdysseyBootstrap.ClockHeld` (`:604`, `:1305`); the wake
  assigns that flag outright, so trade must not share it.
- `ui.trade.*` keys, and a `TradeDirector.IconKeys` RegistryTests case.
- Tests: clamping, balance sign, intent order, and that the panel is wide enough for its own padding
  and border (the pattern from the Work tab).
- **Playtest checkpoint 2:** the full trade.

**T7 — Harm.**
- A `VisitorHarmListener` on `CombatHooks`, in the style of `FriendlyFireListener`.
- **Deliberate:** a colonist whose `CombatTarget` is the trader, reached only through a Ctrl-attack
  order (`CombatOrders.Route:52`). The trader then carries `FactionOverride = Hostile`, saved in
  `odyssey.trade` and hashed at pawn-word bit 28 only while set. The outfit is kept.
- **Anything else** (strays, raiders, falls): cancel the session and leave at once.

## Verification

- `scripts/test-fast.sh`, and the Long tier (`--filter TestCategory=Long`), after every unit.
- Content gates on every content commit: `build_wiki.py --check`, `emit_labels.py --check`,
  `icons.py validate`.
- `GoldenColonyProbe` against `main` wherever a golden moves.
- The fast tier cannot compile Presentation, so T2, T5 and T6 are **unproven until the owner's
  Unity tier runs**: `scripts/unity.sh test editmode` and `playmode`. The same goes for the player
  build smoke test with `-odyssey-newgame`.
- The end of the work adds rows to `docs/plans/playtest-queue.md`, republishes the wiki artifact,
  and ends with the CLAUDE.md handover (folder, *What changed*, *What to test*).
- Commit and push to `claude/wonderful-clarke-kkd9pq`. **No PR unless asked.**
