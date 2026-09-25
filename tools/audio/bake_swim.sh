#!/usr/bin/env bash
#
# Bake the swim stroke — one arm pulling through water — from the recording the owner supplied
# (2026-09-25, docs/design/20-swimming-and-water.md §9). Played once per arm, each time a hand
# reaches forward into the water (`SwimPose.StrokeSoundsBetween`), at the swimmer, heard only
# with the camera close.
#
# ## The source, measured (mono downmix, 25 ms slices)
#
#   freesound_community-swim-44183.mp3   1.10 s stereo, 24 kHz, 160 kb/s. Silent to 0.05 s,
#       rises through 0.10-0.17 s, loudest 25 ms from 0.225 s (-15.8 dB), a wash to -30 dB by
#       0.45 s and -45 dB by 0.75 s, then a floor. Max momentary -20.0 LUFS, peak -2.1 dBFS.
#       One stroke: the catch and the pull, not a pair.
#
# Pixabay, under the Pixabay Content License (the `freesound_community` handle is Pixabay's
# import of Freesound material): free for use in a product without attribution, modification
# allowed, not to be redistributed on its own. Committed as part of the game, which that licence
# allows — the owner to confirm, per docs/reference/audio-sourcing.md "Licensing".
#
# ## What is done to it, and why
#
# - **The head is cut hard at 0.045 s**, just before the onset, with a 4 ms guard fade — a fixed
#   time rather than a silence threshold, because the offset of the loudest moment is a timing
#   constant (`SwimPose.StrokeSoundPeakSeconds`) and a threshold would move it.
# - **The tail is cut at 0.80 s with a fade from 0.55 s.** A stroke comes every 0.77 s
#   (`SwimPose.StrokesPerSecond` 0.65, two arms), so a longer tail only smears one stroke into the
#   next; by 0.75 s the source is 30 dB under its peak anyway.
# - **Three takes at 0.94, 1.00 and 1.06 speed**, the carry sounds' trick: the director picks one
#   at random with its own pitch variance on top, so a swimmer crossing a stream is not one sample
#   on a loop. Speed moves the peak by at most 11 ms, which nobody can hear against a hand.
# - **Mono**: it is placed at the swimmer.
# - **Levelled to -24 LUFS max momentary against a -3 dBFS peak ceiling** — 3 dB under the carry
#   sounds (-21 to -22), because it repeats for as long as somebody swims and a carry happens once.
#   Measured on the file that ships, with astats for the peak (volumedetect clamps float
#   intermediates; the draft bake's lesson).
#
# Usage: tools/audio/bake_swim.sh [path/to/freesound_community-swim-44183.mp3]

set -euo pipefail

IN="${1:-$HOME/Downloads/freesound_community-swim-44183.mp3}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"

TARGET=-24       # max momentary loudness, LUFS
CEIL=-3.0        # peak ceiling, dBFS. Whichever binds first wins.
RATE=44100
HEAD=0.045       # seconds cut from the front
KEEP=0.755       # seconds kept after the head (to 0.80 s of the source)
FADE_AT=0.505    # tail fade starts here, in the kept file (0.55 s of the source)

[ -f "$IN" ] || { echo "missing: $IN" >&2; exit 1; }

max_momentary () {  # the loudest 400 ms window, over a padded copy so a short file is measured whole
  ffmpeg -hide_banner -nostats -i "$1" -af "apad=pad_dur=0.5,ebur128" -f null - 2>&1 \
    | grep -oE "M: *-?[0-9.]+" | awk '{print $2}' | sort -g | tail -1
}

peak_db () {
  ffmpeg -hide_banner -i "$1" -af astats -f null - 2>&1 | grep "Peak level dB" | tail -1 | awk '{print $NF}'
}

bake () {  # bake <clip-name> <speed>
  local dst="$OUT/$1.wav" raw="$OUT/.$1.raw.wav" speed="$2" shifted
  shifted=$(awk "BEGIN{printf \"%d\", $RATE*$speed}")
  local keep fade
  keep=$(awk "BEGIN{printf \"%.4f\", $KEEP/$speed}")
  fade=$(awk "BEGIN{printf \"%.4f\", $FADE_AT/$speed}")

  ffmpeg -y -v error -i "$IN" \
    -af "atrim=start=$HEAD,asetpts=PTS-STARTPTS,aresample=$RATE,asetrate=$shifted,aresample=$RATE,atrim=end=$keep" \
    -ac 1 -c:a pcm_f32le -ar $RATE "$raw"

  local m p gain
  m=$(max_momentary "$raw")
  p=$(peak_db "$raw")
  gain=$(awk "BEGIN{a=$TARGET-($m); b=$CEIL-($p); printf \"%.2f\", (a<b?a:b)}")

  ffmpeg -y -v error -i "$raw" \
    -af "volume=${gain}dB,afade=t=in:st=0:d=0.004,afade=t=out:st=$fade:d=$(awk "BEGIN{printf \"%.4f\", $keep-$fade}")" \
    -c:a pcm_s16le -ar $RATE "$dst"
  rm -f "$raw"

  printf '%-16s x%-5s %5.3fs  gain %6s dB  maxM %6s LUFS  peak %7s dBFS\n' \
    "$1" "$speed" "$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$dst")" "$gain" \
    "$(max_momentary "$dst")" "$(peak_db "$dst")"
}

bake swim-stroke     1.00
bake swim-stroke_01  0.94
bake swim-stroke_02  1.06

echo
echo "Re-import and rebuild the audio catalogue:"
echo "  Odyssey > Presentation > Build audio catalogue (or scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build)"
