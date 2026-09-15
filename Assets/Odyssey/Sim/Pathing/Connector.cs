#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pathing
{
    public enum ConnectorKind : byte
    {
        /// <summary>Two cells on the lower layer, two on the upper. Every mode may use it.</summary>
        Stair = 0,

        /// <summary>One cell and one cell. Colonists and bashers only; a bulky load or an animal cannot.</summary>
        Ladder = 1,

        /// <summary>One shaft cell per served layer. Cost is a per-tick snapshot, so every agent in a tick agrees.</summary>
        Lift = 2,
    }

    /// <summary>
    /// A connector declares <em>both</em> of its ends.
    ///
    /// This is the single most important negative lesson in <c>docs/research/c-cataclysm-dda.md</c>:
    /// CDDA resolves the far end of a staircase at run time by searching a 12-tile radius for a
    /// matching landing, has to tolerate a one-layer discontinuity in route validation as a
    /// consequence, and its NPC vertical navigation has been broken by that for a decade. Here
    /// the far end is data. There is no run-time search for a landing, anywhere, ever.
    ///
    /// Registration refuses a connector whose ends are not exactly one layer apart, which is the
    /// same rule the path validator enforces from the other side.
    /// </summary>
    public sealed class Connector
    {
        public readonly int Id;
        public readonly ConnectorKind Kind;

        /// <summary>Declared footprint on the lower layer, in ascending cell index.</summary>
        public readonly int[] LowerCells;

        /// <summary>Declared footprint on the upper layer, in ascending cell index.</summary>
        public readonly int[] UpperCells;

        public readonly int CostUp;
        public readonly int CostDown;
        public readonly byte ModeMask;

        public Connector(int id, ConnectorKind kind, int[] lowerCells, int[] upperCells, GridSize size)
        {
            if (lowerCells.Length == 0 || upperCells.Length == 0)
                throw new ArgumentException("A connector must declare cells at both ends.");

            Array.Sort(lowerCells);
            Array.Sort(upperCells);

            int lowerLayer = size.FromIndex(lowerCells[0]).Y;
            int upperLayer = size.FromIndex(upperCells[0]).Y;
            if (upperLayer != lowerLayer + 1)
                throw new ArgumentException(
                    "A connector joins exactly two adjacent layers. A declared end that skips a layer " +
                    "is the discontinuity this architecture refuses to tolerate.");

            foreach (int c in lowerCells)
                if (size.FromIndex(c).Y != lowerLayer)
                    throw new ArgumentException("All lower cells of a connector lie on one layer.");
            foreach (int c in upperCells)
                if (size.FromIndex(c).Y != upperLayer)
                    throw new ArgumentException("All upper cells of a connector lie on one layer.");

            Id = id;
            Kind = kind;
            LowerCells = lowerCells;
            UpperCells = upperCells;

            switch (kind)
            {
                case ConnectorKind.Stair:
                    CostUp = MoveCost.StairUp;
                    CostDown = MoveCost.StairDown;
                    ModeMask = TraverseModes.AllMask;
                    break;
                case ConnectorKind.Ladder:
                    CostUp = MoveCost.LadderUp;
                    CostDown = MoveCost.LadderDown;
                    // A hauler's bulky load and an animal's lack of hands both rule a ladder out.
                    ModeMask = (byte)(TraverseModes.Mask(TraverseMode.Colonist) |
                                      TraverseModes.Mask(TraverseMode.IgnoreDoors));
                    break;
                default:
                    CostUp = MoveCost.LiftUp;
                    CostDown = MoveCost.LiftDown;
                    ModeMask = TraverseModes.AllMask;
                    break;
            }
        }

        public NavFlags FootprintFlag => Kind switch
        {
            ConnectorKind.Stair => NavFlags.ConnectorStair,
            ConnectorKind.Ladder => NavFlags.ConnectorLadder,
            _ => NavFlags.ConnectorLift,
        };
    }
}
