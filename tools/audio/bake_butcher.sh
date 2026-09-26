#!/usr/bin/env bash
#
# Bake the butcher's voice and its cleaver's deep whoosh (docs/design/62-pig-butcher.md §8d).
#
# Owner, 2026-09-26: "process and make variations of this sound when the pig gets a hit or makes a
# strike play these sounds at varying volumes - loud when he knocks people back or even when hit"
# for pig-sound and pig-squeak; "this is for when they get knocked down or killed" for pig-oink; and
# "make sure their weapon has a swoosh like a sword as well - make it deeper if you can".
#
# ## The sources (mono, 44.1 kHz, one call each)
#
#   freesound_community-pig-sound-47168.mp3     0.68 s, a grunt; silent from 0.60 s.
#   freesound_community-pig-squeak-47166.mp3    0.86 s, a squeal; silent to 0.085 s and from 0.73 s.
#   freesound_community-pig-oink-47167 (1).mp3  0.78 s, an oink, no silence at either end.
#   combat-whoosh.wav, combat-whoosh_01.wav     the committed sword whooshes (bake_combat.sh):
#                                               their loudest 10 ms is centred at 0.041 s.
#
# The three pig recordings are Pixabay's "freesound_community" uploads, under the Pixabay Content
# License: free for use in a product without attribution, modification allowed, not to be
# redistributed on their own. Committed as part of the game — the owner to confirm, per
# docs/reference/audio-sourcing.md "Licensing", as for the blows.
#
# ## What is done, and why
#
# - **Each call is one sound, so the variations are made, not cut**: every take is a source
#   resampled lower (asetrate, which lowers pitch and slows it together — a bigger animal's
#   larger throat, not a pitch-shifted small one), with a low shelf so it has weight at the play
#   distance. Four to five takes a moment, so a fight does not repeat a file.
# - **Four moments at four loudnesses**, so the moment a player most needs to notice is the
#   loudest (max momentary, the bake_combat.sh measure):
#       strike  -21 LUFS  the grunt as it winds up; under the blows, often
#       hurt    -17 LUFS  a squeal when it is hit
#       fling   -13 LUFS  a bellow when it throws somebody: the loudest thing it does
#       down    -14 LUFS  the oink as it goes down or dies
#   The catalogue's Volume and variance then move each play by a little (AudioSetup.cs), and the
#   level's pitch (SpeciesDef.voicePitchPerMille) takes a king lower still at play time.
# - **The deep whoosh** is the sword whoosh resampled to 0.62 (about eight semitones down) with its
#   top rolled off and a low shelf — a heavy blade, not a light one — and its head cut so its
#   loudest 10 ms is centred at 0.040 s again, **the whoosh's own timing constant**
#   (CombatSoundTiming.WhooshPeakSeconds), so the schedule times it with no new number. Levelled
#   at -19 LUFS, two over the sword's -21: a bigger blade.
# - **Mono, 44.1 kHz, -3 dBFS peak ceiling** (astats, never volumedetect), as every placed sound,
#   reached through a lookahead limiter rather than by turning the call down (see take()).
#
# Usage:  tools/audio/bake_butcher.sh [source-directory]
# Needs:  ffmpeg and ffprobe on PATH.
#
set -euo pipefail

SRC="${1:-$HOME/Downloads}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$ROOT/Assets/Odyssey/Presentation/Audio/Clips"
CEIL=-3.0
RATE=44100

GRUNT="$SRC/freesound_community-pig-sound-47168.mp3"
SQUEAL="$SRC/freesound_community-pig-squeak-47166.mp3"
OINK="$SRC/freesound_community-pig-oink-47167 (1).mp3"
for f in "$GRUNT" "$SQUEAL" "$OINK"; do [ -f "$f" ] || { echo "missing: $f" >&2; exit 1; }; done

