#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the orders strip, and the gutter it shares with the depth rail.
    ///
    /// <para><b>What it is.</b> The four verbs a player applies to what is already on the board —
    /// chop, mine, deconstruct, cancel — as a column of icon buttons down the right-hand edge of
    /// the screen, directly under the depth rail. Owner, 2026-09-17: <i>"the small buttons on the
    /// build menu for Chop Trees, Mine, Deconstruct, Cancel should be a vertical button strip that
    /// sits below the depth control and menu button — to the right hand side of the screen very
    /// close to the screen border … as this enables us to quickly give orders without having to
    /// click the build button — we can use this in future for more orders."</i></para>
    ///
    /// <para><b>Why it is not in the Build palette any more.</b> They were "always on show" in the
    /// palette's header, which is always on show inside a panel that is usually shut — so every
    /// order a player gave cost opening the palette first, and the palette is a list of things to
    /// <i>put down</i> rather than verbs to apply. That is the third turn of the same screw: Chop
    /// and Mine came out of a dropped Orders category so they would not be left on the M and C
    /// keys and nothing else, and Cancel before them <i>"was never missing — every way of finding
    /// it was missing"</i>. Here they are visible with nothing open at all.</para>
    ///
    /// <para><b>The gutter is one column, and that is what keeps the strip under the rail.</b> The
    /// rail is the one region the world sizes — sixteen layers today, more on request, squeezed
    /// when the screen is short — so a strip placed at a top of its own would sit under a rail of
    /// one particular length and float away from or run into every other. The two share an
    /// absolutely positioned gutter at the screen's right edge and stack inside it, which is the
    /// same fix the clock and the alerts column carries and for the same reason recorded there:
    /// never two panels in one corner with hand-picked tops.</para>
    ///
    /// <para><b>The shell decides nothing here either.</b> Which orders exist is
    /// <see cref="PaletteTools.Pinned"/>, what each is called is the naming registry, what colour
    /// it wears is <see cref="HudTheme.PinnedActionHue"/>, what it does is
    /// <see cref="BuildPaletteModel.TogglePinned"/>, and whether it is held is asked of the
    /// designate director rather than remembered — so a tool armed by a hotkey lights its button
    /// without anybody wiring the two together.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        /// <summary>The right-hand gutter: the depth rail, and the orders strip under it.</summary>
        VisualElement _gutter = null!;

        /// <summary>One button per order, by the key it arms. Built once — the strip has no
        /// layouts and nothing throws it away.</summary>
        readonly Dictionary<string, VisualElement> _orderButtons =
            new Dictionary<string, VisualElement>();

        /// <summary>What the strip was last painted for, so a per-frame repaint costs a string
        /// comparison. The same bargain <see cref="UpdateArmedBanner"/> makes.
        ///
        /// <para>The sentinel is a value no order key and no empty string can equal, so the first
        /// paint always runs. <b>Written as the escape, and it has to stay that way</b>: it was a
        /// literal NUL byte in the source until 2026-09-18, which made git class this whole file as
        /// binary and hide every diff of it — the change that added
        /// <see cref="CloseMenusOverTheBoard"/> to the button below landed as "Bin 7775 -> 8672
        /// bytes" with nothing for a reviewer to read.</para></summary>
        string _ordersPaintedFor = "\0";

        // ============================================================ the gutter

        void BuildGutter()
        {
            _gutter = new VisualElement { name = "right-edge", pickingMode = PickingMode.Ignore };
            _gutter.AddToClassList("column-edge");
            _worldUi.Add(_gutter);

            BuildRail(_gutter);
            BuildOrders(_gutter);
            BuildViews(_gutter);
        }

        // ============================================================ the views strip

        /// <summary>One toggle per view, by its key (design 32 §14).</summary>
        readonly Dictionary<string, VisualElement> _viewButtons = new Dictionary<string, VisualElement>();

        /// <summary>What the strip was last painted for: one bit per view, and -1 so the first paint runs.</summary>
        int _viewsPaintedFor = -1;

        /// <summary>The views drawn as paths rather than glyphs, re-tinted when they go on and off.</summary>
        readonly Dictionary<string, PathGlyph> _viewPaths = new Dictionary<string, PathGlyph>();

        /// <summary>
        /// The views strip: under the orders, the same buttons, but each a switch that stays where
        /// it is put rather than a tool that is held (owner, 2026-09-23). Which views exist is
        /// <see cref="HudViews.Keys"/>; whether one is on is the overlay director's.
        /// </summary>
        void BuildViews(VisualElement gutter)
        {
            VisualElement strip = Panel("views", "orders", "views");
            foreach (string key in HudViews.Keys) strip.Add(ViewButton(key));
            gutter.Add(strip);
        }

        VisualElement ViewButton(string key)
        {
            var button = new VisualElement { name = "view-" + key };
            button.AddToClassList("ord__btn");
            // Home is drawn from Claude Design's path (design 43 §5a), in the accent at 80% while
            // off and in the text colour while on; power keeps the palette category's bolt.
            string? path = HudViews.PathOf(key);
            if (path != null)
            {
                var glyph = new PathGlyph(path, 17f, HudTokens.Convert(HudTheme.Accent.WithAlpha(0.80f)), fill: true);
                _viewPaths[key] = glyph;
                button.Add(glyph);
            }
            else
            {
                button.Add(new HudGlyph(key == HudViews.Power ? HudGlyphKind.CategoryPower : HudGlyphKind.Placeholder,
                    17f, HudTokens.Convert(HudTheme.Accent)));
            }
            button.tooltip = Registry.Label(key) + " — show it whatever is armed; press again to hide it";
            button.RegisterCallback<ClickEvent>(_ =>
            {
                if (_directors == null) return;
                HudViews.Toggle(_directors.Overlays, key);
                MarkViews();
            });
            _viewButtons[key] = button;
            return button;
        }

        /// <summary>Light the views that are on. Called every frame; early-returns when nothing moved.</summary>
        void MarkViews()
        {
            if (_directors == null) return;
            int bits = 0;
            for (int i = 0; i < HudViews.Keys.Length; i++)
                if (HudViews.IsOn(_directors.Overlays, HudViews.Keys[i])) bits |= 1 << i;
            if (bits == _viewsPaintedFor) return;
            _viewsPaintedFor = bits;

            for (int i = 0; i < HudViews.Keys.Length; i++)
            {
                if (!_viewButtons.TryGetValue(HudViews.Keys[i], out VisualElement? button)) continue;
                bool on = (bits & (1 << i)) != 0;
                HudColour hue = HudTheme.Accent;
                button.EnableInClassList("ord__btn--on", on);
                button.style.backgroundColor = HudTokens.Convert(hue.WithAlpha(on ? 0.30f : 0.06f));
                button.style.borderTopColor = button.style.borderRightColor =
                    button.style.borderBottomColor = button.style.borderLeftColor =
                        HudTokens.Convert(hue.WithAlpha(on ? 1f : 0.30f));
                if (_viewPaths.TryGetValue(HudViews.Keys[i], out PathGlyph? glyph))
                    glyph.Tint = HudTokens.Convert(on ? HudTheme.TextPrimary : hue.WithAlpha(0.80f));
            }

            // The Menu's rows for the same views are the same switches (design 43 §5a).
            foreach (KeyValuePair<string, VisualElement> row in _overlayRows)
                row.Value.EnableInClassList("menu__row--on", HudViews.IsOn(_directors.Overlays, row.Key));
        }

        // ============================================================ the orders strip

        void BuildOrders(VisualElement gutter)
        {
            VisualElement strip = Panel("orders", "orders");

            // No label row. "DEPTH" fits a 44 px gutter and "ORDERS" does not — the acceptance
            // criteria forbid clipped text, and a word set small enough to fit would be a word
            // nobody reads. Every button carries its name and its key in a tooltip instead, which
            // is what the palette header's copies did when they were 26 px squares with no room
            // for a label either.
            foreach (string key in PaletteTools.Pinned) strip.Add(OrderButton(key));

            gutter.Add(strip);
        }

        /// <summary>
        /// One order, as a square in its own colour.
        ///
        /// <para>The hue is the mode colour the open palette wears while the order is held
        /// (<c>docs/design/17-build-palette-layouts.md</c> §7), so the button a player pressed and
        /// the panel that goes that colour are obviously the same thing.</para>
        /// </summary>
        VisualElement OrderButton(string key)
        {
            var button = new VisualElement { name = "action-" + key };
            button.AddToClassList("ord__btn");

            button.Add(new HudGlyph(PaletteGlyphs.For(key), 17f,
                HudTokens.Convert(HudTheme.PinnedActionHue(key) ?? HudTheme.TextMeta)));

            button.tooltip = Registry.Label(key) + HotkeyLegend(key) + " — drag a box over the world";
            button.RegisterCallback<ClickEvent>(_ =>
            {
                // Giving an order puts away whatever was open over the board (owner, 2026-09-18:
                // "if I'm in the build menu (or any other menu) and I click on an order - I expect
                // that menu to be closed down and the … order would happen"). The strip is on
                // screen whether or not anything else is, which is the whole reason it was moved
                // out of the palette's header — so it is the one control a player reaches for
                // *from inside* something else, and leaving that something else standing is what
                // made it feel like the click had not landed.
                //
                // Closed before the toggle rather than after, so the order is the last thing that
                // happens and the panels are gone by the time it lights its button.
                CloseMenusOverTheBoard();

                _palette?.TogglePinned(key);
                MarkOrders();
                MarkBuildState();
            });

            _orderButtons[key] = button;
            return button;
        }

        /// <summary>
        /// Light the order that is held, if any.
        ///
        /// <para>Called every frame and early-returns on the usual one, because the tool can be
        /// armed and put down from four places — this strip, the palette, a hotkey, and Escape or
        /// right-click putting it down — and a strip that lit only when it was itself clicked
        /// would be a second record of what is armed, which is the drift
        /// <see cref="BuildPaletteModel.ArmedPinned"/> exists to avoid.</para>
        /// </summary>
        void MarkOrders()
        {
            string armed = _palette == null ? string.Empty : _palette.ArmedPinned;
            if (armed == _ordersPaintedFor) return;
            _ordersPaintedFor = armed;

            foreach (KeyValuePair<string, VisualElement> entry in _orderButtons)
            {
                bool on = entry.Key == armed;
                HudColour hue = HudTheme.PinnedActionHue(entry.Key) ?? HudTheme.TextMeta;

                VisualElement button = entry.Value;
                button.EnableInClassList("ord__btn--on", on);
                button.style.backgroundColor = HudTokens.Convert(hue.WithAlpha(on ? 0.30f : 0.12f));
                button.style.borderTopColor = button.style.borderRightColor =
                    button.style.borderBottomColor = button.style.borderLeftColor =
                        HudTokens.Convert(hue.WithAlpha(on ? 1f : 0.45f));
            }
        }

        /// <summary>Rebuild every tooltip, after a rebind. The key a button names is read from the
        /// binding map, so a rebind that did not reach here would leave the strip promising a key
        /// that no longer arms anything.</summary>
        void RefreshOrderTooltips()
        {
            foreach (KeyValuePair<string, VisualElement> entry in _orderButtons)
                entry.Value.tooltip =
                    Registry.Label(entry.Key) + HotkeyLegend(entry.Key) + " — drag a box over the world";
        }
    }
}
