#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// Natural cell material: rock, fill, soil, pavement, rubble (docs/design/04-data-model.md #3).
    ///
    /// Worldgen stores the *index* of one of these in <see cref="World.CellGrid.Terrain"/>, which
    /// is a <see cref="DefTable{T}"/> index in exactly the sense the Def system means. The table
    /// is loaded from <c>Defs/Core/World/Terrain.xml</c> and reached through
    /// <see cref="WorldContent.Table"/>; <see cref="WorldContent.TerrainOrder"/> is what decides
    /// which name gets which index, and it is not the order the loader stores Defs in.
    /// </summary>
    public class TerrainDef : Def
    {
        /// <summary>Solid material blocks movement and holds up whatever sits on it.</summary>
        public bool solid;

        /// <summary>Ticks of work to clear or mine one cell. Breach is cheaper than mining rock.</summary>
        public int workToClear = 100;

        /// <summary>Percent, 100 = ordinary soil. Nothing grows below 1.</summary>
        public int fertility;

        /// <summary>Relative weight for salvage scattering. 0 = never carries salvage.</summary>
        public int salvageWeight;

        /// <summary>
        /// Neither standable nor stood upon — deep water. Distinct from <see cref="solid"/>,
        /// which blocks the cell but holds up the one above it. Sets
        /// <see cref="World.CellFlags.ImpassableTerrain"/>.
        /// </summary>
        public bool impassable;

        /// <summary>
        /// Whether the colony may put anything here at all. False for water, which needs a
        /// bridge first. Nothing constructs anything yet, so today this is read by placement
        /// validation only; the build pipeline inherits it rather than reopening the question.
        /// </summary>
        public bool buildable = true;

        /// <summary>
        /// May a Mine order clear this, although it is not solid?
        ///
        /// <para>Mining asks for solid terrain, which is right for rock and wrong for the one
        /// thing a collapse leaves behind: rubble is a heap on a floor, not a face to cut, and
        /// without this there was no way to get rid of it (U29). A flag rather than a rule —
        /// "any non-solid terrain with work to clear" would have offered the player a Mine order
        /// on open water, marsh and the soil band under the city, all of which are non-solid and
        /// all of which carry the default workToClear.</para>
        ///
        /// <para>This is `vertical-slice.md`'s U28 line "breach a slab, clear rubble, mine rock",
        /// whose middle speed has been missing since mining landed.</para>
        /// </summary>
        public bool clearable;

        /// <summary>
        /// Whether a bridge may be built over it: the complement of <see cref="buildable"/> for
        /// water, and false for everything else. A bridge spans what cannot be built on, and
        /// bridging solid ground is not a thing.
        /// </summary>
        public bool bridgeable;
    }

    /// <summary>
    /// A building shell authored **in cells**, carrying its own vertical extent
    /// (docs/design/04-data-model.md #17, docs/design/02-world-and-layers.md section 6 pass 3).
    ///
    /// The authoring format is ASCII rows, one character per cell; see <see cref="ShellTemplate"/>
    /// for the character table. Rows are listed layer by layer from <see cref="bottomLayer"/> up
    /// to <see cref="topLayer"/>, with <see cref="sizeZ"/> rows per layer and <see cref="sizeX"/>
    /// characters per row. Layer numbers are relative to street level, so a basement is -1; the
    /// generator adds the ground-layer offset when it stamps.
    ///
    /// Presentation modules are named by **id strings only**. Nothing here resolves a module, so a
    /// clone without the licensed Synty packs loads every template and the simulation runs
    /// headless exactly as it does with them.
    /// </summary>
    public class TemplateDef : Def
    {
        [DefRequired] public int sizeX;
        [DefRequired] public int sizeZ;

        /// <summary>Lowest layer, relative to street level. Zero or negative.</summary>
        public int bottomLayer;

        /// <summary>Highest occupied layer, relative to street level. Zero or positive.</summary>
        public int topLayer;

        /// <summary>Construction material for slabs and edifices. A <see cref="CoreContent"/> stuff name.</summary>
        public string stuff = "Concrete";

        /// <summary>Selection weight when several templates fit a plot.</summary>
        public int weight = 100;

        /// <summary>Lay a roof slab one layer above <see cref="topLayer"/> over the footprint.</summary>
        public bool roof = true;

        /// <summary>
        /// How readily this shell takes damage, in per mille of the district intensity. A sturdy
        /// civic block sits below 1000; a light commercial frontage sits above it.
        /// </summary>
        public int damageTolerance = 1000;

        /// <summary>Presentation module ids, by cell kind. Never resolved inside the simulation.</summary>
        public string wallModuleId = "odyssey.module.wall";
        public string doorModuleId = "odyssey.module.door";
        public string windowModuleId = "odyssey.module.window";
        public string pillarModuleId = "odyssey.module.pillar";
        public string stairModuleId = "odyssey.module.stair";
        public string ladderModuleId = "odyssey.module.ladder";
        public string slabModuleId = "odyssey.module.slab";

        /// <summary>The cell rows, layer by layer. Length must be LayerCount * sizeZ.</summary>
        public List<string> rows = new List<string>();

        public int LayerCount => topLayer - bottomLayer + 1;
    }

    /// <summary>
    /// The ordered pass list with per-pass parameters (docs/design/04-data-model.md #18).
    ///
    /// This doubles as the generator's parameter object: <see cref="WorldGenerator.Generate"/>
    /// takes one. Everything tunable about a map lives here and nothing lives in a constant buried
    /// in a pass, so the slice map and the scale-target map differ only by this record.
    /// </summary>
    public class MapGenDef : Def
    {
        /// <summary>
        /// The array layer index that is street level. Storage is unsigned with a fixed offset
        /// (docs/design/02-world-and-layers.md section 1), and this is that offset: array layer
        /// <c>groundLayer</c> is design layer 0, and design layer -1 is array layer
        /// <c>groundLayer - 1</c>. No pass computes it a second time.
        /// </summary>
        public int groundLayer = 1;

        // ---- pass 1, streets -------------------------------------------------------------
        public int minBlock = 8;
        public int maxBlock = 15;
        public int minStreetWidth = 1;
        public int maxStreetWidth = 2;

        // ---- pass 2, plots ---------------------------------------------------------------
        public int minPlot = 6;
        public int maxPlot = 14;
        public int maxPlotSplitDepth = 3;

        // ---- pass 4, damage --------------------------------------------------------------
        /// <summary>District damage intensity band, per mille. Noise picks a value inside it.</summary>
        public int minDamageIntensity = 120;
        public int maxDamageIntensity = 620;

        /// <summary>Extra damage per storey above street level, per mille of the district value.</summary>
        public int damagePerStorey = 220;

        /// <summary>Chance per mille that a shell is toppled above some layer.</summary>
        public int toppleChance = 180;

        /// <summary>Chance per mille that a removed wall leaves rubble behind.</summary>
        public int rubbleFromWallChance = 520;

        /// <summary>Slab holes are drawn at this fraction, per mille, of the wall-removal chance.</summary>
        public int slabHoleFraction = 380;

        // ---- pass 5, intactness ----------------------------------------------------------
        public int intactnessPeriod = 16;
        public int intactnessOctaves = 3;

        /// <summary>Street columns are biased toward intact pavement by this much, 0..1023.</summary>
        public int streetIntactnessBonus = 190;
        public int pavementThreshold = 700;
        public int crackedThreshold = 480;
        public int rubbleThreshold = 280;

        // ---- pass 6, strata --------------------------------------------------------------
        /// <summary>Depth below street level at which engineered fill starts grading into soil.</summary>
        public int minSoilDepth = 4;
        public int maxSoilDepth = 6;

        /// <summary>Depth below street level at which natural rock starts.</summary>
        public int minRockDepth = 7;
        public int maxRockDepth = 9;

        /// <summary>Threshold, 0..1023, above which a service-stratum street column is a tunnel.</summary>
        public int tunnelThreshold = 430;

        /// <summary>Threshold, 0..1023, above which deep rock is a cave void.</summary>
        public int caveThreshold = 790;

        /// <summary>Threshold, 0..1023, above which deep infrastructure is buried-city seam.</summary>
        public int seamThreshold = 540;

        /// <summary>Half-width of a metro tube, so 1 gives a three-cell bore.</summary>
        public int metroHalfWidth = 1;

        // ---- pass 7, salvage -------------------------------------------------------------
        public int salvageDepositsPer10000Columns = 90;
        public int minSalvageBlob = 6;
        public int maxSalvageBlob = 22;

        // ---- pass 8, utility taps --------------------------------------------------------
        public int utilityTapsPer10000Columns = 14;
        public int minUtilityTaps = 2;
        public int utilityTapSpacing = 10;

        // ---- pass 9, vaults --------------------------------------------------------------
        /// <summary>
        /// a-12-map-generation.md puts the reference density at 0.1 to 0.3 per 10,000 cells per
        /// inhabited layer; with a dozen underground layers that lands here.
        /// </summary>
        public int vaultsPer10000Columns = 2;

        /// <summary>The starting map is guaranteed at least this many, per a-12-map-generation.md.</summary>
        public int minVaults = 1;
        public int vaultSize = 5;

        // ---- pass 10, start --------------------------------------------------------------
        /// <summary>
        /// Support written into every slab worldgen stamps. Pre-existing shells begin life
        /// "supported by construction" (docs/design/02-world-and-layers.md section 4) and are
        /// re-validated by the ordinary rule the moment anything beneath them changes.
        /// </summary>
        public byte constructedSupport = 4;

        // ---- wildlife (design 30 §1) ----------------------------------------------------------

        /// <summary>
        /// The kinds that live on this world, how common each is, how many arrive together and
        /// where they are put. Empty is a world with no animals, which is what the bare board is
        /// on purpose: anything that is not grass on it is a bug.
        /// </summary>
        public Pawns.Wildlife.WildlifeEntry[] wildlife = Array.Empty<Pawns.Wildlife.WildlifeEntry>();

        /// <summary>
        /// The population the board holds, per ten thousand walkable, dry, reachable surface
        /// columns — a census taken of the board as generated, not a number typed for one size.
        /// Reachable is the word that matters: an animal hops only at a drawn ramp, so it can
        /// walk to under half of the meadow's 14,400 columns (6,354 on seed 1), and fifteen per
        /// ten thousand of those is nine or ten animals — about one per 1,500 cells of board.
        /// Zero is no wildlife and no level-keeping at all.
        /// </summary>
        public int wildlifePer10000Columns;

        /// <summary>
        /// The most animals the level-keeper will let a board carry, whatever the census says.
        /// The 64 drawn figures are the colonists' first (design 29 §8, the figure ceiling).
        /// </summary>
        public int wildlifeCeiling = 24;

        /// <summary>The slice map: 60 x 60 x 5 — one service layer, street level, three storeys.</summary>
        public static MapGenDef Slice() => new MapGenDef { defName = "MapGen_Slice", groundLayer = 1 };

        /// <summary>
        /// Parameters scaled to a grid. The ground-layer offset is the only thing that must track
        /// the layer count: a third of the layers underground, capped at twelve, which gives the
        /// L-1 to L-6 strata room and leaves the rest for shells.
        /// </summary>
        public static MapGenDef For(GridSize size)
        {
            var gen = new MapGenDef { defName = "MapGen_" + size };
            gen.groundLayer = Math.Max(1, Math.Min(12, size.SizeY / 3));
            // The ruin's wildlife: rats first, in the rubble, and a few hogs at the middens
            // (design 30 §1). The same density as the meadow; a city block is no emptier.
            gen.wildlife = new[]
            {
                new Pawns.Wildlife.WildlifeEntry("PawnKind_DuctRat", 3, 1, 2, Pawns.Wildlife.Habitat.Rock),
                new Pawns.Wildlife.WildlifeEntry("PawnKind_MiddenHog", 1, 2, 3, Pawns.Wildlife.Habitat.Any),
            };
            gen.wildlifePer10000Columns = 15;
            return gen;
        }

        public void Validate(GridSize size)
        {
            if (groundLayer < 0 || groundLayer >= size.SizeY)
                throw new ArgumentOutOfRangeException(nameof(groundLayer), $"groundLayer {groundLayer} outside {size}.");
            if (minBlock < 2 || maxBlock < minBlock) throw new ArgumentOutOfRangeException(nameof(minBlock));
            if (minStreetWidth < 1 || maxStreetWidth < minStreetWidth) throw new ArgumentOutOfRangeException(nameof(minStreetWidth));
            if (minPlot < 2 || maxPlot < minPlot) throw new ArgumentOutOfRangeException(nameof(minPlot));
            if (minSoilDepth < 1 || maxSoilDepth < minSoilDepth) throw new ArgumentOutOfRangeException(nameof(minSoilDepth));
            if (minRockDepth <= maxSoilDepth || maxRockDepth < minRockDepth) throw new ArgumentOutOfRangeException(nameof(minRockDepth));
            if (vaultSize < 3) throw new ArgumentOutOfRangeException(nameof(vaultSize));
            if (wildlifePer10000Columns < 0) throw new ArgumentOutOfRangeException(nameof(wildlifePer10000Columns));
            if (wildlifeCeiling < 0) throw new ArgumentOutOfRangeException(nameof(wildlifeCeiling));
            for (int i = 0; i < wildlife.Length; i++) wildlife[i].Validate();
        }
    }

    /// <summary>
    /// The core content set worldgen writes: terrain kinds, slab kinds, stuffs and edifice kinds.
    ///
    /// **The terrain table is content** (OQ-16, OQ-49): <c>Defs/Core/World/Terrain.xml</c> holds
    /// these ten and the wilderness's eleven as one table, and <see cref="WorldContent.Table"/> is
    /// what the running game reads. The copy that used to live here, and the comparison that kept
    /// the two honest, are both gone — a fingerprint guards the values now, because with one copy
    /// left there was nothing for the comparison to compare against.
    ///
    /// **The constants below stay, and are not a leftover.** They are compile-time handles: a
    /// terrain index is stored in every cell of every save and folded into every state hash, so it
    /// has to be a number the code can name without loading anything. The declaration order
    /// **is** the table order and is therefore a save-compatibility contract rather than a
    /// convention; <see cref="WorldContent.TerrainOrder"/> restates it, and a test checks every
    /// constant here and in <c>NaturalContent</c> against it.
    ///
    /// The old note here worried that the generator must run headless in a clone with no
    /// <c>Assets/</c> content. That case does not exist: the pack is committed at
    /// <c>Assets/Odyssey/Defs/Core</c>, so every clone has it. The rule it was reaching for is
    /// about <c>Assets/Synty/</c>, which is gitignored, and nothing here touches that.
    /// </summary>
    public static class CoreContent
    {
        // Terrain indices. 0 must remain "nothing", matching CellGrid's zero default.
        public const ushort TerrainAir = 0;
        public const ushort TerrainPavement = 1;
        public const ushort TerrainCrackedPavement = 2;
        public const ushort TerrainRubble = 3;
        public const ushort TerrainSoil = 4;
        public const ushort TerrainGravel = 5;
        public const ushort TerrainFill = 6;
        public const ushort TerrainRock = 7;
        public const ushort TerrainBuriedSeam = 8;
        public const ushort TerrainSalvage = 9;
        public const int TerrainCount = 10;

        // Slab indices. 0 = open, a hole.
        public const ushort SlabNone = 0;
        public const ushort SlabStructural = 1;
        public const ushort SlabDeck = 2;
        public const ushort SlabRoof = 3;

        /// <summary>
        /// A floor the colony built (U29). The three above are all the generator's — structural
        /// decks, plaza decks and roofs, stamped by SurfacePasses and DepthPasses — so a fourth
        /// kind is what makes "take our own floors apart and not the ruined city's" a question
        /// that can be asked rather than guessed at. It is PlacedEdifice.Built's argument one
        /// level down, and it needs no new state: Floor[] is already saved and already hashed.
        ///
        /// Nothing in presentation has to know: WorldRenderModel.FloorModule returns the stuff
        /// group's slab module for any non-zero floor and never looks at the kind.
        /// </summary>
        public const ushort SlabBuilt = 4;

        /// <summary>
        /// A floor covering the colony laid on ground that was already there (U42) — paving, not
        /// structure.
        ///
        /// A fifth kind rather than a reuse of <see cref="SlabBuilt"/>, for the reason SlabBuilt is
        /// not a reuse of the generator's three: it is the only way to ask "is this ours, and is it
        /// holding something up or only being walked on?" A line that strips paving without
        /// touching floors needs that question to exist, and a collapse must never take it — which
        /// it cannot, since a covering over ground is grounded and its support is never zero.
        ///
        /// Costs no new state and nothing in presentation has to know, exactly as SlabBuilt did:
        /// Floor[] is already saved and already hashed, and WorldRenderModel.FloorModule draws any
        /// non-zero floor in its stuff's own tint.
        /// </summary>
        public const ushort SlabPaved = 5;

        // Stuff indices. 0 = none.
        public const ushort StuffNone = 0;
        public const ushort StuffConcrete = 1;
        public const ushort StuffSteel = 2;
        public const ushort StuffComposite = 3;

        // Edifice def indices, stored on the placement record rather than in the cell.
        public const ushort EdificeNone = 0;
        public const ushort EdificeWall = 1;
        public const ushort EdificeDoor = 2;
        public const ushort EdificeWindow = 3;
        public const ushort EdificePillar = 4;
        public const ushort EdificeStairLower = 5;
        public const ushort EdificeStairUpper = 6;
        public const ushort EdificeLadder = 7;
        public const ushort EdificeVaultWall = 8;
        public const ushort EdificeUtilityTap = 9;

        /// <summary>
        /// The first furniture. Not stamped by any generator — it arrives only by
        /// <c>ConstructionGrid.Raise</c>, so a bed in the list is a bed a colonist built, and the
        /// two cells it spans point at the one record (docs/design/20-beds.md §4).
        ///
        /// <para><b>12, not 10.</b> Ten and eleven are the trees' —
        /// <c>NaturalContent.FirstEdifice</c> reserved them the day woodland landed — and the
        /// first cut of the bed took 10 anyway, which would have drawn every bed as a conifer.
        /// Spelled as a literal with this note rather than as
        /// <c>NaturalContent.FirstEdifice + 2</c> because CoreContent does not depend on the
        /// natural tables and a number that can be read beside the ones it must not collide with
        /// is safer than an offset that has to be re-derived.</para>
        /// </summary>
        public const ushort EdificeBed = 12;

        /// <summary>
        /// A shelf: the colony's first buildable <b>store</b>, and the second thing that arrives
        /// only by <c>ConstructionGrid.Raise</c> (docs/design/26-storage.md, the S2 branch).
        ///
        /// <para><b>13, and the numbers it must not collide with are 10 and 11.</b> Those are the
        /// trees' — <c>NaturalContent.FirstEdifice</c> reserved them the day woodland landed — and
        /// 12 is the bed. Spelled as a literal with this note for the reason the bed's own comment
        /// gives: CoreContent does not depend on the natural tables, and a number that can be read
        /// beside the ones it must not collide with is safer than an offset to re-derive.</para>
        /// </summary>
        public const ushort EdificeShelf = 13;

        /// <summary>
        /// The first heat source (design 28 §7). Like the bed, it arrives only by
        /// <c>ConstructionGrid.Raise</c> — nothing stamps it — and it is the one building whose
        /// <c>heatPerPass</c> is not zero, which is the whole reason it exists: Rime is survivable
        /// by shelter and fire, and nothing else. 14, the next free id after the shelf — spelled as
        /// a literal for the same reason the bed's is.
        /// </summary>
        public const ushort EdificeCampfire = 14;

        /// <summary>
        /// The wood-fired generator (design 32 §6): the first thing that makes power, and like the
        /// bed two cells along its facing. 15, the next free id after the campfire, spelled as a
        /// literal for the reason the bed's is.
        /// </summary>
        public const ushort EdificeGenerator = 15;

        /// <summary>The electric heater (design 32 §7): the first thing that spends power.</summary>
        public const ushort EdificeHeater = 16;

        /// <summary>
        /// The city's ten, which are the first ten of the one table. Loaded from
        /// <c>Defs/Core/World/Terrain.xml</c> like everything else: this class used to build them
        /// in code and the XML mirrored it, which meant every terrain was written twice.
        /// </summary>
        public static IReadOnlyList<TerrainDef> Terrain =>
            new System.ArraySegment<TerrainDef>(WorldContent.Table, 0, TerrainCount);

        public static TerrainDef TerrainAt(ushort index) => WorldContent.Table[index];

        public static bool IsSolid(ushort terrain) => WorldContent.Table[terrain].solid;

        public static ushort StuffByName(string name)
        {
            switch (name)
            {
                case "Concrete": return StuffConcrete;
                case "Steel": return StuffSteel;
                case "Composite": return StuffComposite;
                default: throw new DefLoadException($"Unknown stuff '{name}'.");
            }
        }
    }
}
