#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The skills a colonist's record lists, in the order it lists them.
    ///
    /// <para><b>This is the design's fourteen, not the simulation's four</b> (owner, 2026-09-17).
    /// The list in <c>docs/design/icon-keys.csv</c> is canon; it already has registry names, wiki
    /// entries and icon-map rows, and it is the list the game is being built towards. What the
    /// simulation can actually train today is four of them, so the other rows draw as
    /// unavailable with the reason beside them — which is the idiom this interface already uses
    /// for a tab, a command or a panel that does not exist yet, and is better than a tab that
    /// hides the shape of the game until the last system lands.</para>
    ///
    /// <para><b>Construction and Growing were greyed out here long after they began working</b>
    /// (fixed 2026-09-20, SK1/SK5). Construction has been fully simulated since U26 — three jobs
    /// train it and it drives both a build's speed and its botch roll — and Growing since U47,
    /// where sowing and harvest both train it. Both rows still said "nothing is built yet" and
    /// "nothing is planted yet", so the one screen that tells a player what a colonist can do was
    /// denying two of the four things she actually does. <b>A row's liveness is not documentation,
    /// it is a claim about the simulation, and nothing was checking it</b> — so two tests now
    /// do, one on each side of a seam neither can cross. <c>SkillCatalogueTests</c> pins the live
    /// rows and the aspect names they mint; <c>SkillTests.EverySkillTheSimulationTrainsIsNamedHere</c>
    /// pins <c>SkillIndex.Names</c>. Adding a skill to the simulation fails the second, whose
    /// message sends the author to the first, which is the path the growing branch walked straight
    /// past.</para>
    ///
    /// <para><b>Where the simulation's three go.</b> <c>Skill_Mining</c> is <c>ui.skill.mining</c>
    /// and needs no argument. <c>Skill_Cutting</c> is <c>ui.skill.cutting</c>, <b>Chopping</b>.
    /// <c>Skill_Hauling</c> has no canon skill at all, deliberately: the design makes hauling a
    /// work type and not a skill, which is also the reference's answer. <b>It is therefore not
    /// listed here, and the open item is on the simulation's side</b> — see
    /// <c>docs/design/15-skills.md</c> §1.</para>
    ///
    /// <para><b>Chopping got a row of its own on 2026-09-18, and before that it wore Growing's.</b>
    /// The reasoning was defensible — felling is plant work, and growing was the only plant skill
    /// in the canon list — but it meant a colonist who spent a day with an axe levelled up
    /// <i>Growing</i>, and there was no Chopping anywhere on the screen. The owner met it from the
    /// other end: <i>"I noticed the chopping varied in speed — could we add that to the skills"</i>,
    /// which is a player watching WS2's curve work and going looking for the number behind it.
    /// A skill a player can feel and cannot find is worse than one that does nothing.</para>
    ///
    /// <para><b>The word is the owner's and the family now agrees.</b> The order says Chop, the
    /// activity <c>ui.status.felling</c> says Chopping, and <c>ui.work.cutting</c> said Cutting
    /// until this change brought it along. <b>The key stays <c>cutting</c></b> — it matches the
    /// simulation's <c>SkillIndex.Cutting</c> and a key is a stable identifier, not a label. Three
    /// of these rows already do not spell their own label.</para>
    /// </summary>
    public static class SkillCatalogue
    {
        /// <summary>Nothing in the simulation trains this one yet.</summary>
        public const string NotSimulated = "";

        /// <summary>
        /// The prefix every skill aspect is published under. It is a string and not a shared
        /// constant on purpose: this assembly cannot reference <c>Odyssey.Sim</c> at all, which is
        /// the architectural point of <see cref="PawnAspect"/> — a name is provably enough, and a
        /// constant they both imported would be the shared file the mechanism exists to avoid.
        /// </summary>
        const string Prefix = "odyssey.pawn.skill.";

        public readonly struct Entry
        {
            /// <summary>The registry key, which is also the icon key.</summary>
            public readonly string Key;

            /// <summary>
            /// The simulation's own name for the skill that trains this one, or
            /// <see cref="NotSimulated"/>. The keys below are minted from it.
            /// </summary>
            public readonly string Skill;

            /// <summary>Why it is not live, shown beside the row. Empty when it is.</summary>
            public readonly string Reason;

            /// <summary>What the row says about itself beyond its name, or empty.</summary>
            public readonly string Note;

            /// <summary>The four names this skill's numbers arrive under.</summary>
            public readonly AspectKey Level;
            public readonly AspectKey Passion;
            public readonly AspectKey Experience;

            /// <summary>Per mille towards the next level, which the row's bar is drawn from (SK2).</summary>
            public readonly AspectKey Progress;

            public Entry(string key, string skill, string reason, string note = "")
            {
                Key = key;
                Skill = skill;
                Reason = reason;
                Note = note;
                Level = skill.Length == 0 ? default : AspectKey.Of(Prefix + skill + ".level");
                Passion = skill.Length == 0 ? default : AspectKey.Of(Prefix + skill + ".passion");
                Experience = skill.Length == 0 ? default : AspectKey.Of(Prefix + skill + ".experience");
                Progress = skill.Length == 0 ? default : AspectKey.Of(Prefix + skill + ".progress");
            }

            public bool Live => Skill.Length != 0;
        }

        /// <summary>
        /// The fourteen, in <c>icon-keys.csv</c> order — which is roughly the order the systems
        /// are planned in rather than alphabetical, so the live ones cluster at the top as the
        /// game fills out.
        /// </summary>
        public static readonly Entry[] All =
        {
            new Entry("ui.skill.construction", "construction", string.Empty),
            new Entry("ui.skill.mining", "mining", string.Empty),
            new Entry("ui.skill.salvage", NotSimulated, "salvage is hauled, not stripped"),
            new Entry("ui.skill.cooking", NotSimulated, "meals are found, not made"),
            new Entry("ui.skill.growing", "growing", string.Empty),
            new Entry("ui.skill.cutting", "cutting", string.Empty),
            new Entry("ui.skill.animals", NotSimulated, "no creature simulation"),
            new Entry("ui.skill.crafting", NotSimulated, "no bench work"),
            new Entry("ui.skill.fabrication", NotSimulated, "no production chain"),
            new Entry("ui.skill.medicine", NotSimulated, "a colonist cannot be hurt"),
            new Entry("ui.skill.social", NotSimulated, "no other people"),
            new Entry("ui.skill.shooting", NotSimulated, "no combat"),
            new Entry("ui.skill.melee", NotSimulated, "no combat"),
            new Entry("ui.skill.intellect", NotSimulated, "no research"),
        };

        /// <summary>How many rows a two-column grid of these needs.</summary>
        public static int Rows => (All.Length + 1) / 2;

        static Entry[]? _alphabetical;

        /// <summary>
        /// The fourteen in the order a player reads them: <b>alphabetical by the word on screen</b>
        /// (owner, 2026-09-17).
        ///
        /// <para><b>By the label, not the key or the internal name.</b> A player scanning for
        /// "Construction" is scanning the column they are reading, and <c>ui.skill.cutting</c>
        /// does not spell its own label — it reads <b>Chopping</b>, and the key is the one thing
        /// on the row nobody sees. (It used to be worse: the key was borrowed from Growing until
        /// 2026-09-18, so a colonist who spent a day with an axe levelled up a skill called
        /// Growing.)</para>
        ///
        /// <para><see cref="All"/> keeps its planning order, because that is what the file is a
        /// record of and other readers depend on it. This is the presentation order, and it is
        /// computed once here rather than sorted by each of the two grids that draw it — the whole
        /// point of the shared component is that the Skills tab and a candidate's card cannot come
        /// to disagree about what order skills go in.</para>
        ///
        /// <para>Ordinal, not culture-aware: a colonist's skills must not reorder themselves
        /// because the machine is set to Turkish.</para>
        /// </summary>
        public static IReadOnlyList<Entry> Alphabetical
        {
            get
            {
                if (_alphabetical != null) return _alphabetical;

                var sorted = new Entry[All.Length];
                System.Array.Copy(All, sorted, All.Length);
                System.Array.Sort(sorted, (a, b) =>
                    string.Compare(Registry.Label(a.Key), Registry.Label(b.Key),
                        System.StringComparison.Ordinal));
                return _alphabetical = sorted;
            }
        }

        /// <summary>
        /// The rows of one column of the two-column grid, in reading order: down the left, then
        /// down the right (owner, 2026-09-17).
        ///
        /// <para>Eight then five, so each column is a run that can be scanned. Across-then-down was
        /// refused because a skill's position would then depend on how many come before it in
        /// <i>both</i> columns.</para>
        /// </summary>
        public static IReadOnlyList<Entry> Column(int column)
        {
            IReadOnlyList<Entry> order = Alphabetical;
            int rows = Rows;
            var slice = new List<Entry>(rows);

            for (int row = 0; row < rows; row++)
            {
                int index = column * rows + row;
                if (index < order.Count) slice.Add(order[index]);
            }
            return slice;
        }

        static Entry[]? _reading;

        /// <summary>
        /// The order to <b>lay the rows down in</b> so that a two-column grid <b>reads</b> down the
        /// left column and then down the right.
        ///
        /// <para><b>Not the same as <see cref="Alphabetical"/>, and that is the whole point.</b> The
        /// grid is a wrapping flex row — thirteen items at half width, which the engine flows left
        /// to right and then wraps. Appending A, B, C into that gives A and B side by side, which
        /// is across-then-down: the layout the owner refused. So the sequence is interleaved here,
        /// where it can be tested, rather than by giving the stylesheet a column count it would
        /// then own.</para>
        ///
        /// <para>Thirteen into seven rows leaves the right column one short, so the last row holds
        /// only the left item and the sequence simply runs out — which is why this is computed
        /// rather than written as a table of indices that would be wrong the day a skill is
        /// added.</para>
        /// </summary>
        public static IReadOnlyList<Entry> ReadingOrder
        {
            get
            {
                if (_reading != null) return _reading;

                IReadOnlyList<Entry> order = Alphabetical;
                int rows = Rows;
                var laid = new List<Entry>(order.Count);

                for (int row = 0; row < rows; row++)
                for (int column = 0; column < 2; column++)
                {
                    int index = column * rows + row;
                    if (index < order.Count) laid.Add(order[index]);
                }

                return _reading = laid.ToArray();
            }
        }

        /// <summary>The keys, for the registry test that holds every key this panel can draw.</summary>
        public static string[] IconKeys
        {
            get
            {
                var keys = new string[All.Length];
                for (int i = 0; i < All.Length; i++) keys[i] = All[i].Key;
                return keys;
            }
        }
    }
}
