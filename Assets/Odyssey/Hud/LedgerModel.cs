#nullable enable
using System.Collections.Generic;
using System.Linq;
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
        const string Meal = "ui.res.meal";
        const string Wood = "ui.res.wood";
        const string Scrap = "ui.res.scrap";

        /// <summary>The future economy's rows, shown greyed until it exists. Names come from the
        /// registry, as every row's does.</summary>
        static readonly string[] Planned = { Scrap, "ui.res.alloy", "ui.res.concrete", "ui.res.water", "ui.res.medkit" };

        /// <summary>Every key a row can carry, so a test can prove each is a name the registry knows.</summary>
        public static readonly string[] IconKeys = new[] { Meal, Wood }.Concat(Planned).ToArray();

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

            if (meals > 0) Rows.Add(Row(Meal, meals, real: true));
            if (wood > 0) Rows.Add(Row(Wood, wood, real: true));
            if (salvage > 0) Rows.Add(Row(Scrap, salvage, real: true));

            // A commodity has one row. Scrap is planned and, once salvage lies on the ground,
            // real as well; under one registry name the two rows would read as a duplicate.
            foreach (string key in Planned)
                if (!Has(key)) Rows.Add(Row(key, 0, real: false));
        }

        static LedgerRow Row(string key, int quantity, bool real) =>
            new LedgerRow { IconKey = key, Name = Registry.Label(key), Quantity = quantity, Real = real };

        bool Has(string key)
        {
            for (int i = 0; i < Rows.Count; i++)
                if (Rows[i].IconKey == key) return true;
            return false;
        }
    }
}
