#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// The graphics levers the panel can throw. The order is the order they are drawn in.
    ///
    /// <para>Every one of these is <b>decoration</b>: none is a cell, none is in the save and none
    /// reaches the hash. That is why a settings panel may write them at all — it bypasses the
    /// intent queue (`10-ui-panel-catalogue.md` B17), which no panel that touched the simulation
    /// would be allowed to do.</para>
    /// </summary>
    public enum GraphicsOption
    {
        Shadows,
        Surround,
        GrassTufts,
        GroundRelief,
        SeeThrough,
    }

    /// <summary>
    /// What pressing Escape should do, given what is open. Returned rather than performed because
    /// the order is a rule worth testing in the fast tier, and the doing of it needs Unity.
    /// </summary>
    public enum EscapeAction
    {
        DisarmTool,
        ClosePanel,
        OpenPanel,
    }

    /// <summary>
    /// Where a graphics preference is kept between sessions.
    ///
    /// <para>An interface rather than a call to <c>PlayerPrefs</c> because this assembly is
    /// compiled without UnityEngine (ADR 0003) and must keep running in the fast tier. The
    /// implementation lives in the Presentation assembly, which is allowed to know about Unity.
    /// </para>
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>The stored value, or null if this machine has never been told.</summary>
        bool? Read(string key);

        void Write(string key, bool value);
    }

    /// <summary>
    /// The settings panel: whether it is open, and the graphics options it holds.
    ///
    /// <para><b>Why a director owns this at all.</b> Until now every graphics lever was a field on
    /// <c>OdysseyBootstrap</c>, which means stopping play, changing a number and pressing play
    /// again — once per variable. Every look decision this project has made ended with a note that
    /// it still wanted the owner's eye in the play scene, and judging one is really judging A
    /// against B. A panel that throws the lever while the colony runs turns a session per question
    /// into a question per session. It is also the surface the quality tier needs, since the
    /// golden-hour work has already committed to a Low tier that drops the expensive effects.</para>
    ///
    /// <para><b>Two kinds of lever, and the difference is not cosmetic.</b> Shadows and the
    /// surround are read as the frame is drawn, so throwing them is free and instant. Grass tufts
    /// and ground relief are baked into the instance matrices when a chunk is meshed, so throwing
    /// them means meshing the board again. <see cref="NeedsRedraw"/> is how the presenter knows
    /// which it is holding, and the panel says so beside the row rather than leaving the player to
    /// wonder why one toggle stutters and three do not.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class SettingsDirector
    {
        /// <summary>The registry key naming the panel itself.</summary>
        public const string PanelKey = "ui.settings.panel";

        /// <summary>The registry key naming the one section the panel has so far.</summary>
        public const string GraphicsKey = "ui.settings.graphics";

        static readonly GraphicsOption[] Order =
        {
            GraphicsOption.Shadows,
            GraphicsOption.Surround,
            GraphicsOption.GrassTufts,
            GraphicsOption.GroundRelief,
            GraphicsOption.SeeThrough,
        };

        /// <summary>
        /// Every key this panel can put on screen, so <c>RegistryTests</c> can hold the whole
        /// panel to the naming CSV the way it already holds the ledger and the job list. A label
        /// invented in C# is a label the owner cannot correct.
        /// </summary>
        public static readonly string[] IconKeys =
        {
            PanelKey,
            GraphicsKey,
            "ui.settings.shadows",
            "ui.settings.surround",
            "ui.settings.grass",
            "ui.settings.relief",
            "ui.settings.seethrough",
        };

        readonly Dictionary<GraphicsOption, bool> _on = new();

        ISettingsStore? _store;

        public SettingsDirector()
        {
            foreach (GraphicsOption option in Order) _on[option] = true;
        }

        /// <summary>The options, in the order they are drawn.</summary>
        public static IReadOnlyList<GraphicsOption> All => Order;

        public bool Open { get; private set; }

        /// <summary>Raised when the panel opens or closes.</summary>
        public event Action? Changed;

        /// <summary>Raised when one option's value changes, with the option that changed.</summary>
        public event Action<GraphicsOption>? OptionChanged;

        public bool IsOn(GraphicsOption option) => _on.TryGetValue(option, out bool on) && on;

        /// <summary>
        /// Whether throwing this lever costs a remesh of the board. True for anything baked into
        /// an instance matrix, false for anything read as the frame is drawn.
        /// </summary>
        public static bool NeedsRedraw(GraphicsOption option) =>
            option is GraphicsOption.GrassTufts or GraphicsOption.GroundRelief;

        /// <summary>The registry key naming this option. Never a word: words live in the CSV.</summary>
        public static string KeyOf(GraphicsOption option) => option switch
        {
            GraphicsOption.Shadows => "ui.settings.shadows",
            GraphicsOption.Surround => "ui.settings.surround",
            GraphicsOption.GrassTufts => "ui.settings.grass",
            GraphicsOption.GroundRelief => "ui.settings.relief",
            GraphicsOption.SeeThrough => "ui.settings.seethrough",
            _ => "ui.settings.panel",
        };

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            Changed?.Invoke();
        }

        public void Toggle() => SetOpen(!Open);

        public void Set(GraphicsOption option, bool on)
        {
            if (IsOn(option) == on) return;
            _on[option] = on;
            _store?.Write(KeyOf(option), on);
            OptionChanged?.Invoke(option);
        }

        public void Toggle(GraphicsOption option) => Set(option, !IsOn(option));

        /// <summary>
        /// Record what the scene was already configured to do, without raising anything and
        /// without writing it back.
        ///
        /// <para>The inspector fields on the bootstrap stay the source of truth for how a session
        /// starts, so the panel opens describing the board that is actually on screen. A panel
        /// whose defaults quietly overrode the scene would be a panel that changed the game by
        /// existing.</para>
        /// </summary>
        public void Seed(GraphicsOption option, bool on) => _on[option] = on;

        /// <summary>
        /// Attach the place preferences are kept, and apply anything this machine has already been
        /// told. Stored values are laid over the seeded ones, so a preference beats the scene and
        /// the scene beats nothing at all.
        /// </summary>
        public void UseStore(ISettingsStore store)
        {
            _store = store;
            foreach (GraphicsOption option in Order)
            {
                bool? stored = store.Read(KeyOf(option));
                if (stored.HasValue) Set(option, stored.Value);
            }
        }

        /// <summary>
        /// What Escape means right now. The order is fixed by `09-ui-and-input.md` §6: cancel the
        /// active tool first, then close the top panel, and only then open the menu.
        ///
        /// <para>It is decided here, in one place, rather than in each component that happens to
        /// read the key. Escape was already spoken for by the designate tool, and a second
        /// listener would have disarmed the tool and opened the panel in the same keystroke —
        /// the sort of fault that looks like a flicker and is diagnosed as a rendering bug.</para>
        /// </summary>
        public EscapeAction Escape(bool toolArmed)
        {
            if (toolArmed) return EscapeAction.DisarmTool;
            return Open ? EscapeAction.ClosePanel : EscapeAction.OpenPanel;
        }
    }
}
