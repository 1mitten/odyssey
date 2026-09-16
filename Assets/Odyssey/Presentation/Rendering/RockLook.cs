#nullable enable
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Which lump of stone a cell wears, and which way round it sits.
    ///
    /// <para>Everything here is a hash of the cell's own coordinates, for the reason
    /// <see cref="GroundScatter"/> gives at length: a chunk is re-meshed whenever anything in it
    /// changes, so a stream of random numbers would reshuffle the rock face every time a colonist
    /// dug a cell twenty metres away. A hash depends on nothing but the cell, so a cliff looks the
    /// same after a rebuild, after a reload and on another machine.</para>
    ///
    /// <para>The layer is folded into the salt, so a column of rock is not the same block stacked
    /// three times — which is the single most box-like thing a voxel world can do.</para>
    /// </summary>
    public static class RockLook
    {
        const uint SaltVariant = 0x5A17u;
        const uint SaltYaw = 0x7E11u;

        /// <summary>
        /// Is this terrain stone, and so drawn as a lump rather than as a cube?
        ///
        /// Rock, bedrock and both ore seams: the things a pick goes through. Soil, subsoil, sand
        /// and grass stay flat-topped cubes on purpose — they are the ground a colony stands and
        /// builds on, and a chipped, uneven floor would read as damage rather than as earth.
        /// </summary>
        public static bool IsStone(ushort terrain) =>
            terrain == CoreContent.TerrainRock ||
            terrain == NaturalContent.TerrainBedrock ||
            NaturalContent.IsOre(terrain);

        /// <summary>Which of <see cref="RockMesh.Variants"/> lumps this cell wears.</summary>
        public static int Variant(int x, int z, int y) =>
            (int)(Hash(x, z, y, SaltVariant) % RockMesh.Variants);

        /// <summary>
        /// The cell's bearing, in degrees, as one of the four right angles.
        ///
        /// Free variety: a square footprint is unchanged by a quarter turn, so the block still
        /// tiles exactly, and rotating the instance costs no extra mesh and no extra draw — the
        /// block's local transform is a vertical shift and a scale that is equal in x and z, and a
        /// turn about the vertical commutes with both. Six lumps become twenty-four.
        /// </summary>
        public static float Yaw(int x, int z, int y) => 90f * (Hash(x, z, y, SaltYaw) % 4u);

        static uint Hash(int x, int z, int y, uint salt) =>
            GroundScatter.Hash(x, z, salt + (uint)y * 2654435761u);
    }
}
