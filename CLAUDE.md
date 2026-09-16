# Odyssey — project guide for Claude Code

Read this first, then `docs/brief.md` (the governing brief) and `docs/research/INDEX.md` (what is known so far). Update the **Current status** section whenever it changes, and **append the reasoning to `docs/journal.md`** rather than growing this file — that is what the journal is for.

## What this is

A prototype colony sim in the RimWorld mould, in true 3D with discrete vertical layers, set in a ruined sci-fi city. Unity 6.3 LTS (6000.3.x), URP, C#. Art: Synty POLYGON Sci-Fi City. The owner does visual work in the editor on Pop!_OS; Claude Code does code, tests, research and documentation.

## Working agreement (from the brief; these hold everywhere)

- **Phases with hard stops.** Ground → Interview → Research → Plan → Execute. End the turn after each phase and wait. Never run a later phase on assumed answers. No gameplay code until Phase 4, and only after the plan is approved.
- **Research lives in subagents.** One subagent, one question, a hard cap ("stop after N searches / N reads"), fixed return format: Findings, Sources (URLs), Confidence (high/medium/low), Could not be determined. Write each result to `docs/research/<slug>.md` and keep `docs/research/INDEX.md` current (the naming scheme is in it).
- **Commit to recommendations.** Rank options and back one. If two are tied, name the observation that breaks the tie and the cheapest experiment that gets it.
- **Clean room.** Study RimWorld's mechanics, formulas, data shapes and design intent; never paste Def XML, decompiled code, art, audio, names or flavour text into this repo. Never decompile into the repo. Invent our own names.
- **Licensed assets stay licensed.** Synty content lives only under `Assets/Synty/`, which is gitignored. Never copy it elsewhere, never commit it, and never let the simulation or its tests depend on it (clones without the pack must still build and run headless).
- **Files outlive context.** Every phase produces files under `docs/`. Assume the next session knows nothing except what is written down.
- British English in documentation. No multiplayer, ever. *Ramble* (Godot) is reference only, no code reuse.

## The content wiki is a standing obligation

