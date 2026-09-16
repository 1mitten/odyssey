#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// Turns the connectors the stamper declared into portal edges in the navigation graph.
    ///
    /// <para><b>Why this is a separate step.</b> The stamper runs as pass 3 and damage as pass 4,
    /// so by the time anyone wants a nav graph the world has had three more passes at the shells:
    /// a storey may have been toppled, a slab holed, the run buried in rubble. A connector whose
    /// ends are no longer standable is worse than a missing one — it is a portal edge into rubble,
    /// and every path through it is a lie the path validator has no way to catch. So the ends are
    /// checked here, once, against the finished map.</para>
    ///
    /// <para>The check is deliberately the same question a pawn asks: can something stand on this
    /// cell. It is not "is there still a stair edifice here", because the connector's existence is
    /// declared by the template, not inferred from cell contents — inferring it is precisely the
    /// run-time search this architecture refuses (<see cref="Connector"/>).</para>
    /// </summary>
    public static class ConnectorRegistrar
    {
        /// <summary>What a registration run did, for the report and for tests to assert on.</summary>
        public readonly struct Result
        {
            public readonly int Declared;
            public readonly int Registered;

            public Result(int declared, int registered)
            {
                Declared = declared;
                Registered = registered;
            }

            /// <summary>Declared connectors whose ends damage made unusable.</summary>
            public int Dropped => Declared - Registered;

            public override string ToString() =>
                $"{Registered} of {Declared} connectors registered, {Dropped} dropped as unusable";
        }

        /// <summary>
        /// Register every declared connector whose ends still hold up. Call before the first
        /// <see cref="NavGraph.Rebuild"/>: a connector added afterwards needs another rebuild to
        /// show up in the region graph.
        /// </summary>
        public static Result Register(NavGraph nav, CellGrid grid, IReadOnlyList<StampedConnector> declared)
        {
            if (nav == null) throw new ArgumentNullException(nameof(nav));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (declared == null) throw new ArgumentNullException(nameof(declared));

            int registered = 0;
            for (int i = 0; i < declared.Count; i++)
            {
                var connector = declared[i];
                if (!Usable(grid, connector.LowerCells) || !Usable(grid, connector.UpperCells)) continue;

                nav.AddConnector(connector.Kind, Copy(connector.LowerCells), Copy(connector.UpperCells));
                registered++;
            }

            return new Result(declared.Count, registered);
        }

        /// <summary>
        /// Every declared cell must be somewhere a pawn could stand. One unusable cell drops the
        /// whole connector: half a stairwell is not a narrower stairwell, it is a portal whose far
        /// end is a hole.
        /// </summary>
        static bool Usable(CellGrid grid, int[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if ((uint)cells[i] >= (uint)grid.Size.CellCount) return false;
                if (!grid.IsWalkable(cells[i])) return false;
            }
            return true;
        }

        /// <summary>
        /// The connector sorts the arrays it is given, so it gets its own copy — otherwise
        /// registering twice would reorder the generator's record of what it stamped, and the
        /// second registration would differ from the first.
        /// </summary>
        static int[] Copy(int[] cells) => (int[])cells.Clone();
    }
}
