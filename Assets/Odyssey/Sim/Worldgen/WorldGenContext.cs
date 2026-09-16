#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// One generation pass. The passes are the ordered list from
    /// docs/design/02-world-and-layers.md section 6, and each is separately constructible so a
    /// test can run pass 1 alone, or passes 1 to 3, and assert on the intermediate state.
    /// </summary>
    public interface IWorldGenPass
    {
        /// <summary>1 to 10, matching the design document's numbering.</summary>
        int Order { get; }

        string Name { get; }

        void Run(WorldGenContext context);
    }

    /// <summary>
    /// The structural solve that ends generation (section 6, pass 10).
    ///
    /// The implementation is <c>Assets/Odyssey/Sim/World/SupportConsistencyCheck.cs</c>: clear
    /// every construction-trust mark, then solve the whole grid until it reaches a fixed point.
    /// It is what <see cref="StartPass"/> uses when no other check is supplied, so a template that
    /// cannot hold itself up is caught at generation rather than collapsing on tick one. Worldgen
    /// deliberately implements no support rule of its own — the solver is the single definition,
    /// and the generator is one of its callers.
    ///
    /// The seam stays an interface for the two cases that want something else: a test that needs
    /// to observe the call, and a future map type whose consistency means something different.
    /// </summary>
    public interface IStructuralConsistencyCheck
    {
        void Verify(CellGrid grid, WorldGenContext context);
    }

    /// <summary>A rectangular run of cells between streets, before subdivision. Inclusive bounds.</summary>
    public readonly struct Block
    {
        public readonly int X0, Z0, X1, Z1;
        public Block(int x0, int z0, int x1, int z1) { X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; }
        public int SizeX => X1 - X0 + 1;
        public int SizeZ => Z1 - Z0 + 1;
        public override string ToString() => $"block[{X0}..{X1},{Z0}..{Z1}]";
    }

    /// <summary>One buildable parcel, with the template chosen for it. -1 means vacant.</summary>
    public readonly struct Plot
    {
        public readonly int X0, Z0, X1, Z1, TemplateIndex;
        public Plot(int x0, int z0, int x1, int z1, int templateIndex)
        {
            X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; TemplateIndex = templateIndex;
        }
        public int SizeX => X1 - X0 + 1;
        public int SizeZ => Z1 - Z0 + 1;
        public bool IsVacant => TemplateIndex < 0;
        public override string ToString() => $"plot[{X0}..{X1},{Z0}..{Z1}] t{TemplateIndex}";
    }

    /// <summary>
    /// A vertical connector the stamper laid down, with both ends declared.
    ///
    /// Recorded at stamp time rather than searched for later, which is the whole point: a
    /// connector declares both ends and nothing ever hunts for the far one
    /// (<see cref="Pathing.Connector"/>, and the Cataclysm lesson in `c-cataclysm-dda.md`). The
    /// template knows where its stairwell runs; by the time the nav graph is built, that knowledge
    /// would have to be reconstructed from cell contents, which is exactly the search being
    /// avoided.
    ///
    /// <para>A recorded connector is not necessarily a usable one. Damage runs three passes later
    /// and can take the slab out from under a stairwell or topple the storey it served, so the
    /// registrar checks both ends before it declares one.</para>
    /// </summary>
    public readonly struct StampedConnector
    {
        public readonly Pathing.ConnectorKind Kind;

        /// <summary>Cells on the lower layer, and the cells directly above them.</summary>
        public readonly int[] LowerCells;
        public readonly int[] UpperCells;

        public StampedConnector(Pathing.ConnectorKind kind, int[] lowerCells, int[] upperCells)
        {
            Kind = kind;
            LowerCells = lowerCells;
            UpperCells = upperCells;
        }
    }

    /// <summary>A stamped shell: which template, where its origin corner landed, which plot.</summary>
    public readonly struct ShellPlacement
    {
        public readonly int TemplateIndex, X0, Z0, PlotIndex;
        public ShellPlacement(int templateIndex, int x0, int z0, int plotIndex)
        {
            TemplateIndex = templateIndex; X0 = x0; Z0 = z0; PlotIndex = plotIndex;
        }
    }

    /// <summary>
    /// A wall, door, pillar or connector, referenced from <see cref="CellGrid.Edifice"/> by index.
    ///
    /// Worldgen owns this list because the thing registry does not exist yet. Handles are index
    /// positions and are assigned in generation order, which is fixed, so they are part of the
    /// determinism contract and hash identically across processes.
    /// </summary>
    public struct PlacedEdifice
    {
        public int CellIndex;
        public ushort Def;
        public ushort Stuff;

        /// <summary>Set when the damage pass knocks it out. The slot is kept so handles are stable.</summary>
        public bool Removed;
    }

    public readonly struct SalvageDeposit
    {
        public readonly int CellIndex, Cells;
        public SalvageDeposit(int cellIndex, int cells) { CellIndex = cellIndex; Cells = cells; }
    }

    public readonly struct UtilityTap
    {
        public readonly int CellIndex;
        public UtilityTap(int cellIndex) { CellIndex = cellIndex; }
    }

    public readonly struct SealedVault
    {
        public readonly int X0, Z0, Layer, SizeX, SizeZ;
        public SealedVault(int x0, int z0, int layer, int sizeX, int sizeZ)
        {
            X0 = x0; Z0 = z0; Layer = layer; SizeX = sizeX; SizeZ = sizeZ;
        }
    }

    /// <summary>Counts and outcomes, for tests, milestone reports and the generation log.</summary>
    public sealed class WorldGenReport
    {
        public int StreetColumns;
        public int StreetsX;
        public int StreetsZ;
        public int Blocks;
        public int Plots;
        public int VacantPlots;
        public int ShellsStamped;
        public int StampedCells;
        public int DamagedCells;
        public int RemovedEdifices;
        public int SlabHoles;
        public int RubbleCells;
        public int SolidCells;
        public int TunnelCells;
        public int MetroCells;
        public int SeamCells;
        public int CaveCells;
        public int SalvageDeposits;
        public int SalvageCells;
        public int UtilityTaps;
        public int SealedVaults;

        /// <summary>Connectors the stamper laid down, before damage had a chance at them.</summary>
        public int ConnectorsStamped;
        public CellRef StartCell;
        public bool StructuralCheckRan;

        /// <summary>Slabs the pass-10 solve dropped because the damage pass had orphaned them.</summary>
        public int SettledSlabs;

        /// <summary>Solves the map needed to reach a fixed point. Two is the settled case.</summary>
        public int SettleRounds;

        public int PassesRun;

        public override string ToString() =>
            $"streets {StreetColumns}, plots {Plots} ({VacantPlots} vacant), shells {ShellsStamped}, " +
            $"damaged {DamagedCells}, salvage {SalvageDeposits}/{SalvageCells}, taps {UtilityTaps}, " +
            $"vaults {SealedVaults}, start {StartCell}";
    }

    /// <summary>
    /// Everything the passes share. Allocated once per generation; passes write into it in order
    /// and never allocate per cell.
    /// </summary>
    public sealed class WorldGenContext
    {
        readonly ulong[] _claimed;

        public WorldGenContext(CellGrid grid, uint seed, MapGenDef gen, TemplateSet templates)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Templates = templates ?? throw new ArgumentNullException(nameof(templates));
            Gen = gen ?? throw new ArgumentNullException(nameof(gen));
            Seed = seed;
            Size = grid.Size;
            gen.Validate(Size);

            Columns = Size.SizeX * Size.SizeZ;
            IsStreetX = new bool[Size.SizeX];
            IsStreetZ = new bool[Size.SizeZ];
            IsStreet = new bool[Columns];
            Intactness = new int[Columns];
            DistrictIntensity = new int[Columns];
            _claimed = new ulong[(Size.CellCount + 63) / 64];
        }

        public CellGrid Grid { get; }
        public GridSize Size { get; }
        public uint Seed { get; }
        public MapGenDef Gen { get; }
        public TemplateSet Templates { get; }
        public WorldGenReport Report { get; } = new WorldGenReport();

        /// <summary>Cells in one layer. Street and district fields are per column, not per cell.</summary>
        public int Columns { get; }

        /// <summary>Array layer index of street level. The single offset from section 1.</summary>
        public int GroundLayer => Gen.groundLayer;

        /// <summary>Layers available below street level, and above it including the roof.</summary>
        public int LayersBelow => Gen.groundLayer;
        public int LayersAbove => Size.SizeY - 1 - Gen.groundLayer;

        public bool[] IsStreetX { get; }
        public bool[] IsStreetZ { get; }
        public bool[] IsStreet { get; }
        public int[] Intactness { get; }
        public int[] DistrictIntensity { get; }

        public List<Block> Blocks { get; } = new List<Block>();
        public List<Plot> Plots { get; } = new List<Plot>();
        public List<ShellPlacement> Shells { get; } = new List<ShellPlacement>();
        public List<PlacedEdifice> Edifices { get; } = new List<PlacedEdifice>();
        public List<SalvageDeposit> SalvageDeposits { get; } = new List<SalvageDeposit>();
        public List<UtilityTap> UtilityTaps { get; } = new List<UtilityTap>();
        public List<SealedVault> SealedVaults { get; } = new List<SealedVault>();

        /// <summary>Stairs and ladders the stamper laid, in stamp order. See <see cref="StampedConnector"/>.</summary>
        public List<StampedConnector> Connectors { get; } = new List<StampedConnector>();

        /// <summary>The metro tube lines, as the street coordinate each one runs along. May be empty.</summary>
        public int MetroLineX { get; set; } = -1;
        public int MetroLineZ { get; set; } = -1;

        public int Column(int x, int z) => z * Size.SizeX + x;
        public int Index(int x, int z, int y) => Size.Index(x, z, y);

        /// <summary>
        /// The random stream for one pass. Purposes are separated the way the tick streams are, so
        /// that adding a draw to one pass cannot shift the draws every later pass sees — which
        /// would otherwise make any tuning change reroll the entire map.
        /// </summary>
        public DeterministicRandom Random(WorldGenPurpose purpose) =>
            DeterministicRandom.ForTick(Seed, (int)purpose, (uint)purpose * 2654435761u);

        /// <summary>A stream for one item within a pass — one shell, one deposit, one vault.</summary>
        public DeterministicRandom Random(WorldGenPurpose purpose, int item) =>
            DeterministicRandom.ForTick(Seed ^ ((uint)purpose * 2246822519u), item, (uint)purpose);

        // ---- the claimed mask ------------------------------------------------------------
        //
        // A stamped shell owns its cells. Passes that run afterwards and would otherwise write
        // over them — the intactness grid and the strata — consult this instead. It is what lets
        // the design document's pass order stand while keeping the research note's rule that
        // "templates override strata".

        public bool IsClaimed(int index) => (_claimed[index >> 6] & (1UL << (index & 63))) != 0;

        public void Claim(int index) => _claimed[index >> 6] |= 1UL << (index & 63);

        // ---- cell writes -----------------------------------------------------------------

        public void SetTerrain(int index, ushort terrain)
        {
            Grid.Terrain[index] = terrain;
            if (CoreContent.IsSolid(terrain)) Grid.Flags[index] |= CellFlags.SolidTerrain;
            else Grid.Flags[index] &= ~CellFlags.SolidTerrain;
        }

        public void SetSlab(int index, ushort slab, ushort stuff)
        {
            Grid.Floor[index] = slab;
            Grid.FloorStuff[index] = slab == CoreContent.SlabNone ? CoreContent.StuffNone : stuff;
            // Pre-existing structure begins life supported by construction (section 4). The
            // ordinary rule re-validates it the moment anything beneath it changes.
            Grid.Support[index] = slab == CoreContent.SlabNone ? (byte)0 : Gen.constructedSupport;
        }

        public void PlaceEdifice(int index, ushort def, ushort stuff, bool blocking)
        {
            Edifices.Add(new PlacedEdifice { CellIndex = index, Def = def, Stuff = stuff });
            Grid.Edifice[index] = Edifices.Count - 1;
            if (blocking) Grid.Flags[index] |= CellFlags.BlockingEdifice;
            else Grid.Flags[index] &= ~CellFlags.BlockingEdifice;
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
    }

    /// <summary>
    /// Named random streams. The numeric values are part of the save-compatible determinism
    /// contract: changing one reseeds that pass and every map generated afterwards differs.
    /// </summary>
    public enum WorldGenPurpose
    {
        Streets = 1,
        Plots = 2,
        Stamp = 3,
        Damage = 4,
        Intactness = 5,
        Strata = 6,
        Salvage = 7,
        UtilityTaps = 8,
        Vaults = 9,
        Start = 10,
    }
}
