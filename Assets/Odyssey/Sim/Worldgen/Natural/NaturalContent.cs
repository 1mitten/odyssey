#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>
    /// The content the wilderness generator writes: the natural terrain kinds, the tree edifices
    /// and the presentation module ids for both.
    ///
    /// **There is one terrain table, and this class holds the wilderness half of its numbering.**
    /// Terrain is stored in <see cref="World.CellGrid.Terrain"/> as a table index; the city's ten
    /// occupy 0 to <see cref="CoreContent.TerrainCount"/> - 1 and these continue from
    /// <see cref="FirstTerrain"/> rather than starting a second table at zero, so the two
    /// generators can never write the same index meaning two different things. The same is done
    /// for edifice ids (<see cref="FirstEdifice"/>) and stuffs (<see cref="StuffWood"/>).
    ///
    /// **What is left here is the constants, and only the constants** (OQ-16, OQ-49).
    /// <c>Defs/Core/World/Terrain.xml</c> declares all twenty-one kinds,
    /// <see cref="WorldContent.TerrainOrder"/> is the list that decides which name is which index,
    /// and <see cref="WorldContent.Table"/> is what the running game reads. The in-code tables
    /// that used to mirror the XML are gone. A constant below is a compile-time handle — it is in
    /// the views, the saves and the hash — so it cannot become content, and
    /// <c>WorldContentDefTests</c> checks every one of them against the order list.
    ///
    /// Two rules still hold, both about the numbering rather than the values:
    ///   1. never renumber <see cref="CoreContent"/> without renumbering here;
    ///   2. presentation tables sized by <c>CoreContent.Terrain.Count</c> (currently
    ///      <c>WorldRenderModel</c>) must be widened to <see cref="TerrainCount"/> before a natural
    ///      map is rendered. The module ids below are what such a table needs to resolve.
    ///
    /// The third rule this list used to carry — always ask <see cref="TerrainAt"/> here and never
    /// <c>CoreContent.TerrainAt</c>, because that table would throw on a natural index — is gone
    /// with the split it described. Both now read the one table and either answers for any index.
    /// </summary>
    public static class NaturalContent
    {
        // ---- terrain -----------------------------------------------------------------------
        //
        // Natural indices continue where CoreContent's leave off. Rock (CoreContent.TerrainRock)
        // is reused as-is: a wilderness map's stone and a ruined city's bedrock stratum are the
        // same material and nothing is gained by having two.

        public const ushort FirstTerrain = (ushort)CoreContent.TerrainCount;

        /// <summary>The living surface: soil with grass on it. Solid, and the only place a tree grows.</summary>
        public const ushort TerrainGrass = (ushort)(FirstTerrain + 0);

        /// <summary>A bare patch of the same soil. Solid, fertile, no cover.</summary>
        public const ushort TerrainBareEarth = (ushort)(FirstTerrain + 1);

        /// <summary>Stony ground. Solid; distinct from CoreContent's loose, diggable gravel.</summary>
        public const ushort TerrainPackedGravel = (ushort)(FirstTerrain + 2);

        public const ushort TerrainSand = (ushort)(FirstTerrain + 3);

        /// <summary>The band between soil and rock. Solid, cheap to dig, nothing grows in it.</summary>
        public const ushort TerrainSubsoil = (ushort)(FirstTerrain + 4);

        /// <summary>The floor of the world. Solid and deliberately punitive to mine.</summary>
        public const ushort TerrainBedrock = (ushort)(FirstTerrain + 5);

        public const ushort TerrainIronOre = (ushort)(FirstTerrain + 6);
        public const ushort TerrainCoalSeam = (ushort)(FirstTerrain + 7);

        // ---- water -------------------------------------------------------------------------
        //
        // Water sits in a channel cut one layer below the ground around it, so the cell holding
        // it is level with the bank's own solid top and a colonist steps *down* to wade. Neither
        // water is solid: the bed beneath it is what a wader stands on.

        /// <summary>Wadeable water. Walkable, and a third of walking speed (cost class 2).</summary>
        public const ushort TerrainShallowWater = (ushort)(FirstTerrain + 8);

        /// <summary>
        /// Water out of one's depth. Impassable rather than merely expensive, so the pathfinder
        /// routes around a lake instead of pricing a swim nobody can survive.
        /// </summary>
        public const ushort TerrainDeepWater = (ushort)(FirstTerrain + 9);

        /// <summary>
        /// Wet ground fringing water. Ordinary solid ground at its own height — a marsh column is
        /// never lowered — and slow to cross (cost class 1) rather than impassable.
        /// </summary>
        public const ushort TerrainMarsh = (ushort)(FirstTerrain + 10);

        /// <summary>One past the last natural index. Sizes any table that must span both sets.</summary>
        public const int TerrainCount = FirstTerrain + 11;

        /// <summary>Stone, shared with the city generator. Ore is grown inside this and nothing else.</summary>
        public const ushort TerrainRock = CoreContent.TerrainRock;

        public const ushort TerrainAir = CoreContent.TerrainAir;

        // ---- edifices ----------------------------------------------------------------------
        //
        // CoreContent's edifice ids end at EdificeUtilityTap = 9.

        public const ushort FirstEdifice = 10;

        /// <summary>
        /// A tree. Placed in the air cell above the ground it grows from, and **not** blocking:
        /// a colonist walks through woodland, the way RimWorld's plants work, so every surface
        /// cell stays walkable and a forest is a supply of wood rather than a wall.
        /// </summary>
        public const ushort EdificeTreeConifer = (ushort)(FirstEdifice + 0);

        public const ushort EdificeTreeBroadleaf = (ushort)(FirstEdifice + 1);

        public const int EdificeCount = FirstEdifice + 2;

        public static bool IsTree(ushort edifice) =>
            edifice == EdificeTreeConifer || edifice == EdificeTreeBroadleaf;

        // ---- stuffs ------------------------------------------------------------------------
        //
        // CoreContent's stuffs end at StuffComposite = 3.

        /// <summary>What a tree is made of, and what felling one yields.</summary>
        public const ushort StuffWood = 4;

        // ---- presentation module ids -------------------------------------------------------
        //
        // Id strings only. Nothing here resolves a module, so a clone without the licensed art
        // generates and simulates a wilderness map exactly as a machine with the packs does.
        // The terrain ids follow the renderer's convention, Prefix + "terrain." + the lowercased
        // def name, so a catalogue entry can be keyed either way round.

        public const string Prefix = "odyssey.module.";

        public const string ModuleGrass = Prefix + "terrain.grass";
        public const string ModuleBareEarth = Prefix + "terrain.bareearth";
        public const string ModulePackedGravel = Prefix + "terrain.packedgravel";
        public const string ModuleSand = Prefix + "terrain.sand";
        public const string ModuleSubsoil = Prefix + "terrain.subsoil";
        public const string ModuleRock = Prefix + "terrain.rock";
        public const string ModuleBedrock = Prefix + "terrain.bedrock";
        public const string ModuleIronOre = Prefix + "terrain.ironore";
        public const string ModuleCoalSeam = Prefix + "terrain.coalseam";
        public const string ModuleShallowWater = Prefix + "terrain.shallowwater";
        public const string ModuleDeepWater = Prefix + "terrain.deepwater";
        public const string ModuleMarsh = Prefix + "terrain.marsh";
        public const string ModuleTreeConifer = Prefix + "tree.conifer";
        public const string ModuleTreeBroadleaf = Prefix + "tree.broadleaf";

        static readonly string[] ModuleIdTable =
        {
            ModuleGrass,
            ModuleBareEarth,
            ModulePackedGravel,
            ModuleSand,
            ModuleSubsoil,
            ModuleRock,
            ModuleBedrock,
            ModuleIronOre,
            ModuleCoalSeam,
            ModuleShallowWater,
            ModuleDeepWater,
            ModuleMarsh,
            ModuleTreeConifer,
            ModuleTreeBroadleaf,
        };

        /// <summary>Every module id a natural map can ask the catalogue for, in a fixed order.</summary>
        public static IReadOnlyList<string> ModuleIds => ModuleIdTable;

        /// <summary>The module id for a terrain index, or null for air and for city-only terrain.</summary>
        public static string? ModuleForTerrain(ushort terrain)
        {
            switch (terrain)
            {
                case TerrainGrass: return ModuleGrass;
                case TerrainBareEarth: return ModuleBareEarth;
                case TerrainPackedGravel: return ModulePackedGravel;
                case TerrainSand: return ModuleSand;
                case TerrainSubsoil: return ModuleSubsoil;
                case TerrainBedrock: return ModuleBedrock;
                case TerrainIronOre: return ModuleIronOre;
                case TerrainCoalSeam: return ModuleCoalSeam;
                case TerrainShallowWater: return ModuleShallowWater;
                case TerrainDeepWater: return ModuleDeepWater;
                case TerrainMarsh: return ModuleMarsh;
                case CoreContent.TerrainRock: return ModuleRock;
                default: return null;
            }
        }

        public static string? ModuleForEdifice(ushort edifice)
        {
            switch (edifice)
            {
                case EdificeTreeConifer: return ModuleTreeConifer;
                case EdificeTreeBroadleaf: return ModuleTreeBroadleaf;
                default: return null;
            }
        }

        // ---- the terrain table -------------------------------------------------------------

        /// <summary>
        /// The wilderness half of the table, loaded from <c>Defs/Core/World/Terrain.xml</c> like
        /// the city's. This class used to build it in code with the XML mirroring it, which meant
        /// every terrain was written twice and only a test noticed when one copy was forgotten.
        /// </summary>
        public static IReadOnlyList<TerrainDef> Terrain =>
            new ArraySegment<TerrainDef>(WorldContent.Table, FirstTerrain,
                                         WorldContent.Table.Length - FirstTerrain);

        /// <summary>
        /// Looks up any index, core or natural.
        ///
        /// <para>There is no longer a split to dispatch on — the core terrains are simply the
        /// first ten rows of one table, which is what <c>WorldContent.TerrainOrder</c> always
        /// said they were. The branch this replaced was the seam between two hand-written
        /// tables, and with the tables gone the seam goes too.</para>
        /// </summary>
        public static TerrainDef TerrainAt(ushort terrain) => WorldContent.Table[terrain];

        public static bool IsSolid(ushort terrain) => WorldContent.Table[terrain].solid;

        public static bool IsKnown(ushort terrain) => terrain < TerrainCount;

        /// <summary>
        /// Terrain nothing can stand in or on. Only deep water, and only on this table — no core
        /// terrain is impassable, and both waters are already non-solid, so this is an extra
        /// question rather than a correction to <see cref="IsSolid"/>.
        /// </summary>
        public static bool IsImpassable(ushort terrain) =>
            terrain < TerrainCount && WorldContent.Table[terrain].impassable;

        /// <summary>
        /// Ground a colonist can stand on. Marsh is in: it is solid ground, and leaving it out
        /// would make a bog column fail every surface-kind and walkability invariant the
        /// generator asserts. A tree does *not* grow in it — the tree pass asks for grass
        /// specifically, which is why this can stay the broad question it is.
        /// </summary>
        public static bool IsGround(ushort terrain) =>
            terrain == TerrainGrass || terrain == TerrainBareEarth ||
            terrain == TerrainPackedGravel || terrain == TerrainSand ||
            terrain == TerrainMarsh;

        public static bool IsOre(ushort terrain) =>
            terrain == TerrainIronOre || terrain == TerrainCoalSeam;

        /// <summary>Either depth of water. The bed beneath it is ground and answers false.</summary>
        public static bool IsWater(ushort terrain) =>
            terrain == TerrainShallowWater || terrain == TerrainDeepWater;

        // ---- movement cost classes -----------------------------------------------------------
        //
        // The pathfinder charges MoveCost.Orthogonal plus an addend chosen by a per-cell byte
        // (NavGrid.CostClass indexing NavGrid.CostByClass). Worldgen owns which terrain is which
        // class, because terrain is content; navigation owns the per-cell grid. The addends are
        // in hundredths of a flat clear crossing, so +200 is exactly a third of walking speed.

        public const byte CostClassClear = 0;
        public const byte CostClassMarsh = 1;
        public const byte CostClassShallowWater = 2;

        /// <summary>The cost class of a terrain, or <see cref="CostClassClear"/> for most of them.</summary>
        public static byte CostClassOf(ushort terrain)
        {
            if (terrain == TerrainShallowWater) return CostClassShallowWater;
            if (terrain == TerrainMarsh) return CostClassMarsh;
            return CostClassClear;
        }

        /// <summary>
        /// Fills a <c>NavGrid.CostByClass</c> table. Deep water has no class: it is impassable,
        /// and a cost saying "very expensive" is a different and worse thing from one saying "not
        /// at all" — the first still lets a desperate search price a drowning.
        /// </summary>
        public static void ApplyCostClasses(int[] costByClass)
        {
            if (costByClass == null) throw new System.ArgumentNullException(nameof(costByClass));
            costByClass[CostClassClear] = 0;
            costByClass[CostClassMarsh] = 40;            // 140 per cell: boggy, not slow
            costByClass[CostClassShallowWater] = 200;    // 300 per cell: exactly a third speed
        }

        /// <summary>
        /// The cost class a cell is *entered* at, which is not always its own terrain. A wader
        /// enters the water cell itself, so shallow water's class comes from that cell. Someone
        /// crossing a bog enters the **air cell above** the marsh, so marsh's class comes from
        /// the cell below. Getting this the wrong way round gives free marsh and fails silently.
        /// </summary>
        public static byte EntryCostClass(ushort here, ushort below)
        {
            byte own = CostClassOf(here);
            return own != CostClassClear ? own : CostClassOf(below);
        }


        // ---- ore kinds ---------------------------------------------------------------------

        /// <summary>
        /// One minable deposit kind: what it is made of, how often it is chosen, and the band of
        /// depths below the local surface it is found in. Depth weighting is the whole point —
        /// coal sits below iron, so digging deeper is worth doing and the two are not
        /// interchangeable.
        /// </summary>
        public readonly struct OreKind
        {
            public readonly ushort Terrain;
            public readonly int Weight;
            public readonly int MinDepth;
            public readonly int MaxDepth;
            public readonly string ModuleId;

            public OreKind(ushort terrain, int weight, int minDepth, int maxDepth, string moduleId)
            {
                Terrain = terrain;
                Weight = weight;
                MinDepth = minDepth;
                MaxDepth = maxDepth;
                ModuleId = moduleId;
            }
        }

        static readonly OreKind[] OreTable =
        {
            new OreKind(TerrainIronOre, 60, 3, 13, ModuleIronOre),
            new OreKind(TerrainCoalSeam, 40, 7, 22, ModuleCoalSeam),
        };

        /// <summary>The ore kinds, in a fixed order. Index order is part of the determinism contract.</summary>
        public static IReadOnlyList<OreKind> Ores => OreTable;

        public static int OreKindCount => OreTable.Length;

        public static OreKind OreAt(int kind) => OreTable[kind];

        public static int TotalOreWeight()
        {
            int total = 0;
            for (int i = 0; i < OreTable.Length; i++) total += OreTable[i].Weight;
            return total;
        }
    }
}
