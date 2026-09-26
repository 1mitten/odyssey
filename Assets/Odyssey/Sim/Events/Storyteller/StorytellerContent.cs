#nullable enable
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// The storytellers, read from a loaded <see cref="DefDatabase"/> (design 59 §3).
    ///
    /// <para><b><see cref="Order"/> is the contract</b>, as <c>IncidentContent.Order</c> is: position
    /// in it is the index the intent carries and the save keeps, and <c>StorytellerHandle</c> in the
    /// contracts assembly repeats it. Append, never insert.</para>
    /// </summary>
    public sealed class StorytellerContent
    {
        public static readonly string[] Order =
        {
            "Storyteller_Jacob",
            "Storyteller_Trent",
            "Storyteller_Kano",
        };

        public StorytellerDef[] Defs = System.Array.Empty<StorytellerDef>();

        public int Count => Defs.Length;

        /// <summary>Read and check. A storyteller that could never fire anything is a load error.</summary>
        public static StorytellerContent FromDefs(DefDatabase defs)
        {
            var content = new StorytellerContent { Defs = new StorytellerDef[Order.Length] };
            for (int i = 0; i < Order.Length; i++)
            {
                StorytellerDef def = One(defs, Order[i]);
                Validate(def);
                content.Defs[i] = def;
            }
            return content;
        }

        public static void Validate(StorytellerDef def)
        {
            string where = $"{def.Origin}: storyteller '{def.defName}'";
            if (def.labelKey.Length == 0) throw new DefLoadException($"{where} has no labelKey.");
            if (def.graceDays < 0) throw new DefLoadException($"{where} has a negative grace.");
            if (def.generators.Count == 0) throw new DefLoadException($"{where} has no generators, so it could never fire anything.");
            for (int g = 0; g < def.generators.Count; g++)
            {
                GeneratorDef gen = def.generators[g];
                string at = $"{where}, generator {g} ({gen.kind})";
                switch (gen.kind)
                {
                    case GeneratorKind.OnOffCycle:
                        if (gen.onDays <= 0 || gen.offDays < 0)
                            throw new DefLoadException($"{at} needs onDays above 0 and offDays of 0 or more.");
                        if (gen.firesMin < 1 || gen.firesMax < gen.firesMin)
                            throw new DefLoadException($"{at} needs 1 <= firesMin <= firesMax.");
                        if (gen.minSpacingHours < 0 || (gen.firesMax - 1) * gen.minSpacingHours >= gen.onDays * 24)
                            throw new DefLoadException($"{at}: firesMax at minSpacingHours does not fit in onDays.");
                        break;
                    case GeneratorKind.MeanTimeBetween:
                        if (gen.meanHours < 2) throw new DefLoadException($"{at} needs meanHours of 2 or more.");
                        break;
                    case GeneratorKind.RandomBag:
                        if (gen.meanHours < 2) throw new DefLoadException($"{at} needs meanHours of 2 or more.");
                        if (gen.weights.Count == 0) throw new DefLoadException($"{at} has no weights.");
                        foreach (BagWeight w in gen.weights)
                            if (w.weight <= 0) throw new DefLoadException($"{at} has a weight of {w.weight} for {w.category}.");
                        if (gen.droughtDays < 0) throw new DefLoadException($"{at} has a negative drought.");
                        if (gen.budgetMinPerMille <= 0 || gen.budgetMaxPerMille < gen.budgetMinPerMille)
                            throw new DefLoadException($"{at} needs 0 < budgetMinPerMille <= budgetMaxPerMille.");
                        break;
                }
            }
            foreach (int p in def.populationCurve)
                if (p < 0) throw new DefLoadException($"{where} has a negative population intent.");
        }

        static StorytellerDef One(DefDatabase defs, string defName)
        {
            if (!defs.HasTable<StorytellerDef>())
                throw new DefLoadException($"the content has no StorytellerDef at all, and '{defName}' is required.");
            if (!defs.Table<StorytellerDef>().TryGetHandle(defName, out var handle))
                throw new DefLoadException($"the content has no StorytellerDef named '{defName}'.");
            return defs.Table<StorytellerDef>()[handle];
        }
    }
}
