# Lessons

Operational lessons that each cost real time once. They are here so they do not cost it twice. Findings that shape *design* live in the design documents and ADRs; this file is for the things that bite you while working.

Add to it whenever something takes more than about ten minutes to diagnose.

---

## Running Unity from a script

**A batch test run can finish its work and then never exit.** Confirmed reproducible on this project: every `-runTests` run writes its results file and then keeps running. On 2026-09-15 one kept going for twenty-five minutes, holding `Temp/UnityLockfile`. The next batch command died instantly with exit code 1 and a log containing nothing but the banner, which is close to undiagnosable if you do not know to look for the lock. `-quit` is not the fix, because it can cut the run short before results are written.

What to do: `scripts/unity.sh` treats the **results file, not the process exit code, as the verdict**, gives a lingering process a grace period and then terminates it, and has a hard timeout so CI fails instead of hanging. Tune with `UNITY_TEST_TIMEOUT` and `UNITY_TEST_GRACE`.

**Cause found and confirmed, 2026-09-15: the Unity MCP plugin.** Removing `com.ivanmurzak.unity.mcp` from the manifest made Unity exit cleanly on its own for the first time, and the watchdog stopped having to intervene. The same plugin was also logging an authorisation error mid-run (`Authorization failed. Token may be missing, invalid, or revoked`), and Unity fails whichever test happens to be executing when an unexpected error is logged — so it was also producing random, unrelated test failures. The watchdog stays, because it turns any future recurrence into a clear message rather than a hang.

**If MCP is wanted back**, `npx unity-mcp-cli install-plugin .` re-adds it, but it has no batchmode guard, so it will hang headless runs again. Keeping it for editor sessions and stripping it for CI needs a second manifest or a define constraint.

**A batch command cannot share a project with an open editor.** Same symptom, different cause. The wrapper now distinguishes the two: a live Unity process means "close the editor", no live process means the lock is stale and it is removed automatically.

**Adding an assembly definition silently removes implicit package references.** Scripts under `Assets/Editor/` compile into `Assembly-CSharp-Editor`, which auto-references most packages. The moment an `.asmdef` covers them, every reference must be explicit. Adding `Odyssey.Editor.asmdef` broke `SyntyImport` because it uses URP types. Symptom: `CS0234: The type or namespace name X does not exist in the namespace Y`. Fix: add the package assemblies (`Unity.RenderPipelines.Core.Editor` and friends) to the asmdef `references`.

**`AssetDatabase.ImportPackage(path, interactive: false)` only *queues* the import under `-executeMethod`.** The editor can exit having imported nothing, and the run reports success. `SyntyImport.cs` calls the editor's synchronous internal import instead. If that ever disappears, fall back to one `-importPackage` invocation per package.

**URP 17.3.0's `Converters.RunInBatchMode` is broken.** It throws `MissingMethodException` on `Base2DMaterialUpgrader` while enumerating the container, before converting anything, with or without a converter filter. Use the public `MaterialUpgrader` API directly; it is batchmode-aware and skips its confirmation dialog automatically.

**Burst compiles asynchronously by default and will pollute a measured window.** In the D1 benchmark it inflated the grid phase from 0.283 ms mean to 0.498 ms with a 3.9 ms maximum. Set `EnableBurstCompileSynchronously` before any run whose numbers you intend to trust, or prewarm with a discarded run.

## Testing

**The tests are not the slow part.** The suite executes in about 40 ms. A Unity EditMode cycle takes minutes, and essentially all of it is Unity booting, refreshing the asset database (~7 s) and reloading the script domain (~3 s compile). **Filtering which tests run therefore saves nothing.** The only thing that helps is not starting Unity.

**Two tiers.** `scripts/test-fast.sh` runs the same Sim test sources through mirror projects in `tools/dotnet/` with no editor, in about 1.7 seconds warm. `scripts/unity.sh test editmode` takes about 37 seconds with the watchdog and is the authority, because only Unity proves the assembly-definition boundaries hold and only Unity can run editor or PlayMode tests. Work in the fast tier, gate on the slow one. See `docs/setup/local-dev.md` §10.

**Unity ships a .NET *runtime*, not an SDK.** `dotnet --list-sdks` against a runtime-only install prints an error to stdout and still exits 0, so the exit code cannot be trusted; check for an actual version line. Install a real SDK without admin rights with the official script, which lands in `%USERPROFILE%\.dotnet`.

