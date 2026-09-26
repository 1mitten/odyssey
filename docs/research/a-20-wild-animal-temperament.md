# a-20 — Wild animal temperament: the stat block, aggression and what a colony does about it

**Phase 2, Lane A**, run 2026-09-26 for the forest-animals unit (branch `claude/forest-animals`).
One subagent, capped at 12 searches and 10 page reads; it used all of both. Clean room: numbers,
formulas and design intent only — no Def XML, code or flavour text. Builds on, and does not repeat,
`a-09-animals.md` (race/kind split, wildness, the think tree), `a-10-melee-combat.md` (revenge on
harm, ×3 in melee, the calm-down timer), `a-18-cooking-hunting-butchering.md` (the hunt designation,
hunting stealth, meat = 140 × body size) and `animal-generation-interview.md`.

## Question

How does the reference game stat and drive its wild animals — temperament, what makes one
aggressive, and how it behaves when it is — in enough numeric detail to design Odyssey's own stat
block for ten forest species (bear, boar, deer, elk, fox, hare, raccoon, wolf, skunk, and one more)?

## Findings

### 1. The stat block

The reference's list pages carry two tables between them. Read from `List of animals` and
`List of animals/Additional`, cross-checked against the grizzly and timber wolf pages (which agree
with the list on every shared number). Columns: **BS** body size, **HS** health scale (×human
hit points), **Spd** move speed in cells/s (a baseline human is 4.6), **CP** combat power (the
threat-budget cost), **Wild** wildness, **Rev** manhunter chance when harmed, **TF** manhunter
chance on a failed taming, **DPS** average melee damage per second, **Meat/Lthr** yield at 100 %
butchery, **Life** life expectancy in years, **Filth** filth rate, **Hung** nutrition per day,
**MHS** minimum handling skill to tame.

| Analogue | Role | BS | HS | Spd | CP | Wild | Rev | TF | DPS | Meat / Lthr | Life | Filth | Hung | MHS | Diet | Temp °C |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Hare | **prey baseline** | 0.2 | 0.4 | 6.0 | 33 | 75 % | 0 % | 0 % | 1.05 | 31 / 16 | 8 | 1 | 0.18 | 8 | plants | −30…40 |
| Squirrel | prey | 0.2 | 0.25 | 5.1 | 33 | 75 % | 0 % | 0 % | 1.32 | 31 / 16 | 8 | 1 | 0.16 | 8 | plants | −35…40 |
| Rat | vermin | 0.2 | 0.29 | 4.0 | 30 | 50 % | 0 % | 0 % | 1.60 | 31 / 16 | 8 | 1 | 0.16 | 5 | meat, plants | −30…40 |
| Raccoon | small omnivore | 0.4 | 0.4 | 4.1 | 35 | 75 % | 0 % | 0 % | 1.94 | 56 / 22 | 8 | 4 | 0.32 | 8 | meat, plants | −30…40 |
| Red fox (= fennec, arctic) | small predator | 0.55 | 0.7 | 4.6 | 45 | 75 % | 0 % | 0 % | 2.59 | 77 / 27 | 9 | 1 | 0.16 | 8 | meat | −35…40 |
| Wild boar | defensive grazer | 0.85 | 0.7 | 4.6 | 55 | 50 % | 0 % | 0 % | 2.91 | 119 / 36 | 12 | 16 | 0.48 | 5 | meat, plants | −23…40 |
| Deer | herd prey | 1.2 | 0.9 | 5.5 | 50 | 75 % | 0 % | 0 % | 2.25 | 168 / 48 | 15 | 16 | 0.32 | 8 | plants | −30…40 |
| Caribou | herd prey | — | 2.0 | 5.0 | — | — | 10 % | 0 % | 3.10 | 140 / 40 | — | — | 0.44 | 8 | plants | — |
| Elk | large herd grazer | 2.1 | 1.9 | 5.0 | 110 | 75 % | 0 % | 0 % | 3.10 | 294 / 84 | 18 | 16 | 0.86 | 8 | plants | −50…40 |
| Moose | large, touchy | 2.5 | 2.1 | 4.7 | 125 | 75 % | 20 % | 10 % | 3.71 | 350 / 100 | 18 | 16 | 0.86 | 8 | plants | −65…40 |
| Timber wolf | **pack predator** | 0.85 | 0.99 | 5.0 | 75 | 85 % | **100 %** | 30 % | 3.63 | 119 / 36 | 12 | 2 | 0.29 | 9 | meat | −40…40 |
| Lynx | small cat predator | 0.6 | 0.8 | 5.0 | 80 | 80 % | 50 % | 20 % | 3.72 | 84 / 28 | 9 | 2 | 0.19 | 8 | meat, eggs | −50…40 |
| Cougar (= panther) | **predator baseline** | 1.0 | 1.3 | 5.0 | 120 | 80 % | 50 % | 30 % | 4.67 | 140 / 40 | 13 | 2 | 0.32 | 8 | meat, eggs | −25…40 |
| Grizzly bear | apex, solitary | 2.15 | 2.5 | 4.6 | 200 | 80 % | 50 % | 30 % | 5.38 | 301 / 86 | 22 | 4 | 0.56 | 8 | omnivore, eggs, fishes | −40…40 |
| Muffalo | herd baseline | 2.4 | 1.75 | 4.5 | 100 | 60 % | 10 % | 0 % | 3.10 | 336 / 96 | 15 | 16 | 0.86 | 6 | plants | −55…45 |
| Boomalope | hazard grazer | 2.0 | 0.65 | 3.4 | 80 | 60 % | 10 % | 10 % | 2.87 | 280 / 80 | 15 | 16 | 0.86 | 6 | plants | −15…40 |
| Megasloth | huge, touchy | 4.0 | 3.6 | 4.8 | 280 | 97 % | 50 % | 30 % | 6.13 | 560 / 160 | 20 | 24 | 1.6 | 10 | plants | −55…40 |
| Thrumbo | huge, vengeful | 4.0 | 8.0 | 5.5 | 500 | 98 % | 100 % | 0 % | 6.88 | 560 / 160 | 220 | 8 | 2.8 | 10 | plants | −65…50 |

