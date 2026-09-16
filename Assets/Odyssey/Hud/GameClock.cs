#nullable enable
namespace Odyssey.Hud
{
    /// <summary>
    /// Reads the simulation tick as a calendar, per the structure proposed in
    /// <c>docs/design/proper-nouns.csv</c>: 24-hour days, 12 days a month, six months of
    /// botanical names in three seasons (Wash, Glare, Rime). A 72-day year puts a full seasonal
    /// cycle inside a prototype's play time, which is the point of a short year.
    ///
    /// The simulation contains no clock: it counts ticks and nothing else, so this class is the
    /// only place the tick-to-time mapping lives and the interface never disagrees with itself
    /// about what time it is. 2,500 ticks an hour follows the genre convention; at 60 ticks a
    /// second and speed 1 a day passes in about seventeen real minutes.
    /// </summary>
    public static class GameClock
    {
        public const int TicksPerHour = 2_500;
        public const int HoursPerDay = 24;
        public const int DaysPerMonth = 12;

        public const int TicksPerDay = TicksPerHour * HoursPerDay;
        public const int TicksPerMonth = TicksPerDay * DaysPerMonth;

        /// <summary>Two months a season, six months a year, in calendar order.</summary>
        public static readonly string[] Months =
            { "Larkspur", "Tansy", "Bramble", "Ember", "Hollow", "Candle" };

        /// <summary>The season each month belongs to, parallel to <see cref="Months"/>.</summary>
        public static readonly string[] SeasonOfMonth =
            { "Wash", "Wash", "Glare", "Glare", "Rime", "Rime" };

        /// <summary>Days are counted from one: the first day of the settlement is day 1.</summary>
        public static int DayOfMonthsStart(long tick) => (int)(tick / TicksPerDay) + 1;

        /// <summary>The hour the tick falls in, 0 to 23.</summary>
        public static int HourOfDay(long tick) => (int)(tick % TicksPerDay / TicksPerHour);

        /// <summary>Zero-based month of the 72-day year.</summary>
        public static int MonthOfYear(long tick) => (int)(tick / TicksPerMonth) % Months.Length;

        public static string MonthName(long tick) => Months[MonthOfYear(tick)];
        public static string SeasonName(long tick) => SeasonOfMonth[MonthOfYear(tick)];

        /// <summary>Day within the current month, 1 to 12, which is how the clock reads it.</summary>
        public static int DayOfMonth(long tick) =>
            (int)(tick % TicksPerMonth / TicksPerDay) + 1;

        /// <summary>"07h · Day 3 · Larkspur · Wash": one line, hour-prefixed, the way the
        /// readout is specified in the panel catalogue (A3). Built here rather than in the view
        /// so the format is testable and localisation has one place to land.</summary>
        public static string Describe(long tick)
        {
            int day = DayOfMonth(tick);
            int month = MonthOfYear(tick);
            return $"{HourOfDay(tick):00}h · Day {day} · {Months[month]} · {SeasonOfMonth[month]}";
        }
    }
}
