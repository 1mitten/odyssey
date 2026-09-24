#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// A colonist sitting by a fire is drawn sitting (design 31 §18d): every colonist row carries a
    /// settled idle, and that idle, <b>measured off the drawn mesh</b>, is lower than standing with
    /// its feet still on the floor.
    ///
    /// <para>Measured rather than trusted to the clip's name, which is the lesson of
    /// <c>FigureBuildTests</c>: a name says what somebody meant, and the mesh says what the player
    /// sees. The two ways this could be wrong both look like a figure — a clip that barely lowers
    /// the body reads as standing, and one that lowers the hips with the root puts the feet through
    /// the ground.</para>
    ///
    /// <para>They need the packs and ignore themselves without them, asking whether the rows'
    /// art <b>resolved</b> rather than whether there is a catalogue: the catalogue is committed and
    /// loads perfectly with every clip null on the runner.</para>
    /// </summary>
    public class SitPoseTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        static ModuleCatalogue? Catalogue() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);

        /// <summary>The colonist rows whose body resolved on this machine.</summary>
        static List<ModuleEntry> ResolvedColonists()
        {
            var rows = new List<ModuleEntry>();
            ModuleCatalogue? catalogue = Catalogue();
            if (catalogue == null) return rows;
            foreach (ModuleEntry row in catalogue.FindFamily(ModuleIds.ColonistBase))
                if (row.prefab != null && row.poseClip != null) rows.Add(row);
            return rows;
        }

        [Test]
        public void EveryColonistRowCanSit()
        {
            List<ModuleEntry> rows = ResolvedColonists();
            if (rows.Count == 0) Assert.Ignore("no colonist art on this machine; the packs are gitignored");

            foreach (ModuleEntry row in rows)
            {
                Assert.That(row.sitClipName, Is.Not.Empty, $"{row.prefabName} names no settled idle");
                Assert.That(row.sitClip, Is.Not.Null,
                    $"{row.prefabName}'s settled idle {row.sitClipName} did not resolve, so it will " +
                    "stand at the fire; rebuild the catalogue");
            }
        }

        /// <summary>
        /// The seated pose is really lower, and really grounded. One row of each sex, because the
        /// pack authors the two separately and the catalogue matches them by name.
        /// </summary>
        [Test]
        public void TheSeatIsLowerThanStandingWithTheFeetStillOnTheFloor()
        {
            List<ModuleEntry> rows = ResolvedColonists();
            if (rows.Count == 0) Assert.Ignore("no colonist art on this machine; the packs are gitignored");

            var measured = new HashSet<string>();
            foreach (ModuleEntry row in rows)
            {
                if (row.sitClip == null || !measured.Add(row.sitClip.name)) continue;

                Measure(row, row.poseClip!, out float standSole, out float standCrown);
                Measure(row, row.sitClip, out float sitSole, out float sitCrown);
                float height = standCrown - standSole;

                TestContext.WriteLine(
                    $"{row.prefabName} / {row.sitClip.name}: standing {standSole:0.000}..{standCrown:0.000} m, " +
                    $"seated {sitSole:0.000}..{sitCrown:0.000} m, crown at {(sitCrown - sitSole) / height:P0} of standing");

                Assert.That(sitCrown - standSole, Is.LessThan(height * 0.8f),
                    $"{row.sitClip.name} lowers the crown by less than a fifth; at the play camera " +
                    "that reads as standing");
                Assert.That(sitSole - standSole, Is.EqualTo(0f).Within(0.12f),
                    $"{row.sitClip.name} moves the lowest point of the body by {sitSole - standSole:0.000} m; " +
                    "its feet are through the floor or off it");
            }

            Assert.That(measured, Is.Not.Empty, "no row had a settled idle to measure");
        }

        /// <summary>The drawn mesh's lowest and highest points under one clip, as the director poses it.</summary>
        static void Measure(ModuleEntry row, AnimationClip clip, out float lowest, out float highest)
        {
            GameObject instance = Object.Instantiate(row.prefab!);
            PlayableGraph graph = default;
            try
            {
                instance.transform.position = Vector3.zero;
                instance.transform.localScale = row.scale.sqrMagnitude < 1e-6f ? Vector3.one : row.scale;

                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                graph = PlayableGraph.Create("Odyssey sit pose test");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                graph.Evaluate(0f);

                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                FigureBuild.DrawnExtent(skins, out lowest, out highest);
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(instance);
            }
        }
    }
}
