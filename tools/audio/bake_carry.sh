#!/usr/bin/env bash
#
# Bake the two carry sounds — a load coming up off the ground, and a load going back down —
# from the one recording the owner supplied.
#
# ## Why one recording becomes two sounds
#
# There is one source: a soft mid-range scuff, most of its energy between 250 and 800 Hz with a
# second lobe up to 2.5 kHz, about half a second long. It is the sound of material being handled.
# Played unchanged at both ends of a carry it would say "something happened" twice and never say
# which — and which is the whole of what a player needs to hear from a hauler they are not
# watching.
#
# So each end is **resampled rather than pitch-shifted**, which changes pitch and length together:
#
#   lift  x1.14  — up about two and a half semitones, an eighth shorter. Lighter, quicker, and
#                  brighter for the high shelf. A thing leaving the ground.
#   drop  x0.82  — down about three and a half semitones, a fifth longer. Heavier, slower, and
#                  duller for the low-pass. A thing arriving on it.
#
# Resampling is the right transform here precisely because it is not a clean pitch shift: a bigger,
# heavier object really does sound both lower AND longer, so the artefact is the effect. A
# formant-preserving shift would give two sounds of the same size at different pitches, which
# reads as one sample played twice.
#
# ## Why three takes of each
#
# This plays on every leg of every haul — after footsteps, which do not exist yet, it will be the
# most repeated sound in the game. One sample is recognisable as a sample within three or four
# plays. Three takes at spread rates, times the catalogue's own per-play pitch jitter, is enough
# spectral spread that the ear stops matching them.
#
# ## Processing notes
#
# **Gain first, then denoise, then trim — the order matters and only this one works.** The source
# is very quiet (-44.6 LUFS, peaks at -28 dBFS), quiet enough that its whole signal sits below the
# level a denoiser takes for noise and below the level a silence trim takes for silence: run
# either on the raw file and the clip comes out empty. So it is lifted 24 dB into a normal working
# range first, and the two cleaners then see the levels they are written for.
#
# **Levelling is RMS against a peak ceiling, not EBU R128.** The alert chimes are baked with
# two-pass loudnorm and the obvious thing was to reuse it. It cannot be used: R128's integrated
# loudness is gated in 400 ms blocks, these clips are under half a second, and loudnorm duly
# reports -inf and refuses the second pass. Peak-ceilinged RMS is what a one-shot wants anyway —
# what matters about an impact is how hard it hits, not how loud it is over time.
#
# The target is -20 dBFS RMS, under the alert chimes. This sound is meant to sit *in* the
# environment rather than on top of it; the rest of that job is the catalogue's Volume and range.
#
# Usage:  tools/audio/bake_carry.sh [source-directory]
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

SRC="${1:-$HOME/Downloads}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"
IN="$SRC/item-pick.mp3"

RMS=-20          # target mean level, dBFS — under the alerts on purpose
CEIL=-3.0        # peak ceiling, dBFS. Whichever of the two binds first wins.
RATE=44100

[ -f "$IN" ] || { echo "missing: $IN" >&2; exit 1; }

CLEAN="volume=24dB,afftdn=nr=12:nf=-45"

# The tail is trimmed at -50 dB rather than the alerts' -58: the decay of a scuff IS the sound,
# and cutting it where it stops registering leaves a click where a settle should be.
TRIM="silenceremove=start_periods=1:start_threshold=-35dB:start_silence=0.01:detection=rms:window=0.02"
TRIM="$TRIM,areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.03:detection=rms:window=0.02,areverse"

# A carried thing is somewhere, so these are mono: a stereo file cannot be placed, and Unity
# flattens it better if it was flattened here. (The alert chimes are the opposite case — they are
# 2D and keep their width. See tools/audio/bake_alerts.sh.)
bake () {  # bake <clip-name> <rate-multiplier> <colour-filter>
  local dst="$OUT/$1.wav" raw="$OUT/.$1.raw.wav" rate="$2" colour="$3"
  local shifted
  shifted=$(awk "BEGIN{printf \"%d\", $RATE*$rate}")
  local chain="$CLEAN,$TRIM,aresample=$RATE,asetrate=$shifted,aresample=$RATE,$colour"

  # **Pass one writes the file; pass two levels it.** Measuring the filter chain through
  # `-f null` instead would be a pass cheaper and is wrong: the stereo-to-mono downmix lands
  # differently on the null muxer than on a WAV, by three and a half decibels, so the gain
  # computed from the measurement overshot into clipping and every clip came out pinned at 0 dBFS.
  # Measure the bytes that will ship.
  ffmpeg -y -v error -i "$IN" -af "$chain" -ac 1 -c:a pcm_f32le -ar $RATE "$raw"

  local stats mean peak gain d
  stats=$(ffmpeg -i "$raw" -af volumedetect -f null /dev/null 2>&1)
  mean=$(echo "$stats" | grep -o "mean_volume: [-0-9.]*" | grep -o "[-0-9.]*$")
  peak=$(echo "$stats" | grep -o "max_volume: [-0-9.]*"  | grep -o "[-0-9.]*$")
  gain=$(awk "BEGIN{r=$RMS-($mean); c=$CEIL-($peak); printf \"%.2f\", (r<c?r:c)}")

  d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$raw")
  ffmpeg -y -v error -i "$raw" \n    -af "volume=${gain}dB,afade=t=in:st=0:d=0.004,afade=t=out:st=$(awk "BEGIN{printf \"%.4f\", $d-0.012}"):d=0.012" \n    -c:a pcm_s16le -ar $RATE "$dst"
  rm -f "$raw"

  printf '%-15s x%-5s %5.2fs %7sB  gain %6s dB  ' "$1" "$rate" "$d" "$(stat -c%s "$dst")" "$gain"
  ffmpeg -i "$dst" -af volumedetect -f null /dev/null 2>&1 \n    | grep -oE "(mean|max)_volume: [-0-9.]+ dB" | tr -s ' 
' ' '
  echo
}

# Lifting: brighter. The high shelf is what stops "faster and higher" reading as a chipmunk take
# of the drop; the high-pass takes out body it has no business having on the way up.
LIFT="highpass=f=170:poles=2,treble=g=2.5:f=3000"

# Setting down: duller and with body. The low shelf is the weight and the low-pass is the floor
# absorbing the top end, which is what a thing landing on ground rather than on a table sounds
# like.
DROP="bass=g=3:f=220,lowpass=f=5500:poles=2"

bake carry-lift      1.14 "$LIFT"
bake carry-lift_01   1.10 "$LIFT"
bake carry-lift_02   1.19 "$LIFT"

bake carry-drop      0.82 "$DROP"
bake carry-drop_01   0.78 "$DROP"
bake carry-drop_02   0.87 "$DROP"

echo
echo "Re-import and rebuild the catalogue:"
echo "  scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build"