Notes on reading the table:

- **The column mapping of the list page was inferred** (its headings did not line up with its
  cells) and then confirmed on two rows: the grizzly and wolf pages give the same hunger rate,
  meat, leather, DPS, speed, health scale and both manhunter chances. The caribou row's missing
  cells were not in the second table read.
- **Meat is exactly 140 × body size** in every row checked except the three 0.2-size animals,
  which read 31 rather than 28 — there appears to be a floor (not confirmed).
- **Minimum handling skill tracks wildness**: 50 % → 5, 60 % → 6, 75–85 % → 8–9, 97–98 % → 10.
- **Pen ("roaming") animals** were marked on boar, deer, elk, moose, caribou, muffalo, ibex and
  boomalope; the manhunter-pack event cannot pick them (§2).
- **No skunk-like animal exists** in the reference's base list, so the skunk's temperament has no
  precedent and is ours to design.
- Two further timing columns were read: every herbivore's filth rate is **16** against a
  carnivore's 1–4 (grazers foul the ground), and pen animals carry a **roam interval of 2 days**.

**Melee attacks** (read for three species; the rest were not fetched):

| Species | Attacks (damage / cooldown / armour penetration) | Avg DPS |
|---|---|---|
| Grizzly bear | two paw scratches 17 / 2.0 s / 25 % (37.5 % pick each), bite 23.6 / 2.6 s / 35 % (25 %), head blunt 11 / 2.0 s (0 % pick). **First strike stuns 280 ticks (4.7 s).** | 5.38 |
| Timber wolf | two paw scratches 10.9 / 2.0 s / 16 %, bite 12 / 2.0 s / 18 %, head blunt 6 / 2.0 s / 9 % | 3.63 |
| Elk | four leg attacks (blunt and poke) 10 / 2.0 s / 15 %, bite 10 / 2.0 s / 15 % at half pick weight, head blunt 13 / 2.6 s / 19 % | 3.10 |

The shape: every animal attack is a **body part with a damage type** (scratch, bite, blunt, poke),
all on a **2.0–2.6 s cooldown**, with damage scaling by size and ferocity rather than cooldown. A
human fist is about 2 DPS (`a-10`), so a hare is half a fist, a wolf nearly two, a bear nearly three
— against a bear's 2.5× hit points. No armour values were found on any page read.

**Biomes**: the grizzly lives in temperate forest and swamp, boreal forest, cold bog, tundra,
glacial plains and grassland; elk in boreal forest, cold bog, tundra and glacial plains. Numeric
commonality per biome was not read.

### 2. The aggression model

