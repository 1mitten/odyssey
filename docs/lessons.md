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
