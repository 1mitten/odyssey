#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>What came down, as far as a sound is concerned (design 57 §9).</summary>
    public enum Demolition : byte
    {
        /// <summary>Something built of wood, broken in a fight or taken apart.</summary>
        Wood = 1,

        /// <summary>A mined face, collapsed.</summary>
        Rock = 2,
    }

    /// <summary>One thing that came down this frame, and where.</summary>
    public readonly struct Demolished
    {
        public readonly int CellIndex;
        public readonly Demolition Kind;

        public Demolished(int cellIndex, Demolition kind)
        {
            CellIndex = cellIndex;
            Kind = kind;
        }
    }

    /// <summary>
    /// What a cell holds, as the drawn world has it: the questions <see cref="DemolitionWatch"/>
    /// asks of the render mirror, behind an interface so the fast tier can answer them.
    /// </summary>
    public interface IDemolitionCells
    {
        bool IsSolid(int cellIndex);
        ushort EdificeDef(int cellIndex);
        ushort EdificeStuff(int cellIndex);
        ushort Floor(int cellIndex);
        ushort FloorStuff(int cellIndex);
    }

    /// <summary>
    /// Hears things come down (design 57 §9; owner, 2026-09-26: wood <i>"on breaking (or
    /// deconstruct)"</i>, and rock <i>"when the rock has collapsed"</i>).
    ///
    /// <para><b>Nothing announces it, so it is watched for.</b> The simulation takes the thing away
    /// on the tick the work finishes and says nothing. What the snapshot does say is which cells
    /// have work on them — a mining order, a deconstruct order, a struck building's hit points — so
    /// every such cell is tracked, and <b>what stood in it is written down the first time it is
    /// seen</b>. When the cell stops being listed it is watched for up to
    /// <see cref="WatchFrames"/> frames: if the mirror then says the rock is no longer solid, or the
    /// building or floor written down is gone, it came down. A cancelled order, a repaired wall, a
    /// cut left part-done all leave the cell as it was, and the watch lets go in silence. The wait
    /// is there because the order can leave the snapshot a publish before the cell's new state
    /// reaches the mirror — the same reason the break's watch has one (§7).</para>
    ///
    /// <para><b>Broken in one blow.</b> A building that goes from whole to nothing in a single blow
    /// was never struck before, so it is never tracked. For that the combat log's
    /// <see cref="CombatEventKind.Demolished"/> names its anchor cell, and the mirror's note of
    /// what left it (<see cref="NoteRemoved"/>) says what it was made of; whichever arrives first
    /// waits for the other. A cell heard is not heard again for <see cref="HeardFrames"/>, so a
    /// wall struck before and then broken — tracked <i>and</i> reported — is one sound.</para>
    ///
    /// <para><b>What makes a sound.</b> Rock under a mining order, whatever the rock. A building or
    /// a floor made of <see cref="WoodStuff"/>, however it went. Anything else built — stone,
    /// concrete, steel — comes down silent until it is given a sound of its own; that is one line
    /// in <see cref="SoundFor"/>.</para>
    ///
    /// <para><b>Scales with</b> the cells that have work on them, which are sparse, and allocates
    /// nothing once the busiest frame has been seen.</para>
    /// </summary>
    public sealed class DemolitionWatch
    {
        /// <summary>How many frames a cell that stopped being listed is watched for coming down.</summary>
        public const int WatchFrames = 45;

        /// <summary><c>DesignationKind.Mine</c> and <c>Deconstruct</c>, restated as the order view
        /// carries them (as <see cref="CrackModel.MineOrderKind"/> does).</summary>
        public const byte MineOrderKind = 1;
        public const byte DeconstructOrderKind = 2;

        [System.Flags]
        enum Why : byte
        {
            Mined = 1,
            Taken = 2,
            Struck = 4,
        }

        sealed class Tracked
        {
            public Why Why;
            public bool Seen;
            public int FramesLeft = -1;      // -1 while still listed
            public ushort Def, Stuff, Floor, FloorStuff;
        }

        readonly Dictionary<int, Tracked> _cells = new Dictionary<int, Tracked>();
        readonly List<int> _done = new List<int>();

        // Broken in a fight (the combat log's Demolished, by its anchor cell) waiting for the
        // mirror's note of what it was made of, and those notes waiting for their event: frames left.
        readonly Dictionary<int, int> _fought = new Dictionary<int, int>();
        readonly Dictionary<int, (ushort stuff, int framesLeft)> _removed = new Dictionary<int, (ushort, int)>();

        // Cells already heard, for a second: a wall struck before and then broken is both a
        // tracked cell and a Demolished event, and must be heard once.
        readonly Dictionary<int, int> _heard = new Dictionary<int, int>();

        int _lastCombatEvent;
        bool _armed;

        /// <summary>How many frames a cell just heard is not heard again (about a second).</summary>
        public const int HeardFrames = 60;

        public DemolitionWatch(ushort woodStuff) => WoodStuff = woodStuff;

        /// <summary>The stuff value wood is built of (<c>NaturalContent.StuffWood</c>).</summary>
        public ushort WoodStuff { get; }

        /// <summary>Cells tracked now, listed or watched, and fights waiting on a note. Exposed for tests.</summary>
        public int Tracking => _cells.Count + _fought.Count;

        /// <summary>Forget everything: a new world, or a load.</summary>
        public void Clear()
        {
            _cells.Clear();
            _fought.Clear();
            _removed.Clear();
            _heard.Clear();
            _armed = false;
        }

        /// <summary>
        /// The mirror's note that a building left it, and what it was made of
        /// (<c>WorldRenderModel.DrainRemoved</c>). Given before <see cref="Step"/> each frame. It is
        /// the only record of the stuff of a building broken from whole in one blow, which was
        /// never struck before and so never tracked.
        /// </summary>
        public void NoteRemoved(int cellIndex, ushort stuff) => _removed[cellIndex] = (stuff, WatchFrames);

        /// <summary>
        /// One frame: take note of every cell with work on it, and add to <paramref name="into"/>
        /// (cleared first) everything that came down since the last. Returns the count.
        /// </summary>
        public int Step(WorldSnapshot snapshot, IDemolitionCells cells, List<Demolished> into)
        {
            into.Clear();
            Age(_heard);
            foreach (Tracked tracked in _cells.Values) tracked.Seen = false;

            // Broken in a fight: the combat log names the anchor cell, and the mirror's note says
            // what it was. Armed on the first frame, so a loaded world's old events are not heard.
            System.ReadOnlySpan<CombatEventView> events = snapshot.CombatEvents;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Id <= _lastCombatEvent) continue;
                _lastCombatEvent = events[i].Id;
                if (_armed && events[i].Kind == CombatEventKind.Demolished)
                    _fought[snapshot.Size.Index(events[i].Cell)] = WatchFrames;
            }
            _armed = true;

            _done.Clear();
            foreach (KeyValuePair<int, int> pair in _fought)
            {
                if (_removed.TryGetValue(pair.Key, out (ushort stuff, int framesLeft) note))
                {
                    Hear(pair.Key, SoundFor(note.stuff), into);
                    _removed.Remove(pair.Key);
                    _cells.Remove(pair.Key);
                    _done.Add(pair.Key);
                }
                else if (pair.Value <= 1) _done.Add(pair.Key);
            }
            for (int i = 0; i < _done.Count; i++) _fought.Remove(_done[i]);
            _done.Clear();
            foreach (KeyValuePair<int, int> pair in _fought) _done.Add(pair.Key);
            for (int i = 0; i < _done.Count; i++) _fought[_done[i]]--;
            AgeRemoved();

            System.ReadOnlySpan<OrderView> orders = snapshot.Orders;
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i].Kind == MineOrderKind) Note(orders[i].CellIndex, Why.Mined, cells);
                else if (orders[i].Kind == DeconstructOrderKind) Note(orders[i].CellIndex, Why.Taken, cells);
            }
            System.ReadOnlySpan<EdificeDamageView> struck = snapshot.EdificeDamage;
            for (int i = 0; i < struck.Length; i++) Note(struck[i].CellIndex, Why.Struck, cells);

            _done.Clear();
            foreach (KeyValuePair<int, Tracked> pair in _cells)
            {
                Tracked t = pair.Value;
                if (t.Seen) continue;
                if (t.FramesLeft < 0) t.FramesLeft = WatchFrames;

                if (CameDown(pair.Key, t, cells, out Demolition? kind))
                {
                    Hear(pair.Key, kind, into);
                    _done.Add(pair.Key);
                }
                else if (--t.FramesLeft <= 0)
                {
                    _done.Add(pair.Key);
                }
            }
            for (int i = 0; i < _done.Count; i++) _cells.Remove(_done[i]);
            return into.Count;
        }

        void Hear(int cell, Demolition? kind, List<Demolished> into)
        {
            if (!kind.HasValue || _heard.ContainsKey(cell)) return;
            into.Add(new Demolished(cell, kind.Value));
            _heard[cell] = HeardFrames;
        }

        void Age(Dictionary<int, int> frames)
        {
            _done.Clear();
            foreach (KeyValuePair<int, int> pair in frames) _done.Add(pair.Key);
            for (int i = 0; i < _done.Count; i++)
            {
                int left = frames[_done[i]] - 1;
                if (left <= 0) frames.Remove(_done[i]);
                else frames[_done[i]] = left;
            }
        }

        void AgeRemoved()
        {
            _done.Clear();
            foreach (KeyValuePair<int, (ushort stuff, int framesLeft)> pair in _removed) _done.Add(pair.Key);
            for (int i = 0; i < _done.Count; i++)
            {
                (ushort stuff, int framesLeft) note = _removed[_done[i]];
                if (note.framesLeft <= 1) _removed.Remove(_done[i]);
                else _removed[_done[i]] = (note.stuff, note.framesLeft - 1);
            }
        }

        void Note(int cell, Why why, IDemolitionCells cells)
        {
            if (!_cells.TryGetValue(cell, out Tracked? t))
            {
                // Written down once, while it still stands: the thing the work is on.
                t = new Tracked
                {
                    Def = cells.EdificeDef(cell),
                    Stuff = cells.EdificeStuff(cell),
                    Floor = cells.Floor(cell),
                    FloorStuff = cells.FloorStuff(cell),
                };
                _cells.Add(cell, t);
            }
            t.Why |= why;
            t.Seen = true;
            t.FramesLeft = -1;
        }

        /// <summary>
        /// Whether what the cell was tracked for is gone, and if so what it sounds like — null for
        /// something gone that has no sound. False while it still stands.
        /// </summary>
        bool CameDown(int cell, Tracked t, IDemolitionCells cells, out Demolition? kind)
        {
            kind = null;
            if ((t.Why & Why.Mined) != 0)
            {
                if (cells.IsSolid(cell)) return false;
                kind = Demolition.Rock;
                return true;
            }
            if (t.Def != 0)
            {
                if (cells.EdificeDef(cell) == t.Def) return false;
                kind = SoundFor(t.Stuff);
                return true;
            }
            if (t.Floor != 0)
            {
                if (cells.Floor(cell) == t.Floor) return false;
                kind = SoundFor(t.FloorStuff);
                return true;
            }
            return false;
        }

        /// <summary>What a built thing of this stuff sounds like coming down; null for silence.</summary>
        Demolition? SoundFor(ushort stuff) => stuff == WoodStuff ? Demolition.Wood : (Demolition?)null;
    }
}
