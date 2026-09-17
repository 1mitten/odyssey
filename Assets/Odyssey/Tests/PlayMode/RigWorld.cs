#nullable enable
using System.Collections;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

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
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";

        /// <summary>
        /// The same world with the HUD on top of it, for the tests that are about the interface
        /// standing between the pointer and the world.
        /// </summary>
        /// <param name="buildOnPlay">
        /// False lands the rig on the start screen with no session built, which is what pressing
        /// Play does since U38. Every other caller wants a colony and says so.
        /// </param>
        public static GameObject BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig rig,
            out HudShell shell, bool buildOnPlay = true)
        {
            GameObject root = Build(out boot, out rig, active: false);
            boot.buildOnPlay = buildOnPlay;
            GameObject bootObject = boot.gameObject;

            var doc = bootObject.AddComponent<UIDocument>();
#if UNITY_EDITOR
            doc.panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
#endif
            if (doc.panelSettings == null)
            {
                doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                doc.panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            }

            shell = bootObject.AddComponent<HudShell>();
#if UNITY_EDITOR
            shell.hudStyles = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);
#endif
            bootObject.AddComponent<SelectionPresenter>();
            bootObject.SetActive(true);
            return root;
        }

        public static GameObject Build(out OdysseyBootstrap boot, out SliceCameraRig rig) =>
            Build(out boot, out rig, active: true);

        static GameObject Build(out OdysseyBootstrap boot, out SliceCameraRig rig, bool active)
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
            // Explicitly, not by default: since U38 pressing Play lands on the start screen, and
            // what this rig is asserting is that a session exists.
            boot.buildOnPlay = true;
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.layers = 8;
            boot.seed = 1;
            boot.barrenMap = true;
            boot.grassScatter = 0;
            boot.cameraRig = rig;
            // A HUD build keeps the object inactive so the shell and the document land before
            // Start runs; a bare rig world has nothing more to add and can start at once.
            if (active) bootObject.SetActive(true);
            return root;
        }

        public static IEnumerator WarmUp()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>
        /// Wait until the camera has stopped moving of its own accord.
        ///
        /// <para>The rig smooths toward its target every frame, and after a warm-up it is still
        /// arriving — measured by the control in <c>InputHarnessTests</c>, which caught the camera
        /// drifting with no input at all and would have made any input test meaningless. Any probe
        /// of "did input move the camera" has to start from a camera that is not already moving.</para>
        /// </summary>
        public static IEnumerator SettleCamera(SliceCameraRig rig, float tolerance = 0.0005f)
        {
            float last = rig.distance;
            for (int frame = 0; frame < 240; frame++)
            {
                yield return null;
                float now = rig.distance;
                if (Mathf.Abs(now - last) <= tolerance && !rig.GlideTarget.HasValue) yield break;
                last = now;
            }
        }
    }
}
