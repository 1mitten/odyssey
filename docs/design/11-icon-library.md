# 11 — The icon library

- **Status:** design. Written out of phase order alongside `09` and `10`, at the owner's request.
- **Governed by:** `docs/adr/0007-pixel-art-icon-pipeline.md` for the format and size contract.
- **Data:** `docs/design/icon-keys.csv` (the registry, 382 keys) and `docs/design/icon-map.csv`
  (the assignments, 268 with art and 114 gaps).

The owner owns eight pixel-art icon sheets, roughly six hundred cells, and has decided they are the
prototype's real HUD art. This file records what they cover, what they do not, and the item taxonomy
that fell out of reading them.

## 1. The art-led principle

The instruction was to align the game's items to the art rather than hunt for art to match an
invented list. That inverts the usual order, and it is the right way round for a prototype, so it is
recorded here as a principle rather than as a one-off:

> Where a concept could reasonably carry several names, **the art picks the name**. A commodity with
> no icon is a commodity with a worse name. Where the art is strong and we had planned no concept at
> all, that is a **content proposal**, not an automatic addition.

Two guard rails, because the principle is easy to over-apply. The direction of causation for the
**interface** stays keys-first: the HUD needs a cancel mark and a pause button whether or not a sheet
contains one, and those become gaps rather than disappearing. And a suggestion stays a suggestion:
the sheets contain a tyre, a teddy bear, a packet of cigarettes and a radiation canister, which is
why the taxonomy below has a curio, a morale item and a hazard in it — but each was accepted on
whether a colony sim wants it, not merely on whether it could be drawn.

## 2. The eight sheets

| Sheet | Theme | Verdict |
|---|---|---|
| 01 | Raw and wilderness materials: stone, logs, ore, hides, plants, fungus, bone, hand tools | Good for raw stock. Some of it is stone-age and unused |
| 02 | Prepared food: tins, soup, bread, cheese, fish, produce, milk, coffee, wine | Carries the whole food chain |
| 03 | Camp and crafting stations: campfire, anvil, cauldron, workbench, spinning wheel, tent, well | **Weakest fit.** Pre-industrial. Used only where a piece is era-neutral: crate, barrel, bucket, bedroll, table, trap |
| 04 | Manufactured materials: ingots, wire, circuit boards, microchips, glass, gears, pipe, fabric | **Strongest fit for the setting.** Nearly the entire refined economy |
| 05 | Tools and weapons: drill, knives, axes, pickaxe, shovel, sickle, revolver, crossbow, shield | Equipment, and the work verbs that have a tool |
| 06 | Framed action tiles: cooking, farming, medical cross, crosshair, flame, paw, skull, hourglass, book | Not items but **verbs**. The only source for work types, tabs and overlays |
| 07 | Anatomy and butchery: organs, bone, hide, meat, skull, brain, eye | Butchery products, and the health system's body parts |
| 08 | Modern salvage and survival gear: jerrycan, battery, solar panel, generator, med kit, radiation, sandbag | **Best thematic fit.** The ruined-city sheet |

Cells drawn on, by sheet: 01 (11), 02 (13), 03 (17), 04 (29), 05 (37), 06 (64), 07 (25), 08 (72).
Sheet 06 and sheet 08 carry the interface between them; sheet 03 contributes 17.

## 3. Coverage

