using System;
using System.Collections.Generic;
using System.IO;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.SaveProbe
{
    /// <summary>
    /// <b>What is actually in a saved game, read without Unity.</b>
    ///
    /// <para>A playtest report is a screenshot, and a screenshot is an argument about pixels. Three
    /// sessions in a row argued about one grey floor tile from stills — "it is one layer down", "it
    /// is a stale chunk", "it is the ground through a hole" — and every one of those answers was
    /// reached by reading code and looking harder at the picture. The owner's own save files were
    /// on the same disk the whole time and answer the question in a second: the grey tiles were
    /// stone <i>paving</i>, which nothing in the picture could have told anybody
    /// (<c>docs/design/15-building.md</c> §"The grey tile", 2026-09-18).</para>
    ///
    /// <para>So this loads each save the way the game does — read the header, rebuild the world
    /// from its recipe, load the state over it — and prints the floor arrays. Reach for it before
    /// reasoning about what a screenshot shows.</para>
    ///
    /// <code>
    /// dotnet run --project tools/dotnet/Odyssey.SaveProbe            # the usual save folder
    /// dotnet run --project tools/dotnet/Odyssey.SaveProbe -- &lt;path&gt;  # one file or one folder
    /// </code>
    /// </summary>
    static class Program
    {
        /// <summary>
        /// Where the editor and a built player put saves: <c>Application.persistentDataPath</c>,
        /// which on Windows is company/product under <c>AppData\LocalLow</c>. Restated here rather
        /// than asked, because asking means referencing UnityEngine and this tool exists precisely
        /// so that it does not have to.
        /// </summary>
        static string DefaultFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "Unity Technologies", "com_unity_template_urp-blank", "Saves");

        static int Main(string[] args)
        {
            string target = args.Length > 0 ? args[0] : DefaultFolder;

            // The Defs, found the same way every other headless caller finds them: walk up from
            // this assembly to the directory holding Assets and ProjectSettings. Nothing to set.
            string[] files = File.Exists(target)
                ? new[] { target }
                : Directory.Exists(target)
                    ? Directory.GetFiles(target, "*.odyssey")
                    : Array.Empty<string>();

            if (files.Length == 0)
            {
                Console.Error.WriteLine($"No .odyssey file at '{target}'.");
                return 1;
            }

            Array.Sort(files);
            foreach (string path in files)
            {
                try
                {
                    Report(path);
                }
                catch (Exception e)
                {
                    // One unreadable save must not hide the rest: a folder usually holds several
                    // format versions and the interesting one is rarely the first.
                    Console.WriteLine($"{Path.GetFileName(path)}: FAILED {e.GetType().Name}: {e.Message}");
                }

                Console.WriteLine();
            }

            return 0;
        }

        static void Report(string path)
        {
            SaveHeader header = WorldSave.ReadHeaderOnly(path);
            ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
            {
                Size = header.Size,
                Seed = header.Seed,
                Map = header.Recipe.Map,
                Barren = header.Recipe.Barren,
                Wooded = header.Recipe.Wooded,
                Name = header.Recipe.ColonyName,
                Scenario = ScenarioByName(header.Recipe.Scenario),
            });
            colony.LoadFromFile(path);

            CellGrid grid = colony.Grid;
            GridSize size = grid.Size;

            Console.WriteLine(
                $"=== {Path.GetFileName(path)}  day {header.Recipe.Day}  " +
                $"{size.SizeX}x{size.SizeZ}x{size.SizeY}  tick {header.Tick}");

            // Every (kind, material) pair that exists. The pair is the point: the pane titles a
            // floor by its material alone, so a stone covering and a stone structural floor read
            // identically on screen and differ only here.
            var counts = new Dictionary<(ushort kind, ushort stuff), int>();
            for (int i = 0; i < grid.Floor.Length; i++)
            {
                ushort kind = grid.Floor[i];
                if (kind == CoreContent.SlabNone) continue;
                var key = (kind, grid.FloorStuff[i]);
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;
            }

            if (counts.Count == 0) Console.WriteLine("    no floor of any kind");
            foreach (var pair in counts)
                Console.WriteLine($"    Floor={Kind(pair.Key.kind),-10} Stuff={Stuff(pair.Key.stuff),-10} {pair.Value}");

            ReportMixedMaterials(grid, size);
            ReportItems(colony, size);
            ReportTerrain(grid);
        }

        /// <summary>
        /// What is lying about, by kind and by layer.
        ///
        /// <para>Here because an item on a deck is drawn on the deck, and "grey slivers scattered
        /// over a wooden floor" describes a scatter of dropped things at least as well as it
        /// describes a rendering fault (2026-09-18). The layer matters: a stack on the storey below
        /// is not what anybody is looking at.</para>
        /// </summary>
        static void ReportItems(ColonyWorld colony, GridSize size)
        {
            var byKind = new Dictionary<(string kind, int layer), (int stacks, int units)>();

            foreach (ColonyItem item in colony.Pawns.Items.Items)
            {
                if (item.Despawned || item.Cell < 0) continue;
                if ((uint)item.Cell >= (uint)size.CellCount) continue;

                string kind = item.DefIndex >= 0 && item.DefIndex < colony.Pawns.Items.Content.Items.Length
                    ? colony.Pawns.Items.Content.Items[item.DefIndex].defName
                    : $"def({item.DefIndex})";

                var key = (kind, size.FromIndex(item.Cell).Y);
                byKind.TryGetValue(key, out var n);
                byKind[key] = (n.stacks + 1, n.units + item.Stack);
            }

            if (byKind.Count == 0) { Console.WriteLine("    no items on the ground"); return; }

            foreach (var pair in byKind)
                Console.WriteLine(
                    $"    item {pair.Key.kind,-14} L{pair.Key.layer,-3} " +
                    $"{pair.Value.stacks,4} stacks {pair.Value.units,5} units");
        }

        /// <summary>
        /// Which terrains the board actually holds.
        ///
        /// <para>Some terrains are drawn with a street tile — pavement, soil, gravel all resolve to
        /// a grey <c>SM_Env_Ground_Tile_*</c> — so "is there anything on this board that draws
        /// grey?" is a question about terrain as much as about slabs.</para>
        /// </summary>
        static void ReportTerrain(CellGrid grid)
        {
            var counts = new Dictionary<ushort, int>();
            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                ushort t = grid.Terrain[i];
                if (t == CoreContent.TerrainAir) continue;
                counts.TryGetValue(t, out int n);
                counts[t] = n + 1;
            }

            foreach (var pair in counts)
                Console.WriteLine(
                    $"    terrain {WorldContent.Table[pair.Key].defName,-18} {pair.Value,8}");
        }

        /// <summary>
        /// Cells where two floors of different materials meet on one layer — the picture the owner
        /// sends when a deck has come out patchy, stated as cells and materials so that nobody has
        /// to judge a colour.
        /// </summary>
        static void ReportMixedMaterials(CellGrid grid, GridSize size)
        {
            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };
            int said = 0;

            for (int i = 0; i < grid.Floor.Length && said < 16; i++)
            {
                if (grid.Floor[i] == CoreContent.SlabNone) continue;
                ushort stuff = grid.FloorStuff[i];

                CellRef at = size.FromIndex(i);
                for (int d = 0; d < 4; d++)
                {
                    int nx = at.X + dx[d], nz = at.Z + dz[d];
                    if (!size.Contains(nx, nz, at.Y)) continue;

                    int n = size.Index(nx, nz, at.Y);
                    if (grid.Floor[n] == CoreContent.SlabNone) continue;
                    if (grid.FloorStuff[n] == stuff) continue;

                    Console.WriteLine(
                        $"    {Kind(grid.Floor[i])}/{Stuff(stuff)} at {at} touches " +
                        $"{Kind(grid.Floor[n])}/{Stuff(grid.FloorStuff[n])}");
                    said++;
                    break;
                }
            }
        }

        static string Kind(ushort kind) =>
            kind == CoreContent.SlabBuilt ? "Built"
            : kind == CoreContent.SlabPaved ? "Paved"
            : $"worldgen({kind})";

        static string Stuff(ushort stuff) =>
            stuff == NaturalContent.StuffWood ? "Wood"
            : stuff == NaturalContent.StuffStone ? "Stone"
            : stuff == CoreContent.StuffNone ? "none"
            : $"stuff({stuff})";

        /// <summary>
        /// A scenario name back into a <see cref="ScenarioDef"/>.
        ///
        /// <para><b>The third hand-written copy of this table</b>, which CLAUDE.md already records
        /// as a known gap: <c>OdysseyBootstrap.ScenarioFor</c> and
        /// <c>SessionRoundTripTests.ScenarioByName</c> are the other two. It stays harmless for the
        /// same reason theirs do — a scenario acts only at tick zero, and this tool never ticks —
        /// but it is a third reason to give the registry the lookup it wants.</para>
        /// </summary>
        static ScenarioDef ScenarioByName(string defName)
        {
            switch (defName)
            {
                case "Scenario_Bare": return ScenarioDef.Bare();
                case "Scenario_Playtest":
                default: return ScenarioDef.Playtest();
            }
        }
    }
}
