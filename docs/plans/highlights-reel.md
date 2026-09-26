# Highlights reel — the plan

**Related files:**
- The design: `docs/design/63-highlights-reel.md`
- The interview (2026-09-26): `docs/research/highlights-interview.md`
- The prior art: `docs/research/b-highlights-reel.md`

**Phase gate: this plan waits for the owner's approval. No unit is started.** One commit per unit.
A PR per group: 0; 1–3; 4–7; 8–12.

| Unit | What | Gate | Goldens |
|---|---|---|---|
| **HR0** | **Spike.** A handler-level order recorder and a 600-tick light-hash ring behind a debug flag, plus an audit of every presentation write that bypasses the intent bus. The owner plays one session through a raid, with paused orders, across a day turn. An editor test replays the autosave and the log headless. | Report: every light hash matches, and the ms per re-simulated tick. Settles risks 1 and 2 (design 63 §8). | none |
| **HR1** | The temperature solve runs only inside the tick; reads never write (design 28 §12a) | fast tier; a between-tick read leaves the hash trace unchanged | **may move**: re-bake, and measure with `GoldenColonyProbe` |
| **HR2** | `SimWorld.IntentSink`, the recorder, and `Replay_IntentLog_ProducesIdenticalWorld` (ADR 0004). Covers paused orders, the speed-change tick and the load tick, with a tampered-log negative control. | fast tier; Long tier | none |
| **HR3** | `ComputeLightHash` with its cost test; `BuildStamp`; the runtime content fingerprint | fast tier | none |
| **HR4** | `Chronicle` (section `odyssey.chronicle`, saved, not hashed); the combat listener; `IRaidListener`; spawn and despawn | fast tier; a test that the state hash and every golden are unchanged | none |
| **HR5** | The close-call tracker; milestones | fast tier | none |
| **HR6** | The `.reel` folder: keyframe copy off the main thread, log segments, pruning, forks on an older load | fast tier; Unity compile | none |
| **HR7** | Headless `Replayer`: open, fast-forward, verify | fast tier | none |
| **HR8** | `Session` extracted from `OdysseyBootstrap`; park and rebind | Unity tiers; a PlayMode test that a replay leaves the live colony's hash untouched | none |
| **HR9** | `ReelDirector`, `ReelCuts`, captions (`reel.tale.*`, `ui.reel.*`), ranking | Hud tier (test-first); both content checks | none |
| **HR10** | Camera, letterbox strip, music phase, the suppressions | Unity tiers; frame measured | none |
| **HR11** | The "then and now" card | Unity tiers | none |
| **HR12** | Entry points: History F9 with the paged channel, the run's end and *Retire colony*, "Previously on…", the main menu | Unity tiers; both content checks | none |

**Merge order.** In unit order.
- **HR0 decides whether a danger keyframe is needed** (design 63 §3b). If it is, it goes into HR6.
- **HR1 has to land before anything replays** in front of the owner.

**Owed on the owner's machine:**
- the HR0 play session
- both Unity tiers from HR6 onwards
- a play after HR10–HR12, following the handover tables
