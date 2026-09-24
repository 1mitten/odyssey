#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What drawing the power lines costs, and when it is drawn at all (design 32 §9).
    ///
    /// <para>The guard is <c>docs/bug-patterns.md</c> P10's: <see cref="AThousandLinesCostDrawsInColoursNotInCells"/>
    /// fails the moment the lines cost a submission per cell. The others hold the two promises the
    /// pass makes about hiding — that a hidden line is not submitted, and that a still frame
    /// rebuilds nothing.</para>
    ///
    /// <para>The fast tier compiles neither Presentation nor Editor, so this file is only ever
    /// proved by the Unity tier.</para>
    /// </summary>
    public class PowerLinePassTests
    {
        static readonly GridSize Size = new GridSize(64, 64, 8);

        /// <summary>A frame carrying a straight run of built live lines, and an order or two.</summary>
        static WorldSnapshot Frame(int built, int ordered = 0, int version = 1)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 2);
            frame.SetPowerVersion(version);
            for (int i = 0; i < built; i++)
            {
                int x = i % 60, z = i / 60 % 60, y = 2 + i / 3600;
                byte links = (byte)(x < 59 ? 1 << 1 : 0);
                frame.AddConduit(new ConduitView(Size.Index(x, z, y), ConduitKind.Built, PowerNetState.Live, links, 0));
            }
            for (int i = 0; i < ordered; i++)
                frame.AddConduit(new ConduitView(Size.Index(i, 63, 2), ConduitKind.Ordered, PowerNetState.Idle, 0, -1));
            return frame;
        }

        [Test]
        public void AThousandLinesCostDrawsInColoursNotInCells()
        {
            using var pass = new PowerLinePass();
            pass.Draw(Frame(1_000), visible: true, activeLayer: 2);

            // A thousand nodes and nearly a thousand rods, one colour, one tier: four calls of 511.
            Assert.That(pass.LastDrawCalls, Is.LessThanOrEqualTo(4));
            Assert.That(pass.LastDrawCalls, Is.GreaterThan(0), "the control: it drew");

            pass.Draw(Frame(2_000, version: 2), visible: true, activeLayer: 2);
            Assert.That(pass.LastDrawCalls, Is.LessThanOrEqualTo(8), "twice the lines, twice the instances, not a call each");
        }

        [Test]
        public void AHiddenLineIsNotSubmittedButAnOrderStillIs()
        {
            using var pass = new PowerLinePass();
            pass.Draw(Frame(200), visible: false, activeLayer: 2);
            Assert.That(pass.LastDrawCalls, Is.Zero, "built lines are hidden");

            pass.Draw(Frame(200, ordered: 3), visible: false, activeLayer: 2);
            Assert.That(pass.LastDrawCalls, Is.EqualTo(1), "an order is drawn like every other standing order");
        }

        [Test]
        public void AStillFrameRebuildsNothing()
        {
            using var pass = new PowerLinePass();
            WorldSnapshot frame = Frame(500);
            pass.Draw(frame, visible: true, activeLayer: 2);
            int rebuilds = pass.Rebuilds;

            for (int i = 0; i < 10; i++) pass.Draw(frame, visible: true, activeLayer: 2);
            Assert.That(pass.Rebuilds, Is.EqualTo(rebuilds), "nothing moved, nothing rebuilt");

            pass.Draw(frame, visible: true, activeLayer: 3);
            Assert.That(pass.Rebuilds, Is.EqualTo(rebuilds + 1), "the control: a new slice is a new picture");
        }

        [Test]
        public void EachStateHasItsOwnColour()
        {
            var colours = new Color[5];
            for (int k = 0; k < colours.Length; k++) colours[k] = PowerLinePass.ColourOf(k);
            for (int a = 0; a < colours.Length; a++)
            for (int b = a + 1; b < colours.Length; b++)
                Assert.That(colours[a], Is.Not.EqualTo(colours[b]), $"kinds {a} and {b} share a colour");
        }
    }
}
