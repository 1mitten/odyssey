# 21 — Tree colours

**Status:** built 2026-09-18 on `claude/tree-colours`, and revised the same day after the owner
played it — §3a is that round and is the one to read first. EditMode 1544 total, 1532 passed, 0
failed; fast tier 645 Sim and 358 Hud. **Cost on the played board: 1758 draw calls to 2135,
+21.4%**, instances unchanged at 44,200 — that is what a thoroughly mixed wood costs, against
+3.2% when a stand was one colour, and `TreeLook.ThemesPerStand` is the knob it is bought with.

**Read this before touching** `TreePalette`, `TreeLook`, `TreeSwatches`, `TreeMaterials` or
`Assets/Odyssey/Presentation/Shaders/OdysseyTree.shader`.

## 1. What was wrong

Every tree on the board drew in one green over one brown, because there are two tree meshes and
each wore the colour its pack shipped it in. The owner's report, 2026-09-18:

> The trees generated in the world. They need to be a variety of colours — mix in different shades
> brown and variation into this list as the world feels dull and we need to brighten up the trees.

They supplied a table of six themes, four colours each — a deep bark, a warm trunk, a deep canopy
and a fresh leaf — and asked for it to be **baked and made performant**.

The dullness was not an oversight. `ChunkMesher.EmitEdifice` gives a natural edifice **no stuff
tint at all**, deliberately, and says why: a tree is placed with `NaturalContent.StuffWood` because
that is what it is made of, not because it was built from it, so the moment the wood tint became a
brown multiply (2026-09-17, so that a wooden wall stopped drawing as cream plaster) every tree on
the board would have gone brown with it. The right fix for that was to give a tree no tint. The
consequence was that a tree had no colour lever at all.

## 2. Why a tint cannot express a tree, and what can

Measured first, because the mechanism depends on it. `TreeSwatchProbe`
(`scripts/unity.sh exec Odyssey.EditorTools.TreeSwatchProbe.Run`, output in `Logs/tree-swatches.txt`)
clusters the UVs of every tree mesh in PolygonGeneric and samples the atlas inside each cluster.

| | verts | clusters | material | atlas |
|---|---|---|---|---|
| `SM_Gen_Env_Tree_Pine_01` (the conifer) | 1,264 | 5 | `Generic_01_A` | 4096 × 4096 |
| `SM_Gen_Env_Tree_03` (the broadleaf) | 1,366 | 3 | `Generic_01_A` | 4096 × 4096 |

One mesh, one submesh, **one material**, and the trunk and the canopy are different flat cells of
the same atlas. So:

- **A `_BaseColor` multiply — the lever every other module in the game is tinted with — moves both
  at once.** Brown the bark and the leaves go brown. That is the very conflation the stuff tint
  already refuses to make.
- **A recoloured atlas per theme is out on memory.** The atlas is 4096 × 4096: one uncompressed
  copy is 64 MB and this feature wants about a dozen. Measured off the file, not estimated.
- **Repainting the cells in the fragment shader costs four rectangle tests and no memory**, and it
  is legitimate here for the reason it is legitimate for colonists: the probe reports a maximum
  texel deviation of **0** inside every cluster of every tree mesh in the pack. The cells really
  are flat, so replacing the colour inside one throws no art away.

`Odyssey/Tree` is that shader. It is `Odyssey/Character` with the slots renamed and the ink hull
removed — the hull exists because skinned meshes are missing from the depth texture `OdysseyOutline`
reads, and a tree is ordinary instanced geometry the screen-space pass inks perfectly well. The two
are deliberately **not** merged into a shared include: the shared half is thirty lines of rectangle
arithmetic, and one include would have to fix one set of property names for both, which would mean
renaming the character's in a feature the owner has already judged.

### The cells, and the one slot the art does not have

| slot | conifer | broadleaf |
|---|---|---|
| bark deep | trunk, #554B40, u 0.293 v 0.163–0.173 | trunk, #6B5E4E, u 0.272 v 0.168 |
| bark warm | branch stubs, #9B7E5A, u 0.008 v 0.331 | **none** |
| leaf deep | lower needles, #4E543D | lower canopy, #586644 |
| leaf fresh | upper needles, #5E654A | upper canopy, #6A7B52 |

