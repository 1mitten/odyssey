#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <b>A dragged run of buildings lands on one layer.</b>
    ///
    /// <para>Written from the owner's screenshot of 2026-09-18: a floor dragged over a walled room
    /// built the ring on the storey above and left a hole in the middle, with the storey below
    /// showing through it — reported as *"it constructed stone/steel floor on the floor below"*.
    /// The lift (<c>WhereItWouldLand</c>) is per cell and conditional, so one box resolved to two
    /// layers: the perimeter cells sit over walls and lifted, the interior cells sit over open air
    /// and did not.</para>
    ///
    /// <para>The fault is in the <i>run</i>, so the tests are about a run. Every existing
    /// construction test places one cell by hand, which is exactly why none of them could see
    /// this.</para>
    /// </summary>
    public class FloorRunTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
        }

        /// <summary>Walls round the outside of a 4x4 patch, as a room to roof over.</summary>
        static void ARoomOnTheGround(ColonyWorld colony, out int x0, out int z0, out int y)
        {
            CellRef start = colony.Start;
            y = start.Y;
            x0 = start.X + 2;
            z0 = start.Z + 2;

            for (int dz = 0; dz < 4; dz++)
            for (int dx = 0; dx < 4; dx++)
            {
                if (dx != 0 && dz != 0 && dx != 3 && dz != 3) continue;
                int cell = Size.Index(x0 + dx, z0 + dz, y);
                Assume.That(colony.Construction.Place(
                        Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None));
                colony.Construction.Raise(colony.Pawns, cell);
                colony.World.Tick();
            }
        }

        /// <summary>The box a player drags over the room, as the director would hand it over.</summary>
        static List<CellRef> TheWholeRoom(int x0, int z0, int y)
        {
            var cells = new List<CellRef>();
            for (int dz = 0; dz < 4; dz++)
            for (int dx = 0; dx < 4; dx++)
                cells.Add(new CellRef(x0 + dx, z0 + dz, y));
            return cells;
        }

        /// <summary>
        /// <b>The measurement that named the bug.</b> Cell by cell, the same box resolves to two
        /// different layers — and it is the interior, the part a player is least likely to notice
        /// until the roof has a hole in it, that stays behind.
        /// </summary>
        [Test]
        public void TheLiftOnItsOwnSendsOneBoxToTwoLayers()
        {
            ColonyWorld colony = Board();
            ARoomOnTheGround(colony, out int x0, out int z0, out int y);

            var layers = new HashSet<int>();
            foreach (CellRef at in TheWholeRoom(x0, z0, y))
                layers.Add(Size.FromIndex(
                    colony.Construction.WhereItWouldLand(Size.Index(at), BuildingHandle.Floor)).Y);

            Assert.That(layers, Has.Count.EqualTo(2),
                "the per-cell lift really does split one drag across two storeys — "
                + "this is the fault, pinned so the fix below cannot be mistaken for a no-op");
        }

        /// <summary>
        /// And the run rule collapses it to one: the storey the player is plainly pointing at.
        /// </summary>
        [Test]
        public void ARunLandsOnOneLayerAndItIsTheStoreyAbove()
        {
            ColonyWorld colony = Board();
            ARoomOnTheGround(colony, out int x0, out int z0, out int y);

            Assert.That(colony.Construction.RunLayerFor(TheWholeRoom(x0, z0, y), BuildingHandle.Floor),
                Is.EqualTo(y + 1),
                "a floor dragged over a room goes on top of it, every cell of it");
        }

        /// <summary>
        /// <b>The whole room roofs over, with no hole.</b> The assertion that matters is the count:
        /// before the run rule this built twelve of sixteen, and twelve is what a ring looks like.
        /// </summary>
        [Test]
        public void AFloorDraggedOverARoomRoofsAllOfIt()
        {
            ColonyWorld colony = Board();
            ARoomOnTheGround(colony, out int x0, out int z0, out int y);

            List<CellRef> box = TheWholeRoom(x0, z0, y);
            int runY = colony.Construction.RunLayerFor(box, BuildingHandle.Floor);

            foreach (CellRef at in box)
            {
                var on = new CellRef(at.X, at.Z, runY);
                Assert.That(colony.Construction.Place(on, BuildingHandle.Floor, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None), $"the order at {on} was refused");
                colony.Construction.Raise(colony.Pawns, Size.Index(on));
            }

            colony.World.Tick();

            int laid = 0;
            foreach (CellRef at in box)
                if (colony.Grid.Floor[Size.Index(new CellRef(at.X, at.Z, runY))] != CoreContent.SlabNone)
                    laid++;

            Assert.That(laid, Is.EqualTo(16), "a roof with a hole in it is the reported bug");
        }

        /// <summary>
        /// <b>The rule has to be idempotent or it would fight the lift.</b> The cells the run rule
        /// hands back are asked again by <c>Place</c>, one at a time — so if a cell already on the
        /// open layer lifted again, the order would climb a storey per drag.
        /// </summary>
        [Test]
        public void AskingTheRunRuleTwiceGivesTheSameLayer()
        {
            ColonyWorld colony = Board();
            ARoomOnTheGround(colony, out int x0, out int z0, out int y);

            List<CellRef> box = TheWholeRoom(x0, z0, y);
            int once = colony.Construction.RunLayerFor(box, BuildingHandle.Floor);

            var lifted = new List<CellRef>();
            foreach (CellRef at in box) lifted.Add(new CellRef(at.X, at.Z, once));

            Assert.That(colony.Construction.RunLayerFor(lifted, BuildingHandle.Floor), Is.EqualTo(once),
                "a run already on its layer must stay there");
        }
    }
}
