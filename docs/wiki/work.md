# Work and skills

The work types a colonist can be assigned, in priority order of urgency, the skills that govern how well they do them, and the schedule blocks that say when. Salvaging is ours: it sits beside mining because taking a ruin apart without wrecking what is inside it is a different craft from digging. Work and schedule share one tab and one table, so they share a page here.

57 entries, 22 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Work types

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Firefighting** | `ui.work.firefighting` | Put out fires. Always first | sheet 05 (tools and weapons), low | M7 |
| **Patient** | `ui.work.patient` | Accept treatment when hurt | sheet 06 (action tiles), high | M7 |
| **Bed rest** | `ui.work.bedrest` | Stay in bed while recovering | sheet 08 (salvage gear), high | M7 |
| **Doctor** | `ui.work.doctor` | Treat the hurt, with medical supplies or without | sheet 08 (salvage gear), high | M3 |
| **Warden** | `ui.work.warden` | Feed, talk to and recruit prisoners | sheet 06 (action tiles), med | M7 |
| **Handling** | `ui.work.handling` | Tame, train and feed animals | sheet 06 (action tiles), high | M7 |
| **Cooking** | `ui.work.cooking` | Work the bills at an electric cooker or a campfire | sheet 06 (action tiles), high | M3 |
| **Hunting** | `ui.work.hunting` | Kill wild animals for meat | sheet 06 (action tiles), high | M7 |
| **Construction** | `ui.work.construction` | Deliver, build and deconstruct, and lay power lines | sheet 06 (action tiles), med | M7 |
| **Growing** | `ui.work.growing` | Sow and harvest growing zones, and pick berries | sheet 06 (action tiles), high | M7 |
| **Mining** | `ui.work.mining` | Dig, and clear rubble | sheet 05 (tools and weapons), high | M7 |
| **Salvaging** | `ui.work.salvaging` | Strip shells and extract salvage | sheet 05 (tools and weapons), med | M7 |
| **Chopping** | `ui.work.cutting` | Fell trees and clear growth | sheet 05 (tools and weapons), high | M7 |
| **Hauling** | `ui.work.hauling` | Move things to storage, and refuel generators | sheet 08 (salvage gear), low | M7 |
| **Cleaning** | `ui.work.cleaning` | Clear filth from rooms | sheet 08 (salvage gear), low | M7 |
| **Research** | `ui.work.research` | Work the research bench | sheet 06 (action tiles), high | M7 |
| **Crafting** | `ui.work.crafting` | General bench work | sheet 03 (camp and crafting), high | M7 |
| **Tailoring** | `ui.work.tailoring` | Make apparel from fabric and leather | sheet 04 (manufactured), high | M7 |
| **Fabrication** | `ui.work.fabrication` | Advanced production | sheet 06 (action tiles), med | M7 |
| **Art** | `ui.work.art` | Make sculptures and decoration | sheet 06 (action tiles), low | M7 |
| **Operating** | `ui.work.operating` | Run powered machinery | sheet 04 (manufactured), high | M7 |
| **Rescue** | `ui.work.rescue` | Carry the downed to a bed <br>**Needs:** carrying a casualty. Blocked on the missing human figure | no art | M7 |

## Skills

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Construction** | `ui.skill.construction` | Building, and the quality of what is built | sheet 06 (action tiles), high | M2 |
| **Mining** | `ui.skill.mining` | Digging and rubble clearance: how fast | sheet 06 (action tiles), high | M2 |
| **Salvage** | `ui.skill.salvage` | Stripping a shell without wrecking what is in it | sheet 06 (action tiles), high | M2 |
| **Cooking** | `ui.skill.cooking` | How fast a colonist cooks, and how seldom the meal burns | sheet 06 (action tiles), high | M3 |
| **Growing** | `ui.skill.growing` | Sowing, harvesting and picking: how fast | sheet 06 (action tiles), high | M2 |
| **Chopping** | `ui.skill.cutting` | Felling trees. How fast the axe comes down, and how fast the trunk goes over | sheet 09 (?), high | M2 |
| **Animals** | `ui.skill.animals` | Taming, training and husbandry | sheet 06 (action tiles), high | M2 |
| **Crafting** | `ui.skill.crafting` | Bench work and item quality | sheet 06 (action tiles), high | M2 |
| **Fabrication** | `ui.skill.fabrication` | Advanced production. Gates the best gear | sheet 06 (action tiles), high | M2 |
| **Medicine** | `ui.skill.medicine` | How fast a colonist treats the hurt, and how well | sheet 06 (action tiles), high | M2 |
| **Social** | `ui.skill.social` | Negotiation, recruitment and warden work | sheet 08 (salvage gear), low | M2 |
| **Shooting** | `ui.skill.shooting` | Ranged accuracy | sheet 06 (action tiles), high | M2 |
| **Melee** | `ui.skill.melee` | Close combat: landing a blow, and dodging one | sheet 06 (action tiles), high | M2 |
| **Intellect** | `ui.skill.intellect` | Research speed | sheet 06 (action tiles), high | M2 |

