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

        /// <summary>
        /// The rate the pawn walks at, per mille (design 17 §5) — pace and condition composed. A
        /// fact about the pawn wherever she stands, unlike the tile readout's "walk speed",
        /// which is a fact about the cell and keeps the phrase.
        /// </summary>
        public static readonly AspectKey Move = AspectKey.Of("odyssey.pawn.rate.move");

        // ---- the pace's factors (design 17 §5a) ------------------------------------------------
        //
        // What Move is a product of, one row a factor, so the colonist pane can say why she walks
        // at the pace she does. Each is the value of the very method <see cref="Pawn.MoveRatePerMille"/>
        // multiplies, asked again at publish time — never a second formula — and each but the
        // rolled pace is **sparse**: absent means exactly 1,000, the factor costs her nothing. So a
        // colonist dry, fed and undrafted publishes one row more than before, and a colonist
        // starving in the rain while drafted publishes four.
        //
        // The species' own pace is not published: it is 1,000 for every person, and these rows go
        // out for persons only.

        /// <summary>The pace she was rolled with, per mille. Every colonist, always.</summary>
        public const string PaceRolledName = "odyssey.pawn.rate.move.rolled";

        /// <summary><see cref="Pawn.ConditionPerMille"/>, while below 1,000.</summary>
        public const string PaceConditionName = "odyssey.pawn.rate.move.condition";

        /// <summary><see cref="Pawn.WeatherPerMille"/>, while the sky is slowing her.</summary>
        public const string PaceWeatherName = "odyssey.pawn.rate.move.weather";

        /// <summary><see cref="Pawn.UrgencyPerMille"/>, while she is running.</summary>
        public const string PaceUrgencyName = "odyssey.pawn.rate.move.urgency";

        public static readonly AspectKey PaceRolled = AspectKey.Of(PaceRolledName);
        public static readonly AspectKey PaceCondition = AspectKey.Of(PaceConditionName);
        public static readonly AspectKey PaceWeather = AspectKey.Of(PaceWeatherName);
        public static readonly AspectKey PaceUrgency = AspectKey.Of(PaceUrgencyName);
    }
}
