#nullable enable
// Keeps the INSTANCING_ON variants of the runtime-found shaders inside a player build.
//
// Editor menu:  Odyssey > Build > Fix instancing keep-alive materials
// Headless:     scripts/unity.sh exec Odyssey.EditorTools.InstancingKeepAlive.Apply
// Automatic:    PlayerBuild refuses to build without it. Nothing calls Apply for you.
//
// **The third cause of the empty player, measured 2026-09-19.** `ShaderInclusion` fixed a real
// fault and `ContentPackBuild` fixed a second one, and after both the world still did not draw.
// The player was doing all the work: 1731 draw calls, 43921 instances, 104 chunks, 22 materials
// and 18192 surround instances every frame, with `Universal Render Pipeline/Lit` found, supported
// and carrying its five passes. None of it reached the screen.
//
// Always-included keeps the *shader*. It does not keep every *variant* — Unity's built-in variant
// stripping still runs, and it drops `INSTANCING_ON` (from `#pragma multi_compile_instancing`)
// unless some **material asset** in the build has instancing switched on. Every instanced material
// in this game is built at runtime from `Shader.Find`, so there was no such asset, and
// `Graphics.RenderMeshInstanced` drew into a variant that did not exist. It does not warn.
//
// Colonists were visible throughout because they are the one thing not drawn instanced. That is
// the same exception that made the first cause diagnosable, and it pointed at the wrong half twice.
//
// **Why a material asset rather than a ShaderVariantCollection.** A collection has to name the
// keyword combination, and the combination a runtime material ends up in is the pipeline's
// business, not ours — a collection that names one is a guess that goes stale when a quality
// setting moves. An instancing-enabled material asset states the one thing we actually know: that
// this shader is drawn instanced. Built-in stripping then keeps the instancing axis of whatever
// variants survive the rest of the pass, which is exactly the right amount.
//
// **Measured, because the received wisdom is that this is expensive.** On URP/Lit's ForwardLit
// pass the keep-alive doubles the kept variants and nothing more: 64 -> 128 fragment and 16 -> 32
// vertex, which is the instancing axis and only that. Build 386 MB either way, 16 s to 28 s.
// Compare the alternative somebody reached for before this was understood -- switching URP's
// `m_StripUnusedVariants` off, which takes the same pass to **884,736** variants and the build to
// an estimated day and a half. Both numbers are in docs/journal.md.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    public static class InstancingKeepAlive
    {
        /// <summary>
        /// Where the keep-alive materials live. Under <c>Resources</c> deliberately: the point is
        /// that they ship whether or not anything references them, and a folder somebody can open
        /// and read says that better than a hidden reference on a scene object would.
        /// </summary>
        public const string Folder = "Assets/Resources/OdysseyKeepAlive";

        /// <summary>
        /// One per shader in <see cref="ShaderInclusion.Required"/>, and deliberately not a second
        /// hand-maintained list of "the instanced ones". This project's standing lesson is that a
        /// list maintained by reading the code is a list that is short by one — that is why
        /// <c>ShaderInclusionTests</c> derives its list from the source — and a keep-alive for a
        /// shader that is never drawn instanced costs one material asset and no variants worth
        /// counting. One list, no drift.
        /// </summary>
        public static IEnumerable<string> Required => ShaderInclusion.Required;

        static string PathFor(string shaderName) =>
            Folder + "/" + shaderName.Replace('/', '_') + ".mat";

        /// <summary>
        /// Keyword variants the renderer turns on at runtime, on materials it clones in code, which
        /// stripping can therefore never see used: each gets a keep-alive of its own with the keyword
        /// on. The scenery drawn from GPU buffers (design 38 §22) is <c>Odyssey/Foliage</c> under
        /// <c>ODYSSEY_INDIRECT</c>; without its own keep-alive the player keeps only the base variant,
        /// which reads no buffer, and every indirect clump draws at the origin.
        /// </summary>
        public static readonly (string Shader, string Keyword)[] KeywordVariants =
        {
            ("Odyssey/Foliage", "ODYSSEY_INDIRECT"),
        };

        static string PathFor(string shaderName, string keyword) =>
            Folder + "/" + shaderName.Replace('/', '_') + "_" + keyword + ".mat";

        [MenuItem("Odyssey/Build/Fix instancing keep-alive materials")]
        public static void Apply()
        {
            if (Ensure(out string[] created))
                Debug.Log("[InstancingKeepAlive] created: " + string.Join(", ", created));
            else
                Debug.Log("[InstancingKeepAlive] every required shader already has one.");
        }

        /// <summary>
        /// Create any missing keep-alive material. Returns whether anything changed, and what.
        ///
        /// <para>Idempotent, and it writes nothing when the folder is already right, for the same
        /// reason <see cref="ShaderInclusion.Ensure"/> does not: a no-op save is a spurious diff
        /// on every scene rebuild.</para>
        /// </summary>
        public static bool Ensure(out string[] created)
        {
            Directory.CreateDirectory(Folder);

            var made = new List<string>();
            foreach (string name in Required)
            {
                string path = PathFor(name);
                if (File.Exists(path)) continue;

                Shader? shader = Shader.Find(name);
                if (shader == null)
                {
                    // Not a failure, for the same reason as in ShaderInclusion: a fallback the
                    // pipeline does not ship is simply not there to keep alive.
                    Debug.LogWarning($"[InstancingKeepAlive] no shader named '{name}'.");
                    continue;
                }

                AssetDatabase.CreateAsset(new Material(shader) { enableInstancing = true }, path);
                made.Add(name);
            }

            foreach ((string name, string keyword) in KeywordVariants)
            {
                string path = PathFor(name, keyword);
                if (File.Exists(path)) continue;
                Shader? shader = Shader.Find(name);
                if (shader == null) continue;
                var material = new Material(shader) { enableInstancing = true };
                material.EnableKeyword(keyword);
                AssetDatabase.CreateAsset(material, path);
                made.Add(name + " + " + keyword);
            }

            created = made.ToArray();
            if (created.Length == 0) return false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        /// <summary>
        /// Which required shaders have no keep-alive material, for a test or a build to refuse on.
        /// A shader that is not in this project at all is not missing — it is absent, and the call
        /// sites already handle finding nothing.
        /// </summary>
        public static string[] Missing() =>
            Required.Where(name => Shader.Find(name) != null && !File.Exists(PathFor(name)))
                    .Concat(KeywordVariants
                        .Where(v => Shader.Find(v.Shader) != null && !File.Exists(PathFor(v.Shader, v.Keyword)))
                        .Select(v => v.Shader + " + " + v.Keyword))
                    .ToArray();

        /// <summary>
        /// Whether a keep-alive material has had its instancing switched off since it was made —
        /// which turns it back into an ordinary material asset and silently stops it doing the one
        /// job it exists for. A file being present is not the same as it being right.
        /// </summary>
        public static string[] NotInstanced()
        {
            var wrong = new List<string>();
            foreach (string name in Required)
            {
                string path = PathFor(name);
                if (!File.Exists(path)) continue;

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && !material.enableInstancing) wrong.Add(name);
            }
            return wrong.ToArray();
        }
    }
}
