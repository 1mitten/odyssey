#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the colonist pane's Thoughts tab (design 43 §5b). Built on
    /// <c>HudShell.Combat</c>'s Health tab, method for method, so the pane has one way of drawing a
    /// list of label-and-value rows: build the body once into the fixed-height tab box, forget it
    /// when the pane is rebuilt for another subject, show it with the tab strip, and write a value
    /// only when it moved.
    ///
    /// <para><b>Every word and number is the model's</b> (<see cref="InspectModel.ThoughtHeading"/>,
    /// <see cref="InspectModel.ThoughtRows"/>, tested in the fast tier). This file draws them. A
    /// row's value is tinted good or bad by the model; a group heading has no value and is drawn
    /// in the heavier row role so the two groups read apart.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        /// <summary>The Thoughts tab's body, built once per subject into the pane's tab box.</summary>
        VisualElement? _thoughtsBody;

        /// <summary>The line over the list: her mood and where it is heading.</summary>
        Label? _thoughtsHeading;

        /// <summary>The rows under the heading.</summary>
        VisualElement? _thoughtsRowsGrid;

        readonly List<ThoughtRowView> _thoughtRows = new List<ThoughtRowView>();
        string? _thoughtsHeadingShown;

        sealed class ThoughtRowView
        {
            public VisualElement Root = null!;
            public Label Name = null!;
            public Label Value = null!;
            public string LastName = string.Empty;
            public string LastValue = string.Empty;
            public string? LastTint;
            public string? LastTooltip;
            public bool LastHeading;
        }

        /// <summary>Build the Thoughts tab's body into <paramref name="tabBody"/>, hidden until the tab is shown.</summary>
        void BuildThoughtsTab(VisualElement tabBody)
        {
            _thoughtsBody = new VisualElement();
            _thoughtsBody.style.display = DisplayStyle.None;

            _thoughtsHeading = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__rowvalue");
            _thoughtsHeading.style.height = HudLayout.CellRow;
            _thoughtsBody.Add(_thoughtsHeading);

            _thoughtsRowsGrid = new VisualElement();
            _thoughtsBody.Add(_thoughtsRowsGrid);
            _thoughtRows.Clear();
            _thoughtsHeadingShown = null;

            tabBody.Add(_thoughtsBody);
        }

        /// <summary>The pane is being rebuilt for another subject: drop what was built.</summary>
        void ForgetThoughtsTab()
        {
            _thoughtsBody = null;
            _thoughtsHeading = null;
            _thoughtsRowsGrid = null;
            _thoughtRows.Clear();
            _thoughtsHeadingShown = null;
        }

        /// <summary>Show the body while the Thoughts tab is the active one, hide it otherwise.</summary>
        void ShowThoughtsTab(bool shown)
        {
            if (_thoughtsBody != null)
                _thoughtsBody.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Update the rows in place. Called by <c>HudShell.Inspect</c> only for a colonist who is in
        /// the frame (<see cref="InspectModel.ShowsColonistBody"/>, not tombstoned).
        /// </summary>
        void SyncThoughtsTab()
        {
            if (_thoughtsBody == null || _thoughtsHeading == null || _thoughtsRowsGrid == null) return;

            if (!ReferenceEquals(_thoughtsHeadingShown, _inspect.ThoughtHeading))
            {
                _thoughtsHeadingShown = _inspect.ThoughtHeading;
                HudText.Set(_thoughtsHeading, _inspect.ThoughtHeading, HudTextRole.Meta);
            }

            SyncRowList(_thoughtRows, _thoughtsRowsGrid, _inspect.ThoughtRows);
        }

        // ---- the traits, under the needs bars (design 43 §5f) -----------------------------

        /// <summary>The traits block: a heading and a row per trait, shown with the needs grid.</summary>
        VisualElement? _traitsBlock;
        Label? _traitsHeading;
        VisualElement? _traitsRowsGrid;
        readonly List<ThoughtRowView> _traitRows = new List<ThoughtRowView>();

        /// <summary>
        /// Build the traits block into <paramref name="tabBody"/>, straight after the needs grid so
        /// it sits under the bars. Two or three rows and a heading fit the slack the fixed body
        /// leaves under two rows of needs (<c>HudLayoutTests.TheTraitsFitUnderTheNeeds</c>).
        /// </summary>
        void BuildTraitsRows(VisualElement tabBody)
        {
            _traitsBlock = new VisualElement();
            _traitsHeading = HudText.Make(Registry.Label("ui.mind.traits"), HudTextRole.Row, ussClass: "inspect__rowname");
            _traitsHeading.style.width = StyleKeyword.Auto;
            _traitsHeading.style.height = HudLayout.CellRow;
            _traitsHeading.tooltip = Registry.Describe("ui.mind.traits");
            _traitsBlock.Add(_traitsHeading);
            _traitsRowsGrid = new VisualElement();
            _traitsBlock.Add(_traitsRowsGrid);
            _traitRows.Clear();
            tabBody.Add(_traitsBlock);
        }

        void ForgetTraitsRows()
        {
            _traitsBlock = null;
            _traitsHeading = null;
            _traitsRowsGrid = null;
            _traitRows.Clear();
        }

        /// <summary>Shown with the needs grid, and only when she has traits to show.</summary>
        void ShowTraitsRows(bool shown)
        {
            if (_traitsBlock == null) return;
            bool any = _inspect.TraitRows.Count > 0;
            _traitsBlock.style.display = shown && any ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SyncTraitsRows(bool needsShowing)
        {
            if (_traitsBlock == null || _traitsRowsGrid == null) return;
            SyncRowList(_traitRows, _traitsRowsGrid, _inspect.TraitRows);
            ShowTraitsRows(needsShowing);
        }

        /// <summary>
        /// Make <paramref name="views"/> in <paramref name="grid"/> say what <paramref name="rows"/>
        /// says, adding and removing views to match and writing only what moved. A row whose value
        /// is empty and which carries a tooltip is a group heading, drawn in the heavier role.
        /// </summary>
        static void SyncRowList(List<ThoughtRowView> views, VisualElement grid, List<InspectRow> rows)
        {
            while (views.Count < rows.Count)
            {
                var view = new ThoughtRowView();
                view.Root = new VisualElement();
                view.Root.AddToClassList("inspect__row");
                view.Name = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__rowname");
                // A thought's or a trait's name is longer than a tile fact's, so the name takes
                // the row and the value keeps its own width at the right.
                view.Name.style.width = StyleKeyword.Auto;
                view.Name.style.flexGrow = 1;
                view.Value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, ussClass: "inspect__rowvalue");
                view.Value.style.flexGrow = 0;
                view.Root.Add(view.Name);
                view.Root.Add(view.Value);
                grid.Add(view.Root);
                views.Add(view);
            }
            while (views.Count > rows.Count)
            {
                grid.Remove(views[views.Count - 1].Root);
                views.RemoveAt(views.Count - 1);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                ThoughtRowView view = views[i];
                InspectRow row = rows[i];

                bool heading = string.IsNullOrEmpty(row.Value) && row.Tooltip != null;
                if (view.LastName != row.Name || view.LastHeading != heading)
                {
                    view.LastName = row.Name;
                    view.LastHeading = heading;
                    HudText.Set(view.Name, row.Name, heading ? HudTextRole.Row : HudTextRole.Meta);
                }
                if (view.LastValue != row.Value)
                {
                    view.LastValue = row.Value;
                    HudText.Set(view.Value, row.Value, HudTextRole.Meta);
                    view.LastTint = null;
                }

                string? tint = row.Tint?.Hex;
                if (view.LastTint != tint)
                {
                    view.LastTint = tint;
                    if (row.Tint is HudColour colour) view.Value.style.color = HudTokens.Convert(colour);
                    else view.Value.style.color = StyleKeyword.Null;
                }

                if (view.LastTooltip != row.Tooltip)
                {
                    view.LastTooltip = row.Tooltip;
                    view.Root.tooltip = row.Tooltip;
                }
            }
        }
    }
}
