# 63 — The highlights reel

**Status: designed 2026-09-26; HR0 (the spike) built the same day, waiting on the owner's play session (§10).** Branch `claude/charming-edison-8xso6q`.

**Related files:**
- The ground and the owner's answers: `docs/research/highlights-interview.md`
- The prior art: `docs/research/b-highlights-reel.md`
- The units: `docs/plans/highlights-reel.md`

**The owner's request:** *"a "highlights reel" so any significant events such as people joining the
colonists, dying, near death or close calls are recorded or stored somehow so later you could watch
a highlights reel so far and at the end of your run. […] It could be replayed in a cinematic fashion
and played together in succession explaining the event. […] We don't [want] to hit performance."*

## 1. What it is

**A reel of real moments, re-played.**

- **Recording.** While the colony lives, the game writes down what mattered and when: deaths, downs,
  close calls, raids, and colony milestones. It also keeps the day's keyframe and a log of every
  order given.
- **Watching.** Each moment is re-simulated from that morning's keyframe and played in full 3D,
  from its lead-up to its end.
- **The screen.** A directed camera frames each moment, a letterbox frames the screen, and a
  chronicle sentence explains it.
- **Old builds.** A moment recorded on a different build cannot replay, so it becomes a **"then
  and now" card**: the sentence and the colonist's portrait, over the place as it is today.

| Decision | Answer | Why |
|---|---|---|
| Fidelity | **True replays** (owner) | The only form that lets a camera move through a moment. Here it costs nothing during play, because the simulation is deterministic and every write is an intent. |
| Another build | **"Then and now" card** (owner) | Replays break on every update in every game that uses them (`b-highlights-reel.md` §5–§6). A card needs no footage, and it is built from parts that already exist. |
| When to watch | **End of a run, History (F9), "Previously on…" on load, from a save on the main menu** (owner) | — |
| What counts | **Deaths, downs, close calls; raids and fights; milestones** (owner) | Personal moments (level-ups, weather) are left out. |
| Narration | **Chronicle sentences written when shown** (owner) | RimWorld's tales: a record of stable ids survives rewording, renaming and updates, where stored text does not. |
| Camera | **Directed, the mouse takes over** (owner) | First Person's hand-back, reused. |
| Length | **Varies by kind** (owner) | §6b. |
| Play cost | **None beyond what already exists** (owner) | §3 is shaped by this. |

## 2. The chronicle (Sim)

**`Sim/Events/Chronicle.cs`**, in its own save section `odyssey.chronicle`.
- **No format bump**: a section a build does not know is skipped (`SaveFormat.cs:367`).
- **Saved and not hashed.** This is the saved view's exception, stated the same way: nothing in a
  tick reads the chronicle, so it cannot change what a colony does. No golden moves.
- **Not the incident ledger.** `IncidentLedger` is hashed, so writing a death into it would move
  every golden that has a fight. A raid entry points at its ledger id instead.

**An entry:**

| Field | Why |
|---|---|
| `Id`, `Kind`, `Tick`, `Cell` | what, when and where |
| up to three `PawnId`s, each with a **name and appearance snapshot** (`RollSeed`, outfit) | the dead can still be named and drawn: `PortraitStudio.For(rollSeed, id, outfit)` |
| `RaidGroupId` / ledger id | ties the cuts of one raid together |
| `KeyframeTick` | where a replay starts |
| `BuildStamp` | §4: can this build replay it? |
| light hashes at the lead-in tick and the event tick | §4: did the replay arrive where the colony was? |

**The text is never stored.** A sentence is generated when it is shown, from `reel.tale.<kind>`
templates through `Registry`, with the date from `Calendar`. The content wiki rule applies to the
templates: they go in `docs/design/icon-keys.csv` with the unit that first shows them.

**The hooks**, one per kind of event:

