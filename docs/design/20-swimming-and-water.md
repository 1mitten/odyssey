# 20 — Swimming, wading and water

**Status:** designed 2026-09-17. **The drawn float and the swim pose are built** (§5); the
simulation half — deep water becoming passable, the helpless-swimmer rules — is **not**. The
owner's decisions in §2 and §2a are settled; nobody has pressed Play on the float.

> **Since 2026-09-25 a one-cell stream is jumped, not swum** (design 44, owner: *"swimming isn't
> necessary most of the time"*). A person on one bank of a stream one cell wide jumps to the other
> at walking pace; wider water is still waded and floated exactly as below, and a jump that falls
> short lands in the water and is floated out. Deep water stays impassable — decision 2 below is
> still unbuilt. Two lines here are stale and corrected in place rather than rewritten: §3 lists two
> traverse modes where there are five (`NavGrid.TraverseMode`), and §6's "no animals swimming …
> until there is an animal" is now `TraverseModes.Swims`, which keeps both animals out of water.

## 2a. The float came back for a second round (owner, 2026-09-17)

After the depth experiment, the owner played again: *"I saw someone walk under water again when it
was 1 deep? — it's meant to float when this shallow if possible."* Two things follow, and they
change §4.1 and the shape of the work.

1. **Do not lower the water.** The wade option is off; `WaterSurface` stays at 0.72. Colonists float
   in shallow water as well as deep.
2. **In shallow water the float is a drawing and nothing else** — *"float is how it looks; shallow
   stays crossable"*. A colonist in a stream carries what it was carrying, works where it was
   working, and pays the third speed the cost class has always charged. Haulers cross streams. The
   helpless rules in §4.3 belong to **deep** water only.

That makes the shallow-water half **pure presentation**: no Def changes, no cost changes, no golden
moves, nothing in the save or the hash. It is built — `WaterLine`, `SwimPose`,
`PawnFigureDirector.ApplySwimPose` — and §5 records what it does.

**Read this before touching** `Terrain.xml`'s two water rows, `NavGrid.EnterCost`,
`TraverseMode`, `PawnPose.Of`, or anything in `PawnFigureDirector` that poses a walking figure.

---

## 1. What is wrong today

The owner, 2026-09-17: *"When walking under/through water. Is it possible to make the player float
on top of the water but is still vulnerable, would it be possible to have some kind of
paddling/swimming animation"*.

They are not walking *through* water. They are walking along the bottom of it, submerged.

- `Terrain.xml` gives both water rows `solid=false`, so the cell is not a floor and the **bed
  below it** is what a colonist stands on. A colonist in a shallow-water cell is therefore standing
  at that cell's floor.
- `ChunkMesher.WaterSurface` is `0.72`, and a cell is 3.0 m tall, so the drawn surface is
  **2.16 m above the bed**.
- A colonist is roughly 1.8 m. So the whole figure is under the drawn surface, walking normally,
  with no pose, no cost and no acknowledgement of any kind.
- Deep water carries `impassable=true`, which is the only reason a colonist does not do this in
  the middle of a lake.
- There is no swim state anywhere. `NavGrid.EnterCost` adds nothing for water beyond doors and
  hazards, and no water cell carries `NavFlags.Hazard`.

**One thing this section got wrong on the first pass, corrected 2026-09-17 while building the
measurement tool.** Shallow water is *not* free to cross. `NaturalContent` gives it
`CostClassShallowWater` with `costByClass = 200`, so entering one costs **300 against a flat cell's
100 — exactly a third speed**, with the comment saying so. The cost model already has a place for
"water is slow" and already uses it; what it has no place for is what the colonist *looks like*
while being slowed. That makes §4.2 below a smaller change than it was written as: a swim price is
another cost class beside an existing one, not a new mechanism.

## 2. The owner's decisions (2026-09-17)

1. **Float at the surface, paddling** — not wade. Chosen over lowering the water so colonists could
   stay upright, and over a wade/swim split by depth.
2. **Deep water becomes passable, and only when forced.** Priced so high that a route round is
   almost always preferred; a swim happens when fleeing, when the far bank is otherwise
   unreachable, or on a player's forced order.
3. **Slow and helpless, no drowning.** A swimmer moves slowly, cannot work, cannot carry, and is an
   easy target. No health-system involvement; nothing kills them outright.
4. **A load is dropped on the bank.** A colonist will not swim while carrying; a haul that would
   cross deep water routes round instead, or fails.

## 3. The one structural decision: swimming is a traverse mode, not a state

Decision 4 and decision 2 are the same rule said from two ends, and the existing
`TraverseMode` enum already says it:

```
Colonist   = 0   floors, stairs, ladders, doors it may open
Hauler     = 1   as Colonist, but a bulky load forbids ladders
```

`Hauler` exists precisely because *what a colonist is carrying changes where it may go*. Deep
water is the second instance of that idea and wants no new machinery: **`Hauler` does not enter
deep water at all.** Then

- a haul path never crosses deep water, because the path was planned in `Hauler` mode — there is
  no "drop it on the bank" step to write, and no way for a load to end up in a lake by accident;
- `HaulJobDriver` needs no water code whatever;
- a colonist who is somehow at the water's edge holding something — a forced order, a flee — is
  handled by `JobDriver.DropCarried`, which already exists and already puts a load somewhere real.

**Do not implement this as a `Swimming` pawn state with a flag on the pawn.** A flag would have to
be kept in step with where the pawn actually is, and the cell is already the authority. Whether a
colonist is swimming is a question about the cell it occupies, asked wherever it is needed.

## 4. Simulation changes

### 4.1 Terrain

`DeepWater` loses `impassable`. It gains nothing else: what makes it dear is a cost class, not a
property of the terrain row, so that the number can be tuned without a content migration.

`ShallowWater` is unchanged. A colonist in shallow water is standing on the bed with 2.16 m of
water over it, which is over its head, so **shallow water is swum too**. That is not a fudge: the
board's shallow water is 2.16 m deep and a person does not wade 2.16 m. Either both depths are
swum, or the drawn surface comes down — and the owner chose the float, which settles it.

> **Open, and it is the owner's call. The experiment has been run** (2026-09-17,
> `Odyssey.EditorTools.WaterDepthCheck`, `Logs/water-depth-{72,50,30,15}-play.png`). A 1.8 m post
> stands on the bed in a stream with a second, dark post of the same height on the bank beside it,
> photographed at four surface heights. **The 48-degree `-play` shot is the one to read**; the two
> lower framings are mostly bank, because a stream is cut into a channel and a low camera looks
> through the ground before it looks at the water.
>
> | `WaterSurface` | depth over the bed | of a 1.8 m colonist | |
> |---|---|---|---|
> | **0.72** (ships) | 2.16 m | 120% | over their head |
> | 0.50 | 1.50 m | 83% | chest deep |
> | **0.30** (proposed) | 0.90 m | 50% | waist deep |
> | 0.15 | 0.45 m | 25% | knee deep |
>
> At 0.72 only the top of the post shows, and only because the water is translucent. At 0.30 a
> clear banded section stands above the surface. **If the owner takes 0.30, shallow water is waded
> upright and only deep water needs the swim pose**, which is half the work in §5.2. Until they
> say, this document assumes the shipped 0.72 and swims both.

