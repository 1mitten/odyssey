# Sourcing real audio

What to buy or record, what to call it, and where to put it. Drop files in and the game picks
them up; no code changes, and nothing here needs a programmer.

Everything below is generated as a crude synthesised placeholder today, so the system can be
heard and judged before a penny is spent (`AudioSetup`, ADR 0010). Replacing a placeholder is
putting a file of the same name in the same folder.

## The short version

1. Put the file in **`Assets/Odyssey/Presentation/Audio/Clips/`**.
2. Name it exactly as the table says — `chop.wav`, not `Axe Chop 03 FINAL.wav`.
3. Make it a **WAV**. Not MP3, not OGG. See "Why WAV" below.
4. In Unity: **Odyssey → Presentation → Build audio catalogue**. That sets the import class and
   rewrites the catalogue. It never overwrites a file that is already there.

## What the game asks for

| File | What it is | Length | Loops | Channels |
|---|---|---|---|---|
| `chop.wav` | An axe biting into a tree trunk — the felling stroke landing. Dull; wood does not ring. | 0.2–0.8 s | no | mono |
| `pick.wav` | A pick striking stone — the mining stroke landing. A click and a short ring. | 0.2–0.8 s | no | mono |
| ~~`alert.wav`~~ | **Supplied 2026-09-19** and split into five: `alert-normal`, `alert-negative`, `alert-happy`, `alert-joined`, `alert-raid`. See `docs/design/24-alert-sounds.md` — the sourcing note below still applies to any future chime. |  |  |  |
| `alert-raid-arrive.wav` | **Supplied 2026-09-25** (Pixabay, freesound_community, `war-horn-horror-73771`; Pixabay Content License, the owner to confirm) and baked by `tools/audio/bake_raid.sh`: a raid arriving at the edge of the board. 2D, Alerts bus, played by the raid's Events row. **The assault horn the owner supplied beside it (trading_nation, `low-horn-185556`) is already `alert-raid.wav`**, measured sample for sample, so the siren's source and licence are now known. See `docs/design/55-raids.md` §7. | 18.0 s | no | stereo |
| `draft.wav` | **Supplied 2026-09-23** (a sword drawn; Pixabay, Dragon Studio) and baked by `tools/audio/bake_draft.sh`: the sound of a colonist being drafted. 2D, Effects bus. See `docs/design/33-combat.md` §2i. | | | |
| `carry-lift.wav`, `carry-drop.wav` | **Supplied 2026-09-19** as one recording and split into two by `tools/audio/bake_carry.sh`. See `docs/design/24-carrying.md` §12. | | | |
| `swim-stroke.wav`, `_01`, `_02` | **Supplied 2026-09-25** (Pixabay, freesound_community, `swim-44183`; Pixabay Content License, the owner to confirm) and baked by `tools/audio/bake_swim.sh` into three takes at 0.94/1.00/1.06 speed: one arm of a swimmer's stroke, played once per arm as the hand goes in. Its loudest moment is at 0.18 s, and **that number is a timing constant** (`SwimPose.StrokeSoundPeakSeconds`). -24 LUFS max momentary. Heard within 40 m of the camera only. See `docs/design/20-swimming-and-water.md` §9. | 0.71–0.80 s | no | mono |
| `combat-whoosh.wav` | **Supplied 2026-09-23** (Pixabay, floraphonic, `swing-whoosh-4`; Pixabay Content License, the owner to confirm) and baked by `tools/audio/bake_combat.sh`: a weapon through the air, every swing with a weapon, hit or miss. Its loudest moment is at 0.040 s, and **that number is a timing constant** (`CombatSoundTiming.WhooshPeakSeconds`): a replacement must be cut to match, or the constant moved with it. See `docs/design/33-combat.md` §9g. | 0.20 s | no | mono |
| `combat-whoosh_01.wav` | **Supplied 2026-09-23** (Pixabay, floraphonic, `swing-whoosh-3`; the same licence): the second whoosh, cut so its loudest moment lines up with the first's at 0.040 s. | 0.18 s | no | mono |
| `combat-crit-slice.wav` | **Supplied 2026-09-23** (Pixabay, Dragon Studio, `violent-sword-slice`; the same licence): a sharp weapon's critical, played instead of the whoosh, its loudest moment (0.065 s, `SlicePeakSeconds`) on the impact. The source's 2.2 s tail is cut to 1.1 s with a fade. | 1.10 s | no | mono |
| `combat-hit.wav` | **Supplied 2026-09-23** (Pixabay, virtual_vibes, `cinematic-thud-fx`; the same licence): every landed blow, from the struck, played on the hit's own frame — so its transient is cut to its first sample (loudest at 0.013 s). Quiet in the source, lifted 5 dB to the ear through a limiter. | 0.75 s | no | mono |
| `break-wood.wav`, `_01`, `_02` | **Supplied 2026-09-26** (Pixabay, floraphonic, `wood-smash-3-170418`; Pixabay Content License, **confirmed by the owner 2026-09-26**) and baked by `tools/audio/bake_demolition.sh`: something built of wood coming down — broken in a fight or taken apart, wall, door, furniture or floor. The source is brick-walled (+2.5 dBFS in the float decode); the bake takes it down in float, high-passes at 60 Hz and low-passes at 9 kHz, adds the gunshot's outdoor space (a convolved tail and a 140 ms slapback) at −11 dB, and makes three takes at 0.94–1.06 speed with their own tails, at −19 LUFS max momentary. **Imported with `normalize` off.** See `docs/design/58-cracks.md` §9. | 1.70 s | no | mono |
| `break-rock.wav`, `_01`, `_02` | **Supplied 2026-09-26** (Pixabay, Dragon Studio, `boulder-impact-487673`; the same licence) and baked by the same script: a mined face collapsing. Brick-walled harder (+3.3 dBFS, RMS at full scale for its first 0.35 s); high-passed at 35 Hz, low-passed at 5.5 kHz, the rest as the wood. | 1.60 s | no | mono |
| `water.wav` | The bed for ponds, streams and the river. Flat and eventless — nothing may *happen* in it, or the event repeats every few seconds and becomes the only thing you hear. Its level is driven by how much water is near the camera, so what is wanted is the sound of standing beside a stream, not of approaching one. | 15–90 s | **yes, seamlessly** | mono |
| `ambience-day.wav` | The sound of the world outdoors by day, under everything else: air, distance, birds. The floor of the mix — the thing you stop hearing and would notice the absence of. Eventless, like the water. | 10–60 s | **yes, seamlessly** | stereo |
| `ambience-night.wav` | The same after dark, and a *different world* rather than a quieter one: the day's birds gone, something else started. It plays at a lower level than the day bed. | 10–60 s | **yes, seamlessly** | stereo |
| `rain-light.wav`, `rain-heavy.wav` | **Supplied 2026-09-25** (Pixabay: Dragon Studio's `gentle-rain-07`, boons_freak's `rain-sound`) and baked by `tools/audio/bake_rain.sh`: light rain and heavy rain, weighted against each other by the published sky (`RainMix`, design 43 §7a). 2D, Ambience bus. See "What arrived" below. | 42.5 s, 82 s | **yes, seamlessly** | stereo |
| `combat-shot.wav`, `_01`, `_02`; `combat-shot-far.wav`, `_01` | **Supplied 2026-09-25** (Pixabay, freesound_community, `single-pistol-gunshot-33-37187`; Pixabay Content License, the owner to confirm) and baked by `tools/audio/bake_gunshot.sh`: the pistol shot, played on the frame of each shot from the shooter. The source is brick-walled (+4.3 dBTP); the bake takes it down in float, adds an outdoor space (a convolved tail and a 140 ms slapback), and makes a **near** set (three takes at 0.95–1.06 speed, −14 LUFS max momentary) and a **far** set (low-passed at 1.6 kHz, the space forward, −20 LUFS), picked by the shooter's distance from the camera (70 m). Onset within 7 ms of the first sample. **Imported with `normalize` off.** Not played until design 47's P3 wires it. See `docs/design/47-ranged-combat.md` §4c-bis. | 1.4 s, 1.8 s | no | mono |
| `menu-bed.wav` | **Supplied 2026-09-19.** The title screen's bed, and the one sound that must never be heard in a colony. See `docs/design/17-start-flow.md` §12. | 151 s | **yes, seamlessly** | stereo |
| `music-day.wav` | The daytime track. Plays from 06:00 to 19:00 game time, crossfading in over 3 s. | any | **yes, seamlessly** | stereo |
| `music-night.wav` | The night-time track. 19:00 to 06:00, crossfading over 4 s. | any | **yes, seamlessly** | stereo |