| Event | Where |
|---|---|
| Downed, died | a new `ICombatListener` beside `FriendlyFireListener` (`CombatHooks.cs`). Every death, bleed-out and debug kill goes through `CombatSystem.Kill`; every down, pain shock included, through `Down`. |
| Raid arrival, assault, rout, end | a new `IRaidListener`, called from `RaidSystem.SetPhase` and from the group's removal, which is silent today and becomes the rout's end |
| Arrival, departure | `PawnRegistry.Spawn` / `Despawn` for colonists. There is no join mechanic yet, so founding is the only arrival until one exists. |
| Milestones | founding; first roofed room; first harvest; first powered consumer; first winter survived. Each is read from state the systems already publish, and recorded once. |

**The close call** has no counterpart in the code, so it is defined here:
1. `DamageApplied` opens a **danger window** for a colonist.
2. A Rare-interval tracker keeps the window's **lowest margin**: hit points as a fraction of the
   pool, and blood against `bloodDeathAtPerMille`, whichever is nearer death.
3. The window closes when there has been no damage for 2,500 ticks (a game hour), the colonist is
   not bleeding and not downed.
4. If the lowest margin fell below the threshold (**15 % to start**, a tuning number), a close
   call is recorded, **centred on the tick of the minimum**.
5. A death supersedes it.

This is Left 4 Dead's intensity peak turned round: the recording is of how near death came. The
tracker's state is saved and not hashed, like the chronicle.

**Ranking** (for a reel longer than the owner will sit through): death 100, close call 80 − margin,
raid 60 + casualties, milestone 40. Once the storyteller (design 59, on its own branch) lands, its
*tension* at the tick is added as a tie-break.

## 3. Keyframes and the order log

### 3a. The order log

- **Hook the handler, not the bus.** The recorder wraps `_handleIntent` (`SimWorld.cs:53`), so both
  `Intents.Drain` (in the tick) and `Intents.DrainWhere` (`RepublishViews`, orders given while
  paused) pass through one point, in the order they were applied. Rejected intents are recorded
  too, because a rejection is behaviour.
- **Note `DrainWhere`'s own comment is stale.** It says a command never comes through there, but
  `PausedIntents.AppliesWhilePaused` has let commands through since 2026-09-17 (laying a slab, work
  priorities). The recorder must treat paused orders as real commands.
- **The sink:** `SimWorld.IntentSink`, null by default, in the same shape as `HashSink`.
- **A record:** `(tick, seq, paused, Intent)`, about 36 bytes. A busy day is a few hundred KB.
- **Replaying tick T:** submit T's paused records and call `RepublishViews()`; then submit the rest
  and call `Tick()`. That is exactly the order the live game applied them in.
- **Presentation must not write to the simulation except by intent.** Unit 0 audits the debug menu:
  `DebugSkipTicks` (ordinary ticks, fine), *Ripen crops*, *Finish research*, and anything else that
  reaches past the bus. Every direct write it finds becomes an intent, or the replay cannot see it.

### 3b. Keyframes

**Only the daily autosave, at first.**
- It already exists and already costs its hitch.
- One more synchronous save (about 130 ms on a large board) would land exactly when danger starts,
  which is the one moment a hitch is least acceptable. That breaks "no hit to performance".
- An hourly in-memory keyframe would be 24 hitches a day. **Rejected.**

**Where they live:** at each day turn a **background thread copies** the autosave file into
`<save>.reel/k<tick>.sav`. The loaded save counts as a keyframe too. The order log sits beside it as
`<save>.reel/log-<fromTick>.bin`, flushed off the main thread.

**Pruning:**
- Keep only the keyframes a recorded highlight needs (the nearest one at or before its lead-in).
- Delete logs older than the oldest keyframe kept.
- Cap the folder at about 20 keyframes. A highlight past the cap becomes a card.

**Loading an older save forks history.** Keyframes and logs later than the loaded tick are
discarded, because the future they describe did not happen.

