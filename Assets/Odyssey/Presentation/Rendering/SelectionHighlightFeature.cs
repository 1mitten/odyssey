#nullable enable
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Lights the selected thing at its own edges: a line round its silhouette, a lift over it, and
    /// a wash over a selected tile (<c>docs/design/44-selection-highlight.md</c>).
    ///
    /// <para><b>Two passes, and none at all when nothing is selected.</b> The mask draws only the
    /// selected thing again, against the camera's own depth, so it knows which of it is seen and
    /// which is behind a wall; the composite lays the line over the frame by alpha blending,
    /// scissored to the selection's rectangle. Neither reads the scene, so neither needs a copy of
    /// it. The drawing list is <see cref="SelectionHighlight.Current"/>, which the composition root
    /// fills every frame.</para>
    ///
    /// <para>Added to the renderer asset by <c>Odyssey &gt; Presentation &gt; Apply render setup</c>,
    /// like <see cref="OutlineFeature"/>, which owns its tuning too.</para>
    /// </summary>
    [DisallowMultipleRendererFeature("Odyssey Selection Highlight")]
    public sealed class SelectionHighlightFeature : ScriptableRendererFeature
    {
        public const string MaskShaderName = "Odyssey/SelectionMask";
        public const string CompositeShaderName = "Odyssey/SelectionComposite";

        [Tooltip("The highlight's colour. Above 1 so the line survives the tonemapper as white.")]
        [ColorUsage(false, true)]
        public Color colour = new Color(1.25f, 1.25f, 1.25f, 1f);

        [Tooltip("Line width in pixels at 1080 lines; scaled with the height of the frame.")]
        [Range(1f, 6f)]
        public float width = 2.5f;

        [Tooltip("How strongly the line shows where the thing is behind something.")]
        [Range(0f, 1f)]
        public float hiddenStrength = 0.4f;

        [Tooltip("How much brighter the visible part of the thing is drawn, as a fraction. Multiplied, so its colour is kept.")]
        [Range(0f, 0.4f)]
        public float lift = 0.12f;

        [Tooltip("How strongly a selected tile's top face is washed.")]
        [Range(0f, 0.6f)]
        public float fill = 0.16f;

        [Tooltip("When it is drawn: after the transparents, so it lies over grass and water; before post-processing.")]
        public RenderPassEvent stage = RenderPassEvent.AfterRenderingTransparents;

        Material? _mask;
        Material? _composite;
        HighlightPass? _pass;

        static readonly int ColourId = Shader.PropertyToID("_SelectionColour");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int HiddenId = Shader.PropertyToID("_HiddenAlpha");
        static readonly int LiftId = Shader.PropertyToID("_Lift");
        static readonly int FillAlphaId = Shader.PropertyToID("_FillAlpha");

        /// <summary>The frame height the width is quoted at.</summary>
        public const float ReferenceHeight = 1080f;

        public override void Create()
        {
            Shader mask = Shader.Find(MaskShaderName);
            Shader composite = Shader.Find(CompositeShaderName);
            if (mask == null || composite == null)
            {
                // The brackets take over (SelectionHighlight.FeaturePresent), so a missing shader
                // costs the look and nothing else.
                Debug.LogWarning("[Odyssey] selection highlight shaders not found; the brackets are used instead.");
                return;
            }

            _mask = CoreUtils.CreateEngineMaterial(mask);
            _mask.SetFloat(Shader.PropertyToID("_SelectionCutoff"), 0f);
            _composite = CoreUtils.CreateEngineMaterial(composite);
            _pass = new HighlightPass(_mask, _composite) { renderPassEvent = stage };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _mask == null || _composite == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            Camera camera = renderingData.cameraData.camera;
            if (SelectionHighlight.Camera == null || camera != SelectionHighlight.Camera) return;

            // Present, whether or not there is anything to draw: this is what keeps the brackets off.
            SelectionHighlight.FeatureSeenFrame = Time.frameCount;

            SelectionHighlight frame = SelectionHighlight.Current;
            if (frame.IsEmpty) return;

            float height = renderingData.cameraData.cameraTargetDescriptor.height;
            float radius = width * Mathf.Max(0.5f, height / ReferenceHeight);

            _composite.SetColor(ColourId, colour);
            _composite.SetFloat(RadiusId, radius);
            _composite.SetFloat(HiddenId, hiddenStrength);
            _composite.SetFloat(LiftId, lift);
            _composite.SetFloat(FillAlphaId, fill);

            _pass.Frame = frame;
            _pass.Viewport = frame.ViewportRect(camera, radius + 2f);
            _pass.renderPassEvent = stage;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            HighlightPass.ReleaseClipped();
            CoreUtils.Destroy(_mask);
            CoreUtils.Destroy(_composite);
            _mask = _composite = null;
            _pass = null;
        }

        sealed class HighlightPass : ScriptableRenderPass
        {
            readonly Material _mask;
            readonly Material _composite;

            public SelectionHighlight Frame = SelectionHighlight.Current;
            public Rect Viewport;

            static readonly int StrengthId = Shader.PropertyToID("_SelectionStrength");
            static readonly int FillId = Shader.PropertyToID("_SelectionFill");
            static readonly int CutoffId = Shader.PropertyToID("_SelectionCutoff");
            static readonly int AlbedoId = Shader.PropertyToID("_SelectionAlbedo");

            const int VisiblePass = 0, SilhouettePass = 1;
            const int LinePass = 0, LiftPass = 1;

            static readonly System.Collections.Generic.List<Material> Shared = new System.Collections.Generic.List<Material>();

            public HighlightPass(Material mask, Material composite)
            {
                _mask = mask;
                _composite = composite;
            }

            class MaskData
            {
                public SelectionHighlight Frame = null!;
                public Material Material = null!;
            }

            class CompositeData
            {
                public TextureHandle Mask;
                public Material Material = null!;
                public Rect Scissor;
                public bool Lift;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                TextureHandle colour = resources.activeColorTexture;
                TextureHandle depth = resources.activeDepthTexture;
                if (!colour.IsValid() || !depth.IsValid()) return;

                // The mask is the depth's size and sample count, because the depth is its depth
                // attachment; a render scale below one shrinks both together.
                TextureDesc colourDesc = renderGraph.GetTextureDesc(colour);
                var desc = new TextureDesc(colourDesc.width, colourDesc.height)
                {
                    name = "Odyssey selection mask",
                    format = GraphicsFormat.R8G8B8A8_UNorm,
                    msaaSamples = colourDesc.msaaSamples,
                    bindTextureMS = false,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                TextureHandle mask = renderGraph.CreateTexture(desc);

                using (IRasterRenderGraphBuilder builder =
                       renderGraph.AddRasterRenderPass<MaskData>("Odyssey selection mask", out MaskData data))
                {
                    data.Frame = Frame;
                    data.Material = _mask;
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((MaskData pass, RasterGraphContext context) => DrawMask(pass, context.cmd));
                }

                // In pixels of the target, which a render scale makes smaller than the screen.
                var scissor = new Rect(
                    Viewport.x * colourDesc.width, Viewport.y * colourDesc.height,
                    Viewport.width * colourDesc.width, Viewport.height * colourDesc.height);
                if (scissor.width < 1f || scissor.height < 1f) return;

                using (IRasterRenderGraphBuilder builder =
                       renderGraph.AddRasterRenderPass<CompositeData>("Odyssey selection highlight", out CompositeData data))
                {
                    data.Mask = mask;
                    data.Material = _composite;
                    data.Scissor = scissor;
                    data.Lift = Frame.Lifted;
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(colour, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((CompositeData pass, RasterGraphContext context) =>
                    {
                        context.cmd.EnableScissorRect(pass.Scissor);
                        Blitter.BlitTexture(context.cmd, pass.Mask, new Vector4(1f, 1f, 0f, 0f), pass.Material, LinePass);
                        if (pass.Lift)
                            Blitter.BlitTexture(context.cmd, pass.Mask, new Vector4(1f, 1f, 0f, 0f), pass.Material, LiftPass);
                        context.cmd.DisableScissorRect();
                    });
                }
            }

            static void DrawMask(MaskData pass, RasterCommandBuffer cmd)
            {
                SelectionHighlight frame = pass.Frame;
                Material opaque = pass.Material;

                var renderers = frame.Renderers;
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer renderer = renderers[i].Renderer;
                    if (renderer == null) continue;
                    renderer.GetSharedMaterials(Shared);
                    var shared = Shared;
                    int submeshes = SubmeshCount(renderer, shared.Count);
                    for (int s = 0; s < submeshes; s++)
                    {
                        Material material = MaskFor(opaque, s < shared.Count ? shared[s] : null);
                        SetDraw(cmd, renderers[i].Strength, 0f);
                        cmd.DrawRenderer(renderer, material, s, VisiblePass);
                        cmd.DrawRenderer(renderer, material, s, SilhouettePass);
                    }
                }

                var meshes = frame.Meshes;
                for (int i = 0; i < meshes.Count; i++)
                {
                    SelectionHighlight.MeshDraw draw = meshes[i];
                    if (draw.Mesh == null) continue;
                    Material material = MaskFor(opaque, draw.Source);
                    SetDraw(cmd, draw.Strength, draw.Fill);
                    cmd.DrawMesh(draw.Mesh, draw.Matrix, material, draw.Submesh, VisiblePass);
                    cmd.DrawMesh(draw.Mesh, draw.Matrix, material, draw.Submesh, SilhouettePass);
                }
            }

            static int SubmeshCount(Renderer renderer, int materials)
            {
                Mesh? mesh = renderer switch
                {
                    SkinnedMeshRenderer skin => skin.sharedMesh,
                    MeshRenderer => renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null,
                    _ => null,
                };
                int count = mesh != null ? mesh.subMeshCount : materials;
                return Mathf.Max(1, Mathf.Min(count, Mathf.Max(1, materials)));
            }

            static void SetDraw(RasterCommandBuffer cmd, float strength, float fill)
            {
                cmd.SetGlobalFloat(StrengthId, strength);
                cmd.SetGlobalFloat(FillId, fill);
            }

            /// <summary>
            /// The mask material for a part: the opaque one, or a copy carrying the albedo and
            /// cut-off of alpha-clipped art (a leaf card), made once per texture and kept. On a copy
            /// rather than a global, because a render-graph pass cannot bind a plain texture globally.
            /// </summary>
            static Material MaskFor(Material opaque, Material? source)
            {
                (Texture? texture, float cutoff) = SelectionHighlight.ClipOf(source);
                if (texture == null) return opaque;
                if (Clipped.TryGetValue(texture, out Material? known) && known != null) return known;
                var made = new Material(opaque) { name = "Selection mask (" + texture.name + ")", hideFlags = HideFlags.DontSave };
                made.SetTexture(AlbedoId, texture);
                made.SetFloat(CutoffId, Mathf.Max(0.001f, cutoff));
                Clipped[texture] = made;
                return made;
            }

            static readonly System.Collections.Generic.Dictionary<Texture, Material> Clipped =
                new System.Collections.Generic.Dictionary<Texture, Material>();

            /// <summary>Destroys the per-texture copies, with the feature.</summary>
            public static void ReleaseClipped()
            {
                foreach (Material material in Clipped.Values) CoreUtils.Destroy(material);
                Clipped.Clear();
            }
        }
    }
}
