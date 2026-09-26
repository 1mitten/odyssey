#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>The seam between the butterflies and the world</b> (design 52 §4). The game answers it from
    /// the render mirror (<c>ButterflyHabitat</c>) and the tests from a fake; nothing else about the
    /// board reaches the meadow.
    /// </summary>
    public interface IButterflyHabitat
    {
        /// <summary>
        /// Whether a butterfly may live over (x, z), in world metres: the top of that column is grass
        /// open to the sky with nothing built on it. <paramref name="ground"/> is the height of that
        /// grass in metres, and is only meaningful when the answer is yes.
        /// </summary>
        bool Habitat(float x, float z, out float ground);

        /// <summary>The height in metres of whatever tops the column — ground, water, a floor — for
        /// flying over a place that is not habitat.</summary>
        float Surface(float x, float z);

        /// <summary>Whether flowers grow at (x, z). A landing preference only.</summary>
        bool Flowers(float x, float z);
    }

    /// <summary>The world a step happens in: the calendar, the sky and the wind.</summary>
    public readonly struct ButterflyConditions
    {
        public ButterflyConditions(long tick, float cloud, float rain, float windX, float windZ)
        {
            Tick = tick;
            Cloud = cloud;
            Rain = rain;
            WindX = windX;
            WindZ = windZ;
        }

        /// <summary>The game tick, for the season. The hour is the drawing's business (the glow).</summary>
        public readonly long Tick;

        /// <summary>How far cloud covers the sky, 0 to 1.</summary>
        public readonly float Cloud;

        /// <summary>How hard it is raining, 0 to 1.</summary>
        public readonly float Rain;

        /// <summary>The drift the wind gives a butterfly, metres a second, on the ground plane.</summary>
        public readonly float WindX, WindZ;

        public static ButterflyConditions ClearSpring => new ButterflyConditions(Calendar.TicksPerDay * 6, 0f, 0f, 0f, 0f);
    }

    public enum ButterflyState : byte
    {
        /// <summary>A free slot: nobody is here.</summary>
        Empty = 0,

        /// <summary>Wandering: short legs, a heading re-rolled at the end of each, bursts and glides.</summary>
        Flying = 1,

        /// <summary>Making for a chosen spot on the grass, a flower where one is near.</summary>
        Landing = 2,

        /// <summary>On the grass, wings closed upright — or, for a third of them, opening slowly to bask.</summary>
        Resting = 3,

        /// <summary>Startled by a walker: climbing away fast, flapping hard, for a second or three.</summary>
        Fleeing = 4,

        /// <summary>Fading out: left the window, or the meadow has more than the season wants.</summary>
        Leaving = 5,
    }

    /// <summary>
    /// The butterflies near the camera, as a model the fast tier runs (design 52 §3).
    ///
    /// <para><b>Drawn, never simulated.</b> Nothing here is in a cell, a save, the state hash or
    /// the pawn registry; the simulation does not know butterflies exist. The drawing owns a seed of
    /// its own, so a screenshot tool gets the same meadow twice, and a colony that plays the same
    /// ticks twice is not affected by where a butterfly went.</para>
    ///
    /// <para><b>A small state machine, not an analytic path</b>, because a path computed from a
    /// seed and a clock cannot remember having been startled (e-13). Each butterfly is a handful of
    /// floats in structure-of-arrays form; a step is O(live × walkers / 4) and allocates nothing
    /// once the arrays are sized. The wings are the shader's: this model says how hard a butterfly
    /// is flapping, how folded it is and where it is, and the vertex stage turns that into strokes.</para>
    ///
    /// <para><b>Real seconds, not game seconds</b> — the one place this unit departs from the birds
    /// and the rain. A butterfly beats about ten times a second, and at speed 3 that is thirty, which
    /// strobes at sixty frames (e-13 §13). So the caller passes the frame's own seconds while the
    /// world runs and zero while it is paused: a pause holds every butterfly where it is.</para>
    ///
    /// <para><b>It lives in a window round the camera's focus</b>, not over the board. The number
    /// alive follows the ladder's rung, the share of the window that is habitat, how far the camera
    /// is zoomed in, the season and the weather. One that leaves the window fades out, and the meadow
    /// deals a new one on the grass inside it, born at rest and faded in, so nothing pops.</para>
    /// </summary>
    public sealed class ButterflyMeadow
    {
        // ------------------------------------------------------------------ the numbers (design 52 §3)

        /// <summary>How fast a butterfly cruises, metres a second (e-13: a little over 1 m/s).</summary>
        public const float CruiseSlowest = 0.8f, CruiseQuickest = 1.6f;

        /// <summary>How long one leg of wandering lasts before the heading is re-rolled, seconds.
        /// Short on purpose: a smooth path reads as an orbit (e-13 §14).</summary>
        public const float LegShortest = 0.3f, LegLongest = 1.0f;

        /// <summary>How far a heading swings at the end of an ordinary leg, radians either way, and
        /// how often a leg instead turns hard.</summary>
        public const float LegTurn = 1.2f, HardTurnChance = 0.15f;

        /// <summary>How fast a heading closes on the one it wants, radians a second.</summary>
        public const float TurnRate = 4f;

        /// <summary>Wingbeats in one burst of flapping (e-13 §5).</summary>
        public const int BurstFewest = 3, BurstMost = 8;

        /// <summary>How long a glide between bursts lasts, seconds, and how fast it sinks.</summary>
        public const float GlideShortest = 0.3f, GlideLongest = 1.0f, GlideSink = 0.35f;

        /// <summary>Beats a second, dealt per butterfly. Never over ten, or it strobes at 60 fps (e-13 §13).</summary>
        public const float BeatSlowest = 8.5f, BeatQuickest = 10f;

        /// <summary>How high over the grass a butterfly wanders, metres.</summary>
        public const float AltitudeLowest = 0.6f, AltitudeHighest = 2.6f;

        /// <summary>How high over the grass a resting butterfly sits: on the tufts, not in the soil.</summary>
        public const float RestHeight = 0.3f;

        /// <summary>The lowest it ever flies over whatever is beneath it.</summary>
        public const float Clearance = 0.2f;

        /// <summary>How long it wanders before it looks for somewhere to land, seconds.</summary>
        public const float FlightShortest = 4f, FlightLongest = 14f;

        /// <summary>How far from where it is it looks for a landing spot, metres, and how many places it tries.</summary>
        public const float LandingReach = 6f;
        public const int LandingTries = 6;

        /// <summary>How long a rest lasts, seconds, and the share of rests spent basking.</summary>
        public const float RestShortest = 2f, RestLongest = 20f, BaskChance = 1f / 3f;

        /// <summary>
        /// A walker this close, horizontally, and within <see cref="StartleHeight"/> vertically, puts a
        /// butterfly up (e-13 §8: observers kept about three metres back; a cell is 2.5 m).
        /// </summary>
        public const float StartleRadius = 2.5f, StartleHeight = 3f;

        /// <summary>How fast and how long a startled butterfly flies away, and how high it climbs.</summary>
        public const float FleeSpeed = 2.6f, FleeShortest = 1.5f, FleeLongest = 3f, FleeAltitude = 3.5f;

        /// <summary>How fast a butterfly fades in or out, in scale a second: half a second either way.</summary>
        public const float FadeRate = 2f;

        /// <summary>How far past the window's edge one may stray before it fades, as a multiple of the radius.</summary>
        public const float StrayFactor = 1.15f;

        /// <summary>
        /// The window's radius for a camera this far from its focus, metres: about the view's
        /// half-height, so the meadow covers what is on screen, within bounds that keep a close
        /// view from being empty and a far one from spreading the count across a whole board.
        /// </summary>
        public static float RadiusFor(float cameraDistance) => Clamp(cameraDistance * 0.75f, 18f, 80f);

        /// <summary>The window at the default camera (48 m), the one the ladder's counts are for.</summary>
        public static readonly float ReferenceRadius = RadiusFor(48f);

        /// <summary>
        /// How many the season wants, 0 to 1, by the calendar (design 52 §3): fullest in Tansy, thinning
        /// through Glare, **none in Rime**. Blended across each month by its centre, so it thins rather
        /// than switching.
        /// </summary>
        public static float SeasonFor(long tick)
        {
            if (Calendar.SeasonOfYear(tick) == 2) return 0f;
            int month = Calendar.MonthOfYear(tick);
            float within = tick % Calendar.TicksPerMonth / (float)Calendar.TicksPerMonth;
            // Measured from the month's centre: before it, blend with the month before; after, the
            // next — halfway to it at the month's edge, where the neighbour blends back the same way,
            // so the curve is continuous across the boundary.
            int other = within < 0.5f ? (month + MonthCentres.Length - 1) % MonthCentres.Length : (month + 1) % MonthCentres.Length;
            float t = Math.Abs(within - 0.5f);
            return MonthCentres[month] + (MonthCentres[other] - MonthCentres[month]) * t;
        }

        /// <summary>How full the meadow is at the middle of each month, Larkspur to Candle.</summary>
        static readonly float[] MonthCentres = { 0.8f, 1f, 0.85f, 0.6f, 0f, 0f };

        /// <summary>
        /// How many the sky wants, 0 to 1 (design 52 §3): cloud thins them to six in ten, and rain
        /// sends them down — none at all once it rains at three-tenths.
        /// </summary>
        public static float WeatherFor(float cloud, float rain) =>
            (1f - CloudThinning * Clamp01(cloud)) * Clamp01(1f - rain / RainGone);

        public const float CloudThinning = 0.4f, RainGone = 0.3f;

        /// <summary>
        /// Price the rung, not the calendar: every meadow at its fullest season. A measurement control,
        /// the way <c>PawnCrowdIndex.Mode</c> is — the timing arm (<c>FrameTimeTests.TheButterfliesAgainstTheFrame</c>)
        /// starts on the first day of Larkspur, when the meadow is at four-tenths, and walking the
        /// clock to Tansy is a million ticks. Static so a test can set it between two timed stretches
        /// of one run. Never set in play.
        /// </summary>
        public static bool FullSeason { get; set; }

        /// <summary>
        /// How many should be alive: the rung, times the share of the window that is habitat, times
        /// how much of the reference window this one is (zoomed in, the same density means fewer),
        /// never more than the rung, times the season and the weather.
        /// </summary>
        public static int TargetFor(int rung, float share, float radius, float season, float weather)
        {
            float area = radius / ReferenceRadius;
            area = Math.Min(1f, area * area);
            return (int)Math.Round(rung * Clamp01(share) * area * Clamp01(season) * Clamp01(weather));
        }

        // ------------------------------------------------------------------ the state

        const float MaxStep = 0.05f;
        const int MaxSteps = 4;
        const int ShareSamples = 24, ShareSamplesFresh = 128;

        uint _rng;
        int _capacity;
        int _cursor;
        int _frame;
        float _focusX = float.NaN, _focusZ = float.NaN;

        ButterflyState[] _state = Array.Empty<ButterflyState>();
        float[] _x = Array.Empty<float>(), _y = Array.Empty<float>(), _z = Array.Empty<float>();
        float[] _yaw = Array.Empty<float>(), _wantYaw = Array.Empty<float>(), _speed = Array.Empty<float>();
        float[] _vy = Array.Empty<float>(), _ground = Array.Empty<float>(), _altitude = Array.Empty<float>();
        float[] _flap = Array.Empty<float>(), _flapWant = Array.Empty<float>(), _rest = Array.Empty<float>();
        float[] _scale = Array.Empty<float>(), _bank = Array.Empty<float>(), _pitch = Array.Empty<float>();
        float[] _rate = Array.Empty<float>(), _legTimer = Array.Empty<float>(), _beatTimer = Array.Empty<float>();
        float[] _stateTimer = Array.Empty<float>(), _targetX = Array.Empty<float>(), _targetZ = Array.Empty<float>();
        float[] _targetY = Array.Empty<float>();
        bool[] _bask = Array.Empty<bool>();
        int[] _seed = Array.Empty<int>();

        public ButterflyMeadow(uint seed, int capacity)
        {
            _rng = seed == 0 ? 0x9E3779B9u : seed;
            Resize(capacity);
        }

        /// <summary>How many butterflies the meadow can hold: the ladder's rung.</summary>
        public int Capacity => _capacity;

        /// <summary>How many are alive (every state but <see cref="ButterflyState.Empty"/>).</summary>
        public int Live { get; private set; }

        /// <summary>How many the last step wanted alive.</summary>
        public int Target { get; private set; }

        /// <summary>The share of the window the last estimate found to be habitat, 0 to 1.</summary>
        public float Share { get; private set; }

        /// <summary>
        /// Take a new capacity — the ladder's rung — keeping whoever fits. Allocates, once, on the
        /// press; a step never does.
        /// </summary>
        public void Resize(int capacity)
        {
            capacity = Math.Max(0, capacity);
            if (capacity == _capacity) return;
            int keep = Math.Min(capacity, _capacity);
            Array.Resize(ref _state, capacity);
            Array.Resize(ref _x, capacity);
            Array.Resize(ref _y, capacity);
            Array.Resize(ref _z, capacity);
            Array.Resize(ref _yaw, capacity);
            Array.Resize(ref _wantYaw, capacity);
            Array.Resize(ref _speed, capacity);
            Array.Resize(ref _vy, capacity);
            Array.Resize(ref _ground, capacity);
            Array.Resize(ref _altitude, capacity);
            Array.Resize(ref _flap, capacity);
            Array.Resize(ref _flapWant, capacity);
            Array.Resize(ref _rest, capacity);
            Array.Resize(ref _scale, capacity);
            Array.Resize(ref _bank, capacity);
            Array.Resize(ref _pitch, capacity);
            Array.Resize(ref _rate, capacity);
            Array.Resize(ref _legTimer, capacity);
            Array.Resize(ref _beatTimer, capacity);
            Array.Resize(ref _stateTimer, capacity);
            Array.Resize(ref _targetX, capacity);
            Array.Resize(ref _targetZ, capacity);
            Array.Resize(ref _targetY, capacity);
            Array.Resize(ref _bask, capacity);
            Array.Resize(ref _seed, capacity);
            _capacity = capacity;
            _cursor = 0;
            int live = 0;
            for (int i = 0; i < keep; i++) if (_state[i] != ButterflyState.Empty) live++;
            Live = live;
        }

        /// <summary>Everybody gone, at once: a new session, or the ladder turned to Off.</summary>
        public void Clear()
        {
            Array.Clear(_state, 0, _state.Length);
            Live = 0;
            Target = 0;
            _focusX = _focusZ = float.NaN;
        }

        public ButterflyState StateAt(int i) => _state[i];
        public float XAt(int i) => _x[i];
        public float YAt(int i) => _y[i];
        public float ZAt(int i) => _z[i];
        public float ScaleAt(int i) => _scale[i];
        public float FlapAt(int i) => _flap[i];
        public float RestAt(int i) => _rest[i];

        // ------------------------------------------------------------------ the step

        /// <summary>
        /// Advance the meadow by <paramref name="seconds"/> of real time — zero while the world is
        /// paused, which moves nothing at all. <paramref name="walkers"/> is x, y, z in metres for
        /// every colonist and animal drawn this frame, the birds' shape (design 50 §5).
        /// </summary>
        public void Step(float seconds, in ButterflyConditions sky, IButterflyHabitat habitat,
            float focusX, float focusZ, float radius, ReadOnlySpan<float> walkers)
        {
            if (seconds <= 0f || _capacity == 0) return;

            // A hitch is not a leap: at most a fifth of a second is simulated, in short steps.
            seconds = Math.Min(seconds, MaxStep * MaxSteps);
            int steps = Math.Max(1, (int)Math.Ceiling(seconds / MaxStep));
            float dt = seconds / steps;

            Populate(sky, habitat, focusX, focusZ, radius);

            for (int s = 0; s < steps; s++)
            {
                _frame++;
                for (int i = 0; i < _capacity; i++)
                {
                    if (_state[i] == ButterflyState.Empty) continue;
                    Advance(i, dt, sky, habitat, focusX, focusZ, radius, walkers);
                }
            }
        }

        /// <summary>How many should be alive, and dealing or retiring towards it.</summary>
        void Populate(in ButterflyConditions sky, IButterflyHabitat habitat, float focusX, float focusZ, float radius)
        {
            // The habitat share: a fresh estimate when the focus has jumped, a running one otherwise.
            bool jumped = float.IsNaN(_focusX) ||
                          Sq(focusX - _focusX) + Sq(focusZ - _focusZ) > Sq(radius * 0.5f);
            int samples = jumped ? ShareSamplesFresh : ShareSamples;
            int hits = 0;
            for (int k = 0; k < samples; k++)
            {
                PointInDisc(focusX, focusZ, radius, out float sx, out float sz);
                if (habitat.Habitat(sx, sz, out _)) hits++;
            }
            float sampled = hits / (float)samples;
            Share = jumped ? sampled : Share + (sampled - Share) * 0.1f;
            _focusX = focusX;
            _focusZ = focusZ;

            float season = FullSeason ? 1f : SeasonFor(sky.Tick);
            Target = TargetFor(_capacity, Share, radius, season, WeatherFor(sky.Cloud, sky.Rain));

            // Deal the shortfall. The first population arrives whole, at rest and fading in; after
            // that a few at a time, so a pan refills the window over a handful of frames.
            int shortfall = Target - Live;
            if (shortfall > 0)
            {
                int budget = Live == 0 ? shortfall : Math.Max(4, shortfall / 6);
                for (int b = 0; b < budget && Live < Target; b++)
                    if (!TrySpawn(habitat, focusX, focusZ, radius)) break;
            }

            // Retire the surplus a few at a time, fading. A small margin, so the count does not
            // hunt around a target that moves with every habitat estimate — and none at all when
            // the target is nought, or a downpour leaves two butterflies out in it for ever.
            int surplus = Live - Target - (Target == 0 ? 0 : Math.Max(2, Target / 10));
            for (int attempt = 0; attempt < 8 && surplus > 0; attempt++)
            {
                int at = (int)(Next() % (uint)_capacity);
                if (_state[at] == ButterflyState.Empty || _state[at] == ButterflyState.Leaving) continue;
                _state[at] = ButterflyState.Leaving;
                surplus--;
            }
        }

        bool TrySpawn(IButterflyHabitat habitat, float focusX, float focusZ, float radius)
        {
            // A free slot, searched from where the last one was found, so dealing a whole meadow at
            // once is not a scan of every slot per butterfly.
            int slot = -1;
            for (int n = 0; n < _capacity; n++)
            {
                int at = (_cursor + n) % _capacity;
                if (_state[at] != ButterflyState.Empty) continue;
                slot = at;
                _cursor = (at + 1) % _capacity;
                break;
            }
            if (slot < 0) return false;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                PointInDisc(focusX, focusZ, radius, out float x, out float z);
                if (!habitat.Habitat(x, z, out float ground)) continue;

                _x[slot] = x;
                _z[slot] = z;
                _ground[slot] = ground;
                _y[slot] = ground + RestHeight;
                _yaw[slot] = _wantYaw[slot] = Range(0f, Tau);
                _speed[slot] = 0f;
                _vy[slot] = 0f;
                _flap[slot] = _flapWant[slot] = 0f;
                _rest[slot] = 1f;
                _scale[slot] = 0f;
                _bank[slot] = _pitch[slot] = 0f;
                _rate[slot] = Range(BeatSlowest, BeatQuickest);
                _seed[slot] = (int)(Next() & 0xFFFFFF);
                _bask[slot] = Unit() < BaskChance;
                _altitude[slot] = Range(AltitudeLowest, AltitudeHighest);
                _state[slot] = ButterflyState.Resting;
                // Born at rest, part-way through it, so a warm start does not take off in step.
                _stateTimer[slot] = Range(0.5f, RestLongest * 0.6f);
                Live++;
                return true;
            }
            return false;
        }

        void Advance(int i, float dt, in ButterflyConditions sky, IButterflyHabitat habitat,
            float focusX, float focusZ, float radius, ReadOnlySpan<float> walkers)
        {
            ButterflyState state = _state[i];

            // Fading: in for everyone arriving, out for anyone leaving.
            if (state == ButterflyState.Leaving)
            {
                _scale[i] -= FadeRate * dt;
                if (_scale[i] <= 0f)
                {
                    _state[i] = ButterflyState.Empty;
                    Live--;
                    return;
                }
            }
            else if (_scale[i] < 1f) _scale[i] = Math.Min(1f, _scale[i] + FadeRate * dt);

            // Strayed out of the window: fade where it is.
            if (state != ButterflyState.Leaving &&
                Sq(_x[i] - focusX) + Sq(_z[i] - focusZ) > Sq(radius * StrayFactor))
            {
                _state[i] = state = ButterflyState.Leaving;
            }

            // A walker too close — checked on one step in four, staggered, so a crowd costs a
            // quarter of what it would. At a tenth of a second between checks nobody is missed.
            if (state != ButterflyState.Fleeing && state != ButterflyState.Leaving && ((_frame + i) & 3) == 0 &&
                Startled(i, walkers, out float awayX, out float awayZ))
            {
                Flee(i, awayX, awayZ);
                state = ButterflyState.Fleeing;
            }

            float yawBefore = _yaw[i];
            switch (state)
            {
                case ButterflyState.Resting:
                    _stateTimer[i] -= dt;
                    _flapWant[i] = 0f;
                    if (_stateTimer[i] <= 0f) TakeOff(i);
                    break;

                case ButterflyState.Flying:
                case ButterflyState.Leaving:
                    Wander(i, dt, habitat, focusX, focusZ, radius);
                    if (state == ButterflyState.Flying)
                    {
                        _stateTimer[i] -= dt;
                        if (_stateTimer[i] <= 0f) ChooseLanding(i, habitat);
                    }
                    break;

                case ButterflyState.Landing:
                    Approach(i, dt);
                    break;

                case ButterflyState.Fleeing:
                    _stateTimer[i] -= dt;
                    _flapWant[i] = 1f;
                    Move(i, dt, habitat, _ground[i] + FleeAltitude, climb: 1.6f);
                    if (_stateTimer[i] <= 0f) Resume(i);
                    break;
            }

            // The drift of the wind, on anything in the air.
            if (_state[i] != ButterflyState.Resting)
            {
                _x[i] += sky.WindX * dt;
                _z[i] += sky.WindZ * dt;
            }

            // The pose the shader reads: flapping eased, folding eased, a bank into the turn and a
            // nose up into a climb.
            float restWant = _state[i] == ButterflyState.Resting ? 1f : 0f;
            _flap[i] += (_flapWant[i] - _flap[i]) * Math.Min(1f, dt * 12f);
            _rest[i] += (restWant - _rest[i]) * Math.Min(1f, dt * 6f);
            float turn = WrapAngle(_yaw[i] - yawBefore) / Math.Max(dt, 1e-4f);
            _bank[i] += (Clamp(-turn * 0.25f, -0.7f, 0.7f) - _bank[i]) * Math.Min(1f, dt * 6f);
            _pitch[i] += (Clamp(_vy[i] * 0.35f, -0.45f, 0.45f) - _pitch[i]) * Math.Min(1f, dt * 6f);
        }

        void Wander(int i, float dt, IButterflyHabitat habitat, float focusX, float focusZ, float radius)
        {
            // A new leg: a new heading and a new pace.
            _legTimer[i] -= dt;
            if (_legTimer[i] <= 0f)
            {
                _legTimer[i] = Range(LegShortest, LegLongest);
                float swing = Unit() < HardTurnChance ? Range(2f, 3.2f) * (Unit() < 0.5f ? -1f : 1f) : Range(-LegTurn, LegTurn);
                _wantYaw[i] = _yaw[i] + swing;
                _speed[i] = Range(CruiseSlowest, CruiseQuickest);
                if (Unit() < 0.2f) _altitude[i] = Range(AltitudeLowest, AltitudeHighest);
            }

            // Bursts and glides.
            _beatTimer[i] -= dt;
            if (_beatTimer[i] <= 0f)
            {
                bool wasFlapping = _flapWant[i] > 0.5f;
                _flapWant[i] = wasFlapping ? 0f : 1f;
                _beatTimer[i] = wasFlapping
                    ? Range(GlideShortest, GlideLongest)
                    : (BurstFewest + (int)(Unit() * (BurstMost - BurstFewest + 1))) / _rate[i];
            }

            // Keep over the grass and inside the window: look a little ahead, one step in four, and
            // turn hard away — or, for one the wind has carried off the grass, make for the focus.
            if (((_frame + i) & 3) == 1)
            {
                const float Ahead = 1.8f;
                float ax = _x[i] + (float)Math.Sin(_wantYaw[i]) * Ahead;
                float az = _z[i] + (float)Math.Cos(_wantYaw[i]) * Ahead;
                if (!habitat.Habitat(ax, az, out _) || Sq(ax - focusX) + Sq(az - focusZ) > Sq(radius))
                {
                    if (habitat.Habitat(_x[i], _z[i], out _))
                        _wantYaw[i] += Range(1.9f, 2.9f) * ((_seed[i] & 1) == 0 ? 1f : -1f);
                    else
                        _wantYaw[i] = (float)Math.Atan2(focusX - _x[i], focusZ - _z[i]);
                    _legTimer[i] = Math.Max(_legTimer[i], 0.4f);
                }
            }

            float lift = _flapWant[i] > 0.5f ? 0f : -GlideSink;
            Move(i, dt, habitat, float.NaN, climb: 1.2f, glide: lift);
        }

        /// <summary>
        /// Turn, go forward, and rise or fall towards <paramref name="wantY"/> (or the wandering
        /// altitude over whatever is below when it is NaN). A glide sinks instead of climbing.
        /// </summary>
        void Move(int i, float dt, IButterflyHabitat habitat, float wantY, float climb, float glide = 0f)
        {
            _wantYaw[i] = WrapAngle(_wantYaw[i]);
            float step = WrapAngle(_wantYaw[i] - _yaw[i]);
            float most = TurnRate * dt;
            _yaw[i] = WrapAngle(_yaw[i] + Clamp(step, -most, most));

            float speed = _state[i] == ButterflyState.Fleeing ? FleeSpeed : _speed[i];
            _x[i] += (float)Math.Sin(_yaw[i]) * speed * dt;
            _z[i] += (float)Math.Cos(_yaw[i]) * speed * dt;

            float below = habitat.Habitat(_x[i], _z[i], out float grass) ? grass : habitat.Surface(_x[i], _z[i]);
            _ground[i] = below;
            if (float.IsNaN(wantY)) wantY = below + _altitude[i];
            _vy[i] = glide < 0f ? glide : Clamp((wantY - _y[i]) * climb, -0.7f, 0.9f);
            _y[i] = Math.Max(below + Clearance, _y[i] + _vy[i] * dt);
        }

        void ChooseLanding(int i, IButterflyHabitat habitat)
        {
            float bestX = 0f, bestZ = 0f, bestY = 0f;
            bool found = false;
            for (int t = 0; t < LandingTries; t++)
            {
                PointInDisc(_x[i], _z[i], LandingReach, out float x, out float z);
                if (!habitat.Habitat(x, z, out float ground)) continue;
                bool flowers = habitat.Flowers(x, z);
                if (!found || flowers)
                {
                    bestX = x;
                    bestZ = z;
                    bestY = ground;
                    found = true;
                }
                if (flowers) break;
            }

            if (!found)
            {
                // Nowhere near to land: wander a little longer and try again.
                _stateTimer[i] = 2f;
                return;
            }

            _state[i] = ButterflyState.Landing;
            _targetX[i] = bestX;
            _targetZ[i] = bestZ;
            _targetY[i] = bestY + RestHeight;
            _stateTimer[i] = 8f;
            _flapWant[i] = 1f;
        }

        void Approach(int i, float dt)
        {
            float dx = _targetX[i] - _x[i], dz = _targetZ[i] - _z[i];
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            _stateTimer[i] -= dt;

            if (distance < 0.25f && Math.Abs(_y[i] - _targetY[i]) < 0.25f)
            {
                _state[i] = ButterflyState.Resting;
                _x[i] = _targetX[i];
                _z[i] = _targetZ[i];
                _y[i] = _targetY[i];
                _ground[i] = _targetY[i] - RestHeight;
                _vy[i] = 0f;
                _flapWant[i] = 0f;
                _bask[i] = Unit() < BaskChance;
                _stateTimer[i] = Range(RestShortest, RestLongest);
                return;
            }
            if (_stateTimer[i] <= 0f)
            {
                Resume(i);
                return;
            }

            _wantYaw[i] = (float)Math.Atan2(dx, dz);
            float step = WrapAngle(_wantYaw[i] - _yaw[i]);
            float most = TurnRate * 1.5f * dt;
            _yaw[i] = WrapAngle(_yaw[i] + Clamp(step, -most, most));

            float speed = Math.Min(_speed[i] <= 0f ? CruiseSlowest : _speed[i], distance * 1.5f + 0.2f);
            float forward = Math.Min(distance, speed * dt);
            _x[i] += (float)Math.Sin(_yaw[i]) * forward;
            _z[i] += (float)Math.Cos(_yaw[i]) * forward;

            // Descend as it closes: all the way down in the last metre and a half.
            float wantY = _targetY[i] + Math.Min(distance * 0.6f, 2f);
            _vy[i] = Clamp((wantY - _y[i]) * 2f, -1f, 0.9f);
            _y[i] += _vy[i] * dt;
            _ground[i] = _targetY[i] - RestHeight;
        }

        void TakeOff(int i)
        {
            _state[i] = ButterflyState.Flying;
            _vy[i] = 1.2f;
            _flapWant[i] = 1f;
            _beatTimer[i] = (BurstFewest + (int)(Unit() * 3)) / _rate[i];
            _legTimer[i] = 0f;
            _speed[i] = Range(CruiseSlowest, CruiseQuickest);
            _stateTimer[i] = Range(FlightShortest, FlightLongest);
        }

        void Resume(int i)
        {
            _state[i] = ButterflyState.Flying;
            _legTimer[i] = 0f;
            _stateTimer[i] = Range(FlightShortest, FlightLongest);
        }

        void Flee(int i, float awayX, float awayZ)
        {
            _state[i] = ButterflyState.Fleeing;
            _wantYaw[i] = (float)Math.Atan2(awayX, awayZ) + Range(-0.5f, 0.5f);
            // A startled butterfly leaves on its first stroke rather than turning first.
            _yaw[i] = _wantYaw[i];
            _flapWant[i] = 1f;
            _vy[i] = 1.6f;
            _stateTimer[i] = Range(FleeShortest, FleeLongest);
        }

        bool Startled(int i, ReadOnlySpan<float> walkers, out float awayX, out float awayZ)
        {
            awayX = awayZ = 0f;
            float x = _x[i], y = _y[i], z = _z[i];
            float nearest = StartleRadius * StartleRadius;
            bool found = false;
            for (int w = 0; w + 2 < walkers.Length; w += 3)
            {
                float dx = x - walkers[w], dz = z - walkers[w + 2];
                float d2 = dx * dx + dz * dz;
                if (d2 >= nearest || Math.Abs(y - walkers[w + 1]) > StartleHeight) continue;
                nearest = d2;
                // Straight above a walker there is no "away"; any heading will do.
                awayX = d2 > 1e-6f ? dx : 1f;
                awayZ = d2 > 1e-6f ? dz : 0f;
                found = true;
            }
            return found;
        }

        // ------------------------------------------------------------------ the drawing's view

        /// <summary>Floats each butterfly packs into the draw buffer: four float4s.</summary>
        public const int PackedFloats = 16;

        /// <summary>
        /// Write every drawn butterfly into <paramref name="into"/>, <see cref="PackedFloats"/> each,
        /// and say how many were written. Only those whose ground lies between
        /// <paramref name="lowestY"/> and <paramref name="highestY"/> metres are drawn — the layers the
        /// slice draws solid — so looking down a mine does not show the meadow's butterflies over it.
        ///
        /// <para>The layout, which <c>OdysseyButterfly.shader</c> reads by instance id:
        /// (x, y, z, heading) · (ground, flap, rest, scale) · (seed, bank, pitch, beats a second) ·
        /// (bask, 0, 0, 0).</para>
        /// </summary>
        public int Pack(Span<float> into, float lowestY, float highestY)
        {
            int count = 0;
            int room = into.Length / PackedFloats;
            for (int i = 0; i < _capacity && count < room; i++)
            {
                if (_state[i] == ButterflyState.Empty || _scale[i] <= 0f) continue;
                float ground = _ground[i];
                if (ground < lowestY - 0.01f || ground >= highestY) continue;

                int o = count * PackedFloats;
                into[o] = _x[i];
                into[o + 1] = _y[i];
                into[o + 2] = _z[i];
                into[o + 3] = _yaw[i];
                into[o + 4] = ground;
                into[o + 5] = _flap[i];
                into[o + 6] = _rest[i];
                into[o + 7] = _scale[i];
                into[o + 8] = _seed[i];
                into[o + 9] = _bank[i];
                into[o + 10] = _pitch[i];
                into[o + 11] = _rate[i];
                into[o + 12] = _bask[i] ? 1f : 0f;
                into[o + 13] = 0f;
                into[o + 14] = 0f;
                into[o + 15] = 0f;
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ small things

        const float Tau = 6.2831853f;

        void PointInDisc(float cx, float cz, float radius, out float x, out float z)
        {
            float r = radius * (float)Math.Sqrt(Unit());
            float a = Unit() * Tau;
            x = cx + (float)Math.Sin(a) * r;
            z = cz + (float)Math.Cos(a) * r;
        }

        uint Next()
        {
            // xorshift32: cheap, allocation-free, and the same sequence on every machine.
            uint x = _rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rng = x;
            return x;
        }

        float Unit() => (Next() >> 8) * (1f / 16777216f);

        float Range(float lo, float hi) => lo + (hi - lo) * Unit();

        static float Sq(float v) => v * v;

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        static float Clamp01(float v) => Clamp(v, 0f, 1f);

        static float WrapAngle(float a)
        {
            while (a > Math.PI) a -= Tau;
            while (a < -Math.PI) a += Tau;
            return a;
        }
    }
}
