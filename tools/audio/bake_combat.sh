#!/usr/bin/env bash
#
# Bake the sound of a blow — two whooshes, the critical slice and the thud — from the four
# recordings the owner supplied (2026-09-23, docs/design/33-combat.md §9g). "This all needs to be
# coordinated at the right times for effect": so every file is cut to put its loudest moment at a
# known, small offset from its first sample, and the offsets below are what
# Odyssey.Hud.CombatSoundTiming schedules against. **Re-bake, re-measure, and change the constants
# there in the same commit** — a file whose peak moves by 30 ms moves the whoosh on to the thud.
#
# ## The sources, measured (mono downmix, 44.1 kHz)
#
#   dragon-studio-violent-sword-slice-393848.mp3   2.19 s stereo. Onset (-20 dB rel) 0.076 s, the
#       loudest 10 ms from 0.116 s, a second hump at 0.28-0.32 s, then a ring that is -40 dB by
#       0.86 s and floor by 1.4 s. Downmix peak +2.9 dBFS (a float intermediate: the MP3 decode
#       overshoots and -ac 1 sums the two channels at 0.707 each).
#   floraphonic-swing-whoosh-4-198496.mp3          0.26 s, identical channels. Onset 0.044 s,
#       loudest 10 ms from 0.077 s, a plateau 0.06-0.10 s, silent by 0.22 s. Peak +2.5 dBFS.
#   floraphonic-swing-whoosh-3-198495.mp3          0.24 s, identical channels. Onset 0.047 s,
#       loudest 10 ms from 0.061 s, silent by 0.20 s. Peak +2.5 dBFS.
#   virtual_vibes-cinematic-thud-fx-379991.mp3     1.10 s stereo. Onset 0.080 s, loudest 10 ms
#       from 0.091 s, -40 dB by 0.24 s with a low rumble to 0.36 s. Peak -4.9 dBFS in the downmix,
#       -7.4 per channel: quiet, and low, so quieter still to the ear (max momentary -23.5 LUFS
#       against the slice's -10.9).
#
# All four are Pixabay, under the Pixabay Content License: free for use in a product without
# attribution, modification allowed, not to be redistributed on their own. Committed as part of
# the game, which that licence allows — the owner to confirm, per
# docs/reference/audio-sourcing.md "Licensing" (as §2i did for the draft).
#
# ## What is done to each, and why
#
# - **The head is cut hard, at a fixed time per source**, not by a silence threshold: the offset
#   of the loudest moment is a timing constant, and a threshold would move it whenever the
#   threshold or the source's noise did. Each cut sits just before the source's own onset, with a
#   2 ms guard fade, so the rise of the whoosh is kept and the dead air is not.
# - **The two whooshes are cut to line their loudest moments up**, so one constant times both;
#   the variant the director picks at random must not change when the blade passes.
# - **The tails are cut to what is useful, with a fade.** The whooshes are over by 0.2 s. The
#   slice keeps its ring to 1.1 s, fading from 0.7 s — the ring is the blade and the rest of the
#   2.2 s is a floor nobody hears across a fight. The thud keeps its rumble to 0.75 s.
# - **Mono.** Every one is placed in the world, at the swinger or the struck (AudioSetup's chop
#   pattern), and a placed source is mono.
# - **Levelled on loudness against a -3 dBFS peak ceiling.** Short one-shots, so the measure is
#   the loudest 400 ms momentary (EBU R128 M, over a padded file), not integrated loudness, and
#   the ceiling is measured with astats, never volumedetect: the float intermediates peak above
#   full scale and volumedetect clamps them (the draft bake's lesson). Targets, max momentary:
#       slice  -15 LUFS  the loudest thing in a fight: it is the critical
#       thud   -18 LUFS  every landed blow; the owner found it quiet, so it is LIFTED 7.5 dB into
#                        a -3 dBFS lookahead limiter (latency-compensated, so its onset does not
#                        move) — the ceiling alone would have allowed +1.9 dB. 5 dB louder to the
#                        ear than the source (-23.5 to -18.5 M)
#       whoosh -21 LUFS  under the thud: it is the blade on its way, not the blow
#   The ceiling binds on the slice (its peak is 18 dB over its loudness). Against the axe at the
#   play distance: chop's files measure -27 to -31 M (a quarter-second, so the 400 ms window
#   under-reads them by about 2 dB), so with the catalogue's Volume the thud sits about 6 dB over a
#   felling blow and the whoosh about 4 dB over — a fight is heard over woodcutting, not under it.
#   The rest of the mix is the catalogue's Volume (Assets/Editor/Odyssey/AudioSetup.cs).
#
# ## The shipped files, measured after the bake (the timing constants)
#
#   file                   length   loudest 10 ms, middle   sample peak   max M       gain
#   combat-whoosh.wav      0.200 s  0.041 s                 -5.1 dBFS     -21.0 LUFS  -7.6 dB
#   combat-whoosh_01.wav   0.180 s  0.041 s                 -4.5 dBFS     -21.0 LUFS  -7.0 dB
#   combat-crit-slice.wav  1.100 s  0.065 s                 -3.0 dBFS     -16.8 LUFS  -5.9 dB (ceiling)
#   combat-hit.wav         0.750 s  0.013 s                 -3.0 dBFS     -18.5 LUFS  +7.5 dB into the limiter
#
#   So CombatSoundTiming.WhooshPeakSeconds = 0.040, SlicePeakSeconds = 0.065 and
#   ThudPeakSeconds = 0.013. The slice's LOUDEST SAMPLE is later (0.24 s, the body of the cut),
#   but its loudest 10 ms is the edge meeting the target, which is the moment the owner means;
#   the thud's onset is within 1 ms of its first sample. The script prints the windows it
#   measures, so a re-bake that moves them says so. The thud's target is -18 and it reaches
#   -18.5: another decibel would be bought by limiting a transient already held 5.6 dB (+2.6 into -3.0).
#
# Usage:  tools/audio/bake_combat.sh [source-directory]
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

