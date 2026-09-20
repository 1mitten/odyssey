#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a store accepts and how much the colony cares about it, as rows a panel can draw and
    /// presses a panel can report — the one control, pointed at a cell.
    ///
    /// <para><b>Pointed at a cell and not at a zone</b>, which is what makes it reusable. Every
    /// intent it emits names <c>Cell</c>, so the same model drives a painted zone today and a
    /// crate the day crates exist (S2) without a second intent pair or a second control. The
    /// simulation resolves the cell to whichever store covers it.</para>
    ///
    /// <para><b>Unity-free, so the fast tier owns the rules.</b> What goes wrong in a filter is
    /// the arithmetic — which rows are on, what a category row says when half its members are
    /// ticked, which preset a press means — and none of that wants an editor to test.</para>
    ///
    /// <para><b>The category rows are here and the tri-state tree is not</b> (decisions 21 and 31).
    /// Four of the six categories have no commodity in them yet, so a parent-and-children tree
    /// would be a roll-up over branches of nought; the rows are flat until the item table is long
    /// enough to need compressing. <see cref="CategoryRow.State"/> already carries the three-way
    /// answer, so the tree is a layout change when it comes rather than a model change.</para>
    /// </summary>
    public sealed class StorageSettingsModel
    {
        /// <summary>The five rungs, in ladder order, by registry key. The panel draws them in this order and the player reads Last at the bottom.</summary>
        public static readonly string[] PriorityKeys =
        {
            "ui.storage.priority.last",
            "ui.storage.priority.low",
            "ui.storage.priority.normal",
            "ui.storage.priority.preferred",
            "ui.storage.priority.urgent",
        };

        /// <summary>The two presets. Two and not eight: one per category would say what the category rows already say (decision 28).</summary>
        public static readonly string[] PresetKeys =
        {
            "ui.storage.preset.everything",
            "ui.storage.preset.nothing",
        };

        /// <summary>The six categories in the owner's order (decision 23), by registry key.</summary>
        public static readonly string[] CategoryKeys =
        {
            "ui.res.category.food",
            "ui.res.category.medicine",
            "ui.res.category.materials",
            "ui.res.category.books",
            "ui.res.category.items",
            "ui.res.category.weapons",
        };

        /// <summary>A category row: what it is called, what it covers, and whether all, some or none of that is ticked.</summary>
        public readonly struct CategoryRow
        {
            public readonly int Category;
            public readonly string Key;
            public readonly int State;
            public readonly int Members;

            public CategoryRow(int category, string key, int state, int members)
            {
                Category = category;
                Key = key;
                State = state;
                Members = members;
            }

            /// <summary>Nothing of this kind exists yet, so the row is drawn but cannot be pressed.</summary>
            public bool IsEmpty => Members == 0;
        }

        /// <summary>One commodity's row: its registry key, its category, and whether it is accepted.</summary>
        public readonly struct DefRow
        {
            public readonly int DefIndex;
            public readonly string Key;
            public readonly int Category;
            public readonly bool Accepted;

            public DefRow(int defIndex, string key, int category, bool accepted)
            {
                DefIndex = defIndex;
                Key = key;
                Category = category;
                Accepted = accepted;
            }
        }

        public const int CategoryOff = 0;
        public const int CategoryMixed = 1;
        public const int CategoryOn = 2;

        readonly List<DefRow> _defs = new List<DefRow>();
        readonly List<CategoryRow> _categories = new List<CategoryRow>();

        /// <summary>The cell the panel is about, or -1 when it is showing nothing.</summary>
        public int Cell { get; private set; } = -1;

        /// <summary>Whether a store covers <see cref="Cell"/> at all. False means the panel does not open.</summary>
        public bool HasStore { get; private set; }

        /// <summary>The store's priority, 0 to 4.</summary>
        public int Priority { get; private set; }

        /// <summary>How many cells the store covers, which is what titles a zone that has no name yet.</summary>
        public int CellCount { get; private set; }

        public IReadOnlyList<DefRow> Defs => _defs;

        public IReadOnlyList<CategoryRow> Categories => _categories;

        /// <summary>The title of a zone that has no name: its priority and its size (naming arrives with S2's groups).</summary>
        public string Title =>
            HasStore ? $"{Registry.Label(PriorityKeys[Priority])} · {CellCount}" : string.Empty;

        /// <summary>
        /// Point the panel at a cell. <paramref name="accepts"/> answers for one item def index,
        /// <paramref name="categoryOf"/> says which category a def belongs to, and
        /// <paramref name="defKeys"/> is the commodity registry keys in def-index order.
        ///
        /// <para>Taken as functions rather than as a reference to the simulation because this
        /// assembly does not reference <c>Odyssey.Sim</c> — the same seam every other model here
        /// is built on, and the reason the fast tier can exercise the whole control.</para>
        /// </summary>
        public void Show(int cell, bool hasStore, int priority, int cellCount,
            IReadOnlyList<string> defKeys, Func<int, bool> accepts, Func<int, int> categoryOf)
        {
            Cell = cell;
            HasStore = hasStore;
            Priority = priority;
            CellCount = cellCount;
            _defs.Clear();
            _categories.Clear();
            if (!hasStore) return;

            for (int i = 0; i < defKeys.Count; i++)
                _defs.Add(new DefRow(i, defKeys[i], categoryOf(i), accepts(i)));

            for (int category = 0; category < CategoryKeys.Length; category++)
            {
                int on = 0, members = 0;
                for (int i = 0; i < _defs.Count; i++)
                {
                    if (_defs[i].Category != category) continue;
                    members++;
                    if (_defs[i].Accepted) on++;
                }

                // An empty category reads as off rather than as on, which is the honest answer to
                // "is everything of this kind accepted" when there is nothing of this kind: a row
                // drawn ticked with nothing behind it tells the player the store takes medicine.
                int state = members == 0 || on == 0 ? CategoryOff
                    : on == members ? CategoryOn
                    : CategoryMixed;
                _categories.Add(new CategoryRow(category, CategoryKeys[category], state, members));
            }
        }

        /// <summary>Nothing is being shown. The panel closes; it does not draw an empty frame.</summary>
        public void Hide()
        {
            Cell = -1;
            HasStore = false;
            _defs.Clear();
            _categories.Clear();
        }

        // ---- what a press means -----------------------------------------------------------------
        //
        // Each returns the intent to submit, described in the four integers an Intent carries, so
        // the presenter that owns the world does the submitting and this stays Unity-free and
        // simulation-free. A press on a row the model says cannot be pressed returns false.

        /// <summary>The payload of one intent: which kind, and its three integers. The cell is <see cref="Cell"/>.</summary>
        public readonly struct Command
        {
            public readonly int A;
            public readonly int B;
            public readonly int C;

            public Command(int a, int b, int c) { A = a; B = b; C = c; }
        }

        /// <summary>Press a rung of the priority ladder. False when it is already that rung.</summary>
        public bool PressPriority(int priority, out Command command)
        {
            command = new Command(priority, 0, 0);
            return HasStore && (uint)priority < (uint)PriorityKeys.Length && priority != Priority;
        }

        /// <summary>Press a preset. Always a change worth sending — pressing Everything on a store that already takes everything is a no-op the simulation refuses cheaply.</summary>
        public bool PressPreset(int preset, out Command command)
        {
            command = new Command(ScopePreset, preset, 0);
            return HasStore && (uint)preset < (uint)PresetKeys.Length;
        }

        /// <summary>
        /// Press a category row. <b>Mixed goes to on</b>, which is the rule the reference uses and
        /// the one a hand expects: a half-ticked row is a row you are in the middle of turning on.
        /// A category with no members cannot be pressed at all.
        /// </summary>
        public bool PressCategory(int category, out Command command)
        {
            command = default;
            if (!HasStore || (uint)category >= (uint)_categories.Count) return false;

            CategoryRow row = _categories[category];
            if (row.IsEmpty) return false;

            bool on = row.State != CategoryOn;
            command = new Command(ScopeCategory, category, on ? 1 : 0);
            return true;
        }

        /// <summary>Press one commodity's row.</summary>
        public bool PressDef(int defIndex, out Command command)
        {
            command = default;
            if (!HasStore) return false;
            for (int i = 0; i < _defs.Count; i++)
            {
                if (_defs[i].DefIndex != defIndex) continue;
                command = new Command(ScopeDef, defIndex, _defs[i].Accepted ? 0 : 1);
                return true;
            }

            return false;
        }

        /// <summary>The scope values <c>SetStorageFilter</c> carries. Restated here for the reason <c>OrderColours.ToolOf</c> restates a designation kind: the enum lives in the simulation, which this assembly does not reference.</summary>
        public const int ScopeDef = 0;

        public const int ScopeCategory = 1;
        public const int ScopePreset = 2;
    }
}
