#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>
    /// The layer above the boards (design 64 §4): the planet-level state, and every live board ticked
    /// together on one clock.
    ///
    /// <para><b>The invariant is that every board's <c>CurrentTick</c> equals every other's.</b> It is
    /// not a preference: pawns carry absolute ticks — a memory's expiry, the skill day, the next
    /// swing, a treatment's cooldown — so a colonist who crossed from a board that was behind would
    /// arrive with her thoughts expiring early. A site board is therefore built at the campaign's
    /// tick (<c>ColonyRequest.StartTick</c>) and ticked in the same step as home, and
    /// <see cref="Step"/> refuses to go on if the two ever disagree.</para>
    ///
    /// <para><b>Home's hash is untouched by being in a campaign.</b> The campaign reads each board and
    /// writes only through the seams a board already has, so a campaign with no expedition leaves
    /// home ticking exactly as it ticks alone — which is what lets every golden stay where it is.
    /// <see cref="ComputeHash"/> is a second, wider number over all of it.</para>
    ///
    /// <para>Engine-free like the rest of the simulation, and in the fast tier.</para>
    /// </summary>
    public sealed partial class Campaign
    {
        readonly List<Board> _boards = new List<Board>();
        readonly List<CampaignOrder> _orders = new List<CampaignOrder>();
        int _nextSlot;

        /// <summary>The seed the planet is regenerated from. Every site board's seed derives from it.</summary>
        public uint WorldSeed { get; }

        /// <summary>
        /// The planet, when the campaign has one. Never saved: rebuilt from <see cref="WorldSeed"/>,
        /// which is what the World screen does too (design 59 §3).
        /// </summary>
        public PlanetView? Planet { get; }

        /// <summary>The one counter every board's pawn ids come from (design 64 §4c).</summary>
        public PawnIdSource Ids { get; }

        public ColonyWorld Home => _boards[0].World;

        /// <summary>Every live board, home first, then site boards in slot order.</summary>
        public IReadOnlyList<Board> Boards => _boards;

        /// <summary>The campaign's clock, which is every board's clock.</summary>
        public int Tick => Home.World.CurrentTick;

        /// <summary>
        /// Wrap a colony that already exists. Its pawn ids move on to the campaign's shared counter,
        /// starting where the colony's own had got to, so nothing already alive changes id.
        /// </summary>
        public Campaign(ColonyWorld home, uint worldSeed, PlanetView? planet = null)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            WorldSeed = worldSeed;
            Planet = planet;
            Ids = new PawnIdSource();
            Ids.Reset(home.Pawns.Pawns.Ids.Peek);
            home.Pawns.Pawns.Ids = Ids;
            AddBoard(home, place: -1);
        }

        /// <summary>The board in a slot, or null if it has been discarded or never existed.</summary>
        public Board? BoardAt(int slot)
        {
            for (int i = 0; i < _boards.Count; i++)
                if (_boards[i].Slot == slot) return _boards[i];
            return null;
        }

        /// <summary>
        /// Queue an order for the start of the next <see cref="Step"/>. Orders are applied between
        /// ticks and never in the middle of one, the same promise the boards' own intents make.
        /// </summary>
        public void Submit(CampaignOrder order) => _orders.Add(order);

        /// <summary>
        /// One tick of everything: the queued orders, then home, then each site board in slot order,
        /// then the campaign's own phase (departures, the road, arrivals).
        /// </summary>
        public void Step()
        {
            ApplyOrders();
            int before = Tick;
            for (int i = 0; i < _boards.Count; i++) _boards[i].World.World.Tick();
            for (int i = 0; i < _boards.Count; i++)
            {
                int tick = _boards[i].World.World.CurrentTick;
                if (tick != before + 1)
                    throw new InvalidOperationException(
                        $"board {_boards[i].Slot} is at tick {tick} and home is at {before + 1}; " +
                        "every board must tick in step (design 64 §4b)");
            }
            AfterBoards();
        }

        /// <summary>Several ticks, as <see cref="Step()"/> would take them one by one.</summary>
        public void Step(int count)
        {
            for (int i = 0; i < count; i++) Step();
        }

        /// <summary>
        /// The whole campaign in one number: its own state, then home's hash, then each site
        /// board's in slot order. Home's own <c>ComputeStateHash</c> is not changed by this.
        /// </summary>
        public ulong ComputeHash()
        {
            StateHash hash = StateHash.New();
            hash.Add(WorldSeed);
            hash.Add(_boards.Count);
            hash.Add(_nextSlot);
            hash.Add(Ids.Peek);
            ContributeState(ref hash);
            for (int i = 0; i < _boards.Count; i++)
            {
                hash.Add(_boards[i].Slot);
                hash.Add(_boards[i].Place);
                hash.Add(_boards[i].World.World.ComputeStateHash().Value);
            }
            return hash.Value;
        }

        /// <summary>Put a board into the tick and hash order. Home is slot 0.</summary>
        internal Board AddBoard(ColonyWorld world, int place)
        {
            if (_boards.Count > 0 && world.World.CurrentTick != Tick)
                throw new InvalidOperationException(
                    $"a board at tick {world.World.CurrentTick} cannot join a campaign at {Tick}");
            world.Pawns.Pawns.Ids = Ids;
            var board = new Board(_nextSlot++, place, world);
            _boards.Add(board);
            return board;
        }

        /// <summary>Take a site board out of the tick and hash order. Home is never removed.</summary>
        internal void RemoveBoard(Board board)
        {
            if (board.IsHome) throw new InvalidOperationException("home is never discarded");
            _boards.Remove(board);
        }

        void ApplyOrders()
        {
            if (_orders.Count == 0) return;
            for (int i = 0; i < _orders.Count; i++)
            {
                bool handled = false;
                ApplyOrder(_orders[i], ref handled);
            }
            _orders.Clear();
        }

        // The pieces later units fill: an order's effect, the campaign's own phase after the boards
        // have ticked, and the campaign state the hash covers. Partial so each unit's file owns its
        // own part and this one stays the clock.
        partial void ApplyOrder(CampaignOrder order, ref bool handled);
        partial void AfterBoards();
        partial void ContributeState(ref StateHash hash);
    }
}
