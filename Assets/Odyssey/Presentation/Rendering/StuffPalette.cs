#nullable enable
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Colour for the simulation's material indices.
    ///
    /// Two palettes, because the same index means two different things depending on what draws it.
    /// Over Synty art, <c>_BaseColor</c> is a **multiply** over the colour atlas
    /// (<c>e-04-tint-strategy.md</c>): it can only darken, so the tints are near-white and exist
    /// to separate concrete from steel, not to paint the building. Over the primitive stand-ins
    /// there is no atlas underneath, so the colour carries the whole look and is fully saturated.
    ///
    /// The emissive column is the cyan trim from the concept renders: salvage, buried seams and
    /// utility taps glow, which is what makes them findable at night without a separate overlay.
    /// </summary>
    public static class StuffPalette
    {
        static readonly Color[] StuffTints =
        {
            Color.white,                               // none
            new Color(0.92f, 0.91f, 0.88f),            // concrete
            new Color(0.82f, 0.86f, 0.92f),            // steel
            new Color(0.88f, 0.92f, 0.94f),            // composite
        };

        static readonly Color[] StuffSolids =
        {
            new Color(0.70f, 0.70f, 0.70f),            // none
            new Color(0.62f, 0.60f, 0.56f),            // concrete
            new Color(0.48f, 0.53f, 0.60f),            // steel
            new Color(0.60f, 0.66f, 0.70f),            // composite
        };

        static readonly Color[] TerrainSolids =
        {
            new Color(0.00f, 0.00f, 0.00f),            // air, never drawn
            new Color(0.44f, 0.44f, 0.46f),            // pavement
            new Color(0.38f, 0.37f, 0.36f),            // cracked pavement
            new Color(0.32f, 0.29f, 0.26f),            // rubble
            new Color(0.27f, 0.21f, 0.15f),            // soil
            new Color(0.40f, 0.38f, 0.35f),            // gravel
            new Color(0.33f, 0.32f, 0.31f),            // engineered fill
            new Color(0.24f, 0.25f, 0.28f),            // rock
            new Color(0.21f, 0.28f, 0.31f),            // buried city seam
            new Color(0.30f, 0.42f, 0.45f),            // salvage
        };

        /// <summary>The cyan trim. Black means the material has no emissive contribution.</summary>
        static readonly Color[] TerrainEmission =
        {
            Color.black, Color.black, Color.black, Color.black, Color.black,
            Color.black, Color.black, Color.black,
            new Color(0.06f, 0.30f, 0.34f),            // buried city seam
            new Color(0.10f, 0.50f, 0.56f),            // salvage
        };

        public static readonly Color TrimEmission = new Color(0.10f, 0.62f, 0.70f);

        public static Color StuffTint(int stuff) =>
            stuff >= 0 && stuff < StuffTints.Length ? StuffTints[stuff] : Color.white;

        public static Color StuffSolid(int stuff) =>
            stuff >= 0 && stuff < StuffSolids.Length ? StuffSolids[stuff] : StuffSolids[0];

        public static Color TerrainSolid(int terrain) =>
            terrain >= 0 && terrain < TerrainSolids.Length ? TerrainSolids[terrain] : TerrainSolids[6];

        public static Color TerrainEmissive(int terrain) =>
            terrain >= 0 && terrain < TerrainEmission.Length ? TerrainEmission[terrain] : Color.black;

        /// <summary>The colour a module of this stuff takes, given whether art is underneath it.</summary>
        public static Color For(int stuff, bool overArt) => overArt ? StuffTint(stuff) : StuffSolid(stuff);

        /// <summary>The terrain def name for a terrain index, for the module id.</summary>
        public static string TerrainName(int terrain) =>
            terrain >= 0 && terrain < CoreContent.Terrain.Count
                ? CoreContent.Terrain[terrain].defName
                : "Rock";
    }
}