## Repository layout

**`build/` is gitignored** by the standard Unity rules, and git will not descend into an excluded directory, so a negation pattern inside it does not work. Committed tooling goes in `tools/`.

**`*.csproj` is gitignored** because Unity regenerates its own. Ours need an explicit negation.

## Working method

**A fix that is plausibly wrong is worse than a bug that is obviously broken.** The asset inventory reported an implied cell of `0.00 x 0.00 x 0.00 m`, which at least announced itself. The obvious repair, taking the best-scoring candidate pitch, produced a confident `0.50 m` instead, because snap share is monotonically better for smaller pitches: every measure that is a multiple of 2.5 is also a multiple of 0.5. Only re-running the tool caught it. **Always re-run after a fix and read the output for meaning, not just for absence of errors.** The working version uses the modal measure and independently reproduces the cell that was confirmed by hand.

**Verify what a peer session is before sending it anything actionable.** A listing gives a name, not an identity. On 2026-09-15 a coordination message was sent to a session assumed to be the UI work; it was a different project, and the request would have corrupted its decision records had it complied. It verified and refused, which is the behaviour to copy. For unconfirmed peers, write it in the repository instead, where it survives and can be checked.

**One owner per shared file when agents run in parallel.** Wave 1 had several research agents editing `docs/research/INDEX.md` at once and produced duplicate rows that had to be reconciled by hand. Wave 2 told every agent to write only its own file and kept the index with the coordinating session. Fan out on the leaves; single-thread the spine.

**Fix the judging criteria before you see the numbers.** ADR 0005 recorded its gates, weights and tie-break rule before either benchmark candidate ran. Deciding how to judge after seeing results is how a benchmark becomes a justification for a preference.

**Cross-implementation agreement is what makes a benchmark comparison real.** Two independent implementations of the D1 workload produced the identical state hash, which is the only reason their timings can be compared at all. It also caught the single genuine ambiguity in the written contract. A benchmark whose implementations are not proven equivalent is measuring two different programs.

**Report partial results rather than nothing, and never invent a number.** Both benchmark agents flagged their own fairness caveats unprompted, and those caveats changed how the result was read.

## This harness

**The Bash tool mangles apostrophes inside heredocs.** A `cat > file <<'EOF'` block containing ordinary English possessives fails with `unexpected EOF while looking for matching`. Write prose and C# with the file-writing tool; keep heredocs for JSON and other apostrophe-free content.

**A plausible causal story attached to a real number is still a guess.** The D1 benchmark measured a real fact: pathfinding was 65% of the tick and 1,058 of 1,800 replans exhausted their node budget. The explanation attached to it — that these were searches for unreachable targets — was written into a design document and an ADR as though it were part of the measurement. It was not, and when measured separately it proved wrong: only 14% of those targets were actually unreachable, and under 1% on a structured map. The fix that worked was hierarchical search plus a better heuristic. **Measure the cause, not just the symptom, before designing against it.** The design survived, but for a different and better-stated reason, and the documents had to be corrected.

**Unity Hub opens a project with the newest installed editor, not the pinned one — and the newer editor upgrades the project without asking.** On 2026-09-15 the project was opened with 6000.6.0f1 while pinned to 6000.3.24f1. Unity silently rewrote `ProjectVersion.txt` and bumped URP 17.3 to 17.6, Timeline 1.8 to 6.6, uGUI 2.0 to 2.6 and Burst 1.8 to 2.0, after which the code stopped compiling on obsolete APIs (`Object.GetInstanceID` is obsolete in 6.6 and current in 6.3). The same code had passed 222 tests headless minutes earlier. It recurs on every open, because Hub keeps defaulting to the newest.

Guard: `.unity-version` is the committed pin and `scripts/unity.sh` checks `ProjectVersion.txt` against it before doing anything, restoring the pin and the package files if they have drifted. Without that check the wrapper would read the upgraded file and dutifully launch the wrong editor. **Open the project with the pinned entry in Unity Hub, or with `scripts/unity.sh open`, which always resolves the pin.**

The general lesson: **a version pin that only a file records is not a pin, it is a preference.** If a tool can silently rewrite it, something has to check it.

## Generators and their parameters