### 4.2 Cost

One new entry in `MoveCost`, beside the connector prices:

```
Swim = 900
```

Nine times a flat cell. The relations that matter, in the idiom `MoveCost.JumpUp` already uses:

- dearer than a built ladder (`LadderUp` 540), because a ladder is a thing somebody made to make
  this easier and a lake is not;
- dear enough that a detour of up to nine cells per cell of water is preferred, which on the played
  board means a colonist walks round every pond and stream on it rather than through;
- not `Fall` (100,000), which means "effectively forbidden" — this has to be a price, or decision 2's
  "when the far bank is otherwise unreachable" cannot happen.

**It is a guess and it is the one number here most likely to be wrong.** The experiment that settles
it is a colony on the wooded meadow with a stockpile across a stream: count how often anybody swims.

Priced through the cost-class byte, not through a special case in `EnterCost`, so that the
pathfinder's inner loop stays a scan over two flat arrays — the tier-0 rule from
`d-04-pathfinding.md` that nothing in the pathfinder reads a building or a thing. **This is the
mechanism shallow water already uses** (see §1): `CostClassShallowWater` carries 200, making a
shallow cell 300 against a flat cell's 100. Deep water wants a class of its own beside it, which
is one entry in the same table.

### 4.3 Speed and helplessness

A swimmer retires the same `movePerTick` against a bill nine times larger, so **slowness is already
paid for by the cost** and needs no second mechanism. This is the same identity `MoveCost.JumpUp`
relies on: cost is duration.

"Cannot work" is a guard in the job system rather than in each driver — a colonist in water cannot
be given, and does not continue, a work toil. The cheapest correct form is that no work giver
targets a cell in water and no toil that swings progresses while the pawn is in one.

