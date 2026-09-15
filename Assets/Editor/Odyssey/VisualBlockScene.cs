#nullable enable
// Lane D3 spike, visual half: generates the "what does it look like in game" scene —
// one ruined-city block at the confirmed 2.5 × 2.5 × 3.0 m cell: an intact three-storey
// shell, a collapsed two-storey shell, street clutter, posed characters for scale, and
// fire/smoke on the ruin. Not gameplay code; a reproducible script-generated mock per
// the project convention (scenes are generated, never hand-authored).
//
// Editor menu:  Odyssey > Spikes > Build visual block scene
// Headless:     scripts/unity.sh exec Odyssey.EditorTools.VisualBlockScene.Build
//
// Placement is pivot-tolerant: pieces are snapped by their measured renderer bounds,
// so mixed Synty pivots (base/centre/corner) all land on the grid. Prefabs are found
// by name under Assets/Synty; a missing name is logged and skipped, never fatal, so
// the scene still generates on a clone without the licensed packs (it will just be
// mostly empty ground).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Odyssey.EditorTools
{
    public static class VisualBlockScene
    {
        const float CellXZ = 2.5f;
        const float CellY = 3.0f;
        const string ScenePath = "Assets/Scenes/Spikes/VisualBlock.unity";

        static readonly List<string> Missing = new List<string>();
        static Transform? _root;

        [MenuItem("Odyssey/Spikes/Build visual block scene")]
        public static void BuildFromMenu() => BuildInternal(exitWhenDone: false);

        /// <summary>Batchmode entry point. Exits the editor with 0 on success, 1 on failure.</summary>
        public static void Build() => BuildInternal(Application.isBatchMode);

        static void BuildInternal(bool exitWhenDone)
        {
            int exitCode = 0;
            try
            {
                Missing.Clear();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _root = new GameObject("VisualBlock").transform;

                BuildLighting();
                BuildGroundAndStreet();
                BuildIntactShell(cellX: 0, cellZ: 2, cellsX: 4, cellsZ: 3, storeys: 3);
                BuildRuinedShell(cellX: 7, cellZ: 2, cellsX: 4, cellsZ: 3);
                BuildBackdrop();
                BuildStreetLife();
                BuildCamera();

                Directory.CreateDirectory(Path.GetFullPath("Assets/Scenes/Spikes"));
                AssetDatabase.Refresh();
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[VisualBlock] saved {ScenePath}. Missing prefab names: " +
                          (Missing.Count == 0 ? "none" : string.Join(", ", Missing.Distinct())));
            }
            catch (Exception e)
            {
                Debug.LogError($"[VisualBlock] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        // ------------------------------------------------------------ lookup

        static GameObject? Load(string nameFilter, int pick, bool exact)
        {
            string[] guids = AssetDatabase.FindAssets($"{nameFilter} t:Prefab", new[] { "Assets/Synty" });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => exact
                    ? string.Equals(Path.GetFileNameWithoutExtension(p), nameFilter, StringComparison.OrdinalIgnoreCase)
                    : Path.GetFileNameWithoutExtension(p).StartsWith(nameFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            if (paths.Count == 0) { Missing.Add(nameFilter); return null; }
            return AssetDatabase.LoadAssetAtPath<GameObject>(paths[pick % paths.Count]);
        }

        /// <summary>Instantiate by exact prefab name; snap the piece's bounds onto the grid point.</summary>
        static GameObject? Place(string exactName, Vector3 baseCentre, float yaw = 0f, bool snap = true)
            => Instantiate(Load(exactName, 0, exact: true), exactName, baseCentre, yaw, snap);

        /// <summary>Instantiate the deterministic nth prefab whose name starts with the filter.</summary>
        static GameObject? PlaceAny(string nameFilter, int pick, Vector3 baseCentre, float yaw = 0f, bool snap = true)
            => Instantiate(Load(nameFilter, pick, exact: false), nameFilter, baseCentre, yaw, snap);

        static GameObject? Instantiate(GameObject? prefab, string label, Vector3 baseCentre, float yaw, bool snap)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(_root, worldPositionStays: false);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.transform.position = baseCentre;
            if (snap && TryWorldBounds(go, out Bounds b))
            {
                // Move so the bounds' base centre lands exactly on the requested point,
                // neutralising whichever pivot convention the piece uses.
                Vector3 delta = baseCentre - new Vector3(b.center.x, b.min.y, b.center.z);
                go.transform.position += delta;
            }
            return go;
        }

        static bool TryWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue; // particle bounds are unstable pre-sim
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has;
        }

        static Vector3 CellBase(float cellX, int layer, float cellZ)
            => new Vector3(cellX * CellXZ, layer * CellY, cellZ * CellXZ);

        // ------------------------------------------------------------ scene

        static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1.0f, 0.94f, 0.82f);
            sun.shadows = LightShadows.Soft;
            sun.transform.SetParent(_root, false);
            sun.transform.rotation = Quaternion.Euler(52f, 38f, 0f);
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.52f);
        }

        static void BuildGroundAndStreet()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_root, false);
            ground.transform.position = new Vector3(14f, -0.02f, 6f);
            ground.transform.localScale = new Vector3(9f, 1f, 9f); // 90 × 90 m
            Shader? lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                var asphalt = new Material(lit) { color = new Color(0.16f, 0.165f, 0.18f) };
                ground.GetComponent<Renderer>().sharedMaterial = asphalt;
            }
            // Road strip along z = 0..2 cells in front of both shells, if road pieces exist.
            for (int i = 0; i < 8; i++)
                PlaceAny("SM_Env_Road_", 0, CellBase(i * 2 + 1, 0, 0.4f), 0f);
        }

        static void BuildIntactShell(int cellX, int cellZ, int cellsX, int cellsZ, int storeys)
        {
            int stairCellX = cellX + cellsX - 1, stairCellZ = cellZ + 1;
            for (int s = 0; s < storeys; s++)
            {
                // Perimeter walls. South face gets the door (ground floor) and windows.
                for (int x = 0; x < cellsX; x++)
                {
                    string south = s == 0 && x == 1 ? "SM_Bld_Base_Wall_Door_01"
                                 : x % 2 == 0 ? "SM_Bld_Base_Wall_01" : "SM_Bld_Base_Wall_Window_01";
                    Place(south, CellBase(cellX + x + 0.5f, s, cellZ), 0f);
                    string north = x % 2 == 1 ? "SM_Bld_Base_Wall_01" : "SM_Bld_Base_Wall_Window_01";
                    Place(north, CellBase(cellX + x + 0.5f, s, cellZ + cellsZ), 180f);
                }
                for (int z = 0; z < cellsZ; z++)
                {
                    Place(z == 1 ? "SM_Bld_Base_Wall_Window_01" : "SM_Bld_Base_Wall_01",
                        CellBase(cellX, s, cellZ + z + 0.5f), 270f);
                    Place("SM_Bld_Base_Wall_01", CellBase(cellX + cellsX, s, cellZ + z + 0.5f), 90f);
                }
                // Corner pillars mask the wall joins.
                Place("SM_Bld_Base_Pillar_01", CellBase(cellX, s, cellZ));
                Place("SM_Bld_Base_Pillar_01", CellBase(cellX + cellsX, s, cellZ));
                Place("SM_Bld_Base_Pillar_01", CellBase(cellX, s, cellZ + cellsZ));
                Place("SM_Bld_Base_Pillar_01", CellBase(cellX + cellsX, s, cellZ + cellsZ));

                // Floor slabs: every cell, except the stairwell opening above the stair.
                for (int x = 0; x < cellsX; x++)
                    for (int z = 0; z < cellsZ; z++)
                    {
                        bool stairwell = s > 0 && cellX + x == stairCellX && cellZ + z == stairCellZ;
                        if (stairwell) continue;
                        PlaceFloor(cellX + x, s, cellZ + z);
                    }
                // Full-height stair, alternating direction per storey. Raw pivot placement:
                // the flight carries a skirt below its pivot that bounds-snapping would misalign.
                if (s < storeys - 1)
                    Place("SM_Bld_Base_Stairs_02",
                        CellBase(stairCellX + 0.5f, s, stairCellZ + 0.5f), s % 2 == 0 ? 0f : 180f, snap: false);
            }
            // Flat roof: ceiling slabs one layer above the top storey.
            for (int x = 0; x < cellsX; x++)
                for (int z = 0; z < cellsZ; z++)
                    Place("SM_Bld_Base_Ceiling_01", CellBase(cellX + x + 0.5f, storeys, cellZ + z + 0.5f));
        }

        static void PlaceFloor(int cellX, int layer, int cellZ)
        {
            Vector3 at = CellBase(cellX + 0.5f, layer, cellZ + 0.5f);
            // First name that exists wins; packs differ on which floor slab they ship.
            foreach (string candidate in new[]
                     { "SM_Bld_Base_Floor_01", "SM_Bld_Base_45_Floor_Combined_01", "SM_Bld_Base_Ceiling_01" })
            {
                if (Load(candidate, 0, exact: true) != null) { Place(candidate, at); return; }
            }
        }

        static void BuildRuinedShell(int cellX, int cellZ, int cellsX, int cellsZ)
        {
            // Ground storey: broken perimeter — destroyed pieces, gaps where walls fell.
            for (int x = 0; x < cellsX; x++)
            {
                string?[] south = { "SM_Bld_Base_Wall_01", "SM_Bld_Base_Wall_Destroyed_01", null, "SM_Bld_Base_Wall_Window_01" };
                if (south[x % 4] != null) Place(south[x % 4]!, CellBase(cellX + x + 0.5f, 0, cellZ), 0f);
                string?[] north = { "SM_Bld_Base_Wall_Destroyed_02", "SM_Bld_Base_Wall_01", "SM_Bld_Base_Wall_Destroyed_01", null };
                if (north[x % 4] != null) Place(north[x % 4]!, CellBase(cellX + x + 0.5f, 0, cellZ + cellsZ), 180f);
            }
            for (int z = 0; z < cellsZ; z++)
            {
                Place("SM_Bld_Base_Wall_01", CellBase(cellX, 0, cellZ + z + 0.5f), 270f);
                if (z != 1) Place("SM_Bld_Base_Wall_Destroyed_01", CellBase(cellX + cellsX, 0, cellZ + z + 0.5f), 90f);
            }
            for (int x = 0; x < cellsX; x++)
                for (int z = 0; z < cellsZ; z++)
                    PlaceFloor(cellX + x, 0, cellZ + z);

            // First storey: half-collapsed — sparse floor, fragmentary walls, no roof.
            for (int x = 0; x < cellsX; x++)
                for (int z = 0; z < cellsZ; z++)
                    if ((x + z) % 2 == 0) PlaceFloor(cellX + x, 1, cellZ + z);
            Place("SM_Bld_Base_Wall_Destroyed_01", CellBase(cellX + 0.5f, 1, cellZ), 0f);
            Place("SM_Bld_Base_Wall_Destroyed_02", CellBase(cellX + 1.5f, 1, cellZ), 0f);
            Place("SM_Bld_Base_Wall_01", CellBase(cellX, 1, cellZ + 0.5f), 270f);
            Place("SM_Bld_Base_Wall_Destroyed_02", CellBase(cellX, 1, cellZ + 1.5f), 270f);

            // Rubble spilling inward and onto the street; fire and smoke on the broken storey.
            // Names verified against synty-inventory.csv: the packs ship
            // SM_Prop_Sidewalk_Rubble_01 and SM_Prop_Ute_Wreck_01, not a generic rubble family.
            // The earlier guesses (SM_Env_Rubble / SM_Prop_Rubble) matched nothing.
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = CellBase(cellX + 0.8f * i + 0.6f, 0, cellZ - 0.6f + (i % 2) * 1.1f);
                if (PlaceAny("SM_Prop_Sidewalk_Rubble", i, at, i * 63f) == null)
                    PlaceAny("SM_Prop_Ute_Wreck", i, at, i * 63f);
            }
            Vector3 fireAt = CellBase(cellX + 2.5f, 1, cellZ + 1.5f);
            PlaceAny("FX_Fire_0", 0, fireAt, 0f, snap: false);
            PlaceAny("FX_Smoke_Black", 0, fireAt + Vector3.up * 1.0f, 0f, snap: false);
        }

        static void BuildBackdrop()
        {
            // A mid-rise assembled from 5 m facade Sections (2 × 2 cells each), three storeys.
            string[] sections = { "SM_Bld_Section_Wall_03", "SM_Bld_Section_Window_01", "SM_Bld_Section_Window_03" };
            for (int s = 0; s < 3; s++)
                for (int i = 0; i < 3; i++)
                    Place(sections[(s + i) % sections.Length], CellBase(i * 2 + 3, s, 9), 0f);
            // Distant towers to close the skyline.
            PlaceAny("SM_Bld_Background_", 2, CellBase(2, 0, 16), 10f);
            PlaceAny("SM_Bld_Background_", 4, CellBase(11, 0, 18), -15f);
        }

        static void BuildStreetLife()
        {
            PlaceAny("SM_Veh_", 0, CellBase(3.5f, 0, -1.2f), 8f);
            PlaceAny("SM_Veh_", 5, CellBase(9.5f, 0, -0.8f), 184f);
            for (int i = 0; i < 3; i++)
                PlaceAny("SM_Prop_Crate", i, CellBase(5.6f + 0.5f * i, 0, 1.2f), i * 40f);
            PlaceAny("SM_Prop_Barrier", 0, CellBase(6.6f, 0, -1.5f), 95f);
            PlaceAny("SM_Prop_Dumpster", 0, CellBase(11.8f, 0, 1.4f), 12f);
            PlaceAny("SM_Sign_", 3, CellBase(0.6f, 0, 1.4f), 25f);

            // Posed characters for scale (static meshes, no animator).
            PlaceAny("SM_Chr_", 2, CellBase(2.2f, 0, 0.8f), 140f);
            PlaceAny("SM_Chr_", 7, CellBase(2.6f, 0, 1.1f), 320f);
            PlaceAny("SM_Chr_", 11, CellBase(8.4f, 0, 0.6f), 220f);
        }

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(_root, false);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            go.AddComponent<AudioListener>();
            // Colony-sim vantage: high, pulled back, looking down the street at both shells.
            Vector3 focus = new Vector3(14f, 2.5f, 5f);
            go.transform.position = focus + new Vector3(9f, 15.5f, -17f);
            go.transform.LookAt(focus);
        }
    }
}