`docs/wiki/` is the naming reference for the whole game: every commodity, item, building, command,
work type, need, body part, alert and proper noun, with a stable key beside each. It exists so the
owner can read what is in the game and correct it, and so no session has to guess what something is
called. Hosted: [wiki](https://claude.ai/artifact/JsYRQk1vnza2wFWSNfpQwr) ·
[HUD mockup](https://claude.ai/artifact/PcHWoujGDvd4rzcH1LAwFG).

**The rule: any commit that changes game content updates the wiki in the same commit.** Game content
means anything a player could see named — a new commodity, a renamed building, a reworded
description, a new alert, a faction, a creature, a research project, a month of the calendar. Design
and mechanics are not wiki content; they stay in `docs/design/` and `docs/adr/`.

**Never hand-edit anything under `docs/wiki/`.** It is generated and your edit is silently
overwritten. Edit the source, then rebuild:

| Source | Holds |
|---|---|
| `docs/design/icon-keys.csv` | the name, namespace, milestone and description of every named thing |
| `docs/design/icon-map.csv` | whether the owner's pixel-art sheets can draw it |
| `docs/design/proper-nouns.csv` | people, places, factions, creatures, the calendar |

```
python3 tools/wiki/build_wiki.py            # rebuild docs/wiki
python3 tools/wiki/build_wiki.py --check    # exit 1 if stale; run before committing
python3 tools/wiki/emit_labels.py           # rebuild Assets/Odyssey/Hud/Registry.g.cs
python3 tools/wiki/emit_labels.py --check   # exit 1 if stale; run before committing
```

The HUD reads its labels from the same file: `emit_labels.py` generates `Registry.g.cs`
(`Registry.Label(key)`), `JobLabels` and `LedgerModel` name nothing themselves, and
`RegistryTests` fails the fast tier on any key the CSV does not know. A content commit runs both
checks.

Both `--check`s are the gate and belong in CI beside the test tiers. Two notes before extending
it. The registry is hand-authored **only until the Def set covers it**: then `icon-keys.csv` is generated
one way out of the Defs and committed, so the wiki and the build-gating icon tests share one
source. Do not create a second source of truth meanwhile. And the hosted copies are snapshots:
after a rebuild, republish `docs/wiki/artifact.html` and
`docs/reference/mockups/hud-v2.artifact.html` (regenerate the latter with
`python3 tools/mockups/artifact_body.py docs/reference/mockups/hud-v2.html`).

## Current status

**Read `docs/journal.md` for how any of this came to be.** It is the narrative record — every
decision, measurement and reversal behind the state below, and it is where this section's history
went on 2026-09-16. What follows is only what is true *now*.

### Where the project is

Phases 0–3 (ground, interview, research, design) are complete. **Phase 4, execution, is under way.**

- **M0 is closed.** CI runs two tiers on every push and pull request: a *fast tier* on
  GitHub-hosted Linux (Sim, Hud, Long, the content-registry check, the icon tooling) and a *Unity
  tier* on the owner's Windows machine as a self-hosted runner (EditMode, PlayMode, the headless
  one-day run), switched on by the repository variable `UNITY_RUNNER=1`.
- **M1 and M2 are done and reported** — `docs/milestones/M1-report.md`,
  `docs/milestones/M2-report.md`. Both went further than the plan asked.
- **M3 is under way:** designations, felling, stockpiles and mining are in.
- **Work reaches `main` only through a pull request** with both tiers green, one approving review
  and the branch up to date. Branch protection enforces it, agents included. There is no long-lived
  feature branch — `claude/*` branches are per-change and short-lived.

**Before more features, open the seams.** The mining line was 73 files and had to edit six shared
files to add itself, five of which should have been extension points. The table and the order are in
`docs/plans/vertical-slice.md`, "Where the seams are" — **audited against the code on 2026-09-17,
because it had gone stale and misled a session into recommending work that had already landed.**

- **Content is written once** (2026-09-17). `PawnContent.Core()` is deleted and the XML under
  `Assets/Odyssey/Defs/Core/Pawns` is the only copy; every caller goes through
  `ContentPack.Pawns()`, which finds the pack by walking up to the repository root and caches the
  parse. A built player would not find it — nothing builds one, and `ContentPack.UseRoot` is the
  tested seam for the day something does. **The world tables are still doubled** (`CoreContent.Terrain`,
  `NaturalContent.Terrain` against `Defs/Core/World/*.xml`); that is the remaining half.
- **Work givers register themselves** (OQ-44): a giver in the simulation assembly joins by existing.
- **Still open:** mesh contributors for `ChunkMesher` (OQ-46), and `OdysseyBootstrap` wiring every
  presentation system by hand — the one chokepoint with no queue row.

### What runs today

**Simulation** (`Odyssey.Sim`, `Odyssey.Sim.Contracts`, both UnityEngine-free) — grid, support
solver, two worldgen paths, pathfinding, pawns, save/load, Def loader, subsystem schedule, and the
snapshot-read / intent-write seam. Colonists walk, chop, mine, haul, eat and sleep, with needs, mood
and skills. A colony survives ten headless days on three seeds, and a 60,000-tick day ends on the
same hash under both Mono and CoreCLR.

**The board is a wooded meadow**, 120 x 120 x 16: grass everywhere, woodland at the natural
generator's density with a clearing at the start, streams and ponds, real 3 m terrace risers, and
rock, ore and sealed caverns beneath. `NaturalMapGenDef.MakeWooded()`, chosen by
`OdysseyBootstrap.woodedMap`; it is a *cover* mode, not the barren board with trees put back. The
bare board (`MakeBarren()`) is the test baseline on which anything that is not grass is a bug. The
ruined-city generator is still present and still tested, but it is not what the scene loads.

**Movement is walk, stair, ladder and a one-block hop.** Climbing was removed as a mechanic (owner,
2026-09-16): one block up into the column next door is a jump (`MoveCost.JumpUp` 135), one block
down off it is a drop (`Drop` 50), and anything deeper wants a ladder, which is built. Every cell a
pawn can be in has something under it. A hop is three seams that must agree — the cell search
(`PathFinder.RelaxHop`), the region graph (`NavGraph.TryHopEdges`) and the mover's price
(`MovementSystem.StepCost`); **a price the planner and the mover disagree about fails silently.**
Ladders are still climbed, so the climb *pose* is live presentation code.

**Presentation** — instanced chunk rendering (no GameObject per cell), a slice camera rig, the HUD,
audio, a day/night cycle and golden-hour grading. No pack contains a work animation, so the axe,
pick and hammer strokes are **computed** (`WorkSwing`, `WorkStyle`) and stand in for art we do not
have.

**Colonists are 61 Synty characters, recoloured — not dressed.** No modular body exists in any
pack (a character is one skinned mesh from scalp to boots with one material), so clothing, hair and
skin are repainted by rewriting the atlas swatch rectangles each vertex is already mapped to
(`Odyssey/Character`, `ColonistMaterials`). Appearance derives from the world seed and the pawn id,
so a world deals the same people every load. **`OdysseyBootstrap.randomCastEachSession` defaults
on** while the palette is being judged, which means pressing Play deals new faces each time; switch
it off for a stable cast.

**What the player can see is decided by how deep they are.** At or above the surface, every layer
above is drawn solid; below it, one layer above is x-rayed and every layer below is drawn. Anything
drawn solid is clickable at any depth; a ghost never is. Whatever hides a selected colonist fades to
a ghost while it stands there. **The landscape is never cut away:** the band below the slice reaches
down to the lowest ground on the board (`WorldRenderModel.LowestOutdoorLayer`) or `belowDepth`
layers, whichever is lower, because the surface is terraced and spans five layers while the depth
budget is a cue for looking *through* something. A pit somebody digs is still governed by the
budget; a hillside never was.

**Nothing in presentation is in a cell, a save or the hash** — grass tufts, ground relief, banks,
chips, the surrounding land, sound and the see-through fade are all drawn and none are simulated.
That rule is load-bearing; keep it.

### Tests and gates

- **Fast tier** (`scripts/test-fast.sh`, ~10 s, no Unity): **432 Sim + 106 Hud**.
- **Unity tier** (`scripts/unity.sh test editmode`, authoritative) plus PlayMode, which is the only
  place frame time is measured — never an editor `camera.Render()` loop.
- **Content gates:** `python3 tools/wiki/build_wiki.py --check` and
  `python3 tools/wiki/emit_labels.py --check`. Both must pass before a content commit.

Frame time under the real player loop, against a 5 ms budget: meadow ~0.99 ms, city ~1.56 ms on an
RTX 5070 Ti at 640 x 480. The city's move from 0.88 to 1.56 ms is **unexplained** and still open.

### Fixed decisions

- **Cell size: 2.5 × 2.5 × 3.0 m** (ADR 0002), irreversible. Half-heights and slopes are a drawing
  offset, never a cell.
- **Architecture: plain C# structure-of-arrays with Burst on measured hot paths** (ADR 0005),
  decided by a benchmark in which both candidates produced the identical state hash.
- **Determinism before threads.** Single-threaded fixed-tick sim with tick groups.
- **No multiplayer, ever.**
- *Subsystems* are simulation-side; *directors* are presentation-side. Do not unify the two words
  (`01-architecture.md` §3a).

### Top technical risk

**Pathfinding cost:** 65% of the measured tick is A-star. The once-recorded explanation — futile
searches for unreachable targets — was **falsified by its own follow-up experiment** (only 14% of
budget exhaustions were unreachable, under 1% on a structured map). The fix is hierarchical search
plus a better heuristic, with the district-id reachability check (`d-04-pathfinding.md`) measured
first.

### Waiting on the owner

- **Nobody has pressed Play on the look work.** Every judgement about the day cycle, the golden
  hour, the hill wood and the colonist palette comes from contact sheets and `FrameTimeTests`. A
  sheet cannot say whether night is playable or whether the light steps at speed 3.
- **The rest of the icon art.** Four keys draw real art as of 2026-09-16 (`ui.res.wood`,
  `ui.res.stone`, `ui.res.ironore`, `ui.res.scrap`); the other **130 gaps of 405 keys** still draw
  an outlined square. `IconArt` resolves a key to a texture and falls back, so the HUD is correct
  at every stage in between and one icon can be judged in the running game. Sheets go in
  `art-source/icons/sheets/` (that folder's README names them). **Open, and the owner's call:** the
  HUD draws icons at 16, 17 and 30 px while ADR 0007 says not to draw pixel art below 32 — measured,
  30 px reads, 17 px loses the grooves, 16 px goes to noise.
- **The 29 proposed proper nouns** in `docs/design/proper-nouns.csv` await approval or veto.
- **Marsh reads as a sandy bank** — re-tint it greener or rename it.
- **The audio listener is on the camera**, 32–160 m up, while the catalogue authors ranges as ground
  distances. Either move the listener to the camera's focus or re-author the ranges; it changes how
  the whole game sounds.

### Known gaps

Felled trees, mined cells and climbs are not in the save (the designation grid is not saved). Mining
collapses nothing. There is no fog of war, so a sealed cavern is visible if the player scrolls the
layer down. Cross-runtime determinism is a measurement rather than a standing test.

## Read this before losing an hour

`docs/lessons.md` collects the operational lessons that have already cost time once: the Unity batch run that finishes without exiting and locks the project, assembly definitions silently dropping implicit package references, why filtering tests saves nothing, and the working-method rules for parallel agents. **Add to it whenever something takes more than about ten minutes to diagnose.**

The two that come up daily:

- **Tests:** `scripts/test-fast.sh` while working (~1.7 s, no Unity), `scripts/unity.sh test editmode` before committing (authoritative).
- **Fix issues before adding features** (owner rule, 2026-09-15), and re-run after a fix to check the output *means something* — a plausibly wrong result is worse than an obviously broken one.

## Repository layout

- `docs/lessons.md` operational lessons (read it) · `docs/journal.md` the narrative record of how the build got here (why a decision was made, what was measured, what was later falsified) · `docs/brief.md` governing brief · `docs/research/` research files and `INDEX.md` · `docs/reference/screenshots/` reference images and descriptions · `docs/setup/local-dev.md` dev-machine setup (§8 for Windows). Later phases add `docs/design/`, `docs/adr/`, `docs/plans/`, `docs/milestones/`.
- The Unity project lives at the **repository root** (`Assets/`, `Packages/`, `ProjectSettings/`), created 2026-09-15 on the Windows dev machine (Unity 6000.3.24f1, Universal 3D template).
- `Assets/Odyssey/` the game assemblies: `Sim.Contracts`, `Sim` (both UnityEngine-free), `Tests/Sim`. `Assets/Editor/Odyssey/` editor tooling: `SyntyInventory.cs`, `SyntyImport.cs`, `VisualBlockScene.cs`. `tools/dotnet/` mirror projects for the fast test tier. `Assets/Synty/` licensed packs, ignored by git.
- `scripts/unity.sh` headless Unity wrapper (`inventory`, `test`, `exec`, `shot`, `open`, `which`; `shot` takes an optional method, e.g. `shot Odyssey.EditorTools.ScatterSheet.Shoot` for a contact sheet of candidate props) and `scripts/test-fast.sh` the no-Unity test tier.
- `docs/wiki/` the generated content wiki (read it, never edit it — see the section above). `tools/wiki/build_wiki.py` builds it; `tools/icons/icons.py` is the icon pipeline (detect, contact, export, validate, emit-web) with 30 tests via `python3 -m unittest discover -s tools/icons -t tools/icons`; `tools/mockups/artifact_body.py` makes a mockup publishable. All three are standard library only, so they run in a container with no Unity.
- `art-source/` owner-owned source art kept **outside** `Assets/` so Unity does not import it. `art-source/icons/sheets/` is where the eight icon sheets go.

## Environment

- **Dev machines:** Pop!_OS (Unity Hub, RTX 5070 Ti) and Windows 11 (`D:\code\odyssey`, Unity CLI/Hub beta — see `docs/setup/local-dev.md` §8). Both run Unity 6000.3.x LTS; neither is the performance target (that is a 2022 mid-range laptop). Keep the project path free of spaces (a Unity-MCP constraint).
- **dotnet SDK 8.0.425** is installed on the Windows machine at `%USERPROFILE%/.dotnet` and powers `scripts/test-fast.sh`. A remote container without Unity can still run every Sim test through it, given an SDK.
- **Python 3.13.15** is installed on the Windows machine as of 2026-09-16 (`%LOCALAPPDATA%\Programs\Python\Python313`, ahead of `WindowsApps` in PATH, with a `python3.exe` copy beside `python.exe` because CPython ships none). The wiki, icon and mockup tooling therefore runs on **both** machines now. `PYTHONUTF8=1` is set for the user and is required: without it Windows Python reads the docs as cp1252 and `build_wiki.py --check` calls every file stale. See `docs/lessons.md`.
- **Blender (optional):** only for gaps no Synty asset fills (a stair or ladder variant at the cell size, UV or atlas fixes, rig or animation retargeting). Synty first. Blender-made pieces go under `Assets/Art/Custom/` and are committed; they must match the Synty style and snap to the cell grid.
- **Unity MCP:** IvanMurzak/Unity-MCP, installed per `docs/setup/local-dev.md`. Once connected, Claude Code can open scenes, run EditMode/PlayMode tests, read the console and execute editor C#. Prefer `scripts/unity.sh` for anything that must also work in CI.

## Conventions for code (apply from Phase 4 / M0 onwards)

- C# with nullable enabled and analysers on. Assembly definitions per layer: Sim (no UnityEngine dependency where possible), Presentation, Editor, Tests.
- **Pragmatic TDD** (Phase 1 Q5): test-first for every Sim system; a determinism harness (same seed → same state hash) and golden-master one-day headless runs are first-class tests; presentation/tooling get smoke tests; throwaway spikes exempt until kept.
- **Determinism before threads** (Phase 1 Q6): single-threaded fixed-tick sim with tick groups; Burst jobs behind clean boundaries only on benchmark-proven hot paths. Composition root, no scattered manager singletons.
- Sim classes public, unsealed and virtual where cheap, so Harmony-style patching stays possible. Data-driven Defs with inheritance and patch operations from day one.
- Every system that touches a cell is layer-aware (x, y, z) from its first commit. No 2D-first code, ever.
- Scenes and prefab variants are generated by editor scripts, not hand-authored, so they are reproducible.
- Tests run headless via `scripts/unity.sh test`. Each milestone gate is: tests pass, a headless one-day simulation runs with no errors, `docs/milestones/Mx-report.md` written, stop for review.
- Commits: small, one concern each, descriptive message. Never commit `Assets/Synty/`, `Library/`, logs or test results.
- Interface icons are referenced by symbolic key, never by filename, and are 64 px, point-filtered, uncompressed, no mips, displayed at 32 and 64 only (`docs/adr/0007-pixel-art-icon-pipeline.md`).
- Content changes carry their regenerated wiki and label registry: `python3 tools/wiki/build_wiki.py --check` and `python3 tools/wiki/emit_labels.py --check` both pass before the commit.

## Starting a local session

Run `claude` in the repository root; this file is read automatically. Useful first prompts:

- "Read CLAUDE.md and docs/research/INDEX.md. Run the procedure in docs/research/synty-import.md, fix SyntyInventory.cs if it does not compile, commit the inventory, and report the implied cell size."
- "Phase 1 answers are: … Record them in docs/research/phase1-answers.md, update CLAUDE.md status, then start the Phase 2 lanes that do not depend on the inventory."
