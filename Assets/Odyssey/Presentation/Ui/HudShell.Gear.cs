#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the Gear tab (design 47), drawn to Claude Design's specification of
    /// 2026-09-25 — a paper doll of six slot tiles round a figure, a kit row under it, a footer of
    /// effects and the loadout — and its three popovers beside the pane.
    ///
    /// <para><c>HudShell.Inspect</c> calls the four methods the Health tab has (build into the
    /// fixed-height box, forget on rebuild, show with the strip, sync fifteen times a second).
    /// <b>Every word, number and state is <see cref="GearModel"/>'s</b>, tested in the fast tier;
    /// every measurement is <see cref="GearLayout"/>'s. This file draws them, and rewrites the tab
    /// only when <see cref="GearModel.Version"/> moves.</para>
    ///
    /// <para><b>What reaches the world</b>: only the hand. Unequip and Drop send
    /// <see cref="IntentKind.OrderUnequip"/>, and a weapon chosen from Pick from stores sends the
    /// right-click menu's own <see cref="IntentKind.OrderEquip"/>. Every other button edits the
    /// <see cref="GearPreview"/>, which is interface state and nothing else.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        enum GearPopoverKind { None, Item, Kit, Pick, Loadout }

        sealed class GearSlotView
        {
            public GearSlot Slot;
            public VisualElement Root = null!;
            public VisualElement Tile = null!;
            public DashedOutline Dashes = null!;
            public IconBadge Icon = null!;
            public Label Name = null!;
            public Label SlotWord = null!;
            public Label Quality = null!;
            public Label Carry = null!;
        }

        sealed class GearKitView
        {
            public int Index;
            public VisualElement Tile = null!;
            public DashedOutline Dashes = null!;
            public IconBadge Icon = null!;
            public VisualElement Badge = null!;
            public Label Count = null!;
            public PathGlyph Lock = null!;
        }

        VisualElement? _gearBody;
        readonly List<GearSlotView> _gearSlots = new List<GearSlotView>();
        readonly List<GearKitView> _gearKit = new List<GearKitView>();
        readonly List<(Label Label, Label Value)> _gearEffects = new List<(Label, Label)>();
        Label? _gearKitHint;
        VisualElement? _gearLoadoutCell;
        Label? _gearLoadoutValue;
        int _gearVersionShown = int.MinValue;

        VisualElement? _gearPopover;
        GearPopoverKind _gearPopoverKind;
        GearSlot _gearPopoverSlot;
        int _gearPopoverKit = -1;
        VisualElement? _gearPopoverAnchor;
        readonly GearPickModel _gearPick = new GearPickModel();

        /// <summary>The debug menu's preview, which the inspect model's gear model reads.</summary>
        GearPreview _gearPreview => _inspect.GearPreview;

        /// <summary>Is one of the Gear tab's popovers open? Escape's second rung (design 47 §3).</summary>
        public bool GearPopoverOpen => _gearPopoverKind != GearPopoverKind.None;

        // Ink(HudColour) is HudShell.Settings's, shared across the partials.

        static void Border(VisualElement e, float width, Color colour)
        {
            e.style.borderTopWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderTopColor = colour;
            e.style.borderRightColor = colour;
            e.style.borderBottomColor = colour;
            e.style.borderLeftColor = colour;
        }

        static void Flat(VisualElement e)
        {
            e.style.borderTopLeftRadius = 0;
            e.style.borderTopRightRadius = 0;
            e.style.borderBottomLeftRadius = 0;
            e.style.borderBottomRightRadius = 0;
        }

        static Label GearText(string text, HudTextRole role, Color colour, bool numeric = false)
        {
            Label label = HudText.Make(text, role, numeric);
            label.style.color = colour;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            return label;
        }

        // ================================================================ the tab

        /// <summary>Build the Gear tab's body into <paramref name="tabBody"/>, hidden until the tab is shown.</summary>
        void BuildGearTab(VisualElement tabBody)
        {
            _gearSlots.Clear();
            _gearKit.Clear();
            _gearEffects.Clear();
            _gearVersionShown = int.MinValue;

            var body = new VisualElement();
            body.style.display = DisplayStyle.None;
            body.style.flexDirection = FlexDirection.Column;
            body.style.height = GearLayout.TabBody;
            body.style.paddingTop = GearLayout.Pad;

            // ---- the doll: 1fr 96 1fr
            var doll = new VisualElement();
            doll.style.flexDirection = FlexDirection.Row;
            doll.style.height = GearLayout.DollHeight;

            var left = Column();
            var right = Column();
            var figure = new VisualElement { pickingMode = PickingMode.Ignore };
            figure.style.width = GearLayout.FigureWidth;
            figure.style.height = GearLayout.DollHeight;
            figure.style.flexShrink = 0;
            figure.style.marginLeft = GearLayout.DollColumnGap;
            figure.style.marginRight = GearLayout.DollColumnGap;
            figure.style.backgroundColor = Ink(HudTheme.RowRule);
            Border(figure, HudTheme.BorderWidth, Ink(HudTheme.PanelBorder));

            for (int i = 0; i < GearModel.Order.Length; i++)
            {
                bool onTheLeft = i < GearLayout.SlotRows;
                GearSlotView view = SlotView(GearModel.Order[i], onTheLeft);
                if (i % GearLayout.SlotRows != 0) view.Root.style.marginTop = GearLayout.SlotGap;
                (onTheLeft ? left : right).Add(view.Root);
                _gearSlots.Add(view);
            }

            doll.Add(left);
            doll.Add(figure);
            doll.Add(right);
            body.Add(doll);

            // ---- the kit row
            var kit = new VisualElement();
            kit.style.flexDirection = FlexDirection.Row;
            kit.style.alignItems = Align.Center;
            kit.style.height = GearLayout.KitRow;
            kit.style.marginTop = GearLayout.SectionGap;

            Label kitLabel = GearText(Registry.Label(GearModel.KitKey), HudTextRole.PanelLabel, HudTokens.TextDim);
            kitLabel.style.width = GearLayout.KitLabelWidth;
            kit.Add(kitLabel);
            for (int i = 0; i < GearLayout.KitSlots; i++)
            {
                GearKitView view = KitView(i);
                view.Tile.style.marginLeft = i == 0 ? 0 : GearLayout.KitGap;
                kit.Add(view.Tile);
                _gearKit.Add(view);
            }
            _gearKitHint = GearText(Registry.Label(GearModel.PackHintKey), HudTextRole.Meta, HudTokens.TextDim);
            _gearKitHint.style.marginLeft = GearLayout.KitGap + GearLayout.HintGap;
            kit.Add(_gearKitHint);
            body.Add(kit);

            // ---- the footer: the effects on the left, the loadout on the right
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.height = GearLayout.Footer;
            footer.style.marginTop = GearLayout.SectionGap;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = Ink(HudTheme.RowRule);

            for (int i = 0; i < 4; i++)
            {
                var effect = new VisualElement();
                effect.style.flexDirection = FlexDirection.Row;
                effect.style.alignItems = Align.Center;
                if (i > 0) effect.style.marginLeft = GearLayout.EffectGap;
                Label name = GearText(string.Empty, HudTextRole.Meta, HudTokens.TextMeta);
                Label value = GearText(string.Empty, HudTextRole.Meta, HudTokens.TextPrimary, numeric: true);
                value.style.marginLeft = GearLayout.EffectValueGap;
                effect.Add(name);
                effect.Add(value);
                footer.Add(effect);
                _gearEffects.Add((name, value));
            }

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            footer.Add(spacer);

            footer.Add(GearText(Registry.Label(GearModel.LoadoutKey), HudTextRole.PanelLabel, HudTokens.TextDim));
            _gearLoadoutCell = new VisualElement();
            _gearLoadoutCell.style.flexDirection = FlexDirection.Row;
            _gearLoadoutCell.style.alignItems = Align.Center;
            _gearLoadoutCell.style.height = GearLayout.LoadoutCell;
            _gearLoadoutCell.style.marginLeft = GearLayout.LoadoutLabelGap;
            _gearLoadoutCell.style.paddingLeft = GearLayout.LoadoutPadLeft;
            _gearLoadoutCell.style.paddingRight = GearLayout.LoadoutPadRight;
            Border(_gearLoadoutCell, HudTheme.BorderWidth, Ink(HudTheme.ControlBorder));
            _gearLoadoutValue = GearText(string.Empty, HudTextRole.Row, HudTokens.TextDim);
            _gearLoadoutCell.Add(_gearLoadoutValue);
            var chevron = new PathGlyph(HudIcons.ChevronDown, GearLayout.LoadoutChevron, HudTokens.TextDim);
            chevron.style.marginLeft = GearLayout.LoadoutChevronGap;
            _gearLoadoutCell.Add(chevron);
            VisualElement cell = _gearLoadoutCell;
            _gearLoadoutCell.RegisterCallback<ClickEvent>(_ => OpenGearPopover(GearPopoverKind.Loadout, GearSlot.Weapon, -1, cell));
            footer.Add(_gearLoadoutCell);

            body.Add(footer);
            tabBody.Add(body);
            _gearBody = body;
        }

        static VisualElement Column()
        {
            var column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 1;
            column.style.flexShrink = 1;
            column.style.flexBasis = 0;
            column.style.minWidth = 0;
            return column;
        }

        /// <summary>
        /// One slot: the tile against the figure and the words away from it — on the left the row
        /// is reversed and the words right-aligned, on the right they read left to right.
        /// </summary>
        GearSlotView SlotView(GearSlot slot, bool onTheLeft)
        {
            var view = new GearSlotView { Slot = slot };
            view.Root = new VisualElement();
            view.Root.style.flexDirection = onTheLeft ? FlexDirection.RowReverse : FlexDirection.Row;
            view.Root.style.alignItems = Align.Center;
            view.Root.style.height = GearLayout.SlotRow;

            view.Tile = new VisualElement();
            view.Tile.style.width = GearLayout.SlotTile;
            view.Tile.style.height = GearLayout.SlotTile;
            view.Tile.style.flexShrink = 0;
            view.Tile.style.alignItems = Align.Center;
            view.Tile.style.justifyContent = Justify.Center;
            view.Dashes = new DashedOutline { Colour = Ink(HudTheme.ControlBorder) };
            view.Tile.Add(view.Dashes);
            view.Icon = new IconBadge(string.Empty, GearLayout.Icon);
            view.Icon.Inherit(HudTokens.TextMeta);
            view.Tile.Add(view.Icon);
            view.Root.Add(view.Tile);

            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.style.flexDirection = FlexDirection.Column;
            words.style.flexGrow = 1;
            words.style.flexShrink = 1;
            words.style.minWidth = 0;
            words.style.overflow = Overflow.Hidden;
            if (onTheLeft) words.style.marginRight = GearLayout.SlotTextGap;
            else words.style.marginLeft = GearLayout.SlotTextGap;

            view.Name = GearText(string.Empty, HudTextRole.Row, HudTokens.TextPrimary);
            view.Name.style.overflow = Overflow.Hidden;
            view.Name.style.textOverflow = TextOverflow.Ellipsis;
            view.Name.style.unityTextAlign = onTheLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            words.Add(view.Name);

            var meta = new VisualElement { pickingMode = PickingMode.Ignore };
            meta.style.flexDirection = FlexDirection.Row;
            meta.style.alignItems = Align.Center;
            meta.style.justifyContent = onTheLeft ? Justify.FlexEnd : Justify.FlexStart;
            meta.style.marginTop = GearLayout.MetaLineGap;
            view.SlotWord = GearText(string.Empty, HudTextRole.PanelLabel, HudTokens.TextDim);
            view.Quality = GearText(string.Empty, HudTextRole.Meta, HudTokens.TextMeta);
            view.Quality.style.marginLeft = GearLayout.MetaWordGap;
            view.Carry = GearText(string.Empty, HudTextRole.Meta, HudTokens.TextDim);
            view.Carry.style.marginLeft = GearLayout.MetaWordGap;
            meta.Add(view.SlotWord);
            meta.Add(view.Quality);
            meta.Add(view.Carry);
            words.Add(meta);
            view.Root.Add(words);

            view.Root.RegisterCallback<ClickEvent>(_ => OnGearSlotClicked(view));
            return view;
        }

        GearKitView KitView(int index)
        {
            var view = new GearKitView { Index = index };
            view.Tile = new VisualElement();
            view.Tile.style.width = GearLayout.KitTile;
            view.Tile.style.height = GearLayout.KitTile;
            view.Tile.style.flexShrink = 0;
            view.Tile.style.alignItems = Align.Center;
            view.Tile.style.justifyContent = Justify.Center;

            view.Dashes = new DashedOutline { Colour = Ink(HudTheme.PanelBorder) };
            view.Tile.Add(view.Dashes);
            view.Icon = new IconBadge(string.Empty, GearLayout.Icon);
            view.Icon.Inherit(HudTokens.TextMeta);
            view.Tile.Add(view.Icon);

            view.Lock = new PathGlyph(HudIcons.Lock, GearLayout.Lock, HudTokens.TextFaint, stroke: GearLayout.LockStroke);
            view.Tile.Add(view.Lock);

            view.Badge = new VisualElement { pickingMode = PickingMode.Ignore };
            view.Badge.style.position = Position.Absolute;
            view.Badge.style.right = GearLayout.BadgeInset;
            view.Badge.style.bottom = GearLayout.BadgeInset;
            view.Badge.style.height = GearLayout.Badge;
            view.Badge.style.minWidth = GearLayout.Badge;
            view.Badge.style.paddingLeft = GearLayout.BadgePad;
            view.Badge.style.paddingRight = GearLayout.BadgePad;
            view.Badge.style.backgroundColor = HudTokens.PanelFill;
            view.Badge.style.justifyContent = Justify.Center;
            view.Badge.style.alignItems = Align.Center;
            view.Count = GearText(string.Empty, HudTextRole.Meta, HudTokens.TextPrimary, numeric: true);
            view.Badge.Add(view.Count);
            view.Tile.Add(view.Badge);

            view.Tile.RegisterCallback<ClickEvent>(_ => OnGearKitClicked(view));
            return view;
        }

        /// <summary>The pane is being rebuilt for another subject: drop what was built, and shut any popover.</summary>
        void ForgetGearTab()
        {
            CloseGearPopovers();
            _gearBody = null;
            _gearSlots.Clear();
            _gearKit.Clear();
            _gearEffects.Clear();
            _gearKitHint = null;
            _gearLoadoutCell = null;
            _gearLoadoutValue = null;
            _gearVersionShown = int.MinValue;
        }

        /// <summary>Show the body while Gear is the active tab; leaving the tab shuts its popovers.</summary>
        void ShowGearTab(bool shown)
        {
            if (_gearBody != null) _gearBody.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown) CloseGearPopovers();
        }

        /// <summary>Rewrite the tab when the model has moved. Called for a colonist in the frame.</summary>
        void SyncGearTab()
        {
            if (_gearBody == null) return;
            GearModel gear = _inspect.Gear;
            if (gear.Version == _gearVersionShown) return;
            _gearVersionShown = gear.Version;

            foreach (GearSlotView view in _gearSlots) PaintSlot(view, gear.Row(view.Slot));
            for (int i = 0; i < _gearKit.Count && i < gear.Kit.Count; i++) PaintKit(_gearKit[i], gear.Kit[i]);
            if (_gearKitHint != null)
                _gearKitHint.style.display = gear.PackHint ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _gearEffects.Count && i < gear.Effects.Count; i++)
            {
                GearEffect effect = gear.Effects[i];
                (Label name, Label value) = _gearEffects[i];
                HudText.Set(name, effect.Label, HudTextRole.Meta);
                HudText.Set(value, effect.Value, HudTextRole.Meta);
                // A bare default is drawn dim rather than left out, so the line never reflows.
                value.style.color = effect.IsDefault ? HudTokens.TextDim : HudTokens.TextPrimary;
            }

            if (_gearLoadoutValue != null && _gearLoadoutCell != null)
            {
                HudText.Set(_gearLoadoutValue, gear.LoadoutName, HudTextRole.Row);
                _gearLoadoutValue.style.color = gear.HasLoadout ? HudTokens.TextPrimary : HudTokens.TextDim;
                _gearLoadoutCell.style.opacity = gear.Downed ? GearLayout.DisabledOpacity : 1f;
                _gearLoadoutCell.pickingMode = gear.Downed ? PickingMode.Ignore : PickingMode.Position;
            }

            // An open popover shows what it was opened on: if that is gone, it goes too.
            if (_gearPopoverKind == GearPopoverKind.Item && gear.Row(_gearPopoverSlot).State != GearSlotState.Filled)
                CloseGearPopovers();
            else if (_gearPopoverKind == GearPopoverKind.Kit
                     && ((uint)_gearPopoverKit >= (uint)gear.Kit.Count || gear.Kit[_gearPopoverKit].State != KitTileState.Filled))
                CloseGearPopovers();
        }

        void PaintSlot(GearSlotView view, in GearRow row)
        {
            bool selected = (_gearPopoverKind == GearPopoverKind.Item || _gearPopoverKind == GearPopoverKind.Pick)
                            && _gearPopoverSlot == view.Slot;
            bool empty = row.State == GearSlotState.Empty;
            bool jumpsuit = row.State == GearSlotState.Jumpsuit;

            Border(view.Tile, empty && !selected ? 0 : HudTheme.BorderWidth,
                selected ? HudTokens.Accent : Ink(HudTheme.PanelBorder));
            view.Tile.style.backgroundColor = selected ? Ink(HudTheme.ActiveTabFill) : Color.clear;
            view.Dashes.style.display = empty && !selected ? DisplayStyle.Flex : DisplayStyle.None;

            view.Icon.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            if (!empty) view.Icon.SetKey(row.IconKey);
            view.Icon.style.opacity = jumpsuit ? GearLayout.JumpsuitIconOpacity : 1f;

            HudText.Set(view.Name, row.Name, HudTextRole.Row);
            view.Name.style.color = empty ? HudTokens.TextDim : HudTokens.TextPrimary;
            HudText.Set(view.SlotWord, row.SlotName, HudTextRole.PanelLabel);

            HudText.Set(view.Quality, row.QualityWord, HudTextRole.Meta);
            view.Quality.style.display = row.QualityWord.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            HudColour? tier = HudTheme.Quality(row.Quality);
            view.Quality.style.color = jumpsuit ? HudTokens.TextDim
                : tier.HasValue ? Ink(tier.Value) : HudTokens.TextMeta;

            HudText.Set(view.Carry, row.CarryWord, HudTextRole.Meta);
            view.Carry.style.display = row.CarryWord.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void PaintKit(GearKitView view, in KitTile tile)
        {
            bool selected = _gearPopoverKind == GearPopoverKind.Kit && _gearPopoverKit == view.Index;
            switch (tile.State)
            {
                case KitTileState.Filled:
                    Border(view.Tile, HudTheme.BorderWidth, selected ? HudTokens.Accent : Ink(HudTheme.PanelBorder));
                    view.Dashes.style.display = DisplayStyle.None;
                    view.Icon.style.display = DisplayStyle.Flex;
                    view.Icon.SetKey(tile.IconKey);
                    view.Badge.style.display = DisplayStyle.Flex;
                    HudText.Set(view.Count, tile.CountText, HudTextRole.Meta);
                    view.Lock.style.display = DisplayStyle.None;
                    view.Tile.tooltip = tile.Name;
                    break;
                case KitTileState.Empty:
                    Border(view.Tile, HudTheme.BorderWidth, Ink(HudTheme.ControlBorder));
                    view.Dashes.style.display = DisplayStyle.None;
                    view.Icon.style.display = DisplayStyle.None;
                    view.Badge.style.display = DisplayStyle.None;
                    view.Lock.style.display = DisplayStyle.None;
                    view.Tile.tooltip = string.Empty;
                    break;
                default:
                    Border(view.Tile, 0, Color.clear);
                    view.Dashes.style.display = DisplayStyle.Flex;
                    view.Icon.style.display = DisplayStyle.None;
                    view.Badge.style.display = DisplayStyle.None;
                    view.Lock.style.display = DisplayStyle.Flex;
                    view.Tile.tooltip = string.Empty;
                    break;
            }
            view.Tile.style.backgroundColor = selected ? Ink(HudTheme.ActiveTabFill) : Color.clear;
        }

        // ================================================================ clicks

        void OnGearSlotClicked(GearSlotView view)
        {
            GearRow row = _inspect.Gear.Row(view.Slot);
            // A filled slot opens its item; an empty one — the jumpsuit included, which is the empty
            // Body — opens Pick from stores.
            GearPopoverKind kind = row.State == GearSlotState.Filled ? GearPopoverKind.Item : GearPopoverKind.Pick;
            OpenGearPopover(kind, view.Slot, -1, view.Tile);
        }

        void OnGearKitClicked(GearKitView view)
        {
            GearModel gear = _inspect.Gear;
            if ((uint)view.Index >= (uint)gear.Kit.Count || gear.Kit[view.Index].State != KitTileState.Filled) return;
            OpenGearPopover(GearPopoverKind.Kit, GearSlot.Weapon, view.Index, view.Tile);
        }

        // ================================================================ popovers

        /// <summary>
        /// Open a popover beside the pane, level with what raised it; the same click again shuts it.
        /// One element, filled afresh for each kind, because only one is ever open.
        /// </summary>
        void OpenGearPopover(GearPopoverKind kind, GearSlot slot, int kit, VisualElement anchor)
        {
            if (_gearPopoverKind == kind && _gearPopoverSlot == slot && _gearPopoverKit == kit)
            {
                CloseGearPopovers();
                return;
            }

            EnsureGearPopover();
            _gearPopoverKind = kind;
            _gearPopoverSlot = slot;
            _gearPopoverKit = kit;
            _gearPopoverAnchor = anchor;
            FillGearPopover();
            _gearPopover!.style.display = DisplayStyle.Flex;
            PlaceGearPopover();
            _gearVersionShown = int.MinValue;   // repaint the selected tile
            SyncGearTab();
        }

        /// <summary>Shut whichever Gear popover is open. Safe when none is, and before any was built.</summary>
        public void CloseGearPopovers()
        {
            if (_gearPopoverKind == GearPopoverKind.None) return;
            _gearPopoverKind = GearPopoverKind.None;
            _gearPopoverKit = -1;
            _gearPopoverAnchor = null;
            if (_gearPopover != null) _gearPopover.style.display = DisplayStyle.None;
            _gearVersionShown = int.MinValue;
        }

        void EnsureGearPopover()
        {
            if (_gearPopover != null) return;
            var popover = new VisualElement { name = "gear-popover" };
            popover.AddToClassList("panel");
            popover.style.position = Position.Absolute;
            popover.style.flexDirection = FlexDirection.Column;
            popover.style.backgroundColor = HudTokens.PanelFill;
            Border(popover, HudTheme.BorderWidth, Ink(HudTheme.PanelBorder));
            Flat(popover);
            popover.style.display = DisplayStyle.None;
            // Placed again when its size is known, which it is not on the frame it opens.
            popover.RegisterCallback<GeometryChangedEvent>(_ => PlaceGearPopover());
            _hud.Add(popover);
            _gearPopover = popover;
        }

        /// <summary>
        /// Outside the pane on its right, the pane's edge and <see cref="GearLayout.PopoverGap"/>
        /// (<see cref="GearLayout.PopoverLeft"/>), top level with the tile — lifted only as far as it
        /// must be to stay on the screen. It never covers the doll.
        /// </summary>
        void PlaceGearPopover()
        {
            if (_gearPopover == null || _gearPopoverAnchor == null || _gearPopoverKind == GearPopoverKind.None) return;
            Rect hud = _hud.worldBound;
            Rect pane = _inspectPanel.worldBound;
            Rect at = _gearPopoverAnchor.worldBound;
            if (float.IsNaN(pane.xMax) || float.IsNaN(at.yMin)) return;

            _gearPopover.style.left = GearLayout.PopoverLeft((int)(pane.xMin - hud.xMin), (int)pane.width);
            float top = at.yMin - hud.yMin;
            float height = _gearPopover.resolvedStyle.height;
            if (!float.IsNaN(height) && height > 1f && top + height > hud.height) top = Mathf.Max(0f, hud.height - height);
            _gearPopover.style.top = top;
        }

        /// <summary>Once a frame: a press anywhere but the popover or the pane shuts it, as the context menu's does.</summary>
        void UpdateGearPopovers()
        {
            if (_gearPopoverKind == GearPopoverKind.None || _gearPopover == null) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.wasPressedThisFrame) return;
            Vector2 at = ToPanel(mouse.position.ReadValue());
            if (_gearPopover.worldBound.Contains(at) || _inspectPanel.worldBound.Contains(at)) return;
            CloseGearPopovers();
        }

        void FillGearPopover()
        {
            VisualElement popover = _gearPopover!;
            popover.Clear();
            popover.style.paddingTop = 0;
            popover.style.paddingBottom = 0;
            popover.style.paddingLeft = 0;
            popover.style.paddingRight = 0;
            switch (_gearPopoverKind)
            {
                case GearPopoverKind.Item: FillItemPopover(popover, _inspect.Gear.Row(_gearPopoverSlot)); break;
                case GearPopoverKind.Kit: FillKitPopover(popover); break;
                case GearPopoverKind.Pick: FillPickPopover(popover, rebuild: true); break;
                case GearPopoverKind.Loadout: FillLoadoutPopover(popover); break;
            }
        }

        /// <summary>The specification's 21d: the thing, its quality, what it does, and one or two buttons.</summary>
        void FillItemPopover(VisualElement popover, in GearRow row)
        {
            PadPopover(popover, GearLayout.ItemPopoverWidth);
            popover.Add(ItemHeader(row.IconKey, row.Name, row.Quality, row.QualityWord));
            if (row.EffectA.Length > 0) popover.Add(EffectLine(row.EffectA, row.EffectAValue));
            if (row.EffectB.Length > 0) popover.Add(EffectLine(row.EffectB, row.EffectBValue));
            if (row.Slot == GearSlot.Weapon && row.CarryWord.Length > 0)
                popover.Add(GearText(row.CarryWord, HudTextRole.Meta, HudTokens.TextDim));

            PawnId pawn = _inspect.Pawn;
            GearSlot slot = row.Slot;
            if (slot == GearSlot.Weapon)
            {
                ButtonRow(popover,
                    (Registry.Label("ui.command.unequip"), () => SendGearOrder(CombatOrders.Unequip(pawn, leaveHere: false))),
                    (Registry.Label("ui.command.drop"), () => SendGearOrder(CombatOrders.Unequip(pawn, leaveHere: true))));
            }
            else if (row.Preview)
            {
                ButtonRow(popover, (Registry.Label("ui.command.remove"), () => _gearPreview.Remove(pawn, slot)));
            }
        }

        void FillKitPopover(VisualElement popover)
        {
            GearModel gear = _inspect.Gear;
            if ((uint)_gearPopoverKit >= (uint)gear.Kit.Count) return;
            KitTile tile = gear.Kit[_gearPopoverKit];
            PadPopover(popover, GearLayout.ItemPopoverWidth);
            popover.Add(ItemHeader(tile.IconKey, tile.Name, 0, string.Empty));
            popover.Add(EffectLine(Registry.Label(GearModel.KitKey), tile.CountText));

            PawnId pawn = _inspect.Pawn;
            int index = tile.PreviewIndex;
            ButtonRow(popover,
                (Registry.Label("ui.command.remove"), () => _gearPreview.RemoveKit(pawn, index)),
                (Registry.Label("ui.command.drop"), () => _gearPreview.RemoveKit(pawn, index)));
        }

        /// <summary>The specification's 21e: what the stores hold that fits, eight a page, never a scrollbar.</summary>
        void FillPickPopover(VisualElement popover, bool rebuild)
        {
            var world = _boot?.World;
            if (world == null) return;
            if (rebuild) _gearPick.Build(world.Views.Current, _gearPopoverSlot, _gearPreview);
            popover.Clear();
            popover.style.width = GearLayout.PickWidth;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = GearLayout.PickHeader;
            header.style.paddingLeft = GearLayout.ItemPopoverPad;
            header.style.paddingRight = GearLayout.ItemPopoverPad;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Ink(HudTheme.PanelBorder);
            header.Add(GearText(Registry.Label(GearPickModel.TitleKey), HudTextRole.PanelLabel, HudTokens.TextPrimary));
            Label slotWord = GearText(_gearPick.SlotName, HudTextRole.PanelLabel, HudTokens.TextDim);
            slotWord.style.marginLeft = GearLayout.MetaWordGap;
            header.Add(slotWord);
            popover.Add(header);

            bool downed = _inspect.Gear.Downed;
            if (_gearPick.Rows.Count == 0)
            {
                Label none = GearText(Registry.Label(GearPickModel.EmptyKey), HudTextRole.Meta, HudTokens.TextDim);
                none.style.paddingLeft = GearLayout.ItemPopoverPad;
                none.style.height = GearLayout.PickRow;
                none.style.unityTextAlign = TextAnchor.MiddleLeft;
                popover.Add(none);
            }

            for (int i = 0; i < _gearPick.Rows.Count; i++)
            {
                GearPickRow pick = _gearPick.Rows[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = GearLayout.PickRow;
                row.style.paddingLeft = GearLayout.ItemPopoverPad;
                row.style.paddingRight = GearLayout.ItemPopoverPad;
                var icon = new IconBadge(pick.IconKey, GearLayout.Icon);
                icon.Inherit(HudTokens.TextMeta);
                row.Add(icon);
                Label name = GearText(pick.Name, HudTextRole.Row, HudTokens.TextPrimary);
                name.style.marginLeft = GearLayout.SlotTextGap;
                row.Add(name);
                if (pick.QualityWord.Length > 0)
                {
                    HudColour? tier = HudTheme.Quality(pick.Quality);
                    Label quality = GearText(pick.QualityWord, HudTextRole.Meta, tier.HasValue ? Ink(tier.Value) : HudTokens.TextMeta);
                    quality.style.marginLeft = GearLayout.MetaWordGap;
                    row.Add(quality);
                }
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.style.flexGrow = 1;
                row.Add(fill);
                row.Add(GearText(pick.Place, HudTextRole.Meta, HudTokens.TextMeta));

                if (downed)
                {
                    row.style.opacity = GearLayout.DisabledOpacity;
                }
                else
                {
                    int index = i;
                    row.RegisterCallback<PointerEnterEvent>(_ => row.style.backgroundColor = Ink(HudTheme.RowRule));
                    row.RegisterCallback<PointerLeaveEvent>(_ => row.style.backgroundColor = Color.clear);
                    row.RegisterCallback<ClickEvent>(_ => ChoosePick(index));
                }
                popover.Add(row);
            }

            if (_gearPick.PageCount > 1) popover.Add(PickPager());

            if (downed) popover.Add(DownedReason(GearLayout.ItemPopoverPad));
            else if (_gearPick.Rows.Count > 0)
            {
                Label hint = GearText(Registry.Label(GearPickModel.HintKey), HudTextRole.Meta, HudTokens.TextDim);
                hint.style.paddingLeft = GearLayout.ItemPopoverPad;
                hint.style.paddingTop = GearLayout.ItemPopoverGap;
                hint.style.paddingBottom = GearLayout.ItemPopoverGap;
                popover.Add(hint);
            }
        }

        VisualElement PickPager()
        {
            var pager = new VisualElement();
            pager.style.flexDirection = FlexDirection.Row;
            pager.style.alignItems = Align.Center;
            pager.style.justifyContent = Justify.Center;
            pager.style.height = GearLayout.PickHeader;

            var back = new PathGlyph(HudIcons.ChevronLeft, GearLayout.LoadoutChevron + 4, HudTokens.TextMeta);
            back.pickingMode = PickingMode.Position;
            back.RegisterCallback<ClickEvent>(_ => { _gearPick.PreviousPage(); FillPickPopover(_gearPopover!, rebuild: false); });
            pager.Add(back);

            Label page = GearText((_gearPick.Page + 1) + " / " + _gearPick.PageCount, HudTextRole.Meta, HudTokens.TextMeta, numeric: true);
            page.style.marginLeft = GearLayout.KitGap;
            page.style.marginRight = GearLayout.KitGap;
            pager.Add(page);

            var next = new PathGlyph(HudIcons.ChevronRight, GearLayout.LoadoutChevron + 4, HudTokens.TextMeta);
            next.pickingMode = PickingMode.Position;
            next.RegisterCallback<ClickEvent>(_ => { _gearPick.NextPage(); FillPickPopover(_gearPopover!, rebuild: false); });
            pager.Add(next);
            return pager;
        }

        void ChoosePick(int row)
        {
            if (_gearPick.Choose(row, _inspect.Pawn, _gearPreview, out Intent order)) SendGearOrder(order);
            CloseGearPopovers();
        }

        /// <summary>
        /// The loadout picker: None, and while the preview is on its two made-up loadouts. The
        /// editor is a later unit (design 47 §6), and the picker says so.
        /// </summary>
        void FillLoadoutPopover(VisualElement popover)
        {
            PadPopover(popover, GearLayout.ItemPopoverWidth);
            PawnId pawn = _inspect.Pawn;
            int current = _gearPreview.Loadout(pawn);
            int count = _gearPreview.On ? GearPreview.LoadoutKeys.Length : 1;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                Label option = GearText(Registry.Label(GearPreview.LoadoutKeys[i]), HudTextRole.Row,
                    i == current ? HudTokens.Accent : HudTokens.TextPrimary);
                option.style.height = GearLayout.PickRow - 10;
                option.style.unityTextAlign = TextAnchor.MiddleLeft;
                option.RegisterCallback<ClickEvent>(_ =>
                {
                    _gearPreview.SetLoadout(pawn, index);
                    CloseGearPopovers();
                });
                popover.Add(option);
            }
            popover.Add(GearText(Registry.Label("ui.gear.loadout.later"), HudTextRole.Meta, HudTokens.TextDim));
        }

        // ---- the popovers' parts

        static void PadPopover(VisualElement popover, int width)
        {
            popover.style.width = width;
            popover.style.paddingTop = GearLayout.ItemPopoverPad;
            popover.style.paddingBottom = GearLayout.ItemPopoverPad;
            popover.style.paddingLeft = GearLayout.ItemPopoverPad;
            popover.style.paddingRight = GearLayout.ItemPopoverPad;
        }

        static VisualElement ItemHeader(string iconKey, string name, int quality, string qualityWord)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = GearLayout.ItemPopoverGap;
            var icon = new IconBadge(iconKey, GearLayout.Icon);
            icon.Inherit(HudTokens.TextMeta);
            header.Add(icon);
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.style.marginLeft = GearLayout.SlotTextGap;
            words.Add(GearText(name, HudTextRole.Row, HudTokens.TextPrimary));
            if (qualityWord.Length > 0)
            {
                HudColour? tier = HudTheme.Quality(quality);
                words.Add(GearText(qualityWord, HudTextRole.Meta, tier.HasValue ? Ink(tier.Value) : HudTokens.TextMeta));
            }
            header.Add(words);
            return header;
        }

        static VisualElement EffectLine(string label, string value)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.marginBottom = 4;
            line.Add(GearText(label, HudTextRole.Meta, HudTokens.TextMeta));
            Label figure = GearText(value, HudTextRole.Meta, HudTokens.TextPrimary, numeric: true);
            figure.style.marginLeft = GearLayout.EffectValueGap;
            line.Add(figure);
            return line;
        }

        Label DownedReason(int pad)
        {
            Label reason = GearText(_inspect.Gear.DownedReason, HudTextRole.Meta, HudTokens.Warn);
            reason.style.paddingLeft = pad;
            reason.style.paddingTop = GearLayout.ItemPopoverGap;
            reason.style.paddingBottom = GearLayout.ItemPopoverGap;
            reason.style.whiteSpace = WhiteSpace.Normal;
            return reason;
        }

        /// <summary>
        /// One or two buttons across the popover. On a downed colonist they are there, at 40 %, and
        /// do nothing — with the reason above them in the warning colour (the specification's 21f).
        /// </summary>
        void ButtonRow(VisualElement popover, params (string Label, Action Act)[] buttons)
        {
            bool downed = _inspect.Gear.Downed;
            if (downed) popover.Add(DownedReason(0));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = GearLayout.ItemPopoverGap;
            for (int i = 0; i < buttons.Length; i++)
            {
                (string label, Action act) = buttons[i];
                var button = new VisualElement();
                button.style.flexGrow = 1;
                button.style.flexBasis = 0;
                button.style.height = GearLayout.ButtonHeight;
                button.style.justifyContent = Justify.Center;
                button.style.alignItems = Align.Center;
                if (i > 0) button.style.marginLeft = GearLayout.ItemPopoverGap;
                Border(button, HudTheme.BorderWidth, Ink(HudTheme.ControlBorder));
                button.Add(GearText(label, HudTextRole.Row, HudTokens.TextPrimary));
                if (downed)
                {
                    button.style.opacity = GearLayout.DisabledOpacity;
                    button.SetEnabled(false);
                }
                else
                {
                    button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = Ink(HudTheme.RowRule));
                    button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = Color.clear);
                    button.RegisterCallback<ClickEvent>(_ =>
                    {
                        act();
                        CloseGearPopovers();
                    });
                }
                row.Add(button);
            }
            popover.Add(row);
        }

        /// <summary>Hand one order to the world; it lands at the tick, paused or not (both apply while paused).</summary>
        void SendGearOrder(Intent intent) => _boot?.World?.Intents.Submit(intent);
    }
}
