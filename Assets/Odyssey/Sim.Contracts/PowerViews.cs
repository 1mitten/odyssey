#nullable enable

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// What a power net is doing (design 32 §5). The whole net is one of these: there is no
    /// shedding, so a net is never partly dark.
    /// </summary>
    public enum PowerNetState : byte
    {
        /// <summary>Nothing running and nothing wanted — lines with no generator and no demand.</summary>
        Idle = 0,

        /// <summary>What its running generators make covers what its switched-on consumers want.</summary>
        Live = 1,

        /// <summary>It wants more than it makes, so every consumer on it has stopped.</summary>
        Dark = 2,
    }

    /// <summary>What a published line cell is: laid, only ordered, or laid and marked to come up.</summary>
    public enum ConduitKind : byte
    {
        Built = 0,
        Ordered = 1,
        Marked = 2,
    }

    /// <summary>
    /// One line cell, for drawing (design 32 §9).
    ///
    /// <para><b>Built lines are published only while presentation is watching</b> — while the
    /// lines are shown — through <see cref="IntentKind.WatchPower"/> (process §3: a channel is
    /// published to a subscriber). Ordered and marked lines are published always, as every other
    /// standing order is, because an order is drawn whether or not the lines are.</para>
    ///
    /// <para><see cref="Links"/> is computed by the simulation so presentation never works out
    /// adjacency for itself: bit 0 north (+Z), 1 east (+X), 2 south, 3 west, 4 up, 5 down — a bit
    /// set where the cell across that face holds a line of the same kind of thing (built to built,
    /// ordered to ordered or built).</para>
    /// </summary>
    public readonly struct ConduitView
    {
        public readonly int CellIndex;
        public readonly ConduitKind Kind;
        public readonly PowerNetState State;
        public readonly byte Links;

        /// <summary>The key of the net a built line is on, or -1 for an order.</summary>
        public readonly int NetKey;

        public ConduitView(int cellIndex, ConduitKind kind, PowerNetState state, byte links, int netKey)
        {
            CellIndex = cellIndex;
            Kind = kind;
            State = state;
            Links = links;
            NetKey = netKey;
        }
    }

    /// <summary>A power building's part in its net.</summary>
    public enum PowerRole : byte
    {
        Generator = 1,
        Consumer = 2,
    }

    /// <summary>
    /// One power building, for the pane, the alerts and the overlay (design 32 §10). Published
    /// always: there are as many as the colony has built, which is a handful.
    /// </summary>
    public readonly struct PowerDeviceView
    {
        public readonly int HeadCell;

        /// <summary>The far cell of a two-cell building, or -1.</summary>
        public readonly int SecondCell;

        /// <summary>The <see cref="BuildingHandle"/> it was built from.</summary>
        public readonly byte Building;

        public readonly PowerRole Role;
        public readonly bool On;

        /// <summary>A consumer that is getting power, or a generator that is running.</summary>
        public readonly bool Powered;

        /// <summary>The key of the net it is attached to, or -1 where no line touches it.</summary>
        public readonly int NetKey;

        /// <summary>A generator's output or a consumer's draw, in watts, whatever it is doing now.</summary>
        public readonly int Watts;

        /// <summary>The watts a generator is carrying now; nought for a consumer.</summary>
        public readonly int LoadW;

        /// <summary>Fuel in the hopper and what it holds, milli-units; both nought for anything that burns nothing.</summary>
        public readonly int FuelMilli;
        public readonly int FuelCapacityMilli;

        public PowerDeviceView(int headCell, int secondCell, byte building, PowerRole role, bool on,
            bool powered, int netKey, int watts, int loadW, int fuelMilli, int fuelCapacityMilli)
        {
            HeadCell = headCell;
            SecondCell = secondCell;
            Building = building;
            Role = role;
            On = on;
            Powered = powered;
            NetKey = netKey;
            Watts = watts;
            LoadW = loadW;
            FuelMilli = fuelMilli;
            FuelCapacityMilli = fuelCapacityMilli;
        }

        public bool Covers(int cell) => cell == HeadCell || (SecondCell >= 0 && cell == SecondCell);

        public bool BurnsFuel => FuelCapacityMilli > 0;
    }

    /// <summary>One net's balance (design 32 §5). Published always, one row a net.</summary>
    public readonly struct PowerNetView
    {
        public readonly int Key;
        public readonly int SupplyW;
        public readonly int DemandW;
        public readonly PowerNetState State;

        public PowerNetView(int key, int supplyW, int demandW, PowerNetState state)
        {
            Key = key;
            SupplyW = supplyW;
            DemandW = demandW;
            State = state;
        }
    }
}
