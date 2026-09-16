#nullable enable
using System.Text;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// What a colonist's hand is actually made of.
    ///
    /// <para>Written to answer one question before any code was changed on the strength of a
    /// guess: <b>can these hands grip at all?</b> A fist is fingers, and if the Synty humanoid maps
    /// no finger bones then no rotation applied to the wrist will ever make a hand close round a
    /// haft — the mesh is modelled open and stays open, and the only honest fix is a different
    /// hand mesh or none. If the fingers *are* mapped, they can be curled and the question becomes
    /// tuning.</para>
    ///
    /// <para>It also prints the hand's local axes against the forearm, because which way a hand
    /// bone points is a decision made by whoever rigged the character — the same trap
    /// <c>PawnFigureDirector</c> avoids for the swing by pitching about the figure's own axis
    /// rather than the bone's, and the same one <c>GripTool</c> avoids by finding the haft from
    /// mesh bounds instead of trusting a prefab's orientation.</para>
    ///
    /// <para><c>Odyssey → Presentation → Probe a colonist's hands</c>, or
    /// <c>scripts/unity.sh exec Odyssey.EditorTools.HandProbe.Run</c>.</para>
    /// </summary>
    public static class HandProbe
    {
        [MenuItem("Odyssey/Presentation/Probe a colonist's hands")]
        public static void RunFromMenu() => Execute(false);

        public static void Run() => Execute(Application.isBatchMode);

        static readonly HumanBodyBones[] Fingers =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate,
            HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate,
            HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate,
            HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate,
            HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal,
        };

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? instance = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                if (catalogue == null)
                {
                    Debug.LogError("[Hands] no catalogue; nothing to probe.");
                    exitCode = 1;
                    return;
                }

                // Any colonist will do: all 61 come off the same Polygon humanoid rig, and if that
                // were not true the swing would already be wrong on some of them.
                ModuleEntry? row = catalogue.Find(ModuleIds.Colonist(0));
                GameObject? prefab = row != null ? row.prefab : null;
                if (prefab == null)
                {
                    Debug.LogError("[Hands] no colonist prefab; the packs are probably absent.");
                    exitCode = 1;
                    return;
                }

                instance = Object.Instantiate(prefab);
                var animator = instance.GetComponent<Animator>();
                if (animator == null || !animator.isHuman)
                {
                    Debug.LogError($"[Hands] {prefab.name} has no humanoid animator; " +
                                   "no bone can be asked for by role at all.");
                    exitCode = 1;
                    return;
                }

                var report = new StringBuilder();
                report.Append("[Hands] ").Append(prefab.name).AppendLine();

                int mapped = 0;
                foreach (HumanBodyBones bone in Fingers)
                {
                    if (animator.GetBoneTransform(bone) == null) continue;
                    mapped++;
                    report.Append("  finger bone: ").Append(bone).AppendLine();
                }

                report.Append("  finger bones mapped: ").Append(mapped)
                      .Append(" of ").Append(Fingers.Length).AppendLine();

                Transform? hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform? lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                if (hand != null && lower != null)
                {
                    // Which way the forearm runs, expressed in the hand's own space: that is the
                    // axis a held haft lies across, and it cannot be assumed.
                    Vector3 alongArm = hand.InverseTransformDirection(
                        (hand.position - lower.position).normalized);
                    report.Append("  forearm direction in hand space: ").Append(alongArm.ToString("F3"))
                          .AppendLine();
                    report.Append("  hand local axes vs figure: right ")
                          .Append(hand.right.ToString("F2")).Append(" up ")
                          .Append(hand.up.ToString("F2")).Append(" fwd ")
                          .Append(hand.forward.ToString("F2")).AppendLine();
                }

                // How many renderers the hand carries, which says whether the fingers are separate
                // geometry that could be swapped, or part of one body mesh that cannot.
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                report.Append("  skinned meshes on the character: ").Append(skins.Length).AppendLine();

                Debug.Log(report.ToString());
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
