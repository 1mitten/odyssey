#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Weather
{
    /// <summary>The weather's own random streams, apart from every other system's (design 43 §3).</summary>
    public static class WeatherPurpose
    {
        /// <summary>Which kind the next spell is, how hard, and how long. SHA-256 round constants, like the incidents'.</summary>
        public const uint Roll = 0x9BDC_06A7;

        /// <summary>A forced spell's length and intensity (the debug menu).</summary>
        public const uint Force = 0xC19B_F174;
    }

    /// <summary>
    /// The sky, map-wide (design 43): one kind at a time, rolled from the season's weights when a
    /// spell ends, blended into the next over two game hours, and written into the outdoor
    /// temperature through the seam temperature left for it (<c>WeatherOffsetC</c>, design 28 §10).
    ///
    /// <para><b>Never per cell.</b> The same call temperature made with its per-room scalars: the
    /// whole state is a handful of integers, and the pass is O(1). Shelter from it is the cover
    /// map's question, not this system's (design 43 §6, the next build step).</para>
    ///
    /// <para><b>One writer for the offset.</b> Only this system writes <c>WeatherOffsetC</c>, each
    /// pass. The debug menu does not write the sky either: it commands this system
    /// (<see cref="IntentKind.DebugSetWeather"/>), which is the rule the cold-snap incident will
    /// follow when there is a storyteller to fire it (design 43 §5).</para>
    ///
    /// <para><b>Order 35</b>, after the enclosure (30) and before growth (40), power (45) and
    /// temperature (50), so every reader sees this pass's sky (design 43 §5, corrected on review).
    /// Doors share 35 and ties break on the name, so the doors run first; nothing here reads them.</para>
    /// </summary>
    public sealed class WeatherSystem : IWorldSystem, IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>The pass cadence, the thermal pass's, so the offset is current when it reads it.</summary>
        public const int IntervalTicks = 120;

        /// <summary>How long one spell takes to hand over to the next: two game hours (design 43 §4).</summary>
        public const int BlendTicks = 2 * Calendar.TicksPerHour;

        /// <summary>A forced spell's hand-over, for the debug menu: a few seconds at speed 1.</summary>
        public const int QuickBlendTicks = 300;

        const int SectionVersion = 1;
        const int SeasonCount = 3;

        readonly PawnContext _ctx;
        readonly WeatherDef[] _defs;

        bool _started;
        int _kind, _intensity;
        int _previousKind, _previousIntensity;
        int _blendStart, _blendTicks = BlendTicks;
        int _spellEnd;

        public WeatherSystem(PawnContext ctx, WeatherDef[] defs)
        {
            if (defs.Length == 0) throw new ArgumentException("the weather table is empty", nameof(defs));
            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i].kind != i)
                    throw new ArgumentException($"weather def {defs[i].defName} is kind {defs[i].kind} at index {i}", nameof(defs));
                if (defs[i].seasonWeights.Count != SeasonCount)
                    throw new ArgumentException($"weather def {defs[i].defName} has {defs[i].seasonWeights.Count} season weights, not {SeasonCount}", nameof(defs));
            }
            _ctx = ctx;
            _defs = defs;
        }

        public string Name => "Weather";
        public TickPhase Phase => TickPhase.WorldSystems;
        public int Order => 35;

        /// <summary>The kind now rolling in, and its intensity.</summary>
        public WeatherKind Kind => (WeatherKind)_kind;
        public int IntensityPerMille => _intensity;

        /// <summary>The tick the current spell ends and the next is rolled.</summary>
        public int SpellEndTick => _spellEnd;

        /// <summary>The kind a spell blending out, and its intensity.</summary>
        public WeatherKind PreviousKind => (WeatherKind)_previousKind;

        public void Tick(SimWorld world)
        {
            if (world.CurrentTick % IntervalTicks != 0) return;
            _ctx.Sync(world);
            int tick = world.CurrentTick;

            if (!_started) Begin(tick);
            else if (tick >= _spellEnd) Roll(tick, quick: false);

            if (_ctx.Temperature != null) _ctx.Temperature.WeatherOffsetC = ViewAt(tick).TempOffsetC;
        }

        /// <summary>
        /// The first sky, with no blend: a world does not load into a spell arriving. Rolled from
        /// the season the world starts in, like every spell after it.
        /// </summary>
        void Begin(int tick)
        {
            _started = true;
            Roll(tick, quick: false);
            _previousKind = _kind;
            _previousIntensity = _intensity;
            _blendStart = tick - _blendTicks;
        }

        /// <summary>Roll the next spell from the season's weights and start blending it in.</summary>
        void Roll(int tick, bool quick)
        {
            var rng = DeterministicRandom.ForTick(_ctx.Seed, tick, WeatherPurpose.Roll);
            int season = Calendar.SeasonOfYear(tick);
            int kind = PickKind(_defs, season, rng.NextInt(TotalWeight(_defs, season)));
            Start(tick, kind, 0, quick, ref rng);
        }

        void Start(int tick, int kind, int intensity, bool quick, ref DeterministicRandom rng)
        {
            WeatherDef def = _defs[kind];
            // Hand over from wherever the sky is now: the kind holding the larger share.
            WeatherView now = ViewAt(tick);
            _previousKind = (int)now.Kind;
            _previousIntensity = now.IntensityPerMille;

            _kind = kind;
            _intensity = intensity > 0 ? Math.Min(intensity, 1000)
                : rng.NextInt(def.minIntensity, def.maxIntensity + 1);
            int hours = rng.NextInt(def.minHours, def.maxHours + 1);
            _spellEnd = tick + Math.Max(1, hours) * Calendar.TicksPerHour;
            _blendStart = tick;
            _blendTicks = quick ? QuickBlendTicks : BlendTicks;
        }

        /// <summary>The season's total weight, per 10,000. A season that rolls nothing rolls clear.</summary>
        public static int TotalWeight(WeatherDef[] defs, int season)
        {
            int total = 0;
            for (int i = 0; i < defs.Length; i++) total += Math.Max(0, defs[i].seasonWeights[season]);
            return Math.Max(1, total);
        }

        /// <summary>Which kind a roll of <paramref name="draw"/> (0 up to the total) lands on. Pure, so it can be tested alone.</summary>
        public static int PickKind(WeatherDef[] defs, int season, int draw)
        {
            for (int i = 0; i < defs.Length; i++)
            {
                int w = Math.Max(0, defs[i].seasonWeights[season]);
                if (draw < w) return i;
                draw -= w;
            }
            return 0;
        }

        /// <summary>What one kind contributes at an intensity, before blending. Pure.</summary>
        public static WeatherView Terms(WeatherDef def, int intensity)
        {
            int i = Math.Max(0, Math.Min(1000, intensity));
            return new WeatherView((WeatherKind)def.kind, i,
                def.cloudPerMille * i / 1000,
                def.gloomPerMille * i / 1000,
                def.rainPerMille * i / 1000,
                1000 + (def.windPerMille - 1000) * i / 1000,
                def.tempOffsetC * i / 1000);
        }

        /// <summary>
        /// The sky at a tick: the outgoing spell and the incoming one in proportion to how far the
        /// hand-over has got, integer-linear, landing exactly on the incoming spell's terms.
        /// </summary>
        public WeatherView ViewAt(int tick)
        {
            if (!_started) return WeatherView.None;
            WeatherView from = Terms(_defs[_previousKind], _previousIntensity);
            WeatherView to = Terms(_defs[_kind], _intensity);
            int share = _blendTicks <= 0 ? 1000
                : Math.Max(0, Math.Min(1000, (int)((long)(tick - _blendStart) * 1000 / _blendTicks)));
            bool incoming = share >= 500;
            return new WeatherView(
                incoming ? to.Kind : from.Kind,
                incoming ? to.IntensityPerMille : from.IntensityPerMille,
                Mix(from.CloudPerMille, to.CloudPerMille, share),
                Mix(from.GloomPerMille, to.GloomPerMille, share),
                Mix(from.RainPerMille, to.RainPerMille, share),
                Mix(from.WindPerMille, to.WindPerMille, share),
                Mix(from.TempOffsetC, to.TempOffsetC, share));
        }

        static int Mix(int a, int b, int share) => a + (b - a) * share / 1000;

        /// <summary>
        /// The debug menu's Weather rows (design 43 §8): set the sky now. The spell runs its rolled
        /// length and the season takes over when it ends.
        /// </summary>
        public IntentRejection HandleForce(Intent intent)
        {
            if (intent.A < 0 || intent.A >= _defs.Length) return IntentRejection.OutOfBounds;
            int tick = _ctx.World?.CurrentTick ?? _ctx.CurrentTick;
            var rng = DeterministicRandom.ForTick(_ctx.Seed, tick, WeatherPurpose.Force);
            if (!_started) Begin(tick);
            Start(tick, intent.A, intent.B, intent.C == 1, ref rng);
            if (_ctx.Temperature != null) _ctx.Temperature.WeatherOffsetC = ViewAt(tick).TempOffsetC;
            return IntentRejection.None;
        }

        public void Contribute(SimWorld world, SnapshotWriter writer) => writer.SetWeather(ViewAt(world.CurrentTick));

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_started);
            hash.Add(_kind);
            hash.Add(_intensity);
            hash.Add(_previousKind);
            hash.Add(_previousIntensity);
            hash.Add(_blendStart);
            hash.Add(_blendTicks);
            hash.Add(_spellEnd);
        }

        public string SaveKey => "odyssey.weather";

        public void Save(SaveWriter writer)
        {
            writer.Write(SectionVersion);
            writer.Write(_started);
            writer.Write(_kind);
            writer.Write(_intensity);
            writer.Write(_previousKind);
            writer.Write(_previousIntensity);
            writer.Write(_blendStart);
            writer.Write(_blendTicks);
            writer.Write(_spellEnd);
        }

        /// <summary>
        /// A save from before weather has no section, and loads with nothing started: the first
        /// pass rolls a sky from the season the colony is in, which is what a new world does.
        /// </summary>
        public void Load(SaveReader reader)
        {
            int version = reader.ReadInt();
            if (version < 1 || version > SectionVersion)
                throw new InvalidOperationException($"odyssey.weather section version {version} is not one this build reads.");
            _started = reader.ReadBool();
            _kind = Clamp(reader.ReadInt());
            _intensity = reader.ReadInt();
            _previousKind = Clamp(reader.ReadInt());
            _previousIntensity = reader.ReadInt();
            _blendStart = reader.ReadInt();
            _blendTicks = reader.ReadInt();
            _spellEnd = reader.ReadInt();
        }

        int Clamp(int kind) => kind < 0 || kind >= _defs.Length ? 0 : kind;
    }
}
