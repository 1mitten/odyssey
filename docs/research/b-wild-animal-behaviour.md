# Lane B — Wild-animal temperament beyond RimWorld: what other games do, what the animals do, what is cheap

## Question

Odyssey is adding ten wild forest species — rabbit, fox, raccoon, skunk, deer (doe and stag),
moose (cow and bull), boar, wolf, bear. Beyond RimWorld, how do other games and the real animals
shape wild-animal temperament so that each species plays differently, and which behaviours are
cheap to simulate but high in character?

Budget: 12 web searches and 8 page fetches, all used (one fetch, the Going Medieval Fandom wiki,
returned HTTP 402; its content below comes from the search snippet of the same page). Anything
marked **(recalled)** was not read in this pass.

## Findings

### 1. Other games' animal models

**Dwarf Fortress** — temperament is a set of independent **flags on the creature**, not a
single enum, and the combinations make the species (DF wiki, *Creature token*, read):

- `BENIGN`: "non-aggressive by default … running away from any creatures that are not friendly to
  it" — and soldiers will not engage it automatically.
- `LARGE_PREDATOR`: "will attack other creatures that are smaller than it" — a size comparison, not
  a hostility list.
- `AMBUSHPREDATOR`: starts hidden and stays put "until its prey draws near".
- `CURIOUSBEAST_EATER` / `_ITEM` / `_GUZZLER`: steals and eats edible items from the site and
  escapes with them; steals the highest-value item; drinks or spills the alcohol. The thief is its
  own temperament, separate from the fighter.
- `FLEEQUICK`: "will flee at the first sign of resistance".
- `CRAZED`: attacks everything except its own crazed kin (the rabies shape).
- `HUNTS_VERMIN`, `CARNIVORE`, `NOCTURNAL` (appears only 22:05–04:30).
- Warning to the player: an announcement when a thief or predator arrives **(recalled)**.

**Going Medieval** — three temperaments (Fandom *Wildlife*, via search snippet): **passive**
animals flee when approached or hunted; **skittish** animals flee and "attack only when wounded";
**aggressive** animals attack. Wolves "retaliate quickly when shot and force the settler hunting
them into melee"; there are two wolf kinds, **wanderers** and **ferocious** (a wolf raid). Bears are
rare, found **in pairs near cave entrances** — a habitat anchor.

**Farthest Frontier** — every predator has a **home on the map** (official guide, read; Steam
threads, search): wolves roam near **dens** and attack villagers who come too close; boars are
territorial round **nests**, attack on approach "though they may flee if startled", and hunters
leave them alone unless threatened; bears are "curious scavengers" that **rummage through homes
and stores looking for food**, cannot be cleared by destroying a lair (they are a random event),
and are too fast for anyone but cavalry. Wolf bites can transmit **rabies**, "almost always
lethal". Livestock unfenced are prey. The warning is spatial (you learn where the den is) plus an
**alarm bell** that pulls villagers indoors (Steam thread, search).

**Valheim** — senses, not tempers (Valheim wiki *Creature AI*, read; *Creature senses*, search):
a creature must **see or hear** a target; noise (running and jumping 30, attacks by weapon) alerts
from further than sight; sneaking is silent. **Deer always flee** a detected player and never
chase; **boars attack**. Everything flees fire within 3 m; non-bosses flee after 30 s unable to
attack while taking damage; aggro drops after 30 s unseen/unheard or past a chase distance.

**Don't Starve** — **seasonal state made visible** (DS wiki *Beefalo*, read): beefalo are
neutral, but twice a year (mid-summer, mid-winter) they are **in heat and hostile to anything
nearby** — and the tell is on the animal: "in-heat beefalo have red rears". Attack one and **five
in the vicinity turn**, but those not struck "lose interest quickly". Calves follow the parent,
cannot attack, run when approached. Hound waves are announced by a growl that rises before they
arrive **(recalled)**.

