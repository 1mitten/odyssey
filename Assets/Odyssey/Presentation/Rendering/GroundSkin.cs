#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The ground drawn as one continuous surface per chunk-layer instead of a box per cell and a
    /// wedge per bank (<c>docs/design/38-meadow-overhaul.md</c> §6, §20).
    ///
    /// <para><b>The simulation does not change.</b> Its cells are still whole 3 m layers and a
    /// colonist still hops one of them. The skin is a picture of that hop drawn across the cell it
    /// is taken from — the bank, generalised by a corner rule (<see cref="BankLayout.RampCorners"/>)
    /// so neighbouring ramps meet instead of stepping — and of the flat ground either side of it.
    /// It lives in no cell, no save and no hash, like everything else the mesher draws.</para>
    ///
    /// <para><b>Why a mesh and not more instances.</b> A surface chunk was 625 turf boxes whose
    /// matrices were uploaded every frame, most of them the same flat top at the same height. A
    /// baked mesh uploads once, when the chunk re-meshes, and is drawn with one call per material
    /// and tint rather than per bucket. Vertices share heights at shared corners — the relief is
    /// sampled at the corner, not taken from a cell's tangent plane — so there is no seam between
    /// two cells of skin to see through.</para>
    ///
    /// <para><b>What stays a box.</b> Anything with a visible side (a riser, a cutting, the edge of
    /// the world's ground), anything under a built floor or a building (whose drape it must match
    /// to the millimetre), anything a cave looks up at. Those keep the instanced blocks they had.</para>
    /// </summary>
    public static class GroundSkin
    {
        /// <summary>
        /// Draw the skin. Off gives the boxes and bank wedges exactly as before, which is what the
        /// measurement arm compares against.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// How far a ramp stands above the plane it rises from, in metres: enough that its low edge
        /// does not tie on depth with the flat ground it meets, far too little to see.
        /// </summary>
        public const float RampLift = 0.01f;
    }

    /// <summary>
    /// One chunk's skin: triangles grouped by (material, tint code), baked into a single mesh with a
    /// submesh per group. Owned by its <see cref="ChunkBatch"/>, rebuilt when the chunk re-meshes and
    /// destroyed with it.
    /// </summary>
    public sealed class GroundSkinMesh : System.IDisposable
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<List<int>> _triangles = new List<List<int>>();
        readonly List<Material> _materials = new List<Material>();
        readonly List<int> _tints = new List<int>();
        readonly List<bool> _fallback = new List<bool>();
        readonly Dictionary<(Material, int), int> _groupIndex = new Dictionary<(Material, int), int>();
        Mesh? _mesh;

        /// <summary>Groups built last time, each drawn as one submesh.</summary>
        public int GroupCount { get; private set; }

        /// <summary>Triangles in the built mesh, for the measurement arms.</summary>
        public int TriangleCount { get; private set; }

        public Mesh? Mesh => GroupCount > 0 ? _mesh : null;
        public Material GroupMaterial(int group) => _materials[group];
        public int GroupTint(int group) => _tints[group];
        public bool GroupFallback(int group) => _fallback[group];

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            for (int i = 0; i < _triangles.Count; i++) _triangles[i].Clear();
            _materials.Clear();
            _tints.Clear();
            _fallback.Clear();
            _groupIndex.Clear();
        }

        int Group(Material material, int tint, bool fallback)
        {
            if (_groupIndex.TryGetValue((material, tint), out int group)) return group;
            group = _materials.Count;
            _materials.Add(material);
            _tints.Add(tint);
            _fallback.Add(fallback);
            if (_triangles.Count <= group) _triangles.Add(new List<int>());
            _groupIndex.Add((material, tint), group);
            return group;
        }

        /// <summary>
        /// A triangle, turned so it faces <paramref name="facing"/> (its normal has a positive dot
        /// with it). Flat-shaded: its three vertices are its own, which is the low-poly look the
        /// pack's ground is authored in and keeps the ramp's crease sharp.
        /// </summary>
        public void Triangle(Material material, int tint, bool fallback, Vector3 a, Vector3 b, Vector3 c,
            Vector3 facing, bool vertical = false)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(normal, facing) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }
            normal.Normalize();

            List<int> triangles = _triangles[Group(material, tint, fallback)];
            int start = _vertices.Count;
            Add(a, normal, vertical);
            Add(b, normal, vertical);
            Add(c, normal, vertical);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        void Add(Vector3 at, Vector3 normal, bool vertical)
        {
            _vertices.Add(at);
            _normals.Add(normal);
            // Ground tiles once a cell, the same as the boxes did, for any material that samples a
            // texture by UV; the Meadow ground paints in world space and ignores it.
            _uvs.Add(vertical
                ? new Vector2((at.x + at.z) / CellMetrics.SizeXZ, at.y / CellMetrics.SizeXZ)
                : new Vector2(at.x / CellMetrics.SizeXZ, at.z / CellMetrics.SizeXZ));
        }

        /// <summary>Bake what was added since <see cref="Clear"/>. Allocates only when it grows.</summary>
        public void Build(Bounds bounds)
        {
            GroupCount = _materials.Count;
            TriangleCount = _vertices.Count / 3;
            if (GroupCount == 0)
            {
                if (_mesh != null) _mesh.Clear();
                return;
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "GroundSkin" };
                _mesh.MarkDynamic();
            }
            _mesh.Clear();
            _mesh.indexFormat = _vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            _mesh.subMeshCount = GroupCount;
            for (int g = 0; g < GroupCount; g++)
                _mesh.SetTriangles(_triangles[g], g, calculateBounds: false);
            _mesh.bounds = bounds;
        }

        public void Dispose()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Object.Destroy(_mesh);
            else Object.DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}
