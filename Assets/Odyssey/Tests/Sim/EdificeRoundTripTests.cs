#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A wall a colonist built: is it in the save, and is it in the state hash?
    ///
    /// <para><b>These tests were written to fail.</b> Deconstruct needs a standing wall to say what
    /// it is made of — the refund is half of what it cost, in the material it cost — and reading
    /// the save path suggested it could not, because <c>List&lt;PlacedEdifice&gt;</c> is not a
    /// saved component and <c>CellGrid.Edifice[cell]</c> stores only an index into a list that
    /// worldgen rebuilds from the seed. That was a prediction from reading code, which has been
    /// wrong on this project every time it mattered, so it was measured before a line of
    /// deconstruct was written (<c>docs/design/16-cancel-and-deconstruct.md</c> §4).</para>
    /// </summary>
    public class EdificeRoundTripTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);
        const uint Seed = 4242;

        static ColonyWorld Fresh() => ColonyWorld.Build(Size, Seed, ScenarioDef.Bare());

        /// <summary>
        /// Put a wooden wall up the way the build pipeline does, without the twenty thousand ticks
        /// of walking that the end-to-end test in <c>ConstructionTests</c> pays for. The raise
        /// itself is the real one.
        /// </summary>
        static int RaiseAWoodenWall(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            for (int radius = 1; radius < 8; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;
                int index = Size.Index(x, z, start.Y);
                if (!colony.Construction.Allows(index)) continue;

                Assume.That(colony.Construction.Place(Size.FromIndex(index), BuildingHandle.Wall, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None));
                colony.Construction.Raise(colony.Pawns, index);
                Assume.That(colony.Grid.Edifice[index], Is.GreaterThanOrEqualTo(0), "the wall went up");
                return index;
            }

            return -1;
        }

        /// <summary>
        /// The claim deconstruct rests on: a wall that is standing can be asked what it is made of,
        /// after a save and a load, and answers the same as before.
        /// </summary>
        [Test]
        public void AWallRemembersWhatItIsMadeOfAcrossASave()
        {
            ColonyWorld original = Fresh();
            int cell = RaiseAWoodenWall(original);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            PlacedEdifice before = original.Outcome.Edifices[original.Grid.Edifice[cell]];
            Assume.That(before.Stuff, Is.EqualTo(NaturalContent.StuffWood));

            ColonyWorld restored = Fresh();
            restored.Load(original.Save());

            int handle = restored.Grid.Edifice[cell];
            Assert.That(handle, Is.GreaterThanOrEqualTo(0), "the cell still says a wall stands there");
            Assert.That(handle, Is.LessThan(restored.Outcome.Edifices.Count),
                "the cell's edifice handle points past the end of the restored list, so the wall " +
                "the colonist built is not in the save at all");

            PlacedEdifice after = restored.Outcome.Edifices[handle];
            Assert.That(after.Def, Is.EqualTo(before.Def), "it is still a wall");
            Assert.That(after.Stuff, Is.EqualTo(before.Stuff), "it is still made of wood");
            Assert.That(after.CellIndex, Is.EqualTo(cell), "it is still in the same cell");
        }

        /// <summary>
        /// Two colonies alike but for the material of one wall must not hash the same.
        ///
        /// <para>The same argument OQ-50 made about the world grid, one level down: a hash that
        /// wrongly says two worlds are identical is worse than no hash, because every determinism
        /// gate in the project is built on it agreeing only when it should.</para>
        /// </summary>
        [Test]
        public void AWallsMaterialIsInTheStateHash()
        {
            ColonyWorld wood = Fresh();
            int woodCell = RaiseAWoodenWall(wood);
            Assume.That(woodCell, Is.GreaterThanOrEqualTo(0));

            ColonyWorld stone = Fresh();
            CellRef at = Size.FromIndex(woodCell);
            Assume.That(stone.Construction.Place(at, BuildingHandle.Wall, StuffHandle.Stone),
                Is.EqualTo(IntentRejection.None));
            stone.Construction.Raise(stone.Pawns, woodCell);

            Assert.That(stone.World.ComputeStateHash().Value,
                Is.Not.EqualTo(wood.World.ComputeStateHash().Value),
                "a wooden wall and a stone wall in the same cell hash identically, so what a " +
                "building is made of is outside the hash");
        }
    }
}
