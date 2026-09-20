#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Manages the automatic opening, hold-open, and timeout-based closing lifecycle of doors.
    ///
    /// Runs at order 35 in the pawn phase, directly after <see cref="MovementSystem"/> (order 30).
    /// When a pawn steps into a door cell, the door opens immediately. If pawns occupy the cell
    /// or are actively walking into an already-open door, the door stays open. When the door and
    /// its approach are clear, the door closes after a short timeout.
    /// </summary>
    public sealed class DoorSystem : IWorldSystem
    {
        public struct ActiveDoor
        {
            public int Cell;
            public int TicksRemaining;

            public ActiveDoor(int cell, int ticksRemaining)
            {
                Cell = cell;
                TicksRemaining = ticksRemaining;
            }
        }

        readonly PawnContext _pawns;
        readonly List<ActiveDoor> _activeDoors = new List<ActiveDoor>();

        /// <summary>Ticks to remain open after a door is cleared of pawns (0.5 seconds at 60 tps).</summary>
        public const int DefaultCloseDelayTicks = 30;

        public DoorSystem(PawnContext pawns)
        {
            _pawns = pawns;
        }

        public string Name => "Doors";

        public TickPhase Phase => TickPhase.Pawns;

        public int Order => 35;

        public int ActiveCount => _activeDoors.Count;

        public bool IsOpen(int cell) =>
            (_pawns.Nav.Grid.Flags[cell] & NavFlags.DoorOpen) != 0;

        public void Tick(SimWorld world)
        {
            var pawns = _pawns.Pawns.All;

            // 1. Inspect all pawns:
            // - Any pawn standing in a door cell opens it and refreshes its timer.
            // - Any pawn actively moving into an already-open door refreshes its timer so the
            //   door does not slam shut mid-stride.
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                int cell = p.Cell;

                if ((_pawns.Nav.Grid.Flags[cell] & NavFlags.Door) != 0)
                {
                    _pawns.Nav.SetDoorOpen(cell, true);
                    SetActiveDoor(cell, DefaultCloseDelayTicks);
                }

                if (p.HasPath && p.PathIndex < p.Path.Length)
                {
                    int next = p.Path[p.PathIndex];
                    if ((_pawns.Nav.Grid.Flags[next] & NavFlags.DoorOpen) != 0)
                    {
                        SetActiveDoor(next, DefaultCloseDelayTicks);
                    }
                }
            }

            // 2. Decrement remaining open ticks and close expired doors in deterministic order
            for (int i = _activeDoors.Count - 1; i >= 0; i--)
            {
                ActiveDoor door = _activeDoors[i];
                if ((_pawns.Nav.Grid.Flags[door.Cell] & NavFlags.Door) == 0)
                {
                    _activeDoors.RemoveAt(i);
                    continue;
                }

                if (door.TicksRemaining <= 0)
                {
                    bool occupied = false;
                    for (int p = 0; p < pawns.Count; p++)
                    {
                        if (pawns[p].Cell == door.Cell)
                        {
                            occupied = true;
                            break;
                        }
                    }

                    if (!occupied)
                    {
                        _pawns.Nav.SetDoorOpen(door.Cell, false);
                        _activeDoors.RemoveAt(i);
                    }
                    else
                    {
                        door.TicksRemaining = DefaultCloseDelayTicks;
                        _activeDoors[i] = door;
                    }
                }
                else
                {
                    door.TicksRemaining--;
                    _activeDoors[i] = door;
                }
            }
        }

        void SetActiveDoor(int cell, int ticks)
        {
            int index = FindActiveDoor(cell);
            if (index >= 0)
            {
                _activeDoors[index] = new ActiveDoor(cell, ticks);
            }
            else
            {
                _activeDoors.Insert(~index, new ActiveDoor(cell, ticks));
            }
        }

        int FindActiveDoor(int cell)
        {
            int low = 0, high = _activeDoors.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int c = _activeDoors[mid].Cell;
                if (c == cell) return mid;
                if (c < cell) low = mid + 1;
                else high = mid - 1;
            }
            return ~low;
        }
    }
}
