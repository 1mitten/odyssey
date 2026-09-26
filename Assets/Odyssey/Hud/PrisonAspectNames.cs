using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The prison's pawn aspects as the interface reads them (design 59 §11): the simulation's
    /// <c>PrisonAspects</c> names, copied because this assembly cannot see that one, and held to
    /// agree by spelling.
    /// </summary>
    public static class PrisonAspectNames
    {
        /// <summary>Present on a downed pawn waiting for a prison bed when none is free.</summary>
        public const string NoBed = "odyssey.pawn.prison.nobed";

        /// <summary>Present on a person marked for capture and not yet taken.</summary>
        public const string CaptureMark = "odyssey.pawn.prison.capture";

        /// <summary>A held prisoner's <c>PrisonMode</c>.</summary>
        public const string Mode = "odyssey.pawn.prison.mode";

        /// <summary>How far talked round, in thousandths of a full bar.</summary>
        public const string Willing = "odyssey.pawn.prison.willing";

        /// <summary>Game hours until she joins; -1 when nobody will talk to her. Recruit mode only.</summary>
        public const string Hours = "odyssey.pawn.prison.hours";

        /// <summary>Why she is talked round slowly, as bits: see <see cref="Blocker"/>.</summary>
        public const string Blockers = "odyssey.pawn.prison.blockers";

        /// <summary>Present while her bed is a shackle bed.</summary>
        public const string Shackled = "odyssey.pawn.prison.shackled";

        /// <summary>Her chance of breaking out in a day, in parts per million.</summary>
        public const string Escape = "odyssey.pawn.prison.escape";

        /// <summary>Why her escape risk is what it is, as bits: see <see cref="Reason"/>.</summary>
        public const string EscapeWhy = "odyssey.pawn.prison.escapewhy";

        /// <summary>Present on a downed prisoner out of her prison bed, to be carried back to it.</summary>
        public const string Stray = "odyssey.pawn.prison.stray";

        public static readonly AspectKey NoBedKey = AspectKey.Of(NoBed);
        public static readonly AspectKey StrayKey = AspectKey.Of(Stray);
        public static readonly AspectKey EscapeKey = AspectKey.Of(Escape);
        public static readonly AspectKey EscapeWhyKey = AspectKey.Of(EscapeWhy);
        public static readonly AspectKey CaptureMarkKey = AspectKey.Of(CaptureMark);
        public static readonly AspectKey ModeKey = AspectKey.Of(Mode);
        public static readonly AspectKey WillingKey = AspectKey.Of(Willing);
        public static readonly AspectKey HoursKey = AspectKey.Of(Hours);
        public static readonly AspectKey BlockersKey = AspectKey.Of(Blockers);
        public static readonly AspectKey ShackledKey = AspectKey.Of(Shackled);

        /// <summary>The bits of <see cref="EscapeWhy"/>, the simulation's <c>EscapeReasons</c> by value.</summary>
        public static class Reason
        {
            public const int Miserable = 1 << 0, Unhappy = 1 << 1, Content = 1 << 2, DoorOpen = 1 << 3, Shackled = 1 << 4,
                Unwatched = 1 << 5, Unhurt = 1 << 6, Hurt = 1 << 7, WellKept = 1 << 8;
        }

        /// <summary>The bits of <see cref="Blockers"/>, the simulation's <c>RecruitBlockers</c> by value.</summary>
        public static class Blocker
        {
            public const int NoWarden = 1 << 0, Hungry = 1 << 1, Untended = 1 << 2, Shackled = 1 << 3,
                LowMood = 1 << 4, LowSocial = 1 << 5;
        }
    }
}