max_m() {
  ffmpeg -nostats -i "$1" -af "aformat=channel_layouts=mono,apad=pad_dur=0.5,ebur128=framelog=info" \
    -f null - 2>&1 | grep -o "M: *[-0-9.]*" | awk '{print $2}' | sort -g | tail -1
}
peak_db() {
  ffmpeg -i "$1" -af astats=measure_perchannel=none -f null /dev/null 2>&1 \
    | grep -o "Peak level dB: [-0-9.]*" | tail -1 | grep -o "[-0-9.]*$"
}
loudest_10ms() {
  ffmpeg -v error -i "$1" -af "asetnsamples=n=88:p=0,astats=metadata=1:reset=1:measure_perchannel=none,ametadata=print:key=lavfi.astats.Overall.RMS_level:file=-" -f null - 2>/dev/null \
    | awk '/pts_time/ { split($0, a, "pts_time:"); t[n] = a[2] + 0 }
           /RMS_level/ { split($0, b, "="); v = b[2] + 0; p[n++] = (v < -200 ? 0 : 10 ^ (v / 10)) }
           END { for (i = 0; i + 5 <= n; i++) { s = 0; for (j = 0; j < 5; j++) s += p[i + j]; if (s > best) { best = s; at = t[i] } }
                 printf "%.3f", at + 0.005 }'
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# One take: source, head cut (s), resample factor, low shelf (dB at 180 Hz), low-pass (Hz, 0 none),
# fade-out (s), target max M. Shaped in float, then levelled with one gain against the ceiling.
take() {
  local name="$1" src="$2" head="$3" factor="$4" shelf="$5" lp="$6" fade="$7" target="$8"
  local shaped="$TMP/$name.shaped.wav"
  local lpf=""; [ "$lp" != "0" ] && lpf=",lowpass=f=$lp"
  local len; len=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$src")
  ffmpeg -v error -y -i "$src" -af "aformat=channel_layouts=mono,atrim=start=$head,asetpts=PTS-STARTPTS,asetrate=$((RATE))*$factor,aresample=$RATE,lowshelf=f=180:g=$shelf$lpf,afade=t=in:d=0.004" \
    -c:a pcm_f32le "$shaped"
  local dur; dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$shaped")
  local fstart; fstart=$(awk -v d="$dur" -v f="$fade" 'BEGIN { s = d - f; print (s < 0 ? 0 : s) }')
  ffmpeg -v error -y -i "$shaped" -af "afade=t=out:st=$fstart:d=$fade" -c:a pcm_f32le "$TMP/$name.faded.wav"
  local m; m=$(max_m "$TMP/$name.faded.wav")
  local p; p=$(peak_db "$TMP/$name.faded.wav")
  # The full gain to the target, into a latency-compensated lookahead limiter at the ceiling: a
  # pig's call peaks some 17 dB over its loudness, so the ceiling alone left the fling at -20
  # LUFS, nowhere near loud (the first bake). The limiter holds the transients; the call keeps its
  # onset because the limiter's latency is compensated.
  local gain; gain=$(awk -v t="$target" -v m="$m" 'BEGIN { printf "%.2f", t - m }')
  local limit; limit=$(awk -v c="$CEIL" 'BEGIN { printf "%.4f", 10 ^ (c / 20) }')
  ffmpeg -v error -y -i "$TMP/$name.faded.wav" -af "volume=${gain}dB,alimiter=limit=$limit:attack=2:release=60:level=0:latency=1"     -ar $RATE -ac 1 -c:a pcm_s16le "$OUT/$name.wav"
  printf "%-26s %6.3f s  loudest %s s  peak %6s dBFS  max M %6s LUFS  gain %6s dB\n" \
    "$name.wav" "$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT/$name.wav")" \
    "$(loudest_10ms "$OUT/$name.wav")" "$(peak_db "$OUT/$name.wav")" "$(max_m "$OUT/$name.wav")" "$gain"
}

# The strike: the grunt, low and weighty, winding up.        name              src       head   factor shelf lp   fade  target
take butcher-strike        "$GRUNT"  0.000 0.82 4 0    0.12 -21
take butcher-strike_01     "$GRUNT"  0.000 0.74 5 0    0.12 -21
take butcher-strike_02     "$GRUNT"  0.000 0.68 6 0    0.14 -21
take butcher-strike_03     "$SQUEAL" 0.080 0.70 5 3500 0.14 -21
# The hurt: the squeal, less far down so it stays a squeal.
take butcher-hurt          "$SQUEAL" 0.080 0.86 3 0    0.12 -17
take butcher-hurt_01       "$SQUEAL" 0.080 0.78 4 0    0.12 -17
take butcher-hurt_02       "$SQUEAL" 0.080 0.72 4 0    0.14 -17
take butcher-hurt_03       "$GRUNT"  0.000 0.90 3 0    0.12 -17
# The fling: a bellow, the furthest down and the heaviest.
take butcher-fling         "$SQUEAL" 0.080 0.64 7 5000 0.18 -13
take butcher-fling_01      "$SQUEAL" 0.080 0.58 8 4500 0.20 -13
take butcher-fling_02      "$GRUNT"  0.000 0.60 8 4500 0.18 -13
take butcher-fling_03      "$GRUNT"  0.000 0.54 9 4000 0.20 -13
# The down: the oink, slower each take, the last a long dying one.
take butcher-down          "$OINK"   0.000 0.84 4 0    0.16 -14
take butcher-down_01       "$OINK"   0.000 0.74 5 0    0.20 -14
take butcher-down_02       "$OINK"   0.000 0.64 6 5000 0.26 -14

# The deep whoosh: the sword's two takes at 0.62, the head cut so the peak is back at 0.040 s.
# Resampled, a peak at 0.041 s moves to 0.066 s; cutting 0.026 s puts it back.
take combat-whoosh-heavy    "$OUT/combat-whoosh.wav"    0.000 0.62 6 3000 0.10 -19
take combat-whoosh-heavy_01 "$OUT/combat-whoosh_01.wav" 0.000 0.62 6 3000 0.10 -19
for f in combat-whoosh-heavy combat-whoosh-heavy_01; do
  at=$(loudest_10ms "$OUT/$f.wav")
  cut=$(awk -v a="$at" 'BEGIN { c = a - 0.040; printf "%.3f", (c < 0 ? 0 : c) }')
  ffmpeg -v error -y -i "$OUT/$f.wav" -af "atrim=start=$cut,asetpts=PTS-STARTPTS,afade=t=in:d=0.002" -c:a pcm_s16le "$TMP/$f.wav"
  mv "$TMP/$f.wav" "$OUT/$f.wav"
  printf "%-26s head cut %s s -> loudest 10 ms at %s s\n" "$f.wav" "$cut" "$(loudest_10ms "$OUT/$f.wav")"
done
