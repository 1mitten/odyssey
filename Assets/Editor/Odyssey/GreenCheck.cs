#nullable enable
using System;
using System.Linq;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// A controlled test for "the colonists look green".
    ///
    /// The question is whether something is tinting the figures, and the only way to answer it is
    /// to change one thing at a time and photograph the result. So: the same three colonists, and
    /// beside them **two reference objects whose colour nobody can argue about** — a pure white
    /// cube and a mid-grey sphere — rendered four times over the matrix of
    /// {grass ground, neutral ground} x {outline on, outline off}.
    ///
    /// Each picture rules something out:
    ///
    /// - White reference greens over grass but not over neutral ground → light really is bouncing
    ///   off the grass, and the lighting setup is the culprit.
    /// - Everything gains a green fringe with the outline on and loses it with the outline off →
    ///   it is the ink, which is a dark desaturated green rather than a neutral black.
    /// - Colonists are green in all four and the references never are → it is the character
    ///   materials or the pack atlas, and nothing to do with the scene at all.
    ///
    /// Written as a throwaway diagnostic rather than a test because the answer is a picture. It is
    /// kept because "does X tint Y" is a question this project has asked before and will again.
    /// </summary>
    public static class GreenCheck
    {
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        static readonly string[] Subjects =
        {
            "SM_Gen_Chr_Street_Male_01",
            "SM_Gen_Chr_Business_Female_01",
            "SM_Chr_Medical_Male_01",
        };

        [MenuItem("Odyssey/Presentation/Check for colour cast")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            OutlineFeature? outline = FindOutline();
            bool outlineWas = outline != null && outline.isActive;
            GameObject? root = null;

            try
            {
                if (outline == null)
                    Debug.LogWarning("[Green] no outline feature found; the outline column is moot.");

                // One dimension at a time, in the order that rules things out fastest.
                var conditions = new (string name, bool grass, bool tufts, bool ink)[]
                {
                    ("neutral",    false, false, true),
                    ("grass",      true,  false, true),
                    ("tufts",      true,  true,  true),
                    ("tufts-noink", true, true,  false),
                };

                foreach ((string name, bool grass, bool tufts, bool ink) in conditions)
                {
                    outline?.SetActive(ink);

                    root = new GameObject("GreenCheck");
                    PlayScene.BuildSheetLighting(root.transform);
                    Stage(root.transform, grass, tufts);

                    string path = $"Logs/green-{name}.png";
                    PlayScene.ShootAt(new Vector3(CellMetrics.SizeXZ * 2f, 0.8f, 0f), 7f, path);
                    Debug.Log($"[Green] wrote {path}");

                    UnityEngine.Object.DestroyImmediate(root);
                    root = null;
                }

                Debug.Log("[Green] compare the four: a white cube that greens only over grass means " +
                          "bounced light; a green fringe that vanishes without ink means the ink; " +
                          "colonists green in all four means their own materials.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Green] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                outline?.SetActive(outlineWas);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>Ground, three colonists, and two references of known colour, in a row.</summary>
        static void Stage(Transform parent, bool grass, bool tufts)
        {
            Material? ground = grass ? ScatterSheet.FindMaterial("Mat_Grass_Textures_01") : null;

            for (int x = -1; x <= 5; x++)
            for (int z = -2; z <= 2; z++)
            {
                var at = new Vector3(x * CellMetrics.SizeXZ, 0f, z * CellMetrics.SizeXZ);
                Tile(parent, at, ground);
                if (tufts) Scatter(parent, x, z);
            }

            for (int i = 0; i < Subjects.Length; i++)
            {
                GameObject? prefab = ScatterSheet.FindPrefab(Subjects[i]);
                if (prefab == null) continue;
                GameObject figure = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                figure.transform.position = new Vector3(i * CellMetrics.SizeXZ, 0f, 0f);
                figure.transform.localScale = Vector3.one * 1.4f;
            }

            // The controls. Unlit would prove nothing — these have to be lit by the same sun and
            // the same ambient as everything else, so that a cast in the lighting shows up on them.
            Reference(parent, new Vector3(3f * CellMetrics.SizeXZ, 0f, 0f), PrimitiveType.Cube,
                Color.white, "White reference");
            Reference(parent, new Vector3(4f * CellMetrics.SizeXZ, 0f, 0f), PrimitiveType.Sphere,
                new Color(0.5f, 0.5f, 0.5f), "Grey reference");
        }

        /// <summary>
        /// Grass tufts on one cell, placed by the same arithmetic the mesher uses.
        ///
        /// The same arithmetic matters: the suspicion under test is that a clump 1.9 m across,
        /// scattered up to a third of a cell off centre, simply grows through the legs of a
        /// colonist standing at that centre. Placing them by eye here would answer a question
        /// nobody asked.
        /// </summary>
        static void Scatter(Transform parent, int x, int z)
        {
            string[] variants =
            {
                "SM_Env_Grass_Med_Clump_03",
                "SM_Env_Grass_Short_Clump_03",
                "SM_Env_Grass_Tall_Clump_03",
            };

            int count = GroundScatter.CountFor(x, z, 120);
            for (int slot = 0; slot < count; slot++)
            {
                GroundScatter.Placement(x, z, slot,
                    out float offsetX, out float offsetZ, out float yaw, out float scale);

                GameObject? prefab = ScatterSheet.FindPrefab(
                    variants[GroundScatter.VariantFor(x, z, slot, variants.Length)]);
                if (prefab == null) continue;

                GameObject tuft = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                tuft.transform.position = new Vector3(
                    (x + offsetX) * CellMetrics.SizeXZ, 0f, (z + offsetZ) * CellMetrics.SizeXZ);
                tuft.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                tuft.transform.localScale = Vector3.one * scale;
            }
        }

        static void Tile(Transform parent, Vector3 at, Material? ground)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
            tile.transform.SetParent(parent, false);
            tile.transform.position = at + Vector3.down * (CellMetrics.SizeY * 0.5f);
            tile.transform.localScale = new Vector3(
                CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ);
            if (ground != null) tile.GetComponent<MeshRenderer>().sharedMaterial = ground;
        }

        static void Reference(Transform parent, Vector3 at, PrimitiveType shape, Color colour, string name)
        {
            GameObject reference = GameObject.CreatePrimitive(shape);
            UnityEngine.Object.DestroyImmediate(reference.GetComponent<Collider>());
            reference.name = name;
            reference.transform.SetParent(parent, false);
            reference.transform.position = at + Vector3.up * 0.9f;
            reference.transform.localScale = Vector3.one * 1.8f;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(lit) { name = name };
            material.SetColor("_BaseColor", colour);
            // Flat and matte: a specular highlight from the sky would be one more thing to argue
            // about in a picture whose whole purpose is to have nothing to argue about.
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            reference.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static OutlineFeature? FindOutline()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            return data == null ? null : data.rendererFeatures.OfType<OutlineFeature>().FirstOrDefault();
        }
    }
}
