# 15. Skills, and the art that names them

**Status: grounding, 2026-09-17.** The owner supplied `06-action-tiles.png` — 56 framed 32 px
tiles, the sheet the icon map has been calling sheet 06 since it was written blind — and asked two
things of it: work out how the skills it depicts could apply to this game, and put the icons beside
a colonist's skills in the interface, a facade if need be, to see how it reads.

Nothing here is built. This is the catalogue, the proposed mapping and the questions the owner has
to answer before any of it becomes code.

---

## 1. Where skills actually stand

Three things are true at once and they are easy to confuse.

| | What exists |
|---|---|
| **The simulation** | Three skills: `Skill_Hauling`, `Skill_Cutting`, `Skill_Mining`. Each has real experience in thousandths, a passion rolled at placement, a level 0–20 read off a def table, and daily decay above level 10. Saved and hashed. |
| **The published frame** | Nothing. `PawnView` carries no skill at all, which is why the inspect pane's Skills tab is disabled with the reason "no skill data yet" (OQ-45). |
| **The design** | Thirteen skills in `icon-keys.csv`: construction, mining, salvage, cooking, growing, animals, crafting, fabrication, medicine, social, shooting, melee, intellect. Twelve in `a-01-pawns.md`, which has *artistic* and no *salvage* or *fabrication*. The two lists have never been reconciled. |

So the gap is not a rendering gap. **A Skills tab showing real data can show three rows today**;
anything wider is a drawing of a game that has not been built. Which of those two the owner wants is
question 1 below.

Two further facts worth having in front of you:

- **`Skill_Hauling` and `Skill_Cutting` have no `ui.skill.*` key.** They borrow the work type's
  words (`ui.work.hauling`, `ui.work.cutting`). The thirteen designed keys have no *hauling* and no
  *cutting* at all, because the design expected cutting to be part of *growing* and hauling to be
  unskilled. The simulation disagreed with the design and nobody noticed.
- **Nothing reads a skill level yet.** Not work speed, not yield, not quality. `SkillSystem` decays
  levels above ten and that is the whole of it. A Skills tab is therefore honest about a number
  that currently has no consequence.

  **Scheduled 2026-09-17** — the owner asked why chopping is not faster for a skilled colonist, and
  the answer to "what would make the level mean something" is now designed in
  `17-rates-and-stats.md` and planned as `U42`–`U45` (`vertical-slice.md` §WS). It reads
  `SkillIndex` as it stands, so it neither depends on nor blocks the table reshuffle §6 leaves
  open. One convergence worth noting here: that design's research found the reference's general
  labour has **no skill-driven speed at all**, which is §6's "hauling is a work type, not a skill"
  arriving from the opposite direction.

---

## 2. The sheet, cell by cell

`art-source/icons/sheets/06-action-tiles.png`, 256 × 224, an 8 × 7 grid of 32 × 32 cells at native
scale 1, every cell filled. `trim_mode=cell`, because the frame is part of the drawing. Rows and
columns below are **zero-indexed**, as `icon-map.csv` and `tools/icons/icons.py` count them.

| | c0 | c1 | c2 | c3 | c4 | c5 | c6 | c7 |
|---|---|---|---|---|---|---|---|---|
| **r0** | owl on a bare branch, full moon | stew pot over a fire | axe struck into a log pile | cave mouth in rock | hand picking red berries | mixed woodland | cut gems, sparkling | roast on a spit |
| **r1** | pickaxe on dark rock | submachine gun on canvas | treasure map with a red X | basket of root vegetables | drill bit biting riveted plate | stone fire ring with laid logs | mine cart heaped with ore | axe buried in a chopping block |
| **r2** | hide stretched on a drying frame | knife carving a plank | upright loom, warp strung | crate of TNT | boar's head mounted | hammer striking an anvil | cauldron on a hook over flames | cleaver in meat on a block |
| **r3** | sack of seed, leaf-marked | crucible pouring molten metal | strung bow, arrow nocked | sprung steel jaw trap | broad axe with slash marks | spear in undergrowth | paw print in mud | first-aid case, pills spilling |
| **r4** | campfire with a green cross | lit torch | eye in a red reticle | green-hilted dagger | fish leaping in water | lean-to of poles and planks | mallet and cog on a workbench | door standing open |
| **r5** | scope reticle over a landscape | binoculars, green lenses | drop of blood | pistol over a target arc | skull with a bullet hole | rifle round in flight | burning cartridge | rifle firing, muzzle flash |
| **r6** | slung submachine gun | log cabin | hourglass with a green gem | hide on a stretching frame | mortar and pestle among herbs | green up arrow with a cross | open book, lightbulb above | blueprint, blue schematic |

