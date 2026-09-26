# Trading: owner interview

**Phase:** interview, at the feature level, in the shape of `cover-interview.md`.
**Date:** 2026-09-26.
**Branch:** `claude/wonderful-clarke-kkd9pq`. Documents only; no code was written before this file or
under it.
**Conducted by:** Claude Code. Eight questions in two rounds, asked after three read-only surveys:
- trade content and items (the registry, the design documents, `ItemDef`);
- visitor arrival (incidents, raids, wildlife, sides, the hearth);
- the right-click menu and the panels.

A design pass then checked the surveys against the code.

**The owner's brief:** *"We need to plan out a trading system similar to this but to fit in with
Odyssey's content. We should look to use gold as currency - but we can sort this out later.
https://rimworldwiki.com/wiki/Trade This has the idea but we could have an event at first that can
be spawned. We'll decide on the exact charatcers later - so a random character could walk into the
map from the edge (could be a few people and some other animals later etc). He 'll wander to the
hearth / campfire and you can right click and select trade. Can we get a decent MVP to allow this to
happen, ask all the questions about the mechanisms and fill in any gaps."*

**Read next:**
- `docs/design/65-trading.md`, the design these answers decide.
- `docs/plans/trading.md`

**The reference page could not be read.** `rimworldwiki.com` is blocked by this environment's
network policy. The reference's mechanics below come from `a-11-storyteller-incidents.md`,
`a-14-bills-stockpiles-inventory.md` and general knowledge of the game, and are marked unverified.
Nothing was copied from it.

## 1. What the surveys found, put to the owner before the questions

- **Nothing for trade exists in code.** `ItemDef` has no value field, and there is no currency item
  and no faction, trader or visitor class. `RaidWorker.cs:71` says *"nothing in the game has a value
  to total yet"*.
- **The registry already names the pieces**, all unused:
  - `ui.command.trade` *Trade* ("Open the ledger with this trader")
  - `ui.tab.trade`
  - `ui.bulletin.trader` ("A trader has come to us")
  - `ui.res.credit` *Credit chit*, with coin art already mapped
  - `ui.occupation.market-trader`

  The proper-nouns list proposes `faction.traders` *the Cartage* (a hauliers' combine) and
  `creature.pack` *dray hog*.
- **Panel B10** (`10-ui-panel-catalogue.md:548`) already specifies the ledger: two columns, a
  transfer control per row and a running balance at the foot.
- **A trader is the raid's shape with a neutral side** (design 23 §8). The following are all
  reusable:
  - edge arrival (`RaidWorker`)
  - the hearth lookup (`RaidTargets.Resolve`)
  - milling (`RaidThinkNode.Mill`)
  - leaving and despawning at an edge (`EdgeTarget.Find`, `Pawn.Leaving`, `PawnRegistry.Despawn`)
- **There is no neutral side.** `Faction` is Colony, Wild or Hostile. A non-hostile person would get
  the colonists' mind and do the colony's work. `PawnView.IsColonist` would put them on the roster,
  the Assign tab and the right-click menu.
- **The right-click menu only listens while a colonist is selected** (`OrderModel.HearsRightClick`),
  and a row carries intents, never a UI action.

## 2. Round 1

| Question | Options put | Answer |
|---|---|---|
| How does gold exist? | **a real item** (rec.) · an abstract balance · reuse *Credit chit* | **A real item** |
| How do goods change hands? | **sell from stores, buy at the trader** (rec.) · colonists carry both ways · instant both ways | **Sell from stores, buy at the trader** |
| What does *Trade* on the right-click do? | **a colonist walks over first** (rec.) · the window opens at once | **The colonist walks over first** |
| What is the window? | **a pausing modal built to B10** (rec.) · a Claude Design mockup first · docked and not pausing | **A pausing modal built to B10** |

## 3. Round 2

| Question | Options put | Answer |
|---|---|---|
| How are prices set? | **base value plus a fixed gap** (rec.) · add a Social skill now · flat, no gap | **Base value plus a fixed gap** |
| What does the trader bring? | **a limited purse and rolled stock** (rec.) · unlimited gold and everything · buys only | **A limited purse and rolled stock** |
| How long does the trader stay? | **about a game day** (rec.) · until dismissed · leaves after one trade | **About a game day** |
| What if the trader is hurt? | **takes damage and leaves** (rec.) · cannot be harmed · turns hostile | ***"1 and 3 depending"*** |

**How the harm answer is read.** Accidental harm (a stray shot, a raider, friendly fire, a fall)
makes the trader leave at once. A **deliberate** attack, where a colonist is ordered to attack the
trader, turns them hostile. This reading is recorded in design 65 §6 for the owner to confirm.

## 4. Gaps filled without asking

Each of these is recorded in design 65 with its reason, so it can be overruled:
- the currency key (rename `ui.res.credit` to `ui.res.gold`)
- a base value for every item
- the rounding
- the trade radius for loose goods
- the purse and stock sizes
- the stay clock pausing during a negotiation
- a downed trader healing
- a raid sending the trader away
- the trader's weapon leaving with them
