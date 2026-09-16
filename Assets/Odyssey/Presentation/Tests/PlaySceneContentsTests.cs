#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What is actually inside <c>Assets/Scenes/Play.unity</c>, which until now nothing asserted.
    ///
    /// <para><b>The scene is generated and committed, and those two facts fight each other.</b> It
    /// is rebuilt by <c>PlayScene.Build</c> only when somebody remembers, so it drifts behind the
    /// code that reads it — and it drifts silently, because every PlayMode test builds its own
    /// bootstrap GameObject by hand and never opens this file. Two bugs have already come out of
    /// that: the <c>M</c> key doing nothing because <c>DesignatePresenter</c> was not on the scene,
    /// and a slice policy that turned out to be innocent but cost an afternoon to clear.</para>
    ///
    /// <para>These tests open the real committed scene and read it.</para>
    /// </summary>
    public class PlaySceneContentsTests
    {
        const string ScenePath = "Assets/Scenes/Play.unity";

        static T FindOne<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(includeInactive: true);
                if (found != null) return found;
            }
            return null!;
        }

        /// <summary>
        /// <b>A field the scene has never heard of is not a default; it is whatever the
        /// deserialiser leaves behind — and this test exists because that had to be measured.</b>
        ///
        /// <para><c>SliceSettings</c> is a <c>[Serializable]</c> field on the rig, so the scene's
        /// copy, not the field initialiser, is what the game runs with. The committed scene was
        /// written before <c>followDepth</c> and <c>surfaceLayer</c> existed and its YAML contains
        /// neither. That looked like a very good candidate for "a click will not leave the active
        /// layer": with <c>followDepth</c> off, <c>above</c> is obeyed literally, and the scene's
        /// <c>above</c> is <c>3</c> — <c>Xray</c>, under which nothing above the slice is a pointer
        /// target at all.</para>
        ///
        /// <para><b>It was not the cause.</b> Measured: Unity keeps the field initialiser for a
        /// field missing from the YAML, so <c>followDepth</c> loads <c>true</c>, the depth-following
        /// default wins, and at L12 the slice resolves to <c>Full</c> above with L9–L15 selectable.
        /// The scene is fine. The hypothesis was wrong and the measurement is kept, because the
        /// next person to look at that YAML will have exactly the same idea.</para>
        ///
        /// <para>It is worth holding as a guard regardless: if anyone ever rebuilds the scene with
        /// <c>followDepth</c> off, or writes it off by hand, this is the only thing that would
        /// notice before a playtest did.</para>
        /// </summary>
        [Test]
        public void TheSceneOpensWithTheDepthFollowingSlice()
        {
            Assert.That(File.Exists(ScenePath), Is.True, $"{ScenePath} is missing");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var rig = FindOne<SliceCameraRig>(scene);
                Assert.That(rig, Is.Not.Null, "the scene has no SliceCameraRig");

                SliceSettings slice = rig.slice;
                Console.WriteLine(
                    $"[Scene] slice as loaded: followDepth={slice.followDepth}, above={slice.above}, " +
                    $"aboveDepth={slice.aboveDepth}, below={slice.below}, belowDepth={slice.belowDepth}, " +
                    $"surfaceLayer={slice.surfaceLayer}");

                // What that resolves to for a slice sitting on the surface, which is where the game
                // opens. Anything but "the whole stack above" means a click cannot leave the layer.
                const int Active = 12, Layers = 16;
                slice.surfaceLayer = Active;
                Console.WriteLine(
                    $"[Scene] at L{Active}: above={slice.AboveAt(Active)}, " +
                    $"selectable L{slice.LowestSelectableLayer(Active)}..L{slice.HighestSelectableLayer(Active, Layers)}");

                Assert.That(slice.followDepth, Is.True,
                    "the scene's slice does not follow the depth, so `above` is obeyed literally — " +
                    "and the scene's `above` is Xray, under which nothing above the slice is a " +
                    "pointer target. Rebuild the scene, or stop depending on a serialised default.");

                Assert.That(slice.HighestSelectableLayer(Active, Layers), Is.GreaterThan(Active),
                    "a click cannot reach above the active layer in the scene as committed");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        /// <summary>
        /// The pieces that make the scene playable, and which of them the scene itself must carry.
        ///
        /// <para><c>SelectionPresenter</c> must: nothing adds it at runtime, so a scene without it
        /// is a scene where clicking a colonist does nothing.</para>
        ///
        /// <para><c>DesignatePresenter</c> is deliberately <b>not</b> asserted here. It landed in
        /// "A player can give an order", the scene was last generated by a branch that did not
        /// contain it, and the keys did nothing for four consecutive playtests — after which
        /// <c>OdysseyBootstrap</c> was given a self-heal that adds the component and logs a
        /// warning. The committed scene still lacks it, and that is now tolerated by design, so
        /// asserting its presence here would fail the tier over a condition the code handles. The
        /// state is reported instead.</para>
        /// </summary>
        [Test]
        public void TheSceneCarriesThePresentersThatMakeItPlayable()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(FindOne<OdysseyBootstrap>(scene), Is.Not.Null, "no OdysseyBootstrap");
                Assert.That(FindOne<SliceCameraRig>(scene), Is.Not.Null, "no SliceCameraRig");
                Assert.That(FindOne<SelectionPresenter>(scene), Is.Not.Null,
                    "no SelectionPresenter: clicking a colonist would do nothing");

                bool designate = FindOne<DesignatePresenter>(scene) != null;
                Console.WriteLine($"[Scene] DesignatePresenter on the scene: {designate}" +
                                  (designate ? "" : " (self-healed at runtime by OdysseyBootstrap)"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
