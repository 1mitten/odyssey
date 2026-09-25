#!/usr/bin/env bash
#
# Bake the two rain beds (design 43 §7): light rain and heavy rain, each a seamless stereo loop,
# levelled so that the mix in AudioDirector (RainMix) is what decides how loud rain is, not the
# accident of how each was recorded.
#
# ## What the sources are (measured 2026-09-25, owner-supplied, Pixabay)
#
#   dragon-studio-gentle-rain-07-437321.mp3   90.0 s, 48 kHz stereo, -28.2 LUFS, LRA 7.9 LU
#   boons_freak-rain-sound-188158.mp3          91.1 s, 44.1 kHz stereo, -20.6 LUFS, LRA 2.4 LU
#
# **The gentle rain is only steady for its first ~46 s.** Per second, its RMS sits between -34
# and -37 dB until 46 s and then falls steadily, about 7 dB by 80 s and more into the last second:
# the recording fades out over its second half. A loop that took the whole file would swell by
# 7 dB every forty seconds, which is exactly the metronome a bed must not be. So the window is
# 0.5-47.0 s. It is drips over a hiss — a crest of 20-28 dB a second — which is what makes it read
# as light rain, and the levelling below is careful not to flatten it.
#
# **The heavy rain is steady almost end to end.** RMS -27 to -30 dB from 1.5 s to 88.5 s, a slow
# 2 dB rise and fall across the middle, and short-term loudness at the two ends of the window that
# agree to 0.2 LU (-21.5 against -21.7). The first second fades in and the last two fade out;
# both are cut. Window 1.5-88.5 s.
#
# ## What the bake does
#
# **Levels both to the same loudness, not the same peak.** Every other bed is peak-normalised
# (AudioSetup.BedPeak, -6 dBFS). That is wrong for these two, and measurably: the gentle rain's
# crest is ~8 dB higher than the heavy rain's, so at a shared peak it would sit 7-8 LU quieter,
# and the crossfade from light to heavy would be a 7 dB jump before the mix did anything. Both
# are brought to -23 LUFS integrated, so equal-power weights in RainMix are equal loudness, and a
# peak limiter at -3 dBFS (the sourcing doc's ceiling) catches the few drips the gain pushes
# over. Measured after the bake, the limiter touches only transients; see the printout.
#
# **Levels before looping, so the limiter never sees the seam.** A limiter has state (lookahead
# and release), and running it over a file whose last sample continues into its first would
# treat the wrap as a cut. The window is gained and limited first, then folded.
#
# **Loops by folding its own tail over its head, equal-power.** Rain is noise, and two
# uncorrelated noises crossfaded linearly dip 3 dB in the middle — an audible breath at every
# wrap. The fades are quarter-sine (afade curve=qsin), whose powers sum to one. Four seconds for
# the light bed, five for the heavy one; the blended tail is then dropped, so the file's last
# sample runs straight into its first. The printout measures the step at the wrap against the
# ordinary sample-to-sample step, the check campfire.wav was held to.
#
# **Keeps stereo and the source rate.** A rain bed is 2D — the air, not a thing in it — and the
# width is most of what makes it read as all around. Resampling would add nothing.
#
# Usage:  tools/audio/bake_rain.sh [source-directory]
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

SRC="${1:-$HOME/Downloads}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"

TARGET=-23.0     # LUFS integrated, both beds
CEILING=0.708    # linear, -3 dBFS: the peak limiter's ceiling

bake() {
  local name="$1" in="$2" from="$3" to="$4" blend="$5"
  local dst="$OUT/$name.wav" lev="$OUT/.$name.level.tmp.wav"
  [ -f "$in" ] || { echo "missing: $in" >&2; exit 1; }

  # 1. The window, measured, gained to the target and limited.
  local lufs gain
  lufs=$(ffmpeg -hide_banner -nostats -ss "$from" -to "$to" -i "$in" -af ebur128 -f null - 2>&1 \
         | grep -A3 "Integrated loudness" | grep -oE "I: +-?[0-9.]+" | grep -oE -e "-?[0-9.]+$")
  gain=$(awk "BEGIN{printf \"%.2f\", $TARGET-($lufs)}")
  ffmpeg -y -v error -ss "$from" -to "$to" -i "$in" \
    -af "volume=${gain}dB,alimiter=limit=$CEILING:attack=5:release=50:level=disabled" \
    -c:a pcm_s16le "$lev"

  # 2. Fold: head faded in, tail faded out, both quarter-sine, summed; then the body.
  local dur end
  dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$lev")
  end=$(awk "BEGIN{printf \"%.4f\", $dur-$blend}")
  local loop="[0:a]atrim=0:$blend,asetpts=PTS-STARTPTS,afade=t=in:st=0:d=$blend:curve=qsin[h];"
  loop="$loop[0:a]atrim=$end:$dur,asetpts=PTS-STARTPTS,afade=t=out:st=0:d=$blend:curve=qsin[t];"
  loop="$loop[h][t]amix=inputs=2:normalize=0[join];"
  loop="$loop[0:a]atrim=$blend:$end,asetpts=PTS-STARTPTS[body];"
  loop="$loop[join][body]concat=n=2:v=0:a=1[loop]"
  ffmpeg -y -v error -i "$lev" -filter_complex "$loop" -map "[loop]" -c:a pcm_s16le "$dst"
  rm -f "$lev"

  printf '%-11s %6.2fs (window %s-%s s of %s, blend %ss)  %sch %s Hz  source %s LUFS, gain %s dB\n' \
    "$name" "$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$dst")" "$from" "$to" \
    "$(basename "$in")" "$blend" \
    "$(ffprobe -v error -show_entries stream=channels -of csv=p=0 "$dst")" \
    "$(ffprobe -v error -show_entries stream=sample_rate -of csv=p=0 "$dst")" "$lufs" "$gain"
  printf '            baked: '
  ffmpeg -hide_banner -nostats -i "$dst" -af ebur128=peak=sample -f null - 2>&1 \
    | grep -A14 Summary | grep -E "^\s+(I|LRA|Peak):" | tr -s ' \n' ' '
  echo
}

bake rain-light "$SRC/dragon-studio-gentle-rain-07-437321.mp3" 0.5 47.0 4.0
bake rain-heavy "$SRC/boons_freak-rain-sound-188158.mp3"      1.5 88.5 5.0

echo
echo "Check the seams:  python3 tools/audio/loop_seam.py $OUT/rain-light.wav $OUT/rain-heavy.wav"
echo "Re-import and rebuild the catalogue:"
echo "  scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build"
