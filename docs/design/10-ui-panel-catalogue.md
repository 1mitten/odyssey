# 10 — Interface catalogue: every region and panel

**Status:** design, written out of phase order alongside `docs/design/09-ui-and-input.md`,
which holds the architecture this file's entries are expressed in. Read that first.

**Scope decision.** The owner asked for every region specified now, not just those the M1 to M3
vertical slice needs. This file therefore covers all of them. The `Milestone` field on each
entry says when it is built; several are M7 or M8 and exist here so that nothing is silently
dropped, mirroring how brief §5 treats simulation systems.

**Clean room.** Every name here is ours. Nothing is copied from the reference game's text,
labels or flavour. The taxonomy behind it is `docs/research/g-01-ui-information-design.md`.

---

## Screen map

```
+--------------------------------------------------------------------------+
| A1 ledger            A2 roster bar                    A3 clock   A4 speed |
|                                                                 +------+  |
| +--+                                                            |  A5  |  |
| |A7|                                                            |alerts|  |
| |  |                                                            +------+  |
| |  |                        world                               | A6   |  |
| +--+                                                            |bull. |  |
|                                                                 +------+  |
|                                                                   +--+    |
|                                                                   |A11   |
|                                                                   |depth |
| +------------------+                                              |ruler |
| | A9  inspect pane |                                              +--+    |
| | A10 command grid |                                                      |
| +------------------+      A8 main tab bar          A12 overlays   A14 X   |
+--------------------------------------------------------------------------+
```

Panels in Part B open as floating windows over the world. Nothing is ever a full-screen
takeover during play, per `g-01` finding F1 and the owner's concept composite.

**Resolving the concept render's duplication.** `docs/reference/screenshots/README.md` recorded
that the concept shows a colonist bar at the top *and* colonist cards at the bottom, that one
is redundant, and that the freed slot should go to layer navigation. Settled here: **the top
roster bar survives, the bottom cards are dropped, and the Depth Ruler takes the right edge
below the alert stack.** Bottom-centre goes to the main tab bar, bottom-left to the inspect
pane.

---

## Field definitions used by every entry

- **Slot** — where it lives on screen.
- **Reads** — which published view fields it consumes. The interface reads nothing else.
- **Emits** — which intents it can produce. The interface writes nothing else.
- **Cadence** — its frequency bucket: per frame, 15 Hz, 4 Hz, 1 Hz, or on-event.
- **Owner** — the director from `09` §3 that owns its state.
- **Layer** — whether it must be layer-aware.
- **Icons** — the symbolic icon keys it needs. Art does not exist; keys do.
- **Milestone** — when it is built.

---

# Part A — HUD regions

## A1 Resource ledger

| | |
|---|---|
| Purpose | Current stock of every tracked commodity, at a glance, without a click |
| Slot | Top-left, vertical stack |
| Contents | One row per non-zero commodity: icon, quantity, and a trend arrow when the 24-hour delta crosses a threshold. Grouped by kind with a thin rule between groups. Collapsible to a single summary row |
| Reads | `Globals.Ledger[]` — about forty counters |
| Emits | `OpenPanel(Bills)` on clicking a manufactured good; nothing else |
| Cadence | 4 Hz. Quantities do not need to animate |
| Owner | bound directly from `ViewCache`; no dedicated director |
| Layer | no — stock is colony-wide |
| Icons | `ui.res.*` — one per commodity, about forty |
| Milestone | M1 |

**Ruined-city adaptation.** Salvage classes replace ore classes, so the icon set is scrap
tiers rather than metal ores. The count of rows is unchanged.

**Open question.** Whether zero-stock rows hide or grey out. Recommendation: hide, with a
pinned-rows option, because a forty-row ledger of mostly zeroes defeats the density argument.

## A2 Colonist roster bar

