# G-01 — Reference UI: information design, region by region

## Question

The owner asked for a user interface "like this", pointing at the reference colony sim's
documented interface. What is the complete taxonomy of its screen regions and panels, what
does each communicate, how is density managed, and what does each region have to become once
the world has discrete vertical layers and is set in a ruined city?

Clean room (brief §1): this file records **mechanics, information design and structural
shape**. It contains no text, names, flavour text or Def content from the reference game.
Every name used below is ours.

## Method and its limits

`rimworldwiki.com` and `steamcommunity.com` are **blocked by this container's egress proxy**
(connection refused at the network layer, not a TLS or authentication failure). The page the
owner linked could not be read. Verified by direct request and by the fetch tool independently.

The taxonomy below is therefore reconstructed from familiarity with the shipped game plus one
reachable secondary source on its UI class structure, and cross-checked against the owner's
two concept renders as described in `docs/reference/screenshots/README.md`. It is reliable on
**structure** (which regions exist, what each is for, how density is managed) and should not
be trusted for exact counts, orderings or labels. Anything depending on those is listed under
*Could not be determined*.

One useful structural corroboration did come through. The reference game's UI is built from a
window stack, a set of main tab windows, an inspect pane composed by a filler, and a grid of
"gizmo" command objects that attach to whatever is selected. That confirms the three
structural ideas worth carrying forward, and one worth deliberately rejecting.

## Findings

### F1 — The screen is a ring of docked readouts around a live world

Nothing in the reference interface is a full-screen takeover during play. Readouts occupy the
screen edges; panels float over the world; the simulation stays visible and running behind
everything. The owner's night-time concept composite independently arrives at the same
grammar: four different panels, all mid-sized floating windows, world visible behind each.

This is the single most important information-design decision in the genre and it is free to
adopt. It is what makes a colony sim feel observed rather than administered.

### F2 — Region taxonomy

Each row gives the region, what it communicates, how it manages density, and what verticality
and the ruined-city setting do to it. "Layer impact" is the column that matters most, per
brief non-negotiable 2.

| Region | Screen slot | Communicates | Density technique | Layer impact | Ruined-city impact |
|---|---|---|---|---|---|
| Resource ledger | top-left | current stock of the ~40 tracked commodities | icon plus number, no labels; only non-zero rows; grouped by kind | none — resources are colony-wide, not per layer | salvage classes replace ore classes; scrap tiers need their own icons |
| Colonist roster bar | top-centre | every colonist at a glance: identity, mood, health, current activity | one small card each; state encoded in ring colour and corner glyphs, not text | **needs a layer badge** — "who is on which layer" is a question this bar is the only place to answer | none |
| Date, season and weather readout | top-right | in-game clock, date, season, outdoor conditions | one compact stack, text-light | outdoor temperature and weather apply to exposed layers only; the readout must say it is the *outdoor* value | roofed street level is a common case, so "outdoor" is less often what the player is standing in |
| Play-speed controls | top-right, under the clock | paused / normal / fast / very fast | four mutually exclusive icon buttons | none | none |
| Alert stack | right edge, vertical | continuously re-evaluated bad conditions, worst at top | colour plus position plus icon; collapses to a count when long | **each alert must carry its layer and offer a jump** — an alert with no z is a dead end on a 40-layer map | breaches from service tunnels are a new alert class |
| Bulletin stack and archive | right edge, above the alerts | discrete narrative events, each dismissible, all searchable afterwards | one card per event; the archive is a virtualised list | every bulletin carries a jump target including z | salvage discoveries and tunnel collapses are new bulletin classes |
| Architect toolbar and palette | left edge, expanding | everything the player can place, order or paint | two levels: category icons, then a palette of tools within the category | **placement validity is a per-cell, per-layer question** with support and roof rules; the palette must show why a cell is refused | reclaiming a pre-existing shell is a distinct verb from building new |
| Main tab bar | bottom edge | the roster-wide management views | one icon per view, mutually exclusive, hotkeyed | the work and schedule grids are colony-wide, so layer-neutral | none |
| Inspect pane | bottom-left | full detail of the current selection | fixed frame, tabbed when the subject is complex | a cell selection must state its layer; a thing's pane should show what is above and below it | ruined structures need a condition and salvage-yield readout |
| Command grid | inside the inspect pane | every legal action for the current selection | square icon buttons, disabled with a reason rather than hidden | some commands' legality depends on the layer above, for example roofing | deconstruct versus salvage are different commands with different yields |
| Overlay toggles | near the tab bar | which world-data layers are painted onto the world | one toggle per channel, with a legend | **overlays are per layer** and only the active slice is drawn | a salvage-density overlay is new |
| Tooltips | follow the cursor | the name, hotkey and one-line meaning of whatever is hovered | delayed, transient, never stacked | a cell tooltip must state its layer | none |
| Cancel affordance | bottom-right | escape from the current mode | one large, unmissable button, mirrored on the escape key | none | none |

### F3 — The density techniques, extracted

Five techniques do nearly all the work, and they are what "like this" actually means:

1. **Icon plus number, no label.** A label costs three to five times an icon's width. The
   reference interface uses labels almost nowhere in its readouts. This is why the owner's
   instruction to use icons for now is not a shortcut but the actual target design.
2. **Encode state in colour, ring and glyph position** rather than in extra rows. A colonist
   card conveys mood, health, activity and alerts in the space of a thumbnail.
