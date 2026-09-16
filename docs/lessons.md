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

## Continuous integration

**A self-hosted Windows runner installed as a service has no Git on its `PATH`, and `bash` is then not merely missing — it is worse than missing.** It resolves to `C:\Windows\System32\bash.exe`, the WSL stub, which has no distribution installed and exits 1 before it reads the script. So every `shell: bash` step fails identically and instantly, with no output naming the real problem: the log shows the script you meant to run and an exit code, and nothing about which bash ran it.

**Naming Git Bash by its full path in the shell line does not fix it.** The runner splits the shell line on spaces, so `C:\Program Files\Git\bin\bash.exe` becomes a command that does not exist. Put Git's `bin` on the job's `PATH` in a first step instead — `Add-Content -Path $env:GITHUB_PATH -Value 'C:\Program Files\Git\bin'` — and let every later step say plain `bash`.

**That first step must ask for `powershell`, not `pwsh`.** They are different programs. `powershell` is Windows PowerShell 5.1 and ships with the OS, so it is always there; `pwsh` is PowerShell 7 and is a separate install this machine does not have. A step with `shell: pwsh` dies with `pwsh: command not found` — and, on a job whose whole purpose is to fix the `PATH`, that then fails every step after it and produces a second, louder and misleading error from the test reporter (`No test report files were found`). Read the *first* red step, not the last one.

**Keep `clean: false` on the checkout.** The runner checks the project out into its own folder and the gitignored `Library/` is the import cache. Without it every run re-imports the whole project, which is tens of minutes rather than the 1 m 46 s the first green run took.

## Testing

**The tests are not the slow part.** The suite executes in about 40 ms. A Unity EditMode cycle takes minutes, and essentially all of it is Unity booting, refreshing the asset database (~7 s) and reloading the script domain (~3 s compile). **Filtering which tests run therefore saves nothing.** The only thing that helps is not starting Unity.

**Two tiers.** `scripts/test-fast.sh` runs the same Sim test sources through mirror projects in `tools/dotnet/` with no editor, in about 1.7 seconds warm. `scripts/unity.sh test editmode` takes about 37 seconds with the watchdog and is the authority, because only Unity proves the assembly-definition boundaries hold and only Unity can run editor or PlayMode tests. Work in the fast tier, gate on the slow one. See `docs/setup/local-dev.md` §10.

**Unity ships a .NET *runtime*, not an SDK.** `dotnet --list-sdks` against a runtime-only install prints an error to stdout and still exits 0, so the exit code cannot be trusted; check for an actual version line. Install a real SDK without admin rights with the official script, which lands in `%USERPROFILE%\.dotnet`.

