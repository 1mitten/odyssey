#!/usr/bin/env python3
"""Odyssey icon pipeline: read the owner's pixel-art sheets, check the mapping, export one PNG per
icon key, and produce labelled contact sheets for review.

Standard library only, on purpose. The remote Claude Code container has no Pillow and its proxy
blocks some hosts; a vendored decoder removes the whole class of "the icon gate cannot run here".
The input domain is narrow and ours: 8-bit non-interlaced PNG. Anything else is refused by name.

Commands
  detect    [--sheet ID]      report each sheet's grid and native pixel scale; compare with sheets.csv
  contact   [--sheet ID] [--keys]   write labelled contact sheets and reports for review
  export    [--dry-run]       write Assets/Art/Ui/icons/<key>.png for every approved mapping
  validate  [--strict]        check the registry and the mapping; exit non-zero on error
  emit-web                    write the mockup's key-to-cell table (needs no art, no deps)

Contract, from docs/adr/0007-pixel-art-icon-pipeline.md: export at 64 px, RGBA8, nearest-neighbour
at an integer factor only, never downscaled. Point filtering, sRGB and non-readable are import
settings, asserted on the Unity side.
"""
from __future__ import annotations
import argparse, csv, json, os, struct, sys, zlib
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHEET_DIR = os.path.join(ROOT, "art-source", "icons", "sheets")
SHEETS_CSV = os.path.join(ROOT, "art-source", "icons", "sheets.csv")
KEYS_CSV = os.path.join(ROOT, "docs", "design", "icon-keys.csv")
MAP_CSV = os.path.join(ROOT, "docs", "design", "icon-map.csv")
# Where the running game looks for icon art. This must stay in step with
# Assets/Odyssey/Presentation/Ui/IconArt.cs, which loads "<Folder>/<key>" out of Resources: a
# Resources root under Assets/Art/Ui/ satisfies both ADR 0007's "every interface texture lives
# under Assets/Art/Ui/" and the engine's own rule about where a runtime-loadable asset may sit.
# It read Assets/Art/Ui/icons until 2026-09-17, which is a folder nothing loads from — so
# anything this tool had ever exported would have drawn the placeholder square, silently, because
# a key with no art is not an error and is not logged.
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Ui", "Resources", "odyssey", "icons")
CONTACT_DIR = os.path.join(ROOT, "art-source", "icons", "contact")
WEB_TABLE = os.path.join(ROOT, "docs", "reference", "mockups", "icon-map.js")

TARGET_SIZES = (32, 64, 128)     # powers of two; 64 is the default and the atlas limit
ALPHA_EPS = 8                    # alpha at or below this counts as transparent
PITCH_TOL = 1                    # px of slack when checking a uniform grid pitch
MIN_INK = 4                      # opaque pixels below which a cell counts as empty


# ----------------------------------------------------------------- images

