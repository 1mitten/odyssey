#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// One material per (tree art, theme, depth shade), drawing the pack's own atlas through
    /// <c>Odyssey/Tree</c> so that bark and canopy can be repainted separately.
    ///
    /// <para><b>Why not <see cref="MaterialCache"/>.</b> That cache's whole argument is that a
    /// bucket's colour is one tint, and a tree's is four — see <c>TreePalette</c> for why one
    /// cannot express a tree. Widening the shared key with sixteen floats would change the cost
    /// model of every wall in the world to serve the woodland. This is the same idea with its own
    /// key, which is exactly the bargain <see cref="ColonistMaterials"/> already struck for
    /// colonists, and the existing cache and its tests are left as they were.</para>
    ///
    /// <para><b>How many there are.</b> One per theme actually drawn, times the handful of depth
    /// shades the slice produces, times the two tree meshes — a few dozen materials of a few
    /// hundred bytes each, textures by reference. The number that matters is not this one but the
    /// <i>bucket</i> count, which is what draw calls are made of, and that is bounded by
    /// <c>TreeLook</c> dealing a colour to a stand rather than to a tree.</para>
    ///
    /// <para>Materials are owned here and destroyed on <see cref="Dispose"/>, for the reason
    /// <c>ModuleLibrary</c> records: objects Unity allocates on the GPU are not collected, and
    /// letting them accumulate produced a <c>DXGI_ERROR_DEVICE_REMOVED</c> once already.</para>
    /// </summary>
    public sealed class TreeMaterials : IDisposable
    {
        readonly struct Key : IEquatable<Key>
        {
            readonly Material _source;
            readonly int _theme;
            readonly int _shade;
            readonly int _mute;

            public Key(Material source, int theme, int shade, int mute)
            {
                _source = source;
                _theme = theme;
                _shade = shade;
                _mute = mute;
            }

            public bool Equals(Key other) =>
                ReferenceEquals(_source, other._source) && _theme == other._theme &&
                _shade == other._shade && _mute == other._mute;

            public override bool Equals(object? obj) => obj is Key other && Equals(other);

            public override int GetHashCode() => unchecked(
                ((System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_source) * 397 ^ _theme) * 397 ^ _shade) * 397 ^ _mute);
        }

        readonly Dictionary<Key, Material?> _cache = new Dictionary<Key, Material?>();
        readonly List<Material> _owned = new List<Material>();
        Shader? _shader;
        bool _warned;

        /// <summary>How many materials exist. A test watches this so a per-tree clone fails.</summary>
        public int MaterialCount => _owned.Count;

        /// <summary>
        /// Whether trees are recoloured at all. Off draws the pack's own wood, through the pack's
        /// own material, exactly as the game did before this existed.
        ///
        /// <para>Settable only so that a diagnostic can photograph the alternative — the same
        /// bargain <c>MaterialCache.FoliageQueue</c> makes — because the one question a picture of
        /// a recoloured wood cannot answer on its own is whether the recolouring is what changed
        /// it. The game runs with it on.</para>
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// How far the repaint is taken, 0 to 1. At 0 the shader draws the atlas untouched, which
        /// is the fidelity control: it separates "our shader draws a tree differently from Synty's"
        /// from "the palette draws a different tree".
        /// </summary>
        public static float RemapStrength { get; set; } = 1f;

        /// <summary>
        /// Overrides the normal-map strength carried from the pack material, when set.
        ///
        /// A diagnostic, and a pointed one: <c>Odyssey/Tree</c> draws a tree about a tenth darker
        /// than <c>Synty/Generic_Basic</c> draws the same tree, and the normal map is the input
        /// the two shaders are most likely to treat differently. Photographing the wood with it
        /// switched off is one bit of information and settles the question; reading the two
        /// shaders side by side had already produced two wrong answers.
        /// </summary>
        public static float? NormalAmountOverride { get; set; }

        /// <summary>True when <c>Odyssey/Tree</c> is present and a wood can be recoloured.</summary>
        public bool Available => Shader != null;

        Shader? Shader
        {
            get
            {
                if (_shader == null) _shader = UnityEngine.Shader.Find("Odyssey/Tree");
                if (_shader == null && !_warned)
                {
                    _warned = true;
                    // Loudly, and once. A shader that renders magenta gets fixed; one that renders
                    // nothing at all gets shipped — the argument MaterialCache makes about the
                    // water shader, and ColonistMaterials about the character one.
                    Debug.LogWarning("[Odyssey] Odyssey/Tree not found — the wood draws in the " +
                                     "pack's own colours. Check the shader is in the build.");
                }
                return _shader;
            }
        }

        /// <summary>
        /// The material that draws this tree in this theme at this depth shade, or <c>null</c> to
        /// leave the art alone.
        ///
        /// <para>Null rather than a clone that happens to change nothing, whenever there is no
        /// shader, no source material or no atlas to repaint. The caller then draws exactly what
        /// it drew before this existed, which is what keeps a wood the pack shipped looking the
        /// way the artist painted it on a machine where the shader failed to compile.</para>
        /// </summary>
        /// <param name="muteStep">
        /// The surround's distance haze, which desaturates a tint towards its own luminance. It
        /// has to be applied to all four colours rather than to one, so it is part of the key: a
        /// tree standing in the ring outside the board is a different material from the same tree
        /// standing on it. <c>SkirtLayout.MuteSteps</c> is 3, so it costs at most three times the
        /// themes the surround actually samples, and the board itself always passes 0.
        /// </param>
        public Material? For(Material? source, int theme, float shade, int muteStep = 0)
        {
            if (source == null || !Enabled) return null;
            Shader? shader = Shader;
            if (shader == null) return null;

            // RemapStrength is deliberately not in the key: it is a diagnostic, and a diagnostic
            // that changes what a clone holds builds a fresh renderer to photograph it, which is
            // the bargain MeadowCheck already states for the foliage queue.
            var key = new Key(source, theme, Quantise(shade), muteStep);
            if (_cache.TryGetValue(key, out Material? cached)) return cached;

            TreeTheme look = TreePalette.At(theme);
            TreeCells cells = TreeSwatches.For(look.Species);

            var material = new Material(shader)
            {
                name = source.name + "/tree#" + theme.ToString("x2") +
                       "@" + Quantise(shade).ToString("x2") + "~" + muteStep,
                enableInstancing = true,
            };

            if (!Carry(source, material))
            {
                // No atlas on the source means there is nothing to repaint: this is the primitive
                // stand-in a clone without the packs draws. Fall back rather than paint a cube.
                UnityEngine.Object.DestroyImmediate(material);
                _cache[key] = null;
                return null;
            }

            Paint(material, cells, look, shade, muteStep);

            _cache[key] = material;
            _owned.Add(material);
            return material;
        }

        static readonly int AlbedoId = Shader.PropertyToID("_Albedo_Map");
        static readonly int NormalId = Shader.PropertyToID("_Normal_Map");
        static readonly int NormalAmountId = Shader.PropertyToID("_Normal_Amount");
        static readonly int EmissionMapId = Shader.PropertyToID("_Emission_Map");
        static readonly int EmissionColourId = Shader.PropertyToID("_Emission_Color");
        static readonly int EnableEmissionId = Shader.PropertyToID("_Enable_Emission");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int ClipId = Shader.PropertyToID("_Alpha_Clip_Threshold");
        static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        static readonly int RemapStrengthId = Shader.PropertyToID("_RemapStrength");

        static readonly int[] BarkDeepRectIds =
            { Shader.PropertyToID("_BarkDeepRect0"), Shader.PropertyToID("_BarkDeepRect1") };
        static readonly int[] BarkWarmRectIds =
            { Shader.PropertyToID("_BarkWarmRect0"), Shader.PropertyToID("_BarkWarmRect1") };
        static readonly int[] LeafDeepRectIds =
            { Shader.PropertyToID("_LeafDeepRect0"), Shader.PropertyToID("_LeafDeepRect1") };
        static readonly int[] LeafFreshRectIds =
            { Shader.PropertyToID("_LeafFreshRect0"), Shader.PropertyToID("_LeafFreshRect1") };

        static readonly int BarkDeepColourId = Shader.PropertyToID("_BarkDeepColour");
        static readonly int BarkWarmColourId = Shader.PropertyToID("_BarkWarmColour");
        static readonly int LeafDeepColourId = Shader.PropertyToID("_LeafDeepColour");
        static readonly int LeafFreshColourId = Shader.PropertyToID("_LeafFreshColour");

        /// <summary>
        /// Copy the maps and surface values across from the pack's own material. False when there
        /// is no albedo to copy, which is what says this is not tree art at all.
        /// </summary>
        static bool Carry(Material source, Material material)
        {
            Texture? albedo = null;
            foreach (string property in new[] { "_Albedo_Map", "_BaseMap", "_MainTex" })
                if (source.HasProperty(property) && source.GetTexture(property) != null)
                { albedo = source.GetTexture(property); break; }

            if (albedo == null) return false;
            material.SetTexture(AlbedoId, albedo);

            // The queue is carried across, and it is not a detail. Odyssey/Tree declares the
            // AlphaTest queue its cutout belongs in, while the pack's tree material carries an
            // explicit 2000 — so a clone that took the shader's default would draw the wood at a
            // different point in the frame from the wood it replaced. Nothing about occlusion
            // would change, since either queue writes and tests depth inside the opaque range the
            // outline copies its depth from, but "the same tree, drawn later" is exactly the kind
            // of difference that turns TreeCheck's fidelity control into a puzzle.
            material.renderQueue = source.renderQueue;

            if (source.HasProperty(NormalId)) material.SetTexture(NormalId, source.GetTexture(NormalId));
            if (source.HasProperty(NormalAmountId)) material.SetFloat(NormalAmountId, source.GetFloat(NormalAmountId));
            if (NormalAmountOverride.HasValue) material.SetFloat(NormalAmountId, NormalAmountOverride.Value);
            if (source.HasProperty(EmissionMapId)) material.SetTexture(EmissionMapId, source.GetTexture(EmissionMapId));
            if (source.HasProperty(EmissionColourId)) material.SetColor(EmissionColourId, source.GetColor(EmissionColourId));
            if (source.HasProperty(EnableEmissionId)) material.SetFloat(EnableEmissionId, source.GetFloat(EnableEmissionId));
            if (source.HasProperty(MetallicId)) material.SetFloat(MetallicId, source.GetFloat(MetallicId));
            if (source.HasProperty(SmoothnessId)) material.SetFloat(SmoothnessId, source.GetFloat(SmoothnessId));
            if (source.HasProperty(ClipId)) material.SetFloat(ClipId, source.GetFloat(ClipId));
            return true;
        }

        static void Paint(Material material, TreeCells cells, in TreeTheme look, float shade, int muteStep)
        {
            Write(material, BarkDeepRectIds, cells.BarkDeep);
            Write(material, BarkWarmRectIds, cells.BarkWarm);
            Write(material, LeafDeepRectIds, cells.LeafDeep);
            Write(material, LeafFreshRectIds, cells.LeafFresh);

            // Shaded face to the smaller cell, lit face to the larger one: the probe measures the
            // broadleaf's upper canopy at half the mesh, so the lit colour is what the tree reads
            // as and the shaded one is the underside. Naming them mass and accent, which the first
            // version did, is what put a pale highlight over half a tree.
            material.SetColor(BarkDeepColourId, SkirtLayout.Mute(Colour(look.Bark.Shaded), muteStep));
            material.SetColor(BarkWarmColourId, SkirtLayout.Mute(Colour(look.Bark.Lit), muteStep));
            material.SetColor(LeafDeepColourId, SkirtLayout.Mute(Colour(look.Leaf.Shaded), muteStep));
            material.SetColor(LeafFreshColourId, SkirtLayout.Mute(Colour(look.Leaf.Lit), muteStep));

            // The depth shade, carried the way every other bucket carries it. The shader applies
            // it after the repaint, so a tree on a shaded layer is a darker tree rather than a
            // tree whose recoloured half ignored the slice.
            material.SetColor(BaseColourId, new Color(shade, shade, shade, 1f));
            material.SetFloat(RemapStrengthId, RemapStrength);
        }

        /// <summary>
        /// Write up to two rectangles, leaving any the mesh does not use empty.
        ///
        /// An unused rectangle is (1,1,0,0) — minimum above maximum — which no UV can be inside.
        /// That is how a slot is switched off without the shader needing a count or a branch, and
        /// it is what a broadleaf's second bark slot gets, because the art has one trunk colour.
        /// </summary>
        static void Write(Material material, int[] ids, Rect[] rects)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                Vector4 v = i < rects.Length
                    ? new Vector4(rects[i].xMin, rects[i].yMin, rects[i].xMax, rects[i].yMax)
                    : new Vector4(1f, 1f, 0f, 0f);
                material.SetVector(ids[i], v);
            }
        }

        /// <summary>
        /// The palette's bytes as a <see cref="Color"/>, handed over unconverted — the project
        /// renders linear, the table is authored in sRGB hex, and <c>Material.SetColor</c> is what
        /// converts a <c>Color</c> property. Converting here as well would apply it twice and draw
        /// every wood too dark. <c>ColonistMaterials.Colour</c> carries the whole argument.
        /// </summary>
        public static Color Colour(Rgb24 c) => new Color(c.R / 255f, c.G / 255f, c.B / 255f, 1f);

        /// <summary>
        /// How finely the depth shade splits the cache. The slice produces a shade per layer of
        /// drop, so there are only ever a handful of distinct values; quantising keeps a float
        /// that differs in its last bit from minting a second material for the same picture.
        /// </summary>
        static int Quantise(float shade) => Mathf.RoundToInt(Mathf.Clamp01(shade) * 63f);

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(_owned[i]);
                else UnityEngine.Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
            _cache.Clear();
            _shader = null;
        }
    }
}
