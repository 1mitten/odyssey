#!/usr/bin/env bash
#
# Bake alert chimes from source recordings into the project's clip folder.
#
# The five files the owner supplied were between -14.5 and -24.5 LUFS — a ten-decibel spread,
# which is the difference between a chime that startles and one that is missed entirely. They
# also carried dead air: half a second of it at the head of the raid siren, which is half a
# second of nothing before an alarm. So the bake does three things and nothing else:
#
#   1. trims silence off both ends, so a chime starts when it is played;
#   2. matches every clip to one integrated loudness (EBU R128), so the mix in the catalogue is
#      about how important a sound is rather than how it happened to be recorded;
#   3. writes 44.1 kHz 16-bit PCM WAV with 6 ms guard fades over the cuts.
#
# It does NOT compress, EQ or shorten anything musical. If a clip wants to be shorter, that is a
# decision about the sound and belongs in a conversation, not in this script.
#
# Channel count is left alone: mono sources stay mono, stereo stays stereo. Do not add a
# downmix here — Unity's importer peak-normalises when forceToMono is set, which would undo (2).
#
# Usage:  tools/audio/bake_alerts.sh [source-directory]
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

SRC="${1:-$HOME/Downloads}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"

I=-18            # integrated loudness target, LUFS
TP=-1.5          # true-peak ceiling, dBTP
LRA=11

# -45 dB at the head and -50 dB at the tail. Every source starts above -30 dB, so nothing
# musical is at risk, and the raid siren's silent lead-in is well under both.
TRIM="silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.02:detection=rms:window=0.03"
TRIM="$TRIM,areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.05:detection=rms:window=0.03,areverse"

bake () {  # bake <source-stem> <clip-name>
  local src="$SRC/notification-$1.mp3" dst="$OUT/$2.wav" tmp="$OUT/.$2.tmp.wav"
  [ -f "$src" ] || { echo "missing: $src" >&2; return 1; }

  # Two-pass loudnorm: pass one measures the trimmed signal, pass two applies it. One pass
  # guesses, and guessing is what the ten-decibel spread already was.
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

  printf '%-16s %5.2fs %sch %8sB  ' "$2" "$d" \
    "$(ffprobe -v error -show_entries stream=channels -of csv=p=0 "$dst")" \
    "$(stat -c%s "$dst")"
  ffmpeg -i "$dst" -af ebur128=peak=true -f null /dev/null 2>&1 \
    | sed -n '/Summary/,$p' | grep -E "I: |Peak:" | tr -s ' \n' ' '
  echo
}

bake normal         alert-normal
bake negative       alert-negative
bake happy          alert-happy
bake colonist-joins alert-joined
bake raid           alert-raid

echo
echo "Re-import and rebuild the catalogue:"
echo "  scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build"
