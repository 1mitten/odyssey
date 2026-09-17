# d-15 — Why water hangs in mid air, and what a water body should be made of

## Question

The owner's playtest of 2026-09-17 reported water "in mid air", strange gaps, and water that
"can't handle being at height". Four screenshots of a stream descending the terraced wooded board.
Is the fault in the generator or in the drawing, and what should a water body be made of so that a
stream crossing terraced ground reads as water?

## Findings

### 1. The generator is not at fault, and this was measured rather than argued

A throwaway probe generated the board the scene actually loads — `MakeWooded()`, 120 x 120 x 16 —
on three seeds and measured every water cell. It was deleted after the run; the numbers are here.

| | seed 1 | seed 2 | seed 3 |
|---|---|---|---|
| Water cells | 605 | 713 | 689 |
| **With air directly beneath** | **0** | **0** | **0** |
| With an open side at their own layer | 27 | 19 | 22 |
| Overhanging a column two or more layers lower | 27 | 19 | 22 |
| **Greatest drop between adjacent water columns** | **1** | **1** | **1** |
| Adjacent water pairs, flat : stepped one layer | 762 : 44 | 986 : 29 | 927 : 42 |

Not one water cell on any seed floats. Every one sits directly on solid ground, and the stream
never drops more than a single layer between neighbouring columns. Both of ADR 0009's invariants
hold exactly as it claims they do — its decision 4 asserts that "the surface being continuous with
nothing floating" passes unmodified, and it does.

So **the simulation data is right and the drawing is wrong**, which is what `WaterFillPass`'s own
comment predicted in as many words: *"Depth is a rendering problem, not a geometry problem."*

### 2. The root cause is one line: water is a lid, not a body

`WorldRenderModel.ResolveTerrain` (`WorldRenderModel.cs:511`) gives every non-solid terrain
`ModuleShape.FloorSlab` — "a slab at the cell's lower boundary". Water is non-solid, so water is a
floor slab: a flat tile with no thickness and no sides.

`WaterContributor.Emit` (`TerrainContributors.cs:132`) then takes that tile and raises it to
`ChunkMesher.WaterSurface` = **0.72** of the cell height before draping it. So what is drawn for a
water cell is a single horizontal lid floating **2.16 m above its own bed**, with 2.16 m of
nothing between the two and no side faces anywhere.

That single fact produces all three symptoms in the screenshots.

- **Water in mid air.** The void under the lid is open on every side. It is walled only where the
  neighbour happens to be a dry bank, which the generator puts exactly one layer above the bed —
  so the bank's solid cell fills the water's own layer and hides the void. Wherever the neighbour
  is *lower* instead, the void is in plain view and the lid reads as a slab hanging in the air over
  a drop. That is the 27 overhanging cells a board, and it is exactly the shot with the isolated
  blue quad floating over grass.
- **Water that does not fall.** At a one-layer step — 44 adjacent pairs on seed 1 — the upper lid
  and the lower lid are 3 m apart with a dry bed face between them and nothing drawn on it. A
  cascade is therefore a stack of disconnected shelves. That is the whole of "water should fall
  down".
- **The murky wedge.** Water does not write depth and is drawn after the opaques (ADR 0009,
  decision 8), and below the surface the slice draws every layer beneath it. So a terraced ravine
  presents several lids at once, each seen through the others and each with an open underside, and
  they blend into a translucent mass with diagonal seams.

### 3. What must not be touched

ADR 0009's geometry decisions are all still right and the fix must not reopen any of them.

- **The bed is the column surface and deep water is not cut deeper** (decision 4). Cutting a deep
  core would put neighbouring surface cells two layers apart and break the walkability invariant.
- **Both depths draw their surface at the same height** (decision 4). Shallow and deep sit side by
  side in one pond; a height difference would put a step in the middle of the water.
- **The valley relaxation** (decision 6) is what lets a stream cross terraced ground at all. The
  one-layer steps it produces are the correct shape of a watercourse on a terrace, not a defect to
  be flattened away.
- **Water is drawn by the ordinary chunk machinery**, one instanced draw per chunk (decision 8).
  Anything proposed here has to stay inside that, or water stops being a terrain and becomes a
  system.

## Recommendation

**Give water a skirt: one vertical face system with two triggers.** Ranked first, and it is a
single mechanism rather than two features, which is why it wins.

A water cell emits a vertical face on a side whenever the thing beside it is not water at its own
level, dropped to whichever is lower — the bed it stands on, or the surface of the water in the
next cell down. The two triggers are the same geometry for two reasons:

