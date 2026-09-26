#nullable enable
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The Research tab (design 34): a category rail, the category's projects, and the selected
    /// project's detail, under a strip that always says what is being researched.
    ///
    /// <para><b>Built once and repainted on events, never per frame.</b> Nothing about research
    /// moves on its own yet — the mechanism is owed (design 34 §1) — so the tab repaints when it
    /// opens, when a press changes the research, and when the debug menu finishes a project. The
    /// rows are pooled at build, one page's worth.</para>
    ///
    /// <para><b>The tab and the inspect pane never show together</b>, as with the Animals tab:
    /// opening it clears the selection, and a selection closes it.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly ResearchModel _research = new ResearchModel();

        VisualElement _researchPanel = null!;
        VisualElement? _researchItem;

        Label _researchNowName = null!;
        VisualElement _researchNowTrack = null!;
        VisualElement _researchNowFill = null!;
        Label _researchNowPercent = null!;
        Label _researchNowThen = null!;
        Label _researchNowIdle = null!;

        readonly List<ResearchRailView> _researchRail = new List<ResearchRailView>();
        readonly List<ResearchRowView> _researchRows = new List<ResearchRowView>();
        VisualElement _researchPager = null!;
        Label _researchPageLabel = null!;
        HudGlyph _researchPrevGlyph = null!;
        HudGlyph _researchNextGlyph = null!;

        Label _researchName = null!;
        Label _researchMeta = null!;
        Label _researchDescription = null!;
        VisualElement _researchNeeds = null!;
        VisualElement _researchUnlocks = null!;
        VisualElement _researchLeadsTo = null!;
        Label _researchLocked = null!;
        VisualElement _researchPrimary = null!;
        Label _researchPrimaryLabel = null!;
        VisualElement _researchSecondary = null!;
        Label _researchSecondaryLabel = null!;
        ResearchSecondary _researchSecondaryAction;

        sealed class ResearchRailView
        {
            public VisualElement Root = null!;
            public VisualElement Rail = null!;
            public Label Name = null!;
            public Label Count = null!;
            public string Key = string.Empty;
        }

        sealed class ResearchRowView
        {
            public VisualElement Root = null!;
            public VisualElement Rail = null!;
            public Label Name = null!;
            public Label Tag = null!;
            public Label Cost = null!;
            public string Key = string.Empty;
        }

        // ============================================================ shared by the docked tabs

        /// <summary>
        /// A tab window docked on the command bar with the owner's flat frame (designs 34 and 35):
        /// no radius, the panel fill at full strength, no padding of its own — each band inside
        /// carries its own — a header of <paramref name="headerHeight"/> with 12 px sides and a
        /// hairline under it, and the close X drawn 10 px in a 22 px box.
        /// </summary>
        VisualElement DockedTab(string name, string label, System.Action onClose, int width, int height,
            int headerHeight, out VisualElement header)
        {
            VisualElement panel = Window(name, label, onClose, name);
            panel.style.position = Position.Absolute;
            panel.style.left = HudLayout.Edge;
            panel.style.bottom = HudCommands.ItemHeight + HudCommands.BarPad * 2;
            // A UI Toolkit width is a border box, so the 1 px frame is inside both numbers.
            panel.style.width = width;
            panel.style.height = height;
            panel.style.paddingLeft = 0;
            panel.style.paddingRight = 0;
            panel.style.paddingTop = 0;
            panel.style.paddingBottom = 0;
            SetRadius(panel, 0);
            panel.style.backgroundColor = HudTokens.PanelFill;
            panel.style.overflow = Overflow.Hidden;

            header = panel.Q(className: "panel__hdr");
            header.style.height = headerHeight;
            header.style.flexShrink = 0;
            header.style.paddingLeft = HudLayout.Pad;
            header.style.paddingRight = HudLayout.Pad;
            header.style.borderBottomWidth = HudTheme.BorderWidth;
            header.style.borderBottomColor = HudTokens.PanelBorder;
            header.Q<Label>(className: "panel__label").style.color = HudTokens.TextPrimary;

            VisualElement close = header.Q(className: "panel__close");
            close.style.width = ResearchLayout.CloseBox;
            close.style.height = ResearchLayout.CloseBox;
            close.style.marginLeft = ResearchLayout.Gap;
            SetRadius(close, 0);
            close.Clear();
            close.Add(new HudGlyph(HudGlyphKind.Close, ResearchLayout.CloseMark, HudTokens.TextDim));
            return panel;
        }

        static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        /// <summary>A fixed-height row with the selection's 2 px inset rail, drawn as a child so it takes no width.</summary>
        static VisualElement SelectableRow(int height, out VisualElement rail)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = height;
            row.style.flexShrink = 0;
            rail = new VisualElement { pickingMode = PickingMode.Ignore };
            rail.style.position = Position.Absolute;
            rail.style.left = 0;
            rail.style.top = 0;
            rail.style.bottom = 0;
            rail.style.width = ResearchLayout.SelectedRail;
            rail.style.backgroundColor = HudTokens.Accent;
            rail.style.display = DisplayStyle.None;
            row.Add(rail);
            return row;
        }

        static void PaintSelected(VisualElement row, VisualElement rail, bool selected)
        {
            row.style.backgroundColor = selected
                ? HudTokens.Convert(HudTheme.ActiveTabFill)
                : new Color(0f, 0f, 0f, 0f);
            rail.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static Label Cell(string text, HudTextRole role, bool numeric, int width, TextAnchor anchor)
        {
            Label label = HudText.Make(text, role, numeric);
            if (width > 0)
            {
                label.style.width = width;
                label.style.flexShrink = 0;
            }
            else
            {
                label.style.flexGrow = 1;
                label.style.flexShrink = 1;
            }
            label.style.unityTextAlign = anchor;
            label.style.overflow = Overflow.Hidden;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        /// <summary>A 30 px action button: a hairline box and a centred word.</summary>
        static VisualElement TabButton(out Label label, System.Action press)
        {
            var button = new VisualElement();
            button.style.height = ResearchLayout.ButtonHeight;
            button.style.flexShrink = 0;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;
            SetBorder(button, HudTheme.BorderWidth, HudTokens.PanelBorder);
            label = HudText.Make(string.Empty, HudTextRole.Row);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.pickingMode = PickingMode.Ignore;
            button.Add(label);
            button.RegisterCallback<ClickEvent>(evt => { press(); evt.StopPropagation(); });
            return button;
        }

        /// <summary>The Animals tab's pager: two drawn chevrons in 22 px boxes and "1 / 2" between them.</summary>
        static VisualElement DockedPager(out Label page, out HudGlyph prev, out HudGlyph next,
            System.Action back, System.Action forward)
        {
            var pager = new VisualElement();
            pager.style.flexDirection = FlexDirection.Row;
            pager.style.alignItems = Align.Center;
            pager.style.justifyContent = Justify.FlexEnd;
            pager.style.height = ResearchLayout.RowHeight;
            pager.style.flexShrink = 0;
            pager.style.paddingRight = HudLayout.Pad;
            pager.style.display = DisplayStyle.None;

            pager.Add(PagerBox(out prev, HudGlyphKind.ChevronLeft, back));
            page = HudText.Make("1 / 1", HudTextRole.Meta, numeric: true);
            page.style.color = HudTokens.TextPrimary;
            page.style.marginLeft = ResearchLayout.Gap;
            page.style.marginRight = ResearchLayout.Gap;
            pager.Add(page);
            pager.Add(PagerBox(out next, HudGlyphKind.ChevronRight, forward));
            return pager;
        }

        static VisualElement PagerBox(out HudGlyph glyph, HudGlyphKind kind, System.Action press)
        {
            var box = new VisualElement();
            box.style.width = ResearchLayout.PagerButton;
            box.style.height = ResearchLayout.PagerButton;
            box.style.flexShrink = 0;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            SetBorder(box, HudTheme.BorderWidth, HudTokens.PanelBorder);
            glyph = new HudGlyph(kind, 10f, HudTokens.TextPrimary);
            box.Add(glyph);
            box.RegisterCallback<ClickEvent>(evt => { press(); evt.StopPropagation(); });
            return box;
        }

        static void PaintPager(VisualElement pager, Label label, HudGlyph prev, HudGlyph next, int page, int count)
        {
            pager.style.display = count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            label.text = (page + 1).ToString(CultureInfo.InvariantCulture) + " / "
                         + count.ToString(CultureInfo.InvariantCulture);
            prev.Tint = page > 0 ? HudTokens.TextPrimary : HudTokens.TextDim;
            next.Tint = page < count - 1 ? HudTokens.TextPrimary : HudTokens.TextDim;
        }

        /// <summary>A flat placeholder square where a pixel icon will go.</summary>
        static VisualElement Placeholder(int size)
        {
            var square = new VisualElement { pickingMode = PickingMode.Ignore };
            square.style.width = size;
            square.style.height = size;
            square.style.flexShrink = 0;
            square.style.backgroundColor = HudTokens.PanelBorder;
            return square;
        }

        // ============================================================ the Research tab

        void BuildResearch()
        {
            _researchPanel = DockedTab("research", Registry.Label(ResearchDirector.PanelKey),
                () => _directors?.Research.SetOpen(false), ResearchLayout.Width, ResearchLayout.Height,
                ResearchLayout.HeaderHeight, out VisualElement header);

            // No Esc cap beside the X (owner, 2026-09-23), the same as the Inventory tab.

            _researchPanel.Add(BuildResearchNow());

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.height = ResearchLayout.BodyHeight;
            body.style.flexShrink = 0;
            body.Add(BuildResearchRail());
            body.Add(BuildResearchTable());
            body.Add(BuildResearchDetail());
            _researchPanel.Add(body);

            _hud.Add(_researchPanel);
        }

        VisualElement BuildResearchNow()
        {
            var strip = new VisualElement();
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.alignItems = Align.Center;
            strip.style.height = ResearchLayout.NowStripHeight;
            strip.style.flexShrink = 0;
            strip.style.paddingLeft = HudLayout.Pad;
            strip.style.paddingRight = HudLayout.Pad;
            strip.style.borderBottomWidth = HudTheme.BorderWidth;
            strip.style.borderBottomColor = HudTokens.Divider;

            Label now = HudText.Make(Registry.Label(ResearchDirector.NowKey), HudTextRole.PanelLabel);
            now.style.color = HudTokens.TextDim;
            now.style.marginRight = ResearchLayout.ItemGap;
            strip.Add(now);

            _researchNowName = HudText.Make(string.Empty, HudTextRole.Row);
            _researchNowName.style.color = HudTokens.TextPrimary;
            _researchNowName.style.marginRight = ResearchLayout.ItemGap;
            strip.Add(_researchNowName);

            _researchNowTrack = new VisualElement();
            _researchNowTrack.style.width = ResearchLayout.NowBarWidth;
            _researchNowTrack.style.height = ResearchLayout.NowBarHeight;
            _researchNowTrack.style.flexShrink = 0;
            _researchNowTrack.style.backgroundColor = HudTokens.Divider;
            _researchNowTrack.style.marginRight = ResearchLayout.ItemGap;
            _researchNowFill = new VisualElement();
            _researchNowFill.style.height = ResearchLayout.NowBarHeight;
            _researchNowFill.style.backgroundColor = HudTokens.Accent;
            _researchNowTrack.Add(_researchNowFill);
            strip.Add(_researchNowTrack);

            _researchNowPercent = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
            _researchNowPercent.style.color = HudTokens.Accent;
            strip.Add(_researchNowPercent);

            _researchNowIdle = HudText.Make(Registry.Label(ResearchDirector.IdleKey), HudTextRole.Meta);
            _researchNowIdle.style.color = HudTokens.TextMeta;
            strip.Add(_researchNowIdle);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            strip.Add(spacer);

            _researchNowThen = HudText.Make(string.Empty, HudTextRole.Meta);
            _researchNowThen.style.color = HudTokens.TextMeta;
            _researchNowThen.style.unityTextAlign = TextAnchor.MiddleRight;
            strip.Add(_researchNowThen);
            return strip;
        }

        VisualElement BuildResearchRail()
        {
            var rail = new VisualElement();
            rail.style.width = ResearchLayout.RailWidth;
            rail.style.flexShrink = 0;
            rail.style.paddingTop = ResearchLayout.RailPadY;
            rail.style.paddingBottom = ResearchLayout.RailPadY;
            rail.style.borderRightWidth = HudTheme.BorderWidth;
            rail.style.borderRightColor = HudTokens.PanelBorder;

            foreach (string key in ResearchCatalogue.Categories)
            {
                var view = new ResearchRailView { Key = key };
                view.Root = SelectableRow(ResearchLayout.RowHeight, out view.Rail);
                view.Root.AddToClassList("research__category");
                view.Root.style.paddingLeft = HudLayout.Pad;
                view.Root.style.paddingRight = HudLayout.Pad;

                VisualElement square = Placeholder(ResearchLayout.RailSquare);
                square.style.marginRight = ResearchLayout.Gap;
                view.Root.Add(square);

                view.Name = Cell(Registry.Label(key), HudTextRole.Row, false, 0, TextAnchor.MiddleLeft);
                view.Root.Add(view.Name);
                view.Count = Cell(string.Empty, HudTextRole.Meta, true, 0, TextAnchor.MiddleRight);
                view.Count.style.flexGrow = 0;
                view.Count.style.color = HudTokens.TextDim;
                view.Count.style.marginLeft = ResearchLayout.Gap;
                view.Root.Add(view.Count);

                string captured = key;
                view.Root.RegisterCallback<ClickEvent>(_ =>
                {
                    _research.SelectCategory(captured);
                    RefreshResearch();
                });
                rail.Add(view.Root);
                _researchRail.Add(view);
            }
            return rail;
        }

        VisualElement BuildResearchTable()
        {
            var table = new VisualElement();
            table.style.width = ResearchLayout.TableWidth;
            table.style.flexShrink = 0;
            table.style.borderRightWidth = HudTheme.BorderWidth;
            table.style.borderRightColor = HudTokens.PanelBorder;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.height = ResearchLayout.RowHeight;
            head.style.flexShrink = 0;
            head.style.paddingLeft = HudLayout.Pad;
            head.style.paddingRight = HudLayout.Pad;
            head.style.borderBottomWidth = HudTheme.BorderWidth;
            head.style.borderBottomColor = HudTokens.PanelBorder;
            head.Add(Heading(ResearchDirector.ProjectKey, 0, TextAnchor.MiddleLeft));
            head.Add(Heading(ResearchDirector.StatusKey, ResearchLayout.StatusColumn, TextAnchor.MiddleLeft));
            head.Add(Heading(ResearchDirector.CostKey, ResearchLayout.CostColumn, TextAnchor.MiddleRight));
            table.Add(head);

            var rows = new VisualElement();
            rows.style.flexGrow = 1;
            for (int i = 0; i < ResearchLayout.RowsPerPage; i++)
            {
                var view = new ResearchRowView();
                view.Root = SelectableRow(ResearchLayout.RowHeight, out view.Rail);
                view.Root.AddToClassList("research__row");
                view.Root.style.paddingLeft = HudLayout.Pad;
                view.Root.style.paddingRight = HudLayout.Pad;
                view.Root.style.borderBottomWidth = HudTheme.BorderWidth;
                view.Root.style.borderBottomColor = HudTokens.Divider;
                view.Root.style.display = DisplayStyle.None;

                view.Name = Cell(string.Empty, HudTextRole.Row, false, 0, TextAnchor.MiddleLeft);
                view.Tag = Cell(string.Empty, HudTextRole.Meta, false, ResearchLayout.StatusColumn, TextAnchor.MiddleLeft);
                view.Cost = Cell(string.Empty, HudTextRole.Meta, true, ResearchLayout.CostColumn, TextAnchor.MiddleRight);
                view.Root.Add(view.Name);
                view.Root.Add(view.Tag);
                view.Root.Add(view.Cost);

                ResearchRowView captured = view;
                view.Root.RegisterCallback<ClickEvent>(_ =>
                {
                    if (captured.Key.Length == 0) return;
                    _research.SelectProject(captured.Key);
                    RefreshResearch();
                });
                rows.Add(view.Root);
                _researchRows.Add(view);
            }
            table.Add(rows);

            _researchPager = DockedPager(out _researchPageLabel, out _researchPrevGlyph, out _researchNextGlyph,
                () => { _research.SetPage(_research.Page - 1); RefreshResearch(); },
                () => { _research.SetPage(_research.Page + 1); RefreshResearch(); });
            table.Add(_researchPager);
            return table;
        }

        static Label Heading(string key, int width, TextAnchor anchor)
        {
            Label label = Cell(Registry.Label(key), HudTextRole.PanelLabel, false, width, anchor);
            label.style.color = HudTokens.TextDim;
            return label;
        }

        VisualElement BuildResearchDetail()
        {
            var detail = new VisualElement();
            detail.style.flexGrow = 1;
            detail.style.flexShrink = 1;
            detail.style.flexDirection = FlexDirection.Column;
            detail.style.paddingLeft = HudLayout.Pad;
            detail.style.paddingRight = HudLayout.Pad;
            detail.style.paddingTop = HudLayout.Pad;
            detail.style.paddingBottom = HudLayout.Pad;

            // 1. Identity.
            var identity = new VisualElement();
            identity.style.flexDirection = FlexDirection.Row;
            identity.style.alignItems = Align.Center;
            identity.style.flexShrink = 0;
            VisualElement icon = Placeholder(ResearchLayout.DetailIcon);
            icon.style.marginRight = HudLayout.Pad;
            identity.Add(icon);
            var names = new VisualElement();
            names.style.flexShrink = 1;
            _researchName = HudText.Make(string.Empty, HudTextRole.Name);
            _researchName.style.color = HudTokens.TextPrimary;
            names.Add(_researchName);
            _researchMeta = HudText.Make(string.Empty, HudTextRole.Meta);
            _researchMeta.style.color = HudTokens.TextMeta;
            names.Add(_researchMeta);
            identity.Add(names);
            detail.Add(identity);

            // 2. Description.
            _researchDescription = HudText.Make(string.Empty, HudTextRole.Body);
            _researchDescription.style.color = HudTokens.TextMeta;
            _researchDescription.style.whiteSpace = WhiteSpace.Normal;
            _researchDescription.style.marginTop = HudLayout.Pad;
            _researchDescription.style.flexShrink = 0;
            detail.Add(_researchDescription);

            // 3. The facts.
            var facts = new VisualElement();
            facts.style.marginTop = HudLayout.Pad;
            facts.style.paddingTop = ResearchLayout.Gap;
            facts.style.borderTopWidth = HudTheme.BorderWidth;
            facts.style.borderTopColor = HudTokens.Divider;
            facts.style.flexShrink = 0;
            facts.Add(FactRow(ResearchDirector.NeedsKey, out _researchNeeds, first: true));
            facts.Add(FactRow(ResearchDirector.UnlocksKey, out _researchUnlocks, first: false));
            facts.Add(FactRow(ResearchDirector.LeadsToKey, out _researchLeadsTo, first: false));
            detail.Add(facts);

            // 4. The actions, pinned to the bottom.
            var foot = new VisualElement();
            foot.style.marginTop = StyleKeyword.Auto;
            foot.style.flexShrink = 0;
            _researchLocked = HudText.Make(string.Empty, HudTextRole.Meta);
            _researchLocked.style.color = HudTokens.TextMeta;
            _researchLocked.style.marginBottom = ResearchLayout.Gap;
            foot.Add(_researchLocked);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            _researchPrimary = TabButton(out _researchPrimaryLabel, OnResearchPrimary);
            _researchPrimary.AddToClassList("research__primary");
            _researchPrimary.style.flexGrow = 1;
            buttons.Add(_researchPrimary);
            _researchSecondary = TabButton(out _researchSecondaryLabel, OnResearchSecondary);
            _researchSecondary.AddToClassList("research__secondary");
            _researchSecondary.style.marginLeft = ResearchLayout.Gap;
            _researchSecondary.style.paddingLeft = HudLayout.Pad;
            _researchSecondary.style.paddingRight = HudLayout.Pad;
            _researchSecondaryLabel.style.color = HudTokens.TextPrimary;
            buttons.Add(_researchSecondary);
            foot.Add(buttons);
            detail.Add(foot);
            return detail;
        }

        static VisualElement FactRow(string key, out VisualElement values, bool first)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            if (!first) row.style.marginTop = ResearchLayout.Gap;

            Label label = HudText.Make(Registry.Label(key), HudTextRole.PanelLabel);
            label.style.width = ResearchLayout.FactLabelColumn;
            label.style.flexShrink = 0;
            label.style.marginRight = HudLayout.Pad;
            label.style.color = HudTokens.TextDim;
            row.Add(label);

            values = new VisualElement();
            values.style.flexDirection = FlexDirection.Row;
            values.style.flexWrap = Wrap.Wrap;
            values.style.flexShrink = 1;
            values.style.flexGrow = 1;
            row.Add(values);
            return row;
        }

        void OnResearchChanged()
        {
            bool open = _directors != null && _directors.Research.Open;
            _researchPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _researchItem?.EnableInClassList("cmd--on", open);
            if (!open) return;

            // It docks where the palette, the menu and the other tabs dock; one at a time. And it
            // never shows beside the inspect pane, so whatever was selected is put away.
            SetBuildPalette(false);
            ToggleMenu(false);
            _directors?.Work.SetOpen(false);
            _directors?.Inventory.SetOpen(false);
            _directors?.Animals.SetOpen(false);
            _directors?.Assign.SetOpen(false);
            _directors?.Selection.Clear();
            RefreshResearch();
        }

        void OnResearchStateChanged()
        {
            if (_directors == null) return;
            _research.FollowSelection(_directors.Research);
            RefreshResearch();
        }

        void RefreshResearch()
        {
            if (_directors == null || !_directors.Research.Open) return;
            ResearchDirector research = _directors.Research;
            _research.Refresh(research);

            // The Now strip.
            ResearchProject? current = ResearchCatalogue.Find(research.Current);
            bool busy = current != null;
            _researchNowName.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            _researchNowTrack.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            _researchNowPercent.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            _researchNowIdle.style.display = busy ? DisplayStyle.None : DisplayStyle.Flex;
            if (current != null)
            {
                int percent = research.PercentOf(current);
                _researchNowName.text = Registry.Label(current.Key);
                _researchNowFill.style.width = ResearchLayout.NowBarWidth * percent / 100f;
                _researchNowPercent.text = ResearchModel.Percent(percent);
            }
            _researchNowThen.text = ResearchModel.ThenLine(research);

            // The rail.
            for (int i = 0; i < _researchRail.Count && i < _research.Categories.Count; i++)
            {
                ResearchRailView view = _researchRail[i];
                ResearchCategoryRow row = _research.Categories[i];
                PaintSelected(view.Root, view.Rail, row.Selected);
                view.Name.style.color = row.Selected ? HudTokens.Accent : HudTokens.TextPrimary;
                view.Count.text = row.Done.ToString(CultureInfo.InvariantCulture) + " / "
                                  + row.Total.ToString(CultureInfo.InvariantCulture);
            }

            // The table.
            for (int i = 0; i < _researchRows.Count; i++)
            {
                ResearchRowView view = _researchRows[i];
                if (i >= _research.Rows.Count)
                {
                    view.Root.style.display = DisplayStyle.None;
                    view.Key = string.Empty;
                    continue;
                }
                ResearchRow row = _research.Rows[i];
                view.Root.style.display = DisplayStyle.Flex;
                view.Key = row.Project.Key;
                view.Name.text = Registry.Label(row.Project.Key);
                view.Tag.text = ResearchModel.Tag(row.Status);
                view.Cost.text = ResearchModel.Cost(row.Project);
                PaintSelected(view.Root, view.Rail, row.Selected);

                bool locked = row.Status == ResearchStatus.Locked;
                view.Name.style.color = locked ? HudTokens.TextDim : HudTokens.TextPrimary;
                view.Tag.style.color = row.Status switch
                {
                    ResearchStatus.Done => HudTokens.Good,
                    ResearchStatus.Researching => HudTokens.Accent,
                    ResearchStatus.Available => HudTokens.TextMeta,
                    _ => HudTokens.TextFaint,
                };
                view.Cost.style.color = locked ? HudTokens.TextFaint : HudTokens.TextMeta;
            }
            PaintPager(_researchPager, _researchPageLabel, _researchPrevGlyph, _researchNextGlyph,
                _research.Page, _research.PageCount);

            RefreshResearchDetail(research);
        }

        void RefreshResearchDetail(ResearchDirector research)
        {
            ResearchProject? project = ResearchCatalogue.Find(_research.Project);
            if (project == null)
            {
                _researchName.text = string.Empty;
                _researchMeta.text = string.Empty;
                _researchDescription.text = string.Empty;
                _researchNeeds.Clear();
                _researchUnlocks.Clear();
                _researchLeadsTo.Clear();
                _researchLocked.style.display = DisplayStyle.None;
                _researchPrimary.style.display = DisplayStyle.None;
                _researchSecondary.style.display = DisplayStyle.None;
                return;
            }

            ResearchStatus status = research.StatusOf(project);
            _researchName.text = Registry.Label(project.Key);
            _researchMeta.text = ResearchModel.Meta(project);
            _researchDescription.text = Registry.Describe(project.Key);

            _researchNeeds.Clear();
            for (int i = 0; i < project.Needs.Count; i++)
                AddFact(_researchNeeds, project.Needs[i], i < project.Needs.Count - 1,
                    research.IsDone(project.Needs[i]) ? HudTokens.Good : HudTokens.TextPrimary);
            if (project.Needs.Count == 0) AddNone(_researchNeeds);

            _researchUnlocks.Clear();
            for (int i = 0; i < project.Unlocks.Count; i++)
                AddFact(_researchUnlocks, project.Unlocks[i], i < project.Unlocks.Count - 1, HudTokens.TextPrimary);
            if (project.Unlocks.Count == 0) AddNone(_researchUnlocks);

            _researchLeadsTo.Clear();
            List<ResearchProject> leads = ResearchCatalogue.LeadingFrom(project.Key);
            for (int i = 0; i < leads.Count; i++)
                AddFact(_researchLeadsTo, leads[i].Key, i < leads.Count - 1, HudTokens.TextDim);
            if (leads.Count == 0) AddNone(_researchLeadsTo);

            string lockedLine = ResearchModel.LockedLine(project, research);
            _researchLocked.text = lockedLine;
            _researchLocked.style.display = lockedLine.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // The primary button's four treatments (design 34 §6).
            _researchPrimary.style.display = DisplayStyle.Flex;
            _researchPrimaryLabel.text = ResearchModel.PrimaryLabel(status);
            _researchPrimary.style.backgroundColor = status == ResearchStatus.Researching
                ? HudTokens.Convert(HudTheme.ActiveTabFill)
                : new Color(0f, 0f, 0f, 0f);
            SetBorder(_researchPrimary, HudTheme.BorderWidth, status switch
            {
                ResearchStatus.Researching => HudTokens.Accent,
                ResearchStatus.Locked => HudTokens.Divider,
                _ => HudTokens.PanelBorder,
            });
            _researchPrimaryLabel.style.color = status switch
            {
                ResearchStatus.Available => HudTokens.TextPrimary,
                ResearchStatus.Researching => HudTokens.Accent,
                _ => HudTokens.TextDim,
            };
            _researchPrimary.pickingMode = ResearchModel.PrimaryPressable(status) ? PickingMode.Position : PickingMode.Ignore;

            _researchSecondaryAction = ResearchModel.SecondaryOf(project, research);
            _researchSecondary.style.display = _researchSecondaryAction == ResearchSecondary.None
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _researchSecondaryLabel.text = ResearchModel.SecondaryLabel(_researchSecondaryAction);
        }

        static void AddFact(VisualElement values, string key, bool comma, Color colour)
        {
            Label label = HudText.Make(Registry.Label(key) + (comma ? "," : string.Empty), HudTextRole.Row);
            label.style.color = colour;
            label.style.marginRight = comma ? ResearchLayout.WordSpace : 0;
            values.Add(label);
        }

        static void AddNone(VisualElement values)
        {
            Label label = HudText.Make(Registry.Label(ResearchDirector.NoneKey), HudTextRole.Row);
            label.style.color = HudTokens.TextDim;
            values.Add(label);
        }

        void OnResearchPrimary()
        {
            if (_directors == null || _research.Project == null) return;
            _directors.Research.Start(_research.Project);
        }

        void OnResearchSecondary()
        {
            if (_directors == null || _research.Project == null) return;
            ResearchDirector research = _directors.Research;
            switch (_researchSecondaryAction)
            {
                case ResearchSecondary.Queue: research.Enqueue(_research.Project); break;
                case ResearchSecondary.Unqueue: research.Unqueue(_research.Project); break;
                case ResearchSecondary.Pause: research.Pause(); break;
            }
        }

        /// <summary>The debug menu's <i>Finish research</i>.</summary>
        void FinishResearch() => _directors?.Research.FinishCurrent();
    }
}