class Image:
    __slots__ = ("w", "h", "px")

    def __init__(self, w: int, h: int, px: bytearray | None = None):
        self.w, self.h = w, h
        self.px = px if px is not None else bytearray(w * h * 4)

    def idx(self, x: int, y: int) -> int:
        return (y * self.w + x) * 4

    def get(self, x: int, y: int) -> tuple[int, int, int, int]:
        i = self.idx(x, y)
        return tuple(self.px[i:i + 4])

    def put(self, x: int, y: int, rgba) -> None:
        if 0 <= x < self.w and 0 <= y < self.h:
            i = self.idx(x, y)
            self.px[i:i + 4] = bytes(rgba)

    def opaque(self, x: int, y: int) -> bool:
        return self.px[self.idx(x, y) + 3] > ALPHA_EPS

    def crop(self, x0: int, y0: int, w: int, h: int) -> "Image":
        out = Image(w, h)
        for y in range(h):
            s = self.idx(x0, y0 + y)
            out.px[y * w * 4:(y + 1) * w * 4] = self.px[s:s + w * 4]
        return out

    def scale(self, k: int) -> "Image":
        """Nearest-neighbour by an integer factor. The only resampler in this file."""
        if k < 1:
            raise ValueError("scale factor must be >= 1")
        if k == 1:
            return Image(self.w, self.h, bytearray(self.px))
        out = Image(self.w * k, self.h * k)
        for y in range(self.h):
            row = bytearray()
            for x in range(self.w):
                i = self.idx(x, y)
                row += self.px[i:i + 4] * k
            for r in range(k):
                o = ((y * k + r) * out.w) * 4
                out.px[o:o + len(row)] = row
        return out

    def downsample(self, s: int) -> "Image":
        """Inverse of scale, valid only when every s*s block is uniform. Checked by native_scale."""
        if s == 1:
            return self
        out = Image(self.w // s, self.h // s)
        for y in range(out.h):
            for x in range(out.w):
                out.put(x, y, self.get(x * s, y * s))
        return out

    def fill(self, rgba) -> None:
        self.px[:] = bytes(rgba) * (self.w * self.h)

    def blit(self, src: "Image", x0: int, y0: int) -> None:
        for y in range(src.h):
            for x in range(src.w):
                r, g, b, a = src.get(x, y)
                if a == 0:
                    continue
                if a == 255:
                    self.put(x0 + x, y0 + y, (r, g, b, a))
                else:                                    # source-over, for labels on checkerboard
                    dr, dg, db, da = self.get(x0 + x, y0 + y) if 0 <= x0 + x < self.w and 0 <= y0 + y < self.h else (0, 0, 0, 0)
                    t = a / 255.0
                    self.put(x0 + x, y0 + y, (int(r * t + dr * (1 - t)), int(g * t + dg * (1 - t)),
                                              int(b * t + db * (1 - t)), max(a, da)))

    def rect(self, x0: int, y0: int, w: int, h: int, rgba) -> None:
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                self.put(x, y, rgba)

    def hline(self, x0: int, x1: int, y: int, rgba) -> None:
        for x in range(x0, x1):
            self.put(x, y, rgba)

    def vline(self, x: int, y0: int, y1: int, rgba) -> None:
        for y in range(y0, y1):
            self.put(x, y, rgba)


# ----------------------------------------------------------------- PNG codec

class PngError(Exception):
    pass


def read_png(path: str) -> Image:
    with open(path, "rb") as f:
        data = f.read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise PngError(f"{path}: not a PNG")
    pos, idat, pal, trns, ihdr = 8, bytearray(), None, None, None
    while pos < len(data):
        (ln,) = struct.unpack(">I", data[pos:pos + 4])
        typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if typ == b"IHDR":
            ihdr = struct.unpack(">IIBBBBB", body)
        elif typ == b"PLTE":
            pal = body
        elif typ == b"tRNS":
            trns = body
        elif typ == b"IDAT":
            idat += body
        elif typ == b"IEND":
            break
    if ihdr is None:
        raise PngError(f"{path}: no IHDR")
    w, h, depth, ctype, comp, filt, interlace = ihdr
    if depth != 8:
        raise PngError(f"{path}: {depth}-bit PNG. Re-export as 8-bit, or install Pillow")
    if interlace:
        raise PngError(f"{path}: interlaced PNG. Re-export without Adam7 interlacing")
    if ctype not in (0, 2, 3, 4, 6):
        raise PngError(f"{path}: unsupported colour type {ctype}")
    ch = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ctype]
    raw = zlib.decompress(bytes(idat))
    stride = w * ch
    if len(raw) < (stride + 1) * h:
        raise PngError(f"{path}: truncated image data")

    out, prev = Image(w, h), bytearray(stride)
    for y in range(h):
        ft = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        if ft == 1:
            for i in range(ch, stride):
                line[i] = (line[i] + line[i - ch]) & 0xFF
        elif ft == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ft == 3:
            for i in range(stride):
                a = line[i - ch] if i >= ch else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xFF
        elif ft == 4:
            for i in range(stride):
                a = line[i - ch] if i >= ch else 0
                b = prev[i]
                c = prev[i - ch] if i >= ch else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        elif ft != 0:
            raise PngError(f"{path}: bad filter type {ft} on row {y}")
        prev = line

        for x in range(w):
            p = line[x * ch:(x + 1) * ch]
            if ctype == 0:
                rgba = (p[0], p[0], p[0], 255)
            elif ctype == 4:
                rgba = (p[0], p[0], p[0], p[1])
            elif ctype == 2:
                rgba = (p[0], p[1], p[2], 255)
            elif ctype == 6:
                rgba = (p[0], p[1], p[2], p[3])
            else:
                i = p[0]
                if pal is None:
                    raise PngError(f"{path}: palette image without PLTE")
                a = trns[i] if trns and i < len(trns) else 255
                rgba = (pal[i * 3], pal[i * 3 + 1], pal[i * 3 + 2], a)
            out.put(x, y, rgba)
    return out


