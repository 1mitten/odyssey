# Cover: owner interview

**Phase:** interview, at the feature level, in the shape of `ranged-interview.md` and
`home-area-interview.md`.
**Date:** 2026-09-25.
**Branch:** `claude/cool-darwin-akh02q` (documents only; no code was written before this file or
under it).
**Conducted by:** Claude Code. Twenty-five questions in eight rounds, asked while three read-only
explorations ran: the ranged-combat branch's hit, line of sight, bullet and AI; the building system,
palette and art; and the reference's cover rule (`a-10-cover.md`).

**The owner's brief:** *"We need to understand cover - we should build sandbags and barricades based
on your recommendation. https://rimworldwiki.com/wiki/Cover - can we make it work like this but in
this - explore, research and plan an implementation with some items to build (we can decide on exact
reciepes later. Check out branch claude/ranged-combat - for reference to the attack implementation in
conjunction with this - suggest items,, animations and placements that make sense. Interview me and
clarify every detail"*

**Read next:**
- `docs/design/53-cover.md` — the design these answers decide.
- `docs/plans/cover.md`
- `docs/research/a-10-cover.md`

## 1. What the exploration found, put to the owner before the questions

- **The hit formula already has a cover slot**, `CombatDef.coverPerMille`, fixed at 1000 (design 47
  §2a). It is a single global number. `HitChancePerMille` is given only the distance, so a cover
  value per shot has to be applied in `RangedRules.Resolve`, where the shooter and target cells are
  known.
- **`LineOfSight` only asks "blocking or not"**, from two cell flag bits. Trees, beds and bushes
  block nothing. `LineOfSight.Walk` does list every cell a shot crosses, in order, so the cells next
  to the target can be found.
- **A bullet only damages a building that blocks the line.** Anything low and passable is never
  struck today.
- **No AI chooses where to stand to shoot.** A shooter stops at the first step from which the line
  opens.
- **No crouch, kneel or prone clip exists**, and no gun clip pack is owned. The only crouch in the
  game is the computed gesture stoop (lift, stow, sow).
- **Both registry keys exist** (`ui.arch.tool.sandbag`, *"Fast cover from salvage"*;
  `ui.arch.tool.barricade`, *"Cover without blocking sight"*). The palette's Security row holds
  turret, trap and barricade, all dim, and sandbags are in no row.
- **No pack has a sandbag piece that joins in a line.** Western Frontier's `SM_Prop_Barricade_Sand_01`
  is two cells long. Battle Royale and the Meadow pack were never inventoried.
- **No sand or earth commodity exists.** Only wood and stone can be built with.
- **Repair does not exist** for any building.
- **Only the ranged *design* (PR #220) is on `main`.** The code is on `claude/ranged-combat`.

## 2. Questions and answers

Recommended options are marked ★. The answer is the owner's, verbatim where it was typed.

| # | Question | Options offered | Answer |
|---|---|---|---|
| 1 | How closely should cover follow the reference? | ★ faithful + 3D · faithful, flat · simple (any cover on the line within one cell) | **Faithful + 3D** |
| 2 | Which sources go in the first build? | sandbags + barricade · natural cover · furniture and walls · embrasure | **All four** (the embrasure was then moved to a second unit, Q10) |
| 3 | What does a colonist do behind low cover? | ★ crouch while aiming, pose only · no change · crouch that changes the maths | **Crouch while aiming** |
| 4 | Does the AI use cover in this unit? | ★ bandits pick covered spots and undrafted defenders seek cover, drafted colonists hold · player only · everyone | **Bandits + undrafted defenders; drafted colonists hold** |
| 5 | How are they walked through? | ★ pass-through, never stood on · walk over at a cost · impassable | **Pass-through, never stood on** |
| 6 | How are they placed? | ★ drag a line, neighbours auto-join · single rotatable pieces | **Drag a line, auto-join** |
| 7 | What happens to cover when it is hit? | ★ takes damage, can be destroyed, leaves material · indestructible for now | **Takes damage, can be destroyed** |
| 8 | Where may cover be built? | ground and floors · terrace lips · inside home only | Ticked all three. Clarified in Q8b as **anywhere buildable** (ground, floors, roofs, terrace lips) — the home tick was confirming that home is included, not a restriction |
| 9 | How far should height beat low cover? | ★ graded · any height beats low cover · height ignored | **Graded** — refined in Q13 |
| 10 | How does the player see cover working? | hit chance on hover · shield marker · placement preview · floater when cover stops a bullet · *something else* | **Hit chance on hover, and the floater.** "Something else" was then answered *nothing more* |
| 11 | Sandbags and barricade: the reference makes them identical. Ours? | ★ distinct roles · identical, different art · tiered | **Distinct roles** — sandbags a fixed cheap recipe; the barricade built from a material, its hit points following the material, slower to cross |
| 12 | Embrasure now or later? | ★ second unit · same unit | **Second unit** |
| 13 | Height, refined (a wall is one full layer, so from one layer up a shooter does see over it) | ★ by the shot's angle of descent · by layer count | **By the shot's angle** |
| 14 | Sandbag art | ★ custom modular Blender pieces (straight, corner, end, T) · procedural shape · Synty prefab, one per cell | **Custom modular pieces** |
| 15 | Build on `claude/ranged-combat`, or wait for it to merge? | ★ stack on it · wait | *"It is now merged ? if not use it"* — checked: only the design is merged, so **stack on `claude/ranged-combat`** |
| 16 | Repair (no building has it) | ★ separate unit for every building · in this unit | **Separate unit, all buildings** |
| 17 | The lean round a corner | ★ later unit · this unit | **Later unit** |
| 18 | Direction | ★ only facing the shot · round cover | **Only facing the shot** |
| 19 | Melee against cover | ★ cover ignores melee · bandits bash cover | **Cover ignores melee** |
| 20 | Can cover catch a stray bullet flying past? | ★ yes, at half its cover value · no | **Yes, at a fraction** |
| 21 | What does destroyed cover leave? | ★ a quarter of its cost · nothing · half | **A quarter of the cost** |
| 22 | When to crouch | while aiming or firing · drafted and waiting · bandits too | **All three** |
| 23 | Default cover values | ★ accept the table · change some | **Accept** (design 53 §3) |
| 24 | Barricade art | ★ wood → Western Frontier timber, stone → Meadow low stone wall · stone → Sci-Fi City concrete barrier · custom | **Wood → timber, stone → low stone wall** |
| 25 | Crossing cost (a step is 100, a bush +50) | ★ sandbags +150, barricade +250 · both +150 · +300 / +500 | **+150 / +250** |
| 26 | Placeholder recipe (no sand exists) | ★ sandbags 5 stone fixed, barricade 5 wood or stone · scrap · a new sand item | **Sandbags: stone; barricade: wood or stone** |
| 27 | Where the hit-chance breakdown shows | ★ a tooltip at the cursor · the inspect pane | **Tooltip at the cursor** |

## 3. Tensions chosen into

- **Faithful and 3D pull against each other at a terrace.** The reference has no height. Grading by
  layer count would make walls unrealistically strong from above, and a terrace one layer high too
  strong a firing step. Grading by the angle of descent keeps distance in the answer: a shooter one
  layer up and ten cells away (7°) still faces the sandbags in full.
- **Pass-through-only is the expensive answer.** It needs one owner for "may a pawn stand here",
  asked by every place that picks where a pawn ends up — thirty-one walkability callers to audit.
  But it is the only answer under which cover is always *beside* you and never *under* you, and
  under which a line of sandbags cannot seal anybody in.
- **"Leaves material" departs from design 33**, where a building destroyed in combat leaves nothing.
  It is a per-building field, so the older buildings keep their rule.
- **Distinct roles depart from the reference** so that choosing between the two pieces means
  something.
