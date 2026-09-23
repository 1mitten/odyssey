#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Designations;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Pawns.Wildlife
{
    /// <summary>
    /// The surface a world offers its animals (design 30 §2): every column's topmost walkable
    /// cell that is dry, that an animal may enter, and that can be walked to from the colony's
    /// start. The count sets the population target; the habitat lists say where a group may be
    /// put. Built once at tick zero by the seeder and again by the level-keeper when it needs an
    /// edge to arrive at.
    /// </summary>
    public sealed class SurfaceCensus
    {
        /// <summary>Walkable, dry, reachable surface cells, in column order.</summary>
        public readonly List<int> Cells = new List<int>();

        public readonly List<int> Woodland = new List<int>();
        public readonly List<int> Rock = new List<int>();

        /// <summary>Surface cells on the board's outer ring: where an arrival appears and a leaver vanishes.</summary>
        public readonly List<int> Edge = new List<int>();

        readonly HashSet<int> _set = new HashSet<int>();

        public bool Contains(int cell) => _set.Contains(cell);

        /// <summary>
        /// <paramref name="keepClear"/> is a Chebyshev radius round <paramref name="start"/> in
        /// which nothing is offered: the colony's own clearing.
        /// </summary>
        public static SurfaceCensus Take(CellGrid grid, NavGraph nav, DesignationGrid? designations,
            CellRef start, int keepClear, TraverseMode mode)
        {
            var census = new SurfaceCensus();
            GridSize size = grid.Size;
            int startIndex = size.Index(start.X, start.Z, start.Y);
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                int cell = Topmost(grid, x, z);
                if (cell < 0) continue;
                if (!nav.Grid.CanEnter(cell, mode)) continue;
                if (!nav.Reachable(startIndex, cell, mode)) continue;
                census.Cells.Add(cell);
                census._set.Add(cell);
                bool onEdge = x == 0 || z == 0 || x == size.SizeX - 1 || z == size.SizeZ - 1;
                if (onEdge) census.Edge.Add(cell);
                if (Math.Max(Math.Abs(x - start.X), Math.Abs(z - start.Z)) <= keepClear) continue;
                int y = size.FromIndex(cell).Y;
                if (designations != null && NearTree(grid, designations, x, z, y)) census.Woodland.Add(cell);
                if (BesideRock(grid, x, z, y)) census.Rock.Add(cell);
            }
            return census;
        }

        /// <summary>The highest walkable cell in a column, or −1 for a column with none.</summary>
        public static int Topmost(CellGrid grid, int x, int z)
        {
            GridSize size = grid.Size;
            for (int y = size.SizeY - 1; y >= 0; y--)
            {
                int cell = size.Index(x, z, y);
                if (grid.IsWalkable(cell)) return cell;
            }
            return -1;
        }

        static bool NearTree(CellGrid grid, DesignationGrid designations, int x, int z, int y)
        {
            GridSize size = grid.Size;
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!size.Contains(x + dx, z + dz, y + dy)) continue;
                if (designations.IsTree(size.Index(x + dx, z + dz, y + dy))) return true;
            }
            return false;
        }

        static bool BesideRock(CellGrid grid, int x, int z, int y)
        {
            GridSize size = grid.Size;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                if (!size.Contains(x + dx, z + dz, y)) continue;
                if (grid.IsSolidTerrain(size.Index(x + dx, z + dz, y))) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Puts a world's animals on it at tick zero (design 30 §2), after the colonists are placed
    /// and before the first tick, from the same seed the colonists came from.
    ///
    /// <para><b>The target is a census, not a number.</b> The table says animals per ten thousand
    /// walkable surface columns; the seeder counts the columns this board actually offers — dry,
    /// enterable by an animal, and reachable from the start, so no hog is sealed in a cavern
    /// nobody will ever open — and takes the table's share of that. A board that is half lake
    /// carries half the animals, and the bare board, whose table is empty, carries none.</para>
    ///
    /// <para><b>Groups land together, on their habitat.</b> A sounder's centre is drawn from the
    /// woodland cells and its members fill the walkable cells round it; a rat is drawn from the
    /// cells beside rock. Where a habitat has no cells at all the entry falls back to any surface
    /// cell rather than to nothing, so a treeless seed still has its hogs. Nothing lands in the
    /// colony's clearing.</para>
    /// </summary>
    public static class WildlifeSeeder
    {
        /// <summary>How far past the starting fell radius the clearing is kept clear.</summary>
        public const int ClearingMargin = 6;

        /// <summary>How far from a group's centre its members may be put.</summary>
        public const int GroupRadius = 3;

        public static int TargetFor(MapGenDef gen, int surfaceColumns) =>
            gen.wildlifePer10000Columns <= 0 || surfaceColumns <= 0
                ? 0
                : Math.Min(gen.wildlifeCeiling, (surfaceColumns * gen.wildlifePer10000Columns + 5_000) / 10_000);

        /// <summary>Seed the board. Returns how many animals were placed.</summary>
        public static int Seed(PawnContext pawns, MapGenDef gen, CellRef start, int keepClear, uint seed)
        {
            if (gen.wildlife.Length == 0 || gen.wildlifePer10000Columns <= 0) return 0;
            SurfaceCensus census = SurfaceCensus.Take(pawns.Cells, pawns.Nav, pawns.Designations, start, keepClear, TraverseMode.Animal);
            int target = TargetFor(gen, census.Cells.Count);
            if (target <= 0) return 0;

            var rng = DeterministicRandom.ForTick(seed, 0, WildlifePurpose.Seed);
            var taken = new HashSet<int>();
            int placed = 0;
            for (int attempt = 0; attempt < 64 && placed < target; attempt++)
            {
                WildlifeEntry? entry = Pick(gen.wildlife, ref rng);
                if (entry == null) break;
                int kind = KindIndex(pawns.Content, entry.kind);
                if (kind < 0) continue;
                int group = Math.Min(GroupSize(entry, ref rng), target - placed);
                List<int> habitat = HabitatCells(census, entry.habitat, start, keepClear, pawns.Size);
                if (habitat.Count == 0) continue;
                int centre = habitat[rng.NextInt(habitat.Count)];
                placed += PlaceGroup(pawns, census, taken, centre, kind, group);
            }
            return placed;
        }

        /// <summary>The cells an entry's habitat offers, with the clearing excluded whatever the habitat.</summary>
        internal static List<int> HabitatCells(SurfaceCensus census, Habitat habitat, CellRef start, int keepClear, GridSize size)
        {
            List<int> list = habitat switch
            {
                Habitat.Woodland => census.Woodland,
                Habitat.Rock => census.Rock,
                _ => census.Cells,
            };
            if (list.Count > 0 && habitat != Habitat.Any) return list;
            // Any, or a habitat this board has none of: every surface cell outside the clearing.
            var open = new List<int>(census.Cells.Count);
            for (int i = 0; i < census.Cells.Count; i++)
            {
                CellRef c = size.FromIndex(census.Cells[i]);
                if (Math.Max(Math.Abs(c.X - start.X), Math.Abs(c.Z - start.Z)) <= keepClear) continue;
                open.Add(census.Cells[i]);
            }
            return open;
        }

        /// <summary>Spawn up to <paramref name="count"/> of a kind on census cells within <see cref="GroupRadius"/> of a centre.</summary>
        internal static int PlaceGroup(PawnContext pawns, SurfaceCensus census, HashSet<int> taken, int centre, int kind, int count)
        {
            GridSize size = pawns.Size;
            CellRef c = size.FromIndex(centre);
            int placed = 0;
            for (int radius = 0; radius <= GroupRadius && placed < count; radius++)
            for (int dz = -radius; dz <= radius && placed < count; dz++)
            for (int dx = -radius; dx <= radius && placed < count; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
                int x = c.X + dx, z = c.Z + dz;
                if (x < 0 || z < 0 || x >= size.SizeX || z >= size.SizeZ) continue;
                int cell = SurfaceCensus.Topmost(pawns.Cells, x, z);
                if (cell < 0 || !census.Contains(cell) || taken.Contains(cell)) continue;
                if (pawns.Pawns.IsCellOccupiedByStandingPawn(cell)) continue;
                pawns.Pawns.Spawn(cell, kind);
                taken.Add(cell);
                placed++;
            }
            return placed;
        }

        public static WildlifeEntry? Pick(WildlifeEntry[] table, ref DeterministicRandom rng)
        {
            int total = 0;
            for (int i = 0; i < table.Length; i++) total += Math.Max(0, table[i].weight);
            if (total <= 0) return null;
            int roll = rng.NextInt(total);
            for (int i = 0; i < table.Length; i++)
            {
                roll -= Math.Max(0, table[i].weight);
                if (roll < 0) return table[i];
            }
            return table[table.Length - 1];
        }

        public static int GroupSize(WildlifeEntry entry, ref DeterministicRandom rng) =>
            entry.groupMax > entry.groupMin
                ? entry.groupMin + rng.NextInt(entry.groupMax - entry.groupMin + 1)
                : entry.groupMin;

        /// <summary>The kind index a table entry names, or −1 when this content has no such kind.</summary>
        public static int KindIndex(PawnContent content, string kindName)
        {
            for (int i = 0; i < content.Kinds.Length; i++)
                if (content.Kinds[i].defName == kindName) return i;
            return -1;
        }
    }
}
