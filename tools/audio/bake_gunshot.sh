#!/usr/bin/env bash
#
# Bake the pistol shot from the recording the owner supplied (2026-09-25,
# docs/design/47-ranged-combat.md §4c-bis): "blend this into the environment and process it for
# every gun shot (and give it some variance)". Played once per shot, on the frame of the `Shot`
# event, from the shooter's feet.
#
# ## The source, measured (mono downmix, 48 kHz decode)
#
#   freesound_community-single-pistol-gunshot-33-37187.mp3   2.50 s stereo, 24 kHz, 160 kb/s.
#       Silent (-60 dB) to 0.043 s; the report starts at 0.046 s and is at full level by 0.06 s;
#       a 200 ms plateau at about 0 dBFS RMS to 0.22 s; -20 dB by 0.40 s, -40 dB by 0.80 s,
#       -60 dB by 1.30 s, digital silence from 1.82 s. Integrated -6.0 LUFS, true peak +4.3 dBTP,
#       sample peak +7.0 dBFS in the float decode, and 4,082 samples at or over full scale: the
#       recording was mastered into a brick wall before it was encoded, and the MP3 decode
#       overshoots it. A close, dry, indoor-sounding report with no space round it.
#
# Pixabay, under the Pixabay Content License (the `freesound_community` handle is Pixabay's
# import of Freesound material): free for use in a product without attribution, modification
# allowed, not to be redistributed on its own. Committed as part of the game, which that licence
# allows — the owner to confirm, per docs/reference/audio-sourcing.md "Licensing".
#
# ## What is done to it, and why
#
# - **Everything is float until the last step**, and the first thing done is -12 dB: the decode
#   is 7 dB over full scale, and any integer step before the gain would clip it a second time.
# - **High-passed at 60 Hz**: the source carries a -0.005 DC offset and sub-bass rumble that a
#   laptop speaker turns into a flap, and that eats the headroom the report needs.
# - **The head is cut hard at 0.044 s**, 2 ms before the onset, with a 1 ms guard fade — a fixed
#   time, not a silence threshold, because the report plays on the frame the muzzle flash is drawn
#   and the onset must stay on the first frame (below). There is no latency to hide behind: the
#   flash and the sound start together.
# - **Blended into the environment.** The source is dry and close, which in a meadow reads as a
#   sound effect laid over the world rather than a shot fired in it. So each take is the dry
#   report plus an **outdoor space**: a diffuse tail from a synthesised impulse response (pink
#   noise decaying at 5.5 nepers a second, band-limited 180 Hz - 3.2 kHz because air and foliage
#   take the top and open ground does not hold the bottom, 22 ms pre-delay for the nearest
#   reflecting ground), plus one soft **slapback at 140 ms** — a treeline or a terrace riser
#   ~24 m off. No room: the board is outdoors, and a roof over the shooter is a later seam (§4c-bis).
# - **Two distances, as two sounds.** The director has no per-voice filter and a rolloff curve can
#   only make a sound quieter with distance, never duller. So there are two sets:
#     near  (`combat-shot`, three takes)   the dry report forward, the space at -13 dB under it
#     far   (`combat-shot-far`, two takes) low-passed at 1.6 kHz (air absorbs the top of a
#                                          report within 100 m), the transient softened, and
#                                          the space only 3 dB under the report — far away,
#                                          most of what reaches you is the space
#   and design 47 §4c-bis picks one by the shooter's distance from the listener.
# - **Variance**: the near takes at 1.00 / 0.95 / 1.06 speed and the far at 1.00 / 0.96 — the
#   swim stroke's and the pick's trick, pitch being most of what tells one report from another.
#   Each take gets its **own impulse response** (a different noise seed), so the tails differ
#   too and three shots in a row are three shots, not one sample at three pitches. The director
#   adds its own pitch and volume variance on top (catalogue values in design 47 §4c-bis).
# - **Mono**: it is placed at the shooter.
# - **The tail is cut at 1.40 s (near) and 1.80 s (far) with a fade**, by which point the dry
#   report is 60 dB down; a pistol fires at most once a second (the cooldown counts from the
#   aim's start), so a shooter's next report lands on her last one's tail 45 dB down.
# - **Levelled on loudness into a lookahead limiter at -3 dBFS.** Targets, max momentary:
#       near  -14 LUFS  the loudest event in the game: 4.5 dB over the melee thud (-18.5), 1 dB
#                       over the critical slice's target, because a gunshot should be
#       far   -20 LUFS  before the rolloff; a distant shot is a thump, not a crack
#   The limiter is latency-compensated so the onset does not move (the combat thud's approach).
#   Peaks are measured with astats, never volumedetect (the draft bake's lesson).
#
# ## The shipped files, measured after the bake (2026-09-25)
#
#   file                  speed  length   max M       sample peak   onset (> -30 dBFS)  full level
#   combat-shot.wav       1.00   1.400 s  -14.2 LUFS  -3.0 dBFS     0.005-0.007 s       0.015 s
#   combat-shot_01.wav    0.95   1.400 s  -14.1 LUFS  -3.0 dBFS     0.005-0.007 s       0.015 s
#   combat-shot_02.wav    1.06   1.400 s  -14.3 LUFS  -3.0 dBFS     0.005-0.007 s       0.015 s
#   combat-shot-far.wav   1.00   1.800 s  -20.0 LUFS  -4.6 dBFS     0.015 s             0.10-0.15 s (the space blooms)
#   combat-shot-far_01    0.96   1.800 s  -20.0 LUFS  -5.9 dBFS     0.016 s             0.10-0.15 s
#
#   No sample at full scale in any take (4,082 in the source). The near takes' plateau is 0.015 to
#   0.18 s, so "the loudest 10 ms" wanders across it from take to take and is NOT a timing constant;
#   the onset is, and it is under a frame at 60 Hz, so the report plays on the Shot frame with no
#   offset — `CombatSoundTiming` schedules nothing for a shot (design 47 §3f). The -3 dBFS ceiling
#   binds on every near take: the limiter holds the plateau, which the brick-walled source had
#   already flattened, so it changes the sound less than its gain reduction suggests.
#
# Usage:  tools/audio/bake_gunshot.sh [path/to/freesound_community-single-pistol-gunshot-33-37187.mp3]
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

