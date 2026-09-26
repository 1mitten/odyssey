# 57 — Trading

**Written 2026-09-26**, from the owner's request and a two-round interview the same day
(`docs/research/trading-interview.md`). Branch `claude/wonderful-clarke-kkd9pq`. Plan:
`docs/plans/trading.md`. **Designed; nothing built.** Phase gate: the plan waits for approval.

> *"We need to plan out a trading system similar to this but to fit in with Odyssey's content. We
> should look to use gold as currency … a random character could walk into the map from the edge
> … He'll wander to the hearth / campfire and you can right click and select trade."* — the owner,
> 2026-09-26

## 1. What trading is, in the MVP

A **visit** is one person who walks in from a map edge, waits by the hearth for about a game day,
and walks out again. A colonist the player sends to them opens a **session**. The session opens a
**ledger** in which the player moves goods and gold across, and a confirmed deal changes hands
**all at once or not at all**.

It is pulled forward from M7 (brief §94, systems catalogue 13) as a thin slice. The names are ours
(clean room): `TradeSystem`, `TraderKindDef`, `TradePricing`, `ColonyTradeStock`, `TradeDrops`,
`TradeView`, `TradeModel`, `VisitorThinkNode`.

**Reused, not rebuilt:**
- the incident door and the debug Events tab (design 23);
- the raid's edge choice, extracted as `EdgeArrival` (design 55);
- the hearth lookup `RaidTargets.Resolve` and milling (design 55, 43-home-area);
- leaving by an edge: `EdgeTarget.Find`, `Pawn.Leaving`, `PawnRegistry.Despawn` (design 30);
- the ordered-job template `HandleOrderTend` (design 37);
- the right-click menu `ContextMenuModel` (design 33 §7a);
- the outfit layer `PawnOutfit` (design 42);
- the modal and the bill stepper (designs 39, 49);
- the storage counts the Inventory tab makes (design 35).

## 2. Currency: gold is a thing

**Gold is an item** (`Item_Gold`, handle 18, stack 500, category *Items*, value 1). It is stored,
hauled and filtered like any commodity, and thieves and raids can take it once they want things.
The reference does the same with its silver (a-14: stack 500).

**Alternatives weighed and rejected:**
- **An abstract balance** on the HUD. Nothing could store, carry or steal it, which cuts against
  every other commodity in the game.
- **A second currency key.** The registry's `ui.res.credit` *Credit chit* already had coin art and
  no reader, so it is **renamed to `ui.res.gold` "Gold"** rather than joined by a second row. The
  owner may still rename it (*"we can sort this out later"*); the name is one CSV cell.

Gold is **never a row** in the ledger. It is the balance.

## 3. Value and price

`ItemDef.marketValue` is new: a whole number of gold for one unit. It is also the first brick of
the wealth measure that `RaidWorker` is waiting for.

| Item | Value | Item | Value |
|---|---|---|---|
| wood, stone, carrots, berries, mushrooms | 1 | ration pack | 6 |
| iron ore, coal, burnt meal | 2 | vegetable meal / cooked meal | 10 / 12 |
| scrap metal | 3 | medical supplies | 20 |
| bat / crowbar / machete | 25 / 30 / 45 | arc blade / pistol | 120 / 150 |

**One owner of price: `TradePricing`.** It works in per-mille integers, so the price is
deterministic and the same on every surface:

```
buy  (trader pays the colony) = max(1,        floor(value × buyPerMille  × negotiator / 10^6))
sell (colony pays the trader) = max(buy + 1,  ceil (value × sellPerMille / negotiator))
```

- `buyPerMille` is 600 and `sellPerMille` is 1400, and both live on the `TraderKindDef`.
- `negotiator` is 1000 until a Social skill exists. It is the one multiplier that skill will move.
- Selling above the buy price makes a buy-and-sell-back loop always lose.
- The HUD shows the published price and never computes one. A test holds the Hud to that, in the
  same way `HopPriceHasOneOwnerTests` holds the hop price.

Weapon quality does not move the price in the MVP. A sale takes the lowest-quality weapon first, and
a bought weapon rolls its quality at the drop.

## 4. What the colony can trade

A stack counts toward what the colony can sell or spend if it is:
- **stored** (in a stockpile cell or on a shelf, the same set the Inventory tab counts:
  `StoredItems + ContainedItems`), or
- **loose within `TradeRadius`** (4 cells) of the trader.

**Excluded:** anything carried, anything reserved by a job, and loose stacks anywhere else.

**Why the radius:** without it, the gold paid out by one deal cannot be spent in the next until a
hauler has stored it.

**Removal takes from the smallest stacks first**, so partly used stacks empty before full ones are
split.

**Bought goods, and gold paid to the colony, are set down beside the trader** by `TradeDrops`, and
colonists haul them away. `TradeDrops` plans every cell before anything changes, splits by stack
limit and obeys `ColonyItems.CellHasSpace`, so nothing lands in a tree, a bed or another stack. If
there is not enough room, **the whole deal is refused**.

## 5. The visit

**Arrival.**
- The incident is `Incident_Trader` (bulletin `ui.bulletin.trader`, with the arrival chime). There
  is no storyteller: the debug Events tab fires it.
