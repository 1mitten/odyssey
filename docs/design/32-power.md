# 32 — Power: the first net

*A wood-fired generator, the lines that carry what it makes, and a heater to spend it on.*
Interview 2026-09-23; research `docs/research/a-07-power-and-networks.md`. Pulled forward from
M7 (`03-systems-catalogue.md` §7) exactly as temperature was pulled forward from M4, and for the
same reason: the first consumer has something real to do on the day it lands, because the rooms
it heats are already simulated (design 28).

Every number here is **invented** unless a-07 is cited beside it, and every one of them is a
keyboard number — tuned by playing, recorded here when it moves.

## 1. The shape in one paragraph

A **line** (the registry calls it a *conduit*) is a thing in its own per-cell layer: it can run
through a wall, under a floor, under a bed or a door, and up a shaft, and it does not take the
cell's one edifice slot. Lines that touch face to face — the four sideways neighbours and the
cell directly above and below — are one **net**. A powered building **attaches** to a net when a
line runs under or beside any of its cells. A net is **live** when what its running generators can
make covers what its switched-on consumers want, and **dark** otherwise: then every consumer on it
stops at once. A generator burns wood **in proportion to the load it is carrying**, so an idle one
burns nothing, and haulers top its hopper up when it falls below half. Lines are **hidden** except
while the player is doing something about power, and then they are drawn **through** walls and
floors on every layer the net touches.

## 2. The owner's decisions (2026-09-23, fixed)

| # | Decision | Where it lives |
|---|---|---|
| 1 | The first consumer is an **electric heater**. The **cooker** is the next unit, with cooking. | §7, §12 |
| 2 | Lines go **anywhere, under anything** — through walls, under floors, beds, doors. | §3 |
| 3 | A line directly **above or below** another joins it. No riser piece. | §4 |
| 4 | A building joins a net if a line runs **under or beside** any of its cells. | §4 |
| 5 | Lines are **visible** when a power tool, deconstruct or cancel is armed, when a powered building is selected, or when the Menu's power overlay is on. | §9 |
| 6 | Visible lines are drawn **through** walls and floors, coloured by state, on every layer the net touches. | §9 |
| 7 | A net short of power goes **dark, whole**. | §5 |
| 8 | A generator burns wood **in proportion to load**. | §6 |
| 9 | **Haulers top it up** below half; an alert when one runs dry. | §6, §10 |
| 10 | A line costs **one wood** a cell. | §3 |
| 11 | Generators and heaters have an **on/off switch**. No battery, thermostat or line switch yet. | §5 |

## 2a. Defaults taken without asking (say if wrong)

- **Buildings do not carry power.** A net is exactly a connected set of lines, so the drawn lines
  *are* the net and the flood fill walks only lines. Two generators standing side by side are two
  unconnected things until a line joins them — and a line can sit under a building, so that costs
  one wood. The reference lets its power buildings pass power along (a-07 §1); rejected because
  a net whose extent depends on which furniture happens to touch is a net the overlay cannot show.
- **Lines are taken up with their own tool** — *Remove conduit*, in the Power category — and the
  Deconstruct tool never touches them. Deconstruct takes one thing per cell (the building, then our
  floor: `DesignationGrid.TryTakeApart`), so folding lines into it would make rerouting a wire
  under a floor cost the floor. The consequence, which is wanted: pulling down a wall leaves the
  wire that ran through it.
- **An ordered line is always drawn**, like every other standing order; only built lines hide.
- **The generator gives off a little heat while it is burning** — up to 400 at full load, against
  the heater's 1,000 and the campfire's 1,200 (design 28 §7). A generator in the living room is a choice with a cost.

## 3. The line layer

`Sim/Power/PowerGrid.cs` owns lines, and nothing else writes them.

- **Membership** is a bitset over the whole board plus a sorted list of line cells. The bitset
  answers "is there a line here" in one read; the list makes every walk deterministic and bounded
  by the lines rather than by the board. A bit in `CellGrid.Flags` was rejected: it would put
  wiring into the cell hash and the grid save and blur what the flags mean. A byte per cell was
  rejected as 2.5 MB for a layer that is almost empty.
