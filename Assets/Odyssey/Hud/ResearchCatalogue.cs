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
    /// <para><b>Only Power.</b> The rail draws one row per category in this list's order, so a
    /// second field is a line here and nothing in the shell.</para>
    /// </summary>
    public static class ResearchCatalogue
    {
        public const string PowerKey = "ui.research.category.power";

        public const string WiringKey = "ui.research.project.wiring";
        public const string GeneratorsKey = "ui.research.project.generators";
        public const string LightingKey = "ui.research.project.lighting";
        public const string BatteriesKey = "ui.research.project.batteries";
        public const string SolarKey = "ui.research.project.solar";

        /// <summary>The fields, in the rail's order.</summary>
        public static readonly IReadOnlyList<string> Categories = new[] { PowerKey };

        /// <summary>Every project, in catalogue order (the last tie-break of the table's sort).</summary>
        public static readonly IReadOnlyList<ResearchProject> Projects = new[]
        {
            new ResearchProject(WiringKey, PowerKey, 300,
                Array.Empty<string>(), new[] { "ui.arch.tool.conduit" }),
            new ResearchProject(GeneratorsKey, PowerKey, 500,
                new[] { WiringKey }, new[] { "ui.arch.tool.generator" }),
            new ResearchProject(LightingKey, PowerKey, 400,
                new[] { WiringKey }, new[] { "ui.arch.tool.lamp" }),
            new ResearchProject(BatteriesKey, PowerKey, 700,
                new[] { GeneratorsKey }, new[] { "ui.arch.tool.battery" }),
            new ResearchProject(SolarKey, PowerKey, 1200,
                new[] { BatteriesKey }, new[] { "ui.arch.tool.solar" }),
        };

        /// <summary>
        /// What a colony knows on the first day. Wiring, because the power branch lets a colony
        /// lay conduit with no research at all, and a tab that called it undiscovered would be
        /// contradicting the Build palette.
        /// </summary>
        public static readonly IReadOnlyList<string> StartsDone = new[] { WiringKey };

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
