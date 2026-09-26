# The storyteller — the plan

The design is `docs/design/59-storyteller.md`, and the interview is
`docs/research/storyteller-interview.md` (2026-09-26). ST0 is documents only, on
`claude/sweet-cerf-wzfxgs`. **Phase gate: this plan waits for the owner's approval; no unit after
ST0 is started.** From ST1 on, the work goes one commit per unit on its own `claude/storyteller-*`
branch, with a PR per two or three units.

| Unit | What | Save / hash | Gate |
|---|---|---|---|
| **ST0** | The interview, `a-11-storyteller-pacing.md`, design 59, this plan, and notes in designs 23 §8, 55 §9/§14 and 03 §11 | — | docs only |
| **ST1** | The kit and the system (below) | new `odyssey.storyteller`, hashed only while set | fast tier; goldens untouched; the tuning harness |
| **ST2** | Colony strength and the remembered peak; `RaidBudget.AutoSize` from strength | derived, except the peak, which goes in ST1's section | fast tier; Long tier re-baselined |
| **ST3** | Tension | ST1's section | fast tier |
| **ST4** | Difficulty rungs and Custom | ST1's section | fast tier; the harness with big threats off |
| **ST5** | The setup page and the Settings > Gameplay rows | none | Hud tier; both content checks; Unity owed |
| **ST6** | The tension gauge, pause and jump, the old-save toast, a debug readout | none | Hud tier; `HudLayoutTests`; Unity owed |
| **ST7** | *Someone joins*: an Arrival incident; population intent goes live | per design 23's recipe | fast tier; content checks |

## ST1 — the kit and the system

- **New, in `Sim/Events/Storyteller/`:**
  - `StorytellerDef`: grace, generators, the population curve, the tension profile.
  - Three generator blocks: `OnOffCycle`, `MeanTimeBetween`, `RandomBag`.
  - `StorytellerContent` with an append-only `Order`.
  - `StorytellerState`: the storyteller and difficulty indices, the colony start tick, each
    generator's plan, the tension, the last cause, the peak.
  - `StorytellerDecision.Decide(...)`, the pure core.
  - `Storyteller`: `IWorldSystem`, `IStateHashable`, `ISaveable`, `ISnapshotContributor`. World
    phase, order 90, checks once a game hour.
  - `StorytellerPurpose`: new RNG constants on unused SHA-256 round constants. Grep before taking
    one.
- **Contracts:**
  - `StorytellerHandle` in `Catalogue.cs`, held to `Order` by a test.
  - `IntentKind.SetStoryteller`, appended last.
  - `ColonyRequest.Storyteller`, default −1.
- **Content:**
  - `Defs/Core/Events/Storytellers.xml` with the three Defs (design 59 §3b).
  - `Incident_Raid.minRefireDays` 4 → 2.
  - A `populationGain` flag on `IncidentDef`, false everywhere.
- **Wiring:**
  - `ColonyComposition.AddColony` (`AddSystem`, `AddSnapshotContributor`).
  - `ColonyWorld.SaveComponents`: append the section.
  - The raid's `<raid>` block validation is unchanged.
- **Tests:**
  - The content loads, and a bad generator block is a load error with a file and a line.
  - The same seed and Steady give the same hash after *N* hourly checks.
  - Saved mid-on-phase, the next fire tick and the hash match an uninterrupted run.
  - `Fireable = false` is never picked.
  - Each gate is honoured, with a negative control: the gate removed means it fires.
  - A category with nothing fireable loses its roll; the raid rate is unchanged when a stub
    ThreatSmall is added.
  - Population intent multiplies a stub `populationGain` incident's weight and nothing else.
  - **The goldens do not move** (the default is −1).
  - The tuning harness (design 59 §9) prints its table.
- **Risk.** Firing from the world phase rather than the intent phase: the raid schedules its
  arrivals from `Tick + 1`. One test fires the same raid from both and compares.

## ST2 — strength

- **`ColonyStrength.Of(PawnContext)`** and `RaiderStrength.Of(kind, mix)`, as design 59 §4a. The
  inputs are `Vitals.Of`, `IWeaponRules.ArmamentOf`, `WeaponQuality.DamagePerMille` and
  `AccuracyPerMille`, and `Pawn.SkillLevel` (Melee 5, Shooting 8).
- **The remembered peak** is updated in the hourly check.
- **`RaidBudget.AutoSize`** is re-signed to take strength, the tick, the params and a scale. The
  headcount fields on `RaidParams` become the ramp's.
- **Tests:**
  - Each input raises strength.
  - Negative controls: sandbags, walls, a tame animal and a stockpiled weapon leave it unchanged.
  - A downed colonist counts 0.
  - The peak outlives an hour of disarming.
  - Debug *Auto* equals the storyteller's size.
