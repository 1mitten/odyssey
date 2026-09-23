#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Pawns spawned in quick succession at one point land on separate tiles (owner, 2026-09-24:
    /// marauders spawned one after another "don't spawn from same tile so quickly — spawn on free
    /// tiles around"; design 33 §9h). The control is the first spawn, which lands exactly where the
    /// debug spawn always put it.
    /// </summary>
    public class SpawnSpreadTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            return ColonyWorld.Build(Size, 11u, scenario, barren: true, wooded: false);
        }

        static List<Pawn> Spawned(ColonyWorld colony, int before)
        {
            var list = new List<Pawn>();
            var all = colony.Pawns.Pawns.All;
            for (int i = before; i < all.Count; i++) list.Add(all[i]);
            return list;
        }

        static CellRef SpawnPoint(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            return new CellRef(start.X + 6, start.Z + 6, start.Y);
        }

        [Test]
        public void SixMaraudersSentInOneTickStandOnSixTiles()
        {
            var colony = Board();
            CellRef at = SpawnPoint(colony);
            int before = colony.Pawns.Pawns.Count;
            int expected = colony.Pawns.Cells.NearestWalkableInColumn(at.X, at.Z, at.Y);

            for (int i = 0; i < 6; i++)
                colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Marauder));
            colony.World.Tick();

            List<Pawn> spawned = Spawned(colony, before);
            Assert.That(spawned.Count, Is.EqualTo(6));
            Assert.That(spawned[0].Cell, Is.EqualTo(expected), "the first spawn moved from where it always landed");

            var cells = new HashSet<int>();
            foreach (Pawn p in spawned)
            {
                Assert.That(cells.Add(p.Cell), Is.True, "two spawned marauders share a tile");
                Assert.That(colony.Pawns.Cells.IsWalkable(p.Cell), Is.True);
                CellRef c = Size.FromIndex(p.Cell);
                Assert.That(System.Math.Max(System.Math.Abs(c.X - at.X), System.Math.Abs(c.Z - at.Z)),
                    Is.LessThanOrEqualTo(PawnRegistry.SpawnSpreadRings), "spread too far from the spawn point");
            }
        }

        [Test]
        public void SpawnsOnSuccessiveTicksDoNotStackEither()
        {
            var colony = Board();
            CellRef at = SpawnPoint(colony);
            int before = colony.Pawns.Pawns.Count;

            // One a tick, as fast as a player can click the row. Read where each landed on the tick
            // it arrived, and check it against where every earlier spawn stands on that tick.
            for (int i = 0; i < 4; i++)
            {
                colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Marauder));
                colony.World.Tick();
                List<Pawn> spawned = Spawned(colony, before);
                Pawn newest = spawned[spawned.Count - 1];
                for (int k = 0; k < spawned.Count - 1; k++)
                    Assert.That(spawned[k].Cell, Is.Not.EqualTo(newest.Cell), $"spawn {i} landed on spawn {k}'s tile");
            }
            Assert.That(Spawned(colony, before).Count, Is.EqualTo(4));
        }

        [Test]
        public void EveryKindSpreadsNotOnlyMarauders()
        {
            var colony = Board();
            CellRef at = SpawnPoint(colony);
            int before = colony.Pawns.Pawns.Count;
            int[] kinds = { PawnKindIndex.Colonist, PawnKindIndex.MiddenHog, PawnKindIndex.DuctRat, PawnKindIndex.Marauder };
            foreach (int kind in kinds)
                colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, kind));
            colony.World.Tick();

            var cells = new HashSet<int>();
            foreach (Pawn p in Spawned(colony, before))
                Assert.That(cells.Add(p.Cell), Is.True, $"a {p.Kind} landed on another spawn's tile");
        }
    }
}
