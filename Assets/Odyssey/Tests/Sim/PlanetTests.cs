#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Planet;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The planet generator (design 57 §4, §6): deterministic, seamless east to west, exact in its
    /// shares of sea and hills, total in its biome table, and never without a tile a colony can land on.
    /// </summary>
    public class PlanetTests
    {
        static PlanetView Generate(uint seed) => PlanetGenerator.Generate(seed, WorldContent.Planet, WorldContent.Biomes, WorldContent.Climate);

        static ulong Hash(PlanetView p)
        {
            ulong h = 1469598103934665603UL;
            void Mix(long v) { unchecked { h = (h ^ (ulong)v) * 1099511628211UL; } }
            for (int i = 0; i < p.TileCount; i++)
            {
                Mix(p.Biome[i]); Mix(p.Elevation[i]); Mix(p.Hills[i]); Mix(p.MeanTempC[i]);
                Mix(p.RainfallMm[i]); Mix(p.RuinPerMille[i]); Mix(p.Water[i] ? 1 : 0); Mix(p.Coastal[i] ? 1 : 0);
            }
            Mix(p.SuggestedTile);
            return h;
        }

        /// <summary>
        /// The biome table and the planet as they stand, pinned the way the terrain is
        /// (<c>WorldContentDefTests</c>): a deliberate change to <c>Biomes.xml</c> or <c>Planet.xml</c>
        /// is one line here, and an accidental one fails.
        /// </summary>
        const ulong PlanetFingerprint = 11131572715734754125UL;

        [Test]
        public void ThePlanetsContentIsStillWhatItWas()
        {
            ulong actual = DefComparison.Fingerprint(WorldContent.Biomes, "Biomes")
                           ^ (DefComparison.Fingerprint(WorldContent.Planet, "Planet") * 31UL);
            Assert.That(actual, Is.EqualTo(PlanetFingerprint),
                $"the planet's content has moved. If that was deliberate, set PlanetFingerprint to {actual}UL " +
                "and say what changed; if not, `git diff Assets/Odyssey/Defs/Core/World` is what moved.");
        }

        /// <summary>The control: one band's edge moved by a centi-degree must move the fingerprint.</summary>
        [Test]
        public void ThePlanetFingerprintNoticesOneBand()
        {
            BiomeDef[] fresh = WorldContent.BiomesFromDefs(Odyssey.Sim.Defs.ContentPack.LoadCore(RepoPaths.CoreDefs));
            ulong before = DefComparison.Fingerprint(fresh, "Biomes");
            fresh[0].bands[0].tempMaxC += 1;
            Assert.That(DefComparison.Fingerprint(fresh, "Biomes"), Is.Not.EqualTo(before));
        }

        [Test]
        public void TheSameSeedIsTheSamePlanet()
        {
            Assert.That(Hash(Generate(7u)), Is.EqualTo(Hash(Generate(7u))));
        }

        /// <summary>The control: a hash that cannot tell two seeds apart proves nothing above.</summary>
        [Test]
        public void AnotherSeedIsAnotherPlanet()
        {
            Assert.That(Hash(Generate(7u)), Is.Not.EqualTo(Hash(Generate(8u))));
        }

        [Test]
        public void EveryNeighbourIsNeighbourlyBack()
        {
            const int w = 64, h = 32;
            for (int i = 0; i < w * h; i++)
            {
                int c = HexGrid.Column(i, w), r = HexGrid.Row(i, w);
                for (int d = 0; d < HexGrid.Directions; d++)
                {
                    int next = HexGrid.Neighbour(c, r, d, w, h);
                    if (next < 0) { Assert.That(r == 0 || r == h - 1, $"only the poles have no neighbour ({c},{r})"); continue; }
                    int back = HexGrid.Neighbour(HexGrid.Column(next, w), HexGrid.Row(next, w), (d + 3) % 6, w, h);
                    Assert.That(back, Is.EqualTo(i), $"({c},{r}) direction {d}");
                }
            }
        }

        [Test]
        public void ThePlanetWrapsEastToWest()
        {
            Assert.That(HexGrid.Neighbour(63, 10, 0, 64, 32), Is.EqualTo(HexGrid.Index(0, 10, 64)), "east of the last column is the first");
            Assert.That(HexGrid.Neighbour(0, 10, 3, 64, 32), Is.EqualTo(HexGrid.Index(63, 10, 64)), "west of the first is the last");
            for (int z = 0; z < 64; z += 3)
                Assert.That(PlanetGenerator.WrappedFractal(11u, 127, z, 32, 3, 128) - PlanetGenerator.WrappedFractal(11u, -1, z, 32, 3, 128),
                    Is.Zero, "the noise is one field round the planet");
        }

        /// <summary>
        /// The wrap is not free: the control is the plain noise, whose last column (x = 127) and first
        /// (x = 0) are unrelated — the seam the map would show where it joins. Wrapped, the step across
        /// the join is no bigger than any other step between neighbours.
        /// </summary>
        [Test]
        public void TheUnwrappedNoiseWouldHaveASeam()
        {
            int seam = 0, joined = 0, ordinary = 0;
            for (int z = 0; z < 64; z++)
            {
                seam += Math.Abs(ValueNoise.Fractal2D(11u, 127, z, 32, 3) - ValueNoise.Fractal2D(11u, 0, z, 32, 3));
                joined += Math.Abs(PlanetGenerator.WrappedFractal(11u, 127, z, 32, 3, 128) - PlanetGenerator.WrappedFractal(11u, 0, z, 32, 3, 128));
                ordinary += Math.Abs(PlanetGenerator.WrappedFractal(11u, 63, z, 32, 3, 128) - PlanetGenerator.WrappedFractal(11u, 64, z, 32, 3, 128));
            }
            Assert.That(seam, Is.GreaterThan(joined * 3), $"plain noise across the join {seam}, wrapped {joined}");
            Assert.That(joined, Is.LessThan(ordinary * 3 + 64 * 8), $"the join {joined} against an ordinary step {ordinary}");
        }

        [Test]
        public void TheSeaIsExactlyItsShare()
        {
            foreach (uint seed in new[] { 1u, 2u, 3u, 99u })
            {
                PlanetView p = Generate(seed);
                int sea = 0;
                for (int i = 0; i < p.TileCount; i++) if (p.Water[i]) sea++;
                Assert.That(sea, Is.EqualTo(p.TileCount * WorldContent.Planet.oceanPerMille / 1000), $"seed {seed}");
            }
        }

        [Test]
        public void TheHillsAreExactlyTheirShares()
        {
            PlanetView p = Generate(5u);
            var counts = new int[5];
            int land = 0;
            for (int i = 0; i < p.TileCount; i++)
            {
                if (p.Water[i]) { Assert.That(p.HillsAt(i), Is.EqualTo(HillBand.Flat), "the sea is flat"); continue; }
                counts[p.Hills[i]]++;
                land++;
            }
            List<int> shares = WorldContent.Planet.hillSharesPerMille;
            int cumulative = 0, previousEnd = 0;
            for (int b = 0; b < 5; b++)
            {
                cumulative += shares[b];
                int end = b == 4 ? land : land * cumulative / 1000;
                Assert.That(counts[b], Is.EqualTo(end - previousEnd), $"{(HillBand)b}");
                previousEnd = end;
            }
        }

        [Test]
        public void ItIsColderTowardsThePoles()
        {
            PlanetView p = Generate(3u);
            long Row(int r) { long sum = 0; for (int c = 0; c < p.Width; c++) sum += p.MeanTempC[HexGrid.Index(c, r, p.Width)]; return sum / p.Width; }
            long equator = (Row(15) + Row(16)) / 2, temperate = (Row(5) + Row(26)) / 2, polar = (Row(0) + Row(31)) / 2;
            Assert.That(equator, Is.GreaterThan(temperate));
            Assert.That(temperate, Is.GreaterThan(polar));
        }

        /// <summary>Every point of the plane a land tile could reach lands in a biome: the table is total.</summary>
        [Test]
        public void TheBiomeTableIsTotal()
        {
            IReadOnlyList<BiomeDef> biomes = WorldContent.Biomes;
            int ocean = -1;
            for (int b = 0; b < biomes.Count; b++) if (biomes[b].water) ocean = b;
            for (int t = -6000; t <= 6000; t += 50)
                for (int rain = 0; rain <= 5000; rain += 50)
                {
                    int land = PlanetGenerator.Classify(biomes, ocean, water: false, t, rain);
                    Assert.That(biomes[land].water, Is.False, "land is never the sea");
                    int sea = PlanetGenerator.Classify(biomes, ocean, water: true, t, rain);
                    Assert.That(sea == ocean || biomes[sea].freezesSea, Is.True, "the sea is the sea, or frozen");
                }
        }

        [Test]
        public void TheSixBiomesAreTheOwnersSix()
        {
            var names = new List<string>();
            foreach (BiomeDef b in WorldContent.Biomes) names.Add(b.labelKey);
            Assert.That(names, Is.EquivalentTo(new[]
            {
                "ui.biome.ocean", "ui.biome.meadow", "ui.biome.coldsteppe", "ui.biome.dryscrub", "ui.biome.marsh", "ui.biome.ice",
            }));
            foreach (BiomeDef b in WorldContent.Biomes)
                Assert.That(b.settleable, Is.EqualTo(b.labelKey == "ui.biome.meadow"), $"{b.defName}: only Meadow, until the next art pack");
        }

        [Test]
        public void EveryBiomeTurnsUpAcrossTwoHundredWorlds()
        {
            var seen = new HashSet<string>();
            bool frozenSea = false;
            for (uint seed = 1; seed <= 200; seed++)
            {
                PlanetView p = Generate(seed);
                for (int i = 0; i < p.TileCount; i++)
                {
                    seen.Add(p.BiomeAt(i).LabelKey);
                    if (p.Water[i] && !p.BiomeAt(i).Water) frozenSea = true;
                }
            }
            Assert.That(seen.Count, Is.EqualTo(WorldContent.Biomes.Length), string.Join(", ", seen));
            Assert.That(frozenSea, Is.True, "a polar sea freezes to Ice somewhere");
        }

        [Test]
        public void EveryWorldHasASiteAColonyCanLandOn()
        {
            for (uint seed = 0; seed < 300; seed++)
            {
                PlanetView p = Generate(seed * 2654435761u);
                Assert.That(p.SuggestedTile, Is.GreaterThanOrEqualTo(0), $"seed {seed}");
                Assert.That(p.Verdict(p.SuggestedTile), Is.EqualTo(SettleVerdict.Settleable));
            }
        }

        [Test]
        [Category("Long")]
        public void EveryWorldOfAThousandHasASiteAColonyCanLandOn()
        {
            for (uint seed = 0; seed < 1000; seed++)
                Assert.That(Generate(seed * 40503u + 17u).SuggestedTile, Is.GreaterThanOrEqualTo(0), $"seed {seed}");
        }

        [Test]
        public void TheVerdictSaysWhy()
        {
            PlanetView p = Generate(4u);
            bool water = false, steep = false, notYet = false, yes = false;
            for (int i = 0; i < p.TileCount; i++)
            {
                switch (p.Verdict(i))
                {
                    case SettleVerdict.OpenWater: water = true; Assert.That(p.Water[i]); break;
                    case SettleVerdict.TooSteep: steep = true; Assert.That(p.HillsAt(i), Is.EqualTo(HillBand.Sheer)); break;
                    case SettleVerdict.NotYetAvailable: notYet = true; Assert.That(p.BiomeAt(i).Settleable, Is.False); break;
                    case SettleVerdict.Settleable: yes = true; Assert.That(p.BiomeAt(i).LabelKey, Is.EqualTo("ui.biome.meadow")); break;
                }
            }
            Assert.That(water && steep && notYet && yes, Is.True, "a planet shows all four answers");
        }

        /// <summary>A tile read off the planet is the site a colony remembers, latitude and all.</summary>
        [Test]
        public void ATileIsTheSiteAColonyRemembers()
        {
            PlanetView p = Generate(6u);
            SiteTile site = p.Tile(p.SuggestedTile);
            Assert.That(site.TileIndex, Is.EqualTo(p.SuggestedTile));
            Assert.That(HexGrid.Index(site.Column, site.Row, p.Width), Is.EqualTo(site.TileIndex));
            Assert.That(site.BiomeDefName, Is.EqualTo("Biome_Meadow"));
            Assert.That(site.LatitudePerMille, Is.EqualTo(HexGrid.LatitudePerMille(site.Row, p.Height)));
            Assert.That(site.MeanTempC, Is.EqualTo(p.MeanTempC[p.SuggestedTile]));
        }

        /// <summary>
        /// What a planet costs to generate, recorded in design 57 §9d. Asserted only against a
        /// generous ceiling: the fast tier is not a benchmark, and the number to quote is the printed one.
        /// </summary>
        [Test]
        public void WhatAPlanetCostsToGenerate()
        {
            Generate(1u); // warm the content and the JIT
            var times = new List<double>();
            for (uint seed = 2; seed < 22; seed++)
            {
                var watch = Stopwatch.StartNew();
                Generate(seed);
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            double median = times[times.Count / 2];
            TestContext.Progress.WriteLine($"planet 64 x 32: median {median:F2} ms, worst {times[times.Count - 1]:F2} ms over {times.Count} seeds");
            Assert.That(median, Is.LessThan(250), "a planet is a menu's wait, not a frame's; 50 ms is the budget design 57 quotes");
        }
    }
}
