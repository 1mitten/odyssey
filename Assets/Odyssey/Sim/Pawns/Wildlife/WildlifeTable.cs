#nullable enable
using System;

namespace Odyssey.Sim.Pawns.Wildlife
{
    /// <summary>Where a kind of animal is placed when a world is seeded (design 30 §1).</summary>
    public enum Habitat
    {
        /// <summary>Any dry, walkable, reachable surface cell.</summary>
        Any = 0,

        /// <summary>Within two cells of a standing tree.</summary>
        Woodland = 1,

        /// <summary>Beside solid terrain on its own layer: an outcrop's side, a rock face, a cut.</summary>
        Rock = 2,
    }

    /// <summary>
    /// One line of a world's wildlife table (design 30 §1): a kind that lives there, how common
    /// it is against the others, how many arrive together, and where they are put. A world lists
    /// only kinds that exist and have art; a kind the content does not know generates nothing,
    /// rather than throwing, so a modded table on a stock build degrades to what it can draw.
    /// </summary>
    public sealed class WildlifeEntry
    {
        /// <summary>The <c>PawnKindDef</c> name, e.g. <c>PawnKind_MiddenHog</c>.</summary>
        public string kind = string.Empty;

        /// <summary>Relative commonality against the other entries; zero is never picked.</summary>
        public int weight = 1;

        /// <summary>Smallest and largest group that spawns together. A solitary kind is 1–1.</summary>
        public int groupMin = 1;

        public int groupMax = 1;

        public Habitat habitat = Habitat.Any;

        public WildlifeEntry() { }

        public WildlifeEntry(string kind, int weight, int groupMin, int groupMax, Habitat habitat)
        {
            this.kind = kind;
            this.weight = weight;
            this.groupMin = groupMin;
            this.groupMax = groupMax;
            this.habitat = habitat;
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(kind)) throw new ArgumentException("A wildlife entry names no kind.");
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
            if (groupMin < 1 || groupMax < groupMin) throw new ArgumentOutOfRangeException(nameof(groupMin), $"{kind}: group {groupMin}–{groupMax}");
        }
    }

    /// <summary>The seeded streams wildlife draws on. Distinct from every <see cref="PawnPurpose"/>.</summary>
    public static class WildlifePurpose
    {
        public const uint Seed = 0x71D1_1FE3;
        public const uint Level = 0x71D1_1FE5;
    }
}
