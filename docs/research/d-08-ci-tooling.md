# Lane D8 — Headless CI: licence activation, runners, workflow

*Researched 2026-09-15. Unity 6000.3.24f1, URP 17.3.0, Unity **Personal** licence, private repository `1mitten/odyssey`.*

## Question

What is the practical continuous-integration setup for Odyssey in 2026 — running EditMode/PlayMode tests and the headless one-day simulation automatically — and specifically, how does licence activation work on a non-interactive machine under a Unity Personal licence?

## Findings

### 1. Licence activation in CI — the part that breaks

**The single most important fact, straight from the 6000.3 manual:**

> "For Unity Personal, the Unity Hub is the only method for activating and returning licences."
> — *License activation methods*, Unity 6.3 manual

and, on the manual/offline route:

> "Manual activation supports two kinds of licenses: assigned seats on a Unity Enterprise or Unity Industry subscription, and legacy serial-based Unity Pro licenses. It doesn't support Unity Personal, or floating license subscriptions."
> — *Offline (manual) license activation*, Unity 6.3 manual

So, in 2026, **there is no Unity-supported way to activate a Personal licence on a headless machine.** Everything else in this section is either a workaround or applies to paid tiers only.

**What the command-line flags actually do now.** The flags still exist and are still documented in the 6000.3 *Editor command line arguments* page — none are marked deprecated — but their scope excludes Personal:

| Flag | Manual text (6000.3) | Applies to Personal? |
|---|---|---|
| `-createManualActivationFile` | "Step one of a three-step process to manually activate a Unity license." | **No** — manual activation excludes Personal |
| `-manualLicenseFile <file>` | "Step three… The license file is a `.ulf` file for serial key activations, or an `.xml` file for assigned seat activations." | **No** |
| `-serial <key>` | "Activate your paid Unity license." Must be used with `-batchmode`. | **No** — the word is *paid* |
| `-username` / `-password` | Unity ID email and password, "used for license activation and UPM package signing" | Present, but only meaningful alongside `-serial` or a seat |
| `-returnlicense` | "Return the currently active serial-based or named user license. Don't use `-returnlicense` to return a floating license." | **No** |

Note the change from the old story: the `-username`/`-password`/`-serial` trio has **not** been removed, but it has been narrowed to paid licences, and the modern implementation sits behind the separate **Unity Licensing Client** binary (`Unity.Licensing.Client`, shipped in the editor install) rather than the editor executable. Unity's own docs state that `unity-editor -serial` "doesn't apply to Personal".

**The `.alf` → `license.unity3d.com/manual` → `.ulf` flow is dead for Personal.** Unity hid the "Personal Edition" option on that page around **August 2023** (community thread 926760, 18 Aug 2023; the well-known DOM-editing workaround was published 20 Aug 2023 and its author later marked it obsolete). Do not build a pipeline on it.

**What GameCI actually does for Personal, and what still works.** GameCI's current instructions (game.ci/docs/github/activation) no longer mention `.alf` at all. They are:

1. Activate Personal **interactively in Unity Hub on a real machine** (Hub → Preferences → Licenses → *Get a free personal license*).
2. Copy the resulting `.ulf` out of
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
3. Store it as the repository secret `UNITY_LICENSE`, plus `UNITY_EMAIL` and `UNITY_PASSWORD`.

The Linux path in `unity-test-runner`/`unity-builder` then simply **writes that file into the container** — it is a file copy, not an activation call. That is why it works headlessly and why it is a workaround rather than a supported flow. `unity-test-runner` **v4.4.0 (9 Sep 2026)** explicitly lists "Restored UNITY_LICENSE activation methods" in its changelog, which is good evidence the file-drop route is functioning as of this month — and also evidence that it is fragile enough to have broken at least once.

**The newest development (this month).** `game-ci/cli` PR #246, **merged 5 September 2026**, adds first-class personal activation by driving the licensing client:

```
Unity.Licensing.Client --activate-all --include-personal \
  --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"
```

