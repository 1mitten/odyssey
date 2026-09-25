#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Weather
{
    /// <summary>
    /// One kind of sky (design 43 §4): what it does at full intensity, how often each season rolls
    /// it, and how long a spell of it lasts. Numbers in XML (<c>Defs/Core/World/Weather.xml</c>),
    /// the blend's shape in <see cref="WeatherSystem"/> — the climate's rule.
    ///
    /// <para>Every value is integer: temperatures in centi-degrees, the look in per-mille, the
    /// weights in per-10,000, durations in game hours. Every number the XML ships is invented for
    /// the owner to tune (design 43).</para>
    /// </summary>
    public class WeatherDef : Def
    {
        /// <summary>Which <c>WeatherKind</c> this is, as its integer. The table is indexed by it.</summary>
        public int kind;

        /// <summary>What the kind adds to the outdoor temperature at full intensity, in centi-degrees.</summary>
        public int tempOffsetC;

        /// <summary>How far it dims the day at full intensity. Keeps the colour.</summary>
        public int cloudPerMille;

        /// <summary>How far it drains the day to grey at full intensity. Only the grey days carry it.</summary>
        public int gloomPerMille;

        /// <summary>How hard it rains at full intensity: 1000 for rain and storm, 0 otherwise.</summary>
        public int rainPerMille;

        /// <summary>The wind against ordinary, 1000 being ordinary. Not scaled by intensity.</summary>
        public int windPerMille = 1000;

        /// <summary>
        /// The pace of anybody standing under the open sky at full intensity, per mille of their
        /// own (design 43 §5): 1000 for a dry sky, below it for rain. Scaled by intensity, so a
        /// drizzle costs a fraction of what a downpour does.
        /// </summary>
        public int moveFloorPerMille = 1000;

        /// <summary>
        /// What rain adds to a sky-exposed crop's growth at full intensity, per mille (design 43
        /// §5, the stand-in for a moisture model, 22 §8). Scaled by intensity. 0 for a dry sky.
        /// </summary>
        public int growBonusPerMilleAtFull;

        /// <summary>How often each season rolls it, per 10,000, in Wash · Glare · Rime order.</summary>
        public List<int> seasonWeights = new List<int> { 0, 0, 0 };

        /// <summary>A spell's length, in game hours, rolled between these.</summary>
        public int minHours = 6, maxHours = 24;

        /// <summary>A spell's intensity, in per-mille, rolled between these.</summary>
        public int minIntensity = 1000, maxIntensity = 1000;
    }
}
