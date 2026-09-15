#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Photographs candidate scatter props, on grass, at the size they would be drawn, side by
    /// side and in a known order.
    ///
    /// **Why this exists.** A Synty prefab's name says where it was meant to be used, not what it
    /// looks like: `SM_Prop_Box_Supplies_01` is a crate of scrap, and `SM_Env_Grass_01` is a stand
    /// of reeds. The inventory CSV gives dimensions and triangle counts, which rules a piece out
    /// for size and says nothing whatever about whether it reads as a meadow. Picking from names
    /// and numbers has now cost two rounds of rebuilding the catalogue and re-rendering the world
    /// to find out, and both times the answer was obvious within a second of seeing a picture.
    ///
    /// The sheet is the cheap version of that second: one command, one image, every candidate on
    /// the same ground under the same sun at the same scale, with the order printed to the log so
    /// a row can be named from it.
    ///
    /// Run it with <c>scripts/unity.sh shot Odyssey.EditorTools.ScatterSheet.Shoot</c> — it needs
    /// a graphics device, like every other entry point that produces a picture.
    /// </summary>
    public static class ScatterSheet
    {
        /// <summary>
        /// The candidates. Add a name, run the sheet, look. Nothing here is committed to anything.
        /// </summary>
        static readonly string[] Candidates =
        {
            "SM_Env_Grass_Short_Clump_01",
            "SM_Env_Grass_Short_Clump_02",
            "SM_Env_Grass_Short_Clump_03",
            "SM_Env_Grass_Med_Clump_01",
            "SM_Env_Grass_Med_Clump_02",
            "SM_Env_Grass_Med_Clump_03",
            "SM_Env_Grass_Tall_Clump_01",
            "SM_Env_Grass_Tall_Clump_02",
            "SM_Env_Grass_Tall_Clump_03",
            "SM_Env_Grass_Tall_Clump_04",
            "SM_Env_Grass_Bush_01",
            "SM_Env_Wildflowers_01",
            "SM_Env_Wildflowers_02",
            "SM_Env_Flowers_Flat_01",
            "SM_Env_Flowers_Flat_02",
        };

        [MenuItem("Odyssey/Presentation/Shoot scatter sheet")]
        public static void ShootFromMenu() => Run(exitWhenDone: false);

        public static void Shoot() => Run(Application.isBatchMode);

        /// <summary>Cells across the sheet before it wraps to the next row.</summary>
        const int Columns = 5;

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? root = null;
            try
            {
                root = new GameObject("ScatterSheet");
                PlayScene.BuildSheetLighting(root.transform);

                // The same ground the game draws, so a prop is judged against the colour it will
                // actually stand on rather than against a grey studio floor.
                Material? grass = FindMaterial("Mat_Grass_Textures_01");
                var placed = new List<string>();

                for (int i = 0; i < Candidates.Length; i++)
                {
                    GameObject? prefab = FindPrefab(Candidates[i]);
                    if (prefab == null) continue;

                    int column = placed.Count % Columns;
                    int row = placed.Count / Columns;
                    var at = new Vector3(column * CellMetrics.SizeXZ, 0f, -row * CellMetrics.SizeXZ);

                    Tile(root.transform, at, grass);
                    Place(root.transform, prefab, at);
                    placed.Add(Candidates[i]);
                }

                // The order is the whole value of the sheet: without it the picture shows two
                // dozen indistinguishable clumps and none of them can be named in a catalogue row.
                for (int i = 0; i < placed.Count; i++)
                    Debug.Log($"[Sheet] cell {i % Columns},{i / Columns}: {placed[i]}");

                int rows = Mathf.CeilToInt(placed.Count / (float)Columns);
                var centre = new Vector3(
                    (Columns - 1) * CellMetrics.SizeXZ * 0.5f, 0f,
                    -(rows - 1) * CellMetrics.SizeXZ * 0.5f);

                PlayScene.ShootAt(centre, Mathf.Max(Columns, rows) * CellMetrics.SizeXZ * 1.1f,
                    "Logs/scatter-sheet.png");
                Debug.Log($"[Sheet] wrote Logs/scatter-sheet.png with {placed.Count} of " +
                          $"{Candidates.Length} candidates, {Columns} per row");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Sheet] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>One cell of ground, so scale is judged against the grid rather than in a void.</summary>
        static void Tile(Transform parent, Vector3 at, Material? grass)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
            tile.transform.SetParent(parent, false);
            tile.transform.position = at + Vector3.down * (CellMetrics.SizeY * 0.5f);
            tile.transform.localScale = new Vector3(
                CellMetrics.SizeXZ * 0.98f, CellMetrics.SizeY, CellMetrics.SizeXZ * 0.98f);
            if (grass != null) tile.GetComponent<MeshRenderer>().sharedMaterial = grass;
        }

        /// <summary>
        /// The prop, centred on the cell and standing on it — the same normalisation a catalogue
        /// row with <c>centreXZ</c> and <c>baseAtY</c> gets, so the sheet does not flatter a piece
        /// whose pivot happens to be convenient.
        /// </summary>
        static void Place(Transform parent, GameObject prefab, Vector3 at)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = at;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            instance.transform.position = at + new Vector3(
                at.x - bounds.center.x, at.y - bounds.min.y, at.z - bounds.center.z);
        }

        static GameObject? FindPrefab(string exactName) =>
            Load<GameObject>($"{exactName} t:Prefab", exactName);

        static Material? FindMaterial(string exactName) =>
            Load<Material>($"{exactName} t:Material", exactName);

        static T? Load<T>(string filter, string exactName) where T : UnityEngine.Object
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/Synty"))) return null;
            string? path = AssetDatabase.FindAssets(filter, new[] { "Assets/Synty" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), exactName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
