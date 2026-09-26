#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// A map's climate: the outdoor temperature curve and the ground's answer to it
    /// (design 28 §5). One Def per map type, authored in <c>Defs/Core/World/Climate.xml</c>.
    ///
    /// <para><b>Temperatures are centi-degrees</b> — 900 is 9 °C — because everything in the
    /// thermal model is an integer and one unit of 0.01 °C is the resolution the pass moves a
    /// room by. The monthly offsets are quoted against the annual mean, so a season is what it
    /// feels like rather than an absolute.</para>
    ///
    /// <para><b>The curve's shape is code, not content</b>: annual mean plus one offset per
    /// month plus a fixed daily term. What a climate <i>is</i> — how warm on average, how far
    /// the seasons swing, how much the day itself swings — is content; the shape those numbers
    /// ride is the same everywhere and lives in <c>TemperatureSystem</c>.</para>
    /// </summary>
    public class ClimateDef : Def
    {
        /// <summary>The year's anchor, in centi-degrees. The ground converges to this at depth,
        /// which is what makes a deep mine the same temperature in Rime as in Glare.</summary>
        public int annualMeanC = 900;

        /// <summary>
        /// One offset per month in calendar order (Larkspur, Tansy, Bramble, Ember, Hollow,
        /// Candle), in centi-degrees. Six entries; the calendar owns the month count and the
        /// two are checked against each other at load.
        /// </summary>
        public List<int> monthlyOffsetC = new List<int> { 600, 1000, 1300, 1800, -700, -1700 };

        /// <summary>
        /// The day's own swing, in centi-degrees: the daily term runs from minus this to plus
        /// it, peaking at 14h and bottoming at 02h. ±6 °C on the shipped temperate table.
        /// </summary>
        public int dailyAmplitudeC = 500;

        /// <summary>
        /// How much of the surface's seasonal swing survives one layer down, in per-mille — 717
        /// is e^(−1/3), a damping depth of three of our 3 m layers (design 28 §5). Expressed as
        /// the per-layer factor rather than the depth so the whole curve stays integer: the
        /// factor compounds per layer by multiplication and nothing ever takes a logarithm.
        /// </summary>
        public int groundOneLayerDampingPerMille = 717;
    }
}
