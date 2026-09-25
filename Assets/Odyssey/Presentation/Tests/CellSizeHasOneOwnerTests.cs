#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The cell is one size, and this fails the day the two sides of the seam disagree about it
    /// (design 47 §3a). The <c>HopPriceHasOneOwnerTests</c> lesson again.
    ///
    /// <para><b>Why there are two copies at all.</b> ADR 0002 fixes a cell at 2.5 × 2.5 × 3.0 m.
    /// Presentation has always held it, in metres, in <see cref="CellMetrics"/> — "the one place
    /// metres exist" — because nothing simulated needed a metre. The pistol's fall-off does: a
    /// shot's distance is measured in millimetres over the anisotropic cell, so the simulation
    /// gained <see cref="GridSize.CellSizeXZMm"/> and <see cref="GridSize.CellSizeYMm"/>, and it
    /// cannot read <see cref="CellMetrics"/> without depending on UnityEngine. Two copies of a
    /// number agree by coincidence until one is edited; a bullet that flies a distance the drawn
    /// board does not have would be a silent disagreement between what is hit and what is seen.</para>
    /// </summary>
    public class CellSizeHasOneOwnerTests
    {
        [Test]
        public void TheMetresAreTheSimulationsMillimetres()
        {
            Assert.That(Mathf.RoundToInt(CellMetrics.SizeXZ * 1000f), Is.EqualTo(GridSize.CellSizeXZMm), "across");
            Assert.That(Mathf.RoundToInt(CellMetrics.SizeY * 1000f), Is.EqualTo(GridSize.CellSizeYMm), "up");
        }

        [Test]
        public void AFarCellsCornerIsWhereTheMillimetresPutIt()
        {
            // Not only the constants: the conversion the renderer actually places things with.
            Vector3 corner = CellMetrics.Corner(37, 21, 9);
            Assert.That(Mathf.RoundToInt(corner.x * 1000f), Is.EqualTo(37 * GridSize.CellSizeXZMm), "x");
            Assert.That(Mathf.RoundToInt(corner.z * 1000f), Is.EqualTo(21 * GridSize.CellSizeXZMm), "z");
            Assert.That(Mathf.RoundToInt(corner.y * 1000f), Is.EqualTo(9 * GridSize.CellSizeYMm), "layer");
        }
    }
}
