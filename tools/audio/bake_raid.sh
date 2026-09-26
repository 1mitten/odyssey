#!/usr/bin/env bash
#
# Bake the raid's arrival horn from the recording the owner supplied (2026-09-25,
# docs/design/53-raids.md §7): played when a raid ARRIVES at the edge of the board. An alert, 2D,
# on the same bus and at the same loudness as the chimes `bake_alerts.sh` makes, so the mix says
# how important a sound is rather than how it was recorded.
#
# The owner supplied a second recording for the ASSAULT, `trading_nation-low-horn-185556.mp3`.
# **It is already in the game**: baked through this same chain it is sample-for-sample
# `alert-raid.wav` (correlation 1.0000, 9.54 s, -18.6 LUFS both), which `bake_alerts.sh` makes
# from the owner's `notification-raid.mp3` — the same file under another name. So the assault
# plays `alert-raid` through the `ui.alert.raid` override that was wired on 2026-09-19, this
# script does not write it (one file, one owner), and the siren's source and licence are now
# known.
#
# ## The source, measured
#
#   freesound_community-war-horn-horror-73771.mp3   18.07 s stereo, 24 kHz, 160 kb/s.
#       -20.9 LUFS integrated, LRA 4.7, peak -4.6 dBFS. The arrival: a long, low war horn.
#
# Pixabay, under the Pixabay Content License (`freesound_community` is Pixabay's import of
# Freesound material): free for use in a product without attribution, modification allowed, not
# to be redistributed on its own. Committed as part of the game, which that licence allows — the
# owner to confirm, per docs/reference/audio-sourcing.md "Licensing". The source is NOT
# committed; only what this script writes is.
#
# ## What is done to it, and why — the alert bake, unchanged
#
#   1. silence trimmed off both ends, so a horn sounds when it is played;
#   2. two-pass EBU R128 to -18 LUFS integrated, -1.5 dBTP, the alerts' target;
#   3. 44.1 kHz 16-bit PCM WAV with 6 ms / 8 ms guard fades over the cuts.
#
# Nothing musical is shortened. The war horn is 18 s, nearly twice the old siren's 9.5 s; whether
# that is too long for an alert is a question for a person at the keyboard (design 53 §7), not
# for this script. Stereo stays stereo — see bake_alerts.sh on forceToMono.
#
# Usage:  tools/audio/bake_raid.sh [source-directory]
#         The directory holds the .mp3 under its Pixabay download name; a prefix
#         before the name (an upload id) is allowed.
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

SRC="${1:-$HOME/Downloads}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"

I=-18
TP=-1.5
LRA=11

TRIM="silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.02:detection=rms:window=0.03"
TRIM="$TRIM,areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.05:detection=rms:window=0.03,areverse"

find_source () {  # find_source <download-name-stem>
  local hit
  hit=$(find "$SRC" -maxdepth 1 -type f -name "*$1.mp3" | head -n 1)
  [ -n "$hit" ] || { echo "missing: $SRC/*$1.mp3" >&2; return 1; }
  echo "$hit"
}

bake () {  # bake <download-name-stem> <clip-name>
  local src dst="$OUT/$2.wav" tmp="$OUT/.$2.tmp.wav"
  src=$(find_source "$1")

  local m mi mtp mlra mthr d
  m=$(ffmpeg -i "$src" -af "$TRIM,loudnorm=I=$I:TP=$TP:LRA=$LRA:print_format=json" -f null /dev/null 2>&1 | sed -n '/^{/,/^}/p')
  mi=$(echo   "$m" | grep input_i      | sed 's/.*: "//;s/".*//')
  mtp=$(echo  "$m" | grep input_tp     | sed 's/.*: "//;s/".*//')
  mlra=$(echo "$m" | grep input_lra    | sed 's/.*: "//;s/".*//')
  mthr=$(echo "$m" | grep input_thresh | sed 's/.*: "//;s/".*//')

  ffmpeg -y -v error -i "$src" \
    -af "$TRIM,loudnorm=I=$I:TP=$TP:LRA=$LRA:measured_I=$mi:measured_TP=$mtp:measured_LRA=$mlra:measured_thresh=$mthr:linear=false,afade=t=in:st=0:d=0.006,aresample=44100" \
    -c:a pcm_s16le -ar 44100 "$tmp"

  d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$tmp")
  ffmpeg -y -v error -i "$tmp" \
    -af "afade=t=out:st=$(awk "BEGIN{printf \"%.4f\", $d-0.008}"):d=0.008" -c:a pcm_s16le "$dst"
  rm -f "$tmp"

  printf '%-20s %5.2fs %sch %8sB  ' "$2" "$d" \
    "$(ffprobe -v error -show_entries stream=channels -of csv=p=0 "$dst")" \
    "$(stat -c%s "$dst")"
  ffmpeg -i "$dst" -af ebur128=peak=true -f null /dev/null 2>&1 \
    | sed -n '/Summary/,$p' | grep -E "I: |Peak:" | tr -s ' \n' ' '
  echo
}

bake freesound_community-war-horn-horror-73771 alert-raid-arrive

echo
echo "Re-import and rebuild the catalogue:"
echo "  scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build"
