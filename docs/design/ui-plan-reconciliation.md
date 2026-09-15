# Reconciliation notes for the UI agent's plan (2026-09-15)

The owner shared the UI session's plan ("UI architecture, panel catalogue and reviewable mockup") with this line of work. The plan is sound and this project **adopts its sim→UI contract as a constraint on Lane D1** (see below). But the plan was written against a pre-merge snapshot of the repository, from a remote container; the facts below changed under it and need reconciling when its branch merges.

## Facts that changed since that plan's snapshot

1. **The Unity project exists** at the repository root: Unity 6000.3.24f1 LTS, URP 17.3.0, created 2026-09-15 on the Windows dev machine. Five Synty packs are imported under the gitignored `Assets/Synty/` (7,222 assets).
2. **The cell size is fixed: 2.5 × 2.5 × 3.0 m** (owner-confirmed; `docs/adr/0002-cell-size-and-layer-model.md`). The UI docs' "written against an unknown cell size" caveats can be resolved on merge — the layer ruler, slice control and overlay budgets can assume 250 × 250 cells × ~40 layers at this pitch.
3. **Phase 1 is complete** (`docs/research/phase1-answers.md`, seven answers) and **Phase 2 wave 1 is done** — twelve research files, including `b-going-medieval.md`, which answers the above/below display-policy question the UI plan flags as "pending Lane B": ghosted-but-clickable layers are Going Medieval's top player complaint, so the policy should be *ghosted and non-interactive* above the slice.
4. **`claude/synty-import-windows` and `claude/phase2-wave1` are merged into `main`** (PRs #2, #3). The UI plan targets `claude/cool-gates-5xkpgi`, which predates both; expect conflicts in `CLAUDE.md`, `docs/research/INDEX.md`, `docs/setup/local-dev.md` and `docs/reference/screenshots/README.md`, all substantially rewritten on `main`.
5. **ADR numbers 0001 and 0002 are taken** on `main` (0001 engine and version, 0002 cell size and layer model). The UI plan's ADRs should renumber: **0003 UI framework, 0004 sim→UI contract**. This line of work reserves **0005 simulation architecture** for the Lane D1 outcome.
6. **The wiki egress block is container-specific.** rimworldwiki.com was reachable from the Windows machine (Lane A wave 1 completed against it, sometimes needing a browser user agent). The INDEX constraint note should say "blocked from the remote container", not blocked in general.

## What this line of work adopts from the UI plan (so the two converge)

- **Snapshot-read / intent-write is a constraint on the Lane D1 architecture ADR.** The two-way benchmark (plain C# + jobs vs DOTS/ECS) includes, in every candidate's tick, building the immutable `WorldViewStore` back buffer — measured against the plan's budget of **≤ 0.8 ms/tick and ≤ 2 MB double-buffered**. An architecture that cannot afford its own view build loses.
- **`Odyssey.Sim.Contracts`** (views, handles, intents, reason codes; no UnityEngine) joins the assembly plan alongside Sim / Presentation / Editor / Tests.
- Stable handles (`PawnId`, `ThingId`, `CellRef(x,y,z)`) and tick-boundary intent consumption suit the determinism-first tick model already decided in Phase 1 Q6 — intents are also the natural record for deterministic replay tests.
- The UI plan's three deferred dev-machine measurements (UI Toolkit dense-grid build, overlay slice meshing, headless `Odyssey.Tests.Ui`) will be runnable here once its branch merges; this machine has the editor and the packs.

## Division of labour (restated)

UI and information design — HUD, panels, input, icons, the mockup — is **owned by the UI session**; this line of work owns simulation, world, assets and the architecture benchmark, and will not create or edit `docs/design/09-ui-and-input.md`, `10-ui-panel-catalogue.md`, `g-*` research files or the mockup.
