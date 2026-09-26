# a-10 (cover) — how the reference game's partial cover works

**Lane A, item 10 (the cover half).** Asked 2026-09-25 for the cover line (design 53). One subagent,
one question, capped at 14 searches and 10 fetches. **Every wiki and Steam page was refused by the
proxy** (rimworldwiki.com, rimworld.fandom.com, steamcommunity.com, the Wayback Machine), so the
findings come from search extracts plus community analyses of how the game behaves, and the numbers
were cross-checked between sources. Clean room: this file records mechanics, formulas and numbers in
our own words. No Def XML, no source and no flavour text was copied, and nothing was decompiled into
this repository.

## Question

How does the reference game decide how much a thing standing beside a target protects it from a
shot? Which cells count, how does the direction of the shot matter, how do several pieces of cover
combine, what are the values, and what happens to a bullet the cover stops? And, for our buildings:
what are sandbags and barricades as buildings, and how does the AI use cover?

## Findings

**Which cells**

1. **Only the eight cells round the target count.** Cover further away gives the target nothing, so a
   long sandbag line protects only through the piece or pieces next to the target. A neighbour is
   skipped if it is off the map, if it is the shooter's own cell, or if it holds nothing that gives
   cover. [search extracts, "only the 8 tiles around a colonist are checked"; community analyses]
2. **One cover thing per cell.** If a cell holds several, the one that fills most of the cell counts.
   Anything that fills essentially none of its cell never counts. [community analyses]

**Distance and angle**

3. **A shooter close to the cover defeats most of it.** The distance is measured from the shooter to
   the *cover cell* (not to the target):
   - under 1.9 cells: the cover is worth a third;
   - under 2.9 cells: it is worth two thirds;
   - further than that: it is worth all of it.

   A shooter standing right at the target's sandbag nearly cancels it. [community analyses]
4. **The angle is measured at the target**, between the direction to the shooter and the direction to
   the cover cell. The bands and what each leaves of the cover:

   | Angle | Cover kept |
   |---|---|
   | under 15° | all of it |
   | 15° to 27° | 80 % |
   | 27° to 40° | 60 % |
   | 40° to 52° | 40 % |
   | 52° to 65° | 20 % |
   | 65° or more | none |

   [search extract; community analyses]
5. **Diagonal cover is penalised.** For a cover cell diagonal to the target, the angle is multiplied
   by 1.75 before the bands are applied.
   - A shot from due east gets the east cell in full and nothing from the NE or SE diagonals
     (45° × 1.75 = 79°).
   - A shot from exactly NE gets the NE cell in full, and the N and E cells at 40 % each.
   - This matches the players' rule of thumb: "up to three pieces of cover at 45°, only one at 90°".
   [community analyses; search extract]
6. **Per cell:** block = base value × angle factor × distance factor.

**Combining cover**

7. **Several pieces combine by "noisy-OR", not by taking the best one:** total = 1 − Π(1 − cᵢ). For
   example, 55 %, 22 % and 22 % give about 73 %. [community analyses]

**Values**

8. **A thing that fills its whole cell counts as 75 %, not 100 %**: a wall, a closed door, natural
   rock. A partial thing counts at its own fill fraction. An open door gives nothing.
   [community analyses]

   | Thing | Cover | Source |
   |---|---|---|
   | Sandbags | 55 % | search extracts, several |
   | Barricade | 55 % | search extracts, several |
   | Tree | 25 % | search extracts |
   | Rock chunk | 50 % | search extracts |
   | Slag | 50 % | search extracts |
   | Furniture | about 40 % | recollection, low confidence |

   **Pawns are not cover.** They stop bullets only through the separate in-flight interception rule
   (`a-10-projectile-path` finding 12).

**What cover does to a shot**

9. **A shot goes through three rolls, in this order:**
   1. a forced miss (area weapons only);
   2. the aim roll — if it fails, the bullet goes wild and can still hit things on its way;
   3. the cover roll, against 1 − total cover.

   **If the cover roll fails, one piece of cover is chosen, weighted by its own contribution, and the
   bullet is fired at that piece**, which takes the hit and the damage. This is why sandbags wear down
   in a long fight. The chance shown to the player is aim × (1 − cover), and the tooltip lists each
   piece of cover and what it blocks. [community analyses; confirms `a-10-projectile-path` finding 8]