**A "disable this feature" floor will quietly overrule a request for none.** The outcrop and ore passes both computed a count from a per-ten-thousand-columns rate and then did `if (count < 1) count = 1`, so that a small map still received the feature instead of rounding it away. Perfectly reasonable, and it also meant that a rate of zero produced one outcrop and one ore deposit on every map ever generated. The barren-map tests failed on it immediately. The floor now applies only when the rate is positive, so zero means zero and small maps still get their one. **A clamp that protects against rounding must not also silently override an explicit value.**

**Read the comparison before reaching for a sentinel.** Making every cell grass looked like it needed a threshold the noise could never reach, so the first attempt set three thresholds to `int.MaxValue`. The pass keeps grass when `cover >= barePatchThreshold`, so the correct value was zero, and the huge one asked for the exact opposite of what was wanted. The generator validated its own parameters and rejected it on the first run. **Parameter validation earns its keep at the moment it refuses something confidently wrong.**

## Rendering

**A benchmark of a map the game does not load is worse than no benchmark, because it still prints a number.** The slice measurement was hardwired to a 60 x 60 x 5 ruined city and went on reporting healthy figures long after the play scene moved to a barren 120 x 120 x 16 wilderness. Nothing failed; the numbers were simply about something else. The size now comes from constants shared with the scene builder, and the measurement renders the layer the scene actually opens on rather than the ground layer beneath it.

**One flat colour over a large area does not read as a surface.** A barren map is a single material in every cell, and drawn in one tint it looks like a painted plane: there is no grain, so nothing conveys that a colonist is crossing ground at all. Dithering the tint across four near-identical shades fixes it with no texture, no extra geometry and no shader work, because the tint is already part of the instanced bucket key. Cost measured rather than assumed: the ground layer goes from 50 draw calls to 100, steady submission stays at 0.04 ms per frame.

Two details matter. Use **smooth low-frequency noise, not a per-cell hash** — an independent shade per cell is television static that shimmers when the camera moves, whereas soft patches a few cells across read as mottled ground and stay still. And derive it from the **world column only**, never from anything chunk-relative, or a seam appears along the grid the renderer happens to divide the map into.

**Only terrain carries a shade, so only terrain may be asked for one.** Reading the variation bits from a construction code returns shade zero and darkens every wall in the world by the low end of the scale. A uniform change like that is invisible as a bug: nothing on screen looks wrong, it just looks slightly darker than intended forever.

**Where a thing is drawn is not where it can be clicked.** Picking answers with the floor cell a ray crosses, but colonists are drawn as a body and a beacon standing metres clear of that floor so they can be found at a glance. Under a tilted camera the player aims at the beacon and the ray lands a cell or two beyond the pawn. With a pick radius of one, the most natural click in the game — straight at the bright marker — selected nothing, which reads as the click being ignored rather than as a near miss.

**The Windows `python3` stub exits 0 while doing nothing.** There was no Python on PATH on the Windows dev machine, and `python3` resolved to the Microsoft Store app-execution alias, which prints "Python was not found" and **exits 0**. A gate invoked as `python3 tools/wiki/build_wiki.py --check && git commit` therefore passed without ever checking anything. `py -3` did not exist either. This is the same shape as the `dotnet --list-sdks` trap above: **a missing interpreter that reports success is worse than one that reports failure.** Check for real output, not for a zero exit code.

**Fixed 2026-09-16: Python 3.13.15 is installed on the Windows machine** (`winget install --id Python.Python.3.13 --scope user`), at `%LOCALAPPDATA%\Programs\Python\Python313`, which the installer put *ahead* of `WindowsApps` in the user PATH, so `python` now wins over the alias. CPython on Windows ships no `python3.exe`, so a copy of `python.exe` was placed beside it under that name — without it every `python3 …` command in this repository's documentation still hit the Store stub. A shell started before the install keeps the old PATH: check `python3 -V` prints a version, not the Store message.

**And the second half of that install, which is easy to miss: `PYTHONUTF8=1`.** Windows Python defaults to the locale encoding (cp1252), not UTF-8, so `open(path)` with no `encoding=` mangles every em dash in the docs. The first `build_wiki.py --check` after installing reported **all fourteen wiki files stale** — not stale at all, merely decoded in the wrong codec; the same run in write mode would have rewritten the whole wiki in cp1252. `PYTHONUTF8=1` is now set as a user environment variable on this machine. It is a per-machine plaster: the tools under `tools/` still pass no explicit `encoding=`, so a fresh Windows clone hits this again. **When a checker says everything is stale, suspect the reader before the files.**

