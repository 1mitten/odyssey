#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// The Research, Inventory and Assign windows as UI Toolkit actually lays them out (designs 34,
    /// 35, 43):
    /// the exact size the specs give, the same size whatever is showing, and nothing drawn outside
    /// the frame. The fast tier holds the arithmetic; only a laid-out tree can say a row really is
    /// 30 px with its rule inside it, and that a pane's content did not push past the 420 px body.
    /// </summary>
    public class DockedTabGeometryTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";

        /// <summary>How far a scaled layout may sit off a whole number: one physical pixel.</summary>
        const float PixelGrid = 1f;

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
                Assert.That(directors.Research.Start(ResearchCatalogue.ElectricityKey), Is.True);
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
                        Assert.That(row.layout.height, Is.EqualTo(InventoryLayout.RowHeight).Within(PixelGrid),
                            "a row is 30 px with its rule inside it, or fourteen of them overflow the body");

                // The Assign tab (design 43 §6): 536 wide, its height the page's, and it takes the
                // corner from Inventory. Choosing a colonist — what a press on a name does — leaves
                // it open, where every other docked tab gives the corner to the inspect pane.
                directors.Assign.SetOpen(true);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(directors.Inventory.Open, Is.False, "Assign shares the corner, so it closes Inventory");
                int colonists = Colonists(boot.World!.Views.Current, out Odyssey.Sim.Contracts.PawnId first);
                int rows = Mathf.Min(colonists, AssignLayout.RowsPerPage);
                VisualElement assign = doc.rootVisualElement.Q("assign");
                AssertWindow(assign, AssignLayout.TabWidth,
                    AssignLayout.PanelHeight(rows, colonists > AssignLayout.RowsPerPage), "Assign");
                int shown = 0;
                foreach (VisualElement row in assign.Query(className: "assign__row").ToList())
                {
                    if (row.resolvedStyle.display != DisplayStyle.Flex) continue;
                    shown++;
                    Assert.That(row.layout.height, Is.EqualTo(AssignLayout.RowHeight).Within(PixelGrid));
                }
                Assert.That(shown, Is.EqualTo(rows), "a row per colonist on the page");

                if (first.IsValid)
                {
                    directors.ChooseColonist(first, boot.World!.Views.Current);
                    for (int i = 0; i < 3; i++) yield return null;
                    Assert.That(directors.Assign.Open, Is.True, "choosing a colonist closed the Assign tab");
                    // Both dock bottom-left, so the pane waits for the tab to close rather than
                    // drawing over it — and then shows whoever was chosen.
                    VisualElement? inspect = doc.rootVisualElement.Q("inspect");
                    Assert.That(inspect, Is.Not.Null, "the inspect pane is not in the tree");
                    Assert.That(inspect!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                        "the inspect pane opened over the Assign tab");
                    directors.Assign.SetOpen(false);
                    for (int i = 0; i < 3; i++) yield return null;
                    Assert.That(inspect.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                        "closing the Assign tab did not bring back the chosen colonist's pane");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The colonist pane's header (design 59 §16 H1): the longest given name in the pool must
        /// clear the buttons. Design 57 §6 took a fourth labelled button out because it "would have
        /// run her name under the buttons"; the prisoner line put one back (Arrest) and this test,
        /// measuring at 1920 x 1080, found 17 px left where the name needs about 70. Arrest is a
        /// right-click row now. **The word after the name does not fit either, on `main` too**
        /// since First Person went in: that shortfall is logged, not asserted, and is the header's
        /// own open question. The margin is the number a later button has to fit into.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLongestNameStillClearsTheColonistsButtons()
        {
            GameObject root = Build();
            try
            {
                yield return new WaitForSecondsRealtime(0.3f);
                for (int i = 0; i < 3; i++) yield return null;
                var boot = root.GetComponentInChildren<OdysseyBootstrap>();
                var doc = root.GetComponentInChildren<UIDocument>();
                HudDirectors? directors = boot!.Directors;
                if (directors == null) { Assert.Ignore("no session, so there is no pane to open"); yield break; }
                int colonists = Colonists(boot.World!.Views.Current, out Odyssey.Sim.Contracts.PawnId first);
                Assume.That(colonists, Is.GreaterThan(0));

                directors.ChooseColonist(first, boot.World!.Views.Current);
                for (int i = 0; i < 6; i++) yield return null;

                VisualElement inspect = doc!.rootVisualElement.Q("inspect");
                VisualElement titles = inspect.Q(className: "inspect__titles");
                var title = inspect.Q<Label>(className: "inspect__title");
                var meta = inspect.Q<Label>(className: "inspect__meta");
                Assume.That(titles != null && title != null && meta != null, "the header's parts");

                // Whatever stands after the name block in the header — the toggles, then Close over
                // Info (design 61) — is what the name line runs into.
                VisualElement header = titles!.parent;
                int at = header.IndexOf(titles);
                Assume.That(at + 1, Is.LessThan(header.childCount), "nothing after the name block");
                float rightEdge = header[at + 1].worldBound.xMin;

                string longest = "";
                foreach (string name in ColonistNamePool.Names) if (name.Length > longest.Length) longest = name;
                float nameWidth = title!.MeasureTextSize(longest, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                float metaWidth = meta!.MeasureTextSize(meta.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                float needed = nameWidth + meta.resolvedStyle.marginLeft + metaWidth;
                float room = rightEdge - titles.worldBound.xMin;
                var parts = new System.Text.StringBuilder();
                for (int i = at + 1; i < header.childCount; i++)
                    parts.Append($" [{header[i].GetClasses().FirstOrDefault() ?? header[i].name}: {header[i].worldBound.width:0}]");
                Debug.Log($"[Header] after the name:{parts}; room for the name line {room:0}; '{longest}' needs {nameWidth:0}, " +
                          $"with '{meta.text}' {needed:0}; margin {room - needed:0}");
                Assert.That(nameWidth, Is.LessThanOrEqualTo(room),
                    $"'{longest}' needs {nameWidth:0} and the header leaves {room:0}: her name runs under its controls");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// How many colonists the frame holds, and the first of them. Out here because a span's
        /// enumerator is a ref struct, which an iterator method may not hold across a yield.
        /// </summary>
        static int Colonists(Odyssey.Sim.Contracts.WorldSnapshot frame, out Odyssey.Sim.Contracts.PawnId first)
        {
            int colonists = 0;
            first = default;
            foreach (Odyssey.Sim.Contracts.PawnView pawn in frame.Pawns)
            {
                if (!pawn.IsColonist) continue;
                if (colonists == 0) first = pawn.Id;
                colonists++;
            }
            return colonists;
        }

        static void AssertWindow(VisualElement? window, int width, int height, string what)
        {
            Assert.That(window, Is.Not.Null, $"{what} is not in the tree");
            Assert.That(window!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), $"{what} did not open");
            // Within a pixel, not exactly: under the panel's scale the layout snaps to the
            // physical pixel grid.
            Assert.That(window.layout.width, Is.EqualTo(width).Within(PixelGrid), $"{what} width");
            Assert.That(window.layout.height, Is.EqualTo(height).Within(PixelGrid), $"{what} height");

            Rect frame = window.worldBound;
            var spills = new List<string>();
            Walk(window, frame, spills);
            var bands = new List<string>();
            for (int i = 0; i < window.childCount; i++)
                bands.Add($"{string.Join(".", window[i].GetClasses())} {window[i].layout}");
            Assert.That(spills, Is.Empty, $"{what} draws outside its own frame {frame} " +
                $"(layout {window.layout}, bands {string.Join(" | ", bands)}): " + string.Join("; ", spills));
        }

        static void Walk(VisualElement element, Rect frame, List<string> spills)
        {
            for (int i = 0; i < element.childCount; i++)
            {
                VisualElement child = element[i];
                if (child.resolvedStyle.display == DisplayStyle.None) continue;
                if (child.resolvedStyle.visibility == Visibility.Hidden) continue;
                Rect box = child.worldBound;
                const float slack = PixelGrid;
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

            // Drawn into a 1920 x 1080 texture, as HudGeometryTests draws: the batch runner's game
            // view is small enough that the panel scales to about 0.39, a 1 px border rounds up to
            // a whole physical pixel (2.57 layout units) and every band grows with it. That is the
            // runner's screen, not the window; at a play resolution the snapping is under a pixel.
            var doc = bootObject.AddComponent<UIDocument>();
            PanelSettings? settings = null;
#if UNITY_EDITOR
            settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
#endif
            settings = settings != null ? Object.Instantiate(settings) : ScriptableObject.CreateInstance<PanelSettings>();
            if (settings.themeStyleSheet == null)
                settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.targetTexture = new RenderTexture(1920, 1080, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            doc.panelSettings = settings;

            var shell = bootObject.AddComponent<HudShell>();
#if UNITY_EDITOR
            shell.hudStyles = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);
#endif
            bootObject.SetActive(true);
            return root;
        }
    }
}
