#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Events
{
    /// <summary>The three shapes a storyteller is built from (design 68 §3). <c>scheduled</c> stays reserved.</summary>
    public enum GeneratorKind
    {
        /// <summary>On-days and off-days; in each on-phase, a few fires of its category a minimum apart.</summary>
        OnOffCycle = 0,

        /// <summary>About every so many hours, sampled.</summary>
        MeanTimeBetween = 1,

        /// <summary>One roll on a mean interval, then a category by weight; a drought forces a big threat.</summary>
        RandomBag = 2,
    }

    /// <summary>One category's weight in a random bag.</summary>
    public sealed class BagWeight
    {
        public IncidentCategory category = IncidentCategory.Misc;
        public int weight;
    }

    /// <summary>
    /// One generator of a storyteller (design 68 §3): a nested block of a <see cref="StorytellerDef"/>,
    /// the <c>&lt;raid&gt;</c> block's precedent. Times are in whole game hours or days, because the
    /// storyteller checks once a game hour.
    /// </summary>
    public sealed class GeneratorDef
    {
        public GeneratorKind kind = GeneratorKind.MeanTimeBetween;

        /// <summary>What an <see cref="GeneratorKind.OnOffCycle"/> or a mean-time-between stream fires.</summary>
        public IncidentCategory category = IncidentCategory.Misc;

        /// <summary>A mean-time-between stream of good and neutral things only: Bad incidents are skipped.</summary>
        public bool excludeBad;

        // ---- the cycle ----
        public int onDays;
        public int offDays;
        public int firesMin = 1;
        public int firesMax = 1;

        /// <summary>The least time between two fires of one on-phase.</summary>
        public int minSpacingHours;

        // ---- the mean-time-between stream and the bag ----

        /// <summary>The mean interval between rolls. A roll is drawn uniformly from half to one and a half of it.</summary>
        public int meanHours;

        /// <summary>The bag's categories and their weights.</summary>
        public List<BagWeight> weights = new List<BagWeight>();

        /// <summary>The bag: this many days with no big threat force the next roll to be one. 0 for never.</summary>
        public int droughtDays;

        /// <summary>The bag: a threat's size is multiplied by a draw between these, per mille.</summary>
        public int budgetMinPerMille = 1000;
        public int budgetMaxPerMille = 1000;
    }

    /// <summary>
    /// A storyteller (design 68 §3): a grace before the first big threat, the generators that pace
    /// it, and the curve population intent reads. The name and the blurb are the registry's
    /// (<c>labelKey</c>), so "inviting our own storytellers" is a Def and a registry row.
    /// </summary>
    public class StorytellerDef : Def
    {
        /// <summary>The <c>ui.storyteller.*</c> key the interface names this by.</summary>
        public string labelKey = string.Empty;

        /// <summary>Days from the colony's start before the first big threat, before difficulty stretches it.</summary>
        public int graceDays;

        public List<GeneratorDef> generators = new List<GeneratorDef>();

        /// <summary>
        /// Population intent (design 68 §3c), per mille by colonist count; the last entry holds for
        /// every count past the table. Multiplies the weight of any incident marked
        /// <c>populationGain</c>, of which there are none until the joiner (ST7).
        /// </summary>
        public int[] populationCurve = System.Array.Empty<int>();
    }
}
