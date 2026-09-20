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

  **Corrected 2026-09-17, on an audit against the code:** one thing does read a level, and it is
  `BuildWorkGiver.CanBuild`, which will not offer a site to a colonist below the building's
  `minSkill` (`BuildJob.cs:213`). It is **inert** — every shipped building is `minSkill = 0` — but
  it is not nothing, and it matters to the sentence above because it is a *gate*, not a rate. A
  skill drives either what you are allowed to attempt or how fast you do it, and those are two
  mechanisms; this game had the first and not the second.

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

---

## 8. The bar, the toast, and two rows that had been lying (SK1–SK5, 2026-09-20)

**What the owner asked for.** *"Enable skills for chopping, mining and plants/gardening"*, plus a bar
in the colonist card that fills as the work is done, a level-up that increments and raises a positive
notification, and consideration of *"traits or 1/2 stars"*.

**Most of it already existed, and the status lines said otherwise.** The audit is worth recording
because three separate claims were stale at once:

| Claim | Truth on the day |
|---|---|
| `CLAUDE.md`: *"a skill level buys nothing a player can feel — no rate reads it"* | WS2 had landed. Cutting, mining and construction all had curves and the accumulator read them. |
| §1 above: three simulated skills | **Five**: hauling, cutting, mining, construction, growing. |
| `SkillCatalogue`: construction *"nothing is built yet"*, growing *"nothing is planted yet"* | **Both false.** Construction since U26, growing since U47. |

So the work was not building a skill system. It was closing the last gap in the simulation and making
what was already being earned visible while it is earned.

### 8a. Two reversals, stated rather than slipped in

**§6.3 is reversed.** It chose *"a row is an icon, a name, a level and a passion mark. No progress
bar: thirteen bars is a lot of furniture."* The objection was sound and the answer is to narrow it,
not to overrule it: **the bar is on live rows only, so there are four, not fourteen.** The greyed rows
keep a name and a reason and draw no bar at all.

**§6.2's leftover is gone.** It had `Skill_Cutting` feeding `ui.skill.growing` — already undone on
2026-09-18 when Chopping got its own row, and now growing has its own simulation too, so nothing
borrows anything.

### 8b. Experience per tick, not per hit — and why that is what the owner asked for

The owner asked for experience *"with every hit… depending on the success of that hit"*.
`17-rates-and-stats.md` §3e refuses per-strike mechanics outright — *"mining as rock hit points… a
second mechanism with its own state, save and hash"* — so this could not be taken literally without
overturning a recorded decision.

It did not need to be. **Experience already accrues every tick of work** (`Job.Work` →
`Pawn.GainExperience`, 110 per tick per `Jobs.xml`), which is *finer* than per hit, not coarser. What
was missing was any way to see it. So the bar is the answer to the request and the arithmetic keeps
one owner.

**The bar does move, and this was computed before it was built** rather than hoped for. At 60 ticks a
second a working colonist earns 6,600 experience a second before passion:

| Passion | Per second | Level 0 → 1 (1,000 points) | Bar |
|---|---|---|---|
| None ×0.35 | 2,310 | ~7 min of solid work | 0.23 %/s |
| Minor ×1.0 | 6,600 | ~2.5 min | 0.66 %/s |
| Major ×1.5 | 9,900 | ~1.7 min | 0.99 %/s |

About **1.5 px a second** on a 231 px bar at level 0, three times that at speed 3. By level 9→10 the
span is ten times larger and it is a pixel every seven seconds — correct, and the reason the toast
matters more than the bar at high levels. It also confirms the soft cap's tuning: 4,000,000 a day is
reached after ~10 minutes of work against a 16.7-minute day, the two-thirds relationship `Jobs.xml`
claims.

**The per-stroke pip is not built.** The plan had a flash on the bar timed to each drawn stroke. It
wants the pane coupled to the world's stroke clock for a decoration over a bar that is already
correct, and the bar's continuous movement is what the request was actually about. Left out; the
playtest can say whether anything is missing.

### 8c. Passion, and no traits

**Passion is the "1/2 stars", and it was already built** — three tiers rolled deterministically from
the colonist's own `RollSeed`, multiplying experience by ×0.35 / ×1.0 / ×1.5, and already drawn as one
or two pips on the row. Nothing was needed but to use it: **the bar's fill takes its colour from the
passion**, so a burning skill both fills faster and looks different doing it. A four-fold spread is
otherwise invisible in something moving at 1.5 px a second.

**Traits are out of scope and the seam stays empty.** `Pawn.LearningFactorPerMille()` returns 1,000
and is the hook a trait would arrive through. Building one means new Defs, a save bump, a hash move
and fresh rolls that shift every existing colonist — real work, and none of it needed to answer the
question the owner asked.