**A danger keyframe** (at raid arrival, taken only inside a frame that is already paused) is added
**only if** unit 0 shows a day of re-simulation takes more than about 8 s. Raids gather for 2–4 game
hours first, which leaves room for one.

## 4. Can this build replay it?

**`BuildStamp`** combines:
- the module version ids of `Odyssey.Sim` and `Odyssey.Sim.Contracts`
- a **runtime content fingerprint** of the loaded Def XML. Only tests take one today; unit 3 adds
  it to the loader.
- `SaveFormat.CurrentFormatVersion`

If the stamp differs, show the card and attempt no replay.

**Light hash.** On a matching build a replay still checks itself. A full `ComputeStateHash` costs
about 10 ms on the played board since OQ-50 hashed the grid, far too much to take at an event. So:
- **`SimWorld.ComputeLightHash()`** walks the tickables and systems and skips the grid.
- It is taken every 600 ticks into an in-memory ring, and an entry keeps the ring's values around
  its lead-in and its event.

**When the light hash disagrees:**
- At the lead-in, still behind the veil: go straight to the card.
- At the event tick: cut to the card.
- Either way it is a **determinism bug**: logged, and an error in development builds. **Every replay
  watched is a determinism test**, the gate ADR 0004 asked for and never had.

## 5. Playing one back

**Park the live session; never save and reload it.**
- A load ticks the world once (`RefreshAfterLoad`, `OdysseyBootstrap.cs:4481`), so a reload would
  move the colony forward a tick for having watched a replay.
- A reload also costs about 200 ms.

**Unit 8** extracts a `Session { SimWorld, ColonyWorld, model, mirror }` from `OdysseyBootstrap`
(4,612 lines, bound to one world today). The live session is parked untouched. The replay session:
1. is built from the keyframe
2. is fast-forwarded headless to the lead-in behind the veil, with `Views.Publish` skipped until the
   veil lifts (safe once unit 1 makes reads pure)
3. is played at ×1 by the reel director
4. is thrown away. Presentation binds back to the parked session and restores its view (camera,
   slice, selection, speed).

Directors already clear themselves on a world change, and that is correct here.

**Suppressed while a replay plays:**
- autosave
- the recorder and the chronicle
- auto-pause and the clock hold
- alert and letter chimes
- every input that submits an intent
- the speed keys

Only camera and reel keys pass.

## 6. The reel

### 6a. The director

**`Odyssey.Hud/Reel/ReelDirector.cs`** is engine-free, in the shape of `WakeTransition` and
`RideDirector`.
- **States:** `Veil → Lead → Beat → Tail → Card → Next | Done`.
- **Input:** mouse delta, pause, skip, Escape.
- **Output:** a `ReelFrame`: focus cell, orbit angle, distance, push-in, letterbox 0–1, caption key
  and arguments, clock rate.
- **Taking over:** moving the mouse switches to *Manual*. After 2 s idle it eases back, which is
  First Person's settle.

### 6b. The cuts (`ReelCuts`)

| Kind | Cut |
|---|---|
| Death, down | 900 ticks before, 300 after (15 s and 5 s at ×1) |
| Close call | round the minimum: 720 ticks before, 480 after (12 s and 8 s) |
| Raid | four cuts of about 10 s: arrival, assault, turning point (the first side to lose a fighter), rout. The gaps are fast-forwarded behind a dip to black. |
| Milestone | 480 ticks (8 s) |

### 6c. The picture and the sound

- **The strip.** `HudShell.Reel.cs` swaps `_worldUi` for a letterboxed strip, as `HudShell.Ride.cs`
  does for First Person. The sentence, the date and a portrait sit in the lower bar.
- **The camera** goes through `SliceCameraRig.RestorePose` and `SetSettle`. A slow orbit is a small
  `SetTargetYaw` addition.
- **Transitions** reuse the wake's pieces: `WakeBlur` for the dip between moments, `WakeVolume`'s
  vignette, and `WakeHearing` for the muffle across a cut.
