#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Photograph a recoloured colony, because no test can say whether it looks like anything.
    ///
    /// <para>What <i>is</i> tested: that the appearance is stable across loads, that the three
    /// slots are independent, that every palette entry is reachable, that the swatch rectangles
    /// are disjoint and that a look costs one material. None of that says whether a colonist
    /// reads as a person in work clothes or as a bag of sweets, which is the only question the
    /// owner actually asked.</para>
    ///
    /// <para><b>Four sheets, and the magenta ones are the load-bearing ones.</b> The parade shows
    /// the colony as it will be played and answers the taste question. The three signal sheets
    /// force one slot at a time to magenta across every colonist at once, which turns a judgement
    /// about colour into a judgement about <i>place</i>: a magenta jacket, or a magenta face, is
    /// unmistakable and needs no eye for shade at all. A classifier that has put the shirt
    /// rectangle on the trousers cannot survive that picture, and could easily survive the parade.
    /// </para>
    ///
    /// <para>The numbers printed beside each sheet matter as much as the pictures, for the reason
    /// <c>docs/lessons.md</c> gives about counting rather than squinting: a hair slot holding
    /// twelve vertices is a misclassification a photograph will not show.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.ColourCheck.Run</c>. Output:
    /// <c>Logs/colour-*.png</c>. Needs a real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class ColourCheck
    {
        const float FrameSeconds = 1f / 60f;

        /// <summary>Enough colonists that the palette can repeat, and few enough to see faces.</summary>
        const int Colonists = 12;

        static readonly Rgb24 Signal = Rgb24.FromHex(0xFF00FF);

        /// <summary>Grey, so the one slot that is magenta is the only thing the eye goes to.</summary>
        static readonly Rgb24 Muted = Rgb24.FromHex(0x808080);

        [MenuItem("Odyssey/Presentation/Check the colonist colours")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        enum Sheet { Art, Palette, Skin, Hair, Cloth }

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            PawnFigureDirector? figures = null;
            ColonistMaterials? materials = null;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();

                library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);
                renderer = new ChunkRenderer(model);

                var nav = new NavGraph(grid);
                nav.Rebuild();
                var pawns = new PawnContext(
                    grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core())
                    { Chunks = chunks };
                var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
                var mirror = new GridMirrorContributor(grid, result.Edifices, model);
                var designations = new Odyssey.Sim.Designations.DesignationGrid(grid, result.Edifices);

                SimWorld world = new SimWorldBuilder()
                    .WithSeed(1u)
                    .WithSize(size)
                    .AddSnapshotContributor(mirror)
                    .AddColony(pawns, designations, support, nav)
                    .Build();

                // Bare, and a bigger colony than the scenario's five: the question is what a crowd
                // of them looks like together, and five cannot show a palette repeating.
                ScenarioDef scenario = ScenarioDef.Bare();
                scenario.colonists = Colonists;
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, scenario);

                lightingRoot = new GameObject("ColourRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                var appearances = new ColonistAppearanceBook(20260916u, catalogue);
                materials = new ColonistMaterials();
                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0)
                {
                    Appearances = appearances,
                    Materials = materials,
                };

                if (!figures.Enabled)
                {
                    Debug.LogError("[Colour] no live figures: no character art or no gait clips. " +
                                   "The pictures would show baked meshes and prove nothing.");
                    exitCode = 1;
                    return;
                }

                if (!materials.Available)
                    Debug.LogWarning("[Colour] Odyssey/Character did not resolve; every sheet will " +
                                     "show the pack art and the magenta sheets will prove nothing.");

                int movePerTick = PawnContent.Core().Movement.movePerTick;
                for (int tick = 0; tick < 400; tick++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                cameraObject = new GameObject("ColourCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;

                ChunkRenderer active = renderer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                Report(world.Views.Current, appearances, catalogue, figures);
                ReportRenderState(lightingRoot);

                // The same warm-up GestureCheck needs: a character drawn in the first frames after
                // its material is first touched comes out flat and untextured, then draws properly
                // a frame or two later.
                Vector3 centre = Centre(world.Views.Current, figures);
                for (int warm = 0; warm < 4; warm++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    PlayScene.Shoot(camera, centre, 25f, 30f, 16f, "Logs/colour-warm.png");
                }

                // The ink experiment, before the ordinary sheets. Shooting each material
                // condition with the outline feature off and then on isolates the ink: the trees
                // and rocks contribute the same line in both conditions and cancel, so what is
                // left in the difference is the colonists. Comparing the pack material against
                // ours this way is the only way to answer "are our colonists inked" without a
                // judgement about how dark a coat is, which is what confounded the first attempt.
                foreach (Sheet sheet in new[] { Sheet.Art, Sheet.Palette })
                foreach (bool ink in new[] { false, true })
                {
                    SetOutline(ink);
                    Dress(sheet, world.Views.Current, appearances, figures, materials);
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    centre = Centre(world.Views.Current, figures);
                    string tag = sheet.ToString().ToLowerInvariant() + (ink ? "-ink" : "-noink");
                    PlayScene.Shoot(camera, centre, 12f, 30f, 7f, $"Logs/colour-{tag}.png");
                    // And at the framing the game is actually played from. The ink fades from
                    // 150 m, so the board camera is well inside it, but a colonist is small and a
                    // line that reads at seven metres may not read at forty.
                    PlayScene.Shoot(camera, centre, 48f, 30f, 45f, $"Logs/colour-{tag}-board.png");
                }
                // Is the sliver test what keeps colonists out of the ink? It suppresses the line
                // on anything narrower than the line itself, which is what stops a meadow reading
                // as dark smudges -- and a colonist is a bundle of narrow limbs. Shooting with it
                // switched off answers in one picture what no amount of reading the shader will.
                SetOutline(true, 0f);
                Dress(Sheet.Palette, world.Views.Current, appearances, figures, materials);
                world.Tick();
                figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                figures.Evaluate(FrameSeconds);
                centre = Centre(world.Views.Current, figures);
                PlayScene.Shoot(camera, centre, 12f, 30f, 7f, "Logs/colour-nosliver.png");

                // And with the detector wound right up: no sliver test, a threshold twelve times
                // finer and a fat line. If a colonist gains no ink even here, the silhouette is
                // not in the depth texture at all, and no amount of tuning will ink it.
                // A control object: an ordinary GameObject cube with a stock URP material, stood
                // beside the colonists. If the cube inks and the colonists do not, the fault is in
                // the figures; if neither inks while the instanced trees do, then no GameObject
                // reaches the depth texture here and that is a much larger finding.
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                probe.name = "InkProbe";
                // Deliberately *behind* the colonists and overlapping them on screen. If a
                // colonist is in the depth texture it occludes the cube, and the cube's ink stops
                // at the colonist's silhouette. If the ink runs straight across the colonist, the
                // colonist is not in the depth texture -- which is the whole question, answered
                // without writing a debug shader.
                Vector3 behind = camera.transform.forward;
                behind.y = 0f;
                probe.transform.position = centre + behind.normalized * 3.5f + new Vector3(0f, 0.6f, 0f);
                probe.transform.localScale = new Vector3(2.4f, 3.0f, 0.4f);

                SetOutline(true, 0f, 0.001f, 5f);
                world.Tick();
                figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                figures.Evaluate(FrameSeconds);
                PlayScene.Shoot(camera, centre, 12f, 30f, 7f, "Logs/colour-maxink.png");

                // The one thing a figure has that the cube does not: skinning forced to
                // recalculate on every render. It is set for a good reason -- without it a held
                // axe swings while the colonist holding it stands still -- but a depth prepass is
                // another render, and this is the only difference left to test.
                int touched = 0;
                foreach (SkinnedMeshRenderer skin in lightingRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.forceMatrixRecalculationPerRender = false;
                    touched++;
                }
                world.Tick();
                figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                figures.Evaluate(FrameSeconds);
                PlayScene.Shoot(camera, centre, 12f, 30f, 7f, "Logs/colour-noforce.png");
                Debug.Log($"[Colour] forceMatrixRecalculationPerRender cleared on {touched} renderers");

                UnityEngine.Object.DestroyImmediate(probe);
                SetOutline(true, 3f, 0.012f, 2.2f);

                foreach (Sheet sheet in new[] { Sheet.Art, Sheet.Palette, Sheet.Skin, Sheet.Hair, Sheet.Cloth })
                {
                    Dress(sheet, world.Views.Current, appearances, figures, materials);

                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    centre = Centre(world.Views.Current, figures);

                    string name = sheet.ToString().ToLowerInvariant();
                    PlayScene.Shoot(camera, centre, 25f, 30f, 16f, $"Logs/colour-{name}.png");
                    PlayScene.Shoot(camera, centre, 12f, 30f, 7f, $"Logs/colour-{name}-close.png");
                    Debug.Log($"[Colour] Logs/colour-{name}.png and -close.png " +
                              $"({figures.Drawn.Count} figures, {materials.MaterialCount} materials)");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                figures?.Dispose();
                materials?.Dispose();
                renderer?.Dispose();
                library?.Dispose();
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }

            if (!exitWhenDone) ShotFolder.Reveal("colour-*.png");
        }

        /// <summary>
        /// Switch the ink line on or off, so a pair of pictures can be differenced.
        ///
        /// The outline is a renderer feature on the pipeline's renderer asset rather than
        /// anything this harness owns, so it is reached through the asset. Left switched back on.
        /// </summary>
        static void SetOutline(bool on, float sliverRadius = -1f, float depthThreshold = -1f, float thickness = -1f)
        {
            var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(
                "Assets/Settings/PC_Renderer.asset");
            if (data == null) { Debug.LogWarning("[Colour] no PC_Renderer.asset; ink cannot be toggled"); return; }

            foreach (UnityEngine.Rendering.Universal.ScriptableRendererFeature feature in data.rendererFeatures)
            {
                if (feature == null || !feature.GetType().Name.Contains("Outline")) continue;
                feature.SetActive(on);
                Set(feature, "sliverRadius", sliverRadius);
                Set(feature, "depthThreshold", depthThreshold);
                Set(feature, "thickness", thickness);
            }
            data.SetDirty();
        }

        static void Set(object feature, string name, float value)
        {
            if (value < 0f) return;
            System.Reflection.FieldInfo? field = feature.GetType().GetField(name);
            if (field != null) field.SetValue(feature, value);
        }

        /// <summary>
        /// Put every colonist into the sheet being shot.
        ///
        /// The signal sheets go through the appearance book's override map — the same seam a
        /// player-facing appearance panel will use — rather than through a back door of their own,
        /// so the harness exercises the mechanism the game will ship.
        /// </summary>
        static void Dress(Sheet sheet, WorldSnapshot frame, ColonistAppearanceBook book,
                          PawnFigureDirector figures, ColonistMaterials materials)
        {
            foreach (PawnView pawn in frame.Pawns)
            {
                int id = pawn.Id.Value;
                if (sheet == Sheet.Art || sheet == Sheet.Palette) { book.ClearOverride(id); continue; }

                ColonistAppearance real = book.For(id);
                Rgb24 skin = sheet == Sheet.Skin ? Signal : Muted;
                Rgb24 hair = sheet == Sheet.Hair ? Signal : Muted;
                Rgb24 cloth = sheet == Sheet.Cloth ? Signal : Muted;
                book.Override(id, new ColonistAppearance(real.Look, skin, hair, cloth, cloth));
            }

            // The art sheet is the control: no character material at all, so what is drawn is
            // exactly what the packs paint. It is the only way to see whether the shader matches
            // the stock one, which is the observation the whole mechanism rests on.
            figures.Materials = sheet == Sheet.Art ? null : materials;
            figures.RepaintAll();
        }

        /// <summary>
        /// The numbers, which a photograph cannot give: what each drawn colonist was dealt, and
        /// how much of its body each slot actually covers.
        /// </summary>
        static void Report(WorldSnapshot frame, ColonistAppearanceBook book,
                           ModuleCatalogue? catalogue, PawnFigureDirector figures)
        {
            List<ModuleEntry> rows = catalogue != null
                ? catalogue.FindFamily(ModuleIds.ColonistBase)
                : new List<ModuleEntry>();

            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Colonist colours ===");
            foreach (PawnView pawn in frame.Pawns)
            {
                if (!figures.Drawn.Contains(pawn.Id.Value)) continue;
                ColonistAppearance look = book.For(pawn.Id.Value);
                string body = "?", quality = "?", shares = string.Empty;
                if ((uint)look.Look < (uint)rows.Count)
                {
                    ModuleEntry row = rows[look.Look];
                    AppearanceCells cells = row.appearance;
                    body = row.prefabName;
                    quality = cells.quality.ToString();
                    int total = Mathf.Max(1, cells.totalVerts);
                    shares = $"skin {100 * cells.skinVerts / total,3}%  hair {100 * cells.hairVerts / total,3}%  " +
                             $"cloth {100 * cells.clothVerts / total,3}%  cloth2 {100 * cells.cloth2Verts / total,3}%";
                }

                report.AppendLine($"  pawn {pawn.Id.Value,3}  {body,-34} {quality,-10} {shares}");
                report.AppendLine($"           {look}");
            }
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// What queue every figure renderer actually draws in, and with what shader.
        ///
        /// The outline reads a depth texture copied after the opaques, so anything drawn in the
        /// transparent range is absent from it and can never be inked -- and water, which fades
        /// against that same depth, shades straight over it. Both of the owner reported symptoms
        /// fall out of one number, so the number is worth printing rather than reasoning about.
        /// </summary>
        static void ReportRenderState(GameObject root)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Figure render state ===");
            report.AppendLine("  queue  castShadows  shader / renderer");

            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material? m = skin.sharedMaterial;
                report.AppendLine($"  {(m == null ? -1 : m.renderQueue),5}  {skin.shadowCastingMode,11}  " +
                                  $"{(m == null ? "<none>" : m.shader.name)}  [{skin.name}] " +
                                  $"enabled={skin.enabled} layer={skin.gameObject.layer}");
            }

            foreach (MeshRenderer mesh in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material? m = mesh.sharedMaterial;
                report.AppendLine($"  {(m == null ? -1 : m.renderQueue),5}  {mesh.shadowCastingMode,11}  " +
                                  $"{(m == null ? "<none>" : m.shader.name)}  [{mesh.name}] (mesh)");
            }

            Debug.Log(report.ToString());
        }

        static Vector3 Centre(WorldSnapshot frame, PawnFigureDirector figures)
        {
            var sum = Vector3.zero;
            int n = 0;
            foreach (PawnView pawn in frame.Pawns)
            {
                if (!figures.TryGetFeet(pawn.Id, out Vector3 feet)) continue;
                sum += feet;
                n++;
            }
            return n == 0 ? Vector3.zero : sum / n + new Vector3(0f, 1f, 0f);
        }
    }
}
