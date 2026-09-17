# Lane E6 — POLYGON Adult Face Plates: can we put faces on our colonists?

Measured on disk 2026-09-17, from the owner's question: *can we apply this to our Synty models —
what are the options?*, handed the file
`POLYGON_AdultFacePlates_Unity_2022_3_v1_0_0.unitypackage`.

## Question

What does the free Synty **Adult Face Plates** pack actually contain, can it be fitted to the 61
POLYGON bodies our colonists already use, and what would it cost?

## Findings

### The pack is geometry only. It ships no face.

Unpacked and enumerated: **four assets, 22 KB total.**

| Asset | What it is |
|---|---|
| `Models/SM_Chr_Faceplate_Adult_Male.fbx` | static mesh, 10 vertices, 4 quads |
| `Models/SM_Chr_Faceplate_Adult_Female.fbx` | static mesh, 9 vertices, 4 quads |
| `Prefabs/SM_Chr_Faceplate_Adult_Male.prefab` | the mesh with a material override |
| `Prefabs/SM_Chr_Faceplate_Adult_Female.prefab` | the same |

**There is no texture and no material.** Both prefabs override `m_Materials.Array.data[0]` to
material GUID `fa7b86b966ce1d54389c840b6083cb0f`, which **exists in no pack we own** — grepped
across all nine installed packs. On import the plates will draw with a missing material.

The FBX's own embedded texture node names what Synty authored it against:
`Polygon_Kids_Texture_Facial_Expression_Disgust_01`, wired to the material's `DiffuseColor` **and**
`TransparentColor` channels. So Synty's faces live in **POLYGON Kids**, which we do not own, and the
intended material is an **alpha-cut overlay**, not an opaque surface.

**The face art does not exist and has to come from us.** That is the whole cost of this feature;
everything else is small.

### The plate is a curved 4-quad shell, baked in bind-pose space

Measured from the binary FBX directly (parser in the scratchpad; FBX 7500, deflate-packed arrays):

| | Male | Female |
|---|---|---|
| Vertices / polygons | 10 / 4 | 9 / 4 |
| Bounds X (cm) | −8.87 … 8.87 | −8.87 … 8.87 |
| Bounds Y (cm) | 150.88 … 173.32 | 154.32 … 173.36 |
| Bounds Z (cm) | 10.58 … 13.53 | 10.18 … 13.05 |
| `animationType` | 0 (**no rig**) | 0 (**no rig**) |

Three things follow.

- **It is about 17.7 cm wide and 22 cm tall** — a face-sized shell, bowed forward 10–13.5 cm, which
  is the curvature of a POLYGON head. Getting that curve right by eye is the one thing the pack
  gives us that we would otherwise have to guess.
- **It is authored at head height in the character's bind-pose space**, not at the origin — Y 151
  to 173 cm is where a standing adult's face is. The prefab zeroes its own transform, so dropped
  in as a sibling of the body it lands correctly **in bind pose and then stays there while the head
  moves.** To ride the head it must be parented to the `Head` bone and given the local transform
  that cancels that bone's bind pose. One line at figure-build time, not a problem.
- **It is a static mesh, not skinned**, so it costs no skinning and cannot deform. An expression
  change is a texture change, never a blend shape.

### One plate = one whole texture, so expressions are a texture swap

The 9 distinct UVs span **U 0.093–0.907, V 0.005–0.902** — effectively the full 0–1 square. There
is no sub-rectangle and no grid. **One texture is one expression**, which matches the Synty file
name (`..._Disgust_01`) being a single mood.

For us that means either one material per expression, or — better, and the way the rest of this
renderer already works — **our own atlas with the expressions in a grid, selected by a UV offset
pushed per material**, exactly as `ColonistMaterials` already pushes swatch rectangles.

### The face *cannot* be painted into the existing recolour path. This is the finding that matters.

`e-05-character-customisation.md` established that every skin surface on every body is UV-mapped
onto **one flat swatch cell** in the shared `Character Colours` block — measured at u 0.008–0.029,
v 0.183–0.190, which on the 4096 × 4096 character atlas is about **86 × 29 pixels**, and measured
flat (deviation 0) on every cluster of every body tested.

So the head, the hands and the forearms all sample the same few dozen pixels. **Painting eyes into
that cell would put eyes on the backs of the hands.** There is no head-specific UV region to paint
into, on any of the 61 bodies.

That closes off the cheap route and leaves added geometry as the only way to give a colonist a
face. Which is precisely why this pack exists.

### Our side is ready for it

- **The rig is shared and already bound.** All four character packs use one 49/50-bone Mecanim
  Humanoid rig with identical bone names (E5), and `PawnFigureDirector.Tools.cs` already resolves
  bones through `Animator.GetBoneTransform(HumanBodyBones.…)`. `HumanBodyBones.Head` needs no new
  machinery.
- **Attachment has a precedent in the same file.** The held axe/pick/hammer is an ordinary child of
  the hand bone, fitted at figure-build time in `PawnFigureDirector.Create`. A plate on the head
  bone is the same shape of thing, and simpler — it never swaps and never hides.
- **Identity already survives the figure cap.** `ColonistLook` / `ColonistAppearanceBook` exist so
  the live-figure drawer and the baked instanced drawer cannot disagree about who somebody is. A
  face index belongs in the book beside the body index, for the same reason and with the same test.