Contact sheet: `art-source/icons/contact/06-contact.png`. Enlarged row strips are trivial to
regenerate; the pipeline's own `contact` command is the supported way.

---

## 3. What the sheet says this game could be

Read as a whole, the sheet is a **survival colony game with hunting, firearms and a frontier
economy**. Sorting the 56 by what they would need from the simulation is the most useful thing
that can be said about it, because it separates "art for a skill we have" from "art arguing for a
system we have not designed".

**a. Draws something the simulation already has (3 cells).**
Mining (r1c0), felling (r0c2 / r1c7), hauling (r1c6, at a stretch — it is a mine cart).

**b. Draws a skill the design has already named but nothing implements (10 cells).**
Construction (r4c6, r6c1), cooking (r0c1, r2c6, r0c7), growing (r3c0, r1c3), animals (r3c6),
crafting (r2c5, r2c1), medicine (r3c7), shooting (r5c0, r5c3, r5c7), melee (r3c4, r4c3),
intellect (r6c6), salvage (r1c4, r4c7), fabrication (r3c1).

**c. Argues for a system nobody has designed (the interesting part).**

| Cells | The system they imply | Nearest thing we have |
|---|---|---|
| r2c4 boar, r3c2 bow, r3c3 trap, r3c5 spear, r3c6 tracks, r4c4 fish | **Hunting, trapping and fishing** — wild animals as a food source | Nothing. No creature simulation at all |
| r2c7 cleaver, r2c0 + r6c3 hides, r3c7 | **Butchery and tanning** — a carcass becomes meat and leather | Nothing |
| r2c2 loom, r6c3 parchment | **Textiles** — fibre to cloth | Nothing |
| r3c1 crucible, r2c5 anvil, r1c4 drill | **A refining chain** — ore to ingot to part | Ore is mined and then only stockpiled |
| r2c3 TNT | **Demolition** — blowing a hole rather than cutting one | Mining collapses nothing (U29) |
| r0c4 berries, r0c5 woodland, r1c3 basket | **Foraging** — food from an unfarmed map | Trees are felled for wood only |
| r4c0, r1c5, r4c1 fire and torch | **Warmth and light** as needs | Neither exists; the day/night cycle is presentation only |
| r0c0 owl, r6c2 hourglass | **Time of day mattering to a colonist** | The clock drives light and nothing else |
| r5c2 blood, r5c4 skull | **Injury and death** | A pawn cannot be hurt |
| r6c5 up arrow, r6c6 book, r6c7 blueprint | **Progression** — levelling, research, plans | Skills level; research and blueprints do not exist |
| r4c5 lean-to, r6c1 cabin | **Shelter as a built thing with a purpose** | Beds exist; rooms and roofs do not |
| r4c7 open door, r1c2 map, r5c1 binoculars | **Exploration beyond the map edge** | The map is the world |

**This is the finding.** The sheet is not a skill list for the game we have; it is a skill list for
a game two or three milestones out, and about half of it belongs to systems the plan has not
reached. That is not a problem — it is what an art set is for — but it means **the skill rows have
to be chosen deliberately rather than taken from the sheet wholesale**, or the interface will
promise hunting, tanning and weaving to a player who can only chop and dig.

