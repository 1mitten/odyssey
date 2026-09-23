#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Whether the Wildlife panel is open (design 30 §6). Session state, for the reason the Work
    /// tab's director is: it is not saved, and it is not the simulation's.
    ///
    /// <para><b>Wildlife, not Animals.</b> The registry has both tabs and the bar has advertised
    /// both since M1 — <c>ui.tab.animals</c> is "tame beasts and their training" and
    /// <c>ui.tab.wildlife</c> is "what is out there". With no taming there is nothing for the
    /// first to show, so this is the second, on the F6 the bar has always promised for it, and
    /// the Animals tab arrives with taming.</para>
    /// </summary>
    public sealed class WildlifeDirector
    {
        public const string PanelKey = "ui.tab.wildlife";

        /// <summary>The four column headings.</summary>
        public const string KindKey = "ui.wildlife.kind";
        public const string DoingKey = "ui.wildlife.doing";
        public const string LayerKey = "ui.wildlife.layer";
        public const string AwayKey = "ui.wildlife.away";

        /// <summary>Every key the panel puts on screen that is its own, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys = { PanelKey, KindKey, DoingKey, LayerKey, AwayKey };

        public bool Open { get; private set; }

        public event Action? Changed;

        public void Toggle() => SetOpen(!Open);

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            Changed?.Invoke();
        }
    }

    /// <summary>The panel's fixed geometry (design 30 §6), one owner, in the pattern of <see cref="WorkGridLayout"/>.</summary>
    public static class WildlifeLayout
    {
        public const int RowHeight = 30;

        /// <summary>Rows a page shows. Bounded whatever the board carries, as the Work tab's rows are.</summary>
        public const int RowsPerPage = 12;

        public const int KindColumn = 160;
        public const int DoingColumn = 120;
        public const int LayerColumn = 56;
        public const int AwayColumn = 64;
        public const int LeftPad = 10;

        public const int PanelWidth = LeftPad + KindColumn + DoingColumn + LayerColumn + AwayColumn + LeftPad;

        /// <summary>
        /// The width to set on the panel: UI Toolkit's <c>width</c> is a border box, and the
        /// panel's padding and border are inside it (design 27 §17; the rule in CLAUDE.md).
        /// </summary>
        public const int PanelOuterWidth = PanelWidth + 2 * (HudLayout.Pad + HudTheme.BorderWidth);
    }
}
