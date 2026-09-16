#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// One material per distinct colonist look, drawing the pack's own atlas through
    /// <c>Odyssey/Character</c> so that skin, hair and clothing can be repainted separately.
    ///
    /// <para><b>Why not <see cref="MaterialCache"/>.</b> That cache is keyed on
    /// (art material, tint, emission, ghost, foliage, water) and its whole argument is that a
    /// handful of buckets costs nothing. A colonist look is eight rectangles and four colours;
    /// widening the shared key with thirty-two floats would change the cost model of every wall
    /// in the world to serve fifty people. This is the same idea with its own key, and the
    /// existing cache and its tests are left exactly as they were.</para>
    ///
    /// <para><b>The maps come from the pack material, the shading from ours.</b> A clone keeps the
    /// atlas, the normal map, the emissive and the surface values it was built from, so a
    /// recoloured colonist stands in the same light as the buildings behind it. Nothing is forked
    /// out of <c>Assets/Synty</c>: the property names match so a clone can carry its maps across,
    /// and the shader itself is written from scratch.</para>
    ///
    /// <para><b>Cost.</b> One <see cref="Material"/> per look actually drawn — a few hundred bytes,
    /// textures by reference — and the SRP Batcher batches by shader variant rather than by
    /// material, so fifty of them are still one batch family. Materials are owned here and
    /// destroyed on <see cref="Dispose"/>, for the reason <c>ModuleLibrary</c> records: objects
    /// Unity allocates on the GPU are not collected, and letting them accumulate is what produced
    /// a <c>DXGI_ERROR_DEVICE_REMOVED</c> once already.</para>
    /// </summary>
    public sealed class ColonistMaterials : IDisposable
    {
        readonly struct Key : IEquatable<Key>
        {
            readonly Material _source;
            readonly AppearanceCells _cells;
            readonly uint _skin, _hair, _cloth, _cloth2;

            public Key(Material source, AppearanceCells cells, in ColonistAppearance look)
            {
                _source = source;
                _cells = cells;
                _skin = look.Skin.Packed;
                _hair = look.Hair.Packed;
                _cloth = look.Cloth.Packed;
                _cloth2 = look.Cloth2.Packed;
            }

            // The cells are part of the key by reference: two bodies sharing one pack material
            // paint different rectangles of it, so they need different materials even when they
            // happen to have been dealt the same four colours.
            public bool Equals(Key other) =>
                ReferenceEquals(_source, other._source) && ReferenceEquals(_cells, other._cells) &&
                _skin == other._skin && _hair == other._hair &&
                _cloth == other._cloth && _cloth2 == other._cloth2;

            public override bool Equals(object? obj) => obj is Key other && Equals(other);

            public override int GetHashCode() => unchecked(
                ((((System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_source) * 397) ^
                   System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_cells)) * 397 ^ (int)_skin) * 397 ^
                 (int)_hair) * 397 ^ (int)_cloth);
        }

        readonly Dictionary<Key, Material> _cache = new Dictionary<Key, Material>();
        readonly List<Material> _owned = new List<Material>();
        Shader? _shader;
        bool _warned;

        /// <summary>How many materials exist. A test watches this so a per-colonist clone fails.</summary>
        public int MaterialCount => _owned.Count;

        /// <summary>True when <c>Odyssey/Character</c> is present and colonists can be recoloured.</summary>
        public bool Available => Shader != null;

        Shader? Shader
        {
            get
            {
                if (_shader == null) _shader = UnityEngine.Shader.Find("Odyssey/Character");
                if (_shader == null && !_warned)
                {
                    _warned = true;
                    // Loudly, and once. A shader that renders magenta gets fixed; one that renders
                    // nothing at all gets shipped, which is the argument MaterialCache already
                    // makes about the water shader.
                    Debug.LogWarning("[Odyssey] Odyssey/Character not found — colonists draw in " +
                                     "the pack's own colours. Check the shader is in the build.");
                }
                return _shader;
            }
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

        static readonly int[] SkinRectIds = { Shader.PropertyToID("_SkinRect0"), Shader.PropertyToID("_SkinRect1") };
        static readonly int[] HairRectIds = { Shader.PropertyToID("_HairRect0"), Shader.PropertyToID("_HairRect1") };
        static readonly int[] ClothRectIds = { Shader.PropertyToID("_ClothRect0"), Shader.PropertyToID("_ClothRect1") };
        static readonly int[] Cloth2RectIds = { Shader.PropertyToID("_Cloth2Rect0"), Shader.PropertyToID("_Cloth2Rect1") };

        static readonly int InkColourId = Shader.PropertyToID("_InkColour");
        static readonly int InkWidthId = Shader.PropertyToID("_InkWidth");

        /// <summary>
        /// The ink line characters draw for themselves, which must match the one the rest of the
        /// world gets from <c>OutlineFeature</c>.
        ///
        /// <para>Characters cannot use the screen-space outline pass: skinned meshes are missing
        /// from the depth texture it reads, which was measured rather than assumed — a cube stood
        /// behind a colonist keeps its outline straight across the colonist. So the world is inked
        /// from depth and characters from a hull in <c>Odyssey/Character</c>, and there being two
        /// mechanisms is exactly why there must be one colour and one width. The bootstrap copies
        /// these off the real feature, and a test pins the defaults to the feature's own.</para>
        /// </summary>
        public static Color InkColour { get; set; } = new Color(0.06f, 0.09f, 0.08f, 1f);

        public static float InkWidth { get; set; } = 2.2f;

        /// <summary>
        /// Copy the ink colour and width off the real outline feature, so the two agree in the
        /// running game rather than only in their defaults.
        ///
        /// Found by asking for the loaded asset rather than by walking the pipeline asset, whose
        /// renderer features are not publicly enumerable. If it is not found, the defaults stand
        /// and a test has already pinned those to the feature's own.
        /// </summary>
        public static void AdoptInkFrom()
        {
            OutlineFeature[] features = Resources.FindObjectsOfTypeAll<OutlineFeature>();
            if (features.Length == 0) return;

            OutlineFeature feature = features[0];
            InkColour = feature.outlineColour;
            InkWidth = feature.thickness;
        }

        static readonly int SkinColourId = Shader.PropertyToID("_SkinColour");
        static readonly int HairColourId = Shader.PropertyToID("_HairColour");
        static readonly int ClothColourId = Shader.PropertyToID("_ClothColour");
        static readonly int Cloth2ColourId = Shader.PropertyToID("_Cloth2Colour");

        /// <summary>
        /// The material that draws this body wearing these colours, or <c>null</c> to leave the
        /// art alone.
        ///
        /// Null is returned — rather than a clone that happens to change nothing — whenever there
        /// is no shader, no source material, or nothing classified to repaint. The caller then
        /// draws exactly what it drew before this existed, which is what keeps a body the
        /// classifier could not read looking the way the artist painted it.
        /// </summary>
        public Material? For(Material? source, AppearanceCells? cells, in ColonistAppearance look)
        {
            if (source == null || cells == null || !cells.Any) return null;
            Shader? shader = Shader;
            if (shader == null) return null;

            var key = new Key(source, cells, look);
            if (_cache.TryGetValue(key, out Material cached)) return cached;

            var material = new Material(shader)
            {
                name = source.name + "/colonist#" + look.Cloth.Packed.ToString("x6"),
                enableInstancing = true,
            };

            Carry(source, material);
            Paint(material, cells, look);

            _cache[key] = material;
            _owned.Add(material);
            return material;
        }

        /// <summary>Copy the maps and surface values across from the pack's own material.</summary>
        static void Carry(Material source, Material material)
        {
            if (source.HasProperty(AlbedoId)) material.SetTexture(AlbedoId, source.GetTexture(AlbedoId));
            else if (source.HasProperty("_BaseMap")) material.SetTexture(AlbedoId, source.GetTexture("_BaseMap"));
            else if (source.HasProperty("_MainTex")) material.SetTexture(AlbedoId, source.GetTexture("_MainTex"));

            if (source.HasProperty(NormalId)) material.SetTexture(NormalId, source.GetTexture(NormalId));
            if (source.HasProperty(NormalAmountId)) material.SetFloat(NormalAmountId, source.GetFloat(NormalAmountId));
            if (source.HasProperty(EmissionMapId)) material.SetTexture(EmissionMapId, source.GetTexture(EmissionMapId));
            if (source.HasProperty(EmissionColourId)) material.SetColor(EmissionColourId, source.GetColor(EmissionColourId));
            if (source.HasProperty(EnableEmissionId)) material.SetFloat(EnableEmissionId, source.GetFloat(EnableEmissionId));
            if (source.HasProperty(MetallicId)) material.SetFloat(MetallicId, source.GetFloat(MetallicId));
            if (source.HasProperty(SmoothnessId)) material.SetFloat(SmoothnessId, source.GetFloat(SmoothnessId));
            if (source.HasProperty(ClipId)) material.SetFloat(ClipId, source.GetFloat(ClipId));
            if (source.HasProperty(BaseColourId)) material.SetColor(BaseColourId, source.GetColor(BaseColourId));
        }

        static void Paint(Material material, AppearanceCells cells, in ColonistAppearance look)
        {
            Write(material, SkinRectIds, cells.skin);
            Write(material, HairRectIds, cells.hair);
            Write(material, ClothRectIds, cells.cloth);
            Write(material, Cloth2RectIds, cells.cloth2);

            material.SetColor(SkinColourId, Colour(look.Skin));
            material.SetColor(HairColourId, Colour(look.Hair));
            material.SetColor(ClothColourId, Colour(look.Cloth));
            material.SetColor(Cloth2ColourId, Colour(look.Cloth2));

            material.SetColor(InkColourId, InkColour);
            material.SetFloat(InkWidthId, InkWidth);
        }

        /// <summary>
        /// Write up to two rectangles, leaving any the body does not use empty.
        ///
        /// An unused rectangle is (1,1,0,0) — minimum above maximum — which no UV can be inside.
        /// That is how a slot is switched off without the shader needing a count or a branch.
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
        /// The palette's bytes as a <see cref="Color"/>, handed over unconverted.
        ///
        /// The project renders in linear space and the tables are authored in sRGB hex — the
        /// numbers somebody would type into a colour picker — so the conversion has to happen
        /// somewhere. It happens in Unity: the shader declares these as <c>Color</c> properties,
        /// and <c>Material.SetColor</c> converts a Color property to the active colour space.
        /// Converting here as well would apply it twice and draw every colonist too dark. This is
        /// the same thing <c>MaterialCache.SetColour</c> does with the stuff tints, against these
        /// very Synty materials, which is the evidence it is right.
        /// </summary>
        static Color Colour(Rgb24 c) => new Color(c.R / 255f, c.G / 255f, c.B / 255f, 1f);

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i] != null) UnityEngine.Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            _cache.Clear();
        }
    }
}
