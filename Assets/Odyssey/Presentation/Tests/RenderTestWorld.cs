#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// A hand-built grid and its render mirror, so a test can state exactly what is in the world
    /// and then ask what the renderer does with it. No worldgen, no scene, no catalogue: the
    /// module library falls back to primitives, which is the path a clone without the licensed
    /// packs takes anyway.
    /// </summary>
    sealed class RenderTestWorld
    {
        readonly List<PlacedEdifice> _edifices = new List<PlacedEdifice>();
        readonly List<SiteView> _sites = new List<SiteView>();

        public RenderTestWorld(int sizeX, int sizeZ, int layers) : this(sizeX, sizeZ, layers, new ModuleLibrary(null))
        {
        }

        /// <summary>A test world whose modules resolve through <paramref name="library"/> — the
        /// real catalogue's, for a test that needs the art and ignores itself where it did not
        /// resolve.</summary>
        public RenderTestWorld(int sizeX, int sizeZ, int layers, ModuleLibrary library)
        {
            Size = new GridSize(sizeX, sizeZ, layers);
            Grid = new CellGrid(Size);
            Chunks = new ChunkGrid(Size);
            Library = library;
            Model = new WorldRenderModel(Size, Chunks, Library);
        }

        public GridSize Size { get; }
        public CellGrid Grid { get; }
        public ChunkGrid Chunks { get; }
        public ModuleLibrary Library { get; }
        public WorldRenderModel Model { get; }

        public int Index(int x, int z, int y) => Size.Index(x, z, y);

        public RenderTestWorld Solid(int x, int z, int y, ushort terrain = CoreContent.TerrainRock)
        {
            int index = Index(x, z, y);
            Grid.Terrain[index] = terrain;
            Grid.Flags[index] |= CellFlags.SolidTerrain;
            return this;
        }

        public RenderTestWorld Surface(int x, int z, int y, ushort terrain = CoreContent.TerrainPavement)
        {
            Grid.Terrain[Index(x, z, y)] = terrain;
            return this;
        }

        public RenderTestWorld Slab(int x, int z, int y, ushort stuff = CoreContent.StuffConcrete)
        {
            int index = Index(x, z, y);
            Grid.Floor[index] = CoreContent.SlabStructural;
            Grid.FloorStuff[index] = stuff;
            return this;
        }

        /// <summary>
        /// Take a cell out of the world the way a miner does: the terrain goes, the solid flag
        /// goes, and everything solid touching it is now a face the colony has cut.
        ///
        /// <para>The reveal is <c>CellGrid.RevealAround</c> itself rather than a hand-set flag,
        /// because the thing being tested is precisely that presentation reads the mark mining
        /// leaves. Nothing else <c>MineJob.MineCell</c> does — the yield, the nav dirtying, the
        /// people and items that fall down the hole — has any bearing on what the mesher draws.</para>
        /// </summary>
        public RenderTestWorld Mine(int x, int z, int y)
        {
            int index = Index(x, z, y);
            Grid.Terrain[index] = CoreContent.TerrainAir;
            Grid.Flags[index] &= ~CellFlags.SolidTerrain;
            Grid.RevealAround(index);
            return this;
        }

        public RenderTestWorld Edifice(int x, int z, int y, ushort def, bool blocking = true) =>
            Edifice(x, z, y, def, CoreContent.StuffConcrete, blocking);

        /// <summary>
        /// The same, for a test that cares what the thing is made of.
        ///
        /// <para>Most do not, which is why concrete is the default and was for a long time the
        /// only option. A test that asks how the stuff tint is chosen needs to say — a tree and a
        /// wall are both placed with wood, and telling them apart is the whole point.</para>
        /// </summary>
        public RenderTestWorld Edifice(int x, int z, int y, ushort def, ushort stuff, bool blocking = true,
            int facing = 0)
        {
            int index = Index(x, z, y);
            _edifices.Add(new PlacedEdifice
            {
                CellIndex = index, Def = def, Stuff = stuff, Facing = (byte)facing,
            });
            Grid.Edifice[index] = _edifices.Count - 1;
            if (blocking) Grid.Flags[index] |= CellFlags.BlockingEdifice;
            return this;
        }

        /// <summary>
        /// A bed the way <c>ConstructionGrid.Raise</c> leaves it: one record, two
        /// <c>Edifice[]</c> slots pointing at it, the facing stored and the far cell derived —
        /// the same derivation (<c>EdificeFootprint</c>'s, restated as the rig's own arithmetic
        /// so this assembly need not reach into the simulation's).
        /// </summary>
        public RenderTestWorld Bed(int x, int z, int y, int facing, ushort stuff = CoreContent.StuffConcrete)
        {
            int head = Index(x, z, y);
            _edifices.Add(new PlacedEdifice
            {
                CellIndex = head, Def = CoreContent.EdificeBed, Stuff = stuff, Built = true,
                Facing = (byte)facing,
            });
            int handle = _edifices.Count - 1;
            Grid.Edifice[head] = handle;

            int fx = facing == 1 ? 1 : facing == 3 ? -1 : 0;
            int fz = facing == 0 ? 1 : facing == 2 ? -1 : 0;
            if (Size.Contains(x + fx, z + fz, y)) Grid.Edifice[Index(x + fx, z + fz, y)] = handle;
            return this;
        }

        /// <summary>A shelf standing in one cell, facing as given.</summary>
        public RenderTestWorld Shelf(int x, int z, int y, int facing, ushort stuff = CoreContent.StuffConcrete)
        {
            int cell = Index(x, z, y);
            _edifices.Add(new PlacedEdifice
            {
                CellIndex = cell, Def = CoreContent.EdificeShelf, Stuff = stuff, Built = true,
                Facing = (byte)facing,
            });
            Grid.Edifice[cell] = _edifices.Count - 1;
            return this;
        }

        /// <summary>
        /// A stair the way <c>ConstructionGrid.RaiseEdifice</c> leaves it: <b>two</b> records, not
        /// one, because a stair is the only buildable that finishes as two edifice values — the
        /// lower half at the head and the upper half at the far cell, each facing the other, which
        /// is what makes the footprint derivation symmetric from either end (U44).
        /// </summary>
        public RenderTestWorld Stair(int x, int z, int y, int facing, ushort stuff = CoreContent.StuffConcrete)
        {
            int fx = facing == 1 ? 1 : facing == 3 ? -1 : 0;
            int fz = facing == 0 ? 1 : facing == 2 ? -1 : 0;
            if (!Size.Contains(x + fx, z + fz, y)) return this;

            int head = Index(x, z, y);
            _edifices.Add(new PlacedEdifice
            {
                CellIndex = head, Def = CoreContent.EdificeStairLower, Stuff = stuff, Built = true,
                Facing = (byte)facing,
            });
            Grid.Edifice[head] = _edifices.Count - 1;

            int far = Index(x + fx, z + fz, y);
            _edifices.Add(new PlacedEdifice
            {
                CellIndex = far, Def = CoreContent.EdificeStairUpper, Stuff = stuff, Built = true,
                Facing = (byte)((facing + 2) & 3),
            });
            Grid.Edifice[far] = _edifices.Count - 1;
            return this;
        }

        /// <summary>
        /// A board whose <c>x &lt; half</c> is <paramref name="rise"/> layers higher than the rest:
        /// a straight terrace step running the whole depth of the map, published and ready.
        ///
        /// <para>Shared because two fixtures want the same board — what the mesher draws on a
        /// terrace, and what a figure standing on one is drawn at — and a second copy would drift.
        /// The low half is at layer 1, so the bank stands in the empty cells at layer 1 with their
        /// floor at layer 0.</para>
        /// </summary>
        public static RenderTestWorld Terrace(int rise, ushort stepTerrain, int n = 6, int layers = 8)
        {
            var world = new RenderTestWorld(n, n, layers);

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x < n / 2 ? 1 + rise : 1;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, x < n / 2 ? stepTerrain : NaturalContent.TerrainGrass);
            }

            return world.Publish();
        }

        /// <summary>
        /// An order waiting to be built here, as the frame would carry it.
        ///
        /// <para>Not part of the chunk mirror and deliberately not published by
        /// <see cref="Publish"/>: a site is not geometry, it arrives once a frame from the
        /// snapshot, and the composition root hands it over separately. A test that wants one says
        /// so, in the order it happens in the game — the world, then the frame.</para>
        /// </summary>
        public RenderTestWorld Site(int x, int z, int y, byte building = BuildingHandle.Wall)
        {
            _sites.Add(new SiteView(Index(x, z, y), building, (byte)CoreContent.StuffConcrete,
                delivered: 0, cost: 5, workDone: 0, workTotal: 100));
            Model.SetSites(new ReadOnlySpan<SiteView>(_sites.ToArray()));
            return this;
        }

        /// <summary>Publish everything into the mirror, as the snapshot contributor would.</summary>
        public RenderTestWorld Publish()
        {
            Model.RefreshAll(Grid, _edifices);
            return this;
        }

        /// <summary>Publish only the chunks marked dirty since, as a tick's edit reaches the mirror.</summary>
        public RenderTestWorld PublishEdits()
        {
            Model.RefreshDirty(Grid, _edifices);
            return this;
        }
    }
}
