#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The interface's clock: the same tick-to-calendar mapping, read through
    /// <see cref="Odyssey.Sim.Contracts.Calendar"/> rather than held here.
    ///
    /// <para><b>The mapping moved down, and this is the second home rather than a new one.</b>
    /// Until temperature (design 28) the interface was the calendar's only reader, so the mapping
    /// lived in the Hud assembly where the sim could not reach it — correctly, since Sim must not
    /// reference Hud. A system that models the season broke that arrangement the only way it
    /// could: the calendar is <see cref="Calendar"/> in the contracts assembly now, both sides
    /// read the same functions, and this class keeps the interface's name so no caller or test
    /// moves. The doc comment the mapping used to carry is the one on <c>Calendar</c>.</para>
    /// </summary>
    public static class GameClock
    {
        public const int TicksPerHour = Calendar.TicksPerHour;
        public const int HoursPerDay = Calendar.HoursPerDay;
        public const int DaysPerMonth = Calendar.DaysPerMonth;
        public const int TicksPerDay = Calendar.TicksPerDay;
        public const int TicksPerMonth = Calendar.TicksPerMonth;

        public static int DayOfMonthsStart(long tick) => Calendar.DayOfMonthsStart(tick);
        public static int HourOfDay(long tick) => Calendar.HourOfDay(tick);
        public static int MonthOfYear(long tick) => Calendar.MonthOfYear(tick);
        public static string MonthName(long tick) => Calendar.MonthName(tick);
        public static string SeasonName(long tick) => Calendar.SeasonName(tick);
        public static int DayOfMonth(long tick) => Calendar.DayOfMonth(tick);
        public static string Describe(long tick) => Calendar.Describe(tick);
    }
}
