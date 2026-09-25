#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The grass taken off every ordered cell to its edge (design 45 §13a; owner, 2026-09-25: "the
    /// grass is still appearing on top of the selection tiles … all orders should be checked for
    /// this"). Laying it flat left blades across the plate; the cut takes them away, and these say
    /// where its edge falls and that every way an order reaches the ground makes one.
    /// </summary>
    public class OrderCutTests
    {
        static readonly Vector3 Focus = new Vector3(40f, 0f, 40f);

        static GrassClearance Field()
        {
            var field = new GrassClearance();
            field.Begin(Focus);
            return field;
        }

        /// <summary>
        /// The edge is the cell's to within a few centimetres, wherever the cell falls across the
        /// field's texels: a 2.5 m cell is six and two-thirds texels, so the cells below cross
        /// every phase of one.
        /// </summary>
        [Test]
        public void ACutEndsAtTheCellsOwnEdge()
        {
            const float Tolerance = 0.06f;
            for (int cx = 10; cx < 13; cx++)
            {
                GrassClearance field = Field();
                PlacementClearing.Rect(new CellRef(cx, 12, 1), new CellRef(cx, 12, 1), out Vector2 low, out Vector2 high);
                Assert.That(field.CutRect(low, high), Is.True);

                float z = (low.y + high.y) * 0.5f;
                Assert.That(field.IsCut(new Vector3((low.x + high.x) * 0.5f, 0f, z)), Is.True, "the middle");
                Assert.That(field.IsCut(new Vector3(low.x + Tolerance, 0f, z)), Is.True, $"just inside the left edge of cell {cx}");
                Assert.That(field.IsCut(new Vector3(high.x - Tolerance, 0f, z)), Is.True, $"just inside the right edge of cell {cx}");
                Assert.That(field.IsCut(new Vector3(low.x - Tolerance, 0f, z)), Is.False, $"just outside the left edge of cell {cx}");
                Assert.That(field.IsCut(new Vector3(high.x + Tolerance, 0f, z)), Is.False, $"just outside the right edge of cell {cx}");
                Assert.That(field.IsCut(new Vector3(low.x + 0.3f, 0f, low.y + 0.3f)), Is.True, "a corner is cut, not rounded off");
            }
        }

        /// <summary>A marked field is one bare patch: no strip of grass between two cells.</summary>
        [Test]
        public void TwoCellsSideBySideLeaveNoStripBetweenThem()
        {
            GrassClearance field = Field();
            PlacementClearing.Rect(new CellRef(10, 12, 1), new CellRef(10, 12, 1), out Vector2 aLow, out Vector2 aHigh);
            PlacementClearing.Rect(new CellRef(11, 12, 1), new CellRef(11, 12, 1), out Vector2 bLow, out Vector2 bHigh);
            field.CutRect(aLow, aHigh);
            field.CutRect(bLow, bHigh);
            float z = (aLow.y + aHigh.y) * 0.5f;
            for (float x = aLow.x + 0.05f; x < bHigh.x - 0.05f; x += 0.05f)
                Assert.That(field.IsCut(new Vector3(x, 0f, z)), Is.True, $"x = {x:0.00}");
        }

        /// <summary>A placement cuts its footprint while armed, and nothing while not.</summary>
        [Test]
        public void AnArmedToolCutsItsFootprintAndADisarmedOneNothing()
        {
            var footprint = new List<(CellRef Min, CellRef Max)> { (new CellRef(10, 10, 1), new CellRef(13, 11, 1)) };

            GrassClearance armed = Field();
            PlacementClearing.Stamp(armed, armed: true, footprint);
            for (int z = 10; z <= 11; z++)
            for (int x = 10; x <= 13; x++)
                Assert.That(armed.IsCut(CellMetrics.Centre(x, z, 1)), Is.True, $"cell ({x},{z})");
            Assert.That(armed.IsCut(CellMetrics.Centre(14, 10, 1)), Is.False, "the next cell keeps its grass");

            GrassClearance idle = Field();
            PlacementClearing.Stamp(idle, armed: false, footprint);
            Assert.That(idle.Cuts, Is.Zero);
        }

        static RenderTestWorld Board()
        {
            var world = new RenderTestWorld(16, 16, 4);
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
                world.Solid(x, z, 1);
            world.Publish();
            return world;
        }

        /// <summary>
        /// Every way an order reaches the ground goes through one plate, and every plate cuts its
        /// cell: a standing order's mark, a job's progress cut and a site's rising fill — the three
        /// shapes the renderer has. Each cell alone, so a plate that forgot the cut fails here and
        /// not in a photograph.
        /// </summary>
        [Test]
        public void EveryKindOfPlateCutsItsOwnCellAndNoOther()
        {
            var shapes = new (string Name, System.Action<ChunkRenderer, CellRef> Draw)[]
            {
                ("a standing order's mark", (r, c) => r.DrawCellMark(c, Color.magenta)),
                ("a mark drawn to the tile's edge", (r, c) => r.DrawCellMark(c, Color.magenta, inset: 0f)),
                ("a job's progress cut", (r, c) => r.DrawCellCut(c, 0.5f, Color.red)),
                ("a site's rising fill", (r, c) => r.DrawCellFill(c, 0.5f, Color.white)),
            };

            foreach (var shape in shapes)
            {
                RenderTestWorld world = Board();
                using var renderer = new ChunkRenderer(world.Model);
                renderer.Clearance.Begin(Focus);
                var cell = new CellRef(8, 8, 2);
                shape.Draw(renderer, cell);

                Vector3 centre = CellMetrics.Centre(cell.X, cell.Z, cell.Y);
                float half = CellMetrics.SizeXZ * 0.5f;
                Assert.That(renderer.Clearance.IsCut(centre), Is.True, shape.Name + ": the middle");
                Assert.That(renderer.Clearance.IsCut(centre + new Vector3(half - 0.1f, 0f, half - 0.1f)), Is.True,
                    shape.Name + ": into the corner, past the plate's inset");
                Assert.That(renderer.Clearance.IsCut(centre + new Vector3(half + 0.15f, 0f, 0f)), Is.False,
                    shape.Name + ": the next cell");
                Assert.That(renderer.Clearance.At(centre + new Vector3(half + 0.15f, 0f, 0f)), Is.GreaterThan(0f),
                    shape.Name + ": whose grass is laid flat at the margin, not left leaning over");
            }
        }
    }
}
