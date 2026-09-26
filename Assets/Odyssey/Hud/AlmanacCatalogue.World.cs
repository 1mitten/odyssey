#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    // Terrain and flora. Move costs are in hundredths of one flat step (MoveCost.Orthogonal = 100,
    // NaturalContent's cost classes); work is in ticks at full speed, 2,500 ticks to the game hour.
    // The played board is the barren default with MakeWooded() on top (ColonyWorld.DefFor), which
    // is why several grounds say they are not on it.
    public static partial class AlmanacCatalogue
    {
        /// <summary>A tick count of work as a player reads it: the number, and roughly how long at full speed.</summary>
        static string Work(int ticks)
        {
            int minutes = (int)System.Math.Round(ticks * 60.0 / 2500.0);
            string time = minutes < 1 ? "under a game minute"
                : minutes < 90 ? $"about {minutes} game min"
                : $"about {System.Math.Round(minutes / 60.0, 1)} game h";
            return $"{ticks:N0} ({time} at full speed)";
        }

        static IReadOnlyList<AlmanacEntry> TerrainEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.terrain.grass", Terrain, "Terrain", "Natural",
                Icon("grass", "#4d9e5a", "M4 20c2-6 5-11 8-16 3 5 6 10 8 16M8 20c1-4 3-7 5-10M16 20c-1-4-3-7-5-10"),
                "The living surface of the meadow. Every dry surface column on the board is grass, and every tree and bush stands on it.",
                "Covers the surface of the meadow", AlmanacAction.FindOnMap,
                new[] {
                    ("Walking", "Full pace (100%)"), ("Fertility", "100: carrots can be sown"),
                    ("Dig", Work(60) + ", yields nothing"), ("Build on", "Yes"),
                },
                Effects("BEHAVIOUR",
                    "Grass is the ground a colony is founded on. It costs nothing to cross, takes any building, and is fertile enough for a growing zone.",
                    ("Trees and bushes", "Grow only on grass; nothing else plants itself."),
                    ("Growing zones", "Fertility 100 is above the carrot's 70, so a zone on grass sows."),
                    (L("ui.res.mushrooms"), "Come up in the grass beside trees.")),
                new[] { ("ui.terrain.carrot", "sown into grass"), ("ui.terrain.tree.meadow", "grows on it"), ("ui.terrain.subsoil", "the two cells beneath") }),

            Keyed("ui.terrain.marsh", Terrain, "Terrain", "Natural",
                Icon("marsh", "#6b7d4a", "M3 18h18M5 18c0-4 1-7 2-9M9 18c0-3 0-6-1-8M15 18c0-3 1-6 2-8M19 18c0-4-1-7-2-9M3 14c3-1 5 1 8 0"),
                "Wet ground in a broken ring round the meadow's water. Slower to cross than grass and too poor to sow.",
                "One cell round streams and ponds, broken up", AlmanacAction.FindOnMap,
                new[] {
                    ("Walking", "140 per step: 71% pace"), ("Fertility", "40: too poor for carrots"),
                    ("Dig", Work(70) + ", yields nothing"), ("Build on", "Yes"),
                },
                Effects("BEHAVIOUR",
                    "Marsh fringes the water one cell deep, with gaps where the noise breaks it. Paths cross it when they must and prefer the grass beside it.",
                    ("Slow", "Each step onto marsh costs 140 against grass's 100."),
                    ("No trees", "Trees and bushes grow only on grass, so the marsh is open ground.")),
                new[] { ("ui.terrain.water.shallow", "the water it fringes"), ("ui.terrain.grass", "the faster ground beside it") }),

            Keyed("ui.terrain.water.shallow", Terrain, "Terrain", "Natural",
                Icon("water_shallow", "#3d7e9a", "M2 15c4-2 6 2 10 0s6 2 10 0M2 9c4-2 6 2 10 0s6 2 10 0"),
                "Streams and the edges of ponds, one layer down on a sand bed. People wade it at a third of their pace; animals never enter it.",
                "Streams, and within two cells of a pond's shore", AlmanacAction.FindOnMap,
                new[] {
                    ("Walking", "300 per step: 33% pace"), ("Jumping", "A one-cell stream is jumped for 200"),
                    ("Animals", "Never enter it"), ("Orders allowed", "None: nothing can be ordered on water"),
                    ("Build on", "No"), ("Sow", "No"),
                },
                Effects("BEHAVIOUR",
                    "Water is a body, not a lid: a colonist steps down into it and wades. A stream one cell wide is cheaper to jump than to wade, and colonists do.",
                    ("Wading", "Dropping in and climbing out costs 290 against a jump's 200."),
                    ("A short jump", "A jump can fall short and land in the water; carrying doubles the chance."),
                    ("Nothing built", "No bridge exists yet, so water is never built on.")),
                new[] { ("ui.terrain.water.deep", "further from the shore"), ("ui.terrain.sand", "the bed beneath"), ("ui.terrain.marsh", "the ring round it") }),

            Keyed("ui.terrain.water.deep", Terrain, "Terrain", "Natural",
                Icon("water_deep", "#255a78", "M2 8c4-2 6 2 10 0s6 2 10 0M2 13c4-2 6 2 10 0s6 2 10 0M2 18c4-2 6 2 10 0s6 2 10 0"),
                "The middle of a pond, more than two cells from the shore. Nobody enters it and paths go round.",
                "The middle of ponds", AlmanacAction.FindOnMap,
                new[] {
                    ("Walking", "Impassable"), ("Build on", "No"),
                    ("Orders allowed", "None"), ("Its bed", L("ui.terrain.packedgravel")),
                },
                Effects("BEHAVIOUR",
                    "Deep water is a wall to everyone. A colony on a pond's edge has one side it never needs to watch.",
                    ("Impassable", "No person or animal steps into it, and no path is planned through it.")),
                new[] { ("ui.terrain.water.shallow", "the wadeable edge"), ("ui.terrain.packedgravel", "the bed beneath") }),

            Keyed("ui.terrain.sand", Terrain, "Terrain", "Natural",
                Icon("sand", "#c9b27a", "M3 17c3-1 6-1 9 0s6 1 9 0M6 12h.01M11 10h.01M16 12h.01M9 14h.01M14 14h.01"),
                "Loose, barren ground. On the meadow it is the bed of every stream and pond edge.",
                "Under shallow water", AlmanacAction.FindOnMap,
                new[] {
                    ("Walking", "Full pace (100%)"), ("Fertility", "8: nothing grows"),
                    ("Dig", Work(70) + ", yields nothing"),
                },
                Effects("BEHAVIOUR",
                    "Sand is what a colonist stands on while wading. It is too barren for any crop.",
                    ("Riverbed", "Every shallow-water cell lies over sand.")),
                new[] { ("ui.terrain.water.shallow", "the water above it"), ("ui.terrain.packedgravel", "the deep water's bed") }),

            Keyed("ui.terrain.packedgravel", Terrain, "Terrain", "Natural",
                Icon("packed_gravel", "#7d7a74", "M4 17h16M6 14a2 1.5 0 1 0 4 0 2 1.5 0 1 0-4 0M13 13a2.5 1.5 0 1 0 5 0 2.5 1.5 0 1 0-5 0M9 10a2 1.5 0 1 0 4 0 2 1.5 0 1 0-4 0"),
                "Stony ground packed hard. Firm footing and poor soil. On the meadow it lies only under deep water.",
                "Under deep water", AlmanacAction.FindOnMap,
                new[] {
                    ("Walking", "Full pace (100%)"), ("Fertility", "30: too poor for carrots"),
                    ("Dig", Work(90) + ", yields nothing"),
                },
                Effects("BEHAVIOUR",
                    "The deep water's bed. A colonist meets it only where a pond has been drained or dug round.",
                    ("Poor soil", "Refused by a growing zone.")),
                new[] { ("ui.terrain.water.deep", "the water above it"), ("ui.terrain.sand", "the shallow water's bed") }),

            Keyed("ui.terrain.subsoil", Terrain, "Terrain", "Natural",
                Icon("subsoil", "#8a6b4b", "M2 8h20M2 14h20M5 11h.01M10 11h.01M15 11h.01M19 11h.01M7 17h.01M13 17h.01M18 17h.01"),
                "The band between the grass and the rock, two cells deep everywhere. Cheap to dig through.",
                "The two cells under the surface", AlmanacAction.FindOnMap,
                new[] {
                    ("Dig", Work(160) + ", yields nothing"), ("Fertility", "12"),
                    ("Order", L("ui.arch.tool.mine")), ("Work type", L("ui.work.mining")),
                },
                Effects("BEHAVIOUR",
                    "Digging down from the surface goes through two cells of subsoil before reaching rock. It is a quarter of rock's work and gives nothing back.",
                    ("Quick to dig", "160 against rock's 700.")),
                new[] { ("ui.terrain.grass", "the surface above"), ("ui.terrain.rock", "what lies below"), ("ui.arch.tool.mine", "how it is dug") }),

            Keyed("ui.terrain.rock", Terrain, "Terrain", "Natural",
                Icon("rock", "#87929e", "M4 18l3-11 9-3 5 7-2 7z M7 7l6 5 4-5"),
                "Natural stone: the band under the subsoil, the outcrops that break the surface, and the walls of the caverns. Mined, and every cell gives stone.",
                "Below the subsoil, and in outcrops at the surface", AlmanacAction.FindOnMap,
                new[] {
                    ("Mining work", Work(700)), ("Yields", "8 " + Lc("ui.res.stone") + " every cell"),
                    ("Order", L("ui.arch.tool.mine")), ("Work type", L("ui.work.mining")),
                    ("Holds up", "A slab resting on rock has full support"), ("Cover", "Tall, 75%"),
                },
                Effects("BEHAVIOUR",
                    "Rock is where stone comes from and where ore hides: iron and coal seams replace rock cells and look like rock until a mined cell beside them lays them open. A cut is only handed out if the miner can get back out afterwards.",
                    (L("ui.res.stone"), "Eight every cell, every time."),
                    ("Ore", "Mining a cell uncovers the six cells touching it; a seam shows only then."),
                    ("Caverns", "Sealed chambers inside the rock, three per ten thousand columns.")),
                new[] { ("ui.res.stone", "what it yields"), ("ui.res.ironore", "a seam inside it"), ("ui.res.coal", "a deeper seam"), ("ui.skill.mining", "the skill that digs it") }),

            Keyed("ui.terrain.bedrock", Terrain, "Terrain", "Natural",
                Icon("bedrock", "#4f5660", "M2 20h20M2 16h20M4 16l2-4 4 2 4-5 4 3 2-2v6"),
                "The floor of the world: the bottom two layers. It cannot be mined.",
                "The bottom two layers", AlmanacAction.None,
                new[] {
                    ("Can be mined", "Never"), ("Walking", "Solid"),
                    ("Holds up", "Everything above it"),
                },
                Effects("BEHAVIOUR",
                    "Digging stops at bedrock. The Mine order refuses it outright.",
                    ("Refused", "No order can be placed on a bedrock cell.")),
                new[] { ("ui.terrain.rock", "the stone above it"), ("ui.arch.tool.mine", "the order that stops here") }),

            Keyed("ui.res.rubble", Terrain, "Terrain", "Debris",
                Icon("rubble", "#8f8a82", "M3 19l3-4 3 2 3-5 4 3 5-2v6H3z M7 10l2-2 2 2-2 2z M15 8l2-1 1 2-2 1z"),
                "Broken masonry left where a floor fell in. It must be cleared before anything is built in its cell.",
                "Where a slab collapses", AlmanacAction.FindOnMap,
                new[] {
                    ("Clear", Work(90) + ", yields nothing"), ("Order", L("ui.arch.tool.mine")),
                    ("Walking", "Stood in, at full pace"), ("Build on", "Not until cleared"),
                },
                Effects("BEHAVIOUR",
                    "The meadow grows no rubble: it arrives when a slab loses its support and comes down on the first floor below. The Mine order clears it.",
                    ("Blocks building", "A site is refused on rubble."),
                    ("Nothing back", "Clearing gives no stone.")),
                new[] { ("ui.arch.tool.roof", "what falls to make it"), ("ui.arch.tool.mine", "the order that clears it") }),

            Keyed("ui.terrain.bareearth", Terrain, "Terrain", "Natural",
                Icon("bare_earth", "#8a6b4b", "M2 18c4-3 8-3 10 0 2-3 6-3 10 0M4 12c3-2 6-2 8 0 2-2 5-2 8 0"),
                "Soil with the grass worn off it. The meadow generator does not lay it today, so a colony will not meet it yet.",
                "Not on the meadow board", AlmanacAction.None,
                new[] {
                    ("Walking", "Full pace (100%)"), ("Fertility", "85: carrots can be sown"),
                    ("Dig", Work(60) + ", yields nothing"),
                },
                Effects("BEHAVIOUR", "Named so the pane can say it; the wooded meadow covers every dry column in grass."),
                new[] { ("ui.terrain.grass", "what covers it on the meadow") }),

            Keyed("ui.terrain.gravel", Terrain, "Terrain", "Natural",
                Icon("gravel", "#7d7a74", "M6 16a3 2 0 1 0 6 0 3 2 0 1 0-6 0M14 12a4 2.5 0 1 0 8 0 4 2.5 0 1 0-8 0M8 8a2.5 2 0 1 0 5 0 2.5 2 0 1 0-5 0"),
                "Loose gravel of the ruined city. A colonist stands in it rather than on it. The meadow has none.",
                "The ruined city only", AlmanacAction.None,
                new[] {
                    ("Walking", "Stood in, at full pace"), ("Fertility", "55: too poor for carrots"),
                    ("Dig", Work(70) + ", yields nothing"),
                },
                Effects("BEHAVIOUR", "Part of the ruined-city board, which is built and tested but is not the board a new game plays."),
                new[] { ("ui.terrain.packedgravel", "the meadow's gravel") }),
        };

        static IReadOnlyList<AlmanacEntry> FloraEntries() => new List<AlmanacEntry>
        {
            Tree("ui.terrain.tree.birch", "#e8e4d8", 650, 22,
                "A slim white-barked tree, more than half of the meadow's woodland. Quickest to fell, and the least wood."),
            Tree("ui.terrain.tree.meadow", "#3f8a4a", 900, 32,
                "A broad round-crowned tree of the open meadow, the common broadleaf of the woods."),
            Tree("ui.terrain.tree.fruit", "#8fae3d", 800, 28,
                "A low spreading tree. It bears no fruit yet: for now it is wood like the others."),
            Tree("ui.terrain.tree.giant", "#2f6a3a", 2400, 100,
                "A rare old giant, about one tree in a hundred. An hour's felling, and more wood than four meadow trees."),

            Keyed("ui.terrain.bush", Flora, "Flora", "Undergrowth",
                Icon("bush", "#4f8a3f", "M4 19h16M5 19c-2-3 0-7 3-7 0-3 3-5 5-4 2-2 6 0 5 3 3 0 4 5 1 8"),
                "Dense undergrowth among the trees. Slow to push through, and it must be cleared before anything is built in its cell.",
                "In the woods, away from the colony's start", AlmanacAction.FindOnMap,
                new[] {
                    ("Walking", "150 per step: 67% pace"), ("Clear", Work(250) + ", yields nothing"),
                    ("Order", L("ui.arch.tool.fell")), ("Work type", L("ui.work.cutting")),
                    ("Cover", "Low, 15%"),
                },
                Effects("BEHAVIOUR",
                    "Bushes grow on grass, never in or beside water, never at the foot of a terrace, and never within four cells of where the colony lands. One in six is a berry bush.",
                    ("Slow going", "Paths bend round thickets when there is a way round."),
                    ("In the way", "A building site is refused on a bush; Chop and clear takes it out.")),
                new[] { ("ui.terrain.bush.berry", "one bush in six"), ("ui.arch.tool.fell", "the order that clears it"), ("ui.terrain.grass", "the only ground it grows on") }),

            Keyed("ui.terrain.bush.berry", Flora, "Flora", "Undergrowth",
                Icon("berry_bush", "#a0304a", "M4 19h16M5 19c-2-3 0-7 3-7 0-3 3-5 5-4 2-2 6 0 5 3 3 0 4 5 1 8M9 14h.01M13 12h.01M15 15h.01"),
                "A bush that bears berries. Order a Harvest when it is ripe and a colonist picks eight; it grows them back in three days.",
                "One bush in six", AlmanacAction.FindOnMap,
                new[] {
                    ("Pick", Work(300)), ("Yields", "8 " + Lc("ui.res.berries")),
                    ("Grows back", "3 days"), ("Order", L("ui.arch.tool.harvest")),
                    ("Work type", L("ui.work.growing")), ("Walking", "67% pace, like any bush"),
                },
                Effects("BEHAVIOUR",
                    "Harvest is allowed only on a ripe bush. The berries are put down beside it, and the picked bush waits three days before it bears again. Clearing it with Chop and clear gives nothing.",
                    ("Picked", "A picked bush is named for it until the berries return."),
                    ("Trains", "Picking trains Growing.")),
                new[] { ("ui.res.berries", "what it bears"), ("ui.arch.tool.harvest", "the order that picks it"), ("ui.terrain.bush", "an ordinary bush") },
                alsoKeys: new[] { "ui.terrain.bush.picked" }),

            Keyed("ui.terrain.carrot", Flora, "Flora", "Crop",
                Icon("carrot_plant", "#ff8c3b", "M12 22V10 M8 10c0-3 4-6 4-6s4 3 4 6 M7 15h10"),
                "The one crop. Sown in a growing zone, it grows for about four days of daylight, is cut at full growth and sown again.",
                "Growing zones", AlmanacAction.FindOnMap,
                new[] {
                    ("Grows", "130,000 daylight ticks: about 4 days"), ("Sow", Work(170)),
                    ("Harvest work", Work(200)), ("Yields", "5 " + Lc("ui.res.carrots")),
                    ("Needs", "Fertility 70: grass, not marsh or sand"), ("Temperature", "None at 0 °C, full from 6 to 42 °C, none at 58 °C"),
                    ("Rain", "Up to +25% under open sky"), ("Work type", L("ui.work.growing")),
                },
                Effects("BOTANY",
                    "A carrot grows only in the day's growing window, from the sixth hour to the nineteenth, and only when it is warm enough. A harvested cell is left fallow and the next sowing pass finds it again.",
                    ("Cold stops it", "Nothing grows at 0 °C or below; growth climbs to full speed at 6 °C."),
                    ("Heat stops it", "Growth falls away above 42 °C and stops at 58 °C."),
                    ("Rain helps", "An exposed crop grows up to a quarter faster in a downpour.")),
                new[] { ("ui.res.carrots", "the harvest"), ("ui.arch.tool.growzone", "where it is sown"), ("ui.skill.growing", "the skill that works it") }),
        };

        static AlmanacEntry Tree(string key, string colour, int work, int wood, string definition) =>
            Keyed(key, Flora, "Flora", "Tree",
                Icon(key.Substring(key.LastIndexOf('.') + 1) + "_tree", colour,
                    "M12 2c-4 0-7 3-7 7 0 3 2 5 4 6h6c2-1 4-3 4-6 0-4-3-7-7-7z M12 15v7 M9 22h6"),
                definition,
                "The woods of the meadow", AlmanacAction.FindOnMap,
                new[] {
                    ("Fell", Work(work)), ("Yields", $"{wood} " + Lc("ui.res.wood")),
                    ("Order", L("ui.arch.tool.fell")), ("Work type", L("ui.work.cutting")),
                    ("Walking", "Walked past at full pace"), ("Cover", "Tall, 25%"),
                },
                Effects("BEHAVIOUR",
                    "Trees stand on grass and do not grow or spread. Chop and clear fells one for wood; it topples and the wood lands at its foot. The ground under a standing tree cannot be dug, zoned or built on.",
                    ("Woodland", "About a quarter of grass cells carry a tree before clumping; 58% of them are birches."),
                    ("Cover", "A trunk stops a quarter of shots aimed past it."),
                    (L("ui.res.mushrooms"), "Come up beside trees, and more appear as they are eaten.")),
                new[] { ("ui.res.wood", "what it yields"), ("ui.arch.tool.fell", "the order that fells it"), ("ui.skill.cutting", "the skill that fells it"), ("ui.res.mushrooms", "found beside it") });
    }
}
