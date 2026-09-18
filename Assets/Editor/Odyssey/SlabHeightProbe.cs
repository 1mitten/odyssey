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
                    $"xz: {b.size.x:F2} x {b.size.z:F2}");
            }
        }
    }
}
