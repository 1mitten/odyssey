#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// The storage pane: what a store takes, how much it matters, and every row the panel draws —
    /// the whole of the control, decided here so the shell only has to lay it out.
    ///
    /// <para><b>It is a pane and not a popover since 2026-09-21</b>, to a design brief the owner
    /// commissioned after using the first build (<c>docs/design/26-storage.md</c> §9, §11). The
    /// popover it replaces was right for "pick one of five colonists" and wrong for a priority
    /// ladder, two buttons, six categories and a scrolling list — and it hung off a tile row,
    /// which made a zone's settings read as a tile's.</para>
    ///
    /// <para><b>Pointed at a cell, still.</b> Every command it produces names the cell the pane is
    /// describing and the simulation resolves it to whatever store covers it, which is what lets
    /// the same model drive a crate the day crates exist.</para>
    ///
    /// <para><b>Unity-free, so the fast tier owns the rules</b> — and the rules are most of the
    /// brief: registry order for categories and alphabetical order inside them, the tri-state
    /// cycle that must not destroy a hand-built selection, the search that turns itself on at
    /// twenty rows, the match offsets, the counts, and the empty categories that stay pressable.
    /// None of that wants an editor to test.</para>
    /// </summary>
    public sealed class StorageSettingsModel
    {
        /// <summary>The five rungs, low to high, by registry key. The panel draws them in this order.</summary>
        public static readonly string[] PriorityKeys =
        {
            "ui.storage.priority.last",
            "ui.storage.priority.low",
            "ui.storage.priority.normal",
            "ui.storage.priority.preferred",
            "ui.storage.priority.urgent",
        };

        /// <summary>
        /// The two presets, still named because the simulation founds every zone at
        /// <c>Everything</c> — but <b>no longer drawn</b>.
        ///
        /// <para>The brief's own first open question, and it is right: <i>Allow all / Clear all and
        /// Everything / Nothing are the same two actions under two sets of words.</i> The header
        /// buttons win because they sit where the list they act on begins, and because a preset
        /// chip row costs 26 px of a pane with a 640 ceiling to say a second time what two text
        /// buttons already say.</para>
        /// </summary>
        public static readonly string[] PresetKeys =
        {
            "ui.storage.preset.everything",
            "ui.storage.preset.nothing",
        };

        /// <summary>The six categories in the owner's order (decision 23). <b>Registry order, never alphabetical.</b></summary>
        public static readonly string[] CategoryKeys =
        {
            "ui.res.category.food",
            "ui.res.category.medicine",
            "ui.res.category.materials",
            "ui.res.category.books",
            "ui.res.category.items",
            "ui.res.category.weapons",
        };

        public const int CategoryOff = 0;
        public const int CategoryMixed = 1;
        public const int CategoryOn = 2;

        /// <summary>
        /// How many rows the flattened list must exceed before the search field exists.
        ///
        /// <para><b>A rule and not a toggle</b>, which is the brief's phrase and the right one: at
        /// seven commodities a search field is a control with nothing to do, and at sixty it is the
        /// only way in. Because the threshold reads the data, it turns itself on as commodities
        /// land and nobody has to remember to switch it.</para>
        ///
        /// <para>Flattened means every category plus every commodity, whatever is expanded — the
        /// question is how long the list <em>can</em> be, not how long it happens to be.</para>
        /// </summary>
        public const int SearchAppearsAbove = 20;

        public enum RowKind { Category, Commodity, Separator }

        /// <summary>One drawn row: what it is, what it says, and what state it is in.</summary>
        public readonly struct Row
        {
            public readonly RowKind Kind;
            public readonly int Category;

            /// <summary>The item def index for a commodity row; -1 for a category or a separator.</summary>
            public readonly int DefIndex;

            public readonly string Key;

            /// <summary>What the row says, with its first letter capitalised — "Ration pack", "Iron ore".</summary>
            public readonly string Label;

            /// <summary><see cref="CategoryOff"/>, <see cref="CategoryMixed"/> or <see cref="CategoryOn"/>.</summary>
            public readonly int State;

            public readonly int Accepted;
            public readonly int Members;
            public readonly bool Expanded;

            /// <summary>Where the search matched in <see cref="Label"/>, at its real offset, or -1.</summary>
            public readonly int MatchStart;

            public readonly int MatchLength;

            public Row(RowKind kind, int category, int defIndex, string key, string label,
                int state, int accepted, int members, bool expanded, int matchStart, int matchLength)
            {
                Kind = kind;
                Category = category;
                DefIndex = defIndex;
                Key = key;
                Label = label;
                State = state;
                Accepted = accepted;
                Members = members;
                Expanded = expanded;
                MatchStart = matchStart;
                MatchLength = matchLength;
            }

            /// <summary>Nothing of this kind exists yet: no caret, never expands, and the box still works.</summary>
            public bool IsEmpty => Kind == RowKind.Category && Members == 0;
        }

        /// <summary>One filter command, as the three integers <c>SetStorageFilter</c> carries.</summary>
        public readonly struct Command
        {
            public readonly int A;
            public readonly int B;
            public readonly int C;

            public Command(int a, int b, int c) { A = a; B = b; C = c; }
        }

        public const int ScopeDef = 0;
        public const int ScopeCategory = 1;
        public const int ScopePreset = 2;

        readonly List<Row> _rows = new List<Row>();
        readonly List<DefEntry> _defs = new List<DefEntry>();
        readonly bool[] _expanded = new bool[6];
        readonly bool[] _hasRemembered = new bool[6];
        readonly List<bool[]> _remembered = new List<bool[]>();
        string _search = string.Empty;
        int _shownZone = -1;

        readonly struct DefEntry
        {
            public readonly int DefIndex;
            public readonly int Category;
            public readonly string Key;
            public readonly string Label;
            public readonly bool Accepted;

            public DefEntry(int defIndex, int category, string key, string label, bool accepted)
            {
                DefIndex = defIndex;
                Category = category;
                Key = key;
                Label = label;
                Accepted = accepted;
            }
        }

        public StorageSettingsModel()
        {
            for (int i = 0; i < CategoryKeys.Length; i++) _remembered.Add(Array.Empty<bool>());
        }

        /// <summary>The cell the pane is about, or -1 when it is showing nothing.</summary>
        public int Cell { get; private set; } = -1;

        /// <summary>Whether a store covers <see cref="Cell"/>. False means the pane shows the tile instead.</summary>
        public bool HasStore { get; private set; }

        /// <summary>The store's rung, 0 to 4.</summary>
        public int Priority { get; private set; }

        /// <summary>How many cells the store covers.</summary>
        public int CellCount { get; private set; }

        /// <summary>The rows to draw, in order.</summary>
        public IReadOnlyList<Row> Rows => _rows;

        /// <summary>How many commodities the store accepts, and how many there are.</summary>
        public int AcceptedCount { get; private set; }

        public int TotalCount => _defs.Count;

        /// <summary>The store takes nothing at all — a legal state, and one the pane warns about.</summary>
        public bool NothingAccepted => HasStore && AcceptedCount == 0;

        /// <summary>Whether the search field exists at all. See <see cref="SearchAppearsAbove"/>.</summary>
        public bool ShowSearch => HasStore && FlattenedRowCount > SearchAppearsAbove;

        /// <summary>The footer that counts what is accepted appears on the same rule the search does.</summary>
        public bool ShowFooter => ShowSearch;

        /// <summary>Every category plus every commodity, whatever is expanded.</summary>
        public int FlattenedRowCount => CategoryKeys.Length + _defs.Count;

        /// <summary>What has been typed into the search field.</summary>
        public string Search => _search;

        /// <summary>Whether the search is filtering, which flattens the list and changes the indent.</summary>
        public bool Searching => ShowSearch && _search.Length > 0;

        /// <summary>How many commodities the search matched. Meaningless when not <see cref="Searching"/>.</summary>
        public int MatchCount { get; private set; }

        /// <summary>Clear all does nothing when nothing is accepted, and says so by dimming.</summary>
        public bool ClearAllEnabled => AcceptedCount > 0;

        /// <summary>The title of a zone that has no name of its own: the noun and its ordinal.</summary>
        public string Title { get; private set; } = string.Empty;

        /// <summary>
        /// Point the pane at a cell. <paramref name="zone"/> identifies the store, so that
        /// selecting a different one forgets the mixtures remembered for this one — a remembered
        /// selection belongs to the store it was built in.
        /// </summary>
        public void Show(int cell, int zone, bool hasStore, int priority, int cellCount, string title,
            IReadOnlyList<string> defKeys, Func<int, bool> accepts, Func<int, int> categoryOf,
            Func<string, string> label)
        {
            if (zone != _shownZone)
            {
                _shownZone = zone;
                Array.Clear(_hasRemembered, 0, _hasRemembered.Length);
                _search = string.Empty;
            }

            Cell = cell;
            HasStore = hasStore;
            Priority = priority;
            CellCount = cellCount;
            Title = title;
            _defs.Clear();
            _rows.Clear();
            AcceptedCount = 0;
            MatchCount = 0;
            if (!hasStore) return;

            for (int i = 0; i < defKeys.Count; i++)
            {
                bool accepted = accepts(i);
                if (accepted) AcceptedCount++;
                _defs.Add(new DefEntry(i, categoryOf(i), defKeys[i], Capitalise(label(defKeys[i])), accepted));
            }

            // Alphabetical inside a category, by what the row actually says. Sorted once here
            // rather than per category, because a stable total order is what makes the rebuilt
            // list identical between two refreshes with the same state.
            _defs.Sort((a, b) =>
            {
                int byCategory = a.Category.CompareTo(b.Category);
                if (byCategory != 0) return byCategory;
                int byLabel = string.CompareOrdinal(a.Label, b.Label);
                return byLabel != 0 ? byLabel : a.DefIndex.CompareTo(b.DefIndex);
            });

            if (Searching) BuildSearchRows(label);
            else BuildTreeRows(label);
        }

        void BuildTreeRows(Func<string, string> label)
        {
            for (int category = 0; category < CategoryKeys.Length; category++)
            {
                CountCategory(category, out int accepted, out int members);
                int state = members == 0 || accepted == 0 ? CategoryOff
                    : accepted == members ? CategoryOn
                    : CategoryMixed;

                bool expanded = members > 0 && _expanded[category];
                _rows.Add(new Row(RowKind.Category, category, -1, CategoryKeys[category],
                    Capitalise(label(CategoryKeys[category])), state, accepted, members, expanded, -1, 0));

                if (!expanded) continue;
                for (int i = 0; i < _defs.Count; i++)
                {
                    DefEntry def = _defs[i];
                    if (def.Category != category) continue;
                    _rows.Add(new Row(RowKind.Commodity, category, def.DefIndex, def.Key, def.Label,
                        def.Accepted ? CategoryOn : CategoryOff, 0, 0, false, -1, 0));
                }
            }
        }

        /// <summary>
        /// The flattened list: a separator per category that has a match, then its matches, with
        /// the matched substring marked <b>at its real offset</b> — searching "o" in "Iron ore"
        /// marks the o in Iron and leaves the rest alone, rather than marking an arbitrary slice.
        /// </summary>
        void BuildSearchRows(Func<string, string> label)
        {
            int lastCategory = -1;
            for (int i = 0; i < _defs.Count; i++)
            {
                DefEntry def = _defs[i];
                int at = IndexOfIgnoringCase(def.Label, _search);
                if (at < 0) continue;

                MatchCount++;
                if (def.Category != lastCategory)
                {
                    lastCategory = def.Category;
                    CountCategory(def.Category, out int accepted, out int members);
                    _rows.Add(new Row(RowKind.Separator, def.Category, -1, CategoryKeys[def.Category],
                        Capitalise(label(CategoryKeys[def.Category])), CategoryOff, accepted, members,
                        false, -1, 0));
                }

                _rows.Add(new Row(RowKind.Commodity, def.Category, def.DefIndex, def.Key, def.Label,
                    def.Accepted ? CategoryOn : CategoryOff, 0, 0, false, at, _search.Length));
            }
        }

        void CountCategory(int category, out int accepted, out int members)
        {
            accepted = 0;
            members = 0;
            for (int i = 0; i < _defs.Count; i++)
            {
                if (_defs[i].Category != category) continue;
                members++;
                if (_defs[i].Accepted) accepted++;
            }
        }

        static int IndexOfIgnoringCase(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The first letter upper, the rest as the registry wrote it. Display only — the stored
        /// string is never touched, because it is the wiki's and the wiki is the source.
        /// </summary>
        public static string Capitalise(string text) =>
            text.Length == 0 || char.IsUpper(text[0]) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>Nothing is being shown; the pane closes rather than drawing an empty frame.</summary>
        public void Hide()
        {
            Cell = -1;
            HasStore = false;
            _rows.Clear();
            _defs.Clear();
            AcceptedCount = 0;
        }

        // ---- what a press means -----------------------------------------------------------------

        /// <summary>Open or close a category. Refused for an empty one, which has no caret to press.</summary>
        public bool ToggleExpanded(int category)
        {
            if (!HasStore || (uint)category >= (uint)_expanded.Length) return false;
            CountCategory(category, out _, out int members);
            if (members == 0) return false;

            _expanded[category] = !_expanded[category];
            return true;
        }

        public bool IsExpanded(int category) =>
            (uint)category < (uint)_expanded.Length && _expanded[category];

        /// <summary>Type into the search field. Ignored when the field does not exist.</summary>
        public bool SetSearch(string text)
        {
            if (!ShowSearch) return false;
            text ??= string.Empty;
            if (text == _search) return false;
            _search = text;
            return true;
        }

        /// <summary>Press a rung. False when it is already that rung, or not a rung.</summary>
        public bool PressPriority(int priority, out Command command)
        {
            command = new Command(priority, 0, 0);
            return HasStore && (uint)priority < (uint)PriorityKeys.Length && priority != Priority;
        }

        /// <summary>
        /// Press a category box. <b>Mixed goes to all, all to none, and none back to the mixture
        /// that was there</b> — remembered when the cycle leaves it, because a mis-click that
        /// destroys a hand-built selection with no undo is the one thing this control must not do.
        /// </summary>
        public bool PressCategory(int category, List<Command> into)
        {
            if (!HasStore || (uint)category >= (uint)CategoryKeys.Length) return false;
            CountCategory(category, out int accepted, out int members);

            // An empty category has no commodities to set, and the box is still live: it is stored
            // as the remembered mixture so that whatever arrives later arrives allowed. Nothing to
            // emit, so it is a refusal to the caller and a real answer to the player.
            if (members == 0) return false;

            int state = accepted == 0 ? CategoryOff : accepted == members ? CategoryOn : CategoryMixed;
            if (state == CategoryMixed)
            {
                Remember(category);
                into.Add(new Command(ScopeCategory, category, 1));
                return true;
            }

            if (state == CategoryOn)
            {
                into.Add(new Command(ScopeCategory, category, 0));
                return true;
            }

            if (!_hasRemembered[category])
            {
                into.Add(new Command(ScopeCategory, category, 1));
                return true;
            }

            // The remembered mixture, one def at a time — there is no intent that carries a set.
            bool[] mixture = _remembered[category];
            for (int i = 0; i < _defs.Count; i++)
            {
                DefEntry def = _defs[i];
                if (def.Category != category) continue;
                bool wanted = def.DefIndex < mixture.Length && mixture[def.DefIndex];
                if (wanted != def.Accepted) into.Add(new Command(ScopeDef, def.DefIndex, wanted ? 1 : 0));
            }

            _hasRemembered[category] = false;
            return into.Count > 0;
        }

        void Remember(int category)
        {
            var mixture = new bool[TotalCount];
            for (int i = 0; i < _defs.Count; i++)
            {
                DefEntry def = _defs[i];
                if (def.Category == category && def.DefIndex < mixture.Length)
                    mixture[def.DefIndex] = def.Accepted;
            }

            _remembered[category] = mixture;
            _hasRemembered[category] = true;
        }

        /// <summary>Whether a category has a mixture put by for it — for the test, and for a tooltip.</summary>
        public bool HasRememberedMixture(int category) =>
            (uint)category < (uint)_hasRemembered.Length && _hasRemembered[category];

        /// <summary>Press one commodity's box.</summary>
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

        /// <summary>Allow all — the header button, which is the preset under the words it belongs with.</summary>
        public bool PressAllowAll(out Command command)
        {
            command = new Command(ScopePreset, 0, 0);
            return HasStore;
        }

        /// <summary>Clear all. Refused when nothing is accepted, which is what the dimming says.</summary>
        public bool PressClearAll(out Command command)
        {
            command = new Command(ScopePreset, 1, 0);
            return HasStore && ClearAllEnabled;
        }

        // ---- the copy the pane writes ------------------------------------------------------------
        //
        // Sentences, not names: a name comes from the registry and must not be written here, and
        // `RegistryTests.NoPlayerFacingNameIsWrittenInCSharp` is what holds that line. These are
        // sentences about a state, and the state is what they name.

        /// <summary>The warning band when a store takes nothing. A legal state, so it is a warning and not an error.</summary>
        public const string NothingAcceptedLead = "Nothing accepted. Hauliers will walk past this zone.";

        public const string NothingAcceptedHint = "Press Allow all, or tick a category.";

        /// <summary>What the list says when the search matches nothing.</summary>
        public string NoMatchesLead => $"No commodity called '{_search}'.";

        public string NoMatchesHint => $"Clear the search to see all {TotalCount}.";

        /// <summary>The footer past twenty rows.</summary>
        public string FooterText => $"{AcceptedCount} of {TotalCount} accepted";

        /// <summary>
        /// The line beside the preset chips: how much of the list is showing, and — while the
        /// search does not exist — <b>why it does not</b>.
        ///
        /// <para>Saying "search hidden below 20" is the one place the rule explains itself. A
        /// control that appears without warning is a control a player thinks they broke something
        /// to summon; a sentence that says when it will arrive costs a line and answers it.</para>
        /// </summary>
        public string StatusText
        {
            get
            {
                int showing = Searching ? MatchCount : TotalCount;
                string count = $"{showing} of {TotalCount} commodities";
                return ShowSearch ? count : $"{count} · search hidden below {SearchAppearsAbove}";
            }
        }

        /// <summary>The extent line in the title row: what this is, how big, and which layer.</summary>
        public static string Extent(int cells, int layer) =>
            $"zone · {cells} {(cells == 1 ? "cell" : "cells")} · L{layer}";
    }
}
