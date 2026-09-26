#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    public sealed partial class HudShell
    {
        VisualElement _almanacPanel = null!;
        VisualElement _almanacRail = null!;
        VisualElement _almanacIndexColumn = null!;
        VisualElement _almanacIndexList = null!;
        ScrollView _almanacDetailPane = null!;
        TextField _almanacSearchInput = null!;
        Label _almanacSearchHint = null!;
        Label _almanacIndexCatLabel = null!;
        VisualElement _almanacBtnBack = null!;
        VisualElement _almanacBtnFwd = null!;
        VisualElement _almanacBtnSearchReset = null!;

        string _almanacSearchQuery = string.Empty;
        readonly List<VisualElement> _almanacCatRows = new();
        readonly List<VisualElement> _almanacIndexRows = new();

        public bool AlmanacOpen => _directors != null && _directors.Almanac.Open;

        void BuildAlmanac()
        {
            _almanacPanel = new VisualElement { name = "almanac-panel" };
            _almanacPanel.AddToClassList("almanac-panel");
            _almanacPanel.AddToClassList("region");

            // Full-bleed placement: left:0, right:0, top:0, bottom:70px (docked above bottom tab bar)
            _almanacPanel.style.position = Position.Absolute;
            _almanacPanel.style.left = 0;
            _almanacPanel.style.right = 0;
            _almanacPanel.style.top = 0;
            _almanacPanel.style.bottom = HudCommands.ItemHeight + HudCommands.BarPad * 2;
            _almanacPanel.style.display = DisplayStyle.None;

            BuildAlmanacHeader();

            var body = new VisualElement { name = "almanac-body" };
            body.AddToClassList("almanac-body");
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;

            // Column 1: Category Rail (250px, fixed)
            _almanacRail = new VisualElement { name = "almanac-rail" };
            _almanacRail.AddToClassList("almanac-rail");
            _almanacRail.style.width = 250;
            _almanacRail.style.flexShrink = 0;
            body.Add(_almanacRail);

            // Column 2: Index (290px, fixed)
            _almanacIndexColumn = new VisualElement { name = "almanac-index" };
            _almanacIndexColumn.AddToClassList("almanac-index");
            _almanacIndexColumn.style.width = 290;
            _almanacIndexColumn.style.flexShrink = 0;

            var indexHdr = new VisualElement();
            indexHdr.AddToClassList("almanac-index__hdr");
            _almanacIndexCatLabel = HudText.Make("INDEX", HudTextRole.PanelLabel, ussClass: "almanac-index__title");
            Label sortHint = HudText.Make("A–Z", HudTextRole.Meta, ussClass: "almanac-index__sort");
            indexHdr.Add(_almanacIndexCatLabel);
            indexHdr.Add(sortHint);
            _almanacIndexColumn.Add(indexHdr);

            var indexScroller = new ScrollView(ScrollViewMode.Vertical);
            indexScroller.AddToClassList("almanac-index__scroller");
            indexScroller.style.flexGrow = 1;
            _almanacIndexList = new VisualElement();
            indexScroller.Add(_almanacIndexList);
            _almanacIndexColumn.Add(indexScroller);

            body.Add(_almanacIndexColumn);

            // Column 3: Detail Pane (fills rest)
            _almanacDetailPane = new ScrollView(ScrollViewMode.Vertical);
            _almanacDetailPane.AddToClassList("almanac-detail");
            _almanacDetailPane.style.flexGrow = 1;
            body.Add(_almanacDetailPane);

            _almanacPanel.Add(body);
            _hud.Add(_almanacPanel);

            RefreshAlmanacCategories();
        }

        void BuildAlmanacHeader()
        {
            var header = new VisualElement { name = "almanac-hdr" };
            header.AddToClassList("almanac-hdr");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            // Header Left: Title and subtitle count
            var left = new VisualElement();
            left.AddToClassList("almanac-hdr__left");
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.Center;

            Label title = HudText.Make("ALMANAC", HudTextRole.Name, ussClass: "almanac-hdr__title");
            left.Add(title);
            header.Add(left);

            // Header Middle: 360px Search Field with its reset control. Both live in one group
            // that is the header's middle element: the header spreads its children with
            // space-between, so a reset added as a fourth child on its own would shift the box.
            var searchGroup = new VisualElement();
            searchGroup.AddToClassList("almanac-search-group");

            var searchWrap = new VisualElement();
            searchWrap.AddToClassList("almanac-search");
            searchWrap.style.width = 360;

            _almanacSearchInput = new TextField();
            _almanacSearchInput.AddToClassList("almanac-search__input");
            // The typed text is drawn by elements nested under the field, which the sheet's rule
            // for the field itself does not reach; the box is light, so they are black (owner,
            // 2026-09-20). Type comes from the scale, as everywhere else — Row (14/500), plus the
            // renderer's bold, because the variable face draws 500 as regular and the owner asked
            // for the query to read heavier than the chrome around it.
            _almanacSearchInput.Query<TextElement>().ForEach(e =>
            {
                HudText.Apply(e, HudTextRole.Row);
                e.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                e.style.color = UnityEngine.Color.black;
            });
            // Strip the field's own chrome: Unity's TextField ships its nested input with a
            // border, background and padding of its own, which drew as grey trim around the
            // query once the outer box took over the visuals (owner, 2026-09-20).
            _almanacSearchInput.Query<VisualElement>().ForEach(v =>
            {
                v.style.borderLeftWidth = 0;
                v.style.borderRightWidth = 0;
                v.style.borderTopWidth = 0;
                v.style.borderBottomWidth = 0;
                v.style.backgroundColor = UnityEngine.Color.clear;
                v.style.marginTop = 0;
                v.style.marginBottom = 0;
                v.style.marginLeft = 0;
                v.style.marginRight = 0;
            });
            _almanacSearchInput.value = string.Empty;
            _almanacSearchInput.RegisterValueChangedCallback(evt =>
            {
                _almanacSearchQuery = (evt.newValue ?? string.Empty).Trim();
                OnSearchQueryChanged();
            });

            TakesTheKeyboard(_almanacSearchInput, () =>
            {
                if (!string.IsNullOrEmpty(_almanacSearchInput.value))
                    _almanacSearchInput.value = string.Empty;
            });

            _almanacSearchHint = HudText.Make("/", HudTextRole.Meta, ussClass: "almanac-search__hint");

            // Clears the query and hands the index back every entry, docked to the right edge of
            // the box on the header's own background. The value assignment fires the field's own
            // change callback, which is what refreshes the results.
            _almanacBtnSearchReset = new VisualElement();
            _almanacBtnSearchReset.AddToClassList("almanac-search-reset");
            _almanacBtnSearchReset.tooltip = "Reset search";
            _almanacBtnSearchReset.Add(new HudGlyph(HudGlyphKind.Close, 14f, HudTokens.TextMeta,
                strokeScale: 1.5f));
            _almanacBtnSearchReset.style.visibility = Visibility.Hidden;
            _almanacBtnSearchReset.RegisterCallback<ClickEvent>(_ =>
                _almanacSearchInput.value = string.Empty);

            searchWrap.Add(_almanacSearchInput);
            searchWrap.Add(_almanacSearchHint);

            searchGroup.Add(searchWrap);
            searchGroup.Add(_almanacBtnSearchReset);
            header.Add(searchGroup);

            // Header Right: History buttons & Close
            var right = new VisualElement();
            right.AddToClassList("almanac-hdr__right");
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;

            _almanacBtnBack = new VisualElement();
            _almanacBtnBack.AddToClassList("almanac-hdr__btn");
            _almanacBtnBack.tooltip = "Back (Alt+Left)";
            _almanacBtnBack.Add(new HudGlyph(HudGlyphKind.ChevronLeft, 14f, HudTokens.TextMeta));
            _almanacBtnBack.RegisterCallback<ClickEvent>(_ => _directors?.Almanac.GoBack());

            _almanacBtnFwd = new VisualElement();
            _almanacBtnFwd.AddToClassList("almanac-hdr__btn");
            _almanacBtnFwd.tooltip = "Forward (Alt+Right)";
            _almanacBtnFwd.Add(new HudGlyph(HudGlyphKind.ChevronRight, 14f, HudTokens.TextMeta));
            _almanacBtnFwd.RegisterCallback<ClickEvent>(_ => _directors?.Almanac.GoForward());

            var closeBtn = new VisualElement();
            closeBtn.AddToClassList("almanac-hdr__btn");
            closeBtn.AddToClassList("almanac-hdr__close");
            closeBtn.tooltip = "Close Almanac — Esc";
            closeBtn.Add(new HudGlyph(HudGlyphKind.Close, 14f, HudTokens.TextMeta));
            closeBtn.RegisterCallback<ClickEvent>(_ => _directors?.Almanac.SetOpen(false));

            right.Add(_almanacBtnBack);
            right.Add(_almanacBtnFwd);
            right.Add(closeBtn);
            header.Add(right);

            _almanacPanel.Add(header);
        }

        public void ToggleAlmanac(bool? open = null)
        {
            if (_directors?.Almanac == null) return;
            bool target = open ?? !_directors.Almanac.Open;
            _directors.Almanac.SetOpen(target);
        }

        void OnAlmanacChanged()
        {
            if (_directors?.Almanac == null) return;
            bool open = _directors.Almanac.Open;
            _almanacPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            if (open)
            {
                CloseMenusOverTheBoard();
                RefreshAlmanacAll();
            }
            else
            {
                Hotkeys().EndTyping(_almanacSearchInput);
            }
        }

        void OnAlmanacNavigated()
        {
            if (!AlmanacOpen) return;
            RefreshAlmanacCategoriesHighlight();
            RefreshAlmanacIndex();
            RefreshAlmanacDetail();
            UpdateHistoryButtons();
        }

        void OnSearchQueryChanged()
        {
            bool hasSearch = !string.IsNullOrEmpty(_almanacSearchQuery);
            _almanacSearchHint.style.display = hasSearch ? DisplayStyle.None : DisplayStyle.Flex;
            _almanacBtnSearchReset.style.visibility = hasSearch ? Visibility.Visible : Visibility.Hidden;
            RefreshAlmanacIndex();
        }

        void UpdateHistoryButtons()
        {
            if (_directors?.Almanac == null) return;
            _almanacBtnBack.SetEnabled(_directors.Almanac.CanGoBack);
            _almanacBtnBack.EnableInClassList("almanac-hdr__btn--off", !_directors.Almanac.CanGoBack);
            _almanacBtnFwd.SetEnabled(_directors.Almanac.CanGoForward);
            _almanacBtnFwd.EnableInClassList("almanac-hdr__btn--off", !_directors.Almanac.CanGoForward);
        }

        void RefreshAlmanacAll()
        {
            RefreshAlmanacCategories();
            RefreshAlmanacIndex();
            RefreshAlmanacDetail();
            UpdateHistoryButtons();
        }

        void RefreshAlmanacCategories()
        {
            _almanacRail.Clear();
            _almanacCatRows.Clear();

            string activeCat = _directors?.Almanac.CurrentCategory ?? "Terrain";

            foreach (AlmanacCategory cat in AlmanacCatalogue.Categories)
            {
                var row = new VisualElement();
                row.AddToClassList("almanac-cat");
                if (string.Equals(cat.Name, activeCat, StringComparison.OrdinalIgnoreCase))
                    row.AddToClassList("almanac-cat--active");

                var icon = new PathGlyph(cat.IconPath, 16f, HudTokens.TextMeta);
                icon.AddToClassList("almanac-cat__icon");

                Label name = HudText.Make(cat.Name, HudTextRole.Body, ussClass: "almanac-cat__name");
                Label count = HudText.Make(cat.Entries.Count.ToString(), HudTextRole.Meta, numeric: true, ussClass: "almanac-cat__count");

                row.Add(icon);
                row.Add(name);
                row.Add(count);

                string catName = cat.Name;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _directors?.Almanac.SelectCategory(catName);
                });

                _almanacCatRows.Add(row);
                _almanacRail.Add(row);
            }
        }

        void RefreshAlmanacCategoriesHighlight()
        {
            string activeCat = _directors?.Almanac.CurrentCategory ?? "Terrain";
            for (int i = 0; i < AlmanacCatalogue.Categories.Count && i < _almanacCatRows.Count; i++)
            {
                bool isActive = string.Equals(AlmanacCatalogue.Categories[i].Name, activeCat, StringComparison.OrdinalIgnoreCase);
                _almanacCatRows[i].EnableInClassList("almanac-cat--active", isActive);
            }
        }

        void RefreshAlmanacIndex()
        {
            _almanacIndexList.Clear();
            _almanacIndexRows.Clear();

            string activeCat = _directors?.Almanac.CurrentCategory ?? "Terrain";
            string activeEntry = _directors?.Almanac.CurrentEntry ?? "Grass";
            bool isSearching = !string.IsNullOrEmpty(_almanacSearchQuery);

            List<AlmanacEntry> entries = new();
            if (isSearching)
            {
                foreach (AlmanacCategory cat in AlmanacCatalogue.Categories)
                    foreach (AlmanacEntry ent in cat.Entries)
                    {
                        if (ent.Name.IndexOf(_almanacSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ent.Summary.IndexOf(_almanacSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ent.Definition.IndexOf(_almanacSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ent.CategoryName.IndexOf(_almanacSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            entries.Add(ent);
                        }
                    }

                _almanacIndexCatLabel.text = $"RESULTS ({entries.Count})";
            }
            else
            {
                _almanacIndexCatLabel.text = activeCat.ToUpperInvariant();
                AlmanacCategory? cat = AlmanacCatalogue.GetCategory(activeCat);
                if (cat != null) entries.AddRange(cat.Entries);
            }

            foreach (AlmanacEntry ent in entries)
            {
                var row = new VisualElement();
                row.AddToClassList("almanac-idx-row");
                bool isSelected = string.Equals(ent.Name, activeEntry, StringComparison.OrdinalIgnoreCase);
                if (isSelected) row.AddToClassList("almanac-idx-row--active");

                // 32px identity square: the owner's art at 32 where the key has it, else the
                // entry's own line icon (ADR 0007: pixel art is shown at 32 and 64 only).
                var sq = new VisualElement();
                sq.AddToClassList("almanac-idx-row__sq");
                sq.Add(AlmanacIconFor(ent, art: 32f, line: 20f));
                row.Add(sq);

                var texts = new VisualElement();
                texts.AddToClassList("almanac-idx-row__texts");
                Label name = HudText.Make(ent.Name, HudTextRole.Body, ussClass: "almanac-idx-row__name");
                Label summary = HudText.Make(ent.Summary, HudTextRole.Meta, ussClass: "almanac-idx-row__summary");
                texts.Add(name);
                texts.Add(summary);
                row.Add(texts);

                string catName = ent.CategoryName;
                string entName = ent.Name;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _directors?.Almanac.SelectEntry(catName, entName);
                });

                _almanacIndexRows.Add(row);
                _almanacIndexList.Add(row);
            }
        }

        void RefreshAlmanacDetail()
        {
            _almanacDetailPane.Clear();
            if (_directors?.Almanac == null) return;

            AlmanacEntry? entry = AlmanacCatalogue.GetEntry(_directors.Almanac.CurrentCategory, _directors.Almanac.CurrentEntry);
            if (entry == null) return;

            // Band 1: Identity Band
            var band1 = new VisualElement();
            band1.AddToClassList("almanac-band1");

            var sq96 = new VisualElement();
            sq96.AddToClassList("almanac-sq96");
            sq96.Add(AlmanacIconFor(entry, art: 64f, line: 56f));
            band1.Add(sq96);

            var idCol = new VisualElement();
            idCol.AddToClassList("almanac-id-col");

            var titleLine = new VisualElement();
            titleLine.AddToClassList("almanac-title-line");
            Label title = HudText.Make(entry.Name, HudTextRole.Name, ussClass: "almanac-detail__title");
            Label typeChip = HudText.Make(entry.TypeChip.ToUpperInvariant(), HudTextRole.Meta, ussClass: "almanac-chip");
            Label natureChip = HudText.Make(entry.NatureChip.ToUpperInvariant(), HudTextRole.Meta, ussClass: "almanac-chip");
            natureChip.AddToClassList("almanac-chip--nature");

            titleLine.Add(title);
            titleLine.Add(typeChip);
            titleLine.Add(natureChip);
            idCol.Add(titleLine);

            Label def = HudText.Make(entry.Definition, HudTextRole.Body, ussClass: "almanac-definition");
            idCol.Add(def);

            Label live = HudText.Make(entry.Source, HudTextRole.Meta, ussClass: "almanac-live");
            idCol.Add(live);

            band1.Add(idCol);

            var actionsCol = new VisualElement();
            actionsCol.AddToClassList("almanac-actions-col");

            // The action is what the entry can actually do (AlmanacAction): find the thing on the
            // map, or open the tab that holds its live half. An entry for something that is not
            // on the map and has no tab of its own has no button, rather than one that closes the
            // Almanac and does nothing.
            if (entry.Action != AlmanacAction.None)
            {
                var actBtn = new VisualElement();
                actBtn.AddToClassList("almanac-action-btn");
                Label actLabel = HudText.Make(entry.PrimaryAction, HudTextRole.Body);
                actBtn.Add(actLabel);
                actBtn.RegisterCallback<ClickEvent>(_ => RunAlmanacAction(entry, actLabel));
                actionsCol.Add(actBtn);
            }

            var pinBtn = new VisualElement();
            pinBtn.AddToClassList("almanac-pin-btn");
            pinBtn.tooltip = "Pin to HUD";
            pinBtn.Add(HudText.Make("Pin", HudTextRole.Meta));
            // Not wired: pinning is not built. Shown disabled rather than hidden so the action
            // column keeps its shape and the button says "not yet" instead of "nothing" (owner,
            // 2026-09-20).
            pinBtn.AddToClassList("almanac-pin-btn--off");
            pinBtn.pickingMode = PickingMode.Ignore;
            actionsCol.Add(pinBtn);

            band1.Add(actionsCol);
            _almanacDetailPane.Add(band1);

            // Bands 2 & 3: Two-Column Section (520px fixed left column for properties, remaining for body)
            var grid = new VisualElement();
            grid.AddToClassList("almanac-grid");

            // Band 2: Properties Column (520px fixed)
            var propsCol = new VisualElement();
            propsCol.AddToClassList("almanac-props-col");
            propsCol.style.width = 520;
            propsCol.style.flexShrink = 0;

            Label propsHdr = HudText.Make("PROPERTIES", HudTextRole.PanelLabel, ussClass: "almanac-sec-label");
            propsCol.Add(propsHdr);

            foreach ((string key, string value) in entry.Properties)
            {
                var pRow = new VisualElement();
                pRow.AddToClassList("almanac-prop-row");

                Label pKey = HudText.Make(key, HudTextRole.Meta, ussClass: "almanac-prop-key");
                pKey.style.width = 150;
                pKey.style.flexShrink = 0;

                Label pVal = HudText.Make(value, HudTextRole.Body, ussClass: "almanac-prop-val");
                pRow.Add(pKey);
                pRow.Add(pVal);
                propsCol.Add(pRow);
            }

            grid.Add(propsCol);

            // Band 3: Body Column (flex-grow: 1)
            var bodyCol = new VisualElement();
            bodyCol.AddToClassList("almanac-body-col");
            bodyCol.style.flexGrow = 1;

            Label bodyHdr = HudText.Make(entry.Body.SectionLabel, HudTextRole.PanelLabel, ussClass: "almanac-sec-label");
            bodyCol.Add(bodyHdr);

            Label bodyText = HudText.Make(entry.Body.Paragraph, HudTextRole.Body, ussClass: "almanac-body-paragraph");
            bodyCol.Add(bodyText);

            // Specific content widgets (Cards, Levels, Specs, Effects)
            if (entry.Body.Cards != null)
            {
                foreach ((string cTitle, string cKind, string cText) in entry.Body.Cards)
                {
                    var card = new VisualElement();
                    card.AddToClassList("almanac-card");
                    card.AddToClassList(cKind == "best" ? "almanac-card--best" : "almanac-card--avoid");

                    Label cardTitle = HudText.Make(cTitle, HudTextRole.PanelLabel, ussClass: "almanac-card__title");
                    Label cardBody = HudText.Make(cText, HudTextRole.Body, ussClass: "almanac-card__text");
                    card.Add(cardTitle);
                    card.Add(cardBody);
                    bodyCol.Add(card);
                }
            }
            else if (entry.Body.Levels != null)
            {
                var ladder = new VisualElement();
                ladder.AddToClassList("almanac-levels");

                for (int i = 0; i < entry.Body.Levels.Count; i++)
                {
                    (string range, string lName, string note) = entry.Body.Levels[i];
                    var lRow = new VisualElement();
                    lRow.AddToClassList("almanac-level-row");

                    Label badge = HudText.Make(range, HudTextRole.Meta, numeric: true, ussClass: "almanac-level-badge");
                    Label nameLbl = HudText.Make(lName, HudTextRole.Body, ussClass: "almanac-level-name");
                    Label noteLbl = HudText.Make(note, HudTextRole.Meta, ussClass: "almanac-level-note");

                    lRow.Add(badge);
                    lRow.Add(nameLbl);
                    lRow.Add(noteLbl);
                    ladder.Add(lRow);
                }
                bodyCol.Add(ladder);
            }
            else if (entry.Body.Specs != null)
            {
                var specs = new VisualElement();
                specs.AddToClassList("almanac-specs");
                foreach ((string sTitle, string sVal, string sDetail) in entry.Body.Specs)
                {
                    var sRow = new VisualElement();
                    sRow.AddToClassList("almanac-spec-row");

                    Label sHdr = HudText.Make(sTitle, HudTextRole.Meta, ussClass: "almanac-spec-hdr");
                    Label sText = HudText.Make(sVal, HudTextRole.Body, ussClass: "almanac-spec-val");
                    Label sSub = HudText.Make(sDetail, HudTextRole.Meta, ussClass: "almanac-spec-detail");

                    sRow.Add(sHdr);
                    sRow.Add(sText);
                    sRow.Add(sSub);
                    specs.Add(sRow);
                }
                bodyCol.Add(specs);
            }
            else if (entry.Body.Effects != null)
            {
                var effs = new VisualElement();
                effs.AddToClassList("almanac-effects");
                foreach ((string eLabel, string eDesc) in entry.Body.Effects)
                {
                    var eRow = new VisualElement();
                    eRow.AddToClassList("almanac-eff-row");

                    Label eHdr = HudText.Make(eLabel, HudTextRole.Name, ussClass: "almanac-eff-title");
                    Label eBody = HudText.Make(eDesc, HudTextRole.Body, ussClass: "almanac-eff-desc");

                    eRow.Add(eHdr);
                    eRow.Add(eBody);
                    effs.Add(eRow);
                }
                bodyCol.Add(effs);
            }

            // Band 4: Related Section pinned to bottom
            var relSec = new VisualElement();
            relSec.AddToClassList("almanac-related");
            relSec.style.marginTop = 24;

            Label relHdr = HudText.Make("RELATED", HudTextRole.PanelLabel, ussClass: "almanac-sec-label");
            relSec.Add(relHdr);

            var chipsWrap = new VisualElement();
            chipsWrap.AddToClassList("almanac-related-chips");
            chipsWrap.style.flexDirection = FlexDirection.Row;
            chipsWrap.style.flexWrap = Wrap.Wrap;

            foreach ((string rTarget, string rReason) in entry.Related)
            {
                // A link names its target by registry key (or by name for an entry that has no
                // key); AlmanacCatalogueTests holds every one of them to an entry, so this never
                // draws a chip that goes nowhere.
                AlmanacEntry? targetEntry = AlmanacCatalogue.Resolve(rTarget);
                if (targetEntry == null) continue;

                var chip = new VisualElement();
                chip.AddToClassList("almanac-rel-chip");

                var thumb = new VisualElement();
                thumb.AddToClassList("almanac-rel-thumb");
                thumb.Add(AlmanacLineIcon(targetEntry, 16f));

                var texts = new VisualElement();
                Label relTitle = HudText.Make(targetEntry.Name, HudTextRole.Body, ussClass: "almanac-rel-name");
                Label relWhy = HudText.Make(rReason, HudTextRole.Meta, ussClass: "almanac-rel-reason");
                texts.Add(relTitle);
                texts.Add(relWhy);

                chip.Add(thumb);
                chip.Add(texts);

                chip.RegisterCallback<ClickEvent>(_ =>
                    _directors?.Almanac.NavigateTo(targetEntry.CategoryName, targetEntry.Name));

                chipsWrap.Add(chip);
            }

            relSec.Add(chipsWrap);
            bodyCol.Add(relSec);

            grid.Add(bodyCol);
            _almanacDetailPane.Add(grid);
        }

        /// <summary>
        /// The entry's picture, drawn by the same <see cref="IconBadge"/> the inspect pane and every
        /// list draw for the same key: the owner's pixel art where there is some, else the key's
        /// line art in <see cref="IconGlyphs"/>. The Almanac holds no picture of its own, so a thing
        /// looks the same on its page as it does when clicked (owner, 2026-09-26). Pixel art is
        /// drawn at 32 or 64 (ADR 0007); line art at the smaller size, inside the same square.
        /// </summary>
        static VisualElement AlmanacIconFor(AlmanacEntry entry, float art, float line)
        {
            var badge = new IconBadge(entry.IconKey, IconArt.Has(entry.IconKey) ? art : line);
            badge.Inherit(HudTokens.TextPrimary);
            return badge;
        }

        static VisualElement AlmanacLineIcon(AlmanacEntry entry, float size)
        {
            var badge = new IconBadge(entry.IconKey, size);
            badge.Inherit(HudTokens.TextMeta);
            return badge;
        }

        void RunAlmanacAction(AlmanacEntry entry, Label label)
        {
            if (_directors == null) return;
            switch (entry.Action)
            {
                case AlmanacAction.FindOnMap:
                    // Only close once something is found: closing and then finding nothing left
                    // the player on the map with no idea why the page went away.
                    if (!FindOnMap(entry)) label.text = "None on this map";
                    return;
                case AlmanacAction.OpenWork:
                    ToggleAlmanac(false);
                    _directors.Work.SetOpen(true);
                    return;
                case AlmanacAction.OpenAnimals:
                    ToggleAlmanac(false);
                    _directors.Animals.SetOpen(true);
                    return;
                case AlmanacAction.OpenInventory:
                    ToggleAlmanac(false);
                    _directors.Inventory.SetOpen(true);
                    return;
                case AlmanacAction.OpenAssign:
                    ToggleAlmanac(false);
                    _directors.Assign.SetOpen(true);
                    return;
                case AlmanacAction.OpenBuild:
                    ToggleAlmanac(false);
                    SetBuildPalette(true);
                    return;
            }
        }

        /// <summary>
        /// Finds an instance of the entry's thing by its registry key — the same key the pane, the
        /// palette and the Inventory name it by — and selects it. Returns false, leaving the
        /// Almanac open, when the map has none.
        /// </summary>
        bool FindOnMap(AlmanacEntry entry)
        {
            if (_directors == null || _boot?.World == null || entry.Key.Length == 0) return false;
            WorldSnapshot snapshot = _boot.World.Views.Current;
            WorldRenderModel? model = _boot.Model;
            string key = entry.Key;

            CellRef? found = null;
            PawnId pawn = PawnId.None;

            // Animals and people: the first of the kind.
            for (int i = 0; i < snapshot.PawnCount && found == null; i++)
                if (PawnKindLabels.IconKey(snapshot.Pawns[i].Kind) == key)
                {
                    found = snapshot.Pawns[i].Cell;
                    pawn = snapshot.Pawns[i].Id;
                }

            // Loose things: a pile on the ground (a thing in a shelf or a hand has no cell).
            for (int i = 0; i < snapshot.ThingCount && found == null; i++)
            {
                ThingView tv = snapshot.Things[i];
                if (ItemLabels.IconKey(tv.DefIndex) == key && tv.Cell.Y >= 0) found = tv.Cell;
            }

            // Crops in a growing zone.
            for (int i = 0; i < snapshot.PlantCount && found == null; i++)
                if (BuildLabels.PlantKey(snapshot.Plants[i].Plant) == key)
                    found = snapshot.Size.FromIndex(snapshot.Plants[i].CellIndex);

            // Building sites waiting for work.
            for (int i = 0; i < snapshot.SiteCount && found == null; i++)
                if (BuildLabels.BuildingKey(snapshot.Sites[i].Building) == key)
                    found = snapshot.Size.FromIndex(snapshot.Sites[i].CellIndex);

            // Standing things and the ground itself, from the render mirror.
            if (found == null && model != null)
                found = FindInModel(model, key, _directors.Slice.ActiveLayer);

            if (found == null) return false;

            CellRef cell = found.Value;
            ToggleAlmanac(false);
            _directors.Slice.SetLayer(cell.Y);
            _directors.Selection.Pick(cell, pawn, snapshot);
            _directors.Camera.JumpTo(cell);
            return true;
        }

        /// <summary>
        /// The first cell, walking out from the layer the player is on, whose edifice or terrain is
        /// named by <paramref name="key"/>.
        /// </summary>
        static CellRef? FindInModel(WorldRenderModel model, string key, int preferredY)
        {
            GridSize size = model.Size;
            for (int step = 0; step < size.SizeY; step++)
            {
                int y = (preferredY + step) % size.SizeY;
                for (int z = 0; z < size.SizeZ; z++)
                    for (int x = 0; x < size.SizeX; x++)
                    {
                        int idx = size.Index(x, z, y);
                        ushort ed = model.EdificeDef(idx);
                        if (ed != 0 && EdificeLabels.IconKey(ed) == key) return new CellRef(x, z, y);
                        ushort t = model.Terrain(idx);
                        if (t != 0 && TerrainLabels.IconKey(t) == key) return new CellRef(x, z, y);
                    }
            }
            return null;
        }
    }
}
