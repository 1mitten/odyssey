# ADR 0010 — Audio playback as a presentation director: pooled voices, code buses, camera-anchored ambience

- **Status:** accepted; implemented 2026-09-16 on `claude/audio-framework`
- **Date:** 2026-09-16
- **Deciders:** owner, with the research in `docs/research/d-12-audio.md`
- **Supersedes:** nothing
- **Related:** `docs/adr/0004-sim-ui-contract.md` (the seam audio reads), `docs/adr/0003-ui-framework.md` (the Unity-free-where-possible principle), `docs/design/10-ui-panel-catalogue.md` (panel B17, settings)

## Context

Nothing in the brief or the plan covers playing sound: the design set mentions sound only as a
future per-cell *gameplay* simulation (propagation, like gas and fire), and the repository had
no audio code at all — one `AudioListener` on the generated camera, no clips, no mixer, no
volume settings. Meanwhile the game now has work worth hearing (felling, mining), water worth
being near, a published pawn list whose needs can cross thresholds, and a day/night clock.

What arrived as the request was the environmental-sound layer: sounds anchored to where the
camera is and what is near it (water, chopping, at differing volumes), in-game alerts, and
background music — as a framework, with the standing constraints of this project: nothing in
Sim or Hud references UnityEngine, scenes and assets are generated rather than hand-authored,
a clone without licensed assets still runs, and everything with a decision in it is testable
headlessly.

## Decision

**Audio is a presentation director, reading only the published frame and the render mirror.**
`AudioDirector` (in `Assets/Odyssey/Presentation/Audio/`) owns every AudioSource in the game,
is constructed by the composition root with the world, is stepped once per frame from
LateUpdate, and is disposed with it. The simulation is never aware audio exists — the same
standing `ChipDirector` has, for the same reason: a sound is not a simulation object, is not in
a cell, is not in the save and is not in the state hash.

**Five buses in code, gains in dB.** Master, Music, Ambience, Effects, Alerts. There is no
mixer asset: bus volumes are dB values that multiply into each source's gain
(`AudioMath`), stored settings are dB so they survive a later move into mixer exposed
parameters, and the duck (music steps down while an alert chimes) is a smoothed gain on the
music voice, applied live. The bus *concept set* is the mixer's; the implementation is the part
that is arithmetic. Adopting a real `AudioMixer` asset is deferred until something needs what
only it provides — snapshot moods, send-based sidechains, bus DSP — at which point routing each
pooled voice through `outputAudioMixerGroup` is a one-line change and nothing else moves
(research §1; the trigger is named so the deferral is a decision and not a drift).

**One pool of sixteen voices for the whole colony, culled before a voice is spent.** Work
sounds, chimes and anything positional come from one fixed bank with priority stealing —
Unity's own voice virtualisation applied one layer earlier: distance-culled by the def's own
max range, repeat-gated per sound id (five woodcutters near the camera are one rhythm section,
not five), and stolen only from lower-priority voices. Per-play pitch and amplitude variance is
part of the def, because an identical sample is recognisably identical within a few plays.

**Ambience is measured, not placed.** One looping bed per environment type; `AmbienceProbe`
samples the terrain mirror in a disc around the *camera's focus* (the camera itself is tens of
metres in the air; the water is not), weights water cells by proximity, saturates the sum at
"clearly full water", and places the bed's 3D voice at the weighted centroid so a river pans as
the camera orbits it. The probe reads the active layer only — water under the floor of a shaft
the player has descended into is not heard through the floor. Layer-aware from the first
commit, per the brief's standing rule.

**Two kinds of ambience, because there are two questions.** The water bed answers *how much of
this is near me* — measured, positioned, scaled. The outdoor bed answers *where am I*, which is
not a quantity: it plays flat whenever the slice is at or above the surface, is silent below it,
and changes with the clock rather than with the terrain. One track per phase, crossfaded at dawn
and dusk, 2D because it is the air itself and not a thing in the air. It rides the Ambience bus
with the water and sits under everything as the floor of the mix — day at 0.34, night at 0.26,
the world being quieter after dark. Music and the outdoor bed are the same shape of thing (a loop
per phase, ping-ponged across two voices, each track keeping its own fade length), so they are one
class, `PhaseLoop`, used twice.

**Music and alerts are 2D and ride their own buses.** Music crossfades between day and night
tracks from the tick, through `GameClock` — the one place ticks become hours — with two
ping-ponged voices so a phase change is a crossfade and never a gap. Alerts chime 2D (an alert
is *for the player*, not from a position) and start the duck.

