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

So Chop, Mine, Deconstruct and Cancel are all **always on show**. They pass the same test the first
two already passed: all four are *verbs applied to what is already on the board*, not nouns to
place, and all four are wanted at the moment the player is holding something else.
`EveryLiveToolIsDrawnSomewhere` is the general form of the rule, so a third occurrence cannot be
silent.

**They were pinned in this panel's header, as 26 px icon buttons, until 2026-09-17.** The owner
then moved them out to a vertical strip in the right-hand gutter, under the depth rail — the design
is `14-hud-layout.md` §5.4 and the code is `HudShell.Orders.cs`:

> the small buttons on the build menu for Chop Trees, Mine, Deconstruct, Cancel should be a
> vertical button strip that sits below the depth control and menu button … as this enables us to
> quickly give orders without having to click the build button — we can use this in future for more
> orders

The header was the wrong home for the reason the header itself made plain: *always on show* inside
a panel that is usually **shut**. Giving an order cost opening the palette first, on every order,
and the palette is a list of things to put down. **Four was the ceiling here** — the header had
room for four beside the switcher and the way out, and a fifth would have pushed something off a
1280-wide screen. In a column down an otherwise empty gutter that limit is gone.

Two things that did not change with the move. The panel still wears the held order's colour (§7),
because the two are joined by `BuildPaletteModel` rather than by being in the same box — and the
PlayMode test that proves it now picks the order up from the strip, which is the change that could
have broken it. And `PaletteTools.Pinned` kept its name: it is still the list of tools that belong
to no category and are always on show, and what it is pinned *to* is now the screen's edge.

## 3. The tiers

| Tier | Colour rule | Selected |
|---|---|---|
| Category (7) | one hue each, `HudTheme.BuildCategoryTiers` | a stronger wash of **its own** hue |
| Sub-type | neutral white-alpha | cyan, the HUD's ordinary "this is on" |
| Material | tinted **from the material** | the material's own border, doubled |

