#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>
    /// What a place on the planet is (design 64 §7): an authored situation, cast at random from the
    /// place's own seed. Authored in XML (<c>Defs/Core/World/Situations.xml</c>).
    ///
    /// <para>The first slice has one, the salvage cache with a hazard (§7c), and so only the fields
    /// it needs: where it may stand, how common it is, what the cache holds and who guards it. Roles,
    /// fragments and hooks (§7a) arrive with the situations that need them, as fields here, so a
    /// Def written today still loads.</para>
    /// </summary>
    public class SituationDef : Def
    {
        /// <summary>The registry key the World tab names a place of this kind by (<c>ui.situation.*</c>).</summary>
        public string labelKey = string.Empty;

        /// <summary>Relative weight among the situations a tile may hold.</summary>
        public int weight = 1;

        /// <summary>Places per thousand eligible tiles, before spacing thins them.</summary>
        public int perThousandTiles = 20;

        /// <summary>The biomes (defNames) it may stand in; empty is any land that is not ice.</summary>
        public List<string> biomes = new List<string>();

        /// <summary>No closer to home than this many hexes.</summary>
        public int minHexesFromHome = 1;

        /// <summary>What the cache holds: each stack's item and a count range, rolled from the place's seed.</summary>
        public List<SituationStack> cache = new List<SituationStack>();

        /// <summary>The pawn kind (defName) that guards it, or empty for nobody.</summary>
        public string guardKind = string.Empty;

        public int guardsMin;
        public int guardsMax;

        /// <summary>Whether a tile of this biome may hold this situation.</summary>
        public bool StandsIn(string biomeDefName)
        {
            if (biomes.Count == 0) return true;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i] == biomeDefName) return true;
            return false;
        }
    }

    /// <summary>One stack a situation's cache holds: an item defName and how many, min to max inclusive.</summary>
    public class SituationStack
    {
        public string item = string.Empty;
        public int min = 1;
        public int max = 1;
    }
}
