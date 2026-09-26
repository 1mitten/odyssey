#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A small hand-made planet for the World screen's tests: this assembly cannot see the
    /// simulation's generator, and a planet made by hand says exactly which tile is what.
    /// </summary>
    static class TestPlanet
    {
        public const int Ice = 0, Meadow = 1, Scrub = 2, Ocean = 3;

        /// <summary>
        /// 64 × 32. Rows 0–2 and 29–31 are sea, the top row frozen; columns 40–47 are a sea strip from
        /// pole to pole; the rest is land, Meadow west of column 20 and Scrub east of it. A land tile's
        /// hills are its column mod 5, so every band is present.
        /// </summary>
        public static PlanetView Make(uint seed = 1u)
        {
            const int w = 64, h = 32, n = w * h;
            var biomes = new[]
            {
                new BiomeView("Biome_Ice", "ui.biome.ice", false, false, 0xb9c9d2, 0xf4f8fa, MapRamp.Ice),
                new BiomeView("Biome_Meadow", "ui.biome.meadow", true, false, 0xa3cf72, 0x4f8f45, MapRamp.Land),
                new BiomeView("Biome_DryScrub", "ui.biome.dryscrub", false, false, 0xe0b577, 0xa8743f, MapRamp.Land),
                new BiomeView("Biome_Ocean", "ui.biome.ocean", false, true, 0x0b1d2c, 0x2f6f8c, MapRamp.Ocean),
            };
            var biome = new byte[n];
            var elevation = new int[n];
            var hills = new byte[n];
            var temp = new int[n];
            var rain = new int[n];
            var metres = new int[n];
            var ruin = new int[n];
            var water = new bool[n];
            var coastal = new bool[n];
            int suggested = -1;
            for (int i = 0; i < n; i++)
            {
                int c = HexGrid.Column(i, w), r = HexGrid.Row(i, w);
                water[i] = r < 3 || r > 28 || (c >= 40 && c < 48);
                biome[i] = (byte)(water[i] ? (r == 0 ? Ice : Ocean) : c < 20 ? Meadow : Scrub);
                elevation[i] = water[i] ? 200 : 600 + c * 5;
                hills[i] = (byte)(water[i] ? 0 : c % 5);
                temp[i] = 900 + c * 10;
                rain[i] = 1000;
                metres[i] = water[i] ? -1000 : 100 * c;
                if (suggested < 0 && biome[i] == Meadow && hills[i] == (byte)HillBand.Rolling) suggested = i;
            }
            return new PlanetView(seed, w, h, 400, biomes, biome, elevation, hills, temp, rain, metres, ruin, water,
                coastal, suggested, new[] { 600, 1000, 1300, 1800, -700, -1700 });
        }

        public static int Tile(int column, int row) => HexGrid.Index(column, row, 64);
    }

    public class WorldMapGeometryTests
    {
        static readonly WorldMapGeometry Map = new WorldMapGeometry(64, 32);

        [Test]
        public void TheMapIsTheSizeTheSpecificationQuotes()
        {
            Assert.That(Map.Width, Is.EqualTo(1187f).Within(1f));
            Assert.That(Map.Height, Is.EqualTo(515f).Within(1f));
            Assert.That(Map.HexHeight, Is.EqualTo(21.2f).Within(0.1f));
        }

        [Test]
        public void EveryTileCentrePicksItsOwnTile()
        {
            for (int t = 0; t < 64 * 32; t++)
            {
                int c = HexGrid.Column(t, 64), r = HexGrid.Row(t, 64);
                Assert.That(Map.TileAt(Map.CentreX(c, r), Map.CentreY(r)), Is.EqualTo(t), $"({c},{r})");
            }
        }

        /// <summary>A point just inside each corner of a hex still picks that hex.</summary>
        [Test]
        public void APointNearTheEdgeOfAHexPicksThatHex()
        {
            int tile = TestPlanet.Tile(10, 7);
            float[] corners = Map.Outline(tile, -1.5f);
            for (int i = 0; i < 12; i += 2)
                Assert.That(Map.TileAt(corners[i], corners[i + 1]), Is.EqualTo(tile), $"corner {i / 2}");
        }

        [Test]
        public void ThePickWrapsEastToWest()
        {
            int first = TestPlanet.Tile(0, 6);
            Assert.That(Map.TileAt(Map.CentreX(0, 6) + Map.WrapWidth, Map.CentreY(6)), Is.EqualTo(first), "one turn east");
            Assert.That(Map.TileAt(Map.CentreX(0, 6) - Map.WrapWidth, Map.CentreY(6)), Is.EqualTo(first), "one turn west");
            // The odd rows' overhang past the last column is the first column again.
            Assert.That(Map.TileAt(Map.WrapWidth + 2f, Map.CentreY(7)), Is.EqualTo(TestPlanet.Tile(63, 7)));
            Assert.That(Map.TileAt(Map.WrapWidth + Map.HexWidth * 0.9f, Map.CentreY(6)), Is.EqualTo(TestPlanet.Tile(0, 6)));
        }

        [Test]
        public void OffTheTopAndBottomIsNoTile()
        {
            Assert.That(Map.TileAt(100f, -30f), Is.EqualTo(-1));
            Assert.That(Map.TileAt(100f, Map.Height + 30f), Is.EqualTo(-1));
        }
    }

    public class WorldMapViewTests
    {
        static WorldMapView View()
        {
            var view = new WorldMapView(new WorldMapGeometry(64, 32));
            view.Resize(1187f, 515f); // fit 1
            return view;
        }

        [Test]
        public void AtOneTimesTheMapIsCentredAndStill()
        {
            WorldMapView view = View();
            view.PanBy(200f, 50f);
            Assert.That(view.PanX, Is.Zero);
            Assert.That(view.PanY, Is.Zero);
            Assert.That(view.TileAt(view.ToScreenX(view.Map.CentreX(5, 5)), view.ToScreenY(view.Map.CentreY(5))),
                Is.EqualTo(TestPlanet.Tile(5, 5)));
        }

        [Test]
        public void ZoomStepsByOneAndAHalfBetweenOneAndFour()
        {
            WorldMapView view = View();
            var seen = new List<float>();
            for (int i = 0; i < 6; i++) { view.ZoomIn(); seen.Add(view.TargetZoom); }
            Assert.That(seen, Is.EqualTo(new[] { 1.5f, 2.25f, 3.375f, 4f, 4f, 4f }).Within(0.001f));
            for (int i = 0; i < 6; i++) view.ZoomOut();
            Assert.That(view.TargetZoom, Is.EqualTo(1f));
            Assert.That(view.ZoomLabel, Is.EqualTo("1x"));
        }

        /// <summary>The map point under the pointer stays under it when the wheel zooms there.</summary>
        [Test]
        public void ZoomingAboutThePointerKeepsWhatIsUnderIt()
        {
            WorldMapView view = View();
            int under = view.TileAt(300f, 200f);
            view.ZoomIn(300f, 200f);
            view.Tick(1f);
            Assert.That(view.TileAt(300f, 200f), Is.EqualTo(under));
            view.ZoomIn(300f, 200f);
            view.Tick(1f);
            Assert.That(view.TileAt(300f, 200f), Is.EqualTo(under));
        }

        [Test]
        public void TheZoomEasesOverAFifthOfASecond()
        {
            WorldMapView view = View();
            view.ZoomIn();
            Assert.That(view.Moving, Is.True);
            Assert.That(view.Zoom, Is.EqualTo(1f).Within(0.001f));
            view.Tick(0.09f);
            Assert.That(view.Zoom, Is.GreaterThan(1f).And.LessThan(1.5f));
            view.Tick(0.1f);
            Assert.That(view.Moving, Is.False);
            Assert.That(view.Zoom, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void ThePanWrapsEastAndStopsNorthAndSouth()
        {
            WorldMapView view = View();
            view.ZoomTo(4f, 593f, 257f);
            view.Tick(1f);
            int middle = view.TileAt(593f, 257f);
            view.PanBy(-view.Map.WrapWidth * view.Scale, 0f); // one whole turn east
            Assert.That(view.TileAt(593f, 257f), Is.EqualTo(middle), "a whole turn comes back to the same place");

            view.PanBy(0f, 100000f);
            Assert.That(view.ToScreenY(0f), Is.LessThanOrEqualTo(0.01f), "the north edge never comes inside the box");
            view.PanBy(0f, -100000f);
            Assert.That(view.ToScreenY(view.Map.Height), Is.GreaterThanOrEqualTo(view.BoxHeight - 0.01f), "nor the south");
        }

        [Test]
        public void FitComesBackToOneAndTheCentre()
        {
            WorldMapView view = View();
            view.ZoomTo(3f, 100f, 100f);
            view.Tick(1f);
            view.FitToBox();
            view.Tick(1f);
            Assert.That(view.Zoom, Is.EqualTo(1f));
            Assert.That(view.PanX, Is.Zero);
            Assert.That(view.PanY, Is.Zero);
        }
    }

    public class WorldChoiceTests
    {
        static System.Func<uint> Deals(params uint[] numbers)
        {
            int next = 0;
            return () => numbers[next < numbers.Length ? next++ : numbers.Length - 1];
        }

        static WorldChoice Choice(out SeedField seed, out int generated, int randomPick = 0)
        {
            seed = new SeedField(Deals(11u, 12u, 13u));
            int count = 0;
            var choice = new WorldChoice(seed, s => { count++; return TestPlanet.Make(s); }, n => randomPick % n);
            seed.Draw();
            generated = count;
            return choice;
        }

        [Test]
        public void ANewSeedMakesAPlanetAndPicksItsSuggestedSite()
        {
            WorldChoice choice = Choice(out _, out int generated);
            Assert.That(generated, Is.EqualTo(1));
            Assert.That(choice.Selected, Is.EqualTo(choice.Planet!.SuggestedTile));
            Assert.That(choice.CanGoNext, Is.True, "Next is one press away");
            Assert.That(choice.Site!.Value.BiomeDefName, Is.EqualTo("Biome_Meadow"));
        }

        [Test]
        public void ThePlanetIsMadeOncePerSeed()
        {
            int count = 0;
            var seed = new SeedField(Deals(11u, 12u));
            var choice = new WorldChoice(seed, s => { count++; return TestPlanet.Make(s); });
            seed.Draw();
            choice.Refresh();
            choice.Refresh();
            Assert.That(count, Is.EqualTo(1), "a refresh with the same seed repaints nothing");
            seed.Reroll();
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void EachVerdictSaysWhyAndNextFollowsIt()
        {
            WorldChoice choice = Choice(out _, out _);
            (int Tile, SettleVerdict Verdict)[] cases =
            {
                (TestPlanet.Tile(1, 10), SettleVerdict.Settleable),
                (TestPlanet.Tile(25, 10), SettleVerdict.NotYetAvailable),
                (TestPlanet.Tile(44, 10), SettleVerdict.OpenWater),
                (TestPlanet.Tile(0, 0), SettleVerdict.OpenWater),
                (TestPlanet.Tile(4, 10), SettleVerdict.TooSteep),
            };
            foreach (var (tile, verdict) in cases)
            {
                choice.Select(tile);
                Assert.That(choice.Verdict, Is.EqualTo(verdict), $"tile {tile}");
                Assert.That(choice.CanGoNext, Is.EqualTo(verdict == SettleVerdict.Settleable));
                Assert.That(choice.Site.HasValue, Is.EqualTo(verdict == SettleVerdict.Settleable));
            }
            Assert.That(WorldChoice.VerdictKey(SettleVerdict.NotYetAvailable), Is.EqualTo("ui.world.unavailable"));
            Assert.That(Registry.Describe("ui.world.unavailable"), Does.Contain("Only Meadow"));
        }

        /// <summary>A typed seed that no longer names the planet on screen switches Next off rather than landing on the old one.</summary>
        [Test]
        public void NextIsOffWhileTheBoxDoesNotNameTheSeed()
        {
            WorldChoice choice = Choice(out SeedField seed, out _);
            seed.Type("twelve");
            Assert.That(choice.CanGoNext, Is.False);
        }

        [Test]
        public void RandomSiteIsAnotherPlaceAColonyCanLand()
        {
            WorldChoice choice = Choice(out _, out _, randomPick: 7);
            int before = choice.Selected;
            Assert.That(choice.SelectRandom(), Is.True);
            Assert.That(choice.Selected, Is.Not.EqualTo(before));
            Assert.That(choice.Verdict, Is.EqualTo(SettleVerdict.Settleable));
        }

        [Test]
        public void TheArrowsStepOneHex()
        {
            WorldChoice choice = Choice(out _, out _);
            choice.Select(TestPlanet.Tile(63, 10));
            choice.Move(0);
            Assert.That(choice.Selected, Is.EqualTo(TestPlanet.Tile(0, 10)), "east of the last column is the first");
            choice.Select(TestPlanet.Tile(5, 0));
            choice.Move(1);
            Assert.That(choice.Selected, Is.EqualTo(TestPlanet.Tile(5, 0)), "nothing north of the pole");
        }

        [Test]
        public void TheSitePanelSaysWhatTheSpecificationLists()
        {
            PlanetView planet = TestPlanet.Make();
            int tile = TestPlanet.Tile(6, 10); // Meadow, Rolling
            List<WorldStat> rows = WorldChoice.Stats(planet, tile);
            var keys = rows.ConvertAll(r => r.LabelKey);
            Assert.That(keys, Is.EqualTo(new[]
            {
                "ui.world.biome", "ui.world.hills", "ui.world.temperature", "ui.world.seasons", "ui.world.rainfall",
                "ui.world.latitude", "ui.world.tile",
            }));
            Assert.That(rows[0].Value, Is.EqualTo("Meadow"));
            Assert.That(rows[1].Value, Is.EqualTo("Rolling"));
            Assert.That(rows[4].Value, Is.EqualTo("1,000 mm"));
            Assert.That(rows[6].Value, Is.EqualTo("6, 10"));
            Assert.That(rows[3].Value, Does.Contain(" to "));

            List<WorldStat> mountain = WorldChoice.Stats(planet, TestPlanet.Tile(3, 10));
            Assert.That(mountain[mountain.Count - 1].LabelKey, Is.EqualTo("ui.world.depth"));
            Assert.That(mountain[mountain.Count - 1].Value, Is.EqualTo("24 layers"));
        }

        [Test]
        public void TheSeasonsAreTheColonysOwnArithmetic()
        {
            PlanetView planet = TestPlanet.Make();
            int tile = TestPlanet.Tile(6, 10);
            planet.SeasonRange(tile, out int coldest, out int warmest);
            int seasonality = SiteRules.SeasonalityPerMille(planet.LatitudePerMille(tile));
            Assert.That(coldest, Is.EqualTo(SiteRules.MonthMeanC(planet.MeanTempC[tile], -1700, seasonality)));
            Assert.That(warmest, Is.EqualTo(SiteRules.MonthMeanC(planet.MeanTempC[tile], 1800, seasonality)));
        }

        [Test]
        public void EveryWorldWordIsARegisteredName()
        {
            foreach (string key in WorldChoice.IconKeys)
                Assert.That(Registry.Labels.ContainsKey(key), $"{key} is not in icon-keys.csv");
            foreach (string key in WorldChoice.HillKeys)
                Assert.That(Registry.Labels.ContainsKey(key), $"{key} is not in icon-keys.csv");
            foreach (string key in RegionNames.LandFrames)
                Assert.That(Registry.Label(key), Does.Contain("{name}"), key);
            foreach (string key in RegionNames.SeaFrames)
                Assert.That(Registry.Label(key), Does.Contain("{name}"), key);
        }
    }

    public class WorldMapPainterTests
    {
        [Test]
        public void TheSameSeedPaintsTheSamePicture()
        {
            byte[] a = WorldMapPainter.Paint(TestPlanet.Make(), 1f, out int w, out int h);
            byte[] b = WorldMapPainter.Paint(TestPlanet.Make(), 1f, out _, out _);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(w, Is.EqualTo(1187).Within(1));
            Assert.That(h, Is.EqualTo(516).Within(1));
        }

        /// <summary>A mountain tile carries its dark mark at the centre; the same biome's flat tile beside it does not.</summary>
        [Test]
        public void AMountainIsMarkedAndFlatGroundIsNot()
        {
            PlanetView planet = TestPlanet.Make();
            var map = new WorldMapGeometry(64, 32);
            byte[] image = WorldMapPainter.Paint(planet, 2f, out int w, out _);
            int Luma(int column, int row, float dy)
            {
                int x = (int)(map.CentreX(column, row) * 2f), y = (int)((map.CentreY(row) + dy) * 2f);
                int p = 4 * (y * w + x);
                return image[p] + image[p + 1] + image[p + 2];
            }
            // Column 3 is Mountainous (3 mod 5); column 5 is Flat. The peak's apex is 3 px above the centre.
            Assert.That(Luma(3, 10, -2.6f), Is.LessThan(Luma(5, 10, -2.6f) - 40), "the peak's ink darkens its apex");
        }

        [Test]
        public void TheBottomUpImageIsTheTopDownOneFlipped()
        {
            byte[] down = WorldMapPainter.Paint(TestPlanet.Make(), 1f, out int w, out int h);
            byte[] up = WorldMapPainter.Paint(TestPlanet.Make(), 1f, out _, out _, bottomUp: true);
            for (int x = 0; x < w * 4; x++) Assert.That(up[x], Is.EqualTo(down[(h - 1) * w * 4 + x]));
        }

        /// <summary>What a repaint costs at the page's 2×, recorded in design 59 §9d. The fast tier is not a benchmark: the printed number is the one to quote.</summary>
        [Test]
        public void WhatAPaintCosts()
        {
            PlanetView planet = TestPlanet.Make();
            byte[] buffer = WorldMapPainter.Paint(planet, WorldLayout.PaintScale, out _, out _, bottomUp: true);
            var times = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var watch = Stopwatch.StartNew();
                WorldMapPainter.Paint(planet, WorldLayout.PaintScale, out _, out _, bottomUp: true, into: buffer);
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            TestContext.Progress.WriteLine($"map paint at {WorldLayout.PaintScale}x: median {times[2]:F1} ms, worst {times[4]:F1} ms");
            Assert.That(times[2], Is.LessThan(1000));
        }
    }

    public class RegionNamesTests
    {
        [Test]
        public void TheSameSeedNamesTheSamePlaces()
        {
            List<RegionLabel> a = RegionNames.For(TestPlanet.Make(5u));
            List<RegionLabel> b = RegionNames.For(TestPlanet.Make(5u));
            Assert.That(a.Count, Is.GreaterThan(0));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].Text, Is.EqualTo(b[i].Text));
                Assert.That(a[i].X, Is.EqualTo(b[i].X));
            }
        }

        [Test]
        public void AnotherSeedNamesThemOtherwise()
        {
            Assert.That(RegionNames.For(TestPlanet.Make(5u))[0].Text, Is.Not.EqualTo(RegionNames.For(TestPlanet.Make(6u))[0].Text));
        }

        [Test]
        public void LandsAreCapitalsAndSeasKeepTheirDistance()
        {
            List<RegionLabel> labels = RegionNames.For(TestPlanet.Make(9u));
            var map = new WorldMapGeometry(64, 32);
            var seas = labels.FindAll(l => l.Sea);
            Assert.That(labels.FindAll(l => !l.Sea).TrueForAll(l => l.Text == l.Text.ToUpperInvariant()));
            Assert.That(seas.Count, Is.LessThanOrEqualTo(WorldLayout.SeaLabelsMax));
            for (int i = 0; i < seas.Count; i++)
                for (int j = i + 1; j < seas.Count; j++)
                {
                    float dx = Math.Abs(seas[i].X - seas[j].X);
                    dx = Math.Min(dx, map.WrapWidth - dx);
                    float d = (float)Math.Sqrt(dx * dx + (seas[i].Y - seas[j].Y) * (seas[i].Y - seas[j].Y));
                    Assert.That(d, Is.GreaterThanOrEqualTo(WorldLayout.SeaLabelSpacing));
                }
        }

        /// <summary>The syllables are code here and a row in proper-nouns.csv for the owner to read: the two must agree.</summary>
        [Test]
        public void TheSyllablesAreTheOnesTheWikiLists()
        {
            string? csv = Find("docs/design/proper-nouns.csv");
            if (csv == null) Assert.Ignore("no repository checkout beside this build");
            string row = Array.Find(File.ReadAllLines(csv!), l => l.StartsWith("world.regionnames,", StringComparison.Ordinal))
                         ?? throw new AssertionException("proper-nouns.csv has no world.regionnames row");

            string Table(string name)
            {
                int at = row.IndexOf(name + "=", StringComparison.Ordinal);
                int end = row.IndexOfAny(new[] { ';', '.' }, at);
                return row.Substring(at + name.Length + 1, end - at - name.Length - 1).Trim();
            }
            Assert.That(Table("S1"), Is.EqualTo(string.Join(" ", RegionNames.First)));
            Assert.That(Table("S2"), Is.EqualTo(string.Join(" ", RegionNames.Middle)));
            Assert.That(Table("S3"), Is.EqualTo(string.Join(" ", Array.ConvertAll(RegionNames.Last, s => s.Length == 0 ? "-" : s))));
        }

        static string? Find(string relative)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}

