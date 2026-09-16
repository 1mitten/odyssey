#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The HUD's view-level smoke test: the first test anywhere in the project that asserts on
    /// a live UI Toolkit panel (experiment R4 of <c>docs/research/g-02-unity-ui-framework.md</c>
    /// — the playmode gate runs with a graphics device, so this proves the player-loop half;
    /// whether a panel also resolves under <c>-nographics</c> stays open).
    ///
    /// Thin on purpose, per the test layout in design 09 §5: element existence and binding,
    /// not looks. What it asserts is the contract the shell promises — every region built,
    /// the roster bound to the published frame, and the inspect pane answering a selection by
    /// name — which is everything a later screenshot cannot check for us.
    /// </summary>
    public class HudSmokeTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";

        [UnityTest]
        public IEnumerator TheHudBuildsEveryRegionFromTheFrame()
        {
            GameObject root = Build(out OdysseyBootstrap boot);
            try
            {
                yield return WarmUp();

                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");

                var doc = root.GetComponentInChildren<UIDocument>();
                Assert.That(doc, Is.Not.Null, "the HUD document is on the bootstrap object");
                Assert.That(doc!.rootVisualElement, Is.Not.Null, "the panel has no root: no panel settings?");

                // The panel scales with the screen against the mockup's canvas. At constant pixel
                // size a 4K monitor showed nine-pixel labels nine pixels tall, which is no HUD.
                Assert.That(doc.panelSettings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
                Assert.That(doc.panelSettings.referenceResolution, Is.EqualTo(new Vector2Int(1200, 800)));

                // Every region of the screen map, built once by the shell: ledger, architect,
                // clock, alerts, ruler, inspect, overlays — seven framed regions.
                Assert.That(doc.rootVisualElement.Query(className: "region").ToList().Count,
                    Is.EqualTo(7), "the HUD did not build every region");

                // The roster is bound to the frame: one card per published pawn.
                int cards = doc.rootVisualElement.Query(className: "card").ToList().Count;
                Assert.That(cards, Is.EqualTo(boot.World!.Views.Current.PawnCount),
                    "the roster bar does not match the published pawn count");

                // The ruler covers every layer.
                Assert.That(doc.rootVisualElement.Query(className: "ruler__row").ToList().Count,
                    Is.EqualTo(boot.Model!.Size.SizeY));

                // Four speed buttons, and the clock has a time in it.
                Assert.That(doc.rootVisualElement.Query(className: "speed__btn").ToList().Count,
                    Is.EqualTo(4));
                Label? clock = doc.rootVisualElement.Q<Label>(className: "clock__time");
                Assert.That(clock, Is.Not.Null);
                Assert.That(clock!.text, Is.Not.Empty, "the clock label never bound");

                // The theme applied: a label resolves a font. An empty theme leaves every label
                // fontless and the panel draws nothing at all, which no query on the tree can
                // see. This is the assertion that would have caught the first HUD build.
                var font = clock.resolvedStyle.unityFontDefinition;
                Assert.That(font.fontAsset != null || font.font != null || clock.resolvedStyle.unityFont != null, Is.True,
                    "no font resolved on a label: the runtime theme does not import unity-theme://default");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator SelectingAColonistFillsTheInspectPane()
        {
            GameObject root = Build(out OdysseyBootstrap boot);
            try
            {
                yield return WarmUp();

                Assert.That(boot.World, Is.Not.Null);
                Assert.That(boot.World!.Views.Current.PawnCount, Is.GreaterThan(0),
                    "the scenario placed no colonists to select");

                PawnId pawn = boot.World.Views.Current.Pawns[0].Id;
                root.GetComponentInChildren<SelectionReadout>().SelectPawn(pawn);
                yield return null; // the resolved event answers in the frame it happened

                var doc = root.GetComponentInChildren<UIDocument>();
                Label? title = doc!.rootVisualElement.Q<Label>(className: "inspect__title");
                Assert.That(title, Is.Not.Null, "the inspect pane never built a header");
                Assert.That(title!.text, Is.EqualTo(ColonistNames.Of(pawn)),
                    "the pane does not show the selected colonist by name");

                // A card click also takes the camera to the colonist: the rig glides to their
                // cell on its own smoothing, and lands within a fraction of a second.
                var rig = boot.cameraRig!;
                CellRef at = boot.World.Views.Current.Pawns[0].Cell;
                Assert.That(rig.ActiveLayer, Is.EqualTo(at.Y), "the rig changes to the colonist's layer");
                float waited = 0f;
                while (rig.GlideTarget.HasValue && waited < 3f) { waited += Time.unscaledDeltaTime; yield return null; }
                Vector3 centre = CellMetrics.FloorCentre(at);
                Assert.That(rig.Focus.x, Is.EqualTo(centre.x).Within(0.05f), "the camera glided to the colonist");
                Assert.That(rig.Focus.z, Is.EqualTo(centre.z).Within(0.05f));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Real seconds, not frame counts: the shell primes on the first frame a world exists,
        /// and the cadence buckets after that are wall-clock, which in this harness advances
        /// by well under a millisecond a frame. Waiting on frames would not let a bucket fire.
        /// </summary>
        static IEnumerator WarmUp()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>
        /// The play scene's HUD stack, built by hand on a small world: camera with rig, sun,
        /// bootstrap, pick resolver, document and shell, with the real generated panel asset
        /// and the authored stylesheet — the same assets the scene carries.
        /// </summary>
        static GameObject Build(out OdysseyBootstrap boot)
        {
            var root = new GameObject("HudSmoke");

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            var rig = cameraObject.AddComponent<SliceCameraRig>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
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
            boot.showReadout = false;
            bootObject.AddComponent<SelectionReadout>();

            var doc = bootObject.AddComponent<UIDocument>();
#if UNITY_EDITOR
            doc.panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
#endif
            if (doc.panelSettings == null)
            {
                // A clone without the generated asset still gets a working panel.
                doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                doc.panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            }

            var shell = bootObject.AddComponent<HudShell>();
#if UNITY_EDITOR
            shell.hudStyles = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);
#endif
            bootObject.SetActive(true);
            return root;
        }
    }
}
