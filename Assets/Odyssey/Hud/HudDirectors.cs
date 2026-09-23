#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The interface directors that exist so far, built together so that the rules between them
    /// live in one place: a layer change clears the selection, and choosing a colonist from the
    /// roster moves the slice to their layer, selects them, and sends the camera to them, in that
    /// order. Presenters in the Unity assembly hold one of these and realise what it decides.
    ///
    /// Unity-free by construction (ADR 0003): everything here runs in the fast tier.
    /// </summary>
    public sealed class HudDirectors
    {
        public SelectionDirector Selection { get; } = new SelectionDirector();
        public SliceDirector Slice { get; } = new SliceDirector();
        public CameraDirector Camera { get; } = new CameraDirector();
        public OverlayDirector Overlays { get; } = new OverlayDirector();

        /// <summary>Whether the debug menu is open. Session state — see the class doc.</summary>
        public DebugDirector Debug { get; } = new DebugDirector();

        /// <summary>Whether the Work tab is open, and how it reads (design 27). Session state,
        /// for the reason <see cref="Debug"/> is.</summary>
        public WorkDirector Work { get; } = new WorkDirector();

        /// <summary>Whether the Inventory tab is open (design 35). Session state, likewise.</summary>
        public InventoryDirector Inventory { get; } = new InventoryDirector();

        /// <summary>Whether the Research tab is open, and the research until the mechanism exists (design 34).</summary>
        public ResearchDirector Research { get; } = new ResearchDirector();

        /// <summary>Whether the Almanac reference browser is open, and what entry it shows.</summary>
        public AlmanacDirector Almanac { get; } = new AlmanacDirector();

        /// <summary>
        /// The settings panel's levers — and <b>handed in rather than made here since U38</b>,
        /// because they are not session state.
        ///
        /// <para>Everything in <see cref="SettingsDirector"/> and <see cref="HotkeyDirector"/> is a
        /// preference about this machine: how large the interface is drawn, how fast the camera
        /// moves, how loud each bus is, what the keys do. None of it is a fact about a colony, none
        /// of it is saved with one, and a player who sets their interface scale and then starts a
        /// second colony has not changed their mind about it.</para>
        ///
        /// <para><b>What forced the change was the start screen.</b> These directors used to be
        /// built here, so they existed only while a session did — and the start screen exists
        /// precisely when one does not, which would have left its Options row with no panel behind
        /// it. Two instances would have been the other way out, and the wrong one: two answers to
        /// "how large is the interface" is a setting that appears not to stick.</para>
        /// </summary>
        public SettingsDirector Settings { get; }

        /// <summary>The key bindings. Hoisted with <see cref="Settings"/>, for the same reason.</summary>
        public HotkeyDirector Hotkeys { get; }

        /// <summary>
        /// The standing order the player is about to give.
        ///
        /// <para>Here rather than owned by <c>DesignatePresenter</c>, which is where it used to
        /// live, because two things arm a tool now: the keys the presenter reads, and the Build
        /// palette. Two directors would be two answers to "what is armed" and the palette would
        /// light a button the world did not agree with.</para>
        /// </summary>
        public DesignateDirector Designate { get; } = new DesignateDirector();

        /// <summary>
        /// A session's directors with fresh preferences behind them. What every test and every
        /// harness that builds one world and looks at it wants, and what the composition root
        /// deliberately does not use — see the other constructor.
        /// </summary>
        public HudDirectors(int layerCount, int startLayer)
            : this(layerCount, startLayer, new SettingsDirector(), new HotkeyDirector())
        {
        }

        /// <summary>
        /// A session's directors over preferences that outlive the session (U38). The composition
        /// root holds one <see cref="SettingsDirector"/> and one <see cref="HotkeyDirector"/> for
        /// as long as the game is running and hands the same pair to every session it builds.
        /// </summary>
        public HudDirectors(int layerCount, int startLayer, SettingsDirector settings, HotkeyDirector hotkeys)
        {
            Settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
            Hotkeys = hotkeys ?? throw new System.ArgumentNullException(nameof(hotkeys));
            Slice.Bind(layerCount, startLayer);
            Slice.LayerChanged += _ => Selection.OnLayerChanged();
        }

        /// <summary>
        /// The roster path: a card is clicked for a colonist who may be anywhere, so this takes
        /// the player to them — their layer first, since the picker will not look through a
        /// floor, then the selection, then a camera jump to their cell at the current zoom. A
        /// world click does none of the moving; that colonist is already under the cursor, and a
        /// view that shifts under a click is the camera fighting the player.
        /// </summary>
        public bool ChooseColonist(PawnId id, WorldSnapshot snapshot)
        {
            if (!snapshot.TryGetPawn(id, out PawnView view)) return false;
            Slice.SetLayer(view.Cell.Y);
            Selection.Choose(id);
            Camera.JumpTo(view.Cell);
            return true;
        }

        /// <summary>
        /// The Inventory tab's Go (design 35): a store may be on any layer, so this is the roster's
        /// path rather than the Animals tab's — the slice to its layer first, since the picker and
        /// the pane will not look through a floor, then the cell as the selection, so the pane
        /// opens on the store, then the camera.
        /// </summary>
        public void ChooseStore(CellRef cell)
        {
            Slice.SetLayer(cell.Y);
            Selection.ChooseCell(cell);
            Camera.JumpTo(cell);
        }

        /// <summary>Once per interface frame, before anything reads the selection.</summary>
        public void Refresh(WorldSnapshot snapshot) => Selection.Refresh(snapshot);
    }
}
