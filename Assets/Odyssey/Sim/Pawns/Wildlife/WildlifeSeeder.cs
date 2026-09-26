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

        /// <summary>Surface cells with no tree within two cells and no rock beside them, outside the clearing (<see cref="Habitat.Open"/>).</summary>
        public readonly List<int> Open = new List<int>();

        /// <summary>Surface cells within <see cref="WaterBank.SeedRadius"/> of water, outside the clearing (design 30 §8).</summary>
        public readonly List<int> Bank = new List<int>();

        /// <summary>Surface cells on the board's outer ring: where an arrival appears and a leaver vanishes.</summary>
        public readonly List<int> Edge = new List<int>();

        readonly HashSet<int> _set = new HashSet<int>();
        readonly HashSet<int> _bank = new HashSet<int>();

        public bool Contains(int cell) => _set.Contains(cell);

        /// <summary>Is this census cell on a bank? The clearing is never one.</summary>
        public bool IsBank(int cell) => _bank.Contains(cell);

        /// <summary>
        /// <paramref name="keepClear"/> is a Chebyshev radius round <paramref name="start"/> in
        /// which nothing is offered: the colony's own clearing.
        /// </summary>
        public static SurfaceCensus Take(CellGrid grid, NavGraph nav, DesignationGrid? designations,
            CellRef start, int keepClear, TraverseMode mode) =>
            Take(grid, nav, designations, start, keepClear, mode, new SurfaceCensus());

        /// <summary>
        /// The same census into an instance kept from last time, cleared and refilled, so a
        /// system that retakes it on a rare tick allocates nothing once its lists have grown
        /// to the board (<c>PathAllocationTests</c> holds every idle tick to sixteen bytes).
        /// </summary>
        public static SurfaceCensus Take(CellGrid grid, NavGraph nav, DesignationGrid? designations,
            CellRef start, int keepClear, TraverseMode mode, SurfaceCensus census)
        {
            census.Cells.Clear();
            census.Woodland.Clear();
            census.Rock.Clear();
            census.Open.Clear();
            census.Bank.Clear();
            census.Edge.Clear();
            census._set.Clear();
            census._bank.Clear();
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
                bool nearTree = designations != null && NearTree(grid, designations, x, z, y);
                bool besideRock = BesideRock(grid, x, z, y);
                if (nearTree) census.Woodland.Add(cell);
                if (besideRock) census.Rock.Add(cell);
                if (!nearTree && !besideRock) census.Open.Add(cell);
                if (WaterBank.Near(grid, cell, WaterBank.SeedRadius))
                {
                    census.Bank.Add(cell);
                    census._bank.Add(cell);
                }
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

        /// <summary>How far from a group's centre its members may be put — a loose group, not a clump.</summary>
        public const int GroupRadius = 4;

        /// <summary>
        /// How far apart two groups' centres are kept, Chebyshev, where the board allows it
        /// (owner, 2026-09-23: "spread the animals more as I noticed the pigs were all together
        /// - which is fine sometimes"). A sounder stays a sounder; two sounders and the rats
        /// land in different parts of the board. Tried for, not insisted on: a board with one
        /// small woodland gets its hogs there whatever this says.
        /// </summary>
        public const int GroupSpacing = 24;

        /// <summary>Draws made looking for a centre that keeps its distance before one is taken anyway.</summary>
        public const int SpacingDraws = 12;

        /// <summary>
        /// Weighted draws made after the every-kind pass before the seeder gives up short of its
        /// target. 64 until the forest (plan forest-animals §2): at 80 animals on Huge a group is
        /// about two and a half animals, so a target needs thirty-odd draws that land, and a draw
        /// whose habitat the board lacks lands nothing.
        /// </summary>
        public const int MaxDraws = 128;

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
            var centres = new List<int>();
            int placed = 0;

            // Every kind the table names, once, in the table's order (plan forest-animals §2): with
            // a dozen kinds sharing a ceiling, a weighted draw alone leaves the rare ones — the bear,
            // one a board — out of most worlds, and the owner's roster is nine species that are
            // there to be met. A kind named twice (the moose on the bank and in the wood) is
            // guaranteed once, from its first line. Each first group is held to an even share of
            // what is left, so a board with room for a dozen animals still has room for the last
            // kind in the table; the weighted draws grow the herds afterwards.
            var kinds = new HashSet<int>();
            for (int i = 0; i < gen.wildlife.Length; i++)
            {
                int kind = gen.wildlife[i].weight > 0 ? KindIndex(pawns.Content, gen.wildlife[i].kind) : -1;
                if (kind >= 0) kinds.Add(kind);
            }
            var seeded = new HashSet<int>();
            int kindsLeft = kinds.Count;
            for (int i = 0; i < gen.wildlife.Length && placed < target; i++)
            {
                WildlifeEntry entry = gen.wildlife[i];
                if (entry.weight <= 0) continue;
                int kind = KindIndex(pawns.Content, entry.kind);
                if (kind < 0 || seeded.Contains(kind)) continue;
                int share = Math.Max(1, (target - placed) / Math.Max(1, kindsLeft));
                int put = SeedGroup(pawns, census, entry, kind, share, start, keepClear, taken, centres, ref rng);
                if (put > 0) seeded.Add(kind);
                kindsLeft--;
                placed += put;
            }

            // Then the weighted draws to the target.
            for (int attempt = 0; attempt < MaxDraws && placed < target; attempt++)
            {
                WildlifeEntry? entry = Pick(gen.wildlife, ref rng);
                if (entry == null) break;
                int kind = KindIndex(pawns.Content, entry.kind);
                if (kind < 0) continue;
                placed += SeedGroup(pawns, census, entry, kind, target - placed, start, keepClear, taken, centres, ref rng);
            }
            return placed;
        }

        /// <summary>One group of an entry's kind, of at most <paramref name="room"/>, on its habitat.</summary>
        static int SeedGroup(PawnContext pawns, SurfaceCensus census, WildlifeEntry entry, int kind, int room,
            CellRef start, int keepClear, HashSet<int> taken, List<int> centres, ref DeterministicRandom rng)
        {
            int group = Math.Min(GroupSize(entry, ref rng), room);
            List<int> habitat = HabitatCells(census, entry.habitat, start, keepClear, pawns.Size);
            habitat = FarFromStart(habitat, entry.minStartDistance, start, pawns.Size);
            if (habitat.Count == 0) return 0;
            int centre = PickCentre(habitat, centres, pawns.Size, ref rng);
            int put = PlaceGroup(pawns, census, taken, centre, kind, group, ref rng, new List<int>(),
                bankOnly: entry.habitat == Habitat.Bank && census.Bank.Count > 0);
            if (put > 0) centres.Add(centre);
            return put;
        }

        /// <summary>
        /// The cells of <paramref name="habitat"/> at least <paramref name="distance"/> from the
        /// start, Chebyshev — or the habitat as it is, when the distance is nought or nothing that
        /// far out is left (<see cref="WildlifeEntry.minStartDistance"/> is tried for).
        /// </summary>
        internal static List<int> FarFromStart(List<int> habitat, int distance, CellRef start, GridSize size)
        {
            if (distance <= 0) return habitat;
            var far = new List<int>();
            for (int i = 0; i < habitat.Count; i++)
            {
                CellRef c = size.FromIndex(habitat[i]);
                if (Math.Max(Math.Abs(c.X - start.X), Math.Abs(c.Z - start.Z)) >= distance) far.Add(habitat[i]);
            }
            return far.Count > 0 ? far : habitat;
        }

        /// <summary>The cells an entry's habitat offers, with the clearing excluded whatever the habitat.</summary>
        internal static List<int> HabitatCells(SurfaceCensus census, Habitat habitat, CellRef start, int keepClear, GridSize size) =>
            HabitatCells(census, habitat, start, keepClear, size, new List<int>(census.Cells.Count));

        internal static List<int> HabitatCells(SurfaceCensus census, Habitat habitat, CellRef start, int keepClear, GridSize size, List<int> open)
        {
            List<int> list = habitat switch
            {
                Habitat.Woodland => census.Woodland,
                Habitat.Rock => census.Rock,
                Habitat.Bank => census.Bank,
                Habitat.Open => census.Open,
                _ => census.Cells,
            };
            if (list.Count > 0 && habitat != Habitat.Any) return list;
            // Any, or a habitat this board has none of: every surface cell outside the clearing.
            open.Clear();
            for (int i = 0; i < census.Cells.Count; i++)
            {
                CellRef c = size.FromIndex(census.Cells[i]);
                if (Math.Max(Math.Abs(c.X - start.X), Math.Abs(c.Z - start.Z)) <= keepClear) continue;
                open.Add(census.Cells[i]);
            }
            return open;
        }

        /// <summary>
        /// A centre from a habitat list that keeps <see cref="GroupSpacing"/> from every centre
        /// already used, in up to <see cref="SpacingDraws"/> draws; the last draw is taken
        /// whatever its distance, so a cramped board still seeds.
        /// </summary>
        internal static int PickCentre(List<int> habitat, List<int> centres, GridSize size, ref DeterministicRandom rng)
        {
            int centre = habitat[rng.NextInt(habitat.Count)];
            for (int draw = 0; draw < SpacingDraws && !KeepsDistance(centre, centres, size); draw++)
                centre = habitat[rng.NextInt(habitat.Count)];
            return centre;
        }

        static bool KeepsDistance(int cell, List<int> centres, GridSize size)
        {
            CellRef c = size.FromIndex(cell);
            for (int i = 0; i < centres.Count; i++)
            {
                CellRef o = size.FromIndex(centres[i]);
                if (Math.Max(Math.Abs(c.X - o.X), Math.Abs(c.Z - o.Z)) < GroupSpacing) return false;
            }
            return true;
        }

        /// <summary>
        /// Spawn up to <paramref name="count"/> of a kind on census cells within
        /// <see cref="GroupRadius"/> of a centre, <b>scattered</b>: the candidate cells in the
        /// square are collected and drawn at random, so a sounder is a loose group across the
        /// glade rather than a knot at one cell and its neighbours. The centre itself is always
        /// a candidate, so a group of one lands where it was aimed.
        /// </summary>
        internal static int PlaceGroup(PawnContext pawns, SurfaceCensus census, HashSet<int> taken, int centre, int kind, int count, ref DeterministicRandom rng) =>
            PlaceGroup(pawns, census, taken, centre, kind, count, ref rng, new List<int>());

        /// <summary>
        /// <paramref name="bankOnly"/> scatters the group over bank cells alone (design 30 §8): a
        /// frog centred on a stream's edge whose fellows were dealt four cells inland would spend
        /// its first legs walking back to the water.
        /// </summary>
        internal static int PlaceGroup(PawnContext pawns, SurfaceCensus census, HashSet<int> taken, int centre, int kind, int count, ref DeterministicRandom rng, List<int> candidates, bool bankOnly = false)
        {
            GridSize size = pawns.Size;
            CellRef c = size.FromIndex(centre);
            candidates.Clear();
            for (int dz = -GroupRadius; dz <= GroupRadius; dz++)
            for (int dx = -GroupRadius; dx <= GroupRadius; dx++)
            {
                int x = c.X + dx, z = c.Z + dz;
                if (x < 0 || z < 0 || x >= size.SizeX || z >= size.SizeZ) continue;
                int cell = SurfaceCensus.Topmost(pawns.Cells, x, z);
                if (cell < 0 || !census.Contains(cell) || taken.Contains(cell)) continue;
                if (bankOnly && !census.IsBank(cell)) continue;
                if (pawns.Pawns.IsCellOccupiedByStandingPawn(cell)) continue;
                candidates.Add(cell);
            }
            int placed = 0;
            while (placed < count && candidates.Count > 0)
            {
                int at = rng.NextInt(candidates.Count);
                int cell = candidates[at];
                candidates[at] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);
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
