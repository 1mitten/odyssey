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
    /// Measure how a Synty character body uses the pack atlas, before any recolouring is built.
    ///
    /// <para>The recolouring mechanism rests on one claim: every garment, hair patch and skin
    /// region on a body is UV-mapped onto a <b>flat</b> cell of the atlas's <c>Character Colours</c>
    /// block, so a shader can replace the colour inside a rectangle and lose nothing. If a region
    /// instead carries a gradient, a pattern or baked shading, replacement throws art away and the
    /// mechanism is the wrong one. That is not a thing to reason about — it is a thing to
    /// measure, and this measures it.</para>
    ///
    /// <para>It also reports the <b>clusters</b> themselves, because the classifier plans to
    /// discover the rectangles from the mesh rather than from a hand-measured atlas grid. A body
    /// whose UVs form a few dozen tight clusters is one the classifier can work on; a body whose
    /// UVs smear is not. The bone mix per cluster is printed beside it, since that is what tells
    /// hair from a shirt.</para>
    ///
    /// <para><b>This writes nothing.</b> No catalogue is rebuilt, no asset is touched, no pixel
    /// is copied anywhere — the numbers below are measurements of licensed art, which is the same
    /// standing the mesh bounds and haft ratios already recorded in <c>PlayScene</c> have. The
    /// atlas is read by loading the PNG's bytes into a throwaway texture, so no import setting is
    /// changed either.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh exec Odyssey.EditorTools.SwatchProbe.Run</c>.
    /// No graphics device needed.</para>
    /// </summary>
    public static class SwatchProbe
    {
        /// <summary>
        /// How close two UVs must be to count as the same swatch cell.
        ///
        /// A cell in the Sci-Fi atlas is roughly 30 px of 2048, i.e. about 0.0146 in UV. Half of
        /// that would merge neighbours, so the tolerance is deliberately well under it: a cluster
        /// that should have been one thing splitting into two is harmless here (the report shows
        /// both), where two cells merging into one would hide exactly what is being looked for.
        /// </summary>
        const float MergeTolerance = 0.004f;

        /// <summary>Clusters smaller than this are listed in the tail, not the table.</summary>
        const int InterestingVertices = 8;

        /// <summary>One body per pack, plus a couple that are expected to be awkward.</summary>
        static readonly string[] Bodies =
        {
            "SM_Gen_Chr_Street_Male_01",
            "SM_Gen_Chr_Business_Female_01",
            "SM_Chr_CyberPunk_Male_01",
            "SM_Chr_Cop_01",
            "SM_Chr_Robot_01",
            "SM_Chr_Bandit_Male_01",
            "SM_Chr_GoldMiner_Male_01",
            "SM_Chr_Farmer_Male_01",
        };

        [MenuItem("Odyssey/Presentation/Probe the character swatches")]
        public static void RunFromMenu() => Execute();

        public static void Run() => Execute();

        /// <summary>
        /// Does <c>Odyssey/Character</c> compile, and with what?
        ///
        /// A shader that fails to compile renders magenta and is noticed; one that compiles with
        /// warnings, or silently drops a pass, is not. Run headless with
        /// <c>scripts/unity.sh exec Odyssey.EditorTools.SwatchProbe.CheckShader</c>.
        /// </summary>
        public static void CheckShader()
        {
            Shader? shader = Shader.Find("Odyssey/Character");
            if (shader == null)
            {
                Debug.LogError("[SwatchProbe] Odyssey/Character not found");
                return;
            }

            var messages = UnityEditor.ShaderUtil.GetShaderMessages(shader);
            var report = new StringBuilder();
            report.AppendLine($"=== Odyssey/Character ===");
            report.AppendLine($"passes {shader.passCount}, render queue {shader.renderQueue}, " +
                              $"errors {UnityEditor.ShaderUtil.ShaderHasError(shader)}");

            // shader.passCount reports the *active* subshader, which in batch mode is not
            // necessarily the one authored. The shader data is the structure as written.
            UnityEditor.ShaderData data = UnityEditor.ShaderUtil.GetShaderData(shader);
            report.AppendLine($"subshaders {data.SubshaderCount}");
            for (int i = 0; i < data.SubshaderCount; i++)
            {
                UnityEditor.ShaderData.Subshader sub = data.GetSubshader(i);
                report.AppendLine($"  subshader {i}: {sub.PassCount} passes");
                for (int j = 0; j < sub.PassCount; j++)
                {
                    UnityEditor.ShaderData.Pass pass = sub.GetPass(j);
                    report.AppendLine($"    pass {j}: name '{pass.Name}'");
                }
            }
            foreach (var m in messages)
                report.AppendLine($"  [{m.severity}] {m.message} {m.messageDetails} ({m.file}:{m.line})");
            if (messages.Length == 0) report.AppendLine("  no messages");

            if (UnityEditor.ShaderUtil.ShaderHasError(shader)) Debug.LogError(report.ToString());
            else Debug.Log(report.ToString());
        }

        static void Execute()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Character swatch probe ===");
            report.AppendLine($"merge tolerance {MergeTolerance:0.0000} UV");
            report.AppendLine();

            var atlases = new Dictionary<Texture, Texture2D?>();
            try
            {
                foreach (string name in Bodies) Probe(name, report, atlases);
            }
            finally
            {
                foreach (Texture2D? copy in atlases.Values)
                    if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            }

            Debug.Log(report.ToString());
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

            // The prefab holds every body in its pack on one skeleton with all but one switched
            // off, so the body wanted is simply the one left active. Matching on the name does
            // not work across packs: PolygonGeneric names the child after the prefab, while the
            // other three rename SM_Chr_Cop_01 to Character_Cop_01, and five of eight bodies came
            // back "not found" on the first run because of it.
            SkinnedMeshRenderer? skin = null;
            foreach (SkinnedMeshRenderer candidate in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (candidate.gameObject.activeSelf && candidate.sharedMesh != null)
                { skin = candidate; break; }

            if (skin == null || skin.sharedMesh == null)
            {
                report.AppendLine($"-- {prefabName}: no matching skinned mesh");
                report.AppendLine();
                return;
            }

            Mesh mesh = skin.sharedMesh;
            Vector2[] uv = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = skin.bones;

            var clusters = ClustersOf(uv);
            clusters.Sort((x, y) => y.Count.CompareTo(x.Count));

            Texture2D? atlas = AtlasFor(skin, atlases);

            report.AppendLine($"-- {prefabName}  ({mesh.vertexCount} verts, {clusters.Count} clusters, " +
                              $"atlas {(atlas == null ? "unreadable" : atlas.width + "x" + atlas.height)})");
            report.AppendLine("    verts   share  uv rect (u0,v0 - u1,v1)                 colour    flatness  bones");

            int listed = 0;
            foreach (Cluster c in clusters)
            {
                if (c.Count < InterestingVertices) continue;
                listed++;

                string bone = BoneMix(c, uv, weights, bones);
                string colour = "-", flat = "-";
                if (atlas != null)
                {
                    Flatness f = Measure(atlas, c.Rect);
                    colour = $"#{f.Mean:X6}";
                    // Max deviation of any sampled texel from the cluster's mean, 0-255 per
                    // channel. This is the number the mechanism turns on.
                    flat = f.MaxDeviation.ToString(CultureInfo.InvariantCulture).PadLeft(3);
                }

                report.AppendLine(
                    $"    {c.Count,5}  {100f * c.Count / mesh.vertexCount,5:0.0}%  " +
                    $"({c.Rect.xMin:0.0000},{c.Rect.yMin:0.0000} - {c.Rect.xMax:0.0000},{c.Rect.yMax:0.0000})  " +
                    $"{colour,8}  {flat,8}  {bone}");
            }

            int tail = clusters.Count - listed;
            if (tail > 0) report.AppendLine($"    (+{tail} clusters under {InterestingVertices} verts)");
            report.AppendLine();
        }

        // ---------------------------------------------------------------- clustering

        sealed class Cluster
        {
            public Rect Rect;
            public int Count;
            public readonly List<int> Vertices = new List<int>();
        }

        /// <summary>
        /// Group UVs that land on the same flat cell.
        ///
        /// Grid-free on purpose. Assuming a regular grid would produce plausible-looking nonsense
        /// wherever the atlas block is irregular, and the bounding box of an actual cluster is
        /// correct by construction and automatically tight.
        /// </summary>
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

        static string BoneMix(Cluster c, Vector2[] uv, BoneWeight[] weights, Transform[] bones)
        {
            if (weights.Length == 0 || bones.Length == 0) return "(no weights)";

            int head = 0, arm = 0, leg = 0, body = 0;
            foreach (int v in c.Vertices)
            {
                if (v >= weights.Length) continue;
                int b = weights[v].boneIndex0;
                if ((uint)b >= (uint)bones.Length || bones[b] == null) continue;
                string n = bones[b].name;

                if (n.StartsWith("Head", StringComparison.Ordinal) || n.StartsWith("Neck", StringComparison.Ordinal) ||
                    n.StartsWith("Jaw", StringComparison.Ordinal) || n.StartsWith("Eye", StringComparison.Ordinal))
                    head++;
                else if (n.StartsWith("Hand", StringComparison.Ordinal) || n.StartsWith("Elbow", StringComparison.Ordinal) ||
                         n.StartsWith("Finger", StringComparison.Ordinal) || n.StartsWith("Thumb", StringComparison.Ordinal) ||
                         n.StartsWith("Index", StringComparison.Ordinal))
                    arm++;
                else if (n.StartsWith("UpperLeg", StringComparison.Ordinal) || n.StartsWith("LowerLeg", StringComparison.Ordinal) ||
                         n.StartsWith("Ankle", StringComparison.Ordinal) || n.StartsWith("Ball", StringComparison.Ordinal) ||
                         n.StartsWith("Toes", StringComparison.Ordinal))
                    leg++;
                else body++;
            }

            var parts = new List<string>(4);
            if (head > 0) parts.Add($"head {Pct(head, c.Count)}");
            if (arm > 0) parts.Add($"arm {Pct(arm, c.Count)}");
            if (leg > 0) parts.Add($"leg {Pct(leg, c.Count)}");
            if (body > 0) parts.Add($"torso {Pct(body, c.Count)}");
            return string.Join(", ", parts);
        }

        static string Pct(int n, int of) => (100 * n / Mathf.Max(1, of)) + "%";

        // ---------------------------------------------------------------- atlas sampling

        readonly struct Flatness
        {
            public readonly uint Mean;
            public readonly int MaxDeviation;
            public Flatness(uint mean, int maxDeviation) { Mean = mean; MaxDeviation = maxDeviation; }
        }

        /// <summary>
        /// Mean colour inside the rect, and how far the furthest texel in it strays from that mean.
        ///
        /// The rect is inset before sampling: a cluster's bounding box is the extent of the UVs
        /// themselves, which sit at the cell's edge as often as not, and a texel picked up from
        /// the neighbouring cell would report a flat cell as wildly uneven.
        /// </summary>
        static Flatness Measure(Texture2D atlas, Rect rect)
        {
            float inset = 0.0015f;
            float u0 = rect.xMin + inset, u1 = rect.xMax - inset;
            float v0 = rect.yMin + inset, v1 = rect.yMax - inset;
            if (u1 < u0) { u0 = u1 = rect.center.x; }
            if (v1 < v0) { v0 = v1 = rect.center.y; }

            const int Steps = 4;
            long r = 0, g = 0, b = 0;
            var seen = new List<Color32>(Steps * Steps);

            for (int i = 0; i < Steps; i++)
            for (int j = 0; j < Steps; j++)
            {
                float u = Steps == 1 ? u0 : Mathf.Lerp(u0, u1, i / (float)(Steps - 1));
                float v = Steps == 1 ? v0 : Mathf.Lerp(v0, v1, j / (float)(Steps - 1));
                int x = Mathf.Clamp(Mathf.RoundToInt(u * (atlas.width - 1)), 0, atlas.width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(v * (atlas.height - 1)), 0, atlas.height - 1);
                Color32 c = atlas.GetPixel(x, y);
                seen.Add(c);
                r += c.r; g += c.g; b += c.b;
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
            return new Flatness(packed, worst);
        }

        /// <summary>
        /// A readable copy of the body's albedo atlas.
        ///
        /// Loaded from the PNG's own bytes rather than from the imported texture, because the
        /// packs import their atlases without read/write and <b>no import setting under
        /// <c>Assets/Synty</c> is changed by this project</b> — that state lives in a gitignored
        /// folder and would not reach CI or another clone. Cached per source texture, and every
        /// copy is destroyed when the probe finishes.
        /// </summary>
        static Texture2D? AtlasFor(SkinnedMeshRenderer skin, Dictionary<Texture, Texture2D?> cache)
        {
            Material? material = skin.sharedMaterial;
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