- **Where a line may go** — `PowerGrid.AllowsLine`, the one owner: inside the board, not solid
  terrain, not water, not rubble. A wall, a door, furniture, a floor and open air (a shaft) are all
  fine. A click on solid ground names the air cell standing on it, exactly as a wall's does
  (`ConstructionGrid.StandingOn`), and `ConstructionGrid.Allows`, `WhereItWouldLand` and
  `RunLayerFor` all delegate here for a line, so the cursor and the order cannot disagree (P4).
- **The order** is `PlaceBuilding` with `BuildingHandle.Conduit`, the intent every other build
  uses; `ConstructionGrid.Place` hands a line to the power grid rather than taking a site of its
  own. A line order has **its own site lane** — a cell can hold a wall order and a line order at
  once — carrying only banked work, because there is nothing to deliver: the laying colonist
  carries the one wood and spends it as the line goes in.
- **Cancel** (`CancelBuilding`) takes every order in the cell: the building site, the line site
  and a line removal mark.
- **Removal** is a mark on a built line (`IntentKind.RemoveConduit`), carried out by a colonist,
  refunding the reference's half with the seeded coin flip (`DeconstructJobDriver.Refund`), which
  for a cost of one is nothing or one.
- **Nothing else can take a line out.** Mining cannot reach a cell a line may be in (lines are
  never in solid terrain), a collapse takes floors and trees, and a line needs no support.
- **Who lays it, and from where.** One job (`LayConduit`) fetches the one wood, carries it, works
  the line in at the Construction rate and puts the rest of the stack down: a separate delivery
  would be a second walk to put one plank beside the cell the same colonist then walks back to. A
  line is worked **like a slab** (`BuildWorkGiver.StandToBuild`): from beside it, or from the
  storey below. So a riser climbs as far as there is somewhere to stand beside it — up a wall
  whose storeys have floors, up a shaft with a landing at each level — and **a line ordered two
  storeys into open air with nothing to stand on is accepted and never laid.** That is the same
  answer a slab in mid-air gets, recorded here rather than guarded, because every real riser has
  floors beside it.

## 4. Nets and attachment

- **Solved lazily and only when dirty**: a line laid or taken up, a power building raised or
  demolished, or a load. The solve walks the sorted line list in ascending order and floods over
  the six face neighbours; the first cell of each component is its minimum, so the **net key is
  the minimum cell index** with no extra work — the room key's rule (`EnclosureGrid`).
- **Attachment** is asked of each power building at the solve: of every line in or beside (six
  faces) any of its cells, the one with the lowest net key wins. A building touching two nets joins
  one and never bridges them.
- **Cost**: nothing on a clean tick; a dirty solve is linear in lines. Measured in §11.

## 5. The balance

For each net: **supply** is the output of every generator attached to it that is switched on and
has fuel; **demand** is the draw of every consumer attached to it that is switched on.

| State | When | Consumers | Generators |
|---|---|---|---|
| **Live** | supply ≥ demand, supply > 0 | powered | burn for their share of demand |
| **Dark** | demand > supply | all unpowered | burn nothing — no load is delivered |
| **Idle** | nothing running and nothing wanted | — | — |

A consumer attached to no net is **unconnected**. The balance is a pure function of saved and
hashed state (lines, devices, switches, fuel), so the powered flags are derived, never saved and
never hashed, and a loaded world recomputes them before the thermal pass reads them. Whole-net
failure is the owner's choice over the reference's shedding (a-07 §2): one rule, deterministic,
readable from the overlay.

**Switching** is `IntentKind.SetPowerSwitch` on either cell of the building, applied while
paused and **at once** — the reference sends a colonist to flick it (a-07 §3); a job for a switch
buys nothing here but a wait. An off generator makes and burns nothing; an off heater wants and warms nothing. A new
building starts switched on, with an empty hopper.

## 6. The generator and its wood

| | Value | Why |
|---|---|---|
| Output | 1,000 W | a-07's generator; five heaters' worth |
| Footprint | 2 cells, rotates | the most the footprint supports; the Synty power unit is 3 × 2 and does not fit |
| Hopper | 75 wood | a-07; and exactly one stack of wood, so one trip fills it |
| Burn at full load | 22 wood a day | a-07, where it burns this whatever the load |
| Refuel below | half, up to full | a-07 §3 records generators running dry while pawns wait; an explicit threshold and target is its fix |
| Build | 30 wood or stone, 600 ticks | the first expensive building |

