#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Where a colonist may work (design 43 §4a), as the interface reads it. A string literal and
    /// not a shared constant, on <see cref="CombatAspectNames"/>' bargain: this assembly cannot
    /// reference <c>Odyssey.Sim</c>, so a test on each side holds its copy to the literal.
    /// Sparse: 1 while she is kept home, absent at Anywhere.
    /// </summary>
    public static class AreaAspectNames
    {
        public const string Area = "odyssey.pawn.area";

        public static readonly AspectKey AreaKey = AspectKey.Of(Area);
    }
}
