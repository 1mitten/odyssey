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

        readonly List<Entry> _entries = new List<Entry>();
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
        }

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
                writer.AddBulletin(new BulletinView(
                    entry.Id, entry.IncidentDef, _size.FromIndex(entry.Cell), entry.Tick, favourability));
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
    }
}