| Namespace | Keys | With art | Gaps | Coverage | Chiefly from |
|---|---:|---:|---:|---:|---|
| `ui.res.*` | 48 | 48 | 0 | 100% | sheet 04, sheet 08 |
| `ui.skill.*` | 13 | 13 | 0 | 100% | sheet 06, sheet 05 |
| `ui.work.*` | 22 | 21 | 1 | 95% | sheet 06, sheet 05 |
| `ui.arch.category.*` | 10 | 9 | 1 | 90% | sheet 06, sheet 08 |
| `ui.item.*` | 30 | 26 | 4 | 86% | sheet 05, sheet 08 |
| `ui.status.*` | 12 | 10 | 2 | 83% | sheet 06, sheet 08 |
| `ui.tab.*` | 11 | 9 | 2 | 81% | sheet 06, sheet 08 |
| `ui.overlay.*` | 10 | 8 | 2 | 80% | sheet 06, sheet 08 |
| `ui.arch.tool.*` | 70 | 49 | 21 | 70% | sheet 08, sheet 03 |
| `ui.alert.*` | 22 | 15 | 7 | 68% | sheet 06, sheet 02 |
| `ui.health.*` | 28 | 18 | 10 | 64% | sheet 07, sheet 06 |
| `ui.need.*` | 8 | 5 | 3 | 62% | sheet 08, sheet 06 |
| `ui.bulletin.*` | 15 | 9 | 6 | 60% | sheet 06, sheet 08 |
| `ui.weather.*` | 11 | 5 | 6 | 45% | sheet 06, sheet 08 |
| `ui.command.*` | 48 | 20 | 28 | 41% | sheet 08, sheet 05 |
| `ui.pawn.*` | 7 | 2 | 5 | 28% | sheet 06, sheet 07 |
| `ui.layer.*` | 9 | 1 | 8 | 11% | sheet 06 |
| `ui.mood.*` | 4 | 0 | 4 | 0% | — |
| `ui.speed.*` | 4 | 0 | 4 | 0% | — |

Confidence across the 268 assignments: 143 high, 88 medium,
37 low. Every low-confidence row is a guess about what a sixteen-pixel drawing
depicts and should be checked against the contact sheet before it is approved.

The shape of that table is the finding. **The sheets are rich in nouns and poor in abstractions.**
Commodities and skills are completely covered; work types nearly so, because most work has a tool.
What is missing clusters into a few themes, and one of them is serious.

## 4. What is missing, and what to supply

114 keys have no suitable cell. Grouped by what would need drawing rather than by namespace,
because that is how art gets commissioned.

### People and faces — 22 missing

| Key | What is needed |
|---|---|
| `ui.alert.mentalbreak` | a mental break. Blocked on the missing face art |
| `ui.alert.prisonerescape` | a prisoner getting out |
| `ui.bulletin.arrival` | someone arriving. Blocked on the missing human figure |
| `ui.bulletin.recruited` | a prisoner joining us |
| `ui.bulletin.refugee` | someone asking for shelter |
| `ui.bulletin.wanderer` | someone asking to join |
| `ui.command.arrest` | an arrest |
| `ui.command.capture` | taking someone prisoner |
| `ui.command.rescue` | carrying a casualty. Blocked on the missing human figure |
| `ui.command.strip` | taking everything a pawn carries |
| `ui.mood.breaking` | mood face, at the break threshold |
| `ui.mood.broken` | mood face, in a break |
| `ui.mood.content` | mood face, content. No sheet has a human face at all |
| `ui.mood.strained` | mood face, strained |
| `ui.pawn.colonist` | a human figure. No sheet contains one, and this is the most-used icon in the HUD |
| `ui.pawn.hostile` | human figure, hostile |
| `ui.pawn.prisoner` | human figure, restrained |
| `ui.pawn.synth` | a machine intelligence. Nothing in the sheets is recognisably robotic |
| `ui.pawn.visitor` | human figure, neutral |
| `ui.status.downed` | a figure prone. Blocked on the missing human figure |
| `ui.tab.colonists` | a group of people. Blocked on the missing human figure |
| `ui.work.rescue` | carrying a casualty. Blocked on the missing human figure |

### Verticality and the layer model — 22 missing

