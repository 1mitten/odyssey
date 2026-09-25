#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// What the selection highlight draws this frame: the selected thing, as the renderers and the
    /// meshes it is actually drawn with (<c>docs/design/44-selection-highlight.md</c>).
    ///
    /// <para><b>Filled by the composition root and read by <see cref="SelectionHighlightFeature"/></b>,
    /// through the one static <see cref="Current"/>, because a renderer feature is an asset and has no
    /// other way to hear from a scene. Cleared at the top of every drawn frame, so a selection that
    /// ends leaves nothing behind and an empty list is the feature's "draw nothing".</para>
    ///
    /// <para>Nothing here is a cell, a save or the hash: it is the picture of a selection.</para>
    /// </summary>
    public sealed class SelectionHighlight
    {
        /// <summary>The one the game fills. Tests make their own.</summary>
        public static SelectionHighlight Current { get; } = new SelectionHighlight();

        /// <summary>
        /// The camera the highlight belongs to. Null draws on no camera at all: the portrait
        /// studio, the title screen's bed and the Scene view all run the same renderer, and none of
        /// them is looking at the selection.
        /// </summary>
        public static Camera? Camera { get; set; }

        /// <summary>
        /// The frame the feature last said it was present on. The composition root falls back to
        /// the brackets unless this is recent, so a renderer asset without the feature — a clone
        /// where <c>Apply render setup</c> was never run, a test camera — never leaves a selection
        /// invisible.
        /// </summary>
        public static int FeatureSeenFrame { get; set; } = -100;

        /// <summary>Whether the feature has been present within the last couple of frames.</summary>
        public static bool FeaturePresent => Time.frameCount - FeatureSeenFrame <= 2;

        /// <summary>
        /// Strength of a selection's line. Every member of a group is drawn at
        /// <see cref="Primary"/> (owner, 2026-09-25); a weaker strength is kept for a later use
        /// such as a hover, and is drawn as a fainter line and nothing else.
        /// </summary>
        public const float Primary = 1f, Secondary = 0.45f;

        /// <summary>
        /// Whether the selection is brightened as well as outlined this frame. True for one thing,
        /// false for a group of colonists (owner, 2026-09-25). Put back to true by <see cref="Clear"/>.
        /// </summary>
        public bool Lifted { get; set; } = true;

        /// <summary>One renderer drawn as it is, skinning and all.</summary>
        public struct RendererDraw
        {
            public Renderer Renderer;
            public float Strength;
        }

        /// <summary>One mesh at one matrix, as the instanced passes draw it.</summary>
        public struct MeshDraw
        {
            public Mesh Mesh;
            public int Submesh;
            public Matrix4x4 Matrix;
            public float Strength;

            /// <summary>1 for a tile's top face, which is washed as well as outlined.</summary>
            public float Fill;

            /// <summary>The material the part is drawn with, for its albedo and cut-off, or null.</summary>
            public Material? Source;
        }

        readonly List<RendererDraw> _renderers = new List<RendererDraw>();
        readonly List<MeshDraw> _meshes = new List<MeshDraw>();
        Bounds _bounds;
        bool _any;

        public IReadOnlyList<RendererDraw> Renderers => _renderers;
        public IReadOnlyList<MeshDraw> Meshes => _meshes;

        public bool IsEmpty => !_any;

        /// <summary>Every draw's world bounds together: what the composite is scissored to.</summary>
        public Bounds Bounds => _bounds;

        public int Count => _renderers.Count + _meshes.Count;

        public void Clear()
        {
            _renderers.Clear();
            _meshes.Clear();
            _any = false;
            _bounds = default;
            Lifted = true;
        }

        void Grow(in Bounds b)
        {
            if (!_any) { _bounds = b; _any = true; }
            else _bounds.Encapsulate(b);
        }

        public void AddRenderer(Renderer renderer, float strength)
        {
            if (renderer == null) return;
            _renderers.Add(new RendererDraw { Renderer = renderer, Strength = strength });
            Grow(renderer.bounds);
        }

        readonly List<Renderer> _scratch = new List<Renderer>();

        /// <summary>
        /// Every renderer under <paramref name="root"/> that is drawing this frame: a figure's
        /// skins, hair, beard, headgear, the weapon at the hip or in the hand and a tool in use.
        /// Particles and shadow-only renderers are not the thing and are left out.
        /// </summary>
        public int AddRenderers(GameObject? root, float strength)
        {
            if (root == null || !root.activeInHierarchy) return 0;
            _scratch.Clear();
            root.GetComponentsInChildren(false, _scratch);
            int added = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                Renderer r = _scratch[i];
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                if (r.forceRenderingOff) continue;
                AddRenderer(r, strength);
                added++;
            }
            _scratch.Clear();
            return added;
        }

        public void AddMesh(Mesh? mesh, int submesh, in Matrix4x4 matrix, float strength,
            Material? source = null, float fill = 0f)
        {
            if (mesh == null) return;
            _meshes.Add(new MeshDraw
            {
                Mesh = mesh, Submesh = submesh, Matrix = matrix, Strength = strength, Fill = fill, Source = source,
            });
            Grow(GeometryUtility.CalculateBounds(BoxCorners(mesh.bounds), matrix));
        }

        /// <summary>Every part of a module at one placement, exactly as <c>SubmitInstances</c> draws it.</summary>
        public void AddModule(ResolvedModule module, in Matrix4x4 placement, float strength)
        {
            ModulePart[] parts = module.Parts;
            for (int p = 0; p < parts.Length; p++)
                AddMesh(parts[p].Mesh, parts[p].Submesh, placement * parts[p].Local, strength, parts[p].Material);
        }

        readonly Vector3[] _corners = new Vector3[8];

        Vector3[] BoxCorners(in Bounds b)
        {
            Vector3 min = b.min, max = b.max;
            for (int i = 0; i < 8; i++)
                _corners[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
            return _corners;
        }

        /// <summary>
        /// The part of the screen the selection covers, as a viewport rectangle (0 to 1), padded by
        /// <paramref name="padPixels"/>; the whole screen when any of it is behind the camera.
        /// </summary>
        public Rect ViewportRect(Camera camera, float padPixels)
        {
            if (!_any) return new Rect(0f, 0f, 0f, 0f);
            Vector3 min = _bounds.min, max = _bounds.max;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 v = camera.WorldToViewportPoint(corner);
                if (v.z <= camera.nearClipPlane) return new Rect(0f, 0f, 1f, 1f);
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x);
                y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y);
            }
            float padX = padPixels / Mathf.Max(1, camera.pixelWidth);
            float padY = padPixels / Mathf.Max(1, camera.pixelHeight);
            x0 = Mathf.Clamp01(x0 - padX); x1 = Mathf.Clamp01(x1 + padX);
            y0 = Mathf.Clamp01(y0 - padY); y1 = Mathf.Clamp01(y1 + padY);
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        // ---- the albedo and cut-off of a part ---------------------------------------------------

        static readonly Dictionary<Material, (Texture? texture, float cutoff)> Clip =
            new Dictionary<Material, (Texture?, float)>();

        static readonly string[] AlbedoNames = { "_BaseMap", "_MainTex", "_Albedo_Map", "_AlbedoMap", "_Albedo" };
        static readonly string[] CutoffNames = { "_Cutoff", "_AlphaClipThreshold", "_AlphaCutoff" };

        /// <summary>
        /// The texture and cut-off a part is alpha-clipped by, or (null, 0) for an opaque part.
        /// Asked once per material and kept: a leaf card drawn into the mask as a whole quad would
        /// outline a tree as a stack of rectangles.
        /// </summary>
        public static (Texture? texture, float cutoff) ClipOf(Material? material)
        {
            if (material == null) return (null, 0f);
            if (Clip.TryGetValue(material, out var known)) return known;

            bool clipped = material.IsKeywordEnabled("_ALPHATEST_ON")
                           || (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
                           || material.renderQueue == (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            (Texture?, float) answer = (null, 0f);
            if (clipped)
            {
                Texture? texture = null;
                foreach (string name in AlbedoNames)
                    if (material.HasProperty(name) && (texture = material.GetTexture(name)) != null) break;
                float cutoff = 0.5f;
                foreach (string name in CutoffNames)
                    if (material.HasProperty(name)) { cutoff = material.GetFloat(name); break; }
                if (texture != null) answer = (texture, cutoff);
            }
            Clip[material] = answer;
            return answer;
        }
    }
}
