#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The work types the priority grid has columns for, in the order it draws them.
    ///
    /// <para><b>This is the design's twenty-two, not the simulation's four</b>, and the reasoning
    /// is <see cref="SkillCatalogue"/>'s, settled by the owner on 2026-09-17: the list in
    /// <c>docs/design/icon-keys.csv</c> is canon — it already has registry names, wiki entries and
    /// icon-map rows — and a panel that hides eighteen of them *"hides the shape of the game until
    /// the last system lands"*. So all twenty-two are drawn and the eighteen with no work giver
    /// carry their reason instead of their cells.</para>
    ///
    /// <para><b>Not-built-yet is a third state and must never be drawn as incapable.</b> An
    /// incapable cell says *this colonist cannot do this*; an unbuilt column says *nobody can yet,
    /// because it does not exist*. Greying both the same way teaches a player that eighteen of
    /// their columns are a disability. <c>27-work-tab.md</c> §4a has the two treatments.</para>
    ///
    /// <para><b>Hauling has a work type and no skill, deliberately.</b> <c>WorkTypes.xml</c> gives
    /// <c>Work_Haul</c> no <c>rateSkill</c> — hauling has no speed stat here or in the reference —
    /// so <see cref="Entry.Skill"/> is empty for it while <see cref="Entry.Work"/> is not. That is
    /// the one row where the two differ, and it is why they are two fields rather than one: the
    /// cell's border reads the skill and its priority reads the work type, and hauling has the
    /// second without the first.</para>
    /// </summary>
    public static class WorkCatalogue
    {
        /// <summary>Nothing in the simulation runs this work type yet.</summary>
        public const string NotSimulated = "";

        /// <summary>No skill governs this work type. Hauling, and only hauling.</summary>
        public const string NoSkill = "";

        /// <summary>The prefix a work type's per-pawn numbers are published under.</summary>
        const string Prefix = "odyssey.pawn.work.";

        /// <summary>The prefix a skill's numbers are published under. Shared with
        /// <see cref="SkillCatalogue"/> by value and not by reference, for the reason given
        /// there: this assembly cannot see <c>Odyssey.Sim</c> at all.</summary>
        const string SkillPrefix = "odyssey.pawn.skill.";

        public readonly struct Entry
        {
            /// <summary>The registry key, which is also the icon key and the column's tooltip.</summary>
            public readonly string Key;

            /// <summary>
            /// The simulation's own name for the work type, or <see cref="NotSimulated"/>. The
            /// aspect keys below are minted from it.
            /// </summary>
            public readonly string Work;

            /// <summary>
            /// The simulation's name for the skill whose level draws this column's cell borders,
            /// or <see cref="NoSkill"/>.
            /// </summary>
            public readonly string Skill;

            /// <summary>Why the column is not live, shown in its header tooltip. Empty when it is.</summary>
            public readonly string Reason;

            /// <summary>
            /// The <see cref="WorkHandle"/> the <c>SetWorkPriority</c> intent carries, or
            /// <see cref="WorkHandle.None"/> for a column the simulation does not run. The one
            /// number about a work type that crosses the seam as a number rather than a name, and
            /// <c>Catalogue.cs</c> says why.
            /// </summary>
            public readonly int Handle;

            /// <summary>This colonist's priority for this work type, 0 to 4.</summary>
            public readonly AspectKey Priority;

            /// <summary>Non-zero when this colonist can do this work at all.</summary>
            public readonly AspectKey Capable;

            /// <summary>The level that picks the border band, or <c>default</c> when there is no skill.</summary>
            public readonly AspectKey Level;

            /// <summary>The passion that picks the flames, or <c>default</c> when there is no skill.</summary>
            public readonly AspectKey Passion;

            public Entry(string key, string work, string skill, string reason,
                int handle = WorkHandle.None)
            {
                Key = key;
                Work = work;
                Skill = skill;
                Reason = reason;
                Handle = handle;
                Priority = work.Length == 0 ? default : AspectKey.Of(Prefix + work + ".priority");
                Capable = work.Length == 0 ? default : AspectKey.Of(Prefix + work + ".capable");
                Level = skill.Length == 0 ? default : AspectKey.Of(SkillPrefix + skill + ".level");
                Passion = skill.Length == 0 ? default : AspectKey.Of(SkillPrefix + skill + ".passion");
            }

            /// <summary>The simulation runs this work type today.</summary>
            public bool Live => Work.Length != 0;

            /// <summary>A level and a passion exist for this column.</summary>
            public bool HasSkill => Skill.Length != 0;

            /// <summary>The name the column header draws, out of the naming registry.</summary>
            public string Label => Registry.Label(Key);
        }

        /// <summary>
        /// The columns, left to right. The order is urgency — firefighting first because a colony
        /// that finishes the wall while the kitchen burns is a colony that loses both — and it is
        /// the order <c>icon-keys.csv</c> already lists them in, which is where a reordering
        /// belongs rather than here.
        /// </summary>
        public static readonly IReadOnlyList<Entry> All = new[]
        {
            new Entry("ui.work.firefighting", NotSimulated, NoSkill, "firefighting arrives with fire (M5)"),
            new Entry("ui.work.patient",      NotSimulated, NoSkill, "treatment arrives with health (M6)"),
            new Entry("ui.work.bedrest",      NotSimulated, NoSkill, "treatment arrives with health (M6)"),
            // Live from health's H3 (design 43 §5): Work_Doctor, whose emergency giver tends the hurt,
            // paid at the Medicine skill's tend speed.
            new Entry("ui.work.doctor",       "doctor",       "medicine",     "", WorkHandle.Doctor),
            new Entry("ui.work.warden",       NotSimulated, NoSkill, "prisoners arrive with factions (M7)"),
            new Entry("ui.work.handling",     NotSimulated, NoSkill, "animals arrive with M5"),
            new Entry("ui.work.cooking",      NotSimulated, NoSkill, "cooking arrives with M5"),
            new Entry("ui.work.hunting",      NotSimulated, NoSkill, "animals arrive with M5"),
            new Entry("ui.work.construction", "construction", "construction", "", WorkHandle.Construction),
            new Entry("ui.work.growing",      "growing",      "growing",      "", WorkHandle.Growing),
            new Entry("ui.work.mining",       "mining",       "mining",       "", WorkHandle.Mining),
            new Entry("ui.work.salvaging",    NotSimulated, NoSkill, "salvaging arrives with M5"),
            new Entry("ui.work.cutting",      "cutting",      "cutting",      "", WorkHandle.Cutting),

            // The one row where a work type has no skill. Not an omission: see the class remarks.
            new Entry("ui.work.hauling",      "haul",         NoSkill,        "", WorkHandle.Haul),

            new Entry("ui.work.cleaning",     NotSimulated, NoSkill, "filth arrives with M5"),
            new Entry("ui.work.research",     NotSimulated, NoSkill, "research arrives with M7"),
            new Entry("ui.work.crafting",     NotSimulated, NoSkill, "benches arrive with M5"),
            new Entry("ui.work.tailoring",    NotSimulated, NoSkill, "apparel arrives with M6"),
            new Entry("ui.work.fabrication",  NotSimulated, NoSkill, "advanced production arrives with M8"),
            new Entry("ui.work.art",          NotSimulated, NoSkill, "decoration arrives with M6"),
            new Entry("ui.work.operating",    NotSimulated, NoSkill, "powered machinery arrives with M4"),
            // Live from the combat contracts step (design 33 §5): Work_Rescue, whose emergency
            // giver carries the downed to bed from C4.
            new Entry("ui.work.rescue",       "rescue",       NoSkill,        "", WorkHandle.Rescue),
        };

        /// <summary>Every key the grid can draw, for the registry test.</summary>
        public static readonly string[] IconKeys = BuildKeys();

        static string[] BuildKeys()
        {
            var keys = new string[All.Count];
            for (int i = 0; i < All.Count; i++) keys[i] = All[i].Key;
            return keys;
        }

        /// <summary>How many columns the simulation actually runs today. Five, since growing.</summary>
        public static int LiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < All.Count; i++) if (All[i].Live) n++;
                return n;
            }
        }
    }
}
