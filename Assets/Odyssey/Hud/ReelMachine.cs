#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>Where the machine as a whole stands (design 41 §6).</summary>
    public enum MachinePhase
    {
        /// <summary>Before the pull: nothing moves.</summary>
        Idle,

        /// <summary>Every reel spinning, waiting for STOP.</summary>
        Spinning,

        /// <summary>STOP pressed: the reels are landing one after another in reading order.</summary>
        Stopping,

        /// <summary>Every reel down; the verdict is showing.</summary>
        Landed,
    }

    /// <summary>Where one reel stands.</summary>
    public enum ReelPhase
    {
        Resting,
        Spinning,

        /// <summary>Braking to its value over the ordinary time.</summary>
        Braking,

        /// <summary>Braking to a hot value over the long time: the suspense (design 41 §6.3).</summary>
        Teasing,

        Landed,
    }

    /// <summary>What the machine says when the last reel lands.</summary>
    public enum Verdict
    {
        None,
        Plain,
        Jackpot,
        Dud,
    }

    /// <summary>One bulb of the marquee.</summary>
    public enum BulbLight
    {
        Off,
        Dim,
        Lit,
        Gold,
    }

    /// <summary>
    /// The verdict and the heat, as rules with their numbers in one place (design 41 §6.4). The
    /// numbers are the design's placeholders and are constants on purpose, so they can be tuned
    /// without touching anything that draws.
    /// </summary>
    public static class DrawVerdict
    {
        /// <summary>A skill at or above this lands hot and teases on the way.</summary>
        public const int HotThreshold = 11;

        /// <summary>A jackpot: any skill at or above this with a major passion behind it...</summary>
        public const int JackpotSkill = 13;

        /// <summary>...or a trait of at least this worth (an extreme good one).</summary>
        public const int StarWorth = 4;

        /// <summary>A dud: the budget skills adding to no more than this...</summary>
        public const int DudSumAtMost = 6;

        /// <summary>...or a trait of at most this worth (an extreme bad one).</summary>
        public const int FlawWorth = -4;

        public static bool IsHotSkill(int level) => level >= HotThreshold;

        public static bool IsStar(int worth) => worth >= StarWorth;

        public static bool IsFlaw(int worth) => worth <= FlawWorth;

        /// <summary>
        /// The verdict on one colonist. A jackpot beats a dud: a Prodigy who is also Bottomless is
        /// still the pull the table celebrates, and the flaw window still lands cold beside it.
        /// </summary>
        /// <param name="levels">The budget skills' levels.</param>
        /// <param name="passions">Their passions, 0 none, 1 minor, 2 major, in the same order.</param>
        /// <param name="traitWorths">The worth of every trait dealt.</param>
        public static Verdict Of(IReadOnlyList<int> levels, IReadOnlyList<int> passions, IReadOnlyList<int> traitWorths)
        {
            bool jackpot = false, flaw = false;
            int sum = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                sum += levels[i];
                if (levels[i] >= JackpotSkill && i < passions.Count && passions[i] >= 2) jackpot = true;
            }

            for (int i = 0; i < traitWorths.Count; i++)
            {
                if (IsStar(traitWorths[i])) jackpot = true;
                if (IsFlaw(traitWorths[i])) flaw = true;
            }

            if (jackpot) return Verdict.Jackpot;
            if (flaw || sum <= DudSumAtMost) return Verdict.Dud;
            return Verdict.Plain;
        }
    }

    /// <summary>
    /// The fruit machine (design 41 §6), as numbers the view reads each frame and nothing else.
    ///
    /// <para><b>The outcome is decided before a reel moves</b> (owner, decision 2). The presenter
    /// draws the colonist at PULL and hands this the value each reel must land on at STOP. So the
    /// reels are a show, timing cannot be a skill, and the tables keep their meaning.</para>
    ///
    /// <para><b>How a reel lands exactly without cheating the eye.</b> Each reel spins at a speed of
    /// its own. When its turn in the cascade comes it brakes at a constant deceleration, and the
    /// point where that deceleration brings it to rest is worked out at that moment, rounded up to a
    /// whole symbol — and the target value is written into the strip at that point, which is at
    /// least a couple of symbols ahead and so out of the window. The reel then simply stops where
    /// physics puts it, on the value it was always going to show. No speed-up, no jump, no snap.</para>
    ///
    /// <para><b>The cascade</b> runs in the order the reels were given (reading order): each lands
    /// <see cref="LandStagger"/> or a little more after the one before. A reel landing hot brakes
    /// over <see cref="TeaseSeconds"/> instead of <see cref="BrakeSeconds"/> — the slot machine's
    /// anticipation rule, and honest here because the value is fixed.</para>
    ///
    /// <para>Unity-free and stepped by a time passed in, like <see cref="ToastModel"/>, so every
    /// property of it is a fast-tier test. It allocates at <see cref="Start"/> and never again.</para>
    /// </summary>
    public sealed class ReelMachine
    {
        // ---- the tuning (design 41 §6, the spec's constants) ------------------------------------

        /// <summary>At least this long between one reel landing and the next.</summary>
        public const double LandStagger = 0.12;

        /// <summary>An ordinary reel's braking time.</summary>
        public const double BrakeSeconds = 0.35;

        /// <summary>A hot reel's braking time: the tease.</summary>
        public const double TeaseSeconds = 0.9;

        /// <summary>How far past its value a landing reel swings before it settles, in symbols.</summary>
        public const double OvershootSymbols = 0.2;

        public const double OvershootSeconds = 0.06;

        /// <summary>The shortest spin: a STOP pressed sooner waits until the reels are up to speed.</summary>
        public const double MinSpinSeconds = 0.45;

        /// <summary>How long the reels take to come up to speed from rest.</summary>
        public const double SpinUpSeconds = 0.25;

        /// <summary>The marquee chase: every third bulb lit, advancing one bulb a step.</summary>
        public const double ChaseStep = 0.08;

        public const double JackpotFlash = 0.15;
        public const int JackpotFlashes = 3;

        /// <summary>How long a hot window takes to turn amber once it lands.</summary>
        public const double HotFade = 0.2;

        /// <summary>How long a dud takes to put the lights out, right to left.</summary>
        public const double DudFade = 0.6;

        /// <summary>How many bulbs a dud leaves dim at the left, so the machine is not dead.</summary>
        public const int DudKeptDim = 3;

        /// <summary>
        /// Symbols a second, one per reel in turn (the owner: "each stat going at a completely
        /// different speed"). No two are near a small ratio of each other, so no pair ever visibly
        /// locks step; <c>ReelMachineTests</c> holds them to that.
        /// </summary>
        public static readonly double[] Speeds = { 23.1, 11.0, 17.0, 18.1, 12.3, 15.9, 19.5, 14.2, 13.2, 15.1, 21.7, 20.8 };

        sealed class Reel
        {
            public int[] Strip = Array.Empty<int>();
            public double Position;
            public double Speed;
            public ReelPhase Phase;
            public int Target;
            public bool Hot;
            public double BrakeStart;
            public double BrakeDuration;
            public double BrakeFrom;
            public double LandsAt;
            public long Stop;
        }

        Reel[] _reels = Array.Empty<Reel>();
        double _now;
        double _startedAt;
        double _landedAt;
        bool _stopAsked;
        int[] _targets = Array.Empty<int>();
        bool[] _hot = Array.Empty<bool>();
        Verdict _verdict;

        /// <summary>How many reels the current pull has.</summary>
        public int Count => _reels.Length;

        public MachinePhase Phase { get; private set; } = MachinePhase.Idle;

        /// <summary>The machine's own clock, in seconds since it was made.</summary>
        public double Now => _now;

        /// <summary>The verdict, once the last reel has landed; <see cref="Verdict.None"/> before.</summary>
        public Verdict Verdict => Phase == MachinePhase.Landed ? _verdict : Verdict.None;

        /// <summary>Raised as each reel lands, with its index — the clunk, and the hot ding.</summary>
        public event Action<int>? ReelLanded;

        /// <summary>Raised once, when the last reel has landed.</summary>
        public event Action<Verdict>? Finished;

        /// <summary>
        /// Start a pull: every reel spinning through a seeded shuffle of its symbols.
        /// </summary>
        /// <param name="symbolCounts">How many symbols each reel carries, in reading order.</param>
        /// <param name="seed">What shuffles the strips; any number will do, the outcome is not in it.</param>
        public void Start(IReadOnlyList<int> symbolCounts, uint seed)
        {
            if (symbolCounts == null) throw new ArgumentNullException(nameof(symbolCounts));
            _reels = new Reel[symbolCounts.Count];
            _targets = new int[symbolCounts.Count];
            _hot = new bool[symbolCounts.Count];
            uint state = seed == 0 ? 0x9E3779B9u : seed;
            for (int i = 0; i < _reels.Length; i++)
            {
                int n = Math.Max(1, symbolCounts[i]);
                // Short sets repeat, so every strip is long enough to keep the target out of the
                // window at the moment it is written (a couple of symbols ahead).
                int length = n;
                while (length < 8) length += n;
                var strip = new int[length];
                for (int k = 0; k < length; k++) strip[k] = k % n;
                for (int k = length - 1; k > 0; k--)
                {
                    state = Next(state);
                    int j = (int)(state % (uint)(k + 1));
                    (strip[k], strip[j]) = (strip[j], strip[k]);
                }

                state = Next(state);
                _reels[i] = new Reel
                {
                    Strip = strip,
                    Speed = Speeds[i % Speeds.Length],
                    Position = state % (uint)length,
                    Phase = ReelPhase.Spinning,
                };
            }

            _startedAt = _now;
            _stopAsked = false;
            _verdict = Verdict.None;
            Phase = MachinePhase.Spinning;
        }

        /// <summary>
        /// STOP: every reel will land on <paramref name="targets"/> in turn, the hot ones slowly.
        /// Pressed before the reels are up to speed, it waits for them. Ignored unless spinning.
        /// </summary>
        /// <param name="targets">The symbol each reel lands on, in reading order.</param>
        /// <param name="hot">Which of them land hot, and so tease.</param>
        /// <param name="verdict">What the machine says at the end.</param>
        public bool Stop(IReadOnlyList<int> targets, IReadOnlyList<bool> hot, Verdict verdict)
        {
            if (Phase != MachinePhase.Spinning || _stopAsked) return false;
            if (targets == null || targets.Count != _reels.Length) throw new ArgumentException("one target per reel", nameof(targets));
            for (int i = 0; i < _reels.Length; i++)
            {
                _targets[i] = targets[i];
                _hot[i] = hot != null && i < hot.Count && hot[i];
            }

            _verdict = verdict;
            _stopAsked = true;
            if (_now - _startedAt >= MinSpinSeconds) Schedule();
            return true;
        }

        /// <summary>Put the machine back at rest, showing nothing.</summary>
        public void Reset()
        {
            _reels = Array.Empty<Reel>();
            _stopAsked = false;
            _verdict = Verdict.None;
            Phase = MachinePhase.Idle;
        }

        /// <summary>Advance the machine by unscaled seconds. A negative step is ignored.</summary>
        public void Step(double seconds)
        {
            if (seconds <= 0) return;
            double from = _now;
            _now += seconds;
            if (Phase == MachinePhase.Idle || Phase == MachinePhase.Landed) return;

            if (Phase == MachinePhase.Spinning && _stopAsked && _now - _startedAt >= MinSpinSeconds)
                Schedule();

            int landed = 0;
            for (int i = 0; i < _reels.Length; i++)
            {
                Reel r = _reels[i];
                switch (r.Phase)
                {
                    case ReelPhase.Spinning:
                        r.Position += r.Speed * SpinUp(from, _now);
                        if (Phase == MachinePhase.Stopping && _now >= r.BrakeStart) BeginBrake(r, i);
                        break;
                }

                if (r.Phase == ReelPhase.Braking || r.Phase == ReelPhase.Teasing)
                {
                    if (_now >= r.LandsAt)
                    {
                        r.Phase = ReelPhase.Landed;
                        r.Position = r.Stop;
                        ReelLanded?.Invoke(i);
                    }
                    else
                    {
                        double t = _now - r.BrakeStart;
                        r.Position = r.BrakeFrom + r.Speed * t - r.Speed / (2 * r.BrakeDuration) * t * t;
                    }
                }

                if (r.Phase == ReelPhase.Landed) landed++;
            }

            if (Phase == MachinePhase.Stopping && landed == _reels.Length)
            {
                Phase = MachinePhase.Landed;
                _landedAt = _now;
                Finished?.Invoke(_verdict);
            }
        }

        /// <summary>
        /// The fraction of the step spent at full speed, ramped in over <see cref="SpinUpSeconds"/>
        /// so a pull starts with a lurch rather than a teleport. Integrated exactly over the step.
        /// </summary>
        double SpinUp(double from, double to)
        {
            double a = Math.Max(0, from - _startedAt), b = Math.Max(0, to - _startedAt);
            return Ramp(b) - Ramp(a);

            static double Ramp(double t) =>
                t >= SpinUpSeconds ? t - SpinUpSeconds / 2 : t * t / (2 * SpinUpSeconds);
        }

        /// <summary>
        /// Lay out the cascade: reel <i>i</i> lands no sooner than its own braking time from now and
        /// no sooner than <see cref="LandStagger"/> after reel <i>i − 1</i>. Its braking start follows
        /// from that; the exact landing point is worked out when it gets there.
        /// </summary>
        void Schedule()
        {
            Phase = MachinePhase.Stopping;
            double previous = double.NegativeInfinity;
            for (int i = 0; i < _reels.Length; i++)
            {
                Reel r = _reels[i];
                r.Hot = _hot[i];
                r.Target = _targets[i];
                double brake = r.Hot ? TeaseSeconds : BrakeSeconds;
                // The worst a braking reel can overrun its nominal time is one symbol's worth at
                // its speed (the rounding up to a whole symbol), so the next reel is laid out
                // against that, which keeps the order even when the rounding is unkind.
                double lands = Math.Max(_now + brake, previous + LandStagger);
                r.BrakeStart = lands - brake;
                previous = lands + 2.0 / r.Speed;
            }
        }

        void BeginBrake(Reel r, int index)
        {
            double brake = r.Hot ? TeaseSeconds : BrakeSeconds;
            // Where constant deceleration from this speed over the nominal time would bring it to
            // rest, rounded up to a whole symbol; then the exact time that takes.
            long stop = (long)Math.Ceiling(r.Position + r.Speed * brake / 2);
            double distance = stop - r.Position;
            r.Stop = stop;
            r.BrakeFrom = r.Position;
            r.BrakeStart = _now;
            r.BrakeDuration = 2 * distance / r.Speed;
            r.LandsAt = _now + r.BrakeDuration;
            r.Phase = r.Hot ? ReelPhase.Teasing : ReelPhase.Braking;
            // The value goes into the strip where the reel will come to rest, which is at least a
            // couple of symbols ahead of the window, so it is never seen arriving.
            r.Strip[Mod(stop, r.Strip.Length)] = r.Target;
        }

        // ---- what the view reads ---------------------------------------------------------------

        public ReelPhase ReelPhaseOf(int reel) => (uint)reel < (uint)_reels.Length ? _reels[reel].Phase : ReelPhase.Resting;

        /// <summary>How many reels have landed: the "9 / 16" on the button.</summary>
        public int LandedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _reels.Length; i++) if (_reels[i].Phase == ReelPhase.Landed) n++;
                return n;
            }
        }

        /// <summary>
        /// Where a reel is, in symbols, including the settle after landing — the view takes the
        /// nearest whole symbol as the one in the window and the fraction as how far it has
        /// scrolled past it.
        /// </summary>
        public double PositionOf(int reel)
        {
            if ((uint)reel >= (uint)_reels.Length) return 0;
            Reel r = _reels[reel];
            if (r.Phase != ReelPhase.Landed) return r.Position;
            double since = _now - r.LandsAt;
            if (since >= OvershootSeconds) return r.Stop;
            return r.Stop + OvershootSymbols * Math.Sin(Math.PI * since / OvershootSeconds);
        }

        /// <summary>The symbol <paramref name="offset"/> places from the one nearest the window's middle.</summary>
        public int SymbolAt(int reel, int offset)
        {
            if ((uint)reel >= (uint)_reels.Length) return 0;
            Reel r = _reels[reel];
            long centre = (long)Math.Round(PositionOf(reel));
            return r.Strip[Mod(centre + offset, r.Strip.Length)];
        }

        /// <summary>How far past the middle symbol the reel has scrolled, −0.5 to 0.5 of a symbol.</summary>
        public double FractionOf(int reel)
        {
            double p = PositionOf(reel);
            return p - Math.Round(p);
        }

        /// <summary>The symbol a reel landed on, or -1 before it has.</summary>
        public int LandedSymbol(int reel) =>
            (uint)reel < (uint)_reels.Length && _reels[reel].Phase == ReelPhase.Landed
                ? _reels[reel].Strip[Mod(_reels[reel].Stop, _reels[reel].Strip.Length)]
                : -1;

        /// <summary>
        /// How far through its tease a reel is, 0 to 1, for the bars that pulse twice; 0 for a reel
        /// that is not teasing.
        /// </summary>
        public double TeaseProgress(int reel)
        {
            if ((uint)reel >= (uint)_reels.Length || _reels[reel].Phase != ReelPhase.Teasing) return 0;
            Reel r = _reels[reel];
            return Math.Min(1, (_now - r.BrakeStart) / r.BrakeDuration);
        }

        /// <summary>How far a landed hot window has turned amber, 0 to 1.</summary>
        public double HotAmount(int reel)
        {
            if ((uint)reel >= (uint)_reels.Length) return 0;
            Reel r = _reels[reel];
            if (!r.Hot || r.Phase != ReelPhase.Landed) return 0;
            return Math.Min(1, (_now - r.LandsAt) / HotFade);
        }

        /// <summary>One bulb of <paramref name="count"/> along a rail, left to right.</summary>
        public BulbLight Bulb(int bulb, int count)
        {
            switch (Phase)
            {
                case MachinePhase.Spinning:
                case MachinePhase.Stopping:
                    long step = (long)Math.Floor(_now / ChaseStep);
                    return Mod(bulb - step, 3) == 0 ? BulbLight.Lit : BulbLight.Dim;

                case MachinePhase.Landed:
                    double since = _now - _landedAt;
                    if (_verdict == Verdict.Jackpot)
                    {
                        if (since >= JackpotFlash * 2 * JackpotFlashes) return BulbLight.Gold;
                        return (long)Math.Floor(since / JackpotFlash) % 2 == 0 ? BulbLight.Gold : BulbLight.Dim;
                    }

                    if (_verdict == Verdict.Dud)
                    {
                        if (bulb < DudKeptDim || count <= DudKeptDim) return BulbLight.Dim;
                        // Right to left: the rightmost goes first, the one next to the kept three last.
                        double at = (double)(count - 1 - bulb) / (count - DudKeptDim) * DudFade;
                        return since >= at ? BulbLight.Off : BulbLight.Dim;
                    }

                    return BulbLight.Lit;

                default:
                    return BulbLight.Dim;
            }
        }

        static int Mod(long value, int length)
        {
            long m = value % length;
            return (int)(m < 0 ? m + length : m);
        }

        static uint Next(uint x)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }
    }
}
