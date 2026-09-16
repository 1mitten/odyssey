#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Presentation.World
{
    /// <summary>The module ids one template uses, resolved to library indices once.</summary>
    public struct ModuleGroup
    {
        public int Wall, Door, Window, Pillar, Stair, Ladder, Slab;
    }

    /// <summary>
    /// The renderer's own copy of everything it needs from the cell grid.
    ///
    /// **Why a copy.** The rule the whole architecture rests on is that presentation reads a
    /// published frame and never reaches into simulation state. The published
    /// <see cref="WorldSnapshot"/> carries one byte per cell of one layer, which is the overlay
    /// channel, not geometry; a slice is several layers of walls, slabs and strata. So this is the
    /// geometry channel of the same seam: it is filled by
    /// <see cref="GridMirrorContributor"/> during the simulation's publish phase, at the one point
    /// in the tick where the world is known to be settled, and read by the renderer between ticks.
    /// Nothing in <c>Rendering</c> holds a <see cref="CellGrid"/>.
    ///
    /// **Why it is not rebuilt wholesale.** At the scale target a full copy would be millions of
    /// cells a tick. Refresh is per chunk and driven by <see cref="ChunkGrid"/>'s dirty flags, so
    /// steady state copies nothing at all and a collapse copies one 25 x 25 chunk.
    ///
    /// Module ids stay ids until here. Worldgen names presentation modules by string and never
    /// resolves one; this class turns each id into a <see cref="ModuleLibrary"/> index exactly
    /// once, at construction, so the mesher deals only in integers.
    /// </summary>
    public sealed class WorldRenderModel
    {
        readonly ushort[] _terrain;
        readonly ushort[] _floor;
        readonly ushort[] _floorStuff;
        readonly ushort[] _edifice;
        readonly ushort[] _edificeStuff;
        readonly byte[] _flags;
        readonly ushort[] _slot;

        ModuleGroup[] _groups;
        readonly int[] _terrainModule;
        readonly int[] _naturalEdificeModule;
        readonly int _vaultWallModule;
        readonly int _utilityTapModule;

        public WorldRenderModel(GridSize size, ChunkGrid chunks, ModuleLibrary library)
        {
            Size = size;
            Chunks = chunks;
            Library = library;

            int count = size.CellCount;
            _terrain = new ushort[count];
            _floor = new ushort[count];
            _floorStuff = new ushort[count];
            _edifice = new ushort[count];
            _edificeStuff = new ushort[count];
            _flags = new byte[count];
            _slot = new ushort[count];

            _groups = new[] { ResolveGroup(library, new TemplateDef()) };
            _terrainModule = ResolveTerrain(library);
            _naturalEdificeModule = ResolveNaturalEdifices(library);
            _vaultWallModule = library.Resolve(ModuleIds.VaultWall, ModuleShape.WallPanel);
            _utilityTapModule = library.Resolve(ModuleIds.UtilityTap, ModuleShape.Pillar);
        }

        public GridSize Size { get; }
        public ChunkGrid Chunks { get; }
        public ModuleLibrary Library { get; }

        /// <summary>Bumped whenever any chunk is refreshed, so the renderer can cheaply notice.</summary>
        public int Version { get; private set; }

        /// <summary>Chunks refreshed on the most recent publish. A milestone-report number.</summary>
        public int LastRefreshedChunks { get; private set; }

        // ------------------------------------------------------------ queries

        public int Index(int x, int z, int y) => Size.Index(x, z, y);

        public ushort Terrain(int index) => _terrain[index];

        public bool IsSolid(int index) => (_flags[index] & (byte)CellFlags.SolidTerrain) != 0;

        public bool IsBlocking(int index) => (_flags[index] & (byte)CellFlags.BlockingEdifice) != 0;

        /// <summary>
        /// Does this cell hide the face towards it, so no panel need be drawn there?
        ///
        /// Doors count, which is not obvious and matters: a door is deliberately *not* blocking
        /// for movement, so a movement-based test would wall the doorway up with the neighbouring
        /// wall cell's panel and the door would open onto masonry.
        /// </summary>
        public bool OccludesFace(int index)
        {
            if (IsSolid(index)) return true;
            ushort def = _edifice[index];
            return def == CoreContent.EdificeWall || def == CoreContent.EdificeDoor ||
                   def == CoreContent.EdificeWindow || def == CoreContent.EdificePillar ||
                   def == CoreContent.EdificeVaultWall;
        }

        public ushort Floor(int index) => _floor[index];

        public ushort FloorStuff(int index) => _floorStuff[index];

        public ushort EdificeDef(int index) => _edifice[index];

        public ushort EdificeStuff(int index) => _edificeStuff[index];

        /// <summary>The module index for whatever edifice stands in this cell, or 0.</summary>
        public int EdificeModule(int index)
        {
            ushort def = _edifice[index];
            if (def == CoreContent.EdificeNone) return 0;
            // The natural table continues CoreContent's numbering, as terrain does. A tree is not
            // a kind of wall: before this branch existed every tree fell through the switch below
            // to the wall module and the woodland rendered as a grid of grey boxes.
            if (def >= NaturalContent.FirstEdifice)
                return def < NaturalContent.EdificeCount ? _naturalEdificeModule[def - NaturalContent.FirstEdifice] : 0;
            ref ModuleGroup group = ref _groups[_slot[index]];
            switch (def)
            {
                case CoreContent.EdificeWall: return group.Wall;
                case CoreContent.EdificeDoor: return group.Door;
                case CoreContent.EdificeWindow: return group.Window;
                case CoreContent.EdificePillar: return group.Pillar;
                case CoreContent.EdificeStairLower:
                case CoreContent.EdificeStairUpper: return group.Stair;
                case CoreContent.EdificeLadder: return group.Ladder;
                case CoreContent.EdificeVaultWall: return _vaultWallModule;
                case CoreContent.EdificeUtilityTap: return _utilityTapModule;
                default: return group.Wall;
            }
        }

        /// <summary>The module index for the slab at this cell's lower boundary, or 0.</summary>
        public int FloorModule(int index) =>
            _floor[index] == CoreContent.SlabNone ? 0 : _groups[_slot[index]].Slab;

        /// <summary>The module index for the natural material in this cell, or 0 for open air.</summary>
        public int TerrainModule(int index) => _terrainModule[_terrain[index]];

        /// <summary>
        /// The module index a terrain code draws as, without needing a cell of it to hand.
        ///
        /// The table is keyed by terrain and nothing else, so this is the same answer
        /// <see cref="TerrainModule"/> gives for any cell of that terrain. The surround outside
        /// the board asks it this way because it has no cells at all.
        /// </summary>
        public int TerrainModuleFor(ushort terrain) =>
            terrain < _terrainModule.Length ? _terrainModule[terrain] : 0;

        // ------------------------------------------------------------- filling

        /// <summary>
        /// Record which template stamped which cells, so a cell can use the module ids its own
        /// template authored rather than a global default. Called once, after generation.
        /// </summary>
        public void ApplyTemplates(WorldGenResult result, MapGenDef gen)
        {
            var templates = result.Context.Templates;
            var groups = new List<ModuleGroup>(_groups);
            int firstTemplateGroup = groups.Count;
            for (int t = 0; t < templates.Count; t++)
                groups.Add(ResolveGroup(Library, templates[t].Source));
            _groups = groups.ToArray();

            var shells = result.Shells;
            for (int s = 0; s < shells.Count; s++)
            {
                var shell = shells[s];
                var template = templates[shell.TemplateIndex];
                ushort slot = (ushort)(firstTemplateGroup + shell.TemplateIndex);

                for (int layer = template.BottomLayer; layer <= template.HighestLayer; layer++)
                {
                    int y = gen.groundLayer + layer;
                    if (y < 0 || y >= Size.SizeY) continue;
                    int readLayer = layer > template.TopLayer ? template.TopLayer : layer;

                    for (int tz = 0; tz < template.SizeZ; tz++)
                    for (int tx = 0; tx < template.SizeX; tx++)
                    {
                        if (template.Cell(readLayer, tx, tz) == ShellCellKind.Void) continue;
                        int x = shell.X0 + tx, z = shell.Z0 + tz;
                        if (!Size.Contains(x, z, y)) continue;
                        _slot[Size.Index(x, z, y)] = slot;
                    }
                }
            }
        }

        static ModuleGroup ResolveGroup(ModuleLibrary library, TemplateDef def) => new ModuleGroup
        {
            Wall = library.Resolve(def.wallModuleId, ModuleShape.WallPanel),
            Door = library.Resolve(def.doorModuleId, ModuleShape.WallPanel),
            Window = library.Resolve(def.windowModuleId, ModuleShape.WallPanel),
            Pillar = library.Resolve(def.pillarModuleId, ModuleShape.Pillar),
            Stair = library.Resolve(def.stairModuleId, ModuleShape.StairFlight),
            Ladder = library.Resolve(def.ladderModuleId, ModuleShape.Ladder),
            Slab = library.Resolve(def.slabModuleId, ModuleShape.FloorSlab),
        };

        /// <summary>Tree modules by natural edifice code, offset by <see cref="NaturalContent.FirstEdifice"/>.</summary>
        static int[] ResolveNaturalEdifices(ModuleLibrary library)
        {
            var table = new int[NaturalContent.EdificeCount - NaturalContent.FirstEdifice];
            for (int i = 0; i < table.Length; i++)
            {
                var def = (ushort)(NaturalContent.FirstEdifice + i);
                table[i] = library.Resolve(NaturalContent.ModuleForEdifice(def), ModuleShape.Pillar);
            }
            return table;
        }

        static int[] ResolveTerrain(ModuleLibrary library)
        {
            // Sized for the natural table, which continues CoreContent's numbering rather than
            // replacing it, so this one array covers both map types. Look terrain up through
            // NaturalContent, never CoreContent directly: the city table stops short and would
            // throw on a natural index.
            var table = new int[NaturalContent.TerrainCount];
            for (int i = 0; i < table.Length; i++)
            {
                if (i == CoreContent.TerrainAir) { table[i] = 0; continue; }
                var def = NaturalContent.TerrainAt((ushort)i);
                table[i] = library.Resolve(
                    ModuleIds.Terrain(def.defName),
                    def.solid ? ModuleShape.SolidBlock : ModuleShape.FloorSlab);
            }
            return table;
        }

        // ------------------------------------------------------------ refresh

        /// <summary>Copy every cell. Run once, after generation, before the first frame.</summary>
        public void RefreshAll(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            for (int index = 0; index < _terrain.Length; index++) CopyCell(grid, edifices, index);
            LastRefreshedChunks = Chunks.Count;
            Version++;
        }

        /// <summary>
        /// Copy the chunks the simulation marked dirty and clear the marks.
        ///
        /// Presentation is currently the only consumer of the dirty flags; when navigation or the
        /// save system wants them too, this becomes a per-consumer generation counter rather than
        /// a shared boolean. Written down because the failure mode — one consumer clearing another
        /// consumer's work — is silent.
        /// </summary>
        public int RefreshDirty(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            int refreshed = 0;
            for (int chunk = 0; chunk < Chunks.Count; chunk++)
            {
                if (!Chunks.IsDirty(chunk)) continue;
                RefreshChunk(grid, edifices, chunk);
                Chunks.ClearDirty(chunk);
                refreshed++;
            }
            LastRefreshedChunks = refreshed;
            if (refreshed > 0) Version++;
            return refreshed;
        }

        public void RefreshChunk(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices, int chunkIndex)
        {
            ChunkBounds(chunkIndex, out int x0, out int z0, out int y, out int x1, out int z1);
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
                CopyCell(grid, edifices, Size.Index(x, z, y));
        }

        void CopyCell(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices, int index)
        {
            _terrain[index] = grid.Terrain[index];
            _floor[index] = grid.Floor[index];
            _floorStuff[index] = grid.FloorStuff[index];
            _flags[index] = (byte)grid.Flags[index];

            int handle = grid.Edifice[index];
            if (handle >= 0 && handle < edifices.Count)
            {
                var placed = edifices[handle];
                _edifice[index] = placed.Def;
                _edificeStuff[index] = placed.Stuff;
            }
            else
            {
                _edifice[index] = CoreContent.EdificeNone;
                _edificeStuff[index] = CoreContent.StuffNone;
            }
        }

        /// <summary>Half-open cell bounds of a chunk, and the layer it lives on.</summary>
        public void ChunkBounds(int chunkIndex, out int x0, out int z0, out int y, out int x1, out int z1)
        {
            int cx = chunkIndex % Chunks.ChunksX;
            int rest = chunkIndex / Chunks.ChunksX;
            int cz = rest % Chunks.ChunksZ;
            y = rest / Chunks.ChunksZ;
            x0 = cx * ChunkGrid.ChunkSize;
            z0 = cz * ChunkGrid.ChunkSize;
            x1 = System.Math.Min(x0 + ChunkGrid.ChunkSize, Size.SizeX);
            z1 = System.Math.Min(z0 + ChunkGrid.ChunkSize, Size.SizeZ);
        }

        public int ChunkLayer(int chunkIndex) => chunkIndex / (Chunks.ChunksX * Chunks.ChunksZ);
    }
}
