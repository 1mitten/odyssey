"""Minimal binary FBX reader: which face nodes are bones, and what vertices they move."""
import struct, sys, zlib
from collections import defaultdict

def read_fbx(path):
    data = open(path, "rb").read()
    assert data[:20] == b"Kaydara FBX Binary  ", "not a binary FBX"
    version = struct.unpack_from("<I", data, 23)[0]
    wide = version >= 7500
    hdr = "<QQQ" if wide else "<III"
    hsz = 24 if wide else 12

    def prop(pos):
        t = chr(data[pos]); pos += 1
        if t == "Y": return struct.unpack_from("<h", data, pos)[0], pos + 2
        if t == "C": return data[pos] != 0, pos + 1
        if t == "I": return struct.unpack_from("<i", data, pos)[0], pos + 4
        if t == "F": return struct.unpack_from("<f", data, pos)[0], pos + 4
        if t == "D": return struct.unpack_from("<d", data, pos)[0], pos + 8
        if t == "L": return struct.unpack_from("<q", data, pos)[0], pos + 8
        if t in "fdlib":
            n, enc, clen = struct.unpack_from("<III", data, pos); pos += 12
            raw = data[pos:pos + clen]; pos += clen
            if enc == 1: raw = zlib.decompress(raw)
            fmt = {"f": "f", "d": "d", "l": "q", "i": "i", "b": "?"}[t]
            return list(struct.unpack("<%d%s" % (n, fmt), raw)), pos
        if t in "SR":
            n = struct.unpack_from("<I", data, pos)[0]; pos += 4
            return data[pos:pos + n], pos + n
        raise ValueError("prop type %r at %d" % (t, pos))

    def node(pos):
        end, nprops, _ = struct.unpack_from(hdr, data, pos)
        if end == 0: return None, pos + hsz + 1
        nlen = data[pos + hsz]
        name = data[pos + hsz + 1: pos + hsz + 1 + nlen].decode()
        p = pos + hsz + 1 + nlen
        props = []
        for _ in range(nprops):
            v, p = prop(p); props.append(v)
        kids = []
        while p < end:
            k, p = node(p)
            if k is None: break
            kids.append(k)
        return (name, props, kids), end

    top, pos = [], 27
    while pos < len(data) - hsz:
        n, pos = node(pos)
        if n is None: break
        top.append(n)
    return top

def child(n, name):
    for k in n[2]:
        if k[0] == name: return k
    return None

def objname(b):
    return b.split(b"\x00\x01")[0].decode(errors="replace")

def analyse(path):
    top = read_fbx(path)
    objects = next(n for n in top if n[0] == "Objects")
    conns = next(n for n in top if n[0] == "Connections")
    models, geoms, clusters = {}, {}, {}
    for o in objects[2]:
        oid = o[1][0]
        if o[0] == "Model":
            models[oid] = (objname(o[1][1]), o[1][2].decode())
        elif o[0] == "Geometry":
            v = child(o, "Vertices")
            geoms[oid] = v[1][0] if v else []
        elif o[0] == "Deformer" and o[1][2] == b"Cluster":
            idx = child(o, "Indexes"); w = child(o, "Weights"); tl = child(o, "TransformLink")
            clusters[oid] = (idx[1][0] if idx else [], w[1][0] if w else [], tl[1][0] if tl else None)
    parent = defaultdict(list)
    for c in conns[2]:
        if c[1][0] == b"OO":
            parent[c[1][1]].append(c[1][2])
    # bone -> clusters ; cluster -> skin -> geometry -> mesh model
    bone_clusters = defaultdict(list)
    for cid in clusters:
        pass
    for childid, pars in parent.items():
        if childid in models:
            for p in pars:
                if p in clusters: bone_clusters[childid].append(p)
    def geom_of_cluster(cid):
        for skin in parent.get(cid, []):
            for g in parent.get(skin, []):
                if g in geoms: return g
        return None
    def mesh_of_geom(g):
        for m in parent.get(g, []):
            if m in models: return models[m][0]
        return "?"
    def model_parent(mid):
        for p in parent.get(mid, []):
            if p in models: return models[p][0]
        return "(root)"

    print("=" * 80); print(path)
    face = [m for m, (nm, _) in models.items() if nm in ("Jaw", "Eyes", "Eyebrows", "Head", "Neck")]
    for mid in face:
        nm, typ = models[mid]
        print("\n%-9s type=%-9s parent=%s" % (nm, typ, model_parent(mid)))
        if nm in ("Head", "Neck"):
            # bind position from any cluster's TransformLink
            for cid in bone_clusters[mid][:1]:
                tl = clusters[cid][2]
                if tl: print("   bind position (m): %.3f %.3f %.3f" % (tl[12] / 100, tl[13] / 100, tl[14] / 100))
            continue
        cl = bone_clusters[mid]
        if not cl:
            print("   NO skin cluster -> moves no vertex"); continue
        for cid in cl:
            idx, w, tl = clusters[cid]
            g = geom_of_cluster(cid); verts = geoms.get(g, [])
            strong = [i for i, wt in zip(idx, w) if wt >= 0.5]
            live = [i for i, wt in zip(idx, w) if wt > 0.0]
            line = "   %-38s %4d verts weighted (>=0.5: %4d)" % (mesh_of_geom(g), len(live), len(strong))
            if live and verts:
                xs = [verts[3 * i] for i in live]; ys = [verts[3 * i + 1] for i in live]; zs = [verts[3 * i + 2] for i in live]
                line += "  box x[%.1f,%.1f] y[%.1f,%.1f] z[%.1f,%.1f]" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))
            if tl: line += "  bone@(%.1f,%.1f,%.1f)" % (tl[12], tl[13], tl[14])
            print(line)

if __name__ == "__main__":
    for p in sys.argv[1:]:
        analyse(p)