| Key | What is needed |
|---|---|
| `ui.alert.breach` | a hole in a wall. Our own concept |
| `ui.alert.collapse` | a collapse that has happened |
| `ui.alert.drop` | something arriving through open sky. Our own concept, and verticality-specific |
| `ui.alert.trapped` | a colonist cut off from the colony |
| `ui.alert.unsupported` | an unsupported span about to fall. Central to the layer model |
| `ui.arch.tool.airlock` | a sealed double door |
| `ui.arch.tool.hatch` | a hatch in a floor |
| `ui.arch.tool.ladder` | a ladder. M1 needs it and no sheet has one |
| `ui.arch.tool.reclaim` | adopting existing ruined structure. Our own invention, nothing to borrow |
| `ui.arch.tool.reclaimer` | a machine that sorts scrap. Our own invention |
| `ui.arch.tool.removefloor` | removing a floor. Pairs with the missing roof icon |
| `ui.arch.tool.roof` | a roof or ceiling panel seen from below. Central to the layer model |
| `ui.arch.tool.stair` | a staircase. M1 needs it and no sheet has one |
| `ui.layer.down` | a downward arrow to match the up arrow |
| `ui.layer.ground` | a ground or street level mark |
| `ui.layer.policy.full` | visibility mode: no cut-away |
| `ui.layer.policy.ghost` | visibility mode: ghost outlines |
| `ui.layer.policy.hide` | visibility mode: hide above |
| `ui.layer.policy.roofsoff` | visibility mode: roofs off |
| `ui.layer.policy.xray` | visibility mode: x-ray, all layers |
| `ui.layer.policy.xraymin` | visibility mode: x-ray, one layer |
| `ui.overlay.roofs` | a roofed-area mark. Pairs with the missing roof tool icon |

### Interface abstractions — 24 missing

| Key | What is needed |
|---|---|
| `ui.arch.tool.cancel` | a cancel mark. Trivial to draw, and in the HUD constantly |
| `ui.command.copysettings` | copy. Pure interface abstraction |
| `ui.command.drop` | dropping a carried item |
| `ui.command.hold` | holding position |
| `ui.command.install` | placing a built thing |
| `ui.command.jumpto` | jump the camera and slice here |
| `ui.command.moveto` | a movement order marker |
| `ui.command.pastesettings` | paste. Pure interface abstraction |
| `ui.command.prioritise` | do this next. An abstract, and heavily used |
| `ui.command.reinstall` | moving a built thing |
| `ui.command.rename` | renaming. Pure interface abstraction |
| `ui.command.selectsimilar` | select-similar. Pure interface abstraction |
| `ui.command.setowner` | assigning a thing to a person |
| `ui.command.setpriority` | a priority number badge |
| `ui.command.switchoff` | a power state, off |
| `ui.command.switchon` | a power state, on |
| `ui.command.undraft` | standing down. Pairs with draft |
| `ui.command.unequip` | putting a weapon down |
| `ui.command.uninstall` | picking a built thing up intact |
| `ui.speed.fast` | fast forward |
| `ui.speed.pause` | pause. Universal symbol, trivial to draw, not in the sheets |
| `ui.speed.play` | normal speed |
| `ui.speed.ultra` | very fast |
| `ui.tab.menu` | a settings or menu mark |

### Weather — 6 missing

| Key | What is needed |
|---|---|
| `ui.weather.ashfall` | ash falling |
| `ui.weather.clear` | clear sky or sun |
| `ui.weather.cloudy` | cloud |
| `ui.weather.fog` | fog or mist |
| `ui.weather.storm` | lightning |
| `ui.weather.windstorm` | wind |

### Recreation and comfort — 9 missing

| Key | What is needed |
|---|---|
| `ui.arch.category.recreation` | a recreation category mark. Blocked on the same missing art as ui.need.joy |
| `ui.arch.tool.gamestable` | a games table. Same gap as recreation throughout |
| `ui.arch.tool.sculpture` | a sculpture or art object |
| `ui.arch.tool.viewscreen` | a screen or monitor |
| `ui.command.recreate` | the recreation gap again |
| `ui.need.comfort` | an armchair or cushion, read as comfort rather than as furniture |
| `ui.need.joy` | something unmistakably recreational: dice, cards, a games board |
| `ui.need.space` | an abstract for room to move. Hardest of the needs to draw |
| `ui.status.recreating` | off-duty by choice. Blocked on the same missing recreation art as ui.need.joy |

