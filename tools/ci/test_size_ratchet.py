"""Tests for the file-size ratchet. Run with the other CI tool tests:

    python3 -m unittest discover -s tools/ci -t tools/ci
"""
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import size_ratchet


def write(root: Path, relative: str, lines: int) -> Path:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("".join(f"// line {i}\n" for i in range(lines)), encoding="utf-8")
    return path


class Counting(unittest.TestCase):
    def test_counts_newlines_like_wc(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "a.cs"
            path.write_bytes(b"one\ntwo\nthree")  # no trailing newline: wc -l says 2
            self.assertEqual(size_ratchet.count_lines(path), 2)

    def test_tests_and_generated_files_are_exempt(self):
        self.assertTrue(size_ratchet.is_exempt("Assets/Odyssey/Tests/Sim/GoldenMasterTests.cs"))
        self.assertTrue(size_ratchet.is_exempt("Assets/Odyssey/Presentation/Tests/HudShellTests.cs"))
        self.assertTrue(size_ratchet.is_exempt("Assets/Odyssey/Hud/Registry.g.cs"))
        self.assertFalse(size_ratchet.is_exempt("Assets/Odyssey/Hud/Registry.cs"))
        self.assertFalse(size_ratchet.is_exempt("Assets/Odyssey/Sim/Pawns/JobSystem.cs"))

    def test_only_the_production_roots_are_walked(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write(root, "Assets/Odyssey/Sim/Big.cs", 10)
            write(root, "Assets/Odyssey/Sim/Tests/BigTests.cs", 10)
            write(root, "Assets/Odyssey/Hud/Registry.g.cs", 10)
            write(root, "Assets/Synty/Pack/Thing.cs", 10)
            write(root, "tools/dotnet/Odyssey.Sim/Program.cs", 10)
            sizes = size_ratchet.production_files(root)
            self.assertEqual(sizes, {"Assets/Odyssey/Sim/Big.cs": 10})


class Checking(unittest.TestCase):
    def ceilings(self, files: dict, threshold: int = 100) -> dict:
        return {"threshold": threshold, "round": 50, "files": files}

    def test_a_listed_file_within_its_ceiling_passes(self):
        sizes = {"Assets/Odyssey/Sim/Big.cs": 120}
        self.assertEqual(size_ratchet.check(sizes, self.ceilings({"Assets/Odyssey/Sim/Big.cs": 150})), [])

    def test_a_listed_file_over_its_ceiling_fails_and_says_by_how_much(self):
        sizes = {"Assets/Odyssey/Sim/Big.cs": 163}
        problems = size_ratchet.check(sizes, self.ceilings({"Assets/Odyssey/Sim/Big.cs": 150}))
        self.assertEqual(len(problems), 1)
        self.assertIn("163 lines, ceiling 150 (+13)", problems[0])

    def test_an_unlisted_file_at_the_threshold_fails(self):
        sizes = {"Assets/Odyssey/Sim/New.cs": 100}
        problems = size_ratchet.check(sizes, self.ceilings({}))
        self.assertEqual(len(problems), 1)
        self.assertIn("not listed", problems[0])

    def test_an_unlisted_file_under_the_threshold_passes(self):
        sizes = {"Assets/Odyssey/Sim/Small.cs": 99}
        self.assertEqual(size_ratchet.check(sizes, self.ceilings({})), [])

    def test_a_listed_file_that_was_deleted_or_moved_is_not_a_failure(self):
        self.assertEqual(size_ratchet.check({}, self.ceilings({"Assets/Odyssey/Sim/Gone.cs": 900})), [])

    def test_the_committed_ceilings_hold_on_this_tree(self):
        """The real gate, run on the real tree: the same check CI runs."""
        if not size_ratchet.CEILINGS.exists():
            self.skipTest("no committed ceilings beside this test")
        problems = size_ratchet.check(size_ratchet.production_files(), size_ratchet.load_ceilings())
        self.assertEqual(problems, [], "\n".join(problems))


class Baking(unittest.TestCase):
    def test_bake_lists_files_at_or_over_the_threshold_with_slack_rounded_up(self):
        sizes = {"b.cs": 120, "a.cs": 99, "c.cs": 100, "d.cs": 150}
        baked = size_ratchet.bake(sizes, threshold=100, step=50, slack=25)
        # 120 + 25 -> 150; 100 + 25 -> 150; 150 + 25 -> 200; 99 is under the threshold.
        self.assertEqual(baked["files"], {"b.cs": 150, "c.cs": 150, "d.cs": 200})
        self.assertEqual(list(baked["files"]), ["b.cs", "c.cs", "d.cs"], "sorted, for a stable diff")
        self.assertEqual(baked["threshold"], 100)
        self.assertEqual(baked["round"], 50)
        self.assertEqual(baked["slack"], 25)

    def test_the_headroom_is_at_least_the_slack_and_under_slack_plus_step(self):
        for lines in range(800, 1200):
            ceiling = size_ratchet.bake({"x.cs": lines}, 800, 50, 25)["files"]["x.cs"]
            self.assertGreaterEqual(ceiling - lines, 25, lines)
            self.assertLess(ceiling - lines, 75, lines)

    def test_round_up(self):
        self.assertEqual(size_ratchet.round_up(100, 50), 100)
        self.assertEqual(size_ratchet.round_up(101, 50), 150)
        self.assertEqual(size_ratchet.round_up(4655, 50), 4700)

    def test_the_command_line_round_trips(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            big = write(root, "Assets/Odyssey/Sim/Big.cs", 120)
            write(root, "Assets/Odyssey/Sim/Small.cs", 20)
            ceilings = root / "size-ceilings.json"
            ceilings.write_text(json.dumps({"threshold": 100, "round": 50, "slack": 25, "files": {}}), encoding="utf-8")

            # Unlisted and over the threshold: refused.
            self.assertEqual(size_ratchet.main(["--root", str(root), "--ceilings", str(ceilings)]), 1)
            # Baked: listed at 150, and the check holds.
            self.assertEqual(size_ratchet.main(["--bake", "--root", str(root), "--ceilings", str(ceilings)]), 0)
            self.assertEqual(json.loads(ceilings.read_text())["files"], {"Assets/Odyssey/Sim/Big.cs": 150})
            self.assertEqual(size_ratchet.main(["--root", str(root), "--ceilings", str(ceilings)]), 0)
            # Grown past the ceiling: refused again.
            write(root, "Assets/Odyssey/Sim/Big.cs", 151)
            self.assertEqual(size_ratchet.main(["--root", str(root), "--ceilings", str(ceilings)]), 1)
            # Shrunk: the ratchet tightens on the next bake (101 + 25 rounds to 150, from 200).
            self.assertEqual(size_ratchet.main(["--bake", "--root", str(root), "--ceilings", str(ceilings)]), 0)
            self.assertEqual(json.loads(ceilings.read_text())["files"], {"Assets/Odyssey/Sim/Big.cs": 200})
            write(root, "Assets/Odyssey/Sim/Big.cs", 101)
            self.assertEqual(size_ratchet.main(["--bake", "--root", str(root), "--ceilings", str(ceilings)]), 0)
            self.assertEqual(json.loads(ceilings.read_text())["files"], {"Assets/Odyssey/Sim/Big.cs": 150})
            write(root, "Assets/Odyssey/Sim/Big.cs", 60)
            self.assertEqual(size_ratchet.main(["--bake", "--root", str(root), "--ceilings", str(ceilings)]), 0)
            self.assertEqual(json.loads(ceilings.read_text())["files"], {}, "under the threshold it leaves the list")
            self.assertTrue(big.exists())


if __name__ == "__main__":
    unittest.main()
