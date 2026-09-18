#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// <b>Is the slab art's walked-on face flat?</b>
    ///
    /// <para>Every placement answer has been tried and measured not to close the seam a floor draws
    /// on the cell pitch: the drape's shear (the flat board draws it too), the ground or a wall
    /// beneath (a floating deck draws it too), growing each tile to overlap its neighbours by 6 mm
    /// and by 200 mm, and staggering alternate tiles by a millimetre. What is left is the one thing
    /// placement cannot move — the art. If the top of the plate steps between its planks, then two
    /// abutting copies do not meet flush however exactly they are placed, and the step is seen at
    /// every seam.</para>
    ///
    /// <para>The pack imports with Read/Write off, so the vertices are not there to measure. This
    /// turns it on for the one model, measures, and turns it back off — the pack is gitignored and
    /// the setting is restored either way.</para>
    ///
    /// <para><c>scripts/unity.sh exec Odyssey.EditorTools.SlabTopFaceProbe.Run</c></para>
    /// </summary>
    public static class SlabTopFaceProbe
    {
        public static void Run()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
            if (catalogue == null)
            {
                Debug.LogError("[TopFace] no ModuleCatalogue");
                return;
            }

            ModuleEntry? row = catalogue.Find("odyssey.module.slab.wood");
            GameObject? prefab = row?.prefab;

            if (prefab == null)
            {
                Debug.LogError("[TopFace] the wood slab row has no prefab");
                return;
            }

            // The catalogue row points at a *prefab*; the Read/Write flag lives on the model the
            // prefab's meshes come from, which is a different asset.
            string path = string.Empty;
            var found = prefab.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (int i = 0; i < found.Length && path.Length == 0; i++)
                if (found[i].sharedMesh != null)
                    path = AssetDatabase.GetAssetPath(found[i].sharedMesh);

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[TopFace] {path} is not a model");
                return;
            }

            bool was = importer.isReadable;
            try
            {
                if (!was)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                Measure(AssetDatabase.GetAssetPath(prefab), prefab);
            }
            finally
            {
                if (!was)
                {
                    var back = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (back != null)
                    {
                        back.isReadable = false;
                        back.SaveAndReimport();
                    }
                }
            }
        }

        static void Measure(string path, GameObject prefab)
        {
            GameObject? reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject subject = reloaded != null ? reloaded : prefab;

            var heights = new List<float>();
            var points = new List<Vector3>();
            float top = float.MinValue;

            var filters = subject.GetComponentsInChildren<MeshFilter>(includeInactive: false);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh? mesh = filters[i].sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;

                Matrix4x4 local = subject.transform.worldToLocalMatrix
                                  * filters[i].transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 point = local.MultiplyPoint3x4(vertices[v]);
                    points.Add(point);
                    heights.Add(point.y);
                    if (point.y > top) top = point.y;
                }
            }

            if (heights.Count == 0)
            {
                Debug.LogError("[TopFace] nothing readable even after the reimport");
                return;
            }

            // Every vertex within 50 mm of the highest one: that is the walked-on face and whatever
            // relief is modelled into it.
            float low = float.MaxValue;
            int counted = 0;
            var rungs = new SortedDictionary<int, int>();
            var topBox = new Bounds();
            var whole = new Bounds();
            bool hasTop = false, hasWhole = false;
            for (int i = 0; i < heights.Count; i++)
            {
                if (!hasWhole) { whole = new Bounds(points[i], Vector3.zero); hasWhole = true; }
                else whole.Encapsulate(points[i]);

                if (heights[i] < top - 0.050f) continue;
                counted++;
                if (heights[i] < low) low = heights[i];
                int rung = Mathf.RoundToInt((top - heights[i]) * 1000f);
                rungs.TryGetValue(rung, out int seen);
                rungs[rung] = seen + 1;

                if (!hasTop) { topBox = new Bounds(points[i], Vector3.zero); hasTop = true; }
                else topBox.Encapsulate(points[i]);
            }

            // The number the whole hunt turns on: how wide the *walked-on face* is, against how
            // wide the piece is. A plate whose sides flare leaves a groove between two abutting
            // copies however exactly they are placed, and the groove is what draws the line.
            Debug.Log($"[TopFace] the piece spans {whole.size.x:F4} x {whole.size.z:F4} m; its top " +
                      $"face spans {topBox.size.x:F4} x {topBox.size.z:F4} m. The overhang is " +
                      $"{(whole.size.x - topBox.size.x) * 500f:F2} mm a side in x and " +
                      $"{(whole.size.z - topBox.size.z) * 500f:F2} mm in z — so two abutting tiles " +
                      $"leave a groove of {(whole.size.x - topBox.size.x) * 1000f:F2} mm between " +
                      "their walked-on faces.");

            var listed = new List<string>();
            foreach (KeyValuePair<int, int> rung in rungs) listed.Add($"{rung.Key}mm x{rung.Value}");

            Debug.Log($"[TopFace] {path}: {heights.Count} vertices, {counted} of them within 50 mm " +
                      $"of the top. The walked-on face spans {(top - low) * 1000f:F2} mm — " +
                      $"{(top - low < 0.0005f ? "FLAT, so two abutting tiles meet flush" : "NOT FLAT, so two abutting tiles cannot meet flush")}. " +
                      $"Steps below the top: {string.Join(", ", listed)}");
        }
    }
}
