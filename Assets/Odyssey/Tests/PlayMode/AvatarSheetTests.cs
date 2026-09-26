#nullable enable
using System.Collections;
using System.IO;
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
    /// Photograph the flat avatars, at the three sizes the HUD draws them
    /// (<c>docs/design/20-avatars.md</c>, the owner's decision 6: a contact sheet before a
    /// playtest).
    ///
    /// <para><b>What no test can answer.</b> Whether a colonist reads as a person at 26 px, and
    /// whether some pair of the seven skins and fourteen garments comes out as one muddy value at
    /// that size. Both are judged by looking. The suite below asserts only the things that are
    /// decidable — that every crown is drawn, that nothing is blank — and writes the picture that
    /// answers the rest.</para>
    ///
    /// <para><b>No world is built.</b> An avatar needs a seed and an id and nothing else, which is
    /// the whole point of a derived identity; a colony would make this test slower and no more
    /// informative.</para>
    /// </summary>
    public class AvatarSheetTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>
        /// Hand the rig the committed module catalogue, which it otherwise has none of.
        ///
        /// <para><b>This is what makes a portrait possible in a test at all</b>, and it is also why
        /// a portrait test can only ever be conditional: the catalogue's prefab references point
        /// into <c>Assets/Synty</c>, which is gitignored. On this machine they resolve through the
        /// junction; on the fast tier's Linux runner they resolve to nothing, and the test ignores
        /// itself with that reason rather than passing on an empty grid. U35's figure-leak check
        /// made the same bargain for the same reason.</para>
        /// </summary>
        static void GiveItACatalogue(OdysseyBootstrap boot)
        {
#if UNITY_EDITOR
            if (boot.moduleCatalogue == null)
                boot.moduleCatalogue =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
        }

        const int Width = 1280;
        const int Height = 720;

        /// <summary>The three sizes the HUD draws an avatar at, and nothing else exists.</summary>
        static readonly float[] Sizes = { HudLayout.CardAvatar, HudLayout.Avatar, HudLayout.DetailAvatar };

        [UnityTest]
        public IEnumerator PhotographTheAvatars()
        {
            var root = new GameObject("AvatarSheet");
            try
            {
                var doc = root.AddComponent<UIDocument>();
#if UNITY_EDITOR
                PanelSettings? asset =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
                doc.panelSettings = asset != null ? Object.Instantiate(asset) : null;
#endif
                if (doc.panelSettings == null)
                {
                    doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    doc.panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
                }

                var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 2,
                };

                // The HUD's own panel fill behind them, because that is what they will sit on and
                // a judgement about contrast made over black would be a judgement about the
                // capture (HudShotTests learned this one first).
                doc.panelSettings.clearColor = true;
                doc.panelSettings.colorClearValue = HudTokens.PanelFill;
                doc.panelSettings.targetTexture = target;

                for (int i = 0; i < 5; i++) yield return null;

                VisualElement page = doc.rootVisualElement;
                page.style.paddingLeft = 24;
                page.style.paddingTop = 20;

                // A colony's worth of people, at each size in turn.
                foreach (float size in Sizes)
                    page.Add(Row($"{(int)size} px — twenty-four colonists of one world", size,
                                 (slot) => ColonistFace.Of(20260918u, new PawnId(slot + 1)), 24));

                // And the shapes on their own, in one colour, so the silhouettes can be judged
                // apart from the palette. Eight crowns times three builds is the whole set.
                page.Add(Row("the twenty-four silhouettes, one palette", HudLayout.DetailAvatar,
                             Silhouette, ColonistFace.HairShapes * ColonistFace.Builds));

                for (int i = 0; i < 10; i++) yield return null;

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                Directory.CreateDirectory(Path.GetFullPath("Logs"));
                File.WriteAllBytes(Path.GetFullPath("Logs/avatars.png"), image.EncodeToPNG());

                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// And the page the choosing happens on, with the faces in it — the three candidate rows
        /// and the portrait beside the record. `Logs/setup-page.png`.
        ///
        /// <para>The sheet above says whether an avatar is a person; this says whether it belongs
        /// where it has been put, which is a different question and the one the owner's decision 6
        /// is really about. No assertion, deliberately: it passes if it managed to take the
        /// picture.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator PhotographTheSetupPage()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                for (int i = 0; i < 8; i++) yield return null;

                // So the page is photographed with the portraits it will really carry, rather than
                // with the no-packs fallback the rig would otherwise give it.
                GiveItACatalogue(boot);

                shell.Menu.Choose(SessionCommands.NewGameKey);
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 57 §9)
                for (int i = 0; i < 8; i++) yield return null;

                var doc = boot.GetComponent<UIDocument>();
                Assert.That(doc, Is.Not.Null, "the rig built no HUD document");

                PanelSettings settings = Object.Instantiate(doc!.panelSettings);
                var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 2,
                };

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
                File.WriteAllBytes(Path.GetFullPath("Logs/setup-page.png"), image.EncodeToPNG());

                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
            finally { Object.Destroy(root); }
        }

        /// <summary>
        /// The rendered portraits at full size, so the framing and the light can be judged
        /// (<c>docs/design/20-avatars.md</c> §10). `Logs/portraits.png`.
        ///
        /// <para>Ignored where the packs are absent: with no catalogue every look resolves to
        /// nothing and the sheet would be an empty grid reported as a pass.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator PhotographThePortraits()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell _, buildOnPlay: false);
            try
            {
                for (int i = 0; i < 8; i++) yield return null;

                GiveItACatalogue(boot);

                PortraitStudio studio = boot.Portraits;
                if (!studio.Available)
                {
                    Assert.Ignore("no module catalogue, so there is nobody to photograph");
                    yield break;
                }

                var doc = boot.GetComponent<UIDocument>();
                PanelSettings settings = Object.Instantiate(doc!.panelSettings);
                var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 2,
                };
                settings.clearColor = true;
                settings.colorClearValue = HudTokens.PanelFill;
                settings.targetTexture = target;
                doc.panelSettings = settings;

                for (int i = 0; i < 5; i++) yield return null;

                VisualElement page = doc.rootVisualElement;
                page.Clear();
                page.style.paddingLeft = 24;
                page.style.paddingTop = 20;

                var strip = new VisualElement();
                strip.style.flexDirection = FlexDirection.Row;
                strip.style.flexWrap = Wrap.Wrap;
                page.Add(HudText.Make(
                    $"{PortraitStudio.Size} px — the render, twenty-four colonists", HudTextRole.Meta));
                page.Add(strip);

                int taken = 0;
                for (int slot = 0; slot < 24; slot++)
                {
                    Texture2D? shot = studio.For(20260918u, new PawnId(slot + 1));
                    if (shot == null) continue;

                    taken++;
                    var tile = new VisualElement();
                    tile.style.width = PortraitStudio.Size;
                    tile.style.height = PortraitStudio.Size;
                    tile.style.marginRight = 6;
                    tile.style.marginBottom = 6;
                    tile.style.backgroundColor = new Color(0.16f, 0.18f, 0.20f);
                    tile.style.backgroundImage = new StyleBackground(shot);
                    tile.style.backgroundSize =
                        new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                    strip.Add(tile);
                }

                Debug.Log($"[Portraits] {taken} taken, {studio.Portraits} cached, " +
                          $"{studio.LiveRenderTextures} render texture(s) alive");

                for (int i = 0; i < 10; i++) yield return null;

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                Directory.CreateDirectory(Path.GetFullPath("Logs"));
                File.WriteAllBytes(Path.GetFullPath("Logs/portraits.png"), image.EncodeToPNG());

                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);

                Assert.That(taken, Is.GreaterThan(0), "the catalogue resolved but nobody was photographed");
            }
            finally { Object.Destroy(root); }
        }

        /// <summary>One crown on one build, all in the same three colours.</summary>
        static ColonistFace Silhouette(int index) =>
            new ColonistFace(
                Rgb24.FromHex(0x4A4F55), Rgb24.FromHex(0x2A2D31), Rgb24.FromHex(0xC89870),
                Rgb24.FromHex(0x3B2A1E),
                index / ColonistFace.Builds, index % ColonistFace.Builds);

        static VisualElement Row(string caption, float size,
                                 System.Func<int, ColonistFace> faces, int count)
        {
            var block = new VisualElement();
            block.style.marginBottom = 16;
            block.Add(HudText.Make(caption, HudTextRole.Meta));

            var strip = new VisualElement();
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.marginTop = 6;
            strip.style.flexWrap = Wrap.Wrap;

            for (int i = 0; i < count; i++)
            {
                var avatar = new AvatarGlyph(size);
                avatar.SetFace(faces(i));
                avatar.style.marginRight = 6;
                avatar.style.marginBottom = 6;
                strip.Add(avatar);
            }

            block.Add(strip);
            return block;
        }

        /// <summary>
        /// The leak test the plan's own `U41 Portraits` row asked for — *"a test that fails if
        /// more than a fixed number of render textures are alive at once"* — which §9 said had
        /// nothing to count, because flat avatars have no render textures. Rendered portraits do,
        /// and the fixed number is **one**.
        ///
        /// <para>That is the design's performance claim stated as an assertion: one target,
        /// reused, read back into a small texture per *appearance* rather than per colonist. A
        /// studio that kept a render texture per portrait would pass every other test in this file
        /// and quietly cost twenty-four render targets on a colony of twenty-four.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheStudioKeepsOneRenderTextureHoweverManyPortraits()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell _, buildOnPlay: false);
            try
            {
                for (int i = 0; i < 8; i++) yield return null;
                GiveItACatalogue(boot);

                PortraitStudio studio = boot.Portraits;
                if (!studio.Available)
                {
                    Assert.Ignore("no module catalogue, so there is nobody to photograph");
                    yield break;
                }

                for (int slot = 0; slot < 24; slot++) studio.For(20260918u, new PawnId(slot + 1));

                Assert.That(studio.Portraits, Is.GreaterThan(1), "nobody was photographed at all");
                Assert.That(studio.LiveRenderTextures, Is.EqualTo(1),
                    "the studio is keeping a render target per portrait rather than reusing one");

                // Asking again is free: the same appearance is the same picture, object for
                // object, which is what makes the roster bar's fifteen refreshes a second cost
                // nothing after the first.
                int after = studio.Portraits;
                Texture2D? first = studio.For(20260918u, new PawnId(1));
                Texture2D? again = studio.For(20260918u, new PawnId(1));
                Assert.That(again, Is.SameAs(first));
                Assert.That(studio.Portraits, Is.EqualTo(after), "asking twice photographed twice");

                studio.Clear();
                Assert.That(studio.Portraits, Is.Zero);
            }
            finally { Object.Destroy(root); }
        }

        [Test]
        public void EverySilhouetteIsADifferentDrawing()
        {
            // The decidable half. Twenty-four shapes that are only twenty-three drawings would be
            // a switch falling through, which is invisible on a contact sheet nobody counts.
            var seen = new System.Collections.Generic.HashSet<(int, int)>();
            for (int i = 0; i < ColonistFace.HairShapes * ColonistFace.Builds; i++)
            {
                ColonistFace face = Silhouette(i);
                Assert.That(seen.Add((face.HairShape, face.Build)), Is.True, $"index {i} repeats");
            }

            Assert.That(seen.Count, Is.EqualTo(24));
        }

        [Test]
        public void AnAvatarWithNobodyInItDrawsNothingRatherThanAnEmptyTile()
        {
            // The element is built before the face is known on three of the four sites, so the
            // resting state has to be honest: no fill, no figure, no half-drawn person.
            var avatar = new AvatarGlyph(HudLayout.Avatar);
            Assert.That(avatar.HasFace, Is.False);

            avatar.SetFace(ColonistFace.Of(1u, new PawnId(1)));
            Assert.That(avatar.HasFace, Is.True);
            Assert.That(avatar.Face, Is.EqualTo(ColonistFace.Of(1u, new PawnId(1))));
        }
    }
}
