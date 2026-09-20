#nullable enable
using System.Text;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// How big a colonist is, and where it ends up when it is laid in a bed.
    ///
    /// <para><b>Written because the owner reported colonists lying half off a bed for the second
    /// time</b> (2026-09-20) and the simulation was measured clean: over three days on four seeds
    /// every sleeping tick was spent on a bed's head cell, so the pose was being placed from the
    /// bed and the fault had to be in the arithmetic that placed it. Reading that arithmetic said
    /// it was right, which on this project has been the wrong answer every time. This prints the
    /// numbers instead — the bed in its own frame, the rig's bones and baked mesh, and where each
    /// of the four postures actually puts the body.</para>
    ///
    /// <para><b>What it found.</b> The humanoid avatar maps <c>HumanBodyBones.Hips</c> to a bone
    /// named <c>Root</c> at the model origin — the real pelvis is its child — so the director's
    /// <c>StandingHipHeight</c> was nought, clamped to 0.2 m, and <c>SleepPose.BodyLength</c> laid
    /// a 2.49 m colonist down 0.38 m long: her feet on the pillow and the rest of her hanging
    /// 1.5 m past the head end of the bed and on to the floor. <c>docs/design/20-beds.md</c>
    /// §7b.</para>
    ///
    /// <para><b>The evaluate is load-bearing.</b> A graph played but never evaluated leaves the
    /// skeleton in the pose the prefab was saved in, which for these characters is the whole
    /// skeleton collapsed on the origin. Measuring before it gives different and equally
    /// plausible-looking numbers, so this drives the figure exactly as
    /// <c>PawnFigureDirector.Create</c> does.</para>
    ///
    /// <para><c>scripts/unity.sh exec Odyssey.EditorTools.SleepProbe.Run</c>.</para>
    /// </summary>
    public static class SleepProbe
    {
        [MenuItem("Odyssey/Presentation/Probe a sleeping colonist")]
        public static void RunFromMenu() => Execute(false);

        public static void Run() => Execute(Application.isBatchMode);

        /// <summary>How many of the cast to measure. They come off one skeleton; the meshes differ.</summary>
        const int Sample = 4;

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? instance = null;
            PlayableGraph graph = default;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                if (catalogue == null)
                {
                    Debug.LogError("[Sleep] no catalogue; nothing to probe.");
                    exitCode = 1;
                    return;
                }

                var report = new StringBuilder();

                // The bed stands with its head in cell (10, 10) facing north, so its foot cell is
                // (10, 11). Everything below is along +Z from the head cell's centre: the two
                // cells are [-1.25, 1.25] and [1.25, 3.75].
                const int hx = 10, hz = 10, hy = 1, facing = Directions.North;
                float reliefWas = GroundRelief.Amplitude;
                GroundRelief.Amplitude = 0f; // flat, so the placement is read without the relief in it

                Vector3 origin = BedShape.Origin(hx, hz, hy, facing);
                Vector3 cellCentre = CellMetrics.FloorCentre(hx, hz, hy);
                const float frameHalf = 4.60f * 0.5f;

                report.AppendLine("[Sleep] the bed, along its facing from the head cell's centre:");
                report.AppendLine($"  head cell     [{-CellMetrics.HalfXZ:F2}, {CellMetrics.HalfXZ:F2}]");
                report.AppendLine($"  foot cell     [{CellMetrics.HalfXZ:F2}, {CellMetrics.HalfXZ + CellMetrics.SizeXZ:F2}]");
                report.AppendLine($"  frame         [{origin.z - cellCentre.z - frameHalf:F2}, {origin.z - cellCentre.z + frameHalf:F2}]");
                report.AppendLine($"  pillow centre {origin.z - cellCentre.z + BedShape.HeadRestAlong:F2}");
                report.AppendLine($"  mattress top  {BedShape.MattressTop:F2} above the bed's floor");

                Vector3 headAt = origin + Vector3.forward * BedShape.HeadRestAlong;
                float surfaceY = origin.y + BedShape.MattressTop;

                int done = 0;
                foreach (ModuleEntry row in catalogue.FindFamily(ModuleIds.ColonistBase))
                {
                    if (row.prefab == null || done >= Sample) continue;

                    AnimationClip? idle = null;
                    for (int i = 0; i < row.locomotion.Count && idle == null; i++)
                        if (row.locomotion[i].clip != null) idle = row.locomotion[i].clip;
                    if (idle == null) continue;

                    instance = Object.Instantiate(row.prefab);
                    instance.transform.localScale =
                        row.scale.sqrMagnitude < 1e-6f ? Vector3.one : row.scale;

                    var animator = instance.GetComponent<Animator>();
                    if (animator == null) animator = instance.AddComponent<Animator>();
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    graph = PlayableGraph.Create("Odyssey sleep probe");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var clip = AnimationClipPlayable.Create(graph, idle);
                    AnimationPlayableOutput output =
                        AnimationPlayableOutput.Create(graph, "Pose", animator);
                    output.SetSourcePlayable(clip);
                    graph.Play();
                    graph.Evaluate(0f);

                    var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    float rootY = instance.transform.position.y;
                    float body = FigureBuild.Height(skins, rootY, FigureBuild.FallbackHeight);

                    report.AppendLine($"  {row.prefabName}: scale {row.scale.x:F2}");
                    report.AppendLine(
                        $"    bones: avatar Hips '{Name(animator, HumanBodyBones.Hips)}' at " +
                        $"{Y(animator, HumanBodyBones.Hips) - rootY:F3}  " +
                        $"upper legs {(Y(animator, HumanBodyBones.LeftUpperLeg) + Y(animator, HumanBodyBones.RightUpperLeg)) * 0.5f - rootY:F3}  " +
                        $"head {Y(animator, HumanBodyBones.Head) - rootY:F3}");
                    FigureBuild.DrawnExtent(skins, out float low, out float high);
                    report.AppendLine($"    drawn body {low - rootY:F3} .. {high - rootY:F3}  ->  length {body:F3}");

                    foreach (SleepPose.Posture posture in SleepPose.Postures)
                    {
                        graph.Evaluate(0f); // re-write the idle, as the director does every frame

                        SleepPose.Place(
                            posture, headAt, Vector3.forward, surfaceY, body, weight: 1f,
                            standingPosition: headAt, standingRotation: Quaternion.identity,
                            out Vector3 position, out Quaternion rotation);
                        instance.transform.SetPositionAndRotation(position, rotation);

                        // The root placement on its own, before the limbs are laid: this is what
                        // Lift is responsible for, and separating the two is the only way to tell
                        // a body sunk in the mattress from an arm swung through it.
                        FigureBuild.DrawnExtent(skins, out float lowBody, out float highBody);

                        LayLimbs(instance, animator, posture);
                        FigureBuild.DrawnExtent(skins, out float lowLain, out float highLain);
                        Vector3 crown = position + rotation * Vector3.up * body;

                        DrawnAlong(skins, out float nearZ, out float farZ);

                        report.AppendLine(
                            $"    {posture.Name,-14} head {crown.z - cellCentre.z,6:F2}" +
                            $"  feet {position.z - cellCentre.z,6:F2}" +
                            $"  drawn along [{nearZ - cellCentre.z,6:F2},{farZ - cellCentre.z,6:F2}]" +
                            $"  clears {lowBody - surfaceY,6:F2} body / {lowLain - surfaceY,6:F2} limbs");
                    }

                    graph.Destroy();
                    graph = default;
                    Object.DestroyImmediate(instance);
                    instance = null;
                    done++;
                }

                GroundRelief.Amplitude = reliefWas;
                Debug.Log(report.ToString());
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (instance != null) Object.DestroyImmediate(instance);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>The drawn figure's extent along the bed, which is what "hanging off" means.</summary>
        static void DrawnAlong(SkinnedMeshRenderer[] skins, out float near, out float far)
        {
            near = float.MaxValue;
            far = float.MinValue;
            Mesh? baked = null;

            for (int i = 0; i < skins.Length; i++)
            {
                SkinnedMeshRenderer skin = skins[i];
                if (skin == null || !skin.enabled || skin.sharedMesh == null) continue;

                baked ??= new Mesh { name = "Odyssey/SleepProbe" };
                skin.BakeMesh(baked, useScale: true);

                Vector3[] vertices = baked.vertices;
                Transform at = skin.transform;
                for (int v = 0; v < vertices.Length; v++)
                {
                    float z = at.TransformPoint(vertices[v]).z;
                    if (z < near) near = z;
                    if (z > far) far = z;
                }
            }

            if (baked != null) Object.DestroyImmediate(baked);
        }

        static float Y(Animator animator, HumanBodyBones which)
        {
            Transform? bone = animator.isHuman ? animator.GetBoneTransform(which) : null;
            return bone != null ? bone.position.y : 0f;
        }

        static string Name(Animator animator, HumanBodyBones which)
        {
            Transform? bone = animator.isHuman ? animator.GetBoneTransform(which) : null;
            return bone != null ? bone.name : "none";
        }

        /// <summary>What <c>PawnFigureDirector.ApplySleepPose</c> does to the limbs, at full weight.</summary>
        static void LayLimbs(GameObject instance, Animator animator, in SleepPose.Posture posture)
        {
            if (!animator.isHuman) return;
            Vector3 axis = instance.transform.right;

            Pitch(animator, HumanBodyBones.RightUpperArm, axis, posture.RightArm);
            Pitch(animator, HumanBodyBones.LeftUpperArm, axis, posture.LeftArm);
            Pitch(animator, HumanBodyBones.RightLowerArm, axis, posture.RightElbow);
            Pitch(animator, HumanBodyBones.LeftLowerArm, axis, posture.LeftElbow);
            Pitch(animator, HumanBodyBones.RightUpperLeg, axis, posture.Hip);
            Pitch(animator, HumanBodyBones.LeftUpperLeg, axis, posture.Hip);
            Pitch(animator, HumanBodyBones.RightLowerLeg, axis, posture.Knee);
            Pitch(animator, HumanBodyBones.LeftLowerLeg, axis, posture.Knee);
        }

        static void Pitch(Animator animator, HumanBodyBones which, Vector3 axis, float degrees)
        {
            Transform? bone = animator.GetBoneTransform(which);
            if (bone == null) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
