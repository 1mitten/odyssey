#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Pawns.Wildlife
{
    /// <summary>
    /// Keeps a world's wild population at the level its table asks for (design 30 §3), the way a
    /// map holds a level rather than a one-off scatter: on the rare tick, an animal may decide to
    /// leave and walk off the board's edge, and a board below its target is topped up by a group
    /// that walks in at the edge.
    ///
    /// <para><b>Nothing here is saved and nothing is hashed.</b> Every decision is drawn from the
    /// world seed and the tick, and the one piece of memory — that an animal has decided to go —
    /// lives on the pawn (<see cref="Pawn.Leaving"/>), where the hash and the save already look.
    /// So a save mid-departure resumes the departure, and two runs of one seed leave and arrive
    /// on the same ticks.</para>
    ///
    /// <para><b>Leaving is removal, and removal is new.</b> Nothing ever left the pawn registry
    /// before this — no health model, no death — so <see cref="PawnRegistry.Despawn"/> is this
    /// unit's, and its one caller is here: an animal that has decided to go, is standing on an
    /// edge cell, and has nothing in hand. The interface already copes with a pawn that is not in
    /// the snapshot, because it copes with one on another layer.</para>
    /// </summary>
    public sealed class WildlifeSystem : ITickable
    {
        /// <summary>Per animal, per rare tick, in per mille: a mean stay of about two days.</summary>
        public const int DeparturePerMille = 2;

        /// <summary>Per rare tick while below target, in per mille: topped up within about a minute.</summary>
        public const int ArrivalPerMille = 60;

        readonly PawnContext _pawns;
        readonly JobSystem _jobs;
        readonly MapGenDef _gen;
        readonly CellRef _start;
        readonly int _keepClear;
        readonly List<Pawn> _scratch = new List<Pawn>();

        // Kept and refilled rather than made per arrival check: a census of a 120 x 120 board
        // is tens of thousands of ints, and an idle tick is held to sixteen bytes.
        readonly SurfaceCensus _census = new SurfaceCensus();
        readonly HashSet<int> _taken = new HashSet<int>();
        readonly List<int> _candidates = new List<int>();

        public WildlifeSystem(PawnContext pawns, JobSystem jobs, MapGenDef gen, CellRef start, int keepClear)
        {
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
            _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
            _gen = gen ?? throw new ArgumentNullException(nameof(gen));
            _start = start;
            _keepClear = keepClear;
        }

        public TickGroup TickGroup => TickGroup.Rare;
        public int TickPhaseOffset => 173;

        /// <summary>How many animals are on the board, leaving or not.</summary>
        public int Population
        {
            get
            {
                int n = 0;
                IReadOnlyList<Pawn> all = _pawns.Pawns.All;
                for (int i = 0; i < all.Count; i++) if (!all[i].IsPerson) n++;
                return n;
            }
        }

        /// <summary>The population the table asks for on this board, from a fresh census.</summary>
        public int Target(SurfaceCensus census) => WildlifeSeeder.TargetFor(_gen, census.Cells.Count);

        public void Tick(SimWorld world)
        {
            if (_gen.wildlife.Length == 0 || _gen.wildlifePer10000Columns <= 0) return;
            var rng = DeterministicRandom.ForTick(_pawns.Seed, world.CurrentTick, WildlifePurpose.Level);

            // Departures first: whoever has reached the edge goes, and a few more decide to.
            _scratch.Clear();
            IReadOnlyList<Pawn> all = _pawns.Pawns.All;
            for (int i = 0; i < all.Count; i++) if (!all[i].IsPerson) _scratch.Add(all[i]);
            int population = _scratch.Count;
            for (int i = 0; i < _scratch.Count; i++)
            {
                Pawn animal = _scratch[i];
                if (animal.Leaving)
                {
                    if (AtEdge(animal) && !animal.HasPath && (animal.CurrentJob == null || animal.CurrentJob.DefIndex == JobIndex.Wait))
                    {
                        _jobs.EndJob(animal, JobStatus.Succeeded);
                        _pawns.Pawns.Despawn(animal);
                        population--;
                    }
                    continue;
                }
                if (rng.NextInt(1000) < DeparturePerMille) animal.Leaving = true;
            }

            // Then an arrival, if the board is short. The census is retaken rather than kept:
            // the board is mined and built on, and a stale edge list is an arrival in a wall.
            if (population >= _gen.wildlifeCeiling) return;
            if (rng.NextInt(1000) >= ArrivalPerMille) return;
            SurfaceCensus census = SurfaceCensus.Take(_pawns.Cells, _pawns.Nav, _pawns.Designations, _start, _keepClear, TraverseMode.Animal, _census);
            int target = Target(census);
            if (population >= target || census.Edge.Count == 0) return;

            WildlifeEntry? entry = WildlifeSeeder.Pick(_gen.wildlife, ref rng);
            if (entry == null) return;
            int kind = WildlifeSeeder.KindIndex(_pawns.Content, entry.kind);
            if (kind < 0) return;
            int group = Math.Min(WildlifeSeeder.GroupSize(entry, ref rng), Math.Min(target, _gen.wildlifeCeiling) - population);
            if (group <= 0) return;
            int centre = census.Edge[rng.NextInt(census.Edge.Count)];
            _taken.Clear();
            WildlifeSeeder.PlaceGroup(_pawns, census, _taken, centre, kind, group, ref rng, _candidates);
        }

        /// <summary>On the board's outer ring, whatever the layer.</summary>
        public bool AtEdge(Pawn pawn) => IsEdge(_pawns.Size, pawn.Cell);

        public static bool IsEdge(GridSize size, int cell)
        {
            CellRef c = size.FromIndex(cell);
            return c.X == 0 || c.Z == 0 || c.X == size.SizeX - 1 || c.Z == size.SizeZ - 1;
        }
    }
}