def write_png(path: str, img: Image) -> None:
    def chunk(typ: bytes, body: bytes) -> bytes:
        return struct.pack(">I", len(body)) + typ + body + struct.pack(">I", zlib.crc32(typ + body) & 0xFFFFFFFF)

    raw = bytearray()
    for y in range(img.h):                      # filter type 0 throughout, so output is deterministic
        raw += b"\x00" + img.px[y * img.w * 4:(y + 1) * img.w * 4]
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", img.w, img.h, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(bytes(raw), 9)))
        f.write(chunk(b"IEND", b""))


# ----------------------------------------------------------------- 5x7 font

_F = {
 " ": (0, 0, 0, 0, 0, 0, 0), ".": (0, 0, 0, 0, 0, 0, 0b00100), "-": (0, 0, 0, 0b11111, 0, 0, 0),
 "/": (0b00001, 0b00010, 0b00010, 0b00100, 0b01000, 0b01000, 0b10000),
 ":": (0, 0b00100, 0, 0, 0, 0b00100, 0), "_": (0, 0, 0, 0, 0, 0, 0b11111),
 "0": (0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110),
 "1": (0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110),
 "2": (0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111),
 "3": (0b11111, 0b00010, 0b00100, 0b00010, 0b00001, 0b10001, 0b01110),
 "4": (0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010),
 "5": (0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110),
 "6": (0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110),
 "7": (0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000),
 "8": (0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110),
 "9": (0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100),
 "A": (0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001),
 "B": (0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110),
 "C": (0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110),
 "D": (0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110),
 "E": (0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111),
 "F": (0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000),
 "G": (0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110),
 "H": (0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001),
 "I": (0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110),
 "J": (0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100),
 "K": (0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001),
 "L": (0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111),
 "M": (0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001),
 "N": (0b10001, 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001),
 "O": (0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110),
 "P": (0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000),
 "Q": (0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10011, 0b01101),
 "R": (0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001),
 "S": (0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110),
 "T": (0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100),
 "U": (0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110),
 "V": (0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100),
 "W": (0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001),
 "X": (0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001),
 "Y": (0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100),
 "Z": (0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111),
}


def text(img: Image, s: str, x0: int, y0: int, k: int = 2, rgba=(235, 235, 235, 255)) -> int:
    """Draw s at integer scale k. Returns the width used."""
    cx = x0
    for chpos in s.upper():
        glyph = _F.get(chpos)
        if glyph is not None:
            for r, bits in enumerate(glyph):
                for c in range(5):
                    if bits & (1 << (4 - c)):
                        img.rect(cx + c * k, y0 + r * k, k, k, rgba)
        cx += 6 * k
    return cx - x0


def text_w(s: str, k: int = 2) -> int:
    return len(s) * 6 * k


# ----------------------------------------------------------------- grid detection

def _runs(zeros: list[bool]) -> list[tuple[int, int]]:
    out, start = [], None
    for i, z in enumerate(zeros):
        if z and start is None:
            start = i
        elif not z and start is not None:
            out.append((start, i - 1)); start = None
    if start is not None:
        out.append((start, len(zeros) - 1))
    return out


