#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The number a store is called by is published with it (design 35 §2), and it is the number
    /// <see cref="StorageZones.OrdinalOfCell"/> gives — the same one the inspect pane reads through
    /// <c>CellDetail.StorageOrdinal</c>. The Inventory tab names every store at once off the
    /// published rows; if the two disagreed, "Go to Stockpile 2" would open a pane titled
    /// "Stockpile 3".
    /// </summary>
    public class StoreOrdinalTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);
        const uint Seed = 20260923;

        static ColonyWorld Fresh()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            return ColonyWorld.Build(Size, Seed, scenario);
        }

        /// <summary>Cells near the start a shelf may stand in and a stockpile may be painted on.</summary>
        static List<int> OpenCells(ColonyWorld colony, int count)
        {
            var cells = new List<int>();
            CellRef start = colony.Start;
            for (int radius = 2; radius < 14 && cells.Count < count; radius += 2)
            for (int dx = -radius; dx <= radius && cells.Count < count; dx += 3)
            {
                int x = start.X + dx, z = start.Z + radius;
                if (!Size.Contains(x, z, start.Y)) continue;
                int cell = Size.Index(x, z, start.Y);
                if (!colony.Construction.Allows(cell, BuildingHandle.Shelf)) continue;
                cells.Add(cell);
            }
            return cells;
        }

        [Test]
        public void EveryStoreIsPublishedWithTheNumberThePaneGivesIt()
        {
            ColonyWorld colony = Fresh();
            StorageZones storage = colony.Pawns.Storage!;
            List<int> cells = OpenCells(colony, 3);
            Assume.That(cells.Count, Is.EqualTo(3), "the fixture found no room for two zones and a shelf");

            // Two painted zones and a shelf between them, so the one series has to interleave.
            foreach (int zone in new[] { cells[0], cells[2] })
                Assume.That(storage.Designate(Size.FromIndex(zone), zone, StoragePreset.Everything),
                    Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Construction.Place(Size.FromIndex(cells[1]), BuildingHandle.Shelf, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, cells[1]);

            colony.World.Tick();
            WorldSnapshot frame = colony.World.Views.Current;

            var seen = new HashSet<int>();
            foreach (StoreView store in frame.Stores)
            {
                Assert.That(store.Ordinal, Is.EqualTo(storage.OrdinalOf(storage.ZoneAt(store.CellIndex))),
                    "a zone cell was published under a different number from the one the pane says");
                seen.Add(store.Ordinal);
            }
            Assert.That(frame.StorageUnits.Length, Is.EqualTo(1));
            StorageUnitView shelf = frame.StorageUnits[0];
            Assert.That(shelf.Ordinal, Is.EqualTo(storage.OrdinalOfCell(shelf.CellIndex)),
                "the shelf was published outside the series zones are numbered in");
            seen.Add(shelf.Ordinal);

            Assert.That(seen, Is.EquivalentTo(new[] { 1, 2, 3 }),
                "three stores must be three numbers, whichever kind each is");
        }
    }
}
