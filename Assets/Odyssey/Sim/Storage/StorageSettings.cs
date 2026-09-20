#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Storage
{
    /// <summary>
    /// How much the colony cares about a store. Higher wins, and priority orders the
    /// <em>destination</em> — never the haul queue (<c>a-14</c> §3).
    ///
    /// <para>Named rather than numbered, which is the owner's call (2026-09-20) and the reference's
    /// too: a player reading "Preferred" knows what it will do, where a player reading "4" has to
    /// remember whether high or low wins.</para>
    /// </summary>
    public static class StoragePriority
    {
        public const int Last = 0;
        public const int Low = 1;

        /// <summary>What a zone is drawn at unless the player says otherwise.</summary>
        public const int Normal = 2;

        public const int Preferred = 3;
        public const int Urgent = 4;
        public const int Count = 5;
    }

    /// <summary>
    /// The two buttons a filter offers beside its rows. Two, not eight: one per category would
    /// say exactly what the category rows already say (docs/plans/storage.md decision 28).
    /// </summary>
    public static class StoragePreset
    {
        /// <summary>Everything goes in. What a new zone is, so a stockpile starts as a dumping zone and the player narrows it (decision 22).</summary>
        public const int Everything = 0;

        public const int Nothing = 1;
        public const int Count = 2;
    }

    /// <summary>
    /// What a store accepts and how much the colony cares about it — the record a zone points at,
    /// and the one a crate will point at when crates exist.
    ///
    /// <para><b>Pointed at, not held.</b> Two stores sharing one record is what a storage group is
    /// (decision 7), and sharing by C# reference would be invisible to the save file and to the
    /// hash: two zones pointing at one object serialise as two records and come back as two, so
    /// the group would quietly dissolve across a save. <see cref="StorageSettingsTable"/> gives
    /// each record an id, and an id serialises as an integer that is equal on both sides.</para>
    /// </summary>
    public sealed class StorageSettings
    {
        public StorageSettings(int priority, bool[] allow, bool allowUnknown)
        {
            Priority = priority;
            Allow = allow;
            AllowUnknown = allowUnknown;
        }

        /// <summary>A <see cref="StoragePriority"/> value.</summary>
        public int Priority;

        /// <summary>The filter, one flag per item def index.</summary>
        public bool[] Allow;

        /// <summary>
        /// What a def this record has never heard of gets — a commodity added to the game after
        /// the record was written.
        ///
        /// <para><b>Authored, not inferred, and that is the whole of the fault it closes.</b> A
        /// filter is saved with its own length and read back at that length, so a save written
        /// before a def existed left <c>Accepts</c> answering false for it for ever, silently. The
        /// two cases a file cannot tell apart without this flag are a zone created with
        /// <see cref="StoragePreset.Everything"/> — where the player means the new thing to go in
        /// — and one created with <see cref="StoragePreset.Nothing"/> and two rows ticked, where
        /// they do not.</para>
        /// </summary>
        public bool AllowUnknown;

        public bool Accepts(int defIndex) =>
            (uint)defIndex < (uint)Allow.Length ? Allow[defIndex] : AllowUnknown;

        /// <summary>Set one item def's flag.</summary>
        public void SetDef(int defIndex, bool on)
        {
            if ((uint)defIndex < (uint)Allow.Length) Allow[defIndex] = on;
        }

        /// <summary>Set every def of one category at once — the filter row the player actually clicks.</summary>
        public void SetCategory(ItemCategory category, bool on, PawnContent content)
        {
            for (int i = 0; i < Allow.Length && i < content.Items.Length; i++)
                if (content.Items[i].category == category)
                    Allow[i] = on;
        }

        /// <summary>
        /// Is every def of this category ticked, none of them, or some? The tri-state a category
        /// row draws — and the answer for a category with no members is <b>none</b>, which is why
        /// four of the six rows read the same on day one and why the tree is deferred.
        /// </summary>
        public int CategoryState(ItemCategory category, PawnContent content)
        {
            int on = 0, members = 0;
            for (int i = 0; i < Allow.Length && i < content.Items.Length; i++)
            {
                if (content.Items[i].category != category) continue;
                members++;
                if (Allow[i]) on++;
            }

            if (members == 0 || on == 0) return CategoryOff;
            return on == members ? CategoryOn : CategoryMixed;
        }

        public const int CategoryOff = 0;
        public const int CategoryMixed = 1;
        public const int CategoryOn = 2;

        /// <summary>Apply a <see cref="StoragePreset"/>, which also decides <see cref="AllowUnknown"/>.</summary>
        public void ApplyPreset(int preset)
        {
            bool on = preset == StoragePreset.Everything;
            for (int i = 0; i < Allow.Length; i++) Allow[i] = on;
            // A preset is a statement about kinds of thing, not about the seven defs that happen
            // to exist today, so it is the one edit that moves the unknown-def answer with it.
            AllowUnknown = on;
        }
    }

    /// <summary>
    /// Every storage configuration the colony has, by id.
    ///
    /// <para><b>Ids are slot positions and are never reused.</b> That is the
    /// <c>PlacedEdifice</c> contract and it is here for the same reason: a zone holds one, and a
    /// reused id is a zone that silently adopts a stranger's filter. A record whose last referent
    /// has gone is left as a tombstone; a colony mints tens of these, not thousands.</para>
    ///
    /// <para>Saved and hashed as one walk, so the whole of "what the colony accepts where" is one
    /// section rather than a copy per zone.</para>
    /// </summary>
    public sealed class StorageSettingsTable : ISaveable, IStateHashable
    {
        readonly List<StorageSettings> _records = new List<StorageSettings>();
        readonly PawnContent _content;

        public StorageSettingsTable(PawnContent content) { _content = content; }

        public int Count => _records.Count;

        public StorageSettings this[int id] => _records[id];

        /// <summary>The record, or null when the id names nothing — a corrupt save, never a live world.</summary>
        public StorageSettings? Get(int id) => (uint)id < (uint)_records.Count ? _records[id] : null;

        /// <summary>Mint a record at a preset and return its id.</summary>
        public int Create(int preset)
        {
            var settings = new StorageSettings(StoragePriority.Normal, new bool[_content.Items.Length], false);
            settings.ApplyPreset(preset);
            _records.Add(settings);
            return _records.Count - 1;
        }

        /// <summary>Mint a record from values already decided — the load path, and the v6 migration.</summary>
        public int Adopt(int priority, bool[] allow, bool allowUnknown)
        {
            _records.Add(new StorageSettings(priority, allow, allowUnknown));
            return _records.Count - 1;
        }

        public string SaveKey => "odyssey.storage.settings";

        public void Save(SaveWriter writer)
        {
            writer.Write(_records.Count);
            for (int i = 0; i < _records.Count; i++)
            {
                StorageSettings settings = _records[i];
                writer.Write(settings.Priority);
                writer.Write(settings.AllowUnknown);
                writer.Write(settings.Allow.Length);
                for (int a = 0; a < settings.Allow.Length; a++) writer.Write(settings.Allow[a]);
            }
        }

        public void Load(SaveReader reader)
        {
            _records.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int priority = reader.ReadInt();
                bool allowUnknown = reader.ReadBool();
                int saved = reader.ReadInt();

                // The array is built at the *current* def count and filled from what the file has:
                // a commodity added since gets AllowUnknown, and one removed since is simply not
                // read into anything. The same pattern PawnRegistry uses for Needs, Skills and
                // WorkPriorities — save the length, copy what fits — with the one difference that
                // the default is authored rather than nought.
                var allow = new bool[_content.Items.Length];
                for (int a = 0; a < saved; a++)
                {
                    bool value = reader.ReadBool();
                    if (a < allow.Length) allow[a] = value;
                }

                for (int a = saved; a < allow.Length; a++) allow[a] = allowUnknown;
                _records.Add(new StorageSettings(priority, allow, allowUnknown));
            }
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_records.Count);
            for (int i = 0; i < _records.Count; i++)
            {
                StorageSettings settings = _records[i];
                hash.Add(settings.Priority);
                hash.Add(settings.AllowUnknown);
                hash.Add(settings.Allow.Length);
                for (int a = 0; a < settings.Allow.Length; a++) hash.Add(settings.Allow[a]);
            }
        }
    }
}