**Burn per pass** (every 120 ticks, 500 passes a day), in integer milli-wood, with the remainder
carried: `acc += 22,000 × share; burn = acc / (output × 500); acc -= burn × output × 500`, where
`share` is the generator's part of its net's demand — demand × output / supply, the odd watt to
the lowest record. At full load that is 44 milli-wood a pass; carrying one heater, 7.7. Its heat
is proportional too — `400 × share / output` — or a generator carrying nothing would warm the room
while burning nothing (a-07's recommendation). An empty hopper
stops the generator, and its net goes dark if the rest cannot cover the load.

**Refuelling** is a Hauling job (`Refuel`): below half, a colonist fetches wood, carries it and
fills the hopper to the top in whole wood. Not offered to a generator marked for deconstruction or
unreachable, and a generator is reserved while one colonist is feeding it.

## 7. The heater

175 W, one cell, blocking, 10 wood or stone and 240 ticks; **1,000 centi-degree-cells a pass into
its room while powered and switched on**, nothing otherwise. It is the campfire's shape
(design 28 §7) behind a gate: `TemperatureSystem` asks the power grid for the heat of each power
building instead of reading the per-def table for it. No thermostat yet (§12).

## 8. Save, hash, goldens

- One new section, **`odyssey.power`**, appended last. It carries its own version, the lines, the
  line sites, the removal marks and the device records (switch, fuel, burn remainder), keyed by
  edifice index. **No save-format bump**: an old save simply has no section and loads with no
  lines and every building at its defaults.
- An empty power grid adds nothing to the state hash, so a colony that has built nothing electric
  hashes exactly as it did.
- Three new jobs (lay, remove, refuel) add their counters to the job system's hash, so **the
  goldens move once, in the jobs commit**, with the colony census as the proof nothing else did.

## 9. Showing the lines

`Hud/PowerLinesVisibility` decides, as a pure function of the armed tool, the selection and the
overlay switch. `Presentation/Rendering/PowerLinePass` draws: one instanced batch per colour,
node boxes at each line and a stretched box along each link (east, north and up, so each link is
drawn once), with a material that ignores depth so a line inside a wall or under a slab still
reads. Colours: live, dark, idle, ordered, marked for removal. Matrices are rebuilt only when the
power state or the slice changes; hiding is not submitting, and the world's meshes are never
touched. The sim publishes each line's six-way links, so presentation never works adjacency out.

- **The shader is our own** (`Odyssey/PowerLine`: unlit, depth-test Always, Overlay queue).
  URP's Unlit carries its depth test as fixed state on some versions and a property on others,
  and a material setting a property its shader does not read is a line that silently goes back
  behind the walls. It is on `ShaderInclusion.Required`, so the player build keeps it.
- **Two tiers**: lines on the active layer at full strength, lines on every other layer at 38%.
  Drawn through everything at one strength is a tangle nobody can read the depth of.
- **The watch.** Built lines are published only while presentation watches them (process §3).
  The bootstrap sends `WatchPower` when the visibility answer changes and never otherwise; a
  paused world answers at once. Orders and removal marks are published and drawn whatever the
  visibility says.
- **The cursor.** A line has no module to ghost, so the hover and the drag draw the cell's plate
  in the build accent, red where refused.

## 10. Interface

- **Palette, Power**: Conduit (dragged in a line, never widened into a box), Remove conduit,
  Generator, Heater.
- **Pane**: a power building says what it is doing — switched off, not connected (with what to
  do about it), the net is short, out of fuel, carrying 175 W of 1,000 W — its hopper in whole
  wood, its net's balance, and a **switch row** the shell turns a press on into
  `SetPowerSwitch`, set above the rows' early return exactly as the bed's owner row is. A line in
  the clicked cell is named only while the lines are drawn. One owner writes watts
  (`Hud/PowerLabels`).
- **Alerts**: *Power failure* (`ui.alert.powerloss`) while any net is dark with demand;
  *Out of fuel* (`ui.alert.nofuel`) while a switched-on generator is empty and its net wants power.
- **Overlay**: the Menu's *Power* row goes live.

## 11. Measurements

**One line edit, at the scale target** (250 × 250 × 40), `PowerCostProbe`, the Windows dev
machine, 2026-09-23 — a serpentine of lines climbing several layers, the middle line taken up and
put back fifty times, a full solve after each:

| Lines | One edit's solve | A clean ask |
|---|---|---|
| 500 | 0.021 ms | 0.002 us |
| 2,000 | 0.078 ms | 0.002 us |
| 10,000 | 0.43 ms | 0.002 us |

**The first cut measured seven times that** — 0.12, 0.56 and 3.1 ms — because it flooded outward
with a binary search per face. At 10,000 lines that is the audit's `NavGraph.Rebuild` fault told
again (a global rebuild on one local edit, 1.19 ms). The solve is a union-find over the sorted
line list now, each face found from its lower side by a pointer that only moves forward, so it is
linear in lines with no search at all. A colony at rest pays nothing: the solve is lazy and the
burn pass is linear in power buildings, every 120 ticks.

**Drawing the lines** (§9), `FrameTimeTests.ThePowerLinesCostWhatTheySubmit`, one world timed
both ways in one run, alone on the machine, 2026-09-24, 640 × 480 on the RTX 5070 Ti: **2,000
lines cost 0.02 ms shown against hidden** (2.13 → 2.15 ms a frame) in **9 draw calls** — the pass
submits by colour and tier, never by line. `FrameSection.Overlays` moves 0.006 → 0.046 ms; nothing
else moves. Hidden costs nothing because hiding is not submitting (§9).

## 12. Seams left open

- **The cooker** is the next unit, with cooking: a consumer whose draw gates a work giver rather
  than a heat term.
- **Batteries**: a net's balance gains a store term; *Dark* becomes *draining*.
- **Thermostat**: a heater's draw gated on its room's temperature against a target.
- **A line switch** (`ui.arch.tool.switch`): a line that stops transmitting when off.
- **Short circuits and flares**: incidents against nets (design 23).
- **Utility taps**: the ruined city's live connection points become a generator with no fuel.

## 13. What not to undo by tidying

- **Lines are not edifices.** Moving them into the edifice slot would make "a line through a
  wall" impossible, which is decision 2.
- **Buildings do not transmit** (§2a). Letting them would make the net wider than the overlay.
- **The balance is derived.** Saving the powered flags would put a second copy of the net in the
  file, one the next solve could contradict.
- **Deconstruct does not take lines** (§2a).

## 14. Scrap metal, and selecting a line (second interview, 2026-09-23)

The first playtest found two things: an ordered line could not be clicked again, so there was no
way back to it to cancel it; and a line made of wood read wrong. The owner's answers:

| # | Decision |
|---|---|
| 12 | Lines are built from **scrap metal** — the existing *Scrap* item (`Item_Salvage`), relabelled. |
| 13 | More scrap metal comes from **wreckage scattered over the board** and from **supply drops**. |
| 14 | A line costs **1** scrap metal; the generator **30 wood or stone + 20**; the heater **10 + 5**. |
| 15 | Selecting an ordered line opens the **pane with a Cancel** — and a laid line, while shown, a **Remove**. |

- **One material becomes two.** A building's `costCount` is paid in the material the player
  chose; `partItem` and `partCount` are a second payment in one fixed item, whatever the building
  is made of. A site banks the two separately, the delivery job carries whichever is outstanding
  (the chosen material first), a site is a frame only when both are in, and a botch, a cancel and
  a deconstruct give back each by its own rule. The part count is saved in a section of its own
  (`odyssey.construction.parts`), so there is still no save-format bump. A line is all part:
  `costCount` 0, one scrap metal.
- **Scrap metal stacks to 50**, so a hauler carries a run's worth; it used to lie one piece to a
  cell. The starting kit still scatters its pieces one to an empty cell, so no golden moves.
- **Wreckage** is spawned with the colony, through the scenario the game loads (Playtest) rather
  than the bare one the goldens and tests stand on — the same place every other starting item
  comes from. Piles land on the topmost walkable cell of random columns at least 15 cells from the
  start: 7 per 10,000 columns (10 on the played 120 × 120 board), 10 to 25 scrap metal each. Every
  number here is a keyboard number.
- **The scrap drop** is a second incident on the supply drop's own worker: 15 to 30 scrap metal,
  on the debug menu's Events tab beside the meals.
- **Selecting**: line orders, and laid lines while they are shown, are pointer targets exactly as a
  building site is (`WorldRenderModel`'s site set, the picker's "a waiting order is a thing"). The
  pane titles the cell *Conduit* and carries a pickable row — *Cancel*, *Remove conduit*, or for
  a line already marked, *Keep it* — which is the switch row's mechanism reused.
- **Every order's pane has its Cancel, in red** (owner, 2026-09-23: *"make the cancel button red …
  the same for any building blueprint that has been put down"*). A building site's pane, which had
  no rows at all, carries one: *Cancel*, in the cancel tool's own red (`OrderColours`, the one owner
  of an order's hue), sending `CancelBuilding` with `A` = 1 — the building order alone, never a line
  ordered through the same cell. A line's Cancel is the same red; *Remove conduit* wears the remove
  tool's amber, because taking a laid line up is not a cancel.
- **The Cancel is a button, not red text** (owner, the same day: *"a red theme with white text"*):
  filled #a8352b with white ink — 6.6:1, where the cancel tool's own #e06a5c would be 3.2 and fail
  small text — and the tool's red kept for its border. Remove conduit is filled amber with dark ink.
  The stylesheet owns both (`inspect__row--danger`, `--warn`); the shell no longer writes the row's
  tint inline over them.
- **The views strip**: under the orders in the right-hand gutter, the orders strip's own box and
  buttons, each a switch that stays where it is put (`HudViews`, `HudRegion.ViewsStrip`). Power is
  the first: on, the lines show whatever is armed — to deconstruct around a wired room, or just to
  look; off, they are the tools' again, shown while power work is in hand. The Menu's overlay row is
  the same switch. The rail's squeeze gives up the strip's room as it does the orders'.
- **Art** (the owner's picks): the generator is Battle Royale's `SM_Prop_Generator_01`, the heater
  Sci-Fi City's `SM_Prop_AirConditioningUnit_01`, fitted into their footprints at bake time
  (`ModuleEntry.fitFootprint` / `fitHeight`; §14c replaced the first fit) and drawn once from the head at the middle of the footprint (`PropShape`, shared by the
  mesher and the cursor). Without the packs they are the tinted block per cell, as before. The rows
  live in `PlayScene`, so the committed catalogue gains them when *Odyssey → Presentation → Rebuild
  module catalogue* is run.

### 14c. Flush in their footprint (third look, 2026-09-23)

The owner: *"the generator and heater don't rotate or blueprint/place flush with the current
walls/doors etc — it's a bit off and places with spacing that is awkward."* Measured off the two
FBX files rather than guessed:

| | As modelled | First fit (uniform) | Now |
|---|---|---|---|
| Generator | 0.61 × 0.58 × 0.91 m | 2.1 × 2.0 × 3.1 m, centred: **0.95 m of daylight at each end** of its 5 m | **2.4 × 2.1 × 4.9 m** — fills both cells to 5 cm of every edge |
| Heater | 0.94 × 0.70 × 0.64 m, pivot on its back | turned side-on, 1.4 m deep, **centred in the cell**; could not rotate | **2.4 m across, back 5 cm off the back edge**, rotates |

- **The generator is stretched to fill** (`fitStretch`): 1.4 times longer than its own proportion.
  A uniform fit cannot fill a 2 : 1 footprint with a 1.5 : 1 engine without being 3 m wide; a
  machine that stands off both ends of its own footprint reads as a placing mistake, and a slightly
  long engine block does not. Its facing is where its second cell lies, so it stays the player's.
- **The heater stands with its back on the back edge** (`fitAgainstBack`) and **rotates**. Its
  facing is drawing only, so it is decided in presentation: `WorldRenderModel.BackedFacing` keeps
  the player's facing when a wall is behind it, and otherwise takes R's next quarter turn that backs
  on to one — so in a corner R chooses the wall and in the open R chooses freely. This parts company
  with the ladder's rule, where the wall wins outright and R does nothing: that would have kept
  *"doesn't rotate"* true against every wall. Doors do not count as a wall (`OccludesFace`).
- **No quarter turn is guessed from a model's proportions any more.** The first fit turned any
  model wider than deep, which put the air-conditioner's grille side-on to its wall. Which way a
  prop's front looks is the row's `yaw`; both of these face +Z as modelled.
- The mesher and the cursor both ask `PropShape` and `BackedFacing`, so the ghost stands where the
  built thing will. `PropFitTests` pins both fits from the models' measured sizes (so it holds
  without the packs) and the three facing cases.
- **Not done:** the heater's facing is not re-drawn when a wall is built beside it in a
  *neighbouring chunk* — the ladder's facing has the same limit and nobody has met it.
