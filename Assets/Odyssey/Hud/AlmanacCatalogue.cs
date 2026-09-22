#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    public static class AlmanacKeys
    {
        public static string Wood => Registry.Label("ui.res.wood");
        public static string Stone => Registry.Label("ui.res.stone");
        public static string Coal => Registry.Label("ui.res.coal");
        public static string Carrots => Registry.Label("ui.res.carrots");
        public static string Scrap => Registry.Label("ui.res.scrap");
        public static string PlantMatter => Registry.Label("ui.res.plantmatter");
        public static string Insulation => Registry.Label("ui.res.insulation");
        public static string Wall => Registry.Label("ui.arch.tool.wall");
        public static string Door => Registry.Label("ui.arch.tool.door");
        public static string Ladder => Registry.Label("ui.arch.tool.ladder");
        public static string Bed => Registry.Label("ui.arch.tool.bed");
        public static string Stair => Registry.Label("ui.arch.tool.stair");
        public static string Mine => Registry.Label("ui.arch.tool.mine");
        public static string Mining => Registry.Label("ui.status.mining");
        public static string Structure => Registry.Label("ui.arch.category.structure");
        public static string Furniture => Registry.Label("ui.arch.category.furniture");
        public static string Salvage => Registry.Label("ui.arch.category.salvage");
        public static string Materials => Registry.Label("ui.res.category.materials");
        public static string Items => Registry.Label("ui.res.category.items");
        public static string Food => Registry.Label("ui.res.category.food");
    }

    public sealed class AlmanacIcon
    {
        public string Name { get; }
        public string Colour { get; }
        public string Path { get; }

        public AlmanacIcon(string name, string colour, string path)
        {
            Name = name;
            Colour = colour;
            Path = path;
        }
    }

    public sealed class AlmanacBody
    {
        public string Type { get; }
        public string SectionLabel { get; }
        public string Paragraph { get; }
        public IReadOnlyList<(string Title, string Kind, string Text)>? Cards { get; }
        public IReadOnlyList<(string Range, string Name, string Note)>? Levels { get; }
        public IReadOnlyList<(string Title, string Value, string Detail)>? Specs { get; }
        public IReadOnlyList<(string Label, string Desc)>? Effects { get; }

        public AlmanacBody(
            string type,
            string sectionLabel,
            string paragraph,
            IReadOnlyList<(string Title, string Kind, string Text)>? cards = null,
            IReadOnlyList<(string Range, string Name, string Note)>? levels = null,
            IReadOnlyList<(string Title, string Value, string Detail)>? specs = null,
            IReadOnlyList<(string Label, string Desc)>? effects = null)
        {
            Type = type;
            SectionLabel = sectionLabel;
            Paragraph = paragraph;
            Cards = cards;
            Levels = levels;
            Specs = specs;
            Effects = effects;
        }
    }

    public sealed class AlmanacEntry
    {
        public string Name { get; }
        public string CategoryName { get; }
        public string Summary { get; }
        public string TypeChip { get; }
        public string NatureChip { get; }
        public bool HasColor { get; }
        public string Color { get; }
        public AlmanacIcon Icon { get; }
        public string Definition { get; }
        public string LiveState { get; }
        public string PrimaryAction { get; }
        public IReadOnlyList<(string Key, string Value)> Properties { get; }
        public AlmanacBody Body { get; }
        public IReadOnlyList<(string Name, string Reason)> Related { get; }

        public AlmanacEntry(
            string name,
            string categoryName,
            string summary,
            string typeChip,
            string natureChip,
            bool hasColor,
            string color,
            AlmanacIcon icon,
            string definition,
            string liveState,
            string primaryAction,
            IReadOnlyList<(string Key, string Value)> properties,
            AlmanacBody body,
            IReadOnlyList<(string Name, string Reason)> related)
        {
            Name = name;
            CategoryName = categoryName;
            Summary = summary;
            TypeChip = typeChip;
            NatureChip = natureChip;
            HasColor = hasColor;
            Color = color;
            Icon = icon;
            Definition = definition;
            LiveState = liveState;
            PrimaryAction = primaryAction;
            Properties = properties;
            Body = body;
            Related = related;
        }
    }

    public sealed class AlmanacCategory
    {
        public string Name { get; }
        public string IconPath { get; }
        public IReadOnlyList<AlmanacEntry> Entries { get; }

        public AlmanacCategory(string name, string iconPath, IReadOnlyList<AlmanacEntry> entries)
        {
            Name = name;
            IconPath = iconPath;
            Entries = entries;
        }
    }

    /// <summary>
    /// Content catalogue for the Odyssey Almanac.
    /// Fully curated reference of all terrain, materials, structures, items, skills,
    /// work types, needs, traits, health conditions, fauna, flora, and events in the game.
    /// </summary>
    public static class AlmanacCatalogue
    {
        public static readonly IReadOnlyList<AlmanacCategory> Categories = BuildCategories();

        static readonly Dictionary<string, AlmanacCategory> CategoriesByName = new(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, AlmanacEntry> EntriesByName = new(StringComparer.OrdinalIgnoreCase);

        static AlmanacCatalogue()
        {
            foreach (AlmanacCategory cat in Categories)
            {
                CategoriesByName[cat.Name] = cat;
                foreach (AlmanacEntry entry in cat.Entries)
                    EntriesByName[entry.Name] = entry;
            }
        }

        public static AlmanacCategory? GetCategory(string name) =>
            CategoriesByName.TryGetValue(name, out AlmanacCategory? cat) ? cat : null;

        public static AlmanacEntry? GetEntry(string name) =>
            EntriesByName.TryGetValue(name, out AlmanacEntry? entry) ? entry : null;

        public static int TotalEntriesCount
        {
            get
            {
                int count = 0;
                foreach (AlmanacCategory cat in Categories) count += cat.Entries.Count;
                return count;
            }
        }

        static IReadOnlyList<AlmanacCategory> BuildCategories()
        {
            var list = new List<AlmanacCategory>();

            // 1. Terrain
            list.Add(new AlmanacCategory("Terrain", "M2 18l5-10 6 5 9-9v14H2z", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Grass", "Terrain", "Fast walking meadow cover, pasture base", "Terrain", "Natural", true, "#4d9e5a",
                    new AlmanacIcon("grass", "#4d9e5a", "M4 20c2-6 5-11 8-16 3 5 6 10 8 16M8 20c1-4 3-7 5-10M16 20c-1-4-3-7-5-10"),
                    "The standard surface vegetation of the meadow plateau. Supports foot traffic and sows readily into cultivated zones.",
                    "Seen on this map · 1,284 tiles · surface layer", "Find on map",
                    new[] {
                        ("Move cost", "100% (Normal)"), ("Fertility", "100%"), ("Work to clear", "150 ticks"),
                        ("Cleanliness", "Neutral (0)"), ("Supports building", "Light & Heavy"),
                        ("Sub-layer", "Soil"), ("Drop on clear", AlmanacKeys.PlantMatter), ("Flammability", "Low (15%)")
                    },
                    new AlmanacBody("terrain", "BEHAVIOUR",
                        "Grass covers the vast majority of the upper clearing. Colonists cross it at baseline pace without foot penalty. Sustained heavy traffic will compress it over seasons into packed pathways.",
                        cards: new[] {
                            ("Best for", "best", "Rapid colony transit lines, outdoor stockpiles, grazing animals and surface farming plots."),
                            ("Avoid for", "avoid", "Medical sterile bays and enclosed high-tech electronics storage.")
                        }),
                    new[] { ("Soil", "sub-layer directly beneath"), ("Bare Earth", "what grass becomes under heavy traffic"), ("Carrot Plant", "optimal crop sown into grass clearings") }
                ),
                new AlmanacEntry(
                    "Soil", "Terrain", "Rich loam underlying meadow turf", "Terrain", "Natural", true, "#8a6b4b",
                    new AlmanacIcon("soil", "#8a6b4b", "M2 18c4-3 8-3 10 0 2-3 6-3 10 0M4 12c3-2 6-2 8 0 2-2 5-2 8 0"),
                    "Unvegetated topsoil formed by generations of decaying river silt and botanical compost.",
                    "Seen on this map · 412 tiles · surface layer", "Find on map",
                    new[] {
                        ("Move cost", "105% (Slight drag)"), ("Fertility", "100%"), ("Work to clear", "120 ticks"),
                        ("Cleanliness", "-0.2"), ("Supports building", "Light & Heavy"),
                        ("Sub-layer", "Subsoil"), ("Moisture", "Moderate"), ("Flammability", "None (0%)")
                    },
                    new AlmanacBody("terrain", "BEHAVIOUR",
                        "Exposed loam is slightly softer underfoot than grass. Readily takes wooden support foundations, paving slabs, and tilled furrow lines.",
                        cards: new[] {
                            ("Best for", "best", "Excavation, agricultural zones, foundation leveling."),
                            ("Avoid for", "avoid", "Unpaved interior corridors subject to dust tracking.")
                        }),
                    new[] { ("Grass", "overlying natural turf"), ("Subsoil", "dense substrate stratum below"), ("Gravel", "coarser stone soil mixture") }
                ),
                new AlmanacEntry(
                    "Rock", "Terrain", "Solid subterranean bedrock seam", "Terrain", "Natural", true, "#87929e",
                    new AlmanacIcon("rock", "#87929e", "M4 18l3-11 9-3 5 7-2 7z M7 7l6 5 4-5"),
                    "Dense natural stone formation. Impassable until excavated with pickaxes, leaving rough stone piles.",
                    "Seen on this map · 3,410 tiles · subterranean layers", "Find on map",
                    new[] {
                        ("Move cost", "Impassable (Solid)"), ("Fertility", "0%"), ("Work to clear", "700 ticks (" + AlmanacKeys.Mining + ")"),
                        ("Cleanliness", "Neutral (0)"), ("Supports building", "Overhead Ceiling"),
                        ("Drop on clear", AlmanacKeys.Stone), ("Collapse risk", "Zero (Self-supporting)"), ("Flammability", "None (0%)")
                    },
                    new AlmanacBody("terrain", "BEHAVIOUR",
                        "Rock formations form the mountain flanks and sealed underground chambers. Requires dedicated mining labour to clear, yielding valuable stone chunks.",
                        cards: new[] {
                            ("Best for", "best", "Deep defensive bunkers, overhead vault roofs, rock chunk harvest."),
                            ("Avoid for", "avoid", "Rapid tunnel expansion without high-skill miners.")
                        }),
                    new[] { (AlmanacKeys.Stone, "quarried material chunk"), (AlmanacKeys.Mining, "governing excavation skill"), ("Bedrock", "unbreakable base stone layer") }
                ),
                new AlmanacEntry(
                    "Gravel", "Terrain", "Coarse loose aggregate and pebbles", "Terrain", "Natural", true, "#7d7a74",
                    new AlmanacIcon("gravel", "#7d7a74", "M6 16a3 2 0 1 0 6 0 3 2 0 1 0-6 0M14 12a4 2.5 0 1 0 8 0 4 2.5 0 1 0-8 0M8 8a2.5 2 0 1 0 5 0 2.5 2 0 1 0-5 0"),
                    "Loose fragmented stones mixed with coarse grit. Poor drainage and low agricultural yield.",
                    "Seen on this map · 295 tiles · riverside banks", "Find on map",
                    new[] {
                        ("Move cost", "110% (Crunch)"), ("Fertility", "55% (Barren)"), ("Work to clear", "110 ticks"),
                        ("Cleanliness", "-0.1"), ("Supports building", "Light only"),
                        ("Sub-layer", "Rock"), ("Moisture", "Dry"), ("Flammability", "None (0%)")
                    },
                    new AlmanacBody("terrain", "BEHAVIOUR",
                        "Gravel forms naturally around terrace steps, river bends, and rock edges. It refuses most crops due to thin nutrient holding.",
                        cards: new[] {
                            ("Best for", "best", "Perimeter drainage trenches, firebreaks."),
                            ("Avoid for", "avoid", "Cereal or root vegetable crop farming.")
                        }),
                    new[] { ("Rock", "parent mineral source"), ("Soil", "richer loam counterpart"), ("Packed Gravel", "compacted roadway derivation") }
                ),
                new AlmanacEntry(
                    "Pavement", "Terrain", "Smooth engineered concrete slabs", "Terrain", "Constructed", true, "#929ca8",
                    new AlmanacIcon("pavement", "#929ca8", "M3 3h8v8H3z M13 3h8v8h-8z M3 13h8v8H3z M13 13h8v8h-8z"),
                    "Manufactured durable flags laid over prepared substrate. Maximises transit velocity and eliminates indoor dirt accumulation.",
                    "Seen on this map · 64 tiles · ruins quadrant", "Find on map",
                    new[] {
                        ("Move cost", "85% (High speed)"), ("Fertility", "0%"), ("Work to place", "80 ticks"),
                        ("Cleanliness", "+0.4 (Sterile baseline)"), ("Supports building", "All categories"),
                        (AlmanacKeys.Materials, "Concrete × 2"), ("Wear resistance", "Indefinite"), ("Flammability", "None (0%)")
                    },
                    new AlmanacBody("terrain", "BEHAVIOUR",
                        "Installed corridor floors grant colonist haulers a 15% speed increase over natural terrain, noticeably compounding colony freight throughput.",
                        cards: new[] {
                            ("Best for", "best", "Central thoroughfares, hospital suites, kitchen prep counters."),
                            ("Avoid for", "avoid", "Outdoor expansion prior to essential fortress masonry.")
                        }),
                    new[] { ("Concrete", "primary construction component"), ("Construct", "assembly labour category"), (AlmanacKeys.Wood, "cheaper early floor alternate") }
                ),
                new AlmanacEntry(
                    "Shallow Water", "Terrain", "Ankle-deep freshwater river shallows", "Terrain", "Natural", true, "#3d7e9a",
                    new AlmanacIcon("water_shallow", "#3d7e9a", "M2 15c4-2 6 2 10 0s6 2 10 0M2 9c4-2 6 2 10 0s6 2 10 0"),
                    "Slow-moving stream bed with gravel bottom. Wadeable by pawns with noticeable drag penalty.",
                    "Seen on this map · 180 tiles · river canyon", "Find on map",
                    new[] {
                        ("Move cost", "145% (Heavy drag)"), ("Fertility", "0%"), ("Work to clear", "Impassable"),
                        ("Cleanliness", "Neutral (0)"), ("Supports building", "Bridge only"),
                        ("Sub-layer", "Riverbed Silt"), ("Fish abundance", "Moderate"), ("Flammability", "None (0%)")
                    },
                    new AlmanacBody("terrain", "BEHAVIOUR",
                        "Shallow water drastically decelerates approaching threats, making natural river loops premier defensive engagement zones.",
                        cards: new[] {
                            ("Best for", "best", "Killbox deceleration channels, water wheel power sites."),
                            ("Avoid for", "avoid", "Main colony commuting paths without raised bridges.")
                        }),
                    new[] { ("Deep Water", "drowning hazard channel"), ("Gravel", "adjoining bank perimeter"), ("Grass", "surrounding basin bank") }
                )
            }));

            // 2. Materials
            list.Add(new AlmanacCategory(AlmanacKeys.Materials, "M19 7l-7-4-7 4v10l7 4 7-4V7z", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    AlmanacKeys.Wood, AlmanacKeys.Materials, "Versatile organic timber harvested from pines", "Material", "Organic", true, "#a26e3c",
                    new AlmanacIcon("wood", "#a26e3c", "M4 6h16M4 12h16M4 18h16M7 3v18M17 3v18"),
                    "Hewn coniferous logs suitable for framing, early furniture, fuel stockpiles, and perimeter stakes.",
                    "Stocked in colony · 420 units · Main store", "View stockpile",
                    new[] {
                        ("Weight", "0.4 kg/unit"), ("Max stack", "75"), ("Work to build", "100% (Baseline)"),
                        ("Beauty", "+0.0"), ("Flammability", "High (100%)"),
                        ("Base HP factor", "100% (Weak)"), (AlmanacKeys.Insulation, "Moderate"), ("Market value", "1.20 silver")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Timber is the bedrock of colony establishment. Highly combustible, it must be rapidly phased out of defensive lines once stone masonry is unlocked.",
                        specs: new[] {
                            ("Harvest source", "Pine Tree", "Chop yield: 25–40 logs"),
                            ("Burn fuel value", "8 hours/log", "Consumable in campfire & stove"),
                            ("Structure HP", "120 HP / " + AlmanacKeys.Wall.ToLowerInvariant() + " unit", "Vulnerable to raider torch attacks")
                        }),
                    new[] { ("Pine", "living timber provider"), ("Construct", "crafting skill used in timber assemblies"), (AlmanacKeys.Wall, "basic protective enclosure") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Stone, AlmanacKeys.Materials, "Quarried rock chunks dressed into blocks", "Material", "Mineral", true, "#7d8590",
                    new AlmanacIcon("stone", "#7d8590", "M3 8l9-5 9 5v8l-9 5-9-5V8z"),
                    "Heavy mineral blocks split from excavated mountain boulders. Fireproof and resistant to sustained blunt force.",
                    "Stocked in colony · 260 units · Masonry yard", "View stockpile",
                    new[] {
                        ("Weight", "1.0 kg/unit"), ("Max stack", "75"), ("Work to build", "450% (Slow)"),
                        ("Beauty", "+0.2"), ("Flammability", "None (0%)"),
                        ("Base HP factor", "420% (Stout)"), (AlmanacKeys.Insulation, "High thermal mass"), ("Market value", "1.90 silver")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Stone barriers cannot catch fire and possess fourfold the impact resistance of timber. Demands substantial colonist hauling and cutting labour.",
                        specs: new[] {
                            ("Harvest source", "Rock Seams", "Excavated by mining operations"),
                            ("Fire resistance", "Complete immunity", "Essential for defensive blast perimeters"),
                            ("Structure HP", "500 HP / " + AlmanacKeys.Wall.ToLowerInvariant() + " unit", "Optimal outer fortress bastion")
                        }),
                    new[] { ("Rock", "unworked raw mineral deposit"), (AlmanacKeys.Mining, "quarrying labor skill"), (AlmanacKeys.Wall, "masonry fortified barricade") }
                ),
                new AlmanacEntry(
                    "Concrete", AlmanacKeys.Materials, "Poured artificial aggregate binder", "Material", "Manufactured", true, "#939ba6",
                    new AlmanacIcon("concrete", "#939ba6", "M4 4h16v16H4z M9 4v16 M15 4v16 M4 10h16 M4 15h16"),
                    "Composite mixture of sand, crushed aggregate, and calcined lime slurry. Cures into rigid non-porous floor flagstones.",
                    "Stocked in colony · 85 units · Chemical store", "View stockpile",
                    new[] {
                        ("Weight", "0.8 kg/unit"), ("Max stack", "75"), ("Work to build", "200%"),
                        ("Cleanliness", "High (+0.4)"), ("Flammability", "None (0%)"),
                        ("Cure time", "Instantaneous"), ("Water resistance", "Immune"), ("Market value", "2.10 silver")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Ideal for rapid paved pathway deployment and sterile surgical flooring. Provides pristine floor surfaces without the high labor investment of cut marble.",
                        specs: new[] {
                            ("Speed bonus", "+15% over walk baseline", "Cuts cross-settlement round-trip delays"),
                            ("Sterility factor", "Superior cleanliness", "Prevents surgical sepsis risks"),
                            ("Structure HP", "350 HP / unit", "Reliable bunker subfloor")
                        }),
                    new[] { ("Pavement", "primary finished surface application"), ("Gravel", "raw aggregate component"), (AlmanacKeys.Salvage, "alternative scrap re-binder") }
                ),
                new AlmanacEntry(
                    "Steel", AlmanacKeys.Materials, "Industrial alloy sheets salvaged from hull plating", "Material", "Metal", true, "#5c7080",
                    new AlmanacIcon("steel", "#5c7080", "M3 7l4-4h10l4 4v10l-4 4H7l-4-4V7z"),
                    "High-tensile refined metallic alloy. Critical for power conduits, automated security turrets, and reinforced blast doors.",
                    "Stocked in colony · 190 units · Secure vault", "View stockpile",
                    new[] {
                        ("Weight", "0.5 kg/unit"), ("Max stack", "75"), ("Work to build", "180%"),
                        ("Conductivity", "High"), ("Flammability", "Low (20%)"),
                        ("Base HP factor", "300%"), ("Tensile strength", "Maximum"), ("Market value", "3.80 silver")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Steel is indispensable for tech progression. Sourced primarily from scrap smelters and deep vein ore extractors.",
                        specs: new[] {
                            ("Reinforcement HP", "380 HP / bulkhead unit", "Withstands sustained siege ramming"),
                            ("Electrical duty", "Standard conductor", "Required for power generators and wiring"),
                            ("Melting point", "1450°C", "Resists external grassfire heat fronts")
                        }),
                    new[] { ("Iron Ore", "unrefined smelter feed"), (AlmanacKeys.Door, "reinforced portal upgrade"), (AlmanacKeys.Salvage, "surface wreck metal salvage") }
                )
            }));

            // 3. Structures
            list.Add(new AlmanacCategory("Structures", "M3 21h18M5 21V7l7-4 7 4v14", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    AlmanacKeys.Wall, "Structures", "Opaque barrier sealing indoor temperature and security", AlmanacKeys.Structure, "Fabricated", false, "#7fd0e0",
                    new AlmanacIcon("wall", "#7fd0e0", "M3 3h18v18H3z M3 9h18 M3 15h18 M9 3v6 M15 3v6 M6 9v6 M12 9v6 M18 9v6 M9 15v6 M15 15v6"),
                    "Primary structural component dividing rooms, insulating against weather extremes, and stopping raiders.",
                    "Constructed · 142 segments in colony", "Inspect structures",
                    new[] {
                        ("Build cost", "5× Material"), ("Passability", "Impassable"), ("Cover provided", "75% (Full wall)"),
                        ("Light transmission", "0% (Opaque)"), ("Roof support span", "Up to 6 tiles"),
                        ("Thermal insulation", "High (0.85)"), ("Work to build", "240 ticks"), ("Deconstruct return", "75% of material")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Walls retain room warmth during cold snaps and define private colonist bedroom boundaries. Ensure regular pillar support to prevent roof cave-ins.",
                        specs: new[] {
                            ("Roofing radius", "6 tiles from wall edge", "Prevents collapse crushing injuries"),
                            ("Temperature barrier", "Reduces heat bleed by 85%", "Cuts indoor heater electric load"),
                            ("Cover value", "75% missile interception", "High defensive value in flanking bastions")
                        }),
                    new[] { (AlmanacKeys.Door, "entryway portal companion"), (AlmanacKeys.Stone, "recommended perimeter composition"), (AlmanacKeys.Wood, "early combustible frame") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Door, "Structures", "Controlled passage allowing colonist transit while sealing rooms", AlmanacKeys.Structure, "Fabricated", false, "#7fd0e0",
                    new AlmanacIcon("door", "#7fd0e0", "M5 3h14v18H5z M15 12h2"),
                    "Hinged portal allowing authorized pawns through while barring wildlife, hostile attackers, and freezing air drafts.",
                    "Constructed · 18 doors in colony", "Inspect structures",
                    new[] {
                        ("Build cost", "25× Material"), ("Open speed", "Wood: 0.4s · Stone: 1.2s"), ("Lockable", "Yes (Forbidden flag)"),
                        ("Thermal bleed", "Minimal when closed"), ("Hit points", "Wood: 150 · Steel: 380"),
                        ("Work to build", "400 ticks"), ("Power consumer", "No (Manual)"), ("Flammability", "Material dependent")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Stone doors are very tough but open sluggishly, causing colonist travel bottlenecks. Wood or steel doors are superior for high-frequency corridors.",
                        specs: new[] {
                            ("Transit penalty", "Wood: none · Stone: slight delay", "Use wood for kitchen/freezer passages"),
                            ("Holding capability", "Repels wild predators", "Prevents starving duct rats raiding larder"),
                            ("Remote lock", "Player toggleable", "Enables colony lockdown during raids")
                        }),
                    new[] { (AlmanacKeys.Wall, "enclosing partition frame"), (AlmanacKeys.Wood, "fast-opening interior door stock"), ("Steel", "blast-resistant security door") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Ladder, "Structures", "Vertical transit link between vertical world layers", AlmanacKeys.Structure, "Fabricated", false, "#7fd0e0",
                    new AlmanacIcon("ladder", "#7fd0e0", "M7 2v20 M17 2v20 M7 6h10 M7 11h10 M7 16h10"),
                    "Rigid rung frame connecting an upper tile directly to the cell immediately below it.",
                    "Constructed · 4 ladders active", "Inspect structures",
                    new[] {
                        ("Build cost", "15× Wood or Steel"), ("Passability", "Passable (Vertical link)"), ("Transit speed", "80% baseline"),
                        ("Footprint", "1×1 cell"), ("Layer connection", "Directly adjacent Y±1"),
                        ("Fall risk", "None (Safety grips)"), ("Work to build", "320 ticks"), ("Cleanliness", "Neutral")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Essential for multi-layer operations. Colonists utilize ladders to haul mined iron ore from subterranean shafts up to surface workshops.",
                        specs: new[] {
                            ("Vertical capacity", "Bidirectional single-file", "Pawns yield to climbing haulers"),
                            ("Shaft requirement", "Clear vertical cell pair", "Both top and bottom must be walkable"),
                            ("Mining synergy", "Allows rapid deep shaft sinking", "Enables safe exploration of deep veins")
                        }),
                    new[] { (AlmanacKeys.Wall, "support anchor backing"), (AlmanacKeys.Mining, "shaft exploration skill"), (AlmanacKeys.Wood, "lightweight vertical structure") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Bed, "Structures", "Colonist sleeping station restoring rest and mood", AlmanacKeys.Structure, AlmanacKeys.Furniture, false, "#7fd0e0",
                    new AlmanacIcon("bed", "#7fd0e0", "M3 7v11M21 7v11M3 13h18M3 9h8a2 2 0 0 1 2 2v2H3z"),
                    "Elevated mattress frame ensuring peaceful night sleep, rapid rest recovery, and immunity to damp ground penalties.",
                    "Constructed · 8 beds in colony", "Inspect furniture",
                    new[] {
                        ("Build cost", "45× Wood"), ("Rest effectiveness", "100% (Standard)"), ("Comfort", "0.75 (Restful)"),
                        ("Owner assignment", "Personal / Medical / Shared"), ("Footprint", "1×2 cells"),
                        ("Room quality bonus", "Appreciated furniture"), ("Work to build", "350 ticks"), ("Flammability", "High (80%)")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Colonists sleeping on the ground suffer immediate mood debuffs and recover rest 30% slower. Proper bed placement in private quarters is essential.",
                        specs: new[] {
                            ("Rest recovery rate", "1.00× multiplier", "Full night rest achieved in 7.2 hours"),
                            ("Medical use", "Togglable medical designation", "Increases surgical success chance by 10%"),
                            ("Comfort contribution", "+4 colonist mood buff", "Prevents mental break thresholds")
                        }),
                    new[] { ("Rest", "primary physiological need recovered"), (AlmanacKeys.Wood, "standard furniture material"), ("Construct", "assembly skill category") }
                )
            }));

            // 4. Items
            list.Add(new AlmanacCategory(AlmanacKeys.Items, "M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Ration Pack", AlmanacKeys.Items, "Preserved long-life emergency calorie packet", "Item", "Manufactured", false, "#d4af37",
                    new AlmanacIcon("ration_pack", "#d4af37", "M5 4h14v16H5z M5 9h14 M9 4v5"),
                    "Vacuum-sealed industrial food concentrate. Will not rot even in tropical humidity, making it the ideal emergency pantry buffer.",
                    "Stocked · 84 packs in pantry", "View stockpile",
                    new[] {
                        ("Nutrition", "0.90 (Full meal)"), ("Spoil time", "Never (Indefinite)"), ("Eating time", "120 ticks"),
                        ("Weight", "0.3 kg"), ("Max stack", "75"),
                        ("Mood effect", "0 (Bland sustenance)"), ("Food poisoning risk", "0% (Sterilized)"), ("Market value", "12.00 silver")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "The lifeblood of new landings. While uninspiring in flavor, ration packs ensure colony survival through harsh winter blizzards without spoiling.",
                        specs: new[] {
                            ("Shelf life", "Permanent without freezing", "No refrigerated warehouse required"),
                            ("Colony consumption", "2 packs/colonist per solar day", "Maintain 10-day buffer of 60+ packs"),
                            ("Caravan utility", "Ideal travel provisions", "Lightweight calorie density")
                        }),
                    new[] { (AlmanacKeys.Food, "governing colonist need satisfied"), (AlmanacKeys.Carrots, "perishable agricultural fresh alternate"), ("Iron Stomach", "trait boosting digestion efficiency") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Carrots, AlmanacKeys.Items, "Nutritious crunchy root crops harvested from farm plots", "Item", "Perishable", false, "#ff8c3b",
                    new AlmanacIcon("carrots", "#ff8c3b", "M12 21l-4-9c-1-2 0-5 3-5s4 3 3 5l-2 9z M12 7V2 M9 4l3 3 3-3"),
                    "Freshly pulled garden root vegetables. High in vitamins, crisp in texture, but subject to bacterial rot if kept warm.",
                    "Stocked · 140 units · Cellar pantry", "View stockpile",
                    new[] {
                        ("Nutrition", "0.05 / carrot"), ("Spoil time", "14 days at 20°C · Indefinite frozen"), ("Eating speed", "Rapid"),
                        ("Weight", "0.05 kg"), ("Max stack", "75"),
                        ("Harvest yield", "12–16 per plant"), ("Growing time", "4.2 days"), ("Market value", "0.80 silver")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Carrots can be eaten raw without cooking penalties or processed into rich vegetable stews at a campfire.",
                        specs: new[] {
                            ("Perishability", "Requires refrigeration below 0°C", "Rotting crops vanish from inventory"),
                            ("Harvest cycle", "Fast turn-around spring crop", "Grows in soil or fertile river silt"),
                            ("Meal synergy", "Campfire stew ingredient", "Increases colonist meal satisfaction")
                        }),
                    new[] { ("Carrot Plant", "botanical crop source"), (AlmanacKeys.Food, "colonist hunger replenishment"), ("Grow", "farming cultivation skill") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Salvage, AlmanacKeys.Items, "Unsorted scrap components recovered from wreckage", "Item", "Industrial", false, "#8ca0b3",
                    new AlmanacIcon("salvage", "#8ca0b3", "M4 7l8-4 8 4v10l-8 4-8-4V7z M9 12l3 3 5-5"),
                    "Twisted titanium struts, damaged relays, and copper cabling salvaged from shuttle landing pods.",
                    "Stocked · 310 units · Scrap heap", "View stockpile",
                    new[] {
                        ("Weight", "0.6 kg"), ("Max stack", "75"), ("Flammability", "None (0%)"),
                        ("Smelter output", "10× Salvage -> 7× Steel"), ("Recyclable", "Yes"),
                        ("Market value", "1.10 silver"), ("Degradation outdoors", "None"), ("Cleanliness impact", "-0.1 outdoors")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "The primary initial source of steel before mountain mine shafts are established. Smelted at the electric furnace or salvaged workbench.",
                        specs: new[] {
                            ("Smelting yield", "70% recovery into refined steel", "Low tech prerequisite"),
                            ("Hauling priority", "Low urgency until shelter is complete", "Immune to rain corrosion"),
                            ("Crafting alternate", "Early tool and spike trap fabrication", "Economical defense material")
                        }),
                    new[] { ("Steel", "smelted refined alloy product"), ("Haul", "logistics work hauling scrap"), ("Construct", "re-use in crude barricades") }
                ),
                new AlmanacEntry(
                    "Iron Ore", AlmanacKeys.Items, "Crude hematite chunks dug from subterranean veins", "Item", "Mineral", false, "#9c5a4c",
                    new AlmanacIcon("iron_ore", "#9c5a4c", "M12 2l8 5v10l-8 5-8-5V7l8-5z M8 10l4 3 4-3"),
                    "Dense red-tinted mineral stone extracted by deep mining. Must be smelted with coal to produce structural steel.",
                    "Stocked · 75 units · Mine mouth", "View stockpile",
                    new[] {
                        ("Weight", "1.2 kg"), ("Max stack", "75"), ("Smelter feed ratio", "2 Ore + 1 Coal = 2 Steel"),
                        ("Mining work", "550 ticks / tile"), ("Vein yield", "35–50 ore chunks"),
                        ("Market value", "1.50 silver"), ("Flammability", "None"), ("Degradation", "Immune")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Abundant in deep stone layers. Essential for transitioning the colony into self-sufficient metal manufacturing.",
                        specs: new[] {
                            ("Smelting requirement", "Fueled furnace or electric smelter", "Produces high-grade construction steel"),
                            ("Shaft exploration", "Found in clusters behind granite seams", "Detected by deep survey scans"),
                            ("Hauling strain", "Heavy cargo loads", "Utilize dedicated pack beasts or high-strength haulers")
                        }),
                    new[] { (AlmanacKeys.Mining, "extraction skill"), (AlmanacKeys.Coal, "reduction smelting partner"), ("Steel", "refined output product") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Coal, AlmanacKeys.Items, "Carbonaceous fossil fuel rock with high thermal energy", "Item", "Combustible", false, "#3a3a3c",
                    new AlmanacIcon("coal", "#3a3a3c", "M5 9l5-5 9 3 2 7-6 6-8-2z"),
                    "Bituminous combustible black rock. Burns hotter and lasts significantly longer than timber logs in heaters and smelters.",
                    "Stocked · 120 units · Bunker fuel bay", "View stockpile",
                    new[] {
                        ("Weight", "0.5 kg"), ("Max stack", "75"), ("Burn duration", "24 hours / lump"),
                        ("Heat output", "High (+28°C radiator)"), ("Mining work", "380 ticks / tile"),
                        ("Market value", "1.30 silver"), ("Flammability", "High (Flashpoint 300°C)"), ("Storage hazard", "Fire spread risk")
                    },
                    new AlmanacBody("item", "SPECIFICATIONS",
                        "Crucial winter fuel reserve. Prevents hypothermia fatalities during blizzards when outdoor logging becomes too dangerous for colonists.",
                        specs: new[] {
                            ("Smelting fuel", "Direct heat source for bloomery forge", "Drives iron ore reduction"),
                            ("Space heater longevity", "3× more efficient than wood logs", "Requires less colonist replenishment labour"),
                            ("Stockpile safety", "Keep isolated from electrical conduits", "Spontaneous combustion risk in thunderstorms")
                        }),
                    new[] { ("Cold Snap", "weather crisis mitigated by coal heat"), ("Iron Ore", "metallurgical reduction partner"), (AlmanacKeys.Mining, "extraction labor discipline") }
                )
            }));

            // 5. Skills
            list.Add(new AlmanacCategory("Skills", "M12 2l3 7h7l-5.5 4.5 2 7.5-6.5-4.5-6.5 4.5 2-7.5-5.5-4.5h7z", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Construction", "Skills", "Fabrication, carpentry, masonry, and structure assembly", "Skill", "Practical", false, "#7fd0e0",
                    new AlmanacIcon("skill_construction", "#7fd0e0", "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"),
                    "Governs speed and precision when erecting walls, hanging doors, securing roofs, and assembling furniture.",
                    "Active skill · 4 colony practitioners", "View practitioners",
                    new[] {
                        ("Primary work type", "Construct"), ("Fail risk at L0", "30% (Wasted materials)"), ("Fail risk at L10", "0.5% (Mastery)"),
                        ("Speed scaling", "+8% per level"), ("Quality impact", "Determines bed comfort & beauty"),
                        ("Experience trigger", "Ticks spent building"), ("Decay threshold", "Level 10+ loses XP daily"), ("Governing attribute", "Dexterity")
                    },
                    new AlmanacBody("skill", "LEVELS",
                        "High-level builders rarely botch complex plans, saving rare steel and components while producing high-comfort masterwork beds.",
                        levels: new[] {
                            ("0–3", "Incapable / Novice", "Frequent build botches, 30% material loss, slow speed"),
                            ("4–7", "Apprentice", "Reliable basic wall and door framing, standard furniture"),
                            ("8–12", "Journeyman", "Negligible botch rate, fast pacing, superior quality beds"),
                            ("13–16", "Expert", "Excellent speed bonus (+80%), frequent excellent grade furniture"),
                            ("17–20", "Master", "Masterwork and legendary installations, maximum speed (+160%)")
                        }),
                    new[] { (AlmanacKeys.Wall, "primary structural project"), (AlmanacKeys.Bed, "quality-dependent furniture outcome"), ("Construct", "assigned work discipline") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Mining, "Skills", "Excavation of rock, ore veins, and subterranean tunneling", "Skill", "Physical", false, "#7fd0e0",
                    new AlmanacIcon("skill_mining", "#7fd0e0", "M4 20l16-16M14 4l6 6M4 14l6 6"),
                    "Governs pickaxe excavation speed through solid rock, mountain veins, and ore yield preservation.",
                    "Active skill · 3 colony practitioners", "View practitioners",
                    new[] {
                        ("Primary work type", AlmanacKeys.Mine), ("Excavation pace", "+10% per level"), ("Ore yield at L0", "60% (Dust & rubble)"),
                        ("Ore yield at L10", "100% (Full vein value)"), ("Collapse prevention", "Recognizes unstable ceiling load"),
                        ("Experience trigger", "Mining damage dealt"), ("Decay threshold", "Level 10+ loses XP daily"), ("Governing attribute", "Constitution")
                    },
                    new AlmanacBody("skill", "LEVELS",
                        "Untrained miners pulverize precious metal veins into worthless slag. Assigning high-mining colonists ensures complete ore nugget recovery.",
                        levels: new[] {
                            ("0–3", "Novice", "60% ore yield, grueling 800-tick excavation cycles"),
                            ("4–7", "Miner", "80% ore yield, steady digging cadence"),
                            ("8–12", "Excavator", "100% ore yield, rapid shaft clearing (+60% speed)"),
                            ("13–16", "Drill Specialist", "High speed (+110%), swift bunker chamber hollowing"),
                            ("17–20", "Earthshaker", "Double pace (+180%), instantaneous quarrying")
                        }),
                    new[] { ("Rock", "excavated medium"), ("Iron Ore", "valuable resource vein"), (AlmanacKeys.Mine, "work priority column") }
                ),
                new AlmanacEntry(
                    "Plants", "Skills", "Agriculture, seed germination, crop harvesting, and logging", "Skill", "Natural", false, "#7fd0e0",
                    new AlmanacIcon("skill_plants", "#7fd0e0", "M12 22v-9 M12 13a5 5 0 0 0 5-5c0-4-5-6-5-6s-5 2-5 6a5 5 0 0 0 5 5z"),
                    "Controls agricultural sow speed, tree felling time, and ensures maximum food calorie yield per harvested crop.",
                    "Active skill · 5 colony practitioners", "View practitioners",
                    new[] {
                        ("Primary work type", "Grow"), ("Harvest fail risk L0", "25% (Lost yield)"), ("Harvest fail risk L8", "0% (Full basket)"),
                        ("Sowing pace", "+7% per level"), ("Felling speed", "+9% per level"),
                        ("Foraging bonus", "Chance of extra wild seeds"), ("Experience trigger", "Crops sown & harvested"), ("Governing attribute", "Patience")
                    },
                    new AlmanacBody("skill", "LEVELS",
                        "A skilled planter produces abundant harvests before autumn frost sets in, preventing crop spoilage and famine.",
                        levels: new[] {
                            ("0–3", "Brown Thumb", "25% crop harvest failure, slow seedling placement"),
                            ("4–7", "Gardener", "Reliable vegetable farming, steady woodcutting"),
                            ("8–12", "Farmer", "Zero harvest loss, swift logging (+60% timber pace)"),
                            ("13–16", "Botanist", "High throughput (+100%), rapid orchard clearance"),
                            ("17–20", "Agronomist", "Bountiful yield bonus, near-instant sow cadence")
                        }),
                    new[] { (AlmanacKeys.Carrots, "tended vegetable crop"), ("Pine", "felled timber tree"), ("Grow", "governing work priority") }
                )
            }));

            // 6. Work types
            list.Add(new AlmanacCategory("Work types", "M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2M9 5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Construct", "Work types", "Erecting designated architectural plans and deconstruction", "Work Type", "Priority", false, "#7fd0e0",
                    new AlmanacIcon("work_construct", "#7fd0e0", "M3 21h18 M4 18l4-12 4 4 6-6"),
                    "Dispatches colonists to deliver materials to blueprints, assemble foundations, frame walls, and hang doors.",
                    "Active work type · 4 colonists assigned", "Manage priorities",
                    new[] {
                        ("Governing skill", "Construction"), ("Tools required", "None (Hands & hammer)"), ("Interruptible", "Yes (Safe at tick end)"),
                        ("Fatigue rate", "Standard (1.0×)"), ("Danger rating", "Low (Cave-in risk if unroofed)"),
                        ("Priority rank", "Typically Rank 1 or 2"), ("Secondary tasks", "Repair, deconstruct, roof")
                    },
                    new AlmanacBody("simple", "BEHAVIOUR",
                        "Assigned builders scan the map for armed blueprints with delivered materials. They prioritize defensive perimeter breaches and bed installations above general cosmetic pavement.",
                        effects: new[] {
                            ("Frame delivery", "Builders haul required wood or steel to the blueprint location before assembly."),
                            ("Deconstruction", "Dismantling returning 75% of original material value to local stockpiles."),
                            ("Repairs", "Automatically patches damaged walls following raider assaults.")
                        }),
                    new[] { ("Construction", "underlying skill"), (AlmanacKeys.Wall, "common target structure"), (AlmanacKeys.Bed, "crucial domestic assignment") }
                ),
                new AlmanacEntry(
                    "Grow", "Work types", "Tilling farm plots, sowing seeds, and harvesting ripe crops", "Work Type", "Priority", false, "#7fd0e0",
                    new AlmanacIcon("work_grow", "#7fd0e0", "M12 2v20 M5 8l7-4 7 4 M5 16l7 4 7-4"),
                    "Directs agricultural colonists to clear unwanted vegetation, till farm soil, sow seeds, and pick ripe vegetables.",
                    "Active work type · 3 colonists assigned", "Manage priorities",
                    new[] {
                        ("Governing skill", "Plants"), ("Tools required", "Trowels and hoe"), ("Seasonal urgency", "Critical in Spring & Autumn"),
                        ("Fatigue rate", "Moderate (1.1× outdoor walking)"), ("Danger rating", "Low (Exposed to predators)"),
                        ("Priority rank", "Rank 1 in harvest season"), ("Secondary tasks", "Prune, weed, clear brush")
                    },
                    new AlmanacBody("simple", "BEHAVIOUR",
                        "Planters automatically monitor designated growing zones. When carrot growth reaches 100%, harvest jobs take precedence over new sowing to prevent crop rot.",
                        effects: new[] {
                            ("Harvest rush", "Urgent collection of ripe vegetables before frost damage occurs."),
                            ("Weeding", "Removes wild grass choking cultivated crop plots."),
                            ("Forestry", "Fells marked pine timber to supply lumber stores.")
                        }),
                    new[] { ("Plants", "governing skill"), ("Carrot Plant", "primary food crop"), ("Pine", "timber harvest target") }
                ),
                new AlmanacEntry(
                    AlmanacKeys.Mine, "Work types", "Extracting stone blocks and valuable mineral seams", "Work Type", "Priority", false, "#7fd0e0",
                    new AlmanacIcon("work_mine", "#7fd0e0", "M2 12h20 M12 2v20"),
                    "Assigns pawns to dig into marked rock faces, quarrying stone building blocks and clearing underground living space.",
                    "Active work type · 2 colonists assigned", "Manage priorities",
                    new[] {
                        ("Governing skill", AlmanacKeys.Mining), ("Tools required", "Heavy pickaxes"), ("Fatigue rate", "High (1.3× muscle strain)"),
                        ("Danger rating", "Moderate (Overhead rock collapse)"), ("Darkness debuff", "Miners gain mood penalty in unlit shafts"),
                        ("Priority rank", "Rank 3 background task"), ("Secondary tasks", "Smoothing stone walls")
                    },
                    new AlmanacBody("simple", "BEHAVIOUR",
                        "Miners carve designated rock tiles. Keep shafts illuminated with torches to prevent darkness mood debuffs and watch ceiling support radiuses.",
                        effects: new[] {
                            ("Rock clearance", "Clears pathway obstructions and yields stone building chunks."),
                            ("Ore discovery", "Exposes rich iron ore and coal seams buried behind bedrock."),
                            ("Subterranean bunker", "Hollows mountain halls insulated against mortar fire.")
                        }),
                    new[] { (AlmanacKeys.Mining, "governing skill"), ("Rock", "target terrain medium"), ("Iron Ore", "valuable resource vein") }
                ),
                new AlmanacEntry(
                    "Haul", "Work types", "Moving loose items into stores, and unwanted ones back out", "Work Type", "Logistics", false, "#7fd0e0",
                    new AlmanacIcon("work_haul", "#7fd0e0", "M5 8h14M5 8a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v2a2 2 0 0 1-2 2M5 8v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8"),
                    "Ensures crops are hauled indoors before rain rots them, and keeps building sites supplied with materials.",
                    "Active work type · All colonists assigned", "Manage priorities",
                    new[] {
                        ("Governing skill", "None (All pawns capable)"), ("Carry capacity", "75 units standard"), ("Fatigue rate", "Moderate to high"),
                        ("Distance scaling", "Speed determines efficiency"), ("Perishable bias", "Prioritizes food over metal scrap"),
                        ("Store clearing", "Anything a store refuses is carried back out"),
                        ("Priority rank", "Rank 4 baseline or Rank 1 during harvest"), ("Secondary tasks", "Clear corpse debris")
                    },
                    new AlmanacBody("simple", "BEHAVIOUR",
                        "The unsung backbone of colony productivity. Without dedicated hauling, harvested vegetables rot in the fields and construction sites stall.",
                        effects: new[] {
                            ("Larder restocking", "Carries carrots and ration packs from fields into freezing rooms."),
                            ("Blueprint supply", "Pre-loads wood and stone to planned wall segments."),
                            ("Corridor clearing", "Removes scrap metal cluttering indoor hallway thoroughfares."),
                            // Added 2026-09-21 with the behaviour itself: a store's filter governs
                            // what is already in it as well as what arrives, so the entry that
                            // describes hauling has to say who empties one.
                            ("Store clearing", "Carries out whatever a store has been told not to accept, to a store that wants it or to open ground.")
                        }),
                    new[] { ("Ration Pack", "critical freight commodity"), (AlmanacKeys.Salvage, "cleared debris"), ("Construct", "recipient of hauled construction materials") }
                )
            }));

            // 7. Needs
            list.Add(new AlmanacCategory("Needs", "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    AlmanacKeys.Food, "Needs", "Caloric nourishment preventing starvation debuffs", "Need", "Physiological", false, "#7fd0e0",
                    new AlmanacIcon("need_food", "#7fd0e0", "M18 8h1a4 4 0 0 1 0 8h-1 M2 8h16v9a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V8z M6 1v3 M10 1v3 M14 1v3"),
                    "Meter tracking colonist digestive reserves. Empties continuously as colonists perform physical labour.",
                    "Vital sign · Depletes over 16 game hours", "Inspect roster",
                    new[] {
                        ("Depletion rate", "62.5 units / hour"), ("Urgent hunger threshold", "Below 30% (Hungry moodlet)"),
                        ("Starvation onset", "At 0% (Accumulates lethal malnutrition)"), ("Satisfaction source", "Ration packs, carrots, meals"),
                        ("Digestion speed", "20 minutes eating interaction"), ("Lethal timer", "3 days at 0% food")
                    },
                    new AlmanacBody("simple", "PHYSIOLOGY",
                        "Hungry colonists suffer escalating mood penalties (-5 to -15), culminating in extreme malnutrition which drops consciousness and causes death.",
                        effects: new[] {
                            ("Full belly (80–100%)", "No penalty, slight satisfaction bonus."),
                            ("Ravenous (1–20%)", "-10 mood debuff, pawn seeks food immediately, interrupting assigned work."),
                            ("Starving (0%)", "Malnutrition health condition ticks upward. Pawn collapses if untreated.")
                        }),
                    new[] { ("Ration Pack", "immediate emergency food"), (AlmanacKeys.Carrots, "fresh garden crop meal"), ("Iron Stomach", "trait boosting digestion efficiency") }
                ),
                new AlmanacEntry(
                    "Rest", "Needs", "Neurological recovery through regular sleep cycles", "Need", "Physiological", false, "#7fd0e0",
                    new AlmanacIcon("need_rest", "#7fd0e0", "M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"),
                    "Tracks neurological exhaustion. Recovered by sleeping in assigned beds, comfortable barracks, or temporary ground swags.",
                    "Vital sign · Depletes over 18 game hours", "Inspect roster",
                    new[] {
                        ("Depletion rate", "55 units / hour"), ("Drowsy threshold", "Below 30% (-4 mood)"),
                        ("Exhaustion threshold", "Below 10% (-12 mood, consciousness drop)"), ("Micro-sleep collapse", "At 0% (Pawn collapses unconscious on floor)"),
                        ("Recovery medium", "Wooden bed (100% speed) · Ground (65% speed)"), ("Disturbed sleep penalty", "Walking near sleeping colonists causes -3 mood")
                    },
                    new AlmanacBody("simple", "PHYSIOLOGY",
                        "Colonists denied rest stagger sluggishly and suffer extreme cognitive degradation. Provide private insulated bedrooms to avoid disturbed sleep penalties.",
                        effects: new[] {
                            ("Well Rested (80–100%)", "Full cognitive alert pace, slight mood boost."),
                            ("Exhausted (<15%)", "-12 mood debuff, move speed drops by 20%, work fail chances triple."),
                            ("Sleep Deprivation (0%)", "Pawn passes out wherever they stand, vulnerable to hypothermia and predators.")
                        }),
                    new[] { (AlmanacKeys.Bed, "restoration station furniture"), ("Night Owl", "trait altering preferred rest schedule"), ("Consciousness", "underlying health stat affected by exhaustion") }
                ),
                new AlmanacEntry(
                    "Mood", "Needs", "Mental composure and psychological stability", "Need", "Psychological", false, "#7fd0e0",
                    new AlmanacIcon("need_mood", "#7fd0e0", "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8zm-3.5-9a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm7 0a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm-7 4.5a5.5 5.5 0 0 0 7 0"),
                    "Aggregate barometer of colonist happiness. Dictated by physical comfort, hunger, injuries, environment, and social interactions.",
                    "Psychological state · Dynamic scale 0–100%", "Inspect roster",
                    new[] {
                        ("Minor break threshold", "Below 35% (Sad wander, binge eating)"), ("Major break threshold", "Below 20% (Tantrum, insult spree)"),
                        ("Extreme break threshold", "Below 5% (Berserk rage, catatonia)"), ("Inspiration threshold", "Above 85% (Critical crafting/work surge)"),
                        ("Contributors", "Spacious room, fine meal, warmth, comfort"), ("Detractors", "Darkness, cold, pain, rotting corpses, hunger")
                    },
                    new AlmanacBody("simple", "PSYCHOLOGY",
                        "When mood plummets below critical thresholds, colonists suffer mental breaks. Keep dining halls clean, supply warm beds, and address pain injuries promptly.",
                        effects: new[] {
                            ("High Spirits (>85%)", "Triggers inspirations: double work speed, guaranteed high-grade construction."),
                            ("Minor Break (<35%)", "Pawn wanders aimlessly for 6 hours, refusing player direct orders."),
                            ("Extreme Break (<5%)", "Pawn attacks nearest colonist or sets fire to stockpiles in uncontrollable rage.")
                        }),
                    new[] { ("Hard Worker", "trait providing natural work drive"), (AlmanacKeys.Bed, "comfort source maintaining mood"), (AlmanacKeys.Food, "preventing hunger mood debuffs") }
                )
            }));

            // 8. Traits
            list.Add(new AlmanacCategory("Traits", "M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M8.5 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M20 8v6 M23 11h-6", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Hard Worker", "Traits", "Tenacious personality boasting elevated baseline labour output", "Trait", "Psychological", false, "#7fd0e0",
                    new AlmanacIcon("trait_hard_worker", "#7fd0e0", "M13 2L3 14h9l-1 8 10-12h-9l1-8z"),
                    "This colonist possesses an innate drive to see chores completed. Works noticeably faster across all tasks.",
                    "Innate trait · Permanent personality modifier", "View colonists",
                    new[] {
                        ("Global work speed", "+20% across all tasks"), ("Break threshold impact", "None (Standard 35%)"),
                        ("Social impact", "Slightly annoyed by lazy colonists"), ("Inspiration frequency", "Elevated"),
                        ("Conflicts with", "Slothful, Lazy"), ("Genetic factor", "No (Innate temperament)")
                    },
                    new AlmanacBody("trait", "EFFECTS",
                        "Hard Workers are prime candidates for construction, mining, and plant harvesting, as the 20% speed bonus compounds across long shifts.",
                        effects: new[] {
                            ("Universal haste", "Adds a flat +20% multiplier to all building, mining, planting, and crafting work."),
                            ("Work ethic", "Less prone to idle wandering when orders are active.")
                        }),
                    new[] { ("Construct", "amplified work assignment"), (AlmanacKeys.Mining, "rapid tunneling synergy"), ("Mood", "satisfaction through industry") }
                ),
                new AlmanacEntry(
                    "Night Owl", "Traits", "Thrives under nocturnal hours, irritated by bright midday sun", "Trait", "Physiological", false, "#7fd0e0",
                    new AlmanacIcon("trait_night_owl", "#7fd0e0", "M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9z"),
                    "Possesses a nocturnal circadian rhythm. Happiest when awake and working between 23:00 and 06:00.",
                    "Innate trait · Schedule-sensitive modifier", "View colonists",
                    new[] {
                        ("Night shift mood bonus", "+16 mood awake at night (23:00–06:00)"), ("Day shift mood penalty", "-10 mood awake during day (11:00–18:00)"),
                        ("Optimal schedule", "Sleep 11:00 to 18:00"), ("Darkness penalty", "Immune to darkness debuff"),
                        ("Conflicts with", "None"), ("Recommended roles", "Night guard, night hauler, quiet workshop crafts")
                    },
                    new AlmanacBody("trait", "EFFECTS",
                        "Adjust this colonist's schedule on the Work tab to sleep through the afternoon. Gives a reliable +16 mood buffer against mental breaks.",
                        effects: new[] {
                            ("Nocturnal joy", "+16 mood while awake in midnight hours."),
                            ("Solar glare", "-10 mood if forced to work in midday sunshine.")
                        }),
                    new[] { ("Rest", "schedule alignment requirement"), ("Work types", "night shift assignment"), ("Mood", "effortless happiness boost") }
                ),
                new AlmanacEntry(
                    "Iron Stomach", "Traits", "Robust digestive tract impervious to spoiled or raw rations", "Trait", "Physiological", false, "#7fd0e0",
                    new AlmanacIcon("trait_iron_stomach", "#7fd0e0", "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"),
                    "Possesses an iron-clad gut lining. Can consume raw field vegetables and questionable meat without food poisoning.",
                    "Innate trait · Biological digestive defense", "View colonists",
                    new[] {
                        ("Food poisoning chance", "0% (Absolute immunity)"), ("Raw food mood penalty", "None (Enjoys raw carrots)"),
                        ("Campfire cooking risk", "Unaffected by dirty kitchen filth"), ("Emergency diet", "Can safely eat emergency raw rations"),
                        ("Conflicts with", "Delicate, Ascetic")
                    },
                    new AlmanacBody("trait", "EFFECTS",
                        "Invaluable in early colonies without sterile kitchens. An Iron Stomach colonist can eat raw unwashed carrots pulled straight from the soil.",
                        effects: new[] {
                            ("Total immunity", "Never contracts food poisoning, eliminating debilitating vomiting spells."),
                            ("Raw foraging", "Can sustain themselves indefinitely on raw harvested garden crops.")
                        }),
                    new[] { (AlmanacKeys.Carrots, "safe raw consumption"), ("Ration Pack", "unflinching digestion"), (AlmanacKeys.Food, "governing physiological need") }
                )
            }));

            // 9. Health
            list.Add(new AlmanacCategory("Health", "M22 12h-4l-3 9L9 3l-3 9H2", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Consciousness", "Health", "Core cognitive alertness and brain function capacity", "Health", "Vital Capacity", false, "#7fd0e0",
                    new AlmanacIcon("health_consciousness", "#7fd0e0", "M12 4.5a4.5 4.5 0 0 0-4.5 4.5c0 1.9 1.1 3.5 2.7 4.1v2.9h3.6v-2.9c1.6-.6 2.7-2.2 2.7-4.1a4.5 4.5 0 0 0-4.5-4.5z M9 19h6v2H9z"),
                    "The primary capacity governing all human faculties. If consciousness drops to 0%, the colonist immediately dies.",
                    "Vital sign · Baseline 100% capacity", "Inspect colonists",
                    new[] {
                        ("Lethal floor", "0% (Instant cessation of life)"), ("Incapacitation threshold", "Below 30% (Comatose collapse)"),
                        ("Governing organ", "Brain"), ("Affected capacities", "All: Moving, Manipulation, Talking, Sight"),
                        ("Impaired by", "Severe pain, blood loss, hypothermia, starvation"), ("Boosted by", "Stimulants, wake-up drugs, inspirations")
                    },
                    new AlmanacBody("simple", "CAPACITY SCALING",
                        "Consciousness acts as a master multiplier. A colonist at 50% consciousness moves at half pace, builds at half speed, and fumbles complex medical tasks.",
                        effects: new[] {
                            ("Full consciousness (100%)", "Standard operating efficiency."),
                            ("Impaired (40–70%)", "Slurred speech, severe work speed penalty, unsteady walking cadence."),
                            ("Comatose (<30%)", "Colonist drops to the floor unconscious; must be rescued and tucked into bed.")
                        }),
                    new[] { ("Moving", "directly multiplied by consciousness"), ("Pain", "primary symptom reducing alertness"), ("Cold Snap", "environmental hazard threat") }
                ),
                new AlmanacEntry(
                    "Moving", "Health", "Locomotion capability driven by leg integrity and spine", "Health", "Capacity", false, "#7fd0e0",
                    new AlmanacIcon("health_moving", "#7fd0e0", "M13.5 5.5c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zM9.8 8.9L7 23h2.1l1.8-8 2.1 2v6h2v-7.5l-2.1-2 .6-3C14.8 12 16.8 13 19 13v-2c-1.9 0-3.5-1-4.3-2.4l-1-1.6c-.4-.6-1-1-1.7-1-.3 0-.5.1-.8.1L6 8.3V13h2V9.6l1.8-.7"),
                    "Directly scales world transit speed across all terrain. Governed by left leg, right leg, spine, and overall consciousness.",
                    "Physical capacity · Baseline 4.6 c/s sprint", "Inspect colonists",
                    new[] {
                        ("Sprint baseline", "4.60 cells / second"), ("Incapacitation point", "0% (Inability to stand or crawl)"),
                        ("Key limbs", "Left Leg (40%), Right Leg (40%), Spine (20%)"), ("Terrain interaction", "Amplified by pavement (+15%), dragged by water (-45%)"),
                        ("Impaired by", "Bruised shins, fractured femur, hypothermia frostbite"), ("Combat impact", "Dictates ability to kite melee raiders")
                    },
                    new AlmanacBody("simple", "CAPACITY SCALING",
                        "Fast colonists outrun biting predators and haul twice the daily material tonnage. Crippled movement paralyzes colony logistic operations.",
                        effects: new[] {
                            ("Unimpaired (100%)", "Crisp baseline pace; outruns duct rats and starving predators."),
                            ("Limping (50–70%)", "Painful plodding; raiders easily catch and surround the colonist in open terrain."),
                            ("Immobile (0%)", "Cannot exit bed or cell; must be hand-fed by assigned haulers.")
                        }),
                    new[] { ("Consciousness", "master capacity multiplier"), ("Grass", "open field sprint surface"), ("Shallow Water", "heavy transit drag terrain") }
                )
            }));

            // 10. Fauna
            list.Add(new AlmanacCategory("Fauna", "M4.5 9.5a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0z M12.5 9.5a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0z M8.5 15.5a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0z", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Scraphound", "Fauna", "Hardy feral canine adapted to scavenging wreck fields", "Fauna", "Canine", false, "#c49a45",
                    new AlmanacIcon("fauna_scraphound", "#c49a45", "M8 5l2 3h4l2-3 2 4-2 7H6L4 9l4-4z M7 19h10v2H7z"),
                    "Semi-domesticated cybernetic scavenger hound. Roams wreckage perimeters hunting vermin; can be tamed as a loyal guard beast.",
                    "Native wildlife · 2 roaming map periphery", "Track on map",
                    new[] {
                        ("Temperament", "Docile unless starving"), ("Sprint speed", "5.2 cells / sec (Fast)"), ("Bite damage", "9 blunt/piercing"),
                        ("Tame chance", "35% (Requires Plants/Animals L4)"), ("Diet", "Omnivore (Ration packs, meat, raw carrots)"),
                        ("Meat yield", "45 raw meat"), ("Leather yield", "18 tough hound leather"), ("Lifespan", "12 solar years")
                    },
                    new AlmanacBody("simple", "BEHAVIOUR",
                        "Scraphounds will not attack unless provoked or driven mad by starvation. Once tamed, their keen senses detect incoming raiders through deep fog.",
                        effects: new[] {
                            ("Guard instinct", "Automatically attacks hostiles threatening its assigned colonist master."),
                            ("Hauling training", "Can be trained to carry light item stacks between stockpiles."),
                            ("Manhunter threat", "If shot from afar, has a 20% chance to enter enraged manhunter pack state.")
                        }),
                    new[] { ("Duct Rat", "natural rodent prey"), ("Moving", "speed comparison benchmark"), (AlmanacKeys.Wall, "fencing barrier keeping hounds out") }
                ),
                new AlmanacEntry(
                    "Duct Rat", "Fauna", "Prolific subterranean rodent nesting in conduit shafts", "Fauna", "Rodent", false, "#8c7d75",
                    new AlmanacIcon("fauna_duct_rat", "#8c7d75", "M3 13c1-2 4-3 7-3 4 0 8 2 10 6H2c0-1 0-2 1-3z M17 10a2 2 0 1 0 0-4 2 2 0 0 0 0 4z"),
                    "Omnipresent subterranean pest. Chews through insulation wiring and raids unattended grain and carrot stockpiles.",
                    "Native pest · 7 infesting lower ruins", "Track on map",
                    new[] {
                        ("Temperament", "Skittish (Flees from humans)"), ("Sprint speed", "3.8 cells / sec"), ("Bite damage", "3 scratch"),
                        ("Tame chance", "10% (Low utility)"), ("Diet", "Omnivore (Carrots, timber, wire insulation)"),
                        ("Meat yield", "12 raw meat"), ("Leather yield", "6 rodent pelt"), ("Disease vector", "15% chance to transmit wound infection")
                    },
                    new AlmanacBody("simple", "BEHAVIOUR",
                        "While individually harmless, duct rats burrow into unguarded pantry stores, eating through valuable carrot stacks over winter.",
                        effects: new[] {
                            ("Food raid", "Sniffs out unsealed stockpiles and nibbles through stacks tile by tile."),
                            ("Wiring fire risk", "Chews electrical conduits, triggering spontaneous short-circuit spark fires."),
                            ("Pest culling", "Easy target practice for apprentice colonist hunters.")
                        }),
                    new[] { (AlmanacKeys.Carrots, "favoured stolen food source"), ("Scraphound", "predatory counter-hunter"), (AlmanacKeys.Door, "essential barrier keeping rats out of larder") }
                )
            }));

            // 11. Flora
            list.Add(new AlmanacCategory("Flora", "M12 22v-8 M8 14c0-4 4-8 4-8s4 4 4 8 M4 22h16", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Carrot Plant", "Flora", "Domesticated edible taproot cultivated in farm zones", "Flora", "Crop", false, "#4d9e5a",
                    new AlmanacIcon("flora_carrot", "#4d9e5a", "M12 22V10 M8 10c0-3 4-6 4-6s4 3 4 6 M7 15h10"),
                    "Hardy leafy taproot crop. Grows reliably in meadow loam, yielding crisp orange carrots after four days of sunlight.",
                    "Cultivated crop · 48 plants in growing zone 1", "Inspect farm",
                    new[] {
                        ("Growth cycle", "4.2 days (Fast)"), ("Optimal soil", "Loam / Soil (100% fertility)"), ("Cold tolerance", "Down to -4°C"),
                        ("Harvest yield", "14 carrots per plant"), ("Light requirement", "51% (Sunlight or sun lamp)"),
                        ("Water need", "Moderate"), ("Blight susceptibility", "Low"), ("Max height", "0.4m")
                    },
                    new AlmanacBody("simple", "BOTANY",
                        "The fastest food source available to newly arrived colonies. Sown in early spring to guarantee a pantry surplus before winter frost.",
                        effects: new[] {
                            ("Spring planting", "Sown into cleared grass or tilled soil furrow plots."),
                            ("Frost susceptibility", "Sub-zero temperatures stunt growth; harvest before freezing snaps."),
                            ("Food yield", "14 units per mature plant, replenishing colony meal stocks immediately.")
                        }),
                    new[] { (AlmanacKeys.Carrots, "harvested edible output"), ("Grow", "assigned farming work type"), ("Soil", "optimal fertile root bed") }
                ),
                new AlmanacEntry(
                    "Pine", "Flora", "Stately evergreen conifer yielding aromatic timber logs", "Flora", "Tree", false, "#2f7542",
                    new AlmanacIcon("flora_pine", "#2f7542", "M12 2L5 12h4l-3 6h12l-3-6h4L12 2z M12 18v4"),
                    "Resilient needle-bearing tree native to the plateau. Resists freezing winters and yields generous lumber when felled.",
                    "Natural forest · 82 trees across surface layer", "Inspect trees",
                    new[] {
                        ("Growth cycle", "14 days to full timber maturity"), ("Optimal soil", "Soil, gravel, bare earth"), ("Cold tolerance", "Down to -25°C (Hardy)"),
                        ("Harvest yield", "35 wood logs"), ("Cover value", "50% missile interception"),
                        ("Flammability", "High (80%)"), ("Wind resistance", "Superior (Living windbreak)"), ("Max height", "4.5m")
                    },
                    new AlmanacBody("simple", "BOTANY",
                        "Provides the timber necessary to frame early colony shelters. Pines can also be left intact around outer perimeters to act as natural missile cover.",
                        effects: new[] {
                            ("Lumber harvest", "Yields 35 wood logs per mature tree when chopped by planters."),
                            ("Natural battlements", "Colonists ducking behind pine trunks gain 50% missile cover."),
                            ("Forest fire hazard", "Lightning strikes can ignite dense pine groves into sweeping firestorms.")
                        }),
                    new[] { (AlmanacKeys.Wood, "harvested lumber product"), ("Plants", "governing felling skill"), ("Grass", "surrounding forest floor") }
                )
            }));

            // 12. Events
            list.Add(new AlmanacCategory("Events", "M13 2L3 14h9l-1 8 10-12h-9l1-8z", new List<AlmanacEntry>
            {
                new AlmanacEntry(
                    "Supply Drop", "Events", "Orbital cargo pod crash landing with valuable salvage", "Event", "Positive", false, "#7fd0e0",
                    new AlmanacIcon("event_supply_drop", "#7fd0e0", "M12 2v10 M12 12l4-4 M12 12l-4-4 M4 16h16v4H4z"),
                    "Atmospheric entry of abandoned orbital supply containers. Crashes onto open terrain, dispersing packaged survival rations.",
                    "Incident · Can occur during clear weather", "View logs",
                    new[] {
                        ("Incident type", "Good fortune / Bounty"), ("Drop location", "Random unroofed surface tile"),
                        ("Payload contents", "25–40× Ration Packs or 50× Steel"), ("Impact hazard", "Destroys roofs directly beneath landing"),
                        ("Frequency", "Once per quadrant cycle"), ("Urgency", "Haul indoors before wild predators scavenge food")
                    },
                    new AlmanacBody("simple", "INCIDENT PROTOCOL",
                        "When alarms signal a pod entry, immediately send haulers to secure the drop pod contents before local scraphounds or duct rats consume the food.",
                        effects: new[] {
                            ("Immediate haul order", "Assign priority hauling to collect packaged rations from impact point."),
                            ("Roof warning", "Pods crashing through thin sheet metal can crush sleeping pawns beneath.")
                        }),
                    new[] { ("Ration Pack", "typical dropped bounty cargo"), ("Steel", "common structural payload"), ("Haul", "urgent recovery work type") }
                ),
                new AlmanacEntry(
                    "Cold Snap", "Events", "Severe arctic air mass plunging temperatures below freezing", "Event", "Threat", false, "#7fd0e0",
                    new AlmanacIcon("event_cold_snap", "#7fd0e0", "M12 2v20M2 12h20M4.93 4.93l14.14 14.14M4.93 19.07l14.14-14.14"),
                    "Sudden polar temperature inversion causing ambient temperatures to drop by 20–30°C over multiple days.",
                    "Weather emergency · Lasts 2–4 solar days", "View logs",
                    new[] {
                        ("Temperature delta", "-25°C below seasonal average"), ("Outdoor hazard", "Lethal hypothermia within 4 hours"),
                        ("Crop impact", "Instant freeze death for unharvested crops"), ("Duration", "36 to 72 game hours"),
                        ("Countermeasures", "Campfires, coal heaters, parka coats, sealed doors"), ("Frequency", "Autumn & Winter threat")
                    },
                    new AlmanacBody("simple", "INCIDENT PROTOCOL",
                        "Cold snaps kill unprepared colonies. Scurry all planters to harvest carrots before the freeze, keep colonists indoors, and stoke coal space heaters.",
                        effects: new[] {
                            ("Emergency harvest", "Immediately chop all crops above 70% growth before frost ruins them."),
                            ("Indoor retreat", "Restrict colonist zone assignments to insulated heated barracks."),
                            ("Fuel consumption", "Stoke campfires and coal stoves; monitor fuel reserves closely.")
                        }),
                    new[] { ("Carrot Plant", "vulnerable crop ruined by frost"), (AlmanacKeys.Coal, "life-saving space heating fuel"), ("Consciousness", "degraded rapidly by severe hypothermia") }
                )
            }));

            return list;
        }
    }
}
