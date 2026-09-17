#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The needs the slice carries. Handles are integer indices into
    /// <see cref="PawnContent.Needs"/>: every tick-time need access is an array index, never a
    /// name lookup.
    /// </summary>
    public static class NeedIndex
    {
        public const int Food = 0;
        public const int Rest = 1;
        public const int Joy = 2;
        public const int Count = 3;
    }

    /// <summary>
    /// One band of a need, with the fall rate and situational mood offset that apply inside it.
    ///
    /// Bands are listed ascending by <see cref="upperBound"/> and the active band is the first
    /// one the value fits in. Per-band rates are the reason a pawn at 20% rest does not drain at
    /// the same rate as one at 80% — a flat rate makes the last fifth of every bar feel wrong.
    /// </summary>
    public class NeedBandDef
    {
        public int upperBound;
        public int fallPerInterval;
        public int moodOffset;
    }

    /// <summary>
    /// A need: an integer scalar in 0..<see cref="max"/>, updated on the needs interval.
    ///
    /// Nothing here is a float. A need is 0..1000 rather than 0..1 precisely so that band
    /// boundaries, fall rates and mood offsets are all exact integers that hash and save without
    /// a rounding question anywhere.
    /// </summary>
    public class NeedDef : Def
    {
        public int max = 1000;
        public int startValue = 800;

        /// <summary>Below this the think tree will divert the pawn to satisfy the need.</summary>
        public int seekThreshold;

        public List<NeedBandDef> bands = new List<NeedBandDef>();

        /// <summary>The band a value falls in. An ordered scan over at most six entries.</summary>
        public int BandIndex(int value)
        {
            for (int i = 0; i < bands.Count; i++)
                if (value <= bands[i].upperBound) return i;
            return bands.Count - 1;
        }

        public int FallPerInterval(int value) => bands[BandIndex(value)].fallPerInterval;

        public int MoodOffset(int value) => bands[BandIndex(value)].moodOffset;
    }

    /// <summary>
    /// A memory thought: created once by an event, expiring after a duration, stacking up to a
    /// limit with a diminishing multiplier per extra copy so that a repeated event saturates
    /// rather than growing without bound.
    /// </summary>
    public class ThoughtDef : Def
    {
        public int moodOffset;
        public int durationTicks = 30_000;
        public int stackLimit = 1;

        /// <summary>Per mille applied once per copy beyond the first.</summary>
        public int stackMultiplierPerMille = 750;
    }

    public static class ThoughtIndex
    {
        public const int Catharsis = 0;
        public const int AteMeal = 1;
        public const int SleptOnGround = 2;

        /// <summary>Rode a floor down when it collapsed (U29).</summary>
        public const int Fell = 3;
        public const int Count = 4;
    }

    /// <summary>
    /// Mood: a difficulty base plus the sum of active thought offsets, approached by drift.
    ///
    /// The drift is what makes mood read as a mood. Snapping to the target turns a single bad
    /// moment into an instant mental break, which is worse drama and worse design.
    /// </summary>
    public class MoodDef : Def
    {
        public int baseMood = 500;
        public int max = 1000;
        public int risePerInterval = 7;
        public int fallPerInterval = 5;

        /// <summary>Below this a break is rolled as a mean-time-between-events draw.</summary>
        public int breakThreshold = 350;

        /// <summary>Mean ticks between breaks while below the threshold. 10 in-game days.</summary>
        public int breakMtbTicks = 600_000;
    }

    /// <summary>The one break behaviour the slice carries. The taxonomy is a later milestone.</summary>
    public class MentalBreakDef : Def
    {
        public int durationTicks = 7_500;

        /// <summary>How far a broken pawn will wander from where it stands, in cells.</summary>
        public int wanderRadius = 6;

        /// <summary>Breaks are self-limiting: the aftermath lifts mood clear of the threshold.</summary>
        public int catharsisThought = ThoughtIndex.Catharsis;
    }

    // The values live in Odyssey.Sim.Contracts (JobHandle/ItemHandle): the published views carry
    // these indices, so both sides of the seam must count the same way. These aliases keep the
    // simulation's own code reading the short historical names.
    public static class JobIndex
    {
        public const int Haul = JobHandle.Haul;
        public const int Eat = JobHandle.Eat;
        public const int Sleep = JobHandle.Sleep;
        public const int Wander = JobHandle.Wander;
        public const int Wait = JobHandle.Wait;
        public const int Fell = JobHandle.Fell;
        public const int Mine = JobHandle.Mine;
        public const int Deliver = JobHandle.Deliver;
        public const int Build = JobHandle.Build;
        public const int Deconstruct = JobHandle.Deconstruct;
        public const int Count = JobHandle.Count;
    }

    /// <summary>A job names a driver; the driver runs toils. This is the naming half.</summary>
    public class JobDef : Def
    {
        /// <summary>Which driver runs it. A <see cref="JobIndex"/> value, not a class name.</summary>
        public int driver;

        /// <summary>Ticks after which the job ends of its own accord. Zero means never.</summary>
        public int expiryTicks;

        /// <summary>May a better think result take this job away without it failing?</summary>
        public bool casuallyInterruptible = true;

        /// <summary>Ticks of work the payload toil takes, where the job has one.</summary>
        public int workTicks;

        /// <summary>
        /// Ticks a colonist stands still after the work is done, before the job ends. Zero for a
        /// job that does not want one.
        ///
        /// <para><b>Follow-through</b> (owner, 2026-09-16: "should there be a second delay so you
        /// can motion more naturally instead of snapping?"). The work itself is finished — the
        /// tree is already down and the rock already gone — and this is the beat afterwards in
        /// which the colonist straightens up before walking off. Stopping an action dead is the
        /// thing that reads as mechanical, and a recovery beat is ordinary practice for exactly
        /// that reason.</para>
        ///
        /// <para><b>It also fixes a measured fault, which is why it is here and not a guess.</b>
        /// The drawn figure steps <i>in</i> towards its work — about 0.8 m for felling — and eases
        /// back out over <c>PawnFigureDirector.WorkEaseSeconds</c>, 0.45 s. Measured over 40,000
        /// ticks: all 27 work-to-move transitions began gliding within <b>1 to 3 ticks</b> of the
        /// work stopping, so every one of them was walking and un-stepping at the same time. The
        /// gait blend deliberately excludes the stance from the speed it measures, so the feet
        /// played an ordinary walk while the body covered the walk <i>and</i> the retraction —
        /// which is the "very quickly walk and then come to a normal pace" the owner saw, lasting
        /// exactly as long as the ease.</para>
        ///
        /// <para><b>So the number is not free taste: it must be at least the presentation ease</b>,
        /// 27 ticks at sixty a second. 30 is that with a little margin. A simulation constant
        /// chosen to cover a drawing constant is an uncomfortable coupling and it is the lesser
        /// one — the alternative is presentation reaching into job timing.</para>
        /// </summary>
        public int settleTicks;

        /// <summary>
        /// The skill a tick of this job's work trains, as a <see cref="SkillIndex"/> value, or -1
        /// for a job that trains nothing (eating, sleeping, wandering). One hop from the job to
        /// the skill rather than two through the work type, because a job is the thing that
        /// knows which of its ticks are work.
        /// </summary>
        public int trainsSkill = -1;

        /// <summary>
        /// Experience one tick of work is worth before passion, in thousandths of a point. Zero
        /// trains nothing. ASSUMED at 110 for every working job: a-01-pawns.md gives the level
        /// costs and the passion multipliers but not the base rate per tick. At 110 a colonist
        /// who works two thirds of a day earns about 4,400 points, so the daily soft cap is
        /// where a full working day lands, which is the relationship the cap exists to have.
        /// </summary>
        public int experiencePerWorkTick;
    }

    public static class WorkTypeIndex
    {
        public const int Haul = 0;
        public const int Cutting = 1;
        public const int Mining = 2;

        /// <summary>Carrying material to a building site, and working at one. Both, deliberately:
        /// fetching the wood is part of building the wall, not a haul that happens to help.</summary>
        public const int Construction = 3;

        public const int Count = 4;
    }

    /// <summary>
    /// The skills the slice carries: one per kind of work there is to do. Handles are integer
    /// indices into <see cref="PawnContent.Skills"/> and into every per-skill array on a pawn.
    /// </summary>
    public static class SkillIndex
    {
        // Unlike JobIndex, these are not aliases of anything in Sim.Contracts, and deliberately:
        // skills reach the interface as pawn aspects, keyed by name, so the index never crosses
        // the seam and the shared assembly does not have to know that skills exist. Adding one is
        // a change to this file and the Defs beside it.
        public const int Hauling = 0;
        public const int Cutting = 1;
        public const int Mining = 2;
        public const int Construction = 3;
        public const int Count = 4;

        /// <summary>
        /// The names skills are published under, parallel to the indices above.
        ///
        /// <para>A name rather than a number, because that is the whole point of an aspect: the
        /// interface reads <c>odyssey.pawn.skill.mining.level</c> without referencing this
        /// assembly or sharing an enum with it. The prefix is the project's, the middle is this
        /// feature's, and the leaf is the value — the same shape as an icon key.</para>
        /// </summary>
        public static readonly string[] Names = { "hauling", "cutting", "mining", "construction" };
    }

    /// <summary>
    /// How much a colonist cares about a skill. Stored on the pawn as a byte, rolled once at
    /// spawn, and the one thing about a skill that work does not change.
    /// </summary>
    public enum Passion : byte
    {
        None = 0,
        Minor = 1,
        Major = 2,
    }

    /// <summary>
    /// A skill: the experience curve, what passion makes a tick of work worth, the daily soft cap
    /// and the decay ladder. Every figure that is not marked ASSUMED is from a-01-pawns.md,
    /// "Skills, passions, learning".
    ///
    /// <para><b>Experience is in thousandths of a point.</b> The reference measures a level in
    /// points (1,000 to leave level 0, 265,000 to reach 20) and a tick of work in fractions of
    /// one, and a passion multiplier of 0.35 applied to a fraction rounds to nothing in integers.
    /// So a point is 1,000 here, the way a need's bar is 1,000, and every rate in this Def is in
    /// the same units. Nothing here is a float, so it all hashes and saves without a rounding
    /// question.</para>
    ///
    /// <para><b>The level is never stored.</b> <see cref="Level"/> reads it off the experience
    /// by this table, so there is no second field to fall out of step with the first. The cost
    /// of that is one thing the reference has and this does not: the 1,000-point grace below a
    /// level's floor before the level is lost. That needs a stored level, and the slice does
    /// without it; a level is lost the moment experience drops below its floor.</para>
    /// </summary>
    public class SkillDef : Def
    {
        public int maxLevel = 20;

        /// <summary>
        /// Experience to go from level L to L + 1, indexed by L. a-01: 1,000 × (L + 1) points to
        /// level 10, then 2,000 more a step, cumulative 265,000 to reach 20. Filled by
        /// <see cref="PawnContent.Core"/>, which says where the second slope comes from.
        /// </summary>
        public int[] experienceToAdvance = System.Array.Empty<int>();

        /// <summary>Gain multiplier per <see cref="Passion"/>, per mille. a-01: ×0.35, ×1.0, ×1.5.</summary>
        public int[] gainPerMilleByPassion = { 350, 1_000, 1_500 };

        /// <summary>Points a skill may gain in one day before the cap bites. a-01: 4,000.</summary>
        public int dailySoftCap = 4_000_000;

        /// <summary>Gains beyond the cap are scaled by this, per mille. a-01: ×0.2.</summary>
        public int overCapGainPerMille = 200;

        /// <summary>The first level that decays. a-01: level 10 and up.</summary>
        public int decayFromLevel = 10;

        /// <summary>
        /// Experience lost per day at each level, indexed by level. a-01 measured the two ends:
        /// about 30 points a day at 10 and about 3,600 at 20. The nine values between are
        /// ASSUMED, a geometric ladder from one end to the other (×1.61 per level), until the
        /// full table is fetched. Filled by <see cref="PawnContent.Core"/>.
        /// </summary>
        public int[] decayPerDay = System.Array.Empty<int>();

        /// <summary>The level an amount of experience is, 0..<see cref="maxLevel"/>. An ordered scan over twenty entries.</summary>
        public int Level(int experience)
        {
            int floor = 0;
            for (int level = 0; level < maxLevel; level++)
            {
                floor += experienceToAdvance[level];
                if (experience < floor) return level;
            }
            return maxLevel;
        }

        /// <summary>The experience at which a level begins. Level 0 begins at nothing.</summary>
        public int ExperienceForLevel(int level)
        {
            int floor = 0;
            for (int l = 0; l < level && l < maxLevel; l++) floor += experienceToAdvance[l];
            return floor;
        }

        /// <summary>The ceiling: the floor of the top level, past which experience is not gained.</summary>
        public int MaxExperience => ExperienceForLevel(maxLevel);

        /// <summary>
        /// Decay for one tick of the decay cadence at a level, or zero below
        /// <see cref="decayFromLevel"/>. The per-day figure is divided down to the cadence, so
        /// thirty Long ticks lose a day's worth, give or take the integer remainder.
        /// </summary>
        public int DecayPerInterval(int level, int intervalTicks, int dayTicks)
        {
            if (level < decayFromLevel || level >= decayPerDay.Length) return 0;
            return decayPerDay[level] / (dayTicks / intervalTicks);
        }
    }

    /// <summary>A container for work givers, carrying the natural order they scan in.</summary>
    public class WorkTypeDef : Def
    {
        /// <summary>Position in the work tab, left to right. Lower scans first at equal priority.</summary>
        public int order;
    }

    public static class ItemIndex
    {
        public const int Meal = ItemHandle.Meal;
        public const int Salvage = ItemHandle.Salvage;
        public const int Wood = ItemHandle.Wood;
        public const int Stone = ItemHandle.Stone;
        public const int IronOre = ItemHandle.IronOre;
        public const int Coal = ItemHandle.Coal;
        public const int Count = ItemHandle.Count;
    }

    /// <summary>
    /// The minimum a thing needs to be hauled or eaten. A real ThingDef belongs to the things
    /// line of work; this exists so pawns have something to pick up.
    /// </summary>
    public class ItemDef : Def
    {
        public bool haulable = true;

        /// <summary>Need units restored by eating one. Zero means it is not food.</summary>
        public int nutrition;

        public int stackLimit = 1;
    }

    /// <summary>Movement tuning. One unit of cost is 1/100 of a flat orthogonal cell crossing.</summary>
    public class MovementDef : Def
    {
        /// <summary>
        /// Cost units retired per tick. A flat cell costs 100, so 1 gives a hundred ticks per
        /// cell: at sixty ticks a second that is a 2.5 m cell every 1.67 s, or 1.5 m/s, the top
        /// of the range a person walks at (about 1.3 to 1.5 m/s) — brisk, and clearly a walk.
        ///
        /// It used to be 2, which is 3 m/s: a jog, and on screen it was one, because the drawn
        /// walk cycle covers about 2 m/s and anything faster blends the run clip in. Before that
        /// it was 10, roughly 54 km/h, and colonists visibly teleported around the map.
        ///
        /// A movement-speed modifier belongs in <see cref="Pawn.MovePerTick"/>, not here; and a
        /// pace between these integers wants the cost scale raised, not a fraction stored.
        /// </summary>
        public int movePerTick = 1;

        /// <summary>Estimated cost of a layer change, used to order candidates before pathing.</summary>
        public int layerChangeEstimate = 300;
    }

    /// <summary>What a pawn starts life with.</summary>
    public class PawnKindDef : Def
    {
        public int startingMood = 600;
        public int[] startingNeeds = { 800, 800, 800 };

        /// <summary>Rest at which a sleeping pawn wakes.</summary>
        public int wakeThreshold = 950;

        /// <summary>Rest gained per needs interval while asleep, at 100 bed effectiveness.</summary>
        public int restGainPerInterval = 6;

        /// <summary>Bed effectiveness, per cent, when sleeping on bare ground.</summary>
        public int groundRestEffectiveness = 80;

        /// <summary>
        /// Recreation gained per interval while idling. The slice has no recreation buildings,
        /// so idle time is the only source there is; without one, joy bottoms out on every
        /// colonist and a long run measures nothing but mental breaks.
        /// </summary>
        public int joyGainPerInterval = 8;

        /// <summary>
        /// Odds, per cent, that a freshly spawned colonist of this kind has a minor or a major
        /// passion for any one skill; the rest are none. ASSUMED: nothing in docs/research/ has
        /// measured how the reference distributes passions at generation. 35 and 15 give a
        /// colony of five, over two skills, a handful of passions and one or two burning ones.
        /// </summary>
        public int passionMinorPerCent = 35;

        public int passionMajorPerCent = 15;

        /// <summary>
        /// Starting skill levels (U37), one independent roll per skill. Index is the level,
        /// value is its weight out of the sum of the whole table; level 0 needs no entry beyond
        /// index 0 carrying the largest share. INVENTED: there is no RimWorld number to take
        /// (clean room) and nothing in docs/research/ or docs/design/ pins one. Weighted toward a
        /// low baseline, on the reasoning that a colonist's life before the crash was mostly not
        /// this particular trade, with a thin tail so an occasional colonist starts competent
        /// rather than every one of five arriving identical — which is also the reason `U40`'s
        /// candidate cards need this at all: three colonists rolled from the same table must be
        /// able to differ. Mean level is 1.16, and levels 6 and 7 together are a 2% roll, so a
        /// visibly skilled starting colonist is rare rather than routine.
        /// </summary>
        public int[] startingSkillLevelWeights = { 40, 20, 14, 10, 6, 4, 3, 2, 1 };
    }

    /// <summary>
    /// The numbers that belong to the pawn simulation as a whole rather than to any one need,
    /// job or item. They were fields on <see cref="PawnContent"/>, which meant they were the one
    /// part of the tuning that content could not reach.
    /// </summary>
    public class PawnTuningDef : Def
    {
        public int needsIntervalTicks = 150;
        public int dayTicks = 60_000;
        public int thinkLoopLimit = 10;
        public int thinkLoopWindowTicks = 60;
        public int standDownTicks = 120;
        public int woodPerTree = 27;
        public int stonePerRock = 8;
        public int stoneChanceOneIn = 1;
        public int orePerCell = 15;
    }

    /// <summary>
    /// Every tunable number the pawn simulation reads, in one frozen record.
    ///
    /// <para><b>There is one way to build one, and that is the point.</b>
    /// <see cref="FromDefs"/> reads the content pack at <c>Assets/Odyssey/Defs/Core/Pawns</c>,
    /// which is where these numbers live and where a mod or a design change edits them. Most
    /// callers want <c>ContentPack.Pawns()</c>, which finds that pack and caches the parse.</para>
    ///
    /// <para><b>There used to be a second way, and removing it is what closed OQ-15.</b> A
    /// <c>Core()</c> factory held the same tables written out in C#, and a test compared the two
    /// field for field — which made the migration safe but meant every new item, job and work
    /// type had to be written twice, in two languages, with nothing but that test to notice when
    /// one of them was forgotten. Every call site now loads the XML, so the duplicate is gone and
    /// <c>PawnContentDefTests</c> guards the content with a fingerprint instead: a change to these
    /// numbers is one deliberate line, and an accidental one fails.</para>
    ///
    /// <para>The array order below <em>is</em> the handle order, and it is not the order the
    /// loader stores Defs in: see <see cref="FromDefs"/>.</para>
    /// </summary>
    public sealed class PawnContent
    {
        public NeedDef[] Needs = System.Array.Empty<NeedDef>();
        public ThoughtDef[] Thoughts = System.Array.Empty<ThoughtDef>();
        public JobDef[] Jobs = System.Array.Empty<JobDef>();
        public WorkTypeDef[] WorkTypes = System.Array.Empty<WorkTypeDef>();
        public SkillDef[] Skills = System.Array.Empty<SkillDef>();
        public ItemDef[] Items = System.Array.Empty<ItemDef>();
        public MoodDef Mood = new MoodDef();
        public MentalBreakDef Break = new MentalBreakDef();
        public MovementDef Movement = new MovementDef();
        public PawnKindDef Kind = new PawnKindDef();

        /// <summary>The needs interval, in ticks. 150 is the cadence a-01-pawns.md measured.</summary>
        public int NeedsIntervalTicks = 150;

        /// <summary>
        /// Ticks in a day: 60,000, per a-15-time-and-simulation.md. The skill soft cap resets on
        /// it and the decay ladder is written per day. The calendar proper is later content; this
        /// is the one number the pawn simulation needs of it.
        /// </summary>
        public int DayTicks = 60_000;

        /// <summary>Job starts allowed inside <see cref="ThinkLoopWindowTicks"/> before a stand-down.</summary>
        public int ThinkLoopLimit = 10;

        /// <summary>
        /// Wood a felled tree leaves on the ground: 27, the pine class's vanilla yield
        /// (docs/research/a-08-plants-growing-food.md §1; the oak class gives 46). One stack of
        /// 75, so a single haul clears it.
        /// </summary>
        public int WoodPerTree = 27;

        /// <summary>Stone a plain rock cell leaves. ASSUMED, like everything else here.</summary>
        public int StonePerRock = 8;

        /// <summary>
        /// One rock cell in this many yields stone. **One, meaning every cell does.**
        ///
        /// <para>It was four, and four was tuned against the wrong denominator. The reasoning was
        /// that the played board holds eighty thousand cells of rock and a yield from every one
        /// would put six hundred thousand stone on the map — true, and irrelevant, because nobody
        /// mines a board. A player mines what they mark, which is tens of cells, and at one in
        /// four a dozen orders produced three piles of stone against five hundred and sixty-seven
        /// wood from the trees beside them. The colony read as getting nothing out of the rock,
        /// which is what a playtest said in as many words (owner, 2026-09-16).</para>
        ///
        /// <para>Kept as a dial rather than deleted, because the machinery behind it is worth
        /// having: the roll is a pure function of (world seed, cell index), so a partial yield can
        /// be reintroduced the day something wants one without reopening how it is decided.</para>
        /// </summary>
        public int StoneChanceOneIn = 1;

        /// <summary>Ore a seam cell leaves. Always, never rolled. ASSUMED.</summary>
        public int OrePerCell = 15;

        public int ThinkLoopWindowTicks = 60;

        /// <summary>How long a pawn tripped by the think-loop trap stands still.</summary>
        public int StandDownTicks = 120;

        /// <summary>
        /// The Def types this content is made of, registered on a loader in one place so that a
        /// caller cannot load half of it. Adding a pawn Def type and forgetting to register it
        /// gives "unknown Def type" at load, which is the right failure but the wrong place to
        /// have to remember.
        /// </summary>
        public static DefLoader Register(DefLoader loader) =>
            loader.Register<NeedDef>()
                .Register<ThoughtDef>()
                .Register<JobDef>()
                .Register<WorkTypeDef>()
                .Register<SkillDef>()
                .Register<ItemDef>()
                .Register<MoodDef>()
                .Register<MentalBreakDef>()
                .Register<MovementDef>()
                .Register<PawnKindDef>()
                .Register<PawnTuningDef>();

        /// <summary>
        /// The same content, read from a loaded <see cref="DefDatabase"/> rather than built in
        /// code.
        ///
        /// <para><b>Every array is filled by name, never by table order.</b> A handle here is a
        /// compile-time constant — <see cref="NeedIndex.Food"/> is 0 because the published views
        /// and the save both say so — while <c>DefLoader</c> sorts each table by defName so that
        /// handles it assigns are stable across machines. Those two orders are not the same one,
        /// and reading the table in its own order would silently swap food for joy the day a Def
        /// is renamed. So the names below are the contract: this list <i>is</i> the handle
        /// order.</para>
        ///
        /// <para>A missing or misspelt Def throws here rather than leaving a null in an array for
        /// the first tick to trip over, and the message names the type and the name it wanted.</para>
        /// </summary>
        public static PawnContent FromDefs(DefDatabase defs)
        {
            var content = new PawnContent();

            content.Needs = ByName<NeedDef>(defs, "Need_Food", "Need_Rest", "Need_Joy");
            content.Thoughts = ByName<ThoughtDef>(defs,
                "Thought_Catharsis", "Thought_AteMeal", "Thought_SleptOnGround", "Thought_Fell");
            content.Jobs = ByName<JobDef>(defs,
                "Job_Haul", "Job_Eat", "Job_Sleep", "Job_Wander", "Job_Wait", "Job_Fell", "Job_Mine",
                "Job_Deliver", "Job_Build", "Job_Deconstruct");
            content.WorkTypes = ByName<WorkTypeDef>(defs,
                "Work_Haul", "Work_Cutting", "Work_Mining", "Work_Construction");
            content.Skills = ByName<SkillDef>(defs,
                "Skill_Hauling", "Skill_Cutting", "Skill_Mining", "Skill_Construction");
            content.Items = ByName<ItemDef>(defs,
                "Item_Meal", "Item_Salvage", "Item_Wood", "Item_Stone", "Item_IronOre", "Item_Coal");

            content.Mood = One<MoodDef>(defs, "Mood_Default");
            content.Break = One<MentalBreakDef>(defs, "Break_Wander");
            content.Movement = One<MovementDef>(defs, "Movement_Colonist");
            content.Kind = One<PawnKindDef>(defs, "PawnKind_Colonist");

            var tuning = One<PawnTuningDef>(defs, "Tuning_Pawns");
            content.NeedsIntervalTicks = tuning.needsIntervalTicks;
            content.DayTicks = tuning.dayTicks;
            content.ThinkLoopLimit = tuning.thinkLoopLimit;
            content.ThinkLoopWindowTicks = tuning.thinkLoopWindowTicks;
            content.StandDownTicks = tuning.standDownTicks;
            content.WoodPerTree = tuning.woodPerTree;
            content.StonePerRock = tuning.stonePerRock;
            content.StoneChanceOneIn = tuning.stoneChanceOneIn;
            content.OrePerCell = tuning.orePerCell;

            return content;
        }

        static T[] ByName<T>(DefDatabase defs, params string[] names) where T : Def
        {
            var array = new T[names.Length];
            for (int i = 0; i < names.Length; i++) array[i] = One<T>(defs, names[i]);
            return array;
        }

        static T One<T>(DefDatabase defs, string defName) where T : Def
        {
            if (!defs.HasTable<T>())
                throw new DefLoadException($"the content has no {typeof(T).Name} at all, and '{defName}' is required.");
            if (!defs.Table<T>().TryGetHandle(defName, out var handle))
                throw new DefLoadException($"the content has no {typeof(T).Name} named '{defName}'.");
            return defs.Table<T>()[handle];
        }
    }

    /// <summary>
    /// Named random purposes. Each consumer draws from its own stream, so adding a system that
    /// rolls dice cannot silently shift the numbers an existing one sees.
    /// </summary>
    public static class PawnPurpose
    {
        public const uint MentalBreak = 0x9E37_79B1;
        public const uint Wander = 0x85EB_CA6B;
        public const uint Passion = 0xC2B2_AE35;

        /// <summary>
        /// Whether a rock cell gives up stone. Drawn from (world seed, <b>cell index</b>) rather
        /// than from the tick, which is the one thing about it that matters: the answer belongs
        /// to the cell and not to the moment. A cell mined on tick 900 in one run and tick 40,000
        /// in another yields the same, so the roll survives a save, a reload and a replay, and no
        /// amount of re-ordering the colony's work can reroll it.
        ///
        /// The number is not 0xC2B2_AE35, which is what it was written as and which
        /// <see cref="Passion"/> took on the same day on another branch. Two purposes sharing a
        /// salt is two streams that agree, and a colonist's passion deciding which rocks hold
        /// stone is the kind of coupling nothing would ever report.
        /// </summary>
        public const uint StoneYield = 0x27D4_EB2F;

        /// <summary>
        /// The odd unit when a demolished building refunds half of an odd cost.
        ///
        /// <para>Drawn from (world seed, <b>cell index ^ tick</b>) — and the tick is the whole
        /// difference from <see cref="StoneYield"/> above, which deliberately leaves it out.
        /// Stone is a property of the rock: the same cell must answer the same way for ever, or a
        /// reload would reroll the map's mineral wealth. A refund is a property of the
        /// <i>moment</i>. Keyed on the cell alone, every cell on the board would be permanently a
        /// "2" cell or a "3" cell — stable, discoverable, and then farmable by rebuilding the good
        /// ones. Keyed on both, it still replays identically from a seed, which is all determinism
        /// asks.</para>
        /// </summary>
        public const uint DeconstructRefund = 0x165667B1;

        /// <summary>
        /// Starting skill levels (U37). Drawn from (world seed, pawn id), the same shape as
        /// <see cref="Passion"/> and for the same reason: a colonist's skills and its passions
        /// are two different questions, and sharing a salt would make one answer depend on the
        /// other by coincidence of arithmetic rather than by design.
        ///
        /// <para>Not <c>0x1656_67B1</c>, which is what this was written as before the merge that
        /// found <see cref="DeconstructRefund"/> claiming the same value on another branch —
        /// both are the fifth of xxHash32's five prime constants, and this file had already used
        /// all five once each. There is no sixth prime to reach for, so this one steps outside
        /// that family rather than fight over who keeps it.</para>
        /// </summary>
        public const uint StartingSkill = 0x5A82_7999;

        /// <summary>
        /// The quality tier a bed finishes at (design 20 §6). Drawn from (world seed,
        /// <b>cell index ^ tick</b>) — the refund's shape, not the yield's, and for the refund's
        /// reason: quality is a property of the <i>moment</i> of completion, not of the cell. A
        /// bed rebuilt on the same spot is a new bed and may finish better, where a rock mined
        /// twice is not a thing that happens; keyed on the cell alone, every site on the board
        /// would be permanently a "Decent" site, stable, discoverable and then farmable by
        /// demolishing the disappointments.
        ///
        /// <para>Chosen outside the xxHash prime family for the reason
        /// <see cref="StartingSkill"/> records: all five primes are spent, and this is the
        /// third constant to step outside it.</para>
        /// </summary>
        public const uint BuildQuality = 0x7F4A_7C15;
    }
}
