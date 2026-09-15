#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Puts the project's renderer features onto the URP renderer asset.
    ///
    /// Renderer features are sub-assets of a <c>ScriptableRendererData</c>, which normally means
    /// dragging one in through the inspector — a step that lives nowhere, is done once on one
    /// machine, and is invisible to anybody who clones the repository afterwards. The same rule
    /// that generates scenes and prefab variants from code applies here: this is a command, it is
    /// idempotent, and running it twice changes nothing.
    /// </summary>
    public static class RenderSetup
    {
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("Odyssey/Presentation/Apply render setup")]
        public static void ApplyFromMenu() => Run(exitWhenDone: false);

        public static void Apply() => Run(Application.isBatchMode);

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            try
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (data == null)
                    throw new InvalidOperationException($"no renderer asset at {RendererPath}");

                bool changed = EnsureFeature<OutlineFeature>(data, "Odyssey Outline");

                if (changed)
                {
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                Debug.Log($"[RenderSetup] {RendererPath}: " +
                          $"{data.rendererFeatures.Count(f => f != null)} features " +
                          $"({string.Join(", ", data.rendererFeatures.Where(f => f != null).Select(f => f!.name))})" +
                          $"{(changed ? " — updated" : " — already correct")}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[RenderSetup] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static bool EnsureFeature<T>(ScriptableRendererData data, string name)
            where T : ScriptableRendererFeature
        {
            if (data.rendererFeatures.Any(f => f is T)) return false;

            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            data.rendererFeatures.Add(feature);
            AssetDatabase.AddObjectToAsset(feature, data);
            Revalidate(data);
            return true;
        }

        /// <summary>
        /// Rebuild the renderer's feature map.
        ///
        /// <c>ScriptableRendererData</c> keeps a parallel list of GUIDs beside its features, and a
        /// feature appended without it is serialised but never instantiated — the asset looks
        /// right in the inspector and the effect simply never runs, with nothing logged. The
        /// method that rebuilds it is internal, so it is called by reflection; if a future URP
        /// renames it, the fallback is to re-save the asset and let <c>OnValidate</c> do the work,
        /// and the log above will show whether the feature actually took.
        /// </summary>
        static void Revalidate(ScriptableRendererData data)
        {
            MethodInfo? validate = typeof(ScriptableRendererData).GetMethod(
                "ValidateRendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);

            if (validate != null) validate.Invoke(data, Array.Empty<object>());
            else Debug.LogWarning("[RenderSetup] ValidateRendererFeatures not found; relying on OnValidate.");
        }
    }
}
