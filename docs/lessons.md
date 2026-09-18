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

**A pull request that conflicts with `main` reports *no checks at all*, which looks exactly like a broken runner.** `gh pr checks` says "no checks reported on the branch" and `gh run list --branch …` is empty, so the first instinct is to go and read `ci.yml` and check whether the runner is online. Neither is the problem. The workflow triggers `on: pull_request`, and a `pull_request` run is built against the *merge* commit — GitHub cannot compute one for a conflicting PR, so the run is never created. The tell is `gh pr view <n> --json mergeable`, which says `CONFLICTING`; merge `main` in, resolve, push, and the checks appear within seconds. **Check `mergeable` before investigating CI**, and be aware the PR page shows this as an absence rather than as a failure, so nothing is red and nothing says why.

**Verify a green Unity tier actually compiled what only it can compile.** The fast tier builds `Odyssey.Sim`, `Odyssey.Sim.Contracts` and the two test projects through the dotnet mirrors; it does **not** build `Assets/Editor/Odyssey/**` or `Odyssey.Presentation`. A change that touches the editor tools is therefore a third unbuilt until the Unity job runs, and "Unity tests — pass" in the same wall-clock time as the fast tier is worth one look rather than a shrug. `gh run view <run> --log --job <job>` and grepping for `error CS`, for `Odyssey.Editor`, and for the names of the files you changed settles it in one command.

## Testing

**The tests are not the slow part.** The suite executes in about 40 ms. A Unity EditMode cycle takes minutes, and essentially all of it is Unity booting, refreshing the asset database (~7 s) and reloading the script domain (~3 s compile). **Filtering which tests run therefore saves nothing.** The only thing that helps is not starting Unity.

**Two tiers.** `scripts/test-fast.sh` runs the same Sim test sources through mirror projects in `tools/dotnet/` with no editor, in about 1.7 seconds warm. `scripts/unity.sh test editmode` takes about 37 seconds with the watchdog and is the authority, because only Unity proves the assembly-definition boundaries hold and only Unity can run editor or PlayMode tests. Work in the fast tier, gate on the slow one. See `docs/setup/local-dev.md` §10.

**Unity ships a .NET *runtime*, not an SDK.** `dotnet --list-sdks` against a runtime-only install prints an error to stdout and still exits 0, so the exit code cannot be trusted; check for an actual version line. Install a real SDK without admin rights with the official script, which lands in `%USERPROFILE%\.dotnet`.

