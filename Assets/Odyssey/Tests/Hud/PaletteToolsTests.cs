#nullable enable
using System.Collections.Generic;
using System;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Build palette's table: that a tool which can be armed can also say so, and that a tool
    /// the player can arm is one the player can actually reach.
    ///
    /// <para><b>Why this suite exists.</b> Arming and lighting used to be two separate lists in
    /// the shell, a hundred lines apart, and the fast tier could see neither — the table lived in
    /// a <c>MonoBehaviour</c>'s assembly. When they drifted the symptom was not a compile error
    /// but a chip that arms a tool and never lights, which a player reads as the click having
    /// missed. Both halves are one row now and this is what holds them together.</para>
    /// </summary>
    public class PaletteToolsTests
    {
        /// <summary>
        /// A category's live count is the table's own answer, not a number written beside it.
        ///
        /// <para>The count is what dims a category tile and what its tooltip says (owner,
        /// 2026-09-18: <i>"disable the top groups that have nothing to build … so we understand
        /// what we can build"</i>). Written as an invariant rather than as seven expected numbers
        /// on purpose: <c>U44</c> puts a stair into Structure and the first workbench will light
        /// Production, and a test that had to be edited on each of those is a test that would be
        /// edited without being read.</para>
        /// </summary>
        [Test]
        public void ACategorysLiveCountIsWhatItsToolsSay()
        {
            for (int i = 0; i < PaletteTools.Categories.Length; i++)
            {
                string[] tools = PaletteTools.Categories[i].tools;

                int expected = 0;
                foreach (string tool in tools)
                    if (PaletteTools.TryGet(tool, out _)) expected++;

                string name = Registry.Label(PaletteTools.Categories[i].key);
                Assert.That(PaletteTools.LiveToolsIn(i), Is.EqualTo(expected),
                    $"{name} miscounts what it holds");
                Assert.That(PaletteTools.HasLiveTool(i), Is.EqualTo(expected > 0),
                    $"{name} disagrees with its own count");
                Assert.That(PaletteTools.LiveToolsIn(i), Is.LessThanOrEqualTo(tools.Length),
                    $"{name} claims more live tools than it has tools");
            }

            // An index off either end is 0 rather than a throw: the shell walks this beside a
            // tile list and a palette that crashed while drawing would be a worse bug than a
            // category drawn dim.
            Assert.That(PaletteTools.LiveToolsIn(-1), Is.Zero);
            Assert.That(PaletteTools.LiveToolsIn(PaletteTools.Categories.Length), Is.Zero);
        }

        /// <summary>
        /// A category with nothing live in it can still be opened, and opening it arms nothing.
        ///
        /// <para><b>Both halves are the decision</b> (owner, 2026-09-18, choosing "grey but still
        /// openable"). The palette draws seven categories while four hold nothing at all, and the
        /// reason to draw them is that a player can look inside and see what is coming — so the
        /// tile dims rather than going dead to the click. That is only safe because opening an
        /// empty category cannot put a tool in the player's hand, which is what the second half
        /// asserts: the landing entry has no live tool behind it, so <c>ApplySubType</c> falls
        /// through and the cursor is still empty.</para>
        /// </summary>
        [Test]
        public void AnEmptyCategoryOpensAndArmsNothing()
        {
            var designate = new DesignateDirector();
            var palette = new BuildPaletteModel(designate);

            int opened = 0;
            for (int i = 0; i < PaletteTools.Categories.Length; i++)
            {
                if (PaletteTools.HasLiveTool(i)) continue;
                opened++;

                designate.Tool = DesignateTool.None;
                palette.SelectCategory(i);

                string name = Registry.Label(PaletteTools.Categories[i].key);
                Assert.That(palette.Category, Is.EqualTo(i), $"{name} refused to open");
                Assert.That(designate.Tool, Is.EqualTo(DesignateTool.None),
                    $"opening {name} put a tool in the player's hand");
                Assert.That(palette.SubTypeIsArmed, Is.False,
                    $"{name} lit a tile that arms nothing");
            }

            Assert.That(opened, Is.GreaterThan(0),
                "no empty category left to check — delete this test and the dimming with it");
        }

        /// <summary>
        /// Arming any live tool makes that tool — and only that tool — report itself as armed.
        ///
        /// <para>The "only that tool" half is the one that matters: a predicate that answered true
        /// for everything would light the whole row and would still pass a test that only asked
        /// about the tool it had just armed.</para>
        /// </summary>
        [Test]
        public void ArmingAToolLightsThatToolAndNoOther()
        {
            foreach (PaletteTool tool in PaletteTools.Live)
            {
                var director = new DesignateDirector();
                tool.Arm(director);

                Assert.That(tool.IsArmed(director), Is.True, $"{tool.Key} was armed and does not say so");
                foreach (PaletteTool other in PaletteTools.Live)
                {
                    if (other.Key == tool.Key) continue;
                    Assert.That(other.IsArmed(director), Is.False,
                        $"{other.Key} lights up while {tool.Key} is the tool being held");
                }
            }
        }

        /// <summary>
        /// Pressing the armed tool's own chip puts it down. Every tool behaves this way, so a
        /// player can always put one down the way they picked it up.
        /// </summary>
        [Test]
        public void ArmingTheSameToolAgainPutsItDown()
        {
            foreach (PaletteTool tool in PaletteTools.Live)
            {
                var director = new DesignateDirector();
                tool.Arm(director);
                tool.Arm(director);

                Assert.That(tool.IsArmed(director), Is.False, $"{tool.Key} stayed armed when pressed twice");
                Assert.That(director.Tool, Is.EqualTo(DesignateTool.None), $"{tool.Key} left something else held");
            }
        }

        /// <summary>
        /// A tool that arms something but sits on no chip can never be clicked. That is exactly how
        /// the cancel tool spent its life: built, bound to a key, and on no row of the palette.
        /// </summary>
        [Test]
        public void EveryLiveToolIsOnAChipSomewhere()
        {
            var drawn = new HashSet<string>(PaletteTools.Pinned);
            foreach (var (_, tools) in PaletteTools.Categories)
                foreach (string tool in tools)
                    drawn.Add(tool);

            foreach (PaletteTool tool in PaletteTools.Live)
                Assert.That(drawn, Does.Contain(tool.Key),
                    $"{tool.Key} arms a tool and is on no row at all, so nothing can click it");
        }

        /// <summary>
        /// The negative control for the suite above: a key drawn on the palette that is not live
        /// arms nothing at all, rather than quietly arming something adjacent.
        /// </summary>
        [Test]
        public void AToolWhoseThingDoesNotExistYetArmsNothing()
        {
            Assert.That(PaletteTools.TryGet("ui.arch.tool.door", out _), Is.False,
                "doors cannot be built yet, so the door chip must be drawn disabled");
            Assert.That(PaletteTools.TryGet("ui.arch.tool.stockpile", out _), Is.False);
            Assert.That(PaletteTools.TryGet("not.a.key.at.all", out _), Is.False);
        }

        /// <summary>
        /// Cancel is reachable without changing category, because the moment a player wants it is
        /// while they are holding another tool — which is exactly when the category row is showing
        /// something else (owner, 2026-09-17).
        /// </summary>
        [Test]
        public void CancelAndDeconstructAreReachableFromEveryCategory()
        {
            Assert.That(PaletteTools.Pinned, Does.Contain(PaletteTools.Cancel));
            Assert.That(PaletteTools.Pinned, Does.Contain(PaletteTools.Deconstruct));

            foreach (string key in PaletteTools.Pinned)
                Assert.That(PaletteTools.TryGet(key, out _), Is.True,
                    $"{key} is pinned and arms nothing, so it is a button that does nothing");
        }

        /// <summary>
        /// The pinned row should stay small. A row that grows is a second palette, and the whole
        /// point of it is to be the short list of things that are true whatever the player is
        /// doing.
        ///
        /// <para><b>The ceiling moved from three to four on 2026-09-17</b>, when the Orders
        /// category was dropped and its two live tools were pinned rather than lost. Four is where
        /// it stops: the palette header has room for four 26 px buttons beside the layout switcher
        /// and the way out, and a fifth would start pushing one of those off a 1280-wide screen —
        /// which is a limit the geometry imposes rather than one this test invented, and is why
        /// the number is written here as well as argued for in <c>PaletteTools.Pinned</c>.</para>
        /// </summary>
        [Test]
        public void ThePinnedRowStaysShort()
        {
            Assert.That(PaletteTools.Pinned.Length, Is.LessThanOrEqualTo(4));
        }

        /// <summary>
        /// And it is in no category, so it is drawn once. The same chip in two places in one open
        /// panel is a question the player has to stop and answer — whether the two do the same
        /// thing — and the answer is never worth the pause.
        /// </summary>
        [Test]
        public void APinnedToolIsNotAlsoFiledUnderACategory()
        {
            foreach (string pinned in PaletteTools.Pinned)
                foreach (var (key, tools) in PaletteTools.Categories)
                    Assert.That(tools, Does.Not.Contain(pinned),
                        $"{pinned} is pinned and also listed under {Registry.Label(key)}, so it draws twice");
        }

        /// <summary>
        /// The orders a player gives are still reachable, now that the category holding them is
        /// gone.
        ///
        /// <para><b>This test used to assert the opposite</b> — that an Orders category existed
        /// and contained Mine and Chop — and it is the same claim, rewritten against where those
        /// two tools now live. The claim worth keeping is not "there is an Orders row"; it is
        /// "the two orders a player actually gives can be found by looking". Dropping the
        /// category without moving them would have left both on the <c>M</c> and <c>C</c> keys
        /// and on nothing visible, which is exactly the fault the owner reported about Cancel on
        /// 2026-09-17: the tool was never missing, every way of finding it was.</para>
        /// </summary>
        [Test]
        public void TheOrdersAPlayerGivesAreStillReachable()
        {
            Assert.That(PaletteTools.Pinned, Does.Contain(PaletteTools.Mine),
                "mining is reachable from the palette rather than only from the M key");
            Assert.That(PaletteTools.Pinned, Does.Contain(PaletteTools.Fell),
                "chopping is reachable from the palette rather than only from the C key");
        }

        /// <summary>
        /// Every live tool in the game is on the palette somewhere, in a category or pinned.
        ///
        /// <para>The general form of the test above, and the one that would have caught the
        /// Orders question without anybody thinking of it. A tool with a working simulation half
        /// and no way in is the failure this project has now made twice; a tier that walks
        /// <see cref="PaletteTools.Live"/> and asks where each one is drawn cannot let it happen
        /// a third time silently.</para>
        /// </summary>
        [Test]
        public void EveryLiveToolIsDrawnSomewhere()
        {
            foreach (PaletteTool tool in PaletteTools.Live)
            {
                bool pinned = Array.IndexOf(PaletteTools.Pinned, tool.Key) >= 0;
                bool filed = false;
                foreach (var (_, tools) in PaletteTools.Categories)
                    if (Array.IndexOf(tools, tool.Key) >= 0) filed = true;

                Assert.That(pinned || filed, Is.True,
                    $"{tool.Key} arms a real tool and appears nowhere on the palette, so the only " +
                    "way to reach it is a key the player has to already know about");
            }
        }

        /// <summary>
        /// <b>Paving is in two categories on purpose, and this test is here so nobody tidies it
        /// away.</b>
        ///
        /// <para>It belongs in <c>Floors</c>, which is what it is. It is also in <c>Structure</c>
        /// beside the wall and the slab, because that is where a player already is when they are
        /// building — the owner asked for it by name (2026-09-17): *"it won't be painful having to
        /// go backwards and forwards between menus"*. A wall, its floor and the slab over it are
        /// one job and should be one row.</para>
        ///
        /// <para>Not a new idea in this table: <c>ui.arch.tool.reclaim</c> has sat in both
        /// <c>Structure</c> and <c>Salvage</c> since it was written, and <see cref="PaletteTools.TryGet"/>
        /// is keyed by the tool rather than by where it is drawn, so a key in two lists arms one
        /// tool and lights in both places.</para>
        /// </summary>
        [Test]
        public void PavingIsOfferedInBothFloorsAndStructure()
        {
            string[]? structure = null, floors = null;
            foreach (var (key, tools) in PaletteTools.Categories)
            {
                if (key == "ui.arch.category.structure") structure = tools;
                if (key == "ui.arch.category.floors") floors = tools;
            }

            Assert.That(floors, Does.Contain(PaletteTools.Paving), "paving is what the Floors row is for");
            Assert.That(structure, Does.Contain(PaletteTools.Paving),
                "and it is beside the wall too, so building a room is not two menus");
            Assert.That(structure, Does.Contain(PaletteTools.Slab),
                "the slab stays in Structure and nowhere else: it is structure");
            Assert.That(floors, Does.Not.Contain(PaletteTools.Slab),
                "a slab under Floors would be the confusion this rename was meant to end");
        }

        /// <summary>
        /// Only a thing made of something offers a material. An order is a verb applied to what is
        /// already there, so a "made of" row under the cancel tool would be asking what to cancel
        /// it out of.
        ///
        /// <para>The list is spelled out rather than derived from <c>WantsMaterial</c> itself,
        /// which would assert that a field equals itself. Seven things are built out of something
        /// today — a wall, a slab, paving, a ladder, a bed, a pillar and a stair — and an eighth
        /// arriving should have to be written here.</para>
        /// </summary>
        [Test]
        public void OnlyAThingMadeOfSomethingAsksWhatItIsMadeOf()
        {
            // A bed is built out of something exactly as a wall is; an order tool never is.
            string[] built =
            {
                PaletteTools.Wall, PaletteTools.Slab, PaletteTools.Paving, PaletteTools.Ladder,
                PaletteTools.Bed, PaletteTools.Pillar, PaletteTools.Stair,
            };
            foreach (PaletteTool tool in PaletteTools.Live)
                Assert.That(tool.WantsMaterial, Is.EqualTo(System.Array.IndexOf(built, tool.Key) >= 0),
                    $"{tool.Key} disagrees with itself about whether it is built out of something");
        }

        /// <summary>One key, one row. Two rows for a key would make which one wins an accident of order.</summary>
        [Test]
        public void NoToolIsListedTwice()
        {
            var seen = new HashSet<string>();
            foreach (PaletteTool tool in PaletteTools.Live)
                Assert.That(seen.Add(tool.Key), Is.True, $"{tool.Key} is in the live table twice");
        }

        /// <summary>
        /// Every chip the Build palette offers actually arms something.
        ///
        /// <para>The build cursor vanished for walls and floors alike after two merges rebuilt the
        /// palette (owner, 2026-09-17), and the first thing to rule out is the simplest: that
        /// pressing a chip no longer arms the tool at all. Nothing drawn in the world can be right
        /// if <c>Director.Tool</c> is still None, because the rig gates hover, the press and the
        /// preview on a tool being armed.</para>
        ///
        /// <para>Every key in every category, not the four this line of work touched: a palette is
        /// a table and the way a table breaks is one row at a time.</para>
        /// </summary>
        [Test]
        public void EveryChipInEveryCategoryArmsItsTool()
        {
            foreach (var (category, tools) in PaletteTools.Categories)
            foreach (string key in tools)
            {
                if (!PaletteTools.TryGet(key, out PaletteTool tool)) continue;

                var director = new DesignateDirector();
                Assume.That(director.Tool, Is.EqualTo(DesignateTool.None));

                tool.Arm(director);

                Assert.That(director.Tool, Is.Not.EqualTo(DesignateTool.None),
                    $"{key} in {category} armed nothing, so the world would show no cursor for it");
                Assert.That(tool.IsArmed(director), Is.True,
                    $"{key} in {category} armed a tool it does not then recognise as its own");
            }
        }
    }
}
