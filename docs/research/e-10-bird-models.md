# e-10 — Bird models: sources, licences, and making our own

**Phase:** Research, ahead of any birds unit (ambient wildlife over the Meadow board). **Status:**
done 2026-09-25. **Method:** 12 web searches, 10 page fetches (**8 of them refused by this
container's egress proxy**: quaternius.com, quaternius.itch.io, poly.pizza, opengameart.org,
sketchfab.com, fab.com, assetstore.unity.com, syntystore.com — only GitHub answered), and repo
reads of `e-03`, `e-08`, `e-09`, `synty-inventory.csv` and the camera rig. Because the listing pages
could not be opened, **most licences and counts below are as a search engine's summary of the page
stated them, not as read on the page**, and are marked so. Nothing was downloaded.

## Question

Which low-poly bird models (ideally with a flap/glide animation) fit this game's Synty style, and
under what licence — and can one be made cheaply ourselves?

## Findings

1. **No owned Synty pack contains a bird.** `synty-inventory.csv` lists every prefab under
   `Assets/Synty`; the Meadow Forest folder (`PNB_Meadow_Forest`) holds 77 Env, 86 Prop, 4
   Butterflies FX (`FX_Butterflies_Blue/Cabbage/Lunar/Monarch_01`), leaves, petals, dust, wind and a
   sunbeam — and **two birdhouses** (`SM_Prop_Birdhouse_01/02`), no bird. Synty's marketing line for
   the pack, *"butterflies fluttering throughout the air, and birds nesting in the trees"*, is most
   plausibly the birdhouses (inference). `e-03` already found no animal in Farm, Western Frontier,
   Generic or Particle FX. The inventory counts prefabs; a bird FBX with no prefab would not appear,
   so this is high rather than certain.
2. **Synty does sell birds elsewhere.** *POLYGON Swamp Marshland — Nature Biome* is described as
   having *"swaying tree branches, animated frogs, dragonflies and birds"* (search summary of the
   Synty Store and Asset Store listings). Whether those birds are skinned meshes, a particle effect
   or a flipbook was not visible, and neither was a standalone price — only that the whole library
   is on a subscription *"starting at $30 USD/mo"*. Anything from it is licensed and lives only under
   `Assets/Synty/` (gitignored). No Synty-published "Animals" or "Birds" POLYGON pack was found in
   the searches; a *"BIRDS PACK"* on Fab (crow, duck, golden eagle, great horned owl, pigeon,
   seagull, sparrow; *"included inside the Animals Full Pack"*) turned up beside Synty results, but
   its publisher could not be seen and **it should not be assumed to be Synty's**.
3. **The existing animals point at Quaternius.** `e-08` attributes the owner's CC0 Pig and Rat to
   Quaternius on the rig, exporter and clip set. Poly Pizza lists a **"Pigeon — Free 3D Model By
   Quaternius"** (`poly.pizza/m/9NGlBTpDEr`), and the search summary also mentions Quaternius Bird
   and Crow models there. Quaternius states that *"everything has been released under CC0"* (search
   summary of the Patreon page). The Pigeon's own page, its triangle count, its clips and whether it
   is rigged could not be opened. The **Ultimate Animated Animal Pack** (12 animals, 12+ clips each,
   FBX/OBJ/glTF/Blend) is quadrupeds; no bird was named in any summary of it.
4. **Free/CC0 elsewhere** (all from search summaries; pages unreachable):
   - OpenGameArt *Low poly 3D Pigeon model (rigged + animated) [untextured]* — CC0, made in Blender.
   - OpenGameArt *Bird* (`bird-1`) — CC0, clips flap, pose, attack, idle, singing.
   - OpenGameArt *Bird - Animated* — modelled and animated in Blender, 7 loopable keyframes on a
     simple skeleton, head and tail bob. **Licence not seen.**
   - OpenGameArt *Animated Animales Low Poly* — an animated eagle among a wolf, dog, cat and piranha,
     CC0.
   - Sketchfab *Animated Bird, Pigeon* by dudecon — five clips (glide, flap, idle on the ground,
     take-off, landing), *"licensed as Public Domain"*. The best-fitting clip set found.
   - Sketchfab *Low Poly Bird (Animated)* by Charlie Tinley — Maya 2016, rigged and animated,
     downloadable. **Licence not seen** (Sketchfab downloads are often CC-BY; unverified).
   - Kenney *Animal Pack* — 80 assets, CC0, but as far as the listing shows it is 2D; no Kenney 3D
     bird was found.
