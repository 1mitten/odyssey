#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Worldgen.Planet
{
    /// <summary>
    /// A site's climate (design 59 §7): today's temperate curve, re-centred on the site's mean and
    /// scaled by its latitude and rainfall. The arithmetic is <see cref="SiteRules"/>'s, so the
    /// World screen's season line and the colony's thermometer cannot disagree.
    /// </summary>
    public static class SiteClimate
    {
        /// <summary>
        /// A <b>new</b> <see cref="ClimateDef"/> for a site. The base is never written through: it
        /// is the content's shared Def, and a write would retune every colony after this one — the
        /// rule that a test replaces a Def rather than writing through it, applied to the game.
        /// </summary>
        public static ClimateDef For(SiteTile site, ClimateDef baseClimate)
        {
            int seasonality = SiteRules.SeasonalityPerMille(site.LatitudePerMille);
            var offsets = new List<int>(baseClimate.monthlyOffsetC.Count);
            for (int i = 0; i < baseClimate.monthlyOffsetC.Count; i++)
                offsets.Add(SiteRules.ScaledOffsetC(baseClimate.monthlyOffsetC[i], seasonality));

            return new ClimateDef
            {
                defName = baseClimate.defName + "_Site" + site.TileIndex,
                label = baseClimate.label,
                annualMeanC = site.MeanTempC,
                monthlyOffsetC = offsets,
                dailyAmplitudeC = SiteRules.DailyAmplitudeC(baseClimate.dailyAmplitudeC, site.RainfallMm),
                groundOneLayerDampingPerMille = baseClimate.groundOneLayerDampingPerMille,
            };
        }
    }
}
