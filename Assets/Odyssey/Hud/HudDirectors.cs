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
        public SettingsDirector Settings { get; } = new SettingsDirector();
        public HotkeyDirector Hotkeys { get; } = new HotkeyDirector();

        /// <summary>
        /// The standing order the player is about to give.
        ///
        /// <para>Here rather than owned by <c>DesignatePresenter</c>, which is where it used to
        /// live, because two things arm a tool now: the keys the presenter reads, and the Build
        /// palette. Two directors would be two answers to "what is armed" and the palette would
        /// light a button the world did not agree with.</para>
        /// </summary>
        public DesignateDirector Designate { get; } = new DesignateDirector();

        public HudDirectors(int layerCount, int startLayer)
        {
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

        /// <summary>Once per interface frame, before anything reads the selection.</summary>
        public void Refresh(WorldSnapshot snapshot) => Selection.Refresh(snapshot);
    }
}
