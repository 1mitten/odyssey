# The Meadow overhaul — owner interview

**Phase:** Interview (feature-level, in the shape of `look-interview.md` and `grass-interview.md`).
**Date:** 2026-09-24. **Branch:** `claude/meadow-overhaul`, worktree `D:\code\odyssey-meadow`
(documents only; no code was written before or under this file).
**Conducted by:** Claude Code, twenty questions in five rounds, after a read-only exploration of the
package, the renderer, worldgen and the settings.

The owner's brief: *"We need to overhaul the graphics. We have a synty pack called meadows, can we
make more graphic settings or replace what we need to. In particular we want grass/flowers and trees
to be replaced taken into account the existing system, frustum culling, occlusion and all the
performance issues we might come up against — if we need a loading screen to prebake / warm shaders
or create environments that is ok … take into account the terrain and what we have currently like
the terraced platforms — how do we make that transition to have a landscape that can handle these
heights and replace the terraced tiles."* And, mid-interview: *"Could we also make the terrain
completely full of grass to make it lush? also consider performance."*

**Read next:** `docs/design/36-meadow-overhaul.md` (the design these answers decide), and the four
research files `e-09`, `d-16`, `d-17`, `d-18`.

## 1. What the exploration found, put to the owner before the first question

- **The pack is already imported, and already half in use.** `Assets/Synty/PNB_Core` and
  `Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest` are on disk with the same 182 prefabs as the
  downloaded `POLYGON_NatureBiomes_MeadowForest_Unity_2022_3_v1_10_5` package. Today's grass tufts
  and the grass ground material are already Meadow assets; the trees are still two ~550-triangle
  PolygonGeneric meshes. **There is nothing to import.** The package's bundled `PolygonGeneric` —
  the same GUID trap Battle Royale brought — is therefore never touched.
- **The terraces are simulation, not art.** Every column top is a whole 3 m layer and neighbours
  differ by at most one (`HeightfieldPass`); a colonist hops exactly one layer. Everything smooth on
  screen — the roll, the banks, the hills outside the board — is drawing only.
- **Cost is batches, not triangles**, nothing uses LOD (`ModuleLibrary.HighestDetail` keeps LOD0),
  frustum culling is built and unmerged (PR #174), and there is no shader warm-up and no loading
  screen beyond `PrimeAll`'s wait.
- **Meadow's pieces are heavy and carry LODGroups we cannot use as they stand**, because the world
  is drawn with `RenderMeshInstanced` rather than GameObjects: trees are 5,000–45,000 triangles
  across their levels against today's 550.
- **Synty's own demo** is one Unity Terrain with 28 terrain layers plus about 12,200 placed
  prefabs, most of them the 4–7 m tall-grass clumps (`Tall_Clump_04/05`).

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | How should the terraced ground become a landscape? | **Smooth skin**: the sim keeps whole layers; the ground is drawn as a continuous heightfield skin over the column tops, every earth step a slope (the banks, generalised). Rock, dug and two-layer faces stay sheer. |
| 2 | How much height? | **Rolling hills, about 8 layers (~24 m)** — *"but make sure performance doesn't suffer"*. |
| 3 | What happens to PR #166 (code grass, closed unmerged today)? | **Meadow meshes, our own shader**: revive #166's shader ideas, tick wind and clearance field; drop its code-built blades. |
| 4 | What replaces the two trees? | **All Meadow, more species.** |
| 5 | Do the 2026-09-22 grass answers still hold? | **Yes — but "really lush on every tile if possible considering performance".** Never hides items, spring/lime, ~1.1 m, all kept; *follow the land* gives way to *full cover everywhere*. |
| 6 | Flowers? | **Decoration now, sim later**: drawn only, placed by a rule the simulation could later own. |
| 7 | Which species exist in the sim? | **Birch, the giant meadow tree, bushes as sim things, fruit trees** — *"For MVP — can miss 2 for now"*. Read as **fruit-bearing deferred** (option 2); fruit trees exist as a shape. **To be confirmed at the M5 gate** (design 36 §11). |
| 8 | Rocks, mushrooms, leaves, lilies…? | **Some become sim things.** |
| 9 | Board depth for 8 layers of hills on a 16-layer board? | **Measure first** (16 against 20–24 tall, all four boards), then the owner picks. |
| 10 | Which settings? | **All four groups**: quality presets, vegetation density + distance, tree and foliage detail (LOD bias, foliage shadows, wind), terrain detail. |
| 11 | What performance bar? | **Ultra holds 60 fps at 3840 × 2160** on the owner's RTX 5070 Ti, **Low holds 60 fps on the laptop.** |
| 12 | How long may loading take? | **Whatever it needs**, if play never hitches. |
| 13 | The surround? | **The skin continues past the rim**, dressed in Meadow wood whose far trees use the card LODs. |
| 14 | Which atmosphere pieces? | **Meadow post-processing, Meadow water, ambient FX.** Not the Meadow sky. |
| 15 | Occlusion culling? | **Agree to skip it.** Merge #174; LOD, distance and the layer slice; revisit only on a number. |
| 16 | A building on a slope? | **The ground levels under it** (drawn only). |
| 17 | What first? | **Lush grass + Meadow trees first**, on today's ground. |
| 18 | Which small things become sim objects? | **Loose rocks, mushrooms, berry bushes.** |
| 19 | Canopies and trees? | **Canopy fades over pawns; trees topple when felled; wind freezes on pause.** |
| 20 | Which laptop is "Low"? | **RTX 3050 / 3060 laptop**, 1080p. |

## 3. Why the recommended landscape was recommended

Four were offered: the smooth skin, Unity Terrain (what Synty's demo does), reskinning the terraces,
and finer simulation heights. **Finer heights reopen ADR 0002**, which is marked irreversible, and
every hop, path, pond and golden with it. **Unity Terrain is one surface**: digging, tunnels, the
slice and x-ray view, and cut faces would each need a second ground system that agrees with it, and
every edit rewrites a heightmap. **The skin keeps the simulation exactly as it is** and generalises a
rule the project already has a simulation twin for (`BankLayout` ↔ `TerraceFoot`), and it can cost
*fewer* instances than the box-per-cell ground it replaces.

## 4. Tensions the owner chose into, recorded so they are not read as faults

1. **"Completely full of grass" against "grass never hides anything" and the 4K bar.** Full cover at
   1.1 m is overdraw, and overdraw is the GPU, which was already 8–9 ms of a 4K frame. The levers are
   recorded in the design (the ground texture carries the far field, clumps cut tight to their silhouette, rank thinning with
   distance, no grass shadows, a density rung), and the 4K measurement is the gate, not the
   640 × 480 one.
2. **Eight layers of hills against "performance doesn't suffer".** More relief means more visible
   layers and possibly a taller board. Hence question 9's *measure first*.
3. **Our own shader against Synty's look.** The Meadow textures will be drawn by a clean-room shader
   so that wind freezes on pause and grass clears round items; it will not look identical to Synty's
   screenshots, and the first Play is where that is judged.

## 5. What this does not settle

- **Whether fruit-bearing is in the MVP** (question 7's note) — confirm at M5 (design 36 §11).
- **The board height**, until M7 measures it (design 36 §7).
- **Whether the Meadow post-processing profile beats the golden hour** — it arrives as an option to
  flick between, the way the Look switch was designed.
- **PR #174** conflicts with `main` as of this date and needs a merge-up and an approving review
  before M1's measurements mean anything.
