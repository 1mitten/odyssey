# Colonists

Who is on the map, what they need, how they feel and what they are doing right now. This is the section with the least art: no sheet contains a human figure. Traits, thoughts and mental breaks are design 44: a thought's description here is its tooltip on the Thoughts tab, and a trait's is its tooltip on the colonist's pane and the select screen.

89 entries, 71 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Kinds of pawn

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Colonist** | `ui.pawn.colonist` | One of ours <br>**Needs:** a human figure. No sheet contains one, and this is the most-used icon in the HUD | no art | M2 |
| **Visitor** | `ui.pawn.visitor` | Here peacefully, and watching <br>**Needs:** human figure, neutral | no art | M2 |
| **Hostile** | `ui.pawn.hostile` | Will attack <br>**Needs:** human figure, hostile | no art | M2 |
| **Prisoner** | `ui.pawn.prisoner` | Held, and someone must feed them <br>**Needs:** human figure, restrained | no art | M2 |
| **Animal** | `ui.pawn.animal` | Tame or wild creature | sheet 06 (action tiles), med | M2 |
| **Synth** | `ui.pawn.synth` | Machine intelligence. Not alive, not harmless <br>**Needs:** a machine intelligence. Nothing in the sheets is recognisably robotic | no art | M2 |
| **Corpse** | `ui.pawn.corpse` | Rots, upsets people, can be buried or worse | sheet 07 (anatomy), high | M2 |
| **Midden hog** | `ui.pawn.hog` | Pig-descended, thrives on refuse heaps. Wild for now; the one you meet first | no art | AN |
| **Duct rat** | `ui.pawn.rat` | Vermin from the ducts and the caverns. Climbs anything | no art | AN |
| **Bandit** | `ui.pawn.bandit` | Masked in a welding helmet and a red vest, with a crowbar or a bat. Hunts whoever is still standing | no art | CB |

## Needs

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Food** | `ui.need.food` | How hungry this colonist is | sheet 07 (anatomy), high | M2 |
| **Rest** | `ui.need.rest` | Sleep debt | sheet 08 (salvage gear), high | M2 |
| **Recreation** | `ui.need.joy` | Time spent on anything enjoyable <br>**Needs:** something unmistakably recreational: dice, cards, a games board | no art | M2 |
| **Comfort** | `ui.need.comfort` | Quality of what they sit and sleep on <br>**Needs:** an armchair or cushion, read as comfort rather than as furniture | no art | M2 |
| **Beauty** | `ui.need.beauty` | How the surroundings look to them | sheet 06 (action tiles), low | M2 |
| **Space** | `ui.need.space` | Room to move. Cramped quarters grate <br>**Needs:** an abstract for room to move. Hardest of the needs to draw | no art | M2 |
| **Outdoors** | `ui.need.outdoors` | Time under open sky. Some need it, some dread it | sheet 06 (action tiles), med | M2 |
| **Cleanliness** | `ui.need.hygiene` | Filth underfoot and in the air | sheet 08 (salvage gear), med | M2 |
| **Mood** | `ui.need.mood` | How good this colonist feels overall | sheet 07 (anatomy), high | M2 |

## Mood states

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Content** | `ui.mood.content` | Mood is comfortable <br>**Needs:** mood face, content. No sheet has a human face at all | no art | M2 |
| **Strained** | `ui.mood.strained` | Mood is falling. Watch this one <br>**Needs:** mood face, strained | no art | M2 |
| **Breaking** | `ui.mood.breaking` | At the threshold of a mental break <br>**Needs:** mood face, at the break threshold | no art | M2 |
| **Breaking down** | `ui.mood.broken` | In a mental break now <br>**Needs:** mood face, in a break | no art | M2 |

## Traits

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Tireless** | `ui.trait.tireless` | Works at everything far faster than most. Nobody has seen her sit down | no art | TM |
| **Diligent** | `ui.trait.diligent` | Works at everything faster than most | no art | TM |
| **Unhurried** | `ui.trait.unhurried` | Works at everything more slowly than most, and will not be rushed | no art | TM |
| **Cheerful** | `ui.trait.cheerful` | Her mood sits a little higher, whatever the day brings | no art | TM |
| **Sunny** | `ui.trait.sunny` | Her mood sits well above everyone else's | no art | TM |
| **Gloomy** | `ui.trait.gloomy` | Her mood sits a little lower, whatever the day brings | no art | TM |
| **Steady** | `ui.trait.steady` | Takes a good deal more than most before she breaks | no art | TM |
| **Jumpy** | `ui.trait.jumpy` | Breaks sooner than most when things go badly | no art | TM |
| **Quick study** | `ui.trait.quickstudy` | Learns every skill much faster than most | no art | TM |
| **Slow study** | `ui.trait.slowstudy` | Learns every skill slowly | no art | TM |
| **Soft hands** | `ui.trait.softhands` | Will not mine. Never has, never will | no art | TM |
| **Black thumb** | `ui.trait.blackthumb` | Will not work a growing zone | no art | TM |
| **Ham-fisted** | `ui.trait.hamfisted` | Will not build | no art | TM |

