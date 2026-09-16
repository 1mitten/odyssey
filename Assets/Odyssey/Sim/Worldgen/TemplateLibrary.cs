#nullable enable
using System.Collections.Generic;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// The shell templates the slice ships (U13).
    ///
    /// They are authored here as <see cref="TemplateDef"/> literals rather than in XML because
    /// there is no content pack yet and the simulation must generate a map in a clone with no
    /// <c>Assets/</c> content and no licensed packs. The *format* is already the Def format, so
    /// moving these into <c>Defs/Core/Templates.xml</c> later is a transcription of the same
    /// fields — <c>rows</c> is a list of strings, which the loader binds natively — and no code
    /// outside this file changes.
    ///
    /// Everything is measured in cells. The only metres in the project live in presentation.
    /// </summary>
    public static class TemplateLibrary
    {
        /// <summary>The slice template set, compiled once and shared. Order is fixed by name.</summary>
        public static TemplateSet Slice() => TemplateSet.FromDefs(SliceDefs());

        public static List<TemplateDef> SliceDefs() => new List<TemplateDef>
        {
            TerraceSmall(),
            BlockMedium(),
            TowerTall(),
        };

        /// <summary>
        /// A two-storey terrace unit, six cells square, with a ladder rather than a stair: the
        /// cheap, common, low-rise filler that most residential blocks are made of.
        /// </summary>
        public static TemplateDef TerraceSmall() => new TemplateDef
        {
            defName = "Shell_TerraceSmall",
            label = "terrace unit",
            sizeX = 6,
            sizeZ = 6,
            bottomLayer = 0,
            topLayer = 1,
            stuff = "Concrete",
            weight = 220,
            damageTolerance = 1150,
            wallModuleId = "odyssey.module.wall.panel",
            doorModuleId = "odyssey.module.door.single",
            windowModuleId = "odyssey.module.wall.window",
            ladderModuleId = "odyssey.module.ladder.fixed",
            slabModuleId = "odyssey.module.slab.concrete",
            rows = new List<string>
            {
                // layer 0 — street level, one door onto the plot frontage. A pillar at (2,2)
                // and one founding the upper partition at (3,3) keep the interior off the
                // perimeter wall alone.
                "######",
                "#....#",
                "#.IL.#",
                "#..I.#",
                "#....#",
                "##+###",
                // layer 1 — upper storey, a partition and two windows
                "#oo###",
                "#....#",
                "#.I..#",
                "#.##.#",
                "#....#",
                "###o##",
            },
        };

        /// <summary>
        /// A mid-rise block with a basement: ten by eight, four layers, a stacked stairwell and
        /// interior partitions. The workhorse — big enough to be worth clearing, small enough to
        /// fit most plots.
        /// </summary>
        public static TemplateDef BlockMedium() => new TemplateDef
        {
            defName = "Shell_BlockMedium",
            label = "commercial block",
            sizeX = 10,
            sizeZ = 8,
            bottomLayer = -1,
            topLayer = 2,
            stuff = "Concrete",
            weight = 150,
            damageTolerance = 900,
            wallModuleId = "odyssey.module.wall.block",
            doorModuleId = "odyssey.module.door.double",
            windowModuleId = "odyssey.module.wall.shopfront",
            stairModuleId = "odyssey.module.stair.straight",
            slabModuleId = "odyssey.module.slab.concrete",
            rows = new List<string>
            {
                // layer -1 — basement, reached by the stairwell only. Two pillar columns at
                // (3,2), (6,2), (3,5), (6,5) run the full height of the block (every layer
                // below), so the floor plan never depends on the perimeter wall alone.
                "##########",
                "#........#",
                "#..I..I..#",
                "#...<>...#",
                "#........#",
                "#..I..I..#",
                "#........#",
                "##########",
                // layer 0 — street level, double doors on the frontage
                "####++####",
                "#........#",
                "#..I..I..#",
                "#...<>...#",
                "#..##.##.#",
                "#..I#.I..#",
                "#...#....#",
                "##########",
                // layer 1
                "#oo####oo#",
                "#........#",
                "#..I..I..#",
                "#...<>...#",
                "#..####..#",
                "#..I..I..#",
                "#........#",
                "##########",
                // layer 2 — top storey, no stair above it
                "#oo####oo#",
                "#........#",
                "#..I..I..#",
                "#........#",
                "#........#",
                "#..I..I..#",
                "#........#",
                "##########",
            },
        };

        /// <summary>
        /// A twelve-cell-square tower, basement to six storeys, with a central stair core. Only
        /// fits a large plot on a map with the layers to spare, which is deliberate: the slice map
        /// is five layers deep and will never stamp one.
        /// </summary>
        public static TemplateDef TowerTall()
        {
            var rows = new List<string>(84);

            // A twelve-wide floor plate needs more than a perimeter wall and one small stair
            // core: nine pillar columns at x,z in {3,6,9} run the full height, on every layer,
            // clear of the stairwell (x=5,6 z=4..5).

            // layer -1 — basement
            rows.AddRange(new[]
            {
                "############",
                "#..........#",
                "#..........#",
                "#..I..I..I.#",
                "#....<>....#",
                "#..........#",
                "#..I..I..I.#",
                "#..........#",
                "#..........#",
                "#..I..I..I.#",
                "#..........#",
                "############",
            });

            // layer 0 — lobby
            rows.AddRange(new[]
            {
                "####o++o####",
                "#..........#",
                "#..........#",
                "#..I..I..I.#",
                "#....<>....#",
                "#....##....#",
                "#..I..I..I.#",
                "#..........#",
                "#..........#",
                "#..I..I..I.#",
                "#..........#",
                "####oooo####",
            });

            // layers 1..4 — the repeated storey. A template is data, so a repeated floor is a
            // repeated block of rows, not a loop the generator has to understand.
            for (int storey = 1; storey <= 4; storey++)
            {
                rows.AddRange(new[]
                {
                    "#oo#oooo#oo#",
                    "#..........#",
                    "#..........#",
                    "#..I..I..I.#",
                    "#....<>....#",
                    "#....##....#",
                    "#..I..I..I.#",
                    "#..........#",
                    "#..........#",
                    "#..I..I..I.#",
                    "#..........#",
                    "#oo#oooo#oo#",
                });
            }

            // layer 5 — top storey, no stair above it
            rows.AddRange(new[]
            {
                "#oo#oooo#oo#",
                "#..........#",
                "#..........#",
                "#..I..I..I.#",
                "#..........#",
                "#..........#",
                "#..I..I..I.#",
                "#..........#",
                "#..........#",
                "#..I..I..I.#",
                "#..........#",
                "#oo#oooo#oo#",
            });

            return new TemplateDef
            {
                defName = "Shell_TowerTall",
                label = "residential tower",
                sizeX = 12,
                sizeZ = 12,
                bottomLayer = -1,
                topLayer = 5,
                stuff = "Steel",
                weight = 60,
                damageTolerance = 780,
                wallModuleId = "odyssey.module.wall.curtain",
                doorModuleId = "odyssey.module.door.lobby",
                windowModuleId = "odyssey.module.wall.glazed",
                stairModuleId = "odyssey.module.stair.core",
                slabModuleId = "odyssey.module.slab.deck",
                rows = rows,
            };
        }
    }
}
