#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Worldgen.Planet
{
    /// <summary>
    /// Builds a planet from a seed (design 59 §4b): seven passes over a flat hex grid that wraps east
    /// to west. **Integer arithmetic throughout, each pass on its own stream**, because a planet's
    /// tile decides a colony's board and climate: it must come out the same under Mono and CoreCLR,
    /// and retuning one pass must not move another.
    ///
    /// <para>The planet is never saved. The World screen regenerates it whenever the seed changes,
    /// and a colony keeps only its own tile, whole, in its save header.</para>
    /// </summary>
    public static class PlanetGenerator
    {
        // One stream per pass: `seed ^ purpose`, the pattern NaturalGenContext uses.
        const uint ElevationStream = 0x31E1_E7A7u;
        const uint RidgeStream = 0x7A1D_6E55u;
        const uint TemperatureStream = 0x7E3B_C0DEu;
        const uint RainStream = 0x5A1F_0A11u;
        const uint RuinStream = 0x4B11_7EDu;
        const uint WarpXStream = 0x3A2B_1C0Du;
        const uint WarpZStream = 0x6D5E_4F3Bu;
        const uint OceanStream = 0x0CEA_2A11u;
        const int RetryLimit = 8;

        /// <summary>
        /// A planet from a seed. If a seed deals no tile a colony may land on, the planet is rebuilt on
        /// a derived seed, up to eight times (design 59 §4b's guarantee); the view keeps the seed it
        /// was asked for, because that is the one the player typed.
        /// </summary>
        public static PlanetView Generate(uint seed, PlanetDef def, IReadOnlyList<BiomeDef> biomes,
            ClimateDef? climate = null)
        {
            Validate(def, biomes);
            int[] curve = climate != null ? climate.monthlyOffsetC.ToArray() : Array.Empty<int>();
            PlanetView? view = null;
            for (int retry = 0; retry < RetryLimit; retry++)
            {
                uint streamSeed = retry == 0 ? seed : seed ^ unchecked((uint)retry * 0x9E37_79B9u);
                view = Build(seed, streamSeed, def, biomes, curve);
                if (view.SuggestedTile >= 0) return view;
            }
            throw new InvalidOperationException(
                $"planet seed {seed} dealt no settleable tile in {RetryLimit} tries; the biome table or the planet's climate needs retuning");
        }

        static PlanetView Build(uint seed, uint streamSeed, PlanetDef def, IReadOnlyList<BiomeDef> biomes, int[] curve)
        {
            int w = def.width, h = def.height, n = w * h;

            // ---- 1. elevation, and the sea cut by rank ------------------------------------------
            var elevation = new int[n];
            // Sampled in half-hex units: an odd row sits half a hex east, so x = 2·column + parity.
            int circumference = 2 * w;
            int grain = Math.Max(1, def.featureScale);
            for (int i = 0; i < n; i++)
            {
                int c = HexGrid.Column(i, w), r = HexGrid.Row(i, w);
                int x = 2 * c + (r & 1), z = 2 * r;
                if (def.warpHalfHexes > 0)
                {
                    // Two wrapped fields push the sample point, so the coasts bend off the lattice. The
                    // x push wraps with the planet; z is never wrapped, so it needs nothing.
                    int half = ValueNoise.Scale / 2;
                    int dx = (ValueNoise.Value2DWrapped(streamSeed ^ WarpXStream, x, z, def.warpPeriod, circumference) - half)
                             * def.warpHalfHexes / half;
                    int dz = (ValueNoise.Value2DWrapped(streamSeed ^ WarpZStream, x, z, def.warpPeriod, circumference) - half)
                             * def.warpHalfHexes / half;
                    x += dx;
                    z += dz;
                }
                elevation[i] = WrappedFractal(streamSeed ^ ElevationStream, x, z,
                    def.elevationPeriod, def.elevationOctaves, circumference);
            }

            int oceanCount = n * OceanPerMille(seed, def) / 1000;
            int[] byElevation = RankOrder(elevation);
            var water = new bool[n];
            for (int k = 0; k < oceanCount; k++) water[byElevation[k]] = true;
            int seaLevel = oceanCount < n ? elevation[byElevation[oceanCount]] : ValueNoise.Scale;
            int landTop = elevation[byElevation[n - 1]];

            var elevationM = new int[n];
            for (int i = 0; i < n; i++)
            {
                elevationM[i] = water[i]
                    ? -Scale(seaLevel - elevation[i], Math.Max(1, seaLevel), def.seaDepthM)
                    : Scale(elevation[i] - seaLevel, Math.Max(1, landTop - seaLevel), def.peakM);
            }

            // ---- 2. temperature ------------------------------------------------------------------
            var meanTemp = new int[n];
            for (int i = 0; i < n; i++)
            {
                int c = HexGrid.Column(i, w), r = HexGrid.Row(i, w);
                long lat = HexGrid.LatitudePerMille(r, h);
                int byLatitude = def.equatorC - (int)((def.equatorC - def.poleC) * lat * lat / 1_000_000L);
                int lapse = def.lapseCPer1000m * Math.Max(0, elevationM[i]) / 1000;
                int noise = (WrappedFractal(streamSeed ^ TemperatureStream, 2 * c + (r & 1), 2 * r, 8 * grain, 2, circumference)
                             - ValueNoise.Scale / 2) * def.tempNoiseC / (ValueNoise.Scale / 2);
                meanTemp[i] = byLatitude - lapse + noise;
            }

            // ---- 3. rainfall ---------------------------------------------------------------------
            int[] toSea = DistanceToWater(water, w, h);
            var rain = new int[n];
            for (int i = 0; i < n; i++)
            {
                int c = HexGrid.Column(i, w), r = HexGrid.Row(i, w);
                int noise = WrappedFractal(streamSeed ^ RainStream, 2 * c + (r & 1), 2 * r, 16 * grain, 2, circumference)
                            * def.rainMaxMm / ValueNoise.Scale;
                int belt = Belt(def, Math.Abs(HexGrid.LatitudePerMille(r, h)));
                int reach = 3 * grain;
                int coast = !water[i] && toSea[i] <= reach ? def.coastRainMm * (reach + 1 - toSea[i]) / reach : 0;
                rain[i] = Math.Max(0, noise + belt + coast);
            }

            // ---- 4. hills, cut by rank over the land ---------------------------------------------
            var hills = new byte[n];
            var hilliness = new int[n];
            var land = new List<int>(n - oceanCount);
            for (int i = 0; i < n; i++)
            {
                if (water[i]) continue;
                int c = HexGrid.Column(i, w), r = HexGrid.Row(i, w);
                int height = (elevation[i] - seaLevel) * ValueNoise.Scale / Math.Max(1, landTop - seaLevel);
                int ridgeNoise = WrappedFractal(streamSeed ^ RidgeStream, 2 * c + (r & 1), 2 * r, 8 * grain, 2, circumference);
                int ridge = ValueNoise.Scale - Math.Abs(2 * ridgeNoise - ValueNoise.Scale);
                hilliness[i] = (height * 6 + ridge * 4) / 10;
                land.Add(i);
            }
            land.Sort((a, b) => hilliness[a] != hilliness[b] ? hilliness[a].CompareTo(hilliness[b]) : a.CompareTo(b));
            // The rank each band ends at, flattest first; the last band takes the remainder.
            var bandEnds = new int[def.hillSharesPerMille.Count];
            for (int b = 0, cumulative = 0; b < bandEnds.Length; b++)
            {
                cumulative += def.hillSharesPerMille[b];
                bandEnds[b] = b == bandEnds.Length - 1 ? land.Count : land.Count * cumulative / 1000;
            }
            for (int k = 0, band = 0; k < land.Count; k++)
            {
                while (band < bandEnds.Length - 1 && k >= bandEnds[band]) band++;
                hills[land[k]] = (byte)band;
            }

            // ---- 5. biome ------------------------------------------------------------------------
            int oceanBiome = -1;
            for (int b = 0; b < biomes.Count; b++) if (biomes[b].water) { oceanBiome = b; break; }
            var biome = new byte[n];
            for (int i = 0; i < n; i++) biome[i] = (byte)Classify(biomes, oceanBiome, water[i], meanTemp[i], rain[i]);

            // ---- 6. ruin density, and 7. the coast -----------------------------------------------
            var ruin = new int[n];
            var coastal = new bool[n];
            for (int i = 0; i < n; i++)
            {
                int c = HexGrid.Column(i, w), r = HexGrid.Row(i, w);
                ruin[i] = WrappedFractal(streamSeed ^ RuinStream, 2 * c + (r & 1), 2 * r, 16 * grain, 2, circumference)
                          * 1000 / ValueNoise.Scale;
                if (water[i]) continue;
                for (int d = 0; d < HexGrid.Directions; d++)
                {
                    int next = HexGrid.Neighbour(c, r, d, w, h);
                    if (next >= 0 && water[next]) { coastal[i] = true; break; }
                }
            }

            var views = new BiomeView[biomes.Count];
            for (int b = 0; b < biomes.Count; b++)
            {
                BiomeDef d = biomes[b];
                views[b] = new BiomeView(d.defName, d.labelKey, d.settleable, d.water, Rgb(d, d.rampFrom),
                    Rgb(d, d.rampTo), d.ramp);
            }

            var planet = new PlanetView(seed, w, h, seaLevel, views, biome, elevation, hills, meanTemp, rain,
                elevationM, ruin, water, coastal, -1);
            int suggested = Suggest(planet);
            return new PlanetView(seed, w, h, seaLevel, views, biome, elevation, hills, meanTemp, rain,
                elevationM, ruin, water, coastal, suggested, curve);
        }

        /// <summary>
        /// The sea's share of a world, per mille: the def's share moved by up to its spread either way
        /// (design 59 §4e). Drawn from the seed the player typed, not a retry's, so a retry keeps the
        /// world's character.
        /// </summary>
        public static int OceanPerMille(uint seed, PlanetDef def)
        {
            if (def.oceanSpreadPerMille <= 0) return def.oceanPerMille;
            uint mix = unchecked(seed * 0x9E37_79B9u) ^ OceanStream;
            mix ^= mix >> 15;
            mix = unchecked(mix * 0x2C1B_3C6Du);
            mix ^= mix >> 12;
            return def.oceanPerMille + (int)(mix % (uint)(2 * def.oceanSpreadPerMille + 1)) - def.oceanSpreadPerMille;
        }

        /// <summary>
        /// The biome of one tile (design 59 §6). A sea tile is the sea unless a biome that freezes the
        /// sea holds it (Ice); a land tile is the first biome, lowest priority first, whose bands hold
        /// it. The table is sorted by priority on the way in, so this is the whole rule.
        /// </summary>
        public static int Classify(IReadOnlyList<BiomeDef> biomes, int oceanBiome, bool water, int meanTempC, int rainfallMm)
        {
            for (int b = 0; b < biomes.Count; b++)
            {
                BiomeDef d = biomes[b];
                if (d.water) continue;
                if (water && !d.freezesSea) continue;
                if (d.Holds(meanTempC, rainfallMm)) return b;
            }
            if (water) return oceanBiome;
            throw new InvalidOperationException(
                $"no biome holds a land tile at {meanTempC} centi-degrees and {rainfallMm} mm: the biome table is not total");
        }

        /// <summary>The settleable tile nearest the reference climate, preferring Rolling; ties to the lower index.</summary>
        static int Suggest(PlanetView planet)
        {
            int best = -1;
            long bestScore = long.MaxValue;
            for (int i = 0; i < planet.TileCount; i++)
            {
                if (planet.Verdict(i) != SettleVerdict.Settleable) continue;
                long score = Math.Abs(planet.MeanTempC[i] - SiteRules.ReferenceMeanTempC)
                             + Math.Abs(planet.RainfallMm[i] - SiteRules.ReferenceRainfallMm) / 2
                             + (planet.HillsAt(i) == HillBand.Rolling ? 0 : 300);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Fractal value noise that wraps: the lattice is taken round the planet, so the east edge
        /// meets the west with no seam and no trigonometry. Each octave's period must divide the
        /// circumference, which <see cref="Validate"/> checks.
        /// </summary>
        public static int WrappedFractal(uint seed, int x, int z, int period, int octaves, int circumference)
        {
            int sum = 0, weight = 1024, total = 0, p = period;
            for (int o = 0; o < octaves && weight > 0; o++)
            {
                sum += ValueNoise.Value2DWrapped(seed + (uint)o * 7919u, x, z, p, circumference) * weight;
                total += weight;
                weight >>= 1;
                p = p > 1 ? p >> 1 : 1;
            }
            return total == 0 ? 0 : sum / total;
        }

        /// <summary>Every tile's hex distance to the nearest sea tile, by breadth-first search; 0 on the sea.</summary>
        static int[] DistanceToWater(bool[] water, int w, int h)
        {
            int n = w * h;
            var distance = new int[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                distance[i] = water[i] ? 0 : int.MaxValue;
                if (water[i]) queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int c = HexGrid.Column(i, w), r = HexGrid.Row(i, w);
                for (int d = 0; d < HexGrid.Directions; d++)
                {
                    int next = HexGrid.Neighbour(c, r, d, w, h);
                    if (next < 0 || distance[next] <= distance[i] + 1) continue;
                    distance[next] = distance[i] + 1;
                    queue.Enqueue(next);
                }
            }
            return distance;
        }

        static int Belt(PlanetDef def, int absLatitude)
        {
            int belt = absLatitude < 170 ? 0 : absLatitude < 400 ? 1 : absLatitude < 700 ? 2 : 3;
            return belt < def.rainBeltsMm.Count ? def.rainBeltsMm[belt] : 0;
        }

        /// <summary>Tile indices sorted by value, ties to the lower index, so a rank cut is exact and repeatable.</summary>
        static int[] RankOrder(int[] values)
        {
            var order = new int[values.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => values[a] != values[b] ? values[a].CompareTo(values[b]) : a.CompareTo(b));
            return order;
        }

        static int Scale(int value, int range, int to) => (int)((long)value * to / range);

        static int Rgb(BiomeDef def, string hex)
        {
            string s = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                throw new InvalidOperationException($"{def.defName}: '{hex}' is not a #rrggbb colour");
            return rgb;
        }

        static void Validate(PlanetDef def, IReadOnlyList<BiomeDef> biomes)
        {
            if (def.width < 2 || (def.width & 1) != 0)
                throw new InvalidOperationException($"{def.defName}: the width must be even, or the offset rows break at the wrap");
            if (def.height < 4) throw new InvalidOperationException($"{def.defName}: a planet needs at least four rows");
            for (int p = def.elevationPeriod, o = 0; o < def.elevationOctaves; o++, p = Math.Max(1, p >> 1))
                if ((2 * def.width) % p != 0)
                    throw new InvalidOperationException($"{def.defName}: elevation period {p} does not divide the circumference {2 * def.width}, so the noise would not wrap");
            int grain = Math.Max(1, def.featureScale);
            if ((2 * def.width) % (16 * grain) != 0)
                throw new InvalidOperationException($"{def.defName}: the rain and ruin noise need a circumference divisible by {16 * grain}");
            if (def.warpHalfHexes > 0 && (def.warpPeriod < 1 || (2 * def.width) % def.warpPeriod != 0))
                throw new InvalidOperationException($"{def.defName}: warp period {def.warpPeriod} does not divide the circumference {2 * def.width}");
            if (def.oceanSpreadPerMille < 0 || def.oceanPerMille - def.oceanSpreadPerMille < 0
                || def.oceanPerMille + def.oceanSpreadPerMille >= 1000)
                throw new InvalidOperationException($"{def.defName}: the sea's share and its spread must stay inside 0 to 999 per mille");
            int water = 0;
            for (int b = 0; b < biomes.Count; b++)
            {
                if (biomes[b].water) water++;
                if (b > 0 && biomes[b].priority < biomes[b - 1].priority)
                    throw new InvalidOperationException("the biome table must be sorted by priority");
            }
            if (water != 1) throw new InvalidOperationException($"the biome table needs exactly one water biome, and has {water}");
            if (biomes.Count > byte.MaxValue) throw new InvalidOperationException("too many biomes for a byte");
            int shares = 0;
            foreach (int s in def.hillSharesPerMille) shares += s;
            if (def.hillSharesPerMille.Count != 5 || shares != 1000)
                throw new InvalidOperationException($"{def.defName}: five hill shares summing to 1000 per mille, one per HillBand");
        }
    }
}