**A remote container can run the whole fast tier, and the SDK comes from the distribution, not from Microsoft.** The container images used by Claude Code on the web carry python3 but no dotnet, and the official installer is useless there: `dot.net/v1/dotnet-install.sh` redirects to `builds.dotnet.microsoft.com`, which the egress proxy refuses outright (`CONNECT tunnel failed, response 403` — a policy denial, so retrying it only spends the session's time). The Ubuntu archive *is* reachable, and 24.04 packages the SDK, so `apt-get install -y dotnet-sdk-8.0` puts 8.0.131 on the path in about a minute and `scripts/test-fast.sh` then restores from nuget.org and runs every Sim test — 227 passed, 4 s cold, on 2026-09-16. **So "no Unity" does not mean "no gate" for Sim work:** every row in `docs/plans/overnight-queue.md` tagged **C** can be proved in a container, and only the **W** rows genuinely need the Windows machine. Still, **check `dotnet --version` before promising "fast tier green"** rather than assuming it: if an image ever has neither the SDK nor a reachable archive, the only safe rows are the ones whose done-when needs no test run at all. What the container cannot do at all: Unity itself (assembly-definition boundaries, editor tooling, PlayMode, frame time), and web research — `rimworldwiki.com`, `dwarffortresswiki.org`, `steamcommunity.com` and even `en.wikipedia.org` are blocked for both `curl` and `WebFetch`, leaving only the `WebSearch` tool's own extracts, which is thinner than a research row's format asks for.

**The fast tier cannot see a broken build, and a commit can be split across the gate.** On
2026-09-16 the tip of `main` (`a98dced`) did not compile in Unity: `OdysseyBootstrap.cs` called
`_renderer.Skirt`, while `TerrainSkirt.cs`, `SkirtLayout.cs` and the `ChunkRenderer.Skirt` property
were still untracked in the working tree. Everything passed — the fast tier covers `Sim` and
`Sim.Contracts` only, and the working tree, which had the missing files, compiled perfectly well.
Nothing was wrong until somebody checked that commit out somewhere else, at which point the whole
Presentation assembly failed and took the editor tests with it. **Before committing Presentation or
Editor code, look at what is untracked as well as running the tests**: a new file that was never
added is invisible to every check this project has, because the only machine that runs Unity is the
one already holding the file.

**Running Unity in a worktree costs two minutes of setup and saves the reimport.** A second checkout
has no `Assets/Synty` (gitignored, so it lives only in the main checkout) and no `Library`, so Unity
would reimport 7,222 pack assets from scratch. Instead: `mklink /J <worktree>\Assets\Synty
D:\code\odyssey\Assets\Synty` for the packs — the `.meta` files come with them, so every GUID in
the committed catalogue still resolves — and `robocopy <main>\Library <worktree>\Library /E /MT:16`
for the artifact database, which is 6.2 GB in 62,220 files and copies in about fifty seconds at
134 MB/s. Unity then recompiles only the scripts that actually changed. Note that robocopy exits
**1** on success ("files were copied"), which reads as a failure to anything checking exit codes.

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

**`python3` in the Bash tool is not the Python that CLAUDE.md says is installed.** The project note says Python 3.13 sits ahead of `WindowsApps` in PATH, and in PowerShell it does. Inside the Bash tool it does not: `which python3` resolves to the `WindowsApps` alias stub, which prints *"Python was not found; run without arguments to install from the Microsoft Store"* and exits 49 — a failure that looks like Python being absent rather than shadowed. Every wiki, icon and mockup command is affected, and `build_wiki.py --check` is a commit gate. Use the interpreter by path:

```bash
export PYTHONUTF8=1
"$LOCALAPPDATA/Programs/Python/Python313/python3.exe" tools/wiki/build_wiki.py --check
```

`PYTHONUTF8=1` is separately required, for the reason already recorded below: without it Windows Python reads the docs as cp1252 and calls every file stale.

**Editing a generated file's *source* is not enough if the generator also fingerprints the design documents.** `build_wiki.py --check` went stale after an edit to `docs/design/02-world-and-layers.md`, with no CSV touched. Rebuild and re-check after *any* documentation change that a wiki page quotes, not only after a change to `icon-keys.csv`.

**A plausible causal story attached to a real number is still a guess.** The D1 benchmark measured a real fact: pathfinding was 65% of the tick and 1,058 of 1,800 replans exhausted their node budget. The explanation attached to it — that these were searches for unreachable targets — was written into a design document and an ADR as though it were part of the measurement. It was not, and when measured separately it proved wrong: only 14% of those targets were actually unreachable, and under 1% on a structured map. The fix that worked was hierarchical search plus a better heuristic. **Measure the cause, not just the symptom, before designing against it.** The design survived, but for a different and better-stated reason, and the documents had to be corrected.

**Unity Hub opens a project with the newest installed editor, not the pinned one — and the newer editor upgrades the project without asking.** On 2026-09-15 the project was opened with 6000.6.0f1 while pinned to 6000.3.24f1. Unity silently rewrote `ProjectVersion.txt` and bumped URP 17.3 to 17.6, Timeline 1.8 to 6.6, uGUI 2.0 to 2.6 and Burst 1.8 to 2.0, after which the code stopped compiling on obsolete APIs (`Object.GetInstanceID` is obsolete in 6.6 and current in 6.3). The same code had passed 222 tests headless minutes earlier. It recurs on every open, because Hub keeps defaulting to the newest.

Guard: `.unity-version` is the committed pin and `scripts/unity.sh` checks `ProjectVersion.txt` against it before doing anything, restoring the pin and the package files if they have drifted. Without that check the wrapper would read the upgraded file and dutifully launch the wrong editor. **Open the project with the pinned entry in Unity Hub, or with `scripts/unity.sh open`, which always resolves the pin.**

The general lesson: **a version pin that only a file records is not a pin, it is a preference.** If a tool can silently rewrite it, something has to check it.

## Water, and two things a design review did not catch

Recorded because both are properties of cutting *anything* into a heightfield, not of water, and the next feature that lowers a column will meet them again.

**Lowering a column by one can leave a step of two.** A channel cut across terraced ground lowers a column that was already a layer below its neighbour, and "neighbouring surface cells are never more than one layer apart" is the invariant that keeps the board walkable without ramps. The design reasoned carefully about the water column and not at all about the column beside it. A test caught it on the first run.

**Lowering a column can also leave the thing in it hanging above its neighbour.** The same channel cut into a slope put water a layer *above* the dry ground next to it — water that would drain. This breaks no invariant, so nothing would have failed; it was caught only because the test asserted the relation it actually wanted (a bank stands *exactly* one layer over its bed) rather than the weaker one that the invariant implies.

**And the fix has to be symmetric.** The first attempt let the wet end pull its bank down. That is half the relation, and it silently fails: a bank lowered by some *other* channel never tells the channel beside it to follow, so a stretch of map keeps its water perched. Stating the relation once, in a `Reconcile(a, b)` that either end can call, is what made it correct — and it is the shape to reach for whenever a constraint holds between two neighbours rather than within one.

The general lesson: **when a change moves the ground, write the test against the relation you want, not against the invariant you are afraid of breaking.** The invariant passed in both cases that were wrong.

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

**Removing a worktree that holds a junction to `Assets/Synty` deletes the licensed art itself.** A worktree needs the packs to resolve art, and a directory junction (`mklink /J`) is the cheap way to give it them. But `git worktree remove`, like any recursive delete on Windows that does not know it is looking at a reparse point, follows the junction into the target: on 2026-09-16 removing a finished worktree emptied `D:\code\odyssey\Assets\Synty` in the main checkout — 7,222 licensed assets gone, every worktree's junction pointing at an empty folder, and the next scene build reporting 0 of 109 rows with art. Recovery was a headless re-import of the six `.unitypackage` files from the owner's Downloads through `SyntyImport.ImportAll`, then the catalogue and scene rebuilt. **Before removing a worktree, delete its junction with `rmdir Assets\Synty`, which removes the link and never the target, and only then remove the worktree.** A symlink (`mklink /D`) is no safer here; the same rule applies.

**A lock guard that is too broad fails in the dangerous direction.** `check_project_lock` counted every `Unity.exe` on the machine, so an editor open on an *unrelated* project made every batch command here refuse to run, while the lock it was complaining about was in fact stale. The failure looks exactly like a real conflict, so the obvious next move is to kill an editor belonging to somebody else's work. It now matches the running process against this project's own path.

## Characters

**A rigged character has no MeshFilter, so the prefab path finds nothing and silently draws a box.** Synty characters hang their geometry off `SkinnedMeshRenderer`. `FlattenPrefab` collected `MeshFilter` only, found none, returned empty and fell back to the primitive with a line in the missing-art list — a grey cube where a person should be, and no error to explain it.

Handing over `sharedMesh` would not have fixed it either: that is the bind pose in bone space, and `RenderMeshInstanced` takes one mesh and many matrices with no per-instance bone palette, so a skinned mesh cannot go through the instanced path at all. Baking each `SkinnedMeshRenderer` once at load collapses the rig into an ordinary mesh, after which a colonist costs what a wall costs and travels the same path as everything else. The price is that a baked figure glides rather than walks, which at board-camera distance is a far smaller deficit than a grey box, and it composes with a pooled animated `GameObject` later for the handful of pawns actually on screen.

**Bake without posing first and everyone stands in a T-pose.** The bake captures the *current* pose, and with no Animator having evaluated, that is the bind pose: arms straight out. Sampling a standing idle clip onto the instance first is what makes a baked character read as a person. Nothing reports the difference, so it is measured instead: a T-posed figure is about as wide as it is tall, an idle one about half a metre wide. The slice measurement prints the baked figure's box for exactly this reason.

**A procedurally posed bone moves its children and not its skin.** Writing bone rotations after the
animation update is the ordinary way to lay a computed pose over a clip — it is how the axe swing
works — but a `SkinnedMeshRenderer` caches the bone matrices it was last handed, so a bone written
afterwards moves anything *parented* to it and leaves the mesh exactly where it was. The symptom is
specific and thoroughly misleading: the axe, an ordinary child of the hand bone, swung through a
perfect arc while the colonist holding it stood perfectly still. That looks like the arm pose
failing to apply, and the arm pose was fine the whole time. The fix is one line,
`forceMatrixRecalculationPerRender = true` on the figure's skinned renderers, set where the figure
is built. Worth suspecting whenever a change to a bone has a visible effect on an attachment and no
visible effect on the body.

**And the signs of a limb rotation cannot be reasoned out; photograph them.** Pitching a bone about
the figure's own right-hand axis, a limb that hangs down goes *forward* under a negative angle and
backward under a positive one, while a spine, which stands up, does the opposite. There is no single
convention to read off the axis name: whichever way you read it, one of the two is wrong. The axe
swing got each of them wrong in turn — once a woodcutter who raised an axe over her head and then
returned it neatly to her side, once one who leant away from her own blow — and each compiled, ran,
and passed every test that existed. One contact sheet settled both, which is what
`Odyssey > Presentation > Check the axe swing` exists for.

**A chain of bones is not a set of independent numbers until you make it one.** The spine carries
the shoulders, so folding the back twenty degrees further into a blow also swings both arms twenty
degrees, and every attempt to tune one silently moved the other — three numbers that could not be
settled in any order. Subtracting the parent's own pitch back out of the child (`Shoulder - Spine`
on the upper arms) costs one subtraction and turns the pose into what it reads as on the page: an
angle against the world, which is the thing a photograph shows.