---

## 4. Proposed mapping, one cell per designed skill

Ranked and committed to, per the working agreement. Where a second cell is defensible it is named
as the alternative, with the reason the first was chosen.

| Key | Cell | Why this one | Alternative |
|---|---|---|---|
| `ui.skill.mining` | **r1c0** pickaxe on rock | The only unambiguous mining tile | r0c3 cave mouth (a place, not an act) |
| `ui.skill.construction` | **r4c6** mallet and cog on a bench | The *act*; the icon map's own guess was "hammer and tools" | r6c1 log cabin — a built thing, which reads better small but names the product |
| `ui.skill.cooking` | **r0c1** stew pot over a fire | Reads at 17 px; steam gives it a silhouette | r2c6 cauldron on a hook, r0c7 spit roast |
| `ui.skill.growing` | **r3c0** seed sack | Covers sowing, which is the half the player orders | r1c3 harvest basket — the yield, not the work |
| `ui.skill.animals` | **r3c6** paw print | Exactly the icon map's guess | — |
| `ui.skill.crafting` | **r2c5** anvil and hammer, sparks | Bench work generally | r2c1 knife carving a plank (too close to *cutting*) |
| `ui.skill.medicine` | **r3c7** first-aid case | Exactly the icon map's guess | r6c4 mortar and pestle — herbal, not clinical |
| `ui.skill.intellect` | **r6c6** book with a lightbulb | Exactly the icon map's guess | — |
| `ui.skill.shooting` | **r5c0** scope reticle | A circle reads at 17 px where a muzzle flash is a blob | r5c7 rifle firing, r5c3 pistol and target |
| `ui.skill.melee` | **r3c4** broad axe with slash marks | The slashes make it combat rather than woodcutting | r4c3 dagger |
| `ui.skill.salvage` | **r1c4** drill biting riveted plate | Stripping a shell; industrial rather than natural | r4c7 open door (breaching, arguably a different skill) |
| `ui.skill.fabrication` | **r3c1** crucible pouring metal | Refining, which is the advanced half | r1c4 drill — but that is salvage's |
| `ui.skill.social` | **none** | **Nothing on this sheet depicts people talking.** The icon map sends it to sheet 08's two-way radio | — |

And the two the simulation has that the design never gave a key:

| Skill in the sim | Cell | Note |
|---|---|---|
| `Skill_Cutting` | **r1c7** axe in a chopping block | r0c2 (axe in a log pile) is the same idea with more clutter |
| `Skill_Hauling` | **r1c6** mine cart of ore | Weak — it is a mining tile. There is no carry, sack or barrow on the sheet |

**Twelve of thirteen designed skills can be drawn from this one sheet**, which is the headline. The
thirteenth is social, and the gap is real rather than an oversight: a sheet about survival has no
picture of a conversation.

---

## 5. Two frictions this creates

**a. Two sources of art for the same three ideas.** The roster card's activity icons shipped on
2026-09-17 from three separate 32 px drawings — an axe, a pickaxe, a hammer, unframed, no
background. This sheet's tiles are framed, with a painted scene behind them. Side by side in one
interface they will not read as one set. Either the status icons come from this sheet too, or the
skill rows take unframed art, or the interface accepts two idioms and separates them by context
(a status is a small mark; a skill is a portrait). This is question 4.

**b. The export path does not reach the HUD.** `tools/icons/icons.py export` writes to
`Assets/Art/Ui/icons/`, and `IconArt` loads from `Assets/Art/Ui/Resources/odyssey/icons/`. Nothing
exported by the pipeline has ever been loadable by the interface. That is a one-line fix to
`OUT_DIR` and it has to happen before any of this sheet reaches the screen through the supported
route rather than by hand.

---

## 6. Answered by interview, 2026-09-17

The owner took all four recommendations.

