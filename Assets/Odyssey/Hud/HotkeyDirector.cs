#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Odyssey.Hud
{
    /// <summary>
    /// A key this game is willing to bind.
    ///
    /// <para>A closed set on purpose: it is everything the rebind panel offers, so the panel
    /// can list the whole set without a fallback for "a key the list does not know", and the
    /// mapper in the Presentation assembly either hands back a member of it or nothing at
    /// all. Three kinds of key are <b>not</b> members. Escape is the unwind rule
    /// (<see cref="SettingsDirector.Escape"/>) and the way out of a rebind, so it is not a
    /// thing anything else may claim. Modifiers are the fast multiplier and the
    /// additive-selection modifier, and a scheme that can put an action <i>on</i> a modifier
    /// is a different, larger scheme. And the function keys are the command bar's, promised
    /// to panels that do not exist yet, so a binding that took one would be a clash the day
    /// the panel arrives.</para>
    /// </summary>
    public enum HudKey
    {
        None = 0,

        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

        Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9,

        Up, Down, Left, Right,
        Space, Home, End, PageUp, PageDown,
        Backquote,
    }

    /// <summary>
    /// One thing a key can do. Actions, not keys: the binding map's whole claim (design 09
    /// §3 row 23) is that "what the player pressed" and "what happens" are different
    /// questions, and only the first one should ever be a setting.
    /// </summary>
    public enum HotkeyAction
    {
        CameraForward,
        CameraBack,
        CameraRight,
        CameraLeft,
        CameraTurnLeft,
        CameraTurnRight,

        SliceUp,
        SliceDown,
        CycleAbove,
        FrameMap,

        Pause,
        Speed1,
        Speed2,
        Speed3,

        ToolMine,
        ToolFell,
        ToolCancel,

        BuildPalette,

        /// <summary>
        /// Open or close the debug menu (was "toggle the developer overlay" directly, until the
        /// overlay moved into that menu as its first row).
        /// </summary>
        DebugMenu,
    }

    /// <summary>What came of offering a key to a listening slot.</summary>
    public enum RebindResult
    {
        /// <summary>The key is the action's now, and the store says so.</summary>
        Bound,

        /// <summary>Another action already owns the key. Nothing changed.</summary>
        Conflict,

        /// <summary>The key is not one this game binds. Nothing changed.</summary>
        UnknownKey,
    }

    /// <summary>
    /// The binding map: which key performs which action, with defaults, user rebinds and
    /// conflict detection — the class design 09 §6 reserved a place for.
    ///
    /// <para><b>Why the defaults are a table in here and not a Def.</b> The design wants
    /// Def-supplied defaults eventually, but the UI Def set does not exist yet, and a second
    /// source of truth beside this table would be the thing the registry rule forbids
    /// elsewhere. When Defs arrive they generate this table; until then it is code, tested in
    /// the fast tier like every other rule the interface keeps.</para>
    ///
    /// <para><b>Two slots per action.</b> The camera pans on WASD <i>or</i> the arrows and the
    /// slice steps on R and F <i>or</i> PageUp and PageDown, and neither is a leftover: they
    /// are the two answers to "where is that key on my keyboard". A one-slot map would have
    /// to pick one and take the other away, so the map has a primary and an alternate, and
    /// an action with no alternate simply leaves the second slot empty.</para>
    ///
    /// <para><b>One context, for now.</b> The design wants contexts — world, tool, panel,
    /// text entry — where a key can mean different things. No key in this game means two
    /// things today (that is what <c>HotkeyClashTests</c> has been holding the line on), so
    /// the first cut of the director is one global map with cross-action conflict detection.
    /// The day a second use for a key exists is the day contexts earn their complexity.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.
    /// The engine half — turning a <c>UnityEngine.InputSystem</c> key into a
    /// <see cref="HudKey"/> and back — is one mapper in the Presentation assembly.</para>
    /// </summary>
    public sealed class HotkeyDirector
    {
        /// <summary>Slots per action: a primary and an alternate.</summary>
        public const int SlotCount = 2;

        /// <summary>The registry key naming the Keys section of the settings panel.</summary>
        public const string KeysKey = "ui.settings.keys";

        /// <summary>The registry key naming the reset row under the binding list.</summary>
        public const string ResetKey = "ui.settings.resetkeys";

        /// <summary>
        /// The defaults, in the order the panel draws them. Every letter the command bar
        /// avoided when it took the function keys is here because the camera or a tool
        /// already read it — this table is the answer to "what was already taken", written
        /// down at last instead of inferred from a grep.
        /// </summary>
        static readonly (HotkeyAction Action, HudKey Primary, HudKey Alternate)[] Defaults =
        {
            (HotkeyAction.CameraForward,   HudKey.W,  HudKey.Up),
            (HotkeyAction.CameraBack,      HudKey.S,  HudKey.Down),
            (HotkeyAction.CameraRight,     HudKey.D,  HudKey.Right),
            (HotkeyAction.CameraLeft,      HudKey.A,  HudKey.Left),
            (HotkeyAction.CameraTurnLeft,  HudKey.Q,  HudKey.None),
            (HotkeyAction.CameraTurnRight, HudKey.E,  HudKey.None),

            (HotkeyAction.SliceUp,   HudKey.R,       HudKey.PageUp),
            (HotkeyAction.SliceDown, HudKey.F,       HudKey.PageDown),
            (HotkeyAction.CycleAbove, HudKey.V,      HudKey.None),
            (HotkeyAction.FrameMap,  HudKey.Home,    HudKey.None),

            (HotkeyAction.Pause,  HudKey.Space,  HudKey.None),
            (HotkeyAction.Speed1, HudKey.Digit1, HudKey.None),
            (HotkeyAction.Speed2, HudKey.Digit2, HudKey.None),
            (HotkeyAction.Speed3, HudKey.Digit3, HudKey.None),

            (HotkeyAction.ToolMine,   HudKey.M, HudKey.None),
            (HotkeyAction.ToolFell,   HudKey.C, HudKey.None),
            (HotkeyAction.ToolCancel, HudKey.X, HudKey.None),

            (HotkeyAction.BuildPalette,     HudKey.B,         HudKey.None),
            (HotkeyAction.DebugMenu,        HudKey.Backquote, HudKey.None),
        };

        /// <summary>
        /// Every key the binding panel can put on screen, so <c>RegistryTests</c> holds the
        /// panel to the naming CSV the way it holds every other labelled thing.
        /// </summary>
        public static readonly string[] IconKeys = BuildIconKeys();

        static readonly HotkeyAction[] ActionOrder = BuildActionOrder();

        readonly Dictionary<HotkeyAction, HudKey[]> _bindings = new();
        readonly List<(HotkeyAction Action, int Slot)> _loadConflicts = new();

        ISettingsStore? _store;

        public HotkeyDirector()
        {
            foreach ((HotkeyAction action, HudKey primary, HudKey alternate) in Defaults)
                _bindings[action] = new[] { primary, alternate };
        }

        /// <summary>The actions, in the order the panel draws them.</summary>
        public static IReadOnlyList<HotkeyAction> All => ActionOrder;

        /// <summary>
        /// Whether <paramref name="key"/> names a key the game can bind. Everything but
        /// <see cref="HudKey.None"/> does: the enum is closed, and the mapper in the
        /// Presentation assembly is what turns an engine key into a member of it or into
        /// nothing at all.
        /// </summary>
        public static bool KeyIsKnown(HudKey key) => key != HudKey.None;

        /// <summary>
        /// The key bound to one slot of an action, or <see cref="HudKey.None"/> when the
        /// slot is empty. Slot 0 is the primary, slot 1 the alternate.
        /// </summary>
        public HudKey Key(HotkeyAction action, int slot) => _bindings[action][slot];

        /// <summary>
        /// The key an action ships on, whatever the player has since done to it. The panel
        /// says this in a tooltip — "default: W" — so a binding can always be traced back to
        /// the key the game installed.
        /// </summary>
        public static HudKey DefaultKey(HotkeyAction action, int slot)
        {
            foreach ((HotkeyAction row, HudKey primary, HudKey alternate) in Defaults)
                if (row == action) return slot == 0 ? primary : alternate;
            return HudKey.None;
        }

        /// <summary>
        /// The action that owns a key right now, or null when nobody does. Two actions on
        /// one key is a state this director refuses to build, so the answer is single.
        /// </summary>
        public HotkeyAction? OwnerOf(HudKey key)
        {
            if (key == HudKey.None) return null;
            foreach ((HotkeyAction action, _, _) in Defaults)
                for (int slot = 0; slot < SlotCount; slot++)
                    if (_bindings[action][slot] == key) return action;
            return null;
        }

        /// <summary>Which slot the panel is waiting to fill, or null when it is not.</summary>
        public (HotkeyAction Action, int Slot)? Listening { get; private set; }

        /// <summary>
        /// The text field that has the keyboard, or null when no field does.
        ///
        /// <para><b>A token rather than a flag, and the token is the point.</b> Focus moves from
        /// one field to another as a blur and a focus, and nothing promises which arrives first —
        /// so a bool would be cleared by the field being left after the field being entered had
        /// set it, and the gate would be open with a cursor blinking on screen. The token means
        /// only the field that took the keyboard can give it back, in either order.</para>
        /// </summary>
        public object? Typist { get; private set; }

        /// <summary>Whether a text field has the keyboard.</summary>
        public bool Typing => Typist != null;

        /// <summary>
        /// Whether the game's keys are the player's to press right now.
        ///
        /// <para><b>Every poller asks this and nothing else.</b> There are six of them — the camera
        /// rig, the designate presenter, the command bar, and the rest — and each used to ask
        /// <c>Listening != null</c> in its own words. That was one rule with six copies, and the
        /// day a second reason to sit a frame out arrived, five of them would have kept typing the
        /// player's save name into the camera (owner, 2026-09-18: *"when you type in during a save
        /// game the in game controls still work and can cause confusion"*). Typing "sss" panned the
        /// camera, armed a tool and changed the game speed, because a UI Toolkit field's focus
        /// cannot gate a poll of <c>Keyboard.current</c> — the field never sees the key at all.</para>
        ///
        /// <para>Escape is deliberately not covered: it is read directly rather than through a
        /// binding, and while a field has the keyboard it belongs to the field, which is where
        /// that rule lives.</para>
        /// </summary>
        public bool GameKeysLive => Listening == null && Typist == null;

        /// <summary>
        /// Take the keyboard for a text field. Idempotent, and a second field taking it from the
        /// first simply takes it — the last field to be focused is the one that has it.
        /// </summary>
        public void BeginTyping(object field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            Typist = field;
        }

        /// <summary>
        /// Give the keyboard back, if this field is the one holding it. A field that has already
        /// lost it to another says nothing, which is what makes the blur-after-focus ordering
        /// harmless.
        /// </summary>
        public void EndTyping(object field)
        {
            if (ReferenceEquals(Typist, field)) Typist = null;
        }

        /// <summary>
        /// Give the keyboard back whoever holds it. For the closing of a screen that may have
        /// taken it without ever being blurred — a hidden element does not always raise a focus
        /// event, and a gate left shut is a game that has stopped answering its keys.
        /// </summary>
        public void StopTyping() => Typist = null;

        /// <summary>
        /// What came of the last <see cref="Capture"/>, for the panel that has to say why a
        /// key did not take. Set before <see cref="ListenChanged"/> is raised, so a handler
        /// reads it in the same breath.
        /// </summary>
        public RebindResult LastResult { get; private set; }

        /// <summary>The action that owned the refused key, when <see cref="LastResult"/> is
        /// <see cref="RebindResult.Conflict"/>.</summary>
        public HotkeyAction? LastConflictOwner { get; private set; }

        /// <summary>
        /// Slots a stored preference was refused for, from the last <see cref="UseStore"/>.
        /// Reported rather than silently resolved — a key two lines claim, or a line that
        /// claims a key a default ships on, is a file worth knowing about, and the slot
        /// keeps its default while it is sorted out.
        /// </summary>
        public IReadOnlyList<(HotkeyAction Action, int Slot)> LoadConflicts => _loadConflicts;

        /// <summary>Raised when a slot's key changes, with the action that changed.</summary>
        public event Action<HotkeyAction>? BindingChanged;

        /// <summary>Raised when the panel starts or stops waiting for a key.</summary>
        public event Action? ListenChanged;

        /// <summary>
        /// Raised when a key was offered to a listening slot and refused because another
        /// action owns it, with the action that was refused and the one that holds the key.
        /// </summary>
        public event Action<HotkeyAction, HotkeyAction>? ConflictNoted;

        /// <summary>The registry key naming this action. Never a word: words live in the CSV.</summary>
        public static string KeyOf(HotkeyAction action) => action switch
        {
            HotkeyAction.CameraForward => "ui.keys.forward",
            HotkeyAction.CameraBack => "ui.keys.back",
            HotkeyAction.CameraRight => "ui.keys.right",
            HotkeyAction.CameraLeft => "ui.keys.left",
            HotkeyAction.CameraTurnLeft => "ui.keys.turnleft",
            HotkeyAction.CameraTurnRight => "ui.keys.turnright",
            HotkeyAction.SliceUp => "ui.keys.sliceup",
            HotkeyAction.SliceDown => "ui.keys.slicedown",
            HotkeyAction.CycleAbove => "ui.keys.cycleabove",
            HotkeyAction.FrameMap => "ui.keys.frame",
            HotkeyAction.Pause => "ui.keys.pause",
            HotkeyAction.Speed1 => "ui.keys.speed1",
            HotkeyAction.Speed2 => "ui.keys.speed2",
            HotkeyAction.Speed3 => "ui.keys.speed3",
            HotkeyAction.ToolMine => "ui.keys.mine",
            HotkeyAction.ToolFell => "ui.keys.fell",
            HotkeyAction.ToolCancel => "ui.keys.cancel",
            HotkeyAction.BuildPalette => "ui.keys.build",
            HotkeyAction.DebugMenu => "ui.keys.debugmenu",
            _ => KeysKey,
        };

        /// <summary>
        /// What a key is called on a cap: <c>W</c>, <c>PgUp</c>, the backquote character
        /// itself. Letters and the digits they share a keycap with name themselves; the empty
        /// slot's name is the panel's business, not the key's.
        /// </summary>
        public static string Display(HudKey key) => key switch
        {
            HudKey.None => "",
            HudKey.Digit0 => "0",
            HudKey.Digit1 => "1",
            HudKey.Digit2 => "2",
            HudKey.Digit3 => "3",
            HudKey.Digit4 => "4",
            HudKey.Digit5 => "5",
            HudKey.Digit6 => "6",
            HudKey.Digit7 => "7",
            HudKey.Digit8 => "8",
            HudKey.Digit9 => "9",
            HudKey.Up => "Up",
            HudKey.Down => "Down",
            HudKey.Left => "Left",
            HudKey.Right => "Right",
            HudKey.PageUp => "PgUp",
            HudKey.PageDown => "PgDn",
            HudKey.Backquote => "`",
            _ => key.ToString(),
        };

        /// <summary>
        /// Start waiting for the player to press a key for one slot. Nothing is captured
        /// until <see cref="Capture"/> is offered a key, and Escape cancels the wait
        /// (<see cref="ConsumeEscape"/>) rather than unwinding whatever is open underneath.
        /// </summary>
        public void Listen(HotkeyAction action, int slot)
        {
            if (Listening == (action, slot)) return;
            Listening = (action, slot);
            ListenChanged?.Invoke();
        }

        public void CancelListen()
        {
            if (Listening == null) return;
            Listening = null;
            ListenChanged?.Invoke();
        }

        /// <summary>
        /// Whether Escape just cancelled a rebind. The caller reads the Escape key once and
        /// asks this first, so the rule "Escape backs out of the rebind before it touches
        /// anything else" lives here, in the fast tier, instead of in whichever component
        /// happened to be holding the key this week.
        /// </summary>
        public bool ConsumeEscape()
        {
            if (Listening == null) return false;
            CancelListen();
            return true;
        }

        /// <summary>
        /// Offer a pressed key to the listening slot.
        ///
        /// <para><b>A key owned by another action is refused, not swapped.</b> Silently
        /// stealing the key would fix the clash the player can see and create the one they
        /// cannot, so the panel says who has it and leaves both bindings alone (design 09
        /// §6: conflicts are detected and reported, not silently resolved). The one
        /// exception is the same action's other slot, where "moving" the key is the whole
        /// of what was asked.</para>
        /// </summary>
        public RebindResult Capture(HudKey key)
        {
            if (Listening == null || !KeyIsKnown(key))
            {
                LastResult = RebindResult.UnknownKey;
                LastConflictOwner = null;
                return RebindResult.UnknownKey;
            }
            (HotkeyAction action, int slot) = Listening.Value;

            if (_bindings[action][slot] == key)
            {
                // Pressing the key the slot already holds is a no-op with the door closed.
                LastResult = RebindResult.Bound;
                LastConflictOwner = null;
                CancelListen();
                return RebindResult.Bound;
            }

            HotkeyAction? owner = OwnerOf(key);
            if (owner != null && owner != action)
            {
                // The slot keeps listening: a refused key is a wrong answer to a question
                // still being asked, not the end of it, and the player should be able to
                // offer the next key without clicking the slot open again.
                LastResult = RebindResult.Conflict;
                LastConflictOwner = owner;
                ConflictNoted?.Invoke(action, owner.Value);
                return RebindResult.Conflict;
            }

            if (owner == action)
                _bindings[action][OtherSlot(slot)] = HudKey.None;

            _bindings[action][slot] = key;
            Write(action);
            BindingChanged?.Invoke(action);
            LastResult = RebindResult.Bound;
            LastConflictOwner = null;
            CancelListen();
            return RebindResult.Bound;
        }

        /// <summary>Empty one slot. An action with both slots empty simply has no key.</summary>
        public void ClearSlot(HotkeyAction action, int slot)
        {
            if (_bindings[action][slot] == HudKey.None) return;
            _bindings[action][slot] = HudKey.None;
            Write(action);
            BindingChanged?.Invoke(action);
        }

        /// <summary>
        /// Put every action back on its default keys, and write the defaults down, so the
        /// store cannot hold a half-reset.
        /// </summary>
        public void ResetKeys()
        {
            foreach ((HotkeyAction action, HudKey primary, HudKey alternate) in Defaults)
            {
                if (_bindings[action][0] == primary && _bindings[action][1] == alternate) continue;
                _bindings[action][0] = primary;
                _bindings[action][1] = alternate;
                Write(action);
                BindingChanged?.Invoke(action);
            }
            CancelListen();
        }

        /// <summary>
        /// Attach the place bindings are kept, and lay anything this machine has already
        /// been told over the defaults.
        ///
        /// <para>Everybody starts on the defaults and works from there, because the defaults
        /// cannot clash with each other (that is tested above) and so the walk only ever has
        /// to keep a valid map valid. A stored line is applied in draw order and a key
        /// already owned — by a default or by an earlier line — is refused for that slot:
        /// recorded in <see cref="LoadConflicts"/>, the slot keeping its default. A line
        /// nobody can parse is dropped whole rather than obeyed, so a key renamed between
        /// builds costs one binding, not the file.</para>
        /// </summary>
        public void UseStore(ISettingsStore store)
        {
            _store = store;
            _loadConflicts.Clear();

            foreach ((HotkeyAction action, HudKey primary, HudKey alternate) in Defaults)
                _bindings[action] = new[] { primary, alternate };

            foreach ((HotkeyAction action, _, _) in Defaults)
            {
                string? stored = store.ReadString(KeyOf(action));
                if (stored == null) continue;

                var wanted = new HudKey[SlotCount];
                int parsed = 0;
                foreach (string name in stored.Split('+'))
                {
                    if (parsed >= SlotCount) break;
                    if (!Enum.TryParse(name, out HudKey key) || !KeyIsKnown(key)) continue;
                    wanted[parsed++] = key;
                }

                // A non-empty line that parsed to nothing is garbage, not a wish. An empty
                // line is a wish: both slots cleared on purpose.
                if (stored.Length > 0 && parsed == 0) continue;
                ApplyStored(action, wanted);
            }
        }

        /// <summary>
        /// Lay one stored line over the map, refusing — not stealing — any key another
        /// action is already on. The same bargain <see cref="Capture"/> makes with the
        /// player, made here with the file.
        /// </summary>
        void ApplyStored(HotkeyAction action, HudKey[] wanted)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                HudKey key = wanted[slot];
                if (key == HudKey.None)
                {
                    _bindings[action][slot] = HudKey.None;
                    continue;
                }

                HotkeyAction? owner = OwnerOf(key);
                if (owner != null && owner != action)
                {
                    _loadConflicts.Add((action, slot));
                    continue;
                }

                if (owner == action) _bindings[action][OtherSlot(slot)] = HudKey.None;
                _bindings[action][slot] = key;
            }
        }

        void Write(HotkeyAction action)
        {
            if (_store == null) return;
            var text = new StringBuilder();
            for (int slot = 0; slot < SlotCount; slot++)
            {
                HudKey key = _bindings[action][slot];
                if (key == HudKey.None) continue;
                if (text.Length > 0) text.Append('+');
                text.Append(key);
            }
            _store.WriteString(KeyOf(action), text.ToString());
        }

        static int OtherSlot(int slot) => slot == 0 ? 1 : 0;

        static string[] BuildIconKeys()
        {
            var keys = new string[Defaults.Length + 2];
            for (int i = 0; i < Defaults.Length; i++) keys[i] = KeyOf(Defaults[i].Action);
            keys[Defaults.Length] = KeysKey;
            keys[Defaults.Length + 1] = ResetKey;
            return keys;
        }

        static HotkeyAction[] BuildActionOrder()
        {
            var actions = new HotkeyAction[Defaults.Length];
            for (int i = 0; i < Defaults.Length; i++) actions[i] = Defaults[i].Action;
            return actions;
        }
    }
}