3. **Disable with a reason, never hide.** A greyed command that explains itself teaches the
   game. A missing command is a mystery. This is a strong, cheap rule.
4. **Two-level drilldown for large sets.** Category then palette; tab then grid. Never one
   flat list of two hundred things.
5. **Progressive disclosure by selection.** The inspect pane and command grid are entirely
   derived from what is selected, so the screen's complexity tracks the player's attention
   rather than the game's total complexity.

### F4 — What to reject

The reference game draws its UI in immediate mode: every frame, every widget is rebuilt from
a draw call that also contains the widget's logic. Three consequences, all of which we should
design away from rather than inherit:

- **It allocates every frame by construction.** At a thousand-plus widgets this is a permanent
  garbage treadmill, and it is the part of the reference implementation least suited to our
  stated target of 60 FPS at 3× speed on a 2022 mid-range laptop.
- **It is effectively untestable.** Logic and drawing are fused in one method, so there is no
  seam to assert against. Our brief requires headless tests as a milestone gate, so this alone
  disqualifies the pattern.
- **Its modding surface is code.** Mods extend the interface by patching draw methods, so
  every internal refactor breaks them. Our brief wants data-driven extension from day one.

The structural ideas worth keeping from it are the **window stack**, the **selection-derived
inspect pane**, and the **command grid attached to the selection**. Those are architecture,
not implementation, and they are good.

### F5 — The region the reference has no equivalent for

The reference game is two-dimensional, so nothing in its interface answers "which layer am I
on, what is above me, what is below me, and how do I get there". The owner's concept renders
have the same gap: `docs/reference/screenshots/README.md` records that all views are
single-layer roofless cut-aways and that the concept never shows two storeys at once.

This is the one region we must invent rather than adapt. Named here as the **Depth Ruler** and
specified in `docs/design/10-ui-panel-catalogue.md`: a vertical scale of the world's layers
showing the current slice, per-layer occupancy, and markers where alerts and bulletins are
pending, with click-to-jump. It absorbs the screen slot the concept renders waste on a
duplicated colonist bar, which the README had already flagged as redundant.

## Recommendation

Adopt the region taxonomy in F2 wholesale, adopt the five density techniques in F3 as stated
rules, reject the immediate-mode implementation pattern in F4, and treat the Depth Ruler in
F5 as a first-class region rather than a widget bolted onto the camera controls.

Two rules should be promoted to tested invariants because they are cheap now and expensive
later. Every interactive element has a tooltip carrying name, hotkey and one-line meaning,
because an icon-only interface without tooltips is unusable. Every alert and bulletin carries
a jump target including its layer, because on a 40-layer map an event you cannot navigate to
is an event you cannot act on.

## Sources

- `docs/reference/screenshots/README.md` — the owner's two concept renders, described. The
  only primary source available for the *target* look, as opposed to the reference game.
- [Reference-game UI class structure, community modding guide](https://github.com/roxxploxx/RimWorldModGuide/wiki/SHORTTUTORIAL:-UserInterface)
  — corroborates the window stack, main tab windows, selection-derived inspect pane and
  command-grid structure, and confirms the immediate-mode drawing model.
- The linked wiki page itself, `https://rimworldwiki.com/wiki/User_interface`, **could not be
  retrieved**: blocked by this container's egress proxy.

## Confidence

**Medium.** High on structure and on the density techniques, which are stable and
independently corroborated by the concept renders. Low on any specific count, ordering or
label, because the primary source was unreachable. The recommendation does not depend on the
low-confidence parts.

## Could not be determined

- The exact set and ordering of main tab bar entries, and their hotkeys.
- The exact set and ordering of architect categories.
- The exact list of tracked resources in the ledger, and which are shown when zero.
- The precise alert severity tiers and their colours.
- Which overlay channels exist by default.
- Default keyboard bindings, and whether the reference game shows hotkey hints persistently
  or only on hover.

All six are cosmetic to the architecture and each is a ten-minute check for the owner, who
has the game. None blocks the design.

## Layer questions touched (brief §5)

**Question 9 — how does the camera slice, and how does the UI show what is above and below?**
This file answers the UI half and defers the camera half to `docs/design/06-rendering-and-camera.md`.
The UI answer is the Depth Ruler (F5) plus three supporting rules: every alert and bulletin
carries its layer and a jump target; every cell readout and tooltip states its layer; and the
colonist roster bar carries a per-colonist layer badge, because it is the only region that can
answer "who is where" at a glance.

Whether the storey above is hidden outright or rendered as a ghost outline is **not settled
here**. It remains a Lane B question, already assigned, and the concept renders do not
disambiguate it. `docs/design/09-ui-and-input.md` carries a provisional recommendation flagged
as pending that research, and the mockup at `docs/reference/mockups/hud-v1.html` exposes it as
a live toggle so the owner can judge it by eye.

*Addendum, 2026-09-15.* The owner did exactly that, and the answer is x-ray by default, with six
modes and two further axes shipped for playtest: `docs/adr/0006-layer-visibility-policy.md`. The
paragraph above stands unedited as what was known when this file was written. Lane B now challenges
a decided default rather than choosing from scratch.

**Question 6 — are zones, stockpiles and growing areas per layer or 3D volumes?** Touched only
indirectly: the overlay rows in F2 assume per-layer painting with only the active slice drawn,
because that is what the rendering budget allows. The gameplay semantics stay with Lane A.