### Anatomy — 10 missing

| Key | What is needed |
|---|---|
| `ui.health.anaesthetic` | under anaesthetic. A syringe or a mask would do |
| `ui.health.ear` | an ear |
| `ui.health.finger` | a finger |
| `ui.health.foot` | a foot |
| `ui.health.implant` | a fitted mechanical part |
| `ui.health.missing` | an absent limb, shown as a silhouette gap |
| `ui.health.neck` | a neck, distinguishable from a torso |
| `ui.health.nose` | a nose |
| `ui.health.scar` | an old healed wound |
| `ui.health.shoulder` | a shoulder joint |

### Machines and structures — 9 missing

| Key | What is needed |
|---|---|
| `ui.arch.tool.blastdoor` | a heavy blast door, distinct from a normal door |
| `ui.arch.tool.cooler` | a cooling unit |
| `ui.arch.tool.geothermal` | a geothermal or steam tap |
| `ui.arch.tool.heater` | a heating unit, distinct from the brazier |
| `ui.arch.tool.hydroponics` | a hydroponic basin. Central to food in a ruined city |
| `ui.arch.tool.switch` | a power switch or breaker |
| `ui.arch.tool.tile` | a finished floor tile |
| `ui.arch.tool.turret` | an automated gun. M6 needs it |
| `ui.arch.tool.wind` | a wind turbine |

### Equipment and apparel — 4 missing

| Key | What is needed |
|---|---|
| `ui.item.boots` | a pair of boots |
| `ui.item.helmet` | a helmet |
| `ui.item.jacket` | a coat or jacket |
| `ui.item.shotgun` | a scattergun, distinguishable from the rifle |

### Everything else — 8 missing

| Key | What is needed |
|---|---|
| `ui.bulletin.birth` | a newborn animal |
| `ui.bulletin.crash` | a crashed ship or drop pod |
| `ui.command.bury` | a grave |
| `ui.command.evacuate` | clear this area now |
| `ui.command.recruit` | persuading someone to join |
| `ui.command.release` | releasing an animal |
| `ui.command.train` | training an animal, distinct from taming |
| `ui.overlay.traffic` | footfall. An abstract with no obvious source |

### The one that matters most

**No sheet contains a human figure.** Not a person, not a silhouette, not a face. That blocks
`ui.pawn.*` almost entirely, all four `ui.mood.*` keys, and every command about carrying, capturing
or rescuing a colonist. In a colony sim the colonist icon is the single most-used image in the
interface, and the mood face is how the player reads the colony's state at a glance.

**If only one thing is added, make it people:** a colonist, a hostile, a prisoner, a downed figure,
and four mood faces. Eight drawings that unblock about seventeen keys and the entire roster bar.

After that, in order of how much they unblock: the **verticality set** (stairs, ladder, roof, and the
six layer-visibility modes from ADR 0006, which are our own invention and cannot be borrowed from
anywhere), then the **interface abstractions** (cancel, pause, play, fast-forward, prioritise, copy
and paste), which are conventional shapes rather than art and would take an afternoon.

## 5. The commodity taxonomy

Forty-eight commodities, named art-first, in `docs/design/icon-keys.csv` under `ui.res.*`. All have
art. This is **provisional input to `docs/design/04-data-model.md`**, which owns the Def schema at
Phase 3; nothing here is a Def yet.

