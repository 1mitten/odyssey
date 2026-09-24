"""Tests for tools/ci/tiers.py: python3 -m unittest discover -s tools/ci -t tools/ci"""

import subprocess
import sys
import unittest
from pathlib import Path

from tiers import KEYS, RULES, classify, select

ROOT = Path(__file__).resolve().parents[2]


def ran(paths, event="pull_request", labels=None, unchanged=False):
    needs = select(paths, event, labels, unchanged).needs
    return {key for key in KEYS if needs[key]}


class PullRequests(unittest.TestCase):
    def test_a_docs_only_change_runs_no_test_tier(self):
        self.assertEqual(ran(["docs/journal.md", "CLAUDE.md", "docs/design/15-skills.md"]), set())

    def test_a_simulation_change_runs_every_tier_but_the_measurements(self):
        self.assertEqual(ran(["Assets/Odyssey/Sim/World/Grid.cs"]), {"sim", "hud", "unity", "simu"})

    def test_a_def_is_simulation(self):
        self.assertEqual(ran(["Assets/Odyssey/Defs/Core/Jobs.xml"]), {"sim", "hud", "unity", "simu"})

    def test_a_contracts_change_reaches_the_hud(self):
        self.assertIn("hud", ran(["Assets/Odyssey/Sim.Contracts/WorldSnapshot.cs"]))

    def test_a_hud_change_skips_the_sim_tests_in_both_tiers(self):
        self.assertEqual(ran(["Assets/Odyssey/Hud/HudLayout.cs"]), {"hud", "unity"})

    def test_a_presentation_change_runs_the_hud_tests_because_they_read_its_source(self):
        self.assertEqual(ran(["Assets/Odyssey/Presentation/Ui/Hud.uss"]), {"hud", "unity"})

    def test_a_meta_file_beside_the_sim_folder_is_still_unity_work(self):
        self.assertEqual(ran(["Assets/Odyssey/Sim.meta"]), {"hud", "unity"})

    def test_one_sim_path_among_many_hud_paths_runs_the_sim(self):
        paths = ["Assets/Odyssey/Hud/A.cs"] * 20 + ["Assets/Odyssey/Tests/Sim/GoldenMasterTests.cs"]
        self.assertIn("sim", ran(paths))
        self.assertIn("simu", ran(paths))

    def test_a_content_table_runs_only_the_gates(self):
        self.assertEqual(ran(["docs/design/icon-keys.csv"]), set())

    def test_a_workflow_change_runs_everything(self):
        self.assertEqual(ran([".github/workflows/ci.yml"]), {"sim", "hud", "unity", "simu"})

    def test_a_script_change_runs_everything(self):
        self.assertEqual(ran(["scripts/unity.sh"]), {"sim", "hud", "unity", "simu"})

    def test_a_package_change_runs_everything(self):
        self.assertEqual(ran(["Packages/manifest.json"]), {"sim", "hud", "unity", "simu"})

    def test_a_path_no_rule_knows_runs_everything(self):
        self.assertEqual(ran(["somewhere/new.txt"]), {"sim", "hud", "unity", "simu"})
        self.assertEqual(ran(["tools/audio/fetch.py"]), {"sim", "hud", "unity", "simu"})

    def test_no_paths_at_all_runs_everything_rather_than_nothing(self):
        self.assertEqual(ran([]), {"sim", "hud", "unity", "simu"})
        self.assertEqual(ran(["", "  "]), {"sim", "hud", "unity", "simu"})

    def test_windows_separators_are_read(self):
        self.assertEqual(ran(["Assets\\Odyssey\\Sim\\World\\Grid.cs"]), {"sim", "hud", "unity", "simu"})

    def test_the_measurements_run_only_when_asked(self):
        self.assertNotIn("perf", ran(["Assets/Odyssey/Presentation/Rendering/ChunkRenderer.cs"]))
        self.assertIn("perf", ran(["docs/journal.md"], labels=["ci:perf"]))
        self.assertIn("unity", ran(["docs/journal.md"], labels=["ci:perf"]))

    def test_ci_full_runs_everything_on_a_docs_change(self):
        self.assertEqual(ran(["docs/journal.md"], labels=["ci:full"]), {"sim", "hud", "unity", "simu"})


class OtherEvents(unittest.TestCase):
    def test_a_push_to_main_runs_the_hosted_tiers_and_not_unity(self):
        self.assertEqual(ran(["docs/journal.md"], event="push"), {"sim", "hud"})

    def test_the_nightly_runs_everything_with_the_measurements(self):
        self.assertEqual(ran([], event="schedule"), set(KEYS))

    def test_a_nightly_on_an_unmoved_main_runs_nothing(self):
        self.assertEqual(ran([], event="schedule", unchanged=True), set())

    def test_a_hand_started_run_is_everything(self):
        self.assertEqual(ran([], event="workflow_dispatch"), set(KEYS))


class TheTable(unittest.TestCase):
    def test_every_tracked_path_is_claimed_or_deliberately_full(self):
        """A path no rule knows runs everything, which is safe but slow; this lists the ones
        that do today, so a new folder is a decision rather than a surprise."""
        tracked = subprocess.run(["git", "ls-files"], cwd=ROOT, capture_output=True, text=True,
                                 check=True).stdout.splitlines()
        unclaimed = sorted({"/".join(path.split("/")[:2]) for path in tracked if classify(path) is None})
        deliberate = (".github/", "scripts/", "tools/almanac", "tools/audio", "tools/dotnet/")
        for prefix in unclaimed:
            self.assertTrue(prefix.startswith(deliberate),
                            f"{prefix} is claimed by no rule, so every change to it runs every "
                            "tier; give it a row in tools/ci/tiers.py or add it here on purpose")

    def test_the_narrow_rows_sit_above_the_broad_ones(self):
        broad = next(i for i, rule in enumerate(RULES) if rule.pattern == "Assets/*")
        for i, rule in enumerate(RULES):
            if rule.pattern.startswith("Assets/") and rule.pattern != "Assets/*":
                self.assertLess(i, broad, f"{rule.pattern} is shadowed by Assets/*")

    def test_the_command_line_prints_one_line_per_key(self):
        result = subprocess.run([sys.executable, str(Path(__file__).with_name("tiers.py")),
                                 "--event", "pull_request"],
                                input="Assets/Odyssey/Hud/HudLayout.cs\n", capture_output=True,
                                text=True, check=True)
        self.assertEqual(result.stdout.split(), [
            "sim=false", "hud=true", "unity=true", "simu=false", "perf=false"])
        self.assertIn("HUD", result.stderr)


if __name__ == "__main__":
    unittest.main()
