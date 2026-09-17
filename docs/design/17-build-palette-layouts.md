# 17 — The Build palette: three layouts over one selection

**Status:** built, both tiers green, **nobody has pressed Play on it.**
**Supersedes:** the A7 section of `10-ui-panel-catalogue.md` and the palette half of `14-hud-layout.md`.
**Depends on:** `15-building.md` (what a build order is), `16-cancel-and-deconstruct.md` (two of
the four header actions), ADR 0007 (why the icons are drawn rather than imported).

Read this before changing anything in `HudShell.Build.cs`, `BuildPalette.cs` or the `bp__*` block
of `Hud.uss`.

---

## 1. What was asked for, and what it is

The owner supplied a specification and three rendered mockups — 4a *Rows*, 4b *Rail*, 4c *Bar* —
with **4a as the default** and the other two switchable from inside the panel, the choice
persisting. All three share one data source and one state object.

That last clause is the whole design. A switch between three arrangements that could lose the
player's place is a setting nobody dares press, so there is exactly one place the selection lives:
`BuildPaletteModel`, in the Unity-free `Odyssey.Hud` assembly. The three layout builders in
`HudShell.Build.cs` read it and decide nothing. "Switching layout never changes selection" is
therefore a property of the structure rather than a thing three builders each have to remember.

## 2. The seven categories, and where Orders went

Ten became seven: **Orders, Zones and Salvage came off.** The remaining seven all answer one
question — *what would you like to put down* — and a palette whose tiles do not all answer one
question is a palette the player has to read rather than aim at.

Zones and Salvage took nothing live with them. **Orders did**, and this is the part a later session
should not undo by tidying. Mine and Chop were its only two live tools. Dropping the category as
written would have left the game's mining and felling reachable by the `M` and `C` keys and by
nothing a player could see — which is not a hypothetical, it is precisely what had already happened
to Cancel: *"the tool was never missing; every way of finding it was missing"*, and it cost a
playtest to find (`16-cancel-and-deconstruct.md` §1).

So Chop, Mine, Deconstruct and Cancel are all **pinned in the panel header**, as 26 px icon
buttons. They pass the same test the first two already passed: all four are *verbs applied to what
is already on the board*, not nouns to place, and all four are wanted at the moment the player is
holding something else. `EveryLiveToolIsDrawnSomewhere` is the general form of the rule, so a third
occurrence cannot be silent.

Four is the ceiling. The header has room for four beside the switcher and the way out; a fifth
starts pushing something off a 1280-wide screen, and a pinned row that keeps growing is a second
palette.

## 3. The tiers

| Tier | Colour rule | Selected |
|---|---|---|
| Category (7) | one hue each, `HudTheme.BuildCategoryTiers` | a stronger wash of **its own** hue |
| Sub-type | neutral white-alpha | cyan, the HUD's ordinary "this is on" |
| Material | tinted **from the material** | the material's own border, doubled |

The category tier is **the one place in this HUD allowed a hue per row**, and `HudTheme` carries the
argument: `HudCategory` a few lines below it exists on the opposite principle (eight categories
sharing five tokens) because that is about icons scattered over a whole screen, where a hue must be
recognised out of context. These seven are a closed row always drawn together, and the acceptance
criterion is "each is visually distinct with the labels masked", which a shared five-token set
cannot meet by construction.

Selection on that tier is **never the global cyan**. Cyan would destroy the only thing the tier is
for: with a global highlight the selected category stops being the one category you can identify by
colour.

**Materials keep the game's own sprites.** They are the one tier whose art does not change — the
specification says so in as many words — so they are `IconBadge` slots on `ui.res.wood` and
`ui.res.stone`, the same keys the stores panel draws. Everything else in the panel is drawn line
art (§5).

**A tinted material button always means buildable.** Out of stock drops the tint entirely rather
than dimming it, which is what lets the stock state be read without putting a number on the button
— and **no stock counts render on the buttons**, by instruction.

## 4. Where the panel sits — the one place the specification was overruled

The mockups drew all three floating at a 28 px margin, the panel's corner in the screen's corner.
They were drawn over a bare board. The real screen has the **stores panel docked at `left:0, top:0`**
and the **roster strip across the whole top**, so a panel there covers both while it is open.

Asked directly, the owner's answer was: *"tight and flush to other elements to enable full use of
space"* (2026-09-17). So:

