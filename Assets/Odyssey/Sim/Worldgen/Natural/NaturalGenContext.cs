#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>One pass of the wilderness generator. Separately constructible, like the city's.</summary>
    public interface INaturalGenPass
    {
        /// <summary>1 to 9, matching <see cref="NaturalMapGenerator.PassCount"/>.</summary>
        int Order { get; }

        string Name { get; }

        void Run(NaturalGenContext context);
    }

    /// <summary>A tree, as placed. The cell is the **air** cell it stands in, above its ground.</summary>
    public readonly struct TreePlacement
    {
        public readonly int CellIndex;
        public readonly ushort Def;
        public TreePlacement(int cellIndex, ushort def) { CellIndex = cellIndex; Def = def; }
    }

    /// <summary>An above-ground stone formation: the centre column, and how many cells it added.</summary>
    public readonly struct RockOutcrop
    {
        public readonly int CellIndex, Cells, Height;
        public RockOutcrop(int cellIndex, int cells, int height)
        {
            CellIndex = cellIndex; Cells = cells; Height = height;
        }
    }

    /// <summary>
    /// A sealed void carved in the rock: the cell the carve started from, and how many cells it
    /// opened. There is no mouth — nothing connects it to the surface, and a colonist reaches it
    /// only by mining into it.
    /// </summary>
    public readonly struct CavernChamber
    {
        public readonly int CellIndex, Cells;
        public CavernChamber(int cellIndex, int cells) { CellIndex = cellIndex; Cells = cells; }
    }

    /// <summary>A lump of ore grown inside the rock strata. Kind indexes <see cref="NaturalContent.Ores"/>.</summary>
    public readonly struct OreDeposit
    {
        public readonly int CellIndex, Cells, Kind;
        public OreDeposit(int cellIndex, int cells, int kind)
        {
            CellIndex = cellIndex; Cells = cells; Kind = kind;
        }
    }

    /// <summary>Counts and outcomes, for tests, milestone reports and the generation log.</summary>
    public sealed class NaturalGenReport
    {
        public int SurfaceMinY;
        public int SurfaceMaxY;
        public int SolidCells;
        public int AirCells;
        public int GrassCells;
        public int BareEarthCells;
        public int GravelCells;
        public int SandCells;
        public int SubsoilCells;
        public int RockCells;
        public int BedrockCells;
        public int Trees;
        public int Conifers;
        public int Broadleaves;
        public int TreesClearedForStart;
        public int Outcrops;
        public int OutcropCells;
        public int Caverns;
        public int CavernCells;
        public int OreDeposits;
        public int OreCells;

        // Water. Columns and cells are the same number for shallow and deep — water is one cell
        // deep whatever its depth — but both are reported because the passes count different
        // things and a disagreement between them is a bug worth seeing.
        public int WaterShapes;
        public int WaterCells;
        public int ShallowWaterColumns;
        public int DeepWaterColumns;
        public int MarshColumns;
        public int ShallowWaterCells;
        public int DeepWaterCells;
        public int MarshCells;

        /// <summary>Ponds dropped because the ground under them was not one level terrace.</summary>
        public int PondsRejectedForRelief;

        /// <summary>Ponds dropped because they did not fit inside the map with their jitter.</summary>
        public int PondsRejectedForEdge;

        /// <summary>Times the whole water plan was abandoned: the map had no room for a channel.</summary>
        public int WaterShapesAbandoned;

        /// <summary>Shallow crossings cut across a river: by construction, plus any forced.</summary>
        public int Fords;

        /// <summary>Fords the reachability check had to add to reconnect the map. Usually zero.</summary>
        public int ForcedFords;

        /// <summary>Columns the colony can reach from the start, and how many it could hope to.</summary>
        public int ReachableColumns;
        public int WalkableColumns;

        /// <summary>Ore cells by kind, indexed as <see cref="NaturalContent.Ores"/> is.</summary>
        public readonly int[] OreCellsByKind = new int[NaturalContent.OreKindCount];

        public CellRef StartCell;

        /// <summary>Layers the surface spans. 1 is a flat plane; 3 to 5 is the intent.</summary>
        public int SurfaceSpread => SurfaceMaxY - SurfaceMinY + 1;

        public int PassesRun;

        public override string ToString() =>
            $"surface {SurfaceMinY}..{SurfaceMaxY}, grass {GrassCells}, patches " +
            $"{BareEarthCells + GravelCells + SandCells}, trees {Trees}, outcrops {Outcrops}/{OutcropCells}, " +
            $"caverns {Caverns}/{CavernCells}, ore {OreDeposits}/{OreCells}, " +
            $"water {WaterShapes}/{WaterCells} " +
            $"({ShallowWaterCells} shallow, {DeepWaterCells} deep, {MarshCells} marsh, {Fords} fords), " +
            $"start {StartCell}";
    }

    /// <summary>
    /// Everything the wilderness passes share. Allocated once per generation; passes write into it
    /// in order and never allocate per cell.
    ///
    /// The per-column fields are the spine of the generator. A wilderness map is a heightfield
    /// with strata hung beneath it, so almost everything is decided once per column and then
    /// applied down a column — which is also what keeps the one full-grid loop (the strata pass)
    /// to a single array read and a handful of comparisons per cell.
    /// </summary>
    public sealed class NaturalGenContext
    {
        public NaturalGenContext(CellGrid grid, uint seed, NaturalMapGenDef gen)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Gen = gen ?? throw new ArgumentNullException(nameof(gen));
            Seed = seed;
            Size = grid.Size;
            gen.ValidateNatural(Size);

            Columns = Size.SizeX * Size.SizeZ;
            SurfaceY = new int[Columns];
            TopSolidY = new int[Columns];
            SubsoilBaseY = new int[Columns];
            BedrockTopY = new int[Columns];
            HasTree = new bool[Columns];
            Water = new byte[Columns];
            ShoreDistance = new byte[Columns];
        }

        public CellGrid Grid { get; }
        public GridSize Size { get; }
        public uint Seed { get; }
        public NaturalMapGenDef Gen { get; }
        public NaturalGenReport Report { get; } = new NaturalGenReport();

        /// <summary>Cells in one layer. The heightfield and everything derived from it are per column.</summary>
        public int Columns { get; }

        /// <summary>Array layer index of nominal ground level, before relief.</summary>
        public int GroundLayer => Gen.groundLayer;

        /// <summary>Layer of the topmost *soil* cell in each column. The cell above it is walkable.</summary>
        public int[] SurfaceY { get; }

        /// <summary>
        /// Layer of the topmost solid cell in each column — the same as <see cref="SurfaceY"/>
        /// until an outcrop raises it. The invariant the whole generator is built on is that a
        /// column is solid from layer 0 up to this, and air above it: no holes, nothing floating.
        /// </summary>
        public int[] TopSolidY { get; }

        /// <summary>
        /// The lowest subsoil layer in each column: subsoil occupies
        /// <c>[SubsoilBaseY, SurfaceY - 1]</c>. Rock is everything below it and above the bedrock.
        /// </summary>
        public int[] SubsoilBaseY { get; }

        /// <summary>
        /// The first non-bedrock layer in each column, so bedrock is <c>y &lt; BedrockTopY</c>.
        /// Per column rather than one global depth because a shallow map has to compress the stack.
        /// </summary>
        public int[] BedrockTopY { get; }

        /// <summary>Set where a tree stands, so the start pass can find a clearing without a scan.</summary>
        public bool[] HasTree { get; }

        // ---- water, per column ---------------------------------------------------------------
        //
        // Two bytes a column, 125 KB at the scale target against a 5 MB terrain array. The water
        // plan pass writes both and everything afterwards only reads them.

        /// <summary>A <see cref="WaterClass"/> per column: none, shallow, deep or marsh.</summary>
        public byte[] Water { get; }

        /// <summary>
        /// Rings from the nearest shore, for water columns within the shallow bound, and zero
        /// everywhere else — including deep water, which is simply everything the flood did not
        /// reach before it stopped.
        /// </summary>
        public byte[] ShoreDistance { get; }

        /// <summary>The bodies of water this map has, in the order they were stamped.</summary>
        public List<WaterShape> WaterShapes { get; } = new List<WaterShape>();

        // The river's march, recorded so a ford can be cut across it without rediscovering where
        // the channel runs. Null on a map with no river, which is most of them.

        /// <summary>Cross coordinate of the river's centre at each march step.</summary>
        public int[]? RiverCross;

        /// <summary>Half-width of the river at each march step.</summary>
        public int[]? RiverHalfWidth;

        /// <summary>Steps in the river's march: the size of the axis it runs along.</summary>
        public int RiverSteps;

        /// <summary>Whether the river marches along x (so it crosses z) or the other way round.</summary>
        public bool RiverAlongX;

        /// <summary>
        /// Force one march step of the river, and a cell either side of it, to wadeable depth.
        /// This is what guarantees no river ever cuts the colony off from half its map: a ford is
        /// still a channel with real banks, so it costs time to cross and still wants a bridge,
        /// but it is never a wall. Returns the columns changed, so a caller can rewrite them.
        /// </summary>
        public int Ford(int step, int halfLength = 1)
        {
            if (RiverCross == null || RiverHalfWidth == null) return 0;

            int changed = 0;
            for (int s = step - halfLength; s <= step + halfLength; s++)
            {
                if ((uint)s >= (uint)RiverSteps) continue;
                int cross = RiverCross[s], half = RiverHalfWidth[s];
                for (int b = cross - half; b <= cross + half; b++)
                {
                    int x = RiverAlongX ? s : b;
                    int z = RiverAlongX ? b : s;
                    if ((uint)x >= (uint)Size.SizeX || (uint)z >= (uint)Size.SizeZ) continue;

                    int column = Column(x, z);
                    if (Water[column] != (byte)WaterClass.Deep) continue;
                    Water[column] = (byte)WaterClass.Shallow;
                    changed++;
                }
            }

            if (changed > 0) Report.Fords++;
            return changed;
        }

        public List<TreePlacement> Trees { get; } = new List<TreePlacement>();
        public List<RockOutcrop> Outcrops { get; } = new List<RockOutcrop>();
        public List<CavernChamber> Caverns { get; } = new List<CavernChamber>();
        public List<OreDeposit> OreDeposits { get; } = new List<OreDeposit>();

        /// <summary>
        /// Every carved cavern cell, in carve order. The ore pass reads it to hang deposits on
        /// chamber walls, and the consistency check reads it to know which holes were meant.
        ///
        /// A list rather than a set: it is appended in a fixed order and indexed by position, so
        /// nothing here can become an unordered iteration that quietly reorders a map.
        /// </summary>
        public List<int> CavernCells { get; } = new List<int>();

        bool[]? _carved;

        /// <summary>Records a carved cell. The flag array is allocated only if a map has caverns.</summary>
        public void Carve(int index)
        {
            _carved ??= new bool[Size.CellCount];
            if (_carved[index]) return;
            _carved[index] = true;
            CavernCells.Add(index);
            SetTerrain(index, NaturalContent.TerrainAir);
        }

        /// <summary>Was this cell hollowed out by the cavern pass? The one hole the column rule allows.</summary>
        public bool IsCavern(int index) => _carved != null && _carved[index];

        public int Column(int x, int z) => z * Size.SizeX + x;
        public int Index(int x, int z, int y) => Size.Index(x, z, y);

        /// <summary>
        /// The random stream for one pass, separated by purpose exactly as the city generator
        /// separates its own: adding a draw to one pass must not shift the draws every later pass
        /// sees, or tuning the tree density would reroll the ore.
        /// </summary>
        public DeterministicRandom Random(NaturalGenPurpose purpose) =>
            DeterministicRandom.ForTick(Seed ^ 0x4E415455u, (int)purpose, (uint)purpose * 2654435761u);

        /// <summary>A stream for one item within a pass — one outcrop, one deposit.</summary>
        public DeterministicRandom Random(NaturalGenPurpose purpose, int item) =>
            DeterministicRandom.ForTick(Seed ^ ((uint)purpose * 2246822519u) ^ 0x4E415455u, item, (uint)purpose);

        // ---- cell writes -------------------------------------------------------------------
        //
        // These mirror WorldGenContext's, but resolve solidity through NaturalContent so that the
        // natural terrain indices — which CoreContent's table would throw on — are handled.
        // The strata pass does not use SetTerrain: it writes the arrays directly in memory order,
        // because at the scale target that loop runs 2.5 million times and the difference is
        // measurable. Every other pass touches a few thousand cells and uses this.

        public void SetTerrain(int index, ushort terrain)
        {
            Grid.Terrain[index] = terrain;
            if (NaturalContent.IsSolid(terrain)) Grid.Flags[index] |= CellFlags.SolidTerrain;
            else Grid.Flags[index] &= ~CellFlags.SolidTerrain;

            // Deep water is the only terrain that is neither solid nor passable, and the flag has
            // to be cleared as well as set: a pass may write ordinary ground over a cell a
            // previous one made a lake of.
            if (NaturalContent.IsImpassable(terrain)) Grid.Flags[index] |= CellFlags.ImpassableTerrain;
            else Grid.Flags[index] &= ~CellFlags.ImpassableTerrain;
        }

        /// <summary>
        /// Adds an edifice — in this generator, always a tree. Trees do not block: woodland is
        /// walkable, and that keeps "every surface cell is walkable" true of a forested map.
        /// </summary>
        public int PlaceEdifice(int index, ushort def, ushort stuff, bool blocking)
        {
            // The natural map has no shells, so the placement list is only trees. It is still an
            // index-assigned list in generation order, which is fixed, so handles hash identically
            // across processes.
            Edifices.Add(new PlacedEdifice { CellIndex = index, Def = def, Stuff = stuff });
            Grid.Edifice[index] = Edifices.Count - 1;
            if (blocking) Grid.Flags[index] |= CellFlags.BlockingEdifice;
            else Grid.Flags[index] &= ~CellFlags.BlockingEdifice;
            return Edifices.Count - 1;
        }

        public void RemoveEdifice(int index)
        {
            int handle = Grid.Edifice[index];
            if (handle >= 0)
            {
                var placed = Edifices[handle];
                placed.Removed = true;
                Edifices[handle] = placed;
            }
            Grid.Edifice[index] = -1;
            Grid.Flags[index] &= ~CellFlags.BlockingEdifice;
        }

        /// <summary>The placed edifices, referenced from <see cref="CellGrid.Edifice"/> by index.</summary>
        public List<PlacedEdifice> Edifices { get; } = new List<PlacedEdifice>();
    }

    /// <summary>
    /// Named random streams. The numeric values are part of the save-compatible determinism
    /// contract: changing one reseeds that pass and every map generated afterwards differs.
    /// </summary>
    public enum NaturalGenPurpose
    {
        Heightfield = 1,
        Strata = 2,
        Cover = 3,
        Trees = 4,
        Outcrops = 5,
        Ore = 6,
        Start = 7,

        /// <summary>
        /// Water layout: river-or-streams, the paths, the ponds and where the fords go. Appended
        /// rather than inserted, so every pass before it draws exactly what it drew before water
        /// existed and a map generated with <c>water = false</c> is bit-identical to an old one.
        /// </summary>
        Water = 8,

        /// <summary>
        /// Added after the others. The value continues the run rather than being slotted in beside
        /// the pass it belongs to, because these numbers seed the streams: renumbering
        /// <see cref="Ore"/> to make room would reroll every ore deposit on every existing map.
        ///
        /// <para><b>Nine, not eight, and the reason is a merge.</b> This and <see cref="Water"/>
        /// were written on separate branches and both took 8. Two purposes on one value is two
        /// passes drawing from the same stream, which is the determinism leak this enum exists to
        /// prevent. Water landed first and keeps the number it shipped with.</para>
        /// </summary>
        Caverns = 9,
    }
}
