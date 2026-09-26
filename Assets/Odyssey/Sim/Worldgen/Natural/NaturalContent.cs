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
    /// <c>Defs/Core/World/Terrain.xml</c> declares all twenty-six kinds,
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

        // ---- deep mining (design 62 §5) ---------------------------------------------------
        //
        // Appended after marsh, so no index a save or a hash already carries moved.

        /// <summary>
        /// The rock below about fourteen layers under the local surface (design 62 §5b): the same
        /// stone as <see cref="TerrainRock"/> at twice the work, and yields stone as rock does.
        /// Rock-like (<see cref="IsRockLike"/>): ore grows in it and caverns are carved from it.
        /// </summary>
        public const ushort TerrainDeepStone = (ushort)(FirstTerrain + 11);

        public const ushort TerrainCopperOre = (ushort)(FirstTerrain + 12);
        public const ushort TerrainGoldOre = (ushort)(FirstTerrain + 13);
        public const ushort TerrainGems = (ushort)(FirstTerrain + 14);

        /// <summary>The deepest find, in bands 18 to 20 only. It glows once discovered.</summary>
        public const ushort TerrainEmberquartz = (ushort)(FirstTerrain + 15);

        /// <summary>One past the last natural index. Sizes any table that must span both sets.</summary>
        public const int TerrainCount = FirstTerrain + 16;

        /// <summary>Stone, shared with the city generator. Ore is grown inside this and deep stone and nothing else.</summary>
        public const ushort TerrainRock = CoreContent.TerrainRock;

        /// <summary>
        /// Host rock: the stone the strata laid — rock or deep stone — which an ore deposit
        /// replaces, a cavern is carved from, and a cut turns into stone (design 62 §5).
        ///
        /// <para><b>Not a second rock-like rule.</b> Rock-like has one owner,
        /// <see cref="Contracts.TerrainHandle.IsRockLike"/>, which the interface asks too; this is
        /// that rule with what cannot host a deposit taken out — anything not solid (rubble), an
        /// ore already there (a deposit never grows over another) and bedrock (never cut). There
        /// used to be three <c>== TerrainRock</c> tests in the generator alone, and deep stone would
        /// have been missed by every one of them.</para>
        /// </summary>
        public static bool IsHostRock(ushort terrain) =>
            Contracts.TerrainHandle.IsRockLike(terrain) && IsSolid(terrain) && !IsOre(terrain) &&
            terrain != TerrainBedrock;

        public const ushort TerrainAir = CoreContent.TerrainAir;

        /// <summary>
        /// Is this terrain one of the natural soils — ground that slumps, spills and grows things,
        /// as against stone, water and pavement?
        ///
        /// <para>Here rather than in presentation, though drawing was the first thing to ask it:
        /// <c>GroundLook.IsEarth</c> now calls this, and so does <see cref="TerraceFoot"/>, which
        /// has to decide whether a terrace step is soil that a bank of earth spills down. Two lists
        /// of six terrain codes would have been two lists to keep in step, and the second one would
        /// have been wrong the first time a soil was added.</para>
        /// </summary>
        public static bool IsEarth(ushort terrain) =>
            terrain == TerrainGrass ||
            terrain == TerrainBareEarth ||
            terrain == TerrainPackedGravel ||
            terrain == TerrainSand ||
            terrain == TerrainSubsoil ||
            terrain == TerrainMarsh;

        // ---- edifices ----------------------------------------------------------------------
        //
        // CoreContent's edifice ids end at EdificeUtilityTap = 9.

        public const ushort FirstEdifice = 10;

        /// <summary>
        /// A tree. Placed in the air cell above the ground it grows from, and **not** blocking:
        /// a colonist walks through woodland, the way RimWorld's plants work, so every surface
        /// cell stays walkable and a forest is a supply of wood rather than a wall.
        ///
        /// <para><b>The species is the simulation's</b> (design 45 §3): which tree stands in a
        /// cell decides its wood and its work, and the art follows the id. The birch and the
        /// meadow tree keep the conifer's and the broadleaf's ten and eleven, so an old save's
        /// trees load as the species its art already showed; the fruit tree and the giant came
        /// later and continue after the buildings, because edifice ids are one space.</para>
        /// </summary>
        public const ushort EdificeTreeBirch = (ushort)(FirstEdifice + 0);

        /// <summary>The meadow tree, the medium broadleaf.</summary>
        public const ushort EdificeTreeMeadow = (ushort)(FirstEdifice + 1);

        /// <summary>A fruit tree. Medium; its fruit is deferred (design 45 §10).</summary>
        public const ushort EdificeTreeFruit = 17;

        /// <summary>The giant meadow tree: one broadleaf in forty, slow, a great deal of wood.</summary>
        public const ushort EdificeTreeGiant = 18;

        /// <summary>
        /// A bush (design 45 §4). Like a tree it stands in the air cell above the grass and blocks
        /// nothing; unlike one it is walked <em>through</em> at a price
        /// (<see cref="CostClassBush"/>) rather than round, and it carries
        /// <c>CellFlags.Undergrowth</c> so navigation can price its cell without the edifice list.
        /// </summary>
        public const ushort EdificeBush = 19;

        /// <summary>A berry bush with its berries on. A kind of bush: everything a bush is, it is.</summary>
        public const ushort EdificeBerryBush = 20;

        /// <summary>A berry bush that has been picked and is growing its berries back.</summary>
        public const ushort EdificeBerryBushPicked = 21;

        /// <summary>One past the highest natural edifice id: the width of a table indexed by one.
        /// <b>Not</b> a range to test against — ids 12 to 16 between are buildings.</summary>
        public const int EdificeLimit = 22;

        public static bool IsTree(ushort edifice) =>
            edifice == EdificeTreeBirch || edifice == EdificeTreeMeadow ||
            edifice == EdificeTreeFruit || edifice == EdificeTreeGiant;

        public static bool IsBush(ushort edifice) =>
            edifice == EdificeBush || edifice == EdificeBerryBush || edifice == EdificeBerryBushPicked;

        /// <summary>
        /// Anything wild that stands in a cell — a tree or a bush. The question every "not a
        /// building" test asks, and the one that replaced the id range the renderer used to
        /// compare against: the natural ids are no longer contiguous, and a range test is how a
        /// shelf once drew as a conifer.
        /// </summary>
        public static bool IsNatural(ushort edifice) => IsTree(edifice) || IsBush(edifice);

        /// <summary>
        /// The Def a wild edifice is made of, or null for anything else. The order of
        /// <c>WorldContent.WildPlantOrder</c> is the order of the ids below, and that pairing is a
        /// save contract like every other content order.
        /// </summary>
        public static WildPlantDef? WildPlantAt(ushort edifice)
        {
            int slot = WildPlantSlot(edifice);
            return slot < 0 ? null : WorldContent.WildPlants[slot];
        }

        /// <summary>The slot in <c>WorldContent.WildPlantOrder</c>, or -1. Parallel to it.</summary>
        public static int WildPlantSlot(ushort edifice)
        {
            switch (edifice)
            {
                case EdificeTreeBirch: return 0;
                case EdificeTreeMeadow: return 1;
                case EdificeTreeFruit: return 2;
                case EdificeTreeGiant: return 3;
                case EdificeBush: return 4;
                case EdificeBerryBush: return 5;
                case EdificeBerryBushPicked: return 5;   // the same plant with its berries off
                default: return -1;
            }
        }

        // ---- stuffs ------------------------------------------------------------------------
        //
        // CoreContent's stuffs end at StuffComposite = 3.

        /// <summary>What a tree is made of, and what felling one yields.</summary>
        public const ushort StuffWood = 4;

        /// <summary>
        /// What mining leaves and what a stone wall is built of.
        ///
        /// <para>A stuff rather than only an item, because a wall has to <i>be</i> made of
        /// something: <c>PlacedEdifice</c> carries a stuff, the mesher tints by it, and the
        /// support solver will one day span by it. Wood was already here for the trees; stone
        /// joins it so that the two things a colony can dig up are the two things it can build
        /// with, with no refining step in between.</para>
        /// </summary>
        public const ushort StuffStone = 5;

        /// <summary>One past the last stuff. The width of any table indexed by one.</summary>
        public const int StuffCount = 6;

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
        public const string ModuleDeepStone = Prefix + "terrain.deepstone";
        public const string ModuleCopperOre = Prefix + "terrain.copperore";
        public const string ModuleGoldOre = Prefix + "terrain.goldore";
        public const string ModuleGems = Prefix + "terrain.gems";
        public const string ModuleEmberquartz = Prefix + "terrain.emberquartz";
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
            ModuleDeepStone,
            ModuleCopperOre,
            ModuleGoldOre,
            ModuleGems,
            ModuleEmberquartz,
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
                case TerrainDeepStone: return ModuleDeepStone;
                case TerrainCopperOre: return ModuleCopperOre;
                case TerrainGoldOre: return ModuleGoldOre;
                case TerrainGems: return ModuleGems;
                case TerrainEmberquartz: return ModuleEmberquartz;
                case CoreContent.TerrainRock: return ModuleRock;
                default: return null;
            }
        }

        public static string? ModuleForEdifice(ushort edifice)
        {
            switch (edifice)
            {
                // The two module ids are art families, not species (design 45 §3): the birch wears
                // the conifer slot's rows and the three broadleaves share the broadleaf slot's,
                // each choosing among its own rows in presentation. The ids are the catalogue's
                // keys, so they keep the names they shipped with.
                case EdificeTreeBirch: return ModuleTreeConifer;
                case EdificeTreeMeadow:
                case EdificeTreeFruit:
                case EdificeTreeGiant: return ModuleTreeBroadleaf;
                // A bush is drawn by the Meadow dressing's own path, which resolves its art itself.
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

        /// <summary>
        /// Is this terrain an ore — one of the kinds in <c>Ores.xml</c>? Read off the ore table,
        /// which is the one owner of what an ore is, through a table built once per load; a new
        /// ore is one XML row and is hidden until discovered, cut for its yield and drawn as stone
        /// without anything here being edited.
        /// </summary>
        public static bool IsOre(ushort terrain)
        {
            int[] kinds = _oreKindByTerrain ?? BuildOreKindByTerrain();
            return terrain < kinds.Length && kinds[terrain] >= 0;
        }

        /// <summary>The ore kind a terrain is, as an index into <see cref="Ores"/>, or -1.</summary>
        public static int OreKindOf(ushort terrain)
        {
            int[] kinds = _oreKindByTerrain ?? BuildOreKindByTerrain();
            return terrain < kinds.Length ? kinds[terrain] : -1;
        }

        static int[]? _oreKindByTerrain;

        static int[] BuildOreKindByTerrain()
        {
            var kinds = new int[TerrainCount];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = -1;
            OreKind[] ores = WorldContent.Ores;
            for (int k = 0; k < ores.Length; k++)
                if (ores[k].Terrain < kinds.Length) kinds[ores[k].Terrain] = k;
            _oreKindByTerrain = kinds;
            return kinds;
        }

        /// <summary>Drop the terrain-to-ore table with the ore table it was built from.</summary>
        internal static void ForgetOres() => _oreKindByTerrain = null;

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

        /// <summary>
        /// The cell at the foot of a terrace step: not a terrain at all, but a **slope**.
        ///
        /// <para><b>Why a cell with grass under it is not clear ground.</b> Presentation fills that
        /// cell with a bank — a ramp from the lower floor to the rim of the step above — and a
        /// colonist crossing it is drawn climbing 1.5 m in the half cell before its centre and
        /// another 1.5 m in the half after. The walk *into* it was priced as flat ground, so that
        /// first half was drawn at <b>1.9 m/s against the 1.5 m/s of walking</b>, and the slowness
        /// began only half way up. Owner, 2026-09-18: <i>"the slowness needs to start happening
        /// much earlier when entering the beginning of the tile … you slow down and then you seem
        /// to still go slow on the flat so it's out of sync."</i></para>
        ///
        /// <para><b>It costs the same in every direction, and that is a decision rather than a
        /// limitation.</b> A cell can carry one cost, so walking *along* the foot of a terrace pays
        /// it too — and should: the figure is drawn part way up a tilted surface the whole way. What
        /// this cannot express is that entering from the high side is a drop rather than a climb,
        /// and that does not matter, because a layer change is priced by
        /// <c>NavGraph.HopCost</c> and never reaches a cell's entry cost.</para>
        ///
        /// <para>Colonists will prefer a flat route one cell away from a terrace edge over walking
        /// along it. That is the intended consequence.</para>
        /// </summary>
        public const byte CostClassSlope = 3;

        /// <summary>
        /// A cell with a bush in it (design 45 §4): walked through, a third slower than grass and
        /// a little worse than a bog. Not a terrain either — the undergrowth is an edifice — so
        /// navigation reads it off <c>CellFlags.Undergrowth</c>, which the bush carries and
        /// <c>CellGrid.RemoveEdifice</c> takes away with it.
        /// </summary>
        public const byte CostClassBush = 4;

        /// <summary>What a bush adds to a flat crossing. INVENTED (design 45 §2).</summary>
        public const int BushExtraCost = 50;

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

            // 240 per cell, and the number is the hop's own. A terrace climb is drawn in two
            // steps — into the foot cell and out of it — and the ramp is split exactly down the
            // middle between them, so the two have to cost the same or the climb changes speed
            // half way up, which is what the owner saw. See MoveCost.JumpUp for where 240 comes
            // from, and docs/design/22-terrace-steps.md §4c for the arithmetic of the pair.
            costByClass[CostClassSlope] = Pathing.MoveCost.SlopeExtra;
            costByClass[CostClassBush] = BushExtraCost;  // 150 per cell: pushing through
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
        /// One minable deposit kind as the generator and the yield read it, resolved from an
        /// <see cref="OreKindDef"/> (design 62 §5c). Everything about an ore is in <c>Ores.xml</c>;
        /// this is that row with its terrain turned into an index.
        /// </summary>
        public readonly struct OreKind
        {
            public readonly ushort Terrain;
            /// <summary>The ItemDef's name. Its handle is resolved where the item table lives (<c>PawnContent.OreYields</c>).</summary>
            public readonly string Item;
            public readonly OreShape Shape;
            public readonly int MinDepth;
            public readonly int MaxDepth;
            public readonly int MinCells;
            public readonly int MaxCells;
            public readonly int DepositsPer10000Columns;
            public readonly int YieldPerCell;
            public readonly int OffBandPerMille;
            public readonly int CaveWallPerMille;
            public readonly bool FavoursDeepStone;
            public readonly string ModuleId;

            public OreKind(ushort terrain, string item, OreShape shape, int minDepth, int maxDepth,
                           int minCells, int maxCells, int depositsPer10000Columns, int yieldPerCell,
                           int offBandPerMille, int caveWallPerMille, bool favoursDeepStone, string moduleId)
            {
                Terrain = terrain;
                Item = item;
                Shape = shape;
                MinDepth = minDepth;
                MaxDepth = maxDepth;
                MinCells = minCells;
                MaxCells = maxCells;
                DepositsPer10000Columns = depositsPer10000Columns;
                YieldPerCell = yieldPerCell;
                OffBandPerMille = offBandPerMille;
                CaveWallPerMille = caveWallPerMille;
                FavoursDeepStone = favoursDeepStone;
                ModuleId = moduleId;
            }
        }

        /// <summary>
        /// The ore kinds, in <c>WorldContent.OreOrder</c>. Index order is part of the determinism
        /// contract. <b>Read from <c>Ores.xml</c></b>: this class used to hold a second copy of the
        /// table in code, and a test compared the two.
        /// </summary>
        public static IReadOnlyList<OreKind> Ores => WorldContent.Ores;

        public static int OreKindCount => WorldContent.Ores.Length;

        public static OreKind OreAt(int kind) => WorldContent.Ores[kind];
    }
}
