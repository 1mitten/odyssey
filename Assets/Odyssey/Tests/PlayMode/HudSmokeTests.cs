#nullable enable
using System.Collections;
using System.Linq;
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
                // 1920x1080 since the interface rebuild: the specification gives every anchor,
                // width and row height in 1080p pixels, and a panel scaled against 1200x800 would
                // draw every one of them 1.6 times too large.
                Assert.That(doc.panelSettings.referenceResolution, Is.EqualTo(new Vector2Int(1920, 1080)));

                // Every framed region, by name rather than by a bare count. The count was the
                // assertion until the interface rebuild, and it earned its keep twice — a panel
                // built but never parented, or parented twice, is invisible in every other check
                // we have. Naming them keeps that and says which one is missing, which a number
                // cannot: the rebuild moved it from seven to eight and the failure was "expected
                // 7, was 8", which is not a sentence anybody can act on.
                // "start" is B18, built whether or not a session exists because the state it
                // belongs to is the one where none does (U38). It is in the tree and hidden here,
                // which is why it counts as a framed region while this rig is in a colony.
                // "saveprompt" joins for the same reason "start" did: both are modals, built at
                // startup and hidden until something asks for them, so both are framed regions in
                // the tree whatever the colony is doing.
                // "orders" is the strip in the right-hand gutter under the rail (2026-09-17): the
                // four order buttons left the Build palette's header, so they are a framed region
                // of their own now rather than eight pixels of somebody else's.
                // "debug" is the debug menu (2026-09-17), backtick's own panel now rather than a
                // direct toggle of the developer overlay: built and hidden at startup exactly as
                // "settings" is, so it too is a framed region whatever the colony is doing.
                // "work" is the Work tab (design 27, 2026-09-20): docked bottom-left over the
                // command bar, built and hidden at startup exactly as "settings" and "debug" are.
                // "bulletins" is the Events panel (design 23, 2026-09-20): under the alerts in
                // their column, hidden until something has happened, a framed region all the same.
                // "almanac-panel" is the reference browser (2026-09-20): full-bleed over the dark
                // wash, built and hidden at startup exactly as "settings" and "debug" are, and a
                // framed region whatever the colony is doing.
                string[] expected =
                {
                    "stores", "clock", "alerts", "bulletins", "rail", "orders", "inspect", "build", "menu",
                    "settings", "debug", "work", "start", "saveprompt", "almanac-panel",
                };
                var regions = doc.rootVisualElement.Query(className: "region").ToList();
                var names = regions.ConvertAll(r => r.name);
                names.Sort();
                var wanted = new System.Collections.Generic.List<string>(expected);
                wanted.Sort();
                Assert.That(names, Is.EqualTo(wanted),
                    "the HUD did not build every framed region exactly once");

                // The roster is bound to the frame: one card per published pawn.
                int cards = doc.rootVisualElement.Query(className: "card").ToList().Count;
                Assert.That(cards, Is.EqualTo(boot.World!.Views.Current.PawnCount),
                    "the roster bar does not match the published pawn count");

                // The ruler covers every layer.
                Assert.That(doc.rootVisualElement.Query(className: "ruler__tick").ToList().Count,
                    Is.EqualTo(boot.Model!.Size.SizeY));

                // The ruler reads like a ruler: the top step is the top layer. Asserted by the
                // lit step's position rather than by anything the shell reports about itself,
                // because the bug this pins was a pair of mirrored indices that agreed with each
                // other — the bar was built upside down and then read with the layer number used
                // as a list index, so the lit step often looked plausible while a click low on
                // the bar moved the slice high. Reported from a playtest on 2026-09-16.
                int layers = boot.Model.Size.SizeY;
                var steps = doc.rootVisualElement.Query(className: "ruler__tick").ToList();

                // What a step will DO, read off userData, not what it looks like. The lit step
                // cannot tell the two bugs apart: build the bar upside down, read it back with a
                // mirrored index, and the highlight lands correctly while the click goes
                // elsewhere. An earlier version of this test asserted on the highlight and passed
                // with the bug deliberately restored, which is the only reason this one exists.
                for (int i = 0; i < steps.Count; i++)
                {
                    Assert.That(steps[i].userData, Is.EqualTo(layers - 1 - i),
                        $"step {i} from the top of the ruler should move the slice to layer " +
                        $"{layers - 1 - i} on a click, and the top step should be the top layer");
                    Assert.That(steps[i].tooltip, Does.StartWith($"Layer {layers - 1 - i}"),
                        $"step {i} describes a different layer than the one it would move to, so " +
                        "the bar is built one way round and read the other");
                }

                // And then the highlight, which is the half the player sees.
                for (int layer = 0; layer < layers; layer++)
                {
                    boot.Directors!.Slice.SetLayer(layer);
                    yield return null;
                    yield return null;

                    var ticks = doc.rootVisualElement.Query(className: "ruler__tick").ToList();
                    int lit = ticks.FindIndex(t => t.ClassListContains("ruler__tick--active"));
                    Assert.That(lit, Is.EqualTo(layers - 1 - layer),
                        $"the slice is on layer {layer} of {layers}, so the lit step should be " +
                        $"{layers - 1 - layer} from the top; it is {lit}");
                }

                // Four speed buttons, and the clock has a time in it.
                Assert.That(doc.rootVisualElement.Query(className: "speed__btn").ToList().Count,
                    Is.EqualTo(4));

                // A command with nothing behind it is drawn as unavailable, and which those are is
                // read off HudCommands rather than written down here — so the day one is wired up,
                // this follows it instead of failing. How many reach the row depends on the width
                // the bar reflowed to; every one that does is checked.
                var drawn = doc.rootVisualElement.Query(className: "cmd").ToList();
                Assert.That(drawn, Is.Not.Empty, "the command bar drew nothing");
                Assert.That(drawn.Any(d => d.ClassListContains("cmd--off")), Is.True,
                    "no dead command reached the row, so the check below proves nothing — either " +
                    "the bar reflowed them all into Menu, or nothing is marking them any more");

                foreach (VisualElement item in drawn)
                {
                    HudCommand command = HudCommands.All.First(c => c.Key == item.name);
                    Assert.That(item.ClassListContains("cmd--off"), Is.EqualTo(!command.Live),
                        $"{command.Key} is {(command.Live ? "live but dimmed" : "dead but drawn as available")}");

                    if (!command.Live)
                        Assert.That(item.tooltip, Does.Contain(command.Reason),
                            $"{command.Key} is dimmed without saying why, which the catalogue forbids");
                }
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
                boot.Directors!.ChooseColonist(pawn, boot.World.Views.Current);
                yield return null; // the resolved event answers in the frame it happened

                var doc = root.GetComponentInChildren<UIDocument>();
                Label? title = doc!.rootVisualElement.Q<Label>(className: "inspect__title");
                Assert.That(title, Is.Not.Null, "the inspect pane never built a header");
                Assert.That(title!.text, Is.EqualTo(ColonistNames.Of(boot.World.Views.Current, pawn)),
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
        /// Every layer of the shipped world has a step on the Depth Ruler that a player can hit,
        /// and hitting it moves the slice to that layer.
        ///
        /// <b>At the depth the game ships, not the depth the other tests use.</b> Everything else
        /// in this file builds eight layers because eight is quick; OdysseyBootstrap ships
        /// <b>sixteen</b>, and the ruler has to fit them into whatever height is left in the right
        /// column once the clock and the alerts have taken theirs. A ruler that fits eight and
        /// silently loses half of sixteen passes every other assertion here — the steps it did
        /// build would be in the right order and would carry the right layers. That is the report
        /// this was written for: the low steps could not be reached, and nor could the layer the
        /// colony starts on.
        ///
        /// The depth is read off a fresh bootstrap rather than typed, so it cannot drift from the
        /// game. What is asserted is what a player needs, in order: a step for every layer, each
        /// given a size, none clipped out of the panel by the ruler's own overflow rule, and each
        /// moving the slice to the layer it names.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLayerHasAStepThatCanBeHit()
        {
            int shipped = ShippedLayerCount();
            GameObject root = Build(out OdysseyBootstrap boot, shipped);
            try
            {
                yield return WarmUp();

                var doc = root.GetComponentInChildren<UIDocument>();
                Assert.That(doc, Is.Not.Null);

                // Laid out at the size a player runs at, not at the size the batch window
                // happens to be. The panel scales with the screen, so USS lengths are reference
                // pixels and the logical height of the sheet depends on the *aspect* it is given:
                // the 640x480 batch window works out about 848 of them tall, where 1920x1080 and
                // 4K are both about 735. The ruler therefore has a hundred more pixels of room
                // in a default headless run than on the owner's monitor, and a bar that does not
                // fit sixteen layers on screen would fit them here and pass.
                var settings = Object.Instantiate(doc!.panelSettings);
                var surface = new RenderTexture(PlayWidth, PlayHeight, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                settings.targetTexture = surface;
                doc.panelSettings = settings;

                for (int i = 0; i < 20; i++) yield return null;

                var rows = doc.rootVisualElement.Q(className: "rail__cells");
                Assert.That(rows, Is.Not.Null, "the depth rail has no cell container");

                var steps = doc.rootVisualElement.Query(className: "ruler__tick").ToList();
                Assert.That(steps.Count, Is.EqualTo(shipped),
                    $"the world has {shipped} layers and the ruler built {steps.Count} steps");

                Rect inside = rows!.worldBound;
                for (int i = 0; i < steps.Count; i++)
                {
                    Rect box = steps[i].worldBound;

                    Assert.That(box.height, Is.GreaterThan(1f),
                        $"step {i} of {steps.Count} was laid out {box.height:0.00} px tall, which " +
                        "is not something a player can click");

                    // Clipped, not merely off: .ruler hides its overflow, so a step past the
                    // bottom of the container is drawn nowhere and receives nothing.
                    Assert.That(box.yMax, Is.LessThanOrEqualTo(inside.yMax + 0.5f),
                        $"step {i} of {steps.Count} ends {box.yMax - inside.yMax:0.0} px below the " +
                        "ruler's own box, so it is clipped away and cannot be hit");

                    Assert.That(steps[i].userData, Is.EqualTo(shipped - 1 - i),
                        $"step {i} from the top should move the slice to layer {shipped - 1 - i}");
                }

                // And the click does what the step says, for every layer including the one the
                // colony starts on.
                foreach (var step in steps)
                {
                    int target = (int)step.userData;
                    boot.Directors!.Slice.SetLayer(target);
                    Assert.That(boot.Directors.Slice.ActiveLayer, Is.EqualTo(target),
                        $"asking for layer {target} left the slice on {boot.Directors.Slice.ActiveLayer}");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>The resolution the ruler is judged at: a common desktop, and the same shape
        /// as 4K, which is what the owner plays on.</summary>
        const int PlayWidth = 1920;
        const int PlayHeight = 1080;

        /// <summary>The layer count the game ships, read off a bootstrap rather than typed.</summary>
        static int ShippedLayerCount()
        {
            var probe = new GameObject("LayerProbe");
            probe.SetActive(false);
            int layers = probe.AddComponent<OdysseyBootstrap>().layers;
            Object.DestroyImmediate(probe);
            return layers;
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
        static GameObject Build(out OdysseyBootstrap boot) => Build(out boot, layers: 8);

        static GameObject Build(out OdysseyBootstrap boot, int layers)
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
            // Explicitly, not by default: since U38 pressing Play lands on the start screen, and
            // what this rig is asserting is that a session exists.
            boot.buildOnPlay = true;
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.layers = layers;
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
