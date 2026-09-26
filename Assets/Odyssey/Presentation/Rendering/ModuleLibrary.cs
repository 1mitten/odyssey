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

    /// <summary>
    /// One level of detail of a module: the parts drawn at that level, and the screen height (as a
    /// fraction of the screen, the pack's own <c>LODGroup</c> number) above which it is used.
    /// </summary>
    public sealed class ModuleLod
    {
        public ModuleLod(ModulePart[] parts, float screenHeight)
        {
            Parts = parts;
            ScreenHeight = screenHeight;
        }

        public ModulePart[] Parts { get; }

        /// <summary>The level is drawn while the module fills at least this much of the screen's
        /// height. The last level is drawn below its own number too: nothing here culls.</summary>
        public float ScreenHeight { get; }
    }

    /// <summary>A module id resolved to drawable parts.</summary>
    public sealed class ResolvedModule
    {
        public ResolvedModule(string id, ModuleShape shape, ModulePart[] parts, bool usesArt,
            Matrix4x4 head = default, bool hasHead = false, ModuleLod[]? lods = null, float lodSize = 0f)
        {
            Id = id;
            Shape = shape;
            Parts = parts;
            UsesArt = usesArt;
            Head = head;
            HasHead = hasHead;
            Bounds = BoundsOf(parts);
            Lods = lods != null && lods.Length > 1 ? lods : new[] { new ModuleLod(parts, 0f) };
            LodSize = lodSize;
        }

        /// <summary>
        /// Every level of detail the art ships, finest first; <c>Lods[0].Parts</c> is
        /// <see cref="Parts"/>. A module without levels — or whose levels could not be drawn from
        /// one matrix — has exactly one (<c>docs/design/38-meadow-overhaul.md</c> §3).
        /// </summary>
        public ModuleLod[] Lods { get; }

        /// <summary>
        /// Whether the renderer draws this module by level: one bucket per placement, whose matrix
        /// serves every part of whichever level is chosen.
        ///
        /// <para><b>Only true when every part of every level sits at one local transform</b>,
        /// checked at load. That is what lets one matrix array draw a trunk, its branches and, at
        /// a distance, the card that replaces both — the Meadow art is built that way
        /// (<c>e-09</c> §1). Art that is not keeps the part-by-part path it always had.</para>
        /// </summary>
        public bool DrawsByLevel => Lods.Length > 1;

        /// <summary>The <c>LODGroup</c>'s size once placed, in metres: what the screen height of
        /// a level is measured against. Zero for a module without levels.</summary>
        public float LodSize { get; }

        /// <summary>Why this module draws its finest level only, when it has a LOD group but no
        /// levels were kept — or empty. So "why is this at full detail" has an answer without a
        /// debugger (design 38 §18c).</summary>
        public string LevelNote { get; internal set; } = string.Empty;

        /// <summary>
        /// Where this module's head bone sits, in the same space <see cref="ModulePart.Local"/> is
        /// in, so that <c>placement * Head</c> puts a thing on its head
        /// (<c>docs/design/29-modular-colonists.md</c>, MC6).
        ///
        /// <para><b>Captured at bake time because there is nothing to ask afterwards.</b> A baked
        /// module is a mesh and a matrix; the rig it came from was instantiated, posed, measured
        /// and destroyed inside one method. The head is read there, in the posed rig, and pushed
        /// through the same normalisation every part gets — so it inherits the pivot convention and
        /// the 1.4 scale rather than having them applied a second time by a caller who might get
        /// one of them wrong.</para>
        ///
        /// <para>Meaningless unless <see cref="HasHead"/>; most modules are walls.</para>
        /// </summary>
        public Matrix4x4 Head { get; }

        /// <summary>Whether this module had a head bone to find. False for everything but a body.</summary>
        public bool HasHead { get; }

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
        /// A copy of the pack material for each skinned overlay baked into a body — the bandit's
        /// vest (design 42) — keyed on the original. It exists so the vest is <b>not merged</b> into
        /// the body it shares a material with: the far form paints the two from different
        /// rectangles, and a merged mesh has one set. See <see cref="IsOverlayMaterial"/>.
        /// </summary>
        readonly Dictionary<Material, Material> _overlayTwins = new Dictionary<Material, Material>();
        readonly HashSet<Material> _overlayMaterials = new HashSet<Material>();

        /// <summary>Whether a baked part is a body's overlay (its vest) rather than the body itself.</summary>
        public bool IsOverlayMaterial(Material? material) =>
            material != null && _overlayMaterials.Contains(material);

        Material OverlayTwin(Material source)
        {
            if (_overlayTwins.TryGetValue(source, out Material twin)) return twin;
            twin = new Material(source) { name = source.name + "/overlay" };
            _overlayTwins[source] = twin;
            _overlayMaterials.Add(twin);
            return twin;
        }

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

            foreach (Material twin in _overlayTwins.Values)
            {
                if (twin == null) continue;
                if (Application.isPlaying) Object.Destroy(twin);
                else Object.DestroyImmediate(twin);
            }
            _overlayTwins.Clear();
            _overlayMaterials.Clear();

            if (_fallbackMaterial != null)
            {
                if (Application.isPlaying) Object.Destroy(_fallbackMaterial);
                else Object.DestroyImmediate(_fallbackMaterial);
                _fallbackMaterial = null;
            }

            if (_meadowGround != null)
            {
                if (Application.isPlaying) Object.Destroy(_meadowGround);
                else Object.DestroyImmediate(_meadowGround);
                _meadowGround = null;
            }

            foreach (Material plain in _plainGround.Values)
            {
                if (plain == null) continue;
                if (Application.isPlaying) Object.Destroy(plain);
                else Object.DestroyImmediate(plain);
            }
            _plainGround.Clear();
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
            Matrix4x4 head = Matrix4x4.identity;
            bool hasHead = false;
            ModuleLod[]? lods = null;
            float lodSize = 0f;
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
                parts = FlattenPrefab(entry.prefab!, entry, shape, out head, out hasHead,
                    out lods, out lodSize);
                usesArt = parts.Length > 0;
            }
            else
            {
                parts = new ModulePart[0];
            }

            if (parts.Length == 0)
            {
                // A natural terrain with no texture of its own — sand, the ore seams — is still
                // drawn by the ground shader when the Meadow look is on, so that it carries the
                // terrain mark the ink line reads (design 38 §17c). Still a fallback: it keeps the
                // palette colour a primitive is given.
                Material? plain = IsNaturalTerrain(moduleId) ? PlainGround(null) : null;
                parts = new[] { FallbackPart(shape, entry, plain, meshVariant, fallback: true) };
                _missing.Add(moduleId!);
            }

            var module = new ResolvedModule(moduleId!, shape, parts, usesArt, head, hasHead, lods, lodSize)
            {
                LevelNote = _levelNote,
            };
            _levelNote = string.Empty;
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

        /// <summary>
        /// The coarser levels of a prefab's <see cref="LODGroup"/>, placed exactly as the finest
        /// was, or null when the module should keep drawing its finest level only.
        ///
        /// <para><b>Null unless one matrix can draw every level</b> — one LOD group, and every part
        /// of every level at the same local transform as the finest level's first part. That is
        /// what the renderer relies on to keep a single matrix array per placement and swap only
        /// the meshes (<c>docs/design/38-meadow-overhaul.md</c> §3). A prefab that breaks it is not
        /// an error: it draws its finest level, as everything did before levels existed.</para>
        ///
        /// <para>A level holds the renderers its <c>LOD</c> names plus every renderer no level
        /// names, which is the same rule <see cref="HighestDetail"/> applies to the finest.</para>
        /// </summary>
        /// <summary>Why the last <see cref="CoarserLevels"/> kept no levels; read by the resolver.</summary>
        string _levelNote = string.Empty;

        ModuleLod[]? CoarserLevels(GameObject prefab, MeshFilter[] filters, ModuleEntry entry,
            Matrix4x4 rootInverse, Matrix4x4 place, ModulePart[] finest, out float size)
        {
            size = 0f;
            var groups = prefab.GetComponentsInChildren<LODGroup>(includeInactive: true);
            if (groups.Length == 0) return null;
            if (groups.Length != 1) { _levelNote = $"{groups.Length} LOD groups"; return null; }
            LOD[] levels = groups[0].GetLODs();
            if (levels.Length < 2) { _levelNote = "one level"; return null; }
            if (!SharesOneLocal(finest, finest[0].Local)) { _levelNote = "the finest level's parts sit apart"; return null; }

            var mentioned = new HashSet<Renderer>();
            foreach (LOD level in levels)
                foreach (Renderer renderer in level.renderers)
                    if (renderer != null) mentioned.Add(renderer);
            var unmentioned = new List<Renderer>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(includeInactive: false))
                if (!mentioned.Contains(renderer)) unmentioned.Add(renderer);

            var result = new ModuleLod[levels.Length];
            result[0] = new ModuleLod(finest, levels[0].screenRelativeTransitionHeight);
            for (int k = 1; k < levels.Length; k++)
            {
                var keep = new HashSet<Renderer>(unmentioned);
                foreach (Renderer renderer in levels[k].renderers)
                    if (renderer != null) keep.Add(renderer);

                var raw = new List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)>();
                var ignored = new Bounds();
                bool any = false;
                CollectStatic(filters, entry, keep, rootInverse, raw, ref ignored, ref any);
                if (raw.Count == 0) { _levelNote = $"level {k} collected no renderer"; return null; }

                List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)> merged = Merge(raw);
                var parts = new ModulePart[merged.Count];
                for (int i = 0; i < merged.Count; i++)
                    parts[i] = new ModulePart(merged[i].mesh, merged[i].submesh, merged[i].material,
                        place * merged[i].local, fallback: false);
                if (!SharesOneLocal(parts, finest[0].Local))
                {
                    _levelNote = $"level {k} sits apart from the finest: {parts[0].Local.GetColumn(3)} vs {finest[0].Local.GetColumn(3)}";
                    return null;
                }
                result[k] = new ModuleLod(parts, levels[k].screenRelativeTransitionHeight);
            }

            // The group's size is in its own space; carried through the prefab and the placement
            // it becomes the metres the screen height is measured against.
            size = groups[0].size * MaxScale(place * rootInverse * groups[0].transform.localToWorldMatrix);
            return result;
        }

        static bool SharesOneLocal(ModulePart[] parts, Matrix4x4 local)
        {
            const float tolerance = 1e-4f;
            for (int i = 0; i < parts.Length; i++)
                for (int c = 0; c < 16; c++)
                    if (Mathf.Abs(parts[i].Local[c] - local[c]) > tolerance) return false;
            return true;
        }

        static float MaxScale(Matrix4x4 m) => Mathf.Max(
            ((Vector3)m.GetColumn(0)).magnitude,
            Mathf.Max(((Vector3)m.GetColumn(1)).magnitude, ((Vector3)m.GetColumn(2)).magnitude));

        ModulePart[] FlattenPrefab(GameObject prefab, ModuleEntry entry, ModuleShape shape,
            out Matrix4x4 head, out bool hasHead, out ModuleLod[]? lods, out float lodSize)
        {
            head = Matrix4x4.identity;
            hasHead = false;
            lods = null;
            lodSize = 0f;

            var filters = prefab.GetComponentsInChildren<MeshFilter>(includeInactive: false);
            var raw = new List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)>();
            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
            HashSet<Renderer>? detail = HighestDetail(prefab);

            var bounds = new Bounds();
            bool hasBounds = false;

            CollectStatic(filters, entry, detail, rootInverse, raw, ref bounds, ref hasBounds);
            int staticCount = raw.Count;

            CollectSkinned(prefab, entry, raw, ref bounds, ref hasBounds,
                out Matrix4x4 headLocal, out hasHead);
            bool skinned = raw.Count > staticCount;

            if (raw.Count == 0) return new ModulePart[0];

            ModulePart[] parts = Place(raw, entry, ref bounds, out Matrix4x4 place);

            // The head goes through the same `place` every part does, so it inherits the pivot
            // convention and the scale rather than having them re-applied by a caller.
            if (hasHead) head = place * headLocal;

            // Every coarser level through the same `place` as the finest, never measured afresh:
            // a level normalised on its own bounds would sit a few centimetres off the one it
            // replaces, and the swap would be seen. A rig has no levels to take.
            if (!skinned) lods = CoarserLevels(prefab, filters, entry, rootInverse, place, parts, out lodSize);
            return parts;
        }

        void CollectStatic(MeshFilter[] filters, ModuleEntry entry, HashSet<Renderer>? keep,
            Matrix4x4 rootInverse, List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)> raw,
            ref Bounds bounds, ref bool hasBounds)
        {
            HashSet<Renderer>? detail = keep;
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
        }

        ModulePart[] Place(List<(Mesh mesh, int submesh, Material material, Matrix4x4 local)> raw,
            ModuleEntry entry, ref Bounds bounds, out Matrix4x4 place)
        {
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

            // A piece dropped on the ground that was modelled standing up lies on its broadest
            // face (ModuleEntry.lieFlat), turned before it is measured so the centring and the base
            // below see it as it lies.
            Quaternion lie = entry.lieFlat && hasExact ? LieFlat(bounds.size) : Quaternion.identity;
            if (entry.lieFlat && hasExact) bounds = TransformBounds(bounds, Matrix4x4.Rotate(lie));

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

            // A prop fitted to a footprint (design 32 §14, §14c). The rectangle is across by along
            // the facing, which is the model's own +Z — the way its front looks and the way the
            // second cell of a two-cell record lies. No quarter turn is guessed from the model's
            // proportions any more: that turned the air-conditioning unit side-on to its wall,
            // and which way a prop's front is is the row's business (its yaw), not its shape's.
            Vector3 scale = SafeScale(entry);
            Vector3 offset = entry.offset;
            if (FitScale(entry, bounds.size, out Vector3 fitted))
            {
                scale = fitted;
                if (entry.fitAgainstBack)
                    offset.z += BackOffset(entry.fitFootprint.y, bounds.size.z * fitted.z);
            }

            place = Matrix4x4.TRS(offset, Quaternion.Euler(0f, entry.yaw, 0f), scale)
                    * Matrix4x4.Translate(-normalise)
                    * Matrix4x4.Rotate(lie);

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
            ref Bounds bounds, ref bool hasBounds,
            out Matrix4x4 headLocal, out bool hasHead)
        {
            headLocal = Matrix4x4.identity;
            hasHead = false;

            if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true).Length == 0)
                return;

            GameObject instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                // The same bare head the live figures get. The packs ship hair, hats and hoods as
                // active skinned children, and this bakes every active one -- so without this a
                // colonist past the figure cap wore the pack's own hair while the same colonist in
                // front of the camera wore ours. Two drawers, one answer
                // (docs/design/29-modular-colonists.md, MC6).
                ColonistAttachments.BareTheHead(instance);

                // The bandit's vest back on (design 42), as the live figure wears it, so the far
                // form wears it too. Its parts get a twin material below so they are kept apart
                // from the body's in the merge.
                SkinnedMeshRenderer? overlay = ColonistAttachments.ShowOverlay(instance, entry.overlayName);

                // Pose it before the snapshot is taken, or the bind pose is what gets captured.
                if (entry.poseClip != null)
                    entry.poseClip!.SampleAnimation(instance, entry.poseClipTime);

                Matrix4x4 rootInverse = instance.transform.worldToLocalMatrix;

                // Read after the pose, so it is where the head actually is in the baked figure.
                var animator = instance.GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                {
                    Transform? headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (headBone != null)
                    {
                        headLocal = rootInverse * headBone.localToWorldMatrix;
                        hasHead = true;
                    }
                }
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
                        if (material != null && skin == overlay) material = OverlayTwin(material);
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

        /// <summary>
        /// The turn that stands a box's thinnest axis up: nothing when it already is, a roll about
        /// z when x is thinnest, a pitch about x when z is. See <see cref="ModuleEntry.lieFlat"/>.
        /// </summary>
        public static Quaternion LieFlat(Vector3 size)
        {
            if (size.y <= size.x && size.y <= size.z) return Quaternion.identity;
            return size.x <= size.z ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(-90f, 0f, 0f);
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
            int meshVariant = 0, bool? fallback = null)
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
                case ModuleShape.Sandbag: mesh = SandbagMesh.Mesh; break;
                default: mesh = PrimitiveMeshes.UnitCube; break;
            }

            return new ModulePart(
                mesh, 0, material ?? FallbackMaterial, local,
                fallback: fallback ?? material == null);
        }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");

        readonly Dictionary<(Material, float, bool), Material> _dressed =
            new Dictionary<(Material, float, bool), Material>();

        /// <summary>The painted meadow floor, built on first use; owned here and destroyed with the library.</summary>
        Material? _meadowGround;

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
            // The grass terrain is painted rather than tiled when the Meadow look is present
            // (design 38 §17): one material for every grass cell, blending the pack's terrain
            // textures by patches in world space. Everything else keeps its tiled texture.
            if (string.Equals(entry.moduleId, ModuleIds.Terrain("Grass"), System.StringComparison.Ordinal))
            {
                _meadowGround ??= MeadowLook.NewGroundMaterial();
                if (_meadowGround != null) return _meadowGround;
            }

            // Marsh wears the grass's material too, when the look has a wet texture: the ground
            // field then paints it into the meadow over metres, where its own tiled texture could
            // only change at a cell's edge — the ring of pale tiles round every stream (§24).
            if (string.Equals(entry.moduleId, ModuleIds.Terrain("Marsh"), System.StringComparison.Ordinal)
                && MeadowLook.PaintsMarsh)
            {
                _meadowGround ??= MeadowLook.NewGroundMaterial();
                if (_meadowGround != null) return _meadowGround;
            }

            Material source = entry.material!;

            // Every other natural terrain is drawn the same way, from its own texture, so that it
            // carries the terrain mark and the ink line leaves it alone (design 38 §17c).
            if (IsNaturalTerrain(entry.moduleId))
            {
                Material? plain = PlainGround(MainTextureOf(source));
                if (plain != null) return plain;
            }

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

        readonly Dictionary<Texture, Material> _plainGround = new Dictionary<Texture, Material>();

        /// <summary>The repeat of a plain terrain texture, in metres: the pack's own terrain layers.</summary>
        const float PlainGroundTileMetres = 4f;

        /// <summary>One ground material per texture, built on first use; null when the look is off.</summary>
        Material? PlainGround(Texture? top)
        {
            Texture key = top != null ? top : Texture2D.whiteTexture;
            if (_plainGround.TryGetValue(key, out Material? ready)) return ready;
            Material? made = MeadowLook.NewPlainGroundMaterial(top, PlainGroundTileMetres);
            if (made != null) _plainGround[key] = made;
            return made;
        }

        /// <summary>
        /// A terrain of the natural board drawn by the ground shader: anything under the terrain
        /// prefix but stone. The city's paving slabs are prefab rows and never reach here.
        ///
        /// <para><b>Stone keeps the pack's shader, and so keeps its ink</b> (design 38 §17c).
        /// Measured by photograph: through the ground shader a rock outcrop's chipped lumps drew a
        /// saturated blue, and back on the pack's material they are grey again. An outcrop is a
        /// thing standing on the meadow rather than a step of it, and its outline is what keeps it
        /// reading as rock — the owner's complaint was the terraced ground.</para>
        /// </summary>
        static bool IsNaturalTerrain(string? moduleId)
        {
            const string prefix = ModuleIds.Prefix + "terrain.";
            if (moduleId == null || !moduleId.StartsWith(prefix, System.StringComparison.Ordinal)) return false;
            string rest = moduleId.Substring(prefix.Length);
            int dot = rest.IndexOf('.');
            string terrain = dot < 0 ? rest : rest.Substring(0, dot);
            return !Stone.Contains(terrain);
        }

        /// <summary>
        /// The terrain names drawn as stone, lower-cased as the module ids spell them: every terrain
        /// <see cref="RockLook.IsStone"/> says is stone, read off the terrain table once, so a new
        /// ore or deep stone keeps the pack's shader without being listed here by hand (design 62).
        ///
        /// <para>Lazy rather than a static initialiser: the terrain table is the content pack, and a
        /// built player only knows where its pack is once the composition root has said.</para>
        /// </summary>
        static HashSet<string> Stone => _stone ??= StoneNames();

        static HashSet<string>? _stone;

        static HashSet<string> StoneNames()
        {
            var names = new HashSet<string>();
            for (int i = 0; i < Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainCount; i++)
                if (RockLook.IsStone((ushort)i))
                    names.Add(Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainAt((ushort)i).defName.ToLowerInvariant());
            return names;
        }

        /// <summary>The albedo a pack material draws with, by the names the packs use.</summary>
        static Texture? MainTextureOf(Material material)
        {
            if (material.HasProperty(BaseMapId) && material.GetTexture(BaseMapId) != null) return material.GetTexture(BaseMapId);
            if (material.HasProperty(MainTexId) && material.GetTexture(MainTexId) != null) return material.GetTexture(MainTexId);
            foreach (string name in material.GetTexturePropertyNames())
            {
                string lower = name.ToLowerInvariant();
                if (lower.Contains("normal") || lower.Contains("bump") || lower.Contains("noise")) continue;
                if (!(lower.Contains("albedo") || lower.Contains("base") || lower.Contains("texture") || lower.Contains("colour") || lower.Contains("color"))) continue;
                Texture? found = material.GetTexture(name);
                if (found != null) return found;
            }
            return null;
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

        /// <summary>
        /// The scale that fits a model of this size into a row's <c>fitFootprint</c> and
        /// <c>fitHeight</c> (design 32 §14c): uniform by its tightest axis, or each axis filled on
        /// its own when the row says <c>fitStretch</c>. False when the row asks for no fit.
        /// </summary>
        public static bool FitScale(ModuleEntry entry, Vector3 size, out Vector3 scale)
        {
            scale = Vector3.one;
            if (entry.fitFootprint.x <= 0f || entry.fitFootprint.y <= 0f || size.x <= 0f || size.z <= 0f)
                return false;

            float across = entry.fitFootprint.x / size.x;
            float along = entry.fitFootprint.y / size.z;
            float up = entry.fitHeight > 0f && size.y > 0f ? entry.fitHeight / size.y : float.PositiveInfinity;
            if (entry.fitStretch)
            {
                // Height takes the across scale when no ceiling is set, so an unbounded row does
                // not come out flat or a tower.
                scale = new Vector3(across, float.IsPositiveInfinity(up) ? across : up, along);
                return true;
            }

            float fit = Mathf.Min(Mathf.Min(across, along), up);
            scale = new Vector3(fit, fit, fit);
            return true;
        }

        /// <summary>
        /// How far along +Z a centred model of this fitted depth moves so its back (-Z) stands on
        /// the back edge of a rectangle this long (design 32 §14c).
        /// </summary>
        public static float BackOffset(float length, float depth) => (depth - length) * 0.5f;

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
                case ModuleShape.Sandbag:
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