| Trigger | Mechanic | Numbers |
|---|---|---|
| **Harmed by a person** | A roll each time the animal is injured by a human; success turns it manhunter. | Chance = species *Rev* × the attacker's hunting stealth factor × a distance term. Community testing puts melee/close range at **×3** (`a-10`, `a-18`). |
| **Melee** | **Every animal fights back against a melee attacker, even at 0 % Rev** — why the reference forbids melee hunting (`a-18`). | always |
| **Failed taming** | One roll against *TF*; affects only the targeted animal. | 0–30 % |
| **The herd** | When one turns, animals of the same species **within 25 tiles with a path to the attacker** may turn too (`a-18`); players report the chance is about the species' own *Rev*. Burst weapons roll more often, so players prefer single high-damage shots. | 25 tiles |
| **Bonded master dies** | A tamed, bonded animal turns. | — |
| **Events** | *Mad animal* (one wild animal), *psychic wave* (every wild animal on the map), a pulser item, and the **manhunter pack**. | — |

**Duration.** Recovers after a **minimum 10,000 ticks** with a **mean of 18,000** (4 h and 7.2 h of
a 60,000-tick day), **or on sleeping, or on being downed**. A manhunter attacks **any human** (and
mechanoids), not only the one who hurt it.

**Obstacles.** A manhunter **does not break through walls or doors on its own**; it attacks a door
only if it **saw somebody go through it**.

**The manhunter pack** event: **1.4× the points of a raid**; the species is drawn from the biome's
wildlife list, restricted to animals that can pass fences (so never a pen animal); the animals
carry scaria (permanent rage, a chance to rot the corpse on death, so the meat is lost); they
**leave after 24–54 hours**. "A pack" can be a single animal.

**Predators.** A predator hunts when its food need falls to about **30–35 %** (a player guide;
low). It picks the **nearest, smallest** acceptable prey, excluding its own species and the
exploding animals; prey must be within its *maximum prey body size* (the values were not read). It
**keeps attacking after the prey is downed** to kill it. Whether it will take a person is a
**difficulty switch**, *Predators hunt humans*: off on the two easiest presets, on from Adventure
upward. The grizzly page says bears are rarely hungry enough to attack people unprovoked; the wolf
page says wolves, *when especially hungry*, frequently try for pets and colonists. **Vanilla has
no pack hunting** — wolves are seen alone or in pairs and hunt singly; it is a common mod.

**Flee.** Animals have a chance of fleeing erratically **from ranged attacks landing near them**.
They **do not flee predators, and do not flee melee**, and never leave a pen or allowed area to
flee. The developers' stated reason is to weaken combat animals, and fleeing can carry an animal
*towards* the shooter.

**Unprovoked aggression.** None is territorial. The only unprovoked attackers are a hungry predator,
a manhunter-pack or mad-animal event, and a psychic wave.

### 3. What the colony does about it

- **Hunt** is a per-animal designation taken by the Hunting work type, ranged-only, from maximum
  range, finished at point blank (`a-18`).
- **Tame** is a per-animal designation for the Handling work type, gated by *MHS*; failure rolls *TF*.
  **Slaughter** exists only for tamed animals.
- **Warning**: when a predator targets a colonist or colony animal, a **short-lived message and
  alarm** — not a standing alert. The inspect line of the animal reads *Hunting <name>*. Players
  report learning of it from the *needs treatment* alert, and a popular mod adds a pausing alert.
- **Difficulty**: *Animal revenge chance* is **25 % on Peaceful and 100 % on every other preset**
  (a multiplier on *Rev*); *Predators hunt humans* as above; manhunter packs scale with the threat
  scale (10 % Peaceful, 30 / 60 / 100 / 155 / 220 % for the other five presets).

### 4. What players complain about

1. **"Every hunt is a fight" is a per-hit dice roll.** A herd species turns as a herd, burst
   weapons roll more checks, and the chance is hidden behind a stat page — so players learn the
   rule by losing a hunter, and then optimise around it (single-shot rifles, a kiting decoy).
2. **Predator hunts are silent.** The message is transient, the danger is discovered once a
   colonist is already down, and players install a mod for a proper alert.
3. **Manhunter packs are resolved by hiding.** They don't break walls, so a closed base waits out
   24–54 hours; a pack of one is called a pack; scaria wastes the meat. Some players call it
   unplayable without mods that exempt certain animals.
4. **Boomalopes** explode on death (10 fire damage, 1.9 / 2.9 / **4.9-tile** radius by life stage),
   so one headshot or a sick one indoors can burn a colony. A hazard whose tell is the animal's
   species rather than its behaviour.
5. **Fleeing is erratic** and can run an animal into the hunter.

## Recommendation

The reference's model has **one axis** — a single *revenge* chance plus a predator flag — and every
complaint above comes from that axis being a hidden dice roll. Back a **five-rung temperament
ladder**, each rung a *behaviour*, with a few integer fields rather than one probability. Our day is
60,000 ticks, so the reference's tick numbers carry over unchanged. All chances are per mille.