| | |
|---|---|
| Purpose | Every colonist at a glance: identity, mood, health, current activity, and **which layer they are on** |
| Slot | Top-centre, horizontal, wrapping to a second row beyond about twenty-five |
| Contents | One card each: composed flat avatar, a mood ring, a health arc, up to three status glyphs, and a **layer badge**. Click selects; double-click selects and jumps the camera; drag-select a range |
| Reads | `Roster[]` — mood, health, four need summaries, eight status bits, job id, world ref |
| Emits | `Select(ThingRef)`, `CameraJumpTo(WorldRef)`, `SetSliceLayer(z)` |
| Cadence | 15 Hz |
| Owner | `SelectionDirector` for selection state, bound from `ViewCache` for display |
| Layer | **yes.** This is the only region that can answer "who is on which layer" at a glance |
| Icons | `ui.status.*` (about twelve), `ui.need.*` (eight), `ui.layer.badge` |
| Milestone | M2 |

**Composed flat avatars, not live portraits.** Fifty render-textured portraits is fifty texture
bindings and fifty render passes, which breaks three budgets at once. See `09` §4.5. Avatars
are built from icon layers — silhouette, apparel tint, status glyphs — in the shared atlas.

**Layer impact.** The layer badge is not decoration. On a forty-layer map the roster bar is the
player's primary answer to "where is everyone", and clicking the badge jumps the slice.

## A3 Date, season and weather readout

| | |
|---|---|
| Purpose | In-game clock, date, season, and current outdoor conditions |
| Slot | Top-right, above the speed controls |
| Contents | Time of day, day and season, a weather icon, outdoor temperature. Hovering expands to a forecast strip |
| Reads | `Globals.Clock`, `Globals.Weather`, `Globals.OutdoorTemperature` |
| Emits | nothing |
| Cadence | 1 Hz for the clock, on-event for weather |
| Owner | `TimeControlDirector` |
| Layer | **partly.** The temperature shown is the *outdoor* value and must be labelled as such, because on a layered map the player is frequently not standing in it |
| Icons | `ui.weather.*` (about ten), `ui.season.*` (four) |
| Milestone | M1 |

**Layer impact.** Once M4 lands, hovering shows both the outdoor value and the value at the
current slice, because on a roofed street level or a dug tunnel they differ sharply.

## A4 Play-speed controls

| | |
|---|---|
| Purpose | Pause and the three speeds |
| Slot | Top-right, under the clock |
| Contents | Four mutually exclusive icon buttons. The paused state also dims a thin border around the whole screen, so pause is unmissable |
| Reads | `Globals.Speed` |
| Emits | `SetGameSpeed(speed)` |
| Cadence | on-event |
| Owner | `TimeControlDirector` |
| Layer | no |
| Icons | `ui.speed.pause`, `ui.speed.1`, `ui.speed.2`, `ui.speed.3` |
| Milestone | M0, as the first visible interface element |

**Critical behaviour.** Intents flush while paused. Designations, priority changes and every
panel action work with the clock stopped. See `09` §2.4. Auto-pause policies (on threat, on
colonist down, on bulletin) are user settings and land with M6.

## A5 Alert stack

| | |
|---|---|
| Purpose | Continuously re-evaluated bad conditions, worst at the top |
| Slot | Right edge, upper |
| Contents | One row per active alert: severity glyph, icon, short text, **layer badge**. Click jumps the camera and the slice to the cause. Collapses to a count above eight |
| Reads | `AlertInputs` — the condition fields the simulation publishes |
| Emits | `CameraJumpTo(WorldRef)`, `SetSliceLayer(z)`, `Select(ThingRef)` |
| Cadence | 1 Hz, staggered across conditions so no frame evaluates all of them |
| Owner | `AlertDirector` |
| Layer | **yes, mandatorily** |
| Icons | `ui.alert.*` — one per condition, plus three severity glyphs |
| Milestone | M2 |

**The layer rule.** Every alert carries its layer and a jump target. An alert with no layer is
a dead end on a forty-layer map, and this is the concrete requirement
`docs/reference/screenshots/README.md` asked for.