**"Reach" is not a number once a swing is diagonal.** A figure's reach was measured as the length
of the line from its feet to the edge of its axe at the moment of the blow, and the figure was stood
at that distance from the tree. It missed, every time, and the arithmetic was right: of 1.68 m of
strike, 1.12 m was *sideways*, because the swing comes over the shoulder. A distance says where the
edge is only when the offset is straight ahead. Keep the whole offset, in the figure's own frame, and
solve the stand from it — which also stays correct when the angles are retuned, when the tilt of the
swing changes, and for a figure scaled differently.

**And then measure the thing itself, in the world, on the frame that was drawn.** Whether the blade
reaches the trunk was misjudged from photographs twice in a row, in both directions. One number
reported out of the renderer — how far the edge finished from the middle of what it was aimed at —
settled it in one run and keeps settling it for free.

**An effect nobody can see is an effect you have to count.** Wood chips fly for half a second and
are a few centimetres across, so "are they working" cannot be answered by looking at a screenshot —
and the answer was no. The emitter was built, warmed, wired and tested, and threw exactly zero
chips, because the blow that lands first in every job lands while the swing is still easing in and
a gate demanding full weight discarded it. One counter printed beside the picture found it in one
run. Count what an effect did; do not photograph it and squint.

**Desynchronise by period, not by phase, or the thing snaps on when it starts.** Figures were
spread out by shifting each one's phase by a constant, which desynchronises perfectly and at a price
nobody had counted: a colonist taking up an axe began at whatever point of the stroke her constant
named — arms half raised, as often as not — so the quarter second of easing in had to carry her from
a standing idle into the middle of a swing. What that reads as is the pose being switched on, and no
length of blend fixes it. Give every figure the same starting phase and vary the *length* of its
stroke instead: they set to together and drift apart over the next few strokes, which is how two
people chopping actually fall out of time. The ease can then be longer, because it only has to cover
the short distance from standing to the nearest point of the stroke.

