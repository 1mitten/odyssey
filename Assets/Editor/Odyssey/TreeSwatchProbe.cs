#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Measure how the two tree meshes use the PolygonGeneric atlas, before any recolouring is
    /// built on top of them.
    ///
    /// <para><c>SwatchProbe</c> asked this question of character bodies and answered it: a Synty
    /// mesh paints each region from one <b>flat</b> cell of the pack atlas, so a shader can
    /// replace the colour inside a rectangle and throw no art away. A tree is not a body, and the
    /// claim has to be made again for it — a canopy could easily carry a gradient or a leaf
    /// texture rather than a flat swatch, and if it does then the swatch mechanism is the wrong
    /// one here and the tint has to be a multiply instead.</para>
    ///
    /// <para>Two things are printed beside each cluster that the character probe had no use for.
    /// The <b>height band</b> of the vertices in it, as a fraction of the mesh's own height, is
    /// what tells bark from canopy without anybody eyeballing the atlas: a trunk cluster sits low
    /// and narrow, a canopy cluster high and wide. And the <b>alpha</b> of the sampled texels,
    /// because these materials are alpha-clipped and a cluster that is mostly cut away is a leaf
    /// card rather than a swatch.</para>
    ///
    /// <para><b>This writes nothing.</b> No asset is touched and no pixel is copied anywhere; the
    /// atlas is read by loading the PNG's bytes into a throwaway texture, so no import setting
    /// under the gitignored <c>Assets/Synty</c> changes either. What survives the run is a table
    /// of numbers in the log, which is the same standing the mesh widths recorded in
    /// <c>PlayScene</c> already have.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh exec Odyssey.EditorTools.TreeSwatchProbe.Run</c>.
    /// No graphics device needed.</para>
    /// </summary>
    public static class TreeSwatchProbe
    {
        /// <summary>See <c>SwatchProbe.MergeTolerance</c>: well under one atlas cell's width.</summary>
        const float MergeTolerance = 0.004f;

        /// <summary>Clusters smaller than this are counted in the tail, not listed.</summary>
        const int InterestingVertices = 4;

        /// <summary>
        /// The meshes the board's woodland is drawn from, as <c>PlayScene</c> casts them, plus the
        /// other Generic trees so that a later session choosing a second broadleaf has the numbers
        /// already.
        /// </summary>
        static readonly string[] Trees =
        {
            "SM_Gen_Env_Tree_Pine_01",
            "SM_Gen_Env_Tree_03",
            "SM_Gen_Env_Tree_01",
            "SM_Gen_Env_Tree_02",
            "SM_Gen_Env_Tree_04",
            "SM_Gen_Env_Tree_Pine_02",
            "SM_Gen_Env_Tree_Pine_03",
        };

        [MenuItem("Odyssey/Presentation/Probe the tree swatches")]
        public static void RunFromMenu() => Execute();

        public static void Run() => Execute();

        /// <summary>
        /// Does <c>Odyssey/Tree</c> compile, and with what?
        ///
        /// A shader that fails to compile renders magenta and is noticed; one that compiles with
        /// warnings, or silently drops a pass, is not — and a tree missing its DepthOnly pass
        /// would quietly lose the outline every other solid thing in the world has. Run headless
        /// with <c>scripts/unity.sh exec Odyssey.EditorTools.TreeSwatchProbe.CheckShader</c>.
        /// </summary>
        public static void CheckShader()
        {
            Shader? shader = Shader.Find("Odyssey/Tree");
            if (shader == null)
            {
                Debug.LogError("[TreeSwatchProbe] Odyssey/Tree not found");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("=== Odyssey/Tree ===");
            report.AppendLine($"passes {shader.passCount}, render queue {shader.renderQueue}, " +
                              $"errors {ShaderUtil.ShaderHasError(shader)}");

            ShaderData data = ShaderUtil.GetShaderData(shader);
            report.AppendLine($"subshaders {data.SubshaderCount}");
            for (int i = 0; i < data.SubshaderCount; i++)
            {
                ShaderData.Subshader sub = data.GetSubshader(i);
                report.AppendLine($"  subshader {i}: {sub.PassCount} passes");
                for (int j = 0; j < sub.PassCount; j++)
                    report.AppendLine($"    pass {j}: name '{sub.GetPass(j).Name}'");
            }

            var messages = ShaderUtil.GetShaderMessages(shader);
            foreach (var m in messages)
                report.AppendLine($"  [{m.severity}] {m.message} {m.messageDetails} ({m.file}:{m.line})");
            if (messages.Length == 0) report.AppendLine("  no messages");

            if (ShaderUtil.ShaderHasError(shader)) Debug.LogError(report.ToString());
            else Debug.Log(report.ToString());
        }

        /// <summary>
        /// Which shader keywords <c>Odyssey/Tree</c> declares and the pack's tree shader does not,
        /// and the other way round.
        ///
        /// <para>Written to settle one question by looking rather than by reasoning:
        /// <c>TreeCheck</c> measures our shader drawing a tree about a tenth darker than
        /// <c>Synty/Generic_Basic</c> draws the same tree, and a keyword one declares and the other
        /// does not is the kind of difference that produces exactly that — a shader that declares
        /// <c>_SCREEN_SPACE_OCCLUSION</c> is darkened by the renderer's ambient occlusion and one
        /// that does not is left alone. The normal map was the first guess and the contact sheet
        /// falsified it: with the map switched off the picture was identical to the byte.</para>
        ///
        /// <para>Headless: <c>scripts/unity.sh exec Odyssey.EditorTools.TreeSwatchProbe.CompareShaders</c>.
        /// </para>
        /// </summary>
        public static void CompareShaders()
        {
            var report = new StringBuilder();
            report.AppendLine("=== tree shader keywords ===");

            Shader? ours = Shader.Find("Odyssey/Tree");
            Material? packMaterial = null;
            foreach (string guid in AssetDatabase.FindAssets("Generic_01_A t:Material", new[] { "Assets/Synty" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != "Generic_01_A") continue;
                packMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                break;
            }

            Shader? theirs = packMaterial == null ? null : packMaterial.shader;
            if (ours == null || theirs == null)
            {
                Debug.LogWarning($"[TreeSwatchProbe] ours={(ours == null ? "missing" : ours.name)} " +
                                 $"theirs={(theirs == null ? "missing (no packs?)" : theirs.name)}");
                return;
            }

            var mine = new HashSet<string>(ours.keywordSpace.keywordNames);
            var pack = new HashSet<string>(theirs.keywordSpace.keywordNames);

            report.AppendLine($"ours   '{ours.name}': {mine.Count} keywords");
            report.AppendLine($"theirs '{theirs.name}': {pack.Count} keywords");

            var onlyMine = new List<string>(mine); onlyMine.RemoveAll(pack.Contains); onlyMine.Sort();
            var onlyPack = new List<string>(pack); onlyPack.RemoveAll(mine.Contains); onlyPack.Sort();

            report.AppendLine("only ours:   " + (onlyMine.Count == 0 ? "(none)" : string.Join(", ", onlyMine)));
            report.AppendLine("only theirs: " + (onlyPack.Count == 0 ? "(none)" : string.Join(", ", onlyPack)));

            Debug.Log(report.ToString());
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/tree-shader-keywords.txt", report.ToString());
        }

        static void Execute()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Tree swatch probe ===");
            report.AppendLine($"merge tolerance {MergeTolerance:0.0000} UV");
            report.AppendLine();

            var atlases = new Dictionary<Texture, Texture2D?>();
            try
            {
                foreach (string name in Trees) Probe(name, report, atlases);
            }
            finally
            {
                foreach (Texture2D? copy in atlases.Values)
                    if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            }

            Debug.Log(report.ToString());
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/tree-swatches.txt", report.ToString());
        }

        static void Probe(string prefabName, StringBuilder report, Dictionary<Texture, Texture2D?> atlases)
        {
            GameObject? prefab = FindPrefab(prefabName);
            if (prefab == null)
            {
                report.AppendLine($"-- {prefabName}: NOT FOUND (no packs?)");
                report.AppendLine();
                return;
            }

            MeshFilter? filter = prefab.GetComponentInChildren<MeshFilter>(true);
            var renderer = filter == null ? null : filter.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null)
            {
                report.AppendLine($"-- {prefabName}: no mesh");
                report.AppendLine();
                return;
            }

            Mesh mesh = filter.sharedMesh;
            Vector2[] uv = mesh.uv;
            Vector3[] position = mesh.vertices;

            var clusters = ClustersOf(uv);
            clusters.Sort((x, y) => y.Count.CompareTo(x.Count));

            Texture2D? atlas = AtlasFor(renderer, atlases);
            Bounds box = mesh.bounds;
            float low = box.min.y, span = Mathf.Max(0.0001f, box.size.y);

            report.AppendLine($"-- {prefabName}  ({mesh.vertexCount} verts, {mesh.subMeshCount} submeshes, " +
                              $"{clusters.Count} clusters, height {box.size.y:0.00} m, " +
                              $"materials {Materials(renderer)}, " +
                              $"atlas {(atlas == null ? "unreadable" : atlas.width + "x" + atlas.height)})");
            report.AppendLine("    verts   share  uv rect (u0,v0 - u1,v1)                 colour    flat  alpha  height band");

            int listed = 0;
            foreach (Cluster c in clusters)
            {
                if (c.Count < InterestingVertices) continue;
                listed++;

                string colour = "-", flat = "-", alpha = "-";
                if (atlas != null)
                {
                    Flatness f = Measure(atlas, c.Rect);
                    colour = $"#{f.Mean:X6}";
                    flat = f.MaxDeviation.ToString(CultureInfo.InvariantCulture).PadLeft(3);
                    alpha = f.MeanAlpha.ToString(CultureInfo.InvariantCulture).PadLeft(3);
                }

                float lo = 1f, hi = 0f;
                foreach (int v in c.Vertices)
                {
                    if (v >= position.Length) continue;
                    float t = (position[v].y - low) / span;
                    lo = Mathf.Min(lo, t);
                    hi = Mathf.Max(hi, t);
                }

                report.AppendLine(
                    $"    {c.Count,5}  {100f * c.Count / mesh.vertexCount,5:0.0}%  " +
                    $"({c.Rect.xMin:0.0000},{c.Rect.yMin:0.0000} - {c.Rect.xMax:0.0000},{c.Rect.yMax:0.0000})  " +
                    $"{colour,8}  {flat,4}  {alpha,5}  {lo:0.00}-{hi:0.00}");
            }

            int tail = clusters.Count - listed;
            if (tail > 0) report.AppendLine($"    (+{tail} clusters under {InterestingVertices} verts)");
            report.AppendLine();
        }

        static string Materials(MeshRenderer renderer)
        {
            var names = new List<string>();
            foreach (Material? m in renderer.sharedMaterials) names.Add(m == null ? "(null)" : m.name);
            return string.Join("+", names);
        }

        // ---------------------------------------------------------------- clustering

        sealed class Cluster
        {
            public Rect Rect;
            public int Count;
            public readonly List<int> Vertices = new List<int>();
        }

        /// <summary>Group UVs that land on the same flat cell. Lifted from <c>SwatchProbe</c>.</summary>
        static List<Cluster> ClustersOf(Vector2[] uv)
        {
            var clusters = new List<Cluster>();
            for (int i = 0; i < uv.Length; i++)
            {
                Vector2 p = uv[i];
                Cluster? into = null;
                for (int c = 0; c < clusters.Count; c++)
                {
                    Rect r = clusters[c].Rect;
                    if (p.x >= r.xMin - MergeTolerance && p.x <= r.xMax + MergeTolerance &&
                        p.y >= r.yMin - MergeTolerance && p.y <= r.yMax + MergeTolerance)
                    { into = clusters[c]; break; }
                }

                if (into == null)
                {
                    into = new Cluster { Rect = new Rect(p.x, p.y, 0f, 0f) };
                    clusters.Add(into);
                }
                else
                {
                    Rect r = into.Rect;
                    float x0 = Mathf.Min(r.xMin, p.x), y0 = Mathf.Min(r.yMin, p.y);
                    float x1 = Mathf.Max(r.xMax, p.x), y1 = Mathf.Max(r.yMax, p.y);
                    into.Rect = new Rect(x0, y0, x1 - x0, y1 - y0);
                }

                into.Count++;
                into.Vertices.Add(i);
            }
            return clusters;
        }

        // ---------------------------------------------------------------- atlas sampling

        readonly struct Flatness
        {
            public readonly uint Mean;
            public readonly int MaxDeviation;
            public readonly int MeanAlpha;
            public Flatness(uint mean, int maxDeviation, int meanAlpha)
            { Mean = mean; MaxDeviation = maxDeviation; MeanAlpha = meanAlpha; }
        }

        static Flatness Measure(Texture2D atlas, Rect rect)
        {
            float inset = 0.0015f;
            float u0 = rect.xMin + inset, u1 = rect.xMax - inset;
            float v0 = rect.yMin + inset, v1 = rect.yMax - inset;
            if (u1 < u0) { u0 = u1 = rect.center.x; }
            if (v1 < v0) { v0 = v1 = rect.center.y; }

            const int Steps = 4;
            long r = 0, g = 0, b = 0, a = 0;
            var seen = new List<Color32>(Steps * Steps);

            for (int i = 0; i < Steps; i++)
            for (int j = 0; j < Steps; j++)
            {
                float u = Mathf.Lerp(u0, u1, i / (float)(Steps - 1));
                float v = Mathf.Lerp(v0, v1, j / (float)(Steps - 1));
                int x = Mathf.Clamp(Mathf.RoundToInt(u * (atlas.width - 1)), 0, atlas.width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(v * (atlas.height - 1)), 0, atlas.height - 1);
                Color32 c = atlas.GetPixel(x, y);
                seen.Add(c);
                r += c.r; g += c.g; b += c.b; a += c.a;
            }

            int n = seen.Count;
            var mean = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            int worst = 0;
            foreach (Color32 c in seen)
            {
                worst = Mathf.Max(worst, Mathf.Abs(c.r - mean.r));
                worst = Mathf.Max(worst, Mathf.Abs(c.g - mean.g));
                worst = Mathf.Max(worst, Mathf.Abs(c.b - mean.b));
            }

            uint packed = ((uint)mean.r << 16) | ((uint)mean.g << 8) | mean.b;
            return new Flatness(packed, worst, (int)(a / n));
        }

        static Texture2D? AtlasFor(MeshRenderer renderer, Dictionary<Texture, Texture2D?> cache)
        {
            Material? material = renderer.sharedMaterial;
            if (material == null) return null;

            Texture? source = null;
            foreach (string property in new[] { "_Albedo_Map", "_BaseMap", "_MainTex" })
                if (material.HasProperty(property) && material.GetTexture(property) != null)
                { source = material.GetTexture(property); break; }

            if (source == null) return null;
            if (cache.TryGetValue(source, out Texture2D? cached)) return cached;

            Texture2D? copy = null;
            string path = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                copy = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!copy.LoadImage(bytes))
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                    copy = null;
                }
            }

            cache[source] = copy;
            return copy;
        }

        static GameObject? FindPrefab(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/Synty" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name) continue;
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }
    }
}
