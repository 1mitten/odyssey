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
                        sb.AppendLine($"frame {frame,3} cell {hog.Cell} path {hog.HasPath}: " + LegReport(root.transform));
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

        static string LegReport(Transform root)
        {
            string[] legs = { "BackUpLeg.L", "FrontUpLeg.L", "BackUpLeg.R", "FrontUpLeg.R" };
            string[] feet = { "BackFoot.L", "FrontFoot.L", "BackFoot.R", "FrontFoot.R" };
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 4; i++)
            {
                Transform? up = FindDeep(root, legs[i]);
                Transform? foot = FindDeep(root, feet[i]);
                if (up == null || foot == null) { parts.Add(legs[i] + " unbound"); continue; }
                parts.Add($"{legs[i]} len {Vector3.Distance(up.position, foot.position):F3} footY {foot.position.y:F3}");
            }
            return string.Join("  ", parts);
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
    }
}
