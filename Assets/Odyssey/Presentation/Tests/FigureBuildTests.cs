#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// <b>The colonist the director measures is the colonist the player sees.</b>
    ///
    /// <para>This is the test that was missing. Every length in <c>PawnFigureDirector</c> is a
    /// fraction of the figure's own build, measured off the rig rather than written down — which
    /// is right, and which means the whole of that arithmetic rests on one measurement nobody had
    /// ever checked. It was wrong: <c>StandingHipHeight</c> reads
    /// <c>animator.GetBoneTransform(HumanBodyBones.Hips)</c>, and the Synty humanoid avatar maps
    /// <c>Hips</c> to a bone literally named <c>Root</c> that sits at the model origin with the
    /// real pelvis as its child. Nought on every one of the sixty-one characters, clamped up to
    /// 0.2 m, and a 2.49 m colonist was laid down in her bed 0.38 m long
    /// (<c>docs/design/20-beds.md</c> §7b).</para>
    ///
    /// <para><b>Everything downstream went on working, which is why it took two owner reports.</b>
    /// 0.38 m is a number; <c>SleepPose</c> laid a body of that length perfectly correctly, and the
    /// arithmetic that was read and pronounced right was right. Only the measurement was wrong, and
    /// nothing asked it whether it looked like a person.</para>
    ///
    /// <para><b>These need the packs</b>, because the question is about a real rig, and they ignore
    /// themselves without them — a clone with no Synty content draws colonists down the baked
    /// instanced path and has no figure to measure.</para>
    /// </summary>
    public class FigureBuildTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>
        /// A colonist instantiated and driven exactly as <c>PawnFigureDirector.Create</c> does it:
        /// the catalogue's own prefab and scale, an animator, a graph playing the first gait, and
        /// <c>Evaluate(0)</c> before anything measures — a graph played but never evaluated leaves
        /// the skeleton in whatever pose the prefab was saved in.
        /// </summary>
        static GameObject? Colonist(out SkinnedMeshRenderer[] skins, out PlayableGraph graph)
        {
            skins = System.Array.Empty<SkinnedMeshRenderer>();
            graph = default;

            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            if (catalogue == null) return null;

            foreach (ModuleEntry row in catalogue.FindFamily(ModuleIds.ColonistBase))
            {
                if (row.prefab == null) continue;

                AnimationClip? idle = null;
                for (int i = 0; i < row.locomotion.Count && idle == null; i++)
                    if (row.locomotion[i].clip != null) idle = row.locomotion[i].clip;
                if (idle == null) continue;

                GameObject instance = Object.Instantiate(row.prefab);
                instance.transform.localScale =
                    row.scale.sqrMagnitude < 1e-6f ? Vector3.one : row.scale;

                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                graph = PlayableGraph.Create("Odyssey figure build test");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var clip = AnimationClipPlayable.Create(graph, idle);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                output.SetSourcePlayable(clip);
                graph.Play();
                graph.Evaluate(0f);

                skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                return instance;
            }

            return null;
        }

        /// <summary>
        /// <b>A colonist measures like a person, not like a boot.</b>
        ///
        /// <para>The measured cast, 2026-09-20: the body skin runs 0.000 m to 2.488 m at the
        /// catalogue's 1.4 scale. The bounds here are deliberately loose — this is not a test of
        /// how tall the art is, which is the owner's business, but of whether the measurement is
        /// measuring the body at all. The value it replaced was 0.38 m and would fail it by a
        /// factor of four.</para>
        /// </summary>
        [Test]
        public void AColonistMeasuresLikeAPerson()
        {
            GameObject? instance = Colonist(out SkinnedMeshRenderer[] skins, out PlayableGraph graph);
            if (instance == null)
                Assert.Ignore("no colonist art on this machine; the packs are gitignored");

            try
            {
                float body = FigureBuild.Height(skins, instance!.transform.position.y,
                    FigureBuild.FallbackHeight);

                Assert.That(body, Is.Not.EqualTo(FigureBuild.FallbackHeight).Within(0.001f),
                    "the measurement fell through to its fallback, so it measured nothing");
                Assert.That(body, Is.GreaterThan(1.5f).And.LessThan(3.5f),
                    $"a colonist measured {body:0.000} m, which is not a person");
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// And the body it measures is longer than the bone the old measurement read, by a lot.
        ///
        /// <para><b>Named rather than implied</b>, because the failure was a length that was
        /// quietly six times too small and every assertion around it passed. This asserts the
        /// difference itself: whatever the rig, the drawn body is a multiple of the distance from
        /// the figure's root to the bone the humanoid avatar calls <c>Hips</c> — which on this cast
        /// is the floor.</para>
        /// </summary>
        [Test]
        public void TheAvatarsHipsBoneIsNotABodyLength()
        {
            GameObject? instance = Colonist(out SkinnedMeshRenderer[] skins, out PlayableGraph graph);
            if (instance == null)
                Assert.Ignore("no colonist art on this machine; the packs are gitignored");

            try
            {
                var animator = instance!.GetComponent<Animator>();
                Assume.That(animator, Is.Not.Null);
                Assume.That(animator.isHuman, Is.True, "a non-humanoid rig has no bones to confuse");

                Transform? hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Assume.That(hips, Is.Not.Null);

                float asHip = hips!.position.y - instance.transform.position.y;
                float body = FigureBuild.Height(skins, instance.transform.position.y,
                    FigureBuild.FallbackHeight);

                // Not a claim that the avatar is wrong to map it there - a rig's conventions are
                // its own - but a record that reading this bone as a height gives nothing, so that
                // nobody derives a length from it again.
                Assert.That(asHip, Is.LessThan(body * 0.5f),
                    $"the avatar's Hips bone stands {asHip:0.000} m up on a {body:0.000} m figure, " +
                    "so it is no longer the floor-level root this was written against - " +
                    "re-read docs/design/20-beds.md §7b before deriving anything from it");
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// <b>And a colonist of that size, laid in a bed, is on the bed.</b> The end-to-end form:
        /// the measured body, the bed's own pillow, and the footprint the order claimed — which is
        /// the thing the owner was looking at when they reported it twice.
        /// </summary>
        [Test]
        public void AMeasuredColonistLiesWithinTheBed()
        {
            GameObject? instance = Colonist(out SkinnedMeshRenderer[] skins, out PlayableGraph graph);
            if (instance == null)
                Assert.Ignore("no colonist art on this machine; the packs are gitignored");

            try
            {
                float body = SleepPose.BodyLength(
                    FigureBuild.Height(skins, instance!.transform.position.y, FigureBuild.FallbackHeight));

                // Bed-local Z: the footprint is two cells centred on the bed's origin, the head
                // cell is the near half, and the head rests on the pillow.
                float halfSpan = CellMetrics.SizeXZ;
                float head = BedShape.HeadRestAlong;
                float feet = head + body;

                Assert.That(head, Is.GreaterThanOrEqualTo(-halfSpan), "the head is off the head end");
                Assert.That(feet, Is.LessThanOrEqualTo(halfSpan),
                    $"the feet are {feet - halfSpan:0.00} m past the foot of the bed");

                // And the owner's own words for it: head on the first tile, the rest of the body
                // on to the second. The head cell is bed-local [-2.5, 0].
                Assert.That(head, Is.LessThanOrEqualTo(0f), "the head is not over the head cell");
                Assert.That(feet, Is.GreaterThan(0f),
                    "the whole colonist fits in the head cell, so the second tile is doing nothing");
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }
    }
}
