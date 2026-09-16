# Lane D12 — Environmental audio: mixers, pooling, ambience and the colony-sim camera

*Researched 2026-09-16. Unity 6000.3.x manual + engine API, community practice for strategy-scale
audio. Backs ADR 0010 and the implementation in `Assets/Odyssey/Presentation/Audio/`.*

## Question

The brief and the plan say nothing about playing sound — "sound" appears in the design set only
as a future per-cell *gameplay* simulation (propagation the way gas and fire propagate, systems
catalogue row 10). What does a best-practice playback system look like for this game
specifically: a top-down colony camera that pans, orbits and sits 10–160 m above a 3D world;
work sounds (chopping, mining) happening wherever colonists are; environmental beds (water) the
camera may or may not be near; UI alerts; day/night music; and per-bus volume the player
controls?

## Findings

### 1. The mixer is Unity's mix architecture — but it is an asset, not an API

The 6000.3 manual's [Audio Mixer](https://docs.unity3d.com/6000.0/Documentation/Manual/AudioMixer.html)
pages describe the canonical structure: every mixer has a master group, further groups define
the mix, AudioSources route *into* groups (not straight to the output), effects and
send/return chains live on groups, snapshots transition parameter sets "to create different
moods", and ducking — "one group can respond to activity in another — e.g., lowering background
ambient noise while something else plays" — is a mixer feature.

