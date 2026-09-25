# Gear — owner interview

**Phase:** Interview (feature-level, in the shape of `home-area-interview.md`).
**Date:** 2026-09-25. **Branch:** `claude/vigilant-bardeen-8idplc` (documents only; no code was
written before or under this file).
**Conducted by:** Claude Code, twenty-two questions in six rounds, after a read-only exploration of
the weapon hand, the equip order, the haul load, the item model, the disabled Gear tab and its
`GearModel` seam, the modular-colonist clothing plan, the bandit outfit layer, the Assign tab, the
icon registry and the research record.

The owner's brief: *"We need to look into gear, check the repo first but at the moment we have
weapons etc. Make suggestions to what the interface might be (we can use claude code design to pass
off). Have it like a rpg where you have weapon equipped and maybe colonists can carry some other
smaller items - but whats the limit - but they need to carry heavy ones like stones, logs etc. Also
we'd liked to bring hats in, clothes and various other things so make suggestions what goes in gear.
Plan and explore and interview - clarify every detail."* Mid-session: *"Provide the claude design
prompt for these screens."*

**Read next:** `docs/reference/mockups/gear-brief.md` (the Claude Design prompt),
`docs/plans/gear.md` (the staged units), `docs/research/a-19-apparel-and-gear.md` (queued: the
reference's numbers), then the design doc when it is written.

## 1. What the exploration found, put to the owner before the first question

- **One weapon slot exists and is real.** `Pawn.EquippedItem` (`Sim/Pawns/Pawn.cs`) points at an
  ordinary `ColonyItem` with `CarriedBy` set (`Combat/WeaponHand.cs`), saved in `odyssey.combat`.
  *Equip* is a right-click row (`Hud/ContextMenuModel.cs`, design 33 §7a) driving
  `EquipJobDriver`. The weapon is drawn from its own ground art on the right hand and sheathed at the
  left hip (`WeaponSheath.cs`). **There is no Unequip or Drop order.**
- **The Gear tab exists and is disabled** (*"equipment arrives with the inventory"*,
  `InspectModel.AddColonistTabs`), with a Unity-free `GearModel` behind it holding one row,
  `GearSlot.Weapon` (design 33 §9d, "a seam only").
- **The haul load is not gear.** `Job.CarriedItem` lifts the **whole stack** — 75 logs in one armful
  — at full walking pace. Nothing has mass and there is no capacity, both deferred on purpose
  (design 24, and the owner's rule *"do not invent an urgency model"*).
- **`ItemDef` knows nothing about size or wearing.** The category `Items` (*"carried, worn or used,
  and are not weapons"*) is declared and unused.
- **Clothing is presentation only.** The issued jumpsuit and the bandit outfit are drawing layers.
  Design 29 §9 already plans clothing as equipment (CL1–CL6) with one hard constraint: **Synty
  cannot layer garments** — a garment is a swap of which whole-body mesh is active, armour is an
  overlay mesh, headgear is a rigid prop on the head socket that was built and left empty.
- **Art on disk** (Battle Royale): ~17 garments, 6 armour overlays, ~35 hats, 3 helmets, masks and a
  gas mask, 3 bags, 8 pouches.
- **The names were reserved**: `ui.item.helmet/vest/jacket/mask/boots/pack/shield`, kit items
  (flashlight, radio, canteen, bedroll…), `ui.command.equip/unequip/wear/pickup`. No slot, tab, kit
  or loadout keys.
- **Never researched**: apparel layers and coverage, insulation, armour values, outfit policies,
  personal inventory, carry capacity, tailoring (`a-13` and the apparel half of `a-14` are open).

**Prior art put to the owner** (for the limit on small items): weight budgets (RimWorld, Project
Zomboid, Kenshi) are realistic but unreadable at a glance and heavy to tune; a grid inventory
(Tarkov, Resident Evil, Kenshi's packs) is tactile but fiddly across a colony; a paper doll with
slots (Diablo, Skyrim, Going Medieval) is instantly readable but arbitrary in its count; bulk
classes (Dwarf Fortress) give one clean rule — small things in pockets, big things in the arms.
Recommended: **slots plus a size class**, because readability is the project's first value and
nothing in the item model has a mass to budget.

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | The limit on small carried items | **Slots plus a size class.** Every item is *pocket*, *pack* or *arms-only*; logs, stone, ore and scrap are arms-only and never go in a slot. |
| 2 | Heavy loads (75 logs in one armful today) | **A cap per trip by bulk.** More trips; no speed model. |
| 3 | What the first unit delivers | **The interface and the weapon verbs**: the Gear tab as a paper doll with the weapon and the jumpsuit, Unequip and Drop, every later slot drawn empty. No golden re-bake. |
| 4 | What gear eventually covers | **Clothes, hats and armour; kit consumables; utility items.** **Not tools** — the strokes stay computed and no axe or pick is an item a colonist keeps. |
| 5 | What worn clothing does | **All four**: warmth (widens the comfort band, design 28), armour (turns blows aside, design 33), rain (buys back the rain's pace, design 43's hook), and mood and look. |
| 6 | Quality and condition (design 29's open question) | **Quality only**, the beds' five tiers (Poor, Normal, Decent, Uber, Epic) as a multiplier. **No wear**; add it later only if it earns its place. |
| 7 | The issued jumpsuit | **Not an item.** An empty Body slot draws it. No naked colonists, no uniform stock. |
| 8 | Filling the kit | **A loadout policy plus manual orders.** A named loadout per colonist; colonists top up from the stores themselves; Take and Drop by hand as well. |
| 9 | The worn slots | **Head, Face, Body, Armour, Back.** Hands and feet are part of the body mesh and cannot be drawn separately. |
| 10 | Kit size | **Belt 2, plus 4 while a pack is worn.** One kind of thing per slot, a small stack up to a per-item cap (e.g. 5 medical supplies, 3 rations, 1 flashlight). |
| 11 | The Gear tab's layout | **Paper doll + kit strip + an effects line**, inside the inspect pane. |
| 12 | Where loadouts are managed | **A Loadout column on the Assign tab (F4) and a loadout editor.** |
| 13 | How many weapons | **One.** A spare is an ordinary kit item, not ready to use. |
| 14 | Where gear comes from first | **Supply drops, bandit loot, crafting at a tailor bench.** *Not* a starting kit. |
| 15 | How the player moves gear | **The right-click menu and buttons on the Gear tab**: Equip / Wear / Take into kit on the board; Remove / Drop and *Pick from stores* on the tab. **No drag and drop.** |
| 16 | How the trip cap is set | **Per commodity in the XML** (for scale: logs 20, stone 15, ore 25, scrap 25, food 75 — numbers for research to confirm), shown in the wiki. |
| 17 | What is drawn on the figure | **Every worn slot, the pack on the back included; kit contents not.** Belt pouches only while the kit holds something. |
| 18 | Downed and dead | **The body keeps its gear; a Strip order** drops it beside them for hauling. The weapon still drops at death, as today. |
| 19 | A colonist with no loadout | **Keeps what they have.** Nobody changes clothes on their own. |
| 20 | Where fabric comes from | **Salvaged cloth from the ruins, a fibre crop, and traders.** |
| 21 | What the Claude Design brief covers | **The end state, built in stages.** |
| 22 | The names on screen | **Gear** (the tab), **Kit** (small things kept on a colonist), **Loadout** (the policy). *Inventory* stays the colony-stores tab on F2. |

## 3. What follows from the answers

- **Three places, three rules.** The hand (one weapon, exists), the body (five worn slots), the kit
  (2 + 4 slots). The arms are a fourth place and stay the haul load's, with a cap per trip.
- **The kit's limit is its slots, so no item needs a mass.** An item needs a *size* (pocket / pack /
  arms) and a *kit cap*; a haul needs a *carry limit*. Three integers on `ItemDef`, all content.
- **The jumpsuit being no item** means the Body slot's empty state is a real, drawn, named thing,
  and no save has to invent a uniform for every existing colonist.
- **A downed colonist cannot be dressed by hand**; only Strip reaches the downed and the dead.
- **Loot needs bandits' gear to be real items**: the welding helmet and the red vest (design 42)
  become worn items rather than a drawing layer, which is the design's own recorded seam.
- **Crafting is the largest unit and needs its own interview**: bills and a workbench do not exist,
  a fibre crop is a growing unit, and traders need factions (`a-13`, unwritten).

## 4. Open numbers (for `a-19` and the design doc, not the owner)

- Each commodity's trip cap, and each small item's kit cap.
- The warmth offsets (how far a coat moves the 16–26 °C band), the armour percentages and how a
  piece's armour enters design 33's blow, the rain buy-back of each piece, the mood of quality.
- What a loadout does when the stores cannot satisfy it, and how often a colonist checks.
- Whether a Strip is a job with a duration or instant at the body.
