#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Events
{
    /// <summary>The storyteller's own random streams (design 68 §8), apart from every other system's.</summary>
    public static class StorytellerPurpose
    {
        /// <summary>The hourly check: every generator's draws and every pick. SHA-256's 29th round constant.</summary>
        public const uint Pace = 0xC6E0_0BF3;

        /// <summary>Arming the pacer when a storyteller is chosen or changed. The 30th.</summary>
        public const uint Arm = 0xD5A7_9147;

        /// <summary>Which incident of a category is fired, mixed with the category. The 31st.</summary>
        public const uint Pick = 0x06CA_6351;
    }

    /// <summary>
    /// The storyteller (design 68): the colony's pacer, its choice of storyteller and difficulty,
    /// its tension and the strength it remembers. One system, checked once a game hour.
    ///
    /// <para><b>Nothing while none is chosen.</b> A world with no storyteller — every golden, every
    /// soak, every save from before this — ticks nothing, publishes <see cref="StorytellerView.None"/>
    /// and hashes nothing, so no golden can move. A new game chooses one by intent on its first
    /// tick; an old save is given one in Settings.</para>
    ///
    /// <para><b>It fires through the one door</b>, <see cref="Incidents.TryFire"/>, with the points
    /// left at 0: a storyteller's raid is sized exactly as a debug <i>Auto</i> raid is, so the two
    /// cannot drift (design 23 §3). What it adds to the size — the difficulty's scale, the tension
    /// and the bag's draw — is read by the raid through <see cref="RaidScalePerMille"/>.</para>
    ///
    /// <para><b>Order 90</b>, in the world phase, after every other world system: nothing reads it.</para>
    /// </summary>
    public sealed class Storyteller : IWorldSystem, IStateHashable, ISaveable, ISnapshotContributor, IStoryOracle
    {
        const int SectionVersion = 1;

        public const int TensionMin = 400, TensionMax = 1500, TensionStart = 1000;

        readonly PawnContext _ctx;
        readonly StorytellerContent _content;
        readonly StorytellerPacer _pacer = new StorytellerPacer();

        int _storyteller = StorytellerHandle.None;
        int _rung = 3;
        int _threatPercent = 100;
        bool _bigThreats = true;
        int _adaptationPercent = 100;
        int _graceHundredths = 100;

        /// <summary>The tick the colony's first storyteller was chosen: the grace counts from it.</summary>
        int _startTick = -1;
        int _graceEnd;

        int _tension = TensionStart;
        TensionCauseKind _cause = TensionCauseKind.None;
        int _causeTick;

        /// <summary>The strength the raid budget reads: the highest recent, decaying (design 68 §4c).</summary>
        int _strengthPeak;

        /// <summary>A bag's draw for the fire in hand, read by the raid it sizes; 1000 otherwise.</summary>
        int _fireBudget = 1000;

        public Storyteller(PawnContext ctx, StorytellerContent content)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public string Name => "Storyteller";
        public TickPhase Phase => TickPhase.WorldSystems;
        public int Order => 90;

        public StorytellerContent Content => _content;
        public StorytellerPacer Pacer => _pacer;

        public int Index => _storyteller;
        public bool HasStoryteller => _storyteller >= 0;
        public int StartTick => _startTick;
        public int GraceEndTick => _graceEnd;
        public int TensionPerMille => _tension;
        public TensionCauseKind Cause => _cause;
        public int CauseTick => _causeTick;
        public int StrengthPeak => _strengthPeak;
        public int ThreatPercent => _threatPercent;
        public bool BigThreats => _bigThreats;
        public int AdaptationPercent => _adaptationPercent;
        public int GraceHundredths => _graceHundredths;

        public void Tick(SimWorld world)
        {
            if (_storyteller < 0) return;
            int tick = world.CurrentTick;
            if (tick % Calendar.TicksPerHour != 0) return;
            _ctx.Sync(world);

            OnHour(tick);

            StorytellerDef def = _content.Defs[_storyteller];
            DeterministicRandom rng = world.RandomForTick(StorytellerPurpose.Pace);
            _pacer.Step(def, tick, _graceEnd, _bigThreats, ref rng, this);
        }

        /// <summary>The hourly bookkeeping beside the pacer: the remembered strength, and at each day's
        /// turn the tension's recovery. Filled in by the strength and tension units.</summary>
        void OnHour(int tick)
        {
            UpdateStrengthPeak();
            if (tick % Calendar.TicksPerDay == 0 && tick > _startTick) OnDay(tick);
        }

        // ------------------------------------------------------------------ firing

        /// <summary>
        /// The pacer's question, answered from the incidents (design 68 §3a): every incident of the
        /// category that is fireable, passes its gates and can fire now, picked by weight; then fired
        /// through the one door. Nothing fireable loses the roll.
        /// </summary>
        public bool TryFire(IncidentCategory category, bool excludeBad, int budgetPerMille)
        {
            Incidents? incidents = _ctx.Incidents;
            if (incidents == null) return false;
            IncidentContent content = incidents.Content;
            int tick = _ctx.World?.CurrentTick ?? _ctx.CurrentTick;
            int colonists = RaidBudget.StandingColonists(_ctx);

            int total = 0;
            Span<int> weights = stackalloc int[content.Count];
            for (int i = 0; i < content.Count; i++)
            {
                weights[i] = 0;
                IncidentDef def = content.Defs[i];
                if (def.category != category) continue;
                if (!content.Workers[i].Fireable) continue;
                if (excludeBad && def.favourability == IncidentFavourability.Bad) continue;
                if (!GatesPass(def, i, incidents.Ledger, tick, colonists)) continue;
                if (!incidents.CanFire(new IncidentParms(i))) continue;
                int w = def.weight;
                if (def.populationGain) w = (int)((long)w * PopulationIntent(colonists) / 1000);
                if (w <= 0) continue;
                weights[i] = w;
                total += w;
            }
            if (total <= 0) return false;

            // Its own stream, per category, so the pacer's draws never depend on what the world held.
            DeterministicRandom rng = DeterministicRandom.ForTick(_ctx.Seed, tick, StorytellerPurpose.Pick ^ (uint)category);
            int pick = rng.NextInt(total);
            int chosen = -1;
            for (int i = 0; i < content.Count; i++)
            {
                if (weights[i] == 0) continue;
                if (pick < weights[i]) { chosen = i; break; }
                pick -= weights[i];
            }
            if (chosen < 0) return false;

            _fireBudget = budgetPerMille;
            try
            {
                return incidents.TryFire(new IncidentParms(chosen));
            }
            finally
            {
                _fireBudget = 1000;
            }
        }

        /// <summary>The gates design 23 wrote and nothing read until now.</summary>
        public static bool GatesPass(IncidentDef def, int index, IncidentLedger ledger, int tick, int colonists)
        {
            if (tick / Calendar.TicksPerDay < def.earliestDay) return false;
            int last = ledger.LastFiredTick(index);
            if (last >= 0 && tick - last < def.minRefireDays * Calendar.TicksPerDay) return false;
            if (colonists < def.minColonists) return false;
            if (def.maxFires > 0 && ledger.Fires(index) >= def.maxFires) return false;
            return true;
        }

        /// <summary>Population intent (design 68 §3c): the storyteller's curve at this headcount.</summary>
        public int PopulationIntent(int colonists)
        {
            if (_storyteller < 0) return 1000;
            int[] curve = _content.Defs[_storyteller].populationCurve;
            if (curve.Length == 0) return 1000;
            return curve[Math.Min(Math.Max(colonists, 0), curve.Length - 1)];
        }

        /// <summary>
        /// What a raid fired now is scaled by, per mille (design 68 §4b): the difficulty's threat
        /// scale, the tension, and a bag's draw. 1000 with no storyteller, so a debug raid on a world
        /// without one is sized by strength alone.
        /// </summary>
        public int RaidScalePerMille =>
            _storyteller < 0
                ? 1000
                : (int)((long)_threatPercent * 10 * _tension / 1000 * _fireBudget / 1000);

        // ------------------------------------------------------------------ the choice

        /// <summary><see cref="IntentKind.SetStoryteller"/>: choose or change the storyteller.</summary>
        public IntentRejection HandleSetStoryteller(Intent intent)
        {
            int index = intent.A;
            if (index < 0 || index >= _content.Count) return IntentRejection.OutOfBounds;
            if (index == _storyteller) return IntentRejection.AlreadyInThatState;
            int tick = _ctx.World?.CurrentTick ?? _ctx.CurrentTick;
            bool first = _startTick < 0;
            if (first)
            {
                _startTick = tick;
                _strengthPeak = CurrentStrength();
            }
            _storyteller = index;
            ReArm(tick);
            return IntentRejection.None;
        }

        /// <summary><see cref="IntentKind.SetDifficulty"/>: the four levers, as the interface's rung set them.</summary>
        public IntentRejection HandleSetDifficulty(Intent intent)
        {
            int rung = intent.A;
            int threat = intent.B & 0xFFFF, adaptation = (intent.B >> 16) & 0xFFFF;
            int grace = intent.C & 0xFFFF;
            bool big = ((intent.C >> 16) & 1) != 0;
            if (rung < 0 || rung > 15 || threat > 500 || adaptation > 200 || grace < 50 || grace > 200)
                return IntentRejection.OutOfBounds;
            if (rung == _rung && threat == _threatPercent && adaptation == _adaptationPercent
                && grace == _graceHundredths && big == _bigThreats)
                return IntentRejection.AlreadyInThatState;

            _rung = rung;
            _threatPercent = threat;
            _adaptationPercent = adaptation;
            _bigThreats = big;
            bool graceMoved = grace != _graceHundredths;
            _graceHundredths = grace;

            // A new stretch moves the first big threat only while it is still to come; once the
            // grace has passed it is not given again (design 68 §7).
            int tick = _ctx.World?.CurrentTick ?? _ctx.CurrentTick;
            if (graceMoved && _storyteller >= 0 && tick < _graceEnd) ReArm(tick);
            return IntentRejection.None;
        }

        void ReArm(int tick)
        {
            StorytellerDef def = _content.Defs[_storyteller];
            _graceEnd = _startTick + (int)((long)def.graceDays * Calendar.TicksPerDay * _graceHundredths / 100);
            DeterministicRandom rng = DeterministicRandom.ForTick(_ctx.Seed, tick, StorytellerPurpose.Arm);
            _pacer.Arm(def, tick, _graceEnd, ref rng);
        }

        // ------------------------------------------------------------------ strength (ST2)

        /// <summary>
        /// How fast the remembered strength lets go, per mille an hour: about a tenth a day, so
        /// stowing the guns before a raid buys nothing for days and a real loss still shows within
        /// a week (design 68 §4c). INVENTED.
        /// </summary>
        public const int PeakDecayPerMillePerHour = 4;

        void UpdateStrengthPeak()
        {
            int now = CurrentStrength();
            int decayed = _strengthPeak - (int)((long)_strengthPeak * PeakDecayPerMillePerHour / 1000);
            _strengthPeak = Math.Max(now, decayed);
        }

        int CurrentStrength() => ColonyStrength.Of(_ctx);

        // ------------------------------------------------------------------ tension (ST3)

        /// <summary>What a colonist's death and a colonist downed take off the tension, at Normal
        /// and in a colony of five or fewer (design 68 §5). INVENTED.</summary>
        public const int DeathDrop = 250, DownDrop = 60;

        /// <summary>What a quiet day gives back, below and at or above the starting 1000.</summary>
        public const int QuietBelow = 25, QuietAbove = 10;

        /// <summary>
        /// A colonist died or was downed (design 68 §5): the tension drops, by less in a bigger colony
        /// and by the difficulty's adaptation strength, and the cause is written down for the gauge's
        /// tooltip. <b>Colonists only</b> — a raider going down eases nothing. Called by
        /// <see cref="StorytellerCombatListener"/> inside the fight's tick; it writes this system's
        /// own numbers and touches no pawn.
        /// </summary>
        public void NoteLoss(Pawn pawn, bool died, int tick)
        {
            if (_storyteller < 0 || !pawn.IsColonist) return;
            int colonists = 0;
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].IsColonist) colonists++;
            colonists = Math.Max(1, colonists);

            long drop = died ? DeathDrop : DownDrop;
            drop = drop * Math.Min(1000, 5000 / colonists) / 1000;
            drop = drop * _adaptationPercent / 100;
            _tension = Math.Max(TensionMin, _tension - (int)drop);
            _cause = died ? TensionCauseKind.Died : TensionCauseKind.Downed;
            _causeTick = tick;
        }

        /// <summary>
        /// A day has turned: if nobody was lost in it, the tension climbs back, faster from below
        /// the start than above it, by the difficulty's adaptation strength; and the cause becomes a
        /// run of quiet days.
        /// </summary>
        void OnDay(int tick)
        {
            bool lossToday = (_cause == TensionCauseKind.Died || _cause == TensionCauseKind.Downed)
                             && tick - _causeTick < Calendar.TicksPerDay;
            if (lossToday) return;
            int gain = (_tension < TensionStart ? QuietBelow : QuietAbove) * _adaptationPercent / 100;
            _tension = Math.Min(TensionMax, _tension + gain);
            if (_cause != TensionCauseKind.Quiet)
            {
                _cause = TensionCauseKind.Quiet;
                _causeTick = tick - Calendar.TicksPerDay;
            }
        }

        /// <summary>The tension's band (design 68 §5a).</summary>
        public static int BandOf(int tension) =>
            tension < 600 ? 0 : tension < 850 ? 1 : tension < 1100 ? 2 : tension < 1300 ? 3 : 4;

        // ------------------------------------------------------------------ view, hash, save

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            if (_storyteller < 0) return;
            int tick = world.CurrentTick;
            int days = _cause == TensionCauseKind.None ? 0 : Math.Max(0, (tick - _causeTick) / Calendar.TicksPerDay);
            writer.SetStoryteller(new StorytellerView(_storyteller, _rung, _threatPercent, _bigThreats,
                _adaptationPercent, _graceHundredths, BandOf(_tension), _cause, days));
        }

        public void ContributeTo(ref StateHash hash)
        {
            if (_storyteller < 0) return;
            hash.Add(_storyteller);
            hash.Add(_rung);
            hash.Add(_threatPercent);
            hash.Add(_bigThreats);
            hash.Add(_adaptationPercent);
            hash.Add(_graceHundredths);
            hash.Add(_startTick);
            hash.Add(_graceEnd);
            hash.Add(_tension);
            hash.Add((int)_cause);
            hash.Add(_causeTick);
            hash.Add(_strengthPeak);
            _pacer.ContributeTo(ref hash);
        }

        public string SaveKey => "odyssey.storyteller";

        public void Save(SaveWriter writer)
        {
            writer.Write(SectionVersion);
            writer.Write(_storyteller);
            writer.Write(_rung);
            writer.Write(_threatPercent);
            writer.Write(_bigThreats);
            writer.Write(_adaptationPercent);
            writer.Write(_graceHundredths);
            writer.Write(_startTick);
            writer.Write(_graceEnd);
            writer.Write(_tension);
            writer.Write((int)_cause);
            writer.Write(_causeTick);
            writer.Write(_strengthPeak);
            _pacer.Save(writer);
        }

        /// <summary>A save from before the storyteller has no section and loads with none (design 68 §7).</summary>
        public void Load(SaveReader reader)
        {
            int version = reader.ReadInt();
            if (version < 1 || version > SectionVersion)
                throw new SaveLoadException($"odyssey.storyteller section version {version} is not one this build reads.");
            int index = reader.ReadInt();
            _storyteller = index >= 0 && index < _content.Count ? index : StorytellerHandle.None;
            _rung = reader.ReadInt();
            _threatPercent = reader.ReadInt();
            _bigThreats = reader.ReadBool();
            _adaptationPercent = reader.ReadInt();
            _graceHundredths = reader.ReadInt();
            _startTick = reader.ReadInt();
            _graceEnd = reader.ReadInt();
            _tension = reader.ReadInt();
            _cause = (TensionCauseKind)reader.ReadInt();
            _causeTick = reader.ReadInt();
            _strengthPeak = reader.ReadInt();
            _pacer.Load(reader);
        }
    }
}
