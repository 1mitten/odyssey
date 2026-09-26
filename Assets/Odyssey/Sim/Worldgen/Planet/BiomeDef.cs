#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Worldgen.Planet
{
    /// <summary>
    /// One biome of the planet (design 57 §6): where on the temperature and rainfall plane it falls,
    /// whether a colony may land on it, and how the World screen colours it. The first map-generation
    /// content authored in XML (<c>Defs/Core/World/Biomes.xml</c>).
    /// </summary>
    public class BiomeDef : Def
    {
        /// <summary>The registry key its name is drawn from (<c>ui.biome.*</c>).</summary>
        public string labelKey = string.Empty;

        /// <summary>Checked lowest first; the first biome whose bands hold a tile takes it.</summary>
        public int priority;

        /// <summary>Whether a colony may land here. Only Meadow, until the next art pack (owner, 2026-09-26).</summary>
        public bool settleable;

        /// <summary>Open sea: the biome of every tile below the sea level that nothing freezes.</summary>
        public bool water;

        /// <summary>Also claims sea tiles its bands hold — Ice, so a polar sea freezes and stays water.</summary>
        public bool freezesSea;

        /// <summary>Half-open bands on the temperature and rainfall plane; a tile in any one belongs here.</summary>
        public List<BiomeBand> bands = new List<BiomeBand>();

        /// <summary>The map ramp's colour at t = 0 and t = 1, as <c>#rrggbb</c> (design 57 §9b).</summary>
        public string rampFrom = "#000000";
        public string rampTo = "#000000";

        /// <summary>Which rule sets the ramp's t.</summary>
        public MapRamp ramp = MapRamp.Land;

        /// <summary>The board preset a site here is built with. Only <c>Meadow</c> exists: <c>MakeWooded</c>.</summary>
        public string board = string.Empty;

        /// <summary>Whether a tile with this mean temperature and rainfall falls in any of the bands.</summary>
        public bool Holds(int meanTempC, int rainfallMm)
        {
            for (int i = 0; i < bands.Count; i++)
                if (bands[i].Holds(meanTempC, rainfallMm)) return true;
            return false;
        }
    }

    /// <summary>A half-open rectangle on the temperature (centi-degrees) and rainfall (mm) plane.</summary>
    public class BiomeBand
    {
        public int tempMinC = -1000000;
        public int tempMaxC = 1000000;
        public int rainMinMm = -1000000;
        public int rainMaxMm = 1000000;

        public bool Holds(int meanTempC, int rainfallMm) =>
            meanTempC >= tempMinC && meanTempC < tempMaxC && rainfallMm >= rainMinMm && rainfallMm < rainMaxMm;
    }
}
