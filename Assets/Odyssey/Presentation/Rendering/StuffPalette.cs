#nullable enable
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
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
            // Wood continues the table at NaturalContent.StuffWood. White: a tree wears its own
            // pack material, which is already the right green and brown, and a built wooden wall
            // will get its own art rather than a tinted concrete one.
            Color.white,                               // wood
        };

        static readonly Color[] StuffSolids =
        {
            new Color(0.70f, 0.70f, 0.70f),            // none
            new Color(0.62f, 0.60f, 0.56f),            // concrete
            new Color(0.48f, 0.53f, 0.60f),            // steel
            new Color(0.60f, 0.66f, 0.70f),            // composite
            new Color(0.46f, 0.34f, 0.22f),            // wood
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

            // Natural terrain. NaturalContent continues CoreContent's numbering rather than
            // replacing it, so these must stay in the same order and this array must stay as long
            // as NaturalContent.TerrainCount. Without them a wilderness map drew entirely in the
            // fallback grey, which is what made the first natural scene look like nothing.
            new Color(0.36f, 0.52f, 0.24f),            // 10 grass
            new Color(0.42f, 0.33f, 0.22f),            // 11 bare earth
            new Color(0.48f, 0.46f, 0.42f),            // 12 packed gravel
            new Color(0.76f, 0.70f, 0.52f),            // 13 sand
            new Color(0.31f, 0.24f, 0.17f),            // 14 subsoil
            new Color(0.20f, 0.20f, 0.22f),            // 15 bedrock
            new Color(0.46f, 0.32f, 0.22f),            // 16 iron ore
            new Color(0.13f, 0.13f, 0.15f),            // 17 coal seam
        };

        /// <summary>
        /// What multiplies a terrain **texture**, the counterpart to <see cref="TerrainSolids"/>
        /// for cells that have pack art behind them. Same index space, same length.
        ///
        /// Mostly identity, because a texture that looks right should be left alone. Grass is the
        /// exception and the reason this array exists. The Synty meadow texture is a muted olive
        /// suited to a photographic landscape, whereas the look this game is aiming at is the
        /// bright, saturated yellow-green of the reference art. Values above one are deliberate
        /// and legal: <c>_BaseColor</c> is a plain multiply with no clamp, so it can lift a texture
        /// as well as darken one.
        ///
        /// This is the single dial for how green the world reads. Turn it here, nowhere else.
        /// </summary>
        static readonly Color[] TerrainTints =
        {
            Color.white,                               // air, never drawn
            Color.white,                               // pavement
            Color.white,                               // cracked pavement
            Color.white,                               // rubble
            Color.white,                               // soil
            Color.white,                               // gravel
            Color.white,                               // engineered fill
            Color.white,                               // rock
            Color.white,                               // buried city seam
            Color.white,                               // salvage
            new Color(1.04f, 1.30f, 1.55f),            // 10 grass — lifted towards the reference
            Color.white,                               // 11 bare earth
            Color.white,                               // 12 packed gravel
            Color.white,                               // 13 sand
            Color.white,                               // 14 subsoil
            Color.white,                               // 15 bedrock
            Color.white,                               // 16 iron ore
            Color.white,                               // 17 coal seam
        };

        public static Color TerrainTint(int terrain) =>
            terrain >= 0 && terrain < TerrainTints.Length ? TerrainTints[terrain] : Color.white;

        /// <summary>
        /// What multiplies a tuft of grass or any other piece of standing foliage.
        ///
        /// White, and deliberately so: the Nature Biomes grass clumps are already the bright
        /// yellow-green of the reference art, which the ground texture is not, so the one thing
        /// foliage needs is to be left alone. This is the dial if that ever stops being true —
        /// for a season, a biome, or a blighted map — and it is a separate dial from
        /// <see cref="TerrainTints"/> precisely so that lifting the ground cannot drag the plants
        /// standing on it somewhere nobody intended.
        /// </summary>
        static readonly Color[] FoliageTints =
        {
            // A touch above white. At board distance a clump's blades are thinner than a pixel and
            // the shadowed sides win the pixel, so the far meadow drifts dark; a small lift keeps
            // it in the same key as the lifted ground beneath it without turning it neon.
            new Color(1.06f, 1.08f, 1.02f),            // 0 grass
        };

        public static Color FoliageTint(int variant) =>
            variant >= 0 && variant < FoliageTints.Length ? FoliageTints[variant] : Color.white;

        /// <summary>
        /// The cyan trim. Black means the material has no emissive contribution.
        ///
        /// <para>Ore glows for a reason that is not decoration. Coal sits at 0.13 grey and rock
        /// at 0.25: down a shaft with no lamp in it they are the same colour, and a seam the
        /// player cannot pick out of the wall is a seam that may as well not have generated. The
        /// trim is what separates them, and it is the same cyan the concept renders use for
        /// salvage — this world's signal for "there is something in there".</para>
        ///
        /// <para>It only ever reaches a <em>discovered</em> cell, because an undiscovered seam
        /// arrives here as plain rock: <c>WorldRenderModel.Seen</c> has already substituted it.
        /// So this table cannot give ore away, however bright it is.</para>
        /// </summary>
        static readonly Color[] TerrainEmission =
        {
            Color.black, Color.black, Color.black, Color.black, Color.black,
            Color.black, Color.black, Color.black,
            new Color(0.06f, 0.30f, 0.34f),            // buried city seam
            new Color(0.10f, 0.50f, 0.56f),            // salvage

            // Natural terrain, continuing CoreContent's numbering exactly as the tables above do.
            Color.black,                               // 10 grass
            Color.black,                               // 11 bare earth
            Color.black,                               // 12 packed gravel
            Color.black,                               // 13 sand
            Color.black,                               // 14 subsoil
            Color.black,                               // 15 bedrock
            // Coal is the brighter of the two, which looks backwards and is not. Iron's rust
            // brown already separates itself from rock on base colour alone; coal is a near-black
            // against a dark grey and has nothing but the trim to be seen by.
            new Color(0.08f, 0.38f, 0.43f),            // 16 iron ore
            new Color(0.11f, 0.56f, 0.63f),            // 17 coal seam
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
            terrain >= 0 && terrain < NaturalContent.TerrainCount
                ? NaturalContent.TerrainAt((ushort)terrain).defName
                : "Rock";
    }
}