**Project Zomboid (Build 42)** — deer move **in families and herds** simulated off-screen; wild
animals are **stressed** by threats and **flee on long timers**; hunting is about closing distance
without spooking; tracking reads tracks, dung, feeding and sleeping signs (PZ guides, search).

**RimWorld, for contrast (recalled)** — one wildness number, a per-species **chance to turn on a
hunter** when harmed, predators that hunt pawns when hungry, and the *manhunter* state as the
dangerous mode. Most species play the same until shot, which is the gap this lane is about.

**Kenshi, Banished, Red Dead Redemption 2, Ark (all recalled, not searched):** Kenshi's animals are
roaming packs that fight whatever is near and scavenge the fallen. Banished has no wild predators;
deer are a hunting yield. RDR2's predators give **threat displays** (a bear rears and roars, a wolf
pack circles and growls) before committing, and prey herds scatter together. Ark labels each
species with a one-word temperament (passive, skittish, neutral, territorial, aggressive) in its
survivor's notes, which is the player-facing half of DF's flags.

**Across the games**, the axes that recur are: *what it does when approached* (ignore, flee,
freeze, display, attack); *what turns it* (being hurt, its young, a season, hunger, night);
*whether the group turns with it*; and *whether it wants something of yours* (livestock, stores,
crops). The warning is either **on the animal** (red rear, display) or **on a place** (den, nest).

### 2. The real animals, translated

Group sizes, hours and flight distances below are largely **(recalled)** unless a source is named;
distances are field-guide orders of magnitude for unhabituated animals, not measured values.

| Species | Group | Active | Flight from a person | Dangerous when | Eats (of ours) | Signature behaviour |
|---|---|---|---|---|---|---|
| **Rabbit** | loose; solitary to feeding groups (recalled) | dawn, dusk, night (recalled) | short, ~5–15 m — relies on **freezing** first, then bolts in a zigzag to cover (recalled) | never | crops, garden greens (recalled) | freeze → bolt; thumps a hind foot as alarm (recalled) |
| **Fox** | pair or family group — a pair and young, sometimes helpers (Wikipedia, read) | night, dusk (recalled; not in the fetched text) | moderate; urban foxes very tolerant (Wikipedia notes they colonise towns) | **rabid** (recalled) | small animals, rabbits, **fruit and vegetables** (Wikipedia, read); poultry, refuse (recalled) | **caches** surplus food and returns to it; high-arc mouse pounce (recalled) |
| **Raccoon** | solitary; mother and kits (recalled) | night (recalled) | short and habituates fast (recalled) | rabid, or cornered (recalled) | **anything** — bins, stores, crops (maize), eggs (recalled) | raids food stores and opens containers; handles food with forepaws (recalled) |
| **Skunk** | solitary (recalled) | night (recalled) | very short — it does not need to flee | never lethal; sprays when pressed | insects, grubs, eggs, refuse (recalled) | **escalation ladder**: freeze, tail up, **foot-stamping** audible for several metres, turn the rear, spray accurate to ~4.5 m (15 ft) (skunk warning sources, search). The handstand is the *spotted* skunk's (same sources) |
| **Deer** (doe, stag) | does in family groups; stags apart outside the rut (recalled) | dawn and dusk (recalled) | long in the open, shorter in cover; groups of **three or more flee more readily** (Lagory 1987, search) | a **stag in the autumn rut**, or cornered (recalled) | crops, orchards, gardens (recalled) | **alarm snort** when it detects a predator; **foot-stamp** that alerts other deer; **tail flag** while fleeing that keeps the group together (Lagory; Caro et al. 1995, search) |
| **Moose** (cow, bull) | solitary; cow with calf (ADF&G, read) | dawn, dusk, but anytime (recalled) | short — **stands its ground** rather than flees | **bull in the rut, late September–October; cow with calf, late spring–summer; in winter deep snow, hungry and tired; harassed by dogs** (ADF&G, read) | browse; gardens (recalled) | warning: stops feeding and stares, **ears pinned, hackles raised on the hump, licks its lips**; "**most moose charges are bluffs**"; if it connects it may stomp and kick. More Alaskans are injured by moose than by bears (ADF&G, read) |
| **Boar** | **sounder** of one or more sows and several generations, usually 2–20, up to 40+; adult males solitary (Virginia Tech, search) | any time, **nocturnal when disturbed** or hot (Virginia Tech, search) | shy, avoids people (search) | **cornered, threatened, protecting young, or wounded** — a wounded boar attacks the hunter (search) | **almost any crop**; roots up fields and wallows (search) | threatened sounder **circles up, adults facing out, young inside** (search) |
| **Wolf** | pack (family) (recalled: ~4–8) | dusk, night; more by day in winter (recalled) | long; wolves avoid people | **rabies** (382 of 491 credible victims 2002–2020), **habituation**, provocation (cornering, entering a den), and predation only in **heavily modified landscapes without wild prey** (Linnell et al. 2021, search) | livestock, deer, carrion (recalled) | hunts as a group and tests prey by chasing; howls to gather (recalled) |
| **Bear** | solitary; sow with cubs (recalled) | day and dusk; **hibernates in winter** (recalled) | moderate; food-conditioned bears lose it (recalled) | **surprised at close range, a sow with cubs, guarding a carcass** (defensive); predatory attacks are rare, silent and persistent (NPS, read; search) | omnivore — **stores, bins, crops, carcasses** (recalled) | **defensive ladder**: huffing, jaw-popping/teeth-clacking, paw-pounding, then a **bluff charge** — head and ears up, bounding, stops short or veers — which is the commoner charge; running from a bluff can trigger a real attack (NPS, read). A predatory bear gives **no** warning (NPS, read) |