### Variants — the thing worth spending on first

A sound played identically is recognisably identical within three or four plays, and a colony of
woodcutters becomes a colony of typewriters. Put extra takes beside the base file with a numbered
suffix and the game picks one at random per blow:

```
chop.wav  chop_01.wav  chop_02.wav  chop_03.wav
```

Up to sixteen per sound. **Three or four takes of `chop` and `pick` will do more for how the game
sounds than any other purchase**, because those two are the sounds a player hears hundreds of
times an hour. The game also varies pitch by ±7% and volume by ±14% on every play, so the takes
do not need to be dramatically different — different enough that the ear stops recognising the
sample.

`water`, the alert chimes, the two outdoor beds and the music tracks do not take variants; one
of each is right.

**If only one take exists, variants can be made from it** — the pick is one recorded strike
resampled to seven pitches, three of them dulled to stand in for a glancing blow. Pitch is most
of what the ear uses to tell two impacts apart, and resampling moves the whole spectrum together
the way a heavier or lighter swing does. It is honestly less than seven recordings and a great
deal more than one clip repeated, and a sound made this way is given more per-play variance than
one with real takes behind it.

## Why WAV, and not OGG or MP3

The source file never ships. Unity decodes whatever you import and **re-encodes** it into the
compression format the game asks for, so an MP3 or OGG source buys nothing at runtime and costs
something: both are lossy, and re-encoding one to Vorbis stacks artefacts on artefacts. Hand over
the lossless master.

