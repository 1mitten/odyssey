# e-11 — Animating cooking and eating

## Question

How should Odyssey animate **butchering, cooking (at a stove and at a campfire), carrying a plated
meal, sitting on a chair, eating seated, eating standing, and standing up**, given how poses are
built today? Colonists are Synty humanoids on one rig across 61 characters; no pack we own has a
sit, eat or cook clip; work strokes are computed.

Lane: e (engine/art). Cap: 25 reads/greps, 4 web searches. Read-only in `D:\code\odyssey-cooking`
at `837c895a`.

## Findings

### 1. How a whole-body computed pose is built: `SleepPose`

- **Computed, not animated, on a stated rule** (`Assets/Odyssey/Presentation/World/SleepPose.cs:15-22`):
  `13-gestures.md` §3 says *author the angles* when the figure aims at something whose position the
  arithmetic does not know, and *solve to a point* when it must meet something it does. Lying down
  is both. The mattress is a known plane, so the body is **placed** on it. The limbs reach for
  nothing, so their angles are **authored**.
- **Every number is an angle or a fraction of the figure's own measured length**, never metres
  (`SleepPose.cs:31-33`). `Lift` is the half-height of a box of body thickness and width turned
  through the roll (`SleepPose.cs:109-115`). `ThicknessPerBody` 0.135 and `ShoulderPerBody` 0.109
  were **measured off the drawn cast** with `Odyssey.EditorTools.SleepProbe.Run`, not taken from
  human proportions (`SleepPose.cs:55-92`).
- **Four postures**, chosen by `PostureFor(pawnId)` (`SleepPose.cs:286-294`), so the choice holds
  every night and after a load at no cost in state. Settles over 0.45 s (`:118`).
- **P11** (`docs/bug-patterns.md:232-262`) has two faces:
  - A reading of the wrong thing that is still a plausible number. `StandingHipHeight` reads the
    avatar's `Hips`, which on this rig is a bone named `Root` on the floor, and a
    `Mathf.Max(0.2f, …)` clamp hides it.
  - An aggregate that answers a different question. The lowest vertex *anywhere* on a side sleeper
    is a drawn-up knee, so tuning `Lift` to it left torsos 9–12 cm above the bed. The fix measures
    **the trunk band** (`SleepPose.cs:70-92`, `docs/design/20-beds.md` §7b).

  **The lesson for a seat:** measure the pelvis band against the seat, not the minimum over the
  body. On a seated figure that minimum is the sole.

### 2. How a work stroke and a held tool are driven

- **`WorkSwing`** (`WorkSwing.cs:6-58`) is three pitch angles: `Shoulder`, `Elbow` and `Spine`.
  They are applied **about the figure's own right-hand axis, not a bone's local axis**, so the pose
  holds on any rig. They are laid **on top of the mixer's output** and never replace it. The
  shoulder is pitched against the world, with the spine subtracted, so the three numbers stay
  independent. The signs differ per bone and "only a photograph settles them".
- **`WorkStroke`** holds the timing: raise and strike poses, seconds, and where the arc turns
  (`WorkStyle.cs:24-35`). The presets are `Axe`, `Pick` and `Hammer` (`:73`, `:92`, `:127`), and
  skill scales the stroke clock (WS2).
- **`WorkStyle`** bundles the stroke, a `ToolModule` catalogue id, a `ChipRecipe`, `AimFromCentre`,
  tilt, grip, butt and slide fractions, and head `Dip` and `Raise` (`WorkStyle.cs:227-300`).
  **Choosing a style is a table in presentation**: `IndexForJob(jobDef)` maps `JobHandle.Mine` and
  `JobHandle.Build`, and everything else falls to felling (`WorkStyle.cs:504-510`). The file says a
  new kind of work — **it names butchering** — should cost "a new static here and a row in
  `ForJob`, and nothing else" (`WorkStyle.cs:233-235`). The Building style proved that claim
  (`:433-478`). The file also names the case that would break the table: **one job index needing
  two strokes** (`:500-502`).
- **The tool is a GameObject parented to the hand, gripped by measurement.** The haft is the long
  axis of the mesh bounds, and `HandGrip.Palm` measures the palm off the knuckles, because a
  humanoid hand bone is the wrist (`PawnFigureDirector.Tools.cs:13-24`, `:236-254`).
- **The off hand is put on the haft by `TwoBoneIk`, unconditionally.** A one-handed tool would be
  the first to need a `TwoHanded` flag (`WorkStyle.cs:456-458`; `13-gestures.md` §10).
