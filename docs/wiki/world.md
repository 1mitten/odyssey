# World and interface

Weather, the data overlays, the layer controls and the rest of the interface furniture. The six layer visibility modes are decided: see ADR 0006.

46 entries, 23 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Weather

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Clear** | `ui.weather.clear` | Open sky <br>**Needs:** clear sky or sun | no art | M1 |
| **Cloudy** | `ui.weather.cloudy` | Less light, no other effect <br>**Needs:** cloud | no art | M1 |
| **Rain** | `ui.weather.rain` | Puts out fires, waters crops | sheet 06 (action tiles), med | M1 |
| **Storm** | `ui.weather.storm` | Rain, wind and lightning <br>**Needs:** lightning | no art | M1 |
| **Snow** | `ui.weather.snow` | Cold, and it settles | sheet 06 (action tiles), high | M1 |
| **Fog** | `ui.weather.fog` | Sight and shooting suffer <br>**Needs:** fog or mist | no art | M1 |
| **Ashfall** | `ui.weather.ashfall` | Filth from the sky, and no sun <br>**Needs:** ash falling | no art | M1 |
| **Heatwave** | `ui.weather.heatwave` | Everything outdoors is too hot | sheet 06 (action tiles), low | M1 |
| **Cold snap** | `ui.weather.coldsnap` | Everything outdoors is too cold | sheet 06 (action tiles), low | M1 |
| **Toxic fallout** | `ui.weather.toxic` | Do not go outside | sheet 08 (salvage gear), high | M1 |
| **Windstorm** | `ui.weather.windstorm` | Wind power up, everything else down <br>**Needs:** wind | no art | M1 |

## Overlays

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Temperature** | `ui.overlay.temperature` | Warm to cold across the slice | sheet 06 (action tiles), med | M1 |
| **Light** | `ui.overlay.light` | How lit each cell is | sheet 06 (action tiles), med | M1 |
| **Beauty** | `ui.overlay.beauty` | What the room looks like to a colonist | sheet 06 (action tiles), low | M1 |
| **Cleanliness** | `ui.overlay.cleanliness` | Filth, and where it matters | sheet 08 (salvage gear), low | M1 |
| **Roofs** | `ui.overlay.roofs` | What is roofed, and by what <br>**Needs:** a roofed-area mark. Pairs with the missing roof tool icon | no art | M1 |
| **Zones** | `ui.overlay.zones` | Stockpiles, growing and allowed areas | sheet 08 (salvage gear), med | M1 |
| **Power** | `ui.overlay.power` | Nets, and which of them are short | sheet 08 (salvage gear), high | M1 |
| **Salvage density** | `ui.overlay.salvage` | Where the worthwhile scrap is | sheet 05 (tools and weapons), med | M1 |
| **Structural support** | `ui.overlay.support` | What is holding this layer up | sheet 04 (manufactured), med | M1 |
| **Traffic** | `ui.overlay.traffic` | Where colonists actually walk <br>**Needs:** footfall. An abstract with no obvious source | no art | M1 |

## Layer controls

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Layer up** | `ui.layer.up` | Slice one layer higher | sheet 06 (action tiles), med | M1 |
| **Layer down** | `ui.layer.down` | Slice one layer lower <br>**Needs:** a downward arrow to match the up arrow | no art | M1 |
| **Ground level** | `ui.layer.ground` | Jump the slice to the street <br>**Needs:** a ground or street level mark | no art | M1 |
| **Hide above** | `ui.layer.policy.hide` | Draw nothing above the slice <br>**Needs:** visibility mode: hide above | no art | M1 |
| **Ghost above** | `ui.layer.policy.ghost` | Structural outlines only <br>**Needs:** visibility mode: ghost outlines | no art | M1 |
| **X-ray, one layer** | `ui.layer.policy.xraymin` | Just the storey overhead <br>**Needs:** visibility mode: x-ray, one layer | no art | M1 |
| **X-ray, all layers** | `ui.layer.policy.xray` | Everything above, fading with distance <br>**Needs:** visibility mode: x-ray, all layers | no art | M1 |
| **Roofs off** | `ui.layer.policy.roofsoff` | Solid above, but roofs and floors dropped <br>**Needs:** visibility mode: roofs off | no art | M1 |
| **No cut-away** | `ui.layer.policy.full` | Draw everything solid <br>**Needs:** visibility mode: no cut-away | no art | M1 |

## Tabs

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Architect** | `ui.tab.architect` | Build, dig and zone — the palette of placement tools | no art | M1 |
| **Work** | `ui.tab.work` | The priority grid | sheet 06 (action tiles), med | M1 |
| **Schedule** | `ui.tab.schedule` | Who does what, and when | sheet 06 (action tiles), med | M1 |
| **Research** | `ui.tab.research` | The tree, and what is being worked on | sheet 06 (action tiles), high | M1 |
| **Colonists** | `ui.tab.colonists` | Everyone, at a glance <br>**Needs:** a group of people. Blocked on the missing human figure | no art | M1 |
| **Animals** | `ui.tab.animals` | Tame beasts and their training | sheet 06 (action tiles), high | M1 |
| **Wildlife** | `ui.tab.wildlife` | What is out there | sheet 06 (action tiles), med | M1 |
| **Bills** | `ui.tab.bills` | Standing production orders | sheet 04 (manufactured), high | M1 |
| **Trade** | `ui.tab.trade` | Caravans and traders | sheet 08 (salvage gear), high | M1 |
| **Factions** | `ui.tab.factions` | Who likes us, and how much | sheet 08 (salvage gear), high | M1 |
| **History** | `ui.tab.archive` | Everything that has happened | sheet 08 (salvage gear), high | M1 |
| **Menu** | `ui.tab.menu` | Save, load, settings, quit <br>**Needs:** a settings or menu mark | no art | M1 |

## Game speed

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Pause** | `ui.speed.pause` | Stop time <br>**Needs:** pause. Universal symbol, trivial to draw, not in the sheets | no art | M1 |
| **Normal speed** | `ui.speed.play` | One tick per tick <br>**Needs:** normal speed | no art | M1 |
| **Fast** | `ui.speed.fast` | Three times speed <br>**Needs:** fast forward | no art | M1 |
| **Very fast** | `ui.speed.ultra` | As fast as the simulation will go <br>**Needs:** very fast | no art | M1 |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