**A remote container can run the whole fast tier, and the SDK comes from the distribution, not from Microsoft.** The container images used by Claude Code on the web carry python3 but no dotnet, and the official installer is useless there: `dot.net/v1/dotnet-install.sh` redirects to `builds.dotnet.microsoft.com`, which the egress proxy refuses outright (`CONNECT tunnel failed, response 403` — a policy denial, so retrying it only spends the session's time). The Ubuntu archive *is* reachable, and 24.04 packages the SDK, so `apt-get install -y dotnet-sdk-8.0` puts 8.0.131 on the path in about a minute and `scripts/test-fast.sh` then restores from nuget.org and runs every Sim test — 227 passed, 4 s cold, on 2026-09-16. **So "no Unity" does not mean "no gate" for Sim work:** every row in `docs/plans/overnight-queue.md` tagged **C** can be proved in a container, and only the **W** rows genuinely need the Windows machine. Still, **check `dotnet --version` before promising "fast tier green"** rather than assuming it: if an image ever has neither the SDK nor a reachable archive, the only safe rows are the ones whose done-when needs no test run at all. What the container cannot do at all: Unity itself (assembly-definition boundaries, editor tooling, PlayMode, frame time), and web research — `rimworldwiki.com`, `dwarffortresswiki.org`, `steamcommunity.com` and even `en.wikipedia.org` are blocked for both `curl` and `WebFetch`, leaving only the `WebSearch` tool's own extracts, which is thinner than a research row's format asks for.

**A test that skips what it cannot see must not then demand a count, or it demands the licensed
packs.** `EveryFloorSlabPutsItsWalkingSurfaceOnTheCellFloor` skipped every catalogue row whose art
was missing — right, and the comment said why — and then asserted that at least ten rows had been
checked, to stop the loop passing by never running. Both halves are reasonable and together they
require `Assets/Synty`, which the self-hosted runner's checkout does not have. **It was the Unity
tier's only red for six consecutive runs on `claude/grey-floor-layer`**, looked exactly like the
branch's own work, and was neither. Tell the two cases apart by asking the *library* rather than the
environment: if nothing in the whole catalogue has art there are no packs here and the rule has
nothing to measure; if something does, the count must hold. The standing rule it broke is in
`CLAUDE.md` — *never let the simulation or its tests depend on the packs* — and the way it broke it
is invisible on any machine that has them, which is every machine a person works on.

**The two tiers do not run the same NUnit, and the fast tier's is the newer one.** Learnt on
2026-09-17, twice in one unit, at a Unity run each. A test written and proved green in the fast
tier can fail to *compile* or fail at *runtime* under Unity because its assertion vocabulary is
larger there. Two that bit:

- **`Assert.Multiple(() => { ... })` does not exist** in Unity's bundled NUnit. It is a compile
  error — `CS0117: 'Assert' does not contain a definition for 'Multiple'` — which aborts the whole
  batch run before a single test executes, so the failure looks like a broken build rather than a
  test problem. Write the assertions out; stopping at the first failure costs nothing here.
- **`Has.Count.EqualTo(n)` throws on an interface-typed collection.** Against
  `IReadOnlyList<T>` Unity's NUnit raises `ArgumentException: Property Count was not found` at
  runtime, because it resolves the property against the declared type and does not find
  `IReadOnlyList<T>.Count` the way the newer one does. It works on `List<T>`, which is why some
  uses in the same file passed and others did not — the rule is about the *declared* type, not the
  object. Write `Assert.That(thing.Count, Is.EqualTo(n))`.
- **`Does.Not.Contain(x)` resolves to the string overload** (2026-09-18, a third Unity run). Under
  Unity's NUnit the negated form only offers `Does.Not.Contain(string)`, so against a
  `HashSet<int>` it is `CS1503: cannot convert from 'int' to 'string'` — and the *positive*
  `Does.Contain(x)` on the same collection two lines above compiles perfectly well, which makes the
  error read as nonsense. Write `Assert.That(set.Contains(x), Is.False)`.

The general rule: **the fast tier proves behaviour, the Unity tier proves the code exists in the
form Unity accepts**, and that includes the test code. Neither of these is catchable by reading;
both are one `scripts/unity.sh test editmode` away, so run it before saying a test suite is done
rather than after.

**A fixture-wide default is a silent precondition on every test in the file, and it can switch off
the thing the file exists to measure.** Found 2026-09-17, and it had been hiding a real fault for
as long as the fault existed. `BankFootingTests` measures whether a walking figure's drawn height
is continuous — five crossings of a terrace, four hundred samples a step, with a comment saying
these are the tests that matter because a height that jumps reads as a teleport and would be blamed
on the animation. Its `[SetUp]`/`[TearDown]` call `GroundRelief.Reset()`, which sets `Amplitude` to
**zero**, and at zero amplitude `GroundRelief.Lift` returns its argument unchanged. So every one of
those cases ran on a perfectly flat field, while the board the game loads carries a 2 m one
everywhere — and an 81.9 mm per-frame snap sat under them, undetected, until somebody measured the
same thing with the field switched on.

The cheap guard is a **control that fails if the fixture is inert**: one test that asserts the
condition being varied is actually varied (`WalkOnReliefTests.TheFieldIsOnAtAll` checks two
neighbouring cells are drawn at different heights). Without it, "twelve tests pass" and "twelve
tests are vacuous" look identical from the outside. Ask of any fixture that resets global state:
*what does this reset turn off, and is it the thing I am testing?*

**Measure the control even when you are sure, because it can reverse the reading of the number.**
Same day, same file. A second, smaller jump turned up at bank boundaries — 28.7 mm with the relief
on. Taken alone it reads as a second relief bug. The same crossing on a flat field is **30.0 mm**,
so the relief is not the cause and is marginally kinder, and the number belongs to the bank surface
and predates everything being worked on. The control cost one extra call in a test that was already
running. Without it, an afternoon goes on the wrong thing.

**A photograph taken to answer a question has to be checked for whether it answers it.** Found
2026-09-17 building `WaterDepthCheck`, which took three unusable sets of shots first. A low camera
near a stream looks *through* the bank, because a stream is cut into a channel — 12° at 10 m and
14° at 9 m both put the lens inside the ground. A scale post banded over its lower metre is useless
for comparing waterlines that are all above a metre: every band was submerged. And two reference
objects the same colour cannot be told apart in the resulting picture. This is the same discipline
as re-running a test after a fix: an artefact produced to settle a question is not done until it
has been looked at with that question in mind.

**A frame is not a tick, and waiting one frame for a simulation effect is a flake.** Found
2026-09-17, one failure in three PlayMode runs. `OdysseyBootstrap` accumulates real time and steps
the simulation only when it has a tick's worth, so **a Unity frame contains zero or more ticks
depending on how long it took**. A test that submits an intent and then does `yield return null`
before asserting is really asserting that the frame happened to be long enough — which it is, most
of the time, on this machine. Wait for the *effect* with a bounded loop instead:

```csharp
for (int frame = 0; frame < 120 && boot.World!.GameSpeed != wanted; frame++) yield return null;
Assert.That(boot.World!.GameSpeed, Is.EqualTo(wanted), "the request never reached the simulation");
```

It is still a real assertion — a request that never arrives exhausts the budget and fails with the
same message — and it does not get slower, because it stops as soon as the effect lands. The same
applies to anything downstream of a tick: a published snapshot, a job starting, a designation
clearing.

**The fast tier compiles neither Presentation nor Editor.** `scripts/test-fast.sh` builds only the
two mirror projects, `Odyssey.Tests.Sim` and `Odyssey.Tests.Hud`, so a green fast tier says nothing
at all about `Assets/Odyssey/Presentation/`, `Assets/Editor/` or the scene wiring. A unit that
touches the composition root or the HUD shell is **unproven until Unity has compiled it**, however
green the 11-second run looks.

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

**A partial EditMode run reports zero failures, and the only tell is the test count.** On
2026-09-16 three runs in a row reported `total="752" passed="750" failed="0"` and were believed;
the same tree run cleanly reports **827**. Seventy-five tests had not run at all, and nothing in the
output said so — the results file is written, the summary is green, and a run that never reached an
assembly cannot report anything about it. The cause was two Unity processes sharing the project:
`check_project_lock` looks for `Temp/UnityLockfile`, and a run launched in the gap before the
previous one has written its lockfile walks straight past the guard. **So read the total, not only
the failures.** The cheap cross-check needs no Unity:

```
grep -rho "\[Test\]\|\[TestCase" --include=*.cs Assets/Odyssey/Presentation/Tests Assets/Odyssey/Tests | wc -l
```

817 attributes on that tree against 827 cases, the difference being parameterised expansion. A total
that has *fallen* since the last run is the signal; it is worth a glance before quoting a number in
a commit message or a pull request, because a number from a partial run is exactly the plausibly
wrong result this project's own rule warns about. The same check catches the other direction: a
count that is far below the attributes means an assembly is missing from the run.

The fast tier moved the same day for a reason that was never established — 412 Sim and 44 Hud early
on, 425 and 51 later, with no test added to either assembly in between. Whatever the cause, the
habit is the same: **the count is part of the result, and a tier is only green against a count you
recognise.**

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

## An instrument wired to the thing it measures reports a perfect result

`BankCheck` shoots a colonist standing on a slope with the lift off and then on, and prints how far
her boots are from the surface. The first version read the gap through `BankLayout.RiseAt`, which is
gated on the very lever the sheet is sweeping — so with the lift off it compared the feet against a
surface it had just been told was flat, and printed **0.000 m in both conditions**. Two perfect
scores, no fault anywhere, and the photographs beside them plainly showed a woman buried to the
shoulders.

The shape of the mistake generalises past this harness: **a measurement must not pass through the
switch being tested.** `BankLayout` has a second `RiseAt` overload taking a bank already in hand,
which answers what the geometry is doing regardless of whether anything is being lifted onto it, and
reading through that gives −1.500 m and 0.000 m as it should. When an A/B harness reports that its
two conditions agree exactly, suspect the instrument before believing the result — a real
no-difference is noisy, and an exact one usually means the two sides are the same code.

The other half of the same lesson: **measure everyone, not the subject.** The sheet framed one
colonist, and a second in the corner of a wide shot still looked sunk. A figure inside a ramp and a
figure standing behind one are identical from every bearing, because a bank is opaque and nearly as
tall as a person, so no photograph could settle it. Printing the gap for all five answered it in one
line: three were in a bank, all three at −1.500 m and then all three at 0.000 m.

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

**Rebuilding the module catalogue takes two commands, and the second one is not optional.** `PlayScene.RebuildCatalogue` regenerates the 138 rows from code and **drops the `appearance` block** — the 311 atlas swatch rectangles that clothe the 61 colonists, which are classified by a different tool entirely. `CharacterSwatches.Classify` has to run straight after it to put them back:

```
scripts/unity.sh exec Odyssey.EditorTools.PlayScene.RebuildCatalogue
scripts/unity.sh exec Odyssey.EditorTools.CharacterSwatches.Classify
```

**A rebuild alone looks like it worked**, which is the whole danger: it exits zero, keeps all 138 rows, keeps every prefab reference, and nothing warns. On 2026-09-18 the loss showed up only as a 2,160-line deletion in `git diff --stat` on a change that should have added one line per row — so **read the stat after regenerating a generated asset**, and if it is not the shape you expected, find out why before committing. `CharacterSwatches`'s header already says it writes only appearance and deliberately does not rebuild; nobody had written down the inverse.

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

Two more, found on 2026-09-16 doing exactly this for a camera change:

- **Exclude `Presentation/Tests/`.** It is its own assembly definition and needs NUnit, so a glob of
  `Presentation/**/*.cs` fails with a screenful of `CS0246: NUnit could not be found` that has
  nothing to do with the code being checked. Adding NUnit to compile it is also an option; excluding
  it is faster when the question is only "does the game code still build".
- **Glob the references rather than naming them.** `$(UnityManaged)\UnityEngine*.dll` and
  `Library/ScriptAssemblies/Unity.RenderPipelines*.dll` in one `<Reference Include>` each. The
  Presentation assembly reaches into particles, physics, animation, the Playables graph and the
  render pipeline, and naming the modules one at a time is a game of whack-a-mole against an error
  list that only reveals the next missing one. `Unity.InputSystem.dll` is needed too, and lives in
  `Library/ScriptAssemblies/` rather than with the engine.

Two traps while doing it. MSBuild reads `<HintPath>` as XML, so a Windows path written with
backslashes dies on `MSB4025: hexadecimal value 0x0C is an invalid character` — the `` in a path
segment. **Write every path in a generated csproj with forward slashes**; MSBuild accepts them and
the error message points nowhere near the cause.

**Presentation tests can be *run* the same way, not merely compiled** (2026-09-17). Point a throwaway
`net8.0` project with NUnit and the test adapter at the test file, reference the DLL the paragraph
above builds plus `UnityEngine.CoreModule.dll`, and `dotnet test` runs them in about a second while
the editor holds the project. **What stops it is an engine ECall**: anything implemented natively
throws `SecurityException: ECall methods must be packaged into a system module` outside the player.
`Matrix4x4.TRS` is one of them; `Matrix4x4.identity`, `operator *`, `MultiplyVector`, `GetColumn`,
`Mathf` and the vector types are managed and work. So a value that is only ever scaled and
translated is better built by writing the seven fields out — it is one multiply cheaper, and it is
the difference between geometry that can be measured in a test and geometry that can only be looked
at. `GroundRelief.Drape` had already made the same choice for its shear.

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

## A tint aimed at a property a shader does not declare fails silently

The owner reported the grass clumps drawing yellow when they should be green. It took **three wrong
explanations reasoned out of the source** before anyone measured, which is the standing lesson in
this file arriving again, so the method is worth recording as much as the answer.

- **`Material.SetColor` on a property the shader does not declare does nothing and reports
  nothing.** `MaterialCache` writes `_BaseColor` and `_Color`. The grass clumps use
  `Synty/Foliage`, which declares neither, so every value ever put in the foliage tint table was
  decorative — the table looked like a working lever for as long as nobody moved it. Note that the
  same file reaches emission through `_Emission_Color` and the cutout through
  `_Alpha_Clip_Threshold`, both Synty Shader Graph names: the evidence that the pack does not use
  URP naming was already in the file, two lines above the code that assumed it did.
- **Synty foliage is procedural.** There is no albedo texture to tint. `Synty/Foliage` mixes a leaf
  from `_Leaf_Base_Color`, `_Leaf_Noise_Color` and `_Leaf_Noise_Large_Color`, and the last of those
  is `(0.50, 0.58, 0.06)` — the near-zero blue against a red nearly as high as the green is exactly
  what "straw" is. Those three are the only handles there are.
- **To turn a yellow-green green, bring red down.** Lifting blue is the instinct and it is a weak
  handle, because green is a low-blue colour too: multiplying 0.06 by two is still 0.12. The
  multipliers that work look lopsided (`0.55, 1.00, 2.20`) and that is why.
- **Two honest pictures of one asset disagreeing is the tell.** `ScatterSheet` instantiates prefabs
  untouched and showed green clumps; the board drew the same clumps yellow. That difference *is* the
  bug localised to the draw path, and it was sitting in the logs from the first run. The other half
  of the answer was that the ground is deliberately lifted by `(1.04, 1.30, 1.55)` while the tufts
  were not moved at all, so even correct art reads warm on a cooled field.
- **Enumerate, do not guess at names.** `TintProbe`
  (`unity.sh exec Odyssey.EditorTools.TintProbe.Run`) prints each module's material, its shader, and
  every colour property that shader actually declares. A list of eleven plausible names matched
  none of them; asking the shader took one run and answered it completely. Reach for it whenever a
  tint, an emission or a cutoff appears to have no effect.

## Variety at cell scale needs a tile set that agrees at its edges, or it reads as noise

Three separate attempts at "make the ground less uniform" failed the same way and it is worth
stating once. The rim ripple gave every cell its own top and produced cracks. The bank gave every
cell its own jittered tread positions and produced a ridge of misaligned bars — "these Toblerone
pieces", in the owner's words. Both were varied, both were individually correct, and both read as
noise because **neighbouring cells did not agree along the edge they share**.

- **The fix is not less variation, it is variation that is continuous.** The bank ended up as three
  height functions — `z`, `max(x, z)`, `min(x, z)` — chosen precisely because along any shared edge
  two of them collapse to the same expression. A run of them is one surface with no seam to find,
  and the width is matched by construction rather than by tuning.
- **Rotation is what keeps a tile set small.** Sixteen patterns of exposed sides fold onto five; the
  three bank shapes cover every corner in both directions. Folding is free because the yaw rides in
  the instance matrix, so the cost of a tile set is meshes, and meshes are buckets, not instances.
- **When the answer is a tile set, the per-cell hash goes away entirely.** Every version that kept a
  hash "for variety" was the version that broke, because a hash cannot know what its neighbour
  chose. If a shape depends on its surroundings, its surroundings must be the only input.
- **The instrument has to be pointed at the fault.** All of this was visible in a close shot and
  invisible at 70 m, and it was reported from close up while the sheet was being judged from far
  away. `SlopeCheck` shoots a 14 m macro for that reason now.

## Coplanar surfaces flicker only when they face the same way

A bank fills its cell in plan, so an inside corner where two terrace steps meet was drawing two
banks in one cell, turned ninety degrees to each other. The side wall of one then lands in the same
plane as the *back* wall of the other, **facing the same way**, and the depth buffer has nothing to
choose between them — so it picks whichever rounds higher, and the choice changes as the camera
moves. That is z-fighting, and the owner saw it before any test did.

The distinction is the useful part. Coplanar surfaces with *opposite* normals are harmless, because
back-face culling removes one of them from every viewpoint — which is why a straight run of banks,
whose touching walls face away from each other, never flickered. Only the corner did. When hunting a
flicker, look for same-facing coplanar pairs and ignore back-to-back ones.

It is also worth noting what no test could have caught: every mesh was watertight, every face was
wound correctly, every instance was in the right place, and the fault was a *relationship between
two of them*. Geometry tests check one mesh at a time.

## Getting the licensed packs into a worktree without copying or committing them

A git worktree is a fresh checkout, and `Assets/Synty/` is gitignored, so a worktree has no art at
all. Everything still builds and every test passes — degrading without the packs is a designed path,
not an error path — but **every screenshot is untextured flat colour**, which makes a worktree the
wrong place to settle any question about how something looks. That cost a whole contact sheet once.

**Junction the folder rather than copying it.** On Windows, from the worktree:

```
cmd /c mklink /J "<worktree>\Assets\Synty" "D:\code\odyssey\Assets\Synty"
copy "D:\code\odyssey\Assets\Synty.meta" "<worktree>\Assets\Synty.meta"
```

`mklink /J` makes a directory junction and needs no administrator rights, unlike `/D`. Three reasons
it beats a copy of 1.5 GB and 15,868 files:

- **The `.meta` files are shared, so the GUIDs match.** This is the load-bearing part.
  `ModuleCatalogue.asset` is committed and refers to prefabs by GUID, so art that imported under
  different GUIDs would resolve to nothing and the world would draw as boxes *with* the packs
  present — which looks exactly like not having them and is far more confusing.
- Nothing is duplicated on disk, and nothing can drift out of step with the main checkout.
- `Assets/Synty/` is gitignored in every worktree too, so `git status` stays empty and licensed
  content cannot be staged by accident. Check that before the first commit, not after.

Two things to know. The two projects share the source files, so if one of them rewrites an import
setting the other sees it — Unity does not rewrite an existing `.meta` during an ordinary import, but
changing an importer setting in one project changes it for both. And the worktree still builds its
*own* `Library`, so the first run after junctioning imports the whole pack set and takes many
minutes and a couple of gigabytes; run it in the background and do something else.

Remove the junction with `rmdir` (not `Remove-Item -Recurse`, which on some shells follows the link
and would delete the real packs).

**And not `git worktree remove` either — that is the same trap and it is not obvious** (2026-09-17,
and it cost the packs). Tearing down a junctioned worktree with

```
git worktree remove --force .claude/worktrees/<name>
```

made git walk the tree deleting as it went, **follow the junction, and empty the real
`D:\code\odyssey\Assets\Synty`** — 15,868 files, 1.54 GB, gone, and the command then failed with
`Permission denied` so it looked like nothing had happened. Every other junctioned worktree
(`odyssey-look`, `odyssey-ui`) went dark at the same moment, because they all point at the one real
copy. Nothing goes to the recycle bin.

**The order that is safe:** remove the junction first, then the worktree.

```
cmd /c rmdir "<worktree>\Assets\Synty"      # unlinks; does NOT touch the target
git worktree remove --force .claude/worktrees/<name>
```

Before deleting any tree that a worktree owns, ask whether anything under it is a reparse point:

```
Get-ChildItem <path> -Recurse -Force -Directory | Where-Object { $_.LinkType }
```

**If it has already happened**, the packs are recoverable without re-downloading: `D:\code\odyssey-audio`
holds a *real* copy rather than a junction. `robocopy <source> <dest> /E /COPY:DAT /DCOPY:DAT` restores
it byte for byte in about ten seconds, and the `.meta` files come with it, so the GUIDs are the ones
`ModuleCatalogue.asset` already refers to — check one before believing it, e.g. that
`PolygonGeneric\Prefabs\Base\SM_Bld_Base_Wall_01.prefab.meta` still reads
`guid: d6b56504304c325419b598fe3ddb95ed`. **Keeping one real copy somewhere is what made that
possible**, so do not "tidy" `odyssey-audio` into a junction as well.

**And `odyssey-audio` is no longer a spare copy — it is the live one** (measured 2026-09-18, and
this paragraph used to imply otherwise). The links now run in a **chain**: the five junctioned
worktrees point at `D:\code\odyssey\Assets\Synty`, and *that* is itself a junction pointing at
`D:\code\odyssey-audio\Assets\Synty`, which holds the only real directory — 15,868 files, 1.54 GB,
eight packs. The main checkout does not own its own art.

Two consequences, and the second is the dangerous one:

- **Everything dies at one remove.** `rmdir` on the main checkout's `Assets\Synty` unlinks only that
  hop, but it also cuts the five worktrees that point through it, because their target stops
  resolving. Any recursive delete of the main checkout's `Assets` follows the chain into
  `odyssey-audio` and takes the real packs with it.
- **The only real copy is sitting inside a worktree that looks disposable.** `odyssey-audio` is on
  `claude/audio-framework`, which is **merged into main and behind it** — exactly the profile of a
  branch somebody tidies up without thinking. `git worktree remove` on it, or a recursive delete of
  `D:\code\odyssey-audio`, destroys 1.54 GB of licensed art that is gitignored and recoverable only
  by re-importing the `.unitypackage` files.

**Check before pruning any worktree**, with the `LinkType` command above, or:
`Get-Item <path>\Assets\Synty -Force | Select Attributes, Target` — a `ReparsePoint` is a link and
safe to `rmdir`, anything else is the real thing. The arrangement wants inverting when somebody has
a quiet moment: the real directory belongs in the **main checkout**, with every worktree and
`odyssey-audio` junctioned to it, so that the packs live where the project does and every worktree
is genuinely disposable.

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

## Driving the mouse in a PlayMode test: three silent failures, in order

`OQ-40` was blocked for a week on "mouse input cannot be driven in a PlayMode test". It can. Three
separate things stop it, each of which looks exactly like the others from outside, and none of
which reports anything:

1. **There is no mouse.** `Mouse.current` is null in a batch run: no window, no pointer, no device.
   Queueing state at it does nothing, and `SliceCameraRig.ReadMouse` returns immediately when the
   device is null, so nothing downstream can be reached. `InputSystem.AddDevice<Mouse>()` fixes it.
2. **The device you add is disabled.** `backgroundBehavior` defaults to
   `ResetAndDisableNonBackgroundDevices` and a batch player is never focused, so the device is
   disabled and every event is dropped. Set `InputSettings.BackgroundBehavior.IgnoreFocus` and
   enable the device. This one is also why an early attempt looked like it worked: between adding
   a device and focus being applied there is a window where events do land, so the same test passed
   when it ran first and failed when it ran second.
3. **Nothing processes the queue, and a queued event does not survive the frame.** The player loop
   never calls `InputSystem.Update()` in a batch run, and queueing in one frame and updating in the
   next delivers nothing — queue and update have to be one act.

Then there is the observation problem on top: **a test coroutine resumes after every `Update` has
run**, so it is always too late to see a delta control, which is spent within the frame. A
coroutine reading `mouse.scroll` therefore cannot tell "delivered and consumed by the game" from
"never delivered at all". That is what made the earlier three tests pass vacuously.

The answer is `MouseHarness` plus `InputPump` in `Tests/PlayMode`: a component at
`DefaultExecutionOrder(-10000)` that takes posted state, queues **and** updates at the top of the
frame, and records what the device read immediately afterwards. The game then reads it through its
ordinary path later in the same frame, and the recording is what lets the harness fail loudly.

**Assert the intent, not the smoothed value.** The first working version still failed: a notch
moved the camera's *target* by six units but its drawn `distance` by 0.457, because the rig smooths
exponentially and a batch player runs frames in about a millisecond. The same test would have
passed on a machine running at sixty frames a second. `SliceCameraRig.TargetDistance` exists for
this: it moves the instant input is read and does not drift, so the assertion and its control are
both exact. Any frame-rate-dependent assertion is a flaky test waiting for a faster machine.
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

## A generated file that is also committed fails silently when it goes stale

`Assets/Scenes/Play.unity` is committed *and* generated — `PlayScene.cs` is the generator and the
scene is its output. Add a component to the generator and the committed scene does not have it
until somebody runs **Odyssey → Presentation → Build play scene**.

Until they do, the feature is simply **absent**. No error, no missing reference, nothing in the
console. It is indistinguishable from a broken feature, and that is exactly how it was read: the
designate tool was reported as "nothing happened" and "I couldn't mark anything", when in truth
nothing was there to respond. A playtest round was spent on it.

The same trap caught the same session twice over, in two forms:

- **The owner's checkout was 22 commits behind** and the build under test predated every change
  being tested. Three further observations — a pause that reset a walker to standing, colonists
  repeating a bad move, animations "a mess" — were all faithful reports of bugs that had already
  been fixed on `main`. **Before reading a playtest report, confirm the commit it was taken
  against.** `git log --oneline -1` in their checkout costs nothing and reframes everything.
- **"git pull" was written as one bullet in a list of steps**, and the whole exercise depended on
  it. A step that everything hinges on is not a bullet; and handing over instructions in the same
  message as the merge they depend on guarantees a race.

Two rules follow.

- **A generated artefact under version control needs a staleness check, not a convention.** The
  wiki and the label registry already have one — `build_wiki.py --check` and `emit_labels.py
  --check` exit 1 when the output does not match the source, and both are CI gates. The play scene
  has no equivalent. Until it does, `OdysseyBootstrap.WarnIfTheSceneIsStale` at least turns silence
  into a console line naming the menu item.
- **Warn, do not self-heal.** Adding the missing component at runtime would paper over a scene
  that may be stale in ways the check cannot see — the camera rig, the lighting, the module
  catalogue. The useful signal is "rebuild the scene", not "one thing was quietly patched".
## The owner playtests `main`; a worktree is invisible until it is merged

Four rounds of "it still does not work" were spent on a fix that was **green on every tier and not
in the code being played**. Work done in `.claude/worktrees/` sits on its own branch. The owner
opens `D:\code\odyssey`, which is on `main`. Until the branch is merged or pulled, every playtest
exercises the old behaviour — and reports back a symptom that is a perfect description of the bug
that was just fixed, which reads exactly like the fix not working.

**Confirm delivery before diagnosing.** One command settles it, and it is cheaper than any
hypothesis:

```
git show main:<the file you changed> | grep <the thing you added>
```

If it is not there, stop. Do not look for a second cause, do not write another test, do not
theorise about serialised defaults — say where the code is and how to get it. Every minute spent
diagnosing before that check is spent on a machine state that does not exist.

Three things make this trap worse here:

- **`main` moves under you.** Other sessions land PRs through the day, so the owner may genuinely
  be pulling fresh code — just not *your* fresh code. "They must have merged by now" is not a
  check.
- **A worktree session cannot merge for them.** Git operations outside the worktree are refused, so
  the only ways across are the owner running `git merge <branch>` or a pushed PR. Offering and
  waiting is not delivery; ask once, then make it the first line of the reply, not the last.
- **The symptom is indistinguishable from a real regression**, so every measurement you take comes
  back consistent with "still broken". That is what makes it burn hours rather than minutes.

Related: a stale `Play.unity` fails the same way for a different reason — see the generated-file
lesson above. Both end with the owner reporting a working feature as broken.

## A promising hypothesis about serialised defaults, and why it was wrong

Worth recording because it is the obvious wrong idea and it will occur to the next person.

`SliceSettings` is a `[Serializable]` field on `SliceCameraRig`, so **the scene's copy, not the
field initialiser, is what the game runs**. `Play.unity` was generated before `followDepth` and
`surfaceLayer` existed and its YAML contains neither, while its `above` is `3` — `Xray`, under
which nothing above the slice is a pointer target. That is a complete, self-consistent explanation
for "a click will not leave the active layer", arrived at from reading the YAML.

**It is wrong.** Measured by opening the scene in an EditMode test and printing what loads:

```
[Scene] slice as loaded: followDepth=True, above=Xray, ... surfaceLayer=0
[Scene] at L12: above=Full, selectable L9..L15
```

Unity **keeps the field initialiser** for a field missing from the YAML — it does not zero it. So a
field added after a scene was generated takes its code default, and only fields actually present in
the file override. `PlaySceneContentsTests` holds that measurement.

The general rule this belongs to is the one at the top of this file: **reading code and reasoning
about a framework's behaviour has been wrong every time.** Unity's deserialisation of a missing
field is a fact to be measured, and measuring it took one test and four minutes against an
afternoon of a confident wrong answer.

## The CI runner is the owner's machine, and a timing test cannot tell you apart from a regression

`HudStressTests.Adr0003_F1_TheDenseHudHoldsItsBudgetAndAllocatesNothing` failed on CI —
**expected under 1.167 ms, measured 2.000 ms** — on a commit whose PlayMode tier had passed 8/8
in a worktree minutes earlier. Nothing in the change touched the HUD. Re-running the same job on
the same commit was green.

The cause is that the Unity tier's self-hosted runner **is the Windows dev machine**, so
`scripts/unity.sh test` in a worktree and the CI job are two Unity instances competing for one
CPU and one GPU. A frame-budget assertion cannot distinguish "the code got slower" from "somebody
else was compiling shaders", and it fails in the direction that looks like a regression.

- **Before pushing, stop running Unity locally**, or expect to re-run the job. The window that
  matters is the minute or two after the push, which is exactly when it is tempting to keep
  working.
- **A timing failure on CI that passes locally on the same commit is contention until proved
  otherwise**, and the proof is one `gh run rerun --failed`. Do not start bisecting a performance
  regression that the next run will not reproduce.
- **Read the whole report before believing the headline.** The run that failed also carried
  `EditMode 696 total, 694 passed, 0 failed`, identical to the local run — which already said the
  change was innocent and narrowed it to one timing assertion.
## A scene rebuild in a packless worktree quietly guts the art catalogue

`scripts/unity.sh exec Odyssey.EditorTools.PlayScene.Build` does what it says and also rewrites
`Assets/Odyssey/Presentation/ModuleCatalogue.asset`. In a worktree with no `Assets/Synty` — which
is every worktree that has not had the junction from the lesson above — every Synty prefab
reference in that asset is resolved against nothing and written back as `{fileID: 0}`. On
2026-09-16 that was 501 lines changed, the whole catalogue reduced to names with no art, and the
run **exited zero and said nothing**. `git add -A` would have committed it, and the next person to
open the main checkout would have had a colony of grey boxes with no failing test to explain it.

Three things make this worth a section rather than a footnote.

- **It looks like ordinary Unity churn.** The same run also re-serialises `OdysseySky.mat`,
  `HudPanelSettings.asset` and `ProjectSettings/ShaderGraphSettings.asset` with no content change
  at all. Three harmless files and one catastrophic one arrive in `git status` together, and the
  catastrophic one is not the one with the alarming name.
- **The tests do not catch it**, and cannot. The catalogue is licensed art, the fast tier never
  loads it, and the whole point of the clean-room rule is that the simulation runs without it.
  A green tier here means the code is fine, not that the commit is.
- **The scene itself is not damaged**, which makes the diff misleading. `Play.unity` keeps its
  catalogue reference by GUID and only renumbers its fileIDs, so reading the scene diff reassures
  you about the wrong file.

**So: after any editor command in a worktree, diff the assets it touched before staging anything,
and never `git add -A` on the strength of an exit code.** If the catalogue is in the list, either
revert it or make the junction first and rebuild. Reverting is right whenever the catalogue is not
what you changed — `git checkout -- Assets/Odyssey/Presentation/ModuleCatalogue.asset` — because a
catalogue rebuilt without the packs can never be more correct than the committed one.

## A generated asset and its generator had drifted apart, and only a rebuild said so

Worse than the packless rebuild above, because it survives having the packs. On 2026-09-16 the
committed `ModuleCatalogue.asset` held a `terrain.marsh` row with the bare-earth material, and
`PlayScene.cs` — the only thing that writes that asset — had stopped emitting it. It also emitted a
`tool.hammer` row the asset did not have. So the asset was simultaneously ahead of and behind its
own generator, and had been for as long as nobody rebuilt it.

Nothing could have caught this. The asset is licensed art, so no test loads it; the generator is
editor tooling, so no test runs it; and both sides were individually valid. The only symptom
available was a diff, and only if somebody rebuilt and then read it rather than staging it.

The damage it was holding: marsh terrain resolves `odyssey.module.terrain.marsh` through
`NaturalContent`, so the next rebuild would have dropped marsh to the untextured fallback — the
dark olive slab that reads as shadow, which is exactly the fault the water work had gone and fixed.
A rebuild for an unrelated reason would have quietly undone it, weeks later, with no failing test
and nothing in the commit to connect the two.

**The rule this suggests is narrow and worth keeping: a generated asset that is committed must be
rebuilt by whoever changes its generator, in the same commit.** And when a rebuild's diff shows a
row *disappearing*, that is never churn — a generator emits what it is told to emit, so a missing
row means the instruction went missing. Read the diff for absences, not just for changes.

## URP keeps post-processing per camera, and a camera built in script has it off

Two days of this project's screenshots were of an ungraded image and nobody could have known. URP
stores `renderPostProcessing` on the camera's `UniversalAdditionalCameraData`, and a camera created
with `AddComponent<Camera>()` gets it **false**. Every contact-sheet tool here builds its own camera,
so every photograph ever taken by one had no volume applied. That was harmless while the project had
no volume at all, and became actively misleading the moment there was a grade to look at: the first
golden-hour contact sheet showed the lighting change and none of the warmth, which reads exactly
like the grade not working.

The same default made a *measurement* lie, which is worse than a picture lying. `FrameTimeTests`
builds its own camera too, so the first run after the grade landed reported it as costing almost
nothing. That was a true statement about a frame the player never sees. A perfectly green test tier
said the effect was free.

Both are fixed at the source — `PlayScene.Shoot` and the frame-time harness now switch post on, and
the harness attaches the profile and uses the real sun angle, since shadow length is height over the
tangent of elevation and the old steep sun understated the shadow pass by most of its cost.

**The general rule: when a harness builds its own camera, lights or volumes, list what the real
scene has that the harness does not.** A harness is a claim that it resembles the game, and every
default it silently takes is a way for that claim to be false while every test passes.

## A volume profile written from code saves five nulls unless you add the components to the asset

A `VolumeComponent` is a `ScriptableObject` in its own right, and `VolumeProfile.Add<T>()` only
creates one in memory. Saved without `AssetDatabase.AddObjectToAsset`, the profile serialises its
`components` list as five entries of `{fileID: 0}` — 571 bytes of an asset that holds nothing. The
correct file is 4,853 bytes with six `MonoBehaviour` blocks in it, which is the cheapest way to tell
the two apart without opening Unity.

It is the same fault as a renderer feature appended to a `ScriptableRendererData` without being
added to its asset, recorded above, and it fails the same way: no error, no warning, the effect
simply never runs.

**What makes this one nastier is that it hides from its own verification.** The editor command that
writes the profile also builds the components in memory, so any screenshot taken in that same run
shows the grade working perfectly. The broken half only appears in a session that did not write the
file — a later run, a player build, or the owner pressing Play tomorrow. Two contact sheets were
taken off a profile that was empty on disk, and both looked right.

**So when code writes an asset, check the file, not the picture.** `grep -c "fileID: 0}"` on the
result costs nothing and answers it exactly.

## Writing RenderSettings every frame costs half a millisecond, even when nothing changed

The day/night cycle sets the sun, the ambient colours and the fog from the tick. Driven from
`Update` that is sixty writes a second, and at speed 1 a frame advances the clock by one tick —
four ten-thousandths of an hour, a change no colour channel can even hold. So almost every write
was setting a value to what it already was.

It was not free. The frame-time test put the meadow at **2.14 ms with the cycle against 1.66 ms
without**, and a guard that skips the whole apply unless the hour has moved by a fiftieth of that
step took it to **1.71 ms**. Roughly **0.43 ms a frame** for writes that changed nothing.
`RenderSettings.ambientSkyColor` and friends are not plain fields; they are engine state with work
behind them, and assigning the same value is not free.

Two things generalise:

- **A per-frame write of a value derived from game time is almost always redundant**, because game
  time moves far more slowly than frames do. Guard on the input having changed, not on the output
  looking different — comparing colours is more work than comparing one float.
- **It was only found because a test measures the real frame.** Nothing was wrong: no error, no
  visual fault, every test green, and the cycle looked perfect in every screenshot. The only
  symptom available was a number that had moved, which is the entire argument for having the number
  in the first place.

## A PlayMode test cannot press a mouse button, and the suite said it could

`MouseHarness` carries three documented failure modes, each measured, each fixed, each guarded by
an assertion so it can never come back in silence. It looked like a finished piece of work. It was
not: **no PlayMode test in this project has ever delivered a mouse press**, so every world gesture —
click-to-inspect, box-select, drag-to-designate — has been untestable since the rig was written.

**Why it stayed hidden.** The three fixed failures were all about state *arriving*, and the two
gestures that had callers, `Scroll` and `MoveTo`, read plain values. **Reading a value has no frame
gate; every edge property does.** `wasPressedThisFrame` is gated on
`InputDevice.wasUpdatedThisFrame`, and that asks whether the device was updated in a *player*
update. A PlayMode test runs inside the editor, where the update type is `Editor`. So the button
goes down, `isPressed` reads true, and the press edge the game reads never exists at all.

The measurement, which is the only reason any of this is known rather than argued:

```
press edges 0, release edges 0, deliveries with the button down 1,
deliveries the device counted as this frame 0
mode=ProcessEventsManually updateType=Editor
```

**Three method notes, in the order they cost time.**

- **An unexercised helper is not code, it is a plan.** `Click` had shipped with no assertion and no
  caller. The first test to call it failed, and four rounds of debugging went into the *product*
  before anyone asked whether the harness worked — during which the real finding, that the rig
  raised no gesture at all while the picker resolved the same point perfectly, read as an
  impossible result rather than as the obvious symptom of a press that never happened.
- **Assert what the helper claims, not what is convenient to check.** `Scroll` and `MoveTo` assert;
  `Click` did not, and it is the one that was broken. A gesture helper should verify the thing the
  game actually reads. `Click` now asserts the button went down, which is genuinely not enough —
  it passes today while the edge never fires — and the docstring says so, because an assertion that
  covers a third of a helper's claim is worth having only if nobody mistakes it for the whole.
- **`[Ignore]`, never `Assume`.** `AClickIsSeenAsAPressAndARelease` is ignored with the reason in
  the attribute, so it is visible in every run. An `Assume` would skip it in silence, which is
  precisely how six `MineJobTests` sat dead on main — the trap this file already documents, walked
  into again the same day by the person who wrote it down.

**Still open, with one untried lead.** Setting `ProcessEventsManually` applied and changed nothing,
because the mode is not what decides the update type. The next thing to try is asking for a player
update explicitly — `InputSystem.Update(InputUpdateType.Dynamic)` in `InputPump` rather than the
bare `InputSystem.Update()`, which resolves to `Editor` in this context. Un-ignore that test to find
out.

## Three UI Toolkit measurements that are off by one or two pixels, and why each is

Found while making the rebuilt HUD's layout model agree with the boxes UI Toolkit actually produced.
Each cost a full PlayMode run to diagnose, and none was guessable from reading the sheet.

- **Unity's runtime theme gives every `Label` a vertical margin of its own.** It costs nothing
  inside a parent of a fixed height, which is why it went unnoticed for a whole HUD: the stores
  rows, the clock line and the inspect header all size their own children. It shows up on a label
  that is the *last* thing in a content-sized panel — the depth rail's "R / F" hint was adding two
  pixels to the rail's height, and nothing about the sheet said so. A `.unity-label { margin: 0 }`
  reset at the top of the sheet is the fix; put it first, or it beats the rules that want a margin.
- **The layout is rounded to the *physical* pixel grid, so a scaled panel reports quantised
  sizes.** A panel scaled to a 1280-pixel window against a 1920-pixel canvas has one physical pixel
  to every 1.5 reference pixels, so an element declared `height: 38px` occupies 25.3 physical
  pixels, rounds to 26, and measures back as **39**. Every element in a column can gain one such
  step. A geometry test that compares realised boxes against a model therefore needs a tolerance
  that scales with `canvasWidth / screenWidth`, not a constant — and a constant that happens to
  work at 1:1 will fail on the first non-integer scale somebody tests at.
- **A label left to size itself claims about twice the point size, and stacked labels then
  overlap.** UI Toolkit's line box for 13 px type measures ~26 px, not the ~17 the point size
  suggests — most of it leading. Inside a row of a fixed height that is invisible; stacked in a
  column it pushes the next row down, and the first screenshot of the rebuilt HUD showed a
  colonist's job line printed through the tab strip under it. The fix is an explicit `height` plus
  `-unity-text-align: middle-*`, which centres the ink in the box you chose and lets the leading
  fall outside it. Do it for every label that shares a column with another; a label alone in a row
  whose height is set by something else is fine as it is. **Measure it rather than assuming a
  factor** — 1.35 would have been the reasonable guess and it is wrong by half a line.
- **A `display: none` element has no width to read.** Reflowing a bar by hiding what does not fit
  means measuring everything first, and measuring means the items have to be laid out. Use
  `visibility: hidden`, which lays out and does not draw; otherwise the only way to find out that
  the bar overflows is to draw it overflowing for a frame.

**And the diagnostic that made all three cheap:** the failure message prints every child's classes,
height, top and margins. The first run said only "the panel is 2 px taller than the model", which is
not a sentence anybody can act on, and a second run of the PlayMode gate costs a minute and a half.
A containment assertion between two boxes should always be able to name what filled the difference.

## A test anchored in a design fact fails when the design changes, and that is correct

`PointerOriginTests` guarded a real and silent bug — `PointOverUi` tested every click against the
opposite side of the screen, because a mouse position is bottom-left origin and a panel is top-left
origin. It could not be written against `PointOverUi`'s own answers, since a mirrored guard is
self-consistent with a mirrored search; so it was anchored in something the transform could not
influence: **this HUD is bottom-heavy**, a full-width bar two rows tall against a clock in one
corner, so it must claim more of the lower half of the screen than the upper.

The HUD rebuild made the bar one row, shrank the resting inspect pane to a single line and put a
four-hundred-pixel depth rail down the top right. The count reversed, the test failed, and it was
*right to*: its own remarks had said it would break "if the HUD stops being bottom-heavy, which
would be a design change worth failing a test over".

The lesson is what replaced it rather than that it broke. **The panel's own hit-test is ground
truth and takes panel coordinates, so it never touches the screen axis at all.** Comparing
`panel.Pick(p)` against `PointOverUi(ToScreen(p))` over a grid asks the question directly, disagrees
at every point off the midline when the flip is missing, and depends on no property of the layout
whatever. It is not circular as long as the test writes its own `ToScreen` with the flip stated
explicitly: that makes it the inverse of a *correct* `ToPanel` rather than of whatever `ToPanel`
happens to do.

Worth generalising: when a guard has to be anchored in something outside the thing it is testing,
prefer another *mechanism* that answers the same question independently over a *fact about the
current design*. The mechanism survives the redesign.

## A reserved-key list maintained by hand is wrong before anyone reads it

The rebuilt command bar gave each of its eleven items a hotkey and shipped a test asserting that
none of them collided with "the keys the game already uses". The list in that test had nine entries
— the designate tools, the slice keys, the speed digits — and was missing **WASD**, which pans the
camera, **B**, which cycled the below-slice mode, and **Q** and **E**, which turn it. Five of the
eleven hotkeys clashed. The test passed.

Nothing about it looked wrong. It was a real assertion with a real list and a clear failure message,
and it had been written specifically to prevent this. What it could not do is know about a binding
added in a file it had never heard of.

**So the reserved set is read out of the source.** `HotkeyClashTests` greps the Presentation
assembly for every `keys.somethingKey`, and allows a command's key only in the one file that reads
it on that command's behalf — `bKey` in `HudShell.Bar.cs` for Build, `escapeKey` in
`SettingsPresenter.cs` for Menu. Anything else reading one of them is the clash. It also asserts
that **every hotkey cap is one the test knows how to map**, because a cap it cannot map is a cap it
silently skips, which is the same failure wearing a different hat.

Two things generalise:

- **When a guard needs to know about "everything else in the codebase", derive the set, do not type
  it.** A grep-based test is an unusual shape and is sometimes the only shape that can answer the
  question at all without a running game and a person pressing keys.
- **A test that skips what it cannot handle passes for the wrong reason.** Make the unhandled case
  an explicit failure. Both of this file's earlier entries about silent skipping — the `Assume` that
  hid six dead mining tests, the loose tolerance that made a test prove nothing — are the same
  shape.

## An optional parameter is how a composition root forgets

`ColonyComposition.AddColony` gained the build pipeline's construction grid as
`ConstructionGrid? construction = null`. Every one of the twelve existing call sites went on
compiling, and eleven of them — including `OdysseyBootstrap`, the one the game actually runs —
silently built a colony with **no intent handler for `PlaceBuilding`, no sites, and both
construction work givers answering no for ever**.

Every test passed, because every test builds its world through `ColonyWorld`, the twelfth call
site, which did pass one. So the feature was green in the fast tier, green in the Long tier, and
did nothing at all in the only build a player can touch. It was found by the owner dragging a wall
across the meadow, watching the preview draw and watching nothing be built.

`ColonyComposition`'s own class comment had predicted it: *"a designation grid that one of them
forgot to attach would be a player command that silently did nothing in that build. So the list
lives here, once."* The comment was right and the signature undid it.

**The rule: a composition root's parameters are not optional.** If a new piece of colony state has
a sensible default of "absent", every existing caller takes that default and the omission is
invisible. Either make the parameter required — the compiler then names every site that has to
think about it — or, better, build the thing inside the composition so there is nothing to pass.
`AddColony` now takes the edifice list and hands the grid back through an `out`, so forgetting is
not expressible.

**And the standing guard is a test over the enum**, not over one command:
`ConstructionTests.EveryIntentTheInterfaceCanSendIsAnsweredByTheColony` walks every `IntentKind`
and asserts the colony answers it. The next command will arrive the same way — an enum value
somebody adds and a handler somebody means to attach.

**The price, met on 2026-09-16: a guard keyed by filename fails when a file is split.** Cutting
`HudShell.cs` into partial-class files moved `keys.bKey` into `HudShell.Bar.cs`, and the ownership
map still named `HudShell.cs`, so the tidy-up broke a test that had nothing to do with hotkeys. That
is the design working, not failing — the map says *which* file may read a key, so a key that moves
house must say so — and it is worth knowing before you split anything in the Presentation assembly.
The fast tier does **not** catch it: `HotkeyClashTests` needs the real directory tree, so it is the
Unity EditMode run that fails. Split a file, then run `scripts/unity.sh test editmode`, not just
`scripts/test-fast.sh`.

## A screenshot is data: sample it before you read the rendering code

The report on 2026-09-17 was that low ground under the trees had *"no ground texture or grass"*, with
a screenshot. Reading the rendering path produced three plausible and entirely wrong culprits in
turn — an untextured fallback material (the library already logs that case and tints it), a
desaturated surround, a sand cover patch — and each needed real work to rule out. None of them was
it: **the ground was not being drawn at all**, and what the picture showed was the sky.

**What settled it was measuring the image, which took about five minutes.** `tools/icons/icons.py`
has a standard-library PNG codec, so a throwaway script can crop, magnify and sample any screenshot
the owner sends without Pillow and without Unity:

```python
sys.path.insert(0, "tools/icons"); import icons
img = icons.read_png(path); r, g, b, a = img.get(x, y)
```

Three measurements, in the order they mattered:

- **The plane was the same colour near and far** — (171,129,100) at both ends of the depth range.
  The scene runs exp2 fog dense enough to be a third opaque at the rim, so *any* lit surface grades
  with distance. **A surface that does not fog is not a surface.**
- **It had no outline.** The adjacent meadow was ringed by the ink pass; the plane was not.
- **It had no tufts and no shadows**, while the meadow six metres behind it had both.

Magnifying the boundary (a 160 px crop at 5x) then showed a hard diagonal edge with the meadow's own
outline running along the meadow's side of it — a silhouette, not a seam. That is a hole, and a hole
is a slice question, not a material question.

**The generalisation.** A rendering complaint names a symptom in the vocabulary of art — texture,
colour, grass — and reading the code invites you to look for a fault in the thing named. The image
itself is evidence and it is cheap to interrogate: constant colour across depth, a missing outline, a
missing shadow and a missing decoration are each a sharp signal, and together they said "nothing is
there" before a single file was opened. This is the same rule as *Measure before diagnosing*
elsewhere in this file, applied to the one artefact the owner actually hands you.

**The second half, which is the real cost.** Once the mechanism was in hand it still had to be
proved on the board that is played, not on a fixture — the terracing is what makes the bug, and no
hand-built grid was going to reproduce five surface layers and their woodland. A throwaway NUnit
case in the Sim fast tier printed the column histogram in nine seconds (`Assert.Fail` with a
`StringBuilder`, since the runner swallows `Console.WriteLine`), which is how the 6,140-of-14,400
figure exists at all. Delete the probe before committing.

## A performance threshold calibrated on your own machine is a gate that fails on everyone else's

`DesignationProgressTests.PublishingCostsWhatTheOrdersCostRatherThanWhatTheLayerCosts` asserted a
tick cost under **0.012 ms**, chosen as "five times the measured figure" on the author's machine.
On 2026-09-17 it failed CI **twice in an hour, on two unrelated pull requests** — one that touched
no simulation code at all and one that was a stylesheet and a HUD control. Measured on the
GitHub-hosted Linux runner: **0.0164 ms** and **0.0125 ms**. The same commit measured **0.0029 ms**
locally. Nothing was slow; the runner is.

**The rule that would have avoided it: pick the number from the bug, not from the noise.** The
defect this test guards costs twenty-six times the fixed figure (0.057 against 0.0022, both
recorded in the test). Anything comfortably under that still catches it. The threshold is now
0.030 — about twice the slowest honest reading, and about a tenth of what the bug would cost on
the same machine — so there is headroom on both sides and the sentence saying so is in the test.

**Two cheaper diagnostics before you believe a timing failure.** Re-run the job: a real regression
repeats and noise usually does not. And run the test locally and read the printed figure — this
one prints `[progress] … ms/tick` to `TestContext`, which is what turned "is my change slow?" into
"the runner is slow" in one command. **Do not re-bake a timing number because CI is red** without
one of those two; the first instinct of loosening it until it passes is how a gate quietly stops
guarding anything.

**A timing assertion on a shared runner is worth having, but only in this shape:** a wide band
justified by the size of the defect, the measured figures for both states written down beside it,
and a failure message that tells the next person which of the two they are probably looking at.

## `GC.GetTotalMemory` sees nothing under Mono, and a byte budget passes loudest where it is blind

**Symptom.** An allocation test measured cleanly in the fast tier — 4.00 bytes per extra path cell,
exactly one `int`, reproducible run to run — and failed in the Unity tier with
`0.0 bytes per request` for *both* arms of the comparison. Same code, same assertions, opposite
verdicts.

**Cause.** `GC.GetTotalMemory(false)` does not mean the same thing on the two runtimes we ship
against. On CoreCLR it moves with allocation. On Mono it reports the heap the collector owns, and
the collector hands out nursery space in blocks and reuses it for short-lived objects — so a couple
of thousand arrays of a couple of hundred bytes each, allocated and dropped, can move the number
**not at all**. Roughly 400 KB of allocation reported as zero.

**The half that matters more than the failure.** The same run had a *second* test asserting that an
idle tick allocates under a 16-byte budget. Under Mono it read zero and **passed** — a guard against
a 64-byte-per-tick delegate, reporting success on the one runtime where it could not have seen one.
A test that fails on a blind instrument is an annoyance. A test that passes on a blind instrument is
the state-hash defect again: an oracle comparing numbers that cannot see the thing they are about.

**What to do.** Calibrate the instrument inside the test before believing it. Allocate a known
quantity, and if the runtime cannot report it to within a factor of two, `Assert.Ignore` with the
reason rather than failing *or* trusting the reading. Two details make the probe honest:

- **Allocate the same shape and total** as the thing being measured. A probe that allocates a
  megabyte proves nothing about whether a few hundred bytes are visible.
- **Discard, do not retain.** Retained allocations force heap growth that a real per-tick
  allocation never forces, so a retaining probe is easier to satisfy than the measurement it stands
  in for — which reproduces exactly the false pass you were trying to prevent.

**Where this bites next.** Any figure in bytes: allocation budgets, save sizes measured by heap
delta, pooling proofs. Timings are fine; byte counts are not. `PathAllocationTests` carries the
worked version, and the figures in `docs/adr/0005-simulation-architecture.md` are the CoreCLR ones,
labelled as such.

## The per-tick allocation budget flakes, and the first reaction is the wrong one

**Seen 2026-09-17**, adding the edifice list to the save and the state hash.
`PathAllocationTests.ATickThatDoesNothingAllocatesNextToNothing` failed with the colony at **24.6
bytes a tick against a 16-byte budget** — a 50% overshoot, which looks nothing like noise and
looks exactly like a change that started allocating. It was noise. The same tier then passed
**19/19 three times running**, and the filtered test passed three times before that.

**Why the reaction matters.** The obvious next move is to go hunting in the diff for a closure or
a boxed struct, because that is what the failure message tells you to do and the message is right
about the usual case. On a change that touches only `ISaveable` and `IStateHashable` — neither of
which runs in a tick — that hunt is guaranteed to find nothing, and the temptation at the end of it
is to widen the budget.

**What to do instead, in order.** Re-run the tier two or three times *before* reading the diff: it
costs ten seconds against a flake and ten seconds against a real regression. Then ask whether
anything in the change is reachable from `SimWorld.Tick` at all — a component registered with
`AddHashable` or added to `SaveComponents` is not. Only then go looking.

**Do not widen the budget to make it stable.** The margin is the test: the measured figures are
1.6 and 3.3 bytes, so 16 is already four times under the 64-byte delegate the test exists to
forbid, and a budget raised to swallow a flake is a budget that swallows the next real one. If it
starts flaking often, the window (5,000 ticks) is what to grow, not the threshold — a longer window
averages the GC timing out rather than hiding what it is measuring.

## Rebuilding the module catalogue erases the colonists' faces

`Odyssey/Presentation/Rebuild module catalogue` writes every row of
`ModuleCatalogue.asset` from `PlayScene`'s registration list, and that list says nothing about
appearance. The 61 character rows carry something no code in `PlayScene` can reproduce: the atlas
swatch rectangles each colonist's skin, hair and cloth vertices are mapped to, measured once by
`CharacterSwatches` and committed. A rebuild clears them — and the asset still looks healthy,
because every row is still there and every `totalVerts` is still a number. It is `0`.

Adding two slab rows produced **665 insertions and 2,162 deletions**, and the deletions were 311
swatch rectangles. The entry count told the reassuring half of the story: 136 before, 138 after,
nothing lost. `git diff --stat` told the true half.

**The rule: after a catalogue rebuild, check the diff is only what you added.** If it is not, keep
the committed asset and splice the new rows into it — split the file on `  - moduleId: `, lift the
blocks you want out of the rebuilt copy and insert them into the original. The rebuilt file is still
the right source for a new row's `prefab` GUID and `fileID`, which is the part that cannot be
written by hand.

Two ways this could stop being a trap, neither taken here: make the rebuild preserve `appearance`
on rows it is not authoring, or re-run `CharacterSwatches` after every rebuild. The first is right;
it wants the owner, because it changes what "rebuild" means.
## A recommendation in a research file is enforced by nothing, and one sat unread for months

`a-08-plants-growing-food.md` ended with an instruction as clear as any in this repository:
*"when OQ-14 lands skills, the felling driver multiplies work by the plant-work-speed curve"*.
`OQ-14` landed. The multiplication did not. Nobody noticed until the owner asked, months later,
why a better woodcutter is not faster — and the answer was that every work tick in the game adds
exactly `1`, in four separate drivers, whoever is swinging.

**Nothing was broken and nothing could have caught it.** The fast tier was green, the Unity gate
was green, the ten-day soak ran clean on three seeds, and every one of them would have stayed green
for ever, because there was no defect — there was an absence, and an absence that nothing had ever
written down as a requirement. `15-skills.md` §1 even *stated* it as a fact — "Nothing reads a
skill level yet. Not work speed, not yield, not quality" — and stated it in a document about icons,
where it read as background rather than as a debt.

**Three things follow, and they cost nothing to apply.**

- **A research file's Recommendation section is a claim about the future, and the future has no
  test.** When a research row recommends work, the recommendation belongs in `overnight-queue.md`
  or `vertical-slice.md` **in the same session**, as a row with done criteria, even if the row is
  opened `blocked`. Prose in `docs/research/` is a finding; a row is an obligation.
- **When a queue row closes, re-read what asked for it.** `OQ-14` was "skills: experience, passion,
  levels, decay" and it did all four correctly. The row was written from the design, and the
  *other* file that wanted something from skills was never consulted at closing time. A one-line
  grep for the unit's own id across `docs/research/` would have found it.
- **"Nothing reads X yet" is the sentence to search for.** It is how this repository honestly
  records a half-built thing, which means it is also where the half-built things are listed. It is
  worth grepping before planning any milestone: `git grep -n "reads .* yet"` and its neighbours
  cost one command and name real gaps in the owner's own words.

**The related failure, from the same afternoon:** the design written to close this gap was
complete, argued and wrong in four specific places, because it had been written from the
simulation's side alone. Scaling an internal accumulator by 1,000 silently breaks a `ushort` in the
published contract, two "about 12s of work" readouts in the interface, a progress fraction whose
denominator lives in a different class, and a `movePercent` **ratio** whose two halves are assigned
in different files. None of them are in `Sim`. **A design that changes a unit has to be walked to
every place that unit is read**, including across the sim→UI contract, and the walk takes ten
minutes with `git grep` — considerably less than the session that would otherwise discover the
`ushort` by watching a progress bar wrap.
## A contact sheet proves nothing until you know the subject is in frame (2026-09-17)

Adding faces to water produced three contact sheets identical to the "before" pair, and a pixel
difference against them was black. The obvious reading - the feature is not drawing - was wrong. The
geometry was being emitted (44 instances, logged), placed correctly (positions, drop and normals all
logged and exactly right), and drawn. **The camera was pointed at a stretch of stream with no
cascade in it**, because the tool picked its subject by scoring how much water lay nearby, which
selects the middle of the widest pool - the one place a step is guaranteed *not* to be.

Two rules out of it.

**When a picture shows no change, prove the subject is in frame before concluding anything about the
feature.** The cheapest proof is to aim the camera at a coordinate you have logged from the thing
itself, rather than at one you computed independently. That is what settled it here in one run,
after three had been spent on the hypothesis that the geometry was wrong.

**A shot-picking heuristic is a measurement and can be wrong like any other.** "Most water nearby"
sounds like "the most interesting bit of river" and is in fact "the flattest bit of river". Score
for the feature you are photographing, not for its surroundings.

## A shader can delete a feature and leave every test green (2026-09-17)

`OdysseyWater.shader` carried `clip(input.normalWS.y - 0.5)` - discard every fragment not facing up
- for a good reason from a year of the project nobody involved remembered. Any vertical water face
added afterwards would have been discarded in the fragment shader, with the mesh built, the matrices
correct, the instances submitted and **every assertion passing**.

Before adding geometry in a new orientation, read the shader it will be drawn with. Grep the
fragment for `clip`, `discard`, and any test against a normal, a position or a facing. The class of
fault is "the geometry is perfect and the pixels are thrown away", and no test that inspects meshes
or matrices can see it - only a picture can.

## A coplanar transparent quad loses most of itself, and reads as a small feature rather than a broken one (2026-09-17)

The water faces were placed on the cell face, which is exactly the plane the terrace riser below
them occupies. Two coplanar surfaces under `ZTest LEqual` are two candidates for the same pixel with
nothing to separate them, and the depth buffer picks whichever rounds higher - **per triangle**, so a
2.5 x 3 m sheet came out as a triangular sliver of its own top corner, split along the quad's
diagonal.

**The reason this is worth a lesson is that it did not look like a bug.** A missing feature is
obvious; a feature reduced to a third of itself just looks like a small, pale, slightly odd
decoration, and it survived several contact sheets and a round of tuning aimed at the wrong thing
(the foam term, which was in fact working and simply had no visible sheet to appear on). Two rounds
were spent adjusting shader constants for a geometry fault.

**How it was settled in one shot:** tint the suspect geometry by its own UV and look. Every visible
fragment came back at `v = 1`, the top edge of the mesh, which says "you are seeing the top sliver
of this quad and nothing else" and cannot be argued with. A debug tint costs one line and one run,
and it is the fastest way to find out which *part* of a mesh is reaching the screen.

The fix is a stand-off of a centimetre or two, the same trick and the same reason as
`ChunkMesher.CoreRecess`. Note the second-order fault it opens: standing a sheet off its wall leaves
a slot between the two, and at the top edge that slot is a line of sight onto whatever is behind -
a hairline of lit ground along the brow of every waterfall. Close it by extending the sheet *into*
the surface it hangs from, and lengthen it by the same amount or it stops short at the bottom, which
is the half-right version of the fix.

## A failed `Assume` is invisible, and `dotnet test` still prints "Passed!"

Found 2026-09-17 reviewing the beds merge. Four tests covering the whole of bed ownership were
not running: a helper raised a bed without first ordering one, so no bed stood, and each test
ended on `Assume.That(assign(...), Is.EqualTo(IntentRejection.None))`. NUnit reports a failed
assumption as **Inconclusive**, VSTest records it as `NotExecuted`, and the console summary
counts it as neither passed nor skipped — the run says `Passed! - Failed: 0, Skipped: 0` and
the test simply is not in the totals. The tier had been green over an untested feature since the
day it was written.

Two habits that catch it:

- **Count the tests, not the word "Passed".** A total that does not grow when you add a test is
  the symptom. `--logger "trx;LogFileName=x.trx"` then grep the TRX for `outcome="NotExecuted"`
  lists every one with its reason; a run with no filter should show only `Long`/`Benchmark` rows.
- **`Assume` is for the board, not for the code under test.** "This seed happened to put a
  buildable cell near the start" is an assumption. "The intent I just submitted was accepted" is
  an assertion — if it can fail, the test must fail with it.

The same run found `Assert.Ignore` hiding a second one: a test searched for solid ground at
`start.Y`, which is the layer a colonist *stands in*, found none anywhere and ignored itself on
every run since it was written. An `Ignore` with a plausible reason reads as an honest skip; check
that the reason can ever be false.

## A hand-written table parallel to a handle set will not conflict when the handles renumber

Same merge. `BuildingHandle.Bed` moved from 2 to 5 to make room for three buildings that reached
main first. `BuildShapes` — a two-array table in the Hud assembly, parallel to `BuildingHandle` —
was not touched by main, so git merged it in silence with three entries, and the bed became a
one-cell thing that could not be turned. Three `DesignateDirector` tests failed and none of them
named the cause. The class's own remarks claimed a test held the two tables together; none did.

**Every parallel table needs a length assertion against the handle set's `Count`**, in whichever
assembly can see both. `RegistryTests` already did it for `BuildLabels`, `JobLabels` and
`ItemLabels`; `BuildShapes` is now beside them. A handle added past the end of such an array does
not throw — it reads as the default, which is the silently wrong answer.

## Hover is not an affordance

Two rounds were paid for this on the bed's owner row (2026-09-17, then again 2026-09-18). The row
submitted an intent and had done since it was written; what it looked like at rest was a pointer
cursor, then a hover brighten, then a border and a chevron. The owner could not find it either
time, and said so in the same words both times: *"I couldn't work out how to assign a colonist to
a bed."*

A player does not hover a row to find out whether it is a control; they scan the panel and see
facts. **A control has to look like one while the pointer is somewhere else entirely** — a filled
box, an icon, and weight. The row also has to *name the action*: it read "—" for an unowned bed,
which says there is nothing here, and a fast-tier test had pinned that em dash in place.

## A simulation that works and cannot be seen is a simulation that is reported broken

Same round, and the more expensive half. The owner reported that colonists would not use spare
beds and stood outside instead. A probe on exactly that case — nobody owning anything, one spare
bed, one tired colonist — walked the colonist into the bed and slept there. `TrySleep` was correct
and always had been.

Nothing in the whole of presentation knew a pawn could be asleep, so a colonist in a bed was drawn
**standing bolt upright in it**. The bug report was accurate about what was on screen and wrong
about what it meant, and it would have been perfectly reasonable to go and "fix" the sleep chooser.

**Measure the behaviour before believing a behavioural bug report**, especially about a system with
no visual state of its own. And when a feature ships whose whole evidence is a pose, a sprite or a
readout — check that the thing exists, because its absence looks exactly like the feature not
working.

## Integer division silently collapses a tier table

Found in the same measurement. Rest gain was `base * effectiveness / 100` with a base of 6, so the
five quality tiers and the ground produced 4.8, 5.1, 6.0, 6.72, 7.5, 8.4 — truncated to 4, 5, 6,
**6**, 7, 8. A Decent bed was worth exactly a Normal one. Nothing failed; every number was a good
number.

Where content is a percentage of a small integer, **assert the ladder** — each step strictly better
than the one below — rather than the values. The fix is to spend the fractional part rather than
discard it; a Bresenham step over an index the world already stores keeps it out of the save and
the hash, where a carried remainder field would not.

## A multiply-and-shift hash has no usable low bits, and `% small` reads only those

The sleep posture is chosen with `% 4`, from a hash of the pawn id. The first cut was the ordinary
Knuth multiply plus one shift — `h = id * 2654435761; h ^= h >> 15` — which is a perfectly good
hash everywhere except in its bottom bits, and the bottom two bits were the only ones `% 4` ever
looked at. **Every colonist in the colony came out in the same posture**, which is exactly the
one-shape outcome the posture table exists to prevent.

Where a hash feeds a small modulus, use a full avalanche (murmur3's `fmix32`: shift-xor, multiply,
shift-xor, multiply, shift-xor) so the low bits carry the whole input. And test the *distribution
over consecutive ids*, because consecutive is what a colony actually has — a test over scattered
ids would have passed.

## A single-assembly compile check cannot see an assembly boundary

With the editor open on a worktree (and so the project locked against a batch run), the Presentation
assembly can be compile-checked by pointing `dotnet build` at the sources with `LangVersion 9` —
Unity's own — and referencing Unity's DLLs plus `Library/ScriptAssemblies`. That catches the thing
the fast tier cannot: a construct that compiles under `latest` and not under 9.

**It does not catch anything about assembly boundaries**, because it compiles every Odyssey source
into one assembly. It reported clean on a PlayMode test that then failed in Unity twice over: a
field that is `private` to `HudShell` looked reachable, and a type visible in the merged assembly
was not visible across the real asmdef reference set. Treat it as a language-version gate, not as a
substitute for `scripts/unity.sh`.

The other half of the lesson: **a test written for a private field is usually asking the wrong
question.** The fix was not to widen `HudShell._inspect` but to wait for the *row* to appear in the
panel — which is what a player actually has, and a stronger assertion than the flag behind it.

## A flag cleared every refresh and set only on the change path lives for one frame

The bed's owner row submitted an intent and never fired, three reports across two sessions. The
cause was two lines a long way apart in `InspectModel`: `Refresh` cleared `_bedUnderPane` **every
time**, and it was set inside `SetCellRows` — which returns early whenever nothing about the cell
has changed. So the flag was true on the refresh that built the rows and false on every refresh
after it, while the row went on reading "Assign…" over a control the shell had already disarmed.

**A pane refreshes many times a second and a player clicks a good deal later than that**, so the
only frame the old code got right was the one nobody could click in. Two consequences worth
carrying:

- **Derive an affordance from the state it describes, not from the work that displayed it.** The
  flag is a fact about the cell being held; it belongs before the early return, beside the data it
  is read from.
- **Test the second refresh.** A test that refreshes once and asserts passes over this bug
  completely. `ABedStaysAssignableAfterTheRowsHaveSettled` refreshes five times, and the control
  run — old code restored — fails on exactly that one.

## NaN written to a USS position is not ignored; it moves the element to the corner

Same feature, second fault, found in the same run. A popover shown on the current frame has not
been laid out, so `resolvedStyle.height` and `worldBound.height` both answer NaN. Feeding that
through placement arithmetic gives `style.bottom = NaN`, and the element lands at (0, 0) — measured
as a picker at the top-left of the screen against the row at y = 1095 that raised it.

`schedule.Execute` is not the fix: it can run before layout resolves, which is what it did here.
**`GeometryChangedEvent` is the event that fires when the size exists**, so placement belongs
there, and the placement call should decline to write a position it cannot compute rather than
writing a NaN.

## Half a rule about state is a rule that does not hold

The owner reported meals standing up through a bed. The first fix refused to **place** a bed on a
cell holding items, which is the direction the report described and is half of the rule: nothing
stopped a hauler carrying a pile **onto** the bed afterwards, and that is the likelier route,
because an empty walkable cell is a perfectly good haul destination whatever is standing in it.

Where a report is about a state that must never exist, fix **every way in**, not the one the
report happened to come through. The second half here was `ColonyItems.CellHasSpace`, the single
gate every spawn, drop and haul destination already passes through — derived from the edifice list
on load, like support and ladder connectors, so it costs no save format and no hash bit.

It also fixed a failure that looked unrelated: bed cells had been haul destinations, so haulers
reserved them, and `TrySleep` checks the reservation before it checks whose bed it is — which
locked a colonist out of their own bed and sent them to sleep on the floor.

## Guard the start and the end of a thing's life and you have missed the middle

A bed must not contain a log. The first fix refused the **order** on a cell holding items; the
second held the cells once the bed **stood**. Both were right and together they still let the
reported picture through, because a building has a third state: ordered, not yet raised, and taking
a colonist a while to get to. A hauler put a log down in exactly that window.

Where a thing has a life cycle, the invariant belongs at the **transition every state passes
through** — here `ConstructionGrid.Set`, which is the one place a site is written, so ordering,
replacing, cancelling and raising all run through it. Two guards at the ends read as thorough and
are not.

## A generated asset can be regenerated outside Unity, but do not keep the replica

The editor holds the project lock whenever the owner is doing visual work, and the sections above
cover how to keep *compiling* and even *running* game code through it. They do not cover the case
where the thing you need is an **asset the editor generates** — `Assets/Editor/Odyssey/AudioSetup.cs`
synthesises the audio beds into `.wav` files, so no amount of compile-checking produces one.

It can be done: on 2026-09-17 the bed synthesis was reimplemented in Python and reproduced the
committed `water.wav` **byte for byte**, which is the only acceptable standard of proof here — an
asset that is merely "close" is a silent change to something nobody will listen to again. Two
things made it exact, and both are the whole difficulty:

- **Everything stays `float32`, in the C#'s own evaluation order.** The committed asset is the
  generator's output and the generator is float32 end to end, so a replica that computes in double
  and rounds at the end diverges in the low bits. Even `math.pi` has to be narrowed to float32
  before use.
- **The random source has to be the same generator, not merely seeded the same.** .NET's legacy
  `Random` is a specific subtractive lagged-Fibonacci with its own constants (`MBIG`, `MSEED`);
  seeding a Mersenne Twister identically gets you nothing.

**The replica was then deleted rather than committed, deliberately.** The generator is checked in as
C# and the assets it produced are checked in beside it; a second implementation in another language,
with nothing asserting the two agree, is exactly the two-sources-of-truth trap this project keeps
paying for elsewhere — and it would drift the first time the C# was tuned, silently, because its
only test is a file nobody diffs. The technique is worth a page; the code is not worth a file.
Reach for this only when the editor is genuinely blocked and the alternative is waiting.

## One NUL byte in a source file makes git hide every diff of it

`HudShell.Orders.cs` carried a sentinel written as a **literal NUL byte** inside a string —
`string _ordersPaintedFor = "<NUL>";` rather than `"\0"`. C# compiles it, the value is identical,
every test passes, and nothing in the editor looks wrong. But git classes any file containing a NUL
as binary, so the file's entire history is `Bin 7775 -> 8672 bytes` with no diff at all.

It surfaced on 2026-09-18 when a change to that file went into a pull request and the commit stat
showed `Bin` beside five ordinary text files. A reviewer would have had nothing to read, and the
review would have passed the one file that most needed looking at.

- **The fix is the escape.** `"\0"` is the same string to the compiler and plain text to git. The
  PlayMode tier gave the same 78 / 73 / 0 before and after, which is the check worth doing: the two
  spellings must not differ.
- **The old blob is still binary**, so the diff of the commit that *fixes* it is also unreadable —
  one side of that diff still contains the NUL. Every commit after it is normal.
- **Look for it whenever a `.cs`, `.uss` or `.json` shows as `Bin` in `git show --stat`.** A source
  file has no business being binary, and the cause is almost always a control character somebody
  typed as a byte where an escape was meant.

`python -c "print(open(PATH,'rb').read().count(b'\x00'))"` answers it in one line.

**And build the replacement bytes explicitly when fixing one.** The first attempt passed `"\0"`
through a shell heredoc into a Python one-liner and the backslash was eaten somewhere on the way, so
the "fix" wrote the NUL straight back and the file still had one. Concatenating `bytes([92])` and
`b"0"` is ugly and cannot be misread by anything in between.