- **Bones are looked up once through `animator.GetBoneTransform`**: spine, chest, neck, head, both
  arms and hands, hips, both legs and feet (`Tools.cs:265-286`). Every pose is then FK `Pitch`
  calls plus `TwoBoneIk.Reach` solves, written after the Playables graph evaluates.
- **What the simulation publishes for all of this:** `PawnView.JobDef`, `Working` and `WorkCell`,
  plus the stroke clock (`Views.cs:216-231`). No per-style field exists. The style is derived in
  presentation.

### 3. The carry path

- **Authored angles with a measured cradle** (`docs/design/24-carrying.md` §4a). Shoulder and elbow
  are authored so every rig holds the same *posture*. The palms are then read, and the load sits at
  their midpoint. Only `Clearance` is solved. §4a explains why a solved cradle was rejected: arm
  length varies across the 61 rigs by more than the cradle does.
- **The load is not a GameObject.** It is one more matrix in `ChunkRenderer`'s per-item instanced
  accumulator (§5a), keyed by the sparse, unsaved aspects `odyssey.pawn.carrying` (the def index)
  and `.stack` (§5b).
- **§9a:** a new item inherits the hold, the turn and both hand-overs automatically. Drawing it as an
  armful is an opt-in row in `ItemHeap.Recipes`. **A distinct hold is explicitly the job of
  `CarryPose`**, not of a renderer special case: *"the seam for it is `CarryPose`"*.
- **Pose order is fixed** (§4d): sleep, then swim, then climb, then work stroke, then carry, then
  gesture.
- **Could a plate ride it? Yes, as is.** A meal carried today is already scooped at the waist in
  both arms. A plated meal reads right only with a new **tray hold** in `CarryPose`: forearms level,
  palms up, elbows at about 90°. That is the §9a seam, with no renderer change.

### 4. The crouch, and how clips are layered

- **The pack's crouching idle has been tried and taken off.** Design 31 §18d blended
  `A_Idle_Crouching_Femn` / `_Masc` in as a `sitClip`, the last input on the per-figure
  `AnimationMixerPlayable` after the gaits (`PawnFigureDirector.cs:2555-2572`, `:2106-2124`). The
  sit weight is taken *from* the gaits, so the mixer still sums to one. It was measured: 69 % of
  standing crown height, soles unmoved. The owner read it as ***"sneaking/crawling and not sat
  down"*** (§18e), and the clip was removed from all 73 colonist rows.
- **The plumbing is still there, dormant:** `ModuleEntry.sitClip`, the mixer input, and `SitPose`,
  which eases over 0.8 s (`SitPose.cs:1-41`). `SitPoseTests` waits for a row that has a clip. §18e
  recommends a **floor sit authored in Blender on the Synty rig**, committed under
  `Assets/Art/Custom/`.
- **`SitPose`'s own header claims** that a computed sit "needs knees bent past anything the rig will
  take from code, and lowering an upright figure instead puts its feet through the floor"
  (`SitPose.cs:15-20`). That argument is about sitting **on the ground**.
- **The computed crouch** is `ApplyGesturePose` (`PawnFigureDirector.Poses.cs:978-1033`). It moves
  the pelvis down and 25 % back, then solves each leg with `TwoBoneIk` back to the foot the gait
  planted, folds the spine 42° and hangs the arms. The gestures are `Lift`, `Stow` and `Sow`, each
  at depth 0.33 (`Gesture.cs:111-132`), with a guard of `DeepestCrouch` = 0.45 (`Poses.cs:925`).
- **P11 finding: the crouch is 66 mm deep.** Depth is `min(d, 0.45) × StandingHipHeight`, and
  `StandingHipHeight` is the 0.2 m clamp on every rig (`Tools.cs:312-333`). So every stoop drops the
  pelvis by 0.33 × 0.2 = 0.066 m. It is kept deliberately because the owner signed off the stoop as
  it looks. **A held working crouch cannot reuse that number.** It must be a fraction of
  `figure.LegLength`, thigh plus shin, which is a real measurement (`Tools.cs:335-345`).

### 5. The far form

- **`FigureCeiling` = 64**, and the setter clamps (`PawnFigureDirector.cs:58-78`). The eligible list
  is cut to the nearest `MaxFigures` (`:1210-1253`). Everyone past the cap is a **baked static mesh
  instanced by placement matrix**, and the baked pose is fixed (`29-modular-colonists.md` §13, lines
  512-541). Hair and beards follow as two instanced buckets placed at a head transform captured at
  bake time.
- **Nothing in the far form holds a non-standing pose.** Design 31 §18d says so outright: *"The
  instanced baked form poses nobody; it does not lie sleepers down either."* A sleeper past the cap
  is drawn standing in her bed today.
