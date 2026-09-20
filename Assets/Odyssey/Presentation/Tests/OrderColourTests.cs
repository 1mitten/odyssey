#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Designations;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// Every kind of standing order is drawn in its own colour, and in the colour its palette chip
    /// wears.
    ///
    /// <para><b>Written because the mapping was not total.</b> It was
    /// <c>Kind == Mine ? mine : fell</c> — two branches for three kinds — so a cell marked for
    /// demolition was painted in the felling green, and nothing failed, because there was nothing
    /// asking.</para>
    ///
    /// <para><b>And narrowed on 2026-09-20, which is the more useful half of the story.</b> This
    /// file asserted totality and distinctness over a table of <c>Color</c> constants that lived
    /// in <c>OdysseyBootstrap</c> — and passed the whole time the board was telling the player a
    /// different thing from the palette, because a *wrong* colour satisfies totality perfectly.
    /// The table is <see cref="OrderColours"/> now, in a Unity-free assembly, and everything about
    /// which hue belongs to which tool is asserted by <c>OrderColoursTests</c> in the fast tier —
    /// where a mapping fault is caught in two seconds rather than in a playtest.</para>
    ///
    /// <para>What is left here is what only this tier can say: that the bridge from a
    /// <c>DesignationKind</c>, which the Hud assembly cannot name, to a <c>UnityEngine.Color</c>
    /// arrives at the right token.</para>
    /// </summary>
    public class OrderColourTests
    {
        /// <summary>
        /// Every kind the simulation can publish gets a colour that no other kind gets.
        ///
        /// <para>Distinctness is the half that bites. A total mapping that returned one colour for
        /// everything would be total and useless, and it is exactly what the fallback branch of a
        /// switch quietly does when a new kind arrives and nobody adds a case.</para>
        /// </summary>
        [Test]
        public void EveryKindOfOrderHasItsOwnColour()
        {
            var seen = new Dictionary<Color, DesignationKind>();

            foreach (DesignationKind kind in Enum.GetValues(typeof(DesignationKind)))
            {
                if (kind == DesignationKind.None) continue;

                Color colour = OdysseyBootstrap.OrderColour(kind);
                Assert.That(seen.ContainsKey(colour), Is.False,
                    $"{kind} is drawn in the same colour as {(seen.TryGetValue(colour, out var other) ? other : default)}, " +
                    "so the two orders are indistinguishable on the board");
                seen[colour] = kind;
            }

            Assert.That(seen, Has.Count.EqualTo(Enum.GetValues(typeof(DesignationKind)).Length - 1));
        }

        /// <summary>
        /// <b>A kind is drawn in the colour its own tool wears on the palette.</b>
        ///
        /// <para>This is the join the fast tier cannot make: <c>DesignationKind</c> lives in
        /// <c>Odyssey.Sim</c>, which <c>Odyssey.Hud</c> does not reference, so the Hud assembly
        /// keys its table on <see cref="DesignateTool"/> and carries the kind numbers as a
        /// restated contract. <c>OrderColoursTests</c> pins the numbers; this pins that the
        /// bootstrap converts the right one without losing anything on the way through
        /// <c>HudTokens.Convert</c>.</para>
        /// </summary>
        [Test]
        public void AKindIsDrawnInItsOwnToolsColour()
        {
            var pairs = new (DesignationKind Kind, DesignateTool Tool)[]
            {
                (DesignationKind.Mine, DesignateTool.Mine),
                (DesignationKind.Deconstruct, DesignateTool.Deconstruct),
                (DesignationKind.Fell, DesignateTool.Fell),
            };

            foreach ((DesignationKind kind, DesignateTool tool) in pairs)
            {
                Color drawn = OdysseyBootstrap.OrderColour(kind);
                Color wanted = Ui.HudTokens.Convert(OrderColours.Mark(tool));
                Assert.That(drawn, Is.EqualTo(wanted),
                    $"{kind} is drawn in a colour that is not {tool}'s own");
            }
        }

        /// <summary>
        /// <b>A demolition order is the palette's orange, not the cancel red</b> (owner,
        /// 2026-09-20: <i>"the placement shouldn't be red — it should use the same colour as
        /// deconstruct (the orange colour)"</i>).
        ///
        /// <para>This replaces <c>ADemolitionOrderIsRed</c>, which asserted the opposite and was
        /// right to at the time: §6a of <c>16-cancel-and-deconstruct.md</c> chose red on the
        /// owner's word, before there was a palette chip for the same tool in a different colour.
        /// The old assertion is the reason the change was caught by a test rather than by a
        /// playtest, which is what it was for.</para>
        /// </summary>
        [Test]
        public void ADemolitionOrderIsTheDeconstructOrangeAndNotTheCancelRed()
        {
            Color orange = OdysseyBootstrap.OrderColour(DesignationKind.Deconstruct);

            Assert.That(orange.r, Is.GreaterThan(0.7f), "it is not warm enough to read as a warning");
            Assert.That(orange.g, Is.GreaterThan(orange.r * 0.6f),
                "it reads as red rather than orange — the colour the interface uses for Cancel");
            Assert.That(orange.r, Is.GreaterThan(orange.b * 1.5f), "it has lost its warmth");

            Color cancel = Ui.HudTokens.Convert(OrderColours.Hue(DesignateTool.Cancel));
            Assert.That(orange.g, Is.GreaterThan(cancel.g),
                "deconstruct and cancel are the same hue again, which is the regression");
        }

        /// <summary>
        /// And it is visible over the thing it marks. A marker is translucent so the wall shows
        /// through it, and opaque enough to be seen at all — the failure at both ends is the same
        /// report: "there is no visual marker".
        /// </summary>
        [Test]
        public void AnOrderMarkerIsTranslucentButNotInvisible()
        {
            foreach (DesignationKind kind in Enum.GetValues(typeof(DesignationKind)))
            {
                if (kind == DesignationKind.None) continue;
                Color colour = OdysseyBootstrap.OrderColour(kind);
                Assert.That(colour.a, Is.InRange(0.2f, 0.8f), $"{kind}'s marker is {colour.a:0.00} alpha");
            }
        }

        /// <summary>
        /// A painted growing zone reads as a field — earthy brown, worked soil, the ground's own
        /// texture showing through (owner, 2026-09-18: the first green was hard to see on the
        /// surface) — and is a colour no order wears. It is the one thing drawn on the board that
        /// is not an order, so the distinctness argument
        /// <see cref="EveryKindOfOrderHasItsOwnColour"/> makes extends to it: a field painted in
        /// the felling green would be a patch of cells that look ordered for something.
        ///
        /// <para><b>Asked of what actually draws, since 2026-09-20.</b> This used to read
        /// <c>OdysseyBootstrap.ZoneTintColour</c>, the tint of a translucent cover mesh laid over
        /// the ground. There is no cover any more — the zone is a grade on the ground's own
        /// terrain bucket, which is what removed 2,065 draw calls a frame — so the colour to hold
        /// to this rule is the graded soil itself.</para>
        /// </summary>
        [Test]
        public void APaintedZoneReadsAsSoilAndAsNoOrder()
        {
            int tilled = TintCode.Tilled(TintCode.Daylit(
                TintCode.Terrain(Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainBareEarth), true));
            ChunkRenderer.ResolveColour(tilled, fallback: false, shade: 1f, out Color soil, out Color _);

            foreach (DesignationKind kind in Enum.GetValues(typeof(DesignationKind)))
            {
                if (kind == DesignationKind.None) continue;
                Assert.That(soil, Is.Not.EqualTo(OdysseyBootstrap.OrderColour(kind)),
                    $"worked soil is indistinguishable from a {kind} order");
            }

            Assert.That(soil.r, Is.GreaterThan(soil.g), "it does not read as soil");
            Assert.That(soil.g, Is.GreaterThan(soil.b), "it does not read as soil");
        }
    }
}
