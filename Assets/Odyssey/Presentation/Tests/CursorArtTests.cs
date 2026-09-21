#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.Ui;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The pointer's art and the state machine that picks it.
    ///
    /// <para>Everything here is a fact that cannot be seen: a cursor one pixel too large stops
    /// being a hardware cursor and starts lagging, a hotspot on a transparent pixel is an
    /// inaccurate pointer, and neither shows up in a screenshot or fails a build.
    /// <c>docs/design/28-pointer-cursor.md</c>.</para>
    /// </summary>
    public class CursorArtTests
    {
        [TearDown]
        public void Forget() => CursorArt.Forget();

        static readonly DesignateTool[] Tools =
        {
            DesignateTool.Mine, DesignateTool.Fell, DesignateTool.Cancel, DesignateTool.Build,
            DesignateTool.Deconstruct, DesignateTool.GrowZone, DesignateTool.Stockpile,
        };

        /// <summary>
        /// <b>Above 32 px Windows cannot carry the cursor and Unity composites a software one
        /// instead</b>, which lags the real pointer and freezes with the frame. Since the work this
        /// art came from was a report that the pointer felt inaccurate, a 64 px cursor would have
        /// answered the complaint by making it worse — and the failure would have been invisible on
        /// the machine that built it.
        /// </summary>
        [Test]
        public void EveryCursorFitsTheHardwareCeiling()
        {
            Assert.That(CursorArt.Size, Is.LessThanOrEqualTo(CursorArt.HardwareCeiling));

            foreach (Texture2D texture in AllCursors())
            {
                Assert.That(texture.width, Is.LessThanOrEqualTo(CursorArt.HardwareCeiling), texture.name);
                Assert.That(texture.height, Is.LessThanOrEqualTo(CursorArt.HardwareCeiling), texture.name);
            }
        }

        /// <summary>
        /// A cursor is read at one pixel per pixel and must not be filtered or mipped — the same
        /// rule ADR 0007 puts on every other piece of interface art, for the same reason.
        /// </summary>
        [Test]
        public void EveryCursorIsPointFilteredAndUnmipped()
        {
            foreach (Texture2D texture in AllCursors())
            {
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point), texture.name);
                Assert.That(texture.mipmapCount, Is.EqualTo(1), texture.name);
            }
        }

        /// <summary>
        /// <b>A hotspot that is not on the art is an inaccurate cursor</b>, which is precisely the
        /// complaint this work came from, and it is invisible in every screenshot. Both hotspots
        /// must be inside the texture and on a pixel that is actually drawn: the arrow's on its
        /// tip, the crosshair's on its centre dot.
        /// </summary>
        [Test]
        public void TheHotspotIsInsideTheArtAndOnAnOpaquePixel()
        {
            AssertHotspot(CursorArt.Arrow, CursorArt.ArrowHotspot, "arrow");
            foreach (DesignateTool tool in Tools)
                AssertHotspot(CursorArt.Crosshair(tool), CursorArt.CrosshairHotspot, $"crosshair {tool}");
        }

        static void AssertHotspot(Texture2D texture, Vector2 hotspot, string what)
        {
            Assert.That(hotspot.x, Is.InRange(0f, texture.width - 1f), $"{what} hotspot x");
            Assert.That(hotspot.y, Is.InRange(0f, texture.height - 1f), $"{what} hotspot y");

            // Hotspots are measured from the top-left; Texture2D reads from the bottom-left.
            int x = Mathf.RoundToInt(hotspot.x);
            int y = texture.height - 1 - Mathf.RoundToInt(hotspot.y);
            Color pixel = texture.GetPixel(x, y);

            Assert.That(pixel.a, Is.GreaterThan(0.5f),
                $"The {what} hotspot lands on a transparent pixel, so the point of the cursor is "
                + "not where the cursor points.");
        }

        /// <summary>
        /// Each tool's crosshair wears that tool's own hue — the fifth surface an armed order
        /// appears on, and the only one that would otherwise have guessed. And each is baked once:
        /// a cursor rebuilt per frame is a texture allocated per frame.
        /// </summary>
        [Test]
        public void EveryToolHasItsOwnHueAndIsCachedOnce()
        {
            foreach (DesignateTool tool in Tools)
            {
                Texture2D first = CursorArt.Crosshair(tool);
                Assert.That(CursorArt.Crosshair(tool), Is.SameAs(first), $"{tool} rebaked its cursor");

                Color hue = HudTokens.Convert(OrderColours.Hue(tool));
                Color centre = first.GetPixel(
                    Mathf.RoundToInt(CursorArt.CrosshairHotspot.x),
                    first.height - 1 - Mathf.RoundToInt(CursorArt.CrosshairHotspot.y));

                Assert.That(Difference(centre, hue), Is.LessThan(0.02f),
                    $"The {tool} crosshair is not the colour OrderColours gives that tool.");
            }
        }

        /// <summary>
        /// <b>Two tools whose chips differ must not share a pointer.</b> The whole point of the
        /// tint is telling a player which order is in hand without looking away from the board.
        /// </summary>
        [Test]
        public void TwoToolsWithDifferentChipsDoNotShareACursor()
        {
            for (int a = 0; a < Tools.Length; a++)
            for (int b = a + 1; b < Tools.Length; b++)
            {
                Color first = HudTokens.Convert(OrderColours.Hue(Tools[a]));
                Color second = HudTokens.Convert(OrderColours.Hue(Tools[b]));
                if (Difference(first, second) < 0.02f) continue; // the chips agree; so may the cursors

                Assert.That(CursorArt.Crosshair(Tools[a]), Is.Not.SameAs(CursorArt.Crosshair(Tools[b])),
                    $"{Tools[a]} and {Tools[b]} have different chips and the same cursor.");
            }
        }

        // ------------------------------------------------------------------ the state machine

        /// <summary>
        /// The pointer follows the two facts that decide it, and the one that matters is the third
        /// row: <b>over the HUD, an armed tool reverts to the arrow</b>. With a tool in hand over a
        /// panel the world ghost is suppressed and nothing else says why.
        /// </summary>
        [Test]
        public void TheCursorFollowsTheArmedToolAndTheInterface()
        {
            Assert.That(CursorDirector.Decide(false, DesignateTool.None), Is.EqualTo(CursorLook.Default));
            Assert.That(CursorDirector.Decide(false, DesignateTool.Mine), Is.EqualTo(CursorLook.Tool));
            Assert.That(CursorDirector.Decide(true, DesignateTool.Mine), Is.EqualTo(CursorLook.Interface));
            Assert.That(CursorDirector.Decide(true, DesignateTool.None), Is.EqualTo(CursorLook.Interface));
        }

        /// <summary>The look picks the texture and the hotspot together — one cannot move without
        /// the other, which is how a crosshair ends up pointing from its corner.</summary>
        [Test]
        public void EachLookCarriesItsOwnHotspot()
        {
            Assert.That(CursorDirector.TextureFor(CursorLook.Default, DesignateTool.None),
                Is.SameAs(CursorArt.Arrow));
            Assert.That(CursorDirector.TextureFor(CursorLook.Interface, DesignateTool.Mine),
                Is.SameAs(CursorArt.Arrow));
            Assert.That(CursorDirector.TextureFor(CursorLook.Tool, DesignateTool.Mine),
                Is.SameAs(CursorArt.Crosshair(DesignateTool.Mine)));

            Assert.That(CursorDirector.HotspotFor(CursorLook.Default), Is.EqualTo(CursorArt.ArrowHotspot));
            Assert.That(CursorDirector.HotspotFor(CursorLook.Tool), Is.EqualTo(CursorArt.CrosshairHotspot));
        }

        // ------------------------------------------------------------------ helpers

        static System.Collections.Generic.IEnumerable<Texture2D> AllCursors()
        {
            yield return CursorArt.Arrow;
            foreach (DesignateTool tool in Tools) yield return CursorArt.Crosshair(tool);
        }

        static float Difference(Color a, Color b) =>
            Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
    }
}
