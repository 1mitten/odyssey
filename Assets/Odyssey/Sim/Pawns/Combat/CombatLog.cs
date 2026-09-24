#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The telling of a fight (design 33 §3, §5): a short ring of <see cref="CombatEventView"/>s
    /// with monotonic ids, whose tail every publish carries. Presentation draws the floating
    /// numbers, the swing sounds and the thud of a fall from it; lane B reads it and lane A writes
    /// it, through <see cref="Report"/> and nothing else.
    ///
    /// <para><b>Not state.</b> Not saved, not hashed and read back by nothing in the simulation —
    /// the ledger's publishing without the ledger. So a world's ids start again at 1 when it is
    /// built, which a load does; a reader resets its watermark when the world changes (see
    /// <see cref="CombatEventView"/>).</para>
    ///
    /// <para><b>Scales with nothing.</b> A report is one write into a fixed ring; a publish walks at
    /// most <see cref="CombatEventView.PublishedTail"/> rows whatever the fight.</para>
    /// </summary>
    public sealed class CombatLog : ISnapshotContributor
    {
        readonly CombatEventView[] _ring = new CombatEventView[CombatEventView.PublishedTail];
        int _next = 1;
        int _count;
        int _head;

        /// <summary>How many events have been reported in this world, ever.</summary>
        public int Reported => _next - 1;

        /// <summary>The id the next report will get.</summary>
        public int NextId => _next;

        /// <summary>
        /// Tell whoever is drawing that something happened. Returns the event's id. The cell is a
        /// whole-world index: the target's cell, or the building's.
        /// </summary>
        public int Report(CombatEventKind kind, PawnId attacker, PawnId target, CellRef cell, int tick,
            int amount = 0, int weapon = -1)
        {
            int id = _next++;
            _ring[_head] = new CombatEventView(id, tick, kind, attacker, target, cell, amount, weapon);
            _head = (_head + 1) % _ring.Length;
            if (_count < _ring.Length) _count++;
            return id;
        }

        /// <summary>The ring, oldest first, which is the order a reader meets ids in ascending.</summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            int start = (_head - _count + _ring.Length) % _ring.Length;
            for (int i = 0; i < _count; i++) writer.AddCombatEvent(_ring[(start + i) % _ring.Length]);
        }
    }
}
