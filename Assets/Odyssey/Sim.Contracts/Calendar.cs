#nullable enable
using System;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// The tick-to-calendar mapping: 2,500 ticks an hour, 24 hours a day, 12 days a month, six
    /// months in three seasons (Wash, Glare, Rime) — a 72-day year, per
    /// <c>docs/design/proper-nouns.csv</c>, which is where the names are owned.
    ///
    /// <para><b>The simulation counts ticks and nothing else; this is the only place the calendar
    /// exists.</b> It lived in the Hud assembly's <c>GameClock</c> until temperature (design 28)
    /// made the simulation the second reader: a system that models the season has to know which
    /// one it is, and Sim must not reference Hud. So the mapping moved down to the contracts
    /// assembly where both sides can read it, and <c>GameClock</c> delegates to it — the interface
    /// keeps its clock, and the two can no longer disagree about what time it is.</para>
    ///
    /// <para>Everything here is a pure function of the tick. A short year — a full seasonal cycle
    /// inside a prototype's play time — is the point of 72 days.</para>
    /// </summary>
    public static class Calendar
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

        /// <summary>The season name, which is what temperature and one day weather key off.</summary>
        public static string SeasonName(long tick) => SeasonOfMonth[MonthOfYear(tick)];

        /// <summary>
        /// The season as an index, 0–2 in the order the calendar holds them (Wash, Glare, Rime) —
        /// the form a simulation system wants, where a string is only ever a label.
        /// </summary>
        public static int SeasonOfYear(long tick) => MonthOfYear(tick) / 2;

        /// <summary>Day within the current month, 1 to 12, which is how the clock reads it.</summary>
        public static int DayOfMonth(long tick) =>
            (int)(tick % TicksPerMonth / TicksPerDay) + 1;

        /// <summary>"07h · Day 3 · Larkspur · Wash": one line, hour-prefixed, the way the
        /// readout is specified in the panel catalogue (A3). Built here rather than in a view
        /// so the format is testable and localisation has one place to land.</summary>
        public static string Describe(long tick)
        {
            int day = DayOfMonth(tick);
            int month = MonthOfYear(tick);
            return $"{HourOfDay(tick):00}h · Day {day} · {Months[month]} · {SeasonOfMonth[month]}";
        }
    }
}
