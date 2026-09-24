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
            // Wood continues the table at NaturalContent.StuffWood.
            //
            // **This was white, and a wooden wall came out cream** (owner, 2026-09-17): the entry
            // was left white on the argument that a built wooden wall would get art of its own
            // rather than a tinted concrete one. True, and still the better answer — the packs
            // have SM_Bld_Fort_Wall_01/02 in PolygonWesternFrontier — but until somebody chooses
            // it, white means a wooden wall draws the plaster of SM_Bld_Base_Wall_01 untouched.
            //
            // The number is measured off the board rather than picked. In the owner's screenshot
            // the wall's sunlit face renders at sRGB (170, 141, 110) while a log pile lying in the
            // same light renders at (155, 110, 65) on its sawn end — which is what wood looks like
            // in this game, under this sun, at this time of day. The ratio of those two in *linear*
            // space, which is where _BaseColor multiplies, is (0.76, 0.59, 0.34). The brick on the
            // inner face goes from (72, 43, 30) to about (63, 32, 14) under it: darker timber, not
            // the black a stronger multiply would have crushed it to.
            new Color(0.76f, 0.59f, 0.34f),            // wood
            // Stone is the second thing a colony can build with (NaturalContent.StuffStone). A
            // grey against the concrete's warm cream, so a stone wall reads as cut rock rather
            // than as poured slab; it takes the rock material, which is already close.
            //
            // **It was (0.86, 0.87, 0.88) and it read as steel** (owner, 2026-09-17: "the stone
            // floor looks more like steel. I would expect a stone floor to be more boring gray
            // with some texture"). Put beside the entry three rows up, the reason is plain: steel
            // is (0.82, 0.86, 0.92) and stone was *brighter than it* and leaning the same way, b
            // over g over r. Two materials a player is asked to tell apart were one pale blue-grey
            // with the labels swapped, and a near-white multiply leaves whatever it is over
            // looking polished — which is the one thing stone is not.
            //
            // The number is not invented. StuffSolids, the tint used where there is no art at all,
            // has always held stone at (0.52, 0.51, 0.49): a mid grey, warm-neutral, r over g over
            // b. That is what this project already decided stone looks like; the over-art entry
            // had simply never been made to agree with it. This is that colour brought up by the
            // amount the art underneath is darker than plain white, and no further.
            new Color(0.62f, 0.61f, 0.58f),            // stone
        };

        static readonly Color[] StuffSolids =
        {
            new Color(0.70f, 0.70f, 0.70f),            // none
            new Color(0.62f, 0.60f, 0.56f),            // concrete
            new Color(0.48f, 0.53f, 0.60f),            // steel
            new Color(0.60f, 0.66f, 0.70f),            // composite
            new Color(0.46f, 0.34f, 0.22f),            // wood
            new Color(0.52f, 0.51f, 0.49f),            // stone
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
            // Rock, pulled cool. The pack's stone texture is a warm grey-brown, which at board
            // distance reads as earth rather than as stone — the complaint that started this.
            // _BaseColor is a plain multiply with no clamp, so red comes down and blue goes up
            // and the brown neutralises into grey. It cannot desaturate (that would need a lerp
            // towards luminance, which a multiply cannot express), so this is a hue shift, not a
            // wash: the texture's own mottling survives it.
            new Color(0.84f, 0.90f, 1.02f),            // rock
            Color.white,                               // buried city seam
            Color.white,                               // salvage
            new Color(1.04f, 1.30f, 1.55f),            // 10 grass — lifted towards the reference
            Color.white,                               // 11 bare earth
            Color.white,                               // 12 packed gravel
            Color.white,                               // 13 sand
            Color.white,                               // 14 subsoil
            new Color(0.78f, 0.84f, 0.98f),            // 15 bedrock — the same cool pull, darker
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

        public static Color TerrainTint(int terrain)
        {
            // Over the painted Meadow ground the grass keeps the pack's own colours (owner,
            // 2026-09-24: "Synty's colours"), so the lift above — which pulled a single olive
            // texture towards a lime it was never painted as — is not applied (design 38 §17).
            if (terrain == GrassTerrain && MeadowLook.GroundActive) return Color.white;
            return terrain >= 0 && terrain < TerrainTints.Length ? TerrainTints[terrain] : Color.white;
        }

        /// <summary>The grass terrain's index in the tables above.</summary>
        const int GrassTerrain = 10;

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
        /// <summary>
        /// The tints a tuft of grass can wear — greens, and one straw.
        ///
        /// <para><b>Why there is more than one now.</b> Every tuft used to take entry 0, so a
        /// meadow was one colour of grass however many clump meshes it strewed. The owner asked for
        /// "all a shade of green and the odd yellow one", and variety here is free in a way variety
        /// almost never is: the tint is chosen by which of the three clump *modules* a tuft uses,
        /// and a module is already its own instancing bucket. Three tints across three modules is
        /// the same number of draw calls as one tint across three modules. Choosing per tuft
        /// instead would multiply the buckets by the number of tints, on the heaviest instanced
        /// thing in the world.</para>
        ///
        /// <para><b>The clumps read yellow because the ground was moved and they were not.</b> This
        /// was a decision rather than a fault, and the previous comment here recorded it: the tufts
        /// were left at a near-neutral <c>(1.06, 1.08, 1.02)</c> on the grounds that "the grass
        /// clumps are already the bright yellow-green of the reference art, which the ground
        /// texture is not". True in isolation — the scatter contact sheet, which draws the prefabs
        /// untouched on an untinted tile, shows perfectly good green clumps. But the board does not
        /// draw the ground untouched: <see cref="TerrainTint"/> lifts grass by
        /// <c>(1.04, 1.30, 1.55)</c> to reach the reference green, and against a ground pulled that
        /// far towards blue a tuft that was not pulled at all is a warm object on a cool field. It
        /// reads yellow by comparison, which is why two honest pictures of the same asset
        /// disagreed. The owner looked at the board and called it: greens, with the odd yellow.</para>
        ///
        /// <para><b>And until now this table did nothing whatsoever.</b> The tint is written to
        /// <c>_BaseColor</c>, and <c>Synty/Foliage</c> — the shader the clumps actually use — does
        /// not declare it. A tint aimed at a property a shader does not have fails silently, so
        /// every value ever put here was decorative and the tufts always drew in the pack's own
        /// colour. <c>MaterialCache.GradeSyntyFoliage</c> is what makes it a real lever, and
        /// <c>TintProbe</c> is the instrument that found it by enumerating what the shader declares
        /// rather than guessing at names.</para>
        ///
        /// <para><b>Red is the lever, not blue.</b> The shader's own
        /// <c>_Leaf_Noise_Large_Color</c> is <c>(0.50, 0.58, 0.06)</c>, and what makes that read as
        /// straw is the red sitting almost as high as the green while the blue is nearly nothing.
        /// Green is a low-blue colour too, so lifting blue is a weak handle — multiplying 0.06 by
        /// two is still 0.12. Bringing red down is what turns yellow-green into green, and it is
        /// why these multipliers look lopsided.</para>
        ///
        /// <para><b>Neutral since the look pass (owner, 2026-09-24: "Synty's colours").</b> The
        /// lopsided multipliers above were tuned against the pack's own shader, which made the
        /// straw green. <c>Odyssey/Foliage</c> draws the art's own flat colour scheme and multiplies
        /// this tint straight onto it, so a blue lifted 2.2 times turned two tuft variants in three
        /// <b>teal</b> — the fault the owner's first look at the meadow showed. Now a whisper of
        /// variety between modules and no more; the colour is the art's.</para>
        /// </summary>
        static readonly Color[] FoliageTints =
        {
            new Color(1.00f, 1.00f, 1.00f),            // 0 the art's own colour
            new Color(0.94f, 0.97f, 0.92f),            // 1 a shade deeper, so a field is not one note
            new Color(1.04f, 1.02f, 0.94f),            // 2 a shade warmer
        };

        /// <summary>How many tints a tuft can wear. One per clump module, so variety costs no draws.</summary>
        public static int FoliageTintCount => FoliageTints.Length;

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

        /// <summary>
        /// Bedding. Not pure white: nothing else in the world is, and a 1.0 surface under the
        /// golden hour blows out and reads as a light source rather than as cloth. This is a warm
        /// off-white, the colour of an unbleached sheet.
        /// </summary>
        public static readonly Color Linen = new Color(0.93f, 0.92f, 0.88f, 1f);

        /// <summary>The terrain def name for a terrain index, for the module id.</summary>
        public static string TerrainName(int terrain) =>
            terrain >= 0 && terrain < NaturalContent.TerrainCount
                ? NaturalContent.TerrainAt((ushort)terrain).defName
                : "Rock";
    }
}
