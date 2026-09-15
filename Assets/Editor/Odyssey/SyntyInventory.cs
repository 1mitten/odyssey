#nullable enable
// Phase 0 ground tooling: produces the Synty asset inventory that fixes the cell size.
// Not gameplay code. Written without a compiler at hand; expect to fix small things on first run.
//
// Run from the menu:   Odyssey > Phase 0 > Run Synty inventory
// Run headless:        Unity -batchmode -nographics -projectPath . \
//                        -executeMethod Odyssey.EditorTools.SyntyInventory.Run \
//                        -logFile Logs/synty-inventory.log -quit
//
// Output: docs/research/synty-inventory.md and docs/research/synty-inventory.csv
//         (repository root is taken to be Application.dataPath/..)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    public static class SyntyInventory
    {
        const string SyntyRoot = "Assets/Synty";
        const float SnapTolerance = 0.02f; // metres
        static readonly float[] CandidatePitches = { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 4f };
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        sealed class PieceRecord
        {
            public string Path = "";
            public string Name = "";
            public string Family = "";   // Bld, Prop, Veh, Wep, Env, Chr, Sign, FX, Generic, Other
            public string Role = "";     // Wall, Floor, Roof, Door, Window, Stair, Pillar, Railing, Ladder, Other
            public Vector3 Size;
            public Vector3 Min;
            public Vector3 Max;
            public int Renderers;
            public long Triangles;
            public int Materials;
            public bool Skinned;
        }

        static readonly (string role, Regex rx)[] RoleRules =
        {
            ("Stair",   new Regex(@"(^|_)Stair",                        RegexOptions.IgnoreCase)),
            ("Ladder",  new Regex(@"(^|_)Ladder",                       RegexOptions.IgnoreCase)),
            ("Door",    new Regex(@"(^|_)Door",                         RegexOptions.IgnoreCase)),
            ("Window",  new Regex(@"(^|_)Window",                       RegexOptions.IgnoreCase)),
            ("Roof",    new Regex(@"(^|_)Roof",                         RegexOptions.IgnoreCase)),
            ("Floor",   new Regex(@"(^|_)(Floor|Tile)",                 RegexOptions.IgnoreCase)),
            ("Pillar",  new Regex(@"(^|_)(Pillar|Column|Support|Beam)", RegexOptions.IgnoreCase)),
            ("Railing", new Regex(@"(^|_)(Rail|Fence|Barrier)",         RegexOptions.IgnoreCase)),
            ("Wall",    new Regex(@"(^|_)Wall",                         RegexOptions.IgnoreCase)),
        };

        [MenuItem("Odyssey/Phase 0/Run Synty inventory")]
        public static void RunFromMenu() => RunInternal(exitWhenDone: false);

        /// <summary>Batchmode entry point (-executeMethod). Exits the editor with 0 on success, 1 on failure.</summary>
        public static void Run() => RunInternal(exitWhenDone: Application.isBatchMode);

        static void RunInternal(bool exitWhenDone)
        {
            int exitCode = 0;
            try
            {
                if (!AssetDatabase.IsValidFolder(SyntyRoot))
                    throw new DirectoryNotFoundException(
                        $"{SyntyRoot} does not exist. Import the pack, then move it under {SyntyRoot} inside the Project window.");

                var md = new StringBuilder();
                var csv = new StringBuilder();
                md.AppendLine("# Synty asset inventory (generated)");
                md.AppendLine();
                md.AppendLine($"Generated {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", Inv)} UTC by `SyntyInventory.cs` on Unity {Application.unityVersion}. Do not edit by hand; re-run the script.");
                md.AppendLine();

                ReportPacks(md);
                ReportPipeline(md);
                var pieces = CollectPieces();
                ReportPieces(md, csv, pieces);
                ReportGrid(md, pieces);
                ReportCharacters(md, pieces);
                ReportAnimations(md);
                ReportMaterials(md);
                ReportScenes(md);

                string docs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "research"));
                Directory.CreateDirectory(docs);
                File.WriteAllText(Path.Combine(docs, "synty-inventory.md"), md.ToString());
                File.WriteAllText(Path.Combine(docs, "synty-inventory.csv"), csv.ToString());
                Debug.Log($"[SyntyInventory] wrote {docs}/synty-inventory.md ({pieces.Count} pieces)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SyntyInventory] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        // ---------------------------------------------------------------- packs

        static void ReportPacks(StringBuilder md)
        {
            md.AppendLine("## Packs present under Assets/Synty");
            md.AppendLine();
            md.AppendLine("| Folder | Assets | Notes (first lines of any top-level .txt/.md) |");
            md.AppendLine("|---|---|---|");
            foreach (string folder in AssetDatabase.GetSubFolders(SyntyRoot))
            {
                int count = AssetDatabase.FindAssets("", new[] { folder }).Length;
                string notes = "";
                string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", folder));
                try
                {
                    var textFiles = Directory.GetFiles(full, "*.txt", SearchOption.TopDirectoryOnly)
                        .Concat(Directory.GetFiles(full, "*.md", SearchOption.TopDirectoryOnly));
                    notes = string.Join("<br>", textFiles.Select(f =>
                        Path.GetFileName(f) + ": " + string.Join(" / ",
                            File.ReadLines(f).Where(l => !string.IsNullOrWhiteSpace(l)).Take(3).Select(l => l.Trim()))));
                }
                catch (Exception e) { notes = "(could not read: " + e.Message + ")"; }
                md.AppendLine($"| `{folder}` | {count} | {Escape(notes)} |");
            }
            md.AppendLine();
            md.AppendLine("Pack version strings are shown by the Package Manager (My Assets); copy them into `synty-import.md`.");
            md.AppendLine();
        }

        // ------------------------------------------------------------ pipeline

        static void ReportPipeline(StringBuilder md)
        {
            md.AppendLine("## Render pipeline");
            md.AppendLine();
            var rp = GraphicsSettings.currentRenderPipeline;
            md.AppendLine($"- Active render pipeline asset: `{(rp == null ? "none (Built-in)" : rp.GetType().Name + " / " + rp.name)}`");
            md.AppendLine($"- Colour space: `{PlayerSettings.colorSpace}`");
            string urpVersion = "unknown";
            try
            {
                string manifest = File.ReadAllText(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
                var m = Regex.Match(manifest, "\"com\\.unity\\.render-pipelines\\.universal\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) urpVersion = m.Groups[1].Value;
            }
            catch { /* reported as unknown */ }
            md.AppendLine($"- URP package version (manifest): `{urpVersion}`");
            md.AppendLine();
        }

        // -------------------------------------------------------------- pieces

        static List<PieceRecord> CollectPieces()
        {
            var records = new List<PieceRecord>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { SyntyRoot });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[SyntyInventory] no prefabs found; falling back to model (FBX) assets");
                guids = AssetDatabase.FindAssets("t:Model", new[] { SyntyRoot });
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var rec = new PieceRecord
                {
                    Path = path,
                    Name = go.name,
                    Family = ClassifyFamily(go.name),
                    Role = ClassifyRole(go.name),
                };
                if (TryComputeBounds(go, out var b, out rec.Renderers, out rec.Triangles, out rec.Materials, out rec.Skinned))
                {
                    rec.Size = b.size;
                    rec.Min = b.min;
                    rec.Max = b.max;
                }
                records.Add(rec);
            }
            return records;
        }

        static string ClassifyFamily(string name)
        {
            // Synty naming: SM_Bld_Wall_01, SK_Chr_Worker_01, FX_..., SM_Prop_..., SM_Veh_..., SM_Wep_..., SM_Env_..., SM_Sign_...
            string[] tokens = name.Split('_');
            if (tokens.Length >= 3 && Regex.IsMatch(tokens[0], "^(SM|SK|FX|PP|SF)$", RegexOptions.IgnoreCase))
                return tokens[1];
            return "Other";
        }

        static string ClassifyRole(string name)
        {
            foreach (var (role, rx) in RoleRules)
                if (rx.IsMatch(name)) return role;
            return "Other";
        }

        static bool TryComputeBounds(GameObject root, out Bounds bounds, out int rendererCount, out long triangles,
            out int materials, out bool skinned)
        {
            bounds = default;
            rendererCount = 0;
            triangles = 0;
            materials = 0;
            skinned = false;
            bool has = false;
            var matSet = new HashSet<Material>();
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix; // root-relative, so pivot placement is measurable

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh? mesh = null;
                if (r is SkinnedMeshRenderer smr)
                {
                    mesh = smr.sharedMesh;
                    skinned = true;
                }
                else if (r.TryGetComponent<MeshFilter>(out var mf))
                {
                    mesh = mf.sharedMesh;
                }
                if (mesh == null) continue;

                rendererCount++;
                foreach (Material m in r.sharedMaterials)
                    if (m != null) matSet.Add(m);
                triangles += TriangleCount(mesh);

                Matrix4x4 local = toRoot * r.transform.localToWorldMatrix;
                foreach (Vector3 corner in Corners(mesh.bounds))
                {
                    Vector3 p = local.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else bounds.Encapsulate(p);
                }
            }
            materials = matSet.Count;
            return has;
        }

        static long TriangleCount(Mesh mesh)
        {
            try
            {
                long n = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) n += mesh.GetIndexCount(i);
                return n / 3;
            }
            catch { return -1; }
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            Vector3 min = b.min, max = b.max;
            yield return new Vector3(min.x, min.y, min.z);
            yield return new Vector3(min.x, min.y, max.z);
            yield return new Vector3(min.x, max.y, min.z);
            yield return new Vector3(min.x, max.y, max.z);
            yield return new Vector3(max.x, min.y, min.z);
            yield return new Vector3(max.x, min.y, max.z);
            yield return new Vector3(max.x, max.y, min.z);
            yield return new Vector3(max.x, max.y, max.z);
        }

        static void ReportPieces(StringBuilder md, StringBuilder csv, List<PieceRecord> pieces)
        {
            csv.AppendLine("path,name,family,role,sizeX,sizeY,sizeZ,minX,minY,minZ,maxX,maxY,maxZ,renderers,triangles,materials,skinned");
            foreach (var p in pieces.OrderBy(p => p.Path, StringComparer.Ordinal))
            {
                csv.AppendLine(string.Join(",",
                    Csv(p.Path), Csv(p.Name), p.Family, p.Role,
                    F(p.Size.x), F(p.Size.y), F(p.Size.z),
                    F(p.Min.x), F(p.Min.y), F(p.Min.z),
                    F(p.Max.x), F(p.Max.y), F(p.Max.z),
                    p.Renderers.ToString(Inv), p.Triangles.ToString(Inv), p.Materials.ToString(Inv), p.Skinned ? "1" : "0"));
            }

            md.AppendLine("## Piece counts by family and role");
            md.AppendLine();
            md.AppendLine($"{pieces.Count} prefabs under `{SyntyRoot}`. Full per-prefab data in `synty-inventory.csv`.");
            md.AppendLine();
            string[] roles = { "Wall", "Floor", "Roof", "Door", "Window", "Stair", "Pillar", "Railing", "Ladder", "Other" };
            md.AppendLine("| Family | " + string.Join(" | ", roles) + " | Total |");
            md.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---:|", roles.Length + 1)));
            foreach (var fam in pieces.GroupBy(p => p.Family).OrderByDescending(g => g.Count()))
            {
                var cells = roles.Select(r => fam.Count(p => p.Role == r).ToString(Inv));
                md.AppendLine($"| {fam.Key} | {string.Join(" | ", cells)} | {fam.Count()} |");
            }
            md.AppendLine();

            md.AppendLine("## Building-module dimensions (family Bld), by role");
            md.AppendLine();
            md.AppendLine("Sizes are root-relative axis-aligned bounds in metres, X × Y(height) × Z, rounded to 0.01. The most common sizes per role are listed with counts and one example. Pivot columns give the fraction of pieces whose bounds start at y=0 (base pivot), are centred on x and z (centre pivot), or start at x=0 and z=0 (corner pivot).");
            md.AppendLine();
            var bld = pieces.Where(p => string.Equals(p.Family, "Bld", StringComparison.OrdinalIgnoreCase)).ToList();
            if (bld.Count == 0)
            {
                md.AppendLine("No pieces in family `Bld` were found. Check the naming convention of this pack and adjust `ClassifyFamily`.");
                md.AppendLine();
            }
            foreach (string role in roles)
            {
                var group = bld.Where(p => p.Role == role).ToList();
                if (group.Count == 0) continue;
                md.AppendLine($"### {role} ({group.Count})");
                md.AppendLine();
                md.AppendLine("| Size X × Y × Z (m) | Count | Example |");
                md.AppendLine("|---|---:|---|");
                foreach (var g in group.GroupBy(p => $"{F(p.Size.x)} × {F(p.Size.y)} × {F(p.Size.z)}")
                             .OrderByDescending(g => g.Count()).Take(8))
                    md.AppendLine($"| {g.Key} | {g.Count()} | `{g.First().Name}` |");
                float basePivot = Fraction(group, p => Mathf.Abs(p.Min.y) <= SnapTolerance);
                float centrePivot = Fraction(group, p => Mathf.Abs(p.Min.x + p.Max.x) <= 2 * SnapTolerance && Mathf.Abs(p.Min.z + p.Max.z) <= 2 * SnapTolerance);
                float cornerPivot = Fraction(group, p => Mathf.Abs(p.Min.x) <= SnapTolerance && Mathf.Abs(p.Min.z) <= SnapTolerance);
                md.AppendLine();
                md.AppendLine($"Pivot: base {Pct(basePivot)}, centre {Pct(centrePivot)}, corner {Pct(cornerPivot)}. Triangles per piece: median {Median(group.Select(p => (float)p.Triangles))}, max {group.Max(p => p.Triangles)}.");
                md.AppendLine();
            }
        }

        // ---------------------------------------------------------------- grid

        static void ReportGrid(StringBuilder md, List<PieceRecord> pieces)
        {
            md.AppendLine("## Grid pitch test (this fixes the cell size)");
            md.AppendLine();
            md.AppendLine($"For each candidate pitch, the share of pieces whose measure is a positive integer multiple of the pitch within ±{F(SnapTolerance)} m. Wall width is the larger of X and Z; wall thickness the smaller. Floor footprint tests X and Z separately. Height is Y.");
            md.AppendLine();

            var bld = pieces.Where(p => string.Equals(p.Family, "Bld", StringComparison.OrdinalIgnoreCase)).ToList();
            var walls = bld.Where(p => p.Role == "Wall" || p.Role == "Door" || p.Role == "Window").ToList();
            var floors = bld.Where(p => p.Role == "Floor" || p.Role == "Roof").ToList();

            var wallWidths = walls.Select(p => Mathf.Max(p.Size.x, p.Size.z)).ToList();
            var wallThick = walls.Select(p => Mathf.Min(p.Size.x, p.Size.z)).ToList();
            var wallHeights = walls.Select(p => p.Size.y).ToList();
            var floorFoot = floors.SelectMany(p => new[] { p.Size.x, p.Size.z }).ToList();
            var floorThick = floors.Select(p => p.Size.y).ToList();

            md.AppendLine("| Pitch (m) | Wall widths | Wall heights | Floor/roof footprints |");
            md.AppendLine("|---:|---:|---:|---:|");
            foreach (float pitch in CandidatePitches)
                md.AppendLine($"| {F(pitch)} | {Pct(SnapShare(wallWidths, pitch))} | {Pct(SnapShare(wallHeights, pitch))} | {Pct(SnapShare(floorFoot, pitch))} |");
            md.AppendLine();

            md.AppendLine("| Measure | Mode (m) | Median (m) | Min (m) | Max (m) | n |");
            md.AppendLine("|---|---:|---:|---:|---:|---:|");
            Row(md, "Wall width", wallWidths);
            Row(md, "Wall thickness", wallThick);
            Row(md, "Wall height", wallHeights);
            Row(md, "Floor/roof footprint (X and Z)", floorFoot);
            Row(md, "Floor/roof thickness", floorThick);
            md.AppendLine();

            float footPitch = BestPitch(floorFoot.Concat(wallWidths).ToList());
            float heightPitch = BestPitch(wallHeights);
            md.AppendLine($"**Implied cell (to be confirmed by a human, not a decision):** footprint pitch {F(footPitch)} m, height pitch {F(heightPitch)} m → cell {F(footPitch)} × {F(footPitch)} × {F(heightPitch)} m. \"Best\" is the largest candidate pitch that at least 80% of measures snap to; 0 means nothing reached 80%, so look at the histogram above.");
            md.AppendLine();
        }

        static float SnapShare(List<float> values, float pitch) =>
            values.Count == 0 ? 0f : values.Count(v => Snaps(v, pitch)) / (float)values.Count;

        static bool Snaps(float v, float pitch)
        {
            float q = v / pitch;
            float r = Mathf.Round(q);
            return r >= 1f && Mathf.Abs(q - r) * pitch <= SnapTolerance;
        }

        static float BestPitch(List<float> values)
        {
            float best = 0f;
            foreach (float pitch in CandidatePitches)
                if (SnapShare(values, pitch) >= 0.8f) best = pitch;
            return best;
        }

        static void Row(StringBuilder md, string label, List<float> values)
        {
            if (values.Count == 0)
            {
                md.AppendLine($"| {label} | – | – | – | – | 0 |");
                return;
            }
            string mode = values.GroupBy(v => F(v)).OrderByDescending(g => g.Count()).First().Key;
            md.AppendLine($"| {label} | {mode} | {Median(values)} | {F(values.Min())} | {F(values.Max())} | {values.Count} |");
        }

        // ----------------------------------------------------------- characters

        static void ReportCharacters(StringBuilder md, List<PieceRecord> pieces)
        {
            md.AppendLine("## Characters and rigs");
            md.AppendLine();
            int skinnedPrefabs = pieces.Count(p => p.Skinned);
            md.AppendLine($"Prefabs with a SkinnedMeshRenderer: {skinnedPrefabs}.");
            md.AppendLine();
            md.AppendLine("| Model (FBX) | Rig type | Avatar | Bones | Skinned triangles | Height (m) |");
            md.AppendLine("|---|---|---|---:|---:|---:|");
            int listed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { SyntyRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (importer == null || go == null) continue;
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr == null) continue; // static mesh, not a character

                string avatar = "none";
                foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                    if (obj is Avatar av) avatar = av.isValid ? (av.isHuman ? "humanoid, valid" : "generic, valid") : "invalid";
                long tris = smr.sharedMesh != null ? TriangleCount(smr.sharedMesh) : -1;
                string height = TryComputeBounds(go, out var b, out _, out _, out _, out _) ? F(b.size.y) : "?";
                md.AppendLine($"| `{Path.GetFileName(path)}` | {importer.animationType} | {avatar} | {smr.bones.Length} | {tris} | {height} |");
                listed++;
            }
            if (listed == 0) md.AppendLine("| (no skinned models found) | | | | | |");
            md.AppendLine();
        }

        // ----------------------------------------------------------- animations

        static void ReportAnimations(StringBuilder md)
        {
            md.AppendLine("## Animation clips and controllers");
            md.AppendLine();
            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { SyntyRoot });
            int controllers = AssetDatabase.FindAssets("t:AnimatorController", new[] { SyntyRoot }).Length;
            md.AppendLine($"Animation clips: {clipGuids.Length}. Animator controllers: {controllers}.");
            md.AppendLine();
            if (clipGuids.Length > 0)
            {
                md.AppendLine("| Clip | Length (s) | Humanoid | Asset |");
                md.AppendLine("|---|---:|---|---|");
                int shown = 0;
                foreach (string guid in clipGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).Concat(new[] { AssetDatabase.LoadMainAssetAtPath(path) }))
                    {
                        if (obj is not AnimationClip clip) continue;
                        if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                        if (shown++ >= 150) break;
                        md.AppendLine($"| {Escape(clip.name)} | {F(clip.length)} | {(clip.isHumanMotion ? "yes" : "no")} | `{Path.GetFileName(path)}` |");
                    }
                }
                if (shown > 150) md.AppendLine($"| … and {shown - 150} more | | | |");
                md.AppendLine();
            }
        }

        // ------------------------------------------------------------ materials

        static void ReportMaterials(StringBuilder md)
        {
            md.AppendLine("## Materials, shaders and textures");
            md.AppendLine();
            var byShader = new Dictionary<string, int>();
            var broken = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { SyntyRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                string shader = mat.shader != null ? mat.shader.name : "(null shader)";
                byShader[shader] = byShader.TryGetValue(shader, out int n) ? n + 1 : 1;
                if (mat.shader == null || shader == "Hidden/InternalErrorShader" || shader.StartsWith("Hidden/", StringComparison.Ordinal))
                    broken.Add(path);
            }
            md.AppendLine("| Shader | Materials | URP? |");
            md.AppendLine("|---|---:|---|");
            foreach (var kv in byShader.OrderByDescending(kv => kv.Value))
            {
                bool urp = kv.Key.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)
                           || kv.Key.StartsWith("Shader Graphs/", StringComparison.Ordinal);
                md.AppendLine($"| `{Escape(kv.Key)}` | {kv.Value} | {(urp ? "yes" : "check")} |");
            }
            md.AppendLine();
            md.AppendLine(broken.Count == 0
                ? "No materials with an error or hidden shader (nothing should render magenta)."
                : $"**{broken.Count} materials have an error/hidden shader (will render magenta):**");
            foreach (string p in broken.Take(50)) md.AppendLine($"- `{p}`");
            if (broken.Count > 50) md.AppendLine($"- … and {broken.Count - 50} more");
            md.AppendLine();

            var texSizes = new Dictionary<string, int>();
            int texCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SyntyRoot }))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (tex == null) continue;
                texCount++;
                string key = $"{tex.width}×{tex.height}";
                texSizes[key] = texSizes.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            md.AppendLine($"Textures: {texCount}. Sizes: " + string.Join(", ", texSizes.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} ×{kv.Value}")) + ".");
            md.AppendLine();
            md.AppendLine("Lane E needs to know whether the pack uses one shared colour atlas (typical for Synty: a few 1024/2048 atlases and vertex-colour or UV-offset tinting) because that decides the runtime \"stuff\" tint strategy.");
            md.AppendLine();
        }

        // --------------------------------------------------------------- scenes

        static void ReportScenes(StringBuilder md)
        {
            md.AppendLine("## Scenes (opened one by one; errors counted during load)");
            md.AppendLine();
            md.AppendLine("| Scene | Root objects | Renderers | Errors | Warnings |");
            md.AppendLine("|---|---:|---:|---:|---:|");
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { SyntyRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                int errors = 0, warnings = 0;
                void OnLog(string condition, string stackTrace, LogType type)
                {
                    if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
                    else if (type == LogType.Warning) warnings++;
                }
                Application.logMessageReceived += OnLog;
                int roots = -1, renderers = -1;
                try
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var rootObjects = scene.GetRootGameObjects();
                    roots = rootObjects.Length;
                    renderers = rootObjects.Sum(r => r.GetComponentsInChildren<Renderer>(true).Length);
                }
                catch (Exception e)
                {
                    errors++;
                    Debug.LogError($"[SyntyInventory] opening {path} threw: {e.Message}");
                }
                finally
                {
                    Application.logMessageReceived -= OnLog;
                }
                md.AppendLine($"| `{path}` | {roots} | {renderers} | {errors} | {warnings} |");
            }
            if (guids.Length == 0) md.AppendLine("| (no scenes found) | | | | |");
            md.AppendLine();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // -------------------------------------------------------------- helpers

        static string F(float v) => v.ToString("0.00", Inv);
        static string Pct(float f) => (f * 100f).ToString("0", Inv) + "%";
        static float Fraction(List<PieceRecord> items, Func<PieceRecord, bool> pred) =>
            items.Count == 0 ? 0f : items.Count(pred) / (float)items.Count;

        static string Median(IEnumerable<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return "–";
            int mid = sorted.Count / 2;
            float m = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2f;
            return F(m);
        }

        static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        static string Escape(string s) => s.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
    }
}
