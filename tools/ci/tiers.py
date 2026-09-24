"""Which CI tiers a change needs, decided from the paths it touches.

    python3 tools/ci/tiers.py --event pull_request --labels "ci:perf" < changed-paths.txt

Reads one changed path per line on stdin and prints ``key=true|false`` lines for GitHub
Actions' ``$GITHUB_OUTPUT``, then a short reason on stderr. The keys:

    sim     the fast tier's Sim tests and the Long tier
    hud     the fast tier's Hud tests
    unity   the Unity tier (EditMode, PlayMode, the headless day)
    simu    the Sim tests again inside Unity EditMode, under Mono
    perf    the PlayMode measurements (``Category("Measurement")``)

The content gates are not a key: they cost seconds and run on every change.

**Selection is by assembly, never by feature.** A path is mapped to the assembly it belongs to,
and the tiers that assembly can break are the ones that run; the compiler proves those edges.
Choosing tests *inside* a tier ("touched storage, run the storage tests") is deliberately not
done: the goldens, ``RegistryTests`` and the source-reading lint tests fail far from the edit
that broke them, which is the whole reason they exist. ``docs/process.md`` §5.

**Only skipping needs a rule.** Every path is matched against the table below, first match
wins, and a path no row claims runs everything. A new top-level folder, a new tool or a new
assembly therefore costs a full run until somebody decides otherwise, rather than silently
running nothing.

Standard library only, so it runs in the hosted job before anything is installed.
"""

from __future__ import annotations

import argparse
import fnmatch
import sys
from dataclasses import dataclass

# What each kind of change needs. ``full`` is everything but the measurements.
NOTHING = frozenset()
GATES_ONLY = NOTHING
HUD_SIDE = frozenset({"hud", "unity"})
SIM_SIDE = frozenset({"sim", "hud", "unity", "simu"})
FULL = frozenset({"sim", "hud", "unity", "simu"})

KEYS = ("sim", "hud", "unity", "simu", "perf")


@dataclass(frozen=True)
class Rule:
    pattern: str
    needs: frozenset
    why: str


# First match wins, so the narrow rows sit above the broad ones. Patterns are fnmatch globs
# against the repository-relative path, where ``*`` also crosses ``/``.
RULES: tuple[Rule, ...] = (
    # --- The simulation: everything downstream of it can break. ---------------------------
    # Hud references Sim.Contracts; RegistryTests reads the Defs off disk; Presentation and
    # PlayMode reference Sim; and the Sim tests run a second time under Unity's Mono, which is
    # the only place the Mono-versus-CoreCLR hash agreement is checked.
    Rule("Assets/Odyssey/Sim/*", SIM_SIDE, "simulation"),
    Rule("Assets/Odyssey/Sim.Contracts/*", SIM_SIDE, "simulation contracts"),
    Rule("Assets/Odyssey/Defs/*", SIM_SIDE, "content Defs"),
    Rule("Assets/Odyssey/Tests/Sim/*", SIM_SIDE, "simulation tests"),
    Rule("tools/dotnet/Odyssey.Sim/*", SIM_SIDE, "fast-tier Sim project"),
    Rule("tools/dotnet/Odyssey.Sim.Contracts/*", SIM_SIDE, "fast-tier contracts project"),
    Rule("tools/dotnet/Odyssey.Tests.Sim/*", SIM_SIDE, "fast-tier Sim test project"),
    Rule("tools/dotnet/Odyssey.SaveProbe/*", SIM_SIDE, "save probe"),
    # --- Everything else Unity compiles. ---------------------------------------------------
    # The Hud tests read Presentation's C#, its style sheet and its fonts off disk
    # (RegistryTests, HudFontTests, HudStyleSheetTests), so a Presentation change runs them.
    Rule("Assets/Odyssey/Hud/*", HUD_SIDE, "HUD"),
    Rule("Assets/Odyssey/Tests/Hud/*", HUD_SIDE, "HUD tests"),
    Rule("tools/dotnet/Odyssey.Hud/*", HUD_SIDE, "fast-tier Hud project"),
    Rule("tools/dotnet/Odyssey.Tests.Hud/*", HUD_SIDE, "fast-tier Hud test project"),
    Rule("Assets/Odyssey/Presentation/*", HUD_SIDE, "presentation"),
    Rule("Assets/Odyssey/Tests/PlayMode/*", HUD_SIDE, "PlayMode tests"),
    Rule("Assets/Editor/*", HUD_SIDE, "editor tooling"),
    Rule("Assets/Art/*", HUD_SIDE, "committed art"),
    Rule("Assets/Scenes/*", HUD_SIDE, "scenes"),
    Rule("Assets/Settings/*", HUD_SIDE, "render settings"),
    Rule("Assets/Resources/*", HUD_SIDE, "resources"),
    Rule("Assets/*", HUD_SIDE, "other Unity assets"),
    # A package or a project setting can change the test framework, the scripting backend or
    # the compiler, which is to say the ground every tier stands on.
    Rule("Packages/*", FULL, "packages"),
    Rule("ProjectSettings/*", FULL, "project settings"),
    Rule(".unity-version", FULL, "editor version"),
    # --- Generated content and the tools with their own tests: the gates cover them. -------
    # A CSV edit regenerates Registry.g.cs under Assets/Odyssey/Hud, which the Hud row claims,
    # and the --check gates fail on a CSV whose generated files were not rebuilt.
    Rule("docs/design/*.csv", GATES_ONLY, "content tables"),
    Rule("tools/wiki/*", GATES_ONLY, "wiki generator"),
    Rule("tools/icons/*", GATES_ONLY, "icon pipeline"),
    Rule("tools/mockups/*", GATES_ONLY, "mockup tooling"),
    Rule("tools/perf/*", GATES_ONLY, "trace reader"),
    Rule("tools/ci/*", GATES_ONLY, "this selector"),
    # --- Words. -----------------------------------------------------------------------------
    Rule("docs/*", NOTHING, "documentation"),
    Rule("art-source/*", NOTHING, "source art outside Assets"),
    Rule("*.md", NOTHING, "markdown"),
    Rule(".claude/*", NOTHING, "agent settings"),
    Rule(".mcp.json", NOTHING, "agent settings"),
    Rule(".gitignore", NOTHING, "gitignore"),
    # Not listed, so a full run: .github/, scripts/, tools/dotnet/ outside the projects above,
    # tools/almanac, tools/audio, and anything added later.
)


