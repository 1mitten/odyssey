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
        readonly List<Mesh> _baked = new List<Mesh>();
        Material? _fallbackMaterial;

        /// <summary>
        /// Meshes baked from rigged art, which the library created and therefore owns. Unity will
        /// not collect them, so a caller that tears the library down destroys these.
        /// </summary>
        public IReadOnlyList<Mesh> BakedMeshes => _baked;

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
            if (entry != null && entry.material != null)
            {
                // A material straight onto the cell-shaped box: how textured ground is drawn.
                // Checked before the prefab paths so a row can carry both and prefer the texture.
                parts = new[] { FallbackPart(shape, entry, DressGround(entry)) };
                usesArt = true;
            }
            else if (entry != null && entry.prefab != null && entry.materialOnly)
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

            CollectSkinned(prefab, entry, raw, ref bounds, ref hasBounds);

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

        /// <summary>
        /// Turn a rigged character into plain meshes this renderer can actually draw.
        ///
        /// **Why this exists.** A Synty character carries no <see cref="MeshFilter"/> at all: its
        /// geometry hangs off <see cref="SkinnedMeshRenderer"/>. The static path above therefore
        /// found nothing, fell through to the primitive box and logged the id as missing art — a
        /// grey cube where a person should be, with no error to explain it.
        ///
        /// **Why baking rather than instancing the rig.** <c>RenderMeshInstanced</c> takes one mesh
        /// and many matrices; there is no per-instance bone palette, so a skinned mesh cannot go
        /// through it. Handing over <c>sharedMesh</c> would not fail loudly either — that mesh is
        /// the bind pose in bone space, so it would draw a splayed, unskinned figure. Baking once
        /// at load collapses the rig into an ordinary mesh, after which a colonist costs exactly
        /// what a wall costs and travels the same path as everything else in the renderer.
        ///
        /// The cost is that a baked figure does not animate; it glides. At the distance this game
        /// is played from that is a far smaller deficit than a grey box, and it is a stepping
        /// stone rather than a dead end: a pooled <c>GameObject</c> with an Animator can take over
        /// for the handful of pawns actually on screen, keeping this as the cheap far-distance
        /// form.
        ///
        /// The instance is temporary and is destroyed before this returns. The baked meshes are
        /// ours and are owned by the library from here on.
        /// </summary>
        void CollectSkinned(GameObject prefab, ModuleEntry entry,
            List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)> raw,
            ref Bounds bounds, ref bool hasBounds)
        {
            if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true).Length == 0)
                return;

            GameObject instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                // Pose it before the snapshot is taken, or the bind pose is what gets captured.
                if (entry.poseClip != null)
                    entry.poseClip!.SampleAnimation(instance, entry.poseClipTime);

                Matrix4x4 rootInverse = instance.transform.worldToLocalMatrix;
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: false);

                for (int i = 0; i < skins.Length; i++)
                {
                    SkinnedMeshRenderer skin = skins[i];
                    if (skin.sharedMesh == null || !skin.enabled) continue;

                    var baked = new Mesh { name = skin.sharedMesh.name + "/baked" };
                    skin.BakeMesh(baked, useScale: true);
                    _baked.Add(baked);

                    // BakeMesh reports in the renderer's own space, so the renderer's place in the
                    // hierarchy still has to be accounted for before anything is comparable.
                    Matrix4x4 local = rootInverse * skin.transform.localToWorldMatrix;
                    Material[] materials = skin.sharedMaterials;

                    for (int sub = 0; sub < baked.subMeshCount; sub++)
                    {
                        Material? material = materials.Length == 0
                            ? null
                            : materials[Mathf.Min(sub, materials.Length - 1)];
                        raw.Add((baked, sub, material ?? FallbackMaterial, local));
                    }

                    Bounds b = TransformBounds(baked.bounds, local);
                    if (!hasBounds) { bounds = b; hasBounds = true; }
                    else bounds.Encapsulate(b);
                }
            }
            finally
            {
                if (Application.isPlaying) Object.Destroy(instance);
                else Object.DestroyImmediate(instance);
            }
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

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");

        readonly Dictionary<(Material, float, bool), Material> _dressed =
            new Dictionary<(Material, float, bool), Material>();

        /// <summary>
        /// A pack terrain material adjusted to this game's grid and lighting.
        ///
        /// Always a clone: the source lives under <c>Assets/Synty</c> and is licensed content that
        /// must never be written to. The clone is cached per source and settings, so a terrain
        /// type costs one material no matter how many cells wear it.
        ///
        /// See <see cref="ModuleEntry.materialTilesPerCell"/> and
        /// <see cref="ModuleEntry.flattenNormalMap"/> for why each adjustment is made.
        /// </summary>
        Material DressGround(ModuleEntry entry)
        {
            Material source = entry.material!;
            if (entry.materialTilesPerCell <= 0f && !entry.flattenNormalMap) return source;

            var key = (source, entry.materialTilesPerCell, entry.flattenNormalMap);
            if (_dressed.TryGetValue(key, out Material? ready)) return ready;

            var dressed = new Material(source)
            {
                name = source.name + "/ground",
                enableInstancing = true,
            };

            if (entry.materialTilesPerCell > 0f)
            {
                var repeats = new Vector2(entry.materialTilesPerCell, entry.materialTilesPerCell);
                if (dressed.HasProperty(BaseMapId)) dressed.SetTextureScale(BaseMapId, repeats);
                if (dressed.HasProperty(MainTexId)) dressed.SetTextureScale(MainTexId, repeats);
            }

            if (entry.flattenNormalMap)
            {
                if (dressed.HasProperty(BumpScaleId)) dressed.SetFloat(BumpScaleId, 0f);
                if (dressed.HasProperty(BumpMapId)) dressed.SetTexture(BumpMapId, null);
                dressed.DisableKeyword("_NORMALMAP");
            }

            _dressed[key] = dressed;
            return dressed;
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
