#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// Whether the Research tab is open, and — until the mechanism exists — what the colony has
    /// researched (design 34).
    ///
    /// <para><b>The research state lives here only because nothing else holds it yet.</b> The
    /// owner asked for the interface first (2026-09-23), so the project in hand, the queue and
    /// what is done are session state on the interface side: not saved, not hashed, and nothing
    /// in the simulation reads them. Progress never moves on its own — there is no research bench
    /// work to move it — and the debug menu's <i>Finish research</i> is the only way a project
    /// becomes done. When research becomes a simulation system, these members become reads of
    /// its snapshot and presses become intents; the tab's model reads them through
    /// <see cref="StatusOf"/> and <see cref="PercentOf"/> and does not change.</para>
    ///
    /// <para>Session state, like <see cref="WorkDirector"/>: built fresh with every session's
    /// <see cref="HudDirectors"/>. Unity-free (ADR 0003), so all of it runs in the fast tier.</para>
    /// </summary>
    public sealed class ResearchDirector
    {
        /// <summary>The window's name, which is the tab's.</summary>
        public const string PanelKey = "ui.tab.research";

        public const string NowKey = "ui.research.hud.now";
        public const string ThenKey = "ui.research.hud.then";
        public const string IdleKey = "ui.research.hud.idle";
        public const string ProjectKey = "ui.research.hud.project";
        public const string StatusKey = "ui.research.hud.status";
        public const string CostKey = "ui.research.hud.cost";
        public const string MetaKey = "ui.research.hud.meta";
        public const string NeedsKey = "ui.research.hud.needs";
        public const string UnlocksKey = "ui.research.hud.unlocks";
        public const string LeadsToKey = "ui.research.hud.leadsto";
        public const string NoneKey = "ui.research.hud.none";
        public const string NeedsFirstKey = "ui.research.hud.needsfirst";
        public const string ResearchKey = "ui.research.hud.research";
        public const string QueueKey = "ui.research.hud.queue";
        public const string UnqueueKey = "ui.research.hud.unqueue";
        public const string PauseKey = "ui.research.hud.pause";

        public const string ResearchingKey = "ui.research.status.researching";
        public const string AvailableKey = "ui.research.status.available";
        public const string DoneKey = "ui.research.status.done";
        public const string LockedKey = "ui.research.status.locked";

        /// <summary>Every key the panel puts on screen that is its own, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys =
        {
            PanelKey, NowKey, ThenKey, IdleKey, ProjectKey, StatusKey, CostKey, MetaKey, NeedsKey,
            UnlocksKey, LeadsToKey, NoneKey, NeedsFirstKey, ResearchKey, QueueKey, UnqueueKey,
            PauseKey, ResearchingKey, AvailableKey, DoneKey, LockedKey,
        };

        /// <summary>The registry key naming a status: the table's tag (lower-cased) and the primary button's word.</summary>
        public static string KeyOf(ResearchStatus status) => status switch
        {
            ResearchStatus.Researching => ResearchingKey,
            ResearchStatus.Available => AvailableKey,
            ResearchStatus.Done => DoneKey,
            _ => LockedKey,
        };

        readonly HashSet<string> _done = new HashSet<string>();
        readonly Dictionary<string, int> _progress = new Dictionary<string, int>();
        readonly List<string> _queue = new List<string>();

        public ResearchDirector()
        {
            foreach (string key in ResearchCatalogue.StartsDone) _done.Add(key);
        }

        public bool Open { get; private set; }

        /// <summary>Raised after every open or close.</summary>
        public event Action? Changed;

        /// <summary>Raised after anything a project's status, the queue or the progress depends on changes.</summary>
        public event Action? StateChanged;

        public void Toggle() => SetOpen(!Open);

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ the research itself

        /// <summary>The project being researched, or null.</summary>
        public string? Current { get; private set; }

        /// <summary>What starts after <see cref="Current"/>, in order. Never holds the current project.</summary>
        public IReadOnlyList<string> Queue => _queue;

        public bool IsDone(string key) => _done.Contains(key);

        public bool IsQueued(string key) => _queue.Contains(key);

        public ResearchStatus StatusOf(ResearchProject project)
        {
            if (_done.Contains(project.Key)) return ResearchStatus.Done;
            if (Current == project.Key) return ResearchStatus.Researching;
            return FirstUnmet(project) == null ? ResearchStatus.Available : ResearchStatus.Locked;
        }

        /// <summary>The first of a project's needs that is not done, in the order it lists them, or null.</summary>
        public string? FirstUnmet(ResearchProject project)
        {
            foreach (string need in project.Needs)
                if (!_done.Contains(need)) return need;
            return null;
        }

        /// <summary>Research points put into a project so far.</summary>
        public int ProgressOf(string key) => _progress.TryGetValue(key, out int points) ? points : 0;

        /// <summary>Whole per cent, rounded down: 100 only when done.</summary>
        public int PercentOf(ResearchProject project)
        {
            if (_done.Contains(project.Key)) return 100;
            if (project.Cost <= 0) return 0;
            return Math.Min(99, ProgressOf(project.Key) * 100 / project.Cost);
        }

        /// <summary>
        /// Research this project now. Only an available project starts; one already in hand goes
        /// back to available with its progress kept, as a paused one does.
        /// </summary>
        public bool Start(string key)
        {
            ResearchProject? project = ResearchCatalogue.Find(key);
            if (project == null || StatusOf(project) != ResearchStatus.Available) return false;
            Current = key;
            _queue.Remove(key);
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Stop researching, keeping the progress. The queue waits.</summary>
        public bool Pause()
        {
            if (Current == null) return false;
            Current = null;
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Research this project after the one in hand. With nothing in hand it simply starts —
        /// a queue with nothing ahead of it is the project in hand.
        /// </summary>
        public bool Enqueue(string key)
        {
            ResearchProject? project = ResearchCatalogue.Find(key);
            if (project == null || StatusOf(project) != ResearchStatus.Available || _queue.Contains(key))
                return false;
            if (Current == null) return Start(key);
            _queue.Add(key);
            StateChanged?.Invoke();
            return true;
        }

        public bool Unqueue(string key)
        {
            if (!_queue.Remove(key)) return false;
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Put research points into the project in hand, finishing it when they reach its cost.
        /// Nothing calls this yet but the tests: it is the seam the research bench will drive.
        /// </summary>
        public void Advance(int points)
        {
            if (Current == null || points <= 0) return;
            ResearchProject project = ResearchCatalogue.Find(Current)!;
            int total = ProgressOf(Current) + points;
            _progress[Current] = Math.Min(total, project.Cost);
            if (total >= project.Cost) Complete(project);
            else StateChanged?.Invoke();
        }

        /// <summary>The debug menu's <i>Finish research</i>: the project in hand is done at once.</summary>
        public bool FinishCurrent()
        {
            if (Current == null) return false;
            Complete(ResearchCatalogue.Find(Current)!);
            return true;
        }

        void Complete(ResearchProject project)
        {
            _done.Add(project.Key);
            _progress[project.Key] = project.Cost;
            Current = null;

            // The queue's head starts. Everything in the queue was available when it went in and
            // nothing becomes un-done, so the head is always startable.
            if (_queue.Count > 0)
            {
                Current = _queue[0];
                _queue.RemoveAt(0);
            }
            StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// The Research tab's fixed geometry, one owner, from the owner's spec of 2026-09-23
    /// (design 34 §3). Every number here is asserted against the tree the panel builds.
    /// </summary>
    public static class ResearchLayout
    {
        /// <summary>The window's outer width. A UI Toolkit width is a border box, so this includes the 1 px frame.</summary>
        public const int Width = 1100;

        public const int HeaderHeight = 34;
        public const int NowStripHeight = 30;
        public const int BodyHeight = 420;

        /// <summary>The window's outer height, constant in every category: the three bands and the frame.</summary>
        public const int Height = HeaderHeight + NowStripHeight + BodyHeight + 2 * HudTheme.BorderWidth;

        public const int NowBarWidth = 220;
        public const int NowBarHeight = 4;

        public const int RailWidth = 200;
        public const int TableWidth = 460;

        /// <summary>What is left for the detail pane: the window less its frame, the rail and the table.</summary>
        public const int DetailWidth = Width - 2 * HudTheme.BorderWidth - RailWidth - TableWidth;

        public const int StatusColumn = 110;
        public const int CostColumn = 70;

        public const int RowHeight = 30;

        /// <summary>The table body: its heading row and thirteen rows fill the 420.</summary>
        public const int RowsPerPage = (BodyHeight - RowHeight) / RowHeight;

        /// <summary>When a category pages, the foot takes one row's height and the table shows one row fewer.</summary>
        public const int RowsPerPagedPage = RowsPerPage - 1;

        public const int SidePad = 12;
        public const int Gap = 9;
        public const int ItemGap = 12;
        public const int RailPadY = 9;
        public const int RailSquare = 12;
        public const int SelectedRail = 2;

        public const int DetailIcon = 32;
        public const int FactLabelColumn = 80;
        public const int ButtonHeight = 30;
        public const int PagerButton = 22;

        public const int CloseBox = 22;
        public const int CloseMark = 10;

        /// <summary>After the comma in a list of facts: a space's width, since a label trims its own trailing space.</summary>
        public const int WordSpace = 4;
    }
}