5. **Asset Store (licensed, gitignored).** *Bird Flock Bundle* by Unluck Software: sparrow, pigeon,
   crow, vulture, seagull and a butterfly, flock roaming with obstacle avoidance, **baked**
   animation, GPU-oriented, textures at 2048². *Low Poly Bird: Ultimate Pack*: seagulls, eagles,
   parrots, vultures, magpies, owls, ravens and others, animated; publisher not seen. Asset Store
   content normally ships under the Standard Unity Asset Store EULA — **not seen stated on either
   listing** — which in any case means `Assets/Synty`-style treatment: gitignored, never a
   dependency of the sim or the tests. The Unluck birds are textured and semi-realistic, so they
   would not share Synty's flat-swatch look without repainting.
6. **Making one is a known, tiny technique.** The three.js GPGPU birds example (source read on
   GitHub) builds each bird from **three triangles** — one vertical body triangle and two wing
   triangles sharing its centre line — and flaps in the vertex shader by moving only the two
   wing-tip vertices: `newPosition.y = sin(phase) * 5.`, with the phase advanced each frame by the
   bird's speed, so faster birds beat faster. Boid demos generally use the same body-plus-two-wings
   shape; procedural-wing write-ups drive the tip by trigonometry rather than keys, flat wings when
   diving, faster beats when climbing.
7. **What a Synty-style version needs, and what it costs.** A bird of **24–40 triangles**: a
   faceted diamond body (8), a tail fan (2), and each wing as an inner and an outer panel (2 + 2
   per side, drawn double-sided). Unshared vertices give Synty's faceted flat shading for nothing,
   and the facets flicker light-to-dark as the wing turns, which is itself what reads as a beat.
   Colour is one swatch: either a per-material `_BaseColor` (as the rat and pig already are) or a
   UV pinned to one texel of a palette atlas — the Synty convention `e-09` found on the Meadow
   atlas. The flap wants a **hinge weight** per vertex (0 body, ~0.5 elbow, 1 tip, in a vertex
   colour or UV2 channel) and a side sign, so the outer panel can lag the inner one by a phase
   offset — the difference between a paddle and a wing. Per-instance phase from the instance
   index plus the clock; a **glide** is the same mesh with the amplitude at zero and a small
   dihedral. Drawn with `RenderMeshInstanced`/indirect, a species is **one draw call**, the pattern
   the project already uses for the GPU-driven scenery (design 38 §22) and the foliage shader's
   game-clock wind (§16). A skinned bird, by contrast, would be a figure competing with colonists
   for the **64-figure ceiling** (`PawnFigureDirector.FigureCeiling`).
8. **What reads from this camera.** `SliceCameraRig` runs 10–160 m and `ChunkRenderer` assumes a
   40° field of view, so a metre spans about `H / (0.73 × d)` pixels: at 1080p, **37 px at 40 m,
   18 px at 80 m, 9 px at 160 m**; double that at 4K. A crow or rook (wingspan ~1 m) is a clear
   silhouette at every zoom; a wood pigeon (~0.75 m) likewise; a gull (~1 m, white) reads best
   over water; a sparrow or skylark (~0.2–0.3 m) is **2–7 px at 1080p from 80 m — a speck**.
   Birds fly above the ground and so nearer the camera, which helps a little. From a 48° camera
   the silhouette is mostly plan view, so the **wings carry the whole read**, and dark birds over
   green grass have the most contrast.
9. **Species for a temperate meadow**, ranked by how well they read and how little they need:
   - **Rooks / crows** — flocks, forage on open ground and fields (a later hook into crops),
     dark against grass. The best first species.
   - **A soaring buzzard or kite** — large, circles on thermals, **glide only**: the cheapest
     animation there is and the most legible single bird at 160 m.
   - **Wood pigeons** — grey, common, clatter up from trees.
   - **Swallows / martins** — summer, low and fast over water and fields; small, so a darting
     speck, best near the camera or at dusk.
   - **Gulls** — plausible inland over ponds (black-headed gull), white, read against water.
   - **Songbirds** — too small to read from the play camera; heard rather than seen (the weather
     work already has birdsong stepping back under rain, design 43 §7a).