44.1 or 48 kHz, 16 or 24 bit, either is fine. The importer is told what to do with each file:

| Sound | Imported as | Why |
|---|---|---|
| chop, pick, carry-lift, carry-drop, `alert-normal`, `alert-negative` | PCM, decompress on load | no decode at all at the instant it plays |
| `combat-whoosh`, `combat-crit-slice`, `combat-hit` | PCM, decompress on load | timed to the frame against the blow; a decode at the moment of play is latency the timing cannot see |
| `alert-happy`, `alert-joined`, `alert-raid` | ADPCM, compressed in memory | seconds long, rare, and nothing is waiting on the frame they start |
| menu-bed | Vorbis, streamed from disc | two and a half minutes of bed nobody should pay memory for |
| water, ambience-day, ambience-night | ADPCM, decompress on load | Unity's own answer for noisy sounds played in quantity — 3.5× smaller than PCM, near-free to decode |
| rain-light, rain-heavy | Vorbis, streamed from disc | long stereo loops that play together for hours, the outdoor beds' reasoning |
| music | Vorbis, streamed from disc | decompressed Vorbis costs ~10× its compressed size in memory, and a track is long |

## Things that will sound wrong if they are not right

- **No leading silence on `chop` and `pick`.** The sound is fired on the exact frame the blade
  enters the wood, matched to the animation and the flying chips. 80 ms of silence at the head of
  the file is 80 ms of the axe visibly landing in silence.
- **`water`, both outdoor beds and the music must loop seamlessly.** They play for hours. A click, a gap or a
  swelling at the loop point becomes a metronome. If you can only get a non-looping bed, say so
  and it can be cross-faded into itself in the tool.
- **Normalise peaks, leave headroom.** Aim for peaks around −3 dBFS and do not master them loud.
  The mix is set in the catalogue — chop sits at 0.85, music at 0.5 — and a clip that arrives
  already squashed cannot be turned back up.
