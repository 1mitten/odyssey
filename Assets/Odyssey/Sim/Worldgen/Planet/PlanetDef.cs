#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Worldgen.Planet
{
    /// <summary>
    /// The planet's shape and climate (design 57 §4c), in <c>Defs/Core/World/Planet.xml</c>. Every
    /// number is a proposal to tune; temperatures are centi-degrees, like the thermal model's.
    /// </summary>
    public class PlanetDef : Def
    {
        /// <summary>Tiles east to west. Even, so the offset rows survive the wrap, and divisible by
        /// half the coarsest elevation period so the noise wraps too.</summary>
        public int width = 64;

        /// <summary>Tiles north to south; the first and last rows are the poles.</summary>
        public int height = 32;

        /// <summary>The share of tiles that are sea, per mille. Cut by rank, so it is exact on every seed.</summary>
        public int oceanPerMille = 450;

        /// <summary>The height of the highest land, and the depth of the deepest sea, in metres.</summary>
        public int peakM = 3000;
        public int seaDepthM = 4000;

        /// <summary>Elevation noise: the coarsest lattice period in half-hexes, and its octaves.</summary>
        public int elevationPeriod = 32;
        public int elevationOctaves = 3;

        /// <summary>Mean temperature at the equator and at the pole, before height and noise.</summary>
        public int equatorC = 2700;
        public int poleC = -2500;

        /// <summary>How much colder per 1,000 m of height: 6.5 °C (a-13 §5).</summary>
        public int lapseCPer1000m = 650;

        /// <summary>Local variation either side of the latitude curve.</summary>
        public int tempNoiseC = 300;

        /// <summary>The noise's share of the rainfall, 0 up to this.</summary>
        public int rainMaxMm = 1800;

        /// <summary>
        /// Latitude belts added to the rainfall (design 57 §4b): a wet equator, dry belts about 30°,
        /// wet about 60°, dry poles. Four values, for |latitude| per mille below 170, 400, 700 and above.
        /// </summary>
        public List<int> rainBeltsMm = new List<int> { 500, -300, 300, -300 };

        /// <summary>Extra rain one hex from the sea, falling to nothing three hexes in.</summary>
        public int coastRainMm = 300;

        /// <summary>
        /// The land's share of each <c>HillBand</c>, per mille, flattest first: Flat, Rolling, Hilly,
        /// Mountainous, Sheer. Cut by rank, so no seed deals a world without flat ground or mountains.
        /// </summary>
        public List<int> hillSharesPerMille = new List<int> { 300, 300, 220, 130, 50 };
    }
}
