# Colonists

Who is on the map, what they need, how they feel and what they are doing right now. This is the section with the least art: no sheet contains a human figure.

41 entries, 23 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

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

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
