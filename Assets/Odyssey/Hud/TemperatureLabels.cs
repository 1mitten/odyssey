#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// How the interface writes a temperature — the one owner of the form, beside
    /// <see cref="HudTheme.Temperature"/> which owns the colour.
    ///
    /// <para><b>Written because it was written twice.</b> The pane's tile row and the clock's
    /// outdoor reading each had their own copy of "centi-degrees to one signed decimal with the
    /// unit" — the same four operations spelled out in two assemblies, agreeing by luck. That is
    /// the shape of fault this project keeps meeting and has a name for: one rule with two
    /// owners (<c>docs/bug-patterns.md</c> P1), the same fault as the two order-colour tables
    /// that disagreed about deconstruct for months. Nothing was wrong with either copy on the day
    /// they were written; the cost arrives the first time somebody is asked to show whole degrees
    /// and corrects one of them.</para>
    ///
    /// <para><b>Integer arithmetic throughout</b>, and the sign handled by hand rather than by a
    /// format string: the whole thermal model is integers on purpose, and −12.5 °C must not
    /// arrive by way of a float that two runtimes could round apart. Truncation, not rounding, so
    /// the number shown is always one the simulation actually holds.</para>
    /// </summary>
    public static class TemperatureLabels
    {
        /// <summary>The unit, spelled once. Both shipped fonts can draw the degree sign —
        /// <c>HudFontTests</c> reads the cmap tables and would fail on it if they could not.
        /// </summary>
        public const string Unit = " °C";

        /// <summary>
        /// Centi-degrees as the interface reads them: signed, one decimal, with the unit.
        /// 1,250 is "12.5 °C" and −1,250 is "-12.5 °C".
        /// </summary>
        public static string Describe(int centiC)
        {
            int magnitude = centiC < 0 ? -centiC : centiC;
            return (centiC < 0 ? "-" : "") + magnitude / 100 + "." + magnitude % 100 / 10 + Unit;
        }
    }
}
