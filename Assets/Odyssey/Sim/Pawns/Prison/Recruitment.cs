#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Why a prisoner is being talked round slowly, as bits (design 59 §8). Published as one number
    /// so the prisoner's pane can say it in words.
    /// </summary>
    [System.Flags]
    public enum RecruitBlockers
    {
        None = 0,
        /// <summary>Nobody in the colony has Warden work switched on, so nobody will talk to her.</summary>
        NoWarden = 1 << 0,
        Hungry = 1 << 1,
        /// <summary>An injury nobody has tended.</summary>
        Untended = 1 << 2,
        Shackled = 1 << 3,
        LowMood = 1 << 4,
        /// <summary>The best warden's Social is low.</summary>
        LowSocial = 1 << 5,
    }

    /// <summary>What one chat would come to, and why: the arithmetic and its reasons together.</summary>
    public readonly struct RecruitFactors
    {
        public readonly int Social, Mood, Treatment, Gain;
        public readonly RecruitBlockers Blockers;

        public RecruitFactors(int social, int mood, int treatment, int gain, RecruitBlockers blockers)
        {
            Social = social;
            Mood = mood;
            Treatment = treatment;
            Gain = gain;
            Blockers = blockers;
        }
    }

    /// <summary>
    /// <b>The one owner of how fast a prisoner is talked round</b> (design 59 §8). The chat driver
    /// and the pane's readout both call <see cref="Factors"/>, so the ETA a player reads is the
    /// arithmetic that runs — no hidden roll, and no second copy to drift.
    ///
    /// <code>
    /// gain = 50,000 × S × M × T / 1000³
    /// S = 400 + 120 × Social level
    /// M = max(250, 250 + 1.5 × mood)
    /// T = fed × tended × cell / 1000²
    /// </code>
    /// Every number is a proposal the owner confirms at the first play.
    /// </summary>
    public static class Recruitment
    {
        /// <summary>A full bar: she joins.</summary>
        public const int Full = 1_000_000;

        /// <summary>A warden talks to her once every six game hours: four chats a day.</summary>
        public const int ChatIntervalTicks = 6 * Calendar.TicksPerHour;

        const long Base = 50_000;

        /// <summary>Mood below this slows her and is named as a reason.</summary>
        public const int LowMoodBelow = 300;

        /// <summary>A best warden below this Social level is named as a reason.</summary>
        public const int LowSocialBelow = 3;

        /// <summary>
        /// What a chat by <paramref name="warden"/> would add to <paramref name="prisoner"/>'s bar
        /// now, and what is holding it back. A null warden is the best one the colony has, or none.
        /// </summary>
        public static RecruitFactors Factors(Pawn prisoner, Pawn? warden, PawnContext ctx)
        {
            RecruitBlockers blockers = RecruitBlockers.None;
            if (warden == null) blockers |= RecruitBlockers.NoWarden;

            int level = warden != null ? warden.SkillLevel(SkillIndex.Social) : 0;
            if (warden != null && level < LowSocialBelow) blockers |= RecruitBlockers.LowSocial;
            int s = 400 + 120 * level;

            int m = System.Math.Max(250, 250 + prisoner.Mood * 3 / 2);
            if (prisoner.Mood < LowMoodBelow) blockers |= RecruitBlockers.LowMood;

            bool hungry = prisoner.Needs[NeedIndex.Food] < ctx.Content.Needs[NeedIndex.Food].seekThreshold;
            if (hungry) blockers |= RecruitBlockers.Hungry;
            bool untended = prisoner.HasHealthState && prisoner.Health!.UntendedCount > 0;
            if (untended) blockers |= RecruitBlockers.Untended;
            bool shackled = PrisonerTrees.IsShackled(prisoner, ctx);
            if (shackled) blockers |= RecruitBlockers.Shackled;

            int cell = shackled ? 600 : 1000 + 50 * System.Math.Max(0, BedQuality(prisoner, ctx) - QualityHandle.Normal);
            long t = (long)(hungry ? 500 : 1000) * (untended ? 600 : 1000) * cell / 1_000_000;

            long gain = Base * s * m * t / 1_000_000_000L;
            return new RecruitFactors(s, m, (int)t, (int)System.Math.Max(1, gain), blockers);
        }

        /// <summary>The standing colonist with Warden work switched on and the most Social, or null.</summary>
        public static Pawn? BestWarden(PawnContext ctx)
        {
            Pawn? best = null;
            int bestLevel = -1;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!pawn.IsColonist || pawn.Downed || pawn.WorkPriorities[WorkTypeIndex.Warden] == 0) continue;
                int level = pawn.SkillLevel(SkillIndex.Social);
                if (level <= bestLevel) continue;
                best = pawn;
                bestLevel = level;
            }
            return best;
        }

        /// <summary>
        /// Game hours until she joins at the best warden's pace, rounded up to whole chats; -1 when
        /// nobody will talk to her. The number the pane says, from the arithmetic the chat runs.
        /// </summary>
        public static int HoursToJoin(Pawn prisoner, PawnContext ctx) => HoursToJoin(prisoner, ctx, BestWarden(ctx));

        /// <summary><see cref="HoursToJoin(Pawn, PawnContext)"/> with the best warden already found.</summary>
        public static int HoursToJoin(Pawn prisoner, PawnContext ctx, Pawn? warden)
        {
            if (warden == null || prisoner.Prison == null) return -1;
            RecruitFactors f = Factors(prisoner, warden, ctx);
            long remaining = Full - prisoner.Prison.Willingness;
            if (remaining <= 0) return 0;
            long chats = (remaining + f.Gain - 1) / f.Gain;
            return (int)System.Math.Min(int.MaxValue, chats * ChatIntervalTicks / Calendar.TicksPerHour);
        }

        /// <summary>Whether a warden is due to talk to her: in Recruit mode, held, and not talked to in the last six hours.</summary>
        public static bool Due(Pawn prisoner, int tick) =>
            prisoner.Custody == PawnCustody.Prisoner && prisoner.Prison != null
            && prisoner.Prison.Mode == PrisonMode.Recruit
            && (prisoner.Prison.LastChatTick == 0 || tick - prisoner.Prison.LastChatTick >= ChatIntervalTicks);

        /// <summary>
        /// A chat by <paramref name="warden"/> has ended: the bar fills by the factors' gain, and she
        /// joins if it is full. Returns whether she joined.
        /// </summary>
        public static bool Chat(Pawn prisoner, Pawn warden, PawnContext ctx)
        {
            PrisonRecord record = prisoner.Prison ??= new PrisonRecord();
            RecruitFactors f = Factors(prisoner, warden, ctx);
            record.Willingness = (int)System.Math.Min(Full, (long)record.Willingness + f.Gain);
            record.LastChatTick = System.Math.Max(1, ctx.CurrentTick);
            if (record.Willingness < Full) return false;
            Join(prisoner, ctx);
            return true;
        }

        /// <summary>
        /// She joins the colony (design 59 §8): free, her side the colony's for good, her kind
        /// unchanged. A raider is dealt the passions and skills a raid never gave her — from the
        /// streams a new colonist of her seed would use, keeping anything she has trained — and
        /// every work priority at 3. Her prison bed goes back, her jumpsuit comes off, and the
        /// colony is told.
        /// </summary>
        public static void Join(Pawn prisoner, PawnContext ctx)
        {
            PrisonRecord record = prisoner.Prison ??= new PrisonRecord();
            bool arrested = record.Arrested;
            ctx.Combat?.Jobs.EndJob(prisoner, JobStatus.Failed);
            ctx.Construction?.ReleaseBedsOf(prisoner.Id.Value);

            prisoner.Custody = PawnCustody.Free;
            record.Joined = true;
            record.Dressed = false;
            record.Arrested = false;
            record.CaptureMark = false;
            record.Mode = PrisonMode.Hold;
            record.Willingness = 0;
            record.LastChatTick = 0;

            if (!arrested)
            {
                prisoner.RollPassions();
                prisoner.RollStartingSkills();
                for (int w = 0; w < prisoner.WorkPriorities.Length; w++) prisoner.WorkPriorities[w] = 3;
            }
            ctx.Incidents?.Ledger.Record(IncidentHandle.Recruited, prisoner.Cell, ctx.CurrentTick);
        }

        static int BedQuality(Pawn prisoner, PawnContext ctx)
        {
            int bed = PrisonerTrees.OwnBed(prisoner, ctx);
            return bed >= 0 && ctx.Construction != null ? ctx.Construction.BedQualityAt(bed) : QualityHandle.Normal;
        }
    }
}