A broadleaf paints its whole trunk from one cell, so a broadleaf theme's warm-trunk colour is not
drawn. That is recorded in a test (`EverySlotButOneHasACellToPaint`) rather than left as a silent
hole, so that adding the cell later is a test that changes rather than a discovery.

The rectangles are **unions over every tree mesh in the pack**, not only the two the catalogue
casts: each mesh sits at a slightly different spot inside the same cell, and a table fitted to
today's casting would silently stop covering the day somebody recasts the wood. Silently is the
word — an uncovered cluster is not an error, it is one tree keeping the pack's colour while its
neighbours change. They are padded by 0.0015 UV because a cluster's rectangle is often degenerate
(483 of the pine's vertices share one exact point); the narrowest gap between two cells a tree uses
is 0.0079, five times the pad, and `TheAtlasCellsOfOneTreeDoNotOverlap` asserts it rather than the
comment claiming it.

## 3. The performance question, which is the whole design

Drawing is bucketed per *(module, part, tint)* within a chunk, so **a tree's colour is a bucket
key** and a draw call is what it costs. Trees are the second most numerous thing on the board.

A colour rolled per tree is therefore the expensive answer, and by a lot: a chunk of woodland holds
about 160 trees, so per-tree rolling saturates the whole palette in nearly every chunk and the bill
is the length of the table. `TreeBucketTests` measures both on the same board.

`TreeLook` deals a colour to a **stand** instead. The board is sown with stand sites on a jittered
lattice and a tree joins the nearest site; each stand is dealt one conifer theme and one broadleaf
theme, and a tree takes the one for its own kind — so the generator goes on mixing pine and
broadleaf inside a wood without a stand of Scots pine sprouting an oak in oak's colours.

It is cellular rather than a plain grid quantisation for a reason worth keeping: a grid draws colour
boundaries with ruler-straight edges running the width of the board, which nothing in a landscape
does and which reads at once as a bug. `StandBoundariesWander` is the test.

**The stand size was measured, not picked.** The bill is (stands overlapping a chunk) × 2 species,
and a chunk is 25 cells:

Measured on a 200 x 200 board carrying 10,403 trees over 64 chunks, palette of 11:

| | tree buckets per chunk | worst chunk | themes drawn |
|---|---|---|---|
| a colour rolled freely per tree | — | **107** | — |
| a handful of 4 per stand — **shipped** | **17.72** | 29 | 130 of 166 |
| one colour per stand, 40-cell stands | 4.34 | 9 | 11 of 11 |
| one colour per stand, 20-cell stands | 6.75 | 10 | |

Three numbers were chosen by measurement rather than by eye. The stand is **40 cells** because at 20
a chunk overlaps about five squares and the wood cost half again as much for no more variety. The
handful is **4** because that is what *"really mix them in together"* asked for, and the row above
it is what it costs. And the free per-tree roll is the row that says why a handful exists at all:
107 colours in the worst chunk is the whole palette arriving in every chunk, and a table that could
never grow again.

The other end of the trade is the board, measured at the size the game actually loads:
`ThePlayedBoardCarriesAWoodWorthLookingAt` reports **89 of the 166 themes on a 120-cell meadow**,
against eleven before this round.

Everything here is a hash of the cell's own coordinates, for the reason `GroundLook` records: a
chunk is re-meshed whenever anything in it changes, so a stream of random numbers would recolour
the wood every time a colonist felled a tree twenty metres away.

**Nothing is saved, hashed or visible to the simulation.** A tree's colour is drawing, in exactly
the sense a colonist's face is. No Def changed, no golden hash moved, and a clone without the packs
draws the same wood it always did.

## 3a. The owner played it, and two things came back (2026-09-18)

> a shorter tree that was white/pale leaves that looked odd … it all needs a much larger variation
> of bark and leaf colours, really vary it up as much as possible … but also really mix them in
> together

**The white tree was a mapping fault, not a bad colour.** The owner's table is authored as a deep
colour plus a *"fresh leaf / highlight"*, which reads as a small bright accent on a mass of the
deep colour. The mesh is the other way round: the probe measures the broadleaf's **upper** canopy
cell at **49.9%** of its vertices against the lower at 24.3%, so whatever colour goes on top *is*
the tree — and the shorter tree is exactly the broadleaf, 6.15 m against the pine's 9.47 m. Silver
Birch's highlight #8F9779 and Mossy Birch's #9CAF88 measure luminance 145 and 151; painted over
half a tree they are a pale sage tree.

So a colour is now **two faces of one colour — lit and shaded — rather than a mass and an accent**,
and how far apart the faces may be is taken from the art rather than invented. `TreeToneRules`
holds the bands and three tests enforce them:

| | measured in the pack | the band |
|---|---|---|
| leaf step (lit ÷ shaded luminance) | 1.21 on both meshes | 1.10 – 1.45 |
| bark step | **1.68** (trunk #554B40 against branch stub #9B7E5A) | 1.10 – 2.20 |
| brightest leaf lit face | 110.9 | ≤ 132 |

**Bark keeps a band of its own and that was found, not decided.** Holding trunks to the canopy's
band rejected six entries including three of the owner's — Scots Pine at 2.13, Redwood at 1.79,
Ancient Oak at 1.58 — and measuring the pack's own trunk pair settled it at 1.68 in the owner's
favour. A trunk is a cylinder with a lit side; a canopy is a cloud of leaves and has no such thing.

**"Vary it up as much as possible" is answered by a cross product.** Eleven hand-written themes
became **fourteen leaf tones and eight bark tones for broadleaves (112 combinations) and nine
against six for conifers (54)** — 166 themes from twenty-two readable lines, where writing 166 by
hand would have been 166 chances to author another white tree. The owner's six survive as the tones
they were built from, and the cross product contains their original pairings along with every
other. This costs nothing: the length of the table was never what a wood costs.

**"Really mix them in together" is what does cost.** A stand used to deal *one* colour, which made
a wood of uniform patches. A stand now deals a **handful** — `TreeLook.ThemesPerStand` — and each
tree picks one of them by its own hash, so neighbouring trees differ while neighbouring woods are
different mixtures. That is the one number that decides the bill: every step of it multiplies the
tree buckets in a chunk. The handful is drawn without replacement, by walking the species' rows
from a hashed start at a hashed coprime stride, because four independent hashes would hand the same
colour out twice about one stand in ten and quietly narrow the mixing.

## 4. The palette

`TreePalette`. The first six rows are the owner's, verbatim, down to the hex. The other five are
ours, added under the same instruction, and they are the ones to veto first if the board comes out
gaudy: a Golden Aspen and a Hazel Thicket that reach the bright yellow-green the reference art uses
for grass, a Copper Beech that puts **brown in the canopy** rather than only in the trunk, and two
more conifers because the owner's six were two conifers against four broadleaves while the board
rolls roughly as many of one as the other.

A theme belongs to exactly one species, and the tests pin four invariants that fail silently if
broken: the four colours of a theme are distinct, the highlight of each pair is genuinely lighter
than what it sits on (or the tree is lit from underneath), every theme belongs to one species, and
no two atlas rectangles overlap.

## 5. Where it is wired

| | |
|---|---|
| `TintCode.TreeBase` (bit 12) | marks a bucket as a tree; the value is a theme index |
| `ChunkMesher.EmitEdifice` | a tree takes `TintCode.Tree(TreeLook.ThemeFor(...))` instead of `StuffNone` |
| `ChunkRenderer.DrawBuckets` | a tree bucket resolves through `MaterialCache.Trees`, everything else as before |
| `ChunkRenderer.ResolveColour` | the fallback colour: white over art (exactly what a tree drew before this), the theme's canopy over a primitive |
| `TerrainSkirt.Survey` / `NewBatch` | the surround samples the board's stands by frequency and repaints its own trees the same way, haze and all — or the wood would change colour at the rim, which is the one thing the surround exists to prevent |
| `MaterialCache.Trees` | owns `TreeMaterials`, so everything that already holds a material cache can reach it and disposal stays in one place |

A ghosted tree is **not** repainted: it is drawn by the translucent stand-in, which has no atlas.
Nor is a fallback primitive, which has no atlas either — that one takes the theme's canopy as a
plain tint, so a clone without the packs gets a green wood instead of a grey one.

## 6. The fidelity gap, measured and left open

`TreeCheck`'s `plain` column — our shader with the repaint at zero — is **not** identical to
`pack`. Measured off the contact sheets on the played board:

| | trunk (mean sRGB) | conifer canopy |
|---|---|---|
| `pack`, `Synty/Generic_Standard` | 51.7, 45.2, 39.5 | 91.5, 107.7, 62.6 |
| `plain`, `Odyssey/Tree` at strength 0 | 46.2, 39.2, 31.8 | 79.2, 96.3, 53.8 |
| the meadow beside them, as a control | identical to the last digit in every column | |

So **`Odyssey/Tree` draws a tree about a tenth darker in sRGB — a quarter to a third darker in the
linear space the albedo multiply lives in — than the pack's own shader draws the same tree.** The
meadow row is the control that says nothing else moved.

Three explanations were tested and all three are dead:

- **Emission.** The pack material has an emission map and `_Enable_Emission = 1`, which our shader
  originally dropped. It is carried now, and it changed the picture by **nothing at all**, because
  `_Emission_Color` is black. Carried anyway: a clone that keeps everything the source had is one
  fewer thing to suspect next time.
- **The normal map.** Shooting the `flat` column with `_Normal_Amount` forced to zero produced an
  image **identical to `plain` byte for byte**, so the normal map contributes nothing to a tree at
  this distance and cannot be the difference.
- **Screen-space ambient occlusion**, which is active in `PC_Renderer` at intensity 0.4 and is
  applied through a shader keyword, so a shader that declares it is darkened and one that does not
  is not. `TreeSwatchProbe.CompareShaders` prints both keyword sets: **both declare it.**

What is left is that the two shaders implement lighting differently. The pack's is a Shader Graph
carrying a *built-in* target as well as a URP one (`DIRECTIONAL`, `SHADOWS_SCREEN`,
`VERTEXLIGHT_ON`, `UNITY_HDR_ON` are all in its keyword space and in none of ours); ours calls
`UniversalFragmentPBR` directly. Chasing that to zero means reverse-engineering the graph, which is
the licensed-content boundary the working agreement draws, and this is where the chase stopped.

**It was deliberately not compensated for.** A uniform gain on `_BaseColor` would have cancelled
most of it — the slot is already there, since the depth shade rides in it — but the correction is
not uniform (blue needs 1.24 where red needs 1.12), and a fudge factor calibrated from two
rectangles of one frame is exactly the sort of number this project is right to distrust. **The
honest brightness lever is the palette**, which is one table and the owner's. The same lighting
difference already exists in the game between colonists (`Odyssey/Character`, the same
hand-written lighting) and the walls they stand beside, and nobody has reported it.

## 7. What nobody has judged

- **Nobody has pressed Play on it.** `scripts/unity.sh shot Odyssey.EditorTools.TreeCheck.Run`
  writes `Logs/tree-{pack,plain,themed}-{play,wood,close}.png` and that is all anybody has looked
  at. `pack` is the wood as the game drew it before this; `plain` is our shader with the repaint
  at zero, which is the fidelity control — if `plain` and `pack` differ then `Odyssey/Tree` is not
  drawing a Synty tree the way Synty's shader does, which is a different bug from a palette
  somebody dislikes.
- **The five themes we added are open to veto**, and so is the order of the table.
- **Whether a 100 m stand is the right size to look at** is a judgement a still can make badly: the
  number was chosen on the bucket bill and checked for variety, not for whether a wood reads as a
  wood. The lever is one constant, `TreeLook.StandCell`.
- **A broadleaf's warm-trunk colour is not drawn**, above. Whether that matters is something only
  the picture can say.
- **Within a stand every tree of a species is the same colour.** The obvious next variation — a
  minority of trees in a stand taking a second theme — was considered and left out, because it is
  exactly the cost this design exists to avoid and it should be bought with a measurement rather
  than added on the way past.