**A pose applied on top of a pose is twice the pose.** `Strike` adds its angles to whatever the
bones are already at, which is right once per animation update and wrong the moment anything calls
it twice. Refitting a tool mid-swing measured a doubled pose, a reach to match, and a blade that
had been landing in the wood reporting itself two thirds of a metre out. Anything that re-poses
outside the normal path must first put the clip pose back: `graph.Evaluate(0f)`.

**The first batch run after a script edit executes the previous assembly.** Edit a file, run
`unity.sh shot`, and what runs is the build from before the edit — the new code compiles during that
run and is live on the next one. It cost three runs and a wrong diagnosis before the pattern was
plain: a sweep that had been extended from five settings to eight wrote five files, twice. Run it
twice after an edit, or delete `Library/ScriptAssemblies` first and take the slower compile. And
whenever a harness produces output that looks like the *previous* version of the code, suspect this
before suspecting the code.

**Photograph the moment on purpose, never on the sample grid.** The stroke was sampled at a fixed
number of frames apart, so which picture caught the blow depended on how long the stroke was — and
the moment the stroke's length became a per-figure thing, none of them reliably did. A pose that was
measurably correct looked wrong in every frame, because every frame was of something else. Pin the
instant that matters and shoot that as well.

**Judge a distance side on to it, never in three-quarter.** The board camera's 45-degree bearing
puts a colonist and the tree she is working on at different depths in the frame, and the gap between
an axe head and a trunk then reads as whatever you please — it was read wrongly twice before the
harness was changed to shoot across that line instead of along it. `PlayScene.Shoot` takes a bearing
for exactly this; the default stays the board camera's, because everything else should be judged in
the view a player will actually have.

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

## Compiling the game code while the editor holds the project

`scripts/unity.sh` refuses to run while the editor is open, and the editor is often open because the
owner is doing visual work in it. That does not mean code has to be written blind until the editor
closes. Everything the assemblies need is already on disk:

```
dotnet build <scratch>/PresCheck.csproj   # Assets/Odyssey/Presentation/**/*.cs
```

against `Library/ScriptAssemblies/*.dll` (the compiled package and sibling assemblies, minus the one
being compiled) and `<Unity>/Editor/Data/Managed/UnityEngine/UnityEngine*.dll`. Add
`Library/PackageCache/com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll` to compile the
test assembly too. That catches every compile error in seconds. It is **not** a substitute for
`scripts/unity.sh test editmode`, which is still the gate — it only stops a broken handoff.

Pure layout and arithmetic can be *run* the same way: reference the built DLL from a throwaway
`net8.0` console project and print the numbers. `Mathf`, `Color` and the rest of the value types in
`UnityEngine.CoreModule` are managed code and work outside the player. That is how the surround's
ring coverage was checked to be exactly 100.0000% of the area outside the board before anything was
committed.

Two traps while doing it. MSBuild reads `<HintPath>` as XML, so a Windows path written with
backslashes dies on `MSB4025: hexadecimal value 0x0C is an invalid character` — the `` in a path
segment. **Write every path in a generated csproj with forward slashes**; MSBuild accepts them and
the error message points nowhere near the cause.

And a shell trap that is not a documentation error: the user PATH does put Python 3.13 ahead of
`WindowsApps`, but a shell started before that change inherits the old environment, so `python3`
resolves to the Microsoft Store stub and answers *"Python was not found"* to everything —
`build_wiki.py --check` included. It looks exactly like Python not being installed. Check with
`which -a python3`, and in a stale session call
`$LOCALAPPDATA/Programs/Python/Python313/python.exe` directly rather than believing the stub.

## Displacing instanced geometry

Written after giving the flat board a shape (`GroundRelief`, `06-rendering-and-camera.md` §2b).
Four of these cost more than ten minutes each.

- **A shear belongs in the Y row, and Unity writes matrices row-column.** `y' = y + gx*x + gz*z` is
  `m10` and `m12`. Setting `m01` and `m21` is its transpose: it leans the cubes sideways and leaves
  their tops flat, which looks exactly like a rotation bug and sends you hunting in the wrong place.
- **An object-to-world matrix is handed local coordinates, so the shear acts on them.** The mesh
  arrives already relative to the cell centre, so the Y row is just the height. Subtracting the
  centre out of it as well — which is what you would write for a shear expressed in world
  coordinates — takes it off twice. Neighbouring cells then disagreed by 1.15 m instead of by
  millimetres, and the seam test is what caught it.
- **Gentle slopes are invisible, so amplitude has to be chosen against the lighting, not against
  intuition.** With the sun at 72 degrees over a strong trilight ambient, a 1.7-degree slope moves
  the lit value by well under one per cent. The prudent-looking 0.35 m amplitude would have shipped
  the whole system with nothing whatever to see. Sizing a visual effect is a measurement, not a
  matter of taste, and the cautious number is not the safe one when the failure mode is "no effect".
- **Tangent-plane displacement scales with the square of the tile.** Two neighbouring tiles drawn as
  tilted planes part company across their shared edge by roughly `(A/2) * (2*pi*L/P)^2`. At the
  board's 2.5 m cells that is 41 mm and invisible; at the surround's old 120 m tiles it was twenty
  metres, and it read as long diagonal cracks scored across the hillsides. The same technique is
  fine at one scale and useless at another, and the arithmetic tells you which before the screenshot
  does.

