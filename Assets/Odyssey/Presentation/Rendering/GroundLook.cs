#nullable enable
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Which piece of ground a cell wears, and which way round it sits.
    ///
    /// <para>The earth counterpart of <see cref="RockLook"/>, and it works the same way for the
    /// same reason: everything here is a hash of the cell's own coordinates. A chunk is re-meshed
    /// whenever anything in it changes, so a stream of random numbers would reshuffle the meadow
    /// every time a colonist felled a tree twenty metres away. A hash depends on nothing but the
    /// cell, so the ground looks the same after a rebuild, after a reload and on another
    /// machine.</para>
    ///
    /// <para>The salts differ from <see cref="RockLook"/>'s. Sharing them would tie the variant of
    /// a grass cell to the variant of the rock beneath it, which is invisible almost everywhere
    /// and then abruptly is not: mine a bench out of a hillside and every cut cell would wear the
    /// lump matching the turf directly above it.</para>
    /// </summary>
    public static class GroundLook
    {
        const uint SaltVariant = 0x6D11u;
        const uint SaltYaw = 0x2F0Du;

        /// <summary>
        /// Is this terrain drawn as earth — a rippled block rather than a smooth cube or a lump?
        ///
        /// <para>The natural soils, named one at a time rather than defined as "everything that is
        /// not stone". The wide definition is tempting and wrong: it sweeps in the city
        /// generator's engineered fill, salvage and buried seam, which are man-made or industrial
        /// and have no business rippling like a meadow. A list is also the honest shape for this —
        /// whether something reads as soil is a judgement per terrain, not a property derivable
        /// from the others.</para>
        ///
        /// <para>Stone is excluded because <see cref="RockMesh"/> already serves it and wants to
        /// stay jagged; water because it is a surface with a shader of its own and never a block.
        /// Marsh is included: it is ground that happens to be wet, and it is drawn as a block like
        /// any other soil.</para>
        /// </summary>
        public static bool IsEarth(ushort terrain) =>
            terrain == NaturalContent.TerrainGrass ||
            terrain == NaturalContent.TerrainBareEarth ||
            terrain == NaturalContent.TerrainPackedGravel ||
            terrain == NaturalContent.TerrainSand ||
            terrain == NaturalContent.TerrainSubsoil ||
            terrain == NaturalContent.TerrainMarsh;

        /// <summary>Which of <see cref="GroundMesh.Variants"/> tops this cell wears.</summary>
        public static int Variant(int x, int z, int y) =>
            (int)(Hash(x, z, y, SaltVariant) % (uint)GroundMesh.Variants);

        /// <summary>
        /// The cell's bearing, in degrees, as one of the four right angles.
        ///
        /// <para>Free variety, and the reason <see cref="GroundMesh.Variants"/> can stay at two. A
        /// square footprint is unchanged by a quarter turn, so the block still tiles exactly, and
        /// the turn rides in the instance matrix the cell already had — no extra mesh, no extra
        /// bucket and no extra draw. Two tops become eight.</para>
        /// </summary>
        public static float Yaw(int x, int z, int y) => 90f * (Hash(x, z, y, SaltYaw) % 4u);

        // A bank used to pick a variant from here, and does not any more. Its shape is decided
        // entirely by the steps around its cell — see BankMesh, where the reasoning is — and the
        // variety it used to take from a hash was the very thing that made a run of banks read as
        // a ridge of misaligned bars rather than as one slope. Nothing replaced it: the answer was
        // less variation, not different variation.

        static uint Hash(int x, int z, int y, uint salt) =>
            GroundScatter.Hash(x, z, salt + (uint)y * 2246822519u);
    }
}