namespace Odyssey.Tests.Hud
{
    /// <summary>The New game flow with a planet in it (design 59 §9): New game, World, Next, setup page, Start.</summary>
    public class WorldFlowTests
    {
        static MenuDirector WithWorld(out WorldChoice world, params uint[] seeds)
        {
            int next = 0;
            var seed = new SeedField(() => seeds[next < seeds.Length ? next++ : seeds.Length - 1]);
            var select = new ColonistSelect(
                (s, slot) => new Candidate(s, "person-" + s, 30 + slot, "Scrapper", System.Array.Empty<SkillRow>()));
            world = new WorldChoice(seed, s => TestPlanet.Make(s), n => 0);
            var menu = new MenuDirector(seed, select, world);
            menu.Show();
            return menu;
        }

        [Test]
        public void NewGameOpensOnThePlanetAndNextOnTheSetupPage()
        {
            MenuDirector menu = WithWorld(out WorldChoice world, 77u);
            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.World));
            Assert.That(world.Planet!.WorldSeed, Is.EqualTo(77u), "the drawn seed made the planet");
            Assert.That(menu.Start(), Is.False, "Start is the setup page's, not the planet's");

            Assert.That(menu.NextFromWorld(), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.NewGame));
        }

        /// <summary>Back goes one level at a time, and the setup page's people survive a trip to the planet and back.</summary>
        [Test]
        public void BackIsOneLevelAndKeepsThePeople()
        {
            MenuDirector menu = WithWorld(out _, 77u, 1u, 2u, 3u);
            menu.Choose(SessionCommands.NewGameKey);
            menu.NextFromWorld();
            uint[] people = menu.Colonists!.ChosenSeeds();

            Assert.That(menu.Back(), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.World));
            Assert.That(menu.NextFromWorld(), Is.True);
            Assert.That(menu.Colonists.ChosenSeeds(), Is.EqualTo(people), "hunting for a site must not cost a colonist");

            menu.Back();
            Assert.That(menu.Back(), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root));
        }

        [Test]
        public void NextIsRefusedOnASiteNoColonyCanLandOn()
        {
            MenuDirector menu = WithWorld(out WorldChoice world, 77u);
            menu.Choose(SessionCommands.NewGameKey);
            world.Select(TestPlanet.Tile(44, 10)); // open water
            Assert.That(menu.NextFromWorld(), Is.False);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.World));
        }

        [Test]
        public void StartCarriesTheSiteAndTheWorldsSeed()
        {
            MenuDirector menu = WithWorld(out WorldChoice world, 77u);
            NewGameChoice? started = null;
            menu.StartRequested += c => started = c;
            menu.Choose(SessionCommands.NewGameKey);
            world.Select(TestPlanet.Tile(1, 12));
            menu.NextFromWorld();
            Assert.That(menu.Start(), Is.True);

            Assert.That(started!.Value.Seed, Is.EqualTo(77u), "with a site the choice's seed is the world's");
            Assert.That(started.Value.Site!.Value.TileIndex, Is.EqualTo(TestPlanet.Tile(1, 12)));
        }

        /// <summary>The control: a flow with no planet is the one every rig was written against.</summary>
        [Test]
        public void WithNoPlanetNewGameGoesStraightToTheSetupPage()
        {
            var menu = new MenuDirector(new SeedField(() => 5u));
            menu.Show();
            menu.Choose(SessionCommands.NewGameKey);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.NewGame));
            NewGameChoice? started = null;
            menu.StartRequested += c => started = c;
            menu.Start();
            Assert.That(started!.Value.Site, Is.Null);
            Assert.That(menu.Back(), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root));
        }
    }
}
