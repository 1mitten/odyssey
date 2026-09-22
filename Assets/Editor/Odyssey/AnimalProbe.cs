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
                    var gait = Odyssey.Presentation.World.QuadrupedGait.Bind(walker.transform, 1f);
                    if (gait != null)
                    {
                        gait.Advance(1f, 1f);      // a whole stride at full weight: phase back to 0
                        gait.Advance(1f, phase);   // then to the phase asked for
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
                        if (bone.name == "root" || bone.name == "Head" || bone.name == "Body" || bone.name.EndsWith("Foot.L") || bone.name == "Tail7")
                            sb.AppendLine($"    bone {bone.name} world {bone.position} lossy {bone.lossyScale}");
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