1. **Thirteen rows, the ones with a simulation behind them live and the rest visibly
   unavailable** — the idiom the rest of this HUD already uses for a tab, a command or a panel
   that does not exist yet. Not three real rows (too thin to judge), and not thirteen invented
   ones (a mockup to be torn out).
2. **The thirteen in `icon-keys.csv` are canon.** Not `a-01`'s twelve. So *artistic* is out and
   *salvage* and *fabrication* are in, and **hauling and cutting are not skills** — they are work
   types, which is both the design's position and the reference's.
3. **A row is an icon, a name, a level and a passion mark.** No progress bar: thirteen bars is a
   lot of furniture, and passion is the field that decides who you put on a job, so it goes on the
   row rather than in a tooltip.
4. **Two icon idioms, split by context.** Framed painted tiles for skills — a skill is a portrait
   you read once in a pane — and bare marks for status, alerts and the command bar, which are
   glanced at over the world. The frame becomes the cue for *this is a thing about a person*.

### What that leaves open, and it is on the simulation's side

**`Skill_Hauling` should go.** Under answer 2 hauling is a work type and not a skill, so the
experience the simulation has been accruing in it has nowhere to be shown and nothing to spend
itself on. Deleting it is a Defs change, a `SkillIndex` change, a save-format change and a hash
change — real work, not done here, and worth doing in one piece with whatever else reshapes the
skill table.

**`Skill_Cutting` feeds `ui.skill.growing` for now.** Felling is plant work: the canon work type
`ui.work.cutting` is "cut plants and clear growth" and the only plant skill in the canon list is
growing. That is the reference's own answer. It is visible in the interface — the row's tooltip
says "trained by felling, which is plant work" — precisely so it can be objected to.

### Still open

5. **How much of §3c the sheet is allowed to promise.** A skill list is a statement about the
   game. The thirteen are already a promise; the sheet argues for hunting, tanning, weaving and
   a refining chain on top of that.
6. **Where the sheet sits in the art plan.** Registered as sheet 06 because its contents match the
   README's description of 06 exactly — "framed action tiles: cooking, farming, medical,
   crosshair, flame, paw, skull, book", every one of which is on it. Confirm, or rename.
7. **Whether a framed tile reads at 17 px.** It does not read *well*: at row size the frame is a
   large share of the tile and what is left of the painting is a few pixels of colour. Contact
   sheet at `Logs/skill-icons.png`, 64 px over 17 px. The fix, if it is wanted, is to crop the
   frames — which the pipeline cannot do today, since these cells are opaque edge to edge and
   `tight_box` therefore returns the whole cell whatever `trim_mode` says.

---

## 7. What was built, 2026-09-17

- **The contract**: `SkillView` (pawn, skill, level, passion, experience) as a sparse counted list
  on `WorldSnapshot`, in the shape `OrderView` established, and `SkillHandle` beside `JobHandle`
  so both sides of the seam count the same way. `PawnRegistry` publishes every colonist's skills
  every frame — a few hundred bytes into a reused buffer — because the snapshot has no notion of
  selection and should not grow one. **This closes OQ-45.**
- **`SkillCatalogue`** in the Unity-free Hud assembly: the thirteen, their keys, which
  `SkillHandle` backs each, and the reason beside the ones nothing backs.
- **`InspectModel.Skills`** and a real tab strip: `ActiveTab`, `ShowTab`, and the rule that a
  disabled tab cannot become the active one. The pane opens on Needs and the choice survives a
  refresh — it is model state, or clicking a tab would undo itself fifteen times a second.
- **Twelve icons** cut from the sheet by `tools/icons/icons.py export`, which is the first time
  that tool's output has been loadable: `OUT_DIR` pointed at `Assets/Art/Ui/icons`, and
  `IconArt` loads from `Assets/Art/Ui/Resources/odyssey/icons`. Anything it had ever exported
  would have drawn the placeholder square, silently, because a key with no art is not an error
  and is not logged.
