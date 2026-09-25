# 47 — The Gear tab (G1)

**2026-09-25.** Built on `claude/vigilant-bardeen-8idplc`. Unit **G1** of `docs/plans/gear.md`, widened by
the owner to the whole tab. The owner's interview is `docs/research/gear-interview.md` (twenty-two
answers). The brief that went to Claude Design is `docs/reference/mockups/gear-brief.md`, and this
document is what came back and what was built. The gear seam it grows from is design 33 §9d.

## 1. What it is, and what is real

The colonist pane's third tab, dimmed since the pane was written, is live. It is a paper doll: six slot
tiles round a figure, a kit row under it, and a footer with the colonist's effects and loadout.

The owner ruled on 2026-09-25: *"tab + weapon real, rest preview"*.

| Part | Real today | Otherwise |
|---|---|---|
| **Weapon** (the hand) | Yes. `odyssey.pawn.weapon` and `PawnFlags.Drawn`, with *Unequip* and *Drop*, and Pick from stores listing the stored weapons | — |
| **Downed** | Yes. `PawnFlags.Downed` | — |
| **Body** | The issued jumpsuit, always | the preview's garment |
| **Head, Face, Back, Armour** | *Nothing worn* | the preview's things |
| **Kit** | Two empty belt slots; four pack slots locked, with the hint | the preview's four stacks, and the pack's slots open |
| **Effects** | The bare defaults, drawn dim: 0 %, 16 to 26 °C, 0 %, 0 of 2 | summed from the preview's things |
| **Loadout** | *None* | *Doctor*, or whatever was picked |

**The preview** (§4) is the debug menu's *Preview full kit*. Nothing in the simulation wears, carries or
has a loadout until units G3 to G6 exist.

## 2. The layout — Claude Design's numbers, as built

Every number is in `Odyssey.Hud.GearLayout`; Presentation writes none of its own.

| | |
|---|---|
| Tab body | **244**, the body for **every** tab (`HudLayout.InspectTabBody` = max of the needs, skills and gear bodies). Owner: 244 for all, 2026-09-25. |
| Down the body | 12 padding, doll 138 (three rows of 40 and two gaps of 9), 12, kit row 40, 12, footer 30. That sums to 244, held by `GearLayoutTests`. |
| Doll | `1fr 96 1fr` with a gap of 12. The figure is 96 × 138 in the row-rule fill (a placeholder; no person is drawn). |
| Slot order | Left: Head, Face, Body. Right: Back, Armour, Weapon. The weapon sits lowest, at the hand. |
| Slot | 40 tile, 32 icon, 9 to the words. **The tile always faces the figure**: left rows are `row-reverse` with right-aligned text. The name is 14/500, ellipsised. The meta line is the slot label (11/600 tracked), then the quality in its tier colour, then *At the hip* / *Drawn*. |
| Slot states | Filled: 1 px border. Empty: dashed `--ctl`. Jumpsuit: bordered, icon at 45 %, *Always worn* dim. Selected: accent border and a 12 % accent fill. |
| Kit | KIT label 40 wide, then six 40 tiles 9 apart. Filled: a count badge (14 high, mono) in the corner. Empty: `--ctl` border. Locked: dashed `--border` and a drawn lock (16, stroke 2, `--cap`). The hint *Wear a pack for 4 more* shows once. |
| Footer | Top rule. Four effects 18 apart, each a meta label then a 12 mono value; **a default value is drawn dim, never dropped**, so the line never reflows. Then LOADOUT and a 22-high cell with a drawn down chevron. |
| Popovers | Open **outside the pane on its right**, at the pane's edge + 9 (569 when the pane is at 0), level with the tile. They never cover the doll. Item popover 260 wide; Pick from stores 340 wide, 34 header, 40 rows, at most 8 a page with a pager, and never a scrollbar. |

## 3. What the buttons do

- **Unequip** and **Drop**, on the weapon, send the new `IntentKind.OrderUnequip`. Its A is the
  colonist and its B is 0 for Unequip, 1 for Drop.
  - The hand empties **at once**, through `WeaponHand.PutDown` (the door a swap and a death already
    use), and the weapon lies at her feet.
  - **The two words differ by one flag.** *Unequip* ("put it down") leaves a loose thing for the
    haulers. *Drop* ("leave this here") forbids it.
  - Refused for anybody but a standing colonist of ours. It applies while paused. It is appended
    last, so nothing renumbers. `UnequipTests` has seven cases, and the forbid rule was seen to fail
    with its condition flipped.
- **Pick from stores** opens on any empty slot, the jumpsuit's Body included.
  - For the hand it lists every weapon in a stockpile or on a shelf, named by its store in the one
    form the pane and the Inventory tab write (`InventoryPlace.NameOf`).
  - Choosing a row sends the **same** `OrderEquip` as the right-click menu's row. Both call
    `CombatOrders.Equip`, and `GearPickModelTests` holds them equal.
  - A weapon loose on the ground is not "in the stores", and the right-click menu reaches it.
  - For a worn slot the list is empty (*Nothing in the stores fits*) unless the preview is on.
