#nullable enable
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

        public RenderTestWorld(int sizeX, int sizeZ, int layers)
        {
            Size = new GridSize(sizeX, sizeZ, layers);
            Grid = new CellGrid(Size);
            Chunks = new ChunkGrid(Size);
            Library = new ModuleLibrary(null);
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
        public RenderTestWorld Edifice(int x, int z, int y, ushort def, ushort stuff, bool blocking = true)
        {
            int index = Index(x, z, y);
            _edifices.Add(new PlacedEdifice { CellIndex = index, Def = def, Stuff = stuff });
            Grid.Edifice[index] = _edifices.Count - 1;
            if (blocking) Grid.Flags[index] |= CellFlags.BlockingEdifice;
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

        /// <summary>Publish everything into the mirror, as the snapshot contributor would.</summary>
        public RenderTestWorld Publish()
        {
            Model.RefreshAll(Grid, _edifices);
            return this;
        }
    }
}