## Per-cell geometry cracks where a continuous field does not

Written after giving earth its own mesh (`GroundMesh`, `06-rendering-and-camera.md` §2c). The
mistake took one screenshot to find and would have taken a long time to reason out.

- **A mesh shared by every cell cannot make neighbours agree at a shared edge.** Each cell picks its
  own variant, so if the rim moves at all, two neighbours disagree across their boundary by up to
  *twice* the movement. At 12 cm of rim ripple that is a 24 cm step at every cell edge on the board,
  and the meadow came out as crazy paving — visibly worse than the flat quads it replaced. There is
  no amount of tuning that fixes this, only a smaller number that hides it: the ceiling is set by
  the fact that the rim height is a function of the *cell*, when it needs to be a function of the
  *shared corner's world position*. That wants vertex displacement in a shader, or per-cell meshes
  and no instancing.
- **The same trick at a different scale is the opposite of the same trick.** `GroundRelief` gives
  the board a rolling surface that never cracks, because it is one smooth field sampled per cell and
  neighbouring tangent planes part by millimetres. Adding per-cell noise on top looked like more of
  the same thing and is structurally the reverse of it. Compare with the sibling lesson above:
  tangent-plane displacement scales with the square of the tile, and this is what happens at the
  other end of that argument.
- **Photograph the control, not just the change.** The shot of the change alone showed a textured
  meadow and could plausibly have been called a success. The shot of the board *without* it showed a
  clean green surface, and the comparison settled it in one glance. `SlopeCheck` shoots plain, earth
  and banks for exactly this reason, and the harness paid for itself on its first run.
- **Frame the instrument before trusting it.** The first side-on shot put the camera 3 m above its
  focus — one layer — so it sat inside the hillside and all three conditions photographed the same
  flat green nothing, at identical file sizes. Identical output from conditions that must differ is
  the instrument telling you it is broken, and it is worth checking the file sizes for that.
- **Verify which fault you were asked to fix.** The complaint was a terrace riser: "one big block
  and then a completely straight wall". The top surface was never mentioned and was already fine.
  Adding geometry to the part that worked, and only then getting to the part that did not, is how a
  change ends up net negative while every piece of it passes its tests.
- **A dark line between two surfaces is more often a lighting fault than a hole.** The black lines
  round every ground tile looked like gaps and were not: a gap would have shown the pale blue
  skybox, and these were near-black, so they were geometry receiving no light. The fix was to tilt
  the shading normals of side faces up towards the sky, which costs no vertex, no triangle and no
  draw call — the measured count was identical with it and without. **Check the colour of the fault
  before deciding what kind of fault it is**: a hole shows you what is behind the world, and what is
  behind the world is not black.
- **"It does not happen on main" is not evidence about a cause when main does not have the
  feature.** The owner reported the lines against `main`, which has no `GroundMesh` at all. That
  narrowed nothing by itself, and the temptation was to accept the offered explanation (the missing
  textures) and move on. Measuring instead settled it in one run: 14.7 mm from the relief field, 72
  mm from the rim ripple. Both observations turned out to be true — the lines were on main, subtly —
  and only the measurement said which part to spend on.
- **When a mesh has to vary by its surroundings, fold the cases with rotation before building them.**
  A chamfer that may only touch exposed sides needs a mesh per pattern of exposed sides: sixteen.
  Turning a mesh is free because the yaw rides in the instance matrix, so the sixteen fold onto
  **five** — one side, two adjacent, two opposite, three, four. The price is that the bearing stops
  being available for variety, which is worth stating out loud because it silently removes a source
  of variation somebody else may be relying on.
- **A lever whose "off" costs more than its "on" is not a lever.** With the chamfer at zero all five
  exposure patterns build the identical mesh, and five buckets a chunk for five copies of one block
  would have made turning it off the expensive choice. The family collapses to one when there is
  nothing to cut. Worth checking for any feature whose cost is paid in *variants* rather than in
  work per instance.

## A loose tolerance can make a test prove nothing

The test for "a click on a slope lands on the cell under the cursor" allowed the answer to be one
cell out. It passed. It also passed with the bug deliberately put back, because the error a flat
floor plane produces at that amplitude is *almost exactly one cell* — the tolerance had been sized,
without anyone meaning to, to admit precisely the failure the test existed to catch.

The negative control is the only thing that showed it, and it took a minute: put the bug back, run
the test, check it goes red. The general rule is the standing one about re-running after a fix to
see whether the output means anything, applied to tolerances — **a tolerance chosen for comfort
rather than derived from the thing being measured is where a vacuous test comes from.** Prefer an
exact assertion where the quantity is exact, as a cell index is.

## A pose that adds, and a tool fitted to a doubled arm

`PawnFigureDirector.Strike` **adds** its angles to whatever the bones are already at. It does not
set them. `RegripTools` has always known this and resets with `figure.Graph.Evaluate(0f)` first,
with a comment saying why: refitting mid-swing "measures a doubled pose and a reach to match".

When `BindWorkBones` grew from fitting one tool to fitting one per `WorkStyle`, the loop struck once
per style **without** the reset, so the second style was posed on top of the first. The pick was then
gripped and measured against an arm reaching half as far again, and the result on screen was a
colonist at a rock face with its arms in the strike and **nothing in its hands** — the tool was
there, pointing at the sky above the top of the frame.

Two things worth keeping from it:

