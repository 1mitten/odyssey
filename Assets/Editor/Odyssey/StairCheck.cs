#nullable enable
using System;
using System.Text;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// What a built stair actually looks like, because nobody has ever seen one.
    ///
    /// <para><b>Written for the owner's report of 2026-09-21</b> — <i>"I put one stairs to build -
    /// and it built two"</i>. A stair is two cells by design and finishes as two edifice records,
    /// so the simulation answering "two" is correct and says nothing about what is drawn. The
    /// question this settles is whether the two half-flights read as <em>one</em> continuous
    /// flight: <c>SM_Bld_Base_Stairs_01</c> rises 1.50 m over a 2.5 m run
    /// (<c>docs/research/e-01-module-mapping.md</c>), so the upper half lifted by half a layer
    /// should carry on exactly where the lower one stops. If it does not, the seam is what the
    /// owner saw.
    /// </para>
    ///
    /// <para>The side elevation is the load-bearing picture. A three-quarter view cannot answer a
    /// question about a profile along one line — which is the same reason <c>PlayScene.Shoot</c>
    /// grew a bearing argument in the first place.</para>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.StairCheck.Run</c>.</para>
    /// </summary>
    public static class StairCheck
    {
        [MenuItem("Odyssey/Presentation/Check a stair")]
        public static void RunFromMenu() => Execute(false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            Action<ScriptableRenderContext, Camera>? hook = null;
            float amplitudeWas = GroundRelief.Amplitude;

            try
            {
                // Flat, deliberately: the question is the seam between two pieces, and relief
                // would drape each half by a different amount and confuse the answer.
                GroundRelief.Amplitude = 0f;

                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                if (catalogue == null)
                {
                    Debug.LogError("[Stair] no catalogue; nothing to draw.");
                    exitCode = 1;
                    return;
                }

                var size = new GridSize(48, 48, 12);
                ScenarioDef scenario = ScenarioDef.Bare();
                scenario.colonists = 0;
                scenario.beds = 0;
                scenario.startingFellRadius = 0;
                ColonyWorld colony = ColonyWorld.Build(size, 1u, scenario, barren: true, wooded: false);

                CellRef start = colony.Start;
                int head = size.Index(start.X, start.Z, start.Y);
                const int facing = 1;              // +X, so the far half is the next cell along X
                int second = head + 1;

                IntentRejection order = colony.Construction.Place(
                    size.FromIndex(head), BuildingHandle.Stair, StuffHandle.Wood, facing);
                if (order != IntentRejection.None)
                {
                    Debug.LogError($"[Stair] the order was refused: {order}.");
                    exitCode = 1;
                    return;
                }

                colony.Construction.Raise(colony.Pawns, head);
                colony.World.Tick();

                var chunks = new ChunkGrid(size);
                var library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(colony.Grid, colony.Outcome.Edifices);

                var report = new StringBuilder();
                report.AppendLine("[Stair] one order, raised, and what came of it:");
                report.AppendLine($"  order          {order}");
                report.AppendLine($"  lower half     {size.FromIndex(head)} def={model.EdificeDef(head)} "
                                  + $"stand={model.StandHeight(head):0.000} m mark={model.MarkHeight(head):0.000} m");
                report.AppendLine($"  upper half     {size.FromIndex(second)} def={model.EdificeDef(second)} "
                                  + $"stand={model.StandHeight(second):0.000} m mark={model.MarkHeight(second):0.000} m");
                report.AppendLine($"  expected defs  lower={CoreContent.EdificeStairLower} "
                                  + $"upper={CoreContent.EdificeStairUpper}");
                report.AppendLine($"  StairShape     rise={StairShape.Rise:0.000} m per cell, "
                                  + $"layer={CellMetrics.SizeY:0.000} m, cell={CellMetrics.SizeXZ:0.000} m");

                // The art's own geometry, measured rather than taken from the research note.
                foreach (ModuleEntry row in catalogue.FindFamily(ModuleIds.Stair))
                {
                    if (row.prefab == null) continue;
                    var probe = UnityEngine.Object.Instantiate(row.prefab);
                    try
                    {
                        Bounds bounds = default;
                        bool any = false;
                        foreach (MeshFilter filter in probe.GetComponentsInChildren<MeshFilter>(true))
                        {
                            if (filter.sharedMesh == null) continue;
                            Bounds b = filter.sharedMesh.bounds;
                            b.center = filter.transform.localPosition + b.center;
                            if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
                        }
                        report.AppendLine(any
                            ? $"  {row.prefabName,-28} size={bounds.size.x:0.00} x {bounds.size.y:0.00} "
                              + $"x {bounds.size.z:0.00}  y in [{bounds.min.y:0.00}, {bounds.max.y:0.00}]"
                            : $"  {row.prefabName,-28} no mesh to measure");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(probe); }
                    break;
                }

                Debug.Log(report.ToString());

                lightingRoot = new GameObject("StairRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                var renderer = new ChunkRenderer(model);
                int activeLayer = start.Y;
                var slice = new SliceSettings();

                cameraObject = new GameObject("StairCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    renderer.ViewerPosition = rendering.transform.position;

                    // **A probe gets one frame, and a frame meshes eleven chunks.**
                    // MeshBudgetPerFrame landed on main on 2026-09-21 and it is right for the
                    // game: a deferred chunk keeps its old geometry and comes back next frame.
                    // A photograph has no next frame. PlayScene.Shoot calls camera.Render()
                    // exactly once, so without this the first shot drew eleven chunks of board
                    // and no stair at all, and each later shot in the same run drew eleven more
                    // — which reads as a framing mistake rather than as a stale mesh, and is why
                    // it is written down rather than merely fixed (docs/lessons.md).
                    //
                    // PrimeAll is the one unbudgeted walk and is idempotent: a chunk that is not
                    // stale costs nothing, so paying it every frame of a four-shot probe is one
                    // board's meshing in total.
                    renderer.PrimeAll(activeLayer, slice);
                    renderer.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                // The seam between the two cells, at the height the flight passes through it.
                Vector3 seam = CellMetrics.FloorCentre(start.X, start.Z, start.Y)
                               + new Vector3(CellMetrics.HalfXZ, StairShape.Rise, 0f);

                PlayScene.Shoot(camera, seam, 48f, 45f, 12f, "Logs/stair-play.png");

                // Along +Z, so the climb runs left to right across the frame and the profile of
                // both pieces is unambiguous. This is the picture that answers the report.
                PlayScene.Shoot(camera, seam, 4f, 0f, 11f, "Logs/stair-side.png");
                PlayScene.Shoot(camera, seam, 20f, 0f, 11f, "Logs/stair-side-high.png");
                PlayScene.Shoot(camera, seam, 48f, 225f, 12f, "Logs/stair-play-behind.png");

                Debug.Log("[Stair] wrote Logs/stair-side.png (the profile), stair-side-high.png, "
                          + "stair-play.png and stair-play-behind.png.");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Stair] {error}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                GroundRelief.Amplitude = amplitudeWas;
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
