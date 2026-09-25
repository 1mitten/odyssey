#nullable enable

using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where the Meadow dressing stands: the tall-grass stands, the flowers, the bushes and the
    /// stones that make a meadow of a grass field (the look pass, owner 2026-09-24; design 38 §17).
    ///
    /// <para><b>Patches, not confetti.</b> The reference picture is not a uniform sprinkle: it is
    /// a stand of tall grass here, a flowered sweep there, a clump of bushes at a wood's edge.
    /// So every kind reads a low-frequency noise field over the board and is dense where its field
    /// is high and absent where it is low, with the per-cell hash deciding only the detail. The
    /// fields are different noises, so a stand of grass and a flower meadow are not the same
    /// place.</para>
    ///
    /// <para><b>Everything here is a function of the cell's coordinates</b>, for the reason
    /// <see cref="GroundScatter"/> gives: a chunk is re-meshed whenever anything in it changes, so
    /// the dressing has to come out the same every time, needs no state and is in no save. It is
    /// drawn only — not in a cell, the save or the hash — and it asks the mesher, not the
    /// simulation, whether a cell is free to stand on.</para>
    ///
    /// <para><b>Big pieces on a lattice.</b> A bush is up to five metres across and a tall-grass
    /// mat up to six, so they are offered on alternate cells only — bushes on one lattice, grass
    /// stands on the other — and a cell that takes one gives its neighbours room.</para>
    /// </summary>
    public static class MeadowDressing
    {
        const uint SaltTall = 0x51A3u;
        const uint SaltBush = 0x7C1Fu;
        const uint SaltFlower = 0x2E97u;
        const uint SaltCover = 0x6B33u;
        const uint SaltRock = 0x19D5u;
        const uint SaltSun = 0x4F61u;
        const uint SaltField = 0xA11Cu;

        /// <summary>What a dressing slot holds.</summary>
        public enum Kind { None, TallGrass, Bush, Flower, Cover, Rock, Sunflower }

        /// <summary>
        /// Smooth value noise over cell coordinates, [0, 1]: a hash on a lattice every
        /// <paramref name="cells"/> cells, smoothstepped between. Same field on every machine.
        /// </summary>
        public static float Field(int x, int z, float cells, uint salt)
        {
            float fx = x / cells, fz = z / cells;
            int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
            float tx = fx - x0, tz = fz - z0;
            tx = tx * tx * (3f - 2f * tx);
            tz = tz * tz * (3f - 2f * tz);
            float a = GroundScatter.Unit(x0, z0, salt);
            float b = GroundScatter.Unit(x0 + 1, z0, salt);
            float c = GroundScatter.Unit(x0, z0 + 1, salt);
            float d = GroundScatter.Unit(x0 + 1, z0 + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>How strongly a field wants its kind here: nothing below the threshold, rising
        /// to one at the top, so a patch has a soft edge rather than a line.</summary>
        static float Want(float field, float threshold) =>
            Mathf.Clamp01((field - threshold) / (1f - threshold));

        /// <summary>
        /// The big piece a cell carries, if any: a bush or a tall-grass stand, on alternating
        /// lattices. <paramref name="density"/> is the grass ladder's rung over its shipped value,
        /// so the Meadow rung is one and Full is five; <paramref name="woodEdge"/> is whether a tree
        /// stands beside the cell, which is where bushes gather.
        /// </summary>
        public static Kind BigPiece(int x, int z, float density, bool woodEdge)
        {
            if (density <= 0f) return Kind.None;
            // Bushes on one cell in four; tall grass on the checkerboard's other half, which is
            // most of the meadow — the reference's field is stands of tall grass more than it is
            // open lawn.
            bool even = ((x & 1) == 0) && ((z & 1) == 0);
            bool odd = ((x + z) & 1) == 1;
            float scale = Mathf.Min(density, 5f);

            // The even lattice was the bushes'. They are the simulation's now (design 45 §4) —
            // placed by UndergrowthPass on this same lattice at this rule's shipped density and
            // drawn from their edifice — so the dressing leaves those cells to it.
            if (even) return Kind.None;
            if (odd)
            {
                float want = Want(Field(x, z, 18f, SaltField + 2u), 0.2f);
                float chance = Mathf.Min(0.9f, want * 0.7f * Mathf.Sqrt(scale));
                if (GroundScatter.Unit(x, z, SaltTall) < chance) return Kind.TallGrass;
            }
            return Kind.None;
        }

        /// <summary>
        /// How many small pieces a cell carries and of what: flowers where the flower field is
        /// high, ground cover lightly everywhere, the odd stone (more beside rock), the rare
        /// sunflower. Slot by slot, so a cell can hold a flower and a stone.
        /// </summary>
        public static Kind SmallPiece(int x, int z, int slot, float density, bool nearRock)
        {
            if (density <= 0f) return Kind.None;
            float scale = Mathf.Min(density, 5f);
            uint slotSalt = (uint)slot * 7919u;

            switch (slot)
            {
                case 0:
                {
                    float want = Want(Field(x, z, 11f, SaltField + 3u), 0.35f);
                    if (GroundScatter.Unit(x, z, SaltFlower + slotSalt) < want * 0.8f * Mathf.Sqrt(scale))
                        return Kind.Flower;
                    return Kind.None;
                }
                case 1:
                {
                    if (GroundScatter.Unit(x, z, SaltCover + slotSalt) < 0.1f * scale) return Kind.Cover;
                    return Kind.None;
                }
                case 2:
                    // The stones were this slot's: 18 per cent of cells beside rock, 1.2 per cent in
                    // the open. They are the simulation's now (design 45 §6) — the undergrowth pass
                    // lays a stack of stone at those spots, hauled for stone and drawn as the heap
                    // it is — so the dressing leaves them to it.
                    return Kind.None;
                case 3:
                {
                    float want = Want(Field(x, z, 23f, SaltField + 4u), 0.8f);
                    if (GroundScatter.Unit(x, z, SaltSun + slotSalt) < want * 0.4f) return Kind.Sunflower;
                    return Kind.None;
                }
            }
            return Kind.None;
        }

        /// <summary>Slots <see cref="SmallPiece"/> is asked about per cell.</summary>
        public const int SmallSlots = 4;

        /// <summary>Where a piece stands in its cell, its bearing and its size, from the hash.</summary>
        public static void Placement(int x, int z, uint salt, float spread,
            out float offsetX, out float offsetZ, out float yaw, out float scale)
        {
            offsetX = (GroundScatter.Unit(x, z, salt + 11u) - 0.5f) * spread;
            offsetZ = (GroundScatter.Unit(x, z, salt + 13u) - 0.5f) * spread;
            yaw = GroundScatter.Unit(x, z, salt + 17u) * 360f;
            scale = 0.8f + GroundScatter.Unit(x, z, salt + 19u) * 0.4f;
        }

        /// <summary>Which variant of a family a piece is.</summary>
        public static int VariantFor(int x, int z, uint salt, int variants) =>
            variants <= 1 ? 0 : (int)(GroundScatter.Hash(x, z, salt + 23u) % (uint)variants);

        /// <summary>The salt a kind's placement and variant draw on, so kinds are independent.</summary>
        public static uint SaltOf(Kind kind) => kind switch
        {
            Kind.TallGrass => SaltTall,
            Kind.Bush => SaltBush,
            Kind.Flower => SaltFlower,
            Kind.Cover => SaltCover,
            Kind.Rock => SaltRock,
            Kind.Sunflower => SaltSun,
            _ => 0u,
        };
    }
}
