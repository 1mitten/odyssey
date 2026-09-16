#nullable enable
using System;
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
        readonly int[][] _stoneModule;
        readonly int[][] _turfModule;
        readonly int[][] _earthFaceModule;
        readonly int[][] _bankModule;
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
            _stoneModule = ResolveStone(library);
            _turfModule = ResolveEarth(library, ModuleShape.GroundBlock);
            _earthFaceModule = ResolveEarth(library, ModuleShape.GroundFace);
            _bankModule = ResolveEarth(library, ModuleShape.Bank);
            _naturalEdificeModule = ResolveNaturalEdifices(library);
            _vaultWallModule = library.Resolve(ModuleIds.VaultWall, ModuleShape.WallPanel);
            _utilityTapModule = library.Resolve(ModuleIds.UtilityTap, ModuleShape.Pillar);
        }

        public GridSize Size { get; }
        public ChunkGrid Chunks { get; }
        public ModuleLibrary Library { get; }

        /// <summary>Bumped whenever any chunk is refreshed, so the renderer can cheaply notice.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// The highest layer worth drawing: the top of the geometry, plus the one a colonist
        /// standing on it occupies.
        ///
        /// <para><b>Why the renderer needs a ceiling at all.</b> Above the surface the slice draws
        /// every layer above it solid, with no fade to cut the loop short (owner, 2026-09-16), and
        /// <c>ChunkRenderer.BatchFor</c> <i>meshes</i> a chunk the first time it is asked for and
        /// again after every version bump. Without a bound, a 40-layer map would mesh a dozen
        /// layers of empty sky on every edit and walk them on every frame, for nothing.</para>
        ///
        /// <para>It is a high-water mark: raised as cells are copied in, never lowered except by a
        /// full refresh. That is deliberate and it is the safe direction — the worst it can do is
        /// draw a few empty layers after something is demolished, where the other way round it
        /// would hide a roof somebody had just built.</para>
        /// </summary>
        public int HighestOccupiedLayer { get; private set; }

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

        /// <summary>Is the cell drawn as a chipped lump rather than as a cube?</summary>
        public bool IsStone(int index) => RockLook.IsStone(_terrain[index]);

        /// <summary>
        /// The module for one lump of a stone cell. Falls back to the plain terrain module if the
        /// cell is not stone, so a caller that gets the test wrong draws a cube rather than
        /// nothing at all.
        /// </summary>
        public int StoneModule(int index, int variant)
        {
            int[] variants = _stoneModule[_terrain[index]];
            return variants.Length == 0 ? _terrainModule[_terrain[index]] : variants[variant % variants.Length];
        }
        /// <summary>Is this cell one of the natural soils, drawn as earth rather than as a cube?</summary>
        public bool IsEarth(int index) => GroundLook.IsEarth(_terrain[index]);

        /// <summary>
        /// The module for a piece of earth: the cheap rippled top when nothing can see its sides,
        /// the coursed block when something can.
        ///
        /// <para>Falls back to the plain terrain module when the cell is not a soil, so a caller
        /// that gets the test wrong draws a cube rather than nothing at all — the same courtesy
        /// <see cref="StoneModule"/> extends.</para>
        /// </summary>
        public int EarthModule(int index, int variant, bool showsAFace)
        {
            if (showsAFace) return EarthFaceModule(index, variant, 0b1111);

            int[] variants = _turfModule[_terrain[index]];
            return variants.Length == 0 ? _terrainModule[_terrain[index]] : variants[variant % variants.Length];
        }

        /// <summary>
        /// The block for a piece of earth that shows a side, cut for one canonical pattern of
        /// exposed sides so the chamfer lands only on edges that are open.
        ///
        /// <para>The family is laid out pattern-major, so the five patterns each carry their own
        /// run of variants. A terrain with no earth family falls back to the plain terrain module,
        /// the same courtesy <see cref="StoneModule"/> extends: a caller that gets the test wrong
        /// draws a cube rather than nothing at all.</para>
        /// </summary>
        public int EarthFaceModule(int index, int variant, int canonicalExposure)
        {
            int[] family = _earthFaceModule[_terrain[index]];
            if (family.Length == 0) return _terrainModule[_terrain[index]];

            int pattern = GroundMesh.PatternIndex(canonicalExposure);
            if (pattern < 0) pattern = GroundMesh.ExposurePatterns.Length - 1;

            // The modulo is what lets the family collapse when there is no lip to cut: with the
            // chamfer off every pattern builds the same block, the family is one pattern long, and
            // every pattern folds onto it rather than the mesher having to know that it should.
            int slot = pattern * GroundMesh.Variants + (variant % GroundMesh.Variants);
            return family[slot % family.Length];
        }

        /// <summary>
        /// The stepped bank a terrace of this terrain is climbed by, or 0 when the terrain is not
        /// one a bank is ever built out of.
        ///
        /// <para>Keyed by terrain rather than by cell, because the cell the bank is drawn in is
        /// empty: the terrain that decides what it is made of is the one at the <em>top</em> of the
        /// step, one cell sideways. Returning 0 rather than a substitute is deliberate here — a
        /// bank is decoration, and drawing a cube where one does not belong would be worse than
        /// drawing nothing.</para>
        /// </summary>
        public int BankModuleFor(ushort terrain, int variant)
        {
            if (terrain >= _bankModule.Length) return 0;
            int[] variants = _bankModule[terrain];
            return variants.Length == 0 ? 0 : variants[variant % variants.Length];
        }

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

        /// <summary>
        /// The lumps each stone terrain is drawn with: <c>[terrain][variant]</c>, and empty for
        /// anything that is not stone.
        ///
        /// Resolved once at construction, like everything else here, so the mesher deals only in
        /// integers. Each variant is its own module and so its own instancing bucket, which is
        /// the price of the whole effect: six buckets per stone terrain in a chunk instead of one.
        /// Only *exposed* stone is ever emitted, so on a surface board that is the outcrops and
        /// the terrace faces rather than the eighty thousand cells underneath them.
        /// </summary>
        static int[][] ResolveStone(ModuleLibrary library)
        {
            var table = new int[NaturalContent.TerrainCount][];
            for (int i = 0; i < table.Length; i++)
            {
                if (!RockLook.IsStone((ushort)i)) { table[i] = System.Array.Empty<int>(); continue; }

                string name = NaturalContent.TerrainAt((ushort)i).defName;
                var variants = new int[RockMesh.Variants];
                for (int v = 0; v < variants.Length; v++)
                    variants[v] = library.Resolve(
                        ModuleIds.TerrainVariant(name, v), ModuleShape.RockBlock, v);
                table[i] = variants;
            }
            return table;
        }

        /// <summary>
        /// The earth blocks, one family per soil, resolved once like the stone lumps.
        ///
        /// <para>Every variant borrows the plain terrain row for its material — see
        /// <see cref="ModuleLibrary.Resolve(string, string, ModuleShape, int)"/>. The geometry is
        /// ours and needs no pack; the grass texture lives on exactly one catalogue row and is a
        /// direct reference into <c>Assets/Synty</c>. Borrowing is what lets the number of
        /// variants be a constant in code rather than a shape baked into a generated asset that
        /// can only be rebuilt on a machine holding the licensed packs.</para>
        ///
        /// <para>Turf and face are resolved as separate families rather than as one with twice the
        /// variants, because they are two meshes and the id is what the library caches against.
        /// </para>
        /// </summary>
        static int[][] ResolveEarth(ModuleLibrary library, ModuleShape shape)
        {
            var table = new int[NaturalContent.TerrainCount][];
            // A face family is pattern-major: one run of course variants per pattern of exposed
            // sides, because the chamfer has to be cut for the sides that are really open. Turf
            // collapses to one when there is no ripple to vary, and a bank varies only by its own
            // step jitter.
            int count =
                shape == ModuleShape.Bank ? BankMesh.Kinds
                : shape == ModuleShape.GroundBlock ? GroundMesh.TurfVariants
                : GroundMesh.FaceSlots;
            for (int i = 0; i < table.Length; i++)
            {
                if (!GroundLook.IsEarth((ushort)i)) { table[i] = System.Array.Empty<int>(); continue; }

                string name = NaturalContent.TerrainAt((ushort)i).defName;
                string baseId = ModuleIds.Terrain(name);
                var variants = new int[count];
                for (int v = 0; v < variants.Length; v++)
                {
                    string id;
                    switch (shape)
                    {
                        case ModuleShape.GroundFace: id = ModuleIds.TerrainFace(name, v); break;
                        case ModuleShape.Bank: id = ModuleIds.TerrainBank(name, v); break;
                        default: id = ModuleIds.Terrain(name) + ".turf" + v.ToString(); break;
                    }
                    variants[v] = library.Resolve(id, baseId, shape, v);
                }
                table[i] = variants;
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
                    // Stone asks for a lump even here, where the variant is always the first one.
                    // The catalogue says RockBlock on every row, so this only decides what a
                    // library with *no* catalogue does — and if it answered SolidBlock, that
                    // library would hand back a smooth cube for variant 0 and chipped lumps for
                    // the other five. One cell in six wrong is the kind of fault that renders
                    // perfectly and gets blamed on the art.
                    RockLook.IsStone((ushort)i) ? ModuleShape.RockBlock
                        : def.solid ? ModuleShape.SolidBlock
                        : ModuleShape.FloorSlab);
            }
            return table;
        }

        // ------------------------------------------------------------ refresh

        /// <summary>
        /// Draw every chunk again, without a cell having changed.
        ///
        /// <para>For the drawing decisions that are baked in as a chunk is meshed rather than read
        /// as it is submitted — the grass tufts and the ground relief, which end up in the
        /// instance matrices themselves. Throwing one of those levers at runtime is otherwise
        /// silent: the setting changes and the board keeps showing what it was meshed with.</para>
        ///
        /// <para>It bumps the version and nothing else, so the remesh is lazy and per chunk, and
        /// costs exactly what the renderer already pays after a world edit. No cell is touched, so
        /// nothing here reaches the simulation, the save or the hash.</para>
        /// </summary>
        public void Remesh() => Version++;

        /// <summary>Copy every cell. Run once, after generation, before the first frame.</summary>
        public void RefreshAll(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            // The one place the high-water mark is allowed to fall: everything is being rewritten,
            // so what comes out is exact rather than accumulated.
            HighestOccupiedLayer = 0;
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
            _terrain[index] = Seen(grid, index);
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

            // Anything at all here means this layer is worth drawing, and so is the one above it —
            // that is where a colonist standing on this cell is, and where a roof laid on it goes.
            bool anything = (_flags[index] & (byte)CellFlags.SolidTerrain) != 0
                         || _floor[index] != 0
                         || _edifice[index] != CoreContent.EdificeNone;
            if (!anything) return;

            int layer = index / Size.LayerStride;
            if (layer + 1 > HighestOccupiedLayer)
                HighestOccupiedLayer = Math.Min(Size.SizeY - 1, layer + 1);
        }

        /// <summary>
        /// The terrain as the colony has seen it: an undiscovered seam is plain rock.
        ///
        /// <para>The lie is told once, here, on the way into the mirror — so the module, the
        /// colour, the emissive trim and the inspect readout all agree about what the cell looks
        /// like without any of them knowing there is a rule. The simulation is untouched and
        /// still knows perfectly well that the cell is iron; this is the seam between what is
        /// true and what has been seen, and presentation is the right side of it
        /// (<see cref="CellFlags.Discovered"/>).</para>
        /// </summary>
        static ushort Seen(CellGrid grid, int index)
        {
            ushort terrain = grid.Terrain[index];
            if (NaturalContent.IsOre(terrain) && !grid.IsDiscovered(index)) return NaturalContent.TerrainRock;
            return terrain;
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
