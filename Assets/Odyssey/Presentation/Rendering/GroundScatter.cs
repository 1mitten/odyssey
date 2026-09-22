#nullable enable

using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where the tufts of grass go: how many a cell gets, and where each one stands in it.
    ///
    /// **Everything here is a hash of the cell's own coordinates, and that is the whole design.**
    /// A chunk is re-meshed whenever anything in it changes, so scatter has to come out identical
    /// every single time or the grass would crawl about whenever a wall went up nearby. A stream
    /// of random numbers cannot promise that — it would depend on how many cells had been visited
    /// first, which depends on which chunk is being rebuilt and in which order. A hash depends on
    /// nothing but the cell, so it is stable by construction, needs no state, allocates nothing,
    /// and gives the same field on every machine and after every reload.
    ///
    /// It also keeps the simulation out of it. Grass is decoration: it blocks nothing, costs
    /// nothing to walk through and is not in the save. If it drew from the simulation's own random
    /// stream it would be a determinism hazard for no gain at all.
    /// </summary>
    public static class GroundScatter
    {
        /// <summary>
        /// The most a single cell will ever be given, however high the density goes.
        ///
        /// <para>Three until 2026-09-22, when the owner played the thickened meadow and said
        /// it still was not bushy. Three clumps in a 2.5 m cell cannot cover it however large
        /// each one is, so the ceiling was the binding constraint and not the density.</para>
        /// </summary>
        public const int MaxPerCell = 6;

        // Arbitrary and fixed. Different salts make the count, the position and the choice of
        // tuft independent of one another, so cells with two tufts are not also the cells whose
        // tufts are all in the same corner.
        const uint SaltCount = 0x9E37u;
        const uint SaltPlace = 0x85EBu;
        const uint SaltVariant = 0xC2B2u;

        /// <summary>
        /// How many tufts a cell gets, for a density expressed in tufts per hundred cells.
        ///
        /// Fractional densities matter more than whole ones: at 120 a field is mostly single
        /// tufts with a fifth of it doubled up, and that unevenness is what stops a meadow
        /// reading as a lawn. So the whole part is given to every cell and the remainder is a
        /// per-cell coin weighted by the fraction.
        /// </summary>
        public static int CountFor(int x, int z, int density)
        {
            if (density <= 0) return 0;

            int whole = density / 100;
            int fraction = density % 100;
            int count = whole;
            if (fraction > 0 && Unit(x, z, SaltCount) * 100f < fraction) count++;
            return count < MaxPerCell ? count : MaxPerCell;
        }

        /// <summary>
        /// Where one tuft stands, as a fraction of the cell from its centre, plus its bearing and
        /// its size.
        ///
        /// The offset stops short of the cell edge so a tuft does not visibly straddle the grid —
        /// the point of scattering is to hide the grid, and a row of tufts split down the middle
        /// by a cell boundary advertises it instead.
        /// </summary>
        public static void Placement(int x, int z, int slot,
            out float offsetX, out float offsetZ, out float yaw, out float scale)
        {
            uint salt = SaltPlace + (uint)slot * 7919u;

            // **The hole in the middle of every cell is gone, and this is the record of why it
            // was there.** Clumps used to be placed in a ring from 0.30 to 0.44 of a cell,
            // leaving a disc of about 1.8 square metres — twenty-eight per cent of every cell —
            // permanently bare, which is most of why the meadow would not thicken however much
            // was asked of it (owner, 2026-09-22: "it's not very bushy").
            //
            // The reason was sound at the time: a colonist, a crate and a stack of rations are
            // all drawn at the cell centre, and scattering uniformly put clump centres on top of
            // them, so grass grew through people's legs and out of the side of crates. The note
            // here said, correctly, that keeping the middle clear "costs nothing and needs to
            // know nothing about what is standing there, which matters: pawns and items live in
            // the published snapshot, not in the cell mirror the mesher reads".
            //
            // **That is exactly what GrassClearance now does properly.** Items, order marks and
            // pawns stamp themselves into a field the shader reads, so the grass gets out of the
            // way of what is actually there rather than of the place where something might be.
            // The ring was a static approximation of a dynamic fact, and it outlived the
            // constraint that forced it.
            //
            // The square root spreads clumps evenly over the disc's area rather than crowding
            // them into the middle, which is what a linear radius would do.
            float angle = Unit(x, z, salt) * (Mathf.PI * 2f);
            float radius = Mathf.Lerp(InnerRadius, OuterRadius, Mathf.Sqrt(Unit(x, z, salt + 1u)));

            offsetX = Mathf.Cos(angle) * radius;
            offsetZ = Mathf.Sin(angle) * radius;
            yaw = Unit(x, z, salt + 2u) * 360f;
            scale = 0.7f + Unit(x, z, salt + 3u) * 0.5f;
        }

        /// <summary>
        /// How close to the cell centre a clump may stand, as a fraction of the cell. Zero:
        /// anywhere. See <see cref="Placement"/> for what used to hold it open, and why that
        /// job moved to <c>GrassClearance</c>.
        /// </summary>
        public const float InnerRadius = 0f;

        /// <summary>How far out it may stand. Short of the edge, so a clump's own centre does
        /// not straddle the grid — its reach carries it over the line and its neighbours cover
        /// the corners.</summary>
        public const float OuterRadius = 0.46f;

        /// <summary>
        /// Half the widest clump mesh, in metres: a clump nearly two metres across overhangs
        /// the cell it is placed in by this much, which is the whole of the growing zone's
        /// border problem - a tilled tile beside grass kept a fringe of meadow lying over it
        /// (owner, 2026-09-19: "remove the grass graphics from the garden plots ... it makes
        /// it jarring to see").
        /// </summary>
        public const float ClumpReach = 1.0f;

        /// <summary>
        /// Pull a clump's offset in from any side whose neighbour is tilled, so its mesh stays
        /// on the grass it belongs to. The offset arrives as a fraction of the cell and leaves
        /// the same way; the limit is how far out a clump may sit before its reach crosses the
        /// tile line, and a cell ringed on all four sides still has the centre strip to stand
        /// in - a cell is 2.5 m and the clump reaches 1.0, so 0.5 m of grass remains.
        /// </summary>
        public static void PullInFromTilled(
            ref float offsetX, ref float offsetZ,
            bool tilledXPlus, bool tilledXMinus, bool tilledZPlus, bool tilledZMinus)
        {
            float limit = (CellMetrics.SizeXZ * 0.5f - ClumpReach) / CellMetrics.SizeXZ;
            if (limit < 0f) limit = 0f;
            if (tilledXPlus) offsetX = Mathf.Min(offsetX, limit);
            if (tilledXMinus) offsetX = Mathf.Max(offsetX, -limit);
            if (tilledZPlus) offsetZ = Mathf.Min(offsetZ, limit);
            if (tilledZMinus) offsetZ = Mathf.Max(offsetZ, -limit);
        }

        /// <summary>
        /// How deep the grass grows here, from what the land around it is: 0 thin, 1 lush.
        ///
        /// <para><b>Following the land rather than noise was the owner's choice</b>
        /// (<c>grass-interview.md</c>, answer 11), and it is the more interesting one: a meadow
        /// that is deep by the water and thin against the rock tells you something true about
        /// the map while you are looking at it, where noise only tells you the artist wanted
        /// variety. It also makes the grass part of worldgen rather than a texture laid over
        /// it.</para>
        ///
        /// <para>The recorded risk is that a rule keyed on terrain draws edges where the
        /// terrain changes, so the field can read as a contour map. Noise on top was offered
        /// and declined; if it reads badly, adding it here is one line.</para>
        ///
        /// <para>A pure function of two counts so a test can state the rule without building a
        /// world: four wet neighbours is as lush as it gets, four stony ones as thin.</para>
        /// </summary>
        public static float Lushness(int wetNeighbours, int stonyNeighbours) =>
            Mathf.Clamp01(0.5f + wetNeighbours * 0.22f - stonyNeighbours * 0.18f);

        /// <summary>How the lushness scales the density, from thin ground to deep grass.</summary>
        public static float DensityScale(float lushness) => Mathf.Lerp(0.45f, 1.25f, lushness);

        /// <summary>And how it scales a clump itself, so deep grass is taller as well as thicker.</summary>
        public static float ClumpScale(float lushness) => Mathf.Lerp(0.78f, 1.18f, lushness);

        /// <summary>Which of the available tuft meshes this one is.</summary>
        public static int VariantFor(int x, int z, int slot, int variants)
        {
            if (variants <= 1) return 0;
            return (int)(Hash(x, z, SaltVariant + (uint)slot * 104729u) % (uint)variants);
        }

        /// <summary>A stable value in [0, 1) for a cell and a salt.</summary>
        public static float Unit(int x, int z, uint salt) =>
            (Hash(x, z, salt) & 0xFFFFFFu) * (1f / 0x1000000);

        /// <summary>
        /// FNV-1a over the coordinates and the salt, then avalanched.
        ///
        /// The avalanche is not decoration. FNV on its own leaves neighbouring inputs with
        /// neighbouring low bits, and taking a small modulus of that gives diagonal stripes across
        /// the map — a pattern the eye picks out instantly in a field of grass and which would
        /// look like a worldgen bug rather than like a hash being reused past its strength.
        /// </summary>
        public static uint Hash(int x, int z, uint salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)z) * 16777619u;
                h = (h ^ salt) * 16777619u;
                h ^= h >> 13;
                h *= 0x5BD1E995u;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
