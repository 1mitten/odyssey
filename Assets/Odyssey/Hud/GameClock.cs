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

        /// <summary>
        /// The clock's date, "Day 12 · Larkspur": the day and the month, and <b>not the season</b>
        /// (design 59 §12, mockup 25g). With the season the longest date measured about 150 px and
        /// the line about 297 against the 247 the 271-wide panel holds, so the tension gauge could
        /// not join it without a second line. The season lives in the weather glyph's tooltip and
        /// in the date's own (<see cref="FullDate"/>).
        /// </summary>
        public static string DateLine(long tick) => "Day " + DayOfMonth(tick) + " · " + MonthName(tick);

        /// <summary>"Day 12 · Larkspur · Wash": the whole date, for the date's tooltip.</summary>
        public static string FullDate(long tick) => DateLine(tick) + " · " + SeasonName(tick);

        /// <summary>"Rain · Wash": the weather glyph's tooltip, now carrying the season too; the
        /// season alone when the sky has no word.</summary>
        public static string WeatherTip(string weatherWord, long tick) =>
            weatherWord.Length == 0 ? SeasonName(tick) : weatherWord + " · " + SeasonName(tick);
    }
}
