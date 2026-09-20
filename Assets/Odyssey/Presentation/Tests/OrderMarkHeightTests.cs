#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// <b>An order's mark sits on the face you read it from.</b>
    ///
    /// <para>Every standing order is one shape — a flat plate, inset from the cell's edges — and
    /// the only thing that varies is how high it sits. <c>WorldRenderModel.MarkHeight</c> is the
    /// one rule, and this is what pins it.</para>
    ///
    /// <para><b>It exists because deconstruct had no mark at all.</b> The height used to be a
    /// solid-terrain test, so a mine order went on top of the rock (right) and a deconstruct order
    /// went on the floor of a cell with a three-metre wall standing in it — drawn correctly,
    /// exactly where the wall's own panels hide it. That was reported as "deconstruct has no
    /// visual marker" (2026-09-17), answered then with a whole-cell wash, and answered properly on
    /// 2026-09-20 when the owner asked for the mining treatment instead: <i>"make it mark the tile
    /// for deconstruction instead like you would mark in mining"</i>. The answer was never a
    /// different shape — it was the same shape at the right height.</para>
    ///
    /// <para>Heights are asserted against <c>CellMetrics</c> and <c>BedShape</c> rather than
    /// against metres, so a bed redrawn taller or a cell resized moves the mark with it.</para>
    /// </summary>
    public class OrderMarkHeightTests
    {
        const int Layer = 1;

        static RenderTestWorld FlatGround(int size = 8)
        {
            var world = new RenderTestWorld(size, size, 4);
            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
                world.Solid(x, z, 0, CoreContent.TerrainRock);
            return world;
        }

        static float MarkAt(RenderTestWorld world, int x, int z, int y) =>
            world.Model.MarkHeight(world.Model.Size.Index(x, z, y));

        /// <summary>
        /// Rock is marked on its top face. Unchanged behaviour, asserted because it is the case
        /// every other one is measured against — and because the mine mark is the thing the owner
        /// named as the standard the others should meet.
        /// </summary>
        [Test]
        public void AMineOrderIsMarkedOnTopOfTheRock()
        {
            RenderTestWorld world = FlatGround().Publish();
            Assert.That(MarkAt(world, 2, 2, 0), Is.EqualTo(CellMetrics.SizeY).Within(0.001f),
                "solid rock is marked on its floor, which is inside the rock");
        }

        /// <summary>
        /// <b>And so is a wall, which is the fix.</b> A wall fills its cell exactly as rock does,
        /// so the face a player reads a demolition order from is its top.
        /// </summary>
        [Test]
        public void ADeconstructOrderOnAWallIsMarkedOnTopOfIt()
        {
            RenderTestWorld world = FlatGround()
                .Edifice(3, 3, Layer, CoreContent.EdificeWall)
                .Publish();

            Assert.That(MarkAt(world, 3, 3, Layer), Is.EqualTo(CellMetrics.SizeY).Within(0.001f),
                "the mark is at the wall's floor, which is where the wall hides it");
        }

        /// <summary>
        /// A bed does not fill its cell, so its mark goes on top of the bed rather than a whole
        /// cell up — which would hang it in the air a colonist's height above the mattress.
        /// <b>Both cells</b>, because a bed is one thing behind two of them and a player may click
        /// either half.
        /// </summary>
        [Test]
        public void ADeconstructOrderOnABedIsMarkedOnTopOfTheBed()
        {
            RenderTestWorld world = FlatGround().Bed(4, 4, Layer, facing: 0).Publish();

            Assert.That(MarkAt(world, 4, 4, Layer), Is.EqualTo(BedShape.Size.y).Within(0.001f),
                "the head cell");
            Assert.That(MarkAt(world, 4, 5, Layer), Is.EqualTo(BedShape.Size.y).Within(0.001f),
                "the foot cell");
            Assert.That(BedShape.Size.y, Is.LessThan(CellMetrics.SizeY),
                "the premise: a bed is knee high, so lifting its mark a whole cell would hang it in the air");
        }

        /// <summary>
        /// <b>A tree is marked on the ground it stands in</b>, and this is the assertion that
        /// stops the rule being written as "is anything here".
        ///
        /// <para>A fell order is read looking down at the grass the tree grows out of. Lifting it
        /// to the top of the cell would put it three metres up in the canopy — which is why the
        /// rule asks <c>OccludesFace</c>, a question trees answer no to, rather than asking
        /// whether the cell is occupied.</para>
        /// </summary>
        [Test]
        public void AFellOrderStaysOnTheGroundTheTreeStandsIn()
        {
            RenderTestWorld world = FlatGround()
                .Edifice(5, 5, Layer, NaturalContent.EdificeTreeConifer, blocking: false)
                .Publish();

            Assert.That(MarkAt(world, 5, 5, Layer), Is.EqualTo(0f).Within(0.001f),
                "a fell order was lifted off the ground");
        }

        /// <summary>
        /// An empty cell is marked on its floor — a build blueprint, and the control that keeps
        /// the rule from growing an invisible lid on every cell of the board.
        /// </summary>
        [Test]
        public void AnEmptyCellIsMarkedOnItsFloor()
        {
            RenderTestWorld world = FlatGround().Publish();
            Assert.That(MarkAt(world, 6, 6, Layer), Is.EqualTo(0f).Within(0.001f));
        }

        /// <summary>A cell index off the end of the board answers nought rather than throwing.</summary>
        [Test]
        public void AnIndexOffTheBoardAnswersNought()
        {
            RenderTestWorld world = FlatGround().Publish();
            Assert.That(world.Model.MarkHeight(-1), Is.EqualTo(0f));
            Assert.That(world.Model.MarkHeight(int.MaxValue), Is.EqualTo(0f));
        }
    }
}
