#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the Health tab (design 43 §10; design 33 §5f before it). Two columns
    /// of seven on the Skills tab's own grid — the same <see cref="SkillLine"/> builder, the same
    /// <c>.skills</c> and <c>.skill</c> classes — so the tab is one hand with the Skills tab beside
    /// it and costs the pane's fixed tab body nothing.
    ///
    /// <para><c>HudShell.Inspect</c> calls the four methods below: build the tab body into the
    /// colonist's fixed-height box, forget it when the pane is rebuilt, show or hide it with the
    /// tab strip, and sync it fifteen times a second. <b>Every word and number is the model's</b>
    /// (<see cref="HealthTab"/>, tested in the fast tier); this file only draws them, and writes an
    /// element only when its value moved.</para>
    ///
    /// <para><b>The grid wraps row by row</b>, so the lines are added left, right, left, right: the
    /// left column is the regions and pain, the right the pool, the capacities, the blood, the
    /// bleed and the tend. A click on a region line selects it (<see cref="HealthTab.Select"/>) and
    /// the model answers with that region's injuries in the right column.</para>
    ///
    /// <para><b>The mark</b> at the end of a line sits where a skill's passion lozenges sit, and is
    /// a drawn glyph (<c>docs/bug-patterns.md</c> P13): the warning triangle in the bad ink for a
    /// bleed, the medical cross in the good ink for a tend. The HUD has no blood drop; a proposed
    /// one is the brief's to return.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        /// <summary>The Health tab's body, built once per subject into the pane's tab box.</summary>
        VisualElement? _healthBody;

        readonly List<HealthLineView> _healthLeft = new List<HealthLineView>();
        readonly List<HealthLineView> _healthRight = new List<HealthLineView>();

        sealed class HealthLineView
        {
            public SkillLineView Line = null!;
            public HudGlyph Mark = null!;
            public string LastName = "\u0000";
            public string LastValue = "\u0000";
            public string LastIcon = "\u0000";
            public string LastTip = "\u0000";
            public int LastBar = int.MinValue;
            public HudColour LastInk;
            public HealthMark LastMark = (HealthMark)255;
            public int LastSelected = -1;
            public int LastEmpty = -1;
        }

        /// <summary>Build the Health tab's body into <paramref name="tabBody"/>, hidden until the tab is shown.</summary>
        void BuildHealthTab(VisualElement tabBody)
        {
            _healthBody = new VisualElement();
            _healthBody.AddToClassList("skills");
            _healthBody.style.display = DisplayStyle.None;
            _healthLeft.Clear();
            _healthRight.Clear();

            for (int row = 0; row < HealthTab.Rows; row++)
            {
                _healthLeft.Add(HealthLine(_healthBody, left: true, row));
                _healthRight.Add(HealthLine(_healthBody, left: false, row));
            }

            tabBody.Add(_healthBody);
        }

        HealthLineView HealthLine(VisualElement grid, bool left, int row)
        {
            var view = new HealthLineView { Line = SkillLine(grid, withBar: true) };

            // The passion lozenges make way for the mark.
            for (int i = 0; i < view.Line.Passion.childCount; i++)
                view.Line.Passion[i].style.display = DisplayStyle.None;
            view.Mark = new HudGlyph(HudGlyphKind.Placeholder, 14f, HudTokens.TextMeta);
            view.Mark.style.display = DisplayStyle.None;
            view.Line.Passion.Add(view.Mark);

            // A region line is a button: the model says which one it is on each sync.
            if (left)
            {
                int index = row;
                view.Line.Root.RegisterCallback<ClickEvent>(_ =>
                {
                    int region = _inspect.Health.Left[index].Region;
                    if (region < 0) return;
                    _inspect.Health.Select(region);
                    SyncHealthTab();
                });
            }
            return view;
        }

        /// <summary>The pane is being rebuilt for another subject: drop what was built.</summary>
        void ForgetHealthTab()
        {
            _healthBody = null;
            _healthLeft.Clear();
            _healthRight.Clear();
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
            if (_healthBody == null) return;
            HealthTab tab = _inspect.Health;
            for (int row = 0; row < HealthTab.Rows && row < _healthLeft.Count; row++)
            {
                SetHealthLine(_healthLeft[row], tab.Left[row]);
                SetHealthLine(_healthRight[row], tab.Right[row]);
            }
        }

        static void SetHealthLine(HealthLineView view, in HealthLine line)
        {
            SkillLineView skill = view.Line;

            int empty = line.Empty ? 1 : 0;
            if (view.LastEmpty != empty)
            {
                view.LastEmpty = empty;
                skill.Root.style.visibility = line.Empty ? Visibility.Hidden : Visibility.Visible;
            }
            if (line.Empty) return;

            if (view.LastIcon != line.IconKey)
            {
                view.LastIcon = line.IconKey;
                skill.Icon.SetKey(line.IconKey);
            }
            if (view.LastName != line.Name)
            {
                view.LastName = line.Name;
                HudText.Set(skill.Name, line.Name, skill.NameRole);
            }
            if (view.LastValue != line.Value)
            {
                view.LastValue = line.Value;
                HudText.Set(skill.Value, line.Value, skill.LevelRole);
            }
            if (view.LastTip != line.Tip)
            {
                view.LastTip = line.Tip;
                skill.Root.tooltip = line.Tip;
            }

            if (skill.Track != null && skill.Fill != null && (view.LastBar != line.Bar || !view.LastInk.Equals(line.Ink)))
            {
                view.LastBar = line.Bar;
                view.LastInk = line.Ink;
                skill.Track.style.display = line.Bar < 0 ? DisplayStyle.None : DisplayStyle.Flex;
                if (line.Bar >= 0)
                {
                    skill.Fill.style.width = Length.Percent(line.Bar / 10f);
                    skill.Fill.style.backgroundColor = HudTokens.Convert(line.Ink);
                }
            }

            if (view.LastMark != line.Mark)
            {
                view.LastMark = line.Mark;
                view.Mark.style.display = line.Mark == HealthMark.None ? DisplayStyle.None : DisplayStyle.Flex;
                if (line.Mark == HealthMark.Bleeding)
                {
                    view.Mark.Kind = HudGlyphKind.AlertTriangle;
                    view.Mark.Tint = HudTokens.Bad;
                }
                else if (line.Mark == HealthMark.Tended)
                {
                    view.Mark.Kind = HudGlyphKind.CategoryMedicine;
                    view.Mark.Tint = HudTokens.Good;
                }
            }

            int selected = line.Selected ? 1 : 0;
            if (view.LastSelected != selected)
            {
                view.LastSelected = selected;
                skill.Root.style.backgroundColor = line.Selected ? HudTokens.Accent : new StyleColor(StyleKeyword.Null);
                skill.Name.style.color = line.Selected ? HudTokens.OnAccent : new StyleColor(StyleKeyword.Null);
                skill.Value.style.color = line.Selected ? HudTokens.OnAccent : new StyleColor(StyleKeyword.Null);
            }
        }
    }
}