Points worth knowing: those flags are **undocumented by Unity** and can change without notice; a headless activation of this kind **cannot answer a 2FA or device-verification challenge**; and because it takes a *seat* rather than a file, the PR adds a `game-ci return-license` command with EXIT traps, because "personal seats must be explicitly returned after use to prevent account-level degradation". In `unity-test-runner` this arrives via the v5 line (v5.0.0-beta.1, 18 Aug 2026); v4.4.0 delegates to the CLI as a subprocess but keeps the v4 inputs.

**Seat limits and what the terms permit.** Unity's licence-compliance page is blunt:

> "Running Unity on more than one machine at the same time is not allowed."
> "A separate license is required for build machines."

Unity's answer for build machines is **Unity Build Server**, a floating-licence product; it is not available under Personal. I could not find a public, Personal-specific activation count for 2026 (the "2 activations" figure that circulates refers to Pro). The honest reading is: **a Personal licence entitles one person to one machine at a time; a GitHub-hosted runner running your `.ulf` while you also have the editor open locally is outside the letter of the terms.** Unity does not appear to enforce it, and the entire GameCI Personal flow depends on that, but it is a real exposure for a project that may later ship commercially.

This is the decisive argument for the runner choice below: **running CI on the owner's own, already-Hub-activated machine sidesteps the whole problem.** There is no activation step, no secret, no seat to return, and no second machine.

### 2. Runner choice

| | GitHub-hosted (`ubuntu-latest` + `unityci/editor`) | Self-hosted on the owner's Windows 11 box |
|---|---|---|
| Editor install | Pulled as a multi-gigabyte docker image each run (minutes are billed while it pulls) | Already installed (`6000.3.24f1`), zero setup |
| `Library/` | Must be cached; a URP project's `Library` is easily 2–5 GB against a **10 GB per-repository Actions cache limit**, so it thrashes | Persists in the runner workspace between runs for free — the fastest possible option |
| Licence | Personal `.ulf` file-drop workaround; grey area under the terms | **None needed** — the machine is already activated in Hub |
| Synty assets | Absent (gitignored) — proves the "must build without Synty" rule | Also absent: the runner clones into its own `_work` directory, so it **equally** proves the rule. Do *not* point the runner at `D:\code\odyssey` |
| Cost | 2,000 free Linux minutes/month on the Free plan for private repos; Linux 1×, Windows 2×, macOS 10× multiplier; overage from about $0.006/min Linux and $0.010/min Windows (rates cut by up to 39% on 1 Jan 2026) | **Free** — self-hosted runner minutes are not billed |
| Availability | Always | Only when the machine is on; a PR at 3am waits |
| PlayMode graphics | Container uses `xvfb-run` for a virtual display | Real GPU, closest to the actual game |

**Docker tags exist for our exact version.** Confirmed on Docker Hub, all published **2026-09-10**:
`unityci/editor:ubuntu-6000.3.24f1-base-3`, `…-base-3.2`, `…-base-3.2.2`, plus `windows-6000.3.24f1-base-3*` and per-module variants (`linux-il2cpp`, `windows-mono`, `webgl`, `android`, `ios`, `mac-mono`). `-base` is the right one for us: we run tests and an editor method, we do not build a player. The `3`/`3.2`/`3.2.2` suffix is the GameCI image version, matching the `containerRegistryImageVersion` input (default `3`), so `unityVersion: auto` resolves correctly from `ProjectSettings/ProjectVersion.txt`.

**Recommendation: self-hosted Windows runner as the merge gate**, for three reasons in order of weight — (a) it removes the Personal-licence activation problem entirely, which is the thing that historically breaks; (b) the warm `Library/` makes the gate fast enough that people will actually wait for it, where hosted runners would spend most of every run re-importing assets; (c) it costs nothing against a 2,000-minute private-repo allowance that a Unity project burns through quickly. The availability drawback is acceptable for a one-owner project. Self-hosted runners on a **private** repository are safe; the standard warning about self-hosted runners applies to public repos, where forks can run arbitrary code.

