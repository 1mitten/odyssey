# 56 — Riding along with a colonist

**Status: built 2026-09-26; compiled and tested in Unity the same day, not yet played.** Branch `claude/planning-session-fok6wm`.
The ground and the owner's answers are `docs/research/first-person-interview.md`. The owner's request:
*"Plan out a first person mode. Use a button on the colonist card next to draft etc. This will lock
the user into a fps mode until they push esc. This camera will look into the colonist's view, what
they are doing, so you can see them clearly fighting with a melee, walking etc and a more close up way
to see action."*

## 1. What it is

**A First Person button on a colonist's card** (after Draft and her response). Pressing it:

- locks the view to her, over her right shoulder. The wheel steps in to her eyes and back out, and
  the mouse looks round;
- puts the in-game interface away for **one strip** at the bottom of the screen. The strip shows her
  name, what she is doing, her health, the game speed and *Esc — Leave*, with the first alert under
  them;
- lasts **until Escape**. It also ends two seconds after she has gone from the frame.

She goes on living her own life. **Nothing is ordered, so nothing reaches a tick, a save or the
hash** (owner: *"watch only"*).

| Decision | Answer | Why |
|---|---|---|
| Where the camera sits | **Behind her right shoulder, the wheel going in to her eyes** (owner) | Only a camera outside her shows *her* fighting. From the eyes you see the far end of her own swing at most, because no pack has a first-person arms rig. The eye stop is still there, because that is the literal ask. |
| What the player controls | **Watch only** (owner) | Contained: no intent, no mechanic. Drafted orders from the view (a click on the crosshair through `SelectionPresenter.Order`) would be a cheap later unit. Steering her with WASD would be a new mechanic, a unit the size of Manor Lords' walk mode. |
| The screen | **A minimal strip** (owner) | The "locked in" feeling, and the three facts a watcher wants. |
| Time | **Unchanged** (owner) | Speed keys and pause keep working. Entering never touches the speed. |
| The name | **First Person** (`ui.command.ride`) (owner, 2026-09-26) | The owner's name for the mode: *"call this Mode 'First Person' Not go along with the ride"*. Title case as they wrote it, although the rest of the registry is sentence case. The key stays `ui.command.ride` because a key is stable and only the label moved; the code keeps *ride* as its internal word. |

## 2. Where it lives (who owns what)

- **`Odyssey.Hud.RideDirector`** (engine-free) owns the ride's state:
  - who is being ridden with, and the arm;
  - the wheel stops `0, 1.6, 2.4, 3.2, 4.2, 5.4, 7` m. It starts on 3.2 m, and 0 is her eyes;
  - the player's look (yaw offset from her facing, and pitch);
  - the idle settle: 2.5 s after the last mouse movement, the look eases back behind her at a rate
    of 2/s;
  - the hold, for two seconds after she leaves the frame;
  - her card for the strip, an `InspectModel`, so the strip and the pane word her the same way.
- **`HudDirectors.BeginRide` / `AdvanceRide` / `EndRide`** own the rules between directors:
  - a ride clears the selection, puts down an armed tool, cancels a camera jump and suspends the
    game's keys (`HotkeyDirector.Suspended`);
  - the slice follows her layer while riding;
  - leaving restores the layer and makes her the selection, so the pane the player pressed from is
    the pane they come back to;
  - a new `HudDirectors` clears `Suspended`, because the key preferences outlive a session.
- **`Odyssey.Hud.RideCamera`** (engine-free) is the geometry. Section 3 describes it.
- **`SliceCameraRig` (partial, `SliceCameraRig.Ride.cs`)** does the engine half:
  - reads the mouse (the look and the wheel) and the four time keys;
  - puts the colony view's drawing aside and gives it back (section 4);
  - locks and hides the pointer;
  - stands the camera in `PlaceRide`.
- **`OdysseyBootstrap.PlaceRide`** calls it **first in `LateUpdate`** (section 3).
- **`PawnFigureDirector.HeadHidden`** takes her head away (section 4).
- **`HudShell.Ride.cs`** draws the strip and swaps it with `_worldUi`.
- **`SettingsDirector.Escape`** has a new widest overload whose first argument is `riding`. It
  answers `LeaveRide` above every other rung.

## 3. The camera

`RideCamera.Solve` takes where she is drawn standing, her eye height, the view's yaw and pitch, and
the arm. It returns where the camera stands:

- **The pivot.** It sits 0.6 m to her right and 0.35 m above her eyes when the arm is 1.5 m or more.
  Below 1.5 m it blends continuously to a point 0.18 m in front of her eyes. So one control runs from
  "watch her" to "see what she sees", with no second mode.
- **A spring arm.**
  - The arm is marched back from the pivot in 0.1 m steps.
  - It stops 0.3 m short of the first solid point.
  - The pivot's own step to the shoulder is marched out from the eyes the same way, so a wall at her
    right shoulder does not swallow it.
  - What is solid is the rig's `RideBlocks`: a cell that occludes a face (rock, wall, window,
    pillar), or within 0.2 m under the slab of the storey above.
  - **Doors are not solid for the camera, although the picker counts them.** Treating the doorway she
    is standing in as rock would collapse the camera into her head on every threshold.
  - Trees and bushes are not solid either. The sight lines fade them instead (section 4).
- **In at once, out gently.** The rig pulls the shown arm in on the frame something is behind her.
  When the obstruction clears, the arm eases back out at 6/s. A camera flung back out reads as a jolt.
- **Following her.**
  - Across the ground the camera follows her position exactly, so she keeps her place in the frame.
  - Height is eased at 10/s, so a hop or a terrace step is a lift, not a jolt.
  - Facing is eased at 4/s over the shoulder and 12/s at the eyes.
  - Eye height is the head bone plus 0.12 m, eased at 3/s, which takes out the walk's bob. It drops
    as she sits, sleeps or goes down.
  - **Nothing is parented to a bone.** Parenting would carry the walk's bob and the slope lean's
    roll into the view, the two things chase cameras are worst for.
- **The field of view is 60° and the near clip 0.05 m** while riding. The colony camera's 40° is a
  telephoto at a metre. Both are given back on leaving.
- **Where it is stood, and why there.** The rig's `Update` runs before the figures are posed.
  The frustum, the viewer position (used by LOD and the figure cap) and the sight lines are all
  taken at the top of `LateUpdate`, before the figures. So the bootstrap stands the camera first
  thing in `LateUpdate` (`PlaceRide`), from her figure's feet as last drawn. The pose from
  `PawnPose.Of` is the fallback until her figure exists.
  - The feet are one frame old. The target is itself smoothed over several frames, so that lag is
    invisible. In return, every reader sees the camera that will render.
  - Following the bare pose instead would lose her on a jump's arc, a climb and a swim, which the
    figure director lays on top of it.
- **The colony view is frozen, not moved.** A ride writes only the camera's transform. The focus,
  yaw, pitch and distance are untouched, so:
  - a save taken mid-ride records the colony view (`ViewStateSection` reads those fields);
  - leaving is one instant `ApplyTransform`.

  Readers that follow *the view* take `ViewFocus` and `ViewDistance`: the grass clearance, the
  ambience anchor, the rain, the birds, the butterflies and the sight lines. `Focus` and
  `TargetDistance` stay the colony view's own.

## 4. The building as it is, and her head

- **Walls up, roof on, nothing x-rayed.** The colony view cuts buildings open to read them from
  above. From beside her that would be a house with no ceiling and a ghost for the rock over a mine.
  - The rig sets the slice to `Full` above (`followDepth` off), `Normal` below, and the cut-away
    ceiling off.
  - The bootstrap holds walls-down off while riding, because it writes that every frame.
  - All four come back on leaving.
- **Her head goes when the camera is against it.** Her face is part of her one skinned body. Its ink
  hull is drawn with front faces culled, so from inside the head the hull is solid black, and there
  is no separate head renderer to switch off.
  - Under 0.6 m from her eyes (`RidePose.FromEyes`), `PawnFigureDirector.HeadHidden` shrinks her
    head bone to a thousandth. That takes the face, the hull, and the hair, beard and headgear slots
    all together.
  - This happens at the eye stop, and wherever walls have squeezed the camera up against her.
  - It is re-applied every frame, after the gaze pass, and restored whenever it moves off a figure.
    A pooled figure can be retired and leased to somebody else between two frames, and that
    somebody must not arrive headless.
- **The sight line.** A line from the camera to her fades trees and bushes, like the selection's.
  - It is added *after* `SightLines.Primary`, so walls never fade: the camera is kept out of walls
    rather than seeing through them.
  - The ride clears the selection, so she is never both.
- **Her gaze glances at random in a fight** (combat jobs are not a work stroke). The ride does not
  use it: the camera follows her body's facing, which turns to her opponent. The glance is still
  there for anybody watching her head from the shoulder, which is a later refinement.