IN="${1:-$HOME/Downloads/freesound_community-single-pistol-gunshot-33-37187.mp3}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$ROOT/Assets/Odyssey/Presentation/Audio/Clips"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

RATE=44100
HEAD=0.044
CEIL=-3.0

[ -f "$IN" ] || { echo "missing: $IN" >&2; exit 1; }

max_momentary () {  # the loudest 400 ms window, over a padded copy so a short file is measured whole
  ffmpeg -hide_banner -nostats -i "$1" -af "apad=pad_dur=0.5,ebur128" -f null - 2>&1 \
    | grep -oE "M: *-?[0-9.]+" | awk '{print $2}' | sort -g | tail -1
}

peak_db () {
  ffmpeg -hide_banner -i "$1" -af astats -f null - 2>&1 | grep "Peak level dB" | tail -1 | awk '{print $NF}'
}

onset_at () {  # when the report first rises above -30 dBFS: the moment the flash is drawn against
  ffmpeg -hide_banner -nostats -i "$1" -af "silencedetect=n=-30dB:d=0.001" -f null - 2>&1 \
    | grep -oE "silence_end: *[0-9.]+" | head -1 | awk '{printf "%.3f", $2}'
}

# The source, decoded once: mono, float, 44.1 kHz, 12 dB down, high-passed, head cut.
ffmpeg -y -v error -i "$IN" \
  -af "volume=-12dB,highpass=f=60:p=2,aresample=$RATE,atrim=start=$HEAD,asetpts=PTS-STARTPTS,afade=t=in:st=0:d=0.001" \
  -ac 1 -c:a pcm_f32le -ar $RATE "$TMP/dry.wav"

