"""The file-size ratchet: no production source file grows past its ceiling unnoticed.

    python3 tools/ci/size_ratchet.py            check; exit 1 on any file over its ceiling
    python3 tools/ci/size_ratchet.py --bake     rewrite tools/ci/size-ceilings.json from today's sizes
    python3 tools/ci/size_ratchet.py --list     print every file the ratchet watches, largest first

Why this exists (docs/audit/2026-09-26-architecture-review.md §5). Between the two audits the
composition root went from 2,408 lines to 4,655, the chunk renderer from 1,848 to 4,548 and the
HUD shell from about 7,000 to 16,257 across its partials, and not one of those steps was a
decision anybody took: each feature added its method to the file that already had the field it
needed, and the review that would have said "put it beside, not inside" never saw a diff large
enough to say it on. A ceiling turns the growth into one visible line in the pull request that
grows the file.

The rule. Every production ``.cs`` file at or over ``threshold`` lines is listed in
``size-ceilings.json`` with a ceiling. A listed file may not grow past its ceiling, and a file
not listed may not reach the threshold. Either way the pull request has two ways out, and both
are decisions the diff shows: move code into a file of its own, or raise the ceiling here and say
why in the commit. ``--bake`` writes today's sizes back, rounded up to the next ``round`` lines
after adding ``slack`` lines, so a fix of a few lines never trips it and a feature-sized addition
always does; the ratchet tightens by itself when a file shrinks and loosens only by hand.

What is counted: newline characters, so the number is ``wc -l``'s. What is exempt: anything
under a ``Tests`` folder and every generated ``*.g.cs``. Standard library only, so it runs in the
hosted content-gates job before anything is installed, and locally on both machines.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CEILINGS = Path(__file__).resolve().parent / "size-ceilings.json"

# The production assemblies. Tests are exempt because a long test file is a long list of cases,
# and generated files because their length is the registry's, not a design.
PRODUCTION_ROOTS = (
    "Assets/Odyssey/Sim.Contracts",
    "Assets/Odyssey/Sim",
    "Assets/Odyssey/Hud",
    "Assets/Odyssey/Presentation",
    "Assets/Editor/Odyssey",
)

README = (
    "Every production .cs file at or over `threshold` lines is listed here with a ceiling it may "
    "not grow past. A pull request that grows one either moves code out into a file of its own or "
    "raises the number here and says why in the commit. `python3 tools/ci/size_ratchet.py --bake` "
    "rewrites the list from today's sizes plus `slack` lines, rounded up to the next `round`. "
    "docs/audit/2026-09-26-architecture-review.md section 5 is the reason; docs/plans/refactoring.md "
    "is the order the listed files come down in."
)


def is_exempt(relative: str) -> bool:
    parts = relative.split("/")
    return "Tests" in parts or relative.endswith(".g.cs")


def count_lines(path: Path) -> int:
    """``wc -l``: the number of newline characters, whatever the encoding."""
    with path.open("rb") as handle:
        return handle.read().count(b"\n")


def production_files(root: Path = ROOT) -> dict[str, int]:
    """Every production source file under the watched roots, with its line count."""
    sizes: dict[str, int] = {}
    for top in PRODUCTION_ROOTS:
        base = root / top
        if not base.is_dir():
            continue
        for dirpath, _dirnames, filenames in os.walk(base):
            for name in filenames:
                if not name.endswith(".cs"):
                    continue
                path = Path(dirpath) / name
                relative = path.relative_to(root).as_posix()
                if is_exempt(relative):
                    continue
                sizes[relative] = count_lines(path)
    return sizes


def load_ceilings(path: Path = CEILINGS) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def round_up(lines: int, step: int) -> int:
    return ((lines + step - 1) // step) * step


def bake(sizes: dict[str, int], threshold: int, step: int, slack: int) -> dict:
    """Today's sizes as ceilings: every file at or over the threshold, plus ``slack`` lines,
    rounded up to ``step`` — so the headroom is between ``slack`` and ``slack + step - 1``."""
    files = {
        relative: round_up(lines + slack, step)
        for relative, lines in sorted(sizes.items())
        if lines >= threshold
    }
    return {"_readme": README, "threshold": threshold, "round": step, "slack": slack, "files": files}


def check(sizes: dict[str, int], ceilings: dict) -> list[str]:
    """The violations, as the lines to print. Empty means the ratchet holds."""
    threshold = int(ceilings["threshold"])
    listed: dict[str, int] = ceilings.get("files", {})
    problems: list[str] = []

    for relative, ceiling in sorted(listed.items()):
        lines = sizes.get(relative)
        if lines is None:
            continue  # moved or deleted: nothing to hold; --bake drops the row
        if lines > ceiling:
            problems.append(
                f"  {relative}: {lines} lines, ceiling {ceiling} (+{lines - ceiling})"
            )

    for relative, lines in sorted(sizes.items()):
        if relative in listed or lines < threshold:
            continue
        problems.append(
            f"  {relative}: {lines} lines and not listed; the threshold is {threshold}"
        )

    return problems


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--bake", action="store_true", help="rewrite the ceilings from today's sizes")
    mode.add_argument("--list", action="store_true", help="print the watched files, largest first")
    parser.add_argument("--ceilings", type=Path, default=CEILINGS, help=argparse.SUPPRESS)
    parser.add_argument("--root", type=Path, default=ROOT, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)

    sizes = production_files(args.root)

    if args.bake:
        existing = load_ceilings(args.ceilings) if args.ceilings.exists() else {}
        threshold = int(existing.get("threshold", 800))
        step = int(existing.get("round", 50))
        slack = int(existing.get("slack", 25))
        baked = bake(sizes, threshold, step, slack)
        with args.ceilings.open("w", encoding="utf-8") as handle:
            json.dump(baked, handle, indent=2)
            handle.write("\n")
        print(f"size_ratchet: {len(baked['files'])} files at or over {threshold} lines written to "
              f"{args.ceilings.relative_to(args.root) if args.ceilings.is_relative_to(args.root) else args.ceilings}")
        return 0

    ceilings = load_ceilings(args.ceilings)

    if args.list:
        listed = ceilings.get("files", {})
        rows = sorted(((sizes.get(f, 0), c, f) for f, c in listed.items()), reverse=True)
        try:
            for lines, ceiling, relative in rows:
                print(f"{lines:6d} / {ceiling:5d}  {relative}")
        except BrokenPipeError:
            pass  # piped into head: nothing to report
        return 0

    problems = check(sizes, ceilings)
    if not problems:
        print(f"size_ratchet: {len(ceilings.get('files', {}))} ceilings hold; "
              f"nothing unlisted is at or over {ceilings['threshold']} lines")
        return 0

    print("size_ratchet: a source file has grown past what the project agreed to carry:")
    print("\n".join(problems))
    print(
        "\nTwo ways out, and the diff shows either: move the new code into a file of its own "
        "(docs/code-map.md says where each kind of thing goes), or raise the ceiling in "
        "tools/ci/size-ceilings.json in this pull request and say why in the commit message. "
        "python3 tools/ci/size_ratchet.py --bake rewrites the file from today's sizes."
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
