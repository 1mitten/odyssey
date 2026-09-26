# Deep mining: owner interview

**Phase:** interview, at the feature level, in the shape of `mining-interview.md` and
`cover-interview.md`.
**Date:** 2026-09-26.
**Branch:** `claude/intelligent-davinci-rbnjm7` (documents only; no code was written before this file
or under it).
**Conducted by:** Claude Code. Sixteen questions in five rounds, asked after three read-only
explorations (the mining code, what depth costs, and the earlier mining research) and while three
capped research subagents ran (`a-12-ore-by-depth.md`, `a-04-cave-ins-and-prospecting.md`, and a
code-seams check whose findings are in design 62 §3).

**The owner's brief:** *"We need to make the mining more comprenhensive but allow it to be performant -
can we have more depth in the world but maybe only process it when necessary or have a cut off into
"deeper mining" which is a deeper height that gets processed at that point. Research what is good
practice a good idea. We should look at having "Ore", "Gold", "Coal" and things like that - if we
could even have caverned mines but maybe that comes later - how do we make a mining experience work
here ? research, plan and see what is reasonable - ensure it's performant. Interview me and ask me
questions"*

**Read next:**
- `docs/design/62-deep-mining.md` — the design these answers decide.
- `docs/plans/deep-mining.md`
- `docs/research/a-12-ore-by-depth.md`, `docs/research/a-04-cave-ins-and-prospecting.md`
- `docs/research/mining-interview.md` — the 2026-09-16 answers this builds on. None of them is
  reversed except the board depth (16 → 32) and "mining causes no collapse".

## 1. What the exploration found, put to the owner before the questions

- **Mining already exists**: stone, iron ore (3–13 below the surface) and coal (7–22), 3–5 small
  sealed caverns with ore on their walls, ore hidden until a face is cut (`CellFlags.Discovered`),
  bedrock unmineable.
- **Missing**: ore has no use (nothing smelts it); there is no fog, so a sealed cavern is visible by
  scrolling down; undercut rock never collapses; and **a hauler cannot climb a ladder**, so nothing
  mined deep can be carried up until stairs (U44) exist.
- **Depth mostly costs memory.** `28-map-size.md` §11: 16 → 24 layers is +48 % memory with the tick
  and the frame in noise. The one per-edit cost that grows is `NavGraph.Rebuild`, because every
  solid 10 × 10 block still allocates an `Impassable` region that no consumer uses.
- **Unloading deep layers was considered and rejected** (§8.5): a colony sim cannot unload state. So
  "only process it when necessary" means *never touch rock nobody has opened*, not switching layers
  off.

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | How should "deeper mining" be structured? (one deeper board / a separate deep level generated on first breach / a deep drill building / board now and drill later) | **One deeper board**, with unopened rock made nearly free to process |
| 2 | Which minerals beyond iron ore and coal? (multi-select) | **Copper, gold, gems / crystal, and a sci-fi mineral** — all four |
| 3 | What is ore for in this build? (smelter chain / raw piles / build straight from ore) | **A smelter chain** — ore and fuel make bars |
| 4 | How much does the player know about the underground? (fog + prospecting / fog only / as now) | **Fog + prospecting** |
| 5 | How does ore come up? (stairs then a lift / a lift first / haulers climb ladders) | **Stairs first (U44), a mine lift later** |
| 6 | How deep is a board? (32 / 24 / 40 / by map size) | **32 layers** |
| 7 | What are caverns in this build? (larger deep caves / keep small / caves with creatures) | **Larger deep cave systems now**, no creatures |
| 8 | What risks does mining carry? (multi-select: cave-ins / harder with depth / heat at depth / none) | **Cave-ins** and **harder with depth** |
| 9 | How do you prospect? (a job now and a scanner later / scanner only / job only) | **A prospecting job now, a powered scanner later** |
| 10 | How is unknown underground drawn? (as plain rock / dark) | **As plain rock** — caverns and ore simply are not there until exposed or prospected |
| 11 | What does a cave-in do to anyone under it? (warn then hurt / can kill / rubble only) | **Warn first, then hurt**: it can down, never kills (the rule fights follow) |
| 12 | Grass can be mined today; guard it? (keep with a label and a drag rule / keep and yield soil / guard it off / leave) | **Keep it diggable**; soft ground reads **Dig** rather than Mine, and a Mine drag that **starts on rock marks only rock** |
| 13 | What do the new minerals feed? (iron and copper used, the rest stored / one use each / all stored) | **Iron and copper bars are used** in building and power; **gold, gems and the sci-fi mineral are stored**, uses recorded |
| 14 | What does the smelter burn? (coal / electric / coal or wood) | **Coal or wood**, coal the better |
| 15 | What holds a mine roof up? (rock pillars and a mine prop / rock and walls only) | **Uncut rock, walls, and a buildable mine prop**; unsafe cells tinted before they fail; a safe span about 6 |
| 16 | The deepest mineral's name | **Emberquartz** |

## 3. Notes on the answers

- **Q12 came from the owner mid-interview**, not from the question list: *"Grass can be mined atm -
  should we guard that for these mine deposits - until we could decide to add a grass layer ? what do
  you recommend"*. The recommendation given: a grass tile is a whole 3 m block of soil with grass on
  its top face, ore only ever replaces rock (`OrePass.GrowBlob`) at least three layers under the
  surface below two layers of subsoil, so no deposit can be a grass tile; and digging soft ground is
  the only way to sink a shaft on flat meadow. The drag rule is decided **once, from the drag's start
  cell** — per cell would be `docs/bug-patterns.md` P4.
- **Q2 and Q13 together** mean gold, gems and Emberquartz are found, mined, hauled and stored with
  nothing yet consuming them. That is accepted; trade and research are where they go.
- **Q8 did not choose heat at depth.** The deep ground still holds the annual mean (design 28).
- **The name "Emberquartz"** is ours; it glows when discovered (design 62 §5). The deep stone has a
  working name only and waits for the owner's name in the wiki.
