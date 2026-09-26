# Riding with a colonist (first-person view) — the ground and the interview, 2026-09-26

**Phase 0–1 for a new unit**, an owner request: *"Plan out a first person mode. Use a button on the
colonist card next to draft etc. This will lock the user into a fps mode until they push esc. This
camera will look into the colonist's view, what they are doing, so you can see them clearly fighting
with a melee, walking etc and a more close up way to see action. Please explore and plan first. Ask
me questions."*

The questions and the owner's answers are at the end (§5). The design document
(`docs/design/56-ride-along.md`, name provisional) is written from them.

## 1. Does it already exist?

**Not in this repository.** No first-person, follow, chase or "hide the HUD" mode anywhere in the
code or the docs. Two things point at it:

- `Odyssey.Hud.CameraDirector`'s class comment: *"Follow-selection belongs here too and arrives with
  the command that asks for it."* This request is that command, and `CameraDirector` is where it
  goes.
- The roster double-click already *closes in* on a colonist (`HudDirectors.CloseInOnColonist`,
  `CameraDirector.CloseUpMetres = 14`). It is a one-off glide, not a lock.

*Ramble* (`f-prior-art-here.md`) is a first-person game, but it is reference only and no code comes
from it.

**Elsewhere, in games of this kind** (from general knowledge, not a research lane; a lane can check
it if the owner wants one):

| Game | What it does | The lesson |
|---|---|---|
| Planet Coaster / Planet Zoo | a first-person view from any guest's or animal's eyes, plus ride-cams | A **spectator** view, with the AI still in charge. It is a treat, not a control scheme. |
| Cities: Skylines | a "follow" camera on a citizen or vehicle from its info panel (first-person camera mods exist too) | Launched from the info panel, the same place as this button. A free orbit round the target, with Escape or a click to leave. |
| Manor Lords | you can walk the lord about in third person | Direct control is a whole second game (movement, collision, animation). It is a large scope step. |
| Kenshi | a close third-person RTS camera that can lock on to a squad member | Close third person is enough to watch a fight read clearly. |
| Dwarf Fortress | Adventure mode is a separate game mode, not a camera | What happens when "be one character" is taken all the way. |
| RimWorld (the reference) | nothing of the kind: top-down only | Nothing to copy. The mechanics are unaffected either way. |

## 2. What the code says (grounding)

**Where it fits.** It is wholly presentation-side: nothing goes in a cell, a save or the hash, so
no golden moves.

- **The button:** `InspectModel.AddColonistCommands` (`InspectModel.cs:1740`) builds Draft, Response
  and the others. `HudShell.ActionButton` (`HudShell.Inspect.cs:2172`) dispatches on the icon key. A
  presentation-only command calls a director and submits no intent.
- **The name:** it needs a `ui.command.<x>` row and a `ui.keys.<x>` row in `icon-keys.csv`, plus a
  wiki rebuild.
- **Escape:** it needs a new top rung, above the context menu, in `SettingsDirector.Escape`
  (`:1818`) and in `SettingsPresenter`'s switch.
- **Input:** `SliceCameraRig` has no suspension flag. A mode enum checked in its pan, zoom and orbit
  branches is small.
- **Saved view:** `ViewStateSection` could carry the mode at version 3. The recommendation below is
  **not** to save it.

**The camera.** `SliceCameraRig` alone writes the transform, as an orbit round `_focus` whose height
is *locked to the slice layer*. The limits are pitch 20–80°, distance 10–160 m, FOV 40 and near
clip 0.3 m. Its `Update` runs before the figures are posed in `OdysseyBootstrap.LateUpdate`, and
the frustum is taken before them too. So:

- **A camera parented to a bone runs a frame late.** The camera must instead be placed from
  `PawnPose.Of` (the same smooth, sim-derived position the figure is drawn from), before the
  frustum is taken.
- **The rig clamps would throw a close pose away.** `RestorePose` would too, so the mode needs its
  own pose and a stored copy of the slice pose to return to.

**Things tuned for a 48° camera 10–160 m up** that an eye-level or shoulder camera would upset:

- **Frustum culling (FC).** A level view reaches the 1,800 m far plane, which gives back most of
  the culling saving. The mode wants its own far plane and fog.
- **The shadow edge.** 60 m by default, it becomes a visible line on the ground ahead.
- **Tree and dressing LOD biases**, tuned for 60–160 m.
- **The grass-clearance mask**, 0.375 m texels, may look blocky at 1 m.
- **Readers of the rig's distance:** birds' zoom scale, the butterfly radius, the rain's screen
  layer and the context menu (which closes on a 5° turn).
- **The walls-down stumps, the hidden upper storeys, the cut-away ceiling and the x-rayed layer
  above** would all look wrong from inside a room. The mode wants the building drawn **as it is**
  (walls up, roof on, the slice at her layer), restored on exit.

**The colonist up close.**

- **Motion.** Her position is continuous and smooth (step progress × tick alpha). Her yaw eases at
  540°/s, with kinks at cell centres on 8-way paths. The slope lean rolls the whole body. The walk
  clip bobs the head. There are a few hard steps: a sheer drop falls 250 mm a frame, the swim exit
  pulls up 55–106 mm a frame, and the jump has its own arc.
- **Size.** Figures are drawn at ×1.4, about 2.5 m tall, with eyes near 2.3 m in a 3 m storey.
  Rooms will feel low from the eyes.
- **The head.** The face is part of the one skinned body mesh, and hair and beard are props on the
  Head bone. **A camera inside the head sees its ink hull (Cull Front) as solid black.** A true eye
  view has to hide the head: scale the bone, drop the watched figure's ink, and draw it
  shadows-only.
