#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    // What can be built, painted and ordered. Costs and work are Buildings.xml's in wood; stone is
    // ×1.7 work plus 15 and ×1.5 hit points (the StuffDefs). Deconstructing refunds half the cost
    // and half the parts, an odd unit going by a coin flip (DeconstructJob).
    public static partial class AlmanacCatalogue
    {
        /// <summary>The standing lines every ordered building shares: cost, work in wood and in stone, hit points.</summary>
        static (string, string)[] Built(int cost, int work, int hitPoints, params (string, string)[] more)
        {
            int stoneWork = work * 17 / 10 + 15;
            var rows = new List<(string, string)>
            {
                ("Cost", $"{cost} " + Lc("ui.res.wood") + " or " + Lc("ui.res.stone")),
                ("Work", $"{work:N0} in wood, {stoneWork:N0} in stone"),
            };
            if (hitPoints > 0) rows.Add(("Hit points", $"{hitPoints} in wood, {hitPoints * 3 / 2} in stone"));
            rows.AddRange(more);
            return rows.ToArray();
        }

        static IReadOnlyList<AlmanacEntry> StructureEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.arch.tool.wall", Structures, L("ui.arch.category.structure"), "Built",
                "A wall blocks movement and shots and closes a room. A slab resting on one has full support.",
                "Build › Structure", AlmanacAction.OpenBuild,
                Built(5, 135, 300, ("Cover", "Tall, 75%"), ("Holds up", "A slab, fully"), ("Refund", "Half the cost")),
                Effects("BEHAVIOUR",
                    "Walls are what make a room: an enclosed space holds its own temperature. They still pass some heat through their material — wood less than stone.",
                    ("Nobody walled in", "The last blow waits while someone crosses the cell, and moves anyone standing in it."),
                    ("Built on a wall", "A wall may stand on a wall, so a storey goes up course by course."),
                    ("Quality", "Walls take no quality roll.")),
                new[] { ("ui.arch.tool.door", "the way through"), ("ui.arch.tool.roof", "rests on it"), ("ui.skill.construction", "the skill that builds it") }),

            Keyed("ui.arch.tool.door", Structures, L("ui.arch.category.structure"), "Built",
                "A door lets colonists through and keeps a room closed. Bandits and hogs cannot open one; a bandit breaks it down.",
                "Build › Structure", AlmanacAction.OpenBuild,
                Built(5, 135, 160, ("Cover", "Tall, 75% while shut"), ("Opens for", "Colonists and rats"), ("Rotates", "Yes")),
                Effects("BEHAVIOUR",
                    "A shut door counts as wall for the room behind it and for shots. Open, it is no cover at all.",
                    ("Holds bandits", "A bandit treats a shut door as a wall and attacks it."),
                    ("Holds hogs", "A hog never opens a door; a rat does.")),
                new[] { ("ui.arch.tool.wall", "the wall it sits in"), ("ui.pawn.bandit", "breaks it down"), ("ui.pawn.rat", "opens it") }),

            Keyed("ui.arch.tool.roof", Structures, L("ui.arch.category.structure"), "Built",
                "An upper floor. It goes on a wall or reaches out from one, three cells at most beyond a support; with nothing holding it up, it falls.",
                "Build › Structure", AlmanacAction.OpenBuild,
                Built(4, 120, 0, ("Support", "4 over ground, rock, a wall or a pillar; 1 less per cell out"), ("Reach", "3 cells beyond a support"), ("Falls", "At support 0")),
                Effects("BEHAVIOUR",
                    "A slab is how a colony builds up. Its support is counted from whatever holds it: ground, rock, a wall or a pillar give 4, and each cell out loses one. A slab that reaches 0 collapses, leaving rubble on the floor below and dropping anyone standing on it.",
                    ("When it falls", "Rubble on the first floor below; anyone on it falls."),
                    ("Shaft rule", "A slab is refused over a ladder one or two cells below: the shaft must stay open.")),
                new[] { ("ui.arch.tool.wall", "holds it up"), ("ui.res.rubble", "what a collapse leaves"), ("ui.arch.tool.ladder", "needs a hole in it") }),

            Keyed("ui.arch.tool.pillar", Structures, L("ui.arch.category.structure"), "Not buildable yet",
                "A support pillar holds a slab up as a wall does. Pillars stand in the ruined city; one cannot be built yet.",
                "Not in the palette yet", AlmanacAction.None,
                new[] { ("Holds up", "A slab, fully, like a wall"), ("Build", "Not yet") },
                Effects("BEHAVIOUR", "The support solver counts a pillar as it counts a wall: a slab on it has full support."),
                new[] { ("ui.arch.tool.roof", "what it holds up"), ("ui.arch.tool.wall", "does the same today") }),

            Keyed("ui.arch.tool.deckplate", Structures, L("ui.arch.category.structure"), "Built",
                "A floor laid on ground a colonist already walks on. It never falls, because the ground holds it.",
                "Build › Structure, or Build › Floors", AlmanacAction.OpenBuild,
                Built(3, 60, 0, ("Needs", "Ground or a floor already there")),
                Effects("BEHAVIOUR",
                    "A floor covers the ground in wood or stone. It changes the look of a room, not the pace across it.",
                    ("On the ground", "Needs something to lay on; it cannot bridge a gap. That is the slab's work.")),
                new[] { ("ui.arch.tool.roof", "the floor that bridges") }),

            Keyed("ui.arch.tool.ladder", Structures, L("ui.arch.category.structure"), "Built",
                "One cell of rungs between one layer and the next. Cheap, slow to climb, and no hauler can carry a load up one.",
                "Build › Structure", AlmanacAction.OpenBuild,
                Built(4, 90, 80, ("Climb up", "540 per layer"), ("Climb down", "400 per layer"), ("Needs", "An open cell above it")),
                Effects("BEHAVIOUR",
                    "A ladder climbs through a hole in the floor above: the cell over it must stay open, and a slab poured there is refused. Colonists step off sideways on to the landing beside the shaft.",
                    ("Slow", "A climb costs over five flat steps."),
                    ("No hauling", "A hauler with a load cannot use a ladder, so materials do not go up one."),
                    ("Rats climb", "A rat takes ladders; a hog or a frog never does.")),
                new[] { ("ui.arch.tool.roof", "leave a hole in it"), ("ui.pawn.rat", "climbs it"), ("ui.arch.tool.stair", "not buildable yet") }),

            Keyed("ui.arch.tool.stair", Structures, L("ui.arch.category.structure"), "Not buildable yet",
                "A stair: two cells, and the fast way between layers. It is in the ruined city but cannot be built yet.",
                "Not in the palette yet", AlmanacAction.None,
                new[] { ("Footprint", "2 cells"), ("Build", "Not yet") },
                Effects("BEHAVIOUR", "Its chip sits dim in the Build palette until it arrives."),
                new[] { ("ui.arch.tool.ladder", "the way up today") }),

            Keyed("ui.arch.tool.sandbag", Structures, L("ui.arch.category.structure"), "Cover",
                "Low cover built of stone, dragged out as a line. Shots from beyond it are stopped more than half the time; it is climbed over, never stood on.",
                "Build › Security", AlmanacAction.OpenBuild,
                new[] {
                    ("Cost", "5 " + Lc("ui.res.stone") + ", always"), ("Work", "321 in stone"),
                    ("Hit points", "450"), ("Cover", "Low, 55%"),
                    ("Crossing", "+150 a step"), ("Standing on it", "Never"),
                },
                Effects("BEHAVIOUR",
                    "Low cover keeps its value against a shot coming in flat and loses it as the shot comes down more steeply, gone by 35 degrees. A defender behind sandbags crouches.",
                    ("A line", "Dragged as a joined run; a bag's neighbours shape it."),
                    ("Worn", "A shot the cover stops is fired into it, and wears it down."),
                    ("Wrecked", "Leaves a quarter of its cost when destroyed in a fight.")),
                new[] { ("ui.item.pistol", "what it stops"), ("ui.res.stone", "what it is made of"), ("ui.arch.tool.wall", "tall cover") }),
        };

        static IReadOnlyList<AlmanacEntry> FurnitureEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.arch.tool.bed", Furniture, Furniture, "Built",
                "A bed rests a sleeper faster than the ground and spares her the thought of sleeping on it. The only thing built with a quality roll.",
                "Build › Furniture", AlmanacAction.OpenBuild,
                Built(5, 180, 120, ("Footprint", "2 cells"), ("Rest", "85% to 140% by quality; the ground is 80%"), ("Cover", "30%")),
                Levels("A bed's quality is rolled when it is finished, from the builder's Construction skill. The ground gives 80% rest and a −40 thought for a quarter of a day.",
                    ("Poor", "85% rest", "Likeliest at skill 0 to 4"),
                    ("Normal", "100% rest", "Likeliest at skill 5 to 9"),
                    ("Decent", "112% rest", "Likeliest at skill 10 to 14"),
                    ("Uber", "125% rest", "Likeliest at skill 15 to 19"),
                    ("Epic", "140% rest", "Only at skill 20")),
                new[] { ("ui.need.rest", "what it restores"), ("ui.skill.construction", "sets its quality"), ("ui.work.rescue", "carries the downed to one") }),

            Keyed("ui.arch.tool.shelf", Furniture, Furniture, "Store",
                "One cell holding eight stacks off the floor. A store like a stockpile, numbered in the same series, with the same settings.",
                "Build › Furniture", AlmanacAction.OpenBuild,
                Built(5, 180, 100, ("Holds", "8 stacks"), ("Walking", "Passable"), ("Cover", "50%")),
                Effects("BEHAVIOUR",
                    "Haulers fill a shelf by its priority and its filter, as they would a stockpile. Colonists eat from shelves and builders take material from them.",
                    ("Emptied first", "A shelf with anything on it is never offered to a deconstructor; haulers clear it."),
                    ("Its pane", "Click it for the store's settings: priority, and what it accepts.")),
                new[] { ("ui.arch.tool.stockpile", "the painted store"), ("ui.work.hauling", "fills it") }),

            Keyed("ui.arch.tool.campfire", Furniture, Furniture, "Built",
                "A fire: warmth you can build. It needs no fuel, warms an enclosed room, and cooks — slowly — for a stick of wood a meal. The first one raised is the colony's hearth.",
                "Build › Furniture", AlmanacAction.OpenBuild,
                Built(3, 60, 60, ("Warms", "+26 °C at its own cell; a closed room besides"), ("Burns", "Nothing: it needs no fuel"), ("Cooks", "Twice as slow, burns half as often again, 1 " + Lc("ui.res.wood") + " a meal")),
                Effects("BEHAVIOUR",
                    "The hearth is where the home area is centred and where a raid heads. A campfire's pane says whether it is the hearth, or offers to make it so.",
                    ("Hearth", "The first raised; moved with Make this the hearth; not replaced if it is lost."),
                    ("Warmth", "Radiant heat at its own cell anywhere, and room heat only inside an enclosed room."),
                    ("Bills", "Takes up to five bills like a cooker.")),
                new[] { ("ui.overlay.home", "centred on the hearth"), ("ui.arch.tool.galley", "the faster cooker"), ("ui.res.wood", "burned per meal") }),
        };

        static IReadOnlyList<AlmanacEntry> ProductionEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.arch.tool.galley", Production, Production, "Powered",
                "The electric cooker works bills while it has power: a cook takes any raw food, 500 nutrition of it, and puts down a meal.",
                "Build › Production", AlmanacAction.OpenBuild,
                Built(15, 300, 100, ("Parts", "10 " + Lc("ui.res.scrap")), ("Draws", "350 W while on"), ("A meal", "300 ticks of work"), ("Bills", "Up to 5")),
                Effects("BILLS",
                    "Each bill says what to make and how many: until you have some, a set number, or forever. A power cut leaves the pan on the hob with its work kept.",
                    ("Burn", "A cook burns 30% of meals at level 0, 5% at 8 and none from 14."),
                    ("The pan", "Belongs to the station; a cook called away leaves it for the next.")),
                new[] { ("ui.res.meal.veg", "what it makes"), ("ui.work.cooking", "the work that runs it"), ("ui.arch.tool.generator", "powers it") }),
        };

        static IReadOnlyList<AlmanacEntry> PowerEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.arch.tool.generator", Power, Power, "Supply",
                "A wood-fired generator: 1,000 watts for the net it is joined to, burning wood in proportion to what the net draws.",
                "Build › Power", AlmanacAction.OpenBuild,
                Built(30, 600, 300, ("Parts", "20 " + Lc("ui.res.scrap")), ("Output", "1,000 W"), ("Hopper", "75 " + Lc("ui.res.wood")), ("Burns", "Up to 22 wood a day at full load")),
                Effects("BEHAVIOUR",
                    "A net is conduits joined face to face, in any direction. When what it draws is more than its generators give, the whole net goes dark. Haulers refill a hopper once it falls below half.",
                    (L("ui.alert.nofuel"), "An empty hopper stops it, and the alert says so."),
                    ("Switch", "Every powered building has an on/off switch on its pane.")),
                new[] { ("ui.arch.tool.conduit", "carries its power"), ("ui.res.wood", "its fuel"), ("ui.arch.tool.heater", "draws on it") }),

            Keyed("ui.arch.tool.heater", Power, Power, "Consumer",
                "An electric heater. While it is powered and switched on it warms the enclosed room it stands in.",
                "Build › Power", AlmanacAction.OpenBuild,
                Built(10, 240, 100, ("Parts", "5 " + Lc("ui.res.scrap")), ("Draws", "175 W"), ("Warms", "Its room, only while powered")),
                Effects("BEHAVIOUR",
                    "Heat goes into the room, so a heater outdoors does nothing. A cold room costs mood and sleep; a heater is the answer to the depths of Rime.",
                    ("Needs a room", "Walls, doors and a roof: an enclosed space.")),
                new[] { ("ui.arch.tool.generator", "powers it"), ("ui.arch.tool.campfire", "warmth without power"), ("ui.overlay.temperature", "shows the rooms") }),

            Keyed("ui.arch.tool.conduit", Power, Power, "Line",
                "A power line. Lines run anywhere — through walls, under floors, up shafts — and join face to face into a net.",
                "Build › Power", AlmanacAction.OpenBuild,
                new[] {
                    ("Cost", "1 " + Lc("ui.res.scrap") + " a cell"), ("Work", Work(40)),
                    ("Joins", "Face to face, up and down too; never diagonally"), ("Not through", "Rock, water or rubble"),
                    ("Removed by", L("ui.arch.tool.unwire")),
                },
                Effects("BEHAVIOUR",
                    "Lines are hidden except while a power tool is armed, a power building is selected or the Power overlay is on — and then they are drawn through everything. A building joins a line under or beside it.",
                    ("Nets", "Never bridged by a building: two nets touch only through a line."),
                    ("Taken up", "Only Remove conduit lifts a line; deconstruct never does.")),
                new[] { ("ui.arch.tool.generator", "feeds the net"), ("ui.res.scrap", "what it is made of"), ("ui.arch.tool.unwire", "takes it up") }),
        };

        static IReadOnlyList<AlmanacEntry> ZoneAndOrderEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.arch.tool.stockpile", ZonesAndOrders, "Zone", "Store",
                "Painted ground where haulers put things, one stack a cell. Click any cell of it for the store's settings.",
                "Build › Zones", AlmanacAction.OpenInventory,
                new[] {
                    ("Priority", "Last, Low, Normal, Preferred, Urgent"), ("New", "Normal, accepting everything"),
                    ("Filter", "By the six categories and each thing in them"), ("Merging", "Never: two zones stay two stores"),
                },
                Effects("BEHAVIOUR",
                    "A load goes to the best store that will take it. A store empties itself of anything it has been told to refuse, to a store that wants it or to open ground.",
                    ("Numbered", "Stockpiles and shelves share one series of numbers."),
                    ("Inventory", "The Inventory tab lists what every store holds.")),
                new[] { ("ui.arch.tool.shelf", "the built store"), ("ui.work.hauling", "fills it") }),

            Keyed("ui.arch.tool.growzone", ZonesAndOrders, "Zone", "Crop",
                "Painted ground sown with one crop — carrots, the only one — and re-sown after each harvest.",
                "Build › Zones", AlmanacAction.OpenBuild,
                new[] {
                    ("Crop", L("ui.terrain.carrot")), ("Needs", "Fertility 70, open sky, no building"),
                    ("Work type", L("ui.work.growing")),
                },
                Effects("BEHAVIOUR",
                    "Growers sow each empty cell, and harvest each crop at full growth. A cell under a roof or a slab, on water, or on ground poorer than grass is refused.",
                    ("Grass, not marsh", "Grass is 100 fertile; marsh, sand and subsoil are too poor.")),
                new[] { ("ui.terrain.carrot", "what it grows"), ("ui.skill.growing", "the skill that works it") }),

            Keyed("ui.overlay.home", ZonesAndOrders, "Area", "Derived",
                "The colony's home: everything it has placed, grown by five cells, and only the part joined to the hearth. It is worked out, not painted.",
                "The Home switch under Power; the Assign tab", AlmanacAction.OpenAssign,
                new[] {
                    ("Made of", "Buildings, floors, sites, zones, lines"), ("Grown by", "5 cells, square"),
                    ("Centre", "The hearth"), ("Without a hearth", "No home, and nobody kept in"),
                },
                Effects("BEHAVIOUR",
                    "On the Assign tab each colonist is Anywhere or Home. Kept Home, she takes no work outside it, eats outside it only when starving, and walks back when idle. A draft overrides it.",
                    ("Edge", "Drawn as a line, no wash, with a house over the hearth."),
                    ("Hearth", "The first campfire raised, or the one made so.")),
                new[] { ("ui.arch.tool.campfire", "the hearth"), ("ui.pawn.colonist", "who is kept in") }),

            Order("ui.arch.tool.mine",
                "Marks cells to be dug out: rock, ore, subsoil and grass, and rubble cleared.", L("ui.work.mining"),
                "A miner digs a marked cell only if she can get back out afterwards. Bedrock and the ground under a standing tree refuse the order.",
                new[] { ("ui.terrain.rock", "gives stone"), ("ui.res.ironore", "a seam it finds"), ("ui.skill.mining", "sets the pace") }),
            Order("ui.arch.tool.fell",
                "Marks trees to be felled for wood and bushes to be cleared out of the way.", L("ui.work.cutting"),
                "A felled tree topples and its wood lands at its foot. A cleared bush gives nothing.",
                new[] { ("ui.terrain.tree.birch", "a quick fell"), ("ui.terrain.bush", "cleared, not felled"), ("ui.skill.cutting", "sets the pace") }),
            Order("ui.arch.tool.harvest",
                "Marks a ripe berry bush to be picked. It grows its berries back in three days.", L("ui.work.growing"),
                "Allowed only on a ripe bush. The berries are put down beside it.",
                new[] { ("ui.terrain.bush.berry", "what it picks"), ("ui.res.berries", "what it gives") }),
            Order("ui.arch.tool.deconstruct",
                "Takes the colony's own building apart for half its materials.", L("ui.work.construction"),
                "Deconstructing takes as long as building did. A store with anything in it is emptied by haulers before a deconstructor is offered it.",
                new[] { ("ui.arch.tool.wall", "comes down for half") }),
            Order("ui.arch.tool.cancel",
                "Removes orders and building sites. A site gives back everything delivered to it.", "None",
                "Cancel lifts whatever order stands on a cell: a mark to dig or fell, a site, a deconstruct.",
                new[] { ("ui.arch.tool.deconstruct", "a different undo") }),
            Order("ui.arch.tool.unwire",
                "Takes a power line up, and nothing else in the cell. It gives back its scrap, or none, by a coin flip.", L("ui.work.construction"),
                "The only way to lift a line: Deconstruct never takes one.",
                new[] { ("ui.arch.tool.conduit", "what it takes up") }),
        };

        static AlmanacEntry Order(string key, string definition, string work,
            string paragraph, (string, string)[] related) =>
            Keyed(key, ZonesAndOrders, "Order", "Designation",
                definition, "Build › Orders", AlmanacAction.OpenBuild,
                new[] { ("Work type", work), ("Placed by", "A click or a drag"), ("Undone by", key == "ui.arch.tool.cancel" ? "Nothing" : L("ui.arch.tool.cancel")) },
                Effects("BEHAVIOUR", paragraph),
                related);
    }
}
