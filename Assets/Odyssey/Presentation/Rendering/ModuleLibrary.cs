#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// One drawable piece of a module: a mesh, a submesh, the material the art shipped with, and
    /// where it sits relative to the module's placement point.
    ///
    /// A Synty prefab is flattened into these once, at load. Nothing walks a prefab hierarchy at
    /// draw time and no GameObject is ever instantiated for a cell.
    /// </summary>
    public sealed class ModulePart
    {
        public ModulePart(Mesh mesh, int submesh, Material material, Matrix4x4 local, bool fallback)
        {
            Mesh = mesh;
            Submesh = submesh;
            Material = material;
            Local = local;
            IsFallback = fallback;
        }

        public Mesh Mesh { get; }
        public int Submesh { get; }

        /// <summary>The art's own material. Never mutated; the tint cache clones it.</summary>
        public Material Material { get; }

        /// <summary>Placement point to part, already normalised onto the cell grid.</summary>
        public Matrix4x4 Local { get; }

        /// <summary>True when this part is a stand-in primitive rather than licensed art.</summary>
        public bool IsFallback { get; }
    }

    /// <summary>A module id resolved to drawable parts.</summary>
    public sealed class ResolvedModule
    {
        public ResolvedModule(string id, ModuleShape shape, ModulePart[] parts, bool usesArt)
        {
            Id = id;
            Shape = shape;
            Parts = parts;
            UsesArt = usesArt;
        }

        public string Id { get; }
        public ModuleShape Shape { get; }
        public ModulePart[] Parts { get; }

        /// <summary>False when the module fell back to a primitive because no art was found.</summary>
        public bool UsesArt { get; }

        public bool IsEmpty => Parts.Length == 0;
    }

    /// <summary>
    /// Module ids resolved to geometry, once, against a <see cref="ModuleCatalogue"/>.
    ///
    /// Resolution is by integer index from then on: the mirror stores module indices per cell and
    /// the mesher never sees a string. Index 0 is always the empty module, so "nothing here" and
    /// "index zero" are the same thing and no caller needs a sentinel.
    ///
    /// **Degrading without the licensed packs is a first-class path, not an error path.** A null
    /// prefab reference, a catalogue with no rows, or no catalogue at all all resolve to the
    /// primitive stand-in for the module's shape. The world draws, the camera works and the only
    /// consequence is that it is made of boxes.
    /// </summary>
    public sealed class ModuleLibrary
    {
        readonly ModuleCatalogue? _catalogue;
        readonly List<ResolvedModule> _modules = new List<ResolvedModule>();
        readonly Dictionary<string, int> _byId = new Dictionary<string, int>();
        readonly List<string> _missing = new List<string>();
        Material? _fallbackMaterial;

        public ModuleLibrary(ModuleCatalogue? catalogue)
        {
            _catalogue = catalogue;
            _modules.Add(new ResolvedModule(string.Empty, ModuleShape.None, new ModulePart[0], usesArt: false));
            _byId[string.Empty] = 0;
        }

        public int Count => _modules.Count;

        public ResolvedModule this[int index] => _modules[index];

        /// <summary>Module ids that had no art and were drawn as primitives. For the log, once.</summary>
        public IReadOnlyList<string> MissingArt => _missing;

        public int ArtBackedCount()
        {
            int n = 0;
            for (int i = 1; i < _modules.Count; i++) if (_modules[i].UsesArt) n++;
            return n;
        }

        /// <summary>
        /// The index for an id, resolving it on first use. <paramref name="defaultShape"/> is used
        /// when the catalogue has no row for the id at all, which is what makes a template that
        /// invents a module name render as a box rather than as a hole.
        /// </summary>
        public int Resolve(string? moduleId, ModuleShape defaultShape)
        {
            if (string.IsNullOrEmpty(moduleId)) return 0;
            if (_byId.TryGetValue(moduleId!, out int existing)) return existing;

            ModuleEntry? entry = _catalogue != null ? _catalogue.Find(moduleId!) : null;
            ModuleShape shape = entry != null ? entry.shape : defaultShape;
            if (shape == ModuleShape.None)
            {
                _byId[moduleId!] = 0;
                return 0;
            }

            ModulePart[] parts;
            bool usesArt = false;
            if (entry != null && entry.prefab != null && entry.materialOnly)
            {
                // The box stays cell-shaped; only the look comes from the pack. See
                // ModuleEntry.materialOnly for why solid ground must work this way.
                Material? art = MaterialOf(entry.prefab!);
                if (art != null)
                {
                    parts = new[] { FallbackPart(shape, entry, art) };
                    usesArt = true;
                }
                else
                {
                    parts = new ModulePart[0];
                }
            }
            else if (entry != null && entry.prefab != null)
            {
                parts = FlattenPrefab(entry.prefab!, entry, shape);
                usesArt = parts.Length > 0;
            }
            else
            {
                parts = new ModulePart[0];
            }

            if (parts.Length == 0)
            {
                parts = new[] { FallbackPart(shape, entry) };
                _missing.Add(moduleId!);
            }

            var module = new ResolvedModule(moduleId!, shape, parts, usesArt);
            _modules.Add(module);
            int index = _modules.Count - 1;
            _byId[moduleId!] = index;
            return index;
        }

        // ---------------------------------------------------------------- art

        ModulePart[] FlattenPrefab(GameObject prefab, ModuleEntry entry, ModuleShape shape)
        {
            var filters = prefab.GetComponentsInChildren<MeshFilter>(includeInactive: false);
            var raw = new List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)>();
            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;

            var bounds = new Bounds();
            bool hasBounds = false;

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh? mesh = filters[i].sharedMesh;
                var renderer = filters[i].GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled) continue;

                Matrix4x4 local = rootInverse * filters[i].transform.localToWorldMatrix;
                Material[] materials = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material? material = materials.Length == 0
                        ? null
                        : materials[Mathf.Min(sub, materials.Length - 1)];
                    if (material == null) material = FallbackMaterial;
                    raw.Add((mesh!, sub, material!, local));
                }

                Bounds local_b = TransformBounds(mesh.bounds, local);
                if (!hasBounds) { bounds = local_b; hasBounds = true; }
                else bounds.Encapsulate(local_b);
            }

            if (raw.Count == 0) return new ModulePart[0];

            // Neutralise whichever pivot convention the piece uses, once per module rather than
            // once per instance: the same trick the look-check scene plays, moved off the hot path.
            var normalise = Vector3.zero;
            if (entry.centreXZ) { normalise.x = bounds.center.x; normalise.z = bounds.center.z; }
            if (entry.baseAtY) normalise.y = bounds.min.y;

            Matrix4x4 place = Matrix4x4.TRS(entry.offset, Quaternion.Euler(0f, entry.yaw, 0f), SafeScale(entry))
                              * Matrix4x4.Translate(-normalise);

            var parts = new ModulePart[raw.Count];
            for (int i = 0; i < raw.Count; i++)
                parts[i] = new ModulePart(raw[i].mesh, raw[i].submesh, raw[i].material,
                    place * raw[i].local, fallback: false);
            return parts;
        }

        static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            Vector3 centre = m.MultiplyPoint3x4(b.center);
            Vector3 e = b.extents;
            Vector3 extents = new Vector3(
                Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);
            return new Bounds(centre, extents * 2f);
        }

        // ---------------------------------------------------------- fallbacks

        /// <summary>The stand-in box for a shape, sized to the cell. One mesh, many matrices.</summary>
        ModulePart FallbackPart(ModuleShape shape, ModuleEntry? entry, Material? material = null)
        {
            GetFallbackBox(shape, out Vector3 size, out Vector3 centre);
            Matrix4x4 local = Matrix4x4.TRS(centre, Quaternion.identity, size);
            if (entry != null)
                local = Matrix4x4.TRS(entry.offset, Quaternion.Euler(0f, entry.yaw, 0f), SafeScale(entry)) * local;
            return new ModulePart(
                PrimitiveMeshes.UnitCube, 0, material ?? FallbackMaterial, local,
                fallback: material == null);
        }

        /// <summary>The first shared material on a prefab, used when only its look is wanted.</summary>
        static Material? MaterialOf(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(includeInactive: false);
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].sharedMaterial;
                if (material != null) return material;
            }
            return null;
        }

        /// <summary>A zero scale in a deserialised row would silently delete the module.</summary>
        static Vector3 SafeScale(ModuleEntry entry) =>
            entry.scale.sqrMagnitude < 1e-6f ? Vector3.one : entry.scale;

        public static void GetFallbackBox(ModuleShape shape, out Vector3 size, out Vector3 centre)
        {
            switch (shape)
            {
                case ModuleShape.WallPanel:
                    size = new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, 0.25f);
                    centre = new Vector3(0f, CellMetrics.SizeY * 0.5f, 0f);
                    return;
                case ModuleShape.FloorSlab:
                    size = new Vector3(CellMetrics.SizeXZ, 0.15f, CellMetrics.SizeXZ);
                    centre = new Vector3(0f, 0.075f, 0f);
                    return;
                case ModuleShape.Pillar:
                    size = new Vector3(0.5f, CellMetrics.SizeY, 0.5f);
                    centre = new Vector3(0f, CellMetrics.SizeY * 0.5f, 0f);
                    return;
                case ModuleShape.StairFlight:
                    size = new Vector3(2.2f, 1.5f, 2.2f);
                    centre = new Vector3(0f, 0.75f, 0f);
                    return;
                case ModuleShape.Ladder:
                    size = new Vector3(0.6f, CellMetrics.SizeY, 0.14f);
                    centre = new Vector3(0f, CellMetrics.SizeY * 0.5f, 0f);
                    return;
                default:
                    size = new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ);
                    centre = new Vector3(0f, CellMetrics.SizeY * 0.5f, 0f);
                    return;
            }
        }

        /// <summary>
        /// The one material every primitive stand-in shares. URP Lit, white, instancing on; the
        /// tint cache derives a coloured clone per stuff and per depth shade from it.
        /// </summary>
        public Material FallbackMaterial
        {
            get
            {
                if (_fallbackMaterial != null) return _fallbackMaterial;
                Shader? shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _fallbackMaterial = new Material(shader) { name = "Odyssey/Fallback", enableInstancing = true };
                _fallbackMaterial.SetFloat("_Smoothness", 0.12f);
                _fallbackMaterial.SetFloat("_Metallic", 0f);
                return _fallbackMaterial;
            }
        }
    }
}