10. **Where birds sit in the architecture.** Ambient flying birds are presentation, like tufts,
    butterflies and sound — not in a cell, a save or the hash (CLAUDE.md, *standing rules*). A bird
    that lands, eats a crop or can be hunted is a `Pawn` with a `Kind` (design 29) and would need
    the rigged route or a ground pose; that is a different, later unit.

### Candidate models

| Candidate | Source | Licence (as seen) | Tris | Animated | URL |
|---|---|---|---|---|---|
| Procedural bird, body + two-panel wings | ours | ours — committed under `Assets/Odyssey` | 24–40 | yes, vertex shader flap + glide | — (three.js 3-tri precedent below) |
| Pigeon (also Bird, Crow per summary) | Quaternius via Poly Pizza | CC0 (Quaternius blanket statement; page not opened) | not seen | not seen | https://poly.pizza/m/9NGlBTpDEr |
| Animated Bird, Pigeon (glide, flap, ground idle, take-off, land) | Sketchfab, dudecon | Public Domain (summary) | not seen | yes, 5 clips | https://sketchfab.com/3d-models/animated-bird-pigeon-797d27b68af3453e865149435df6aa30 |
| Low poly 3D Pigeon, rigged, untextured | OpenGameArt | CC0 (summary) | not seen | yes | https://opengameart.org/content/low-poly-3d-pigeon-model-rigged-animated-untextured |
| Bird (flap, pose, attack, idle, singing) | OpenGameArt | CC0 (summary) | not seen | yes | https://opengameart.org/content/bird-1 |
| Bird - Animated (7 keyframes) | OpenGameArt | **not seen** | not seen | yes | https://opengameart.org/content/bird-animated |
| Eagle in *Animated Animales Low Poly* | OpenGameArt | CC0 (summary) | not seen | yes | https://opengameart.org/content/animated-animales-low-poly |
| Low Poly Bird (Animated) | Sketchfab, Charlie Tinley | **not seen** | not seen | yes | https://sketchfab.com/3d-models/low-poly-bird-animated-82ada91f0ac64ab595fbc3dc994a3590 |
| Swamp Marshland birds | Synty | Synty licence, gitignored; subscription from $30/mo | not seen | "animated" (form unknown) | https://syntystore.com/products/polygon-swamp-marshland-nature-biome |
| BIRDS PACK (7 species) | Fab, publisher not seen | not seen | not seen | yes | https://www.fab.com/listings/0d26d643-46e7-48eb-8b38-9a9873068d29 |
| Bird Flock Bundle (sparrow, pigeon, crow, vulture, seagull) | Asset Store, Unluck Software | Asset Store EULA assumed, not seen; gitignored | not seen ("low-poly optimised", 2048² textures) | yes, baked + flock script | https://assetstore.unity.com/packages/3d/characters/animals/birds/bird-flock-bundle-25576 |
| Low Poly Bird: Ultimate Pack | Asset Store | Asset Store EULA assumed, not seen; gitignored | not seen | yes | https://assetstore.unity.com/packages/3d/characters/animals/birds/low-poly-bird-ultimate-pack-200559 |

## Recommendation

1. **Build our own procedural bird — back this one.** 24–40 triangles, faceted, one swatch colour,
   flap and glide in the vertex shader with a lagged outer panel, instanced one draw call per
   species, presentation-only. It is the only option that is committed, needs no licence question,
   works on the CI runner and on a clone without any pack, costs nothing against the figure
   ceiling, and is the right shape for the one thing ambient birds are: many small things moving
   at distance. Start with **rooks** (a flock over the fields) and a **soaring buzzard** (the same
   mesh, glide only).
2. **Quaternius's Pigeon/Crow**, if CC0 is confirmed on the page — the same author and flat-colour
   family as the rat and pig, so it would match the animals already on the board. The route for a
   bird that **lands and walks**, since a vertex-shader bird has no ground pose.
3. **dudecon's public-domain pigeon** — the best clip set (take-off and landing included), the
   runner-up for the same landed-bird role.
4. **Synty Swamp Marshland** — only if the owner already subscribes and its birds turn out to be
   meshes; licensed art means no birds on any machine without the pack.
5. **Asset Store flocks** — textured, off-style, EULA, gitignored. Last.

