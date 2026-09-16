#nullable enable
using System;
using System.Text;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// What a module's material actually exposes, so a tint can be aimed at a property that exists.
    ///
    /// <para><b>Why this had to be measured rather than read.</b> A tint that writes a property the
    /// shader does not declare fails in the worst possible way: silently, with the art drawing in
    /// its own colour and nothing anywhere reporting a problem. Three separate explanations for
    /// yellow grass were reasoned out from the source and all three were wrong, which is the
    /// standing lesson in <c>docs/lessons.md</c> arriving again — a plausible causal story attached
    /// to a real symptom is still a guess.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh exec Odyssey.EditorTools.TintProbe.Run</c>.</para>
    /// </summary>
    public static class TintProbe
    {
        [MenuItem("Odyssey/Presentation/Probe module tints")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        /// <summary>Every colour property a tint might plausibly be aimed at.</summary>
        static readonly string[] Candidates =
        {
            "_BaseColor", "_Color", "_BaseColour", "_Base_Color", "_MainColor", "_Main_Color",
            "_Tint", "_TintColor", "_Albedo", "_Emission_Color", "_EmissionColor",
        };

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            ModuleLibrary? library = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                library = new ModuleLibrary(catalogue);

                Report(library, "grass tuft A", ModuleIds.GrassTuftA, ModuleShape.Pillar);
                Report(library, "grass tuft B", ModuleIds.GrassTuftB, ModuleShape.Pillar);
                Report(library, "grass tuft C", ModuleIds.GrassTuftC, ModuleShape.Pillar);
                Report(library, "grass ground", ModuleIds.Terrain("Grass"), ModuleShape.SolidBlock);
                Report(library, "rock ground", ModuleIds.Terrain("Rock"), ModuleShape.RockBlock);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Tint] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                library?.Dispose();
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static void Report(ModuleLibrary library, string label, string moduleId, ModuleShape shape)
        {
            int index = library.Resolve(moduleId, shape);
            ResolvedModule module = library[index];

            if (module.Parts.Length == 0)
            {
                Debug.Log($"[Tint] {label}: no parts at all");
                return;
            }

            for (int p = 0; p < module.Parts.Length; p++)
            {
                Material material = module.Parts[p].Material;
                if (material == null)
                {
                    Debug.Log($"[Tint] {label} part {p}: no material");
                    continue;
                }

                var found = new StringBuilder();
                foreach (string name in Candidates)
                {
                    if (!material.HasProperty(name)) continue;
                    if (found.Length > 0) found.Append(", ");
                    found.Append(name).Append('=').Append(material.GetColor(name).ToString("F2"));
                }

                Debug.Log($"[Tint] {label} part {p}: material '{material.name}', " +
                          $"shader '{(material.shader != null ? material.shader.name : "none")}', " +
                          $"fallback={module.Parts[p].IsFallback}, " +
                          $"colour properties: {(found.Length > 0 ? found.ToString() : "NONE")}");

                // Guessing property names is what got this wrong three times over, so when none of
                // the expected ones exist, enumerate what the shader really declares rather than
                // reaching for a longer list of guesses.
                if (found.Length > 0 || material.shader == null) continue;

                int count = UnityEditor.ShaderUtil.GetPropertyCount(material.shader);
                var all = new StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    UnityEditor.ShaderUtil.ShaderPropertyType type =
                        UnityEditor.ShaderUtil.GetPropertyType(material.shader, i);
                    if (type != UnityEditor.ShaderUtil.ShaderPropertyType.Color &&
                        type != UnityEditor.ShaderUtil.ShaderPropertyType.Vector) continue;

                    string name = UnityEditor.ShaderUtil.GetPropertyName(material.shader, i);
                    if (all.Length > 0) all.Append(", ");
                    all.Append(name).Append('(').Append(type).Append(")=")
                       .Append(material.GetColor(name).ToString("F2"));
                }

                Debug.Log($"[Tint]   -> every colour or vector property '{material.shader.name}' " +
                          $"declares: {(all.Length > 0 ? all.ToString() : "NONE AT ALL")}");
            }
        }
    }
}
