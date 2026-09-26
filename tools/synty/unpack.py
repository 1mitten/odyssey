"""Unpack a Synty .unitypackage into the project, filtered by folder, preserving Synty's GUIDs.

A .unitypackage is a gzipped tar of <guid>/pathname, <guid>/asset and <guid>/asset.meta. Writing the
asset and its .meta at the recorded path is exactly what Unity's import does, so the GUIDs the
committed ModuleCatalogue.asset points at are kept -- and, unlike Unity's importer, a folder can be
left out. That matters: Battle Royale and Shops each ship their own PolygonGeneric under identical
GUIDs, and importing them whole silently overwrites the one the other packs share
(docs/setup/local-dev.md §8a has the full recipe and the pack table).

usage:
  python3 tools/synty/unpack.py list PKG
  python3 tools/synty/unpack.py extract PKG . --only Assets/Synty/PolygonShops [--skip P] [--dry-run] [--overwrite]
  python3 tools/synty/unpack.py extract PKG . --remap Assets/SimpleForestAnimal=Assets/Synty/SimpleForestAnimal

--remap moves a pack that installs outside Assets/Synty (SIMPLE Forest Animals installs to
Assets/SimpleForestAnimal, which git does not ignore) to where licensed art belongs; --only and --skip
match the package's own paths, before the remap. Nothing is written outside Assets/Synty/ unless
--allow-outside is given, so a licensed pack cannot land one `git add .` from a commit.

Existing files are left alone unless --overwrite. Standard library only. Never commit the output:
Assets/Synty/ is gitignored and must stay so.
"""
import argparse, collections, os, sys, tarfile


def entries(pkg):
    by_guid = collections.defaultdict(dict)
    with tarfile.open(pkg, "r:gz") as tar:
        for m in tar:
            if not m.isfile():
                continue
            parts = m.name.lstrip("./").split("/")
            if len(parts) != 2:
                continue
            guid, kind = parts
            if kind == "pathname":
                by_guid[guid]["path"] = tar.extractfile(m).read().decode("utf-8").splitlines()[0].strip()
            elif kind in ("asset", "asset.meta"):
                by_guid[guid][kind] = m.name
    return by_guid


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["list", "extract"])
    ap.add_argument("pkg")
    ap.add_argument("project", nargs="?")
    ap.add_argument("--only", action="append", default=[])
    ap.add_argument("--skip", action="append", default=[])
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--overwrite", action="store_true")
    ap.add_argument("--remap", action="append", default=[], metavar="OLD=NEW")
    ap.add_argument("--allow-outside", action="store_true")
    a = ap.parse_args()

    by_guid = entries(a.pkg)
    if a.cmd == "list":
        c = collections.Counter("/".join(e["path"].split("/")[:3]) for e in by_guid.values() if "path" in e)
        for k, n in sorted(c.items()):
            print(f"{n:6d}  {k}")
        return

    def wanted(p):
        if a.only and not any(p == o or p.startswith(o.rstrip("/") + "/") for o in a.only):
            return False
        return not any(p == s or p.startswith(s.rstrip("/") + "/") for s in a.skip)

    remaps = [r.split("=", 1) for r in a.remap]

    def moved(p):
        for old, new in remaps:
            old = old.rstrip("/")
            if p == old or p.startswith(old + "/"):
                return new.rstrip("/") + p[len(old):]
        return p

    picked = {g: e for g, e in by_guid.items() if "path" in e and wanted(e["path"])}
    outside = sorted({moved(e["path"]) for e in picked.values()
                      if not (moved(e["path"]) + "/").startswith("Assets/Synty/")})
    if outside and not a.allow_outside:
        print("refusing: these would land outside Assets/Synty/ (use --remap, or --allow-outside):", file=sys.stderr)
        for p in outside[:10]:
            print("  " + p, file=sys.stderr)
        return 1
    written = skipped = 0
    with tarfile.open(a.pkg, "r:gz") as tar:
        for g, e in picked.items():
            dest = os.path.join(a.project, *moved(e["path"]).split("/"))
            if a.dry_run:
                continue
            if "asset" in e:
                if os.path.exists(dest) and not a.overwrite:
                    skipped += 1
                    continue
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                with open(dest, "wb") as f:
                    f.write(tar.extractfile(e["asset"]).read())
            else:
                os.makedirs(dest, exist_ok=True)
            if "asset.meta" in e and (a.overwrite or not os.path.exists(dest + ".meta")):
                with open(dest + ".meta", "wb") as f:
                    f.write(tar.extractfile(e["asset.meta"]).read())
            written += 1
    print(f"{len(picked)} entries matched, {written} written, {skipped} skipped (exists)")


if __name__ == "__main__":
    sys.exit(main())
