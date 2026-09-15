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
            readonly int _baseId;
            readonly uint _tint;
            readonly uint _emission;
            readonly bool _ghost;

            public Key(int baseId, uint tint, uint emission, bool ghost)
            {
                _baseId = baseId;
                _tint = tint;
                _emission = emission;
                _ghost = ghost;
            }

            public bool Equals(Key other) =>
                _baseId == other._baseId && _tint == other._tint &&
                _emission == other._emission && _ghost == other._ghost;

            public override bool Equals(object? obj) => obj is Key other && Equals(other);

            public override int GetHashCode() =>
                unchecked((((_baseId * 397) ^ (int)_tint) * 397 ^ (int)_emission) * 397 ^ (_ghost ? 1 : 0));
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
        public Material Get(Material baseMaterial, Color tint, Color emission, bool ghost, float alpha)
        {
            Material source = ghost ? GhostBase : baseMaterial;
            var colour = new Color(tint.r, tint.g, tint.b, ghost ? alpha : 1f);
            var key = new Key(source.GetInstanceID(), Pack(colour), Pack(emission), ghost);
            if (_cache.TryGetValue(key, out Material cached)) return cached;

            var material = new Material(source)
            {
                name = source.name + (ghost ? "/ghost#" : "/tint#") + Pack(colour).ToString("x8"),
                enableInstancing = true,
            };
            SetColour(material, colour);
            SetEmission(material, emission);
            if (ghost) MakeTransparent(material);

            _cache.Add(key, material);
            _owned.Add(material);
            return material;
        }

        static uint Pack(Color c) =>
            ((uint)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f) << 24) |
            ((uint)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f) << 16) |
            ((uint)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f) << 8) |
            (uint)Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f);

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