"Vulnerable" has nothing to consume it yet: there are no threats. It is recorded here as the
reason the mode exists and is **not** built until there is something to be vulnerable to. Building
a vulnerability nothing can exploit would be a rule nobody could observe, and this document would
be claiming a thing the game does not do.

### 4.4 The three seams that must agree

The hop's lesson, applied before it costs anything: a price the planner and the mover disagree
about fails silently, and `HopPriceHasOneOwnerTests` exists because all three seams once agreed by
coincidence. Swimming has the same shape.

- the cell search (`PathFinder`),
- the region graph (`NavGraph`) — deep water currently forms `RegionKind.Impassable` regions that
  carry no links; passable water must join the walkable flood or a path can never be found across
  it,
- the mover's price (`MovementSystem.StepCost`).

**One owner for the number, and a test in the shape of `HopPriceHasOneOwnerTests` that fails the
fast tier if any other file in `Odyssey.Sim` names `MoveCost.Swim`.** Write that test in the same
commit, not afterwards.

### 4.5 What moves

Every golden. Deep water joining the walkable flood changes the region graph on any board with a
lake on it, and the played board has streams and ponds. Expect `Generated` to move as well as
`Simulated` this time — unlike the pickup change, this one alters the world's own structure before
a tick runs. Check the grid hash by hand before concluding the generator changed, per `Golden`'s
own note.

`NavGraphStatisticsTests` will move too: it counts walkable regions as a percentage, and this
promotes a category.

## 5. Presentation

### 5.0 What is built (2026-09-17)

`WaterLine` owns where a figure sits, `SwimPose` owns the shape, and
`PawnFigureDirector.ApplySwimPose` applies it. Four things worth not undoing:

- **The draught is about a hip height, not a few centimetres.** It is measured to the figure's
  *root*, which is at its feet, and the pose tips the body about the **hips** — roughly 0.9 m above
  the root. The first value, 0.25 m, was reasoned from "a floating body sits just under the
  surface", which is true of the body and not of the root: it put the torso 0.65 m clear of the
  water and the colonist lay on the stream like a raft. 1.0 m sinks it to the waterline. Found by
  photographing it, not by thinking about it.
- **The rise and the pose blend across the step, together.** The float is 1.16 m; switched on at a
  cell boundary it is a colonist teleporting — twenty times the 81.9 mm midpoint snap that
  `WalkOnReliefTests` exists for. `WaterLine.Weight` blends the same way the rise does, so the
  figure tips over as it sinks in.
- **The gait's speed is faded out by the swim weight.** A swimmer *has* ground speed, and the mixer
  reads speed, so without this the figure strides along the surface. This is the climb's fault from
  the other side: there a vertical step had *no* ground speed and the colonist went up a shaft
  standing to attention. Ground speed is a poor proxy for what the legs are doing.
- **`Footing` is faded by the swim weight, not skipped.** Its correction is a 0.32 m band, so on
  open water it finds nothing to do and at the shore it finds the bank, which would drag the figure
  back under. The first version skipped the pass outright above a threshold, so the footing arrived
  **complete on one frame** as a colonist came ashore — hips dropping and both feet planting between
  one frame and the next, every single time. Multiplying the correction by `1 - SwimWeight` makes
  the hand-over continuous and still costs nothing at full weight, because the existing early return
  skips a correction of zero.

### 5.0a The crossing curve — the height change happens on the water side

Added 2026-09-17 after the owner played the float: *"getting out — the colonist ends up clipped and
sunk half way into a terrain tile — is it possible the lift out of the water happens earlier or
reaches the edge and pulls up"*.

**What was wrong.** The float was added on top of the ordinary ground clamp, and the two disagreed
about when the step happens. Leaving a channel, the float decayed evenly across the step while
`PawnPose.OnTheDrawnGround` jumped to the arriving cell at the midpoint — so the figure spent the
first half of the step *below* the bank, buried in it, and the second half *above* it, having
overshot by the float it had not yet lost.

**The rule now.** A step with water at either end is drawn by interpolating its two resting heights
(`WaterLine.CrossingHeight`), because **between a waterline and the bank above it there is no drawn
surface to follow**. And the whole vertical change happens in the *water* half of the step
(`VerticalProgress`):

- **climbing out:** risen by the time it crosses the edge, so it steps onto the bank at bank height;
- **getting in:** walks to the edge at bank height, and only then goes down.

The pose weight uses the same curve, so a figure is never still prone once its height has reached
the bank, nor standing while still at the waterline.

**Measured on a real generated board** (`ShorelineWalkTests`, 64 × 64 wooded, seed 1): across **all
341 places a colonist can climb out**, the deepest it is ever inside the ground of the cell it is
climbing into, while over that cell, is **0 cm**. The rise is a median of 1.87 m and at worst
3.53 m.

