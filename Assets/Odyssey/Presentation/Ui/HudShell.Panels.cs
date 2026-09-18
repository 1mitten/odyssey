#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// A modal and the scrim beneath it, which are one thing as far as anything outside is
    /// concerned.
    ///
    /// <para>A pair rather than two fields on the shell because the failure mode is specific and
    /// silent: a scrim left showing over a hidden panel is a screen that swallows every click,
    /// shows nothing, and cannot be dismissed. <see cref="Show"/> is the only thing that moves
    /// either element, so the two cannot get out of step.</para>
    /// </summary>
    public sealed class HudModal
    {
        /// <summary>The wash over the viewport. Pickable, which is what swallows the pointer.</summary>
        public readonly VisualElement Scrim;

        /// <summary>The window itself.</summary>
        public readonly VisualElement Panel;

        internal HudModal(VisualElement scrim, VisualElement panel)
        {
            Scrim = scrim;
            Panel = panel;
        }

        public bool Showing => Panel.style.display == DisplayStyle.Flex;

        /// <summary>Show or hide both halves at once.</summary>
        public void Show(bool on)
        {
            DisplayStyle display = on ? DisplayStyle.Flex : DisplayStyle.None;
            Scrim.style.display = display;
            Panel.style.display = display;
        }

        /// <summary>
        /// Take the panel away and leave the scrim, for when another window stands in its place.
        ///
        /// <para>The one case where the two halves legitimately differ, and it is not the failure
        /// this class exists to prevent: that one is a scrim with <i>nothing</i> over it, which
        /// eats every click and shows no reason why. Here the state is still modal and something is
        /// still on screen — the start screen's Options row puts the settings panel where the menu
        /// was, rather than on top of it (owner, 2026-09-17).</para>
        /// </summary>
        public void ShowScrimOnly()
        {
            Scrim.style.display = DisplayStyle.Flex;
            Panel.style.display = DisplayStyle.None;
        }
    }


    /// <summary>
    /// <see cref="HudShell"/>: the shared furniture and the panels down the left and right.
    ///
    /// <para>The <c>Panel</c>/<c>Header</c>/<c>Scrim</c> builders every region uses, then A1
    /// stores, A2 the colonist strip, and the right-hand column (clock, speed, A6 alerts). Split
    /// out of the shell on 2026-09-16 because the one file had reached 1,947 lines; it is the same
    /// class and the same behaviour.</para>
    ///
    /// <para>Contrast comes from the two always-on scrims, not from the panels — which is what
    /// lets the panels stay light enough to sit at 11% of the viewport. See
    /// <c>14-hud-layout.md</c>.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        // ============================================================ shared furniture

        /// <summary>
        /// A panel: the fill, the hairline, the radius and the padding, anchored by the caller.
        /// Every framed region on the screen is one of these, and <c>HudSmokeTests</c> counts
        /// them by name.
        /// </summary>
        static VisualElement Panel(string name, params string[] extraClasses)
        {
            var panel = new VisualElement { name = name };
            panel.AddToClassList("panel");
            panel.AddToClassList("region");   // the smoke test's name for a framed region
            foreach (string extra in extraClasses) panel.AddToClassList(extra);
            return panel;
        }

        /// <summary>A panel's label row: the 11 px tracked capitals, and whatever sits opposite.</summary>
        static VisualElement Header(VisualElement panel, string label, out Label title)
        {
            var row = new VisualElement();
            row.AddToClassList("panel__hdr");
            title = HudText.Make(label, HudTextRole.PanelLabel, ussClass: "panel__label");
            row.Add(title);
            panel.Add(row);
            return row;
        }

        /// <summary>
        /// The close button every window carries in its top right (owner, 2026-09-17: "all windows
        /// can be escaped but also should have an X in the top right … like the one used in the
        /// tile selection").
        ///
        /// <para>It is the inspect pane's own control, lifted out rather than reinvented: the
        /// same glyph, the same 26 px box, the same red hairline on hover. A second close button
        /// that looked slightly different would be the interface disagreeing with itself about
        /// what closing means.</para>
        /// </summary>
        static VisualElement CloseButton(VisualElement header, string what, Action onClose)
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            var close = new VisualElement();
            close.AddToClassList("inspect__close");
            close.AddToClassList("panel__close");
            close.Add(new HudGlyph(HudGlyphKind.Close, 14f, HudTokens.TextDim));
            close.tooltip = "Close " + what + " — Esc";
            close.RegisterCallback<ClickEvent>(_ => onClose());
            header.Add(close);
            return close;
        }

        /// <summary>
        /// A panel the player opened and is looking at, as against a board panel read while
        /// watching the world: the opaque fill and the close X, which are the two halves of the
        /// owner's rule. Every window is built through here, so "consistent" is a property of the
        /// code rather than a convention three call sites have to remember.
        /// </summary>
        /// <param name="onClose">
        /// What the X does, or <c>null</c> for a window with no way out of its own — which is only
        /// ever the start screen's root, because a window with nothing behind it has nothing to
        /// close <i>to</i>, and an X that does nothing is worse than no X at all.
        /// </param>
        VisualElement Window(string name, string label, Action? onClose, params string[] extraClasses)
        {
            var panel = Panel(name, extraClasses);
            panel.AddToClassList("window");
            VisualElement header = Header(panel, label, out _);
            if (onClose != null) CloseButton(header, label, onClose);
            panel.style.display = DisplayStyle.None;
            return panel;
        }

        /// <summary>
        /// A modal: a window with a scrim under it, the two shown and hidden as one thing.
        ///
        /// <para><b>The fourth link in this chain, and the first real modal in the project.</b>
        /// <c>09-ui-and-input.md</c> §6 case 5 — "a modal swallows every pointer and key event
        /// except its own dismissal" — has been specified since the interface was designed and has
        /// never had anything to be true of. The settings panel is deliberately not one: you open
        /// it to watch the board change as a lever moves.</para>
        ///
        /// <para><b>The swallow needs no code.</b> The scrim is a pickable element covering the
        /// viewport, under the panel and over everything else, so
        /// <see cref="HudShell.PointOverUi"/> — which asks the panel what is under the cursor —
        /// answers "the interface" everywhere while a modal is up, and the camera rig already
        /// declines a press it is told belongs to the interface. The two always-on scrims are
        /// explicitly <i>not</i> pickable for exactly this reason; this one is pickable for exactly
        /// this reason. That is the whole mechanism, and it is worth stating because the obvious
        /// alternative — a flag consulted in every input path — is the version that grows a case
        /// somebody forgets.</para>
        ///
        /// <para>Returned as a pair rather than as a panel, because a scrim left showing over a
        /// hidden panel is a screen the player cannot dismiss and cannot see the cause of.
        /// <see cref="HudModal.Show"/> is the only thing that moves either of them.</para>
        /// </summary>
        HudModal Modal(string name, string label, Action? onClose, params string[] extraClasses)
        {
            var scrim = new VisualElement { name = name + "-scrim" };
            scrim.AddToClassList("modal-scrim");
            scrim.style.display = DisplayStyle.None;
            _hud.Add(scrim);

            VisualElement panel = Window(name, label, onClose, extraClasses);
            _hud.Add(panel);

            return new HudModal(scrim, panel);
        }

        /// <summary>
        /// A panel raised from the command bar: the one rule, in one place.
        ///
        /// <para>Anchored to the button that raised it, flush on the bar with no gap, less
        /// transparent than a board panel, an X in the header and closed by Escape.</para>
        /// </summary>
        VisualElement Popover(string name, string label, Action onClose, params string[] extraClasses)
        {
            VisualElement panel = Window(name, label, onClose, extraClasses);
            panel.AddToClassList("popover");
            return panel;
        }

        /// <summary>
        /// Shut every panel standing over the board that the player did not just ask for.
        ///
        /// <para><b>One method rather than a list at each call site</b> (owner, 2026-09-18: <i>"if
        /// I'm in the build menu (or any other menu) and I click on an order"</i> — the parenthesis
        /// is the requirement). A rule written about the Build palette alone would have been right
        /// about the panel that happened to be open when the owner noticed, and wrong about Menu
        /// and the bed picker on the same day.</para>
        ///
        /// <para><b>The modals are not in it, and do not need to be.</b> Settings, the debug
        /// windows and the rest of <see cref="HudModal"/> put a pickable scrim over the whole
        /// screen, so nothing behind one can be clicked at all — the orders strip included. They
        /// are excluded by construction rather than by omission, which is worth writing down
        /// because the list otherwise looks incomplete.</para>
        ///
        /// <para>Every call is safe when nothing is open: the two popovers check first and the bed
        /// picker may not have been built at all.</para>
        /// </summary>
        void CloseMenusOverTheBoard()
        {
            if (BuildPaletteOpen) SetBuildPalette(false);
            if (MenuOpen) ToggleMenu(false);
            CloseBedPicker();
        }

        /// <summary>
        /// Put a popover over the button that raised it.
        ///
        /// <para>Written from code because where it sits is a fact about the bar, and only the
        /// laid-out bar knows where its buttons are: the reflow moves them as items go into Menu,
        /// and the interface scale moves them again. The arithmetic itself is
        /// <see cref="HudLayout.PopoverLeft"/>, in the assembly the fast tier can read.</para>
        /// </summary>
        void PlacePopover(VisualElement popover, VisualElement anchor, bool onTheBar = true)
        {
            float screen = _hud.resolvedStyle.width;
            float width = popover.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 1f) width = popover.worldBound.width;

            Rect button = anchor.worldBound;
            if (!float.IsNaN(width)) popover.style.left = HudLayout.PopoverLeft(button.xMin, width, screen);

            if (onTheBar)
            {
                popover.style.bottom = HudLayout.PopoverBottom;
                return;
            }

            // Raised by a row inside a panel, so it sits on that row rather than on the command
            // bar three hundred pixels below it. See HudLayout.PopoverBottomFor.
            //
            // **Placed only once it has a size, and left alone until then.** An element shown this
            // frame has not been laid out yet, so both of these answer NaN — and a NaN written to
            // `bottom` is not ignored, it drops the popover into the top-left corner of the screen.
            // Measured: picker at (0, 0) against a row at y = 1095, with `bottom` reading NaN. The
            // caller re-places it on GeometryChangedEvent, which is the event that fires when the
            // size it needs finally exists.
            float height = popover.resolvedStyle.height;
            if (float.IsNaN(height) || height <= 1f) height = popover.worldBound.height;
            if (float.IsNaN(height) || height <= 1f) return;

            float panel = _hud.resolvedStyle.height;
            if (float.IsNaN(panel) || panel <= 1f) return;

            popover.style.bottom = HudLayout.PopoverBottomFor(
                button.yMin, button.height, height, panel);
        }

        // ============================================================ scrims

        /// <summary>
        /// The two gradients that carry the HUD's text contrast.
        ///
        /// <para><b>They are the reason the panels could shrink.</b> A panel dark enough to hold
        /// 13 px text over bright terrain has to be nearly opaque, and a screen of nearly opaque
        /// panels is the 31% coverage this rebuild was asked to halve. A scrim costs no panel area
        /// at all: it is a transparent ramp, it is never a pointer target, and with it the fill
        /// can drop to 86% and the panels can stop being walls.</para>
        ///
        /// <para>USS has no gradient property, so each is a one-pixel-wide ramp texture stretched
        /// over its element — one 1x64 texture apiece for the whole HUD, no shader and no pass.</para>
        /// </summary>
        void BuildScrims()
        {
            Color ink = HudTokens.ScrimInk;
            Color clear = new Color(ink.r, ink.g, ink.b, 0f);

            _topRamp = HudTokens.VerticalRamp(clear, new Color(ink.r, ink.g, ink.b, HudTheme.TopScrimAlpha));
            _bottomRamp = HudTokens.VerticalRamp(new Color(ink.r, ink.g, ink.b, HudTheme.BottomScrimAlpha), clear);

            _worldUi.Add(Scrim("scrim-top", _topRamp, top: true, HudTheme.TopScrimHeight));
            _worldUi.Add(Scrim("scrim-bottom", _bottomRamp, top: false, HudTheme.BottomScrimHeight));
        }

        static VisualElement Scrim(string name, Texture2D ramp, bool top, int height)
        {
            // Never a pointer target. A pickable scrim would eat every click in the top 170 and
            // bottom 200 rows of the screen, which is a third of the board, silently.
            var scrim = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            scrim.AddToClassList("scrim");
            scrim.style.height = height;
            if (top) scrim.style.top = 0;
            else scrim.style.bottom = 0;
            scrim.style.backgroundImage = new StyleBackground(ramp);
            return scrim;
        }

        // ============================================================ A1 stores

        void BuildStores()
        {
            _storesPanel = Panel("stores", "stores");
            Header(_storesPanel, "Stores", out _);

            // The header's right-hand side: how many rows have anything in them, and the
            // disclosure that shows the ones that do not.
            VisualElement header = _storesPanel.Q(className: "panel__hdr");
            var right = new VisualElement();
            right.AddToClassList("panel__hdrright");
            _storesCount = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "stores__count");
            _storesChevron = new HudGlyph(HudGlyphKind.ChevronDown, 14f, HudTokens.TextDim);
            right.Add(_storesCount);
            right.Add(_storesChevron);
            header.Add(right);
            header.RegisterCallback<ClickEvent>(_ => ToggleStores());
            header.tooltip = "Show or hide the commodities the colony has none of";

            _storesRows = new VisualElement();
            _storesRows.AddToClassList("stores__rows");
            _storesPanel.Add(_storesRows);
            _worldUi.Add(_storesPanel);
        }

        void ToggleStores()
        {
            _storesExpanded = !_storesExpanded;
            _storesChevron.Kind = _storesExpanded ? HudGlyphKind.ChevronUp : HudGlyphKind.ChevronDown;
            RefreshStores();
        }

        void RefreshStores()
        {
            var world = _boot!.World;
            if (world == null) return;
            _ledger.Refresh(world.Views.Current, Time.unscaledTimeAsDouble);

            while (_storeRows.Count < _ledger.Rows.Count) _storeRows.Add(NewStoreRow());
            while (_storeRows.Count > _ledger.Rows.Count)
            {
                _storeRows[^1].Root.RemoveFromHierarchy();
                _storeRows.RemoveAt(_storeRows.Count - 1);
            }

            for (int i = 0; i < _ledger.Rows.Count; i++)
            {
                LedgerRow model = _ledger.Rows[i];
                StoreRowView view = _storeRows[i];

                if (view.Icon.Key != model.IconKey)
                {
                    view.Icon.SetKey(model.IconKey);
                    HudText.Set(view.Name, model.Name, HudTextRole.Row);
                    view.SteadyTip = model.Real
                        ? model.Name + " — counted from the published frame"
                        : model.Name + " — arrives with the economy (M4)";
                    view.FallingTip = model.Name + " — counted from the published frame, and falling";
                }

                if (view.LastQuantity != model.Quantity)
                {
                    view.LastQuantity = model.Quantity;
                    HudText.Set(view.Value, model.Quantity.ToString(), HudTextRole.Row);
                }

                bool falling = model.Trend == StockTrend.Falling;
                view.Falling.style.display = falling ? DisplayStyle.Flex : DisplayStyle.None;
                view.Root.EnableInClassList("stores__row--falling", falling);
                view.Root.EnableInClassList("stores__row--zero", model.Zero);
                view.Root.tooltip = falling && model.Real ? view.FallingTip : view.SteadyTip;

                // Zero rows are folded away by default. Not deleted: the disclosure is how a
                // player finds out what the economy will eventually hold, and a row that vanishes
                // when its last item is hauled away is a row that reads as a bug.
                view.Root.style.display = model.Zero && !_storesExpanded
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (_storesStocked != _ledger.Stocked || _storesTotal != _ledger.Total)
            {
                _storesStocked = _ledger.Stocked;
                _storesTotal = _ledger.Total;
                HudText.Set(_storesCount, $"{_storesStocked} / {_storesTotal}", HudTextRole.Meta);
            }
        }

        StoreRowView NewStoreRow()
        {
            var row = new VisualElement();
            row.AddToClassList("stores__row");

            // Categorised: stores is one of the two places the spec allows an icon to carry a
            // colour of its own, and it is a stroke colour, never a filled tile behind the value.
            var icon = new IconBadge(string.Empty, IconBadge.RowSize, categorised: true);
            var name = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "stores__name");
            var falling = new HudGlyph(HudGlyphKind.ChevronDown, 12f, HudTokens.Warn);
            falling.AddToClassList("stores__falling");
            var value = HudText.Make(string.Empty, HudTextRole.Row, numeric: true, "stores__value");

            row.Add(icon);
            row.Add(name);
            row.Add(falling);
            row.Add(value);
            _storesRows.Add(row);

            return new StoreRowView
            {
                Root = row, Icon = icon, Name = name, Value = value, Falling = falling,
            };
        }

        // ============================================================ A2 colonist strip

        void BuildStrip()
        {
            // A full-width row that centres its cards, rather than a panel placed at a computed x.
            // Centring is what the layout engine is for, and the model's own centring arithmetic
            // then describes what the engine will do rather than competing with it.
            _strip = new VisualElement { name = "strip", pickingMode = PickingMode.Ignore };
            _strip.AddToClassList("strip");
            _worldUi.Add(_strip);
        }

        void RefreshStrip()
        {
            var world = _boot!.World;
            if (world == null) return;
            _roster.Refresh(world.Views.Current,
                selected: _directors != null ? _directors.Selection.Pawns : (IReadOnlyList<PawnId>)Array.Empty<PawnId>());

            int shown = Math.Min(_roster.Cards.Count, _stripCapacity);

            while (_cards.Count < shown) _cards.Add(NewCard(_cards.Count));
            while (_cards.Count > shown)
            {
                _cards[^1].Root.RemoveFromHierarchy();
                _cards.RemoveAt(_cards.Count - 1);
            }

            for (int i = 0; i < shown; i++)
            {
                RosterCard model = _roster.Cards[i];
                CardView view = _cards[i];

                bool somebodyElse = view.LastId != model.Id;
                if (somebodyElse)
                {
                    view.LastId = model.Id;
                    HudText.Set(view.Name, model.Name, HudTextRole.Row);

                    // Keyed on the id like the name beside it, and for the same reason: a card is
                    // a slot rather than a person, so what changes here is which colonist this
                    // slot is showing. SetFace is a second guard on top of that one.
                    view.Avatar.SetFace(ColonistFace.Of(world.Views.Current, model.Id));
                }

                // **And again whenever the pictures themselves have been thrown away**, which the
                // id cannot tell us: a new colony reuses the same small pawn ids, so starting or
                // loading a game left every slot holding a texture that Clear had destroyed and
                // the bar went blank (owner, 2026-09-18). Asked here rather than inside the id
                // branch because the two questions are different — *who is in this slot* and
                // *does their picture still exist* — and only one of them changes on a load.
                int generation = _boot!.Portraits.Generation;
                if (somebodyElse || view.LastPortraits != generation)
                {
                    view.LastPortraits = generation;
                    view.Avatar.SetPortrait(_boot.Portraits.For(world.Views.Current, model.Id));
                }
                if (view.LastJob != model.JobDef)
                {
                    HudText.Set(view.Job, JobLabels.Label(model.JobDef), HudTextRole.Meta);
                    // A same-key call does nothing, so a colonist moving between two jobs that
                    // read as idle retargets nothing at all.
                    view.JobIcon.SetKey(JobLabels.IconKey(model.JobDef));
                }

                view.Root.EnableInClassList("card--sel", model.Selected);
                view.Ring.style.display = model.Selected ? DisplayStyle.Flex : DisplayStyle.None;

                // The layer is deliberately not on the card any more (spec): the depth rail states
                // it once and the inspect pane states it again for whoever is selected. It stays
                // in the tooltip, where it costs no pixels — and is rebuilt only when it would
                // read differently, because building a string every refresh is what ADR 0003's
                // flip condition F1 forbids.
                if (view.LastJob != model.JobDef || view.LastLayer != model.Layer)
                {
                    view.LastJob = model.JobDef;
                    view.LastLayer = model.Layer;
                    view.Root.tooltip =
                        $"{model.Name} — {JobLabels.Label(model.JobDef)}, layer {model.Layer}. Click to select.";
                }
            }
        }

        CardView NewCard(int index)
        {
            var card = new VisualElement();
            card.AddToClassList("card");

            // The selection ring: UI Toolkit has no box-shadow, so the spec's
            // "0 0 0 1px rgba(111,211,227,.35)" outside the accent border is an inset element.
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("card__ring");
            ring.style.display = DisplayStyle.None;
            card.Add(ring);

            var top = new VisualElement();
            top.AddToClassList("card__top");

            // The colonist's own face (docs/design/20-avatars.md). It replaced a People-coloured
            // tile with the first letter of the name on it — which was honest while nothing could
            // draw a person, and stopped being so the day something could. The letter said less
            // than the name beside it already did.
            var avatar = new AvatarGlyph(HudLayout.CardAvatar);
            avatar.AddToClassList("card__avatar");

            var names = new VisualElement();
            names.AddToClassList("card__names");
            Label name = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "card__name");
            names.Add(name);

            top.Add(avatar);
            top.Add(names);
            card.Add(top);

            // The job goes on its own line under the avatar row, not in the strip beside the
            // avatar. The whole width of the card is what lets it be a full word: an ellipsis is
            // allowed on a colonist's name and on nothing else.
            //
            // The icon leads the word rather than replacing it (owner, 2026-09-17). Uncategorised,
            // because the spec gives a colour of its own only to stores and the command bar; here
            // it takes the ink of the word beside it, which is what makes the line read as one
            // thing. It is not hidden where a key has no art: the slot is always occupied, so the
            // word does not shift sideways as a colonist changes job, and the outlined square
            // says a picture belongs there — which is true, and is the same thing it says
            // everywhere else in the HUD.
            var jobRow = new VisualElement { pickingMode = PickingMode.Ignore };
            jobRow.AddToClassList("card__jobrow");
            var jobIcon = new IconBadge(JobLabels.IconKey(-1), IconBadge.RowSize);
            jobIcon.Inherit(HudTokens.TextDim);
            Label job = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "card__job");
            jobRow.Add(jobIcon);
            jobRow.Add(job);
            card.Add(jobRow);

            // Shift is the strip's toggle, exactly as it is in the world: a shift-press on a card
            // turns it on or off without moving the camera, and while shift is held a drag across
            // cards toggles each one it crosses (A2 "drag-select a range"). A plain press keeps the
            // jump: a card is a way of getting to someone far away.
            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (index >= _roster.Cards.Count || _boot!.World == null) return;
                PawnId id = _roster.Cards[index].Id;
                if (evt.shiftKey)
                {
                    _sweepingRoster = true;
                    _directors?.Selection.Toggle(id);
                }
                else _directors?.ChooseColonist(id, _boot.World.Views.Current);
            });
            card.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!_sweepingRoster || index >= _roster.Cards.Count) return;
                _directors?.Selection.Toggle(_roster.Cards[index].Id);
            });

            _strip.Add(card);
            return new CardView
            {
                Root = card, Ring = ring, Avatar = avatar, Name = name,
                JobIcon = jobIcon, Job = job,
            };
        }

        // ============================================================ A3/A4 clock and speed, A5 alerts

        /// <summary>
        /// The right-hand column: the clock with the speed buttons under it, then the alerts.
        ///
        /// <para><b>This merge is the fix for a specific accident.</b> The clock and the alerts
        /// were two absolutely positioned panels in the same corner with hand-picked tops, and the
        /// clock grew past the 122 px the alerts panel's fixed top allowed it, burying the speed
        /// buttons underneath. In a column, whatever height each region turns out to be, the next
        /// one starts below it — and the spec's instruction is blunter still: never stack two
        /// panels in the same corner again.</para>
        /// </summary>
        void BuildRightColumn()
        {
            var column = new VisualElement { name = "right-column", pickingMode = PickingMode.Ignore };
            column.AddToClassList("column-right");
            _worldUi.Add(column);

            // ---- clock and speed, one panel
            VisualElement clock = Panel("clock", "clock");

            var line = new VisualElement();
            line.AddToClassList("clock__line");
            _clockTime = HudText.Make(string.Empty, HudTextRole.Clock, numeric: true, "clock__time");
            _clockDate = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "clock__date");
            line.Add(_clockTime);
            line.Add(_clockDate);
            clock.Add(line);

            var speed = new VisualElement();
            speed.AddToClassList("speed");
            (HudGlyphKind glyph, string name)[] speeds =
            {
                (HudGlyphKind.Pause, "Pause"),
                (HudGlyphKind.Play, "Play"),
                (HudGlyphKind.Forward, "Double speed"),
                (HudGlyphKind.FastForward, "Triple speed"),
            };
            for (int i = 0; i < speeds.Length; i++)
            {
                int requested = i;      // 0 paused, 1..3 speeds — the rig's own convention
                var button = new VisualElement();
                button.AddToClassList("speed__btn");
                var glyph = new HudGlyph(speeds[i].glyph, 14f, HudTokens.TextPrimary);
                glyph.AddToClassList("speed__glyph");
                button.Add(glyph);
                button.tooltip = speeds[i].name + " — Space pauses, 1/2/3 set speed";
                button.RegisterCallback<ClickEvent>(_ => _rig?.RequestGameSpeed(requested));
                speed.Add(button);
                _speedButtons.Add(button);
            }
            clock.Add(speed);
            column.Add(clock);

            // ---- alerts, under it in the same column
            _alertsPanel = Panel("alerts", "alerts");
            VisualElement header = Header(_alertsPanel, "Alerts", out _);
            VisualElement clearAll = CloseButton(header, "alerts", () =>
            {
                _alerts.DismissAll();
                RefreshAlerts();
            });
            clearAll.tooltip = "Clear all alerts";

            _alertRows = new VisualElement();
            _alertRows.AddToClassList("alerts__rows");
            _alertsPanel.Add(_alertRows);

            // Hidden outright when there is nothing to say.
            _alertsPanel.style.display = DisplayStyle.None;
            column.Add(_alertsPanel);
        }

        void RefreshClock()
        {
            var world = _boot!.World;
            if (world == null) return;
            long tick = world.CurrentTick;
            HudText.Set(_clockTime, $"{GameClock.HourOfDay(tick):00}:00", HudTextRole.Clock);
            HudText.Set(_clockDate,
                $"Day {GameClock.DayOfMonth(tick)} · {GameClock.MonthName(tick)} · {GameClock.SeasonName(tick)}",
                HudTextRole.Body);
        }

        void RefreshSpeed()
        {
            var world = _boot!.World;
            if (world == null) return;
            int current = world.GameSpeed;
            for (int i = 0; i < _speedButtons.Count; i++)
            {
                bool on = i == current;
                _speedButtons[i].EnableInClassList("speed__btn--on", on);
                if (_speedButtons[i].Q<HudGlyph>() is { } glyph)
                    glyph.Tint = on ? HudTokens.OnAccent : HudTokens.TextPrimary;
            }
        }

        void RefreshAlerts()
        {
            var world = _boot!.World;
            if (world == null) return;
            _alerts.Refresh(world.Views.Current, Time.unscaledTimeAsDouble);

            while (_alertViews.Count < _alerts.Rows.Count) _alertViews.Add(NewAlertRow());
            while (_alertViews.Count > _alerts.Rows.Count)
            {
                _alertViews[^1].Root.RemoveFromHierarchy();
                _alertViews.RemoveAt(_alertViews.Count - 1);
            }

            for (int i = 0; i < _alerts.Rows.Count; i++)
            {
                AlertRow model = _alerts.Rows[i];
                AlertRowView view = _alertViews[i];
                view.DismissKey = model.DismissKey;
                view.TargetPawn = model.Pawn;
                view.TargetCell = model.Cell;

                Color ink = model.Severity switch
                {
                    AlertSeverity.Danger => HudTokens.Bad,
                    AlertSeverity.Warning => HudTokens.Warn,
                    _ => HudTokens.Accent,
                };
                view.Icon.Kind = model.Severity == AlertSeverity.Notice
                    ? HudGlyphKind.Info
                    : HudGlyphKind.AlertTriangle;
                view.Icon.Tint = ink;

                if (!ReferenceEquals(view.LastLead, model.Lead))
                {
                    view.LastLead = model.Lead;
                    HudText.Set(view.Prefix, model.TargetPrefix, HudTextRole.Body);
                    view.Prefix.style.display = string.IsNullOrEmpty(model.TargetPrefix) ? DisplayStyle.None : DisplayStyle.Flex;

                    HudText.Set(view.Target, model.TargetName, HudTextRole.Body);
                    view.Target.style.color = ink;

                    HudText.Set(view.Message, model.TargetSuffix, HudTextRole.Body);
                    view.Root.tooltip = model.Lead;
                }
            }

            _alertsPanel.style.display =
                _alerts.Rows.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        AlertRowView NewAlertRow()
        {
            var row = new VisualElement();
            row.AddToClassList("alert");

            var icon = new HudGlyph(HudGlyphKind.AlertTriangle, IconBadge.BarSize, HudTokens.Warn);
            icon.AddToClassList("alert__icon");

            var text = new VisualElement();
            text.AddToClassList("alert__text");

            Label prefix = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "alert__prefix");
            Label target = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "alert__target");
            target.style.unityFontStyleAndWeight = FontStyle.Bold;
            Label message = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "alert__message");
            Label lead = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "alert__lead");
            Label detail = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "alert__detail");

            text.Add(prefix);
            text.Add(target);
            text.Add(message);

            var dismiss = new VisualElement();
            dismiss.AddToClassList("alert__dismiss");
            var dismissGlyph = new HudGlyph(HudGlyphKind.Close, 11f, HudTokens.TextDim);
            dismiss.Add(dismissGlyph);
            dismiss.tooltip = "Dismiss alert";

            row.Add(icon);
            row.Add(text);
            row.Add(dismiss);
            _alertRows.Add(row);

            var view = new AlertRowView
            {
                Root = row,
                Icon = icon,
                Text = text,
                Prefix = prefix,
                Target = target,
                Message = message,
                Lead = lead,
                Detail = detail,
                Dismiss = dismiss,
            };

            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                var world = _boot?.World;
                if (world == null) return;
                if (view.TargetPawn.IsValid)
                {
                    _directors?.ChooseColonist(view.TargetPawn, world.Views.Current);
                }
                else if (view.TargetCell.HasValue)
                {
                    _directors?.Slice.SetLayer(view.TargetCell.Value.Y);
                    _directors?.Selection.Pick(view.TargetCell.Value, PawnId.None, world.Views.Current);
                    _directors?.Camera.JumpTo(view.TargetCell.Value);
                }
            });

            dismiss.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            dismiss.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _alerts.Dismiss(view.DismissKey);
                RefreshAlerts();
            });

            return view;
        }
    }
}
