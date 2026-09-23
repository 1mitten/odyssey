#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the fight's Health tab (design 33 §1, §5f). <b>Lane C's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>): one of the two Presentation files the interface lane
    /// writes, so it never edits a file the drawing lane owns. The corpse's and the marauder's
    /// panes need nothing here — their shape is <c>InspectModel</c>'s answer, read by
    /// <c>HudShell.Inspect</c> — and the Spawn rows are <c>HudShell.Debug</c>'s.
    ///
    /// <para><c>HudShell.Inspect</c> calls the four methods below: build the tab body into the
    /// colonist's fixed-height box, forget it when the pane is rebuilt, show or hide it with the
    /// tab strip, and sync it fifteen times a second. <b>Every word and number is the model's</b>
    /// (<see cref="InspectModel.HealthValue"/>, <see cref="InspectModel.HealthPerMille"/>,
    /// <see cref="InspectModel.HealthInk"/>, <see cref="InspectModel.HealthRows"/>, tested in the
    /// fast tier); this file only draws them, and writes an element only when its value moved.</para>
    ///
    /// <para><b>The shape</b>: the hit points as a need bar is drawn — the same builder, so the
    /// bar reads like Food and Rest beside it on the Needs tab — then the condition and the weapon
    /// as two label-and-value rows, in the tile readout's classes.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        /// <summary>The Health tab's body, built once per subject into the pane's tab box.</summary>
        VisualElement? _healthBody;

        /// <summary>The hit-point bar, a need bar in every respect but its numbers.</summary>
        NeedView? _healthBar;

        /// <summary>The rows under the bar: condition, weapon.</summary>
        VisualElement? _healthRowsGrid;

        readonly List<HealthRowView> _healthRows = new List<HealthRowView>();

        // The last values written, so a refresh that says the same thing writes nothing.
        string? _healthValueShown;
        int _healthFillShown = int.MinValue;

        sealed class HealthRowView
        {
            public VisualElement Root = null!;
            public Label Name = null!;
            public Label Value = null!;
            public string LastName = string.Empty;
            public string LastValue = string.Empty;
        }

        /// <summary>Build the Health tab's body into <paramref name="tabBody"/>, hidden until the tab is shown.</summary>
        void BuildHealthTab(VisualElement tabBody)
        {
            _healthBody = new VisualElement();
            _healthBody.style.display = DisplayStyle.None;

            var grid = new VisualElement();
            grid.AddToClassList("needs");
            _healthBar = Need(grid, InspectModel.HealthKey);
            _healthBody.Add(grid);

            _healthRowsGrid = new VisualElement();
            _healthBody.Add(_healthRowsGrid);
            _healthRows.Clear();
            _healthValueShown = null;
            _healthFillShown = int.MinValue;

            tabBody.Add(_healthBody);
        }

        /// <summary>The pane is being rebuilt for another subject: drop what was built.</summary>
        void ForgetHealthTab()
        {
            _healthBody = null;
            _healthBar = null;
            _healthRowsGrid = null;
            _healthRows.Clear();
            _healthValueShown = null;
            _healthFillShown = int.MinValue;
        }

        /// <summary>Show the body while the Health tab is the active one, hide it otherwise.</summary>
        void ShowHealthTab(bool shown)
        {
            if (_healthBody != null)
                _healthBody.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Update the values in place. Called by <c>HudShell.Inspect</c> only for a colonist who is
        /// in the frame (<see cref="InspectModel.ShowsColonistBody"/>, not tombstoned).
        /// </summary>
        void SyncHealthTab()
        {
            if (_healthBody == null || _healthBar == null || _healthRowsGrid == null) return;

            int fill = _inspect.HealthPerMille;
            if (fill != _healthFillShown)
            {
                _healthFillShown = fill;
                _healthBar.Fill.style.width = Length.Percent(fill / 10f);
                _healthBar.Fill.style.backgroundColor = HudTokens.Convert(_inspect.HealthInk);
            }

            if (!ReferenceEquals(_healthValueShown, _inspect.HealthValue))
            {
                _healthValueShown = _inspect.HealthValue;
                HudText.Set(_healthBar.Value, _inspect.HealthValue, HudTextRole.Meta);
            }

            List<InspectRow> rows = _inspect.HealthRows;
            while (_healthRows.Count < rows.Count)
            {
                var view = new HealthRowView();
                view.Root = new VisualElement();
                view.Root.AddToClassList("inspect__row");
                view.Name = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__rowname");
                view.Value = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__rowvalue");
                view.Root.Add(view.Name);
                view.Root.Add(view.Value);
                _healthRowsGrid.Add(view.Root);
                _healthRows.Add(view);
            }
            while (_healthRows.Count > rows.Count)
            {
                _healthRowsGrid.Remove(_healthRows[_healthRows.Count - 1].Root);
                _healthRows.RemoveAt(_healthRows.Count - 1);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                HealthRowView view = _healthRows[i];
                InspectRow row = rows[i];
                if (view.LastName != row.Name)
                {
                    view.LastName = row.Name;
                    HudText.Set(view.Name, row.Name, HudTextRole.Meta);
                }
                if (view.LastValue != row.Value)
                {
                    view.LastValue = row.Value;
                    HudText.Set(view.Value, row.Value, HudTextRole.Meta);
                }
            }
        }
    }
}
