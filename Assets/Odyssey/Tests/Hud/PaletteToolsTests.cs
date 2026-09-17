#nullable enable
using System.Collections.Generic;
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
            foreach (var (_, _, tools) in PaletteTools.Categories)
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
        /// The pinned row is two chips and should stay small. A row that grows is a second palette,
        /// and the whole point of it is to be the short list of things that are true whatever the
        /// player is doing.
        /// </summary>
        [Test]
        public void ThePinnedRowStaysShort()
        {
            Assert.That(PaletteTools.Pinned.Length, Is.LessThanOrEqualTo(3));
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
                foreach (var (_, label, tools) in PaletteTools.Categories)
                    Assert.That(tools, Does.Not.Contain(pinned),
                        $"{pinned} is pinned and also listed under {label}, so it draws twice");
        }

        /// <summary>The Orders row keeps the two tools whose orders Cancel takes off.</summary>
        [Test]
        public void TheOrdersRowOffersTheOrdersAPlayerGives()
        {
            string[]? orders = null;
            foreach (var (key, _, tools) in PaletteTools.Categories)
                if (key == "ui.arch.category.orders")
                    orders = tools;

            Assert.That(orders, Is.Not.Null, "the palette has an Orders category");
            Assert.That(orders, Does.Contain(PaletteTools.Mine));
            Assert.That(orders, Does.Contain(PaletteTools.Fell));
        }

        /// <summary>
        /// Only a thing made of something offers a material. An order is a verb applied to what is
        /// already there, so a "made of" row under the cancel tool would be asking what to cancel
        /// it out of.
        ///
        /// <para>The list is spelled out rather than derived from <c>WantsMaterial</c> itself,
        /// which would assert that a field equals itself. Two things are built out of something
        /// today — a wall and a floor — and a third arriving should have to be written here.</para>
        /// </summary>
        [Test]
        public void OnlyAThingMadeOfSomethingAsksWhatItIsMadeOf()
        {
            foreach (PaletteTool tool in PaletteTools.Live)
                Assert.That(tool.WantsMaterial,
                    Is.EqualTo(tool.Key == PaletteTools.Wall || tool.Key == PaletteTools.Floor),
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
    }
}