Two facts shape how much of that applies here today. First, **there is no supported API to
create mixer groups**: the `AudioMixer` class exposes lookup only; group creation is an
editor-window operation, and generating the asset from code means reflecting into internal
`AudioMixerController` types that Unity does not commit to. Second, everything the prototype
needs the mixer *for* — bus volumes, one duck, silence — is exactly the part that is plain
arithmetic: volumes are dB, buses multiply in linear amplitude and therefore add in dB, and the
slider-to-dB conversion is `20·log10(v)` (the conversion Unity's own tutorials use for exposed
parameters; see e.g. Unity's [AudioMixer tutorial](https://learn.unity.com/tutorial/audio-mixer)
and community write-ups such as
[GameDevBeginner's 10 Unity audio tips](https://gamedevbeginner.com/10-unity-audio-tips-that-you-wont-find-in-the-tutorials-2/)).

So: **keep the mixer's *concepts* (bus hierarchy, dB, ducking) and implement them in code over
pooled AudioSources**, with every source still routing through a named bus owned by one
director. When the game needs what only a mixer provides — snapshot moods, send-based sidechain
ducking, bus DSP — the sources' `outputAudioMixerGroup` is a one-line change per voice and the
stored dB settings keep their meaning. That is ADR 0010's decision; this document is the
evidence.

One more manual note worth keeping: the AudioMixer is "partially supported" on the Web platform.
Irrelevant to a desktop prototype, but a reason not to make the mixer load-bearing for
correctness.

### 2. Pool voices; never create an AudioSource per sound

`AudioSource.PlayClipAtPoint` and naive `PlayOneShot`-on-demand patterns allocate a
GameObject+AudioSource and destroy it after the clip —
[documented](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.PlayClipAtPoint.html)
convenience, and the standard community advice for anything that happens often is a pool: take
a fixed bank of AudioSources, set position/clip/pitch/volume per play, return on finish (see
[AudioSources vs AudioSource pooling](https://discussions.unity.com/t/audiosources-vs-audiosource-pooling/719481),
[best practices for playing lots of audio](https://discussions.unity.com/t/best-practices-for-playing-lots-of-audios/248700)).
Two properties matter to this project specifically:

- **Per-instance control.** `PlayOneShot` on a shared source cannot vary spatial blend or pitch
  per call and cannot stop one shot; a pool can (the [PlayOneShot performance
  discussion](https://discussions.unity.com/t/playoneshot-performance/595405) names exactly
  this). Pitch/amplitude variance per play is not decoration — an identical sample every time is
  recognisably identical within a few plays.
- **Cull before the voice is spent.** The strategy-scale advice (see [this RTS-focused Unity
  discussion](https://discussions.unity.com/t/what-is-the-recommended-way-to-manage-hundreds-to-thousands-of-audiosources/809600)
  and the [RTS mixing write-up at Stray Pixels](https://straypixels.net/unity-audio-mix/)) is to
  play sounds only for things near the listener and recent, with per-clip repeat windows.
  Unity's engine-side voice management (real vs virtual voices, priority) exists for when you
  *have* too many live voices; done early it never has to. The implementation culls by the
  def's own max distance, gates repeats per sound id (five woodcutters near the camera are one
  rhythm section), and steals by priority when all sixteen voices are busy.

### 3. Rolloff for a camera that is not a pair of ears

The [AudioSource component reference](https://docs.unity3d.com/6000.0/Documentation/Manual/AudioSource-reference.html)
documents spatial blend (0=2D, 1=3D) and the three rolloff modes. The manual-relevant trap:
**logarithmic rolloff never reaches zero** (the community
[fade-out discussion](https://discussions.unity.com/t/audio-volume-does-not-fade-out-to-zero-over-distance/787812)
covers the audible consequence: distant sounds stay faintly audible forever), and linear rolloff
reaches zero with a corner. For a colony camera every work sound is 20–100 m from the listener,
so: spatial blend 1 for work sounds, doppler 0 (the camera flies; the world does not), and a
**custom curve** — full to min distance, easing to zero at the def's max distance.

Music and alerts are the opposite case: they are 2D. Music is mood, not place; an alert is
*for the player*, not from a position.

### 4. Ambience: one bed per environment, driven by what the camera is near

A map holds thousands of water cells; a source per cell is thousands of voices, most of them
under others. The colony-sim pattern (the reference genre's games all do some version of this)
is one looping bed per environment type whose **volume tracks how much of that environment is
near the camera**. What "near" means for a colony sim is the camera's *focus*, not the camera
transform — the camera is tens of metres up in the air, the water is not.

The measurement here (`AmbienceProbe`) samples the terrain mirror in a disc around the focus:
each water cell weighted by `1 − d²/R²`, the sum saturating at "clearly full water", and the
weighted **centroid** placing the bed's 3D voice, so a river *pans* as the camera orbits it.
Sampled every second cell — the disc is ~600 cells, the number changes slowly, and a pond's
centroid does not move between frames. Layer-aware like everything in this project: the probe
reads the active layer's terrain, so water under the floor of a shaft the player has descended
into is not heard through the floor.

### 5. Clip import: match the load type to the sound class

The [AudioClip import settings](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioClip.html)
give per-class guidance the placeholder generator (`AudioSetup`) applies:

| Sound | Format | Load type | Manual's reason |
|---|---|---|---|
| Chop / pick / chime (short SFX) | PCM | Decompress On Load | "smaller compressed sounds"; lightest CPU at play time |
| Water bed (noisy, near-constant) | ADPCM | Decompress On Load | noisy sounds played frequently; 3.5× smaller than PCM |
| Music tracks | Vorbis | Streaming | "continuous audio like long music tracks"; minimal memory, ~200 KB overhead per stream |

Decompressed Vorbis costs ~10× its compressed size in memory — the manual says so directly —
which is why nothing long is decompressed here.

### 6. Persistence and settings

Player settings are not simulation state: the save format is integers-only by design and the
panel catalogue already exempts panel B17 (settings) from the intent queue. `PlayerPrefs` is
Unity's store for user preferences; nothing else in the project uses it, and the audio faders
(in dB, so a stored value survives a future move into mixer exposed parameters) are its first
tenants. That is the B17 stub.

## What this research deliberately does not decide

- **The ADR 0004 event ring.** "A job completed (for audio)" is listed in ADR 0004 as a future
  event type. Work *impact* sounds do not need it — the presentation-side stroke clock
  (`WorkSwing.Lands`) already knows the frame a blow lands, which is the same moment the chips
  fly, and is per-blow rather than per-job. Discrete sim events (a thing destroyed, a
  construction finished) will arrive on the ring when it is built; the director's
  `PlayOneShot(id, position)` is the shape of its consumer.
- **Real audio assets.** The six placeholder clips are synthesised so the system can be heard
  and judged. Licensing real sound is an art decision with a budget, not an engineering one;
  the catalogue re-points and the placeholders are deleted.
- **Gameplay sound propagation.** Systems catalogue row 10 (per-cell, layer-aware, active
  frontier) is a *simulation* — guards hearing an axe — and has nothing to do with playback. It
  remains unscheduled.

## Sources

- Unity 6.3 manual: [Audio Mixer](https://docs.unity3d.com/6000.0/Documentation/Manual/AudioMixer.html),
  [Audio Mixer specifics](https://docs.unity3d.com/6000.0/Documentation/Manual/AudioMixerSpecifics.html),
  [AudioSource component reference](https://docs.unity3d.com/6000.0/Documentation/Manual/AudioSource-reference.html),
  [AudioClip import settings](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioClip.html)
- Unity discussions:
  [AudioSources vs AudioSource pooling](https://discussions.unity.com/t/audiosources-vs-audiosource-pooling/719481),
  [PlayOneShot performance](https://discussions.unity.com/t/playoneshot-performance/595405),
  [managing hundreds to thousands of AudioSources](https://discussions.unity.com/t/what-is-the-recommended-way-to-manage-hundreds-to-thousands-of-audiosources/809600),
  [best practices for playing lots of audio](https://discussions.unity.com/t/best-practices-for-playing-lots-of-audios/248700),
  [volume does not fade to zero over distance](https://discussions.unity.com/t/audio-volume-does-not-fade-out-to-zero-over-distance/787812)
- [Stray Pixels: A technique for mixing RTS audio in Unity](https://straypixels.net/unity-audio-mix/)
- [GameDevBeginner: 10 Unity audio tips](https://gamedevbeginner.com/10-unity-audio-tips-that-you-wont-find-in-the-tutorials-2/)