def _axis(ink: list[int], a: int, b: int, limit: int, name: str) -> tuple[list[int], int, int]:
    """Cell boundaries along one axis, from the transparent gutters between cells.

    The interior boundaries are gutter midpoints. The two outer boundaries are *extrapolated* from
    the interior pitch rather than taken from the ink extremes: a sheet whose outermost art sits
    flush against the image edge would otherwise report a short first and last cell and look
    non-uniform. Returns (cuts, pitch, gutter).
    """
    gut = [(gs, ge) for gs, ge in _runs([v == 0 for v in ink]) if a < gs and ge < b]
    if not gut:
        raise ValueError(f"no transparent gutters on the {name} axis")
    mids = [(gs + ge) // 2 for gs, ge in gut]
    gutter = round(sum(ge - gs + 1 for gs, ge in gut) / len(gut))
    if len(mids) == 1:
        return [max(a - 1, -1), mids[0], min(b + 1, limit - 1)], (b - a + 1) // 2, gutter
    d = [mids[i + 1] - mids[i] for i in range(len(mids) - 1)]
    if max(d) - min(d) > PITCH_TOL:
        raise ValueError(f"non-uniform {name} pitch {sorted(set(d))}")
    pitch = round(sum(d) / len(d))
    cuts = [mids[0] - pitch] + mids + [mids[-1] + pitch]
    if cuts[0] >= a or cuts[-1] <= b:
        raise ValueError(f"{name} boundaries {cuts[0]}..{cuts[-1]} do not contain the art {a}..{b}")
    cuts[0] = max(cuts[0], -1)
    cuts[-1] = min(cuts[-1], limit - 1)
    return cuts, pitch, gutter


def detect_grid(img: Image, asserted: dict | None = None) -> dict:
    """Find the cell grid.

    The gutter pass is the only detector. A sheet with no transparent gutters between cells carries
    no grid information in its alpha channel at all -- a framed tile whose border touches the cell
    edge makes every boundary line fully opaque, so "the boundary with least ink" is meaningless --
    so such a sheet must declare its grid in sheets.csv and detection reports that it was skipped.
    """
    col_ink = [sum(1 for y in range(img.h) if img.opaque(x, y)) for x in range(img.w)]
    row_ink = [sum(1 for x in range(img.w) if img.opaque(x, y)) for y in range(img.h)]
    xs = [x for x, v in enumerate(col_ink) if v]
    ys = [y for y, v in enumerate(row_ink) if v]
    if not xs or not ys:
        raise PngError("sheet is entirely transparent")
    x0, x1, y0, y1 = xs[0], xs[-1], ys[0], ys[-1]

    det, why = None, ""
    try:
        cx, px, gx = _axis(col_ink, x0, x1, img.w, "x")
        cy, py, gy = _axis(row_ink, y0, y1, img.h, "y")
        det = {"cols": len(cx) - 1, "rows": len(cy) - 1, "cuts_x": cx, "cuts_y": cy,
               "cell_w": px - gx, "cell_h": py - gy, "pitch_x": px, "pitch_y": py,
               "method": "gutter"}
    except ValueError as e:
        why = str(e)

    a_cols = int(asserted["cols"]) if asserted and asserted.get("cols") else None
    a_rows = int(asserted["rows"]) if asserted and asserted.get("rows") else None

    if det and a_cols and a_rows and (det["cols"], det["rows"]) != (a_cols, a_rows):
        raise PngError(
            f"grid disagreement. sheets.csv asserts {a_cols}x{a_rows}; detection found "
            f"{det['cols']}x{det['rows']} by gutters at pitch {det['pitch_x']}x{det['pitch_y']}. "
            f"Neither is preferred silently: correct one of them")
    if det:
        return det
    if a_cols and a_rows:
        cx = [round(k * img.w / a_cols) - 1 for k in range(a_cols + 1)]
        cy = [round(k * img.h / a_rows) - 1 for k in range(a_rows + 1)]
        return {"cols": a_cols, "rows": a_rows, "cuts_x": cx, "cuts_y": cy,
                "cell_w": img.w // a_cols, "cell_h": img.h // a_rows,
                "pitch_x": img.w // a_cols, "pitch_y": img.h // a_rows,
                "method": "asserted"}
    raise PngError(f"grid not detectable ({why}), and sheets.csv asserts no cols/rows for this "
                   f"sheet. Count the cells and fill them in")


def native_scale(img: Image) -> int:
    """Largest s for which the whole image is an exact s-times nearest-neighbour upscale.

    The sheets may already have been exported at 2x or 4x. Without this, the "integer upscale only"
    rule in ADR 0004 cannot be enforced, because the apparent source size would be wrong.

    Caveat: art made entirely of large flat blocks can satisfy the test at a higher factor than the
    artist intended, because a 2x2 run of one colour is indistinguishable from an upscaled pixel.
    Detection therefore only advises; `sheets.csv` has a `native_scale` column that overrides it, and
    `detect` prints the figure for a human to confirm.
    """
    stride = img.w * 4
    for s in (8, 6, 5, 4, 3, 2):
        if img.w % s or img.h % s:
            continue
        ok = True
        for by in range(0, img.h, s):
            base = bytes(img.px[by * stride:(by + 1) * stride])
            for r in range(1, s):                     # the s rows of a block must be identical
                o = (by + r) * stride
                if bytes(img.px[o:o + stride]) != base:
                    ok = False; break
            if not ok:
                break
            for i in range(0, img.w, s):              # and each run of s pixels must be one colour
                if base[i * 4:(i + s) * 4] != base[i * 4:i * 4 + 4] * s:
                    ok = False; break
            if not ok:
                break
        if ok:
            return s
    return 1


def sheet_scale(row: dict | None, img: Image) -> int:
    """The sheet's native pixel scale: the manifest's value if it states one, else detection."""
    if row and (row.get("native_scale") or "").strip():
        return int(row["native_scale"])
    return native_scale(img)


def cell_rects(grid: dict) -> list[list[tuple[int, int, int, int]]]:
    cx, cy = grid["cuts_x"], grid["cuts_y"]
    out = []
    for r in range(grid["rows"]):
        row = []
        for c in range(grid["cols"]):
            x, y = cx[c] + 1, cy[r] + 1
            row.append((x, y, cx[c + 1] - x, cy[r + 1] - y))
        out.append(row)
    return out


def tight_box(img: Image, rect) -> tuple[int, int, int, int] | None:
    x0, y0, w, h = rect
    xs = [x for x in range(x0, x0 + w) if any(img.opaque(x, y) for y in range(y0, y0 + h))]
    ys = [y for y in range(y0, y0 + h) if any(img.opaque(x, y) for x in range(x0, x0 + w))]
    if not xs or not ys:
        return None
    ink = sum(1 for x in xs for y in ys if img.opaque(x, y))
    if ink < MIN_INK:
        return None
    return (xs[0], ys[0], xs[-1] - xs[0] + 1, ys[-1] - ys[0] + 1)


# ----------------------------------------------------------------- data files

def load_csv(path: str) -> list[dict]:
    if not os.path.exists(path):
        return []
    with open(path, newline="") as f:
        return list(csv.DictReader(f))


def sheet_path(sheet_id: str, sheets: dict) -> str:
    row = sheets.get(sheet_id)
    fn = (row or {}).get("file") or f"{sheet_id}.png"
    return os.path.join(SHEET_DIR, fn)


def load_sheets() -> dict:
    return {r["sheet"]: r for r in load_csv(SHEETS_CSV)}


# ----------------------------------------------------------------- validate

def cmd_validate(args) -> int:
    keys = {r["key"]: r for r in load_csv(KEYS_CSV)}
    mapping = load_csv(MAP_CSV)
    sheets = load_sheets()
    errors, warnings, infos = [], [], []

    if not keys:
        errors.append(f"registry missing or empty: {KEYS_CSV}")
    seen = {}
    for i, r in enumerate(mapping, start=2):
        key, st = r["key"], r["status"]
        where = f"{os.path.basename(MAP_CSV)}:{i}"
        if key not in keys:
            errors.append(f"{where}: unknown-key {key}")
        if st not in ("mapped", "shared", "gap", "approved", "proposed", "rejected", "hold"):
            errors.append(f"{where}: bad status {st!r}")
        if st != "gap":
            if r["sheet"] not in sheets:
                errors.append(f"{where}: unknown-sheet {r['sheet']!r} for {key}")
            if not r["cell_description"].strip():
                errors.append(f"{where}: {key} has art assigned but no description")
            if r["confidence"] not in ("high", "med", "low"):
                errors.append(f"{where}: bad confidence {r['confidence']!r} for {key}")
            if r["row"] and r["col"]:
                sh = sheets.get(r["sheet"], {})
                if sh.get("cols") and sh.get("rows"):
                    if not (0 <= int(r["row"]) < int(sh["rows"]) and 0 <= int(r["col"]) < int(sh["cols"])):
                        errors.append(f"{where}: cell-out-of-range {r['row']},{r['col']} "
                                      f"for sheet {r['sheet']} ({sh['cols']}x{sh['rows']})")
        if key in seen:
            errors.append(f"{where}: duplicate-key {key}, first seen at line {seen[key]}")
        seen[key] = i

    located = [r for r in mapping if r["status"] != "gap" and r["row"] and r["col"]]
    cells = Counter((r["sheet"], r["row"], r["col"]) for r in located)
    for cell, n in cells.items():
        if n > 1:
            ks = [r["key"] for r in located if (r["sheet"], r["row"], r["col"]) == cell]
            warnings.append(f"shared-cell sheet {cell[0]} at {cell[1]},{cell[2]} serves {n} keys: {', '.join(ks)}")

    unmapped = sorted(set(keys) - {r["key"] for r in mapping})
    for k in unmapped:
        infos.append(f"unmapped-key {k} (legal: falls through to a generated placeholder)")

    with_art = [r for r in mapping if r["status"] != "gap"]
    gaps = [r for r in mapping if r["status"] == "gap"]
    unlocated = [r for r in with_art if not (r["row"] and r["col"])]
    print(f"registry   {len(keys)} keys")
    print(f"mapping    {len(mapping)} rows: {len(with_art)} with art, {len(gaps)} gaps")
    print(f"located    {len(located)} of {len(with_art)} have row and col "
          f"({len(unlocated)} awaiting the contact-sheet pass)")
    ns = Counter(".".join(r["key"].split(".")[:3]) if r["key"].startswith("ui.arch.")
                 else ".".join(r["key"].split(".")[:2]) for r in mapping)
    print(f"namespaces {len(ns)}")
    for m in infos[:5]:
        print(f"  info    {m}")
    if len(infos) > 5:
        print(f"  info    ... and {len(infos) - 5} more unmapped keys")
    for m in warnings:
        print(f"  warn    {m}")
    for m in errors:
        print(f"  ERROR   {m}")
    if args.strict and unlocated:
        print(f"  ERROR   strict: {len(unlocated)} assignments have no row and col yet")
        return 1
    print("FAIL" if errors else "ok")
    return 1 if errors else 0


# ----------------------------------------------------------------- detect

def cmd_detect(args) -> int:
    sheets = load_sheets()
    ids = [args.sheet] if args.sheet else sorted(sheets)
    rc = 0
    for sid in ids:
        p = sheet_path(sid, sheets)
        if not os.path.exists(p):
            print(f"sheet {sid}: MISSING {os.path.relpath(p, ROOT)}")
            rc = 1
            continue
        try:
            img = read_png(p)
            grid = detect_grid(img, sheets.get(sid))
            rects = [r for row in cell_rects(grid) for r in row]
            s = sheet_scale(sheets.get(sid), img)
            filled = sum(1 for r in rects if tight_box(img, r))
            print(f"sheet {sid}: {img.w}x{img.h}  grid {grid['cols']}x{grid['rows']}  "
                  f"pitch {grid['pitch_x']}x{grid['pitch_y']}  art box ~{grid['cell_w']}x{grid['cell_h']}  "
                  f"via {grid['method']}  native scale {s}x  cells with art {filled}/{len(rects)}")
            print(f"  -> sheets.csv: cols={grid['cols']},rows={grid['rows']},"
                  f"cell_w={grid['cell_w']},cell_h={grid['cell_h']},native_scale={s}")
        except PngError as e:
            print(f"sheet {sid}: ERROR {e}")
            rc = 1
    return rc


# ----------------------------------------------------------------- contact sheets

CHECK_A, CHECK_B = (42, 42, 42, 255), (52, 52, 52, 255)
GRIDC = (110, 110, 110, 255)
CONF_COL = {"high": (90, 190, 110, 255), "med": (215, 170, 70, 255), "low": (210, 90, 80, 255)}


def cmd_contact(args) -> int:
    sheets = load_sheets()
    mapping = [r for r in load_csv(MAP_CSV) if r["status"] != "gap"]
    by_cell = defaultdict(list)
    for r in mapping:
        if r["row"] and r["col"]:
            by_cell[(r["sheet"], int(r["row"]), int(r["col"]))].append(r)
    ids = [args.sheet] if args.sheet else sorted(sheets)
    os.makedirs(CONTACT_DIR, exist_ok=True)
    rc = 0
    for sid in ids:
        p = sheet_path(sid, sheets)
        if not os.path.exists(p):
            print(f"sheet {sid}: MISSING {os.path.relpath(p, ROOT)}")
            rc = 1
            continue
        img = read_png(p)
        grid = detect_grid(img, sheets.get(sid))
        rects = cell_rects(grid)
        s = sheet_scale(sheets.get(sid), img)
        art = 96                                        # art area per tile
        k = max(1, art // max(grid["cell_w"] // s, grid["cell_h"] // s))
        cap = 34 if args.keys else 10
        tw, th, gut, edge = art + 8, art + cap + 8, 2, 52
        W = edge * 2 + grid["cols"] * (tw + gut)
        H = edge * 2 + grid["rows"] * (th + gut)
        out = Image(W, H)
        for y in range(H):                              # checkerboard, because these are transparent
            for x in range(W):
                out.put(x, y, CHECK_A if ((x // 8) + (y // 8)) % 2 == 0 else CHECK_B)
        for c in range(grid["cols"]):
            lx = edge + c * (tw + gut) + tw // 2 - text_w(str(c), 3) // 2
            text(out, str(c), lx, 14, 3)
            text(out, str(c), lx, H - 38, 3)
        for r in range(grid["rows"]):
            ly = edge + r * (th + gut) + th // 2 - 10
            text(out, str(r), 12, ly, 3)
            text(out, str(r), W - 40, ly, 3)
        unmapped = []
        for r in range(grid["rows"]):
            for c in range(grid["cols"]):
                tx, ty = edge + c * (tw + gut), edge + r * (th + gut)
                out.rect(tx, ty, tw, th, (26, 26, 26, 255))
                box = tight_box(img, rects[r][c])
                rows_here = by_cell.get((sid, r, c), [])
                if box:
                    cell = img.crop(*box).downsample(s) if s > 1 else img.crop(*box)
                    scaled = cell.scale(max(1, min(k, art // max(1, max(cell.w, cell.h)))))
                    out.blit(scaled, tx + (tw - scaled.w) // 2, ty + 4 + (art - scaled.h) // 2)
                    if not rows_here:
                        unmapped.append((r, c))
                if args.keys and rows_here:
                    for i, rr in enumerate(rows_here[:2]):
                        label = rr["key"][3:]
                        text(out, label[:13], tx + 3, ty + art + 6 + i * 14, 2)
                bar = CONF_COL.get(rows_here[0]["confidence"]) if rows_here else (
                    (200, 60, 200, 255) if box else None)
                if bar:
                    out.rect(tx, ty + th - 3, tw, 3, bar)
                out.hline(tx, tx + tw, ty, GRIDC); out.hline(tx, tx + tw, ty + th - 1, GRIDC)
                out.vline(tx, ty, ty + th, GRIDC); out.vline(tx + tw - 1, ty, ty + th, GRIDC)
        name = os.path.join(CONTACT_DIR, f"{sid}-contact.png")
        write_png(name, out)
        rep = os.path.join(CONTACT_DIR, f"{sid}-report.md")
        with open(rep, "w") as f:
            f.write(f"# Sheet {sid} contact report\n\n")
            f.write(f"- image: `{os.path.basename(p)}` {img.w}x{img.h}\n")
            f.write(f"- grid: {grid['cols']} x {grid['rows']}, cell {grid['cell_w']}x{grid['cell_h']}, "
                    f"detected by {grid['method']}\n- native pixel scale: {s}x\n")
            f.write(f"- magenta bar means a cell has art but no key\n\n")
            f.write(f"## Unmapped cells ({len(unmapped)})\n\nPaste into `icon-map.csv` once named.\n\n")
            f.write("```csv\nkey,sheet,row,col,cell_description,confidence,status\n")
            for r, c in unmapped:
                f.write(f",{sid},{r},{c},,,\n")
            f.write("```\n")
        print(f"sheet {sid}: wrote {os.path.relpath(name, ROOT)} ({W}x{H}) "
              f"and {os.path.relpath(rep, ROOT)}; {len(unmapped)} unmapped cells")
    return rc


# ----------------------------------------------------------------- export

def cmd_export(args) -> int:
    sheets = load_sheets()
    mapping = [r for r in load_csv(MAP_CSV)
               if r["status"] in ("mapped", "shared", "approved") and r["row"] and r["col"]]
    if not mapping:
        print("nothing to export: no assignment has a row and col yet.")
        print("run `contact`, fill in row and col in docs/design/icon-map.csv, then export.")
        return 0
    planned, errors = [], []
    cache: dict[str, tuple] = {}
    for r in mapping:
        sid = r["sheet"]
        if sid not in cache:
            p = sheet_path(sid, sheets)
            if not os.path.exists(p):
                errors.append(f"{r['key']}: sheet {sid} missing at {os.path.relpath(p, ROOT)}")
                continue
            img = read_png(p)
            grid = detect_grid(img, sheets.get(sid))
            rects = cell_rects(grid)
            cache[sid] = (img, grid, rects, sheet_scale(sheets.get(sid), img))
        img, grid, rects, s = cache[sid]
        target = int((sheets.get(sid, {}) or {}).get("target_px") or 64)
        if target not in TARGET_SIZES:
            errors.append(f"{r['key']}: target_px {target} not in {TARGET_SIZES} (ADR 0004)")
            continue
        row, col = int(r["row"]), int(r["col"])
        if not (0 <= row < grid["rows"] and 0 <= col < grid["cols"]):
            errors.append(f"{r['key']}: cell {row},{col} outside {grid['cols']}x{grid['rows']}")
            continue
        box = tight_box(img, rects[row][col])
        if box is None:
            errors.append(f"{r['key']}: cell {row},{col} on sheet {sid} is empty")
            continue
        cell = img.crop(*box)
        cell = cell.downsample(s) if s > 1 else cell
        n = max(cell.w, cell.h)
        k = target // n
        if k < 1:
            errors.append(f"{r['key']}: source {cell.w}x{cell.h} exceeds target {target}; "
                          f"downscaling pixel art is refused")
            continue
        scaled = cell.scale(k)
        canvas = Image(target, target)
        ox = ((target - scaled.w) // 2) // k * k
        oy = ((target - scaled.h) // 2) // k * k
        canvas.blit(scaled, ox, oy)
        planned.append((r["key"], canvas, k, target))
    if errors:
        for e in errors:
            print(f"  ERROR   {e}")
        print(f"FAIL: {len(errors)} problems; nothing written")
        return 1
    if args.dry_run:
        for key, img, k, t in planned:
            print(f"  would write {key}.png {t}x{t} (scale {k}x)")
        print(f"ok: {len(planned)} icons would be written to {os.path.relpath(OUT_DIR, ROOT)}")
        return 0
    os.makedirs(OUT_DIR, exist_ok=True)
    lock = {}
    for key, img, k, t in planned:
        path = os.path.join(OUT_DIR, f"{key}.png")
        write_png(path, img)
        lock[key] = {"size": t, "scale": k}
    lock_path = os.path.join(ROOT, "art-source", "icons", "export-lock.json")
    os.makedirs(os.path.dirname(lock_path), exist_ok=True)
    with open(lock_path, "w") as f:
        json.dump({"generator": "tools/icons/icons.py", "icons": lock}, f, indent=1, sort_keys=True)
    print(f"ok: wrote {len(planned)} icons to {os.path.relpath(OUT_DIR, ROOT)}")
    return 0


# ----------------------------------------------------------------- emit-web

def cmd_emit_web(args) -> int:
    sheets = load_sheets()
    mapping = load_csv(MAP_CSV)
    out = {}
    for r in mapping:
        if r["status"] == "gap":
            continue
        out[r["key"]] = {"s": r["sheet"], "r": int(r["row"]) if r["row"] else None,
                         "c": int(r["col"]) if r["col"] else None,
                         "d": r["cell_description"], "q": r["confidence"]}
    meta = {sid: {"file": (row.get("file") or f"{sid}.png"),
                  "cols": int(row["cols"]) if row.get("cols") else None,
                  "rows": int(row["rows"]) if row.get("rows") else None,
                  "cell_w": int(row["cell_w"]) if row.get("cell_w") else None,
                  "cell_h": int(row["cell_h"]) if row.get("cell_h") else None}
            for sid, row in sheets.items()}
    gaps = sorted(r["key"] for r in mapping if r["status"] == "gap")
    os.makedirs(os.path.dirname(WEB_TABLE), exist_ok=True)
    with open(WEB_TABLE, "w") as f:
        f.write("// Generated by tools/icons/icons.py emit-web. Do not edit by hand.\n")
        f.write("// Maps icon keys to a sheet cell so the mockup can show the real art as CSS sprites.\n")
        f.write("// Rows with r or c null are not located yet: the mockup falls back to a placeholder.\n")
        f.write(f"window.ICON_SHEETS = {json.dumps(meta, indent=1, sort_keys=True)};\n")
        f.write(f"window.ICON_MAP = {json.dumps(out, indent=1, sort_keys=True)};\n")
        f.write(f"window.ICON_GAPS = {json.dumps(gaps, indent=1)};\n")
    located = sum(1 for v in out.values() if v["r"] is not None)
    print(f"ok: {os.path.relpath(WEB_TABLE, ROOT)} — {len(out)} keys with art "
          f"({located} located), {len(gaps)} gaps, {len(meta)} sheets")
    return 0


# ----------------------------------------------------------------- main

def main(argv=None) -> int:
    ap = argparse.ArgumentParser(prog="icons.py", description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    d = sub.add_parser("detect"); d.add_argument("--sheet"); d.set_defaults(fn=cmd_detect)
    c = sub.add_parser("contact"); c.add_argument("--sheet")
    c.add_argument("--keys", action="store_true"); c.set_defaults(fn=cmd_contact)
    e = sub.add_parser("export"); e.add_argument("--dry-run", action="store_true")
    e.set_defaults(fn=cmd_export)
    v = sub.add_parser("validate"); v.add_argument("--strict", action="store_true")
    v.set_defaults(fn=cmd_validate)
    w = sub.add_parser("emit-web"); w.set_defaults(fn=cmd_emit_web)
    args = ap.parse_args(argv)
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