- **The music** is a new `MusicPhase` in `AudioDirector`. The replayed world's own sound plays
  under it.

### 6d. The card

The **"then and now" card** is shown when a replay cannot play:
- the sentence, the date and the portrait (the appearance snapshot, so the dead still have faces)
- the camera gliding over the entry's cell in the **live** colony as it is today, through
  `CameraDirector.JumpTo`

It needs no replay session.

## 7. Where it is watched

| Entry | Needs |
|---|---|
| **History (F9)** | The tab itself (`ui.tab.archive`, B16), with the paged ledger channel design 23 §5 asks for, extended to the chronicle. *Play highlights* plays the whole run so far; a row plays one. |
| **End of a run** | A run-ended state, which does not exist yet. It is reached by everyone dead or gone, by *Retire colony* (`ui.run.retire`), or by a goal the player sets. The save is marked finished; the reel plays; then the main menu. |
| **"Previously on…"** | On load, after the wake, the last session's top three highlights. Any key skips. The Settings → Interface switch is the wake-up switch's sibling. |
| **Main menu** | On a save row: read only the header and `odyssey.chronicle` (§2), then play the reel. With no live session, nothing needs parking. |

## 8. Risks, and the experiment that settles the two largest

1. **A replay drifts** through a path no test has exercised:
   - presentation writes outside intents (§3a)
   - the temperature solve that runs on a read (design 28 §12a)
   - paused orders
2. **A day of re-simulation from the autosave is too slow.** Ticks cost 0.003–0.2 ms typically and
   3.9–5.8 ms on the worst edit ticks, so re-simulating a day takes 0.2 s to minutes. Too slow would
   force the danger keyframe and its hitch.
3. **Unit 8** refactors a 4,612-line class.
4. **Two worlds in memory** on Huge, at about 80 MB each.
5. **Module version ids under IL2CPP** are unverified.
6. **Portraits and lighting inside a replay.** The portrait rule in CLAUDE.md applies: a render kept
   is a render of everything true at that instant.

**Unit 0, a spike of about a day, settles 1 and 2 together:**
- the handler-level recorder and a 600-tick light-hash ring, behind a debug flag
- the owner plays one real session through a raid, pausing to give orders, across a day turn
- an editor test loads that autosave and log, replays headless, and reports:
  - whether every light hash matches
  - the milliseconds per re-simulated tick

A green spike means the design stands as written. A red one names the drifting system, and the
bisector from OQ-06 (`HashTrace.FirstDivergence`) finds its tick.

## 9. Not in this unit, and recorded

- **Stills.** The owner chose replays. If the card proves too thin on long runs, one still per
  event taken at the moment (an asynchronous GPU readback) is the cheapest insurance.
- **Exporting a reel to video.**
- **Personal moments:** level-ups, a short jump, weather.
- **Joining colonists:** there is no recruit mechanic; the hook is `PawnRegistry.Spawn` when one
  exists.
- **Keeping old builds to replay old moments**, StarCraft II's answer. Not practical for a prototype
  that builds several times a day.

## 10. HR0 as built — the spike, 2026-09-26

**What it is.** The recorder and the replayer that §3–§4 describe, without anything a player sees.
It exists to answer the two risks of §8 on real play before anything else is built on them.