**Tie between 1 and 2:** they are not really rivals — 1 is for birds in the air, 2 for birds on the
ground. The observation that decides which comes first is **whether the owner wants birds that land
and peck** (on crops, on the stockpile) or only birds overhead. The cheapest experiment is an editor
shot, as `AnimalProbe` did for the rat and pig: a 30-triangle procedural rook beside the rat, shot
from 40, 80 and 160 m at 1080p and 4K, flapping and gliding — half a day, and it settles both
whether it reads and whether the flat facets sit with the Synty ground.

## Sources

- https://syntystore.com/products/polygon-meadow-forest-nature-biome (search summary only)
- https://assetstore.unity.com/packages/3d/vegetation/trees/polygon-meadow-forest-nature-biomes-3d-environment-art-by-synty-234255 (search summary only)
- https://syntystore.com/products/polygon-swamp-marshland-nature-biome (search summary only)
- https://assetstore.unity.com/packages/3d/environments/landscapes/polygon-swamp-marshland-nature-biomes-3d-environment-art-by-synt-234254 (search summary only)
- https://www.fab.com/listings/0d26d643-46e7-48eb-8b38-9a9873068d29 (search summary only)
- https://quaternius.com/packs/ultimateanimatedanimals.html (search summary only; fetch refused)
- https://www.patreon.com/quaternius/about (search summary only)
- https://poly.pizza/m/9NGlBTpDEr (search listing only; fetch refused)
- https://poly.pizza/u/Quaternius (search listing only)
- https://opengameart.org/content/low-poly-3d-pigeon-model-rigged-animated-untextured (summary)
- https://opengameart.org/content/bird-1 (summary)
- https://opengameart.org/content/bird-animated (summary)
- https://opengameart.org/content/animated-animales-low-poly (summary)
- https://opengameart.org/content/5-low-poly-animals (summary; species not seen)
- https://sketchfab.com/3d-models/animated-bird-pigeon-797d27b68af3453e865149435df6aa30 (summary)
- https://sketchfab.com/3d-models/low-poly-bird-animated-82ada91f0ac64ab595fbc3dc994a3590 (summary)
- https://kenney.nl/assets/animal-pack (summary)
- https://assetstore.unity.com/packages/3d/characters/animals/birds/bird-flock-bundle-25576 (summary)
- https://assetstore.unity.com/packages/3d/characters/animals/birds/low-poly-bird-ultimate-pack-200559 (summary)
- https://raw.githubusercontent.com/mrdoob/three.js/dev/examples/webgl_gpgpu_birds.html (**read**)
- https://bubblepins.com/blog/procedural-animation-my-flapping-wing-test (summary)
- https://github.com/techcentaur/Flocking-Simulation (summary)
- Repo: `docs/research/synty-inventory.csv`, `e-03-other-packs.md`, `e-08-animal-fbx-inspection.md`,
  `e-09-meadow-mesh-data.md`, `Assets/Odyssey/Presentation/Camera/SliceCameraRig.cs`,
  `Assets/Odyssey/Presentation/Rendering/ChunkRenderer.cs`.

## Confidence

- **High** that no owned Synty pack has a bird (read from the inventory), and for the three.js
  technique (source read).
- **High** for the pixel arithmetic, which follows from the rig's 10–160 m and the 40° field of
  view in code; **medium** for which species read, which is that arithmetic plus judgement.
- **Medium** that a procedural bird of this size matches Synty's style — reasoned from the flat
  facets and single-swatch convention, not yet seen on screen.
- **Low to medium** for every third-party licence, count and clip list: taken from search-engine
  summaries because the pages were refused by the proxy. **Open each page and read its licence
  before downloading anything into the repository.**

## Could not be determined

- The Quaternius Pigeon's licence line, triangle count, rig and clips; whether Quaternius Bird and
  Crow models exist as the summary said.
- Whether Swamp Marshland's birds are meshes, particles or flipbooks, and the pack's standalone
  price.
- The publisher of Fab's *BIRDS PACK*, and whether it is Synty's.
- Triangle counts for every third-party model; licences for Tinley's Sketchfab bird and OGA's
  *Bird - Animated*.
- Whether the Asset Store listings state the Standard EULA or an extension licence.
- Whether a 24–40 triangle bird reads on the owner's screen — the experiment in the
  Recommendation answers it.
