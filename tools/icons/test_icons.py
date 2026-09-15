"""Tests for the icon pipeline. Synthesises sheets in memory, so they run with no art and no Unity.

    python3 -m unittest discover -s tools/icons -t .
"""
import os, sys, tempfile, unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import icons  # noqa: E402


def make_sheet(cols, rows, cell=16, gutter=4, scale=1, fill_all=True, frame=False):
    """A synthetic sheet: one solid colour per cell, distinct per cell, on transparency."""
    cw = cell * scale
    g = gutter * scale
    w = cols * cw + (cols - 1) * g
    h = rows * cw + (rows - 1) * g
    img = icons.Image(w, h)
    for r in range(rows):
        for c in range(cols):
            if not fill_all and (r + c) % 3 == 0:
                continue
            x0, y0 = c * (cw + g), r * (cw + g)
            col = (20 + 30 * c % 236, 40 + 25 * r % 216, 128, 255)
            if frame:                                  # a border touching the cell edge
                img.rect(x0, y0, cw, cw, (200, 200, 200, 255))
                img.rect(x0 + scale, y0 + scale, cw - 2 * scale, cw - 2 * scale, col)
            else:                                      # inset art, leaving the gutter transparent
                img.rect(x0 + 2 * scale, y0 + 2 * scale, cw - 4 * scale, cw - 4 * scale, col)
            # One marker block of a different colour, at a native-odd offset. Without it the sheet
            # is made only of large flat areas and is a valid 2x upscale of itself, which
            # native_scale would correctly but unhelpfully report.
            img.rect(x0 + 3 * scale, y0 + 3 * scale, scale, scale, (255, 255, 255, 255))
    return img


class Png(unittest.TestCase):
    def test_round_trip_preserves_every_pixel(self):
        src = make_sheet(3, 2)
        with tempfile.TemporaryDirectory() as d:
            p = os.path.join(d, "a.png")
            icons.write_png(p, src)
            back = icons.read_png(p)
        self.assertEqual((src.w, src.h), (back.w, back.h))
        self.assertEqual(bytes(src.px), bytes(back.px))

    def test_write_is_deterministic(self):
        src = make_sheet(2, 2)
        with tempfile.TemporaryDirectory() as d:
            a, b = os.path.join(d, "a.png"), os.path.join(d, "b.png")
            icons.write_png(a, src)
            icons.write_png(b, src)
            with open(a, "rb") as fa, open(b, "rb") as fb:
                self.assertEqual(fa.read(), fb.read())

    def test_rejects_a_non_png(self):
        with tempfile.TemporaryDirectory() as d:
            p = os.path.join(d, "x.png")
            open(p, "wb").write(b"not a png at all")
            with self.assertRaises(icons.PngError):
                icons.read_png(p)


class Grid(unittest.TestCase):
    def test_gutter_detection_recovers_the_grid(self):
        for cols, rows in ((10, 8), (9, 7), (12, 3)):
            g = icons.detect_grid(make_sheet(cols, rows))
            self.assertEqual((g["cols"], g["rows"]), (cols, rows), f"{cols}x{rows}")
            self.assertEqual(g["method"], "gutter")

    def test_detection_survives_empty_cells(self):
        g = icons.detect_grid(make_sheet(8, 6, fill_all=False))
        self.assertEqual((g["cols"], g["rows"]), (8, 6))

    def test_a_full_bleed_framed_sheet_cannot_be_detected_and_says_so(self):
        # Framed tiles whose border touches the cell edge leave no transparent gutter, so the
        # alpha channel carries no grid information at all. Refusing is the honest answer.
        img = make_sheet(6, 4, gutter=0, frame=True)
        with self.assertRaises(icons.PngError) as cm:
            icons.detect_grid(img)
        self.assertIn("sheets.csv asserts no cols/rows", str(cm.exception))

    def test_a_full_bleed_sheet_uses_the_asserted_grid(self):
        img = make_sheet(6, 4, gutter=0, frame=True)
        g = icons.detect_grid(img, {"cols": "6", "rows": "4"})
        self.assertEqual((g["cols"], g["rows"], g["method"]), (6, 4, "asserted"))
        rects = icons.cell_rects(g)
        self.assertEqual(len(rects), 4)
        self.assertEqual(len(rects[0]), 6)
        self.assertIsNotNone(icons.tight_box(img, rects[2][3]))

    def test_a_disagreement_with_the_manifest_is_a_hard_error(self):
        img = make_sheet(10, 8)
        with self.assertRaises(icons.PngError) as cm:
            icons.detect_grid(img, {"cols": "9", "rows": "8"})
        msg = str(cm.exception)
        self.assertIn("9x8", msg)          # names what was asserted
        self.assertIn("10x8", msg)         # and what was found
        self.assertIn("Neither is preferred", msg)

    def test_agreement_with_the_manifest_passes(self):
        icons.detect_grid(make_sheet(5, 4), {"cols": "5", "rows": "4"})

    def test_a_blank_sheet_is_refused(self):
        with self.assertRaises(icons.PngError):
            icons.detect_grid(icons.Image(32, 32))

    def test_native_scale_detects_pre_upscaled_art(self):
        for s in (1, 2, 4):
            img = make_sheet(4, 3, scale=s)
            self.assertEqual(icons.native_scale(img), s, f"scale {s}")

    def test_the_manifest_overrides_detected_native_scale(self):
        img = make_sheet(4, 3, scale=2)
        self.assertEqual(icons.sheet_scale({"native_scale": "1"}, img), 1)
        self.assertEqual(icons.sheet_scale({"native_scale": ""}, img), 2)
        self.assertEqual(icons.sheet_scale(None, img), 2)

    def test_cell_rects_cover_the_right_count(self):
        img = make_sheet(7, 5)
        self.assertEqual(sum(len(r) for r in icons.cell_rects(icons.detect_grid(img))), 35)