- **An empty hand in a screenshot is ambiguous and a number is not.** A tool the catalogue never
  supplied and a tool fitted somewhere absurd look identical at any distance, and they want opposite
  fixes. `PawnFigureDirector.DescribeTools()` answers the first question in one line (row present?
  prefab present? fitted on how many figures?), and `MeasuredBladeHeight` answered the second: 2.39 m
  against a colonist 1.79 m to the crown. The photograph said "empty"; the number said "above her
  head", and only one of those points anywhere.
- **The struck pose is the worst frame to judge a tool's head in.** The aim deliberately finishes the
  head just inside the work, so at the moment of the blow it is buried in the rock where nothing can
  see it — correct, and useless for deciding which way round the head is. Hold the stroke part way
  up the raise (`HeldPhase`) and photograph it against the sky. That is why `SwingCheck` shoots its
  blade sheet at a held phase rather than letting the stroke run.

## SwingCheck exhausts render textures in batch mode

`scripts/unity.sh exec Odyssey.EditorTools.SwingCheck.Run` segfaults inside
`Camera::CustomRenderWithPipeline`, reproducibly, after about six of its nine sample frames and
before it writes a single blade sheet. Running it twice — the usual cure for a batch run executing
the previous assembly — does not help.

The crash is not the first symptom. Further up `Logs/exec.log` is `RenderTexture.Create failed`
followed by "Failed to set the active render target": it is **resource exhaustion**, not a logic
fault. `PlayScene.Shoot` takes a render target per call, and `SwingCheck` calls it far more often
than the screenshot path does — nine samples, then an impact frame, then eight blade rolls, then
eight yaws. A give-away that it has already begun failing before it dies: every `Logs/swing-*.png`
comes out at exactly the same byte size, because they are failed captures rather than pictures.

Two consequences. **Do not diagnose this from `Logs/exec.log` after running `shot`** — `shot` writes
`Logs/shot.log`, and reading the stale `exec.log` from a previous `SwingCheck` run attributes a
crash to a command that only had a compile error. And until it is fixed, the pick's stroke angles
cannot be settled the way the axe's were; the workaround is the `shot-miner` and `shot-miner-raised`
frames in `PlayScene`, which render one setting at a time through the path that does work.

## A snapshot channel costs its whole layer, every tick, whether or not anything uses it

The mining line published a per-cell "how far through its order is this cell" channel by asking
`Fraction()` for **every cell of the active layer, every tick**. On the 120 x 120 board that is
14,400 calls a tick, and it cost **0.057 ms a tick on a board with no orders on it at all** —
against 0.002 ms for the entire rest of the simulation. The ten-day soak went from one second a
seed to thirty-nine, and the default Long tier from five seconds to two minutes sixteen.

The shape of the measurement is what identified it: **the job counts were identical** to the
pre-mining run (haul 14, eat 92, sleep 53, wander 2,595) and mean tick cost equalled p95. Same
work, constant overhead, no spikes — that is a fixed per-tick scan, not a feature doing more.

Three rules come out of it.

- **A channel is written every tick, so its cost is per tick, not per use.** A feature nobody is
  using should cost nothing. Walk the sparse list of things that have state (`_cells`, already
  kept sorted for the work-giver scans) rather than the dense grid they live in — tens of orders
  against fourteen thousand cells.
- **A sparse write needs an explicit clear.** The snapshot is double-buffered, so a byte written
  two frames ago is still there. The dense loop zeroed everything by accident; the sparse one has
  to `Clear()` on purpose, or a cancelled order stays drawn as a half-cut rock face.
- **A performance guard must run at the size the thing is used at.** The first draft of the
  regression test used the 60 x 60 board the rest of its file uses and *passed against the
  unfixed code*: the cost is proportional to the layer, so a quarter of the cells is a quarter of
  the bug, which sat inside the threshold. It only became evidence when it ran on 120 x 120 and
  was seen to fail.

## Bisect the measurement, and beware perl against CRLF

Two method notes from the same hunt.

**`git bisect run` with a measurement is fast and exact.** The probe script ran the one-day soak
(four seconds) and exited non-zero above 0.01 ms/tick; five steps over thirty-two commits named
the commit. Do it in the sibling checkout (`D:\code\odyssey-ui`) so the owner's editor keeps its
own working tree.

**A multi-line `perl -0pi -e 's/.../.../s'` silently does nothing against a CRLF file**, because
the `\n` in the pattern does not match `\r\n`. It exits 0 and reports nothing. This cost a wrong
conclusion: a stubbing experiment "proved" the suspect loop was innocent when in fact the stub had
never been applied — the file was unchanged. Read the file back, or use the Edit tool, before
believing an experiment that depends on an edit.

## Photographing a figure: the mesh is not square to its own root

Three traps, all found in one afternoon building `GestureCheck`, and all three produced pictures
that looked like a reasonable answer to the wrong question.

**A "side on" camera aimed at `facing + 90` shoots the colonist's back.** The arithmetic is right —
the camera really is perpendicular to the bearing the figure's transform reports — and the picture
is still square behind the figure. The **mesh inside a Synty character prefab is turned ninety
degrees from its root**, so a colonist faces across the yaw its transform carries. `SwingCheck`
never met this because it aims across the *line between the worker and the work*, which is a
world-space line and owes nothing to the rig.

