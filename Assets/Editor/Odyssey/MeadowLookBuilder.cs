#nullable enable
using System;
using System.IO;
using System.Linq;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Writes <c>Assets/Odyssey/Presentation/Resources/OdysseyLook/MeadowLook.asset</c>: references,
    /// by GUID, to the Meadow Forest terrain textures and the demo scene's URP volume profile
    /// (<c>docs/design/38-meadow-overhaul.md</c> §17), and to its cloud rings and their material
    /// (design 63), whose sizes it logs.
    ///
    /// <para>Generated rather than hand-authored, like the module catalogue, and for the same
    /// reason: the asset points into the gitignored <c>Assets/Synty</c>, and a rebuild on a machine
    /// without the packs would write nulls over every reference. So it refuses to write when the
    /// packs are absent. Run it from the menu, or headless with
    /// <c>scripts/unity.sh exec Odyssey.EditorTools.MeadowLookBuilder.Build</c>.</para>
    /// </summary>
    public static class MeadowLookBuilder
    {
        const string AssetPath = "Assets/Odyssey/Presentation/Resources/OdysseyLook/MeadowLook.asset";

        [MenuItem("Odyssey/Presentation/Rebuild meadow look")]
        public static void Build()
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/Synty/PolygonNatureBiomes")))
            {
                Debug.LogWarning("[MeadowLook] no Meadow Forest pack on this machine; the asset is left as it is.");
                return;
            }

            var look = AssetDatabase.LoadAssetAtPath<MeadowLook>(AssetPath);
            bool created = look == null;
            if (look == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(AssetPath))!);
                look = ScriptableObject.CreateInstance<MeadowLook>();
            }

            // The pack's own terrain-layer textures. Grass_Texture_01 is the one the stock ground
            // wore; the rest are what a painted Unity terrain in the demo blends over it.
            look.grassA = Texture("Grass_Texture_01");
            look.grassB = Texture("Grass_Texture_02");
            look.clover = Texture("Grass_Clovers_Texture_01");
            look.flowers = Texture("Grass_Flowers_Texture_01");
            look.leaves = Texture("Grass_Leaves_Texture_01");
            look.earth = Texture("Dirt_Texture_01");
            // The marsh round water, blended in by the ground field (design 38 §24).
            look.wet = Texture("Moss_Texture_01");
            look.grade = Asset<VolumeProfile>("Global Volume Profile", "Assets/Synty/PolygonNatureBiomes");
            // The Meadow demo's sky (design 63): its two rings and the material both wear there.
            look.cloudRing = MeshOf("Env_CloudRing_Larger_01_Smooth_03", "Assets/Synty/PolygonNatureBiomes");
            look.cloudRingHigh = MeshOf("SM_Env_Cloud_Ring_01", "Assets/Synty/PNB_Core");
            look.clouds = Asset<Material>("Synty_Clouds_Meadows", "Assets/Synty/PolygonNatureBiomes");

            if (created) AssetDatabase.CreateAsset(look, AssetPath);
            else EditorUtility.SetDirty(look);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MeadowLook] wrote {AssetPath}: ground {(look.HasGround ? "complete" : "INCOMPLETE")}, " +
                      $"grade {(look.grade != null ? look.grade.name : "missing")}, " +
                      $"clouds {(look.HasClouds ? "complete" : "INCOMPLETE")}");
            Describe("cloudRing", look.cloudRing);
            Describe("cloudRingHigh", look.cloudRingHigh);
        }

        /// <summary>A ring's size in its own units and what it costs to draw, for design 63's arithmetic.</summary>
        static void Describe(string field, Mesh? mesh)
        {
            if (mesh == null)
            {
                Debug.Log($"[MeadowLook] {field}: missing");
                return;
            }
            Bounds b = mesh.bounds;
            // The inside of the ring: the nearest any vertex comes to its middle, across the ground.
            float inner = float.MaxValue;
            foreach (Vector3 v in mesh.vertices)
                inner = Mathf.Min(inner, new Vector2(v.x - b.center.x, v.z - b.center.z).magnitude);
            Debug.Log($"[MeadowLook] {field}: {mesh.name} centre {b.center.ToString("F3")} size {b.size.ToString("F3")} " +
                      $"inner radius {inner:F1} (outer {Mathf.Max(b.extents.x, b.extents.z):F1}) " +
                      $"vertices {mesh.vertexCount} triangles {mesh.triangles.Length / 3} submeshes {mesh.subMeshCount}");
        }

        static Texture2D? Texture(string exactName) => Asset<Texture2D>(exactName, "Assets/Synty/PolygonNatureBiomes");

        /// <summary>The first mesh inside a model file, found by the file's exact name.</summary>
        static Mesh? MeshOf(string modelName, string folder)
        {
            GameObject? model = Asset<GameObject>(modelName, folder);
            if (model == null) return null;
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<Mesh>().FirstOrDefault();
        }

        static T? Asset<T>(string exactName, string folder) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"\"{exactName}\" t:{typeof(T).Name}", new[] { folder });
            string? path = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), exactName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
