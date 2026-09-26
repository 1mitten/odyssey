#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>A name on the World map, where it is drawn, in 1× map pixels.</summary>
    public readonly struct RegionLabel
    {
        public RegionLabel(string text, float x, float y, bool sea)
        {
            Text = text;
            X = x;
            Y = y;
            Sea = sea;
        }

        /// <summary>As drawn: a land name in capitals, a sea's as written.</summary>
        public readonly string Text;
        public readonly float X;
        public readonly float Y;
        public readonly bool Sea;
    }

    /// <summary>
    /// Names for the planet's lands and seas (design 59 §9c, the specification's "Region names"):
    /// generated from the world seed, drawn only, never saved and never in the hash.
    ///
    /// <para><b>The syllables are listed twice and guarded once.</b> The tables are here, because the
    /// interface builds the names; <c>proper-nouns.csv</c>'s <c>world.regionnames</c> row lists the
    /// same three so the owner can read and strike them, and <c>RegionNamesTests</c> fails the day the
    /// two differ. The frames are registry labels (<c>ui.world.region.*</c>), so they translate.</para>
    /// </summary>
    public static class RegionNames
    {
        public static readonly string[] First =
            { "Ver", "Cal", "Mor", "Ess", "Tal", "Ond", "Bra", "Hes", "Kel", "Iv", "Sar", "Dun", "Ul", "Quen", "Ash" };

        public static readonly string[] Middle =
            { "an", "ere", "oth", "ia", "ul", "ane", "ess", "ir", "orra", "en", "ys" };

        /// <summary>The endings; the two blanks make a bare name twice as likely as any one ending.</summary>
        public static readonly string[] Last = { "", "", "d", "n", "th", "ry", "ck" };

        /// <summary>A land's name is the word bare, in one of these frames, or with <c>-ia</c>.</summary>
        public static readonly string[] LandFrames =
            { "ui.world.region.reach", "ui.world.region.downs", "ui.world.region.hold", "ui.world.region.greater" };

        public static readonly string[] SeaFrames =
        {
            "ui.world.region.seaof", "ui.world.region.deep", "ui.world.region.shelf", "ui.world.region.sound",
            "ui.world.region.gulfof",
        };

        const uint Purpose = 0x4E41_4D45u;

        /// <summary>
        /// The labels for a planet: the seven largest lands of fourteen tiles or more at their centres,
        /// and up to five names on the largest sea, well inside it and well apart.
        /// </summary>
        public static List<RegionLabel> For(PlanetView planet)
        {
            var map = new WorldMapGeometry(planet.Width, planet.Height);
            var random = new NameRandom(planet.WorldSeed ^ Purpose);
            var used = new HashSet<string>(StringComparer.Ordinal);
            var labels = new List<RegionLabel>();

            // ---- lands ----
            List<List<int>> lands = Components(planet, land: true);
            lands.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : a[0].CompareTo(b[0]));
            for (int i = 0; i < lands.Count && labels.Count < WorldLayout.LandLabelsMax; i++)
            {
                if (lands[i].Count < WorldLayout.LandLabelMinTiles) break;
                Centroid(map, planet, lands[i], out float x, out float y);
                string name = Unique(used, () => LandName(ref random));
                labels.Add(new RegionLabel(name.ToUpperInvariant(), x, y, sea: false));
            }

            // ---- the largest sea ----
            List<List<int>> seas = Components(planet, land: false);
            if (seas.Count == 0) return labels;
            seas.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : a[0].CompareTo(b[0]));
            var inSea = new HashSet<int>(seas[0]);
            int[] fromShore = DistanceFromShore(planet, inSea);

            var candidates = new List<int>();
            foreach (int tile in seas[0])
            {
                int row = HexGrid.Row(tile, planet.Width);
                if (row < WorldLayout.SeaLabelPolarRows || row >= planet.Height - WorldLayout.SeaLabelPolarRows) continue;
                if (fromShore[tile] < 2) continue; // every neighbour open sea
                candidates.Add(tile);
            }
            // Deepest into the sea first, then by index: the names sit in open water and the choice repeats.
            candidates.Sort((a, b) => fromShore[a] != fromShore[b] ? fromShore[b].CompareTo(fromShore[a]) : a.CompareTo(b));

            var placed = new List<(float X, float Y)>();
            foreach (int tile in candidates)
            {
                if (placed.Count >= WorldLayout.SeaLabelsMax) break;
                int column = HexGrid.Column(tile, planet.Width), row = HexGrid.Row(tile, planet.Width);
                float x = map.CentreX(column, row), y = map.CentreY(row);
                bool clear = true;
                foreach (var p in placed)
                    if (WrappedDistance(map, x, y, p.X, p.Y) < WorldLayout.SeaLabelSpacing) { clear = false; break; }
                if (!clear) continue;
                placed.Add((x, y));
                string name = Unique(used, () => SeaName(ref random));
                labels.Add(new RegionLabel(name, x, y, sea: true));
            }
            return labels;
        }

        /// <summary>One word: a first syllable, a middle and an ending.</summary>
        public static string Word(ref NameRandom random) =>
            First[random.Next(First.Length)] + Middle[random.Next(Middle.Length)] + Last[random.Next(Last.Length)];

        static string LandName(ref NameRandom random)
        {
            string word = Word(ref random);
            int form = random.Next(LandFrames.Length + 2);
            if (form == LandFrames.Length) return word;
            if (form == LandFrames.Length + 1) return word + "ia";
            return Registry.Label(LandFrames[form]).Replace("{name}", word);
        }

        static string SeaName(ref NameRandom random)
        {
            string word = Word(ref random);
            return Registry.Label(SeaFrames[random.Next(SeaFrames.Length)]).Replace("{name}", word);
        }

        /// <summary>A name not already on this map; after a few tries, whatever came up.</summary>
        static string Unique(HashSet<string> used, Func<string> make)
        {
            string name = make();
            for (int i = 0; i < 8 && used.Contains(name); i++) name = make();
            used.Add(name);
            return name;
        }

        /// <summary>Connected land (or sea) over the six neighbours, the wrap included.</summary>
        static List<List<int>> Components(PlanetView planet, bool land)
        {
            var seen = new bool[planet.TileCount];
            var result = new List<List<int>>();
            var stack = new Stack<int>();
            for (int start = 0; start < planet.TileCount; start++)
            {
                if (seen[start] || IsLand(planet, start) != land) continue;
                var part = new List<int>();
                seen[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int tile = stack.Pop();
                    part.Add(tile);
                    int column = HexGrid.Column(tile, planet.Width), row = HexGrid.Row(tile, planet.Width);
                    for (int d = 0; d < HexGrid.Directions; d++)
                    {
                        int next = HexGrid.Neighbour(column, row, d, planet.Width, planet.Height);
                        if (next < 0 || seen[next] || IsLand(planet, next) != land) continue;
                        seen[next] = true;
                        stack.Push(next);
                    }
                }
                part.Sort();
                result.Add(part);
            }
            return result;
        }

        /// <summary>Land for naming: not water. Frozen sea is still sea.</summary>
        static bool IsLand(PlanetView planet, int tile) => !planet.Water[tile];

        /// <summary>A centroid with a circular mean for x, so a land across the wrap is named in its middle.</summary>
        static void Centroid(WorldMapGeometry map, PlanetView planet, List<int> tiles, out float x, out float y)
        {
            double sin = 0, cos = 0, rows = 0;
            foreach (int tile in tiles)
            {
                int column = HexGrid.Column(tile, planet.Width), row = HexGrid.Row(tile, planet.Width);
                double angle = 2 * Math.PI * map.CentreX(column, row) / map.WrapWidth;
                sin += Math.Sin(angle);
                cos += Math.Cos(angle);
                rows += map.CentreY(row);
            }
            double mean = Math.Atan2(sin, cos);
            if (mean < 0) mean += 2 * Math.PI;
            x = (float)(mean / (2 * Math.PI) * map.WrapWidth);
            y = (float)(rows / tiles.Count);
        }

        /// <summary>Hexes from the nearest tile outside the sea; 1 on the shore.</summary>
        static int[] DistanceFromShore(PlanetView planet, HashSet<int> sea)
        {
            var distance = new int[planet.TileCount];
            var queue = new Queue<int>();
            for (int t = 0; t < planet.TileCount; t++)
            {
                if (!sea.Contains(t)) { distance[t] = 0; queue.Enqueue(t); }
                else distance[t] = int.MaxValue;
            }
            while (queue.Count > 0)
            {
                int tile = queue.Dequeue();
                int column = HexGrid.Column(tile, planet.Width), row = HexGrid.Row(tile, planet.Width);
                for (int d = 0; d < HexGrid.Directions; d++)
                {
                    int next = HexGrid.Neighbour(column, row, d, planet.Width, planet.Height);
                    if (next < 0 || distance[next] <= distance[tile] + 1) continue;
                    distance[next] = distance[tile] + 1;
                    queue.Enqueue(next);
                }
            }
            // A pole row has fewer neighbours and no shore beyond it: count it as the shore.
            for (int t = 0; t < planet.TileCount; t++)
            {
                int row = HexGrid.Row(t, planet.Width);
                if ((row == 0 || row == planet.Height - 1) && sea.Contains(t)) distance[t] = Math.Min(distance[t], 1);
            }
            return distance;
        }

        static float WrappedDistance(WorldMapGeometry map, float x1, float y1, float x2, float y2)
        {
            float dx = Math.Abs(x1 - x2);
            dx = Math.Min(dx, map.WrapWidth - dx);
            float dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>A small seeded stream (xorshift) for names: presentation, so it never touches the simulation's.</summary>
    public struct NameRandom
    {
        uint _state;

        public NameRandom(uint seed)
        {
            _state = seed == 0 ? 0x9E37_79B9u : seed;
            for (int i = 0; i < 4; i++) Next(2); // stir, so neighbouring seeds part at once
        }

        public int Next(int exclusiveMax)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)exclusiveMax);
        }
    }
}
