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
            readonly bool _foliage;
            readonly bool _water;
            readonly bool _unlit;
            readonly bool _ours;

            public Key(Material baseMaterial, uint tint, uint emission, bool ghost, bool foliage, bool water,
                bool unlit = false, bool ours = false)
            {
                _ours = ours;
                _base = baseMaterial;
                _tint = tint;
                _emission = emission;
                _ghost = ghost;
                _foliage = foliage;
                _water = water;
                _unlit = unlit;
            }

            // Foliage is part of the key because a foliage clone carries a queue and a cutoff a
            // plain clone of the same art must not. In the game no material is both, but a key
            // that only holds by convention is one a test cannot trust.
            public bool Equals(Key other) =>
                ReferenceEquals(_base, other._base) && _tint == other._tint &&
                _emission == other._emission && _ghost == other._ghost &&
                _unlit == other._unlit && _ours == other._ours &&
                _foliage == other._foliage && _water == other._water;

            public bool Foliage => _foliage;

            public override bool Equals(object? obj) => obj is Key other && Equals(other);

            public override int GetHashCode() =>
                unchecked(((System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_base) * 397) ^ (int)_tint) * 397 ^ (int)_emission) * 397 ^ (_ghost ? 1 : 0) ^ (_foliage ? 1 << 30 : 0) ^ (_water ? 1 << 29 : 0) ^ (_ours ? 1 << 28 : 0);
        }

        readonly Dictionary<Key, Material> _cache = new Dictionary<Key, Material>();
        readonly List<Material> _owned = new List<Material>();
        Material? _ghostBase;
        Material? _unlitBase;
        Material? _waterBase;
        Material? _foliageBase;
        bool _foliageBaseMissing;
        TreeMaterials? _trees;

        public int MaterialCount => _owned.Count;

        /// <summary>
        /// The tree cache, which has a key of its own because a tree's colour is four colours.
        ///
        /// <para>It is <b>owned</b> here rather than being a second object passed around beside
        /// this one. Everything that draws — the chunk renderer, the surround — already holds a
        /// material cache and would otherwise have to be handed a tree cache as well, and
        /// disposal, which is what stops a session leaking GPU objects until the device resets,
        /// would then have two places to go wrong. What is deliberately <em>not</em> shared is the
        /// key: see <see cref="TreeMaterials"/> for why widening this one would change the cost
        /// model of every wall in the world.</para>
        /// </summary>
        public TreeMaterials Trees => _trees ??= new TreeMaterials();

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
        /// grass goes thinner, and the pack's own cutoff of 0.25 then discards what is left.
        /// Lowering the cutoff keeps the far blades. It is *not* a lever on the meadow darkening
        /// towards the horizon: <c>MeadowCheck</c> photographed 0.12 against 0.25 and the two are
        /// indistinguishable. That darkness was the outline ink, see <see cref="FoliageQueue"/>.
        /// </summary>
        public static float FoliageClipThreshold { get; set; } = 0.12f;

        /// <summary>
        /// The render queue foliage is drawn in: the first slot past the opaque range, so it is
        /// drawn after the depth copy and after the outline pass, and before anything ghosted.
        ///
        /// **Why foliage is drawn late: grass is never inked.** The outline pass draws on every
        /// depth step it finds in the camera depth texture, and a clump of grass is nothing but
        /// depth steps — a fan of cut-out cards at every angle. Inked, a tuft wears a black cap
        /// and a rim as wide as itself at board distance, and the sliver test in the shader only
        /// ever reached the narrowest of them. <c>MeadowCheck</c> has the pictures: the
        /// <c>opaque</c> condition is what the field looks like with the grass under the ink.
        ///
        /// Drawing foliage in the transparent range costs nothing and needs no mask: the depth
        /// texture the outline reads is copied after the opaques, so grass is simply not in it,
        /// and the tufts are then drawn over the inked picture with their own depth test intact.
        /// The material keeps its depth write and its alpha clip, so nothing about how a tuft
        /// looks or occludes changes — only when it is drawn. The queue sits below the ghost
        /// material's, so a ghosted storey above still blends over the grass beneath it.
        ///
        /// Settable only so that a diagnostic can photograph the alternative; the game runs on
        /// <see cref="DefaultFoliageQueue"/>.
        /// </summary>
        public static int FoliageQueue { get; set; } = DefaultFoliageQueue;

        public const int DefaultFoliageQueue = (int)RenderQueue.GeometryLast + 1;

        /// <summary>
        /// Whether Meadow foliage is drawn by <c>Odyssey/Foliage</c> rather than by a clone of the
        /// pack's own material (<c>docs/design/38-meadow-overhaul.md</c> §4, M3). On in the game;
        /// settable so one measurement run can price the two against each other, with
        /// <see cref="ForgetFoliage"/> to drop the clones already made.
        /// </summary>
        public static bool OwnFoliageShader { get; set; } = true;

        /// <summary>
        /// Drop every foliage clone, so the next draw builds them again under whatever
        /// <see cref="OwnFoliageShader"/> now says. For a measurement arm. Returns how many went,
        /// so the arm can check it reached something (<c>docs/bug-patterns.md</c> P18).
        /// </summary>
        public int ForgetFoliage()
        {
            var gone = new List<Key>();
            foreach (KeyValuePair<Key, Material> pair in _cache)
                if (pair.Key.Foliage) gone.Add(pair.Key);
            foreach (Key key in gone)
            {
                Material material = _cache[key];
                _cache.Remove(key);
                _owned.Remove(material);
                if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                else UnityEngine.Object.DestroyImmediate(material);
            }
            return gone.Count;
        }

        /// <summary>
        /// Moves foliage to another queue, including every foliage clone this cache has already
        /// built — <see cref="FoliageQueue"/> alone is read only when a clone is made, so setting
        /// it on a running world moves nothing. For a measurement arm, not for the game.
        ///
        /// <para>Returns how many clones were moved, so the caller can check the change reached
        /// something. A control that never applied is how the culling proof read the same shot
        /// twice and called it a comparison (<c>docs/bug-patterns.md</c> P18).</para>
        /// </summary>
        public int RequeueFoliage(int queue)
        {
            FoliageQueue = queue;
            int moved = 0;
            foreach (KeyValuePair<Key, Material> pair in _cache)
            {
                if (!pair.Key.Foliage) continue;
                pair.Value.renderQueue = queue;
                moved++;
            }
            return moved;
        }

        static readonly int AlphaClipThresholdId = Shader.PropertyToID("_Alpha_Clip_Threshold");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        /// <param name="foliage">Clone with the softer foliage cutoff and the late queue. Part of
        /// the cache key, so the same art asked for plain and as foliage gives two clones.</param>
        /// <param name="water">
        /// Draw with <c>Odyssey/Water</c> instead of the art material, and take the opacity from
        /// the tint's own alpha. Water replaces its source material the way a ghost does, rather
        /// than tinting one, because what makes water look like water is the shading and not the
        /// colour: ripples, a sun glint, a Fresnel-weighted reflection and a shore that fades
        /// against the depth of the bed behind it. None of that can be reached by tinting a Synty
        /// ground tile, and a clone without the packs draws exactly the same water as one with.
        /// </param>
        public Material Get(Material baseMaterial, Color tint, Color emission, bool ghost, float alpha,
            bool foliage = false, bool water = false, bool unlit = false)
        {
            // Meadow foliage is drawn by our own shader from the art's textures (§4 of design 38);
            // anything else foliage-tinted — a crop, a pack without a leaf slot — keeps its own.
            bool ours = foliage && !ghost && !unlit && !water && OwnFoliageShader
                        && FoliageLook.IsMeadowFoliage(baseMaterial) && FoliageBase != null;
            Material source = unlit ? UnlitBase : ghost ? GhostBase : water ? WaterBase
                : ours ? FoliageBase! : baseMaterial;
            var colour = new Color(tint.r, tint.g, tint.b, ghost || unlit ? alpha : water ? tint.a : 1f);
            // Keyed on the material reference rather than its instance id: identity is what we
            // actually mean, and it avoids an API whose name changed between Unity versions.
            // Keyed on the art material when ours draws it, or every Meadow material would share
            // the first one's textures.
            var key = new Key(ours ? baseMaterial : source, Pack(colour), Pack(emission), ghost, foliage,
                water, unlit, ours);
            if (_cache.TryGetValue(key, out Material cached)) return cached;

            var material = new Material(source)
            {
                name = source.name + (ghost ? "/ghost#" : "/tint#") + Pack(colour).ToString("x8"),
                enableInstancing = true,
            };
            SetColour(material, colour);
            SetEmission(material, emission);
            if (ghost) MakeTransparent(material, premultiplied: true);
            else if (unlit && colour.a < 1f) MakeTransparent(material, premultiplied: false);
            if (foliage)
            {
                if (material.HasProperty(AlphaClipThresholdId)) material.SetFloat(AlphaClipThresholdId, FoliageClipThreshold);
                if (material.HasProperty(CutoffId)) material.SetFloat(CutoffId, FoliageClipThreshold);
                material.renderQueue = FoliageQueue;
                if (ours) FoliageLook.Dress(material, baseMaterial);
                else GradeSyntyFoliage(material, source, colour);
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

        /// <summary>
        /// The three colours <c>Synty/Foliage</c> actually builds a leaf out of.
        ///
        /// <para>It is a <em>procedural</em> shader: there is no albedo texture to tint and no
        /// <c>_BaseColor</c> to multiply. The leaf colour is mixed from a dark base and two noise
        /// colours, and those are the only handles on it there are.</para>
        /// </summary>
        static readonly int[] SyntyLeafColourIds =
        {
            Shader.PropertyToID("_Leaf_Base_Color"),
            Shader.PropertyToID("_Leaf_Noise_Color"),
            Shader.PropertyToID("_Leaf_Noise_Large_Color"),
        };

        /// <summary>
        /// Grade a Synty foliage material by the tint, on the properties it really has.
        ///
        /// <para><b>Why this exists, and why it was invisible.</b> <see cref="SetColour"/> writes
        /// <c>_BaseColor</c> and <c>_Color</c>, which is right for URP Lit and for the pack's
        /// ordinary materials. <c>Synty/Foliage</c> declares neither, so every value ever put in
        /// the foliage tint table did exactly nothing — a tint aimed at a property a shader does
        /// not declare fails silently, with the art drawing in its own colour and nothing anywhere
        /// reporting a problem. The table looked like a working lever and was not, which is why
        /// three separate explanations for yellow grass were reasoned out and all three were wrong.
        /// <c>TintProbe</c> is the instrument that settled it by enumerating what the shader really
        /// declares instead of guessing at names.</para>
        ///
        /// <para><b>Multiplied, not set.</b> These three carry the art — the clumps are straw
        /// because <c>_Leaf_Noise_Large_Color</c> is <c>(0.50, 0.58, 0.06)</c> and that blue is
        /// near zero — so replacing them would throw the pack's work away and flatten every clump
        /// to one colour. Multiplying grades what the artist made, which is what every tint table
        /// in this file is written to mean.</para>
        ///
        /// <para>Alpha is left alone. On this shader it is not opacity; the cutout comes from the
        /// leaf texture and <see cref="FoliageClipThreshold"/>.</para>
        /// </summary>
        static void GradeSyntyFoliage(Material material, Material source, Color colour)
        {
            foreach (int id in SyntyLeafColourIds)
            {
                if (!material.HasProperty(id) || !source.HasProperty(id)) continue;

                Color art = source.GetColor(id);
                material.SetColor(id, new Color(
                    art.r * colour.r, art.g * colour.g, art.b * colour.b, art.a));
            }
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

        static void MakeTransparent(Material material, bool premultiplied = true)
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
            if (premultiplied) material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>
        /// The material every Meadow foliage clone is made from, or null when <c>Odyssey/Foliage</c>
        /// is not there — a player build that stripped it — in which case the pack's own material
        /// draws as it did before M3 rather than nothing drawing at all.
        /// </summary>
        Material? FoliageBase
        {
            get
            {
                if (_foliageBase != null || _foliageBaseMissing) return _foliageBase;
                Shader? shader = Shader.Find("Odyssey/Foliage");
                if (shader == null)
                {
                    _foliageBaseMissing = true;
                    Debug.LogWarning("Odyssey/Foliage shader not found; Meadow foliage draws with the pack's own shader.");
                    return null;
                }
                _foliageBase = new Material(shader) { name = "Odyssey/Foliage", enableInstancing = true };
                _owned.Add(_foliageBase);
                return _foliageBase;
            }
        }

        /// <summary>
        /// The one material a flat-colour overlay is cloned from: URP's unlit shader, so the
        /// colour is the colour on every tilt and under every light. A lit overlay - the ghost
        /// path - shades each differently-tilted tile differently, and on rolled ground that
        /// read as per-tile borders however seamless the geometry (the growing-zone cover,
        /// 2026-09-20: fixed twice geometrically before the light was caught).
        /// </summary>
        Material UnlitBase
        {
            get
            {
                if (_unlitBase != null) return _unlitBase;
                Shader? shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                _unlitBase = new Material(shader) { name = "Odyssey/Flat", enableInstancing = true };
                _owned.Add(_unlitBase);
                return _unlitBase;
            }
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

        /// <summary>
        /// The one material every water tile is cloned from.
        ///
        /// If <c>Odyssey/Water</c> is missing — a stripped build, a shader that failed to compile
        /// — this falls back to the ghost's Lit material made transparent, so the water is a flat
        /// blue-grey pane rather than magenta. That matters more than it sounds: a shader error
        /// that renders as magenta gets fixed, and one that renders as *nothing* gets shipped.
        /// </summary>
        Material WaterBase
        {
            get
            {
                if (_waterBase != null) return _waterBase;

                Shader? shader = Shader.Find("Odyssey/Water");
                if (shader != null)
                {
                    _waterBase = new Material(shader) { name = "Odyssey/Water", enableInstancing = true };
                    _owned.Add(_waterBase);
                    return _waterBase;
                }

                Debug.LogWarning("Odyssey/Water shader not found; water will draw as flat translucency.");
                shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _waterBase = new Material(shader) { name = "Odyssey/Water(fallback)", enableInstancing = true };
                MakeTransparent(_waterBase);
                _owned.Add(_waterBase);
                return _waterBase;
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
            _waterBase = null;
            _foliageBase = null;
            _foliageBaseMissing = false;
            _trees?.Dispose();
            _trees = null;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int SyntyEmissionColorId = Shader.PropertyToID("_Emission_Color");
        static readonly int EnableEmissionId = Shader.PropertyToID("_Enable_Emission");
    }
}