def classify(path: str) -> Rule | None:
    """The first rule that claims a path, or None for one no rule knows."""
    path = path.strip().replace("\\", "/")
    for rule in RULES:
        if fnmatch.fnmatchcase(path, rule.pattern):
            return rule
    return None


@dataclass
class Selection:
    needs: dict
    reasons: list

    def lines(self) -> list[str]:
        return [f"{key}={'true' if self.needs[key] else 'false'}" for key in KEYS]


def select(paths: list[str], event: str, labels: list[str] | None = None,
           unchanged: bool = False) -> Selection:
    """
    What to run for a change.

    ``event`` is GitHub's event name. A pull request is selected from its paths; a push to main
    runs the hosted tiers whole and leaves the Unity tier to the nightly, because branch
    protection requires a PR to be up to date, so the tree a merge produces is the tree its PR
    already tested; a scheduled or hand-started run is everything, measurements included.
    ``unchanged`` is a nightly whose commit the last nightly already passed on.
    """
    labels = labels or []
    needs = {key: False for key in KEYS}
    reasons: list[str] = []

    if event in ("schedule", "workflow_dispatch"):
        if unchanged:
            return Selection(needs, [f"{event}: main has not moved since the last green nightly"])
        needs.update({key: True for key in KEYS})
        return Selection(needs, [f"{event}: everything, measurements included"])

    if event == "push":
        needs.update({key: True for key in FULL})
        needs["unity"] = needs["simu"] = False
        return Selection(needs, ["push to main: the hosted tiers whole; Unity is the nightly's, "
                                 "since the PR already tested this exact tree"])

    if "ci:full" in labels:
        needs.update({key: True for key in FULL})
        reasons.append("label ci:full")
    if "ci:perf" in labels:
        needs["perf"] = needs["unity"] = True
        reasons.append("label ci:perf")

    unknown: list[str] = []
    seen: dict[str, int] = {}
    for path in paths:
        if not path.strip():
            continue
        rule = classify(path)
        if rule is None:
            unknown.append(path.strip())
            continue
        seen[rule.why] = seen.get(rule.why, 0) + 1
        for key in rule.needs:
            needs[key] = True

    if unknown:
        needs.update({key: True for key in FULL})
        shown = ", ".join(unknown[:5]) + (" ..." if len(unknown) > 5 else "")
        reasons.append(f"no rule claims {len(unknown)} path(s), so everything runs: {shown}")
    for why, count in sorted(seen.items(), key=lambda item: -item[1]):
        reasons.append(f"{count} x {why}")
    if not any(path.strip() for path in paths):
        # An empty diff is a question we cannot answer from paths; answer it the safe way.
        needs.update({key: True for key in FULL})
        reasons.append("no changed paths were read, so everything runs")
    return Selection(needs, reasons)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--event", required=True, help="github.event_name")
    parser.add_argument("--labels", default="", help="comma- or space-separated PR labels")
    parser.add_argument("--unchanged", action="store_true",
                        help="a nightly on a commit the last green nightly already covered")
    args = parser.parse_args(argv)

    labels = [label for label in args.labels.replace(",", " ").split() if label]
    paths = [line.rstrip("\n") for line in sys.stdin]
    selection = select(paths, args.event, labels, args.unchanged)

    for line in selection.lines():
        print(line)
    for reason in selection.reasons:
        print(f"select: {reason}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
