# Forest animals — the plan

**Status (2026-09-26): written, every open question answered (§6), waiting for approval to start FA1. Nothing is built.** Branch
`claude/forest-animals`, worktree `D:\code\odyssey-forest`. The interview is
`docs/research/forest-animals-interview.md` (sixteen answers); research `e-15`, `a-20`,
`b-wild-animal-behaviour`; designs **64** temperament, **65** predators, **66** art and far form,
**67** signatures. The designs hold the mechanics and the numbers; this file holds the order, the
seams between them, the gates and the measurements.

Nine species from Synty's SIMPLE Forest Animals — rabbit, deer (doe, stag), fox, raccoon, skunk,
boar, moose (cow, bull), wolf, bear — at life size against a colonist, flavoured names, five visible
temperament rungs, predators that hunt prey and may kill a downed colonist after an hour's rescue
window, four signature behaviours, activity hours, 32/48/64/80 animals by board, a baked far form.
**Three PRs, each played before the next starts** (answer 16).

## 0. Before any of it

| Step | Why |
|---|---|
| **Merge #248 (the butcher) first**, or agree that it rebases on us | It takes kinds 6–9 and species 4–7 and adds `CanDrawKind` and a per-kind look table; appending after it is one rebase, racing it is three hand-kept tables in conflict. This plan assumes kinds **10–18** and species **8–16**. |
| **Import the pack once, in the main checkout** (design 66 §2) | Every worktree's `Assets/Synty` is a junction to `D:\code\odyssey\Assets\Synty`. The import must not run while an editor holds that checkout. |
| ~~The owner answers §6 below and approves the names~~ **Done 2026-09-26** | Names are content: they go in the CSVs in FA1's first content commit. |

## 1. Seams between the four designs — decided here

| Seam | Decision | Lands in |
|---|---|---|
| **Who derives a deer's or moose's form** (66 §4 says 64 owns it; 64 does not) | The **simulation** derives it: `Pawn.Form` is a pure function of `RollSeed` and the species' `formCount`, never saved, published as the `form` aspect. The rut (67) and the art (66) both read it; there is one owner. | FA1 |
| **The raccoon's traverse mode** (67 §7 needs `Animal`) | `TraverseMode.Animal`: it cannot open a door, so a closed door keeps a raid out. The rat stays `Climber`. | FA1 |
| **Predators startle prey** (65 §3b) | Design 64's noticing scan takes a *threat* set: people (and, per §6 Q1, bandits) for every species, **plus any predator whose prey limit covers it**. One scan, one list. | FA2 |
| **`bodySizePerMille`** (65 needs it for prey, 64 for the herd and the scan) | Added in FA1 with the species, so FA2 adds no field to a Def that is already content-pinned. | FA1 |
| **Meat and hide yields** (answer 15, "stats now") | Added in FA1 as data, read by nothing until K3, pinned by the content fingerprint. This departs from the `SpeciesDef` comment "not here until something reads them" on the owner's word; the comment is amended to say so. | FA1 |
| **The wildlife table** — nobody designed it | §2 of this plan. | FA1 |
| **The Eat clip's driver** (66 §6, used by 65's `Job_Feed` and 67's `Job_Graze`) | One aspect, `grazing`, published for *any* eating job an animal does; the name stays although a wolf is feeding. FA1 lays the plumbing and a test; FA2 and FA3 set it. | FA1 |
| **Rage duration** (64: 10–26 k, research: 10 k min, 18 k mean) | 64's range stands; the mean is 18 k. | FA2 |

## 2. The wildlife table (FA1)