## Thoughts

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Blew off steam** | `ui.thought.catharsis` | The aftermath of a mental break that ran its course. Lighter for a while | no art | TM |
| **Ate a meal** | `ui.thought.atemeal` | A proper meal. A second in a row counts for less than the first | no art | TM |
| **Slept on the ground** | `ui.thought.sleptonground` | A night in the dirt with no bed under her | no art | TM |
| **Fell through a floor** | `ui.thought.fell` | Rode a collapsing floor down. Badly shaken, and a second fall adds to it | no art | TM |
| **Slept cold** | `ui.thought.sleptcold` | Woke from a night colder than is comfortable | no art | TM |
| **Slept too hot** | `ui.thought.slepthot` | Woke from a night hotter than is comfortable | no art | TM |
| **Attacked by a colonist** | `ui.thought.attackedbycolonist` | One of the colony swung at her. Lasts a day, from the latest swing | no art | TM |
| **A colonist died** | `ui.thought.colonistdied` | Felt by every other colonist for three days, up to three deaths at once | no art | TM |
| **Hunger** | `ui.thought.hunger` | Hungry costs mood; starving costs more. Eating clears it | no art | TM |
| **Tiredness** | `ui.thought.tiredness` | Tired costs mood; exhausted costs more. Sleep clears it | no art | TM |
| **Recreation** | `ui.thought.recreation` | Idle time lifts it and a long stretch of work without any wears it down | no art | TM |
| **Temperature** | `ui.thought.temperature` | Too cold or too hot where she is standing | no art | TM |

## Mental breaks

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Daze** | `ui.break.wander` | A minor break: wanders aimlessly and does no work | no art | TM |
| **Sulk** | `ui.break.sulk` | A minor break: goes to her own bed and stays there, doing nothing | no art | TM |
| **Food binge** | `ui.break.binge` | A major break: eats whatever food she can reach, over and over | no art | TM |
| **Tantrum** | `ui.break.tantrum` | A major break: smashes whatever the colony has built near her. Beds are spared | no art | TM |
| **Berserk** | `ui.break.berserk` | An extreme break: attacks whoever is nearest. Draft the colony to bring her down; nobody is killed | no art | TM |

## The Thoughts tab's words

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Traits** | `ui.mind.traits` | Who she is: dealt when she arrives, and hers for good | no art | TM |
| **Work** | `ui.mind.work` | How much faster or slower than most she works at everything | no art | TM |
| **Learns** | `ui.mind.learns` | How much faster or slower than most she learns every skill | no art | TM |
| **Breaks sooner** | `ui.mind.breakssooner` | Her mood need not fall as far before she can break | no art | TM |
| **Breaks later** | `ui.mind.breakslater` | Her mood must fall further than most before she can break | no art | TM |
| **Cannot** | `ui.mind.cannot` | Work she will never do. The Work tab greys it | no art | TM |
| **Now** | `ui.mind.now` | What is weighing on her at this moment, gone when its cause is | no art | TM |
| **Memories** | `ui.mind.memories` | Things that happened to her, each fading after its own time | no art | TM |
| **Heading for** | `ui.mind.target` | The mood everything listed pulls her toward; she drifts there, faster up than down | no art | TM |
| **left** | `ui.mind.left` | How long until a memory's oldest copy fades | no art | TM |
| **Nothing on her mind** | `ui.mind.nothing` | No need is short and no memory is held: she sits at the base | no art | TM |
| **more** | `ui.mind.more` | The rows past what the tab has room for, counted | no art | TM |

## Current activity

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Hauling** | `ui.status.hauling` | Carrying something to storage | sheet 08 (salvage gear), low | M2 |
| **Building** | `ui.status.building` | Working a frame or blueprint | sheet 06 (action tiles), med | M2 |
| **Deconstructing** | `ui.status.deconstructing` | Taking one of our own buildings apart | no art | M3 |
| **Mining** | `ui.status.mining` | Cutting into rubble, concrete or rock | sheet 05 (tools and weapons), high | M2 |
| **Chopping** | `ui.status.felling` | Cutting a tree down for wood <br>**Needs:** an axe swung at a trunk | no art | M3 |
| **Sowing** | `ui.status.sowing` | Breaking ground and planting a zone cell | no art | M3 |
| **Harvesting** | `ui.status.harvesting` | Cutting a ripe crop and gathering it | no art | M3 |
| **Refuelling** | `ui.status.refuelling` | Carrying fuel to a generator and filling it | no art | M3 |
| **Sleeping** | `ui.status.sleeping` | Asleep, and should stay that way | sheet 08 (salvage gear), high | M2 |
| **Eating** | `ui.status.eating` | Taking a meal | sheet 02 (food), high | M2 |
| **Idle** | `ui.status.idle` | Nothing to do. Usually a priorities problem | sheet 06 (action tiles), med | M2 |
| **Recreating** | `ui.status.recreating` | Off duty on purpose <br>**Needs:** off-duty by choice. Blocked on the same missing recreation art as ui.need.joy | no art | M2 |
| **Tending** | `ui.status.tending` | Treating a patient | sheet 06 (action tiles), high | M2 |
| **Downed** | `ui.status.downed` | Cannot move. Needs rescue <br>**Needs:** a figure prone. Blocked on the missing human figure | no art | M2 |
| **Bleeding** | `ui.status.bleeding` | Losing blood. Timed problem | sheet 06 (action tiles), low | M2 |
| **On fire** | `ui.status.burning` | Burning now | sheet 06 (action tiles), high | M2 |
| **Drafted** | `ui.status.drafted` | Under direct order, not the work list | sheet 05 (tools and weapons), med | M2 |
| **Wandering** | `ui.status.wandering` | An animal on a leg of its own: going somewhere nearby for no reason | no art | AN |
| **Resting** | `ui.status.resting` | An animal between legs | no art | AN |
| **Fighting** | `ui.status.fighting` | Closing on a target and swinging at it | no art | CB |
| **Fleeing** | `ui.status.fleeing` | Running from danger, or from whatever hurt it | no art | CB |
| **Equipping** | `ui.status.equipping` | Fetching a weapon to hold | no art | CB |
| **Rescuing** | `ui.status.rescuing` | Carrying the downed to a bed | no art | CB |
| **Stealing** | `ui.status.stealing` | A bandit carrying something off the board | no art | CB |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
