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

## 10. Interface

- **Palette, Power**: Conduit (dragged in a line, never widened into a box), Remove conduit,
  Generator, Heater.
- **Pane**: a power building says its net's supply and demand, whether it is powered or burning,
  its fuel, and offers *Switch on* / *Switch off*. One owner writes watts (`Hud/PowerLabels`).
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

Not yet measured: the drawing of the lines (§9), which is the next number owed.

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
