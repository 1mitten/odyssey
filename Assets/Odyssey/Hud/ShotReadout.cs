#nullable enable
using System.Text;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The hit-chance readout at the pointer (design 53 §8b; the owner chose a tooltip at the cursor):
    /// with a drafted colonist holding a gun selected, hovering a hostile shows what a shot would
    /// come to and why. The numbers are the simulation's <see cref="ShotReportView"/>; this is only
    /// the words, every one from the registry, and ASCII punctuation, because a character the two
    /// shipped fonts lack draws as nothing (<c>HudFontTests</c>).
    ///
    /// <para>One line, most important first: the chance, then what moved it — the skill, the
    /// distance, the gun, the cover and what gives it, and the descent when a shot comes down.</para>
    /// </summary>
    public static class ShotReadout
    {
        public const string ToHitKey = "ui.combat.shot.tohit";
        public const string AboveKey = "ui.combat.shot.above";
        public const string OutOfRangeKey = "ui.combat.shot.outofrange";
        public const string NoSightKey = "ui.combat.shot.nosight";
        public const string CoverKey = "ui.combat.cover";
        public const string ShootingKey = "ui.skill.shooting";

        static readonly StringBuilder Builder = new StringBuilder(96);

        /// <summary>The readout's one line for a report.</summary>
        public static string Text(in ShotReportView report)
        {
            if (!report.InRange) return Registry.Label(OutOfRangeKey);
            if (!report.InSight) return Registry.Label(NoSightKey);

            Builder.Clear();
            Builder.Append(Percent(report.TotalPerMille)).Append("% ").Append(Registry.Label(ToHitKey));
            Builder.Append(": ").Append(Registry.Label(ShootingKey)).Append(' ').Append(report.ShootingLevel);
            Builder.Append(", ").Append((report.DistanceMm + 500) / 1_000).Append(" m");
            string gun = ItemLabels.Label(report.WeaponDef).ToLowerInvariant();
            if (gun.Length > 0) Builder.Append(", ").Append(gun);
            if (report.CoverPerMille > 0)
            {
                Builder.Append("; ").Append(Registry.Label(CoverKey)).Append(" -").Append(Percent(report.CoverPerMille)).Append('%');
                string what = CoverName(report.TopCoverEdifice);
                if (what.Length > 0) Builder.Append(" (").Append(what).Append(')');
                if (report.LowElevationPerMille < 1_000)
                    Builder.Append(", ").Append(Registry.Label(AboveKey)).Append(" x")
                        .Append((report.LowElevationPerMille / 1_000f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
            return Builder.ToString();
        }

        /// <summary>
        /// The line's colour: the chance to hit on <see cref="StatInks.HitChance"/> (design 59). A
        /// shot out of range or out of sight has no chance, and reads as the bottom of the scale.
        /// </summary>
        public static HudColour Ink(in ShotReportView report) =>
            !report.InRange || !report.InSight
                ? StatInks.Ink(0)
                : StatInks.Ink(StatInks.HitChance, report.TotalPerMille);

        /// <summary>Per mille to a whole per cent, rounded to the nearest.</summary>
        public static int Percent(int perMille) => (perMille + 5) / 10;

        /// <summary>What gives the most cover: the edifice's own name, or the rock's.</summary>
        static string CoverName(int edifice)
        {
            if (edifice == 0) return string.Empty;
            if (edifice < 0) return Registry.Label("ui.terrain.rock").ToLowerInvariant();
            return EdificeLabels.Label(edifice);
        }
    }
}
