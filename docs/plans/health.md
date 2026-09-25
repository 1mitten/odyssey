# Health — the plan

**Designed 2026-09-25**, `docs/design/43-health.md`, from the interview in
`docs/research/health-interview.md` (four answers). **Approved and built the same day** (owner:
*"implement it"*): H1–H6 on `claude/relaxed-heisenberg-zxy63b`, PR 1mitten/odyssey#213. H5 was
built to the brief's own content ahead of the mockups; design 43 §14 records every departure.
The shape follows `docs/plans/combat.md`: one contracts step first, because the handles it
claims are save contracts; goldens moved once, measured; the fast tier for every lane and one
integrator for Unity.

## Units

| Unit | What | Checkpoint | Goldens | State |
|---|---|---|---|---|
| H0 | Research `a-02-health.md` (done 2026-09-16), the interview, design 43, this plan, the brief | — | — | **done 2026-09-25** |
| H1 | **The body and the ledger**: `BodyDef` and the person's six regions; `AfflictionDef` (wound, bruise, fracture); the affliction record, merged by (region, kind); `ApplyDamage` extracted from `ApplySwing` as the one owner of damage, the region roll from the resolver's stream, overflow to the torso; head or torso at zero is death; pain, the three capacities, pain shock and the downed line; the pool invariant asserted across the whole combat gate; `odyssey.health` section and the hash under `HasHealthState`; the rate seam filled. Soak numbers before and after (design 43 §3) recorded in the doc | fast + Long tiers; `BanditSoakTests.TheGateWithRaids` downs and deaths per seed, before and after, in design 43 §3 | **none expected** — hashed only while hurt; `GoldenColonyProbe` proves it | **built** — commit d94e220 |
| H2 | **Bleeding and blood**: the bleed per record, blood loss on the pawn, the stages, death by blood loss through `Kill`, recovery, the hours-to-death aspect; the fourteen sparse aspects and `AfflictionView`, with `HealthAspects` / `HealthAspectNames` checked each side | fast tier; a test that a skill-0 bare-handed tend stops a 30-point bleed that would otherwise kill in 13 hours | none | **built** — commit d94e220 |
| H3 | **Tending**: `Work_Doctor` (a new work handle: claimed here, once), `Skill_Medicine` live on the SK5 pattern, the giver (untended first bleeding, nearest, reachable, never self), `Job_Tend` with the medkit fetched by the builder's path, quality = skill × potency clamped, tend speed by skill, healing +4..+12 anywhere; `ui.status.tending` on the line | fast tier; `WorkPriorityEffectTests`-style proof that a Doctor priority changes who tends | **moves every golden** (a work type and a skill each add a hashed array); re-baked once, measured by the probe to be their counters alone | **built** — commit d4b62f2 |
| H4 | **Fall damage**: `FallDamageBase` 15, `FallDamageExponent` 1.5, the split over the bottom-facing regions with ±20 %, the fracture at two layers, each landing of a cascade on its own height, the three `Falling.PawnsOutOf` call sites through `ApplyDamage`; `Thought_Fell` removed | fast tier; the five-row table of `a-02:93-99` reproduced as a test with a negative control (a fall of zero layers) | none expected (no golden collapses a floor); probe-checked | **built** — commit d4b62f2 |
| H5 | **The Health tab** as Claude Design returned it: two columns on the Skills grid, the region click, the marks as drawn glyphs, the Condition word and the weapon moved to the header line; the six keys the brief flagged added to `icon-keys.csv`, the wiki and `Registry.g.cs` rebuilt in the same commit; `InspectTabBody` moved only if the mockup said a number | EditMode: `CombatPaneTests` grown to the six states; `HudFontTests`, `HudStyleSheetTests`, `TheFixedBodyIsTheTallestLiveTab` green; PlayMode: the pane laid out at 1920 × 1080 like `DockedTabGeometryTests` | none (views are unhashed) | **built** (model tested; view type-checked against stubs, no Unity run) — d4b62f2 |
| H6 | **The edges**: alerts `injured` and `nomedicine`; debug rows Hurt, Heal, Kill; the right-click Tend row, and Rescue into the menu **only if the owner says so** | EditMode | none | **built** — d4b62f2; Rescue left an instant order |
| H7 | **The gate**: ten days on the three combat seeds with raids, every H1 invariant hourly, a save mid-tend resuming the same, the hash twin; `docs/milestones/health-report.md`; the playtest-queue rows | Long tier; Unity tiers | none | **partly**: fast and Long tiers green, soak measured (design 43 §14b); Unity tiers and the milestone report owed |

Order: H1 → H2 → H3 and H4 in parallel → H5 when the mockups are back → H6 → H7. H1 is the
contracts step: it claims no handle (the work type and the skill are H3's, and H3 claims them
together, once) but it owns the spine edits (`Pawn.cs`, `CombatSystem.Apply.cs`, the save
components list, `Views.cs`), so nothing runs beside it.

## Blocked on the owner

- **A Unity run**: both tiers and a player build. Nothing here compiled the Presentation half.
- **The mockups** from Claude Design, to replace or confirm the tab as built; and the height
  decision if the returned tab needs more than 157 px.
- **The fight's new balance** (design 43 §14b).
- **The soak numbers' verdict** (design 43 §3): pain shock on, or off, once both are on record.
- **Whether a hurt colonist out of bed and untended heals** (design 43 §12).
- **Whether Rescue moves into the right-click menu** (H6).