1. **Nothing beside it** (a lip, an overhang, the outer bend of a stream on a slope). The face
   closes the void, so the lid stops hanging in the air and becomes the top of a body of water.
2. **Water one layer below** (a cascade step). The face spans the 3 m between the two lids, and the
   water falls.

**This is already the house idiom, not a new one.** Ground has exactly this: `ModuleShape.GroundFace`
is "the same block where a side of it can be seen", a separate shape and a separate mesh precisely
because nearly all ground never shows a side and a mesh is what a bucket is keyed by. Water wants
the same split for the same reason, and `TerrainCell.ExposedSides()` — which exists, is lazy, and
is documented as being for the one contributor that wants it — is the neighbour scan it needs.

Three things to get right when it is built:

- **Emit a face only where the neighbour is not water.** Water is transparent and writes no depth,
  so interior faces buried inside the next water cell would double-blend and darken the middle of
  every stream. This is a stricter predicate than the existing "is the side open" and wants stating
  once.
- **The falls face wants the shader to know it is vertical.** `Odyssey/Water` is arithmetic authored
  for a horizontal surface — ripples in plan, a glint, a Fresnel that strengthens as the surface
  turns away. Applied unchanged to a 3 m vertical sheet it will read as a pane of glass. Whether
  that wants a flag on the material or a second shader variant is the one design question inside
  this recommendation.
- **Cost is negligible and should be stated anyway.** 27 lip cells and 44 step pairs a board, against
  605 water cells and the meadow's measured 34,961 instances. This is noise; it is the mesh count
  and the transparent overdraw that want watching, not the instances.

**The cheapest stop-gap, if the owner wants the worst of it gone today:** raise
`ChunkMesher.WaterSurface` from 0.72 to 1.0, putting the lid at the top of its cell and level with
the bank beside it. It hides most of the void from most angles for a one-number change. It is
**not** the answer — it contradicts the channel that decision 4 exists to create, a colonist no
longer steps down to wade, and it does nothing at all for the cascade — but it is reversible in a
second and it doubles as the experiment below.

**Rejected, with reasons.**

- **Flatten the stream beds in worldgen** so there are no steps. This fixes a drawing fault by
  changing the simulation, which the project's own facade rule forbids; it would break the valley
  relaxation that lets a stream cross terraced ground; and it would make the wooded board's
  terracing and its water mutually exclusive.
- **Leave the geometry and fake depth in the shader.** A screen-space depth fade is a good thing to
  have and is orthogonal to this, but the void under the lid is a hole in the geometry and no
  amount of shading fills a hole.
- **Cut a two-layer core for deep water** so the body has somewhere to be. Rejected by ADR 0009
  decision 4 on the walkability invariant, and still rejected.

## The cheapest experiment, and what it would settle

One claim here is not measured: that the large diagonal translucent wedge in the third and fourth
screenshots is the open underside seen through the lids, rather than a second and separate fault in
the transparent draw.

Set `WaterSurface` to 1.0 and re-shoot the same view. If the wedge goes, it was the open underside
and the skirt fixes it. If the wedge survives with the lid flush to the bank, there is a second
fault and it is in the transparent draw order, not in the missing faces — which would be worth
knowing before any of the above is built. One number, one screenshot.

## What building it found (2026-09-17, same day)

The recommendation was built. Three things it changed about the account above, recorded here rather
than edited away.

**The 27 and the 44 are the same cells, and this file had them as two classes.** Finding 1 read the
"27 with an open side" and the "44 stepped pairs" as a lip class and a cascade class. They are one
phenomenon counted twice: a water cell at the top of a step has an open side *because* the water
beside it is a layer down, and 44 exceeds 27 only because a cell in a notch steps two ways. Measured
by the mesher afterwards: it emits **exactly 44 faces on seed 1**, one per stepped pair, and **not
one of them is a lip**. The lip branch is right and is currently unreachable on the played board —
it fires the first time somebody mines beside water, which is precisely when it is wanted.

**The shader would have eaten the whole feature, and nothing would have said so.**
`OdysseyWater.shader` carried `clip(input.normalWS.y - 0.5)` — discard every fragment whose
geometric normal is not pointing up. It was there because the surface was the unit cube squashed to
a 0.15 m slab, so each cell had four side faces and an underside, and two coincident translucent
faces either side of a shared edge each added their own alpha and **ruled the board into dark
squares along every cell boundary**. The comment called one instruction cheaper than "adding a mesh
shape that nothing else would use". So the surface is now a **sheet** (`WaterMesh`), which has no
spurious sides to clip, the clip is gone, and the dark-square fault is answered by not building the
faces rather than by discarding them. A side effect worth knowing: the drawn surface drops 0.15 m,
because the old slab's *top* was what the player saw and the sheet sits exactly at `WaterSurface`.

