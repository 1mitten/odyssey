#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// One item of the command bar: what it is called, what key reaches it, and whether it is the
    /// one filled item on the row.
    ///
    /// <para>Nothing here is named in C#. <see cref="Label"/> is read out of the naming registry
    /// by <see cref="HudCommands"/>, which is the whole point of the registry — a name the owner
    /// corrects in <c>docs/design/icon-keys.csv</c> reaches the screen without anyone retyping
    /// it.</para>
    /// </summary>
    public readonly struct HudCommand
    {
        public readonly string Key;
        public readonly string Label;

        /// <summary>The cap drawn at the right of the item. Never blank: the acceptance criteria
        /// require every item on the bar to show one.</summary>
        public readonly string Hotkey;

        public readonly HudCategory Category;

        /// <summary>The single filled item on the row. Exactly one command is primary.</summary>
        public readonly bool Primary;

        /// <summary>Why this item does nothing yet, or empty when it does.</summary>
        public readonly string Reason;

        public HudCommand(string key, string label, string hotkey, HudCategory category,
            bool primary, string reason)
        {
            Key = key;
            Label = label;
            Hotkey = hotkey;
            Category = category;
            Primary = primary;
            Reason = reason;
        }

        public bool Live => Reason.Length == 0;
    }

    /// <summary>
    /// The command bar's contents and its overflow rule.
    ///
    /// <para><b>The overflow rule is an acceptance criterion, not a nicety.</b> "Nothing may run
    /// off the edge, and no unlabelled colour chips" is how the spec puts it, and the bar this
    /// replaces did neither: it wrapped to a second row once every tab carried its full name, and
    /// the inspect pane above it was held clear by a hand-picked offset chosen for a two-row bar.
    /// <c>Hud.uss</c> carries that coupling in a comment admitting it is one. A bar that cannot
    /// grow cannot push anything, so the offset stops being a guess.</para>
    ///
    /// <para><b>Where an item goes when it does not fit.</b> Into Menu, which is always last and
    /// never dropped — a player who cannot see History can still reach it. Items leave the row
    /// from the right, so the order on screen never reshuffles as the window is resized; only its
    /// tail shortens.</para>
    ///
    /// <para><b>The panels are on function keys, and that was not the first answer.</b> The first
    /// pass gave each item a letter — W for Work, S for Schedule, A for Animals — and every one of
    /// those is already a camera key: WASD pans, Q and E turn, B cycled the below-slice mode, V the
    /// above one, M, C and X arm the designate tools, R and F move the slice, Space and 1 to 3 are
    /// the clock, Home recentres. The test that was supposed to catch it listed nine reserved keys
    /// and missed five, which is why <c>HotkeyClashTests</c> now reads the reserved set out of the
    /// source rather than out of somebody's memory.</para>
    ///
    /// <para>F1 to F9 are unclaimed, are the convention for top-level panels, and have the
    /// incidental virtue of being narrow: eleven labelled items have to cross the bottom of the
    /// screen without one falling off the end. <b>Build keeps a letter</b>, because it is the one
    /// item here that does something today and the one a player reaches for without looking; the
    /// below-slice cycle gave B up and moved to shift-V, beside the above-slice cycle it belongs
    /// with. Escape opens Menu, which is the panel Escape already opened.</para>
    /// </summary>
    public static class HudCommands
    {
        /// <summary>Opens the Build palette. The first command on the bar that did something.</summary>
        public const string BuildKey = "ui.tab.build";

        /// <summary>
        /// Opens the Work tab. The second command on this bar to do something, and the first of
        /// the function keys to stop being a legend (design 27).
        ///
        /// <para><b>Schedule left this bar on 2026-09-20</b> and did not go anywhere else: the two
        /// are one table now, work on the left and the day on the right, sharing one frozen column
        /// of names. Two tabs would have meant two answers to "what is this colonist doing", read
        /// one after the other, which is the comparison the combined row exists to remove. The key
        /// <c>ui.tab.schedule</c> is kept in the registry and says so; F2 is free again.</para>
        /// </summary>
        public const string WorkKey = "ui.tab.work";

        /// <summary>Always last, always present, and the home of anything that did not fit.</summary>
        public const string MenuKey = "ui.tab.menu";

        /// <summary>Opens the Almanac in-game reference wiki (F9).</summary>
        public const string AlmanacKey = "ui.tab.almanac";

        /// <summary>Opens the Inventory tab (design 35): what the stores hold, and where. F2, between Work and Research.</summary>
        public const string InventoryKey = "ui.tab.inventory";

        /// <summary>Opens the Research tab (design 34). F3, which the bar has advertised since M1.</summary>
        public const string ResearchKey = "ui.tab.research";
        /// <summary>
        /// F5 became real with design 30: one tab, Animals, listing the wild animals now and the
        /// tamed ones when taming exists (owner, 2026-09-23). Wildlife on F6 is a dead item again.
        /// </summary>
        public const string AnimalsKey = "ui.tab.animals";

        /// <summary>
        /// Opens the Assign tab (design 43 §6): where each colonist may work and what she does
        /// about danger. F4, the slot the bar has advertised for a colonist list since M1; the
        /// roster strip stays the list, and <c>ui.tab.colonists</c> stays in the registry.
        /// </summary>
        public const string AssignKey = "ui.tab.assign";

        /// <summary>
        /// The icon a bar item draws, by key: its own, except Assign's, which draws the owner's
        /// three-person silhouette already drawn for the Colonists slot it took over.
        /// </summary>
        public static string IconOf(string key) => key == AssignKey ? "ui.tab.colonists" : key;

        static readonly (string Key, string Hotkey, string Reason)[] Order =
        {
            (BuildKey, "B", ""),
            (WorkKey, "F1", ""),
            (InventoryKey, "F2", ""),
            (ResearchKey, "F3", ""),
            (AssignKey, "F4", ""),
            (AnimalsKey, "F5", ""),
            // Wildlife left the bar on 2026-09-23 (owner: "remove Wildlife bottom bar"): what is
            // out there is the Animals tab. The key stays in the registry for the day the tamed
            // half arrives and the two halves want naming apart.
            ("ui.tab.bills", "F7", "bills arrive with M5"),
            ("ui.tab.factions", "F8", "factions arrive with M7"),
            (AlmanacKey, "F9", ""),
            (MenuKey, "Esc", ""),
        };

        /// <summary>Every key the bar can draw, for the registry test.</summary>
        public static readonly string[] IconKeys = BuildKeys();

        static string[] BuildKeys()
        {
            var keys = new string[Order.Length];
            for (int i = 0; i < Order.Length; i++) keys[i] = Order[i].Key;
            return keys;
        }

        /// <summary>The bar, in the order it is drawn, names resolved from the registry.</summary>
        public static IReadOnlyList<HudCommand> All
        {
            get
            {
                var items = new List<HudCommand>(Order.Length);
                foreach ((string key, string hotkey, string reason) in Order)
                    items.Add(new HudCommand(
                        key, Registry.Label(key), hotkey, HudTheme.CategoryOf(key),
                        primary: key == BuildKey, reason));
                return items;
            }
        }

        // ---------------------------------------------------------------- measurement

        /// <summary>Side padding of an ordinary item.</summary>
        public const int SidePad = 12;

        /// <summary>Side padding of the primary item, which is a touch wider so the filled block
        /// does not crowd its own label.</summary>
        public const int PrimarySidePad = 14;

        /// <summary>The icon slot in a command-bar item.</summary>
        public const int IconSize = 16;

        /// <summary>Icon to label.</summary>
        public const int IconGap = 9;

        /// <summary>Label to hotkey cap.</summary>
        public const int HotkeyGap = 8;

        /// <summary>Between two items.</summary>
        public const int ItemGap = 3;

        /// <summary>Height of an item, and so of the bar's content.</summary>
        public const int ItemHeight = 38;

        /// <summary>The bar's own padding, all round.</summary>
        public const int BarPad = 5;

        /// <summary>The hairline before Menu.</summary>
        public const int DividerWidth = 1;

        public const int DividerHeight = 26;

        /// <summary>Space either side of the divider.</summary>
        public const int DividerGap = 3;

        /// <summary>
        /// A rough advance per character, as a fraction of the font size.
        ///
        /// <para>Used only by the layout <i>model</i>, which is what the fast tier measures
        /// coverage against; the shell itself reflows the real bar from the widths UI Toolkit
        /// actually laid out, so a bad estimate here can make a coverage claim pessimistic and can
        /// never make the bar overflow. It is deliberately generous for that reason.</para>
        /// </summary>
        public const float UiAdvance = 0.50f;

        /// <summary>The same, for the mono face, which is wider per character.</summary>
        public const float MonoAdvance = 0.60f;

        /// <summary>The modelled width of one item.</summary>
        public static float Width(HudCommand command)
        {
            int pad = command.Primary ? PrimarySidePad : SidePad;
            float label = command.Label.Length * HudType.Of(HudTextRole.Row).Size * UiAdvance;
            float hotkey = command.Hotkey.Length * HudType.Of(HudTextRole.Hotkey).Size * MonoAdvance;
            return pad * 2 + IconSize + IconGap + label + HotkeyGap + hotkey;
        }

        /// <summary>
        /// How many of the leading items fit, given the widths of each and the space the bar has.
        /// Menu is the last item and is never counted out: the returned count always includes it,
        /// and everything between the cut and Menu belongs in the Menu popup.
        /// </summary>
        /// <param name="widths">One width per item, in <see cref="All"/> order, Menu last.</param>
        /// <param name="available">The width the bar may occupy, inclusive of its own padding.</param>
        /// <returns>How many leading items are drawn on the row, not counting Menu.</returns>
        public static int Fit(IReadOnlyList<float> widths, float available)
        {
            if (widths.Count == 0) return 0;

            int last = widths.Count - 1;                 // Menu
            float fixedPart =
                BarPad * 2 + widths[last] + DividerWidth + DividerGap * 2;

            float room = available - fixedPart;
            int shown = 0;
            for (int i = 0; i < last; i++)
            {
                float step = widths[i] + (shown > 0 ? ItemGap : 0f);
                if (room - step < 0f) break;
                room -= step;
                shown++;
            }
            return shown;
        }

        /// <summary>
        /// The width the bar actually takes with <paramref name="shown"/> leading items and Menu.
        /// Never larger than the space it was given, which is the invariant the acceptance
        /// criterion "nothing is cut off at the right edge" reduces to.
        /// </summary>
        public static float BarWidth(IReadOnlyList<float> widths, int shown)
        {
            if (widths.Count == 0) return 0f;
            int last = widths.Count - 1;
            float total = BarPad * 2 + widths[last] + DividerWidth + DividerGap * 2;
            for (int i = 0; i < shown && i < last; i++) total += widths[i] + (i > 0 ? ItemGap : 0f);
            return total;
        }

        /// <summary>The modelled widths of the whole bar, in order.</summary>
        public static List<float> ModelWidths()
        {
            var widths = new List<float>();
            foreach (HudCommand command in All) widths.Add(Width(command));
            return widths;
        }

        /// <summary>The height of the bar: its content plus its own padding.</summary>
        public const int BarHeight = ItemHeight + BarPad * 2;

        static HudCommands()
        {
            int primaries = 0;
            foreach ((string key, string _, string __) in Order)
                if (key == BuildKey) primaries++;
            if (primaries != 1)
                throw new InvalidOperationException("the command bar has exactly one primary item");
        }
    }
}
