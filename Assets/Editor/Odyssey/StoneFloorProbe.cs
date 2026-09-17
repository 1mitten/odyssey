#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// What colour does a stone floor actually come out, and why.
    ///
    /// <para>The owner has now said a stone floor looks like steel twice, once either side of a
    /// tint change that should have fixed it. Reading the tint table says it is fixed, which is
    /// exactly the kind of answer <c>docs/lessons.md</c> says to stop trusting: the tint is a
    /// multiply over whatever the art already is, so the number in the table is at most half of
    /// the colour that reaches the screen.</para>
    ///
    /// <para>So this measures the other half. For each slab module it takes the mesh's own UVs,
    /// samples the material's texture at every one of them, and averages — which is the colour the
    /// art contributes, weighted the way the mesh actually uses the atlas. Multiply that by the
    /// stuff tint and you have the pixel. The texture is copied through a RenderTexture rather than
    /// read directly, because the licensed packs are imported without Read/Write and changing that
    /// to answer a question would be editing the art to measure it.</para>
    ///
    /// <para><c>scripts/unity.sh exec Odyssey.EditorTools.StoneFloorProbe.Run</c></para>
    /// </summary>
    public static class StoneFloorProbe
    {
        public static void Run()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            if (catalogue == null) { Debug.LogError("[Stone] no catalogue"); return; }

            var library = new ModuleLibrary(catalogue);

            Report(library, ModuleIds.SlabOf("stone"), NaturalContent.StuffStone, "stone");
            Report(library, ModuleIds.SlabOf("wood"), NaturalContent.StuffWood, "wood");
            Report(library, ModuleIds.Slab, CoreContent.StuffConcrete, "template slab / concrete");

            // What the same tints would do over a neutral white surface, for contrast: this is the
            // colour the table *intends*, and the gap between it and the line above is the art.
            Debug.Log($"[Stone] tints alone — stone {Show(StuffPalette.StuffTint(NaturalContent.StuffStone))} " +
                      $"steel {Show(StuffPalette.StuffTint(CoreContent.StuffSteel))} " +
                      $"wood {Show(StuffPalette.StuffTint(NaturalContent.StuffWood))}");
        }

        static void Report(ModuleLibrary library, string moduleId, ushort stuff, string label)
        {
            int index = library.Resolve(moduleId, ModuleShape.FloorSlab);
            ResolvedModule module = library[index];
            Color tint = StuffPalette.For(stuff, overArt: module.UsesArt);

            Debug.Log($"[Stone] {label}: id={moduleId} usesArt={module.UsesArt} parts={module.Parts.Length}");

            for (int i = 0; i < module.Parts.Length; i++)
            {
                ModulePart part = module.Parts[i];
                Material? mat = part.Material;
                Texture? tex = FindTexture(mat, out string where);
                Color baseColour = mat != null && mat.HasProperty("_BaseColor")
                    ? mat.GetColor("_BaseColor") : Color.white;

                Color art = AverageOverUvs(part.Mesh, part.Submesh, tex as Texture2D);
                Color lit = new Color(art.r * baseColour.r * tint.r,
                                      art.g * baseColour.g * tint.g,
                                      art.b * baseColour.b * tint.b);

                Debug.Log($"[Stone]   part {i}: mat={(mat == null ? "none" : mat.name)} " +
                          $"shader={(mat == null ? "-" : mat.shader.name)} " +
                          $"tex={(tex == null ? "none" : tex.name)}@{where} " +
                          $"art={Show(art)} baseColour={Show(baseColour)} tint={Show(tint)} " +
                          $"=> pixel {Show(lit)} {Cast(lit)}");
                Debug.Log($"[Stone]     surface: {Surface(mat)}");
            }
        }

        /// <summary>Warm, neutral or cool, which is the whole question here.</summary>
        static string Cast(Color c)
        {
            float d = c.r - c.b;
            return d > 0.02f ? "WARM" : d < -0.02f ? "COOL (reads as metal)" : "neutral";
        }


        /// <summary>
        /// The albedo, wherever this shader keeps it. Synty packs arrive on whatever pipeline they
        /// were authored for, and a URP upgrade moves the slot — so asking one property name and
        /// believing the answer is how the first run of this probe reported "no texture" for a
        /// material that plainly has one.
        /// </summary>
        static Texture? FindTexture(Material? mat, out string where)
        {
            where = "-";
            if (mat == null) return null;

            foreach (string name in new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_AlbedoMap" })
                if (mat.HasProperty(name) && mat.GetTexture(name) is Texture found)
                {
                    where = name;
                    return found;
                }

            // Nothing named: walk every texture slot the shader declares and take the first.
            Shader shader = mat.shader;
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                string name = shader.GetPropertyName(i);
                if (mat.GetTexture(name) is not Texture found) continue;
                where = name + " (scanned)";
                return found;
            }

            return null;
        }


        /// <summary>
        /// Every scalar and colour the shader declares, which is where "looks like metal" lives.
        /// Hue is only half of a material: a neutral grey that is smooth and metallic reads as
        /// brushed steel, and a neutral grey that is rough reads as stone. The tint table cannot
        /// reach either of those.
        /// </summary>
        static string Surface(Material? mat)
        {
            if (mat == null) return "none";
            Shader shader = mat.shader;
            var parts = new List<string>();
            int count = shader.GetPropertyCount();

            for (int i = 0; i < count; i++)
            {
                var kind = shader.GetPropertyType(i);
                string name = shader.GetPropertyName(i);
                if (kind == UnityEngine.Rendering.ShaderPropertyType.Float ||
                    kind == UnityEngine.Rendering.ShaderPropertyType.Range)
                    parts.Add($"{name}={mat.GetFloat(name):0.###}");
                else if (kind == UnityEngine.Rendering.ShaderPropertyType.Color)
                    parts.Add($"{name}={Show(mat.GetColor(name))}");
            }

            return parts.Count == 0 ? "no scalar properties" : string.Join("  ", parts);
        }

        static string Show(Color c) => $"({c.r:0.000},{c.g:0.000},{c.b:0.000})";

        /// <summary>
        /// The texture averaged over the UVs this submesh's triangles actually use, area-weighted,
        /// so a swatch the mesh barely touches does not count as much as the face you stand on.
        /// </summary>
        static Color AverageOverUvs(Mesh? mesh, int submesh, Texture2D? texture)
        {
            if (mesh == null || texture == null) return Color.white;

            Texture2D readable = Readable(texture);
            Vector2[] uvs = mesh.uv;
            if (uvs.Length == 0) { Object.DestroyImmediate(readable); return Color.white; }

            int[] tris = mesh.GetTriangles(Mathf.Clamp(submesh, 0, mesh.subMeshCount - 1));
            Vector3[] verts = mesh.vertices;
            double r = 0, g = 0, b = 0, weight = 0;

            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = tris[t], c = tris[t + 1], d = tris[t + 2];
                if (a >= uvs.Length || c >= uvs.Length || d >= uvs.Length) continue;

                float area = Vector3.Cross(verts[c] - verts[a], verts[d] - verts[a]).magnitude * 0.5f;
                if (area <= 0f) continue;

                Vector2 centre = (uvs[a] + uvs[c] + uvs[d]) / 3f;
                Color texel = readable.GetPixelBilinear(centre.x, centre.y);
                r += texel.r * area; g += texel.g * area; b += texel.b * area; weight += area;
            }

            Object.DestroyImmediate(readable);
            return weight > 0 ? new Color((float)(r / weight), (float)(g / weight), (float)(b / weight)) : Color.white;
        }

        /// <summary>A readable copy, without touching the import settings of licensed art.</summary>
        static Texture2D Readable(Texture2D source)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }
    }
}
