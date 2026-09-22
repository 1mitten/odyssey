#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Measure the POLYGON Battle Royale characters against the cast already in the catalogue,
    /// before a line of the modular colonist system is written
    /// (<c>docs/design/29-modular-colonists.md</c> §5).
    ///
    /// <para>Five questions, all of which have been guessed at in this project before and all of
    /// which are cheaper to measure than to argue about:</para>
    ///
    /// <list type="number">
    /// <item>Do the pack's prefabs survive import at all? They are authored in the pre-2018.3
    /// prefab format (<c>!u!1001 Prefab</c>, <c>m_ParentPrefab</c>), which the editor upgrades on
    /// import. If that upgrade fails the renderers come back with null meshes — which looks
    /// exactly like the packs being absent, the failure <c>docs/lessons.md</c> records twice.</item>
    /// <item>How tall is a Battle Royale body against a body the cast already uses? The catalogue
    /// applies <c>scale 1.4</c> to every colonist, and a pack that stands 10% taller would put a
    /// new colonist's head through a doorway the old ones clear.</item>
    /// <item>Where are the hair and beard attachments authored — in head-bone space, or in
    /// character-root space? That decides whether the attachment system parents them to
    /// <c>HumanBodyBones.Head</c> with an identity transform or has to measure an offset.</item>
    /// <item>What do the two <c>Default_Hair</c> prefabs contain? Whether "bald" is one of them or
    /// the absence of any hair prop changes what the lottery deals.</item>
    /// <item>Does the swatch classifier find skin, hair and cloth in the pack's own atlases? Its
    /// skin columns were measured against four other packs and are hard-coded; a body wrongly
    /// classed as covered loses a slot silently.</item>
    /// </list>
    ///
    /// <para><b>This writes nothing but a report.</b> No catalogue is rebuilt, no asset is
    /// touched, no import setting is changed, and nothing is copied out of <c>Assets/Synty</c> —
    /// the numbers are measurements of licensed art, the same standing the mesh bounds already
    /// recorded in <c>PlayScene</c> have.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh exec Odyssey.EditorTools.BattleRoyaleProbe.Run</c>.
    /// No graphics device needed.</para>
    /// </summary>
    public static class BattleRoyaleProbe
    {
        const string PackRoot = "Assets/Synty/PolygonBattleRoyale";
        const string CharacterPrefabs = PackRoot + "/Prefabs/Characters";
        const string ReportPath = "Logs/battle-royale-probe.txt";

        /// <summary>A body already in the cast, for the height comparison to mean something.</summary>
        static readonly string[] Baseline =
        {
            "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Street_Male_02.prefab",
            "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Street_Female_01.prefab",
        };

        [MenuItem("Odyssey/Presentation/Probe Battle Royale")]
        public static void RunFromMenu() => Report(false);

        public static void Run() => Report(Application.isBatchMode);

        static void Report(bool exitWhenDone)
        {
            var r = new StringBuilder();
            r.AppendLine("=== POLYGON Battle Royale probe ===");
            r.AppendLine();

            if (!Directory.Exists(PackRoot))
            {
                Debug.LogError($"[BRProbe] the pack is not at {PackRoot}. " +
                               "Assets/Synty is gitignored; this machine may not have it.");
                if (exitWhenDone) EditorApplication.Exit(1);
                return;
            }

            GameObject? host = LoadFirstCharacter(r);
            if (host != null)
            {
                Bodies(host, r);
                Sockets(host, r);
            }

            Attachments(r);
            Baselines(r);
            Swatches(r);
            AttachmentUvs(r);
            GenericAttachments(r);
            AttachmentUvs(r, "6b",
                "Assets/Synty/PolygonGeneric/Prefabs/Characters/Attachments");

            string text = r.ToString();
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, text);
            Debug.Log(text + "\nwritten to " + ReportPath);
            if (exitWhenDone) EditorApplication.Exit(0);
        }

        // ---------------------------------------------------------------- question 1

        static GameObject? LoadFirstCharacter(StringBuilder r)
        {
            string[] paths = Directory
                .GetFiles(CharacterPrefabs, "Character_*.prefab", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();

            r.AppendLine($"-- 1. import: {paths.Length} character prefabs on disk");
            if (paths.Length == 0) return null;

            GameObject? first = null;
            int loaded = 0, withMeshes = 0;
            foreach (string path in paths)
            {
                string asset = path.Replace('\\', '/');
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(asset);
                if (go == null)
                {
                    r.AppendLine($"   FAILED TO LOAD  {Path.GetFileName(asset)}");
                    continue;
                }

                loaded++;
                first ??= go;

                SkinnedMeshRenderer[] skins = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                int meshes = skins.Count(s => s.sharedMesh != null);
                if (meshes > 0) withMeshes++;
                else r.AppendLine($"   NO MESHES       {Path.GetFileName(asset)}");
            }

            r.AppendLine($"   loaded {loaded}/{paths.Length}, with resolved meshes {withMeshes}");

            if (first != null)
            {
                var animator = first.GetComponent<Animator>();
                r.AppendLine($"   animator on root: {(animator != null ? "yes" : "NO")}" +
                             (animator != null
                                 ? $", humanoid {animator.isHuman}, avatar " +
                                   (animator.avatar != null ? animator.avatar.name : "(none)")
                                 : ""));
                if (animator != null && animator.isHuman)
                {
                    // Every bone the figure director and the tool fitting actually ask for.
                    HumanBodyBones[] needed =
                    {
                        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                        HumanBodyBones.Neck, HumanBodyBones.Head,
                        HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
                        HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbDistal,
                        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexDistal,
                        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleDistal,
                        HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
                        HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
                    };
                    string[] missing = needed
                        .Where(b => animator.GetBoneTransform(b) == null)
                        .Select(b => b.ToString())
                        .ToArray();
                    r.AppendLine(missing.Length == 0
                        ? "   every bone the figure director asks for resolves"
                        : "   MISSING BONES: " + string.Join(", ", missing));
                }
            }

            r.AppendLine();
            return first;
        }

        // ---------------------------------------------------------------- question 2

        static void Bodies(GameObject host, StringBuilder r)
        {
            r.AppendLine("-- 2. bodies on the shared rig (one prefab carries them all)");
            r.AppendLine("   name                                   verts     height  y0     y1");

            foreach (SkinnedMeshRenderer skin in host.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                         .OrderBy(s => s.name, StringComparer.Ordinal))
            {
                Mesh? mesh = skin.sharedMesh;
                if (mesh == null)
                {
                    r.AppendLine($"   {skin.name,-38} (no mesh)");
                    continue;
                }

                Bounds b = mesh.bounds;
                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-38} {1,6}   {2,6:F3}  {3,6:F3} {4,6:F3}",
                    skin.name, mesh.vertexCount, b.size.y, b.min.y, b.max.y));
            }

            r.AppendLine();
        }

        // ---------------------------------------------------------------- question 3 sockets

        static void Sockets(GameObject host, StringBuilder r)
        {
            r.AppendLine("-- 3a. where the head is, in character-root space");
            var animator = host.GetComponent<Animator>();
            Transform? head = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            if (head == null)
            {
                r.AppendLine("   no head bone; cannot place an attachment");
            }
            else
            {
                Vector3 local = host.transform.InverseTransformPoint(head.position);
                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   head bone '{0}' at root-space y {1:F3} (x {2:F3}, z {3:F3})",
                    head.name, local.y, local.x, local.z));
            }

            r.AppendLine();
        }

        // ---------------------------------------------------------------- questions 3, 4

        static void Attachments(StringBuilder r)
        {
            string dir = CharacterPrefabs + "/Attachments";
            r.AppendLine("-- 3b/4. hair and beard attachments");
            if (!Directory.Exists(dir))
            {
                r.AppendLine("   no Attachments folder");
                r.AppendLine();
                return;
            }

            string[] paths = Directory.GetFiles(dir, "*.prefab", SearchOption.TopDirectoryOnly)
                .Select(p => p.Replace('\\', '/'))
                .Where(p =>
                {
                    string n = Path.GetFileNameWithoutExtension(p);
                    return n.Contains("Hair") || n.Contains("Beard");
                })
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();

            r.AppendLine($"   {paths.Length} hair/beard prefabs");
            r.AppendLine("   name                                   verts  skinned  centre y   size y  material");

            foreach (string path in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    r.AppendLine($"   FAILED TO LOAD  {Path.GetFileName(path)}");
                    continue;
                }

                var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var filters = go.GetComponentsInChildren<MeshFilter>(true);
                Mesh? mesh = skinned.Length > 0
                    ? skinned[0].sharedMesh
                    : filters.Length > 0 ? filters[0].sharedMesh : null;

                Renderer? renderer = go.GetComponentInChildren<Renderer>(true);
                string material = renderer != null && renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.name
                    : "(none)";

                if (mesh == null)
                {
                    r.AppendLine($"   {go.name,-38} (no mesh)");
                    continue;
                }

                Bounds b = mesh.bounds;
                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-38} {1,5}  {2,-7}  {3,7:F3}  {4,7:F3}  {5}",
                    go.name, mesh.vertexCount, skinned.Length > 0 ? "yes" : "no",
                    b.center.y, b.size.y, material));
            }

            r.AppendLine();
            r.AppendLine("   (centre y near 0 means head-bone space; near 1.6-1.8 means root space)");
            r.AppendLine();
        }

        // ---------------------------------------------------------------- question 2 baseline

        static void Baselines(StringBuilder r)
        {
            r.AppendLine("-- 2b. the cast already in use, for comparison");
            foreach (string path in Baseline)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    r.AppendLine($"   {Path.GetFileName(path),-40} (absent)");
                    continue;
                }

                var tallest = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(s => s.sharedMesh != null)
                    .OrderByDescending(s => s.sharedMesh.bounds.size.y)
                    .FirstOrDefault();

                if (tallest == null)
                {
                    r.AppendLine($"   {Path.GetFileName(path),-40} (no meshes)");
                    continue;
                }

                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-40} tallest body {1:F3} ({2})",
                    Path.GetFileNameWithoutExtension(path),
                    tallest.sharedMesh.bounds.size.y, tallest.name));
            }

            r.AppendLine();
        }

        // ---------------------------------------------------------------- question 5

        /// <summary>
        /// Does the swatch classifier find skin, hair and cloth in the pack's own atlas?
        ///
        /// Its skin columns were measured against four other packs and are hard-coded, so a body
        /// wrongly classed as covered loses a slot silently — which is exactly the failure the
        /// contact sheet was built for on the last pack.
        /// </summary>
        static void Swatches(StringBuilder r)
        {
            r.AppendLine("-- 5. swatch classification of the Battle Royale bodies");
            r.AppendLine("   prefab                                 quality     skin hair cloth cl2  note");

            foreach (string path in Directory
                         .GetFiles(CharacterPrefabs, "Character_*.prefab", SearchOption.TopDirectoryOnly)
                         .Select(p => p.Replace('\\', '/'))
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                AppearanceCells cells = CharacterSwatches.Classify(go, out string note);
                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-38} {1,-11} {2,4} {3,4} {4,5} {5,4}  {6}",
                    Path.GetFileNameWithoutExtension(path), cells.quality,
                    cells.skin?.Length ?? 0, cells.hair?.Length ?? 0,
                    cells.cloth?.Length ?? 0, cells.cloth2?.Length ?? 0, note));
            }

            r.AppendLine();
        }

        // ---------------------------------------------------------------- question 5b

        /// <summary>
        /// Where a hair or beard mesh sits in the atlas.
        ///
        /// The whole recolouring mechanism is "a vertex's UV cell is its material identity". If a
        /// beard's UVs land inside the same cell the body's hair does, then repainting the hair
        /// rectangle recolours the beard too and the two can never disagree — which is the
        /// property the design wants and would otherwise have to enforce with a second mechanism.
        /// </summary>
        static void AttachmentUvs(StringBuilder r) =>
            AttachmentUvs(r, "5b", CharacterPrefabs + "/Attachments");

        static void AttachmentUvs(StringBuilder r, string section, string dir)
        {
            r.AppendLine($"-- {section}. where hair and beard meshes sit in the atlas ({dir})");
            r.AppendLine("   name                                   verts  u0      u1      v0      v1      extent");

            if (!Directory.Exists(dir))
            {
                r.AppendLine("   no Attachments folder");
                r.AppendLine();
                return;
            }

            foreach (string path in Directory.GetFiles(dir, "*.prefab", SearchOption.TopDirectoryOnly)
                         .Select(p => p.Replace('\\', '/'))
                         .Where(p =>
                         {
                             string n = Path.GetFileNameWithoutExtension(p);
                             // Bun, Ponytail, Chops and Moustache contain neither word, and the
                             // first pass silently left all four unmeasured. Named here rather
                             // than widened to every attachment, so the list stays the list of
                             // things that can be dealt as hair.
                             return n.Contains("Hair") || n.Contains("Beard") ||
                                    n.Contains("Bun") || n.Contains("Ponytail") ||
                                    n.Contains("Chops") || n.Contains("Moustache");
                         })
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var filter = go.GetComponentInChildren<MeshFilter>(true);
                var skinned = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Mesh? mesh = filter != null ? filter.sharedMesh : skinned != null ? skinned.sharedMesh : null;
                if (mesh == null) continue;

                Vector2[] uv = mesh.uv;
                if (uv.Length == 0)
                {
                    r.AppendLine($"   {go.name,-38} (no UVs)");
                    continue;
                }

                float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
                foreach (Vector2 t in uv)
                {
                    if (t.x < u0) u0 = t.x;
                    if (t.x > u1) u1 = t.x;
                    if (t.y < v0) v0 = t.y;
                    if (t.y > v1) v1 = t.y;
                }

                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-38} {1,5}  {2:F4}  {3:F4}  {4:F4}  {5:F4}  {6:F4}",
                    go.name, mesh.vertexCount, u0, u1, v0, v1, Mathf.Max(u1 - u0, v1 - v0)));
            }

            r.AppendLine();
            r.AppendLine("   (an extent under ~0.012 means the whole mesh is one flat swatch cell)");
            r.AppendLine();
        }

        // ---------------------------------------------------------------- question 6

        /// <summary>
        /// The other pack in the colonist pool ships attachments too, and a comment in
        /// <c>CharacterSwatches</c> says they are skinned children left active rather than rigid
        /// props. If that is right, the two packs need different attachment mechanisms and the
        /// design has to carry both.
        /// </summary>
        static void GenericAttachments(StringBuilder r)
        {
            const string dir = "Assets/Synty/PolygonGeneric/Prefabs/Characters/Attachments";
            r.AppendLine("-- 6. how PolygonGeneric ships the same thing");

            if (!Directory.Exists(dir))
            {
                r.AppendLine("   no Attachments folder");
                r.AppendLine();
                return;
            }

            r.AppendLine("   name                                   verts  skinned  centre y  material");
            foreach (string path in Directory.GetFiles(dir, "*.prefab", SearchOption.TopDirectoryOnly)
                         .Select(p => p.Replace('\\', '/'))
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    r.AppendLine($"   FAILED TO LOAD  {Path.GetFileName(path)}");
                    continue;
                }

                var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var filter = go.GetComponentInChildren<MeshFilter>(true);
                Mesh? mesh = skinned.Length > 0
                    ? skinned[0].sharedMesh
                    : filter != null ? filter.sharedMesh : null;

                Renderer? renderer = go.GetComponentInChildren<Renderer>(true);
                string material = renderer != null && renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.name
                    : "(none)";

                r.AppendLine(mesh == null
                    ? $"   {go.name,-38} (no mesh)"
                    : string.Format(CultureInfo.InvariantCulture,
                        "   {0,-38} {1,5}  {2,-7}  {3,7:F3}  {4}",
                        go.name, mesh.vertexCount, skinned.Length > 0 ? "yes" : "no",
                        mesh.bounds.center.y, material));
            }

            r.AppendLine();
        }
    }
}
