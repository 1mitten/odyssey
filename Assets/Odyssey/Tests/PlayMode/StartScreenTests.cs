#nullable enable
using System.Collections;
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
    /// B18, the start screen: the one state of this game in which no world exists (U38).
    ///
    /// <para><b>Why these are PlayMode tests and not fast-tier ones.</b> What
    /// <c>MenuDirectorTests</c> already proves in eleven milliseconds is the navigation — root to
    /// load and back, what each row raises, when an armed row stands down. What it cannot reach is
    /// the half that only exists once Unity has laid a panel out: that the screen is actually on
    /// screen when no session is built, that its scrim really does take the pointer everywhere,
    /// and that pressing New game produces a colony and takes the screen away. Those are the three
    /// claims here, and each of them is about the seam between the director and the engine rather
    /// than about either one.</para>
    /// </summary>
    public class StartScreenTests
    {
        static VisualElement? Screen(UIDocument doc) =>
            doc.rootVisualElement.Q("start");

        static VisualElement? Scrim(UIDocument doc) =>
            doc.rootVisualElement.Q("start-scrim");

        static bool Shown(VisualElement? element) =>
            element != null && element.style.display == DisplayStyle.Flex;

        static IEnumerator Settle()
        {
            for (int frame = 0; frame < 8; frame++) yield return null;
        }

        /// <summary>
        /// Press Play, and there is a screen rather than a colony.
        ///
        /// <para>This is the behaviour change U38 makes, so it is asserted on both sides: no world
        /// <i>and</i> a screen. Either alone would pass while the other was broken — a rig that
        /// simply failed to build anything would satisfy the first, and a screen drawn over a
        /// running colony would satisfy the second.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator WithNoSessionThereIsAScreen()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell _, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                Assert.That(boot.HasSession, Is.False, "Play built a world when it should not have");
                Assert.That(Shown(Screen(doc)), Is.True, "the start screen is not on screen");
                Assert.That(Shown(Scrim(doc)), Is.True,
                    "the scrim is not up, so the screen is not a modal and the world is still clickable");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The scrim takes the pointer everywhere, which is the whole of "a modal swallows every
        /// pointer event" (09-ui-and-input.md §6 case 5).
        ///
        /// <para><b>Sampled across the viewport rather than at one point</b>, because the failure
        /// this guards against is a scrim that is up but not full-size, or up and not pickable —
        /// both of which leave a corner of world that still answers a click while a modal is
        /// showing. The centre is not enough: the panel is there, and it would pass on its own
        /// account.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheModalTakesThePointerEverywhere()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();

                int width = UnityEngine.Screen.width;
                int height = UnityEngine.Screen.height;

                foreach ((float x, float y) in new[]
                         {
                             (0.04f, 0.04f), (0.96f, 0.04f), (0.04f, 0.96f), (0.96f, 0.96f),
                             (0.5f, 0.5f), (0.5f, 0.08f), (0.08f, 0.5f),
                         })
                {
                    var point = new Vector2(x * width, y * height);
                    Assert.That(shell.PointOverUi(point), Is.True,
                        $"a press at ({x:0.00}, {y:0.00}) of the viewport would reach the world " +
                        "through a modal");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// New game builds a colony and the screen goes; Quit to main menu puts it down and the
        /// screen comes back.
        ///
        /// <para>The round trip rather than one direction, because the bug this would really catch
        /// is a screen that hides on the way in and never returns — which would leave a player who
        /// quit to the menu looking at an empty scene with no interface at all, and no way to
        /// start anything.</para>
        ///
        /// <para>The rows are pressed through <c>MenuDirector</c> rather than by synthesising a
        /// click, because what is under test here is the session lifecycle and not the pointer;
        /// <c>MouseHarness</c> is for the latter.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NewGameBuildsAColonyAndQuittingToTheMenuBringsTheScreenBack()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                Assert.That(boot.HasSession, Is.True, "New game built no world");
                Assert.That(boot.Directors, Is.Not.Null, "a session exists with no directors");
                Assert.That(Shown(Screen(doc)), Is.False,
                    "the start screen is still up over a running colony");
                Assert.That(Shown(Scrim(doc)), Is.False, "the modal scrim survived its own screen");

                // The way back, which is the half that would otherwise strand a player.
                boot.TeardownSession();
                yield return Settle();

                Assert.That(boot.HasSession, Is.False, "the session survived its own teardown");
                Assert.That(Shown(Screen(doc)), Is.True,
                    "quitting to the menu left no screen to be on");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The panel is the same box on every screen it shows (owner, 2026-09-17: *"keep it fixed
        /// width and height because it becomes hard to read between loading and saving
        /// screens"*).
        ///
        /// <para>Measured on the realised boxes rather than on the model, because the model already
        /// says so by construction — <c>HudLayoutTests</c> proves the arithmetic, and this proves
        /// the shell built what the arithmetic describes. That pair is the same division of labour
        /// the rest of the HUD's geometry uses.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ThePanelIsTheSameBoxOnEveryScreen()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                Rect atRoot = Screen(doc)!.worldBound;
                Assert.That(atRoot.width, Is.GreaterThan(1f), "the panel has not been laid out yet");

                shell.Menu.Choose(SessionCommands.LoadKey);
                yield return Settle();
                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.Load));

                Rect atLoad = Screen(doc)!.worldBound;
                Assert.That(atLoad.width, Is.EqualTo(atRoot.width).Within(0.5f),
                    "the panel changed width between the menu and the load list");
                Assert.That(atLoad.height, Is.EqualTo(atRoot.height).Within(0.5f),
                    "the panel changed height between the menu and the load list, so every row " +
                    "under the pointer moved");
                Assert.That(atLoad.x, Is.EqualTo(atRoot.x).Within(0.5f));
                Assert.That(atLoad.y, Is.EqualTo(atRoot.y).Within(0.5f));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The settings panel works with no session, which is the reason the preferences were
        /// hoisted out of <c>HudDirectors</c> in the first place.
        ///
        /// <para>Before U38 those directors were built with a colony and thrown away with it, so
        /// the start screen's Options row would have opened a panel that nothing drove. This is
        /// that decision stated as a test: ask the screen for settings, and the panel answers.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator OptionsOpensTheSettingsPanelWithNoColonyRunning()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                Assert.That(boot.HasSession, Is.False);
                Assert.That(Shown(doc.rootVisualElement.Q("settings")), Is.False,
                    "the settings panel is open before anything asked for it");

                shell.Menu.Choose(SessionCommands.OptionsKey);
                yield return Settle();

                Assert.That(boot.Preferences.Open, Is.True, "the panel's director was not opened");
                Assert.That(Shown(doc.rootVisualElement.Q("settings")), Is.True,
                    "Options opened nothing, so the preferences are still session-shaped");

                // The owner's report: the two panels stacked, because both are centred. Settings
                // stands in the menu's place now — the menu goes, the scrim stays, because the
                // state is still modal and there is still no world behind any of it.
                Assert.That(Shown(Screen(doc)), Is.False,
                    "the menu is still under the settings panel, which is the stack the owner saw");
                Assert.That(Shown(Scrim(doc)), Is.True,
                    "the scrim went with the menu, so the settings panel is no longer modal");

                // And back, by the panel's own close — not by the row that opened it, because the
                // ways out of that panel already existed and this has to be all of them.
                boot.Preferences.SetOpen(false);
                yield return Settle();

                Assert.That(Shown(Screen(doc)), Is.True,
                    "closing settings left no menu to come back to");
                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.Root));
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
