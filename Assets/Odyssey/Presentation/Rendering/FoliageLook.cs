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

        /// <summary>
        /// The leaf grade, a linear multiplier on the art's colour.
        ///
        /// <para>The grass interview (2026-09-22, answer 3) asked for a <b>lighter, yellower spring
        /// green</b> than the art. The Meadow grass textures average about sRGB (88, 112, 48) where
        /// they are opaque — a dark olive — so the grade lifts all three channels and red and green
        /// more than blue, which moves the hue towards lime as well as lightening it. A first
        /// setting, to be judged at the play camera; it is one number because it is meant to be
        /// turned.</para>
        /// </summary>
        public static Vector4 LeafGrade { get; set; } = new Vector4(1.9f, 2.1f, 1.3f, 1f);

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
        }
    }
}