**The pull-up is deliberately fast and its rate is not free.** Finishing by the midpoint is what
keeps the figure out of the block, so the rate follows from the geometry: the median exit is
55 mm a frame and the worst on the board is 106 mm. That is brisk — it is a pull-up — and the one
way to slow it is to finish later, which puts the figure back inside the bank. If it ever needs
softening, the lever is the step's own duration in the simulation, not this curve.

**The shoreline jitter was a separate report and the obvious explanation is wrong.** The owner also
saw snapping "as the colonist walks along the shoreline it just came out of". The drawn height is
**not** it: profiled across every one of 223 steps along a real shore, the worst step is a perfectly
even ramp — 31.45 m to 31.61 m in identical 0.031 m samples — with a hand-over gap to the next step
of **0.0 mm**. Thirty-one millimetres a frame is simply walking up a slope, and the 25 mm
"one frame of walking" yardstick is horizontal travel, so a slope beats it honestly. The remaining
candidate is the footing hand-over above, now continuous. **Nobody has confirmed that by playing
it.**

### 5.1 Where the figure is drawn

One change, in one place: `PawnPose.Of` raises a figure in a water cell to the surface rather than
leaving it at the cell floor. The rise is `CellMetrics.SizeY * ChunkMesher.WaterSurface` less
however much of a swimmer is under the waterline — a person swimming is mostly submerged, with the
head, shoulders and upper back clear.

This obeys the standing rule that **nothing in presentation is in a cell, a save or the hash**: the
pawn is in the water cell for picking, for the cursor and for every rule, exactly as a figure
leaning on a slope is still in its cell.

### 5.2 The pose

Computed, not animated — no pack contains a swim clip, the same reason the axe, pick and hammer
strokes are computed. It belongs beside `WorkSwing` and `ClimbPose` as arithmetic that a test can
check rather than a screenshot:

- the body pitched towards horizontal, floating, not upright;
- an alternating arm cycle, in the `WorkStroke` idiom — a phase from 0 to 1 driven by a clock that
  runs for as long as the state holds, which is exactly what a stroke is and what a `Gesture` is
  not;
- legs trailing with a small kick, out of phase with the arms;
- the gait blend must be told not to play a walk cycle: `GaitBlend` reads ground speed, and a
  swimmer has ground speed, so without this a colonist would run along the surface.

`Footing` must be suppressed entirely. A swimmer has no feet on the ground, and the hip drop and
ankle levelling would be solving against a bed two metres below.

### 5.3 What it costs to look at

Nothing measured yet. The pose is arithmetic per swimming figure per frame, and the number of
swimming figures is bounded by how many colonists are in water, which decision 2 makes rare by
construction.

## 6. What this does not do

- **No drowning**, by decision 3. Time in water accumulates nothing.
- **No bridges.** Both water rows already carry `bridgeable=true`, written where the build pipeline
  will find it, and nothing constructs one. Swimming does not change that; a bridge remains the
  intended answer to "I want to cross this every day".
- **No animals swimming.** `TraverseMode.Animal` is left as it is until there is an animal.
- **No current, no flow, no being carried downstream.**
- **Marsh is untouched.** It is solid ground that happens to be slow and is walked, not swum. (It
  still reads as a sandy bank, which is a separate open item.)

## 7. Test procedure, by hand

Nobody can judge a swim from a contact sheet. When this is built:

1. New game on the wooded meadow. Find a stream.
2. Order a colonist to the far bank by forced order. **Watch whether they swim or walk round.**
   Walking round is the correct answer for a narrow stream and the whole point of §4.2's price.
3. Order one into the middle of a pond. Watch the entry, the paddle and the exit — the exit is the
   moment most likely to look wrong, because it is where the float has to become a walk.
4. Give a hauler a job whose only straight route crosses deep water. It must route round or fail,
   and it must never enter the water holding anything.
5. Set a stockpile on the far side of a pond and leave the colony to run. Count swims. **Zero is a
   good result**, not a broken one.

## 8. Order of work

1. `WaterSurface` experiment (§4.1) — one number, and it may halve everything below.
2. Terrain, cost, mode, region graph, and the one-owner test (§4.2, §4.4). Sim only, fast tier.
3. Golden re-bake, with the `Generated`/`Simulated` check written down (§4.5).
4. `PawnPose` rise (§5.1). Unity tier.
5. The pose (§5.2). Unity tier, and a PlayMode shot in `Logs/` like the palette work, because it is
   the only thing anybody will actually look at.
