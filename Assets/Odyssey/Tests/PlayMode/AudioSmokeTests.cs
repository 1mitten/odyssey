#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The audio system's view-level smoke test: the composition root builds it into a live
    /// world and a few frames of the real player loop step it without an error.
    ///
    /// Thin on purpose, the same bargain <see cref="HudSmokeTests"/> makes: everything with
    /// arithmetic in it lives in the EditMode suite; what only the player loop can prove is that
    /// the director is constructed, driven from LateUpdate, and torn down with the world. The
    /// world here is barren on purpose — with no catalogue asset assigned the whole path runs
    /// silent, which is exactly the clone-without-audio case worth holding in CI.
    /// </summary>
    public class AudioSmokeTests
    {
        [UnityTest]
        public IEnumerator TheBootstrapBuildsAndStepsTheAudioDirector()
        {
            GameObject root = Build(out OdysseyBootstrap boot);
            try
            {
                yield return WarmUp();

                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");

                Transform audio = root.transform.Find("Bootstrap/Odyssey Audio");
                Assert.That(audio, Is.Not.Null,
                    "the composition root builds the audio pool with the world, not on demand");

                // Sixteen voices, the two music channels and the two outdoor-bed channels, warm
                // on construction: a pool built lazily is a pool whose first sound pays for it.
                // Each sits on a child of its own, because a source is spatialised from the
                // transform it shares.
                Assert.That(audio!.GetComponentsInChildren<AudioSource>(includeInactive: true).Length,
                    Is.EqualTo(20),
                    "the pool exists before anything asks it for a sound");
                Assert.That(audio.GetComponents<AudioSource>().Length, Is.Zero,
                    "no two voices share a transform, or they would share a position");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        static IEnumerator WarmUp()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>The smallest live world the bootstrap can run: camera with rig, sun, world.
        /// No HUD and no catalogue — neither is what this test is about.</summary>
        static GameObject Build(out OdysseyBootstrap boot)
        {
            var root = new GameObject("AudioSmoke");

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            var rig = cameraObject.AddComponent<SliceCameraRig>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(72f, 35f, 0f);

            var bootObject = new GameObject("Bootstrap");
            bootObject.transform.SetParent(root.transform, false);
            bootObject.SetActive(false);   // so the fields land before Start runs
            boot = bootObject.AddComponent<OdysseyBootstrap>();
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.layers = 8;
            boot.seed = 1;
            boot.barrenMap = true;
            boot.grassScatter = 0;
            boot.cameraRig = rig;
            bootObject.SetActive(true);
            return root;
        }
    }
}
