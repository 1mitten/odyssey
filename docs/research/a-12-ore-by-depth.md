# a-12 (ore by depth) — how colony and voxel games place ore, gems and caves by depth

**Lane A, item 12 (a second file on map generation).** Asked 2026-09-26 for deep mining (design 62).
One subagent, one question, capped at 10 searches and 10 page reads. **Every page the subagent tried
to open was refused by the proxy** (minecraft.wiki, rimworldwiki.com, dwarffortresswiki.org,
planetminecraft, gamedeveloper.com), so the findings come from search extracts only. Clean room:
mechanics, shapes and numbers in our own words; no data files, source or flavour text copied.

## Question

How do colony and voxel games distribute ores, gems and caves by depth, and what shapes and densities
make digging rewarding without being tedious? Relevance: our board is 32 layers with about 20 of rock
under the surface; we are adding copper, gold, gems, Emberquartz, a harder deep stone and larger
caverns to the existing iron and coal (today: 5–20-cell single-layer random-walk blobs, 70 deposits
per 10k columns, 15 items a cell).

## Findings

**Dwarf Fortress**
- Rock comes in stacked layer types (soil, sedimentary, igneous extrusive, metamorphic, igneous
  intrusive). From the bottom: undiggable semi-molten rock, the magma sea, stone, then three cavern
  layers separated by stone, then soil. The first cavern starts about 10–11 z-levels down; each is
  several levels tall.
- Four shapes: **veins** 1–4 tiles wide, ~100 tiles on average (some over 200); **large clusters**,
  ovals of about 20 × 40 (~750 tiles), at most about one per 48 × 48 area; **small clusters** of 1–9
  tiles; **single gems** inside a small cluster of a related common gem.
- Minerals follow the layer type (community rule of thumb: coal, iron and flux shallow; silver, lead
  and zinc in metamorphic layers; gold deep in intrusive rock).

**RimWorld**
- Surface ore lumps weighted relative to compacted steel at 1: silver ~0.10, uranium ~0.12, gold
  ~0.07, jade ~0.065. Lump sizes: steel ~30+ cells, uranium 6–12, silver 4–12, gold 2–8. About 40 a
  block, scaled by the miner's yield and difficulty.
- Deep deposits are invisible until a ground-penetrating scanner finds one; a deep drill then extracts
  it. A deep cell holds ~300 units; a plasteel deposit averages ~7 cells, a steel one ~9,000 units.

**Minecraft (1.18 onwards)**
- Most ores use a **triangular height distribution**: densest at a peak, thinning to the ends of the
  range. Copper spans a wide middle band peaking in it; iron has two bands; diamond is "lower is
  better"; lapis is a triangle plus a uniform band.
- Several attempts a chunk, blob sizes 0–37 depending on ore; the rarest have a large variant only
  about once per nine chunks, and some ores are **removed half the time when they touch a cave**, a
  lever for how much caves reveal. Huge ribbon "ore veins" of iron and copper mixed with filler stone
  sit in deep bands.

**Going Medieval**
- Clay, limestone and iron are dug underground; nothing shows where deposits are without test tunnels,
  and iron sometimes sits under limestone, so one material hints at another. Shallow iron runs out and
  pushes the player deeper.

**Design writing**
- SteamWorld Dig: value rises with depth, which paces the game; the risk of getting stuck is the
  tension; a dug tile cannot be put back, so every dig is a choice.
- The common thread: **big, common, low-value deposits high; few, small, rare, valuable deposits deep;
  and a signal telling the player where to look** — the layer type, a tell-tale mineral, cave walls,
  or a scanner.

## Recommendation

A band table for a 32-layer board with about 20 layers of rock, counts per 10k surface columns, "band"
in layers below the local surface. This is our synthesis, not any game's numbers; design 62 §5 tunes
it.

| Mineral | Band | Shape | Cells | Per 10k | Yield a cell |
|---|---|---|---|---|---|
| Copper | 1–8, peak 3 | round blobs, may cross 2 layers | 10–30 | 60 | 15 |
| Iron (existing) | 3–13, peak 8 | winding veins 1–2 wide | 15–40 | 45 | 15 |
| Coal (existing) | 7–20, flat | flat single-layer seams | 25–60 | 35 | 12 |
| Gold | 12–20, peak 17 | small clusters, favouring deep stone | 2–8 | 12 | 6–8 |
| Gems | 10–20 | pockets, favouring cave walls | 1–4 | 6 | 1–3 |
| Emberquartz | 18–20 only | one large oval cluster | 20–40 | 2–3 | 20 |
| Deep stone | 14–20 | whole layers, ~2× the work | — | — | — |
| Big caverns | two bands, ~11–12 and ~17–18 | multi-layer chambers | 300–1,500 | 3–5 | — |

- Let 30–50 % of gold and gem clusters touch a cave wall, so caves reward exploring (Minecraft's
  cave-contact removal is the opposite lever).
- Let about 5 % of deposits fall outside their band, so out-of-place finds still happen.
- Total ore stays near today's (~900–1,000 cells per 10k columns) but concentrated by depth.

## Sources

- https://www.badlion.net/minecraft-blog/minecraft-ore-distribution-1-20
- https://beebom.com/minecraft-1-18-ore-distribution/
- https://minecraft.fandom.com/wiki/Ore
- https://www.planetminecraft.com/blog/a-1-18-ore-distribution-guide/
- https://rimworldwiki.com/wiki/Deep_drill
- https://rimworldwiki.com/wiki/Ground-penetrating_scanner
- https://rimworldwiki.com/wiki/Property:Mineable_Scatter_Commonality
- https://rimworldwiki.com/wiki/Compacted_steel
- https://rimworldwiki.com/wiki/Silver_ore
- https://rimworldwiki.com/wiki/Uranium_ore
- https://rimworldhub.com/post/rimworld-ground-penetrating-scanner-tool
- https://dwarffortresswiki.org/index.php/DF2014:Vein
- https://dwarffortresswiki.org/index.php/DF2014:Stone_layers
- https://dwarffortresswiki.org/index.php/DF2014:Cavern
- https://steamcommunity.com/app/975370/discussions/0/4040358019730192496/
- https://www.thegamer.com/going-medieval-mining-tips/
- https://www.gamedeveloper.com/design/game-design-deep-dive-the-digging-mechanic-in-i-steamworld-dig-i-

## Confidence

- Minecraft peaks, ranges and ribbon-vein bands: **high**; its per-chunk counts and sizes: **medium**
  (snippets may mix versions).
- RimWorld lump sizes, ~40 a block and spawn weights: **medium-high**; deep deposit sizes: **medium**
  (a third-party site).
- Dwarf Fortress shapes, sizes and cavern depth: **medium-high**; which mineral sits in which layer:
  **medium**.
- Going Medieval: **low-medium** (guide articles).
- The recommended table: **our synthesis**, not sourced.

## Could not be determined

- Minecraft's exact cave-contact removal rate per ore, and the full ribbon-vein parameters.
- RimWorld jade and plasteel lump sizes, the scanner's find rate and radius, deep drill work amounts.
- Dwarf Fortress per-mineral frequencies and layer thicknesses.
- Going Medieval's underground generation rules; whether Timberborn mines underground at all.
- Any talk specifically on pacing resource reveals.
