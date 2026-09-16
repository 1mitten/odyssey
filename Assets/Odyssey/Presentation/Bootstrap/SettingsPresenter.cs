#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// Escape opens the settings panel, and throwing a lever in it changes what is drawn.
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

        void Update()
        {
            Attach();

            Keyboard? keys = Keyboard.current;
            if (keys == null || _director == null) return;
            if (!keys.escapeKey.wasPressedThisFrame) return;

            // One key, one rule, one place. The order itself is the director's and is tested
            // without an engine; all that happens here is the doing of it.
            switch (_director.Escape(
                        _designate != null && _designate.ToolArmed,
                        _shell != null && _shell.BuildPaletteOpen))
            {
                case EscapeAction.DisarmTool:
                    _designate?.PutToolAway();
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
        /// Find the director once the bootstrap has built it.
        ///
        /// <para>Lazily, because component <c>Start</c> order on one GameObject is undefined and
        /// this presenter and the bootstrap share one — the same reason <c>HudShell</c> attaches
        /// the way it does.</para>
        /// </summary>
        void Attach()
        {
            if (_director != null) return;
            SettingsDirector? director = _bootstrap?.Directors?.Settings;
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
            }

            director.OptionChanged += Apply;
            director.UiScaleChanged += ApplyScale;

            // Applied once up front, because the shell may have been built before the seed was
            // known and the stored preference is laid over it below.
            ApplyScale(director.UiScale);

            // Anything this machine has been told before is laid over the scene, and raises
            // OptionChanged as it goes, so the board catches up without a second code path.
            director.UseStore(new PlayerPrefsSettingsStore());
        }

        void OnDestroy()
        {
            if (_director == null) return;
            _director.OptionChanged -= Apply;
            _director.UiScaleChanged -= ApplyScale;
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
    }
}
