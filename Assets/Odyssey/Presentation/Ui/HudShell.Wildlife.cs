#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The Wildlife panel (design 30 §6): what is out there. A count per kind across the top,
    /// then one row per animal — kind, what it is doing, its layer, how far from the colony —
    /// twelve to a page with the roster's pager, and a click on a row takes the player to it
    /// the way a roster card does.
    ///
    /// <para><b>Built the way the Work tab is built</b>: one window, docked bottom-left on the
    /// command bar at a constant width, rows pooled once at build and retexted on refresh, and
    /// nothing rebuilt per frame. The count strip is the one thing rebuilt, when the number of
    /// kinds on the board changes, which is a handful of labels a few times a day.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly WildlifeModel _wildlife = new WildlifeModel();

        VisualElement _wildlifePanel = null!;
        VisualElement _wildlifeCounts = null!;
        VisualElement _wildlifeRowsHost = null!;
        VisualElement _wildlifePager = null!;
        Label _wildlifePageLabel = null!;
        VisualElement _wildlifePrev = null!;
        VisualElement _wildlifeNext = null!;

        readonly List<WildlifeRowView> _wildlifeRows = new List<WildlifeRowView>();
        readonly List<Label> _wildlifeCountLabels = new List<Label>();
        int _wildlifeCountsFor = -1;
        int _wildlifeDrawnPage = -1;
        int _wildlifeDrawnPageCount = -1;
        PawnId _wildlifeFollowed;

        sealed class WildlifeRowView
        {
            public VisualElement Root = null!;
            public Label Kind = null!;
            public Label Doing = null!;
            public Label Layer = null!;
            public Label Away = null!;
            public PawnId Id;
            public bool Selected;
        }

        void BuildWildlife()
        {
            _wildlifePanel = Window("wildlife", Registry.Label(WildlifeDirector.PanelKey),
                () => _directors?.Wildlife.SetOpen(false), "wildlife");
            _wildlifePanel.style.position = Position.Absolute;
            _wildlifePanel.style.left = HudLayout.Edge;
            _wildlifePanel.style.bottom = HudCommands.ItemHeight + HudCommands.BarPad * 2;
            // PanelOuterWidth, not PanelWidth: the padding and the border are inside a UI
            // Toolkit width (design 27 §17).
            _wildlifePanel.style.width = WildlifeLayout.PanelOuterWidth;

            // The count strip: "Midden hog 4  Duct rat 3".
            _wildlifeCounts = new VisualElement();
            _wildlifeCounts.style.flexDirection = FlexDirection.Row;
            _wildlifeCounts.style.flexWrap = Wrap.Wrap;
            _wildlifeCounts.style.paddingLeft = WildlifeLayout.LeftPad;
            _wildlifeCounts.style.paddingBottom = HudLayout.HeaderGap;
            _wildlifePanel.Add(_wildlifeCounts);

            // The column headings.
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.paddingLeft = WildlifeLayout.LeftPad;
            head.style.borderBottomWidth = HudTheme.BorderWidth;
            head.style.borderBottomColor = HudTokens.PanelBorder;
            head.Add(Heading(WildlifeDirector.KindKey, WildlifeLayout.KindColumn));
            head.Add(Heading(WildlifeDirector.DoingKey, WildlifeLayout.DoingColumn));
            head.Add(Heading(WildlifeDirector.LayerKey, WildlifeLayout.LayerColumn));
            head.Add(Heading(WildlifeDirector.AwayKey, WildlifeLayout.AwayColumn));
            _wildlifePanel.Add(head);

            // The rows, pooled once: a page's worth and never more.
            _wildlifeRowsHost = new VisualElement();
            for (int i = 0; i < WildlifeLayout.RowsPerPage; i++)
            {
                var view = new WildlifeRowView();
                view.Root = new VisualElement();
                view.Root.AddToClassList("wildlife__row");
                view.Root.style.flexDirection = FlexDirection.Row;
                view.Root.style.alignItems = Align.Center;
                view.Root.style.height = WildlifeLayout.RowHeight;
                view.Root.style.paddingLeft = WildlifeLayout.LeftPad;
                view.Root.style.borderBottomWidth = HudTheme.BorderWidth;
                view.Root.style.borderBottomColor = HudTokens.Divider;
                view.Root.style.display = DisplayStyle.None;

                view.Kind = Cell(WildlifeLayout.KindColumn, numeric: false);
                view.Doing = Cell(WildlifeLayout.DoingColumn, numeric: false);
                view.Layer = Cell(WildlifeLayout.LayerColumn, numeric: true);
                view.Away = Cell(WildlifeLayout.AwayColumn, numeric: true);
                view.Root.Add(view.Kind);
                view.Root.Add(view.Doing);
                view.Root.Add(view.Layer);
                view.Root.Add(view.Away);

                WildlifeRowView captured = view;
                view.Root.RegisterCallback<ClickEvent>(_ => OnWildlifeRowClicked(captured));
                view.Root.RegisterCallback<MouseEnterEvent>(_ => PaintWildlifeRow(captured, hover: true));
                view.Root.RegisterCallback<MouseLeaveEvent>(_ => PaintWildlifeRow(captured, hover: false));

                _wildlifeRowsHost.Add(view.Root);
                _wildlifeRows.Add(view);
            }
            _wildlifePanel.Add(_wildlifeRowsHost);

            _wildlifePager = Pager(
                () => { _wildlife.SetPage(_wildlife.Page - 1); RefreshWildlife(); },
                () => { _wildlife.SetPage(_wildlife.Page + 1); RefreshWildlife(); },
                out _wildlifePrev, out _wildlifeNext, out _wildlifePageLabel);
            _wildlifePager.style.display = DisplayStyle.None;
            _wildlifePanel.Add(_wildlifePager);

            _hud.Add(_wildlifePanel);
        }

        static Label Heading(string key, int width)
        {
            Label label = HudText.Make(Registry.Label(key), HudTextRole.PanelLabel);
            label.style.width = width;
            label.style.flexShrink = 0;
            label.style.height = HudLayout.HeaderHeight;
            return label;
        }

        static Label Cell(int width, bool numeric)
        {
            Label label = HudText.Make(string.Empty, HudTextRole.Row, numeric: numeric);
            label.style.width = width;
            label.style.flexShrink = 0;
            label.style.overflow = Overflow.Hidden;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        void OnWildlifeChanged()
        {
            bool open = _directors != null && _directors.Wildlife.Open;
            _wildlifePanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) return;

            // It docks where the palette, the menu and the Work tab dock; one at a time.
            SetBuildPalette(false);
            ToggleMenu(false);
            _directors?.Work.SetOpen(false);
            _wildlifeDrawnPage = -1;
            RefreshWildlife();
        }

        void RefreshWildlife()
        {
            if (_directors == null || !_directors.Wildlife.Open) return;
            var world = _boot?.World;
            if (world == null) return;

            WorldSnapshot frame = world.Views.Current;
            IReadOnlyList<PawnId> selected = _directors.Selection.Pawns;
            _wildlife.Refresh(frame, selected);

            // A selection made elsewhere brings its page up once, on the frame it changes.
            PawnId one = selected.Count == 1 ? selected[0] : default;
            if (one != _wildlifeFollowed)
            {
                _wildlifeFollowed = one;
                if (one.IsValid && _wildlife.EnsurePageFor(one)) _wildlife.Refresh(frame, selected);
            }

            RefreshWildlifeCounts();

            for (int i = 0; i < _wildlifeRows.Count; i++)
            {
                WildlifeRowView view = _wildlifeRows[i];
                if (i >= _wildlife.Rows.Count)
                {
                    view.Root.style.display = DisplayStyle.None;
                    view.Id = default;
                    continue;
                }
                WildlifeRow row = _wildlife.Rows[i];
                view.Root.style.display = DisplayStyle.Flex;
                view.Id = row.Id;
                view.Kind.text = Registry.Label(row.KindKey);
                view.Doing.text = Registry.Label(row.ActivityKey);
                view.Layer.text = "L" + row.Layer.ToString(System.Globalization.CultureInfo.InvariantCulture);
                view.Away.text = row.Away.ToString(System.Globalization.CultureInfo.InvariantCulture);
                view.Selected = row.Selected;
                PaintWildlifeRow(view, hover: false);
            }

            if (_wildlifeDrawnPage != _wildlife.Page || _wildlifeDrawnPageCount != _wildlife.PageCount)
            {
                _wildlifeDrawnPage = _wildlife.Page;
                _wildlifeDrawnPageCount = _wildlife.PageCount;
                _wildlifePager.style.display = _wildlife.PageCount > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                _wildlifePageLabel.text = (_wildlife.Page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " / " + _wildlife.PageCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _wildlifePrev.style.opacity = _wildlife.Page > 0 ? 1f : 0.35f;
                _wildlifeNext.style.opacity = _wildlife.Page < _wildlife.PageCount - 1 ? 1f : 0.35f;
            }
        }

        /// <summary>One label per kind, rebuilt only when the number of kinds on the board changes.</summary>
        void RefreshWildlifeCounts()
        {
            if (_wildlifeCountsFor != _wildlife.Counts.Count)
            {
                _wildlifeCountsFor = _wildlife.Counts.Count;
                _wildlifeCounts.Clear();
                _wildlifeCountLabels.Clear();
                for (int i = 0; i < _wildlife.Counts.Count; i++)
                {
                    Label label = HudText.Make(string.Empty, HudTextRole.Meta);
                    label.style.marginRight = HudLayout.Gap * 2;
                    _wildlifeCounts.Add(label);
                    _wildlifeCountLabels.Add(label);
                }
            }
            for (int i = 0; i < _wildlifeCountLabels.Count; i++)
            {
                WildlifeCount count = _wildlife.Counts[i];
                _wildlifeCountLabels[i].text = Registry.Label(count.KindKey) + " "
                    + count.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        void PaintWildlifeRow(WildlifeRowView view, bool hover)
        {
            view.Root.style.backgroundColor = view.Selected
                ? HudTokens.Accent
                : hover ? HudTokens.Divider : new UnityEngine.Color(0f, 0f, 0f, 0f);
            UnityEngine.Color ink = view.Selected ? HudTokens.OnAccent : HudTokens.TextPrimary;
            view.Kind.style.color = ink;
            view.Doing.style.color = ink;
            view.Layer.style.color = view.Selected ? HudTokens.OnAccent : HudTokens.TextMeta;
            view.Away.style.color = view.Selected ? HudTokens.OnAccent : HudTokens.TextMeta;
        }

        /// <summary>The roster path: layer, selection, camera — the animal is anywhere.</summary>
        void OnWildlifeRowClicked(WildlifeRowView view)
        {
            if (_directors == null || !view.Id.IsValid) return;
            var world = _boot?.World;
            if (world == null) return;
            _directors.ChooseColonist(view.Id, world.Views.Current);
            RefreshWildlife();
        }
    }
}