Do not reason about it, and above all do not reason about it twice: shoot four bearings at the pose
worth judging and pick the profile out by looking, the way the axe's blade roll was settled. Two
rounds went into re-deriving the yaw convention from the source, both wrong in the same confident
way, before four pictures answered it in a single run.

**Ask the transform, not the field it came from.** `PawnFigureDirector`'s `Yaw` is the director's
own eased bearing; `Transform.eulerAngles.y` is what the figure is drawn at, and the gait clip's
root rotation sits between them. And neither can be had from the snapshot at all: the obvious
bearing is `NextCell - Cell`, which is **zero for a pawn standing still** — exactly what a pose
harness photographs. It falls back to a fixed bearing and the whole sheet comes out from one
arbitrary side.

**A measured value with an early return goes stale, and a stale measurement is worse than none.**
`MeasuredCrouchDrop` was written only when there was a crouch to report, so at the top of a motion
it kept the last non-zero reading: the log said 0.22 m of stoop beside a picture of a colonist
standing plainly upright. A measurement exists to be trusted over the picture — `MeasuredBladeGap`
is in the code precisely because a photograph can be read either way — so one that lies is the only
thing on the board with nothing to catch it. Clear it at the top of the pass that computes it.

**And characters draw flat yellow for the first frames after their material is first touched.** Not
magenta, so it does not read as a missing shader: the whole colonist is one flat unlit colour while
the ground and trees around it are correct, and it settles a frame or two later. Same class of thing
`ChipDirector` warms its particle material for, and the same fix — take a few throwaway pictures
first, where nobody is looking.

## The pose runs twice a frame, and only one of them starts from clean bones

`PawnFigureDirector.ApplyWorkPose` is called at the end of **both** `Sync` and `Evaluate`, and only
`Evaluate` evaluates the animation graph first. So every pose operation happens twice per frame:
once on a freshly written skeleton, and once on whatever the previous frame left behind.

For a **bone** that is harmless, and the file has always said so. Unity rewrites every bone on each
graph evaluation, so an additive `Pitch` is re-derived from scratch on the pass that is actually
drawn, and the stale pass's result is discarded unseen. This has been true and invisible since the
swing landed.

For **anything the graph does not own it is fatal**, because nothing ever resets it. Two things bit
on the same day (2026-09-16), both reported by the owner as visible faults and neither catchable by
a contact sheet, since the harnesses step the graph by hand, one pose per picture:

- **The axe span, fast.** The tool was kept still while the wrist turned by capturing its *world*
  pose and putting it back. On a child object, that writes a **local** rotation worked out from the
  parent's rotation at that instant — it never sets the tool to a known orientation, it only nudges
  it by the inverse of whatever the wrist just did. Two unbounded nudges a frame and it winds.
- **The off hand hunted round the haft and sometimes flipped across it.** `HandGrip.FaceHaft`
  searched for the best roll *starting from the hand's current rotation*. Three `Grasp` passes times
  two pose passes is six partial, quantised turns a frame, converging on nothing.

**The rule: a pose may add to a bone, because the graph rewrites bones; it may never add to
anything the graph does not own.** Props, search results and anything else outside the skeleton are
*placed* — computed absolutely from the fitting and the current state — so that running the pass
twice does nothing the second time and a dropped frame leaves no trace.

**And a corollary that cost an extra round.** The two hands are not the same problem. The off hand
is *reaching for* wood whose position is known, so seating it to the haft is right. The working hand
*carries the tool*, so its orientation belongs to the stroke and the tool's to the fitting: turning
that wrist to face the haft turns the blade with it, and the axe comes out facing the wrong way.
Applying the off hand's fix to both hands was a regression, and the owner caught it in one look.

**Measure it rather than trusting it.** `MeasuredToolDrift` is how far the worst tool had turned in
its fist since it was last put right. Zero is the only acceptable value, and it is "a tool never
spins" written as something a log can print.

## A state that is inferred is a state that is late, and a state that is partial

The owner reported that pausing "resets" every figure and that some "carry on for a moment". Two
symptoms, one cause: presentation was *guessing* at the pause instead of being told.

**The guess had to be late, by construction.** A stopped tick cannot be told from a slow frame
without waiting, so the director waited a quarter of a second before calling it a pause. That is
fifteen frames at sixty, and against the axe's 1.15 s stroke it is 24% of a swing that ran on after
the player pressed space. No tuning fixes this; only a real signal does. `WorldSnapshot.GameSpeed`
is now published with every frame, and the tick the bootstrap already spends letting a speed change
through is the frame that carries it, so the lag is one frame.

**And the guess was only ever wired to one thing.** It gated the swing, because the swing was what
somebody had noticed. Everything else in the pose pass went on easing, and the one that shows is
the gait: a paused pawn stops moving, so the measured speed is nought, so the figure's smoothed
speed is carried to nought at 0.35 a frame — **ten frames, 0.167 s, from 73.5% walk weight to 99%
idle**. Nobody wrote a line that resets a figure; the reset is what a blend to idle looks like at
sixty frames a second, and it was reported as a reset because from outside it is one. **When a flag
means "the world is stopped", every clock in the file is its business, not just the clock that was
in the bug report.** Running the whole pass on one delta that is zero while paused is the shape
that cannot be half-applied.

**Absence of movement is not a measurement of nought.** The general form, and the line that is most
of the fix: a frame in which the pawn had no opportunity to move says nothing whatever about how
fast the figure is going, so the last answer must stand. Reading it as a measured zero and
smoothing towards it is what made the figures snap.

