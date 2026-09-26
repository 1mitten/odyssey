#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>Where the passage from the menu into a world has got to (design 56 §2).</summary>
    public enum WakePhase
    {
        /// <summary>Nothing is happening: the menu, or a world being played.</summary>
        Idle,
        /// <summary>The veil is rising over the menu and the menu's bed is leaving.</summary>
        Closing,
        /// <summary>The screen is black and has been drawn black; the build is not yet asked for.</summary>
        Dark,
        /// <summary>The build has been asked for and has not yet reported back.</summary>
        Building,
        /// <summary>The world exists and is being drawn behind the veil for a few frames.</summary>
        Covered,
        /// <summary>The dream: the veil is gone and the world is coming into focus.</summary>
        Waking,
        /// <summary>A skip: whatever is left of the dream is cleared quickly.</summary>
        Rushing,
        /// <summary>The wake-up is switched off: the veil simply fades.</summary>
        Lifting,
        /// <summary>Finished. The same as <see cref="Idle"/> except that it says a passage happened.</summary>
        Done,
    }

    /// <summary>
    /// How long each part of the passage takes. A class of constants rather than a table in the
    /// transition, so a test can compress the wake to a fraction of a second without the curves
    /// changing shape.
    /// </summary>
    public sealed class WakeTiming
    {
        /// <summary>How long the veil takes to close over the menu, in seconds.</summary>
        public double CloseSeconds { get; }

        /// <summary>
        /// How many black frames are drawn before the build is asked for. <b>Two, not one</b>: the
        /// frame that finished closing may still be being presented while the next frame's update
        /// blocks on the build, and a build started on a frame that never reached the screen
        /// freezes whatever was on it — the menu, which is the whole complaint (design 56 §3).
        /// </summary>
        public int DarkFrames { get; }

        /// <summary>How many frames a built world is drawn behind the veil before it opens —
        /// design 38 §25's curtain frames, which hide the first submit's driver cost.</summary>
        public int CoverFrames { get; }

        /// <summary>How long the dream lasts, from the veil starting to open to the last of the
        /// blur, in seconds (owner, 2026-09-26: about five).</summary>
        public double WakeSeconds { get; }

        /// <summary>How long the veil takes to fade when the wake-up is switched off.</summary>
        public double LiftSeconds { get; }

        /// <summary>How long a skip takes to clear what is left. Not nought: a one-frame jump in
        /// blur and camera reads as a glitch rather than as waking at once.</summary>
        public double RushSeconds { get; }

        /// <summary>
        /// The longest step one frame may take, in seconds. The frame the world is built in is a
        /// second long or more, and the frame after it is the GPU meeting the world; unclamped,
        /// either would eat a fifth of the dream in one jump. Below twenty frames a second the
        /// passage stretches, which is the better failure.
        /// </summary>
        public double MaxStepSeconds { get; }

        public WakeTiming(double closeSeconds, int darkFrames, int coverFrames, double wakeSeconds,
            double liftSeconds, double rushSeconds, double maxStepSeconds)
        {
            CloseSeconds = Math.Max(0d, closeSeconds);
            DarkFrames = Math.Max(1, darkFrames);
            CoverFrames = Math.Max(0, coverFrames);
            WakeSeconds = Math.Max(0d, wakeSeconds);
            LiftSeconds = Math.Max(0d, liftSeconds);
            RushSeconds = Math.Max(0d, rushSeconds);
            MaxStepSeconds = Math.Max(1e-3, maxStepSeconds);
        }

        /// <summary>What the player sees (design 56 §2).</summary>
        public static readonly WakeTiming Standard = new(
            closeSeconds: 0.6, darkFrames: 2, coverFrames: 3, wakeSeconds: 5.0,
            liftSeconds: 0.25, rushSeconds: 0.25, maxStepSeconds: 0.05);

        /// <summary>For tests that press Start and want a world a few frames later: every clock is
        /// nought, but the black frames and the cover still happen, because they are frames.</summary>
        public static readonly WakeTiming Instant = new(
            closeSeconds: 0, darkFrames: 2, coverFrames: 3, wakeSeconds: 0,
            liftSeconds: 0, rushSeconds: 0, maxStepSeconds: 0.05);
    }

    /// <summary>
    /// Every output of the passage at one instant, each 0 to 1, 0 meaning "as the game normally
    /// is". The engine side turns them into an opacity, a post-processing weight, a filter and a
    /// camera offset; nothing here knows what those are.
    /// </summary>
    public readonly struct WakeLook
    {
        /// <summary>The black veil over everything: 1 opaque, 0 gone.</summary>
        public readonly float Veil;
        /// <summary>How far out of focus the world is.</summary>
        public readonly float Blur;
        /// <summary>The warm, faded, glowing grade of the dream.</summary>
        public readonly float Haze;
        /// <summary>How far the camera still is from where it will settle.</summary>
        public readonly float Settle;
        /// <summary>How closed and distant the sound is.</summary>
        public readonly float Muffle;

        public WakeLook(float veil, float blur, float haze, float settle, float muffle)
        {
            Veil = veil;
            Blur = blur;
            Haze = haze;
            Settle = settle;
            Muffle = muffle;
        }

        public static readonly WakeLook Clear = default;

        public WakeLook Scaled(float k) =>
            new(Veil * k, Blur * k, Haze * k, Settle * k, Muffle * k);

        /// <summary>The low-pass cutoff the sound is heard through, in hertz. Log-spaced between
        /// open (22 kHz) and closed (500 Hz), so the half-way muffle is the geometric mean — an
        /// ear hears octaves, not hertz.</summary>
        public float CutoffHz => (float)(WakeTransition.OpenCutoffHz
            * Math.Pow(WakeTransition.ClosedCutoffHz / WakeTransition.OpenCutoffHz, Clamp01(Muffle)));

        /// <summary>The whole mix's gain, 0 to 1: about −9 dB at the most muffled.</summary>
        public float ListenerGain => 1f - (1f - WakeTransition.MuffledGain) * Clamp01(Muffle);

        /// <summary>The reverb's room level in millibels: −600 fully muffled, falling 20 dB a
        /// decade of muffle, and −10000 (off) at nought — so the room drains away rather than
        /// switching off at the end.</summary>
        public float ReverbRoomMb
        {
            get
            {
                double m = Math.Max(Clamp01(Muffle), 1e-6);
                double mb = WakeTransition.MuffledRoomMb + 2000d * Math.Log10(m);
                return (float)Math.Clamp(mb, -10000d, WakeTransition.MuffledRoomMb);
            }
        }

        /// <summary>The camera's extra pitch, in degrees.</summary>
        public float SettlePitchDeg => WakeTransition.SettlePitchDeg * Settle;
        /// <summary>The camera's extra yaw, in degrees.</summary>
        public float SettleYawDeg => WakeTransition.SettleYawDeg * Settle;
        /// <summary>The camera's extra distance, as a fraction of its own.</summary>
        public float SettleDistanceFactor => WakeTransition.SettleDistanceFactor * Settle;

        static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Clamp(v, 0f, 1f);
    }

    /// <summary>
    /// The passage from the menu into a world: the menu fading out, the build behind a black
    /// veil, and the world arriving as waking from sleep — blurred, warm and muffled, clearing
    /// over five seconds (owner, 2026-09-26; design 56).
    ///
    /// <para><b>Engine-free on purpose.</b> This is the whole of the timing and the curves, so the
    /// fast tier can hold every rule — one build, focus last, the clock held and released exactly
    /// once — without Unity. The shell steps it once a frame and applies <see cref="Look"/>.</para>
    ///
    /// <para><b>It moves the request for a build, never the hand-over.</b> The build still happens
    /// in one frame behind an opaque cover and the interface still attaches in that frame, which
    /// design 38 §25b measured to be the only order that keeps the reveal frame cheap. What is new
    /// is that the build is asked for only once the screen has been drawn black, so the long
    /// frame freezes black rather than freezing the menu.</para>
    /// </summary>
    public sealed class WakeTransition
    {
        public const float OpenCutoffHz = 22000f;
        public const float ClosedCutoffHz = 500f;
        public const float MuffledGain = 0.35f;
        public const float MuffledRoomMb = -600f;

        /// <summary>How much higher the camera starts, in degrees.</summary>
        public const float SettlePitchDeg = 10f;
        /// <summary>How far round the camera starts, in degrees.</summary>
        public const float SettleYawDeg = -6f;
        /// <summary>How much further out the camera starts, as a fraction of its distance.</summary>
        public const float SettleDistanceFactor = 0.35f;

        // Where in the dream (0 to 1 of WakeSeconds) each part has finished. Sound comes back
        // before sight, the glow lingers, and focus arrives last, at the very end.
        public const double VeilClearAt = 0.16;
        public const double HearingBackAt = 0.70;
        public const double HazeStartsAt = 0.05;
        public const double HazeGoneAt = 0.85;
        public const double FocusStartsAt = 0.10;

        readonly WakeTiming _timing;
        double _t;
        int _frames;
        bool _skipQueued;
        WakeLook _rushFrom;

        public WakeTransition(WakeTiming timing, bool dream)
        {
            _timing = timing ?? throw new ArgumentNullException(nameof(timing));
            Dream = dream;
        }

        public WakeTiming Timing => _timing;

        /// <summary>Whether this passage is the dream, or the plain fade the setting asks for when
        /// the wake-up is switched off.</summary>
        public bool Dream { get; }

        public WakePhase Phase { get; private set; } = WakePhase.Idle;

        public WakeLook Look { get; private set; } = WakeLook.Clear;

        /// <summary>Whether a passage is under way.</summary>
        public bool Active => Phase != WakePhase.Idle && Phase != WakePhase.Done;

        /// <summary>
        /// True from the step the black frames have been drawn until <see cref="Built"/> or
        /// <see cref="Abort"/>: the shell builds the world then, once.
        /// </summary>
        public bool BuildDue => Phase == WakePhase.Building && !_buildTaken;
        bool _buildTaken;

        /// <summary>Mark the build as taken, so a second read of <see cref="BuildDue"/> in the same
        /// frame cannot build twice.</summary>
        public void TakeBuild()
        {
            if (Phase == WakePhase.Building) _buildTaken = true;
        }

        /// <summary>
        /// Whether the colony's clock is held. From the build to the end of the dream, and only in
        /// the dream (owner: <i>"game holds paused until the eyes are open"</i>). A skip releases
        /// it on the step it is pressed.
        /// </summary>
        public bool HoldsClock =>
            Dream && (Phase == WakePhase.Building || Phase == WakePhase.Covered || Phase == WakePhase.Waking);

        /// <summary>Whether the veil takes the pointer and the keys. A press while it does is a
        /// skip and nothing else.</summary>
        public bool CatchesInput =>
            Phase == WakePhase.Closing || Phase == WakePhase.Dark || Phase == WakePhase.Building
            || Phase == WakePhase.Covered || Phase == WakePhase.Waking;

        /// <summary>Whether a built world is being drawn behind an opaque veil right now.</summary>
        public bool WorldCovered => Phase == WakePhase.Building || Phase == WakePhase.Covered;

        /// <summary>
        /// Start a passage. False while one is under way, which is the guard against a second
        /// press of Start building a second world.
        /// </summary>
        public bool Begin()
        {
            if (Active) return false;
            Phase = WakePhase.Closing;
            _t = 0d;
            _frames = 0;
            _skipQueued = false;
            _buildTaken = false;
            Look = Evaluate();
            return true;
        }

        /// <summary>The world has been built: draw it behind the veil for the cover frames.</summary>
        public void Built()
        {
            if (Phase != WakePhase.Building) return;
            Phase = WakePhase.Covered;
            _frames = 0;
            Look = Evaluate();
        }

        /// <summary>
        /// Wake at once. Ignored while the menu is still closing or the screen is dark — the press
        /// is committed and there is no world yet to wake into — kept for later while the world is
        /// behind the cover, and taken at once in the dream.
        /// </summary>
        public void Skip()
        {
            switch (Phase)
            {
                case WakePhase.Building:
                case WakePhase.Covered:
                    _skipQueued = true;
                    break;
                case WakePhase.Waking:
                    StartRush(Look);
                    break;
            }
        }

        /// <summary>The build failed: everything off, back to where it started.</summary>
        public void Abort()
        {
            Phase = WakePhase.Idle;
            _t = 0d;
            _frames = 0;
            _skipQueued = false;
            _buildTaken = false;
            Look = WakeLook.Clear;
        }

        /// <summary>One frame, of <paramref name="deltaSeconds"/> real (unscaled) seconds.</summary>
        public void Step(double deltaSeconds)
        {
            double dt = double.IsNaN(deltaSeconds) ? 0d : Math.Clamp(deltaSeconds, 0d, _timing.MaxStepSeconds);
            switch (Phase)
            {
                case WakePhase.Closing:
                    _t += dt;
                    if (_t >= _timing.CloseSeconds)
                    {
                        Phase = WakePhase.Dark;
                        _frames = 0;
                    }
                    break;

                case WakePhase.Dark:
                    // Counted in steps, not seconds: what matters is that frames were drawn black.
                    if (++_frames >= _timing.DarkFrames)
                    {
                        Phase = WakePhase.Building;
                        _buildTaken = false;
                    }
                    break;

                case WakePhase.Covered:
                    if (_frames++ >= _timing.CoverFrames - 1 || _timing.CoverFrames == 0)
                        Open();
                    break;

                case WakePhase.Waking:
                    _t += dt;
                    if (_t >= _timing.WakeSeconds) Finish();
                    break;

                case WakePhase.Rushing:
                case WakePhase.Lifting:
                    _t += dt;
                    double length = Phase == WakePhase.Rushing ? _timing.RushSeconds : _timing.LiftSeconds;
                    if (_t >= length) Finish();
                    break;
            }
            Look = Evaluate();
        }

        void Open()
        {
            _t = 0d;
            if (!Dream)
            {
                Phase = WakePhase.Lifting;
                return;
            }
            if (_skipQueued)
            {
                StartRush(Full);
                return;
            }
            Phase = WakePhase.Waking;
            if (_timing.WakeSeconds <= 0d) Finish();
        }

        void StartRush(WakeLook from)
        {
            _rushFrom = from;
            _t = 0d;
            Phase = WakePhase.Rushing;
            if (_timing.RushSeconds <= 0d) Finish();
        }

        void Finish()
        {
            Phase = WakePhase.Done;
            _t = 0d;
            _skipQueued = false;
        }

        WakeLook Full => Dream ? new WakeLook(1f, 1f, 1f, 1f, 1f) : new WakeLook(1f, 0f, 0f, 0f, 0f);

        WakeLook Evaluate()
        {
            switch (Phase)
            {
                case WakePhase.Closing:
                {
                    float v = _timing.CloseSeconds <= 0d ? 1f : (float)Smooth(0d, 1d, _t / _timing.CloseSeconds);
                    // The sound closes with the eyes, in the dream; the plain fade leaves it to the bed.
                    return new WakeLook(v, 0f, 0f, 0f, Dream ? v : 0f);
                }
                case WakePhase.Dark:
                    return new WakeLook(1f, 0f, 0f, 0f, Dream ? 1f : 0f);
                case WakePhase.Building:
                case WakePhase.Covered:
                    return Full;
                case WakePhase.Waking:
                    return Dreaming(_timing.WakeSeconds <= 0d ? 1d : _t / _timing.WakeSeconds);
                case WakePhase.Rushing:
                {
                    double r = _timing.RushSeconds <= 0d ? 1d : _t / _timing.RushSeconds;
                    return _rushFrom.Scaled((float)(1d - Smooth(0d, 1d, r)));
                }
                case WakePhase.Lifting:
                {
                    double r = _timing.LiftSeconds <= 0d ? 1d : _t / _timing.LiftSeconds;
                    return new WakeLook((float)(1d - Smooth(0d, 1d, r)), 0f, 0f, 0f, 0f);
                }
                default:
                    return WakeLook.Clear;
            }
        }

        /// <summary>
        /// The dream at <paramref name="u"/>, 0 to 1 of its length. Every output falls and every
        /// one is exactly nought at 1. Public so a test can walk the curves without a clock.
        /// </summary>
        public static WakeLook Dreaming(double u)
        {
            u = double.IsNaN(u) ? 1d : Math.Clamp(u, 0d, 1d);
            double veil = 1d - Smooth(0d, VeilClearAt, u);
            double muffle = 1d - Smooth(0d, HearingBackAt, u);
            double haze = 1d - Smooth(HazeStartsAt, HazeGoneAt, u);
            double blur = 1d - Smoother(FocusStartsAt, 1d, u);
            double settle = (1d - u) * (1d - u) * (1d - u);
            return new WakeLook((float)veil, (float)blur, (float)haze, (float)settle, (float)muffle);
        }

        static double Unit(double a, double b, double x) =>
            b <= a ? (x >= b ? 1d : 0d) : Math.Clamp((x - a) / (b - a), 0d, 1d);

        static double Smooth(double a, double b, double x)
        {
            double t = Unit(a, b, x);
            return t * t * (3d - 2d * t);
        }

        static double Smoother(double a, double b, double x)
        {
            double t = Unit(a, b, x);
            return t * t * t * (t * (6d * t - 15d) + 10d);
        }
    }
}
