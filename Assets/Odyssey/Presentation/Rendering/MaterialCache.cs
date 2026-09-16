#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// One cached material per (art material, tint, emission), created once and shared for ever.
    ///
    /// This is the committed tint strategy from <c>e-04-tint-strategy.md</c>, and the reason it is
    /// this and not something cleverer is worth restating: <c>Synty/Generic_Basic</c> declares
    /// every property in the per-material CBUFFER, so a per-instance colour is not available
    /// inside a <c>RenderMeshInstanced</c> call and a per-renderer MaterialPropertyBlock would
    /// break SRP batching without helping. A handful of materials and buckets keyed by
    /// (mesh, stuff, shade) costs nothing and keeps the stock shaders untouched — which also keeps
    /// the licensed-asset boundary clean, because nothing is forked out of <c>Assets/Synty</c>.
    ///
    /// Depth shading and ghosting are folded into the same key. A cell's geometry does not change
    /// when the slice moves; only which material draws it does, so the instance buckets are built
    /// once and the shade is chosen at draw time.
    /// </summary>
    public sealed class MaterialCache
    {
        readonly struct Key : IEquatable<Key>
        {
            readonly Material _base;
            readonly uint _tint;
            readonly uint _emission;
            readonly bool _ghost;

            public Key(Material baseMaterial, uint tint, uint emission, bool ghost)
            {
                _base = baseMaterial;
                _tint = tint;
                _emission = emission;
                _ghost = ghost;
            }

            public bool Equals(Key other) =>
                ReferenceEquals(_base, other._base) && _tint == other._tint &&
                _emission == other._emission && _ghost == other._ghost;

            public override bool Equals(object? obj) => obj is Key other && Equals(other);

            public override int GetHashCode() =>
                unchecked(((System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_base) * 397) ^ (int)_tint) * 397 ^ (int)_emission) * 397 ^ (_ghost ? 1 : 0);
        }

        readonly Dictionary<Key, Material> _cache = new Dictionary<Key, Material>();
        readonly List<Material> _owned = new List<Material>();
        Material? _ghostBase;

        public int MaterialCount => _owned.Count;

        /// <summary>
        /// The material that draws a bucket: the art's own material, tinted and shaded.
        ///
        /// <paramref name="ghost"/> switches to the translucent stand-in instead of the art
        /// material, which is deliberate. The design document calls for the ghosted layer to be a
        /// different material rather than a shader branch, and the pack shaders are alpha-clipped
        /// Shader Graphs that cannot be turned transparent from script without forking them.
        /// </summary>
        /// <summary>
        /// Alpha cutoff for foliage, applied to every cloned foliage material.
        ///
        /// The pack's grass is a colour cutout imported without alpha-is-transparency, so its
        /// mipmaps blend blade texels with the background behind the alpha: at distance the
        /// grass goes darker and thinner at once, and the pack's own cutoff of 0.25 then discards
        /// what is left. Lowering the cutoff keeps the far blades — a tunable, and the first lever
        /// to pull when the meadow darkens towards the horizon.
        /// </summary>
        public static float FoliageClipThreshold { get; set; } = 0.12f;

        static readonly int AlphaClipThresholdId = Shader.PropertyToID("_Alpha_Clip_Threshold");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        /// <param name="foliage">Clone with the softer foliage cutoff. Foliage materials are never
        /// shared with anything else, so the base material's identity keeps the cache key honest.</param>
        public Material Get(Material baseMaterial, Color tint, Color emission, bool ghost, float alpha,
            bool foliage = false)
        {
            Material source = ghost ? GhostBase : baseMaterial;
            var colour = new Color(tint.r, tint.g, tint.b, ghost ? alpha : 1f);
            // Keyed on the material reference rather than its instance id: identity is what we
            // actually mean, and it avoids an API whose name changed between Unity versions.
            var key = new Key(source, Pack(colour), Pack(emission), ghost);
            if (_cache.TryGetValue(key, out Material cached)) return cached;

            var material = new Material(source)
            {
                name = source.name + (ghost ? "/ghost#" : "/tint#") + Pack(colour).ToString("x8"),
                enableInstancing = true,
            };
            SetColour(material, colour);
            SetEmission(material, emission);
            if (ghost) MakeTransparent(material);
            if (foliage)
            {
                if (material.HasProperty(AlphaClipThresholdId)) material.SetFloat(AlphaClipThresholdId, FoliageClipThreshold);
                if (material.HasProperty(CutoffId)) material.SetFloat(CutoffId, FoliageClipThreshold);
            }

            _cache.Add(key, material);
            _owned.Add(material);
            return material;
        }

        /// <summary>
        /// How bright a tint the cache key can tell apart. Tints are not confined to 0..1: a
        /// terrain texture can be *lifted* as well as darkened, so grass carries a multiplier above
        /// one. Clamping the key at one would hand two different bright tints the same key, and
        /// the first material built would then be handed out for both — one terrain drawn in
        /// another's colour, everywhere, with nothing on screen to identify it as a fault.
        /// </summary>
        const float KeyRange = 4f;

        static uint Quantise(float v) =>
            (uint)Mathf.RoundToInt(Mathf.Clamp01(v / KeyRange) * 255f);

        static uint Pack(Color c) =>
            (Quantise(c.r) << 24) | (Quantise(c.g) << 16) | (Quantise(c.b) << 8) | Quantise(c.a);

        /// <summary>
        /// Both shader families in play multiply a colour over the albedo: URP Lit calls it
        /// <c>_BaseColor</c> and so does <c>Synty/Generic_Basic</c>. Setting whichever exists
        /// keeps this working if a module is ever drawn with a third shader.
        /// </summary>
        static void SetColour(Material material, Color colour)
        {
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, colour);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, colour);
        }

        static void SetEmission(Material material, Color emission)
        {
            bool on = emission.maxColorComponent > 0.001f;
            if (material.HasProperty(EnableEmissionId)) material.SetFloat(EnableEmissionId, on ? 1f : 0f);
            if (material.HasProperty(SyntyEmissionColorId)) material.SetColor(SyntyEmissionColorId, emission);
            if (!material.HasProperty(EmissionColorId)) return;
            material.SetColor(EmissionColorId, emission);
            if (on) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
        }

        static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        Material GhostBase
        {
            get
            {
                if (_ghostBase != null) return _ghostBase;
                Shader? shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _ghostBase = new Material(shader) { name = "Odyssey/Ghost", enableInstancing = true };
                _ghostBase.SetFloat("_Smoothness", 0f);
                _owned.Add(_ghostBase);
                return _ghostBase;
            }
        }

        /// <summary>Destroy every material this cache made. Called when the renderer shuts down.</summary>
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
            _ghostBase = null;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int SyntyEmissionColorId = Shader.PropertyToID("_Emission_Color");
        static readonly int EnableEmissionId = Shader.PropertyToID("_Enable_Emission");
    }
}
