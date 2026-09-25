#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The area setting as the interface reads it (design 43 §4a). Sparse: published only at
    /// <see cref="PawnArea.Home"/>, so a colony nobody restricts publishes exactly the rows it did
    /// before, and <c>AspectScaleTests</c>' row count does not move.
    /// </summary>
    public static class AreaAspects
    {
        /// <summary>1 while the colonist is kept home; absent at the default.</summary>
        public const string AreaName = "odyssey.pawn.area";

        public static readonly AspectKey Area = AspectKey.Of(AreaName);
    }
}
