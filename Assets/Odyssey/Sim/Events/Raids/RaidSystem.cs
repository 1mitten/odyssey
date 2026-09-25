#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// One raid: a band of hostiles that behaves as one (design 50 §1). The group owns the phase,
    /// the places, the clocks and who is in it; each member's job is still its own.
    ///
    /// <para><b>Everything a raid will do is decided when it fires</b> — where each member walks on,
    /// when, as what, and how long the band loiters — and saved here, so the system's tick draws
    /// nothing and a save taken mid-trickle resumes the same trickle (design 50 §12).</para>
    /// </summary>
    public sealed class RaidGroup
    {
        /// <summary>Stable for the raid's life and across a save; never reused in a world.</summary>
        public int Id;

        /// <summary>The incident that fired it, as an index into the incident content.</summary>
        public int Def;

        /// <summary>The mix it was made from, as an index into <see cref="IncidentContent.MixOrder"/>.</summary>
        public int Mix;

        public RaidPhase Phase;

        /// <summary>The tick the current phase began.</summary>
        public int PhaseTick;

        /// <summary>How long the band gathers once the last member has arrived, drawn at the fire.</summary>
        public int LoiterTicks;

        /// <summary>How long it holds at the probe point.</summary>
        public int ProbeTicks;

        public int GatherCell;
        public int ProbeCell;

        /// <summary>Where the assault makes for. Re-read from the hearth when the assault begins.</summary>
        public int TargetCell;

        /// <summary>How far from its point a member mills, in cells.</summary>
        public int MillRadius;

        /// <summary>A standing colonist this near a standing member, in cells, starts the assault early.</summary>
        public int EarlyTriggerCells;

        /// <summary>The share of the band down or dead, per mille, at which the rest withdraw.</summary>
        public int RetreatPerMille;

        /// <summary>How many it set out with: every member, arrived or not.</summary>
        public int StartingSize;

        public readonly List<RaidMember> Members = new List<RaidMember>();

        /// <summary>Who is still to walk on, in arrival order.</summary>
        public readonly List<RaidArrival> Pending = new List<RaidArrival>();

        /// <summary>Is it in one of the three phases before the assault?</summary>
        public bool Staging => Phase == RaidPhase.Arriving || Phase == RaidPhase.Gathering || Phase == RaidPhase.Probing;
    }

    /// <summary>A member of a raid: the pawn, and whether it walked off the board (which is not dying).</summary>
    public struct RaidMember
    {
        public int Pawn;
        public bool Left;

        public RaidMember(int pawn, bool left)
        {
            Pawn = pawn;
            Left = left;
        }
    }

    /// <summary>A member still to arrive: when, where, and as what kind.</summary>
    public readonly struct RaidArrival
    {
        public readonly int Tick;
        public readonly int Cell;
        public readonly int Kind;

        public RaidArrival(int tick, int cell, int kind)
        {
            Tick = tick;
            Cell = cell;
            Kind = kind;
        }
    }

    /// <summary>
    /// Every raid on the board, and the clock that moves each through its phases (design 50 §3):
    /// arriving, gathering, probing, assaulting, withdrawing. Members ask it what the band is doing
    /// (<see cref="RaidThinkNode"/>); it asks nothing of them but whether they stand.
    ///
    /// <para><b>In the pawn phase at order 15</b>, after the needs (10) and before the jobs (20), so
    /// a member that arrives this tick thinks this tick and a phase change is seen by the think
    /// that follows it.</para>
    ///
    /// <para><b>Scales with the members of live raids</b>: an arrival is a spawn, and every
    /// <see cref="CheckTicks"/> each group counts its members once and, while staging, compares its
    /// bounding box with every standing colonist — members one by one only for a colonist inside it.
    /// Nothing at all while there is no raid, which is nearly always.</para>
    ///
    /// <para><b>Hashed only while a group exists</b>, the pattern <see cref="Projectiles"/> set, so a
    /// colony that has never been raided hashes as it did before raids and no golden moved
    /// (design 50 §10).</para>
    /// </summary>
    public sealed class RaidSystem : IWorldSystem, IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>How often a group's clocks and counts are looked at, in ticks: half a second at normal speed.</summary>
        public const int CheckTicks = 30;

        /// <summary>
        /// Over how many ticks a phase change reaches the band (design 50 §3). A member idling in
        /// the raid's own wander or wait is interrupted on one tick of the window by its place in
        /// the band, so two hundred path searches do not land together.
        /// </summary>
        public const int StaggerTicks = 20;

        /// <summary>The record layout this build writes. 1: the first.</summary>
        public const int Layout = 1;

        readonly PawnContext _ctx;
        readonly List<RaidGroup> _groups = new List<RaidGroup>();
        readonly Dictionary<int, RaidGroup> _byPawn = new Dictionary<int, RaidGroup>();
        int _nextId = 1;

        public RaidSystem(PawnContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public string Name => "Raids";
        public TickPhase Phase => TickPhase.Pawns;
        public int Order => 15;

        /// <summary>The job pipeline, for the phase-change interrupt. Set by the composition root.</summary>
        internal JobSystem? Jobs { get; set; }

        public IReadOnlyList<RaidGroup> Groups => _groups;

        public int Count => _groups.Count;

        /// <summary>How many members are still to arrive, across every raid: room a new raid must leave.</summary>
        public int PendingArrivals
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _groups.Count; i++) n += _groups[i].Pending.Count;
                return n;
            }
        }

        /// <summary>The raid a pawn belongs to, or null. One dictionary lookup.</summary>
        public RaidGroup? GroupOf(int pawn) => _byPawn.TryGetValue(pawn, out RaidGroup group) ? group : null;

        /// <summary>Is this pawn in a raid that is withdrawing? What a thief asks before it looks back to the fight.</summary>
        public bool IsWithdrawing(Pawn pawn) => GroupOf(pawn.Id.Value)?.Phase == RaidPhase.Withdrawing;

        /// <summary>
        /// A new raid, scheduled in full. The group starts <see cref="RaidPhase.Arriving"/> on
        /// <paramref name="tick"/>; its members walk on as their arrivals fall due.
        /// </summary>
        public RaidGroup Begin(int def, int mix, int tick, int gather, int probe, int target, int loiterTicks,
            int probeTicks, int millRadius, int earlyTriggerCells, int retreatPerMille, IReadOnlyList<RaidArrival> arrivals)
        {
            if (arrivals == null || arrivals.Count == 0) throw new ArgumentException("a raid of nobody", nameof(arrivals));
            var group = new RaidGroup
            {
                Id = _nextId++,
                Def = def,
                Mix = mix,
                Phase = RaidPhase.Arriving,
                PhaseTick = tick,
                LoiterTicks = loiterTicks,
                ProbeTicks = probeTicks,
                GatherCell = gather,
                ProbeCell = probe,
                TargetCell = target,
                MillRadius = millRadius,
                EarlyTriggerCells = earlyTriggerCells,
                RetreatPerMille = retreatPerMille,
                StartingSize = arrivals.Count,
            };
            for (int i = 0; i < arrivals.Count; i++) group.Pending.Add(arrivals[i]);
            _groups.Add(group);
            return group;
        }

        /// <summary>
        /// A member walked off the board (<see cref="Theft.Leave"/>): it is gone, and that is not a
        /// death, so it counts toward neither half of the withdrawal.
        /// </summary>
        public void NoteLeft(Pawn pawn)
        {
            RaidGroup? group = GroupOf(pawn.Id.Value);
            if (group == null) return;
            for (int i = 0; i < group.Members.Count; i++)
                if (group.Members[i].Pawn == pawn.Id.Value)
                    group.Members[i] = new RaidMember(pawn.Id.Value, left: true);
        }

        public void Tick(SimWorld world)
        {
            if (_groups.Count == 0) return;
            _ctx.Sync(world);
            int tick = world.CurrentTick;

            for (int g = 0; g < _groups.Count; g++)
            {
                RaidGroup group = _groups[g];
                Arrive(group, tick);
                Stagger(group, tick);
                if (tick % CheckTicks == 0) Advance(group, tick);
            }

            for (int g = _groups.Count - 1; g >= 0; g--)
            {
                RaidGroup group = _groups[g];
                if (group.Pending.Count > 0 || Standing(group) > 0) continue;
                for (int m = 0; m < group.Members.Count; m++) _byPawn.Remove(group.Members[m].Pawn);
                _groups.RemoveAt(g);
            }
        }

        /// <summary>Walk on every member whose arrival has fallen due, on its slot or the nearest free tile to it.</summary>
        void Arrive(RaidGroup group, int tick)
        {
            int due = 0;
            while (due < group.Pending.Count && group.Pending[due].Tick <= tick) due++;
            if (due == 0) return;

            for (int i = 0; i < due; i++)
            {
                RaidArrival arrival = group.Pending[i];
                int cell = _ctx.Pawns.FreeSpawnCell(arrival.Cell);
                Pawn pawn = _ctx.Pawns.Spawn(cell, arrival.Kind);
                group.Members.Add(new RaidMember(pawn.Id.Value, left: false));
                _byPawn[pawn.Id.Value] = group;
            }
            group.Pending.RemoveRange(0, due);
        }

        /// <summary>
        /// The band's clocks, every <see cref="CheckTicks"/>: the last arrival starts the gathering,
        /// the loiter and the probe run out, a poke or a colonist too near starts the assault early,
        /// and half the band down sends the rest away.
        /// </summary>
        void Advance(RaidGroup group, int tick)
        {
            if (group.Phase == RaidPhase.Withdrawing) return;

            if (Broken(group))
            {
                SetPhase(group, RaidPhase.Withdrawing, tick);
                return;
            }

            if (group.Staging && Provoked(group, tick))
            {
                SetPhase(group, RaidPhase.Assaulting, tick);
                return;
            }

            int elapsed = tick - group.PhaseTick;
            switch (group.Phase)
            {
                case RaidPhase.Arriving:
                    if (group.Pending.Count == 0) SetPhase(group, RaidPhase.Gathering, tick);
                    break;
                case RaidPhase.Gathering:
                    if (elapsed >= group.LoiterTicks) SetPhase(group, RaidPhase.Probing, tick);
                    break;
                case RaidPhase.Probing:
                    if (elapsed >= group.ProbeTicks) SetPhase(group, RaidPhase.Assaulting, tick);
                    break;
            }
        }

        /// <summary>
        /// Move a band on. The assault re-reads where it is going: a hearth made or moved while the
        /// band gathered is the one it makes for.
        /// </summary>
        public void SetPhase(RaidGroup group, RaidPhase phase, int tick)
        {
            if (group.Phase == phase) return;
            group.Phase = phase;
            group.PhaseTick = tick;
            if (phase == RaidPhase.Assaulting) group.TargetCell = RaidTargets.Resolve(_ctx, group.GatherCell);
        }

        /// <summary>
        /// The phase change reaching the band (design 50 §3): in the <see cref="StaggerTicks"/> after
        /// it, member <c>i</c> is interrupted on tick <c>i mod StaggerTicks</c> of the window — a
        /// function of saved state alone — if it is idling in the raid's own wander or wait. A member
        /// in a fight or a theft is left to its own job, which looks up on its own cadence.
        /// </summary>
        void Stagger(RaidGroup group, int tick)
        {
            if (Jobs == null) return;
            int window = tick - group.PhaseTick - 1;
            if (window < 0 || window >= StaggerTicks) return;
            for (int m = window; m < group.Members.Count; m += StaggerTicks)
            {
                Pawn? pawn = _ctx.Pawns.Get(new PawnId(group.Members[m].Pawn));
                if (pawn == null || pawn.Downed || pawn.CurrentJob == null) continue;
                int job = pawn.CurrentJob.DefIndex;
                if (job != JobIndex.Wander && job != JobIndex.Wait) continue;
                Jobs.Interrupt(pawn, JobStatus.Failed);
            }
        }

        /// <summary>Half the band — or whatever share the incident says — down or dead.</summary>
        bool Broken(RaidGroup group)
        {
            int lost = 0;
            for (int m = 0; m < group.Members.Count; m++)
            {
                RaidMember member = group.Members[m];
                if (member.Left) continue;
                Pawn? pawn = _ctx.Pawns.Get(new PawnId(member.Pawn));
                if (pawn == null || !Melee.IsStanding(pawn)) lost++;
            }
            return lost > 0 && lost * 1000 >= group.RetreatPerMille * group.StartingSize;
        }

        /// <summary>
        /// Has anything poked the band while it stages? A member struck — it remembers who, or it is
        /// down — or a standing colonist within <see cref="RaidGroup.EarlyTriggerCells"/> of a standing
        /// member, measured on the ground plan in cells. The members' bounding box is taken first and
        /// only a colonist inside it grown by the reach is compared member by member.
        /// </summary>
        bool Provoked(RaidGroup group, int tick)
        {
            GridSize size = _ctx.Size;
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            for (int m = 0; m < group.Members.Count; m++)
            {
                Pawn? pawn = _ctx.Pawns.Get(new PawnId(group.Members[m].Pawn));
                if (pawn == null) continue;
                if (pawn.Downed) return true;
                if (pawn.RetaliateAgainst != 0 && tick < pawn.RetaliateUntilTick) return true;
                CellRef at = size.FromIndex(pawn.Cell);
                if (at.X < minX) minX = at.X;
                if (at.X > maxX) maxX = at.X;
                if (at.Z < minZ) minZ = at.Z;
                if (at.Z > maxZ) maxZ = at.Z;
            }
            if (minX == int.MaxValue) return false;

            int reach = group.EarlyTriggerCells;
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn colonist = pawns[i];
                if (!colonist.IsColonist || !Melee.IsStanding(colonist)) continue;
                CellRef c = size.FromIndex(colonist.Cell);
                if (c.X < minX - reach || c.X > maxX + reach || c.Z < minZ - reach || c.Z > maxZ + reach) continue;
                for (int m = 0; m < group.Members.Count; m++)
                {
                    Pawn? pawn = _ctx.Pawns.Get(new PawnId(group.Members[m].Pawn));
                    if (pawn == null) continue;
                    CellRef at = size.FromIndex(pawn.Cell);
                    if (Math.Max(Math.Abs(at.X - c.X), Math.Abs(at.Z - c.Z)) <= reach) return true;
                }
            }
            return false;
        }

        /// <summary>Members on the board and not down.</summary>
        public int Standing(RaidGroup group)
        {
            int n = 0;
            for (int m = 0; m < group.Members.Count; m++)
            {
                if (group.Members[m].Left) continue;
                Pawn? pawn = _ctx.Pawns.Get(new PawnId(group.Members[m].Pawn));
                if (pawn != null && Melee.IsStanding(pawn)) n++;
            }
            return n;
        }

        // ---- hash --------------------------------------------------------------------------------

        public void ContributeTo(ref StateHash hash)
        {
            if (_groups.Count == 0) return;
            hash.Add(_groups.Count);
            hash.Add(_nextId);
            for (int g = 0; g < _groups.Count; g++)
            {
                RaidGroup group = _groups[g];
                hash.Add(group.Id);
                hash.Add(group.Def);
                hash.Add(group.Mix);
                hash.Add((int)group.Phase);
                hash.Add(group.PhaseTick);
                hash.Add(group.LoiterTicks);
                hash.Add(group.ProbeTicks);
                hash.Add(group.GatherCell);
                hash.Add(group.ProbeCell);
                hash.Add(group.TargetCell);
                hash.Add(group.MillRadius);
                hash.Add(group.EarlyTriggerCells);
                hash.Add(group.RetreatPerMille);
                hash.Add(group.StartingSize);
                hash.Add(group.Members.Count);
                for (int m = 0; m < group.Members.Count; m++)
                {
                    hash.Add(group.Members[m].Pawn);
                    hash.Add(group.Members[m].Left ? 1 : 0);
                }
                hash.Add(group.Pending.Count);
                for (int p = 0; p < group.Pending.Count; p++)
                {
                    hash.Add(group.Pending[p].Tick);
                    hash.Add(group.Pending[p].Cell);
                    hash.Add(group.Pending[p].Kind);
                }
            }
        }

        // ---- snapshot ----------------------------------------------------------------------------

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            GridSize size = _ctx.Size;
            for (int g = 0; g < _groups.Count; g++)
            {
                RaidGroup group = _groups[g];
                long sx = 0, sy = 0, sz = 0;
                int standing = 0;
                for (int m = 0; m < group.Members.Count; m++)
                {
                    if (group.Members[m].Left) continue;
                    Pawn? pawn = _ctx.Pawns.Get(new PawnId(group.Members[m].Pawn));
                    if (pawn == null || !Melee.IsStanding(pawn)) continue;
                    CellRef at = size.FromIndex(pawn.Cell);
                    sx += at.X;
                    sy += at.Y;
                    sz += at.Z;
                    standing++;
                }
                CellRef centre = standing > 0
                    ? new CellRef((int)(sx / standing), (int)(sy / standing), (int)(sz / standing))
                    : size.FromIndex(group.GatherCell);
                writer.AddRaid(new RaidView(group.Id, group.Phase, group.Mix, group.StartingSize, standing, centre,
                    size.FromIndex(group.TargetCell)));
            }
        }

        // ---- save --------------------------------------------------------------------------------

        public string SaveKey => "odyssey.raids";

        public void Save(SaveWriter writer)
        {
            writer.Write(Layout);
            writer.Write(_nextId);
            writer.Write(_groups.Count);
            for (int g = 0; g < _groups.Count; g++)
            {
                RaidGroup group = _groups[g];
                writer.Write(group.Id);
                writer.Write(group.Def);
                writer.Write(group.Mix);
                writer.Write((int)group.Phase);
                writer.Write(group.PhaseTick);
                writer.Write(group.LoiterTicks);
                writer.Write(group.ProbeTicks);
                writer.Write(group.GatherCell);
                writer.Write(group.ProbeCell);
                writer.Write(group.TargetCell);
                writer.Write(group.MillRadius);
                writer.Write(group.EarlyTriggerCells);
                writer.Write(group.RetreatPerMille);
                writer.Write(group.StartingSize);
                writer.Write(group.Members.Count);
                for (int m = 0; m < group.Members.Count; m++)
                {
                    writer.Write(group.Members[m].Pawn);
                    writer.Write(group.Members[m].Left);
                }
                writer.Write(group.Pending.Count);
                for (int p = 0; p < group.Pending.Count; p++)
                {
                    writer.Write(group.Pending[p].Tick);
                    writer.Write(group.Pending[p].Cell);
                    writer.Write(group.Pending[p].Kind);
                }
            }
        }

        public void Load(SaveReader reader)
        {
            _groups.Clear();
            _byPawn.Clear();
            int layout = reader.ReadInt();
            if (layout != Layout)
                throw new SaveLoadException($"{SaveKey} layout {layout} is not one this build reads ({Layout}).");
            _nextId = reader.ReadInt();
            int groups = reader.ReadInt();
            for (int g = 0; g < groups; g++)
            {
                var group = new RaidGroup
                {
                    Id = reader.ReadInt(),
                    Def = reader.ReadInt(),
                    Mix = reader.ReadInt(),
                    Phase = (RaidPhase)reader.ReadInt(),
                    PhaseTick = reader.ReadInt(),
                    LoiterTicks = reader.ReadInt(),
                    ProbeTicks = reader.ReadInt(),
                    GatherCell = reader.ReadInt(),
                    ProbeCell = reader.ReadInt(),
                    TargetCell = reader.ReadInt(),
                    MillRadius = reader.ReadInt(),
                    EarlyTriggerCells = reader.ReadInt(),
                    RetreatPerMille = reader.ReadInt(),
                    StartingSize = reader.ReadInt(),
                };
                int members = reader.ReadInt();
                for (int m = 0; m < members; m++)
                {
                    int pawn = reader.ReadInt();
                    bool left = reader.ReadBool();
                    group.Members.Add(new RaidMember(pawn, left));
                    _byPawn[pawn] = group;
                }
                int pending = reader.ReadInt();
                for (int p = 0; p < pending; p++)
                    group.Pending.Add(new RaidArrival(reader.ReadInt(), reader.ReadInt(), reader.ReadInt()));
                _groups.Add(group);
            }
        }
    }
}