- **`MaxFigures` is 64**, so in a colony of the size we simulate *every* colonist has a live
  figure. The instanced fallback path only carries pawns past 64, at which distance a 17 cm plate
  is not the thing to spend work on. The feature can ship on figures alone.

### The licence position is the ordinary one

The plates are Synty content, so they install to `Assets/Synty/PolygonAdultFacePlates/` and are
gitignored with everything else under `/Assets/Synty/`. Presentation may depend on them the way it
already depends on `ModuleLibrary` — **with a fallback**, so a clone with no packs still builds and
runs headless and simply keeps today's blank faces. The **face texture would be ours**, authored
outside `Assets/Synty/` and committed, which keeps the one asset we have to make in the repository
where CI can see it.

### Installed

The four assets are unpacked into `Assets/Synty/PolygonAdultFacePlates/` by hand (asset + meta per
`pathname` entry) so their GUIDs are preserved. Unity was open at the time and will import them;
expect **one missing-material warning per prefab**, which is the dangling GUID above and not a
mistake in the unpacking.

## Recommendation

**Fit the pack's plate to the head bone, and draw our own expression atlas into it — but shoot the
visibility test first, because it can cancel the whole thing for the world view.**

Ranked:

1. **Pack mesh + our own expression atlas** *(backed)*. Uses the one thing the pack is actually
   worth — a correct POLYGON head curvature we would otherwise eyeball — and puts the only real
   cost, the art, in a file we own and commit. Degrades to today's blank face when the pack is
   absent.
2. **Generate our own plate mesh in code.** We already build meshes procedurally
   (`PrimitiveMeshes`, `BankMesh`, `RockMesh`) and 4 quads is nothing, so this would be
   *committable* and free of the gitignore dance entirely. It loses only the measured curvature —
   and we may not transcribe Synty's vertices to get it, so it means fitting by eye against 61
   heads. Worth doing **only** if the gitignored dependency ever becomes a real problem; it is not
   one today, because presentation already has several.
3. **Buy into Synty Sidekick** for real facial blend shapes. Already declined by the owner (E5) and
   still the wrong trade.
4. **Do nothing.** Blank faces are on-theme for POLYGON and may be the honest answer for the world
   camera — see the experiment below.

**The observation that breaks the tie, and the cheapest experiment that gets it.** The play camera
sits **32–160 m above the ground**. Nobody has measured whether a 17 cm plate is more than a couple
of pixels at those stops, and this project has already been bitten once by pixel art that stopped
reading below 32 px (ADR 0007). So: **attach one plate to one colonist with a placeholder face and
take the contact sheet at the nearest and farthest camera stop.** One `scripts/unity.sh shot` run.

If it does not read in the world, the feature is **not** dead, and this is the second half of the
recommendation: it moves to where the camera is close and faces are the entire point — the **U41
colonist select screen**, three portraits rendered large, and the roster card. That is a screen
whose job is to make three candidates feel like three people, and which currently has nothing to
distinguish them but hair colour, because colonists start every skill at 0. **Faces pay for
themselves there whether or not they survive the world camera**, so the experiment decides scope,
not go/no-go.

Two smaller things the build will hit, recorded now so they are not rediscovered:

- **Not every body should get a face.** 61 bodies include helmeted, masked and cyborg heads, where
  a plate would sit on top of a visor. That is a per-row flag, and it belongs in the catalogue
  build where `SwatchProbe` already classifies bodies, not in a hand-kept list.
- **Two plates, male and female, with different heights** (Y starts at 150.9 vs 154.3 cm). The
  catalogue must know which body takes which, and that is the same per-row decision.

## Sources

Local measurement only — no web research was needed or done.

- `POLYGON_AdultFacePlates_Unity_2022_3_v1_0_0.unitypackage` (owner-supplied, `~/Downloads`),
  unpacked and parsed.
- FBX 7500 binary read directly (node tree, `Vertices`, `PolygonVertexIndex`, `LayerElementUV`,
  `Connections`, `Video`/`Texture` nodes).
- `Assets/Synty/**` grepped for the referenced material GUID and for any facial-expression texture:
  no match.
- `docs/research/e-05-character-customisation.md` for the atlas swatch geometry and the shared rig.
- `Assets/Odyssey/Presentation/World/PawnFigureDirector*.cs`,
  `Assets/Odyssey/Presentation/Rendering/Colonist*.cs` for the attachment precedent and the
  two-drawer rule.

## Confidence

**High** on everything measured on disk: the pack's contents, the missing texture and material, the
mesh dimensions, the absence of a rig, the full-square UVs, and the impossibility of painting a
face into the shared skin swatch. All of it was read out of the files rather than inferred.

**Medium** on the fit: that the plate parents cleanly to `HumanBodyBones.Head` with one bind-pose
compensation is reasoned from the geometry and the shared rig, not yet seen on screen.

## Could not be determined

- **Whether a face reads at the play camera's distance.** Unmeasured, and it is the experiment
  above. Nobody has pressed Play on this or anything else in the look line.
- **Which of the 61 bodies have heads a plate suits.** Needs the catalogue pass, or eyes on a
  contact sheet.
- **What Synty's own face textures look like**, since they ship in POLYGON Kids, which we do not
  own. We are authoring ours blind of theirs, which is the clean-room position anyway.