### 8d. A level-up needs nothing from the simulation, and that is the interesting part

The level of every skill of **every** colonist is already published every frame — not just the
selected one (`PawnRegistry`). So a level-up is detected on the presentation side by comparing against
what was last seen. No event, no flag, no queue, no saved field, no hash movement.

**Why polling cannot miss one.** `PawnGesture` needs a sticky flag *and* a serial because a gesture is
an *instant*: a reader that blinks between two frames misses it for ever. A level is a *standing
value* — whatever happened in between, the next read still says what the level is now. The worst a
slow poll can do is see two levels as one rise, and `SkillLevelWatch` reports *the level reached*
rather than the number of steps taken precisely so that case needs no handling. It runs on the Mid
bucket at 4 Hz beside the alerts, so a toast can be up to 250 ms late, which nobody can perceive.

Three rules, each a test: **first sight of a colonist is silent** (or everybody announces her starting
roll on every load — the gesture serial's own rule); **a fall through decay is silent** and becomes the
new mark; **a colonist off the frame is forgotten**, so a dead one cannot leave an entry for a later
pawn to be measured against.

### 8e. A toast, not an alert and not a bulletin

The notification is a **transient toast** — design 09 §2.3's own word, used there for a rejected
intent that *"surfaces as a transient toast rather than a bulletin"*. The channel was named in the
design and never built; this builds it, and rejections (today only counted into a log) are its obvious
second customer.

Three channels, and 09 §3.1 already insists the first two stay apart because *"merging them produces
a system that is wrong for both"*:

| | What it is | Life |
|---|---|---|
| **Alert** | a *condition* — a job the player has not done | re-evaluated every refresh, clears itself |
| **Bulletin** | an *event worth keeping* — an arrival, a death, a raid | until dismissed by hand, then archived |
| **Toast** | an *event worth mentioning* | six seconds, cannot be dismissed |

**Why a level-up is a toast.** Frequency decides it. A bulletin is a card that waits to be cleared,
which is right for the fifteen things `ui.bulletin.*` names — all of them a handful of times in a
colony's life. A level lands every couple of minutes per colonist at low levels, so as a bulletin it
would be a stack cleared as a chore, and the chore would teach the player to clear the raid warning
beside it without reading it.

It is not an alert severity either: `AlertModel` exists to recompute standing conditions with a latch
per condition, and a level-up has nothing to latch on and nothing in a later frame to recompute from.

**`AlertSeverity` gains no fourth "good" value.** `AlertChime.ForSeverity` already maps `Notice` to
`alert-normal`, so the chime needed no new audio code at all — and the channel is what makes a toast
good news, not its severity. The sound reads the model's row count and fires once per refresh however
many rows arrived, because **the audio must never detect a level-up itself**: `AlertChimeWatch`'s own
remarks record what a second copy of a model's rule cost last time, when its private starvation
threshold drifted and the chime fired at a hundredth of the intended level.

Capped at four rows, oldest dropped, newest at the bottom. The one thing a passing toast must never do
is bury a starving colonist in the panel above it.

### 8f. Where it is drawn, and the trick that made it free

**The bar is an absolutely positioned 3 px underline.** `.skill` is 19 px and `HudLayout.SkillRow`
says so; the colonist pane is one fixed height across every tab *deliberately*, so that changing tab
does not move its top edge under the pointer. A bar in the flow would have grown all seven rows and
undone exactly that. Out of flow it costs the row nothing — no layout constant, no overlap case and no
coverage figure moves. **This is also what makes "live rows only" safe**: uneven row heights were the
one thing that would have forced a bar onto all fourteen.

**It is the one field on the row not guarded against change.** Everything else is written only when it
changes, because a level moves about once in a working day. The pane refreshes at 15 Hz and the bar
wants every one of them. Only the width is written, so a selected colonist still builds no string and
allocates nothing.

**The setup page gets no bar.** It is the same builder, behind a flag: a candidate has not started
working, so a part-filled bar there would report progress nobody has made.

The toast stack takes the alerts' column and width, below them — panel A6 already puts the bulletin
stack *"right edge, below the alerts"*. No header, because a heading earns its place over a standing
list somebody returns to and here would be the tallest thing in the stack for most of its life.

### 8g. Correct §1 and §6 when reading them

§1's table says three simulated skills and §6's open item says `Skill_Hauling` should go. The first is
out of date (five). The second still stands: hauling has no `ui.skill.*` row, trains from `Job_Haul`,
and has no rate curve by design — so it accrues experience with nowhere to show it and nothing to
spend it on. Deleting it is still a Defs change, a `SkillIndex` change, a save-format change and a
hash change, and is still not done.
