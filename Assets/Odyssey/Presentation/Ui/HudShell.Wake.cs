#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The passage from the menu into a world (<c>docs/design/56-wake-up.md</c>): the menu fades
    /// to black, the world is built behind the black, and the player wakes into it — blurred, warm
    /// and muffled, the camera settling and the colony held until the eyes are open (owner,
    /// 2026-09-26).
    ///
    /// <para><see cref="WakeTransition"/> owns every rule and every curve; this owns the things
    /// they are applied to — the veil (<see cref="_curtainPane"/>), the blur, the grade, the
    /// filters on the listener, the camera's settle, the clock gate and the keys — and puts every
    /// one of them back when the passage ends, however it ends.</para>
    ///
    /// <para><b>The build is still one frame behind an opaque cover</b>, and the interface still
    /// attaches in that frame, which design 38 §25b measured to be the only order that keeps the
    /// reveal cheap. What moved is the <i>request</i>: it waits for the screen to have been drawn
    /// black, so the long frame freezes on black rather than on the menu.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        /// <summary>
        /// The passage's clocks. <see cref="WakeTiming.Standard"/> for the player; a test that
        /// presses Start and wants its world a few frames later sets <see cref="WakeTiming.Instant"/>
        /// before the shell starts.
        /// </summary>
        public WakeTiming WakeTiming { get; set; } = WakeTiming.Standard;

        WakeTransition _wake = new(WakeTiming.Standard, dream: true);
        Action? _pendingBuild;
        WakeBlur? _wakeBlur;
        WakeVolume? _wakeVolume;
        WakeHearing? _wakeHearing;
        bool _keysHeldByWake;

        /// <summary>The passage as it stands, for the tests and the bench.</summary>
        public WakeTransition Wake => _wake;

        /// <summary>Whether the wake has the pointer and the keys, so nothing else should read
        /// them this frame — Escape included, which <c>SettingsPresenter</c> reads directly.</summary>
        public bool WakeHoldsInput => _wake.CatchesInput;

        /// <summary>The blur, while one is drawn, for a test that wants to see it ran.</summary>
        public WakeBlur? WakeBlurNow => _wakeBlur;

        /// <summary>
        /// Start the passage towards <paramref name="build"/>, which is run once the screen has
        /// been drawn black. False while one is already under way — the guard against a second
        /// press of Start building a second world — or while the older curtain is still up.
        /// </summary>
        bool BeginWake(Action build)
        {
            if (_wake.Active || _curtain > 0) return false;

            // The setting is read at the press, so switching it mid-passage changes the next one.
            var wake = new WakeTransition(WakeTiming, dream: _boot!.Preferences.WakeUp);
            if (!wake.Begin()) return false;
            _wake = wake;
            _pendingBuild = build;

            // The menu's bed leaves with the menu, not a second later with the world.
            _boot.MenuLeaving = true;
            _boot.Keys.Suspended = true;
            _keysHeldByWake = true;

            // The sound closes with the eyes, so the filter goes on now, over the menu's bed.
            if (wake.Dream) _wakeHearing = new WakeHearing(Listener());

            _curtainPane.style.opacity = 0f;
            _curtainPane.style.display = DisplayStyle.Flex;
            _curtainPane.BringToFront();
            ApplyWake();
            return true;
        }

        /// <summary>
        /// One frame of the passage. First thing in <c>Update</c>, above the session guard,
        /// because the veil closes over the menu, where there is no session.
        /// </summary>
        void StepWake()
        {
            if (_wake.Active)
            {
                // Any key wakes you. The pointer is taken by the veil itself (OnVeilPointer), so a
                // click that wakes you is not also an order given to the colony.
                var keys = UnityEngine.InputSystem.Keyboard.current;
                if (_wake.CatchesInput && keys != null && keys.anyKey.wasPressedThisFrame) _wake.Skip();

                _wake.Step(Time.unscaledDeltaTime);

                if (_wake.BuildDue) RunBuild();
                if (!_wake.Active)
                {
                    EndWake();
                }
                else
                {
                    _boot!.ClockHeld = _wake.HoldsClock;
                    ApplyWake();
                }
            }

            // The keys come back once the wake lets go of them *and* nothing is held, so a key
            // held through the skip does not start a pan the frame after.
            if (_keysHeldByWake && !_wake.CatchesInput && !AnyKeyHeld())
            {
                _boot!.Keys.Suspended = false;
                _keysHeldByWake = false;
            }
        }

        void RunBuild()
        {
            _wake.TakeBuild();
            Action? build = _pendingBuild;
            _pendingBuild = null;

            // Before the build, so the first frame the world is drawn in is already the dream.
            if (_wake.Dream)
            {
                Camera? camera = CameraOf();
                if (camera != null) _wakeBlur = new WakeBlur(camera);
                _wakeVolume = new WakeVolume(_boot!.transform);
            }

            try
            {
                build?.Invoke();
            }
            catch (Exception e)
            {
                // A build that throws must not leave the player behind a black veil with the keys
                // gone: everything goes back, and the menu is there to press again.
                Debug.LogException(e);
                AbortWake();
                return;
            }

            if (!_boot!.HasSession)
            {
                AbortWake();
                return;
            }
            _wake.Built();
            _boot.ClockHeld = _wake.HoldsClock;
        }

        /// <summary>A click or a scroll on the veil: wake at once, and do nothing else.</summary>
        void OnVeilPointer(EventBase evt)
        {
            if (!_wake.CatchesInput) return;
            _wake.Skip();
            evt.StopPropagation();
        }

        /// <summary>
        /// Enter a world the way the menu does: fade, build behind the black, wake. For the bench's
        /// hitch tour, which must measure the passage a player sits through rather than a bare
        /// build. False if a passage is already under way.
        /// </summary>
        public bool EnterWorld(Action build) => BeginWake(build);

        /// <summary>Wake at once — the veil's click, and a test's.</summary>
        public void SkipWake() => _wake.Skip();

        void ApplyWake()
        {
            WakeLook look = _wake.Look;
            _curtainPane.style.opacity = look.Veil;
            _curtainPane.pickingMode = _wake.CatchesInput ? PickingMode.Position : PickingMode.Ignore;

            // The interface arrives as the hearing does. Opacity only: it is displayed and laid
            // out from the build frame, under the veil, exactly as before (design 38 §25b).
            _worldUi.style.opacity = 1f - look.Muffle;

            if (_wakeBlur != null) _wakeBlur.Strength = look.Blur;
            if (_wakeVolume != null) _wakeVolume.Haze = look.Haze;
            _wakeHearing?.Apply(look.CutoffHz, look.ListenerGain, look.ReverbRoomMb);
            _rig?.SetSettle(look.SettlePitchDeg, look.SettleYawDeg, look.SettleDistanceFactor);
        }

        void AbortWake()
        {
            _wake.Abort();
            EndWake();
        }

        /// <summary>Put back everything the passage took. Safe to call more than once.</summary>
        void EndWake()
        {
            _pendingBuild = null;
            _wakeBlur?.Dispose();
            _wakeBlur = null;
            _wakeVolume?.Dispose();
            _wakeVolume = null;
            _wakeHearing?.Dispose();
            _wakeHearing = null;
            _rig?.SetSettle(0f, 0f, 0f);

            if (_boot != null)
            {
                _boot.ClockHeld = false;
                _boot.MenuLeaving = false;
            }

            if (_curtainPane != null)
            {
                _curtainPane.style.display = DisplayStyle.None;
                _curtainPane.style.opacity = 1f;
                _curtainPane.pickingMode = PickingMode.Position;
            }
            if (_worldUi != null) _worldUi.style.opacity = 1f;
        }

        /// <summary>The shell going away mid-passage must not leave the listener muted, the clock
        /// held or the keys shut.</summary>
        void ReleaseWake()
        {
            if (_wake.Active) _wake.Abort();
            EndWake();
            if (_keysHeldByWake && _boot != null) _boot.Keys.Suspended = false;
            _keysHeldByWake = false;
        }

        Camera? CameraOf()
        {
            Camera? camera = _rig != null ? _rig.GetComponent<Camera>() : null;
            return camera != null ? camera : Camera.main;
        }

        AudioListener? Listener()
        {
            AudioListener? listener = _rig != null ? _rig.GetComponent<AudioListener>() : null;
            return listener != null ? listener : FindFirstObjectByType<AudioListener>();
        }

        static bool AnyKeyHeld()
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            return keys != null && keys.anyKey.isPressed;
        }
    }
}