| Change | Detail |
|---|---|
| **Ceiling per board** | `wildlifeCeiling` becomes Small 32, Standard 48, Large 64, Huge 80 (answer 13), chosen in `ColonyWorld.DefFor`'s path so a test reaches it through `PlayedMap`. The *city* keeps 24. |
| **Density** | Raised so each board reaches its ceiling on the played seed: on Standard, 6,354 reachable columns need ~76 per ten thousand. Measured on all four boards, not computed. |
| **Seeder draws** | `WildlifeSeeder`'s 64-draw cap and 24-cell group spacing are re-checked at 80 animals and twelve entries; a test asserts every entry with a habitat on the board is seeded at least once on the played seed. |
| **Habitats** | One new: **Open** — grass not within 2 cells of a tree (rabbit). Existing: Woodland (deer, fox, skunk, boar, wolf, bear), Bank (raccoon, moose), Rock (bear, rat). A species may list two habitats; the seeder picks by weight. |
| **Weights and groups** | From design 64's table and the interview's §4: rabbit 1–3, deer 3–6, fox 1, raccoon 1–2, skunk 1, boar 3–5, moose 1–3, wolf 2–4, bear 1. Wolves and bears are placed outside a larger clearing radius than the rest. The hog leaves the meadow table (answer 2) and stays in the city's. |

## 3. FA1 — art and roster (PR 1)

The animals exist, wander as today, are drawn at life size in their colourways and forms, and are
named. **No new behaviour.**

| # | Unit | Design | Tests first |
|---|---|---|---|
| FA1.1 | **Import**: the package moved into `Assets/Synty/SimpleForestAnimal/` in the same run; `.gitignore` gains the install path; the catalogue build refuses while `Assets/SimpleForestAnimal/` exists | 66 §2 | an editor test of the refusal |
| FA1.2 | **Species and kinds**: nine `SpeciesDef`s, nine `PawnKindDef`s, appended; `formCount`, `bodySizePerMille`, yields; `Pawn.Form`; the three kind tables together; the raccoon `Animal` | 66 §4, §1 above | `PawnContentDefTests` fingerprint; `KindTablesAgree` (the three tables); `FormIsAPureFunctionOfTheSeed`; `CombatContractTests` count |
| FA1.3 | **Wildlife table** | §2 above | `EveryHabitatEntryIsSeeded`; `TheCeilingIsTheBoards` on four boards; density measured |
| FA1.4 | **Catalogue rows, scale, clips**: rows per form × colourway named by path; scale from `AnimalProbe.ShootForest`; walk and run speeds measured from the planted foot; Eat looped; `grazing` aspect plumbing | 66 §5, §6, §10 | `AnimalLooks.Colourway` in the fast tier; `CanDrawKind` per kind; the stand-in box when art is missing |
| FA1.5 | **Content**: names in `proper-nouns.csv` and `icon-keys.csv`, debug Spawn rows, Almanac Fauna entries, Animals tab labels | interview §8 | `TheAlmanacsFaunaAreTheAnimalsInTheGame` (twelve); `RegistryTests` |
| FA1.6 | **Stale lines**: the frog's "kind 4", design 30's density and "nothing flees", design 29's hog pace, `WildlifeSystem`'s "no death" | ground | — |
| FA1.7 | **Measure and bake**: goldens re-baked and `GoldenColonyProbe` against `main` (expect animal positions only); `FrameTimeTests.TheAnimalsAgainstTheFrame` at 0/48/80 animals, in one run, **the baseline FA2's far form is judged against** | 66 §9 | the frame arm itself |

**Gate**: fast tier, Long tier, Unity EditMode and PlayMode (alone on the machine), a player build
booted into a colony, the three content checks. **Playtest**: are they the right size beside a
colonist, can the three smallest be told apart at the play camera (66 §12 Q2), does the meadow feel
populated at 48.

## 4. FA2 — temperament, predators, the far form (PR 2)

Built in this order; each step a commit, the goldens re-baked once at the end.

