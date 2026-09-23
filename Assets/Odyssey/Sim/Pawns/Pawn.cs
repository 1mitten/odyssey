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
        /// <summary>
        /// The seed this pawn's own draws come from — its passions and its starting skills, and
        /// nothing else (U40).
        ///
        /// <para><b>It is the world's seed unless somebody says otherwise</b>, which is what
        /// <see cref="PawnRegistry.Spawn"/> sets, so a colony nobody chose — every headless run,
        /// every test, every scenario placement — rolls exactly what it rolled before this field
        /// existed. The pawn's id is still mixed into both draws, so five colonists on one world
        /// seed still differ from each other and from the map.</para>
        ///
        /// <para><b>Why a pawn needs one at all.</b> Before U40 there was no way to say "this
        /// colonist, differently": rerolling one candidate on a select screen would have meant
        /// changing the seed of the whole board. A per-pawn seed is the smallest thing that makes
        /// one person rerollable while leaving the world alone.</para>
        ///
        /// <para>Saved in a section of its own and in the state hash — see
        /// <c>docs/design/18-colonist-select.md</c> §3, which also says why it is not in the pawn
        /// record.</para>
        ///
        /// <para><b>A property rather than a field, and the setter is the point.</b> Anything
        /// derived from this seed and cached must be thrown away when it changes, and the one
        /// such thing is <see cref="InnatePacePerMille"/>. The seed arrives late twice over — a
        /// load restores it in <c>PawnSeedSection</c>, which runs <i>after</i> the pawn section
        /// that made the pawn, and a select screen rewrites it on a candidate — so a pace cached
        /// before either would have been drawn from seed zero and kept for the colonist's life.
        /// Nothing reads it that early today; this is what stops the day something does from
        /// being a silent one. The project has had that exact bug once already, when
        /// <c>PawnContext.Seed</c> was unset until the first tick and rolled every colony in the
        /// game from zero.</para>
        /// </summary>
        public uint RollSeed
        {
            get => _rollSeed;
            set
            {
                _rollSeed = value;
                _innatePacePerMille = 0;
            }
        }

        uint _rollSeed;

        public Pawn(PawnId id, int cell, PawnContent content, int kind = 0)
        {
            Id = id;
            Cell = cell;
            Content = content;
            Kind = kind;
            PawnKindDef kindDef = content.KindOf(kind);
            Needs = new int[NeedIndex.Count];
            for (int i = 0; i < NeedIndex.Count && i < kindDef.startingNeeds.Length; i++)
                Needs[i] = kindDef.startingNeeds[i];
            Mood = kindDef.startingMood;
            MoodTarget = kindDef.startingMood;
            WorkPriorities = new byte[WorkTypeIndex.Count];
            for (int i = 0; i < WorkPriorities.Length; i++) WorkPriorities[i] = 3;
            ScheduleHours = new byte[ScheduleHandle.Hours];
            for (int h = 0; h < ScheduleHours.Length; h++) ScheduleHours[h] = DefaultScheduleAt(h);
            Skills = new int[SkillIndex.Count];
            Passions = new byte[SkillIndex.Count];
            SkillGainedToday = new int[SkillIndex.Count];
        }

        public PawnId Id { get; }

        /// <summary>The content set this pawn reads its tuning from. Frozen, shared, never copied.</summary>
        public PawnContent Content { get; }

        /// <summary>
        /// What this pawn is, as an index into <see cref="PawnContent.Kinds"/> (design 29 §1).
        /// The colonist is 0, and so is every pawn from before kinds existed. Saved in a section
        /// of its own (<c>PawnKindSection</c>) and hashed here, beside the roll seed and for the
        /// same reason: saved state that is not derived belongs in the hash.
        ///
        /// <para>Settable only by the loader, which builds the pawn before the section that
        /// names its kind is read. Nothing else may change what a pawn is.</para>
        /// </summary>
        public int Kind { get; internal set; }

        /// <summary>
        /// An animal that has decided to walk off the board (design 30 §3). Set by the wildlife
        /// level-keeper, read by the animal's own think node, which then heads for the nearest
        /// edge; saved in its own section and folded into the hash beside the kind.
        /// </summary>
        public bool Leaving { get; internal set; }

        /// <summary>The species this pawn's kind spawns as: what walks. See <see cref="SpeciesDef"/>.</summary>
        public SpeciesDef Species => Content.SpeciesOf(Kind);

        /// <summary>
        /// A colonist, as against an animal. Every pawn-wide system asks this once at the top of
        /// its loop (design 29 §2): an animal has no needs tick, no mood, no skills, no work and no
        /// schedule, and the same movement, doors and falling as anyone.
        /// </summary>
        public bool IsPerson => Species.person;

        /// <summary>Cell index, layer included. Always layer-aware; there is no 2D form of this.</summary>
        public int Cell { get; set; }

        public int[] Needs { get; }

        /// <summary>
        /// How long she has been starving, 0..1000 (WS3, design 17 §4c) — the severity bar the
        /// design describes when it says food at zero stops being a need and becomes a condition
        /// with a name. Grown by <see cref="NeedsSystem"/> while the food need is at zero and
        /// drained by the same number while it is not, so one meal arrests the bar rather than
        /// merely stopping it; the bands it steps through are <see cref="StarvationOffsetPerMille"/>'s.
        ///
        /// <para><b>Saved.</b> A colonist reloaded mid-starvation is still starving, and how far
        /// gone is the whole of the answer to "how much time have I got".</para>
        /// </summary>
        public int StarvationSeverity { get; set; }

        /// <summary>Displayed mood, 0..1000. Drifts toward <see cref="MoodTarget"/>.</summary>
        public int Mood { get; set; }

        /// <summary>Base plus the sum of active thought offsets, recomputed on the needs interval.</summary>
        public int MoodTarget { get; set; }

        /// <summary>
        /// The last momentary thing this pawn did, for presentation to draw. See
        /// <see cref="PawnGesture"/>.
        ///
        /// <para><b>Deliberately not saved and deliberately not hashed.</b> It is a report about
        /// something that has already finished, and nothing in the simulation reads it back — so
        /// it can have no effect on a tick, and a determinism run with it and without it must
        /// produce the same state hash. A test asserts exactly that, because a field on
        /// <see cref="Pawn"/> that quietly reached the hash would be a save-format change made by
        /// accident.</para>
        ///
        /// <para>Losing it over a save is correct rather than merely tolerable: a colonist should
        /// not finish a lift it began before the game was closed.</para>
        /// </summary>
        public PawnGesture Gesture { get; set; }

        /// <summary>Bumped on every <see cref="BeginGesture"/>. See <see cref="PawnView.GestureSerial"/>.</summary>
        public byte GestureSerial { get; set; }

        /// <summary>
        /// Report that a momentary thing just happened, for whoever is drawing this pawn.
        ///
        /// <para>The serial is what distinguishes two of the same gesture in a row, so it advances
        /// on every call and not only when the kind changes. It wraps, and wrapping is harmless:
        /// the reader tests for a different value, never a greater one.</para>
        /// </summary>
        public virtual void BeginGesture(PawnGesture gesture)
        {
            Gesture = gesture;
            unchecked { GestureSerial++; }
        }

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

        /// <summary>
        /// What this colonist is told to be doing in each of the day's twenty-four hours, as a
        /// <see cref="ScheduleHandle"/> per hour (design 27 §12).
        ///
        /// <para><b>Saved, published and editable — and read by nothing.</b> The hour a colonist
        /// sleeps is still decided by their rest need. That is why this is deliberately absent
        /// from <see cref="ContributeTo"/>: a value no system consults cannot affect a tick, which
        /// is the same test the saved view passes, and hashing it now would move six golden
        /// numbers for a change that alters no behaviour. It enters the hash in the unit that
        /// makes the job system obey it, and the goldens move once, with a sentence.</para>
        /// </summary>
        public byte[] ScheduleHours { get; }

        /// <summary>
        /// The day a colonist arrives on: asleep through the small hours, a slow start, work
        /// through the middle of the day, and an evening off.
        ///
        /// <para><b>It is drawn but not obeyed, so today it is only legible.</b> A colony of
        /// twenty-four identical grey <i>Anything</i> bars would have been the honest picture of a
        /// schedule nothing reads, and also a panel nobody could learn to read. This is the shape
        /// the colony will keep when the schedule starts governing, so the picture is not a lie
        /// about the future — only about the present, which the panel says out loud.</para>
        /// </summary>
        public static byte DefaultScheduleAt(int hour) =>
            hour < 6 ? (byte)ScheduleHandle.Sleep
            : hour < 9 ? (byte)ScheduleHandle.Anything
            : hour < 18 ? (byte)ScheduleHandle.Work
            : hour < 22 ? (byte)ScheduleHandle.Recreation
            : (byte)ScheduleHandle.Sleep;

        /// <summary>This colonist's block for one hour, or <c>Anything</c> out of range.</summary>
        public virtual int ScheduleAt(int hour) =>
            hour < 0 || hour >= ScheduleHours.Length
                ? ScheduleHandle.Anything
                : ScheduleHours[hour];

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

        /// <summary>Cost units accumulated toward the next step, in thousandths (Rates).</summary>
        public int MoveProgress { get; internal set; }

        /// <summary>
        /// What the step now in progress costs, in the same units as <see cref="MoveProgress"/> —
        /// thousandths of the raw nav cost (Rates).
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
        /// other three — which is most of what "colonists float down slowly" was. Progress and
        /// cost scale together, so the published ratio reads exactly what it always read.</para>
        /// </summary>
        public int MoveStepCost { get; internal set; } = Pathing.MoveCost.Orthogonal * Rates.Scale;

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
            RestGainPerInterval(bedEffectiveness, 0);

        /// <summary>
        /// Rest recovered in one interval, at a given bed effectiveness.
        ///
        /// <para><b>The dither is not a nicety; without it two of the five quality tiers do
        /// nothing.</b> The base gain is 6 and the tiers are percentages, so the products are
        /// 4.8, 5.1, 6.0, 6.72, 7.5 and 8.4 — and integer division flattened those to 4, 5, 6,
        /// <b>6</b>, 7, 8. A Decent bed recovered rest at exactly the rate of a Normal one, which
        /// is to say the tier a colonist rolled was worth nothing at all. Measured, not supposed:
        /// <c>RestGainTests</c> walks the table.</para>
        ///
        /// <para><b>Carried on the interval index rather than in a field</b>, which is what keeps
        /// this out of the save and out of the hash. The fractional part is spent by a Bresenham
        /// step over <paramref name="intervalIndex"/>: over any hundred intervals a tier gains
        /// exactly its own percentage, and the sequence is a pure function of a number the world
        /// already stores. A remainder kept on the pawn would have been one more field to save,
        /// hash and round-trip for a fifth of a point of rest.</para>
        /// </summary>
        public virtual int RestGainPerInterval(int bedEffectiveness, int intervalIndex)
        {
            int scaled = Content.Kind.restGainPerInterval * bedEffectiveness;
            int whole = scaled / 100;
            int fraction = scaled % 100;
            if (fraction == 0) return whole;

            // The accumulator this step lands on. It wrapped iff it is below the amount added,
            // and a wrap is the extra point.
            int accumulator = (int)((long)intervalIndex * fraction % 100);
            return accumulator < fraction ? whole + 1 : whole;
        }

        /// <summary>Mood drift toward the target. Rising is faster than falling, as it should be.</summary>
        public virtual int MoodDriftPerInterval(bool rising) =>
            rising ? Content.Mood.risePerInterval : Content.Mood.fallPerInterval;

        /// <summary>Is the pawn eligible to break at all? A sleeping pawn never is.</summary>
        public virtual bool CanMentalBreak() => !Asleep && !IsBroken && Mood < Content.Mood.breakThreshold;

        /// <summary>
        /// The rate this pawn pays work at, in thousandths of a tick-at-standard-rate: 1,000 is
        /// the speed everything is tuned at today (design 17 §2). <b>The rate never changes what
        /// a thing costs; it changes how fast this pawn pays for it.</b> The four drivers add
        /// this to the accumulator their work banks in, and the comparison reads the cost ×
        /// <see cref="Rates.Scale"/>, so a cell worked by two colonists of different speed
        /// accumulates in a unit that means the same thing to both.
        ///
        /// <para>The value is the work type's def curve at her level of the skill that drives it,
        /// times <see cref="ConditionPerMille"/>, floored at the def's floor. The composition
        /// order is the reference's (design 17 §3e): curve first, then every multiplicative
        /// factor, then the clamp — the floor is applied last so no future factor can price a
        /// tick of work at nothing.</para>
        /// </summary>
        public virtual int WorkRatePerMille(int workType)
        {
            WorkTypeDef def = Content.WorkTypes[workType];
            int curve = def.rateSkill < 0
                ? Rates.Scale
                : def.WorkRatePerMille(SkillLevel(def.rateSkill));
            int rate = curve * ConditionPerMille() / 1_000;
            return rate < def.workRateFloorPerMille ? def.workRateFloorPerMille : rate;
        }

        /// <summary>
        /// Cost units this pawn retires per tick, in thousandths of the tuned speed — the place
        /// a movement-speed modifier belongs. Composed in the design's fixed order (design 17
        /// §4a): the pace she was rolled with, then condition, with load and health to arrive
        /// later in the same product. The terrain's own price is not here and must never be —
        /// the planner already charges the cell being entered, and a pawn factor in the step
        /// cost would count it twice (§4g).
        ///
        /// <para>The species' own pace is the last factor (design 29 §5): 1,000 for a person,
        /// which is exact, so no colonist's speed moved when it arrived; 700 for a hog and 900
        /// for a rat.</para>
        /// </summary>
        public virtual int MoveRatePerMille() =>
            Content.Movement.movePerTick * Rates.Scale
                * InnatePacePerMille() / 1_000
                * ConditionPerMille() / 1_000
                * Species.movePerMille / 1_000;

        /// <summary>
        /// The pace this colonist was dealt, per mille of the standard walk, rolled once from
        /// her seed and her id on the <see cref="PawnPurpose.MovePace"/> stream — the same shape
        /// as her passions and starting skills, so a seed deals the same people every load.
        /// Movement's alone: a colonist who walks quickly is not thereby a quicker carpenter.
        /// Cached on first read, both because the band needs no arithmetic twice and because a
        /// per-tick roll would be an allocation on every step of every walk.
        /// </summary>
        public virtual int InnatePacePerMille()
        {
            if (_innatePacePerMille == 0)
            {
                var rng = DeterministicRandom.ForTick(RollSeed, Id.Value, PawnPurpose.MovePace);
                var movement = Content.Movement;
                _innatePacePerMille = movement.innatePaceMinPerMille
                    + rng.NextInt(movement.innatePaceMaxPerMille - movement.innatePaceMinPerMille + 1);
            }

            return _innatePacePerMille;
        }

        int _innatePacePerMille;

        /// <summary>
        /// How the colonist is right now, as one scalar both rates read: 1,000 is well. One
        /// computation, one floor, two consumers — a colonist in a bad way is slower at walking
        /// and slower at working, and there is exactly one place to ask why. WS1 answered a
        /// constant; WS3 lets starvation offset it, in the three bands of
        /// <see cref="StarvationOffsetPerMille"/>.
        ///
        /// <para>The clamps are the point of writing the method this way. The ceiling is the
        /// reference's asymmetry and is worth keeping for ever: <b>nothing may raise condition
        /// above baseline</b> — being dulled slows you, being alert never speeds you up — and a
        /// future factor that pushes up is clamped here rather than trusted not to exist. The
        /// floor is ours and stands in for the downed state we do not have: the reference lets
        /// consciousness fall until the colonist drops, and on the day health builds that
        /// threshold, the floor gives way to it.</para>
        /// </summary>
        public virtual int ConditionPerMille()
        {
            int condition = Rates.Scale - StarvationOffsetPerMille();
            if (condition > Rates.Scale) condition = Rates.Scale;
            if (condition < ConditionFloorPerMille) condition = ConditionFloorPerMille;
            return condition;
        }

        /// <summary>Condition cannot fall below this, in per mille — 0.7 of herself, however bad
        /// it gets, until a health system replaces the floor with a threshold (design 17 §4c).</summary>
        public const int ConditionFloorPerMille = 700;

        /// <summary>
        /// The offset starvation applies to <see cref="ConditionPerMille"/>, read off the
        /// severity bar in thirds: −100 minor, −200 moderate, −300 severe. The offsets are the
        /// design's table (§4c); the bar's thirds as the band edges are ours, chosen so a missed
        /// meal is not immediately a penalty and the worst band is reached on the fifth day of
        /// not eating — gentle, recoverable, and legible at a glance on the bar itself.
        /// </summary>
        public virtual int StarvationOffsetPerMille()
        {
            if (StarvationSeverity >= 750) return 300;
            if (StarvationSeverity >= 500) return 200;
            if (StarvationSeverity >= 250) return 100;
            return 0;
        }

        /// <summary>
        /// How this pawn traverses. Taken from the current job and fixed for its whole life: a
        /// mode that changed halfway through a walk would silently invalidate the path the pawn
        /// is standing on. Between jobs it is the species' own (design 29 §4), which is what a
        /// wander target is tested for reachability under.
        /// </summary>
        public virtual TraverseMode Mode =>
            CurrentJob != null ? CurrentJob.Mode : Species.traverseMode;

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
        /// Roll this pawn's passions from <see cref="RollSeed"/> and its own id, so that the same
        /// seed gives the same colonists and adding a roll elsewhere cannot shift them. Called
        /// once, at placement; a load reads the saved bytes instead.
        ///
        /// <para><b>It reads the pawn's seed rather than taking the world's</b> since U40. For every
        /// colonist the world places itself those are the same number, so nothing about this draw
        /// moved; what changed is that a colonist chosen on a select screen can carry a seed of its
        /// own, and be rerolled without touching the board.</para>
        /// </summary>
        public virtual void RollPassions()
        {
            var kind = Content.Kind;
            var rng = DeterministicRandom.ForTick(RollSeed, Id.Value, PawnPurpose.Passion);
            for (int skill = 0; skill < Passions.Length; skill++)
            {
                int roll = rng.NextInt(100);
                if (roll < kind.passionMajorPerCent) Passions[skill] = (byte)Passion.Major;
                else if (roll < kind.passionMajorPerCent + kind.passionMinorPerCent) Passions[skill] = (byte)Passion.Minor;
                else Passions[skill] = (byte)Passion.None;
            }
        }

        /// <summary>
        /// Roll this pawn's starting skill levels from <see cref="RollSeed"/> and its own id (U37),
        /// one independent draw per skill against
        /// <see cref="PawnKindDef.startingSkillLevelWeights"/> — a separate stream from
        /// <see cref="RollPassions"/>, so adding this roll cannot shift a single passion anywhere.
        ///
        /// <para><b>Called from the world's first tick, deliberately not from placement.</b>
        /// Placement runs inside <see cref="ColonyWorld.Build"/>, before the golden-master gate's
        /// "Generated" hash is taken; rolling here would move that hash as well as "Simulated",
        /// which is not what the plan asks for. Waiting one tick keeps a freshly built,
        /// never-ticked world's skills at the constructor's zero — exactly the state before this
        /// unit existed — and it is also the truer shape of the thing: nobody has any standing
        /// before the clock has run at all. <see cref="StartingSkillsSystem"/> is what calls it,
        /// once, and never again for a pawn already carrying experience.</para>
        ///
        /// <para><b>A skill already holding experience is left alone.</b> The draw is still made
        /// for it, so the sequence a later skill reads never depends on which earlier ones were
        /// already decided, but the result is only written into a skill still at the
        /// constructor's zero. In the game that is every skill, every time — nothing sets
        /// experience before the first tick — but a fixture that spawns a pawn and assigns it a
        /// skill directly, to test something the roll has nothing to do with, is common in this
        /// suite, and a roll that stamped over it after the fact would fail tests that predate
        /// this unit for a reason that has nothing to do with what they check.</para>
        /// </summary>
        public virtual void RollStartingSkills()
        {
            var kind = Content.Kind;
            int[] weights = kind.startingSkillLevelWeights;
            if (weights.Length == 0) return;

            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            if (total <= 0) return;

            var rng = DeterministicRandom.ForTick(RollSeed, Id.Value, PawnPurpose.StartingSkill);
            for (int skill = 0; skill < Skills.Length; skill++)
            {
                int roll = rng.NextInt(total);
                int level = weights.Length - 1;
                int cumulative = 0;
                for (int l = 0; l < weights.Length; l++)
                {
                    cumulative += weights[l];
                    if (roll < cumulative) { level = l; break; }
                }

                if (Skills[skill] != 0) continue;

                var def = Content.Skills[skill];
                if (level > def.maxLevel) level = def.maxLevel;
                Skills[skill] = def.ExperienceForLevel(level);
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
            // U40. Saved state that is not derived belongs in the hash (OQ-50), and this decides
            // what a pawn is. Every Simulated golden moved when it arrived, deliberately.
            hash.Add(unchecked((int)RollSeed));
            // Design 29 §6. Every Simulated golden moved when it arrived, by the hash seeing one
            // more zero per colonist — measured to be that and nothing else.
            // Leaving rides in the kind's word: a colonist never leaves, so a board with no
            // animals hashes exactly as it did before wildlife (design 30 §3).
            hash.Add(Kind | (Leaving ? 1 << 16 : 0));
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
            // Both accumulators count thousandths (Rates) and the hash reads them whole. They
            // were briefly divided back, so that WS1 could land with no golden moving; WS3 then
            // re-baked every Simulated hash anyway and the division outlived its reason, leaving
            // a thousandfold blind spot — two runs could differ by up to 999 milliwork on a cell
            // or a step and agree, until the difference happened to cross a tick boundary. A
            // hash that is late to notice a divergence is the thing this hash exists not to be.
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
