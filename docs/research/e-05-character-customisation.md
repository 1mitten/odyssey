# Lane E5 — Character customisation: what the packs allow, and what we built

Researched and built 2026-09-16, from the owner's question: *is there a generic Synty model we can
create clothes for and wear, or do we need to use existing models — and if so, can we change the
colour/theme of their clothes and hair at least?*

## Question

What are the real options for customising Synty POLYGON characters — modular bodies, swappable
garments, and recolouring clothes, hair and skin per colonist?

## Findings

### There is no generic body, and nothing below the neck is separable

- **A character is one skinned mesh from scalp to boots, with one material.** 69 bodies across four
  packs. No head/torso/arms/legs split exists anywhere in any owned pack.
- **All four packs share one 49/50-bone Mecanim Humanoid rig with byte-identical bone names**
  (`Hips`, `Spine_01..03`, `Clavicle_L/R`, … `Toes_L/R`), so anything rigged for one pack fits every
  other.
- **Whole-body swapping is already free**: every character prefab contains *every* body in its pack
  as sibling `SkinnedMeshRenderer`s on one skeleton with all but one `m_IsActive: 0`.
- **Separable pieces exist only above the neck** — 22 attachment prefabs in PolygonGeneric
  (9 hairstyles, bun, ponytail, beanie, 2 hats, 2 beards, chops, moustache, 3 headsets, sunglasses,
  hood) and 8 in PolygonFarm. PolygonGeneric wears hair as a separate skinned child; Sci-Fi,
  Western Frontier and Farm bake it into the body.
- **No Synty customisation tooling ships in any owned pack.** 17 C# files under `Assets/Synty/`,
  none of which touches character assembly. No character-creator scene, no modular demo.
- Each pack ships 12–24 **alternate atlases**: the same UV layout repainted. This is Synty's own
  recolour mechanism and it recolours the whole character at once, skin included.

### Synty Sidekick is the only route to real modular garments

Launched 13 Nov 2024, a parallel product to the POLYGON line: 1,400+ mix-and-match parts, body and
facial blend shapes, per-material colour in the editor, a **Unity runtime API for changing outfits**,
and a bake-to-one-SkinnedMeshRenderer step. Free Starter Pack; themed packs about £/€184 each, or
SyntyPass at $30/mo. It is a different rig (bridged by Mecanim retargeting) and a separate material
set. **The owner declined it** — no purchases, no new art.

POLYGON Modular Fantasy Hero (720 modular pieces, a colour-slot shader) is the POLYGON line's only
modular pack and is the wrong genre; its parts do not interoperate with Sci-Fi City.

### The load-bearing discovery: the atlas is a labelled palette

The pack atlas contains an explicitly labelled **`Character Colours`** block — a grid of small flat
swatch cells, with `Vegetation`, `Wood` and `Metal` blocks beside it and a skin-tone column beneath.
**Every garment, hair patch and skin region on every body is UV-mapped onto one of those flat
cells**, so a vertex's cell already *is* its material identity. Per-garment recolouring therefore
needs no mask texture, no authored ID channel, no mesh surgery and no new art.

### Measurements

`SwatchProbe` (`Assets/Editor/Odyssey/SwatchProbe.cs`) clusters a body's UVs and reports, per
cluster, the vertex count, the rectangle, the bone mix and the colour deviation inside it.

- **Flatness is 0 on every cluster of eight bodies across all four packs**, including the robot and
  the cop. The cells really are flat, so replacing the colour and sampling a different cell are the
  same picture — and replacing is strictly better (no second fetch, no mip or derivative
  consequence, and the palette stops being limited to the colours Synty painted).
- **The one exception proves the rule.** A single cluster reported a deviation of 64: the Native
  American warrior's war paint, whose rectangle spans 0.0165 against a real cell's 0.002–0.007. It
  is two cells the merge tolerance bridged, not a patterned cell. `MaxSlotExtent` now rejects those.
- **Skin is painted in three different places.** The column every pack shares (u 0.008–0.029,
  v 0.183–0.190), plus one for Western Frontier's Native American characters at (0.2746, 0.1537) and
  one for the Sci-Fi cyborgs at (0.2591, 0.1075). A single column classified fourteen bodies as
  having no skin showing, seven of them bare-chested warriors.
- **`isReadable` is 0 on three of the four character FBXs**, so `Mesh.uv` is unavailable at runtime
  for 43 of the 61 bodies. In the editor every mesh is readable, which is why classification happens
  at catalogue-build time and what ships is a handful of rectangles per row. No import setting is
  touched: those live in gitignored `Assets/Synty/` and would not reach CI or another clone.

### Classification, over all 61 bodies

**55 Full, 5 no skin showing, 1 clothing only.** Every body recolours its clothing. The five without
skin are a helmeted cop, a masked cyborg ninja, a gowned medic and two aliens — all correct; the
sixth is an android with neither skin nor hair.

## Recommendation, and what was built

**Our own character shader that replaces the colour inside a UV rectangle**, with the rectangles
discovered by clustering each body's UVs at editor time and committed to `ModuleCatalogue.asset` as
numbers. Rejected: cloning each body mesh with rewritten UVs (it collapses the figure pool, saves no
instancing buckets, adds a second population of script-created meshes, and needs import state in a
gitignored folder); per-look atlas copies (67 MB each at 4096²); the whole-mesh `_BaseColor`
multiply (one colour for the whole person, skin included).

Built: `ColonistAppearance` / `ColonistPalette` / `ColonistAppearanceBook`, `OdysseyCharacter.shader`,
`CharacterSwatches` (classifier), `ColonistMaterials` (one material per look), and `ColourCheck`
(five contact sheets). Appearance is derived from the world seed and the pawn id, so the same world
deals the same people on every load; nothing is saved and nothing is hashed.

## Sources

- Local: `Assets/Synty/Polygon{Generic,SciFiCity,WesternFrontier,Farm}/` prefabs, materials,
  textures and `.fbx.meta`; measurements by `SwatchProbe` and `CharacterSwatches`.
- `docs/research/e-02-characters-animation.md` (rigs, clip coverage), `e-04-tint-strategy.md` (the
  committed tint strategy and why MaterialPropertyBlock is rejected project-wide).
- https://syntystore.com/blogs/blog/introducing-sidekick-character-creator (Sidekick, 13 Nov 2024)
- https://syntystore.com/products/sidekick-modular-characters-starter-pack (free starter pack)
- https://syntystore.com/products/polygon-modular-fantasy-hero-characters (the POLYGON modular pack)
- https://syntystore.com/pages/one-time-purchase-licence (EULA: editing and shipping permitted,
  redistributing source files is not)

## Confidence

**High** for everything measured on disk — modularity, the shared rig, the atlas layout, flatness,
the three skin columns, the classification tally, and that the shader places colour correctly (the
magenta contact sheets show it). **High** for Sidekick's existence and contents (Synty's own store
pages, cross-checked against the Asset Store). **Medium** on whether Sidekick's skeleton shares bone
names with the POLYGON rig — inferred from Synty shipping separate animation variants, not verified.

## Could not be determined

- Whether the palette reads well. It is deliberately muted, and the parade sheet is a different
  picture from the control but only just. That is an owner judgement from the contact sheet.
- Whether any body outside the eight probed uses a *fourth* skin swatch. The classification tally is
  the detector: five bodies report no skin and all five are genuinely covered.
- Frame cost. `FrameTimeTests` has not been run on this change.
