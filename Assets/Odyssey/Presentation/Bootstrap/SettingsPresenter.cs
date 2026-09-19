#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// Escape opens the settings panel, throwing a lever in it changes what is drawn or
    /// sounded, offering a key to a waiting slot rebinds it, and the exit row, asked twice,
    /// leaves.
    ///
    /// <para><b>Why this exists.</b> Every graphics lever in the project was an inspector field on
    /// <see cref="OdysseyBootstrap"/>, which means stop, edit, play — once per variable, and the
    /// board is gone and rebuilt between the two pictures you are trying to compare. Most of the
    /// look decisions on record ended with a note that they still wanted the owner's eye in the
    /// play scene. This makes the comparison a keystroke.</para>
    ///
    /// <para><b>It decides nothing.</b> What the panel holds and what Escape means are
    /// <see cref="SettingsDirector"/>'s, Unity-free and covered by the fast tier. What is left
    /// here is the half that needs an engine: reading the key, and turning a boolean into a call
    /// on the renderer. That is the ADR 0003 split.</para>
    ///
    /// <para><b>The panel is not modal, and that is deliberate.</b> A modal would swallow the
    /// camera, and the entire purpose of the panel is watching the board while a lever moves. So
    /// the world keeps running, the camera keeps orbiting, and clicks that land on the panel are
    /// already kept off the world by the shell's existing pointer gate. The price is that the tool
    /// keys still work with the panel open. `09-ui-and-input.md` §6 case 5 describes a real modal;
    /// nothing here is it, and the settings panel is not the place to build one.</para>
    /// </summary>
    [RequireComponent(typeof(OdysseyBootstrap))]
    public sealed class SettingsPresenter : MonoBehaviour
    {
        OdysseyBootstrap? _bootstrap;
        DesignatePresenter? _designate;
        Ui.HudShell? _shell;
        SettingsDirector? _director;

        // What "on" means for the two levers that carry an amount rather than a state. Captured
        // from the scene at startup so that turning grass back on restores the density this board
        // was built with, not a number invented here.
        int _grassDensity = 60;
        float _reliefAmplitude = GroundRelief.BoardAmplitude;

        void Awake()
        {
            _bootstrap = GetComponent<OdysseyBootstrap>();
            _designate = GetComponent<DesignatePresenter>();
            _shell = GetComponent<Ui.HudShell>();
        }

        /// <summary>Whether the overlay row has been seeded and subscribed. See <see cref="HookDeveloperOverlay"/>.</summary>
        bool _overlayHooked;

        /// <summary>
        /// Seed the developer-overlay row from the board and follow it thereafter, the first frame
        /// there is a board to ask.
        ///
        /// <para>Separate from <see cref="Attach"/> because the two wait on different things.
        /// Attach waits on <c>Preferences</c>, which exists before any session does; this waits on
        /// <c>Directors</c>, which does not exist until one is running. Folding them together
        /// would mean either an Options panel nothing drives on the start screen, or a null
        /// dereference once a game begins — the two faults this pair exists to avoid, and the
        /// second of which shipped in the first player build.</para>
        ///
        /// <para>Idempotent, and cheap on the frames it does nothing: two null checks.</para>
        /// </summary>
        void HookDeveloperOverlay()
        {
            if (_overlayHooked || _director == null) return;

            OverlayDirector? overlays = _bootstrap?.Directors?.Overlays;
            if (overlays == null) return;

            _overlayHooked = true;
            _director.SeedDeveloperOverlay(overlays.DeveloperVisible);
            overlays.Changed += SyncDeveloperOverlay;
            ApplyDeveloperOverlay();
        }

        void Update()
        {
            Attach();
            HookDeveloperOverlay();

            Keyboard? keys = Keyboard.current;
            if (keys == null || _director == null) return;
            HotkeyDirector? hotkeys = _bootstrap?.Directors?.Hotkeys;

            // A slot in the Keys tab is waiting for its key. This frame's press belongs to
            // the rebind and to nothing else — the pollers sit the frame out too, each at
            // its own post — and Escape, which elsewhere unwinds what is open, here cancels
            // the wait. That order is the director's, decided before anything else sees the
            // key.
            if (hotkeys != null && hotkeys.Listening != null)
            {
                if (keys.escapeKey.wasPressedThisFrame) hotkeys.ConsumeEscape();
                else Capture(keys, hotkeys);
                return;
            }

            // A text field has the keyboard, so Escape belongs to it: it backs out of the name
            // being typed, not out of whatever is open behind the prompt. The field registers for
            // the key itself (HudShell.TakesTheKeyboard), which is why this is a return rather
            // than a call — the alternative is this component knowing which field is focused and
            // what backing out of it means, which is the prompt's business and not its.
            if (hotkeys != null && hotkeys.Typing) return;

            if (!keys.escapeKey.wasPressedThisFrame) return;

            // One key, one rule, one place. The order itself is the director's and is tested
            // without an engine; all that happens here is the doing of it.
            switch (_director.Escape(
                        _designate != null && _designate.ToolArmed,
                        _shell != null && _shell.BuildPaletteOpen,
                        _shell != null && _shell.MenuOpen))
            {
                case EscapeAction.DisarmTool:
                    _designate?.PutToolAway();
                    break;
                case EscapeAction.CloseMenu:
                    // Every window can be escaped (owner, 2026-09-17). The Menu popover was the
                    // one that could not: it had no place in this order and no X, so the only way
                    // to shut it was to press the button that opened it.
                    _shell?.CloseMenu();
                    break;
                case EscapeAction.ClosePalette:
                    // The Build palette is a panel opened over the board by the Build command, so
                    // it unwinds before the menu does. It had no place in this order until the
                    // interface rebuild, because it used to be a column pinned to the left edge
                    // and permanently open.
                    _shell?.CloseBuildPalette();
                    break;
                case EscapeAction.ClosePanel:
                    _director.SetOpen(false);
                    break;
                case EscapeAction.OpenPanel:
                    _director.SetOpen(true);
                    break;
            }
        }

        /// <summary>
        /// Offer this frame's key press to the listening slot. The whole engine half of
        /// rebinding: find which key went down, hand its name to the director, let the
        /// director's rules — conflict, refusal, the closed set — decide what became of it.
        /// </summary>
        static void Capture(Keyboard keys, HotkeyDirector hotkeys)
        {
            foreach (UnityEngine.InputSystem.Controls.KeyControl control in keys.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;

                HudKey? key = HotkeyUnity.ToHud(control.keyCode);
                if (key == null) continue; // Escape, modifiers, the function keys: not bindable
                hotkeys.Capture(key.Value);
                return; // one frame offers one key; the rest belong to nobody
            }
        }

        /// <summary>
        /// Find the director once the bootstrap has built it.
        ///
        /// <para>Lazily, because component <c>Start</c> order on one GameObject is undefined and
        /// this presenter and the bootstrap share one — the same reason <c>HudShell</c> attaches
        /// the way it does.</para>
        /// </summary>
        void Attach()
        {
            if (_director != null) return;

            // The bootstrap's own preferences, not the session's (U38). They are the same object
            // while a session exists — the composition root hands this pair to every HudDirectors
            // it builds — and the difference is that these exist with no session at all, which is
            // when the start screen's Options row opens this panel. Waiting for Directors would
            // have left that row opening a panel nothing drove.
            SettingsDirector? director = _bootstrap?.Preferences;
            if (director == null) return;

            _director = director;

            // The scene is the source of truth for how a session starts, so the panel is told what
            // the board is already doing before anything is allowed to change it. A zero means the
            // scene was built without that decoration, and "on" then has to mean something, so it
            // falls back to the field's own default rather than to nothing at all.
            if (_bootstrap != null)
            {
                if (_bootstrap.grassScatter > 0) _grassDensity = _bootstrap.grassScatter;
                if (_bootstrap.groundRelief > 0f) _reliefAmplitude = _bootstrap.groundRelief;

                // The interface scale is seeded from the screen rather than from the scene,
                // because it is the one setting here that is about the monitor rather than about
                // the board. The owner's report on 2026-09-16 was that the HUD read too small on
                // a 4K panel; SettingsDirector.DefaultScaleFor is where that judgement lives.
                director.SeedUiScale(SettingsDirector.DefaultScaleFor(Screen.height));

                director.Seed(GraphicsOption.Shadows, _bootstrap.castShadows);
                director.Seed(GraphicsOption.Surround, _bootstrap.terrainSkirt);
                director.Seed(GraphicsOption.GrassTufts, _bootstrap.grassScatter > 0);
                director.Seed(GraphicsOption.GroundRelief, _bootstrap.groundRelief > 0f);
                director.Seed(GraphicsOption.SeeThrough, _bootstrap.seeThroughToSelection);
                director.Seed(GraphicsOption.CutAwayCeiling,
                    _bootstrap.cameraRig != null && _bootstrap.cameraRig.slice != null
                    && _bootstrap.cameraRig.slice.suppressActiveCeiling);
            }

            director.OptionChanged += Apply;
            director.UiScaleChanged += ApplyScale;
            director.CameraSpeedChanged += ApplyCameraSpeed;
            director.DeveloperOverlayChanged += ApplyDeveloperOverlay;
            director.BusDbChanged += ApplyBusDb;
            director.ExitRequested += Quit;

            // Applied once up front, because the shell may have been built before the seed was
            // known and the stored preference is laid over it below.
            ApplyScale(director.UiScale);
            ApplyCameraSpeed(director.CameraSpeed);

            // The faders, laid in from the audio store: the panel opens describing what the
            // game is already playing at, because the bootstrap applied this same store before
            // the first frame was drawn.
            _audioStore = AudioSettingsStore.Load();
            foreach (SettingsBus bus in SettingsDirector.Buses)
                director.SeedBusDb(bus, (int)_audioStore.Db(ToSoundBus(bus)));

            // The overlay row starts telling the truth about the screen, and the backquote key
            // keeps it honest afterwards: whatever toggles the readout, the preference follows,
            // because the key never wrote anything down and this row does.
            // **Directors may not exist yet, and that is this method's own design.** The comment
            // above `_bootstrap?.Preferences` says it in as many words: attaching is deliberately
            // not made to wait for Directors, because the start screen's Options row must open a
            // panel that something drives before any session exists. So the overlay row cannot be
            // seeded here unconditionally — it is hooked up the frame Directors appears instead.
            //
            // **It threw every frame in a player build and never once in the editor** (2026-09-19,
            // the first player build this project has run). `OnDestroy` has guarded this exact
            // dereference since it was written, which is the tell: one rule, two places, and only
            // one of them knew. The build warned about it as CS8602 and nothing was listening.
            HookDeveloperOverlay();

            // Anything this machine has been told before is laid over the scene, and raises
            // OptionChanged as it goes, so the board catches up without a second code path.
            director.UseStore(new PlayerPrefsSettingsStore());

            // UseStore raised nothing for the faders — they keep their own store, already laid
            // in above — but the developer preference may have just been applied to the
            // director, so the screen catches up here.
            ApplyDeveloperOverlay();
        }

        void OnDestroy()
        {
            if (_director == null) return;
            _director.OptionChanged -= Apply;
            _director.UiScaleChanged -= ApplyScale;
            _director.CameraSpeedChanged -= ApplyCameraSpeed;
            _director.DeveloperOverlayChanged -= ApplyDeveloperOverlay;
            _director.BusDbChanged -= ApplyBusDb;
            _director.ExitRequested -= Quit;
            if (_bootstrap != null && _bootstrap.Directors != null)
                _bootstrap.Directors.Overlays.Changed -= SyncDeveloperOverlay;
        }

        /// <summary>The shell owns the UI document, so it owns the panel the scale is applied
        /// to.</summary>
        void ApplyScale(int percent) => _shell?.ApplyUiScale(percent);

        /// <summary>
        /// Turn one boolean into what the renderer does.
        ///
        /// <para>Two of these are read as the frame is drawn and cost nothing to change. Two are
        /// baked into instance matrices when a chunk is meshed, so they are followed by a remesh —
        /// and by rebuilding the surround, whose ground tiles bake the relief and whose tufts are
        /// strewn at the board's own density, both at build time.</para>
        /// </summary>
        void Apply(GraphicsOption option)
        {
            ChunkRenderer? renderer = _bootstrap?.Renderer;
            if (renderer == null || _director == null) return;
            bool on = _director.IsOn(option);

            switch (option)
            {
                case GraphicsOption.Shadows:
                    // Read per bucket as the frame is submitted, so this is the whole change.
                    renderer.CastShadows = on;
                    break;

                case GraphicsOption.Surround:
                    // The surround builds itself the first time it is asked to draw, so switching
                    // it on needs no build call here.
                    renderer.Skirt.Enabled = on;
                    break;

                case GraphicsOption.GrassTufts:
                    renderer.ScatterDensity = on ? _grassDensity : 0;
                    Redraw(renderer);
                    break;

                case GraphicsOption.GroundRelief:
                    // A static, because relief is a drawing offset the mesher and the picker both
                    // consult rather than a property of any one object. The picker reads the field
                    // live, so clicking follows the ground without anything further here.
                    GroundRelief.Amplitude = on ? _reliefAmplitude : 0f;
                    Redraw(renderer);
                    break;

                case GraphicsOption.CutAwayCeiling:
                    // The slice's own rule, and nothing has to be re-meshed: SuppressCeilingAt is
                    // asked afresh every frame by the renderer's layer loop and by the picker's
                    // band, so the floor overhead appears and becomes clickable on the next frame.
                    if (_bootstrap != null && _bootstrap.cameraRig != null
                        && _bootstrap.cameraRig.slice != null)
                        _bootstrap.cameraRig.slice.suppressActiveCeiling = on;
                    break;

                case GraphicsOption.SeeThrough:
                    // Read once a frame while the sight lines are rebuilt, so this is the whole
                    // change and it takes effect on the next frame with no remesh.
                    if (_bootstrap != null) _bootstrap.seeThroughToSelection = on;
                    break;
            }
        }

        void Redraw(ChunkRenderer renderer)
        {
            _bootstrap?.Model?.Remesh();
            if (renderer.Skirt.Enabled) renderer.Skirt.Build();
        }

        // ------------------------------------------------------------------ the new levers

        /// <summary>The audio store this machine's faders live in. Held from attach so a
        /// change writes through the same object that was loaded, never a second copy.</summary>
        AudioSettingsStore _audioStore = AudioSettingsStore.Defaults();

        /// <summary>
        /// Turn a camera-speed rung into what the rig does: a multiplier on its tuned
        /// speeds, so 100 is the fields the scene authored and nothing is invented here.
        /// </summary>
        void ApplyCameraSpeed(int percent)
        {
            var rig = _bootstrap?.cameraRig;
            if (rig != null) rig.speedScale = percent / 100f;
        }

        /// <summary>
        /// Turn the developer-overlay lever into what the overlay director does. Reaching
        /// the same director the backquote key reaches is the whole point: one state, two
        /// ways of flipping it.
        /// </summary>
        void ApplyDeveloperOverlay()
        {
            OverlayDirector? overlays = _bootstrap?.Directors?.Overlays;
            if (overlays == null || _director == null) return;
            overlays.SetDeveloper(_director.DeveloperOverlay);
        }

        /// <summary>
        /// The other direction: the backquote key flipped the readout, so the panel's row —
        /// and the preference, which the key never wrote — follow it.
        /// </summary>
        void SyncDeveloperOverlay()
        {
            OverlayDirector? overlays = _bootstrap?.Directors?.Overlays;
            if (overlays == null || _director == null) return;
            if (_director.DeveloperOverlay != overlays.DeveloperVisible)
                _director.SetDeveloperOverlay(overlays.DeveloperVisible);
        }

        /// <summary>
        /// Turn one fader into what the buses do, and write it through the audio store on
        /// the way: what the panel set is what the next session loads, with no second copy
        /// of a volume anywhere.
        /// </summary>
        void ApplyBusDb(SettingsBus bus)
        {
            if (_director == null) return;

            SoundBus sound = ToSoundBus(bus);
            float db = _director.BusDb(bus);

            // **The store is written whether or not a colony exists.** This used to return early
            // on a null director — which is every moment before a world is built — so a player
            // who set their volumes on the title screen, where the settings page is perfectly
            // reachable, had them silently discarded. It also left the title screen's own bed
            // deaf to the Music fader that is drawn right there on the page: MenuAmbience reads
            // this store, so writing it is how the slider reaches the sound under it.
            _audioStore.SetDb(sound, db);
            _audioStore.Save();
            _bootstrap?.Audio?.SetBusDb(sound, db);
        }

        /// <summary>
        /// The one line of glue between the Hud assembly's bus mirror and the audio one:
        /// same five, same order, so a cast is the whole map (see
        /// <see cref="SettingsBus"/>'s docs).
        /// </summary>
        static SoundBus ToSoundBus(SettingsBus bus) => (SoundBus)bus;

        /// <summary>
        /// Leave. In the editor, "leaving" is stopping play — a build's quit in the editor
        /// is a no-op, and a settings row that did nothing when clicked would be a row that
        /// taught the player not to trust it.
        /// </summary>
        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }
    }
}
