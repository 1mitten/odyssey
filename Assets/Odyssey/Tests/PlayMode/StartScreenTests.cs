#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        static VisualElement? Focused(UIDocument doc) =>
            doc.rootVisualElement.panel.focusController.focusedElement as VisualElement;

        /// <summary>
        /// The title screen is a dock (design 40): 560 wide, flush left, the full height of the
        /// screen with a 1 px right edge, the mark and the wordmark on one row inside its 464 px of
        /// content, four buttons, and New game focused so Enter starts a game. Exit game asks
        /// through the leave prompt's no-colony form, with nothing to save.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTitleScreenIsADockFlushLeft()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                yield return new WaitForSecondsRealtime(0.1f);
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();
                VisualElement dock = Screen(doc)!;
                Rect canvas = doc.rootVisualElement.worldBound;
                Rect box = dock.worldBound;

                // Within a physical pixel: at the runner's small game view the panel is scaled well
                // below 1, and the layout is rounded to the physical grid (design 34 §5b).
                float pixel = 1f / doc.rootVisualElement.panel.scaledPixelsPerPoint + 0.01f;
                Assert.That(dock.layout.width, Is.EqualTo(TitleLayout.DockWidth).Within(pixel), "the dock is not 560 wide");
                Assert.That(box.xMin, Is.EqualTo(canvas.xMin).Within(pixel), "the dock is not flush left");
                Assert.That(box.yMin, Is.EqualTo(canvas.yMin).Within(pixel), "the dock does not reach the top");
                Assert.That(box.yMax, Is.EqualTo(canvas.yMax).Within(pixel), "the dock does not reach the bottom");
                // One physical pixel at least, which on the runner's scaled panel is more than one
                // point; the authored 1 px is pinned by HudStyleSheetTests.
                Assert.That(dock.resolvedStyle.borderRightWidth, Is.GreaterThan(0f).And.LessThanOrEqualTo(Mathf.Max(1f, pixel)),
                    "the dock has no right edge");
                Assert.That(dock.Q(className: "panel__hdr"), Is.Null, "the old card's header is still there");

                VisualElement logo = dock.Q(className: "title__logo")!;
                VisualElement word = dock.Q(className: "title__wordmark")!;
                Assert.That(word.layout.xMax, Is.LessThanOrEqualTo(TitleLayout.ContentWidth + pixel),
                    $"the wordmark ends at {word.layout.xMax}, past the dock's {TitleLayout.ContentWidth} px of content");
                Assert.That(word.layout.height, Is.LessThan(TitleLayout.WordmarkSize * 2.5f), "the wordmark wrapped");
                Debug.Log($"[TitleScreen] logo {logo.layout.width:0} px wide, wordmark {word.layout.width:0} px, " +
                          $"tracking {word.resolvedStyle.letterSpacing:0.0} px, logo top {logo.layout.y:0} of {dock.layout.height:0}");

                Assert.That(dock.Query(className: "title__btn").ToList(), Has.Count.EqualTo(4));
                Assert.That(dock.Query<IconBadge>().ToList(), Is.Empty, "a placeholder box is still drawn on the title screen");
                Assert.That(Focused(doc)?.ClassListContains("title__btn--good"), Is.True,
                    "New game does not have focus on load, so Enter starts nothing");

                // Focus lights the button the way hover does (the name in its colour, the edge on its
                // left) and shows the keyboard's ring, which is the button's own child and so stands
                // round the button wherever the layout has put it — it was one element placed by
                // coordinates, and the logo's late offset left it round the gap below New game.
                VisualElement newGame = Focused(doc)!;
                Color lit = newGame.Q(className: "title__name")!.resolvedStyle.color;
                Assert.That(((Vector4)(lit - HudTokens.Convert(HudTheme.Good))).magnitude, Is.LessThan(0.01f),
                    $"the focused button does not light its name (it is {lit})");
                Assert.That(newGame.Q(className: "title__edge")!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                    "the focused button shows no edge");
                VisualElement ring = newGame.Q(className: "title__ring")!;
                Assert.That(ring.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the focused button shows no ring");
                Assert.That(ring.worldBound.center.y, Is.EqualTo(newGame.worldBound.center.y).Within(pixel),
                    "the ring is not round the button it belongs to");
                foreach (VisualElement other in dock.Query(className: "title__btn").ToList())
                    if (other != newGame)
                        Assert.That(other.Q(className: "title__ring")!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                            "a button without focus shows a ring");

                // Exit game: the prompt, with nothing to save.
                shell.Menu.Choose(SessionCommands.QuitKey);
                yield return Settle();
                Assert.That(shell.LeavePromptOpen, Is.True, "Exit game closed nothing and asked nothing");
                VisualElement prompt = doc.rootVisualElement.Q("leaveprompt")!;
                Assert.That(prompt.Q(className: "prompt__answer--save")!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "the title screen's exit offers to save a colony that does not exist");
                shell.CancelLeavePrompt();
                yield return Settle();
                Assert.That(shell.LeavePromptOpen, Is.False);
                Assert.That(Shown(dock), Is.True, "cancelling the exit took the title screen away");
            }
            finally
            {
                Object.Destroy(root);
            }
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
        /// A new world is drawn behind a cover for <see cref="HudShell.CurtainFrames"/> frames
        /// before it is shown (design 38 §25).
        ///
        /// <para>The M10 tour measured the frame after the world first appeared at 102.5 ms — the
        /// driver and the GPU meeting it for the first time — and the one after at 18.7. The curtain
        /// pays those behind the start screen's own picture. The hand-over itself happens when the
        /// world is built — the interface is up under the cover — because deferring it moved the
        /// interface's first layout onto the reveal frame (43 ms). Asserted: the cover is up and the
        /// interface already behind it on the build frame, and the cover is gone once it lifts. A
        /// second press cannot build a second world.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ANewWorldIsDrawnBehindACurtainBeforeItIsShown()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();
                Assert.That(Shown(doc.rootVisualElement.Q("curtain")), Is.False, "the curtain is up over the title screen");
                shell.Menu.Choose(SessionCommands.NewGameKey);
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
                yield return Settle();

                Assert.That(shell.Menu.Start(), Is.True);
                // Not on the press: the menu fades to black first and the build waits for the
                // black to have been drawn (design 56 §3), so the long frame freezes on black.
                Assert.That(boot.HasSession, Is.False, "the world was built on the press frame, over the menu");
                for (int frame = 0; frame < 10 && !boot.HasSession; frame++) yield return null;
                Assert.That(boot.HasSession, Is.True, "Start built no world");
                var built = boot.World;
                Assert.That(shell.CurtainUp, Is.True, "the new world was shown at once, with no frames behind a cover");
                Assert.That(Shown(doc.rootVisualElement.Q("curtain")), Is.True, "the curtain is not on screen");
                Assert.That(Shown(doc.rootVisualElement.Q("world-ui")), Is.True,
                    "the interface is not up under the curtain, so its first layout lands on the reveal");

                shell.Menu.Start();
                Assert.That(boot.World, Is.SameAs(built), "a second press built a second world");

                for (int frame = 0; frame < HudShell.CurtainFrames + 2; frame++) yield return null;

                Assert.That(shell.CurtainUp, Is.False, "the curtain never lifted");
                Assert.That(Shown(doc.rootVisualElement.Q("curtain")), Is.False, "the curtain is still up over the colony");
                Assert.That(Shown(doc.rootVisualElement.Q("backdrop")), Is.False,
                    "the start screen is still up after the curtain");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// <b>A loaded world is covered exactly like a new one.</b> A load raises
        /// <c>SessionChanged</c> twice in one frame — the build, then the save read into it — and
        /// the second raise used to lift the curtain on the build frame, because the first had
        /// already hidden the backdrop it decided by (docs/bug-patterns.md, "An event raised
        /// twice in one frame"). The test above presses New game, which raises once, and so could
        /// never see it.
        /// </summary>
        [UnityTest]
        public IEnumerator ALoadedWorldIsCoveredLikeANewOne()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell);
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "odyssey-load-curtain-" + System.Guid.NewGuid());
            try
            {
                yield return Settle();
                Assert.That(boot.HasSession, Is.True);
                System.IO.Directory.CreateDirectory(folder);
                string path = System.IO.Path.Combine(folder, "cover" + SaveCatalogue.Extension);
                boot.SaveSession(path);

                boot.TeardownSession();
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();
                Assume.That(Shown(doc.rootVisualElement.Q("backdrop")), Is.True, "the title screen is not up");

                boot.LoadSession(path);
                Assert.That(boot.HasSession, Is.True, "the load built no world");
                Assert.That(shell.CurtainUp, Is.True,
                    "the loaded world was shown on its build frame: the second SessionChanged lifted the cover");
                Assert.That(Shown(doc.rootVisualElement.Q("curtain")), Is.True, "the curtain is not on screen");

                for (int frame = 0; frame < HudShell.CurtainFrames + 1; frame++) yield return null;
                Assert.That(shell.CurtainUp, Is.False, "the curtain never lifted over a loaded colony");
                Assert.That(Shown(doc.rootVisualElement.Q("curtain")), Is.False);
            }
            finally
            {
                if (System.IO.Directory.Exists(folder))
                    System.IO.Directory.Delete(folder, recursive: true);
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
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
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
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
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

                // The seed box is the World page's since design 59: it names the planet, and the
                // board is built on the seed of the tile picked on it. Typed there, before Next,
                // because that is the only place a player can type it.
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                var box = doc.rootVisualElement.Q<TextField>("seed");
                Assert.That(box, Is.Not.Null, "the World screen has no seed field");
                Assert.That(box!.value, Is.EqualTo(SeedEntry.Format(shell.Menu.Seed.Seed)),
                    "the box is not showing the seed the screen is holding");

                // A number the draw would never have produced, so passing cannot be a coincidence.
                box.value = "4242";
                yield return Settle();

                Assert.That(shell.Menu.Seed.Seed, Is.EqualTo(4242u),
                    "typing in the field did not reach the director");
                Assert.That(shell.Menu.World!.WorldSeed, Is.EqualTo(4242u),
                    "the planet on screen is not the one the box names");
                int tile = shell.Menu.World.Selected;

                Assert.That(shell.Menu.NextFromWorld(), Is.True, "the planet's suggested site cannot be taken");
                yield return Settle();
                Assert.That(shell.Menu.Start(), Is.True);
                yield return Settle();

                Assert.That(boot.World, Is.Not.Null, "Start built no world");
                Assert.That(boot.Colony!.Recipe(1).WorldSeed, Is.EqualTo(4242u),
                    "the colony was built on a planet the player never saw");
                Assert.That(boot.World!.Seed, Is.EqualTo(SiteRules.BoardSeed(4242u, tile)),
                    "the board was not built on the seed of the tile the player picked");

                // And the colony is the three that were on the setup page — the same
                // claim one level up, and the one no fast-tier test can make because it spans the
                // screen, the director, the bootstrap and ColonyRequest.
                // The people, not the pawns: the world seeds its own animals beside them
                // (design 30), and none of those was on the setup page.
                int people = 0;
                foreach (Odyssey.Sim.Pawns.Pawn pawn in boot.Colony!.Pawns.Pawns.All) if (pawn.IsPerson) people++;
                Assert.That(people, Is.EqualTo(ColonistSelect.Slots),
                    "the colony is not the size the screen offered");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// A box that does not name a seed goes nowhere, and says so by drawing Next inert.
        ///
        /// <para>The director's half is a fast-tier test; this is the half a player can see, and
        /// the two are asserted together on purpose — a Next that refused silently would look
        /// exactly like a Next that was broken. Since design 59 the seed box is on the World page,
        /// so a bad seed is stopped there, before the setup page and its Start are ever reached.</para>
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
                Assert.That(shell.Menu.World!.CanGoNext, Is.False,
                    "the planet of the last good seed can still be left from a box that names none");
                Assert.That(shell.Menu.NextFromWorld(), Is.False,
                    "Next left the planet from a box that does not name a seed");
                Assert.That(shell.Menu.Start(), Is.False,
                    "a world was started from a box that does not name a seed");
                Assert.That(boot.HasSession, Is.False,
                    "a world was built from a box that does not name a seed");

                VisualElement? next = doc.rootVisualElement.Q(className: "world__next");
                Assert.That(next, Is.Not.Null, "the World page has no Next button");
                Assert.That(next!.ClassListContains("settings__row--off"), Is.True,
                    "Next is still drawn pressable over a seed that cannot be used");

                // And back, so the disabling is a state rather than a one-way door.
                box.value = "77";
                yield return Settle();

                Assert.That(next.ClassListContains("settings__row--off"), Is.False,
                    "Next stayed inert over a seed that is perfectly good");
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
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
                yield return Settle();
                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.NewGame));

                Assert.That(Shown(Screen(doc)), Is.False,
                    "the menu panel is still up underneath the setup page");
                VisualElement? page = doc.rootVisualElement.Q("setup");
                Assert.That(Shown(page), Is.True, "the setup page is not on screen");

                // Back, and the box is exactly where it was — which is the half that would strand
                // a player if the page did not put the panel back. Twice since design 59: the setup
                // page backs out to the planet, and the planet to the root.
                shell.Menu.Back();
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
        /// The New game page's Story block (design 68 §12, mockups 25a-25c) stands inside the page
        /// at the scale it is designed at and at 150%, where the page goes compact, and picking
        /// Custom moves nothing: its block is always drawn and only lights.
        ///
        /// <para>The one check of the page's fit the fast tier cannot make: it knows the columns'
        /// widths, not how tall the laid-out text and the wrapping ladder come to.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheStoryBlockFitsThePageAndCustomMovesNothing()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                foreach (int scale in new[] { 100, 150 })
                {
                    boot.Preferences.SetUiScale(scale);
                    yield return Settle();

                    VisualElement page = doc.rootVisualElement.Q("setup")!;
                    VisualElement story = page.Q(className: "story")!;
                    // At 150% the canvas is at most 1280 wide, under the full layout's 1692, on any
                    // aspect; at 100% which layout applies depends on the game view's aspect, so
                    // only the fit is asserted there.
                    if (scale == 150)
                        Assert.That(page.ClassListContains("setup--compact"), Is.True,
                            "at 150% the page kept the full layout it has no room for");

                    Rect bounds = page.worldBound;
                    foreach (VisualElement part in story.Query(className: "story__card").ToList()
                                 .Concat(story.Query(className: "story__rung").ToList())
                                 .Concat(story.Query(className: "story__lever").ToList()))
                    {
                        Rect r = part.worldBound;
                        Assert.That(r.xMax, Is.LessThanOrEqualTo(bounds.xMax + 0.5f),
                            $"at {scale}% a Story element runs off the right of the page");
                        Assert.That(r.yMax, Is.LessThanOrEqualTo(bounds.yMax + 0.5f),
                            $"at {scale}% a Story element runs off the bottom of the page");
                    }

                    var before = new List<Rect>();
                    foreach (VisualElement e in story.Query<VisualElement>().ToList()) before.Add(e.worldBound);
                    shell.Menu.ChooseRung(StoryCatalogue.CustomRung);
                    yield return Settle();
                    List<VisualElement> after = story.Query<VisualElement>().ToList();
                    Assert.That(after.Count, Is.EqualTo(before.Count), "picking Custom added or took away an element");
                    for (int i = 0; i < after.Count; i++)
                        Assert.That(after[i].worldBound, Is.EqualTo(before[i]),
                            $"at {scale}% picking Custom moved {after[i].name}/{string.Join(".", after[i].GetClasses())}");
                    shell.Menu.ChooseRung(StoryCatalogue.NormalRung);
                    yield return Settle();
                }
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

                // Settings opens over the title screen's dock (design 40). The stack the owner
                // reported on 2026-09-17 was two centred boxes; the dock is flush left and the
                // window centred, and the window's own scrim dims the dock under it.
                Assert.That(Shown(Screen(doc)), Is.True, "the dock went away under the settings window");
                Assert.That(Shown(Scrim(doc)), Is.True,
                    "the scrim went with the menu, so the settings panel is no longer modal");

                // And it can be clicked. The menu's scrim is built after the settings window, so
                // unless the window is raised over it every click lands on the scrim and the
                // window shown on top does nothing (owner, 2026-09-24). Picked at the centre and
                // at a rail tab, the way a pointer would be.
                VisualElement settings = doc.rootVisualElement.Q("settings")!;
                foreach (Vector2 at in new[]
                         {
                             settings.worldBound.center,
                             settings.Q(className: "sw__tab")!.worldBound.center,
                         })
                {
                    VisualElement? hit = settings.panel.Pick(at);
                    Assert.That(hit != null && (hit == settings || settings.Contains(hit)), Is.True,
                        $"a click on the settings window at {at} landed on {hit?.name ?? "nothing"} " +
                        $"({(hit == null ? "" : string.Join(" ", hit.GetClasses()))}), not on the window");
                }

                // And back, by the panel's own close — not by the row that opened it, because the
                // ways out of that panel already existed and this has to be all of them.
                boot.Preferences.SetOpen(false);
                yield return Settle();

                Assert.That(Shown(Screen(doc)), Is.True,
                    "closing settings left no menu to come back to");
                Assert.That(shell.Menu.Screen, Is.EqualTo(MenuScreen.Root));
                yield return new WaitForSecondsRealtime(0.1f);
                yield return Settle();
                Assert.That(Focused(doc)?.ClassListContains("title__btn--violet"), Is.True,
                    "closing Settings did not hand focus back to the Settings button");
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
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
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

        /// <summary>
        /// The three candidate cards stand clear of one another, and each holds its own face.
        ///
        /// <para><b>Written because nothing could see the defect it catches.</b> When the avatar
        /// doubled 30 → 60 on 2026-09-18 every card carrying one was re-derived except this one,
        /// which kept the 47 px it was given when the face was 30. The sheet agreed with the model
        /// and the model agreed with itself, so the fast tier was green and the only witness was
        /// three faces visibly running into one another on `Logs/setup-page.png`. Arithmetic in
        /// <c>HudLayoutTests</c> now holds the card to its avatar; this holds the laid-out
        /// elements to each other, which is the question a player actually asks.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheCandidateCardsDoNotRunIntoEachOther()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                var doc = boot.GetComponent<UIDocument>();

                shell.Menu.Choose(SessionCommands.NewGameKey);
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
                yield return Settle();

                var cards = doc.rootVisualElement.Query(className: "colonist").ToList();
                Assert.That(cards.Count, Is.EqualTo(ColonistSelect.Slots),
                    "the page did not draw three candidates");

                for (int i = 0; i < cards.Count; i++)
                {
                    Rect box = cards[i].worldBound;
                    Assert.That(box.height, Is.GreaterThan(1f), "the card has not been laid out");

                    // The face is inside the card it belongs to, top and bottom. This is the one
                    // that failed at 47 px against a 60 px avatar.
                    var face = cards[i].Q(className: "colonist__face");
                    Assert.That(face, Is.Not.Null, $"candidate {i} has no face");
                    Rect head = face!.worldBound;
                    Assert.That(head.height, Is.LessThanOrEqualTo(box.height + 0.5f),
                        $"candidate {i}'s face is {head.height:0.#} px in a {box.height:0.#} px " +
                        "card, so it overflows onto its neighbours and over the selection outline");

                    // And no card overlaps the next one down.
                    if (i + 1 >= cards.Count) continue;
                    Rect next = cards[i + 1].worldBound;
                    Assert.That(box.yMax, Is.LessThanOrEqualTo(next.yMin + 0.5f),
                        $"candidate {i} ends at {box.yMax:0.#} and candidate {i + 1} starts at " +
                        $"{next.yMin:0.#}");
                }

                // The card is identity alone by the owner's decision (2026-09-18): a name and age
                // over an occupation, with the skills in the detail pane beside it. Both lines are
                // asserted present and filled, because an empty one looks exactly like a refresh
                // that never ran.
                var name = cards[0].Q<Label>(className: "colonist__name");
                var trade = cards[0].Q<Label>(className: "colonist__trade");
                Assert.That(name, Is.Not.Null, "the candidate card has no name line");
                Assert.That(trade, Is.Not.Null, "the candidate card has no occupation line");
                Assert.That(name!.text, Is.Not.Empty, "the name was never filled in");
                Assert.That(trade!.text, Is.Not.Empty, "the occupation was never filled in");

                // And the skills are in the pane beside them, under a heading of their own.
                var headings = doc.rootVisualElement.Query<Label>(className: "setup__heading").ToList();
                Assert.That(headings.Count, Is.EqualTo(2),
                    "the detail pane should carry two section headings, Skills and Traits");

                Debug.Log($"[StartScreen] card '{name.text}' / '{trade.text}' in a " +
                          $"{cards[0].worldBound.height:0.#} px card, " +
                          $"headings: {string.Join(", ", headings.ConvertAll(h => h.text))}");
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