**No test can see whether a face is drawn**, which is why `WaterCheck` exists. The face tests assert
matrices, and a quad wound backwards, a normal pointing the wrong way, or the clip left in each
leave every assertion green with the water exactly as broken as before. That is not hypothetical:
the first three contact sheets came back showing no change at all, and the falls were very nearly
written off as not drawing — the real cause was that the tool had framed a spot with **no cascade in
it**, because it scored candidate sites by how much water was nearby and so picked the middle of the
widest pool. It scores by nearby steps now.

## The wedge is the banks, and it is a separate fault (2026-09-17)

The one thing this file could not determine — whether the diagonal translucent wedge in the owner's
third and fourth screenshots was the open underside or a second cause — **is now answered, and it is
a second cause.** It is not water at all.

`Logs/water-lip.png` reproduces it: large diagonal green sheets standing proud of the meadow beside
the channel, reading exactly like something hanging in mid air. Setting `BankLayout.Enabled = false`
and shooting the identical frame (`Logs/water-nobanks.png`) makes every one of them vanish and
leaves clean terrace risers. So they are **bank ramps** — `BankMesh`, the walkable slope drawn in
the empty cell at the foot of a terrace step — and a channel cut through terraced ground grows one
at practically every step, which is why they cluster along a stream and read as part of the water
fault.

**Not fixed here, deliberately.** It is a different feature with its own design
(`BankLayout`/`BankMesh`), it was not what this question asked, and the owner should see the two
apart rather than have them fixed together. The comparison is now a standing pair of shots rather
than a one-off, so the next session can tell a bank fault from a water one in a single run.


## Sources

Read in this repository, not from the web:

- `Assets/Odyssey/Presentation/World/WorldRenderModel.cs:511` — non-solid terrain resolves to `ModuleShape.FloorSlab`.
- `Assets/Odyssey/Presentation/Rendering/TerrainContributors.cs:117–147` — `WaterContributor.Emit`.
- `Assets/Odyssey/Presentation/Rendering/ChunkMesher.cs:121–147` — `WaterSurface`, and the contributor order.
- `Assets/Odyssey/Presentation/Rendering/ModuleCatalogue.cs:12–60` — `ModuleShape`, including `FloorSlab` and the `GroundFace` precedent.
- `Assets/Odyssey/Sim/Worldgen/Natural/NaturalWaterPasses.cs` — `WaterPlanPass`, `Reconcile`, `Settle`, `WaterFillPass.WriteColumn`.
- `Assets/Odyssey/Defs/Core/World/Terrain.xml:192–213` — both water terrains, neither solid.
- `docs/adr/0009-water-depth-and-the-impassable-bit.md` — decisions 4, 6 and 8.
- Measurement: a throwaway probe over `MakeWooded()` at 120 x 120 x 16 on seeds 1, 2 and 3, run in
  the fast tier and deleted. The table in finding 1 is its output.

## Confidence

**High** that the generator is not at fault and that water is drawn as a raised lid with no sides:
both are measured, and the second is two lines of code read directly.

**High** that a skirt is the right shape of fix and that it belongs in the contributor rather than
in worldgen.

**Medium** on the falls face specifically: that the geometry is right is clear, but nobody has seen
`Odyssey/Water` applied to a vertical surface and it may want more than a flag.

## Could not be determined

- ~~Whether the diagonal wedge in screenshots three and four is wholly explained by the open
  underside.~~ **Answered 2026-09-17: it is not water, it is the bank ramps** — see the section
  above. Struck rather than deleted, because the wrong hypothesis is the useful part: the wedge was
  read as a water fault by everyone who looked at it, including this file.
- How the skirt should behave where water meets a **wall** a player has built rather than terrain —
  there is no such case on the board today, because water is `buildable false`, but bridges are
  named in ADR 0009 as a future and will create one.
- Whether the transparent overdraw of a skirted cascade costs anything measurable. The instance
  count plainly does not; overdraw on a stack of vertical transparent sheets viewed edge-on is the
  one number that might, and it wants `FrameTimeTests` rather than reasoning.
