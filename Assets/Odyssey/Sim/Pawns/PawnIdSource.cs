#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Where a <see cref="PawnRegistry"/> gets the next pawn id (design 64 §4c).
    ///
    /// A board on its own owns its own counter, and that is the default: <see cref="PawnRegistry"/>
    /// makes one, so a colony that never travels allocates exactly the ids it always did. A
    /// campaign installs <b>one</b> source into every board it holds, because a colonist carries
    /// her id from board to board and her name is derived from it (<c>ColonistNames</c>), so two
    /// boards counting from one apiece would hand a second person the same id — and the same name.
    ///
    /// <para>Splitting the range per board was considered and rejected: a site board is rebuilt
    /// every visit and would reuse its range, so a colonist who came home from it could collide
    /// with one born there on the next visit.</para>
    /// </summary>
    public sealed class PawnIdSource
    {
        int _next = 1;

        /// <summary>The id the next <see cref="Next"/> will hand out.</summary>
        public int Peek => _next;

        /// <summary>A fresh id, never handed out before by this source.</summary>
        public int Next() => _next++;

        /// <summary>
        /// Note an id that exists already — a pawn adopted from elsewhere, or restored from a save —
        /// so no later <see cref="Next"/> repeats it.
        /// </summary>
        public void Observe(int id)
        {
            if (id >= _next) _next = id + 1;
        }

        /// <summary>
        /// Set the counter outright, as a load does: the saved value is the truth, whatever this
        /// source had counted to while the world was being built for the load to land on.
        /// </summary>
        public void Reset(int next)
        {
            _next = next < 1 ? 1 : next;
        }
    }
}