**Accessibility.** Severity is encoded three ways: colour, glyph shape, and stack position.
Never colour alone. This costs nothing now and is expensive to retrofit.

## A6 Bulletin stack

| | |
|---|---|
| Purpose | Discrete narrative events, each dismissible |
| Slot | Right edge, below the alerts |
| Contents | One card per undismissed bulletin: icon, title, dismiss control. Click opens the full text and a jump control |
| Reads | the event ring, drained once per frame |
| Emits | `CameraJumpTo(WorldRef)`, `SetSliceLayer(z)`, `DismissBulletin(id)` |
| Cadence | on-event |
| Owner | `BulletinDirector` |
| Layer | **yes** — every bulletin carries a jump target including its layer |
| Icons | `ui.bulletin.*` — about fifteen categories |
| Milestone | M2 |

**Why separate from alerts.** An alert is a condition with a lifetime that can clear itself. A
bulletin is an event, immutable once raised. Merging them produces a system wrong for both.

## A7 Architect toolbar and palette

| | |
|---|---|
| Purpose | Everything the player can place, order or paint |
| Slot | Left edge, expanding rightwards into a palette |
| Contents | Two levels. A column of category icons; selecting one opens a palette of tools within it. Categories: orders, zones, structure, production, furniture, power, security, salvage, floors, recreation |
| Reads | `ToolCategoryDef[]`, `ToolDef[]`, and per-cell validity from `PlacementValidator` |
| Emits | `Designate(toolId, cellSpan)`, `CancelTool()` |
| Cadence | per frame while a drag is active, otherwise on-event |
| Owner | `ToolDirector`, with `PlacementValidator` and `GhostRenderer` |
| Layer | **yes.** Placement validity is a per-cell, per-layer question with support and roof rules |
| Icons | `ui.arch.category.*` (ten), `ui.arch.tool.*` (about eighty by M8) |
| Milestone | M3 |

**Drag shapes:** single cell, line, rectangle outline, filled rectangle, freeform paint, and
flood-fill within a room. Each is a strategy in `ToolDirector` with its own headless test.

**Disabled with a reason, never hidden.** A cell that refuses a placement shows *why* — no
support, roof above, occupied, out of materials — sourced from the validator's per-cell reason
code, not guessed by the interface. This is `g-01` density technique 3.

**Ruined-city adaptation.** *Salvage* and *deconstruct* are distinct tools with distinct yields,
and *reclaim* — adopting an existing ruined structure rather than building new — is a verb the
reference game has no equivalent for. It is a tool in the structure category.

## A8 Main tab bar

| | |
|---|---|
| Purpose | Access to the roster-wide management views |
| Slot | Bottom-centre |
| Contents | One icon button per view, mutually exclusive, each hotkeyed. Entries: work, schedule, assign, animals, wildlife, research, quests, factions, world, menu |
| Reads | `TabDef[]` |
| Emits | `OpenPanel(key)`, `ClosePanel(key)` |
| Cadence | on-event |
| Owner | `TabDirector` |
| Layer | no |
| Icons | `ui.tab.*` — ten |
| Milestone | M1 for the bar, individual tabs as their panels land |

Tabs whose systems do not exist yet are present and disabled with a reason, so the shape of the
game is visible from M1. This is the catalogue's guarantee made visible.

## A9 Inspect pane

| | |
|---|---|
| Purpose | Full detail of the current selection |
| Slot | Bottom-left, fixed frame |
| Contents | Header with name and kind; a body that varies by selection class; the command grid (A10) below. Tabbed when the subject is complex, as colonists are |
| Reads | `SelectionDetail[]`, bounded at thirty-two subjects |
| Emits | varies by class; all through the command grid |
| Cadence | 15 Hz |
| Owner | `InspectDirector` |
| Layer | **yes.** A cell selection states its layer; a thing's pane shows what is directly above and below it |
| Icons | `ui.inspect.tab.*` (seven) |
| Milestone | M1 for cells, M2 for colonists, M3 for buildings |