- the 28 px margins are gone;
- **Rows and Bar span the screen**, `left:0` to `right:0`;
- **Rail keeps its 840** and stays anchored to the button that raised it, like every other popover;
- all three dock **flush on whatever is under them** — the command bar, or the inspect pane's
  collapsed header when something is selected.

This is the rule the bar and the popovers already follow, and the same words the owner used about
the popovers a day earlier: *"directly above the build button … no spacing and padding to ensure
tight space"*.

The hint line (*"Left click places · drag for a run · right click cancels"*) is outside the panel
but is a **child** of it, absolutely positioned above its top edge. Putting it in the shell and
working out where the panel's top was drew the sentence straight through the material buttons: the
panel is docked, its height changes with the layout, and the measurement does not exist when the
panel is first placed.

## 5. The icons are drawn, and why

The specification asks for *"1.8px-stroke line art on a 24px grid, single colour inheriting the
label colour"* and forbids the placeholder square anywhere in the palette. The ADR 0007 pixel
pipeline covers nineteen keys and **not one of them is an architecture tool**, so every tile in the
panel would have been an outlined box.

`HudGlyph.Palette.cs` adds **thirty-seven** `Painter2D` paths to `HudGlyph`'s existing 24-unit box,
sharing its stroke rule and its helpers — which is what makes a category tile and a play button read
as parts of one interface. One path each, no texture, no atlas, no licence, and **the key is still
the contract**: `PaletteGlyphs.For` maps a registry key to a shape, and the day a sheet covers these
keys the slot goes back to `IconBadge` with nothing about the layout moving.

Two tests hold the line: `EveryPaletteKeyHasItsOwnShape` walks `PaletteTools.IconKeys` in the fast
tier, and `EveryTileInTheBuildPaletteDrawsSomething` walks the open panel in all three layouts and
fails on any `Placeholder`.

## 6. Rail's promise, and the three times it leaked

Rail exists for one reason: **its height never changes when you switch category, so nothing below it
reflows.** That promise was broken three times, each found by the test written for it rather than by
reading the code, and each needing a different fix:

1. **The sub-type grid sized itself to its contents.** Structure has six sub-types, Security three.
   376 px against 343. Fixed at two rows — the largest category — and left ragged for the rest.
   `HudLayout.BuildRailSubRows`, held to the real table by a fast-tier test so a seventh tool fails
   in eleven seconds rather than in a PlayMode run.
2. **The material band collapsed entirely** for a category whose first buildable tool is not made of
   anything, which is five of the seven. A hidden row takes its height with it. Reserved.
3. **The cost line did not stand as tall when empty.** 17 px. Its minimum height is the renderer's
   line box, taken from `HudText` where that fact belongs.

All seven categories now stand at **396 px**, printed every run by
`TheRailLayoutKeepsItsHeightWhateverCategoryIsOpen`.

The cost of the promise is an **empty band under the word MATERIAL** while an order is armed. That
is the honest price, and it is visible only in Rail: Rows and Bar let the band collapse, because
neither ever claimed its height would hold still.

## 7. Mode colour — you can see which tool you are holding

Owner, 2026-09-17: *"the cancel/deconstruct colours … should also be represented in the dialog … so
it becomes clearer what mode you are in"*.

Each of the four pinned actions has a hue of its own — `HudTheme.PinnedActionHue`, four existing
signal tokens rather than four new ones:

| Action | Colour | |
|---|---|---|
| Chop | `Good` | `#7fc98c` |
| Mine | `Info` | `#8fd0e3` |
| Deconstruct | `Warn` | `#e8b55c` |
| Cancel | `Bad` | `#e06a5c` |

While one is held, the panel wears it: the header line becomes **that action's name in its colour**,
its icon appears beside it, and the panel's **top edge becomes a 2 px hairline** of it. The header
line is a sentence to read; the edge is seen without reading, which is the half that actually
answers the question. The breadcrumb is *replaced* rather than joined, because what the palette
would have built is not what is about to happen, and a header showing both reads as though it might
be.

The floating armed banner is suppressed while the palette is open — it said the same thing, in the
middle of the screen, over the panel that had just set it.

## 8. Persistence and the second control

The layout is stored as `ui.settings.buildlayout` on `SettingsDirector`, beside the interface scale
and the camera speed, and it is **one preference with two controls**: the three-icon switcher in the
panel header, and a row in **Options › Interface**. Both read and write the director, so neither
appears to undo the other.

