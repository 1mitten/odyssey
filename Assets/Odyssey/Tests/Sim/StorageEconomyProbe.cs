#nullable enable
using System;
using System.Text;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// TEMPORARY. Prints what the golden colonies actually *do*, in numbers a state hash cannot
    /// fake, so that a re-bake can be shown to be the hash seeing the world differently rather
    /// than the colony living differently. Deleted once the measurement is recorded.
    /// </summary>
    public class StorageEconomyProbe
    {
        static string Signature(ColonyWorld colony)
        {
            var text = new StringBuilder();
            var items = colony.Pawns.Items;

            int live = 0;
            var perDef = new int[ItemHandle.Count];
            long cellSum = 0;
            for (int i = 0; i < items.Items.Count; i++)
            {
                ColonyItem item = items.Items[i];
                if (item.Despawned) continue;
                live++;
                if (item.DefIndex < perDef.Length) perDef[item.DefIndex] += item.Stack;
                cellSum += item.Cell;
            }

            text.Append("live=").Append(live);
            text.Append(" loose=").Append(items.LooseItems.Count);
            text.Append(" stored=").Append(items.StoredItems.Count);
            text.Append(" cellsum=").Append(cellSum);
            for (int d = 0; d < perDef.Length; d++) text.Append(" d").Append(d).Append('=').Append(perDef[d]);

            long pawnCells = 0, food = 0, rest = 0;
            var pawns = colony.Pawns.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                pawnCells += pawns[i].Cell;
                food += pawns[i].Needs[NeedIndex.Food];
                rest += pawns[i].Needs[NeedIndex.Rest];
            }

            text.Append(" pawncells=").Append(pawnCells);
            text.Append(" food=").Append(food);
            text.Append(" rest=").Append(rest);
            text.Append(" orders=").Append(colony.Designations.Cells.Count);
            return text.ToString();
        }

        [Test]
        public void PrintTheEconomies()
        {
            foreach (var (name, golden) in new[]
                     {
                         ("meadow", Golden.Meadow),
                         ("played", Golden.PlayedBoard),
                         ("city", Golden.City),
                     })
            {
                ColonyWorld colony = golden.Build();
                Console.WriteLine($"PROBE {name} generated: {Signature(colony)}");
                colony.World.Tick(golden.Ticks);
                Console.WriteLine($"PROBE {name} simulated: {Signature(colony)}");
            }
        }
    }
}
