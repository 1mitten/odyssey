#nullable enable
using System.Collections;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using UnityEngine;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A small live world with a camera rig on it: the least a PlayMode test needs to ask
    /// whether input reaches the game.
    ///
    /// <para>No HUD, deliberately. The question these tests ask is whether the wheel reaches the
    /// camera at all, and a panel over the pointer is the thing that would stop it — so the
    /// panel arrives later, in its own test, as the subject rather than as scenery.</para>
    /// </summary>
    public static class RigWorld
    {
        public static GameObject Build(out OdysseyBootstrap boot, out SliceCameraRig rig)
        {
            var root = new GameObject("RigWorld");

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            rig = cameraObject.AddComponent<SliceCameraRig>();

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

        public static IEnumerator WarmUp()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < 3; i++) yield return null;
        }
    }
}