- `TraderWorker` spawns one trader, pawn kind 6 *Trader*, on an edge chosen by `EdgeArrival`.
- The trader is a rolled person with a trader outfit and a name from the pool, and carries a pistol.
- Groups and pack animals are later work. The visit holds a list of pawns from the start, so a group
  adds rows rather than restructuring anything.

**The mind.** A new side, `Faction.Visitor`, gets its own tree:

```
Downed → VisitorThinkNode → Idle
```

`VisitorThinkNode` walks to `RaidTargets.Resolve`, which gives the hearth or the colony start, and
mills there. While `Leaving`, it heads for the nearest edge instead. A visitor has no needs, does no
work, and cannot be drafted or assigned.

**The stay.** The trader stays for 1 game day. **The clock pauses while a session is open**, so
nobody is cut off mid-deal. The trader then sets `Leaving`, walks out, and despawns at the edge with
their weapon, since `Despawn` alone would leave the gun on the ground.

**The purse and the stock** are rolled at the fire from the `TraderKindDef`:
- a purse of 400–900 gold;
- 4–7 stock lines from a weighted table ("general goods"), each with a count between its min and
  max.

The stock is **abstract counts in the visit**, not items on the board, until something is bought.
Goods sold to the trader join the stock.

**Endings.**
- A **raid** arriving sends the trader away at once.
- A **downed** trader heals at the animal rate and leaves when able. A non-colonist person never
  heals today, so without this they would lie there for ever.
- A **dead** trader leaves a corpse and their stock is lost. Loot comes with factions.

## 6. The session and the ledger

**Starting a session.**
1. With a colonist selected, right-click the trader and choose *Trade* (`ui.command.trade`,
   `ContextMenuModel.OfferTrade`).
2. The colonist walks beside the trader (`Job_Trade`, labelled `ui.status.trading`) and opens a
   session.
3. The visit publishes `Ready`, and the trader holds still.

**What ends a session:** drafting, downing or reordering the negotiator, the trader leaving, and the
window's *Cancel*.

**The window** is a modal over a scrim that **pauses the game** while it is open. It is built to
B10: one row per item either side holds, with these columns:

| Colony qty | Colony-side price | Transfer stepper | Trader price | Trader qty |
|---|---|---|---|---|

- The stepper moves one unit, or ten with Shift.
- The foot shows colony gold, the **running balance**, and the trader's purse.
- The buttons are *Reset*, *Cancel* and *Confirm*.
- *Confirm* is disabled with a reason when the colony cannot pay, the trader cannot pay, or a
  quantity is beyond what either holds.

**Where the numbers live.** The quantities are **local to the HUD** until *Confirm*. Confirm submits
one `TradeLine(trader, def, signedQty)` per row, then `TradeCommit(trader, expectedBalance)`. The
simulation re-validates everything at the commit and applies the deal atomically or rejects it
whole. All three intents apply while paused.

**The pause** is a `ModalHeld` gate ORed with `OdysseyBootstrap.ClockHeld`. The wake assigns
`ClockHeld` outright, so trade must not share that flag.

## 7. Harm (the owner's *"1 and 3 depending"*)

| Harm | Result |
|---|---|
| **Accidental**: a stray shot, a raider, a fall, friendly fire | The session is cancelled and the trader leaves at once |
| **Deliberate**: a colonist ordered to attack the trader (Ctrl-attack; an ordinary right-click never attacks a visitor) | The trader turns hostile and fights back through the hostile tree |

**How turning hostile is stored.** It is a per-pawn `FactionOverride`, saved with the visit and
hashed only while set. The trader keeps their outfit. Raiders only ever target colonists, so they
ignore a trader by construction, but their stray shots can still hit one, which is why the
accidental rule exists. Goodwill, and a faction remembering the attack, come with factions.

## 8. Save, hash, goldens, scaling

- **Save.** New section `odyssey.trade` (the visit, the session and any override). No format bump:
  a save without the section loads with no visit. The HUD's ledger quantities are **not** saved, so
  a load mid-trade reopens an empty ledger. Buffered trade lines never outlive the tick.
- **Hash.** The visit is hashed only while it exists, so a world that never saw a trader hashes as
  before and no golden moves for the visit. **The new item might move goldens**, because
  `StorageSettingsTable` hashes `Allow.Length`. This is to be measured with `GoldenColonyProbe` and
  re-baked only on a clean probe.
- **Scaling (process §3).** The colony's tradeable stock is scanned on publish **only while a trader
  is on the board**, over stored stacks. That is O(stored stacks) for about a day at a time and
  nothing otherwise.

## 9. Left for later, with the seam it plugs into

| Later | Seam |
|---|---|
| Groups and pack animals (*dray hog*) | The visit's pawn list; `SpeciesDef` for the animal |
| Trader types (weapons dealer, food merchant) | Further `TraderKindDef` tables |
| Social skill on price | `negotiator` in `TradePricing` |
| Factions and goodwill (*the Cartage*) | `FactionOverride` becomes a faction id |
| A storyteller firing visits | `Incident_Trader` already has gates |
| Carrying goods to the trader | `TradeDrops` and removal are the only two places that move things |
| A comms console to call a trader | `ui.arch.tool.comms` |
| Gifts | A commit whose balance favours the trader |

## 10. Owed to the owner

- **Confirm the harm reading** in §7.
- **Confirm the currency name**, *Gold*, against the registry's *Credit chit*.
- **Look over the value table** in §3.
- **Decide what a trader looks like**, since the characters were deferred.
