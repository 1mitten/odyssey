# Lane E4 — Atlas and tint strategy: runtime "stuff" colours on instanced Synty modules

Researched 2026-09-15 (local file inspection on the Windows dev machine plus web checks). Constraint from the brief: every building module takes a runtime "stuff" tint at the scale of tens of thousands of instanced modules, without per-instance materials.

## Question

How do the imported Synty packs texture and colour their meshes (shared atlas? which UVs? vertex colours?), what parameters do the Synty/Generic_* Shader Graphs expose, and what is the best runtime tint strategy compatible with GPU instancing / `RenderMeshInstanced`?

## Findings

### How the packs texture their meshes

- **One shared colour atlas per pack series, sampled on UV0, no vertex colours.** Every texture sample in `Generic_Basic.shadergraph` uses UV channel 0, and the graph contains no vertex-colour node. Colour comes entirely from the albedo atlas; the meshes are UV-mapped onto flat colour blocks in it.
- **Sci-Fi City ships four base atlases with six pre-painted colour schemes each.** `Assets/Synty/PolygonSciFiCity/Textures/Alts/` holds `PolygonScifi_{01..04}_{A..F}.png` (24 albedo variants of the same layouts — same UVs, different paint). Each has a matching material in `Materials/Alts/PolygonScifi_XX_Y.mat`. This is Synty's own recolour mechanism: **swap the atlas/material, keep the UVs**. Alongside them: per-series emissive atlases (`Textures/Emissive/PolygonScifi_0X_Emissive.png`), one shared normal map (`Textures/Normals/PolygonSciFiCity_01_Normals.png`), a metallic mask, and small `Misc/` textures for roads, signs, billboards, FX.
- A representative building material (`PolygonScifi_01_A.mat`, shader `Synty/Generic_Basic`) binds exactly `_Albedo_Map` (the variant atlas), `_Emission_Map`, `_Normal_Map`; `_BaseColor` is white (1,1,1,1), `_Alpha_Clip_Threshold` 0.5 with alpha test on, `_Metallic` 0, `_Smoothness` 0.2. So in shipped materials the tint is at identity.

### What the Shader Graphs expose

`Synty/Generic_Basic` (110 of 234 materials — the building-module shader):

| Reference name | Type | Role |
|---|---|---|
| `_BaseColor` | Colour | **Multiplied over the albedo atlas sample** (verified by tracing the graph: PropertyNode(BaseColor) → Multiply(B), albedo sample → Multiply(A), product → BaseColor surface output) |
| `_Albedo_Map` | Texture2D | the colour atlas, UV0 |
| `_Normal_Map`, `_Normal_Amount` | Texture2D, float | normals |
| `_Metallic`, `_Smoothness` | float | surface |
| `_Emission_Map`, `_Emission_Color`, `_Enable_Emission` | Texture2D, colour, bool | emissive (separately tintable) |
| `_Alpha_Clip_Threshold` | float | cutout |

Every property has `overrideHLSLDeclaration: false`, i.e. all land in the per-material `UnityPerMaterial` CBUFFER: **SRP Batcher-compatible, nothing declared per-instance.**

Other shaders: `Synty/Generic_Decals` (59 mats) exposes `_Base_Map`, `_Tint`, `_Albedo_Strength`, `_Normal_Map`/`_Normal_Strength`/`_Normal_Blend`, `_Metallic`, `_Smoothness` — decals also have a multiply tint. The legacy `Synty/PolygonShader` (3 mats; GUI in `InterfaceOverrides/Editor/PolygonShaderGUI.cs`) exposes `_Color_Tint` plus base/overlay/alpha/emission texture groups, a full triplanar-dirt group (`_Enable_Triplanar_Texture`, top/bottom/side textures, fade, intensity), snow and wave groups. Sci-Fi City's dirt demo uses the separate `Synty/Polygon_Triplanar`. 14 materials use stock URP/Lit (whose `_BaseColor` tints the same way).

### Instancing and batching constraints (URP 17 / Unity 6)