> **Amended 2026-09-19.** Alerts were raised here by `AlertWatch`, reading the published pawn
> list against its own starvation threshold. That was a second owner of a rule `AlertModel`
> already held, and the two had drifted onto different scales, so the chime effectively never
> fired. Alerts are now raised by `AlertChimeWatch` from the alerts panel's own rows, at the
> refresh that builds them, with the sound chosen by severity. The playback half of this
> decision — 2D, own bus, starts the duck — is unchanged. See `docs/design/24-alert-sounds.md`.

**Trigger sources are what presentation already knows.** The frame a tool lands is
`WorkSwing.Lands` — the same stroke clock that flies the chips — published by
`PawnFigureDirector` as a `BlowLanded` event with the work style and the edge position; the
bootstrap maps style to sound id. This deliberately does not wait for the ADR 0004 event ring:
the ring's audio use-case is *discrete sim events* (a thing destroyed, a construction
finished), while a blow is a presentation-side moment that the ring would only re-derive.
`PlayOneShot(id, position)` is the shape of the ring's consumer when it is built.

**Volume settings are user settings, not sim state.** `AudioSettingsStore` keeps one dB fader
per bus in PlayerPrefs — the stub of panel B17, which the panel catalogue already exempts from
the intent queue, and the integers-only save format is untouched.

**Placeholders are generated, real audio drops in as data.** `AudioSetup` (editor tooling,
`scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build`) synthesises six WAVs — water bed,
chop, pick, chime, day and night chords — and writes the `AudioCatalogue` ScriptableObject
that maps every sound id to clips, bus, ranges, variance and cooldown, in the
`ModuleCatalogue` mould. Import settings follow the manual's per-class guidance (PCM
decompressed for impacts, ADPCM for the noisy bed, Vorbis streamed for music). When licensed
audio arrives: replace clips, delete placeholders, zero code changes.

## Alternatives considered

**An AudioMixer asset now.** The documented best-practice structure, but creating groups has no
supported API — the asset would be hand-authored (against the project's generated-asset
convention) or generated through internal reflection (fragile across editor versions), and
nothing in the prototype needs its unique capabilities. Rejected for now, adopted as the named
migration target (research §1).

**A heartbeat of sim events for work sounds.** Wrong granularity: a job completes once per
tree; an axe lands nine times. The stroke clock already knows the blow to the frame, and it is
presentation-side. The event ring stays reserved for the discrete events it was designed for.

**One AudioSource per emitter** (per water cell, per colonist). Thousands of voices most of
which are inaudible under others; the colony-sim pattern of measured beds and pooled one-shots
gives the same audible result at sixteen voices (research §4).

**Foley-first (footsteps, UI clicks, everything audible).** Scope. The framework covers the
three layers the request named — positional one-shots, environmental beds, music and alerts —
and a new one-shot is a catalogue row plus an id; the rest is content, not architecture.

## What it costs, measured

`AudioCostTests` runs the audio frame on the played board and reports it. A full `Sync` — the
ambience probe's 225 samples, both phase loops, the duck and the alert watch — costs **0.0035 ms**
a frame. Forty one-shots offered in a single frame, four times the colony the slice will ever run,
cost **0.005 ms** against a 5 ms budget.

The second number was **0.15 ms before the director indexed the catalogue by id**, and it grew
with the size of the table: `AudioCatalogue.Find` walked the list comparing strings, which is
nothing at three sounds and 3% of a frame at the few hundred real audio brings. The index, a
cooldown keyed by the def rather than its id, and the rolloff curve moved from every play to
construction took it to a price that does not move with the catalogue at all. The test holds a
0.05 ms budget so that cannot quietly come back.

## Consequences

- Every sound is one `PlayOneShot`/bed/track away, routed, ranged and varied by data; adding
  the *next* sound (building, hauling, a collapse) is a catalogue row, not a code change.
- Ambience reacts to the camera in the way the request specified: sitting near water is audible
  water, a screen away is silence, and the volume follows how much water is in view.
- The developer overlay carries the audio counters (played/culled/skipped/starved, water level,
  music phase), so a silent clone is diagnosable as "no catalogue" rather than a mystery.
- Tests: EditMode suites for the math, the probe, the clock, the watcher and the director (all
  stepped on the director's own clock, so edit mode and play mode answer identically), plus a
  PlayMode smoke test that the composition root builds and steps the system in a live world.
- The ADR 0004 ring, when built, gets its audio consumer for free; no audio-side change will be
  needed for discrete events.
