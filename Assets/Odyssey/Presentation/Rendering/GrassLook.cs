#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The numbers that decide what grass looks like, in one place.
    ///
    /// <para><b>Why a table rather than values on the material.</b> They are on the material as
    /// well — <c>Odyssey/Grass</c> declares every one of them, so a material can be inspected and
    /// nudged — but a material built at runtime by <see cref="MaterialCache"/> has no asset for
    /// anybody to author, so something has to say what it starts as. This is that something, and
    /// it is one place rather than seven property assignments scattered through the cache, for the
    /// same reason <c>GoldenHour</c> owns the whole grade: the numbers only make sense together.</para>
    ///
    /// <para><b>Changing one takes effect on the next material built, not on the ones already
    /// cloned.</b> That is fine for the two callers there are — the contact sheet builds a fresh
    /// renderer per condition, and the Look setting will rebuild when it lands — and it is written
    /// down because the alternative reading ("set it and the meadow changes") is the one somebody
    /// will assume.</para>
    /// </summary>
    public static class GrassLook
    {
        /// <summary>
        /// The colour at the root of a blade, where it meets the turf.
        ///
        /// Darker and greyer than the tip by more than looks right in isolation: a clump is read
        /// against the ground it stands in, and a root the same value as the tip makes the whole
        /// thing float. This is the shading the geometry cannot supply, since at board distance a
        /// blade is a pixel and there is no light left to model.
        /// </summary>
        public static Color Root { get; set; } = new Color(0.290f, 0.416f, 0.212f);

        /// <summary>The colour at the tip. Lighter, warmer and more saturated than the root.</summary>
        public static Color Tip { get; set; } = new Color(0.616f, 0.780f, 0.376f);

        /// <summary>
        /// How much of the blade the root colour keeps. Above one it hangs on and the clump reads
        /// as dark with bright ends, which is what the reference art does; at one it is a plain
        /// gradient.
        /// </summary>
        public static float RampBias { get; set; } = 1.6f;

        /// <summary>
        /// How hard the ramp steps: 0 is a gradient, 1 is two flat bands with a line between them.
        ///
        /// <b>This is the grass half of the Look setting</b> — one shader serves both rungs, which
        /// is why it is a number and not a shader keyword.
        /// </summary>
        public static float Banding { get; set; }

        /// <summary>Where the band falls, as a height along the blade. Only read when banding is on.</summary>
        public static float BandHeight { get; set; } = 0.45f;

        /// <summary>
        /// How dark the foot of a blade goes, and how far up that darkening reaches.
        ///
        /// <para>Contact shading, and at a camera looking down from tens of metres it does
        /// more for the read than anything happening at the tip: it is what makes a clump
        /// sit <em>in</em> the ground instead of on it. Distinct from the root colour, which
        /// is a hue — this is a value, and a clump wants both.</para>
        /// </summary>
        public static float BaseShade { get; set; } = 0.66f;

        /// <inheritdoc cref="BaseShade"/>
        public static float BaseShadeHeight { get; set; } = 0.40f;

        /// <summary>
        /// How far the colour drifts between one patch of meadow and the next.
        ///
        /// <para>A field of one green reads as a painted surface however good the blades
        /// are, and <c>b-painted-look-games.md</c> ranked low-frequency colour variation as
        /// the single highest-value change available to this renderer — a year ago, and it
        /// had never been built. It rides the grass shader because that is where it costs
        /// nothing: two sines of the clump's own world position, no texture fetch.</para>
        /// </summary>
        public static float PatchVariation { get; set; } = 0.14f;

        /// <summary>
        /// How brightly a blade lights up with the sun behind it, how tight that response
        /// is, and what colour comes through.
        ///
        /// <para>Fake translucency, and the cheapest thing in the feature that reads as
        /// <em>alive</em>: real grass is thin enough to pass light, and looking across a
        /// field into a low sun is when it stops being green and starts being lit. It is
        /// emission rather than a light term, so it costs a dot product and a power.</para>
        /// </summary>
        public static float Sheen { get; set; } = 0.9f;

        /// <inheritdoc cref="Sheen"/>
        public static float SheenSharpness { get; set; } = 7f;

        /// <inheritdoc cref="Sheen"/>
        public static Color SheenColour { get; set; } = new Color(0.72f, 0.84f, 0.38f);

        /// <summary>
        /// How far the tip leans towards the camera, as a fraction of the blade's length.
        ///
        /// <b>The setting the whole feature turns on at this camera.</b> A blade is a thin upright
        /// card and a camera looking down at 48° sees its top edge; leaning it over turns its face
        /// up towards the lens. At zero a meadow reads as grey fuzz. Past about a half the field
        /// visibly leans in around the camera as you pan, which reads as the grass watching you.
        /// </summary>
        public static float FaceCamera { get; set; } = 0.3f;

        static readonly int RootId = Shader.PropertyToID("_RootColour");
        static readonly int TipId = Shader.PropertyToID("_TipColour");
        static readonly int RampBiasId = Shader.PropertyToID("_RampBias");
        static readonly int BandingId = Shader.PropertyToID("_Banding");
        static readonly int BandHeightId = Shader.PropertyToID("_BandHeight");
        static readonly int FaceCameraId = Shader.PropertyToID("_FaceCamera");
        static readonly int BaseShadeId = Shader.PropertyToID("_BaseShade");
        static readonly int BaseShadeHeightId = Shader.PropertyToID("_BaseShadeHeight");
        static readonly int PatchVariationId = Shader.PropertyToID("_PatchVariation");
        static readonly int SheenId = Shader.PropertyToID("_Sheen");
        static readonly int SheenSharpnessId = Shader.PropertyToID("_SheenSharpness");
        static readonly int SheenColourId = Shader.PropertyToID("_SheenColour");

        /// <summary>Write the table onto a freshly built grass material.</summary>
        public static void Apply(Material material)
        {
            if (!material.HasProperty(RootId)) return;   // the stripped-shader fallback
            material.SetColor(RootId, Root);
            material.SetColor(TipId, Tip);
            material.SetFloat(RampBiasId, RampBias);
            material.SetFloat(BandingId, Banding);
            material.SetFloat(BandHeightId, BandHeight);
            material.SetFloat(FaceCameraId, FaceCamera);
            material.SetFloat(BaseShadeId, BaseShade);
            material.SetFloat(BaseShadeHeightId, BaseShadeHeight);
            material.SetFloat(PatchVariationId, PatchVariation);
            material.SetFloat(SheenId, Sheen);
            material.SetFloat(SheenSharpnessId, SheenSharpness);
            material.SetColor(SheenColourId, SheenColour);
        }

        /// <summary>Back to what the game ships with. For a check harness that changed them.</summary>
        public static void Reset()
        {
            Root = new Color(0.290f, 0.416f, 0.212f);
            Tip = new Color(0.616f, 0.780f, 0.376f);
            RampBias = 1.6f;
            Banding = 0f;
            BandHeight = 0.45f;
            FaceCamera = 0.3f;
            BaseShade = 0.66f;
            BaseShadeHeight = 0.40f;
            PatchVariation = 0.14f;
            Sheen = 0.9f;
            SheenSharpness = 7f;
            SheenColour = new Color(0.72f, 0.84f, 0.38f);
        }
    }
}
