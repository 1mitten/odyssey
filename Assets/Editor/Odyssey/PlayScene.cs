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
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Odyssey.EditorTools
{
    public static class PlayScene
    {
        const string ScenePath = "Assets/Scenes/Play.unity";
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>
        /// The world the play scene is built with, and the world "Measure a slice" measures. One
        /// pair of constants so the two cannot drift apart again: a benchmark of a map the game
        /// does not load is worse than no benchmark, because it still produces a number.
        /// </summary>
        const int PlaySizeXZ = 120;
        const int PlayLayers = 16;

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

                // Deliberately the world the play scene builds, not a convenient small one. A
                // performance number for a map nobody loads answers no question worth asking, and
                // this measurement had quietly gone on describing a 60 x 60 city long after the
                // scene moved to a barren 120 x 120 wilderness.
                var size = new GridSize(PlaySizeXZ, PlaySizeXZ, PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeBarren();
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);

                var clock = System.Diagnostics.Stopwatch.StartNew();
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                double genMs = clock.Elapsed.TotalMilliseconds;

                var library = new ModuleLibrary(catalogue);
                var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);

                // The layer the scene actually opens on: the air cell a colonist stands in, which
                // is one above the ground. Measuring the layer below it would quietly report the
                // cost of a slice the player never sees.
                int activeLayer = result.StartCell.Y;
                var renderer = new ChunkRenderer(model) { SubmitToGpu = false };
                var slice = new SliceSettings();

                clock.Restart();
                renderer.Render(activeLayer, slice);
                double firstMs = clock.Elapsed.TotalMilliseconds;
                int meshed = renderer.ChunksMeshedThisFrame;

                clock.Restart();
                for (int i = 0; i < 100; i++) renderer.Render(activeLayer, slice);
                double steadyMs = clock.Elapsed.TotalMilliseconds / 100d;

                // A full re-mesh once everything is warm: the honest cost of rebuilding every
                // chunk of a slice, against the 4 ms budget in 06-rendering-and-camera.md section 4.
                model.RefreshAll(grid, result.Natural!.Context.Edifices);
                clock.Restart();
                renderer.Render(activeLayer, slice);
                double remeshMs = clock.Elapsed.TotalMilliseconds;
                int remeshed = renderer.ChunksMeshedThisFrame;

                var report = new System.Text.StringBuilder();
                report.AppendLine($"[Measure] {size} seed 1: worldgen {genMs:0.0} ms, {result.Natural!.Report}");
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

            // A cell-shaped box wearing a tiling terrain texture. See the note above the natural
            // terrain rows for why this is the one kind of pack material a box may wear.
            void Ground(string id, string material) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.SolidBlock, prefabName = string.Empty,
                materialName = material,
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
            Ground(ModuleIds.Terrain("Rock"), "Mat_Rock_01");
            Block(ModuleIds.Terrain("BuriedSeam"));
            Block(ModuleIds.Terrain("Salvage"));

            // ---- natural wilderness (ADR 0008) --------------------------------------------
            // Ground and strata stay tinted blocks for the same reason the city strata do: the
            // Nature Biomes pack has no cubic ground module. Its "grass planes" measure
            // 1.00 x h x 0.00 — they are standing grass cards for scattering, not tiles. The
            // colours that make these read as landscape are in StuffPalette.TerrainSolids.
            // Ground uses the square tile family, which measures exactly 5.00 x 5.00 — two cells
            // — so scaling by half gives a seamless one-cell tile with no gaps and no overlap.
            //
            // An earlier attempt used SM_Gen_Env_Ground_Grass_01. That is an organic patch with a
            // rounded outline, authored to be strewn across a landscape, and tiling it produced
            // circles across the map. Square art for a square grid; scatter art stays scatter.
            //
            // Two attempts at using pack art then failed, and the reason they failed is the
            // reason the current approach works. Pointing a cell at a flat ground-tile prefab drew
            // a thin plane inside every cell that z-fought with its neighbours: flicker and stray
            // shapes. Taking only that prefab's material failed worse: Synty *props* are UV-mapped
            // into a shared colour atlas, so the material on a cell-sized box samples the entire
            // atlas across each face rather than the grass swatch, which is exactly where the
            // stray blades and the dark patches came from.
            //
            // The Nature Biomes pack also ships **terrain** materials, which are a different kind
            // of asset altogether: ordinary tiling textures with no atlas, authored for Unity
            // terrain layers. One of those worn by the cell-shaped box is what ground should have
            // been all along. Each face carries one unit of UV, the texture tiles twice across it,
            // and it is authored to wrap, so the pattern continues across a cell boundary instead
            // of restarting. The result reads as a field rather than a grid of stamps.
            //
            // A clone without the packs resolves these to null and gets the flat tints in
            // StuffPalette.TerrainSolids, which is still a perfectly legible landscape.
            Ground(ModuleIds.Terrain("Grass"), "Mat_Grass_Textures_01");
            Ground(ModuleIds.Terrain("BareEarth"), "Mat_Dirt_01");
            Ground(ModuleIds.Terrain("PackedGravel"), "Mat_Gravel_01");
            Ground(ModuleIds.Terrain("Subsoil"), "Mat_Mud_01");
            Ground(ModuleIds.Terrain("Bedrock"), "Mat_Rock_Rough_01");

            // No meadow texture reads as these, and a wrong texture is worse than an honest
            // colour: sand would come out as mud, and an ore seam has to stay findable at a
            // glance. They keep their tints until a pack with the right ground arrives.
            Block(ModuleIds.Terrain("Sand"));
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

            var materials = new Dictionary<string, Material?>(StringComparer.Ordinal);
            foreach (ModuleEntry row in rows)
            {
                if (string.IsNullOrEmpty(row.materialName)) continue;
                if (!materials.TryGetValue(row.materialName, out Material? material))
                {
                    material = FindSyntyMaterial(row.materialName);
                    materials[row.materialName] = material;
                }
                row.material = material;
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

        /// <summary>Exact-name material lookup under Assets/Synty. Absent packs give null.</summary>
        static Material? FindSyntyMaterial(string exactName)
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/Synty"))) return null;
            string[] guids = AssetDatabase.FindAssets($"{exactName} t:Material", new[] { "Assets/Synty" });
            string? path = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), exactName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
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
            boot.sizeX = PlaySizeXZ;
            boot.sizeZ = PlaySizeXZ;
            boot.layers = PlayLayers;
            boot.seed = 1;
            boot.moduleCatalogue = catalogue;
            boot.cameraRig = rig;
        }
    }
}
