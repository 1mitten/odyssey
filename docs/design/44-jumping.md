# 44 — Jumping a one-cell stream

**Status:** designed and built 2026-09-25, **not yet played, and the Unity tier has not been run**
(the session that built it had no Unity: the simulation half is proven in the fast and Long tiers,
the drawing half only by reading). Branch `claude/funny-allen-2qipcn`. The owner's decisions in §2
are settled; the price in §3 is derived, not chosen.

**Read this before touching** `NavGraph.IsJump`, `NavGraph.IsJumpAcross`, `NavGraph.JumpCost`,
`MoveCost.Jump`, `MovementSystem.StepCost`, `Pawn.JumpLanding`, `CombatSection` layout 4,
`JumpArc`, or anything in `PawnPose.Of` above the wading line.

## 1. What was wrong

Owner, 2026-09-24: *"A colonist/bandit assumes to swim across a stream. If the stream/river is 1 tile
(or 2 even) that the colonist will jump across and there is already synty animation. Where this is
preferred over swimming (as swimming isn't necessary most of them) and jumping should be
introduced."*

Measured from the code, not from the picture:

- A stream is a channel **one layer below** its banks (`NaturalWaterPasses.LowerChannels`). The
  water cell sits level with the bank's solid top; a colonist on the bank stands a layer above it.
- So crossing a one-cell stream is **two hops**: a drop in (`MoveCost.Drop` 50) and a jump out
  (`MoveCost.JumpUp` 240) — **290**, against 200 for two flat cells. The water's own +200 is never
  charged, because a hop charges its own price (`PathFinder.RelaxHop`).
- The float (design 20 §5) is drawn for every step with water at either end, so every one of those
  crossings reads as a swim.
- Streams generate **one or three cells wide** (`streamMaxHalfWidth = 1`); two occurs only where
  a meander's back-fill widens one locally. All stream water is shallow.
- No step anywhere in the pathfinder, the region graph or the mover spans two cells.

## 2. The owner's decisions (interview, 2026-09-24)

| Question | Answer |
|---|---|
| How wide | **One cell only.** 5 m bank centre to bank centre. A three-wide stream stays a wade. |
| Who | **People, with their load.** Colonists, haulers holding the armful, bandits. Animals unchanged — neither wades today, and a jump over water they cannot enter would be a second way across it. |
| What is jumped | **Water only.** The cell skipped must be open air over a wadeable channel. A trench, a missing slab or a hole to a cavern stays a hole. |
| Does wading stay | **Yes.** The jump is chosen by price; nothing becomes impassable, and a forced order into the stream still gets in. |
| How quick | **About walking pace.** §3. |
| Can it fail | **Yes; the harm is a later unit.** A deterministic roll; a failed jump lands short in the water, the float takes over, and the colonist hops out on the far side. No injury until the health model can carry one. |
| What makes it fail | **A flat base chance in the Defs**, worse when hungry or exhausted, worse when carrying. Not per kind. |
| How the player learns | **A splash.** No Events row, no registry key. |
| Geometry | **Straight across, same level.** Orthogonal only, both banks on one layer, exactly one water cell between. |
| Deep water | **Held.** Design 20's swim half stays unbuilt. |

## 3. The price: 200, and why it is not a number from the clips

`MoveCost.Jump = 200`, owned by `NavGraph.JumpCost`.

- **Below 290** — the two hops it replaces — so a colonist offered both jumps.
- **Not below 200.** The cell search's heuristic (`PathFinder.Octile`) estimates `Orthogonal` for
  every cell of distance, so a two-cell move priced under 200 would make the estimate an
  *over*-estimate on every route that uses one. A* stops being admissible across the whole board
  for the sake of a rare edge, and the failure is silent — a slightly worse path, never an error.
- **Exactly 200 is walking pace**, which is what the owner asked for, and it means no route ever
  prefers a stream to the grass beside it.

Cost is duration, so 200 is **3.33 s** at the standard walk, 1.67 s drafted. `JumpArc` spends it
the way the price was reasoned: the 1.25 m from the bank centre to the lip is walked, the 2.5 m
over the water is the flight, and the 1.25 m after the far lip is walked. The pack's walking
take-off and landing run 0.90 s together and sit inside the flight with a gather at the lip and a
settle on landing.

**Rejected:** deriving the number from the clips (about 154–180). It would have been faster than
walking, which is the one thing the owner said it should not be, and it breaks the heuristic.

## 4. The rule, and who owns it

`NavGraph.IsJumpAcross(from, to, mode)` is the whole of it, and the cell search, the region graph
and the per-tick legality check all ask it:

1. the step is two cells straight across on one layer (`NavGraph.IsJump`);
2. the mode is a person's (`JumpMask`, which is `AllMask` less the animals);
3. `from` can be stood in and `to` walked into;
4. both ends stand on **solid terrain** (`UpperEndIsABlockTop`) — bank to bank, never a built
   floor to a built floor;
5. the cell between is **open air** (`NavGrid.IsAir`), so a floor poured over the stream or
   anything built there takes the jump away;
6. the cell under it is **walkable shallow water**. Shallow only, and not by taste: a jump that
   fails has to land somewhere a person can stand, and deep water is impassable.

Every one of the owner's exclusions falls out of 4–6 without being named: a three-wide stream has
water, not a bank, at `to`; a trench has no water under the gap; a terrace bank a layer higher is
solid at `to`.

**The price has one owner too.** `HopPriceHasOneOwnerTests` now also fails on any file outside
`NavGrid.cs` and `NavGraph.cs` that names `MoveCost.Jump`. The mover used to take any same-layer
step at `EnterCost(to)`, so a two-cell step would have been charged as one flat cell — the exact
shape of the fault that test was written for, arriving by a new door.

## 5. The region graph

A jump is a link of its own kind, **`LinkKind.Jump`**, two-way, priced `JumpCost` both ways, mode
mask `JumpMask`. Not `Portal`, which is what the hop uses: `RecomputeLayerChangeEstimate` counts
Portal links as ways between layers and `RebuildPortalEdges` turns each into a per-cell edge the
mover prices a layer change by. A jump is neither, and would have lowered every layer-change
estimate on a board with streams.

**Ownership, and why the dirty radius did not grow.** A jump link is built by the interior zone of
the block holding `from`, in **+x and +z only**, reaching past the block edge if it has to — so
each pair is built exactly once. Every cell the rule reads is within one cell of the gap: `from`,
`to`, the gap, the water under it, the ground under both ends. `MarkDirty` dirties the edited
cell's block and its ±1 neighbours on the edited layer and the two above; `CollectAffectedZones`
then rebuilds the interior zones of every dirty block's eight neighbours. An edit to any cell the
rule reads therefore rebuilds the zone that owns the link. That is an argument, and the proof is
`PathingTests.IncrementalRebuildMatchesAFullRebuildOverRandomisedEdits` with a channel added to
its fixture.

Districts are per mode, so an animal's district never crosses a stream and
`PawnContext.Reachable` answers across one for a person for nothing.

## 6. The failure

**Rolled at take-off.** The first tick a jump is the step in hand, before any of its cost is paid,
`MovementSystem.CommitJump` rolls once and records where the jump will land in
`Pawn.JumpLanding` — the far bank, or the water short of it. Rolling at landing would move the
figure 2.5 m in one frame. **It is recorded on success too**, so a save taken mid-jump resumes the
same jump and never rolls again.

**Odds**, integer per mille, clamped to 1,000:

```
fail = jumpFailPerMille                                  30
if carrying: fail = fail * jumpFailCarryingPerMille / 1000    ×2.0
fail = fail * 1000 / ConditionPerMille                   ×1.0 well, ×1.43 at the floor
```

`ConditionPerMille` is the one scalar both rates already read: starvation and temperature offset
it and it floors at 700. Carrying is a job holding a carried item, or a colonist carrying somebody
to a bed. A well, unladen colonist fails 3 % of jumps; a starving hauler 8.5 %. The numbers are
invented and live in `Colonist.xml` beside the pace, where they are pinned by the content
fingerprint.

**Stream:** `PawnPurpose.Jump` (`0x2431_85BE`, SHA-256's eleventh round constant, next after
`Knockback`), rolled as `DeterministicRandom.ForTick(seed, tick, Jump ^ id)` — the melee roll's
discipline, so a lockstep twin rolls the same.

**Landing short** truncates the path in place to end in the water cell. The step from the bank
into the water is already a legal hop down, so the per-tick legality check passes, and it is
charged at the **jump's** price while `JumpLanding` is set — the colonist was in the air for the
same time either way. On landing the path is spent, the job driver asks for a new one from the
water, and the hop out costs what it always has.

**Saved** in `CombatSection` **layout 4**, beside `FinishingStepTo`, which is the section's other
"step the world cannot re-derive". No save format bump. **Hashed** while set, flagged in bit 26 of
the kind word (22 and 23 are promised to the power line; 24 and 25 are the response), so a colony
that never jumps hashes as it did before.

**Published** as `PawnView.JumpingShort` while the landing is a layer below. A bool beside
`Seated` rather than a `PawnFlags` bit, because that byte is full. The drawing needs it, because a
short jump's step has exactly the shape of a drop off the bank, and the audio listens for its end.

**The debug menu's Cheats tab has *Jumps always fail***, because a 3 % event is not something a
playtest can wait for. `IntentKind.DebugJumpsFail`, appended last so no recorded intent
renumbers, applied while paused, unsaved and unhashed. It is the one thing in this unit a player
could see named, so `ui.debug.jumpsfail` is in `icon-keys.csv` and the wiki moved by one row.

## 7. The drawing

`JumpArc` owns the shape, `PawnPose.Of` places it, nothing is simulated — the same split as
`HopArc`.

- **An early return, above the wading line.** A jump is drawn from its own curve, never through
  `OnTheDrawnGround`'s clamp: that clamp samples the bank under whichever cell the figure is over,
  and a figure over the gap is 2.5 m from either cell's centre.
- **Pace**: the approach and the departure are walked; the flight holds the clip's 0.9 s at its
  centre with a gather before and a settle after.
- **Height**: a parabola over the chord between the two lips, its apex from gravity for the
  flight's length (`g·t²/8`), so the arc cannot drift from the clip.
- **A short jump** takes off the same way and comes down on the water line at the water cell's
  centre, where `WaterLine` takes over — a hand-over gap of nought by construction.
- **Clips**: `A_Jump_Walking` and `A_Land_Walking` (Base Locomotion, Masc and Femn, in place),
  played in the combat action slot at speed nought with their time set by hand, the way a sword
  swing is timed to its tick. The jump takes the slot only when the fight has nothing to show. With
  no clip — the runner, or a clone without the packs — the gait is held through the flight, as it is
  through a hop.
- **Both clips live in one file**, `A_Jump_Walking_*.fbx`, so a clip row gained `fileName` and the
  catalogue build resolves the jump rows by file *and* exact clip name (`ResolveJumpClips`). The
  generic by-file lookup would have returned whichever clip came first. **The two rows were added
  to the committed `ModuleCatalogue.asset` by hand, with empty clip references**, so the runner has
  them; the owner's rebuild fills the references, and `JumpClipRowTests` fails until it does on a
  machine with the pack.
- **Footing** is faded out in the air. **The load** rides the palms, because the carry pose is
  applied after the clip.
- **The far form** is placed by `PawnPose.Of` and glides the same arc.
- **Splash**: `SoundIds.Splash`, named and in no catalogue yet — the file wants sourcing under ADR
  0010. Raised when a pawn that was `JumpingShort` last frame is not this frame and is standing in
  water.

## 8. What this does not do

- **No harm.** A failed jump costs a soaking and a few seconds. Injury waits for health.
- **No two-cell jump**, no diagonal jump, no jump between layers.
- **No animal jumps.** The rat and the hog have jump clips of their own, committed and unused.
- **No run-up.** Drafted colonists jump faster only because they move faster.
- **Deep water stays impassable.**

## 9. Tests and measurements

**Sim, fast tier** — `JumpTests` (the rule, the price, the region link, animals, and
`IncrementalRebuildMatchesAFullRebuildWithStreamsInIt`, the sibling of the randomised-edit rebuild
test with water in it — mutation-checked: never clearing the jump's dedupe table fails it) and
`JumpColonyTests` (a real colonist through the composition root: a clean jump that stays dry, a
short one that lands at the jump's price and climbs out, the lockstep twin, a save mid-jump for both
outcomes that never rolls again, an order given mid-jump, the published short jump, the odds).
`HopPriceHasOneOwnerTests` now guards `MoveCost.Jump`.

**Presentation, EditMode, not yet run** — `JumpArcTests` (the shares, gravity, the ends, walking
pace, the per-frame displacement budget, the hand-over to the water line, the clip order, and
`PawnPose.Of` drawing a jump from the arc) and `JumpClipRowTests` (the committed rows and, with the
pack, that each clip is the right in-place Humanoid clip of the length the arc assumes).

**The played board has plenty to jump** (`ThePlayedBoardHasStreamsNarrowEnoughToJump`, 120 × 120 ×
16, the played map):

| Seed | One-cell crossings | Wadeable cells |
|---|---|---|
| 1 | 86 | 596 |
| 2 | 73 | 658 |
| 3 | 89 | 651 |

**Goldens.** Only `PlayedBoard.Simulated` moved, as predicted: its `Generated` hash, the stream-free
`Meadow` and the `City`, and every soak run held. See §9a for what the probe said moved.

## 10. Test procedure, by hand

1. New game on the wooded meadow. Find a stream one cell wide.
2. Right-click a colonist to the far bank. **They should jump, not swim.**
3. Debug menu → Cheats → *Jumps always fail*. Do it again. **They should land in the water with a
   splash and climb out on the far side.**
4. Give a hauler a load whose route crosses the stream. **The load should stay in the hands.**
5. Find a stream three cells wide. **They should wade it, as before.**