class Scaling(unittest.TestCase):
    def test_nearest_neighbour_replicates_exactly(self):
        src = icons.Image(2, 2)
        src.put(0, 0, (255, 0, 0, 255)); src.put(1, 0, (0, 255, 0, 255))
        src.put(0, 1, (0, 0, 255, 255)); src.put(1, 1, (255, 255, 0, 255))
        big = src.scale(3)
        self.assertEqual((big.w, big.h), (6, 6))
        for y in range(6):
            for x in range(6):
                self.assertEqual(big.get(x, y), src.get(x // 3, y // 3))

    def test_scale_then_downsample_is_the_identity(self):
        src = make_sheet(3, 2)
        self.assertEqual(bytes(src.scale(3).downsample(3).px), bytes(src.px))

    def test_a_scale_below_one_is_refused(self):
        with self.assertRaises(ValueError):
            icons.Image(4, 4).scale(0)

    def test_tight_box_finds_the_art_and_ignores_empty_cells(self):
        img = make_sheet(2, 2)
        rects = icons.cell_rects(icons.detect_grid(img))[0]
        box = icons.tight_box(img, rects[0])
        self.assertIsNotNone(box)
        self.assertEqual((box[2], box[3]), (12, 12))     # 16 cell minus the 2px inset each side
        self.assertIsNone(icons.tight_box(icons.Image(16, 16), (0, 0, 16, 16)))


class Text(unittest.TestCase):
    def test_digits_draw_something_and_scale(self):
        for k in (1, 2, 3):
            img = icons.Image(80, 40)
            icons.text(img, "0123", 2, 2, k)
            self.assertGreater(sum(1 for y in range(40) for x in range(80) if img.opaque(x, y)), 10 * k)

    def test_unknown_characters_are_skipped_without_error(self):
        img = icons.Image(60, 20)
        icons.text(img, "a~b", 0, 0, 1)                  # ~ has no glyph
        self.assertEqual(icons.text_w("abc", 2), 36)


class EndToEnd(unittest.TestCase):
    """The whole chain against a synthetic sheet: detect, validate, export, check the pixels."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        d = self.tmp.name
        self.saved = {k: getattr(icons, k) for k in
                      ("SHEET_DIR", "SHEETS_CSV", "KEYS_CSV", "MAP_CSV", "OUT_DIR", "ROOT")}
        icons.ROOT = d
        icons.SHEET_DIR = os.path.join(d, "sheets"); os.makedirs(icons.SHEET_DIR)
        icons.SHEETS_CSV = os.path.join(d, "sheets.csv")
        icons.KEYS_CSV = os.path.join(d, "keys.csv")
        icons.MAP_CSV = os.path.join(d, "map.csv")
        icons.OUT_DIR = os.path.join(d, "out")

        self.sheet = make_sheet(4, 3, cell=16, gutter=4, scale=2)   # 32px cells, 16px native
        icons.write_png(os.path.join(icons.SHEET_DIR, "t.png"), self.sheet)
        with open(icons.SHEETS_CSV, "w") as f:
            f.write("sheet,file,cols,rows,cell_w,cell_h,trim_mode,target_px,native_scale,source_note\n")
            f.write("t,t.png,4,3,,,union,64,,synthetic\n")
        with open(icons.KEYS_CSV, "w") as f:
            f.write("key,label,namespace,milestone,tooltip_seed\n")
            f.write("ui.res.alloy,Alloy,ui.res,M1,ingot\n")
            f.write("ui.res.wire,Wire,ui.res,M1,coil\n")
        with open(icons.MAP_CSV, "w") as f:
            f.write("key,sheet,row,col,cell_description,confidence,status\n")
            f.write("ui.res.alloy,t,1,2,a test cell,high,mapped\n")
            f.write("ui.res.wire,t,0,0,another test cell,med,mapped\n")

    def tearDown(self):
        for k, v in self.saved.items():
            setattr(icons, k, v)
        self.tmp.cleanup()

    def test_detect_agrees_with_the_asserted_grid(self):
        self.assertEqual(icons.main(["detect"]), 0)

    def test_validate_passes(self):
        self.assertEqual(icons.main(["validate", "--strict"]), 0)

    def test_export_writes_64px_icons_with_the_source_pixels_intact(self):
        self.assertEqual(icons.main(["export"]), 0)
        for key in ("ui.res.alloy", "ui.res.wire"):
            path = os.path.join(icons.OUT_DIR, f"{key}.png")
            self.assertTrue(os.path.exists(path), key)
            img = icons.read_png(path)
            self.assertEqual((img.w, img.h), (64, 64))
        # The cell is 12px native art upscaled by 5 -> 60px, centred on a multiple of 5.
        img = icons.read_png(os.path.join(icons.OUT_DIR, "ui.res.alloy.png"))
        opaque = [(x, y) for y in range(64) for x in range(64) if img.opaque(x, y)]
        w = max(x for x, _ in opaque) - min(x for x, _ in opaque) + 1
        self.assertEqual(w, 60)
        self.assertEqual(min(x for x, _ in opaque) % 5, 0)   # every source pixel on a 5x5 block

    def test_export_refuses_a_cell_outside_the_grid(self):
        with open(icons.MAP_CSV, "a") as f:
            f.write("ui.res.wire,t,9,9,off the end,low,mapped\n")
        self.assertEqual(icons.main(["export"]), 1)
        self.assertFalse(os.path.exists(icons.OUT_DIR))      # nothing written on any error

    def test_export_refuses_a_target_size_off_the_contract(self):
        with open(icons.SHEETS_CSV, "w") as f:
            f.write("sheet,file,cols,rows,cell_w,cell_h,trim_mode,target_px,native_scale,source_note\n")
            f.write("t,t.png,4,3,,,union,48,,synthetic\n")
        self.assertEqual(icons.main(["export"]), 1)

    def test_validate_reports_an_unknown_key(self):
        with open(icons.MAP_CSV, "a") as f:
            f.write("ui.res.nonesuch,t,2,2,not in the registry,low,mapped\n")
        self.assertEqual(icons.main(["validate"]), 1)

    def test_validate_reports_a_duplicate_key(self):
        with open(icons.MAP_CSV, "a") as f:
            f.write("ui.res.alloy,t,2,3,claimed twice,low,mapped\n")
        self.assertEqual(icons.main(["validate"]), 1)

    def test_validate_reports_an_empty_cell_and_export_refuses_it(self):
        sparse = make_sheet(4, 3, cell=16, gutter=4, scale=2, fill_all=False)
        icons.write_png(os.path.join(icons.SHEET_DIR, "t.png"), sparse)
        with open(icons.MAP_CSV, "w") as f:
            f.write("key,sheet,row,col,cell_description,confidence,status\n")
            f.write("ui.res.alloy,t,0,0,this cell is blank,high,mapped\n")   # (0+0)%3==0 -> skipped
        self.assertEqual(icons.main(["export"]), 1)


class Cli(unittest.TestCase):
    def test_emit_web_runs_against_the_committed_csvs(self):
        self.assertEqual(icons.main(["emit-web"]), 0)
        self.assertTrue(os.path.exists(icons.WEB_TABLE))
        body = open(icons.WEB_TABLE).read()
        self.assertIn("window.ICON_MAP", body)
        self.assertIn("window.ICON_GAPS", body)

    def test_validate_passes_on_the_committed_csvs(self):
        self.assertEqual(icons.main(["validate"]), 0)

    def test_export_is_a_no_op_while_no_cell_is_located(self):
        self.assertEqual(icons.main(["export", "--dry-run"]), 0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