**A Synty pack has two kinds of material, and only one of them can be worn by a box.** Props are UV-mapped into a shared colour atlas, where each material is one small swatch of one large image. Put that material on a cell-sized cube and every face samples the whole atlas, which is where the stray blades of grass and the dark patches came from. The Nature Biomes pack also ships **terrain** materials under `PNB_Meadow_Forest/Terrain/`, and those are ordinary tiling textures with no atlas, authored for Unity terrain layers. One of those on the cell-shaped box is what ground needed all along. Three attempts were spent on this: a flat tile prefab that z-fought inside every cell, a prop material that sampled the atlas, and a flat tint that was correct but lifeless. **When a texture comes out as garbage on a primitive, ask what the UVs were authored against before changing the mesh.**

Seamlessness is why it works at cell scale: each face carries one unit of UV, the texture tiles twice across it, and it is authored to wrap, so the pattern continues across a cell boundary instead of restarting.

**The world boundary is not a surface.** Face culling treated an out-of-bounds neighbour as open air, so every solid cell in the outermost ring drew its outward face and the map gained a cross-section wall around its whole perimeter, as tall as the slice drew layers below the surface. Looking down at a flat meadow you saw a slab with sides rather than a field. There is no outside of the map, so there is nowhere those faces could be seen from; treating the boundary as solid removed 952 instances from a 120 x 120 map and changed nothing interior, because a dug-out cell still exposes its neighbours the ordinary way.

**Ground that casts shadows shadows itself.** The first textured pass rendered as a dark cross-hatched olive that read as filth on the grass. The cause was that all 14,400 ground cubes were shadow casters: ground is a contiguous mass of cell-sized boxes, and with one shadow map stretched over a 300 m board the texel is far larger than a cell, so every tile acned against its neighbours. Terrain now receives shadows and never casts them. Nothing worth seeing is lost, because what actually tells the eye where something stands is a colonist or a wall casting **onto** the ground, and it takes 14,400 instances per layer out of the shadow pass.

**A tint is a colour the shader reads in linear space, so a modest-looking multiplier is a large one.** The ground sat one layer below the active layer and was therefore dimmed to 0.68 as though it were a storey below. It is not a storey below, it is the floor being stood on. Worse, 0.68 as an sRGB colour arrives at the shader as about 0.42, so a third off on paper was nearer three fifths in practice, which is most of why a healthy green texture rendered near black. **When a brightness factor is carried as a Color rather than a float, do the sums in linear.**

**Fog measured against the wrong distance is a wash, not atmosphere.** Fog started at 90 m on a board 300 m on a side and 424 m corner to corner, so it covered essentially the whole playing area as soon as the camera pulled back far enough to see it. A colony sim is looked at rather than walked through; the board has to stay legible corner to corner, so fog belongs beyond the far corner.

**A cache key that clamps will merge values the renderer keeps apart.** Tint keys were packed with `Clamp01`, which was correct while every tint darkened. Grass now carries a multiplier above one, because `_BaseColor` is an unclamped multiply and can lift a texture as well as darken it. Two different bright tints would have packed identically and the first material built would have been handed out for both — one terrain drawn in another's colour, everywhere, with nothing on screen to identify it. The key now quantises over 0..4.

**A workaround can outlive its problem and become the new problem.** Dithering ground across four near-identical shades was the right answer to a single flat colour over a whole map. Once the ground had a real texture the dithering had nothing left to add and its 5-cell period showed through the texture as visible blotching. It also cost four buckets per chunk for nothing. Removing it took ground from 100 draw calls to 41 and from four materials to one. **When a fix lands upstream of a workaround, delete the workaround rather than tuning it.**

## The bug that four rounds of reasoning could not find

**Every primitive in the renderer was inside-out, and nothing ever said so.** The cube built in code wound its triangles 0-1-2 and 0-2-3 over vertices laid out anticlockwise about the outward normal, which produces triangles facing *inward*. Backface culling then removed the surface the camera should see and left the far interior wall showing through, shaded by a vertex normal pointing away from the sun. Ground rendered as a dark lattice with the tile apparently only on one end of each cube, which is exactly what it was.

It survived so long because an inside-out mesh is **valid geometry**. Nothing throws, nothing is logged, the draw-call and instance counts are healthy, and the measurement harness reported everything as fine. It was diagnosed only after rendering a picture and looking at it, and it had been quietly making every previous theory half-right: the darkness, the odd cubes and the strange shading all had this underneath them.

