# Interview — the health system's basics, and the Health tab to be designed in Claude Design

**Phase 1, 2026-09-25.** Owner: *"Can we plan out the health system - at least some basics so we
can work out a ui to inform claude design about - do the research based on whats in the repo."*
The research came from the repo (three read-only sweeps of the simulation, the documents and the
HUD); the four questions below were asked with a recommendation each and answered in one round.
The decisions are `docs/design/43-health.md`; the brief is
`docs/reference/mockups/health-tab-brief.md`; the units are `docs/plans/health.md`.

## Ground

| Fact | Where |
|---|---|
| **The brief puts health in M6** ("health and injuries, 3D cover and line of sight, first raid"); design 17 says M4 in three places and nobody reconciled them. Lane A's research question for it names "body-part hierarchy, injuries, bleeding, infections, diseases, chronic conditions, capacities derived from parts, tending, medicine quality, surgery, prosthetics". | `docs/brief.md:83, :207`; `17-rates-and-stats.md` §4a, §4c, §4e |
| **A hit-point pool exists.** One integer per pawn in thousandths (`HpMilli`, `HpMaxMilli` = `SpeciesDef.healthPoints` × 1000: person 100, hog 60, rat 15), **downed at 0, dead at −50 %** (`deathAtPerMille`). One place loses it, `CombatSystem.ApplySwing`; `Down`, `Recover`, `Kill` beside it; hooks `DamageApplied` / `Downed` / `Died`, and a death that does not raise `Died` is not mourned. A colonist heals **only in a bed** at 20 points a day and gets up whole; an animal heals anywhere and gets up at 15 %; a bandit never heals. Rescue carries the downed to a bed. Saved in `odyssey.combat`, hashed under `HasCombatState` so a colony that never fought is unchanged. | `Assets/Odyssey/Sim/Pawns/Pawn.cs:223-236`, `Combat/CombatSystem.Apply.cs`, `CombatSystem.cs:149`, `CombatDef.cs`, `Saving/CombatSection.cs`; `docs/design/33-combat.md` §1, §3, §5, §11 |
| **No body, no bleeding, no tending.** `DamageKind { Blunt, Sharp }` carries the comment "bleeding waits for a body". Blood on the ground is presentation only. `Skill_Medicine` has a registry row and a disabled Skills-tab row ("wounds heal in bed, untreated") and no Def. | `CombatDef.cs:8`, `Hud/BloodModel.cs`, `Hud/SkillCatalogue.cs:123` |
| **Two severity bars slow work and never hurt anyone.** `StarvationSeverity` 0..1000 (saved, **not hashed**) and `TemperatureSeverity` ±1000 (saved, hashed) each step `ConditionPerMille` down by 100/200/300 with a floor of 700, whose comment says the floor "should give way to a threshold on the day health exists". Design 28 promised "real lethality arriving with M6 health". | `Pawn.cs:356, :373, :720-730`; `28-temperature.md` §8 |
| **The rate seam has a slot waiting.** `MoveRatePerMille` says "load and health to arrive later in the same product"; design 17 §4a fixes the order innate × condition × load × health and §4e says health "multiplies in at `health` … and nothing else in either feature changes". `WorkAspects.Capable[w]` is always 1: "no traits and no health model". | `Pawn.cs:634, :660`; `PawnRegistry.cs:538` |
| **Falls move a pawn and hand out a memory.** `Falling.PawnsOutOf` is called from the support solver (with `Thought_Fell`, "stands in for an injury that cannot exist yet"), from mining and from the construction grid. `a-02` has the number: `fallDamage(n) = round(15 × n^1.5)` blunt, one layer bruises, three down, five kill, with a single-pool fallback. | `Sim/Pawns/Falling.cs:77`; `SupportSystem.cs:133`, `MineJob.cs:400`, `ConstructionGrid.cs:1483`; `docs/research/a-02-health.md:91-105` |
| **The research is done.** `a-02-health.md` (2026-09-16) has the part tree with HP, the eleven capacities and the consciousness formula, pain at 1.25 % per HP with shock at 80 %, bleeding (any tend stops it; 33 %/day back), the heal-rate table (8 base, +8 bed, +4–12 tend), tend quality (skill × potency, clamped by medicine), infection, and the downed and dead rules. It could not determine tend duration or the bleed-to-blood-loss formula. | `docs/research/a-02-health.md` |
| **The registry already names the vocabulary.** 28 `ui.health.*` keys, all M6: twenty parts (brain … foot) and eight conditions (blood loss, scar, wound, burn, fracture, missing part, implant, anaesthetic). Also `ui.status.{downed,bleeding,tending,rescuing}`, `ui.combat.{health,hurt,unhurt,condition,weapon,barehands}`, `ui.work.{doctor,rescue,patient}` (all three among the Work tab's 22 drawn columns), `ui.command.{rescue,tend}`, `ui.alert.{injured,nomedicine,infection,norescuebed}`, `ui.skill.medicine`, `ui.res.{medicine,medkit}`. Sheet art exists for head, torso, arm, leg, blood, wound and fracture; ear, nose, neck, shoulder, finger, foot, scar, missing, implant and anaesthetic are gaps. | `docs/design/icon-keys.csv:385-412`, `icon-map.csv:120-147` |
| **A Health tab is live on the colonist pane** since combat C2: "73 / 100" rounded up, a bar in the need bars' colours, a Condition word (Unhurt, Hurt, Stunned, Downed) and the weapon row "until the Gear tab ships". Its body is `HudShell.Combat.cs`; its model `InspectModel.RefreshHealth`; its tests `CombatPaneTests`. Bandits get no tab; an animal has no tabs at all. | `33-combat.md` §6C, §9d; `Hud/InspectModel.cs:535`; `Presentation/Ui/HudShell.Combat.cs` |
| **The pane is a fixed box.** 560 wide; the tab body is **157 px**, derived as the taller of the needs grid and the skills grid (seven 19-px rows with 4-px gaps in two 256-px columns: icon 17, name 95, bar 72 × 6, level, passion mark). The doc says the disabled tabs "will move this number when they arrive, once". **The HUD never scrolls.** `HudStyleSheetTests` ties the USS to the constants; `TheFixedBodyIsTheTallestLiveTab` and `TheInspectPaneNeverReachesTheCommandBar` bound it. | `Hud/HudLayout.cs:1068-1082`; `14-hud-layout.md:521-527`; `Tests/Hud/HudLayoutTests.cs` |
| **The panel catalogue's B1a** describes the Health sub-tab as "body-part hierarchy as a tree … the densest tree in the game and the main justification for a virtualising tree control", and there is no tree control. | `10-ui-panel-catalogue.md:457-464` |
| **Health cues elsewhere.** The roster card's 4 px health bar and downed plate (the card is at the coverage ceiling: no pixel to spare); the bar over a hurt pawn's head; floating words; the corpse pane. Alerts raise `norescuebed`; `injured`, `nomedicine` and `infection` are registered and raised by nothing. The right-click menu has one row (Equip) and a comment reserving Rescue as "a change to decide on at the keyboard". No tooltip system beyond UI Toolkit's own string. | `Hud/RosterModel.cs:218`, `HealthBarLayout.cs`, `AlertModel.cs:166`, `ContextMenuModel.cs:55-61` |
| **A Claude Design brief has a shape here**: the Animals tab's — a prompt handed over verbatim (what the tab is, content, states, the design system verbatim, deliverables, what not to do), then the attachments, the answers recorded, and what was built. | `docs/reference/mockups/animals-tab-brief.md` |
| **The debug menu has no hurt, heal or kill row**, on a reason written before the pool existed ("`Pawn` has no health, injury or downed model at all"). | `18-debug-menu.md:89-93` |
| **Open on the combat side, the owner's:** whether four armed colonists should lose to three bandits at the invented numbers; the Thoughts tab; kidnap; a damaged building's drawing. | `docs/milestones/combat-report.md` §5 |

## Questions

Each had a recommendation; the owner answered all four in one round.

**1. How detailed is the body?** Recommended **six regions** — head, torso, two arms, two legs —
each injury on a region, three derived capacities (consciousness, moving, manipulation), a Def
shape that grows to the full tree later without a save break. Alternatives: the pool plus a
condition list with no parts (cheapest; the tab is a list of words and a leg wound cannot slow
anyone specifically); the full RimWorld-shape tree of about forty parts and eleven capacities,
which needs a tree control the HUD does not have and a window of its own.

**2. Which mechanics, beyond injuries on the body?** Recommended **bleeding and tending** (sharp
wounds bleed, any tend stops it, a Doctor work type, a Tend command, a medicine item, heal rate
from bed and tend) and **fall damage** (wire `a-02`'s table into the three fall sites). Offered
and not recommended now: cold, heat and hunger becoming lethal through the same ledger (design
28's promise; moves goldens); infection and disease (the most work and the most new UI, and
`a-02` could not determine tend duration).

**3. Where does the brief reach?** Recommended **the Health tab only**, every state; the roster
bar and the over-head bar stay as they are. Offered: alert rows (Injured, Bleeding, No
medicine); right-click Tend and Rescue rows; a docked Medical window like the Animals tab.

**4. What does this unit deliver?** Recommended **the design doc and the brief**, the interview
recorded, no code until the mockups come back and the plan is approved. Offered: the same plus
the simulation ledger in parallel; the brief alone.

## Answers

**2026-09-25, owner:** six regions (1); bleeding and tending, and fall damage (2); the Health
tab only (3); design doc and brief now, no code (4). Every recommendation taken as offered.

## Two things the code corrected in the plan afterwards

- The plan proposed relabelling the Rescue work type as Doctor to avoid a 23rd Work-tab column.
  The 22 drawn columns **already include** `ui.work.doctor`, `ui.work.rescue` and `ui.work.patient`;
  tending lights the Doctor column that is drawn dim today, and Rescue keeps its own.
- The plan expected the Medicine skill to bump the save format. The Melee skill arrived with C2
  and **no format changed** (design 33 line 1076); Medicine follows the same path, and the
  affliction records are a keyed section of their own. Nothing in this line bumps the format.
