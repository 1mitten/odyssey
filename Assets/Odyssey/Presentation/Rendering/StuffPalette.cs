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

            // Water carries its opacity in the alpha, which is the one place in this file where
            // alpha means anything: Odyssey/Water reads _BaseColor.a directly, and it is how the
            // two depths are told apart. They must stay close in hue — a body of water has one
            // colour and gets darker, it does not change colour halfway across — so the deep
            // entry is the shallow one darkened and closed up rather than a different blue.
            new Color(0.28f, 0.52f, 0.55f, 0.62f),     // 18 shallow water — the bed reads through
            new Color(0.10f, 0.26f, 0.34f, 0.90f),     // 19 deep water — almost nothing does
            new Color(0.44f, 0.46f, 0.34f),            // 20 marsh — wet ground, not shadow
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

            // Water is never drawn over pack art — it has a shader of its own and the solids
            // above are what it uses — so these two are placeholders that keep the arrays the
            // same length, which is the invariant this file's own comment asks for.
            Color.white,                               // 18 shallow water
            Color.white,                               // 19 deep water
            // Over the dirt texture: pulled green and kept bright. The first value tried was
            // darker, and against a meadow lifted to 1.04/1.30/1.55 it read as shadow rather
            // than as bog — the eye takes a dark band beside bright grass for a shade before it
            // takes it for a material.
            new Color(0.92f, 1.10f, 0.74f),            // 20 marsh
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
            terrain >= 0 && terrain < NaturalContent.TerrainCount
                ? NaturalContent.TerrainAt((ushort)terrain).defName
                : "Rock";
    }
}