Grouped as the ledger will group them: **salvage tiers** (scrap, rubble, girder, hull panel) replacing
ore classes; **structural** (alloy, composite, concrete, glass, polymer, ceramic, rebar); **refined and
technical** (wire, circuitry, processor, mechanism, pipe, power cell, fuel, coolant, optic);
**soft goods** (fabric, leather, cord, insulation, paper); **food** (rations, meal, protein paste, raw
meat, produce, grain, fungus, water, stimulant, alcohol); **medical and organic** (medkit, medicine,
organ, prosthetic, bone, tallow, plant matter, seed); and **trade, morale and munitions** (ammunition,
credit chit, relic, smokes, curio).

Two naming notes. **`plasteel` is gone**: it appeared in the mockup's placeholder ledger, it is
RimWorld's material name, and `CLAUDE.md` requires our own. It is `composite`, which is also what the
art shows. And **salvage tiers are deliberately four**, because the brief's open question on what
replaces ore wants an answer the player can hold in their head: unsorted scrap, cleared rubble, cut
girders, stripped panels.

## 6. Where the files live

| Path | What |
|---|---|
| `art-source/icons/sheets/` | The eight source sheets. **Outside `Assets/`** on purpose, so Unity never imports a six-hundred-cell texture and the rule "every texture under `Assets/Art/Ui/` is at most 64 pixels" needs no exception |
| `art-source/icons/sheets.csv` | Per sheet: file, asserted grid, cell size, trim mode, target size, native scale |
| `docs/design/icon-keys.csv` | The registry. From M0 it is generated out of the Def set and committed; until then it is hand-authored |
| `docs/design/icon-map.csv` | Key to cell, with a description, a confidence and a status |
| `Assets/Art/Ui/icons/<key>.png` | The exported icons, committed. What the resolution chain in `09` §7 step 2 finds |
| `Assets/Art/Ui/generated/` | Placeholders for everything unmapped. Gitignored and regenerated |
| `tools/icons/icons.py` | The slicer, validator and contact-sheet generator. Standard library only |

## 7. The review loop, because the mapping is not yet verified

The assignments in `icon-map.csv` name a **sheet and a described cell**, not a row and column. That is
deliberate rather than unfinished: the sheets were supplied as pasted images, so they were read as
pictures rather than measured as files, and a row-and-column guess would look more certain than it is.

The loop that finishes it, once the eight PNGs are in `art-source/icons/sheets/`:

1. `tools/icons/icons.py detect` reports each sheet's grid and native pixel scale, and fails loudly
   if detection and `sheets.csv` disagree rather than preferring either.
2. `tools/icons/icons.py contact` writes labelled contact sheets: every cell at 3× with its row and
   column along the edges, and the assigned key drawn over it.
3. Those images are what a session or the owner reads to fill in `row` and `col` and to correct the
   descriptions. The output format of the report is the input format of the manifest, so corrections
   paste straight back.
4. `icons.py validate --strict`, then `icons.py export` writes `Assets/Art/Ui/icons/<key>.png`.

Per-row status, not per-sheet, so the food sheet can ship while the anatomy sheet is still being
argued about.

## 8. One thing deliberately not done

A full catalogue of all six hundred cells, one row per cell whether or not a key uses it, was
planned and then dropped. Two reasons. Without the files on disk the coordinates would be invented,
and a six-hundred-row file of invented coordinates is worse than no file because it looks
authoritative. And the mechanism that replaces it is better: `icons.py contact` writes a
`<sheet>-unmapped.csv` listing every cell that has art but no key, already in the mapping's own
column order, so the answer pastes straight back in. The catalogue therefore gets generated from the
art rather than guessed at, and it records the cells that matter — the unused ones — rather than all
six hundred.

## 9. What is provisional here

The commodity taxonomy, until `04-data-model.md` turns it into Defs. Every row and column, until the
contact sheets exist. The claim that the art is 32-pixel native, which `detect` settles in one run and
on which ADR 0007's export factor depends. And the thirty-seven low-confidence assignments, which are
guesses in the honest sense.
