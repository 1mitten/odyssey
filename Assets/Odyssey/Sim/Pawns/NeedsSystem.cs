#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Needs, thoughts, mood and the one mental break, on the 150-tick cadence.
    ///
    /// Three properties are worth stating because each is a decision rather than an accident.
    ///
    /// <para><b>The cadence is 150 ticks, not every tick.</b> Four hundred updates per in-game day
    /// is fine enough that no player will see the quantisation, and it makes the needs subsystem
    /// cost proportional to the pawn count divided by 150. Pawns are spread across the interval by
    /// id, so a hundred colonists cost two thirds of a pawn per tick rather than a hundred pawns
    /// every hundred and fiftieth tick.</para>
    ///
    /// <para><b>Rates are per band.</b> A pawn at 20% rest does not drain at the rate of one at
    /// 80%. Flat rates are what make the bottom of a bar feel wrong.</para>
    ///
    /// <para><b>Mood drifts.</b> The target is a difficulty base plus the sum of active thought
    /// offsets, and the displayed mood approaches it at a capped rate. Snapping to the target
    /// turns one bad moment into an instant break, which is both worse drama and worse design.
    /// A break is then a mean-time-between-events roll below the threshold rather than a cliff
    /// edge, so a miserable colonist breaks <em>probably soon</em>.</para>
    /// </summary>
    public sealed class NeedsSystem : IWorldSystem
    {
        readonly PawnContext _ctx;

        public NeedsSystem(PawnContext ctx) { _ctx = ctx; }

        public string Name => "Needs";

        public TickPhase Phase => TickPhase.Pawns;

        /// <summary>First in the phase: the think tree reads needs, so they settle before it runs.</summary>
        public int Order => 10;

        public int IntervalTicks => _ctx.Content.NeedsIntervalTicks;

        /// <summary>Breaks rolled since the world began. A cheap sanity check for a long run.</summary>
        public int BreaksTriggered { get; private set; }

        public void Tick(SimWorld world)
        {
            _ctx.Sync(world);
            int interval = IntervalTicks;
            int tick = world.CurrentTick;

            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];

                // Phase spreading by id. Over any window of exactly `interval` ticks each pawn
                // updates exactly once, which is what keeps the cadence exact and testable while
                // the cost stays flat.
                if ((tick + pawn.Id.Value) % interval != 0) continue;
                UpdatePawn(pawn, tick, interval);
            }
        }

        void UpdatePawn(Pawn pawn, int tick, int interval)
        {
            var content = _ctx.Content;

            // ---- food: always falls, even asleep -------------------------------------------
            Fall(pawn, NeedIndex.Food);

            // Starvation severity: the bar that fills while the pantry is empty and drains while
            // it is not, by the same number (WS3, design 17 §4c). Recovery being symmetric is
            // one of the three brakes on the starvation spiral — no point of no return — and it
            // is why this is an offset to condition rather than a second hunger bar: the food
            // need refills in a meal, the bar takes days, and the gap between those two speeds
            // is the difference between having missed lunch and having been starving.
            if (pawn.Needs[NeedIndex.Food] <= 0)
                pawn.StarvationSeverity = System.Math.Min(
                    1_000, pawn.StarvationSeverity + content.Kind.starvationPerInterval);
            else if (pawn.StarvationSeverity > 0)
                pawn.StarvationSeverity = System.Math.Max(
                    0, pawn.StarvationSeverity - content.Kind.starvationPerInterval);

            // ---- rest: falls awake, recovers asleep, scaled by what is under the pawn -------
            if (pawn.Asleep)
            {
                var rest = content.Needs[NeedIndex.Rest];
                int effectiveness = RestEffectiveness(pawn.Cell);

                // The interval this pawn is on, which is what spends the fractional part of the
                // gain — see Pawn.RestGainPerInterval for why a tier is worth nothing without it.
                // Derived from the tick and the id, exactly as the phase spreading above is, so it
                // is a pure function of state the world already keeps.
                int intervalIndex = (tick + pawn.Id.Value) / interval;
                pawn.Needs[NeedIndex.Rest] = System.Math.Min(
                    rest.max,
                    pawn.Needs[NeedIndex.Rest] + pawn.RestGainPerInterval(effectiveness, intervalIndex));
            }
            else
            {
                Fall(pawn, NeedIndex.Rest);
            }

            // ---- joy: paused asleep, topped up while idle ----------------------------------
            //
            // The slice has no recreation buildings, so idling is the only source of joy there
            // is. Without one, joy falls to zero on every colonist, mood sits under the break
            // threshold permanently, and a ten-day run measures nothing but mental breaks.
            if (!pawn.Asleep)
            {
                if (IsIdling(pawn))
                {
                    var joy = content.Needs[NeedIndex.Joy];
                    pawn.Needs[NeedIndex.Joy] =
                        System.Math.Min(joy.max, pawn.Needs[NeedIndex.Joy] + content.Kind.joyGainPerInterval);
                }
                else
                {
                    Fall(pawn, NeedIndex.Joy);
                }
            }

            UpdateMood(pawn, tick);
            RollMentalBreak(pawn, tick, interval);
        }

        void Fall(Pawn pawn, int needIndex)
        {
            int value = pawn.Needs[needIndex] - pawn.NeedFallPerInterval(needIndex);
            pawn.Needs[needIndex] = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Target = base + situational band offsets + memory offsets, then drift toward it.
        /// Situational thoughts are recomputed from the world and never stored; memories are
        /// stored and expire.
        /// </summary>
        void UpdateMood(Pawn pawn, int tick)
        {
            var mood = _ctx.Content.Mood;
            pawn.ExpireMemories(tick);

            int target = mood.baseMood;
            for (int n = 0; n < NeedIndex.Count; n++)
                target += _ctx.Content.Needs[n].MoodOffset(pawn.Needs[n]);
            target += pawn.MemoryMoodOffset(tick);

            if (target < 0) target = 0;
            if (target > mood.max) target = mood.max;
            pawn.MoodTarget = target;

            if (pawn.Mood < target)
                pawn.Mood = System.Math.Min(target, pawn.Mood + pawn.MoodDriftPerInterval(true));
            else if (pawn.Mood > target)
                pawn.Mood = System.Math.Max(target, pawn.Mood - pawn.MoodDriftPerInterval(false));
        }

        /// <summary>
        /// A mean-time-between-events draw, not a trigger. The per-check probability is
        /// interval/MTB, expressed as an integer comparison so no float ever enters the
        /// simulation: draw uniformly below the MTB and break if the draw lands inside one
        /// interval's worth of it.
        /// </summary>
        void RollMentalBreak(Pawn pawn, int tick, int interval)
        {
            if (!pawn.CanMentalBreak()) return;

            var mood = _ctx.Content.Mood;
            var rng = DeterministicRandom.ForTick(
                _ctx.Seed, tick, PawnPurpose.MentalBreak ^ (uint)pawn.Id.Value);
            if (rng.NextInt(mood.breakMtbTicks) >= interval) return;

            pawn.BreakTicksLeft = _ctx.Content.Break.durationTicks;
            BreaksTriggered++;
        }

        bool IsBed(int cell)
        {
            var beds = _ctx.Items.Beds;
            int low = 0, high = beds.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int value = beds[mid];
                if (value == cell) return true;
                if (value < cell) low = mid + 1;
                else high = mid - 1;
            }
            return false;
        }

        /// <summary>
        /// How well a sleeping pawn recovers where it lies, in per cent: the ground at the
        /// kind's own rate, a scenario's bed spot at a plain 100 as it always has, and a built
        /// bed at the tier its finisher rolled (design 20 §7). The quality table is the single
        /// source of the numbers — the hardcoded 100 the pane of glass used to carry is the one
        /// thing this replaced.
        /// </summary>
        int RestEffectiveness(int cell)
        {
            if (!IsBed(cell)) return _ctx.Content.Kind.groundRestEffectiveness;

            // No construction grid, no built beds: a rig that never wired one still has its
            // scenario spots, and they are plain.
            byte tier = _ctx.Construction != null ? _ctx.Construction.BedQualityAt(cell) : (byte)0;
            return tier == 0 ? 100 : Construction.QualityContent.RestEffectiveness(tier);
        }

        static bool IsIdling(Pawn pawn)
        {
            var job = pawn.CurrentJob;
            if (job == null) return true;
            int driver = pawn.Content.Jobs[job.DefIndex].driver;
            return driver == JobIndex.Wander || driver == JobIndex.Wait;
        }
    }
}
