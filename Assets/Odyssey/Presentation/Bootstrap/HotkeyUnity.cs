#nullable enable
using Odyssey.Hud;
using UnityEngine.InputSystem;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// The one place the engine's keys and the binding map's keys meet.
    ///
    /// <para><see cref="HudKey"/> is a closed set so the rebind panel can offer the whole of
    /// it; this is the seam that keeps it closed — every engine key either maps to a member
    /// of the set or maps to nothing, and nothing outside <see cref="ToHud"/> ever decides
    /// what a game key means. Both directions are total on purpose: <see cref="ToKey"/> is
    /// the pollers' half and may never miss (an unbindable member would be a key that
    /// rebinding could strand an action on), and <see cref="ToHud"/> is the capture half,
    /// where "nothing" is the honest answer for every key the game will not bind.</para>
    ///
    /// <para>It is a mapper and nothing else. The moment it grows an opinion — about
    /// contexts, about who may listen — that opinion belongs in
    /// <see cref="HotkeyDirector"/>, where the fast tier can hold it.</para>
    /// </summary>
    public static class HotkeyUnity
    {
        /// <summary>
        /// The engine key a binding is read from. Total: every member of
        /// <see cref="HudKey"/> except <see cref="HudKey.None"/>, which no poller ever
        /// asks about.
        /// </summary>
        public static Key ToKey(HudKey key) => key switch
        {
            HudKey.A => Key.A,
            HudKey.B => Key.B,
            HudKey.C => Key.C,
            HudKey.D => Key.D,
            HudKey.E => Key.E,
            HudKey.F => Key.F,
            HudKey.G => Key.G,
            HudKey.H => Key.H,
            HudKey.I => Key.I,
            HudKey.J => Key.J,
            HudKey.K => Key.K,
            HudKey.L => Key.L,
            HudKey.M => Key.M,
            HudKey.N => Key.N,
            HudKey.O => Key.O,
            HudKey.P => Key.P,
            HudKey.Q => Key.Q,
            HudKey.R => Key.R,
            HudKey.S => Key.S,
            HudKey.T => Key.T,
            HudKey.U => Key.U,
            HudKey.V => Key.V,
            HudKey.W => Key.W,
            HudKey.X => Key.X,
            HudKey.Y => Key.Y,
            HudKey.Z => Key.Z,
            HudKey.Digit0 => Key.Digit0,
            HudKey.Digit1 => Key.Digit1,
            HudKey.Digit2 => Key.Digit2,
            HudKey.Digit3 => Key.Digit3,
            HudKey.Digit4 => Key.Digit4,
            HudKey.Digit5 => Key.Digit5,
            HudKey.Digit6 => Key.Digit6,
            HudKey.Digit7 => Key.Digit7,
            HudKey.Digit8 => Key.Digit8,
            HudKey.Digit9 => Key.Digit9,
            HudKey.Up => Key.UpArrow,
            HudKey.Down => Key.DownArrow,
            HudKey.Left => Key.LeftArrow,
            HudKey.Right => Key.RightArrow,
            HudKey.Space => Key.Space,
            HudKey.Home => Key.Home,
            HudKey.End => Key.End,
            HudKey.PageUp => Key.PageUp,
            HudKey.PageDown => Key.PageDown,
            HudKey.Backquote => Key.Backquote,
            HudKey.F1 => Key.F1,
            HudKey.F9 => Key.F9,
            _ => Key.None,
        };

        /// <summary>
        /// The binding-map key an engine key names, or null when the game will not bind it —
        /// Escape, the modifiers, and everything else the closed set leaves out. <b>The function
        /// keys were listed here as excluded until the Work tab arrived on F1</b> (design 27):
        /// one is bindable now because one has a panel behind it, and the rest follow as theirs
        /// are built.
        /// </summary>
        public static HudKey? ToHud(Key key) => key switch
        {
            Key.A => HudKey.A,
            Key.B => HudKey.B,
            Key.C => HudKey.C,
            Key.D => HudKey.D,
            Key.E => HudKey.E,
            Key.F => HudKey.F,
            Key.G => HudKey.G,
            Key.H => HudKey.H,
            Key.I => HudKey.I,
            Key.J => HudKey.J,
            Key.K => HudKey.K,
            Key.L => HudKey.L,
            Key.M => HudKey.M,
            Key.N => HudKey.N,
            Key.O => HudKey.O,
            Key.P => HudKey.P,
            Key.Q => HudKey.Q,
            Key.R => HudKey.R,
            Key.S => HudKey.S,
            Key.T => HudKey.T,
            Key.U => HudKey.U,
            Key.V => HudKey.V,
            Key.W => HudKey.W,
            Key.X => HudKey.X,
            Key.Y => HudKey.Y,
            Key.Z => HudKey.Z,
            Key.Digit0 => HudKey.Digit0,
            Key.Digit1 => HudKey.Digit1,
            Key.Digit2 => HudKey.Digit2,
            Key.Digit3 => HudKey.Digit3,
            Key.Digit4 => HudKey.Digit4,
            Key.Digit5 => HudKey.Digit5,
            Key.Digit6 => HudKey.Digit6,
            Key.Digit7 => HudKey.Digit7,
            Key.Digit8 => HudKey.Digit8,
            Key.Digit9 => HudKey.Digit9,
            Key.UpArrow => HudKey.Up,
            Key.DownArrow => HudKey.Down,
            Key.LeftArrow => HudKey.Left,
            Key.RightArrow => HudKey.Right,
            Key.Space => HudKey.Space,
            Key.Home => HudKey.Home,
            Key.End => HudKey.End,
            Key.PageUp => HudKey.PageUp,
            Key.PageDown => HudKey.PageDown,
            Key.Backquote => HudKey.Backquote,
            Key.F1 => HudKey.F1,
            Key.F9 => HudKey.F9,
            _ => null,
        };

        /// <summary>
        /// Whether one action's key is held right now, on either of its slots. The poller's
        /// whole sentence: no key names in the game's code, only actions.
        /// </summary>
        public static bool IsPressed(this Keyboard keyboard, HotkeyDirector hotkeys, HotkeyAction action)
        {
            for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
            {
                HudKey key = hotkeys.Key(action, slot);
                if (key != HudKey.None && keyboard[ToKey(key)].isPressed) return true;
            }
            return false;
        }

        /// <summary>Whether one action's key went down this frame, on either of its slots.</summary>
        public static bool WasPressedThisFrame(this Keyboard keyboard, HotkeyDirector hotkeys, HotkeyAction action)
        {
            for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
            {
                HudKey key = hotkeys.Key(action, slot);
                if (key != HudKey.None && keyboard[ToKey(key)].wasPressedThisFrame) return true;
            }
            return false;
        }
    }
}
