#!/usr/bin/env bash
#
# Bake the two demolition sounds from the recordings the owner supplied (2026-09-26,
# docs/design/57-cracks.md §9): "on the sound of the wood wall (or other wood based items) — on
# breaking (or deconstruct) — play this audio, and for mining when the rock has collapsed … process
# them as necessary and blend it into the environment".
#
#   break-wood   something built of wood coming down: broken in a fight, or taken apart
#   break-rock   a mined face collapsing, the last stroke of the pick
#
# Both are played once, from the cell, on the frame the thing goes (`DemolitionSounds`), which is
# the frame the break (§7) starts to shudder.
#
# ## The sources, measured (mono downmix, 48 kHz float decode, 10 ms slices)
#
#   floraphonic-wood-smash-3-170418.mp3        2.40 s stereo, 48 kHz, 256 kb/s. Silent to 0.04 s;
#       the smash is at full level by 0.05 s, a splintering body at -7 to -12 dB RMS to 0.6 s,
#       debris landing at 0.7 and 0.9 s, -35 dB by 1.4 s, silent from 1.8 s. Integrated
#       -13.0 LUFS, and sample peaks to +2.5 dBFS in the float decode: mastered into a wall.
#   dragon-studio-boulder-impact-487673.mp3    2.11 s stereo, 48 kHz, 256 kb/s. Onset at 0.02 s;
#       0.02-0.35 s is brick-walled — RMS at 0 dBFS and peaks at +3.3 — a rumble to 0.6 s, a
#       second fall at 0.8 s, -40 dB by 1.3 s. Integrated -10.0 LUFS.
#
# Both are Pixabay, under the Pixabay Content License: free for use in a product without
# attribution, modification allowed, not to be redistributed on its own. Committed as part of the
# game, which that licence allows — the owner to confirm, per docs/reference/audio-sourcing.md.
#
# ## What is done to them, and why
#
# - **Float until the last step, and -12 dB first**: both decodes are over full scale, and an
#   integer step before the gain would clip them a second time. The gunshot bake's lesson.
# - **Head cut** just before each onset (0.035 s and 0.012 s) with a 2 ms guard fade, so the sound
#   starts on the frame the thing goes rather than a breath after it.
# - **Tone.** High-passed (60 Hz wood, 35 Hz rock — a boulder keeps its weight, but not the sub
#   rumble a laptop speaker turns into a flap) and low-passed (9 kHz wood, 5.5 kHz rock). The low
#   pass is doing two jobs: air takes the top off anything heard from the play camera's 48 m, and it
#   rounds the squared-off tops the brick-walled masters left, which is most of their harshness.
# - **Blended into the environment**, the gunshot's recipe: each take is the dry sound plus a
#   diffuse **outdoor space** — pink noise decaying at 5.5 nepers a second, band-limited 180 Hz to
#   3.2 kHz, 22 ms pre-delay — and one soft **slapback at 140 ms**, a treeline or a terrace riser
#   ~24 m off. Wetter than the gunshot's near set (-11 dB against -13): a collapse is a slower,
#   rounder sound than a report and reads as belonging to the meadow with more space round it.
#   No room: a wall broken indoors is a later seam, as it is for the gunshot.
# - **Three takes each** at 1.00 / 0.94 / 1.06 speed, each with its own impulse response (a
#   different noise seed), so a miner clearing a gallery is not one sample on repeat. The director
#   adds its own pitch and volume variance on top.
# - **Mono**: it is placed at the cell.
# - **Tails** cut at 1.70 s (wood) and 1.60 s (rock) with a fade from 1.20 / 1.05 s: the break's
#   pieces are gone into the ground by 1.6 s, and the sound should not outlast the picture.
# - **Levelled on loudness into a lookahead limiter at -3 dBFS**, max momentary -19 LUFS for both:
#   5 dB over the pick (-24.0, measured) that leads up to a collapse, so the face coming down is
#   the payoff of the strokes before it; level with the melee thud (-18.5); 5 dB under the gunshot
#   (-14). **Imported with `normalize` off**, or Unity would undo the levelling.
#
# Usage: tools/audio/bake_demolition.sh [wood.mp3] [boulder.mp3]

set -euo pipefail

WOOD="${1:-$HOME/Downloads/floraphonic-wood-smash-3-170418.mp3}"
ROCK="${2:-$HOME/Downloads/dragon-studio-boulder-impact-487673.mp3}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

CEIL=-3.0        # peak ceiling, dBFS
RATE=44100

[ -f "$WOOD" ] || { echo "missing: $WOOD" >&2; exit 1; }
[ -f "$ROCK" ] || { echo "missing: $ROCK" >&2; exit 1; }