**Tombstone behaviour.** If the subject is destroyed while the pane is open, the pane stays
open with values greyed, adds a reason line, and disables every command. It does not close
under the cursor. Full specification in `09` §2.3.

**Multi-select.** With a homogeneous selection the pane shows the common fields and a count.
With a heterogeneous selection it shows only the count and the intersected command set. Command
intersection across a heterogeneous selection is M7; same-class multi-select is enough for the
vertical slice.

## A10 Command grid

| | |
|---|---|
| Purpose | Every legal action for the current selection |
| Slot | Inside the inspect pane, below the body |
| Contents | A grid of square icon buttons. Disabled commands stay visible and greyed with a reason on hover. Hotkey shown on hover |
| Reads | `CommandDef[]` filtered by selection class and state |
| Emits | one intent per command, from the command's intent template |
| Cadence | on selection change, plus 4 Hz to refresh enabled state |
| Owner | `GizmoRegistry`, composed by `InspectDirector` |
| Layer | **yes.** A command's legality can depend on the layer above; roofing is the obvious case |
| Icons | `ui.command.*` — about sixty by M8 |
| Milestone | M3 |

## A11 Depth Ruler and slice control

**This is the region with no counterpart in the reference game, and the answer to brief layer
question 9.** The owner's concept renders omit it entirely; `docs/reference/screenshots/README.md`
recorded that omission as the gap the interface must close.

| | |
|---|---|
| Purpose | Which layer am I on, what is on the others, and how do I get there |
| Slot | Right edge, below the bulletin stack, vertical |
| Contents | A vertical scale of the world's layers. The current slice is highlighted. Each layer shows an **occupancy pip** whose weight reflects how much is built or occupied there, a **colonist count**, and **markers** where alerts or bulletins are pending. Ground level is marked. Click any layer to jump. Above-and-below display policy toggles live here |
| Reads | `LayerSummary[]` — one compact row per layer: occupancy, colonist count, pending alert count |
| Emits | `SetSliceLayer(z)`, `SetAboveBelowPolicy(policy)` |
| Cadence | 1 Hz for pips, on-event for the current slice |
| Owner | `SliceDirector` |
| Layer | by definition |
| Icons | `ui.layer.up`, `ui.layer.down`, `ui.layer.ground`, `ui.layer.policy.*` (three) |
| Milestone | M1 |

**Bindings**, which the reference game has no need for and are therefore specified fresh:

| Action | Binding |
|---|---|
| Slice up one layer | `Page Up`, or the layer modifier with scroll up |
| Slice down one layer | `Page Down`, or the layer modifier with scroll down |
| Jump to ground level | `Home` |
| Follow selection's layer | `F` toggles |
| Cycle above-and-below policy | `Ctrl` with the layer key |

Scroll with the layer modifier changes the slice **regardless of what is under the cursor**,
which is case 8 of the input router's eight enumerated tests in `09` §6.

**Above-and-below policy**, the one genuinely unresolved design question. Three options:
hide everything above; ghost structural outlines only; full x-ray. **Provisional
recommendation: hide solid geometry above, ghost structural outlines only, and dim the layer
below where it shows through holes.** Flagged pending Lane B, which already owns the prior-art
question and where two comparable games take opposite positions. The mockup at
`docs/reference/mockups/hud-v1.html` exposes all three as a live toggle so the owner can judge
by eye rather than from prose.

## A12 Overlay menu and legend

| | |
|---|---|
| Purpose | Which world-data channels are painted onto the world, and what the colours mean |
| Slot | Bottom-right, left of the cancel affordance |
| Contents | A row of toggle icons, one per channel, with a legend strip appearing above when a channel is active. Channels: zones, stockpiles, growing, roof, power, temperature, beauty, light, salvage density, designations |
| Reads | `OverlayDef[]`, and the cell field windows in the published frame |
| Emits | `SetOverlayChannel(id, on)` |
| Cadence | on-event for toggles; the overlay itself redraws on dirty chunks only |
| Owner | `OverlayDirector`, rendered by `OverlayRenderer` |
| Layer | **yes.** Only the active slice is drawn |
| Icons | `ui.overlay.*` — ten |
| Milestone | M1 with one channel, more per milestone |

