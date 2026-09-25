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

            List<InspectRow> rows = _inspect.ThoughtRows;
            while (_thoughtRows.Count < rows.Count)
            {
                var view = new ThoughtRowView();
                view.Root = new VisualElement();
                view.Root.AddToClassList("inspect__row");
                view.Name = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__rowname");
                // A thought's name is longer than a tile fact's, so the name takes the row and the
                // value keeps its own width at the right.
                view.Name.style.width = StyleKeyword.Auto;
                view.Name.style.flexGrow = 1;
                view.Value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, ussClass: "inspect__rowvalue");
                view.Value.style.flexGrow = 0;
                view.Root.Add(view.Name);
                view.Root.Add(view.Value);
                _thoughtsRowsGrid.Add(view.Root);
                _thoughtRows.Add(view);
            }
            while (_thoughtRows.Count > rows.Count)
            {
                _thoughtsRowsGrid.Remove(_thoughtRows[_thoughtRows.Count - 1].Root);
                _thoughtRows.RemoveAt(_thoughtRows.Count - 1);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                ThoughtRowView view = _thoughtRows[i];
                InspectRow row = rows[i];

                // A heading is the row with no value: the group's name, in the heavier role.
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
