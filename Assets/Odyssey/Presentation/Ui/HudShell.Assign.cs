#nullable enable
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The Assign tab (design 43 §6; Claude Design's specification of 2026-09-25): one row per
    /// colonist in the roster's order — her portrait and name, where she may work, and what she
    /// does about danger — twelve to a page. A press on a cell moves that colonist's setting round
    /// its values; a press on a name selects her and takes the camera to her.
    ///
    /// <para><b>Built the way the Research and Animals tabs are</b>: one window docked bottom-left
    /// on the command bar at a constant width, rows pooled once at build and retexted on refresh,
    /// nothing rebuilt per frame. Its height follows the page, so a colony of three is a short
    /// window and never a tall empty one.</para>
    ///
    /// <para><b>Selecting a colonist keeps it open</b> (the Work tab's rule, and the spec's): the
    /// point of the tab is setting several people in a row, and a selection that closed it would
    /// make every name a way out.</para>
    ///
    /// <para><b>A cell is a setting, not a button</b>: it reads as the value with a small cycle mark
    /// at its right, bordered, and a colonist kept home or told to flee is drawn in the warning ink
    /// so a player can find them when the raid comes.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly AssignModel _assign = new AssignModel();

        VisualElement _assignPanel = null!;
        Label _assignCount = null!;
        Label _assignNoHearth = null!;
        VisualElement _assignPager = null!;
        Label _assignPageLabel = null!;
        PathGlyph _assignPrevGlyph = null!;
        PathGlyph _assignNextGlyph = null!;

        /// <summary>The bar's Assign item, washed while the tab is open.</summary>
        VisualElement? _assignItem;

        readonly List<AssignRowView> _assignRows = new List<AssignRowView>();
        int _assignDrawnCount = -1;
        int _assignDrawnPage = -1;
        int _assignDrawnPageCount = -1;
        int _assignDrawnRows = -1;
        int _assignDrawnHearth = -1;

        sealed class AssignRowView
        {
            public VisualElement Root = null!;
            public VisualElement NameCell = null!;
            public AvatarGlyph Avatar = null!;
            public Label Name = null!;
            public AssignCell Area = null!;
            public AssignCell Response = null!;
            public PawnId Id;
            public AssignRow Row;
        }

        sealed class AssignCell
        {
            public VisualElement Root = null!;
            public Label Value = null!;
            public PathGlyph Mark = null!;
            public bool Hover;
        }

        void BuildAssign()
        {
            int height = AssignLayout.PanelHeight(0, paged: false);
            _assignPanel = DockedTab("assign", Registry.Label(AssignDirector.PanelKey),
                () => _directors?.Assign.SetOpen(false), AssignLayout.TabWidth, height,
                AssignLayout.HeaderHeight, out VisualElement header);

            // "ASSIGN (14)": the count in mono, six after the word.
            _assignCount = HudText.Make(string.Empty, HudTextRole.Hotkey, numeric: true);
            _assignCount.style.color = HudTokens.TextMeta;
            _assignCount.style.marginLeft = AssignLayout.CountGap;
            Label title = header.Q<Label>(className: "panel__label");
            header.Insert(header.IndexOf(title) + 1, _assignCount);

            var body = new VisualElement();
            body.style.paddingLeft = HudLayout.Pad;
            body.style.paddingRight = HudLayout.Pad;
            body.style.width = AssignLayout.TabWidth - 2 * HudTheme.BorderWidth;
            _assignPanel.Add(body);

            // The column headings. The grid is left open to the right for a fourth column.
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.height = AssignLayout.ColumnHeaderHeight;
            head.style.flexShrink = 0;
            head.style.borderBottomWidth = HudTheme.BorderWidth;
            head.style.borderBottomColor = HudTokens.Convert(HudTheme.RowRule);
            head.Add(AssignHeading(AssignDirector.ColonistKey, AssignLayout.ColonistColumn, out _));
            head.Add(AssignGap());
            VisualElement areaHeading = AssignHeading(AssignDirector.AreaKey, AssignLayout.AreaColumn, out _);
            _assignNoHearth = HudText.Make(Registry.Label(AssignDirector.NoHearthKey), HudTextRole.Meta);
            _assignNoHearth.style.color = HudTokens.TextMeta;
            _assignNoHearth.style.marginLeft = AssignLayout.NoHearthGap;
            _assignNoHearth.style.whiteSpace = WhiteSpace.NoWrap;
            _assignNoHearth.style.display = DisplayStyle.None;
            areaHeading.Add(_assignNoHearth);
            areaHeading.style.overflow = Overflow.Visible;
            head.Add(areaHeading);
            head.Add(AssignGap());
            head.Add(AssignHeading(AssignDirector.ResponseKey, AssignLayout.ResponseColumn, out _));
            body.Add(head);

            // The rows, pooled once: a page's worth and never more.
            for (int i = 0; i < AssignLayout.RowsPerPage; i++)
            {
                var view = new AssignRowView();
                view.Root = new VisualElement();
                view.Root.AddToClassList("assign__row");
                view.Root.style.flexDirection = FlexDirection.Row;
                view.Root.style.alignItems = Align.Center;
                view.Root.style.height = AssignLayout.RowHeight;
                view.Root.style.flexShrink = 0;
                view.Root.style.borderBottomWidth = HudTheme.BorderWidth;
                view.Root.style.borderBottomColor = HudTokens.Convert(HudTheme.RowRule);
                view.Root.style.display = DisplayStyle.None;

                view.NameCell = new VisualElement();
                view.NameCell.style.width = AssignLayout.ColonistColumn;
                view.NameCell.style.height = AssignLayout.RowHeight;
                view.NameCell.style.flexShrink = 0;
                view.NameCell.style.flexDirection = FlexDirection.Row;
                view.NameCell.style.alignItems = Align.Center;
                view.Avatar = new AvatarGlyph(AssignLayout.Portrait);
                view.NameCell.Add(view.Avatar);
                view.Name = HudText.Make(string.Empty, HudTextRole.Row);
                view.Name.style.marginLeft = AssignLayout.PortraitGap;
                view.Name.style.overflow = Overflow.Hidden;
                view.Name.style.whiteSpace = WhiteSpace.NoWrap;
                view.Name.style.flexShrink = 1;
                view.NameCell.Add(view.Name);
                AssignRowView captured = view;
                view.NameCell.RegisterCallback<ClickEvent>(evt =>
                {
                    ChooseAssignColonist(captured.Id);
                    evt.StopPropagation();
                });
                view.Root.Add(view.NameCell);

                view.Root.Add(AssignGap());
                view.Area = AssignSettingCell(AssignLayout.AreaColumn, () => CycleAssignArea(captured));
                view.Root.Add(view.Area.Root);
                view.Root.Add(AssignGap());
                view.Response = AssignSettingCell(AssignLayout.ResponseColumn, () => CycleAssignResponse(captured));
                view.Root.Add(view.Response.Root);

                body.Add(view.Root);
                _assignRows.Add(view);
            }

            BuildAssignPager();
            body.Add(_assignPager);

            _hud.Add(_assignPanel);
        }

        static VisualElement AssignGap()
        {
            var gap = new VisualElement { pickingMode = PickingMode.Ignore };
            gap.style.width = AssignLayout.ColumnGap;
            gap.style.flexShrink = 0;
            return gap;
        }

        static VisualElement AssignHeading(string key, int width, out Label label)
        {
            var cell = new VisualElement();
            cell.style.width = width;
            cell.style.flexShrink = 0;
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            label = HudText.Make(Registry.Label(key), HudTextRole.PanelLabel);
            label.style.color = HudTokens.TextDim;
            cell.Add(label);
            return cell;
        }

        AssignCell AssignSettingCell(int width, System.Action press)
        {
            var cell = new AssignCell();
            cell.Root = new VisualElement();
            cell.Root.AddToClassList("assign__cell");
            cell.Root.style.width = width;
            cell.Root.style.height = AssignLayout.CellHeight;
            cell.Root.style.flexShrink = 0;
            cell.Root.style.flexDirection = FlexDirection.Row;
            cell.Root.style.alignItems = Align.Center;
            cell.Root.style.paddingLeft = AssignLayout.CellPad;
            cell.Root.style.paddingRight = AssignLayout.CellPad;
            cell.Root.style.borderTopWidth = HudTheme.BorderWidth;
            cell.Root.style.borderBottomWidth = HudTheme.BorderWidth;
            cell.Root.style.borderLeftWidth = HudTheme.BorderWidth;
            cell.Root.style.borderRightWidth = HudTheme.BorderWidth;

            cell.Value = HudText.Make(string.Empty, HudTextRole.Row);
            cell.Value.style.flexGrow = 1;
            cell.Value.style.overflow = Overflow.Hidden;
            cell.Value.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Root.Add(cell.Value);

            cell.Mark = new PathGlyph(HudIcons.Cycle, AssignLayout.CycleMark, HudTokens.TextDim,
                stroke: HudIcons.CycleStroke);
            cell.Root.Add(cell.Mark);

            cell.Root.RegisterCallback<ClickEvent>(evt =>
            {
                press();
                evt.StopPropagation();
            });
            cell.Root.RegisterCallback<PointerEnterEvent>(_ => { cell.Hover = true; RepaintAssignCells(); });
            cell.Root.RegisterCallback<PointerLeaveEvent>(_ => { cell.Hover = false; RepaintAssignCells(); });
            return cell;
        }

        void BuildAssignPager()
        {
            _assignPager = new VisualElement();
            _assignPager.AddToClassList("assign__pager");
            _assignPager.style.flexDirection = FlexDirection.Row;
            _assignPager.style.alignItems = Align.Center;
            _assignPager.style.justifyContent = Justify.FlexEnd;
            _assignPager.style.height = AssignLayout.PagerHeight;
            _assignPager.style.flexShrink = 0;
            _assignPager.style.display = DisplayStyle.None;

            _assignPager.Add(AssignPagerButton(HudIcons.ChevronLeft, out _assignPrevGlyph, -1));
            _assignPageLabel = HudText.Make("1 / 1", HudTextRole.Meta, numeric: true);
            _assignPageLabel.style.color = HudTokens.TextPrimary;
            _assignPageLabel.style.marginLeft = AssignLayout.PagerGap;
            _assignPageLabel.style.marginRight = AssignLayout.PagerGap;
            _assignPager.Add(_assignPageLabel);
            _assignPager.Add(AssignPagerButton(HudIcons.ChevronRight, out _assignNextGlyph, +1));
        }

        VisualElement AssignPagerButton(string path, out PathGlyph glyph, int delta)
        {
            var button = new VisualElement();
            button.style.width = AssignLayout.PagerButton;
            button.style.height = AssignLayout.PagerButton;
            button.style.flexShrink = 0;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.borderTopWidth = HudTheme.BorderWidth;
            button.style.borderBottomWidth = HudTheme.BorderWidth;
            button.style.borderLeftWidth = HudTheme.BorderWidth;
            button.style.borderRightWidth = HudTheme.BorderWidth;
            button.style.borderTopColor = HudTokens.PanelBorder;
            button.style.borderBottomColor = HudTokens.PanelBorder;
            button.style.borderLeftColor = HudTokens.PanelBorder;
            button.style.borderRightColor = HudTokens.PanelBorder;
            glyph = new PathGlyph(path, AssignLayout.PagerChevron, HudTokens.TextPrimary);
            button.Add(glyph);
            button.RegisterCallback<ClickEvent>(evt =>
            {
                _assign.SetPage(_assign.Page + delta);
                RefreshAssign();
                evt.StopPropagation();
            });
            return button;
        }

        void OnAssignChanged()
        {
            bool open = _directors != null && _directors.Assign.Open;
            _assignPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _assignItem?.EnableInClassList("cmd--on", open);
            if (!open) return;

            // It docks where the palette, the menu and the other tabs dock; one at a time. And
            // opening it puts the inspect pane away (the spec), as opening Animals does — but a
            // name pressed afterwards selects its colonist and leaves the tab open.
            SetBuildPalette(false);
            ToggleMenu(false);
            _directors?.Work.SetOpen(false);
            _directors?.Animals.SetOpen(false);
            _directors?.Inventory.SetOpen(false);
            _directors?.Research.SetOpen(false);
            _directors?.Selection.Clear();
            _assignDrawnCount = _assignDrawnPage = _assignDrawnPageCount = _assignDrawnRows = _assignDrawnHearth = -1;
            RefreshAssign();
        }

        void RefreshAssign()
        {
            if (_directors == null || !_directors.Assign.Open) return;
            var world = _boot?.World;
            if (world == null) return;

            WorldSnapshot frame = world.Views.Current;
            _assign.Refresh(frame, _roster.CustomOrder, _directors.Selection.Pawns);

            if (_assignDrawnCount != _assign.TotalCount)
            {
                _assignDrawnCount = _assign.TotalCount;
                _assignCount.text = "(" + _assign.TotalCount.ToString(CultureInfo.InvariantCulture) + ")";
            }

            int hearth = _assign.NoHearth ? 1 : 0;
            if (_assignDrawnHearth != hearth)
            {
                _assignDrawnHearth = hearth;
                _assignNoHearth.style.display = _assign.NoHearth ? DisplayStyle.Flex : DisplayStyle.None;
            }

            for (int i = 0; i < _assignRows.Count; i++)
            {
                AssignRowView view = _assignRows[i];
                if (i >= _assign.Rows.Count)
                {
                    view.Root.style.display = DisplayStyle.None;
                    view.Id = default;
                    continue;
                }
                AssignRow row = _assign.Rows[i];
                view.Root.style.display = DisplayStyle.Flex;
                view.Id = row.Id;
                view.Row = row;
                HudText.Set(view.Name, row.Name, HudTextRole.Row);
                view.Area.Value.text = Registry.Label(row.AreaKey);
                view.Response.Value.text = Registry.Label(row.ResponseKey);
                view.Avatar.SetFace(ColonistFace.Of(frame, row.Id));
                if (_boot != null) view.Avatar.SetPortrait(_boot.Portraits.For(frame, row.Id));
            }
            RepaintAssignCells();

            // The height follows the page, so a small colony is a short window.
            if (_assignDrawnRows != _assign.Rows.Count || _assignDrawnPageCount != _assign.PageCount)
            {
                _assignDrawnRows = _assign.Rows.Count;
                _assignPanel.style.height = AssignLayout.PanelHeight(_assign.Rows.Count, _assign.Paged);
            }

            if (_assignDrawnPage != _assign.Page || _assignDrawnPageCount != _assign.PageCount)
            {
                _assignDrawnPage = _assign.Page;
                _assignDrawnPageCount = _assign.PageCount;
                _assignPager.style.display = _assign.Paged ? DisplayStyle.Flex : DisplayStyle.None;
                _assignPageLabel.text = (_assign.Page + 1).ToString(CultureInfo.InvariantCulture)
                    + " / " + _assign.PageCount.ToString(CultureInfo.InvariantCulture);
                Color live = HudTokens.TextPrimary;
                Color dim = live;
                dim.a *= AssignLayout.PagerDimmed;
                _assignPrevGlyph.Tint = _assign.Page > 0 ? live : dim;
                _assignNextGlyph.Tint = _assign.Page < _assign.PageCount - 1 ? live : dim;
            }
        }

        /// <summary>The four states of a row and its two cells (design 43 §6): normal, cautious, hover, selected.</summary>
        void RepaintAssignCells()
        {
            for (int i = 0; i < _assignRows.Count; i++)
            {
                AssignRowView view = _assignRows[i];
                if (!view.Id.IsValid) continue;
                bool selected = view.Row.Selected;
                view.Root.style.backgroundColor = selected ? HudTokens.Accent : new Color(0f, 0f, 0f, 0f);
                view.Name.style.color = selected ? HudTokens.OnAccent : HudTokens.TextPrimary;
                view.Avatar.style.backgroundColor = selected
                    ? HudTokens.Convert(HudTheme.OnAccent.WithAlpha(0.25f))
                    : HudTokens.PanelBorder;
                PaintAssignCell(view.Area, selected, view.Row.AreaCautious);
                PaintAssignCell(view.Response, selected, view.Row.ResponseCautious);
            }
        }

        static void PaintAssignCell(AssignCell cell, bool selected, bool cautious)
        {
            Color border, fill = new Color(0f, 0f, 0f, 0f), ink, mark;
            if (selected)
            {
                // On the accent row the warning is dropped: the row is already the loudest thing.
                border = HudTokens.Convert(HudTheme.OnAccent.WithAlpha(0.35f));
                ink = HudTokens.OnAccent;
                mark = HudTokens.Convert(HudTheme.OnAccent.WithAlpha(0.60f));
            }
            else if (cell.Hover)
            {
                border = HudTokens.Accent;
                fill = HudTokens.Convert(HudTheme.Accent.WithAlpha(0.12f));
                ink = HudTokens.TextPrimary;
                mark = HudTokens.Accent;
            }
            else if (cautious)
            {
                border = HudTokens.Convert(HudTheme.Warn.WithAlpha(0.50f));
                ink = HudTokens.Warn;
                mark = HudTokens.Warn;
            }
            else
            {
                border = HudTokens.Convert(HudTheme.ControlBorder);
                ink = HudTokens.TextPrimary;
                mark = HudTokens.TextDim;
            }
            cell.Root.style.backgroundColor = fill;
            cell.Root.style.borderTopColor = border;
            cell.Root.style.borderBottomColor = border;
            cell.Root.style.borderLeftColor = border;
            cell.Root.style.borderRightColor = border;
            cell.Value.style.color = ink;
            cell.Mark.Tint = mark;
        }

        void CycleAssignArea(AssignRowView view)
        {
            var world = _boot?.World;
            if (world == null || !view.Id.IsValid) return;
            if (AssignModel.TryCycleArea(world.Views.Current, view.Id, out Intent intent))
                world.Intents.Submit(intent);
        }

        void CycleAssignResponse(AssignRowView view)
        {
            var world = _boot?.World;
            if (world == null || !view.Id.IsValid) return;
            if (AssignModel.TryCycleResponse(world.Views.Current, view.Id, out Intent intent))
                world.Intents.Submit(intent);
        }

        /// <summary>A name pressed: the roster card's path, selection and camera. The tab stays open.</summary>
        void ChooseAssignColonist(PawnId id)
        {
            var world = _boot?.World;
            if (world == null || _directors == null || !id.IsValid) return;
            _directors.ChooseColonist(id, world.Views.Current);
        }
    }
}
