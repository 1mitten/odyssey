# Buildings and orders

Everything on the architect menu: what can be built, and the one-off orders that can be given to things that already exist. The vertical connectors matter more here than in a flat colony sim, because a stair occupies two cells and a ladder one.

83 entries, 24 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Architect categories

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Structure** | `ui.arch.category.structure` | Walls, doors, floors and vertical connectors | sheet 06 (action tiles), med | M3 |
| **Zones** | `ui.arch.category.zones` | Stockpiles, growing zones and allowed areas | sheet 08 (salvage gear), med | M3 |
| **Orders** | `ui.arch.category.orders` | One-off designations on existing things | sheet 06 (action tiles), med | M3 |
| **Power** | `ui.arch.category.power` | Generation, storage and conduit | sheet 08 (salvage gear), high | M3 |
| **Production** | `ui.arch.category.production` | Benches and machines that make things | sheet 06 (action tiles), med | M3 |
| **Furniture** | `ui.arch.category.furniture` | Beds, seating, storage and lighting | sheet 03 (camp and crafting), high | M3 |
| **Security** | `ui.arch.category.security` | Turrets, traps and hard cover | sheet 05 (tools and weapons), high | M3 |
| **Salvage** | `ui.arch.category.salvage` | Tools for taking a ruin apart profitably | sheet 05 (tools and weapons), med | M3 |
| **Floors** | `ui.arch.category.floors` | Surfaces, and the roof of the layer below | sheet 04 (manufactured), high | M3 |
| **Recreation** | `ui.arch.category.recreation` | Things colonists enjoy <br>**Needs:** a recreation category mark. Blocked on the same missing art as ui.need.joy | no art | M3 |

