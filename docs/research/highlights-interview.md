# Highlights reel — the ground and the interview, 2026-09-26

**Phases 0–1 for a new unit.** The owner's request: *"I was wondering whether it would be possible
to have a "highlights reel" so any significant events such as people joining the colonists, dying,
near death or close calls are recorded or stored somehow so later you could watch a highlights reel
so far and at the end of your run. […] It could be replayed in a cinematic fashion and played
together in succession explaining the event. Ask me questions if need be — we don't [want] to hit
performance but wondered what is possible?"*

The prior art is in `b-highlights-reel.md`. The design is `docs/design/63-highlights-reel.md` and the
units are `docs/plans/highlights-reel.md`.

## 1. Does it already exist here?

**No.** What there is:

- **`IncidentLedger`** (`Sim/Events/IncidentLedger.cs`) is saved, hashed and append-only, but it
  records only incidents: the supply drop, theft and a raid's start. An entry is
  `(Id, Tick, IncidentDef, Cell)` with a sparse `Detail(Subject, Amount)`. Only the newest 16 are
  published, for the Events panel.
- **Nothing records** a death, a down, a close call, a raid's end or a milestone. `CombatLog` is a
  32-row unsaved ring kept for floating numbers. A pawn's memories expire.
- **History (F9, `ui.tab.archive`)** is specified (design 23 §5, panel B16) and unbuilt.
- **No intent log.** ADR 0004 says *"an intent log plus a world seed is a replayable test case"* and
  names `Replay_IntentLog_ProducesIdenticalWorld`; neither exists.
- **No end of run**, and no join or recruit mechanic (only the starting colonists "join").

## 2. What makes true replays possible here

- **The simulation is deterministic.** Its randomness is derived per (seed, tick, purpose), so a
  keyframe carries no RNG state. No wall clock, `UnityEngine.Random` or mutable static reaches the
  sim.
- **Every write is an intent** (`IntentBus`, about 49 kinds, the debug menu included).
- **Resume is proven hash-exact**: `BanditSoakTests` saves mid-raid, reloads and matches the unbroken
  run, and the lockstep twin compares hashes hourly.
- **A keyframe already exists every game day**: the autosave, synchronous and 137–559 KB.

## 3. What stands in the way

- **Nobody has replayed from a log**, so paths nobody has exercised could drift. One is already on
  record: a temperature solve can run from a snapshot read, outside the tick (design 28 §12a).
- **`OdysseyBootstrap` is bound to one world**, so a replay has to swap sessions.
- **A replay only plays on the build that recorded it**, which is the industry-wide finding in
  `b-highlights-reel.md` §5–§6.

## 4. The cinematic parts already built

- the Events panel's camera glide (`CameraDirector.JumpTo`)
- First Person's take-over and hand-back of the camera, and its HUD-to-strip swap
- the wake-up's veil, dual-filter blur, grade, muffle and `ClockHeld`
- `PortraitStudio`
- the `AudioDirector` music phases
- Timeline, installed and unused

## 5. The questions and the answers

Two rounds.

| Question | Answer | Chosen over |
|---|---|---|
| How faithful should a highlight be? | **True replays only**: re-simulated from the day's keyframe, full 3D | stills now and replays later (recommended); stills only |
| When would you watch it? | **All four**: end of a run, any time from History (F9), "Previously on…" on load, from a save on the main menu | — |
| Which moments count? | **Deaths, downs and close calls; raids and fights; colony milestones** | personal moments (level-ups, a short jump, weather) |
| How is each explained? | **Chronicle-style sentences** from templates, with a date and a portrait (recommended) | plain captions; captions with the moment's sound |
| A highlight from another build? | **"Then and now" card** (recommended): the sentence and the portrait while the camera glides over that place as it is today | keep one still as insurance; skip it |
| What ends a run? | **All three**: everyone dead or gone, a Retire button, a milestone the player sets | — |
| May you steer the camera? | **Directed, you can take over** (recommended): the mouse takes over, letting go hands it back | directed only; free camera |
| How much lead-up? | **Varies by event** (recommended): a death or down 15 s before and 5 s after, a raid in cuts, a milestone about 8 s | 10 s each; 30–60 s each |

**Consequence the owner has been told:** builds change several times a day here, so on a long run
most highlights recorded on earlier builds will show as cards, not replays. That is the price of
choosing replays over stills, and the card is the owner's answer to it.
