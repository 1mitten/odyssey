#nullable enable
using System;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Photograph a terrace step three ways: as it was, with the ground given a surface, and with
    /// a bank up it.
    ///
    /// <para>Whether a 3 m riser now reads as a hillside rather than as a wall is a judgement, and
    /// reasoning about a renderer from its source is guesswork — the same argument that gave the
    /// axe swing <c>SwingCheck</c>, the relief <c>ReliefCheck</c> and the water
    /// <c>WaterCheck</c>. What a test can prove is that a bank is emitted where one belongs and
    /// nowhere else; only a photograph can say whether it was worth drawing.</para>
    ///
    /// <para><b>It finds the step rather than being told where one is.</b> The board is generated
    /// from noise, so a hardcoded position is a position that is a terrace on one seed and open
    /// meadow on the next. This walks the surface heightmap for the longest run of one-layer steps
    /// it can find and frames that, which also means the shot is of the most representative piece
    /// of terracing on the board rather than the first.</para>
    ///
    /// <para><b>The pitches are chosen, not swept.</b> Side-on and low is the shot that answers the
    /// question, for the reason <c>SwingCheck</c> records about the axe: in the board camera's
    /// three-quarter view a step and the ground in front of it sit at different depths and the
    /// profile of a bank reads as anything you like. The play pitch is there because that is the
    /// view the game is actually judged in, and a change that only looks right from a camera
    /// nobody uses has not been made.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.SlopeCheck.Run</c>. It needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class SlopeCheck
    {
        [MenuItem("Odyssey/Presentation/Check the slopes and banks")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        readonly struct Condition
        {
            public Condition(string name, bool earth, bool banks, float tilt,
                float ripple = 0f, float chamfer = 0f)
            {
                Name = name;
                Earth = earth;
                Banks = banks;
                Tilt = tilt;
                Ripple = ripple;
                Chamfer = chamfer;
            }

            public readonly string Name;
            public readonly bool Earth;
            public readonly bool Banks;

            /// <summary>Side-face normal tilt in degrees. See GroundMesh.SideNormalTiltDegrees.</summary>
            public readonly float Tilt;

            /// <summary>Rim ripple, as a fraction of cell height. See GroundMesh.MaxRipple.</summary>
            public readonly float Ripple;

            /// <summary>How far the lip of an exposed face is cut back, in metres.</summary>
            public readonly float Chamfer;
        }

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            float amplitudeWas = GroundRelief.Amplitude;
            float periodWas = GroundRelief.Period;
            float rippleWas = GroundMesh.MaxRipple;
            float tiltWas = GroundMesh.SideNormalTiltDegrees;
            float chamferWas = GroundMesh.ChamferMetres;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);

                // The board the scene actually loads. Its terracing is the subject, so it must not
                // be a flattened stand-in: MakeWooded leaves surfaceRelief at the def's own 2.
                gen.MakeWooded();

                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                var slice = new SliceSettings();

                GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
                GroundRelief.Period = 150f;

                FindAStep(grid, size, out int stepX, out int stepZ, out int stepY, out int facing);
                Debug.Log($"[Slope] framing the step at ({stepX}, {stepZ}) on layer {stepY}, " +
                          $"looking along bearing {facing} degrees");

                // The step's own layer is the surface here, not the colony's start layer.
                //
                // It matters because the slice treats "underground" and "outdoors" as different
                // questions: below the surface it x-rays one layer overhead, which on a terraced
                // board means every step higher than the one being photographed comes out
                // translucent and fills the foreground with ghosts. Telling it this layer is the
                // surface gets the exterior view, which is what a landscape shot wants.
                int activeLayer = stepY;
                slice.surfaceLayer = stepY;
                var focus = new Vector3(
                    stepX * CellMetrics.SizeXZ + CellMetrics.HalfXZ,
                    stepY * CellMetrics.SizeY,
                    stepZ * CellMetrics.SizeXZ + CellMetrics.HalfXZ);
                focus = GroundRelief.Lift(focus);

                lightingRoot = new GameObject("SlopeRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                cameraObject = new GameObject("SlopeCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                // A contact sheet rather than an argument, because every one of these is a
                // judgement about how something looks and reasoning about a renderer from its
                // source is guesswork. The conditions are chosen so that consecutive pairs differ
                // by exactly one thing: plain to flat is the coursed riser alone, flat to ripple is
                // the rim noise alone, ripple to tilt is the lighting alone.
                var conditions = new[]
                {
                    // What main draws today, for a control that is not a memory.
                    new Condition("plain", earth: false, banks: false, tilt: 0f),
                    // The riser geometry on its own: coursed walls, square lip, true normals.
                    new Condition("flat", earth: true, banks: false, tilt: 0f),
                    // Light the side faces like ground rather than like a wall. This is the one
                    // that answers the black lines, and it costs no geometry at all.
                    new Condition("tilt", earth: true, banks: false, tilt: 38f),
                    // Round the lip off. Two amounts, because whether a terrace still reads as a
                    // terrace is a judgement and the owner asked to be shown rather than told.
                    new Condition("round", earth: true, banks: false, tilt: 38f, chamfer: 0.22f),
                    new Condition("rounder", earth: true, banks: false, tilt: 38f, chamfer: 0.45f),
                    // Everything, banks included.
                    new Condition("banks", earth: true, banks: true, tilt: 38f, chamfer: 0.22f),
                };

                foreach (Condition condition in conditions)
                {
                    // Before the library, never after: a ModuleLibrary holds the built meshes by
                    // reference, so rebuilding them under a live one leaves it pointing at
                    // destroyed meshes and the ground silently stops drawing.
                    GroundMesh.MaxRipple = condition.Ripple;
                    GroundMesh.SideNormalTiltDegrees = condition.Tilt;
                    GroundMesh.ChamferMetres = condition.Chamfer;
                    GroundMesh.Invalidate();
                    BankMesh.Invalidate();

                    // A renderer per condition, for the reason ReliefCheck gives: the choice of
                    // mesh is made at mesh time and baked into the buckets, so a renderer built
                    // under one setting keeps it however the lever is moved afterwards.
                    library = new ModuleLibrary(catalogue);
                    var chunks = new ChunkGrid(size);
                    var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                    model.RefreshAll(grid, result.Natural!.Context.Edifices);
                    renderer = new ChunkRenderer(model)
                    {
                        EarthGeometry = condition.Earth,
                        Banks = condition.Banks,
                    };
                    renderer.Skirt.Enabled = true;
                    renderer.Skirt.Build();

                    ChunkRenderer active = renderer;
                    hook = (context, rendering) =>
                    {
                        if (rendering != camera) return;
                        active.ViewerPosition = rendering.transform.position;
                        active.Render(activeLayer, slice);
                    };
                    RenderPipelineManager.beginCameraRendering += hook;

                    // Side on to the step: the profile of a bank is the whole question, and it is
                    // exactly what a three-quarter view destroys.
                    //
                    // The pitch and the distance are a pair and the first attempt got them wrong.
                    // A camera at 8 degrees and 22 m sits 3 m above its focus, which is one layer —
                    // so it was inside the hillside, and all three conditions photographed the same
                    // flat green nothing. The camera has to clear the terrace it is looking at
                    // before it can look along it, and 14 degrees at 50 m puts it 12 m up, which is
                    // above the tallest step this board makes and still shallow enough to read a
                    // profile.
                    PlayScene.Shoot(camera, focus, 14f, facing, 50f, $"Logs/slope-{condition.Name}-sideon.png");
                    // Low and across, where a riser's silhouette is against the ground beyond it.
                    PlayScene.Shoot(camera, focus, 24f, facing + 30f, 45f, $"Logs/slope-{condition.Name}-low.png");
                    // The view the game is judged in.
                    PlayScene.Shoot(camera, focus, 48f, facing + 45f, 70f, $"Logs/slope-{condition.Name}-play.png");
                    // Right in, at the zoom the owner plays at when something looks wrong.
                    //
                    // Worth having as a separate shot rather than trusting the others: a seam, a
                    // z-fight and the colour of a tuft of grass are all sub-pixel at 70 m and
                    // obvious at 14 m, and the faults reported so far have all been reported from
                    // close up while the sheet was being judged from far away.
                    PlayScene.Shoot(camera, focus, 40f, facing + 45f, 14f, $"Logs/slope-{condition.Name}-macro.png");

                    RenderPipelineManager.beginCameraRendering -= hook;
                    hook = null;

                    // The cost claim, stated as numbers rather than as an argument: mesh variety is
                    // supposed to cost buckets, which is draw calls, and not instances. If the
                    // instance count moves between the first two conditions, something is emitting
                    // geometry per cell that was meant to be a choice of mesh.
                    Debug.Log($"[Slope] {condition.Name}: earth {condition.Earth}, banks {condition.Banks}, " +
                              $"ripple {condition.Ripple * CellMetrics.SizeY * 100f:F1} cm, " +
                              $"tilt {condition.Tilt} deg, chamfer {condition.Chamfer * 100f:F0} cm — " +
                              $"{renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances, " +
                              $"{renderer.ChunksDrawn} chunks");

                    renderer.Dispose();
                    renderer = null;
                    library.Dispose();
                    library = null;
                }

                Debug.Log("[Slope] wrote Logs/slope-{plain,earth,banks}-{sideon,low,play}.png");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Slope] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                GroundRelief.Amplitude = amplitudeWas;
                GroundRelief.Period = periodWas;

                // Put the meshes back as the rest of the editor expects to find them. Without the
                // rebuild every scene opened after this one would draw whatever the last condition
                // happened to be, which is a confusing thing to inherit from a screenshot tool.
                GroundMesh.MaxRipple = rippleWas;
                GroundMesh.SideNormalTiltDegrees = tiltWas;
                GroundMesh.ChamferMetres = chamferWas;
                GroundMesh.Invalidate();
                BankMesh.Invalidate();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// The middle of the longest straight run of one-layer terrace step on the board, and the
        /// bearing to look along it from.
        ///
        /// <para>Longest rather than first, because a one-cell step is a nick in the ground and
        /// photographs as noise; a run of eight is a terrace and photographs as one. The search is
        /// over the low side of each step, which is where the bank stands, and the bearing returned
        /// looks from the low side towards the high one — face on to the riser, which is the view
        /// that shows a profile.</para>
        /// </summary>
        static void FindAStep(CellGrid grid, GridSize size, out int x, out int z, out int y, out int facing)
        {
            var tops = new int[size.SizeX * size.SizeZ];
            for (int cz = 0; cz < size.SizeZ; cz++)
            for (int cx = 0; cx < size.SizeX; cx++)
            {
                int top = 0;
                for (int cy = size.SizeY - 1; cy >= 0; cy--)
                    if (grid.IsSolidTerrain(size.Index(cx, cz, cy))) { top = cy; break; }
                tops[cz * size.SizeX + cx] = top;
            }

            int bestX = size.SizeX / 2, bestZ = size.SizeZ / 2, bestRun = 0, bestFacing = 0;

            // Runs along z, with the step facing +x, and runs along x with it facing +z. Two
            // orientations is enough: a diagonal terrace still presents one of them somewhere.
            ScanRuns(tops, size, alongZ: true, ref bestX, ref bestZ, ref bestRun, ref bestFacing);
            ScanRuns(tops, size, alongZ: false, ref bestX, ref bestZ, ref bestRun, ref bestFacing);

            x = bestX;
            z = bestZ;
            y = tops[bestZ * size.SizeX + bestX] + 1;    // the empty cell the bank stands in
            facing = bestFacing;
        }

        static void ScanRuns(int[] tops, GridSize size, bool alongZ,
            ref int bestX, ref int bestZ, ref int bestRun, ref int bestFacing)
        {
            int outer = alongZ ? size.SizeX : size.SizeZ;
            int inner = alongZ ? size.SizeZ : size.SizeX;

            // Keep away from the rim: the surround meets the board there and a shot of the join is
            // a shot of a different question.
            const int margin = 12;

            for (int a = margin; a < outer - margin - 1; a++)
            {
                int run = 0;
                for (int b = margin; b < inner - margin; b++)
                {
                    int lowX = alongZ ? a : b;
                    int lowZ = alongZ ? b : a;
                    int highX = alongZ ? a + 1 : b;
                    int highZ = alongZ ? b : a + 1;

                    int low = tops[lowZ * size.SizeX + lowX];
                    int high = tops[highZ * size.SizeX + highX];

                    if (high - low == 1) run++;
                    else run = 0;

                    if (run <= bestRun) continue;

                    bestRun = run;
                    // The middle of the run so far, on the low side, which is where a bank stands.
                    int midB = b - run / 2;
                    bestX = alongZ ? a : midB;
                    bestZ = alongZ ? midB : a;
                    // Look from the low side towards the high one: +x is a bearing of 90, +z of 0.
                    bestFacing = alongZ ? 270 : 180;
                }
            }
        }
    }
}
