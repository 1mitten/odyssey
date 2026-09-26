#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Photograph the HUD as the player sees it, so a complaint about it can be looked at rather
    /// than reasoned about.
    ///
    /// **Why this is a test and not an editor tool.** A UI Toolkit panel does not render into a
    /// camera's target texture, so `PlayScene.Shoot` — which is how every other picture in this
    /// project is taken — photographs the world with no interface on it at all. The panel is drawn
    /// by the player loop onto the screen, so the only place to catch it composited over the world
    /// is a PlayMode run with a graphics device, which is exactly what the Unity gate already is.
    ///
    /// The resolution matters more than anything else here, because the panel scales with it:
    /// `HudPanelSettings` is ScaleWithScreenSize against 1200x800 with a balanced match, so the
    /// same 11 px label is 6 px in a 640x480 batch window and 32 px on a 4K monitor. A picture
    /// taken at the default batch size would therefore prove nothing about what the owner can
    /// read. Pass `-screen-width 1920 -screen-height 1080` to the run and this records what it
    /// actually got, so a picture can never be mistaken for one taken at another size.
    ///
    /// Not an assertion, deliberately: there is no automatic answer to "can a human read this".
    /// It writes `Logs/hud-shot.png` and passes if it managed to take the picture at all.
    /// </summary>
    public class HudShotTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>The size the picture is taken at, which decides how big the type is.</summary>
        const int Width = 1920;
        const int Height = 1080;

        /// <summary>The close-up of the top-left corner, which is where A1 Resources sits.</summary>
        const int CloseWidth = 420;
        const int CloseHeight = 320;

        /// <summary>
        /// The title screen (design 40), photographed with no colony, so the dock's translucency and
        /// the line-up of each button's icon and words can be judged from a picture rather than
        /// reasoned about. Cleared to a mid blue so the translucency shows. Asserts that each
        /// button's words are centred on its icon to a pixel, which is what the owner reported
        /// "out of whack" on 2026-09-24.
        /// </summary>
        [UnityTest]
        public IEnumerator PhotographTheTitleScreen()
        {
            GameObject root = Build(out OdysseyBootstrap _, buildOnPlay: false);
            try
            {
                yield return new WaitForSecondsRealtime(0.5f);
                for (int i = 0; i < 20; i++) yield return null;

                var doc = root.GetComponentInChildren<UIDocument>();
                var settings = Object.Instantiate(doc.panelSettings);
                var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                settings.clearColor = true;
                settings.colorClearValue = new Color(0.20f, 0.30f, 0.45f);
                settings.targetTexture = target;
                doc.panelSettings = settings;
                for (int i = 0; i < 10; i++) yield return null;

                // No focus in this picture: a panel drawing into a render texture does not keep the
                // keyboard in a batch run (measured: nothing is focused after the swap), so the lit
                // state and the ring are asserted where focus is real, in
                // StartScreenTests.TheTitleScreenIsADockFlushLeft.

                foreach (VisualElement button in doc.rootVisualElement.Query(className: "title__btn").ToList())
                {
                    VisualElement icon = button.Q<PathGlyph>()!;
                    VisualElement words = button.Q(className: "title__words")!;
                    VisualElement name = button.Q(className: "title__name")!;
                    VisualElement description = button.Q(className: "title__desc")!;
                    float textCentre = (name.worldBound.yMin + description.worldBound.yMax) / 2f;
                    Assert.That(textCentre, Is.EqualTo(icon.worldBound.center.y).Within(1f),
                        "a button's words are not centred on its icon");
                    Assert.That(name.worldBound.xMin, Is.EqualTo(description.worldBound.xMin).Within(0.5f),
                        "a button's name and description do not start at the same x");
                    Assert.That(words.worldBound.xMin, Is.EqualTo(
                        doc.rootVisualElement.Q(className: "title__words")!.worldBound.xMin).Within(0.5f),
                        "the four buttons' words do not start at the same x");
                }

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                Directory.CreateDirectory(Path.GetFullPath("Logs"));
                File.WriteAllBytes(Path.GetFullPath("Logs/title-shot.png"), image.EncodeToPNG());

                // The load list, from rows handed to the menu rather than files on disk: the saves
                // folder is the player's own, and a test has no business writing into it. Colony
                // names of three lengths, because the fault reported was a date that moved with
                // the name in front of it.
                HudShell shell = root.GetComponentInChildren<HudShell>();
                shell.Menu.ShowSaves(new[]
                {
                    new SaveRow("a", "Ashford", 12, "Standard", problem: string.Empty, when: "24 Sep 17:20", colony: "Ashford"),
                    new SaveRow("b", "Before the winter", 3, "Large", problem: string.Empty, when: "2 Sep 09:05", colony: "Blackwater Reach"),
                    new SaveRow("c", "Hx", 140, "Small", problem: string.Empty, when: "19 Aug 23:59", colony: "Hx"),
                });
                for (int i = 0; i < 10; i++) yield return null;

                List<VisualElement> days = doc.rootVisualElement.Query(className: "save__day").ToList();
                List<VisualElement> whens = doc.rootVisualElement.Query(className: "save__when").ToList();
                Assert.That(days, Has.Count.EqualTo(3), "the load list did not draw the three saves");
                foreach (VisualElement day in days)
                    Assert.That(day.worldBound.xMin, Is.EqualTo(days[0].worldBound.xMin).Within(0.5f),
                        "the day moves with the colony's name");
                foreach (VisualElement when in whens)
                    Assert.That(when.worldBound.xMax, Is.EqualTo(whens[0].worldBound.xMax).Within(0.5f),
                        "the dates do not end in one column");
                foreach (VisualElement row in doc.rootVisualElement.Query(className: "save").ToList())
                    Assert.That(row.Q(className: "save__name")!.worldBound.xMin,
                        Is.EqualTo(row.Q(className: "save__line")!.worldBound.xMin).Within(0.5f),
                        "a save's title and its line under it do not start at one x");

                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.GetFullPath("Logs/load-shot.png"), image.EncodeToPNG());
                Object.Destroy(image);
                target.Release();
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator PhotographTheHud()
        {
            GameObject root = Build(out OdysseyBootstrap boot);
            try
            {
                yield return new WaitForSecondsRealtime(0.5f);
                for (int i = 0; i < 20; i++) yield return null;

                var doc = root.GetComponentInChildren<UIDocument>();
                Assert.That(doc, Is.Not.Null, "no HUD document");
                Assert.That(doc!.rootVisualElement, Is.Not.Null, "the panel has no root");

                // Select a colonist so the panes that only appear with a selection are in the
                // picture too, rather than photographing the empty state and calling it the HUD.
                if (boot.World != null && boot.World.Views.Current.PawnCount > 0 && boot.Directors != null)
                {
                    boot.Directors.ChooseColonist(
                        boot.World.Views.Current.Pawns[0].Id, boot.World.Views.Current);
                }

                for (int i = 0; i < 10; i++) yield return null;

                // The panel renders into a texture of our choosing rather than onto the screen.
                // Two reasons, and the first is not optional: `WaitForEndOfFrame` is never evoked
                // in batchmode, so `ScreenCapture` cannot be used from a headless run at all. The
                // second is that it fixes the resolution, and resolution is the whole question
                // here — the same sheet is unreadable at 640x480 and comfortable at 4K.
                //
                // The settings object is cloned, because it is a committed project asset and a
                // test has no business dirtying it.
                var settings = Object.Instantiate(doc.panelSettings);
                // sRGB, and that is not a detail. Read back through a linear target the whole
                // sheet comes out washed olive and every judgement about contrast made from the
                // picture would be a judgement about the capture instead.
                var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 2,
                };

                // Cleared to something like the meadow, so the panel's ten per cent of
                // translucency is represented rather than flattered by a black void behind it.
                settings.clearColor = true;
                settings.colorClearValue = new Color(0.36f, 0.58f, 0.22f);
                settings.targetTexture = target;
                doc.panelSettings = settings;

                for (int i = 0; i < 10; i++) yield return null;

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                Directory.CreateDirectory(Path.GetFullPath("Logs"));
                File.WriteAllBytes(Path.GetFullPath("Logs/hud-shot.png"), image.EncodeToPNG());

                // And a close-up of one region, because the whole sheet shrunk to fit a page
                // cannot answer "can you read this" either. ReadPixels takes its rect from the
                // bottom left, so the top-left panel is at the top of the buffer.
                RenderTexture.active = target;
                var close = new Texture2D(CloseWidth, CloseHeight, TextureFormat.RGB24, false);
                close.ReadPixels(new Rect(0, Height - CloseHeight, CloseWidth, CloseHeight), 0, 0);
                close.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.GetFullPath("Logs/hud-a1.png"), close.EncodeToPNG());
                Object.Destroy(close);

                // Reported in physical pixels, because that is the units the complaint that led to
                // the interface-scale setting was made in: type that subtends the right angle can
                // still read as too small on a large panel. The shell swaps in its own copy of the
                // panel settings so the scale can move without writing to the asset, so the live
                // one is read off the document rather than off the instance built above.
                PanelSettings live = doc.panelSettings;
                float scale = Mathf.Sqrt(
                    (target.width / (float)live.referenceResolution.x) *
                    (target.height / (float)live.referenceResolution.y));
                Debug.Log($"[HudShot] {target.width}x{target.height} to Logs/hud-shot.png; " +
                          $"scale mode {live.scaleMode}, reference {live.referenceResolution}, " +
                          $"panel scale {scale:0.00}x. Body text (13 px) lands at " +
                          $"{13f * scale:0.0} physical px here, the panel label (11 px) at " +
                          $"{11f * scale:0.0}, and the clock (24 px) at {24f * scale:0.0}.");

                // And again with the architect palette open, since it is hidden until the
                // bottom bar opens it and a picture of the closed state cannot show it at all.
                var palette = doc.rootVisualElement.Q(className: "build");
                if (palette != null)
                {
                    palette.style.display = DisplayStyle.Flex;
                    for (int i = 0; i < 10; i++) yield return null;

                    RenderTexture.active = target;
                    var open = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    open.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    open.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.GetFullPath("Logs/hud-architect.png"), open.EncodeToPNG());
                    Object.Destroy(open);
                }

                // And the settings panel, which is likewise hidden until asked for. It is the one
                // panel whose whole purpose is to be judged by eye — every look decision this
                // project has made ended wanting the owner's eye in the play scene, and this is
                // the surface that turns a session per question into a question per session.
                // By name: the window stopped carrying a "settings" class when it was rebuilt
                // (design 39), and a lookup by class found nothing and skipped these pictures
                // silently. Opened through the director so it is raised, scrimmed and laid out as a
                // player sees it.
                var panel = doc.rootVisualElement.Q("settings");
                Assert.That(panel, Is.Not.Null, "there is no settings window to photograph");
                if (panel != null)
                {
                    if (palette != null) palette.style.display = DisplayStyle.None;
                    boot.Directors!.Settings.SetOpen(true);
                    for (int i = 0; i < 10; i++) yield return null;

                    RenderTexture.active = target;
                    var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    shot.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.GetFullPath("Logs/hud-settings.png"), shot.EncodeToPNG());
                    Object.Destroy(shot);

                    boot.Directors!.Settings.SetTab(Odyssey.Hud.SettingsTab.Graphics);
                    for (int i = 0; i < 10; i++) yield return null;
                    RenderTexture.active = target;
                    var shotGraphics = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    shotGraphics.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    shotGraphics.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.GetFullPath("Logs/hud-graphics.png"), shotGraphics.EncodeToPNG());
                    Object.Destroy(shotGraphics);

                    boot.Directors!.Settings.SetTab(Odyssey.Hud.SettingsTab.Keys);
                    for (int i = 0; i < 10; i++) yield return null;
                    RenderTexture.active = target;
                    var shotKeys = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    shotKeys.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    shotKeys.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.GetFullPath("Logs/hud-keys.png"), shotKeys.EncodeToPNG());
                    Object.Destroy(shotKeys);
                }

                // And the debug menu (2026-09-17), backtick's own panel now — the same argument as
                // the settings panel above: judged by eye, not by an assertion.
                var debugPanel = doc.rootVisualElement.Q(className: "debug");
                if (debugPanel != null)
                {
                    if (panel != null) panel.style.display = DisplayStyle.None;
                    debugPanel.style.display = DisplayStyle.Flex;
                    for (int i = 0; i < 10; i++) yield return null;

                    RenderTexture.active = target;
                    var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    shot.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.GetFullPath("Logs/hud-debug.png"), shot.EncodeToPNG());
                    Object.Destroy(shot);
                }

                var workPanel = doc.rootVisualElement.Q(className: "work");
                if (workPanel != null)
                {
                    if (debugPanel != null) debugPanel.style.display = DisplayStyle.None;
                    if (panel != null) panel.style.display = DisplayStyle.None;
                    boot.Directors?.Work.SetOpen(true);
                    workPanel.style.display = DisplayStyle.Flex;
                    for (int i = 0; i < 10; i++) yield return null;

                    RenderTexture.active = target;
                    var shotWork = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    shotWork.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    shotWork.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.GetFullPath("Logs/hud-work.png"), shotWork.EncodeToPNG());
                    Object.Destroy(shotWork);
                }

                Object.Destroy(image);
                Object.Destroy(target);
                Object.Destroy(settings);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The bill list (design 49), photographed on three stations: an electric cooker with no
        /// power (the status strip up, three bills in their three modes, one paused), a campfire
        /// with bills and no strip, and a campfire with none. Writes <c>Logs/bills-*.png</c>, a
        /// crop of the bottom-left corner where the pane docks. Asserts only that the control is
        /// on the pane and at the bench width; the rest is for an eye.
        /// </summary>
        [UnityTest]
        public IEnumerator PhotographTheBillList()
        {
            GameObject root = Build(out OdysseyBootstrap boot);
            RenderTexture? target = null;
            try
            {
                yield return new WaitForSecondsRealtime(0.5f);
                for (int i = 0; i < 20; i++) yield return null;
                Assert.That(boot.Colony, Is.Not.Null, "no colony to put a station in");
                Odyssey.Sim.Pawns.ColonyWorld colony = boot.Colony!;
                Odyssey.Sim.Cooking.Kitchen kitchen = colony.Pawns.Kitchen!;

                var doc = root.GetComponentInChildren<UIDocument>();
                var settings = Object.Instantiate(doc.panelSettings);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 2,
                };
                settings.clearColor = true;
                settings.colorClearValue = new Color(0.36f, 0.58f, 0.22f);
                settings.targetTexture = target;
                doc.panelSettings = settings;

                var free = new List<int>();
                Odyssey.Sim.Contracts.GridSize size = colony.Grid.Size;
                Odyssey.Sim.Contracts.CellRef start = colony.Start;
                for (int dz = -4; dz <= 4 && free.Count < 3; dz += 2)
                for (int dx = -4; dx <= 4 && free.Count < 3; dx += 2)
                {
                    int x = start.X + dx, z = start.Z + dz;
                    if (!size.Contains(x, z, start.Y)) continue;
                    int index = size.Index(x, z, start.Y);
                    if (colony.Construction.Allows(index, Odyssey.Sim.Contracts.BuildingHandle.Galley)) free.Add(index);
                }
                Assert.That(free.Count, Is.EqualTo(3), "no room near the start for three stations");

                int Raise(int cell, int building)
                {
                    Assert.That(colony.Construction.Place(size.FromIndex(cell), building,
                        Odyssey.Sim.Contracts.StuffHandle.Wood), Is.EqualTo(Odyssey.Sim.Contracts.IntentRejection.None));
                    colony.Construction.Deliver(cell, Odyssey.Sim.Construction.ConstructionContent.BuildingAt(building).costCount);
                    Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
                    return cell;
                }

                void Edit(int cell, int op, int b = 0, int c = 0) =>
                    kitchen.HandleEditBill(new Odyssey.Sim.Contracts.Intent(Odyssey.Sim.Contracts.IntentKind.EditBill,
                        size.FromIndex(cell), op, b, c));

                int cooker = Raise(free[0], Odyssey.Sim.Contracts.BuildingHandle.Galley);
                int fire = Raise(free[1], Odyssey.Sim.Contracts.BuildingHandle.Campfire);
                int bare = Raise(free[2], Odyssey.Sim.Contracts.BuildingHandle.Campfire);
                foreach (int station in new[] { cooker, fire })
                {
                    for (int b = 0; b < 3; b++) Edit(station, Odyssey.Sim.Contracts.BillEdit.Add, Odyssey.Sim.Contracts.RecipeHandle.Meal);
                    Edit(station, Odyssey.Sim.Contracts.BillEdit.SetMode, 1, Odyssey.Sim.Contracts.BillModeHandle.Times);
                    Edit(station, Odyssey.Sim.Contracts.BillEdit.SetTarget, 1, 25);
                    Edit(station, Odyssey.Sim.Contracts.BillEdit.SetMode, 2, Odyssey.Sim.Contracts.BillModeHandle.Forever);
                    Edit(station, Odyssey.Sim.Contracts.BillEdit.SetSuspended, 2, 1);
                }
                kitchen.Invalidate();

                IEnumerator Shoot(int cell, string name)
                {
                    // As a click does (SelectionPresenter): ask the tile's question, republish, then
                    // select. ChooseCell alone leaves the pane reading "Ground", with no answer.
                    boot.World!.Intents.Submit(new Odyssey.Sim.Contracts.Intent(
                        Odyssey.Sim.Contracts.IntentKind.QueryCell, size.FromIndex(cell)));
                    boot.World.RepublishViews();
                    boot.Directors!.Selection.ChooseCell(size.FromIndex(cell));
                    // The pane refreshes fifteen times a second and a batch frame is a millisecond or
                    // two, so frames alone do not reach the next refresh: wait in real time.
                    yield return new WaitForSecondsRealtime(1f);
                    for (int i = 0; i < 10; i++) yield return null;

                    const int cropW = 1120, cropH = 820;
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    var image = new Texture2D(cropW, cropH, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, cropW, cropH), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    Directory.CreateDirectory(Path.GetFullPath("Logs"));
                    File.WriteAllBytes(Path.GetFullPath("Logs/bills-" + name + ".png"), image.EncodeToPNG());
                    Object.Destroy(image);
                    var list = doc.rootVisualElement.Q<BillList>();
                    var inspect = doc.rootVisualElement.Q(className: "inspect");
                    Debug.Log($"[BillShot] {name}: {doc.rootVisualElement.Query<BillList>().ToList().Count} lists, inspect classes " +
                              $"[{(inspect == null ? "none" : string.Join(" ", inspect.GetClasses()))}], edifice at the cell " +
                              $"{colony.Grid.Edifice[cell]}");
                    Assert.That(list, Is.Not.Null, name + ": the pane has no bill list");
                    Assert.That(list!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), name + ": the bill list is hidden");
                    var pane = doc.rootVisualElement.Q(className: "inspect--bench");
                    Assert.That(pane, Is.Not.Null, name + ": the pane is not at the bench width");
                    Assert.That(pane!.resolvedStyle.width, Is.EqualTo(BillsLayout.PaneWidth).Within(0.5f));

                    Debug.Log($"[BillShot] Logs/bills-{name}.png, pane {pane.resolvedStyle.width} x {pane.resolvedStyle.height}");
                }

                yield return Shoot(cooker, "nopower");
                yield return Shoot(fire, "campfire");
                yield return Shoot(bare, "empty");
            }
            finally
            {
                Object.Destroy(root);
                if (target != null) target.Release();
            }
        }

        /// <summary>The play scene's HUD stack, as <c>HudSmokeTests</c> builds it.</summary>
        static GameObject Build(out OdysseyBootstrap boot) => Build(out boot, buildOnPlay: true);

        static GameObject Build(out OdysseyBootstrap boot, bool buildOnPlay)
        {
            var root = new GameObject("HudShot");

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
            bootObject.SetActive(false);
            boot = bootObject.AddComponent<OdysseyBootstrap>();
            // Explicitly, not by default: since U38 pressing Play lands on the start screen, and
            // what this rig is asserting is that a session exists.
            boot.buildOnPlay = buildOnPlay;
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.layers = 8;
            boot.seed = 1;
            boot.barrenMap = false;
            boot.grassScatter = 60;
            boot.cameraRig = rig;

            // The committed catalogue, so the roster cards and the inspect header are photographed
            // with the portraits the player actually sees rather than with the no-packs fallback
            // (docs/design/20-avatars.md §10). The scene assigns this; a rig built by hand did not,
            // which is why the HUD photograph went on showing drawn avatars after the portraits
            // landed. Null on a runner without the licensed packs, and the fallback is correct
            // there — this picture is only ever taken on a machine that has them.
#if UNITY_EDITOR
            boot.moduleCatalogue =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
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