SRC="${1:-$HOME/Downloads}"
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/Assets/Odyssey/Presentation/Audio/Clips"

CEIL=-3.0
RATE=44100

# name | source | head cut (s) | length (s) | fade-out (s) | target max M (LUFS) | limiter lift (dB, 0 = none)
SOUNDS=(
  "combat-whoosh|floraphonic-swing-whoosh-4-198496.mp3|0.042|0.200|0.060|-21|0"
  "combat-whoosh_01|floraphonic-swing-whoosh-3-198495.mp3|0.026|0.180|0.060|-21|0"
  "combat-crit-slice|dragon-studio-violent-sword-slice-393848.mp3|0.056|1.100|0.400|-15|0"
  "combat-hit|virtual_vibes-cinematic-thud-fx-379991.mp3|0.081|0.750|0.300|-18|7.5"
)

# The loudest 400 ms momentary loudness, over the file padded with half a second of silence so a
# clip shorter than the window is still measured.
max_m() {
  ffmpeg -nostats -i "$1" -af "aformat=channel_layouts=mono,apad=pad_dur=0.5,ebur128=framelog=info" \
    -f null - 2>&1 | grep -o "M: *[-0-9.]*" | awk '{print $2}' | sort -g | tail -1
}

peak_db() {
  ffmpeg -i "$1" -af astats=measure_perchannel=none -f null /dev/null 2>&1 \
    | grep -o "Peak level dB: [-0-9.]*" | tail -1 | grep -o "[-0-9.]*$"
}

# Where the loudest 10 ms is: mean power over 2 ms blocks (88 samples), slid five blocks at a
# time, so the window it names is good to 2 ms rather than to the 10 ms a back-to-back window
# would give. Prints the middle of the loudest window — the number CombatSoundTiming carries.
loudest_10ms() {
  ffmpeg -v error -i "$1" -af "asetnsamples=n=88:p=0,astats=metadata=1:reset=1:measure_perchannel=none,ametadata=print:key=lavfi.astats.Overall.RMS_level:file=-"     -f null - 2>/dev/null     | awk '/pts_time/ { split($0, a, "pts_time:"); t[n] = a[2] + 0 }
           /RMS_level/ { split($0, b, "="); v = b[2] + 0; p[n++] = (v < -200 ? 0 : 10 ^ (v / 10)) }
           END {
             for (i = 0; i + 5 <= n; i++) { s = 0; for (j = 0; j < 5; j++) s += p[i + j]; if (s > best) { best = s; at = t[i] } }
             printf "middle %.3f s (%.1f dB)", at + 0.005, 10 * log(best / 5) / log(10)
           }'
}

for row in "${SOUNDS[@]}"; do
  IFS='|' read -r name source head len fade target lift <<< "$row"
  in="$SRC/$source"
  [ -f "$in" ] || { echo "missing: $in" >&2; exit 1; }

  raw="$OUT/.$name.raw.wav"
  dst="$OUT/$name.wav"

  # Pass one: downmix, cut, and (for the thud) lift into the limiter. The downmix is the chain's
  # first step, not -ac on the output: a limiter ahead of the downmix held each channel to -3 and
  # the sum then peaked 3 dB over it, so the ceiling bound the level and the lift was wasted. The
  # limiter's lookahead is compensated (latency=1), and the file is padded first so the
  # compensation does not eat the tail. The 2 ms fade-in guards the hard head cut.
  chain="aformat=channel_layouts=mono,atrim=start=$head:duration=$len,asetpts=PTS-STARTPTS"
  if [ "$lift" != "0" ]; then
    chain="$chain,apad=pad_dur=0.05,volume=${lift}dB,alimiter=limit=0.708:attack=1:release=40:level=0:latency=1,atrim=duration=$len"
  fi
  ffmpeg -y -v error -i "$in" -af "$chain" -ac 1 -c:a pcm_f32le -ar $RATE "$raw"

  # Pass two: level the bytes that will ship — loudness target or peak ceiling, whichever binds.
  m=$(max_m "$raw")
  peak=$(peak_db "$raw")
  gain=$(awk "BEGIN{r=$target-($m); c=$CEIL-($peak); printf \"%.2f\", (r<c?r:c)}")

  d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$raw")
  ffmpeg -y -v error -i "$raw" \
    -af "volume=${gain}dB,afade=t=in:st=0:d=0.002,afade=t=out:st=$(awk "BEGIN{printf \"%.4f\", $d-$fade}"):d=$fade" \
    -c:a pcm_s16le -ar $RATE "$dst"
  rm -f "$raw"

  printf '%-18s %5.3fs  %7sB  gain %6s dB  peak %6s dBFS  max M %6s LUFS  loudest %s\n' \
    "$name" "$d" "$(stat -c%s "$dst")" "$gain" "$(peak_db "$dst")" "$(max_m "$dst")" "$(loudest_10ms "$dst")"
done

echo
echo "Re-import and rebuild the catalogue:"
echo "  scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build"
