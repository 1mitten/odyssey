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

            AlmanacEntry? ent = AlmanacCatalogue.GetEntry(entry);
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
        /// Attempts to resolve the current selection from <see cref="InspectModel"/> to an Almanac
        /// entry and category.
        /// </summary>
        public static (string Category, string Entry)? ResolveSelection(InspectModel inspect)
        {
            if (inspect.Subject == InspectSubject.None) return null;

            if (inspect.Subject == InspectSubject.Item)
            {
                string title = inspect.Title;
                string iconKey = inspect.ItemIconKey;

                if (title.StartsWith(AlmanacKeys.Wood, StringComparison.OrdinalIgnoreCase)) return ("Materials", AlmanacKeys.Wood);
                if (title.StartsWith(AlmanacKeys.Stone, StringComparison.OrdinalIgnoreCase)) return ("Materials", AlmanacKeys.Stone);
                if (title.StartsWith("Concrete", StringComparison.OrdinalIgnoreCase)) return ("Materials", "Concrete");
                if (title.StartsWith("Steel", StringComparison.OrdinalIgnoreCase)) return ("Materials", "Steel");

                if (title.StartsWith("Ration", StringComparison.OrdinalIgnoreCase) || iconKey == "ui.res.meal")
                    return ("Items", "Ration Pack");
                if (title.StartsWith(AlmanacKeys.Carrots, StringComparison.OrdinalIgnoreCase) || iconKey == "ui.res.carrots")
                    return ("Items", AlmanacKeys.Carrots);
                if (title.StartsWith(AlmanacKeys.Scrap, StringComparison.OrdinalIgnoreCase) ||
                    title.StartsWith(AlmanacKeys.Salvage, StringComparison.OrdinalIgnoreCase) ||
                    iconKey == "ui.res.scrap")
                    return ("Items", AlmanacKeys.Salvage);
                if (title.StartsWith("Iron", StringComparison.OrdinalIgnoreCase) || iconKey == "ui.res.ironore")
                    return ("Items", "Iron Ore");
                if (title.StartsWith(AlmanacKeys.Coal, StringComparison.OrdinalIgnoreCase) || iconKey == "ui.res.coal")
                    return ("Items", AlmanacKeys.Coal);

                return ("Items", "Ration Pack");
            }

            if (inspect.Subject == InspectSubject.Cell)
            {
                string title = inspect.Title;

                if (title.IndexOf(AlmanacKeys.Wall, StringComparison.OrdinalIgnoreCase) >= 0) return ("Structures", AlmanacKeys.Wall);
                if (title.IndexOf(AlmanacKeys.Door, StringComparison.OrdinalIgnoreCase) >= 0) return ("Structures", AlmanacKeys.Door);
                if (title.IndexOf(AlmanacKeys.Ladder, StringComparison.OrdinalIgnoreCase) >= 0) return ("Structures", AlmanacKeys.Ladder);
                if (title.IndexOf(AlmanacKeys.Bed, StringComparison.OrdinalIgnoreCase) >= 0) return ("Structures", AlmanacKeys.Bed);
                if (title.IndexOf("Pillar", StringComparison.OrdinalIgnoreCase) >= 0) return ("Structures", AlmanacKeys.Wall);
                if (title.IndexOf(AlmanacKeys.Stair, StringComparison.OrdinalIgnoreCase) >= 0) return ("Structures", AlmanacKeys.Ladder);

                if (title.IndexOf("Conifer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Flora", "Pine");

                if (title.IndexOf(AlmanacKeys.Carrots, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("Plant", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Flora", "Carrot Plant");

                if (title.IndexOf(AlmanacKeys.Wood, StringComparison.OrdinalIgnoreCase) >= 0) return ("Materials", AlmanacKeys.Wood);
                if (title.IndexOf(AlmanacKeys.Stone, StringComparison.OrdinalIgnoreCase) >= 0) return ("Materials", AlmanacKeys.Stone);
                if (title.IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0) return ("Materials", "Concrete");
                if (title.IndexOf("Steel", StringComparison.OrdinalIgnoreCase) >= 0) return ("Materials", "Steel");

                if (title.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0) return ("Terrain", "Grass");
                if (title.IndexOf("Soil", StringComparison.OrdinalIgnoreCase) >= 0) return ("Terrain", "Soil");
                if (title.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0) return ("Terrain", "Rock");
                if (title.IndexOf("Gravel", StringComparison.OrdinalIgnoreCase) >= 0) return ("Terrain", "Gravel");
                if (title.IndexOf("Pavement", StringComparison.OrdinalIgnoreCase) >= 0) return ("Terrain", "Pavement");
                if (title.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0) return ("Terrain", "Shallow Water");
                if (title.IndexOf("Sand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("Bare", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("Subsoil", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Terrain", "Soil");

                return ("Terrain", "Grass");
            }

            if (inspect.Subject == InspectSubject.Colonist)
            {
                if (string.Equals(inspect.ActiveTabName, "Skills", StringComparison.OrdinalIgnoreCase))
                    return ("Skills", "Construction");
                if (string.Equals(inspect.ActiveTabName, "Needs", StringComparison.OrdinalIgnoreCase))
                    return ("Needs", "Food");

                return ("Skills", "Construction");
            }

            return null;
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