- **This is accepted because it rarely shows.** At the colony sizes the game is played at, the far
  form barely appears (§13, line 582).

### 6. The head while eating, and the gaze system

- **Eating pitches the head.** `UpdateFigureGaze` sets `Gaze.PosturePitchOffset` = −25° when
  `JobDef == JobHandle.Eat`, and −12° when hauling or delivering
  (`PawnFigureDirector.cs:2128-2142`; `docs/design/23-head-turning-and-gaze.md` §4, line 98).
- **The gaze arbiter has six tiers** (§4): sleep lock, ladder, work focus (which aims at
  `WorkCentre` + 1.30 m) and below. Bones are **never written in `Sync()`**, only in `Evaluate()`
  (§2, line 42), because writing both compounds rotation.
- **Eating today is standing.** `EatJobDriver` walks to the meal's cell, which is usually a stockpile
  or shelf cell, and stands there for `workTicks` (`Assets/Odyssey/Sim/Pawns/JobDrivers.cs:130-182`).
  It has no table, no chair and no carried meal. There is no cook or butcher job in the simulation
  yet: a grep of `Sim` for `Cook` and `Butcher` found nothing.

### 7. Web

- **Seated proportions.** Seat height is designed to **popliteal height**, about 35–47 cm on adults
  (5th-percentile women to 95th-percentile men), which is roughly **0.25 of stature**. The knee sits
  at about **90–110°** and the hip at **95–135°**, with the hips a little above the knees
  (Eureka Ergonomic; Sitpack). Odyssey's colonists are about 2.5 m tall at catalogue scale 1.4
  (design 31 §18d table), so a chair that fits them has a seat about **0.6 m** high, not the
  pack's.
- **Eating cycle.** One annotated study gives a **food-to-mouth movement of 0.9 ± 0.8 s**. Bite
  detectors tuned best at a **6–8 s** window per bite, and one source takes about 15 s as a whole
  bite cycle (PMC 5503793 / arXiv 1806.05352; US patent 10213036).
- **Mixamo.** Its clips retarget to a Unity Humanoid avatar in the usual way. **Licence in one
  line:** free and royalty-free for commercial games with no credit required, but the **raw files
  may not be redistributed**, whether as a pack, template or engine package (Adobe Mixamo FAQ;
  LicenseOrg). Committing the FBX files to this repository is the redistribution question §18e
  left open.
- **Synty sells *ANIMATION – Idles*:** 330 humanoid idles for POLYGON characters at $49.99, built
  to blend with Base Locomotion. **Whether it includes a seated or eating idle was not visible in
  the listing.** The *MoCap Central Seated Pack* (257 chair and table clips) is a paid alternative.

## Recommendation

**Compute every one of these moments except the sit on the ground, and back computed over Mixamo.**
The reason is one sentence: a chair, a table top, a plate, a pot and a carcass are all **known
points**, so the body can be placed on them and the hands solved to them on all 61 rigs. That is
the bargain `SleepPose` and the crouch already make. A clip bakes a seat height and a hand path in
another rig's metres (P11 territory), and Mixamo's raw files cannot be committed until the
redistribution question is answered.

The sit **on the ground** at the fire stays §18e's recommendation: a Blender-authored clip through
the dormant `sitClip`. Check *ANIMATION – Idles* first, because buying beats authoring.

**On the chair sit this departs from `SitPose.cs:15-20`, and on purpose.** That argument is about a
floor sit, which needs knees past full flexion, or an upright figure lowered through the floor. A
chair sit is different: the hips are placed on a measured seat, and each knee is a 90° two-bone
solve to a foot target *ahead of* the knee. That is the easy end of what `TwoBoneIk` does, where the
crouch's `DeepestCrouch` limit is about a foot pinned *under* the hips. **The tie-breaker is one
contact sheet.** `SeatProbe`, modelled on `SleepProbe`, poses four rigs on a chair and prints the
clearance at the pelvis band and at the sole. If the hip skinning tears at 90° flexion, the chair
sit moves to the clip route and nothing else in this plan changes.

### Moment by moment