The single most useful real-world finding for design: **in the large, dangerous species the
danger is conditional and mostly signalled** — moose and bears both bluff far more than they
connect, both display first, and both are dangerous for a *reason* (young, rut, surprise, food,
hunger). The unsignalled attack (a predatory bear, a rabid animal) is the rare, frightening case,
which is exactly its dramatic value.

### 3. Signature mechanics that give character cheaply

Each is judged against what the project already has: `SpeciesDef`/`PawnKindDef`, the one-node
animal mind, `SpeciesDef.nocturnal`, a bank habitat radius, `AnimalShelterThinkNode`, combat
through `CombatSystem.Hurt`, mood thoughts, growing zones, stores and shelves, raids' band as a
group that acts as one, the calendar's seasons (design 29, 30, 33, 43, 55).

| Mechanic | Character | Cost | Why |
|---|---|---|---|
| **Response on approach** as Def data: flee distance, and what happens inside it (bolt, freeze-then-bolt, stand, display, attack) | very high | very low | One distance check per animal against the nearest person; one enum. This alone separates rabbit, deer, moose, skunk and bear. |
| **Warning display before attack** (a timed "threat" state: stamp, huff, ears back; ends in a bluff charge that stops short, or a real one) | very high | low | One state, one duration, one floater and an animation or computed pose; the colonist walking away *is* the counterplay. Real bears and moose bluff more than they strike. |
| **Retaliation chance when hurt** per species | high | very low | A roll in `Hurt`'s aftermath. Going Medieval's *skittish* (flee, fight only when wounded) is one number. |
| **Herd flight contagion** (one flees, its group within R flees the same way) | high | low | A broadcast to group members on the flee transition; the tail flag is a presentation detail. |
| **Group retaliation** (a struck member turns N neighbours; unstruck ones lose interest soon) | high | low | Don't Starve's rule; the boar sounder circling up is the same broadcast with the young inside. |
| **Seasonal and life-stage aggression** (rut: stag, bull moose in autumn; young: sow bear, cow moose, sow boar in spring–summer) | high | low–medium | A season window on the Def, or a "has young nearby" test once juveniles exist. **Must be visible on the animal and its pane** — Don't Starve's red rear is the lesson. |
| **Skunk spray as a debuff**: a mood thought on the sprayed colonist, a smaller one on people who share her room, lasting about a day | high | low | A thought and a trigger at the end of the skunk's ladder; no damage at all. Funny, memorable and harmless. |
| **Activity hours** (nocturnal fox, raccoon, skunk; crepuscular deer; bear asleep in winter) | medium | very low | `nocturnal` exists; a crepuscular window and a hibernation season are two more fields. |
| **Crop raiding** (rabbit, deer, boar eat or uproot sown crops) | high | medium | A leg whose destination is a crop cell, an eat step that removes growth. Gives walls and fences a job. |
| **Store raiding** (raccoon, bear, fox take food from stores or the ground and leave) | very high | medium–high | Reuses the thief-that-leaves shape combat already has; needs the item taken and carried off. DF's curious beasts and Farthest Frontier's bears. |
| **Rabid individual** (a flag: out by day, no flight, attacks on sight; fox, raccoon, wolf) | high | low as a flag, high with transmission | DF's `CRAZED` as behaviour is cheap; infection belongs to health's later units. Real wolf attacks are mostly rabies. |
| **Predator culls prey** (wolf, fox take rabbits and fawns) | medium | medium–high | Animal-on-animal hunting and corpses of animals; lively, but the player mostly sees its results. |
| **Scavenging corpses** (bear, wolf, fox, raccoon come to a carcass) | medium | medium | Needs animal corpses to exist; a bear guarding a carcass is then free. |
| **Pack and winter boldness** (wolves as one band; hunger in winter lowers their flight distance) | high | medium | The raid band already acts as one; winter is a season modifier on the flight distance. |
| **Dens and nests as places** (Farthest Frontier) | medium | medium | A home cell per group anchors the danger in space; wander radius about it already exists in shape. |
| **Food caching** (fox buries surplus) | low | medium | Charming, invisible without close watching. |

