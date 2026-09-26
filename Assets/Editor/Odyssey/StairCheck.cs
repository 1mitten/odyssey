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
                // **Facing 0, +Z — north, and the facing a player gets before they press R.** One
                // cell since 2026-09-21, so this is the direction the flight CLIMBS rather than the
                // direction a far half lay in, and the profile shots below look along +Z for it.
                // The cell the flight climbs toward is left empty on purpose: it is what the
                // pictures are checked against — a flight that spills into it is a flight drawn at
                // the wrong scale, and one that leans into it is one yawed wrong.
                const int facing = 0;
                int beyond = head + size.SizeX;

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
                report.AppendLine($"  the stair      {size.FromIndex(head)} def={model.EdificeDef(head)} "
                                  + $"facing={model.EdificeFacing(head)} "
                                  + $"stand={model.StandHeight(head):0.000} m mark={model.MarkHeight(head):0.000} m");
                report.AppendLine($"  the cell along {size.FromIndex(beyond)} def={model.EdificeDef(beyond)} "
                                  + "(0 = empty, which is the point: one cell)");
                report.AppendLine($"  expected def   {CoreContent.EdificeStairFull} "
                                  + $"(worldgen's halves are still {CoreContent.EdificeStairLower} "
                                  + $"and {CoreContent.EdificeStairUpper})");
                report.AppendLine($"  StairShape     full={StairShape.FullRise:0.000} m in one cell, "
                                  + $"half-flight={StairShape.Rise:0.000} m, "
                                  + $"layer={CellMetrics.SizeY:0.000} m, cell={CellMetrics.SizeXZ:0.000} m");
                report.AppendLine("  FLUSH means StandHeight == layer exactly, and the picture has to "
                                  + "agree with the number: a mesh yawed the wrong way reports the "
                                  + "same height and descends.");

                // The art's own geometry, measured rather than taken from the research note.
                foreach (ModuleEntry row in catalogue.FindFamily(ModuleIds.StairFull))
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

                        // **Which way the art climbs, measured off its own vertices.** §8b fixed
                        // SM_Bld_Base_Stairs_01 with `yaw = 180` because it ascends toward its local
                        // -Z; the one-cell stair inherited that number by family resemblance and was
                        // drawn DESCENDING into the ground (2026-09-21). A number copied from a
                        // sibling is an inference, and this is the measurement that replaces it.
                        //
                        // Mean vertex height in the low half of each axis against the high half. The
                        // axis with the larger split is the run; its sign says which end is the top,
                        // and the yaw that makes the flight climb toward world +Z follows from it.
                        if (any)
                        {
                            float lowX = 0f, highX = 0f, lowZ = 0f, highZ = 0f;
                            int nLowX = 0, nHighX = 0, nLowZ = 0, nHighZ = 0;
                            foreach (MeshFilter filter in probe.GetComponentsInChildren<MeshFilter>(true))
                            {
                                if (filter.sharedMesh == null) continue;
                                foreach (Vector3 v in filter.sharedMesh.vertices)
                                {
                                    Vector3 local = filter.transform.localPosition + v;
                                    if (local.x < bounds.center.x) { lowX += local.y; nLowX++; }
                                    else { highX += local.y; nHighX++; }
                                    if (local.z < bounds.center.z) { lowZ += local.y; nLowZ++; }
                                    else { highZ += local.y; nHighZ++; }
                                }
                            }

                            float dx = (nHighX > 0 ? highX / nHighX : 0f) - (nLowX > 0 ? lowX / nLowX : 0f);
                            float dz = (nHighZ > 0 ? highZ / nHighZ : 0f) - (nLowZ > 0 ? lowZ / nLowZ : 0f);
                            string run = Mathf.Abs(dx) >= Mathf.Abs(dz) ? "X" : "Z";
                            float along = Mathf.Abs(dx) >= Mathf.Abs(dz) ? dx : dz;

                            report.AppendLine(
                                $"  climb          runs along local {run}, toward "
                                + $"{(along > 0f ? "+" : "-")}{run} "
                                + $"(mean height rises {Mathf.Abs(along):0.000} m across it; "
                                + $"dx={dx:0.000} dz={dz:0.000})");
                            report.AppendLine(
                                "  yaw wanted     " + (run == "Z"
                                    ? (along > 0f ? "0 (already climbs toward +Z)" : "180")
                                    : (along > 0f ? "270 (climbs toward +X)" : "90")));
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(probe); }
                    break;
                }

                // **The walked surface, binned over the cell's own footprint.** A switchback is
                // not a ramp and cannot be drawn as one: a figure climbing it has to follow the
                // lower flight, turn on the landing and climb the upper one, with its feet on the
                // treads. That path has to come from the art, so this prints the art's top
                // surface as a grid and StairTread is authored against the numbers
                // (docs/design/60-stairs.md 11).
                {
                    var placed = library[model.EdificeModule(head)];
                    const int bins = 20;
                    var top = new float[bins, bins];
                    for (int a = 0; a < bins; a++)
                    for (int b = 0; b < bins; b++)
                        top[a, b] = float.NegativeInfinity;

                    foreach (ModulePart part in placed.Parts)
                    {
                        if (part.Mesh == null) continue;
                        foreach (Vector3 v in part.Mesh.vertices)
                        {
                            Vector3 q = part.Local.MultiplyPoint3x4(v);
                            int ix = Mathf.Clamp(
                                Mathf.FloorToInt((q.x + CellMetrics.HalfXZ) / CellMetrics.SizeXZ * bins), 0, bins - 1);
                            int iz = Mathf.Clamp(
                                Mathf.FloorToInt((q.z + CellMetrics.HalfXZ) / CellMetrics.SizeXZ * bins), 0, bins - 1);
                            if (q.y > top[ix, iz]) top[ix, iz] = q.y;
                        }
                    }

                    var grid = new StringBuilder();
                    grid.AppendLine("[Stair] the walked surface, metres above the cell floor.");
                    grid.AppendLine("  rows are +Z (north, the climb) DOWN the page; columns are +X to the right.");
                    for (int iz = bins - 1; iz >= 0; iz--)
                    {
                        grid.Append($"  z{iz,2} ");
                        for (int ix = 0; ix < bins; ix++)
                            grid.Append(float.IsNegativeInfinity(top[ix, iz]) ? "  .  " : $"{top[ix, iz],5:0.0}");
                        grid.AppendLine();
                    }

                    // The centre line of the climb: the highest surface in each +Z band, which is
                    // what a figure walking up the middle of the flight would tread on.
                    grid.AppendLine("  centre line (max over x, per z band):");
                    for (int iz = 0; iz < bins; iz++)
                    {
                        float best = float.NegativeInfinity;
                        int at = -1;
                        for (int ix = 0; ix < bins; ix++)
                            if (top[ix, iz] > best) { best = top[ix, iz]; at = ix; }
                        float zMid = (iz + 0.5f) / bins * CellMetrics.SizeXZ - CellMetrics.HalfXZ;
                        float xMid = (at + 0.5f) / bins * CellMetrics.SizeXZ - CellMetrics.HalfXZ;
                        grid.AppendLine($"    z={zMid,6:0.00}  highest x={xMid,6:0.00}  y={best,6:0.00}");
                    }

                    Debug.Log(grid.ToString());
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

                // **The middle of the climb, not a seam** — there are no two pieces to meet any
                // more. Half a layer up in the stair's own cell, so the frame holds the whole
                // flight and the empty cell along +X that it must not spill into.
                Vector3 seam = CellMetrics.FloorCentre(start.X, start.Z, start.Y)
                               + new Vector3(0f, StairShape.FullRise * 0.5f, 0f);

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