| Moment | Reuses | New | Simulation must publish | Far form |
|---|---|---|---|---|
| **Butcher** (crouch and carve) | `ApplyGesturePose`'s pelvis-down, leg-solve and spine fold, held rather than phased. `WorkStyle` / `WorkStroke` / `IndexForJob`. Tool grip and `HandGrip.Palm`. Blood's spurt and mark (design 33 §10) on contact instead of chips | A **held crouch** whose depth is a fraction of `LegLength` (start at 0.30, cap 0.45), **not** of `StandingHipHeight` (§4 finding). `WorkStroke.Carve`: short, fast (about 0.6 s), low arc, with a large `Dip` aimed at the carcass. `WorkStyle.Butchering` with a knife module. **A `TwoHanded` flag**: the off hand is solved to the carcass (steadying it) rather than to the haft | `JobHandle.Butcher` on the existing `JobDef`, `Working`, and `WorkCell` set to the carcass cell. **No new field.** A butchery table, if the owner prefers one, is the stove row below with a knife | Stands at the corpse |
| **Cook at a stove** (stand, stir or flip) | `WorkStyle` for the flip: a small sagittal `WorkSwing`. Tool grip for the spatula. Face-the-work turn via `WorkCell` | **Stir is not a `WorkSwing`**: it is a horizontal circle, and `WorkSwing` is pitch in one plane. Drive it as a **solve to a moving point**: `TwoBoneIk.Reach` to a target circling (radius about 0.08 of body length) at the pan's measured rim, read off the stove prop's bounds. **Food in the pan** is an instanced item placement on the hob (the carry seam, §3). Its tint is lerped raw → cooked from progress, with a mesh swap at done. The style is picked by **(job, station def at `WorkCell`)**, which is still a presentation lookup and so keeps `IndexForJob`'s "no contract change" (it answers the one-job-two-strokes case, `WorkStyle.cs:500-502`) | `JobHandle.Cook`, `Working`, `WorkCell` = stove. **New sparse aspects:** `odyssey.pawn.cooking` (ingredient or product def) and `odyssey.pawn.cooking.progress` (per mille, derived from the already-saved `ToilProgress`; the SK2 precedent). **Burnt only if the simulation models burning.** Do not invent a failure state for the art's sake (the WS4 rule) | Stands at the stove. Food on the hob still draws, because it is an item placement and not part of the figure |
| **Cook at a campfire** (crouch, stir a pot) | The butcher's held crouch. The stove's stir solve. The campfire art (design 31) | A pot prop on the fire's cell, placed at the fire's measured top. The stir target is lower | As the stove. The station def (campfire) is what selects the crouch | Stands |
| **Carry a plated meal** | The whole carry path: aspect, instanced accumulator, `CarryHandover`, turn (§3) | **A tray hold in `CarryPose`**: forearms level, palms up, plate at the palms' midpoint. The hold is chosen by item def in a presentation table beside `ItemHeap.Recipes` | Nothing new. `odyssey.pawn.carrying` already names the def. A plated meal needs a distinct commodity or a flag only if cooked meals and packaged meals must be carried differently | Stands. The plate is not drawn beyond the cap, as for any carried load |
| **Sit on a chair** (`SeatPose`) | `SleepPose`'s shape: place the body on a known plane, author limb angles, measure off the drawn mesh. `SitPose.Settle`. The `Seated` → face-`WorkCell` turn. Foot planting | `SeatPose`: the root is placed so the **pelvis band** meets the chair's measured seat top (P11: the pelvis band, not the minimum vertex). Upper legs pitched 85–95° by FK. Lower legs **solved** with `TwoBoneIk` to floor targets a fixed fraction of `LegLength` ahead. Spine about 8° forward. Chairs and tables scaled to the figure: seat at about 0.25 of standing crown height, table top at about 0.42 | `PawnView.Seated` (exists) with `WorkCell` = table cell, or the fire. Presentation reads the chair's edifice at `pawn.Cell` to tell a chair sit from a floor sit. **No new field** | Stands beside the chair. Optional mitigation below |
| **Eat seated** | `SeatPose`. Tool grip for fork and knife. The −25° posture pitch in gaze | **A bite cycle**: the right hand is solved to the plate (0.6 s), lifted to a mouth point (the `Head` bone plus a forward fraction) over 0.9 s, held 0.3 s, lowered 0.7 s, then rests 2–3 s. About 5 s per bite, run on the game clock, with the phase jittered by pawn id. The left hand holds the knife solved to the plate edge. The head's posture pitch eases from −25° to about −5° on the lift. The **plate, fork and knife on the table** are instanced placements at the table top; the meal's mesh goes full → half → empty from progress | `JobDef` = Eat, `Seated`, `WorkCell` = table. The meal stays `Job.CarriedItem` while eaten, so `odyssey.pawn.carrying` keeps naming it and presentation draws it on the table rather than in the palms, keyed on `JobDef` = Eat. Eat progress per mille as for cooking. **The eat driver needs a sit toil at a table.** That is the simulation half, not art | Stands |
| **Eat standing** (today's case, kept as the fallback) | Tray hold (plate in the left palm at chest height). The bite cycle with the right hand. The −25° pitch | Nothing new beyond the pieces above | Nothing new. `JobDef` = Eat with no `Seated` | Stands, as today |
| **Stand up** | `SitPose.Settle` over 0.8 s, reversed on `Seated` falling. The seat and leg solves fade with the weight | Only a rule: the fork and plate hand back (plate to the table, or picked up as a tray carry if the simulation hauls the dishes) at the start of the rise, not at the end | Nothing; it is the edge of `Seated` | n/a |

**Pose order.** Seat slots after swim and before the work stroke. Eating's bite cycle is
work-stroke-like and runs after the seat. The tray hold is a carry, so it stays after the stroke,
as §4d has it.

**Tool modules.** A knife, a spatula, a pan, a pot, a fork and a plate. Whether Sci-Fi City or the
other owned packs contain each one is unchecked. Any gap is a few-polygon Blender piece under
`Assets/Art/Custom/`, which the brief allows.

**The far form.** Accept standing, exactly as sleepers do today, because under the 64-figure ceiling
it is only the furthest colonists. If a full dining hall past 64 ever shows it, the cheap mitigation
is to **let a posed pawn (seated, asleep, working) outrank a standing one at similar distance in
the cap ordering**. That is a sort key, not a second bake. Baking a seated variant per body doubles
the far form's modules and is the expensive last resort.

## Sources

**Repository**

- `Assets/Odyssey/Presentation/World/SleepPose.cs:6-118, 286-294, 326-374`
- `Assets/Odyssey/Presentation/World/SitPose.cs:1-41`
- `Assets/Odyssey/Presentation/World/WorkSwing.cs:6-58`
- `Assets/Odyssey/Presentation/World/WorkStyle.cs:24-35, 227-300, 433-521`
- `Assets/Odyssey/Presentation/World/PawnFigureDirector.Tools.cs:13-24, 236-345`
- `Assets/Odyssey/Presentation/World/PawnFigureDirector.Poses.cs:914-1033`
- `Assets/Odyssey/Presentation/World/Gesture.cs:111-132`
- `Assets/Odyssey/Presentation/World/PawnFigureDirector.cs:58-78, 1210-1253, 1870-1910, 2090-2142, 2555-2572`
- `Assets/Odyssey/Sim/Pawns/JobDrivers.cs:130-182`
- `Assets/Odyssey/Sim.Contracts/Views.cs:112-372`
- `docs/design/20-beds.md` §7b
- `docs/design/24-carrying.md` §4a, §4d, §5a–b, §9a
- `docs/design/31-campfire-art-and-fire.md` §18c–§18e
- `docs/design/29-modular-colonists.md` §13
- `docs/design/23-head-turning-and-gaze.md` §2, §4
- `docs/bug-patterns.md` P11
- `docs/research/e-02-characters-animation.md`

**Web**

- https://eurekaergonomic.com/blogs/eureka-ergonomic-blog/ergonomic-chair-seat-height-range-guide
- https://sitpack.com/blogs/news/seat-height-ergonomics-your-2026-comfort-guide
- https://pmc.ncbi.nlm.nih.gov/articles/PMC5503793/
- https://arxiv.org/pdf/1806.05352
- https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/10213036
- https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html
- https://www.licenseorg.com/guide/3d-assets/mixamo
- https://assetstore.unity.com/packages/3d/animations/synty-animation-idles-299700
- https://syntystore.com/products/animation-idles
- https://mocapcentral.com/products/mocap-studio-series-seated-pack

## Confidence

- **High** on how poses are built today, on what the simulation publishes, on the far form, and on
  the 66 mm crouch finding (read directly from code with its own comment).
- **High** on the Mixamo licence.
- **Medium** on the chair sit being computable. It is argued from how `TwoBoneIk` behaves and from
  the crouch's own limit, not measured. The `SeatProbe` contact sheet settles it.
- **Medium-low** on the bite timings. The web figures are for detecting bites, not for staging them
  at a colony-sim camera, so every number here is a starting point for the owner's eye.

## Could not be determined

- Whether Synty *ANIMATION – Idles* contains a seated or eating idle. The listing does not itemise
  its clips.
- Whether the owned packs ship a knife, spatula, pan, pot, fork, plate, chair or table at a usable
  scale. No pack search was made within the cap.
- Whether Synty's hip skinning holds at 90° thigh flexion without collapsing. This is the
  `SeatProbe` experiment.
- Whether the simulation will model burning, table seating or dish hauling. These are simulation
  design decisions for the cooking plan, and the art must follow them rather than lead.
- How long `Job_Eat`'s `workTicks` is, and so how many bite cycles a meal gets. It is in the Defs
  and was not read.