- **Mono for anything with a position.** `chop`, `pick` and `water` are placed in the world and a
  stereo file cannot be placed; the importer flattens them, and it flattens them better if they
  were recorded mono in the first place. The alert chimes, the outdoor beds and the music are 2D and keep
  their stereo image — the outdoor bed is the air itself rather than a thing in the air, and a
  wide stereo field is most of what makes it read as everywhere rather than as over there.

- **The two outdoor beds want to be the same *place*.** They crossfade into each other at dawn
  and dusk over six and eight seconds, so a day bed recorded in woodland and a night bed recorded
  on a moor will read as the map changing underfoot. Same location, twelve hours apart.

## What arrived, and what was done to it

| Clip | Source | Processing |
|---|---|---|
| `campfire` | owner-supplied, `soundsforyou-campfire-crackling-fireplace-sound-119594.mp3`, 2026-09-23 | see below |

**The campfire, 2026-09-23.** 140 s stereo MP3 in, 30 s mono 44.1 kHz 16-bit WAV out.

- **Mono**, because it has a position (the rule above).
- **A 33 s window from 12 s in**, chosen by measuring: the middle of the recording (45–105 s)
  peaks at or above 0 dBFS and is already clipped, and the last ten seconds fade.
- **High-passed at 45 Hz**, then **compressed** (−36 dB, 4:1) — which is the part worth arguing
  about. The raw recording has a **~38 dB crest factor**: a very quiet bed under sharp cracks.
  Left alone it sits at −46.9 LUFS, and getting it to the −19.8 LUFS the synthesised placeholder
  had would have taken about 19 dB of limiting on the cracks, which is the one thing that makes
  it a fire rather than a hiss. Compressed and gained, it lands at **−23.8 LUFS with sample peaks
  at exactly −3.0 dBFS**, which is what this document asks for.
- **The catalogue makes up the difference**: `Volume` 0.55 → 0.75, +2.7 dB of the remaining 4 dB.
  The rest is left — a campfire is a quiet thing and `MinDistance` is 15 m.
- **Looped by folding its own tail over its head**, 3 s equal-power crossfade, so the wrap is
  continuous by construction rather than a splice between two unrelated moments. Verified by
  measurement rather than by listening: the discontinuity at the wrap is **303** against a typical
  sample-to-sample step of **1,246** and a worst of **21,619** — a quarter of ordinary movement,
  which is below the noise.

The lossless master is what is committed; the MP3 is not, per the rule above.

**The rain, 2026-09-25.** Two owner-supplied recordings, one for light rain and one for heavy
(design 43 §7a); `tools/audio/bake_rain.sh` makes both, and `tools/audio/loop_seam.py` checks them.

| | `rain-light` | `rain-heavy` |
|---|---|---|
| Source | `dragon-studio-gentle-rain-07-437321.mp3`, 90 s, 48 kHz stereo, −28.2 LUFS, LRA 7.9 LU | `boons_freak-rain-sound-188158.mp3`, 91 s, 44.1 kHz stereo, −20.6 LUFS, LRA 2.4 LU |
| Window | 0.5–47.0 s: the recording fades by ~7 dB over its second half, so only the steady first half loops | 1.5–88.5 s: steady end to end, ends matching to 0.2 LU; the fade-in and fade-out are cut |
| Level | +3.6 dB to −23 LUFS, peaks limited at −3 dBFS. The limiter touched 101 samples in 4.46 million, so the drips keep their shape | −2.4 dB to −23 LUFS, peaks at −3.1 dBFS |
| Loop | 4 s equal-power fold of its tail over its head, 42.5 s | 5 s fold, 82.0 s |
| Seam | wrap step 330 / 1,426 against a 99th-percentile ordinary step of 2,567 / 3,834; level across the wrap 0.6 dB, inside the file's own 2.4 dB wander | wrap step 21 / 632 against 2,581 / 2,709; 0.2 dB against 2.6 dB |

