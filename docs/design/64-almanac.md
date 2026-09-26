# 64 — The Almanac, true to the game

Owner, 2026-09-26: *"ensure the almanac in game is completely up-to-date with everything in game.
The information is correct and links are good. How possible would be to create icons for
everything where there isn't — we can take pictures or reuse something? What are the gaps?"*

## 1. What was wrong

The Almanac (F9, `AlmanacCatalogue`, `HudShell.Almanac`) shipped on 2026-09-20 with a mock-up's
text, and only its Fauna had been corrected since (2026-09-23). Measured against the Defs and the
simulation by five fact-gathering passes, one per domain, each fact cited to a file and line:

- **Things that do not exist**: steel, concrete as a material, pines, soil and pavement on the
  meadow, silver prices, smelting, traits (Hard Worker, Night Owl, Iron Stomach), inspirations,
  mental breaks other than wandering, a cold-snap event, food poisoning, weight in kilograms.
- **Things that exist and had no page**: 3 of 4 tree species, bushes, berries, mushrooms, the
  meals, medical supplies, all five weapons, the slab, floor, shelf, campfire, cooker, generator,
  heater, conduit, sandbags, both zones, the home area, every order, the three kinds of person,
  five of eight live skills, four of eight live work types, recreation, every health capacity and
  injury, the weather, the year, the temperature, and every event but the supply drop.
- **Numbers invented**: a wall was "240 ticks, 75% return"; it is 135 and half. Carrots were "4.2
  days, 14 a plant"; they are about 4 days and 5. Grass was "150 ticks to clear, low
  flammability"; it is 60 and nothing burns.
- **Live counts that were never live**: "Seen on this map · 1,284 tiles", "8 beds in colony".
- **Links to nothing**: dozens of related chips named entries that did not exist (Bedrock,
  Subsoil, Pain, Iron Stomach…), and a click on one did nothing.
- **The info button guessed** from the pane's title: any tree was a pine, anything unknown was
  grass, any item unknown was the ration pack, and a crowbar or a bandit opened nothing.
- **"Find on map" on a skill, a need or a trait closed the Almanac and did nothing.**
- **No icons drew.** Every square was a placeholder or a colour swatch; the path icons in the
  catalogue were never passed to anything that draws.

## 2. What it is now

110 entries in 19 categories, in the Build palette's and the storage categories' order where they
have one: Terrain, Flora, Materials, Food, Medicine, Weapons, Structures, Furniture, Production,
Power, Zones and orders, People, Fauna, Skills, Work types, Needs, Health, Weather and seasons,
Events.

- **Keyed by registry key.** An entry's name is `Registry.Label(key)` and its index line is
  `Registry.Describe(key)` — `emit_labels.py` now emits descriptions for the Almanac's namespaces
  (`DESCRIBED`), so the wiki and the Almanac say the same sentence. An entry may answer to more
  keys (`AlsoKeys`): a picked berry bush opens the berry bush, a stair's two halves one stair.
  Three ideas have no key and are named: *Body*, *The year*, *Temperature*.
