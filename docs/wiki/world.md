# World and interface

Weather, the data overlays, the layer controls and the rest of the interface furniture. The six layer visibility modes are decided: see ADR 0006.

135 entries, 109 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Terrain

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Grass** | `ui.terrain.grass` | The living surface. Trees grow here and nothing else <br>**Needs:** a tuft of grass on a ground tile | no art | M1 |
| **Bare Earth** | `ui.terrain.bareearth` | Soil with the grass worn off it <br>**Needs:** a bare soil tile | no art | M1 |
| **Gravel** | `ui.terrain.gravel` | Stony ground. Poor soil, firm footing <br>**Needs:** a tile of loose stones | no art | M1 |
| **Sand** | `ui.terrain.sand` | Loose and barren. What a riverbed is made of <br>**Needs:** a plain sand tile | no art | M1 |
| **Marsh** | `ui.terrain.marsh` | Wet ground fringing water. Crossed at three quarters pace <br>**Needs:** a reed or two on wet ground | no art | M1 |
| **Shallow Water** | `ui.terrain.water.shallow` | Wadeable, at a third of walking pace. Nothing can be built on it without a bridge <br>**Needs:** ripples with the bed showing through | no art | M1 |
| **Deep Water** | `ui.terrain.water.deep` | Out of your depth. Colonists will not enter it and paths go round <br>**Needs:** flat dark water, no bed visible | no art | M1 |
| **Rock** | `ui.terrain.rock` | Natural stone. Mined, not cleared <br>**Needs:** a face of natural stone | no art | M1 |
| **Subsoil** | `ui.terrain.subsoil` | The band between soil and rock. Cheap to dig <br>**Needs:** a tile of dug earth | no art | M1 |
| **Bedrock** | `ui.terrain.bedrock` | The floor of the world. Deliberately punitive to mine <br>**Needs:** a tile of dark, banded stone | no art | M1 |
| **Packed Gravel** | `ui.terrain.packedgravel` | Stony ground, packed hard. Firm footing, poor soil | no art | M3 |
| **Birch** | `ui.terrain.tree.birch` | A slim white-barked tree. Quick to chop, not much wood in it <br>**Needs:** a slim pale-trunked tree | no art | M3 |
| **Meadow tree** | `ui.terrain.tree.meadow` | A broad round-crowned tree of the open meadow. Chopped for wood <br>**Needs:** a round-crowned tree | no art | M3 |
| **Fruit tree** | `ui.terrain.tree.fruit` | A low spreading tree. It will bear fruit one day; for now it is wood <br>**Needs:** a low tree with a spreading crown | no art | M3 |
| **Giant tree** | `ui.terrain.tree.giant` | A rare old giant. Slow to bring down, and a great deal of wood <br>**Needs:** a towering tree dwarfing a figure | no art | M3 |
| **Bush** | `ui.terrain.bush` | Dense undergrowth. Slow to push through; cleared before anything is built here <br>**Needs:** a round green bush | no art | M3 |
| **Berry bush** | `ui.terrain.bush.berry` | A bush that bears berries <br>**Needs:** a bush dotted with red berries | no art | M3 |
| **Picked berry bush** | `ui.terrain.bush.picked` | A berry bush picked bare, growing its berries back <br>**Needs:** a bare bush with no berries | no art | M3 |
| **Carrot** | `ui.terrain.carrot` | A root vegetable of the meadow. Grown in zones, cut at full growth | no art | M3 |

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
| **Home** | `ui.overlay.home` | The colony's home: the base joined to the hearth, and five cells round it <br>**Needs:** a house, drawn as a path (docs/reference/mockups/home-glyph.svg) | no art | M3 |
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
| **Build** | `ui.tab.build` | Build, dig and zone — the palette of placement tools | sheet 06 (action tiles), high | M1 |
| **Work** | `ui.tab.work` | The priority grid | sheet 06 (action tiles), high | M1 |
| **Schedule** | `ui.tab.schedule` | Folded into Work: one table says who does what, and when | sheet 06 (action tiles), high | M1 |
| **Research** | `ui.tab.research` | The tree, and what is being worked on | sheet 06 (action tiles), high | M1 |
| **Colonists** | `ui.tab.colonists` | Everyone, at a glance | sheet 06 (action tiles), high | M1 |
| **Animals** | `ui.tab.animals` | Tame beasts and their training | sheet 06 (action tiles), high | M1 |
| **Wildlife** | `ui.tab.wildlife` | What is out there. Listed under Animals for now; not on the bar | sheet 06 (action tiles), med | M1 |
| **Bills** | `ui.tab.bills` | Standing production orders | sheet 04 (manufactured), high | M1 |
| **Trade** | `ui.tab.trade` | Caravans and traders | sheet 08 (salvage gear), high | M1 |
| **Factions** | `ui.tab.factions` | Who likes us, and how much | sheet 08 (salvage gear), high | M1 |
| **History** | `ui.tab.archive` | Everything that has happened | sheet 08 (salvage gear), high | M1 |
| **Almanac** | `ui.tab.almanac` | The colony reference: every terrain, material, structure, item and craft | sheet 08 (salvage gear), high | M1 |
| **Menu** | `ui.tab.menu` | Save, load, settings, quit <br>**Needs:** a settings or menu mark | no art | M1 |
| **Storage** | `ui.tab.storage` | What a store takes, and how much it matters <br>**Needs:** an open crate seen from above | no art | M3 |
| **Tile** | `ui.tab.tile` | The ground itself, under whatever is standing on it <br>**Needs:** a single square of ground, in plan | no art | M3 |
| **Inventory** | `ui.tab.inventory` | Everything in the colony's stores, and which store holds it | no art | INV |
| **Assign** | `ui.tab.assign` | Where each colonist may work, and what she does about danger <br>**Needs:** drawn with ui.tab.colonists' art (HudCommands.IconOf) | no art | HA |

