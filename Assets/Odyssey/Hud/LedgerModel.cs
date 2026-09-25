#nullable enable
using System.Collections.Generic;
using System.Linq;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>Which way a stock has moved since the ledger last took a baseline.</summary>
    public enum StockTrend
    {
        Steady,
        Rising,
        Falling,
    }

    /// <summary>
    /// One row of the stores panel (A1). <see cref="Real"/> rows are counted from the published
    /// frame; the rest are the resources of the future economy, kept in the list at zero and
    /// folded away behind the panel's disclosure rather than printed in grey down the side of the
    /// screen, so the region's eventual density is discoverable without being on show.
    /// </summary>
    public struct LedgerRow
    {
        public string IconKey;
        public string Name;
        public int Quantity;
        public bool Real;

        /// <summary>Which way the stock is going. Only <see cref="StockTrend.Falling"/> is drawn:
        /// a store going up is not news.</summary>
        public StockTrend Trend;

        /// <summary>True when there is none of this in the colony. Zero rows are the ones the
        /// disclosure hides.</summary>
        public bool Zero => Quantity <= 0;
    }

    /// <summary>
    /// The resource ledger's content. The simulation publishes things by item def; meals, wood and
    /// salvage are the three that exist, and they are counted as items rather than as piles —
    /// a pile of twenty meals is twenty in the ledger, which is the number the player is deciding
    /// on.
    ///
    /// <para><b>A falling stock is the one piece of state this model keeps.</b> The spec asks for
    /// a down-chevron and a row tint on anything that is going down, and "going down" cannot be
    /// read from a single frame. It is measured against a baseline resampled every
    /// <see cref="BaselineSeconds"/> rather than against the previous refresh, because the ledger
    /// refreshes four times a second and a hauler picking a stack up momentarily lowers every
    /// count on screen: compared frame to frame, every row on the panel would flicker amber all
    /// day. Compared against where the stock stood ten seconds ago, only a stock that is really
    /// draining does.</para>
    /// </summary>
    public sealed class LedgerModel
    {
        /// <summary>Cooked meals, all three kinds (design 48 §4): the food a player cooks and decides on.</summary>
        const string Meal = "ui.res.meal";

        /// <summary>The ration pack, which had the Meal row to itself until the kitchen.</summary>
        const string Rations = "ui.res.rations";
        const string Wood = "ui.res.wood";
        const string Scrap = "ui.res.scrap";

        /// <summary>
        /// The field crop (GR). It earns a row for the reason Meal has one: it is <b>food the
        /// player is deciding on</b>, and until 2026-09-20 it had nowhere on screen to be
        /// counted at all. A harvest left a pile, a hauler carried it to the store or a hungry
        /// colonist ate it, and nothing anywhere showed a carrot afterwards - which from the
        /// keyboard is indistinguishable from the crop vanishing, and is exactly how the owner
        /// reported it ("they seemed to disappear now"). The simulation was right the whole
        /// time: the ten-day field soak accounts for every carrot, 580 harvested against 501
        /// left on the map and the rest eaten.
        /// </summary>
        const string Carrots = "ui.res.carrots";

        /// <summary>How long a baseline stands before it is resampled.</summary>
        public const double BaselineSeconds = 10.0;

        /// <summary>
        /// How far a stock must have fallen below its baseline to be called falling, as a
        /// fraction. A colony of three constantly eats and constantly cooks, and a one-item dip
        /// out of eighty is not a trend anybody should be warned about.
        /// </summary>
        public const double FallingFraction = 0.10;

        /// <summary>The future economy's rows. Names come from the registry, as every row's does.</summary>
        static readonly string[] Planned = { Scrap, "ui.res.medkit" };

        /// <summary>Every key a row can carry, so a test can prove each is a name the registry knows.</summary>
        public static readonly string[] IconKeys = new[] { Meal, Rations, Wood, Carrots }.Concat(Planned).ToArray();

        public readonly List<LedgerRow> Rows = new List<LedgerRow>();

        readonly Dictionary<string, int> _baseline = new Dictionary<string, int>();
        double _baselineTakenAt = double.NegativeInfinity;

        /// <summary>How many rows have anything in them. The header reads "n / total".</summary>
        public int Stocked { get; private set; }

        /// <summary>Every row, stocked or not.</summary>
        public int Total => Rows.Count;

        /// <summary>Without a clock, which is what every existing caller and test wants: counts
        /// are correct and nothing is ever reported as falling.</summary>
        public void Refresh(WorldSnapshot snapshot) => Refresh(snapshot, double.NegativeInfinity);

        /// <summary>
        /// Recompute the rows. <paramref name="seconds"/> is wall-clock — the trend has to be
        /// measured in the time the player is looking at the panel, not in ticks, which run three
        /// times as fast at speed three and stop dead when paused.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, double seconds)
        {
            Rows.Clear();

            int meals = 0, rations = 0, salvage = 0, wood = 0, carrots = 0;
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                int stack = things[i].Stack;
                int def = things[i].DefIndex;
                if (def == ItemHandle.CookedMeal || def == ItemHandle.VegetableMeal || def == ItemHandle.BurntMeal)
                    meals += stack;
                else if (def == ItemHandle.Meal) rations += stack;
                else if (things[i].DefIndex == ItemHandle.Salvage) salvage += stack;
                else if (things[i].DefIndex == ItemHandle.Wood) wood += stack;
                else if (things[i].DefIndex == ItemHandle.Carrots) carrots += stack;
            }

            Add(Meal, meals, real: true);
            Add(Rations, rations, real: true);
            Add(Carrots, carrots, real: true);
            Add(Wood, wood, real: true);
            Add(Scrap, salvage, real: true);

            // A commodity has one row. Scrap is planned and, once salvage lies on the ground,
            // real as well; under one registry name the two rows would read as a duplicate.
            foreach (string key in Planned)
                if (!Has(key)) Add(key, 0, real: false);

            ApplyTrends(seconds);

            Stocked = 0;
            for (int i = 0; i < Rows.Count; i++) if (!Rows[i].Zero) Stocked++;
        }

        void Add(string key, int quantity, bool real) =>
            Rows.Add(new LedgerRow
            {
                IconKey = key,
                Name = Registry.Label(key),
                Quantity = quantity,
                Real = real,
                Trend = StockTrend.Steady,
            });

        void ApplyTrends(double seconds)
        {
            // No clock, no trend. The overload without one exists for callers and tests that only
            // want the counts, and a trend inferred from "how many times has Refresh been called"
            // would be a different measurement wearing the same name.
            if (double.IsNegativeInfinity(seconds)) return;

            bool resample = seconds - _baselineTakenAt >= BaselineSeconds;

            for (int i = 0; i < Rows.Count; i++)
            {
                LedgerRow row = Rows[i];
                if (!_baseline.TryGetValue(row.IconKey, out int was)) was = row.Quantity;

                if (row.Quantity > was) row.Trend = StockTrend.Rising;
                else if (was > 0 && was - row.Quantity >= was * FallingFraction)
                    row.Trend = StockTrend.Falling;

                Rows[i] = row;
            }

            if (!resample) return;
            _baselineTakenAt = seconds;
            foreach (LedgerRow row in Rows) _baseline[row.IconKey] = row.Quantity;
        }

        bool Has(string key)
        {
            for (int i = 0; i < Rows.Count; i++)
                if (Rows[i].IconKey == key) return true;
            return false;
        }
    }
}
