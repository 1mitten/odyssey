#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>Where a project stands. The order is the table's sort order (design 34 §4).</summary>
    public enum ResearchStatus
    {
        Researching = 0,
        Available = 1,
        Done = 2,
        Locked = 3,
    }

    /// <summary>
    /// One research project: its key, its field, its cost, what it needs and what it gives.
    ///
    /// <para>Named by key only. The name and the description are the registry's
    /// (<c>docs/design/icon-keys.csv</c>, namespace <c>ui.research.project</c>), so the wiki and
    /// the detail pane read one copy.</para>
    /// </summary>
    public sealed class ResearchProject
    {
        public string Key { get; }
        public string CategoryKey { get; }
        public int Cost { get; }

        /// <summary>Project keys that must be done before this one can start.</summary>
        public IReadOnlyList<string> Needs { get; }

        /// <summary>Registry keys of what finishing it gives: Build palette tools today.</summary>
        public IReadOnlyList<string> Unlocks { get; }

        public ResearchProject(string key, string categoryKey, int cost, string[] needs, string[] unlocks)
        {
            Key = key;
            CategoryKey = categoryKey;
            Cost = cost;
            Needs = needs;
            Unlocks = unlocks;
        }
    }

    /// <summary>
    /// The research the Research tab shows (design 34).
    ///
    /// <para><b>A placeholder table, and the only copy of it.</b> The owner asked for the
    /// interface first and the mechanism later (2026-09-23: "only include power for now but we'll
    /// create the mechanism later"), so there is no research Def and nothing in the simulation
    /// reads these costs yet. When the Def arrives this table is what it replaces: the Def set
    /// becomes the registry's source, as <c>CLAUDE.md</c> already says of every other named
    /// thing, and the tab reads it through the same four properties.</para>
    ///
    /// <para><b>Only what is in the game</b> (owner, 2026-09-23: "only research for what in this
    /// game"): Electricity, which opens Power lines and the Generator, and the Ladder. Two fields,
    /// four projects. The rail draws one row per category in this list's order, so a third field
    /// is a line here and nothing in the shell.</para>
    /// </summary>
    public static class ResearchCatalogue
    {
        public const string PowerKey = "ui.research.category.power";
        public const string FurnitureKey = "ui.research.category.furniture";

        public const string ElectricityKey = "ui.research.project.electricity";
        public const string PowerLinesKey = "ui.research.project.powerlines";
        public const string GeneratorKey = "ui.research.project.generator";
        public const string LadderKey = "ui.research.project.ladder";

        /// <summary>The fields, in the rail's order.</summary>
        public static readonly IReadOnlyList<string> Categories = new[] { PowerKey, FurnitureKey };

        /// <summary>Every project, in catalogue order (the last tie-break of the table's sort).</summary>
        public static readonly IReadOnlyList<ResearchProject> Projects = new[]
        {
            // The root: it unlocks nothing to build, only the two projects under it.
            new ResearchProject(ElectricityKey, PowerKey, 300,
                Array.Empty<string>(), Array.Empty<string>()),
            // The palette calls the line a conduit (the power branch, PR #173); the project is
            // named as the owner names it, and the unlock reads the palette's own name.
            new ResearchProject(PowerLinesKey, PowerKey, 400,
                new[] { ElectricityKey }, new[] { "ui.arch.tool.conduit" }),
            new ResearchProject(GeneratorKey, PowerKey, 500,
                new[] { ElectricityKey }, new[] { "ui.arch.tool.generator" }),
            new ResearchProject(LadderKey, FurnitureKey, 200,
                Array.Empty<string>(), new[] { "ui.arch.tool.ladder" }),
        };

        /// <summary>
        /// What a colony knows on the first day: nothing. Research unlocks nothing yet, so a
        /// colony can still build a ladder or a line before it is researched; the day the palette
        /// reads this, the starting set is the decision to revisit.
        /// </summary>
        public static readonly IReadOnlyList<string> StartsDone = Array.Empty<string>();

        /// <summary>The project with this key, or null.</summary>
        public static ResearchProject? Find(string? key)
        {
            if (key == null) return null;
            foreach (ResearchProject project in Projects)
                if (project.Key == key) return project;
            return null;
        }

        /// <summary>The projects that name <paramref name="key"/> among their needs, in catalogue order.</summary>
        public static List<ResearchProject> LeadingFrom(string key)
        {
            var list = new List<ResearchProject>();
            foreach (ResearchProject project in Projects)
                foreach (string need in project.Needs)
                    if (need == key) { list.Add(project); break; }
            return list;
        }

        /// <summary>Every key the catalogue puts on screen, for <c>RegistryTests</c>.</summary>
        public static IEnumerable<string> IconKeys()
        {
            foreach (string category in Categories) yield return category;
            foreach (ResearchProject project in Projects)
            {
                yield return project.Key;
                foreach (string unlock in project.Unlocks) yield return unlock;
            }
        }
    }
}
