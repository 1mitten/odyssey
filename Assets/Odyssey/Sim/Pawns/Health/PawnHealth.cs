#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// One injury on one region (design 43 §2): a record of the ledger. Points are in thousandths,
    /// the pool's unit, so a record's severity and the pool's loss are the same number.
    /// </summary>
    public struct Affliction
    {
        /// <summary>The region's index in <see cref="HealthDef.regions"/>.</summary>
        public int Region;

        public AfflictionKind Kind;

        /// <summary>Points it has taken, in thousandths. Nought is healed, and the record goes.</summary>
        public int SeverityMilli;

        /// <summary>Somebody has tended it since it last grew.</summary>
        public bool Tended;

        /// <summary>How well, per mille, once tended; nought otherwise.</summary>
        public int TendQualityPerMille;

        /// <summary>The tick it was last made worse.</summary>
        public int Tick;

        /// <summary>A wound bleeds until it is tended; nothing else ever bleeds.</summary>
        public bool Bleeding => Kind == AfflictionKind.Wound && !Tended;
    }

    /// <summary>
    /// A pawn's injuries and blood (design 43 §2, §4): the ledger over the pool.
    ///
    /// <para><b>Records merge by (region, kind)</b>: a second cut on a wounded leg adds to the one
    /// record and re-opens it, untended and bleeding. So a body of six regions carries at most
    /// eighteen records whatever the fight was, and the save, the hash, the snapshot and the tab
    /// all read a bounded list (design 43 §13: do not unmerge them).</para>
    ///
    /// <para><b>The pool is not derived from this, and is checked against it</b>: for a person
    /// with a body, <c>HpMaxMilli − HpMilli == TotalSeverityMilli</c> always, because
    /// <see cref="CombatSystem.Hurt"/> and the heal write both in the same call. Animals have no
    /// ledger and every reader of the pool is untouched.</para>
    ///
    /// <para>Created the first time the pawn is hurt and kept; <see cref="IsEmpty"/> is what the
    /// save and the hash ask, so a pawn who healed hashes as one who was never touched.</para>
    /// </summary>
    public sealed class PawnHealth
    {
        /// <summary>Six regions by three kinds: the most a body can carry, by construction.</summary>
        public const int MaxRecords = 6 * 3;

        readonly Affliction[] _records = new Affliction[MaxRecords];

        /// <summary>How many records there are. They are <c>this[0]</c> to <c>this[Count - 1]</c>.</summary>
        public int Count { get; private set; }

        /// <summary>Blood lost, in millionths — per mille × 1,000, so a slow bleed spread over a day is exact.</summary>
        public int BloodLossMicro { get; set; }

        public ref Affliction this[int index] => ref _records[index];

        /// <summary>Nothing to say: no record and no blood lost. What the save and the hash ask.</summary>
        public bool IsEmpty => Count == 0 && BloodLossMicro == 0;

        /// <summary>The record of this kind on this region, or -1.</summary>
        public int Find(int region, AfflictionKind kind)
        {
            for (int i = 0; i < Count; i++)
                if (_records[i].Region == region && _records[i].Kind == kind) return i;
            return -1;
        }

        /// <summary>
        /// Add points to the record of this kind on this region, making one if there is none. A
        /// record made worse is untended again, so a wound re-opened bleeds.
        /// </summary>
        public void Add(int region, AfflictionKind kind, int milli, int tick)
        {
            if (milli <= 0) return;
            int i = Find(region, kind);
            if (i < 0)
            {
                if (Count >= MaxRecords) return;
                i = Count++;
                _records[i] = new Affliction { Region = region, Kind = kind };
            }

            ref Affliction record = ref _records[i];
            record.SeverityMilli += milli;
            record.Tended = false;
            record.TendQualityPerMille = 0;
            record.Tick = tick;
        }

        /// <summary>Restore a record exactly as saved. The loader's alone.</summary>
        public void Restore(in Affliction record)
        {
            if (Count >= MaxRecords) return;
            _records[Count++] = record;
        }

        /// <summary>Take the record out, keeping the order of the rest (the order is saved and hashed).</summary>
        public void RemoveAt(int index)
        {
            for (int i = index; i < Count - 1; i++) _records[i] = _records[i + 1];
            Count--;
            _records[Count] = default;
        }

        /// <summary>Points on one region, in thousandths.</summary>
        public int RegionDamageMilli(int region)
        {
            int total = 0;
            for (int i = 0; i < Count; i++) if (_records[i].Region == region) total += _records[i].SeverityMilli;
            return total;
        }

        /// <summary>Every point on the body, in thousandths.</summary>
        public int TotalSeverityMilli
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Count; i++) total += _records[i].SeverityMilli;
                return total;
            }
        }

        /// <summary>Points of wound still bleeding, in thousandths.</summary>
        public int BleedingSeverityMilli
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Count; i++) if (_records[i].Bleeding) total += _records[i].SeverityMilli;
                return total;
            }
        }

        /// <summary>How many records are tended.</summary>
        public int TendedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) if (_records[i].Tended) n++;
                return n;
            }
        }

        /// <summary>How many records nobody has tended.</summary>
        public int UntendedCount => Count - TendedCount;

        /// <summary>The best tend quality on the body, per mille, or -1 when nothing is tended.</summary>
        public int BestTendQualityPerMille
        {
            get
            {
                int best = -1;
                for (int i = 0; i < Count; i++)
                    if (_records[i].Tended && _records[i].TendQualityPerMille > best) best = _records[i].TendQualityPerMille;
                return best;
            }
        }

        /// <summary>
        /// Heal <paramref name="milli"/> points off the ledger, the most severe record first (the
        /// lowest index on a tie), removing each that reaches nought. Returns what it could spend,
        /// which is less only when the ledger ran out.
        /// </summary>
        public int Heal(int milli)
        {
            int spent = 0;
            while (milli > 0 && Count > 0)
            {
                int worst = 0;
                for (int i = 1; i < Count; i++)
                    if (_records[i].SeverityMilli > _records[worst].SeverityMilli) worst = i;

                ref Affliction record = ref _records[worst];
                int take = milli < record.SeverityMilli ? milli : record.SeverityMilli;
                record.SeverityMilli -= take;
                milli -= take;
                spent += take;
                if (record.SeverityMilli <= 0) RemoveAt(worst);
            }
            return spent;
        }

        /// <summary>Tend every untended record at one quality. Returns how many were tended.</summary>
        public int TendAll(int qualityPerMille)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                if (_records[i].Tended) continue;
                _records[i].Tended = true;
                _records[i].TendQualityPerMille = qualityPerMille;
                n++;
            }
            return n;
        }

        /// <summary>Everything that decides the future, in the order it is saved.</summary>
        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(BloodLossMicro);
            hash.Add(Count);
            for (int i = 0; i < Count; i++)
            {
                ref Affliction r = ref _records[i];
                hash.Add(r.Region);
                hash.Add((int)r.Kind);
                hash.Add(r.SeverityMilli);
                hash.Add(r.Tended);
                hash.Add(r.TendQualityPerMille);
                hash.Add(r.Tick);
            }
        }
    }
}
