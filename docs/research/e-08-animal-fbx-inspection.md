# e-08 — Inspection of `Pig.fbx` and `Rat.fbx` as the first animal art

**Phase:** Ground, for the animals unit (systems catalogue §9, milestone M5, pulled forward by
the owner on 2026-09-22). **Status:** done 2026-09-22, read straight from the two files with a
binary-FBX reader (`tools/`-free, standard library only); nothing has been imported into Unity
yet, so the in-editor size and the Generic-avatar mapping are still to be measured.

## Question

What are the two FBX files the owner supplied (`C:\Users\timjo\Downloads\Pig.fbx`, `Rat.fbx`),
and are they enough to stand an animal on the board — rigged, animated, sized to the cell, and
drawable through the same figure path the colonists use?

## Findings

### Both files, in one table

| | Pig | Rat |
|---|---|---|
| File | 360 KB, binary FBX 7400 | 1.18 MB, binary FBX 7400 |
| Made in | Blender 2.79 (stable FBX IO 3.7.17) | same |
| Mesh | `Pig`, 283 verts, 298 polys (562 tris) | `Rat`, 1,998 verts, 2,002 polys (4,004 tris) |
| Bones | 35 (24 skinned clusters + `_end` leaves) | 42 (31 skinned + `_end` leaves), 7 of them tail |
| Rig root | `root` → `Body` → `Hips`/`Torso`/`Back`/`Shoulders`/`Neck`/`Head`, four legs `Up`/`Low`/`Foot` `.L`/`.R` | identical convention plus `Tail1`–`Tail7` |
| UV layer | **none** | **none** |
| Textures | none | none |
| Materials | 2 flat colours: pink `(0.69, 0.36, 0.37)`, dark brown `(0.20, 0.09, 0.05)` | 2 flat colours: `Pink (0.80, 0.46, 0.41)`, `Grey (0.14, 0.12, 0.13)` |
| Clips | **2**: `Idle` 6.25 s, `Jump` 1.50 s | **6**: `Idle` 2.38 s, `Walk` 1.33 s, `Run` 0.50 s, `Jump` 0.83 s, `Attack` 0.67 s, `Death` 1.08 s |
| Scale in file | unit 1 cm, mesh and armature carry `Lcl Scaling 100` (Blender's "FBX Units Scale" export) | unit 1 cm, mesh `Lcl Scaling 100`, armature **39.55** |
| Raw bounding box | 4 × 10 × 5 units (w × l × h), Z up | 2 × 7 × 2 units, Z up |

### What that means

1. **Same author, same rig convention.** The pig's bone list is a strict subset of the rat's
   (the rat adds the tail). The names, the `.L`/`.R` suffixes, the `_end` leaves and the
   Blender 2.79 exporter all match the free **Quaternius** animal sets, whose rat ships
   exactly these six clips. Provenance and licence are an interview question, not a finding.
2. **The rat is the complete one.** Idle, walk, run, jump, attack and death cover everything
   a colony-sim animal does; walk at 1.33 s and run at 0.50 s are usable gait cycles for the
   colonists' gait-blend path (`PawnFigureDirector` builds an `AnimationMixerPlayable` over
   the catalogue's `locomotion` entries, so a Generic-rig animal with its own clips is the
   same shape of thing, not a new director).
3. **The pig cannot walk.** Its only clips are a 6.25 s idle and a 1.5 s jump. A pig that
   wanders needs a gait from somewhere: a computed trot in the manner of `WorkSwing` (the
   quadruped rig is regular enough — four legs, each `Up`/`Low`/`Foot`), a walk authored in
   Blender by the owner, or retargeting the rat's walk (same bone names, so a clip
   *can* be shared by hierarchy match, but a rat's stride on a pig's proportions will read
   as a scurry). This is the first decision the pig forces.
4. **No UVs and no atlas.** Both models are coloured by two flat materials. Every Synty
   character is recoloured by rewriting swatch rectangles on a shared atlas
   (`e-04-tint-strategy.md`, `e-05-character-customisation.md`); that path does not apply
   here. A flat-colour model is *simpler* to tint — set `_BaseColor` per material — but it
   will not read as the same family as the colonists. Whether that matters is the owner's
   call; a placeholder is what they asked for.
5. **Size is not authored to the cell.** The raw boxes say nothing until Unity has applied
   the file scale and the node scale, and the rat's armature carrying 39.55 against the mesh's
   100 is exactly the sort of thing that imports at a surprising size. The 2.5 m cell is the
   target: a pig about 1.2 m long and 0.7 m tall reads as half a cell, a rat about 0.3 m.
   The import must be measured, not assumed — `FigureBuild` already bakes a posed mesh and
   takes the sole and the crown for the colonists (`docs/bug-patterns.md` P11), and an
   animal owes the same measurement.
6. **Triangle counts are cheap.** 562 and 4,004 triangles against a figure ceiling of 64 live
   animated figures (`PawnFigureDirector.FigureCeiling`). Three hundred animals — the vision's
   scale target — cannot all be skinned figures under that ceiling; the far ones need the same
   nearest-first budget the colonists have, or a static instanced pose.
7. **Nothing in the repo can hold them yet.** `Assets/Art/` has only `Ui/`; there is no
   `Assets/Art/Custom/` and no committed model of any kind. `CLAUDE.md` reserves
   `Assets/Art/Custom/` for Blender-made pieces that *are* committed, which is where a
   CC0 model would go. A model under any other licence goes beside the Synty packs and stays
   out of git.
8. **The simulation has no animal.** `Pawn` is documented as "a colonist": one class, one
   Def (`Defs/Core/Pawns/Colonist.xml`), no species or kind concept, no health beyond
   alive, and no death at all. An animal that is hunted or that dies of anything needs the
   first health model; an animal that only wanders and is looked at does not.

## Recommendation

Rat first for the *mechanics* (it has every clip), pig first for the *game* (a pig is what a
player wants to keep). Commit to whichever the interview says the MVP is; if both, stand the
rat up as the walking proof and let the pig's missing gait be the second unit.

## Sources

- The two files, read directly. No web sources; the Quaternius attribution is an inference
  from the exporter, the rig and the clip set and is marked as such.
- `docs/research/e-03-other-packs.md` — no animal in any owned Synty pack.
- `docs/design/03-systems-catalogue.md` §9 — animals at M5, "no animal assets exist".

## Confidence

- **High** for everything read from the files: counts, names, clips, materials, scale nodes.
- **Medium** for the Quaternius attribution.
- **Low** for the imported size until Unity has been asked.

## Could not be determined

- The licence, which decides where the files may live.
- The imported size and whether Unity maps the rig to a Generic avatar cleanly (the `_end`
  leaves are harmless; the armature scale on the rat is the thing to check).
- Whether the pig's `Jump` clip carries root motion.
