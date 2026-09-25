# The blow cam — owner interview

**Phase:** Interview (feature-level, in the shape of `drop-interview.md`). **Date:** 2026-09-25.
**Branch:** `claude/intelligent-faraday-1660wy` (documents only; no code was written before or under
this file). **Conducted by:** Claude Code, after a read-only exploration of the combat lanes
(design 33), the tick loop, the camera rig, the figure clips, the audio director and the settings.

The owner's brief, verbatim: *"I want to create an effect — a setting by default, just before or as
someone is being downed/killed — is it possible to have a cinematic close up of what is killing /
hurting them but in extra slow motion the camera zooms over, the sound becomes muted and you can
see the tumble or death / downed hit that occurs and then camera zips back — it can freeze
time/slow time as necessary in very slow motion for effect."* And a minute later: *"sorry sounds
becomes muffled and deep as the camera films the action."*

**Read next, once answered:** the design this decides (a new `docs/design/NN-the-blow-cam.md`),
and the research it may ask for (URP post-processing for the picture treatment, `d-12` already
covers the stack).

## 1. What the exploration found, put to the owner before the first question

**Nothing of this exists, but the one thing it most needs already does.**

- **The outcome of a blow is known before it lands.** Since design 33 §9b (2026-09-24), a swing
  is rolled on the tick its wind-up *begins* — hit, dodge, damage, stun, critical, knockback —
  and kept on the attacker (`Pawn.HeldSwing`) for the 18–36 ticks (0.3–0.6 s at normal speed)
  until the impact applies exactly that. The victim's hit points are published every frame. So
  *"this blow will put her down"* or *"this blow will kill him"* is computable at the wind-up's
  start, and there is a precedent for publishing it: a critical that will land goes out as
  `SwingCritical` in place of `Swing`, because the owner wanted the sword-slice sound *during*
  the swing. The cam wants the same contract one step further: a swing that will cross the
  downed line (0) or the death line (−50 %) is announced at its start. **It can still fall on
  air** — the target may step out of reach, or go down to somebody else's blow first — and the
  simulation then reports a miss and nothing else. A cam that started on the announcement shows
  a miss; that is honest and it will be rare, since the swing only starts in reach.
- **Only combat downs or kills anybody today.** `CombatSystem.Down` and `CombatSystem.Kill` have
  no other callers: starvation collapses a colonist into sleep, a fall and a collapsing floor
  hurt nobody (no health model outside combat), and the temperature severity moves mood, sleep
  and work but never crosses into death. So the trigger is *a melee blow* for now; the seam should
  still be phrased as *somebody about to go down*, so that fall damage or cold arrive later as
  more announcers rather than a second cam.
- **There is no slow motion anywhere, and no fractional speed.** The simulation runs at 0, 1, 2 or
  3 (`SimWorld.GameSpeed`, `SpeedControl`), and the composition root spends ticks off a wall-clock
  accumulator at `1 / (60 × speed)` seconds each (`OdysseyBootstrap.Update`). `Time.timeScale` is
  never touched, on purpose: a pause is held by setting every clip's speed to nought. Every combat
  clip is timed by hand in *ticks* — the authored impact frame lands on the tick the simulation
  resolves the blow (`CombatPose.ClipTime`) — and a walking figure is carried between ticks by
  `_tickAlpha`. **That is the seam for slow motion**: stretch the *interval between ticks* on the
  presentation side and every clip, fall, blood drop and floater follows, because they all read
  the tick and its fraction. The simulation would never know it was being watched slowly; the
  same ticks land in the same order, so determinism and the hash are untouched by construction.
  Two things read the *speed* rather than the tick and would need the dilation fed to them: the
  gait clips' playback rate (`Blend`) and the combat sound scheduler
  (`CombatSoundTiming.TicksPerRealSecond`).
- **The camera has no scripted move but three.** `SliceCameraRig` rebuilds its pose every frame
  from a focus, a pitch (20–80°), a yaw and a distance (**10–160 m**); the only scripted moves are
  the instant `FocusOn`, the Events row's sideways glide (`CameraDirector.JumpTo`, a lerp of the
  focus with the pitch, yaw and distance untouched), and `RestorePose`, which puts live values and
  smoothing targets back together and is the precedent for *returning exactly*. No dolly, no
  target following, no field-of-view change, no roll. Cinemachine is not installed; Timeline is
  installed and unused. A drawn colonist is half again life size (about 2.5 m), so at the rig's
  10 m minimum a figure is roughly a quarter of the frame's height: **a close-up is either a
  lower minimum for the shot's duration, a narrower field of view, or a second camera.**