## 5. Entering and leaving

- **Entering:**
  - The button is on the card (`InspectModel.RideKey`), live unless she is tombstoned.
  - `HudDirectors.BeginRide` refuses anybody who is not `PawnView.IsColonist`, and a second ride
    over the first.
  - The rig abandons any drag, box, hover or glide.
  - It locks and hides the pointer. The mouse's travel turns the view.
- **Leaving:**
  - **Escape.** `SettingsPresenter` asks the ride first. The rebind listener, the leave prompt and a
    focused text field still sit above it, as they do above every rung, but none can be up while
    riding.
  - **Her going.** The camera holds where she was, the strip greys, and after two seconds the ride
    ends by itself. If she comes back inside the hold (a dropped frame), the hold is forgotten.
  - **A session ending.** `SliceCameraRig.Bind` and `OnDisable` give back the drawing and the
    pointer, and the shell hides the strip.
- **Keys while riding:**
  - Pause and speeds 1 to 3 still work.
  - The slice keys, walls-down, the tool keys, the tab keys, Draft and the debug menu are held
    (`HotkeyDirector.Suspended`, folded into `GameKeysLive`).
- **Not saved.** A load always opens on the colony view.

## 6. The strip

Bottom centre, `.ride` in `Hud.uss`, never pickable. It holds:

- her name, and her activity line (with her condition beside it when she is hurt);
- a 96 px health bar in the pane's own colours, with its value;
- the speed, as the registry names it (`ui.speed.*`);
- `Esc` and *Leave* (`ui.command.leaveride`);
- underneath, in amber, the first alert's lead line when there is one.

It refreshes on the inspect pane's fast cadence. The alerts go on being watched while the in-game
interface is hidden, so their chimes still sound.

**The card's Prioritise placeholder is gone.** It was a dimmed button promising *"job priorities
arrive with the work grid (M7)"*. The work grid has arrived, and a forced "do this next" belongs to
the right-click menu. A fourth labelled button would have run her name under the buttons of a 560 px
pane whose name does not wrap. The same rule took the store's dead Rename out: an affordance for
something that does not exist is worse than a gap.

## 7. What is owed, and what not to undo

- **Owed: the frame, measured.** A level view reaches the 1,800 m far plane, which gives back much of
  frustum culling's saving. The 60 m shadow edge and the LOD biases were tuned for a camera
  60–160 m up. Measure it in `FrameTimeTests` at 4K on Standard and Huge, riding and not riding. Do
  not guess a far plane: the fog is only 97 % at 900 m, so a nearer plane would cut the land in front
  of the sky.
- **Owed: the key.** A rebindable First Person key, unbound by default. It was left out so this unit
  would not move `SettingsLayout.KeyColumns` without a Unity run.
- **Owed: the look.** Is 3.2 m the right first stop? Does the eye stop read with her head gone? And
  the grass-clearance mask's 0.375 m texels at a metre.
- **Later:** drafted orders from the view; a gaze target for a melee fight; riding with an animal or
  a bandit.
- **Do not:**
  - parent the camera to her head bone;
  - make doors solid in `RideBlocks`;
  - move `PlaceRide` below the frustum or the sight lines;
  - write the colony view's focus or zoom during a ride.

  Each of these is a failure described above.

## 8. Tests

`RideTests` and `RideCameraTests` run in the fast tier (Hud), 24 in all. Each rule has its control
beside it:

- the button's place and state;
- colonists only;
- what a ride puts aside;
- the slice following her;
- the two-second hold and its reset;
- what leaving gives back;
- a new session's keys;
- Escape above every other rung;
- the wheel's ends, the eye stop's level, and the idle settle;
- the arm, the shoulder, looking down, the eye stop, a wall behind, a wall at the shoulder, the
  ground, and a squeezed camera's distance from her eyes.

`HudModelTests` now expects First Person among the live commands.

None of this compiles the Presentation half. That is Unity's (process §5). **First Unity run,
2026-09-26, after merging `main`:** the Presentation half compiled clean first time; EditMode 4,387
total, 4,348 passed, 1 failed (`WeaponSheathGapTests`, a bat 3.2 cm off the hip, which fails on a
clean `main` with the same numbers and is recorded in the journal); PlayMode 161 total, 144 passed,
0 failed. Nothing tests a ride end to end in PlayMode: a PlayMode test cannot press the card's
button (CLAUDE.md, *Nothing tests that a click reaches the game*), so the first ride is the playtest.
