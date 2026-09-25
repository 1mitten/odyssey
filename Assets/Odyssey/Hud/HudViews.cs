#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// The views strip (owner, 2026-09-23): switches for what the board shows, under the orders
    /// strip in the right-hand gutter — *"a toggle toolbar like the orders that allow us to switch on
    /// and off views/settings, starting with power"*.
    ///
    /// <para><b>A switch stays where it is put.</b> The power lines already show on their own while
    /// power work is in hand and hide again when it is put down (<see cref="PowerLinesVisibility"/>);
    /// switching the view on keeps them shown whatever is armed, so a player can deconstruct around
    /// a wired room or just look, and switching it off hands the lines back to the tools. The
    /// Menu's overlay row and this button are the same switch, <see cref="OverlayDirector"/>'s.</para>
    ///
    /// <para>One row of this table per view: the strip's length, its buttons and the layout's
    /// room for it all read <see cref="Keys"/>, so the next view is one entry here and one case
    /// below.</para>
    /// </summary>
    public static class HudViews
    {
        /// <summary>The power lines (design 32 §9).</summary>
        public const string Power = "ui.overlay.power";

        /// <summary>The colony's home and its hearth (design 43 §5).</summary>
        public const string Home = "ui.overlay.home";

        /// <summary>Every view the strip offers, top to bottom. Registry keys, as everything the HUD names is.</summary>
        public static readonly string[] Keys = { Power, Home };

        /// <summary>
        /// The path a view's button draws, filled, or null for one drawn as a <c>HudGlyph</c>
        /// (power's bolt is the palette category's glyph, and stays it).
        /// </summary>
        public static string? PathOf(string key) => key switch
        {
            Home => HudIcons.Home,
            _ => null,
        };

        /// <summary>
        /// The Menu's Overlays rows, top to bottom (A12). A row is live — a switch, lit while its
        /// view is on — exactly when its key is in <see cref="Keys"/>; the rest stay dim with their
        /// reason until their channel renders. One list, so a view added to the strip is a live
        /// Menu row without a second edit (design 43 §5a).
        /// </summary>
        public static readonly string[] MenuOverlays =
        {
            "ui.overlay.temperature", "ui.overlay.light", "ui.overlay.beauty", "ui.overlay.cleanliness",
            "ui.overlay.roofs", "ui.overlay.zones", Power, Home, "ui.overlay.salvage",
            "ui.overlay.support", "ui.overlay.traffic",
        };

        /// <summary>Is this Menu overlay row a working switch?</summary>
        public static bool IsLive(string key) => System.Array.IndexOf(Keys, key) >= 0;

        /// <summary>Is this view switched on?</summary>
        public static bool IsOn(OverlayDirector overlays, string key) => key switch
        {
            Power => overlays.PowerVisible,
            Home => overlays.HomeVisible,
            _ => false,
        };

        /// <summary>Switch this view over.</summary>
        public static void Toggle(OverlayDirector overlays, string key)
        {
            switch (key)
            {
                case Power: overlays.TogglePower(); break;
                case Home: overlays.ToggleHome(); break;
            }
        }
    }
}
