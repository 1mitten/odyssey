#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>
    /// Pass 11: the bushes (design 45 §4). The Meadow look pass strewed bushes as drawn dressing,
    /// placed by a hash of the cell and never simulated; this places them as edifices at the same
    /// kind of spots and the same density, so a bush a player can see is one a colonist pushes
    /// through slowly and has to clear before building.
    ///
    /// <para><b>The rule is the dressing's</b> (<c>MeadowDressing.BigPiece</c>) at its shipped
    /// rung: only on the even-even lattice, where a value-noise field fourteen cells across stands
    /// above 0.45, raised to 0.7 at a wood's edge, times 0.4. It is written again here in integers
    /// rather than shared, because the simulation cannot reach presentation and must not use
    /// floats; the field is the same smoothstepped value noise, in 16.16 fixed point, over the same
    /// FNV hash — <b>keyed on the world seed</b>, which the dressing's never was, so two worlds do
    /// not share their bushes.</para>
    ///
    /// <para>It draws one number from its own stream (<see cref="NaturalGenPurpose.Undergrowth"/>)
    /// and hashes every cell from that, so the answer for a cell does not depend on the order the
    /// cells are visited or on any other pass's draws.</para>
    /// </summary>
    public sealed class UndergrowthPass : INaturalGenPass
    {
        public int Order => 11;
        public string Name => "Undergrowth";

        const uint SaltField = 0xB0C1u;
        const uint SaltBush = 0x7C1Fu;
        const uint SaltBerry = 0x5B3Du;

        /// <summary>One, in the 16-bit fixed point the field and the chances are kept in.</summary>
        const int One = 1 << 16;

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            if (gen.bushPerMille <= 0) return;

            uint key = ctx.Random(NaturalGenPurpose.Undergrowth).NextUInt();
            GridSize size = ctx.Size;
            CellRef start = ctx.Report.StartCell;
            int clear = gen.undergrowthClearRadius;
            int threshold = gen.bushFieldThreshold * One / 1000;
            int edgeWant = gen.bushWoodEdgeWant * One / 1000;

            for (int z = 0; z < size.SizeZ; z += 2)
            for (int x = 0; x < size.SizeX; x += 2)
            {
                int column = ctx.Column(x, z);
                int surface = ctx.SurfaceY[column];
                if (ctx.TopSolidY[column] != surface) continue;            // an outcrop, not ground
                if (ctx.Water[column] != 0) continue;
                int ground = ctx.Index(x, z, surface);
                if (ctx.Grid.Terrain[ground] != NaturalContent.TerrainGrass) continue;
                int cell = ground + size.LayerStride;
                if (cell >= size.CellCount) continue;
                if (ctx.Grid.Edifice[cell] >= 0) continue;                 // a tree stands here
                if (!ctx.Grid.IsWalkable(cell)) continue;

                int dx = x - start.X, dz = z - start.Z;
                if (dx * dx + dz * dz <= clear * clear) continue;          // the landing site

                int want = Want(Field(x, z, gen.bushFieldPeriod, key ^ SaltField), threshold);
                if (want < edgeWant && WoodEdge(ctx, x, z)) want = edgeWant;
                long chance = (long)want * gen.bushPerMille / 1000;
                if (Unit(x, z, key ^ SaltBush) >= chance) continue;

                // The façade rule (CLAUDE.md, design 22): the bank at the foot of a terrace step
                // fills this cell, so a bush in it would be sheared by the hillside exactly as the
                // trees were. Asked last, because it is the dearest question and most cells have
                // already been turned away.
                if (TerraceFoot.IsFoot(ctx.Grid, x, z, surface + 1))
                {
                    ctx.Report.BushesRefusedOnTerraceSteps++;
                    continue;
                }

                bool berries = Hash(x, z, key ^ SaltBerry) % (uint)gen.berryBushOneIn == 0;
                ushort def = berries ? NaturalContent.EdificeBerryBush : NaturalContent.EdificeBush;
                ctx.PlaceEdifice(cell, def, CoreContent.StuffNone, blocking: false);
                ctx.Grid.Flags[cell] |= CellFlags.Undergrowth;
                ctx.Report.Bushes++;
                if (berries) ctx.Report.BerryBushes++;
            }
        }

        /// <summary>Is a tree standing in any of the eight columns around this one?</summary>
        static bool WoodEdge(NaturalGenContext ctx, int x, int z)
        {
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = x + dx, nz = z + dz;
                if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;
                if (ctx.HasTree[ctx.Column(nx, nz)]) return true;
            }
            return false;
        }

        /// <summary>How strongly the field wants a bush: nothing below the threshold, one at the top.</summary>
        static int Want(int field, int threshold)
        {
            if (field <= threshold) return 0;
            long want = (long)(field - threshold) * One / (One - threshold);
            return want > One ? One : (int)want;
        }

        /// <summary>
        /// Smoothstepped value noise over a lattice <paramref name="cells"/> across, 0 to
        /// <see cref="One"/>: the dressing's <c>MeadowDressing.Field</c>, in integers.
        /// </summary>
        public static int Field(int x, int z, int cells, uint salt)
        {
            int x0 = x / cells, z0 = z / cells;
            long tx = (long)(x - x0 * cells) * One / cells;
            long tz = (long)(z - z0 * cells) * One / cells;
            tx = tx * tx * (3L * One - 2L * tx) / ((long)One * One);
            tz = tz * tz * (3L * One - 2L * tz) / ((long)One * One);
            long a = Unit(x0, z0, salt), b = Unit(x0 + 1, z0, salt);
            long c = Unit(x0, z0 + 1, salt), d = Unit(x0 + 1, z0 + 1, salt);
            long ab = a + (b - a) * tx / One;
            long cd = c + (d - c) * tx / One;
            return (int)(ab + (cd - ab) * tz / One);
        }

        /// <summary>A cell's hash as a fraction, 0 to <see cref="One"/> exclusive.</summary>
        public static int Unit(int x, int z, uint salt) => (int)(Hash(x, z, salt) >> 16);

        /// <summary>
        /// FNV-1a over the cell and a salt, then an avalanche — the same function as the
        /// renderer's <c>GroundScatter.Hash</c>, restated because the simulation cannot reach it.
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