Two lessons.

**Reasoning about a renderer from its source is guesswork, so build the thing that lets you look.** `scripts/unity.sh shot` renders the play view to PNGs headless, using the scene's own lighting and the real simulation. It must run **without** `-nographics`, which is why it is a separate command, and the instanced draws are submitted from the render-pipeline callback because `RenderMeshInstanced` enqueues for the camera currently rendering. Four rounds of visual faults were argued about from the code before this existed; the first picture settled it in minutes. Build the observability before the third guess, not after the fifth.

**Assert the invariant that has no error path.** `PrimitiveMeshTests.TheCubeFacesOutwards` walks every triangle, derives the facing from the winding exactly as Unity does, and compares it to the vertex normal. It is three lines of vector arithmetic and it would have caught this on the day the mesh was written. Geometry that is wrong but valid is precisely what needs a test, because nothing else will ever complain.

**A lock guard that is too broad fails in the dangerous direction.** `check_project_lock` counted every `Unity.exe` on the machine, so an editor open on an *unrelated* project made every batch command here refuse to run, while the lock it was complaining about was in fact stale. The failure looks exactly like a real conflict, so the obvious next move is to kill an editor belonging to somebody else's work. It now matches the running process against this project's own path.

## Characters

**A rigged character has no MeshFilter, so the prefab path finds nothing and silently draws a box.** Synty characters hang their geometry off `SkinnedMeshRenderer`. `FlattenPrefab` collected `MeshFilter` only, found none, returned empty and fell back to the primitive with a line in the missing-art list — a grey cube where a person should be, and no error to explain it.

Handing over `sharedMesh` would not have fixed it either: that is the bind pose in bone space, and `RenderMeshInstanced` takes one mesh and many matrices with no per-instance bone palette, so a skinned mesh cannot go through the instanced path at all. Baking each `SkinnedMeshRenderer` once at load collapses the rig into an ordinary mesh, after which a colonist costs what a wall costs and travels the same path as everything else. The price is that a baked figure glides rather than walks, which at board-camera distance is a far smaller deficit than a grey box, and it composes with a pooled animated `GameObject` later for the handful of pawns actually on screen.

**Bake without posing first and everyone stands in a T-pose.** The bake captures the *current* pose, and with no Animator having evaluated, that is the bind pose: arms straight out. Sampling a standing idle clip onto the instance first is what makes a baked character read as a person. Nothing reports the difference, so it is measured instead: a T-posed figure is about as wide as it is tall, an idle one about half a metre wide. The slice measurement prints the baked figure's box for exactly this reason.

## Choosing pack art

**A Synty prefab's name describes where it was meant to be used, not what it looks like.**
`SM_Prop_Box_Supplies_01` is an open crate of stripped mechanical parts. Picked from the name it
became the game's rations, and the scrap heap became its salvage, so the first render had the two
kinds of item exactly the wrong way round — each one perfectly legible, and each one labelled as
the other. The inventory CSV gives dimensions and triangle counts, which is enough to rule a piece
out for size but nothing at all about what it depicts. **Render it and look before writing the
catalogue row**; `scripts/unity.sh shot` costs about a minute.

**An item with no module id draws as the stand-in marker and never says so.** The orange box the
renderer falls back to is the same shape and colour for every kind of item, so a barren map with
twelve ration stacks and eight pieces of salvage on it came out spattered with identical orange
blobs. Nothing in the log mentions it, because falling back is the designed behaviour for a clone
without the licensed packs. `ModuleIdTests` now fails if an item def index exists with no module
id, which is the only moment the mistake is cheap to catch.

## Scattering things over the ground

**A Synty prefab is often several renderers, and a part is a draw rather than a triangle count.**
The Nature Biomes meadow clumps are fifty triangles each and looked free. They are three separate
renderers sharing one material, and the renderer submits one instanced call per part per bucket, so
every tuft cost three draws and three matrices. Scattering them took a slice from 41 draw calls to
266 and from 14,400 instances to 66,441 — nearly all of it the same fifty triangles being asked for
three times. Merging a module's same-material pieces into one mesh at load put it back to 116 and
31,747. It is safe exactly because it happens at load: the pieces of a module never move relative
to one another, which is what makes them one module.

Check the `renderers` column of `docs/research/synty-inventory.csv` before scattering anything.

