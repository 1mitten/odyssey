#nullable enable
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The Research and Inventory windows as UI Toolkit actually lays them out (designs 34, 35):
    /// the exact size the specs give, the same size whatever is showing, and nothing drawn outside
    /// the frame. The fast tier holds the arithmetic; only a laid-out tree can say a row really is
    /// 30 px with its rule inside it, and that a pane's content did not push past the 420 px body.
    /// </summary>
    public class DockedTabGeometryTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";

        [UnityTest]
        public IEnumerator BothWindowsAreTheirSpecifiedSizeAndNothingSpillsOut()
        {
            GameObject root = Build();
            try
            {
                yield return new WaitForSecondsRealtime(0.3f);
                for (int i = 0; i < 3; i++) yield return null;

                var boot = root.GetComponentInChildren<OdysseyBootstrap>();
                var doc = root.GetComponentInChildren<UIDocument>();
                HudDirectors? directors = boot!.Directors;
                if (directors == null)
                {
                    Assert.Ignore("no session, so there is no tab to open");
                    yield break;
                }

                directors.Research.SetOpen(true);
                for (int i = 0; i < 5; i++) yield return null;
                VisualElement research = doc!.rootVisualElement.Q("research");
                AssertWindow(research, ResearchLayout.Width, ResearchLayout.Height, "Research");

                // Start a project: the table re-sorts and the detail pane grows a second button and
                // loses the locked line. Neither may change the window.
                directors.Research.Start(ResearchCatalogue.GeneratorsKey);
                for (int i = 0; i < 3; i++) yield return null;
                AssertWindow(research, ResearchLayout.Width, ResearchLayout.Height, "Research, researching");

                directors.Inventory.SetOpen(true);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(directors.Research.Open, Is.False, "the two tabs share a corner, so one closes the other");
                Assert.That(research.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

                VisualElement inventory = doc.rootVisualElement.Q("inventory");
                AssertWindow(inventory, InventoryLayout.Width, InventoryLayout.Height, "Inventory");

                foreach (VisualElement row in inventory.Query(className: "inventory__row").ToList())
                    if (row.resolvedStyle.display == DisplayStyle.Flex)
                        Assert.That(row.layout.height, Is.EqualTo(InventoryLayout.RowHeight).Within(0.01f),
                            "a row is 30 px with its rule inside it, or fourteen of them overflow the body");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void AssertWindow(VisualElement? window, int width, int height, string what)
        {
            Assert.That(window, Is.Not.Null, $"{what} is not in the tree");
            Assert.That(window!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), $"{what} did not open");
            Assert.That(window.layout.width, Is.EqualTo(width).Within(0.01f), $"{what} width");
            Assert.That(window.layout.height, Is.EqualTo(height).Within(0.01f), $"{what} height");

            Rect frame = window.worldBound;
            var spills = new List<string>();
            Walk(window, frame, spills);
            Assert.That(spills, Is.Empty, $"{what} draws outside its own frame: " + string.Join("; ", spills));
        }

        static void Walk(VisualElement element, Rect frame, List<string> spills)
        {
            for (int i = 0; i < element.childCount; i++)
            {
                VisualElement child = element[i];
                if (child.resolvedStyle.display == DisplayStyle.None) continue;
                if (child.resolvedStyle.visibility == Visibility.Hidden) continue;
                Rect box = child.worldBound;
                const float slack = 0.5f;
                if (box.width > 0f && box.height > 0f &&
                    (box.xMin < frame.xMin - slack || box.xMax > frame.xMax + slack ||
                     box.yMin < frame.yMin - slack || box.yMax > frame.yMax + slack))
                    spills.Add($"{child.GetType().Name} '{child.name}' {string.Join(" ", child.GetClasses())} at {box}");
                Walk(child, frame, spills);
            }
        }

        static GameObject Build()
        {
            var root = new GameObject("DockedTabGeometry");

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            var rig = cameraObject.AddComponent<SliceCameraRig>();

            var bootObject = new GameObject("Bootstrap");
            bootObject.transform.SetParent(root.transform, false);
            bootObject.SetActive(false);
            var boot = bootObject.AddComponent<OdysseyBootstrap>();
            boot.buildOnPlay = true;
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.layers = 8;
            boot.seed = 1;
            boot.barrenMap = true;
            boot.grassScatter = 0;
            boot.cameraRig = rig;
            bootObject.AddComponent<SelectionPresenter>();

            var doc = bootObject.AddComponent<UIDocument>();
#if UNITY_EDITOR
            doc.panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
#endif
            if (doc.panelSettings == null)
            {
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
