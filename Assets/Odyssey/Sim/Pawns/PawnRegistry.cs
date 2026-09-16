#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Every colonist, in id order.
    ///
    /// The registry is where the pawn simulation meets the three things outside it: the state
    /// hash, the snapshot the interface reads, and the save file. It is registered as an
    /// <see cref="ITickable"/> in the <see cref="TickGroup.Never"/> group purely so that it folds
    /// into <see cref="SimWorld.ComputeStateHash"/> — it has no tick of its own, because the work
    /// is done by the three pawn systems.
    ///
    /// Iteration is always over the list, in ascending id. There is no dictionary walk anywhere
    /// in a tick; the by-id map is probed and never enumerated.
    /// </summary>
    public sealed class PawnRegistry : ITickable, IStateHashable, ISnapshotContributor, ISaveable
    {
        readonly List<Pawn> _pawns = new List<Pawn>();
        readonly Dictionary<int, int> _byId = new Dictionary<int, int>();
        readonly PawnContext _ctx;
        int _nextId = 1;

        internal PawnRegistry(PawnContext ctx) { _ctx = ctx; }

        /// <summary>Ascending by id. The one true iteration order for anything pawn-shaped.</summary>
        public IReadOnlyList<Pawn> All => _pawns;

        public int Count => _pawns.Count;

        public Pawn? Get(PawnId id) => _byId.TryGetValue(id.Value, out int index) ? _pawns[index] : null;

        /// <summary>
        /// Build a colonist at a cell. This is the whole public API for making a pawn: one call,
        /// no partially-initialised intermediate state, and the driver pool built up front so no
        /// job start ever allocates.
        /// </summary>
        public Pawn Spawn(int cell)
        {
            var pawn = new Pawn(new PawnId(_nextId++), cell, _ctx.Content);
            Adopt(pawn);
            return pawn;
        }

        /// <summary>Register a pawn subclass. The seam a mod would use to add a pawn kind.</summary>
        public Pawn Adopt(Pawn pawn)
        {
            pawn.DriverPool = BuildDrivers();
            _byId[pawn.Id.Value] = _pawns.Count;
            _pawns.Add(pawn);
            if (pawn.Id.Value >= _nextId) _nextId = pawn.Id.Value + 1;
            return pawn;
        }

        /// <summary>One instance per job kind, indexed by the driver number a JobDef names.</summary>
        static JobDriver[] BuildDrivers() => new JobDriver[]
        {
            new HaulJobDriver(),
            new EatJobDriver(),
            new SleepJobDriver(),
            new WanderJobDriver(),
            new WaitJobDriver(),
            new FellJobDriver(),
        };

        // ---- ITickable: registration only, so the hash sees the pawns --------------------

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_pawns.Count);
            for (int i = 0; i < _pawns.Count; i++) _pawns[i].ContributeTo(ref hash);
            _ctx.Items.ContributeTo(ref hash);
        }

        // ---- the snapshot seam ------------------------------------------------------------

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            GridSize size = world.Size;
            for (int i = 0; i < _pawns.Count; i++)
            {
                var pawn = _pawns[i];
                // Where the pawn is stepping to, and how far along, so presentation can glide it
                // between cells instead of snapping. A flat orthogonal crossing costs 100 units,
                // so progress converts straight to a percentage; it is clamped because a diagonal
                // or a connector can cost more and would otherwise overshoot.
                var cell = size.FromIndex(pawn.Cell);
                var nextCell = cell;
                int movePercent = 0;
                if (pawn.HasPath)
                {
                    nextCell = size.FromIndex(pawn.Path[pawn.PathIndex]);
                    movePercent = pawn.MoveProgress;
                    if (movePercent < 0) movePercent = 0;
                    else if (movePercent > 100) movePercent = 100;
                }

                // What the pawn is working on, if anything. Asked of the driver rather than
                // derived from the job: only the driver knows whether the walk toil is over,
                // and a figure that swings an axe while walking is worse than one that glides.
                int workFocus = pawn.Driver != null ? pawn.Driver.WorkFocus : -1;

                writer.AddPawn(new PawnView(
                    pawn.Id,
                    cell,
                    pawn.Needs[NeedIndex.Food],
                    pawn.Needs[NeedIndex.Rest],
                    pawn.Mood,
                    pawn.CurrentJob != null ? pawn.CurrentJob.DefIndex : -1,
                    nextCell,
                    movePercent,
                    workFocus >= 0,
                    workFocus >= 0 ? size.FromIndex(workFocus) : cell));
            }

            var items = _ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.Cell < 0) continue;
                writer.AddThing(new ThingView(item.Id, size.FromIndex(item.Cell), item.DefIndex, 0, item.Stack));
            }
        }

        // ---- saving ------------------------------------------------------------------------
        //
        // Mid-job state persists: the job, the toil index, the toil progress and the
        // reservations. Paths do not — they are re-derived on the next tick, because a
        // recomputed path is correct by construction and a saved one can be stale.

        public string SaveKey => "odyssey.pawns";

        public void Save(SaveWriter writer)
        {
            writer.Write(_nextId);
            writer.Write(_pawns.Count);

            for (int i = 0; i < _pawns.Count; i++)
            {
                var pawn = _pawns[i];
                writer.Write(pawn.Id.Value);
                writer.Write(pawn.Cell);

                writer.Write(pawn.Needs.Length);
                for (int n = 0; n < pawn.Needs.Length; n++) writer.Write(pawn.Needs[n]);

                writer.Write(pawn.Mood);
                writer.Write(pawn.MoodTarget);

                writer.Write(pawn.Skills.Length);
                for (int s = 0; s < pawn.Skills.Length; s++) writer.Write(pawn.Skills[s]);

                // Passions as ints, for the same reason the work priorities are below.
                writer.Write(pawn.Passions.Length);
                for (int s = 0; s < pawn.Passions.Length; s++) writer.Write((int)pawn.Passions[s]);

                writer.Write(pawn.SkillGainedToday.Length);
                for (int s = 0; s < pawn.SkillGainedToday.Length; s++) writer.Write(pawn.SkillGainedToday[s]);
                writer.Write(pawn.SkillDay);

                writer.Write(pawn.WorkPriorities.Length);
                // Written as an int, not as the byte it is stored in: the reader asks for an int,
                // and a width mismatch here corrupts every field after it.
                for (int w = 0; w < pawn.WorkPriorities.Length; w++) writer.Write((int)pawn.WorkPriorities[w]);

                writer.Write(pawn.BreakTicksLeft);
                writer.Write(pawn.Asleep);
                writer.Write(pawn.JobStartsInWindow);
                writer.Write(pawn.WindowStartTick);

                writer.Write(pawn.Memories.Count);
                for (int m = 0; m < pawn.Memories.Count; m++)
                {
                    writer.Write(pawn.Memories[m].ThoughtIndex);
                    writer.Write(pawn.Memories[m].ExpiryTick);
                }

                writer.Write(pawn.Destination);
                writer.Write(pawn.MoveProgress);

                var job = pawn.CurrentJob;
                writer.Write(job != null);
                if (job != null)
                {
                    writer.Write(job.DefIndex);
                    writer.Write(job.TargetItem.Value);
                    writer.Write(job.TargetCell);
                    writer.Write(job.DestCell);
                    writer.Write(job.CarriedItem);
                    writer.Write(job.PlayerForced);
                    writer.Write((int)job.Mode);
                    writer.Write(job.WorkTicks);
                    writer.Write(pawn.JobStartTick);
                    writer.Write(pawn.Driver != null ? pawn.Driver.ToilIndex : 0);
                    writer.Write(pawn.Driver != null ? pawn.Driver.ToilProgress : 0);
                }

                writer.Write(pawn.HeldReservations.Count);
                for (int r = 0; r < pawn.HeldReservations.Count; r++) writer.Write(pawn.HeldReservations[r]);
            }
        }

        public void Load(SaveReader reader)
        {
            _pawns.Clear();
            _byId.Clear();
            _ctx.Reservations.Clear();

            _nextId = reader.ReadInt();
            int count = reader.ReadInt();

            for (int i = 0; i < count; i++)
            {
                var pawn = new Pawn(new PawnId(reader.ReadInt()), reader.ReadInt(), _ctx.Content);
                pawn.DriverPool = BuildDrivers();

                int needCount = reader.ReadInt();
                for (int n = 0; n < needCount; n++)
                {
                    int value = reader.ReadInt();
                    if (n < pawn.Needs.Length) pawn.Needs[n] = value;
                }

                pawn.Mood = reader.ReadInt();
                pawn.MoodTarget = reader.ReadInt();

                int skillCount = reader.ReadInt();
                for (int s = 0; s < skillCount; s++)
                {
                    int value = reader.ReadInt();
                    if (s < pawn.Skills.Length) pawn.Skills[s] = value;
                }

                int passionCount = reader.ReadInt();
                for (int s = 0; s < passionCount; s++)
                {
                    int value = reader.ReadInt();
                    if (s < pawn.Passions.Length) pawn.Passions[s] = (byte)value;
                }

                int todayCount = reader.ReadInt();
                for (int s = 0; s < todayCount; s++)
                {
                    int value = reader.ReadInt();
                    if (s < pawn.SkillGainedToday.Length) pawn.SkillGainedToday[s] = value;
                }
                pawn.SkillDay = reader.ReadInt();

                int workCount = reader.ReadInt();
                for (int w = 0; w < workCount; w++)
                {
                    int value = reader.ReadInt();
                    if (w < pawn.WorkPriorities.Length) pawn.WorkPriorities[w] = (byte)value;
                }

                pawn.BreakTicksLeft = reader.ReadInt();
                pawn.Asleep = reader.ReadBool();
                pawn.JobStartsInWindow = reader.ReadInt();
                pawn.WindowStartTick = reader.ReadInt();

                int memoryCount = reader.ReadInt();
                for (int m = 0; m < memoryCount; m++)
                    pawn.Memories.Add(new Memory { ThoughtIndex = reader.ReadInt(), ExpiryTick = reader.ReadInt() });

                pawn.Destination = reader.ReadInt();
                pawn.MoveProgress = reader.ReadInt();

                if (reader.ReadBool())
                {
                    var job = pawn.JobBuffer;
                    job.Reset(reader.ReadInt());
                    job.TargetItem = new ThingId(reader.ReadInt());
                    job.TargetCell = reader.ReadInt();
                    job.DestCell = reader.ReadInt();
                    job.CarriedItem = reader.ReadInt();
                    job.PlayerForced = reader.ReadBool();
                    job.Mode = (TraverseMode)reader.ReadInt();
                    job.WorkTicks = reader.ReadInt();
                    pawn.JobStartTick = reader.ReadInt();

                    var driver = pawn.DriverPool[_ctx.Content.Jobs[job.DefIndex].driver];
                    driver.Begin(pawn, job);
                    driver.ToilIndex = reader.ReadInt();
                    driver.ToilProgress = reader.ReadInt();
                    pawn.CurrentJob = job;
                    pawn.Driver = driver;
                }

                int reservationCount = reader.ReadInt();
                for (int r = 0; r < reservationCount; r++)
                {
                    long key = reader.ReadLong();
                    _ctx.Reservations.Reserve(pawn.Id, key);
                    pawn.HeldReservations.Add(key);
                }

                _byId[pawn.Id.Value] = _pawns.Count;
                _pawns.Add(pawn);
            }
        }
    }
}
