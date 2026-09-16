#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One ledger row (A1). <see cref="Real"/> rows are counted from the published frame;
    /// the rest are the resources of the future economy, shown greyed with a tooltip rather
    /// than hidden, so the region's density is visible before the economy that fills it exists.
    /// </summary>
    public struct LedgerRow
    {
        public string IconKey;
        public string Name;
        public int Quantity;
        public bool Real;
    }

    /// <summary>
    /// The resource ledger's content. The simulation publishes things by item def; meals and
    /// salvage are the two that exist, and they are counted as stacks — the frame carries no
    /// stack sizes, so the row says how many piles, not how many meals, until the contract
    /// grows a quantity field. A number with a known limitation beats a precise-looking guess.
    /// </summary>
    public sealed class LedgerModel
    {
        static readonly (string key, string name)[] Planned =
        {
            ("ui.res.scrap", "Scrap"),
            ("ui.res.alloy", "Alloy"),
            ("ui.res.concrete", "Concrete"),
            ("ui.res.water", "Water"),
            ("ui.res.medkit", "Medkit"),
        };

        public readonly List<LedgerRow> Rows = new List<LedgerRow>();

        public void Refresh(WorldSnapshot snapshot)
        {
            Rows.Clear();

            // Stacks, not piles: a pile of twenty meals is twenty in the ledger, which is the
            // number the player is deciding on.
            int meals = 0, salvage = 0, wood = 0;
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                int stack = things[i].Stack;
                if (things[i].DefIndex == ItemHandle.Meal) meals += stack;
                else if (things[i].DefIndex == ItemHandle.Salvage) salvage += stack;
                else if (things[i].DefIndex == ItemHandle.Wood) wood += stack;
            }

            if (meals > 0) Rows.Add(new LedgerRow
                { IconKey = "ui.res.meal", Name = "Meals", Quantity = meals, Real = true });
            if (wood > 0) Rows.Add(new LedgerRow
                { IconKey = "ui.res.wood", Name = "Wood", Quantity = wood, Real = true });
            if (salvage > 0) Rows.Add(new LedgerRow
                { IconKey = "ui.res.scrap", Name = "Salvage", Quantity = salvage, Real = true });

            foreach (var (key, name) in Planned)
                Rows.Add(new LedgerRow { IconKey = key, Name = name, Quantity = 0, Real = false });
        }
    }
}
