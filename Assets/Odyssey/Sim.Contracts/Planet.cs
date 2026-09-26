#nullable enable
using System;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// The planet's grid (design 59 §4a): hexes in offset rows, pointy-top, odd rows shifted half a
    /// hex east, wrapping east to west. Pure index arithmetic, shared by the generator that fills
    /// the planet and the screen that draws and picks it, so the two cannot disagree about who a
    /// tile's neighbours are.
    /// </summary>
    public static class HexGrid
    {
        /// <summary>Six directions, east first, going round anticlockwise on screen (north up).</summary>
        public const int Directions = 6;

        // (column step, row step) for an even row, then an odd one. Row −1 is north.
        static readonly int[] EvenColumn = { +1, 0, -1, -1, -1, 0 };
        static readonly int[] OddColumn = { +1, +1, 0, -1, 0, +1 };
        static readonly int[] RowStep = { 0, -1, -1, 0, +1, +1 };

        public static int Index(int column, int row, int width) => row * width + column;

        public static int Column(int index, int width) => index % width;

        public static int Row(int index, int width) => index / width;

        /// <summary>Column taken round the planet: −1 is the last column, width is the first.</summary>
        public static int Wrap(int column, int width)
        {
            int c = column % width;
            return c < 0 ? c + width : c;
        }

        /// <summary>
        /// The neighbour of a tile in one of the six directions, or −1 off the north or south edge.
        /// East and west wrap.
        /// </summary>
        public static int Neighbour(int column, int row, int direction, int width, int height)
        {
            int r = row + RowStep[direction];
            if (r < 0 || r >= height) return -1;
            int c = column + ((row & 1) == 0 ? EvenColumn[direction] : OddColumn[direction]);
            return Index(Wrap(c, width), r, width);
        }

        /// <summary>
        /// Latitude of a row in signed per-mille of a quarter-turn (design 59 §4a): row 0 is the far
        /// north, the last row the far south, the equator between the middle two.
        /// </summary>
        public static int LatitudePerMille(int row, int height) => 1000 - (2 * row + 1) * 1000 / height;
    }

    /// <summary>Which rule sets a biome's map ramp position (design 59 §9b).</summary>
    public enum MapRamp
    {
        /// <summary>Depth below the sea: deep is dark, the coast light.</summary>
        Ocean = 0,

        /// <summary>Height above the sea: lowland at one end, upland at the other.</summary>
        Land = 1,

        /// <summary>Land's rule on land; on frozen sea, a pale band by depth.</summary>
        Ice = 2,
    }

    /// <summary>What the World screen needs to know about one biome.</summary>
    public readonly struct BiomeView
    {
        public BiomeView(string defName, string labelKey, bool settleable, bool water, int rampFromRgb,
            int rampToRgb, MapRamp ramp)
        {
            DefName = defName;
            LabelKey = labelKey;
            Settleable = settleable;
            Water = water;
            RampFromRgb = rampFromRgb;
            RampToRgb = rampToRgb;
            Ramp = ramp;
        }

        public readonly string DefName;

        /// <summary>The registry key the name is drawn from (<c>ui.biome.*</c>).</summary>
        public readonly string LabelKey;

        public readonly bool Settleable;

        /// <summary>Open sea: never settled, whatever the flag above says.</summary>
        public readonly bool Water;

        /// <summary>The map ramp's colour at t = 0 and t = 1, as 0xRRGGBB.</summary>
        public readonly int RampFromRgb;
        public readonly int RampToRgb;

        public readonly MapRamp Ramp;
    }

    /// <summary>
    /// A generated planet as the World screen reads it (design 59 §4): per-tile arrays and the biome
    /// table. Built by the simulation's generator and handed across, because the interface assembly
    /// cannot call the simulation — the same shape as a colonist candidate. Never saved: it is
    /// regenerated from the world seed whenever it is wanted, and a colony keeps only its own tile.
    /// </summary>
    public sealed class PlanetView
    {
        public PlanetView(uint worldSeed, int width, int height, int seaLevel, BiomeView[] biomes,
            byte[] biome, int[] elevation, byte[] hills, int[] meanTempC, int[] rainfallMm, int[] elevationM,
            int[] ruinPerMille, bool[] water, bool[] coastal, int suggestedTile, int[]? seasonCurveC = null)
        {
            if (width < 2 || (width & 1) != 0) throw new ArgumentException("the planet's width must be even", nameof(width));
            int n = width * height;
            if (biome.Length != n || elevation.Length != n || hills.Length != n || meanTempC.Length != n
                || rainfallMm.Length != n || elevationM.Length != n || ruinPerMille.Length != n
                || water.Length != n || coastal.Length != n)
                throw new ArgumentException("every per-tile array must hold width x height tiles");
            WorldSeed = worldSeed;
            Width = width;
            Height = height;
            SeaLevel = seaLevel;
            Biomes = biomes;
            Biome = biome;
            Elevation = elevation;
            Hills = hills;
            MeanTempC = meanTempC;
            RainfallMm = rainfallMm;
            ElevationM = elevationM;
            RuinPerMille = ruinPerMille;
            Water = water;
            Coastal = coastal;
            SuggestedTile = suggestedTile;
            SeasonCurveC = seasonCurveC ?? Array.Empty<int>();
        }

        public uint WorldSeed { get; }
        public int Width { get; }
        public int Height { get; }
        public int TileCount => Width * Height;

        /// <summary>The elevation noise at the sea's edge, on the same 0–1023 scale as <see cref="Elevation"/>.</summary>
        public int SeaLevel { get; }

        public BiomeView[] Biomes { get; }

        /// <summary>Per tile: an index into <see cref="Biomes"/>.</summary>
        public byte[] Biome { get; }

        /// <summary>Per tile: the elevation noise, 0–1023. The map ramp reads it; the board never does.</summary>
        public int[] Elevation { get; }

        /// <summary>Per tile: a <see cref="HillBand"/>. Flat on the sea.</summary>
        public byte[] Hills { get; }

        public int[] MeanTempC { get; }
        public int[] RainfallMm { get; }
        public int[] ElevationM { get; }
        public int[] RuinPerMille { get; }
        public bool[] Water { get; }
        public bool[] Coastal { get; }

        /// <summary>The settleable tile nearest the reference climate, preferring Rolling (design 59 §9).</summary>
        public int SuggestedTile { get; }

        /// <summary>
        /// The content's monthly offsets from the annual mean (centi-degrees), which a site scales by
        /// its latitude (<see cref="SiteRules.MonthMeanC"/>). Carried so the World screen can say a
        /// site's seasons with the colony's own arithmetic; empty when nobody supplied it.
        /// </summary>
        public int[] SeasonCurveC { get; }

        /// <summary>The coldest and the warmest month's mean at a tile, by the colony's own rule.</summary>
        public void SeasonRange(int tile, out int coldestC, out int warmestC)
        {
            coldestC = warmestC = MeanTempC[tile];
            int seasonality = SiteRules.SeasonalityPerMille(LatitudePerMille(tile));
            for (int m = 0; m < SeasonCurveC.Length; m++)
            {
                int month = SiteRules.MonthMeanC(MeanTempC[tile], SeasonCurveC[m], seasonality);
                if (month < coldestC) coldestC = month;
                if (month > warmestC) warmestC = month;
            }
        }

        public BiomeView BiomeAt(int tile) => Biomes[Biome[tile]];

        public HillBand HillsAt(int tile) => (HillBand)Hills[tile];

        public int LatitudePerMille(int tile) => HexGrid.LatitudePerMille(HexGrid.Row(tile, Width), Height);

        /// <summary>Whether a colony may land on a tile, and if not, why. Water first, then steepness, then art.</summary>
        public SettleVerdict Verdict(int tile)
        {
            if (Water[tile] || BiomeAt(tile).Water) return SettleVerdict.OpenWater;
            if (HillsAt(tile) == HillBand.Sheer) return SettleVerdict.TooSteep;
            return BiomeAt(tile).Settleable ? SettleVerdict.Settleable : SettleVerdict.NotYetAvailable;
        }

        /// <summary>The tile as a colony would remember it (design 59 §8).</summary>
        public SiteTile Tile(int tile) => new SiteTile(tile, HexGrid.Column(tile, Width), HexGrid.Row(tile, Width),
            BiomeAt(tile).DefName, HillsAt(tile), LatitudePerMille(tile), MeanTempC[tile], RainfallMm[tile],
            ElevationM[tile], RuinPerMille[tile], Coastal[tile]);
    }
}