impulse () {  # impulse <out> <seed> <seconds>: a diffuse outdoor space
  ffmpeg -y -v error -f lavfi -i "anoisesrc=d=$3:c=pink:r=$RATE:a=0.5:seed=$2" \
    -af "volume='exp(-5.5*t)':eval=frame,highpass=f=180,lowpass=f=3200,afade=t=in:d=0.012,adelay=22,apad=pad_dur=0" \
    -ac 1 -c:a pcm_f32le "$1"
}

bake () {  # bake <clip-name> <speed> <seed> <wet-dB> <lowpass-Hz|0> <keep-s> <fade-from-s> <target-LUFS>
  local name="$1" speed="$2" seed="$3" wet="$4" lp="$5" keep="$6" fadeat="$7" target="$8"
  local shifted; shifted=$(awk "BEGIN{printf \"%d\", $RATE*$speed}")
  local keep_s fade_s
  keep_s=$(awk "BEGIN{printf \"%.4f\", $keep}")
  fade_s=$(awk "BEGIN{printf \"%.4f\", $fadeat}")

  impulse "$TMP/ir-$name.wav" "$seed" 1.6

  # Speed first (pitch and length together), then the tone, then the space.
  local tone="anull"
  [ "$lp" != "0" ] && tone="lowpass=f=$lp:p=2,lowpass=f=$lp:p=2"
  ffmpeg -y -v error -i "$TMP/dry.wav" -i "$TMP/ir-$name.wav" -filter_complex "
    [0]asetrate=$shifted,aresample=$RATE,$tone,apad=pad_dur=2,asplit=3[d][r][s];
    [r][1]afir=dry=10:wet=10:irnorm=1[space0];
    [space0]volume=${wet}dB[space];
    [s]adelay=140,lowpass=f=1800,volume=$(awk "BEGIN{printf \"%.1f\", $wet-6}")dB[slap];
    [d][space][slap]amix=inputs=3:normalize=0,atrim=end=$keep_s[out]" \
    -map "[out]" -c:a pcm_f32le -ar $RATE "$TMP/$name.raw.wav"

  local m p gain
  m=$(max_momentary "$TMP/$name.raw.wav")
  gain=$(awk "BEGIN{printf \"%.2f\", $target-($m)}")

  # Level to the target into a latency-compensated lookahead limiter at the ceiling.
  local lim; lim=$(awk "BEGIN{printf \"%.4f\", 10^($CEIL/20)}")
  ffmpeg -y -v error -i "$TMP/$name.raw.wav" \
    -af "volume=${gain}dB,alimiter=limit=$lim:level=disabled:attack=1:release=40:latency=1,afade=t=out:st=$fade_s:d=$(awk "BEGIN{printf \"%.4f\", $keep_s-$fade_s}")" \
    -c:a pcm_s16le -ar $RATE "$OUT/$name.wav"

  printf '%-20s x%-5s %5.3fs  gain %6s dB  maxM %6s LUFS  peak %6s dBFS  onset %s s\n' \
    "$name" "$speed" "$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT/$name.wav")" "$gain" \
    "$(max_momentary "$OUT/$name.wav")" "$(peak_db "$OUT/$name.wav")" "$(onset_at "$OUT/$name.wav")"
}

#     name                  speed seed wet  lowpass keep fade  target
bake combat-shot            1.00  11   -13  0       1.40 0.80  -14
bake combat-shot_01         0.95  23   -13  0       1.40 0.80  -14
bake combat-shot_02         1.06  37   -13  0       1.40 0.80  -14
bake combat-shot-far        1.00  41   -3   1600    1.80 1.00  -20
bake combat-shot-far_01     0.96  53   -3   1600    1.80 1.00  -20

echo
echo "Re-import and rebuild the audio catalogue once design 47's P3 has added the rows:"
echo "  Odyssey > Presentation > Build audio catalogue (or scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build)"