- `Graphics.RenderMeshInstanced` renders up to 1023 instances per call (≈511 with the default two matrices per instance); extra per-instance data must be supplied as `MaterialPropertyBlock` arrays **and the shader must declare those properties with `UNITY_DEFINE_INSTANCED_PROP`** — which Shader Graph does not generate for ordinary exposed properties. Stock Synty graphs therefore support instanced transforms but **not per-instance colours within one call**.
- Shader Graph's route to per-instance data is the property setting *Override Property Declaration → Hybrid Per Instance*, which targets the **DOTS instancing** path (BatchRendererGroup / Entities Graphics / GPU Resident Drawer), not classic `MaterialPropertyBlock` float arrays. A bug in 2022.1–2023.1 put such properties in the CBUFFER anyway (broken); it is fixed in current versions.
- For ordinary GameObject renderers the SRP Batcher takes priority, and it batches by **shader variant, not by material** — "you can still use as many different materials with the same shader as you want". A renderer carrying a `MaterialPropertyBlock` is excluded from SRP Batcher batching.
- Net: per-instance MPB colours would break SRP batching on GameObjects and do nothing inside a `RenderMeshInstanced` call; per-call uniforms and per-stuff materials keep both paths fast.

### Art caveat on multiply tinting

`_BaseColor` is a multiply: it can darken and hue-shift but never lighten, and saturated atlas blocks tint poorly. Tintable "stuff" modules should be UV-mapped to the most neutral/desaturated blocks (or the most neutral of the A–F variants). Which blocks those are needs a look at the atlas in the editor (see *Could not be determined*).

## Recommendation

**Committed: one cached material per stuff, buckets per (mesh, stuff) — never a material or MPB per instance.**

1. Presentation keeps a material cache keyed `(base Synty material, stuff)`. Each entry is instantiated once from the shared base with `_BaseColor` set to the stuff colour (and `_Emission_Color` where relevant). Tens of stuffs → tens of materials, created lazily at runtime; nothing is committed, nothing depends on Synty in the Sim assembly (the sim only knows a stuff RGBA).
2. **Static placed modules (GameObject/scene path):** assign the cached material directly. The SRP Batcher batches all of them because they share one shader variant; no MaterialPropertyBlocks anywhere.
3. **Massed rendering path:** group module instances by `(mesh, stuff-material)` and issue one `Graphics.RenderMeshInstanced` per bucket (chunked at the ~511–1023 instance ceiling), with *Enable GPU Instancing* ticked on the cached material. The tint is a per-call uniform, so the stock `Synty/Generic_Basic` shader works completely untouched.
4. Cost model: draw calls ≈ #module meshes × #stuffs in view × chunks. With ~30 module meshes and ~15 stuffs that is a few hundred cheap instanced calls for tens of thousands of modules — well within budget on the 2022 mid-range laptop target. Per-instance effects (selection, damage flash, blueprint ghost) are rendered as their own small buckets or an overlay pass, not by varying colour inside a bucket.

Cheapest validating experiment (Lane D spike): 20,000 wall modules via `RenderMeshInstanced` in two stuff colours; Frame Debugger must show ~40 instanced draws and no magenta — confirms Synty SG shaders accept the instanced path on this URP version.

**Fallback (only if bucket counts explode or per-instance overlays must combine with stuff tint): own-authored instanced-tint graph + BatchRendererGroup.** Author from scratch — *never* copy or fork the Synty `.shadergraph` files out of `Assets/Synty/` (licence rule) — a small URP Lit graph under `Assets/Art/Shaders/`: albedo atlas × `_TintColor`, emissive × `_Emission_Color`, with `_TintColor` declared *Hybrid Per Instance*, rendered through BatchRendererGroup (DOTS instancing). That gives a true per-instance colour in one giant batch, at the price of adopting BRG plumbing and re-pointing module materials at our shader. It is committed to the repo and takes any atlas, so headless clones without Synty still compile.