- **Re-baseline** `RaidTests.TheAutoSizeIsHeadcountAndDays` and every soak that fires an Auto raid,
  and say so in the commit.

## ST3 — tension

- **A `StorytellerCombatListener`**, registered in `CombatListeners.Register`, colonists only. Use
  only its tick argument: the hook runs inside a deferred removal.
- **The daily recovery** runs in the hourly check at the day boundary.
- **Tests:**
  - A colonist's death lowers tension; a raider's does not.
  - The change scales with colony size and with adaptation strength.
  - Tension, the cause and the tick survive a round trip.

## ST4 — difficulty

- **`DifficultyDef`**, six rungs (design 59 §6), plus Custom as a sentinel with four saved values.
- **`DifficultyHandle`**, and the intents `SetDifficulty` and `SetDifficultyValue`.
- **Tests:**
  - "Big threats off" gives none in 72 days.
  - The grace stretch moves the first fire.
  - Custom round-trips.
  - A mid-colony change applies from the next decision, and a storyteller change re-arms the
    generators without re-applying the grace.

## ST5 — setup and settings

- **Hud:**
  - `NewGameChoice` gains the storyteller and the difficulty.
  - New `StorytellerLabels` and `DifficultyLabels`, shaped like `RaidMixLabels`.
  - `SettingsDirector` gains two Gameplay rows (from the snapshot, by intent, greyed with no
    session) and a `PauseOnBigThreats` preference (`ISettingsStore`, default on, reset with the tab).
- **Presentation:**
  - `HudShell.Start.cs`: the picker, with the portrait and the blurb.
  - `HudShell.Settings.cs`: the rows.
  - `OdysseyBootstrap`: the choice goes on the `ColonyRequest`.
- **Registry** (`icon-keys.csv`, then both generators):
  - `ui.storyteller.steady`, `.calm`, `.chaotic` and `.none`, with each blurb as the description;
    the portraits are `icon-map.csv` gap rows;
  - `ui.difficulty.r0`–`r5` and `.custom`, and the four Custom field labels;
  - `ui.settings.storyteller`, `.difficulty` and `.threatpause`.
  - Add `ui.storyteller.*` and `ui.difficulty.*` to the `RegistryTests` literal lint: each is named
    on two surfaces.
- **Before this unit:** the Claude Design brief, `docs/reference/mockups/storyteller-brief.md`
  (2026-09-26), covers the picker, the Settings rows, the gauge and the placeholder portraits. What
  comes back sets ST5's and ST6's constants.

## ST6 — the gauge and the pause

- **Contracts:**
  - `StorytellerView` (storyteller, difficulty, band, cause, cause tick), published only while set.
  - `BulletinView.Category`.
- **Hud:**
  - `BulletinModel.ArrivedBigThreat` replaces the raid special case.
  - `TensionModel` builds the tooltip.
  - The old-save toast goes through the existing toast stack.
- **Presentation:**
  - The gauge glyph on `clock__line` (`HudShell.BuildRightColumn`).
  - Beside the chime: if the preference is on, set speed 0 and `Camera.JumpTo`.
- **Debug menu, Events tab:** the storyteller's next planned fire per generator, and *Fire next
  now*.
- **Registry:** `ui.tension.gauge`, `ui.tension.band.0`–`4`, `ui.tension.cause.death`, `.downed`,
  `.quiet`, `ui.alert.nostoryteller`.
- **Tests:**
  - The band comes from the simulation.
  - `HudLayoutTests.TheStripIsAlwaysOneRowAndNoFurther` still passes.
  - The glyph is drawn as a `HudGlyph` (`HudFontTests`).

## ST7 — someone joins

- **An Arrival incident marked `populationGain`**, built by design 23's recipe;
  `ui.bulletin.wanderer` already exists.
- **Population intent goes live.** Its own interview first, because a joiner is a person: who they
  are, and whether the player may refuse them.

## Also

- **`StorytellerSoakTests`** (Long tier, with ST1–ST3): 72 days per storyteller at Normal. It
  prints the per-season table beside the harness's.
- **Register the system in `TickBenchmarkTests`' busy arm.** It should cost nothing between hourly
  checks, and the number says so.

**Merge order.** ST1 → ST2 → ST3 → ST4, then ST5 and ST6 in parallel (one owner per file; ST6 owns
`HudShell.Panels.cs`), then ST7.

**Owed on the owner's machine.** The Unity EditMode and PlayMode tiers for ST5 and ST6, and a play
per the handover table. The first question a person at the keyboard can answer is whether a season
on Steady *feels* like three raids' worth of tension, or like too many.
