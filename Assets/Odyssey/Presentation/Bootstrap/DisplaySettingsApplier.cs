#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// The one place the display levers touch Unity.
    ///
    /// <para><b>Its own class rather than six more cases in <see cref="SettingsPresenter"/></b>,
    /// which is already four hundred lines and named in the baseline audit's list of monoliths.
    /// These six share a lifetime the rest of that file does not have — the pipeline copy below
    /// has to be made before anything reads a rung and given back when the session ends — and a
    /// thing with its own lifetime is a thing with its own owner.</para>
    ///
    /// <para><b>Nothing here runs per frame.</b> Every lever is applied on the event that moved
    /// it and never polled; there is no <c>Update</c> and nothing to allocate. That is not
    /// frugality for its own sake — a settings object that reasserted itself every frame would
    /// fight anything else that set the same Unity property, and the fault would present as a
    /// setting that "won't stay changed".</para>
    ///
    /// <para><b>Unity's own numbers, verbatim.</b> <c>vSyncCount</c>, <c>msaaSampleCount</c> and
    /// <c>FullScreenMode</c> are the rungs the director stores, so this class casts rather than
    /// translates and no table here can drift from the API it feeds.</para>
    /// </summary>
    public sealed class DisplaySettingsApplier : IDisposable
    {
        readonly SettingsDirector _director;

        /// <summary>The pipeline asset the project committed, put back on the way out.</summary>
        readonly UniversalRenderPipelineAsset? _original;

        /// <summary>Whether the live asset was found on the quality level rather than on the
        /// graphics defaults. It has to go back where it came from.</summary>
        readonly bool _fromQualityLevel;

        /// <summary>
        /// Our own copy of the pipeline asset, so that moving the render scale does not write to
        /// the committed one.
        ///
        /// <para><b>This is the trap this class exists to avoid.</b> A
        /// <c>UniversalRenderPipelineAsset</c> is a ScriptableObject, and in the editor the live
        /// one <i>is</i> <c>Assets/Settings/PC_RPAsset.asset</c>. Writing <c>renderScale</c>
        /// straight on to it would mean pressing a row in the settings panel showed up in
        /// <c>git status</c>, and a committed render scale of 70% would then ship to everyone.
        /// The HUD already solved this exact shape for <c>PanelSettings</c> — see
        /// <c>HudShell.EnsurePanelCopy</c> — and this is the same answer: instantiate, hide, use
        /// the copy, hand the original back at the end.</para>
        /// </summary>
        UniversalRenderPipelineAsset? _copy;

        bool _disposed;

        /// <summary>
        /// Whether a moved rung should reach Unity yet.
        ///
        /// <para>False until <see cref="ApplyAll"/> runs, because the stored preferences are laid
        /// over the seeds one at a time and each one raises. Letting those through would rebuild
        /// the render targets once per stored lever on the way into a session, to arrive exactly
        /// where <see cref="ApplyAll"/> puts them in a single pass.</para>
        /// </summary>
        bool _live;

        public DisplaySettingsApplier(SettingsDirector director)
        {
            _director = director;

            // The quality level's override wins over the graphics default, so that is the one to
            // read first. Copying the default while the quality level held another asset would
            // leave every lever here silently doing nothing — the worst failure available, because
            // the panel would still light the rung.
            var onQuality = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            _fromQualityLevel = onQuality != null;
            _original = onQuality
                ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

            if (_original != null)
            {
                _copy = UnityEngine.Object.Instantiate(_original);
                _copy.name = _original.name + " (session)";

                // Not saved with the scene, not shown in the hierarchy, and — the point — not the
                // asset on disk.
                _copy.hideFlags = HideFlags.HideAndDontSave;
                Install(_copy);
            }

            Seed();

            _director.LadderChanged += Apply;
            _director.ResolutionChanged += ApplyResolution;
        }

        /// <summary>
        /// Tell the panel what the machine is already doing, before any stored preference is laid
        /// over it.
        ///
        /// <para>The same bargain every seed in this project makes: the machine beats the asset
        /// and the asset beats nothing at all. It matters most for the shadow distance, whose
        /// default in the director is a guess and whose real value was tuned by the golden-hour
        /// work.</para>
        /// </summary>
        void Seed()
        {
            _director.SeedValue(GraphicsLadder.VSync, QualitySettings.vSyncCount);
            _director.SeedValue(GraphicsLadder.FrameCap,
                Application.targetFrameRate <= 0 ? SettingsDirector.Uncapped : Application.targetFrameRate);
            _director.SeedValue(GraphicsLadder.DisplayMode, (int)Screen.fullScreenMode);

            if (_copy != null)
            {
                _director.SeedValue(GraphicsLadder.RenderScale, Mathf.RoundToInt(_copy.renderScale * 100f));
                _director.SeedValue(GraphicsLadder.AntiAliasing, _copy.msaaSampleCount);
                _director.SeedValue(GraphicsLadder.ShadowDistance, Mathf.RoundToInt(_copy.shadowDistance));
            }

            _director.SeedResolutions(OfferedModes());
            _director.SeedResolution(new SettingsDirector.Mode(Screen.width, Screen.height));
        }

        /// <summary>
        /// The sizes this screen offers.
        ///
        /// <para>Refresh rate is dropped here rather than in the director because it is the only
        /// part of a <c>Resolution</c> that needs Unity to read: the director de-duplicates by
        /// area, so a monitor that does 60, 120 and 144 Hz contributes 1920×1080 once.</para>
        ///
        /// <para>A short list, or none, is a case rather than a fault — an X11 session can report
        /// very little — so the panel simply draws no rank.</para>
        /// </summary>
        static IEnumerable<SettingsDirector.Mode> OfferedModes()
        {
            Resolution[] offered = Screen.resolutions;
            if (offered == null || offered.Length == 0)
            {
                if (Screen.width > 0 && Screen.height > 0)
                    yield return new SettingsDirector.Mode(Screen.width, Screen.height);
                yield break;
            }

            for (int i = 0; i < offered.Length; i++)
                yield return new SettingsDirector.Mode(offered[i].width, offered[i].height);
        }

        void Install(UniversalRenderPipelineAsset asset)
        {
            if (_fromQualityLevel) QualitySettings.renderPipeline = asset;
            else GraphicsSettings.defaultRenderPipeline = asset;
        }

        /// <summary>Hand one rung to Unity. Called on the event that moved it and never
        /// otherwise.</summary>
        void Apply(GraphicsLadder ladder)
        {
            if (!_live) return;
            ApplyNow(ladder);
        }

        void ApplyNow(GraphicsLadder ladder)
        {
            int rung = _director.Value(ladder);

            switch (ladder)
            {
                case GraphicsLadder.VSync:
                    QualitySettings.vSyncCount = rung;

                    // The cap has to be re-stated: it is ignored while vSyncCount is above zero,
                    // so the rung the player chose has to be put back the moment VSync lets go.
                    ApplyFrameCap();
                    break;

                case GraphicsLadder.FrameCap:
                    ApplyFrameCap();
                    break;

                case GraphicsLadder.RenderScale:
                    if (_copy == null) break;
                    _copy.renderScale = rung / 100f;

                    // Below full size the image is upscaled, and which upscaler does it is a
                    // decision worth making rather than leaving on Auto: the asset already
                    // carries an FSR sharpness, and FSR is the whole reason a render scale below
                    // 100 is worth offering at all. At 100 nothing is upscaled, so the filter
                    // goes back to Auto rather than staying named.
                    _copy.upscalingFilter = rung < 100
                        ? UpscalingFilterSelection.FSR
                        : UpscalingFilterSelection.Auto;
                    break;

                case GraphicsLadder.AntiAliasing:
                    // Honoured because the renderer is Forward+. Under Deferred, URP ignores this
                    // and the row would be a lie — see docs/design/27-graphics-settings.md.
                    if (_copy != null) _copy.msaaSampleCount = rung;
                    break;

                case GraphicsLadder.ShadowDistance:
                    if (_copy != null) _copy.shadowDistance = rung;
                    break;

                case GraphicsLadder.DisplayMode:
                    // Meaningless against the Game view, and the panel greys the row there.
                    if (!Application.isEditor) Screen.fullScreenMode = (FullScreenMode)rung;
                    break;
            }
        }

        /// <summary>
        /// The cap, and the one rule about it.
        ///
        /// <para>Unity ignores <c>Application.targetFrameRate</c> whenever <c>vSyncCount</c> is
        /// above zero, so the value is set unconditionally and the <em>panel</em> is what says the
        /// row is inert (<c>SettingsDirector.FrameCapIsLive</c>). Setting it anyway is what makes
        /// turning VSync off restore the player's cap without them touching the row again.</para>
        /// </summary>
        void ApplyFrameCap()
        {
            int rung = _director.Value(GraphicsLadder.FrameCap);
            Application.targetFrameRate = rung == SettingsDirector.Uncapped ? -1 : rung;
        }

        void ApplyResolution()
        {
            if (!_live) return;
            ApplyResolutionNow();
        }

        void ApplyResolutionNow()
        {
            if (Application.isEditor) return;
            SettingsDirector.Mode mode = _director.Resolution;
            if (mode.Width <= 0 || mode.Height <= 0) return;
            Screen.SetResolution(mode.Width, mode.Height, Screen.fullScreenMode);
        }

        /// <summary>
        /// Apply everything once, after the store has been read.
        ///
        /// <para>Called by the presenter rather than from the constructor, because the stored
        /// preferences are laid over the seeds afterwards and applying twice would cost two
        /// render-target rebuilds to reach the same place.</para>
        /// </summary>
        public void ApplyAll()
        {
            _live = true;
            foreach (GraphicsLadder ladder in SettingsDirector.AllLadders) ApplyNow(ladder);
            ApplyResolutionNow();
        }

        /// <summary>
        /// Give the committed asset back and destroy the copy.
        ///
        /// <para>Without this the editor keeps a copy per play session, and each one holds the
        /// renderer and its shaders alive until a domain reload. The original reference is put
        /// back where it was found, so a second session starts from the asset rather than from
        /// the last session's copy of it.</para>
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _director.LadderChanged -= Apply;
            _director.ResolutionChanged -= ApplyResolution;

            if (_copy == null) return;
            if (_original != null) Install(_original);
            UnityEngine.Object.Destroy(_copy);
            _copy = null;
        }
    }
}
