#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>What the home tests share (design 43): ordering a building, raising it, and a hearth.</summary>
    static class HomeFixture
    {
        /// <summary>Order this building here and answer the site's cell (the order lifts a click on the ground).</summary>
        public static int Order(ColonyWorld colony, CellRef cell, int building)
        {
            var before = new HashSet<int>(colony.Construction.Sites);
            Assume.That(colony.Construction.Place(cell, building, StuffHandle.Wood), Is.EqualTo(IntentRejection.None),
                $"building {building} could not be ordered at {cell}");
            foreach (int site in colony.Construction.Sites)
                if (!before.Contains(site)) return site;
            Assert.Fail("the order made no site");
            return -1;
        }

        /// <summary>Order this building here and raise it at once; answers the cell it stands in.</summary>
        public static int Raise(ColonyWorld colony, CellRef cell, int building)
        {
            int site = Order(colony, cell, building);
            Assume.That(colony.Construction.Raise(colony.Pawns, site), Is.True, $"building {building} could not be raised");
            return site;
        }

        /// <summary>A campfire raised here, which a colony with no hearth takes as its hearth.</summary>
        public static int Campfire(ColonyWorld colony, CellRef cell) => Raise(colony, cell, BuildingHandle.Campfire);
    }
}