**Unity's own clocks are not on the simulation's clock.** A `PlayableGraph` played with
`DirectorUpdateMode.GameTime` and a world-simulated `ParticleSystem` both advance on wall-clock
frames, and nothing in this project touches `Time.timeScale`, so both went on running through a
pause. The narrow fixes are a clip speed of zero (the same `SetSpeed` call `Blend` already makes
every frame, and it *continues* rather than restarting when the rate comes back) and
`main.simulationSpeed = 0`. Stopping the graph outright would leave the bones unwritten, which the
additive work pose needs.

**Measuring this with the editor open.** None of it can be run: a `PawnFigureDirector` cannot be
constructed outside a running editor, because its constructor builds a `ChipDirector` and
`Shader.Find` is a native call. What *can* be run is the arithmetic, and it is worth extracting for
that reason alone — pulling the speed smoothing out as a static over two positions
(`PawnFigureDirector.ObserveSpeed`) made the whole pause behaviour testable by an ordinary test, and
the test file itself was then executed headlessly by compiling it against the rebuilt
`Odyssey.Presentation.dll` with the NuGet NUnit and invoking every `[Test]` by reflection. Three of
the five went red against the old behaviour, which is the only reason they are worth anything.
## An Assume can hide a dead feature, and a green tier can mean nothing ran

Six of `MineJobTests`' thirteen tests open with a variant of `Assume.That(rock >= 0)` — the fixture
looks for a cell of rock a colonist could actually get at, and gives up if there is none. On `main`
that assumption was failing: **not one rock cell on the fixture's board had a stance a colonist
could reach**, so `NearestRock` returned -1 and the tests reported *inconclusive*. The default fast
tier prints those as part of a passing run. Mining was effectively dead on that board and the suite
said nothing.

It cost a wrong conclusion in this session. Checking "was this failing before my change?" by
stashing and re-running showed `Passed: 6, Failed: 0` and I read it as "these tests passed on main,
so I broke them". They had not passed; seven of them had not run. Only `-v n`, which prints a line
per test, showed six `Skipped`.

- **Read the total, not the verdict.** `Passed: 6 ... Total: 6` against a file with thirteen
  `[Test]` methods is the finding. Compare the count to the file before comparing anything else.
- **An `Assume` guards a fixture, not a feature.** It is right for "this seed happened not to put a
  pond here" and wrong for "the thing under test is unreachable", which is the failure itself
  wearing the fixture's clothes. Where the assumption is really a precondition the feature must
  meet, make it an `Assert`.
- **A skip is not a pass, and neither is a category.** The same board also hid this behind
  `Category("Long")`, which the default tier excludes — so the one test that would have run the
  colony for a day only ran when something passed an explicit filter.

## A merge that auto-resolves in the wrong direction, and a compile check that lies

Reconciling the vertical-movement line (which deleted climbing as a mining mechanic) with the
gesture line (which had just finished the climb *pose*, legs and all) produced two separate traps
worth the same hour twice.

**Git flagged three conflicts and silently dropped four more things.** The conflicts were the
obvious ones — the pose methods, and two documents both lines had appended to. What auto-merged
without a murmur was every deletion made in a region the other branch had not touched:
`World = _model` in the composition root, the whole climb shot in `PlayScene`, the `Figure` fields
(`ClimbPhase`, `ClimbWeight`, `ClimbFace`), and the `ClimbLean` / `ClimbEaseSeconds` constants.

The result compiled in the reviewer's head and would have run wrong: with no world mirror,
`TryWallBeside` returns false, `ClimbFace` stays zero, and `ApplyClimbPose` is gated on it — so the
pose would have been present, correct, and **never executed**, with the one diagnostic that could
have shown it also deleted.

- **A merge conflict list is not a change list.** After a merge that removes a feature one side
  extended, grep the merged tree for the feature's own vocabulary and check each hit is where you
  expect. `git merge-tree --write-tree` gives you the merged blobs without touching a working tree,
  so this can be done *before* committing to the merge.
- **Resolving conflict hunks is not the same as taking a side.** Keeping "ours" in five hunks still
  left the file broken, because the losses were outside the hunks. Taking the whole file from the
  branch that owns the feature and re-applying the other side's few real additions was quicker and
  correct — and the second approach is the one to reach for first when one side deleted a subsystem.

**And the headless compile check can bind stale assemblies.** `docs/lessons.md` above recommends
compiling the Unity-only assemblies against `Library/ScriptAssemblies`. In a *worktree* that is a
trap: the built DLLs there belong to whatever branch the main checkout is on. Worse, pointing the
generated csproj at freshly built DLLs did not fix it — the compiler went on reporting members that
were plainly in the source (`MoveCost.Drop`, `WorldSnapshot.Running`) even after the only Odyssey
references in the project were the fresh ones, after `obj/` was cleared, and after every prebuilt
`Odyssey.*` was excluded. Roughly an hour went into that, and none of it was a real defect.

**Compile the sources, not against the binaries.** One csproj that includes `Sim.Contracts`, `Sim`,
`Hud`, `Presentation` and `Editor/Odyssey` as `<Compile>` items, referencing only Unity's own DLLs
and the package assemblies, builds in seconds and answers the only question a handoff needs: do
*these sources* agree with each other. Assembly-definition boundaries are then unverified, which is
what `scripts/unity.sh test editmode` is for.
