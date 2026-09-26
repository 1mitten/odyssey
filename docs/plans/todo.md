# To do

Issues found in the game that nobody has fixed yet: each one noticed, measured or read in the code,
with where the evidence is. Not a feature list — features go through `docs/process.md` and a design
document. **Take a row, fix it, and move it to *Done* with the commit or PR.** A new finding goes in
as a row the day it is found; that is what this file is for. Ordered by what a player meets first.

Started 2026-09-26 from the Almanac rewrite (design 64 §5), whose five fact-gathering passes read
the whole game against the Defs and found as much outside the Almanac as in it.

## Bugs — the game does something it should not

| # | Issue | Evidence | What a player sees | Fix, and its size |
|---|---|---|---|---|
| 1 | **The pace readout leaves out injuries.** `PaceModel.PerMille` multiplies rolled × condition × weather × urgency; the move rate also multiplies by the Moving capacity. | `Assets/Odyssey/Hud/PaceModel.cs:84`, the published factors in `Sim/Pawns/PawnRegistry.cs:746`, against `Sim/Pawns/Pawn.cs:720-727` (`HealthMovingPerMille`) | An injured colonist's *Pace 100%* while she limps at 60%. The readout's own claim is that its factors multiply back to the move rate exactly. | Publish Moving as a fifth pace aspect with a `ui.stat.pace.*` key, include it in `PerMille` and the tooltip, and add a test that the factors multiply back to `MoveRatePerMille` for a hurt pawn. Small; no golden should move (aspects are unhashed). |
| 2 | **The quality roll is cut off above the middle.** `QualityContent.Roll` draws `NextInt(9)`, but the bell's weights sum past 9 whenever the centre has neighbours on both sides (13 at skill 5–14, 9 only at the ends). | `Assets/Odyssey/Sim/Construction/QualityContent.cs:84-101` — its own comment says "nine weights whatever the skill … a master can never fall to Poor" | At skill 10–14 a bed is Poor 1 in 9, Normal 3, Decent 5, and **never** Uber or Epic; Epic is possible only at skill 20. The odds lean to worse than the design meant. | Draw from the sum of the weights (or clamp the bell at the ends so it is nine). One line, plus a test on the tier distribution per skill band. **Moves any golden that builds a bed** — measure with `GoldenColonyProbe`. The Almanac's bed page quotes today's odds (design 64) and changes with it. |
| 3 | **`WeaponSheathGapTests.EverySheathedWeaponSitsAgainstTheHip` fails** on this machine: `Character_MilitaryMale_01`, bat, +10 frames, nearest point 3.2 cm off the body against 0.8–3.0 cm. | EditMode run 2026-09-26 on `claude/almanac-refresh` (twice); the test is `Assets/Odyssey/Presentation/Tests/WeaponSheathGapTests.cs:144`, last changed `85ed339c` | A bat on one body hangs a hair off the hip. | First check `main` fails the same way (the Almanac branch touches no weapon art). If it does, it is a fit for design 33 §9c's tolerance or that rig's offset. Small. |

## Gaps — the game has half of something

| # | Issue | Evidence | Why it matters | Next step |
|---|---|---|---|---|
| 4 | **A cooked meal with meat cannot be made.** The kitchen makes `Item_CookedMeal` only when an ingredient has `meat`, and no item does, so every cook makes a vegetable meal. | `Sim/Cooking/Kitchen.cs:394,433`; no `<meat>` in `Defs/Core/Pawns/Items.xml` | Harmless today (both are 900 nutrition and +50 mood), but the *Meal* in the Inventory, the stores' filters and the Almanac can never appear. | Arrives with meat (plan `cooking.md` K3). As of 2026-09-26 `claude/pig-butcher` has no `<meat>` item either, so it does not close this by itself. |
| 5 | **Iron ore and coal are mined and used for nothing.** No building, recipe or fuel reads either. | `Defs/Core/World/Buildings.xml` (the generator's `fuelItem` is wood), `Recipes.xml` (one recipe) | A player digs them expecting a use; they fill stores. | A design decision: smelting, coal as generator fuel, or leave them out of the mining yield until then. |
| 6 | **Recreation is simulated and never shown.** It rises while a colonist is idle, falls while she works, and moves mood by −200 to +100. | `Defs/Core/Pawns/Needs.xml:211-218`; the pane shows Food, Rest and Mood only (`Presentation/Ui/HudShell.Inspect.cs:846-848`) | A colonist's mood can fall for a reason the player cannot see. | Add a Recreation bar to the Needs tab (the registry key `ui.need.joy` exists). Small. |
| 7 | **Cold, heat and hunger never kill.** Severity takes up to 30% off condition, floored at 70%. | `Sim/Pawns/Pawn.cs:813,837-844`; design 43 §14d | Rime is a nuisance, not a danger. Already in `CLAUDE.md` Known gaps; listed here so the lethality unit has a row. | The health milestone's next unit. |
| 8 | **The wiki describes unbuilt things as if they were in the game.** `ui.need.comfort`, `beauty`, `space`, `outdoors`, `hygiene`; the category tooltips promising turrets, lighting, storage and allowed areas; `ui.work.patient` and `bedrest`. | `docs/design/icon-keys.csv`; found by the Almanac passes (design 64 §5) | The owner reads the wiki to learn what is in the game. The Almanac never shows these. | Mark planned rows in the wiki (the milestone column is there to drive it), rather than rewording design intent. Small, tooling only. |

## Art

| # | Issue | Evidence | Next step |
|---|---|---|---|
| 9 | **Six of the eight icon sheets were never committed** (01–05, 07, 08); 26 Almanac pages and about 130 other keys are mapped to them. | `art-source/icons/sheets/README.md`, `docs/design/icon-map.csv` | The owner copies the PNGs in and runs `python3 tools/icons/icons.py export`. Their art then replaces the line icons everywhere at once (`IconGlyphs`, design 64 §2a). |
| 10 | **Eight keys on sheet 06, which is committed, are marked *shared* and not exported**: door, growing zone, three work types, blood loss, raid, rain. | `docs/design/icon-map.csv` | Export them and judge whether each shared tile reads right for its key. |
| 11 | **57 keys have no art source at all.** About 45 are 3D things (trees, animals, people, weapons, buildings) that could be rendered in the game; about 12 are ideas (weather, health, events) to draw. | design 64 §4 | A `ThingStudio` beside `PortraitStudio` for the first (never committed — a Synty render is still Synty); a Claude Design brief for the rest. |

## Stale comments

| # | Where | What |
|---|---|---|
| 12 | `Defs/Core/Pawns/Items.xml:86` | Carrots "Stacks to 40"; the Def says 75. |
| 13 | `Sim/Construction/QualityContent.cs:75-82` | Goes with row 2: the comment describes the roll the code does not do. |

## Done

| # | Issue | Fixed by |
|---|---|---|
| — | The Almanac was the mock-up's text; thirty registry descriptions were false; the pane and the Almanac drew different pictures of one thing | `claude/almanac-refresh`, design 64 |