## Recommendation

**Adopt a temperament made of a few independent Def fields, not one enum** — DF's lesson — read
by the existing single think node, in this order of value for cost:

1. **Flight distance and an approach response** per species (`bolt`, `freezeThenBolt`, `stand`,
   `display`, `attack`), in cells. Every species plays differently from this alone.
2. **A warning display state** before any attack a species starts itself: duration, a tell, and
   a bluff chance (the charge that stops short). Default a large animal to bluffing; make the
   silent, unsignalled attack rare and specific (a hungry winter wolf pack, a rabid animal).
3. **Retaliation chance when hurt**, and **group response**: flee together (prey) or turn together
   (sounder, pack) within a radius, with the unstruck losing interest after a short time.
4. **Seasonal aggression windows** (the rut) and, once young exist, **protective young**,
   each shown on the animal and in its pane — never a hidden state.
5. **Skunk spray as a mood thought** — the cheapest memorable thing on this list.
6. **Activity hours**: add crepuscular and hibernating beside `nocturnal`.
7. **Crop raiding**, then **store raiding** (raccoon first — it is the whole of the raccoon).
8. Later: rabid individuals as a behaviour flag, then pack hunting with winter boldness, then
   predators culling prey and scavenging, then dens.

The two ties worth naming: store raiding against crop raiding — crop raiding is cheaper and
reuses growing zones, store raiding has more character; **the observation that breaks it is
whether players already defend a field** (if growing zones sit unwalled, crop raiding creates a
decision at once). Cheapest experiment: one deer kind that eats a crop cell, in one playtest.

**What makes each species play differently (one line each):**