- **Indoors is solved by walls-down, mostly.** Walls, windows and doors drop to a 0.75 m stump on
  every drawn layer by default (WD, PR #197), so a shot of a fight in a room sees the room.
  A fight on a lower terrace or underground is behind the slice: the cam would have to set the
  slice layer to the victim's and put it back.
- **The sound seam is a duck, and there is no filter.** `AudioDirector` ducks the music for an
  alert chime (2.5 s) and swells it back over a second; there is no mixer, no low-pass and no
  pitch control — the faders are plain volumes (`AudioSettingsStore` says a mixer is a possible
  future). *Muffled and deep* is a low-pass filter and a pitch drop on every playing source, both
  of which Unity provides per source or per mixer group; slowed one-shots (the thud of the blow)
  are a pitch change on that source, which is how a sound sounds in slow motion.
- **The picture has a post stack.** `d-12` mapped the URP post-processing volume for the golden
  hour; vignette, depth of field, colour grading and a letterbox are all one volume weight away.
  Depth of field is the one with a GPU cost worth measuring at 4K.
- **The game knows when the player is watching.** The frame trace (PT) and the developer overlay
  can time the shot; `FrameTimeTests` has the pattern for measuring a pass with and without it.
- **RimWorld never moves the camera on the player's behalf**, and that is the risk to name: in a
  colony sim the player is usually ordering somewhere else when a colonist falls, and a raid of
  seven can put seven bodies down in a minute. The playtest queue is over a hundred rows; the
  rule there is a fix or a measurement before a new feature unless the owner says otherwise.
  This interview is the owner saying otherwise, or not (question 20).

## 2. Precedents, and what each one teaches

| Precedent | Form | Lesson for us |
|---|---|---|
| **XCOM 2 "glam cam"** | The strategy camera swoops to a cinematic angle for a decisive shot, then returns | The closest relative: a top-down tactics game hijacking its own camera. Its outcome is resolved *before* the cam plays — exactly our `HeldSwing`. Players famously switch it off after the first hours, so the budget of how often it fires, and an Off switch, matter more than the shot itself. |
| **Sniper Elite / Fallout VATS / Max Payne** | Follow the projectile in slow motion | Projectile cams; ours is melee, so the shot is about the two bodies rather than a flight. Slow motion of 1/10 to 1/20 with a ramp in and a snap out is the accepted shape. |
| **Sekiro deathblow, Hades, fighting-game hit-stop** | A 3–8 frame freeze on impact, often with a small shake | The cheap alternative: no camera move, runs on every blow, reads as weight. Worth naming as either the *default* form for ordinary blows or the impact beat inside the full cam. |
| **Mount & Blade: Warband** | Slow motion on the *player's own* death only | The trigger is scarcity: it fires when it matters most and never for a lesser blow. |
| **Darkest Dungeon "death's door"** | A slow zoom on the portrait, a stinger, then play resumes | The moment can be marked without moving the world camera at all — a card, a stinger and a zoom on the avatar. An option if the swoop turns out to fight the colony sim. |
| **Total War** | Cinematic camera as a *player-driven* mode | The camera is offered, never imposed. The "any key skips" rule descends from here. |

**What best practice agrees on:** resolve before showing and never fake the result; budget how
often it fires and never queue a second behind the first; skippable by any input and switchable off;
put the player's clock and camera back exactly; and dilate the *presentation* clock, never the
rules.

## 3. The proposal, restated

As read, the proposal is **five things**, judged separately:

1. **An announcement.** The simulation publishes, at the wind-up's start, that a swing will down
   or kill its target — one more event kind beside `SwingCritical`, decided from the held outcome
   and the victim's hit points. Nothing else in the simulation changes; no roll, no save field, no
   hash.
2. **A dilated clock.** Presentation stretches the wall-clock interval between ticks by a curve
   over the shot, so the wind-up's last part, the impact and the fall are watched at a fraction of
   their speed. The simulation runs the same ticks; the player's chosen speed is restored after.
3. **A shot.** The rig swoops to a close framing of the attacker and the victim, holds through the
   impact and the fall, and returns to where it was.
4. **A treatment.** The sound goes muffled and deep; the picture may take a vignette, a letterbox,
   a desaturation or a shallow depth of field; the world-space HUD marks may hide.
5. **A setting.** On by default; a ladder of who it fires for; an Off rung.

## 4. The questions

Each carries the assumption the design will run on if it is not answered, and why it matters. Answer
only where the assumption is wrong.

### A. When it fires

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 1 | **Whose fall?** A colonist going down; a colonist dying; a hostile downed or killed *by* a colonist; an animal killed; a colonist hurt but not down? | A colonist going down or dying, from anything; and a colonist's own blow that downs or kills a hostile. Never an ordinary hit, never an animal, never bandit-on-bandit. | A raid of seven bandits is seven candidates; firing on every one is XCOM's mistake. Keeping it to *your people, and your people's decisive blows* makes it rare enough to keep on. |
| 2 | **How often may it fire?** A minimum gap; and what happens to a second candidate inside it — dropped, or queued? | Never while one is playing; never within 20 s of the last; a candidate inside the gap is **dropped**, never queued (the roster and the alerts already say who fell). | A queue of three cams after a brawl is worse than none. Dropping keeps the mechanic a punctuation mark. |
| 3 | **Start at the wind-up, or at the impact?** The outcome is known 0.3–0.6 s ahead, so the camera can arrive *before* the blow and the player sees it land; or wait for the impact and slow the fall only. | At the wind-up's start: the swoop uses the wind-up, the slow motion covers the last of the swing, the impact and the first of the fall. An announced blow that falls on air shows the miss and returns. | The fly-in needs time from somewhere; taking it from the wind-up costs nothing. A replay (rewind to before the blow) does not exist and would be a recording system of its own — not proposed. |
| 4 | **When the player is not looking.** Fire when the fight is off screen (that is when a cam is most useful), only when on screen, or both? | Both; off screen it *cuts* rather than swoops (question 9). | A swoop across half a Huge board, through terrain, reads as a mistake. A cut says "meanwhile". |
| 5 | **At fast speeds.** At ×3 the wind-up is 0.1–0.2 s of wall time. Fire anyway, and resume at ×3 afterwards? | Yes, at every speed; the dilated clock makes the shot the same length whatever the speed was; `SpeedControl.Resume` puts the speed back. | Fast speed is when you would otherwise miss it. |
| 6 | **When the player is paused, or pauses.** A paused world never lands a blow, so nothing can fire; but if the player pauses *during* the cam? | The pause wins: the cam ends at once, camera restored, world paused. Space during the cam is a pause, not a skip. | The pause is the player's most important key in a raid; the cam must never eat it. |

### B. The clock

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 7 | **How slow, and how long?** A curve: normal → ramp to 1/10 over the swoop → 1/20 through the impact → hold through the fall → snap back. Total wall time? | About **3 s** in all: 0.4 s swoop with the clock ramping to 1/10; the impact and the first 0.5 s of *game* time of the fall at 1/20 (about 1.5 s of wall time); then 0.3 s zip back with the clock returning. The fall's clip (`KnockDown_Begin`, about a second) is not shown whole. | Slower than 1/20 shows the hand-timed clips as stills and the tick's 60 Hz as steps (16 ms of game time is 320 ms of wall time at 1/20, and the tween between ticks is linear). Longer than 3 s is the second thing players switch off. |
| 8 | **A freeze on the impact frame?** A true freeze (hit-stop, 4–8 frames) on the tick the blow lands, inside the slow motion. | Yes, four frames (about 70 ms) at the impact, then the slow motion resumes. | Reads as weight; costs nothing; and it is where a small camera shake would go if wanted. |

### C. The shot

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 9 | **Fly or cut?** A swoop from the player's view to the shot and a fast swoop back; or a hard cut both ways; or a swoop in and a cut back. | Swoop in (0.4 s, ease-out) and zip back (0.3 s, ease-in) when the fight is within about 60 m of the focus on the same layer; **cut** in, zip back otherwise. | "The camera zooms over ... then zips back" is the brief; the cut is the fallback for a fight far away or on another layer. |
| 10 | **The framing.** Where the camera stands: behind the attacker's shoulder looking at the victim; side on to both; low and looking up at the victim? And how close? | Three-quarter from behind the attacker's off-hand side, about **5 m** out, pitch about 15°, the victim's chest at the frame's centre, yaw chosen so both bodies are in frame. The rig's 10 m minimum is lowered for the shot only. | Behind the attacker shows *what is killing them*, which is the brief's own phrase. 5 m at the rig's field of view fills about half the frame with the victim. The owner does visual work and may want to set this in the editor: the shot's numbers will be inspector fields. |
| 11 | **Through the rig, or a second camera?** Drive the shot through `SliceCameraRig` (set its targets, restore with `RestorePose`) or add a cinematic camera that takes over the display? | Through the rig. | One camera keeps the slice, the outline, the walls-down and the picking rules; a second camera is the drop's answer (kilometres up) and is not needed for 5 m. `RestorePose` already exists and is tested. |
| 12 | **Indoors and below.** A fight in a cellar or on a lower terrace: set the slice to the victim's layer for the shot and put it back? | Yes, the slice follows the victim for the shot and is restored with the pose. | Without it a fight underground is a shot of a roof. |
| 13 | **What the shot follows.** Locked on the impact point; or the victim as they fall or are knocked back; or the attacker? | The camera holds still once arrived; the victim falls within the frame. A knockback (one cell) stays in frame at 5 m. | A tracking camera on a slow fall reads as a drift; a still shot reads as a photograph. |

### D. Sound and picture

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 14 | **Muffled and deep — how much, and what escapes it?** A low-pass and a pitch drop on music, ambience and every world sound; does the blow's own thud play slowed and pitched down too, or stay clean and loud? | Everything drops: low-pass to about 800 Hz and pitch to about 0.6 over the swoop, the blow's own thud plays *through the same filter*, slowed with the clock; both come back with the zip. No new sound is added (no heartbeat, no whoosh) — those are sourcing rows if wanted later. | A clean thud in a muffled world is the other legitimate choice, and it is the one film uses; it is a one-line difference. |
| 15 | **The picture treatment.** Which of: letterbox bars, vignette, desaturation, shallow depth of field, a hidden HUD? | Letterbox and a slight vignette, the world-space marks (health bars, floaters, the lock-on ring, order marks) hidden, the HUD panels left. **No depth of field** until measured at 4K. | Each is a volume weight, except depth of field, which is the one with a GPU cost; on the Low preset the treatment should be the letterbox alone. |
| 16 | **Does the owner want to author it?** The shot's numbers, the curve, the filter depth — inspector fields on a component, or constants in a design document? | Inspector fields on the director, with the defaults in the design document and a test holding the defaults. | The owner does the visual work in the editor; the pattern is the camera rig's own fields. |

### E. Control and the setting

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 17 | **Skipping.** Any click or key ends the cam and restores at once? Or only Escape? | Any input; Escape and Space included (Space then also pauses, question 6). A click that skipped is *consumed*, not passed to the world. | A click landing on the world at 5 m through a returning camera would order something nobody meant. |
| 18 | **The setting's ladder and home.** Off / Colonists only / Colonists and their kills / Everyone? On the Gameplay tab beside the autosave? A player preference (like walls-down), not saved with the colony? | Three rungs — **Off, Colonists, Colonists and their kills** — default the third; Gameplay tab; a stored preference never in the save. "Everyone" is left out on purpose. | The autosave ladder is the pattern; `GraphicsOption.WallsDown` is the pattern for a remembered preference. |
| 19 | **The name.** What is it called on the settings row and in the wiki? *Slow-motion blows*? *Dramatic falls*? *Blow cam*? | *Slow-motion blows*, key `ui.settings.gameplay.slow-motion-blows`. | Content is named once in `icon-keys.csv`; the owner corrects names, so the first guess is written down to be corrected. |
| 20 | **Now, or after the queue?** The playtest queue's rule is a fix or a measurement before a feature unless the owner says otherwise. | Build it now, as a small unit stacked on the combat gate (C7), because the combat playtests from C4 on are still unplayed and this would be played in the same sessions. | It is the owner's rule to waive, not the session's. |

### F. Cost and proof

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 21 | **The frame bar.** The usual 5 ms and the same 4K rule; the cam adds only the treatment. Measured how? | `FrameTimeTests` gains one arm: a staged fight with the cam on and off in one run; the treatment's cost is the difference, at 640 × 480 here and at 4K on the owner's machine via the trace. | The rule from the grass and the surround: a cost measured at 640 × 480 says nothing about the GPU. |
| 22 | **What must not move.** The state hash, every golden, every save section, and the combat gate's lockstep twin; a save taken during the cam (the autosave can land there) is a save of the world, not of the shot, and a load never resumes one. | All of that, held by tests: the announcement is a published event only. | Everything here is presentation; if a golden moves the design was wrong. |
