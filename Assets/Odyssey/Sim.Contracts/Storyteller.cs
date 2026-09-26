#nullable enable
namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// Storyteller indices (design 68), as <see cref="Intent"/> and <see cref="StorytellerView"/>
    /// carry them. The order is <c>StorytellerContent.Order</c> in the simulation and
    /// <c>StoryCatalogue.Tellers</c> in the interface, and a test on each side holds its list to
    /// this table.
    /// </summary>
    public static class StorytellerHandle
    {
        /// <summary>No storyteller: nothing fires but the debug menu. A save from before the
        /// storyteller loads with this (design 68 §7).</summary>
        public const int None = -1;

        /// <summary>The llama who keeps the calendar: threats on a cycle you can learn.</summary>
        public const int Jacob = 0;

        /// <summary>Half man, half salvage: a random bag with a drought rule.</summary>
        public const int Trent = 1;

        /// <summary>The vast, sleepy pig: a long quiet cycle, one hard test.</summary>
        public const int Kano = 2;

        public const int Count = 3;
    }

    /// <summary>What last moved the tension (design 68 §5). The interface's <c>TensionCause</c> is
    /// the same four, in the same order.</summary>
    public enum TensionCauseKind
    {
        None = 0,
        Died = 1,
        Downed = 2,
        Quiet = 3,
    }

    /// <summary>
    /// The storyteller as the interface reads it (design 68 §5a, §7): who, how hard, and the
    /// tension's band and last cause. <b>No tension number</b>: the band is decided here, in the
    /// simulation, and the interface draws it (owner, 2026-09-26: bands and a cause, not the
    /// multiplier).
    /// </summary>
    public readonly struct StorytellerView
    {
        public StorytellerView(int storyteller, int rung, int threatPercent, bool bigThreats,
            int adaptationPercent, int graceHundredths, int band, TensionCauseKind cause, int causeDays)
        {
            Storyteller = storyteller;
            Rung = rung;
            ThreatPercent = threatPercent;
            BigThreats = bigThreats;
            AdaptationPercent = adaptationPercent;
            GraceHundredths = graceHundredths;
            Band = band;
            Cause = cause;
            CauseDays = causeDays;
        }

        /// <summary>A <see cref="StorytellerHandle"/>, or <see cref="StorytellerHandle.None"/>.</summary>
        public readonly int Storyteller;

        /// <summary>The difficulty rung the levers came from, as the interface's ladder numbers it.</summary>
        public readonly int Rung;

        public readonly int ThreatPercent;
        public readonly bool BigThreats;
        public readonly int AdaptationPercent;

        /// <summary>The grace stretch in hundredths: 100 is the storyteller's own.</summary>
        public readonly int GraceHundredths;

        /// <summary>The tension band, 0 (reeling) to 4 (peak).</summary>
        public readonly int Band;

        public readonly TensionCauseKind Cause;

        /// <summary>Days since the cause, or for <see cref="TensionCauseKind.Quiet"/> how many quiet days.</summary>
        public readonly int CauseDays;

        public bool HasStoryteller => Storyteller >= 0;

        /// <summary>No storyteller: what an old save, a headless test and a world with none publish.</summary>
        public static StorytellerView None =>
            new StorytellerView(StorytellerHandle.None, 3, 100, true, 100, 100, 2, TensionCauseKind.None, 0);
    }
}
