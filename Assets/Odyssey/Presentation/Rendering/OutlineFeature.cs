#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Draws an ink line around everything, as one fullscreen pass.
    ///
    /// This is the step towards the illustrated reference that costs the least: a screen-space
    /// edge detect is one blit whatever the scene holds, where the usual alternative — a second,
    /// inflated copy of every object — would double a draw count that the whole renderer exists
    /// to keep down. Nothing about how the world is submitted changes.
    ///
    /// It deliberately stops short of cel shading. Banded lighting was raised and rejected on
    /// 2026-09-15 (<c>06-rendering-and-camera.md</c> §1) and that decision stands: it would mean
    /// replacing the pack shaders everywhere and reconciling the bands with the depth-darkening
    /// that marks layers below the slice. The outline is the half of that look which needs neither.
    ///
    /// Added to the renderer asset by <c>Odyssey &gt; Presentation &gt; Apply render setup</c>,
    /// not by hand, so a fresh clone gets it from a command rather than from a checklist.
    /// </summary>
    [DisallowMultipleRendererFeature("Odyssey Outline")]
    public sealed class OutlineFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Odyssey/Outline";

        [Tooltip("Line colour and, in alpha, how strongly it is laid over the picture.")]
        public Color outlineColour = new Color(0.06f, 0.09f, 0.08f, 0.95f);

        [Tooltip("Line width in pixels. Above about 2 it stops reading as ink and starts as smudge.")]
        [Range(0.5f, 3f)]
        public float thickness = 2.2f;

        [Tooltip("How large a depth step counts as an edge, as a fraction of the distance to it.")]
        [Range(0.001f, 0.1f)]
        public float depthThreshold = 0.012f;

        [Tooltip("Features narrower than twice this, in pixels, get no line. Sweep 1-6 to tune.")]
        [Range(1f, 8f)]
        public float sliverRadius = 3f;

        [Tooltip("How far behind the centre a neighbour must be to count, as a fraction of distance.")]
        [Range(0.002f, 0.2f)]
        public float sliverTolerance = 0.02f;

        // Pushed right out, now that the sliver test measures the thing that actually decides
        // whether a line is legible. These are a backstop for the far corner of the board, not
        // the mechanism — a distant building should keep its outline, and before the width test
        // existed the fade was the only lever and had to kill that too.
        [Tooltip("Metres at which the line starts to fade out. A long-range backstop only.")]
        public float fadeStart = 150f;

        [Tooltip("Metres by which the line has gone entirely.")]
        public float fadeEnd = 300f;

        // Before the transparents, not before post-processing: foliage is drawn in the transparent
        // range precisely so that it lands after this pass and is never inked
        // (MaterialCache.FoliageQueue). Still before post-processing, so the ink stays out of bloom.
        [Tooltip("When the line is drawn. Before transparents, so late-drawn foliage is never inked; still before post-processing.")]
        public RenderPassEvent stage = RenderPassEvent.BeforeRenderingTransparents;

        Material? _material;
        OutlinePass? _pass;

        static readonly int OutlineColourId = Shader.PropertyToID("_OutlineColour");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");
        static readonly int SliverRadiusId = Shader.PropertyToID("_SliverRadius");
        static readonly int SliverToleranceId = Shader.PropertyToID("_SliverTolerance");

        public override void Create()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                // A missing shader must not take the renderer down with it: without this the
                // whole pipeline throws once per camera per frame and the screen goes black,
                // which looks like a far worse bug than "the outlines are missing".
                Debug.LogWarning($"[Odyssey] shader {ShaderName} not found; outlines are off.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new OutlinePass(_material) { renderPassEvent = stage };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null) return;

            // Game cameras only. The Scene view, reflection probes and asset previews all run the
            // same renderer, and none of them wants an ink line — and the Scene view is the one
            // camera no headless picture has ever exercised, which makes it the one place a
            // render-graph mistake could be hiding.
            CameraType type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game) return;

            _material.SetColor(OutlineColourId, outlineColour);
            _material.SetFloat(ThicknessId, thickness);
            _material.SetFloat(DepthThresholdId, depthThreshold);
            _material.SetFloat(FadeStartId, fadeStart);
            _material.SetFloat(FadeEndId, Mathf.Max(fadeEnd, fadeStart + 1f));
            _material.SetFloat(SliverRadiusId, sliverRadius);
            _material.SetFloat(SliverToleranceId, sliverTolerance);
            _pass.renderPassEvent = stage;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        sealed class OutlinePass : ScriptableRenderPass
        {
            readonly Material _material;

            public OutlinePass(Material material)
            {
                _material = material;
                // Say out loud that the camera colour has to be a real texture rather than the
                // back buffer, because a fullscreen pass cannot read the surface it writes to.
                requiresIntermediateTexture = true;
                // Normals as well as depth: the terrain mark rides in the normals texture's spare
                // channel (design 38 §17c). SSAO already asks for the prepass that writes it, so
                // asking here costs nothing while it is on and keeps the mark if it is ever off.
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            /// <summary>State the render graph hands back to the render function.</summary>
            class PassData
            {
                public Material Material = null!;
                public TextureHandle Source;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!source.IsValid() || !depth.IsValid()) return;

                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "Odyssey outline";
                desc.clearBuffer = false;
                desc.depthBufferBits = DepthBits.None;
                // The destination keeps the source's sample count. This pass runs before the
                // transparents, and what follows it draws into the texture it hands back against
                // the camera's own depth attachment, so the two have to pair. (It used to drop to
                // one sample to save a resolve, which was fine while nothing drew after it.)
                TextureHandle destination = renderGraph.CreateTexture(desc);

                // A raster pass rather than AddBlitPass, for two reasons that cost nothing.
                //
                // Every *material* overload of AddBlitPass is an unsafe pass — verified in the
                // shipped package — which opts the whole thing out of the graph's scheduling. And
                // the depth texture this shader reads arrives as a global, which the graph cannot
                // see: an undeclared dependency is one the compiler is free to reorder around.
                // Declaring it with UseTexture makes the graph responsible for the ordering it was
                // built to be responsible for. URP's own fullscreen feature does exactly this
                // whenever a pass takes an input.
                using (IRasterRenderGraphBuilder builder =
                       renderGraph.AddRasterRenderPass<PassData>("Odyssey outline", out PassData data))
                {
                    data.Material = _material;
                    data.Source = source;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(depth, AccessFlags.Read);
                    // Declared for the same reason as the depth: it arrives as a global.
                    TextureHandle normals = resources.cameraNormalsTexture;
                    if (normals.IsValid()) builder.UseTexture(normals, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, pass.Source,
                            new Vector4(1f, 1f, 0f, 0f), pass.Material, 0));
                }

                // Hand the result on as the camera colour, which is how a fullscreen effect
                // chains in the render graph: the next pass reads what this one wrote.
                resources.cameraColor = destination;
            }
        }
    }
}
