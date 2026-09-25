#nullable enable
using System.Text;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// How a trait is written on the colonist's pane and the select screen (design 43 §5f): its
    /// name, then what it does in a few words — "Work +20%", "Mood +6", "Breaks sooner",
    /// "Learns x1.75", "Cannot: Mining" — from the numbers the simulation published, never from a
    /// copy of the Def. <b>One owner</b> for both surfaces: the pane reads the numbers off aspects
    /// and the select screen off the rolled pawn, and both come here to be written.
    ///
    /// <para><b>The tint is a judgement, and only the glyph-free value carries it</b> (design 43
    /// §5f, g-04): bad for a trait that forbids work or brings a break nearer, or costs mood,
    /// work or learning; good for one that only buys; none for a trait that is both.</para>
    /// </summary>
    public static class TraitSummary
    {
        /// <summary>The registry key that names a trait.</summary>
        public static string Key(int handle) =>
            handle >= 0 && handle < TraitHandle.Count ? "ui.trait." + TraitHandle.Names[handle] : "ui.trait.unknown";

        /// <summary>What a trait does, in a few words, from its published effects.</summary>
        public static string Of(int mood, int nerve, int learnPerMille, int workPerMille, int cannotMask)
        {
            var text = new StringBuilder();
            if (workPerMille != 1_000)
                Part(text, Registry.Label("ui.mind.work") + " " + Signed((workPerMille - 1_000) / 10) + "%");
            if (mood != 0)
                Part(text, Registry.Label("ui.need.mood") + " " + MindCatalogue.Points(mood));
            if (nerve != 0)
                Part(text, Registry.Label(nerve > 0 ? "ui.mind.breakssooner" : "ui.mind.breakslater"));
            if (learnPerMille != 1_000)
                Part(text, Registry.Label("ui.mind.learns") + " x" + Factor(learnPerMille));
            if (cannotMask != 0)
            {
                var names = new StringBuilder();
                foreach (WorkCatalogue.Entry entry in WorkCatalogue.All)
                {
                    if (entry.Handle < 0 || (cannotMask & (1 << entry.Handle)) == 0) continue;
                    if (names.Length > 0) names.Append(", ");
                    names.Append(Registry.Label(entry.Key));
                }
                Part(text, Registry.Label("ui.mind.cannot") + ": " + names);
            }
            return text.ToString();
        }

        /// <summary>The value's colour: bad, good, or none (see the class remarks).</summary>
        public static HudColour? Tint(int mood, int nerve, int learnPerMille, int workPerMille, int cannotMask)
        {
            bool costs = cannotMask != 0 || nerve > 0 || mood < 0 || learnPerMille < 1_000 || workPerMille < 1_000;
            bool buys = nerve < 0 || mood > 0 || learnPerMille > 1_000 || workPerMille > 1_000;
            return costs && !buys ? HudTheme.Bad : buys && !costs ? HudTheme.Good : (HudColour?)null;
        }

        /// <summary>One trait as a pane row: its name, what it does, its tint and its description.</summary>
        public static InspectRow Row(int handle, int mood, int nerve, int learnPerMille, int workPerMille, int cannotMask)
        {
            string key = Key(handle);
            return new InspectRow
            {
                Name = Registry.Label(key),
                Value = Of(mood, nerve, learnPerMille, workPerMille, cannotMask),
                Tint = Tint(mood, nerve, learnPerMille, workPerMille, cannotMask),
                Tooltip = Registry.Describe(key),
            };
        }

        static void Part(StringBuilder text, string part)
        {
            if (text.Length > 0) text.Append(", ");
            text.Append(part);
        }

        static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        /// <summary>A per-mille factor as the fewest decimals that say it: 1750 is "1.75", 400 is "0.4".</summary>
        static string Factor(int perMille)
        {
            string whole = (perMille / 1_000).ToString();
            int fraction = perMille % 1_000;
            if (fraction == 0) return whole;
            string digits = fraction.ToString("000").TrimEnd('0');
            return whole + "." + digits;
        }
    }
}
