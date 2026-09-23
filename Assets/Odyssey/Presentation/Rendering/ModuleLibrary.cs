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
            Bounds = BoundsOf(parts);
        }

        public string Id { get; }
        public ModuleShape Shape { get; }
        public ModulePart[] Parts { get; }

        /// <summary>
        /// The box the module occupies once placed, relative to its placement point — for a
        /// floor-standing piece, the cell's floor centre. Empty for a module with no parts.
        ///
        /// This is what lets a cursor fit the thing selected rather than the cell it sits in: a
        /// half-height crate gets a half-height bracket because the art says so, not because
        /// anyone typed a number for crates.
        /// </summary>
        public Bounds Bounds { get; }

        static Bounds BoundsOf(ModulePart[] parts)
        {
            var bounds = new Bounds();
            bool any = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Mesh == null) continue;
                Bounds b = ModuleLibrary.TransformBounds(parts[i].Mesh.bounds, parts[i].Local);
                if (!any) { bounds = b; any = true; }
                else bounds.Encapsulate(b);
            }
            return bounds;
        }

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
    public sealed class ModuleLibrary : System.IDisposable
    {
        readonly ModuleCatalogue? _catalogue;
        readonly List<ResolvedModule> _modules = new List<ResolvedModule>();
        readonly Dictionary<string, int> _byId = new Dictionary<string, int>();
        readonly List<string> _missing = new List<string>();
        readonly List<Mesh> _baked = new List<Mesh>();
        Material? _fallbackMaterial;

        /// <summary>
        /// Meshes the library created and therefore owns: baked from rigged art, or merged from a
        /// prefab's parts. Unity does not garbage-collect either, so <see cref="Dispose"/> is what
        /// frees them.
        /// </summary>
        public IReadOnlyList<Mesh> BakedMeshes => _baked;

        /// <summary>
        /// Free the meshes and the material this library made.
        ///
        /// **This is a leak fix and the leak was a crash.** A `Mesh` created in code is a GPU
        /// allocation that Unity never collects — the comment above said so and nothing acted on
        /// it. Every Play session builds a fresh library and bakes a mesh for every character it
        /// resolves, so entering and leaving Play mode in the editor accumulated them until the
        /// graphics device gave out: Windows detects the GPU has stopped responding, resets it,
        /// and Unity reports `DXGI_ERROR_DEVICE_REMOVED` (0x887A0005) and shuts down. It gets
        /// worse the more characters are in the cast, which is why it only started biting once
        /// colonists could be any of sixty-one people.
        ///
        /// Safe to call twice, and safe on a library that never resolved anything.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _baked.Count; i++)
            {
                Mesh mesh = _baked[i];
                if (mesh == null) continue;
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
            }
            _baked.Clear();

            if (_fallbackMaterial != null)
            {
                if (Application.isPlaying) Object.Destroy(_fallbackMaterial);
                else Object.DestroyImmediate(_fallbackMaterial);
                _fallbackMaterial = null;
            }
        }

        public ModuleLibrary(ModuleCatalogue? catalogue)
        {
            _catalogue = catalogue;
            _modules.Add(new ResolvedModule(string.Empty, ModuleShape.None, new ModulePart[0], usesArt: false));
            _byId[string.Empty] = 0;
        }

        /// <summary>
        /// The catalogue this library resolves against, or null when there is none.
        ///
        /// Exposed so a caller can ask a question the library cannot answer for it: how many
        /// variants a *family* of rows has. Resolution is by id, and there is no way to discover
        /// the ids of a family by resolving them one at a time — an id nobody authored resolves to
        /// a primitive and gets logged as missing art, so probing for the end of a family would
        /// manufacture exactly the warning it was trying to avoid.
        /// </summary>
        public ModuleCatalogue? Catalogue => _catalogue;

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
        public int Resolve(string? moduleId, ModuleShape defaultShape) =>
            Resolve(moduleId, defaultShape, meshVariant: 0);

        /// <summary>
        /// The same, for a shape that has several meshes to choose between.
        ///
        /// <paramref name="meshVariant"/> is a resolution-time parameter and not a catalogue
        /// field on purpose: a clone with no catalogue at all must still get the varied stone,
        /// because the lumps are ours and owe nothing to the licensed packs. The id already
        /// carries the number, so a row *may* exist per variant to give each its own material —
        /// but if none does, the fallback still picks the right mesh.
        /// </summary>
        public int Resolve(string? moduleId, ModuleShape defaultShape, int meshVariant) =>
            Resolve(moduleId, null, defaultShape, meshVariant);

        /// <summary>
        /// The same, for a variant that may have no row of its own and should borrow one.
        ///
        /// <para><b>What this is for.</b> A family of meshes — the six stone lumps, the earth tops
        /// and their coursed faces — is the same *material* cut several ways. The geometry is ours
        /// and owes nothing to the licensed packs, but the material is a pack terrain texture that
        /// lives on exactly one catalogue row. Without this, every variant past the one with a row
        /// resolved to no entry at all, fell through to the untextured stand-in, and logged itself
        /// as missing art: a meadow where one cell in two wore grass and the rest wore flat white,
        /// which renders perfectly and gets blamed on the art.</para>
        ///
        /// <para>The alternative was a catalogue row per variant, and it is worse. It puts the
        /// number of meshes into a generated asset, so changing <see cref="GroundMesh.Variants"/>
        /// means regenerating the catalogue — which can only be done on a machine that has the
        /// packs, because the asset holds direct references into <c>Assets/Synty</c> and a rebuild
        /// without them writes nulls over every one. Borrowing needs no asset change at all.</para>
        ///
        /// <para><paramref name="baseId"/> is consulted only for its material, never for its
        /// shape: the row being borrowed describes a plain block, and the whole point of asking is
        /// that this variant is not one. So a borrowed row takes <paramref name="defaultShape"/>.</para>
        /// </summary>
        public int Resolve(string? moduleId, string? baseId, ModuleShape defaultShape, int meshVariant)
        {
            if (string.IsNullOrEmpty(moduleId)) return 0;
            if (_byId.TryGetValue(moduleId!, out int existing)) return existing;

            ModuleEntry? entry = _catalogue != null ? _catalogue.Find(moduleId!) : null;
            ModuleShape shape = entry != null ? entry.shape : defaultShape;

            if (entry == null && !string.IsNullOrEmpty(baseId) && _catalogue != null)
            {
                entry = _catalogue.Find(baseId!);
                shape = defaultShape;
            }
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
                parts = new[] { FallbackPart(shape, entry, DressGround(entry), meshVariant) };
                usesArt = true;
            }
            else if (entry != null && entry.prefab != null && entry.materialOnly)
            {
                // The box stays cell-shaped; only the look comes from the pack. See
                // ModuleEntry.materialOnly for why solid ground must work this way.
                Material? art = MaterialOf(entry.prefab!);
                if (art != null)
                {
                    parts = new[] { FallbackPart(shape, entry, art, meshVariant) };
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
                parts = new[] { FallbackPart(shape, entry, null, meshVariant) };
                _missing.Add(moduleId!);
            }

            var module = new ResolvedModule(moduleId!, shape, parts, usesArt);
            _modules.Add(module);
            int index = _modules.Count - 1;
            _byId[moduleId!] = index;
            return index;
        }

        // ---------------------------------------------------------------- art

        /// <summary>
        /// The renderers to take from a prefab that ships level-of-detail meshes: LOD0, plus
        /// anything the LOD group does not mention at all. Null when there is no LOD group.
        ///
        /// **This is a bug fix, and the bug was invisible.** A pack prefab with an
        /// <see cref="LODGroup"/> carries the same object three times over — LOD0, LOD1, LOD2 —
        /// and a Unity scene shows one of them because the group switches between them by screen
        /// size. Nothing here is a scene: the flattener walked every <c>MeshFilter</c> under the
        /// prefab and took them all, so every tuft of grass was drawn three times, as three
        /// slightly different meshes occupying the same space. It cost triple the geometry and
        /// triple the instances, and it looked *almost* right, which is why it survived a
        /// measurement pass: the extra copies are the same shape, just coarser.
        ///
        /// Taking LOD0 always is not the same as supporting LODs — a distant tuft still draws its
        /// finest mesh — but drawing one mesh is correct where drawing three was not, and picking
        /// a level per chunk distance is a separate piece of work with its own measurement.
        /// </summary>
        static HashSet<Renderer>? HighestDetail(GameObject prefab)
        {
            var groups = prefab.GetComponentsInChildren<LODGroup>(includeInactive: true);
            if (groups.Length == 0) return null;

            var keep = new HashSet<Renderer>();
            var mentioned = new HashSet<Renderer>();

            foreach (LODGroup group in groups)
            {
                LOD[] levels = group.GetLODs();
                for (int level = 0; level < levels.Length; level++)
                foreach (Renderer renderer in levels[level].renderers)
                {
                    if (renderer == null) continue;
                    mentioned.Add(renderer);
                    if (level == 0) keep.Add(renderer);
                }
            }

            // A renderer no LOD level claims is not a level of detail, it is just part of the
            // prefab, and dropping it would quietly delete geometry.
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(includeInactive: false))
                if (!mentioned.Contains(renderer)) keep.Add(renderer);

            return keep;
        }

        ModulePart[] FlattenPrefab(GameObject prefab, ModuleEntry entry, ModuleShape shape)
        {
            var filters = prefab.GetComponentsInChildren<MeshFilter>(includeInactive: false);
            var raw = new List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)>();
            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
            HashSet<Renderer>? detail = HighestDetail(prefab);

            var bounds = new Bounds();
            bool hasBounds = false;

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh? mesh = filters[i].sharedMesh;
                var renderer = filters[i].GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled) continue;
                if (detail != null && !detail.Contains(renderer)) continue;

                Matrix4x4 local = rootInverse * filters[i].transform.localToWorldMatrix;
                Material[] materials = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material? material = materials.Length == 0
                        ? null
                        : materials[Mathf.Min(sub, materials.Length - 1)];
                    if (material == null) material = FallbackMaterial;

                    // Doors in the Synty packs assign a contrasting brick material to the doorway surround.
                    // To match the wall texture and avoid an out-of-place red brick arch, override any brick
                    // submesh on door modules with the matching plaster material present on the same renderer.
                    if (entry.shape == ModuleShape.WallPanel && entry.moduleId != null &&
                        entry.moduleId.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                        material != null && material.name.IndexOf("Brick", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        for (int m = 0; m < materials.Length; m++)
                        {
                            if (materials[m] != null && materials[m].name.IndexOf("Plaster", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                material = materials[m];
                                break;
                            }
                        }
                    }

                    raw.Add((mesh!, sub, material!, local));
                }

                Bounds local_b = TransformBounds(mesh.bounds, local);
                if (!hasBounds) { bounds = local_b; hasBounds = true; }
                else bounds.Encapsulate(local_b);
            }

            CollectSkinned(prefab, entry, raw, ref bounds, ref hasBounds);

            if (raw.Count == 0) return new ModulePart[0];

            // Merge before measuring, because merging is what makes the measurement honest.
            //
            // A piece's bounds were previously its mesh's axis-aligned box pushed through its
            // local transform, and the box that comes out of that is a box around a rotated box —
            // always at least as large as the geometry and, for anything turned at an angle,
            // noticeably larger. A merged group has its locals baked into its vertices, so its
            // bounds are simply its bounds. The colonist is the case that shows the difference:
            // measured loosely, it stood on a floor 0.13 m below its own feet.
            List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)> merged = Merge(raw);

            var exact = new Bounds();
            bool hasExact = false;
            for (int i = 0; i < merged.Count; i++)
            {
                Bounds b = TransformBounds(merged[i].mesh.bounds, merged[i].local);
                if (!hasExact) { exact = b; hasExact = true; }
                else exact.Encapsulate(b);
            }
            if (hasExact) bounds = exact;

            // Neutralise whichever pivot convention the piece uses, once per module rather than
            // once per instance: the same trick the look-check scene plays, moved off the hot path.
            var normalise = Vector3.zero;
            if (entry.centreXZ) { normalise.x = bounds.center.x; normalise.z = bounds.center.z; }
            // baseAtY first: the two ask opposite questions, and every piece already using the
            // older one is placed correctly, so a row that somehow sets both keeps what it had.
            if (entry.baseAtY) normalise.y = bounds.min.y;
            // The top face lands CellMetrics.SlabLift *above* the placement height, not on it: the
            // cell's floor plane is also the top face of the block below, so a surface levelled
            // exactly on to it z-fights with the ground it is laid on. The clearance belongs to the
            // rule rather than to each row, or every future walked-on piece has to remember it.
            else if (entry.topAtY) normalise.y = bounds.max.y - CellMetrics.SlabLift;

            // A prop fitted to a footprint (design 32 §14): scaled uniformly into the rectangle and
            // turned a quarter when its own long side is its X, so the long side runs along the
            // facing — which is +Z, the way the second cell of a two-cell record lies.
            float yaw = entry.yaw;
            Vector3 scale = SafeScale(entry);
            if (entry.fitFootprint.x > 0f && entry.fitFootprint.y > 0f && bounds.size.x > 0f && bounds.size.z > 0f)
            {
                bool turn = bounds.size.x > bounds.size.z;
                float along = turn ? bounds.size.x : bounds.size.z;
                float across = turn ? bounds.size.z : bounds.size.x;
                float fit = Mathf.Min(entry.fitFootprint.y / along, entry.fitFootprint.x / across);
                if (entry.fitHeight > 0f && bounds.size.y > 0f) fit = Mathf.Min(fit, entry.fitHeight / bounds.size.y);
                scale = new Vector3(fit, fit, fit);
                if (turn) yaw += 90f;
            }

            Matrix4x4 place = Matrix4x4.TRS(entry.offset, Quaternion.Euler(0f, yaw, 0f), scale)
                              * Matrix4x4.Translate(-normalise);

            var parts = new ModulePart[merged.Count];
            for (int i = 0; i < merged.Count; i++)
                parts[i] = new ModulePart(merged[i].mesh, merged[i].submesh, merged[i].material,
                    place * merged[i].local, fallback: false);
            return parts;
        }

        /// <summary>
        /// Collapse the pieces of a prefab that share a material into one mesh apiece.
        ///
        /// **Why this is worth doing at all.** A part is not a triangle count, it is a *draw*: the
        /// renderer submits one instanced call per part per bucket, so a prefab modelled as three
        /// separate renderers costs three times the calls and three times the matrices of the same
        /// geometry in one mesh. It goes unnoticed on a wall, which there are hundreds of. Grass
        /// found it, because there are tens of thousands: the meadow clumps are three cards each,
        /// and scattering them took the slice from 41 draw calls to 266 and from 14,400 instances
        /// to 66,441 — very nearly all of it the same fifty triangles being asked for three times.
        ///
        /// Merging is safe precisely because it happens here. The pieces of a module never move
        /// relative to one another — that is what makes them one module — so their local transforms
        /// can be baked into vertices once, at load, and every instance thereafter is one matrix.
        /// Anything genuinely articulated would not be a module in the first place.
        ///
        /// Grouping is by material and by nothing else, since a material is what forces a separate
        /// draw. Single-part modules, which is most of them, take the cheap path and are untouched.
        /// </summary>
        /// <summary>
        /// Whether every mesh in a group can legally be combined.
        ///
        /// <see cref="Mesh.CombineMeshes"/> needs CPU-side vertex data, and an imported mesh only
        /// has it when the importer's Read/Write flag is on — which it is not, for the licensed
        /// packs. Calling it anyway does not throw: it logs a stack trace and returns an **empty**
        /// mesh, so the module silently draws nothing while the console fills up once per module
        /// per library. Asking first costs a field read.
        /// </summary>
        static bool Mergeable(
            List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)> raw, List<int> group)
        {
            for (int i = 0; i < group.Count; i++)
                if (!raw[group[i]].mesh.isReadable) return false;
            return true;
        }

        List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)> Merge(
            List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)> raw)
        {
            if (raw.Count == 1) return raw;

            var byMaterial = new Dictionary<Material, List<int>>();
            var order = new List<Material>();
            for (int i = 0; i < raw.Count; i++)
            {
                if (!byMaterial.TryGetValue(raw[i].material, out List<int>? group))
                {
                    group = new List<int>();
                    byMaterial.Add(raw[i].material, group);
                    order.Add(raw[i].material);
                }
                group.Add(i);
            }

            var parts = new List<(Mesh, int, Material, Matrix4x4)>(order.Count);
            foreach (Material material in order)
            {
                List<int> group = byMaterial[material];
                if (group.Count == 1 || !Mergeable(raw, group))
                {
                    // Kept separate. One draw each is the cost of not merging; the alternative is
                    // CombineMeshes refusing and handing back an empty mesh, which draws nothing
                    // at all and does it silently apart from a stack trace per module.
                    for (int i = 0; i < group.Count; i++) parts.Add(raw[group[i]]);
                    continue;
                }

                var combine = new CombineInstance[group.Count];
                for (int i = 0; i < group.Count; i++)
                    combine[i] = new CombineInstance
                    {
                        mesh = raw[group[i]].mesh,
                        subMeshIndex = raw[group[i]].submesh,
                        transform = raw[group[i]].local,
                    };

                var merged = new Mesh { name = raw[group[0]].mesh.name + "/merged" };
                // 32-bit indices: a merge is bounded by the prefab, not by the world, so this
                // will not overflow in practice — but a silently truncated mesh would be a
                // fortnight of confusion, and the format costs nothing at these sizes.
                merged.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                merged.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
                merged.RecalculateBounds();
                _baked.Add(merged);

                // The locals are already inside the merged vertices, so the piece needs no local
                // of its own and its bounds are now exactly its geometry.
                parts.Add((merged, 0, material, Matrix4x4.identity));
            }

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

        internal static Bounds TransformBounds(Bounds b, Matrix4x4 m)
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
        ModulePart FallbackPart(ModuleShape shape, ModuleEntry? entry, Material? material = null,
            int meshVariant = 0)
        {
            GetFallbackBox(shape, out Vector3 size, out Vector3 centre);
            Matrix4x4 local = Matrix4x4.TRS(centre, Quaternion.identity, size);
            if (entry != null)
                local = Matrix4x4.TRS(entry.offset, Quaternion.Euler(0f, entry.yaw, 0f), SafeScale(entry)) * local;

            // Stone gets a chipped lump, earth gets a rippled top and — where a side of it shows —
            // coursed walls, and everything else gets the one shared cube. All four span the same
            // -0.5..0.5 unit box, so the placement maths above is identical for any of them.
            Mesh mesh;
            switch (shape)
            {
                case ModuleShape.RockBlock: mesh = RockMesh.For(meshVariant); break;
                case ModuleShape.GroundBlock: mesh = GroundMesh.Turf(meshVariant); break;
                case ModuleShape.GroundFace: mesh = GroundMesh.FaceBySlot(meshVariant); break;
                case ModuleShape.Bank: mesh = BankMesh.For(meshVariant); break;
                case ModuleShape.WaterSurface: mesh = WaterMesh.Surface; break;
                case ModuleShape.WaterFall: mesh = WaterMesh.Fall; break;
                case ModuleShape.Pillow: mesh = PillowMesh.Mesh; break;
                default: mesh = PrimitiveMeshes.UnitCube; break;
            }

            return new ModulePart(
                mesh, 0, material ?? FallbackMaterial, local,
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
                case ModuleShape.WaterSurface:
                    // A cell across in plan and flat: the mesh sits at local y = 0, so the height
                    // here scales nothing. One, rather than zero, so the box is never degenerate.
                    size = new Vector3(CellMetrics.SizeXZ, 1f, CellMetrics.SizeXZ);
                    centre = Vector3.zero;
                    return;
                case ModuleShape.WaterFall:
                    // A cell across and **one metre** tall, so that the instance matrix's Y scale
                    // reads directly as the drop in metres. Anything else here would make the
                    // caller multiply by a constant it had to go and look up.
                    size = new Vector3(CellMetrics.SizeXZ, 1f, 1f);
                    centre = Vector3.zero;
                    return;
                case ModuleShape.Pillow:
                    // The unit box, so the caller's scale reads directly as the pillow's size in
                    // metres divided by a cell. Centred on its own middle rather than standing on
                    // a floor, because a pillow is placed by where it lies on a mattress.
                    size = Vector3.one;
                    centre = Vector3.zero;
                    return;
                case ModuleShape.RockBlock:
                case ModuleShape.GroundBlock:
                case ModuleShape.GroundFace:
                case ModuleShape.Bank:
                    // Exactly a cell, like SolidBlock. The lump varies inside that box and never
                    // outside it in a direction that could open a seam.
                    size = new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ);
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
