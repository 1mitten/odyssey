#nullable enable
using System.Collections.Generic;
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
        public const int Count = 3;
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

    public static class JobIndex
    {
        public const int Haul = 0;
        public const int Eat = 1;
        public const int Sleep = 2;
        public const int Wander = 3;
        public const int Wait = 4;
        public const int Count = 5;
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
    }

    public static class WorkTypeIndex
    {
        public const int Haul = 0;
        public const int Count = 1;
    }

    /// <summary>A container for work givers, carrying the natural order they scan in.</summary>
    public class WorkTypeDef : Def
    {
        /// <summary>Position in the work tab, left to right. Lower scans first at equal priority.</summary>
        public int order;
    }

    public static class ItemIndex
    {
        public const int Meal = 0;
        public const int Salvage = 1;
        public const int Count = 2;
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
        /// Cost units retired per tick. A flat cell costs 100, so 2 gives fifty ticks per cell:
        /// at sixty ticks a second that is a cell every 0.83 s, about 3 m/s, which reads as a
        /// brisk walk. The previous 10 meant ten ticks a cell, roughly 54 km/h, and colonists
        /// visibly teleported around the map.
        /// </summary>
        public int movePerTick = 2;

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
    }

    /// <summary>
    /// Every tunable number the pawn simulation reads, in one frozen record.
    ///
    /// Built in code for now for the same reason CoreContent is: there is no content pack yet,
    /// and a clone with no Assets/ content must still run the simulation headless. The
    /// declaration order below <em>is</em> the handle order, so the eventual XML must declare the
    /// same names in the same order.
    ///
    /// TODO(content): move to Defs/Core/Pawns/*.xml, resolve handles by name at world
    /// construction, and delete <see cref="Core"/>. Nothing that reads these has to change.
    /// </summary>
    public sealed class PawnContent
    {
        public NeedDef[] Needs = System.Array.Empty<NeedDef>();
        public ThoughtDef[] Thoughts = System.Array.Empty<ThoughtDef>();
        public JobDef[] Jobs = System.Array.Empty<JobDef>();
        public WorkTypeDef[] WorkTypes = System.Array.Empty<WorkTypeDef>();
        public ItemDef[] Items = System.Array.Empty<ItemDef>();
        public MoodDef Mood = new MoodDef();
        public MentalBreakDef Break = new MentalBreakDef();
        public MovementDef Movement = new MovementDef();
        public PawnKindDef Kind = new PawnKindDef();

        /// <summary>The needs interval, in ticks. 150 is the cadence a-01-pawns.md measured.</summary>
        public int NeedsIntervalTicks = 150;

        /// <summary>Job starts allowed inside <see cref="ThinkLoopWindowTicks"/> before a stand-down.</summary>
        public int ThinkLoopLimit = 10;

        public int ThinkLoopWindowTicks = 60;

        /// <summary>How long a pawn tripped by the think-loop trap stands still.</summary>
        public int StandDownTicks = 120;

        public static PawnContent Core()
        {
            var content = new PawnContent();

            content.Needs = new[]
            {
                new NeedDef
                {
                    defName = "Need_Food", label = "food", seekThreshold = 300,
                    bands =
                    {
                        // Drain slackens as the bar empties, per a-01-pawns.md.
                        new NeedBandDef { upperBound = 0,    fallPerInterval = 0, moodOffset = -200 },
                        new NeedBandDef { upperBound = 125,  fallPerInterval = 1, moodOffset = -120 },
                        new NeedBandDef { upperBound = 250,  fallPerInterval = 2, moodOffset = -60 },
                        new NeedBandDef { upperBound = 1000, fallPerInterval = 4, moodOffset = 0 },
                    },
                },
                new NeedDef
                {
                    defName = "Need_Rest", label = "rest", seekThreshold = 280,
                    bands =
                    {
                        new NeedBandDef { upperBound = 0,    fallPerInterval = 0, moodOffset = -180 },
                        new NeedBandDef { upperBound = 100,  fallPerInterval = 1, moodOffset = -180 },
                        new NeedBandDef { upperBound = 200,  fallPerInterval = 2, moodOffset = -120 },
                        new NeedBandDef { upperBound = 280,  fallPerInterval = 3, moodOffset = -60 },
                        new NeedBandDef { upperBound = 1000, fallPerInterval = 3, moodOffset = 0 },
                    },
                },
                new NeedDef
                {
                    defName = "Need_Joy", label = "recreation", seekThreshold = 0,
                    bands =
                    {
                        new NeedBandDef { upperBound = 0,    fallPerInterval = 0, moodOffset = -200 },
                        new NeedBandDef { upperBound = 150,  fallPerInterval = 1, moodOffset = -100 },
                        new NeedBandDef { upperBound = 300,  fallPerInterval = 1, moodOffset = -50 },
                        new NeedBandDef { upperBound = 700,  fallPerInterval = 2, moodOffset = 0 },
                        new NeedBandDef { upperBound = 850,  fallPerInterval = 2, moodOffset = 50 },
                        new NeedBandDef { upperBound = 1000, fallPerInterval = 2, moodOffset = 100 },
                    },
                },
            };

            content.Thoughts = new[]
            {
                new ThoughtDef { defName = "Thought_Catharsis", moodOffset = 150, durationTicks = 30_000 },
                new ThoughtDef { defName = "Thought_AteMeal", moodOffset = 20, durationTicks = 15_000, stackLimit = 2 },
                new ThoughtDef { defName = "Thought_SleptOnGround", moodOffset = -40, durationTicks = 15_000 },
            };

            content.Jobs = new[]
            {
                new JobDef { defName = "Job_Haul", driver = JobIndex.Haul, expiryTicks = 5_000 },
                new JobDef { defName = "Job_Eat", driver = JobIndex.Eat, casuallyInterruptible = false, workTicks = 300 },
                new JobDef { defName = "Job_Sleep", driver = JobIndex.Sleep, casuallyInterruptible = false },
                new JobDef { defName = "Job_Wander", driver = JobIndex.Wander, expiryTicks = 1_200 },
                new JobDef { defName = "Job_Wait", driver = JobIndex.Wait, workTicks = 120 },
            };

            content.WorkTypes = new[]
            {
                new WorkTypeDef { defName = "Work_Haul", label = "hauling", order = 0 },
            };

            content.Items = new[]
            {
                new ItemDef { defName = "Item_Meal", label = "ration pack", nutrition = 450 },
                new ItemDef { defName = "Item_Salvage", label = "salvage" },
            };

            content.Mood = new MoodDef { defName = "Mood_Default" };
            content.Break = new MentalBreakDef { defName = "Break_Wander" };
            content.Movement = new MovementDef { defName = "Movement_Colonist" };
            content.Kind = new PawnKindDef { defName = "PawnKind_Colonist" };
            return content;
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
    }
}
