#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;

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

        /// <summary>
        /// Added again at its <see cref="stackLimit"/>, does the memory <b>renew</b> — the copy
        /// that would lapse soonest pushed out to a full <see cref="durationTicks"/> from now — rather
        /// than the repeat being dropped? False, the rule every thought had before it, unless the
        /// content says otherwise (design 33 §14e: the friendly-fire memory, so the day runs from the
        /// latest blow). Read by <see cref="Pawn.AddMemory"/> and nothing else.
        /// </summary>
        public bool renewsOnRepeat;
    }

    public static class ThoughtIndex
    {
        public const int Catharsis = 0;
        public const int AteMeal = 1;
        public const int SleptOnGround = 2;

        /// <summary>Rode a floor down when it collapsed (U29).</summary>
        public const int Fell = 3;

        /// <summary>Woke from a night outside the temperature bands, cold side (design 28 §8).
        /// Appended, as every thought is — an index rides every saved memory.</summary>
        public const int SleptCold = 4;

        /// <summary>The same, hot side.</summary>
        public const int SleptHot = 5;

        /// <summary>Hurt by a colonist's blow (design 33 §12, friendly fire). Given by
        /// <c>FriendlyFireListener</c>; appended, as every thought is.</summary>
        public const int AttackedByColonist = 6;

        /// <summary>A colonist died; felt by every other colonist (design 33 §12).</summary>
        public const int ColonistDied = 7;

        // The kitchen (design 48 §4). What a colonist thinks of what she ate is the food's own
        // (`ItemDef.ateThought`); AteMeal above is the cooked meal's, and these are the rest.

        /// <summary>Ate a ration pack: filling, and nothing more.</summary>
        public const int AteRation = 8;

        /// <summary>Ate a meal the cook let burn.</summary>
        public const int AteBurnt = 9;

        /// <summary>Ate food raw: carrots from the pile, or worse.</summary>
        public const int AteRaw = 10;
        public const int Count = 11;
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
        public const int Sow = JobHandle.Sow;
        public const int Harvest = JobHandle.Harvest;
        public const int DraftHold = JobHandle.DraftHold;
        public const int Goto = JobHandle.Goto;
        public const int LayConduit = JobHandle.LayConduit;
        public const int RemoveConduit = JobHandle.RemoveConduit;
        public const int Refuel = JobHandle.Refuel;
        public const int AttackMelee = JobHandle.AttackMelee;
        public const int Flee = JobHandle.Flee;
        public const int Downed = JobHandle.Downed;
        public const int Equip = JobHandle.Equip;
        public const int Rescue = JobHandle.Rescue;
        public const int Steal = JobHandle.Steal;
        public const int Treat = JobHandle.Treat;
        public const int Patient = JobHandle.Patient;
        public const int Forage = JobHandle.Forage;
        public const int Cook = JobHandle.Cook;
        public const int AttackRanged = JobHandle.AttackRanged;
        public const int Count = JobHandle.Count;
    }

    /// <summary>
    /// The kinds a pawn can be, as handles into <see cref="PawnContent.Kinds"/> (design 29 §1).
    /// The order is a save contract: appended, never inserted.
    /// </summary>
    public static class PawnKindIndex
    {
        public const int Colonist = 0;
        public const int MiddenHog = 1;
        public const int DuctRat = 2;

        /// <summary>
        /// The debug-spawned hostile person (design 33 §1): a person species under the Hostile
        /// faction. Claimed by the combat contracts step.
        /// </summary>
        public const int Bandit = 3;

        /// <summary>
        /// The bandit with a pistol (design 55 §8): its own kind so a raid mix can name it. Appended.
        /// </summary>
        public const int Gunman = 4;

        /// <summary>The frog of the banks (design 30 §8): kind 5, species 3. Appended after the gunman.</summary>
        public const int CulvertFrog = 5;

        public const int Count = 6;
    }

    /// <summary>
    /// Whose side a kind is on (design 33 §3): <b>hostility comes from the kind</b>, so no pawn
    /// carries a saved field for it. The colony's own people are <see cref="Colony"/>; animals are
    /// <see cref="Wild"/> until something tames one; a bandit is <see cref="Hostile"/> and fights
    /// on sight.
    /// </summary>
    public enum Faction : byte
    {
        Colony = 0,
        Wild = 1,
        Hostile = 2,
    }

    /// <summary>
    /// What a hostile came for, once there is nobody left standing to fight and nothing left to
    /// break (design 33 §17; the owner, 2026-09-24: <i>"It will thieve items or kidnap people
    /// depending on their motivation creating a negative event (but they could be rescued later) -
    /// seam this later but for now - thieve items"</i>). On the kind, as its weapon and its way of
    /// walking are. <b>Only <see cref="Loot"/> is acted on</b>: <see cref="Kidnap"/> does exactly
    /// what <see cref="Loot"/> does today, and <c>TheftTests.AKidnapperStealsAsALooterDoes</c>
    /// says so. Appended, never inserted: a content name, and the day a motive is rolled per pawn
    /// it is saved.
    /// </summary>
    public enum Motive : byte
    {
        /// <summary>Came for nothing but the fight: with nobody to fight and nothing to break, it idles. Every kind but the bandit.</summary>
        None = 0,

        /// <summary>Carries off the nearest stack it can lift and leaves the board with it.</summary>
        Loot = 1,

        /// <summary>
        /// Carries off a downed colonist, for the colony to get back later. <b>The seam, not built</b>:
        /// today it loots, exactly as <see cref="Loot"/> does (design 33 §17f).
        /// </summary>
        Kidnap = 2,
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

    /// <summary>
    /// Aliases of <see cref="WorkHandle"/>, exactly as <see cref="JobIndex"/> aliases
    /// <see cref="JobHandle"/>: a work priority is written by the player as well as read, so the
    /// index crosses the seam inside an <see cref="Intent"/> and both sides must agree what it
    /// counts. The order is <c>Sim.Contracts/Catalogue.cs</c>'s and is written down there.
    /// </summary>
    public static class WorkTypeIndex
    {
        public const int Haul = WorkHandle.Haul;
        public const int Cutting = WorkHandle.Cutting;
        public const int Mining = WorkHandle.Mining;

        /// <summary>Carrying material to a building site, and working at one. Both, deliberately:
        /// fetching the wood is part of building the wall, not a haul that happens to help.</summary>
        public const int Construction = WorkHandle.Construction;

        /// <summary>
        /// Breaking ground in a growing zone and cutting what ripens there. One work type for
        /// both ends of the crop, because they are one craft at one patch of soil and a colonist
        /// who will sow but not reap strands the field at its only interesting moment.
        /// </summary>
        public const int Growing = WorkHandle.Growing;

        /// <summary>
        /// Carrying a downed colonist to a bed (design 33 §4, C4). Its giver is an emergency one,
        /// so it scans ahead of everything else at the same priority.
        /// </summary>
        public const int Rescue = WorkHandle.Rescue;

        /// <summary>Treating the hurt (design 37). An emergency giver, like rescue's.</summary>
        public const int Doctor = WorkHandle.Doctor;

        /// <summary>Working the bills at a galley or a campfire (design 48 §5).</summary>
        public const int Cooking = WorkHandle.Cooking;

        public const int Count = WorkHandle.Count;

        /// <summary>
        /// The names work types are published under, parallel to the indices above, and the same
        /// shape as <see cref="SkillIndex.Names"/>. The interface reads
        /// <c>odyssey.pawn.work.mining.priority</c> by name and never sees this array.
        ///
        /// <para><b>It has to stay as long as <see cref="Count"/>.</b> <c>WorkAspects</c> mints one
        /// key per work type by walking this array to <c>Count</c>, so a work type added to the
        /// indices and forgotten here is an index-out-of-range at static initialisation rather
        /// than a missing aspect — which is why growing is in both or in neither.</para>
        /// </summary>
        public static readonly string[] Names =
            { "haul", "cutting", "mining", "construction", "growing", "rescue", "doctor", "cooking" };
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
        public const int Growing = 4;

        /// <summary>
        /// Close combat (design 33 §1): the attacker's level reads the hit curve and the
        /// defender's the dodge curve, both in <see cref="CombatDef"/>, and every swing trains it.
        /// Claimed by the combat contracts step.
        /// </summary>
        public const int Melee = 5;

        /// <summary>Treating the hurt (design 37): buys speed at it and nothing else.</summary>
        public const int Medicine = 6;

        /// <summary>Cooking (design 48 §5): buys speed at the stove and keeps the meal from burning.</summary>
        public const int Cooking = 7;
        /// <summary>
        /// Ranged combat (design 47 §2a): the shooter's level reads the per-cell accuracy curve in
        /// <see cref="CombatDef"/>, raised to the distance in cells, and every shot trains it, hit
        /// or miss. Claimed by the ranged line's contracts step, 8 after medical supplies' Medicine and the kitchen's Cooking; a colonist from a save older than
        /// format 10 is dealt it once on load (<see cref="PawnRegistry.BackfillSkills"/>).
        /// </summary>
        public const int Shooting = 8;
        public const int Count = 9;

        /// <summary>
        /// The names skills are published under, parallel to the indices above.
        ///
        /// <para>A name rather than a number, because that is the whole point of an aspect: the
        /// interface reads <c>odyssey.pawn.skill.mining.level</c> without referencing this
        /// assembly or sharing an enum with it. The prefix is the project's, the middle is this
        /// feature's, and the leaf is the value — the same shape as an icon key.</para>
        /// </summary>
        public static readonly string[] Names = { "hauling", "cutting", "mining", "construction", "growing", "melee", "medicine", "cooking", "shooting" };
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
        /// How far an amount of experience stands between the level it buys and the next one, in
        /// per mille, and the level itself — both out of one walk of the ladder.
        ///
        /// <para><b>Published rather than the table it comes from</b> (SK2). The interface wants a
        /// bar, a bar wants a denominator, and the denominator is
        /// <see cref="experienceToAdvance"/>, which is tuning content: mirroring it into the
        /// presentation assembly would be a second copy of a number a mod is meant to be able to
        /// override. So the fraction is derived where the ladder lives, exactly as
        /// <see cref="Level"/> already is, and the table stays here.</para>
        ///
        /// <para><b>One scan, two answers.</b> <see cref="Level"/> and
        /// <see cref="ExperienceForLevel"/> each walk up to twenty entries, and the publish loop
        /// already asks for the level every tick for every skill of every colonist. Asking for the
        /// progress separately would walk the same ladder twice more, so the floor is carried out
        /// of the one walk that has already found it.</para>
        ///
        /// <para><b>The multiply is a long on purpose.</b> The top of the ladder is 265,000,000
        /// and a thousand times that overflows a signed 32-bit integer about eight times over. In
        /// int arithmetic this reads correctly at low levels and silently returns nonsense at high
        /// ones, which is the worst shape a bug can have.</para>
        ///
        /// <para>At <see cref="maxLevel"/> it is full: there is no next level to be part of the
        /// way towards, and <see cref="Pawn.GainExperience"/> pins experience to
        /// <see cref="MaxExperience"/> there.</para>
        /// </summary>
        public int ProgressPerMille(int experience, out int level)
        {
            int floor = 0;
            for (level = 0; level < maxLevel; level++)
            {
                int span = experienceToAdvance[level];
                if (experience < floor + span)
                {
                    if (span <= 0) return 1_000;
                    long into = (long)experience - floor;
                    if (into <= 0) return 0;
                    long permille = into * 1_000L / span;
                    return permille > 1_000L ? 1_000 : (int)permille;
                }
                floor += span;
            }

            level = maxLevel;
            return 1_000;
        }

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

        /// <summary>
        /// Per-mille chance that a <b>completed</b> piece of this work succeeds, at skill level 0.
        /// Construction is the only work type that completes things and rolls; every other type
        /// leaves this at the default, which is certainty — no roll, the behaviour before the
        /// success roll existed.
        ///
        /// <para>U26's last line, and the reference's own shape (a-04 §4: a novice at 75% rising
        /// to a certain 100%) re-anchored the way every reference curve here is: its certainty
        /// sits at skill 8, where its colonists actually are, and ours sits three levels up from
        /// zero, just above what our starting roll averages (1.16). The integers are INVENTED and
        /// the owner's to tune at the keyboard.</para>
        /// </summary>
        public int successBasePerMille = 1_000;

        /// <summary>
        /// Points of that chance per skill level. With the shipped 850 + 50 a level: a novice
        /// botches one wall in seven, a level-1 colonist one in ten, and a level-3 builder never
        /// botches at all.
        /// </summary>
        public int successSlopePerLevel;

        /// <summary>
        /// The chance a builder of this level completes a build successfully, in thousandths,
        /// floored at the base and ceilinged at certainty — no future content value can turn the
        /// roll into a guarantee below level 0 or a lottery above it.
        /// </summary>
        public int SuccessPerMille(int level)
        {
            int chance = successBasePerMille + successSlopePerLevel * level;
            return chance < 0 ? 0 : chance > 1_000 ? 1_000 : chance;
        }

        /// <summary>
        /// The <see cref="SkillIndex"/> whose level drives this type's work rate, or -1 for none.
        /// Hauling is the -1: a skill drives either rate or quality, and hauling has neither —
        /// it is move speed and carrying capacity (design 17 §3a, and 15-skills §6 agrees).
        /// </summary>
        public int rateSkill = -1;

        /// <summary>
        /// Work rate at skill level 0, per mille of the speed everything is tuned at (design
        /// 17 §3b). The default 1,000 is the flat rate a work type had before curves existed.
        /// </summary>
        public int workRateBasePerMille = 1_000;

        /// <summary>Work rate added per level of <see cref="rateSkill"/>, per mille.</summary>
        public int workRateSlopePerLevel;

        /// <summary>
        /// The lowest rate a pawn may pay at, per mille. The curve itself never reaches it —
        /// the floor is what stops a future multiplier from pricing a tick of work at nothing
        /// and turning every job into one the job system can never finish.
        /// </summary>
        public int workRateFloorPerMille = 100;

        /// <summary>
        /// The work rate of this type at a skill level, per mille: dead linear, no diminishing
        /// returns anywhere — every diminishing return in this project is on <i>acquiring</i>
        /// levels, which is machinery the skill Def already owns. The integers are INVENTED
        /// (design 17 §3b): anchored on our own mean starting roll of 1.16 rather than the
        /// reference's level 8, with the reference's relative character kept — mining steepest,
        /// construction shallowest.
        /// </summary>
        public int WorkRatePerMille(int level)
        {
            int rate = workRateBasePerMille + workRateSlopePerLevel * level;
            return rate < workRateFloorPerMille ? workRateFloorPerMille : rate;
        }
    }

    public static class ItemIndex
    {
        public const int Meal = ItemHandle.Meal;
        public const int Salvage = ItemHandle.Salvage;
        public const int Wood = ItemHandle.Wood;
        public const int Stone = ItemHandle.Stone;
        public const int IronOre = ItemHandle.IronOre;
        public const int Coal = ItemHandle.Coal;
        public const int Carrots = ItemHandle.Carrots;
        public const int Bat = ItemHandle.Bat;
        public const int Crowbar = ItemHandle.Crowbar;
        public const int Machete = ItemHandle.Machete;
        public const int ArcBlade = ItemHandle.ArcBlade;
        public const int MedicalSupplies = ItemHandle.MedicalSupplies;
        public const int Berries = ItemHandle.Berries;
        public const int Mushrooms = ItemHandle.Mushrooms;
        public const int CookedMeal = ItemHandle.CookedMeal;
        public const int VegetableMeal = ItemHandle.VegetableMeal;
        public const int BurntMeal = ItemHandle.BurntMeal;
        public const int Pistol = ItemHandle.Pistol;
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

        /// <summary>
        /// What kind of thing this is, as a <see cref="CategoryHandle"/> value — what a storage
        /// filter groups by and what the wiki files it under.
        ///
        /// <para>Defaults to <see cref="ItemCategory.Materials"/> rather than to nought, and the
        /// difference matters: nought is Food, and a commodity that forgot to declare itself would
        /// quietly join the pantry and be offered to a hungry colonist by a filter. Materials is
        /// the harmless answer and the commonest one.</para>
        /// </summary>
        public ItemCategory category = ItemCategory.Materials;

        /// <summary>
        /// What it does in a hand, or null for anything that is not a weapon (design 33 §1, C3).
        /// Read through <c>IWeaponRules</c>, never directly, so the lookup has one owner.
        /// </summary>
        public AttackDef? weapon;

        /// <summary>
        /// Hit points one unit restores when a doctor treats with it (design 37 §4), in whole
        /// points. Zero means it is not medicine. Self-treatment and the treatment cap scale and
        /// clamp it (<c>MedicalDef</c>); the amount itself is the item's, so a weaker item is one
        /// Def row.
        /// </summary>
        public int healPerUnit;

        /// <summary>
        /// Which food a hungry colonist takes first (design 48 §8): lowest first, and the nearest
        /// within a tier, so a cooked meal across the room beats a carrot at her feet. Read only
        /// for things with <see cref="nutrition"/>. <see cref="LastResortTier"/> and above is
        /// eaten only when nothing better can be reached.
        /// </summary>
        public int foodTier;

        /// <summary>A tier this deep is food only for somebody with nothing else: raw meat (design 48 §8).</summary>
        public const int LastResortTier = 4;

        /// <summary>
        /// May a cook put this in a pan (design 48 §5)? Raw food, which is carrots now and meat
        /// once there is any. Its <see cref="nutrition"/> is what it is worth there.
        /// </summary>
        public bool rawIngredient;

        /// <summary>
        /// Is this meat, for the purpose of what a meal comes out as (design 48 §5): a pan with any
        /// meat in it makes a meal, and one with none a vegetable meal.
        /// </summary>
        public bool meat;

        /// <summary>
        /// Ticks at the ordinary rate until a stack of this goes off, or 0 for never (design 48 §6).
        /// Declared with the kitchen; nothing reads it until the cold store (K2).
        /// </summary>
        public int ticksToRot;

        /// <summary>
        /// The <see cref="ThoughtIndex"/> eating one adds, or -1 for none (design 48 §4). What a
        /// colonist thinks of a meal is the food's, not the eater's, so a new food is one row.
        /// </summary>
        public int ateThought = -1;
    }

    /// <summary>
    /// One thing a cooking station can make (design 48 §5): raw food in, by nutrition, and one
    /// product out — which of three is decided by what went in and whether it burnt. Loaded from
    /// <c>Recipes.xml</c>; a <see cref="RecipeHandle"/> is its index.
    /// </summary>
    public class RecipeDef : Def
    {
        /// <summary>Work at the standard pace, in ticks, before the station's own factor and the cook's speed.</summary>
        public int workTicks = 300;

        /// <summary>Raw food it takes, by nutrition: 500 is three carrots or ten pieces of meat.</summary>
        public int ingredientNutrition = 500;

        /// <summary>What comes out when there was meat in the pan, by item def name.</summary>
        public string product = "";

        /// <summary>What comes out when there was none.</summary>
        public string productNoMeat = "";

        /// <summary>What comes out when the cook let it catch.</summary>
        public string burntProduct = "";

        /// <summary>
        /// Chance per mille that the meal burns, by the cook's skill level: the index is the level,
        /// and a level past the end reads the last entry (design 48 §5, the owner's shape).
        /// </summary>
        public int[] burnPerMilleByLevel = System.Array.Empty<int>();

        /// <summary>Where it can be made, and on what terms.</summary>
        public System.Collections.Generic.List<RecipeStation> stations =
            new System.Collections.Generic.List<RecipeStation>();

        // Resolved at load from the names above: item def indices. Not fields a Def declares, so
        // properties, which the binder never sees.
        public int ProductItem { get; set; } = -1;
        public int ProductNoMeatItem { get; set; } = -1;
        public int BurntItem { get; set; } = -1;

        /// <summary>The burn chance per mille at a skill level, before the station's factor.</summary>
        public int BurnPerMille(int level)
        {
            if (burnPerMilleByLevel.Length == 0) return 0;
            if (level < 0) level = 0;
            if (level >= burnPerMilleByLevel.Length) level = burnPerMilleByLevel.Length - 1;
            return burnPerMilleByLevel[level];
        }

        /// <summary>The terms at a building, or null where this recipe cannot be made there.</summary>
        public RecipeStation? At(int building)
        {
            for (int i = 0; i < stations.Count; i++)
                if (stations[i].building == building) return stations[i];
            return null;
        }
    }

    /// <summary>One place a <see cref="RecipeDef"/> can be made (design 48 §5).</summary>
    public class RecipeStation
    {
        /// <summary>A <see cref="BuildingHandle"/> value.</summary>
        public int building;

        /// <summary>The recipe's work here, per mille: a campfire is 2,000, twice the galley's time.</summary>
        public int workFactorPerMille = 1_000;

        /// <summary>The burn chance here, per mille of the recipe's: a campfire is 1,500.</summary>
        public int burnFactorPerMille = 1_000;

        /// <summary>An item this station also swallows per meal — the campfire's one wood — or -1.</summary>
        public int fuelItem = -1;

        /// <summary>How many of <see cref="fuelItem"/>.</summary>
        public int fuelCount;

        public bool NeedsFuel => fuelItem >= 0 && fuelCount > 0;
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
        /// A movement-speed modifier belongs in <see cref="Pawn.MoveRatePerMille"/>, not here; and
        /// a pace between these integers wants the cost scale raised, not a fraction stored.
        /// </summary>
        public int movePerTick = 1;

        /// <summary>
        /// The band a colonist's innate pace rolls in, per mille of the standard walk (WS3,
        /// design 17 §4b). The integers are INVENTED, but the top of the band is not free taste:
        /// it is bounded by the drawn walk cycle, which covers about 2 m/s where
        /// <see cref="movePerTick"/> 1 is 1.5 m/s, so anything past about 1,333 would visibly
        /// jog while ostensibly walking. ±15 per cent keeps every colonist inside a walk —
        /// roughly the true spread of human walking pace — and everything faster is reserved for
        /// a deliberate run, which is held (§4f) until the game has something worth running from.
        /// </summary>
        public int innatePaceMinPerMille = 850;

        public int innatePaceMaxPerMille = 1_150;

        /// <summary>Estimated cost of a layer change, used to order candidates before pathing.</summary>
        public int layerChangeEstimate = 300;

        /// <summary>
        /// How fast a drafted colonist moves, per mille of her own pace (design 33 §2h, design 17
        /// §4f): 2,000 — about 3 m/s at the standard pace, which the gait blend draws as a run.
        /// The owner's call after the first draft playtest (2026-09-23): <i>"when you are drafted
        /// you should walk faster/run as this would make sense with the urgency"</i>. It is the
        /// first reason to run the game has, which is what §4f held the run for.
        /// </summary>
        public int draftedPacePerMille = 2_000;

        /// <summary>
        /// How often a well, unladen person's jump over a one-cell stream falls short, per mille
        /// (design 46 §6): 30, one in thirty-three. INVENTED. A failed jump lands in the water
        /// and costs a soaking and a few seconds; nothing is hurt until the health model can
        /// carry an injury.
        /// </summary>
        public int jumpFailPerMille = 30;

        /// <summary>
        /// What carrying does to that chance, per mille of it: 2,000 doubles it. A load in the
        /// arms, or a person being carried to a bed. INVENTED.
        /// </summary>
        public int jumpFailCarryingPerMille = 2_000;
    }

    /// <summary>
    /// What a pawn <em>is</em> — the biology, as against <see cref="PawnKindDef"/>, which is
    /// what spawns (design 29 §1, a-09 §1). The colonist is a species like any other, with
    /// <see cref="person"/> set, and everything a person does that an animal does not — needs,
    /// mood, skills, work, a schedule, a roster card — hangs off that one flag. The rest is what
    /// walks: how it traverses, how fast against the colonist, how far it wanders and how long
    /// it rests between legs.
    ///
    /// <para>Wildness, ecosystem weight and commonality are not here until something reads
    /// them (design 29 §1).</para>
    /// </summary>
    public class SpeciesDef : Def
    {
        /// <summary>The registry key the interface names this species by.</summary>
        public string labelKey = string.Empty;

        /// <summary>The colonist's species, and nobody else's. See the class summary.</summary>
        public bool person;

        /// <summary>Nose to tail, for presentation to size a figure against. Not read by the simulation.</summary>
        public int bodyLengthMm = 1_200;

        /// <summary>
        /// Pace relative to the colonist's standard walk, per mille: the last factor in
        /// <see cref="Pawn.MoveRatePerMille"/>'s product. 1,000 is exact in integer arithmetic,
        /// which is what keeps a person's speed where it was.
        /// </summary>
        public int movePerMille = 1_000;

        /// <summary>
        /// How this species traverses the graph — the mask every link and portal edge already
        /// carries (design 29 §4). A hog is <see cref="TraverseMode.Animal"/>: no ladders and no
        /// doors it must open. A rat climbs anything but does not swim, so it is
        /// <see cref="TraverseMode.Climber"/>. A kind may override it
        /// (<see cref="PawnKindDef.traverseMode"/>): the bandit does.
        /// </summary>
        public TraverseMode traverseMode = TraverseMode.Colonist;

        /// <summary>Cells either side of where it stands that a wander may pick.</summary>
        public int wanderRadius = 6;

        /// <summary>The rest between legs, in ticks, jittered between these two (design 29 §3).</summary>
        public int restTicksMin = 300;

        public int restTicksMax = 900;

        /// <summary>
        /// Out at night and resting by day (design 30 §4). Off-hours an animal takes a quarter
        /// as many legs and rests three times as long; the hours are the board clock's, 20:00 to
        /// 06:00. A rat is nocturnal; a hog is not.
        /// </summary>
        public bool nocturnal;

        /// <summary>
        /// Keeps within this many cells of water, Chebyshev, or 0 for anywhere (design 30 §8): the
        /// frog's bank. Every leg its mind picks ends this close to a water cell on its own layer
        /// or the one below, and an animal that finds itself further out heads back to the
        /// nearest bank it can reach. A world seeds it on the bank habitat.
        /// </summary>
        public int bankRadius;

        /// <summary>
        /// Stays out in the rain rather than heading for cover (design 43 §6, design 30 §8). The
        /// shelter node's own flag, named in its summary for the day a species wanted it: a frog.
        /// </summary>
        public bool ignoresRain;

        /// <summary>
        /// Cells within which a new leg is turned away from its own kind's (design 30 §8e), or 0
        /// for no such rule. A frog picking where to hop next looks at every other frog this close
        /// that is already hopping somewhere and prefers a heading at least 60 degrees from all of
        /// theirs (owner, 2026-09-26: a group's frogs "jump in different directions as some were
        /// very similar"). A preference, never a refusal: where every open cell lies the same way,
        /// the least alike is taken.
        /// </summary>
        public int divergeRadius;

        /// <summary>The figure catalogue entry presentation draws this species with. Not read by the simulation.</summary>
        public string figureKey = string.Empty;

        // ---- combat (design 33 §1, §3) -----------------------------------------------------

        /// <summary>
        /// The hit-point pool, in whole points (owner, 2026-09-23: person 100, hog 60, rat 15).
        /// A pawn carries its hit points in thousandths of these, <c>Pawn.HpMilli</c>, the
        /// <c>Rates</c> convention, so a slow heal is exact without a float.
        /// </summary>
        public int healthPoints = 100;

        /// <summary>
        /// Dead at this fraction of the pool, per mille and negative (owner: dead at −50 %).
        /// Downed at nought and below; dead at or below <c>healthPoints × this / 1000</c>.
        /// </summary>
        public int deathAtPerMille = -500;

        /// <summary>
        /// The chance, per mille, that a hurt animal turns on whoever hurt it rather than running
        /// (owner: a hog usually turns, a rat usually runs). Rolled on every hit. Unread for a
        /// person, whose answer is the faction's.
        /// </summary>
        public int revengePerMille;

        /// <summary>
        /// What it fights with when it holds nothing, or null for a person, whose bare hands are
        /// <see cref="CombatDef.fists"/>. A hog's tusks, a rat's teeth.
        /// </summary>
        public AttackDef? naturalAttack;

        /// <summary>
        /// The melee level an animal fights at, 0–20, read on the same hit and dodge curves as a
        /// colonist's skill. Animals have no skills to train (design 29 §2), so it is a constant
        /// of the species. Unread for a person.
        /// </summary>
        public int meleeSkill;

        // ---- health (design 43 §2) ---------------------------------------------------------

        /// <summary>
        /// The body this species has, by the <see cref="HealthDef"/>'s defName, or empty for none.
        /// A species with no body keeps the hit-point pool alone and nothing else: no regions, no
        /// injuries, no bleeding, no tending — every animal today (design 43 §8).
        /// </summary>
        public string health = string.Empty;

        /// <summary>
        /// The chance, per mille, that a bullet crossing this pawn's cell takes it (design 47 §2c),
        /// before the dead zone near the shooter scales it. The reference's 40 % × body size,
        /// clamped to 4–80 %.
        /// </summary>
        public int interceptPerMille = 400;
    }

    /// <summary>What a pawn starts life with.</summary>
    public class PawnKindDef : Def
    {
        /// <summary>
        /// The species this kind spawns as, by defName (design 29 §1). Resolved once, by name, in
        /// <see cref="PawnContent.FromDefs"/>; a kind naming a species the content does not have
        /// fails the load rather than the first tick.
        /// </summary>
        public string species = "Species_Person";

        /// <summary>Whose side it is on (design 33 §3). See <see cref="Faction"/>.</summary>
        public Faction faction = Faction.Colony;

        /// <summary>
        /// The weapons this kind may arrive holding, by item defName, or none for bare hands (design
        /// 33 §1: the bandit is "debug-spawned, armed"). One is dealt per pawn by
        /// <see cref="PawnContent.WeaponFor"/> — a crowbar or a bat for a bandit (owner,
        /// 2026-09-24: <i>"not swords - not their style"</i>, <c>docs/design/42-bandits.md</c>).
        /// Resolved once, by name, into <see cref="PawnContent.KindWeapons"/>; a kind naming an item
        /// the content does not have, or one with no <see cref="ItemDef.weapon"/> block, fails the
        /// load. What puts it in the hand is <see cref="IWeaponRules.ArmOnSpawn"/>, which the
        /// registry calls for every pawn it spawns whose kind names one — so the colonist and the
        /// two animals cost one comparison.
        /// </summary>
        public string[] weapons = System.Array.Empty<string>();

        /// <summary>
        /// How this kind traverses the graph, by <see cref="TraverseMode"/> name, when it is not
        /// its species' way (design 33 §16); empty takes <see cref="SpeciesDef.traverseMode"/>. A
        /// bandit is a person who does not open the colony's doors, so it is
        /// <see cref="TraverseMode.Bandit"/> on the kind while its species stays the colonist's.
        /// Resolved once, by name, into <see cref="PawnContent.KindMode"/>; a name the enum does not
        /// have fails the load. Read through <see cref="Pawn.OwnMode"/>, never here.
        /// </summary>
        public string traverseMode = string.Empty;

        /// <summary>
        /// What a hostile of this kind came for (design 33 §17): <see cref="Motive.Loot"/> for the
        /// bandit, <see cref="Motive.None"/> for everybody else. Resolved once into
        /// <see cref="PawnContent.KindMotive"/>; read through <see cref="PawnContent.MotiveOf"/>.
        /// </summary>
        public Motive motive = Motive.None;

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
        /// Starvation severity gained per needs interval while the food need is at zero, and
        /// lost per interval while it is not (WS3, design 17 §4c). INVENTED: the design pins the
        /// three offsets and the floor and nothing about the bar's speed.
        ///
        /// <para><b>The cadence is what sets this number, and it is 400 intervals a day</b> —
        /// 60,000 tick day over the 150-tick needs cadence. One per interval is 400 per mille a
        /// day, so a bar that fills in two and a half days of an empty pantry: the food need
        /// reaches zero at hour 72, the first band about fifteen hours later, and the worst band
        /// in the fifth day of not eating. That is the gentle, recoverable slope the design
        /// argues for, and recovery is symmetric by the same number, so one meal arrests the bar
        /// rather than merely stopping it.</para>
        ///
        /// <para>It was 2, on a comment that read the cadence as 200 intervals a day and so
        /// described a bar filling four times slower than it did. The arithmetic is written out
        /// above rather than summarised, because it is the sentence that was wrong, and
        /// <c>TheBarFillsAtTheCadenceItsCommentClaims</c> is the test that now holds it.</para>
        /// </summary>
        public int starvationPerInterval = 1;

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
    /// How temperature feels and what it does to a colonist: the comfort band, the mood and
    /// sleep bands around it, the work band, and the severity that builds past the safe bounds
    /// (design 28 §8). Authored in <c>Defs/Core/Pawns/Temperature.xml</c>; every temperature is
    /// centi-degrees and every factor is per-mille, like the rest of the model.
    ///
    /// <para><b>Bands, not curves.</b> Four of them each side of comfort — comfortable, mild,
    /// bad, extreme — because a colonist who is a little cold and one who is freezing differ in
    /// kind, and a smooth slope would hide the moment the player is deciding against. The band
    /// edges are fields so a mod can widen comfort without rewriting the offsets.</para>
    /// </summary>
    public class TemperatureDef : Def
    {
        /// <summary>The comfort band. Inside it, temperature does nothing at all.</summary>
        public int comfortMinC = 1_600;
        public int comfortMaxC = 2_600;

        /// <summary>Width of the mild band beyond comfort: cool below, warm above.</summary>
        public int mildBandC = 600;

        /// <summary>The cold floor and the hot ceiling: past these the extreme band begins, and
        /// past these severity builds.</summary>
        public int coldFloorC = -300;
        public int hotCeilingC = 3_500;

        /// <summary>Situational mood offset in the mild band (cool or warm).</summary>
        public int moodMildOffset = -10;

        /// <summary>In the bad band (cold or hot).</summary>
        public int moodBadOffset = -50;

        /// <summary>In the extreme band (freezing or sweltering).</summary>
        public int moodExtremeOffset = -120;

        /// <summary>Rest effectiveness in each band, per-mille of the bed's own answer.</summary>
        public int sleepMildPerMille = 900;
        public int sleepBadPerMille = 750;
        public int sleepExtremePerMille = 550;

        /// <summary>The work band: outside it, work rate is scaled.</summary>
        public int workMinC = 800;
        public int workMaxC = 3_500;
        public int workOutsidePerMille = 700;

        /// <summary>Severity begins below this (hypothermia) and above <see cref="hotCeilingC"/>
        /// (heatstroke) — which are the same edges as the mood bands' extremes, so what feels
        /// worst is what first hurts.</summary>
        public int hypothermiaC = -300;
        public int heatstrokeC = 3_500;

        /// <summary>
        /// Severity per needs interval, per centi-degree of distance beyond the safe bound:
        /// distance × this / 1000. 15 makes a Candle night at −13 °C (a thousand centi-degrees
        /// past the floor) build 15 an interval — a full bar in 67 intervals, four game-hours —
        /// and a cold snap's −33 °C fill it in an hour and a half, which is the "lethal
        /// hypothermia within hours" the almanac already promises. It shipped as 300 for a day,
        /// applied per centi-degree as the formula says, and filled the bar in fourteen
        /// game-minutes while three comments promised hours (design 28 §12, F1).
        /// </summary>
        public int severitySlopePerMille = 15;

        /// <summary>Severity drained per interval inside the safe bounds. One arrest, not a
        /// cure: a frozen colonist warms through over a day, not a step.</summary>
        public int severityRecoveryPerInterval = 5;

        /// <summary>Body heat, in centi-degree-cells per pawn per thermal pass.</summary>
        public int bodyHeatPerPass = 15;

        /// <summary>No body heat at or above this — the crowded-room brake.</summary>
        public int bodyHeatGateC = 4_000;

        /// <summary>Which of the four bands a temperature falls in: 0 comfortable, 1 mild,
        /// 2 bad, 3 extreme.</summary>
        public int BandOf(int tempC)
        {
            if (tempC < coldFloorC || tempC > hotCeilingC) return 3;
            if (tempC < comfortMinC - mildBandC || tempC > comfortMaxC + mildBandC) return 2;
            if (tempC < comfortMinC || tempC > comfortMaxC) return 1;
            return 0;
        }

        /// <summary>The situational mood offset at a temperature — recomputed, never stored, the
        /// same answer the need bands give.</summary>
        public int MoodOffset(int tempC)
        {
            switch (BandOf(tempC))
            {
                case 1: return moodMildOffset;
                case 2: return moodBadOffset;
                case 3: return moodExtremeOffset;
                default: return 0;
            }
        }

        /// <summary>Rest effectiveness at a temperature, per-mille of the bed's own answer.</summary>
        public int SleepPerMille(int tempC)
        {
            switch (BandOf(tempC))
            {
                case 1: return sleepMildPerMille;
                case 2: return sleepBadPerMille;
                case 3: return sleepExtremePerMille;
                default: return 1_000;
            }
        }

        /// <summary>Work rate at a temperature, per-mille — the reference's own ×0.70 outside
        /// its comfortable working band, carried as content rather than code.</summary>
        public int WorkPerMille(int tempC) =>
            tempC >= workMinC && tempC <= workMaxC ? 1_000 : workOutsidePerMille;

        /// <summary>
        /// Severity change this needs interval, signed: negative is hypothermia, positive
        /// heatstroke, zero inside the safe bounds (recovery is the caller's, by the symmetric
        /// drain, exactly as starvation recovers).
        /// </summary>
        public int SeverityDelta(int tempC)
        {
            if (tempC < hypothermiaC) return -((hypothermiaC - tempC) * severitySlopePerMille / 1_000);
            if (tempC > heatstrokeC) return (tempC - heatstrokeC) * severitySlopePerMille / 1_000;
            return 0;
        }
    }

    /// <summary>The numbers that belong to the pawn simulation as a whole rather than to any one need,
    /// job or item. They were fields on <see cref="PawnContent"/>, which meant they were the one
    /// part of the tuning that content could not reach.</summary>
    public class PawnTuningDef : Def
    {
        public int needsIntervalTicks = 150;
        public int dayTicks = 60_000;
        public int thinkLoopLimit = 10;
        public int thinkLoopWindowTicks = 60;
        public int standDownTicks = 120;
        public int stonePerRock = 8;
        public int stoneChanceOneIn = 1;
        public int orePerCell = 15;
        public int liftTicks = 48;
        public int liftGraspTicks = 24;
        public int draftQuietTicks = 10_000;
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

        /// <summary>
        /// The item table slot of a def name, or -1 if the content has no such item. What a Def
        /// that names its yield by <c>[DefReference]</c> is resolved through — a crop, a tree, a
        /// bush — rather than each keeping a handle of its own: an item handle is a save contract,
        /// and deriving one at load would be a second place to keep it in step. The table is a
        /// dozen entries long and this is asked once a harvest.
        /// </summary>
        public int ItemIndexOf(string defName)
        {
            for (int i = 0; i < Items.Length; i++)
                if (string.Equals(Items[i].defName, defName, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }
        /// <summary>What a cooking station can make, in <see cref="RecipeHandle"/> order (design 48 §5).</summary>
        public RecipeDef[] Recipes = System.Array.Empty<RecipeDef>();
        public MoodDef Mood = new MoodDef();
        public MentalBreakDef Break = new MentalBreakDef();
        public MovementDef Movement = new MovementDef();

        /// <summary>The fight's numbers (design 33 §1): the curves, bare hands, healing, the windows.</summary>
        public CombatDef Combat = new CombatDef();

        /// <summary>
        /// The colonist's kind — <see cref="Kinds"/>[0] once loaded. Kept as a field of its own
        /// because every needs and rest rule reads its tuning through this name, and because a
        /// <see cref="PawnContent"/> built in code rather than from Defs has no table at all.
        /// </summary>
        public PawnKindDef Kind = new PawnKindDef();
        public TemperatureDef Temperature = new TemperatureDef();

        /// <summary>
        /// Every kind a pawn can be, in handle order (design 29 §1). <b>Appended, never
        /// inserted</b>: a pawn's kind is saved by this index, so its number is a save contract,
        /// exactly as a job def index or an item handle is. The colonist is 0 and every pawn from
        /// before this table existed reads as 0.
        /// </summary>
        public PawnKindDef[] Kinds = System.Array.Empty<PawnKindDef>();

        /// <summary>Every species, in handle order. Reached through <see cref="SpeciesOf"/>.</summary>
        public SpeciesDef[] Species = System.Array.Empty<SpeciesDef>();

        /// <summary>The species each kind spawns as, by index into <see cref="Species"/>.</summary>
        public int[] KindSpecies = System.Array.Empty<int>();

        /// <summary>
        /// The body each species has (<see cref="SpeciesDef.health"/>), by index into
        /// <see cref="Species"/>, or null for none (design 43 §2). Read through <see cref="HealthOf"/>.
        /// </summary>
        public HealthDef?[] SpeciesHealth = System.Array.Empty<HealthDef?>();

        /// <summary>
        /// The body a pawn of this kind has, or null: its species' <see cref="HealthDef"/>, and
        /// nothing for a content set built in code, which keeps the pool alone as it always did.
        /// </summary>
        public HealthDef? HealthOf(int kind)
        {
            if (Species.Length == 0 || (uint)kind >= (uint)KindSpecies.Length) return null;
            int species = KindSpecies[kind];
            return (uint)species < (uint)SpeciesHealth.Length ? SpeciesHealth[species] : null;
        }

        /// <summary>
        /// The item defs each kind may arrive holding (<see cref="PawnKindDef.weapons"/>), empty
        /// for bare hands. Read through <see cref="ArmsOnSpawn"/> and <see cref="WeaponFor"/>.
        /// </summary>
        public int[][] KindWeapons = System.Array.Empty<int[]>();

        /// <summary>Whether a pawn of this kind is spawned holding anything — false for a content set with no table.</summary>
        public bool ArmsOnSpawn(int kind) =>
            (uint)kind < (uint)KindWeapons.Length && KindWeapons[kind].Length > 0;

        /// <summary>
        /// The item def this one pawn of this kind is spawned holding, or -1.
        ///
        /// <para><b>A pure hash of the pawn, never a draw from the world's random stream</b>
        /// (<c>docs/design/42-bandits.md</c> §3). Drawing would move every roll after it, so a
        /// colony that meets a bandit would diverge from one that does not in ways that have
        /// nothing to do with the bandit. The id is unique per world and the roll seed is the
        /// pawn's own, so two bandits side by side are dealt independently.</para>
        /// </summary>
        public int WeaponFor(int kind, int pawnId, uint rollSeed)
        {
            if (!ArmsOnSpawn(kind)) return -1;
            int[] choices = KindWeapons[kind];
            if (choices.Length == 1) return choices[0];
            unchecked
            {
                uint h = rollSeed ^ 0x7F4A7C15u;
                h ^= (uint)pawnId * 2654435761u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                return choices[(int)(h % (uint)choices.Length)];
            }
        }

        /// <summary>
        /// The traverse mode each kind moves in: its own <see cref="PawnKindDef.traverseMode"/> where
        /// it names one, else its species'. Read through <see cref="ModeOf"/>.
        /// </summary>
        public TraverseMode[] KindMode = System.Array.Empty<TraverseMode>();

        /// <summary>
        /// The traverse mode a pawn of this kind moves in (design 33 §16) — and its species' for a
        /// content set with no table. The one owner of "how does this pawn walk between jobs, and in
        /// every job it chooses for itself"; <see cref="Pawn.OwnMode"/> is how it is asked.
        /// </summary>
        public TraverseMode ModeOf(int kind) =>
            (uint)kind < (uint)KindMode.Length ? KindMode[kind] : SpeciesOf(kind).traverseMode;

        /// <summary>
        /// What each kind came for (<see cref="PawnKindDef.motive"/>), copied out of the Defs into
        /// this record's own array, so a test that wants a kidnapper sets it here and never writes
        /// through a Def every other record shares. Read through <see cref="MotiveOf"/>.
        /// </summary>
        public Motive[] KindMotive = System.Array.Empty<Motive>();

        /// <summary>What a pawn of this kind came for (design 33 §17) — <see cref="Motive.None"/> for a content set with no table.</summary>
        public Motive MotiveOf(int kind) =>
            (uint)kind < (uint)KindMotive.Length ? KindMotive[kind] : Motive.None;

        /// <summary>
        /// The one species a content set built in code has: a person. Content from Defs always
        /// carries a table and never reaches this.
        /// </summary>
        public static readonly SpeciesDef PersonFallback = new SpeciesDef { defName = "Species_Person", person = true };

        /// <summary>The kind by handle, or the colonist's for a content set with no table.</summary>
        public PawnKindDef KindOf(int kind) =>
            Kinds.Length == 0 ? Kind : Kinds[kind];

        /// <summary>The species a kind spawns as, or the person for a content set with no table.</summary>
        public SpeciesDef SpeciesOf(int kind) =>
            Species.Length == 0 ? PersonFallback : Species[KindSpecies[kind]];

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
        /// How long it takes a colonist to stoop, take something off the ground and straighten up
        /// again — 48 ticks, which is 0.8 s at sixty a second.
        ///
        /// <para><b>This used to be nothing at all</b> (owner, 2026-09-16), and the owner reversed
        /// it on 2026-09-17: <i>"when picking up — it happens quickly in a stride — I think there
        /// should be time spent motion down, picking up object and standing up"</i>. The motion was
        /// always drawn: <c>Gesture.Lift</c> is a solved crouch of exactly 0.8 s. What was missing
        /// is that the simulation moved the pawn on in the same tick, so the figure was still
        /// straightening while its pawn walked away — which <see cref="TakeUp"/>'s own note called
        /// the accepted price of keeping the duration out of the simulation. It is no longer
        /// accepted, so the duration is here.</para>
        ///
        /// <para><b>The number is not free taste: it must match the drawn gesture.</b> This is the
        /// same uncomfortable coupling <see cref="JobDef.settleTicks"/> already carries and for the
        /// same reason — the alternative is presentation reaching into job timing. Shorter than the
        /// gesture and the colonist walks off mid-rise, which is the fault being fixed; longer and
        /// it stands finished over the thing it has already picked up.</para>
        ///
        /// <para>It belongs to the colonist rather than to a job, because a lift is a lift: the
        /// haul and the delivery both use it today and a harvest or a butcher's will want the same
        /// number rather than their own.</para>
        /// </summary>
        public int LiftTicks = 48;

        /// <summary>
        /// How far into <see cref="LiftTicks"/> the thing actually changes hands — 24 ticks, the
        /// middle of the drawn gesture's hold.
        ///
        /// <para>Without it the owner's three beats are two: the item would vanish off the ground
        /// either as the colonist began to bend or after it was already upright. <c>Gesture.Lift</c>
        /// holds the hands at the floor between phase 0.4 and 0.55 — 19 to 26 ticks of 48 — and
        /// this is the middle of that window, so the pile shrinks while the hands are on it.</para>
        ///
        /// <para>Clamped into the lift by <c>JobDriver.LiftToil</c> rather than trusted, because a
        /// grasp later than the lift itself would be a thing picked up after the job had moved
        /// on.</para>
        /// </summary>
        public int LiftGraspTicks = 24;

        /// <summary>
        /// How long a drafted colonist with nothing to do stays drafted: 10,000 ticks, four
        /// in-game hours, the reference's figure (a-10). Counted from the draft or the last order,
        /// whichever is later (design 33 §2b).
        /// </summary>
        public int DraftQuietTicks = 10_000;

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
                .Register<RecipeDef>()
                .Register<MoodDef>()
                .Register<MentalBreakDef>()
                .Register<MovementDef>()
                .Register<PawnKindDef>()
                .Register<SpeciesDef>()
                .Register<TemperatureDef>()
                .Register<PawnTuningDef>()
                .Register<CombatDef>()
                .Register<HealthDef>();

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
                "Thought_Catharsis", "Thought_AteMeal", "Thought_SleptOnGround", "Thought_Fell",
                // Appended, never inserted: a thought index rides every saved memory.
                "Thought_SleptCold", "Thought_SleptHot",
                // Friendly fire (design 33 §12).
                "Thought_AttackedByColonist", "Thought_ColonistDied",
                // The kitchen (design 48 §4): what each food is thought of.
                "Thought_AteRation", "Thought_AteBurnt", "Thought_AteRaw");
            content.Jobs = ByName<JobDef>(defs,
                "Job_Haul", "Job_Eat", "Job_Sleep", "Job_Wander", "Job_Wait", "Job_Fell", "Job_Mine",
                "Job_Deliver", "Job_Build", "Job_Deconstruct",
                // Appended, never inserted: a job def index rides every pawn's current job and
                // every save taken with one running, so its number is a save contract.
                "Job_Sow", "Job_Harvest",
                // The draft (design 33 §2c).
                "Job_DraftHold", "Job_Goto",
                // Power (design 32), appended for the same reason.
                "Job_LayConduit", "Job_RemoveConduit", "Job_Refuel",
                // The combat line, claimed together by its contracts step (design 33 §5).
                "Job_AttackMelee", "Job_Flee", "Job_Downed", "Job_Equip", "Job_Rescue",
                // A bandit carrying something off the board (design 33 §17).
                "Job_Steal",
                // Medical supplies (design 37).
                "Job_Treat", "Job_Patient",
                // Picking a berry bush (design 45 §6), appended after medical supplies.
                "Job_Forage",
                // The kitchen (design 48 §5).
                "Job_Cook",
                // The ranged attack (design 47 §2d).
                "Job_AttackRanged");
            content.WorkTypes = ByName<WorkTypeDef>(defs,
                "Work_Haul", "Work_Cutting", "Work_Mining", "Work_Construction",
                "Work_Growing",
                // Appended with the combat line (design 33 §5): a pawn's priority array is indexed
                // by this order, so it is a save contract like the rest.
                "Work_Rescue",
                // Medical supplies (design 37).
                "Work_Doctor",
                // The kitchen (design 48 §5).
                "Work_Cooking");
            content.Skills = ByName<SkillDef>(defs,
                "Skill_Hauling", "Skill_Cutting", "Skill_Mining", "Skill_Construction",
                "Skill_Growing",
                // Appended with the combat line (design 33 §5).
                "Skill_Melee",
                // Medical supplies (design 37).
                "Skill_Medicine",
                // The kitchen (design 48 §5).
                "Skill_Cooking",
                // Appended with the ranged line (design 47 §3a).
                "Skill_Shooting");
            content.Items = ByName<ItemDef>(defs,
                "Item_Meal", "Item_Salvage", "Item_Wood", "Item_Stone", "Item_IronOre", "Item_Coal",
                // Appended, never inserted: an item handle is stored in every stack, every haul
                // job and every stockpile's allow list, so its number is a save contract
                // (docs/design/22-growing.md §2).
                "Item_Carrots",
                // The four melee weapons (design 33 §1, C3), appended together.
                "Item_Bat", "Item_Crowbar", "Item_Machete", "Item_ArcBlade",
                // What a doctor treats with (design 37), appended.
                "Item_MedicalSupplies",
                // The wild foods (design 45 §6), appended together after medical supplies.
                "Item_Berries", "Item_Mushrooms",
                // The kitchen (design 48 §4), appended: the two meals and the burnt one.
                "Item_CookedMeal", "Item_VegetableMeal", "Item_BurntMeal",
                // The pistol (design 47), the first ranged weapon.
                "Item_Pistol");
            content.Recipes = ByName<RecipeDef>(defs, "Recipe_Meal");
            for (int r = 0; r < content.Recipes.Length; r++)
            {
                RecipeDef recipe = content.Recipes[r];
                recipe.ProductItem = ItemNamed(content, recipe.product, recipe.defName);
                recipe.ProductNoMeatItem = ItemNamed(content, recipe.productNoMeat, recipe.defName);
                recipe.BurntItem = ItemNamed(content, recipe.burntProduct, recipe.defName);
            }

            content.Mood = One<MoodDef>(defs, "Mood_Default");
            content.Break = One<MentalBreakDef>(defs, "Break_Wander");
            content.Movement = One<MovementDef>(defs, "Movement_Colonist");
            content.Kind = One<PawnKindDef>(defs, "PawnKind_Colonist");
            content.Temperature = One<TemperatureDef>(defs, "Temperature_Colonist");

            // Kinds and species (design 29 §1). Appended, never inserted: a pawn's kind is saved
            // as this index. The colonist is 0 so that every pawn from before the table reads
            // as what it was.
            content.Kinds = ByName<PawnKindDef>(defs,
                "PawnKind_Colonist", "PawnKind_MiddenHog", "PawnKind_DuctRat",
                // The debug-spawned hostile person (design 33 §1), appended.
                "PawnKind_Bandit",
                // The bandit with a pistol, a raid's second kind (design 55 §8), appended.
                "PawnKind_Gunman",
                // The frog of the banks (design 30 §8), appended after the gunman.
                "PawnKind_CulvertFrog");
            content.Species = ByName<SpeciesDef>(defs,
                "Species_Person", "Species_MiddenHog", "Species_DuctRat", "Species_CulvertFrog");
            content.KindSpecies = new int[content.Kinds.Length];
            for (int k = 0; k < content.Kinds.Length; k++)
            {
                string wanted = content.Kinds[k].species;
                int found = -1;
                for (int s = 0; s < content.Species.Length; s++)
                    if (content.Species[s].defName == wanted) { found = s; break; }
                if (found < 0)
                    throw new DefLoadException(
                        $"PawnKindDef '{content.Kinds[k].defName}' names species '{wanted}', which the content does not have.");
                content.KindSpecies[k] = found;
            }

            // The body each species has (design 43 §2), by name, once. A body of more regions
            // than a ledger can hold fails the load rather than a fight.
            content.SpeciesHealth = new HealthDef?[content.Species.Length];
            for (int s = 0; s < content.Species.Length; s++)
            {
                string wanted = content.Species[s].health;
                if (string.IsNullOrEmpty(wanted)) continue;
                HealthDef body = One<HealthDef>(defs, wanted);
                if (body.regions.Count == 0 || body.regions.Count > PawnHealth.MaxRecords / 3)
                    throw new DefLoadException(
                        $"HealthDef '{body.defName}' has {body.regions.Count} regions; a body has one to six.");
                content.SpeciesHealth[s] = body;
            }

            // The weapon a kind arrives holding (design 33 §1), by name, once — after the items,
            // which this reads. A name the content does not have, or an item that is not a weapon,
            // fails the load rather than a spawn.
            content.KindWeapons = new int[content.Kinds.Length][];
            for (int k = 0; k < content.Kinds.Length; k++)
            {
                string[] wanted = content.Kinds[k].weapons ?? System.Array.Empty<string>();
                var resolved = new int[wanted.Length];
                for (int w = 0; w < wanted.Length; w++)
                {
                    resolved[w] = -1;
                    for (int i = 0; i < content.Items.Length; i++)
                        if (content.Items[i].defName == wanted[w]) { resolved[w] = i; break; }
                    if (resolved[w] < 0)
                        throw new DefLoadException(
                            $"PawnKindDef '{content.Kinds[k].defName}' names weapon '{wanted[w]}', which the content does not have.");
                    if (content.Items[resolved[w]].weapon == null)
                        throw new DefLoadException(
                            $"PawnKindDef '{content.Kinds[k].defName}' names weapon '{wanted[w]}', which has no weapon block.");
                }
                content.KindWeapons[k] = resolved;
            }
            // How each kind moves (design 33 §16): its own mode by name, or its species'. After the
            // species, which this reads. A name the enum does not have fails the load.
            content.KindMode = new TraverseMode[content.Kinds.Length];
            for (int k = 0; k < content.Kinds.Length; k++)
            {
                string wanted = content.Kinds[k].traverseMode;
                if (string.IsNullOrEmpty(wanted))
                {
                    content.KindMode[k] = content.Species[content.KindSpecies[k]].traverseMode;
                    continue;
                }
                if (!System.Enum.TryParse(wanted, ignoreCase: false, out TraverseMode mode)
                    || !System.Enum.IsDefined(typeof(TraverseMode), mode))
                    throw new DefLoadException(
                        $"PawnKindDef '{content.Kinds[k].defName}' names traverse mode '{wanted}', which is not one.");
                content.KindMode[k] = mode;
            }

            // What each kind came for (design 33 §17), into the record's own array.
            content.KindMotive = new Motive[content.Kinds.Length];
            for (int k = 0; k < content.Kinds.Length; k++) content.KindMotive[k] = content.Kinds[k].motive;

            if (!content.SpeciesOf(0).person)
                throw new DefLoadException("kind 0 must be a person: it is what every pawn from before the kind table reads as.");

            var tuning = One<PawnTuningDef>(defs, "Tuning_Pawns");
            content.NeedsIntervalTicks = tuning.needsIntervalTicks;
            content.DayTicks = tuning.dayTicks;
            content.ThinkLoopLimit = tuning.thinkLoopLimit;
            content.ThinkLoopWindowTicks = tuning.thinkLoopWindowTicks;
            content.StandDownTicks = tuning.standDownTicks;
            content.StonePerRock = tuning.stonePerRock;
            content.StoneChanceOneIn = tuning.stoneChanceOneIn;
            content.OrePerCell = tuning.orePerCell;
            content.LiftTicks = tuning.liftTicks;
            content.LiftGraspTicks = tuning.liftGraspTicks;
            content.DraftQuietTicks = tuning.draftQuietTicks;

            content.Combat = One<CombatDef>(defs, "Combat_Default");

            return content;
        }

        /// <summary>The index of the item def named <paramref name="name"/>, or a failed load naming who asked.</summary>
        static int ItemNamed(PawnContent content, string name, string askedBy)
        {
            for (int i = 0; i < content.Items.Length; i++)
                if (content.Items[i].defName == name) return i;
            throw new DefLoadException($"'{askedBy}' names item '{name}', which the content does not have.");
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
        /// An animal deciding between a leg and a rest, and how long the rest is (design 29 §3).
        /// Its own stream, so an animal thinking on a tick cannot shift what a colonist on the
        /// same tick wanders to. The salt is not one already in this list; two purposes sharing a
        /// salt is two streams that agree.
        /// </summary>
        public const uint AnimalMind = 0x165667B1;

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
        /// Whether a completed build succeeds or botches (U26). Drawn from (world seed,
        /// <b>cell index ^ tick</b>) for the same reason <see cref="DeconstructRefund"/> mixes the
        /// tick in: success keyed on the cell alone would make every cell on the board permanently
        /// a lucky one or an unlucky one — stable, discoverable, and then worth farming by
        /// demolishing and re-ordering on the good cells. Keyed on both, it replays identically
        /// from a seed, which is all determinism asks.
        /// </summary>
        public const uint BuildSuccess = 0xCC9E_2D51;

        /// <summary>
        /// The odd unit when a botched build keeps half of an odd delivery. Beside
        /// <see cref="BuildSuccess"/> rather than sharing its salt, because two draws from one
        /// stream are two answers the arithmetic has tied together. Both are MurmurHash3
        /// constants — a different family from the xxHash primes above, for the same reason
        /// <see cref="StartingSkill"/> stepped outside them.
        /// </summary>
        public const uint BuildBotchLoss = 0x1B87_3593;

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
        /// <see cref="StartingSkill"/> records: all five primes are spent. It read "the third
        /// constant to step outside it" when the bed landed; the success roll merged in beside it
        /// with two more, so it is now the <b>fourth</b> — <see cref="StartingSkill"/>,
        /// <see cref="BuildSuccess"/>, <see cref="BuildBotchLoss"/>, this.</para>
        /// </summary>
        public const uint BuildQuality = 0x7F4A_7C15;

        /// <summary>
        /// A colonist's innate walking pace (design 17 §4b). Drawn from (world seed, pawn id),
        /// the same shape as <see cref="Passion"/> and <see cref="StartingSkill"/> and for the
        /// same reason: a seed deals the same people every load, a pace is a fact about the
        /// person and not about the moment, and sharing a salt with another pawn roll would tie
        /// one colonist's walk to another colonist's temper by coincidence of arithmetic.
        ///
        /// <para>The fifth constant outside the spent xxHash family: SHA-256's first round
        /// constant, reached for the same reason the MurmurHash3 constants were — the primes are
        /// gone and distinct families read as the discipline they are.</para>
        /// </summary>
        public const uint MovePace = 0x428A_2F98;

        /// <summary>
        /// Whether an idle colonist heads for a fire or wanders (design 33).
        ///
        /// <para>Its own salt, and the reason is the one this file states twice: two purposes
        /// sharing a salt is two streams that agree. Drawn on the same tick with the same pawn
        /// id as <see cref="Wander"/>, a shared salt would make the fireside roll and the
        /// wander's first coordinate the <i>same number</i> — so whether she went to the fire
        /// and which way she would otherwise have drifted would be one decision wearing two
        /// names. Nothing would ever report that; it would just look slightly wrong for ever.</para>
        /// </summary>
        public const uint Fireside = 0x846C_A68B;

        // ---- combat (design 33 §3) -------------------------------------------------------------
        //
        // Claimed by the combat contracts step so that the two lanes that roll dice in a fight
        // cannot pick the same salt on two branches — the fault StartingSkill and
        // DeconstructRefund once had. SHA-256's round constants, continuing where MovePace (the
        // first) and the two incident salts (the second and third) left off, so no value here is
        // one already in use anywhere in the simulation.
        //
        // AnimalMind, above, is 0x165667B1 — the same value as DeconstructRefund. That collision
        // predates combat and is recorded, not fixed here: changing either moves a golden, and
        // the two streams are keyed differently (a pawn id against a cell) so they rarely meet.

        /// <summary>Whether a swing lands, on the attacker's hit curve. Drawn from (seed, tick, attacker id).</summary>
        public const uint MeleeHit = 0xE9B5_DBA5;

        /// <summary>Whether a landed swing is dodged, on the defender's dodge curve.</summary>
        public const uint MeleeDodge = 0x3956_C25B;

        /// <summary>How hard it lands, within <see cref="CombatDef.damageSpreadPerMille"/> of the weapon's figure.</summary>
        public const uint MeleeDamage = 0x59F1_11F1;

        /// <summary>Whether a hurt animal turns on its attacker or runs (<see cref="SpeciesDef.revengePerMille"/>).</summary>
        public const uint Revenge = 0x923F_82A4;

        /// <summary>Whether a blunt blow stuns (<see cref="AttackDef.stunPerMille"/>).</summary>
        public const uint Stun = 0xAB1C_5ED5;

        /// <summary>
        /// Whether a landing blow is critical (<see cref="CombatDef.critChancePerMille"/>, design 33
        /// §9b). SHA-256's ninth round constant, next after <see cref="Stun"/>.
        /// </summary>
        public const uint MeleeCritical = 0xD807_AA98;

        /// <summary>The debug menu's "Arm every colonist" (design 33 §9i): which weapon each colonist is dealt.</summary>
        public const uint DebugArm = 0x243F_6A88;

        /// <summary>
        /// Whether a critical blow knocks its target back (<see cref="CombatDef.knockbackPerMille"/>).
        /// SHA-256's tenth round constant.
        /// </summary>
        public const uint Knockback = 0x1283_5B01;

        /// <summary>
        /// Whether a jump over a stream falls short (design 46 §6). SHA-256's eleventh round
        /// constant.
        /// </summary>
        public const uint Jump = 0x2431_85BE;

        /// <summary>
        /// Whether the meal in a pan will come out burnt (design 48 §5), rolled once when the
        /// cooking starts. SHA-256's twelfth round constant.
        /// </summary>
        public const uint Burn = 0x550C_7DC3;
        // The ranged line's four (design 47 §3a). SHA-256's thirteenth, fourteenth, seventeenth
        // and eighteenth round constants: the eleventh is taken by the stream jump, the twelfth
        // is cooking's Burn, and the fifteenth and sixteenth by the weather.

        /// <summary>Whether a shot hits (design 47 §2a).</summary>
        public const uint RangedHit = 0x72BE_5D74;

        /// <summary>How hard a bullet strikes, within the spread — rolled for a miss too, because a stray still carries its weight.</summary>
        public const uint RangedDamage = 0x80DE_B1FE;

        /// <summary>Where a miss goes: the scatter cell round the target (design 47 §2c).</summary>
        public const uint RangedScatter = 0xE49B_69C1;

        /// <summary>Whether a bystander on the line takes the bullet, salted by the cell as well as the shooter.</summary>
        public const uint RangedIntercept = 0xEFBE_4786;

        /// <summary>
        /// A weapon's quality when it is made (design 47 §11), salted by the thing's id. SHA-256's
        /// nineteenth round constant.
        /// </summary>
        public const uint WeaponQuality = 0x0FC1_9DC6;

        // ---- health (design 43) ----------------------------------------------------------------
        // Built as the eleventh and twelfth, moved to the twelfth and thirteenth when the stream
        // jump shipped first, and moved again when cooking (Burn) and ranged (RangedHit) shipped
        // with those: two purposes on one stream would let a cooking or shooting roll decide
        // where a blow lands. The twentieth and twenty-first are free; cover took the next three.

        /// <summary>
        /// Which region a hit lands on, by coverage (design 43 §2). SHA-256's twentieth round
        /// constant. Its own stream, so the body can move no roll a fight made before it existed.
        /// </summary>
        public const uint HitRegion = 0x240C_A1CC;

        /// <summary>How a fall's damage is split into hits and spread (design 43 §7). The twenty-first.</summary>
        public const uint FallSplit = 0x2DE9_2C6F;

        // Cover's three (design 53 §2d, §2e). SHA-256's twenty-second, twenty-third and
        // twenty-fourth round constants. Built on the twentieth to the twenty-second, moved when
        // health (design 43) shipped first on the twentieth and twenty-first.

        /// <summary>Whether cover defeats a shot whose aim roll hit (design 53 §2d).</summary>
        public const uint RangedCover = 0x4A74_84AA;

        /// <summary>Which piece of cover a defeated shot is fired into, weighted by what each gave.</summary>
        public const uint RangedCoverPick = 0x5CB0_A9DC;

        /// <summary>Whether a stray crossing a cover cell is caught by it, salted by the cell as well as the shooter.</summary>
        public const uint RangedCoverIntercept = 0x76F9_88DA;
    }
}
