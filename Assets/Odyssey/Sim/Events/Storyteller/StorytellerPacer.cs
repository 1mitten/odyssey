#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// What the pacer asks of the world when a generator comes due (design 68 §3a): fire one incident
    /// of this category, if anything in it can fire. The live storyteller answers from the incident
    /// gates and the ledger; the tuning harness answers from a table. <b>A category with nothing
    /// fireable loses its roll</b> — it is never passed on — so adding an incident to one category
    /// cannot change how often another fires.
    /// </summary>
    public interface IStoryOracle
    {
        /// <summary>
        /// Fire one incident of <paramref name="category"/>, or say nothing could. <b>It must not
        /// draw from the pacer's stream</b>: the pacer's draws are the same whatever the world
        /// answers, so what one category holds cannot move when another fires.
        /// </summary>
        bool TryFire(IncidentCategory category, bool excludeBad, int budgetPerMille);
    }

    /// <summary>
    /// The storyteller's clock (design 68 §3): when each generator is next due, and each cycle's
    /// planned fires. <b>Pure</b>: it touches no world, so the live system and the fast-tier
    /// harness drive the same code, and the thing tuned is the thing shipped.
    ///
    /// <para><b>Plans are drawn ahead and saved</b>, not rolled every hour: a cycle draws its whole
    /// on-phase at the phase's start, a stream draws its next roll when it rolls. So a save
    /// mid-phase resumes onto the same plan, and the cost is the number of generators, not the
    /// board. Every time is in ticks.</para>
    ///
    /// <para><b>Grace is for big threats only</b>: a cycle of big threats first opens at the end of
    /// grace, and a bag's big-threat roll before it loses its roll. The good stream runs from the
    /// start.</para>
    /// </summary>
    public sealed class StorytellerPacer
    {
        /// <summary>At most this many fires planned in one on-phase: enough for any sane Def.</summary>
        public const int MaxPlanned = 8;

        int[] _next = Array.Empty<int>();
        int[] _phaseEnd = Array.Empty<int>();
        int[][] _planned = Array.Empty<int[]>();

        /// <summary>The tick of the last big threat any bag fired, or the end of grace: the drought counts from it.</summary>
        public int LastBigTick { get; private set; }

        public int GeneratorCount => _next.Length;

        /// <summary>When generator <paramref name="g"/> is next due (a cycle: its next on-phase).</summary>
        public int NextTick(int g) => _next[g];

        /// <summary>Generator <paramref name="g"/>'s next planned fire in its current on-phase, or -1.</summary>
        public int NextPlanned(int g)
        {
            int best = -1;
            foreach (int p in _planned[g])
                if (p >= 0 && (best < 0 || p < best)) best = p;
            return best;
        }

        /// <summary>
        /// Start every generator from <paramref name="tick"/>: a cycle of big threats opens its first
        /// phase at <paramref name="graceEnd"/>, a stream rolls its first interval. Called when a
        /// storyteller is chosen, and again when it is changed, so a change keeps nothing of the old
        /// clock.
        /// </summary>
        public void Arm(StorytellerDef def, int tick, int graceEnd, ref DeterministicRandom rng)
        {
            int n = def.generators.Count;
            _next = new int[n];
            _phaseEnd = new int[n];
            _planned = new int[n][];
            for (int g = 0; g < n; g++)
            {
                GeneratorDef gen = def.generators[g];
                _planned[g] = new int[MaxPlanned];
                Array.Fill(_planned[g], -1);
                _phaseEnd[g] = -1;
                _next[g] = gen.kind == GeneratorKind.OnOffCycle
                    ? (gen.category == IncidentCategory.ThreatBig ? Math.Max(tick, graceEnd) : tick)
                    : tick + Interval(gen.meanHours, ref rng);
            }
            LastBigTick = Math.Max(tick, graceEnd);
        }

        /// <summary>
        /// One hourly check: every generator that has come due asks <paramref name="oracle"/> to fire.
        /// Returns how many fired.
        /// </summary>
        public int Step(StorytellerDef def, int tick, int graceEnd, bool bigAllowed,
            ref DeterministicRandom rng, IStoryOracle oracle)
        {
            int fired = 0;
            for (int g = 0; g < _next.Length && g < def.generators.Count; g++)
            {
                GeneratorDef gen = def.generators[g];
                switch (gen.kind)
                {
                    case GeneratorKind.OnOffCycle:
                        fired += StepCycle(g, gen, tick, bigAllowed, ref rng, oracle);
                        break;
                    case GeneratorKind.MeanTimeBetween:
                        if (tick < _next[g]) break;
                        _next[g] = tick + Interval(gen.meanHours, ref rng);
                        if (gen.category == IncidentCategory.ThreatBig && (tick < graceEnd || !bigAllowed)) break;
                        if (oracle.TryFire(gen.category, gen.excludeBad, 1000))
                        {
                            fired++;
                            if (gen.category == IncidentCategory.ThreatBig) LastBigTick = tick;
                        }
                        break;
                    case GeneratorKind.RandomBag:
                        if (tick < _next[g]) break;
                        _next[g] = tick + Interval(gen.meanHours, ref rng);
                        fired += StepBag(gen, tick, graceEnd, bigAllowed, ref rng, oracle);
                        break;
                }
            }
            return fired;
        }

        int StepCycle(int g, GeneratorDef gen, int tick, bool bigAllowed, ref DeterministicRandom rng, IStoryOracle oracle)
        {
            if (tick >= _next[g]) Plan(g, gen, _next[g], ref rng);

            bool big = gen.category == IncidentCategory.ThreatBig;
            int fired = 0;
            int[] planned = _planned[g];
            for (int i = 0; i < planned.Length; i++)
            {
                if (planned[i] < 0 || planned[i] > tick) continue;
                // A fire that finds nothing fireable waits an hour at a time until its phase ends.
                if (tick >= _phaseEnd[g] || (big && !bigAllowed))
                {
                    planned[i] = -1;
                    continue;
                }
                if (oracle.TryFire(gen.category, false, 1000))
                {
                    planned[i] = -1;
                    fired++;
                    if (big) LastBigTick = tick;
                }
            }
            return fired;
        }

        /// <summary>
        /// Draw an on-phase's fires: how many, then when, each at least the minimum spacing after the
        /// last. Draw sorted offsets in the room left after the spacing, then add the spacing back,
        /// so every plan fits the phase and none is rejected and redrawn.
        /// </summary>
        void Plan(int g, GeneratorDef gen, int phaseStart, ref DeterministicRandom rng)
        {
            int span = gen.onDays * Calendar.TicksPerDay;
            int spacing = gen.minSpacingHours * Calendar.TicksPerHour;
            int count = Math.Min(MaxPlanned, rng.NextInt(gen.firesMin, gen.firesMax + 1));
            int room = Math.Max(1, span - (count - 1) * spacing);

            int[] planned = _planned[g];
            Array.Fill(planned, -1);
            for (int i = 0; i < count; i++) planned[i] = rng.NextInt(room);
            Array.Sort(planned, 0, count);
            for (int i = 0; i < count; i++) planned[i] = phaseStart + planned[i] + i * spacing;

            _phaseEnd[g] = phaseStart + span;
            _next[g] = phaseStart + (gen.onDays + gen.offDays) * Calendar.TicksPerDay;
        }

        int StepBag(GeneratorDef gen, int tick, int graceEnd, bool bigAllowed, ref DeterministicRandom rng, IStoryOracle oracle)
        {
            IncidentCategory category;
            bool drought = gen.droughtDays > 0 && tick >= graceEnd
                           && tick - LastBigTick >= gen.droughtDays * Calendar.TicksPerDay;
            if (drought) category = IncidentCategory.ThreatBig;
            else
            {
                int total = 0;
                foreach (BagWeight w in gen.weights) total += w.weight;
                int pick = rng.NextInt(total);
                category = gen.weights[gen.weights.Count - 1].category;
                foreach (BagWeight w in gen.weights)
                {
                    if (pick < w.weight) { category = w.category; break; }
                    pick -= w.weight;
                }
            }

            bool big = category == IncidentCategory.ThreatBig;
            if (big && (tick < graceEnd || !bigAllowed)) return 0;

            int budget = gen.budgetMinPerMille == gen.budgetMaxPerMille
                ? gen.budgetMinPerMille
                : rng.NextInt(gen.budgetMinPerMille, gen.budgetMaxPerMille + 1);
            if (!oracle.TryFire(category, false, budget)) return 0;
            if (big) LastBigTick = tick;
            return 1;
        }

        /// <summary>A stream's next interval: uniform from half to one and a half of the mean, in whole hours.</summary>
        static int Interval(int meanHours, ref DeterministicRandom rng)
        {
            int lo = Math.Max(1, meanHours / 2);
            int hi = Math.Max(lo + 1, meanHours + meanHours / 2 + 1);
            return rng.NextInt(lo, hi) * Calendar.TicksPerHour;
        }

        // ------------------------------------------------------------------ hash and save

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(LastBigTick);
            hash.Add(_next.Length);
            for (int g = 0; g < _next.Length; g++)
            {
                hash.Add(_next[g]);
                hash.Add(_phaseEnd[g]);
                foreach (int p in _planned[g]) hash.Add(p);
            }
        }

        public void Save(SaveWriter writer)
        {
            writer.Write(LastBigTick);
            writer.Write(_next.Length);
            for (int g = 0; g < _next.Length; g++)
            {
                writer.Write(_next[g]);
                writer.Write(_phaseEnd[g]);
                writer.Write(_planned[g].Length);
                foreach (int p in _planned[g]) writer.Write(p);
            }
        }

        public void Load(SaveReader reader)
        {
            LastBigTick = reader.ReadInt();
            int n = reader.ReadInt();
            if (n < 0 || n > 64) throw new SaveLoadException($"odyssey.storyteller: {n} generators is not a count this build reads.");
            _next = new int[n];
            _phaseEnd = new int[n];
            _planned = new int[n][];
            for (int g = 0; g < n; g++)
            {
                _next[g] = reader.ReadInt();
                _phaseEnd[g] = reader.ReadInt();
                int m = reader.ReadInt();
                if (m < 0 || m > 64) throw new SaveLoadException($"odyssey.storyteller: {m} planned fires is not a count this build reads.");
                _planned[g] = new int[Math.Max(m, MaxPlanned)];
                Array.Fill(_planned[g], -1);
                for (int i = 0; i < m; i++) _planned[g][i] = reader.ReadInt();
            }
        }
    }
}
