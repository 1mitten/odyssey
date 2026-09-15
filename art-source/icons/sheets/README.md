# The eight icon sheets go here

They are not in the repository yet. They were supplied as images pasted into a chat turn, which
reach the assistant as pixels rather than as files, so they could not be committed from the remote
session. The same is true of the two concept renders that `docs/reference/screenshots/concept/`
still asks for.

Copy them in under exactly these names, because `sheets.csv` and `docs/design/icon-map.csv` refer to
them by sheet id:

| File | Sheet id | Contents |
|---|---|---|
| `01-raw-materials.png` | 01 | stone, logs, ore, hides, plants, fungus, bone, hand tools |
| `02-food.png` | 02 | tins, soup, bread, cheese, fish, produce, milk, coffee, wine |
| `03-camp-crafting.png` | 03 | campfire, anvil, cauldron, workbench, spinning wheel, tent, well |
| `04-manufactured.png` | 04 | ingots, wire, circuit boards, microchips, glass, gears, pipe, fabric |
| `05-tools-weapons.png` | 05 | drill, knives, axes, pickaxe, shovel, sickle, revolver, crossbow, shield |
| `06-action-tiles.png` | 06 | framed action tiles: cooking, farming, medical, crosshair, flame, paw, skull, book |
| `07-anatomy.png` | 07 | organs, bone, hide, meat, skull, brain, eye |
| `08-salvage-gear.png` | 08 | jerrycan, battery, solar panel, generator, med kit, radiation, sandbag |

Then, from the repository root:

```
python3 tools/icons/icons.py detect     # confirms each grid; prints the sheets.csv line to paste
python3 tools/icons/icons.py contact    # labelled contact sheets into ../contact/
python3 tools/icons/icons.py validate
python3 tools/icons/icons.py export     # writes Assets/Art/Ui/icons/<key>.png
```

PNG only, 8-bit, not interlaced. The decoder is ours and refuses anything else by name.