**A loose bounding box hides a floating figure.** Bounds used to be each mesh's axis-aligned box
pushed through its local transform, which for a piece rotated at an angle gives a box around a
rotated box — always bigger than the geometry. Every module was placed against that inflated box,
so the colonist had been standing 0.13 m above the floor since the day it was added, and the
measurement that would have shown it was computed the same wrong way, so it read 0.00. Merging
bakes the locals into the vertices, after which the bounds are simply the bounds; the number moved
to 0.13 and then, once placement used the tight box too, back to a true 0.00.

**Grass has to be a hash of the cell, not a stream of random numbers.** A chunk is re-meshed
whenever anything in it changes, so scatter drawn from a generator would depend on how many cells
had been visited first — and the grass would crawl about whenever a wall went up nearby. Hashing
the coordinates is stable by construction, needs no state and allocates nothing. Avalanche the
hash: FNV alone leaves neighbouring inputs with neighbouring low bits, and a small modulus of that
lays the field out in diagonal stripes. `GroundScatterTests` splits the map by the parity of x + z
and counts, which is a cheap test for exactly that failure.

**Tint what a thing is, not what it stands on.** The terrain tint is calibrated against a tiling
ground texture that needed lifting towards the reference art, and multiplies blue by 1.55. Applied
to a grass tuft, whose art is already the right yellow-green, it turned a meadow into a stand of
dark teal reeds. Foliage now has its own tint code and its own palette entry, which is white,
because the right answer for art that is already correct is to leave it alone.

## Renderer features and the look

**A serialised asset value wins over the C# default, and half a tuning change lands silently.**
Renderer features are sub-assets. Once one has been saved, editing a field's initialiser in C#
changes nothing for a field the asset already holds — but a field the asset has *never* seen does
pick up its initialiser. Tuning the outline moved four numbers: the two new ones took effect and
the two old ones did not, so the picture changed a little and the obvious conclusion was that the
shader maths was wrong. `RenderSetup.Configure` now writes the whole tuning into the asset on every
run, which makes the command the single source of it.

Related: a renderer feature appended to `ScriptableRendererData.rendererFeatures` without rebuilding
the internal feature map serialises fine, shows up in the inspector, and never runs, with nothing
logged.

**For an outline, the question is how wide a thing is on screen, not how far away it is.** Grass
clumps turned into solid dark blots at board distance: a tuft is a few pixels across and every one
of them sits on a depth discontinuity, so the whole clump inks over. A distance fade was the
obvious lever and it is the wrong measurement — it cannot keep a far-off building outlined while
killing a near railing. Measuring width instead (sample wider than the detector; if both opposite
neighbours are behind the centre, the feature is thinner than the sample diameter) fixes both, and
the line comes back by itself as the camera moves in. It only works if the ink is one-sided first,
because a Roberts cross puts half its line on the background where a width test cannot reach it.
See `d-10-outline-pass.md`.

**One-sided ink is half the width, so the thickness has to be re-tuned when you switch.** Otherwise
the change reads as "the outlines have mostly disappeared" rather than "the outlines are now on the
object".

**A depth-edge test built on the first difference inks flat ground at a grazing angle.** The meadow
went dark towards the horizon, with the tufts bright and the ground between them ink-green. Three
diagnoses were reasoned from the source before a picture was taken — facet lighting (which led to a
110 m grass cutoff), the cut-out texture's mip fringe (which led to a lower clip threshold), and a
multisample mismatch — and `MeadowCheck` refuted each in one render. The cause: a flat plane seen
low across the board has its depth change per pixel grow with the *square* of the distance, while a
threshold that scales with distance grows only linearly, so from about eighty metres out the ground
itself passed the edge test and was inked. The tufts looked bright only because their own depth
broke the gradient. The fix is to measure the second difference of *inverse* depth: 1/z is affine
across the screen on any plane, so a plane scores exactly zero however steeply it recedes, and only a
real step scores. Two lessons. **Photograph before the second theory, not after the third** — every
one of the three wrong diagnoses was plausible, and each cost a workaround that then had to be
removed. And **when a fix lands upstream of a workaround, delete the workaround**: the grass cutoff
and the lowered threshold both outlived the problem they were guessing at.

