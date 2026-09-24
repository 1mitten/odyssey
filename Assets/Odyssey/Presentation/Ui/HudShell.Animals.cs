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
    /// The Animals tab (design 30 §6; the brief in <c>docs/reference/mockups/animals-tab-brief.md</c>):
    /// what is out there. A count per kind across the top, then one row per animal — a portrait
    /// tile, the kind, what it is doing — twelve to a page, sortable by either heading, and a
    /// click on a row takes the player to the animal the way a roster card does.
    ///
    /// <para><b>Built the way the Work tab is built</b>: one window docked bottom-left on the
    /// command bar at a constant width, rows pooled once at build and retexted on refresh, and
    /// nothing rebuilt per frame. The count strip is the one thing rebuilt, when the number of
    /// kinds on the board changes, which is a handful of labels a few times a day.</para>
    ///
    /// <para><b>The tab and the inspect pane never show together</b> (the brief): opening the
    /// tab clears the selection, and a selection — including the one a row click makes — closes
    /// the tab, so the pane that then shows the animal has the corner to itself.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly AnimalsModel _animals = new AnimalsModel();

        VisualElement _animalsPanel = null!;
        VisualElement _animalsCounts = null!;
        VisualElement _animalsRowsHost = null!;
        Label _animalsEmpty = null!;
        VisualElement _animalsPager = null!;
        Label _animalsPageLabel = null!;
        VisualElement _animalsPrev = null!;
        VisualElement _animalsNext = null!;
        HudGlyph _animalsPrevGlyph = null!;
        HudGlyph _animalsNextGlyph = null!;

        /// <summary>The bar's Animals item, washed while the tab is open, as the Build cap is while the palette is.</summary>
        VisualElement? _animalsItem;

        readonly AnimalsHeading[] _animalsHeadings = new AnimalsHeading[2];
        readonly List<AnimalRowView> _animalsRows = new List<AnimalRowView>();
        readonly List<(Label Word, Label Figure)> _animalsCountLabels = new List<(Label, Label)>();
        int _animalsCountsFor = -1;
        int _animalsDrawnPage = -1;
        int _animalsDrawnPageCount = -1;
        AnimalsSort _animalsDrawnSort = (AnimalsSort)(-1);

        sealed class AnimalsHeading
        {
            public VisualElement Root = null!;
            public Label Label = null!;
            public SortMark Mark = null!;
            public AnimalsSort Sort;
        }

        sealed class AnimalRowView
        {
            public VisualElement Root = null!;
            public VisualElement Portrait = null!;
            public Label Kind = null!;
            public VisualElement DoingSquare = null!;
            public Label Doing = null!;
            public PawnId Id;
            public bool Selected;
        }

        /// <summary>The 7 x 5 triangle beside a sorted heading: drawn, because no shipped font has one.</summary>
        sealed class SortMark : VisualElement
        {
            Color _tint;

            public SortMark(Color tint)
            {
                _tint = tint;
                style.width = AnimalsLayout.SortMarkWidth;
                style.height = AnimalsLayout.SortMarkHeight;
                style.flexShrink = 0;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                painter.fillColor = _tint;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, 0f));
                painter.LineTo(new Vector2(AnimalsLayout.SortMarkWidth, 0f));
                painter.LineTo(new Vector2(AnimalsLayout.SortMarkWidth / 2f, AnimalsLayout.SortMarkHeight));
                painter.ClosePath();
                painter.Fill();
            }
        }

        void BuildAnimals()
        {
            _animalsPanel = Window("animals", Registry.Label(AnimalsDirector.PanelKey),
                () => _directors?.Animals.SetOpen(false), "animals");
            _animalsPanel.style.position = Position.Absolute;
            _animalsPanel.style.left = HudLayout.Edge;
            _animalsPanel.style.bottom = HudCommands.ItemHeight + HudCommands.BarPad * 2;
            // The brief's 560 is the window: a UI Toolkit width is a border box, so the panel's
            // 12 px padding and 1 px border on each side are inside it and the grid gets 534.
            _animalsPanel.style.width = AnimalsLayout.TabWidth;

            // The count strip: "Midden hog 6   Duct rat 4", hidden on an empty board.
            _animalsCounts = new VisualElement();
            _animalsCounts.AddToClassList("animals__counts");
            _animalsCounts.style.flexDirection = FlexDirection.Row;
            _animalsCounts.style.alignItems = Align.Center;
            _animalsCounts.style.height = AnimalsLayout.CountStripHeight;
            _animalsCounts.style.borderBottomWidth = HudTheme.BorderWidth;
            _animalsCounts.style.borderBottomColor = HudTokens.Divider;
            _animalsCounts.style.display = DisplayStyle.None;
            _animalsPanel.Add(_animalsCounts);

            // The header row: a spacer over the portraits, then two headings that sort.
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.height = AnimalsLayout.RowHeight;
            head.style.borderBottomWidth = HudTheme.BorderWidth;
            head.style.borderBottomColor = HudTokens.PanelBorder;
            var spacer = new VisualElement();
            spacer.style.width = AnimalsLayout.PortraitColumn;
            spacer.style.flexShrink = 0;
            head.Add(spacer);
            _animalsHeadings[0] = Heading(AnimalsDirector.KindKey, AnimalsLayout.KindColumn, AnimalsSort.Kind);
            _animalsHeadings[1] = Heading(AnimalsDirector.DoingKey, AnimalsLayout.DoingColumn, AnimalsSort.Doing);
            head.Add(_animalsHeadings[0].Root);
            head.Add(_animalsHeadings[1].Root);
            _animalsPanel.Add(head);

            // The rows, pooled once: a page's worth and never more.
            _animalsRowsHost = new VisualElement();
            for (int i = 0; i < AnimalsLayout.RowsPerPage; i++)
            {
                var view = new AnimalRowView();
                view.Root = new VisualElement();
                view.Root.AddToClassList("animals__row");
                view.Root.style.flexDirection = FlexDirection.Row;
                view.Root.style.alignItems = Align.Center;
                view.Root.style.height = AnimalsLayout.RowHeight;
                view.Root.style.borderBottomWidth = HudTheme.BorderWidth;
                view.Root.style.borderBottomColor = HudTokens.Divider;
                view.Root.style.display = DisplayStyle.None;

                var portraitCell = new VisualElement();
                portraitCell.style.width = AnimalsLayout.PortraitColumn;
                portraitCell.style.flexShrink = 0;
                portraitCell.style.justifyContent = Justify.Center;
                view.Portrait = new VisualElement();
                view.Portrait.style.width = AnimalsLayout.PortraitTile;
                view.Portrait.style.height = AnimalsLayout.PortraitTile;
                portraitCell.Add(view.Portrait);
                view.Root.Add(portraitCell);

                view.Kind = HudText.Make(string.Empty, HudTextRole.Row);
                view.Kind.style.width = AnimalsLayout.KindColumn;
                view.Kind.style.flexShrink = 0;
                view.Kind.style.overflow = Overflow.Hidden;
                view.Kind.style.whiteSpace = WhiteSpace.NoWrap;
                view.Root.Add(view.Kind);

                var doingCell = new VisualElement();
                doingCell.style.width = AnimalsLayout.DoingColumn;
                doingCell.style.flexShrink = 0;
                doingCell.style.flexDirection = FlexDirection.Row;
                doingCell.style.alignItems = Align.Center;
                view.DoingSquare = new VisualElement();
                view.DoingSquare.style.width = AnimalsLayout.DoingSquare;
                view.DoingSquare.style.height = AnimalsLayout.DoingSquare;
                view.DoingSquare.style.marginRight = AnimalsLayout.DoingGap;
                view.DoingSquare.style.flexShrink = 0;
                doingCell.Add(view.DoingSquare);
                view.Doing = HudText.Make(string.Empty, HudTextRole.Row);
                view.Doing.style.overflow = Overflow.Hidden;
                view.Doing.style.whiteSpace = WhiteSpace.NoWrap;
                doingCell.Add(view.Doing);
                view.Root.Add(doingCell);

                AnimalRowView captured = view;
                view.Root.RegisterCallback<ClickEvent>(_ => OnAnimalRowClicked(captured));

                _animalsRowsHost.Add(view.Root);
                _animalsRows.Add(view);
            }
            _animalsPanel.Add(_animalsRowsHost);

            // The empty board: one quiet line where the rows would be.
            _animalsEmpty = HudText.Make(Registry.Label(AnimalsDirector.EmptyKey), HudTextRole.Meta, ussClass: "animals__empty");
            _animalsEmpty.style.height = AnimalsLayout.RowHeight;
            _animalsEmpty.style.unityTextAlign = TextAnchor.MiddleLeft;
            _animalsEmpty.style.color = HudTokens.TextMeta;
            _animalsEmpty.style.display = DisplayStyle.None;
            _animalsPanel.Add(_animalsEmpty);

            BuildAnimalsPager();
            _animalsPanel.Add(_animalsPager);

            _hud.Add(_animalsPanel);
        }

        AnimalsHeading Heading(string key, int width, AnimalsSort sort)
        {
            var heading = new AnimalsHeading { Sort = sort };
            heading.Root = new VisualElement();
            heading.Root.AddToClassList("animals__heading");
            heading.Root.style.width = width;
            heading.Root.style.flexShrink = 0;
            heading.Root.style.flexDirection = FlexDirection.Row;
            heading.Root.style.alignItems = Align.Center;
            heading.Label = HudText.Make(Registry.Label(key), HudTextRole.PanelLabel);
            heading.Label.style.color = HudTokens.TextDim;
            heading.Root.Add(heading.Label);
            heading.Mark = new SortMark(HudTokens.Accent);
            heading.Mark.AddToClassList("animals__sortmark");
            heading.Mark.style.marginLeft = AnimalsLayout.SortMarkGap;
            heading.Mark.style.display = DisplayStyle.None;
            heading.Root.Add(heading.Mark);
            heading.Root.RegisterCallback<ClickEvent>(_ =>
            {
                if (_animals.SortBy(sort)) RefreshAnimals();
            });
            return heading;
        }

        void BuildAnimalsPager()
        {
            _animalsPager = new VisualElement();
            _animalsPager.AddToClassList("animals__pager");
            _animalsPager.style.flexDirection = FlexDirection.Row;
            _animalsPager.style.alignItems = Align.Center;
            _animalsPager.style.justifyContent = Justify.FlexEnd;
            _animalsPager.style.height = AnimalsLayout.PagerHeight;
            _animalsPager.style.display = DisplayStyle.None;

            _animalsPrev = PagerButton(out _animalsPrevGlyph, HudGlyphKind.ChevronLeft,
                () => { _animals.SetPage(_animals.Page - 1); RefreshAnimals(); });
            _animalsPager.Add(_animalsPrev);

            _animalsPageLabel = HudText.Make("1 / 1", HudTextRole.Meta, numeric: true);
            _animalsPageLabel.style.color = HudTokens.TextPrimary;
            _animalsPageLabel.style.marginLeft = AnimalsLayout.PagerGap;
            _animalsPageLabel.style.marginRight = AnimalsLayout.PagerGap;
            _animalsPager.Add(_animalsPageLabel);

            _animalsNext = PagerButton(out _animalsNextGlyph, HudGlyphKind.ChevronRight,
                () => { _animals.SetPage(_animals.Page + 1); RefreshAnimals(); });
            _animalsPager.Add(_animalsNext);
        }

        static VisualElement PagerButton(out HudGlyph glyph, HudGlyphKind kind, System.Action press)
        {
            var button = new VisualElement();
            button.style.width = AnimalsLayout.PagerButton;
            button.style.height = AnimalsLayout.PagerButton;
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
            glyph = new HudGlyph(kind, 10f, HudTokens.TextPrimary);
            button.Add(glyph);
            button.RegisterCallback<ClickEvent>(evt => { press(); evt.StopPropagation(); });
            return button;
        }

        void OnAnimalsChanged()
        {
            bool open = _directors != null && _directors.Animals.Open;
            _animalsPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _animalsItem?.EnableInClassList("cmd--on", open);
            if (!open) return;

            // It docks where the palette, the menu and the Work tab dock; one at a time. And it
            // never shows beside the inspect pane (the brief), so whatever was selected is put
            // away, as opening Build puts it away.
            SetBuildPalette(false);
            ToggleMenu(false);
            _directors?.Work.SetOpen(false);
            _directors?.Inventory.SetOpen(false);
            _directors?.Research.SetOpen(false);
            _directors?.Selection.Clear();
            _animalsDrawnPage = -1;
            _animalsDrawnSort = (AnimalsSort)(-1);
            RefreshAnimals();
        }

        void RefreshAnimals()
        {
            if (_directors == null || !_directors.Animals.Open) return;
            var world = _boot?.World;
            if (world == null) return;

            WorldSnapshot frame = world.Views.Current;
            _animals.Refresh(frame, _directors.Selection.Pawns);

            RefreshAnimalsCounts();

            bool empty = _animals.TotalCount == 0;
            _animalsEmpty.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _animalsRows.Count; i++)
            {
                AnimalRowView view = _animalsRows[i];
                if (i >= _animals.Rows.Count)
                {
                    view.Root.style.display = DisplayStyle.None;
                    view.Id = default;
                    continue;
                }
                AnimalRow row = _animals.Rows[i];
                view.Root.style.display = DisplayStyle.Flex;
                view.Id = row.Id;
                view.Kind.text = Registry.Label(row.KindKey);
                view.Doing.text = Registry.Label(row.ActivityKey);
                view.Selected = row.Selected;
                PaintAnimalRow(view);
            }

            if (_animalsDrawnSort != _animals.Sort)
            {
                _animalsDrawnSort = _animals.Sort;
                for (int i = 0; i < _animalsHeadings.Length; i++)
                {
                    bool sorted = _animalsHeadings[i].Sort == _animals.Sort;
                    _animalsHeadings[i].Label.style.color = sorted ? HudTokens.Accent : HudTokens.TextDim;
                    _animalsHeadings[i].Mark.style.display = sorted ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            if (_animalsDrawnPage != _animals.Page || _animalsDrawnPageCount != _animals.PageCount)
            {
                _animalsDrawnPage = _animals.Page;
                _animalsDrawnPageCount = _animals.PageCount;
                _animalsPager.style.display = _animals.PageCount > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                _animalsPageLabel.text = (_animals.Page + 1).ToString(CultureInfo.InvariantCulture)
                    + " / " + _animals.PageCount.ToString(CultureInfo.InvariantCulture);
                _animalsPrevGlyph.Tint = _animals.Page > 0 ? HudTokens.TextPrimary : HudTokens.TextDim;
                _animalsNextGlyph.Tint = _animals.Page < _animals.PageCount - 1 ? HudTokens.TextPrimary : HudTokens.TextDim;
            }
        }

        /// <summary>One word and one figure per kind, rebuilt only when the number of kinds on the board changes.</summary>
        void RefreshAnimalsCounts()
        {
            if (_animalsCountsFor != _animals.Counts.Count)
            {
                _animalsCountsFor = _animals.Counts.Count;
                _animalsCounts.Clear();
                _animalsCountLabels.Clear();
                for (int i = 0; i < _animals.Counts.Count; i++)
                {
                    var entry = new VisualElement();
                    entry.style.flexDirection = FlexDirection.Row;
                    entry.style.alignItems = Align.Center;
                    entry.style.marginRight = AnimalsLayout.CountGap;
                    Label word = HudText.Make(string.Empty, HudTextRole.Meta);
                    word.style.color = HudTokens.TextMeta;
                    Label figure = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
                    figure.style.color = HudTokens.TextPrimary;
                    figure.style.marginLeft = AnimalsLayout.CountFigureGap;
                    entry.Add(word);
                    entry.Add(figure);
                    _animalsCounts.Add(entry);
                    _animalsCountLabels.Add((word, figure));
                }
                _animalsCounts.style.display = _animals.Counts.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            for (int i = 0; i < _animalsCountLabels.Count; i++)
            {
                AnimalCount count = _animals.Counts[i];
                _animalsCountLabels[i].Word.text = Registry.Label(count.KindKey);
                _animalsCountLabels[i].Figure.text = count.Count.ToString(CultureInfo.InvariantCulture);
            }
        }

        void PaintAnimalRow(AnimalRowView view)
        {
            bool on = view.Selected;
            view.Root.style.backgroundColor = on ? HudTokens.Accent : new Color(0f, 0f, 0f, 0f);
            Color ink = on ? HudTokens.OnAccent : HudTokens.TextPrimary;
            view.Kind.style.color = ink;
            view.Doing.style.color = ink;
            view.Portrait.style.backgroundColor = on ? HudTokens.OnAccent : HudTokens.PanelBorder;
            view.DoingSquare.style.backgroundColor = on ? HudTokens.OnAccent : HudTokens.TextDim;
        }

        /// <summary>Selection and camera, and the depth left alone. The selection then closes the tab and the pane shows the animal.</summary>
        void OnAnimalRowClicked(AnimalRowView view)
        {
            if (_directors == null || !view.Id.IsValid) return;
            var world = _boot?.World;
            if (world == null) return;
            _directors.ChooseAnimal(view.Id, world.Views.Current);
        }
    }
}
