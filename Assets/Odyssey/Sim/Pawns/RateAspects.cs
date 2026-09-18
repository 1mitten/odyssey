#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The names a colonist's rates are published under, minted the way
    /// <see cref="SkillAspects"/> mints the skill names (design 17 §3d, §5).
    ///
    /// <para>The rate reaches presentation as a pawn aspect rather than as a field on
    /// <c>PawnView</c>, which is the OQ-45 seam doing what it is for: <c>Sim.Contracts</c> never
    /// hears that rates exist, and the presentation that needs one — the stroke clock above
    /// all — asks for it by name.</para>
    /// </summary>
    public static class RateAspects
    {
        /// <summary>
        /// The work rate the pawn is paying at right now, per mille. 1,000 when the pawn is not
        /// working, because a figure that runs no stroke clock has no rate to publish.
        /// </summary>
        public static readonly AspectKey Work = AspectKey.Of("odyssey.pawn.rate.work");
    }
}