### The ladder

| Rung | Does unprovoked | Struck in melee | Shot (ranged, per hit) | The herd | Species |
|---|---|---|---|---|---|
| **0 Timid** | flees a person within its **startle radius** | flees (never fights) | flees | flees with it | hare, deer |
| **1 Skittish** | flees within startle radius | **fights back while cornered or struck** | `revenge` roll, low | — (solitary) | fox, raccoon, skunk |
| **2 Defensive** | ignores people; stands its ground | **always fights back** (the reference's rule, already ours for the hog) | `revenge` roll | members within `herdRadius` join on the same roll | boar, elk/moose |
| **3 Territorial** | **warns, then charges** a person who stays within `territoryRadius` of it while it rests | always fights back | `revenge` roll, high | — | bear |
| **4 Predator** | hunts prey when hungry; people only when starving and allowed | always fights back | `revenge` 1000 | packmates within `herdRadius` join a hunt and a fight | wolf |

Rung 1 and startle radius are **our additions**: the reference has no proximity flee, only
flee-from-nearby-gunfire. They exist because with melee hunting (design 33, `a-18` §1) a hare
that stands and bites is absurd and one that is always caught is no game. Rung 3's territory is
also ours; the reference has none, and a bear that charges a person who lingers near where it
sleeps is exactly the "readable threat" the complaints ask for.

### Fields and numbers to back

| Field | Meaning | Value |
|---|---|---|
| `revengePerMille` | chance a ranged hit enrages; ×3 within 4 cells, capped at 1000 | see species table |
| `rageMinTicks` / `rageMeanTicks` | how long enraged; ends early on sleep or down | **10,000 / 18,000**, as the reference |
| `herdRadius` | cells within which a herd or pack joins | **12** (the reference's 25 tiles scaled to our 120-cell board, a tenth of its width either way) |
| `startleRadius` | a person inside it makes the animal flee | hare 5, deer 7, fox/raccoon 4, skunk 3; 0 for rungs 2–4 |
| `fleeDistance` | how far it runs | 2 × `startleRadius` + 4 |
| `territoryRadius`, `warnTicks` | territorial rung only | bear 4 cells, 250 ticks of warning before the charge |
| `preyMaxBodySizePerMille`, `huntBelowFoodPerMille` | predators only | wolf 1,000 (a deer calf, a boar; never an elk), hunt below **330** |
| `huntsPeople` | a predator will take a colonist only if **food < 100 and she is alone** (no other person within `herdRadius`) and the world setting allows it | off by default |

**Rage ends in downs, not deaths**, including a predator's: design 33's standing rule
("an unordered fight ends in downs, never deaths") is the owner's, and the reference's
predator-finishes-its-prey is the one place to depart deliberately. A predator drags nothing and
eats nothing that is a person; it disengages when its target goes down. This is an owner decision
and is named as one.

### Proposed stat block for the forest

Body size, health and speed are per mille of a colonist; the reference analogue's numbers are
converted, not invented, except where marked.

| Species | Rung | Analogue | Size ‰ | Health ‰ | Speed ‰ | DPS (×10) | Revenge ‰ | Group | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Hare | 0 Timid | hare | 200 | 400 | **1,300** | 10 | 0 | 1 | outruns everyone: hunted with a gun or not at all |
| Deer | 0 Timid | deer | 1,200 | 900 | 1,200 | 22 | 0 | 3–6 (ours) | the herd bolts together |
| Fox | 1 Skittish | red fox | 550 | 700 | 1,000 | 26 | 0 | 1 | a small predator of hares and rats only |
| Raccoon | 1 Skittish | raccoon | 400 | 400 | 900 | 19 | 0 | 1–2 | raids food on the ground (hook) |
| Skunk | 1 Skittish | **none** | 300 (ours) | 400 (ours) | 850 (ours) | 10 (ours) | 0 | 1 | **sprays instead of biting**: a mood thought and a smell for a day on whoever cornered it — no damage |
| Boar | 2 Defensive | wild boar | 850 | 700 | 1,000 | 29 | 100 (ours; the reference reads 0) | 3–5 (the hog's) | the midden hog is this rung already |
| Elk | 2 Defensive | elk / moose | 2,100 | 1,900 | 1,090 | 31 | 150 (between elk 0 and moose 200) | 3–6 (ours) | the herd joins |
| Bear | 3 Territorial | grizzly | 2,150 | 2,500 | 1,000 | 54 | 500 | 1 | first strike stuns (reference: 280 ticks) |
| Wolf | 4 Predator | timber wolf | 850 | 990 | 1,090 | 36 | 1,000 | 2–4 (ours) | the pack joins |

For **combat power** as a threat budget, take the reference's analogue values as they stand (hare
33, deer 50, boar 55, wolf 75, elk 110, bear 200) — they are already on the scale a band of
hostiles is priced on.

### Learn from the complaints

1. **Make the rung visible before the hunt.** The inspect pane and the Almanac name the temperament
   in words (*Timid*, *Will fight back*, *Territorial*, *Predator*), and the hunt order's tooltip
   says what happens if the shot does not kill. A player should never learn a species' rung by
   losing a colonist.
2. **A predator's hunt is a standing alert, not a message** — *Wolf hunting Ada*, pinned for as long
   as the target is chosen, jumping to the predator. The reference's silence here is its most-cited
   gap, and the mod that fixes it is among its most installed.
3. **Roll per shot, not per pellet.** Our pistol fires single shots, so this is free today; state
   it as a rule before a burst weapon arrives.
4. **An enraged animal remembers the door** as the reference's does (it attacks a door it saw
   somebody use), but a manhunter-pack analogue, if one ever comes, should have a reason to leave
   sooner than a day and a half — hiding should be a cost, not the answer.
5. **No death hazard tied to species alone** (the boomalope lesson): if a forest animal is
   dangerous to kill, its danger shows in its behaviour first.

## Sources

- https://rimworldwiki.com/wiki/Animals
- https://rimworldwiki.com/wiki/List_of_animals
- https://rimworldwiki.com/wiki/List_of_animals/Additional
- https://rimworldwiki.com/wiki/Grizzly_bear
- https://rimworldwiki.com/wiki/Timber_wolf
- https://rimworldwiki.com/wiki/Elk
- https://rimworldwiki.com/wiki/Manhunter
- https://rimworldwiki.com/wiki/Manhunter_pack
- https://rimworldwiki.com/wiki/Difficulty
- https://rimworldwiki.com/wiki/User:Yoshida_Keiji/User_guides/Predator_attacks
- https://rimworldwiki.com/wiki/Boomalope (search extract)
- https://steamcommunity.com/app/294100/discussions/0/1471967615877347927/ (search extract: predators hunting colonists)
- https://steamcommunity.com/app/294100/discussions/0/1751276927094441571/ (search extract: no warning when hunted)
- https://steamcommunity.com/sharedfiles/filedetails/?id=1537786185 (search extract: Predator Hunt Alert mod)
- https://steamcommunity.com/app/294100/discussions/0/1489992713688468037/ (search extract: herd revenge, burst weapons)
- https://steamcommunity.com/app/294100/discussions/0/3390660147481358988/ (search extract: animals run from gunfire)
- https://steamcommunity.com/sharedfiles/filedetails/comments/2601296065 (search extract: manhunter complaints)
- https://steamcommunity.com/app/294100/discussions/0/1746720717352881058/ (search extract: wolves in pairs)
- https://steamcommunity.com/sharedfiles/filedetails/?id=3679396881 (search extract: vanilla's nearest-smallest prey bias, per a mod's description)

## Confidence

- **Stat table — high** for the rows read on both list pages and confirmed on the species pages
  (grizzly, wolf); **medium** for the rest, whose columns were mapped by inference and checked on
  two rows. **Low** for the caribou row's gaps.
- **Melee attacks — high** for bear, wolf and elk; none read for the rest.
- **Manhunter trigger, duration, doors, pack event — high** (wiki pages read whole).
- **Herd join radius — medium** (from `a-18`); the join chance "about the same as *Rev*" is
  community (**low**).
- **Predator logic — medium-low**: nearest-and-smallest and the 30–35 % trigger come from a
  player guide and a mod description; the difficulty switch is **high**.
- **Flee rule — medium** (wiki summary plus a forum thread).
- **Difficulty factors — high.**
- **The recommendation's numbers** that say *ours* are judgement, to be tuned by playing.

## Could not be determined

- Any animal's **maximum prey body size**, and whether prey choice weighs combat power at all.
- The exact **distance term** in the revenge roll and how it combines with stealth (only "×3 close").
- **Wild group sizes** per kind (deer herds, wolf packs) and **biome commonality** numbers.
- **Nocturnal or diurnal** flags for any of these species in the current version.
- **Armour** values for any animal; the grizzly's first-strike stun as a general rule or a bear-only one.
- Whether the 0.2-size animals' 31 meat is a floor or a separate value.
- The **flee distance** and flee chance when a shot lands nearby.