**The menu is UI; the overlay is not.** One layer is 62,500 cells and an element per cell is
wrong by three orders of magnitude. Overlays are textures and instanced quads. See `09` §4.6.
This distinction is enforced by a test.

## A13 Tooltips

| | |
|---|---|
| Purpose | The name, hotkey and one-line meaning of whatever is hovered |
| Slot | follows the cursor, flipping to stay on screen |
| Contents | Title, hotkey hint, one line of description. Expanded detail on a modifier key |
| Reads | registered providers, writing into a reused buffer |
| Emits | nothing |
| Cadence | on hover, after a delay |
| Owner | `TooltipDirector` |
| Layer | **sometimes** — a cell tooltip must state its layer |
| Icons | none of its own |
| Milestone | M1 |

**Mandatory, not optional.** An icon-only interface without tooltips is unusable. A test asserts
every command Def carries a tooltip text key, and the build fails without one.

## A14 Cancel affordance

| | |
|---|---|
| Purpose | Escape from the current mode |
| Slot | Bottom-right corner |
| Contents | One large button, mirrored on the escape key, which unwinds in order: cancel the active tool, then close the top panel, then open the game menu |
| Reads | `ToolDirector` state, `PanelDirector` stack |
| Emits | `CancelTool()`, `ClosePanel(top)` |
| Cadence | on-event |
| Owner | `InputRouter` |
| Layer | no |
| Icons | `ui.cancel` |
| Milestone | M1 |

Present in the owner's concept render as a large red X, noted there as prominent enough to
double as "clear designation". Adopted.

## A15 Developer overlay

| | |
|---|---|
| Purpose | Frame time, tick rate, allocation counters, draw calls, per-director budget usage |
| Slot | Top-left under the ledger, toggleable, absent from shipping builds |
| Contents | The `UiBudgetMonitor` readout, plus the four icon debug modes from `09` §7 |
| Reads | profiler counters and the budget monitor |
| Emits | debug-mode toggles |
| Cadence | 4 Hz |
| Owner | `UiBudgetMonitor` |
| Layer | no |
| Icons | none; this is the one region permitted to use text labels |
| Milestone | **M0**, and it is the first visible interface in the project |

This is also the one place immediate-mode drawing is permitted, because none of its constraints
apply to a non-shipping diagnostic.

---

# Part B — Panels

All are floating windows over a live world. None is a full-screen takeover.

## B1 Colonist detail

| | |
|---|---|
| Purpose | Everything about one colonist |
| Contents | Seven tabs: **Needs** (bars with current value and decay rate), **Thoughts** (active memories with expiry and mood contribution), **Skills** (level, passion, learning rate), **Social** (opinions, relationships), **Gear** (equipment, inventory, apparel condition), **Health** (see B1a), **Log** (recent jobs and events) |
| Reads | `ColonistDetail` — subscription-scoped, filled only while open |
| Emits | `SetOutfit`, `DropItem`, `Prioritise`, `SetMedicalCare` |
| Cadence | 15 Hz for needs, 1 Hz for the rest |
| Owner | `PanelDirector`, composed by `InspectDirector` |
| Layer | no, except the header's layer badge |
| Icons | `ui.need.*`, `ui.skill.*` (about twelve), `ui.passion.*` (three) |
| Milestone | M2 for needs, skills and gear; social M6; log M6 |

**Subscription is the performance story.** A closed colonist panel costs the simulation nothing.
Opening one registers a subscription and the simulation begins filling that view. See `09` §2.2.

### B1a Health sub-tab