The material tier is **the same box and type as the sub-type buttons in Rows** and keeps the
specification's larger treatment only in Rail's grid — see §4a.

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
- **only Bar spans the screen**, `left:0` to `right:0`;
- **Rows is a 372 px column** and **Rail an 840 px panel**;
- all three pin into the **bottom-left corner**: `left: 0`, sitting on the command bar (owner,
  2026-09-17: *"it needs to pin/dock against the bottom and left for space — so up against the left
  screen border and also attached to the bottom bar"*). Anchoring under the Build cap with
  `PopoverLeft`, which is the rule every other popover follows, left the panel a few pixels of the
  bar's own padding short of the screen edge;
- and **opening Build closes whatever was being inspected** (same instruction: *"if the tile info
  dialog is showing, that is closed down and the build mode is open"*). The specification asked
  only for the pane to collapse to its header, and collapsing was the wrong half of the idea: the
  pane is docked in the same corner, so a collapsed header is still a strip of panel wedged between
  the palette and the bar, describing a cell the player has stopped asking about. Clearing the
  selection is also what lets the palette sit on the bar in every case rather than lifting over a
  pane whose height changes with what is selected.

This is the rule the bar and the popovers already follow, and the same words the owner used about
the popovers a day earlier: *"directly above the build button … no spacing and padding to ensure
tight space"*.

### 4a. Rows is a column, not a band (owner, second pass)

Rows spanned the screen at first — which is what the mockup drew, and what *"the first group
should use the horizontal space"* had asked for back when the palette was ten wrapping chips. Seven
tiles stretched across 1920 are seven very wide tiles with a small icon adrift in each, and the
board they cover is the board the player is aiming at. The owner's correction:

> make the 1st group of buttons short width as possible but evenly sized. You could probably fit 4
> on a row but increase the height and try to use the left hand side of the screen instead of the
> width … also make the stone/wood and material buttons evenly sized in font and size as the other
> buttons but keep the style.

So:

- **372 px wide** (`HudLayout.BuildRowsWidth`), which is the narrowest that holds four category
  tiles: 14 px of band padding each side, four tiles at 23% of what is left, 6 px after each.
  `Recreation` is the longest label and sets the floor.
- **Four categories across**, wrapping, so seven stand **two rows deep** — which is where the extra
  height comes from. Percentage widths rather than `flex-grow`, because a growing row does not wrap
  into even columns.
- **Sub-types wrap** in the same width, at their natural lengths.
- **Materials are the same 34 px box and the same 14/500 label as the sub-type buttons**, and keep
  their tint, their doubled border and their seated shadow. What said *terminal choice* was never
  the extra eight pixels and the heavier type. **Rail is deliberately unchanged**: its 86 px grid
  is the shape of that layout rather than a row in it.
- **The material band stacks** — the word, the buttons, then the price. Across a full-width band
  those sat on one line with the cost pushed right; in a column that line does not exist, and a
  66 px label column in front of two buttons would spend a fifth of the width on a word.
- **The height is fixed** at the widest category's — Structure, three rows of sub-types —
  `HudLayout.BuildRowsSubBand`, with the material row and the cost line reserved beside it
  (owner: *"the height needs to stay fixed, ie as tall as the structure menu/selection goes so it
  can accommodate all of the menus"*). The panel is docked on the command bar and grows upward, so
  a shorter category does not shrink neatly — it drops the whole control down the screen while the
  player is aiming at it. Unlike Rail's grid the row count is not arithmetic: Rows wraps by how
  wide the *words* are, so the figure is measured and the PlayMode test prints every category's
  band on every run so it can be re-derived rather than guessed at twice. All seven stand at
  **516 px**.
- **The header is two stacked lines in Rows**: what is selected, then the eight controls. In one
  row they came to 426 px and ran off a 372 px panel. The split is made in the shell rather than by
  letting the row wrap, because a wrapping row breaks wherever it runs out of room and could put
  the close button on a line of its own.

**There is no hint line.** The specification put *"Left click places · drag for a run · right click
cancels"* under the panel; the owner had it removed outright (2026-09-17). It is a sentence about
the three most basic gestures in the game, printed permanently over the board, and a player who
needs it needs it once. The tooltip on every tile still says what a drag does.

## 4b. The Build cap on the command bar, and the tool that armed itself

The owner, 2026-09-17: *"when you are in build mode the button stays highlighted — but when I come
out of build mode by escaping etc, the build button still stays bold when it shouldn't and is
confusing. It's an indicator to whether you are truly in build mode."*

Two faults sat behind that, and the second was the serious one.

**The cap could not report a state.** Build is the bar's primary item and was drawn with a solid
accent fill at all times; `.cmd--on`, the faint wash meant to say "open", was invisible underneath
it. So the fill became the state and an outline became the resting style — accent border and accent
ink at rest, the filled cap the bar always had while build mode is on. Build is still the only item
on the row with a colour of its own, so it is exactly as findable; what it no longer does is claim
to be active when it is not.

**And the game really was in build mode.** `BuildPaletteModel` is constructed when the HUD attaches
to its directors, long before the palette is opened, and its seeding pass *armed* the landing
sub-type. A wall was on the cursor from the first frame, and a click on the world would have placed
one. The lit cap was telling the truth. Nothing is armed now until the player asks: the seeded pass
sets where the palette is **pointing**, a click is what picks a tool **up**, and the sub-type tiles
light by asking `DesignateDirector` what is in the player's hand rather than by comparing against
what the palette points at.

The half of that fix which could have gone wrong on its own: with the landing sub-type seeded but
unarmed, the first tile a player reaches for is usually the one already pointed at — so the guard
in `SelectSubType` is "already pointed at **and** already in the player's hand", or that first click
would do nothing and read as a dead button. `ClickingTheTileAlreadyPointedAtStillArmsIt` pins it.

**Build mode is a tool in the player's hand, and nothing else.** It counted an open panel as well
for a few hours, on the reasoning that a player who has opened the palette is about to build. That
is not what the cap is being asked (owner: *"when you click off build mode the button shouldn't be
highlighted — ie I click esc, that button is not highlighted at all"*). Escape puts the tool down
before it closes anything, so counting the panel meant the first Escape left the cap lit over an
empty hand — the very state the complaint was about, one step further on.

What is left is the honest question: **will the next click on the world place, cancel or dig
something, rather than select it?** Any designate tool answers yes, and all of them are reached
through this panel. An open palette with nothing chosen answers no, and the open palette is its own
evidence that it is open.

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
**Rows and Rail both hold their height across all seven categories**; switching layout on screen
keeps what was armed; holding Cancel colours the panel; **the Build cap is lit only while a tool is
held** — dark when the panel is merely open, and dark again the moment the tool is put down —
checked on the resolved fill and not only on the class, because the class being set and the fill not
following is the shape that fault took first; and **all three layouts pin into the bottom-left
corner**, against the left edge of the screen and on the command bar.

**Not judged by anybody**, and that now includes **where the orders strip sits**: it is measured
against the rail and the screen edge at three resolutions, and whether its four buttons are the
right size, in the right place and far enough from the Menu button at the other end of that edge is
an eye's question.

Three portraits are written to `Logs/palette-{rows,rail,bar}.png` on every
PlayMode run for exactly this reason, and they are the only reason the mode colours, the hue set and
the line art are not pure assertion. What a picture still cannot say:

- **Whether thirty-seven drawn icons read as what they are.** They are legible and distinct in the
  portraits; whether the Production sawtooth says *factory* and the Recreation arch says *anything*
  is an eye's question and the owner's.
- **Whether seven hues are enough apart** at a glance with the labels masked. The test proves no two
  are the same colour, which is a different claim.
- **Whether Rows is the right default.** It is the owner's choice from the mockups, but the mockups
  floated across the screen and this is a docked column.
- **Whether 372 px is the right width.** It is the narrowest that fits four category tiles; three
  across would be narrower and taller still.
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
