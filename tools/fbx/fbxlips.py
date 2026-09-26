"""The mouth: which UV cells the lower face samples, and whether the mouth polygons share corners with the face."""
import sys
from collections import defaultdict
sys.path.insert(0, __file__.rsplit("\\", 1)[0].rsplit("/", 1)[0])
from fbxface import read_fbx, child, objname

def run(path, want):
    top = read_fbx(path)
    objects = next(n for n in top if n[0] == "Objects")
    conns = next(n for n in top if n[0] == "Connections")
    models, geoms = {}, {}
    for o in objects[2]:
        if o[0] == "Model": models[o[1][0]] = objname(o[1][1])
        elif o[0] == "Geometry": geoms[o[1][0]] = o
    parent = defaultdict(list)
    for c in conns[2]:
        if c[1][0] == b"OO": parent[c[1][1]].append(c[1][2])
    geo = next(g for g in geoms if any(models.get(m) == want for m in parent[g]))
    g = geoms[geo]
    verts = child(g, "Vertices")[1][0]
    pvi = child(g, "PolygonVertexIndex")[1][0]
    uvl = child(g, "LayerElementUV")
    uv = child(uvl, "UV")[1][0]; uvidx = child(uvl, "UVIndex")[1][0]
    polys, cur = [], []
    for k, v in enumerate(pvi):
        cur.append(((v if v >= 0 else ~v), uvidx[k]))
        if v < 0: polys.append(cur); cur = []
    # lower face, front: below the eyes (y < 164), above the chin, facing forward
    cells = defaultdict(list)
    for p in polys:
        ys = [verts[3 * vi + 1] for vi, _ in p]; zs = [verts[3 * vi + 2] for vi, _ in p]
        cy, cz = sum(ys) / len(ys), sum(zs) / len(zs)
        if 152 <= cy <= 164 and cz >= 9:
            u = round(sum(uv[2 * t] for _, t in p) / len(p), 3); w = round(sum(uv[2 * t + 1] for _, t in p) / len(p), 3)
            cells[(u, w)].append(p)
    print("\n" + want)
    for (u, w), ps in sorted(cells.items(), key=lambda kv: len(kv[1])):
        pts = {vi for p in ps for vi, _ in p}
        ys = [verts[3 * vi + 1] for vi in pts]; xs = [verts[3 * vi] for vi in pts]; zs = [verts[3 * vi + 2] for vi in pts]
        # corners of these polygons also used, with a different UV, by any other polygon of the mesh
        others = set()
        for p in polys:
            if p in ps: continue
            for vi, t in p:
                if vi in pts: others.add(vi)
        print("  cell u%.3f v%.3f: %2d polys, %2d corners, x[%.1f,%.1f] y[%.1f,%.1f] z[%.1f,%.1f], corners shared with other polys: %d"
              % (u, w, len(ps), len(pts), min(xs), max(xs), min(ys), max(ys), min(zs), max(zs), len(others)))

path = sys.argv[1]
for name in sys.argv[2:]:
    run(path, name)