max_momentary () {  # the loudest 400 ms window, over a padded copy so a short file is measured whole
  ffmpeg -hide_banner -nostats -i "$1" -af "apad=pad_dur=0.5,ebur128" -f null - 2>&1 \
    | grep -oE "M: *-?[0-9.]+" | awk '{print $2}' | sort -g | tail -1
}

peak_db () {
  ffmpeg -hide_banner -i "$1" -af astats -f null - 2>&1 | grep "Peak level dB" | tail -1 | awk '{print $NF}'
}

onset_at () {  # when the sound first rises above -30 dBFS
  ffmpeg -hide_banner -nostats -i "$1" -af "silencedetect=n=-30dB:d=0.001" -f null - 2>&1 \
    | grep -oE "silence_end: *[0-9.]+" | head -1 | awk '{printf "%.3f", $2}'
}

dry () {  # dry <source> <out> <head-s> <highpass-Hz> <lowpass-Hz>
  ffmpeg -y -v error -i "$1" \
    -af "volume=-12dB,aresample=$RATE,atrim=start=$3,asetpts=PTS-STARTPTS,highpass=f=$4:p=2,lowpass=f=$5:p=2,afade=t=in:st=0:d=0.002" \
    -ac 1 -c:a pcm_f32le -ar $RATE "$2"
}

impulse () {  # impulse <out> <seed> <seconds>: a diffuse outdoor space
  ffmpeg -y -v error -f lavfi -i "anoisesrc=d=$3:c=pink:r=$RATE:a=0.5:seed=$2" \
    -af "volume='exp(-5.5*t)':eval=frame,highpass=f=180,lowpass=f=3200,afade=t=in:d=0.012,adelay=22,apad=pad_dur=0" \
    -ac 1 -c:a pcm_f32le "$1"
}

bake () {  # bake <dry.wav> <clip-name> <speed> <seed> <wet-dB> <keep-s> <fade-from-s> <target-LUFS>
  local src="$1" name="$2" speed="$3" seed="$4" wet="$5" keep="$6" fadeat="$7" target="$8"
  local shifted; shifted=$(awk "BEGIN{printf \"%d\", $RATE*$speed}")

  impulse "$TMP/ir-$name.wav" "$seed" 1.6

  ffmpeg -y -v error -i "$src" -i "$TMP/ir-$name.wav" -filter_complex "
    [0]asetrate=$shifted,aresample=$RATE,apad=pad_dur=2,asplit=3[d][r][s];
    [r][1]afir=dry=10:wet=10:irnorm=1[space0];
    [space0]volume=${wet}dB[space];
    [s]adelay=140,lowpass=f=1800,volume=$(awk "BEGIN{printf \"%.1f\", $wet-6}")dB[slap];
    [d][space][slap]amix=inputs=3:normalize=0,atrim=end=$keep[out]" \
    -map "[out]" -c:a pcm_f32le -ar $RATE "$TMP/$name.raw.wav"

  local m gain lim
  m=$(max_momentary "$TMP/$name.raw.wav")
  gain=$(awk "BEGIN{printf \"%.2f\", $target-($m)}")
  lim=$(awk "BEGIN{printf \"%.4f\", 10^($CEIL/20)}")

  ffmpeg -y -v error -i "$TMP/$name.raw.wav" \
    -af "volume=${gain}dB,alimiter=limit=$lim:level=disabled:attack=1:release=40:latency=1,afade=t=out:st=$fadeat:d=$(awk "BEGIN{printf \"%.4f\", $keep-$fadeat}")" \
    -c:a pcm_s16le -ar $RATE "$OUT/$name.wav"

  printf '%-14s x%-5s %5.3fs  gain %6s dB  maxM %6s LUFS  peak %6s dBFS  onset %s s\n' \
    "$name" "$speed" "$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT/$name.wav")" "$gain" \
    "$(max_momentary "$OUT/$name.wav")" "$(peak_db "$OUT/$name.wav")" "$(onset_at "$OUT/$name.wav")"
}

#   source   out               head   highpass lowpass
dry "$WOOD" "$TMP/wood.wav"    0.035  60       9000
dry "$ROCK" "$TMP/rock.wav"    0.012  35       5500

#    dry             name            speed seed wet keep fade target
bake "$TMP/wood.wav" break-wood      1.00  61   -11 1.70 1.20 -19
bake "$TMP/wood.wav" break-wood_01   0.94  73   -11 1.70 1.20 -19
bake "$TMP/wood.wav" break-wood_02   1.06  89   -11 1.70 1.20 -19
bake "$TMP/rock.wav" break-rock      1.00  97   -11 1.60 1.05 -19
bake "$TMP/rock.wav" break-rock_01   0.94  101  -11 1.60 1.05 -19
bake "$TMP/rock.wav" break-rock_02   1.06  113  -11 1.60 1.05 -19

echo
echo "Re-import and rebuild the audio catalogue:"
echo "  Odyssey > Presentation > Build audio catalogue (or scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build)"