- **Remove** and **Drop** on a preview item, a preview kit tile, and the loadout picker all edit the
  preview only.
- **Downed** (the specification's 21f):
  - Everything stays worn.
  - Every button is there at 40 %, disabled, with *Downed. Only the Strip order can take this.*
    above it in warn.
  - The loadout cell is at 40 % and does not open.
  - The pane's activity line turns warn on every tab.
  - The Strip order is G7.
- **Closing** a popover: its own click again, a press anywhere but the popover or the pane, a new
  subject, leaving the tab, the board menus closing, or **Escape**. Escape uses a new second rung,
  `EscapeAction.CloseGearPopover`, under the context menu and above everything else.

## 4. The preview

`Odyssey.Hud.GearPreview`, switched by Debug → Cheats → **Preview full kit**.

- It is interface state and nothing else: never saved, never hashed, never sent, and reset by every
  `SessionChanged`.
- Switched on, every colonist wears the specification's 21c:
  - a Wool cap (Normal), a Gas mask (Decent), a Field coat (Uber), a Pack (Poor) and a Padded vest
    (Normal);
  - the kit holds Medical supplies 4, Rations 2, a Torch and a Canteen;
  - the loadout is Doctor;
  - the effects read **24 %, 4 to 26 °C, 50 %, 4 of 6**.
- **The hand is never made up**: 21c's drawn crowbar is a real crowbar in a drafted colonist's hand.
- Edits are per colonist and forgotten at switch-off:
  - taking off the pack locks its four slots and loses what was in them;
  - taking off the coat brings back the jumpsuit.
- The effects' arithmetic is the preview's: base 16–26 °C plus each thing's offsets. That base is
  `GearModel.BareComfortLow/High`, a copy of `Temperature.xml`'s comfort band that the interface
  keeps **only until G5 publishes the real range**.

## 5. Where the build departs from the specification

| Specification | Built | Why |
|---|---|---|
| 12 side padding in the body | none of its own | The pane already pads every tab by 12; a second 12 would narrow the doll by 24 for nothing. |
| "4 to 26 C", no degree sign | **"4 to 26 °C"** | The brief was wrong: both shipped fonts draw °, `HudFontTests` proves it, and `TemperatureLabels` owns the form (`Range`, whole degrees). |
| Mock *Flashlight* | **Torch** | The registry has called `ui.item.flashlight` "Torch" since M3, and the registry wins. |
| Crowbar "Normal" | **the weapon's real tier** beside the slot word, tinted | Built with none, because weapons had no quality; ranged combat gave them one (`CombatAspectNames.WeaponQuality`, `ThingView.Quality`) and the merge with `main` on 2026-09-25 wired it into the weapon tile, its popover and *Pick from stores*. The inspect pane brackets it into the name ("Pistol (Decent)"); the Gear tab writes it where a worn thing's goes. |
| Tab strip "no radius, 2 px inset line" | the shipped strip | The specification says the header is unchanged; the strip keeps its USS. |
| Loadout picker from the Assign tab | a small picker beside the pane: None, plus Doctor / Winter under the preview, and a line saying loadouts arrive with their editor | The column and the editor are G6. |
| Kit hover name "through the game's tooltip" | `VisualElement.tooltip` | That is the game's tooltip. |

## 6. What it touched

- **Simulation:** `OrderUnequip` (`Sim.Contracts/Intents.cs`, `JobSystem.Equip.cs`,
  `ColonyComposition.cs`). No golden moves: an order nobody sends changes nothing.
- **Hud (fast tier):**
  - `GearModel` (six slots, the kit, the effects, the loadout, downed; rebuilt only when something
    moves);
  - `GearLayout`, `GearPreview`, `GearPickModel`;
  - `CombatOrders.Equip/Unequip`, `InventoryPlace.NameOf`, `TemperatureLabels.Range`;
  - the pane's tab names from the registry (`InspectModel.TabNeeds` and the rest, `Showing(key)`),
    as design 33 §9d asked;
  - the Escape rung;
  - `HudIcons.Lock` / `ChevronDown`.
- **Presentation:** `HudShell.Gear.cs`, plus wiring in `.Inspect`, `.Debug`, `.Start`, `.Panels`,
  `HudShell.cs` and `SettingsPresenter`. `DashedOutline` takes a colour.
- **Content:** 41 registry rows (`ui.gear.*`, the pane's `ui.tab.*`, four placeholder items,
  `ui.command.remove`, `ui.debug.gearpreview`). The wiki lists `ui.gear` on its items page.

## 7. Not yet proven

**Presentation was not compiled.** The session that built it had no Unity, only a Roslyn syntax
parse, and the fast tier compiles neither Presentation nor Editor. The Unity tier is owed:

- the EditMode compile;
- `HudGeometryTests` and the overlap cases with the pane 87 px taller (371 rather than 284 at 1×);
- `DockedTabGeometryTests`;
- a look at 1920 × 1080.
