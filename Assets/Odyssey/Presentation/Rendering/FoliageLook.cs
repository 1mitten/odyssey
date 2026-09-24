#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// How Meadow foliage looks when <c>Odyssey/Foliage</c> draws it: which of the art material's
    /// textures go where, and the handful of numbers that are this project's rather than the pack's
    /// (<c>docs/design/38-meadow-overhaul.md</c> §4, M3).
    ///
    /// <para><b>Nothing here is copied from the pack.</b> The art material is read for its texture
    /// slots only — the leaf and trunk albedo — because those are the art; every colour, strength
    /// and threshold below is ours, chosen against the owner's grass interview and the play camera.
    /// The pack's own colour controls (base, noise, frosting) belong to its shader and are not
    /// carried over.</para>
    /// </summary>
    public static class FoliageLook
    {
        static readonly int SourceLeafId = Shader.PropertyToID("_Leaf_Texture");
        static readonly int SourceTrunkId = Shader.PropertyToID("_Trunk_Texture");

        static readonly int LeafMapId = Shader.PropertyToID("_LeafMap");
        static readonly int TrunkMapId = Shader.PropertyToID("_TrunkMap");
        static readonly int LeafGradeId = Shader.PropertyToID("_LeafGrade");
        static readonly int TrunkGradeId = Shader.PropertyToID("_TrunkGrade");
        static readonly int WindResponseId = Shader.PropertyToID("_WindResponse");
        static readonly int FlutterId = Shader.PropertyToID("_Flutter");
        static readonly int ClearableId = Shader.PropertyToID("_Clearable");
        static readonly int ThinnableId = Shader.PropertyToID("_Thinnable");

        // The art's colour scheme, as the pack's own foliage shader names it. Read at runtime off
        // the art material, exactly as its textures are, and never written into this repository.
        static readonly int SourceFlatId = Shader.PropertyToID("_Leaf_Flat_Color");
        static readonly int SourceUseNoiseId = Shader.PropertyToID("_Use_Color_Noise");
        static readonly int SourceBaseId = Shader.PropertyToID("_Leaf_Base_Color");
        static readonly int SourceNoiseId = Shader.PropertyToID("_Leaf_Noise_Color");
        static readonly int SourceNoiseLargeId = Shader.PropertyToID("_Leaf_Noise_Large_Color");
        static readonly int SourceSmallFreqId = Shader.PropertyToID("_Color_Noise_Small_Freq");
        static readonly int SourceLargeFreqId = Shader.PropertyToID("_Color_Noise_Large_Freq");
        static readonly int SourceFrostOnId = Shader.PropertyToID("_Enable_Frosting");
        static readonly int SourceFrostId = Shader.PropertyToID("_Frosting_Color");
        static readonly int SourceTrunkBaseId = Shader.PropertyToID("_Trunk_Base_Color");

        static readonly int LeafFlatId = Shader.PropertyToID("_LeafFlat");
        static readonly int LeafBaseId = Shader.PropertyToID("_LeafBase");
        static readonly int LeafNoiseId = Shader.PropertyToID("_LeafNoise");
        static readonly int LeafNoiseLargeId = Shader.PropertyToID("_LeafNoiseLarge");
        static readonly int LeafNoiseAmountId = Shader.PropertyToID("_LeafNoiseAmount");
        static readonly int LeafNoiseScaleId = Shader.PropertyToID("_LeafNoiseScale");
        static readonly int LeafBigNoiseAmountId = Shader.PropertyToID("_LeafBigNoiseAmount");
        static readonly int LeafBigNoiseScaleId = Shader.PropertyToID("_LeafBigNoiseScale");
        static readonly int FrostOnId = Shader.PropertyToID("_Frost");
        static readonly int FrostColourId = Shader.PropertyToID("_FrostColour");
        static readonly int TrunkBaseId = Shader.PropertyToID("_TrunkBase");

        /// <summary>
        /// The leaf grade, a linear multiplier on the art's colour.
        ///
        /// <para><b>Neutral since the look pass (owner, 2026-09-24): "Synty's colours".</b> M3 lifted
        /// the texture towards the spring lime the 2026-09-22 interview asked for; the owner then
        /// set the Meadow screenshots as the target, and their colour is the art's own flat-colour
        /// scheme (<see cref="Dress"/>), not the texture. Kept as the one lever if the whole field
        /// needs moving.</para>
        /// </summary>
        public static Vector4 LeafGrade { get; set; } = Vector4.one;

        /// <summary>The trunk grade. Grass has no trunk; bark keeps its colour until M5 decides.</summary>
        public static Vector4 TrunkGrade { get; set; } = Vector4.one;

        /// <summary>How much of the global wind a clump takes. <c>WindDirector.Strength</c> was
        /// tuned for metre-tall code blades; the Meadow clumps bend about the same, so half of it
        /// keeps a gust at about thirteen degrees at the tip.</summary>
        public static float GrassWindResponse { get; set; } = 0.5f;

        /// <summary>How far a blade tip flutters, in metres.</summary>
        public static float GrassFlutter { get; set; } = 0.03f;

        /// <summary>
        /// Whether a material is Meadow foliage this shader knows how to draw: it has a leaf texture
        /// slot, and the slot is filled. That is the pack's foliage shader's own input, so it is
        /// asked of the material rather than of a module's shape — a crop carries the foliage tint
        /// too, and is PolygonFarm art with no leaf slot, so it is left exactly as it was.
        /// </summary>
        public static bool IsMeadowFoliage(Material? source) =>
            source != null && source.HasProperty(SourceLeafId) && source.GetTexture(SourceLeafId) != null;

        /// <summary>
        /// Fill a clone of <c>Odyssey/Foliage</c> from a Meadow material: its leaf and trunk
        /// textures, and this project's grade, wind and clearing.
        /// </summary>
        public static void Dress(Material material, Material source)
        {
            Texture? leaf = source.HasProperty(SourceLeafId) ? source.GetTexture(SourceLeafId) : null;
            Texture? trunk = source.HasProperty(SourceTrunkId) ? source.GetTexture(SourceTrunkId) : null;

            material.SetTexture(LeafMapId, leaf);
            // A leaf-only material (grass) leaves its trunk slot empty; white keeps the shader's
            // trunk branch harmless if a stray vertex reads as bark.
            material.SetTexture(TrunkMapId, trunk != null ? trunk : Texture2D.whiteTexture);
            material.SetVector(LeafGradeId, LeafGrade);
            material.SetVector(TrunkGradeId, TrunkGrade);
            material.SetFloat(WindResponseId, GrassWindResponse);
            material.SetFloat(FlutterId, GrassFlutter);
            material.SetFloat(ClearableId, 1f);
            // Grass and flowers thin with distance (design 38 §21); trees never do.
            material.SetFloat(ThinnableId, 1f);
            CopyColours(material, source);
        }

        /// <summary>How much of the wind a tree's crown takes: a crown is stiffer than grass.</summary>
        public static float TreeWindResponse { get; set; } = 0.18f;

        /// <summary>How far a leaf in a crown flutters, in metres.</summary>
        public static float TreeFlutter { get; set; } = 0.04f;

        /// <summary>How far a crown's normals are pulled towards straight up; grass takes the
        /// shader's default, which is far more.</summary>
        public static float TreeNormalUp { get; set; } = 0.2f;

        static readonly int NormalUpId = Shader.PropertyToID("_NormalUp");

        /// <summary>
        /// Fill a clone of <c>Odyssey/Foliage</c> for a tree or bush: as <see cref="Dress"/>, but a
        /// crown sways less than a blade, and it does not shrink out of the way of an item under it
        /// — a canopy is not in front of what lies beneath it; the see-through fade handles a
        /// canopy between the camera and a colonist.
        /// </summary>
        public static void DressTree(Material material, Material source)
        {
            Dress(material, source);
            material.SetFloat(WindResponseId, TreeWindResponse);
            material.SetFloat(FlutterId, TreeFlutter);
            material.SetFloat(ClearableId, 0f);
            material.SetFloat(ThinnableId, 0f);
            // A crown shaded by its own leaves' normals, not pulled flat towards the sky the way
            // a grass clump is: from above a crown has a lit side and a shadowed one, and pulled
            // up it read as one flat blob of colour.
            material.SetFloat(NormalUpId, TreeNormalUp);
            if (source.HasProperty(CutoffSourceId)) material.SetFloat(CutoffId, Mathf.Max(0.25f, source.GetFloat(CutoffSourceId)));
        }

        static readonly int CutoffSourceId = Shader.PropertyToID("_Alpha_Clip_Threshold");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        /// <summary>
        /// The art's own colouring onto our material: whether its leaves are a flat colour, the
        /// three colours and two frequencies of its world noise, its frosting and its bark colour.
        /// A material without the property keeps our default, which draws the texture as it is.
        /// </summary>
        static void CopyColours(Material material, Material source)
        {
            bool flat = source.HasProperty(SourceFlatId) && source.GetFloat(SourceFlatId) > 0.5f;
            bool noise = !source.HasProperty(SourceUseNoiseId) || source.GetFloat(SourceUseNoiseId) > 0.5f;
            material.SetFloat(LeafFlatId, flat ? 1f : 0f);
            if (source.HasProperty(SourceBaseId)) material.SetColor(LeafBaseId, source.GetColor(SourceBaseId));
            Color baseColour = material.GetColor(LeafBaseId);
            material.SetColor(LeafNoiseId, noise && source.HasProperty(SourceNoiseId)
                ? source.GetColor(SourceNoiseId) : baseColour);
            material.SetColor(LeafNoiseLargeId, noise && source.HasProperty(SourceNoiseLargeId)
                ? source.GetColor(SourceNoiseLargeId) : baseColour);
            material.SetFloat(LeafNoiseAmountId, 1f);
            material.SetFloat(LeafBigNoiseAmountId, 1f);
            if (source.HasProperty(SourceSmallFreqId)) material.SetFloat(LeafNoiseScaleId, source.GetFloat(SourceSmallFreqId));
            if (source.HasProperty(SourceLargeFreqId)) material.SetFloat(LeafBigNoiseScaleId, source.GetFloat(SourceLargeFreqId));
            bool frost = source.HasProperty(SourceFrostOnId) && source.GetFloat(SourceFrostOnId) > 0.5f;
            material.SetFloat(FrostOnId, frost ? 1f : 0f);
            if (frost && source.HasProperty(SourceFrostId)) material.SetColor(FrostColourId, source.GetColor(SourceFrostId));
            if (source.HasProperty(SourceTrunkBaseId)) material.SetColor(TrunkBaseId, source.GetColor(SourceTrunkBaseId));
        }
    }
}