Body-part hierarchy as a tree: each part with its condition, injuries, bleeding rate, and the
capacities it contributes to. Derived capacities listed separately with their contributing
parts. Tended and infected states shown as glyphs.

This is the densest tree in the game and is the main justification for a virtualising tree
control. Icons: `ui.health.*`, about twenty. Milestone M6.

## B2 Work priority grid

| | |
|---|---|
| Purpose | Assign work-type priorities across the whole colony |
| Contents | A grid of colonists by work type, about 50 × 25. Each cell is a priority from one to four or disabled. Column headers are work-type icons with a tooltip; rows are colonist avatars. Click cycles, drag paints, shift-click sets a column |
| Reads | `WorkGrid` — subscription-scoped, 50 × 25 bytes |
| Emits | `SetWorkPriority(pawn, workType, priority)` |
| Cadence | on-event; the grid does not animate |
| Owner | `PanelDirector` |
| Layer | no |
| Icons | `ui.work.*` — about twenty-five |
| Milestone | M7 |

**The stress case.** 1,250 cells is where the virtualisation budget gets cashed, and it is the
scenario in experiment R1 that decides the framework flip condition. Realised rows must stay
under forty regardless of colony size.

## B3 Schedule

Per-colonist daily schedule: a 24-column grid of colonists by hour, each cell one of sleep,
work, recreation, or anything. Drag paints across hours and colonists. Reads `ScheduleGrid`,
emits `SetScheduleBlock`. Icons `ui.schedule.*`, four. Milestone M7.

## B4 Assign

Per-colonist assignment of outfits, food restrictions, drug policy, medical care and areas.
One row per colonist, one column per assignable policy, each a dropdown. Includes the **area
allowance** control, which on a layered map must express areas as per-layer regions. Reads
`AssignGrid`, emits `SetAssignment`. Icons `ui.assign.*`, six. Milestone M7.

**Layer impact.** An allowed area on a forty-layer map is a volume, not a shape. The control
paints per layer and shows a layer selector, which is the one place the Depth Ruler's policy
and a panel interact.

## B5 Animals

Tamed animals: name, species, training progress per skill, bonded colonist, area, and
slaughter or release commands. Reads `AnimalRoster`, emits `SetTrainingTarget`,
`SetAnimalArea`, `DesignateSlaughter`. Icons `ui.animal.*`, about ten. Milestone M5.

## B6 Wildlife

Untamed creatures currently on the map: species, count, manhunter state, predator flag, and
designate-for-hunting. Reads `WildlifeSummary`, emits `DesignateHunt`. Shares B5's icons.
Milestone M5.

**Ruined-city adaptation.** City scavengers and feral synths rather than grazing herds, so the
predator and manhunter columns carry more weight than the herd columns.

## B7 Research tree

| | |
|---|---|
| Purpose | Choose the current research project and see the tree |
| Contents | A node graph with tabs per branch. Nodes show cost, prerequisites and unlocks. Current project highlighted with progress. **Windowed, not full-screen** |
| Reads | `ResearchState` — subscription-scoped |
| Emits | `SetResearchProject(id)` |
| Cadence | 1 Hz |
| Owner | `PanelDirector` |
| Layer | no |
| Icons | `ui.research.*` — one per branch, about eight |
| Milestone | M7 |

The owner's concept composite shows this windowed with about a dozen visible nodes, and notes
it is legible *because* it is windowed. That is the design, adopted as specified.

## B8 Quests

Available and active quests: title, offer expiry, rewards, requirements, accept or decline.
Reads `QuestList`, emits `AcceptQuest`, `DeclineQuest`. Icons `ui.quest.*`, four. Milestone M7.

## B9 Factions

Known factions: name, kind, goodwill value and trend, current relations state, and any
available diplomatic action. Reads `FactionList`, emits `SendGift`, `DeclareHostility`. Icons
`ui.faction.*`, about six. Milestone M7.

## B10 Trade and caravan ledger

