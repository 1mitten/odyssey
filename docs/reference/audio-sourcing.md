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
| `alert.wav` | The chime for "a colonist is starving". Heard over the music, which ducks for it. Must read as *attention*, not as alarm — it will fire in a quiet room. | 0.5–1.5 s | no | stereo |
| `water.wav` | The bed for ponds, streams and the river. Flat and eventless — nothing may *happen* in it, or the event repeats every few seconds and becomes the only thing you hear. | 4–15 s | **yes, seamlessly** | mono |
| `ambience-day.wav` | The sound of the world outdoors by day, under everything else: air, distance, birds. The floor of the mix — the thing you stop hearing and would notice the absence of. Eventless, like the water. | 10–60 s | **yes, seamlessly** | stereo |
| `ambience-night.wav` | The same after dark, and a *different world* rather than a quieter one: the day's birds gone, something else started. It plays at a lower level than the day bed. | 10–60 s | **yes, seamlessly** | stereo |
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

`water`, `alert`, the two outdoor beds and the music tracks do not take variants; one of each is
right.

## Why WAV, and not OGG or MP3

The source file never ships. Unity decodes whatever you import and **re-encodes** it into the
compression format the game asks for, so an MP3 or OGG source buys nothing at runtime and costs
something: both are lossy, and re-encoding one to Vorbis stacks artefacts on artefacts. Hand over
the lossless master.

44.1 or 48 kHz, 16 or 24 bit, either is fine. The importer is told what to do with each file:

| Sound | Imported as | Why |
|---|---|---|
| chop, pick, alert | PCM, decompress on load | no decode at all at the instant it plays |
| water, ambience-day, ambience-night | ADPCM, decompress on load | Unity's own answer for noisy sounds played in quantity — 3.5× smaller than PCM, near-free to decode |
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
  were recorded mono in the first place. `alert`, the outdoor beds and the music are 2D and keep
  their stereo image — the outdoor bed is the air itself rather than a thing in the air, and a
  wide stereo field is most of what makes it read as everywhere rather than as over there.

- **The two outdoor beds want to be the same *place*.** They crossfade into each other at dawn
  and dusk over six and eight seconds, so a day bed recorded in woodland and a night bed recorded
  on a moor will read as the map changing underfoot. Same location, twelve hours apart.

## Licensing

Note the licence with the files. If it forbids redistribution, say so **before** they go in —
`Assets/Odyssey/Presentation/Audio/Clips/` is committed to git, and licensed content that cannot
be committed goes in a gitignored folder instead, the way `Assets/Synty/` does. The game already
runs silent with no clips present, so that arrangement works; it just has to be decided rather
than discovered.

## Sounds the game does not ask for yet

Only the eight above are wired. These are the obvious next ones, and each is a catalogue row plus a
file rather than new code — worth knowing if a pack you are buying happens to contain them:
footsteps (by surface), a tree falling, rock collapsing, hauling and dropping items, a building
finishing, eating, sleeping, UI clicks and panel opens, and weather.
