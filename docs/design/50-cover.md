# 50: Cover, sandbags and the barricade

**Status (2026-09-25): designed; nothing built.** The work so far is ground, the interview
(`docs/research/cover-interview.md`, 27 answers), the research (`docs/research/a-10-cover.md`) and
this document. The plan is `docs/plans/cover.md`. **The next hard stop is the owner's approval of this
document.** §10 lists what to confirm or overrule, and no gameplay code is written before approval.

It sits on **design 47 (ranged combat)**, whose code is on `claude/ranged-combat` and not yet on
`main` (only the design merged, PR #220). The build therefore stacks on that branch, and **the merge
order is ranged first, then cover**. Every file reference to `Combat/…` below is on that branch.

## 1. What is being built

The owner's ask, 2026-09-25: *"We need to understand cover - we should build sandbags and barricades
… can we make it work like this [the reference's cover] but in this … suggest items, animations and
placements that make sense."*

- **A cover rule.** A pawn standing beside something is harder to hit from the side that thing faces.
  How much harder depends on:
  - what the thing is;
  - the angle the shot comes from;
  - how close the shooter is to the cover;
  - **how steeply the shot comes down.** This is our 3D term; the reference has none.
- **A shot the cover defeats hits the cover.** Cover wears down in a firefight and can be destroyed.
- **Two buildable pieces, sandbags and the barricade**, placed by dragging a line like a wall, joined
  visually, crossable but never stood on.
- **Cover from what is already in the world:** walls, rock faces, doors, trees, bushes and furniture.
- **Colonists and bandits crouch behind cover** while fighting from it.
- **Bandits choose covered firing spots.** A colonist fighting back undrafted looks for nearby cover.
  A drafted colonist holds where she is put.
- **The player sees it** in a hit-chance breakdown at the cursor and a *Cover* floater when cover stops
  a bullet.

Deferred by the owner, in §9: the embrasure, the lean round a corner, repair, and the real recipes.

## 2. The rule

### 2a. Which cells

For a shot from shooter cell **S** to target cell **T**, the candidates are the **eight neighbours of T
on T's own layer**. A neighbour is skipped if it:
- is S itself;
- is off the board;
- holds nothing that gives cover (§3).

A cell has one cover value: the edifice in it, or its solid terrain. Nothing further away than one
cell protects T, so a long sandbag line protects only through the pieces next to T. That is the
reference's rule and the one that makes a line worth *standing at* rather than behind at a distance.

### 2b. One neighbour's contribution

`cᵢ = base × angleBand × shooterDistance × elevation`, all in per-mille, integer only.

**`base`** comes from content (§3). A full-fill thing is 750, not 1000: the reference's rule, and it
means a wall beside you is not invulnerability.

**`angleBand`** is the horizontal angle at T between T→S and T→C. For a diagonal neighbour the angle
is counted ×1.75.

| Angle (cardinal neighbour) | Angle (diagonal neighbour) | Factor |
|---|---|---|
| under 15° | under 8.57° | 1000 |
| under 27° | under 15.43° | 800 |
| under 40° | under 22.86° | 600 |
| under 52° | under 29.71° | 400 |
| under 65° | under 37.14° | 200 |
| otherwise | otherwise | 0 |

**Computed without an angle.** With `u = S − T` and `v = C − T` in horizontal cells, *θ < B* is the
same test as `dot(u, v) > 0 && dot² × 1 000 000 > K_B × |u|² × |v|²`, where `K_B = round(cos²B ×
1 000 000)` is a constant per band. The whole test is one `long` multiply per band and no `atan`, so it
agrees under Mono and CoreCLR (design 47's "no float anywhere in it").

The constants: cardinal 933 013 / 793 893 / 586 824 / 379 039 / 178 606; diagonal (B / 1.75)
977 786 / 929 224 / 849 118 / 754 306 / 635 420. A test pins them against angles either side of each
boundary.

**`shooterDistance`** uses the horizontal squared distance from S to **C**, in cells:
- `100 × d² < 361` (under 1.9 cells): 333;
- `100 × d² < 841` (under 2.9 cells): 667;
- otherwise 1000.

A shooter at your sandbags has walked round most of them.

**`elevation`** is ours. The descent is `t = Δh × 1000 / horizontal`, in per-mille of a tangent:
- `Δh = (S.y − T.y) × 3000 mm`;
- `horizontal` is `RangedGeometry`'s integer root of the x/z distance in mm.

A shot from level or from below is `t = 0`. The factor falls linearly in `t` from 1000 to 0 between two
tangents. It is linear in the tangent, not the angle, so there is no `atan`, and the difference is a
few degrees at worst.

| Cover class | Full up to | Gone by | tan, per-mille |
|---|---|---|---|
| **Low** (sandbags, barricade, bush, furniture) | 10° | 35° | 176 → 700 |
| **Tall** (wall, rock face, door, pillar, tree) | 30° | 60° | 577 → 1732 |

Worked cases, one layer (3 m) above:

| Horizontal distance | Descent | Sandbags keep | A wall keeps |
|---|---|---|---|
| 10 cells (25 m) | 7° | all | all |
| 5 cells (12.5 m) | 13.5° | 88 % | all |
| 3 cells (7.5 m) | 22° | 57 % | all |
| 2 cells (5 m) | 31° | 19 % | 98 % |
| 1 cell (2.5 m) | 50° | none | 46 % |

Two layers up (6 m) halves every distance in that table.

The constants are **INVENTED** and tuned in play. What they buy: a terrace or a rooftop is worth
holding, close to the edge more than far back, and full cover still means something.

### 2c. Combining

`total = 1000 − Π(1000 − cᵢ) / 1000^(n−1)`: noisy-OR, folded one contributor at a time,
`total += (1000 − total) × cᵢ / 1000`. For example, 550, 220 and 220 give 729.

### 2d. The shot

`RangedRules.Resolve` (`Combat/RangedRules.cs:125-143`) takes the reference's two stages:
1. **The aim roll**, as today on `RangedHit ^ shooter`. `CombatDef.coverPerMille` is **deleted**, and
   `HitChancePerMille` loses its cover line (`:98`). The combat Def fingerprint is re-pinned.
2. **The cover roll**, only after an aim roll that hit and only with `total > 0`: `NextInt(1000) <
   total` on a new salt `RangedCover ^ shooter`. If the cover wins, one contributor is picked, weighted
   by its `cᵢ` (salt `RangedCoverPick ^ shooter`). The bullet's end becomes **that cover cell**, and it
   is marked covered.

**What the player is told the chance is:** `aim × (1000 − total) / 1000`.

`Projectiles.Entry` gains `CoverCell` (−1 for none). It is appended to `odyssey.projectiles` behind a
layout field: the section is keyed and skipped when absent, so there is **no game-wide format bump**.

### 2e. The impact

In `CombatSystem.LandBullet` (`Combat/CombatSystem.Ranged.cs:163-232`) the order of design 47 §2c stands,
with two additions:

- **A covered bullet** walks to its cover cell. Anything blocking on the way still takes it first.
  At the cover cell it **strikes the cover edifice**: `StrikeBuilding` at the Bullet factor, reported as
  `CombatEventKind.Covered` with the intended target, and `SwingResolved(target, shooter, Miss)` raised
  so it still counts as an attack on the target. `BuildingTargets.TryFind` already accepts any edifice
  with hit points; today only the blocking branch ever reaches it.
- **A stray crossing a cover cell** — a miss, or any bullet crossing cover that is not the target's
  own already-rolled neighbourhood — rolls `base × coverInterceptPerMille (500) / 1000 ×
  deadZoneRamp(distance from shooter)` on `RangedIntercept ^ shooter ^ cell`, the existing bystander
  salt. This is tested in walk order beside the bystander test.
  - **The dead zone is the existing one**, 0 at 5 m rising to full at 12 m, so a colonist's own
    sandbags never eat her outgoing shots.
  - The **only** crossed cells not rolled are the target's eight neighbours on an aimed-true shot,
    because the cover roll has already spoken for them.

**Destroyed.** A cover piece reaching 0 hit points is demolished as today (`ConstructionGrid.Demolish`).
A new `BuildingDef.wreckRefundPerMille` then drops that share of the cost on the cell: **250 for
sandbags and the barricade, 0 for every existing building**, so design 33's "no refund" stands for
them. The odd unit is settled by the seeded coin `DeconstructJob.Refund` already uses.

## 3. What gives cover

Every value below is ours unless marked; the reference's are 550 (sandbags, barricade), 750 (full
fill) and 250 (tree).

| Thing | Base | Class | Where it lives |
|---|---|---|---|
| Sandbags | 550 | low | `BuildingDef.coverPerMille`, `coverTall = false` |
| Barricade | 550 | low | same |
| Shelf | 500 | low | same |
| Generator | 500 | low | same |
| Heater | 400 | low | same |
| Bed | 300 | low | same |
| Campfire | 250 | low | same |
| Wall, closed door, pillar, vault wall | 750 | tall | the full-fill rule: `CellFlags.BlockingEdifice` without the open-door nav flag |
| Rock face (solid terrain on T's layer) | 750 | tall | `CellFlags.SolidTerrain` |
| Open door | 0 | — | `NavFlags.Door | DoorOpen` |
| Tree (every species) | 250 | tall | a cover column beside `NaturalContent`'s edifice ids |
| Bush, berry bush | 150 | low | same |
| A pawn | 0 | — | pawns intercept in flight instead (design 47 §2c) |
| Items, loose stones, the dressing's boulders | 0 | — | loose stones are items and boulders are scenery (design 45); **simulated boulders are a later unit** |

The lookup walks the same chain `BuildingTargets.TryFind` walks: `Cells.Edifice[cell]` →
`PlacedEdifice` → `BuildingDef`. An unbuilt site gives nothing.

**One owner for the number.** `Cover.BaseAt(ctx, cell, out bool tall)` is the only place that answers
it. The rule, the AI, the crouch and the readout all ask it, and a test reads the C# files for a second
copy, in the manner of `HopPriceHasOneOwnerTests`.

## 4. The two pieces

| | Sandbags | Barricade |
|---|---|---|
| Role | cheap, quick, a fixed recipe | built from a material; hit points follow it |
| Placeholder cost | **5 stone, fixed** (standing in for filled bags; no sand exists) | **5 of wood or stone**, chosen on the palette |
| Work | low, as the shelf (180) — INVENTED | higher (320, the reference's ratio) — INVENTED |
| Hit points | 300 × the stuff factor (stone) | 300 × the chosen stuff's factor |
| Crossing | **+150** (a step is 100; a bush +50) | **+250** |
| Cover | 550, low | 550, low |
| Size and placement | one cell; **line-only** drag, like a wall | same |
| Stood on | **never** (§5) | never |
| Encloses a room | no (`EnclosureGrid.IsWallOrRock` names only walls) | no |
| Blocks sight or movement | neither | neither |
| Wreck | 250 ‰ of the cost | 250 ‰ |

The **recipes are placeholders by the owner's word** ("we can decide on exact recipes later"); each is
one content line. The sandbag key's text changes from *"Fast cover from salvage"* to match.

**Where** is anywhere buildable: ground, a floor, a rooftop, the lip of a terrace step. The ordinary
`ConstructionGrid.Allows` applies — no water, no solid terrain, no existing edifice, no items on the
cell (`needsClearCell`). There is no home-area restriction.

**Crossing cost** is a **cost class** in `NavGrid.ClassAt` read from the cell's edifice. This is the
bush's precedent (design 45 §4, `CostClassBush`), with one owner, mirrored in `NavGraph.StepCost` and
held together by a test in the shape of `SiteDetourHasOneOwnerTests`.

**Palette:** the Security row becomes `{turret, trap, barricade, sandbag}`, and sandbags and barricade go
live in `PaletteTools.Live`. The sandbag needs a drawn glyph (`HudGlyphKind.ToolSandbag`), or
`EveryPaletteKeyHasItsOwnShape` fails. Both keys already exist, so the wiki gains no row; the sandbag's
description is corrected.

**Handles.** Two `BuildingHandle`s, two edifice ids and `NaturalContent.EdificeLimit`, all at **the next
free numbers when CV3 is built**. Cooking landed on `main` after the ranged branch forked, and design 43
§13 shows what a handle collision costs on merge. The ids are values of an existing saved field, so
there is no format bump.

## 5. Pass-through only

A pawn may **cross** a sandbag or barricade cell and may **never stop** on one. Rest, work, wait, wander
and aim all happen beside it.

- **`NavFlags.PassThrough`**, sticky, set by `RaiseEdifice` and cleared by `RemoveEdifice`.
- **`Standing.CanStandAt(ctx, cell)` is the one owner** of "may a pawn end up here". Every picker asks
  it: path goals (a goal on a pass-through cell snaps to the nearest standable neighbour),
  `Melee.ChooseSide`, `FleeJobDriver.FindFleeCell`, `ShelterTarget.Find`, wander and idle, work stances
  (`NearestOfRing`), `PawnEviction`, a drafted `OrderMove` (refused onto the cell or snapped),
  and the drop-cell and `OpenGroundFor` pickers for items.
- **The raise guard.** `CanRaiseNow` (design 30-nobody-in-a-wall) extends to pass-through Defs, so
  nobody is left standing inside a sandbag the instant it goes up.
- **A sweep.** Like `TrappedPawnSystem`, it moves any pawn found at rest on a pass-through cell. It is
  the safety net for anything the audit missed, and a Long-tier property test fails if it ever fires
  over ten days with a sandbag ring round the start.
- **The audit.** CV4 lists all 31 `IsWalkable` callers and what each one does.

**Rejected:** walk-over-at-a-cost (standing on your own sandbag removes its cover); impassable (a line
seals people in, and needs gaps). Both are in the interview.

## 6. The AI

`CoverPosition.Find(ctx, pawn, target)` returns a firing cell or none.

- **Candidates** are cells within **6 cells** (Chebyshev) on the pawn's layer that are:
  - reachable (`ctx.Reachable`);
  - standable (§5);
  - not held by another fighter (`MayShootFrom`);
  - within the gun's range;
  - on a clear `LineOfSight` to the target;
  - **no more than 2 cells farther from the target than the pawn is now.** Design 47 §12 is "no
    backing off to shoot … it invites kiting", and this keeps to it.
- **Score** = `4 × cover the cell has from the target` + `aim‰ / 2` − `60 × travel cells`, with the
  current cell preferred on a tie and the lower index after that. The weights are INVENTED.
- **When:** at the start of `Approach` in `AttackRangedJobDriver`, when the target changes, when the line
  is lost, and on the 300-tick rechoose. **Never per tick.** After a shot a shooter moves only if its
  cover from the target is under 200 ‰ and a better cell is within 3 cells, so nobody dances between
  shots.
- **Who:**
  - bandits with a gun (`HostileThinkNode`);
  - a colonist fighting back undrafted with a gun (`SelfDefence` / Defend);
  - **not** a drafted colonist (`DraftHoldJobDriver` unchanged): she holds where the player put her.
- **Melee ignores cover.** A melee bandit crosses a line at its crossing cost and fights. Bashing
  through cover is a later raid-AI unit.
- **Cost:** 169 candidates × (one line walk of about 14 reads + eight cover reads) per call, a few times
  per shooter per 300 ticks. It is measured in CV9 with a control, not assumed.

## 7. Drawing

### 7a. Sandbags

**Custom modular pieces** (owner): straight, corner, end and T, one cell, under
`Assets/Art/Custom/Cover/`, committed, in the Synty style. `ChunkMesher` picks one from a
four-neighbour mask, the way wall panels are picked.

**Height about 1.3 m.** A drawn colonist is about 2.5 m (the rigs' `scale 1.4`), and 1.3 m is roughly
the Western Frontier sand barricade (1.54 m) and the Meadow stone wall (1.31 m). **Judge it against a
crouched figure in play.**

Two steps:
1. **CV6 ships boxy placeholder pieces built in code first**, so the unit is playable, headless tests
   draw nothing unexpected, and the runner (no `Assets/Synty`) is unaffected.
2. The Blender set replaces them. Who authors it is open (§10). Before any is made, the owner runs the
   Synty inventory over Battle Royale and the Meadow pack, which were never inventoried, in case a
   joinable sandbag already exists.

### 7b. The barricade

The art follows the material:
- **wood → Western Frontier `SM_Prop_Barricade_Wood_01/02`** (timber rails);
- **stone → Meadow `SM_Prop_Stonewall_Small_01`**, with its `_End_01` and `_Pillar_01/02` for the ends
  of a run.

Both are fitted to one cell (`fitFootprint`) and turned along the run by the same neighbour mask. The
rows go in `PlayScene.cs` and the regenerated `ModuleCatalogue.asset`, and a runner without the packs
draws the placeholder.

### 7c. What every new edifice needs, or it draws nothing

`WorldRenderModel.ModuleForEdificeAt` returns module 0 for an unknown non-natural id. So each piece
needs:
- `StandHeight` and `MarkHeight` of about 1.3 m, or clicks drift a quarter cell;
- the ghost in `DrawThingGhost`, following the drag.

Walls-down leaves it alone: furniture is never lowered, and a 1.3 m sandbag stands above a 0.75 m
stump, which is correct.

### 7d. Damage shown

The health bar over a struck building that design 33 §13k owes is **built here, for every building**,
from the published `EdificeDamage` table. The cover pieces are simply its first frequent customers.

A damaged look (cracks, a darker tint) stays owed.

### 7e. A covered hit

The *Cover* floater, a new key `ui.combat.cover`, rises at the cover cell. Dust flies from it, the
building-hit dust `ProjectileDirector` already throws. The streak ends on the cover piece's face.

## 8. The crouch, and the readout

### 8a. The crouch

A pawn aspect `CoverCrouch`, **derived at publish time** (the *Sheltering* precedent, design 43 §6a):
**not saved, not hashed**, and no golden can move for it.

It is 1:
- while a pawn is in the aim toil or its cooldown and its cover **from its current target** is at least
  200 ‰ with a low contributor;
- or while it is drafted with no target and has a low cover piece as a neighbour;
- for colonists and bandits alike.

Drawn by reusing the gesture stoop (`ApplyGesturePose`, the legs solved back to the feet) **under** the
aim pose in `ApplyWorkPose`'s chain (`PawnFigureDirector.Poses.cs:244-315`), blended over about 0.2 s.
The pawn rises to walk. **The depth is measured from the drawn mesh** — the aim line has to clear the
piece's top — and never from a bone's name (P11; `StandingHipHeight` is the floor of its own clamp).

A crouch changes nothing in the maths: the owner's answer to Q3.

### 8b. The readout

With a drafted ranged colonist selected, hovering a hostile shows a tooltip at the cursor:

> **63 %** — Shooting 10 · 12 m · pistol (Decent) · cover −55 % (sandbags, N) · from above ×0.76

The simulation owns every number. The HUD asks through a question intent `QueryShot(shooter, target)`,
the shape of `QueryCell` and `WatchHome` (`Sim.Contracts/Intents.cs`). The world answers on the next
publish with a `ShotReportView`, which is neither saved nor hashed. Every word comes from
`Registry.Label` (new `ui.combat.*` keys, wiki rebuilt).

**Declined** (Q10): the shield marker, a placement preview wash, a cover overlay view.

## 9. Later units, named

| Unit | What it needs |
|---|---|
| **Embrasure** | A shoot-through flag on the edifice read by `LineOfSight.Blocks`. It blocks walking, is shot through, and gives high cover (the mods use 65–95 %) |
| **The lean** | `LineOfSight` tries S's orthogonal neighbours at a corner (`a-10-cover` finding 12), plus a side-step pose |
| **Repair** | A job for every building (a new work giver, and the goldens move once); the owner's Q16 |
| **Boulders as simulated cover** | Chunks at 500 |
| **The real recipes** | A sand commodity from sand or earth, if wanted |
| **Target choice** | Prefer exposed enemies |
| **Melee bashing** | Melee bandits bashing cover |

## 10. For the owner to confirm or overrule

1. **The elevation constants** (§2b), and the tangent grading rather than the angle.
2. **The AI weights and the 2-cell "no kiting" allowance** (§6).
3. **The sandbag height of about 1.3 m** (§7a) — the one number the owner can judge only at the
   keyboard.
4. **Who authors the Blender sandbag set** (§7a), after the Battle Royale and Meadow inventory.
5. **Building the health bar for every building here** (§7d), rather than for cover alone.

## 11. Save, hash, goldens and cost

| Thing | Saved | Hashed | Moves a golden |
|---|---|---|---|
| New edifice ids and `BuildingDef` fields | ids in the existing edifice field | per site, as today | no — `BuildingFingerprint` re-pinned |
| `CombatDef.coverPerMille` removed | — | — | no — combat Def fingerprint re-pinned |
| `Projectiles.Entry.CoverCell` | `odyssey.projectiles`, layout field | only while bullets fly | no: no golden fights with guns |
| `NavFlags.PassThrough` | derived | no | no |
| The cover and cover-pick rolls | — | — | only in a gunfight |
| `CoverCrouch`, `ShotReportView` | no | no | no |

**Expect no golden to move.** This is proved rather than argued, with `GoldenColonyProbe` against
`claude/ranged-combat`.

**Cost:**
- `Cover.Evaluate` is eight cell reads and at most eight band tests per shot.
- The AI search is bounded in §6.
- Both are `Measurement` arms in CV9, each with a control in the same run.