## Schedule blocks

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Anything** | `ui.schedule.anything` | No instruction: work, rest or idle as needs dictate | no art | M7 |
| **Work** | `ui.schedule.work` | Take jobs from the priority grid this hour | no art | M7 |
| **Sleep** | `ui.schedule.sleep` | Go to bed whether tired or not | no art | M7 |
| **Recreation** | `ui.schedule.recreation` | Rest and recover mood | no art | M7 |
| **Eat** | `ui.schedule.eat` | Take a meal even if not yet hungry | no art | M7 |
| **Meditate** | `ui.schedule.meditate` | Quiet hours, for whatever comes to need them | no art | M7 |

## Recipes

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Cook a meal** | `ui.recipe.meal` | Any raw food, three carrots' worth, into one meal <br>**Needs:** a pan with food in it. Our own concept | no art | M3 |

## Bills, and the words of a station's pane

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Bills** | `ui.bill.heading` | What this station is told to make. The top one is worked first <br>**Needs:** a list of orders pinned up. Our own concept | no art | M3 |
| **Add a bill** | `ui.bill.add` | Put a new order at the bottom of the list <br>**Needs:** a plus. Interface furniture drawn as a HudGlyph | no art | M3 |
| **No bills yet** | `ui.bill.none` | Nothing will be made here until a bill is added <br>**Needs:** an empty list. Interface words, no icon wanted | no art | M3 |
| **Until you have** | `ui.bill.mode.until` | Cook while the colony holds fewer than this many meals, and start again when it drops below <br>**Needs:** Interface words, no icon wanted | no art | M3 |
| **Make** | `ui.bill.mode.times` | Cook this many, then stop <br>**Needs:** Interface words, no icon wanted | no art | M3 |
| **Forever** | `ui.bill.mode.forever` | Never stop cooking <br>**Needs:** Interface words, no icon wanted | no art | M3 |
| **Paused** | `ui.bill.suspended` | Stopped by you. The next bill down is worked instead <br>**Needs:** a pause mark. Interface furniture drawn as a HudGlyph | no art | M3 |
| **Done** | `ui.bill.done` | This bill's count is met. It starts again by itself when it needs to <br>**Needs:** a tick. Interface furniture drawn as a HudGlyph | no art | M3 |
| **No power** | `ui.bill.unpowered` | The electric cooker needs power to cook. Nothing on the hob will spoil in the meantime <br>**Needs:** a broken plug. Our own concept | no art | M3 |
| **Waiting for power** | `ui.bill.waiting` | This bill will be worked once the station has power again <br>**Needs:** Interface words, no icon wanted | no art | M3 |
| **Each meal: raw food, about 3 carrots** | `ui.bill.needs` | A meal takes 500 of any raw food by nutrition: three carrots, or meat once there is any <br>**Needs:** Interface words, no icon wanted | no art | M3 |
| **Each meal: raw food, about 3 carrots, and 1 wood** | `ui.bill.needs.fire` | A campfire burns one wood for every meal it cooks <br>**Needs:** Interface words, no icon wanted | no art | M3 |
| **Raw food for {n} meals** | `ui.bill.supply` | What the raw food on the map, in stores and on the ground, would cook into <br>**Needs:** Interface words, no icon wanted | no art | M3 |
| **No raw food on the map** | `ui.bill.nosupply` | Nothing to cook. Grow carrots, or use the debug menu's Give carrots <br>**Needs:** an empty basket. Our own concept | no art | M3 |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