| Part | Where | What it does |
|---|---|---|
| The hook | `SimWorld.ReplaySink` | Called from the intent handler (`HandleIntentAndRecord`) for every applied or refused intent, with the tick and whether it came through `RepublishViews` (the paused frame), and at the end of every tick after the hash sink. Null in an ordinary run: one branch per intent and per tick. |
| The light hash | `SimWorld.ComputeLightHash` | The full hash without the `CellGrid`. **0.54 ms against 23.2 ms** for the full one on the played board (120 x 120 x 16, this container; `ReplayTests.TheLightHashIsCheaperThanTheFullOneAndStillSeesAPawn`). Taken once every 600 ticks while recording. |
| The log | `Sim/Diagnostics/ReplayLog.cs` | `ReplayLog` (binary, `OYRP` v1): seed, size, from and end tick, full hash at both ends, the light hash at the end, a note, the **view preamble**, the records and the checkpoints. `ReplayRecorder` writes it. |
| The replay | `Sim/Diagnostics/Replayer.cs` | Puts the preamble back (`SimWorld.ApplyPendingWithoutTicking`, internal, replay only). Then, per tick, submits the paused records and republishes, submits the rest and ticks. Every applied intent is compared with the record it came from (tick, paused, outcome), and every checkpoint and both end hashes are compared too. |
| Recording in play | `Presentation/Diagnostics/ReplayRecording.cs`, `OdysseyBootstrap.SampleReplay` | **On by default in the editor, never in a batch run.** On a session's first frame: `Logs/replay/replay-<time>/keyframe.odyssey` (the simulation sections only) and `orders.oyreplay`. The log is rewritten once a real minute with the light hash, and at teardown with the full hash. Twelve recordings are kept. |
| The switch | Debug → Cheats → **Replay recording** (`ui.debug.replay`) | Off seals and closes the recording; on starts a new one with its own keyframe. |
| The probe | `tools/dotnet/Odyssey.ReplayProbe` | Replays every recording in `Logs/replay` with no Unity and prints the result, and what a whole day would cost at the measured rate. `--sample <folder>` writes a headless recording to try it on. |

**The view preamble** is the one thing §3 did not foresee. The slice, the speed and the standing
questions (the queried cell, the power and home watches, the shot question) are not in a save,
because none of them is simulation state. But the publish reads them, and design 28 §12a records a
lazy pass that runs from the publish. So the recorder writes them down as the intents that would
ask them again, and the replayer applies them before anything else, unrecorded and uncompared,
because they were already in force.

**What the fast tier proves** (`ReplayTests`, six tests):
- **A session recorded mid-run replays exactly.** The session includes orders given with the clock
  running, two paused frames (one with a refused order in it) and two speed changes. Result:
  start hash, **12 of 12 checkpoints**, the end light hash and the **end full hash with the grid**
  all match, and all 22 records have the same outcome. That is 7,000 ticks in 314 ms, 0.045 ms a
  tick.
- **Two controls, both caught:**
  - One order dropped: parts at the first checkpoint.
  - One order moved to a different tree: parts at the checkpoint after it (3 of 12 agree).
  - A replay that matched because nothing was visible would fail both.
- **The file round-trips**, and a world with no recorder records nothing.

**The audit §3a asked for.** Presentation reaches the simulation only through intents and
`SimWorld.Tick`:
- The debug menu's day, month, morning and night skips are ordinary ticks.
- *Ripen crops* is `DebugRipen`, an intent. *Finish research* never touches the simulation.
- The jumps switch, the raid rows and the weather go through intents too.
- The direct reads that remain (`Incidents.CanFire`, `Temperature.OutdoorTempC`, the storage and
  inventory panes) were read for writes and found none. None of them is the publish-time lazy
  solve; that one runs inside `Views.Publish`, which the replay calls exactly where the game did.

**What only the owner's machine can answer**, and the reason HR0 is not closed:
- Does a real session, with a real player's orders, the wake, a raid and the day skips, replay to
  the same hashes on the game's Mono runtime?
- What does a re-simulated tick cost on a played colony a few days in? That decides §3b's danger
  keyframe.

**Not yet proven:**
- The Presentation half has not been compiled; the fast tier builds neither Presentation nor the
  editor.
- `ReplayRecording` has not run in Unity.

**Next.**
- If every recording matches and a day costs under about 8 s: HR1 (the temperature leak, now
  that a replay can measure it), then HR2 onwards as planned.
- If a recording diverges: the first divergent checkpoint names a 600-tick window. A `HashTrace`
  over that window on two replays of the same log finds the tick, and the fix becomes the next
  unit.
