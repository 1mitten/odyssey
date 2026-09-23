#nullable enable
// Design 31 C1: photograph the campfire module through the real resolution path, beside things
// of known size, so "does it read as a campfire or as a toy" is answered by looking rather than
// by arithmetic on a bounds figure.
//
//   scripts/unity.sh shot Odyssey.EditorTools.CampfireCheck.Shoot
//
// Writes Logs/campfire-<view>.png. Not a test and not a gate: it asserts nothing, it makes a
// picture. The gate that a campfire resolves to art at all is CampfireArtTests in EditMode.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Odyssey.Presentation.Rendering;

namespace Odyssey.EditorTools
{
    public static class CampfireCheck
    {
        [MenuItem("Odyssey/Presentation/Photograph the campfire")]
        public static void ShootFromMenu() => Run(exitWhenDone: false);

        public static void Shoot() => Run(Application.isBatchMode);

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            var owned = new List<UnityEngine.Object>();
            GameObject? root = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                    "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
                using var library = new ModuleLibrary(catalogue);

                // The same call WorldRenderModel makes, so what is photographed is what the game
                // would draw — scale, centreXZ and baseAtY included.
                ResolvedModule fire = library[library.Resolve(ModuleIds.Campfire, ModuleShape.SolidBlock)];

                Debug.Log($"[Campfire] parts={fire.Parts.Length} usesArt={fire.UsesArt} " +
                          $"empty={fire.IsEmpty} fallbackFirstPart=" +
                          (fire.Parts.Length > 0 ? fire.Parts[0].IsFallback.ToString() : "n/a"));

                // Measured, not eyeballed. The first picture made the ring look about a metre
                // across where the row's scale predicted 2.30 m, and a screenshot cannot settle
                // that — the camera's own perspective is part of the impression.
                Bounds placed = Measure(fire);
                Debug.Log($"[Campfire] resolved footprint {placed.size.x:F2} x {placed.size.z:F2} m, " +
                          $"height {placed.size.y:F2} m, base y {placed.min.y:F2}, " +
                          $"centre ({placed.center.x:F2}, {placed.center.z:F2}). " +
                          $"The cell is {CellMetrics.SizeXZ:F2} x {CellMetrics.SizeXZ:F2} x {CellMetrics.SizeY:F2}.");

                if (fire.IsEmpty || !fire.UsesArt)
                    Debug.LogWarning("[Campfire] resolved to no art — either the packs are absent " +
                                     "or the catalogue row did not take. The picture will show the " +
                                     "placeholder block, which is the thing being fixed.");

                root = new GameObject("CampfireCheck");
                owned.Add(root);
                PlayScene.BuildSheetLighting(root.transform);

                Ground(root.transform, owned);

                // The fire on the middle cell, a cell-sized reference cube one cell to its left,
                // and a colonist-height post one cell to its right. A prop is "the right size"
                // relative to the cell it must not overflow and to the people who stand by it,
                // and both of those are in the frame on purpose.
                Place(root.transform, fire, Cell(0, 0));

                // A flat cell-sized pad UNDER the fire rather than a cube beside it: the first
                // version put a 3 m cube one cell away and it stood straight in front of the
                // thing being photographed. What the picture has to show is whether the ring
                // overflows its own cell, and a pad it is standing on shows that directly.
                Reference(root.transform, owned, Cell(0, 0), new Vector3(
                    CellMetrics.SizeXZ, 0.02f, CellMetrics.SizeXZ), new Color(0.85f, 0.2f, 0.2f, 1f));

                // The colonist-height post goes behind and to the side, out of the sight line.
                Reference(root.transform, owned, Cell(1, 1), new Vector3(0.45f, 1.8f, 0.45f),
                    new Color(0.2f, 0.45f, 0.85f, 1f));

                var centre = Cell(0, 0);
                Shot(centre, 7f, "Logs/campfire-close.png");
                Shot(centre, 14f, "Logs/campfire-wide.png");

                Debug.Log("[Campfire] wrote Logs/campfire-close.png and Logs/campfire-wide.png. " +
                          "The red box is one whole cell (2.5 x 3.0 x 2.5 m); the blue post is " +
                          "1.8 m, about a colonist. The ring must sit inside the red box's " +
                          "footprint and must not look like a toy beside the blue one.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Campfire] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                for (int i = 0; i < owned.Count; i++)
                    if (owned[i] != null && owned[i] != root) UnityEngine.Object.DestroyImmediate(owned[i]);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static Vector3 Cell(int x, int z) =>
            new Vector3(x * CellMetrics.SizeXZ, 0f, z * CellMetrics.SizeXZ);

        /// <summary>The world-space box the resolved parts actually occupy, placed at the origin
        /// cell — the number the screenshot cannot give you.</summary>
        static Bounds Measure(ResolvedModule module)
        {
            var box = new Bounds();
            bool has = false;
            for (int i = 0; i < module.Parts.Length; i++)
            {
                ModulePart part = module.Parts[i];
                Bounds b = part.Mesh.bounds;
                var corners = new Vector3[8];
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3(
                        (c & 1) == 0 ? b.min.x : b.max.x,
                        (c & 2) == 0 ? b.min.y : b.max.y,
                        (c & 4) == 0 ? b.min.z : b.max.z);
                    corners[c] = part.Local.MultiplyPoint3x4(corner);
                }
                for (int c = 0; c < 8; c++)
                {
                    if (!has) { box = new Bounds(corners[c], Vector3.zero); has = true; }
                    else box.Encapsulate(corners[c]);
                }
            }
            return box;
        }

        /// <summary>Draw the resolved module exactly as the mesher would: each part at its own
        /// baked local, under one placement at the cell's floor centre.</summary>
        static void Place(Transform parent, ResolvedModule module, Vector3 at)
        {
            for (int i = 0; i < module.Parts.Length; i++)
            {
                ModulePart part = module.Parts[i];
                var piece = new GameObject($"part{i}");
                piece.transform.SetParent(parent, worldPositionStays: false);

                Matrix4x4 m = Matrix4x4.Translate(at) * part.Local;
                piece.transform.localPosition = m.GetColumn(3);
                piece.transform.localRotation = m.rotation;
                piece.transform.localScale = m.lossyScale;

                piece.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                piece.AddComponent<MeshRenderer>().sharedMaterial = part.Material;
            }
        }

        static void Reference(Transform parent, List<UnityEngine.Object> owned,
            Vector3 at, Vector3 size, Color colour)
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
            plane.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            plane.transform.localScale = new Vector3(CellMetrics.SizeXZ * 7f, 0.5f, CellMetrics.SizeXZ * 7f);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.36f, 0.42f, 0.25f, 1f) };
            owned.Add(material);
            plane.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Shot(Vector3 focus, float span, string path)
        {
            PlayScene.ShootAt(focus + new Vector3(0f, 0.8f, 0f), span, path);
            Debug.Log($"[Campfire] wrote {path}");
        }
    }
}