| | |
|---|---|
| Purpose | Exchange goods with a trader or load a caravan |
| Contents | A two-column split ledger: our inventory against theirs. Each row carries item, quantity, unit value. A central transfer control per row and a running balance at the foot |
| Reads | `TradeSession` — subscription-scoped |
| Emits | `AdjustTradeRow(item, delta)`, `ConfirmTrade`, `CancelTrade` |
| Cadence | on-event |
| Owner | `PanelDirector` |
| Layer | no |
| Icons | `ui.trade.*`, four, plus the resource icons |
| Milestone | M7 |

Matches the concept composite's split-ledger layout exactly, which is the standard solution and
worth no invention.

## B11 World map

The planetary view: settlements, factions, caravan routes and travel. Reads `WorldMap`, emits
`FormCaravan`, `SetCaravanDestination`. Icons `ui.world.*`, about eight. Milestone M7.

**Note.** This is the one view that legitimately takes the full screen, because it replaces the
local map rather than overlaying it. The no-takeover rule applies to panels over a live local
map, not to changing which map is shown.

## B12 Bills

Per-workbench production orders: recipe, target count or "do until", ingredient filters,
quality and skill restrictions, and a suspend toggle. Reads `BillStack` for the selected
workbench, emits `AddBill`, `ReorderBill`, `SetBillTarget`, `SuspendBill`. Icons `ui.bill.*`,
six. Milestone M5.

**First serious list stress.** An ingredient filter is a deep nested tree, and this panel is the
dry run for B2's density two milestones later.

## B13 Stockpile and zone settings

For a selected stockpile or zone: priority, the allowed-item filter tree, and per-cell extent
editing. Reads `ZoneSettings`, emits `SetZonePriority`, `SetZoneFilter`, `PaintZone`. Icons
`ui.zone.*`, five. Milestone M3.

**Layer impact, and an open question.** Is a stockpile a per-layer area or a volume spanning
layers? Recommendation: **per layer**, because hauling cost across layers is materially
different from hauling within one, and a volume hides that from the player. This is a gameplay
question that belongs to Lane A item 14 and is flagged, not settled, here.

## B14 Room readout

For a detected room: role, size, beauty, cleanliness, impressiveness, temperature, and the
thoughts it generates. Reads `RoomStats`, emits nothing. Icons `ui.room.*`, five. Milestone M4.

**Layer impact, and the second open question.** Do rooms span layers? A two-storey atrium is one
room to a player and two flood-fills to an algorithm. Flagged for Lane A item 5; the panel is
designed to display either, with a "spans N layers" line that renders empty when they do not.

## B15 Power net inspector

For a selected power network: generation, consumption, stored charge, connected devices, and a
highlight of the net on the world. Reads `PowerNet`, emits `ToggleDevice`. Icons `ui.power.*`,
six. Milestone M7.

**Layer impact.** Networks connect vertically, per brief §5 item 7. The inspector lists
connected devices grouped by layer, and the net overlay draws on every layer the net touches
rather than only the active slice — the single documented exception to the active-slice-only
overlay rule, justified because a power net the player cannot see the shape of is unusable.

## B16 Archive

Every bulletin ever raised, searchable and filterable by category and date. Reads
`BulletinArchive` — subscription-scoped and potentially ten thousand rows, so strictly
virtualised. Emits `CameraJumpTo`, `SetSliceLayer`. Icons: shares A6's. Milestone M2 for the
list, M7 for search and filtering.

**The virtualisation test case.** `Archive_With10kBulletins_RealisesUnder40Rows` is the assertion
that proves the list strategy across the whole interface.

## B17 Settings and keybindings

Graphics, audio, interface scale, accessibility modes, autosave, and a rebindable action list
with conflict detection. Reads and writes user settings, not simulation state, so it is the one
panel that bypasses the intent queue. Icons `ui.settings.*`, six. Milestone M1 for a stub,
M8 for the full pass.

