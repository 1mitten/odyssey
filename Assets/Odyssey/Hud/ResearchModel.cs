#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Odyssey.Hud
{
    /// <summary>One row of the category rail.</summary>
    public readonly struct ResearchCategoryRow
    {
        public readonly string Key;
        public readonly int Done;
        public readonly int Total;
        public readonly bool Selected;

        public ResearchCategoryRow(string key, int done, int total, bool selected)
        {
            Key = key;
            Done = done;
            Total = total;
            Selected = selected;
        }
    }

    /// <summary>One row of the project table.</summary>
    public readonly struct ResearchRow
    {
        public readonly ResearchProject Project;
        public readonly ResearchStatus Status;
        public readonly bool Selected;

        public ResearchRow(ResearchProject project, ResearchStatus status, bool selected)
        {
            Project = project;
            Status = status;
            Selected = selected;
        }
    }

    /// <summary>What the detail pane's secondary button does, if it is there at all.</summary>
    public enum ResearchSecondary
    {
        None,
        Queue,
        Unqueue,
        Pause,
    }

    /// <summary>
    /// The Research tab's content: which category and project are selected, the table's sorted
    /// page, and the words the detail pane and the Now strip put on screen (design 34).
    ///
    /// <para>Everything a test would want to ask about the tab is answered here, in the fast
    /// tier, and the shell only paints it. Selection is view state: kept for the session, so the
    /// tab reopens where the player left it, and never saved.</para>
    /// </summary>
    public sealed class ResearchModel
    {
        readonly List<ResearchCategoryRow> _categories = new List<ResearchCategoryRow>();
        readonly List<ResearchRow> _rows = new List<ResearchRow>();
        readonly List<ResearchProject> _sorted = new List<ResearchProject>();

        public ResearchModel()
        {
            Category = ResearchCatalogue.Categories.Count > 0 ? ResearchCatalogue.Categories[0] : string.Empty;
        }

        /// <summary>The selected category. Exactly one, always.</summary>
        public string Category { get; private set; }

        /// <summary>The selected project, or null only when the category holds none.</summary>
        public string? Project { get; private set; }

        public int Page { get; private set; }
        public int PageCount { get; private set; } = 1;

        /// <summary>True when the category holds more projects than the body fits, so the foot shows a pager.</summary>
        public bool Paged => PageCount > 1;

        public IReadOnlyList<ResearchCategoryRow> Categories => _categories;

        /// <summary>The table's rows on the current page.</summary>
        public IReadOnlyList<ResearchRow> Rows => _rows;

        public void SelectCategory(string key)
        {
            if (Category == key) return;
            Category = key;
            Project = null;
            Page = 0;
        }

        public void SelectProject(string key) => Project = key;

        public void SetPage(int page) => Page = page;

        /// <summary>
        /// The table's order: researching, then available, then done, then locked; within each
        /// group the cheapest first; and the catalogue's order last, so equal costs never swap.
        /// </summary>
        public static List<ResearchProject> Sorted(string category, ResearchDirector research)
        {
            var list = new List<(ResearchProject Project, int Status, int Index)>();
            for (int i = 0; i < ResearchCatalogue.Projects.Count; i++)
            {
                ResearchProject project = ResearchCatalogue.Projects[i];
                if (project.CategoryKey == category)
                    list.Add((project, (int)research.StatusOf(project), i));
            }
            list.Sort((a, b) =>
            {
                int by = a.Status.CompareTo(b.Status);
                if (by != 0) return by;
                by = a.Project.Cost.CompareTo(b.Project.Cost);
                return by != 0 ? by : a.Index.CompareTo(b.Index);
            });
            var sorted = new List<ResearchProject>(list.Count);
            foreach (var entry in list) sorted.Add(entry.Project);
            return sorted;
        }

        /// <summary>Rows per page for a category of <paramref name="count"/> projects.</summary>
        public static int PageSize(int count) =>
            count > ResearchLayout.RowsPerPage ? ResearchLayout.RowsPerPagedPage : ResearchLayout.RowsPerPage;

        public void Refresh(ResearchDirector research)
        {
            _categories.Clear();
            foreach (string key in ResearchCatalogue.Categories)
            {
                int done = 0, total = 0;
                foreach (ResearchProject project in ResearchCatalogue.Projects)
                {
                    if (project.CategoryKey != key) continue;
                    total++;
                    if (research.IsDone(project.Key)) done++;
                }
                _categories.Add(new ResearchCategoryRow(key, done, total, key == Category));
            }

            _sorted.Clear();
            _sorted.AddRange(Sorted(Category, research));

            // One project is always selected: the first row when nothing in this category is.
            int at = _sorted.FindIndex(p => p.Key == Project);
            if (at < 0)
            {
                Project = _sorted.Count > 0 ? _sorted[0].Key : null;
                at = _sorted.Count > 0 ? 0 : -1;
                Page = 0;
            }

            int size = PageSize(_sorted.Count);
            PageCount = Math.Max(1, (_sorted.Count + size - 1) / size);
            Page = Math.Max(0, Math.Min(Page, PageCount - 1));

            _rows.Clear();
            int first = Page * size;
            for (int i = first; i < _sorted.Count && i < first + size; i++)
                _rows.Add(new ResearchRow(_sorted[i], research.StatusOf(_sorted[i]), _sorted[i].Key == Project));
        }

        /// <summary>
        /// Keep the selected project on screen after the order changes under it: pressing
        /// Research moves a row to the top, and the page follows it there.
        /// </summary>
        public void FollowSelection(ResearchDirector research)
        {
            List<ResearchProject> sorted = Sorted(Category, research);
            int at = sorted.FindIndex(p => p.Key == Project);
            if (at >= 0) Page = at / PageSize(sorted.Count);
        }

        // ------------------------------------------------------------------ words

        /// <summary>The status tag in the table: the status word, lower case.</summary>
        public static string Tag(ResearchStatus status) =>
            Registry.Label(ResearchDirector.KeyOf(status)).ToLowerInvariant();

        public static string Cost(ResearchProject project) =>
            project.Cost.ToString(CultureInfo.InvariantCulture);

        /// <summary>"Power, cost 500".</summary>
        public static string Meta(ResearchProject project) =>
            Registry.Label(ResearchDirector.MetaKey)
                .Replace("{category}", Registry.Label(project.CategoryKey))
                .Replace("{cost}", Cost(project));

        /// <summary>"Needs Generators first." for a locked project, or empty.</summary>
        public static string LockedLine(ResearchProject project, ResearchDirector research)
        {
            if (research.StatusOf(project) != ResearchStatus.Locked) return string.Empty;
            string? unmet = research.FirstUnmet(project);
            return unmet == null
                ? string.Empty
                : Registry.Label(ResearchDirector.NeedsFirstKey).Replace("{project}", Registry.Label(unmet));
        }

        public static ResearchSecondary SecondaryOf(ResearchProject project, ResearchDirector research) =>
            research.StatusOf(project) switch
            {
                ResearchStatus.Available => research.IsQueued(project.Key)
                    ? ResearchSecondary.Unqueue
                    : ResearchSecondary.Queue,
                ResearchStatus.Researching => ResearchSecondary.Pause,
                _ => ResearchSecondary.None,
            };

        public static string SecondaryLabel(ResearchSecondary secondary) => secondary switch
        {
            ResearchSecondary.Queue => Registry.Label(ResearchDirector.QueueKey),
            ResearchSecondary.Unqueue => Registry.Label(ResearchDirector.UnqueueKey),
            ResearchSecondary.Pause => Registry.Label(ResearchDirector.PauseKey),
            _ => string.Empty,
        };

        /// <summary>The primary button's word: the verb for an available project, the state for the rest.</summary>
        public static string PrimaryLabel(ResearchStatus status) =>
            status == ResearchStatus.Available
                ? Registry.Label(ResearchDirector.ResearchKey)
                : Registry.Label(ResearchDirector.KeyOf(status));

        /// <summary>Only an available project's primary button does anything.</summary>
        public static bool PrimaryPressable(ResearchStatus status) => status == ResearchStatus.Available;

        /// <summary>"then Batteries, Solar arrays", or empty with nothing queued.</summary>
        public static string ThenLine(ResearchDirector research)
        {
            if (research.Current == null || research.Queue.Count == 0) return string.Empty;
            var names = new List<string>(research.Queue.Count);
            foreach (string key in research.Queue) names.Add(Registry.Label(key));
            return Registry.Label(ResearchDirector.ThenKey).Replace("{list}", string.Join(", ", names));
        }

        public static string Percent(int percent) =>
            percent.ToString(CultureInfo.InvariantCulture) + "%";
    }
}
