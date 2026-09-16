#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>One memory thought on a pawn: which thought, and when it stops counting.</summary>
    public struct Memory
    {
        public int ThoughtIndex;
        public int ExpiryTick;
    }

    /// <summary>
    /// A colonist.
    ///
    /// Public, unsealed, and virtual at every decision point, per the code conventions in
    /// CLAUDE.md: the places a mod would want to intercept — how fast a need falls, whether a
    /// break is rolled, how fast the pawn walks, whether it may take a job — are virtual methods
    /// rather than inline arithmetic, so Harmony-style patching stays possible without this class
    /// being rewritten first.
    ///
    /// <para>Everything on it is an integer. Needs are 0..1000, mood is 0..1000, costs are in
    /// hundredths of a flat cell crossing. There is no float in the pawn simulation at all, which
    /// is what lets every field here fold into the state hash and the save file without a
    /// rounding question.</para>
    ///
    /// <para>Position is a cell <em>index</em>, not an (x, z, y) triple: the grid, the nav flags,
    /// the region table and the path buffer are all indexed the same way, so a pawn's position is
    /// already the key to every one of them.</para>
    /// </summary>
    public class Pawn
    {
        public Pawn(PawnId id, int cell, PawnContent content)
        {
            Id = id;
            Cell = cell;
            Content = content;
            Needs = new int[NeedIndex.Count];
            for (int i = 0; i < NeedIndex.Count && i < content.Kind.startingNeeds.Length; i++)
                Needs[i] = content.Kind.startingNeeds[i];
            Mood = content.Kind.startingMood;
            MoodTarget = content.Kind.startingMood;
            WorkPriorities = new byte[WorkTypeIndex.Count];
            for (int i = 0; i < WorkPriorities.Length; i++) WorkPriorities[i] = 3;
            Skills = new int[SkillIndex.Count];
            Passions = new byte[SkillIndex.Count];
            SkillGainedToday = new int[SkillIndex.Count];
        }

        public PawnId Id { get; }

        /// <summary>The content set this pawn reads its tuning from. Frozen, shared, never copied.</summary>
        public PawnContent Content { get; }

        /// <summary>Cell index, layer included. Always layer-aware; there is no 2D form of this.</summary>
        public int Cell { get; set; }

        public int[] Needs { get; }

        /// <summary>Displayed mood, 0..1000. Drifts toward <see cref="MoodTarget"/>.</summary>
        public int Mood { get; set; }

        /// <summary>Base plus the sum of active thought offsets, recomputed on the needs interval.</summary>
        public int MoodTarget { get; set; }

        /// <summary>
        /// Experience per skill, in thousandths of a point (see <see cref="SkillDef"/>). Levels
        /// are derived by <see cref="SkillLevel"/>, never stored.
        /// </summary>
        public int[] Skills { get; }

        /// <summary><see cref="Passion"/> per skill, as the byte it is saved as. Rolled once at spawn.</summary>
        public byte[] Passions { get; }

        /// <summary>
        /// Experience gained per skill since the day began, for the soft cap. Reset lazily by
        /// <see cref="GainExperience"/> when the day it belongs to (<see cref="SkillDay"/>) has
        /// passed, so the cap needs no cadence of its own and costs nothing on a tick nobody
        /// works.
        /// </summary>
        public int[] SkillGainedToday { get; }

        /// <summary>The day <see cref="SkillGainedToday"/> counts. Hashed and saved with it.</summary>
        public int SkillDay { get; internal set; }

        /// <summary>Player priority per work type: 0 disabled, 1 highest, 4 lowest.</summary>
        public byte[] WorkPriorities { get; }

        public List<Memory> Memories { get; } = new List<Memory>();

        // ---- job state -------------------------------------------------------------------

        /// <summary>The job in progress, or null when the pawn is between jobs.</summary>
        public Job? CurrentJob { get; internal set; }

        /// <summary>The driver running <see cref="CurrentJob"/>. Pooled per job kind, never per tick.</summary>
        public JobDriver? Driver { get; internal set; }

        /// <summary>
        /// The pawn's one job record, reused. A pawn has at most one job, so starting one is a
        /// field assignment rather than an allocation.
        /// </summary>
        public Job JobBuffer { get; } = new Job();

        /// <summary>One driver instance per job kind, built once at spawn and reset on reuse.</summary>
        public JobDriver[] DriverPool { get; internal set; } = System.Array.Empty<JobDriver>();

        /// <summary>Tick the current job started, for expiry.</summary>
        public int JobStartTick { get; internal set; }

        /// <summary>
        /// Every claim this pawn holds, in the order it took them. The ordered list, not the
        /// reservation table, is what release walks — a hash table's iteration order is not
        /// allowed to be a simulation input.
        /// </summary>
        public List<long> HeldReservations { get; } = new List<long>();

        // ---- mental state ----------------------------------------------------------------

        public int BreakTicksLeft { get; internal set; }

        public bool IsBroken => BreakTicksLeft > 0;

        public bool Asleep { get; internal set; }

        // ---- the think-loop circuit breaker ----------------------------------------------

        public int JobStartsInWindow { get; internal set; }
        public int WindowStartTick { get; internal set; }

        // ---- movement --------------------------------------------------------------------

        /// <summary>
        /// The path being followed, start cell first. Never saved: a recomputed path is correct
        /// by construction and a saved one can be stale.
        /// </summary>
        public int[] Path = new int[64];

        public int PathLength { get; internal set; }

        /// <summary>Index of the next cell to step into. Always at least 1 on a live path.</summary>
        public int PathIndex { get; internal set; }

        /// <summary>Cost units accumulated toward the next step.</summary>
        public int MoveProgress { get; internal set; }

        /// <summary>
        /// What the step now in progress costs, in the same units as <see cref="MoveProgress"/>.
        ///
        /// <para><b>Derived, and deliberately outside the hash and the save.</b> The movement
        /// system recomputes it from the graph every tick it advances a pawn, so storing it would
        /// be storing an answer the world can already give — and a saved copy that disagreed with
        /// a rebuilt graph would be a divergence nothing could explain.</para>
        ///
        /// <para>It exists for presentation. The published move percentage used to be the raw
        /// progress clamped to 100, which is exact for a flat cell at 100 units and wrong for
        /// everything dearer: a ladder down costs 400, so the drawn figure completed its whole
        /// descent in the first quarter of the step and then stood frozen at the bottom for the
        /// other three — which is most of what "colonists float down slowly" was.</para>
        /// </summary>
        public int MoveStepCost { get; internal set; } = Pathing.MoveCost.Orthogonal;

        /// <summary>Where the pawn is trying to get to, or -1.</summary>
        public int Destination { get; internal set; } = -1;

        /// <summary>A path request is queued and has not been served yet.</summary>
        public bool PathPending { get; internal set; }

        /// <summary>The last request failed; the driver treats this as a job failure.</summary>
        public bool PathFailed { get; internal set; }

        public bool HasPath => PathLength > 0 && PathIndex < PathLength;

        // ---- decision points, virtual on purpose -----------------------------------------

        /// <summary>How this pawn's needs fall. Override to make a pawn kind hungrier.</summary>
        public virtual int NeedFallPerInterval(int needIndex) =>
            Content.Needs[needIndex].FallPerInterval(Needs[needIndex]);

        /// <summary>Rest recovered per interval, scaled by what the pawn is lying on.</summary>
        public virtual int RestGainPerInterval(int bedEffectiveness) =>
            Content.Kind.restGainPerInterval * bedEffectiveness / 100;

        /// <summary>Mood drift toward the target. Rising is faster than falling, as it should be.</summary>
        public virtual int MoodDriftPerInterval(bool rising) =>
            rising ? Content.Mood.risePerInterval : Content.Mood.fallPerInterval;

        /// <summary>Is the pawn eligible to break at all? A sleeping pawn never is.</summary>
        public virtual bool CanMentalBreak() => !Asleep && !IsBroken && Mood < Content.Mood.breakThreshold;

        /// <summary>Cost units retired per tick. The place a movement-speed modifier belongs.</summary>
        public virtual int MovePerTick() => Content.Movement.movePerTick;

        /// <summary>
        /// How this pawn traverses. Taken from the current job and fixed for its whole life: a
        /// mode that changed halfway through a walk would silently invalidate the path the pawn
        /// is standing on.
        /// </summary>
        public virtual TraverseMode Mode =>
            CurrentJob != null ? CurrentJob.Mode : TraverseMode.Colonist;

        /// <summary>Whether the pawn will consider work at all this think.</summary>
        public virtual bool WillWork() => !IsBroken && !Asleep;

        /// <summary>Player priority for a work type, 0 meaning disabled.</summary>
        public virtual int WorkPriority(int workType) => WorkPriorities[workType];

        // ---- skills ----------------------------------------------------------------------

        /// <summary>The level of a skill, 0..20, read off its experience by the Def table.</summary>
        public int SkillLevel(int skill) => Content.Skills[skill].Level(Skills[skill]);

        public Passion PassionFor(int skill) => (Passion)Passions[skill];

        /// <summary>
        /// The global learning factor, per mille. 1,000 until traits exist; the reference adds
        /// trait and implant offsets here, which is why it is a seam and not a constant.
        /// </summary>
        public virtual int LearningFactorPerMille() => 1_000;

        /// <summary>
        /// Earn experience in a skill: the base amount, scaled by the learning factor and the
        /// passion, then by the over-cap factor once the day's gains have passed the soft cap,
        /// and never past the top level. Called once per tick of work by the job drivers.
        ///
        /// <para>The day counter is reset here rather than at midnight by a system, so a pawn
        /// that does not work costs nothing and the cap cannot drift from the counter.</para>
        /// </summary>
        public virtual void GainExperience(int skill, int baseExperience, int currentTick)
        {
            var def = Content.Skills[skill];
            int day = currentTick / Content.DayTicks;
            if (day != SkillDay)
            {
                for (int i = 0; i < SkillGainedToday.Length; i++) SkillGainedToday[i] = 0;
                SkillDay = day;
            }

            int gain = baseExperience * LearningFactorPerMille() / 1_000;
            gain = gain * def.gainPerMilleByPassion[Passions[skill]] / 1_000;
            if (SkillGainedToday[skill] >= def.dailySoftCap) gain = gain * def.overCapGainPerMille / 1_000;

            int ceiling = def.MaxExperience;
            if (Skills[skill] + gain > ceiling) gain = ceiling - Skills[skill];
            if (gain <= 0) return;

            Skills[skill] += gain;
            SkillGainedToday[skill] += gain;
        }

        /// <summary>
        /// Lose experience in a skill through disuse, floored at the first decaying level: decay
        /// is a property of the levels from <see cref="SkillDef.decayFromLevel"/> up, so it is
        /// never what drops a pawn out of them.
        /// </summary>
        public virtual void DecayExperience(int skill, int amount)
        {
            var def = Content.Skills[skill];
            int floor = def.ExperienceForLevel(def.decayFromLevel);
            int value = Skills[skill] - amount;
            Skills[skill] = value < floor ? floor : value;
        }

        /// <summary>
        /// Roll this pawn's passions from the world seed and its own id, so that the same seed
        /// gives the same colonists and adding a roll elsewhere cannot shift them. Called once,
        /// at placement; a load reads the saved bytes instead.
        /// </summary>
        public virtual void RollPassions(uint seed)
        {
            var kind = Content.Kind;
            var rng = DeterministicRandom.ForTick(seed, Id.Value, PawnPurpose.Passion);
            for (int skill = 0; skill < Passions.Length; skill++)
            {
                int roll = rng.NextInt(100);
                if (roll < kind.passionMajorPerCent) Passions[skill] = (byte)Passion.Major;
                else if (roll < kind.passionMajorPerCent + kind.passionMinorPerCent) Passions[skill] = (byte)Passion.Minor;
                else Passions[skill] = (byte)Passion.None;
            }
        }

        // ---- thoughts --------------------------------------------------------------------

        /// <summary>
        /// Remember something. Copies beyond the stack limit are dropped rather than queued: the
        /// limit is the point, and a queue behind it would only delay the same saturation.
        /// </summary>
        public virtual void AddMemory(int thoughtIndex, int currentTick)
        {
            var def = Content.Thoughts[thoughtIndex];
            int copies = 0;
            for (int i = 0; i < Memories.Count; i++)
                if (Memories[i].ThoughtIndex == thoughtIndex) copies++;
            if (copies >= def.stackLimit) return;
            Memories.Add(new Memory { ThoughtIndex = thoughtIndex, ExpiryTick = currentTick + def.durationTicks });
        }

        /// <summary>
        /// The summed mood offset of live memories, with each copy past the first scaled down.
        /// Walks the list in order and allocates nothing.
        /// </summary>
        public int MemoryMoodOffset(int currentTick)
        {
            int total = 0;
            for (int i = 0; i < Memories.Count; i++)
            {
                var memory = Memories[i];
                if (memory.ExpiryTick <= currentTick) continue;

                // Position among earlier copies of the same thought decides the multiplier.
                int earlier = 0;
                for (int j = 0; j < i; j++)
                    if (Memories[j].ThoughtIndex == memory.ThoughtIndex &&
                        Memories[j].ExpiryTick > currentTick) earlier++;

                var def = Content.Thoughts[memory.ThoughtIndex];
                int offset = def.moodOffset;
                for (int k = 0; k < earlier; k++) offset = offset * def.stackMultiplierPerMille / 1000;
                total += offset;
            }
            return total;
        }

        public void ExpireMemories(int currentTick)
        {
            for (int i = Memories.Count - 1; i >= 0; i--)
                if (Memories[i].ExpiryTick <= currentTick) Memories.RemoveAt(i);
        }

        // ---- path buffer -----------------------------------------------------------------

        public void ClearPath()
        {
            PathLength = 0;
            PathIndex = 0;
            MoveProgress = 0;
            PathPending = false;
            PathFailed = false;
        }

        internal void AdoptPath(int[] cells, int length)
        {
            if (Path.Length < length)
            {
                int capacity = Path.Length;
                while (capacity < length) capacity *= 2;
                Path = new int[capacity];
            }
            for (int i = 0; i < length; i++) Path[i] = cells[i];
            PathLength = length;
            PathIndex = 1;
            PathPending = false;
            PathFailed = false;

            // Move progress is deliberately *not* reset here. Adopting a path for a new walk
            // always follows a ClearPath, which zeroes it; the one case where a path is adopted
            // with progress already banked is a save being resumed, and there the progress is
            // exactly the state that has to survive.
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(Id.Value);
            hash.Add(Cell);
            for (int i = 0; i < Needs.Length; i++) hash.Add(Needs[i]);
            hash.Add(Mood);
            hash.Add(MoodTarget);
            for (int i = 0; i < Skills.Length; i++) hash.Add(Skills[i]);
            for (int i = 0; i < Passions.Length; i++) hash.Add(Passions[i]);
            for (int i = 0; i < SkillGainedToday.Length; i++) hash.Add(SkillGainedToday[i]);
            hash.Add(SkillDay);
            for (int i = 0; i < WorkPriorities.Length; i++) hash.Add(WorkPriorities[i]);
            hash.Add(BreakTicksLeft);
            hash.Add(Asleep);
            hash.Add(Memories.Count);
            for (int i = 0; i < Memories.Count; i++)
            {
                hash.Add(Memories[i].ThoughtIndex);
                hash.Add(Memories[i].ExpiryTick);
            }

            // Destination and move progress are simulation state; the path itself is not, and
            // hashing it would make a save/load resume look like a divergence for no reason.
            hash.Add(Destination);
            hash.Add(MoveProgress);

            hash.Add(HeldReservations.Count);
            for (int i = 0; i < HeldReservations.Count; i++) hash.Add(HeldReservations[i]);

            if (CurrentJob == null)
            {
                hash.Add(-1);
                return;
            }

            CurrentJob.ContributeTo(ref hash);
            hash.Add(JobStartTick);
            hash.Add(Driver != null ? Driver.ToilIndex : -1);
            hash.Add(Driver != null ? Driver.ToilProgress : -1);
        }
    }
}