- **Rabbit** — freezes when you come near, then bolts zigzagging; eats your carrots; never fights.
- **Fox** — a night visitor that keeps its distance and snatches small food or a rabbit; dangerous only when rabid.
- **Raccoon** — the thief: comes at night, walks into your stores and leaves with food; fights only when cornered.
- **Skunk** — does not run; stamps, lifts its tail, turns — keep walking and the colonist is sprayed and miserable for a day.
- **Deer (doe)** — the long flight distance of the forest; one snort and the whole group flags and flees; grazes crops at dusk.
- **Deer (stag)** — the doe's timidity for most of the year, but in the autumn rut it stamps and may charge a colonist who crowds it.
- **Moose (cow)** — does not flee; stands, ears back, and bluff-charges anyone near her calf in spring and summer.
- **Moose (bull)** — the rut's real danger: in autumn it holds its ground and charges; more dangerous in practice than the bear.
- **Boar** — shy until hurt or cornered, then the sounder turns together with the young in the middle; roots up fields at night.
- **Wolf** — rarely seen and avoids people, but a pack acts as one; in hard winter it grows bold and takes stock or the lone colonist.
- **Bear** — huffs, clacks and bluff-charges if surprised or near cubs; raids stores for food; sleeps through winter; the silent, predatory bear is its rare nightmare.

## Sources

- https://www.dwarffortresswiki.org/index.php/Creature_token (read)
- https://goingmedieval.fandom.com/wiki/Wildlife (search snippet; fetch refused, HTTP 402)
- https://goingmedieval.fandom.com/wiki/Wolf (search snippet)
- https://www.farthestfrontier.com/guide/gameplay/combat/ (read)
- https://steamcommunity.com/app/1044720/discussions/0/598540359040082586/ (search snippet)
- https://valheim.weirdgloop.org/w/Creature_AI (read)
- https://valheim.fandom.com/wiki/Creature_senses (search snippet)
- https://dontstarve.wiki.gg/wiki/Beefalo (read)
- https://pzwiki.net/wiki/Animal and https://steamcommunity.com/sharedfiles/filedetails/?id=3518228334 (search snippets)
- https://www.adfg.alaska.gov/index.cfm?adfg=livewith.aggressivemoose (read)
- https://www.nps.gov/articles/bearattacks.htm (read)
- https://www.outdoorlife.com/how-to-decode-black-bears-body-language/ and https://bearsmart.org/about-bears/communication (search snippets)
- https://thefaunalist.com/wildlife/skunk-sounds/ and https://blog.nature.org/2024/08/11/this-skunk-does-handstands-yes-handstands/ (search snippets)
- https://www.sciencedirect.com/science/article/abs/pii/S0003347287802063 (Lagory 1987, abstract via search)
- https://academic.oup.com/beheco/article-abstract/6/4/442/187306 (Caro et al. 1995, abstract via search)
- https://www.pubs.ext.vt.edu/CNRE/cnre-146/cnre-146.html (feral swine, search snippet)
- https://wolf.org/wp-content/uploads/2021/11/WolfAttacksUpdate.pdf (Linnell et al. 2021, search snippet)
- https://en.wikipedia.org/wiki/Red_fox (read; the fetched text omitted activity hours and caching)

## Confidence

- **DF flags, Valheim senses, Don't Starve beefalo, Farthest Frontier predators**: **high** (read at source).
- **Going Medieval temperaments**: **medium** (search snippet of the wiki page, not the page).
- **Project Zomboid**: **medium-low** (guides via snippets). **RimWorld, Kenshi, Banished, RDR2, Ark**: **low** (recalled).
- **Moose, bear, skunk ladders; deer alarm signals; boar sounder; wolf attack causes**: **high** (agency and peer-reviewed sources, read or abstracted).
- **Group sizes, active hours and flight distances for rabbit, fox, raccoon, skunk, wolf, bear**: **low–medium** (mostly recalled; flight distances are orders of magnitude only).
- **The value-for-cost ranking**: **medium** — costs are judged from the design documents' descriptions of the existing seams, not from the code.

## Could not be determined

- Measured flight-initiation distances per species toward people; the deer study relates flight to distance and group size but the numbers were not read.
- Fox caching and raccoon food-raiding behaviour at a source (no search budget left; recalled).
- Going Medieval's exact trigger distances and whether its "ferocious" wolves are an event or a spawn kind.
- How Kenshi, RDR2 and Ark actually implement their displays (recalled only).
- Whether RimWorld-style tuning values would suit a 2.5 m cell; every distance should be a Def field tuned in play.