### 3. Concrete workflow

See the fenced block under **Recommendation**. Key mechanics:

- **`clean: false` on `actions/checkout` is load-bearing.** Checkout's default `clean: true` runs `git clean -ffdx`, which deletes `Library/` (it is gitignored) and throws away the entire benefit of a self-hosted runner. With `clean: false` the workspace persists like a developer's own checkout.
- **No `actions/cache` on the self-hosted job.** Caching `Library/` there would upload gigabytes to GitHub storage and restore them over the network — slower than the disk that already holds them, and it would blow the 10 GB cache quota.
- **`shell: bash`** on a Windows runner selects Git Bash, which is exactly what `scripts/unity.sh` expects. No script changes are needed for portability.
- **PR check.** `dorny/test-reporter@v2` with `reporter: dotnet-nunit` parses Unity's NUnit3 XML and creates a check run with per-test annotations; it is pure JavaScript and runs on Windows. (`EnricoMi/publish-unit-test-result-action` is the usual alternative and also reads NUnit, but its Windows support goes through a separate `/windows` sub-action — one more moving part.) On the hosted Linux job, `unity-test-runner` creates the check itself from `githubToken` + `checkName`.
- **Artifacts.** `actions/upload-artifact@v4` on `TestResults/*.xml` and `Logs/*.log`, with `if: always()` so a failing run still uploads the evidence. Note the Free plan's 500 MB artifact storage — keep `retention-days` short and never upload `Library/`.
- **The headless day stays on the self-hosted job.** Running `-executeMethod` inside the GameCI container means hand-mounting the `.ulf` into the right in-container path, which is undocumented and version-dependent. Not worth it; the self-hosted runner needs none of it.

### 4. Cross-platform notes

- **`scripts/unity.sh` already works on both.** It resolves `C:\Program Files\Unity\Hub\Editor` under Git Bash and `~/Unity/Hub/Editor` on Linux, and honours `UNITY_EDITOR` for an explicit override. Set `UNITY_EDITOR` in the runner's environment if the Hub path ever moves; nothing else needs to change.
- **`-batchmode` vs `-nographics`.** `-batchmode` alone already selects a null graphics device in most configurations; `-nographics` makes that explicit and is what `unity.sh` passes today. EditMode tests are unaffected. **PlayMode is where it bites:** anything that renders — URP camera output, render-texture readback, shader compilation checks, screenshot comparisons — will fail or return blank against a null device, and there are long-standing reports of PlayMode runs hanging under `-nographics`.
- **Linux containers solve this with `xvfb`:** the GameCI images wrap the editor in `xvfb-run` to supply a virtual X display, so `-nographics` is *not* needed there. On **Windows** there is no xvfb equivalent; the fix is to drop `-nographics` (the runner has a real GPU and can render offscreen) or, on a GPU-less box, to force software rendering with `-force-driver-type-warp` (D3D11 WARP).
- **Practical rule for Odyssey:** keep `-nographics` for EditMode and for the headless one-day simulation (pure Sim code, no rendering — this is exactly why the brief keeps Sim free of `UnityEngine` dependencies), and run PlayMode **without** `-nographics` on the self-hosted Windows runner. That means a small addition to `unity.sh`: let the `test` subcommand decide, rather than hard-coding `-nographics` for both modes.
- **Licence differences between the two OSes.** On Linux, GameCI's Personal path is the `.ulf` file-drop and works. On **Windows and macOS hosted runners, the Personal `.ulf` route is unreliable** — GameCI's Windows/macOS path is built around `-serial` (professional), and `-serial` explicitly does not apply to Personal. If we ever need a hosted runner, it must be Linux.

### 5. Cost

- Self-hosted runner: **nothing**. Minutes are not billed.
- Optional hosted Linux parity job: within the Free plan's 2,000 Linux minutes/month. A GameCI run on this project is realistically 10–20 minutes including the image pull and a cold import, so roughly 100 runs/month before overage at about $0.006/min.
- No Unity spend. Unity Build Server is the "correct" answer for a build machine and is not available on Personal; if the project ever moves to Pro it becomes the clean fix.

