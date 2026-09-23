#!/usr/bin/env bash
#
# Bake the draft sound — a blade drawn — from the recording the owner supplied (2026-09-23):
# "use it when draft mode is clicked/actioned as an indicator" (docs/design/33-combat.md §2i).
#
# ## The source, measured
#
# dragon-studio-sword-unsheathing-393851.mp3: 1.10 s, 44.1 kHz stereo whose two channels measure
# identically (RMS -25.0 dBFS, peak -0.8 dBFS each), noise floor -83 dB. Pixabay, by Dragon
# Studio, under the Pixabay Content License: free for use in a product without attribution,
# modification allowed, not to be redistributed on its own. Committed as part of the game, which
# that licence allows — the owner to confirm, per docs/reference/audio-sourcing.md "Licensing".
#
# ## What is done to it, and why
#
# - **No denoise.** The floor is -83 dB; a denoiser would only take the air off the steel.
# - **The head is trimmed hard.** 108 ms of silence precede the ring. The sound confirms an order
#   the player has just given, and a tenth of a second of nothing after the key reads as lag.
# - **The tail is trimmed gently**, at -50 dB with a 25 ms fade: the ring of the blade after the
#   draw IS the sound, and cutting it where it stops registering leaves a click.
# - **Mono.** The channels are identical, and the sound is 2D (an indicator, not a thing at a
#   place), so the second channel would be 50 KB of the first.
# - **Levelled to a -3 dBFS peak ceiling, RMS target -20** — the carry bake's rule, whichever
#   binds first. The source is peaky (22 dB crest), so the ceiling binds: -5.2 dB, because the
#   mono downmix of two identical channels peaks 2.2 dB above the stereo file. The rest of the mix
#   is the catalogue's Volume.
#
# Usage:  tools/audio/bake_draft.sh [source-directory]
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

SRC="${1:-$HOME/Downloads}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"
IN="$SRC/dragon-studio-sword-unsheathing-393851.mp3"

RMS=-20
CEIL=-3.0
RATE=44100

[ -f "$IN" ] || { echo "missing: $IN" >&2; exit 1; }

TRIM="silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.005:detection=peak"
TRIM="$TRIM,areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.03:detection=rms:window=0.02,areverse"

dst="$OUT/draft.wav"
raw="$OUT/.draft.raw.wav"

# Pass one writes the file and pass two levels it, measuring the bytes that will ship (the carry
# bake found the null muxer's downmix three and a half decibels away from the WAV's).
ffmpeg -y -v error -i "$IN" -af "$TRIM" -ac 1 -c:a pcm_f32le -ar $RATE "$raw"

# Measured with astats, not volumedetect: on this source the mono downmix peaks ABOVE full scale
# in the float intermediate (+2.2 dB), and volumedetect reports a float peak clamped at 0 dB — so
# the ceiling was computed against 0, the gain came out 2.2 dB short, and the shipped file peaked
# at -0.8 dBFS instead of -3. The carry bake never met it because its source is 28 dB down.
stats=$(ffmpeg -i "$raw" -af astats=measure_perchannel=none -f null /dev/null 2>&1)
mean=$(echo "$stats" | grep -o "RMS level dB: [-0-9.]*" | tail -1 | grep -o "[-0-9.]*$")
peak=$(echo "$stats" | grep -o "Peak level dB: [-0-9.]*" | tail -1 | grep -o "[-0-9.]*$")
gain=$(awk "BEGIN{r=$RMS-($mean); c=$CEIL-($peak); printf \"%.2f\", (r<c?r:c)}")

d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$raw")
ffmpeg -y -v error -i "$raw" \
  -af "volume=${gain}dB,afade=t=in:st=0:d=0.003,afade=t=out:st=$(awk "BEGIN{printf \"%.4f\", $d-0.025}"):d=0.025" \
  -c:a pcm_s16le -ar $RATE "$dst"
rm -f "$raw"

printf 'draft  %5.2fs  %7sB  gain %6s dB  ' "$d" "$(stat -c%s "$dst")" "$gain"
ffmpeg -i "$dst" -af volumedetect -f null /dev/null 2>&1 \
  | grep -oE "(mean|max)_volume: [-0-9.]+ dB" | tr -s ' \n' ' '
echo
echo
echo "Re-import and rebuild the catalogue:"
echo "  scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build"
