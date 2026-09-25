#nullable enable
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The Inventory tab (design 35): every commodity in the colony's stores, grouped by the
    /// storage pane's six categories, and for the one selected, the stores holding it — each with
    /// a Go that takes the camera there and opens the store's pane.
    ///
    /// <para><b>Built once, rows pooled, and rebuilt only when the stock moves.</b> The model reads
    /// a signature off the frame at the HUD's middle cadence and rebuilds only when it changes, so
    /// an open tab over a quiet colony does no work beyond that walk.</para>
    ///
    /// <para><b>The tab and the inspect pane never show together</b>: opening it clears the
    /// selection, and a selection — Go's included — closes it, so the store's pane that Go opens
    /// has the corner to itself.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly InventoryModel _inventory = new InventoryModel();

        VisualElement _inventoryPanel = null!;
        VisualElement? _inventoryItem;
        Label _inventoryTotal = null!;
        TextField _inventorySearch = null!;
        Label _inventorySearchHint = null!;
        readonly List<InventoryHeading> _inventoryHeadings = new List<InventoryHeading>();
        readonly List<InventoryRowView> _inventoryRows = new List<InventoryRowView>();
        Label _inventoryEmpty = null!;
        VisualElement _inventoryPager = null!;
        Label _inventoryPageLabel = null!;
        HudGlyph _inventoryPrevGlyph = null!;
        HudGlyph _inventoryNextGlyph = null!;

        VisualElement _inventoryDetail = null!;
        IconBadge _inventoryArt = null!;
        HudGlyph _inventoryGlyph = null!;
        Label _inventoryName = null!;
        Label _inventoryMeta = null!;
        Label _inventoryItemTotal = null!;
        readonly List<InventoryPlaceView> _inventoryPlaces = new List<InventoryPlaceView>();
        VisualElement _inventoryPrimary = null!;
        Label _inventoryPrimaryLabel = null!;

        sealed class InventoryHeading
        {
            public Label Label = null!;
            public DrawnTriangle Mark = null!;
            public InventorySort Sort;
        }

        sealed class InventoryRowView
        {
            public VisualElement Root = null!;
            public VisualElement Rail = null!;
            public VisualElement Group = null!;
            public HudGlyph GroupGlyph = null!;
            public Label GroupName = null!;
            public Label GroupCount = null!;
            public VisualElement Item = null!;
            public IconBadge Art = null!;
            public HudGlyph Glyph = null!;
            public Label Name = null!;
            public Label Places = null!;
            public Label Total = null!;
            public int Def = -1;
        }

        sealed class InventoryPlaceView
        {
            public VisualElement Root = null!;
            public VisualElement Rail = null!;
            public Label Name = null!;
            public Label Qty = null!;
            public int Ordinal;
        }

        /// <summary>A small filled triangle pointing down: the sort mark, drawn because no shipped font has one.</summary>
        sealed class DrawnTriangle : VisualElement
        {
            readonly Color _tint;

            public DrawnTriangle(Color tint)
            {
                _tint = tint;
                style.width = InventoryLayout.SortMarkWidth;
                style.height = InventoryLayout.SortMarkHeight;
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
                painter.LineTo(new Vector2(InventoryLayout.SortMarkWidth, 0f));
                painter.LineTo(new Vector2(InventoryLayout.SortMarkWidth / 2f, InventoryLayout.SortMarkHeight));
                painter.ClosePath();
                painter.Fill();
            }
        }

        /// <summary>The search field's magnifier: a circle of radius 3.8 and a diagonal, 1.5 stroke.</summary>
        sealed class DrawnMagnifier : VisualElement
        {
            public DrawnMagnifier()
            {
                style.width = InventoryLayout.SearchGlyph;
                style.height = InventoryLayout.SearchGlyph;
                style.flexShrink = 0;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            static void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                painter.strokeColor = HudTokens.TextDim;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.Arc(new Vector2(5f, 5f), 3.8f, 0f, 360f);
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(7.8f, 7.8f));
                painter.LineTo(new Vector2(11f, 11f));
                painter.Stroke();
            }
        }

        void BuildInventory()
        {
            _inventoryPanel = DockedTab("inventory", Registry.Label(InventoryDirector.PanelKey),
                () => _directors?.Inventory.SetOpen(false), InventoryLayout.Width, InventoryLayout.Height,
                InventoryLayout.HeaderHeight, out VisualElement header);

            // "INVENTORY (926)": the colony's stored total, 6 px after the title. No Esc cap here
            // (the spec's own rule for this tab).
            _inventoryTotal = HudText.Make(string.Empty, HudTextRole.Hotkey, numeric: true);
            _inventoryTotal.style.color = HudTokens.TextMeta;
            _inventoryTotal.style.marginLeft = InventoryLayout.TotalGap;
            header.Insert(1, _inventoryTotal);

            _inventoryPanel.Add(BuildInventoryToolbar());

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.height = InventoryLayout.BodyHeight;
            body.style.flexShrink = 0;
            body.Add(BuildInventoryTable());
            body.Add(BuildInventoryDetail());
            _inventoryPanel.Add(body);

            _hud.Add(_inventoryPanel);
        }

        VisualElement BuildInventoryToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.height = InventoryLayout.ToolbarHeight;
            toolbar.style.flexShrink = 0;
            toolbar.style.paddingLeft = HudLayout.Pad;
            toolbar.style.paddingRight = HudLayout.Pad;
            toolbar.style.borderBottomWidth = HudTheme.BorderWidth;
            toolbar.style.borderBottomColor = HudTokens.Divider;

            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.width = InventoryLayout.SearchWidth;
            box.style.height = InventoryLayout.SearchHeight;
            box.style.paddingLeft = InventoryLayout.SearchInnerPad;
            box.style.paddingRight = InventoryLayout.SearchInnerPad;
            SetBorder(box, HudTheme.BorderWidth, HudTokens.PanelBorder);
            box.Add(new DrawnMagnifier());

            var input = new VisualElement();
            input.style.flexGrow = 1;
            input.style.height = InventoryLayout.SearchHeight - 2 * HudTheme.BorderWidth;
            input.style.marginLeft = InventoryLayout.Gap;
            input.style.justifyContent = Justify.Center;

            _inventorySearch = new TextField();
            _inventorySearch.AddToClassList("inventory__search");
            _inventorySearch.style.position = Position.Absolute;
            _inventorySearch.style.left = 0;
            _inventorySearch.style.right = 0;
            _inventorySearch.style.top = 0;
            _inventorySearch.style.bottom = 0;
            // Strip the field's own chrome, as the Almanac's search does: the box around it is ours.
            _inventorySearch.Query<VisualElement>().ForEach(v =>
            {
                v.style.borderLeftWidth = 0;
                v.style.borderRightWidth = 0;
                v.style.borderTopWidth = 0;
                v.style.borderBottomWidth = 0;
                v.style.backgroundColor = Color.clear;
                v.style.marginTop = 0;
                v.style.marginBottom = 0;
                v.style.marginLeft = 0;
                v.style.marginRight = 0;
                v.style.paddingLeft = 0;
                v.style.paddingRight = 0;
            });
            _inventorySearch.Query<TextElement>().ForEach(e =>
            {
                HudText.Apply(e, HudTextRole.Meta);
                e.style.color = HudTokens.TextPrimary;
            });
            _inventorySearch.RegisterValueChangedCallback(evt =>
            {
                string text = (evt.newValue ?? string.Empty).Trim();
                _inventorySearchHint.style.display = (evt.newValue ?? string.Empty).Length == 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _inventory.SetSearch(text);
                PaintInventory();
            });
            TakesTheKeyboard(_inventorySearch, () =>
            {
                if (!string.IsNullOrEmpty(_inventorySearch.value)) _inventorySearch.value = string.Empty;
            });

            _inventorySearchHint = HudText.Make(Registry.Label(InventoryDirector.SearchKey), HudTextRole.Meta);
            _inventorySearchHint.style.color = HudTokens.TextDim;
            _inventorySearchHint.pickingMode = PickingMode.Ignore;
            input.Add(_inventorySearch);
            input.Add(_inventorySearchHint);
            box.Add(input);
            toolbar.Add(box);
            return toolbar;
        }

        VisualElement BuildInventoryTable()
        {
            var table = new VisualElement();
            table.style.width = InventoryLayout.TableWidth;
            table.style.flexShrink = 0;
            table.style.borderRightWidth = HudTheme.BorderWidth;
            table.style.borderRightColor = HudTokens.PanelBorder;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.height = InventoryLayout.RowHeight;
            head.style.flexShrink = 0;
            head.style.paddingLeft = HudLayout.Pad;
            head.style.paddingRight = HudLayout.Pad;
            head.style.borderBottomWidth = HudTheme.BorderWidth;
            head.style.borderBottomColor = HudTokens.PanelBorder;
            head.Add(SortHeading(InventoryDirector.ItemKey, 0, Justify.FlexStart, InventorySort.Item));
            head.Add(SortHeading(InventoryDirector.PlacesKey, InventoryLayout.PlacesColumn, Justify.FlexEnd, InventorySort.Places));
            head.Add(SortHeading(InventoryDirector.TotalKey, InventoryLayout.TotalColumn, Justify.FlexEnd, InventorySort.Total));
            table.Add(head);

            var rows = new VisualElement();
            rows.style.flexGrow = 1;
            for (int i = 0; i < InventoryLayout.RowsPerPage; i++)
            {
                InventoryRowView view = InventoryRowPool();
                rows.Add(view.Root);
                _inventoryRows.Add(view);
            }

            _inventoryEmpty = HudText.Make(string.Empty, HudTextRole.Meta);
            _inventoryEmpty.style.height = InventoryLayout.RowHeight;
            _inventoryEmpty.style.paddingLeft = HudLayout.Pad;
            _inventoryEmpty.style.unityTextAlign = TextAnchor.MiddleLeft;
            _inventoryEmpty.style.color = HudTokens.TextMeta;
            _inventoryEmpty.style.display = DisplayStyle.None;
            rows.Add(_inventoryEmpty);
            table.Add(rows);

            _inventoryPager = DockedPager(out _inventoryPageLabel, out _inventoryPrevGlyph, out _inventoryNextGlyph,
                () => { _inventory.SetPage(_inventory.Page - 1); PaintInventory(); },
                () => { _inventory.SetPage(_inventory.Page + 1); PaintInventory(); });
            table.Add(_inventoryPager);
            return table;
        }

        VisualElement SortHeading(string key, int width, Justify justify, InventorySort sort)
        {
            var root = new VisualElement();
            root.AddToClassList("inventory__heading");
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = justify;
            root.style.height = InventoryLayout.RowHeight;
            if (width > 0)
            {
                root.style.width = width;
                root.style.flexShrink = 0;
            }
            else root.style.flexGrow = 1;

            var heading = new InventoryHeading { Sort = sort };
            heading.Label = HudText.Make(Registry.Label(key), HudTextRole.PanelLabel);
            heading.Label.style.color = HudTokens.TextDim;
            root.Add(heading.Label);
            heading.Mark = new DrawnTriangle(HudTokens.Accent);
            heading.Mark.style.marginLeft = InventoryLayout.SortMarkGap;
            heading.Mark.style.display = DisplayStyle.None;
            root.Add(heading.Mark);
            root.RegisterCallback<ClickEvent>(_ =>
            {
                if (_inventory.SortBy(sort)) PaintInventory();
            });
            _inventoryHeadings.Add(heading);
            return root;
        }

        InventoryRowView InventoryRowPool()
        {
            var view = new InventoryRowView();
            view.Root = SelectableRow(InventoryLayout.RowHeight, out view.Rail);
            view.Root.AddToClassList("inventory__row");
            view.Root.style.paddingLeft = HudLayout.Pad;
            view.Root.style.paddingRight = HudLayout.Pad;
            view.Root.style.borderBottomWidth = HudTheme.BorderWidth;
            view.Root.style.borderBottomColor = HudTokens.Divider;
            view.Root.style.display = DisplayStyle.None;

            // A category's heading, drawn as the storage pane draws one (owner, 2026-09-23:
            // "uniform for easy identification"): the row washed with the category's hue, its
            // glyph and its name in that hue, and how many kinds it holds. The colour is the second
            // cue; the glyph and the name are the first, which is what makes it safe for a
            // colour-blind player (HudTheme.ItemCategoryHues).
            view.Group = new VisualElement();
            view.Group.style.flexDirection = FlexDirection.Row;
            view.Group.style.alignItems = Align.Center;
            view.Group.style.flexGrow = 1;
            view.GroupGlyph = new HudGlyph(CategoryGlyph(0), InventoryLayout.CategoryGlyph, HudTokens.TextDim);
            view.GroupGlyph.style.marginRight = InventoryLayout.Gap;
            view.Group.Add(view.GroupGlyph);
            view.GroupName = HudText.Make(string.Empty, HudTextRole.ListHeading);
            view.GroupName.style.marginRight = InventoryLayout.Gap;
            view.Group.Add(view.GroupName);
            view.GroupCount = HudText.Make(string.Empty, HudTextRole.Row, numeric: true);
            view.Group.Add(view.GroupCount);
            view.Root.Add(view.Group);

            // An item: indent, its icon, name, places, total. The name stays neutral: the colour
            // marks the group, not every line.
            view.Item = new VisualElement();
            view.Item.style.flexDirection = FlexDirection.Row;
            view.Item.style.alignItems = Align.Center;
            view.Item.style.flexGrow = 1;
            view.Item.Add(IconSlot(InventoryLayout.ItemIcon, InventoryLayout.ItemIndent, out view.Art, out view.Glyph));
            view.Name = Cell(string.Empty, HudTextRole.Row, false, 0, TextAnchor.MiddleLeft);
            view.Item.Add(view.Name);
            view.Places = Cell(string.Empty, HudTextRole.Meta, true, InventoryLayout.PlacesColumn, TextAnchor.MiddleRight);
            view.Places.style.color = HudTokens.TextMeta;
            view.Item.Add(view.Places);
            view.Total = Cell(string.Empty, HudTextRole.Row, true, InventoryLayout.TotalColumn, TextAnchor.MiddleRight);
            view.Total.style.color = HudTokens.TextPrimary;
            view.Item.Add(view.Total);
            view.Root.Add(view.Item);

            InventoryRowView captured = view;
            view.Root.RegisterCallback<ClickEvent>(_ => OnInventoryRowClicked(captured));
            return view;
        }

        VisualElement BuildInventoryDetail()
        {
            _inventoryDetail = new VisualElement();
            _inventoryDetail.style.flexGrow = 1;
            _inventoryDetail.style.flexShrink = 1;
            _inventoryDetail.style.flexDirection = FlexDirection.Column;

            // 1. Identity.
            var identity = new VisualElement();
            identity.style.flexDirection = FlexDirection.Row;
            identity.style.alignItems = Align.Center;
            identity.style.flexShrink = 0;
            identity.style.paddingLeft = HudLayout.Pad;
            identity.style.paddingRight = HudLayout.Pad;
            identity.style.paddingTop = HudLayout.Pad;
            identity.style.paddingBottom = HudLayout.Pad;
            identity.style.borderBottomWidth = HudTheme.BorderWidth;
            identity.style.borderBottomColor = HudTokens.PanelBorder;
            VisualElement detailSlot = IconSlot(InventoryLayout.DetailIcon, 0, out _inventoryArt, out _inventoryGlyph);
            detailSlot.style.marginRight = HudLayout.Pad;
            identity.Add(detailSlot);
            var names = new VisualElement();
            names.style.flexGrow = 1;
            names.style.flexShrink = 1;
            _inventoryName = HudText.Make(string.Empty, HudTextRole.Name);
            _inventoryName.style.color = HudTokens.TextPrimary;
            names.Add(_inventoryName);
            _inventoryMeta = HudText.Make(string.Empty, HudTextRole.Meta);
            _inventoryMeta.style.color = HudTokens.TextMeta;
            names.Add(_inventoryMeta);
            identity.Add(names);
            _inventoryItemTotal = HudText.Make(string.Empty, HudTextRole.Name, numeric: true);
            _inventoryItemTotal.style.color = HudTokens.TextPrimary;
            _inventoryItemTotal.style.unityTextAlign = TextAnchor.MiddleRight;
            identity.Add(_inventoryItemTotal);
            _inventoryDetail.Add(identity);

            // 2. The WHERE table.
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.height = InventoryLayout.RowHeight;
            head.style.flexShrink = 0;
            head.style.paddingLeft = HudLayout.Pad;
            head.style.paddingRight = HudLayout.Pad;
            head.style.borderBottomWidth = HudTheme.BorderWidth;
            head.style.borderBottomColor = HudTokens.PanelBorder;
            head.Add(Heading(InventoryDirector.WhereKey, 0, TextAnchor.MiddleLeft));
            head.Add(Heading(InventoryDirector.QtyKey, InventoryLayout.QtyColumn, TextAnchor.MiddleRight));
            var blank = new VisualElement();
            blank.style.width = InventoryLayout.GoColumn;
            blank.style.flexShrink = 0;
            head.Add(blank);
            _inventoryDetail.Add(head);

            // Pooled for the most places that can show: the pane's height, less the identity band,
            // the heading, the hint and the button, in rows. A colony with more stores holding one
            // commodity than that is shown its largest; the hint says Go reaches any of them.
            for (int i = 0; i < InventoryPlaceRows; i++)
            {
                var view = new InventoryPlaceView();
                view.Root = SelectableRow(InventoryLayout.RowHeight, out view.Rail);
                view.Root.AddToClassList("inventory__place");
                view.Root.style.paddingLeft = HudLayout.Pad;
                view.Root.style.paddingRight = HudLayout.Pad;
                view.Root.style.borderBottomWidth = HudTheme.BorderWidth;
                view.Root.style.borderBottomColor = HudTokens.Divider;
                view.Root.style.display = DisplayStyle.None;
                view.Name = Cell(string.Empty, HudTextRole.Row, false, 0, TextAnchor.MiddleLeft);
                view.Root.Add(view.Name);
                view.Qty = Cell(string.Empty, HudTextRole.Row, true, InventoryLayout.QtyColumn, TextAnchor.MiddleRight);
                view.Qty.style.color = HudTokens.TextPrimary;
                view.Root.Add(view.Qty);

                var goCell = new VisualElement();
                goCell.style.width = InventoryLayout.GoColumn;
                goCell.style.flexShrink = 0;
                goCell.style.alignItems = Align.FlexEnd;
                var go = new VisualElement();
                go.AddToClassList("inventory__go");
                go.style.height = InventoryLayout.GoButtonHeight;
                go.style.paddingLeft = InventoryLayout.GoButtonPad;
                go.style.paddingRight = InventoryLayout.GoButtonPad;
                go.style.justifyContent = Justify.Center;
                SetBorder(go, HudTheme.BorderWidth, HudTokens.PanelBorder);
                Label goLabel = HudText.Make(Registry.Label(InventoryDirector.GoKey), HudTextRole.Meta);
                goLabel.style.color = HudTokens.TextPrimary;
                goLabel.pickingMode = PickingMode.Ignore;
                go.Add(goLabel);
                goCell.Add(go);
                view.Root.Add(goCell);

                InventoryPlaceView captured = view;
                go.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    GoToPlace(captured.Ordinal);
                });
                view.Root.RegisterCallback<ClickEvent>(_ =>
                {
                    _inventory.SelectPlace(captured.Ordinal);
                    PaintInventoryDetail();
                });
                _inventoryDetail.Add(view.Root);
                _inventoryPlaces.Add(view);
            }

            // 3. The hint.
            Label hint = HudText.Make(Registry.Label(InventoryDirector.HintKey), HudTextRole.Body);
            hint.style.color = HudTokens.TextMeta;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.paddingLeft = HudLayout.Pad;
            hint.style.paddingRight = HudLayout.Pad;
            hint.style.paddingTop = HudLayout.Pad;
            hint.style.flexShrink = 0;
            _inventoryDetail.Add(hint);

            // 4. The one action, pinned to the bottom.
            var foot = new VisualElement();
            foot.style.marginTop = StyleKeyword.Auto;
            foot.style.flexShrink = 0;
            foot.style.paddingLeft = HudLayout.Pad;
            foot.style.paddingRight = HudLayout.Pad;
            foot.style.paddingTop = HudLayout.Pad;
            foot.style.paddingBottom = HudLayout.Pad;
            foot.style.borderTopWidth = HudTheme.BorderWidth;
            foot.style.borderTopColor = HudTokens.Divider;
            _inventoryPrimary = TabButton(out _inventoryPrimaryLabel, () => GoToPlace(_inventory.SelectedPlace));
            _inventoryPrimary.AddToClassList("inventory__primary");
            _inventoryPrimary.style.height = InventoryLayout.PrimaryHeight;
            _inventoryPrimary.style.backgroundColor = HudTokens.Convert(HudTheme.ActiveTabFill);
            SetBorder(_inventoryPrimary, HudTheme.BorderWidth, HudTokens.Accent);
            _inventoryPrimaryLabel.style.color = HudTokens.Accent;
            foot.Add(_inventoryPrimary);
            _inventoryDetail.Add(foot);
            return _inventoryDetail;
        }

        /// <summary>
        /// How many places the WHERE table draws: the 420 body less the identity band (12 + 32 + 12
        /// and its rule), the heading row, the hint's two lines and the pinned foot (12 + 30 + 12
        /// and its rule), in 30 px rows, rounded down.
        /// </summary>
        const int InventoryPlaceRows = 6;

        void OnInventoryChanged()
        {
            bool open = _directors != null && _directors.Inventory.Open;
            _inventoryPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _inventoryItem?.EnableInClassList("cmd--on", open);
            if (!open)
            {
                Hotkeys().EndTyping(_inventorySearch);
                return;
            }

            SetBuildPalette(false);
            ToggleMenu(false);
            _directors?.Work.SetOpen(false);
            _directors?.Research.SetOpen(false);
            _directors?.Assign.SetOpen(false);
            _directors?.Animals.SetOpen(false);
            _directors?.Selection.Clear();

            // Opens on the item the colony holds most of, whatever was selected last time.
            _inventory.Invalidate();
            RefreshInventory();
            _inventory.SelectLargest();
            PaintInventory();
        }

        /// <summary>The middle cadence: rebuild only when the stock moved.</summary>
        void RefreshInventory()
        {
            if (_directors == null || !_directors.Inventory.Open) return;
            var world = _boot?.World;
            var content = _boot?.Colony?.Pawns.Content;
            if (world == null || content == null) return;
            if (_inventory.Refresh(world.Views.Current,
                    def => def >= 0 && def < content.Items.Length ? (int)content.Items[def].category : 0))
                PaintInventory();
        }

        void PaintInventory()
        {
            _inventoryTotal.text = "(" + InventoryModel.Figure(_inventory.Total) + ")";

            foreach (InventoryHeading heading in _inventoryHeadings)
            {
                bool sorted = heading.Sort == _inventory.Sort;
                heading.Label.style.color = sorted ? HudTokens.Accent : HudTokens.TextDim;
                heading.Mark.style.display = sorted ? DisplayStyle.Flex : DisplayStyle.None;
            }

            for (int i = 0; i < _inventoryRows.Count; i++)
            {
                InventoryRowView view = _inventoryRows[i];
                if (i >= _inventory.Rows.Count)
                {
                    view.Root.style.display = DisplayStyle.None;
                    view.Def = -1;
                    continue;
                }
                InventoryRow row = _inventory.Rows[i];
                view.Root.style.display = DisplayStyle.Flex;
                Color hue = HudTokens.Convert(HudTheme.ItemCategoryHue(row.Category));
                view.Group.style.display = row.IsGroup ? DisplayStyle.Flex : DisplayStyle.None;
                view.Item.style.display = row.IsGroup ? DisplayStyle.None : DisplayStyle.Flex;

                if (row.IsGroup)
                {
                    view.Def = -1;
                    bool empty = row.Kinds == 0;
                    HudColour raw = HudTheme.ItemCategoryHue(row.Category);
                    Color ink = HudTokens.Convert(empty ? raw.WithAlpha(HudTheme.ItemCategoryEmptyInk) : raw);
                    view.GroupGlyph.Kind = CategoryGlyph(row.Category);
                    view.GroupGlyph.Tint = ink;
                    HudText.Set(view.GroupName, Registry.Label(StorageSettingsModel.CategoryKeys[row.Category]),
                        HudTextRole.ListHeading);
                    view.GroupName.style.color = ink;
                    // An empty category shows its name and nothing else.
                    view.GroupCount.text = empty ? string.Empty : row.Kinds.ToString(CultureInfo.InvariantCulture);
                    PaintSelected(view.Root, view.Rail, false);
                    view.Root.style.backgroundColor = new Color(hue.r, hue.g, hue.b,
                        empty ? HudTheme.ItemCategoryWashEmpty : HudTheme.ItemCategoryWash);
                    continue;
                }

                InventoryItem item = row.Item!;
                bool selected = item.DefIndex == _inventory.SelectedDef;
                view.Def = item.DefIndex;
                PaintIcon(view.Art, view.Glyph, item);
                view.Name.text = item.Name;
                view.Name.style.color = selected ? HudTokens.Accent : HudTokens.TextPrimary;
                view.Places.text = item.Places.Count.ToString(CultureInfo.InvariantCulture);
                view.Total.text = item.Total.ToString(CultureInfo.InvariantCulture);
                PaintSelected(view.Root, view.Rail, selected);
            }

            bool nothing = _inventory.Rows.Count == 0;
            _inventoryEmpty.style.display = nothing ? DisplayStyle.Flex : DisplayStyle.None;
            _inventoryEmpty.text = Registry.Label(_inventory.Searching && _inventory.Items.Count > 0
                ? InventoryDirector.NoMatchKey
                : InventoryDirector.EmptyKey);
            // With nothing stored at all, the six headings still show; the line goes under them.
            if (!_inventory.Searching && _inventory.Items.Count == 0 && _inventory.Rows.Count > 0)
                _inventoryEmpty.style.display = DisplayStyle.Flex;

            PaintPager(_inventoryPager, _inventoryPageLabel, _inventoryPrevGlyph, _inventoryNextGlyph,
                _inventory.Page, _inventory.PageCount);
            PaintInventoryDetail();
        }

        void PaintInventoryDetail()
        {
            InventoryItem? item = _inventory.Selected;
            _inventoryDetail.style.visibility = item == null ? Visibility.Hidden : Visibility.Visible;
            if (item == null) return;

            Color hue = HudTokens.Convert(HudTheme.ItemCategoryHue(item.Category));
            PaintIcon(_inventoryArt, _inventoryGlyph, item);
            _inventoryName.text = item.Name;
            _inventoryMeta.text = InventoryModel.Meta(item);
            _inventoryItemTotal.text = item.Total.ToString(CultureInfo.InvariantCulture);

            InventoryPlace? chosen = _inventory.SelectedPlaceOf;
            for (int i = 0; i < _inventoryPlaces.Count; i++)
            {
                InventoryPlaceView view = _inventoryPlaces[i];
                if (i >= item.Places.Count)
                {
                    view.Root.style.display = DisplayStyle.None;
                    continue;
                }
                InventoryPlace place = item.Places[i];
                bool selected = chosen != null && place.Ordinal == chosen.Ordinal;
                view.Root.style.display = DisplayStyle.Flex;
                view.Ordinal = place.Ordinal;
                view.Name.text = place.Name;
                view.Name.style.color = selected ? HudTokens.Accent : HudTokens.TextPrimary;
                view.Qty.text = place.Quantity.ToString(CultureInfo.InvariantCulture);
                PaintSelected(view.Root, view.Rail, selected);
            }

            _inventoryPrimaryLabel.text = chosen != null ? InventoryModel.GoTo(chosen) : string.Empty;
        }

        /// <summary>
        /// An icon slot holding both answers, one shown: the item's pixel art where it exists, and
        /// its category's glyph in the category's hue where it does not. Both are built once so a
        /// pooled row only switches which one shows.
        /// </summary>
        static VisualElement IconSlot(int size, int marginLeft, out IconBadge art, out HudGlyph glyph)
        {
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.style.width = size;
            slot.style.height = size;
            slot.style.flexShrink = 0;
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;
            if (marginLeft > 0) slot.style.marginLeft = marginLeft;
            slot.style.marginRight = InventoryLayout.Gap;
            art = new IconBadge(string.Empty, size);
            glyph = new HudGlyph(HudGlyphKind.Placeholder, size, HudTokens.TextDim);
            slot.Add(art);
            slot.Add(glyph);
            return slot;
        }

        static void PaintIcon(IconBadge art, HudGlyph glyph, InventoryItem item)
        {
            bool drawn = IconArt.Has(item.Key);
            art.style.display = drawn ? DisplayStyle.Flex : DisplayStyle.None;
            glyph.style.display = drawn ? DisplayStyle.None : DisplayStyle.Flex;
            if (drawn) art.SetKey(item.Key);
            else
            {
                glyph.Kind = CategoryGlyph(item.Category);
                glyph.Tint = HudTokens.Convert(HudTheme.ItemCategoryHue(item.Category));
            }
        }

        void OnInventoryRowClicked(InventoryRowView view)
        {
            if (view.Def < 0) return;
            InventoryPlace? go = _inventory.PressItem(view.Def);
            if (go != null)
            {
                GoToPlace(go.Ordinal);
                return;
            }
            PaintInventory();
        }

        /// <summary>
        /// Go: close the tab, take the camera to the store, select it and open its pane. The
        /// selection is what closes the tab, so this closes it first only to be certain.
        /// </summary>
        void GoToPlace(int ordinal)
        {
            if (_directors == null) return;
            InventoryItem? item = _inventory.Selected;
            if (item == null) return;
            InventoryPlace? place = null;
            foreach (InventoryPlace p in item.Places)
                if (p.Ordinal == ordinal) { place = p; break; }
            if (place == null) return;

            _directors.Inventory.SetOpen(false);
            _directors.ChooseStore(place.Cell);
        }
    }
}