| # | Unit | Design |
|---|---|---|
| FA2.1 | Temperament fields and the one reader; the rung derived, never stored; the test that no other code reads the fields | 64 §3 |
| FA2.2 | `AnimalNoticeSystem`: the staggered 30-tick scan, the threat set (people, predators) | 64 §4, §1 above |
| FA2.3 | Freeze, bolt, stand, warn; the charge and its bluff; the herd copying its neighbour | 64 §5–§6 |
| FA2.4 | Revenge ×3 in reach; rage; the cornered | 64 §7 |
| FA2.5 | A colonist gives way: back off, then path round through the pather's unused cost hook | 64 §8 |
| FA2.6 | Hunger for predators on the food need; prey by size and reach; the hunt on the melee job; the pack | 65 §2–§3 |
| FA2.7 | Feeding and the kill left (`EatenPerMille`); `Job_Feed` with the Eat clip | 65 §4 |
| FA2.8 | `WorldRules` and `odyssey.rules`; the man-eater's stalk and charge | 65 §5 |
| FA2.9 | **The kill window**: `Job_Maul`, driven off, the rewording of design 33 §3 and the CLAUDE.md combat row | 65 §6 |
| FA2.10 | Alerts `stalked` and `mauled`; the `alert-maul` sound hook | 65 §7 |
| FA2.11 | The computed motions the rungs need: stamp, rear, tail-up, lunge, bluff stop | 66 §6 (list) |
| FA2.12 | **The far form**: animals in the baked instanced pass, one bucket per form × colourway; closes design 29 §8a | 66 §8 |
| FA2.13 | The rung and the statuses in words on the pane and in the Almanac | 64 §9 |
| FA2.14 | Measure: `WildlifeLayerCost` (Long) under **0.05 ms a tick** at 48 animals and 50 colonists; the frame arm again, against FA1's baseline; goldens and the probe | 64 §11, 65 §8, 66 §9 |

**Playtest**: can you tell a species' temperament before you meet it, does a warning read before a
charge, does a colonist giving way look like caution or like a bug, is the hour's window winnable,
do wolves hunting deer read as a hunt.

## 5. FA3 — signatures and hours (PR 3)

Design 67 §13's order: activity hours (conversion first, with a test that the hog, rat and frog
hash identically), then winter away; skunk spray; the rut; grazing and the Eat clip; raids and the
ledger line; registry, wiki, Almanac, goldens, `SignaturesCost` (Measurement, target **0.01 ms**).

**Playtest**: is the spray funny or merely a penalty, is the rut visible before it bites, does
grazing make you wall a field, is a raccoon raid noticed.

## 6. Decided with the owner

From the four designs. **Answered 2026-09-26: the owner took every recommendation**, so each
design's recommended branch is now its decision.

| # | From | Question | Decided |
|---|---|---|---|
| 1 | 64 §12 | Do bandits startle animals, and can an animal charge a bandit? | Yes to both; a bandit never gives way |
| 2 | 64 §12 | The *Warning* floater on every display, or only one facing a colonist? | Only a colonist's |
| 3 | 65 §10 | Does a man-eater feed on the colonist it kills? | Yes, and her body is unchanged |
| 4 | 65 §10 | Does carrying her away drive the predator off? | Yes |
| 5 | 65 §10 | Wolf prey limit 1,300 so wolves take deer (research backed 1,000)? | 1,300 |
| 6 | 67 §14 | The rut month (the calendar has no autumn) | Ember, the month before Rime |
| 7 | 67 §14 | The bear's winter: off the board, or a den on it? | Off the board; a den later if winter feels empty |
| 8 | 67 §14 | A grazed crop: uprooted, or set back a stage? | Uprooted |
| 9 | 67 §14 | Does the skunk smell reach the sprayed colonist's neighbours? | Yes, −10 within 2 cells |
| 10 | 67 §14 | Does a raccoon's haul leave the board? | Yes, the food is lost |
| — | 66 §12 | The size factor for the hog and rat; the smallest three at the play camera | **Not asked**: decided from the probe sheet and FA1's first look |

## 7. What the plan does not cover

Taming, pens, breeding and young (so no protective mother), hunting and butchering (K3), rabies,
dens, corpses rotting or being scavenged, a manhunter-pack incident, swimming, animals drawn on the
city board beyond the rat and hog, and the reference's temperature ranges per species.
