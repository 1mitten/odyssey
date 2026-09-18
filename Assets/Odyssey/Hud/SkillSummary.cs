#nullable enable
using System.Collections.Generic;
using System.Text;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a colonist is good at, in one line: <c>Mining 6 · Cutting 3</c>.
    ///
    /// <para><b>The line the candidate card exists for.</b> A player on the setup page is choosing
    /// between three people, and until this existed the only thing on a card that varied by
    /// ability was nothing at all — the two lines were the name and the occupation, and an
    /// occupation is drawn from its own salt (<see cref="ColonistIdentity"/>) and says nothing
    /// about what anybody can do. The skills were on the screen, but only in the detail pane and
    /// only for the one card you had clicked, so comparing three candidates meant clicking each in
    /// turn and remembering.</para>
    ///
    /// <para><b>Here rather than in the presenter</b>, so the fast tier can hold the rules: the
    /// order, the cut, what a colonist with nothing to show reads as. Presentation sets the text
    /// and nothing else.</para>
    /// </summary>
    public static class SkillSummary
    {
        /// <summary>
        /// What a colonist with nothing above zero reads as — the same em dash the traits row uses
        /// for a thing that exists and is empty, rather than a blank, because an absent line and an
        /// empty one look identical and only one of them is a promise. About one candidate in forty
        /// is this one.
        /// </summary>
        public const string Nothing = "—";

        /// <summary>Between two skills on the line. The interface's own separator for facts that
        /// belong to one subject.</summary>
        public const string Separator = " · ";

        /// <summary>
        /// The best <paramref name="count"/> skills this colonist has, highest first.
        ///
        /// <para>Three rules, and each is here to be tested rather than discovered on screen.
        /// <b>Only live skills</b> — the nine nothing simulates are drawn greyed in the detail
        /// pane, which is honest there and would be noise here. <b>Only levels above zero</b>,
        /// because "Mining 0" is not a thing somebody is good at and a card that lists two of them
        /// tells a player less than a card that says so plainly. <b>Ties keep reading order</b>, so
        /// two skills at the same level always come out in the same order and a reroll that
        /// changes nothing looks like it changed nothing.</para>
        /// </summary>
        public static string Line(IReadOnlyList<SkillRow>? rows, int count)
        {
            if (rows == null || count <= 0) return Nothing;

            // Selection sort over at most thirteen rows, taking `count` of them: it keeps reading
            // order on a tie for free, and a comparison sort would have to be told to.
            var taken = new List<int>(count);
            for (int slot = 0; slot < count; slot++)
            {
                int best = -1;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!rows[i].Live || rows[i].Level <= 0) continue;
                    if (taken.Contains(i)) continue;
                    if (best < 0 || rows[i].Level > rows[best].Level) best = i;
                }

                if (best < 0) break;
                taken.Add(best);
            }

            if (taken.Count == 0) return Nothing;

            var line = new StringBuilder();
            for (int i = 0; i < taken.Count; i++)
            {
                if (i > 0) line.Append(Separator);
                line.Append(rows[taken[i]].Name).Append(' ').Append(rows[taken[i]].Level);
            }

            return line.ToString();
        }
    }
}