**Accessibility modes exposed here**, all specified in `09`: text fallback for icons,
colour-blind-safe alert palette, interface scale from 80 to 150 per cent.

## B18 Game menu

Save, load, options, quit. Reached from the tab bar or by the escape key unwinding to the
bottom of the stack. Milestone M0.

---

## Cadence rollup

Every region assigned to a frequency bucket, which is how the 2.0 ms budget in `09` §4.2 is met.

| Bucket | Regions |
|---|---|
| Per frame | drag ghost, tooltip follow, camera |
| 15 Hz | roster bar, inspect pane, colonist needs |
| 4 Hz | resource ledger, command enabled-state, developer overlay |
| 1 Hz | clock, alert evaluation (staggered), Depth Ruler pips, research progress |
| On event | speed controls, tab bar, bulletins, overlay toggles, every panel body |

**The cadence is wall-clock, never tick-keyed.** At 3× speed the tick rate triples and the
interface's update rate must not. Only the clock readout tracks game time, and it reads it from
the published frame. Asserted by `Cadence_At3xSpeed_RunsSameNumberOfEvaluations`.

## Milestone rollup

| Milestone | Regions and panels |
|---|---|
| M0 | A4 speed, A15 developer overlay, B18 menu |
| M1 | A1 ledger, A3 clock, A7 bar frame, A8 tab bar, A9 cells, **A11 Depth Ruler**, A12 one channel, A13 tooltips, A14 cancel, B17 stub |
| M2 | A2 roster bar, A5 alerts, A6 bulletins, A9 colonists, B1 needs/skills/gear, B16 archive list |
| M3 | A7 full palette, A10 command grid, A9 buildings, B13 zones |
| M4 | A12 temperature, light, roof channels; B14 room readout |
| M5 | B5 animals, B6 wildlife, B12 bills |
| M6 | B1a health, B1 social and log, combat mode, threat alerts |
| M7 | B2 work, B3 schedule, B4 assign, B7 research, B8 quests, B9 factions, B10 trade, B11 world, B15 power, B16 search |
| M8 | accessibility pass, modding surface frozen, performance pass, second locale |

## Icon key inventory

The interface needs roughly **250 icon keys** by M8, and about **95** by the end of the M3
vertical slice. None exist as art. All are generated as deterministic placeholders per `09` §7,
so layout and density can be reviewed now and art can arrive at any time without a code change.

| Namespace | Count | First needed |
|---|---|---|
| `ui.res.*` | ~40 | M1 |
| `ui.arch.tool.*` | ~80 | M3 |
| `ui.command.*` | ~60 | M3 |
| `ui.work.*` | ~25 | M7 |
| `ui.health.*` | ~20 | M6 |
| `ui.alert.*` | ~20 | M2 |
| `ui.bulletin.*` | ~15 | M2 |
| `ui.status.*` | ~12 | M2 |
| `ui.skill.*` | ~12 | M2 |
| `ui.arch.category.*` | 10 | M3 |
| `ui.overlay.*` | 10 | M1 |
| `ui.tab.*` | 10 | M1 |
| `ui.weather.*` | ~10 | M1 |
| `ui.need.*` | 8 | M2 |
| everything else | ~40 | various |

## Open questions this catalogue raises rather than settles

1. **Above-and-below display policy** (A11). Provisional recommendation given; belongs to Lane B.
2. **Are stockpiles per layer or volumes** (B13). Recommendation given; belongs to Lane A item 14.
3. **Do rooms span layers** (B14). Panel designed to display either; belongs to Lane A item 5.
4. **Does the ledger hide or grey zero rows** (A1). Ten-second owner decision.
5. **Command intersection across heterogeneous multi-selection** (A9). Deferred to M7; the
   vertical slice needs only same-class multi-select.
6. **Whether the power net overlay's cross-layer exception generalises** (B15). If more channels
   need it, the active-slice-only rule needs restating rather than exempting case by case.
