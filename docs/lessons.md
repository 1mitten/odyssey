# Lessons

Operational lessons that each cost real time once. They are here so they do not cost it twice. Findings that shape *design* live in the design documents and ADRs; this file is for the things that bite you while working.

Add to it whenever something takes more than about ten minutes to diagnose.

---

## Running Unity from a script

**A batch test run can finish its work and then never exit.** On 2026-09-15 a `-runTests` run wrote its results file and kept running for twenty-five minutes, holding `Temp/UnityLockfile`. The next batch command died instantly with exit code 1 and a log containing nothing but the banner, which is close to undiagnosable if you do not know to look for the lock. `-quit` is not the fix, because it can cut the run short before results are written.

What to do: `scripts/unity.sh` now treats the **results file, not the process exit code, as the verdict**, gives a lingering process a grace period and then terminates it, and has a hard timeout so CI fails instead of hanging. Tune with `UNITY_TEST_TIMEOUT` and `UNITY_TEST_GRACE`. The suspected cause is an editor package holding a background connection open; the Unity MCP plugin is the obvious candidate and is worth eliminating if this recurs.

**A batch command cannot share a project with an open editor.** Same symptom, different cause. The wrapper now distinguishes the two: a live Unity process means "close the editor", no live process means the lock is stale and it is removed automatically.

**Adding an assembly definition silently removes implicit package references.** Scripts under `Assets/Editor/` compile into `Assembly-CSharp-Editor`, which auto-references most packages. The moment an `.asmdef` covers them, every reference must be explicit. Adding `Odyssey.Editor.asmdef` broke `SyntyImport` because it uses URP types. Symptom: `CS0234: The type or namespace name X does not exist in the namespace Y`. Fix: add the package assemblies (`Unity.RenderPipelines.Core.Editor` and friends) to the asmdef `references`.

**`AssetDatabase.ImportPackage(path, interactive: false)` only *queues* the import under `-executeMethod`.** The editor can exit having imported nothing, and the run reports success. `SyntyImport.cs` calls the editor's synchronous internal import instead. If that ever disappears, fall back to one `-importPackage` invocation per package.

**URP 17.3.0's `Converters.RunInBatchMode` is broken.** It throws `MissingMethodException` on `Base2DMaterialUpgrader` while enumerating the container, before converting anything, with or without a converter filter. Use the public `MaterialUpgrader` API directly; it is batchmode-aware and skips its confirmation dialog automatically.

**Burst compiles asynchronously by default and will pollute a measured window.** In the D1 benchmark it inflated the grid phase from 0.283 ms mean to 0.498 ms with a 3.9 ms maximum. Set `EnableBurstCompileSynchronously` before any run whose numbers you intend to trust, or prewarm with a discarded run.

## Testing

**The tests are not the slow part.** The suite executes in about 40 ms. A Unity EditMode cycle takes minutes, and essentially all of it is Unity booting, refreshing the asset database (~7 s) and reloading the script domain (~3 s compile). **Filtering which tests run therefore saves nothing.** The only thing that helps is not starting Unity.

**Two tiers.** `scripts/test-fast.sh` runs the same Sim test sources through mirror projects in `tools/dotnet/` with no editor, in about 1.7 seconds warm. `scripts/unity.sh test editmode` is the authority, because only Unity proves the assembly-definition boundaries hold and only Unity can run editor or PlayMode tests. Work in the fast tier, gate on the slow one. See `docs/setup/local-dev.md` §10.

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
