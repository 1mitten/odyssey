#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What the wild does between one order and the next (design 45 §6): a picked berry bush grows
    /// its berries back, and mushrooms come up under the trees.
    ///
    /// <para><b>The only state is the picked bushes</b>, one <i>(ripe-at tick, cell)</i> pair
    /// each, kept sorted so a tick costs one peek at the head. A ripe bush has no entry — it is a
    /// <see cref="NaturalContent.EdificeBerryBush"/> in the edifice list, which is already saved
    /// and hashed — so a board of berry bushes nobody has touched costs nothing here, and an old
    /// save, which has no section, loads with every berry bush ripe.</para>
    ///
    /// <para><b>Mushrooms carry no state at all.</b> Every <see cref="MushroomIntervalTicks"/> the
    /// loose mushroom stacks are counted, and if there are fewer than one per
    /// <see cref="TreesPerMushroom"/> trees the world's own random stream for that tick picks a
    /// tree and puts a stack beside it. A stack that is eaten or hauled in is simply one fewer,
    /// and the next grows somewhere else — the owner's "foraged once, reappearing slowly
    /// elsewhere" with nothing to save.</para>
    /// </summary>
    public sealed class NatureSystem : ITickable, IStateHashable, ISaveable
    {
        /// <summary>Ticks between mushroom censuses. INVENTED: ten a day.</summary>
        public const int MushroomIntervalTicks = 6_000;

        /// <summary>The board holds a mushroom stack for this many standing trees, at most.</summary>
        public const int TreesPerMushroom = 40;

        /// <summary>Trees tried for a spot before a census gives up until the next.</summary>
        const int MushroomAttempts = 8;

        const uint MushroomPurpose = 0x4D555348u;

        readonly PawnContext _pawns;
        readonly List<PlacedEdifice> _edifices;
        readonly List<(int Tick, int Cell)> _picked = new List<(int, int)>();

        public NatureSystem(PawnContext pawns, List<PlacedEdifice> edifices)
        {
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
            _edifices = edifices ?? throw new ArgumentNullException(nameof(edifices));
        }

        /// <summary>The picked bushes and when each is ripe again, soonest first. For tests and the pane.</summary>
        public IReadOnlyList<(int Tick, int Cell)> Picked => _picked;

        public TickGroup TickGroup => TickGroup.Rare;
        public int TickPhaseOffset => 91;

        public void Tick(SimWorld world)
        {
            int now = world.CurrentTick;
            while (_picked.Count > 0 && _picked[0].Tick <= now)
            {
                int cell = _picked[0].Cell;
                _picked.RemoveAt(0);
                Swap(cell, NaturalContent.EdificeBerryBushPicked, NaturalContent.EdificeBerryBush);
            }

            // The census runs on the rare tick that crosses each interval, whatever the phase.
            if (now / MushroomIntervalTicks != (now - (int)TickGroup.Rare) / MushroomIntervalTicks)
                GrowMushrooms(now);
        }

        // ---- berries -------------------------------------------------------------------------

        /// <summary>
        /// A berry bush has just been picked: it is bare until its Def's regrowth has passed.
        /// False, and nothing changes, if what stands there is not a ripe berry bush.
        /// </summary>
        public bool Pick(int cell, int now)
        {
            if (!Swap(cell, NaturalContent.EdificeBerryBush, NaturalContent.EdificeBerryBushPicked)) return false;
            WildPlantDef plant = NaturalContent.WildPlantAt(NaturalContent.EdificeBerryBush)!;
            var entry = (now + Math.Max(1, plant.fruitRegrowTicks), cell);
            int at = _picked.BinarySearch(entry);
            _picked.Insert(at < 0 ? ~at : at, entry);
            return true;
        }

        /// <summary>Change what stands in a cell from one kind to another, in place: the handle,
        /// the flag and everything else the record carries stay as they were.</summary>
        bool Swap(int cell, ushort from, ushort to)
        {
            if ((uint)cell >= (uint)_pawns.Size.CellCount) return false;
            int handle = _pawns.Cells.Edifice[cell];
            if (handle < 0 || handle >= _edifices.Count) return false;
            PlacedEdifice placed = _edifices[handle];
            if (placed.Removed || placed.Def != from) return false;
            placed.Def = to;
            _edifices[handle] = placed;
            _pawns.Chunks?.MarkDirty(_pawns.Size.FromIndex(cell));
            return true;
        }

        // ---- mushrooms -----------------------------------------------------------------------

        void GrowMushrooms(int now)
        {
            int trees = 0;
            for (int i = 0; i < _edifices.Count; i++)
                if (!_edifices[i].Removed && NaturalContent.IsTree(_edifices[i].Def)) trees++;
            int cap = trees / TreesPerMushroom;
            if (cap <= 0) return;

            int stacks = 0;
            IReadOnlyList<ColonyItem> items = _pawns.Items.Items;
            IReadOnlyList<int> loose = _pawns.Items.LooseItems;
            for (int i = 0; i < loose.Count; i++)
                if (items[loose[i]].DefIndex == ItemIndex.Mushrooms) stacks++;
            if (stacks >= cap) return;

            var rng = DeterministicRandom.ForTick(_pawns.Seed, now, MushroomPurpose);
            for (int attempt = 0; attempt < MushroomAttempts; attempt++)
            {
                PlacedEdifice tree = _edifices[rng.NextInt(_edifices.Count)];
                int dir = rng.NextInt(8);
                int count = 3 + rng.NextInt(3);
                if (tree.Removed || !NaturalContent.IsTree(tree.Def)) continue;
                if (_pawns.Cells.Edifice[tree.CellIndex] < 0) continue;
                int cell = UndergrowthPass.BesideTree(_pawns.Cells, tree.CellIndex, dir);
                if (cell < 0 || !_pawns.Items.CellHasSpace(cell, ItemIndex.Mushrooms, count)) continue;
                if (!_pawns.OpenGroundFor(ItemIndex.Mushrooms)(cell)) continue;
                _pawns.Items.Spawn(ItemIndex.Mushrooms, cell, count);
                return;
            }
        }

        // ---- hash and save -------------------------------------------------------------------

        public void ContributeTo(ref StateHash hash)
        {
            // Nothing while nothing is picked, so registering the system moved no golden: a board
            // nobody has foraged hashes exactly as it did before berries could be picked.
            if (_picked.Count == 0) return;
            hash.Add(_picked.Count);
            for (int i = 0; i < _picked.Count; i++)
            {
                hash.Add(_picked[i].Tick);
                hash.Add(_picked[i].Cell);
            }
        }

        public string SaveKey => "odyssey.nature";

        public void Save(SaveWriter writer)
        {
            writer.Write(_picked.Count);
            for (int i = 0; i < _picked.Count; i++)
            {
                writer.Write(_picked[i].Tick);
                writer.Write(_picked[i].Cell);
            }
        }

        public void Load(SaveReader reader)
        {
            _picked.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int tick = reader.ReadInt();
                int cell = reader.ReadInt();
                _picked.Add((tick, cell));
            }
            _picked.Sort();
        }
    }

    /// <summary>
    /// Find a ripe berry bush the player has ordered picked (design 45 §6). Growing work: picking
    /// is harvesting a crop that nobody sowed.
    /// </summary>
    public sealed class ForageWorkGiver : WorkGiver
    {
        public override string Name => "Forage";

        public override int WorkType => WorkTypeIndex.Growing;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var designations = ctx.Designations;
            if (designations == null || ctx.Nature == null) return false;

            var cells = designations.Cells;
            int best = -1, bestStand = -1, bestDistance = int.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                if (designations.At(cell) != DesignationKind.Harvest) continue;
                if (!designations.IsRipeBerryBush(cell)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;
                int stand = FellJobDriver.StandBeside(ctx, pawn, cell);
                if (stand < 0) continue;

                bestDistance = distance;
                best = cell;
                bestStand = stand;
            }

            if (best < 0) return false;
            job.Reset(JobIndex.Forage);
            job.TargetCell = bestStand;
            job.DestCell = best;
            return true;
        }
    }

    /// <summary>
    /// Walk beside a ripe berry bush, pick it, and leave the berries on the ground beside it
    /// (design 45 §6). The bush goes bare and <see cref="NatureSystem"/> grows it back.
    /// </summary>
    public class ForageJobDriver : JobDriver
    {
        public override int WorkType => WorkTypeIndex.Growing;

        /// <summary>No focus, so no axe: picking is the kneel the sower and the harvester share.</summary>
        public override int WorkFocus => -1;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0 || Job.DestCell < 0) return false;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.DestCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            if (ToilIndex == SettleToil) return Settle(ctx);

            var designations = ctx.Designations;
            if (designations == null || ctx.Nature == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            if (designations.At(cell) != DesignationKind.Harvest || !designations.IsRipeBerryBush(cell))
            {
                Pawn.BeginGesture(PawnGesture.None);
                return JobStatus.Failed;
            }

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            if (!StillInReach(ctx, Pawn, cell, layersAbove: 0, layersBelow: 0))
            {
                WalkBack();
                Pawn.BeginGesture(PawnGesture.None);
                return JobStatus.Ongoing;
            }

            if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Sow);

            WildPlantDef plant = NaturalContent.WildPlantAt(NaturalContent.EdificeBerryBush)!;
            ToilProgress += Pawn.WorkRatePerMille(WorkTypeIndex.Growing);
            Work(ctx);
            if (ToilProgress < plant.fruitWorkTicks * Rates.Scale) return JobStatus.Ongoing;

            Pawn.BeginGesture(PawnGesture.None);
            designations.Clear(cell);
            ctx.Defer(_ => Picked(ctx, cell, plant));
            NextToil();
            return JobStatus.Ongoing;
        }

        static void Picked(PawnContext ctx, int cell, WildPlantDef plant)
        {
            if (ctx.Nature == null || !ctx.Nature.Pick(cell, ctx.CurrentTick)) return;
            int item = ctx.Content.ItemIndexOf(plant.fruitYields);
            if (item < 0 || plant.fruitCount <= 0) return;

            // Beside the bush rather than in it: a pile inside a bush is drawn inside the bush.
            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, cell, item, plant.fruitCount, maxRadius: 3,
                accept: c => c != cell && !ctx.Cells.IsUndergrowth(c));
            if (at < 0) at = ctx.Items.NearestCellWithSpace(ctx.Cells, cell, item, plant.fruitCount, maxRadius: 3);
            if (at >= 0) ctx.Items.Spawn(item, at, plant.fruitCount);
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => Pawn.BeginGesture(PawnGesture.None);
    }
}

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Put the loose stones and the first mushrooms where the generator chose (design 45 §6). Run
    /// once, after the scenario has placed its own things and before the first tick; a spot a
    /// starting pile has taken is skipped rather than shared.
    /// </summary>
    public static class NatureSeeder
    {
        public static void Seed(PawnContext pawns, NaturalMapResult map)
        {
            NaturalGenContext gen = map.Context;
            foreach ((int cell, int count) in gen.LooseRocks)
                if (pawns.Items.CellHasSpace(cell)) pawns.Items.Spawn(ItemIndex.Stone, cell, count);
            foreach ((int cell, int count) in gen.MushroomSpots)
                if (pawns.Items.CellHasSpace(cell)) pawns.Items.Spawn(ItemIndex.Mushrooms, cell, count);
        }
    }
}
