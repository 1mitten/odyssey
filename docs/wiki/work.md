# Work and skills

The work types a colonist can be assigned, in priority order of urgency, and the skills that govern how well they do them. Salvaging is ours: it sits beside mining because taking a ruin apart without wrecking what is inside it is a different craft from digging.

36 entries, 2 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Work types

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Firefighting** | `ui.work.firefighting` | Put out fires. Always first | sheet 05 (tools and weapons), low | M7 |
| **Patient** | `ui.work.patient` | Accept treatment when hurt | sheet 06 (action tiles), high | M7 |
| **Bed rest** | `ui.work.bedrest` | Stay in bed while recovering | sheet 08 (salvage gear), high | M7 |
| **Doctor** | `ui.work.doctor` | Tend the injured and operate | sheet 08 (salvage gear), high | M7 |
| **Warden** | `ui.work.warden` | Feed, talk to and recruit prisoners | sheet 06 (action tiles), med | M7 |
| **Handling** | `ui.work.handling` | Tame, train and feed animals | sheet 06 (action tiles), high | M7 |
| **Cooking** | `ui.work.cooking` | Prepare meals and butcher | sheet 06 (action tiles), high | M7 |
| **Hunting** | `ui.work.hunting` | Kill wild animals for meat | sheet 06 (action tiles), high | M7 |
| **Construction** | `ui.work.construction` | Build frames and blueprints | sheet 06 (action tiles), med | M7 |
| **Growing** | `ui.work.growing` | Sow and harvest growing zones | sheet 06 (action tiles), high | M7 |
| **Mining** | `ui.work.mining` | Dig, and clear rubble | sheet 05 (tools and weapons), high | M7 |
| **Salvaging** | `ui.work.salvaging` | Strip shells and extract salvage | sheet 05 (tools and weapons), med | M7 |
| **Chopping** | `ui.work.cutting` | Fell trees and clear growth | sheet 05 (tools and weapons), high | M7 |
| **Hauling** | `ui.work.hauling` | Move things to storage | sheet 08 (salvage gear), low | M7 |
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
| **Mining** | `ui.skill.mining` | Digging, rubble clearance and salvage extraction | sheet 06 (action tiles), high | M2 |
| **Salvage** | `ui.skill.salvage` | Stripping a shell without wrecking what is in it | sheet 06 (action tiles), high | M2 |
| **Cooking** | `ui.skill.cooking` | Meal quality, and food poisoning avoided | sheet 06 (action tiles), high | M2 |
| **Growing** | `ui.skill.growing` | Yield and harvest speed | sheet 06 (action tiles), high | M2 |
| **Chopping** | `ui.skill.cutting` | Felling trees. How fast the axe comes down, and how fast the trunk goes over <br>**Needs:** an axe head, edge-on | no art | M2 |
| **Animals** | `ui.skill.animals` | Taming, training and husbandry | sheet 06 (action tiles), high | M2 |
| **Crafting** | `ui.skill.crafting` | Bench work and item quality | sheet 06 (action tiles), high | M2 |
| **Fabrication** | `ui.skill.fabrication` | Advanced production. Gates the best gear | sheet 06 (action tiles), high | M2 |
| **Medicine** | `ui.skill.medicine` | Tending, surgery and survival odds | sheet 06 (action tiles), high | M2 |
| **Social** | `ui.skill.social` | Negotiation, recruitment and warden work | sheet 08 (salvage gear), low | M2 |
| **Shooting** | `ui.skill.shooting` | Ranged accuracy | sheet 06 (action tiles), high | M2 |
| **Melee** | `ui.skill.melee` | Close combat, hit and parry | sheet 06 (action tiles), high | M2 |
| **Intellect** | `ui.skill.intellect` | Research speed | sheet 06 (action tiles), high | M2 |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
