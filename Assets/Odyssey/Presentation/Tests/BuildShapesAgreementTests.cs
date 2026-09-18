#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// <b>The interface's copy of "what shape is this thing" must agree with the Defs.</b>
    ///
    /// <para><see cref="BuildShapes"/> is parallel to <see cref="BuildingHandle"/> by necessity:
    /// the real table lives in <c>Odyssey.Sim.Construction</c>, which the HUD assembly cannot see
    /// (ADR 0003), and the interface needs the answer before any intent exists. Parallel tables are
    /// allowed here; parallel tables that nothing compares are not.</para>
    ///
    /// <para><b>Written the day the gap cost something.</b> <c>Building_Ladder</c> gained
    /// <c>rotates</c> in the Defs and <see cref="BuildShapes.Rotates"/> was left alone, and every
    /// simulation test stayed green — because the consequence is not in the simulation at all. The
    /// rotate key is shared: <c>DesignateDirector.RotatableArmed</c> reads <b>this</b> table to
    /// decide whether R turns the ghost or raises the slice, so the def alone would have left R
    /// doing the wrong one of its two jobs and the player's rotation quietly discarded. The
    /// fixture that existed checked the two arrays' <i>lengths</i> and spot-checked the wall and
    /// the bed, which is exactly the shape of test that passes while the row that matters is
    /// wrong.</para>
    ///
    /// <para>It lives in this assembly rather than beside <c>BuildShapes</c> because this is the
    /// only tier that can see both halves — the same reason <see cref="FloorToolReachTests"/> is
    /// here.</para>
    /// </summary>
    public class BuildShapesAgreementTests
    {
        [Test]
        public void BuildShapesAgreeWithTheDefs()
        {
            Assert.That(BuildShapes.Cells.Length, Is.EqualTo(BuildingHandle.Count),
                "a buildable was added without a row in BuildShapes.Cells");
            Assert.That(BuildShapes.Rotates.Length, Is.EqualTo(BuildingHandle.Count),
                "a buildable was added without a row in BuildShapes.Rotates");

            for (int building = 0; building < BuildingHandle.Count; building++)
            {
                if (building == BuildingHandle.None) continue;

                BuildingDef def = ConstructionContent.BuildingAt(building);

                Assert.That(BuildShapes.CellsOf(building), Is.EqualTo(def.footprint),
                    $"{def.defName}: the interface thinks it is {BuildShapes.CellsOf(building)} " +
                    $"cells and the def says {def.footprint}");

                Assert.That(BuildShapes.CanRotate(building), Is.EqualTo(def.rotates),
                    $"{def.defName}: the interface thinks rotates={BuildShapes.CanRotate(building)} " +
                    $"and the def says {def.rotates}. This one is not cosmetic — the rotate key is " +
                    "shared with the slice, and the disagreement is R doing the other job.");
            }
        }
    }
}