The settings control is a **three-rung ladder, not the dropdown the specification asked for** — the
same shape as the two multiple choices already on that panel, and for the reason written against
those: a handful of honest answers rather than a control that hides two of three until it is opened.

The store key is `ui.settings.buildlayout`, not the specification's `ui.buildPaletteLayout`: in this
project a settings row's registry key and its store key are the same string, and the row needs a
registry name so the owner can correct it in `icon-keys.csv`.

**A layout switch is refused mid-drag.** The cells already swept have no meaning in the differently
shaped panel that would replace this one; finishing or abandoning first is one extra click and the
only version of this that cannot leave a half-placed run on the board.

## 9. The one token that departs from the specification

`HudTheme.SubTypeDisabledInk` is **0.35 where the specification says 0.30**.

At 0.30 the label of a thing you cannot build yet measures **2.71:1** against its own chip over the
brightest terrain the game can draw; at 0.35, **3.21:1**. WCAG allows either — it exempts inactive
controls — and on a finished game 0.30 would be right, because a greyed-out row is meant to recede.

It is wrong *here*, temporarily and specifically: **one of the twenty-seven sub-types is live.** Five
of the seven categories are disabled end to end, so the greyed-out state is very nearly the whole
panel and is the only thing telling a player what this game is eventually going to let them build. A
label nobody can read is a roadmap nobody can read. When the palette is mostly live this goes back
to 0.30 and the test holding it to 3:1 is deleted, deliberately.

## 10. What has been proved, and what has not

**Proved, fast tier** (`BuildPaletteTests`, 25 tests): a fresh profile opens in Rows; a chosen
layout survives a reload; a nonsense stored value falls back; switching layout preserves category,
sub-type and material exactly; a switch mid-drag is refused; both controls are one preference; seven
categories and no more; every category has its own hue and no two share one; selection on that tier
is the category's own colour and stronger than its resting state; a category opens on its first
*buildable* entry; each sub-type remembers its own last material; an order is made of nothing; an
empty store takes its material off the menu and an armed build falls off it; the breadcrumb and the
cost line read as specified; every pinned action has a distinct colour and nothing else claims one;
every label clears 4.5:1 on its own button, including Power and Wood; the Rail grid fits the largest
category.

**Proved, PlayMode** (`HudGeometryTests`): no row overflows in any layout at 1280×720, 1920×1080 or
2560×1440; no panel overlaps another; every tile draws a real shape rather than the placeholder;
Rail holds its height across all seven categories; switching layout on screen keeps what was armed;
holding Cancel colours the panel.

**Not judged by anybody.** Three portraits are written to `Logs/palette-{rows,rail,bar}.png` on every
PlayMode run for exactly this reason, and they are the only reason the mode colours, the hue set and
the line art are not pure assertion. What a picture still cannot say:

- **Whether thirty-seven drawn icons read as what they are.** They are legible and distinct in the
  portraits; whether the Production sawtooth says *factory* and the Recreation arch says *anything*
  is an eye's question and the owner's.
- **Whether seven hues are enough apart** at a glance with the labels masked. The test proves no two
  are the same colour, which is a different claim.
- **Whether Rows is the right default.** It is the owner's choice from the mockups, but the mockups
  floated and this docks.
- **Whether Bar's icon-only tiers are usable** without hovering everything.
- **Whether the empty MATERIAL band in Rail reads as deliberate** or as something broken.

## 11. Still open

- **Concrete and Steel are not offered.** The material band is driven by what
  `ConstructionContent.IsBuildable` admits, which is Wood and Stone. Tints for all four are written
  (`HudTheme.MaterialTintOf`) and shown by nothing, so the day either becomes buildable it arrives
  coloured with no further decision — and neither needed an invented registry key, which matters
  because concrete was struck out of the content on 2026-09-16.
- **The sub-type tier is the category's own tools**, not the mockup's fixed seven. The mockups drew
  one hard-coded row; the specification's own behaviour section ("selecting a category resets the
  sub-type to its first buildable entry") is what settles it.
- **`HudLayout.BuildHintBlock` is now only a fallback.** The hint places itself from USS; the
  constant survives for the case where the panel has not been laid out yet.
- **The 4.5:1 check is against the panel over white**, the harshest case. It says nothing about a
  label over a *tinted* neighbouring tile, which is not a case that arises today.