## Recommendation

**Commit to one self-hosted Windows runner on the owner's dev machine as the merge gate, with no licence plumbing at all. Add a manually-triggered hosted-Linux job later, only if a second pair of eyes on clean-clone behaviour is wanted.**

This is the cheapest setup that genuinely gates merges, and it is the only one that does not depend on a Personal-licence workaround that Unity's own documentation says is unsupported.

**Do first, in this order:**

1. **Register the runner.** On the Windows box: repo → *Settings → Actions → Runners → New self-hosted runner* (Windows x64). Install it as a **service** so it survives reboots, give it the labels `self-hosted`, `windows`, `unity`, and let it use its **own** `_work` directory — never `D:\code\odyssey`. This alone takes about ten minutes and proves the licence story end to end, because a Hub-activated machine needs no activation step.
2. **Add the workflow below** as `.github/workflows/ci.yml` (the lead session creates it after review — this research file deliberately does not).
3. **Split `-nographics`** in `scripts/unity.sh` so PlayMode runs with a real device while EditMode and `exec` keep it.
4. **Turn on branch protection** for `main`: require the `Unity tests` check and require branches to be up to date. Without this step there is no gate, only a report.
5. Defer the hosted-Linux job until there is something concrete to gain from it.

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

concurrency:
  # One Unity run at a time per branch: the editor locks the project directory.
  group: unity-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read
  checks: write        # dorny/test-reporter creates the PR check
  pull-requests: read

