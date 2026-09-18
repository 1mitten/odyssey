#nullable enable
using System.Collections.Generic;
using System.Text;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The colonists the player has named themselves, by <see cref="PawnId"/>.
    ///
    /// <para><b>An overlay on the pool, not a replacement for it.</b> <see cref="ColonistNames"/>
    /// derives a name from a roll seed and an id, and that stays the answer for everybody nobody
    /// has touched — 244 names, no allocation, the same word every session. This book holds the
    /// exceptions, and it is empty in every colony where the player took the three they were dealt
    /// (owner, 2026-09-18: *"ability to rename your colonist on creation by clicking on the
    /// name"*).</para>
    ///
    /// <para><b>Interface-side, like every other identity here.</b> The simulation has no names and
    /// no opinion about names; a pawn is an id and a roll seed. So a typed name is not simulation
    /// state, cannot move the state hash, and is saved the way the camera pose is — through an
    /// <c>ISaveable</c> that is deliberately not an <c>IStateHashable</c>. Two colonies identical
    /// but for what their people are called still compare equal, which is the property that makes
    /// the ten-day soak mean anything.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): all of it runs in the fast tier.</para>
    /// </summary>
    public sealed class ColonistNameBook
    {
        /// <summary>
        /// The longest name a player may type (owner, 2026-09-18).
        ///
        /// <para>Sixteen, and the number is about the narrowest place a name is drawn rather than
        /// about names: the roster strip and the docked bars are the densest region in the
        /// interface, and a name that elides there is a colonist the player cannot tell apart at a
        /// glance — which is the whole reason for naming one. The colony's own field takes 32
        /// because a colony's name is drawn once, on a screen with room.</para>
        /// </summary>
        public const int MaxLength = 16;

        readonly Dictionary<int, string> _given = new Dictionary<int, string>();

        /// <summary>How many colonists have been named. Zero is the ordinary case and the fast
        /// path: <see cref="ColonistNames.Of(uint, PawnId)"/> does not look here at all.</summary>
        public int Count => _given.Count;

        /// <summary>
        /// What a typed name becomes: trimmed, its runs of blank collapsed to single spaces, its
        /// control characters dropped, and cut to <see cref="MaxLength"/>.
        ///
        /// <para><b>The cut comes last, and that is not an accident.</b> Cutting first and
        /// trimming after would let sixteen characters of which the last three are spaces arrive
        /// as a thirteen-character name, so two players typing the same thing would get different
        /// answers depending on how they got there. Empty is a real answer and means "no name of
        /// their own" — it is what an emptied box says, and it restores the rolled name rather
        /// than producing a colonist with no name at all.</para>
        /// </summary>
        public static string Clean(string? typed)
        {
            if (string.IsNullOrEmpty(typed)) return string.Empty;

            var built = new StringBuilder(typed!.Length);
            bool blank = true; // leading whitespace is trailing whitespace seen from the front
            foreach (char c in typed)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!blank) built.Append(' ');
                    blank = true;
                    continue;
                }
                if (char.IsControl(c)) continue;

                built.Append(c);
                blank = false;
            }

            // One trailing space at most can be here, from the flag above.
            int end = built.Length;
            while (end > 0 && built[end - 1] == ' ') end--;
            if (end > MaxLength) end = MaxLength;

            // And cutting can uncover another one: "Tom             X" cut at sixteen ends in
            // blank, which would be a name with a space on the end of it.
            while (end > 0 && built[end - 1] == ' ') end--;

            return built.ToString(0, end);
        }

        /// <summary>The name this colonist was given, or null when nobody has named them.</summary>
        public string? Given(PawnId id) =>
            _given.TryGetValue(id.Value, out string? name) ? name : null;

        /// <summary>
        /// Name a colonist, or — with a name that <see cref="Clean"/> empties — stop naming them,
        /// which puts the rolled name back.
        /// </summary>
        /// <returns>Whether anything changed, so a caller can skip a redraw on an echoed
        /// keystroke.</returns>
        public bool Rename(PawnId id, string? typed)
        {
            if (!id.IsValid) return false;

            string clean = Clean(typed);
            if (clean.Length == 0) return _given.Remove(id.Value);

            if (_given.TryGetValue(id.Value, out string? held) && held == clean) return false;
            _given[id.Value] = clean;
            return true;
        }

        /// <summary>Forget every name. What starting or loading a colony does first, so that the
        /// last colony's people do not walk into this one.</summary>
        public void Clear() => _given.Clear();

        /// <summary>Every named colonist, for the save section. Order is the dictionary's and
        /// means nothing; the section sorts what it writes.</summary>
        public IEnumerable<KeyValuePair<int, string>> Entries => _given;
    }
}
