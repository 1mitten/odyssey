#nullable enable
// Design 50 §7a-bis: photograph sandbags laid by CoverShape — a straight run, a corner, a T and a
// lone piece — beside a post the height of a drawn colonist, so "do they look like sandbags" is
// answered by looking rather than by reading the layout code.
//
//   scripts/unity.sh shot Odyssey.EditorTools.SandbagCheck.Shoot
//
// Writes Logs/sandbags-<view>.png. Not a test and not a gate: SandbagShapeTests holds the layout.
// Each bag is a GameObject here rather than an instance in a chunk bucket; the mesh, the matrix
// and the cloth colour are the ones the mesher uses.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;

namespace Odyssey.EditorTools
{
    public static class SandbagCheck
    {
        [MenuItem("Odyssey/Presentation/Photograph the sandbags")]
        public static void ShootFromMenu() => Run(exitWhenDone: false);

        public static void Shoot() => Run(Application.isBatchMode);

        const int Y = 0;

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            var owned = new List<UnityEngine.Object>();
            GameObject? root = null;
            float amplitude = GroundRelief.Amplitude;

            try
            {
                GroundRelief.Amplitude = 0f;
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                    "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
                using var library = new ModuleLibrary(catalogue);
                ResolvedModule bag = library[library.Resolve(ModuleIds.Sandbags, ModuleShape.Sandbag)];

                root = new GameObject("SandbagCheck");
                owned.Add(root);
                PlayScene.BuildSheetLighting(root.transform);
                Ground(root.transform, owned);

                var cloths = new Material[StuffPalette.HessianShades];
                for (int s = 0; s < cloths.Length; s++)
                {
                    cloths[s] = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { color = StuffPalette.Hessian(s) };
                    cloths[s].SetFloat("_Smoothness", 0.1f);
                    owned.Add(cloths[s]);
                }

                // A straight run of four along x = 0..3 at z = 0, turning north at x = 3 for two
                // more (a corner); a T at x = 1 with one arm south; and a lone piece off to the side.
                var cells = new HashSet<(int x, int z)>
                {
                    (0, 0), (1, 0), (2, 0), (3, 0), (3, 1), (3, 2), (1, -1), (-2, 3),
                };
                int bags = 0;
                foreach ((int x, int z) in cells)
                {
                    int joins = 0;
                    for (int dir = 0; dir < Directions.Count; dir++)
                        if (cells.Contains((x + Directions.DeltaX[dir], z + Directions.DeltaZ[dir]))) joins |= 1 << dir;
                    bags += Lay(root.transform, bag, cloths, x, z, joins);
                }
                Debug.Log($"[Sandbags] {cells.Count} pieces, {bags} bags, " +
                          $"{bag.Parts[0].Mesh.triangles.Length / 3} triangles a bag");

                // A post the height a colonist is drawn (about 2.5 m, the rigs' scale 1.4), behind
                // the run, and a cell-sized pad under the lone piece to show it stays in its cell.
                Reference(root.transform, owned, Centre(1, 1), new Vector3(0.45f, 2.5f, 0.45f), new Color(0.2f, 0.45f, 0.85f, 1f));
                Reference(root.transform, owned, Centre(-2, 3), new Vector3(CellMetrics.SizeXZ, 0.02f, CellMetrics.SizeXZ), new Color(0.85f, 0.2f, 0.2f, 1f));

                PlayScene.ShootAt(Centre(1, 0) + new Vector3(0f, 0.8f, 0f), 6f, "Logs/sandbags-close.png");
                PlayScene.ShootAt(Centre(1, 1) + new Vector3(0f, 0.8f, 0f), 14f, "Logs/sandbags-wide.png");
                PlayScene.ShootAt(Centre(3, 0) + new Vector3(0f, 0.8f, 0f), 5f, "Logs/sandbags-corner.png");
                Debug.Log("[Sandbags] wrote Logs/sandbags-close.png, -wide.png and -corner.png. The blue post " +
                          "is 2.5 m, a drawn colonist; the red pad is one cell under the lone piece.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Sandbags] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                GroundRelief.Amplitude = amplitude;
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                for (int i = 0; i < owned.Count; i++)
                    if (owned[i] != null && owned[i] != root) UnityEngine.Object.DestroyImmediate(owned[i]);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static int Lay(Transform parent, ResolvedModule bag, Material[] cloths, int x, int z, int joins)
        {
            var parts = new Matrix4x4[CoverShape.MaxParts];
            var shades = new int[CoverShape.MaxParts];
            int count = CoverShape.Parts(CoreContent.EdificeSandbags, x, z, Y, joins, parts, shades);
            for (int i = 0; i < count; i++)
            for (int p = 0; p < bag.Parts.Length; p++)
            {
                ModulePart part = bag.Parts[p];
                Matrix4x4 m = parts[i] * part.Local;
                var piece = new GameObject($"bag{x}_{z}_{i}");
                piece.transform.SetParent(parent, worldPositionStays: false);
                piece.transform.localPosition = m.GetColumn(3);
                piece.transform.localRotation = m.rotation;
                piece.transform.localScale = m.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                piece.AddComponent<MeshRenderer>().sharedMaterial = cloths[shades[i]];
            }
            return count;
        }

        static Vector3 Centre(int x, int z) => CellMetrics.FloorCentre(x, z, Y);

        static void Reference(Transform parent, List<UnityEngine.Object> owned, Vector3 at, Vector3 size, Color colour)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent, worldPositionStays: false);
            box.transform.localPosition = at + new Vector3(0f, size.y * 0.5f, 0f);
            box.transform.localScale = size;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = colour };
            owned.Add(material);
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Ground(Transform parent, List<UnityEngine.Object> owned)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plane.transform.SetParent(parent, worldPositionStays: false);
            plane.transform.localPosition = Centre(1, 1) + new Vector3(0f, -0.25f, 0f);
            plane.transform.localScale = new Vector3(CellMetrics.SizeXZ * 9f, 0.5f, CellMetrics.SizeXZ * 9f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.36f, 0.42f, 0.25f, 1f) };
            owned.Add(material);
            plane.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
