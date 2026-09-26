#nullable enable
using System;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// One of the inspect header's two large toggles, Draft and First Person (design 61, mockup
    /// 24c): a square tile with an icon and a hotkey cap, and a label under it.
    ///
    /// <para><b>The icon never swaps.</b> Off it is an outline in the dim ink in a neutral tile; on,
    /// the same path is filled and stroked in the toggle's hue and the tile takes the hue's border
    /// and a 12 % wash. The tile's colours are the stylesheet's (<c>.inspect__toggle--on</c> and the
    /// hue modifier); the icon's are here, because a path is painted rather than styled.</para>
    ///
    /// <para><b>Built once per header and set in place</b>: <see cref="Set"/> compares before it
    /// writes, so the pane's fifteen refreshes a second touch nothing while nothing changes, and a
    /// draft no longer rebuilds the header the way the old two-faced button did.</para>
    /// </summary>
    public sealed class InspectToggle : VisualElement
    {
        readonly PathGlyph _icon;
        readonly Label _cap;
        readonly Label _label;
        readonly VisualElement _mixed;
        readonly Color _hue;

        bool _on;
        bool _live = true;
        bool _hover;
        HudKey _key = (HudKey)(-1);
        string _labelText = string.Empty;
        DraftFace _face = (DraftFace)(-1);

        /// <param name="hueClass">The stylesheet modifier naming the hue: <c>inspect__toggle--draft</c> or <c>--view</c>.</param>
        public InspectToggle(string path, HudColour hue, string hueClass, Action press)
        {
            _hue = HudTokens.Convert(hue);
            AddToClassList("inspect__toggle");
            AddToClassList(hueClass);

            var tile = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.AddToClassList("inspect__tile");

            _icon = new PathGlyph(path, HudLayout.InspectToggleIcon, HudTokens.TextDim,
                stroke: HudIcons.ToggleStroke);
            tile.Add(_icon);

            // The mixed draft's mark (design 61 §3): a 2 px line along the tile's foot, inside its
            // border. Absolute, so showing it moves nothing.
            _mixed = new VisualElement { pickingMode = PickingMode.Ignore };
            _mixed.AddToClassList("inspect__mixed");
            _mixed.style.display = DisplayStyle.None;
            tile.Add(_mixed);

            _cap = HudText.Make(string.Empty, HudTextRole.Hotkey, ussClass: "inspect__cap");
            _cap.pickingMode = PickingMode.Ignore;
            tile.Add(_cap);
            Add(tile);

            _label = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__togglelabel");
            _label.pickingMode = PickingMode.Ignore;
            Add(_label);

            RegisterCallback<PointerEnterEvent>(_ => { _hover = true; PaintIcon(); });
            RegisterCallback<PointerLeaveEvent>(_ => { _hover = false; PaintIcon(); });
            RegisterCallback<ClickEvent>(evt =>
            {
                if (_live) press();
                evt.StopPropagation();
            });
        }

        /// <summary>
        /// The toggle's whole state. <paramref name="face"/> carries the mixed draft; First Person
        /// passes <see cref="DraftFace.On"/> or <see cref="DraftFace.Off"/>.
        /// </summary>
        public void Set(DraftFace face, bool live, string label, HudKey key)
        {
            if (face != _face)
            {
                _face = face;
                _on = face == DraftFace.On;
                EnableInClassList("inspect__toggle--on", _on);
                _mixed.style.display = face == DraftFace.Mixed ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (live != _live)
            {
                _live = live;
                EnableInClassList("inspect__toggle--off", !live);
            }
            if (!ReferenceEquals(label, _labelText))
            {
                _labelText = label;
                HudText.Set(_label, label, HudTextRole.Meta);
            }
            // The cap names the key the binding map holds now, so a rebind shows here at once. An
            // unbound action shows no cap rather than an empty one.
            if (key != _key)
            {
                _key = key;
                _cap.text = HotkeyDirector.Display(key);
                _cap.style.display = key == HudKey.None ? DisplayStyle.None : DisplayStyle.Flex;
            }
            PaintIcon();
        }

        void PaintIcon()
        {
            _icon.Solid = _on;
            _icon.Tint = _on ? _hue : _hover && _live ? HudTokens.TextMeta : HudTokens.TextDim;
        }
    }
}