## Game speed

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Pause** | `ui.speed.pause` | Stop time <br>**Needs:** pause. Universal symbol, trivial to draw, not in the sheets | no art | M1 |
| **Normal speed** | `ui.speed.play` | One tick per tick <br>**Needs:** normal speed | no art | M1 |
| **Fast** | `ui.speed.fast` | Three times speed <br>**Needs:** fast forward | no art | M1 |
| **Very fast** | `ui.speed.ultra` | As fast as the simulation will go <br>**Needs:** very fast | no art | M1 |

## Settings

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Settings** | `ui.settings.panel` | What the game does, not what the colony does | no art | M1 |
| **Graphics** | `ui.settings.graphics` | How the world is drawn. None of it reaches the simulation | no art | M1 |
| **Interface** | `ui.settings.interface` | How the interface itself is drawn and how large it is | no art | M1 |
| **Interface scale** | `ui.settings.uiscale` | How large the HUD is drawn. Larger type covers more of the board | no art | M1 |
| **Shadows** | `ui.settings.shadows` | Whether people and buildings cast shadows on the ground | no art | M1 |
| **Surrounding land** | `ui.settings.surround` | The land carried past the rim so the board does not end in mid-air | no art | M1 |
| **Ground relief** | `ui.settings.relief` | The roll of the drawn ground. The cells underneath stay flat | no art | M1 |
| **See through to selection** | `ui.settings.seethrough` | Fade whatever stands between the camera and a selected colonist | no art | M1 |
| **Trees fade for every colonist** | `ui.settings.seethroughall` | Fade the trees in front of every colonist on screen, not only the selected ones | no art | M3 |
| **Cut away the ceiling** | `ui.settings.cutaway` | See into rooms on this layer. Off shows the floor above you | no art | M3 |
| **Walls down** | `ui.settings.wallsdown` | Lower walls to a stump and hide the storeys above, so you can see inside. Building shows them in full | no art | M3 |
| **Keys** | `ui.settings.keys` | Every key the game reads, and what each one may be changed to | no art | M3 |
| **Reset keys to defaults** | `ui.settings.resetkeys` | Put every action back on the key it shipped with | no art | M3 |
| **Audio** | `ui.settings.audio` | How loud each part of the game is. Stored on the machine, in decibels | no art | M3 |
| **Camera speed** | `ui.settings.camspeed` | How fast the camera pans and zooms, as a share of its tuned speed | no art | M3 |
| **Build palette layout** | `ui.settings.buildlayout` | Which of the three shapes the Build palette takes: rows, rail or bar | no art | M3 |
| **Selection style** | `ui.settings.selectionstyle` | How the selected thing is marked: a line round the thing itself, or corner brackets | no art | M3 |
| **Developer overlay** | `ui.settings.developer` | The frame-time and draw-call readout, kept on the machine between sessions | no art | M3 |
| **Display** | `ui.settings.display` | How the frame is paced and how large it is drawn | no art | M3 |
| **Detail** | `ui.settings.detail` | What the board is drawn with. None of it reaches the simulation | no art | M3 |
| **VSync** | `ui.settings.vsync` | Match the screen's refresh rate. Stops tearing, and paces the frame | no art | M3 |
| **Frame rate cap** | `ui.settings.framecap` | The ceiling on frames a second. A rate the machine can hold beats a higher one that swings | no art | M3 |
| **Render scale** | `ui.settings.renderscale` | How large the world is drawn before it is scaled to the window. The interface stays sharp | no art | M3 |
| **Anti-aliasing** | `ui.settings.antialias` | Smooth the stepped edges of the board. The most expensive thing on this page | no art | M3 |
| **Shadow distance** | `ui.settings.shadowdist` | How far from the camera shadows are still drawn | no art | M3 |
| **Display mode** | `ui.settings.displaymode` | Fullscreen, borderless or a window | no art | M3 |
| **Quality** | `ui.settings.quality` | Set every lever on this page at once, from Low to Ultra. Custom once any is moved by hand | no art | MF |
| **Grass** | `ui.settings.vegetation` | How thick the grass is strewn, from bare ground to every cell. Decoration, in no cell and no save | no art | MF |
| **Grass distance** | `ui.settings.grassdist` | How far from the camera grass is still drawn. Past it the ground carries the field | no art | MF |
| **Grass shadows** | `ui.settings.foliageshadows` | Whether grass casts shadows. Off, as it has always shipped: a shadow centimetres long on grass the same colour | no art | MF |
| **Resolution** | `ui.settings.resolution` | How many pixels the game is drawn at. Only a built game can change it | no art | M3 |
| **Exit game** | `ui.settings.exit` | Leave the game. The row asks twice, because leaving is not undoable | no art | M3 |
| **Gameplay** | `ui.settings.gameplay` | What the game does for you while you play | no art | MS |
| **Autosave** | `ui.settings.autosave` | How often the colony is written over its own save. Off writes nothing unless you ask | no art | MS |
| **Master volume** | `ui.settings.volume.master` | Everything at once | no art | M3 |
| **Music volume** | `ui.settings.volume.music` | What plays under the game | no art | M3 |
| **Ambience volume** | `ui.settings.volume.ambience` | The sound of the place itself | no art | M3 |
| **Effects volume** | `ui.settings.volume.effects` | Axes, picks and the noises of work | no art | M3 |
| **Alerts volume** | `ui.settings.volume.alerts` | What asks for attention | no art | M3 |
| **Scale** | `ui.settings.group.scale` | How large the interface is drawn | no art | M3 |
| **Camera** | `ui.settings.group.camera` | How the camera moves, and the keys that move it | no art | M3 |
| **Build palette** | `ui.settings.group.palette` | The shape of the Build palette | no art | M3 |
| **Selection** | `ui.settings.group.selection` | How a selected colonist, item, building or tile is marked in the world | no art | M3 |
| **Performance** | `ui.settings.group.performance` | What the frame costs to draw | no art | M3 |
| **Volume** | `ui.settings.group.volume` | The loudness of the game and its music and ambience | no art | M3 |
| **Cues** | `ui.settings.group.cues` | The loudness of work and of what asks for attention | no art | M3 |
| **Saving** | `ui.settings.group.saving` | When the colony is written to disk without being asked | no art | M3 |
| **Game** | `ui.settings.group.game` | Saving, loading and leaving, pinned under the tabs | no art | M3 |
| **View** | `ui.settings.group.view` | The keys that move the slice and frame the map | no art | M3 |
| **Tools** | `ui.settings.group.tools` | The keys that arm an order | no art | M3 |
| **Time** | `ui.settings.group.time` | The keys that pause the game and set its speed | no art | M3 |

