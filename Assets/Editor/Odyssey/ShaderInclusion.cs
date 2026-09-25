#nullable enable
// Keeps every shader this game finds at runtime inside the player build.
//
// Editor menu:  Odyssey > Build > Fix always-included shaders
// Headless:     scripts/unity.sh exec Odyssey.EditorTools.ShaderInclusion.Apply
// Automatic:    PlayerBuild refuses to build without it. Nothing calls Apply for you -- the
//               claim that PlayScene.Build did was wrong from the day it was written (2026-09-19).
//
// **The fault this exists to prevent, measured 2026-09-19 on the first player build this project
// ever ran.** The owner's report was "there is no terrain — no graphics, terrain etc, apart from
// characters", and it happened only in a build, never in the editor.
//
// Everything in the world is drawn with `Graphics.RenderMeshInstanced`, using materials created
// *at runtime* from `Shader.Find(...)` with instancing on — ChunkRenderer, MaterialCache,
// ModuleLibrary, TreeMaterials, ColonistMaterials. A runtime-created material is not an asset, so
// the build's shader collector never sees it, and Unity ships only what assets reference:
//
//   * `Odyssey/Water`, `Odyssey/Tree`, `Odyssey/Character` and `Odyssey/Outline` were absent from
//     the player altogether. Grepping Odyssey_Data for their names returned nothing, and the
//     player's own log said so: "shader Odyssey/Outline not found; outlines are off."
//   * `Universal Render Pipeline/Lit` was present — Synty's prefab materials reference it — but
//     only in the variants those materials use, none of which is instanced. So the one variant
//     every chunk, floor, wall and item in the game is drawn with was stripped.
//
// Characters were the single visible thing because they alone are GameObjects wearing real
// material assets. They also drew in the pack's own colours, because the shader that recolours
// them had gone with the rest.
//
// **The editor cannot catch this**, which is why it survived so long: the editor has every shader
// and every variant, always. Both test tiers run in the editor's own domain. The only thing that
// can fail on it is a player build, and until 2026-09-19 nothing had ever made one.
//
// Always-included is the blunt instrument on purpose: it forces the shader *and all its variants*
// into the build, which is exactly what a runtime-created material needs and what a variant
// collection cannot promise for a keyword nothing has declared. The cost is build size and shader
// compile time, and it is measured in docs/lessons.md rather than assumed.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    public static class ShaderInclusion
    {
        /// <summary>
        /// Every shader the game asks for by name at runtime.
        ///
        /// <para><b>Adding a <c>Shader.Find</c> anywhere means adding a row here</b>, or the thing
        /// it draws is invisible in a player and perfect in the editor. <c>ShaderInclusionTests</c>
        /// reads the source for <c>Shader.Find</c> calls and fails on a name this list does not
        /// have, so the pair cannot drift silently.</para>
        /// </summary>
        public static readonly string[] Required =
        {
            // Ours. Absent from the player entirely before this existed.
            "Odyssey/Character",
            "Odyssey/Tree",
            "Odyssey/Water",
            "Odyssey/Outline",

            // The power lines (design 32 §9), drawn over everything. Found by name like the rest.
            "Odyssey/PowerLine",

            // The selection highlight (design 44): its mask and its composite, both found by
            // name from the renderer feature. Without them a player falls back to the brackets.
            "Odyssey/SelectionMask",
            "Odyssey/SelectionComposite",

            // Meadow foliage (design 38 §4). Without it a player strips the shader and the cache
            // falls back to the pack's own, so the meadow draws but its wind runs on wall time.
            "Odyssey/Foliage",

            // The painted meadow floor (design 38 §17). Without it a player strips the shader and
            // the grass terrain falls back to the pack's single tiled texture.
            "Odyssey/MeadowGround",

            // Rain (the rain-look prototype): streaks and splashes drawn procedurally.
            "Odyssey/Rain",

            // The sky, and it was not on the first draft of this list — `ShaderInclusionTests`
            // found it on the run that was meant to confirm the fix, along with `Standard` below.
            // Worth recording rather than quietly adding: a list of runtime-found shaders
            // maintained by reading the code is a list that will be short by one, and that is the
            // whole reason the test derives it from the source instead of trusting this array.
            "Odyssey/GradientSky",

            // Unity's, and the expensive one. Present in the build already, but only in the
            // variants Synty's material assets happen to use — and every instanced draw in the
            // game needs a variant none of them does.
            "Universal Render Pipeline/Lit",

            // The fallbacks the same call sites reach for when the above are missing. A fallback
            // that is itself stripped is not a fallback.
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",

            // The last-resort fallback when even URP/Lit cannot be found. Listed for completeness
            // rather than need — Unity's own defaults already carry it — but a fallback whose
            // presence is somebody else's decision is not a fallback anybody should rely on.
            "Standard",
        };

        [MenuItem("Odyssey/Build/Fix always-included shaders")]
        public static void Apply()
        {
            if (Ensure(out string[] added))
            {
                Debug.Log("[ShaderInclusion] added to always-included shaders: "
                          + string.Join(", ", added));
            }
            else
            {
                Debug.Log("[ShaderInclusion] every required shader was already included.");
            }
        }

        /// <summary>
        /// Add any missing required shader to the graphics settings' always-included list.
        /// Returns whether anything changed, and what was added.
        ///
        /// <para>Idempotent, and it writes nothing when the list is already right — a no-op save
        /// of <c>GraphicsSettings.asset</c> is a spurious diff on every scene rebuild, and that
        /// file is already noisy enough after a build (see <c>docs/lessons.md</c>).</para>
        /// </summary>
        public static bool Ensure(out string[] added)
        {
            SerializedObject settings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");

            var present = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null) present.Add(shader.name);
            }

            var wanted = new List<string>();
            foreach (string name in Required)
            {
                if (present.Contains(name)) continue;

                Shader? shader = Shader.Find(name);
                if (shader == null)
                {
                    // Not a failure: a fallback shader a render pipeline does not ship is simply
                    // not there to include, and the call site already handles finding nothing.
                    // Saying so is worth more than silence, because a *required* shader going
                    // missing looks identical from here.
                    Debug.LogWarning($"[ShaderInclusion] no shader named '{name}' in this project.");
                    continue;
                }

                int at = list.arraySize;
                list.InsertArrayElementAtIndex(at);
                list.GetArrayElementAtIndex(at).objectReferenceValue = shader;
                wanted.Add(name);
            }

            added = wanted.ToArray();
            if (added.Length == 0) return false;

            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>
        /// Which required shaders are missing from the list, for a test or a build to refuse on.
        /// </summary>
        public static string[] Missing()
        {
            var present = new HashSet<string>(
                GraphicsSettings.GetGraphicsSettings() == null
                    ? System.Array.Empty<string>()
                    : AlwaysIncluded().Select(shader => shader.name));

            return Required
                .Where(name => !present.Contains(name) && Shader.Find(name) != null)
                .ToArray();
        }

        static IEnumerable<Shader> AlwaysIncluded()
        {
            var settings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Shader shader)
                    yield return shader;
        }
    }
}
