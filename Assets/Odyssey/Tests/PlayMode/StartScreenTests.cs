#nullable enable
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
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
        /// <b>The in-game interface is not on screen when there is no game</b> (owner, 2026-09-17).
        ///
        /// <para>It used to be: every region was built straight onto the shell root and drawn
        /// behind the main menu's scrim — dimmed rather than absent, which reads as the game being
        /// open behind a dialog it is not open behind. They live in one container now and it is put
        /// away with the session.</para>
        ///
        /// <para><b>Asserted on the container and on a region inside it</b>, because either alone
        /// passes while the other is broken: hiding the parent proves nothing if a panel were
        /// reparented out of it, and a hidden panel proves nothing about the eleven beside it.</para>
        ///
        /// <para>The round trip matters as much as the first half — an interface that hides with no
        /// colony and never comes back is a game you cannot play.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheInGameInterfaceIsNotDrawnWithNoColony()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                VisualElement? world = doc.rootVisualElement.Q("world-ui");
                Assert.That(world, Is.Not.Null, "the in-game interface has no container to put away");
                Assert.That(Shown(world), Is.False,
                    "the in-game interface is drawn over the main menu");

                // And a region inside it, resolved through the tree rather than by class alone, so
                // the check cannot pass because the element simply does not exist.
                Assert.That(world!.Q(className: "commandbar"), Is.Not.Null,
                    "the command bar is not inside the container that gets put away");

                Assert.That(Shown(doc.rootVisualElement.Q("backdrop")), Is.True,
                    "the menu has no backdrop, so it sits over an empty camera");

                // Start a colony: the interface comes back and the backdrop goes.
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();
                Assert.That(shell.Menu.Start(), Is.True);
                yield return Settle();

                Assert.That(Shown(doc.rootVisualElement.Q("world-ui")), Is.True,
                    "the interface never came back, so the colony cannot be played");
                Assert.That(Shown(doc.rootVisualElement.Q("backdrop")), Is.False,
                    "the menu backdrop is still up over a running colony");
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

                // Two presses since U39: New game opens the screen that shows the seed, and Start
                // is what commits. Driven through the director rather than by synthesising a
                // click, as the note above says.
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                Assert.That(boot.HasSession, Is.False,
                    "opening the New game screen built a world before anything was chosen");

                // Two presses: New game opens the setup page with a board and three people
                // already dealt, and Start commits. It was briefly three, when the colonists had
                // a screen of their own; the whole setup is one page now.
                Assert.That(shell.Menu.Start(), Is.True);
                yield return Settle();

                Assert.That(boot.HasSession, Is.True, "Start built no world");
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
        /// <b>The claim U39 exists to keep: the number on screen is the number the world was built
        /// from.</b>
        ///
        /// <para>No fast-tier test can make it. <c>SeedFieldTests</c> proves the box parses,
        /// <c>MenuDirectorTests</c> proves the press carries the seed it parsed, and neither can
        /// see <c>ColonyRequest</c> — the seam runs from a <c>TextField</c> in the presentation
        /// assembly, through the director, through the bootstrap, to a <c>SimWorld</c>, and it is
        /// exactly the middle of that chain where a unit like this goes wrong.</para>
        ///
        /// <para><b>It is driven through the control rather than the director</b>, unlike the test
        /// above, because the field is the one part of this screen whose wiring nothing else
        /// checks. And the seed is typed rather than accepted: a drawn one would pass this test
        /// against a build that ignored the box entirely.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheWorldIsBuiltFromTheSeedInTheBox()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                var box = doc.rootVisualElement.Q<TextField>("seed");
                Assert.That(box, Is.Not.Null, "the New game screen has no seed field");
                Assert.That(box!.value, Is.EqualTo(SeedEntry.Format(shell.Menu.Seed.Seed)),
                    "the box is not showing the seed the screen is holding");

                // A number the draw would never have produced, so passing cannot be a coincidence.
                box.value = "4242";
                yield return Settle();

                Assert.That(shell.Menu.Seed.Seed, Is.EqualTo(4242u),
                    "typing in the field did not reach the director");

                Assert.That(shell.Menu.Start(), Is.True);
                yield return Settle();

                Assert.That(boot.World, Is.Not.Null, "Start built no world");
                Assert.That(boot.World!.Seed, Is.EqualTo(4242u),
                    "the colony was built from a seed the player never saw");

                // And the colony is the three that were on the setup page — the same
                // claim one level up, and the one no fast-tier test can make because it spans the
                // screen, the director, the bootstrap and ColonyRequest.
                Assert.That(boot.Colony!.Pawns.Pawns.Count, Is.EqualTo(ColonistSelect.Slots),
                    "the colony is not the size the screen offered");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// A box that does not name a seed builds nothing, and says so by drawing Start inert.
        ///
        /// <para>The director's half is a fast-tier test; this is the half a player can see, and
        /// the two are asserted together on purpose — a Start that refused silently would look
        /// exactly like a Start that was broken.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ASeedThatIsNotANumberBuildsNothing()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                var box = doc.rootVisualElement.Q<TextField>("seed");
                box!.value = "twelve";
                yield return Settle();

                Assert.That(shell.Menu.Seed.Usable, Is.False);
                Assert.That(shell.Menu.Start(), Is.False,
                    "a world was started from a box that does not name a seed");
                Assert.That(boot.HasSession, Is.False,
                    "a world was built from a box that does not name a seed");

                VisualElement? commit = doc.rootVisualElement.Q(className: "settings__row--off");
                Assert.That(commit, Is.Not.Null,
                    "Start is still drawn pressable over a seed that cannot be used");

                // And back, so the disabling is a state rather than a one-way door.
                box.value = "77";
                yield return Settle();

                Assert.That(doc.rootVisualElement.Q(className: "settings__row--off"), Is.Null,
                    "Start stayed inert over a seed that is perfectly good");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// No text anywhere in the HUD is invisible against the panel behind it.
        ///
        /// <para><b>Written because two labels were</b> (owner, 2026-09-17: *"I couldn't see any
        /// text on the save as/save buttons when saving a world"*). <c>color</c> is an inherited
        /// property in UI Toolkit and nothing in the sheet set one above a leaf, so a label built
        /// without a style class fell through to the imported runtime theme's dark ink and drew
        /// invisibly on a near-black panel. Every other label in the HUD passes a class, which is
        /// why it had never happened — and when it did, the text was *absent* rather than wrong,
        /// which no screenshot review would flag as a mistake because there is nothing there to
        /// look at.</para>
        ///
        /// <para><b>This is an invisibility test, not a legibility one.</b> The floor is 2:1, well
        /// under the 4.5:1 <see cref="HudContrast.BodyMinimum"/> the acceptance criteria set for
        /// body text, because the deliberately quiet inks — a hotkey cap at
        /// <see cref="HudTheme.TextFaint"/>, a disabled row — are meant to be faint and are not
        /// what this is looking for. <b>Measured, not assumed:</b> the quietest ink anybody chose
        /// draws at about 2.8:1 and ink that inherited the runtime theme's default draws at 1.0,
        /// so the floor sits in a wide gap rather than next to either.</para>
        ///
        /// <para><b>The control was run, and the first one taught something.</b> Stripping the
        /// style class off a button — the original bug — now *passes*, because <c>.hud</c> carries
        /// a default ink and a classless label simply inherits it. That route is closed, so the
        /// remaining risk is a class that names a dark colour, and the control for that is real:
        /// setting <c>.prompt__answer</c>'s colour to <c>#0b1116</c> fails this test with
        /// <i>"'Save' draws at 1.0:1 — ink #0b1116 on #0c0f13"</i>, naming the button and the
        /// number. Restored, it passes.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NoTextInTheHudIsInvisible()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                // Open the surfaces that are hidden at rest, so their labels are laid out and
                // counted. The naming prompt is the one that started this.
                boot.Preferences.SetOpen(true);
                shell.Prompt.Show("Ashford", SaveNameStatus.Free);
                yield return Settle();

                int checkedLabels = 0;
                foreach (Label label in doc.rootVisualElement.Query<Label>().ToList())
                {
                    if (label.worldBound.width <= 0f || label.worldBound.height <= 0f) continue;
                    if (string.IsNullOrWhiteSpace(label.text)) continue;

                    HudColour behind = BackgroundBehind(label);
                    HudColour ink = Ink(label);

                    double ratio = HudContrast.Ratio(HudContrast.Over(ink, behind), behind);
                    checkedLabels++;

                    Assert.That(ratio, Is.GreaterThan(2.0),
                        $"'{label.text}' draws at {ratio:0.0}:1 — ink {ink.Hex} on {behind.Hex}. " +
                        "A label with no style class inherits the runtime theme's ink, which is " +
                        "dark, and vanishes rather than looking wrong.");
                }

                // Without this the test passes on a HUD that built nothing at all, which is the
                // shape of vacuous pass this project has been bitten by before.
                Assert.That(checkedLabels, Is.GreaterThan(20),
                    "too few labels were on screen for this to have checked anything");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// A label's own ink, <b>without</b> the opacity its ancestors apply to it.
        ///
        /// <para><b>Deliberately not the number the eye finally gets</b>, and the first version of
        /// this test was wrong to use that. It failed on a command-bar hotkey cap: faint ink by
        /// design, on a row dimmed to 0.42 because the command is unavailable, and the two
        /// compounding put it under the floor. Both of those are the interface saying something on
        /// purpose.</para>
        ///
        /// <para>The question this test asks is "was this ink ever chosen", and a dimmed row is an
        /// answer to a different one. Ink that inherited the runtime theme's default lands at about
        /// 1.0 whatever opacity is over it; the quietest ink anybody chose lands near 2.8. The gap
        /// between those is the whole test, and folding opacity in closes it for no gain.</para>
        /// </summary>
        static HudColour Ink(Label label)
        {
            Color colour = label.resolvedStyle.color;
            return new HudColour(
                (byte)(colour.r * 255f + 0.5f), (byte)(colour.g * 255f + 0.5f),
                (byte)(colour.b * 255f + 0.5f), colour.a);
        }

        /// <summary>
        /// What is actually behind a label: every ancestor's background composited from the
        /// outermost inwards, over black.
        ///
        /// <para><b>Not simply the panel fill, and the first version of this test was wrong to
        /// assume so.</b> It failed on a colonist's initial — dark ink, deliberately, because it
        /// sits on an accent-coloured avatar tile. The same is true of the depth rail's active
        /// number and the command bar's primary label. Ink that looks invisible against a panel is
        /// perfectly legible against the thing it is really drawn on, so the background has to be
        /// computed rather than assumed, or this test punishes the interface for being careful.
        /// </para>
        /// </summary>
        static HudColour BackgroundBehind(Label label)
        {
            var chain = new List<VisualElement>();
            for (VisualElement? at = label; at != null; at = at.parent) chain.Add(at);
            chain.Reverse();

            var behind = new HudColour(0, 0, 0);
            foreach (VisualElement element in chain)
            {
                Color fill = element.resolvedStyle.backgroundColor;
                if (fill.a <= 0.001f) continue;

                behind = HudContrast.Over(
                    new HudColour((byte)(fill.r * 255f + 0.5f), (byte)(fill.g * 255f + 0.5f),
                        (byte)(fill.b * 255f + 0.5f), fill.a),
                    behind);
            }

            return behind;
        }

        /// <summary>
        /// Saving twice writes one file, not two (owner, 2026-09-17: *"I notice you keep saving a
        /// new game everytime … otherwise lots of saves will be created"*).
        ///
        /// <para>The claim the whole naming change exists to make, asserted on the folder itself
        /// rather than on the binding — the binding is the mechanism, and a mechanism that is right
        /// while the folder still fills up would be no use to anybody.</para>
        ///
        /// <para>It writes into a real temporary folder rather than the player's own, and puts it
        /// back afterwards whatever happens: a test that left files in <c>persistentDataPath</c>
        /// would show up in the owner's load list.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SavingTwiceWritesOneFile()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell);
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "odyssey-save-twice-" + System.Guid.NewGuid());
            try
            {
                yield return Settle();
                Assert.That(boot.HasSession, Is.True);
                Assert.That(boot.SaveSession(), Is.Null,
                    "a colony that has never been named claimed to have somewhere to save to");

                // The path-taking overload, so this writes into a temporary folder rather than
                // into the player's own. SaveSessionAs is the same act with the folder and the
                // naming rules applied, and those are SaveCatalogue's to prove.
                System.IO.Directory.CreateDirectory(folder);
                string first = System.IO.Path.Combine(folder, "keep" + SaveCatalogue.Extension);
                boot.SaveSession(first);

                Assert.That(boot.BoundSavePath, Is.EqualTo(first),
                    "writing a save did not bind the session to it, so the next Save has nowhere to go");

                // The ordinary case: Save, with no prompt and no new file.
                string? second = boot.SaveSession();
                Assert.That(second, Is.EqualTo(first), "Save wrote somewhere other than its own file");

                Assert.That(System.IO.Directory.GetFiles(folder).Length, Is.EqualTo(1),
                    "saving twice left more than one file, which is the whole complaint");

                // And the binding does not outlive the colony: a new one inheriting it would
                // overwrite somebody else's save on its first press.
                boot.TeardownSession();
                yield return Settle();
                Assert.That(boot.BoundSavePath, Is.Null);
            }
            finally
            {
                if (System.IO.Directory.Exists(folder))
                    System.IO.Directory.Delete(folder, recursive: true);
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

                // New game is not one of this box's screens any more, and that is the point of the
                // setup page: rather than growing the panel to hold three candidates and a skills
                // grid, New game leaves it entirely. So the panel goes away and the page stands in
                // its place — asserted here because "the box never changes size" is now kept by
                // there being nothing in it to change size for.
                shell.Menu.Back();
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();
                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.NewGame));

                Assert.That(Shown(Screen(doc)), Is.False,
                    "the menu panel is still up underneath the setup page");
                VisualElement? page = doc.rootVisualElement.Q("setup");
                Assert.That(Shown(page), Is.True, "the setup page is not on screen");

                // Back, and the box is exactly where it was — which is the half that would strand
                // a player if the page did not put the panel back.
                shell.Menu.Back();
                yield return Settle();

                Rect returned = Screen(doc)!.worldBound;
                Assert.That(returned.width, Is.EqualTo(atRoot.width).Within(0.5f));
                Assert.That(returned.height, Is.EqualTo(atRoot.height).Within(0.5f));
                Assert.That(returned.x, Is.EqualTo(atRoot.x).Within(0.5f));
                Assert.That(returned.y, Is.EqualTo(atRoot.y).Within(0.5f));
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

        /// <summary>
        /// A candidate row holds its occupation beside the face, measured with the real text
        /// engine rather than estimated (<c>docs/design/20-avatars.md</c> §3, §7).
        ///
        /// <para><b>Why this is a test at all.</b> The avatar took 38 px out of a 244 px row — the
        /// 30 px face and its gap — and the design settled the question with arithmetic: the
        /// longest of the registry's 174 occupations is 21 characters, which "fits with room". An
        /// arithmetic claim about text is a guess about a font. The rule it would break is one of
        /// the interface's oldest: <b>a colonist's own name may be cut short and nothing else
        /// may</b>, so a trade that overflows is a fault rather than an inelegance.</para>
        ///
        /// <para>If it ever fails the lever is <c>.colonists</c>, which is 260 px on a page that
        /// is the whole viewport — not the avatar, and not the rule.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ACandidateRowHoldsItsTradeBesideTheFace()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();
                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.NewGame));

                VisualElement? row = doc.rootVisualElement.Q(className: "colonist");
                var trade = row?.Q<Label>(className: "colonist__trade");
                var name = row?.Q<Label>(className: "colonist__name");
                Assert.That(trade, Is.Not.Null, "the candidate row has no trade line");
                Assert.That(name, Is.Not.Null);

                // The column the text actually gets: the row, less its own side padding, less the
                // face and the gap after it. Measured off the laid-out element rather than
                // recomputed from constants, so a change to any of them is felt here.
                var lines = row!.Q(className: "colonist__lines");
                Assert.That(lines, Is.Not.Null, "the candidate row has no text column");
                float room = lines!.worldBound.width;
                Assert.That(room, Is.GreaterThan(1f), "the row has not been laid out yet");

                float widest = 0f;
                string longest = string.Empty;
                foreach (string key in ColonistIdentity.Occupations)
                {
                    string label = Registry.Label(key);
                    float w = trade!.MeasureTextSize(label, 0f, VisualElement.MeasureMode.Undefined,
                                                     0f, VisualElement.MeasureMode.Undefined).x;
                    if (w <= widest) continue;
                    widest = w;
                    longest = label;
                }

                Debug.Log($"[StartScreen] widest trade '{longest}' {widest:0.#} px in " +
                          $"{room:0.#} px beside a {HudLayout.Avatar} px face " +
                          $"({ColonistIdentity.Occupations.Count} occupations)");

                Assert.That(widest, Is.LessThanOrEqualTo(room),
                    $"'{longest}' needs {widest:0.#} px and the row leaves {room:0.#} beside the " +
                    "face, so a trade is being cut short — and only a name may be");
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