## Architect tools

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Wall** | `ui.arch.tool.wall` | Blocks movement, sight and heat | sheet 03 (camp and crafting), med | M3 |
| **Door** | `ui.arch.tool.door` | Passable, and holds a room's air | sheet 06 (action tiles), med | M3 |
| **Stair** | `ui.arch.tool.stair` | Two cells. The fast way between layers, and the one a load can be carried up <br>**Needs:** a staircase. M1 needs it and no sheet has one | no art | M3 |
| **Ladder** | `ui.arch.tool.ladder` | One cell. Slow, cheap, no hauling <br>**Needs:** a ladder. M1 needs it and no sheet has one | no art | M3 |
| **Support pillar** | `ui.arch.tool.pillar` | Extends how far a roof can span | sheet 04 (manufactured), low | M3 |
| **Slab** | `ui.arch.tool.roof` | An upper floor. Goes on a wall or bridges out from one <br>**Needs:** a roof or ceiling panel seen from below. Central to the layer model | no art | M3 |
| **Reclaim shell** | `ui.arch.tool.reclaim` | Adopt existing ruined structure as ours <br>**Needs:** adopting existing ruined structure. Our own invention, nothing to borrow | no art | M3 |
| **Window** | `ui.arch.tool.window` | Light through a wall, less insulation | sheet 04 (manufactured), high | M3 |
| **Hatch** | `ui.arch.tool.hatch` | A door in a floor <br>**Needs:** a hatch in a floor | no art | M3 |
| **Airlock** | `ui.arch.tool.airlock` | Two doors and a seal. Holds atmosphere <br>**Needs:** a sealed double door | no art | M3 |
| **Stockpile** | `ui.arch.tool.stockpile` | Where hauled things go | sheet 08 (salvage gear), high | M3 |
| **Growing zone** | `ui.arch.tool.growzone` | Soil or hydroponics planted with one crop | sheet 06 (action tiles), high | M3 |
| **Allowed area** | `ui.arch.tool.allowed` | Where colonists may go | sheet 08 (salvage gear), med | M3 |
| **Dumping zone** | `ui.arch.tool.dumping` | Where rubbish and rubble go | sheet 08 (salvage gear), low | M3 |
| **Animal area** | `ui.arch.tool.kennel` | Where tame animals may roam | sheet 06 (action tiles), high | M3 |
| **Mine** | `ui.arch.tool.mine` | Dig out a cell | sheet 05 (tools and weapons), high | M3 |
| **Deconstruct** | `ui.arch.tool.deconstruct` | Take our own building apart for materials | sheet 01 (raw materials), med | M3 |
| **Salvage** | `ui.arch.tool.salvage` | Strip a ruin. Slower than deconstruct, better yield | sheet 05 (tools and weapons), med | M3 |
| **Haul urgently** | `ui.arch.tool.haulurgent` | Jump this to the top of the haul list | sheet 08 (salvage gear), low | M3 |
| **Cancel** | `ui.arch.tool.cancel` | Remove designations and orders <br>**Needs:** a cancel mark. Trivial to draw, and in the HUD constantly | no art | M3 |
| **Forbid** | `ui.arch.tool.forbid` | Hands off this | sheet 08 (salvage gear), high | M3 |
| **Chop and clear** | `ui.arch.tool.fell` | Cut a tree down for wood, or clear a bush out of the way <br>**Needs:** an axe against a trunk | no art | M3 |
| **Allow** | `ui.arch.tool.allowitem` | Hands back on | sheet 08 (salvage gear), high | M3 |
| **Clear rubble** | `ui.arch.tool.clearrubble` | Remove loose debris from a cell | sheet 05 (tools and weapons), high | M3 |
| **Extinguish** | `ui.arch.tool.extinguish` | Put this fire out now | sheet 05 (tools and weapons), low | M3 |
| **Harvest** | `ui.arch.tool.harvest` | Pick the berries from a ripe berry bush. They grow back in three days | sheet 05 (tools and weapons), high | M3 |
| **Conduit** | `ui.arch.tool.conduit` | Carries power. Connects vertically | sheet 04 (manufactured), high | M3 |
| **Remove conduit** | `ui.arch.tool.unwire` | Takes a conduit up, and nothing else in the cell | sheet 04 (manufactured), med | M3 |
| **Battery** | `ui.arch.tool.battery` | Stores charge against the night | sheet 08 (salvage gear), high | M3 |
| **Generator** | `ui.arch.tool.generator` | Burns fuel for power | sheet 08 (salvage gear), high | M3 |
| **Solar array** | `ui.arch.tool.solar` | Free power, and useless in the dark | sheet 08 (salvage gear), high | M3 |
| **Wind turbine** | `ui.arch.tool.wind` | Needs open sky and clear space <br>**Needs:** a wind turbine | no art | M3 |
| **Geothermal tap** | `ui.arch.tool.geothermal` | Deep, expensive, constant <br>**Needs:** a geothermal or steam tap | no art | M3 |
| **Power switch** | `ui.arch.tool.switch` | Cuts a net in two <br>**Needs:** a power switch or breaker | no art | M3 |
| **Reactor** | `ui.arch.tool.reactor` | Late, enormous, dangerous | sheet 08 (salvage gear), med | M3 |
| **Fabricator** | `ui.arch.tool.fabricator` | Makes refined materials and components | sheet 06 (action tiles), med | M3 |
| **Electric Cooker** | `ui.arch.tool.galley` | Cooks meals from bills. Needs power | sheet 03 (camp and crafting), high | M3 |
| **Reclaimer** | `ui.arch.tool.reclaimer` | Sorts scrap into usable material <br>**Needs:** a machine that sorts scrap. Our own invention | no art | M3 |
| **Hydroponics basin** | `ui.arch.tool.hydroponics` | Grows without soil, needs power <br>**Needs:** a hydroponic basin. Central to food in a ruined city | no art | M3 |
| **Crafting bench** | `ui.arch.tool.bench` | General making | sheet 03 (camp and crafting), high | M3 |
| **Tailoring bench** | `ui.arch.tool.tailorbench` | Apparel from fabric and leather | sheet 03 (camp and crafting), low | M3 |
| **Forge** | `ui.arch.tool.smithy` | Works metal by hand | sheet 03 (camp and crafting), high | M3 |
| **Butcher table** | `ui.arch.tool.butcher` | Carcasses into meat and leather | sheet 05 (tools and weapons), high | M3 |
| **Research bench** | `ui.arch.tool.researchbench` | Where research happens | sheet 06 (action tiles), high | M3 |
| **Comms console** | `ui.arch.tool.comms` | Talk to factions and traders | sheet 08 (salvage gear), high | M3 |
| **Bunk** | `ui.arch.tool.bunk` | Cheap sleeping. Poor comfort | sheet 03 (camp and crafting), high | M3 |
| **Bed** | `ui.arch.tool.bed` | Better rest, better mood | sheet 08 (salvage gear), high | M3 |
| **Campfire** | `ui.arch.tool.campfire` | A fire: warmth you can build | sheet 03 (camp and crafting), high | M4 |
| **Table** | `ui.arch.tool.table` | Eating at one beats eating on the floor | sheet 03 (camp and crafting), high | M3 |
| **Chair** | `ui.arch.tool.chair` | Comfort while working or eating | sheet 03 (camp and crafting), med | M3 |
| **Lamp** | `ui.arch.tool.lamp` | Light. Costs power | sheet 08 (salvage gear), high | M3 |
| **Shelf** | `ui.arch.tool.shelf` | Storage that keeps things off the floor | sheet 08 (salvage gear), low | M3 |
| **Locker** | `ui.arch.tool.locker` | Personal storage | sheet 08 (salvage gear), med | M3 |
| **Brazier** | `ui.arch.tool.brazier` | Heat and light, no power, some risk | no art | M3 |
| **Cooler** | `ui.arch.tool.cooler` | Moves heat out of a room <br>**Needs:** a cooling unit | no art | M3 |
| **Heater** | `ui.arch.tool.heater` | Moves heat into a room <br>**Needs:** a heating unit, distinct from the brazier | no art | M3 |
| **Vent** | `ui.arch.tool.vent` | Lets two rooms share air | sheet 04 (manufactured), med | M3 |
| **Turret** | `ui.arch.tool.turret` | Shoots hostiles. Needs power and ammunition <br>**Needs:** an automated gun. M6 needs it | no art | M3 |
| **Trap** | `ui.arch.tool.trap` | One-shot, cheap, forgettable by your own colonists | sheet 03 (camp and crafting), high | M3 |
| **Barricade** | `ui.arch.tool.barricade` | Cover without blocking sight | sheet 08 (salvage gear), high | M3 |
| **Blast door** | `ui.arch.tool.blastdoor` | Slow, strong, holds a breach <br>**Needs:** a heavy blast door, distinct from a normal door | no art | M3 |
| **Sandbags** | `ui.arch.tool.sandbag` | Cheap, quick low cover. Climbed over, never stood on | sheet 08 (salvage gear), high | M3 |
| **Searchlight** | `ui.arch.tool.searchlight` | Light where you need to shoot | sheet 08 (salvage gear), med | M3 |
| **Floor** | `ui.arch.tool.deckplate` | Laid on ground you already walk on | sheet 04 (manufactured), high | M3 |
| **Grating** | `ui.arch.tool.grating` | See and fall through. Light passes | sheet 04 (manufactured), med | M3 |
| **Poured concrete** | `ui.arch.tool.concretefloor` | Slow, cheap, permanent | sheet 08 (salvage gear), med | M3 |
| **Tile** | `ui.arch.tool.tile` | Clean and pretty. Hospitals and kitchens <br>**Needs:** a finished floor tile | no art | M3 |
| **Remove floor** | `ui.arch.tool.removefloor` | Back to bare substrate <br>**Needs:** removing a floor. Pairs with the missing roof icon | no art | M3 |
| **Bench** | `ui.arch.tool.recbench` | Somewhere to sit and talk | sheet 03 (camp and crafting), med | M3 |
| **Games table** | `ui.arch.tool.gamestable` | Two colonists, one distraction <br>**Needs:** a games table. Same gap as recreation throughout | no art | M3 |
| **Viewscreen** | `ui.arch.tool.viewscreen` | Recreation for a room at once <br>**Needs:** a screen or monitor | no art | M3 |
| **Planter** | `ui.arch.tool.planter` | Decorative growth indoors | sheet 03 (camp and crafting), low | M3 |
| **Sculpture** | `ui.arch.tool.sculpture` | Beauty, made by hand <br>**Needs:** a sculpture or art object | no art | M3 |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
