#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// One row of a raid mix: a hostile kind and its share of the band, per mille.
    /// </summary>
    public sealed class RaidMixEntry
    {
        /// <summary>The <c>PawnKindDef</c> this row brings, by defName.</summary>
        public string kind = string.Empty;

        /// <summary>Its share of the band, per mille. A mix's rows sum to 1,000.</summary>
        public int perMille;
    }

    /// <summary>
    /// Who a raid is made of (design 55 §8): the debug menu's dropdown, and what a storyteller will
    /// draw from. Data, so a new mix is a Def and never a code change — the owner's "a mix of melee
    /// and projectiles" is a row of these.
    ///
    /// <para><b>A band is split by largest remainder</b> (<see cref="RaidMix.Compose"/>), so the
    /// counts are exact and add up to the band: ten in a 700/300 mix is seven and three, and one is
    /// one of the larger share.</para>
    /// </summary>
    public class RaidMixDef : Def
    {
        /// <summary>The registry key the dropdown and the wiki name this by, <c>ui.raid.mix.*</c>.</summary>
        public string labelKey = string.Empty;

        public List<RaidMixEntry> entries = new List<RaidMixEntry>();
    }

    /// <summary>A <see cref="RaidMixDef"/> bound to the pawn content: each row's kind as a <c>PawnKindIndex</c>.</summary>
    public sealed class RaidMix
    {
        public RaidMix(RaidMixDef def, int[] kinds, int[] perMille)
        {
            Def = def;
            Kinds = kinds;
            PerMille = perMille;
        }

        public RaidMixDef Def { get; }

        /// <summary>Each row's kind, as a <c>PawnKindIndex</c>. Parallel to <see cref="PerMille"/>.</summary>
        public int[] Kinds { get; }

        public int[] PerMille { get; }

        /// <summary>
        /// How many of each row a band of <paramref name="size"/> brings, into
        /// <paramref name="counts"/> (one per row). Largest remainder: every row gets the whole part
        /// of its share, and what is left over goes one each to the rows with the largest
        /// fractional parts, a tie to the earlier row. The counts always sum to
        /// <paramref name="size"/>, and a pure function of it.
        /// </summary>
        public void Compose(int size, int[] counts)
        {
            int rows = Kinds.Length;
            int given = 0;
            for (int i = 0; i < rows; i++)
            {
                counts[i] = size * PerMille[i] / 1000;
                given += counts[i];
            }

            for (int left = size - given; left > 0; left--)
            {
                int best = -1, bestRemainder = -1;
                for (int i = 0; i < rows; i++)
                {
                    // The remainder a row still holds after what it has been given.
                    int remainder = size * PerMille[i] - counts[i] * 1000;
                    if (remainder > bestRemainder)
                    {
                        best = i;
                        bestRemainder = remainder;
                    }
                }
                counts[best]++;
            }
        }

        /// <summary>
        /// Bind a Def: every row's kind must exist and be hostile, and the shares must sum to
        /// 1,000. Throws a <see cref="DefLoadException"/> naming the mix otherwise, so a misspelt
        /// kind is a content error at load and never a raid of nobody.
        /// </summary>
        public static RaidMix Bind(RaidMixDef def, PawnContent pawns)
        {
            if (def.labelKey.Length == 0)
                throw new DefLoadException($"{def.Origin}: raid mix '{def.defName}' has no labelKey, so the dropdown could not name it.");
            if (def.entries.Count == 0)
                throw new DefLoadException($"{def.Origin}: raid mix '{def.defName}' has no entries.");

            var kinds = new int[def.entries.Count];
            var perMille = new int[def.entries.Count];
            int sum = 0;
            for (int i = 0; i < def.entries.Count; i++)
            {
                RaidMixEntry entry = def.entries[i];
                int kind = -1;
                for (int k = 0; k < pawns.Kinds.Length; k++)
                    if (pawns.Kinds[k].defName == entry.kind) { kind = k; break; }
                if (kind < 0)
                    throw new DefLoadException(
                        $"{def.Origin}: raid mix '{def.defName}' names kind '{entry.kind}', which the pawn content does not have.");
                if (pawns.Kinds[kind].faction != Faction.Hostile)
                    throw new DefLoadException(
                        $"{def.Origin}: raid mix '{def.defName}' names kind '{entry.kind}', which is not hostile.");
                if (entry.perMille <= 0)
                    throw new DefLoadException(
                        $"{def.Origin}: raid mix '{def.defName}' gives '{entry.kind}' a share of {entry.perMille}.");
                kinds[i] = kind;
                perMille[i] = entry.perMille;
                sum += entry.perMille;
            }

            if (sum != 1000)
                throw new DefLoadException(
                    $"{def.Origin}: raid mix '{def.defName}' has shares summing to {sum}, not 1000.");
            return new RaidMix(def, kinds, perMille);
        }
    }
}
