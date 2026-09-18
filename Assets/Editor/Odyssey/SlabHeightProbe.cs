#nullable enable
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// <b>Where does each slab art put its walkable surface?</b>
    ///
    /// <para>A floor slab is drawn at the cell's lower boundary — <c>ChunkMesher.EmitFloor</c> calls
    /// <c>AddRoof</c> at <c>CellMetrics.FloorCentre</c> — so in the numbers below <b>y = 0 is the
    /// plane a colonist stands on</b>. Every slab's top face should therefore land on 0, and two
    /// slabs whose tops differ are two floors at one layer that do not draw level.</para>
    ///
    /// <para>The owner reported that as "it landed one height below" with the stone tile recessed
    /// into the wood deck (2026-09-18, two screenshots). This measures it rather than judging it
    /// from a picture, which is what the previous three sessions did wrong
    /// (<c>docs/bug-patterns.md</c> P6).</para>
    ///
    /// <para><c>scripts/unity.sh exec Odyssey.EditorTools.SlabHeightProbe.Run</c></para>
    /// </summary>
    public static class SlabHeightProbe
    {
        static readonly string[] Ids =
        {
            ModuleIds.Slab,
            "odyssey.module.slab.concrete",
            "odyssey.module.slab.deck",
            "odyssey.module.slab.wood",
            "odyssey.module.slab.stone",
        };

        public static void Run()
        {
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            if (catalogue == null)
            {
                Debug.LogError("[SlabHeight] no ModuleCatalogue at the expected path");
                return;
            }

            var library = new ModuleLibrary(catalogue);

            Debug.Log($"[SlabHeight] cell is {CellMetrics.SizeXZ} x {CellMetrics.SizeXZ} x {CellMetrics.SizeY} m; " +
                      "y = 0 is the cell's floor plane, so a slab's top face wants to be 0");

            for (int i = 0; i < Ids.Length; i++)
            {
                int module = library.Resolve(Ids[i], ModuleShape.FloorSlab);
                ResolvedModule resolved = library[module];
                Bounds b = resolved.Bounds;

                Debug.Log(
                    $"[SlabHeight] {Ids[i],-34} art={resolved.UsesArt,-5} " +
                    $"y: min={b.min.y,7:F3} max={b.max.y,7:F3} thickness={b.size.y,6:F3}  " +
                    $"xz: {b.size.x:F6} x {b.size.z:F6} centred on ({b.center.x:F6}, {b.center.z:F6}) " +
                    $"— a cell is {CellMetrics.SizeXZ:F6}, so the edge-to-edge slack is " +
                    $"{(CellMetrics.SizeXZ - b.size.x) * 1000f:F3} mm in x, " +
                    $"{(CellMetrics.SizeXZ - b.size.z) * 1000f:F3} mm in z");

                // And how flat the walked-on face actually is. A tile whose top undulates shows
                // its own rim at every seam, because the neighbour's planks do not line up with
                // it — which is a different fault from any placement and cannot be placed away.
                Debug.Log($"[SlabHeight] {Ids[i],-34} {TopFace(resolved, b)}");
            }
        }

        /// <summary>
        /// The spread of the module's vertices in the top 30 mm, and how many of them there are.
        ///
        /// <para>Read in transformed space — the parts carry their placement in
        /// <c>ModulePart.Local</c> — because that is the geometry the renderer submits, and a
        /// question about whether two tiles meet flush is a question about world heights.</para>
        /// </summary>
        static string TopFace(ResolvedModule resolved, Bounds bounds)
        {
            float band = bounds.max.y - 0.030f;
            int counted = 0, unreadable = 0;
            float low = float.MaxValue, high = float.MinValue;

            ModulePart[] parts = resolved.Parts;
            for (int p = 0; p < parts.Length; p++)
            {
                Mesh mesh = parts[p].Mesh;
                if (mesh == null || !mesh.isReadable) { unreadable++; continue; }

                Vector3[] vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    float y = parts[p].Local.MultiplyPoint3x4(vertices[v]).y;
                    if (y < band) continue;
                    counted++;
                    if (y < low) low = y;
                    if (y > high) high = y;
                }
            }

            if (unreadable > 0)
                return $"top face: {unreadable} of {parts.Length} parts are not readable, so the " +
                       "flatness of the walked-on surface cannot be measured from here";
            if (counted == 0) return "top face: no vertices within 30 mm of the top";

            return $"top face: {counted} vertices in the top 30 mm, spanning " +
                   $"{(high - low) * 1000f:F2} mm — {(high - low < 0.0005f ? "flat" : "NOT flat")}";
        }
    }
}
