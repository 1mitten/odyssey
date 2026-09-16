#nullable enable
using System.IO;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// The golden hour: one place that owns what the light, the sky, the fog and the grade are,
    /// so that they cannot disagree with each other.
    ///
    /// <para><b>Why they are together.</b> The whole effect rests on one identity — the colour the
    /// distance fades to is the colour the sky is at the horizon — and that identity is invisible
    /// if the two numbers live in different files. It was two numbers that happened to match, and
    /// the next person to warm the sky would have left the fog behind and wondered why the hills
    /// no longer dissolved. Here the sky is *given* the fog colour, and the join is right by
    /// construction rather than by coincidence.</para>
    ///
    /// <para><b>Where it came from.</b> Owner interview 2026-09-16
    /// (<c>docs/research/look-interview.md</c>) against six reference screenshots, and five
    /// research files. Two recorded decisions are deliberately overturned and must not be
    /// re-argued from the old comments: the 72-degree sun comes down to a raking 30, and bloom is
    /// adopted. On the first, the research found the cause the old decision never tested — a
    /// raking sun was rejected for darkening the ground, but the darkness was the *shadow
    /// strength*, not the angle, and a shadow that keeps the key light's hue costs nothing.</para>
    ///
    /// <para><b>What is not here.</b> The sun shafts and the tilt-shift blur. Both need a
    /// measurement first: the shafts can be geometrically impossible at the default framing, since
    /// at a 48-degree downward pitch the sun can sit behind the camera where a radial blur has
    /// nothing to radiate from. Neither is in this pass, and the panel has no switch for them.</para>
    /// </summary>
    public static class GoldenHour
    {
        // ---------------------------------------------------------------- the hour

        /// <summary>
        /// The light the scene is built at.
        ///
        /// <para><b>The palette itself is no longer here.</b> It moved to
        /// <see cref="Daylight"/> in the Presentation assembly when the fixed golden hour became
        /// a day, because a cycle has to sample it at runtime and editor code cannot be called
        /// from a running game. This class keeps the two jobs that are genuinely the editor's:
        /// baking one hour into the scene so a fresh open is lit before anything ticks, and
        /// writing the grade profile.</para>

        /// <para>Leaving a copy of the numbers behind would have been the worst of both: two
        /// palettes that agree until somebody edits one.</para>
        /// </summary>
        public static DaylightState Baked => Daylight.Sample(Daylight.DefaultHour);
        // ---------------------------------------------------------------- shadows

        /// <summary>
        /// How far shadows are drawn, in metres.
        ///
        /// <para>Fifty was chosen for a steep sun that threw almost nothing. At 30 degrees the
        /// shadows are eight cells long and the visible ground runs from 50 m to 224 m at the
        /// default pitch, so at 50 m the shadows simply stopped a third of the way into the view.
        /// </para>
        /// </summary>
        public const float ShadowDistance = 250f;

        /// <summary>
        /// Where the cascades divide, as fractions of <see cref="ShadowDistance"/>.
        ///
        /// <para><b>These are not the stock splits, and the stock splits were wrong for this
        /// camera.</b> A cascade split is a fraction of distance *from the camera*, and this camera
        /// never sees anything nearer than about 50 m — it is tens of metres in the air looking
        /// down. The default 0.07/0.18/0.42 therefore spent the first two cascades, half the
        /// shadow atlas, on empty air in front of the lens. Starting at 0.30 puts the first split
        /// at 75 m, just past where the ground actually begins.</para>
        /// </summary>
        public static readonly Vector3 CascadeSplits = new Vector3(0.30f, 0.48f, 0.70f);

        /// <summary>
        /// Normal bias does the work at a grazing angle; depth bias is kept low on purpose.
        ///
        /// <para>Acne at a shallow sun is a depth-slope problem, and depth bias answers it by
        /// pushing the whole shadow back along the light — which at 30 degrees slides it a long
        /// way across the ground and lifts every shadow off the foot of the thing casting it.
        /// Normal bias moves the sample sideways along the surface normal instead, which is the
        /// fault's own geometry and does not detach anything.</para>
        /// </summary>
        public const float ShadowDepthBias = 0.5f;

        public const float ShadowNormalBias = 1.4f;

        // ---------------------------------------------------------------- grade

        const string ProfilePath = "Assets/Settings/OdysseyGoldenHour.asset";

        /// <summary>
        /// The post-processing profile, created if absent and rewritten every run.
        ///
        /// <para><b>The project had no volume stack at all until this.</b> The pipeline asset
        /// pointed its default profile at an asset that did not exist, and the one profile in the
        /// repository was referenced by nothing and full of stray editor-test components — so
        /// there has never been tonemapping, grading, bloom or a vignette in this build. Almost
        /// all of the warmth in the references is this, and none of it is a shader.</para>
        ///
        /// <para>Every value is written on each run for the reason the outline's settings are: once
        /// an asset has been saved, its serialised values are what load, so editing a default in C#
        /// changes nothing while a field the asset has never seen does pick up its initialiser.
        /// Half a tuning change lands and half does not, which reads as the maths being wrong.</para>
        ///
        /// <para>Note that the grade is nearly free. Tonemapping, colour adjustments, white balance
        /// and the vignette all fold into one lookup table that is built once at its own small
        /// resolution and applied in a pass that already runs; only bloom is a real pass of its
        /// own. The expensive half of this look is the part that is not here yet.</para>
        /// </summary>
        public static VolumeProfile BuildProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(ProfilePath)!));
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // Neutral, never ACES. ACES is filmic and pulls saturated highlights towards white
            // while skewing their hue, which is precisely what it would do to a warm sunlit roof —
            // the one thing this look cannot afford to lose. Neutral keeps the hue and only maps
            // the range.
            Tonemapping tonemapping = Get<Tonemapping>(profile);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            ColorAdjustments colour = Get<ColorAdjustments>(profile);
            colour.postExposure.overrideState = true;
            colour.postExposure.value = 0.15f;
            colour.contrast.overrideState = true;
            // Gently, and this is a readability lever as much as a taste one: the board is mostly
            // in shadow at this sun angle, and contrast is what would drive those shadows towards
            // black after shadow strength has gone to the trouble of lifting them.
            colour.contrast.value = 6f;
            colour.saturation.overrideState = true;
            colour.saturation.value = 4f;
            colour.colorFilter.overrideState = true;
            colour.colorFilter.value = new Color(1.00f, 0.97f, 0.92f);

            WhiteBalance balance = Get<WhiteBalance>(profile);
            balance.temperature.overrideState = true;
            balance.temperature.value = 7f;
            balance.tint.overrideState = true;
            balance.tint.value = 2f;

            // Adopted against d-09's advice, on the owner's call: the references bloom plainly and
            // the note was written for a painted look this is not. The threshold is above 1 so it
            // catches only what the sun has actually blown out — lit roofs, water glint and the
            // horizon — rather than fogging the whole frame, which is the failure that makes a
            // stylised scene read as a cheap game render.
            Bloom bloom = Get<Bloom>(profile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.1f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.9f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.65f;
            // The dominant cost lever, and it is this rather than the iteration count: the first
            // iteration is at full resolution and dwarfs the rest, so halving the start resolution
            // is worth more than dropping several iterations.
            bloom.downscale.overrideState = true;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.highQualityFiltering.overrideState = true;
            bloom.highQualityFiltering.value = false;

            Vignette vignette = Get<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.16f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.4f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>
        /// Find or create one override, <b>and make it part of the asset on disk</b>.
        ///
        /// <para><b>The trap, and it is silent.</b> A <c>VolumeComponent</c> is a
        /// <c>ScriptableObject</c> in its own right, and <c>VolumeProfile.Add</c> only creates it
        /// in memory. Saved without <see cref="AssetDatabase.AddObjectToAsset"/>, the profile
        /// serialises five entries of <c>{fileID: 0}</c> — an asset that looks right in the
        /// inspector of the session that made it and has no overrides at all in any other. It is
        /// the same fault as a renderer feature appended without being added to its asset, and it
        /// fails the same way: the effect simply never runs, with nothing logged.</para>
        ///
        /// <para>It would have survived every check we have. The editor command that writes the
        /// profile also rebuilds the components in memory, so a screenshot taken in the same run
        /// shows the grade working perfectly. Only a fresh session — or a player build — would
        /// have shown a scene with no grade on it.</para>
        /// </summary>
        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing) && existing != null) return existing;

            T component = profile.Add<T>();
            component.name = typeof(T).Name;
            if (!AssetDatabase.IsSubAsset(component)) AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
