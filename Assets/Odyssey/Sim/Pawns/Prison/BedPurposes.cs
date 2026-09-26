#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>What every bed is for</b> (design 58 §5b): the one owner of the prison bed.
    ///
    /// <para><b>What is saved is what the player marked</b>, as the head cells of the beds that
    /// were marked, and nothing else — no <c>PlacedEdifice</c> field, because
    /// <c>EdificeSaveSection</c> hashes its records whole and a field there would bump the format
    /// and move every golden for a feature no golden uses. Marking one bed marks every bed in its
    /// room at that instant, so that a wall knocked out later leaves shackle beds rather than
    /// silently turning a prisoner's bed back into a colonist's.</para>
    ///
    /// <para><b>What a bed is for is derived</b>: a prison bed is one marked, or one standing in a
    /// room that holds a marked bed. So a bed built later inside a cell, or a room merged into a
    /// cell, is a prison bed with no further order. A marked bed with no room around it — open,
    /// unroofed, or over <see cref="EnclosureGrid.MaxRoomCells"/> — is a <b>shackle bed</b>: it
    /// still holds a prisoner, who stays on it.</para>
    ///
    /// <para><b>The cell set is a cache</b>, rebuilt only when the enclosure's
    /// <see cref="EnclosureGrid.Generation"/> or this class's <see cref="Version"/> moves, never
    /// per query (<c>docs/bug-patterns.md</c> P12). It is not saved and not hashed.</para>
    /// </summary>
    public sealed class BedPurposes : ISaveable, IStateHashable
    {
        /// <summary>The record layout this build writes. 1: the marked beds' head cells, ascending.</summary>
        public const int Layout = 1;

        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;

        // Ascending head cells, so the save and the hash walk them in one order.
        readonly List<int> _marked = new List<int>();

        readonly HashSet<int> _cellRooms = new HashSet<int>();
        int _cellsForVersion = -1, _cellsForGeneration = -1;

        public BedPurposes(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _edifices = edifices ?? throw new ArgumentNullException(nameof(edifices));
        }

        /// <summary>The rooms, told to this class by the composition root. Null in a fixture with none: every marked bed is then a shackle bed.</summary>
        public EnclosureGrid? Enclosure { get; set; }

        /// <summary>Moves whenever what the player marked changes. Read, with the enclosure's generation, to know when to look again.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// A key that moves whenever the answer to <see cref="PurposeAt"/> can have changed: a mark,
        /// an unmark, a bed going, or the rooms themselves. The owner sweep compares it.
        /// </summary>
        public long StateKey => ((long)Version << 32) | (uint)(Enclosure?.Generation ?? 0);

        /// <summary>The head cells the player marked, ascending.</summary>
        public IReadOnlyList<int> Marked => _marked;

        /// <summary>Whether any bed on the board is marked: nothing else here costs anything while none is.</summary>
        public bool Any => _marked.Count > 0;

        // ---- the answers ---------------------------------------------------------------------

        /// <summary>
        /// What the bed at <paramref name="cell"/> — either of its two cells — is for. A cell with
        /// no bed answers <see cref="BedPurpose.Colony"/>, as it did before prison beds existed.
        /// </summary>
        public BedPurpose PurposeAt(int cell)
        {
            if (_marked.Count == 0) return BedPurpose.Colony;
            int head = BedHeadAt(cell);
            if (head < 0) return BedPurpose.Colony;
            if (_marked.BinarySearch(head) >= 0) return BedPurpose.Prison;
            int room = RoomOf(head);
            return room != 0 && CellRooms().Contains(room) ? BedPurpose.Prison : BedPurpose.Colony;
        }

        /// <summary>A prison bed with no room around it: its prisoner is shackled to it (design 58 §5b).</summary>
        public bool IsShackled(int cell)
        {
            int head = BedHeadAt(cell);
            return head >= 0 && PurposeAt(head) == BedPurpose.Prison && RoomOf(head) == 0;
        }

        /// <summary>Whether this room — an <see cref="EnclosureGrid.RoomAt"/> key — is a cell.</summary>
        public bool IsCell(int room) => room != 0 && _marked.Count > 0 && CellRooms().Contains(room);

        /// <summary>The room the bed's head stands in, or 0 for none.</summary>
        public int RoomOf(int cell) => Enclosure?.RoomAt(cell) ?? 0;

        /// <summary>The head cell of the bed at <paramref name="cell"/>, or -1 where no built bed stands.</summary>
        public int BedHeadAt(int cell)
        {
            if ((uint)cell >= (uint)_grid.Edifice.Length) return -1;
            int handle = _grid.Edifice[cell];
            if (handle < 0 || handle >= _edifices.Count) return -1;
            PlacedEdifice placed = _edifices[handle];
            if (placed.Def != CoreContent.EdificeBed || placed.Removed || !placed.Built) return -1;
            return placed.CellIndex;
        }

        HashSet<int> CellRooms()
        {
            int generation = Enclosure?.Generation ?? 0;
            if (_cellsForVersion == Version && _cellsForGeneration == generation) return _cellRooms;
            _cellRooms.Clear();
            for (int i = 0; i < _marked.Count; i++)
            {
                int room = RoomOf(_marked[i]);
                if (room != 0) _cellRooms.Add(room);
            }
            // Read the generation after the rooms were asked for: asking may solve the enclosure,
            // which moves it, and the set is true of the rooms as they now are.
            _cellsForVersion = Version;
            _cellsForGeneration = Enclosure?.Generation ?? 0;
            return _cellRooms;
        }

        // ---- the order ----------------------------------------------------------------------

        /// <summary>
        /// <c>SetBedPurpose(cell, A = purpose)</c>: mark the bed at the cell for prisoners, and every
        /// bed in its room with it, or unmark it and every marked bed in its room. Refused where no
        /// built bed stands or the purpose is not one; <c>AlreadyInThatState</c> for a no-op.
        /// </summary>
        public IntentRejection SetPurpose(int cell, int purpose)
        {
            if (purpose != (int)BedPurpose.Colony && purpose != (int)BedPurpose.Prison)
                return IntentRejection.NotPermitted;
            int head = BedHeadAt(cell);
            if (head < 0) return IntentRejection.NotPermitted;

            bool prison = purpose == (int)BedPurpose.Prison;
            if ((PurposeAt(head) == BedPurpose.Prison) == prison) return IntentRejection.AlreadyInThatState;

            int room = RoomOf(head);
            if (prison)
            {
                Add(head);
                if (room != 0)
                    for (int i = 0; i < _edifices.Count; i++)
                    {
                        int other = BedHeadAt(_edifices[i].CellIndex);
                        if (other >= 0 && other == _edifices[i].CellIndex && RoomOf(other) == room) Add(other);
                    }
            }
            else
            {
                Remove(head);
                if (room != 0)
                    for (int i = _marked.Count - 1; i >= 0; i--)
                        if (RoomOf(_marked[i]) == room) _marked.RemoveAt(i);
            }
            Version++;
            return IntentRejection.None;
        }

        /// <summary>A bed has come down: whatever it was marked, it is not any more.</summary>
        /// <summary>
        /// A bed has just been raised (review 2026-09-26). If it stands in a cell it is a prison bed
        /// by derivation, and that is written down now: otherwise it would turn back into a colony
        /// bed the moment its cell was opened — a door broken, a wall down, the first bed taken
        /// away — while the bed that was marked became a shackle bed as it should.
        /// </summary>
        public void Raised(int cell)
        {
            int head = BedHeadAt(cell);
            if (head < 0 || _marked.Count == 0 || _marked.BinarySearch(head) >= 0) return;
            if (PurposeAt(head) != BedPurpose.Prison) return;
            Add(head);
            Version++;
        }

        public void Forget(int head)
        {
            if (Remove(head)) Version++;
        }

        void Add(int head)
        {
            int at = _marked.BinarySearch(head);
            if (at < 0) _marked.Insert(~at, head);
        }

        bool Remove(int head)
        {
            int at = _marked.BinarySearch(head);
            if (at < 0) return false;
            _marked.RemoveAt(at);
            return true;
        }

        // ---- save and hash ------------------------------------------------------------------

        public string SaveKey => "odyssey.bedpurpose";

        public void Save(SaveWriter writer)
        {
            writer.Write(Layout);
            writer.Write(_marked.Count);
            for (int i = 0; i < _marked.Count; i++) writer.Write(_marked[i]);
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException(
                    $"The bed purpose section has layout {layout} and this build reads up to {Layout}.");
            _marked.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) Add(reader.ReadInt());
            Version++;
        }

        /// <summary>The marked beds, walked only while there are any: a colony with none hashes as before.</summary>
        public void ContributeTo(ref StateHash hash)
        {
            if (_marked.Count == 0) return;
            hash.Add(_marked.Count);
            for (int i = 0; i < _marked.Count; i++) hash.Add(_marked[i]);
        }
    }
}
