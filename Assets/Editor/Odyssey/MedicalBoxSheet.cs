#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// The three Battle Royale medical boxes side by side, for the owner to pick the one medical
    /// supplies are drawn as (<c>docs/design/37-medical-supplies.md</c> §5).
    ///
    /// <para>The top row is every candidate at <b>one uniform size</b>: its longest side scaled to
    /// <see cref="Longest"/>, which is the owner's "made uniformly small". The bottom row is the
    /// meal sacks and a stone at their own catalogue scales, so the size is judged against what
    /// already lies on the ground. Each candidate's size as authored is logged, because that is the
    /// number the catalogue row's scale is worked out from.</para>
    ///
    /// <para>Writes <c>Logs/medical-box-sheet.png</c> and nothing else.
    /// <c>scripts/unity.sh shot Odyssey.EditorTools.MedicalBoxSheet.Shoot</c>.</para>
    /// </summary>
    public static class MedicalBoxSheet
    {
        static readonly string[] Candidates =
        {
            "SM_Prop_MedicalBox_01", "SM_Prop_MedicalBox_02", "SM_Prop_Crate_Medical_01",
        };

        /// <summary>The longest side of a box on the sheet, in metres: about an ore lump's width.</summary>
        public const float Longest = 0.5f;

        [MenuItem("Odyssey/Presentation/Medical box sheet")]
        public static void ShootFromMenu() => Run(exitWhenDone: false);

        public static void Shoot() => Run(Application.isBatchMode);

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? root = null;
            try
            {
                root = new GameObject("MedicalBoxSheet");
                PlayScene.BuildSheetLighting(root.transform);
                Material? grass = ScatterSheet.FindMaterial("Mat_Grass_Textures_01");

                for (int i = 0; i < Candidates.Length; i++)
                {
                    var at = new Vector3(i * CellMetrics.SizeXZ, 0f, 0f);
                    Tile(root.transform, at, grass);
                    GameObject? prefab = ScatterSheet.FindPrefab(Candidates[i]);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[MedSheet] {Candidates[i]} not found");
                        continue;
                    }

                    Vector3 size = Place(root.transform, prefab, at, target: Longest);
                    Debug.Log($"[MedSheet] cell {i},0: {Candidates[i]} authored " +
                              $"{size.x:0.00} x {size.y:0.00} x {size.z:0.00} m, " +
                              $"drawn at scale {Longest / Mathf.Max(size.x, Mathf.Max(size.y, size.z)):0.000}");
                }

                // For scale: what already lies on the ground, at its own catalogue row's scale.
                var references = new List<(string name, float scale)>
                {
                    ("SM_Gen_Prop_Sack_Stack_01", 1.6f),
                    ("SM_Gen_Env_Rock_03", 0.35f),
                };
                for (int i = 0; i < references.Count; i++)
                {
                    var at = new Vector3(i * CellMetrics.SizeXZ, 0f, -CellMetrics.SizeXZ);
                    Tile(root.transform, at, grass);
                    GameObject? prefab = ScatterSheet.FindPrefab(references[i].name);
                    if (prefab != null) Place(root.transform, prefab, at, scale: references[i].scale);
                    Debug.Log($"[MedSheet] cell {i},1: {references[i].name} at {references[i].scale} (reference)");
                }

                var centre = new Vector3(CellMetrics.SizeXZ, 0f, -CellMetrics.SizeXZ * 0.5f);
                PlayScene.ShootAt(centre, 3.3f * CellMetrics.SizeXZ, "Logs/medical-box-sheet.png");
                Debug.Log("[MedSheet] wrote Logs/medical-box-sheet.png");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MedSheet] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

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
        /// Stand the prop on the cell's centre, scaled either so its longest side is
        /// <paramref name="target"/> or by <paramref name="scale"/>. Returns its authored size.
        /// </summary>
        static Vector3 Place(Transform parent, GameObject prefab, Vector3 at, float target = 0f, float scale = 1f)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = Vector3.zero;
            instance.transform.localScale = Vector3.one;

            Bounds authored = BoundsOf(instance);
            float longest = Mathf.Max(authored.size.x, Mathf.Max(authored.size.y, authored.size.z));
            float s = target > 0f && longest > 0f ? target / longest : scale;
            instance.transform.localScale = Vector3.one * s;

            Bounds drawn = BoundsOf(instance);
            instance.transform.position = at + new Vector3(-drawn.center.x, -drawn.min.y, -drawn.center.z);
            return authored.size;
        }

        static Bounds BoundsOf(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