- **Loudness, not peak.** Every other bed is peak-normalised. Here that would have put the gentle
  rain 7–8 LU under the heavy one, because its drips give it a crest ~8 dB higher, and the
  crossfade between them would have jumped. At equal loudness, `RainMix`'s weights mean loudness.
- **Equal-power folds.** Rain is noise, and two uncorrelated noises crossfaded linearly dip 3 dB in
  the middle: an audible breath at every wrap. The fades are quarter-sine.
- **Levelled before folding**, so the limiter's lookahead never sees the seam.
- **Stereo, source rate kept.** A 2D bed's width is most of what makes it read as all around.

**The butcher's voice and its cleaver, 2026-09-26** (design 62 §8d, `tools/audio/bake_butcher.sh`).
Three owner-supplied Pixabay recordings, each a single call of under a second, mono 44.1 kHz:
`freesound_community-pig-sound-47168` (a grunt), `-pig-squeak-47166` (a squeal) and
`-pig-oink-47167` (an oink).

- **The variations are made, not cut.** Each take is a source resampled lower, 0.54 to 0.90 of
  its rate, which lowers the pitch and slows the call together: a bigger throat, not a
  pitch-shifted small one. A low shelf gives each weight at the play distance, and the deepest are
  low-passed. The result is fifteen takes: strike ×4, hurt ×4, fling ×4, down ×3.
- **Four loudnesses, the owner's order** ("loud when he knocks people back or even when hit"):
  strike −21, hurt −18, fling −15 and down −14 LUFS (max momentary).
- **A lookahead limiter, not the ceiling.** A pig's call peaks about 17 dB over its loudness, so
  holding the −3 dBFS ceiling by turning each take down left the fling at −20 LUFS, no louder than
  the hurt. Each take is lifted by its full gain into a latency-compensated limiter at −3 dBFS.
- **The deep whoosh is the committed sword whoosh at 0.62**, about eight semitones down, with its
  top rolled off. Its head is re-cut so its loudest 10 ms is centred at 0.041 s again, the
  whoosh's own timing constant, and it is levelled at −19 LUFS, two over the sword's −21.
- **Imported without normalising** (`ButcherSoundTests`), or the ladder flattens.
- **The level's pitch at play time:** 1.00, 0.94, 0.88 and 0.82 from the butcher to the king
  (`SpeciesDef.voicePitchPerMille`).
- **Pixabay Content License**, as the blows: the owner to confirm.

## Licensing

**Open, and it has to be decided rather than discovered — `campfire.wav`.** The file the clip was
made from is `soundsforyou-campfire-crackling-fireplace-sound-119594.mp3`. The name is the shape a
Pixabay download has, and the Pixabay licence would allow this, but **nobody has confirmed where it
came from** and the clip is committed to git. If it is not redistributable it belongs in a
gitignored folder the way `Assets/Synty/` does, and the game already runs silent without it.

**Open in the same way — `rain-light.wav` and `rain-heavy.wav`** (2026-09-25). The file names
(`dragon-studio-gentle-rain-07-437321`, `boons_freak-rain-sound-188158`) have the shape of Pixabay
downloads, and if that is where they came from, the Pixabay Content License allows use in a game.
**The owner to confirm the source**, as for the combat sounds; until then this is an assumption
from a file name, and the clips are committed.

Note the licence with the files. If it forbids redistribution, say so **before** they go in —
`Assets/Odyssey/Presentation/Audio/Clips/` is committed to git, and licensed content that cannot
be committed goes in a gitignored folder instead, the way `Assets/Synty/` does. The game already
runs silent with no clips present, so that arrangement works; it just has to be decided rather
than discovered.

## Sounds the game does not ask for yet

Only the eight above are wired. These are the obvious next ones, and each is a catalogue row plus a
file rather than new code — worth knowing if a pack you are buying happens to contain them:
footsteps (by surface), a tree falling, rock collapsing, hauling and dropping items, a building
finishing, eating, sleeping, UI clicks and panel opens, and weather beyond rain: thunder, wind,
and rain drumming on a roof.
