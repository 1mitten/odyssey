# Commodities

What the colony stockpiles, hauls, cooks and trades. Salvage tiers replace the ore classes a wilderness colony sim would have, because the setting is a dead city and the ground is already full of manufactured things. These names appear in the resource ledger, in every bill and in every trade, so they are the names worth arguing about first.

54 entries, 9 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Scrap metal** | `ui.res.scrap` | Salvaged metal. Power lines and machines are built from it | sheet 04 (manufactured), med | M1 |
| **Rubble** | `ui.res.rubble` | Broken masonry where a floor fell in. Cleared with the Mine order before anything is built | sheet 01 (raw materials), high | M1 |
| **Girder** | `ui.res.girder` | Structural steel cut from a shell. Heavy, valuable | sheet 04 (manufactured), med | M1 |
| **Hull panel** | `ui.res.panel` | Flat salvaged plating. The cheapest wall material | sheet 04 (manufactured), high | M1 |
| **Composite** | `ui.res.composite` | Light high-strength laminate. Scarce, from deep salvage | sheet 04 (manufactured), med | M1 |
| **Glass** | `ui.res.glass` | Windows, screens and hydroponic covers | sheet 04 (manufactured), high | M1 |
| **Polymer** | `ui.res.polymer` | Moulded plastics. Light, flammable | sheet 04 (manufactured), med | M1 |
| **Ceramic** | `ui.res.ceramic` | Heat-resistant tile and insulator stock | sheet 01 (raw materials), low | M1 |
| **Rebar** | `ui.res.rebar` | Reinforcing rod. Doubles a span's support | sheet 04 (manufactured), med | M1 |
| **Wire** | `ui.res.wire` | Conduit and machine winding stock | sheet 04 (manufactured), high | M1 |
| **Circuitry** | `ui.res.circuitry` | Salvaged boards. Every powered thing needs some | sheet 04 (manufactured), high | M1 |
| **Processor** | `ui.res.chip` | Intact pre-collapse logic. Rarely recoverable | sheet 04 (manufactured), high | M1 |
| **Mechanism** | `ui.res.mechanism` | Gears and actuators. Doors, lifts, turrets | sheet 04 (manufactured), high | M1 |
| **Pipe** | `ui.res.pipe` | Fluid and gas runs, and cheap conduit | sheet 04 (manufactured), high | M1 |
| **Power cell** | `ui.res.cell` | Portable charge. Batteries and tools | sheet 08 (salvage gear), high | M1 |
| **Fuel** | `ui.res.fuel` | Burns in generators. Salvaged, not refined, at first | sheet 08 (salvage gear), high | M1 |
| **Coolant** | `ui.res.coolant` | Keeps fabricators and reactors inside tolerance | sheet 08 (salvage gear), med | M1 |
| **Optic** | `ui.res.optic` | Lenses and sensors. Scanners and scopes | sheet 01 (raw materials), low | M1 |
| **Fabric** | `ui.res.fabric` | Bedding, apparel and filters | sheet 04 (manufactured), high | M1 |
| **Leather** | `ui.res.leather` | Tanned hide. Tougher apparel than fabric | sheet 07 (anatomy), high | M1 |
| **Cord** | `ui.res.cord` | Rope and cable. Ladders, lifts, bindings | sheet 04 (manufactured), high | M1 |
| **Insulation** | `ui.res.insulation` | Foam batting. Slows heat through a wall | sheet 04 (manufactured), med | M1 |
| **Paper** | `ui.res.paper` | Records and packaging. Research feedstock | sheet 04 (manufactured), high | M1 |
| **Rations** | `ui.res.rations` | Sealed pre-collapse food. It keeps, it fills, and it is a little better than nothing | sheet 02 (food), high | M1 |
| **Meal** | `ui.res.meal` | A cooked meal with meat in it, the best food there is. There is no meat yet, so cooks make vegetable meals | sheet 02 (food), high | M3 |
| **Vegetable meal** | `ui.res.meal.veg` | A meal cooked without meat. As filling and as welcome as one with <br>**Needs:** a bowl of cooked vegetables. The food sheet's bowl may serve once the art is judged | no art | M3 |
| **Burnt meal** | `ui.res.meal.burnt` | A meal the cook let catch. Edible, less filling, and nobody enjoys it <br>**Needs:** a blackened bowl of food. Our own concept | no art | M3 |
| **Protein paste** | `ui.res.protein` | Reclaimed nutrition. Edible; that is all | sheet 02 (food), med | M1 |
| **Raw meat** | `ui.res.meat` | Butchered. Spoils without cold | sheet 07 (anatomy), high | M1 |
| **Produce** | `ui.res.produce` | Grown food, uncooked | sheet 02 (food), high | M1 |
| **Wood** | `ui.res.wood` | Felled timber. The first thing the colony builds with <br>**Needs:** a short stack of logs, cut ends showing | no art | M3 |
| **Stone** | `ui.res.stone` | Broken rock from a mined face, or loose on the ground. Slower to build with, and it lasts <br>**Needs:** a few angular grey rocks with fresh broken faces | no art | M3 |
| **Iron ore** | `ui.res.ironore` | Raw ore. Worthless until something smelts it <br>**Needs:** dark rock flecked with rust-orange | no art | M3 |
| **Coal** | `ui.res.coal` | Found deeper than iron. Nothing burns it yet <br>**Needs:** glossy black lumps | no art | M3 |
| **Carrots** | `ui.res.carrots` | The first field crop. Eaten raw in a pinch; three make a meal | no art | M3 |
| **Berries** | `ui.res.berries` | Picked from a wild berry bush. Eaten straight; the bush grows more <br>**Needs:** a handful of red berries | no art | M3 |
| **Mushrooms** | `ui.res.mushrooms` | Found under the trees. Eaten straight; more come up somewhere else <br>**Needs:** two capped mushrooms | no art | M3 |
| **Grain** | `ui.res.grain` | Bulk staple. Stores well, needs cooking | sheet 01 (raw materials), med | M1 |
| **Fungus** | `ui.res.fungus` | Grows without light. The underground staple | sheet 01 (raw materials), high | M1 |
| **Stimulant** | `ui.res.stimulant` | Buys an hour of wakefulness at a cost | sheet 02 (food), med | M1 |
| **Alcohol** | `ui.res.alcohol` | Recreation, and a problem for some | sheet 02 (food), high | M1 |
| **Medical supplies** | `ui.res.medkit` | Dressings and drugs. One is used up per treatment | sheet 08 (salvage gear), high | M1 |
| **Medicine** | `ui.res.medicine` | Compounded drugs. Better outcomes than a medkit | sheet 02 (food), low | M1 |
| **Organ** | `ui.res.organ` | Harvested and kept cold. Surgery, or trade | sheet 07 (anatomy), high | M1 |
| **Prosthetic** | `ui.res.prosthetic` | Replaces a lost part. Quality varies wildly | sheet 05 (tools and weapons), low | M1 |
| **Bone** | `ui.res.bone` | Butchery by-product. Tool and craft stock | sheet 07 (anatomy), high | M1 |
| **Tallow** | `ui.res.tallow` | Rendered fat. Candles, soap, fuel | sheet 07 (anatomy), med | M1 |
| **Plant matter** | `ui.res.plantmatter` | Cuttings and chaff. Compost and feedstock | sheet 01 (raw materials), high | M1 |
| **Seed** | `ui.res.seed` | Plants a growing zone. Some strains are irreplaceable | sheet 01 (raw materials), med | M1 |
| **Ammunition** | `ui.res.ammo` | Feeds firearms and turrets | sheet 08 (salvage gear), high | M1 |
| **Gold** | `ui.res.gold` | The currency every trader takes. Stored and hauled like any other goods | sheet 08 (salvage gear), high | M1 |
| **Relic** | `ui.res.relic` | Pre-collapse curio. Trade value and a mood lift | sheet 08 (salvage gear), med | M1 |
| **Smokes** | `ui.res.smokes` | Cheap comfort with a long bill | sheet 08 (salvage gear), med | M1 |
| **Curio** | `ui.res.curio` | Sentimental object. Worth more to a colonist than a trader | sheet 08 (salvage gear), high | M1 |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