Rejected: per-instance `MaterialPropertyBlock`s (break SRP batching, unusable as arrays with Shader Graph); UV-offset palette shifting at runtime (per-instance UV offset has the same instancing problem and Synty's atlas blocks are not laid out as a regular palette grid); atlas swapping per stuff (only six pre-painted schemes exist, and 24 × 2048² albedo variants cost memory without covering arbitrary stuff colours).

## Layer questions touched

None directly. One note for the cut-away/dimming question: because tint is already a per-bucket uniform, per-layer dimming (darkening layers above the camera slice) can ride the same mechanism — extend the bucket key with the layer-visibility state or multiply a per-call dim factor into `_BaseColor` — still with zero per-instance data.

## Sources

Local files (parameter names and structure only; no shader or texture content copied):

- `D:\code\odyssey\Assets\Synty\PolygonGeneric\Shaders\Generic_Basic.shadergraph` (properties, wiring, UV channels, HLSL declarations)
- `D:\code\odyssey\Assets\Synty\PolygonGeneric\Shaders\Generic_Decals.shadergraph` (property reference names)
- `D:\code\odyssey\Assets\Synty\PolygonGeneric\Shaders\InterfaceOverrides\Editor\PolygonShaderGUI.cs` (legacy PolygonShader parameter set)
- `D:\code\odyssey\Assets\Synty\PolygonSciFiCity\Materials\Alts\PolygonScifi_01_A.mat` (bindings and defaults)
- `D:\code\odyssey\Assets\Synty\PolygonSciFiCity\Textures\` (Alts/Emissive/Normals/Misc layout)
- `D:\code\odyssey\docs\research\synty-inventory.md` (shader/material/texture census)

Web:

- https://docs.unity3d.com/ScriptReference/Graphics.RenderMeshInstanced.html (instance ceiling, MPB arrays for per-instance data)
- https://docs.unity3d.com/Manual/GPUInstancing.html and https://docs.unity3d.com/6000.3/Documentation/Manual/gpu-instancing-enable.html (SRP Batcher priority, Enable GPU Instancing)
- https://docs.unity3d.com/2022.3/Documentation/Manual/SRPBatcher.html and https://docs.unity3d.com/6000.0/Documentation/Manual/SRPBatcher-Incompatible.html (batches per shader variant across materials; MPB renders a GameObject SRP Batcher-incompatible)
- https://issuetracker.unity3d.com/issues/shader-graph-shader-instance-property-gets-added-to-cbuffer-which-causes-gpu-instancing-with-instanced-properties-to-not-work (Hybrid Per Instance CBUFFER bug, 2022.1–2023.1)
- https://discussions.unity.com/t/shader-properties-per-instance/770230 (Shader Graph has no classic per-instance property path; DOTS/Hybrid route)
- https://www.cyanilux.com/tutorials/gpu-instanced-grass-breakdown/ (instancing with SG in URP; Unity 6 InstanceID note)
- https://community.gamedev.tv/t/a-trick-for-recoloring-synty-assets/245419 and https://discussions.unity.com/t/changing-color-in-scripting-synty-package/795104 (community Synty recolour practice: UV regions / atlas edits / material tint)

## Confidence

**High** for everything read from local files (atlas layout, exposed parameters, `_BaseColor` multiply wiring, UV0-only, no vertex colours, CBUFFER declarations). **Medium-high** for the instancing/batching behaviour claims (multiple concurring sources, but `RenderMeshInstanced` with Synty's Shader Graph materials on URP 17.3 should be confirmed by the 20k-wall spike before M0 locks the renderer design).

## Could not be determined

- The pixel layout of the colour blocks inside `PolygonScifi_0X` atlases (which regions are neutral enough to tint well, whether any gradient strip exists). Needs a two-minute look at the texture in the editor — no image data belongs in this doc. Feeds the e-01 module → tintable-region mapping.
- Whether the building meshes carry a second UV set (would matter only for a future lightmap or overlay channel; nothing in Generic_Basic uses one).
- Exact per-instance ceiling for `RenderMeshInstanced` under this URP version's default instance data (511 vs 1023) — the spike's Frame Debugger run will show it.
