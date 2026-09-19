#!/usr/bin/env bash
#
# Bake the title-screen bed: the loop that plays behind the main screen, the load list and the
# world-setup page, and stops the moment a world exists.
#
# ## What the source is
#
# A deep drone, and deeper than it looks. Measured by band:
#
#      20- 120 Hz   -26.6 dB      <- almost all of it
#     120- 500 Hz   -38.4 dB
#     500-2000 Hz   -62.3 dB
#    2000-6000 Hz   -85.5 dB      <- effectively silence
#
# Two consequences. It reads as -27.6 LUFS integrated, which sounds very quiet on paper and is
# mostly K-weighting doing what it does to sub-bass; its actual peak is -11.4 dBFS. And it will
# behave completely differently on laptop speakers (nearly inaudible) from headphones or anything
# with a woofer (a presence you feel). That is worth knowing before anybody retunes the level.
#
# It is also genuinely wide — sum and difference are within 2.4 dB of each other, so the two
# channels are close to decorrelated. **Do not fold it to mono**; the width is most of what makes
# a bed read as everywhere rather than as over there, and here it is nearly all there is.
#
# ## What the bake does
#
# **Makes it loop.** The whole 157 s is kept (the owner is happy to use all of it), and the last
# six seconds are blended into the first six, after which the blended tail is dropped. The loop
# then shortens by the blend and is seamless, which is the property that matters — the same thing
# `AudioSetup.CrossfadeTail` does for the synthesised beds. A bed plays for as long as somebody
# sits on the title screen, so a click or a swell at the loop point becomes a metronome.
#
# **Levels it to the peak the other beds use.** `AudioSetup.BedPeak` is 0.5, i.e. -6 dBFS, and
# every bed in the game is normalised to it so that catalogue Volume means the same thing across
# all of them. Peak and not loudness, because R128 has strong opinions about sub-bass that have
# nothing to do with how loud this will seem.
#
# **Leaves the sample rate alone.** The source is 24 kHz and has nothing above 500 Hz worth
# keeping; resampling to 44.1 would add eight megabytes of nothing to the repository.
#
# Usage:  tools/audio/bake_menu_bed.sh [source-directory]
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

SRC="${1:-$HOME/Downloads}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"
IN="$SRC/freesound_community-deep-space-ambiance-48854.mp3"
DST="$OUT/menu-bed.wav"

BLEND=6.0        # seconds of tail folded into the head
PEAK=-6.0        # dBFS, matching AudioSetup.BedPeak

[ -f "$IN" ] || { echo "missing: $IN" >&2; exit 1; }

DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$IN")
END=$(awk "BEGIN{printf \"%.4f\", $DUR-$BLEND}")

# head(0..BLEND) faded in, mixed with tail(END..DUR) faded out, then the body(BLEND..END).
# amix with normalize=0 so the two halves of a crossfade sum rather than each losing 6 dB.
LOOP="[0:a]atrim=0:$BLEND,asetpts=PTS-STARTPTS,afade=t=in:st=0:d=$BLEND[h];"
LOOP="$LOOP[0:a]atrim=$END:$DUR,asetpts=PTS-STARTPTS,afade=t=out:st=0:d=$BLEND[t];"
LOOP="$LOOP[h][t]amix=inputs=2:normalize=0[join];"
LOOP="$LOOP[0:a]atrim=$BLEND:$END,asetpts=PTS-STARTPTS[body];"
LOOP="$LOOP[join][body]concat=n=2:v=0:a=1[loop]"

TMP="$OUT/.menu-bed.tmp.wav"
ffmpeg -y -v error -i "$IN" -filter_complex "$LOOP" -map "[loop]" -c:a pcm_s16le "$TMP"

# Peak-normalise. No fades at the ends: this file's ends are the loop point, and a fade there is
# exactly the gap the blend above exists to remove.
PK=$(ffmpeg -i "$TMP" -af volumedetect -f null /dev/null 2>&1 \
     | grep -o "max_volume: [-0-9.]*" | grep -o "[-0-9.]*$")
GAIN=$(awk "BEGIN{printf \"%.2f\", $PEAK-($PK)}")
ffmpeg -y -v error -i "$TMP" -af "volume=${GAIN}dB" -c:a pcm_s16le "$DST"
rm -f "$TMP"

printf 'menu-bed        %6.2fs (from %6.2fs)  %sch %s Hz  %9sB  gain %s dB  ' \
  "$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$DST")" "$DUR" \
  "$(ffprobe -v error -show_entries stream=channels -of csv=p=0 "$DST")" \
  "$(ffprobe -v error -show_entries stream=sample_rate -of csv=p=0 "$DST")" \
  "$(stat -c%s "$DST")" "$GAIN"
ffmpeg -i "$DST" -af volumedetect -f null /dev/null 2>&1 \
  | grep -oE "(mean|max)_volume: [-0-9.]+ dB" | tr -s ' \n' ' '
echo

echo
echo "Re-import and rebuild the catalogue:"
echo "  scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build"
