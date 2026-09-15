#!/usr/bin/env python3
"""Emit an Artifact-publishable copy of a self-contained mockup page.

A mockup in docs/reference/mockups/ is a complete HTML document, because it has to open from disk
and be servable by a static host. Publishing one as a Claude Artifact needs the opposite: the
platform wraps the file in its own document skeleton, so the file must carry no doctype, <html>,
<head> or <body> of its own. This writes that variant beside the original so publishing is
reproducible rather than a hand edit nobody can repeat.

    python3 tools/mockups/artifact_body.py docs/reference/mockups/hud-v2.html
    python3 tools/mockups/artifact_body.py --check <same>

Output: <name>.artifact.html
"""
from __future__ import annotations
import os, re, sys

DROP = re.compile(r"^\s*(<!doctype[^>]*>|</?html[^>]*>|</?head[^>]*>|</?body[^>]*>"
                  r"|<meta\s+charset[^>]*>|<meta\s+name=[\"']viewport[^>]*>)\s*$",
                  re.IGNORECASE)


def body_of(src: str) -> str:
    kept = [ln for ln in src.splitlines() if not DROP.match(ln)]
    return "\n".join(kept).strip() + "\n"


def main(argv):
    check = "--check" in argv
    paths = [a for a in argv if not a.startswith("--")]
    if not paths:
        print(__doc__)
        return 2
    rc = 0
    for src_path in paths:
        with open(src_path) as f:
            out = body_of(f.read())
        dst = src_path[:-5] + ".artifact.html" if src_path.endswith(".html") else src_path + ".artifact.html"
        if check:
            if not os.path.exists(dst) or open(dst).read() != out:
                print(f"stale: {dst}. Run: python3 tools/mockups/artifact_body.py {src_path}")
                rc = 1
            else:
                print(f"ok: {dst} is current")
        else:
            with open(dst, "w") as f:
                f.write(out)
            print(f"ok: wrote {dst} ({len(out.splitlines())} lines)")
    return rc


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
