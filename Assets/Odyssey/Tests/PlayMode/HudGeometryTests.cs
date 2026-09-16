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
    /// The interface acceptance criteria, measured on the screen rather than in the model.
    ///
    /// <para><b>Why both this and <c>HudLayoutTests</c>.</b> The fast tier proves that
    /// <see cref="HudLayout"/>'s arithmetic does not overlap and does not exceed the coverage
    /// ceiling, in under a tenth of a second and with no engine. What it cannot prove is that the
    /// shell builds the screen the model describes — a stylesheet that quietly places the alerts
    /// panel somewhere else, or a padding that makes a panel taller than the model says, would
    /// leave every fast-tier assertion passing about a screen nobody is looking at. This test
    /// lays the real HUD out in a real panel at three resolutions and measures the boxes UI
    /// Toolkit actually produced.</para>
    ///
    /// <para><b>What "the model contains reality" means.</b> Each realised box has to sit inside
    /// its modelled box, allowing a pixel of rounding. Containment rather than equality, because a
    /// conservative model is safe in both directions that matter — a panel smaller than modelled
    /// cannot overlap a neighbour the model cleared, and cannot push coverage above a ceiling the
    /// model already met. A panel <i>larger</i> than modelled is the failure, and it is the one
    /// this catches.</para>
    /// </summary>
    public class HudGeometryTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";
        const string UiFontPath = "Assets/Odyssey/Presentation/Ui/Fonts/ArchivoNarrow.ttf";
        const string MonoFontPath = "Assets/Odyssey/Presentation/Ui/Fonts/IBMPlexMono-Medium.ttf";

        /// <summary>The three the acceptance criteria name.</summary>
        static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
        };

        /// <summary>
        /// How far a realised box may sit outside its modelled one.
        ///
        /// <para><b>It has to depend on the panel scale, and that is a fact about UI Toolkit
        /// rather than a fudge.</b> The layout is rounded to the <i>physical</i> pixel grid, and a
        /// panel scaled to fit a 1280-pixel window against a 1920-pixel canvas has one physical
        /// pixel to every 1.5 reference pixels. So a command item declared 38 reference pixels
        /// tall occupies 25.3 physical pixels, rounds to 26, and measures back as <b>39</b>. Every
        /// element in a column can gain one such step, which is why this is two of them rather
        /// than one.</para>
        /// </summary>
        static float SlackFor(Rect canvas, Vector2Int resolution) =>
            Mathf.Max(1.5f, 2f * canvas.width / resolution.x);

        [UnityTest]
        public IEnumerator NoTwoPanelsOverlapAtAnyOfTheThreeResolutions()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);

                    float slack = SlackFor(doc.rootVisualElement.worldBound, resolution);
                    Dictionary<string, Rect> boxes = Regions(doc);
                    var names = new List<string>(boxes.Keys);

                    for (int i = 0; i < names.Count; i++)
                    for (int j = i + 1; j < names.Count; j++)
                    {
                        Rect a = boxes[names[i]];
                        Rect b = boxes[names[j]];
                        Rect shared = Intersection(a, b);

                        Assert.That(shared.width <= slack || shared.height <= slack, Is.True,
                            $"at {resolution.x}x{resolution.y} the '{names[i]}' panel {a} overlaps " +
                            $"the '{names[j]}' panel {b} by {shared.width:0.#} x {shared.height:0.#} px");
                    }
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        [UnityTest]
        public IEnumerator TheHudCoversUnderTheCeilingWithNothingSelected()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);

                    Rect canvas = doc.rootVisualElement.worldBound;
                    float area = 0f;
                    foreach (KeyValuePair<string, Rect> region in Regions(doc))
                        area += region.Value.width * region.Value.height;

                    float coverage = area / (canvas.width * canvas.height);
                    Debug.Log($"[HudGeometry] {resolution.x}x{resolution.y} " +
                              $"(logical {canvas.width:0}x{canvas.height:0}): {coverage:P1} covered");

                    Assert.That(coverage, Is.LessThanOrEqualTo(HudLayout.CoverageCeiling),
                        $"the HUD covers {coverage:P1} of a {resolution.x}x{resolution.y} screen " +
                        $"with nothing selected, over the {HudLayout.CoverageCeiling:P0} ceiling");
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        /// <summary>
        /// The realised boxes sit inside the modelled ones. This is the assertion that keeps the
        /// fast tier honest: without it, <c>HudLayoutTests</c> proves things about a model that
        /// nothing forces the shell to obey.
        /// </summary>
        [UnityTest]
        public IEnumerator TheModelDescribesTheScreenTheShellActuallyBuilds()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                Rect canvas = doc.rootVisualElement.worldBound;
                float slack = SlackFor(canvas, Resolutions[1]);
                Dictionary<string, Rect> realised = Regions(doc);

                var content = new HudContent(
                    colonists: doc.rootVisualElement.Query(className: "card").ToList().Count,
                    storeRows: Visible(doc, "stores__row"),
                    alerts: Visible(doc, "alert"),
                    layers: doc.rootVisualElement.Query(className: "ruler__tick").ToList().Count,
                    needRows: 0);

                Dictionary<HudRegion, HudRect> model = HudLayout.Solve(canvas.width, canvas.height, content);

                (string Name, HudRegion Region)[] pairs =
                {
                    ("stores", HudRegion.Stores),
                    ("clock", HudRegion.Clock),
                    ("rail", HudRegion.DepthRail),
                    ("inspect", HudRegion.Inspect),
                };

                foreach ((string name, HudRegion region) in pairs)
                {
                    Assert.That(realised, Does.ContainKey(name), $"the shell built no '{name}' panel");
                    Rect box = realised[name];
                    HudRect want = model[region];

                    Debug.Log($"[HudGeometry] {name}: realised {box}, modelled " +
                              $"({want.X:0.#}, {want.Y:0.#}) {want.Width:0.#} x {want.Height:0.#}");

                    Assert.That(box.xMin, Is.GreaterThanOrEqualTo(want.X - slack), $"{name} starts left of the model");
                    Assert.That(box.yMin, Is.GreaterThanOrEqualTo(want.Y - slack), $"{name} starts above the model");
                    Assert.That(box.xMax, Is.LessThanOrEqualTo(want.Right + slack), $"{name} runs right of the model");
                    Assert.That(box.yMax, Is.LessThanOrEqualTo(want.Bottom + slack),
                        $"{name} is {box.yMax - want.Bottom:0.#} px taller than the model allows for, " +
                        "so the coverage and overlap arithmetic in the fast tier is describing a " +
                        "different screen from the one the shell builds. " +
                        Describe(doc.rootVisualElement.Q(name: name)!));
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator TheCommandBarNeverRunsOffTheEdgeAndAlwaysShowsItsHotkeys()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);

                    Rect canvas = doc.rootVisualElement.worldBound;
                    float slack = SlackFor(canvas, resolution);
                    VisualElement? bar = doc.rootVisualElement.Q(name: "bar");
                    Assert.That(bar, Is.Not.Null, "the shell built no command bar");

                    Rect box = bar!.worldBound;
                    Assert.That(box.xMin, Is.GreaterThanOrEqualTo(HudLayout.Edge - slack),
                        $"the bar starts {box.xMin:0.#} px from the left at {resolution.x}x{resolution.y}");
                    Assert.That(box.xMax, Is.LessThanOrEqualTo(canvas.width - HudLayout.Edge + slack),
                        $"the bar runs to {box.xMax:0.#} on a {canvas.width:0} px canvas at " +
                        $"{resolution.x}x{resolution.y}, so an item is cut off at the right edge");

                    // The bar may not wrap. One row, whatever fits; the rest go into Menu, which
                    // is what lets the inspect pane above it assume a height for it.
                    Debug.Log($"[HudGeometry] bar at {resolution.x}x{resolution.y}: {box}, {Describe(bar)}");
                    Assert.That(box.height, Is.LessThanOrEqualTo(HudCommands.BarHeight + HudLayout.Frame + slack),
                        $"the bar is {box.height:0.#} px tall against a modelled " +
                        $"{HudCommands.BarHeight + HudLayout.Frame}, so it has wrapped to a second " +
                        $"row. {Describe(bar)}");

                    // Every item that is on the row shows a hotkey cap with something in it.
                    int caps = 0;
                    foreach (VisualElement item in doc.rootVisualElement.Query(className: "cmd").ToList())
                    {
                        if (item.resolvedStyle.display == DisplayStyle.None) continue;
                        Label? key = item.Q<Label>(className: "cmd__key");
                        Assert.That(key, Is.Not.Null, "a command-bar item has no hotkey cap");
                        Assert.That(key!.text, Is.Not.Empty, "a command-bar item shows a blank hotkey");
                        caps++;
                    }
                    Assert.That(caps, Is.GreaterThan(1),
                        $"only {caps} command items are on the bar at {resolution.x}x{resolution.y}");
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        /// <summary>
        /// The one acceptance criterion that is about words rather than boxes: no three-letter
        /// placeholder strings anywhere on the screen. MEA, WOO and SCR were the old icon
        /// stand-in, and they read as truncated data rather than as a deliberate gap.
        /// </summary>
        [UnityTest]
        public IEnumerator NoLabelIsAThreeLetterPlaceholder()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                foreach (Label label in doc.rootVisualElement.Query<Label>().ToList())
                {
                    string text = label.text;
                    if (string.IsNullOrEmpty(text)) continue;

                    // A hotkey cap is a legend, not a word, and a colonist's initial is one
                    // letter. What is forbidden is an upper-case fragment of two or three letters
                    // standing in for a name.
                    if (label.ClassListContains("cmd__key") || label.ClassListContains("menu__key")) continue;
                    if (label.ClassListContains("card__initial")) continue;
                    if (label.ClassListContains("rail__hint")) continue;

                    bool shout = text.Length is 2 or 3 && text == text.ToUpperInvariant() &&
                                 System.Array.TrueForAll(text.ToCharArray(), char.IsLetter);

                    Assert.That(shout, Is.False,
                        $"'{text}' is a two-or-three-letter capitalised fragment on a " +
                        $"{string.Join(".", label.GetClasses())} label, which is the placeholder " +
                        "idiom the acceptance criteria strike out");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        // ---------------------------------------------------------------- fixture

        /// <summary>Every framed region's box, keyed by the name the shell gives it. Regions that
        /// are hidden — the alerts panel with nothing to say, the Build palette, the menu, the
        /// settings panel — are left out, because a hidden panel covers nothing.</summary>
        static Dictionary<string, Rect> Regions(UIDocument doc)
        {
            var boxes = new Dictionary<string, Rect>();
            foreach (VisualElement region in doc.rootVisualElement.Query(className: "region").ToList())
            {
                if (region.resolvedStyle.display == DisplayStyle.None) continue;
                boxes[region.name] = region.worldBound;
            }

            // The colonist strip is not a framed panel — each card is its own — so it is measured
            // as the union of the cards, which is what HudLayout models.
            Rect? strip = null;
            foreach (VisualElement card in doc.rootVisualElement.Query(className: "card").ToList())
                strip = strip == null ? card.worldBound : Union(strip.Value, card.worldBound);
            if (strip != null) boxes["strip"] = strip.Value;

            VisualElement? bar = doc.rootVisualElement.Q(name: "bar");
            if (bar != null) boxes["bar"] = bar.worldBound;

            return boxes;
        }

        /// <summary>
        /// Every visible child's classes, height, top and vertical margins. A panel two pixels
        /// taller than the model is not something anybody can diagnose from the total, and a
        /// second run of the PlayMode gate to find out costs a minute and a half.
        /// </summary>
        static string Describe(VisualElement element)
        {
            var parts = new List<string>();
            foreach (VisualElement child in element.Children())
            {
                if (child.resolvedStyle.display == DisplayStyle.None) continue;
                Rect box = child.worldBound;
                parts.Add($"[{string.Join(".", child.GetClasses())} h{box.height:0.##} " +
                          $"y{box.yMin:0.##} m{child.resolvedStyle.marginTop:0.##}/" +
                          $"{child.resolvedStyle.marginBottom:0.##}]");
            }
            return string.Join(" ", parts);
        }

        static int Visible(UIDocument doc, string className)
        {
            int count = 0;
            foreach (VisualElement element in doc.rootVisualElement.Query(className: className).ToList())
                if (element.resolvedStyle.display != DisplayStyle.None) count++;
            return count;
        }

        static Rect Intersection(Rect a, Rect b) => Rect.MinMaxRect(
            Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin),
            Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(
            Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        /// <summary>
        /// Real seconds, not frame counts: the shell primes on the first frame a world exists, and
        /// the cadence buckets after that are wall-clock, which in this harness advances by well
        /// under a millisecond a frame.
        /// </summary>
        static IEnumerator Settle(UIDocument doc)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            for (int i = 0; i < 6; i++) yield return null;
        }

        /// <summary>
        /// The play scene's HUD stack over a small world, rendered into a texture of the size
        /// under test. The panel scales with the screen, so the <i>logical</i> canvas it lays out
        /// against depends on the aspect it is given — which is exactly the thing being measured,
        /// and is why the resolution is applied to the panel rather than to the game window.
        /// </summary>
        static GameObject Build(out OdysseyBootstrap boot, out UIDocument doc, Vector2Int resolution)
        {
            var root = new GameObject("HudGeometry");

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
            boot = bootObject.AddComponent<OdysseyBootstrap>();
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.seed = 1;
            boot.barrenMap = true;
            boot.grassScatter = 0;
            boot.cameraRig = rig;
            bootObject.AddComponent<SelectionPresenter>();

            doc = bootObject.AddComponent<UIDocument>();
            PanelSettings? settings = null;
#if UNITY_EDITOR
            settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
#endif
            settings = settings != null ? Object.Instantiate(settings) : ScriptableObject.CreateInstance<PanelSettings>();
            if (settings.themeStyleSheet == null)
                settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.targetTexture = new RenderTexture(resolution.x, resolution.y, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            doc.panelSettings = settings;

            var shell = bootObject.AddComponent<HudShell>();
#if UNITY_EDITOR
            shell.hudStyles = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);
            shell.uiFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
            shell.monoFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(MonoFontPath);
#endif
            bootObject.SetActive(true);
            return root;
        }
    }
}
