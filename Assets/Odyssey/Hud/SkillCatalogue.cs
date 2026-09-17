#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The skills a colonist's record lists, in the order it lists them.
    ///
    /// <para><b>This is the design's thirteen, not the simulation's three</b> (owner, 2026-09-17).
    /// The list in <c>docs/design/icon-keys.csv</c> is canon; it already has registry names, wiki
    /// entries and icon-map rows, and it is the list the game is being built towards. What the
    /// simulation can actually train today is three of them, so the other rows draw as
    /// unavailable with the reason beside them — which is the idiom this interface already uses
    /// for a tab, a command or a panel that does not exist yet, and is better than a tab that
    /// hides the shape of the game until the last system lands.</para>
    ///
    /// <para><b>Where the simulation's three go.</b> <c>Skill_Mining</c> is <c>ui.skill.mining</c>
    /// and needs no argument. <c>Skill_Cutting</c> is plant work — the canon work type
    /// <c>ui.work.cutting</c> is "cut plants and clear growth" — and the only plant skill in the
    /// canon list is growing, so felling trains <c>ui.skill.growing</c>. <c>Skill_Hauling</c> has
    /// no canon skill at all, deliberately: the design makes hauling a work type and not a skill,
    /// which is also the reference's answer. <b>It is therefore not listed here, and the open
    /// item is on the simulation's side</b> — see <c>docs/design/15-skills.md</c> §1.</para>
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

            /// <summary>The three names this skill's numbers arrive under.</summary>
            public readonly AspectKey Level;
            public readonly AspectKey Passion;
            public readonly AspectKey Experience;

            public Entry(string key, string skill, string reason, string note = "")
            {
                Key = key;
                Skill = skill;
                Reason = reason;
                Note = note;
                Level = skill.Length == 0 ? default : AspectKey.Of(Prefix + skill + ".level");
                Passion = skill.Length == 0 ? default : AspectKey.Of(Prefix + skill + ".passion");
                Experience = skill.Length == 0 ? default : AspectKey.Of(Prefix + skill + ".experience");
            }

            public bool Live => Skill.Length != 0;
        }

        /// <summary>
        /// The thirteen, in <c>icon-keys.csv</c> order — which is roughly the order the systems
        /// are planned in rather than alphabetical, so the live ones cluster at the top as the
        /// game fills out.
        /// </summary>
        public static readonly Entry[] All =
        {
            new Entry("ui.skill.construction", NotSimulated, "nothing is built yet"),
            new Entry("ui.skill.mining", "mining", string.Empty),
            new Entry("ui.skill.salvage", NotSimulated, "salvage is hauled, not stripped"),
            new Entry("ui.skill.cooking", NotSimulated, "meals are found, not made"),
            new Entry("ui.skill.growing", "cutting", string.Empty,
                      "trained by felling, which is plant work"),
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
