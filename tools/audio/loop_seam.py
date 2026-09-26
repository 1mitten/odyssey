"""Measure whether a looping bed is seamless where it wraps.

Two numbers per file, both against the file's own ordinary behaviour, because "seamless" is a
comparison and not an absolute:

- **the step at the wrap**: the jump from the last sample to the first, per channel, against the
  median and the 99th percentile of every other sample-to-sample step in the file. A click is a
  step far above the file's own worst ordinary one.
- **the level across the wrap**: RMS of the 400 ms either side of the wrap, against the spread of
  400 ms RMS over the whole file. A swell or a breath at the loop point is a level change larger
  than the file's own wandering.

Standard library only (plus ffmpeg on PATH to decode), like the rest of tools/.
Usage:  python3 tools/audio/loop_seam.py file.wav [file.wav ...]
"""
import math
import struct
import subprocess
import sys


def decode(path):
    probe = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "stream=channels,sample_rate",
                            "-of", "csv=p=0", path], capture_output=True, text=True, check=True).stdout
    rate, channels = (int(v) for v in probe.strip().split(",")[:2])
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "s16le", "-"],
                         capture_output=True, check=True).stdout
    samples = struct.unpack("<%dh" % (len(raw) // 2), raw)
    return [samples[c::channels] for c in range(channels)], rate


def rms_db(xs):
    if not xs:
        return -120.0
    return 10 * math.log10(max(sum(x * x for x in xs) / len(xs), 1e-9) / 32768 ** 2)


def seam(path):
    chans, rate = decode(path)
    print(f"{path}: {len(chans)} ch, {rate} Hz, {len(chans[0]) / rate:.2f} s")
    for c, xs in enumerate(chans):
        steps = sorted(abs(xs[i + 1] - xs[i]) for i in range(len(xs) - 1))
        wrap = abs(xs[0] - xs[-1])
        median = steps[len(steps) // 2]
        p99 = steps[int(len(steps) * 0.99)]
        print(f"  ch{c}: wrap step {wrap:5d}   ordinary median {median:5d}, 99th {p99:5d}, worst {steps[-1]:5d}"
              f"   -> {'ok' if wrap <= p99 else 'CLICK'}")

    window = int(rate * 0.4)
    mono = [sum(ch[i] for ch in chans) / len(chans) for i in range(len(chans[0]))]
    levels = [rms_db(mono[i:i + window]) for i in range(0, len(mono) - window, window)]
    levels.sort()
    lo, hi = levels[len(levels) // 20], levels[-len(levels) // 20 - 1]
    before, after = rms_db(mono[-window:]), rms_db(mono[:window])
    print(f"  level: last 400 ms {before:6.1f} dB, first 400 ms {after:6.1f} dB, difference {abs(before - after):4.1f}"
          f"   (the file's own 400 ms levels span {lo:6.1f} to {hi:6.1f}, {hi - lo:4.1f} dB between the 5th and 95th)")


if __name__ == "__main__":
    for arg in sys.argv[1:]:
        seam(arg)
