#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What drawing the home's edge costs, and when it is drawn at all (design 43 §5b).
    ///
    /// <para>The guard is <c>docs/bug-patterns.md</c> P10's: <see cref="AnyHomeIsAtMostTwoCalls"/>
    /// fails the moment the edge costs a submission per cell or per strip. The others hold the
    /// pass's promises: a hidden edge is not submitted, a still frame rebuilds nothing, nothing is
    /// drawn above the active layer, and no corner is covered twice.</para>
    ///
    /// <para>The fast tier compiles neither Presentation nor Editor, so this file is only ever
    /// proved by the Unity tier.</para>
    /// </summary>
    public class HomeEdgePassTests
    {
        static readonly GridSize Size = new GridSize(120, 120, 8);

        /// <summary>A frame carrying the border of a square home of side <paramref name="side"/> on each given layer.</summary>
        static WorldSnapshot Frame(int side, int version, params int[] layers)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 3);
            frame.SetHomeVersion(version);
            foreach (int y in layers)
            for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
            {
                byte edges = 0;
                if (x == 0) edges |= 1;
                if (x == side - 1) edges |= 2;
                if (z == 0) edges |= 4;
                if (z == side - 1) edges |= 8;
                if (edges != 0) frame.AddHomeCell(new HomeCellView(Size.Index(x + 2, z + 2, y), edges));
            }
            return frame;
        }

        [Test]
        public void AnyHomeIsAtMostTwoCalls()
        {
            using var pass = new HomeEdgePass();
            pass.Draw(Frame(11, 1, 3, 2), activeLayer: 3, lowestLayer: 0);
            Assert.That(pass.LastDrawCalls, Is.EqualTo(2), "one call for the active layer and one below");

            // A hundred cells a side is 396 border cells a layer: still two calls.
            pass.Draw(Frame(100, 2, 3, 2), activeLayer: 3, lowestLayer: 0);
            Assert.That(pass.LastDrawCalls, Is.EqualTo(2), "a bigger home cost more calls");
            Assert.That(pass.StripsOn(0), Is.EqualTo(400), "four sides of a hundred on the active layer");
        }

        [Test]
        public void AHiddenEdgeIsNotSubmittedAndShowingAgainRebuilds()
        {
            using var pass = new HomeEdgePass();
            WorldSnapshot frame = Frame(11, 1, 3);
            pass.Draw(frame, activeLayer: 3, lowestLayer: 0);
            int rebuilds = pass.Rebuilds;

            pass.Hide();
            Assert.That(pass.LastDrawCalls, Is.Zero, "the view is off and something was submitted");

            // Same version, shown again: the rows were not published while hidden, so it rebuilds.
            pass.Draw(frame, activeLayer: 3, lowestLayer: 0);
            Assert.That(pass.Rebuilds, Is.EqualTo(rebuilds + 1));
            Assert.That(pass.LastDrawCalls, Is.EqualTo(1));
        }

        [Test]
        public void AStillFrameRebuildsNothing()
        {
            using var pass = new HomeEdgePass();
            WorldSnapshot frame = Frame(11, 1, 3);
            pass.Draw(frame, activeLayer: 3, lowestLayer: 0);
            int rebuilds = pass.Rebuilds;

            for (int i = 0; i < 10; i++) pass.Draw(frame, activeLayer: 3, lowestLayer: 0);
            Assert.That(pass.Rebuilds, Is.EqualTo(rebuilds), "nothing moved, nothing rebuilt");

            pass.Draw(Frame(12, 2, 3), activeLayer: 3, lowestLayer: 0);
            Assert.That(pass.Rebuilds, Is.EqualTo(rebuilds + 1), "the control: a new version is a new edge");
            pass.Draw(Frame(12, 2, 3), activeLayer: 4, lowestLayer: 0);
            Assert.That(pass.Rebuilds, Is.EqualTo(rebuilds + 2), "the control: a new slice is a new picture");
        }

        /// <summary>Nothing above the active layer; the active layer at full strength and the ones below it faint.</summary>
        [Test]
        public void NothingAboveTheActiveLayerIsDrawn()
        {
            using var pass = new HomeEdgePass();
            pass.Draw(Frame(11, 1, 5, 3, 1), activeLayer: 3, lowestLayer: 2);
            Assert.That(pass.StripsOn(0), Is.EqualTo(44), "the active layer's square");
            Assert.That(pass.StripsOn(1), Is.Zero, "layer 5 is above and layer 1 below the drawn band");

            pass.Draw(Frame(11, 1, 5, 3, 1), activeLayer: 3, lowestLayer: 0);
            Assert.That(pass.StripsOn(1), Is.EqualTo(44), "the control: layer 1 is drawn once the band reaches it");
            Assert.That(HomeEdgePass.ColourOf(0).a, Is.EqualTo(0.70f).Within(1e-4f));
            Assert.That(HomeEdgePass.ColourOf(1).a, Is.EqualTo(0.30f).Within(1e-4f));
        }

        /// <summary>A corner cell's two strips meet without overlapping: the corner square is covered once.</summary>
        [Test]
        public void NoCornerIsCoveredTwice()
        {
            Span<Rect> strips = stackalloc Rect[4];
            int n = HomeEdgePass.StripsOf(1 | 2 | 4 | 8, strips);
            Assert.That(n, Is.EqualTo(4));

            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                area += strips[i].width * strips[i].height;
                for (int j = i + 1; j < n; j++)
                {
                    Rect a = strips[i], b = strips[j];
                    float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    Assert.That(w <= 1e-5f || h <= 1e-5f, Is.True, $"strips {i} and {j} overlap");
                }
            }

            // Four strips round a cell, corners once each: the ring's area exactly.
            float side = CellMetrics.SizeXZ, inner = side - 2f * HomeEdgePass.Width;
            Assert.That(area, Is.EqualTo(side * side - inner * inner).Within(1e-4f));

            // Every strip lies inside its cell.
            for (int i = 0; i < n; i++)
            {
                Assert.That(strips[i].xMin, Is.GreaterThanOrEqualTo(-CellMetrics.HalfXZ - 1e-5f));
                Assert.That(strips[i].xMax, Is.LessThanOrEqualTo(CellMetrics.HalfXZ + 1e-5f));
                Assert.That(strips[i].yMin, Is.GreaterThanOrEqualTo(-CellMetrics.HalfXZ - 1e-5f));
                Assert.That(strips[i].yMax, Is.LessThanOrEqualTo(CellMetrics.HalfXZ + 1e-5f));
            }
        }

        /// <summary>The grass is parted along the active layer's line and not along the faint one below.</summary>
        [Test]
        public void TheGrassPartsOnlyAlongTheActiveLine()
        {
            using var pass = new HomeEdgePass();
            pass.Draw(Frame(11, 1, 3), activeLayer: 3, lowestLayer: 0);
            Assert.That(pass.GrassPoints.Count, Is.EqualTo(44 * HomeEdgePass.GrassStampsPerSide));

            pass.Draw(Frame(11, 2, 2), activeLayer: 3, lowestLayer: 0);
            Assert.That(pass.StripsOn(1), Is.EqualTo(44), "the control: the lower line is drawn");
            Assert.That(pass.GrassPoints, Is.Empty, "the grass was parted for a faint line on a lower layer");
        }
    }
}