jobs:
  unity:
    name: Unity tests
    # Label the owner's Windows 11 machine with all three of these.
    runs-on: [self-hosted, windows, unity]
    timeout-minutes: 60
    defaults:
      run:
        shell: bash    # Git Bash on Windows; this is what scripts/unity.sh expects.
    steps:
      - name: Checkout
        uses: actions/checkout@v5
        with:
          # clean: false keeps the gitignored Library/ between runs. This IS the cache;
          # without it every run re-imports the whole project (tens of minutes).
          clean: false
          lfs: false

      - name: Which editor
        run: scripts/unity.sh which

      # EditMode: pure Sim plus editor tooling. Safe with -nographics (the unity.sh default).
      - name: EditMode tests
        run: scripts/unity.sh test editmode

      # PlayMode: needs a real graphics device for URP. The self-hosted box has a GPU, so
      # drop -nographics. UNITY_PLAYMODE_GRAPHICS is read by the unity.sh change in step 3
      # of the recommendation; until that lands, pass the flags through explicitly instead.
      - name: PlayMode tests
        env:
          UNITY_PLAYMODE_GRAPHICS: "1"
        run: scripts/unity.sh test playmode

      # The milestone gate: a headless one-day simulation with no errors.
      # Pure Sim code, no rendering, so -nographics (baked into `exec`) is correct.
      - name: Headless one-day simulation
        run: scripts/unity.sh exec Odyssey.EditorTools.HeadlessDay.Run

      - name: Upload test results and logs
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: unity-test-results
          path: |
            TestResults/*.xml
            Logs/*.log
          retention-days: 14
          if-no-files-found: warn

      - name: Publish test results as a check
        if: always()
        uses: dorny/test-reporter@v2
        with:
          name: Unity tests
          path: TestResults/*.xml
          reporter: dotnet-nunit
          fail-on-error: true

  # ---------------------------------------------------------------------------
  # OPTIONAL, NOT PART OF THE GATE. Hosted Linux run in a clean clone, for parity
  # checks. Needs the Personal .ulf in the UNITY_LICENSE secret (Unity Hub ->
  # Preferences -> Licenses on an activated machine, then copy
  # C:\ProgramData\Unity\Unity_lic.ulf). Manual trigger only, so it never eats the
  # 2,000-minute allowance by accident, and never runs while the editor is open
  # locally (see the one-machine-at-a-time clause in the findings).
  # ---------------------------------------------------------------------------
  unity-linux:
    name: Clean-clone tests (hosted Linux)
    if: github.event_name == 'workflow_dispatch'
    runs-on: ubuntu-latest
    timeout-minutes: 60
    permissions:
      contents: read
      checks: write
    steps:
      - uses: actions/checkout@v5

      - name: Cache Library
        uses: actions/cache@v4
        with:
          path: Library
          key: Library-linux-${{ hashFiles('Packages/packages-lock.json', 'ProjectSettings/ProjectVersion.txt') }}
          restore-keys: |
            Library-linux-

      - name: EditMode and PlayMode tests
        uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          unityVersion: auto              # reads ProjectVersion.txt -> 6000.3.24f1
          testMode: all                   # editmode + playmode; xvfb gives PlayMode a display
          artifactsPath: artifacts
          coverageEnabled: false          # turn on later if coverage becomes a gate
          githubToken: ${{ secrets.GITHUB_TOKEN }}
          checkName: Unity tests (Linux)
          # containerRegistryRepository/containerRegistryImageVersion default to
          # unityci/editor and 3, resolving to unityci/editor:ubuntu-6000.3.24f1-base-3.

      - name: Upload results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: unity-test-results-linux
          path: artifacts
          retention-days: 14
```

## Layer questions touched

**None.** This lane is build infrastructure only; it touches no cell, layer or `(x, y, z)` decision. The one indirect tie-in is that the headless one-day simulation must stay free of `UnityEngine` rendering dependencies so it can run under `-nographics` — which the brief's Sim/Presentation assembly split already guarantees.

## Sources

- Unity 6.3 manual, *License activation methods* — https://docs.unity3d.com/6000.3/Documentation/Manual/LicenseActivationMethods.html ("For Unity Personal, the Unity Hub is the only method for activating and returning licences.")
- Unity 6.3 manual, *Offline (manual) license activation* — https://docs.unity3d.com/6000.3/Documentation/Manual/ManualActivationGuide.html ("It doesn't support Unity Personal…")
- Unity 6.3 manual, *Editor command line arguments* — https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html
- Unity 6.3 manual, *Licenses and activation* — https://docs.unity3d.com/6000.3/Documentation/Manual/LicensesAndActivation.html
- Unity, *Licensing and Unity Editor Software Terms* / compliance — https://unity.com/pages/license-compliance ("Running Unity on more than one machine at the same time is not allowed"; "A separate license is required for build machines.")
- GameCI, *Activation (GitHub)* — https://game.ci/docs/github/activation/
- GameCI, *Test runner (GitHub)* — https://game.ci/docs/github/test-runner/
- GameCI, *Getting started (GitHub)* — https://game.ci/docs/github/getting-started/
- `game-ci/unity-test-runner` releases (v4.4.0, 9 Sep 2026; v5.0.0-beta.1, 18 Aug 2026; v4.3.2 `--shm-size`) — https://github.com/game-ci/unity-test-runner/releases
- `game-ci/unity-test-runner` `action.yml` (full input list) — https://raw.githubusercontent.com/game-ci/unity-test-runner/main/action.yml
- `game-ci/cli` PR #246, "personal (free) license activation via the Unity licensing client", merged 5 Sep 2026 — https://github.com/game-ci/cli/pull/246
- `game-ci/cli` — https://github.com/game-ci/cli
- Docker Hub, `unityci/editor` tags filtered to 6000.3 (published 2026-09-10) — https://hub.docker.com/r/unityci/editor/tags
- GameCI docker images overview — https://game.ci/docs/docker/docker-images/
- Unity Discussions, "Unity no longer supports manual activation of Personal licenses" (18 Aug 2023) — https://discussions.unity.com/t/unity-no-longer-supports-manual-activation-of-personal-licenses/926760
- Ankur Sheel, "Workaround for Unity Personal License Manual Activation Not Supported" (20 Aug 2023, updated 7 May 2024; the author notes it is obsolete with unity-builder v4) — https://www.ankursheel.com/blog/unity-personal-license-manual-activation-workaround
- GitHub Changelog, "Simpler pricing and a better experience for GitHub Actions" (16 Dec 2025) — https://github.blog/changelog/2025-12-16-coming-soon-simpler-pricing-and-a-better-experience-for-github-actions/
- Unity Issue Tracker, null graphics device under `-batchmode -nographics` — https://issuetracker.unity3d.com/issues/standalone-your-gpu-null-device-or-driver-doesnt-support-linear-rendering-error-with-batchmode-nographics
- Unity Discussions, "Do PlayMode tests work with -nographics?" — https://discussions.unity.com/t/do-playmode-tests-work-with-nographics-also-ways-to-speed-up-tests/942644
- `game-ci/docker` issue #240 (xvfb-run wrapping of the editor on Linux) — https://github.com/game-ci/docker/issues/240

## Confidence

- **High** — Unity's official position that Personal cannot be activated by manual file or `-serial`, and that Hub is the only supported route. Two independent 6000.3 manual pages say it in plain words.
- **High** — `unityci/editor` tags exist for `6000.3.24f1` (`-base-3`, `-base-3.2`, `-base-3.2.2`, ubuntu and windows), published 2026-09-10.
- **High** — `game-ci/unity-test-runner@v4` is still the sane default for a hosted Linux job; v4.4.0 (9 Sep 2026) is current and v5 is beta only.
- **High** — GitHub Actions costs: 2,000 free Linux minutes/month on the Free plan for private repos, Windows 2× multiplier, self-hosted minutes free, 10 GB cache per repo.
- **High** — the recommendation itself (self-hosted runner as the gate). It follows from the licence finding regardless of the smaller uncertainties below.
- **Medium** — that the Personal `.ulf` file-drop still works on hosted Linux today. Evidence is the v4.4.0 changelog line "Restored UNITY_LICENSE activation methods" plus current GameCI docs; I did not run it. This is precisely why it is the optional job, not the gate.
- **Medium** — the exact PR-check action. `dorny/test-reporter@v2` with `reporter: dotnet-nunit` is the right shape for Unity's NUnit3 XML on a Windows runner, but I did not verify that tag or its behaviour against a Unity results file specifically.
- **Medium** — that a CI Unity process and the owner's interactive editor can run simultaneously on the same activated machine. The licence is machine-bound so this should be fine, and the separate workspace avoids the project lock, but I found no explicit Unity statement permitting two concurrent editor processes.
- **Low / unverified** — the precise in-container path GameCI writes the `.ulf` to. This is why the headless-day step is kept off the container.

## Could not be determined

- **The exact number of machines or activations a Unity Personal seat permits in 2026.** The compliance page gives the principle ("not more than one machine at the same time") but no activation count; the "2 activations" figure circulating online refers to Pro. Not resolvable without Unity support.
- **Whether Unity would consider a self-hosted CI runner on an already-activated personal machine a "build machine" requiring a separate licence.** The terms say build machines need a separate licence and point at Build Server (unavailable on Personal); they do not address the case where the build machine *is* the developer's own activated workstation. Common practice treats it as fine. Flagged rather than guessed, and worth a support ticket before any commercial release.
- **Whether `game-ci/cli`'s `--activate-all --include-personal` path is stable.** The flags are undocumented by Unity, and the PR's own notes say Unity can change them without notice and that headless activation cannot clear a 2FA challenge. Revisit when `unity-test-runner` v5 leaves beta.
- **Precise minutes per run for this project on hosted Linux.** The 10–20 minute estimate is inferred from image size and typical URP import times, not measured. Measure it if the optional job is ever adopted.
- **Whether `-nographics` specifically breaks any of Odyssey's PlayMode tests.** There are no PlayMode tests yet. The mitigation (run PlayMode with a real device on the self-hosted runner) is cheap enough to adopt pre-emptively.