**Grass is drawn in the transparent range so that it is never inked.** With the ground fixed, the
tufts themselves still wore a black cap and a rim under the ink, and the sliver test only ever
reached the narrowest of them. The depth texture the outline reads is copied after the opaques, so
a material in the first transparent slot is simply not in it; the tufts are then drawn over the
inked picture with their depth write and alpha clip intact. No mask pass, no extra draw. The
outline pass had to move to before the transparents for this to work, and the colour target it
hands on now keeps the camera's sample count, because what follows draws into it against the
camera's depth.

## Diagnosing a colour cast

**"It looks like light bouncing off the grass" was geometry growing through people.** Colonists
picked up green and the natural reading was a lighting fault — bounced light, an ambient ground
colour, a reflection probe. It was none of those, and it could not have been: ambient is Trilight
with three neutral blue-grey colours, there is no baked GI, no probe volumes and no light probes,
so there is no mechanism in the scene by which one surface can tint another at all. What was
actually happening is that grass clumps are nearly two metres across, a cell is 2.5 m, and tufts
were scattered over the whole cell — while colonists, crates and ration stacks are all drawn at the
**cell centre**. The grass simply grew through them.

The method is worth keeping, because "does X tint Y" will come up again. `GreenCheck` stages the
suspects beside **two reference objects of undisputed colour** — a white cube and a mid-grey sphere
— and renders the matrix of {grass ground, neutral ground} x {outline on, off} x {tufts, no tufts}.
The white cube staying white over grass killed the bounced-light theory in one picture, and adding
tufts as a variable produced the culprit in the next. Change one thing at a time and photograph it;
four renders cost six minutes and the wrong theory would have cost an afternoon in the lighting
settings.

**The fix keeps the middle of a cell clear rather than asking what is standing in it.** Tufts are
placed in a ring now. The alternative — skipping scatter on occupied cells — cannot work: pawns and
items live in the published snapshot, not in the cell mirror the mesher reads, so the mesher would
have to re-mesh a chunk every time somebody walked across it.

## Benchmarking the renderer

**`camera.Render()` in a loop is not a benchmark, and it convicted our renderer of a crime it
did not commit.** An editor batch harness that renders a camera into a RenderTexture a few hundred
times reported fourteen thousand instanced cubes at 16 ms on an RTX 5070 Ti, then 205 ms, then
307 ms — on three runs of the same code. The numbers tracked how far down the table a row sat, not
what it drew: a row of 26-triangle tufts cost five times a row of cubes with the same material, and
an empty render placed last cost 464 ms against 2.65 ms for the same empty render placed first. A
GPU sync per frame did not change it. There is no frame boundary in such a loop — nothing Presents,
and the render pipeline's per-frame bookkeeping is never told a frame ended — so the cost of every
render includes the debris of every render before it. The screenshot harness never noticed because
it renders four times.

Consequences. Frame time is measured **only** under a real player loop: the PlayMode test
`FrameTimeTests` (which is also the first test the PlayMode gate has ever had), or the on-screen
readout in Play. `RenderBench` is kept as the record of the failure and for *ordering* questions
answered within one row, never for absolutes. And a milestone report stated a red result from that
harness before the harness had been checked against an empty render placed last — the control that
should have been the first row written, not the last.

**A profiler recorder is not free to start.** Merely creating `ProfilerRecorder`s for the render
statistics made every row of the same benchmark ten times slower. Counters that change the thing
they count are worse than none; the editor's own `UnityStats` (what the Stats overlay reads) costs
nothing to read, though in batchmode it returns zeros.

**Flat-lit grass reads dark at range, and the cause is lighting, not distance.** A clump is a fan
of facets pointing every way; half of them turn from the sun and render at ambient. Up close the
lit faces dominate; at range the eye averages the clump, and the average is darker than the
flat-lit ground beside it — so the far meadow went dark wherever tufts crowded, and neither
density, tint nor a softer alpha cutoff moved it, because none of them touches the lighting. The
honest fix at this scale is not to draw grass past a distance at all (`ChunkRenderer.
FoliageDrawDistance`, per chunk against the chunk bounds): a distant tuft is a few pixels, flat
lit ground is what the reference art shows at range anyway, and it takes the largest instance
count in the scene off the far half of the board. The fix that keeps far grass — a foliage
shader that lights every blade as if its normal pointed straight up — is the next lever, and it
is a real shader because the pack's meshes are not readable and their normals cannot be bent at
load. Two things that *were* wrong and are fixed on the same day, and would have muddied the
test if left: one of the three clumps was a flat olive-brown patch, and the sky's underside was a
dark grey that showed as a band between the board's rim and the horizon.
