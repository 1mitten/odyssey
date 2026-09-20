# Growing zones — execution plan (U46–U50)

Approved 2026-09-18. Design: `docs/design/22-growing.md`. Research:
`docs/research/a-08-plants-growing-food.md` + its 2026-09-18 follow-up. Branch
`claude/growing-zones`, worktree `D:\code\odyssey-growing` (pack-less: no `Assets/Synty`
junction — fallback art paths are under test by construction). Work reaches `main` only through
a PR with both tiers green.

Scope, from the interview: one crop (carrot, raw food), continuous zones, daylight-window growth
with a minimum-fertility siting gate, minimal v1 (§8 of the design holds the hooks). Performant
by shape: O(planted) growth on the Rare group, three re-meshes per crop lifetime, sparse
snapshot channels.

## U46 — Sim: content, zones, growth

- `Catalogue.cs`: `PlantHandle.Carrot` (Count 1), `ItemHandle.Carrots`, `JobHandle.Sow/Harvest`;
  `Intents.cs`: `DesignateZone` (A = plant handle), `CancelZone`.
- `Sim/Growing/PlantDef.cs` + `Defs/Core/World/Plants.xml` (`Plant_Carrot`: growTicks 130,000,
  sowWorkTicks 170, harvestWorkTicks 200, yield 5, minFertility 70, three stage module ids);
  register in `WorldContent`, accessor `ContentPack.Plants()`, `Forget()` in `Reset()`.
- `Sim/Growing/GrowingZones.cs`: zone records + parallel crop arrays; designate/merge/cancel;
  `ISaveable` "odyssey.zones", `IStateHashable`, `ISnapshotContributor` (sparse zone channel +
  plant channel); wired in `ColonyComposition`/`ColonyWorld` save order after designations;
  `PawnContext.Growing` seam (null in bare fixtures).
- `Sim/Growing/PlantGrowthSystem.cs`: Rare group, +250 growing-window ticks per run inside
  15,000–47,500, `MarkDirty` on stage-bucket change only.
- `Items.xml` Carrots (nutrition 180, stackLimit 40); verify stockpile `Allow` arrays size with
  the new def.
- Wiki: `icon-keys.csv` rows (carrot plant, carrots item); both `--check` gates.
- **Tests:** Def load + fingerprints (new `WorldContentDefTests` pin; `PawnContentDefTests`
  re-baked); designate/merge/cancel/dissolve; fertility/water/roof refusal; save/load
  round-trip; hash determinism.

Gate: `scripts/test-fast.sh` green.

## U47 — Sim: jobs

- `WorkTypeIndex.Growing` + `WorkTypes.xml` (order 4) + `Skills.xml` `Skill_Growing` +
  `FromDefs` lists; `Jobs.xml` `Job_Sow`/`Job_Harvest` (no `workTicks` — priced per plant, the
  mining precedent; settleTicks ≥ 27).
- Pooled `SowJobDriver`/`HarvestJobDriver` (walk into the cell → swing → deferred structural
  write: plant at 0 / spawn yield by `NearestCellWithSpace` + clear).
- `SowWorkGiver` (unplanted zone cells, siting still valid) and `HarvestWorkGiver` (growth
  ≥ growTicks), auto-discovered, cell reservation.
- Cancel tool: `CancelZone` as third per-cell intent (zone first, then designation).
- **Tests:** sow → mature (tick through daylight windows) → harvest spawns 5 → auto re-sow;
  thinker eats carrots; hauler stocks them; zone cancel mid-growth removes crops; ten-day
  determinism on the three standard seeds unchanged.

Gate: fast tier green; nothing in presentation touched.

## U48 — Presentation: crops and the zone tint

- `ModuleCatalogue`: three carrot stage entries — Farm pack `_S/_M/_L` refs, primitive
  fallbacks (this worktree compiles against the fallback by default).
- Crop emission per species × stage bucket in `ChunkMesher`/`WorldRenderModel`; stage change is
  the only re-mesh.
- Zone overlay: per-cell green tint via the cell-shade span path from the sparse snapshot
  channel; added to the colour-guard test. Crisp-border region shader (stockpiles included) is
  a separate future unit — not this one.
- **Tests:** Unity editmode (fast tier compiles neither Presentation nor Editor) — bucket
  emission, overlay colour, no-asset fallback.

Gate: `scripts/unity.sh test editmode` on this worktree.

## U49 — UI: tool, picker, hotkey

- Build palette: Zones category → Growing zone tool; plant picker via the palette's sub-type
  chooser (carrot only, honestly labelled).
- Orders strip: fifth pinned button (`PaletteTools.Pinned`, `Live` row, `HudTheme` pinned-action
  hue); armed banner through `Registry.Label` — no literals in the six locked namespaces.
- Hotkey `G` = `ToolGrowZone` (`ui.keys.growzone` CSV row; `HotkeyClashTests` clean).
- `RegistryTests`, `JobLabels.IconKeys`, both `--check` gates green.

Gate: Unity editmode green again (HUD shell compiles only there).

## U50 — Gate and closure

- Fast tier green throughout; **`scripts/unity.sh test editmode` authoritative**; PlayMode
  `FrameTimeTests` with a 2,000-cell zone + mature field inside the 5 ms budget.
- Ten-day headless: three standard seeds one hash each (unchanged); **new** growing scenario —
  zone sown at tick 0, colony fed partly from crops by day 10 (OQ-39's first producer).
- Wiki rebuilt, `artifact.html` republished; `AGENTS.md` status (main checkout — file is
  untracked), `docs/journal.md` entry; stop for review; PR with both tiers.

## Sequencing notes

- Write-down landed before code (this file, `22-growing.md`, the research follow-up) — one
  commit, then U46 in small single-concern commits.
- Content commits carry regenerated wiki + labels; fingerprints re-baked deliberately, one line
  each, with the reason in the commit message.
- Presentation and UI compile only under Unity — do not trust a green fast tier for U48/U49
  work; the Unity tier is the gate for anything past `Odyssey.Sim`.
