#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Work out which parts of a colonist's atlas are skin, hair and clothing, and write the
    /// answer into the module catalogue as numbers.
    ///
    /// <para><b>Why this can be done at all.</b> Every garment, hair patch and skin region on a
    /// Synty body is UV-mapped onto one small flat cell of the pack atlas, so a vertex's cell
    /// already <i>is</i> its material identity. Nothing has to be authored: no mask texture, no
    /// vertex-colour channel, no Blender pass over sixty-one bodies. <c>SwatchProbe</c> measured
    /// the claim before any of this was built — every cluster on eight bodies from all four packs
    /// reports zero colour deviation inside its rectangle.</para>
    ///
    /// <para><b>Why it runs in the editor.</b> Three of the four character FBXs are imported
    /// without read/write, so <c>Mesh.uv</c> is unavailable at runtime for forty-three of the
    /// sixty-one bodies. In the editor every mesh is readable whatever the importer says, so the
    /// classification happens once, here, and what ships is a handful of rectangles per row. That
    /// is also why no import setting is touched: those live in gitignored <c>Assets/Synty</c> and
    /// would not reach CI or another clone.</para>
    ///
    /// <para><b>It writes only appearance fields.</b> It does not rebuild the catalogue, and
    /// deliberately so — a rebuild in a worktree without the packs silently rewrites every prefab
    /// reference to <c>{fileID: 0}</c> and exits zero, which <c>docs/lessons.md</c> records
    /// happening once already. This loads the committed asset, fills in the appearance of the
    /// colonist rows and saves. Every other field is left exactly as it was found.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh exec Odyssey.EditorTools.CharacterSwatches.Classify</c>
    /// to write, or <c>...CharacterSwatches.Report</c> to print the table and change nothing.</para>
    /// </summary>
    public static class CharacterSwatches
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>See <c>SwatchProbe.MergeTolerance</c>: well under one cell's width.</summary>
        const float MergeTolerance = 0.004f;

        /// <summary>
        /// Where every pack paints skin.
        ///
        /// <b>Measured, not guessed.</b> `SwatchProbe` put the skin clusters of bodies from all
        /// four packs inside u 0.008-0.029, v 0.183-0.190 — Synty use the same corner of the
        /// layout in each pack, which is why one rectangle serves all four. It is padded to
        /// u 0.004-0.040, v 0.174-0.200, which still excludes every neighbouring cluster the probe
        /// found: the hair browns sit at u 0.049-0.052 and the pale head cell at v 0.166.
        ///
        /// A cluster inside this band is only accepted as skin if it also carries head or arm
        /// vertices, so a garment that happens to be painted from a skin swatch is not mistaken
        /// for a face.
        /// </summary>
        static readonly Rect SkinColumn = Rect.MinMaxRect(0.004f, 0.174f, 0.040f, 0.200f);

        /// <summary>A cluster this head-dominant is something worn on or growing from the head.</summary>
        const float HairHeadShare = 0.80f;

        /// <summary>Below this a cluster is detail — an eye, a button, a badge — not a slot.</summary>
        const int MinSlotVertices = 12;

        /// <summary>
        /// Eyes are small, head-weighted and nearly black, and would otherwise be taken for hair
        /// on a body whose real hair is smaller than its eyes. Measured: the eye cluster is 40 to
        /// 54 vertices and #000000 on every body the probe looked at.
        /// </summary>
        const int MaxEyeVertices = 60;

        [MenuItem("Odyssey/Presentation/Classify character swatches")]
        public static void ClassifyFromMenu() => Execute(write: true);

        [MenuItem("Odyssey/Presentation/Report character swatches")]
        public static void ReportFromMenu() => Execute(write: false);

        public static void Classify() => Execute(write: true);

        public static void Report() => Execute(write: false);

        static void Execute(bool write)
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                Debug.LogError($"[CharacterSwatches] no catalogue at {CataloguePath}");
                return;
            }

            List<ModuleEntry> rows = catalogue.FindFamily(ModuleIds.ColonistBase);
            if (rows.Count == 0)
            {
                Debug.LogError("[CharacterSwatches] the catalogue has no colonist rows");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine($"=== Character swatches ({(write ? "writing" : "report only")}) ===");

            var tally = new Dictionary<AppearanceQuality, int>();
            int unresolved = 0;

            foreach (ModuleEntry row in rows)
            {
                if (row.prefab == null)
                {
                    unresolved++;
                    continue;
                }

                AppearanceCells cells = Classify(row.prefab, out string note);
                if (write) row.appearance = cells;

                tally.TryGetValue(cells.quality, out int n);
                tally[cells.quality] = n + 1;

                report.AppendLine(
                    $"  {row.prefabName,-34} {cells.quality,-10} " +
                    $"skin {Slot(cells.skin, cells.skinVerts, cells.totalVerts)}  " +
                    $"hair {Slot(cells.hair, cells.hairVerts, cells.totalVerts)}  " +
                    $"cloth {Slot(cells.cloth, cells.clothVerts, cells.totalVerts)}  " +
                    $"cloth2 {Slot(cells.cloth2, cells.cloth2Verts, cells.totalVerts)}  {note}");
            }

            report.AppendLine();
            foreach (KeyValuePair<AppearanceQuality, int> pair in tally)
                report.AppendLine($"  {pair.Key}: {pair.Value}");
            if (unresolved > 0)
                report.AppendLine($"  rows with no prefab (packs absent?): {unresolved}");

            // A run that classified nothing must not quietly overwrite good data with empties.
            // This is the same failure the catalogue rebuild has, arriving by a different door.
            if (write && unresolved == rows.Count)
            {
                Debug.LogError("[CharacterSwatches] not one colonist prefab resolved — " +
                               "the packs are absent. Nothing written.\n" + report);
                return;
            }

            if (write)
            {
                EditorUtility.SetDirty(catalogue);
                AssetDatabase.SaveAssets();
                report.AppendLine($"  written to {CataloguePath}");
            }

            Debug.Log(report.ToString());
        }

        static string Slot(Rect[] rects, int verts, int total) =>
            rects.Length == 0 ? "  -  " : $"{rects.Length}x{100 * verts / Mathf.Max(1, total),3}%";

        // ---------------------------------------------------------------- the classifier

        public static AppearanceCells Classify(GameObject prefab, out string note)
        {
            note = string.Empty;
            var cells = new AppearanceCells();

            // Every body in the pack sits on one skeleton inside each prefab with all but one
            // deactivated, so the body wanted is the one left active. Matching on the name does
            // not work: PolygonGeneric names the child after the prefab, the other three rename
            // SM_Chr_Cop_01 to Character_Cop_01.
            // The *largest* active mesh, not the first. PolygonGeneric ships hair, hoods and
            // beards as their own skinned children which are also left active, and taking the
            // first active renderer picked the hair cap on three bodies — they came back with a
            // hair slot covering the whole body and no clothing at all. A body outweighs anything
            // worn on its head by an order of magnitude, so size is the reliable discriminator.
            SkinnedMeshRenderer? skin = null;
            int mostVertices = 0;
            foreach (SkinnedMeshRenderer candidate in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!candidate.gameObject.activeSelf || candidate.sharedMesh == null) continue;
                if (candidate.sharedMesh.vertexCount <= mostVertices) continue;
                mostVertices = candidate.sharedMesh.vertexCount;
                skin = candidate;
            }

            if (skin == null || skin.sharedMesh == null)
            {
                note = "no active skinned mesh";
                return cells;
            }

            Mesh mesh = skin.sharedMesh;
            Vector2[] uv = mesh.uv;
            if (uv.Length == 0)
            {
                note = "mesh has no UVs";
                return cells;
            }

            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = skin.bones;
            cells.totalVerts = mesh.vertexCount;

            List<Cluster> clusters = ClustersOf(uv, weights, bones);
            clusters.Sort((a, b) => b.Count.CompareTo(a.Count));

            var used = new HashSet<Cluster>();

            // --- skin: inside the measured column, and actually on a head or a hand.
            var skinClusters = new List<Cluster>();
            foreach (Cluster c in clusters)
            {
                if (skinClusters.Count == 2) break;
                if (c.Count < MinSlotVertices) continue;
                if (!SkinColumn.Contains(c.Rect.center)) continue;
                if (c.Head == 0 && c.Arm == 0) continue;
                skinClusters.Add(c);
            }
            foreach (Cluster c in skinClusters) used.Add(c);

            // --- hair: the head is wearing it, and it is not skin and not an eye.
            var hairClusters = new List<Cluster>();
            foreach (Cluster c in clusters)
            {
                if (hairClusters.Count == 2) break;
                if (used.Contains(c) || c.Count < MinSlotVertices) continue;
                if (c.Head < HairHeadShare * c.Count) continue;
                if (c.Count <= MaxEyeVertices && c.NearBlack) continue;
                hairClusters.Add(c);
            }
            foreach (Cluster c in hairClusters) used.Add(c);

            // --- clothing: the biggest of what is left that is worn on the body.
            var clothClusters = new List<Cluster>();
            foreach (Cluster c in clusters)
            {
                if (clothClusters.Count == 2) break;
                if (used.Contains(c) || c.Count < MinSlotVertices) continue;
                if (c.Torso + c.Leg + c.Arm < c.Count / 2) continue;
                clothClusters.Add(c);
            }

            cells.skin = Rects(skinClusters);
            cells.hair = Rects(hairClusters);
            cells.skinVerts = Verts(skinClusters);
            cells.hairVerts = Verts(hairClusters);

            if (clothClusters.Count > 0)
            {
                cells.cloth = Rects(clothClusters.GetRange(0, 1));
                cells.clothVerts = clothClusters[0].Count;
            }
            if (clothClusters.Count > 1)
            {
                cells.cloth2 = Rects(clothClusters.GetRange(1, 1));
                cells.cloth2Verts = clothClusters[1].Count;
            }

            cells.quality = QualityOf(cells, ref note);

            // The shader lays the four slots over each other in a fixed order and does not test
            // for overlap, so disjointness is guaranteed here or not at all.
            if (Overlaps(cells))
            {
                note = string.IsNullOrEmpty(note) ? "overlapping slots" : note + "; overlapping slots";
                cells.quality = AppearanceQuality.Shared;
            }

            return cells;
        }

        static AppearanceQuality QualityOf(AppearanceCells cells, ref string note)
        {
            bool hasSkin = cells.skin.Length > 0;
            bool hasHair = cells.hair.Length > 0;
            bool hasCloth = cells.cloth.Length > 0;

            if (!hasCloth)
            {
                note = "no clothing region";
                return AppearanceQuality.None;
            }
            if (hasSkin && hasHair) return AppearanceQuality.Full;
            if (hasSkin) { note = "bald, hooded or helmeted"; return AppearanceQuality.NoHair; }
            if (hasHair) { note = "no skin showing"; return AppearanceQuality.NoSkin; }
            note = "clothing only";
            return AppearanceQuality.ClothOnly;
        }

        static bool Overlaps(AppearanceCells cells)
        {
            var all = new List<Rect>();
            all.AddRange(cells.skin);
            all.AddRange(cells.hair);
            all.AddRange(cells.cloth);
            all.AddRange(cells.cloth2);

            for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                if (all[i].Overlaps(all[j])) return true;
            return false;
        }

        static Rect[] Rects(List<Cluster> clusters)
        {
            var rects = new Rect[clusters.Count];
            for (int i = 0; i < clusters.Count; i++) rects[i] = Padded(clusters[i].Rect);
            return rects;
        }

        static int Verts(List<Cluster> clusters)
        {
            int n = 0;
            foreach (Cluster c in clusters) n += c.Count;
            return n;
        }

        /// <summary>
        /// A hair's breadth wider than the vertices themselves.
        ///
        /// A cluster's bounding box is the extent of its UVs, and a fragment interpolated between
        /// two of them can land a fraction outside it — which would leave a thin unpainted seam
        /// along the edge of every recoloured region. The padding is far smaller than the gap
        /// between cells the probe measured, so it cannot reach a neighbour.
        /// </summary>
        static Rect Padded(Rect r)
        {
            const float pad = 0.0012f;
            return Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);
        }

        // ---------------------------------------------------------------- clustering

        sealed class Cluster
        {
            public Rect Rect;
            public int Count;
            public int Head, Arm, Leg, Torso;
            public bool NearBlack;
            public readonly List<int> Vertices = new List<int>();
        }

        static List<Cluster> ClustersOf(Vector2[] uv, BoneWeight[] weights, Transform[] bones)
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
                Weigh(into, i, weights, bones);
            }

            // Eyes are told from hair by being nearly black, which is a fact about the atlas and
            // so cannot be read from the mesh. The UV corner they sit in is stable across every
            // pack the probe looked at (u under 0.02, v under 0.02), which is enough.
            foreach (Cluster c in clusters)
                c.NearBlack = c.Rect.center.x < 0.02f && c.Rect.center.y < 0.02f;

            return clusters;
        }

        static void Weigh(Cluster cluster, int vertex, BoneWeight[] weights, Transform[] bones)
        {
            if (vertex >= weights.Length) return;
            int b = weights[vertex].boneIndex0;
            if ((uint)b >= (uint)bones.Length || bones[b] == null) return;

            string n = bones[b].name;
            if (n.StartsWith("Head", StringComparison.Ordinal) || n.StartsWith("Neck", StringComparison.Ordinal) ||
                n.StartsWith("Jaw", StringComparison.Ordinal) || n.StartsWith("Eye", StringComparison.Ordinal))
                cluster.Head++;
            else if (n.StartsWith("Hand", StringComparison.Ordinal) || n.StartsWith("Elbow", StringComparison.Ordinal) ||
                     n.StartsWith("Finger", StringComparison.Ordinal) || n.StartsWith("Thumb", StringComparison.Ordinal) ||
                     n.StartsWith("Index", StringComparison.Ordinal))
                cluster.Arm++;
            else if (n.StartsWith("UpperLeg", StringComparison.Ordinal) || n.StartsWith("LowerLeg", StringComparison.Ordinal) ||
                     n.StartsWith("Ankle", StringComparison.Ordinal) || n.StartsWith("Ball", StringComparison.Ordinal) ||
                     n.StartsWith("Toes", StringComparison.Ordinal))
                cluster.Leg++;
            else cluster.Torso++;
        }
    }
}
