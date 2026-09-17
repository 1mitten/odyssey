#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
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
    /// How deep the water is against a person, photographed at several surface heights.
    ///
    /// <para><b>The question this exists to settle.</b> <c>ChunkMesher.WaterSurface</c> is 0.72 of
    /// a 3 m cell, so the drawn surface stands <b>2.16 m</b> above the bed a wader is standing on,
    /// and a colonist is about 1.8 m. Shallow water is therefore over a colonist's head — which is
    /// why the owner saw colonists walking *under* the water rather than through it (2026-09-17),
    /// and why <c>docs/design/20-swimming-and-water.md</c> has to swim both depths rather than
    /// only the deep one. Lower the surface far enough and shallow water becomes genuinely
    /// shallow: waded upright, with no swim pose needed for it at all.</para>
    ///
    /// <para><b>Why it is a photograph and not a number.</b> The arithmetic is already decided —
    /// every depth below is printed in metres and as a fraction of a colonist — and it cannot say
    /// whether 0.9 m of water reads as a stream you could walk through or as a puddle. That is a
    /// judgement, it is the owner's, and it gates about half the work in the swimming design, so
    /// it is worth putting in front of them before any of that is built.</para>
    ///
    /// <para><b>The white post is the whole point of the picture.</b> It is 1.8 m of colonist
    /// standing on the bed, drawn as a plain box so it needs no rig and no licensed pack — a
    /// worktree without <c>Assets/Synty/</c> junctioned would draw a real character as a primitive
    /// anyway. Its lower 1.0 m is banded so the waterline can be read off it: each band is
    /// 0.25 m, and the knee, waist and chest of a person that height fall at roughly 0.5, 1.0 and
    /// 1.35 m.</para>
    ///
    /// <para>Nothing here is a change to the game. <c>WaterSurface</c> is put back to whatever it
    /// was before the tool ran, in a <c>finally</c>, so a crashed run cannot leave the project
    /// holding an experimental value.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.WaterDepthCheck.Run</c>. It
    /// needs a real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class WaterDepthCheck
    {
        [MenuItem("Odyssey/Presentation/Check the water depth")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        /// <summary>
        /// The surface heights to photograph, as a fraction of a cell.
        ///
        /// <para>0.72 is what ships and is the control. 0.50 is waist-deep and is the middle of
        /// the argument. 0.30 is the design's own proposal, 0.9 m, mid-thigh on a colonist and
        /// the shallowest depth that still reads as water rather than as a wet floor. 0.15 is
        /// included as the far end, because a judgement with no bracket round it is a preference
        /// rather than a choice.</para>
        /// </summary>
        static readonly float[] Surfaces = { 0.72f, 0.50f, 0.30f, 0.15f };

        /// <summary>A colonist, in metres. The figures are scaled to about this.</summary>
        const float ColonistHeight = 1.8f;

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            float wasSurface = ChunkMesher.WaterSurface;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            GameObject? posts = null;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var slice = new SliceSettings();

                lightingRoot = new GameObject("WaterDepthRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                cameraObject = new GameObject("WaterDepthCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                // The same stream the water contact sheet uses, so the two sets of pictures are of
                // the same piece of board and can be laid beside each other.
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();

                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 3u, gen);

                if (!TryFindShallowWater(grid, size, result.StartCell.Y, out CellRef wet))
                {
                    Debug.LogError("[WaterDepth] this board has no shallow water on the active layer, " +
                                   "so there is nothing to photograph. Try another seed.");
                    exitCode = 1;
                    return;
                }

                // The layer the water is on, not the one the colony starts on: the slice has to be
                // cut where the subject is or the renderer draws everything above it solid and the
                // photograph is of a lid.
                int activeLayer = wet.Y;

                posts = BuildPosts(grid, size, wet);

                // Say which cell was chosen and what is under and around it. The first run framed
                // a post that turned out to be standing on the shore, and a picture of the wrong
                // cell answers the question wrongly rather than not at all.
                Debug.Log($"[WaterDepth] photographing shallow water at ({wet.X},{wet.Z},{wet.Y}); " +
                          $"the cell below is terrain {grid.Terrain[size.Index(wet.X, wet.Z, wet.Y - 1)]}, " +
                          $"solid={(grid.Flags[size.Index(wet.X, wet.Z, wet.Y - 1)] & CellFlags.SolidTerrain) != 0}. " +
                          $"Neighbours: " +
                          $"+x {grid.Terrain[size.Index(wet.X + 1, wet.Z, wet.Y)]}, " +
                          $"-x {grid.Terrain[size.Index(wet.X - 1, wet.Z, wet.Y)]}, " +
                          $"+z {grid.Terrain[size.Index(wet.X, wet.Z + 1, wet.Y)]}, " +
                          $"-z {grid.Terrain[size.Index(wet.X, wet.Z - 1, wet.Y)]}. " +
                          $"Shallow water is {NaturalContent.TerrainShallowWater}, " +
                          $"deep {NaturalContent.TerrainDeepWater}.");

                library = new ModuleLibrary(catalogue);
                var chunks = new ChunkGrid(size);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);
                renderer = new ChunkRenderer(model);
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

                Vector3 focus = CellMetrics.FloorCentre(wet);

                foreach (float surface in Surfaces)
                {
                    ChunkMesher.WaterSurface = surface;

                    // The mesh carries the surface height, so it has to be rebuilt for each one.
                    // Refreshing the whole model is the blunt way and it is the right way here:
                    // this is a tool that takes four photographs, not a per-frame path.
                    model.RefreshAll(grid, result.Natural!.Context.Edifices);
                    renderer.Dispose();
                    renderer = new ChunkRenderer(model);
                    renderer.Skirt.Enabled = true;
                    renderer.Skirt.Build();
                    active = renderer;

                    float metres = surface * CellMetrics.SizeY;
                    string tag = Mathf.RoundToInt(surface * 100f).ToString("D2");

                    // Low, because the whole question is where the waterline crosses the post and a
                    // shot from the play pitch looks down on the surface and hides it — but not so
                    // low or so near that the banks around the cell get between the camera and the
                    // post, which is what 12 degrees at 10 m did on the first run.
                    // 22 degrees at 20 m. Both ends of that were found by trying: 12 at 10 m put
                    // the camera inside the bank, and so did 14 at 9 m — a low pitch near a cut
                    // channel looks *through* the ground before it looks at the water.
                    PlayScene.Shoot(camera, focus + Vector3.up * 0.9f, 22f, 20f,
                        $"Logs/water-depth-{tag}-waterline.png");
                    PlayScene.Shoot(camera, focus + Vector3.up * 0.6f, 18f, 13f,
                        $"Logs/water-depth-{tag}-close.png");
                    PlayScene.Shoot(camera, focus, 48f, 26f, $"Logs/water-depth-{tag}-play.png");

                    Debug.Log($"[WaterDepth] surface {surface:F2} of a cell = {metres:F2} m over the bed, " +
                              $"{metres / ColonistHeight * 100f:F0}% of a {ColonistHeight:F1} m colonist " +
                              $"({Describe(metres)}).");
                }

                RenderPipelineManager.beginCameraRendering -= hook;
                hook = null;

                Debug.Log("[WaterDepth] wrote Logs/water-depth-{72,50,30,15}-{waterline,play}.png. " +
                          "The banded post is 1.8 m of colonist standing on the bed; each band is 0.25 m.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WaterDepth] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                // Put the game back. A tool that leaves an experimental constant behind is a tool
                // that silently changes what everybody else measures afterwards.
                ChunkMesher.WaterSurface = wasSurface;

                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                if (posts != null) UnityEngine.Object.DestroyImmediate(posts);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static string Describe(float metres) =>
            metres >= ColonistHeight ? "over a colonist's head"
            : metres >= 1.3f ? "chest deep"
            : metres >= 0.9f ? "waist deep"
            : metres >= 0.4f ? "knee deep"
            : "ankle deep";

        /// <summary>
        /// A shallow-water cell on the active layer with dry ground beside it, so the picture has a
        /// bank in it and the waterline has somewhere to meet.
        /// </summary>
        internal static bool TryFindShallowWater(CellGrid grid, GridSize size, int layer, out CellRef found)
        {
            // **Every layer, not the start cell's.** The board is terraced and water lies wherever
            // the ground took it, so a stream is very often not on the layer the colony starts on
            // — searching only that one found nothing at all on the first attempt. The best cell
            // wins rather than the first, scored by how much water is around it, so the picture is
            // of the middle of a stream rather than of its last puddle.
            found = default;
            int bestWater = 0;

            for (int y = 1; y < size.SizeY; y++)
            for (int z = 2; z < size.SizeZ - 2; z++)
            for (int x = 2; x < size.SizeX - 2; x++)
            {
                var cell = new CellRef(x, z, y);
                if (grid.Terrain[size.Index(x, z, y)] != NaturalContent.TerrainShallowWater) continue;

                // The bed has to be solid, or the post stands on nothing and the picture is of a
                // box hanging in the air rather than of a person in a stream.
                if (!Solid(grid, size, Below(cell))) continue;

                int water = 0;
                int shore = 0;
                foreach (CellRef neighbour in Around(cell))
                {
                    if (NaturalContent.IsWater(grid.Terrain[size.Index(neighbour.X, neighbour.Z, neighbour.Y)]))
                        water++;
                    else if (TryStandOn(grid, size, neighbour, out _))
                        shore++;
                }

                // **Both conditions, and the first run had neither.** It framed a one-cell channel
                // whose neighbours were open air, so the shore post was placed in a cell with no
                // floor and drawn floating beside the stream. A cell wants water beside it to be a
                // stream rather than a puddle, and land beside it that a colonist could actually
                // stand on to be a shore rather than a drop.
                if (water < 2 || shore < 1 || water <= bestWater) continue;

                bestWater = water;
                found = cell;
            }

            return bestWater > 0;
        }

        /// <summary>
        /// Where a colonist beside this cell would actually stand: here if the cell is clear with
        /// ground under it, or one layer up if this cell is the bank itself.
        ///
        /// <para><b>The second case is why the first two attempts found nothing.</b> A stream is cut
        /// into the ground, so the land next to it is <em>solid at the water's own layer</em> — the
        /// bank — and a person walking beside the stream is a layer higher than the water they are
        /// walking beside. Asking only "is this neighbour standable" rejected every shore on the
        /// board and reported a map with no shallow water on it, which was not true and was not
        /// what the check meant.</para>
        /// </summary>
        static bool TryStandOn(CellGrid grid, GridSize size, CellRef cell, out CellRef stand)
        {
            stand = default;
            if (cell.Y <= 0 || !size.Contains(cell.X, cell.Z, cell.Y)) return false;

            if (Clear(grid, size, cell) && Solid(grid, size, Below(cell)))
            {
                stand = cell;
                return true;
            }

            var up = new CellRef(cell.X, cell.Z, cell.Y + 1);
            if (Solid(grid, size, cell) && size.Contains(up.X, up.Z, up.Y) && Clear(grid, size, up))
            {
                stand = up;
                return true;
            }

            return false;
        }

        static CellRef Below(CellRef cell) => new CellRef(cell.X, cell.Z, cell.Y - 1);

        static bool Solid(CellGrid grid, GridSize size, CellRef cell) =>
            size.Contains(cell.X, cell.Z, cell.Y) &&
            (grid.Flags[size.Index(cell.X, cell.Z, cell.Y)] & CellFlags.SolidTerrain) != 0;

        static bool Clear(CellGrid grid, GridSize size, CellRef cell) =>
            size.Contains(cell.X, cell.Z, cell.Y) &&
            (grid.Flags[size.Index(cell.X, cell.Z, cell.Y)] & CellFlags.SolidTerrain) == 0;

        /// <summary>
        /// Two posts of colonist height: one standing on the bed in the water, one on the dry cell
        /// beside it.
        ///
        /// <para>The dry one is not decoration. A post in water on its own can be read as a short
        /// post; the pair says "these are the same height, and that is how much of one is under".
        /// </para>
        /// </summary>
        static GameObject BuildPosts(CellGrid grid, GridSize size, CellRef wet)
        {
            var root = new GameObject("WaterDepthPosts");

            Post(root.transform, CellMetrics.FloorCentre(wet), banded: true);

            foreach (CellRef neighbour in Around(wet))
            {
                if (NaturalContent.IsWater(grid.Terrain[size.Index(neighbour.X, neighbour.Z, neighbour.Y)]))
                    continue;
                if (!TryStandOn(grid, size, neighbour, out CellRef stand)) continue;

                Post(root.transform, CellMetrics.FloorCentre(stand), banded: false);
                break;
            }

            return root;
        }

        static IEnumerable<CellRef> Around(CellRef cell)
        {
            yield return new CellRef(cell.X + 1, cell.Z, cell.Y);
            yield return new CellRef(cell.X - 1, cell.Z, cell.Y);
            yield return new CellRef(cell.X, cell.Z + 1, cell.Y);
            yield return new CellRef(cell.X, cell.Z - 1, cell.Y);
        }

        /// <summary>
        /// One 1.8 m post standing on a cell's floor, optionally banded every 0.25 m up its lower
        /// metre so a waterline can be read off it.
        /// </summary>
        static void Post(Transform parent, Vector3 floorCentre, bool banded)
        {
            const float width = 0.28f;

            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.transform.SetParent(parent, false);
            post.transform.localScale = new Vector3(width, ColonistHeight, width);
            post.transform.position = floorCentre + Vector3.up * (ColonistHeight * 0.5f);

            // The two posts are different colours, because in the first usable pair of photographs
            // only one post was visible and there was no way to tell from the picture which. The
            // shore post is dark so it reads against the bank; the one in the water is pale so it
            // reads against the water.
            Paint(post, banded ? new Color(0.96f, 0.96f, 0.93f) : new Color(0.22f, 0.24f, 0.30f));

            if (!banded) return;

            // **Banded the whole way up, not just the first metre.** The first version banded 0 to
            // 1 m, which is under every waterline being compared — so at 0.30 the bands were all
            // submerged and the post read as plain white, and at 0.72 the whole post was under and
            // read as absent. A scale you cannot see above the water cannot measure the water.
            //
            // Alternating 0.25 m bands, a whisker proud of the post so they do not z-fight. On a
            // 1.8 m figure the knee is about band 2, the waist band 4 and the chest band 5.
            for (int band = 0; band * 0.25f < ColonistHeight; band += 2)
            {
                float height = Mathf.Min(0.25f, ColonistHeight - band * 0.25f);
                var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mark.transform.SetParent(parent, false);
                mark.transform.localScale = new Vector3(width * 1.08f, height, width * 1.08f);
                mark.transform.position = floorCentre + Vector3.up * (band * 0.25f + height * 0.5f);
                Paint(mark, new Color(0.85f, 0.18f, 0.16f));
            }
        }

        static void Paint(GameObject thing, Color colour)
        {
            var renderer = thing.GetComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = colour;
            renderer.sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(thing.GetComponent<Collider>());
        }
    }
}