- **The gaze.** It is a usable look direction (work focus, ladder, social, path forward). But it
  **glances about at random in a fight**, because combat jobs are not a work stroke. The aim point
  (`figure.AimPoint`, smoothed) is a ready-made look target for a gunman. A melee fight has
  nothing yet.
- **Animations.** The Sword Combat clips (light and heavy combos, hit reacts, stagger, dodge,
  knockdown, death), walk and run, draw and sheathe, and jump. Every work stroke, the pistol aim and
  recoil, carrying, climbing and swimming are **computed**. **There is nothing arms-only or
  first-person.** From the eyes you would see the far end of a sword swing at most; the colonist
  herself, and her fight, are only visible from behind or beside her.
- **Figures.** Under 64 pawns every pawn is a live figure. Over 64 the nearest to the *camera* keep
  one, so a chase camera keeps her automatically. She disappears only if her layer leaves the drawn
  band, which the mode prevents by driving the slice.
- **Time.** Speeds 1–3 and pause all work. Pausing freezes figures but not the camera.

## 3. Best practice (what makes these views good)

1. **Spectate, don't possess**, unless direct control is the point. The colony's AI stays in charge,
   so the view cannot break the simulation, the hash or a save. This is the Planet Coaster and
   Cities pattern.
2. **Third person shows the action; first person shows the world.** "See her fighting" is a
   **shoulder / chase** camera. A true first-person view needs a first-person arms rig to show a
   fight, and we have none.
3. **A spring arm with collision.** The camera sits behind and above the shoulder and pulls in when
   a wall or trunk comes between it and her. The cell grid gives this cheaply. Without it, every
   corridor is a wall filling the screen.
4. **Smooth the target, never parent to a bone.** Follow the body's smoothed position with a
   critically damped spring. Follow her heading with a lag. Never take the head bob or the slope
   roll.
5. **The mouse orbits freely and eases back behind her after a few seconds idle.** The wheel moves
   between close shoulder and near-FPS.
6. **Comfort:** no head bob, no roll, and FOV 55–70° in the mode (40° is a telephoto for a 1 m
   camera), with a setting if FPS is kept.
7. **Leaving restores exactly where you were.** The slice camera's pose, the layer, walls-down and
   the selection all come back.
8. **A minimal HUD:** her name, what she is doing, health, the speed control and an "Esc to leave"
   hint. Alerts still arrive.

## 4. The questions

Asked 2026-09-26. The recommendation is first in each list.

| # | Question | Options | Recommended, and why |
|---|---|---|---|
| 1 | Where does the camera sit? | **Behind the shoulder, wheel in to the eyes** / behind the shoulder only / at the eyes only | The shoulder is the only one that shows *her* fighting. The eyes are kept as the wheel's closest stop, because that is the literal ask. |
| 2 | What does the player control while riding? | **Watch only** (mouse looks round, the colonist runs herself) / watch, plus orders for a drafted colonist (click to move or attack where the crosshair points) / steer her directly (WASD) | Watch only is contained and cannot touch the sim. Drafted orders reuse `SelectionPresenter.Order` with a ray, so they are a cheap second unit. WASD is a new mechanic (a per-tick move intent, collision, a mode in the job system), a Manor Lords-size step. |
| 3 | What does the screen show? | **A minimal strip** (name, doing, health, speed, "Esc to leave") / nothing (cinematic) / the whole HUD | The minimal strip is the "locked in" feeling with the three facts a watcher wants. |
| 4 | Time while riding | **Leave it as it was; speed keys and pause still work** / drop to speed 1 on entry / pause-only photo mode | Changing speed on entry surprises the player. |
| 5 | What is it called? | owner's word: *Ride along*, *Follow*, *Eyes*, *First person*, … | A registry name, so it goes on the button, the key list and the wiki. |

**Assumed unless the owner says otherwise** (each is written as an assumption here so it can be
overturned):

- Colonists only. Animals and bandits later, if ever.
- The button sits in the colonist card's command row beside Draft. A rebindable key is offered,
  unbound by default (free keys: I J K L N O P U Y Z, End, F6).
- Escape leaves. So does her despawning. If she dies, the camera holds on the body for about two
  seconds and then leaves. Downed, she stays in view.
- Not saved: loading a game always returns to the slice camera.
- The building is drawn as it is while riding (walls up, roof on), and walls-down comes back on
  exit.
- A performance measurement is owed: the frame at a level view, at 4K, on Standard and Huge, with
  the mode's own far plane and fog.

## 5. Answers (2026-09-26, round 1)

| # | Answer | What it decides |
|---|---|---|
| 1 | **Behind the shoulder, and the wheel goes in to the eyes** | A spring-arm chase camera with wall pull-in by default, and a continuous wheel range down to an eye stop. The eye stop owes: hiding the head (bone scale, ink off, shadows kept), a combat look target (the aim point for a gunman, the opponent for melee), and a stabilised gaze. |
| 2 | **Watch only** | No intent and no simulation change: the colonist runs herself. The mouse orbits and looks. Drafted orders from the view are a possible later unit, not this one. |
| 3 | **A minimal strip** | Name, activity line, health, speed control and "Esc to leave". Every other HUD region is hidden while riding; alerts still arrive. |
| 4 | **Time unchanged** | Entering or leaving does not touch the speed. Speed keys and pause stay live; every other game key (slice, walls-down, tools, tabs) is gated while riding. |

Still open: **the name** (Q5). The §4 assumptions stand unless the owner overturns one.

**2026-09-26, "Implement".** The name was not given, so the build uses **Ride along**
(`ui.command.ride`), one CSV row to change. Two assumptions moved in the building:

- **The rebindable key was deferred**, to keep the settings layout untouched without a Unity run.
- **The card's dead Prioritise placeholder was removed** to make room for the button.

Design: `docs/design/56-ride-along.md`.
