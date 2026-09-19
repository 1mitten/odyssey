#nullable enable
// Keeps the instanced variants of the *pack's own* shaders in a player build.
//
// **The fourth cause of the empty player, measured 2026-09-19.** After the runtime-found shaders,
// the content pack and `InstancingKeepAlive`, terrain, trees, rock and colonists all drew and the
// grass tufts, bushes and item piles did not. The owner: "None of the items like meals, wood,
// stone are visible" and "no grass either".
//
// Those are drawn from Synty prefabs, and `ModuleLibrary` takes the prefab's `sharedMaterial` as
// it is. The player said what that means:
//
//   def0 mat='Generic_01_A'  shader='Synty/Generic_Standard' instancing=False
//                            kw=[_ALPHATEST_ON _EMISSION _NORMALMAP]
//   def2 mat='PolygonWesternFrontier_01_A' shader='Synty/Generic_Basic' instancing=False
//                            kw=[_ALPHATEST_ON]
//
// The shaders are the pack's Shader Graph ones, not URP/Lit, so `ShaderInclusion` never mentions
// them and `Shader.Find` never asks for them — they ship because prefabs reference them. But every
// material asset that references them has instancing **off**, so built-in stripping keeps no
// instanced variant, and `Graphics.RenderMeshInstanced` draws them into nothing. Exactly the fault
// `InstancingKeepAlive` fixes for our own shaders, in the one corner it could not see.
//
// **Why this is staged at build time rather than committed.** A keep-alive material for a Synty
// shader is an asset that references licensed content by GUID. Committing one would put pack
// content in the repository and would dangle on any clone without the pack, which the brief
// forbids and CLAUDE.md repeats. So this creates them before a build and deletes them after, the
// same bargain `ContentPackBuild` makes with the Defs — and on a machine with no pack it finds no
// materials, creates nothing, and the build is unaffected.
//
// **It keeps one material per distinct (shader, keyword set), not one per material.** 385 material
// assets in the pack collapse to a handful of combinations, and a variant is a combination.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Odyssey.EditorTools
{
    public class SyntyInstancingKeepAlive : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>Where the pack's materials live. Licensed; never written to, only read.</summary>
        public const string PackRoot = "Assets/Synty";

        /// <summary>Transient, gitignored, removed after every build.</summary>
        public const string Staged = "Assets/Resources/OdysseySyntyKeepAlive";

        // Runs after ContentPackBuild rather than beside it; order only has to be deterministic.
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report) => Stage();

        public void OnPostprocessBuild(BuildReport report) => Unstage();

        /// <summary>
        /// Create one instancing-enabled material per distinct (shader, keyword set) used by the
        /// pack, so built-in stripping keeps those shaders' instanced variants.
        /// </summary>
        public static int Stage()
        {
            Unstage();

            if (!Directory.Exists(PackRoot))
            {
                // A clone without the pack. Nothing to keep alive, and nothing to say about it:
                // everything that would have used these shaders falls back to a primitive anyway.
                Debug.Log("[SyntyInstancingKeepAlive] no pack at " + PackRoot + "; nothing staged.");
                return 0;
            }

            Directory.CreateDirectory(Staged);

            var seen = new HashSet<string>();
            int made = 0;

            foreach (Material source in CatalogueMaterials())
            {
                if (source == null || source.shader == null) continue;

                // The combination is the variant. Sorted, so two materials that enable the same
                // keywords in a different order are one entry rather than two.
                string[] keywords = source.shaderKeywords.OrderBy(k => k).ToArray();
                string key = source.shader.name + "|" + string.Join(",", keywords);
                if (!seen.Add(key)) continue;

                // A blank material on the pack's shader, not a copy of the pack's material: this
                // needs the shader and the keywords, and nothing else the source carries. It keeps
                // textures and colours out of a file we are about to write into the project.
                var keepAlive = new Material(source.shader)
                {
                    name = "KeepAlive_" + made,
                    enableInstancing = true,
                };
                foreach (string keyword in keywords) keepAlive.EnableKeyword(keyword);

                AssetDatabase.CreateAsset(keepAlive, $"{Staged}/KeepAlive_{made}.mat");
                made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SyntyInstancingKeepAlive] staged {made} keep-alive material(s) "
                      + $"for {seen.Count} distinct shader/keyword combination(s).");
            return made;
        }

        /// <summary>
        /// Every material the module catalogue can actually put on screen — the prefabs' shared
        /// materials and the rows that name a material directly.
        ///
        /// <para><b>Derived from the catalogue rather than from the pack folder, and that is the
        /// whole economy of this file.</b> Scanning all 385 materials in the pack yields 126
        /// distinct shader/keyword combinations, and keeping all of them cost 440 MB and 6m42s
        /// against 386 MB and 12s. The catalogue is the single place where an id becomes a mesh,
        /// so the materials reachable from it are exactly the ones drawn instanced — and it is the
        /// same list <c>ModuleLibrary</c> reads at runtime, not a second one maintained by
        /// hand.</para>
        /// </summary>
        static IEnumerable<Material> CatalogueMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ModuleCatalogue"))
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<Presentation.Rendering.ModuleCatalogue>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (catalogue == null) continue;

                foreach (var entry in catalogue.Entries)
                {
                    if (entry == null) continue;
                    if (entry.material != null) yield return entry.material;
                    if (entry.prefab == null) continue;

                    foreach (var renderer in entry.prefab.GetComponentsInChildren<Renderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            if (material != null) yield return material;
                }
            }
        }

        /// <summary>Take them out again, so nothing licensed is left sitting in the project.</summary>
        public static void Unstage()
        {
            string staged = Path.GetFullPath(Staged);
            if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);

            string meta = staged + ".meta";
            if (File.Exists(meta)) File.Delete(meta);

            AssetDatabase.Refresh();
        }

        [MenuItem("Odyssey/Build/Stage Synty instancing keep-alives (debug)")]
        static void StageFromMenu() => Stage();
    }
}
