#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Growing;
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
    // IDemolitionCells: the five questions the demolition sounds ask are already this class's own
    // (design 58 §9), so the mirror answers them as it stands.
    public sealed class WorldRenderModel : Odyssey.Hud.IDemolitionCells
    {
        readonly ushort[] _terrain;
        readonly ushort[] _floor;
        readonly ushort[] _floorStuff;
        readonly ushort[] _edifice;
        readonly ushort[] _edificeStuff;
        readonly byte[] _flags;
        readonly ushort[] _slot;

        /// <summary>
        /// How a built thing was turned, mirrored per cell from its record, and — for a bed — which
        /// of its two cells is the head, so the mesher can draw the whole bed once from the head
        /// without asking the world a question on every cell. A bed carries the facing on both
        /// halves and the flag on one.
        ///
        /// <para>The facing is kept for <b>any</b> record rather than for beds alone: a ladder
        /// rotates too since 2026-09-18, and anything that does not rotate stores nought anyway.
        /// A second array for the second rotatable thing would have been two copies of one fact.</para>
        /// </summary>
        readonly byte[] _edificeFacing;
        readonly bool[] _bedHead;

        /// <summary>Whether this cell is its record's head — the one a building drawn once is drawn from (design 32 §14).</summary>
        readonly bool[] _edificeHead;


        /// <summary>The crop standing in each cell as <c>plant handle + 1</c>, or 0 for fallow. A crop is not in the grid — it lives in zone state — so this mirror is fed from the published snapshot, not from a contributor.</summary>
        readonly byte[] _cropPlant;

        /// <summary>The drawn stage of the crop in each cell, 1–3. Meaningless where <see cref="_cropPlant"/> is 0.</summary>
        readonly byte[] _cropStage;

        /// <summary>One module per plant per drawn stage, resolved once at construction: three stages in a row per plant, the order <see cref="CropModule"/> indexes.</summary>
        readonly int[] _cropModules;

        /// <summary>
        /// How many plants stand in a sown cell of each crop — the yield the plot will give,
        /// drawn from the first sprout (owner, 2026-09-18: the number of carrots grown/growing
        /// is the amount in the plot). Free by instancing: more matrices in the same bucket, not
        /// more buckets. Clamped, because a content error of a thousand would draw a thousand.
        /// </summary>
        readonly int[] _plantCounts;

        /// <summary>The plant table, in handle order. Resolved once at construction; injectable for a test.</summary>
        readonly PlantDef[] _plants;

        /// <summary>The cells <see cref="UpdateCrops"/> last stamped, ascending — the merge twin of the next snapshot's crop list.</summary>
        int[] _cropApplied = Array.Empty<int>();
        int _cropAppliedCount;
        int[] _cropScratch = Array.Empty<int>();

        /// <summary>
        /// Whether each cell is in a growing zone — the tilled ground rides this, exactly as a
        /// crop rides the plant channel. Fed from the published snapshot; a zone is authored
        /// state that changes no terrain, so the mirror is the only place the field's ground
        /// exists.
        /// </summary>
        readonly bool[] _zoned;

        /// <summary>The zoned cells <see cref="UpdateZones"/> last stamped, ascending — the merge twin of the next snapshot's zone list.</summary>
        int[] _zoneApplied = Array.Empty<int>();
        int _zoneAppliedCount;
        int[] _zoneScratch = Array.Empty<int>();

        /// <summary>Which cells are inside a storage zone. The twin of <see cref="_zoned"/>, and deliberately a second array: a cell can be neither or either and never both.</summary>
        readonly bool[] _storage;

        /// <summary>The storage cells <see cref="UpdateStorage"/> last stamped, ascending — the merge twin of the next frame's store list.</summary>
        readonly List<int> _storageApplied = new List<int>();



        /// <summary>
        /// The cells holding a building site, and what is going up in each.
        ///
        /// <para><b>Why a set beside the arrays rather than a column in them.</b> Everything else
        /// here is geometry, mirrored per chunk off the <see cref="CellGrid"/> and driven by that
        /// grid's dirty flags. A site is not geometry and does not dirty a chunk: it appears the
        /// moment an order is given, carries progress that changes every tick, and vanishes when
        /// the thing it describes becomes a real wall. Mirroring it through the chunk path would
        /// mean dirtying chunks for something that changes nothing about the mesh.</para>
        ///
        /// <para><b>Why it is here at all, then.</b> Because "what is in this cell" must have one
        /// answer. <see cref="CameraRig.SlicePicker"/> knew four things — an edifice, a built
        /// floor, water, and the block below — and a site was none of them, so a waiting order was
        /// never a pointer target: over open air the ray found nothing and the whole layer went
        /// dead to every tool, and over ground it fell through to the block and handed back the
        /// cell one layer down. The owner reported both halves on 2026-09-18 (<i>"I tried to use
        /// the cancel tool to cancel a slab being built — but it didn't work"</i>). The alternative
        /// was a second pass in the rig that tested the snapshot's site list around the pick, which
        /// is a second implementation of this question and would have drifted from the first; the
        /// forced-order context menu needs to right-click a site too (`15-building.md` §8), so it
        /// would have been written twice.</para>
        ///
        /// <para>Small by nature — a colony's outstanding orders, tens to hundreds — so it is
        /// cleared and refilled from the frame rather than diffed.</para>
        /// </summary>
        readonly Dictionary<int, byte> _sites = new Dictionary<int, byte>();


        ModuleGroup[] _groups;
        readonly int[] _terrainModule;
        readonly int[][] _stoneModule;
        readonly int[][] _turfModule;
        readonly int[][] _earthFaceModule;
        readonly int[][] _bankModule;
        readonly int[] _naturalEdificeModule;
        readonly int[] _slabByStuff;
        readonly int _vaultWallModule;
        readonly int _utilityTapModule;
        readonly int _wallCoreModule;
        readonly int _waterFallModule;
        readonly int _bedModule;
        readonly int _campfireModule;
        readonly int _generatorModule;
        readonly int _heaterModule;
        readonly int _galleyModule;
        readonly int _bedPillowModule;
        readonly int _shelfModule;
        readonly int _sandbagModule;
        readonly int _storeEdgeModule;

        /// <summary>The strip drawn along a stockpile's outer edge. See <c>ChunkMesher.EmitStoreEdge</c>.</summary>
        public int StoreEdgeModule => _storeEdgeModule;

        public WorldRenderModel(GridSize size, ChunkGrid chunks, ModuleLibrary library, PlantDef[]? plants = null)
        {
            Size = size;
            Chunks = chunks;
            _chunkVersion = new int[chunks.Count];
            _dirtyThisRefresh = new int[chunks.Count];
            Library = library;

            int count = size.CellCount;
            _terrain = new ushort[count];
            _floor = new ushort[count];
            _floorStuff = new ushort[count];
            _edifice = new ushort[count];
            _edificeStuff = new ushort[count];
            _flags = new byte[count];
            _slot = new ushort[count];
            _edificeFacing = new byte[count];
            _bedHead = new bool[count];
            _edificeHead = new bool[count];
            _cropPlant = new byte[count];
            _cropStage = new byte[count];
            _zoned = new bool[count];
            _storage = new bool[count];

            // The plant table is content, not world state, and content is written once: the ids
            // come from the Defs the simulation itself loads, so a stage renamed in the XML needs
            // no edit here. A test may hand its own table in; by default the shipped pack's.
            _plants = plants ?? ContentPack.Plants();
            _plantCounts = new int[_plants.Length];
            for (int i = 0; i < _plants.Length; i++)
                _plantCounts[i] = Math.Clamp(_plants[i].yieldCount, 1, 8);
            _cropModules = new int[_plants.Length * 3];
            for (int i = 0; i < _plants.Length; i++)
            {
                _cropModules[i * 3 + 0] = library.Resolve(_plants[i].moduleIdSmall, ModuleShape.Pillow);
                _cropModules[i * 3 + 1] = library.Resolve(_plants[i].moduleIdMedium, ModuleShape.Pillow);
                _cropModules[i * 3 + 2] = library.Resolve(_plants[i].moduleIdLarge, ModuleShape.Pillow);
            }

            _groups = new[] { ResolveGroup(library, new TemplateDef()) };
            _terrainModule = ResolveTerrain(library);
            _stoneModule = ResolveStone(library);
            _turfModule = ResolveEarth(library, ModuleShape.GroundBlock);
            _earthFaceModule = ResolveEarth(library, ModuleShape.GroundFace);
            _bankModule = ResolveEarth(library, ModuleShape.Bank);
            _naturalEdificeModule = ResolveNaturalEdifices(library);
            _slabByStuff = ResolveSlabsByStuff(library);
            _vaultWallModule = library.Resolve(ModuleIds.VaultWall, ModuleShape.WallPanel);
            _utilityTapModule = library.Resolve(ModuleIds.UtilityTap, ModuleShape.Pillar);
            _wallCoreModule = library.Resolve(ModuleIds.WallCore, ModuleShape.SolidBlock);
            _waterFallModule = library.Resolve(ModuleIds.WaterFall, ModuleShape.WaterFall);

            // The bed's placeholder: a plain block module, because the honest stand-in for absent
            // art is a box the tint colours, not a borrowed tree or wall wearing a bed's name.
            // When real two-cell art exists a catalogue row on this id upgrades it everywhere,
            // with no code change — the same deal every other module id already offers.
            _bedModule = library.Resolve(ModuleIds.Bed, ModuleShape.SolidBlock);
            _campfireModule = library.Resolve(ModuleIds.Campfire, ModuleShape.SolidBlock);
            _generatorModule = library.Resolve(ModuleIds.Generator, ModuleShape.SolidBlock);
            _heaterModule = library.Resolve(ModuleIds.Heater, ModuleShape.SolidBlock);
            _galleyModule = library.Resolve(ModuleIds.Galley, ModuleShape.SolidBlock);

            // The pillow is a module of its own so it can be a rounded shape and a linen colour
            // whatever the bed's frame is made of (BedShape, PillowMesh).
            _bedPillowModule = library.Resolve(ModuleIds.BedPillow, ModuleShape.Pillow);
            _shelfModule = library.Resolve(ModuleIds.Shelf, ModuleShape.SolidBlock);
            // Cover (design 53 §7a-bis): one bag, laid as a wall by CoverShape.
            _sandbagModule = library.Resolve(ModuleIds.Sandbags, ModuleShape.Sandbag);
            _storeEdgeModule = library.Resolve(ModuleIds.StoreEdge, ModuleShape.FloorSlab);
        }

        /// <summary>
        /// The block that fills a wall cell behind its face panels, and caps it.
        ///
        /// One module for every wall in the world rather than one per template: it is the mass
        /// inside a wall, it is tinted by the stuff the wall is made of like everything else, and
        /// a template that wanted its own would be asking for a different *material*, which the
        /// tint already carries. See <see cref="ModuleIds.WallCore"/>.
        /// </summary>
        public int WallCoreModule => _wallCoreModule;

        /// <summary>
        /// The sheet a body of water shows where nothing beside it holds it in.
        ///
        /// <para>One module rather than one per depth: the mesh is the same sheet either way
        /// and shallow or deep arrives in the tint, exactly as it does for the surface.</para>
        /// </summary>
        public int WaterFallModule => _waterFallModule;

        public GridSize Size { get; }
        public ChunkGrid Chunks { get; }
        public ModuleLibrary Library { get; }

        /// <summary>Bumped whenever any chunk is refreshed, so the renderer can cheaply notice.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Per chunk, the version its contents were last written at — and what the renderer
        /// compares its meshed batch against.
        ///
        /// <para><b>Because <see cref="Version"/> is one number for the whole board.</b> A single
        /// cell changing — one wall raised — bumped it, and every batch in the world then failed
        /// its equality test and was re-meshed on the next frame it was drawn. Measured on
        /// 2026-09-21: raising one wall on the meadow re-meshed all 45 drawn chunks and cost
        /// <b>12.53 ms</b> in the frame after the raise, against 0.7 ms either side of it — a
        /// visible hitch, and the "little glitch" in the owner's report of that day. With this,
        /// one changed cell re-meshes the chunks that changed.</para>
        ///
        /// <para>A global refresh (<see cref="RefreshAll"/>, <see cref="Remesh"/>) writes the new
        /// version into every entry, which is a few hundred integers and keeps "everything must
        /// be re-meshed" expressible.</para>
        /// </summary>
        public int ChunkVersion(int chunkIndex) => _chunkVersion[chunkIndex];

        readonly int[] _chunkVersion;

        /// <summary>Scratch: which chunks one <see cref="RefreshDirty"/> copied. Never allocates.</summary>
        readonly int[] _dirtyThisRefresh;

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

        /// <summary>
        /// The lowest layer the open landscape reaches: over every column, the layer its topmost
        /// solid cell or floor slab sits on, taking the lowest of those.
        ///
        /// <para><b>Why a floor is needed at all.</b> The board is terraced, so the outdoor surface
        /// spans <c>surfaceRelief * 2 + 1</c> layers and only one of them is ever the active one.
        /// <c>SliceSettings.LowestDrawnLayer</c> cuts the drawn band off <c>belowDepth</c> layers
        /// under the slice, which is right for looking down a shaft and wrong for looking at a
        /// hillside: on the played board (120 x 120 x 16, seed 1) the ground runs L8 to L12, and a
        /// slice one layer above the colony's own cut 6,140 of 14,400 columns away, leaving their
        /// trees hanging over the skybox. That is the owner's report of 2026-09-16 — ground with no
        /// texture and no grass under the low-lying trees — and it is the same argument
        /// <see cref="Odyssey.Presentation.Rendering.TintCode.DaylitBase"/> already makes about the
        /// depth <em>shade</em>, arriving one step earlier: a lower terrace is not ground you are
        /// peering through something at, it is ground.</para>
        ///
        /// <para><b>It is measured once and never moves.</b> The landscape's span is a fact about
        /// the generated board; a hole somebody digs afterwards is exactly the case the depth
        /// budget is for, so a quarry floor five layers down still fades out rather than forcing
        /// every cavern in the map to be drawn while the player stands in a meadow. That is why
        /// this is set by <see cref="RefreshAll"/> alone and is not maintained by
        /// <see cref="RefreshDirty"/>, which is the opposite choice to
        /// <see cref="HighestOccupiedLayer"/> and for the opposite reason: a roof somebody builds
        /// has to appear, a pit somebody digs is already governed.</para>
        /// </summary>
        public int LowestOutdoorLayer { get; private set; }

        /// <summary>Chunks refreshed on the most recent publish. A milestone-report number.</summary>
        public int LastRefreshedChunks { get; private set; }

        // ------------------------------------------------------------ queries

        public int Index(int x, int z, int y) => Size.Index(x, z, y);

        public ushort Terrain(int index) => _terrain[index];

        public bool IsSolid(int index) => (_flags[index] & (byte)CellFlags.SolidTerrain) != 0;

        public bool IsBlocking(int index) => (_flags[index] & (byte)CellFlags.BlockingEdifice) != 0;

        /// <summary>
        /// True if this cell contains an in-cell obstacle such as a tree trunk.
        /// Used by presentation steering to manoeuvre colonists to the side of the tile.
        /// </summary>
        public bool HasObstacle(CellRef cell)
        {
            if (!Size.Contains(cell.X, cell.Z, cell.Y)) return false;
            int index = Size.Index(cell);
            ushort def = _edifice[index];
            if (NaturalContent.IsTree(def)) return true;
            if (cell.Y > 0)
            {
                ushort defBelow = _edifice[index - Size.LayerStride];
                if (NaturalContent.IsTree(defBelow)) return true;
            }
            return false;
        }

        public bool HasObstacle(int index)
        {
            if ((uint)index >= (uint)_edifice.Length) return false;
            ushort def = _edifice[index];
            if (NaturalContent.IsTree(def)) return true;
            int below = index - Size.LayerStride;
            if (below >= 0 && (uint)below < (uint)_edifice.Length)
            {
                ushort defBelow = _edifice[below];
                if (NaturalContent.IsTree(defBelow)) return true;
            }
            return false;
        }

        /// <summary>
        /// Has the colony cut into this cell — is what you can see of it a face somebody made?
        ///
        /// <para>This is <see cref="CellFlags.Discovered"/> read for its other meaning, and the
        /// two are coextensive rather than merely similar: the flag is set by
        /// <c>CellGrid.RevealAround</c>, <c>RevealAround</c> is called from exactly one place
        /// (<c>MineJob.MineCell</c>), and worldgen sets it on nothing at all — not even the ore
        /// lining a cavern nobody has been in. So a solid cell carries it if and only if a
        /// colonist has taken the cell next to it out of the world.</para>
        ///
        /// <para><b>The coupling is worth stating because it could be broken from a distance.</b>
        /// A deep scanner, or any future way of learning what rock is made of without cutting it,
        /// would set the flag on ground nobody has touched and this would quietly start calling
        /// hillsides quarries. If that day comes, the cut face wants a bit of its own and this
        /// method is the one place that changes. <c>BankMeshTests.AQuarryInEarthKeepsItsSheerFace</c>
        /// is what fails.</para>
        ///
        /// <para><b>Prospecting (design 62 §7) is the first such way, and it stays clear of every
        /// reader.</b> A prospect sets the flag on <i>rock-like</i> cells only
        /// (<c>CellGrid.RevealRockWithin</c>), and three of <c>BankLayout</c>'s four readers ask
        /// <c>IsEarth</c> of the same cell first, so a prospected cell never reaches them. The
        /// fourth asks it of the floor under a bank cell, which is the top of the lower terrace —
        /// grass, never rock, on the generated board. A rock floor under a natural bank that a
        /// prospect reached would lose its bank; that is the one residue, and the day it is
        /// reported the cut face gets its own bit (<c>CellFlags</c> has none free today). The
        /// simulation's own answer to "is this a face somebody can work at" is
        /// <c>CellGrid.IsExposedFace</c>, which asks for an open neighbour as well.</para>
        ///
        /// <para><b>The breach into a cavern (design 62 §6) is the second</b>: it marks the walls of
        /// the chamber it reveals, which nobody cut. A chamber is carved in the rock band alone,
        /// sealed under rock, so its walls are rock-like almost always and never under open sky —
        /// nowhere a bank is drawn.</para>
        /// </summary>
        public bool IsCutFace(int index) => (_flags[index] & (byte)CellFlags.Discovered) != 0;

        /// <summary>
        /// Is there nothing at all over this cell — no slab and no solid cell, all the way up?
        ///
        /// <para>What earns a cell the daylight bit, and so exemption from the depth shade. See
        /// <c>TintCode.DaylitBase</c> for why the landscape must not dim: the surface is terraced
        /// across five layers and only one of them is ever the active one.</para>
        ///
        /// <para>A slab is stored on the cell <em>above</em> the boundary it occupies, so the roof
        /// over this cell is the floor of the next one up — which is why the walk starts at
        /// <c>y + 1</c> and asks about that cell's own floor. A blocking edifice is deliberately
        /// not consulted: a wall standing beside you is not a roof over you, and neither is a
        /// tree, so grass in woodland stays lit like the grass beside it.</para>
        ///
        /// <para>The loop looks unbounded and is not. A buried cell answers on its first step,
        /// because the cell above it is solid; a surface cell walks the headroom, which the
        /// generator holds at three layers. Nothing here walks a full column in practice.</para>
        ///
        /// <para>On the model rather than in the mesher because <see cref="Rendering.BankLayout"/>
        /// asks it too, and it is a question about the world and not about a batch.</para>
        /// </summary>
        public bool OpenToTheSky(int index, int y)
        {
            int above = index + Size.LayerStride;

            for (int layer = y + 1; layer < Size.SizeY; layer++, above += Size.LayerStride)
            {
                if (_floor[above] != 0) return false;
                if (IsSolid(above)) return false;
            }

            return true;
        }

        /// <summary>
        /// The module for the crop standing in this cell, at its drawn stage, or 0 for fallow.
        ///
        /// <para>One bucket per species per stage, exactly the argument that made a tree one
        /// bucket per species: a field of one crop at one stage is then a single instanced draw
        /// per chunk, and the stage transitions that re-mesh a chunk change the bucket key only
        /// when the drawn stage actually changes — which is the rule the growth system marks the
        /// chunk by, so a re-mesh never redraws a field that looks the same.</para>
        /// </summary>
        /// <summary>How many plants a sown cell of this cell's crop draws, or 0 where nothing grows.</summary>
        public int CropCount(int index)
        {
            byte plant = _cropPlant[index];
            return plant == 0 ? 0 : _plantCounts[plant - 1];
        }

        public int CropModule(int index)
        {
            byte plant = _cropPlant[index];
            // Stage nought is the seed day: the specks are the plant, and there is no module
            // to draw. Guarded on its own line because it once fell through to the slot
            // arithmetic and would have indexed backwards off the table.
            if (plant == 0 || _cropStage[index] == 0) return 0;
            int slot = (plant - 1) * 3 + _cropStage[index] - 1;
            return slot < _cropModules.Length ? _cropModules[slot] : 0;
        }


        /// <summary>
        /// The terrain this cell's ground should draw as: bare earth where it is zoned and grass
        /// under it — the tile IS the terrain quad, so it is seamless and boolean by construction
        /// (owner, 2026-09-18: "it has a brown tile or not"). Drawn look only; the grid's own
        /// terrain is untouched, and an unzoned cell reverts to what it was. Named DrawnTerrain
        /// because GroundLook is already this namespace's static classifier.
        /// </summary>
        public ushort DrawnTerrain(int index) =>
            IsZoned(index) && _terrain[index] == Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainGrass
                ? Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainBareEarth
                : _terrain[index];
        /// <summary>
        /// Whether this GROUND cell is tilled - which asks the zone one layer up, because a
        /// zone is painted on the air the colonist stands in and the soil it tills is the cell
        /// beneath her feet. The first version asked the mirror at the ground cell itself and
        /// the answer was always no: the tilled-earth swap never fired, the plots' own tufts
        /// never left, and every brown tile the owner had ever seen was the cover overlay
        /// rather than soil (found 2026-09-19, from the owner's flush screenshots and a photo
        /// sheet that kept saying "carrots growing out of grass").
        ///
        /// <para>Asked for its own sake by the tuft pass, beside the drawn-terrain swap: a
        /// clump's mesh reaches past its own cell, and the tufts beside a tilled tile are
        /// pulled off it.</para>
        /// </summary>
        public bool IsZoned(int index)
        {
            int above = index + Size.LayerStride;
            return above < _zoned.Length && _zoned[above];
        }

        /// <summary>
        /// Whether a storage zone covers the cell a colonist would stand in here — asked two ways,
        /// because a store is drawn on two different surfaces and they are a layer apart.
        ///
        /// <para>The terrain quad of a solid ground cell is drawn for the cell <em>below</em> the
        /// one a pawn walks in, exactly as <see cref="IsZoned"/> describes, so it asks one layer
        /// up. A slab is drawn at the lower boundary of the walked cell itself, so it asks that
        /// cell. Getting these the wrong way round is the fault that made every tilled tile the
        /// owner ever saw an overlay rather than soil, and it took a photo sheet to find.</para>
        /// </summary>
        public bool IsStoredAbove(int index)
        {
            int above = index + Size.LayerStride;
            return above < _storage.Length && _storage[above];
        }

        /// <summary>Whether a storage zone covers this cell itself — what a slab at its own floor asks.</summary>
        public bool IsStoredHere(int index) =>
            (uint)index < (uint)_storage.Length && _storage[index];

        /// <summary>
        /// Restamp the storage mirror from the published frame, as a merge walk over two
        /// ascending lists — the twin of <see cref="UpdateZones"/>, and for its reasons: both
        /// sides are in cell-index order by contract, so a warehouse of a thousand cells costs
        /// O(stored) a frame rather than O(board).
        ///
        /// <para>No dirty marks are raised here. The simulation marks the chunk when a cell joins
        /// or leaves a zone and when a zone's priority changes, because it is the only side that
        /// knows which of those changes what is drawn.</para>
        /// </summary>
        public void UpdateStorage(ReadOnlySpan<StoreView> stores)
        {
            int old = 0, now = 0;
            while (old < _storageApplied.Count || now < stores.Length)
            {
                int priorCell = old < _storageApplied.Count ? _storageApplied[old] : int.MaxValue;
                int nowCell = now < stores.Length ? stores[now].CellIndex : int.MaxValue;

                if (priorCell < nowCell)
                {
                    if ((uint)priorCell < (uint)_storage.Length) _storage[priorCell] = false;
                    old++;
                }
                else if (nowCell < priorCell)
                {
                    if ((uint)nowCell < (uint)_storage.Length) _storage[nowCell] = true;
                    now++;
                }
                else
                {
                    old++;
                    now++;
                }
            }

            _storageApplied.Clear();
            for (int i = 0; i < stores.Length; i++) _storageApplied.Add(stores[i].CellIndex);
        }
        /// <summary>
        /// Restamp the crop mirror from the published snapshot's crop channel.
        ///
        /// <para><b>Called between ticks, not contributed.</b> The geometry mirror is filled by a
        /// snapshot contributor because it reads the cell grid; a crop is not in the grid, and the
        /// crop channel already exists on the snapshot the composition root holds. Copying it here
        /// — before the renderer runs, after the tick has published — keeps the crop picture in
        /// step with the crop orders the same frame shows.</para>
        ///
        /// <para><b>A merge walk, not a clear-and-restamp.</b> Both lists are in cell-index order
        /// — the crop channel's contract — so the cells that lost their crop are exactly the ones
        /// the walk passes on one side, and a 2,000-cell field costs O(planted) a frame rather
        /// than O(board). No dirty marks are raised here: the simulation marks the chunk when a
        /// crop is sown, ripens a stage or is taken out, and it knows which stage transitions
        /// change what is drawn and which do not.</para>
        /// </summary>
        public void UpdateCrops(ReadOnlySpan<PlantView> plants)
        {
            if (_cropScratch.Length < plants.Length) _cropScratch = new int[plants.Length];

            int old = 0, now = 0;
            while (old < _cropAppliedCount || now < plants.Length)
            {
                int priorCell = old < _cropAppliedCount ? _cropApplied[old] : int.MaxValue;
                int freshCell = now < plants.Length ? plants[now].CellIndex : int.MaxValue;

                if (priorCell < freshCell)
                {
                    _cropPlant[priorCell] = 0;
                    _cropStage[priorCell] = 0;
                    old++;
                }
                else
                {
                    if (priorCell == freshCell) old++;
                    PlantView view = plants[now];
                    _cropPlant[view.CellIndex] = (byte)(view.Plant + 1);
                    _cropStage[view.CellIndex] = (byte)Math.Clamp((int)view.Stage, 0, 3);
                    _cropScratch[now] = view.CellIndex;
                    now++;
                }
            }

            (_cropApplied, _cropScratch) = (_cropScratch, _cropApplied);
            _cropAppliedCount = plants.Length;
        }

        /// <summary>
        /// Take this frame's zone cells, as <see cref="UpdateCrops"/> takes its plants: a merge
        /// walk over two ascending lists, so a 2,000-cell field costs O(zoned) a frame. No dirty
        /// marks are raised here — the simulation marks the chunk when a cell is zoned or
        /// unzoned, which is exactly when the ground under it changes.
        /// </summary>
        public void UpdateZones(ReadOnlySpan<ZoneView> zones)
        {
            if (_zoneScratch.Length < zones.Length) _zoneScratch = new int[zones.Length];

            int old = 0, now = 0;
            while (old < _zoneAppliedCount || now < zones.Length)
            {
                int priorCell = old < _zoneAppliedCount ? _zoneApplied[old] : int.MaxValue;
                int freshCell = now < zones.Length ? zones[now].CellIndex : int.MaxValue;

                if (priorCell < freshCell)
                {
                    _zoned[priorCell] = false;
                    old++;
                }
                else
                {
                    if (priorCell == freshCell) old++;
                    _zoned[zones[now].CellIndex] = true;
                    _zoneScratch[now] = zones[now].CellIndex;
                    now++;
                }
            }

            (_zoneApplied, _zoneScratch) = (_zoneScratch, _zoneApplied);
            _zoneAppliedCount = zones.Length;
        }

        /// <summary>
        /// Does this cell hide the face towards it, so no panel need be drawn there?
        ///
        /// A solid block, wall, window, pillar or vault occludes. Doors do NOT occlude: a door
        /// is a framed portal through which the adjacent wall panels must form the jambs, rather
        /// than leaving the wall's hollow interior exposed.
        /// </summary>
        public bool OccludesFace(int index)
        {
            if (IsSolid(index)) return true;
            ushort def = _edifice[index];
            return def == CoreContent.EdificeWall ||
                   def == CoreContent.EdificeWindow || def == CoreContent.EdificePillar ||
                   def == CoreContent.EdificeVaultWall;
        }

        public ushort Floor(int index) => _floor[index];

        public ushort FloorStuff(int index) => _floorStuff[index];

        public ushort EdificeDef(int index) => _edifice[index];

        public ushort EdificeStuff(int index) => _edificeStuff[index];

        /// <summary>
        /// Take this frame's building sites. Cleared and refilled, once a frame, from the snapshot.
        ///
        /// <para>Called before anything reads the mirror, so a site ordered on one frame is
        /// clickable on the next. That one frame of lag is the same bargain the sight lines already
        /// make and for the same reason: nothing observable happens in a sixtieth of a second, and
        /// the alternative is the simulation reaching forward into presentation.</para>
        /// </summary>
        public void SetSites(ReadOnlySpan<SiteView> sites)
        {
            _sites.Clear();
            for (int i = 0; i < sites.Length; i++)
                _sites[sites[i].CellIndex] = sites[i].Building;
        }

        /// <summary>Whether an order is waiting to be built in this cell.</summary>
        public bool HasSite(int index) => _sites.ContainsKey(index);

        readonly HashSet<int> _lines = new HashSet<int>();

        /// <summary>
        /// The power lines published this frame (design 32 §14): every order and removal mark, and
        /// the laid lines while they are shown. A line is a thing the player can point at exactly
        /// as a building site is — it has a cell, an order and a pane — so the picker asks here
        /// beside <see cref="HasSite"/>.
        /// </summary>
        public void SetLines(ReadOnlySpan<ConduitView> lines)
        {
            _lines.Clear();
            for (int i = 0; i < lines.Length; i++) _lines.Add(lines[i].CellIndex);
        }

        /// <summary>Whether a line — ordered, marked, or laid and shown — is in this cell.</summary>
        public bool HasLine(int index) => _lines.Contains(index);

        /// <summary>
        /// What is going up in this cell, as a <c>BuildingHandle</c>, or 0 where nothing is.
        /// </summary>
        public byte SiteBuilding(int index) =>
            _sites.TryGetValue(index, out byte building) ? building : (byte)0;

        /// <summary>
        /// Which way a ladder in this cell faces: the direction a climber on it looks *out*, away
        /// from whatever the ladder is fixed to. Its back is <c>Directions.Opposite</c> of this.
        ///
        /// <para><b>One owner, because two systems have to agree about it and neither did.</b>
        /// <c>ChunkMesher.EmitLadder</c> took the first occluding neighbour and fell back to north;
        /// <c>PawnFigureDirector.TryWallBeside</c> scanned for the first solid neighbour in a
        /// different order and fell back to nothing at all. Same piece of geometry, two rules, two
        /// fallbacks — so where nothing occluded, the mesher drew a ladder on the north face and
        /// the climber gave up, and a colonist rose through clear air beside a ladder that was
        /// plainly there (owner, 2026-09-18: <i>"when a colonist goes up a ladder they seem to
        /// levitate"</i>). This is the <c>NavGraph.HopCost</c> lesson in another place: when two
        /// systems must agree about a number and neither owns it, they disagree silently.</para>
        ///
        /// <para><b>Always an answer.</b> A free-standing ladder — one built up through an open
        /// storey, or against slabs rather than rock — has nothing occluding beside it, and the
        /// fallback is north because that is what the mesher has always drawn. A climber can then
        /// hug the face that is really there rather than finding no wall and standing up straight
        /// in mid-air.</para>
        /// </summary>
        public int LadderFacing(int index) => LadderFacing(index, _edificeFacing[index] & 3);

        /// <summary>
        /// The same rule for a ladder that is not there yet: the wall still wins, and
        /// <paramref name="chosen"/> stands in for the rotation a placed one would carry.
        ///
        /// <para>Here so the build cursor and the built ladder cannot disagree about which face it
        /// ends up on — the ghost has no record to read a facing off, and working the rule out a
        /// second time in the cursor is exactly how the mesher and the figure director came to
        /// disagree in the first place.</para>
        /// </summary>
        public int LadderFacing(int index, int chosen)
        {
            CellRef cell = Size.FromIndex(index);
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = cell.X + Directions.DeltaX[dir], nz = cell.Z + Directions.DeltaZ[dir];
                if (Size.Contains(nx, nz, cell.Y) && OccludesFace(Size.Index(nx, nz, cell.Y)))
                    return Directions.Opposite(dir);
            }

            // **Nothing to be fixed to, so the player's own answer** (owner, 2026-09-18: a built
            // ladder in the right cell and on the wrong side, standing in mid-air). This used to be
            // a flat north, which is an arbitrary way for a free-standing ladder to face and the
            // only case where the player could see it was arbitrary. A ladder rotates now, and the
            // rotation is read here rather than everywhere, so the wall still wins wherever there
            // is one: which side of a wall a ladder is bolted to is physics, not preference.
            return chosen & 3;
        }

        /// <summary>
        /// Which way a one-cell machine that stands against a wall faces, 0–3 — the heater today
        /// (design 32 §14c). The mesher and the build cursor both ask here, for the reason
        /// <see cref="LadderFacing(int, int)"/> is one method rather than two.
        ///
        /// <para><b>Its back to a wall when there is one.</b> The player's facing is kept when the
        /// cell behind it is a wall; otherwise the rotate key's next quarter turn that backs on to
        /// one is taken, so in a corner R chooses which wall and in the open R chooses freely. The
        /// ladder's rule lets the wall win outright, which is right for a thing bolted to rock and
        /// wrong here: the owner reported the heater "doesn't rotate", and a rule that ignored R
        /// against every wall would have kept that true.</para>
        /// </summary>
        public int BackedFacing(int index) => BackedFacing(index, _edificeFacing[index] & 3);

        /// <summary>The same for a machine not built yet, with <paramref name="chosen"/> the cursor's facing.</summary>
        public int BackedFacing(int index, int chosen)
        {
            CellRef cell = Size.FromIndex(index);
            for (int turn = 0; turn < Directions.Count; turn++)
            {
                int facing = (chosen + turn) & 3;
                int back = Directions.Opposite(facing);
                int nx = cell.X + Directions.DeltaX[back], nz = cell.Z + Directions.DeltaZ[back];
                if (Size.Contains(nx, nz, cell.Y) && OccludesFace(Size.Index(nx, nz, cell.Y)))
                    return facing;
            }
            return chosen & 3;
        }

        /// <summary>
        /// The direction a door frame and sliding leaf face to align with adjacent walls, 0–3 (<see cref="Directions"/>).
        ///
        /// <para>Facing points along the opening (the walkway), with the frame running perpendicular to it.
        /// When walls stand on opposite sides (e.g. West and East), the doorway opens North/South (yaw 0).
        /// Shared between <see cref="ChunkMesher"/> and <see cref="DoorDirector"/> so the frame
        /// and the sliding leaf cannot disagree on orientation.</para>
        /// </summary>
        public int DoorFacing(int index) =>
            DoorFacing(Size.FromIndex(index).X, Size.FromIndex(index).Z, Size.FromIndex(index).Y, _edificeFacing[index] & 3);

        public int DoorFacing(int index, int chosen)
        {
            CellRef cell = Size.FromIndex(index);
            return DoorFacing(cell.X, cell.Z, cell.Y, chosen);
        }

        public int DoorFacing(int x, int z, int y) =>
            DoorFacing(x, z, y, Size.Contains(x, z, y) ? (_edificeFacing[Size.Index(x, z, y)] & 3) : 0);

        public int DoorFacing(int x, int z, int y, int chosen)
        {
            bool westWall = Size.Contains(x - 1, z, y) && OccludesFace(Size.Index(x - 1, z, y));
            bool eastWall = Size.Contains(x + 1, z, y) && OccludesFace(Size.Index(x + 1, z, y));
            bool northWall = Size.Contains(x, z + 1, y) && OccludesFace(Size.Index(x, z + 1, y));
            bool southWall = Size.Contains(x, z - 1, y) && OccludesFace(Size.Index(x, z - 1, y));

            bool ewWalls = (westWall && eastWall) || ((westWall || eastWall) && !northWall && !southWall);
            bool nsWalls = (northWall && southWall) || ((northWall || southWall) && !westWall && !eastWall);

            if (ewWalls)
            {
                // Wall runs East-West: doorway opening must run North-South.
                // If one side is indoor (roofed) and the other is outdoor (unroofed),
                // the door frame automatically faces the outdoor facade.
                bool southRoofed = Size.Contains(x, z - 1, y) && IsRoofed(x, z - 1, y);
                bool northRoofed = Size.Contains(x, z + 1, y) && IsRoofed(x, z + 1, y);
                if (northRoofed && !southRoofed) return Directions.South;
                if (southRoofed && !northRoofed) return Directions.North;

                // When both or neither side is roofed, respect player's rotation choice if North or South.
                if (chosen == Directions.North || chosen == Directions.South)
                    return chosen;

                return Directions.South;
            }

            if (nsWalls)
            {
                // Wall runs North-South: doorway opening must run East-West.
                bool westRoofed = Size.Contains(x - 1, z, y) && IsRoofed(x - 1, z, y);
                bool eastRoofed = Size.Contains(x + 1, z, y) && IsRoofed(x + 1, z, y);
                if (westRoofed && !eastRoofed) return Directions.East;
                if (eastRoofed && !westRoofed) return Directions.West;

                // When both or neither side is roofed, respect player's rotation choice if East or West.
                if (chosen == Directions.East || chosen == Directions.West)
                    return chosen;

                return Directions.East;
            }

            // At corners, tees or free-standing: respect the player's rotation choice,
            // avoiding facing directly into an immediately adjacent occluding wall.
            int desired = chosen & 3;
            int nx = x + Directions.DeltaX[desired], nz = z + Directions.DeltaZ[desired];
            if (Size.Contains(nx, nz, y) && OccludesFace(Size.Index(nx, nz, y)))
            {
                int opp = Directions.Opposite(desired);
                int ox = x + Directions.DeltaX[opp], oz = z + Directions.DeltaZ[opp];
                if (!Size.Contains(ox, oz, y) || !OccludesFace(Size.Index(ox, oz, y)))
                    return opp;
            }
            return desired;
        }

        public bool IsRoofed(int x, int z, int y)
        {
            for (int aboveY = y + 1; aboveY < Size.SizeY; aboveY++)
            {
                int idx = Size.Index(x, z, aboveY);
                if (IsSolid(idx) || _floor[idx] != CoreContent.SlabNone) return true;
            }
            return false;
        }

        /// <summary>The module a bed's pillow is drawn from — rounded, and tinted as linen.</summary>
        public int BedPillowModule => _bedPillowModule;

        /// <summary>The facing of whatever rotatable thing stands in this cell, 0–3.</summary>
        public byte EdificeFacing(int index) => _edificeFacing[index];

        /// <summary>The facing of the bed in this cell, 0–3. Meaningful only while a bed stands here.</summary>
        public byte BedFacing(int index) => _edificeFacing[index];

        /// <summary>Whether this cell is the head of the bed that stands in it — the half that draws.</summary>
        public bool BedHead(int index) => _bedHead[index];

        /// <summary>Is this cell the head of the record standing in it? True for every one-cell thing.</summary>
        public bool EdificeHead(int index) => _edificeHead[index];

        /// <summary>
        /// The head cell of the bed occupying this one, or -1 where there is no bed.
        ///
        /// <para>Either half answers, because a bed is one record behind two cells and everything
        /// about how it is drawn — the shape, the selection bracket, the sleeper laid in it — is
        /// measured from the head. A four-neighbour look: only a cell directly beside this one can
        /// be the head of a two-cell thing that claims it.</para>
        /// </summary>
        public int BedHeadAt(int index)
        {
            if ((uint)index >= (uint)_bedHead.Length) return -1;
            if (EdificeDef(index) != CoreContent.EdificeBed) return -1;
            if (_bedHead[index]) return index;

            CellRef at = Size.FromIndex(index);
            for (int f = 0; f < Directions.Count; f++)
            {
                int x = at.X + Directions.DeltaX[f], z = at.Z + Directions.DeltaZ[f];
                if (!Size.Contains(x, z, at.Y)) continue;

                int neighbour = Size.Index(x, z, at.Y);
                if (EdificeDef(neighbour) == CoreContent.EdificeBed && _bedHead[neighbour])
                    return neighbour;
            }
            return -1;
        }

        /// <summary>
        /// How far above this cell's floor the thing in it is <b>drawn</b>, in metres, for
        /// anything that stands up without occluding. Zero for everything else.
        ///
        /// <para><b>This is a picking question, not a rendering one.</b> An occluding edifice
        /// fills its cell and the picker meets it head-on; a bed does not, and before 2026-09-19
        /// the only surface it offered a ray was the floor underneath it. At the play camera a
        /// surface 0.70 m up is drawn a quarter of a cell nearer the viewer than that floor, so
        /// where the bed was drawn and where it could be clicked disagreed by a quarter cell —
        /// measured, and reported by the owner as a bed being "really specific" to click.</para>
        ///
        /// <para>A bed is the only thing that answers today, and the shape it answers with is
        /// <see cref="BedShape"/>'s own, because two copies of a height is how one of them gets
        /// corrected on its own — the same argument that put the bed's shape in one place to
        /// begin with. The next non-occluding thing that stands up adds a line here.</para>
        /// </summary>
        public float StandHeight(int index)
        {
            if ((uint)index >= (uint)_edifice.Length) return 0f;
            if (_edifice[index] == CoreContent.EdificeBed) return BedShape.Size.y;

            // **The deck, not the top of the whole thing.** What a player aims at on a shelf is the
            // goods, and the goods stand on the deck; the back lip is 0.26 m above it, which at the
            // play camera's 48° is about a tenth of a cell of drift — in the same direction the
            // bed's own bug went.
            if (_edifice[index] == CoreContent.EdificeShelf) return ShelfShape.DeckTop;

            // **The flames, which are what a player aims at** (design 43, 2026-09-25). A campfire
            // offered only its floor, so a click on its visible body crossed that floor beyond it
            // and a rolling neighbour in front could take the click instead — measured by
            // CampfirePickTests. FireDirector draws the flames at this height; one number.
            if (_edifice[index] == CoreContent.EdificeCampfire) return FireDirector.FlameHeight;

            // Cover (design 53 §7): the top of the bags or the rail, which is what is clicked and
            // what a deconstruct mark sits on.
            if (CoverShape.Draws(_edifice[index])) return CoverShape.Top(_edifice[index]);
            return 0f;
        }

        /// <summary>
        /// How far above this cell's floor an <b>order mark</b> sits, in metres — the one rule for
        /// where the paint on an ordered cell goes.
        ///
        /// <para><b>Written because deconstruct had no mark it could use.</b> Every other standing
        /// order is drawn as a flat plate, and a plate at the floor of a cell with a wall standing
        /// in it is inside the wall. Deconstruct was given a whole-cell outlined box instead, and
        /// the owner reported that back (2026-09-20): <i>"puts down an entire square as the
        /// blueprint to deconstruct … make it mark the tile for deconstruction instead like you
        /// would mark in mining"</i>. Mining works because rock is solid and the mark goes on top
        /// of it; the answer was never a different shape, it was the same shape at the right
        /// height.</para>
        ///
        /// <para><b>Three cases and no list of defs.</b> Anything that fills its cell is marked on
        /// its top face, which is the face you see it from — solid rock for a mine order, and a
        /// wall, door, window, pillar or vault for a deconstruct one, all of them
        /// <see cref="OccludesFace"/>'s own answer. Anything that stands up without filling the
        /// cell is marked on top of itself, which is <see cref="StandHeight"/> and today means a
        /// bed. Everything else is marked on the floor: a tree ordered felled, a ladder, a slab, a
        /// cell waiting to be built in.</para>
        ///
        /// <para>Trees fall through to the floor deliberately, and that is why this asks
        /// <see cref="OccludesFace"/> rather than "is anything here": a fell order is read on the
        /// ground the tree stands in, and lifting it three metres would hang it in the canopy.</para>
        /// </summary>
        public float MarkHeight(int index)
        {
            if ((uint)index >= (uint)_edifice.Length) return 0f;

            // **A shelf is the first thing for which "where is it picked" and "where is its mark"
            // differ.** A deconstruct mark at deck height is buried under a full shelf, so the mark
            // rides the top of the thing while the pick stays on the deck.
            if (_edifice[index] == CoreContent.EdificeShelf) return ShelfShape.Top;
            return OccludesFace(index) ? CellMetrics.SizeY : StandHeight(index);
        }

        /// <summary>
        /// The same, with the walls down or not: a lowered wall is marked on top of its stump
        /// rather than 2.25 m of nothing above it (design 42 §5).
        /// </summary>
        public float MarkHeight(int index, bool lowered) =>
            lowered && Lowers(index) ? CellMetrics.StumpHeight : MarkHeight(index);

        /// <summary>Is what stands in this cell drawn as a stump while the walls are down?</summary>
        public bool Lowers(int index) => (uint)index < (uint)_edifice.Length && Lowers(_edifice[index]);

        /// <summary>
        /// The edifices that walls-down lowers (owner, 2026-09-24): walls and windows, the ruined
        /// city's vault walls, doors and pillars. Natural rock is terrain and is never lowered, and
        /// neither is anything a player walks up to use — a bed, a shelf, a heater.
        /// </summary>
        public static bool Lowers(ushort def) =>
            def == CoreContent.EdificeWall || def == CoreContent.EdificeWindow
            || def == CoreContent.EdificeVaultWall || def == CoreContent.EdificeDoor
            || def == CoreContent.EdificePillar;

        /// <summary>
        /// Does this cell hold something <em>built</em> — a floor slab, or an edifice that is not a
        /// tree or a bush? It is the difference between the upper storey of a house and a hilltop, and the
        /// question walls-down asks of anything standing above the slice (design 42 §5).
        /// </summary>
        public bool IsBuiltAt(int index)
        {
            if ((uint)index >= (uint)_edifice.Length) return false;
            if (_floor[index] != CoreContent.SlabNone) return true;
            ushort def = _edifice[index];
            return def != CoreContent.EdificeNone && !NaturalContent.IsNatural(def);
        }

        /// <summary>
        /// Does this cell rest on the ground — solid terrain directly beneath it? A building's
        /// ground floor does, whether it stands on the slice or up on a terrace; an upper storey
        /// rests on another floor or on the walls below it, and does not.
        /// </summary>
        public bool RestsOnGround(int index)
        {
            int below = index - Size.LayerStride;
            return below >= 0 && IsSolid(below);
        }

        /// <summary>
        /// Is what is built here <em>stacked</em> — an upper storey rather than a ground floor? The
        /// thing walls-down hides above the slice (owner, 2026-09-24, design 42 §3): the first
        /// floor of the house being looked into goes, the house on the terrace next to it stays.
        ///
        /// <para>It asks only about the cell underneath, so it is absolute rather than relative to
        /// the slice, and the mesher can bake it into a bucket. What changes it is terrain changing
        /// underneath, and digging a cell out already marks the chunk above it dirty
        /// (<c>MineJob.MarkChunksAround</c>).</para>
        /// </summary>
        public bool IsStackedAt(int index) => IsBuiltAt(index) && !RestsOnGround(index);

        /// <summary>The module index for whatever edifice stands in this cell, or 0.</summary>
        public int EdificeModule(int index) => ModuleForEdificeAt(index, _edifice[index]);

        /// <summary>
        /// The module a <em>named</em> edifice would draw with in this cell, whether or not one is
        /// standing there.
        ///
        /// <para><see cref="EdificeModule"/>'s twin for the build cursor, and it exists because that
        /// one reads the grid: a ghost is precisely the case where nothing is in the cell yet. The
        /// stuff group still comes from the cell, because a thing is drawn in the material of where
        /// it stands (U43 — before this, a ladder ghosted as a solid wall block, which is the
        /// opposite of "the cursor is the shape of the thing").</para>
        /// </summary>
        public int ModuleForEdificeAt(int index, ushort def)
        {
            if (def == CoreContent.EdificeNone) return 0;
            // The bed first, before the natural range: its id sits above the trees' but it is not
            // one of theirs, and the natural table below would index past itself for it.
            if (def == CoreContent.EdificeBed) return _bedModule;
            // And the shelf, for the identical reason and it is worth saying twice: id 13 is above
            // the trees' 10 and 11, so without this line the natural table below indexes past
            // itself and every shelf in the colony draws as a conifer.
            if (def == CoreContent.EdificeShelf) return _shelfModule;
            if (def == CoreContent.EdificeCampfire) return _campfireModule;
            // The generator and the heater, above the trees' range for the same reason (design 32).
            if (def == CoreContent.EdificeGenerator) return _generatorModule;
            if (def == CoreContent.EdificeHeater) return _heaterModule;
            // The galley (design 48), above the trees' range for the same reason.
            if (def == CoreContent.EdificeGalley) return _galleyModule;
            // Cover (design 53), above the trees' range for the same reason.
            if (def == CoreContent.EdificeSandbags) return _sandbagModule;
            // A built stair (design 63), above the trees' range for the same reason: the city's
            // flight, from the group that governs the cell, so a clone with no packs draws the
            // library's own stair primitive.
            if (def == CoreContent.EdificeStair) return _groups[_slot[index]].Stair;
            // The natural table continues CoreContent's numbering, as terrain does. A tree is not
            // a kind of wall: before this branch existed every tree fell through the switch below
            // to the wall module and the woodland rendered as a grid of grey boxes.
            if (def >= NaturalContent.FirstEdifice)
                return NaturalContent.IsNatural(def) ? _naturalEdificeModule[def] : 0;
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
            _floor[index] == CoreContent.SlabNone ? 0 : SlabModuleFor(index, _floorStuff[index]);

        /// <summary>
        /// The slab module this cell <em>would</em> draw with, whether or not it holds one.
        ///
        /// <para><see cref="FloorModule"/>'s twin for the build cursor: that one answers "what is
        /// drawn here" and returns nothing for an empty cell, which is precisely the cell a ghost
        /// is being drawn in (`19-build-cursor.md`). Same module, same group, one guard
        /// removed.</para>
        /// </summary>
        public int SlabModuleFor(int index, int stuff)
        {
            int art = (uint)stuff < (uint)_slabByStuff.Length ? _slabByStuff[stuff] : 0;
            return art != 0 ? art : _groups[_slot[index]].Slab;
        }

        /// <summary>The module index for the natural material in this cell, or 0 for open air.</summary>
        public int TerrainModule(int index) => _terrainModule[_terrain[index]];

        /// <summary>The module a terrain is drawn with, wherever it is — for drawing one terrain's
        /// surface in another's cell, as the shoreline lays water over a bank (design 38 §24).</summary>
        public int ModuleForTerrain(ushort terrain) =>
            terrain < _terrainModule.Length ? _terrainModule[terrain] : 0;

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
        public int EarthModule(ushort terrain, int variant, bool showsAFace)
        {
            if (showsAFace) return EarthFaceModule(terrain, variant, 0b1111);

            int[] variants = _turfModule[terrain];
            return variants.Length == 0 ? _terrainModule[terrain] : variants[variant % variants.Length];
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
        public int EarthFaceModule(ushort terrain, int variant, int canonicalExposure)
        {
            int[] family = _earthFaceModule[terrain];
            if (family.Length == 0) return _terrainModule[terrain];

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

        /// <summary>
        /// Slab art by material, for the materials that have their own; 0 for the rest.
        ///
        /// <para>Only the materials a colonist can actually build in are listed. The generator's
        /// concrete, steel and composite slabs keep the template's module and draw exactly what
        /// they drew before, so nothing in the city moves.</para>
        ///
        /// <para>A row that resolved to a fallback is dropped rather than kept. An unknown module
        /// id does not resolve to nothing — it resolves to a built-in primitive — so without this
        /// test a clone with no licensed packs would swap the group's slab for a bare block, which
        /// is worse than the wood it replaced.</para>
        /// </summary>
        static int[] ResolveSlabsByStuff(ModuleLibrary library)
        {
            var table = new int[NaturalContent.StuffCount];
            Take(NaturalContent.StuffWood, "wood");
            Take(NaturalContent.StuffStone, "stone");
            return table;

            void Take(ushort stuff, string material)
            {
                int module = library.Resolve(ModuleIds.SlabOf(material), ModuleShape.FloorSlab);
                if (library[module].UsesArt) table[stuff] = module;
            }
        }

        /// <summary>
        /// Tree modules by edifice id, indexed directly and sized to
        /// <see cref="NaturalContent.EdificeLimit"/>: the natural ids are not contiguous (the
        /// buildings sit between the first two trees and the rest, design 45 §2), so a slot is
        /// filled only where <see cref="NaturalContent.IsNatural"/> says it is one of ours. A bush
        /// has no module here — it is drawn by the dressing's own path.
        /// </summary>
        static int[] ResolveNaturalEdifices(ModuleLibrary library)
        {
            var table = new int[NaturalContent.EdificeLimit];
            for (int i = 0; i < table.Length; i++)
            {
                var def = (ushort)i;
                string? id = NaturalContent.IsTree(def) ? NaturalContent.ModuleForEdifice(def) : null;
                table[i] = id == null ? 0 : library.Resolve(id, ModuleShape.Pillar);
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
                    // Water is a surface, not a floor. It asks for a sheet rather than the slab
                    // every other non-solid terrain gets, because a slab has sides and an
                    // underside that water — which writes no depth — cannot afford to draw. See
                    // WaterMesh.
                    RockLook.IsStone((ushort)i) ? ModuleShape.RockBlock
                        : def.solid ? ModuleShape.SolidBlock
                        : NaturalContent.IsWater((ushort)i) ? ModuleShape.WaterSurface
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
        public void Remesh()
        {
            Version++;
            BumpEveryChunk();
        }

        /// <summary>
        /// Draw one chunk again, without a cell having changed: what a dig, a build or a growing crop
        /// costs the renderer, isolated for measurement (the indirect scenery regathers the layer the
        /// chunk is on, design 38 §22). Nothing reaches the simulation, the save or the hash.
        /// </summary>
        public void RemeshChunk(int chunkIndex)
        {
            Version++;
            _chunkVersion[chunkIndex] = Version;
        }

        /// <summary>Stamp the current version on every chunk: everything is to be re-meshed.</summary>
        void BumpEveryChunk()
        {
            for (int i = 0; i < _chunkVersion.Length; i++) _chunkVersion[i] = Version;
        }

        /// <summary>Copy every cell. Run once, after generation, before the first frame.</summary>
        public void RefreshAll(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            // The one place the high-water mark is allowed to fall: everything is being rewritten,
            // so what comes out is exact rather than accumulated.
            HighestOccupiedLayer = 0;
            for (int index = 0; index < _terrain.Length; index++) CopyCell(grid, edifices, index);
            MeasureTheLandscape();
            LastRefreshedChunks = Chunks.Count;
            Version++;
            BumpEveryChunk();
        }

        /// <summary>
        /// Walk every column from the sky down to the first thing in it, and keep the lowest
        /// answer. See <see cref="LowestOutdoorLayer"/> for what it is for and why it is taken
        /// here and nowhere else.
        ///
        /// <para>A slab counts as the top of its column as readily as solid ground does, which
        /// only ever raises a column's answer: a roofed cell is not open landscape and the ground
        /// under it does not need drawing on a hillside's account. A column of pure air
        /// contributes nothing rather than contributing zero.</para>
        ///
        /// <para>It costs the depth of the air above the ground, not the depth of the map — four
        /// or five reads a column on the played board, taken once before the first frame.</para>
        /// </summary>
        void MeasureTheLandscape()
        {
            int lowest = Size.SizeY - 1;
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            for (int y = Size.SizeY - 1; y >= 0; y--)
            {
                int index = Size.Index(x, z, y);
                if (!IsSolid(index) && _floor[index] == CoreContent.SlabNone) continue;
                if (y < lowest) lowest = y;
                break;
            }

            LowestOutdoorLayer = lowest;
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
                _dirtyThisRefresh[refreshed] = chunk;
                refreshed++;
            }
            LastRefreshedChunks = refreshed;
            if (refreshed > 0)
            {
                Version++;
                // The chunks that actually changed, and only those. Stamped after the version
                // moves so the number they carry is the new one.
                for (int i = 0; i < refreshed; i++) _chunkVersion[_dirtyThisRefresh[i]] = Version;

                // **And every chunk beneath them in the same column** (P15, design 38 §20). Whether
                // a cell is open to the sky is a question about its whole column — a ramp, the
                // daylit bit on the ground — while the simulation marks only the 3 x 3 x 3 chunks
                // around an edit. A slab laid five layers up changed the ground below it and nothing
                // re-meshed it. Their cells did not change, so they are not refreshed, only re-meshed;
                // a chunk below the drawn band is never meshed at all, so this costs what is on screen.
                int perLayer = Chunks.ChunksX * Chunks.ChunksZ;
                for (int i = 0; i < refreshed; i++)
                    for (int below = _dirtyThisRefresh[i] - perLayer; below >= 0; below -= perLayer)
                        _chunkVersion[below] = Version;
            }
            return refreshed;
        }

        public void RefreshChunk(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices, int chunkIndex)
        {
            ChunkBounds(chunkIndex, out int x0, out int z0, out int y, out int x1, out int z1);
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                int index = Size.Index(x, z, y);
                ushort was = _edifice[index];
                ushort wasStuff = _edificeStuff[index];
                CopyCell(grid, edifices, index);

                // A tree that was standing and is not any more: felled, or taken by a collapse.
                // Noted for the topple (design 45 §5), which draws it going over where it stood.
                // Only here, on an edit, and never in RefreshAll: a world being loaded or built
                // has no trees falling in it.
                if (NaturalContent.IsTree(was) && _edifice[index] != was && _felled.Count < MaxFelledPending)
                    _felled.Add(new FelledTree(index, was));
                // Any other building gone, with what it was made of — which nothing else can say
                // once it has left the mirror. For the demolition sounds (design 58 §9): a wall
                // broken from whole in one blow was never struck before, so this is the only
                // record of its stuff.
                else if (was != 0 && !NaturalContent.IsTree(was) && _edifice[index] != was
                         && _removed.Count < MaxFelledPending)
                    _removed.Add(new RemovedEdifice(index, was, wasStuff));
            }
        }

        /// <summary>
        /// How high the bush drawn in this cell stands, in metres over its floor — what a click
        /// on it is measured against (design 45 §12). Written by the mesher when it draws the bush,
        /// because only the mesher knows which art and what size; until then, and for a bush with
        /// no art, <see cref="DefaultBushTop"/>.
        /// </summary>
        public float BushTop(int index) => _bushTop.TryGetValue(index, out float top) ? top : DefaultBushTop;

        /// <summary>The mesher's note of a drawn bush's height. Presentation only.</summary>
        public void NoteBushTop(int index, float top) => _bushTop[index] = top;

        /// <summary>A bush's height before its art has been measured: about the Meadow bushes' middle.</summary>
        public const float DefaultBushTop = 1.4f;

        readonly Dictionary<int, float> _bushTop = new Dictionary<int, float>();

        /// <summary>A tree that has just left the mirror: where it stood and what it was.</summary>
        public readonly struct FelledTree
        {
            public readonly int Cell;
            public readonly ushort Def;
            public FelledTree(int cell, ushort def) { Cell = cell; Def = def; }
        }

        /// <summary>
        /// More felled trees than this between two frames are not drawn falling. A clear-cut of a
        /// wood by a debug command or a collapse is not something anybody watches tree by tree,
        /// and the list must not grow without a reader.
        /// </summary>
        public const int MaxFelledPending = 64;

        readonly List<FelledTree> _felled = new List<FelledTree>();

        /// <summary>Hand over the trees felled since the last call, oldest first, and forget them.</summary>
        public void DrainFelled(List<FelledTree> into)
        {
            into.AddRange(_felled);
            _felled.Clear();
        }

        /// <summary>A building other than a tree that has just left the mirror, and its stuff.</summary>
        public readonly struct RemovedEdifice
        {
            public readonly int Cell;
            public readonly ushort Def;
            public readonly ushort Stuff;
            public RemovedEdifice(int cell, ushort def, ushort stuff) { Cell = cell; Def = def; Stuff = stuff; }
        }

        readonly List<RemovedEdifice> _removed = new List<RemovedEdifice>();

        /// <summary>
        /// Hand over the buildings removed since the last call, oldest first, and forget them.
        /// Capped as the felled trees are (<see cref="MaxFelledPending"/>), so a list nobody reads
        /// cannot grow.
        /// </summary>
        public void DrainRemoved(List<RemovedEdifice> into)
        {
            into.AddRange(_removed);
            _removed.Clear();
        }

        /// <summary>The module a tree of this species is drawn from, before its variant is picked.</summary>
        public int TreeModule(ushort def) => NaturalContent.IsTree(def) ? _naturalEdificeModule[def] : 0;

        void CopyCell(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices, int index)
        {
            _terrain[index] = Seen(grid, index);
            _floor[index] = grid.Floor[index];
            _floorStuff[index] = grid.FloorStuff[index];
            _flags[index] = (byte)grid.Flags[index];

            // A chamber nobody has opened is rock in the mirror, solid flag and all (design 62 §6),
            // so the mesher, the picker, the landscape measure and every pass that asks IsSolid
            // agree it is rock without any of them knowing there is a cavern. The breach marks its
            // chunks dirty, and the next refresh copies the air it always was.
            if (grid.IsUnseen(index)) _flags[index] |= (byte)CellFlags.SolidTerrain;

            int handle = grid.Edifice[index];
            if (handle >= 0 && handle < edifices.Count)
            {
                var placed = edifices[handle];
                _edifice[index] = placed.Def;
                _edificeStuff[index] = placed.Stuff;

                // A bed is one record behind two cells, and the mesher draws it once: from the
                // head, turned the way it was placed. Both halves carry the facing so the drawing
                // half never has to ask the world which end is which, and only the head carries
                // the flag — the far cell draws nothing of the bed at all.
                // The facing is mirrored for **any** record, not only a bed's: a ladder rotates now
                // too, and the one that does not rotate stores nought anyway (ConstructionGrid's
                // RaiseEdifice reads it off the def). A second array for the second rotatable thing
                // would have been two copies of one fact.
                bool bed = placed.Def == CoreContent.EdificeBed && !placed.Removed;
                _edificeFacing[index] = placed.Removed ? (byte)0 : placed.Facing;
                _bedHead[index] = bed && placed.CellIndex == index;
                _edificeHead[index] = !placed.Removed && placed.CellIndex == index;
            }
            else
            {
                _edifice[index] = CoreContent.EdificeNone;
                _edificeStuff[index] = CoreContent.StuffNone;
                _edificeFacing[index] = 0;
                _bedHead[index] = false;
                _edificeHead[index] = false;
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
        /// The terrain as the colony has seen it: an undiscovered seam is plain rock, and so is a
        /// chamber nobody has opened (<c>CellGrid.SeenTerrain</c>, the one owner of the rule since
        /// design 62 §6 — this used to restate the seam half of it).
        ///
        /// <para>The lie is told once, here, on the way into the mirror — so the module, the
        /// colour, the emissive trim and the inspect readout all agree about what the cell looks
        /// like without any of them knowing there is a rule. The simulation is untouched and
        /// still knows perfectly well that the cell is iron; this is the seam between what is
        /// true and what has been seen, and presentation is the right side of it
        /// (<see cref="CellFlags.Discovered"/>).</para>
        /// </summary>
        static ushort Seen(CellGrid grid, int index) => grid.SeenTerrain(index);

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
