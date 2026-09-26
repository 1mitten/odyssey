#nullable enable
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Reports what Unity made of the animal models under <c>Assets/Art/Custom/Animals</c>: the
    /// rig type the importer chose, the file and node scale it applied, the size of each model
    /// in metres as it will stand on the board, its bone count, and every clip with its length,
    /// its loop flag and whether its root moves.
    ///
    /// <para>Written for the animals ground (<c>docs/research/e-08-animal-fbx-inspection.md</c>):
    /// the two FBX files carry a Blender "units scale" export — 1 cm units with a ×100 on the
    /// mesh and, on the rat, a ×39.55 on the armature — so the raw bounding box says nothing
    /// about the imported size and the only honest number is one Unity reports. Run it with
    /// <c>scripts/unity.sh exec Odyssey.EditorTools.AnimalProbe.Run</c>; it writes
    /// <c>Logs/animal-probe.txt</c> beside the editor log.</para>
    /// </summary>
    public static class AnimalProbe
    {
        const string Folder = "Assets/Art/Custom/Animals";

        /// <summary>The import settings live in <see cref="AnimalImport"/>; the probe applies them first.</summary>
        static void ApplyImportScale() => AnimalImport.Apply();

        public static void Run()
        {
            ApplyImportScale();
            var sb = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Report(path, sb);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/animal-probe.txt", sb.ToString());
            Debug.Log("[AnimalProbe]\n" + sb);
        }

        /// <summary>
        /// The picture: each animal on one 2.5 m cell of the game's grass beside a 1 m cube, so
        /// the imported size is seen rather than reasoned from three disagreeing scale nodes.
        /// <c>scripts/unity.sh shot Odyssey.EditorTools.AnimalProbe.Shoot</c> →
        /// <c>Logs/animal-sheet.png</c>. Writes the text report too, so one launch does both.
        /// </summary>
        public static void Shoot()
        {
            int exitCode = 0;
            GameObject? root = null;
            try
            {
                Run();
                root = new GameObject("AnimalSheet");
                PlayScene.BuildSheetLighting(root.transform);
                Material? grass = null;
                foreach (string g in AssetDatabase.FindAssets("Mat_Grass_Textures_01 t:Material"))
                {
                    grass = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                    if (grass != null) break;
                }
                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { Folder });
                int column = 0;
                foreach (string guid in guids)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (prefab == null) continue;
                    var at = new Vector3(column * 2.5f, 0f, 0f);
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(tile.GetComponent<Collider>());
                    tile.transform.SetParent(root.transform, false);
                    tile.transform.position = at + Vector3.down * 1.5f;
                    tile.transform.localScale = new Vector3(2.45f, 3f, 2.45f);
                    if (grass != null) tile.GetComponent<MeshRenderer>().sharedMaterial = grass;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    inst.transform.position = at;
                    column++;
                }
                // The hog mid-stride, twice, so the computed walk's leg signs are judged from a
                // picture (design 29; c-procedural-quadruped-gait.md): at phase 0.15 the left
                // fore leg is near the top of its swing with the knee flexed, and at 0.65 the
                // right fore is. A leg that bends the wrong way reads at once here and never in
                // a number.
                var hogModel = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Pig.fbx");
                foreach (float phase in new[] { 0.15f, 0.65f })
                {
                    if (hogModel == null) break;
                    var at = new Vector3(column * 2.5f, 0f, 0f);
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(tile.GetComponent<Collider>());
                    tile.transform.SetParent(root.transform, false);
                    tile.transform.position = at + Vector3.down * 1.5f;
                    tile.transform.localScale = new Vector3(2.45f, 3f, 2.45f);
                    if (grass != null) tile.GetComponent<MeshRenderer>().sharedMaterial = grass;
                    var walker = (GameObject)PrefabUtility.InstantiatePrefab(hogModel, root.transform);
                    walker.transform.position = at;
                    walker.name = $"Hog at phase {phase}";
                    var gait = Odyssey.Presentation.World.QuadrupedGait.Bind(walker.transform);
                    if (gait != null)
                    {
                        gait.Advance(1f, gait.Stride);      // a whole stride at full weight: phase back to 0
                        gait.Advance(1f, gait.Stride * phase);   // then to the phase asked for
                        gait.Apply(walker.transform.right, walker.transform.up);
                        Debug.Log($"[AnimalProbe] hog gait phase {gait.Phase:F2} weight {gait.Weight:F2}");
                    }
                    column++;
                }

                // A 1 m cube on its own cell as the ruler.
                var ruler = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(ruler.GetComponent<Collider>());
                ruler.transform.SetParent(root.transform, false);
                ruler.transform.position = new Vector3(column * 2.5f, 0.5f, 0f);
                var centre = new Vector3(column * 2.5f * 0.5f, 0f, 0f);
                PlayScene.ShootAt(centre, (column + 1) * 2.5f * 1.2f, "Logs/animal-sheet.png");
                Debug.Log("[AnimalProbe] wrote Logs/animal-sheet.png");

                // The walk as a strip: eight hogs side-on, one per eighth of the cycle, on one
                // line, photographed from the flank at leg height. A walk is judged in motion,
                // and this is the nearest a still can come to it (owner, 2026-09-22: "it looks
                // odd and screwed up").
                if (hogModel != null)
                {
                    var strip = new GameObject("WalkStrip");
                    strip.transform.SetParent(root.transform, false);
                    strip.transform.position = new Vector3(0f, 0f, 12f);
                    for (int i = 0; i < 4; i++)
                    {
                        var walker = (GameObject)PrefabUtility.InstantiatePrefab(hogModel, strip.transform);
                        walker.transform.localPosition = new Vector3(i * 1.5f, 0f, 0f);
                        walker.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // nose along +X
                        var gait = Odyssey.Presentation.World.QuadrupedGait.Bind(walker.transform);
                        if (gait == null) continue;
                        gait.Advance(1f, gait.Stride);
                        gait.Advance(1f, gait.Stride * i / 4f);
                        gait.Apply(walker.transform.right, walker.transform.up);
                    }
                    var stripCamera = new GameObject("StripCamera");
                    try
                    {
                        var cam = stripCamera.AddComponent<Camera>();
                        cam.fieldOfView = 30f;
                        cam.nearClipPlane = 0.3f;
                        cam.farClipPlane = 500f;
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
                        // From the flank: yaw 180 looks along -Z... the helper's yaw is about the
                        // focus; 90 degrees off the sheet's default puts the camera on the row's side.
                        PlayScene.Shoot(cam, strip.transform.position + new Vector3(2.25f, 0.3f, 0f), 6f, 0f, 6.5f, "Logs/hog-walk-strip.png");
                        Debug.Log("[AnimalProbe] wrote Logs/hog-walk-strip.png");
                    }
                    finally { Object.DestroyImmediate(stripCamera); }
                }

                // And the mid-stride hog alone, close enough to see which way a knee bends. The
                // sheet above is the scale question; this is the gait question, and the two are
                // judged at different distances.
                if (column >= 4)
                {
                    PlayScene.ShootAt(new Vector3(2 * 2.5f, 0.3f, 0f), 1.6f, "Logs/animal-gait.png");
                    Debug.Log("[AnimalProbe] wrote Logs/animal-gait.png");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AnimalProbe] failed: " + e);
                exitCode = 1;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// The hog under the figure director's own path — the animator, the graph, the idle clip
        /// and the pose pass — moving for a hundred frames, with each leg's shoulder-to-sole
        /// length printed as it goes and a side-on photograph at the end. Written because the
        /// stills that looked right were taken on a bare instance with no animator, and the game
        /// showed legs drawn as rods (owner, 2026-09-22). A leg length that grows frame by frame
        /// is compounding; one that stays is not.
        /// <c>scripts/unity.sh shot Odyssey.EditorTools.AnimalProbe.ShootMoving</c>.
        /// </summary>
        public static void ShootMoving()
        {
            int exitCode = 0;
            GameObject? root = null;
            Odyssey.Presentation.World.PawnFigureDirector? director = null;
            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<Odyssey.Presentation.Rendering.ModuleCatalogue>(PlayScene.CataloguePath);
                root = new GameObject("MovingHog");
                PlayScene.BuildSheetLighting(root.transform);
                director = new Odyssey.Presentation.World.PawnFigureDirector(catalogue, root.transform, 0);
                var sb = new StringBuilder();
                sb.AppendLine($"director enabled {director.Enabled}");

                // A real colony, a real hog, the real snapshot: the world ticks at sixty a second
                // and the director sees exactly what the game publishes.
                var size = new Odyssey.Sim.Contracts.GridSize(40, 40, 16);
                Odyssey.Sim.Pawns.ScenarioDef scenario = Odyssey.Sim.Pawns.ScenarioDef.Bare();
                scenario.colonists = 1;
                scenario.beds = 1;
                scenario.startingFellRadius = 0;
                Odyssey.Sim.Pawns.ColonyWorld colony = Odyssey.Sim.Pawns.ColonyWorld.Build(size, 1u, scenario, barren: true, wooded: false);
                Odyssey.Sim.Contracts.CellRef start = colony.Start;
                int cell = size.Index(start.X + 3, start.Z, start.Y);
                Odyssey.Sim.Pawns.Pawn hog = colony.Pawns.Pawns.Spawn(cell, Odyssey.Sim.Pawns.PawnKindIndex.MiddenHog);
                int waited = 0;
                while (!hog.HasPath && waited < 20_000) { colony.World.Tick(); waited++; }
                sb.AppendLine($"hog set off after {waited} ticks; path length {hog.PathLength}");

                const float dt = 1f / 60f;
                var slice = new Odyssey.Presentation.CameraRig.SliceSettings();
                for (int frame = 0; frame < 180; frame++)
                {
                    colony.World.Tick();
                    director.Sync(colony.World.Views.Current, start.Y, slice, 0f, colony.Pawns.Content.Movement.movePerTick, dt);
                    director.Evaluate(dt);
                    if (frame % 20 == 0 || frame == 179)
                    {
                        // Measured on the figure the director is actually posing, not on the
                        // first rig a search of the hierarchy happens to find: the pool keeps
                        // rigs that are never posed, and the first version of this report
                        // read one of those.
                        Transform posed = FigureOf(director, hog.Id.Value) ?? root.transform;
                        sb.AppendLine($"frame {frame,3} cell {hog.Cell} progress {hog.MoveProgress} path {hog.HasPath} {GaitReport(director, hog.Id.Value)} rigs {CountDeep(root.transform, "BackUpLeg.L")}: " + LegReport(posed));
                    }
                }
                Debug.Log("[AnimalProbe] moving hog " + sb);
                File.WriteAllText("Logs/animal-moving.txt", sb.ToString());

                Transform? figure = null;
                foreach (Transform child in root.transform) if (child.name.StartsWith("Animal figure")) figure = child;
                if (figure != null)
                {
                    var cam = new GameObject("MovingCamera");
                    try
                    {
                        var c = cam.AddComponent<Camera>();
                        c.fieldOfView = 30f; c.nearClipPlane = 0.3f; c.farClipPlane = 500f;
                        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
                        PlayScene.Shoot(c, figure.position + Vector3.up * 0.3f, 8f, 90f, 3.5f, "Logs/hog-moving.png");
                        Debug.Log("[AnimalProbe] wrote Logs/hog-moving.png");
                    }
                    finally { Object.DestroyImmediate(cam); }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AnimalProbe] moving failed: " + e);
                exitCode = 1;
            }
            finally
            {
                director?.Dispose();
                if (root != null) Object.DestroyImmediate(root);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// The frog hopping under the figure director's own path (design 30 §8): a real colony, a
        /// real frog, the snapshot the game publishes, four seconds at sixty frames. Every frame
        /// prints how far the drawn figure moved and how high its body bone is, so the hop pacing
        /// can be read as numbers — a hop is a run of frames with the figure still and the body
        /// low, then a run with it moving fast and the body high — and the frame with the body
        /// highest is photographed from the flank.
        /// <c>scripts/unity.sh shot Odyssey.EditorTools.AnimalProbe.ShootMovingFrog</c> →
        /// <c>Logs/frog-moving.txt</c>, <c>Logs/frog-moving.png</c>.
        /// </summary>
        public static void ShootMovingFrog()
        {
            int exitCode = 0;
            GameObject? root = null;
            Odyssey.Presentation.World.PawnFigureDirector? director = null;
            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<Odyssey.Presentation.Rendering.ModuleCatalogue>(PlayScene.CataloguePath);
                AnimalImport.Apply();
                root = new GameObject("MovingFrog");
                PlayScene.BuildSheetLighting(root.transform);
                director = new Odyssey.Presentation.World.PawnFigureDirector(catalogue, root.transform, 0);
                var sb = new StringBuilder();
                sb.AppendLine($"director enabled {director.Enabled}");

                var size = new Odyssey.Sim.Contracts.GridSize(40, 40, 16);
                Odyssey.Sim.Pawns.ScenarioDef scenario = Odyssey.Sim.Pawns.ScenarioDef.Bare();
                scenario.colonists = 1;
                scenario.beds = 1;
                scenario.startingFellRadius = 0;
                Odyssey.Sim.Pawns.ColonyWorld colony = Odyssey.Sim.Pawns.ColonyWorld.Build(size, 1u, scenario, barren: true, wooded: false);
                Odyssey.Sim.Contracts.CellRef start = colony.Start;
                int cell = size.Index(start.X + 3, start.Z, start.Y);
                Odyssey.Sim.Pawns.Pawn frog = colony.Pawns.Pawns.Spawn(cell, Odyssey.Sim.Pawns.PawnKindIndex.CulvertFrog);
                int waited = 0;
                while (!frog.HasPath && waited < 20_000) { colony.World.Tick(); waited++; }
                sb.AppendLine($"frog set off after {waited} ticks; path length {frog.PathLength}");

                const float dt = 1f / 60f;
                var slice = new Odyssey.Presentation.CameraRig.SliceSettings();
                Vector3? last = null;
                float highest = float.MinValue, travelled = 0f;
                int stillFrames = 0, movingFrames = 0;
                Vector3 photoAt = Vector3.zero;
                for (int frame = 0; frame < 240; frame++)
                {
                    colony.World.Tick();
                    director.Sync(colony.World.Views.Current, start.Y, slice, 0f, colony.Pawns.Content.Movement.movePerTick, dt);
                    director.Evaluate(dt);
                    Transform? figure = FigureOf(director, frog.Id.Value);
                    if (figure == null) continue;
                    Transform? body = FindDeep(figure, "Body");
                    float bodyUp = body != null ? body.position.y - figure.position.y : 0f;
                    float moved = last.HasValue ? Vector3.Distance(new Vector3(figure.position.x, 0f, figure.position.z),
                        new Vector3(last.Value.x, 0f, last.Value.z)) : 0f;
                    last = figure.position;
                    travelled += moved;
                    if (frog.HasPath) { if (moved < 0.004f) stillFrames++; else movingFrames++; }
                    if (bodyUp > highest && frog.HasPath) { highest = bodyUp; photoAt = figure.position; }
                    sb.AppendLine($"frame {frame,3} path {(frog.HasPath ? 1 : 0)} moved {moved * 1000f,5:F0} mm  body {bodyUp * 1000f,4:F0} mm  {GaitReport(director, frog.Id.Value)}");
                }
                sb.AppendLine($"travelled {travelled:F2} m; while pathing {stillFrames} frames still and {movingFrames} moving");
                Debug.Log("[AnimalProbe] moving frog " + sb);
                File.WriteAllText("Logs/frog-moving.txt", sb.ToString());

                Transform? posed = FigureOf(director, frog.Id.Value);
                if (posed != null)
                {
                    var cam = new GameObject("MovingCamera");
                    try
                    {
                        var c = cam.AddComponent<Camera>();
                        c.fieldOfView = 30f; c.nearClipPlane = 0.05f; c.farClipPlane = 500f;
                        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
                        PlayScene.Shoot(c, posed.position + Vector3.up * 0.3f, 8f, 90f, 5f, "Logs/frog-moving.png");
                        // And from the play camera's pitch and distance, for the question a still
                        // from the flank cannot answer: is it big enough to see?
                        PlayScene.Shoot(c, posed.position, 48f, 0f, 40f, "Logs/frog-play-camera.png");
                    }
                    finally { Object.DestroyImmediate(cam); }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AnimalProbe] moving frog failed: " + e);
                exitCode = 1;
            }
            finally
            {
                director?.Dispose();
                if (root != null) Object.DestroyImmediate(root);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// The snap detector (owner, 2026-09-22: <i>"I've seen pigs snap to different positions
        /// in various scenarios - could you check it never snaps or teleports"</i>). A wooded
        /// colony with trees and terraces, three hogs and three rats, a hundred seconds of the
        /// director's own path with a frame between every pair of ticks — and every figure's
        /// drawn position recorded each frame. A snap is a frame in which a figure moved more than
        /// <c>SnapMetres</c>, or moved backwards against its own previous motion by more than
        /// <c>ReverseMetres</c>. Each one is printed with the simulation's view of that pawn on
        /// that tick, which is the only way to say <i>why</i>.
        /// <c>scripts/unity.sh exec Odyssey.EditorTools.AnimalProbe.Snaps</c> → <c>Logs/animal-snaps.txt</c>.
        /// </summary>
        public static void Snaps()
        {
            int exitCode = 0;
            GameObject? root = null;
            Odyssey.Presentation.World.PawnFigureDirector? director = null;
            try
            {
                const float SnapMetres = 0.35f;
                const float ReverseMetres = 0.12f;
                var catalogue = AssetDatabase.LoadAssetAtPath<Odyssey.Presentation.Rendering.ModuleCatalogue>(PlayScene.CataloguePath);
                root = new GameObject("Snaps");
                director = new Odyssey.Presentation.World.PawnFigureDirector(catalogue, root.transform, 0);
                var sb = new StringBuilder();

                var size = new Odyssey.Sim.Contracts.GridSize(60, 60, 16);
                Odyssey.Sim.Pawns.ScenarioDef scenario = Odyssey.Sim.Pawns.ScenarioDef.Bare();
                scenario.colonists = 1;
                scenario.beds = 1;
                scenario.startingFellRadius = 0;
                Odyssey.Sim.Pawns.ColonyWorld colony = Odyssey.Sim.Pawns.ColonyWorld.Build(size, 7u, scenario, barren: false, wooded: true);
                Odyssey.Sim.Contracts.CellRef start = colony.Start;
                var animals = new System.Collections.Generic.List<Odyssey.Sim.Pawns.Pawn>();
                for (int i = 0; i < 6; i++)
                {
                    int cell = colony.Grid.NearestWalkableInColumn(start.X + (i % 3) * 2 - 2, start.Z + (i / 3) * 2 - 1, start.Y);
                    if (cell < 0) continue;
                    animals.Add(colony.Pawns.Pawns.Spawn(cell, i % 2 == 0 ? Odyssey.Sim.Pawns.PawnKindIndex.MiddenHog : Odyssey.Sim.Pawns.PawnKindIndex.DuctRat));
                }
                sb.AppendLine($"{animals.Count} animals on a wooded 60x60, start {start}");

                var slice = new Odyssey.Presentation.CameraRig.SliceSettings();
                var last = new System.Collections.Generic.Dictionary<int, Vector3>();
                var lastMove = new System.Collections.Generic.Dictionary<int, Vector3>();
                int snaps = 0, reverses = 0, frames = 0, layerChanges = 0;
                var lastLayer = new System.Collections.Generic.Dictionary<int, int>();
                const float dt = 1f / 60f;
                for (int tick = 0; tick < 6_000; tick++)
                {
                    colony.World.Tick();
                    for (int half = 0; half < 2; half++)
                    {
                        float alpha = half == 0 ? 0f : 0.5f;
                        director.Sync(colony.World.Views.Current, start.Y + 2, slice, alpha,
                            colony.Pawns.Content.Movement.movePerTick, half == 0 ? dt : 0f);
                        director.Evaluate(half == 0 ? dt : 0f);
                        frames++;
                        foreach (Odyssey.Sim.Pawns.Pawn animal in animals)
                        {
                            if (!director.TryGetFeet(animal.Id, out Vector3 at)) continue;
                            int id = animal.Id.Value;
                            Odyssey.Sim.Contracts.CellRef cell = size.FromIndex(animal.Cell);
                            if (lastLayer.TryGetValue(id, out int ly) && ly != cell.Y) layerChanges++;
                            lastLayer[id] = cell.Y;
                            if (last.TryGetValue(id, out Vector3 was))
                            {
                                Vector3 move = at - was;
                                float dist = move.magnitude;
                                bool snap = dist > SnapMetres;
                                bool reverse = lastMove.TryGetValue(id, out Vector3 prev) && prev.sqrMagnitude > 1e-6f
                                    && Vector3.Dot(move, prev.normalized) < -ReverseMetres;
                                if (snap || reverse)
                                {
                                    if (snap) snaps++; else reverses++;
                                    Odyssey.Sim.Pawns.Job? job = animal.CurrentJob;
                                    int next = animal.HasPath ? animal.Path[animal.PathIndex] : -1;
                                    sb.AppendLine($"tick {tick} alpha {alpha:F1} pawn {id} kind {animal.Kind} {(snap ? "SNAP" : "REVERSE")} " +
                                        $"{dist:F2} m (dy {move.y:F2}) cell {cell} next {(next >= 0 ? size.FromIndex(next).ToString() : "-")} " +
                                        $"progress {animal.MoveProgress}/{animal.MoveStepCost} job {(job != null ? job.DefIndex.ToString() : "none")} " +
                                        $"target {(job != null ? job.TargetCell : -1)} pathLen {animal.PathLength} idx {animal.PathIndex} pending {animal.PathPending} failed {animal.PathFailed}");
                                }
                                lastMove[id] = move;
                            }
                            last[id] = at;
                        }
                    }
                }
                sb.AppendLine($"frames {frames}: {snaps} snaps over {SnapMetres} m, {reverses} reversals over {ReverseMetres} m, {layerChanges} layer changes");
                Debug.Log("[AnimalProbe] snaps " + sb);
                File.WriteAllText("Logs/animal-snaps.txt", sb.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AnimalProbe] snaps failed: " + e);
                exitCode = 1;
            }
            finally
            {
                director?.Dispose();
                if (root != null) Object.DestroyImmediate(root);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// One line per leg: the segment lengths as drawn, the fold between them, and where the
        /// sole is. <b>The rig's <c>Foot</c> bones are not in the leg chain</b> — they are the
        /// IK targets the author animated against, siblings of the legs that sit on the ground
        /// whatever the leg above them does — so nothing here is measured against one. The
        /// first version of this report was, and it said the legs "held to the millimetre" while
        /// they folded through ninety degrees: it was reading the body bob. Each segment is its
        /// bone's own axis, which on this rig points down the leg, and the sole is the lower
        /// segment's end, at the length it has at rest.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, float> _lowerRest = new();

        static string LegReport(Transform root)
        {
            string[] legs = { "BackUpLeg.L", "FrontUpLeg.L", "BackUpLeg.R", "FrontUpLeg.R" };
            string[] lows = { "BackLowLeg.L", "FrontLowLeg.L", "BackLowLeg.R", "FrontLowLeg.R" };
            string[] feet = { "BackFoot.L", "FrontFoot.L", "BackFoot.R", "FrontFoot.R" };
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 4; i++)
            {
                Transform? up = FindDeep(root, legs[i]);
                Transform? low = FindDeep(root, lows[i]);
                Transform? foot = FindDeep(root, feet[i]);
                if (up == null || foot == null || low == null) { parts.Add(legs[i] + " unbound"); continue; }
                float upper = Vector3.Distance(up.position, low.position);
                // The lower segment's length is the rest distance from its joint to the target on
                // the ground, which the target's own height gives back whatever the pose is.
                if (!_lowerRest.TryGetValue(lows[i], out float lower))
                    _lowerRest[lows[i]] = lower = Vector3.Distance(low.position, foot.position);
                float fold = Vector3.Angle(up.up, low.up);
                Vector3 sole = low.position + low.up * lower;
                parts.Add($"{legs[i]} upper {upper:F3} fold {fold:F0} soleY {sole.y - foot.position.y:+0.000;-0.000}");
            }
            return string.Join("  ", parts);
        }

        /// <summary>
        /// The computed gait's own state for one pawn's figure, read by reflection because the
        /// figure list is the director's own business and an instrument may look where a
        /// caller may not. Says whether the gait is engaged at all — the question the leg report
        /// cannot answer on its own.
        /// </summary>
        static Transform? FigureOf(Odyssey.Presentation.World.PawnFigureDirector director, int pawnId)
        {
            var field = typeof(Odyssey.Presentation.World.PawnFigureDirector).GetField("_figures",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(director) is not System.Collections.IEnumerable figures) return null;
            foreach (object figure in figures)
            {
                var type = figure.GetType();
                if ((int)type.GetField("Pawn")!.GetValue(figure)! != pawnId) continue;
                return type.GetField("Transform")!.GetValue(figure) as Transform;
            }
            return null;
        }

        static int CountDeep(Transform root, string name)
        {
            int n = root.name == name ? 1 : 0;
            for (int i = 0; i < root.childCount; i++) n += CountDeep(root.GetChild(i), name);
            return n;
        }

        static string GaitReport(Odyssey.Presentation.World.PawnFigureDirector director, int pawnId)
        {
            var field = typeof(Odyssey.Presentation.World.PawnFigureDirector).GetField("_figures",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(director) is not System.Collections.IEnumerable figures) return "gait ?";
            foreach (object figure in figures)
            {
                var type = figure.GetType();
                if ((int)type.GetField("Pawn")!.GetValue(figure)! != pawnId) continue;
                var gait = type.GetField("Gait")!.GetValue(figure) as Odyssey.Presentation.World.QuadrupedGait;
                float speed = (float)type.GetField("Speed")!.GetValue(figure)!;
                return gait == null
                    ? $"speed {speed:F2} gait none"
                    : $"speed {speed:F2} weight {gait.Weight:F2} phase {gait.Phase:F2} stride {gait.Stride:F3}";
            }
            return "gait no-figure";
        }

        static Transform? FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform? found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void Report(string path, StringBuilder sb)
        {
            sb.AppendLine("== " + path);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { sb.AppendLine("  not a model"); return; }
            sb.AppendLine($"  animationType {importer.animationType}  avatarSetup {importer.avatarSetup}");
            sb.AppendLine($"  useFileScale {importer.useFileScale}  fileScale {importer.fileScale}  globalScale {importer.globalScale}  useFileUnits {importer.useFileUnits}");
            sb.AppendLine($"  bakeAxisConversion {importer.bakeAxisConversion}  importAnimation {importer.importAnimation}  importBlendShapes {importer.importBlendShapes}");

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { sb.AppendLine("  no prefab"); return; }
            var inst = (GameObject)Object.Instantiate(go);
            try
            {
                inst.transform.position = Vector3.zero;
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    if (t.parent == inst.transform || t == inst.transform)
                        sb.AppendLine($"  node {t.name}: localScale {t.localScale} localPos {t.localPosition} lossy {t.lossyScale}");
                }
                var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrs)
                {
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    var b = baked.bounds;
                    // BakeMesh gives the mesh in the renderer's local space with scale applied
                    // when useScale is true; translate by the renderer's world position.
                    Vector3 min = smr.transform.position + b.min, max = smr.transform.position + b.max;
                    sb.AppendLine($"  skinned {smr.name}: verts {baked.vertexCount} tris {baked.triangles.Length / 3} bones {smr.bones.Length} rootBone {(smr.rootBone ? smr.rootBone.name : "none")}");
                    sb.AppendLine($"    bounds m x[{min.x:F3}..{max.x:F3}] y[{min.y:F3}..{max.y:F3}] z[{min.z:F3}..{max.z:F3}]  size {max.x - min.x:F3} x {max.y - min.y:F3} x {max.z - min.z:F3} (w x h x l)");
                    sb.AppendLine($"    sharedMesh bounds size {smr.sharedMesh.bounds.size}  renderer lossyScale {smr.transform.lossyScale}");
                    Object.DestroyImmediate(baked);
                }
                foreach (var smr in smrs)
                {
                    var wb = smr.bounds;
                    sb.AppendLine($"    renderer.bounds (world AABB) centre {wb.center} size {wb.size}  localBounds size {smr.localBounds.size}");
                    foreach (var bone in smr.bones)
                        if (!bone.name.EndsWith(".R") && !bone.name.StartsWith("Tail"))
                            sb.AppendLine($"    bone {bone.name} world {bone.position} fwd {bone.forward} up {bone.up}");
                }
                var animator = inst.GetComponentInChildren<Animator>();
                sb.AppendLine($"  animator {(animator ? (animator.avatar ? animator.avatar.name + (animator.avatar.isHuman ? " human" : " generic") + (animator.avatar.isValid ? " valid" : " INVALID") : "no avatar") : "none")}");
            }
            finally { Object.DestroyImmediate(inst); }

            foreach (var clip in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<AnimationClip>())
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var rootPos = bindings.Where(x => x.propertyName.StartsWith("m_LocalPosition") && (x.path == "" || !x.path.Contains("/"))).ToArray();
                float travel = 0f;
                foreach (var rb in rootPos)
                {
                    var c = AnimationUtility.GetEditorCurve(clip, rb);
                    if (c != null && c.length > 1) travel = Mathf.Max(travel, Mathf.Abs(c.keys[c.length - 1].value - c.keys[0].value));
                }
                sb.AppendLine($"  clip {clip.name}: {clip.length:F2}s {clip.frameRate}fps loop {settings.loopTime} curves {bindings.Length} topLevelPosCurves {rootPos.Length} (paths {string.Join(",", rootPos.Select(x => x.path).Distinct())}) maxTravel {travel:F3}");
            }
            var takes = importer.importedTakeInfos;
            sb.AppendLine($"  takes {takes.Length}: {string.Join(", ", takes.Select(t => t.name + " " + t.startTime.ToString("F2") + ".." + t.stopTime.ToString("F2")))}");
        }

        /// <summary>
        /// The frog's hop, measured and photographed (design 30 §8). The model has no walk: its
        /// locomotion is the Jump clip, so what matters is what one Jump does — how high the body
        /// rises and when, and whether the clip carries the body forward (a clip that travels and
        /// then loops back is a frog that lurches backwards once a hop). Samples the clip at
        /// twelve phases on a bare instance and prints the Body bone's height and forward offset
        /// and the drawn box at each, then a side-on strip of six phases.
        /// <c>scripts/unity.sh shot Odyssey.EditorTools.AnimalProbe.ShootFrog</c> →
        /// <c>Logs/frog-probe.txt</c>, <c>Logs/frog-hop-strip.png</c>.
        /// </summary>
        public static void ShootFrog()
        {
            int exitCode = 0;
            GameObject? root = null;
            try
            {
                AnimalImport.Apply();
                const string path = Folder + "/Frog.fbx";
                var sb = new StringBuilder();
                Report(path, sb);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                AnimationClip? jump = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name.Contains("Jump"));
                AnimationClip? idle = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name.Contains("Idle"));
                if (model == null || jump == null) throw new System.InvalidOperationException("no frog or no jump clip");

                root = new GameObject("FrogSheet");
                PlayScene.BuildSheetLighting(root.transform);
                var probe = (GameObject)Object.Instantiate(model, root.transform);
                Transform? body = probe.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Body");
                var smr = probe.GetComponentInChildren<SkinnedMeshRenderer>();
                sb.AppendLine($"== jump {jump.name} {jump.length:F3}s; body bone {(body != null ? "found" : "MISSING")}");
                for (int i = 0; i <= 12; i++)
                {
                    float t = jump.length * i / 12f;
                    jump.SampleAnimation(probe, t);
                    Bounds b = smr.bounds;
                    Vector3 bp = body != null ? body.position : Vector3.zero;
                    sb.AppendLine($"  phase {i / 12f:F2} t {t:F3}: body y {bp.y:F3} z {bp.z:F3} x {bp.x:F3}   box min.y {b.min.y:F3} max.y {b.max.y:F3} z[{b.min.z:F3}..{b.max.z:F3}]");
                }
                if (idle != null)
                {
                    idle.SampleAnimation(probe, 0f);
                    Bounds b = smr.bounds;
                    sb.AppendLine($"== idle at 0: box size {b.size} min.y {b.min.y:F3}");
                }
                Object.DestroyImmediate(probe);

                var strip = new GameObject("HopStrip");
                strip.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    var frog = (GameObject)Object.Instantiate(model, strip.transform);
                    frog.transform.localPosition = new Vector3(i * 1.9f, 0f, 0f);
                    frog.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // nose along +X
                    jump.SampleAnimation(frog, jump.length * i / 6f);
                }
                var ruler = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(ruler.GetComponent<Collider>());
                ruler.transform.SetParent(root.transform, false);
                ruler.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                ruler.transform.position = new Vector3(-1.9f, 0.125f, 0f);

                var stripCamera = new GameObject("StripCamera");
                try
                {
                    var cam = stripCamera.AddComponent<Camera>();
                    cam.fieldOfView = 30f;
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = 200f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
                    PlayScene.Shoot(cam, new Vector3(3.8f, 0.4f, 0f), 8f, 0f, 15f, "Logs/frog-hop-strip.png");
                }
                finally { Object.DestroyImmediate(stripCamera); }

                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/frog-probe.txt", sb.ToString());
                Debug.Log("[AnimalProbe] frog: " + sb);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AnimalProbe] frog failed: " + e);
                exitCode = 1;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }
    }
}
