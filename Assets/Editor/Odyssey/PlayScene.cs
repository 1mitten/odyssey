#nullable enable
// Generates the playable scene and the module catalogue it reads.
//
// Scenes in this project are generated, never hand-authored, so that they are reproducible and
// reviewable as code. This one is the M1 look-and-fly scene: a camera rig, a light, and the
// bootstrap that generates a world on Play and draws it.
//
// Editor menu:  Odyssey > Presentation > Build play scene
//               Odyssey > Presentation > Rebuild module catalogue
// Headless:     scripts/unity.sh exec Odyssey.EditorTools.PlayScene.Build
//               scripts/unity.sh exec Odyssey.EditorTools.PlayScene.RebuildCatalogue
//
// The catalogue maps module id strings to Synty prefabs by *name*, resolved here against
// Assets/Synty. A clone without the licensed packs generates the same catalogue with every prefab
// reference empty, and the renderer draws primitives instead: the scene still builds, still runs
// and still flies around.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Odyssey.EditorTools
{
    public static class PlayScene
    {
        const string ScenePath = "Assets/Scenes/Play.unity";
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        [MenuItem("Odyssey/Presentation/Build play scene")]
        public static void BuildFromMenu() => BuildInternal(exitWhenDone: false);

        /// <summary>Batchmode entry point. Exits the editor with 0 on success, 1 on failure.</summary>
        public static void Build() => BuildInternal(Application.isBatchMode);

        [MenuItem("Odyssey/Presentation/Rebuild module catalogue")]
        public static void RebuildCatalogueFromMenu() => RebuildInternal(exitWhenDone: false);

        public static void RebuildCatalogue() => RebuildInternal(Application.isBatchMode);

        [MenuItem("Odyssey/Presentation/Measure a slice")]
        public static void MeasureFromMenu() => MeasureInternal(exitWhenDone: false);

        /// <summary>
        /// Build the world the play scene builds, mesh a slice and report what it costs to submit.
        ///
        /// Headless and with no graphics device, so it runs in CI: the renderer is put in dry-run
        /// mode, which does everything except hand the buckets to the GPU. The draw-call and
        /// instance counts are therefore the real ones; frame time is not measured here, because
        /// a number for that without a GPU would be a fiction.
        /// </summary>
        public static void Measure() => MeasureInternal(Application.isBatchMode);

        static void MeasureInternal(bool exitWhenDone)
        {
            int exitCode = 0;
            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
                var size = new GridSize(60, 60, 5);
                var gen = MapGenDef.For(size);
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);

                var clock = System.Diagnostics.Stopwatch.StartNew();
                WorldGenResult result = WorldGenerator.Generate(grid, 1u, gen);
                double genMs = clock.Elapsed.TotalMilliseconds;

                var library = new ModuleLibrary(catalogue);
                var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                model.ApplyTemplates(result, gen);
                model.RefreshAll(grid, result.Context.Edifices);

                var renderer = new ChunkRenderer(model) { SubmitToGpu = false };
                var slice = new SliceSettings();

                clock.Restart();
                renderer.Render(gen.groundLayer, slice);
                double firstMs = clock.Elapsed.TotalMilliseconds;
                int meshed = renderer.ChunksMeshedThisFrame;

                clock.Restart();
                for (int i = 0; i < 100; i++) renderer.Render(gen.groundLayer, slice);
                double steadyMs = clock.Elapsed.TotalMilliseconds / 100d;

                // A full re-mesh once everything is warm: the honest cost of rebuilding every
                // chunk of a slice, against the 4 ms budget in 06-rendering-and-camera.md section 4.
                model.RefreshAll(grid, result.Context.Edifices);
                clock.Restart();
                renderer.Render(gen.groundLayer, slice);
                double remeshMs = clock.Elapsed.TotalMilliseconds;
                int remeshed = renderer.ChunksMeshedThisFrame;

                var report = new System.Text.StringBuilder();
                report.AppendLine($"[Measure] {size} seed 1: worldgen {genMs:0.0} ms, {result.Report}");
                report.AppendLine(
                    $"[Measure] first slice: {firstMs:0.00} ms including {meshed} chunk meshes; " +
                    $"steady submit {steadyMs:0.00} ms/frame; " +
                    $"warm full re-mesh of {remeshed} chunks {remeshMs:0.00} ms " +
                    $"({(remeshed > 0 ? remeshMs / remeshed : 0d):0.000} ms/chunk)");
                report.AppendLine(
                    $"[Measure] draw calls {renderer.DrawCalls}, instances {renderer.InstancesDrawn}, " +
                    $"chunks drawn {renderer.ChunksDrawn}, materials {renderer.MaterialCount}, " +
                    $"modules with art {library.ArtBackedCount()}/{library.Count - 1}");

                for (int layer = 0; layer < size.SizeY; layer++)
                {
                    renderer.Render(layer, slice);
                    report.AppendLine(
                        $"[Measure] slice at layer {layer}: {renderer.DrawCalls} calls, " +
                        $"{renderer.InstancesDrawn} instances, {renderer.ChunksDrawn} chunks");
                }

                renderer.Dispose();
                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[Measure] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static void BuildInternal(bool exitWhenDone)
        {
            int exitCode = 0;
            try
            {
                ModuleCatalogue catalogue = BuildCatalogueAsset();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Odyssey").transform;

                BuildLighting(root);
                SliceCameraRig rig = BuildCamera(root);
                BuildBootstrap(root, rig, catalogue);

                Directory.CreateDirectory(Path.GetFullPath("Assets/Scenes"));
                AssetDatabase.Refresh();
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[PlayScene] saved {ScenePath}. " +
                          $"Catalogue: {catalogue.ResolvedPrefabCount()}/{catalogue.Entries.Count} rows have art.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayScene] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static void RebuildInternal(bool exitWhenDone)
        {
            int exitCode = 0;
            try
            {
                ModuleCatalogue catalogue = BuildCatalogueAsset();
                var missing = catalogue.Entries
                    .Where(e => e.prefab == null && !string.IsNullOrEmpty(e.prefabName))
                    .Select(e => e.prefabName)
                    .Distinct()
                    .ToList();
                Debug.Log($"[PlayScene] catalogue rebuilt at {CataloguePath}: " +
                          $"{catalogue.ResolvedPrefabCount()}/{catalogue.Entries.Count} rows have art" +
                          (missing.Count == 0 ? "." : $"; unresolved prefabs: {string.Join(", ", missing)}."));
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayScene] catalogue rebuild failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        // ------------------------------------------------------------ catalogue

        /// <summary>
        /// The module table.
        ///
        /// Sources: <c>e-01-module-mapping.md</c> for which Synty piece plays which part, and
        /// <c>synty-inventory.csv</c> for the measurements that justify each one. The whole
        /// buildable kit is PolygonGeneric's <c>SM_Bld_Base_*</c> family, built on our exact pitch:
        /// walls 2.50 x 3.01, floors 2.50 x 2.50, a stair rising 1.50 m per 2.5 m run.
        ///
        /// Rows with an empty prefab name are deliberate, not gaps: underground strata are drawn
        /// as tinted blocks because no pack ships a cubic rock module, and a tinted block is what
        /// a cut-away of bedrock should look like anyway.
        /// </summary>
        static List<ModuleEntry> Rows()
        {
            var rows = new List<ModuleEntry>();

            void Wall(string id, string prefab) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.WallPanel, prefabName = prefab,
                centreXZ = true, baseAtY = true,
            });

            void Slab(string id, string prefab) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.FloorSlab, prefabName = prefab,
                // Pivot placement, not bounds placement: the walking surface is the cell floor and
                // the slab's own thickness hangs below it. Snapping the bounds instead would lift
                // every floor in the world by its own thickness.
                centreXZ = true, baseAtY = false,
            });

            void Block(string id) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.SolidBlock, prefabName = string.Empty,
            });

            // Walls, windows and doors. Several template ids share one piece for now; the point of
            // the catalogue is that giving the tower its own curtain wall is an edit here.
            Wall(ModuleIds.Wall, "SM_Bld_Base_Wall_01");
            Wall("odyssey.module.wall.panel", "SM_Bld_Base_Wall_01");
            Wall("odyssey.module.wall.block", "SM_Bld_Base_Wall_01");
            Wall("odyssey.module.wall.curtain", "SM_Bld_Base_Wall_Window_Double_01");
            Wall(ModuleIds.Window, "SM_Bld_Base_Wall_Window_01");
            Wall("odyssey.module.wall.window", "SM_Bld_Base_Wall_Window_01");
            Wall("odyssey.module.wall.shopfront", "SM_Bld_Base_Wall_Window_Double_01");
            Wall("odyssey.module.wall.glazed", "SM_Bld_Base_Wall_Window_Double_01");
            Wall(ModuleIds.VaultWall, "SM_Bld_Base_Wall_01");

            Wall(ModuleIds.Door, "SM_Bld_Base_Wall_Door_01");
            Wall("odyssey.module.door.single", "SM_Bld_Base_Wall_Door_01");
            Wall("odyssey.module.door.double", "SM_Bld_Base_Wall_Door_Double_01");
            Wall("odyssey.module.door.lobby", "SM_Bld_Base_Wall_Door_Double_Large_01");

            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.Pillar, shape = ModuleShape.Pillar,
                prefabName = "SM_Bld_Base_Pillar_01",
            });

            // Half a flight per cell: 1.50 m of rise over a 2.5 m run, so two cells climb one
            // 3.0 m layer, which is the rule the templates are authored to. The piece carries a
            // 0.33 m skirt below its pivot that must sink into the slab, so no base snapping.
            foreach (string id in new[] { ModuleIds.Stair, "odyssey.module.stair.straight", "odyssey.module.stair.core" })
                rows.Add(new ModuleEntry
                {
                    moduleId = id, shape = ModuleShape.StairFlight, prefabName = "SM_Bld_Base_Stairs_01",
                    centreXZ = true, baseAtY = false,
                });

            // Exactly one layer tall, pushed back against the wall it is fixed to.
            foreach (string id in new[] { ModuleIds.Ladder, "odyssey.module.ladder.fixed" })
                rows.Add(new ModuleEntry
                {
                    moduleId = id, shape = ModuleShape.Ladder, prefabName = "SM_Gen_Bld_Ladder_01",
                    centreXZ = true, baseAtY = true, offset = new Vector3(0f, 0f, -1.15f),
                });

            Slab(ModuleIds.Slab, "SM_Bld_Base_Floor_Combined_01");
            Slab("odyssey.module.slab.concrete", "SM_Bld_Base_Floor_Combined_01");
            Slab("odyssey.module.slab.deck", "SM_Bld_Base_Floor_Combined_01");

            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.UtilityTap, shape = ModuleShape.Pillar, prefabName = string.Empty,
            });

            // Street surfaces: the half tiles are exactly one cell square.
            Slab(ModuleIds.Terrain("Pavement"), "SM_Env_Ground_Tile_Half_01");
            Slab(ModuleIds.Terrain("CrackedPavement"), "SM_Env_Ground_Tile_Half_02");
            Slab(ModuleIds.Terrain("Rubble"), "SM_Env_Ground_Tile_Half_03");
            Slab(ModuleIds.Terrain("Soil"), "SM_Env_Ground_Tile_Half_04");
            Slab(ModuleIds.Terrain("Gravel"), "SM_Env_Ground_Tile_Half_05");

            // Strata. Tinted blocks on purpose: no pack has a cubic rock module, and a cut-away of
            // bedrock is a coloured mass, not a prop.
            Block(ModuleIds.Terrain("EngineeredFill"));
            Block(ModuleIds.Terrain("Rock"));
            Block(ModuleIds.Terrain("BuriedSeam"));
            Block(ModuleIds.Terrain("Salvage"));

            // ---- natural wilderness (ADR 0008) --------------------------------------------
            // Ground and strata stay tinted blocks for the same reason the city strata do: the
            // Nature Biomes pack has no cubic ground module. Its "grass planes" measure
            // 1.00 x h x 0.00 — they are standing grass cards for scattering, not tiles. The
            // colours that make these read as landscape are in StuffPalette.TerrainSolids.
            Block(ModuleIds.Terrain("Grass"));
            Block(ModuleIds.Terrain("BareEarth"));
            Block(ModuleIds.Terrain("PackedGravel"));
            Block(ModuleIds.Terrain("Sand"));
            Block(ModuleIds.Terrain("Subsoil"));
            Block(ModuleIds.Terrain("Bedrock"));
            Block(ModuleIds.Terrain("IronOre"));
            Block(ModuleIds.Terrain("CoalSeam"));

            // Trees are the pieces that actually make this look like a place. Measured widths
            // decide the casting: the pines are 1.78–2.12 m and sit inside a 2.5 m cell, while the
            // broadleaf trees run 2.74–4.32 m. Tree_03 at 2.74 m is the closest fit, and a little
            // overspill between neighbouring trees reads as canopy rather than as error.
            rows.Add(new ModuleEntry
            {
                moduleId = "odyssey.module.tree.conifer", shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Env_Tree_Pine_01", centreXZ = true, baseAtY = true,
            });
            rows.Add(new ModuleEntry
            {
                moduleId = "odyssey.module.tree.broadleaf", shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Env_Tree_03", centreXZ = true, baseAtY = true,
            });

            return rows;
        }

        static ModuleCatalogue BuildCatalogueAsset()
        {
            var rows = Rows();
            var cache = new Dictionary<string, GameObject?>(StringComparer.Ordinal);
            foreach (ModuleEntry row in rows)
            {
                if (string.IsNullOrEmpty(row.prefabName)) continue;
                if (!cache.TryGetValue(row.prefabName, out GameObject? prefab))
                {
                    prefab = FindSyntyPrefab(row.prefabName);
                    cache[row.prefabName] = prefab;
                }
                row.prefab = prefab;
            }

            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<ModuleCatalogue>();
                Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(CataloguePath)!));
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }
            catalogue.SetEntries(rows);
            EditorUtility.SetDirty(catalogue);
            AssetDatabase.SaveAssets();
            return catalogue;
        }

        /// <summary>Exact-name lookup under Assets/Synty. Absent packs give null, which is fine.</summary>
        static GameObject? FindSyntyPrefab(string exactName)
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/Synty"))) return null;
            string[] guids = AssetDatabase.FindAssets($"{exactName} t:Prefab", new[] { "Assets/Synty" });
            string? path = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), exactName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ---------------------------------------------------------------- scene

        static void BuildLighting(Transform root)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1.0f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft;
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.Euler(50f, 35f, 0f);

            // Flat-lit low-poly, as the concept renders are: a strong key, a generous cool ambient
            // so nothing goes black, and no post stylisation. Cel shading was raised and rejected
            // on 2026-09-15 (06-rendering-and-camera.md section 1).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.47f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.36f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.18f, 0.20f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.30f, 0.34f, 0.40f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 400f;
        }

        static SliceCameraRig BuildCamera(Transform root)
        {
            var go = new GameObject("Slice Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(root, false);

            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            go.AddComponent<AudioListener>();

            return go.AddComponent<SliceCameraRig>();
        }

        static void BuildBootstrap(Transform root, SliceCameraRig rig, ModuleCatalogue catalogue)
        {
            var go = new GameObject("Bootstrap");
            go.transform.SetParent(root, false);
            var boot = go.AddComponent<OdysseyBootstrap>();
            go.AddComponent<SelectionReadout>();   // click a colonist to see what they are doing
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.layers = 5;
            boot.seed = 1;
            boot.moduleCatalogue = catalogue;
            boot.cameraRig = rig;
        }
    }
}
