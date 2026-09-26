#nullable enable
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Measure the POLYGON Fantasy Rivals Pig Butcher against a body the cast already uses, before
    /// its catalogue row's scale is chosen (<c>docs/design/62-pig-butcher.md</c> §8a): the scale is
    /// measured, not guessed. Sole to crown of the baked mesh in the prefab's own pose, the cleaver's
    /// longest side, and the scale that would stand the butcher at the design's target of about
    /// 1.45 times a colonist's drawn height.
    ///
    /// <para><b>This writes nothing but a report</b> (<c>Logs/butcher-probe.txt</c>): no asset is
    /// touched and nothing is copied out of <c>Assets/Synty</c>.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh exec Odyssey.EditorTools.ButcherProbe.Run</c>.</para>
    /// </summary>
    public static class ButcherProbe
    {
        const string Butcher = "Assets/Synty/PolygonFantasyRivals/Prefabs/Characters/SM_Chr_BR_PigButcher_01.prefab";
        const string Cleaver = "Assets/Synty/PolygonFantasyRivals/Prefabs/Weapons/SM_Wep_PigButcher_01.prefab";
        const string Colonist = "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Street_Male_02.prefab";
        const string ReportPath = "Logs/butcher-probe.txt";

        /// <summary>Every colonist row's scale (<c>PlayScene.PersonRow</c>).</summary>
        const float ColonistScale = 1.4f;

        /// <summary>The design's target: the butcher about half as tall again as a colonist (design 62 §4, §8).</summary>
        const float TargetRatio = 1.45f;

        [MenuItem("Odyssey/Presentation/Probe the butcher")]
        public static void RunFromMenu() => Report(false);

        public static void Run() => Report(Application.isBatchMode);

        static void Report(bool exitWhenDone)
        {
            var r = new StringBuilder();
            r.AppendLine("=== Pig Butcher probe (design 62 §8a) ===");
            float colonist = Height(Colonist, r);
            float butcher = Height(Butcher, r);
            float cleaver = Longest(Cleaver, r);
            if (colonist > 0f && butcher > 0f)
            {
                float drawnColonist = colonist * ColonistScale;
                float scale = drawnColonist * TargetRatio / butcher;
                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "colonist drawn at {0:0.000} m; butcher at the colonist's 1.4 would be {1:0.000} m ({2:0.00}x)",
                    drawnColonist, butcher * ColonistScale, butcher / colonist));
                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "scale for {0:0.00}x a colonist: {1:0.000} -> {2:0.000} m drawn; cleaver drawn {3:0.000} m",
                    TargetRatio, scale, butcher * scale, cleaver * scale));
            }

            // The catalogue rows as the game resolves them, and the rig's hand as the figure finds it.
            var catalogue = AssetDatabase.LoadAssetAtPath<Odyssey.Presentation.Rendering.ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            if (catalogue != null)
            {
                foreach (string id in new[] { Odyssey.Presentation.Rendering.ModuleIds.Hostile(6),
                             Odyssey.Presentation.Rendering.ModuleIds.HostileWeapon(6) })
                {
                    var row = catalogue.Find(id);
                    r.AppendLine($"row {id}: {(row == null ? "MISSING" : row.prefab == null ? "no prefab" : row.prefab.name)}");
                }
            }
            var body = AssetDatabase.LoadAssetAtPath<GameObject>(Butcher);
            if (body != null)
            {
                GameObject go = Object.Instantiate(body);
                var animator = go.GetComponent<Animator>();
                r.AppendLine(animator == null ? "no Animator"
                    : $"animator human {animator.isHuman}, avatar {(animator.avatar != null ? animator.avatar.name + " valid " + animator.avatar.isValid : "none")}, " +
                      $"right hand {(animator.isHuman && animator.GetBoneTransform(HumanBodyBones.RightHand) != null ? animator.GetBoneTransform(HumanBodyBones.RightHand).name : "none")}");
                Object.DestroyImmediate(go);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            File.WriteAllText(ReportPath, r.ToString());
            Debug.Log("[ButcherProbe]\n" + r);
            if (exitWhenDone) EditorApplication.Exit(0);
        }

        /// <summary>Sole to crown of the baked mesh, in the prefab's own pose, at scale 1.</summary>
        static float Height(string path, StringBuilder r)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                r.AppendLine($"MISSING {path}");
                return 0f;
            }
            GameObject go = Object.Instantiate(prefab);
            try
            {
                go.transform.position = Vector3.zero;
                go.transform.localScale = Vector3.one;
                bool any = false;
                var bounds = new Bounds();
                foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: false))
                {
                    if (!skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                    var baked = new Mesh();
                    skin.BakeMesh(baked, useScale: true);
                    foreach (Vector3 v in baked.vertices)
                    {
                        Vector3 w = skin.transform.TransformPoint(v);
                        if (!any) { bounds = new Bounds(w, Vector3.zero); any = true; }
                        else bounds.Encapsulate(w);
                    }
                    Object.DestroyImmediate(baked);
                }
                float h = any ? bounds.size.y : 0f;
                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}: {1:0.000} m tall, {2:0.000} x {3:0.000} m wide, sole at {4:0.000}",
                    Path.GetFileNameWithoutExtension(path), h, bounds.size.x, bounds.size.z, bounds.min.y));
                return h;
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The longest side of a prop's renderers' bounds, at scale 1.</summary>
        static float Longest(string path, StringBuilder r)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                r.AppendLine($"MISSING {path}");
                return 0f;
            }
            GameObject go = Object.Instantiate(prefab);
            try
            {
                bool any = false;
                var bounds = new Bounds();
                foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { bounds = renderer.bounds; any = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                Vector3 s = bounds.size;
                float longest = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                r.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}: {1:0.000} x {2:0.000} x {3:0.000} m, longest {4:0.000}",
                    Path.GetFileNameWithoutExtension(path), s.x, s.y, s.z, longest));
                return longest;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
