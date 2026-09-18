#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Designations;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// Every kind of standing order is drawn in its own colour.
    ///
    /// <para><b>Written because the mapping was not total.</b> It was
    /// <c>Kind == Mine ? mine : fell</c> — two branches for three kinds — so a cell marked for
    /// demolition was painted in the felling green, and nothing failed, because there was nothing
    /// asking. That is the same fault as a palette chip that arms a tool and never lights, and it
    /// is the second time this shape has cost a playtest.</para>
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
        /// A demolition order is red — the owner asked for it by name, and it is the only standing
        /// order that destroys something the colony has already made.
        /// </summary>
        [Test]
        public void ADemolitionOrderIsRed()
        {
            Color red = OdysseyBootstrap.OrderColour(DesignationKind.Deconstruct);

            Assert.That(red.r, Is.GreaterThan(0.7f), "it is not red enough to read as a warning");
            Assert.That(red.r, Is.GreaterThan(red.g * 2f), "it reads as orange or yellow, not red");
            Assert.That(red.r, Is.GreaterThan(red.b * 2f));
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
        /// The growing-zone tint is a colour no order uses, and it reads as a field: green with
        /// the soil showing through. It is the one overlay on the board that is not an order, so
        /// the distinctness argument <see cref="EveryKindOfOrderHasItsOwnColour"/> makes extends
        /// to it — a zone painted in a felling green would be a field of cells that look ordered
        /// for something.
        /// </summary>
        [Test]
        public void TheZoneTintIsAColourNoOrderUses()
        {
            Color zone = OdysseyBootstrap.ZoneTintColour;

            foreach (DesignationKind kind in Enum.GetValues(typeof(DesignationKind)))
            {
                if (kind == DesignationKind.None) continue;
                Assert.That(zone, Is.Not.EqualTo(OdysseyBootstrap.OrderColour(kind)),
                    $"the zone tint is indistinguishable from a {kind} order");
            }

            Assert.That(zone.g, Is.GreaterThan(zone.r), "it does not read as green");
            Assert.That(zone.g, Is.GreaterThan(zone.b), "it does not read as green");
            Assert.That(zone.a, Is.InRange(0.2f, 0.8f),
                $"the zone tint is {zone.a:0.00} alpha — the soil must show through and a tint nobody sees is worse than none");
        }
    }
}
