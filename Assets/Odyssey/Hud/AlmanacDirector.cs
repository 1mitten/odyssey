#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// Session director for the Almanac in-game wiki / reference browser.
    ///
    /// <para>Holds panel open state, active category and entry navigation, history back/forward
    /// stacks, and selection resolution for opening directly from the inspect pane's info button.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): all navigation logic, history stacks, and
    /// selection resolver mappings run and test in the fast tier.</para>
    /// </summary>
    public sealed class AlmanacDirector
    {
        public const string PanelKey = "ui.tab.almanac";

        /// <summary>Every key the panel puts on screen that is its own, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys = { PanelKey };

        readonly List<(string Category, string Entry)> _backStack = new();
        readonly List<(string Category, string Entry)> _forwardStack = new();

        public bool Open { get; private set; }
        public string CurrentCategory { get; private set; } = "Terrain";
        public string CurrentEntry { get; private set; } = "Grass";

        public bool CanGoBack => _backStack.Count > 0;
        public bool CanGoForward => _forwardStack.Count > 0;

        /// <summary>Raised when the panel opens or closes.</summary>
        public event Action? Changed;

        /// <summary>Raised when active entry or category changes.</summary>
        public event Action? Navigated;

        public void Toggle() => SetOpen(!Open);

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            Changed?.Invoke();
        }

        public void OpenTo(string category, string entry)
        {
            NavigateTo(category, entry, addToHistory: true);
            SetOpen(true);
        }

        public void SelectCategory(string category)
        {
            AlmanacCategory? cat = AlmanacCatalogue.GetCategory(category);
            if (cat == null) return;
            string firstEntry = cat.Entries.Count > 0 ? cat.Entries[0].Name : CurrentEntry;
            NavigateTo(cat.Name, firstEntry, addToHistory: true);
        }

        public void SelectEntry(string category, string entry)
        {
            NavigateTo(category, entry, addToHistory: true);
        }

        public void NavigateTo(string category, string entry, bool addToHistory = true)
        {
            AlmanacCategory? cat = AlmanacCatalogue.GetCategory(category);
            if (cat == null) return;

            AlmanacEntry? ent = AlmanacCatalogue.GetEntry(cat.Name, entry);
            if (ent == null && cat.Entries.Count > 0) ent = cat.Entries[0];
            if (ent == null) return;

            if (CurrentCategory == cat.Name && CurrentEntry == ent.Name) return;

            if (addToHistory)
            {
                _backStack.Add((CurrentCategory, CurrentEntry));
                _forwardStack.Clear();
            }

            CurrentCategory = cat.Name;
            CurrentEntry = ent.Name;
            Navigated?.Invoke();
        }

        public bool GoBack()
        {
            if (_backStack.Count == 0) return false;
            int lastIndex = _backStack.Count - 1;
            (string prevCat, string prevEnt) = _backStack[lastIndex];
            _backStack.RemoveAt(lastIndex);

            _forwardStack.Add((CurrentCategory, CurrentEntry));
            CurrentCategory = prevCat;
            CurrentEntry = prevEnt;
            Navigated?.Invoke();
            return true;
        }

        public bool GoForward()
        {
            if (_forwardStack.Count == 0) return false;
            int lastIndex = _forwardStack.Count - 1;
            (string nextCat, string nextEnt) = _forwardStack[lastIndex];
            _forwardStack.RemoveAt(lastIndex);

            _backStack.Add((CurrentCategory, CurrentEntry));
            CurrentCategory = nextCat;
            CurrentEntry = nextEnt;
            Navigated?.Invoke();
            return true;
        }

        /// <summary>
        /// The entry the inspect pane's info button opens: the one keyed by what the pane is
        /// showing — the item's, the tile's, the animal's or the person's registry key, the same
        /// key the pane draws its avatar from. Until 2026-09-26 this guessed from the title
        /// ("Conifer" was a pine, anything unknown was grass or the ration pack); a key either has
        /// an entry or it has none, and <c>AlmanacCatalogueTests</c> holds every key the pane can
        /// show to one.
        /// </summary>
        public static (string Category, string Entry)? ResolveSelection(InspectModel inspect)
        {
            string key;
            switch (inspect.Subject)
            {
                case InspectSubject.Item:
                    key = inspect.ItemIconKey;
                    break;
                case InspectSubject.Cell:
                    key = inspect.CellIconKey;
                    break;
                case InspectSubject.Corpse:
                    key = inspect.CorpseKindKey;
                    break;
                case InspectSubject.Colonist:
                    if (inspect.IsAnimal || inspect.IsHostile)
                    {
                        key = inspect.KindIconKey;
                        break;
                    }
                    // A colonist's page, or the page for the tab she is on.
                    string tab = inspect.ActiveTabName;
                    key = string.Equals(tab, "Skills", StringComparison.OrdinalIgnoreCase) ? "ui.skill.construction"
                        : string.Equals(tab, "Needs", StringComparison.OrdinalIgnoreCase) ? "ui.need.food"
                        : string.Equals(tab, "Health", StringComparison.OrdinalIgnoreCase) ? "ui.combat.health"
                        : PawnKindLabels.Colonist;
                    break;
                default:
                    return null;
            }

            AlmanacEntry? entry = AlmanacCatalogue.ForKey(key);
            return entry == null ? null : (entry.CategoryName, entry.Name);
        }

        public bool OpenForSelection(InspectModel inspect)
        {
            var target = ResolveSelection(inspect);
            if (target == null) return false;
            OpenTo(target.Value.Category, target.Value.Entry);
            return true;
        }
    }
}
