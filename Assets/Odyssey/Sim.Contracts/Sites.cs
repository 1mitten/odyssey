#nullable enable
using System;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// How hilly a site on the planet is (design 59 §5). The order is the rank the planet cuts its
    /// land into, flattest first, and it is saved: append, never reorder.
    ///
    /// <para><b>The names are ours.</b> Claude Design's specification used the reference's own five
    /// labels, and the owner ruled for these on 2026-09-26 (design 59 §9a).</para>
    /// </summary>
    public enum HillBand
    {
        /// <summary>Level ground: relief 1, half the outcrops.</summary>
        Flat = 0,

        /// <summary>Today's played board, exactly: relief 2, the def's own outcrops and caverns.</summary>
        Rolling = 1,

        /// <summary>Relief 3, half as many outcrops again.</summary>
        Hilly = 2,

        /// <summary>Relief 4 and deep rock: the board goes to 24 layers so rock survives under the valleys.</summary>
        Mountainous = 3,

        /// <summary>Too steep to settle. Shown on the map, never taken.</summary>
        Sheer = 4,
    }

    /// <summary>Whether a colony may land on a tile, and if not, why (design 59 §9).</summary>
    public enum SettleVerdict
    {
        Settleable = 0,

        /// <summary>A biome with no art yet: only Meadow can be settled until the next pack.</summary>
        NotYetAvailable = 1,

        /// <summary>Ocean, or polar sea frozen to Ice.</summary>
        OpenWater = 2,

        /// <summary><see cref="HillBand.Sheer"/>.</summary>
        TooSteep = 3,
    }

    /// <summary>
    /// One tile of the planet, as a colony remembers it (design 59 §8). This is what the save header
    /// carries whole, so a later retune of the planet generator cannot change a saved colony's
    /// climate or hills: the board is rebuilt from what it was built from, never regenerated.
    ///
    /// <para>Integers throughout, like everything that reaches a board. <see cref="BiomeDefName"/>
    /// is the Def's name rather than an index so reordering the biome table cannot change a save.</para>
    /// </summary>
    public readonly struct SiteTile : IEquatable<SiteTile>
    {
        public SiteTile(int tileIndex, int column, int row, string biomeDefName, HillBand hills,
            int latitudePerMille, int meanTempC, int rainfallMm, int elevationM, int ruinPerMille, bool coastal)
        {
            TileIndex = tileIndex;
            Column = column;
            Row = row;
            BiomeDefName = biomeDefName ?? string.Empty;
            Hills = hills;
            LatitudePerMille = latitudePerMille;
            MeanTempC = meanTempC;
            RainfallMm = rainfallMm;
            ElevationM = elevationM;
            RuinPerMille = ruinPerMille;
            Coastal = coastal;
        }

        /// <summary><c>row * width + column</c>: the tile's identity, and half the board's seed.</summary>
        public readonly int TileIndex;
        public readonly int Column;
        public readonly int Row;

        public readonly string BiomeDefName;
        public readonly HillBand Hills;

        /// <summary>Signed per-mille of a quarter-turn: +1000 is the north pole, −1000 the south.</summary>
        public readonly int LatitudePerMille;

        /// <summary>The year's mean outdoor temperature, centi-degrees (900 is 9 °C).</summary>
        public readonly int MeanTempC;

        public readonly int RainfallMm;

        /// <summary>Metres above the sea; negative for a sea tile's depth.</summary>
        public readonly int ElevationM;

        /// <summary>The second axis (design 59 §2): recorded, read by nothing until the city board.</summary>
        public readonly int RuinPerMille;

        /// <summary>Whether any neighbour is sea. Recorded for the coast seam; read by nothing.</summary>
        public readonly bool Coastal;

        public bool Equals(SiteTile other) =>
            TileIndex == other.TileIndex && Column == other.Column && Row == other.Row
            && string.Equals(BiomeDefName, other.BiomeDefName, StringComparison.Ordinal)
            && Hills == other.Hills && LatitudePerMille == other.LatitudePerMille
            && MeanTempC == other.MeanTempC && RainfallMm == other.RainfallMm
            && ElevationM == other.ElevationM && RuinPerMille == other.RuinPerMille
            && Coastal == other.Coastal;

        public override bool Equals(object? obj) => obj is SiteTile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = TileIndex;
                h = h * 31 + (int)Hills;
                h = h * 31 + MeanTempC;
                h = h * 31 + RainfallMm;
                return h;
            }
        }

        public override string ToString() =>
            $"tile {TileIndex} ({Column}, {Row}) {BiomeDefName} {Hills} lat {LatitudePerMille} {MeanTempC}c {RainfallMm}mm";
    }

    /// <summary>
    /// The rules a site imposes that both the simulation and the World screen need (design 59 §5,
    /// §7, §8). They live here, beside the site, because the interface cannot call the simulation
    /// and a rule written twice is a rule that drifts (bug-patterns P1).
    /// </summary>
    public static class SiteRules
    {
        /// <summary>
        /// The latitude at which a site's seasons swing exactly as today's temperate curve does:
        /// 590 per mille, 53°. Together with 900 centi-degrees and 1,000 mm it reproduces
        /// <c>Climate_Temperate</c> field for field (design 59 §7).
        /// </summary>
        public const int ReferenceLatitudePerMille = 590;

        /// <summary>The mean temperature, in centi-degrees, of the reference site.</summary>
        public const int ReferenceMeanTempC = 900;

        /// <summary>The rainfall of the reference site, in millimetres a year.</summary>
        public const int ReferenceRainfallMm = 1000;

        /// <summary>
        /// How deep a mountainous board is at least (owner, 2026-09-26; design 38 §13's
        /// measurement). 24 until every offered board went to 32 (design 62, DM2): a mountainous
        /// site is never shallower than the rest, so it is 32 too, and <see cref="BoardLayers"/>
        /// deepens nothing a player can choose today. Kept as its own number because how deep a
        /// mountain goes is a separate decision from how deep a board is.
        /// </summary>
        public const int MountainLayers = 32;

        /// <summary>
        /// How hard the seasons bite at a latitude, per mille of the reference's swing: ×0.15 at the
        /// equator, ×1.0 at 53°, capped at ×1.6. Linear in latitude, which is the simplest curve
        /// that is near-seasonless at the equator and passes through the reference exactly.
        /// </summary>
        public static int SeasonalityPerMille(int latitudePerMille)
        {
            int lat = Math.Abs(latitudePerMille);
            return Clamp(150 + lat * 850 / ReferenceLatitudePerMille, 150, 1600);
        }

        /// <summary>A month's offset from the annual mean at a site: the base curve's, scaled by its seasonality.</summary>
        public static int ScaledOffsetC(int baseOffsetC, int seasonalityPerMille) =>
            baseOffsetC * seasonalityPerMille / 1000;

        /// <summary>One month's mean at a site: its annual mean plus the scaled offset.</summary>
        public static int MonthMeanC(int meanTempC, int baseOffsetC, int seasonalityPerMille) =>
            meanTempC + ScaledOffsetC(baseOffsetC, seasonalityPerMille);

        /// <summary>
        /// The day's swing at a site: dry air swings more between noon and night. The reference's
        /// 1,000 mm keeps the base amplitude exactly.
        /// </summary>
        public static int DailyAmplitudeC(int baseAmplitudeC, int rainfallMm) =>
            Clamp(baseAmplitudeC * (2000 - rainfallMm) / 1000, 300, 900);

        /// <summary>
        /// How much more (or less) often it rains at a site, per mille of today's weights: the
        /// rainfall itself over the reference's 1,000 mm, held to ×0.4–×1.6.
        /// </summary>
        public static int WetPerMille(int rainfallMm) => Clamp(rainfallMm, 400, 1600);

        /// <summary>
        /// The board's depth for a site: a mountainous site is at least <see cref="MountainLayers"/>
        /// deep, and every other band keeps the size the player chose. Decided once, in
        /// <c>OdysseyBootstrap.BuildSession</c>, before anything is built from the size.
        /// </summary>
        public static int BoardLayers(int chosenLayers, HillBand hills) =>
            hills == HillBand.Mountainous ? Math.Max(chosenLayers, MountainLayers) : chosenLayers;

        /// <summary>
        /// The board's seed for a tile of a world (design 59 §8): the same world and the same tile
        /// always give the same colony, and neighbouring tiles give unrelated boards.
        /// </summary>
        public static uint BoardSeed(uint worldSeed, int tileIndex)
        {
            // Never 0, as §8 promises: one draw in 2^32 would otherwise deal it.
            uint seed = DeterministicRandom.ForTick(worldSeed, tileIndex, BoardSeedPurpose).NextUInt();
            return seed == 0 ? 1u : seed;
        }

        /// <summary>Latitude as whole degrees from the equator, for display.</summary>
        public static int LatitudeDegrees(int latitudePerMille) =>
            (Math.Abs(latitudePerMille) * 90 + 500) / 1000;

        const uint BoardSeedPurpose = 0x5173_5EEDu;

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