10. **The shooter's own cover does nothing for their accuracy.** Cover is worked out only round the
    target. The shooter's surroundings matter only through finding 3 and through the lean
    (finding 12). [community analyses]

**Sandbags and barricades as buildings**

11. The two pieces, one cell each:

    | | Sandbags | Barricade |
    |---|---|---|
    | Size | 1 × 1 | 1 × 1 |
    | Passability | **pass-through only**: walked across, never stood on | same |
    | Extra path cost | 42 (not charged again for a run of them) | same |
    | Cover | 55 % | 55 % |
    | Hit points | 300 before the material's factor | same |
    | Work | 180 | 320 |
    | Material | fabric or leather | metal, wood or stone |
    | Burns | no | as its material |
    | Joins drawn to its neighbours | yes | no; also stops animals |
    | Blocks sight | no — only full-fill things do | no |

    Both are on the Security tab and need no research. [search extracts of the building page,
    medium-high; values for the 1.4+ era]
12. **The lean.** A shooter may fire from their own cell or from an orthogonally adjacent cell they
    can see from. At a wall corner that lets them shoot round it, and a target at a corner can be
    hit the same way, the wall then counting as its 75 %. Diagonal cells are never firing points.
    [community analyses, medium]

**The AI**

13. **Ranged raiders pick a firing position that has cover from their target.** They score cells in a
    search radius; the score rises with the cover the cell would have from the target and falls with
    walking distance. They avoid standing on pass-through cells. **Drafted colonists do not look for
    cover by themselves**: the player places them. [code-search fragments and recollection; low]

**Embrasures**

14. **Embrasures are not in the base game.** Several mods add a wall that can be shot through, worth
    65–95 % cover depending on the mod. [search extracts, Steam Workshop pages]

## Recommendation

**Adopt findings 1–9 as the model, in integers, and add a height term of our own.** Our cells are
2.5 m and layered, so a shot can come *down* over cover, and a model that ignores that is wrong in the
one place our game differs from the reference.

- Keep the eight neighbours, the angle bands, the diagonal penalty, the distance penalty, noisy-OR,
  and the redirect of a defeated shot into the cover.
- Scale each contribution by the **shot's angle of descent**: low cover fades from 10° to 35°, tall
  cover from 30° to 60° (invented, to be tuned in play).
- Keep the two pieces pass-through only.
- Defer the lean and embrasures, both of which need a line-of-sight change.

**The owner took all of this in the interview** (`cover-interview.md`), with the pieces given
distinct roles rather than the reference's identical pair.

**A tie to break later: the in-flight intercept** (a miss crossing a cover cell). The reference's
constant was not recovered; ours is half the cover value, and the owner chose it. The cheapest
experiment is the CV9 skirmish: count sandbag hits per hundred misses and compare it with how
quickly a line wears in play.

## Sources

- https://rimworldwiki.com/wiki/Cover (search extracts only; refused)
- https://rimworldwiki.com/wiki/Sandbags, /wiki/Barricade, /wiki/Defense_structures, /wiki/Chunk
  (search extracts only)
- https://rimworld.fandom.com/wiki/Sandbags (search extract)
- https://steamcommunity.com/app/294100/discussions/0/361798516950703791 and
  /3280318152186706934 (search extracts; refused)
- https://steamcommunity.com/sharedfiles/filedetails/?id=3154189813 (combat guide, extracts)
- Steam Workshop: Vanilla Embrasures (2601392777), Matching Embrasures (937587020), Dynamic Embrasure
  (3510835835) — extracts

## Confidence

| Findings | Confidence | Why |
|---|---|---|
| 1–7 (cells, distance, angle bands, diagonal ×1.75, noisy-OR) | **high** | Several independent analyses agree, and the rule of thumb in finding 5 follows from them |
| 8 (sandbags, barricade, wall 75 %) | **high** | |
| 8 (tree, chunk) | **medium** | |
| 8 (furniture) | **low** | |
| 9 (the roll order and the redirect) | **high** | |
| 11 | **high** | values for the 1.4+ era |
| 12 | **medium** | |
| 13 | **low** | |
| 14 | **high** | |

## Could not be determined

- The constant for how often a partial-cover object stops a stray bullet that crosses it.
- Current furniture fill values, and whether the latest version changed any of the numbers above.
- The AI's firing-position score weights.
