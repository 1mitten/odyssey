#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// Everything that has happened, in order (design 23 §5): the archive the History screen
    /// will read, the tail the Events panel reads now, and the memory a storyteller's refire
    /// gate will consult.
    ///
    /// <para><b>Append-only, and the id is the count.</b> An entry is never edited or removed —
    /// dismissing a row is the interface forgetting, not the world — so an id is stable for the
    /// life of the colony and a reader can hold "the highest id I have seen" as its whole state
    /// (<see cref="BulletinView"/>). The per-incident counters are derived from the entries and
    /// rebuilt on load, so they are neither saved nor hashed: a derived number in the hash is a
    /// number that can disagree with its own source.</para>
    /// </summary>
    public sealed class IncidentLedger : IStateHashable, ISaveable, ISnapshotContributor
    {
        public readonly struct Entry
        {
            /// <summary>From 1, in firing order, never reused.</summary>
            public readonly int Id;
            public readonly int Tick;
            public readonly int IncidentDef;

            /// <summary>Where, as a whole-world cell index.</summary>
            public readonly int Cell;

            public Entry(int id, int tick, int incidentDef, int cell)
            {
                Id = id;
                Tick = tick;
                IncidentDef = incidentDef;
                Cell = cell;
            }
        }

        /// <summary>
        /// What an entry is about, for the few that are about something (design 33 §17): the item
        /// def a bandit carried off and how many. Keyed by entry id, ascending, because entries
        /// are appended in id order and so are these.
        /// </summary>
        public readonly struct Detail
        {
            public readonly int Id;

            /// <summary>An item def, as an <see cref="ItemHandle"/> value.</summary>
            public readonly int Subject;

            public readonly int Amount;

            public Detail(int id, int subject, int amount)
            {
                Id = id;
                Subject = subject;
                Amount = amount;
            }
        }

        readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// Sparse beside <see cref="_entries"/>: one row for each entry that is about a thing, none
        /// for the rest. Its own save section (<see cref="DetailSection"/>), appended, so a ledger
        /// entry stays four integers and no save format moved; hashed only while it has a row.
        /// </summary>
        readonly List<Detail> _details = new List<Detail>();
        readonly int[] _lastFired;
        readonly int[] _fires;
        readonly GridSize _size;
        readonly IncidentContent _content;

        public IncidentLedger(IncidentContent content, GridSize size)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _lastFired = new int[content.Count];
            _fires = new int[content.Count];
            _size = size;
            Array.Fill(_lastFired, -1);
            DetailSection = new Details(this);
        }

        /// <summary>
        /// The ledger's second save section, <c>odyssey.incidents.detail</c>: the rows of
        /// <see cref="Detail"/>. Listed after the ledger's own section in the world's components,
        /// whose load clears them, so a save from before it loads with none.
        /// </summary>
        public ISaveable DetailSection { get; }

        public int Count => _entries.Count;

        public Entry this[int index] => _entries[index];

        /// <summary>The tick this incident last fired on, or -1 if it never has.</summary>
        public int LastFiredTick(int incidentDef) => _lastFired[incidentDef];

        /// <summary>How many times this incident has fired.</summary>
        public int Fires(int incidentDef) => _fires[incidentDef];

        /// <summary>Write one firing down. Returns its id.</summary>
        public int Record(int incidentDef, int cell, int tick)
        {
            int id = _entries.Count + 1;
            _entries.Add(new Entry(id, tick, incidentDef, cell));
            Note(incidentDef, tick);
            return id;
        }

        /// <summary>
        /// Write down something that happened to a thing (design 33 §17): <paramref name="amount"/>
        /// of item def <paramref name="subject"/>. A <paramref name="subject"/> below nought records
        /// the entry alone, exactly as <see cref="Record(int, int, int)"/> does.
        /// </summary>
        public int Record(int incidentDef, int cell, int tick, int subject, int amount)
        {
            int id = Record(incidentDef, cell, tick);
            if (subject >= 0) _details.Add(new Detail(id, subject, amount));
            return id;
        }

        /// <summary>What entry <paramref name="id"/> is about, if anything. A binary search of the sparse rows.</summary>
        public bool TryGetDetail(int id, out Detail detail)
        {
            int low = 0, high = _details.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int at = _details[mid].Id;
                if (at == id) { detail = _details[mid]; return true; }
                if (at < id) low = mid + 1;
                else high = mid - 1;
            }
            detail = default;
            return false;
        }

        void Note(int incidentDef, int tick)
        {
            if ((uint)incidentDef >= (uint)_fires.Length) return;
            _fires[incidentDef]++;
            _lastFired[incidentDef] = tick;
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                hash.Add(entry.Id);
                hash.Add(entry.Tick);
                hash.Add(entry.IncidentDef);
                hash.Add(entry.Cell);
            }

            // Only while set, so a colony no bandit ever robbed hashes as it did before.
            if (_details.Count == 0) return;
            hash.Add(_details.Count);
            for (int i = 0; i < _details.Count; i++)
            {
                hash.Add(_details[i].Id);
                hash.Add(_details[i].Subject);
                hash.Add(_details[i].Amount);
            }
        }

        /// <summary>The newest <see cref="BulletinView.PublishedTail"/> entries, oldest first.</summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            int from = Math.Max(0, _entries.Count - BulletinView.PublishedTail);
            for (int i = from; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                int favourability = (uint)entry.IncidentDef < (uint)_content.Count
                    ? (int)_content.Defs[entry.IncidentDef].favourability
                    : (int)IncidentFavourability.Neutral;
                int subject = -1, amount = 0;
                if (TryGetDetail(entry.Id, out Detail detail))
                {
                    subject = detail.Subject;
                    amount = detail.Amount;
                }
                writer.AddBulletin(new BulletinView(
                    entry.Id, entry.IncidentDef, _size.FromIndex(entry.Cell), entry.Tick, favourability, subject, amount));
            }
        }

        public string SaveKey => "odyssey.incidents";

        public void Save(SaveWriter writer)
        {
            writer.Write(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                writer.Write(entry.Id);
                writer.Write(entry.Tick);
                writer.Write(entry.IncidentDef);
                writer.Write(entry.Cell);
            }
        }

        public void Load(SaveReader reader)
        {
            _entries.Clear();
            // The detail section follows and refills these; a save from before it has none.
            _details.Clear();
            Array.Clear(_fires, 0, _fires.Length);
            Array.Fill(_lastFired, -1);

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var entry = new Entry(reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt());
                _entries.Add(entry);
                Note(entry.IncidentDef, entry.Tick);
            }
        }

        /// <summary>The detail rows as their own section. See <see cref="DetailSection"/>.</summary>
        sealed class Details : ISaveable
        {
            readonly IncidentLedger _ledger;

            public Details(IncidentLedger ledger) { _ledger = ledger; }

            public string SaveKey => "odyssey.incidents.detail";

            public void Save(SaveWriter writer)
            {
                writer.Write(_ledger._details.Count);
                for (int i = 0; i < _ledger._details.Count; i++)
                {
                    Detail detail = _ledger._details[i];
                    writer.Write(detail.Id);
                    writer.Write(detail.Subject);
                    writer.Write(detail.Amount);
                }
            }

            public void Load(SaveReader reader)
            {
                _ledger._details.Clear();
                int count = reader.ReadInt();
                for (int i = 0; i < count; i++)
                    _ledger._details.Add(new Detail(reader.ReadInt(), reader.ReadInt(), reader.ReadInt()));
            }
        }
    }
}
