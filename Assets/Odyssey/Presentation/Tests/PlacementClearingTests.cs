#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The grass laid flat under what an armed tool would place (design 45 §13): the footprint's
    /// cells cleared while a tool is armed, and nothing while none is.
    /// </summary>
    public class PlacementClearingTests
    {
        static GrassClearance Field(Vector3 focus)
        {
            var field = new GrassClearance();
            field.Begin(focus);
            return field;
        }

        [Test]
        public void AnArmedToolClearsEveryCellOfItsFootprint()
        {
            GrassClearance field = Field(new Vector3(50f, 0f, 50f));
            var footprint = new List<(CellRef Min, CellRef Max)> { (new CellRef(18, 18, 3), new CellRef(22, 19, 3)) };

            Assert.That(PlacementClearing.Stamp(field, armed: true, footprint), Is.EqualTo(1), "one stamp for the whole box");
            for (int z = 18; z <= 19; z++)
            for (int x = 18; x <= 22; x++)
            {
                Vector3 centre = CellMetrics.Centre(x, z, 3);
                Assert.That(field.At(centre), Is.EqualTo(1f), $"cell ({x},{z}) is under the footprint");
                Vector3 corner = new Vector3(x * CellMetrics.SizeXZ + 0.05f, 0f, z * CellMetrics.SizeXZ + 0.05f);
                Assert.That(field.At(corner), Is.GreaterThan(0.7f), $"and so is its corner, to within a texel");
            }
            Assert.That(field.At(CellMetrics.Centre(18, 21, 3)), Is.Zero, "a cell two away is left standing");
            Assert.That(field.At(new Vector3(18 * CellMetrics.SizeXZ - PlacementClearing.Margin * 0.5f, 0f,
                CellMetrics.Centre(18, 18, 3).z)), Is.GreaterThan(0f).And.LessThan(1f), "the margin falls off");
        }

        [Test]
        public void NothingIsClearedWhileNoToolIsArmed()
        {
            GrassClearance field = Field(new Vector3(50f, 0f, 50f));
            var footprint = new List<(CellRef Min, CellRef Max)> { (new CellRef(18, 18, 3), new CellRef(22, 19, 3)) };
            Assert.That(PlacementClearing.Stamp(field, armed: false, footprint), Is.Zero);
            Assert.That(field.At(CellMetrics.Centre(20, 18, 3)), Is.Zero);
            Assert.That(field.Stamps, Is.Zero);
        }

        [Test]
        public void ABoxDraggedTheOtherWayRoundClearsTheSameCells()
        {
            GrassClearance a = Field(new Vector3(50f, 0f, 50f)), b = Field(new Vector3(50f, 0f, 50f));
            PlacementClearing.Stamp(a, true, new List<(CellRef, CellRef)> { (new CellRef(18, 18, 3), new CellRef(22, 19, 3)) });
            PlacementClearing.Stamp(b, true, new List<(CellRef, CellRef)> { (new CellRef(22, 19, 3), new CellRef(18, 18, 3)) });
            for (int x = 14; x < 26; x++)
            for (int z = 14; z < 24; z++)
                Assert.That(b.At(CellMetrics.Centre(x, z, 3)), Is.EqualTo(a.At(CellMetrics.Centre(x, z, 3))));
        }
    }
}