- **Thirty registry descriptions were false and are corrected** in `icon-keys.csv` (bedrock "punitive
  to mine" — it cannot be mined; coal "burns hot" — nothing burns it; marsh "three quarters pace" —
  it is 71%; rain "puts out fires"; storm "lightning"; the growing zone's "hydroponics"; and so on),
  with two flavour lines in `proper-nouns.csv`. The wiki is rebuilt from them.
- **The button does what the entry can do** (`AlmanacAction`): find the thing on the map, by key,
  closing only when something was found; or open the tab that holds its live half (Work, Animals,
  Inventory, Assign, Build); or nothing, and then there is no button.
- **Icons draw**: the owner's pixel art where the key has it (at 32 and 64, ADR 0007), otherwise
  the entry's line icon through `PathGlyph`, tinted. Related chips draw their target's line icon.
- **Search reads the definition** as well as the name and the index line.
- **Navigation is by category and name**: the Construction skill and the Construction work type
  are one word for two things, as on the Work tab.

## 3. What keeps it true

`AlmanacCatalogueTests` (fast tier):

- `EveryThingTheGameCanShowHasAnEntry` walks the HUD's own tables — `ItemLabels`,
  `EdificeLabels`, `TerrainLabels`, `PawnKindLabels`, `WeatherLabels`, `IncidentLabels`,
  `PaletteTools.Live`, `BuildLabels`, and the live rows of `SkillCatalogue` and `WorkCatalogue` —
  and fails on any key without a page. **A new item, building, animal or event fails the fast tier
  until it has one.**
- `EveryLinkGoesSomewhereElse`, `NoTwoEntriesShareAKeyOrANameInOneCategory`,
  `AKeyedEntryIsNamedAndSummarisedByTheRegistry`, `EveryIconIsAPathThatDraws`,
  `OnlyAKeyedEntryOffersToFindItselfOnTheMap`, and `NothingFromTheMockUpComesBack`, which fails on
  silver, steel, pines, traits and the rest.

`AlmanacFactsTests` reads the Def XML from disk — the HUD cannot — and holds 93 quoted numbers to
it: work, yields, stacks, nutrition, weapon damage and timing, building cost, work, hit points and
watts, species pace and range. **Retune a Def and it names the page to correct.** Numbers that live
in C# rather than XML (move costs, the needs cadence, the health curves) are copied with the file
they came from in the source comment, and are not pinned.

`RegistryTests.NoPlayerFacingNameIsWrittenInCSharp` and `TemperatureLabelsTests` both read the
catalogue; the latter's exemption for prose now covers the split files (`AlmanacCatalogue*.cs`).

## 4. Icons: what there is, and the three ways to the rest

Of the 107 keyed entries (audit script in the handover, 2026-09-26):

| State | Entries | What it takes |
|---|---|---|
| The owner's pixel art, exported | **16** — eight skills, three needs, wood, stone, scrap, iron ore, grass | nothing; draws now |
| On sheet 06, which is in the repo, marked *shared* and not exported | **8** — door, growing zone, three work types, blood loss, raid, rain | an export and an eye: is the shared tile right for this key |
| Mapped to sheets 01–05, 07, 08, **which were never committed** | **26** — wall, bed, shelf, campfire, cooker, generator, stockpile, sandbags, pistol, meal, rations, medkit, pickaxe orders… | the owner copies six PNGs into `art-source/icons/sheets/` and runs `icons.py export`; about 130 more keys elsewhere come with them |
| No art anywhere (`gap` or not in `icon-map.csv`) | **57** — trees, bushes, water, rock, animals, people, weapons, weather, health, events | drawn or photographed |

Every one of those draws its line icon today, so none is blank.

**Photographing** is feasible and is the natural route for the 3D things: a `ThingStudio` beside
`PortraitStudio` — same environment take-over, one render per key per session, cached, 64 px —
for trees, bushes, the carrot, the three animals, the three kinds of person (the portrait already
exists for colonists and a masked one for bandits), the five weapons, and the eleven buildings:
about 45 of the 57. Two constraints decide the shape. **A render of a Synty model may be drawn at
runtime and never committed** (licensed assets stay licensed), so the studio runs in the game, not
as an exporter; the animals are our own CC0 models and could be baked and committed. And **a 3D
render beside the pixel sheets is two styles**: the cheapest way to make them one is to render at
32 and point-filter up, which the studio can do in the same pass. The ground (water, rock, sand,
marsh) photographs badly at 64 px and is better as a swatch of its own shader colour.

**Drawing** the rest as line icons in the settings window's style (design 39 §4) is the cheapest
consistent route for the concepts — weather, health, events, orders — which have no model to
photograph. A Claude Design brief for twenty-odd glyphs on the 24-unit grid is the unit.

## 5. Gaps the audit found outside the Almanac

Recorded here because the facts passes found them; none is fixed by this unit.

- **A meat meal cannot be cooked**: `Kitchen` makes `Item_CookedMeal` only when an ingredient has
  `meat`, and no item does, so every cook makes a vegetable meal. Harmless (both are 900 and +50)
  until meat arrives.
- **The pace readout omits health**: `PaceModel.PerMille` and the published factors leave out
  the Moving capacity that `Pawn.MoveRatePerMille` applies, so an injured colonist's *Pace* reads
  faster than she walks.
- **Iron ore and coal are mined and used for nothing.**
- **Recreation is simulated and never shown** on the colonist pane.
- **`QualityContent`'s comment** says "nine weights whatever the skill" and "a master can never
  fall to Poor"; the draw is `NextInt(9)` against weights that sum past nine at middle skills, so
  Epic is reachable only at 20 and Poor still happens at 10–14. The bed's Almanac page quotes what
  the code does.
- **Temperature severity cannot kill**, though a Sim comment once said the Almanac promised it
  would (the comment is corrected).
- **Registry descriptions for unbuilt things** (`ui.need.comfort` and the four others, the dim
  palette chips, the category tooltips promising turrets and lighting) describe the design, not
  the game. They are the wiki's to keep; the Almanac never shows them.