## The Inventory tab's words

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Find an item** | `ui.inventory.hud.search` | The search field's placeholder | no art | INV |
| **Item** | `ui.inventory.hud.item` | Column heading | no art | INV |
| **Places** | `ui.inventory.hud.places` | Column heading: how many stores hold the item | no art | INV |
| **Total** | `ui.inventory.hud.total` | Column heading: how many the colony's stores hold | no art | INV |
| **Where** | `ui.inventory.hud.where` | Column heading | no art | INV |
| **Count** | `ui.inventory.hud.qty` | Column heading: how many of the item the store holds. Not Qty, which the HUD reads as a placeholder | no art | INV |
| **Go** | `ui.inventory.hud.go` | Move the camera to this store and select it | no art | INV |
| **Go to {place}** | `ui.inventory.hud.goto` | The primary button: the selected store | no art | INV |
| **{category}, in {count} places** | `ui.inventory.hud.inplaces` | The line under a selected item's name | no art | INV |
| **{category}, in 1 place** | `ui.inventory.hud.inplace` | The line under a selected item's name when one store holds it | no art | INV |
| **Go moves the camera to that place and selects it. Clicking the item row itself goes to the place holding the most.** | `ui.inventory.hud.hint` | The hint under the list of places | no art | INV |
| **Nothing is in a store yet.** | `ui.inventory.hud.empty` | The table when the colony's stores are empty | no art | INV |
| **No item matches.** | `ui.inventory.hud.nomatch` | The table when a search finds nothing | no art | INV |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
