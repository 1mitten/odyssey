#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// One minable deposit kind: what it is made of, how often it is chosen, and the band of
    /// depths below the local surface it is found in.
    ///
    /// <para>Depth weighting is the whole point — coal sits below iron, so digging deeper is
    /// worth doing and the two are not interchangeable. The terrain is named rather than
    /// numbered, so <c>[DefReference]</c> proves at load that the ore is made of something that
    /// exists; a typo here would otherwise surface as a stratum of nothing, seeds later.</para>
    /// </summary>
    public class OreKindDef : Def
    {
        [DefReference(typeof(TerrainDef))] public string terrain = string.Empty;

        public int weight = 100;

        public int minDepth;

        public int maxDepth;

        public string moduleId = string.Empty;
    }

    /// <summary>
    /// The world's content, read from a loaded <see cref="DefDatabase"/> (OQ-16).
    ///
    /// <para><b>One terrain table, not two.</b> In code the terrain kinds are split across
    /// <see cref="CoreContent"/> (the ruined city's) and <see cref="NaturalContent"/> (the
    /// wilderness's) with the second continuing the first's numbering, because the two
    /// generators write into one <c>CellGrid.Terrain</c> array and an index must mean one thing.
    /// The XML declares all twenty-one in one file, in that same numbering, which is what the
    /// TODO in <c>NaturalContent</c> asked for.</para>
    ///
    /// <para><b><see cref="TerrainOrder"/> is the contract.</b> A terrain index is stored in every
    /// cell of every save and folded into every state hash, while <c>DefLoader</c> sorts each
    /// table by defName so the handles it assigns are stable across machines. Those are two
    /// different orders, and this list is the one that matters: position in it <i>is</i> the
    /// terrain index. <c>WorldContentDefTests</c> checks every constant in both content classes
    /// against it, so a name added in the wrong place fails a test rather than a save.</para>
    ///
    /// <para>The in-code tables remain the oracle; nothing constructs a world from these yet.
    /// See <see cref="Pawns.PawnContent"/> for why that switch is its own change.</para>
    /// </summary>
    public static class WorldContent
    {
        /// <summary>
        /// Every terrain kind in index order: ten from the city's table, eleven from the
        /// wilderness's, exactly as the constants number them.
        /// </summary>
        public static readonly string[] TerrainOrder =
        {
            // CoreContent, 0..9. Air is 0 because CellGrid's zero default has to mean "nothing".
            "Air",
            "Pavement",
            "CrackedPavement",
            "Rubble",
            "Soil",
            "Gravel",
            "EngineeredFill",
            "Rock",
            "BuriedSeam",
            "Salvage",

            // NaturalContent, 10..20.
            "Grass",
            "BareEarth",
            "PackedGravel",
            "Sand",
            "Subsoil",
            "Bedrock",
            "IronOre",
            "CoalSeam",
            "ShallowWater",
            "DeepWater",
            "Marsh",
        };

        /// <summary>The ore kinds in the order the deposit draw indexes them.</summary>
        public static readonly string[] OreOrder =
        {
            "Ore_Iron",
            "Ore_Coal",
        };

        /// <summary>
        /// The Def types the world content is made of, registered in one place so a caller cannot
        /// load half of it.
        /// </summary>
        public static DefLoader Register(DefLoader loader) =>
            loader.Register<TerrainDef>().Register<OreKindDef>();

        /// <summary>
        /// The whole terrain table, in index order. A missing or misspelt kind throws here
        /// naming it, rather than leaving a null that the first cell written would trip over.
        /// </summary>
        public static TerrainDef[] TerrainFromDefs(DefDatabase defs)
        {
            var table = new TerrainDef[TerrainOrder.Length];
            for (int i = 0; i < TerrainOrder.Length; i++) table[i] = One<TerrainDef>(defs, TerrainOrder[i]);
            return table;
        }

        /// <summary>The ore kinds, resolved into the same struct the generator already draws from.</summary>
        public static NaturalContent.OreKind[] OresFromDefs(DefDatabase defs)
        {
            var index = TerrainIndexByName();
            var ores = new NaturalContent.OreKind[OreOrder.Length];
            for (int i = 0; i < OreOrder.Length; i++)
            {
                OreKindDef def = One<OreKindDef>(defs, OreOrder[i]);
                if (!index.TryGetValue(def.terrain, out ushort terrain))
                    throw new DefLoadException($"{def.Origin}: ore '{def.defName}' is made of unknown terrain '{def.terrain}'.");
                ores[i] = new NaturalContent.OreKind(terrain, def.weight, def.minDepth, def.maxDepth, def.moduleId);
            }
            return ores;
        }

        /// <summary>The terrain index of each name. Load-time only, as every name lookup is.</summary>
        public static Dictionary<string, ushort> TerrainIndexByName()
        {
            var index = new Dictionary<string, ushort>(TerrainOrder.Length, System.StringComparer.Ordinal);
            for (int i = 0; i < TerrainOrder.Length; i++) index[TerrainOrder[i]] = (ushort)i;
            return index;
        }

        static T One<T>(DefDatabase defs, string defName) where T : Def
        {
            if (!defs.HasTable<T>())
                throw new DefLoadException($"the content has no {typeof(T).Name} at all, and '{defName}' is required.");
            if (!defs.Table<T>().TryGetHandle(defName, out var handle))
                throw new DefLoadException($"the content has no {typeof(T).Name} named '{defName}'.");
            return defs.Table<T>()[handle];
        }
    }
}
